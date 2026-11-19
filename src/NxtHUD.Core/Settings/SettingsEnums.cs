namespace NxtHUD.Core.Settings;

/// <summary>Which colour theme the app uses.</summary>
public enum AppTheme
{
    /// <summary>Follow the Windows app mode (light or dark) live.</summary>
    System,
    Light,
    Dark,
}

/// <summary>The material drawn behind flyouts and windows.</summary>
public enum BackdropKind
{
    Mica,
    Acrylic,
    None,

    /// <summary>
    /// The app's own see-through card, without blur. Works where Windows switches its
    /// materials off, such as in energy saver.
    /// </summary>
    Translucent,
}

public enum FlyoutVerticalPosition
{
    Top,
    Bottom,
}

public enum FlyoutHorizontalPosition
{
    Left,
    Center,
    Right,
}

public enum AnimationSpeed
{
    /// <summary>No animation: flyouts appear and disappear instantly.</summary>
    Off,
    Fast,
    Normal,
    Slow,
}

/// <summary>A screen corner or edge centre a flyout can dock to.</summary>
public enum FlyoutPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

/// <summary>Which display flyouts appear on when there is more than one.</summary>
public enum FlyoutDisplay
{
    Primary,

    /// <summary>The display the mouse pointer is on.</summary>
    Cursor,

    /// <summary>The display showing the window that has focus.</summary>
    ActiveWindow,
}

/// <summary>Where the taskbar visualizer takes its bar colour from.</summary>
public enum VisualizerColorMode
{
    /// <summary>The same colour as the widget's text, so it suits light and dark taskbars.</summary>
    Auto,

    /// <summary>The Windows accent colour.</summary>
    Accent,

    /// <summary>A colour the user picks, stored in <see cref="AppSettings.VisualizerCustomColor"/>.</summary>
    Custom,
}

/// <summary>Which way the taskbar visualizer's bars grow.</summary>
public enum VisualizerAlignment
{
    /// <summary>Outwards from the middle, up and down at once.</summary>
    Center,

    /// <summary>From the bottom up.</summary>
    Bottom,

    /// <summary>From the top down.</summary>
    Top,
}
