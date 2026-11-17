using System.Windows.Controls;

namespace NxtFlyout.Views.Pages;

public partial class FlyoutsPage : Page
{
    public FlyoutsPage()
    {
        InitializeComponent();
        DataContext = App.Current.Services.SettingsViewModel;
    }
}
