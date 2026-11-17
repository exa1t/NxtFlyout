using System.ComponentModel;
using NxtFlyout.Core.Localization;
using NxtFlyout.Core.Settings;

namespace NxtFlyout.Tests;

public class LocalizerTests
{
    public static IEnumerable<object[]> AllSettingEnumValues()
    {
        var enumTypes = new[]
        {
            typeof(AppTheme),
            typeof(BackdropKind),
            typeof(FlyoutVerticalPosition),
            typeof(FlyoutHorizontalPosition),
            typeof(AnimationSpeed),
            typeof(FlyoutPosition),
            typeof(FlyoutDisplay),
            typeof(VisualizerColorMode),
            typeof(VisualizerAlignment),
        };

        foreach (var type in enumTypes)
        {
            foreach (object value in Enum.GetValues(type))
            {
                yield return new[] { value };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllSettingEnumValues))]
    public void EveryEnumValueShownInSettings_HasALabel(Enum value)
    {
        var localizer = new Localizer();
        localizer.SetLanguage("en");

        var key = Localizer.GetEnumKey(value);

        Assert.True(localizer.Contains(key), $"Strings.resx has no entry named '{key}'.");
        Assert.False(string.IsNullOrWhiteSpace(localizer.GetEnumLabel(value)));
    }

    [Fact]
    public void GetEnumKey_FollowsTheNamingConvention()
    {
        Assert.Equal("Enum_AppTheme_Dark", Localizer.GetEnumKey(AppTheme.Dark));
        Assert.Equal("Enum_FlyoutHorizontalPosition_Center", Localizer.GetEnumKey(FlyoutHorizontalPosition.Center));
    }

    [Fact]
    public void Get_ReturnsTheEnglishString()
    {
        var localizer = new Localizer();
        localizer.SetLanguage("en");

        Assert.Equal("Settings", localizer.Get("Tray_Settings"));
        Assert.Equal("Settings", localizer["Tray_Settings"]);
    }

    [Fact]
    public void Get_MissingKey_ReturnsTheKeyInBrackets()
    {
        var localizer = new Localizer();

        Assert.Equal("[No_Such_Key]", localizer.Get("No_Such_Key"));
        Assert.False(localizer.Contains("No_Such_Key"));
    }

    [Fact]
    public void Get_EmptyKey_ReturnsEmpty()
    {
        var localizer = new Localizer();

        Assert.Equal(string.Empty, localizer.Get(string.Empty));
        Assert.False(localizer.Contains(string.Empty));
    }

    [Fact]
    public void Get_LanguageWithoutTranslations_FallsBackToEnglish()
    {
        var localizer = new Localizer();
        localizer.SetLanguage("fr");

        Assert.Equal("Settings", localizer.Get("Tray_Settings"));
    }

    [Fact]
    public void SetLanguage_ChangesTheCultureAndRefreshesBoundStrings()
    {
        var localizer = new Localizer();
        localizer.SetLanguage("en");
        var changes = new List<string?>();
        ((INotifyPropertyChanged)localizer).PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        localizer.SetLanguage("fr");

        Assert.Equal("fr", localizer.Culture.Name);
        Assert.Contains("Item[]", changes);
        Assert.Contains(nameof(Localizer.Culture), changes);
    }

    [Fact]
    public void SetLanguage_SameLanguage_DoesNotNotify()
    {
        var localizer = new Localizer();
        localizer.SetLanguage("en");
        var raised = 0;
        localizer.PropertyChanged += (_, _) => raised++;

        localizer.SetLanguage("en");

        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("system")]
    [InlineData("SYSTEM")]
    public void ResolveCulture_SystemOrBlank_MeansTheWindowsLanguage(string? language)
    {
        var systemCulture = Localizer.ResolveCulture(AppSettings.SystemLanguage);

        Assert.Equal(systemCulture, Localizer.ResolveCulture(language));
    }

    [Fact]
    public void ResolveCulture_NamedLanguage_ReturnsThatCulture()
    {
        Assert.Equal("en", Localizer.ResolveCulture("en").Name);
    }

    [Fact]
    public void GetLanguageDisplayName_System_UsesTheLocalizedLabel()
    {
        var localizer = new Localizer();
        localizer.SetLanguage("en");

        Assert.Equal("Use system language", localizer.GetLanguageDisplayName(AppSettings.SystemLanguage));
    }

    [Fact]
    public void GetLanguageDisplayName_NamedLanguage_IsNotBlank()
    {
        var localizer = new Localizer();

        Assert.False(string.IsNullOrWhiteSpace(localizer.GetLanguageDisplayName("en")));
    }

    [Fact]
    public void SupportedLanguages_StartWithSystemAndResolve()
    {
        Assert.Equal(AppSettings.SystemLanguage, Localizer.SupportedLanguages[0]);

        var localizer = new Localizer();
        foreach (var language in Localizer.SupportedLanguages)
        {
            Assert.NotNull(Localizer.ResolveCulture(language));
            Assert.False(string.IsNullOrWhiteSpace(localizer.GetLanguageDisplayName(language)));
        }
    }

    [Fact]
    public void Instance_IsShared()
    {
        Assert.Same(Localizer.Instance, Localizer.Instance);
    }
}
