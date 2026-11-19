using NxtHUD.Core.Audio;
using NxtHUD.Core.Media;
using NxtHUD.Core.Settings;
using NxtHUD.Core.ViewModels;

namespace NxtHUD.Tests;

/// <summary>An <see cref="IAudioSpectrumSource"/> that records requests and emits levels on demand.</summary>
internal sealed class FakeSpectrumSource : IAudioSpectrumSource
{
    public bool Active { get; private set; }

    public int LastBarCount { get; private set; }

    public event EventHandler<float[]>? LevelsUpdated;

    public void SetActive(bool active, int barCount)
    {
        Active = active;
        LastBarCount = barCount;
    }

    public void Emit(params float[] levels) => LevelsUpdated?.Invoke(this, levels);
}

public class SpectrumAnalyzerTests
{
    private const int SampleRate = 48000;

    private static float[] Sine(double frequency, double amplitude = 0.8, int count = SpectrumAnalyzer.FftSize)
    {
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / SampleRate));
        }

        return samples;
    }

    [Fact]
    public void Silence_GivesZeroLevels()
    {
        var analyzer = new SpectrumAnalyzer();
        analyzer.AddSilence(SpectrumAnalyzer.FftSize);

        var levels = analyzer.ComputeRawLevels(14, SampleRate);

        Assert.Equal(14, levels.Length);
        Assert.All(levels, level => Assert.Equal(0f, level));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(1000)]
    [InlineData(8000)]
    public void Tone_PeaksInTheBandThatContainsIt(double frequency)
    {
        const int bars = 16;
        var analyzer = new SpectrumAnalyzer();
        analyzer.AddSamples(Sine(frequency));

        var levels = analyzer.ComputeRawLevels(bars, SampleRate);

        var top = Math.Min(SpectrumAnalyzer.MaxFrequency, SampleRate / 2.0);
        var expectedBar = (int)Math.Floor(bars * Math.Log(frequency / SpectrumAnalyzer.MinFrequency) / Math.Log(top / SpectrumAnalyzer.MinFrequency));
        var loudest = Array.IndexOf(levels, levels.Max());

        Assert.InRange(loudest, expectedBar - 1, expectedBar + 1);
        Assert.True(levels[loudest] > 0.7f);
    }

    [Fact]
    public void Levels_AreAlwaysBetweenZeroAndOne()
    {
        var analyzer = new SpectrumAnalyzer();
        analyzer.AddSamples(Sine(440, amplitude: 5));
        analyzer.AddSamples(new[] { float.NaN, float.PositiveInfinity });

        var levels = analyzer.ComputeLevels(20, SampleRate);

        Assert.All(levels, level => Assert.InRange(level, 0f, 1f));
    }

    [Fact]
    public void Smoothing_RisesQuicklyAndFallsSlowly()
    {
        var analyzer = new SpectrumAnalyzer();
        analyzer.AddSamples(Sine(1000));
        var rising = analyzer.ComputeLevels(10, SampleRate).Max();

        for (var i = 0; i < 5; i++)
        {
            analyzer.ComputeLevels(10, SampleRate);
        }

        var peak = analyzer.ComputeLevels(10, SampleRate).Max();
        analyzer.AddSilence(SpectrumAnalyzer.FftSize);
        var falling = analyzer.ComputeLevels(10, SampleRate).Max();

        Assert.True(rising > 0.4f);
        Assert.True(falling > 0 && falling < peak);
    }

    [Fact]
    public void InvalidArguments_GiveNoLevels()
    {
        var analyzer = new SpectrumAnalyzer();

        Assert.Empty(analyzer.ComputeLevels(0, SampleRate));
        Assert.Empty(analyzer.ComputeLevels(10, 0));
    }
}

public class TaskbarWidgetVisualizerTests
{
    private readonly FakeMediaService _media = new();
    private readonly InMemorySettingsService _settings = new();
    private readonly FakeSpectrumSource _spectrum = new();

    public TaskbarWidgetVisualizerTests()
    {
        _settings.Update(s => s.TaskbarWidgetEnabled = true);
    }

    private TaskbarWidgetViewModel CreateViewModel() => new(_media, _settings, () => { }, _spectrum);

    private void Play(MediaPlaybackState state = MediaPlaybackState.Playing) =>
        _media.Set(new MediaSnapshot { Title = "Song", PlaybackState = state, CanGoNext = true, CanGoPrevious = true });

    [Fact]
    public async Task PreviousAndNext_ControlThePlayer()
    {
        using var viewModel = CreateViewModel();
        Play();

        Assert.True(viewModel.CanGoNext);
        Assert.True(viewModel.CanGoPrevious);

        await viewModel.NextCommand.ExecuteAsync(null);
        await viewModel.PreviousCommand.ExecuteAsync(null);

        Assert.Equal(1, _media.SkipNextCalls);
        Assert.Equal(1, _media.SkipPreviousCalls);
    }

    [Fact]
    public void Spectrum_IsOnlyActiveWhilePlaying()
    {
        using var viewModel = CreateViewModel();
        Assert.False(_spectrum.Active);

        Play();
        Assert.True(_spectrum.Active);
        Assert.Equal(AppSettings.DefaultVisualizerBars, _spectrum.LastBarCount);

        Play(MediaPlaybackState.Paused);
        Assert.False(_spectrum.Active);
    }

    [Fact]
    public void Spectrum_StopsWhenTheVisualizerIsSwitchedOff()
    {
        using var viewModel = CreateViewModel();
        Play();

        _settings.Update(s => s.TaskbarVisualizerEnabled = false);

        Assert.False(_spectrum.Active);
        Assert.False(viewModel.ShowVisualizer);
    }

    [Fact]
    public void BarCountChange_IsPassedToTheSource()
    {
        using var viewModel = CreateViewModel();
        Play();

        _settings.Update(s => s.VisualizerBarCount = 18);

        Assert.Equal(18, viewModel.VisualizerBarCount);
        Assert.Equal(18, _spectrum.LastBarCount);
    }

    [Fact]
    public void Levels_AreForwardedOnlyWhileActive()
    {
        using var viewModel = CreateViewModel();
        Play();

        _spectrum.Emit(0.5f, 0.25f);
        Assert.Equal(new[] { 0.5f, 0.25f }, viewModel.VisualizerLevels);

        Play(MediaPlaybackState.Paused);
        _spectrum.Emit(0.9f);

        Assert.All(viewModel.VisualizerLevels, level => Assert.Equal(0f, level));
    }

    [Fact]
    public void WithoutASource_TheVisualizerIsHidden()
    {
        using var viewModel = new TaskbarWidgetViewModel(_media, _settings, () => { });
        Play();

        Assert.False(viewModel.ShowVisualizer);
    }

    [Fact]
    public void Dispose_ReleasesTheSource()
    {
        var viewModel = CreateViewModel();
        Play();

        viewModel.Dispose();

        Assert.False(_spectrum.Active);
    }
}

public class VisualizerSettingsTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var settings = new AppSettings();

        Assert.True(settings.TaskbarVisualizerEnabled);
        Assert.Equal(AppSettings.DefaultVisualizerBars, settings.VisualizerBarCount);
        Assert.Equal(VisualizerColorMode.Accent, settings.VisualizerColorMode);
        Assert.Equal(AppSettings.DefaultVisualizerColor, settings.VisualizerCustomColor);
    }

    [Theory]
    [InlineData(0, AppSettings.MinVisualizerBars)]
    [InlineData(15, 15)]
    [InlineData(99, AppSettings.MaxVisualizerBars)]
    public void Normalize_ClampsTheBarCount(int input, int expected)
    {
        var settings = new AppSettings { VisualizerBarCount = input };
        settings.Normalize();
        Assert.Equal(expected, settings.VisualizerBarCount);
    }

    [Theory]
    [InlineData("#1db954", "#1DB954")]
    [InlineData(" #801DB954 ", "#801DB954")]
    [InlineData("green", AppSettings.DefaultVisualizerColor)]
    [InlineData("#12345", AppSettings.DefaultVisualizerColor)]
    [InlineData("", AppSettings.DefaultVisualizerColor)]
    public void Normalize_ValidatesTheCustomColor(string input, string expected)
    {
        var settings = new AppSettings { VisualizerCustomColor = input };
        settings.Normalize();
        Assert.Equal(expected, settings.VisualizerCustomColor);
    }

    [Fact]
    public void Normalize_RepairsAnUnknownColorMode()
    {
        var settings = new AppSettings { VisualizerColorMode = (VisualizerColorMode)42 };
        settings.Normalize();
        Assert.Equal(VisualizerColorMode.Accent, settings.VisualizerColorMode);
    }

    [Fact]
    public void Alignment_DefaultsToCenterAndRepairsUnknownValues()
    {
        Assert.Equal(VisualizerAlignment.Center, new AppSettings().VisualizerAlignment);

        var settings = new AppSettings { VisualizerAlignment = (VisualizerAlignment)9 };
        settings.Normalize();
        Assert.Equal(VisualizerAlignment.Center, settings.VisualizerAlignment);
    }

    [Fact]
    public void Alignment_FlowsFromSettingsToTheWidget()
    {
        var settings = new InMemorySettingsService();
        using var settingsViewModel = new SettingsViewModel(settings, new FakeStartupService());
        using var widget = new TaskbarWidgetViewModel(new FakeMediaService(), settings, () => { }, new FakeSpectrumSource());

        settingsViewModel.VisualizerAlignment = VisualizerAlignment.Bottom;

        Assert.Equal(VisualizerAlignment.Bottom, settings.Current.VisualizerAlignment);
        Assert.Equal(VisualizerAlignment.Bottom, widget.VisualizerAlignment);
    }

    [Fact]
    public void SettingsViewModel_RejectsAnInvalidCustomColor()
    {
        var settings = new InMemorySettingsService();
        using var viewModel = new SettingsViewModel(settings, new FakeStartupService());

        viewModel.VisualizerCustomColor = "#00FF00";
        viewModel.VisualizerCustomColor = "not a colour";

        Assert.Equal("#00FF00", settings.Current.VisualizerCustomColor);
    }

    [Fact]
    public void SettingsViewModel_ClampsTheBarCount()
    {
        var settings = new InMemorySettingsService();
        using var viewModel = new SettingsViewModel(settings, new FakeStartupService());

        viewModel.VisualizerBarCount = 50;

        Assert.Equal(AppSettings.MaxVisualizerBars, settings.Current.VisualizerBarCount);
    }
}

public class TranslucentBackdropTests
{
    [Fact]
    public void Opacity_DefaultsAndIsClamped()
    {
        Assert.Equal(AppSettings.DefaultTranslucentOpacity, new AppSettings().TranslucentOpacity);

        var low = new AppSettings { TranslucentOpacity = 10 };
        low.Normalize();
        Assert.Equal(AppSettings.MinTranslucentOpacity, low.TranslucentOpacity);

        var high = new AppSettings { TranslucentOpacity = 400 };
        high.Normalize();
        Assert.Equal(AppSettings.MaxTranslucentOpacity, high.TranslucentOpacity);
    }

    [Fact]
    public void SettingsViewModel_EnablesTheSliderOnlyForTranslucent()
    {
        var settings = new InMemorySettingsService();
        using var viewModel = new SettingsViewModel(settings, new FakeStartupService());
        Assert.False(viewModel.IsTranslucentBackdrop);

        viewModel.Backdrop = BackdropKind.Translucent;
        viewModel.TranslucentOpacity = 66.4;

        Assert.True(viewModel.IsTranslucentBackdrop);
        Assert.Equal(66, settings.Current.TranslucentOpacity);
    }
}
