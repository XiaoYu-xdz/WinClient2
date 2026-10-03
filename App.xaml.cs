using System.Threading;
using System.Windows;
using WinClient2.Services;
using WinClient2.Views;

namespace WinClient2;

/// <summary>
/// 应用入口：单实例 + 托盘常驻 + 全局热键（右 Shift 唤起 GUI）。
/// 启动后不显示任何窗口，等待热键 / 托盘触发。
/// </summary>
public partial class App : Application
{
    private const string MutexName = "WinClient2_SingleInstance";

    private Mutex? _mutex;
    private GlobalHotkeyService? _hotkey;
    private TrayService? _tray;
    private OverlayWindow? _overlay;
    private HudWindow? _hud;

    /// <summary>桌面 HUD 悬浮窗（HUD 页开关联动）。</summary>
    public HudWindow Hud => _hud!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("WinClient2 已在运行。", "WinClient2");
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 全局异常记录（%TEMP%\wc2_crash.log），用于排查崩溃
        DispatcherUnhandledException += (_, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wc2_crash.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {e.Exception}{Environment.NewLine}");
            }
            catch
            {
                // 日志失败忽略
            }
        };

        // 加载持久化设置并应用主题（颜色/字体）
        SettingsService.Instance.Load();
        ThemeService.EnsureMutableBrushes();
        ThemeService.ApplyAll();

        _overlay = new OverlayWindow();

        _hud = new HudWindow();
        _hud.ApplySettings();

        _tray = new TrayService(this, _overlay);
        _tray.Initialize();

        _hotkey = new GlobalHotkeyService();
        _hotkey.SummonKeyPressed += OnSummonKeyPressed;
        _hotkey.EscapePressed += OnEscapePressed;
        _hotkey.Register();
    }

    private void OnSummonKeyPressed()
    {
        _overlay?.ToggleGui();
    }

    private void OnEscapePressed()
    {
        // HUD 编辑模式中按 ESC 先退出编辑
        if (_hud is { IsEditMode: true })
        {
            _hud.SetEditMode(false);
            return;
        }

        // GUI 打开时按 ESC 关闭（全局，与窗口焦点无关）
        if (_overlay is { IsVisible: true })
            _overlay.HideGui();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
