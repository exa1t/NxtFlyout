using NxtFlyout.Core.Flyouts;

namespace NxtFlyout.Tests;

public class TaskbarWidgetFitTests
{
    private const double Gap = 10;

    // The taskbar measured on a real 2400 px wide Windows 11 screen with centred icons:
    // Start, 12 app icons edge to edge, then the tray and clock on the right.
    private static readonly TaskbarSpan[] CentredTaskbar =
    [
        new(843, 899),
        .. Enumerable.Range(0, 12).Select(i => new TaskbarSpan(898 + (i * 55), 953 + (i * 55))),
        new(1900, 1940),
        new(1940, 1995),
        new(2115, 2380),
        new(2385, 2400),
    ];

    [Fact]
    public void Centred_icons_leave_the_space_up_to_the_start_button()
    {
        var span = TaskbarWidgetFit.FindFreeSpan(0, 2400, 10, CentredTaskbar, Gap);

        Assert.Equal(new TaskbarSpan(10, 833), span);
    }

    [Fact]
    public void An_empty_taskbar_is_free_up_to_its_right_end()
    {
        var span = TaskbarWidgetFit.FindFreeSpan(0, 2400, 10, [], Gap);

        Assert.Equal(new TaskbarSpan(10, 2390), span);
    }

    [Fact]
    public void A_button_at_the_preferred_position_pushes_the_widget_past_it()
    {
        // The Widgets (weather) button at the far left of a centred taskbar.
        TaskbarSpan[] buttons = [new(0, 200), new(843, 899)];

        var span = TaskbarWidgetFit.FindFreeSpan(0, 2400, 10, buttons, Gap);

        Assert.Equal(new TaskbarSpan(210, 833), span);
    }

    [Fact]
    public void Left_aligned_icons_move_the_widget_after_the_last_icon()
    {
        // Start and five apps edge to edge from the left, the tray on the right.
        var buttons = Enumerable.Range(0, 6)
            .Select(i => new TaskbarSpan(i * 55, (i + 1) * 55))
            .Append(new TaskbarSpan(1900, 2400));

        var span = TaskbarWidgetFit.FindFreeSpan(0, 2400, 10, buttons, Gap);

        Assert.Equal(new TaskbarSpan(340, 1890), span);
    }

    [Fact]
    public void The_users_offset_is_kept_when_it_lands_in_free_space()
    {
        var span = TaskbarWidgetFit.FindFreeSpan(0, 2400, 300, CentredTaskbar, Gap);

        Assert.Equal(new TaskbarSpan(300, 833), span);
    }

    [Fact]
    public void A_full_taskbar_has_no_free_span()
    {
        TaskbarSpan[] buttons = [new(0, 2400)];

        Assert.Null(TaskbarWidgetFit.FindFreeSpan(0, 2400, 10, buttons, Gap));
    }

    [Fact]
    public void Buttons_off_the_taskbar_and_empty_ones_are_ignored()
    {
        TaskbarSpan[] buttons = [new(-500, -100), new(3000, 3100), new(400, 400)];

        var span = TaskbarWidgetFit.FindFreeSpan(0, 2400, 10, buttons, Gap);

        Assert.Equal(new TaskbarSpan(10, 2390), span);
    }

    private static readonly Dictionary<TaskbarWidgetLayout, double> Widths = new()
    {
        [TaskbarWidgetLayout.Full] = 400,
        [TaskbarWidgetLayout.NarrowText] = 320,
        [TaskbarWidgetLayout.NoVisualizer] = 250,
        [TaskbarWidgetLayout.Minimal] = 150,
    };

    [Theory]
    [InlineData(1000, TaskbarWidgetLayout.Full)]
    [InlineData(400, TaskbarWidgetLayout.Full)]
    [InlineData(399, TaskbarWidgetLayout.NarrowText)]
    [InlineData(300, TaskbarWidgetLayout.NoVisualizer)]
    [InlineData(200, TaskbarWidgetLayout.Minimal)]
    public void The_fullest_layout_that_fits_is_chosen(double room, TaskbarWidgetLayout expected)
    {
        Assert.Equal(expected, TaskbarWidgetFit.ChooseLayout(room, layout => Widths[layout]));
    }

    [Fact]
    public void Nothing_is_chosen_when_even_the_minimal_layout_does_not_fit()
    {
        Assert.Null(TaskbarWidgetFit.ChooseLayout(100, layout => Widths[layout]));
    }

    [Fact]
    public void Rounding_by_less_than_half_a_pixel_still_fits()
    {
        Assert.Equal(TaskbarWidgetLayout.Full, TaskbarWidgetFit.ChooseLayout(399.6, layout => Widths[layout]));
    }
}
