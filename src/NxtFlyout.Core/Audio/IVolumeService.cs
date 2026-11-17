namespace NxtFlyout.Core.Audio;

/// <summary>The master volume of the default playback device.</summary>
/// <param name="Level">0.0 (silent) to 1.0 (full).</param>
/// <param name="IsMuted">Whether the device is muted.</param>
public readonly record struct VolumeState(double Level, bool IsMuted);

/// <summary>Reads and changes the system volume.</summary>
public interface IVolumeService
{
    /// <summary>The current volume, or null when there is no playback device or it cannot be read.</summary>
    VolumeState? GetState();

    /// <summary>Raises the volume one step (and unmutes). Returns false if it could not be done.</summary>
    bool StepUp();

    /// <summary>Lowers the volume one step. Returns false if it could not be done.</summary>
    bool StepDown();

    /// <summary>Mutes or unmutes. Returns false if it could not be done.</summary>
    bool ToggleMute();
}
