using NxtFlyout.Core.Media;

namespace NxtFlyout.Tests;

/// <summary>An <see cref="IMediaService"/> whose current session the test sets by hand.</summary>
internal sealed class FakeMediaService : IMediaService
{
    public MediaSnapshot? Current { get; private set; }

    public int TogglePlayPauseCalls { get; private set; }

    public int SkipNextCalls { get; private set; }

    public int SkipPreviousCalls { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Replaces the current session and announces the change, like the real service.</summary>
    public void Set(MediaSnapshot? snapshot)
    {
        Current = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task TogglePlayPauseAsync()
    {
        TogglePlayPauseCalls++;
        return Task.CompletedTask;
    }

    public Task SkipNextAsync()
    {
        SkipNextCalls++;
        return Task.CompletedTask;
    }

    public Task SkipPreviousAsync()
    {
        SkipPreviousCalls++;
        return Task.CompletedTask;
    }
}

/// <summary>An <see cref="IDelayTimer"/> that only fires when the test says so.</summary>
internal sealed class FakeDelayTimer : IDelayTimer
{
    public bool IsRunning { get; private set; }

    public TimeSpan LastDelay { get; private set; }

    public int StartCount { get; private set; }

    public event EventHandler? Elapsed;

    public void Start(TimeSpan delay)
    {
        IsRunning = true;
        LastDelay = delay;
        StartCount++;
    }

    public void Stop() => IsRunning = false;

    /// <summary>Simulates the countdown reaching zero. Does nothing if the timer is not running.</summary>
    public void Fire()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        Elapsed?.Invoke(this, EventArgs.Empty);
    }
}
