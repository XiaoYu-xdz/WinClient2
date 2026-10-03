using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WinClient2.Controls;

/// <summary>
/// Meteor 风格开关行：标签 + 勾选框（还原 WMeteorCheckbox：深底 + 勾选时紫色方块动画）+ 重置箭头。
/// </summary>
public class ToggleSettingRow : Grid
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ToggleSettingRow), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(ToggleSettingRow),
        new FrameworkPropertyMetadata(false, OnIsCheckedChanged));

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(bool), typeof(ToggleSettingRow), new PropertyMetadata(false));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public bool DefaultValue
    {
        get => (bool)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <summary>勾选状态变化时触发。</summary>
    public event EventHandler? ValueChanged;

    private readonly TextBlock _label;
    private readonly Border _box;
    private readonly Rectangle _check;
    private readonly Border _resetButton;

    public ToggleSettingRow()
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

        // 勾选框：14x14 深底，勾选时紫色方块居中淡入
        _check = new Rectangle
        {
            Width = 10,
            Height = 10,
            Fill = (Brush)Application.Current.FindResource("BrushCheckbox"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        _box = new Border
        {
            Width = 14,
            Height = 14,
            Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal"),
            Cursor = Cursors.Hand,
            Child = _check,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _box.MouseEnter += (_, _) => _box.Background = (Brush)Application.Current.FindResource("BrushBackgroundHovered");
        _box.MouseLeave += (_, _) => _box.Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal");
        _box.MouseLeftButtonDown += OnBoxClicked;
        Grid.SetColumn(_box, 1);
        Children.Add(_box);

        _resetButton = CreateResetButton(text, OnResetClicked);
        Grid.SetColumn(_resetButton, 2);
        Children.Add(_resetButton);
    }

    private void OnBoxClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        IsChecked = !IsChecked;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnResetClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        IsChecked = DefaultValue;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ToggleSettingRow)d)._label.Text = (string)e.NewValue;

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (ToggleSettingRow)d;
        var anim = new DoubleAnimation((bool)e.NewValue ? 1 : 0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        row._check.BeginAnimation(OpacityProperty, anim);
    }

    /// <summary>圆形重置箭头按钮（还原 Meteor reset 按钮样式）。</summary>
    internal static Border CreateResetButton(Brush textBrush, MouseButtonEventHandler onClick)
    {
        var normal = (Brush)Application.Current.FindResource("BrushBackgroundNormal");
        var hovered = (Brush)Application.Current.FindResource("BrushBackgroundHovered");
        var button = new Border
        {
            Background = normal,
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
                Foreground = textBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        button.MouseEnter += (_, _) => button.Background = hovered;
        button.MouseLeave += (_, _) => button.Background = normal;
        button.MouseLeftButtonDown += onClick;
        return button;
    }
}
