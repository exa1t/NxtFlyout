using System.Windows.Controls;

namespace NxtFlyout.Views.Pages;

public partial class AppearancePage : Page
{
    public AppearancePage()
    {
        InitializeComponent();
        DataContext = App.Current.Services.SettingsViewModel;
    }
}
