using NxtFlyout.Core.Services;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Tests;

/// <summary>An <see cref="ISettingsService"/> that keeps everything in memory.</summary>
internal sealed class InMemorySettingsService : ISettingsService
{
    public AppSettings Current { get; private set; } = new();

    public int FlushCount { get; private set; }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void Update(Action<AppSettings> mutate)
    {
        var next = Current.Copy();
        mutate(next);
        Publish(next);
    }

    public void Replace(AppSettings settings) => Publish(settings.Copy());

    public void Flush() => FlushCount++;

    private void Publish(AppSettings next)
    {
        next.Normalize();
        if (next == Current)
        {
            return;
        }

        var previous = Current;
        Current = next;
        Changed?.Invoke(this, new SettingsChangedEventArgs(previous, next));
    }
}

/// <summary>An <see cref="IStartupService"/> that records calls and can be told to fail.</summary>
internal sealed class FakeStartupService : IStartupService
{
    public bool Enabled { get; set; }

    public Exception? FailWith { get; set; }

    public List<bool> SetEnabledCalls { get; } = new();

    public bool IsEnabled() => Enabled;

    public void SetEnabled(bool enabled)
    {
        SetEnabledCalls.Add(enabled);

        if (FailWith is not null)
        {
            throw FailWith;
        }

        Enabled = enabled;
    }
}

/// <summary>A fresh directory under the temp folder, deleted when disposed.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NxtFlyout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leaving a temp folder behind is harmless.
        }
    }
}
