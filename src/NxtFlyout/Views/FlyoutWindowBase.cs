using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;
using NxtFlyout.Core.Flyouts;
using NxtFlyout.Core.Settings;
using NxtFlyout.Core.ViewModels;
using NxtFlyout.Interop;

namespace NxtFlyout.Views;

/// <summary>What a flyout window needs from the rest of the app.</summary>
/// <param name="Settings">App settings (position, display, animation speed, backdrop).</param>
/// <param name="IsDarkTheme">Returns whether the app theme is currently dark.</param>
/// <param name="UseSystemBackdrop">
/// True to draw the window on a real Mica/Acrylic material supplied by Windows; false for a
/// solid themed card. Fixed for the lifetime of a window; the app recreates flyouts when it changes.
/// </param>
public sealed record FlyoutWindowContext(ISettingsService Settings, Func<bool> IsDarkTheme, bool UseSystemBackdrop);

/// <summary>
/// Base class for every flyout window: a small topmost window that never takes focus,
/// places itself on the right display, sizes itself to its content and slides/fades in and out.
/// </summary>
/// <remarks>
/// <para>The view model decides <em>whether</em> a flyout is visible; this class turns that
/// into a window on screen.</para>
/// <para>There are two ways the window is built, chosen by <see cref="FlyoutWindowContext.UseSystemBackdrop"/>:</para>
/// <list type="bullet">
/// <item><description><b>System backdrop</b>: an ordinary (non-layered) window whose frame is
/// extended over the whole client area. Windows 11 supplies the Mica or Acrylic material,
/// the rounded corners, the border and the shadow.</description></item>
/// <item><description><b>Solid card</b>: a transparent layered window that draws its own
/// rounded card and shadow. Works on every Windows version.</description></item>
/// </list>
/// <para>Positions are computed in physical pixels and applied with SetWindowPos, which is
/// what makes placement correct on displays with different scale factors.</para>
/// </remarks>
public class FlyoutWindowBase : Window
{
    /// <summary>How far the flyout slides while it fades, in device-independent pixels.</summary>
    private const double SlideDistance = 14;

    /// <summary>Gap between the card and the edge of the work area, in device-independent pixels.</summary>
    private const double EdgeGap = 16;

    private const double CardCornerRadius = 12;

    /// <summary>1 = fully slid away (hidden), 0 = at rest. Animated; every change moves the window.</summary>
    private static readonly DependencyProperty SlideProgressProperty = DependencyProperty.Register(
        "SlideProgress",
        typeof(double),
        typeof(FlyoutWindowBase),
        new PropertyMetadata(1.0, (d, _) => ((FlyoutWindowBase)d).MoveToCurrentPosition()));

    private readonly Grid _root = new();

    // Parts of the self-drawn card (solid or translucent); null with a system backdrop.
    private Border? _shadow;
    private Border? _surface;
    private Border? _sheen;

    // Set once by Configure, which every derived constructor calls first.
    private FlyoutViewModelBase _viewModel = null!;
    private FlyoutWindowContext _context = null!;
    private Func<AppSettings, (FlyoutVerticalPosition Vertical, FlyoutHorizontalPosition Horizontal)> _getPosition = null!;
    private readonly DispatcherTimer _tickTimer;

    /// <summary>True from ShowFlyout until HideFlyout, i.e. including the fade-in but not the fade-out.</summary>
    private bool _wanted;

    /// <summary>Set when the window has been closed; a closed window can no longer be shown or hidden.</summary>
    private bool _isClosed;

    // Where the window rests and how far it slides, in physical pixels.
    private double _restLeft;
    private double _restTop;
    private double _slideOffset;

    protected FlyoutWindowBase()
    {
        Title = "NxtFlyout";
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Background = Brushes.Transparent;
        Height = 80;

        _tickTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _tickTimer.Tick += OnTickTimerTick;
        DpiChanged += OnWindowDpiChanged;
    }

    /// <summary>
    /// Supplies what the flyout needs and picks the kind of window. Derived constructors call
    /// this first, then InitializeComponent, then <see cref="BuildChrome"/>.
    /// </summary>
    /// <param name="viewModel">The flyout's view model; becomes the DataContext.</param>
    /// <param name="context">Services and options shared by all flyouts.</param>
    /// <param name="cardWidth">Width of the visible card in device-independent pixels.</param>
    /// <param name="getPosition">Reads this flyout's position from the settings.</param>
    protected void Configure(
        FlyoutViewModelBase viewModel,
        FlyoutWindowContext context,
        double cardWidth,
        Func<AppSettings, (FlyoutVerticalPosition Vertical, FlyoutHorizontalPosition Horizontal)> getPosition)
    {
        _viewModel = viewModel;
        _context = context;
        _getPosition = getPosition;

        DataContext = viewModel;

        if (UseSystemBackdrop)
        {
            // Keep a real window frame (Windows only gives framed windows a backdrop, rounded
            // corners and a shadow) but let the client area cover all of it.
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                GlassFrameThickness = new Thickness(-1),
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false,
            });
            Width = cardWidth;
        }
        else
        {
            // A transparent layered window, larger than the card to leave room for its shadow.
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Width = cardWidth + (2 * EdgeGap);
        }
    }

    private bool UseSystemBackdrop => _context.UseSystemBackdrop;

    /// <summary>
    /// How often <see cref="OnTick"/> runs while the flyout is on screen, or null for never.
    /// </summary>
    protected virtual TimeSpan? TickInterval => null;

    /// <summary>Puts the flyout on screen and animates it in.</summary>
    public void ShowFlyout()
    {
        if (_isClosed)
        {
            return;
        }

        _wanted = true;

        var handle = new WindowInteropHelper(this).EnsureHandle();
        ApplyWindowAttributes(handle);
        ApplyCardStyle();

        if (!IsVisible)
        {
            // Size and place the window before it becomes visible, so it never flashes
            // somewhere else first.
            FitHeightToContent(measureFirst: true);
            BeginAnimation(SlideProgressProperty, null);
            SetValue(SlideProgressProperty, 1.0);
            _root.BeginAnimation(OpacityProperty, null);
            _root.Opacity = 0;

            UpdateRestPosition(handle);
            MoveToCurrentPosition();
            Show();

            if (UseSystemBackdrop)
            {
                // Mark the frame active so the material is drawn; this does not take focus.
                NativeMethods.SendMessage(handle, NativeMethods.WM_NCACTIVATE, new IntPtr(1), NativeMethods.NoFrameRepaint);
            }
        }
        else
        {
            UpdateRestPosition(handle);
        }

        if (TickInterval is TimeSpan interval)
        {
            _tickTimer.Interval = interval;
            _tickTimer.Start();
        }

        Animate(toProgress: 0, toOpacity: 1, onCompleted: null);
    }

    /// <summary>Animates the flyout out, then takes the window off screen.</summary>
    public void HideFlyout()
    {
        _wanted = false;
        _tickTimer.Stop();

        if (_isClosed || !IsVisible)
        {
            return;
        }

        Animate(toProgress: 1, toOpacity: 0, onCompleted: () =>
        {
            // The flyout may have been asked for again while it was fading out,
            // or closed outright (a settings change, app shutdown).
            if (!_wanted && !_isClosed)
            {
                Hide();
            }
        });
    }

    /// <summary>
    /// Wraps the content declared in XAML in the flyout's chrome. Derived classes call this
    /// once, right after InitializeComponent.
    /// </summary>
    protected void BuildChrome()
    {
        var content = Content as UIElement;
        Content = null;

        // Transparent rather than null, so the whole flyout counts as "under the pointer".
        _root.Background = Brushes.Transparent;
        _root.Width = Width;
        _root.Opacity = 0;
        _root.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");
        _root.MouseEnter += OnRootMouseEnter;
        _root.MouseLeave += OnRootMouseLeave;
        _root.SizeChanged += OnRootSizeChanged;

        var cardMargin = new Thickness(0);

        if (!UseSystemBackdrop)
        {
            cardMargin = new Thickness(EdgeGap);
            var radius = new CornerRadius(CardCornerRadius);

            // Shadow, on its own element so the effect does not soften the text above it.
            var shadow = new Border
            {
                Margin = cardMargin,
                CornerRadius = radius,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 22,
                    Direction = 270,
                    Opacity = 0.34,
                    ShadowDepth = 4,
                    Color = Colors.Black,
                },
            };
            shadow.SetResourceReference(Border.BackgroundProperty, "ApplicationBackgroundBrush");

            var surface = new Border { Margin = cardMargin, CornerRadius = radius };
            surface.SetResourceReference(Border.BackgroundProperty, "ApplicationBackgroundBrush");

            // A faint top-down sheen for a little depth.
            var sheen = new Border
            {
                Margin = cardMargin,
                CornerRadius = radius,
                Background = new LinearGradientBrush(
                    Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF),
                    Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    90),
            };

            // Hairline edge that reads in both light and dark themes.
            var edge = new Border
            {
                Margin = cardMargin,
                CornerRadius = radius,
                BorderThickness = new Thickness(1),
                Opacity = 0.12,
            };
            edge.SetResourceReference(Border.BorderBrushProperty, "TextFillColorPrimaryBrush");

            _root.Children.Add(shadow);
            _root.Children.Add(surface);
            _root.Children.Add(sheen);
            _root.Children.Add(edge);

            _shadow = shadow;
            _surface = surface;
            _sheen = sheen;
        }

        _root.Children.Add(new Border { Margin = cardMargin, Child = content });

        // A Canvas measures its children with unlimited space, so the card always reports
        // the height it wants; the window is then resized to match.
        var canvas = new Canvas();
        canvas.Children.Add(_root);
        Content = canvas;
    }

    /// <summary>Runs on <see cref="TickInterval"/> while the flyout is on screen.</summary>
    protected virtual void OnTick()
    {
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;

        // NOACTIVATE: clicking the flyout never takes focus away from the app in use.
        // TOOLWINDOW: keeps it out of Alt+Tab and the taskbar.
        NativeMethods.AddExtendedStyles(handle, NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);

        if (UseSystemBackdrop)
        {
            // Let the material show through wherever WPF draws nothing.
            if (HwndSource.FromHwnd(handle) is { CompositionTarget: not null } source)
            {
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
            }

            // Windows only draws Mica/Acrylic on the active window and shows a flat fallback
            // colour otherwise. Flyouts are never activated (they must not steal focus), so
            // keep their frame in the "active" state instead.
            HwndSource.FromHwnd(handle)?.AddHook(KeepFrameActiveHook);

            // No window menu means no caption close button that could ever be drawn.
            NativeMethods.RemoveStyles(handle, NativeMethods.WS_SYSMENU);

            // The flyout has its own animation; switch off the system's open/close one.
            NativeMethods.SetDwmAttribute(handle, NativeMethods.DWMWA_TRANSITIONS_FORCEDISABLED, 1);
            NativeMethods.SetDwmAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, NativeMethods.DWMWCP_ROUND);
        }
    }

    /// <summary>Turns every "frame inactive" notification into "frame active".</summary>
    private static IntPtr KeepFrameActiveHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_NCACTIVATE && wParam == IntPtr.Zero)
        {
            handled = true;
            return NativeMethods.DefWindowProc(hwnd, msg, new IntPtr(1), NativeMethods.NoFrameRepaint);
        }

        return IntPtr.Zero;
    }

    private void OnWindowDpiChanged(object sender, DpiChangedEventArgs e)
    {
        // The window just moved to a display with a different scale and has been resized for it.
        Dispatcher.InvokeAsync(() =>
        {
            if (_wanted)
            {
                Reposition();
            }
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _wanted = false;
        _tickTimer.Stop();
        _tickTimer.Tick -= OnTickTimerTick;
        DpiChanged -= OnWindowDpiChanged;
        _root.MouseEnter -= OnRootMouseEnter;
        _root.MouseLeave -= OnRootMouseLeave;
        _root.SizeChanged -= OnRootSizeChanged;
        base.OnClosed(e);
    }

    private void OnTickTimerTick(object? sender, EventArgs e) => OnTick();

    private void OnRootMouseEnter(object sender, MouseEventArgs e) => _viewModel.OnPointerEntered();

    private void OnRootMouseLeave(object sender, MouseEventArgs e) => _viewModel.OnPointerExited();

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // The content decides the height (a track with or without a timeline, for example).
        FitHeightToContent(measureFirst: false);

        if (_wanted)
        {
            Reposition();
        }
    }

    private void FitHeightToContent(bool measureFirst)
    {
        if (measureFirst)
        {
            _root.Measure(new Size(Width, double.PositiveInfinity));
        }

        var wanted = measureFirst ? _root.DesiredSize.Height : _root.ActualHeight;
        if (wanted > 0 && Math.Abs(Height - wanted) > 0.5)
        {
            Height = Math.Ceiling(wanted);
        }
    }

    /// <summary>
    /// Switches the self-drawn card between solid and translucent. Refreshed on every show,
    /// so changing the backdrop or the opacity takes effect the next time the flyout appears.
    /// </summary>
    private void ApplyCardStyle()
    {
        if (_surface is null || _shadow is null || _sheen is null)
        {
            return;
        }

        var settings = _context.Settings.Current;
        var translucent = settings.Backdrop == BackdropKind.Translucent;
        var opacity = translucent ? settings.TranslucentOpacity / 100.0 : 1.0;

        _surface.Opacity = opacity;
        _sheen.Opacity = opacity;

        if (translucent)
        {
            // The separate opaque shadow layer would block the view through the card, so
            // the see-through surface casts the (lighter) shadow itself.
            _shadow.Visibility = Visibility.Collapsed;
            _surface.Effect ??= new DropShadowEffect
            {
                BlurRadius = 18,
                Direction = 270,
                Opacity = 0.25,
                ShadowDepth = 3,
                Color = Colors.Black,
            };
        }
        else
        {
            _shadow.Visibility = Visibility.Visible;
            _surface.Effect = null;
        }
    }

    /// <summary>The system backdrop follows these attributes; they are refreshed on every show.</summary>
    private void ApplyWindowAttributes(IntPtr handle)
    {
        if (!UseSystemBackdrop)
        {
            return;
        }

        var backdrop = _context.Settings.Current.Backdrop == BackdropKind.Acrylic
            ? NativeMethods.DWMSBT_ACRYLIC
            : NativeMethods.DWMSBT_MICA;

        NativeMethods.SetDwmAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, _context.IsDarkTheme() ? 1 : 0);
        NativeMethods.SetDwmAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, backdrop);
    }

    private void Reposition()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        UpdateRestPosition(handle);
        MoveToCurrentPosition();
    }

    /// <summary>Works out where the window rests on the target display, in physical pixels.</summary>
    private void UpdateRestPosition(IntPtr handle)
    {
        var settings = _context.Settings.Current;
        var display = ScreenHelper.GetTargetDisplay(settings.Display);

        // Once the window is on screen its real pixel size is known; before that, estimate it
        // from the size in device-independent pixels and the target display's scale.
        double width;
        double height;
        if (IsVisible && NativeMethods.GetWindowRect(handle, out var rect) && rect.Width > 0)
        {
            width = rect.Width;
            height = rect.Height;
        }
        else
        {
            width = Width * display.Scale;
            height = Height * display.Scale;
        }

        var (vertical, horizontal) = _getPosition(settings);

        // The solid card already has the gap built into its transparent margin.
        var margin = UseSystemBackdrop ? EdgeGap * display.Scale : 0;

        (_restLeft, _restTop) = FlyoutPlacement.Calculate(display.WorkArea, width, height, vertical, horizontal, margin);

        // Slide in from the screen edge the flyout is docked to.
        var direction = vertical == FlyoutVerticalPosition.Top ? -1 : 1;
        _slideOffset = direction * SlideDistance * display.Scale;
    }

    private void MoveToCurrentPosition()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var progress = (double)GetValue(SlideProgressProperty);
        NativeMethods.MoveWindowTo(handle, _restLeft, _restTop + (_slideOffset * progress));
    }

    private void Animate(double toProgress, double toOpacity, Action? onCompleted)
    {
        var milliseconds = FlyoutPlacement.GetAnimationDurationMs(_context.Settings.Current.AnimationSpeed);

        if (milliseconds <= 0)
        {
            // Animations are switched off: clear any running ones and jump to the end state.
            BeginAnimation(SlideProgressProperty, null);
            _root.BeginAnimation(OpacityProperty, null);
            SetValue(SlideProgressProperty, toProgress);
            _root.Opacity = toOpacity;
            MoveToCurrentPosition();
            onCompleted?.Invoke();
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(milliseconds));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var slide = new DoubleAnimation
        {
            To = toProgress,
            Duration = duration,
            EasingFunction = easing,
        };

        var fade = new DoubleAnimation
        {
            To = toOpacity,
            Duration = duration,
            EasingFunction = easing,
        };

        if (onCompleted is not null)
        {
            fade.Completed += (_, _) => onCompleted();
        }

        BeginAnimation(SlideProgressProperty, slide);
        _root.BeginAnimation(OpacityProperty, fade);
    }
}
