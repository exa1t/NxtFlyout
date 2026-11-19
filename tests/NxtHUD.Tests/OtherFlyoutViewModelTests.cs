using NxtHUD.Core.Audio;
using NxtHUD.Core.Flyouts;
using NxtHUD.Core.Input;
using NxtHUD.Core.Localization;
using NxtHUD.Core.Media;
using NxtHUD.Core.Settings;
using NxtHUD.Core.ViewModels;

namespace NxtHUD.Tests;

/// <summary>An <see cref="IVolumeService"/> with a volume the test sets by hand.</summary>
internal sealed class FakeVolumeService : IVolumeService
{
    public VolumeState? State { get; set; } = new VolumeState(0.5, false);

    public VolumeState? GetState() => State;

    public bool StepUp() => true;

    public bool StepDown() => true;

    public bool ToggleMute() => true;
}

public class VolumeFlyoutViewModelTests
{
    private readonly FakeVolumeService _volume = new();
    private readonly InMemorySettingsService _settings = new();
    private readonly FakeDelayTimer _timer = new();
    private bool _fullscreen;

    public VolumeFlyoutViewModelTests()
    {
        // The volume flyout is opt-in.
        _settings.Update(s => s.VolumeFlyoutEnabled = true);
    }

    private VolumeFlyoutViewModel CreateViewModel() => new(_volume, _settings, _timer, () => _fullscreen);

    [Fact]
    public void VolumeKey_ShowsTheCurrentLevel()
    {
        var viewModel = CreateViewModel();
        _volume.State = new VolumeState(0.42, false);

        viewModel.OnVolumeKeyHandled();

        Assert.True(viewModel.IsVisible);
        Assert.True(viewModel.IsAvailable);
        Assert.Equal(42, viewModel.Percent);
        Assert.False(viewModel.IsMuted);
        Assert.True(_timer.IsRunning);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.004, 0)]
    [InlineData(0.006, 1)]
    [InlineData(0.5, 50)]
    [InlineData(0.999, 100)]
    [InlineData(1.0, 100)]
    [InlineData(1.7, 100)]
    [InlineData(-0.3, 0)]
    public void Refresh_RoundsTheLevelToAWholePercentage(double level, int expectedPercent)
    {
        var viewModel = CreateViewModel();
        _volume.State = new VolumeState(level, false);

        viewModel.Refresh();

        Assert.Equal(expectedPercent, viewModel.Percent);
    }

    [Theory]
    [InlineData(0, false, VolumeFlyoutViewModel.MutedGlyph)]
    [InlineData(1, false, VolumeFlyoutViewModel.LowGlyph)]
    [InlineData(33, false, VolumeFlyoutViewModel.LowGlyph)]
    [InlineData(34, false, VolumeFlyoutViewModel.MediumGlyph)]
    [InlineData(66, false, VolumeFlyoutViewModel.MediumGlyph)]
    [InlineData(67, false, VolumeFlyoutViewModel.HighGlyph)]
    [InlineData(100, false, VolumeFlyoutViewModel.HighGlyph)]
    [InlineData(80, true, VolumeFlyoutViewModel.MutedGlyph)]
    public void Glyph_MatchesLevelAndMute(int percent, bool muted, string expected)
    {
        Assert.Equal(expected, VolumeFlyoutViewModel.GetGlyph(percent, muted));
    }

    [Fact]
    public void Refresh_ReflectsMute()
    {
        var viewModel = CreateViewModel();
        _volume.State = new VolumeState(0.8, true);

        viewModel.Refresh();

        Assert.True(viewModel.IsMuted);
        Assert.Equal(80, viewModel.Percent);
        Assert.Equal(VolumeFlyoutViewModel.MutedGlyph, viewModel.Glyph);
    }

    [Fact]
    public void VolumeKey_WithNoPlaybackDevice_DoesNotShow()
    {
        var viewModel = CreateViewModel();
        _volume.State = null;

        viewModel.OnVolumeKeyHandled();

        Assert.False(viewModel.IsAvailable);
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void VolumeKey_WhenTheFlyoutIsDisabled_DoesNotShow()
    {
        _settings.Update(s => s.VolumeFlyoutEnabled = false);
        var viewModel = CreateViewModel();

        viewModel.OnVolumeKeyHandled();

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void VolumeKey_DuringAFullscreenApp_DoesNotShow()
    {
        var viewModel = CreateViewModel();
        _fullscreen = true;

        viewModel.OnVolumeKeyHandled();

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void RepeatedKeys_KeepTheFlyoutOpenAndFollowTheLevel()
    {
        var viewModel = CreateViewModel();
        viewModel.OnVolumeKeyHandled();

        _volume.State = new VolumeState(0.52, false);
        viewModel.OnVolumeKeyHandled();

        Assert.True(viewModel.IsVisible);
        Assert.Equal(52, viewModel.Percent);
        Assert.Equal(2, _timer.StartCount);
    }

    [Fact]
    public void TheCountdown_HidesTheFlyout()
    {
        var viewModel = CreateViewModel();
        viewModel.OnVolumeKeyHandled();

        _timer.Fire();

        Assert.False(viewModel.IsVisible);
    }
}

public class LockKeysFlyoutViewModelTests
{
    private readonly Dictionary<LockKey, bool> _states = new();
    private readonly InMemorySettingsService _settings = new();
    private readonly FakeDelayTimer _timer = new();
    private readonly Localizer _localizer = new();

    public LockKeysFlyoutViewModelTests()
    {
        _localizer.SetLanguage("en");
    }

    private LockKeysFlyoutViewModel CreateViewModel() =>
        new(key => _states.GetValueOrDefault(key), _settings, _timer, _localizer);

    [Theory]
    [InlineData(LockKey.CapsLock, true, "Caps Lock is on")]
    [InlineData(LockKey.CapsLock, false, "Caps Lock is off")]
    [InlineData(LockKey.NumLock, true, "Num Lock is on")]
    [InlineData(LockKey.NumLock, false, "Num Lock is off")]
    [InlineData(LockKey.ScrollLock, true, "Scroll Lock is on")]
    [InlineData(LockKey.ScrollLock, false, "Scroll Lock is off")]
    public void LockKey_ShowsTheKeyAndItsNewState(LockKey key, bool isOn, string expectedText)
    {
        var viewModel = CreateViewModel();
        _states[key] = isOn;

        viewModel.OnLockKeyPressed(key);

        Assert.True(viewModel.IsVisible);
        Assert.Equal(key, viewModel.Key);
        Assert.Equal(isOn, viewModel.IsOn);
        Assert.Equal(expectedText, viewModel.Text);
        Assert.Equal(isOn ? LockKeysFlyoutViewModel.OnGlyph : LockKeysFlyoutViewModel.OffGlyph, viewModel.Glyph);
    }

    [Fact]
    public void EveryKeyAndState_HasAText()
    {
        foreach (var key in Enum.GetValues<LockKey>())
        {
            Assert.True(_localizer.Contains(LockKeysFlyoutViewModel.GetTextKey(key, true)));
            Assert.True(_localizer.Contains(LockKeysFlyoutViewModel.GetTextKey(key, false)));
        }
    }

    [Fact]
    public void LockKey_WhenTheFlyoutIsDisabled_DoesNotShow()
    {
        _settings.Update(s => s.LockKeysFlyoutEnabled = false);
        var viewModel = CreateViewModel();

        viewModel.OnLockKeyPressed(LockKey.CapsLock);

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void TogglingAgain_UpdatesTheOpenFlyout()
    {
        var viewModel = CreateViewModel();
        _states[LockKey.CapsLock] = true;
        viewModel.OnLockKeyPressed(LockKey.CapsLock);

        _states[LockKey.CapsLock] = false;
        viewModel.OnLockKeyPressed(LockKey.CapsLock);

        Assert.True(viewModel.IsVisible);
        Assert.False(viewModel.IsOn);
        Assert.Equal("Caps Lock is off", viewModel.Text);
    }
}

public class UpNextFlyoutViewModelTests
{
    private readonly FakeMediaService _media = new();
    private readonly InMemorySettingsService _settings = new();
    private readonly FakeDelayTimer _timer = new();
    private bool _suppressed;

    public UpNextFlyoutViewModelTests()
    {
        // Up Next is opt-in.
        _settings.Update(s => s.UpNextFlyoutEnabled = true);
    }

    private UpNextFlyoutViewModel CreateViewModel() => new(_media, _settings, _timer, () => _suppressed);

    private static MediaSnapshot Track(string title, string artist = "The Examples") => new()
    {
        SourceAppId = "Spotify.exe",
        Title = title,
        Artist = artist,
        PlaybackState = MediaPlaybackState.Playing,
    };

    [Fact]
    public void TrackChange_AnnouncesTheNewTrack()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();
        var art = new byte[] { 1 };

        _media.Set(Track("Second", "Someone Else") with { Artwork = art });

        Assert.True(viewModel.IsVisible);
        Assert.Equal("Second", viewModel.Title);
        Assert.Equal("Someone Else", viewModel.Artist);
        Assert.Same(art, viewModel.Artwork);
        Assert.True(_timer.IsRunning);
    }

    [Fact]
    public void WhatIsPlayingAtStartup_IsNotAnnounced()
    {
        _media.Set(Track("First"));

        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void TheFirstTrackOfAPlayer_IsNotAnnounced()
    {
        var viewModel = CreateViewModel();

        _media.Set(Track("First"));

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void SwitchingToAnotherPlayer_IsNotATrackChange()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();

        _media.Set(Track("Video") with { SourceAppId = "chrome.exe" });

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void PlaybackOrTimelineUpdates_OfTheSameTrack_AreNotAnnounced()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();

        _media.Set(Track("First") with { Position = TimeSpan.FromSeconds(10) });
        _media.Set(Track("First") with { PlaybackState = MediaPlaybackState.Paused });

        Assert.False(viewModel.IsVisible);
    }

    [Theory]
    [InlineData(MediaPlaybackState.Paused)]
    [InlineData(MediaPlaybackState.Stopped)]
    public void TrackChange_WhileNotPlaying_IsNotAnnounced(MediaPlaybackState state)
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();

        _media.Set(Track("Second") with { PlaybackState = state });

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void TrackChange_WhenAnotherFlyoutAlreadyShowsIt_IsNotAnnounced()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();
        _suppressed = true;

        _media.Set(Track("Second"));

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void TrackChange_WhenDisabled_IsNotAnnounced()
    {
        _settings.Update(s => s.UpNextFlyoutEnabled = false);
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();

        _media.Set(Track("Second"));

        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void TrackWithoutATitleYet_IsIgnoredUntilItHasOne()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();

        _media.Set(Track(string.Empty));
        Assert.False(viewModel.IsVisible);

        _media.Set(Track("Second"));
        Assert.True(viewModel.IsVisible);
        Assert.Equal("Second", viewModel.Title);
    }

    [Fact]
    public void ArtworkArrivingLate_UpdatesTheOpenFlyout()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();
        _media.Set(Track("Second"));
        var art = new byte[] { 7 };

        _media.Set(Track("Second") with { Artwork = art });

        Assert.True(viewModel.IsVisible);
        Assert.Same(art, viewModel.Artwork);
        Assert.Equal(1, _timer.StartCount);
    }

    [Fact]
    public void PlayerClosing_HidesTheFlyout_AndItsNextTrackCountsAsAFirstTrack()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();
        _media.Set(Track("Second"));

        _media.Set(null);
        Assert.False(viewModel.IsVisible);

        _media.Set(Track("Third"));
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        _media.Set(Track("First"));
        var viewModel = CreateViewModel();

        viewModel.Dispose();
        _media.Set(Track("Second"));

        Assert.False(viewModel.IsVisible);
    }
}

public class TaskbarWidgetViewModelTests
{
    private readonly FakeMediaService _media = new();
    private readonly InMemorySettingsService _settings = new();
    private int _openRequests;

    public TaskbarWidgetViewModelTests()
    {
        _settings.Update(s => s.TaskbarWidgetEnabled = true);
    }

    private TaskbarWidgetViewModel CreateViewModel() => new(_media, _settings, () => _openRequests++);

    private static MediaSnapshot Song() => new()
    {
        SourceAppId = "Spotify.exe",
        Title = "Night Drive",
        Artist = "The Examples",
        PlaybackState = MediaPlaybackState.Playing,
        CanTogglePlayPause = true,
        Artwork = new byte[] { 1 },
    };

    [Fact]
    public void WithMedia_ShowsTheTrack()
    {
        var viewModel = CreateViewModel();
        var song = Song();

        _media.Set(song);

        Assert.True(viewModel.IsVisible);
        Assert.Equal("Night Drive", viewModel.Title);
        Assert.Equal("The Examples", viewModel.Artist);
        Assert.Same(song.Artwork, viewModel.Artwork);
        Assert.True(viewModel.IsPlaying);
        Assert.True(viewModel.CanTogglePlayPause);
    }

    [Fact]
    public void WithoutMedia_IsHidden()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsVisible);

        _media.Set(Song());
        _media.Set(null);

        Assert.False(viewModel.IsVisible);
        Assert.Equal(string.Empty, viewModel.Title);
        Assert.Null(viewModel.Artwork);
    }

    [Fact]
    public void StaysVisibleWhilePaused()
    {
        var viewModel = CreateViewModel();

        _media.Set(Song() with { PlaybackState = MediaPlaybackState.Paused });

        Assert.True(viewModel.IsVisible);
        Assert.False(viewModel.IsPlaying);
    }

    [Fact]
    public void FollowsTheEnabledSettingLive()
    {
        var viewModel = CreateViewModel();
        _media.Set(Song());

        _settings.Update(s => s.TaskbarWidgetEnabled = false);
        Assert.False(viewModel.IsVisible);

        _settings.Update(s => s.TaskbarWidgetEnabled = true);
        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public void MissingTitle_FallsBackToThePlayerName()
    {
        var viewModel = CreateViewModel();

        _media.Set(Song() with { Title = string.Empty });

        Assert.Equal("Spotify", viewModel.Title);
    }

    [Fact]
    public void Commands_OpenTheFlyoutAndTogglePlayback()
    {
        var viewModel = CreateViewModel();

        viewModel.OpenFlyoutCommand.Execute(null);
        viewModel.PlayPauseCommand.Execute(null);

        Assert.Equal(1, _openRequests);
        Assert.Equal(1, _media.TogglePlayPauseCalls);
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        var viewModel = CreateViewModel();

        viewModel.Dispose();
        _media.Set(Song());

        Assert.False(viewModel.IsVisible);
    }
}

public class FlyoutCoordinatorTests
{
    private readonly FakeMediaService _media = new();
    private readonly FakeVolumeService _volume = new();
    private readonly InMemorySettingsService _settings = new();

    public FlyoutCoordinatorTests()
    {
        _settings.Update(s => s.VolumeFlyoutEnabled = true);
        _media.Set(new MediaSnapshot { Title = "Song", PlaybackState = MediaPlaybackState.Playing });
    }

    [Fact]
    public void ShowingOneFlyout_HidesTheOthers()
    {
        var media = new MediaFlyoutViewModel(_media, _settings, new FakeDelayTimer());
        var volume = new VolumeFlyoutViewModel(_volume, _settings, new FakeDelayTimer());
        using var coordinator = new FlyoutCoordinator(media, volume);

        media.OnMediaKeyPressed(MediaKey.PlayPause);
        Assert.True(media.IsVisible);
        Assert.True(coordinator.IsAnyVisible);

        volume.OnVolumeKeyHandled();
        Assert.True(volume.IsVisible);
        Assert.False(media.IsVisible);

        media.OnMediaKeyPressed(MediaKey.PlayPause);
        Assert.True(media.IsVisible);
        Assert.False(volume.IsVisible);
    }

    [Fact]
    public void HidingAFlyout_LeavesTheOthersAlone()
    {
        var media = new MediaFlyoutViewModel(_media, _settings, new FakeDelayTimer());
        var volume = new VolumeFlyoutViewModel(_volume, _settings, new FakeDelayTimer());
        using var coordinator = new FlyoutCoordinator(media, volume);
        volume.OnVolumeKeyHandled();

        media.Hide();

        Assert.True(volume.IsVisible);
    }

    [Fact]
    public void AfterDispose_FlyoutsAreIndependent()
    {
        var media = new MediaFlyoutViewModel(_media, _settings, new FakeDelayTimer());
        var volume = new VolumeFlyoutViewModel(_volume, _settings, new FakeDelayTimer());
        var coordinator = new FlyoutCoordinator(media, volume);
        coordinator.Dispose();

        media.OnMediaKeyPressed(MediaKey.PlayPause);
        volume.OnVolumeKeyHandled();

        Assert.True(media.IsVisible);
        Assert.True(volume.IsVisible);
    }
}

public class SystemKeyMapTests
{
    [Theory]
    [InlineData(0xAF, VolumeKey.Up)]
    [InlineData(0xAE, VolumeKey.Down)]
    [InlineData(0xAD, VolumeKey.Mute)]
    public void VolumeFromVirtualKey_RecognisesVolumeKeys(int virtualKey, VolumeKey expected)
    {
        Assert.Equal(expected, SystemKeyMap.VolumeFromVirtualKey(virtualKey));
    }

    [Theory]
    [InlineData(0x14, LockKey.CapsLock)]
    [InlineData(0x90, LockKey.NumLock)]
    [InlineData(0x91, LockKey.ScrollLock)]
    public void LockFromVirtualKey_RecognisesLockKeys(int virtualKey, LockKey expected)
    {
        Assert.Equal(expected, SystemKeyMap.LockFromVirtualKey(virtualKey));
        Assert.Equal(virtualKey, SystemKeyMap.ToVirtualKey(expected));
    }

    [Theory]
    [InlineData(0x41)]
    [InlineData(0xB3)]
    [InlineData(0)]
    public void OtherKeys_AreNeitherVolumeNorLockKeys(int virtualKey)
    {
        Assert.Null(SystemKeyMap.VolumeFromVirtualKey(virtualKey));
        Assert.Null(SystemKeyMap.LockFromVirtualKey(virtualKey));
    }
}

public class FlyoutPositionTests
{
    [Theory]
    [InlineData(FlyoutPosition.TopLeft, FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Left)]
    [InlineData(FlyoutPosition.TopCenter, FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Center)]
    [InlineData(FlyoutPosition.TopRight, FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Right)]
    [InlineData(FlyoutPosition.BottomLeft, FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Left)]
    [InlineData(FlyoutPosition.BottomCenter, FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Center)]
    [InlineData(FlyoutPosition.BottomRight, FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Right)]
    public void Split_SeparatesTheVerticalAndHorizontalParts(
        FlyoutPosition position,
        FlyoutVerticalPosition expectedVertical,
        FlyoutHorizontalPosition expectedHorizontal)
    {
        var (vertical, horizontal) = FlyoutPlacement.Split(position);

        Assert.Equal(expectedVertical, vertical);
        Assert.Equal(expectedHorizontal, horizontal);
    }
}
