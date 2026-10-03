using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WinClient2.Controls;

/// <summary>
/// 还原 Meteor WMeteorSlider：
/// 轨道高 3px，左段 sliderLeft(100,35,170)、右段 sliderRight(50,50,50)；
/// 圆形手柄 sliderHandle(130,0,255)，悬停 140,30,255；
/// 点击轨道任意处定位，按住拖动连续调整（支持 Step 吸附）。
/// </summary>
public class MeteorSlider : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(MeteorSlider),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

    public static readonly DependencyProperty MinProperty = DependencyProperty.Register(
        nameof(Min), typeof(double), typeof(MeteorSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(double), typeof(MeteorSlider),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(MeteorSlider),
        new FrameworkPropertyMetadata(0.0));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Min
    {
        get => (double)GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    public double Max
    {
        get => (double)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>值变化时触发（用户拖动或点击）。</summary>
    public event EventHandler? ValueChanged;

    private bool _dragging;

    private static Brush GetBrush(string key, Color fallback)
        => App.Current.Resources[key] as Brush ?? new SolidColorBrush(fallback);

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var handleSize = HandleSize;
        var trackX = handleSize / 2;
        var trackY = (ActualHeight - 3) / 2;
        var valueWidth = ValueWidth;

        // 主题色（ThemeService 修改 Brush 单例后实时生效）
        var trackLeft = GetBrush("BrushSliderLeft", Color.FromRgb(100, 35, 170));
        var trackRight = GetBrush("BrushSliderRight", Color.FromRgb(50, 50, 50));
        var handle = GetBrush(
            _dragging ? "BrushSliderHandlePressed"
                : IsMouseOver ? "BrushSliderHandleHovered"
                : "BrushSliderHandleNormal",
            Color.FromRgb(130, 0, 255));

        dc.DrawRectangle(trackLeft, null, new Rect(trackX, trackY, valueWidth, 3));
        dc.DrawRectangle(trackRight, null,
            new Rect(trackX + valueWidth, trackY, Math.Max(0, ActualWidth - valueWidth - handleSize), 3));

        dc.DrawEllipse(handle, null,
            new Point(trackX + valueWidth + handleSize / 2, ActualHeight / 2), handleSize / 2, handleSize / 2);
    }

    private double HandleSize => Math.Min(ActualHeight, ActualWidth);

    private double ValueWidth
    {
        get
        {
            var range = Max - Min;
            if (range <= 0) return 0;
            return (ActualWidth - HandleSize) * Math.Clamp((Value - Min) / range, 0, 1);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragging = true;
        CaptureMouse();
        SetFromPosition(e.GetPosition(this).X);
        e.Handled = true;
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging && e.LeftButton == MouseButtonState.Pressed)
            SetFromPosition(e.GetPosition(this).X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
        base.OnMouseLeftButtonUp(e);
    }

    private void SetFromPosition(double x)
    {
        var range = Max - Min;
        if (range <= 0) return;

        var track = Math.Max(1, ActualWidth - HandleSize);
        var t = Math.Clamp((x - HandleSize / 2) / track, 0, 1);
        var value = Min + t * range;
        if (Step > 0)
            value = Math.Round(value / Step) * Step;
        Value = Math.Clamp(value, Min, Max);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MeteorSlider)d).ValueChanged?.Invoke(d, EventArgs.Empty);
    }
}
