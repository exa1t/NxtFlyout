using System.Windows.Controls;

namespace NxtHUD.Views.Pages;

public partial class FlyoutsPage : Page
{
    public FlyoutsPage()
    {
        InitializeComponent();
        DataContext = App.Current.Services.SettingsViewModel;
    }
}
