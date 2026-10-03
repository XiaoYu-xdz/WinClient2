using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinClient2.Controls;

/// <summary>
/// 还原 Meteor WKeybind：标签 + 按键绑定按钮。
/// - 按钮显示当前按键名；单击进入监听（显示 "..."），按下任意键即绑定
/// - 监听中右键 = 重置为默认键；ESC = 取消监听
/// - 按键名格式与 config.json 约定一致（RShift / LShiftKey / LControlKey / LMenu / LWin / A–Z / 0–9 / F1–F12 / Space / Enter / Tab）
/// 以后新增模块的按键绑定直接复用本控件。
/// </summary>
public class KeybindSettingRow : Grid
{
    private readonly TextBlock _label;
    private readonly Border _button;
    private readonly TextBlock _buttonText;
    private bool _listening;
    private Window? _captureWindow;

    /// <summary>当前绑定按键名（如 "RShift"）。</summary>
    public string Value { get; private set; } = "RShift";

    /// <summary>默认按键名（监听中右键重置到该值）。</summary>
    public string DefaultValue { get; set; } = "RShift";

    /// <summary>绑定变化时触发。</summary>
    public event EventHandler? ValueChanged;

    public KeybindSettingRow(string label = "按键绑定")
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var mono = (FontFamily)Application.Current.FindResource("FontJetBrainsMono");
        var secondary = (Brush)Application.Current.FindResource("BrushTextSecondary");
        var text = (Brush)Application.Current.FindResource("BrushText");
        var normalBg = (Brush)Application.Current.FindResource("BrushBackgroundNormal");
        var hoverBg = (Brush)Application.Current.FindResource("BrushBackgroundHovered");

        _label = new TextBlock
        {
            Text = label,
            FontFamily = mono,
            FontSize = 13,
            Foreground = secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };
        Grid.SetColumn(_label, 0);
        Children.Add(_label);

        _buttonText = new TextBlock
        {
            FontFamily = mono,
            FontSize = 13,
            Foreground = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _button = new Border
        {
            Background = normalBg,
            Height = 24,
            MinWidth = 90,
            Cursor = Cursors.Hand,
            ToolTip = "单击修改按键；监听中右键恢复默认；ESC 取消",
            Child = _buttonText,
        };
        _button.MouseEnter += (_, _) => _button.Background = hoverBg;
        _button.MouseLeave += (_, _) => _button.Background = normalBg;
        _button.MouseLeftButtonDown += OnButtonClicked;
        _button.MouseRightButtonDown += OnButtonRightClicked;
        Grid.SetColumn(_button, 1);
        Children.Add(_button);

        RefreshLabel();
    }

    public void SetValue(string value)
    {
        Value = value;
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        _buttonText.Text = _listening ? "..." : Value;
    }

    private void OnButtonClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_listening) return;

        _listening = true;
        RefreshLabel();

        _captureWindow = Window.GetWindow(this);
        if (_captureWindow is not null)
            _captureWindow.PreviewKeyDown += OnCaptureKeyDown;
    }

    private void OnButtonRightClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_listening)
        {
            // 监听中右键：恢复默认键
            SetValue(DefaultValue);
            StopListening();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCaptureKeyDown(object sender, KeyEventArgs e)
    {
        if (!_listening) return;

        if (e.Key == Key.Escape)
        {
            // ESC 取消监听，保持原值
            e.Handled = true;
            StopListening();
            return;
        }

        var name = KeyToName(e.Key);
        if (name is null) return; // 不支持的键继续等待

        e.Handled = true;
        Value = name;
        StopListening();
        RefreshLabel();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StopListening()
    {
        _listening = false;
        if (_captureWindow is not null)
            _captureWindow.PreviewKeyDown -= OnCaptureKeyDown;
        _captureWindow = null;
        RefreshLabel();
    }

    /// <summary>WPF 键 → config.json 约定名称（与 BetterDesktop 的解析保持一致）。</summary>
    public static string? KeyToName(Key key)
    {
        if (key == Key.LeftShift) return "LShiftKey";
        if (key == Key.RightShift) return "RShift";
        if (key == Key.LeftCtrl) return "LControlKey";
        if (key == Key.LeftAlt) return "LMenu";
        if (key == Key.LWin) return "LWin";
        if (key == Key.Space) return "Space";
        if (key == Key.Enter) return "Enter";
        if (key == Key.Tab) return "Tab";
        if (key >= Key.A && key <= Key.Z) return key.ToString();
        if (key >= Key.D0 && key <= Key.D9) return key.ToString().Substring(1); // "D5" -> "5"
        if (key >= Key.F1 && key <= Key.F12) return key.ToString();
        return null;
    }
}
