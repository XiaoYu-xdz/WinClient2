using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WinClient2.Controls;

/// <summary>
/// Meteor 风格颜色设置行：标签 + 色块（点击弹出调色面板）+ 重置箭头。
/// 调色面板还原 Meteor ColorSettingScreen：预览色块 + 饱和度/亮度二维色板 + Hue 渐变条
/// + RGBA 输入（0-255）+ 完成/重置，改动实时生效。
/// </summary>
public class ColorSettingRow : Grid
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ColorSettingRow), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ColorSettingRow),
        new FrameworkPropertyMetadata(Colors.Magenta, OnColorPropertyChanged));

    public static readonly DependencyProperty DefaultColorProperty = DependencyProperty.Register(
        nameof(DefaultColor), typeof(Color), typeof(ColorSettingRow), new PropertyMetadata(Colors.Magenta));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public Color DefaultColor
    {
        get => (Color)GetValue(DefaultColorProperty);
        set => SetValue(DefaultColorProperty, value);
    }

    /// <summary>颜色变化时触发（拖动色板/输入 RGBA/重置）。</summary>
    public event EventHandler? ColorChanged;

    private readonly TextBlock _label;
    private readonly Border _swatch;
    private readonly SolidColorBrush _swatchBrush;
    private readonly Border _resetButton;
    private readonly Popup _popup;
    private readonly SvQuad _svQuad;
    private readonly HueBar _hueBar;
    private readonly TextBox[] _rgbaBoxes = new TextBox[4];

    private double _hue;
    private double _sat;
    private double _value;
    private bool _syncing;

    public ColorSettingRow()
    {
            Margin = new Thickness(0, 8, 0, 0); // 行距
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var mono = (FontFamily)Application.Current.FindResource("FontJetBrainsMono");
        var secondary = (Brush)Application.Current.FindResource("BrushTextSecondary");
        var text = (Brush)Application.Current.FindResource("BrushText");

        _label = new TextBlock
        {
            FontFamily = mono,
            FontSize = 15,
            Foreground = secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };
        Grid.SetColumn(_label, 0);
        Children.Add(_label);

        _swatchBrush = new SolidColorBrush(Color);
        _swatch = new Border
        {
            Width = 20,
            Height = 20,
            Background = _swatchBrush,
            BorderBrush = (Brush)Application.Current.FindResource("BrushOutlineNormal"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = "点击选择颜色",
        };
        _swatch.MouseLeftButtonDown += OnSwatchClicked;
        Grid.SetColumn(_swatch, 1);
        Children.Add(_swatch);

        _resetButton = ToggleSettingRow.CreateResetButton(text, OnResetClicked);
        Grid.SetColumn(_resetButton, 2);
        Children.Add(_resetButton);

        // ===== 调色面板 =====
        _svQuad = new SvQuad { Width = 180, Height = 110 };
        _svQuad.Changed += OnSvChanged;
        _hueBar = new HueBar { Width = 180, Height = 14, Margin = new Thickness(0, 8, 0, 0) };
        _hueBar.Changed += OnHueChanged;

        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(new Border
        {
            Height = 26,
            Background = _swatchBrush,
            Child = null,
        });
        panel.Children.Add(new StackPanel { Margin = new Thickness(0, 10, 0, 0), Children = { _svQuad, _hueBar } });
        panel.Children.Add(BuildRgbaGrid(mono, text, secondary));
        panel.Children.Add(BuildBottomButtons(text, mono));

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _swatch,
            AllowsTransparency = true,
            StaysOpen = true, // 单击后常亮，再单击关闭（避免松开鼠标即消失）
            Child = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushBackgroundPressed"),
                Child = panel,
            },
        };
    }

    private UIElement BuildRgbaGrid(FontFamily mono, Brush text, Brush secondary)
    {
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new[] { "R:", "G:", "B:", "A:" };
        for (var i = 0; i < 4; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = labels[i],
                FontFamily = mono,
                FontSize = 13,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 3, 8, 3),
            };
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            _rgbaBoxes[i] = new TextBox
            {
                FontFamily = mono,
                FontSize = 13,
                Foreground = text,
                Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal"),
                CaretBrush = text,
                BorderThickness = new Thickness(0),
                Width = 42,
                Padding = new Thickness(4, 2, 4, 2),
            };
            var index = i;
            _rgbaBoxes[i].KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    ApplyRgba(index);
                    Keyboard.ClearFocus();
                }
            };
            _rgbaBoxes[i].LostFocus += (_, _) => ApplyRgba(index);
            Grid.SetRow(_rgbaBoxes[i], i);
            Grid.SetColumn(_rgbaBoxes[i], 1);
            grid.Children.Add(_rgbaBoxes[i]);
        }
        return grid;
    }

    private UIElement BuildBottomButtons(Brush text, FontFamily mono)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        row.Children.Add(CreateButton("重置", mono, text, (_, _) =>
        {
            Color = DefaultColor;
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }));
        row.Children.Add(CreateButton("完成", mono, text, (_, _) => _popup.IsOpen = false));
        return row;
    }

    private static Border CreateButton(string label, FontFamily mono, Brush text, MouseButtonEventHandler onClick)
    {
        var button = new Border
        {
            Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal"),
            Cursor = Cursors.Hand,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
            BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = label,
                FontFamily = mono,
                FontSize = 13,
                Foreground = text,
            },
        };
        button.MouseEnter += (_, _) => button.Background = (Brush)Application.Current.FindResource("BrushBackgroundHovered");
        button.MouseLeave += (_, _) => button.Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal");
        button.MouseLeftButtonDown += onClick;
        return button;
    }

    // ===== 交互 =====

    private void OnSwatchClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }
        SyncFromColor();
        _popup.IsOpen = true;
    }

    private void OnResetClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Color = DefaultColor;
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSvChanged(object? sender, EventArgs e)
    {
        _sat = _svQuad.Saturation;
        _value = _svQuad.Value;
        ApplyHsv();
    }

    private void OnHueChanged(object? sender, EventArgs e)
    {
        _hue = _hueBar.Hue;
        ApplyHsv();
    }

    private void ApplyHsv()
    {
        _syncing = true;
        Color = ColorHelper.FromHsv(_hue, _sat, _value);
        _syncing = false;
        UpdateRgbaBoxes();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyRgba(int index)
    {
        if (!byte.TryParse(_rgbaBoxes[index].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            UpdateRgbaBoxes(); // 非法输入回显当前值
            return;
        }
        var r = (byte)(index == 0 ? value : Color.R);
        var g = (byte)(index == 1 ? value : Color.G);
        var b = (byte)(index == 2 ? value : Color.B);
        var a = (byte)(index == 3 ? value : Color.A);
        _syncing = true;
        Color = Color.FromArgb(a, r, g, b);
        _syncing = false;
        SyncFromColor(); // 重新计算 HSV 并同步色板
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    // ===== 同步 =====

    private void SyncFromColor()
    {
        var (h, s, v) = ColorHelper.ToHsv(Color);
        _hue = h;
        _sat = s;
        _value = v;
        _svQuad.Hue = h;
        _svQuad.Saturation = s;
        _svQuad.Value = v;
        _svQuad.InvalidateVisual();
        _hueBar.Hue = h;
        _hueBar.InvalidateVisual();
        UpdateRgbaBoxes();
    }

    private void UpdateRgbaBoxes()
    {
        _rgbaBoxes[0].Text = Color.R.ToString();
        _rgbaBoxes[1].Text = Color.G.ToString();
        _rgbaBoxes[2].Text = Color.B.ToString();
        _rgbaBoxes[3].Text = Color.A.ToString();
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColorSettingRow)d)._label.Text = (string)e.NewValue;

    private static void OnColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (ColorSettingRow)d;
        row._swatchBrush.Color = (Color)e.NewValue;
        if (!row._syncing)
            row.SyncFromColor();
    }
}

/// <summary>饱和度/亮度二维色板（Meteor WBrightnessQuad）：横向白→纯色，纵向透明→黑。</summary>
public class SvQuad : FrameworkElement
{
    public double Hue { get; set; }
    public double Saturation { get; set; }
    public double Value { get; set; } = 1;

    public event EventHandler? Changed;

    private bool _dragging;

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);

        var hueColor = ColorHelper.FromHsv(Hue, 1, 1);
        dc.DrawRectangle(new LinearGradientBrush(Colors.White, hueColor, 0), null, bounds);
        dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent, Colors.Black, 90), null, bounds);

        var mx = Saturation * ActualWidth;
        var my = (1 - Value) * ActualHeight;
        dc.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 1), new Point(mx, my), 4, 4);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragging = true;
        CaptureMouse();
        SetFromPosition(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging && e.LeftButton == MouseButtonState.Pressed)
            SetFromPosition(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }
    }

    private void SetFromPosition(Point p)
    {
        Saturation = Math.Clamp(p.X / Math.Max(1, ActualWidth), 0, 1);
        Value = Math.Clamp(1 - p.Y / Math.Max(1, ActualHeight), 0, 1);
        InvalidateVisual();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Hue 渐变条（Meteor WHueQuad）：红→黄→绿→青→蓝→品红→红。</summary>
public class HueBar : FrameworkElement
{
    public double Hue { get; set; }

    public event EventHandler? Changed;

    private bool _dragging;

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
        };
        gradient.GradientStops.Add(new GradientStop(Colors.Red, 0));
        gradient.GradientStops.Add(new GradientStop(Colors.Yellow, 1.0 / 6));
        gradient.GradientStops.Add(new GradientStop(Colors.Lime, 2.0 / 6));
        gradient.GradientStops.Add(new GradientStop(Colors.Cyan, 3.0 / 6));
        gradient.GradientStops.Add(new GradientStop(Colors.Blue, 4.0 / 6));
        gradient.GradientStops.Add(new GradientStop(Colors.Magenta, 5.0 / 6));
        gradient.GradientStops.Add(new GradientStop(Colors.Red, 1));
        dc.DrawRectangle(gradient, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var mx = Hue / 360 * ActualWidth;
        dc.DrawLine(new Pen(Brushes.White, 2), new Point(mx, 0), new Point(mx, ActualHeight));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragging = true;
        CaptureMouse();
        SetFromPosition(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging && e.LeftButton == MouseButtonState.Pressed)
            SetFromPosition(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }
    }

    private void SetFromPosition(Point p)
    {
        Hue = Math.Clamp(p.X / Math.Max(1, ActualWidth), 0, 1) * 360;
        InvalidateVisual();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>HSV ↔ RGB 转换（同 Meteor hsvChanged 算法）。</summary>
public static class ColorHelper
{
    public static Color FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        double r, g, b;
        if (s <= 0)
        {
            r = g = b = v;
        }
        else
        {
            var hh = h / 60.0;
            var i = (int)hh;
            var ff = hh - i;
            var p = v * (1 - s);
            var q = v * (1 - s * ff);
            var t = v * (1 - s * (1 - ff));
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
        }
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    public static (double h, double s, double v) ToHsv(Color c)
    {
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
        }
        if (h < 0) h += 360;
        var s = max <= 0 ? 0 : d / max;
        return (h, s, max);
    }
}
