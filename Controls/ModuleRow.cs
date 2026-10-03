using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WinClient2.Models;
using WinClient2.Services;

namespace WinClient2.Controls;

/// <summary>
/// 还原 Meteor WMeteorModule 模块条目：
/// - 背景宽度动画：悬停或启用时从窄到满（Meteor 4/s ≈ 200ms）
/// - 启用时左侧 2px 紫色竖条从顶部向下生长（Meteor 6/s ≈ 160ms）
/// - 名称始终白色；左键切换开关；悬停显示描述 Tooltip
/// </summary>
public class ModuleRow : Grid
{
    private readonly Module _module;
    private readonly Border _background;
    private readonly Rectangle _accent;
    private readonly ScaleTransform _accentScale;
    private readonly TextBlock _label;
    private bool _hovered;

    /// <summary>右键模块时触发（打开模块设置面板，还原 Meteor 右键行为）。</summary>
    public event Action<Module>? SettingsRequested;

    public ModuleRow(Module module)
    {
        _module = module;
        Height = 25; // pad(4) + 文字高 + pad(4)

        _background = new Border
        {
            Background = (Brush)Application.Current.FindResource("BrushModuleBackground"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0,
            IsHitTestVisible = false,
        };
        Panel.SetZIndex(_background, 0);

        _accentScale = new ScaleTransform(1, module.Enabled ? 1 : 0);
        _accent = new Rectangle
        {
            Width = 2,
            Fill = (Brush)Application.Current.FindResource("BrushAccent"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = _accentScale,
            IsHitTestVisible = false,
        };
        Panel.SetZIndex(_accent, 1);

        _label = new TextBlock
        {
            Text = module.Name,
            FontFamily = (FontFamily)Application.Current.FindResource("FontJetBrainsMono"),
            FontSize = 13,
            Foreground = (Brush)Application.Current.FindResource("BrushText"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 4, 0),
            TextAlignment = SettingsService.Instance.ModuleAlignment switch
            {
                "Left" => TextAlignment.Left,
                "Right" => TextAlignment.Right,
                _ => TextAlignment.Center,
            },
        };
        Panel.SetZIndex(_label, 2);

        Children.Add(_background);
        Children.Add(_accent);
        Children.Add(_label);

        ToolTip = module.Description;
        Cursor = Cursors.Hand;

        Loaded += OnLoaded;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        SettingsRequested?.Invoke(_module);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => AnimateBackground();

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _hovered = true;
        AnimateBackground();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _hovered = false;
        AnimateBackground();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _module.Toggle();
        e.Handled = true;
        AnimateBackground();
        AnimateAccent();
    }

    private void AnimateBackground()
    {
        var target = _hovered || _module.Enabled;
        var to = target ? Math.Max(0, ActualWidth) : 0;
        var animation = new DoubleAnimation(_background.Width, to, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        _background.BeginAnimation(FrameworkElement.WidthProperty, animation);
    }

    private void AnimateAccent()
    {
        var animation = new DoubleAnimation(_module.Enabled ? 1 : 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        _accentScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
