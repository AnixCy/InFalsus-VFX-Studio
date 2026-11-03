using System.Collections.ObjectModel;

namespace InFalsus.Presentation.Authoring;

/// <summary>Host-supplied UI hints. The catalog copies these mutable input objects.</summary>
public sealed class ParameterPresentation
{
    public string Node { get; set; } = "";
    public string Property { get; set; } = "";
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? Group { get; set; }
    public string? Unit { get; set; }
    public string? ControlHint { get; set; }
    public double? SuggestedMin { get; set; }
    public double? SuggestedMax { get; set; }
    public double? Step { get; set; }
    public double? HardMin { get; set; }
    public double? HardMax { get; set; }
    public bool ReadOnly { get; set; }
}

public sealed class ParameterDescriptor
{
    public string Id { get; }
    public ParameterBinding Binding { get; }
    public string Label { get; }
    public string Description { get; }
    public string Group { get; }
    public string Unit { get; }
    public string ControlHint { get; }
    public double? SuggestedMin { get; }
    public double? SuggestedMax { get; }
    public double? Step { get; }
    public double? HardMin { get; }
    public double? HardMax { get; }
    public bool ReadOnly { get; }
    public ParameterValue DefaultValue { get; }
    public IReadOnlyList<string> Choices { get; }
    public bool IsAnimated { get; }
    internal double FirstKeySeconds { get; }

    // Stable under channel reordering. Renaming a node/property deliberately changes its identity.
    public static string MakeId(string node, string property)
        => Uri.EscapeDataString(node) + "/" + Uri.EscapeDataString(property);

    internal ParameterDescriptor(ParameterBinding binding, ChannelDocument channel, TempoMap tempo,
        IEnumerable<string> choices, ParameterPresentation? presentation)
    {
        Binding = binding; Id = MakeId(binding.Node, binding.Property);
        var p = presentation ?? new ParameterPresentation();
        foreach (var value in new[] { p.SuggestedMin, p.SuggestedMax, p.Step, p.HardMin, p.HardMax })
            if (value.HasValue && !double.IsFinite(value.Value))
                throw new ArgumentException(Id + ": presentation bounds must be finite.");
        if (p.SuggestedMin > p.SuggestedMax || p.HardMin > p.HardMax || p.Step <= 0)
            throw new ArgumentException(Id + ": invalid presentation range/step.");
        Label = string.IsNullOrWhiteSpace(p.Label) ? binding.Property : p.Label!;
        Description = p.Description ?? ""; Group = p.Group ?? binding.Node; Unit = p.Unit ?? "";
        ControlHint = p.ControlHint ?? binding.Kind.ToString().ToLowerInvariant();
        SuggestedMin = p.SuggestedMin; SuggestedMax = p.SuggestedMax; Step = p.Step;
        HardMin = p.HardMin; HardMax = p.HardMax;
        ReadOnly = p.ReadOnly || IsClockProperty(binding.Property);
        var v = channel.Default;
        DefaultValue = binding.Kind == ParameterKind.Choice ? new(default, channel.DefaultChoice) :
            binding.Kind == ParameterKind.Float ? new(new((float)v[0], 0, 0, 0)) :
            new(new((float)v[0], (float)v[1], (float)v[2], (float)v[3]));
        Choices = Array.AsReadOnly(choices.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        IsAnimated = channel.Keys.Length != 0;
        FirstKeySeconds = IsAnimated
            ? channel.Keys[0].Seconds ?? tempo.ToSeconds(channel.Keys[0].Beat!.Value)
            : double.PositiveInfinity;
    }

    public static bool IsClockProperty(string property) => property is
        "_PresentationTime" or "_PresentationBeat" or "_PresentationTick" or "_PresentationSeed" or "_PresentationRate";

    internal void ValidateOverride(ParameterValue value)
    {
        if (ReadOnly) throw new PreviewEditException(PreviewEditError.ReadOnlyParameter, Id);
        if (Binding.Kind == ParameterKind.Choice)
        {
            if (value.Choice == null || !Choices.Contains(value.Choice) || value.Number != default)
                throw new PreviewEditException(PreviewEditError.InvalidChoice, Id);
            return;
        }
        var v = value.Number;
        if (value.Choice != null || !float.IsFinite(v.X) || !float.IsFinite(v.Y)
            || !float.IsFinite(v.Z) || !float.IsFinite(v.W))
            throw new PreviewEditException(PreviewEditError.InvalidValue, Id);
        if (Binding.Kind == ParameterKind.Float && (v.Y != 0 || v.Z != 0 || v.W != 0))
            throw new PreviewEditException(PreviewEditError.InvalidValue, Id);
        var count = Binding.Kind == ParameterKind.Float ? 1 : 4;
        for (var i = 0; i < count; i++)
        {
            var n = i == 0 ? v.X : i == 1 ? v.Y : i == 2 ? v.Z : v.W;
            if (n < HardMin || n > HardMax)
                throw new PreviewEditException(PreviewEditError.OutsideHardRange, Id);
        }
    }
}

/// <summary>Declared runtime channels plus optional author-facing hints; no Unity or UI dependency.</summary>
public sealed class ParameterCatalog
{
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
    public IReadOnlyList<ParameterDescriptor> Parameters { get; }
    internal PreparedTimeline Timeline { get; }

    public ParameterCatalog(EffectPackage package, IEnumerable<ParameterPresentation>? presentation = null)
    {
        if (package == null) throw new ArgumentNullException(nameof(package));
        Timeline = package.Timeline;
        var hints = new Dictionary<string, ParameterPresentation>(StringComparer.Ordinal);
        foreach (var p in presentation ?? Array.Empty<ParameterPresentation>())
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Node) || string.IsNullOrWhiteSpace(p.Property))
                throw new ArgumentException("Presentation requires node and property.");
            var id = ParameterDescriptor.MakeId(p.Node, p.Property);
            if (!hints.TryAdd(id, p)) throw new ArgumentException("Duplicate presentation: " + id);
        }
        var descriptors = new ParameterDescriptor[Timeline.Count];
        for (var i = 0; i < descriptors.Length; i++)
        {
            var binding = Timeline.Bindings[i];
            var channel = package.Document.Channels[i];
            if (binding != new ParameterBinding(channel.Node, channel.Property, channel.Kind))
                throw new ArgumentException("Package document changed after compilation; reload it before editing.");
            var node = package.Document.Nodes.Single(n => n.Id == binding.Node);
            var id = ParameterDescriptor.MakeId(binding.Node, binding.Property);
            hints.TryGetValue(id, out var hint);
            var descriptor = new ParameterDescriptor(binding, channel, package.Tempo,
                binding.Kind == ParameterKind.Choice ? node.Textures.Keys : Array.Empty<string>(), hint);
            descriptors[i] = descriptor; _indices.Add(id, i); hints.Remove(id);
        }
        if (hints.Count != 0) throw new ArgumentException("Unknown presentation parameter: " + hints.Keys.First());
        Parameters = new ReadOnlyCollection<ParameterDescriptor>(descriptors);
    }

    public ParameterDescriptor Get(string id) => Parameters[IndexOf(id)];
    internal int IndexOf(string id) => id != null && _indices.TryGetValue(id, out var i) ? i :
        throw new PreviewEditException(PreviewEditError.UnknownParameter, id);
}
