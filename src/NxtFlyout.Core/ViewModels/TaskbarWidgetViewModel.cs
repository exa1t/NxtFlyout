using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NxtFlyout.Core.Audio;
using NxtFlyout.Core.Media;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Core.ViewModels;

/// <summary>
/// Backs the compact now-playing widget on the taskbar: cover, title, artist, previous / play-pause /
/// next buttons, and a click that opens the media flyout.
/// </summary>
public sealed class TaskbarWidgetViewModel : ObservableObject, IDisposable
{
    private readonly IMediaService _media;
    private readonly ISettingsService _settings;
    private readonly IAudioSpectrumSource? _spectrum;

    private bool _isVisible;
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private byte[]? _artwork;
    private bool _isPlaying;
    private bool _canTogglePlayPause;
    private bool _canGoNext;
    private bool _canGoPrevious;
    private bool _showVisualizer;
    private int _visualizerBarCount = AppSettings.DefaultVisualizerBars;
    private VisualizerColorMode _visualizerColorMode = VisualizerColorMode.Accent;
    private string _visualizerCustomColor = AppSettings.DefaultVisualizerColor;
    private VisualizerAlignment _visualizerAlignment = VisualizerAlignment.Center;
    private float[] _visualizerLevels = Array.Empty<float>();
    private bool _spectrumActive;

    /// <param name="media">Source of the current media session.</param>
    /// <param name="settings">App settings.</param>
    /// <param name="openFlyout">Called when the widget is clicked.</param>
    /// <param name="spectrum">Audio levels for the visualizer, or null to never show it.</param>
    public TaskbarWidgetViewModel(
        IMediaService media,
        ISettingsService settings,
        Action openFlyout,
        IAudioSpectrumSource? spectrum = null)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(openFlyout);

        _media = media;
        _settings = settings;
        _spectrum = spectrum;

        PlayPauseCommand = new AsyncRelayCommand(_media.TogglePlayPauseAsync);
        NextCommand = new AsyncRelayCommand(_media.SkipNextAsync);
        PreviousCommand = new AsyncRelayCommand(_media.SkipPreviousAsync);
        OpenFlyoutCommand = new RelayCommand(openFlyout);

        _media.Changed += OnMediaChanged;
        _settings.Changed += OnSettingsChanged;

        if (_spectrum is not null)
        {
            _spectrum.LevelsUpdated += OnLevelsUpdated;
        }

        Refresh();
    }

    public IAsyncRelayCommand PlayPauseCommand { get; }

    public IAsyncRelayCommand NextCommand { get; }

    public IAsyncRelayCommand PreviousCommand { get; }

    public IRelayCommand OpenFlyoutCommand { get; }

    /// <summary>The widget is shown while it is enabled and something is playing or paused.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
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

    /// <summary>The visualizer is drawn while it is enabled and an audio source is available.</summary>
    public bool ShowVisualizer
    {
        get => _showVisualizer;
        private set => SetProperty(ref _showVisualizer, value);
    }

    public int VisualizerBarCount
    {
        get => _visualizerBarCount;
        private set => SetProperty(ref _visualizerBarCount, value);
    }

    public VisualizerColorMode VisualizerColorMode
    {
        get => _visualizerColorMode;
        private set => SetProperty(ref _visualizerColorMode, value);
    }

    public string VisualizerCustomColor
    {
        get => _visualizerCustomColor;
        private set => SetProperty(ref _visualizerCustomColor, value);
    }

    public VisualizerAlignment VisualizerAlignment
    {
        get => _visualizerAlignment;
        private set => SetProperty(ref _visualizerAlignment, value);
    }

    /// <summary>Current bar levels (0..1); all zero while nothing is playing.</summary>
    public float[] VisualizerLevels
    {
        get => _visualizerLevels;
        private set => SetProperty(ref _visualizerLevels, value);
    }

    public void Dispose()
    {
        _media.Changed -= OnMediaChanged;
        _settings.Changed -= OnSettingsChanged;

        if (_spectrum is not null)
        {
            _spectrum.LevelsUpdated -= OnLevelsUpdated;
            if (_spectrumActive)
            {
                _spectrumActive = false;
                _spectrum.SetActive(false, VisualizerBarCount);
            }
        }
    }

    private void OnLevelsUpdated(object? sender, float[] levels)
    {
        if (_spectrumActive)
        {
            VisualizerLevels = levels;
        }
    }

    private void OnMediaChanged(object? sender, EventArgs e) => Refresh();

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var snapshot = _media.Current;
        var hasMedia = snapshot is not null && snapshot.PlaybackState != MediaPlaybackState.Closed;

        if (snapshot is null || !hasMedia)
        {
            Title = string.Empty;
            Artist = string.Empty;
            Artwork = null;
            IsPlaying = false;
            CanTogglePlayPause = false;
            CanGoNext = false;
            CanGoPrevious = false;
        }
        else
        {
            Title = string.IsNullOrWhiteSpace(snapshot.Title)
                ? MediaFormat.FriendlyAppName(snapshot.SourceAppId)
                : snapshot.Title;
            Artist = snapshot.Artist;
            Artwork = snapshot.Artwork;
            IsPlaying = snapshot.PlaybackState == MediaPlaybackState.Playing;
            CanTogglePlayPause = snapshot.CanTogglePlayPause;
            CanGoNext = snapshot.CanGoNext;
            CanGoPrevious = snapshot.CanGoPrevious;
        }

        IsVisible = hasMedia && _settings.Current.TaskbarWidgetEnabled;

        var settings = _settings.Current;
        VisualizerBarCount = settings.VisualizerBarCount;
        VisualizerColorMode = settings.VisualizerColorMode;
        VisualizerCustomColor = settings.VisualizerCustomColor;
        VisualizerAlignment = settings.VisualizerAlignment;
        ShowVisualizer = _spectrum is not null && settings.TaskbarVisualizerEnabled;

        UpdateSpectrumActivity();
    }

    /// <summary>Only listens to the audio device while the bars are on screen and something plays.</summary>
    private void UpdateSpectrumActivity()
    {
        if (_spectrum is null)
        {
            return;
        }

        var wanted = IsVisible && ShowVisualizer && IsPlaying;

        if (wanted)
        {
            // Also re-sent when only the bar count changed.
            _spectrumActive = true;
            _spectrum.SetActive(true, VisualizerBarCount);
        }
        else if (_spectrumActive)
        {
            _spectrumActive = false;
            _spectrum.SetActive(false, VisualizerBarCount);
        }

        if (!wanted && (VisualizerLevels.Length != VisualizerBarCount || VisualizerLevels.Any(level => level > 0)))
        {
            VisualizerLevels = new float[VisualizerBarCount];
        }
    }
}
