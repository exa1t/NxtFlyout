using CommunityToolkit.Mvvm.ComponentModel;
using NxtHUD.Core.Media;
using NxtHUD.Core.Settings;

namespace NxtHUD.Core.ViewModels;

/// <summary>
/// What every flyout has in common: whether it is visible, the auto-hide countdown, staying
/// open while hovered, and the "is it enabled / is a fullscreen app in the way" checks.
/// </summary>
/// <remarks>All members must be used from the UI thread.</remarks>
public abstract class FlyoutViewModelBase : ObservableObject, IDisposable
{
    private readonly IDelayTimer _hideTimer;
    private readonly Func<bool> _isFullscreenAppActive;

    private bool _pointerOver;
    private bool _isVisible;
    private bool _disposed;

    /// <param name="settings">App settings.</param>
    /// <param name="hideTimer">Timer that drives auto-hide.</param>
    /// <param name="isFullscreenAppActive">
    /// Returns true while a fullscreen game or app is in the foreground; null means "never".
    /// </param>
    protected FlyoutViewModelBase(ISettingsService settings, IDelayTimer hideTimer, Func<bool>? isFullscreenAppActive)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(hideTimer);

        Settings = settings;
        _hideTimer = hideTimer;
        _isFullscreenAppActive = isFullscreenAppActive ?? (() => false);

        Settings.Changed += HandleSettingsChanged;
        _hideTimer.Elapsed += HandleHideTimerElapsed;
    }

    /// <summary>Whether the flyout should be on screen. The window animates in and out as this changes.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    protected ISettingsService Settings { get; }

    /// <summary>Whether the user has this flyout switched on.</summary>
    protected abstract bool IsEnabled(AppSettings settings);

    /// <summary>Whether there is anything to show right now.</summary>
    protected virtual bool HasContent => true;

    /// <summary>Shows the flyout and (re)starts the auto-hide countdown, with no checks.</summary>
    public void Show()
    {
        OnShowing();
        IsVisible = true;

        if (!_pointerOver)
        {
            RestartHideTimer();
        }
    }

    /// <summary>Hides the flyout now.</summary>
    public void Hide()
    {
        _hideTimer.Stop();
        IsVisible = false;
    }

    /// <summary>Call when the pointer moves onto the flyout: it stays open while hovered.</summary>
    public void OnPointerEntered()
    {
        _pointerOver = true;
        _hideTimer.Stop();
    }

    /// <summary>Call when the pointer leaves the flyout: the countdown starts over.</summary>
    public void OnPointerExited()
    {
        _pointerOver = false;

        if (IsVisible)
        {
            RestartHideTimer();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Settings.Changed -= HandleSettingsChanged;
        _hideTimer.Elapsed -= HandleHideTimerElapsed;
        _hideTimer.Stop();
        OnDisposed();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Shows the flyout if it is enabled, has content and no fullscreen app is in the way.
    /// Returns whether it was shown.
    /// </summary>
    protected bool TryShow()
    {
        var settings = Settings.Current;
        if (!IsEnabled(settings) || !HasContent)
        {
            return false;
        }

        if (settings.DisableInFullscreen && _isFullscreenAppActive())
        {
            return false;
        }

        Show();
        return true;
    }

    /// <summary>Called just before the flyout becomes visible; refresh anything time-sensitive here.</summary>
    protected virtual void OnShowing()
    {
    }

    /// <summary>Called when settings change, before the base class hides a flyout that was switched off.</summary>
    protected virtual void OnSettingsChanged(SettingsChangedEventArgs e)
    {
    }

    /// <summary>Called once from <see cref="Dispose"/>; unsubscribe from services here.</summary>
    protected virtual void OnDisposed()
    {
    }

    private void RestartHideTimer()
    {
        _hideTimer.Start(TimeSpan.FromMilliseconds(Settings.Current.FlyoutTimeoutMs));
    }

    private void HandleSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        OnSettingsChanged(e);

        if (!IsEnabled(e.Current) && IsVisible)
        {
            Hide();
        }
    }

    private void HandleHideTimerElapsed(object? sender, EventArgs e)
    {
        if (!_pointerOver)
        {
            IsVisible = false;
        }
    }
}
