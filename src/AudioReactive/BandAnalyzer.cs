using System.Security.Cryptography;
using System.Text;

namespace InFalsus.Presentation.AudioReactive;

public static class BandAnalyzer
{
    public const string Algorithm = "centered-periodic-hann-band-rms-v1";

    public static AudioAnalysis Analyze(IPcmSource source, AnalysisSettings settings,
        CancellationToken cancellation = default, IProgress<double>? progress = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        Guard.Source(source.SampleRate, source.Channels, source.FrameCount);
        int count = Guard.Settings(settings, source.SampleRate, source.FrameCount);
        var config = settings.Copy();
        // This extra sequential read provides decoded-content identity, independent of file name/container/volume.
        string fingerprint = Fingerprint(source, cancellation);
        int size = config.WindowSize, channels = source.Channels;
        var fft = new Radix2Fft(size);
        var window = Enumerable.Range(0, size).Select(i => .5 - .5 * Math.Cos(2 * Math.PI * i / size)).ToArray();
        double denominator = size * window.Sum(w => w * w) * channels;
        var weights = config.Bands.Select(b => BinWeights(b, size, source.SampleRate)).ToArray();
        var levels = config.Bands.Select(_ => new float[count]).ToArray();
        var pcm = new float[size * channels];
        var real = new double[size]; var imag = new double[size];
        var powers = new double[config.Bands.Length];
        int reportEvery = Math.Max(1, count / 100);
        progress?.Report(0);
        for (int frame = 0; frame < count; frame++)
        {
            cancellation.ThrowIfCancellationRequested();
            Array.Clear(pcm, 0, pcm.Length); Array.Clear(powers, 0, powers.Length);
            long start = (long)frame * config.HopSize - size / 2;
            long readStart = Math.Max(0, start);
            int offset = (int)(readStart - start);
            int length = (int)Math.Max(0, Math.Min(size - offset, source.FrameCount - readStart));
            if (length > 0)
            {
                int read = source.ReadFrames(readStart, pcm.AsSpan(offset * channels, length * channels));
                if (read != length) throw new InvalidDataException("PCM decoder returned a short read.");
            }
            for (int channel = 0; channel < channels; channel++)
            {
                Array.Clear(imag, 0, size);
                for (int i = 0; i < size; i++) real[i] = pcm[i * channels + channel] * window[i];
                fft.Transform(real, imag);
                for (int band = 0; band < weights.Length; band++)
                {
                    double sum = 0;
                    foreach (var bin in weights[band])
                        sum += (real[bin.Index] * real[bin.Index] + imag[bin.Index] * imag[bin.Index]) * bin.Weight;
                    powers[band] += sum;
                }
            }
            for (int band = 0; band < powers.Length; band++)
            {
                double rms = Math.Sqrt(powers[band] / denominator);
                if (!double.IsFinite(rms) || rms > float.MaxValue) throw new InvalidDataException("Non-finite PCM/spectrum.");
                levels[band][frame] = (float)rms;
            }
            if (frame % reportEvery == 0 || frame == count - 1) progress?.Report((frame + 1.0) / count);
        }
        return AudioAnalysis.FromDocument(new AudioAnalysisDocument {
            SourceFingerprint = fingerprint, CacheKey = CacheKey(fingerprint, source.SampleRate, channels, source.FrameCount, config),
            SampleRate = source.SampleRate, Channels = channels, FrameCount = source.FrameCount, Settings = config, Levels = levels });
    }

    public static string Fingerprint(IPcmSource source, CancellationToken cancellation = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        Guard.Source(source.SampleRate, source.Channels, source.FrameCount);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Header(w => { w.Write("decoded-float-pcm-v1"); w.Write(source.SampleRate); w.Write(source.Channels); w.Write(source.FrameCount); }));
        var pcm = new float[4096 * source.Channels]; var bytes = new byte[pcm.Length * 4];
        for (long first = 0; first < source.FrameCount; first += 4096)
        {
            cancellation.ThrowIfCancellationRequested();
            int frames = (int)Math.Min(4096, source.FrameCount - first), samples = frames * source.Channels;
            if (source.ReadFrames(first, pcm.AsSpan(0, samples)) != frames) throw new InvalidDataException("PCM decoder returned a short read.");
            for (int i = 0; i < samples; i++)
            {
                if (!float.IsFinite(pcm[i])) throw new InvalidDataException("PCM contains NaN/Infinity.");
                int bits = BitConverter.SingleToInt32Bits(pcm[i]);
                for (int b = 0; b < 4; b++) bytes[i * 4 + b] = (byte)(bits >> (8 * b));
            }
            hash.AppendData(bytes, 0, samples * 4);
        }
        return Hex(hash.GetHashAndReset());
    }

    internal static string CacheKey(string fingerprint, int rate, int channels, long frames, AnalysisSettings settings)
    {
        using var hash = SHA256.Create();
        return Hex(hash.ComputeHash(Header(w => {
            w.Write(Algorithm); w.Write(fingerprint); w.Write(rate); w.Write(channels); w.Write(frames);
            w.Write(settings.WindowSize); w.Write(settings.HopSize); w.Write(settings.Bands.Length);
            foreach (var b in settings.Bands) { w.Write(b.Id); w.Write(b.LowHz); w.Write(b.HighHz); }
        })));
    }
    private static byte[] Header(Action<BinaryWriter> write)
    {
        using var memory = new MemoryStream(); using (var writer = new BinaryWriter(memory, Encoding.UTF8, true)) write(writer);
        return memory.ToArray();
    }
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    private static (int Index, double Weight)[] BinWeights(FrequencyBand band, int size, int rate)
    {
        double step = rate / (double)size;
        var bins = new List<(int, double)>();
        for (int k = 0; k <= size / 2; k++)
        {
            double low = Math.Max(0, (k - .5) * step), high = Math.Min(rate / 2.0, (k + .5) * step);
            double overlap = Math.Max(0, Math.Min(high, band.HighHz) - Math.Max(low, band.LowHz));
            if (overlap > 0) bins.Add((k, overlap / (high - low) * (k == 0 || k == size / 2 ? 1 : 2)));
        }
        return bins.ToArray();
    }
}

/// <summary>Independent radix-2 FFT; no native library, NuGet DSP or Unity dependency.</summary>
internal sealed class Radix2Fft
{
    private readonly int[] reverse;
    private readonly double[] cosine, sine;
    internal Radix2Fft(int size)
    {
        reverse = new int[size]; cosine = new double[size / 2]; sine = new double[size / 2];
        int bits = 0; for (int n = size; n > 1; n >>= 1) bits++;
        for (int i = 0; i < size; i++) { int x = i, r = 0; for (int b = 0; b < bits; b++) { r = (r << 1) | (x & 1); x >>= 1; } reverse[i] = r; }
        for (int i = 0; i < cosine.Length; i++) { double a = -2 * Math.PI * i / size; cosine[i] = Math.Cos(a); sine[i] = Math.Sin(a); }
    }
    internal void Transform(double[] real, double[] imag)
    {
        for (int i = 0; i < real.Length; i++)
            if (reverse[i] > i) { int j = reverse[i]; (real[i], real[j]) = (real[j], real[i]); (imag[i], imag[j]) = (imag[j], imag[i]); }
        for (int width = 2; width <= real.Length; width <<= 1)
        {
            int half = width / 2, stride = real.Length / width;
            for (int first = 0; first < real.Length; first += width)
                for (int j = 0; j < half; j++)
                {
                    int a = first + j, b = a + half, phase = j * stride;
                    double tr = real[b] * cosine[phase] - imag[b] * sine[phase];
                    double ti = real[b] * sine[phase] + imag[b] * cosine[phase];
                    real[b] = real[a] - tr; imag[b] = imag[a] - ti; real[a] += tr; imag[a] += ti;
                }
        }
    }
}
