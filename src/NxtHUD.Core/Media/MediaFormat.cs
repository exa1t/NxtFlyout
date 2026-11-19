namespace NxtHUD.Core.Media;

/// <summary>Text formatting for the media flyout.</summary>
public static class MediaFormat
{
    /// <summary>Players whose id does not read well on its own.</summary>
    private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Google Chrome",
        ["msedge"] = "Microsoft Edge",
        ["firefox"] = "Firefox",
        ["308046B0AF4A39CB"] = "Firefox",
        ["vlc"] = "VLC",
        ["ZuneMusic"] = "Media Player",
        ["ZuneVideo"] = "Movies & TV",
    };

    /// <summary>Formats a position or duration as "m:ss", or "h:mm:ss" from one hour up.</summary>
    public static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    /// <summary>
    /// Turns a player's app user model id into something readable:
    /// "Spotify.exe" and "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" both become "Spotify".
    /// Returns an empty string when no sensible name can be derived.
    /// </summary>
    public static string FriendlyAppName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return string.Empty;
        }

        var name = appId.Trim();

        // Packaged apps: "<package family name>!<app id>".
        var bang = name.LastIndexOf('!');
        if (bang >= 0 && bang < name.Length - 1)
        {
            name = name[(bang + 1)..];
        }

        // Desktop apps: an executable name, sometimes with a path.
        var slash = name.LastIndexOfAny(new[] { '\\', '/' });
        if (slash >= 0 && slash < name.Length - 1)
        {
            name = name[(slash + 1)..];
        }

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        // "Publisher.App_publisherhash" -> "App".
        var underscore = name.IndexOf('_');
        if (underscore > 0)
        {
            name = name[..underscore];
        }

        var dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1)
        {
            name = name[(dot + 1)..];
        }

        if (KnownApps.TryGetValue(name, out var known))
        {
            return known;
        }

        if (name.Length == 0 || LooksLikeAnId(name))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>Long all-hex strings are generated ids, not names.</summary>
    private static bool LooksLikeAnId(string name)
    {
        return name.Length >= 8 && name.All(Uri.IsHexDigit);
    }
}
