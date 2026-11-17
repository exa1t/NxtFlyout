using System.Runtime.InteropServices;
using NxtFlyout.Core.Flyouts;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Interop;

/// <summary>A display's usable area in physical pixels and its scale factor (1.0 = 100 %).</summary>
internal readonly record struct DisplayInfo(ScreenRect WorkArea, ScreenRect Bounds, double Scale);

/// <summary>Finds displays and reads their work area and DPI scale.</summary>
internal static class ScreenHelper
{
    /// <summary>Returns the display flyouts should appear on for the given setting.</summary>
    public static DisplayInfo GetTargetDisplay(FlyoutDisplay display)
    {
        var monitor = IntPtr.Zero;

        switch (display)
        {
            case FlyoutDisplay.Cursor:
                if (NativeMethods.GetCursorPos(out var cursor))
                {
                    monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
                }

                break;

            case FlyoutDisplay.ActiveWindow:
                var foreground = NativeMethods.GetForegroundWindow();
                if (foreground != IntPtr.Zero)
                {
                    monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
                }

                break;
        }

        if (monitor == IntPtr.Zero)
        {
            monitor = GetPrimaryMonitor();
        }

        return GetDisplayInfo(monitor);
    }

    /// <summary>Returns the display a window is on.</summary>
    public static DisplayInfo GetDisplayOfWindow(IntPtr window)
    {
        return GetDisplayInfo(NativeMethods.MonitorFromWindow(window, NativeMethods.MONITOR_DEFAULTTONEAREST));
    }

    private static IntPtr GetPrimaryMonitor()
    {
        // The primary display is the one whose top-left corner is the origin of the desktop.
        return NativeMethods.MonitorFromPoint(default, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
    }

    private static DisplayInfo GetDisplayInfo(IntPtr monitor)
    {
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };

        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            // Should not happen; fall back to something sane rather than failing to show a flyout.
            var fallback = new ScreenRect(0, 0, 1920, 1080);
            return new DisplayInfo(fallback, fallback, 1.0);
        }

        var scale = 1.0;
        try
        {
            if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
            {
                scale = dpiX / 96.0;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // shcore.dll is always present on supported Windows versions; treat its absence as 100 %.
        }

        return new DisplayInfo(ToScreenRect(info.rcWork), ToScreenRect(info.rcMonitor), scale);
    }

    private static ScreenRect ToScreenRect(NativeMethods.RECT rect)
    {
        return new ScreenRect(rect.Left, rect.Top, rect.Width, rect.Height);
    }
}
