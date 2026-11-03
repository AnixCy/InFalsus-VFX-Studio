using System.Numerics;

namespace InFalsus.Presentation.AudioReactive;

public sealed class AudioBakeResult
{
    /// <summary>Fresh scalar channel, in seconds; input keyframes/recipes remain unchanged.</summary>
    public ChannelDocument Channel { get; }
    public int EvaluationSamples { get; }
    public double MeasuredMaximumError { get; }
    internal AudioBakeResult(ChannelDocument channel, int count, double error)
        => (Channel, EvaluationSamples, MeasuredMaximumError) = (channel, count, error);
}

public static class AudioCurveBaker
{
    /// <summary>Samples creator evaluation, preserves manual breakpoints and reduces by vertical value error.
    /// Error is measured on the candidate/validation lattice, not an analytic bound for arbitrary frequencies.
    /// The returned channel describes only the requested interval; the host owns placement/outside behavior.</summary>
    public static AudioBakeResult Bake(ChannelDocument source, TempoMap tempo, AudioParameterDriver driver,
        BakeSettings settings, CancellationToken cancellation = default)
    {
        if (source == null || tempo == null || driver == null || settings == null) throw new ArgumentNullException();
        settings = settings.Copy();
        settings.Validate();
        source = Snapshot(source);
        var track = Compile(source, tempo);
        if (track.Binding != driver.Binding) throw new ArgumentException("Baking requires the matching scalar binding.");
        double start = settings.StartSeconds, end = settings.EndSeconds;
        double uniformCount = Math.Ceiling((end - start) * settings.SamplesPerSecond);
        if (uniformCount > settings.MaximumSamples - 1) throw new ArgumentException("Bake sample budget exceeded.");
        var times = new SortedSet<double> { start, end };
        var pinned = new HashSet<double> { start, end };
        var holds = new HashSet<double>();
        void Add(double t, bool pin = false)
        {
            if (t < start || t > end) return;
            // Collapse lattice roundoff, while retaining the exact original time of pinned manual keys.
            var near = times.GetViewBetween(Math.Max(start, t - 1e-10), Math.Min(end, t + 1e-10));
            if (near.Count != 0)
            {
                double existing = near.Min;
                if (existing == t) { if (pin) pinned.Add(t); return; }
                if (!pin) return;
                if (pinned.Contains(existing)) throw new ArgumentException("Pinned bake times are too close to distinguish.");
                times.Remove(existing);
            }
            times.Add(t); if (pin) pinned.Add(t);
            if (times.Count > settings.MaximumSamples) throw new ArgumentException("Bake sample budget exceeded.");
        }
        for (int i = 1; i < (int)uniformCount; i++) Add(start + i / settings.SamplesPerSecond);
        // Include the source envelope lattice: compression must not overlook a peak between output-rate ticks.
        for (int i = 0; i < driver.Envelope.Count; i++) Add(driver.AudioOriginSeconds + i * driver.Envelope.StepSeconds);
        var baseTimes = source.Keys.Select(k => k.Seconds ?? tempo.ToSeconds(k.Beat!.Value)).ToArray();
        for (int i = 0; i < baseTimes.Length; i++) Add(baseTimes[i], true);
        var jumps = new SortedSet<double> { driver.AudioOriginSeconds, driver.AudioOriginSeconds + driver.Envelope.DurationSeconds };
        if (driver.Blend != AudioBlend.Replace)
        {
            // Before the first key the core returns Default, which may differ from that key's value.
            if (baseTimes.Length > 0) jumps.Add(baseTimes[0]);
            for (int i = 1; i < baseTimes.Length; i++) if (source.Keys[i - 1].Ease == Easing.Hold) jumps.Add(baseTimes[i]);
        }
        // Isolate step discontinuities instead of turning them into a visible linear ramp.
        // A <= 1 us guard also bounds the small frozen interval for a simultaneous moving envelope.
        foreach (double jump in jumps.Where(t => t > start && t <= end))
        {
            Add(jump, true);
            double previous = times.GetViewBetween(start, jump).Reverse().First(t => t < jump);
            double guard = jump - Math.Min(1e-6, (jump - previous) / 4);
            if (!(guard > previous && guard < jump)) throw new ArgumentException("Discontinuity exceeds time precision.");
            Add(guard, true); holds.Add(guard);
        }
        int evaluations = 0;
        double Evaluate(double t) { cancellation.ThrowIfCancellationRequested(); evaluations++; return driver.Sample(t, track); }
        // A second half-rate lattice tests curvature between the first samples; retries add only failures.
        var initial = times.ToArray();
        for (int i = 0; i + 1 < initial.Length; i++) if (!holds.Contains(initial[i])) Add(initial[i] + (initial[i + 1] - initial[i]) / 2);
        for (int attempt = 0; attempt < 12; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            var t = times.ToArray(); var value = t.Select(Evaluate).ToArray();
            bool[] keep = Reduce(t, value, pinned, settings.MaximumError * .8, cancellation);
            int keyCount = keep.Count(k => k);
            if (keyCount > settings.MaximumKeys) throw new ArgumentException("Baked key budget exceeded; increase error or shorten the interval.");
            var keys = Enumerable.Range(0, t.Length).Where(i => keep[i]).Select(i => new KeyDocument {
                Seconds = t[i], Value = new[] { value[i] }, Ease = holds.Contains(t[i]) ? Easing.Hold : Easing.Linear }).ToArray();
            var channel = new ChannelDocument { Node = source.Node, Property = source.Property, Kind = ParameterKind.Float,
                Default = (double[])source.Default.Clone(), Keys = keys };
            var baked = Compile(channel, tempo);
            double maximum = 0;
            var failures = new List<double>();
            void Check(double at, double expected)
            {
                double error = Math.Abs(baked.Sample(at).Number.X - expected);
                maximum = Math.Max(maximum, error);
                if (error > settings.MaximumError) failures.Add(at);
            }
            for (int i = 0; i < t.Length; i++)
            {
                Check(t[i], value[i]);
                if (i + 1 < t.Length)
                {
                    double mid = t[i] + (t[i + 1] - t[i]) / 2;
                    Check(mid, Evaluate(mid));
                }
            }
            if (failures.Count == 0) return new AudioBakeResult(channel, evaluations, maximum);
            bool grew = false;
            foreach (double failed in failures)
            {
                if (times.Contains(failed)) continue;
                Add(failed); grew = true;
            }
            if (!grew) throw new InvalidOperationException("Requested error is below Shader float precision or a discontinuity guard; relax the tolerance.");
        }
        throw new InvalidOperationException("Bake validation did not converge within its bounded refinement budget.");
    }

    public static ChannelTrack Compile(ChannelDocument source, TempoMap tempo)
    {
        if (source == null || tempo == null || source.Kind != ParameterKind.Float || source.Keys == null
            || string.IsNullOrWhiteSpace(source.Node) || string.IsNullOrWhiteSpace(source.Property))
            throw new ArgumentException("Audio driving currently supports scalar channels only.");
        static ParameterValue Scalar(double[]? values)
        {
            if (values == null || values.Length != 1 || !double.IsFinite(values[0]) || Math.Abs(values[0]) > float.MaxValue)
                throw new ArgumentException("Scalar channel requires one finite Shader float.");
            return new ParameterValue(new Vector4((float)values[0], 0, 0, 0));
        }
        var keys = source.Keys.Select(k => {
            if (k == null || k.Seconds.HasValue == k.Beat.HasValue || !Enum.IsDefined(typeof(Easing), k.Ease))
                throw new ArgumentException("Invalid scalar key time/easing.");
            return new TimelineKey(k.Seconds ?? tempo.ToSeconds(k.Beat!.Value), Scalar(k.Value), k.Ease,
                k.Curve?.Select(p => new CurvePoint(p.X, p.Y)).ToArray());
        });
        return new ChannelTrack(new ParameterBinding(source.Node, source.Property, source.Kind), Scalar(source.Default), keys);
    }

    private static ChannelDocument Snapshot(ChannelDocument source)
    {
        if (source.Keys == null || source.Default == null || source.Keys.Any(k => k == null || k.Value == null || k.Curve?.Any(p => p == null) == true))
            throw new ArgumentException("Scalar channel/key data is missing.");
        return new ChannelDocument { Node = source.Node, Property = source.Property, Kind = source.Kind, Default = (double[])source.Default.Clone(),
            Keys = source.Keys.Select(k => new KeyDocument { Seconds = k.Seconds, Beat = k.Beat, Value = (double[])k.Value.Clone(), Ease = k.Ease,
                Curve = k.Curve?.Select(p => new CurvePointDocument { X = p.X, Y = p.Y }).ToArray() }).ToArray() };
    }

    private static bool[] Reduce(double[] times, double[] values, HashSet<double> pinned, double tolerance, CancellationToken cancellation)
    {
        var keep = times.Select(pinned.Contains).ToArray();
        var forced = Enumerable.Range(0, times.Length).Where(i => keep[i]).ToArray();
        var stack = new Stack<(int First, int Last)>();
        for (int i = 0; i + 1 < forced.Length; i++) stack.Push((forced[i], forced[i + 1]));
        while (stack.Count != 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var (first, last) = stack.Pop(); double error = tolerance; int split = -1;
            for (int i = first + 1; i < last; i++)
            {
                double fraction = (times[i] - times[first]) / (times[last] - times[first]);
                double delta = Math.Abs(values[i] - (values[first] + (values[last] - values[first]) * fraction));
                if (delta > error) { error = delta; split = i; }
            }
            if (split < 0) continue;
            keep[split] = true; stack.Push((first, split)); stack.Push((split, last));
        }
        return keep;
    }
}
