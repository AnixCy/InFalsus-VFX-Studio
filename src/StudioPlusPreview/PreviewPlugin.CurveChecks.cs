using InFalsus.Presentation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using NVector4=System.Numerics.Vector4;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckCurveEditing(Action<bool,string> check,string output)
    {
        var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());string id="adjust/_Exposure";
        string first=model.AddKey(id,0),last=model.AddKey(id,2);model.UpdateAnimatedParameter(id,new ParameterValue(new NVector4(2,0,0,0)),2,last,true);model.Commit();
        float Sample(double t){model.Sample(t);return model.Values[model.Catalog.Parameters.ToList().FindIndex(p=>p.Id==id)].Number.X;}
        float baseline=Sample(1);long revision=model.Session.Revision;var curve=new BezierCurve(new Vector2(.15f,.95f),new Vector2(.6f,1.2f));
        model.PreviewCurve(first,curve);check(model.Session.Revision==revision&&Sample(1)>baseline+.3f,"curve handles preview nonlinear interpolation before committing");
        model.Cancel();check(Sample(1)==baseline,"canceling a curve gesture restores the exact previous interpolation");
        model.PreviewCurve(first,new BezierCurve(new Vector2(.25f,.8f),new Vector2(.7f,1)));model.PreviewCurve(first,curve);model.Commit();
        check(model.Session.Revision==revision+1,"multiple handle updates become one undo step");float shaped=Sample(1);
        var handles=BezierCurve.From(model.Keys.Single(k=>k.Id==first));check((handles.A-curve.A).magnitude<.0001&&(handles.B-curve.B).magnitude<.0001,"saved portable curve samples recover both editable Bezier handles");
        model.Undo();check(Sample(1)==baseline,"one undo restores the whole curve gesture");model.Redo();check(Sample(1)==shaped,"redo restores shaped interpolation");
        string file=Path.Combine(output,"editable-curves.vfx.json");Store().Save(model.Session,file,model.Session.Revision);
        var restored=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());restored.Replace(Store().Open(file).Session);
        var saved=BezierCurve.From(restored.Keys.Single(k=>k.Id==first));check((saved.A-curve.A).magnitude<.0001&&(saved.B-curve.B).magnitude<.0001,"curve handles survive independent project save and reopen without a new runtime format");
        revision=model.Session.Revision;model.MoveCurveKey(last,3,0,3);check(model.Session.Revision==revision&&model.MovingSeconds==3&&model.EditedValue!.Value.Number.X==3,"key dragging previews both time and value");model.Commit();
        check(model.Session.Revision==revision+1&&model.Keys.Single(k=>k.Id==last).Seconds==3&&model.Keys.Single(k=>k.Id==last).Value.Number.X==3,"time and value dragging commits together");
        model.Undo();check(model.Keys.Single(k=>k.Id==last).Seconds==2&&model.Keys.Single(k=>k.Id==last).Value.Number.X==2,"key drag undo restores both axes");
        var previous=Model;var oldHeights=channelHeights.ToArray();float oldScroll=trackScroll;var oldBody=timelineBody;
        try {
            Model=model;trackScroll=0;timelineBody=new Rect(0,100,1000,400);var rows=TimelineRows();var target=rows.First(r=>r.Parameter==id);float top=target.Top;
            channelHeights[id]=260;var expanded=TimelineRows();var resized=expanded.Single(r=>r.Parameter==id);
            check(resized.Height==260*UiScale&&resized.Top==top&&expanded.Last().Top>=rows.Last().Top,"channel height changes flow through timeline row layout");
            check(expanded.All(r=>RowAt(expanded,timelineBody.y+r.Top+r.Height/2)==expanded.IndexOf(r)),"variable-height row hit testing matches every row center");
        }finally{Model=previous;channelHeights.Clear();foreach(var p in oldHeights)channelHeights[p.Key]=p.Value;trackScroll=oldScroll;timelineBody=oldBody;}
        var tracker=new SourceChanges();tracker.Reset("initial",true);check(!tracker.Ready(100),"unchanged shader sources do not trigger builds");tracker.Observe("edit-1",1);check(!tracker.Ready(1.4)&&tracker.Ready(2),"shader save bursts wait for the debounce interval");tracker.Attempt();check(!tracker.Ready(10),"failed or successful attempts do not rebuild unchanged sources forever");tracker.Observe("edit-2",11);check(tracker.Ready(12),"edits during an earlier build remain pending for the next build");
        var workspace=SourceWorkspace.Open(Path.Combine(output,"SourceFixture"));var descriptor=JObject.Parse(File.ReadAllText(Path.Combine(workspace.Root,workspace.Descriptor)));
        check((bool?)descriptor["AutoParameters"]==true&&descriptor["Effect"]!["Parameters"]!.Count()==0,"new shader effects require no hand-written parameter descriptor");
        string shader=File.ReadAllText(Path.Combine(workspace.Root,workspace.Shader));var markerPath=Path.Combine(workspace.Root,"workspace.vfxsource.json");
        var marker=JObject.Parse(File.ReadAllText(markerPath));marker["CompilerVersion"]=0;File.WriteAllText(markerPath,marker.ToString());descriptor.Remove("AutoParameters");File.WriteAllText(Path.Combine(workspace.Root,workspace.Descriptor),descriptor.ToString());
        var migrated=SourceWorkspace.Open(workspace.Root);migrated.UpgradeCompiler(CustomTools);
        check(migrated.CompilerVersion==2&&Directory.GetFiles(Path.Combine(workspace.Root,".vfx/tool-backups"),"effect.json",SearchOption.AllDirectories).Length==1
            &&File.ReadAllText(Path.Combine(workspace.Root,workspace.Shader))==shader,"old source workspaces upgrade compiler tools with backups and unchanged shader code");
    }
}
