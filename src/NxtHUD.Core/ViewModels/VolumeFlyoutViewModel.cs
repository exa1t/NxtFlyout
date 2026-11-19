using NxtHUD.Core.Audio;
using NxtHUD.Core.Media;
using NxtHUD.Core.Settings;

namespace NxtHUD.Core.ViewModels;

/// <summary>Backs the volume flyout: the current level, mute state and a matching speaker icon.</summary>
public sealed class VolumeFlyoutViewModel : FlyoutViewModelBase
{
    // Speaker glyphs in Segoe Fluent Icons / Segoe MDL2 Assets.
    public const string MutedGlyph = "";
    public const string LowGlyph = "";
    public const string MediumGlyph = "";
    public const string HighGlyph = "";

    private readonly IVolumeService _volume;

    private bool _isAvailable;
    private int _percent;
    private bool _isMuted;
    private string _glyph = MutedGlyph;

    public VolumeFlyoutViewModel(
        IVolumeService volume,
        ISettingsService settings,
        IDelayTimer hideTimer,
        Func<bool>? isFullscreenAppActive = null)
        : base(settings, hideTimer, isFullscreenAppActive)
    {
        ArgumentNullException.ThrowIfNull(volume);
        _volume = volume;
    }

    /// <summary>False when there is no playback device to report on.</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetProperty(ref _isAvailable, value);
    }

    /// <summary>The volume as a whole percentage, 0 to 100.</summary>
    public int Percent
    {
        get => _percent;
        private set => SetProperty(ref _percent, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        private set => SetProperty(ref _isMuted, value);
    }

    /// <summary>The speaker icon for the current level and mute state.</summary>
    public string Glyph
    {
        get => _glyph;
        private set => SetProperty(ref _glyph, value);
    }

    /// <summary>Call after a volume key has taken effect: re-reads the volume and shows the flyout.</summary>
    public void OnVolumeKeyHandled()
    {
        Refresh();
        TryShow();
    }

    /// <summary>
    /// Re-reads the volume. The window calls this on a fast tick while the flyout is visible,
    /// so it also follows changes made elsewhere (a held key, the taskbar slider).
    /// </summary>
    public void Refresh()
    {
        var state = _volume.GetState();
        if (state is not VolumeState current)
        {
            IsAvailable = false;
            return;
        }

        var percent = (int)Math.Round(Math.Clamp(current.Level, 0.0, 1.0) * 100.0, MidpointRounding.AwayFromZero);

        IsAvailable = true;
        Percent = percent;
        IsMuted = current.IsMuted;
        Glyph = GetGlyph(percent, current.IsMuted);
    }

    public static string GetGlyph(int percent, bool isMuted)
    {
        if (isMuted || percent <= 0)
        {
            return MutedGlyph;
        }

        return percent switch
        {
            <= 33 => LowGlyph,
            <= 66 => MediumGlyph,
            _ => HighGlyph,
        };
    }

    protected override bool HasContent => IsAvailable;

    protected override bool IsEnabled(AppSettings settings) => settings.VolumeFlyoutEnabled;

    protected override void OnShowing() => Refresh();
}
