namespace InFalsus.Presentation;

public enum PreparationState { Pending, Ready, Failed }

/// <summary>Backend work is polled on the game main thread; no Unity work on task continuations.</summary>
public interface IEffectPreparation : IDisposable
{
    PreparationState Poll();
    string? Error { get; }
    IEffectNode Take();
}
public interface IEffectNode : IDisposable
{
    void Apply(in FrameContext frame, ReadOnlySpan<ParameterValue> values);
    void Reset(ResetReason reason);
    // Must leave the old presentation intact if activation fails.
    void Activate(IEffectNode? previous);
}
public interface IRenderBackend
{
    IReadOnlyCollection<string> Capabilities { get; }
    IEffectPreparation Prepare(EffectPackage package, in PlaybackSnapshot snapshot);
}

/// <summary>Owns one song's active/pending effect. Loading failures retain the active package.</summary>
public sealed class PresentationSession : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly Action<string> _report;
    private readonly ClockSource _clock = new();
    private IEffectPreparation? _pending;
    private EffectPackage? _pendingPackage, _package;
    private IEffectNode? _active;
    private ParameterValue[] _values = Array.Empty<ParameterValue>();
    private long _generation;
    private bool _disposed;
    public bool IsActive => _active != null;
    public string? ActivePackageId => _package?.Document.Id;
    public FrameContext? LastFrame { get; private set; }

    public PresentationSession(IRenderBackend backend, Action<string> report)
        => (_backend, _report) = (backend, report);

    public bool Request(EffectPackage package, in PlaybackSnapshot snapshot)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PresentationSession));
        if (!package.Matches(snapshot)) return false;
        if (_generation != snapshot.SessionGeneration) End();
        _generation = snapshot.SessionGeneration;
        DisposeSafely(_pending); _pending = null; _pendingPackage = null;
        try { _pending = _backend.Prepare(package, snapshot); _pendingPackage = package; return true; }
        catch (Exception e) { _report($"Prepare {package.Document.Id}: {e}"); return false; }
    }

    public void Tick(in PlaybackSnapshot snapshot)
    {
        if (_disposed) return;
        if (!snapshot.IsValid || _generation != snapshot.SessionGeneration
            || _package != null && !_package.Matches(snapshot)) { End(); return; }
        if (_pending != null)
        {
            IEffectNode? candidate = null;
            try
            {
                if (_pendingPackage == null || !_pendingPackage.Matches(snapshot))
                    throw new InvalidOperationException("Pending package belongs to an obsolete song.");
                var state = _pending.Poll();
                if (state == PreparationState.Failed) throw new InvalidOperationException(_pending.Error ?? "Effect preparation failed.");
                if (state == PreparationState.Ready)
                {
                    candidate = _pending.Take();
                    var p = _pendingPackage;
                    var values = new ParameterValue[p.Timeline.Count];
                    // Use a temporary clock until activation succeeds; old package keeps its domain/state on failure.
                    var frame = new ClockSource().Sample(snapshot, p.Tempo, p.Document.TimeDomain,
                        p.Document.OffsetSeconds, p.Document.Seed, ResetReason.Reload);
                    p.Timeline.Sample(frame.Seconds, values);
                    candidate.Reset(frame.ResetReason); candidate.Apply(frame, values); candidate.Activate(_active);
                    var old = _active; _active = candidate; candidate = null;
                    _package = p; _values = values; _clock.Clear();
                    _report($"Presentation active: {p.Document.Id}; domain={p.Document.TimeDomain}; seconds={frame.Seconds:F3}");
                    DisposeSafely(old);
                    DisposeSafely(_pending); _pending = null; _pendingPackage = null;
                }
            }
            catch (Exception e)
            {
                DisposeSafely(candidate); DisposeSafely(_pending); _pending = null; _pendingPackage = null;
                _report($"Effect load: {e}");
            }
        }
        if (_active == null || _package == null) return;
        try
        {
            var p = _package;
            var frame = _clock.Sample(snapshot, p.Tempo, p.Document.TimeDomain, p.Document.OffsetSeconds, p.Document.Seed);
            if (frame.ResetReason != ResetReason.None) _active.Reset(frame.ResetReason);
            p.Timeline.Sample(frame.Seconds, _values);
            _active.Apply(frame, _values); LastFrame = frame;
        }
        catch (Exception e) { _report($"Effect tick: {e}"); End(); }
    }

    private void DisposeSafely(IDisposable? owned)
    {
        try { owned?.Dispose(); } catch (Exception e) { _report($"Effect cleanup: {e.Message}"); }
    }
    public void End()
    {
        DisposeSafely(_pending); _pending = null; _pendingPackage = null;
        DisposeSafely(_active); _active = null; _package = null;
        _values = Array.Empty<ParameterValue>(); _clock.Clear(); LastFrame = null; _generation = 0;
    }
    public void Dispose() { if (_disposed) return; End(); _disposed = true; }
}
