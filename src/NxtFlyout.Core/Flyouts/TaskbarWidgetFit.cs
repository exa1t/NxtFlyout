namespace NxtFlyout.Core.Flyouts;

/// <summary>
/// How much of the taskbar widget is shown, from everything to the bare minimum. When space is
/// short the widget steps down this list until it fits.
/// </summary>
public enum TaskbarWidgetLayout
{
    /// <summary>Cover, full-width title and artist, buttons and visualizer.</summary>
    Full,

    /// <summary>As <see cref="Full"/>, with a narrower title and artist.</summary>
    NarrowText,

    /// <summary>Cover, narrow title and artist, and buttons: no visualizer.</summary>
    NoVisualizer,

    /// <summary>Cover and buttons only.</summary>
    Minimal,
}

/// <summary>How much of the taskbar stats panel is shown when space is short.</summary>
public enum TaskbarStatsLayout
{
    /// <summary>Load figures on top, transfer rates below.</summary>
    TwoLines,

    /// <summary>Only the first line that has anything in it.</summary>
    OneLine,
}

/// <summary>A horizontal stretch of the taskbar, from <see cref="Left"/> to <see cref="Right"/>.</summary>
public readonly record struct TaskbarSpan(double Left, double Right)
{
    public double Width => Right - Left;
}

/// <summary>Works out where the taskbar widget has room and how much of it fits there.</summary>
public static class TaskbarWidgetFit
{
    /// <summary>
    /// Finds the free stretch of the taskbar for the widget.
    /// </summary>
    /// <param name="taskbarLeft">Left edge of the taskbar.</param>
    /// <param name="taskbarRight">Right edge of the taskbar.</param>
    /// <param name="preferredLeft">Where the widget would like to start (left end plus the user's offset).</param>
    /// <param name="obstacles">The taskbar's own buttons: Start, app icons, Widgets, the tray and so on.</param>
    /// <param name="gap">Space to keep clear on either side of the widget.</param>
    /// <returns>
    /// The free stretch, or <see langword="null"/> when there is none. If the preferred position
    /// lands on a button (for example the Widgets button at the far left, or the icons of a
    /// left-aligned taskbar), the widget moves to the first gap after it.
    /// </returns>
    public static TaskbarSpan? FindFreeSpan(
        double taskbarLeft,
        double taskbarRight,
        double preferredLeft,
        IEnumerable<TaskbarSpan> obstacles,
        double gap)
    {
        var sorted = obstacles
            .Where(o => o.Right > o.Left && o.Right > taskbarLeft && o.Left < taskbarRight)
            .OrderBy(o => o.Left)
            .ToList();

        var left = Math.Max(preferredLeft, taskbarLeft);

        // Step past every button the widget would touch. The list is sorted, so one pass also
        // walks through a row of buttons that sit edge to edge.
        foreach (var obstacle in sorted)
        {
            if (obstacle.Left < left + gap && obstacle.Right > left - gap)
            {
                left = Math.Max(left, obstacle.Right + gap);
            }
        }

        var right = taskbarRight - gap;
        foreach (var obstacle in sorted)
        {
            if (obstacle.Left >= left)
            {
                right = Math.Min(right, obstacle.Left - gap);
                break;
            }
        }

        return right > left ? new TaskbarSpan(left, right) : null;
    }

    /// <summary>
    /// Finds the free stretch of the taskbar that ends as far right as possible, at or before
    /// <paramref name="preferredRight"/>: the mirror image of <see cref="FindFreeSpan"/>, for
    /// something that sits against the tray (the notification icons and clock).
    /// </summary>
    /// <returns>
    /// The free stretch, or <see langword="null"/> when there is none. Buttons at the preferred
    /// position (the tray, when it is the right end of the taskbar) push it to the left of them.
    /// </returns>
    public static TaskbarSpan? FindFreeSpanBefore(
        double taskbarLeft,
        double taskbarRight,
        double preferredRight,
        IEnumerable<TaskbarSpan> obstacles,
        double gap)
    {
        // Mirror everything around zero, reuse FindFreeSpan, and mirror the answer back.
        var mirrored = obstacles.Select(o => new TaskbarSpan(-o.Right, -o.Left));
        var span = FindFreeSpan(-taskbarRight, -taskbarLeft, -Math.Min(preferredRight, taskbarRight), mirrored, gap);
        return span is { } s ? new TaskbarSpan(-s.Right, -s.Left) : null;
    }

    /// <summary>
    /// Picks the fullest layout whose width fits in <paramref name="availableWidth"/>, or
    /// <see langword="null"/> when not even <see cref="TaskbarWidgetLayout.Minimal"/> fits.
    /// </summary>
    /// <param name="availableWidth">The room there is.</param>
    /// <param name="widthOf">The width the widget needs in a given layout, in the same unit.</param>
    public static TaskbarWidgetLayout? ChooseLayout(double availableWidth, Func<TaskbarWidgetLayout, double> widthOf) =>
        ChooseLayout<TaskbarWidgetLayout>(availableWidth, widthOf);

    /// <summary>
    /// Picks the first layout, in declaration order (fullest first), whose width fits in
    /// <paramref name="availableWidth"/>, or <see langword="null"/> when none fits.
    /// </summary>
    public static TLayout? ChooseLayout<TLayout>(double availableWidth, Func<TLayout, double> widthOf)
        where TLayout : struct, Enum
    {
        // Half a pixel of tolerance for rounding between device and logical pixels.
        const double Tolerance = 0.5;

        foreach (var layout in Enum.GetValues<TLayout>())
        {
            if (widthOf(layout) <= availableWidth + Tolerance)
            {
                return layout;
            }
        }

        return null;
    }
}
