using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WinClient2.Services;

namespace WinClient2.Controls;

/// <summary>
/// 还原 Meteor WWindow：紫色标题栏 + 深色主体。
/// 标题栏可拖动（位移按 ScaleFactor 补偿）、单击/右键折叠（三角旋转 + 正文裁剪渐进动画）。
/// 通过 Content 容器填充内容，Id 用于记忆窗口位置。
/// </summary>
public class MeteorWindow : Grid
{
    private const double HeaderHeight = 34;
    private const double BodyPadding = 8;

    private readonly Border _header;
    private readonly TextBlock _titleText;
    private readonly Path _triangle;
    private readonly RotateTransform _triangleRotate;
    private readonly Border _body;
    private readonly ContentControl _content;

    /// <summary>窗口 Id（用于记忆位置）。</summary>
    public string Id { get; }

    /// <summary>缩放因子（拖拽位移补偿）。</summary>
    public double ScaleFactor { get; set; } = 1;

    /// <summary>正文容器。</summary>
    public ContentControl Content => _content;

    public string Title
    {
        get => _titleText.Text;
        set => _titleText.Text = value;
    }

    /// <summary>位置变化（拖动结束/折叠）时触发，用于记忆位置。</summary>
    public event EventHandler? PositionChanged;

    /// <summary>是否正在拖动（布局计算时跳过拖动中的窗口）。</summary>
    public bool IsDragging => _dragging;

    // 拖拽状态
    private Point _dragStart;
    private Point _windowStart;
    private bool _dragCandidate;
    private bool _dragging;

    // 折叠状态
    private bool _collapsed;
    private bool _animating;
    private double _bodyFullHeight;

    public MeteorWindow(string id, string title)
    {
        Id = id;
        MinWidth = 460;
        Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal");

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 紫色标题栏：标题居中 + 右侧折叠三角
        _titleText = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.FindResource("FontComfortaa"),
            FontSize = 20,
            Foreground = (Brush)Application.Current.FindResource("BrushText"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _triangleRotate = new RotateTransform();
        _triangle = new Path
        {
            Data = Geometry.Parse("M 2,1 L 14,1 L 8,12 Z"),
            Fill = (Brush)Application.Current.FindResource("BrushText"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _triangleRotate,
        };
        var headerContent = new Grid();
        headerContent.Children.Add(_titleText);
        headerContent.Children.Add(_triangle);

        _header = new Border
        {
            Background = (Brush)Application.Current.FindResource("BrushAccent"),
            Height = HeaderHeight,
            Cursor = Cursors.Hand,
            Child = headerContent,
        };
        _header.PreviewMouseLeftButtonDown += OnHeaderPreviewMouseLeftButtonDown;
        _header.MouseMove += OnHeaderMouseMove;
        _header.PreviewMouseRightButtonDown += OnHeaderPreviewMouseRightButtonDown;
        Grid.SetRow(_header, 0);
        Children.Add(_header);

        // 主体
        _content = new ContentControl { VerticalAlignment = VerticalAlignment.Top };
        _body = new Border { Padding = new Thickness(BodyPadding), Child = _content };
        Grid.SetRow(_body, 1);
        Children.Add(_body);

        PreviewMouseLeftButtonUp += OnWindowPreviewMouseLeftButtonUp;
        SizeChanged += OnWindowSizeChanged;

        Title = title;
    }

    // ===== Canvas 定位 =====

    public void SetCanvasPosition(double x, double y)
    {
        Canvas.SetLeft(this, x);
        Canvas.SetTop(this, y);
    }

    public Point GetCanvasPosition() => new(Canvas.GetLeft(this), Canvas.GetTop(this));

    // ===== 拖拽（还原 Meteor WHeader：移动即拖动，未移动即折叠） =====

    private void OnHeaderPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // 位置必须以不移动的主窗口为参考系（窗口自身会随拖动移动）
        var reference = Window.GetWindow(this);
        if (reference is null) return;
        _dragCandidate = true;
        _dragging = false;
        _dragStart = e.GetPosition(reference);
        _windowStart = GetCanvasPosition();
    }

    private void OnHeaderMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragCandidate || e.LeftButton != MouseButtonState.Pressed) return;
        var reference = Window.GetWindow(this);
        if (reference is null) return;
        var position = e.GetPosition(reference);
        // 画布位移 = 窗口坐标位移 / scale（缩放后视觉与光标一致）
        var dx = (position.X - _dragStart.X) / ScaleFactor;
        var dy = (position.Y - _dragStart.Y) / ScaleFactor;
        if (!_dragging && (Math.Abs(dx) > 2 || Math.Abs(dy) > 2))
        {
            _dragging = true;
            _header.CaptureMouse();
        }
        if (_dragging)
            SetCanvasPosition(_windowStart.X + dx, _windowStart.Y + dy);
    }

    private void OnWindowPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            e.Handled = true;
            _header.ReleaseMouseCapture();
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (_dragCandidate)
        {
            ToggleCollapse();
        }
        _dragCandidate = false;
        _dragging = false;
    }

    private void OnHeaderPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ToggleCollapse();
    }

    // ===== 折叠/展开动画（还原 Meteor WWindow animProgress） =====

    private void ToggleCollapse()
    {
        _collapsed = !_collapsed;
        AnimateWindow(_collapsed);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 记录展开时的正文完整高度（动画期间不记录）
        if (!_collapsed && !_animating && _body.ActualHeight > 0)
            _bodyFullHeight = _body.ActualHeight;
    }

    private void AnimateWindow(bool collapse)
    {
        // 动画时长受全局"动画速度"设置控制
        var duration = TimeSpan.FromMilliseconds(150 / Math.Max(0.1, SettingsService.Instance.AnimationSpeed));
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        _animating = true;

        // 三角旋转：展开朝下(0°)，收起 -90°
        _triangleRotate.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(collapse ? -90 : 0, duration) { EasingFunction = ease });

        var width = _body.ActualWidth > 0 ? _body.ActualWidth : 200;
        if (_bodyFullHeight <= 0)
            _bodyFullHeight = _body.ActualHeight > 0 ? _body.ActualHeight : 120;
        var full = _bodyFullHeight;
        var target = collapse ? 0 : full;

        // Height 动画起点必须是具体值
        if (double.IsNaN(_body.Height))
            _body.Height = _body.ActualHeight > 0 ? _body.ActualHeight : full;

        if (_body.Clip is not RectangleGeometry clip)
        {
            clip = new RectangleGeometry(new Rect(0, 0, width, full));
            _body.Clip = clip;
        }

        var heightAnim = new DoubleAnimation(target, duration) { EasingFunction = ease };
        _body.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);

        var clipAnim = new RectAnimation(new Rect(0, 0, width, target), duration) { EasingFunction = ease };
        clip.BeginAnimation(RectangleGeometry.RectProperty, clipAnim);

        heightAnim.Completed += (_, _) =>
        {
            _animating = false;
            _body.BeginAnimation(FrameworkElement.HeightProperty, null);
            clip.BeginAnimation(RectangleGeometry.RectProperty, null);
            if (_collapsed)
            {
                _body.Height = 0;
                clip.Rect = new Rect(0, 0, width, 0);
            }
            else
            {
                _body.ClearValue(FrameworkElement.HeightProperty);
                _body.Clip = null;
            }
        };
    }

    /// <summary>切换内容时重置为展开态并清理折叠残留。</summary>
    public void ResetExpanded()
    {
        _collapsed = false;
        _animating = false;
        _body.BeginAnimation(FrameworkElement.HeightProperty, null);
        if (_body.Clip is RectangleGeometry rg)
            rg.BeginAnimation(RectangleGeometry.RectProperty, null);
        _body.ClearValue(FrameworkElement.HeightProperty);
        _body.Clip = null;
        _triangleRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        _triangleRotate.Angle = 0;
    }
}
