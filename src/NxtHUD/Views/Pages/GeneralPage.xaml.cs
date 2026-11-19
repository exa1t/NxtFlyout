using System.Windows.Controls;

namespace NxtHUD.Views.Pages;

public partial class GeneralPage : Page
{
    public GeneralPage()
    {
        InitializeComponent();
        DataContext = App.Current.Services.SettingsViewModel;
    }
}
