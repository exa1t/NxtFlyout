using NxtHUD.Core.ViewModels;

namespace NxtHUD.Views;

/// <summary>The media flyout: album art, track details, a live timeline and transport buttons.</summary>
public partial class MediaFlyoutWindow : FlyoutWindowBase
{
    private const double CardWidth = 340;

    private readonly MediaFlyoutViewModel _viewModel;

    public MediaFlyoutWindow(MediaFlyoutViewModel viewModel, FlyoutWindowContext context)
    {
        _viewModel = viewModel;

        Configure(viewModel, context, CardWidth, settings => (settings.VerticalPosition, settings.HorizontalPosition));
        InitializeComponent();
        BuildChrome();
    }

    /// <summary>Advances the progress bar twice a second while the flyout is on screen.</summary>
    protected override TimeSpan? TickInterval => TimeSpan.FromMilliseconds(500);

    protected override void OnTick() => _viewModel.RefreshPosition();
}
