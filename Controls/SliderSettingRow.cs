using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinClient2.Controls;

/// <summary>
/// Meteor 风格滑条设置行（还原 WDoubleEdit）：标签 + 常显数值输入框 + 滑条。
/// - 输入框始终可见，可直接键入数值（逐字符过滤：数字、一个小数点、一个前导负号）
/// - Enter / 失焦提交：解析 → 钳制到 [Min,Max] → Step 吸附 → 同步滑条；ESC 还原
/// - 拖动滑条时输入框数值实时跟随
/// 以后新增滑条设置直接复用本控件。
/// </summary>
public class SliderSettingRow : Grid
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SliderSettingRow),
        new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SliderSettingRow),
        new FrameworkPropertyMetadata(0.0, OnValuePropertyChanged));

    public static readonly DependencyProperty MinProperty = DependencyProperty.Register(
        nameof(Min), typeof(double), typeof(SliderSettingRow),
        new FrameworkPropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(double), typeof(SliderSettingRow),
        new FrameworkPropertyMetadata(1.0, OnRangeChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(SliderSettingRow),
        new FrameworkPropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(double), typeof(SliderSettingRow),
        new PropertyMetadata(0.0));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Min
    {
        get => (double)GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    public double Max
    {
        get => (double)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>默认值：点击行末的圆形重置箭头恢复到该值。</summary>
    public double DefaultValue
    {
        get => (double)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <summary>值变化时触发（提交输入或拖动滑块）。</summary>
    public event EventHandler? ValueChanged;

    private readonly TextBlock _label;
    private readonly TextBox _textBox;
    private readonly MeteorSlider _slider;
    private readonly Border _resetButton;
    private bool _syncing;

    private static readonly Regex NumberPattern = new(@"^-?\d*\.?\d*$", RegexOptions.Compiled);

    public SliderSettingRow()
    {
            Margin = new Thickness(0, 8, 0, 0); // 行距
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

        // 常显数值输入框（还原 Meteor WTextBox 视觉：深底、白字、无边框）
        _textBox = new TextBox
        {
            FontFamily = mono,
            FontSize = 13,
            Foreground = text,
            Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal"),
            CaretBrush = text,
            SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 45, 125, 245)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 3, 4, 3),
            MinWidth = 75,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _textBox.PreviewTextInput += OnPreviewTextInput;
        _textBox.KeyDown += OnKeyDown;
        _textBox.LostFocus += OnLostFocus;
        DataObject.AddPastingHandler(_textBox, OnPaste);
        Grid.SetColumn(_textBox, 1);
        Children.Add(_textBox);

        _slider = new MeteorSlider
        {
            Height = 18,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _slider.ValueChanged += OnSliderValueChanged;
        Grid.SetColumn(_slider, 2);
        Children.Add(_slider);

        // 圆形重置箭头（还原 Meteor reset 按钮）：点击恢复默认值
        var normalBg = (Brush)Application.Current.FindResource("BrushBackgroundNormal");
        var hoverBg = (Brush)Application.Current.FindResource("BrushBackgroundHovered");
        _resetButton = new Border
        {
            Background = normalBg,
            Width = 22,
            Height = 22,
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand,
            ToolTip = "重置为默认值",
            Child = new TextBlock
            {
                Text = "↺",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 15,
                Foreground = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        _resetButton.MouseEnter += (_, _) => _resetButton.Background = hoverBg;
        _resetButton.MouseLeave += (_, _) => _resetButton.Background = normalBg;
        _resetButton.MouseLeftButtonDown += OnResetClicked;
        Grid.SetColumn(_resetButton, 3);
        Children.Add(_resetButton);
    }

    private void OnResetClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var value = Math.Clamp(DefaultValue, Min, Max);
        if (Step > 0)
            value = Math.Round(value / Step) * Step;
        _textBox.Text = value.ToString("0.00");
        ApplyValue(value);
    }

    // ===== 输入过滤（还原 WDoubleEdit filter：数字、一个 '.', 一个前导 '-'） =====

    private static bool IsValidNumberText(string text) => NumberPattern.IsMatch(text);

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var box = (TextBox)sender;
        var proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength)
                              .Insert(box.SelectionStart, e.Text);
        if (!IsValidNumberText(proposed))
            e.Handled = true;
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(typeof(string)) is string s && !IsValidNumberText(s.Trim()))
            e.CancelCommand();
    }

    // ===== 提交 / 还原 =====

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Commit();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Revert();
        }
    }

    private void OnLostFocus(object sender, RoutedEventArgs e) => Commit();

    private void Commit()
    {
        var text = _textBox.Text.Trim();
        double value = text switch
        {
            "" or "." or "-." => 0,
            "-" => -0,
            _ => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : double.NaN,
        };
        if (double.IsNaN(value))
        {
            Revert();
            return;
        }

        value = Math.Clamp(value, Min, Max);
        if (Step > 0)
            value = Math.Round(value / Step) * Step;

        _textBox.Text = value.ToString("0.00"); // 钳制/吸附后回写显示
        ApplyValue(value);
    }

    private void Revert()
    {
        _textBox.Text = Value.ToString("0.00");
    }

    private void ApplyValue(double value)
    {
        if (Math.Abs(value - Value) < 1e-9) return;
        _syncing = true;
        Value = value;
        _syncing = false;
        _slider.Value = value;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    // ===== 滑块 -> 值/文字同步 =====

    private void OnSliderValueChanged(object? sender, EventArgs e)
    {
        var value = _slider.Value;
        _syncing = true;
        Value = value;
        _syncing = false;
        _textBox.Text = value.ToString("0.00");
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    // ===== 属性同步 =====

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SliderSettingRow)d)._label.Text = (string)e.NewValue;

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (SliderSettingRow)d;
        row._slider.Min = row.Min;
        row._slider.Max = row.Max;
        row._slider.Step = row.Step;
    }

    private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (SliderSettingRow)d;
        if (row._syncing) return; // 滑块/输入框同步路径已自行更新
        row._textBox.Text = row.Value.ToString("0.00");
        row._slider.Value = row.Value;
        row.ValueChanged?.Invoke(row, EventArgs.Empty);
    }
}
