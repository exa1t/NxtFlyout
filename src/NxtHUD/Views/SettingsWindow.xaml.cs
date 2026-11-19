using System.Windows;
using NxtHUD.Views.Pages;

namespace NxtHUD.Views;

/// <summary>The settings window: a Fluent navigation view with one page per area.</summary>
public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        RootNavigation.Navigate(typeof(GeneralPage));
    }
}
