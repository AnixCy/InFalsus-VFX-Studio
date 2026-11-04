using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckSceneOpacity(Action<bool,string> check,string output)
    {
        var saved=Model;
        try {
            Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            var ids=SceneOpacityPlan.EffectIds.Select(id=>Model.AddEffect(Model.Library.Effects.Single(e=>e.Id==id),0)).ToArray();
            check(ids.Length==5&&Model.Document.Runtime.Requires.Contains(SceneOpacityPlan.Capability),"five scene-opacity controls declare protected runtime semantics");
            using(var edit=Model.Session.BeginEdit(Model.Session.Revision,"scene opacity fixture")) {
                for(int i=0;i<ids.Length;i++) {
                    string parameter=ids[i]+"/"+SceneOpacityPlan.Properties[i];
                    edit.AddKey(parameter,KeyPosition.Seconds(0),new ParameterValue(new NVector4(1,0,0,0)),Easing.Linear);
                    edit.AddKey(parameter,KeyPosition.Seconds(2),new ParameterValue(NVector4.Zero),Easing.Linear);
                }
                edit.Commit();
            }
            Model.Refresh();Model.Sample(1);var values=ScenePreviewValues(1);
            check(values==new SceneOpacityValues(.5f,.5f,.5f,.5f,.5f),"all five scene channels interpolate continuously at half opacity");
            Model.Sample(2);check(ScenePreviewValues(2)==new SceneOpacityValues(0,0,0,0,0),"scene controls reach fully hidden endpoints");
            Model.Sample(1);check(ScenePreviewValues(1)==values,"scene opacity seek is deterministic without accumulated fading");
            Model.Sample(5);check(ScenePreviewValues(5)==SceneOpacityValues.Visible,"outside instance ranges native scene visibility is restored");
            check(SceneOpacityPlan.Properties.All(p=>Model.TrackVisible(ids[Array.IndexOf(SceneOpacityPlan.Properties,p)]+"/"+p)),"single opacity channels are immediately visible on their timeline");
            int keys=Model.Keys.Length;Model.Undo();check(Model.Keys.Length==keys-10,"scene keyframe changes undo as one transaction");Model.Redo();
            check(Model.Keys.Length==keys,"scene keyframes redo with stable authoring behavior");
            var before=Model.Session.Snapshot();
            var store=new ProjectFileStore(p=>JsonConvert.SerializeObject(p,Json),t=>JsonConvert.DeserializeObject<AuthoringProjectDocument>(t,Json)!,PreviewModel.Capabilities);
            string path=Path.Combine(output,"scene-opacity.vfx.json");store.Save(Model.Session,path,Model.Session.Revision);
            var reopened=store.Open(path);var restored=reopened.Session.CompileSnapshot(false);
            var sample=new ParameterValue[restored.Timeline.Count];restored.Timeline.Sample(1,sample);
            check(new SceneOpacityPlan(restored.Document).Sample(1,sample)==values,"scene-opacity save/reopen preserves sampled values");
            check(reopened.Session.Snapshot().Tracks.SelectMany(t=>t.KeyIds).SequenceEqual(before.Tracks.SelectMany(t=>t.KeyIds)),"scene keys retain identities across saving");
            var exported=BackgroundExport.Compile(Model.Session.Snapshot(),new AddonTarget{SongKey="game:scene-opacity-fixture",Difficulty=0},"Gamma");
            check(exported.Nodes.Count(SceneOpacityPlan.IsControl)==5&&exported.Requires.Contains(SceneOpacityPlan.Capability),"addon exporter preserves scene channels and their capability");
            check(exported.Nodes.Where(SceneOpacityPlan.IsControl).All(n=>n.Bundle=="background.bundle"),"logical scene controls reuse the game bundle without adding Shader assets");
            var snap=Bridge.Snapshot() with{AudioSeconds=1,ChartSeconds=1};
            Surface!.Render(Model,snap,320,180);
            check(Surface.LastPostProcessDraws==1,"scene controls allocate no additional fullscreen postprocess draws");
            var noCapability=JsonConvert.DeserializeObject<PackageDocument>(JsonConvert.SerializeObject(exported,Json),Json)!;
            noCapability.Requires=noCapability.Requires.Where(c=>c!=SceneOpacityPlan.Capability).ToArray();
            bool blocked=false;try{_ =new SceneOpacityPlan(noCapability);}catch(InvalidDataException){blocked=true;}
            check(blocked,"scene semantics cannot be silently interpreted by an older runtime");
            var control=exported.Nodes.First(SceneOpacityPlan.IsControl);control.Enabled=false;
            var plan=new SceneOpacityPlan(exported);var timeline=EffectPackage.FromDocument(exported,folder,AddonPackage.Capabilities,false);
            sample=new ParameterValue[timeline.Timeline.Count];timeline.Timeline.Sample(1,sample);
            check(plan.Sample(1,sample).Track==1,"disabled track control leaves track visible");
        }finally{Model=saved;sceneDocument=null;scenePreviewPlan=null;}
    }
}
