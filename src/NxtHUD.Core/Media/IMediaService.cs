namespace NxtHUD.Core.Media;

/// <summary>Follows the system's current media session and can control it.</summary>
public interface IMediaService
{
    /// <summary>The current session, or null when nothing is publishing media info.</summary>
    MediaSnapshot? Current { get; }

    /// <summary>Raised on the UI thread whenever <see cref="Current"/> changes.</summary>
    event EventHandler? Changed;

    Task TogglePlayPauseAsync();

    Task SkipNextAsync();

    Task SkipPreviousAsync();
}

/// <summary>The media keys the app reacts to.</summary>
public enum MediaKey
{
    PlayPause,
    Next,
    Previous,
    Stop,
}

/// <summary>Maps Windows virtual-key codes to <see cref="MediaKey"/>.</summary>
public static class MediaKeyMap
{
    public const int VkMediaNextTrack = 0xB0;
    public const int VkMediaPreviousTrack = 0xB1;
    public const int VkMediaStop = 0xB2;
    public const int VkMediaPlayPause = 0xB3;

    /// <summary>Returns the media key for a virtual-key code, or null if it is not one.</summary>
    public static MediaKey? FromVirtualKey(int virtualKey)
    {
        return virtualKey switch
        {
            VkMediaPlayPause => MediaKey.PlayPause,
            VkMediaNextTrack => MediaKey.Next,
            VkMediaPreviousTrack => MediaKey.Previous,
            VkMediaStop => MediaKey.Stop,
            _ => null,
        };
    }
}

/// <summary>
/// A restartable one-shot timer. Abstracted so the flyout's auto-hide logic can be unit
/// tested without waiting for real time.
/// </summary>
public interface IDelayTimer
{
    /// <summary>Raised once, <c>delay</c> after the most recent <see cref="Start"/>.</summary>
    event EventHandler? Elapsed;

    /// <summary>Starts the countdown, replacing any countdown already running.</summary>
    void Start(TimeSpan delay);

    void Stop();
}
