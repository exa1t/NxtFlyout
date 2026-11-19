using System.Windows.Threading;
using NxtHUD.Core.Media;
using NxtHUD.Interop;

namespace NxtHUD.Services;

/// <summary>A restartable one-shot timer that fires on the UI thread.</summary>
internal sealed class DispatcherDelayTimer : IDelayTimer
{
    private readonly DispatcherTimer _timer;

    public DispatcherDelayTimer(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
        _timer.Tick += OnTick;
    }

    public event EventHandler? Elapsed;

    public void Start(TimeSpan delay)
    {
        _timer.Stop();
        _timer.Interval = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        Elapsed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Tells whether a fullscreen game or app currently owns the screen.</summary>
internal static class FullscreenDetector
{
    /// <summary>
    /// Asks the shell, which already tracks this to decide when to hold back notifications.
    /// Covers exclusive-fullscreen games and borderless fullscreen windows.
    /// </summary>
    public static bool IsFullscreenAppActive()
    {
        var result = NativeMethods.SHQueryUserNotificationState(out var state);
        return result == 0
            && (state == NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN || state == NativeMethods.QUNS_BUSY);
    }
}
