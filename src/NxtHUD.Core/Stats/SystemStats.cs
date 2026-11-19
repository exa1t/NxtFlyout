namespace NxtHUD.Core.Stats;

/// <summary>Which figures a <see cref="ISystemStatsSource"/> should measure.</summary>
[Flags]
public enum SystemStatsKinds
{
    None = 0,
    Cpu = 1,
    Ram = 2,
    Gpu = 4,
    Vram = 8,
    GpuTemperature = 16,
    Network = 32,
    Disk = 64,
}

/// <summary>
/// One reading of the system's load. Each figure is <see langword="null"/> when it was not asked
/// for or could not be read (for example a GPU temperature the driver does not report).
/// </summary>
public sealed record SystemStats
{
    /// <summary>Processor use across all cores, 0 to 100.</summary>
    public double? CpuPercent { get; init; }

    /// <summary>Physical memory in use, 0 to 100.</summary>
    public double? RamPercent { get; init; }

    /// <summary>Busiest GPU engine, 0 to 100, as Task Manager shows it.</summary>
    public double? GpuPercent { get; init; }

    /// <summary>Dedicated GPU memory in use, in bytes.</summary>
    public double? VramBytes { get; init; }

    /// <summary>Hottest GPU, in degrees Celsius.</summary>
    public double? GpuTemperatureC { get; init; }

    public double? NetworkUpBytesPerSecond { get; init; }

    public double? NetworkDownBytesPerSecond { get; init; }

    public double? DiskReadBytesPerSecond { get; init; }

    public double? DiskWriteBytesPerSecond { get; init; }
}

/// <summary>Measures the system's load at a steady pace while something shows it.</summary>
public interface ISystemStatsSource
{
    /// <summary>Raised on the UI thread with each new reading while active.</summary>
    event EventHandler<SystemStats>? Updated;

    /// <summary>Starts, changes or stops measuring.</summary>
    /// <param name="kinds">What to measure; <see cref="SystemStatsKinds.None"/> stops.</param>
    /// <param name="interval">Time between readings.</param>
    void SetActive(SystemStatsKinds kinds, TimeSpan interval);
}
