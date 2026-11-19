using NxtHUD.Core.Flyouts;
using NxtHUD.Core.Media;
using NxtHUD.Core.Settings;

namespace NxtHUD.Tests;

public class MediaFormatTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(59.9, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(200, "3:20")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3725, "1:02:05")]
    [InlineData(-10, "0:00")]
    public void FormatTime_UsesMinutesAndSeconds_AddingHoursWhenNeeded(double seconds, string expected)
    {
        Assert.Equal(expected, MediaFormat.FormatTime(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("chrome.exe", "Google Chrome")]
    [InlineData("Chrome", "Google Chrome")]
    [InlineData("MSEdge", "Microsoft Edge")]
    [InlineData("308046B0AF4A39CB", "Firefox")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
    [InlineData("com.squirrel.Discord.Discord", "Discord")]
    [InlineData(@"C:\Apps\foobar2000.exe", "Foobar2000")]
    [InlineData("  vlc.exe  ", "VLC")]
    [InlineData("AIMP.EXE", "AIMP")]
    public void FriendlyAppName_MakesAppIdsReadable(string appId, string expected)
    {
        Assert.Equal(expected, MediaFormat.FriendlyAppName(appId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCDEF0123456789")]
    public void FriendlyAppName_ReturnsEmpty_WhenThereIsNoUsableName(string? appId)
    {
        Assert.Equal(string.Empty, MediaFormat.FriendlyAppName(appId));
    }
}

public class MediaKeyMapTests
{
    [Theory]
    [InlineData(0xB3, MediaKey.PlayPause)]
    [InlineData(0xB0, MediaKey.Next)]
    [InlineData(0xB1, MediaKey.Previous)]
    [InlineData(0xB2, MediaKey.Stop)]
    public void FromVirtualKey_RecognisesMediaKeys(int virtualKey, MediaKey expected)
    {
        Assert.Equal(expected, MediaKeyMap.FromVirtualKey(virtualKey));
    }

    [Theory]
    [InlineData(0x41)] // A
    [InlineData(0x20)] // Space
    [InlineData(0xAF)] // Volume up
    [InlineData(0xAD)] // Volume mute
    [InlineData(0)]
    public void FromVirtualKey_IgnoresOtherKeys(int virtualKey)
    {
        Assert.Null(MediaKeyMap.FromVirtualKey(virtualKey));
    }
}

public class FlyoutPlacementTests
{
    /// <summary>A 1920x1080 screen with a 48 px taskbar along the bottom.</summary>
    private static readonly ScreenRect TaskbarAtBottom = new(0, 0, 1920, 1032);

    [Theory]
    [InlineData(FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Center, 760, 912)]
    [InlineData(FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Left, 0, 912)]
    [InlineData(FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Right, 1520, 912)]
    [InlineData(FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Center, 760, 0)]
    [InlineData(FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Left, 0, 0)]
    [InlineData(FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Right, 1520, 0)]
    public void Calculate_PlacesTheFlyoutAtTheChosenEdge(
        FlyoutVerticalPosition vertical,
        FlyoutHorizontalPosition horizontal,
        double expectedLeft,
        double expectedTop)
    {
        var (left, top) = FlyoutPlacement.Calculate(TaskbarAtBottom, 400, 120, vertical, horizontal);

        Assert.Equal(expectedLeft, left);
        Assert.Equal(expectedTop, top);
    }

    [Fact]
    public void Calculate_AppliesTheMargin()
    {
        var (left, top) = FlyoutPlacement.Calculate(
            TaskbarAtBottom, 400, 120, FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Right, margin: 12);

        Assert.Equal(1508, left);
        Assert.Equal(900, top);
    }

    [Fact]
    public void Calculate_RespectsAWorkAreaThatDoesNotStartAtZero()
    {
        // Taskbar docked on the left (work area starts at x = 48) and at the top (y = 48).
        var workArea = new ScreenRect(48, 48, 1872, 1032);

        var (left, top) = FlyoutPlacement.Calculate(
            workArea, 400, 120, FlyoutVerticalPosition.Top, FlyoutHorizontalPosition.Left);

        Assert.Equal(48, left);
        Assert.Equal(48, top);
    }

    [Fact]
    public void Calculate_BottomMeansJustAboveTheTaskbar()
    {
        var (_, top) = FlyoutPlacement.Calculate(
            TaskbarAtBottom, 400, 120, FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Center);

        Assert.Equal(TaskbarAtBottom.Bottom, top + 120);
    }

    [Fact]
    public void Calculate_KeepsAnOversizedFlyoutAtTheWorkAreaOrigin()
    {
        var small = new ScreenRect(100, 50, 300, 80);

        var (left, top) = FlyoutPlacement.Calculate(
            small, 400, 120, FlyoutVerticalPosition.Bottom, FlyoutHorizontalPosition.Right, margin: 12);

        Assert.Equal(100, left);
        Assert.Equal(50, top);
    }

    [Fact]
    public void ScreenRect_ExposesItsFarEdges()
    {
        var rect = new ScreenRect(10, 20, 300, 400);

        Assert.Equal(310, rect.Right);
        Assert.Equal(420, rect.Bottom);
    }

    [Fact]
    public void AnimationDuration_GrowsWithSlowerSpeeds_AndOffMeansNone()
    {
        var off = FlyoutPlacement.GetAnimationDurationMs(AnimationSpeed.Off);
        var fast = FlyoutPlacement.GetAnimationDurationMs(AnimationSpeed.Fast);
        var normal = FlyoutPlacement.GetAnimationDurationMs(AnimationSpeed.Normal);
        var slow = FlyoutPlacement.GetAnimationDurationMs(AnimationSpeed.Slow);

        Assert.Equal(0, off);
        Assert.True(fast > 0);
        Assert.True(fast < normal);
        Assert.True(normal < slow);
    }
}
