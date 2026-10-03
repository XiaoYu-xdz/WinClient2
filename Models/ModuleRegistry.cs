using System.Collections.Generic;
using System.Linq;

namespace WinClient2.Models;

/// <summary>模块注册表：所有可用模块在此注册，Modules 页据此渲染。</summary>
public static class ModuleRegistry
{
    public static readonly IReadOnlyList<Module> All = new List<Module>
    {
        new Modules.BetterDesktopModule(),
    };

    public static IEnumerable<Module> ByCategory(ModuleCategory category) => All.Where(m => m.Category == category);
}
