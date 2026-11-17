using NxtFlyout.Core.Media;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Core.ViewModels;

/// <summary>
/// Backs the "Up Next" flyout, which briefly announces a track when the song changes on its own.
/// </summary>
/// <remarks>
/// Windows does not tell apps what a player has queued, so there is no way to know the next
/// track before it starts. The flyout therefore appears at the moment the track changes.
/// It stays quiet for the first track seen from a player, while playback is paused, and
/// when another flyout (the media flyout, after a "next" key press) is already showing it.
/// </remarks>
public sealed class UpNextFlyoutViewModel : FlyoutViewModelBase
{
    private readonly IMediaService _media;
    private readonly Func<bool> _isSuppressed;

    private string? _lastTrackKey;
    private string _lastAppId = string.Empty;

    private string _title = string.Empty;
    private string _artist = string.Empty;
    private byte[]? _artwork;

    /// <param name="media">Source of the current media session.</param>
    /// <param name="settings">App settings.</param>
    /// <param name="hideTimer">Timer that drives auto-hide.</param>
    /// <param name="isSuppressed">Returns true when the announcement would be redundant; null means "never".</param>
    /// <param name="isFullscreenAppActive">Returns true while a fullscreen app is in the foreground.</param>
    public UpNextFlyoutViewModel(
        IMediaService media,
        ISettingsService settings,
        IDelayTimer hideTimer,
        Func<bool>? isSuppressed = null,
        Func<bool>? isFullscreenAppActive = null)
        : base(settings, hideTimer, isFullscreenAppActive)
    {
        ArgumentNullException.ThrowIfNull(media);

        _media = media;
        _isSuppressed = isSuppressed ?? (() => false);

        _media.Changed += OnMediaChanged;

        // Remember what is playing at start-up without announcing it.
        Track(_media.Current);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Artist
    {
        get => _artist;
        private set => SetProperty(ref _artist, value);
    }

    public byte[]? Artwork
    {
        get => _artwork;
        private set => SetProperty(ref _artwork, value);
    }

    protected override bool IsEnabled(AppSettings settings) => settings.UpNextFlyoutEnabled;

    protected override void OnDisposed() => _media.Changed -= OnMediaChanged;

    private void OnMediaChanged(object? sender, EventArgs e)
    {
        var snapshot = _media.Current;

        if (Track(snapshot) && snapshot is not null)
        {
            Title = snapshot.Title;
            Artist = snapshot.Artist;
            Artwork = snapshot.Artwork;
            TryShow();
        }
        else if (IsVisible && snapshot is not null && BuildKey(snapshot) == _lastTrackKey)
        {
            // Album art often arrives a moment after the title; keep the open flyout current.
            Artwork = snapshot.Artwork;
        }
    }

    /// <summary>
    /// Records the snapshot as the latest track and returns whether it is a track change
    /// worth announcing.
    /// </summary>
    private bool Track(MediaSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.PlaybackState == MediaPlaybackState.Closed)
        {
            _lastTrackKey = null;
            _lastAppId = string.Empty;

            if (IsVisible)
            {
                Hide();
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(snapshot.Title))
        {
            // The player has not published the track yet.
            return false;
        }

        var key = BuildKey(snapshot);
        var previousKey = _lastTrackKey;
        var previousAppId = _lastAppId;

        _lastTrackKey = key;
        _lastAppId = snapshot.SourceAppId;

        if (key == previousKey)
        {
            return false;
        }

        // The first track seen from a player is "something started playing", not a track change.
        if (previousKey is null || previousAppId != snapshot.SourceAppId)
        {
            return false;
        }

        if (snapshot.PlaybackState is MediaPlaybackState.Paused or MediaPlaybackState.Stopped)
        {
            return false;
        }

        return !_isSuppressed();
    }

    private static string BuildKey(MediaSnapshot snapshot)
    {
        return $"{snapshot.SourceAppId}\n{snapshot.Title}\n{snapshot.Artist}";
    }
}
