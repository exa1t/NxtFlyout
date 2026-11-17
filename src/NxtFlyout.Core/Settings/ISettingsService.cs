namespace NxtFlyout.Core.Settings;

/// <summary>Owns the current <see cref="AppSettings"/> and persists changes.</summary>
public interface ISettingsService
{
    /// <summary>
    /// The current settings snapshot. Treat it as read-only: every change produces a new
    /// snapshot, so a reference you hold never changes underneath you.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Raised after the settings changed, on the thread that made the change.
    /// Not raised when an update leaves every value as it was.
    /// </summary>
    event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>
    /// Applies <paramref name="mutate"/> to a copy of the current settings, normalizes the
    /// result and, if anything changed, publishes it and schedules a save.
    /// </summary>
    void Update(Action<AppSettings> mutate);

    /// <summary>Replaces all settings at once (used by "reset to defaults").</summary>
    void Replace(AppSettings settings);

    /// <summary>Writes pending changes to disk now instead of waiting for the save delay.</summary>
    void Flush();
}

public sealed class SettingsChangedEventArgs : EventArgs
{
    public SettingsChangedEventArgs(AppSettings previous, AppSettings current)
    {
        Previous = previous;
        Current = current;
    }

    public AppSettings Previous { get; }

    public AppSettings Current { get; }
}

/// <summary>What <see cref="SettingsService.Load"/> found on disk.</summary>
public enum SettingsLoadResult
{
    /// <summary>The file existed and was read.</summary>
    Loaded,

    /// <summary>There was no file (first run); defaults are in use.</summary>
    CreatedDefaults,

    /// <summary>The file was not valid JSON. It was copied aside and defaults are in use.</summary>
    RecoveredFromCorruptFile,

    /// <summary>The file exists but could not be read (locked, access denied); defaults are in use.</summary>
    Unreadable,
}
