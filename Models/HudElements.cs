using WinClient2.Services;

namespace WinClient2.Models;

/// <summary>HUD 元素状态（持久化）。</summary>
public class HudElementState
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>HUD 元素：根据系统状态快照生成显示文本。</summary>
public static class HudElements
{
    public static string Render(HudElementState element, SystemStats stats, DateTime now)
    {
        return element.Id switch
        {
            "clock" => now.ToString("HH:mm:ss    yyyy-MM-dd"),
            "system" => $"CPU {stats.CpuPercent:0}%   RAM {stats.RamUsedMb / 1024:0.0}G/{stats.RamTotalMb / 1024:0.0}G   " +
                        $"↓ {stats.NetDownKbps:0}K ↑ {stats.NetUpKbps:0}K",
            "battery" => stats.BatteryPercent >= 0 ? $"电池 {stats.BatteryPercent}%" : "电池 不可用",
            "custom" => RenderCustom(element.Text, stats, now),
            _ => "",
        };
    }

    public static string RenderCustom(string template, SystemStats stats, DateTime now)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return template
            .Replace("{time}", now.ToString("HH:mm:ss"))
            .Replace("{date}", now.ToString("yyyy-MM-dd"))
            .Replace("{cpu}", $"{stats.CpuPercent:0}%")
            .Replace("{ram}", $"{stats.RamUsedMb / 1024:0.0}G/{stats.RamTotalMb / 1024:0.0}G")
            .Replace("{ramUsed}", $"{stats.RamUsedMb / 1024:0.0}G")
            .Replace("{up}", $"{stats.NetUpKbps:0}K")
            .Replace("{down}", $"{stats.NetDownKbps:0}K")
            .Replace("{battery}", stats.BatteryPercent >= 0 ? $"{stats.BatteryPercent}%" : "N/A");
    }

    /// <summary>默认元素集合。</summary>
    public static List<HudElementState> Defaults() => new()
    {
        new HudElementState { Id = "clock", Enabled = true, X = 20, Y = 20 },
        new HudElementState { Id = "system", Enabled = true, X = 20, Y = 48 },
        new HudElementState { Id = "custom", Enabled = false, X = 20, Y = 76, Text = "WinClient2  {time}" },
        new HudElementState { Id = "battery", Enabled = false, X = 20, Y = 104 },
    };

    public static string DisplayName(string id) => id switch
    {
        "clock" => "时钟 / 日期",
        "system" => "系统状态",
        "custom" => "自定义文字",
        "battery" => "电池",
        _ => id,
    };
}
