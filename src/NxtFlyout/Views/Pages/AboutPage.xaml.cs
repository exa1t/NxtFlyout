using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace NxtFlyout.Views.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        DataContext = App.Current.Services.SettingsViewModel;
    }

    private void OnOpenSettingsFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);

            // Shell-executing a folder path opens it in File Explorer.
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = AppInfo.DataDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Error("Opening the settings folder failed.", ex);
        }
    }
}
