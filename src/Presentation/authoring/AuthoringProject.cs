namespace InFalsus.Presentation.Authoring;

public sealed class TrackIdentityDocument
{
    public string ParameterId { get; set; } = "";
    public string TrackId { get; set; } = "";
    public string[] KeyIds { get; set; } = Array.Empty<string>();
}

/// <summary>The editable source: one runtime document and parallel stable identity metadata.</summary>
public sealed class AuthoringProjectDocument
{
    public int FormatVersion { get; set; } = 1;
    public string ProjectId { get; set; } = "";
    public string ContentId { get; set; } = "";
    public long Revision { get; set; }
    public string AssetRoot { get; set; } = ".";
    public PackageDocument Runtime { get; set; } = new();
    public TrackIdentityDocument[] Tracks { get; set; } = Array.Empty<TrackIdentityDocument>();
    public ParameterPresentation[] ParameterHints { get; set; } = Array.Empty<ParameterPresentation>();
}

public static class AuthoringProjects
{
    public static AuthoringProjectDocument Import(EffectPackage package)
    {
        if (package == null) throw new ArgumentNullException(nameof(package));
        return new AuthoringProjectDocument {
            ProjectId = Guid.NewGuid().ToString("N"), ContentId = Guid.NewGuid().ToString("N"), Revision = 1,
            AssetRoot = Path.GetFullPath(package.Root), Runtime = CopyRuntime(package.Document),
            Tracks = package.Document.Channels.Select(c => new TrackIdentityDocument {
                ParameterId = ParameterDescriptor.MakeId(c.Node, c.Property), TrackId = Guid.NewGuid().ToString("N"),
                KeyIds = c.Keys.Select(_ => Guid.NewGuid().ToString("N")).ToArray()
            }).ToArray()
        };
    }

    public static AuthoringProjectDocument Copy(AuthoringProjectDocument source)
    {
        if (source == null || source.Runtime == null || source.Tracks == null || source.ParameterHints == null
            || source.Tracks.Any(t => t == null || t.KeyIds == null) || source.ParameterHints.Any(p => p == null))
            throw new InvalidDataException("Project document/collections are missing.");
        return new AuthoringProjectDocument { FormatVersion = source.FormatVersion, ProjectId = source.ProjectId,
            ContentId = source.ContentId, Revision = source.Revision, AssetRoot = source.AssetRoot, Runtime = CopyRuntime(source.Runtime),
            Tracks = source.Tracks.Select(t => new TrackIdentityDocument {
                ParameterId = t.ParameterId, TrackId = t.TrackId, KeyIds = t.KeyIds.ToArray() }).ToArray(),
            ParameterHints = source.ParameterHints.Select(p => new ParameterPresentation {
                Node = p.Node, Property = p.Property, Label = p.Label, Description = p.Description, Group = p.Group,
                Unit = p.Unit, ControlHint = p.ControlHint, SuggestedMin = p.SuggestedMin, SuggestedMax = p.SuggestedMax,
                Step = p.Step, HardMin = p.HardMin, HardMax = p.HardMax, ReadOnly = p.ReadOnly }).ToArray() };
    }

    internal static PackageDocument CopyRuntime(PackageDocument p)
    {
        if (p.Nodes == null || p.Channels == null || p.Tempo == null || p.Requires == null
            || p.Nodes.Any(n => n == null || n.Textures == null) || p.Channels.Any(c => c == null || c.Keys == null
            || c.Keys.Any(k => k == null || k.Curve?.Any(v => v == null) == true)) || p.Tempo.Any(t => t == null))
            throw new InvalidDataException("Runtime document collections are missing.");
        return new PackageDocument { FormatVersion = p.FormatVersion, Id = p.Id, SongKey = p.SongKey, Difficulty = p.Difficulty,
            ChartFingerprint = p.ChartFingerprint, TimeDomain = p.TimeDomain, OffsetSeconds = p.OffsetSeconds, Seed = p.Seed,
            UnityVersion = p.UnityVersion, Platform = p.Platform, ColorSpace = p.ColorSpace, AllowSpecialScene = p.AllowSpecialScene,
            Requires = p.Requires.ToArray(), Tempo = p.Tempo.Select(t => new TempoDocument { Seconds = t.Seconds, Bpm = t.Bpm }).ToArray(),
            Nodes = p.Nodes.Select(n => new MaterialNodeDocument { Id = n.Id, Target = n.Target, Bundle = n.Bundle,
                Material = n.Material, EffectId=n.EffectId,Name=n.Name,Enabled=n.Enabled,StartSeconds=n.StartSeconds,EndSeconds=n.EndSeconds,
                Textures = new Dictionary<string, string>(n.Textures) }).ToArray(),
            Channels = p.Channels.Select(c => new ChannelDocument { Node = c.Node, Property = c.Property, Kind = c.Kind,
                Default = c.Default?.ToArray()!, DefaultChoice = c.DefaultChoice, Keys = c.Keys.Select(CopyKey).ToArray() }).ToArray() };
    }

    internal static KeyDocument CopyKey(KeyDocument k) => new() { Seconds = k.Seconds, Beat = k.Beat,
        Value = k.Value?.ToArray()!, Choice = k.Choice, Ease = k.Ease,
        Curve = k.Curve?.Select(p => new CurvePointDocument { X = p.X, Y = p.Y }).ToArray() };

    public static EffectPackage Compile(AuthoringProjectDocument document, string projectDirectory,
        IReadOnlyCollection<string> capabilities, bool requireResources = true)
    {
        var p = Copy(document);
        if (p.FormatVersion != 1 || string.IsNullOrWhiteSpace(p.ProjectId) || string.IsNullOrWhiteSpace(p.ContentId)
            || p.Revision <= 0 || string.IsNullOrWhiteSpace(p.AssetRoot))
            throw new InvalidDataException("Invalid project format, identity, revision or asset root.");
        var root = Path.GetFullPath(Path.Combine(projectDirectory, p.AssetRoot));
        var package = EffectPackage.FromDocument(p.Runtime, root, capabilities, requireResources);
        package.ValidateMaterialGraph(p.Runtime.UnityVersion, p.Runtime.Platform, p.Runtime.ColorSpace);
        var catalog = new ParameterCatalog(package, p.ParameterHints);
        if (p.Tracks.Length != catalog.Parameters.Count) throw new InvalidDataException("Track identity count mismatch.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < p.Tracks.Length; i++)
        {
            var t = p.Tracks[i];
            if (t.ParameterId != catalog.Parameters[i].Id || t.KeyIds.Length != p.Runtime.Channels[i].Keys.Length
                || string.IsNullOrWhiteSpace(t.TrackId) || !ids.Add(t.TrackId))
                throw new InvalidDataException("Track identity/binding mismatch.");
            foreach (var id in t.KeyIds)
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidDataException("Key identities must be unique.");
        }
        return package;
    }

    // Editing APIs only change defaults/keys. Other runtime/resource fields have no mutable public draft.
    internal static bool SameEditableContent(AuthoringProjectDocument a, AuthoringProjectDocument b)
    {
        // Runtime material values are floats. Returning a slider to the same float must not
        // dirty a JSON default merely because its original decimal used a double representation.
        static bool SameValues(ParameterKind kind, double[]? x, double[]? y) => kind == ParameterKind.Choice ||
            (x ?? Array.Empty<double>()).Select(v => (float)v).SequenceEqual((y ?? Array.Empty<double>()).Select(v => (float)v));
        var p = a.Runtime; var q = b.Runtime;
        if (a.AssetRoot != b.AssetRoot || a.Tracks.Length != b.Tracks.Length || a.ParameterHints.Length != b.ParameterHints.Length
            || p.FormatVersion != q.FormatVersion || p.Id != q.Id || p.SongKey != q.SongKey || p.Difficulty != q.Difficulty
            || p.ChartFingerprint != q.ChartFingerprint || p.TimeDomain != q.TimeDomain || p.OffsetSeconds != q.OffsetSeconds
            || p.Seed != q.Seed || p.UnityVersion != q.UnityVersion || p.Platform != q.Platform || p.ColorSpace != q.ColorSpace
            || p.AllowSpecialScene != q.AllowSpecialScene || !p.Requires.SequenceEqual(q.Requires)
            || !p.Tempo.Select(t => (t.Seconds, t.Bpm)).SequenceEqual(q.Tempo.Select(t => (t.Seconds, t.Bpm)))
            || p.Nodes.Length != q.Nodes.Length || p.Channels.Length != q.Channels.Length) return false;
        for (var i = 0; i < p.Nodes.Length; i++)
        {
            var x = p.Nodes[i]; var y = q.Nodes[i];
            if (x.Id != y.Id || x.Target != y.Target || x.Bundle != y.Bundle || x.Material != y.Material
                || x.EffectId!=y.EffectId || x.Name!=y.Name || x.Enabled!=y.Enabled || x.StartSeconds!=y.StartSeconds || x.EndSeconds!=y.EndSeconds
                || !x.Textures.OrderBy(v => v.Key, StringComparer.Ordinal).SequenceEqual(y.Textures.OrderBy(v => v.Key, StringComparer.Ordinal))) return false;
        }
        for (var i = 0; i < a.ParameterHints.Length; i++)
        {
            var x = a.ParameterHints[i]; var y = b.ParameterHints[i];
            if (x.Node != y.Node || x.Property != y.Property || x.Label != y.Label || x.Description != y.Description
                || x.Group != y.Group || x.Unit != y.Unit || x.ControlHint != y.ControlHint || x.ReadOnly != y.ReadOnly
                || x.Step != y.Step || x.HardMin != y.HardMin || x.HardMax != y.HardMax
                || x.SuggestedMin != y.SuggestedMin || x.SuggestedMax != y.SuggestedMax) return false;
        }
        for (var i = 0; i < a.Runtime.Channels.Length; i++)
        {
            var x = a.Runtime.Channels[i]; var y = b.Runtime.Channels[i];
            if (x.Node != y.Node || x.Property != y.Property || x.Kind != y.Kind || a.Tracks[i].TrackId != b.Tracks[i].TrackId
                || a.Tracks[i].ParameterId != b.Tracks[i].ParameterId
                || !SameValues(x.Kind, x.Default, y.Default) || x.DefaultChoice != y.DefaultChoice
                || !a.Tracks[i].KeyIds.SequenceEqual(b.Tracks[i].KeyIds) || x.Keys.Length != y.Keys.Length) return false;
            for (var j = 0; j < x.Keys.Length; j++)
            {
                var u = x.Keys[j]; var v = y.Keys[j];
                if (u.Seconds != v.Seconds || u.Beat != v.Beat || u.Ease != v.Ease || u.Choice != v.Choice
                    || !SameValues(x.Kind, u.Value, v.Value)
                    || !(u.Curve ?? Array.Empty<CurvePointDocument>()).Select(p => (p.X, p.Y))
                        .SequenceEqual((v.Curve ?? Array.Empty<CurvePointDocument>()).Select(p => (p.X, p.Y)))) return false;
            }
        }
        return true;
    }
}
