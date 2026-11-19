using System.ComponentModel;
using System.Windows;
using NxtHUD.Core.ViewModels;
using NxtHUD.Views;

namespace NxtHUD.Services;

/// <summary>
/// Connects one flyout view model to its window: creates the window the first time the
/// flyout is shown, then shows and hides it as the view model's IsVisible changes.
/// </summary>
internal sealed class FlyoutPresenter : IDisposable
{
    private readonly FlyoutViewModelBase _viewModel;
    private readonly Func<FlyoutWindowBase> _createWindow;
    private FlyoutWindowBase? _window;
    private bool _disposed;

    /// <param name="viewModel">The flyout to present.</param>
    /// <param name="createWindow">Creates the flyout's window; called lazily and again after <see cref="Reset"/>.</param>
    public FlyoutPresenter(FlyoutViewModelBase viewModel, Func<FlyoutWindowBase> createWindow)
    {
        _viewModel = viewModel;
        _createWindow = createWindow;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Closes the window so the next show builds a fresh one. Used when a setting that is
    /// fixed for a window's lifetime changes (solid card versus system backdrop).
    /// </summary>
    public void Reset()
    {
        if (_viewModel.IsVisible)
        {
            _viewModel.Hide();
        }

        CloseWindow();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        CloseWindow();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FlyoutViewModelBase.IsVisible))
        {
            return;
        }

        try
        {
            if (_viewModel.IsVisible)
            {
                // Created on first use, then kept: re-showing an existing window is instant.
                _window ??= CreateWindow();
                _window.ShowFlyout();
            }
            else
            {
                _window?.HideFlyout();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ArgumentException)
        {
            // A flyout that cannot be shown must not take the app down with it.
            Log.Error($"Showing the {_viewModel.GetType().Name} flyout failed.", ex);
            CloseWindow();
        }
    }

    private FlyoutWindowBase CreateWindow()
    {
        var window = _createWindow();
        window.Closed += OnWindowClosed;

        // WPF makes the first window created the app's main window; a flyout must not take that role.
        var app = Application.Current;
        if (app is not null && ReferenceEquals(app.MainWindow, window))
        {
            app.MainWindow = null;
        }

        return window;
    }

    private void CloseWindow()
    {
        var window = _window;
        if (window is null)
        {
            return;
        }

        _window = null;
        window.Closed -= OnWindowClosed;
        window.Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
        }
    }
}
