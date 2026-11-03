namespace InFalsus.Presentation.Authoring;

public enum PreviewEditError
{
    WrongSession, WrongRevision, StaleSequence, UnknownParameter, DuplicateParameter,
    ReadOnlyParameter, InvalidValue, InvalidChoice, OutsideHardRange
}

public sealed class PreviewEditException : InvalidOperationException
{
    public PreviewEditError Code { get; }
    public string? ParameterId { get; }
    public PreviewEditException(PreviewEditError code, string? parameterId = null)
        : base(parameterId == null ? code.ToString() : code + ": " + parameterId)
        => (Code, ParameterId) = (code, parameterId);
}

public sealed class PreviewOverride
{
    public string ParameterId { get; }
    public ParameterValue Value { get; }
    public PreviewOverride(string parameterId, ParameterValue value) => (ParameterId, Value) = (parameterId, value);
}

/// <summary>Replaces the whole temporary override set. Empty Overrides means cancel/reset.</summary>
public sealed class PreviewEdit
{
    public string SessionId { get; }
    public long ExpectedRevision { get; }
    public long Sequence { get; }
    public IReadOnlyList<PreviewOverride> Overrides { get; }
    public PreviewEdit(string sessionId, long expectedRevision, long sequence, IEnumerable<PreviewOverride> overrides)
    {
        SessionId = sessionId; ExpectedRevision = expectedRevision; Sequence = sequence;
        Overrides = Array.AsReadOnly((overrides ?? throw new ArgumentNullException(nameof(overrides))).ToArray());
    }
}

public enum PreviewValueSource { Default, Timeline, TemporaryOverride }

public sealed class PreviewParameterSample
{
    public ParameterDescriptor Parameter { get; }
    public ParameterValue Value { get; }
    public PreviewValueSource Source { get; }
    internal PreviewParameterSample(ParameterDescriptor parameter, ParameterValue value, PreviewValueSource source)
        => (Parameter, Value, Source) = (parameter, value, source);
}

public sealed class PreviewStamp
{
    public string SessionId { get; }
    public long DocumentRevision { get; }
    public long EditSequence { get; }
    public double Seconds { get; }
    public bool IncludesTemporaryOverrides { get; }
    internal PreviewStamp(string sessionId, long revision, long sequence, double seconds, bool includesOverrides)
        => (SessionId, DocumentRevision, EditSequence, Seconds, IncludesTemporaryOverrides) =
            (sessionId, revision, sequence, seconds, includesOverrides);
}

public sealed class PreviewInspection
{
    public PreviewStamp Stamp { get; }
    public IReadOnlyList<PreviewParameterSample> Parameters { get; }
    internal PreviewInspection(PreviewStamp stamp, PreviewParameterSample[] parameters)
        => (Stamp, Parameters) = (stamp, Array.AsReadOnly(parameters));
}

/// <summary>Call from a GUI, CLI or agent adapter; time is already resolved by the shared clock.</summary>
public interface IParameterPreviewSession
{
    string SessionId { get; }
    long DocumentRevision { get; }
    long LastSequence { get; }
    ParameterCatalog Catalog { get; }
    void ReplaceOverrides(PreviewEdit edit);
    PreviewStamp SampleInto(double seconds, Span<ParameterValue> output, bool includeTemporaryOverrides = true);
    PreviewInspection Inspect(double seconds, bool includeTemporaryOverrides = true);
}

/// <summary>Temporary, atomic preview state. Never changes the package, timeline, clock or disk.</summary>
public sealed class ParameterPreviewSession : IParameterPreviewSession
{
    private sealed class State
    {
        internal readonly long Sequence;
        internal readonly Dictionary<int, ParameterValue> Values;
        internal State(long sequence, Dictionary<int, ParameterValue> values) => (Sequence, Values) = (sequence, values);
    }
    private readonly object _gate = new();
    private State _state = new(0, new Dictionary<int, ParameterValue>());
    public string SessionId { get; }
    public long DocumentRevision { get; }
    public long LastSequence => Capture().Sequence;
    public ParameterCatalog Catalog { get; }

    public ParameterPreviewSession(ParameterCatalog catalog, long documentRevision = 1, string? sessionId = null)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (documentRevision <= 0) throw new ArgumentOutOfRangeException(nameof(documentRevision));
        SessionId = sessionId ?? Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(SessionId)) throw new ArgumentException("Session ID must not be empty.");
        DocumentRevision = documentRevision;
    }

    public void ReplaceOverrides(PreviewEdit edit)
    {
        if (edit == null) throw new ArgumentNullException(nameof(edit));
        lock (_gate)
        {
            if (edit.SessionId != SessionId) throw new PreviewEditException(PreviewEditError.WrongSession);
            if (edit.ExpectedRevision != DocumentRevision) throw new PreviewEditException(PreviewEditError.WrongRevision);
            if (edit.Sequence <= _state.Sequence) throw new PreviewEditException(PreviewEditError.StaleSequence);
            var values = new Dictionary<int, ParameterValue>();
            foreach (var item in edit.Overrides)
            {
                if (item == null) throw new PreviewEditException(PreviewEditError.InvalidValue);
                var i = Catalog.IndexOf(item.ParameterId);
                Catalog.Parameters[i].ValidateOverride(item.Value);
                if (!values.TryAdd(i, item.Value))
                    throw new PreviewEditException(PreviewEditError.DuplicateParameter, item.ParameterId);
            }
            // Validate the complete edit before publishing. Old states are never mutated.
            _state = new State(edit.Sequence, values);
        }
    }

    private State Capture() { lock (_gate) return _state; }
    private void Sample(double seconds, Span<ParameterValue> output, State state, bool includeOverrides)
    {
        Catalog.Timeline.Sample(seconds, output);
        if (includeOverrides) foreach (var pair in state.Values) output[pair.Key] = pair.Value;
    }
    private PreviewStamp Stamp(double seconds, State state, bool includeOverrides)
        => new(SessionId, DocumentRevision, state.Sequence, seconds, includeOverrides);

    public PreviewStamp SampleInto(double seconds, Span<ParameterValue> output, bool includeTemporaryOverrides = true)
    {
        var state = Capture(); Sample(seconds, output, state, includeTemporaryOverrides);
        return Stamp(seconds, state, includeTemporaryOverrides);
    }

    public PreviewInspection Inspect(double seconds, bool includeTemporaryOverrides = true)
    {
        var state = Capture(); var values = new ParameterValue[Catalog.Parameters.Count];
        Sample(seconds, values, state, includeTemporaryOverrides);
        var parameters = new PreviewParameterSample[values.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var descriptor = Catalog.Parameters[i];
            var source = includeTemporaryOverrides && state.Values.ContainsKey(i) ? PreviewValueSource.TemporaryOverride :
                seconds >= descriptor.FirstKeySeconds ? PreviewValueSource.Timeline : PreviewValueSource.Default;
            parameters[i] = new(descriptor, values[i], source);
        }
        return new(Stamp(seconds, state, includeTemporaryOverrides), parameters);
    }
}
