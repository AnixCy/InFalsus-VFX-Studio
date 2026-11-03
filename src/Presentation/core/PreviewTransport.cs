namespace InFalsus.Presentation;

/// <summary>Editor supplies actual audio position; no game process or wall-clock accumulation required.</summary>
public readonly record struct PreviewTransportSnapshot(
    double AudioSeconds, int ChartOffsetMilliseconds, float Rate, bool IsPlaying,
    ulong SeekSerial, long Generation, double MonotonicSeconds);

public sealed class PreviewClock
{
    private readonly ClockSource _source = new();
    public FrameContext Sample(in PreviewTransportSnapshot transport, EffectPackage package)
    {
        // Identity is local to this preview session. Optional game binding is never consulted.
        var snapshot = new PlaybackSnapshot(transport.Generation, "preview:" + package.Document.Id, 0, "",
            transport.AudioSeconds, transport.AudioSeconds + transport.ChartOffsetMilliseconds / 1000.0,
            transport.ChartOffsetMilliseconds, transport.Rate,
            transport.IsPlaying ? TransportState.Running : TransportState.Frozen,
            transport.IsPlaying ? PlaybackPhase.Playing : PlaybackPhase.Paused,
            false, false, false, transport.SeekSerial, true, transport.MonotonicSeconds);
        return _source.Sample(snapshot, package.Tempo, package.Document.TimeDomain,
            package.Document.OffsetSeconds, package.Document.Seed);
    }
}
