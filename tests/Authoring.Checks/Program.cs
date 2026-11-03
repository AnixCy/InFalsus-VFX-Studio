using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;

var root = Path.GetFullPath(args.Length == 0 ? Directory.GetCurrentDirectory() : args[0]);
new Checks(root).Run();

sealed class Checks(string root)
{
    private int _checks;
    private readonly List<string> _groups = new();
    private static readonly string[] Capabilities =
        { "clock.audio", "clock.chart", "shader.bundle", "background.material", "postprocess.blit.experimental" };
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };
    private readonly string _manifest = Path.Combine(root, "tests/Fixtures/material-contract/manifest.json");
    private string PackageRoot => Path.GetDirectoryName(_manifest)!;
    private PackageDocument Document() => JsonSerializer.Deserialize<PackageDocument>(File.ReadAllText(_manifest), Json)!;
    private EffectPackage Package(PackageDocument? doc = null) => EffectPackage.FromDocument(doc ?? Document(), PackageRoot, Capabilities);
    private static ParameterValue Number(float x) => new(new Vector4(x, 0, 0, 0));
    private static ParameterValue Choice(string name) => new(default, name);
    private static PreviewOverride Override(string id, ParameterValue value) => new(id, value);
    private static PreviewEdit Edit(IParameterPreviewSession session, long sequence, params PreviewOverride[] values)
        => new(session.SessionId, session.DocumentRevision, sequence, values);
    private static PreviewParameterSample Value(IParameterPreviewSession session, double t, string id, bool temporary = true)
        => session.Inspect(t, temporary).Parameters.Single(p => p.Parameter.Id == id);
    private void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
    private void Near(float actual, float expected, string message) => Assert(Math.Abs(actual - expected) < .00001f, message);
    private void Reject(Action action, PreviewEditError expected)
    {
        try { action(); }
        catch (PreviewEditException e) { Assert(e.Code == expected, "Wrong rejection: " + e.Code); return; }
        throw new Exception("Expected rejection: " + expected);
    }
    private void Invalid(Action action)
    {
        try { action(); }
        catch (ArgumentException) { _checks++; return; }
        throw new Exception("Expected invalid catalog/input.");
    }
    public void Run()
    {
        Catalog(); PreviewAndCancellation(); AtomicAndStaleEdits(); Validation(); TimeAndSnapshots(); Concurrency();
        Console.WriteLine(JsonSerializer.Serialize(new { checks = _checks, groups = _groups,
            manifest = _manifest, target = "netstandard2.1", graphicsExecuted = false, gameStarted = false,
            uiRequired = false, sourceManifestModified = false }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Catalog()
    {
        var doc = Document(); var package = Package(doc);
        var hints = new ParameterPresentation { Node = "bg", Property = "_Amount", Label = "Strength",
            Group = "Main", Unit = "ratio", SuggestedMin = 0, SuggestedMax = 1, Step = .01 };
        var catalog = new ParameterCatalog(package, new[] { hints });
        var d = catalog.Get("bg/_Amount");
        Assert(catalog.Parameters.Count == doc.Channels.Length, "Only declared channels are exposed.");
        Assert(d.Label == "Strength" && d.Unit == "ratio" && d.Group == "Main", "Host-facing presentation metadata.");
        Assert(d.SuggestedMax == 1 && d.Step == .01 && !d.HardMax.HasValue, "Advisory range is distinct from hard constraints.");
        Assert(catalog.Get("bg/_MainTex").Choices.SequenceEqual(new[] { "blue", "red" }), "Declared texture choices.");
        Assert(catalog.Parameters.Select(p => p.Binding.Kind).Distinct().Count() == 4, "All four existing kinds are represented.");
        hints.Label = "changed"; doc.Channels[0].Default[0] = 99;
        Assert(d.Label == "Strength" && d.DefaultValue.Number.X == 0, "Descriptor owns a snapshot of mutable input.");
        doc = Document(); doc.Channels = doc.Channels.Reverse().ToArray();
        var reordered = new ParameterCatalog(Package(doc));
        Assert(reordered.Get(d.Id).Binding == d.Binding, "Parameter identity survives channel reordering.");
        Assert(ParameterDescriptor.MakeId("a/b", "c") != ParameterDescriptor.MakeId("a", "b/c"), "Escaped identity is unambiguous.");
        Invalid(() => new ParameterCatalog(package, new[] { new ParameterPresentation { Node = "missing", Property = "_Amount" } }));
        Invalid(() => new ParameterCatalog(package, new[] { hints, hints }));
        foreach (var bad in new[]
        {
            new ParameterPresentation { Node = "bg", Property = "_Amount", Step = 0 },
            new ParameterPresentation { Node = "bg", Property = "_Amount", SuggestedMin = 2, SuggestedMax = 1 },
            new ParameterPresentation { Node = "bg", Property = "_Amount", HardMin = 1, HardMax = -1 },
            new ParameterPresentation { Node = "bg", Property = "_Amount", SuggestedMax = double.NaN }
        }) Invalid(() => new ParameterCatalog(package, new[] { bad }));
        var references = typeof(ParameterPreviewSession).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert(!references.Any(n => n.StartsWith("Unity", StringComparison.Ordinal) || n.Contains("Mcp", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Windows.Forms", StringComparison.Ordinal)), "Authoring assembly has no editor/UI/MCP dependency.");
        _groups.Add("Parameter discovery, advisory metadata, immutable descriptors and stable IDs");
    }

    private void PreviewAndCancellation()
    {
        var package = Package(); var source = JsonSerializer.Serialize(package.Document, Json);
        IParameterPreviewSession session = new ParameterPreviewSession(new ParameterCatalog(package));
        Near(Value(session, 2, "bg/_Amount").Value.Number.X, .5f, "Baseline uses shared smooth interpolation.");
        var overrides = new[]
        {
            Override("bg/_Amount", Number(1.4f)),
            Override("bg/_Color", new(new Vector4(2.4f, .8f, .5f, 1))),
            Override("bg/_Vector", new(new Vector4(.2f, .1f, 0, 0))),
            Override("bg/_MainTex", Choice("red"))
        };
        var edit = Edit(session, 1, overrides); overrides[0] = Override("bg/_Amount", Number(99));
        session.ReplaceOverrides(edit);
        Near(Value(session, 2, "bg/_Amount").Value.Number.X, 1.4f, "Overrides beat animation; input list was copied.");
        Near(Value(session, 2, "bg/_Color").Value.Number.X, 2.4f, "HDR/artistic values are not silently clamped.");
        Assert(Value(session, 2, "bg/_Vector").Value.Number.Y == .1f, "Vector preview.");
        Assert(Value(session, 2, "bg/_MainTex").Value.Choice == "red", "Texture choice preview.");
        Assert(Value(session, 2, "bg/_Amount").Source == PreviewValueSource.TemporaryOverride, "Effective value source is exposed.");
        Near(Value(session, 2, "bg/_Amount", false).Value.Number.X, .5f, "Baseline sampling excludes temporary edits.");
        var beforeCancel = session.Inspect(2);
        session.ReplaceOverrides(Edit(session, 2));
        Near(Value(session, 2, "bg/_Amount").Value.Number.X, .5f, "Cancel restores the animated value at the current time.");
        Assert(Value(session, 2, "bg/_MainTex").Value.Choice == "blue", "Cancel restores animated texture selection.");
        Assert(beforeCancel.Stamp.EditSequence == 1 && beforeCancel.Parameters[0].Value.Number.X == 1.4f,
            "Already issued inspection remains unchanged after a new edit.");
        Assert(source == JsonSerializer.Serialize(package.Document, Json), "Preview never mutates source document.");
        Assert(session.LastSequence == 2 && session.DocumentRevision == 1, "Temporary edit sequence is not a document revision.");
        Assert(Value(session, -1, "bg/_Amount").Source == PreviewValueSource.Default, "Default source before the first key.");
        Assert(Value(session, 3, "gain/_Multiply").Source == PreviewValueSource.Default, "Unanimated channel uses its default.");
        _groups.Add("Four parameter kinds, non-destructive temporary values, cancellation and baseline sampling");
    }

    private void AtomicAndStaleEdits()
    {
        var session = new ParameterPreviewSession(new ParameterCatalog(Package()), documentRevision: 7);
        session.ReplaceOverrides(Edit(session, 10, Override("bg/_Amount", Number(.8f))));
        Reject(() => session.ReplaceOverrides(Edit(session, 11, Override("bg/_Amount", Number(.1f)),
            Override("missing/value", Number(1)))), PreviewEditError.UnknownParameter);
        Near(Value(session, 2, "bg/_Amount").Value.Number.X, .8f, "A failed batch does not partially apply its first value.");
        Assert(session.LastSequence == 10, "A rejected batch does not consume its sequence.");
        Reject(() => session.ReplaceOverrides(Edit(session, 9)), PreviewEditError.StaleSequence);
        Reject(() => session.ReplaceOverrides(Edit(session, 10)), PreviewEditError.StaleSequence);
        Reject(() => session.ReplaceOverrides(new PreviewEdit(session.SessionId, 6, 11, Array.Empty<PreviewOverride>())), PreviewEditError.WrongRevision);
        Reject(() => session.ReplaceOverrides(new PreviewEdit("previous-session", 7, 11, Array.Empty<PreviewOverride>())), PreviewEditError.WrongSession);
        Reject(() => session.ReplaceOverrides(Edit(session, 11, Override("bg/_Amount", Number(.1f)),
            Override("bg/_Amount", Number(.2f)))), PreviewEditError.DuplicateParameter);
        session.ReplaceOverrides(Edit(session, 11, Override("bg/_Amount", Number(.3f))));
        Near(Value(session, 2, "bg/_Amount").Value.Number.X, .3f, "Sequence can be retried after validation failure.");
        var next = new ParameterPreviewSession(session.Catalog, documentRevision: 7);
        Assert(next.SessionId != session.SessionId, "Reloaded session gets a new identity.");
        Reject(() => next.ReplaceOverrides(Edit(session, 12)), PreviewEditError.WrongSession);
        _groups.Add("Atomic replacement, out-of-order edits, stale revisions and reload isolation");
    }

    private void Validation()
    {
        var session = new ParameterPreviewSession(new ParameterCatalog(Package(), new[]
        {
            new ParameterPresentation { Node = "bg", Property = "_Amount", SuggestedMin = 0, SuggestedMax = 1 },
            new ParameterPresentation { Node = "bg", Property = "_Vector", HardMin = -.5, HardMax = .5 },
            new ParameterPresentation { Node = "gain", Property = "_Multiply", ReadOnly = true }
        }));
        session.ReplaceOverrides(Edit(session, 1, Override("bg/_Amount", Number(3))));
        Near(Value(session, 1, "bg/_Amount").Value.Number.X, 3, "Suggestion range does not restrict artistic editing.");
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("bg/_Vector", new(new Vector4(.6f, 0, 0, 0))))), PreviewEditError.OutsideHardRange);
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("bg/_Amount", Number(float.NaN)))), PreviewEditError.InvalidValue);
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("bg/_Amount", Number(float.PositiveInfinity)))), PreviewEditError.InvalidValue);
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("bg/_Amount", new(new Vector4(1, 1, 0, 0))))), PreviewEditError.InvalidValue);
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("bg/_Amount", Choice("red")))), PreviewEditError.InvalidValue);
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("bg/_MainTex", Choice("missing")))), PreviewEditError.InvalidChoice);
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("bg/_MainTex", Number(1)))), PreviewEditError.InvalidChoice);
        Reject(() => session.ReplaceOverrides(Edit(session, 2, Override("gain/_Multiply", new(Vector4.One)))), PreviewEditError.ReadOnlyParameter);
        var doc = Document(); doc.Channels[0].Property = "_PresentationTime";
        var clockSession = new ParameterPreviewSession(new ParameterCatalog(Package(doc)));
        Assert(clockSession.Catalog.Get("bg/_PresentationTime").ReadOnly, "Clock context is always read-only.");
        Reject(() => clockSession.ReplaceOverrides(Edit(clockSession, 1, Override("bg/_PresentationTime", Number(9)))), PreviewEditError.ReadOnlyParameter);
        Invalid(() => session.Inspect(double.NaN));
        Invalid(() => session.SampleInto(0, Array.Empty<ParameterValue>()));
        _groups.Add("Explicit hard bounds, finite values, type/choice validation and protected clock inputs");
    }

    private void TimeAndSnapshots()
    {
        var package = Package(); var session = new ParameterPreviewSession(new ParameterCatalog(package));
        var clock = new PreviewClock();
        var transport = new PreviewTransportSnapshot(2, 120, 1, false, 1, 1, 0);
        var frame = clock.Sample(transport, package);
        var a = session.Inspect(frame.Seconds); session.ReplaceOverrides(Edit(session, 1, Override("bg/_Amount", Number(.9f))));
        var held = clock.Sample(transport with { MonotonicSeconds = 20 }, package);
        Assert(held.Seconds == frame.Seconds && held.ClockRevision == frame.ClockRevision, "Preview edits do not move or reset the shared clock.");
        var buffer = new ParameterValue[session.Catalog.Parameters.Count];
        var stamp = session.SampleInto(held.Seconds, buffer);
        Assert(stamp.EditSequence == 1 && stamp.IncludesTemporaryOverrides, "Renderer receives a revision/sequence stamp.");
        Near(buffer[0].Number.X, .9f, "Span sampler returns override in runtime binding order.");
        session.ReplaceOverrides(Edit(session, 2));
        var direct = new ParameterValue[buffer.Length]; package.Timeline.Sample(frame.Seconds, direct);
        session.SampleInto(frame.Seconds, buffer);
        Assert(buffer.SequenceEqual(direct), "Cancel returns exactly to shared runtime sampling.");
        Assert(a.Stamp.EditSequence == 0 && a.Parameters[0].Value.Number.X == .5f, "Earlier snapshot is stable.");
        var times = new[] { -1.0, 0, 1, 2, 3, 4, 8 };
        var ordered = times.ToDictionary(t => t, t => session.Inspect(t).Parameters.Select(p => p.Value).ToArray());
        foreach (var t in times.Reverse())
            Assert(session.Inspect(t).Parameters.Select(p => p.Value).SequenceEqual(ordered[t]), "Random access equals forward evaluation.");
        _groups.Add("Existing clock integration, pause, renderer stamps and repeatable random-access sampling");
    }

    private void Concurrency()
    {
        var session = new ParameterPreviewSession(new ParameterCatalog(Package()));
        Parallel.For(1, 33, i =>
        {
            try { session.ReplaceOverrides(Edit(session, i, Override("bg/_Amount", Number(i)))); }
            catch (PreviewEditException e) when (e.Code == PreviewEditError.StaleSequence) { }
        });
        Assert(session.LastSequence == 32, "Concurrent arrivals keep the newest edit.");
        Near(Value(session, 2, "bg/_Amount").Value.Number.X, 32, "Newest complete value wins.");
        _groups.Add("Concurrent latest-edit publication");
    }
}
