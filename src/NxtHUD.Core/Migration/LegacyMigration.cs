namespace NxtHUD.Core.Migration;

/// <summary>The file operations <see cref="LegacyMigration"/> needs, so tests can stand in for the disk.</summary>
public interface IMigrationFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    /// <summary>Copies a file; never overwrites an existing destination (throws IOException instead).</summary>
    void CopyFile(string source, string destination);

    /// <summary>The files directly inside <paramref name="directory"/>.</summary>
    IEnumerable<string> GetFiles(string directory);
}

/// <summary>
/// The per-user start-with-Windows registry entries <see cref="LegacyMigration"/> needs:
/// HKCU\...\CurrentVersion\Run and the matching StartupApproved flag.
/// </summary>
public interface IStartupRegistry
{
    /// <summary>The command of a Run value, or null when there is none.</summary>
    string? GetRunCommand(string valueName);

    /// <summary>True when the user switched the entry off in Task Manager or Settings → Apps → Startup.</summary>
    bool IsDisabledInTaskManager(string valueName);

    void SetRunCommand(string valueName, string command);

    /// <summary>Removes the Run value and its StartupApproved flag, if they exist.</summary>
    void DeleteEntry(string valueName);
}

/// <summary>What <see cref="LegacyMigration.MigrateSettings"/> did.</summary>
public enum SettingsMigrationResult
{
    /// <summary>There was nothing from the old app to bring across.</summary>
    NothingToMigrate,

    /// <summary>The new app already has settings; the old ones were left alone.</summary>
    AlreadyMigrated,

    /// <summary>The old settings were copied to the new folder.</summary>
    Copied,

    /// <summary>Copying failed; the app starts with defaults (and the old files are untouched).</summary>
    Failed,
}

/// <summary>What <see cref="LegacyMigration.MigrateAutostart"/> did.</summary>
public enum AutostartMigrationResult
{
    /// <summary>The old app was not registered to start with Windows.</summary>
    NothingToMigrate,

    /// <summary>The old entry was replaced by one for the new app.</summary>
    Moved,

    /// <summary>The old entry was switched off in Task Manager, so it was removed and not re-created.</summary>
    RemovedDisabled,

    /// <summary>The new app was already registered; the old entry was removed.</summary>
    RemovedDuplicate,

    /// <summary>Reading or writing the registry failed; nothing was changed knowingly.</summary>
    Failed,
}

/// <summary>
/// Brings an existing user over from the app's old name, NxtFlyout, on the first start of NxtHUD:
/// copies the settings (and logs) from %AppData%\NxtFlyout to %AppData%\NxtHUD, and moves the
/// start-with-Windows entry to the new name.
/// </summary>
/// <remarks>
/// Runs before the settings are loaded. It never overwrites anything NxtHUD already has, never
/// deletes the old folder, and treats every failure as "start fresh" rather than a reason not
/// to start: each step logs what it did through the callback and swallows I/O errors.
/// </remarks>
public sealed class LegacyMigration
{
    /// <summary>The app's old name: its %AppData% folder and its Run value.</summary>
    public const string LegacyName = "NxtFlyout";

    private const string SettingsFileName = "settings.json";
    private const string LogsFolderName = "logs";

    private readonly IMigrationFileSystem _files;
    private readonly IStartupRegistry _registry;
    private readonly Action<string> _log;

    public LegacyMigration(IMigrationFileSystem files, IStartupRegistry registry, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(log);

        _files = files;
        _registry = registry;
        _log = log;
    }

    /// <summary>
    /// Copies settings.json, and the logs folder, from the old data folder when the new one has
    /// no settings yet.
    /// </summary>
    /// <param name="legacyDirectory">%AppData%\NxtFlyout</param>
    /// <param name="newDirectory">%AppData%\NxtHUD</param>
    public SettingsMigrationResult MigrateSettings(string legacyDirectory, string newDirectory)
    {
        var legacySettings = Path.Combine(legacyDirectory, SettingsFileName);
        var newSettings = Path.Combine(newDirectory, SettingsFileName);

        try
        {
            if (_files.FileExists(newSettings))
            {
                return SettingsMigrationResult.AlreadyMigrated;
            }

            if (!_files.FileExists(legacySettings))
            {
                return SettingsMigrationResult.NothingToMigrate;
            }

            _files.CreateDirectory(newDirectory);
            _files.CopyFile(legacySettings, newSettings);
            _log($"Copied the settings of {LegacyName} from {legacySettings} to {newSettings}.");
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            _log($"Copying the settings of {LegacyName} from {legacySettings} failed; starting with defaults. {ex.Message}");
            return SettingsMigrationResult.Failed;
        }

        CopyLogs(Path.Combine(legacyDirectory, LogsFolderName), Path.Combine(newDirectory, LogsFolderName));
        return SettingsMigrationResult.Copied;
    }

    /// <summary>
    /// Replaces the old "NxtFlyout" Run value with one for the new app, unless the user had
    /// switched it off in Task Manager (then it is only removed) or the new app is already registered.
    /// </summary>
    /// <param name="newValueName">The new Run value name ("NxtHUD").</param>
    /// <param name="newCommand">The command line that starts the running app at sign-in.</param>
    public AutostartMigrationResult MigrateAutostart(string newValueName, string newCommand)
    {
        try
        {
            if (_registry.GetRunCommand(LegacyName) is null)
            {
                return AutostartMigrationResult.NothingToMigrate;
            }

            var disabled = _registry.IsDisabledInTaskManager(LegacyName);
            var alreadyRegistered = _registry.GetRunCommand(newValueName) is not null;

            if (!disabled && !alreadyRegistered)
            {
                // Register the new entry first, so a failure half-way never leaves the user with neither.
                _registry.SetRunCommand(newValueName, newCommand);
            }

            _registry.DeleteEntry(LegacyName);

            if (disabled)
            {
                _log($"Removed the start-with-Windows entry of {LegacyName}; it was switched off in Task Manager, so {newValueName} was not registered.");
                return AutostartMigrationResult.RemovedDisabled;
            }

            if (alreadyRegistered)
            {
                _log($"Removed the old start-with-Windows entry of {LegacyName}; {newValueName} was already registered.");
                return AutostartMigrationResult.RemovedDuplicate;
            }

            _log($"Moved the start-with-Windows entry from {LegacyName} to {newValueName}.");
            return AutostartMigrationResult.Moved;
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            _log($"Moving the start-with-Windows entry of {LegacyName} failed. {ex.Message}");
            return AutostartMigrationResult.Failed;
        }
    }

    /// <summary>Best effort: a log that cannot be copied is not worth a warning to the user.</summary>
    private void CopyLogs(string legacyLogs, string newLogs)
    {
        try
        {
            if (!_files.DirectoryExists(legacyLogs))
            {
                return;
            }

            _files.CreateDirectory(newLogs);
            var copied = 0;
            foreach (var file in _files.GetFiles(legacyLogs))
            {
                var destination = Path.Combine(newLogs, Path.GetFileName(file));
                if (!_files.FileExists(destination))
                {
                    _files.CopyFile(file, destination);
                    copied++;
                }
            }

            _log($"Copied {copied} log file(s) of {LegacyName} to {newLogs}.");
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            _log($"Copying the logs of {LegacyName} failed; they stay in {legacyLogs}. {ex.Message}");
        }
    }

    private static bool IsIoFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException;
}
