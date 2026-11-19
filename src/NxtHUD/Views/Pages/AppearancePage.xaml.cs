using System.Windows.Controls;

namespace NxtHUD.Views.Pages;

public partial class AppearancePage : Page
{
    public AppearancePage()
    {
        InitializeComponent();
        DataContext = App.Current.Services.SettingsViewModel;
    }
}
