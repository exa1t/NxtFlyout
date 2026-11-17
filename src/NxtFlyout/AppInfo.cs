using System.IO;
using System.Reflection;

namespace NxtFlyout;

/// <summary>Names, paths and version of this app, in one place.</summary>
internal static class AppInfo
{
    public const string Name = "NxtFlyout";

    /// <summary>Identifies this app for the single-instance check.</summary>
    public const string InstanceId = "NxtFlyout-6f0e2c1a-9b7d-4c35-8e21-3d5a7b9c0f42";

    /// <summary>Passed on the command line when Windows starts the app at sign-in.</summary>
    public const string AutostartArgument = "--autostart";

    public static string Version { get; } = ReadVersion();

    /// <summary>%AppData%\NxtFlyout</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Name);

    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static string LogPath => Path.Combine(LogDirectory, "nxtflyout.log");

    /// <summary>The command line Windows should run at sign-in to start this app.</summary>
    public static string GetAutostartCommand()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The path of the running executable is unknown.");

        var command = $"\"{processPath}\"";

        // When launched as "dotnet NxtFlyout.dll", the process is dotnet.exe and needs the dll as its argument.
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(entryAssembly))
            {
                command += $" \"{entryAssembly}\"";
            }
        }

        return $"{command} {AutostartArgument}";
    }

    private static string ReadVersion()
    {
        var version = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrEmpty(version))
        {
            return "0.0.0";
        }

        // Drop build metadata such as "+<commit hash>".
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
