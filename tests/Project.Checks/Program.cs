using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;

new Checks(Path.GetFullPath(args.Length == 0 ? Directory.GetCurrentDirectory() : args[0])).Run();

sealed class Checks(string root)
{
    private int _checks;
    private readonly List<string> _groups = new();
    private readonly string _manifest = Path.Combine(root, "tests/Fixtures/material-contract/manifest.json");
    private readonly string _out = Path.Combine(root, "local/checks/authoring-project");
    private static readonly string[] Caps = { "clock.audio", "clock.chart", "shader.bundle", "background.material", "postprocess.blit.experimental", "effect.instances" };
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) } };
    private string _work = "";
    private static string Encode(AuthoringProjectDocument p) => JsonSerializer.Serialize(p, Json);
    private static AuthoringProjectDocument Decode(string s) => JsonSerializer.Deserialize<AuthoringProjectDocument>(s, Json)!;
    private static ParameterValue Number(float x) => new(new Vector4(x, 0, 0, 0));
    private static ParameterValue Vector(float x) => new(new Vector4(x, 1.1f, 1, 1));
    private EffectPackage Package() => EffectPackage.Load(_manifest, Caps, s => JsonSerializer.Deserialize<PackageDocument>(s, Json)!);
    private AuthoringSession Session(bool saved = true) => new(AuthoringProjects.Import(Package()), root, Caps, isSaved: saved);
    private static string Track(AuthoringProjectDocument d, string id) => d.Tracks.Single(t => t.ParameterId == id).TrackId;
    private static string Key(AuthoringSession s, string parameter, int index)
        => s.Snapshot().Tracks.Single(t => t.ParameterId == parameter).KeyIds[index];
    private static ParameterValue Sample(EffectPackage p, double seconds, string id)
    {
        var values = new ParameterValue[p.Timeline.Count]; p.Timeline.Sample(seconds, values);
        var i = p.Timeline.Bindings.Select((b, j) => (b, j)).Single(x => ParameterDescriptor.MakeId(x.b.Node, x.b.Property) == id).j;
        return values[i];
    }
    private static ParameterValue Sample(AuthoringSession s, double seconds, string id) => Sample(s.CompileSnapshot(false), seconds, id);
    private void Assert(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    private void Near(float value, float expected, string message) => Assert(Math.Abs(value - expected) < .00001f, message);
    private void Reject(Action action, AuthoringEditError code)
    {
        try { action(); } catch (AuthoringEditException e) { Assert(e.Code == code, "Wrong edit rejection."); return; }
        throw new Exception("Expected authoring rejection: " + code);
    }
    private void Fail<T>(Action action) where T : Exception
    { try { action(); } catch (T) { _checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
    public void Run()
    {
        Directory.CreateDirectory(_out); _work = Path.Combine(_out, "work-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(_work);
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_manifest)));
        ImportAndDrag(); KeysAndCurves(); ConflictsAndGuards(); SaveAndReopen(); BackgroundSave(); MissingAssets(); EffectIntervals();
        Assert(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_manifest))) == sourceHash, "Original runtime package stays untouched.");
        var report = new { checks = _checks, groups = _groups, workDirectory = _work,
            gameStarted = false, graphicsExecuted = false, visualCalls = 0, sourceManifestModified = false };
        File.WriteAllText(Path.Combine(_out, "checks.json"), JsonSerializer.Serialize(report, Json));
        Console.WriteLine(JsonSerializer.Serialize(report, Json));
    }

    private void EffectIntervals()
    {
        var session=Session();var original=session.Snapshot();var keyIds=original.Tracks.Single(t=>t.ParameterId=="bg/_Amount").KeyIds;
        using(var edit=session.BeginEdit(session.Revision,"Move effect")) {
            edit.SetNodeRange("bg",2,7,true);edit.SetNodeRange("bg",3,8,true);
            var preview=edit.CompilePreview();
            Assert(preview.Document.Nodes[0].StartSeconds==3&&preview.Document.Channels[0].Keys[0].Seconds==3,"Effect drag uses absolute target times without cumulative drift.");
            edit.Commit();
        }
        Assert(session.Snapshot().Tracks.Single(t=>t.ParameterId=="bg/_Amount").KeyIds.SequenceEqual(keyIds),"Moving an interval preserves animated key identities.");
        Near(Sample(session,3,"bg/_Amount").Number.X,Sample(Package(),0,"bg/_Amount").Number.X,"Moved animation follows the interval.");
        session.Undo(session.Revision);Assert(session.Snapshot().Runtime.Nodes[0].StartSeconds==0,"One undo restores the whole interval drag.");
        session.Redo(session.Revision);Assert(session.Snapshot().Runtime.Nodes[0].EndSeconds==8,"Redo restores the effect interval.");
        using(var edit=session.BeginEdit(session.Revision)) {
            Fail<ArgumentException>(()=>edit.SetNodeRange("bg",double.NaN,9,true));
            Fail<ArgumentException>(()=>edit.SetNodeRange("bg",4,4,true));
            Assert(edit.CompilePreview().Document.Nodes[0].StartSeconds==3,"Invalid interval input leaves its draft unchanged.");
        }
        var graph=session.Snapshot().Runtime;graph.Nodes[1].EffectId="test-effect";graph.Nodes[1].Name="Test layer";graph.Nodes[1].Enabled=false;
        using(var edit=session.BeginEdit(session.Revision)){edit.ReplaceGraph(graph,session.Snapshot().ParameterHints);edit.Commit();}
        Assert(session.Snapshot().Runtime.Nodes[1].Name=="Test layer"&&!session.Snapshot().Runtime.Nodes[1].Enabled,"Instance identity name and enable state participate in graph edits.");
        var store=new ProjectFileStore(Encode,Decode,Caps);string file=Path.Combine(_work,"effect-instances.json");store.Save(session,file,session.Revision);
        var reopened=store.Open(file).Session.Snapshot();
        Assert(reopened.Runtime.Nodes[1].EffectId=="test-effect"&&reopened.Runtime.Nodes[0].StartSeconds==3,"Instance metadata survives save and reopen.");
        var noSupport=Caps.Where(c=>c!="effect.instances").ToArray();
        Fail<NotSupportedException>(()=>EffectPackage.FromDocument(reopened.Runtime,Package().Root,noSupport,false));
        graph.Requires=graph.Requires.Where(c=>c!="effect.instances").ToArray();
        Fail<InvalidDataException>(()=>EffectPackage.FromDocument(graph,Package().Root,Caps,false));
        _groups.Add("Effect interval metadata, stable-key retiming, undo, persistence and capability guards");
    }
    private void ImportAndDrag()
    {
        var original = Package(); var document = AuthoringProjects.Import(original);
        var session = new AuthoringSession(document, root, Caps, isSaved: true);
        Assert(!session.IsDirty && !session.CanUndo, "Imported saved state starts clean.");
        using (var roundtrip = session.BeginEdit(session.Revision))
        {
            roundtrip.SetDefaultValue("gain/_Multiply", Vector(.8f)); roundtrip.SetDefaultValue("gain/_Multiply", Vector(1.2f));
            Assert(roundtrip.Commit() == 1 && !session.IsDirty, "Returning a float slider to a JSON decimal default is a no-op.");
        }
        using (var roundtrip = session.BeginEdit(session.Revision))
        {
            roundtrip.SetDefaultValue("bg/_MainTex", new(default, "blue")); roundtrip.SetDefaultValue("bg/_MainTex", new(default, "red"));
            Assert(roundtrip.Commit() == 1 && !session.CanUndo, "Returning a texture choice to its default creates no history.");
        }
        Assert(document.Tracks.All(t => t.KeyIds.Length == document.Runtime.Channels.Single(c => ParameterDescriptor.MakeId(c.Node, c.Property) == t.ParameterId).Keys.Length), "Every key has an identity.");
        foreach (var t in new[] { -1.0, 0, 1, 2, 4, 8 })
            Assert(Sample(original, t, "bg/_Amount") == Sample(session, t, "bg/_Amount"), "Import keeps runtime sampling.");
        var revision = session.Revision;
        using (var drag = session.BeginEdit(revision, "Adjust gain"))
        {
            drag.SetDefaultValue("gain/_Multiply", Vector(1.4f)); drag.SetDefaultValue("gain/_Multiply", Vector(1.8f));
            Near(Sample(drag.CompilePreview(), 2, "gain/_Multiply").Number.X, 1.8f, "Private drag can be previewed.");
            Near(Sample(session, 2, "gain/_Multiply").Number.X, 1.2f, "Draft does not affect committed artwork.");
            Assert(drag.Commit() == revision + 1, "A drag commits one revision.");
        }
        Assert(session.IsDirty && session.UndoLabel == "Adjust gain", "Committed drag creates named history.");
        session.Undo(session.Revision);
        Assert(!session.IsDirty && !session.CanUndo && session.CanRedo, "One undo restores saved content, with one history entry.");
        Near(Sample(session, 2, "gain/_Multiply").Number.X, 1.2f, "Undo restores gain.");
        Assert(session.Revision == revision + 2, "Undo does not roll the concurrency revision backward.");
        session.Redo(session.Revision); Near(Sample(session, 2, "gain/_Multiply").Number.X, 1.8f, "Redo restores the edit.");
        revision = session.Revision;
        using (var drag = session.BeginEdit(revision))
        { drag.SetDefaultValue("gain/_Multiply", Vector(.8f)); drag.SetDefaultValue("gain/_Multiply", Vector(1.8f)); Assert(drag.Commit() == revision, "Dragging back to the initial value is a no-op."); }
        using (var canceled = session.BeginEdit(revision)) canceled.SetDefaultValue("gain/_Multiply", Vector(.2f));
        Assert(session.Revision == revision, "Cancel/Dispose publishes nothing.");
        var snapshot = session.Snapshot(); snapshot.Runtime.Channels[0].Default[0] = 99; snapshot.Tracks[0].KeyIds[0] = "corrupt";
        Assert(session.Snapshot().Runtime.Channels[0].Default[0] == 0 && session.Snapshot().Tracks[0].KeyIds[0] != "corrupt", "Returned snapshots cannot mutate the session.");
        Assert(original.Document.Channels[0].Default[0] == 0, "Import does not edit original runtime document.");
        _groups.Add("Import, draft preview, one-drag undo, redo, cancel, no-op and snapshot isolation");
    }

    private void KeysAndCurves()
    {
        var session = Session(); var first = Key(session, "bg/_Amount", 0); var last = Key(session, "bg/_Amount", 1);
        var trackId = Track(session.Snapshot(), "bg/_Amount");
        using (var edit = session.BeginEdit(session.Revision, "Move key to beat 7"))
        { edit.MoveKey(last, KeyPosition.Beat(7)); edit.Commit(); }
        var channel = session.Snapshot().Runtime.Channels.Single(c => c.Property == "_Amount");
        Assert(channel.Keys[1].Beat == 7 && channel.Keys[1].Seconds == null, "Keep musical beat representation.");
        Near(Sample(session, 1.5, "bg/_Amount").Number.X, .5f, "Mixed BPM map resolves beat 7 to 3 seconds.");
        Assert(Key(session, "bg/_Amount", 1) == last, "Moving a key preserves identity.");
        string added;
        using (var edit = session.BeginEdit(session.Revision))
        {
            added = edit.AddKey("bg/_Amount", KeyPosition.Seconds(4), Number(.6f));
            Reject(() => edit.MoveKey(added, KeyPosition.Seconds(0)), AuthoringEditError.DuplicateTime);
            Near(Sample(edit.CompilePreview(), 3.5, "bg/_Amount").Number.X, .8f, "Failed move does not damage the private draft.");
            edit.Commit();
        }
        using (var edit = session.BeginEdit(session.Revision)) { edit.MoveKey(added, KeyPosition.Seconds(-.5)); edit.Commit(); }
        Assert(Key(session, "bg/_Amount", 0) == added && Key(session, "bg/_Amount", 1) == first, "Reordering keeps key/value identities aligned.");
        session.Undo(session.Revision);
        var curve = new[] { new CurvePointDocument { X = 0, Y = 0 }, new CurvePointDocument { X = .5, Y = 1.2 }, new CurvePointDocument { X = 1, Y = 1 } };
        using (var edit = session.BeginEdit(session.Revision))
        {
            edit.SetKeyEase(first, Easing.Curve, curve); curve[1].Y = -99;
            Near(Sample(edit.CompilePreview(), 1.5, "bg/_Amount").Number.X, 1.2f, "Curve is copied and artistic overshoot is preserved.");
            edit.Commit();
        }
        Assert(!session.CanRedo, "New edit clears the abandoned redo branch.");
        using (var edit = session.BeginEdit(session.Revision)) { edit.RemoveKey(added); edit.Commit(); }
        Assert(!session.Snapshot().Tracks.Single(t => t.ParameterId == "bg/_Amount").KeyIds.Contains(added), "Remove selected key.");
        session.Undo(session.Revision);
        Assert(session.Snapshot().Tracks.Single(t => t.ParameterId == "bg/_Amount").KeyIds.Contains(added), "Undo restores the same key identity.");
        Assert(Track(session.Snapshot(), "bg/_Amount") == trackId, "Track identity survives all edits.");
        using (var edit = session.BeginEdit(session.Revision))
        {
            var choice = edit.AddKey("bg/_MainTex", KeyPosition.Seconds(3), new(default, "red"));
            Fail<ArgumentException>(() => edit.SetKeyEase(choice, Easing.Linear));
            Assert(Sample(edit.CompilePreview(), 3, "bg/_MainTex").Choice == "red", "Choice key retains valid hold semantics after an invalid easing edit.");
            edit.Commit();
        }
        _groups.Add("Key add/value/time/delete, stable selection IDs, beat semantics, curves and choice interpolation");
    }

    private void ConflictsAndGuards()
    {
        var session = Session(); var key = Key(session, "bg/_Amount", 0);
        using var human = session.BeginEdit(session.Revision); using var oldAgent = session.BeginEdit(session.Revision);
        human.SetKeyValue(key, Number(.2f)); oldAgent.SetKeyValue(key, Number(.8f)); human.Commit();
        Reject(() => oldAgent.Commit(), AuthoringEditError.RevisionConflict);
        Near(Sample(session, 0, "bg/_Amount").Number.X, .2f, "Stale agent cannot overwrite human work.");
        Reject(() => session.BeginEdit(1), AuthoringEditError.RevisionConflict);
        using var canceled = session.BeginEdit(session.Revision); canceled.Cancel();
        Reject(() => canceled.SetDefaultValue("bg/_Amount", Number(1)), AuthoringEditError.ClosedTransaction);
        Reject(() => canceled.Commit(), AuthoringEditError.ClosedTransaction);
        using var edit = session.BeginEdit(session.Revision);
        Reject(() => edit.RemoveKey("unknown"), AuthoringEditError.UnknownKey);
        Fail<PreviewEditException>(() => edit.SetKeyValue(key, Number(float.NaN)));
        Near(Sample(edit.CompilePreview(), 0, "bg/_Amount").Number.X, .2f, "Invalid value leaves draft intact.");
        var doc = AuthoringProjects.Import(Package()); doc.ParameterHints = new[] { new ParameterPresentation { Node = "bg", Property = "_Amount", ReadOnly = true } };
        var readOnly = new AuthoringSession(doc, root, Caps); using var guarded = readOnly.BeginEdit(readOnly.Revision);
        Fail<PreviewEditException>(() => guarded.MoveKey(Key(readOnly, "bg/_Amount", 0), KeyPosition.Seconds(1)));
        Fail<ArgumentException>(() => KeyPosition.Beat(double.NaN));
        doc = AuthoringProjects.Import(Package()); doc.Tracks[0].KeyIds[0] = doc.Tracks[0].KeyIds[1];
        Fail<InvalidDataException>(() => new AuthoringSession(doc, root, Caps));
        _groups.Add("Stale-agent conflicts, canceled transaction guards, invalid values and identity validation");
    }

    private void SaveAndReopen()
    {
        var store = new ProjectFileStore(Encode, Decode, Caps); var session = Session(false);
        using (var edit = session.BeginEdit(session.Revision))
        { edit.MoveKey(Key(session, "bg/_Amount", 1), KeyPosition.Beat(7)); edit.SetDefaultValue("gain/_Multiply", Vector(1.7f)); edit.Commit(); }
        var before = session.Snapshot(); var file = Path.Combine(_work, "artwork.project.json");
        var saved = store.Save(session, file, session.Revision);
        Assert(saved.MatchesCurrentContent && !session.IsDirty, "Successful save marks exactly the saved content clean.");
        var disk = Decode(File.ReadAllText(file)); Assert(!Path.IsPathRooted(disk.AssetRoot), "Saved artwork uses a relative media root when possible.");
        var opened = store.Open(file); var restored = opened.Session.Snapshot();
        Assert(restored.ProjectId == before.ProjectId && restored.ContentId == before.ContentId, "Project/content identities survive reopen.");
        Assert(restored.Tracks.SelectMany(t => t.KeyIds).SequenceEqual(before.Tracks.SelectMany(t => t.KeyIds)), "Key selection IDs survive reopen.");
        foreach (var t in new[] { -1.0, 0, 1, 2, 3, 4, 9 })
            foreach (var id in before.Tracks.Select(p => p.ParameterId))
                Assert(Sample(opened.Session, t, id) == Sample(session, t, id), "Save/reopen keeps exact runtime samples.");
        var movedFile = Path.Combine(_work, "nested", "copy.project.json"); store.Save(session, movedFile, session.Revision);
        Assert(store.Open(movedFile).Session.Snapshot().AssetRoot == before.AssetRoot, "Save As rebases references without copying media.");
        var unicodeFile = Path.Combine(_work, "创作测试", "中文参数.project.json"); store.Save(session, unicodeFile, session.Revision);
        Assert(store.Open(unicodeFile).Session.Snapshot().ProjectId == before.ProjectId, "Chinese project paths save and reopen.");
        Reject(() => store.Save(session, file, session.Revision), AuthoringEditError.FileConflict);
        var external = File.ReadAllText(file) + "\n"; File.WriteAllText(file, external);
        using (var edit = session.BeginEdit(session.Revision)) { edit.SetDefaultValue("gain/_Multiply", Vector(.7f)); edit.Commit(); }
        Reject(() => store.Save(session, file, session.Revision, saved.FileVersion), AuthoringEditError.FileConflict);
        Assert(File.ReadAllText(file) == external && session.IsDirty, "External changes are preserved on save conflict.");
        var other = store.Open(movedFile); var bytes = File.ReadAllBytes(movedFile);
        var broken = new ProjectFileStore(_ => throw new IOException("codec failure"), Decode, Caps);
        Fail<IOException>(() => broken.Save(other.Session, movedFile, other.Session.Revision, other.FileVersion));
        Assert(File.ReadAllBytes(movedFile).SequenceEqual(bytes), "Failed serialization preserves the existing artwork.");
        var lossy = new ProjectFileStore(p => { var c = AuthoringProjects.Copy(p); c.Runtime.Channels[0].Keys = Array.Empty<KeyDocument>(); return Encode(c); }, Decode, Caps);
        Fail<InvalidDataException>(() => lossy.Save(other.Session, movedFile, other.Session.Revision, other.FileVersion));
        Assert(File.ReadAllBytes(movedFile).SequenceEqual(bytes), "Invalid codec roundtrip cannot replace the artwork.");
        var mutating = new ProjectFileStore(p => { p.Runtime.Seed++; return Encode(p); }, Decode, Caps);
        Fail<InvalidDataException>(() => mutating.Save(other.Session, movedFile, other.Session.Revision, other.FileVersion));
        Assert(File.ReadAllBytes(movedFile).SequenceEqual(bytes), "A mutating/lossy codec cannot alter the captured artwork.");
        var absent = Path.Combine(_work, "stale.project.json");
        Reject(() => store.Save(session, absent, 1), AuthoringEditError.RevisionConflict);
        Assert(!File.Exists(absent), "Stale save creates no file.");
        Assert(!Directory.EnumerateFiles(_work, "*.tmp", SearchOption.AllDirectories).Any(), "Successful/failed save leaves no temporary files.");
        _groups.Add("Atomic save/reopen, media rebasing, exact sample recovery, external edits and failure preservation");
    }

    private void BackgroundSave()
    {
        var session = Session(false); var first = Key(session, "bg/_Amount", 0); var oldRevision = session.Revision;
        var store = new ProjectFileStore(p =>
        {
            using var newer = session.BeginEdit(session.Revision); newer.SetKeyValue(first, Number(.3f)); newer.Commit();
            return Encode(p);
        }, Decode, Caps);
        var file = Path.Combine(_work, "background.project.json"); var result = store.Save(session, file, oldRevision);
        Assert(!result.MatchesCurrentContent && session.IsDirty, "Saving an older snapshot does not mark a newer edit clean.");
        Near(Sample(store.Open(file).Session, 0, "bg/_Amount").Number.X, 0, "Saved file contains the captured version.");
        Near(Sample(session, 0, "bg/_Amount").Number.X, .3f, "Newer in-memory edit survives the save.");
        session.Undo(session.Revision);
        Assert(!session.IsDirty, "Undo to the actual saved content becomes clean despite a higher revision.");
        _groups.Add("Editing during save and correct dirty-state recovery");
    }

    private void MissingAssets()
    {
        var project = AuthoringProjects.Import(Package()); project.AssetRoot = Path.Combine(_work, "missing-media");
        var session = new AuthoringSession(project, _work, Caps);
        using (var edit = session.BeginEdit(session.Revision)) { edit.SetDefaultValue("gain/_Multiply", Vector(.6f)); edit.Commit(); }
        Near(Sample(session, 2, "gain/_Multiply").Number.X, .6f, "Missing media does not block authoring.");
        Fail<FileNotFoundException>(() => session.CompileSnapshot());
        var store = new ProjectFileStore(Encode, Decode, Caps); var file = Path.Combine(_work, "missing-media.project.json");
        store.Save(session, file, session.Revision); var opened = store.Open(file);
        Assert(!opened.Session.IsDirty, "Missing-media project can still be saved and reopened.");
        project.Runtime.Nodes[0].Bundle = "../escape.bundle";
        Fail<InvalidDataException>(() => new AuthoringSession(project, _work, Caps));
        var runtime = Package().Document; runtime.Nodes[0].Bundle = "missing.bundle";
        Fail<FileNotFoundException>(() => EffectPackage.FromDocument(runtime, Package().Root, Caps));
        Assert(!Directory.Exists(Path.Combine(_work, "missing-media")), "Editing did not synthesize or copy media folders.");
        _groups.Add("Missing-media editing remains possible; runtime resource checks and path boundaries remain strict");
    }
}
