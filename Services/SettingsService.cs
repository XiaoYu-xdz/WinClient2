using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using WinClient2.Models;

namespace WinClient2.Services;

/// <summary>应用全局设置（主题色、字体、对齐、动画速度等），JSON 持久化到 %AppData%\WinClient2\config.json。</summary>
public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();

    private const string DirName = "WinClient2";
    private const string FileName = "config.json";
    private static readonly string ConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DirName, FileName);

    /// <summary>任一设置变化时触发（用于实时应用）。</summary>
    public event Action? Changed;

    // ---- General（GUI 页） ----
    public double GuiScale { get; set; } = 0.75;
    public double AnimationSpeed { get; set; } = 1.0;
    public string ModuleAlignment { get; set; } = "Center";
    public bool CategoryIcons { get; set; }
    public bool ModulesHelpText { get; set; } = true;

    // ---- Config Visual ----
    public bool CustomFont { get; set; } = true;
    public string FontName { get; set; } = "JetBrains Mono";
    public bool CustomWindowTitle { get; set; }
    public string WindowTitleText { get; set; } = "WinClient2";


    // ---- 主题色（默认 = Meteor） ----
    public Color Accent { get; set; } = C(145, 61, 226);
    public Color Checkbox { get; set; } = C(145, 61, 226);
    public Color Plus { get; set; } = C(50, 255, 50);
    public Color Minus { get; set; } = C(255, 50, 50);
    public Color Favorite { get; set; } = C(250, 215, 0);

    public Color Text { get; set; } = C(255, 255, 255);
    public Color TextSecondary { get; set; } = C(150, 150, 150);
    public Color TextHighlight { get; set; } = Color.FromArgb(100, 45, 125, 245);
    public Color TitleText { get; set; } = C(255, 255, 255);
    public Color Placeholder { get; set; } = Color.FromArgb(20, 255, 255, 255);

    public Color BackgroundNormal { get; set; } = Color.FromArgb(200, 20, 20, 20);
    public Color BackgroundHovered { get; set; } = Color.FromArgb(200, 30, 30, 30);
    public Color BackgroundPressed { get; set; } = Color.FromArgb(200, 40, 40, 40);
    public Color ModuleBackground { get; set; } = C(50, 50, 50);

    public Color OutlineNormal { get; set; } = C(0, 0, 0);
    public Color OutlineHovered { get; set; } = C(10, 10, 10);
    public Color OutlinePressed { get; set; } = C(20, 20, 20);

    public Color SliderLeft { get; set; } = C(100, 35, 170);
    public Color SliderRight { get; set; } = C(50, 50, 50);
    public Color SliderHandleNormal { get; set; } = C(130, 0, 255);
    public Color SliderHandleHovered { get; set; } = C(140, 30, 255);
    public Color SliderHandlePressed { get; set; } = C(150, 60, 255);

    // ---- HUD ----
    public bool HudActive { get; set; }
    public double HudOpacity { get; set; } = 0.9;
    public double HudScale { get; set; } = 1.0;
    public bool HudClickThrough { get; set; } = true;
    public List<HudElementState> HudElements { get; set; } = WinClient2.Models.HudElements.Defaults();

    public void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions);
            if (dto is null) return;
            ApplyDto(dto);
        }
        catch
        {
            // 配置损坏则使用默认值
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var json = JsonSerializer.Serialize(ToDto(), JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
        }
    }

    public void NotifyChanged() => Changed?.Invoke();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ColorJsonConverter() },
    };

    private Dto ToDto() => new()
    {
        GuiScale = GuiScale,
        AnimationSpeed = AnimationSpeed,
        ModuleAlignment = ModuleAlignment,
        CategoryIcons = CategoryIcons,
        ModulesHelpText = ModulesHelpText,
        CustomFont = CustomFont,
        FontName = FontName,
        CustomWindowTitle = CustomWindowTitle,
        WindowTitleText = WindowTitleText,
        HudActive = HudActive, HudOpacity = HudOpacity, HudScale = HudScale, HudClickThrough = HudClickThrough,
        HudElements = HudElements,
        Accent = Accent, Checkbox = Checkbox, Plus = Plus, Minus = Minus, Favorite = Favorite,
        Text = Text, TextSecondary = TextSecondary, TextHighlight = TextHighlight, TitleText = TitleText, Placeholder = Placeholder,
        BackgroundNormal = BackgroundNormal, BackgroundHovered = BackgroundHovered, BackgroundPressed = BackgroundPressed, ModuleBackground = ModuleBackground,
        OutlineNormal = OutlineNormal, OutlineHovered = OutlineHovered, OutlinePressed = OutlinePressed,
        SliderLeft = SliderLeft, SliderRight = SliderRight,
        SliderHandleNormal = SliderHandleNormal, SliderHandleHovered = SliderHandleHovered, SliderHandlePressed = SliderHandlePressed,
    };

    private void ApplyDto(Dto d)
    {
        GuiScale = d.GuiScale;
        AnimationSpeed = d.AnimationSpeed;
        ModuleAlignment = d.ModuleAlignment;
        CategoryIcons = d.CategoryIcons;
        ModulesHelpText = d.ModulesHelpText;
        CustomFont = d.CustomFont;
        FontName = d.FontName;
        CustomWindowTitle = d.CustomWindowTitle;
        WindowTitleText = d.WindowTitleText;
        HudActive = d.HudActive;
        HudOpacity = d.HudOpacity;
        HudScale = d.HudScale;
        HudClickThrough = d.HudClickThrough;
        // 旧配置无元素数据时回退默认元素
        HudElements = d.HudElements is { Count: > 0 } ? d.HudElements : WinClient2.Models.HudElements.Defaults();
        Accent = d.Accent; Checkbox = d.Checkbox; Plus = d.Plus; Minus = d.Minus; Favorite = d.Favorite;
        Text = d.Text; TextSecondary = d.TextSecondary; TextHighlight = d.TextHighlight; TitleText = d.TitleText; Placeholder = d.Placeholder;
        BackgroundNormal = d.BackgroundNormal; BackgroundHovered = d.BackgroundHovered; BackgroundPressed = d.BackgroundPressed; ModuleBackground = d.ModuleBackground;
        OutlineNormal = d.OutlineNormal; OutlineHovered = d.OutlineHovered; OutlinePressed = d.OutlinePressed;
        SliderLeft = d.SliderLeft; SliderRight = d.SliderRight;
        SliderHandleNormal = d.SliderHandleNormal; SliderHandleHovered = d.SliderHandleHovered; SliderHandlePressed = d.SliderHandlePressed;
    }

    private class Dto
    {
        public double GuiScale { get; set; }
        public double AnimationSpeed { get; set; }
        public string ModuleAlignment { get; set; } = "Center";
        public bool CategoryIcons { get; set; }
        public bool ModulesHelpText { get; set; } = true;
        public bool CustomFont { get; set; } = true;
        public string FontName { get; set; } = "JetBrains Mono";
        public bool CustomWindowTitle { get; set; }
        public string WindowTitleText { get; set; } = "WinClient2";
        public bool HudActive { get; set; }
        public double HudOpacity { get; set; } = 0.9;
        public double HudScale { get; set; } = 1.0;
        public bool HudClickThrough { get; set; } = true;
        public List<HudElementState> HudElements { get; set; } = new();

        public Color Accent { get; set; }
        public Color Checkbox { get; set; }
        public Color Plus { get; set; }
        public Color Minus { get; set; }
        public Color Favorite { get; set; }
        public Color Text { get; set; }
        public Color TextSecondary { get; set; }
        public Color TextHighlight { get; set; }
        public Color TitleText { get; set; }
        public Color Placeholder { get; set; }
        public Color BackgroundNormal { get; set; }
        public Color BackgroundHovered { get; set; }
        public Color BackgroundPressed { get; set; }
        public Color ModuleBackground { get; set; }
        public Color OutlineNormal { get; set; }
        public Color OutlineHovered { get; set; }
        public Color OutlinePressed { get; set; }
        public Color SliderLeft { get; set; }
        public Color SliderRight { get; set; }
        public Color SliderHandleNormal { get; set; }
        public Color SliderHandleHovered { get; set; }
        public Color SliderHandlePressed { get; set; }
    }

    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}

/// <summary>Color ↔ "#AARRGGBB" 字符串的 JSON 转换。</summary>
public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => (Color)ColorConverter.ConvertFromString(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
