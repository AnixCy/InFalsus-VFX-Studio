namespace InFalsus.Presentation.AudioReactive;

/// <summary>Decoded, interleaved, full-scale PCM. ReadFrames fills complete frames from an absolute position.
/// The caller owns the decoder and keeps its content unchanged during analysis; no device playback is involved.</summary>
public interface IPcmSource
{
    int SampleRate { get; }
    int Channels { get; }
    long FrameCount { get; }
    int ReadFrames(long firstFrame, Span<float> interleaved);
}

public sealed class FrequencyBand
{
    public string Id { get; set; } = "low";
    public double LowHz { get; set; } = 40;
    public double HighHz { get; set; } = 150;
    internal FrequencyBand Copy() => new() { Id = Id, LowHz = LowHz, HighHz = HighHz };
}

public sealed class AnalysisSettings
{
    public int WindowSize { get; set; } = 2048;
    public int HopSize { get; set; } = 400;
    public FrequencyBand[] Bands { get; set; } = new[] { new FrequencyBand() };
    internal AnalysisSettings Copy() => new() { WindowSize = WindowSize, HopSize = HopSize, Bands = Bands.Select(b => b.Copy()).ToArray() };
}

/// <summary>Host-serialized analysis cache, separate from authoring recipes and player packages.</summary>
public sealed class AudioAnalysisDocument
{
    public int FormatVersion { get; set; } = 1;
    public string Algorithm { get; set; } = BandAnalyzer.Algorithm;
    public string SourceFingerprint { get; set; } = "";
    public string CacheKey { get; set; } = "";
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public long FrameCount { get; set; }
    public AnalysisSettings Settings { get; set; } = new();
    public float[][] Levels { get; set; } = Array.Empty<float[]>();
}

public sealed class EnvelopeSettings
{
    public double FloorDb { get; set; } = -60;
    public double CeilingDb { get; set; } = -6;
    public double GainDb { get; set; }
    public double AttackSeconds { get; set; } = .01;
    public double ReleaseSeconds { get; set; } = .15;
    public double Exponent { get; set; } = 1;
    internal EnvelopeSettings Copy() => (EnvelopeSettings)MemberwiseClone();
    internal void Validate()
    {
        if (!Guard.Finite(FloorDb, CeilingDb, GainDb, AttackSeconds, ReleaseSeconds, Exponent)
            || FloorDb < -240 || CeilingDb > 120 || CeilingDb <= FloorDb || Math.Abs(GainDb) > 120
            || AttackSeconds < 0 || ReleaseSeconds < 0 || AttackSeconds > 60 || ReleaseSeconds > 60
            || Exponent < .05 || Exponent > 20)
            throw new ArgumentException("Invalid dB range, gain, attack/release or response exponent.");
    }
}

public enum AudioBlend { Replace, Add, Multiply }

public sealed class ParameterMapping
{
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 1;
    public AudioBlend Blend { get; set; } = AudioBlend.Add;
    internal ParameterMapping Copy() => (ParameterMapping)MemberwiseClone();
    internal void Validate()
    {
        if (!Guard.Finite(Minimum, Maximum) || Math.Abs(Minimum) > float.MaxValue || Math.Abs(Maximum) > float.MaxValue
            || !Enum.IsDefined(typeof(AudioBlend), Blend)) throw new ArgumentException("Invalid output mapping.");
    }
}

/// <summary>Retain this sidecar to edit/rebake. AudioOriginSeconds is the timeline time of PCM frame zero;
/// set it once from the host's audio/chart offset. No SV or wall clock is used.</summary>
public sealed class AudioDriverRecipe
{
    public int FormatVersion { get; set; } = 1;
    public string AnalysisCacheKey { get; set; } = "";
    public string BandId { get; set; } = "low";
    public string Node { get; set; } = "background";
    public string Property { get; set; } = "_Brightness";
    public double AudioOriginSeconds { get; set; }
    public EnvelopeSettings Envelope { get; set; } = new();
    public ParameterMapping Mapping { get; set; } = new();
}

public sealed class BakeSettings
{
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double SamplesPerSecond { get; set; } = 120;
    public double MaximumError { get; set; } = .001;
    public int MaximumSamples { get; set; } = 200_000;
    public int MaximumKeys { get; set; } = 100_000;
    internal BakeSettings Copy() => (BakeSettings)MemberwiseClone();
    internal void Validate()
    {
        if (!Guard.Finite(StartSeconds, EndSeconds, SamplesPerSecond, MaximumError) || StartSeconds < 0
            || EndSeconds <= StartSeconds || SamplesPerSecond < 1 || SamplesPerSecond > 1000 || MaximumError <= 0
            || MaximumSamples < 2 || MaximumSamples > 1_000_000 || MaximumKeys < 2 || MaximumKeys > MaximumSamples)
            throw new ArgumentException("Invalid bake interval, rate, error or sample/key budget.");
    }
}

internal static class Guard
{
    internal static bool IsDigest(string? value) => value != null && value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Finite(params double[] values) => values.All(double.IsFinite);
    internal static void Source(int rate, int channels, long frames)
    {
        if (rate < 8000 || rate > 384000 || channels < 1 || channels > 8 || frames <= 0 || frames > (long)rate * 86400)
            throw new ArgumentException("PCM requires 8–384 kHz, 1–8 channels and nonempty content up to 24 hours.");
    }
    internal static int Settings(AnalysisSettings settings, int rate, long frames)
    {
        if (settings == null || settings.WindowSize < 128 || settings.WindowSize > 32768
            || (settings.WindowSize & (settings.WindowSize - 1)) != 0 || settings.HopSize < 1
            || settings.HopSize > settings.WindowSize || settings.Bands == null || settings.Bands.Length is < 1 or > 32)
            throw new ArgumentException("Invalid FFT window/hop or band count.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in settings.Bands)
            if (b == null || string.IsNullOrWhiteSpace(b.Id) || b.Id.Length > 80 || !ids.Add(b.Id)
                || !Finite(b.LowHz, b.HighHz) || b.LowHz < 0 || b.HighHz <= b.LowHz || b.HighHz > rate / 2.0)
                throw new ArgumentException("Band IDs must be unique; frequency ranges must be inside Nyquist.");
        long count = frames / settings.HopSize + (frames % settings.HopSize == 0 ? 1 : 2);
        if (count > 1_000_000 || count * settings.Bands.Length > 8_000_000)
            throw new ArgumentException("Analysis sample budget exceeded; increase HopSize or reduce the band count.");
        return (int)count;
    }
}
