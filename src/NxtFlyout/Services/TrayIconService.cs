using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using NxtFlyout.Core.Localization;
using NxtFlyout.Interop;
using WinForms = System.Windows.Forms;
using WpfUi = Wpf.Ui.Controls;

namespace NxtFlyout.Services;

/// <summary>
/// The notification-area (tray) icon and its menu.
/// </summary>
/// <remarks>
/// The icon itself is a Windows Forms <see cref="WinForms.NotifyIcon"/>, but the menu is a
/// WPF <see cref="ContextMenu"/> so it picks up the Fluent styling, theme and accent colour.
/// </remarks>
internal sealed class TrayIconService : IDisposable
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/NxtFlyout.ico");

    private readonly System.Drawing.Icon _icon;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private ContextMenu? _menu;
    private Window? _menuOwner;
    private bool _disposed;

    public TrayIconService()
    {
        _icon = LoadIcon();
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _icon,
            Text = AppInfo.Name,
            Visible = false,
        };
        _notifyIcon.MouseUp += OnMouseUp;
    }

    /// <summary>The user clicked the icon or chose "Settings".</summary>
    public event EventHandler? OpenSettingsRequested;

    /// <summary>The user chose "Quit".</summary>
    public event EventHandler? QuitRequested;

    public void Show() => _notifyIcon.Visible = true;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notifyIcon.MouseUp -= OnMouseUp;

        // Hide before disposing, otherwise a ghost icon lingers until the mouse moves over it.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();

        if (_menu is not null)
        {
            _menu.IsOpen = false;
        }

        _menuOwner?.Close();
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(IconUri)
            ?? throw new InvalidOperationException("The tray icon resource is missing.");

        using var stream = resource.Stream;

        // Picks the frame closest to the tray icon size for the current DPI.
        return new System.Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    private void OnMouseUp(object? sender, WinForms.MouseEventArgs e)
    {
        switch (e.Button)
        {
            case WinForms.MouseButtons.Left:
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                break;

            case WinForms.MouseButtons.Right:
                ShowMenu();
                break;
        }
    }

    private void ShowMenu()
    {
        _menu ??= BuildMenu();
        _menuOwner ??= CreateMenuOwner();

        // A popup only closes when the user clicks elsewhere if its thread owns the foreground
        // window, so bring an invisible helper window to the foreground first.
        NativeMethods.SetForegroundWindow(new WindowInteropHelper(_menuOwner).Handle);

        _menu.IsOpen = true;
    }

    private ContextMenu BuildMenu()
    {
        var settingsItem = CreateMenuItem("Tray_Settings", WpfUi.SymbolRegular.Settings24);
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

        var quitItem = CreateMenuItem("Tray_Quit", WpfUi.SymbolRegular.Dismiss24);
        quitItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenu
        {
            Placement = PlacementMode.MousePoint,
        };
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);
        return menu;
    }

    private static MenuItem CreateMenuItem(string textKey, WpfUi.SymbolRegular symbol)
    {
        var item = new MenuItem
        {
            Icon = new WpfUi.SymbolIcon { Symbol = symbol },
        };

        // Bound rather than assigned, so the text follows a language change.
        item.SetBinding(
            HeaderedItemsControl.HeaderProperty,
            new Binding($"[{textKey}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay });

        return item;
    }

    /// <summary>Creates a window that is never shown; it only lends its handle to the menu.</summary>
    private Window CreateMenuOwner()
    {
        var window = new Window
        {
            Title = AppInfo.Name,
            Width = 0,
            Height = 0,
            Left = -32000,
            Top = -32000,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
        };

        // WPF closes every window at shutdown; forget this one then so Dispose does not close it twice.
        window.Closed += (_, _) => _menuOwner = null;

        new WindowInteropHelper(window).EnsureHandle();

        // WPF treats the first window created as the app's main window. This helper must not
        // take that role, or theme updates aimed at the main window would land on it.
        if (ReferenceEquals(Application.Current.MainWindow, window))
        {
            Application.Current.MainWindow = null;
        }

        return window;
    }
}
