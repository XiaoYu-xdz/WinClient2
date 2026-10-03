using System.Windows;
using System.Windows.Media;

namespace WinClient2.Services;

/// <summary>
/// 把 SettingsService 的设置应用到 App 资源。
/// App.xaml 里的 SolidColorBrush 是单例，所有控件引用同一实例，
/// 改 Color 即全局实时刷新，无需逐控件绑定。
/// </summary>
public static class ThemeService
{
    /// <summary>
    /// .NET WPF 会把 XAML 资源里的 Freezable（Brush）自动冻结为只读，
    /// 因此启动时用代码创建的未冻结 Brush 替换所有主题色资源，
    /// 之后 ThemeService 才能原地改色、全局实时生效。
    /// </summary>
    public static void EnsureMutableBrushes()
    {
        var s = SettingsService.Instance;

        Replace("BrushAccent", s.Accent);
        Replace("BrushCheckbox", s.Checkbox);
        Replace("BrushPlus", s.Plus);
        Replace("BrushMinus", s.Minus);
        Replace("BrushFavorite", s.Favorite);
        Replace("BrushText", s.Text);
        Replace("BrushTextSecondary", s.TextSecondary);
        Replace("BrushTextHighlight", s.TextHighlight);
        Replace("BrushTitleText", s.TitleText);
        Replace("BrushPlaceholder", s.Placeholder);
        Replace("BrushBackgroundNormal", s.BackgroundNormal);
        Replace("BrushBackgroundHovered", s.BackgroundHovered);
        Replace("BrushBackgroundPressed", s.BackgroundPressed);
        Replace("BrushModuleBackground", s.ModuleBackground);
        Replace("BrushOutlineNormal", s.OutlineNormal);
        Replace("BrushOutlineHovered", s.OutlineHovered);
        Replace("BrushOutlinePressed", s.OutlinePressed);
        Replace("BrushSliderLeft", s.SliderLeft);
        Replace("BrushSliderRight", s.SliderRight);
        Replace("BrushSliderHandleNormal", s.SliderHandleNormal);
        Replace("BrushSliderHandleHovered", s.SliderHandleHovered);
        Replace("BrushSliderHandlePressed", s.SliderHandlePressed);
    }

    private static void Replace(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        App.Current.Resources.Remove(key);
        App.Current.Resources.Add(key, brush);
    }

    public static void ApplyAll()
    {
        var s = SettingsService.Instance;

        Set("BrushAccent", s.Accent);
        Set("BrushCheckbox", s.Checkbox);
        Set("BrushPlus", s.Plus);
        Set("BrushMinus", s.Minus);
        Set("BrushFavorite", s.Favorite);

        Set("BrushText", s.Text);
        Set("BrushTextSecondary", s.TextSecondary);
        Set("BrushTextHighlight", s.TextHighlight);
        Set("BrushTitleText", s.TitleText);
        Set("BrushPlaceholder", s.Placeholder);

        Set("BrushBackgroundNormal", s.BackgroundNormal);
        Set("BrushBackgroundHovered", s.BackgroundHovered);
        Set("BrushBackgroundPressed", s.BackgroundPressed);
        Set("BrushModuleBackground", s.ModuleBackground);

        Set("BrushOutlineNormal", s.OutlineNormal);
        Set("BrushOutlineHovered", s.OutlineHovered);
        Set("BrushOutlinePressed", s.OutlinePressed);

        Set("BrushSliderLeft", s.SliderLeft);
        Set("BrushSliderRight", s.SliderRight);
        Set("BrushSliderHandleNormal", s.SliderHandleNormal);
        Set("BrushSliderHandleHovered", s.SliderHandleHovered);
        Set("BrushSliderHandlePressed", s.SliderHandlePressed);

        // 字体：自定义字体开启时用所选字体，否则回退 JetBrains Mono
        var font = s.CustomFont ? s.FontName : "JetBrains Mono";
        App.Current.Resources["FontJetBrainsMono"] = new FontFamily(font);
    }

    private static void Set(string key, Color color)
    {
        if (App.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
        }
        else
        {
            // 资源被冻结（XAML 声明）时改为整体替换
            Replace(key, color);
        }
    }
}
