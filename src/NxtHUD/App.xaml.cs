using System.Windows;
using System.Windows.Threading;
using NxtHUD.Core.Localization;
using NxtHUD.Services;

namespace NxtHUD;

public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private AppHost? _host;

    /// <summary>The running application, typed.</summary>
    internal static new App Current => (App)Application.Current;

    /// <summary>The app's services. Available once start-up has completed.</summary>
    internal AppHost Services =>
        _host ?? throw new InvalidOperationException("The application has not finished starting.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance(AppInfo.InstanceId);
        if (!_singleInstance.IsFirstInstance)
        {
            // Another copy is already in the tray: ask it to show its settings, then leave.
            _singleInstance.SignalFirstInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = new AppHost(this, e.Args);
            _host.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Start-up failed.", ex);
            MessageBox.Show(
                Localizer.Instance.Get("Error_StartupFailed"),
                AppInfo.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _singleInstance.ListenForActivation(OnActivatedByAnotherLaunch);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Sign-out or shutdown: make sure the last change reaches the disk.
        _host?.TryFlushSettings();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        _host?.Dispose();
        _host = null;

        _singleInstance?.Dispose();
        _singleInstance = null;

        base.OnExit(e);
    }

    /// <summary>Runs on a thread-pool thread, so hop to the UI thread.</summary>
    private void OnActivatedByAnotherLaunch()
    {
        Dispatcher.InvokeAsync(() => _host?.ShowSettings());
    }

    /// <summary>A crash on a background thread cannot be stopped, but it can at least leave a trace.</summary>
    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled exception; the app is closing.", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error("Unobserved exception in a background task.", e.Exception);
        e.SetObserved();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A background utility should survive a failed UI callback rather than vanish from the tray.
        Log.Error("Unhandled exception on the UI thread.", e.Exception);
        e.Handled = true;
    }
}
