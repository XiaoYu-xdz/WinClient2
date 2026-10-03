using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WinClient2.Controls;

/// <summary>
/// Meteor 风格下拉设置行：标签 + 下拉（还原 WMeteorDropdown：深底 + 居中文字 + 右侧三角）+ 重置箭头。
/// 点击展开选项列表，悬停高亮，选择即生效。
/// </summary>
public class EnumSettingRow : Grid
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(EnumSettingRow), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(string[]), typeof(EnumSettingRow), new PropertyMetadata(Array.Empty<string>(), OnOptionsChanged));

    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(
        nameof(Selected), typeof(string), typeof(EnumSettingRow),
        new FrameworkPropertyMetadata(string.Empty, OnSelectedChanged));

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(string), typeof(EnumSettingRow), new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string[] Options
    {
        get => (string[])GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string Selected
    {
        get => (string)GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public string DefaultValue
    {
        get => (string)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <summary>选中项变化时触发。</summary>
    public event EventHandler? ValueChanged;

    private readonly TextBlock _label;
    private readonly Border _dropdown;
    private readonly TextBlock _dropdownText;
    private readonly Popup _popup;
    private readonly StackPanel _optionsPanel;
    private readonly Border _resetButton;

    public EnumSettingRow()
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

        // 下拉按钮
        _dropdownText = new TextBlock
        {
            FontFamily = mono,
            FontSize = 13,
            Foreground = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0),
        };
        _dropdown = new Border
        {
            Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal"),
            Height = 24,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Grid
            {
                Children =
                {
                    _dropdownText,
                    new TextBlock
                    {
                        Text = "▼",
                        FontFamily = new FontFamily("Segoe UI Symbol"),
                        FontSize = 9,
                        Foreground = text,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                    },
                },
            },
        };
        _dropdown.MouseEnter += (_, _) => _dropdown.Background = (Brush)Application.Current.FindResource("BrushBackgroundHovered");
        _dropdown.MouseLeave += (_, _) => _dropdown.Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal");
        _dropdown.MouseLeftButtonDown += OnDropdownClicked;
        Grid.SetColumn(_dropdown, 1);
        Children.Add(_dropdown);

        // 选项弹出层
        _optionsPanel = new StackPanel();
        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _dropdown,
            AllowsTransparency = true,
            StaysOpen = true, // 单击后常亮，再单击关闭（避免松开鼠标即消失）
            Child = new Border
            {
                Background = (Brush)Application.Current.FindResource("BrushBackgroundPressed"),
                MinWidth = 120,
                Child = _optionsPanel,
            },
        };
        _popup.Closed += (_, _) => _dropdown.Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal");

        _resetButton = ToggleSettingRow.CreateResetButton(text, OnResetClicked);
        Grid.SetColumn(_resetButton, 2);
        Children.Add(_resetButton);
    }

    private void OnDropdownClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }
        RebuildOptions();
        _popup.IsOpen = true;
    }

    private void RebuildOptions()
    {
        _optionsPanel.Children.Clear();
        foreach (var option in Options)
        {
            var row = new Border
            {
                Background = option == Selected
                    ? (Brush)Application.Current.FindResource("BrushBackgroundNormal")
                    : Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = option,
                    FontFamily = (FontFamily)Application.Current.FindResource("FontJetBrainsMono"),
                    FontSize = 13,
                    Foreground = option == Selected
                        ? (Brush)Application.Current.FindResource("BrushText")
                        : (Brush)Application.Current.FindResource("BrushTextSecondary"),
                    Margin = new Thickness(8, 4, 8, 4),
                },
            };
            var captured = option;
            row.MouseEnter += (_, _) => row.Background = (Brush)Application.Current.FindResource("BrushBackgroundHovered");
            row.MouseLeave += (_, _) => row.Background = captured == Selected
                ? (Brush)Application.Current.FindResource("BrushBackgroundNormal")
                : Brushes.Transparent;
            row.MouseLeftButtonDown += (_, _) =>
            {
                _popup.IsOpen = false;
                Selected = captured;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            _optionsPanel.Children.Add(row);
        }
    }

    private void OnResetClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Selected = DefaultValue;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((EnumSettingRow)d)._label.Text = (string)e.NewValue;

    private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((EnumSettingRow)d)._dropdownText.Text = ((EnumSettingRow)d).Selected;

    private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((EnumSettingRow)d)._dropdownText.Text = (string)e.NewValue;
}
