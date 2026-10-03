using System;

namespace WinClient2.Models;

/// <summary>数值设置项（滑条/输入框共用）：范围、步进、默认值与变更事件。</summary>
public class Setting
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public double Min { get; }
    public double Max { get; }
    public double Step { get; }
    public double DefaultValue { get; }
    public double Value { get; private set; }

    /// <summary>值变化时触发（钳制 + Step 吸附后）。</summary>
    public event EventHandler? Changed;

    public Setting(string id, string name, string description, double value, double defaultValue, double min, double max, double step)
    {
        Id = id;
        Name = name;
        Description = description;
        Min = min;
        Max = max;
        Step = step;
        DefaultValue = defaultValue;
        Value = Math.Clamp(value, min, max);
    }

    public void Set(double value)
    {
        value = Math.Clamp(value, Min, Max);
        if (Step > 0)
            value = Math.Round(value / Step) * Step;
        if (Math.Abs(value - Value) < 1e-9) return;
        Value = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset() => Set(DefaultValue);
}

/// <summary>通用设置项（bool / 枚举 / 字符串 / 颜色等），供设置面板渲染。</summary>
public class Setting<T>
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public T Value { get; private set; }
    public T DefaultValue { get; }

    /// <summary>值变化时触发。</summary>
    public event EventHandler? Changed;

    public Setting(string id, string name, string description, T value, T defaultValue)
    {
        Id = id;
        Name = name;
        Description = description;
        Value = value;
        DefaultValue = defaultValue;
    }

    public void Set(T value)
    {
        if (EqualityComparer<T>.Default.Equals(value, Value)) return;
        Value = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset() => Set(DefaultValue);
}
