namespace InFalsus.Presentation;

public enum TimeDomain { Audio, Chart }
public enum TransportState { Stopped, Frozen, Running }
public enum PlaybackPhase { Loading, Countdown, Playing, Paused, Ending }
[Flags]
public enum ResetReason { None = 0, Session = 1, Seek = 2, ObservedTimeJump = 4, Reload = 8, Resize = 16 }

/// <summary>One coherent main-thread snapshot. AudioSeconds is the practice-mode clock.</summary>
public readonly record struct PlaybackSnapshot(
    long SessionGeneration, string SongKey, int Difficulty, string ChartFingerprint,
    double AudioSeconds, double ChartSeconds, int InternalOffsetMilliseconds,
    float PlaybackRate, TransportState Transport, PlaybackPhase Phase,
    bool PauseUiActive, bool IsPractice, bool SeekPending,
    ulong SeekSerial, bool HasExactSeekSerial, double MonotonicSeconds)
{
    public bool IsPaused => Transport == TransportState.Frozen;
    public bool IsValid => SessionGeneration > 0 && !string.IsNullOrWhiteSpace(SongKey)
        && Difficulty >= 0 && double.IsFinite(AudioSeconds) && double.IsFinite(ChartSeconds)
        && double.IsFinite(MonotonicSeconds) && float.IsFinite(PlaybackRate) && PlaybackRate >= 0;
}

public readonly record struct FrameContext(
    PlaybackSnapshot Playback, double Seconds, double Beat, double DeltaSeconds,
    long VisualTick, uint Seed, ulong ClockRevision, ResetReason ResetReason)
{
    public bool IsPaused => Playback.IsPaused;
}

/// <summary>No accumulated song clock. Seeking while paused still produces a new sample.</summary>
public sealed class ClockSource
{
    private PlaybackSnapshot _previous;
    private double _previousSeconds;
    private bool _hasPrevious;
    private ulong _revision;

    public FrameContext Sample(in PlaybackSnapshot snapshot, TempoMap tempo,
        TimeDomain domain = TimeDomain.Audio, double offsetSeconds = 0, uint seed = 1,
        ResetReason explicitReset = ResetReason.None)
    {
        if (!snapshot.IsValid) throw new ArgumentException("Invalid playback snapshot.", nameof(snapshot));
        if (!double.IsFinite(offsetSeconds)) throw new ArgumentException("Non-finite effect offset.");
        var seconds = (domain == TimeDomain.Audio ? snapshot.AudioSeconds : snapshot.ChartSeconds) + offsetSeconds;
        if (!double.IsFinite(seconds) || Math.Abs(seconds) > 86400 * 30)
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Effect time exceeds supported range.");
        var reason = explicitReset;
        if (!_hasPrevious || snapshot.SessionGeneration != _previous.SessionGeneration)
            reason |= ResetReason.Session;
        else
        {
            if (snapshot.HasExactSeekSerial && _previous.HasExactSeekSerial && snapshot.SeekSerial != _previous.SeekSerial)
                reason |= ResetReason.Seek;
            // Conservative fallback only. It cannot identify all small/same-target seeks.
            var dt = Math.Max(0, snapshot.MonotonicSeconds - _previous.MonotonicSeconds);
            var expected = _previous.Transport == TransportState.Running ? dt * _previous.PlaybackRate : 0;
            var audioDelta = snapshot.AudioSeconds - _previous.AudioSeconds;
            if (audioDelta < -0.000001 || Math.Abs(audioDelta - expected) > 0.25)
                reason |= ResetReason.ObservedTimeJump;
            if (seconds < _previousSeconds - 0.000001) reason |= ResetReason.ObservedTimeJump;
        }
        if (reason != ResetReason.None) _revision++;
        var delta = reason == ResetReason.None && _hasPrevious ? seconds - _previousSeconds : 0;
        _previous = snapshot; _previousSeconds = seconds; _hasPrevious = true;
        return new(snapshot, seconds, tempo.ToBeat(seconds), delta,
            checked((long)Math.Floor(seconds * 120)), seed, _revision, reason);
    }

    public void Clear() => _hasPrevious = false;
}

public readonly record struct TempoChange(double Seconds, double Bpm);

/// <summary>Seconds are in the package's declared domain. Beat zero is time zero.</summary>
public sealed class TempoMap
{
    private readonly double[] _seconds, _beats, _bpm;
    public TempoMap(IEnumerable<TempoChange> changes)
    {
        var rows = changes.ToArray();
        if (rows.Length == 0 || rows[0].Seconds != 0) throw new ArgumentException("Tempo map must start at zero.");
        _seconds = new double[rows.Length]; _beats = new double[rows.Length]; _bpm = new double[rows.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            var c = rows[i];
            if (!double.IsFinite(c.Seconds) || !double.IsFinite(c.Bpm) || c.Bpm <= 0
                || (i > 0 && c.Seconds <= rows[i - 1].Seconds))
                throw new ArgumentException("Tempo times must increase; BPM must be finite and positive.");
            _seconds[i] = c.Seconds; _bpm[i] = c.Bpm;
            if (i > 0) _beats[i] = _beats[i - 1] + (c.Seconds - _seconds[i - 1]) * _bpm[i - 1] / 60;
            if (!double.IsFinite(_beats[i])) throw new ArgumentException("Tempo integral overflow.");
        }
    }
    public double ToBeat(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentException("Invalid seconds.");
        var i = Find(_seconds, seconds);
        return _beats[i] + (seconds - _seconds[i]) * _bpm[i] / 60;
    }
    public double ToSeconds(double beat)
    {
        if (!double.IsFinite(beat)) throw new ArgumentException("Invalid beat.");
        var i = Find(_beats, beat);
        return _seconds[i] + (beat - _beats[i]) * 60 / _bpm[i];
    }
    private static int Find(double[] sorted, double value)
    {
        var i = Array.BinarySearch(sorted, value);
        return i >= 0 ? i : Math.Max(0, ~i - 1);
    }
}
