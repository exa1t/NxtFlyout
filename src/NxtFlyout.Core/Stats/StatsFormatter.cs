using System.Globalization;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Core.Stats;

/// <summary>Turns a <see cref="SystemStats"/> reading into the taskbar panel's two lines of text.</summary>
/// <remarks>
/// Line 1 holds the load figures (CPU, RAM, GPU, VRAM, GPU temperature); line 2 the transfer
/// rates (network, disk). A figure that is switched on but could not be read shows as a dash,
/// except VRAM and GPU temperature, which are left out because many PCs never report them.
/// </remarks>
public static class StatsFormatter
{
    private const string Separator = "  •  ";
    private const string Missing = "–";

    /// <summary>What to measure for the figures switched on in <paramref name="settings"/>.</summary>
    public static SystemStatsKinds KindsFor(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var kinds = SystemStatsKinds.None;
        if (settings.StatsShowCpu) kinds |= SystemStatsKinds.Cpu;
        if (settings.StatsShowRam) kinds |= SystemStatsKinds.Ram;
        if (settings.StatsShowGpu) kinds |= SystemStatsKinds.Gpu;
        if (settings.StatsShowVram) kinds |= SystemStatsKinds.Vram;
        if (settings.StatsShowGpuTemperature) kinds |= SystemStatsKinds.GpuTemperature;
        if (settings.StatsShowNetwork) kinds |= SystemStatsKinds.Network;
        if (settings.StatsShowDisk) kinds |= SystemStatsKinds.Disk;
        return kinds;
    }

    public static (string Line1, string Line2) Format(SystemStats stats, SystemStatsKinds kinds, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(stats);
        culture ??= CultureInfo.CurrentCulture;

        var load = new List<string>();
        if (kinds.HasFlag(SystemStatsKinds.Cpu)) load.Add("CPU " + Percent(stats.CpuPercent, culture));
        if (kinds.HasFlag(SystemStatsKinds.Ram)) load.Add("RAM " + Percent(stats.RamPercent, culture));
        if (kinds.HasFlag(SystemStatsKinds.Gpu)) load.Add("GPU " + Percent(stats.GpuPercent, culture));
        if (kinds.HasFlag(SystemStatsKinds.Vram) && stats.VramBytes is { } vram) load.Add("VRAM " + Size(vram, culture));
        if (kinds.HasFlag(SystemStatsKinds.GpuTemperature) && stats.GpuTemperatureC is { } temperature)
        {
            load.Add(string.Format(culture, "{0:0}°C", temperature));
        }

        var transfer = new List<string>();
        if (kinds.HasFlag(SystemStatsKinds.Network))
        {
            transfer.Add("▲ " + Rate(stats.NetworkUpBytesPerSecond, culture) + "  ▼ " + Rate(stats.NetworkDownBytesPerSecond, culture));
        }

        if (kinds.HasFlag(SystemStatsKinds.Disk))
        {
            transfer.Add("Disk R " + Rate(stats.DiskReadBytesPerSecond, culture) + "  W " + Rate(stats.DiskWriteBytesPerSecond, culture));
        }

        return (string.Join(Separator, load), string.Join(Separator, transfer));
    }

    /// <summary>A whole percentage, such as "13%".</summary>
    public static string Percent(double? value, CultureInfo culture) =>
        value is { } v ? string.Format(culture, "{0:0}%", Math.Clamp(v, 0, 100)) : Missing;

    /// <summary>A transfer rate in the largest unit that keeps it readable: "0 KB/s", "340 KB/s", "1.2 MB/s", "45 MB/s".</summary>
    public static string Rate(double? bytesPerSecond, CultureInfo culture)
    {
        if (bytesPerSecond is not { } bytes)
        {
            return Missing;
        }

        bytes = Math.Max(0, bytes);
        const double Kb = 1024;
        const double Mb = Kb * 1024;
        const double Gb = Mb * 1024;

        return bytes switch
        {
            < Mb => string.Format(culture, "{0:0} KB/s", bytes / Kb),
            < 100 * Mb => string.Format(culture, "{0:0.0} MB/s", bytes / Mb),
            < Gb => string.Format(culture, "{0:0} MB/s", bytes / Mb),
            _ => string.Format(culture, "{0:0.0} GB/s", bytes / Gb),
        };
    }

    /// <summary>An amount of memory: "512 MB" or "2.1 GB".</summary>
    public static string Size(double bytes, CultureInfo culture)
    {
        const double Mb = 1024 * 1024;
        const double Gb = Mb * 1024;
        return bytes < Gb
            ? string.Format(culture, "{0:0} MB", Math.Max(0, bytes) / Mb)
            : string.Format(culture, "{0:0.0} GB", bytes / Gb);
    }
}
