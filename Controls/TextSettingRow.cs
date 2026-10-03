using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinClient2.Controls;

/// <summary>
/// Meteor 风格文本设置行：标签 + 常显输入框 + 重置箭头。输入即生效。
/// </summary>
public class TextSettingRow : Grid
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(TextSettingRow), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(TextSettingRow),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(string), typeof(TextSettingRow), new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string DefaultValue
    {
        get => (string)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <summary>文本变化时触发。</summary>
    public event EventHandler? ValueChanged;

    private readonly TextBlock _label;
    private readonly TextBox _textBox;
    private readonly Border _resetButton;
    private bool _syncing;

    public TextSettingRow()
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

        _textBox = new TextBox
        {
            FontFamily = mono,
            FontSize = 13,
            Foreground = text,
            Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal"),
            CaretBrush = text,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 3, 6, 3),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _textBox.TextChanged += OnTextBoxTextChanged;
        Grid.SetColumn(_textBox, 1);
        Children.Add(_textBox);

        _resetButton = ToggleSettingRow.CreateResetButton(text, OnResetClicked);
        Grid.SetColumn(_resetButton, 2);
        Children.Add(_resetButton);
    }

    private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        Text = _textBox.Text;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnResetClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        Text = DefaultValue;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TextSettingRow)d)._label.Text = (string)e.NewValue;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (TextSettingRow)d;
        row._syncing = true;
        row._textBox.Text = (string)e.NewValue;
        row._syncing = false;
    }
}
