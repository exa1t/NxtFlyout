using System.Windows.Automation;
using NxtFlyout.Core.Flyouts;

namespace NxtFlyout.Services;

/// <summary>
/// Finds where the taskbar's own buttons are (Start, Search, Widgets, app icons, the tray and
/// clock) through UI Automation, so the taskbar widget can stay out of their way.
/// </summary>
/// <remarks>
/// <para>Windows has no API for the layout of the taskbar, but the taskbar describes its buttons
/// to accessibility tools, with their positions on screen. That works for the Windows 10 and
/// Windows 11 taskbars alike.</para>
/// <para>A scan crosses into Explorer's process and takes tens of milliseconds, so it must not
/// run on the UI thread. It must also not run there because the widget is owned by the taskbar
/// and shows up in the same tree: UI Automation would call back into our own UI thread.</para>
/// </remarks>
internal static class TaskbarButtonScanner
{
    private static bool s_loggedFailure;

    /// <summary>
    /// Returns the horizontal extent of every taskbar button, in physical pixels, or
    /// <see langword="null"/> when the taskbar could not be read.
    /// </summary>
    public static IReadOnlyList<TaskbarSpan>? Scan(IntPtr taskbar, double taskbarTop, double taskbarBottom, double taskbarWidth)
    {
        try
        {
            var root = AutomationElement.FromHandle(taskbar);

            var cache = new CacheRequest
            {
                AutomationElementMode = AutomationElementMode.None,
                TreeScope = TreeScope.Element,
            };
            cache.Add(AutomationElement.BoundingRectangleProperty);
            cache.Add(AutomationElement.ProcessIdProperty);

            var isButton = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);

            AutomationElementCollection found;
            using (cache.Activate())
            {
                found = root.FindAll(TreeScope.Descendants, isButton);
            }

            var ownProcess = Environment.ProcessId;
            var spans = new List<TaskbarSpan>(found.Count);
            foreach (AutomationElement element in found)
            {
                // The widget is owned by the taskbar, so its own buttons are in this tree too.
                if (element.GetCachedPropertyValue(AutomationElement.ProcessIdProperty) is int processId
                    && processId == ownProcess)
                {
                    continue;
                }

                if (element.GetCachedPropertyValue(AutomationElement.BoundingRectangleProperty) is not System.Windows.Rect rect
                    || rect.IsEmpty
                    || rect.Width <= 0
                    || rect.Width > taskbarWidth / 2
                    || rect.Bottom <= taskbarTop
                    || rect.Top >= taskbarBottom)
                {
                    // Off screen, a container rather than a button, or not on this taskbar.
                    continue;
                }

                spans.Add(new TaskbarSpan(rect.Left, rect.Right));
            }

            s_loggedFailure = false;
            return spans.Count == 0 ? null : spans;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException or TimeoutException)
        {
            // Typically Explorer restarting. Log once, not every 1.5 seconds while it lasts.
            if (!s_loggedFailure)
            {
                s_loggedFailure = true;
                Log.Error("Reading the taskbar buttons failed; the taskbar widget keeps its last known room.", ex);
            }

            return null;
        }
    }
}
