using System.Numerics;
#if !PRESENTATION_PORTABLE
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

namespace InFalsus.Presentation;

public sealed class PackageDocument
{
    public int FormatVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string SongKey { get; set; } = "";
    public int? Difficulty { get; set; }
    public string? ChartFingerprint { get; set; }
    public TimeDomain TimeDomain { get; set; } = TimeDomain.Audio;
    public double OffsetSeconds { get; set; }
    public uint Seed { get; set; } = 1;
    public string UnityVersion { get; set; } = "6000.3.9f1";
    public string Platform { get; set; } = "StandaloneWindows64";
    public string ColorSpace { get; set; } = "Gamma";
    public bool AllowSpecialScene { get; set; }
    public string[] Requires { get; set; } = Array.Empty<string>();
    public TempoDocument[] Tempo { get; set; } = new[] { new TempoDocument() };
    public MaterialNodeDocument[] Nodes { get; set; } = Array.Empty<MaterialNodeDocument>();
    public ChannelDocument[] Channels { get; set; } = Array.Empty<ChannelDocument>();
}
public sealed class TempoDocument { public double Seconds { get; set; } public double Bpm { get; set; } = 120; }
public sealed class MaterialNodeDocument
{
    public string Id { get; set; } = "background";
    public string Target { get; set; } = "background";
    public string Bundle { get; set; } = "";
    public string Material { get; set; } = "";
    public string EffectId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public double StartSeconds { get; set; }
    public double? EndSeconds { get; set; }
    public Dictionary<string, string> Textures { get; set; } = new();
}
public sealed class ChannelDocument
{
    public string Node { get; set; } = "background";
    public string Property { get; set; } = "";
    public ParameterKind Kind { get; set; }
    public double[] Default { get; set; } = new[] { 0.0 };
    public string? DefaultChoice { get; set; }
    public KeyDocument[] Keys { get; set; } = Array.Empty<KeyDocument>();
}
public sealed class KeyDocument
{
    public double? Seconds { get; set; }
    public double? Beat { get; set; }
    public double[] Value { get; set; } = new[] { 0.0 };
    public string? Choice { get; set; }
    public Easing Ease { get; set; }
    public CurvePointDocument[]? Curve { get; set; }
}
public sealed class CurvePointDocument { public double X { get; set; } public double Y { get; set; } }

public sealed class EffectPackage
{
    public PackageDocument Document { get; }
    public TempoMap Tempo { get; }
    public PreparedTimeline Timeline { get; }
    public string Root { get; }
    private EffectPackage(PackageDocument doc, string root, TempoMap tempo, PreparedTimeline timeline)
        => (Document, Root, Tempo, Timeline) = (doc, root, tempo, timeline);

    public bool Matches(in PlaybackSnapshot s) => s.IsValid && !string.IsNullOrWhiteSpace(Document.SongKey) && Document.SongKey == s.SongKey
        && (!Document.Difficulty.HasValue || Document.Difficulty == s.Difficulty)
        && (string.IsNullOrEmpty(Document.ChartFingerprint) || Document.ChartFingerprint == s.ChartFingerprint);

    // Unity supplies its bundled JSON reader; validation and compilation remain identical.
    public static EffectPackage Load(string manifest, IReadOnlyCollection<string> capabilities,
        Func<string, PackageDocument>? deserialize = null)
    {
        var file = new FileInfo(manifest);
        if (file.Length > 4 * 1024 * 1024) throw new InvalidDataException("Manifest exceeds 4 MiB.");
#if !PRESENTATION_PORTABLE
        deserialize ??= Deserialize;
#endif
        if (deserialize == null) throw new ArgumentException("This host must supply a JSON reader.");
        var doc = deserialize(File.ReadAllText(manifest)) ?? throw new InvalidDataException("Empty manifest.");
        return FromDocument(doc, file.DirectoryName!, capabilities);
    }
#if !PRESENTATION_PORTABLE
    private static PackageDocument Deserialize(string text)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, MaxDepth = 32 };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return JsonSerializer.Deserialize<PackageDocument>(text, options) ?? throw new InvalidDataException("Empty manifest.");
    }
#endif
    public static EffectPackage FromDocument(PackageDocument doc, string root, IReadOnlyCollection<string> capabilities)
        => FromDocument(doc, root, capabilities, requireResources: true);

    /// <summary>Editors may validate/sample missing-resource projects; runtime loading stays strict.</summary>
    public static EffectPackage FromDocument(PackageDocument doc, string root, IReadOnlyCollection<string> capabilities,
        bool requireResources)
    {
        if (doc.FormatVersion != 1) throw new InvalidDataException("Unsupported formatVersion.");
        if (string.IsNullOrWhiteSpace(doc.Id))
            throw new InvalidDataException("id is required. An absent songKey is preview-only, never a game wildcard.");
        if (!double.IsFinite(doc.OffsetSeconds) || doc.Difficulty < 0 || doc.Difficulty > 3
            || !Enum.IsDefined(typeof(TimeDomain), doc.TimeDomain))
            throw new InvalidDataException("Invalid offset/difficulty.");
        if (doc.Requires == null || doc.Nodes == null || doc.Channels == null || doc.Tempo == null)
            throw new InvalidDataException("Package collections must not be null.");
        foreach (var cap in doc.Requires)
            if (!capabilities.Contains(cap)) throw new NotSupportedException($"Missing capability: {cap}");
        if (doc.Nodes.Length == 0 || doc.Nodes.Any(n => n == null || string.IsNullOrWhiteSpace(n.Id))
            || doc.Nodes.Select(n => n.Id).Distinct().Count() != doc.Nodes.Length)
            throw new InvalidDataException("Unique effect nodes are required.");
        foreach (var n in doc.Nodes)
        {
            if (!double.IsFinite(n.StartSeconds) || n.StartSeconds < 0 || n.EndSeconds.HasValue
                && (!double.IsFinite(n.EndSeconds.Value) || n.EndSeconds <= n.StartSeconds)
                || n.EffectId == null || n.Name == null || n.Name.Length > 160)
                throw new InvalidDataException($"{n.Id}: invalid effect instance or time range.");
            if ((n.EffectId.Length != 0 || n.Name.Length != 0 || !n.Enabled || n.StartSeconds != 0 || n.EndSeconds.HasValue)
                && !doc.Requires.Contains("effect.instances"))
                throw new InvalidDataException("Effect instances require the effect.instances capability.");
            if (string.IsNullOrWhiteSpace(n.Material) || n.Textures == null)
                throw new InvalidDataException($"{n.Id}: material and texture map are required.");
            ResolveAsset(root, n.Bundle, requireResources);
        }
        var tempo = new TempoMap(doc.Tempo.Select(t => new TempoChange(t.Seconds, t.Bpm)));
        var channels = doc.Channels.Select(ch =>
        {
            if (ch == null || ch.Keys == null || ch.Keys.Any(k => k == null)
                || !Enum.IsDefined(typeof(ParameterKind), ch.Kind))
                throw new InvalidDataException("Invalid channel/keys/kind.");
            if (!doc.Nodes.Any(n => n.Id == ch.Node)) throw new InvalidDataException($"Unknown node: {ch.Node}");
            if (ch.Kind == ParameterKind.Choice)
            {
                var textures = doc.Nodes.Single(n => n.Id == ch.Node).Textures;
                foreach (var choice in ch.Keys.Select(k => k.Choice).Prepend(ch.DefaultChoice))
                    if (choice == null || !textures.ContainsKey(choice))
                        throw new InvalidDataException($"{ch.Node}.{ch.Property}: undefined texture choice {choice}.");
            }
            var keys = ch.Keys.Select(k =>
            {
                if (!Enum.IsDefined(typeof(Easing), k.Ease)) throw new InvalidDataException("Unknown easing.");
                if (k.Seconds.HasValue == k.Beat.HasValue)
                    throw new InvalidDataException($"{ch.Property}: use exactly one of seconds or beat.");
                return new TimelineKey(k.Seconds ?? tempo.ToSeconds(k.Beat!.Value),
                    Value(ch.Kind, k.Value, k.Choice), k.Ease,
                    k.Curve?.Select(p => new CurvePoint(p.X, p.Y)).ToArray());
            });
            return new ChannelTrack(new(ch.Node, ch.Property, ch.Kind), Value(ch.Kind, ch.Default, ch.DefaultChoice), keys);
        });
        var timeline=new PreparedTimeline(channels);
        _=new SceneOpacityPlan(doc);
        return new(doc, root, tempo, timeline);
    }

    // Both backends support exactly this material graph in v1, in document order.
    public void ValidateMaterialGraph(string unityVersion, string platform, string colorSpace = "Gamma")
    {
        if (Document.UnityVersion != unityVersion || Document.Platform != platform)
            throw new NotSupportedException("Package Unity version/platform mismatch.");
        if (Document.ColorSpace != colorSpace) throw new NotSupportedException("Package color space mismatch.");
        if (Document.Nodes.Count(n => n.Target == "background") > 1
            || Document.Nodes.Any(n => n.Target is not ("background" or "postprocess")))
            throw new NotSupportedException("v1 supports one background and ordered postprocess materials.");
    }

    private static ParameterValue Value(ParameterKind kind, double[]? v, string? choice)
    {
        if (kind == ParameterKind.Choice) return new(default, choice);
        var length = kind == ParameterKind.Float ? 1 : 4;
        if (v == null || v.Length != length || v.Any(x => !double.IsFinite(x) || Math.Abs(x) > float.MaxValue))
            throw new InvalidDataException($"{kind} expects {length} finite numbers.");
        return new(length == 1 ? new Vector4((float)v[0], 0, 0, 0)
            : new Vector4((float)v[0], (float)v[1], (float)v[2], (float)v[3]));
    }

    public static string ResolveAsset(string root, string relative)
        => ResolveAsset(root, relative, requireExists: true);

    public static string ResolveAsset(string root, string relative, bool requireExists)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new InvalidDataException("Bundle must use a relative package path.");
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, relative));
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Asset escapes package directory.");
        for (var p = path; p != root; p = Path.GetDirectoryName(p)!)
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Package asset links are not supported.");
        if (requireExists && !File.Exists(path)) throw new FileNotFoundException("Bundle not found.", path);
        return path;
    }
}
