using NxtFlyout.Core.Media;
using NxtFlyout.Core.Settings;
using NxtFlyout.Core.ViewModels;

namespace NxtFlyout.Tests;

public class MediaFlyoutViewModelTests
{
    private static readonly DateTimeOffset StartOfTest = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeMediaService _media = new();
    private readonly InMemorySettingsService _settings = new();
    private readonly FakeDelayTimer _timer = new();

    private DateTimeOffset _now = StartOfTest;
    private bool _fullscreen;

    private MediaFlyoutViewModel CreateViewModel() =>
        new(_media, _settings, _timer, () => _now, () => _fullscreen);

    private static MediaSnapshot Song() => new()
    {
        SourceAppId = "Spotify.exe",
        Title = "Night Drive",
        Artist = "The Examples",
        AlbumTitle = "Test Pressing",
        PlaybackState = MediaPlaybackState.Playing,
        CanTogglePlayPause = true,
        CanGoNext = true,
        CanGoPrevious = true,
        Position = TimeSpan.FromSeconds(30),
        Duration = TimeSpan.FromSeconds(200),
        PositionUpdatedAt = StartOfTest,
        Artwork = new byte[] { 1, 2, 3 },
    };

    private static List<string?> RecordChanges(MediaFlyoutViewModel viewModel)
    {
        var names = new List<string?>();
        viewModel.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    // ----- What is playing -----

    [Fact]
    public void WithNoSession_ThereIsNothingToShow()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasMedia);
        Assert.Equal(string.Empty, viewModel.Title);
        Assert.Equal(string.Empty, viewModel.Artist);
        Assert.Null(viewModel.Artwork);
        Assert.False(viewModel.IsPlaying);
        Assert.False(viewModel.CanTogglePlayPause);
        Assert.False(viewModel.CanGoNext);
        Assert.False(viewModel.CanGoPrevious);
        Assert.False(viewModel.HasTimeline);
        Assert.False(viewModel.ShowTimeline);
        Assert.False(viewModel.ShowSourceApp);
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void SessionAlreadyPlayingAtStartup_IsPickedUp()
    {
        _media.Set(Song());

        var viewModel = CreateViewModel();

        Assert.True(viewModel.HasMedia);
        Assert.Equal("Night Drive", viewModel.Title);
    }

    [Fact]
    public void Snapshot_IsMappedToDisplayProperties()
    {
        var viewModel = CreateViewModel();
        var song = Song();

        _media.Set(song);

        Assert.True(viewModel.HasMedia);
        Assert.Equal("Night Drive", viewModel.Title);
        Assert.Equal("The Examples", viewModel.Artist);
        Assert.Equal("Test Pressing", viewModel.AlbumTitle);
        Assert.Equal("Spotify", viewModel.SourceAppName);
        Assert.Same(song.Artwork, viewModel.Artwork);
        Assert.True(viewModel.IsPlaying);
        Assert.True(viewModel.CanTogglePlayPause);
        Assert.True(viewModel.CanGoNext);
        Assert.True(viewModel.CanGoPrevious);
        Assert.True(viewModel.HasTimeline);
        Assert.True(viewModel.ShowTimeline);
        Assert.True(viewModel.ShowSourceApp);
        Assert.Equal(200, viewModel.DurationSeconds);
        Assert.Equal("3:20", viewModel.DurationText);
        Assert.Equal(30, viewModel.PositionSeconds);
        Assert.Equal("0:30", viewModel.PositionText);
    }

    [Fact]
    public void Paused_IsNotPlaying()
    {
        var viewModel = CreateViewModel();

        _media.Set(Song() with { PlaybackState = MediaPlaybackState.Paused });

        Assert.True(viewModel.HasMedia);
        Assert.False(viewModel.IsPlaying);
    }

    [Fact]
    public void MissingTitle_FallsBackToThePlayerName()
    {
        var viewModel = CreateViewModel();

        _media.Set(Song() with { Title = "  " });

        Assert.Equal("Spotify", viewModel.Title);
    }

    [Fact]
    public void ClosedSession_CountsAsNoMedia()
    {
        var viewModel = CreateViewModel();

        _media.Set(Song() with { PlaybackState = MediaPlaybackState.Closed });

        Assert.False(viewModel.HasMedia);
        Assert.Equal(string.Empty, viewModel.Title);
    }

    [Fact]
    public void SessionGoingAway_ClearsEverything()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());

        _media.Set(null);

        Assert.False(viewModel.HasMedia);
        Assert.Equal(string.Empty, viewModel.Title);
        Assert.Null(viewModel.Artwork);
        Assert.False(viewModel.HasTimeline);
        Assert.Equal(0, viewModel.PositionSeconds);
        Assert.Equal(string.Empty, viewModel.PositionText);
    }

    [Fact]
    public void PlaybackChange_OnlyAnnouncesWhatChanged()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        var changes = RecordChanges(viewModel);

        _media.Set(Song() with { PlaybackState = MediaPlaybackState.Paused, Artwork = viewModel.Artwork });

        Assert.Equal(new[] { nameof(MediaFlyoutViewModel.IsPlaying) }, changes);
    }

    [Fact]
    public void SessionWithoutDuration_HasNoTimeline()
    {
        var viewModel = CreateViewModel();

        _media.Set(Song() with { Duration = TimeSpan.Zero });

        Assert.False(viewModel.HasTimeline);
        Assert.False(viewModel.ShowTimeline);
        Assert.Equal(0, viewModel.DurationSeconds);
        Assert.Equal(string.Empty, viewModel.DurationText);
        Assert.Equal(string.Empty, viewModel.PositionText);
    }

    // ----- Live position -----

    [Fact]
    public void RefreshPosition_WhilePlaying_AdvancesWithTheClock()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());

        _now = StartOfTest + TimeSpan.FromSeconds(12);
        viewModel.RefreshPosition();

        Assert.Equal(42, viewModel.PositionSeconds);
        Assert.Equal("0:42", viewModel.PositionText);
    }

    [Fact]
    public void RefreshPosition_HonoursThePlaybackRate()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song() with { PlaybackRate = 2.0 });

        _now = StartOfTest + TimeSpan.FromSeconds(10);
        viewModel.RefreshPosition();

        Assert.Equal(50, viewModel.PositionSeconds);
    }

    [Fact]
    public void RefreshPosition_WhilePaused_StaysPut()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song() with { PlaybackState = MediaPlaybackState.Paused });

        _now = StartOfTest + TimeSpan.FromSeconds(60);
        viewModel.RefreshPosition();

        Assert.Equal(30, viewModel.PositionSeconds);
    }

    [Fact]
    public void RefreshPosition_NeverRunsPastTheEnd()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());

        _now = StartOfTest + TimeSpan.FromHours(1);
        viewModel.RefreshPosition();

        Assert.Equal(200, viewModel.PositionSeconds);
        Assert.Equal("3:20", viewModel.PositionText);
    }

    [Fact]
    public void RefreshPosition_WithUnknownReportTime_UsesTheReportedPosition()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song() with { PositionUpdatedAt = default });

        _now = StartOfTest + TimeSpan.FromSeconds(45);
        viewModel.RefreshPosition();

        Assert.Equal(30, viewModel.PositionSeconds);
    }

    [Fact]
    public void RefreshPosition_ReportFromTheFuture_DoesNotRewind()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song() with { PositionUpdatedAt = StartOfTest + TimeSpan.FromSeconds(5) });

        viewModel.RefreshPosition();

        Assert.Equal(30, viewModel.PositionSeconds);
    }

    // ----- Showing on a media key -----

    [Theory]
    [InlineData(MediaKey.PlayPause)]
    [InlineData(MediaKey.Next)]
    [InlineData(MediaKey.Previous)]
    [InlineData(MediaKey.Stop)]
    public void MediaKey_ShowsTheFlyoutAndStartsTheCountdown(MediaKey key)
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        _settings.Update(s => s.FlyoutTimeoutMs = 4500);

        viewModel.OnMediaKeyPressed(key);

        Assert.True(viewModel.IsVisible);
        Assert.True(_timer.IsRunning);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), _timer.LastDelay);
    }

    [Fact]
    public void MediaKey_WhenTheFlyoutIsDisabled_DoesNothing()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        _settings.Update(s => s.MediaFlyoutEnabled = false);

        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        Assert.False(viewModel.IsVisible);
        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void MediaKey_WithNothingPlaying_DoesNothing()
    {
        var viewModel = CreateViewModel();

        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        Assert.False(viewModel.IsVisible);
        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void MediaKey_DuringAFullscreenApp_IsSuppressed()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        _fullscreen = true;

        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void MediaKey_DuringAFullscreenApp_ShowsWhenThatRuleIsOff()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        _fullscreen = true;
        _settings.Update(s => s.DisableInFullscreen = false);

        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public void MediaKey_WhileAlreadyVisible_RestartsTheCountdown()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.Next);

        viewModel.OnMediaKeyPressed(MediaKey.Next);

        Assert.True(viewModel.IsVisible);
        Assert.Equal(2, _timer.StartCount);
    }

    [Fact]
    public void MediaKey_RefreshesThePositionBeforeShowing()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        _now = StartOfTest + TimeSpan.FromSeconds(20);

        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        Assert.Equal(50, viewModel.PositionSeconds);
    }

    // ----- Hiding -----

    [Fact]
    public void WhenTheCountdownEnds_TheFlyoutHides()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        _timer.Fire();

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void Hovering_KeepsTheFlyoutOpen()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        viewModel.OnPointerEntered();

        Assert.False(_timer.IsRunning);
        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public void LeavingAfterHovering_StartsTheCountdownAgain()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);
        viewModel.OnPointerEntered();

        viewModel.OnPointerExited();
        Assert.True(_timer.IsRunning);
        Assert.True(viewModel.IsVisible);

        _timer.Fire();
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void MediaKey_WhileHovering_DoesNotStartTheCountdown()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);
        viewModel.OnPointerEntered();

        viewModel.OnMediaKeyPressed(MediaKey.Next);

        Assert.True(viewModel.IsVisible);
        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void PointerLeavingAHiddenFlyout_DoesNotStartTheCountdown()
    {
        var viewModel = CreateViewModel();

        viewModel.OnPointerEntered();
        viewModel.OnPointerExited();

        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void Hide_HidesAtOnceAndStopsTheCountdown()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        viewModel.Hide();

        Assert.False(viewModel.IsVisible);
        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void PlayerClosingWhileVisible_HidesTheFlyout()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        _media.Set(null);

        Assert.False(viewModel.IsVisible);
        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void TrackChangeWhileVisible_KeepsTheFlyoutOpenWithTheNewTrack()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.Next);

        _media.Set(Song() with { Title = "Second Song" });

        Assert.True(viewModel.IsVisible);
        Assert.Equal("Second Song", viewModel.Title);
    }

    [Fact]
    public void DisablingTheFlyoutWhileVisible_HidesIt()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        _settings.Update(s => s.MediaFlyoutEnabled = false);

        Assert.False(viewModel.IsVisible);
        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void IsVisible_IsAnnouncedOncePerChange()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        var changes = RecordChanges(viewModel);

        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);
        _timer.Fire();

        Assert.Equal(2, changes.Count(name => name == nameof(MediaFlyoutViewModel.IsVisible)));
    }

    // ----- Toggle (a click on the taskbar widget) -----

    [Fact]
    public void Toggle_ShowsAHiddenFlyoutAndHidesAShownOne()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());

        viewModel.Toggle();
        Assert.True(viewModel.IsVisible);
        Assert.True(_timer.IsRunning);

        viewModel.Toggle();
        Assert.False(viewModel.IsVisible);
        Assert.False(_timer.IsRunning);
    }

    [Fact]
    public void Toggle_IsAnExplicitRequest_SoItIgnoresTheKeyPressRules()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        _settings.Update(s => s.MediaFlyoutEnabled = false);
        _fullscreen = true;

        viewModel.Toggle();

        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public void Toggle_WithNothingPlaying_DoesNothing()
    {
        var viewModel = CreateViewModel();

        viewModel.Toggle();

        Assert.False(viewModel.IsVisible);
    }

    // ----- Compact layout -----

    [Fact]
    public void CompactLayout_HidesTheTimelineAndPlayerName()
    {
        _settings.Update(s => s.CompactLayout = true);
        var viewModel = CreateViewModel();

        _media.Set(Song());

        Assert.True(viewModel.IsCompact);
        Assert.True(viewModel.HasTimeline);
        Assert.False(viewModel.ShowTimeline);
        Assert.False(viewModel.ShowSourceApp);
    }

    [Fact]
    public void CompactLayout_FollowsTheSettingLive()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());

        _settings.Update(s => s.CompactLayout = true);
        Assert.True(viewModel.IsCompact);
        Assert.False(viewModel.ShowTimeline);

        _settings.Update(s => s.CompactLayout = false);
        Assert.False(viewModel.IsCompact);
        Assert.True(viewModel.ShowTimeline);
        Assert.True(viewModel.ShowSourceApp);
    }

    // ----- Commands and lifetime -----

    [Fact]
    public void Commands_ForwardToTheMediaService()
    {
        var viewModel = CreateViewModel();

        viewModel.PlayPauseCommand.Execute(null);
        viewModel.NextCommand.Execute(null);
        viewModel.NextCommand.Execute(null);
        viewModel.PreviousCommand.Execute(null);

        Assert.Equal(1, _media.TogglePlayPauseCalls);
        Assert.Equal(2, _media.SkipNextCalls);
        Assert.Equal(1, _media.SkipPreviousCalls);
    }

    [Fact]
    public void Dispose_StopsReactingToEverything()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());
        viewModel.OnMediaKeyPressed(MediaKey.PlayPause);

        viewModel.Dispose();
        _media.Set(Song() with { Title = "Ignored" });
        _settings.Update(s => s.CompactLayout = true);

        Assert.False(_timer.IsRunning);
        Assert.Equal("Night Drive", viewModel.Title);
        Assert.False(viewModel.IsCompact);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new MediaFlyoutViewModel(null!, _settings, _timer));
        Assert.Throws<ArgumentNullException>(() => new MediaFlyoutViewModel(_media, null!, _timer));
        Assert.Throws<ArgumentNullException>(() => new MediaFlyoutViewModel(_media, _settings, null!));
    }
}
