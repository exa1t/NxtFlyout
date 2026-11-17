using NxtFlyout.Core.Input;
using NxtFlyout.Core.Localization;
using NxtFlyout.Core.Media;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Core.ViewModels;

/// <summary>Backs the lock keys flyout: which key was toggled and whether it is now on.</summary>
public sealed class LockKeysFlyoutViewModel : FlyoutViewModelBase
{
    // Padlock glyphs in Segoe Fluent Icons / Segoe MDL2 Assets.
    public const string OnGlyph = "";
    public const string OffGlyph = "";

    private readonly Func<LockKey, bool> _isKeyOn;
    private readonly Localizer _localizer;

    private LockKey _key;
    private bool _isOn;
    private string _text = string.Empty;
    private string _glyph = OffGlyph;

    /// <param name="isKeyOn">Returns the current toggle state of a lock key.</param>
    /// <param name="settings">App settings.</param>
    /// <param name="hideTimer">Timer that drives auto-hide.</param>
    /// <param name="localizer">Source of the "Caps Lock is on" style texts.</param>
    /// <param name="isFullscreenAppActive">Returns true while a fullscreen app is in the foreground.</param>
    public LockKeysFlyoutViewModel(
        Func<LockKey, bool> isKeyOn,
        ISettingsService settings,
        IDelayTimer hideTimer,
        Localizer localizer,
        Func<bool>? isFullscreenAppActive = null)
        : base(settings, hideTimer, isFullscreenAppActive)
    {
        ArgumentNullException.ThrowIfNull(isKeyOn);
        ArgumentNullException.ThrowIfNull(localizer);

        _isKeyOn = isKeyOn;
        _localizer = localizer;
    }

    public LockKey Key
    {
        get => _key;
        private set => SetProperty(ref _key, value);
    }

    public bool IsOn
    {
        get => _isOn;
        private set => SetProperty(ref _isOn, value);
    }

    /// <summary>For example "Caps Lock is on".</summary>
    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    public string Glyph
    {
        get => _glyph;
        private set => SetProperty(ref _glyph, value);
    }

    /// <summary>Call shortly after a lock key was pressed, once Windows has applied the toggle.</summary>
    public void OnLockKeyPressed(LockKey key)
    {
        var isOn = _isKeyOn(key);

        Key = key;
        IsOn = isOn;
        Text = _localizer.Get(GetTextKey(key, isOn));
        Glyph = isOn ? OnGlyph : OffGlyph;

        TryShow();
    }

    /// <summary>Name of the string resource for a key and state, e.g. "LockKeys_CapsLock_On".</summary>
    public static string GetTextKey(LockKey key, bool isOn) => $"LockKeys_{key}_{(isOn ? "On" : "Off")}";

    protected override bool IsEnabled(AppSettings settings) => settings.LockKeysFlyoutEnabled;
}
