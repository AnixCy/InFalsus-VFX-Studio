using System.Reflection;
using InFalsus.Presentation;
using InFalsusStudio;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

internal sealed class StudioBridge
{
    internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly FieldInfo TransportField = typeof(StudioBootstrap).GetField("transport", Flags)!;
    static readonly Type Dialogs = typeof(StudioBootstrap).Assembly.GetType("InFalsusStudio.StudioFileDialogs")!;
    readonly Type? export = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "InfStudioVisualUI")
        ?.GetType("InFalsusStudio.VideoRender.RenderClock");
    readonly FieldInfo? exportActive, exportTime;
    StudioTransport? lastTransport;
    object? lastClip;
    long generation = 1;
    ulong seek;
    internal StudioBootstrap? Owner;
    internal bool Exporting => exportActive != null && (bool)exportActive.GetValue(null)!;
    internal StudioTransport? Transport => Owner ? (StudioTransport?)TransportField.GetValue(Owner) : null;
    internal StudioBridge() { exportActive = export?.GetField("Active", Flags); exportTime = export?.GetField("NowMs", Flags); }
    internal void Seeked(StudioTransport transport) { if (ReferenceEquals(Transport, transport)) seek++; }
    internal PlaybackSnapshot Snapshot()
    {
        var transport = Transport;
        if (!ReferenceEquals(lastTransport, transport) || !ReferenceEquals(lastClip, transport?.Clip)) {
            lastTransport = transport; lastClip = transport?.Clip; generation++; seek++;
        }
        // CurrentMs is already adapted by Studio+ for ordinary preview and gameplay.
        double chart = Exporting ? (double)exportTime!.GetValue(null)! : Owner ? Owner!.CurrentMs : 0;
        double offset = transport?.AudioOffsetMs ?? 0;
        bool playing = Exporting || (transport?.Playing ?? false);
        return new PlaybackSnapshot(generation, "studio-preview", 0, "", (chart + offset) * .001,
            chart * .001, 0, (float)(transport?.Rate ?? 1),
            playing ? TransportState.Running : TransportState.Frozen,
            playing ? PlaybackPhase.Playing : PlaybackPhase.Paused, !playing, false, false,
            seek, true, Time.realtimeSinceStartupAsDouble);
    }
    internal static string Open(string title, string ext) => (string)Dialogs.GetMethod("Open", Flags)!.Invoke(null, new object[] { title, "", ext })!;
    internal static string Save(string name) => (string)Dialogs.GetMethod("Save", Flags)!.Invoke(null, new object[] { "保存演出工程", "", name, "json" })!;
    internal static bool Confirm(string text) => (bool)Dialogs.GetMethod("Confirm", Flags)!.Invoke(null,
        new object[] { "演出工程", text, "继续", "取消" })!;
}
