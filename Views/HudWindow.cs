using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinClient2.Models;
using WinClient2.Services;

namespace WinClient2.Views;

/// <summary>
/// 桌面 HUD 悬浮窗：独立置顶半透明窗口，常驻显示时钟/系统状态/自定义文字/电池元素。
/// - 点击穿透（WS_EX_TRANSPARENT）：正常模式鼠标可直接穿透，不挡桌面操作
/// - 编辑模式：移除穿透，元素可拖拽定位，右上角"完成"退出，悬停元素出现 × 删除
/// - 透明度/缩放来自设置；元素位置与开关持久化
/// </summary>
public class HudWindow : Window
{
    private class ElementView
    {
        public Border Border = null!;
        public TextBlock Text = null!;
    }

    private readonly Canvas _canvas;
    private readonly ScaleTransform _scale;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, ElementView> _views = new();
    private Border? _editBar;
    private bool _editMode;

    // 拖拽状态
    private HudElementState? _dragElement;
    private Point _dragStart;
    private Point _elementStart;

    public bool IsEditMode => _editMode;

    public HudWindow()
    {
        Title = "WinClient2Hud";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false; // 悬浮窗不抢焦点
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Left = 0;
        Top = 0;

        _scale = new ScaleTransform(1, 1);
        _canvas = new Canvas { RenderTransform = _scale, RenderTransformOrigin = new Point(0, 0) };
        Content = _canvas;

        SourceInitialized += (_, _) =>
        {
            ApplyClickThrough();
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshElements();
    }

    /// <summary>根据设置应用显隐/透明度/缩放/穿透，并重建元素。</summary>
    public void ApplySettings()
    {
        var s = SettingsService.Instance;
        Opacity = s.HudOpacity;
        _scale.ScaleX = _scale.ScaleY = s.HudScale;

        if (s.HudActive && !IsVisible)
        {
            Show();
            _timer.Start();
        }
        else if (!s.HudActive && IsVisible)
        {
            _timer.Stop();
            Hide();
            return;
        }

        if (IsVisible)
        {
            SystemStats.Instance.Tick();
            RebuildViews();
        }
    }

    public void SetEditMode(bool on)
    {
        _editMode = on;
        ApplyClickThrough();
        if (_editBar is not null)
            _editBar.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        RebuildViews();
    }

    // ===== 点击穿透 =====

    private void ApplyClickThrough()
    {
        if (!IsVisible) return; // 未显示时没有窗口句柄（访问会抛异常）
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (_editMode || !SettingsService.Instance.HudClickThrough)
            style &= ~WS_EX_TRANSPARENT;
        else
            style |= WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
    }

    // ===== 元素视图 =====

    private void RebuildViews()
    {
        foreach (var view in _views.Values)
            _canvas.Children.Remove(view.Border);
        _views.Clear();

        foreach (var element in SettingsService.Instance.HudElements.Where(e => e.Enabled))
        {
            var view = BuildElementView(element);
            _views[element.Id] = view;
            Canvas.SetLeft(view.Border, element.X);
            Canvas.SetTop(view.Border, element.Y);
            _canvas.Children.Add(view.Border);
        }

        RefreshElementTexts();
        EnsureEditBar();
    }

    private ElementView BuildElementView(HudElementState element)
    {
        var mono = (FontFamily)Application.Current.FindResource("FontJetBrainsMono");
        var text = new TextBlock
        {
            FontFamily = mono,
            FontSize = 13,
            Foreground = (Brush)Application.Current.FindResource("BrushText"),
            Margin = new Thickness(6, 3, 6, 3),
        };

        var view = new ElementView { Text = text };
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(170, 20, 20, 20)),
            Child = text,
        };
        view.Border = border;

        if (_editMode)
        {
            border.Cursor = Cursors.SizeAll;
            border.MouseLeftButtonDown += (_, e) => OnElementDragStart(element, e);
            border.MouseMove += OnElementDragMove;
            border.MouseLeftButtonUp += OnElementDragEnd;

            // 悬停显示删除按钮
            var remove = new TextBlock
            {
                Text = "×",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 2, 0),
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand,
                ToolTip = "删除元素",
            };
            remove.MouseLeftButtonDown += (_, _) =>
            {
                element.Enabled = false;
                SettingsService.Instance.Save();
                RebuildViews();
            };
            border.MouseEnter += (_, _) => remove.Visibility = Visibility.Visible;
            border.MouseLeave += (_, _) => remove.Visibility = Visibility.Collapsed;
            border.Child = new Grid { Children = { text, remove } };
        }

        return view;
    }

    private void OnElementDragStart(HudElementState element, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _dragElement = element;
        _dragStart = e.GetPosition(_canvas);
        _elementStart = new Point(element.X, element.Y);
        ((FrameworkElement)e.Source).CaptureMouse();
        e.Handled = true;
    }

    private void OnElementDragMove(object sender, MouseEventArgs e)
    {
        if (_dragElement is null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(_canvas);
        _dragElement.X = _elementStart.X + (pos.X - _dragStart.X);
        _dragElement.Y = _elementStart.Y + (pos.Y - _dragStart.Y);
        if (sender is FrameworkElement fe && _views.TryGetValue(_dragElement.Id, out var view))
        {
            Canvas.SetLeft(view.Border, _dragElement.X);
            Canvas.SetTop(view.Border, _dragElement.Y);
        }
    }

    private void OnElementDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (_dragElement is null) return;
        ((FrameworkElement)sender).ReleaseMouseCapture();
        SettingsService.Instance.Save();
        _dragElement = null;
    }

    private void RefreshElements()
    {
        SystemStats.Instance.Tick();
        RefreshElementTexts();
    }

    private void RefreshElementTexts()
    {
        var now = DateTime.Now;
        foreach (var pair in _views)
        {
            var element = SettingsService.Instance.HudElements.FirstOrDefault(e => e.Id == pair.Key);
            if (element is null) continue;
            var textBlock = pair.Value.Text;
            if (textBlock is null) continue;
            textBlock.Text = HudElements.Render(element, SystemStats.Instance, now);
        }
    }

    // ===== 编辑栏（右上角 完成） =====

    private void EnsureEditBar()
    {
        if (_editBar is not null) return;

        var hint = new TextBlock
        {
            Text = "拖动元素调整位置，悬停元素可删除",
            FontFamily = (FontFamily)Application.Current.FindResource("FontJetBrainsMono"),
            FontSize = 12,
            Foreground = (Brush)Application.Current.FindResource("BrushTextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        var done = new Border
        {
            Background = (Brush)Application.Current.FindResource("BrushAccent"),
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 4, 12, 4),
            Child = new TextBlock
            {
                Text = "完成",
                FontFamily = (FontFamily)Application.Current.FindResource("FontJetBrainsMono"),
                FontSize = 13,
                Foreground = (Brush)Application.Current.FindResource("BrushText"),
            },
        };
        done.MouseLeftButtonDown += (_, _) => SetEditMode(false);

        _editBar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
            Padding = new Thickness(10, 6, 10, 6),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { hint, done },
            },
        };
        Canvas.SetLeft(_editBar, 0);
        Canvas.SetTop(_editBar, 0);
        _canvas.Children.Add(_editBar);
        _editBar.Visibility = _editMode ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===== P/Invoke =====

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
