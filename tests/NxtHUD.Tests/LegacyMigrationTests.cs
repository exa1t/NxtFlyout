using NxtHUD.Core.Migration;

namespace NxtHUD.Tests;

/// <summary>An in-memory disk. Paths are compared case-insensitively, as on Windows.</summary>
internal sealed class FakeMigrationFileSystem : IMigrationFileSystem
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When set, CopyFile throws this for any source path that contains the given text.</summary>
    public (string PathPart, Exception Error)? FailCopy { get; set; }

    public void AddFile(string path, string content)
    {
        Files[path] = content;
        Directories.Add(Path.GetDirectoryName(path)!);
    }

    public bool FileExists(string path) => Files.ContainsKey(path);

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public void CreateDirectory(string path) => Directories.Add(path);

    public void CopyFile(string source, string destination)
    {
        if (FailCopy is { } fail && source.Contains(fail.PathPart, StringComparison.OrdinalIgnoreCase))
        {
            throw fail.Error;
        }

        if (Files.ContainsKey(destination))
        {
            throw new IOException($"{destination} already exists.");
        }

        Files[destination] = Files[source];
    }

    public IEnumerable<string> GetFiles(string directory) =>
        Files.Keys.Where(f => string.Equals(Path.GetDirectoryName(f), directory, StringComparison.OrdinalIgnoreCase)).ToList();
}

/// <summary>An in-memory Run key with StartupApproved flags.</summary>
internal sealed class FakeStartupRegistry : IStartupRegistry
{
    public Dictionary<string, string> Run { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> DisabledInTaskManager { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Exception? FailWith { get; set; }

    public string? GetRunCommand(string valueName)
    {
        ThrowIfFailing();
        return Run.GetValueOrDefault(valueName);
    }

    public bool IsDisabledInTaskManager(string valueName) => DisabledInTaskManager.Contains(valueName);

    public void SetRunCommand(string valueName, string command)
    {
        ThrowIfFailing();
        Run[valueName] = command;
    }

    public void DeleteEntry(string valueName)
    {
        Run.Remove(valueName);
        DisabledInTaskManager.Remove(valueName);
    }

    private void ThrowIfFailing()
    {
        if (FailWith is { } error)
        {
            throw error;
        }
    }
}

public class LegacyMigrationSettingsTests
{
    private const string Old = @"C:\Users\Test\AppData\Roaming\NxtFlyout";
    private const string New = @"C:\Users\Test\AppData\Roaming\NxtHUD";

    private readonly FakeMigrationFileSystem _files = new();
    private readonly List<string> _log = new();

    private LegacyMigration Create() => new(_files, new FakeStartupRegistry(), _log.Add);

    [Fact]
    public void Without_old_data_nothing_happens()
    {
        var result = Create().MigrateSettings(Old, New);

        Assert.Equal(SettingsMigrationResult.NothingToMigrate, result);
        Assert.Empty(_files.Files);
        Assert.DoesNotContain(New, _files.Directories);
        Assert.Empty(_log);
    }

    [Fact]
    public void Old_settings_only_are_copied_and_the_old_folder_is_kept()
    {
        _files.AddFile($@"{Old}\settings.json", "{\"theme\":\"Dark\"}");
        _files.AddFile($@"{Old}\logs\nxtflyout.log", "old log");

        var result = Create().MigrateSettings(Old, New);

        Assert.Equal(SettingsMigrationResult.Copied, result);
        Assert.Equal("{\"theme\":\"Dark\"}", _files.Files[$@"{New}\settings.json"]);
        Assert.Equal("old log", _files.Files[$@"{New}\logs\nxtflyout.log"]);
        Assert.True(_files.FileExists($@"{Old}\settings.json"));
        Assert.True(_files.FileExists($@"{Old}\logs\nxtflyout.log"));
        Assert.Contains(_log, line => line.Contains("Copied the settings", StringComparison.Ordinal));
    }

    [Fact]
    public void When_both_exist_the_new_settings_win()
    {
        _files.AddFile($@"{Old}\settings.json", "old");
        _files.AddFile($@"{New}\settings.json", "new");

        var result = Create().MigrateSettings(Old, New);

        Assert.Equal(SettingsMigrationResult.AlreadyMigrated, result);
        Assert.Equal("new", _files.Files[$@"{New}\settings.json"]);
        Assert.Empty(_log);
    }

    [Fact]
    public void A_copy_failure_is_logged_and_does_not_throw()
    {
        _files.AddFile($@"{Old}\settings.json", "old");
        _files.FailCopy = ("settings.json", new UnauthorizedAccessException("Access is denied."));

        var result = Create().MigrateSettings(Old, New);

        Assert.Equal(SettingsMigrationResult.Failed, result);
        Assert.False(_files.FileExists($@"{New}\settings.json"));
        Assert.Contains(_log, line => line.Contains("failed", StringComparison.Ordinal) && line.Contains("Access is denied", StringComparison.Ordinal));
    }

    [Fact]
    public void A_log_that_cannot_be_copied_does_not_undo_the_settings()
    {
        _files.AddFile($@"{Old}\settings.json", "old");
        _files.AddFile($@"{Old}\logs\nxtflyout.log", "locked");
        _files.FailCopy = ("nxtflyout.log", new IOException("The file is in use."));

        var result = Create().MigrateSettings(Old, New);

        Assert.Equal(SettingsMigrationResult.Copied, result);
        Assert.True(_files.FileExists($@"{New}\settings.json"));
        Assert.Contains(_log, line => line.Contains("logs", StringComparison.Ordinal) && line.Contains("failed", StringComparison.Ordinal));
    }

    [Fact]
    public void Log_files_already_in_the_new_folder_are_not_overwritten()
    {
        _files.AddFile($@"{Old}\settings.json", "old");
        _files.AddFile($@"{Old}\logs\shared.log", "old log");
        _files.AddFile($@"{New}\logs\shared.log", "new log");

        Create().MigrateSettings(Old, New);

        Assert.Equal("new log", _files.Files[$@"{New}\logs\shared.log"]);
    }
}

public class LegacyMigrationAutostartTests
{
    private const string Command = "\"C:\\Programs\\NxtHUD\\NxtHUD.exe\" --autostart";

    private readonly FakeStartupRegistry _registry = new();
    private readonly List<string> _log = new();

    private LegacyMigration Create() => new(new FakeMigrationFileSystem(), _registry, _log.Add);

    [Fact]
    public void Without_an_old_entry_nothing_happens()
    {
        var result = Create().MigrateAutostart("NxtHUD", Command);

        Assert.Equal(AutostartMigrationResult.NothingToMigrate, result);
        Assert.Empty(_registry.Run);
    }

    [Fact]
    public void An_enabled_old_entry_is_moved_to_the_new_name_and_running_exe()
    {
        _registry.Run["NxtFlyout"] = "\"C:\\Old\\NxtFlyout.exe\" --autostart";

        var result = Create().MigrateAutostart("NxtHUD", Command);

        Assert.Equal(AutostartMigrationResult.Moved, result);
        Assert.False(_registry.Run.ContainsKey("NxtFlyout"));
        Assert.Equal(Command, _registry.Run["NxtHUD"]);
    }

    [Fact]
    public void An_entry_switched_off_in_Task_Manager_is_removed_and_not_recreated()
    {
        _registry.Run["NxtFlyout"] = "\"C:\\Old\\NxtFlyout.exe\" --autostart";
        _registry.DisabledInTaskManager.Add("NxtFlyout");

        var result = Create().MigrateAutostart("NxtHUD", Command);

        Assert.Equal(AutostartMigrationResult.RemovedDisabled, result);
        Assert.Empty(_registry.Run);
        Assert.Empty(_registry.DisabledInTaskManager);
    }

    [Fact]
    public void An_existing_new_entry_is_kept_and_the_old_one_removed()
    {
        _registry.Run["NxtFlyout"] = "old";
        _registry.Run["NxtHUD"] = "\"D:\\Elsewhere\\NxtHUD.exe\" --autostart";

        var result = Create().MigrateAutostart("NxtHUD", Command);

        Assert.Equal(AutostartMigrationResult.RemovedDuplicate, result);
        Assert.Equal("\"D:\\Elsewhere\\NxtHUD.exe\" --autostart", _registry.Run["NxtHUD"]);
        Assert.False(_registry.Run.ContainsKey("NxtFlyout"));
    }

    [Fact]
    public void A_registry_failure_is_logged_and_does_not_throw()
    {
        _registry.Run["NxtFlyout"] = "old";
        _registry.FailWith = new System.Security.SecurityException("Requested registry access is not allowed.");

        var result = Create().MigrateAutostart("NxtHUD", Command);

        Assert.Equal(AutostartMigrationResult.Failed, result);
        Assert.Contains(_log, line => line.Contains("failed", StringComparison.Ordinal));
    }
}
