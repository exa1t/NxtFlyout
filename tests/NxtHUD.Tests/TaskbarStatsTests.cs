using System.Globalization;
using NxtHUD.Core.Flyouts;
using NxtHUD.Core.Settings;
using NxtHUD.Core.Stats;
using NxtHUD.Core.ViewModels;

namespace NxtHUD.Tests;

/// <summary>An <see cref="ISystemStatsSource"/> that records what it was asked for and emits readings on demand.</summary>
internal sealed class FakeStatsSource : ISystemStatsSource
{
    public List<(SystemStatsKinds Kinds, TimeSpan Interval)> Calls { get; } = new();

    public SystemStatsKinds Kinds => Calls.Count == 0 ? SystemStatsKinds.None : Calls[^1].Kinds;

    public event EventHandler<SystemStats>? Updated;

    public void SetActive(SystemStatsKinds kinds, TimeSpan interval) => Calls.Add((kinds, interval));

    public void Emit(SystemStats stats) => Updated?.Invoke(this, stats);
}

public class StatsFormatterTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private const SystemStatsKinds Everything =
        SystemStatsKinds.Cpu | SystemStatsKinds.Ram | SystemStatsKinds.Gpu | SystemStatsKinds.Vram
        | SystemStatsKinds.GpuTemperature | SystemStatsKinds.Network | SystemStatsKinds.Disk;

    [Fact]
    public void A_full_reading_fills_both_lines()
    {
        var stats = new SystemStats
        {
            CpuPercent = 13.4,
            RamPercent = 68,
            GpuPercent = 5.6,
            VramBytes = 2.1 * 1024 * 1024 * 1024,
            GpuTemperatureC = 54.6,
            NetworkUpBytesPerSecond = 12 * 1024,
            NetworkDownBytesPerSecond = 340 * 1024,
            DiskReadBytesPerSecond = 1.2 * 1024 * 1024,
            DiskWriteBytesPerSecond = 0,
        };

        var (line1, line2) = StatsFormatter.Format(stats, Everything, Invariant);

        Assert.Equal("CPU 13%  •  RAM 68%  •  GPU 6%  •  VRAM 2.1 GB  •  55°C", line1);
        Assert.Equal("▲ 12 KB/s  ▼ 340 KB/s  •  Disk R 1.2 MB/s  W 0 KB/s", line2);
    }

    [Fact]
    public void Figures_not_yet_read_show_a_dash()
    {
        var (line1, line2) = StatsFormatter.Format(new SystemStats(), SystemStatsKinds.Cpu | SystemStatsKinds.Network, Invariant);

        Assert.Equal("CPU –", line1);
        Assert.Equal("▲ –  ▼ –", line2);
    }

    [Fact]
    public void Vram_and_temperature_are_left_out_when_the_driver_does_not_report_them()
    {
        var stats = new SystemStats { GpuPercent = 40 };

        var (line1, _) = StatsFormatter.Format(stats, SystemStatsKinds.Gpu | SystemStatsKinds.Vram | SystemStatsKinds.GpuTemperature, Invariant);

        Assert.Equal("GPU 40%", line1);
    }

    [Fact]
    public void Only_chosen_figures_appear()
    {
        var stats = new SystemStats { CpuPercent = 50, RamPercent = 60, DiskReadBytesPerSecond = 0, DiskWriteBytesPerSecond = 0 };

        var (line1, line2) = StatsFormatter.Format(stats, SystemStatsKinds.Ram, Invariant);

        Assert.Equal("RAM 60%", line1);
        Assert.Equal(string.Empty, line2);
    }

    [Theory]
    [InlineData(0, "0 KB/s")]
    [InlineData(-5, "0 KB/s")]
    [InlineData(1536, "2 KB/s")]
    [InlineData(1023 * 1024, "1023 KB/s")]
    [InlineData(1024 * 1024, "1.0 MB/s")]
    [InlineData(45.25 * 1024 * 1024, "45.3 MB/s")]
    [InlineData(250 * 1024 * 1024, "250 MB/s")]
    [InlineData(2.5 * 1024 * 1024 * 1024, "2.5 GB/s")]
    public void Rates_use_a_readable_unit(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, StatsFormatter.Rate(bytesPerSecond, Invariant));
    }

    [Theory]
    [InlineData(-3, "0%")]
    [InlineData(99.6, "100%")]
    [InlineData(140, "100%")]
    public void Percentages_stay_between_0_and_100(double value, string expected)
    {
        Assert.Equal(expected, StatsFormatter.Percent(value, Invariant));
    }

    [Fact]
    public void Kinds_follow_the_settings()
    {
        var settings = new AppSettings
        {
            StatsShowCpu = true,
            StatsShowRam = false,
            StatsShowGpu = false,
            StatsShowVram = true,
            StatsShowGpuTemperature = false,
            StatsShowNetwork = false,
            StatsShowDisk = true,
        };

        Assert.Equal(SystemStatsKinds.Cpu | SystemStatsKinds.Vram | SystemStatsKinds.Disk, StatsFormatter.KindsFor(settings));
    }
}

public class TaskbarStatsViewModelTests
{
    private static (InMemorySettingsService Settings, FakeStatsSource Source, TaskbarStatsViewModel ViewModel) Create(Action<AppSettings>? setup = null)
    {
        var settings = new InMemorySettingsService();
        if (setup is not null)
        {
            settings.Update(setup);
        }

        var source = new FakeStatsSource();
        return (settings, source, new TaskbarStatsViewModel(settings, source, () => { }));
    }

    [Fact]
    public void Off_by_default_and_measures_nothing()
    {
        var (_, source, viewModel) = Create();

        Assert.False(viewModel.IsVisible);
        Assert.Equal(SystemStatsKinds.None, source.Kinds);
    }

    [Fact]
    public void Switching_on_starts_measuring_the_chosen_figures_at_the_chosen_pace()
    {
        var (settings, source, viewModel) = Create();

        settings.Update(s =>
        {
            s.TaskbarStatsEnabled = true;
            s.StatsIntervalMs = 2000;
        });

        Assert.True(viewModel.IsVisible);
        var expected = StatsFormatter.KindsFor(settings.Current);
        Assert.Equal((expected, TimeSpan.FromSeconds(2)), source.Calls[^1]);
        Assert.Contains("CPU –", viewModel.Line1);
    }

    [Fact]
    public void Readings_update_the_lines()
    {
        var (_, source, viewModel) = Create(s =>
        {
            s.TaskbarStatsEnabled = true;
            s.StatsShowGpu = false;
            s.StatsShowNetwork = false;
            s.StatsShowDisk = false;
        });

        source.Emit(new SystemStats { CpuPercent = 42, RamPercent = 70 });

        Assert.Equal("CPU 42%  •  RAM 70%", viewModel.Line1);
        Assert.Equal(string.Empty, viewModel.Line2);
    }

    [Fact]
    public void Unrelated_settings_changes_leave_the_figures_alone()
    {
        var (settings, source, viewModel) = Create(s => s.TaskbarStatsEnabled = true);
        source.Emit(new SystemStats { CpuPercent = 42 });
        var calls = source.Calls.Count;

        settings.Update(s => s.Theme = AppTheme.Dark);

        Assert.StartsWith("CPU 42%", viewModel.Line1);
        Assert.Equal(calls, source.Calls.Count);
    }

    [Fact]
    public void Choosing_no_figures_hides_the_panel_and_stops_measuring()
    {
        var (settings, source, viewModel) = Create(s => s.TaskbarStatsEnabled = true);

        settings.Update(s =>
        {
            s.StatsShowCpu = false;
            s.StatsShowRam = false;
            s.StatsShowGpu = false;
            s.StatsShowNetwork = false;
            s.StatsShowDisk = false;
        });

        Assert.False(viewModel.IsVisible);
        Assert.Equal(SystemStatsKinds.None, source.Kinds);
    }

    [Fact]
    public void Dispose_stops_measuring()
    {
        var (_, source, viewModel) = Create(s => s.TaskbarStatsEnabled = true);

        viewModel.Dispose();

        Assert.Equal(SystemStatsKinds.None, source.Kinds);
    }
}

public class TaskbarStatsSettingsTests
{
    [Theory]
    [InlineData(100, AppSettings.MinStatsIntervalMs)]
    [InlineData(60000, AppSettings.MaxStatsIntervalMs)]
    [InlineData(1500, 1500)]
    public void The_interval_is_kept_in_range(int stored, int expected)
    {
        var settings = new AppSettings { StatsIntervalMs = stored };

        settings.Normalize();

        Assert.Equal(expected, settings.StatsIntervalMs);
    }

    [Fact]
    public void The_panel_is_off_until_switched_on()
    {
        Assert.False(new AppSettings().TaskbarStatsEnabled);
    }

    [Fact]
    public void The_settings_page_stores_the_interval_in_milliseconds()
    {
        var settings = new InMemorySettingsService();
        using var viewModel = new SettingsViewModel(settings, new FakeStartupService());

        viewModel.StatsIntervalSeconds = 2.5;

        Assert.Equal(2500, settings.Current.StatsIntervalMs);
        Assert.Equal(2.5, viewModel.StatsIntervalSeconds);
    }
}

public class TaskbarStatsFitTests
{
    private const double Gap = 10;

    // Positions measured on a real Windows 11 taskbar: centred icons end at 1558, the tray starts at 1900.
    private static readonly TaskbarSpan[] Taskbar =
    [
        new(843, 1558),
        new(1900, 1940),
        new(1940, 1995),
        new(2000, 2030),
        new(2035, 2065),
        new(2070, 2105),
        new(2115, 2380),
        new(2385, 2400),
    ];

    [Fact]
    public void The_free_space_ends_just_before_the_tray()
    {
        var span = TaskbarWidgetFit.FindFreeSpanBefore(0, 2400, 2400, Taskbar, Gap);

        Assert.Equal(new TaskbarSpan(1568, 1890), span);
    }

    [Fact]
    public void The_widget_on_the_same_side_is_kept_clear()
    {
        // Left-aligned icons push the widget to the right of them, next to where the stats go.
        TaskbarSpan[] buttons = [new(0, 400), new(410, 700), new(1900, 2400)];

        var span = TaskbarWidgetFit.FindFreeSpanBefore(0, 2400, 2400, buttons, Gap);

        Assert.Equal(new TaskbarSpan(710, 1890), span);
    }

    [Fact]
    public void A_full_taskbar_leaves_no_room()
    {
        Assert.Null(TaskbarWidgetFit.FindFreeSpanBefore(0, 2400, 2400, [new TaskbarSpan(0, 2400)], Gap));
    }

    [Theory]
    [InlineData(400, TaskbarStatsLayout.TwoLines)]
    [InlineData(250, TaskbarStatsLayout.OneLine)]
    public void Two_lines_drop_to_one_when_short_of_room(double room, TaskbarStatsLayout expected)
    {
        var widths = new Dictionary<TaskbarStatsLayout, double>
        {
            [TaskbarStatsLayout.TwoLines] = 300,
            [TaskbarStatsLayout.OneLine] = 220,
        };

        Assert.Equal(expected, TaskbarWidgetFit.ChooseLayout<TaskbarStatsLayout>(room, layout => widths[layout]));
    }

    [Fact]
    public void Nothing_fits_in_a_tiny_gap()
    {
        Assert.Null(TaskbarWidgetFit.ChooseLayout<TaskbarStatsLayout>(100, _ => 220));
    }
}
