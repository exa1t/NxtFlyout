using NxtHUD.Core.Flyouts;
using NxtHUD.Core.ViewModels;

namespace NxtHUD.Views;

/// <summary>The volume flyout: speaker icon, level bar and percentage.</summary>
public partial class VolumeFlyoutWindow : FlyoutWindowBase
{
    private const double CardWidth = 300;

    private readonly VolumeFlyoutViewModel _viewModel;

    public VolumeFlyoutWindow(VolumeFlyoutViewModel viewModel, FlyoutWindowContext context)
    {
        _viewModel = viewModel;

        Configure(viewModel, context, CardWidth, settings => FlyoutPlacement.Split(settings.VolumePosition));
        InitializeComponent();
        BuildChrome();
    }

    /// <summary>
    /// Re-reads the volume ten times a second while visible, so the bar follows a held key
    /// and changes made elsewhere (the taskbar slider, another app).
    /// </summary>
    protected override TimeSpan? TickInterval => TimeSpan.FromMilliseconds(100);

    protected override void OnTick() => _viewModel.Refresh();
}

/// <summary>The lock keys flyout: which of Caps/Num/Scroll Lock was toggled, and its new state.</summary>
public partial class LockKeysFlyoutWindow : FlyoutWindowBase
{
    private const double CardWidth = 240;

    public LockKeysFlyoutWindow(LockKeysFlyoutViewModel viewModel, FlyoutWindowContext context)
    {
        Configure(viewModel, context, CardWidth, settings => FlyoutPlacement.Split(settings.LockKeysPosition));
        InitializeComponent();
        BuildChrome();
    }
}

/// <summary>The "Up Next" flyout: a brief announcement of the track that just started.</summary>
public partial class UpNextFlyoutWindow : FlyoutWindowBase
{
    private const double CardWidth = 300;

    public UpNextFlyoutWindow(UpNextFlyoutViewModel viewModel, FlyoutWindowContext context)
    {
        Configure(viewModel, context, CardWidth, settings => FlyoutPlacement.Split(settings.UpNextPosition));
        InitializeComponent();
        BuildChrome();
    }
}
