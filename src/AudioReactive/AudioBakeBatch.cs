namespace InFalsus.Presentation.AudioReactive;

public sealed class AudioBakeChannelSummary
{
    public ParameterBinding Binding { get; }
    public int KeyCount { get; }
    public int EvaluationSamples { get; }
    public double MeasuredMaximumError { get; }
    internal AudioBakeChannelSummary(AudioParameterDriver driver, AudioBakeResult result)
        => (Binding, KeyCount, EvaluationSamples, MeasuredMaximumError) = (driver.Binding, result.Channel.Keys.Length, result.EvaluationSamples, result.MeasuredMaximumError);
}
public sealed class AudioBakeBatchResult
{
    private readonly ChannelDocument[] channels;
    public IReadOnlyList<AudioBakeChannelSummary> BakedChannels { get; }
    public int TotalKeys { get; }
    internal AudioBakeBatchResult(ChannelDocument[] channels, List<AudioBakeChannelSummary> summaries)
    {
        this.channels = channels; BakedChannels = summaries.AsReadOnly(); TotalKeys = channels.Sum(c => c.Keys.Length);
    }
    public ChannelDocument[] ToChannels() => channels.Select(AudioBakeBatch.Copy).ToArray();
}

/// <summary>All-or-nothing creator batch. No project/session is edited; no recipes or manual curves are discarded.</summary>
public static class AudioBakeBatch
{
    public static AudioBakeBatchResult Bake(IReadOnlyList<ChannelDocument> manualChannels, TempoMap tempo,
        IReadOnlyList<AudioParameterDriver> drivers, BakeSettings settings, int maximumTotalKeys = 100_000,
        CancellationToken cancellation = default)
    {
        if (manualChannels == null || tempo == null || drivers == null || settings == null) throw new ArgumentNullException();
        if (manualChannels.Count > 4096 || drivers.Count > 64 || maximumTotalKeys < 2 || maximumTotalKeys > 1_000_000)
            throw new ArgumentException("Batch channel/driver/key budget exceeded.");
        if (manualChannels.Any(c => c == null || c.Keys == null) || manualChannels.Sum(c => (long)c.Keys.Length) > 1_000_000)
            throw new ArgumentException("Invalid source or source-key budget exceeded.");
        cancellation.ThrowIfCancellationRequested();
        settings = settings.Copy(); settings.Validate();
        var channels = manualChannels.Select(Copy).ToArray(); var plan = drivers.ToArray();
        var index = new Dictionary<(string, string), int>();
        for (int i = 0; i < channels.Length; i++)
            if (!index.TryAdd((channels[i].Node, channels[i].Property), i)) throw new ArgumentException("Duplicate channel binding.");
        var used = new HashSet<(string, string)>();
        // Preflight the whole plan before expensive work. One driver per scalar binding is explicit in v1.
        foreach (var driver in plan)
        {
            if (driver == null || !used.Add((driver.Binding.Node, driver.Binding.Property))) throw new ArgumentException("Duplicate/null audio driver.");
            if (!index.TryGetValue((driver.Binding.Node, driver.Binding.Property), out int at)) throw new ArgumentException("Audio target is not declared by the source graph.");
            if (AudioCurveBaker.Compile(channels[at], tempo).Binding != driver.Binding) throw new ArgumentException("Audio target must be a matching scalar channel.");
        }
        long total = channels.Where(c => !used.Contains((c.Node, c.Property))).Sum(c => (long)c.Keys.Length);
        if (total > maximumTotalKeys) throw new ArgumentException("Unchanged channels exceed the total key budget.");
        var summaries = new List<AudioBakeChannelSummary>();
        foreach (var driver in plan)
        {
            cancellation.ThrowIfCancellationRequested();
            int remaining = maximumTotalKeys - (int)total;
            if (remaining < 2) throw new ArgumentException("Batch key budget exhausted.");
            var limits = settings.Copy(); limits.MaximumKeys = Math.Min(limits.MaximumKeys, remaining);
            int at = index[(driver.Binding.Node, driver.Binding.Property)];
            var result = AudioCurveBaker.Bake(channels[at], tempo, driver, limits, cancellation);
            channels[at] = result.Channel; total += result.Channel.Keys.Length;
            summaries.Add(new AudioBakeChannelSummary(driver, result));
        }
        cancellation.ThrowIfCancellationRequested();
        return new AudioBakeBatchResult(channels, summaries);
    }
    internal static ChannelDocument Copy(ChannelDocument c)
    {
        if (c == null || string.IsNullOrWhiteSpace(c.Node) || string.IsNullOrWhiteSpace(c.Property) || c.Keys == null || c.Default == null
            || c.Keys.Any(k => k == null || k.Value == null || k.Curve?.Any(p => p == null) == true)) throw new ArgumentException("Invalid source channel data.");
        return new ChannelDocument { Node = c.Node, Property = c.Property, Kind = c.Kind, Default = (double[])c.Default.Clone(), DefaultChoice = c.DefaultChoice,
            Keys = c.Keys.Select(k => new KeyDocument { Seconds = k.Seconds, Beat = k.Beat, Value = (double[])k.Value.Clone(), Choice = k.Choice, Ease = k.Ease,
                Curve = k.Curve?.Select(p => new CurvePointDocument { X = p.X, Y = p.Y }).ToArray() }).ToArray() };
    }
}
