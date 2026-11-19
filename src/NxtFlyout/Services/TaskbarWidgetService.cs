using System.ComponentModel;
using System.Windows.Interop;
using System.Windows.Threading;
using NxtFlyout.Core.Flyouts;
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

    /// <summary>Widest the widget gets even on an empty taskbar, in device-independent pixels.</summary>
    private const double MaxWidth = 480;

    private readonly TaskbarWidgetViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;

    private TaskbarWidgetWindow? _window;
    private IntPtr _ownerTaskbar;
    private bool _started;

    /// <summary>Where the taskbar's own buttons were at the last scan, in physical pixels.</summary>
    private IReadOnlyList<TaskbarSpan>? _buttons;
    private bool _scanning;

    public TaskbarWidgetService(TaskbarWidgetViewModel viewModel, ISettingsService settings, Dispatcher dispatcher)
    {
        _viewModel = viewModel;
        _settings = settings;
        _dispatcher = dispatcher;

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
        switch (e.PropertyName)
        {
            case nameof(TaskbarWidgetViewModel.IsVisible):
            // These change the widget's width, so the layout may have to step up or down.
            case nameof(TaskbarWidgetViewModel.Title):
            case nameof(TaskbarWidgetViewModel.Artist):
            case nameof(TaskbarWidgetViewModel.ShowVisualizer):
            case nameof(TaskbarWidgetViewModel.VisualizerBarCount):
                Update();
                break;
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Previous.TaskbarWidgetOffset != e.Current.TaskbarWidgetOffset)
        {
            Update();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        ScanButtons();
        Update();
    }

    /// <summary>
    /// Re-reads where the taskbar's buttons are, on a worker thread, and re-places the widget if
    /// they moved (an app was opened or closed, or the alignment changed).
    /// </summary>
    private void ScanButtons()
    {
        if (_scanning)
        {
            return;
        }

        var taskbar = NativeMethods.FindWindow(TaskbarWindowClass, null);
        if (taskbar == IntPtr.Zero || !NativeMethods.GetWindowRect(taskbar, out var rect))
        {
            return;
        }

        _scanning = true;
        Task.Run(() => TaskbarButtonScanner.Scan(taskbar, rect.Top, rect.Bottom, rect.Width))
            .ContinueWith(
                task => _dispatcher.InvokeAsync(() =>
                {
                    _scanning = false;

                    if (task.Exception is { } error)
                    {
                        Log.Error("Reading the taskbar buttons failed unexpectedly.", error.GetBaseException());
                        return;
                    }

                    // A failed scan keeps the last known layout rather than dropping the limit.
                    if (task.Result is { } buttons && (_buttons is null || !buttons.SequenceEqual(_buttons)))
                    {
                        _buttons = buttons;
                        Update();
                    }
                }),
                TaskScheduler.Default);
    }

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
        if (_buttons is null)
        {
            // Showing for the first time: find the taskbar's buttons now rather than on the next tick.
            ScanButtons();
        }

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

        // Fit between the taskbar's own buttons when we know where they are. Until the first
        // scan comes back, or if the taskbar cannot be read, behave as before: no limit.
        var room = MaxWidth;
        if (_buttons is { } buttons)
        {
            var span = TaskbarWidgetFit.FindFreeSpan(taskbarRect.Left, taskbarRect.Right, x, buttons, LeftGap * scale);
            if (span is null)
            {
                HideWindow();
                return;
            }

            x = span.Value.Left;
            room = Math.Min(MaxWidth, span.Value.Width / scale);
        }

        var layout = TaskbarWidgetFit.ChooseLayout(room, _window.MeasureLayouts());
        if (layout is null)
        {
            HideWindow();
            return;
        }

        _window.ApplyLayout(layout.Value);

        // A hard limit as well, so a longer title arriving between two checks cannot spill over.
        if (Math.Abs(_window.MaxWidth - room) > 0.5)
        {
            _window.MaxWidth = room;
        }

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
