using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BetterDesktopPopup
{
    /// <summary>
    /// 级联式弹出窗口
    /// 显示单层文件夹内容（ListView），悬停子文件夹时在右侧新建同款式窗口
    /// </summary>
    public class PopupForm : Form
    {
        // ===== 控件 =====
        private ListView _listView;          // 文件列表（单层，不递归）
        private ImageList _imageList;        // 图标缓存列表（24x24 真实图标）
        private ImageList _displayList;      // 显示列表（24x44，图标居中，驱动行高 44px）
        private Dictionary<string, int> _iconIndexMap = new Dictionary<string, int>(); // iconKey → displayList 索引
        private PopupForm _childPopup;       // 级联子窗口

        // ===== 定时器 =====
        private Timer _hoverTimer;           // 悬停检测：鼠标在文件夹上停留 300ms 后打开子窗口
        private Timer _childCloseTimer;      // 子窗口延迟关闭：离开文件夹项 1.5s 后关闭

        // ===== 状态 =====
        private string _currentFolder;       // 本窗口显示的文件夹路径
        private ListViewItem _hoveredItem;   // 当前悬停的列表项
        private DateTime _hoverStartTime;
        private IntPtr _explorerHwnd;        // 所属资源管理器窗口句柄（用于新增标签页）

        // ===== 样式常量 =====
        private static readonly Color ColorFormBg = Color.FromArgb(252, 252, 252);
        private static readonly Font FontNormal = new Font("Segoe UI", 18f);  // 文字大小 18pt

        // ===== 窗口尺寸 =====
        private const int FormWidth = 420;
        private const int FormHeight = 520;
        private const int ItemHeight = 44;
        private const int IconSize = 24;
        private const int ExtraWidth = 144;

        public PopupForm()
        {
            SetupForm();
            SetupControls();
            SetupImageList();
            SetupTimers();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Size = new Size(FormWidth, FormHeight);
            this.BackColor = ColorFormBg;
            this.DoubleBuffered = true;
            TrySetRoundedCorners();
        }

        private void TrySetRoundedCorners()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 10)
                {
                    int preference = NativeMethods.DWMWCP_ROUND;
                    NativeMethods.DwmSetWindowAttribute(
                        this.Handle,
                        NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                        ref preference,
                        sizeof(int));
                }
            }
            catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private void SetupControls()
        {
            // 使用标准 ListView（显示滚动条，滚轮可滚动）
            // OwnerDraw = false：使用 Windows 原生绘制，悬停/选中颜色由 Win11 主题决定
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                Font = FontNormal,
                BackColor = ColorFormBg,
                BorderStyle = BorderStyle.None,
                View = View.Details,
                HeaderStyle = ColumnHeaderStyle.None,
                FullRowSelect = true,
                HideSelection = false,
                OwnerDraw = false,          // 原生绘制（Win11 主题）
                MultiSelect = false,
                Scrollable = true
            };

            // 创建句柄后应用原生主题 + 原生悬停高亮
            _listView.HandleCreated += (s, e) =>
            {
                // 设置 Explorer 主题（Win11 悬停/选中色与资源管理器一致）
                NativeMethods.SetWindowTheme(_listView.Handle, "Explorer", null);
                // 启用原生悬停高亮（LVS_EX_TRACKSELECT）
                NativeMethods.SendMessage(_listView.Handle,
                    NativeMethods.LVM_SETEXTENDEDLISTVIEWSTYLE,
                    (IntPtr)NativeMethods.LVS_EX_TRACKSELECT,
                    (IntPtr)NativeMethods.LVS_EX_TRACKSELECT);
            };

            // 添加单列（文件名列）
            _listView.Columns.Add("Name", _listView.ClientSize.Width - 20);

            // 事件（悬停检测和双击打开仍在原生绘制下工作）
            _listView.MouseMove += ListView_MouseMove;
            _listView.MouseLeave += ListView_MouseLeave;
            _listView.MouseDoubleClick += ListView_MouseDoubleClick;
            _listView.Resize += (s, e) => {
                if (_listView.Columns.Count > 0)
                    _listView.Columns[0].Width = _listView.ClientSize.Width - 20;
            };

            this.Controls.Add(_listView);
        }

        private void SetupImageList()
        {
            // 图标缓存列表（24x24 真实图标）— 按扩展名缓存
            _imageList = new ImageList
            {
                ImageSize = new Size(IconSize, IconSize),
                ColorDepth = ColorDepth.Depth32Bit
            };

            // 默认图标
            var folderIcon = NativeMethods.GetShellIcon("__generic_folder__", false, true);
            if (folderIcon != null)
                _imageList.Images.Add("folder", folderIcon);

            var fileIcon = NativeMethods.GetShellIcon("dummy.txt", false);
            if (fileIcon != null)
                _imageList.Images.Add("file", fileIcon);

            // 显示列表（24x44）：24x24 图标绘制到 24x44 透明位图（上下留白居中）
            // 该列表高度 44px 驱动 ListView 行高，图标 24x24 原生居中显示
            _displayList = new ImageList
            {
                ImageSize = new Size(IconSize, ItemHeight),
                ColorDepth = ColorDepth.Depth32Bit,
                TransparentColor = Color.Transparent
            };
            _listView.SmallImageList = _displayList;
            _listView.LargeImageList = _displayList;

            // 预加载默认图标的显示位图
            EnsureDisplayIcon("folder");
            EnsureDisplayIcon("file");
        }

        /// <summary>
        /// 确保 iconKey 对应的 24x44 显示位图存在，返回 displayList 索引
        /// </summary>
        private int EnsureDisplayIcon(string iconKey)
        {
            if (_iconIndexMap.ContainsKey(iconKey))
                return _iconIndexMap[iconKey];

            // 从 _imageList 取 24x24 图标
            if (!_imageList.Images.ContainsKey(iconKey))
                return 0;

            // 绘制 24x24 图标到 24x44 位图（垂直居中）
            var bmp = new Bitmap(IconSize, ItemHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                int iconY = (ItemHeight - IconSize) / 2; // 上边距 10px
                g.DrawImage(_imageList.Images[iconKey], 0, iconY, IconSize, IconSize);
            }

            _displayList.Images.Add(bmp);
            int index = _displayList.Images.Count - 1;
            _iconIndexMap[iconKey] = index;
            return index;
        }

        private void SetupTimers()
        {
            _hoverTimer = new Timer { Interval = 300 };
            _hoverTimer.Tick += HoverTimer_Tick;

            _childCloseTimer = new Timer { Interval = Settings.ChildCloseMs };
            _childCloseTimer.Tick += ChildCloseTimer_Tick;
        }

        /// <summary>
        /// 显示此窗口，展示指定文件夹的内容
        /// </summary>
        public void ShowForFolder(string folderPath, int mouseX, int mouseY)
        {
            ShowForFolder(folderPath, mouseX, mouseY, IntPtr.Zero);
        }

        public void ShowForFolder(string folderPath, int mouseX, int mouseY, IntPtr explorerHwnd)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                Logger.Write("[PopupForm] 无效路径: " + folderPath);
                return;
            }

            _currentFolder = folderPath;
            _explorerHwnd = explorerHwnd;
            PopulateList(folderPath);

            // 无级联子窗口时调整宽度
            if (_childPopup == null)
                AdjustWidthToContent();

            // 定位
            this.Location = new Point(mouseX, mouseY);

            // 置顶显示
            this.TopMost = true;
            if (!this.Visible)
                this.Show();
            else
                this.BringToFront();

            NativeMethods.SetWindowPos(this.Handle,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);

            Logger.Write("[PopupForm] 显示: " + folderPath);
        }

        /// <summary>
        /// 填充文件列表到 ListView
        /// </summary>
        private void PopulateList(string folderPath)
        {
            _listView.BeginUpdate();
            _listView.Items.Clear();

            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                if (!dirInfo.Exists) return;

                var entries = dirInfo.GetFileSystemInfos();

                // 先列文件夹，再列文件
                foreach (var entry in entries)
                {
                    bool isDir = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                    if (!isDir) continue;
                    AddListViewItem(entry, true);
                }
                foreach (var entry in entries)
                {
                    bool isDir = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                    if (isDir) continue;
                    AddListViewItem(entry, false);
                }
            }
            catch (UnauthorizedAccessException)
            {
                _listView.Items.Add(new ListViewItem("(无权限访问)"));
            }
            catch (Exception ex)
            {
                _listView.Items.Add(new ListViewItem("(加载失败: " + ex.Message + ")"));
            }

            _listView.EndUpdate();
        }

        private void AddListViewItem(FileSystemInfo entry, bool isDirectory)
        {
            string fullPath = entry.FullName;
            string iconKey = GetIconKey(fullPath, isDirectory);

            // 如果图标不在缓存中，加载它
            if (!_imageList.Images.ContainsKey(iconKey))
            {
                Icon icon = NativeMethods.GetShellIcon(fullPath, false, isDirectory);
                if (icon != null)
                    _imageList.Images.Add(iconKey, icon);
                else
                    iconKey = isDirectory ? "folder" : "file";
            }

            // 确保 24x44 显示位图存在，获取 displayList 索引
            int displayIndex = EnsureDisplayIcon(iconKey);

            var item = new ListViewItem(entry.Name);
            item.ImageIndex = displayIndex;  // 原生绘制显示 24x24 图标（居中于 44px 行）
            item.Tag = fullPath;             // 存储完整路径
            _listView.Items.Add(item);
        }

        private string GetIconKey(string path, bool isDirectory)
        {
            if (isDirectory) return "folder";
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return "file";
            return ext.ToLowerInvariant();
        }

        /// <summary>
        /// 调整窗口宽度适配内容，无横向滚动条
        /// </summary>
        private void AdjustWidthToContent()
        {
            int maxWidth = 0;
            using (Graphics g = _listView.CreateGraphics())
            {
                foreach (ListViewItem item in _listView.Items)
                {
                    int textWidth = (int)g.MeasureString(item.Text, FontNormal).Width;
                    int total = textWidth + IconSize + 20 + SystemInformation.VerticalScrollBarWidth;
                    if (total > maxWidth) maxWidth = total;
                }
            }
            int newWidth = maxWidth + ExtraWidth;
            if (newWidth < 200) newWidth = 200;
            if (newWidth > 600) newWidth = 600;
            this.Width = newWidth;
            if (_listView.Columns.Count > 0)
                _listView.Columns[0].Width = _listView.ClientSize.Width - 20;
        }

        /// <summary>
        /// 隐藏此窗口并关闭所有级联子窗口
        /// </summary>
        public void HidePopup()
        {
            _childCloseTimer.Stop();
            CloseChildPopup();
            _hoverTimer.Stop();
            this.Hide();
            Logger.Write("[PopupForm] 隐藏");
        }

        private void ChildCloseTimer_Tick(object sender, EventArgs e)
        {
            _childCloseTimer.Stop();

            // 若鼠标仍在任意级联窗口上，不关闭（由 MouseHook 统一管理生命周期）
            NativeMethods.POINT pt;
            NativeMethods.GetCursorPos(out pt);
            IntPtr hwnd = NativeMethods.WindowFromPoint(pt);
            if (ContainsWindow(hwnd))
                return;

            CloseChildPopup();
        }

        private void CloseChildPopup()
        {
            if (_childPopup != null)
            {
                _childPopup.CloseChildPopup();
                _childPopup.HidePopup();
                _childPopup.Dispose();
                _childPopup = null;
            }
        }

        /// <summary>
        /// 检查指定窗口句柄是否属于本窗口或其级联子窗口
        /// </summary>
        public bool ContainsWindow(IntPtr hwnd)
        {
            if (hwnd == this.Handle)
                return true;

            // 检查是否是本窗口的子窗口
            IntPtr current = hwnd;
            while (current != IntPtr.Zero)
            {
                if (current == this.Handle)
                    return true;
                current = NativeMethods.GetParent(current);
            }

            // 检查级联子窗口
            if (_childPopup != null && _childPopup.ContainsWindow(hwnd))
                return true;

            return false;
        }

        // ==================== 悬停检测：打开级联子窗口 ====================

        private void ListView_MouseMove(object sender, MouseEventArgs e)
        {
            var item = _listView.GetItemAt(e.X, e.Y);
            if (item != _hoveredItem)
            {
                _hoveredItem = item;
                if (item != null && item.Tag is string path && Directory.Exists(path))
                {
                    _hoverStartTime = DateTime.Now;
                    _childCloseTimer.Stop();   // 回到文件夹，取消延迟关闭
                    _hoverTimer.Start();
                }
                else
                {
                    _hoverTimer.Stop();
                    // 离开文件夹项：按当前配置延迟关闭子窗口
                    if (_childPopup != null)
                    {
                        _childCloseTimer.Stop();
                        _childCloseTimer.Interval = Settings.ChildCloseMs;
                        _childCloseTimer.Start();
                    }
                }
            }
        }

        private void ListView_MouseLeave(object sender, EventArgs e)
        {
            _hoveredItem = null;
            _hoverTimer.Stop();
            // 不关闭子窗口！鼠标可能移入子窗口，由 MouseHook 统一管理生命周期
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();

            if (_hoveredItem == null) return;
            string folderPath = _hoveredItem.Tag as string;
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

            // 打开级联子窗口（在右侧）
            if (_childPopup == null || _childPopup._currentFolder != folderPath)
            {
                CloseChildPopup();

                _childPopup = new PopupForm();
                // 定位到本窗口右侧，紧贴无间隔
                int childX = this.Right;
                int childY = this.Top;
                _childPopup.ShowForFolder(folderPath, childX, childY, _explorerHwnd);
                Logger.Write("[PopupForm] 级联子窗口: " + folderPath);
            }
        }

        // ==================== 双击打开 ====================

        private void ListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var item = _listView.GetItemAt(e.X, e.Y);
            if (item == null) return;
            string path = item.Tag as string;
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (Directory.Exists(path))
                {
                    OpenFolderInNewTab(path);
                }
                else if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Write("[PopupForm] 双击打开失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 在当前资源管理器窗口中新增标签页打开文件夹（Win11 标签页）
        /// 找不到所属窗口时回退为新窗口打开
        /// </summary>
        private void OpenFolderInNewTab(string folderPath)
        {
            IntPtr target = _explorerHwnd;
            if (target == IntPtr.Zero)
                target = NativeMethods.GetForegroundWindow();

            // 用 Shell COM 枚举资源管理器窗口，在所属窗口新增标签页
            try
            {
                Type shellWindowsType = Type.GetTypeFromCLSID(
                    new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"));
                if (shellWindowsType != null)
                {
                    object shellWindows = Activator.CreateInstance(shellWindowsType);
                    object firstExplorer = null;
                    var enumerable = shellWindows as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (object w in enumerable)
                        {
                            if (w == null) continue;
                            object h = w.GetType().InvokeMember("HWND",
                                System.Reflection.BindingFlags.GetProperty, null, w, null);
                            if (h == null) continue;
                            IntPtr wh = (IntPtr)Convert.ToInt32(h);
                            if (wh == target)
                            {
                                OpenTabInShellWindow(w, folderPath);
                                Marshal.ReleaseComObject(shellWindows);
                                return;
                            }
                            // 记录第一个资源管理器窗口作为回退
                            if (firstExplorer == null)
                                firstExplorer = w;
                        }
                        // 回退：在任一资源管理器窗口新增标签页
                        if (firstExplorer != null)
                        {
                            OpenTabInShellWindow(firstExplorer, folderPath);
                            Marshal.ReleaseComObject(shellWindows);
                            return;
                        }
                    }
                    Marshal.ReleaseComObject(shellWindows);
                }
            }
            catch (Exception ex)
            {
                Logger.Write("[PopupForm] 新增标签页失败: " + ex.Message);
            }

            // 回退：新窗口打开
            try { Process.Start("explorer.exe", folderPath); }
            catch (Exception ex) { Logger.Write("[PopupForm] 打开失败: " + ex.Message); }
        }

        /// <summary>
        /// 在指定 Shell 窗口对象中新增标签页（navOpenInNewTab = 0x0800）
        /// </summary>
        private void OpenTabInShellWindow(object w, string folderPath)
        {
            w.GetType().InvokeMember("Navigate2",
                System.Reflection.BindingFlags.InvokeMethod, null, w,
                new object[] { folderPath, 0x0800, null, null, null });
            Logger.Write("[PopupForm] 新增标签页: " + folderPath);
        }

        /// <summary>
        /// 释放资源
        /// 注意：FontNormal 是静态字段，进程退出时才释放，不能在这里 Dispose
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hoverTimer?.Dispose();
                _childCloseTimer?.Dispose();
                _imageList?.Dispose();
                // 释放显示列表（24x44）
                _displayList?.Dispose();
                _displayList = null;
                _iconIndexMap.Clear();
                CloseChildPopup();
            }
            base.Dispose(disposing);
        }
    }
}