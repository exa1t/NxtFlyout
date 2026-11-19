using System.Windows.Threading;
using NAudio.Wave;
using NxtHUD.Core.Audio;

namespace NxtHUD.Services;

/// <summary>
/// Listens to whatever the default playback device is playing (WASAPI loopback) and turns it
/// into bar levels for the taskbar visualizer. Capture only runs while the visualizer asks for it.
/// </summary>
internal sealed class LoopbackSpectrumSource : IAudioSpectrumSource, IDisposable
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>Minimum wait before trying to reopen the device after capture failed or stopped.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    private readonly object _gate = new();
    private readonly SpectrumAnalyzer _analyzer = new();
    private readonly DispatcherTimer _timer;

    private WasapiLoopbackCapture? _capture;
    private int _sampleRate = 48000;
    private int _barCount;
    private int _samplesSinceFrame;
    private bool _active;
    private bool _disposed;
    private bool _loggedFormatProblem;
    private DateTime _nextStartAttempt = DateTime.MinValue;
    private float[] _mono = Array.Empty<float>();

    public LoopbackSpectrumSource(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render, dispatcher) { Interval = FrameInterval };
        _timer.Tick += OnFrame;
    }

    public event EventHandler<float[]>? LevelsUpdated;

    public void SetActive(bool active, int barCount)
    {
        if (_disposed)
        {
            return;
        }

        _barCount = Math.Max(1, barCount);

        if (active == _active)
        {
            return;
        }

        _active = active;

        if (active)
        {
            _nextStartAttempt = DateTime.MinValue;
            TryStartCapture();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            StopCapture();
            lock (_gate)
            {
                _analyzer.Reset();
            }

            LevelsUpdated?.Invoke(this, new float[_barCount]);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _active = false;
        _timer.Stop();
        _timer.Tick -= OnFrame;
        StopCapture();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!_active)
        {
            return;
        }

        if (_capture is null && DateTime.UtcNow >= _nextStartAttempt)
        {
            TryStartCapture();
        }

        float[] levels;
        lock (_gate)
        {
            // Windows delivers no loopback data while nothing is playing; feed silence so the bars fall.
            if (_samplesSinceFrame == 0)
            {
                _analyzer.AddSilence((int)(_sampleRate * FrameInterval.TotalSeconds));
            }

            _samplesSinceFrame = 0;
            levels = _analyzer.ComputeLevels(_barCount, _sampleRate);
        }

        LevelsUpdated?.Invoke(this, levels);
    }

    private void TryStartCapture()
    {
        if (_capture is not null)
        {
            return;
        }

        WasapiLoopbackCapture? capture = null;
        try
        {
            capture = new WasapiLoopbackCapture();
            lock (_gate)
            {
                _sampleRate = capture.WaveFormat.SampleRate;
            }

            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;
            capture.StartRecording();
            _capture = capture;
        }
        catch (Exception ex)
        {
            // No playback device, device in exclusive use, audio service stopped...
            Log.Error("Starting audio capture for the visualizer failed.", ex);
            if (capture is not null)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.RecordingStopped -= OnRecordingStopped;
                capture.Dispose();
            }

            _nextStartAttempt = DateTime.UtcNow + RetryDelay;
        }
    }

    private void StopCapture()
    {
        var capture = _capture;
        if (capture is null)
        {
            return;
        }

        _capture = null;
        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;

        try
        {
            capture.StopRecording();
        }
        catch (Exception ex)
        {
            Log.Error("Stopping audio capture failed.", ex);
        }

        capture.Dispose();
    }

    /// <summary>Runs on NAudio's capture thread.</summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture capture || e.BytesRecorded <= 0)
        {
            return;
        }

        var format = capture.WaveFormat;
        var channels = Math.Max(1, format.Channels);
        var bytesPerSample = format.BitsPerSample / 8;
        var isFloat = format.BitsPerSample == 32 && format.Encoding != WaveFormatEncoding.Pcm;
        var isPcm16 = format.BitsPerSample == 16;

        if (!isFloat && !isPcm16)
        {
            if (!_loggedFormatProblem)
            {
                _loggedFormatProblem = true;
                Log.Info($"Visualizer: unsupported audio format {format.Encoding} {format.BitsPerSample}-bit.");
            }

            return;
        }

        var frames = e.BytesRecorded / (bytesPerSample * channels);
        if (_mono.Length < frames)
        {
            _mono = new float[frames];
        }

        var buffer = e.Buffer;
        for (var frame = 0; frame < frames; frame++)
        {
            float sum = 0;
            var offset = frame * bytesPerSample * channels;
            for (var channel = 0; channel < channels; channel++)
            {
                var position = offset + (channel * bytesPerSample);
                sum += isFloat
                    ? BitConverter.ToSingle(buffer, position)
                    : BitConverter.ToInt16(buffer, position) / 32768f;
            }

            _mono[frame] = sum / channels;
        }

        lock (_gate)
        {
            _analyzer.AddSamples(new ReadOnlySpan<float>(_mono, 0, frames));
            _samplesSinceFrame += frames;
        }
    }

    /// <summary>
    /// Capture stops by itself when the default device changes or disappears. Reopen it on
    /// the next frame (on the new default device) if the visualizer is still wanted.
    /// </summary>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Log.Error("Audio capture for the visualizer stopped.", e.Exception);
        }

        _timer.Dispatcher.InvokeAsync(() =>
        {
            if (ReferenceEquals(sender, _capture))
            {
                StopCapture();
                _nextStartAttempt = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
            }
        });
    }
}
