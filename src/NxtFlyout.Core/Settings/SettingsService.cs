using System.Text.Json;

namespace NxtFlyout.Core.Settings;

/// <summary>
/// Stores <see cref="AppSettings"/> as JSON in a single file.
/// </summary>
/// <remarks>
/// <para>Changes are published immediately but written to disk after a short delay, so
/// dragging a slider does not cause a write per tick. <see cref="Flush"/> and
/// <see cref="Dispose"/> write anything still pending.</para>
/// <para>Writes go to a temporary file that then replaces the real one, so a crash or
/// power loss mid-write cannot leave a half-written settings file behind.</para>
/// </remarks>
public sealed class SettingsService : ISettingsService, IDisposable
{
    public static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly TimeSpan _saveDelay;
    private readonly Timer _saveTimer;

    private AppSettings _current = new();
    private bool _dirty;
    private bool _disposed;

    /// <param name="filePath">Full path of the settings file. Its directory is created on first save.</param>
    /// <param name="saveDelay">
    /// How long to wait after a change before writing. <see cref="TimeSpan.Zero"/> writes
    /// synchronously on every change. Defaults to <see cref="DefaultSaveDelay"/>.
    /// </param>
    public SettingsService(string filePath, TimeSpan? saveDelay = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var delay = saveDelay ?? DefaultSaveDelay;
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(saveDelay), "The save delay cannot be negative.");
        }

        _filePath = Path.GetFullPath(filePath);
        _saveDelay = delay;
        _saveTimer = new Timer(OnSaveTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public string FilePath => _filePath;

    /// <summary>Where a settings file that failed to parse is copied before defaults take over.</summary>
    public string CorruptBackupPath => Path.ChangeExtension(_filePath, ".corrupt.json");

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>
    /// Raised when a background save fails (disk full, file locked, access denied).
    /// May be raised on a thread-pool thread. The change stays pending and is retried on
    /// the next change or <see cref="Flush"/>.
    /// </summary>
    public event EventHandler<Exception>? SaveFailed;

    /// <summary>
    /// Reads the settings file. Never throws for a missing, unreadable or malformed file:
    /// defaults are used instead and the return value says what happened.
    /// </summary>
    public SettingsLoadResult Load()
    {
        AppSettings previous;
        AppSettings loaded;
        SettingsLoadResult result;

        lock (_gate)
        {
            ThrowIfDisposed();

            previous = _current;
            (loaded, result) = ReadFromDisk();
            loaded.Normalize();

            _current = loaded;
            _dirty = false;
            _saveTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        if (loaded != previous)
        {
            Changed?.Invoke(this, new SettingsChangedEventArgs(previous, loaded));
        }

        return result;
    }

    public void Update(Action<AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        Commit(current =>
        {
            var next = current.Copy();
            mutate(next);
            return next;
        });
    }

    public void Replace(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Commit(_ => settings.Copy());
    }

    /// <summary>Writes the current settings to disk now, whether or not anything is pending.</summary>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be written.</exception>
    public void Save()
    {
        lock (_gate)
        {
            WriteToDisk(_current);
            _dirty = false;
        }
    }

    /// <summary>Writes pending changes to disk now. Does nothing if there are none.</summary>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be written.</exception>
    public void Flush()
    {
        lock (_gate)
        {
            if (!_dirty)
            {
                return;
            }

            WriteToDisk(_current);
            _dirty = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _saveTimer.Dispose();
        FlushAndReportFailure();
    }

    private void Commit(Func<AppSettings, AppSettings> produce)
    {
        AppSettings previous;
        AppSettings next;
        var saveNow = false;

        lock (_gate)
        {
            ThrowIfDisposed();

            previous = _current;
            next = produce(previous);
            next.Normalize();

            if (next == previous)
            {
                return;
            }

            _current = next;
            _dirty = true;

            if (_saveDelay == TimeSpan.Zero)
            {
                saveNow = true;
            }
            else
            {
                _saveTimer.Change(_saveDelay, Timeout.InfiniteTimeSpan);
            }
        }

        if (saveNow)
        {
            FlushAndReportFailure();
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, next));
    }

    private void OnSaveTimer(object? state) => FlushAndReportFailure();

    private void FlushAndReportFailure()
    {
        try
        {
            Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveFailed?.Invoke(this, ex);
        }
    }

    private (AppSettings Settings, SettingsLoadResult Result) ReadFromDisk()
    {
        if (!File.Exists(_filePath))
        {
            return (new AppSettings(), SettingsLoadResult.CreatedDefaults);
        }

        try
        {
            // ReadAllText strips a UTF-8 byte-order mark, which editors such as Notepad may add.
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is not null)
            {
                return (settings, SettingsLoadResult.Loaded);
            }
        }
        catch (JsonException)
        {
            // Fall through to the corrupt-file path below.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (new AppSettings(), SettingsLoadResult.Unreadable);
        }

        BackUpCorruptFile();
        return (new AppSettings(), SettingsLoadResult.RecoveredFromCorruptFile);
    }

    private void BackUpCorruptFile()
    {
        try
        {
            File.Copy(_filePath, CorruptBackupPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The backup is a courtesy; failing to make it must not stop the app from starting.
        }
    }

    private void WriteToDisk(AppSettings snapshot)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new TolerantEnumConverterFactory());
        return options;
    }
}
