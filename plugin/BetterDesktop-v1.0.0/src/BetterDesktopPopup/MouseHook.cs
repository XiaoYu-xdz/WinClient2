using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BetterDesktopPopup
{
    internal class MouseHook : IDisposable
    {
        private NativeMethods.LowLevelMouseProc _hookProc;
        private IntPtr _hookId = IntPtr.Zero;
        private readonly PopupForm _popupForm;
        private Timer _detectTimer;
        private IntPtr _lastHwnd = IntPtr.Zero;
        private string _currentFolder = string.Empty;
        private bool _windowVisible = false;
        private DateTime _lastOnPopupTime = DateTime.MinValue; // 上次鼠标在弹出窗口上的时间
        private DateTime _folderLeaveTime = DateTime.MinValue; // 鼠标离开文件夹的时间（用于消失延迟）
        private IntPtr _currentExplorerHwnd = IntPtr.Zero;    // 当前悬停的资源管理器窗口句柄

        public MouseHook(PopupForm popupForm)
        {
            _popupForm = popupForm;
        }

        public void Install()
        {
            _hookProc = MouseHookCallback;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _hookId = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_MOUSE_LL, _hookProc,
                    NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
            Logger.Write(_hookId != IntPtr.Zero ? "[MouseHook] 已安装" : "[MouseHook] 安装失败!");

            // 定时器 50ms，最快响应速度
            _detectTimer = new Timer { Interval = 50 };
            _detectTimer.Tick += DetectTimer_Tick;
            _detectTimer.Start();
        }

        public void Uninstall()
        {
            _detectTimer?.Stop();
            _detectTimer?.Dispose();
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        // 钩子回调：只记录鼠标位置
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == NativeMethods.WM_MOUSEMOVE)
            {
                NativeMethods.POINT pt;
                NativeMethods.GetCursorPos(out pt);
                _lastHwnd = NativeMethods.WindowFromPoint(pt);
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // 定时器：检测 Shift 状态和文件夹，管理窗口显示/隐藏
        private void DetectTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 如果鼠标在弹出窗口（或级联子窗口）上，不关闭
                if (_windowVisible)
                {
                    NativeMethods.POINT pt;
                    NativeMethods.GetCursorPos(out pt);
                    IntPtr hwndUnderMouse = NativeMethods.WindowFromPoint(pt);
                    if (_popupForm.ContainsWindow(hwndUnderMouse))
                    {
                        _lastOnPopupTime = DateTime.Now;
                        return; // 鼠标在弹出窗口上，保持显示
                    }
                    // 备选：检查顶层窗口是否属于任何 PopupForm
                    IntPtr topHwnd = hwndUnderMouse;
                    while (true)
                    {
                        IntPtr parent = NativeMethods.GetParent(topHwnd);
                        if (parent == IntPtr.Zero) break;
                        topHwnd = parent;
                    }
                    bool onAnyPopup = false;
                    foreach (Form f in Application.OpenForms)
                    {
                        if (f is PopupForm pf && pf.Handle == topHwnd)
                        {
                            onAnyPopup = true;
                            break;
                        }
                    }
                    if (onAnyPopup)
                    {
                        _lastOnPopupTime = DateTime.Now;
                        return;
                    }
                    // 宽限期：鼠标刚离开弹出窗口（如在窗口间缝隙），宽限期内不关闭
                    if ((DateTime.Now - _lastOnPopupTime).TotalMilliseconds < Settings.GraceMs)
                        return;
                }

                // 检测配置的热键是否按下（默认右 Shift）
                bool keyPressed = (NativeMethods.GetAsyncKeyState(Settings.HotKeyVk) & 0x8000) != 0;

                // 如果热键没按下，隐藏窗口
                if (!keyPressed)
                {
                    if (_windowVisible)
                    {
                        _popupForm.HidePopup();
                        _windowVisible = false;
                        _currentFolder = string.Empty;
                    }
                    return;
                }

                // 热键按下，获取当前文件夹路径
                string folderPath = GetCurrentFolderPath();
                bool folderChanged = (folderPath != _currentFolder);

                // 如果文件夹变了（或窗口还没显示），更新显示
                if (folderChanged && !string.IsNullOrEmpty(folderPath))
                {
                    _currentFolder = folderPath;
                    _folderLeaveTime = DateTime.MinValue; // 回到文件夹，取消消失延迟
                    NativeMethods.POINT cursorPt;
                    NativeMethods.GetCursorPos(out cursorPt);
                    _popupForm.ShowForFolder(folderPath, cursorPt.X, cursorPt.Y, _currentExplorerHwnd);
                    _windowVisible = true;
                    Logger.Write("[MouseHook] 显示: " + folderPath);
                }
                // 如果文件夹无效但窗口还开着，延迟 1.5s 后关闭
                else if (string.IsNullOrEmpty(folderPath) && _windowVisible)
                {
                    if (_folderLeaveTime == DateTime.MinValue)
                        _folderLeaveTime = DateTime.Now;  // 记录离开文件夹的时间
                    else if ((DateTime.Now - _folderLeaveTime).TotalMilliseconds >= Settings.LingerMs)
                    {
                        _popupForm.HidePopup();
                        _windowVisible = false;
                        _currentFolder = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write("[MouseHook] 异常: " + ex.Message);
            }
        }

        // 获取当前鼠标下的文件夹路径
        private string GetCurrentFolderPath()
        {
            if (_lastHwnd == IntPtr.Zero) return null;

            // 检查是否是资源管理器窗口
            StringBuilder cls = new StringBuilder(256);
            NativeMethods.GetClassName(_lastHwnd, cls, cls.Capacity);
            string className = cls.ToString();
            if (className != "CabinetWClass" && className != "DirectUIHWND" &&
                className != "SysListView32" && className != "SysTreeView32")
                return null;

            // 获取资源管理器窗口的当前路径
            IntPtr topHwnd = _lastHwnd;
            while (true)
            {
                IntPtr parent = NativeMethods.GetParent(topHwnd);
                if (parent == IntPtr.Zero) break;
                topHwnd = parent;
            }

            StringBuilder topCls = new StringBuilder(256);
            NativeMethods.GetClassName(topHwnd, topCls, topCls.Capacity);
            if (topCls.ToString() != "CabinetWClass") return null;

            _currentExplorerHwnd = topHwnd;

            string parentFolderPath = GetParentFolderPath(topHwnd);
            if (string.IsNullOrEmpty(parentFolderPath)) return null;

            // 用 UIAutomation 获取鼠标下具体是哪个文件夹/文件
            NativeMethods.POINT pt;
            NativeMethods.GetCursorPos(out pt);
            string itemName = GetItemNameAtPoint(pt.X, pt.Y);

            if (string.IsNullOrEmpty(itemName))
            {
                // 如果没获取到具体项，返回父文件夹路径本身
                return parentFolderPath;
            }

            string fullPath = Path.Combine(parentFolderPath, itemName);
            return fullPath;
        }

        // 获取资源管理器窗口的当前文件夹路径
        private string GetParentFolderPath(IntPtr topHwnd)
        {
            try
            {
                Type swType = Type.GetTypeFromCLSID(
                    new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"));
                if (swType == null) return null;

                object shellWindows = Activator.CreateInstance(swType);
                if (shellWindows == null) return null;

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
                        if (wh == topHwnd)
                        {
                            object url = w.GetType().InvokeMember("LocationURL",
                                System.Reflection.BindingFlags.GetProperty, null, w, null);
                            if (url != null)
                            {
                                string urlStr = url.ToString();
                                if (urlStr.StartsWith("file://"))
                                {
                                    string path = Uri.UnescapeDataString(urlStr.Substring(8));
                                    Marshal.ReleaseComObject(shellWindows);
                                    return path.Replace('/', '\\');
                                }
                            }
                            break;
                        }
                    }
                }
                Marshal.ReleaseComObject(shellWindows);
            }
            catch (Exception ex)
            {
                Logger.Write("[MouseHook] GetPath: " + ex.Message);
            }
            return null;
        }

        // 用 UIAutomation 获取鼠标位置的具体文件/文件夹名
        private string GetItemNameAtPoint(int x, int y)
        {
            try
            {
                var point = new System.Windows.Point(x, y);
                var element = System.Windows.Automation.AutomationElement.FromPoint(point);
                if (element == null) return null;

                // 检查是否是文件列表项或树形项
                var ctrlType = element.Current.ControlType;
                if (ctrlType == System.Windows.Automation.ControlType.ListItem ||
                    ctrlType == System.Windows.Automation.ControlType.TreeItem ||
                    ctrlType == System.Windows.Automation.ControlType.DataItem)
                {
                    string name = element.Current.Name;
                    if (!string.IsNullOrEmpty(name))
                    {
                        // 去掉可能的后缀（如 "修改日期: ..."）
                        // 在资源管理器中，文件夹名称就是 Name 属性
                        return name;
                    }
                }

                // 如果直接获取不到，尝试获取父级
                var parent = System.Windows.Automation.TreeWalker.RawViewWalker.GetParent(element);
                if (parent != null)
                {
                    var parentCtrl = parent.Current.ControlType;
                    if (parentCtrl == System.Windows.Automation.ControlType.ListItem ||
                        parentCtrl == System.Windows.Automation.ControlType.TreeItem)
                    {
                        string name = parent.Current.Name;
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write("[MouseHook] UIA: " + ex.Message);
            }
            return null;
        }

        public void Dispose()
        {
            Uninstall();
        }
    }
}