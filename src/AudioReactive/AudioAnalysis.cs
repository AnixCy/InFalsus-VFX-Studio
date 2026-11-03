namespace InFalsus.Presentation.AudioReactive;

/// <summary>Validated immutable cache. Serializers remain host-owned, as in the presentation core.</summary>
public sealed class AudioAnalysis
{
    private readonly AudioAnalysisDocument data;
    public string SourceFingerprint => data.SourceFingerprint;
    public string CacheKey => data.CacheKey;
    public int SampleRate => data.SampleRate;
    public int Channels => data.Channels;
    public long FrameCount => data.FrameCount;
    public double DurationSeconds => FrameCount / (double)SampleRate;
    public double StepSeconds => data.Settings.HopSize / (double)SampleRate;
    public int SampleCount => data.Levels[0].Length;
    public IReadOnlyList<string> BandIds { get; }
    private AudioAnalysis(AudioAnalysisDocument data)
    {
        this.data = Copy(data);
        BandIds = Array.AsReadOnly(data.Settings.Bands.Select(b => b.Id).ToArray());
    }
    public static AudioAnalysis FromDocument(AudioAnalysisDocument document)
    {
        if (document == null || document.FormatVersion != 1 || document.Algorithm != BandAnalyzer.Algorithm
            || document.SourceFingerprint == null || document.SourceFingerprint.Length != 64
            || document.SourceFingerprint.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Unknown analysis format/algorithm or invalid PCM fingerprint.");
        Guard.Source(document.SampleRate, document.Channels, document.FrameCount);
        int count = Guard.Settings(document.Settings, document.SampleRate, document.FrameCount);
        if (document.CacheKey != BandAnalyzer.CacheKey(document.SourceFingerprint, document.SampleRate, document.Channels, document.FrameCount, document.Settings)
            || document.Levels == null || document.Levels.Length != document.Settings.Bands.Length
            || document.Levels.Any(b => b == null || b.Length != count || b.Any(v => !float.IsFinite(v) || v < 0)))
            throw new ArgumentException("Cache identity, dimensions or RMS levels are invalid.");
        return new AudioAnalysis(document);
    }
    public bool Matches(string decodedPcmFingerprint, AnalysisSettings settings)
    {
        Guard.Settings(settings, SampleRate, FrameCount);
        return CacheKey == BandAnalyzer.CacheKey(decodedPcmFingerprint, SampleRate, Channels, FrameCount, settings);
    }
    public AudioAnalysisDocument ToDocument() => Copy(data);
    public float[] CopyLevels(string bandId) => (float[])data.Levels[Index(bandId)].Clone();
    public double SampleLevel(string bandId, double audioSeconds)
        => Sample(data.Levels[Index(bandId)], StepSeconds, DurationSeconds, audioSeconds);
    internal static double Sample(float[] samples, double step, double duration, double time)
    {
        if (!double.IsFinite(time)) throw new ArgumentException("Signal time must be finite.");
        if (time < 0 || time >= duration) return 0;
        double position = time / step;
        int i = Math.Min(samples.Length - 1, (int)position), next = Math.Min(samples.Length - 1, i + 1);
        return samples[i] + (samples[next] - samples[i]) * Math.Min(1, position - i);
    }
    private int Index(string id)
    {
        int i = Array.FindIndex(data.Settings.Bands, b => b.Id == id);
        return i >= 0 ? i : throw new ArgumentException("Unknown audio band: " + id);
    }
    private static AudioAnalysisDocument Copy(AudioAnalysisDocument d) => new() {
        FormatVersion = d.FormatVersion, Algorithm = d.Algorithm, SourceFingerprint = d.SourceFingerprint, CacheKey = d.CacheKey,
        SampleRate = d.SampleRate, Channels = d.Channels, FrameCount = d.FrameCount, Settings = d.Settings.Copy(),
        Levels = d.Levels.Select(b => (float[])b.Clone()).ToArray() };
}

public sealed class AudioEnvelope
{
    private readonly float[] values;
    public double StepSeconds { get; }
    public double DurationSeconds { get; }
    public int Count => values.Length;
    private AudioEnvelope(float[] values, double step, double duration) => (this.values, StepSeconds, DurationSeconds) = (values, step, duration);
    public static AudioEnvelope Create(AudioAnalysis analysis, string bandId, EnvelopeSettings settings)
    {
        if (analysis == null || settings == null) throw new ArgumentNullException();
        settings = settings.Copy();
        settings.Validate();
        var input = analysis.CopyLevels(bandId); var output = new float[input.Length];
        double state = 0, step = analysis.StepSeconds;
        for (int i = 0; i < input.Length; i++)
        {
            double db = 20 * Math.Log10(Math.Max(input[i], 1e-20)) + settings.GainDb;
            double target = Math.Pow(Math.Min(1, Math.Max(0, (db - settings.FloorDb) / (settings.CeilingDb - settings.FloorDb))), settings.Exponent);
            double tau = target > state ? settings.AttackSeconds : settings.ReleaseSeconds;
            double decay = tau == 0 ? 0 : Math.Exp(-step / tau);
            state = target + (state - target) * decay;
            output[i] = (float)state;
        }
        return new AudioEnvelope(output, step, analysis.DurationSeconds);
    }
    public double Sample(double audioSeconds) => AudioAnalysis.Sample(values, StepSeconds, DurationSeconds, audioSeconds);
    public float[] CopyValues() => (float[])values.Clone();
}
