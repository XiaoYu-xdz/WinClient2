using System.Runtime.InteropServices;

namespace WinClient2.Services;

/// <summary>
/// 全局按键监听：用低层键盘钩子（WH_KEYBOARD_LL）检测「右 Alt」作为 GUI 唤起键。
/// 钩子对注入输入同样生效（便于自动化验证），且后续模块自定义按键绑定也复用此机制。
/// 唤起键被按下时会被吞掉（不传给底层应用），避免 Alt 触发应用菜单栏。
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_SYSKEYDOWN = 0x0104; // Alt 键按下走系统键消息
    private const uint WM_SYSKEYUP = 0x0105;
    private const uint VK_RMENU = 0xA5;   // 右 Alt
    private const uint VK_ESCAPE = 0x1B;
    private const int VK_LCONTROL = 0xA2; // 左 Ctrl（AltGr 检测用）

    private LowLevelKeyboardProc? _proc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _summonKeyDown;

    /// <summary>唤起键（右 Alt）被按下（首次按下，长按不重复触发）时触发（UI 线程）。</summary>
    public event Action? SummonKeyPressed;

    /// <summary>ESC 被按下时触发（UI 线程），用于关闭 GUI，与窗口焦点无关。</summary>
    public event Action? EscapePressed;

    public void Register()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = (uint)wParam;
                if (msg is WM_KEYDOWN or WM_SYSKEYDOWN or WM_KEYUP or WM_SYSKEYUP)
                {
                    var isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                    var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    if (kbd.vkCode == VK_RMENU)
                    {
                        if (isDown)
                        {
                            // AltGr（左 Ctrl + 右 Alt）是打字输入，不触发唤起、不吞键
                            if ((GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0)
                                return CallNextHookEx(_hook, nCode, wParam, lParam);

                            if (!_summonKeyDown)
                            {
                                _summonKeyDown = true;
                                SummonKeyPressed?.Invoke();
                            }
                            return new IntPtr(1); // 吞掉按下：右 Alt 作为全局热键消费
                        }

                        // 抬起一律放行：避免 OS 认为 Alt 被卡住（AltGr 松开时 Ctrl 已先松开的情况）
                        _summonKeyDown = false;
                        return CallNextHookEx(_hook, nCode, wParam, lParam);
                    }
                    if (kbd.vkCode == VK_ESCAPE && isDown)
                    {
                        EscapePressed?.Invoke();
                    }
                }
            }
        }
        catch
        {
            // 钩子回调内绝不允许异常逃逸（否则会导致进程崩溃）
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _proc = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
