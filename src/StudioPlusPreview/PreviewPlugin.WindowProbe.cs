using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    string? probeFile;
    bool probeReady;
    double probeStarted;
    double probeLastHeartbeat;
    readonly List<object> probeEvents=new();
    void InitWindowProbe()
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-window-probe");
        if(at<0)return;probeFile=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(Path.GetDirectoryName(probeFile)!);
        AudioListener.volume=0;probeStarted=Time.realtimeSinceStartupAsDouble;
    }
    void TickWindowProbe()
    {
        if(probeFile==null||Model==null||!Bridge.Owner)return;
        if(Time.realtimeSinceStartupAsDouble-probeLastHeartbeat>1){probeLastHeartbeat=Time.realtimeSinceStartupAsDouble;Probe("heartbeat",null,new{focus=Application.isFocused,view=Bridge.AuthoringView});}
        if(!probeReady&&Time.frameCount>120&&StudioUiTheme.TryBind()!=null) {
            Bridge.SetView(true);probeReady=true;
            Model.UpdateParameter("adjust/_Exposure",new InFalsus.Presentation.ParameterValue(new System.Numerics.Vector4(.4f,0,0,0)));Model.Commit();
            var info=Harmony.GetPatchInfo(typeof(InFalsusStudio.StudioBootstrap).GetMethod("OnGUI",StudioBridge.Flags)!);
            Probe("ready",null,new {prefixes=info.Prefixes.Select(p=>new{p.owner,p.priority,method=p.PatchMethod.DeclaringType!.FullName+"."+p.PatchMethod.Name}),
                postfixes=info.Postfixes.Select(p=>new{p.owner,p.priority,method=p.PatchMethod.DeclaringType!.FullName+"."+p.PatchMethod.Name})});
        }
        if(Time.realtimeSinceStartupAsDouble-probeStarted>180){probeFile=null;quitPermit=true;quitPermitExpiresAfter=Time.frameCount+1;Application.Quit();}
    }
    void Probe(string stage,Event? e=null,object? extra=null)
    {
        if(probeFile==null)return;
        if(probeEvents.Count>1000)return;
        probeEvents.Add(new{stage,frame=Time.frameCount,time=Time.realtimeSinceStartupAsDouble,
            type=e?.type.ToString(),raw=e?.rawType.ToString(),key=e?.keyCode.ToString(),extra,
            running=Bridge.Snapshot().Transport==InFalsus.Presentation.TransportState.Running,held=heldPreviewKey?.ToString(),seconds=Bridge.Snapshot().ChartSeconds,
            revision=Model?.Session.Revision,canUndo=Model?.Session.CanUndo,canRedo=Model?.Session.CanRedo});
        File.WriteAllText(probeFile,JsonConvert.SerializeObject(new{ready=probeReady,events=probeEvents},Formatting.Indented));
    }
}
