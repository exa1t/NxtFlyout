using CommunityToolkit.Mvvm.Input;
using NxtHUD.Core.Media;
using NxtHUD.Core.Settings;

namespace NxtHUD.Core.ViewModels;

/// <summary>
/// Backs the media flyout: turns the current <see cref="MediaSnapshot"/> into display
/// properties and decides when the flyout is visible.
/// </summary>
/// <remarks>
/// Visibility rules (from <see cref="FlyoutViewModelBase"/>): a media key shows the flyout if
/// it is enabled, something is playing and no fullscreen app is in the way, and restarts the
/// auto-hide countdown, which is suspended while the pointer is over the flyout.
/// </remarks>
public sealed class MediaFlyoutViewModel : FlyoutViewModelBase
{
    private readonly IMediaService _media;
    private readonly Func<DateTimeOffset> _clock;

    private MediaSnapshot? _snapshot;

    private bool _isCompact;
    private bool _hasMedia;
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private string _albumTitle = string.Empty;
    private string _sourceAppName = string.Empty;
    private string _sourceAppId = string.Empty;
    private byte[]? _artwork;
    private bool _isPlaying;
    private bool _canTogglePlayPause;
    private bool _canGoNext;
    private bool _canGoPrevious;
    private bool _hasTimeline;
    private bool _showTimeline;
    private bool _showSourceApp;
    private double _durationSeconds;
    private double _positionSeconds;
    private string _durationText = string.Empty;
    private string _positionText = string.Empty;

    /// <param name="media">Source of the current media session.</param>
    /// <param name="settings">App settings (enabled, timeout, compact layout, fullscreen rule).</param>
    /// <param name="hideTimer">Timer that drives auto-hide.</param>
    /// <param name="clock">Returns the current time; defaults to the system clock.</param>
    /// <param name="isFullscreenAppActive">
    /// Returns true while a fullscreen game or app is in the foreground; defaults to "never".
    /// </param>
    public MediaFlyoutViewModel(
        IMediaService media,
        ISettingsService settings,
        IDelayTimer hideTimer,
        Func<DateTimeOffset>? clock = null,
        Func<bool>? isFullscreenAppActive = null)
        : base(settings, hideTimer, isFullscreenAppActive)
    {
        ArgumentNullException.ThrowIfNull(media);

        _media = media;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        PlayPauseCommand = new AsyncRelayCommand(_media.TogglePlayPauseAsync);
        NextCommand = new AsyncRelayCommand(_media.SkipNextAsync);
        PreviousCommand = new AsyncRelayCommand(_media.SkipPreviousAsync);

        _isCompact = Settings.Current.CompactLayout;

        _media.Changed += OnMediaChanged;

        ApplySnapshot();
    }

    public IAsyncRelayCommand PlayPauseCommand { get; }

    public IAsyncRelayCommand NextCommand { get; }

    public IAsyncRelayCommand PreviousCommand { get; }

    // ----- Layout -----

    public bool IsCompact
    {
        get => _isCompact;
        private set => SetProperty(ref _isCompact, value);
    }

    // ----- What is playing -----

    /// <summary>True when there is a session worth showing.</summary>
    public bool HasMedia
    {
        get => _hasMedia;
        private set => SetProperty(ref _hasMedia, value);
    }

    /// <summary>The track title, or the player's name when the player gives no title.</summary>
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

    public string AlbumTitle
    {
        get => _albumTitle;
        private set => SetProperty(ref _albumTitle, value);
    }

    public string SourceAppName
    {
        get => _sourceAppName;
        private set => SetProperty(ref _sourceAppName, value);
    }

    /// <summary>The player's app id, used by the view to look up the player's icon.</summary>
    public string SourceAppId
    {
        get => _sourceAppId;
        private set => SetProperty(ref _sourceAppId, value);
    }

    /// <summary>Encoded album art, or null. Only changes when the art itself changes.</summary>
    public byte[]? Artwork
    {
        get => _artwork;
        private set => SetProperty(ref _artwork, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public bool CanTogglePlayPause
    {
        get => _canTogglePlayPause;
        private set => SetProperty(ref _canTogglePlayPause, value);
    }

    public bool CanGoNext
    {
        get => _canGoNext;
        private set => SetProperty(ref _canGoNext, value);
    }

    public bool CanGoPrevious
    {
        get => _canGoPrevious;
        private set => SetProperty(ref _canGoPrevious, value);
    }

    // ----- Timeline -----

    /// <summary>True when the player publishes a track length.</summary>
    public bool HasTimeline
    {
        get => _hasTimeline;
        private set => SetProperty(ref _hasTimeline, value);
    }

    /// <summary>The progress row is shown when there is a timeline and the layout is not compact.</summary>
    public bool ShowTimeline
    {
        get => _showTimeline;
        private set => SetProperty(ref _showTimeline, value);
    }

    /// <summary>The player's name is shown when it is known and the layout is not compact.</summary>
    public bool ShowSourceApp
    {
        get => _showSourceApp;
        private set => SetProperty(ref _showSourceApp, value);
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        private set => SetProperty(ref _durationSeconds, value);
    }

    /// <summary>The estimated live position; call <see cref="RefreshPosition"/> to advance it.</summary>
    public double PositionSeconds
    {
        get => _positionSeconds;
        private set => SetProperty(ref _positionSeconds, value);
    }

    public string DurationText
    {
        get => _durationText;
        private set => SetProperty(ref _durationText, value);
    }

    public string PositionText
    {
        get => _positionText;
        private set => SetProperty(ref _positionText, value);
    }

    // ----- Inputs -----

    /// <summary>Call when the user presses a media key.</summary>
    public void OnMediaKeyPressed(MediaKey key) => TryShow();

    /// <summary>
    /// Shows the flyout if it is hidden and hides it if it is showing. Used for an explicit
    /// request (a click on the taskbar widget), so it works even when key presses do not show it.
    /// </summary>
    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else if (HasMedia)
        {
            Show();
        }
    }

    /// <summary>
    /// Re-estimates the live playback position. Players report their position only
    /// occasionally, so while playing it is extrapolated from the last report.
    /// The window calls this on a slow tick while the flyout is visible.
    /// </summary>
    public void RefreshPosition()
    {
        var snapshot = _snapshot;
        if (snapshot is null || !HasTimeline)
        {
            PositionSeconds = 0;
            PositionText = string.Empty;
            return;
        }

        var position = snapshot.Position;

        if (snapshot.PlaybackState == MediaPlaybackState.Playing && snapshot.PositionUpdatedAt != default)
        {
            var elapsed = _clock() - snapshot.PositionUpdatedAt;
            if (elapsed > TimeSpan.Zero)
            {
                position += elapsed * snapshot.PlaybackRate;
            }
        }

        if (position < TimeSpan.Zero)
        {
            position = TimeSpan.Zero;
        }
        else if (position > snapshot.Duration)
        {
            position = snapshot.Duration;
        }

        PositionSeconds = position.TotalSeconds;
        PositionText = MediaFormat.FormatTime(position);
    }

    protected override bool HasContent => HasMedia;

    protected override bool IsEnabled(AppSettings settings) => settings.MediaFlyoutEnabled;

    protected override void OnShowing() => RefreshPosition();

    protected override void OnSettingsChanged(SettingsChangedEventArgs e)
    {
        IsCompact = e.Current.CompactLayout;
        UpdateLayoutFlags();
    }

    protected override void OnDisposed() => _media.Changed -= OnMediaChanged;

    private void OnMediaChanged(object? sender, EventArgs e) => ApplySnapshot();

    private void ApplySnapshot()
    {
        var snapshot = _media.Current;
        _snapshot = snapshot;

        if (snapshot is null || snapshot.PlaybackState == MediaPlaybackState.Closed)
        {
            HasMedia = false;
            Title = string.Empty;
            Artist = string.Empty;
            AlbumTitle = string.Empty;
            SourceAppName = string.Empty;
            SourceAppId = string.Empty;
            Artwork = null;
            IsPlaying = false;
            CanTogglePlayPause = false;
            CanGoNext = false;
            CanGoPrevious = false;
            HasTimeline = false;
            DurationSeconds = 0;
            DurationText = string.Empty;
        }
        else
        {
            var appName = MediaFormat.FriendlyAppName(snapshot.SourceAppId);

            HasMedia = true;
            Title = string.IsNullOrWhiteSpace(snapshot.Title) ? appName : snapshot.Title;
            Artist = snapshot.Artist;
            AlbumTitle = snapshot.AlbumTitle;
            SourceAppName = appName;
            SourceAppId = snapshot.SourceAppId;
            Artwork = snapshot.Artwork;
            IsPlaying = snapshot.PlaybackState == MediaPlaybackState.Playing;
            CanTogglePlayPause = snapshot.CanTogglePlayPause;
            CanGoNext = snapshot.CanGoNext;
            CanGoPrevious = snapshot.CanGoPrevious;

            var hasTimeline = snapshot.Duration > TimeSpan.Zero;
            HasTimeline = hasTimeline;
            DurationSeconds = hasTimeline ? snapshot.Duration.TotalSeconds : 0;
            DurationText = hasTimeline ? MediaFormat.FormatTime(snapshot.Duration) : string.Empty;
        }

        UpdateLayoutFlags();
        RefreshPosition();

        if (!HasMedia && IsVisible)
        {
            // The player went away while the flyout was up.
            Hide();
        }
    }

    private void UpdateLayoutFlags()
    {
        ShowTimeline = HasTimeline && !IsCompact;
        ShowSourceApp = SourceAppName.Length > 0 && !IsCompact;
    }
}
