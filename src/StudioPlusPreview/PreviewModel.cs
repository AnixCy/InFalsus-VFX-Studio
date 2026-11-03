using System.Numerics;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;

namespace InFalsus.Vfx.StudioPlus;

/// <summary>Editor state independent of Unity rendering and IMGUI.</summary>
public sealed class PreviewModel
{
    public static readonly string[] Capabilities = { "clock.chart", "clock.audio", "background.material", "shader.bundle" };
    public AuthoringSession Session { get; private set; }
    public ParameterCatalog Catalog { get; private set; } = null!;
    public EffectPackage Package { get; private set; } = null!;
    public ParameterValue[] Values { get; private set; } = Array.Empty<ParameterValue>();
    ParameterPreviewSession preview = null!;
    AuthoringEdit? drag;
    string? dragId;
    long sequence;
    public bool IsEditing => drag != null;
    public PreviewModel(string assetRoot, string unityVersion, string colorSpace)
    {
        var d = Template(unityVersion, colorSpace);
        var project = AuthoringProjects.Import(EffectPackage.FromDocument(d, assetRoot, Capabilities, false));
        project.ParameterHints = Hints();
        Session = new AuthoringSession(project, assetRoot, Capabilities, isSaved: true);
        Refresh();
    }
    public void Refresh()
    {
        Package = Session.CompileSnapshot(false);
        Catalog = new ParameterCatalog(Package, Session.Snapshot().ParameterHints);
        preview = new ParameterPreviewSession(Catalog, Session.Revision);
        Values = new ParameterValue[Catalog.Parameters.Count]; sequence = 0;
    }
    public void Sample(double seconds, bool committedOnly = false)
        => preview.SampleInto(seconds, Values, !committedOnly);
    public void UpdateParameter(string id, ParameterValue value)
    {
        if (drag != null && dragId != id) Commit();
        if (drag == null) { drag = Session.BeginEdit(Session.Revision, "调整参数"); dragId = id; }
        drag.SetDefaultValue(id, value);
        preview.ReplaceOverrides(new PreviewEdit(preview.SessionId, Session.Revision, ++sequence,
            new[] { new PreviewOverride(id, value) }));
    }
    public void Commit()
    {
        if (drag == null) return;
        var pending = drag; drag = null; dragId = null;
        try { pending.Commit(); } finally { pending.Dispose(); Refresh(); }
    }
    public void Cancel() { drag?.Dispose(); drag = null; dragId = null; Refresh(); }
    public void Undo() { Commit(); Session.Undo(Session.Revision); Refresh(); }
    public void Redo() { Commit(); Session.Redo(Session.Revision); Refresh(); }
    public void Reset()
    {
        Commit(); using var edit = Session.BeginEdit(Session.Revision, "恢复默认参数");
        foreach (var c in Template(Package.Document.UnityVersion, Package.Document.ColorSpace).Channels)
            edit.SetDefaultValue(ParameterDescriptor.MakeId(c.Node, c.Property), Number(c.Default));
        edit.Commit(); Refresh();
    }
    public void Replace(AuthoringSession session)
    {
        var package = session.CompileSnapshot(false);
        var template = Template(package.Document.UnityVersion, package.Document.ColorSpace);
        if (package.Document.Nodes.Length != 1 || package.Document.Nodes[0].Material != "vfx/parameter-preview"
            || package.Document.Nodes[0].Id != "background" || package.Document.Nodes[0].Target != "background"
            || package.Document.Channels.Length != template.Channels.Length
            || package.Document.Channels.Any(c => c.Keys.Length != 0 || !template.Channels.Any(t =>
                t.Node == c.Node && t.Property == c.Property && t.Kind == c.Kind)))
            throw new InvalidDataException("此版本只支持内置背景的常量参数工程。");
        Cancel(); Session = session; Refresh();
    }
    public static ParameterValue Number(double[] v) => new(v.Length == 1
        ? new Vector4((float)v[0], 0, 0, 0) : new Vector4((float)v[0], (float)v[1], (float)v[2], (float)v[3]));
    static ChannelDocument Float(string name, double value) => new() {
        Node = "background", Property = name, Kind = ParameterKind.Float, Default = new[] { value } };
    public static PackageDocument Template(string unityVersion, string colorSpace) => new() {
        Id = "studio-background-preview", UnityVersion = unityVersion, ColorSpace = colorSpace,
        TimeDomain = TimeDomain.Chart, Requires = Capabilities,
        Nodes = new[] { new MaterialNodeDocument { Id = "background", Bundle = "preview.bundle", Material = "vfx/parameter-preview" } },
        Channels = new[] {
            Float("_Brightness", 1),
            new ChannelDocument { Node = "background", Property = "_Tint", Kind = ParameterKind.Color, Default = new[] { 1.0, 1.0, 1.0, 1 } },
            Float("_Distortion", .15), Float("_Frequency", 6), Float("_Speed", .3),
            Float("_OffsetX", 0), Float("_OffsetY", 0), Float("_PatternMix", 1)
        }
    };
    static ParameterPresentation Hint(string property, string label, double min, double max) => new() {
        Node = "background", Property = property, Label = label, SuggestedMin = min, SuggestedMax = max };
    static ParameterPresentation[] Hints() => new[] {
        Hint("_Brightness", "亮度", 0, 3), Hint("_Tint", "颜色", 0, 2),
        Hint("_Distortion", "UV 位移强度", 0, 1), Hint("_Frequency", "空间频率", 1, 24),
        Hint("_Speed", "时间频率", 0, 2), Hint("_OffsetX", "水平偏移", -1, 1),
        Hint("_OffsetY", "垂直偏移", -1, 1), Hint("_PatternMix", "内置图案混合", 0, 1)
    };
}
