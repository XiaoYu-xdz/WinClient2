using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using H.NotifyIcon.Core;

namespace WinClient2.Services;

/// <summary>
/// 系统托盘服务：应用常驻后台，托盘菜单可显示/隐藏 GUI 或退出。
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly Application _app;
    private readonly Views.OverlayWindow _overlay;
    private TrayIconWithContextMenu? _tray;
    private Bitmap? _iconBitmap;
    private IntPtr _iconHandle;

    public TrayService(Application app, Views.OverlayWindow overlay)
    {
        _app = app;
        _overlay = overlay;
    }

    public void Initialize()
    {
        var stream = Application.GetResourceStream(
            new Uri("pack://application:,,,/Resources/app.png"))?.Stream
            ?? throw new InvalidOperationException("缺少托盘图标资源 Resources/app.png");

        _iconBitmap = new Bitmap(stream);
        _iconHandle = _iconBitmap.GetHicon();

        var toggleItem = new PopupMenuItem { Text = "显示 / 隐藏 GUI" };
        toggleItem.Click += (_, _) => _overlay.ToggleGui();

        var exitItem = new PopupMenuItem { Text = "退出" };
        exitItem.Click += (_, _) => _app.Shutdown();

        _tray = new TrayIconWithContextMenu
        {
            Icon = _iconHandle,
            ToolTip = CurrentTitle(),
            ContextMenu = new PopupMenu
            {
                Items =
                {
                    toggleItem,
                    new PopupMenuSeparator(),
                    exitItem,
                },
            },
        };

        _tray.Create();
        _tray.Show();

        // 自定义窗口标题变化时同步托盘提示
        SettingsService.Instance.Changed += OnSettingsChanged;
    }

    private static string CurrentTitle()
    {
        var s = SettingsService.Instance;
        return s.CustomWindowTitle && !string.IsNullOrEmpty(s.WindowTitleText) ? s.WindowTitleText : "WinClient2";
    }

    private void OnSettingsChanged()
    {
        _tray?.UpdateToolTip(CurrentTitle());
    }

    public void Dispose()
    {
        SettingsService.Instance.Changed -= OnSettingsChanged;
        _tray?.Dispose();
        _tray = null;
        if (_iconHandle != IntPtr.Zero)
            DestroyIcon(_iconHandle);
        _iconHandle = IntPtr.Zero;
        _iconBitmap?.Dispose();
        _iconBitmap = null;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
