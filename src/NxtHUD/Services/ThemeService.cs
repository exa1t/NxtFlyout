using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using NxtHUD.Core.Settings;
using Wpf.Ui.Appearance;
using WpfUi = Wpf.Ui.Controls;

namespace NxtHUD.Services;

/// <summary>
/// Applies the theme, backdrop and accent colour chosen in settings, and keeps following
/// Windows live: switching Windows between light and dark or picking a new accent colour
/// restyles the app without a restart.
/// </summary>
/// <remarks>
/// The app usually has no window open, so this listens for the system-wide change
/// notification itself instead of relying on a per-window watcher.
/// </remarks>
internal sealed class ThemeService : IDisposable
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly ISettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _systemChangeDebounce;
    private bool _watching;

    public ThemeService(ISettingsService settings, Dispatcher dispatcher)
    {
        _settings = settings;
        _dispatcher = dispatcher;

        // Windows sends a burst of notifications for one theme change; react once, after it settles.
        _systemChangeDebounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _systemChangeDebounce.Tick += OnSystemChangeDebounceTick;
    }

    /// <summary>The theme currently applied (never "follow system": already resolved).</summary>
    public ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Dark;

    public WpfUi.WindowBackdropType CurrentBackdrop { get; private set; } = WpfUi.WindowBackdropType.Mica;

    /// <summary>Whether the app is currently using its dark theme.</summary>
    public bool IsDark => CurrentTheme == ApplicationTheme.Dark
        || (CurrentTheme == ApplicationTheme.HighContrast && IsWindowsAppModeDark());

    /// <summary>
    /// Whether the taskbar is dark. Windows has a separate light/dark mode for the taskbar and
    /// Start, so this can differ from the app theme.
    /// </summary>
    public static bool IsTaskbarDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);

            // Missing value: Windows defaults to a dark taskbar.
            return key?.GetValue("SystemUsesLightTheme") is not int systemUsesLightTheme || systemUsesLightTheme == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return true;
        }
    }

    /// <summary>Applies the theme and starts following Windows theme and accent changes.</summary>
    public void Start()
    {
        Apply();

        if (!_watching)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _watching = true;
        }
    }

    /// <summary>Applies the current settings to the app resources and every open window.</summary>
    public void Apply()
    {
        var settings = _settings.Current;
        CurrentTheme = ResolveTheme(settings.Theme);
        CurrentBackdrop = ToBackdropType(settings.Backdrop);

        // Swaps the light/dark resource dictionaries and refreshes the accent colour from Windows.
        ApplicationThemeManager.Apply(CurrentTheme, CurrentBackdrop, true);

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTo(window);
        }
    }

    /// <summary>
    /// Gives a window the current backdrop and a light or dark frame. Call it before showing
    /// a new window (so it opens with the right backdrop) and again once it is on screen.
    /// </summary>
    public void ApplyTo(Window window)
    {
        if (window is not WpfUi.FluentWindow fluentWindow)
        {
            return;
        }

        if (fluentWindow.WindowBackdropType != CurrentBackdrop)
        {
            fluentWindow.WindowBackdropType = CurrentBackdrop;
        }

        if (new WindowInteropHelper(window).Handle == IntPtr.Zero)
        {
            // Not on screen yet: FluentWindow applies the backdrop itself when its handle is created.
            return;
        }

        if (CurrentBackdrop == WpfUi.WindowBackdropType.None)
        {
            // No material behind the window: paint the themed background, and keep it tied to the
            // theme resource so it follows a later light/dark switch.
            window.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ApplicationBackgroundBrush");

            if (CurrentTheme == ApplicationTheme.Dark)
            {
                WindowBackgroundManager.ApplyDarkThemeToWindow(window);
            }
            else
            {
                WindowBackgroundManager.RemoveDarkThemeFromWindow(window);
            }
        }
        else
        {
            // Re-applies the material and switches the frame (and so the Mica tint) between light and dark.
            WindowBackgroundManager.UpdateBackground(window, CurrentTheme, CurrentBackdrop);
        }
    }

    public void Dispose()
    {
        if (_watching)
        {
            // SystemEvents is static: failing to unsubscribe would keep this object alive.
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _watching = false;
        }

        _systemChangeDebounce.Stop();
        _systemChangeDebounce.Tick -= OnSystemChangeDebounceTick;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General
            or UserPreferenceCategory.Color
            or UserPreferenceCategory.VisualStyle
            or UserPreferenceCategory.Accessibility)
        {
            _dispatcher.InvokeAsync(RestartDebounce);
        }
    }

    private void RestartDebounce()
    {
        _systemChangeDebounce.Stop();
        _systemChangeDebounce.Start();
    }

    private void OnSystemChangeDebounceTick(object? sender, EventArgs e)
    {
        _systemChangeDebounce.Stop();
        Apply();
    }

    private static ApplicationTheme ResolveTheme(AppTheme theme)
    {
        if (SystemParameters.HighContrast)
        {
            return ApplicationTheme.HighContrast;
        }

        return theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => IsWindowsAppModeDark() ? ApplicationTheme.Dark : ApplicationTheme.Light,
        };
    }

    private static WpfUi.WindowBackdropType ToBackdropType(BackdropKind backdrop)
    {
        return backdrop switch
        {
            BackdropKind.Acrylic => WpfUi.WindowBackdropType.Acrylic,
            BackdropKind.None => WpfUi.WindowBackdropType.None,

            // The settings window is a normal window; give it Mica where Windows allows it.
            BackdropKind.Translucent => WpfUi.WindowBackdropType.Mica,
            _ => WpfUi.WindowBackdropType.Mica,
        };
    }

    /// <summary>Reads "Choose your mode" for apps from Windows personalization settings.</summary>
    private static bool IsWindowsAppModeDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            return key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme && appsUseLightTheme == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }
}
