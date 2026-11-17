using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using NxtFlyout.Core.ViewModels;
using NxtFlyout.Interop;

namespace NxtFlyout.Views;

/// <summary>
/// The compact now-playing widget that sits on the taskbar. TaskbarWidgetService creates,
/// positions and closes it; this class only holds the visuals.
/// </summary>
public partial class TaskbarWidgetWindow : Window
{
    public TaskbarWidgetWindow(TaskbarWidgetViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Uses light text on a dark taskbar and dark text on a light one.</summary>
    public void SetTaskbarTheme(bool isDark)
    {
        var brush = isDark ? Brushes.White : Brushes.Black;
        if (!ReferenceEquals(Foreground, brush))
        {
            Foreground = brush;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Never take focus, and stay out of Alt+Tab.
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.AddExtendedStyles(handle, NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
    }
}
