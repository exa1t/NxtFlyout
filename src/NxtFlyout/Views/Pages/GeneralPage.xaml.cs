using System.Windows.Controls;

namespace NxtFlyout.Views.Pages;

public partial class GeneralPage : Page
{
    public GeneralPage()
    {
        InitializeComponent();
        DataContext = App.Current.Services.SettingsViewModel;
    }
}
