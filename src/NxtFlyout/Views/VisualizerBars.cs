using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Views;

/// <summary>
/// Draws the taskbar visualizer: a row of rounded bars whose heights follow <see cref="Levels"/>.
/// </summary>
public sealed class VisualizerBars : FrameworkElement
{
    private const double BarWidth = 3;
    private const double BarGap = 2;
    private const double MinBarHeight = 2;

    /// <summary>Fraction of the control's height the tallest bar may use.</summary>
    private const double MaxHeightFraction = 0.62;

    public static readonly DependencyProperty LevelsProperty = DependencyProperty.Register(
        nameof(Levels),
        typeof(float[]),
        typeof(VisualizerBars),
        new FrameworkPropertyMetadata(Array.Empty<float>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarCountProperty = DependencyProperty.Register(
        nameof(BarCount),
        typeof(int),
        typeof(VisualizerBars),
        new FrameworkPropertyMetadata(AppSettings.DefaultVisualizerBars, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ColorModeProperty = DependencyProperty.Register(
        nameof(ColorMode),
        typeof(VisualizerColorMode),
        typeof(VisualizerBars),
        new FrameworkPropertyMetadata(VisualizerColorMode.Accent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CustomColorProperty = DependencyProperty.Register(
        nameof(CustomColor),
        typeof(string),
        typeof(VisualizerBars),
        new FrameworkPropertyMetadata(AppSettings.DefaultVisualizerColor, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AlignmentProperty = DependencyProperty.Register(
        nameof(Alignment),
        typeof(VisualizerAlignment),
        typeof(VisualizerBars),
        new FrameworkPropertyMetadata(VisualizerAlignment.Center, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Follows the window's text colour, which is what "Match taskbar" uses.</summary>
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(VisualizerBars),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public float[] Levels
    {
        get => (float[])GetValue(LevelsProperty);
        set => SetValue(LevelsProperty, value);
    }

    public int BarCount
    {
        get => (int)GetValue(BarCountProperty);
        set => SetValue(BarCountProperty, value);
    }

    public VisualizerColorMode ColorMode
    {
        get => (VisualizerColorMode)GetValue(ColorModeProperty);
        set => SetValue(ColorModeProperty, value);
    }

    public string CustomColor
    {
        get => (string)GetValue(CustomColorProperty);
        set => SetValue(CustomColorProperty, value);
    }

    public VisualizerAlignment Alignment
    {
        get => (VisualizerAlignment)GetValue(AlignmentProperty);
        set => SetValue(AlignmentProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Math.Max(1, BarCount);
        var width = (count * BarWidth) + ((count - 1) * BarGap);
        return new Size(width, double.IsInfinity(availableSize.Height) ? 24 : 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var count = Math.Max(1, BarCount);
        var levels = Levels ?? Array.Empty<float>();
        var brush = ResolveBrush();
        var height = ActualHeight;
        var maxBar = Math.Max(MinBarHeight, height * MaxHeightFraction);
        var centerY = height / 2;

        // The bars share one baseline: the bottom or top of the area the tallest bar can fill.
        var areaTop = centerY - (maxBar / 2);
        var areaBottom = centerY + (maxBar / 2);
        var alignment = Alignment;

        for (var i = 0; i < count; i++)
        {
            var level = i < levels.Length ? Math.Clamp(levels[i], 0f, 1f) : 0f;
            var barHeight = MinBarHeight + ((maxBar - MinBarHeight) * level);
            var x = i * (BarWidth + BarGap);
            var y = alignment switch
            {
                VisualizerAlignment.Bottom => areaBottom - barHeight,
                VisualizerAlignment.Top => areaTop,
                _ => centerY - (barHeight / 2),
            };
            var rect = new Rect(x, y, BarWidth, barHeight);
            drawingContext.DrawRoundedRectangle(brush, null, rect, BarWidth / 2, BarWidth / 2);
        }
    }

    private Brush ResolveBrush()
    {
        switch (ColorMode)
        {
            case VisualizerColorMode.Custom:
                if (TryParseColor(CustomColor, out var custom))
                {
                    return Frozen(new SolidColorBrush(custom));
                }

                break;

            case VisualizerColorMode.Accent:
                // WPF-UI publishes the Windows accent colour as a resource; the DWM colour is a fallback.
                var accent = TryFindResource("SystemAccentColorSecondary") ?? TryFindResource("SystemAccentColor");
                if (accent is Color accentColor)
                {
                    return Frozen(new SolidColorBrush(accentColor));
                }

                var glass = SystemParameters.WindowGlassColor;
                glass.A = 255;
                return Frozen(new SolidColorBrush(glass));
        }

        return Foreground ?? Brushes.White;
    }

    private static bool TryParseColor(string? text, out Color color)
    {
        color = default;
        if (!AppSettings.IsValidHexColor(text))
        {
            return false;
        }

        try
        {
            color = (Color)ColorConverter.ConvertFromString(text!.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Brush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
