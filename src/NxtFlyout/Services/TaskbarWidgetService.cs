using System.ComponentModel;
using System.Windows.Interop;
using System.Windows.Threading;
using NxtFlyout.Core.Settings;
using NxtFlyout.Core.ViewModels;
using NxtFlyout.Interop;
using NxtFlyout.Views;

namespace NxtFlyout.Services;

/// <summary>
/// Shows the now-playing widget on the primary taskbar while the view model says it should
/// be visible, and keeps it in place.
/// </summary>
/// <remarks>
/// <para>The widget is a separate small window owned by the taskbar window. Windows always
/// keeps an owned window above its owner, which is what keeps the widget from disappearing
/// behind the taskbar when the taskbar is clicked.</para>
/// <para>Windows offers no API for adding content to the taskbar, so the widget does not know
/// about the taskbar's own buttons. A slow timer re-checks the taskbar's position and size
/// (it moves with display changes, auto-hide and Explorer restarts) and hides the widget
/// while a fullscreen app covers the taskbar.</para>
/// </remarks>
internal sealed class TaskbarWidgetService : IDisposable
{
    private const string TaskbarWindowClass = "Shell_TrayWnd";

    /// <summary>Gap between the widget and the left end of the taskbar, in device-independent pixels.</summary>
    private const double LeftGap = 8;

    /// <summary>Space left above and below the widget inside the taskbar, in device-independent pixels.</summary>
    private const double VerticalInset = 4;

    private readonly TaskbarWidgetViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer _timer;

    private TaskbarWidgetWindow? _window;
    private IntPtr _ownerTaskbar;
    private bool _started;

    public TaskbarWidgetService(TaskbarWidgetViewModel viewModel, ISettingsService settings, Dispatcher dispatcher)
    {
        _viewModel = viewModel;
        _settings = settings;

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(1500),
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _settings.Changed += OnSettingsChanged;
        Update();
    }

    public void Dispose()
    {
        if (_started)
        {
            _started = false;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _settings.Changed -= OnSettingsChanged;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        CloseWindow();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskbarWidgetViewModel.IsVisible))
        {
            Update();
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Previous.TaskbarWidgetOffset != e.Current.TaskbarWidgetOffset)
        {
            Update();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e) => Update();

    /// <summary>Brings the widget in line with the view model and the taskbar's current state.</summary>
    private void Update()
    {
        if (!_started || !_viewModel.IsVisible)
        {
            _timer.Stop();
            CloseWindow();
            return;
        }

        _timer.Start();

        try
        {
            Place();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ArgumentException)
        {
            // For example the taskbar window vanished mid-way because Explorer restarted.
            Log.Error("Placing the taskbar widget failed.", ex);
            CloseWindow();
        }
    }

    private void Place()
    {
        var taskbar = NativeMethods.FindWindow(TaskbarWindowClass, null);
        if (taskbar == IntPtr.Zero || !NativeMethods.GetWindowRect(taskbar, out var taskbarRect))
        {
            CloseWindow();
            return;
        }

        var display = ScreenHelper.GetDisplayOfWindow(taskbar);

        // Only a horizontal taskbar that is actually on screen has room for the widget:
        // not a vertical one, not one slid away by auto-hide, not one covered by a fullscreen app.
        var isHorizontal = taskbarRect.Width > taskbarRect.Height;
        var visibleHeight = Math.Min(taskbarRect.Bottom, display.Bounds.Bottom) - Math.Max(taskbarRect.Top, display.Bounds.Y);
        var isOnScreen = visibleHeight >= taskbarRect.Height / 2.0;

        if (!isHorizontal || !isOnScreen || FullscreenDetector.IsFullscreenAppActive())
        {
            HideWindow();
            return;
        }

        if (_window is not null && _ownerTaskbar != taskbar)
        {
            // Explorer restarted and made a new taskbar; the widget needs the new one as its owner.
            CloseWindow();
        }

        if (_window is null)
        {
            _window = new TaskbarWidgetWindow(_viewModel);
            _window.Closed += OnWindowClosed;
            _ownerTaskbar = taskbar;
            new WindowInteropHelper(_window).Owner = taskbar;
        }

        var scale = display.Scale;
        var height = Math.Max(24, (taskbarRect.Height / scale) - (2 * VerticalInset));
        if (Math.Abs(_window.Height - height) > 0.5)
        {
            _window.Height = height;
        }

        _window.SetTaskbarTheme(ThemeService.IsTaskbarDark());

        var handle = new WindowInteropHelper(_window).EnsureHandle();
        var x = taskbarRect.Left + ((LeftGap + _settings.Current.TaskbarWidgetOffset) * scale);
        var y = taskbarRect.Top + (VerticalInset * scale);
        NativeMethods.MoveWindowTo(handle, x, y);

        if (!_window.IsVisible)
        {
            _window.Show();
        }
    }

    private void HideWindow()
    {
        if (_window is not null && _window.IsVisible)
        {
            _window.Hide();
        }
    }

    private void CloseWindow()
    {
        var window = _window;
        if (window is null)
        {
            return;
        }

        _window = null;
        _ownerTaskbar = IntPtr.Zero;
        window.Closed -= OnWindowClosed;
        window.Close();
    }

    /// <summary>Windows destroys an owned window together with its owner (an Explorer restart).</summary>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
            _ownerTaskbar = IntPtr.Zero;
        }
    }
}
