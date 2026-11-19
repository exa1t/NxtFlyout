using System.IO;
using Microsoft.Win32;
using NxtHUD.Core.Migration;

namespace NxtHUD.Services;

/// <summary>The real disk, for <see cref="LegacyMigration"/>.</summary>
internal sealed class MigrationFileSystem : IMigrationFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void CopyFile(string source, string destination) => File.Copy(source, destination, overwrite: false);

    public IEnumerable<string> GetFiles(string directory) => Directory.GetFiles(directory);
}

/// <summary>
/// The per-user Run key and its StartupApproved flags, for <see cref="LegacyMigration"/>.
/// Uses the same keys and the same "odd first byte means switched off" rule as <see cref="StartupService"/>.
/// </summary>
internal sealed class StartupRegistry : IStartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public string? GetRunCommand(string valueName)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return runKey?.GetValue(valueName) as string;
    }

    public bool IsDisabledInTaskManager(string valueName)
    {
        using var approvedKey = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
        return approvedKey?.GetValue(valueName) is byte[] { Length: > 0 } state && (state[0] & 1) != 0;
    }

    public void SetRunCommand(string valueName, string command)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        runKey.SetValue(valueName, command, RegistryValueKind.String);
    }

    public void DeleteEntry(string valueName)
    {
        using (var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
        {
            runKey?.DeleteValue(valueName, throwOnMissingValue: false);
        }

        using var approvedKey = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
        approvedKey?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
