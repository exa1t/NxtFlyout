namespace NxtHUD.Core.Audio;

/// <summary>
/// Turns a stream of mono audio samples into a small number of smoothed bar levels:
/// a windowed FFT of the most recent samples, grouped into log-spaced frequency bands.
/// </summary>
/// <remarks>Not thread-safe; callers that feed and read from different threads must lock.</remarks>
public sealed class SpectrumAnalyzer
{
    /// <summary>Samples per FFT. 2048 at 48 kHz is about 43 ms, with ~23 Hz per bin.</summary>
    public const int FftSize = 2048;

    public const double MinFrequency = 40;
    public const double MaxFrequency = 16000;

    /// <summary>Levels below this many decibels (relative to full scale) count as silence.</summary>
    private const double FloorDb = -70;

    /// <summary>How much of the gap to a higher level is closed per update (fast rise).</summary>
    private const float Attack = 0.6f;

    /// <summary>How much of the gap to a lower level is closed per update (slow fall).</summary>
    private const float Decay = 0.18f;

    private static readonly double[] Window = CreateHannWindow(FftSize);

    private readonly float[] _ring = new float[FftSize];
    private readonly double[] _real = new double[FftSize];
    private readonly double[] _imag = new double[FftSize];
    private int _writeIndex;
    private float[] _smoothed = Array.Empty<float>();

    /// <summary>Appends samples (nominally -1..1). Older samples beyond <see cref="FftSize"/> are dropped.</summary>
    public void AddSamples(ReadOnlySpan<float> samples)
    {
        foreach (var sample in samples)
        {
            _ring[_writeIndex] = float.IsFinite(sample) ? sample : 0f;
            _writeIndex = (_writeIndex + 1) % FftSize;
        }
    }

    /// <summary>Appends silence, so the bars fall when the audio stream stops delivering data.</summary>
    public void AddSilence(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _ring[_writeIndex] = 0f;
            _writeIndex = (_writeIndex + 1) % FftSize;
        }
    }

    /// <summary>Clears the sample history and the smoothing state.</summary>
    public void Reset()
    {
        Array.Clear(_ring);
        _writeIndex = 0;
        _smoothed = Array.Empty<float>();
    }

    /// <summary>
    /// Computes smoothed levels (0..1) for <paramref name="barCount"/> bands from the latest samples.
    /// </summary>
    public float[] ComputeLevels(int barCount, int sampleRate)
    {
        if (barCount <= 0 || sampleRate <= 0)
        {
            return Array.Empty<float>();
        }

        var raw = ComputeRawLevels(barCount, sampleRate);

        if (_smoothed.Length != barCount)
        {
            _smoothed = new float[barCount];
        }

        for (var i = 0; i < barCount; i++)
        {
            var target = raw[i];
            var current = _smoothed[i];
            var rate = target > current ? Attack : Decay;
            _smoothed[i] = current + ((target - current) * rate);
        }

        return (float[])_smoothed.Clone();
    }

    /// <summary>Levels without smoothing; exposed for tests.</summary>
    public float[] ComputeRawLevels(int barCount, int sampleRate)
    {
        // Oldest sample first, windowed.
        for (var i = 0; i < FftSize; i++)
        {
            _real[i] = _ring[(_writeIndex + i) % FftSize] * Window[i];
            _imag[i] = 0;
        }

        Fft(_real, _imag);

        var nyquist = sampleRate / 2.0;
        var top = Math.Min(MaxFrequency, nyquist);
        var binWidth = (double)sampleRate / FftSize;
        var levels = new float[barCount];

        // Normalises a full-scale sine to about 0 dB (Hann window halves the amplitude).
        const double scale = 4.0 / FftSize;

        for (var bar = 0; bar < barCount; bar++)
        {
            var lowFreq = MinFrequency * Math.Pow(top / MinFrequency, (double)bar / barCount);
            var highFreq = MinFrequency * Math.Pow(top / MinFrequency, (double)(bar + 1) / barCount);

            var lowBin = Math.Max(1, (int)Math.Floor(lowFreq / binWidth));
            var highBin = Math.Min((FftSize / 2) - 1, Math.Max(lowBin, (int)Math.Ceiling(highFreq / binWidth)));

            double peak = 0;
            for (var bin = lowBin; bin <= highBin; bin++)
            {
                var magnitude = Math.Sqrt((_real[bin] * _real[bin]) + (_imag[bin] * _imag[bin])) * scale;
                peak = Math.Max(peak, magnitude);
            }

            var db = 20 * Math.Log10(peak + 1e-12);
            levels[bar] = (float)Math.Clamp((db - FloorDb) / -FloorDb, 0, 1);
        }

        return levels;
    }

    private static double[] CreateHannWindow(int size)
    {
        var window = new double[size];
        for (var i = 0; i < size; i++)
        {
            window[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1)));
        }

        return window;
    }

    /// <summary>In-place iterative radix-2 FFT. Length must be a power of two.</summary>
    private static void Fft(double[] real, double[] imag)
    {
        var n = real.Length;

        // Bit-reversal permutation.
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;

            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var wReal = Math.Cos(angle);
            var wImag = Math.Sin(angle);

            for (var start = 0; start < n; start += length)
            {
                double curReal = 1;
                double curImag = 0;
                var half = length / 2;

                for (var k = 0; k < half; k++)
                {
                    var a = start + k;
                    var b = a + half;
                    var tReal = (real[b] * curReal) - (imag[b] * curImag);
                    var tImag = (real[b] * curImag) + (imag[b] * curReal);

                    real[b] = real[a] - tReal;
                    imag[b] = imag[a] - tImag;
                    real[a] += tReal;
                    imag[a] += tImag;

                    var nextReal = (curReal * wReal) - (curImag * wImag);
                    curImag = (curReal * wImag) + (curImag * wReal);
                    curReal = nextReal;
                }
            }
        }
    }
}
