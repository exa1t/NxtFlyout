using NxtHUD.Core.Settings;
using NxtHUD.Core.ViewModels;

namespace NxtHUD.Tests;

public class SettingsViewModelTests
{
    private readonly InMemorySettingsService _settings = new();
    private readonly FakeStartupService _startup = new();

    private SettingsViewModel CreateViewModel() => new(_settings, _startup, "1.2.3", @"C:\data\settings.json");

    /// <summary>Records the names passed to PropertyChanged.</summary>
    private static List<string?> RecordChanges(SettingsViewModel viewModel)
    {
        var names = new List<string?>();
        viewModel.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [Fact]
    public void Properties_ReflectTheCurrentSettings()
    {
        _settings.Replace(new AppSettings
        {
            StartWithWindows = true,
            Language = "en",
            DisableInFullscreen = false,
            Theme = AppTheme.Dark,
            Backdrop = BackdropKind.None,
            VerticalPosition = FlyoutVerticalPosition.Top,
            HorizontalPosition = FlyoutHorizontalPosition.Left,
            CompactLayout = true,
            AnimationSpeed = AnimationSpeed.Fast,
            FlyoutTimeoutMs = 4500,
            MediaFlyoutEnabled = false,
            VolumeFlyoutEnabled = true,
            UpNextFlyoutEnabled = true,
            LockKeysFlyoutEnabled = false,
            TaskbarWidgetEnabled = true,
        });

        var viewModel = CreateViewModel();

        Assert.True(viewModel.StartWithWindows);
        Assert.Equal("en", viewModel.Language);
        Assert.False(viewModel.DisableInFullscreen);
        Assert.Equal(AppTheme.Dark, viewModel.Theme);
        Assert.Equal(BackdropKind.None, viewModel.Backdrop);
        Assert.Equal(FlyoutVerticalPosition.Top, viewModel.VerticalPosition);
        Assert.Equal(FlyoutHorizontalPosition.Left, viewModel.HorizontalPosition);
        Assert.True(viewModel.CompactLayout);
        Assert.Equal(AnimationSpeed.Fast, viewModel.AnimationSpeed);
        Assert.Equal(4.5, viewModel.TimeoutSeconds);
        Assert.False(viewModel.MediaFlyoutEnabled);
        Assert.True(viewModel.VolumeFlyoutEnabled);
        Assert.True(viewModel.UpNextFlyoutEnabled);
        Assert.False(viewModel.LockKeysFlyoutEnabled);
        Assert.True(viewModel.TaskbarWidgetEnabled);
    }

    [Fact]
    public void SettingAProperty_WritesThroughToTheSettingsService()
    {
        var viewModel = CreateViewModel();

        viewModel.Language = "en";
        viewModel.DisableInFullscreen = false;
        viewModel.Theme = AppTheme.Light;
        viewModel.Backdrop = BackdropKind.Acrylic;
        viewModel.VerticalPosition = FlyoutVerticalPosition.Top;
        viewModel.HorizontalPosition = FlyoutHorizontalPosition.Right;
        viewModel.CompactLayout = true;
        viewModel.AnimationSpeed = AnimationSpeed.Slow;
        viewModel.MediaFlyoutEnabled = false;
        viewModel.VolumeFlyoutEnabled = true;
        viewModel.UpNextFlyoutEnabled = true;
        viewModel.LockKeysFlyoutEnabled = false;
        viewModel.TaskbarWidgetEnabled = true;

        var expected = new AppSettings
        {
            Language = "en",
            DisableInFullscreen = false,
            Theme = AppTheme.Light,
            Backdrop = BackdropKind.Acrylic,
            VerticalPosition = FlyoutVerticalPosition.Top,
            HorizontalPosition = FlyoutHorizontalPosition.Right,
            CompactLayout = true,
            AnimationSpeed = AnimationSpeed.Slow,
            MediaFlyoutEnabled = false,
            VolumeFlyoutEnabled = true,
            UpNextFlyoutEnabled = true,
            LockKeysFlyoutEnabled = false,
            TaskbarWidgetEnabled = true,
        };
        Assert.Equal(expected, _settings.Current);
    }

    [Fact]
    public void SettingAProperty_RaisesPropertyChangedForThatPropertyOnly()
    {
        var viewModel = CreateViewModel();
        var changes = RecordChanges(viewModel);

        viewModel.Theme = AppTheme.Dark;

        Assert.Equal(new[] { nameof(SettingsViewModel.Theme) }, changes);
    }

    [Fact]
    public void ExternalSettingsChange_TellsBindingsToRefreshEverything()
    {
        var viewModel = CreateViewModel();
        var changes = RecordChanges(viewModel);

        _settings.Update(s => s.Theme = AppTheme.Dark);

        Assert.Equal(new[] { string.Empty }, changes);
        Assert.Equal(AppTheme.Dark, viewModel.Theme);
    }

    [Theory]
    [InlineData(3.0, 3000)]
    [InlineData(0.5, 500)]
    [InlineData(7.25, 7250)]
    [InlineData(1.0004, 1000)]
    [InlineData(0.0, AppSettings.MinTimeoutMs)]
    [InlineData(-2.0, AppSettings.MinTimeoutMs)]
    [InlineData(600.0, AppSettings.MaxTimeoutMs)]
    public void TimeoutSeconds_IsStoredInMillisecondsAndClamped(double seconds, int expectedMs)
    {
        var viewModel = CreateViewModel();
        _settings.Update(s => s.FlyoutTimeoutMs = 9999);

        viewModel.TimeoutSeconds = seconds;

        Assert.Equal(expectedMs, _settings.Current.FlyoutTimeoutMs);
        Assert.Equal(expectedMs / 1000.0, viewModel.TimeoutSeconds);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TimeoutSeconds_IgnoresValuesThatAreNotNumbers(double seconds)
    {
        var viewModel = CreateViewModel();

        viewModel.TimeoutSeconds = seconds;

        Assert.Equal(AppSettings.DefaultTimeoutMs, _settings.Current.FlyoutTimeoutMs);
    }

    [Fact]
    public void TimeoutSeconds_OutOfRange_StillNotifiesSoTheSliderSnapsBack()
    {
        var viewModel = CreateViewModel();
        _settings.Update(s => s.FlyoutTimeoutMs = AppSettings.MaxTimeoutMs);
        var changes = RecordChanges(viewModel);

        viewModel.TimeoutSeconds = 9000;

        Assert.Contains(nameof(SettingsViewModel.TimeoutSeconds), changes);
        Assert.Equal(viewModel.MaxTimeoutSeconds, viewModel.TimeoutSeconds);
    }

    [Fact]
    public void TimeoutRange_ComesFromTheSettingsLimits()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(0.5, viewModel.MinTimeoutSeconds);
        Assert.Equal(15.0, viewModel.MaxTimeoutSeconds);
    }

    [Fact]
    public void FlyoutPositionsAndDisplay_WriteThrough()
    {
        var viewModel = CreateViewModel();

        viewModel.Display = FlyoutDisplay.ActiveWindow;
        viewModel.VolumePosition = FlyoutPosition.TopRight;
        viewModel.LockKeysPosition = FlyoutPosition.TopCenter;
        viewModel.UpNextPosition = FlyoutPosition.BottomRight;
        viewModel.ReplaceNativeVolumeOverlay = false;

        var current = _settings.Current;
        Assert.Equal(FlyoutDisplay.ActiveWindow, current.Display);
        Assert.Equal(FlyoutPosition.TopRight, current.VolumePosition);
        Assert.Equal(FlyoutPosition.TopCenter, current.LockKeysPosition);
        Assert.Equal(FlyoutPosition.BottomRight, current.UpNextPosition);
        Assert.False(current.ReplaceNativeVolumeOverlay);

        Assert.Equal(FlyoutDisplay.ActiveWindow, viewModel.Display);
        Assert.Equal(FlyoutPosition.TopRight, viewModel.VolumePosition);
        Assert.Equal(Enum.GetValues<FlyoutPosition>(), viewModel.FlyoutPositions);
        Assert.Equal(Enum.GetValues<FlyoutDisplay>(), viewModel.Displays);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(119.6, 120)]
    [InlineData(-40.0, 0)]
    [InlineData(99999.0, AppSettings.MaxTaskbarWidgetOffset)]
    public void TaskbarWidgetOffset_IsStoredAsAWholeNumberAndClamped(double input, int expected)
    {
        var viewModel = CreateViewModel();
        _settings.Update(s => s.TaskbarWidgetOffset = 500);

        viewModel.TaskbarWidgetOffset = input;

        Assert.Equal(expected, _settings.Current.TaskbarWidgetOffset);
        Assert.Equal(expected, viewModel.TaskbarWidgetOffset);
        Assert.Equal(1000.0, viewModel.MaxTaskbarWidgetOffset);
    }

    [Fact]
    public void StartWithWindows_On_RegistersAndStores()
    {
        var viewModel = CreateViewModel();

        viewModel.StartWithWindows = true;

        Assert.Equal(new[] { true }, _startup.SetEnabledCalls);
        Assert.True(_startup.Enabled);
        Assert.True(_settings.Current.StartWithWindows);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void StartWithWindows_Off_UnregistersAndStores()
    {
        _settings.Update(s => s.StartWithWindows = true);
        _startup.Enabled = true;
        var viewModel = CreateViewModel();

        viewModel.StartWithWindows = false;

        Assert.Equal(new[] { false }, _startup.SetEnabledCalls);
        Assert.False(_startup.Enabled);
        Assert.False(_settings.Current.StartWithWindows);
    }

    [Fact]
    public void StartWithWindows_SameValue_DoesNotTouchTheRegistration()
    {
        var viewModel = CreateViewModel();

        viewModel.StartWithWindows = false;

        Assert.Empty(_startup.SetEnabledCalls);
    }

    [Fact]
    public void StartWithWindows_WhenRegistrationFails_KeepsTheOldValueAndShowsTheError()
    {
        _startup.FailWith = new UnauthorizedAccessException("Access to the registry key is denied.");
        var viewModel = CreateViewModel();
        var changes = RecordChanges(viewModel);

        viewModel.StartWithWindows = true;

        Assert.False(_settings.Current.StartWithWindows);
        Assert.False(viewModel.StartWithWindows);
        Assert.True(viewModel.HasError);
        Assert.Equal("Access to the registry key is denied.", viewModel.ErrorMessage);

        // The switch is told to re-read its value so it snaps back to "off".
        Assert.Contains(nameof(SettingsViewModel.StartWithWindows), changes);
        Assert.Contains(nameof(SettingsViewModel.ErrorMessage), changes);
        Assert.Contains(nameof(SettingsViewModel.HasError), changes);
    }

    [Fact]
    public void StartWithWindows_SucceedingAfterAFailure_ClearsTheError()
    {
        _startup.FailWith = new InvalidOperationException("nope");
        var viewModel = CreateViewModel();
        viewModel.StartWithWindows = true;
        Assert.True(viewModel.HasError);

        _startup.FailWith = null;
        viewModel.StartWithWindows = true;

        Assert.False(viewModel.HasError);
        Assert.Null(viewModel.ErrorMessage);
        Assert.True(_settings.Current.StartWithWindows);
    }

    [Fact]
    public void StartWithWindows_UnexpectedExceptionsAreNotSwallowed()
    {
        _startup.FailWith = new FormatException("a bug, not an environment problem");
        var viewModel = CreateViewModel();

        Assert.Throws<FormatException>(() => viewModel.StartWithWindows = true);
        Assert.False(_settings.Current.StartWithWindows);
    }

    [Fact]
    public void Language_Null_IsIgnored()
    {
        var viewModel = CreateViewModel();
        viewModel.Language = "en";

        viewModel.Language = null!;

        Assert.Equal("en", _settings.Current.Language);
    }

    [Fact]
    public void ResetToDefaults_RestoresEverythingAndUnregistersStartup()
    {
        var viewModel = CreateViewModel();
        viewModel.StartWithWindows = true;
        viewModel.Theme = AppTheme.Dark;
        viewModel.TimeoutSeconds = 9;
        viewModel.TaskbarWidgetEnabled = true;
        var changes = RecordChanges(viewModel);

        viewModel.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(new AppSettings(), _settings.Current);
        Assert.False(_startup.Enabled);
        Assert.Contains(string.Empty, changes);
        Assert.False(viewModel.StartWithWindows);
        Assert.Equal(AppTheme.System, viewModel.Theme);
    }

    [Fact]
    public void ResetToDefaults_WhenUnregisteringFails_StillResetsAndShowsTheError()
    {
        var viewModel = CreateViewModel();
        viewModel.Theme = AppTheme.Dark;
        _startup.FailWith = new UnauthorizedAccessException("denied");

        viewModel.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(new AppSettings(), _settings.Current);
        Assert.Equal("denied", viewModel.ErrorMessage);
    }

    [Fact]
    public void PickerLists_OfferEveryEnumValue()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(Enum.GetValues<AppTheme>(), viewModel.Themes);
        Assert.Equal(Enum.GetValues<BackdropKind>(), viewModel.Backdrops);
        Assert.Equal(Enum.GetValues<FlyoutVerticalPosition>(), viewModel.VerticalPositions);
        Assert.Equal(Enum.GetValues<FlyoutHorizontalPosition>(), viewModel.HorizontalPositions);
        Assert.Equal(Enum.GetValues<AnimationSpeed>(), viewModel.AnimationSpeeds);
        Assert.Contains(AppSettings.SystemLanguage, viewModel.Languages);
        Assert.Contains("en", viewModel.Languages);
    }

    [Fact]
    public void AboutInfo_IsPassedThrough()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("1.2.3", viewModel.AppVersion);
        Assert.Equal(@"C:\data\settings.json", viewModel.SettingsFilePath);
    }

    [Fact]
    public void Dispose_StopsListeningToSettingsChanges()
    {
        var viewModel = CreateViewModel();
        var changes = RecordChanges(viewModel);

        viewModel.Dispose();
        _settings.Update(s => s.Theme = AppTheme.Dark);

        Assert.Empty(changes);
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!, _startup));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(_settings, null!));
    }
}
