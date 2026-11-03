using Newtonsoft.Json;
using UnityEngine;
using InFalsus.Presentation;
using NVector4=System.Numerics.Vector4;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    string sourceProbeRoot="",sourceProbeOutput="",sourceProbeEffect="",sourceProbeKey="";
    int sourceProbeUpdates;
    bool sourceProbeBound;
    double sourceProbeStarted,sourceProbeLastWrite;
    void InitSourceProbe()
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-source-probe");if(at<0)return;
        sourceProbeRoot=Path.GetFullPath(args[at+1]);at=Array.IndexOf(args,"-vfx-source-probe-output");
        if(at<0)throw new ArgumentException("Source probe output is required.");sourceProbeOutput=Path.GetFullPath(args[at+1]);
        if(!File.Exists(Path.Combine(sourceProbeRoot,".vfx/allow-automation-test")))throw new InvalidDataException("Source probe accepts only a disposable marked fixture.");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceProbeOutput)!);AudioListener.volume=0;sourceProbeStarted=Time.realtimeSinceStartupAsDouble;
    }
    void TickSourceProbe()
    {
        if(sourceProbeRoot.Length==0||Model==null||Surface==null||StudioUiTheme.TryBind()==null)return;
        if(File.Exists(Path.Combine(sourceProbeRoot,".vfx/stop-probe"))||Time.realtimeSinceStartupAsDouble-sourceProbeStarted>180) {
            sourceProbeRoot="";quitPermit=true;quitPermitExpiresAfter=Time.frameCount+1;Application.Quit();return;
        }
        if(!sourceProbeBound) {
            sourceProbeBound=true;
            Bridge.SetView(true);var workspace=SourceWorkspace.Open(sourceProbeRoot);UseSource(workspace);autoSourceUpdate=true;sourceChanges.Reset(SourceChanges.Fingerprint(workspace),false);
            attachSourceSession=Model.Session;attachSourceRoot=sourceProbeRoot;
        }
        var node=Model.Document.Runtime.Nodes.FirstOrDefault(n=>Model.Definition(n).SourceDirectory==sourceProbeRoot);
        if(node!=null&&node.EffectId!=sourceProbeEffect) {
            sourceProbeEffect=node.EffectId;sourceProbeUpdates++;
            if(sourceProbeUpdates==1){sourceProbeKey=Model.UpdateAnimatedParameter(node.Id+"/_Intensity",new ParameterValue(new NVector4(.8f,0,0,0)),1,null,true);Model.Commit();}
        }
        if(Time.realtimeSinceStartupAsDouble-sourceProbeLastWrite<.3)return;sourceProbeLastWrite=Time.realtimeSinceStartupAsDouble;
        string gpu=node==null?"":HashTarget(Surface.Render(Model,Bridge.Snapshot() with{ChartSeconds=1,AudioSeconds=1},128,96));
        File.WriteAllText(sourceProbeOutput,JsonConvert.SerializeObject(new{updates=sourceProbeUpdates,phase=sourcePhase,message=sourceMessage,
            compiling=sourceBuild!=null,effectId=node?.EffectId,parameters=node==null?Array.Empty<string>():Model.Definition(node).Parameters.Select(p=>p.Property).ToArray(),
            keyPreserved=sourceProbeKey.Length>0&&Model.Keys.Any(k=>k.Id==sourceProbeKey&&k.Seconds==1),gpu,
            revision=Model.Session.Revision,audioPlayed=false,gameStarted=false},Formatting.Indented));
    }
}
