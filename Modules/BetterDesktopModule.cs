using System.Diagnostics;
using System.IO;
using System.Text;
using WinClient2.Models;

namespace WinClient2.Modules;

/// <summary>
/// BetterDesktop 悬浮窗控制模块：启用时启动 BetterDesktopPopup.exe，禁用时结束其进程。
/// 设置项写入 BetterDesktop 的 config.json（其 FileSystemWatcher 实时生效）。
/// </summary>
public class BetterDesktopModule : Module
{
    private const string ExePath = @"F:\XiaoLv\ZcodeProject\WinClient2\plugin\BetterDesktop-v1.0.0\BetterDesktopPopup.exe";
    private const string ConfigPath = @"F:\XiaoLv\ZcodeProject\WinClient2\plugin\BetterDesktop-v1.0.0\config.json";
    private const string ProcessName = "BetterDesktopPopup";

    /// <summary>鼠标移出文件夹后主窗口延迟关闭（毫秒）。</summary>
    public Setting LingerMs { get; }

    /// <summary>级联子窗口延迟关闭（毫秒）。</summary>
    public Setting ChildCloseMs { get; }

    /// <summary>弹窗间缝隙宽限期（毫秒）。</summary>
    public Setting GraceMs { get; }

    public BetterDesktopModule()
        : base("better-desktop", "BetterDesktop", ModuleCategory.Misc, "启动 / 关闭 BetterDesktop 桌面悬浮窗")
    {
        Keybind = "RShift";
        DefaultKeybind = "RShift";

        LingerMs = new Setting("lingerMs", "主窗口延迟", "鼠标移出文件夹后主窗口延迟关闭", 3000, 3000, 100, 5000, 50);
        ChildCloseMs = new Setting("childCloseMs", "子窗口延迟", "级联子窗口延迟关闭", 2500, 2500, 100, 5000, 50);
        GraceMs = new Setting("graceMs", "缝隙宽限期", "弹窗间缝隙宽限期", 500, 500, 100, 5000, 10);

        Settings.Add(LingerMs);
        Settings.Add(ChildCloseMs);
        Settings.Add(GraceMs);

        foreach (var setting in Settings)
            setting.Changed += (_, _) => SaveConfig();
    }

    protected override void OnEnable()
    {
        SaveConfig(); // 启动时把当前设置写入 config，实时生效
        try
        {
            Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = true });
        }
        catch
        {
            // 启动失败不中断应用
        }
    }

    protected override void OnDisable()
    {
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // 进程可能已退出
            }
        }
    }

    public override void OnKeybindChanged() => SaveConfig();

    /// <summary>写入 BetterDesktop 的 config.json（UTF-8，无 BOM 兼容其解析）。</summary>
    public void SaveConfig()
    {
        try
        {
            var keyName = string.IsNullOrEmpty(Keybind) ? DefaultKeybind : Keybind;
            var json = "{\n" +
                "  \"_comment\": \"悬停终止后窗口保持显示的毫秒数，修改后保存本文件立即生效，无需重启\",\n" +
                $"  \"lingerMs\": {Math.Round(LingerMs.Value)},\n" +
                $"  \"childCloseMs\": {Math.Round(ChildCloseMs.Value)},\n" +
                $"  \"graceMs\": {Math.Round(GraceMs.Value)},\n" +
                $"  \"hotkey\": \"{keyName}\"\n" +
                "}\n";
            File.WriteAllText(ConfigPath, json, new UTF8Encoding(false));
        }
        catch
        {
            // 写配置失败不中断
        }
    }
}
