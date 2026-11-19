using System.Text;
using NxtHUD.Core.Settings;

namespace NxtHUD.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    /// <summary>Long enough that a delayed save never fires on its own during a test.</summary>
    private static readonly TimeSpan NeverDuringTest = TimeSpan.FromMinutes(10);

    private readonly TempDirectory _temp = new();

    private string SettingsPath => _temp.File("settings.json");

    public void Dispose() => _temp.Dispose();

    /// <summary>A service that writes synchronously on every change.</summary>
    private SettingsService CreateImmediate() => new(SettingsPath, TimeSpan.Zero);

    // ----- Loading -----

    [Fact]
    public void Load_WhenFileIsMissing_UsesDefaultsAndDoesNotCreateTheFile()
    {
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.CreatedDefaults, result);
        Assert.Equal(new AppSettings(), service.Current);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEverySetting()
    {
        var expected = new AppSettings
        {
            StartWithWindows = true,
            Language = "en",
            DisableInFullscreen = false,
            Theme = AppTheme.Dark,
            Backdrop = BackdropKind.Acrylic,
            VerticalPosition = FlyoutVerticalPosition.Top,
            HorizontalPosition = FlyoutHorizontalPosition.Right,
            CompactLayout = true,
            AnimationSpeed = AnimationSpeed.Slow,
            FlyoutTimeoutMs = 7500,
            MediaFlyoutEnabled = false,
            VolumeFlyoutEnabled = true,
            UpNextFlyoutEnabled = true,
            LockKeysFlyoutEnabled = false,
            TaskbarWidgetEnabled = true,
            PinnedMediaSessionId = "Spotify.exe",
            Display = FlyoutDisplay.Cursor,
            VolumePosition = FlyoutPosition.TopRight,
            LockKeysPosition = FlyoutPosition.TopCenter,
            UpNextPosition = FlyoutPosition.BottomRight,
            ReplaceNativeVolumeOverlay = false,
            TaskbarWidgetOffset = 120,
        };

        using (var writer = CreateImmediate())
        {
            writer.Replace(expected);
        }

        using var reader = CreateImmediate();
        var result = reader.Load();

        Assert.Equal(SettingsLoadResult.Loaded, result);
        Assert.Equal(expected, reader.Current);
    }

    [Fact]
    public void Load_PartialFile_KeepsDefaultsForMissingSettings()
    {
        File.WriteAllText(SettingsPath, """{ "theme": "Dark" }""");
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.Loaded, result);
        Assert.Equal(new AppSettings { Theme = AppTheme.Dark }, service.Current);
    }

    [Fact]
    public void Load_IgnoresUnknownProperties()
    {
        File.WriteAllText(SettingsPath, """{ "fromTheFuture": { "a": [1, 2] }, "compactLayout": true }""");
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.Loaded, result);
        Assert.True(service.Current.CompactLayout);
    }

    [Fact]
    public void Load_UnknownEnumName_FallsBackForThatSettingOnly()
    {
        File.WriteAllText(SettingsPath, """{ "theme": "Sepia", "backdrop": "Acrylic", "flyoutTimeoutMs": 4000 }""");
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.Loaded, result);
        Assert.Equal(AppTheme.System, service.Current.Theme);
        Assert.Equal(BackdropKind.Acrylic, service.Current.Backdrop);
        Assert.Equal(4000, service.Current.FlyoutTimeoutMs);
    }

    [Theory]
    [InlineData("\"dark\"", AppTheme.Dark)]
    [InlineData("\"LIGHT\"", AppTheme.Light)]
    [InlineData("2", AppTheme.Dark)]
    [InlineData("99", AppTheme.System)]
    [InlineData("null", AppTheme.System)]
    [InlineData("true", AppTheme.System)]
    [InlineData("{ \"nested\": 1 }", AppTheme.System)]
    [InlineData("[1, 2]", AppTheme.System)]
    public void Load_ReadsEnumsTolerantly(string jsonValue, AppTheme expected)
    {
        File.WriteAllText(SettingsPath, $$"""{ "theme": {{jsonValue}}, "compactLayout": true }""");
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.Loaded, result);
        Assert.Equal(expected, service.Current.Theme);
        Assert.True(service.Current.CompactLayout);
    }

    [Fact]
    public void Load_FileFromSchemaOne_MovesTheOldCentreDefaultToTheLeft_Once()
    {
        File.WriteAllText(SettingsPath, """{ "schemaVersion": 1, "horizontalPosition": "Center" }""");
        using var service = CreateImmediate();

        service.Load();
        Assert.Equal(FlyoutHorizontalPosition.Left, service.Current.HorizontalPosition);

        // Choosing centre afterwards sticks, including across a restart.
        service.Update(s => s.HorizontalPosition = FlyoutHorizontalPosition.Center);
        using var reader = CreateImmediate();
        reader.Load();
        Assert.Equal(FlyoutHorizontalPosition.Center, reader.Current.HorizontalPosition);
    }

    [Fact]
    public void Load_FileFromBeforeTheNewSettingsExisted_GetsTheirDefaults()
    {
        File.WriteAllText(SettingsPath, """{ "schemaVersion": 2, "horizontalPosition": "Right", "volumeFlyoutEnabled": true }""");
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.Loaded, result);
        Assert.Equal(FlyoutHorizontalPosition.Right, service.Current.HorizontalPosition);
        Assert.True(service.Current.VolumeFlyoutEnabled);
        Assert.Equal(FlyoutPosition.BottomCenter, service.Current.VolumePosition);
        Assert.Equal(FlyoutDisplay.Primary, service.Current.Display);
        Assert.True(service.Current.ReplaceNativeVolumeOverlay);
        Assert.Equal(AppSettings.CurrentSchemaVersion, service.Current.SchemaVersion);
    }

    [Fact]
    public void Load_NormalizesOutOfRangeValues()
    {
        File.WriteAllText(SettingsPath, """{ "flyoutTimeoutMs": 999999, "language": "" }""");
        using var service = CreateImmediate();

        service.Load();

        Assert.Equal(AppSettings.MaxTimeoutMs, service.Current.FlyoutTimeoutMs);
        Assert.Equal(AppSettings.SystemLanguage, service.Current.Language);
    }

    [Fact]
    public void Load_AcceptsCommentsTrailingCommasAndByteOrderMark()
    {
        const string json = """
            {
              // edited by hand
              "Theme": "Light",
              "compactLayout": true,
            }
            """;
        File.WriteAllText(SettingsPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.Loaded, result);
        Assert.Equal(AppTheme.Light, service.Current.Theme);
        Assert.True(service.Current.CompactLayout);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"theme\": ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ \"flyoutTimeoutMs\": \"soon\" }")]
    public void Load_CorruptFile_UsesDefaultsAndKeepsABackup(string content)
    {
        File.WriteAllText(SettingsPath, content);
        using var service = CreateImmediate();

        var result = service.Load();

        Assert.Equal(SettingsLoadResult.RecoveredFromCorruptFile, result);
        Assert.Equal(new AppSettings(), service.Current);
        Assert.True(File.Exists(service.CorruptBackupPath));
        Assert.Equal(content, File.ReadAllText(service.CorruptBackupPath));
    }

    [Fact]
    public void Load_RaisesChanged_OnlyWhenTheSettingsDiffer()
    {
        File.WriteAllText(SettingsPath, """{ "theme": "Dark" }""");
        using var service = CreateImmediate();
        var events = new List<SettingsChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        service.Load();
        service.Load();

        var change = Assert.Single(events);
        Assert.Equal(AppTheme.System, change.Previous.Theme);
        Assert.Equal(AppTheme.Dark, change.Current.Theme);
    }

    // ----- Updating -----

    [Fact]
    public void Update_PublishesANewSnapshotAndLeavesTheOldOneUntouched()
    {
        using var service = CreateImmediate();
        var before = service.Current;

        service.Update(s => s.Theme = AppTheme.Dark);

        Assert.NotSame(before, service.Current);
        Assert.Equal(AppTheme.System, before.Theme);
        Assert.Equal(AppTheme.Dark, service.Current.Theme);
    }

    [Fact]
    public void Update_RaisesChangedWithPreviousAndCurrent()
    {
        using var service = CreateImmediate();
        var events = new List<SettingsChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        service.Update(s => s.FlyoutTimeoutMs = 5000);

        var change = Assert.Single(events);
        Assert.Equal(AppSettings.DefaultTimeoutMs, change.Previous.FlyoutTimeoutMs);
        Assert.Equal(5000, change.Current.FlyoutTimeoutMs);
        Assert.Same(service.Current, change.Current);
    }

    [Fact]
    public void Update_ThatChangesNothing_NeitherRaisesNorWrites()
    {
        using var service = CreateImmediate();
        var raised = 0;
        service.Changed += (_, _) => raised++;

        service.Update(s => s.Theme = AppTheme.System);
        service.Update(_ => { });

        Assert.Equal(0, raised);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Update_NormalizesBeforePublishing()
    {
        using var service = CreateImmediate();

        service.Update(s =>
        {
            s.FlyoutTimeoutMs = -5;
            s.Theme = (AppTheme)123;
        });

        Assert.Equal(AppSettings.MinTimeoutMs, service.Current.FlyoutTimeoutMs);
        Assert.Equal(AppTheme.System, service.Current.Theme);
    }

    [Fact]
    public void Replace_SwapsEverythingAndDoesNotKeepTheCallersInstance()
    {
        using var service = CreateImmediate();
        var replacement = new AppSettings { Theme = AppTheme.Light, CompactLayout = true };

        service.Replace(replacement);
        replacement.CompactLayout = false;

        Assert.Equal(AppTheme.Light, service.Current.Theme);
        Assert.True(service.Current.CompactLayout);
    }

    // ----- Saving -----

    [Fact]
    public void Update_WithZeroDelay_WritesImmediately()
    {
        using var service = CreateImmediate();

        service.Update(s => s.Theme = AppTheme.Dark);

        Assert.True(File.Exists(SettingsPath));
        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void SavedFile_IsIndentedCamelCaseWithEnumNames()
    {
        using var service = CreateImmediate();

        service.Update(s => s.HorizontalPosition = FlyoutHorizontalPosition.Right);

        var json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"schemaVersion\": 3", json);
        Assert.Contains("\"volumePosition\": \"BottomCenter\"", json);
        Assert.Contains("\"horizontalPosition\": \"Right\"", json);
        Assert.Contains("\"backdrop\": \"Mica\"", json);
        Assert.Contains("\"pinnedMediaSessionId\": null", json);
        Assert.Contains(Environment.NewLine, json);
    }

    [Fact]
    public void Save_CreatesMissingDirectoriesAndLeavesNoTempFile()
    {
        var nestedPath = Path.Combine(_temp.Path, "a", "b", "settings.json");
        using var service = new SettingsService(nestedPath, TimeSpan.Zero);

        service.Save();

        Assert.True(File.Exists(nestedPath));
        Assert.Equal(new[] { nestedPath }, Directory.GetFiles(Path.GetDirectoryName(nestedPath)!));
    }

    [Fact]
    public void Save_OverwritesAnExistingFile()
    {
        File.WriteAllText(SettingsPath, """{ "theme": "Dark" }""");
        using var service = CreateImmediate();
        service.Load();

        service.Update(s => s.Theme = AppTheme.Light);

        using var reader = CreateImmediate();
        reader.Load();
        Assert.Equal(AppTheme.Light, reader.Current.Theme);
    }

    [Fact]
    public void Update_WithDelay_DoesNotWriteUntilFlushed()
    {
        using var service = new SettingsService(SettingsPath, NeverDuringTest);

        service.Update(s => s.Theme = AppTheme.Dark);
        Assert.False(File.Exists(SettingsPath));

        service.Flush();
        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Flush_WithNothingPending_DoesNotWrite()
    {
        using var service = new SettingsService(SettingsPath, NeverDuringTest);

        service.Flush();

        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Dispose_WritesPendingChanges()
    {
        var service = new SettingsService(SettingsPath, NeverDuringTest);
        service.Update(s => s.CompactLayout = true);
        Assert.False(File.Exists(SettingsPath));

        service.Dispose();

        using var reader = CreateImmediate();
        reader.Load();
        Assert.True(reader.Current.CompactLayout);
    }

    [Fact]
    public async Task Update_WithShortDelay_SavesInTheBackground()
    {
        using var service = new SettingsService(SettingsPath, TimeSpan.FromMilliseconds(20));

        service.Update(s => s.Theme = AppTheme.Dark);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!File.Exists(SettingsPath) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void SaveFailure_IsReportedThroughSaveFailed_AndTheChangeStillApplies()
    {
        // A directory sitting where the settings file should go makes every write fail.
        Directory.CreateDirectory(SettingsPath);
        using var service = CreateImmediate();
        var failures = new List<Exception>();
        service.SaveFailed += (_, ex) => failures.Add(ex);

        service.Update(s => s.Theme = AppTheme.Dark);

        Assert.NotEmpty(failures);
        Assert.Equal(AppTheme.Dark, service.Current.Theme);
    }

    // ----- Lifetime and arguments -----

    [Fact]
    public void AfterDispose_UpdatesAreRejected()
    {
        var service = CreateImmediate();
        service.Dispose();
        service.Dispose(); // a second Dispose is harmless

        Assert.Throws<ObjectDisposedException>(() => service.Update(s => s.CompactLayout = true));
        Assert.Throws<ObjectDisposedException>(() => service.Load());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsABlankPath(string path)
    {
        Assert.Throws<ArgumentException>(() => new SettingsService(path));
    }

    [Fact]
    public void Constructor_RejectsANegativeDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SettingsService(SettingsPath, TimeSpan.FromSeconds(-1)));
    }
}
