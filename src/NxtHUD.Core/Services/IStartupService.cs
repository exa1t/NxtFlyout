namespace NxtHUD.Core.Services;

/// <summary>Registers or unregisters the app to launch when the user signs in to Windows.</summary>
public interface IStartupService
{
    /// <summary>Whether the app is currently registered (and not disabled in Task Manager).</summary>
    bool IsEnabled();

    /// <summary>Registers or unregisters the app. Throws if the registration cannot be changed.</summary>
    void SetEnabled(bool enabled);
}
