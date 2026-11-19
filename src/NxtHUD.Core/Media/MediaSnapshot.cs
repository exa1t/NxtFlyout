namespace NxtHUD.Core.Media;

/// <summary>What a media session is doing, mirroring the states Windows reports.</summary>
public enum MediaPlaybackState
{
    Unknown,
    Closed,
    Opened,
    Changing,
    Stopped,
    Playing,
    Paused,
}

/// <summary>
/// An immutable picture of the current media session at one moment: what is playing,
/// whether it is playing, where in the track it is and which controls the player offers.
/// </summary>
public sealed record MediaSnapshot
{
    /// <summary>The app user model id of the player, for example "Spotify.exe".</summary>
    public string SourceAppId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string AlbumTitle { get; init; } = string.Empty;

    public MediaPlaybackState PlaybackState { get; init; } = MediaPlaybackState.Unknown;

    public bool CanTogglePlayPause { get; init; }

    public bool CanGoNext { get; init; }

    public bool CanGoPrevious { get; init; }

    /// <summary>Playback position as last reported by the player.</summary>
    public TimeSpan Position { get; init; }

    /// <summary>Track length, or zero when the player does not publish a timeline.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// When <see cref="Position"/> was reported. Players report it only now and then, so the
    /// live position is estimated from this. <c>default</c> means "unknown".
    /// </summary>
    public DateTimeOffset PositionUpdatedAt { get; init; }

    public double PlaybackRate { get; init; } = 1.0;

    /// <summary>Encoded album art (PNG/JPEG bytes), or null when there is none.</summary>
    public byte[]? Artwork { get; init; }
}
