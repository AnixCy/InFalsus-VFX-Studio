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
    internal bool AuthoringView;
    bool savedCameraEnabled,virtualPlaying;
    double virtualSeconds,virtualAnchor;
    internal double DurationSeconds=>Transport?.Clip?Transport!.Clip.length:Math.Max(180,virtualSeconds+30);
    internal void SetView(bool active)
    {
        if(active==AuthoringView)return;
        if(active&&Owner) {
            if(!(bool)typeof(StudioBootstrap).GetMethod("CommitAllInputFieldsNow",Flags)!.Invoke(Owner,null)!)
                throw new InvalidOperationException("请先完成或取消谱面字段输入。");
            typeof(StudioBootstrap).GetMethod("CancelGesture",Flags)!.Invoke(Owner,null);
            var sounds=typeof(StudioBootstrap).GetField("keySounds",Flags)?.GetValue(Owner);
            sounds?.GetType().GetMethod("StopAll",Flags)?.Invoke(sounds,null);
        }
        Transport?.Pause();virtualPlaying=false;
        if(Owner) {
            if(active){savedCameraEnabled=Owner!.ViewCamera.enabled;Owner.ViewCamera.enabled=false;}
            else Owner!.ViewCamera.enabled=savedCameraEnabled;
        }
        AuthoringView=active;
    }
    internal void SeekSeconds(double seconds)
    {
        seconds=Math.Max(0,seconds);virtualSeconds=seconds;virtualAnchor=Time.realtimeSinceStartupAsDouble;
        Transport?.Pause();Transport?.Seek(seconds*1000);virtualPlaying=false;
    }
    internal void TogglePlayback()
    {
        if(Transport?.Clip)Transport!.Toggle();
        else {virtualSeconds=VirtualTime;virtualAnchor=Time.realtimeSinceStartupAsDouble;virtualPlaying=!virtualPlaying;}
    }
    double VirtualTime=>virtualSeconds+(virtualPlaying?Time.realtimeSinceStartupAsDouble-virtualAnchor:0);
    internal void LoadAudio(string path)
    {
        if(!Owner)throw new InvalidOperationException("编辑器尚未就绪。");
        if(!File.Exists(path))throw new FileNotFoundException("音频文件不存在。",path);
        Owner!.StartCoroutine((System.Collections.IEnumerator)typeof(StudioBootstrap).GetMethod("LoadAudio",Flags)!.Invoke(Owner,new object[]{path})!);
    }
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
        double chart = Exporting ? (double)exportTime!.GetValue(null)! : AuthoringView
            ? transport?.Clip?transport!.TimeMs:VirtualTime*1000 : Owner ? Owner!.CurrentMs : 0;
        double offset = transport?.AudioOffsetMs ?? 0;
        bool playing = Exporting || (transport?.Playing ?? false) || AuthoringView&&virtualPlaying;
        return new PlaybackSnapshot(generation, "studio-preview", 0, "", (chart + offset) * .001,
            chart * .001, 0, (float)(transport?.Rate ?? 1),
            playing ? TransportState.Running : TransportState.Frozen,
            playing ? PlaybackPhase.Playing : PlaybackPhase.Paused, !playing, false, false,
            seek, true, Time.realtimeSinceStartupAsDouble);
    }
    internal static string Open(string title, string ext) => (string)Dialogs.GetMethod("Open", Flags)!.Invoke(null, new object[] { title, "", ext })!;
    internal static string Folder(string title)=>(string)Dialogs.GetMethod("Folder",Flags)!.Invoke(null,new object[]{title,""})!;
    internal static string Save(string name) => (string)Dialogs.GetMethod("Save", Flags)!.Invoke(null, new object[] { "保存演出工程", "", name, "json" })!;
}
