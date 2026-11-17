using NxtFlyout.Core.Settings;

namespace NxtFlyout.Core.Flyouts;

/// <summary>A rectangle on screen, in whatever unit the caller works in.</summary>
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>Works out where a flyout goes and how long its animation runs.</summary>
public static class FlyoutPlacement
{
    /// <summary>
    /// Returns the top-left corner for a flyout of the given size inside
    /// <paramref name="workArea"/> (the screen minus the taskbar), so "bottom" always means
    /// "just above the taskbar" wherever the taskbar is docked.
    /// </summary>
    public static (double Left, double Top) Calculate(
        ScreenRect workArea,
        double width,
        double height,
        FlyoutVerticalPosition vertical,
        FlyoutHorizontalPosition horizontal,
        double margin = 0)
    {
        var left = horizontal switch
        {
            FlyoutHorizontalPosition.Left => workArea.X + margin,
            FlyoutHorizontalPosition.Right => workArea.Right - width - margin,
            _ => workArea.X + ((workArea.Width - width) / 2),
        };

        var top = vertical switch
        {
            FlyoutVerticalPosition.Top => workArea.Y + margin,
            _ => workArea.Bottom - height - margin,
        };

        // Never let the flyout hang off the work area, even if it is larger than expected.
        left = Math.Max(workArea.X, Math.Min(left, workArea.Right - width));
        top = Math.Max(workArea.Y, Math.Min(top, workArea.Bottom - height));

        return (left, top);
    }

    /// <summary>Breaks a combined position into its vertical and horizontal parts.</summary>
    public static (FlyoutVerticalPosition Vertical, FlyoutHorizontalPosition Horizontal) Split(FlyoutPosition position)
    {
        return position switch
        {
            FlyoutPosition.TopLeft => (FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Left),
            FlyoutPosition.TopCenter => (FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Center),
            FlyoutPosition.TopRight => (FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Right),
            FlyoutPosition.BottomLeft => (FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Left),
            FlyoutPosition.BottomRight => (FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Right),
            _ => (FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Center),
        };
    }

    /// <summary>How long the show/hide animation lasts, in milliseconds. Zero means no animation.</summary>
    public static int GetAnimationDurationMs(AnimationSpeed speed)
    {
        return speed switch
        {
            AnimationSpeed.Off => 0,
            AnimationSpeed.Fast => 120,
            AnimationSpeed.Slow => 420,
            _ => 220,
        };
    }
}
