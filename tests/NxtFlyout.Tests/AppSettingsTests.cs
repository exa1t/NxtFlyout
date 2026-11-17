using NxtFlyout.Core.Settings;

namespace NxtFlyout.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_MatchTheDocumentedOutOfBoxExperience()
    {
        var settings = new AppSettings();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.False(settings.StartWithWindows);
        Assert.Equal(AppSettings.SystemLanguage, settings.Language);
        Assert.True(settings.DisableInFullscreen);

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(BackdropKind.Mica, settings.Backdrop);
        Assert.Equal(FlyoutVerticalPosition.Bottom, settings.VerticalPosition);
        Assert.Equal(FlyoutHorizontalPosition.Left, settings.HorizontalPosition);
        Assert.False(settings.CompactLayout);
        Assert.Equal(AnimationSpeed.Normal, settings.AnimationSpeed);
        Assert.Equal(AppSettings.DefaultTimeoutMs, settings.FlyoutTimeoutMs);

        Assert.True(settings.MediaFlyoutEnabled);
        Assert.False(settings.VolumeFlyoutEnabled);
        Assert.False(settings.UpNextFlyoutEnabled);
        Assert.True(settings.LockKeysFlyoutEnabled);
        Assert.False(settings.TaskbarWidgetEnabled);
        Assert.Null(settings.PinnedMediaSessionId);

        Assert.Equal(FlyoutDisplay.Primary, settings.Display);
        Assert.Equal(FlyoutPosition.BottomCenter, settings.VolumePosition);
        Assert.Equal(FlyoutPosition.BottomCenter, settings.LockKeysPosition);
        Assert.Equal(FlyoutPosition.BottomLeft, settings.UpNextPosition);
        Assert.True(settings.ReplaceNativeVolumeOverlay);
        Assert.Equal(0, settings.TaskbarWidgetOffset);
    }

    [Fact]
    public void Defaults_AreAlreadyNormalized()
    {
        var settings = new AppSettings();
        var untouched = new AppSettings();

        settings.Normalize();

        Assert.Equal(untouched, settings);
    }

    [Theory]
    [InlineData(int.MinValue, AppSettings.MinTimeoutMs)]
    [InlineData(0, AppSettings.MinTimeoutMs)]
    [InlineData(499, AppSettings.MinTimeoutMs)]
    [InlineData(500, 500)]
    [InlineData(3000, 3000)]
    [InlineData(15000, 15000)]
    [InlineData(15001, AppSettings.MaxTimeoutMs)]
    [InlineData(int.MaxValue, AppSettings.MaxTimeoutMs)]
    public void Normalize_ClampsTheTimeout(int input, int expected)
    {
        var settings = new AppSettings { FlyoutTimeoutMs = input };

        settings.Normalize();

        Assert.Equal(expected, settings.FlyoutTimeoutMs);
    }

    [Fact]
    public void Normalize_ReplacesUndefinedEnumValuesWithDefaults()
    {
        var settings = new AppSettings
        {
            Theme = (AppTheme)42,
            Backdrop = (BackdropKind)(-1),
            VerticalPosition = (FlyoutVerticalPosition)7,
            HorizontalPosition = (FlyoutHorizontalPosition)(-3),
            AnimationSpeed = (AnimationSpeed)99,
        };

        settings.Normalize();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(BackdropKind.Mica, settings.Backdrop);
        Assert.Equal(FlyoutVerticalPosition.Bottom, settings.VerticalPosition);
        Assert.Equal(FlyoutHorizontalPosition.Left, settings.HorizontalPosition);
        Assert.Equal(AnimationSpeed.Normal, settings.AnimationSpeed);
    }

    [Fact]
    public void Normalize_KeepsDefinedEnumValues()
    {
        var settings = new AppSettings
        {
            Theme = AppTheme.Light,
            Backdrop = BackdropKind.None,
            VerticalPosition = FlyoutVerticalPosition.Top,
            HorizontalPosition = FlyoutHorizontalPosition.Right,
            AnimationSpeed = AnimationSpeed.Off,
        };

        settings.Normalize();

        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.Equal(BackdropKind.None, settings.Backdrop);
        Assert.Equal(FlyoutVerticalPosition.Top, settings.VerticalPosition);
        Assert.Equal(FlyoutHorizontalPosition.Right, settings.HorizontalPosition);
        Assert.Equal(AnimationSpeed.Off, settings.AnimationSpeed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_BlankLanguage_FallsBackToSystem(string? language)
    {
        var settings = new AppSettings { Language = language! };

        settings.Normalize();

        Assert.Equal(AppSettings.SystemLanguage, settings.Language);
    }

    [Fact]
    public void Normalize_TrimsLanguage()
    {
        var settings = new AppSettings { Language = "  en " };

        settings.Normalize();

        Assert.Equal("en", settings.Language);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Normalize_BlankPinnedSession_MeansAutomatic(string pinned)
    {
        var settings = new AppSettings { PinnedMediaSessionId = pinned };

        settings.Normalize();

        Assert.Null(settings.PinnedMediaSessionId);
    }

    [Fact]
    public void Normalize_StampsTheCurrentSchemaVersion()
    {
        var settings = new AppSettings { SchemaVersion = 0 };

        settings.Normalize();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Normalize_SchemaOneFileWithTheOldCentreDefault_MovesToTheLeft()
    {
        var settings = new AppSettings { SchemaVersion = 1, HorizontalPosition = FlyoutHorizontalPosition.Center };

        settings.Normalize();

        Assert.Equal(FlyoutHorizontalPosition.Left, settings.HorizontalPosition);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Normalize_SchemaOneFileWithAnotherPosition_KeepsIt()
    {
        var settings = new AppSettings { SchemaVersion = 1, HorizontalPosition = FlyoutHorizontalPosition.Right };

        settings.Normalize();

        Assert.Equal(FlyoutHorizontalPosition.Right, settings.HorizontalPosition);
    }

    [Fact]
    public void Normalize_CentreChosenOnTheCurrentSchema_IsKept()
    {
        var settings = new AppSettings { HorizontalPosition = FlyoutHorizontalPosition.Center };

        settings.Normalize();
        settings.Normalize();

        Assert.Equal(FlyoutHorizontalPosition.Center, settings.HorizontalPosition);
    }

    [Fact]
    public void Normalize_ReplacesUndefinedFlyoutPositionsAndDisplay()
    {
        var settings = new AppSettings
        {
            Display = (FlyoutDisplay)9,
            VolumePosition = (FlyoutPosition)(-1),
            LockKeysPosition = (FlyoutPosition)6,
            UpNextPosition = (FlyoutPosition)77,
        };

        settings.Normalize();

        Assert.Equal(FlyoutDisplay.Primary, settings.Display);
        Assert.Equal(FlyoutPosition.BottomCenter, settings.VolumePosition);
        Assert.Equal(FlyoutPosition.BottomCenter, settings.LockKeysPosition);
        Assert.Equal(FlyoutPosition.BottomLeft, settings.UpNextPosition);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(250, 250)]
    [InlineData(1000, 1000)]
    [InlineData(5000, AppSettings.MaxTaskbarWidgetOffset)]
    public void Normalize_ClampsTheTaskbarWidgetOffset(int input, int expected)
    {
        var settings = new AppSettings { TaskbarWidgetOffset = input };

        settings.Normalize();

        Assert.Equal(expected, settings.TaskbarWidgetOffset);
    }

    [Fact]
    public void Copy_IsEqualButIndependent()
    {
        var original = new AppSettings { Theme = AppTheme.Dark, FlyoutTimeoutMs = 5000 };

        var copy = original.Copy();
        Assert.Equal(original, copy);
        Assert.NotSame(original, copy);

        copy.Theme = AppTheme.Light;
        Assert.Equal(AppTheme.Dark, original.Theme);
        Assert.NotEqual(original, copy);
    }
}
