using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinClient2.Controls;
using WinClient2.Models;
using WinClient2.Services;

namespace WinClient2.Views;

/// <summary>
/// 覆盖层窗口：全屏透明置顶窗口。
/// - 顶栏（Top Bar）：固定于屏幕顶部居中，不可拖动（还原 Meteor top().centerX()）
/// - 窗口全部为独立的 MeteorWindow（紫色标题栏可拖动/折叠）：
///   · Modules 页：六大分类各一个独立浮动窗口 + 底部帮助文本
///   · 其余页：共用一个页面窗口显示各标签内容
/// - 全局缩放 UiScale：ScaleHost 整体 RenderTransform 缩放
/// 右 Alt 唤起/隐藏、ESC 关闭（由 GlobalHotkeyService 触发）。
/// </summary>
public partial class OverlayWindow : Window
{
    /// <summary>Meteor 顶栏标签顺序（与 WTopBar/Tabs 一致，PathManager 忽略）。</summary>
    private static readonly (string Key, string Title)[] Tabs =
    {
        ("Modules", "Modules"),
        ("Config", "Config"),
        ("GUI", "GUI"),
        ("HUD", "HUD"),
        ("Friends", "Friends"),
        ("Macros", "Macros"),
        ("Profiles", "Profiles"),
    };

    /// <summary>GUI 全局缩放（Meteor scale 0.75–4，默认取设置值）。</summary>
    public double UiScale { get; private set; } = SettingsService.Instance.GuiScale;

    // 基准尺寸（RenderTransform 缩放，布局坐标不随缩放变化）
    private const double BaseButtonFontSize = 19;
    private const double BaseButtonPadX = 13;
    private const double BaseButtonPadY = 10;
    private const double BaseHintFontSize = 13;
    private const double BaseSectionFontSize = 16;
    private const double PageRowWidth = 460 - 2 * (8 + 14);

    private static readonly string[] FontOptions =
    {
        "JetBrains Mono", "Comfortaa", "Consolas", "Cascadia Mono", "Courier New", "Segoe UI", "Microsoft YaHei UI",
    };

    private readonly Dictionary<string, FrameworkElement> _pages = new();
    private readonly Dictionary<string, Point> _windowPositions = new();
    private string? _currentTab;
    private bool _needsDefaultPosition;

    /// <summary>标签页共用窗口（Config/GUI/HUD/Friends/Macros/Profiles）。</summary>
    private readonly MeteorWindow _pageWindow;

    /// <summary>Modules 页六大分类独立窗口。</summary>
    private readonly Dictionary<ModuleCategory, MeteorWindow> _categoryWindows = new();

    /// <summary>模块设置面板（右键模块打开，还原 Meteor ModuleScreen）。</summary>
    private MeteorWindow? _moduleSettingsWindow;

    /// <summary>设置面板当前显示的模块（再次右键同一模块关闭它）。</summary>
    private Module? _settingsModule;

    /// <summary>Modules 页底部帮助文本。</summary>
    private readonly TextBlock _modulesHelpText;

    public OverlayWindow()
    {
        InitializeComponent();
        ApplyWindowTitle();

        _modulesHelpText = new TextBlock
        {
            Text = "左键 - 切换模块    右键 - 打开设置",
            FontFamily = (FontFamily)FindResource("FontJetBrainsMono"),
            FontSize = BaseHintFontSize,
            Foreground = (Brush)FindResource("BrushTextSecondary"),
            Visibility = Visibility.Collapsed,
        };
        ScaleHost.Children.Add(_modulesHelpText);

        _pageWindow = new MeteorWindow("page", "Modules") { Visibility = Visibility.Collapsed };
        _pageWindow.PositionChanged += OnWindowPositionChanged;
        _pageWindow.SizeChanged += OnPageWindowSizeChanged;
        ScaleHost.Children.Add(_pageWindow);

        BuildCategoryWindows();
        BuildTabs();
        ApplyScale();
    }

    // ===== 窗口构建 =====

    private void BuildCategoryWindows()
    {
        foreach (var category in Enum.GetValues<ModuleCategory>())
        {
            if (_categoryWindows.TryGetValue(category, out var existing))
            {
                existing.Visibility = Visibility.Collapsed;
                ScaleHost.Children.Remove(existing);
            }

            var window = new MeteorWindow("Modules:" + category, category.ToString())
            {
                MinWidth = 180,
                Visibility = Visibility.Collapsed,
            };
            window.PositionChanged += OnWindowPositionChanged;
            ScaleHost.Children.Add(window);
            _categoryWindows[category] = window;
        }

        RebuildCategoryContents();
    }

    private void RebuildCategoryContents()
    {
        var settings = SettingsService.Instance;
        var mono = (FontFamily)FindResource("FontJetBrainsMono");
        var secondary = (Brush)FindResource("BrushTextSecondary");

        foreach (var pair in _categoryWindows)
        {
            var window = pair.Value;
            window.Title = settings.CategoryIcons ? "◆ " + pair.Key : pair.Key.ToString();
            window.Content.Content = null;

            var rows = new StackPanel();
            var modules = ModuleRegistry.ByCategory(pair.Key).ToList();
            if (modules.Count == 0)
            {
                rows.Children.Add(new TextBlock
                {
                    Text = "暂无模块",
                    FontFamily = mono,
                    FontSize = 13,
                    Foreground = secondary,
                    Margin = new Thickness(2, 4, 2, 4),
                });
            }
            foreach (var module in modules)
            {
                var row = new ModuleRow(module) { Margin = new Thickness(0, 4, 0, 0) };
                row.SettingsRequested += OpenModuleSettings;
                rows.Children.Add(row);
            }
            window.Content.Content = rows;
        }
    }

    private void RebuildUi()
    {
        BuildTabs();
        _pages.Clear();
        RebuildCategoryContents();
        _pageWindow.Content.Content = null;
        _moduleSettingsWindow?.Content.Content = null;
        if (_currentTab is not null)
            ShowPage(_currentTab);
        CenterTopBar();
        ApplyScale();
    }

    // ===== 顶栏标签 =====

    private void BuildTabs()
    {
        TabHost.Children.Clear();
        for (var i = 0; i < Tabs.Length; i++)
        {
            var (key, title) = Tabs[i];
            var button = new RadioButton
            {
                Content = title,
                Tag = key,
                GroupName = "TopBarTabs",
                Style = (Style)FindResource("TabButtonStyle"),
                FontSize = BaseButtonFontSize,
                Padding = new Thickness(BaseButtonPadX, BaseButtonPadY, BaseButtonPadX, BaseButtonPadY),
            };
            button.Checked += OnTabChecked;
            if (i == 0)
                button.IsChecked = true;
            TabHost.Children.Add(button);
        }
    }

    private void OnTabChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true } button)
            ShowPage((string)button.Tag);
    }

    // ===== 页面切换 =====

    private void ShowPage(string key)
    {
        _currentTab = key;

        if (key == "Modules")
        {
            _pageWindow.Visibility = Visibility.Collapsed;
            ShowCategoryWindows();
            UpdateModulesHelpText();
            return;
        }

        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "Config" => BuildConfigPage(),
                "GUI" => BuildGuiPage(),
                "HUD" => BuildHudPage(),
                _ => BuildPlaceholderPage(key),
            };
            _pages[key] = page;
        }

        HideCategoryWindows();
        if (_moduleSettingsWindow is not null)
            _moduleSettingsWindow.Visibility = Visibility.Collapsed;
        _pageWindow.ResetExpanded();
        _pageWindow.Title = key;
        _pageWindow.Content.Content = page;
        _pageWindow.Visibility = Visibility.Visible;
        UpdateModulesHelpText();

        if (_windowPositions.TryGetValue(key, out var position))
        {
            _pageWindow.SetCanvasPosition(position.X, position.Y);
        }
        else if (_pageWindow.ActualWidth > 0)
        {
            PlaceDefaultPosition();
        }
        else
        {
            _needsDefaultPosition = true;
        }
    }

    private void ShowCategoryWindows()
    {
        foreach (var window in _categoryWindows.Values)
            window.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(LayoutCategoryWindows);
    }

    private void HideCategoryWindows()
    {
        foreach (var window in _categoryWindows.Values)
            window.Visibility = Visibility.Collapsed;
    }

    private void UpdateModulesHelpText()
    {
        var visible = _currentTab == "Modules" && SettingsService.Instance.ModulesHelpText;
        _modulesHelpText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible && ActualWidth > 0)
        {
            _modulesHelpText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_modulesHelpText, (ActualWidth - _modulesHelpText.DesiredSize.Width) / 2);
            Canvas.SetTop(_modulesHelpText, ActualHeight - 40);
        }
    }

    /// <summary>分类窗口布局：单行并排居中（放不下自动换行），用户拖过的保持原位。</summary>
    private void LayoutCategoryWindows()
    {
        const double pad = 4;
        const double rowHeight = 40;
        const double topGap = 44;

        foreach (var pair in _categoryWindows)
        {
            if (_windowPositions.TryGetValue(pair.Value.Id, out var saved))
                pair.Value.SetCanvasPosition(saved.X, saved.Y);
        }

        var flow = _categoryWindows.Values
            .Where(w => w.ActualWidth > 0 && !w.IsDragging && !_windowPositions.ContainsKey(w.Id))
            .ToList();
        if (flow.Count == 0) return;

        double y = TabHost.ActualHeight + topGap;
        double totalWidth = flow.Sum(w => w.ActualWidth) + pad * (flow.Count - 1);
        double x = Math.Max(pad, (ActualWidth - totalWidth) / 2);

        foreach (var window in flow)
        {
            if (x + window.ActualWidth > ActualWidth)
            {
                x = pad;
                y += rowHeight;
            }
            window.SetCanvasPosition(x, y);
            x += window.ActualWidth + pad;
        }
    }

    private void OnWindowPositionChanged(object? sender, EventArgs e)
    {
        if (sender is not MeteorWindow window) return;
        var id = ReferenceEquals(window, _pageWindow) ? _currentTab : window.Id;
        if (id is null) return;
        _windowPositions[id] = window.GetCanvasPosition();
    }

    private void OnPageWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_needsDefaultPosition && _pageWindow.ActualWidth > 0)
        {
            PlaceDefaultPosition();
            _needsDefaultPosition = false;
        }
    }

    /// <summary>页面窗口默认位置：屏幕视觉居中（canvasY = (屏高/2)/scale - 高/2）。</summary>
    private void PlaceDefaultPosition()
    {
        var x = Math.Max(4, (ActualWidth - _pageWindow.ActualWidth) / 2);
        var y = Math.Max(4, (ActualHeight / 2) / UiScale - _pageWindow.ActualHeight / 2);
        _pageWindow.SetCanvasPosition(x, y);
    }

    // ===== 各页内容 =====

    private FrameworkElement BuildPlaceholderPage(string tabName)
    {
        return new TextBlock
        {
            Text = "该页面功能待实现（后续里程碑加入）。",
            FontFamily = (FontFamily)FindResource("FontJetBrainsMono"),
            FontSize = BaseHintFontSize,
            Foreground = (Brush)FindResource("BrushTextSecondary"),
            Margin = new Thickness(12, 18, 12, 18),
        };
    }

    // ===== Config 页：仅 Visual 组（还原 Meteor ConfigScreen Visual） =====

    private FrameworkElement BuildConfigPage()
    {
        var s = SettingsService.Instance;
        var panel = new StackPanel { Margin = new Thickness(14) };
        AddSectionTitle(panel, "Visual");

        var customFont = new ToggleSettingRow
        {
            Label = "自定义字体",
            IsChecked = s.CustomFont,
            DefaultValue = true,
            Width = PageRowWidth,
        };
        customFont.ValueChanged += (_, _) =>
        {
            s.CustomFont = customFont.IsChecked;
            ThemeService.ApplyAll();
            RebuildUi();
            s.Save();
        };
        panel.Children.Add(customFont);

        var font = new EnumSettingRow
        {
            Label = "字体",
            Options = FontOptions,
            Selected = s.FontName,
            DefaultValue = "JetBrains Mono",
            Width = PageRowWidth,
        };
        font.ValueChanged += (_, _) =>
        {
            s.FontName = font.Selected;
            ThemeService.ApplyAll();
            RebuildUi();
            s.Save();
        };
        panel.Children.Add(font);

        var customTitle = new ToggleSettingRow
        {
            Label = "自定义窗口标题",
            IsChecked = s.CustomWindowTitle,
            DefaultValue = false,
            Width = PageRowWidth,
        };
        customTitle.ValueChanged += (_, _) =>
        {
            s.CustomWindowTitle = customTitle.IsChecked;
            ApplyWindowTitle();
            s.Save();
        };
        panel.Children.Add(customTitle);

        var titleText = new TextSettingRow
        {
            Label = "窗口标题文本",
            Text = s.WindowTitleText,
            DefaultValue = "WinClient2",
            Width = PageRowWidth,
        };
        titleText.ValueChanged += (_, _) =>
        {
            s.WindowTitleText = titleText.Text;
            ApplyWindowTitle();
            s.Save();
        };
        panel.Children.Add(titleText);

        return panel;
    }

    private void ApplyWindowTitle()
    {
        var s = SettingsService.Instance;
        Title = s.CustomWindowTitle && !string.IsNullOrEmpty(s.WindowTitleText)
            ? s.WindowTitleText
            : "WinClient2";
    }

    // ===== GUI 页：General + Colors + Text + Background + Outline（还原 Meteor GuiScreen） =====

    private FrameworkElement BuildGuiPage()
    {
        var s = SettingsService.Instance;
        var panel = new StackPanel { Margin = new Thickness(14) };

        // 顶部操作行：重置颜色
        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 4),
        };
        var resetColors = CreateButton("重置颜色", (_, _) =>
        {
            ResetThemeColors();
            s.Save();
        });
        topRow.Children.Add(resetColors);
        panel.Children.Add(topRow);

        // General
        AddSectionTitle(panel, "General");

        var scale = new SliderSettingRow
        {
            Label = "GUI Scale",
            Min = 0.75,
            Max = 4,
            Step = 0.01,
            Value = UiScale,
            DefaultValue = 0.75,
            Width = PageRowWidth,
        };
        scale.ValueChanged += OnScaleSettingChanged;
        panel.Children.Add(scale);

        var animSpeed = new SliderSettingRow
        {
            Label = "GUI 动画速度",
            Min = 0.5,
            Max = 3,
            Step = 0.05,
            Value = s.AnimationSpeed,
            DefaultValue = 1,
            Width = PageRowWidth,
        };
        animSpeed.ValueChanged += (_, _) =>
        {
            s.AnimationSpeed = animSpeed.Value;
            s.Save();
        };
        panel.Children.Add(animSpeed);

        var alignment = new EnumSettingRow
        {
            Label = "模块文字对齐",
            Options = new[] { "Left", "Center", "Right" },
            Selected = s.ModuleAlignment,
            DefaultValue = "Center",
            Width = PageRowWidth,
        };
        alignment.ValueChanged += (_, _) =>
        {
            s.ModuleAlignment = alignment.Selected;
            RebuildCategoryContents();
            s.Save();
        };
        panel.Children.Add(alignment);

        var icons = new ToggleSettingRow
        {
            Label = "分类图标",
            IsChecked = s.CategoryIcons,
            DefaultValue = false,
            Width = PageRowWidth,
        };
        icons.ValueChanged += (_, _) =>
        {
            s.CategoryIcons = icons.IsChecked;
            RebuildCategoryContents();
            s.Save();
        };
        panel.Children.Add(icons);

        var help = new ToggleSettingRow
        {
            Label = "帮助文本",
            IsChecked = s.ModulesHelpText,
            DefaultValue = true,
            Width = PageRowWidth,
        };
        help.ValueChanged += (_, _) =>
        {
            s.ModulesHelpText = help.IsChecked;
            UpdateModulesHelpText();
            s.Save();
        };
        panel.Children.Add(help);

        // Colors
        AddSectionTitle(panel, "Colors");
        AddColorRow(panel, "Accent", s.Accent, C(145, 61, 226), v => s.Accent = v);
        AddColorRow(panel, "Checkbox", s.Checkbox, C(145, 61, 226), v => s.Checkbox = v);
        AddColorRow(panel, "Plus", s.Plus, C(50, 255, 50), v => s.Plus = v);
        AddColorRow(panel, "Minus", s.Minus, C(255, 50, 50), v => s.Minus = v);
        AddColorRow(panel, "Favorite", s.Favorite, C(250, 215, 0), v => s.Favorite = v);

        // Text
        AddSectionTitle(panel, "Text");
        AddColorRow(panel, "Text", s.Text, C(255, 255, 255), v => s.Text = v);
        AddColorRow(panel, "Text Secondary", s.TextSecondary, C(150, 150, 150), v => s.TextSecondary = v);
        AddColorRow(panel, "Text Highlight", s.TextHighlight, Color.FromArgb(100, 45, 125, 245), v => s.TextHighlight = v);
        AddColorRow(panel, "Title Text", s.TitleText, C(255, 255, 255), v => s.TitleText = v);
        AddColorRow(panel, "Placeholder", s.Placeholder, Color.FromArgb(20, 255, 255, 255), v => s.Placeholder = v);

        // Background
        AddSectionTitle(panel, "Background");
        AddColorRow(panel, "Background", s.BackgroundNormal, Color.FromArgb(200, 20, 20, 20), v => s.BackgroundNormal = v);
        AddColorRow(panel, "Background Hovered", s.BackgroundHovered, Color.FromArgb(200, 30, 30, 30), v => s.BackgroundHovered = v);
        AddColorRow(panel, "Background Pressed", s.BackgroundPressed, Color.FromArgb(200, 40, 40, 40), v => s.BackgroundPressed = v);
        AddColorRow(panel, "Module Background", s.ModuleBackground, C(50, 50, 50), v => s.ModuleBackground = v);

        // Outline
        AddSectionTitle(panel, "Outline");
        AddColorRow(panel, "Outline", s.OutlineNormal, C(0, 0, 0), v => s.OutlineNormal = v);
        AddColorRow(panel, "Outline Hovered", s.OutlineHovered, C(10, 10, 10), v => s.OutlineHovered = v);
        AddColorRow(panel, "Outline Pressed", s.OutlinePressed, C(20, 20, 20), v => s.OutlinePressed = v);

        return panel;
    }

    private void AddColorRow(StackPanel panel, string name, Color current, Color defaultColor, Action<Color> apply)
    {
        var row = new ColorSettingRow
        {
            Label = name,
            Color = current,
            DefaultColor = defaultColor,
            Width = PageRowWidth,
        };
        row.ColorChanged += (_, _) =>
        {
            apply(row.Color);
            ThemeService.ApplyAll();
            SettingsService.Instance.Save();
        };
        panel.Children.Add(row);
    }

    private void ResetThemeColors()
    {
        var s = SettingsService.Instance;
        s.Accent = C(145, 61, 226);
        s.Checkbox = C(145, 61, 226);
        s.Plus = C(50, 255, 50);
        s.Minus = C(255, 50, 50);
        s.Favorite = C(250, 215, 0);
        s.Text = C(255, 255, 255);
        s.TextSecondary = C(150, 150, 150);
        s.TextHighlight = Color.FromArgb(100, 45, 125, 245);
        s.TitleText = C(255, 255, 255);
        s.Placeholder = Color.FromArgb(20, 255, 255, 255);
        s.BackgroundNormal = Color.FromArgb(200, 20, 20, 20);
        s.BackgroundHovered = Color.FromArgb(200, 30, 30, 30);
        s.BackgroundPressed = Color.FromArgb(200, 40, 40, 40);
        s.ModuleBackground = C(50, 50, 50);
        s.OutlineNormal = C(0, 0, 0);
        s.OutlineHovered = C(10, 10, 10);
        s.OutlinePressed = C(20, 20, 20);
        s.SliderLeft = C(100, 35, 170);
        s.SliderRight = C(50, 50, 50);
        s.SliderHandleNormal = C(130, 0, 255);
        s.SliderHandleHovered = C(140, 30, 255);
        s.SliderHandlePressed = C(150, 60, 255);

        ThemeService.ApplyAll();
        _pages.Remove("GUI");
        if (_currentTab == "GUI")
            ShowPage("GUI");
    }

    private void AddSectionTitle(StackPanel panel, string title)
    {
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = (FontFamily)FindResource("FontComfortaa"),
            FontSize = BaseSectionFontSize,
            Foreground = (Brush)FindResource("BrushText"),
            Margin = new Thickness(0, 12, 0, 0),
        });
    }

    private static Border CreateButton(string label, MouseButtonEventHandler onClick)
    {
        var text = (Brush)Application.Current.FindResource("BrushText");
        var button = new Border
        {
            Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal"),
            Cursor = Cursors.Hand,
            Padding = new Thickness(12, 4, 12, 4),
            Child = new TextBlock
            {
                Text = label,
                FontFamily = (FontFamily)Application.Current.FindResource("FontJetBrainsMono"),
                FontSize = 13,
                Foreground = text,
            },
        };
        button.MouseEnter += (_, _) => button.Background = (Brush)Application.Current.FindResource("BrushBackgroundHovered");
        button.MouseLeave += (_, _) => button.Background = (Brush)Application.Current.FindResource("BrushBackgroundNormal");
        button.MouseLeftButtonDown += onClick;
        return button;
    }

    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    // ===== HUD 页（还原 Meteor HudScreen：总开关 + 编辑 + 通用设置 + 元素列表） =====

    private FrameworkElement BuildHudPage()
    {
        var s = SettingsService.Instance;
        var hud = ((App)Application.Current).Hud;
        var panel = new StackPanel { Margin = new Thickness(14) };

        // 顶部：编辑模式
        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 4),
        };
        var editButton = CreateButton("编辑模式", (_, _) => hud.SetEditMode(!hud.IsEditMode));
        topRow.Children.Add(editButton);
        panel.Children.Add(topRow);

        AddSectionTitle(panel, "General");

        var active = new ToggleSettingRow
        {
            Label = "HUD 总开关",
            IsChecked = s.HudActive,
            DefaultValue = false,
            Width = PageRowWidth,
        };
        active.ValueChanged += (_, _) =>
        {
            s.HudActive = active.IsChecked;
            hud.ApplySettings();
            s.Save();
        };
        panel.Children.Add(active);

        var clickThrough = new ToggleSettingRow
        {
            Label = "点击穿透",
            IsChecked = s.HudClickThrough,
            DefaultValue = true,
            Width = PageRowWidth,
        };
        clickThrough.ValueChanged += (_, _) =>
        {
            s.HudClickThrough = clickThrough.IsChecked;
            hud.ApplySettings();
            s.Save();
        };
        panel.Children.Add(clickThrough);

        var opacity = new SliderSettingRow
        {
            Label = "透明度",
            Min = 0.3,
            Max = 1,
            Step = 0.01,
            Value = s.HudOpacity,
            DefaultValue = 0.9,
            Width = PageRowWidth,
        };
        opacity.ValueChanged += (_, _) =>
        {
            s.HudOpacity = opacity.Value;
            hud.ApplySettings();
            s.Save();
        };
        panel.Children.Add(opacity);

        var scale = new SliderSettingRow
        {
            Label = "缩放",
            Min = 0.5,
            Max = 2,
            Step = 0.01,
            Value = s.HudScale,
            DefaultValue = 1,
            Width = PageRowWidth,
        };
        scale.ValueChanged += (_, _) =>
        {
            s.HudScale = scale.Value;
            hud.ApplySettings();
            s.Save();
        };
        panel.Children.Add(scale);

        AddSectionTitle(panel, "Elements");
        foreach (var element in s.HudElements)
        {
            var row = new ToggleSettingRow
            {
                Label = HudElements.DisplayName(element.Id),
                IsChecked = element.Enabled,
                DefaultValue = element.Id is "clock" or "system",
                Width = PageRowWidth,
            };
            row.ValueChanged += (_, _) =>
            {
                element.Enabled = row.IsChecked;
                hud.ApplySettings();
                s.Save();
            };
            panel.Children.Add(row);

            if (element.Id == "custom")
            {
                var text = new TextSettingRow
                {
                    Label = "内容",
                    Text = element.Text,
                    DefaultValue = "WinClient2  {time}",
                    Width = PageRowWidth,
                };
                text.ValueChanged += (_, _) =>
                {
                    element.Text = text.Text;
                    hud.ApplySettings();
                    s.Save();
                };
                panel.Children.Add(text);
            }
        }

        var reset = CreateButton("重置位置", (_, _) =>
        {
            foreach (var element in s.HudElements)
            {
                var def = HudElements.Defaults().FirstOrDefault(d => d.Id == element.Id);
                if (def is null) continue;
                element.X = def.X;
                element.Y = def.Y;
            }
            hud.ApplySettings();
            s.Save();
        });
        panel.Children.Add(reset);

        return panel;
    }

    // ===== 模块设置面板（还原 Meteor ModuleScreen：标题 + 描述 + 设置项滑条行） =====

    private void OpenModuleSettings(Module module)
    {
        // 再次右键同一模块：关闭设置窗口（右键 = 开关切换）
        if (_moduleSettingsWindow is { Visibility: Visibility.Visible } && ReferenceEquals(_settingsModule, module))
        {
            _moduleSettingsWindow.Visibility = Visibility.Collapsed;
            _settingsModule = null;
            return;
        }

        if (_moduleSettingsWindow is null)
        {
            _moduleSettingsWindow = new MeteorWindow("module-settings", module.Name)
            {
                MinWidth = 360,
                Visibility = Visibility.Collapsed,
            };
            _moduleSettingsWindow.PositionChanged += OnWindowPositionChanged;
            ScaleHost.Children.Add(_moduleSettingsWindow);
        }

        _settingsModule = module;
        _moduleSettingsWindow.Title = module.Name;

        var mono = (FontFamily)FindResource("FontJetBrainsMono");
        var secondary = (Brush)FindResource("BrushTextSecondary");

        var panel = new StackPanel { Margin = new Thickness(14) };
        if (!string.IsNullOrEmpty(module.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = module.Description,
                FontFamily = mono,
                FontSize = 12,
                Foreground = secondary,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        const double contentWidth = 360 - 2 * (8 + 14);
        foreach (var setting in module.Settings)
        {
            var row = new SliderSettingRow
            {
                Label = setting.Name,
                Min = setting.Min,
                Max = setting.Max,
                Step = setting.Step,
                Value = setting.Value,
                DefaultValue = setting.DefaultValue,
                Width = contentWidth,
                Margin = new Thickness(0, 12, 0, 0),
            };
            row.ValueChanged += (_, _) => setting.Set(row.Value);
            panel.Children.Add(row);
        }

        _moduleSettingsWindow.Content.Content = panel;
        _moduleSettingsWindow.Visibility = Visibility.Visible;

        if (_windowPositions.TryGetValue(_moduleSettingsWindow.Id, out var position))
        {
            _moduleSettingsWindow.SetCanvasPosition(position.X, position.Y);
        }
        else
        {
            var x = Math.Max(4, (ActualWidth - 360) / 2);
            var y = Math.Max(4, (ActualHeight / 2) / UiScale - 95);
            _moduleSettingsWindow.SetCanvasPosition(x, y);
        }
    }

    // ===== 缩放应用 =====

    private void OnScaleSettingChanged(object? sender, EventArgs e)
    {
        if (sender is not SliderSettingRow row) return;
        UiScale = row.Value;
        SettingsService.Instance.GuiScale = UiScale;
        SettingsService.Instance.Save();
        ApplyScale();
    }

    private void ApplyScale()
    {
        // RenderTransform：纯渲染缩放、不触发布局，滑块拖动不卡顿；以顶部中心为原点
        ScaleHost.RenderTransformOrigin = new Point(0.5, 0);
        ScaleHost.RenderTransform = new ScaleTransform(UiScale, UiScale);

        _pageWindow.ScaleFactor = UiScale;
        foreach (var window in _categoryWindows.Values)
            window.ScaleFactor = UiScale;

        if (_currentTab == "Modules")
        {
            Dispatcher.BeginInvoke(LayoutCategoryWindows);
        }
        else if (_currentTab is not null && !_windowPositions.ContainsKey(_currentTab))
        {
            if (_pageWindow.ActualWidth > 0)
                PlaceDefaultPosition();
            else
                _needsDefaultPosition = true;
        }
    }

    // ===== 顶栏定位（固定，居中于屏幕顶部） =====

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        CenterTopBar();
        UpdateModulesHelpText();
    }

    private void CenterTopBar()
    {
        var barWidth = TabHost.ActualWidth;
        var x = Math.Max(4, (ActualWidth - barWidth) / 2);
        Canvas.SetLeft(TopBarHost, x);
        Canvas.SetTop(TopBarHost, 0);
    }

    // ===== 显示 / 隐藏（带淡入淡出） =====

    /// <summary>唤起前的前台窗口句柄，关闭 GUI 后恢复，避免后续键盘输入落空。</summary>
    private IntPtr _previousForeground;

    public void ToggleGui()
    {
        if (IsVisible) HideGui();
        else ShowGui();
    }

    public void ShowGui()
    {
        if (IsVisible) return;
        // 唤起前记录前台窗口（窗口未显示时不能访问其 Handle，故不做自比）
        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero)
            _previousForeground = foreground;

        Opacity = 0;
        Show();
        Activate();
        AnimateOpacity(0, 1, 160 / SettingsService.Instance.AnimationSpeed);
        if (_currentTab == "Modules")
            Dispatcher.BeginInvoke(LayoutCategoryWindows);
    }

    public void HideGui()
    {
        if (!IsVisible) return;
        AnimateOpacity(Opacity, 0, 120 / SettingsService.Instance.AnimationSpeed, () =>
        {
            Hide();
            RestoreForeground();
        });
    }

    private void RestoreForeground()
    {
        if (_previousForeground == IntPtr.Zero) return;
        try
        {
            SetForegroundWindow(_previousForeground);
        }
        catch
        {
            // 焦点恢复失败则用户点击一次即可
        }
        _previousForeground = IntPtr.Zero;
    }

    private void AnimateOpacity(double from, double to, double milliseconds, Action? onCompleted = null)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        if (onCompleted is not null)
            animation.Completed += (_, _) => onCompleted();
        BeginAnimation(OpacityProperty, animation);
    }

    // ===== 工具窗样式：不出现在 Alt+Tab =====

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
