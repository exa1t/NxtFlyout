namespace NxtHUD.Core.Audio;

/// <summary>
/// Supplies live bar levels for the taskbar visualizer from whatever the system is playing.
/// </summary>
public interface IAudioSpectrumSource
{
    /// <summary>
    /// Raised on the UI thread about 30 times a second while active. Each array holds one
    /// level per bar, from 0 (silent) to 1 (loud), lowest frequency first.
    /// </summary>
    event EventHandler<float[]>? LevelsUpdated;

    /// <summary>Starts or stops listening. Stopping releases the audio device.</summary>
    /// <param name="active">True while the visualizer is on screen and media is playing.</param>
    /// <param name="barCount">How many levels each <see cref="LevelsUpdated"/> carries.</param>
    void SetActive(bool active, int barCount);
}
