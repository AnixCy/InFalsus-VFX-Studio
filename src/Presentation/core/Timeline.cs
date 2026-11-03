using System.Numerics;

namespace InFalsus.Presentation;

public enum ParameterKind { Float, Vector, Color, Choice }
public enum Easing { Linear, Hold, Smooth, SineInOut, CubicInOut, BackOut, Curve }
public readonly record struct CurvePoint(double X, double Y);
public readonly record struct ParameterValue(Vector4 Number, string? Choice = null);
public readonly record struct ParameterBinding(string Node, string Property, ParameterKind Kind);
public sealed record TimelineKey(double Seconds, ParameterValue Value,
    Easing Ease = Easing.Linear, CurvePoint[]? Curve = null);

public sealed class ChannelTrack
{
    public ParameterBinding Binding { get; }
    private readonly ParameterValue _default;
    private readonly TimelineKey[] _keys;
    private readonly double[] _times;

    public ChannelTrack(ParameterBinding binding, ParameterValue defaultValue, IEnumerable<TimelineKey> keys)
    {
        if (string.IsNullOrWhiteSpace(binding.Node) || string.IsNullOrWhiteSpace(binding.Property))
            throw new ArgumentException("Channel requires node and property.");
        Binding = binding; _default = defaultValue;
        _keys = keys.Select(k => k with { Curve = k.Curve?.ToArray() }).ToArray();
        _times = _keys.Select(k => k.Seconds).ToArray();
        CheckValue(defaultValue);
        for (var i = 0; i < _keys.Length; i++)
        {
            var k = _keys[i];
            if (!double.IsFinite(k.Seconds) || i > 0 && k.Seconds <= _times[i - 1])
                throw new ArgumentException($"{binding.Property}: key times must strictly increase.");
            CheckValue(k.Value);
            if (binding.Kind == ParameterKind.Choice && k.Ease != Easing.Hold)
                throw new ArgumentException("Discrete resource choices require hold interpolation.");
            if (k.Ease == Easing.Curve)
            {
                var points = k.Curve;
                if (points == null || points.Length < 2 || points[0].X != 0 || points[^1].X != 1)
                    throw new ArgumentException("Custom curve must run from x=0 to x=1.");
                for (var j = 0; j < points.Length; j++)
                    if (!double.IsFinite(points[j].X) || !double.IsFinite(points[j].Y)
                        || j > 0 && points[j].X <= points[j - 1].X)
                        throw new ArgumentException("Custom curve requires finite values and increasing x.");
            }
        }
    }

    private void CheckValue(ParameterValue v)
    {
        if (Binding.Kind == ParameterKind.Choice)
        {
            if (string.IsNullOrWhiteSpace(v.Choice)) throw new ArgumentException("Choice must name a resource.");
        }
        else if (!float.IsFinite(v.Number.X) || !float.IsFinite(v.Number.Y)
                 || !float.IsFinite(v.Number.Z) || !float.IsFinite(v.Number.W))
            throw new ArgumentException("Parameter value must be finite.");
    }

    public ParameterValue Sample(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentException("Invalid timeline time.");
        var i = Array.BinarySearch(_times, seconds);
        if (i < 0) i = ~i - 1;
        if (i < 0) return _default;
        var left = _keys[i];
        if (i == _keys.Length - 1 || Binding.Kind == ParameterKind.Choice) return left.Value;
        var right = _keys[i + 1];
        var u = Math.Clamp((seconds - left.Seconds) / (right.Seconds - left.Seconds), 0, 1);
        var value = new ParameterValue(Vector4.Lerp(left.Value.Number, right.Value.Number, (float)Ease(left, u)));
        CheckValue(value); // Preserve overshoot, but never send NaN/Infinity to a shader.
        return value;
    }

    private static double Ease(TimelineKey k, double u) => k.Ease switch
    {
        Easing.Hold => 0,
        Easing.Smooth => u * u * (3 - 2 * u),
        Easing.SineInOut => (1 - Math.Cos(Math.PI * u)) / 2,
        Easing.CubicInOut => u < .5 ? 4 * u * u * u : 1 - Math.Pow(-2 * u + 2, 3) / 2,
        Easing.BackOut => 1 + 2.70158 * Math.Pow(u - 1, 3) + 1.70158 * Math.Pow(u - 1, 2),
        Easing.Curve => Curve(k.Curve!, u),
        _ => u
    };
    private static double Curve(CurvePoint[] points, double u)
    {
        var lo = 0; var hi = points.Length - 1;
        while (lo + 1 < hi) { var mid = (lo + hi) / 2; if (points[mid].X <= u) lo = mid; else hi = mid; }
        var a = points[lo]; var b = points[hi];
        return a.Y + (b.Y - a.Y) * (u - a.X) / (b.X - a.X);
    }
}

public sealed class PreparedTimeline
{
    private readonly ChannelTrack[] _channels;
    public IReadOnlyList<ParameterBinding> Bindings { get; }
    public int Count => _channels.Length;
    public PreparedTimeline(IEnumerable<ChannelTrack> channels)
    {
        _channels = channels.ToArray();
        if (_channels.GroupBy(c => (c.Binding.Node, c.Binding.Property)).Any(g => g.Count() > 1))
            throw new ArgumentException("Only one track per node/property is supported.");
        Bindings = Array.AsReadOnly(_channels.Select(c => c.Binding).ToArray());
    }
    public void Sample(double seconds, Span<ParameterValue> output)
    {
        if (output.Length != Count) throw new ArgumentException("Incorrect sample buffer length.");
        for (var i = 0; i < Count; i++) output[i] = _channels[i].Sample(seconds);
    }
}
