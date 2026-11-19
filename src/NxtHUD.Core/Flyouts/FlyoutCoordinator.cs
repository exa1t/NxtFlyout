using System.ComponentModel;
using NxtHUD.Core.ViewModels;

namespace NxtHUD.Core.Flyouts;

/// <summary>
/// Keeps flyouts from piling up: when one becomes visible, any other that is showing is
/// hidden, so two flyouts docked to the same corner never overlap.
/// </summary>
public sealed class FlyoutCoordinator : IDisposable
{
    private readonly IReadOnlyList<FlyoutViewModelBase> _flyouts;

    public FlyoutCoordinator(params FlyoutViewModelBase[] flyouts)
    {
        ArgumentNullException.ThrowIfNull(flyouts);

        _flyouts = flyouts.ToArray();
        foreach (var flyout in _flyouts)
        {
            flyout.PropertyChanged += OnFlyoutPropertyChanged;
        }
    }

    public bool IsAnyVisible => _flyouts.Any(flyout => flyout.IsVisible);

    public void Dispose()
    {
        foreach (var flyout in _flyouts)
        {
            flyout.PropertyChanged -= OnFlyoutPropertyChanged;
        }
    }

    private void OnFlyoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FlyoutViewModelBase.IsVisible)
            || sender is not FlyoutViewModelBase shown
            || !shown.IsVisible)
        {
            return;
        }

        foreach (var other in _flyouts)
        {
            if (!ReferenceEquals(other, shown) && other.IsVisible)
            {
                other.Hide();
            }
        }
    }
}
