using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NxtFlyout.Core.Settings;
using NxtFlyout.Core.Stats;

namespace NxtFlyout.Core.ViewModels;

/// <summary>
/// Backs the system stats panel on the taskbar: two lines of text (load figures, then transfer
/// rates) that refresh at the pace the user chose, and a click that opens Task Manager.
/// </summary>
/// <remarks>
/// The stats source only measures while the panel is switched on and has something to show,
/// and only the figures that are switched on, so the panel costs nothing when it is off.
/// </remarks>
public sealed class TaskbarStatsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ISystemStatsSource _source;

    private SystemStatsKinds _kinds;
    private int _intervalMs;
    private bool _refreshed;
    private bool _isVisible;
    private string _line1 = string.Empty;
    private string _line2 = string.Empty;

    /// <param name="settings">App settings.</param>
    /// <param name="source">Where the figures come from.</param>
    /// <param name="openDetails">Called when the panel is clicked (the app opens Task Manager).</param>
    public TaskbarStatsViewModel(ISettingsService settings, ISystemStatsSource source, Action openDetails)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(openDetails);

        _settings = settings;
        _source = source;
        OpenDetailsCommand = new RelayCommand(openDetails);

        _source.Updated += OnUpdated;
        _settings.Changed += OnSettingsChanged;
        Refresh();
    }

    public IRelayCommand OpenDetailsCommand { get; }

    /// <summary>True while the panel is switched on and at least one figure is chosen.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>Load figures, such as "CPU 13%  •  RAM 68%  •  GPU 5%". Empty when none are chosen.</summary>
    public string Line1
    {
        get => _line1;
        private set => SetProperty(ref _line1, value);
    }

    /// <summary>Transfer rates, such as "▲ 12 KB/s  ▼ 340 KB/s  •  Disk R 1.2 MB/s  W 0 KB/s". Empty when none are chosen.</summary>
    public string Line2
    {
        get => _line2;
        private set => SetProperty(ref _line2, value);
    }

    public void Dispose()
    {
        _source.Updated -= OnUpdated;
        _settings.Changed -= OnSettingsChanged;
        _source.SetActive(SystemStatsKinds.None, TimeSpan.Zero);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e) => Refresh();

    private void OnUpdated(object? sender, SystemStats stats)
    {
        if (!IsVisible)
        {
            return;
        }

        (Line1, Line2) = StatsFormatter.Format(stats, _kinds);
    }

    private void Refresh()
    {
        var settings = _settings.Current;
        var kinds = StatsFormatter.KindsFor(settings);
        var visible = settings.TaskbarStatsEnabled && kinds != SystemStatsKinds.None;
        var interval = settings.StatsIntervalMs;

        // Most settings changes have nothing to do with the panel: leave the figures alone.
        if (_refreshed && visible == IsVisible && kinds == _kinds && interval == _intervalMs)
        {
            return;
        }

        _refreshed = true;
        _kinds = kinds;
        _intervalMs = interval;
        IsVisible = visible;

        if (IsVisible)
        {
            // Show the labels straight away, with dashes until the first reading arrives.
            (Line1, Line2) = StatsFormatter.Format(new SystemStats(), _kinds);
            _source.SetActive(_kinds, TimeSpan.FromMilliseconds(interval));
        }
        else
        {
            _source.SetActive(SystemStatsKinds.None, TimeSpan.Zero);
            Line1 = string.Empty;
            Line2 = string.Empty;
        }
    }
}
