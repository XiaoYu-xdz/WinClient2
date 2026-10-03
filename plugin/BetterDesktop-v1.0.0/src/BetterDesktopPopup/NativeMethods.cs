using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterDesktopPopup
{
    /// <summary>
    /// Win32 API 和 DWM API 的 P/Invoke 声明
    /// 用于获取文件图标、设置窗口圆角等底层操作
    /// </summary>
    internal static class NativeMethods
    {
        // ===== DWM API (Windows 桌面窗口管理器) =====

        /// <summary>
        /// 设置窗口属性（如圆角）
        /// 对应 Windows 11 的窗口圆角效果
        /// </summary>
        [DllImport("dwmapi.dll", CharSet = CharSet.Auto, PreserveSig = true)]
        internal static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        // DWMWA 常量
        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        internal const int DWMWA_MICA_ENABLED = 1029;          // Windows 11 Mica 背景
        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // 暗色模式

        // 圆角偏好
        internal const int DWMWCP_DEFAULT = 0;
        internal const int DWMWCP_DONOTROUND = 1;
        internal const int DWMWCP_ROUND = 2;
        internal const int DWMWCP_ROUNDSMALL = 3;


        // ===== Shell32 API (文件图标、Shell 信息) =====

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
            // 别名属性，兼容不同 .NET 版本
            public IntPtr HIcon { get { return hIcon; } }
        }

        // SHGetFileInfo 标志
        internal const uint SHGFI_ICON = 0x000000100;
        internal const uint SHGFI_SMALLICON = 0x000000001;
        internal const uint SHGFI_LARGEICON = 0x000000000;
        internal const uint SHGFI_USEFILEATTRIBUTES = 0x000000004;
        internal const uint SHGFI_DISPLAYNAME = 0x000000200;
        internal const uint SHGFI_TYPENAME = 0x000000400;
        internal const uint SHGFI_SYSICONINDEX = 0x000004000;
        internal const uint SHGFI_LINKOVERLAY = 0x000008000;
        internal const uint SHGFI_OPENICON = 0x000000002;

        // 文件属性
        internal const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;


        // ===== User32 API (窗口管理) =====

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // 低层鼠标钩子回调委托
        internal delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int vKey);

        // 虚拟键码
        internal const int VK_LSHIFT = 0xA0;     // 左 Shift
        internal const int VK_RSHIFT = 0xA1;     // 右 Shift
        internal const int VK_LCONTROL = 0xA2;   // 左 Ctrl
        internal const int VK_LMENU = 0xA4;      // 左 Alt
        internal const int VK_LWIN = 0x5B;       // Win 键

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // 钩子类型
        internal const int WH_MOUSE_LL = 14;

        // 鼠标消息
        internal const int WM_MOUSEMOVE = 0x0200;
        internal const int WM_LBUTTONDOWN = 0x0201;
        internal const int WM_LBUTTONUP = 0x0202;
        internal const int WM_KEYDOWN = 0x0100;
        internal const int WM_KEYUP = 0x0101;
        internal const int WM_COMMAND = 0x0111;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        internal const int SW_SHOW = 5;
        internal const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        internal static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_SHOWWINDOW = 0x0040;

        // 滚动条
        internal const int SB_HORZ = 0;
        internal const int SB_VERT = 1;
        internal const int SB_BOTH = 3;
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        // ===== UxTheme API (Windows 视觉样式) =====
        // 设置窗口使用指定主题（如 "Explorer"），让 ListView 用资源管理器外观
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        internal static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        // ===== ListView 扩展样式消息 =====
        // 通用控件消息
        internal const int LVM_FIRST = 0x1000;
        // 设置 ListView 扩展样式
        internal const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
        // LVS_EX_TRACKSELECT：鼠标悬停行时用系统主题高亮（Win11 淡蓝悬停效果）
        internal const int LVS_EX_TRACKSELECT = 0x00000008;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);


        // ===== GDI32 API (图标管理) =====

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyIcon(IntPtr hIcon);


        /// <summary>
        /// 获取文件或文件夹的 Shell 图标
        /// 使用系统图标以匹配 Windows 11 资源管理器中的图标风格
        /// </summary>
        /// <param name="path">文件/文件夹路径</param>
        /// <param name="small">是否获取小图标（16x16）</param>
        /// <param name="forceDirectory">强制作为文件夹获取图标（用于不存在的路径获取标准文件夹图标）</param>
        /// <returns>图标对象，失败返回 null。调用者负责 Dispose。</returns>
        internal static Icon GetShellIcon(string path, bool small = true, bool forceDirectory = false)
        {
            var shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            flags |= small ? SHGFI_SMALLICON : SHGFI_LARGEICON;

            // 确定文件属性
            uint fileAttr = FILE_ATTRIBUTE_NORMAL;
            if (forceDirectory)
                fileAttr = FILE_ATTRIBUTE_DIRECTORY;
            else
            {
                try
                {
                    if (Directory.Exists(path))
                        fileAttr = FILE_ATTRIBUTE_DIRECTORY;
                }
                catch { }
            }

            IntPtr result = SHGetFileInfo(
                path,
                fileAttr,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                flags);

            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            // 从原始句柄创建图标副本，然后销毁原始句柄
            Icon icon = (Icon)Icon.FromHandle(shinfo.HIcon).Clone();
            DestroyIcon(shinfo.HIcon);

            return icon;
        }
    }
}