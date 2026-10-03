using System;

namespace WinClient2.Models;

/// <summary>Meteor 六大模块分类。</summary>
public enum ModuleCategory
{
    Combat,
    Movement,
    Render,
    Player,
    World,
    Misc,
}

/// <summary>
/// 模块基类：数据 + 开关生命周期。具体模块继承并实现 OnEnable/OnDisable。
/// </summary>
public abstract class Module
{
    public string Id { get; }
    public string Name { get; }
    public ModuleCategory Category { get; }
    public string Description { get; }
    public bool Enabled { get; private set; }
    public bool Favorite { get; set; }
    public string? Keybind { get; set; }
    public string? DefaultKeybind { get; set; }

    /// <summary>模块设置项（右键模块打开的设置面板据此渲染）。</summary>
    public List<Setting> Settings { get; } = new();

    /// <summary>启用/禁用状态变化时触发。</summary>
    public event EventHandler? StateChanged;

    protected Module(string id, string name, ModuleCategory category, string description)
    {
        Id = id;
        Name = name;
        Category = category;
        Description = description;
    }

    public void Toggle()
    {
        Enabled = !Enabled;
        if (Enabled) OnEnable();
        else OnDisable();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnEnable() { }
    protected virtual void OnDisable() { }

    /// <summary>按键绑定变化时调用（子类持久化到配置）。</summary>
    public virtual void OnKeybindChanged() { }
}
