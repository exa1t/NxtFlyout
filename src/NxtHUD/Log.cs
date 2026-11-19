using System.IO;

namespace NxtHUD;

/// <summary>
/// A deliberately tiny file logger for errors: %AppData%\NxtHUD\logs\nxthud.log.
/// Logging never throws; a tray app has nowhere better to report that logging failed.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 512 * 1024;

    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppInfo.LogDirectory);

                var path = AppInfo.LogPath;
                var file = new FileInfo(path);
                if (file.Exists && file.Length > MaxBytes)
                {
                    // Keep one previous log so the file cannot grow without bound.
                    File.Move(path, path + ".old", overwrite: true);
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing sensible to do.
        }
    }
}
