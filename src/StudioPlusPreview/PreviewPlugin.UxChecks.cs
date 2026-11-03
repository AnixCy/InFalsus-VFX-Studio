using InFalsus.Presentation;
using Newtonsoft.Json;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckAuthoringWorkflow(Action<bool,string> check,string output)
    {
        bool geometry=true;
        foreach(var size in new[]{new Vector2(960,540),new Vector2(1280,720),new Vector2(1920,1080),new Vector2(2560,1440)})
        foreach(float preferred in new[]{.8f,1f,1.25f,1.6f}) {
            var l=WorkspaceLayout.Calculate(size.x,size.y,WorkspaceLayout.ScaleFor(size.x,size.y,preferred));
            geometry&=new[]{l.Library,l.Stage,l.Inspector,l.Timeline}.All(r=>r.width>0&&r.height>0&&r.xMin>=0&&r.yMin>=0&&r.xMax<=size.x+.1&&r.yMax<=size.y+.1);
            geometry&=l.Library.xMax<=l.Stage.xMin+.1f&&l.Stage.xMax<=l.Inspector.xMin+.1f&&l.Inspector.yMax<=l.Timeline.yMin+.1f;
            geometry&=l.Stage.width>=240&&l.Stage.height>=110;
        }
        check(geometry,"workspace panels remain bounded and separate across four window sizes and four UI scales");
        var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        var curated=model.Library.Effects.Where(e=>e.Id.StartsWith("effect-",StringComparison.Ordinal)).ToArray();
        check(curated.Length==11&&curated.All(e=>e.Category=="效果"&&e.Parameters.Any(p=>!p.Hidden&&p.Label=="Strength")),"eleven focused effects expose named strengths independently of presets");
        check(model.Library.Effects.Count(e=>e.Category=="预设")==24&&model.Library.Effects.All(e=>!e.Label.Contains("官谱")),"preset catalog has descriptive labels and a separate background target");
        var snapshot=Bridge.Snapshot() with{ChartSeconds=2.75,AudioSeconds=2.75};
        string neutral=HashTarget(Surface!.Render(model,snapshot,128,96));
        foreach(var effect in curated) {
            var test=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            string id=test.AddEffect(test.Library.Effects.Single(e=>e.Id==effect.Id),0);
            string rendered=HashTarget(Surface.Render(test,snapshot,128,96));
            check(rendered!=neutral,effect.Label+" independently changes real GPU output");
            test.UpdateParameter(id+"/_VfxOpacity",new ParameterValue(new NVector4(0,0,0,0)));test.Commit();
            check(HashTarget(Surface.Render(test,snapshot,128,96))==neutral,effect.Label+" bypass restores the exact base image");
        }
        string template=Path.Combine(output,"SourceFixture");var workspace=SourceWorkspace.Create(template,CustomTools);
        check(SourceWorkspace.Open(template).Root==workspace.Root&&File.Exists(Path.Combine(template,"Effect.shader"))&&File.Exists(Path.Combine(template,".vscode/tasks.json")),"new editable source workspace includes shader descriptor and VS Code build task");
        bool duplicate=false;try{SourceWorkspace.Create(template,CustomTools);}catch(IOException){duplicate=true;}
        check(duplicate,"creating a template never overwrites an existing source workspace");
        var tasks=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(template,".vscode/tasks.json")));
        check((string?)tasks["tasks"]?[0]?["type"]=="process"&&File.ReadAllText(Path.Combine(template,".vfx/Build.ps1")).Contains("-ReuseProject"),"external editor builds use process arguments and a reusable owned Unity cache");
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-source-fixture");
        if(at>=0)CheckSourceReload(check,SourceWorkspace.Open(args[at+1]),snapshot);
        Surface.Render(Model!,Bridge.Snapshot(),128,96);
    }
    void CheckSourceReload(Action<bool,string> check,SourceWorkspace workspace,PlaybackSnapshot snapshot)
    {
        var builds=Directory.GetDirectories(Path.Combine(workspace.Root,".vfx/build")).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        check(builds.Length>=2,"source fixture contains two compiled revisions");
        var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        var first=model.Library.Import(Path.Combine(builds[0],"effect.json"),folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        model.Library.LinkSource(first,workspace.Root,folder);string a=model.AddEffect(first,1),b=model.AddEffect(first,1);
        string key=model.UpdateAnimatedParameter(a+"/_Intensity",new ParameterValue(new NVector4(.8f,0,0,0)),3,null,true);model.Commit();
        string before=HashTarget(Surface!.Render(model,snapshot,128,96));
        var original=model.Document.Runtime.Nodes.Select(n=>(n.Id,n.StartSeconds,n.EndSeconds)).ToArray();
        Surface.ReleaseSource(workspace.Root);
        var second=model.Library.Import(workspace.LatestDescriptor()!,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        model.Library.LinkSource(second,workspace.Root,folder);long revision=model.Session.Revision;
        var previousModel=Model;var previousSource=sourceWorkspace;bool previousAuto=autoSourceUpdate;
        string previousFingerprint=sourceFingerprint,previousStatus=sourceStatusFingerprint;double previousPoll=sourcePoll;
        try {
            Model=model;sourceWorkspace=workspace;autoSourceUpdate=true;sourceFingerprint=sourceStatusFingerprint="";sourcePoll=0;
            TickSourceWorkspace();
        }finally{Model=previousModel;sourceWorkspace=previousSource;autoSourceUpdate=previousAuto;sourceFingerprint=previousFingerprint;sourceStatusFingerprint=previousStatus;sourcePoll=previousPoll;}
        check(model.Session.Revision==revision+1&&model.Document.Runtime.Nodes.Count(n=>n.EffectId==second.Id)==2,"completed-build watcher updates all linked instances as one undo transaction");
        check(model.Document.Runtime.Nodes.Select(n=>(n.Id,n.StartSeconds,n.EndSeconds)).SequenceEqual(original)&&model.Keys.Any(k=>k.Id==key&&k.ParameterId==a+"/_Intensity"&&k.Seconds==3),"shader reload preserves instance identities ranges and animated parameter keys");
        string after=HashTarget(Surface.Render(model,snapshot,128,96));check(before!=after,"updated shader code replaces GPU output without restarting the editor");
        model.Undo();check(HashTarget(Surface.Render(model,snapshot,128,96))==before,"undo of source reload restores the exact previous shader and animation");
        model.Redo();check(HashTarget(Surface.Render(model,snapshot,128,96))==after,"redo restores the new compiled shader revision");
        check(EffectLibrary.Load(folder).Effects.Count(e=>e.SourceDirectory==workspace.Root)>=2,"source links survive cache reload for both shader revisions");
    }
}
