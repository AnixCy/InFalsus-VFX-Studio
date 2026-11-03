namespace InFalsus.Presentation.Authoring;

public enum AuthoringEditError { RevisionConflict, ClosedTransaction, UnknownKey, DuplicateTime, FileConflict }
public sealed class AuthoringEditException : InvalidOperationException
{
    public AuthoringEditError Code { get; }
    public AuthoringEditException(AuthoringEditError code) : base(code.ToString()) => Code = code;
}

/// <summary>One in-memory artwork. Revisions increase even on undo; saved-content identity is separate.</summary>
public sealed class AuthoringSession
{
    private sealed class History
    {
        internal readonly AuthoringProjectDocument Document;
        internal readonly string Label;
        internal History(AuthoringProjectDocument document, string label) => (Document, Label) = (document, label);
    }
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string[] _capabilities;
    private readonly int _historyLimit;
    private readonly List<History> _undo = new(), _redo = new();
    private AuthoringProjectDocument _current;
    private string? _savedContent;
    public long Revision { get { lock (_gate) return _current.Revision; } }
    public bool IsDirty { get { lock (_gate) return _current.ContentId != _savedContent; } }
    public bool CanUndo { get { lock (_gate) return _undo.Count != 0; } }
    public bool CanRedo { get { lock (_gate) return _redo.Count != 0; } }
    public string? UndoLabel { get { lock (_gate) return _undo.LastOrDefault()?.Label; } }
    public string? RedoLabel { get { lock (_gate) return _redo.LastOrDefault()?.Label; } }

    public AuthoringSession(AuthoringProjectDocument document, string projectDirectory,
        IReadOnlyCollection<string> capabilities, bool isSaved = false, int historyLimit = 100)
    {
        if (historyLimit < 1) throw new ArgumentOutOfRangeException(nameof(historyLimit));
        _directory = Path.GetFullPath(projectDirectory); _capabilities = capabilities.ToArray(); _historyLimit = historyLimit;
        _current = AuthoringProjects.Copy(document);
        AuthoringProjects.Compile(_current, _directory, _capabilities, requireResources: false);
        _current.AssetRoot = Path.GetFullPath(Path.Combine(_directory, _current.AssetRoot));
        if (isSaved) _savedContent = _current.ContentId;
    }

    public AuthoringProjectDocument Snapshot() { lock (_gate) return AuthoringProjects.Copy(_current); }
    public EffectPackage CompileSnapshot(bool requireResources = true)
        => AuthoringProjects.Compile(Snapshot(), _directory, _capabilities, requireResources);
    public AuthoringEdit BeginEdit(long expectedRevision, string label = "Edit")
    {
        lock (_gate)
        {
            RequireRevision(expectedRevision);
            return new AuthoringEdit(this, AuthoringProjects.Copy(_current), _capabilities, _directory, label);
        }
    }
    private void RequireRevision(long revision)
    { if (revision != _current.Revision) throw new AuthoringEditException(AuthoringEditError.RevisionConflict); }
    private void Push(List<History> list, AuthoringProjectDocument document, string label)
    {
        list.Add(new History(document, label));
        if (list.Count > _historyLimit) list.RemoveAt(0);
    }
    internal long Commit(AuthoringEdit edit)
    {
        lock (_gate)
        {
            RequireRevision(edit.BaseRevision);
            if (AuthoringProjects.SameEditableContent(_current, edit.Draft)) return _current.Revision;
            var candidate = AuthoringProjects.Copy(edit.Draft);
            AuthoringProjects.Compile(candidate, _directory, _capabilities, requireResources: false);
            candidate.Revision = checked(_current.Revision + 1); candidate.ContentId = Guid.NewGuid().ToString("N");
            Push(_undo, _current, edit.Label); _redo.Clear(); _current = candidate;
            return _current.Revision;
        }
    }
    public bool Undo(long expectedRevision) => Navigate(expectedRevision, _undo, _redo);
    public bool Redo(long expectedRevision) => Navigate(expectedRevision, _redo, _undo);
    private bool Navigate(long expectedRevision, List<History> from, List<History> to)
    {
        lock (_gate)
        {
            RequireRevision(expectedRevision);
            if (from.Count == 0) return false;
            var item = from[from.Count - 1]; var candidate = AuthoringProjects.Copy(item.Document);
            candidate.Revision = checked(_current.Revision + 1);
            Push(to, _current, item.Label); from.RemoveAt(from.Count - 1); _current = candidate;
            return true;
        }
    }
    internal AuthoringProjectDocument CaptureForSave(long expectedRevision, string path)
    {
        lock (_gate)
        {
            RequireRevision(expectedRevision);
            var copy = AuthoringProjects.Copy(_current);
            copy.AssetRoot = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(path))!, _current.AssetRoot);
            return copy;
        }
    }
    internal bool MarkSaved(string contentId)
    {
        lock (_gate) { _savedContent = contentId; return _current.ContentId == contentId; }
    }
}

public readonly struct KeyPosition
{
    public bool UsesBeat { get; }
    public double Value { get; }
    private KeyPosition(double value, bool beat)
    {
        if (!double.IsFinite(value)) throw new ArgumentException("Key position must be finite.");
        Value = value; UsesBeat = beat;
    }
    public static KeyPosition Seconds(double value) => new(value, false);
    public static KeyPosition Beat(double value) => new(value, true);
    internal void Apply(KeyDocument key) { key.Seconds = UsesBeat ? null : Value; key.Beat = UsesBeat ? Value : null; }
}

/// <summary>A drag/batch edits a private draft; commit publishes one history entry, cancel publishes nothing.</summary>
public sealed class AuthoringEdit : IDisposable
{
    private readonly AuthoringSession _owner;
    private readonly ParameterCatalog _catalog;
    private readonly TempoMap _tempo;
    private readonly string[] _capabilities;
    private readonly string _directory;
    private bool _closed;
    private bool _graphReplaced;
    internal AuthoringProjectDocument Draft { get; }
    internal string Label { get; }
    public long BaseRevision { get; }

    internal AuthoringEdit(AuthoringSession owner, AuthoringProjectDocument draft, string[] capabilities, string directory, string label)
    {
        _owner = owner; Draft = draft; _capabilities = capabilities; _directory = directory;
        BaseRevision = draft.Revision; Label = label;
        var package = AuthoringProjects.Compile(draft, directory, capabilities, requireResources: false);
        _catalog = new ParameterCatalog(package, draft.ParameterHints); _tempo = package.Tempo;
    }
    private void Open() { if (_closed) throw new AuthoringEditException(AuthoringEditError.ClosedTransaction); }
    private int Track(string parameterId) { Open();if(_graphReplaced)throw new InvalidOperationException("Commit the graph replacement before editing parameters.");return _catalog.IndexOf(parameterId); }
    private (int Track, int Key) Find(string keyId)
    {
        Open();
        if(_graphReplaced)throw new InvalidOperationException("Commit the graph replacement before editing keys.");
        for (var i = 0; i < Draft.Tracks.Length; i++)
        {
            var j = Array.IndexOf(Draft.Tracks[i].KeyIds, keyId);
            if (j >= 0) return (i, j);
        }
        throw new AuthoringEditException(AuthoringEditError.UnknownKey);
    }
    private static ParameterValue ReadValue(ParameterKind kind, double[] value, string? choice)
        => kind == ParameterKind.Choice ? new(default, choice) : kind == ParameterKind.Float ?
            new(new((float)value[0], 0, 0, 0)) : new(new((float)value[0], (float)value[1], (float)value[2], (float)value[3]));
    private static double[] Numbers(ParameterKind kind, ParameterValue value)
    {
        var v = value.Number;
        return kind == ParameterKind.Choice ? Array.Empty<double>() : kind == ParameterKind.Float ? new[] { (double)v.X } :
            new[] { (double)v.X, v.Y, v.Z, v.W };
    }
    public void SetDefaultValue(string parameterId, ParameterValue value)
    {
        var i = Track(parameterId); var p = _catalog.Parameters[i]; p.ValidateOverride(value);
        var c = Draft.Runtime.Channels[i];
        if (ReadValue(c.Kind, c.Default, c.DefaultChoice) == value) return;
        c.Default = Numbers(c.Kind, value); c.DefaultChoice = value.Choice;
    }
    private List<(string Id, KeyDocument Key)> Keys(int track)
        => Draft.Runtime.Channels[track].Keys.Select((k, i) => (Draft.Tracks[track].KeyIds[i], AuthoringProjects.CopyKey(k))).ToList();
    private void PublishKeys(int track, List<(string Id, KeyDocument Key)> keys)
    {
        double Time(KeyDocument k) => k.Seconds ?? _tempo.ToSeconds(k.Beat!.Value);
        keys.Sort((a, b) => Time(a.Key).CompareTo(Time(b.Key)));
        for (var i = 1; i < keys.Count; i++)
            if (Time(keys[i - 1].Key) == Time(keys[i].Key)) throw new AuthoringEditException(AuthoringEditError.DuplicateTime);
        var c = Draft.Runtime.Channels[track]; var binding = _catalog.Parameters[track].Binding;
        // Reuse the runtime curve/type validation before changing even this private draft.
        _ = new ChannelTrack(binding, ReadValue(c.Kind, c.Default, c.DefaultChoice), keys.Select(p =>
            new TimelineKey(Time(p.Key), ReadValue(c.Kind, p.Key.Value, p.Key.Choice), p.Key.Ease,
                p.Key.Curve?.Select(v => new CurvePoint(v.X, v.Y)).ToArray())));
        c.Keys = keys.Select(k => k.Key).ToArray(); Draft.Tracks[track].KeyIds = keys.Select(k => k.Id).ToArray();
    }
    public string AddKey(string parameterId, KeyPosition position, ParameterValue value, Easing? ease = null)
    {
        if (ease.HasValue && !Enum.IsDefined(typeof(Easing), ease.Value)) throw new ArgumentException("Unknown easing.");
        var i = Track(parameterId); var p = _catalog.Parameters[i]; p.ValidateOverride(value);
        var key = new KeyDocument { Value = Numbers(p.Binding.Kind, value), Choice = value.Choice,
            Ease = ease ?? (p.Binding.Kind == ParameterKind.Choice ? Easing.Hold : Easing.Linear) };
        position.Apply(key); var id = Guid.NewGuid().ToString("N"); var keys = Keys(i); keys.Add((id, key)); PublishKeys(i, keys);
        return id;
    }
    public void SetKeyValue(string keyId, ParameterValue value)
    {
        var (i, j) = Find(keyId); var p = _catalog.Parameters[i]; p.ValidateOverride(value);
        var keys = Keys(i); keys[j].Key.Value = Numbers(p.Binding.Kind, value); keys[j].Key.Choice = value.Choice; PublishKeys(i, keys);
    }
    public void MoveKey(string keyId, KeyPosition position)
    {
        var (i, j) = Find(keyId);
        if (_catalog.Parameters[i].ReadOnly) throw new PreviewEditException(PreviewEditError.ReadOnlyParameter, _catalog.Parameters[i].Id);
        var keys = Keys(i); position.Apply(keys[j].Key); PublishKeys(i, keys);
    }
    public void RemoveKey(string keyId)
    {
        var (i, j) = Find(keyId);
        if (_catalog.Parameters[i].ReadOnly) throw new PreviewEditException(PreviewEditError.ReadOnlyParameter, _catalog.Parameters[i].Id);
        var keys = Keys(i); keys.RemoveAt(j); PublishKeys(i, keys);
    }
    public void SetKeyEase(string keyId, Easing ease, IEnumerable<CurvePointDocument>? curve = null)
    {
        var (i, j) = Find(keyId);
        if (_catalog.Parameters[i].ReadOnly) throw new PreviewEditException(PreviewEditError.ReadOnlyParameter, _catalog.Parameters[i].Id);
        if (!Enum.IsDefined(typeof(Easing), ease)) throw new ArgumentException("Unknown easing.");
        var keys = Keys(i); keys[j].Key.Ease = ease;
        keys[j].Key.Curve = curve?.Select(p => new CurvePointDocument { X = p.X, Y = p.Y }).ToArray(); PublishKeys(i, keys);
    }
    public EffectPackage CompilePreview()
    { Open(); return AuthoringProjects.Compile(Draft, _directory, _capabilities, requireResources: false); }
    /// <summary>Replace a material graph in this transaction. Preserve identities at unchanged parameter/time positions.</summary>
    public void ReplaceGraph(PackageDocument runtime, IEnumerable<ParameterPresentation> hints)
    {
        Open();
        if(_graphReplaced)throw new InvalidOperationException("Only one graph replacement is allowed per transaction.");
        var candidate = AuthoringProjects.Copy(Draft);
        candidate.Runtime = AuthoringProjects.CopyRuntime(runtime);
        candidate.ParameterHints = hints.ToArray();
        candidate.Tracks = candidate.Runtime.Channels.Select(channel => {
            var id = ParameterDescriptor.MakeId(channel.Node, channel.Property);
            var oldTrack = Draft.Tracks.FirstOrDefault(t => t.ParameterId == id);
            var oldChannel = Draft.Runtime.Channels.FirstOrDefault(c => c.Node == channel.Node && c.Property == channel.Property && c.Kind == channel.Kind);
            var ids = new Dictionary<(double?, double?), string>();
            if (oldTrack != null && oldChannel != null)
                for (int i = 0; i < oldChannel.Keys.Length; i++) ids[(oldChannel.Keys[i].Seconds,oldChannel.Keys[i].Beat)] = oldTrack.KeyIds[i];
            return new TrackIdentityDocument { ParameterId=id,TrackId=oldTrack?.TrackId??Guid.NewGuid().ToString("N"),
                KeyIds=channel.Keys.Select(k=>ids.TryGetValue((k.Seconds,k.Beat),out var keyId)?keyId:Guid.NewGuid().ToString("N")).ToArray() };
        }).ToArray();
        _ = AuthoringProjects.Compile(candidate,_directory,_capabilities,requireResources:false);
        Draft.Runtime=candidate.Runtime;Draft.Tracks=candidate.Tracks;
        Draft.ParameterHints=AuthoringProjects.Copy(candidate).ParameterHints;
        _graphReplaced=true;
    }
    public long Commit() { Open(); var revision = _owner.Commit(this); _closed = true; return revision; }
    public void Cancel() => _closed = true;
    public void Dispose() => Cancel();
}
