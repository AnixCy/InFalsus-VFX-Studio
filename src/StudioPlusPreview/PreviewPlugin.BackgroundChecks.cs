using InFalsus.Presentation;
using UnityEngine;
using NVector4=System.Numerics.Vector4;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckBackgroundControls(Action<bool,string> check,string output)
    {
        var surface=Surface!;var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        var snapshot=Bridge.Snapshot() with{ChartSeconds=2.75,AudioSeconds=2.75};
        var image=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color((x%17)/17f,(y%13)/13f,((x/8+y/8)%2)*.7f+.1f,1);
        image.SetPixels(pixels);image.Apply();surface.ApplyImage(image);
        void Set(PreviewModel m,string id,float v){m.UpdateParameter(id,new ParameterValue(new NVector4(v,0,0,0)));m.Commit();}
        try {
            Set(model,"background/_PatternMix",0);Set(model,"background/_Distortion",0);
            string baseline=HashTarget(surface.Render(model,snapshot,128,96));int passes=surface.LastPostProcessDraws;
            var flow=model.Library.Effects.Single(e=>e.Id=="bg-control-ephemeralamount");var feedback=model.Library.Effects.Single(e=>e.Id=="bg-control-feedbackamount");
            check(flow.Target=="postprocess"&&feedback.Target=="postprocess"&&flow.BackgroundSource==feedback.BackgroundSource,"Ephemeral and Feedback are independently discoverable effect-library entries");
            string a=model.AddEffect(flow,0),b=model.AddEffect(feedback,0);Set(model,b+"/_FeedbackAmount",.6f);
            string combined=HashTarget(surface.Render(model,snapshot,128,96));
            check(combined!=baseline&&surface.BackgroundControlPasses==1&&surface.ResourceCount==2&&surface.LastPostProcessDraws==passes,"two background controls combine in one original background draw without additional postprocess passes or materials");
            var original=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());original.SetBackground(original.Library.Effects.Single(e=>e.Id==flow.BackgroundSource));
            Set(original,"background/_EphemeralAmount",.5f);Set(original,"background/_FeedbackAmount",.6f);Set(original,"background/_FeedbackRotation",.2f);
            check(HashTarget(surface.Render(original,snapshot,128,96))==combined,"decoupled Ephemeral and Feedback exactly match the original material GPU output at identical values");
            model.EnableEffect(a,false);string onlyFeedback=HashTarget(surface.Render(model,snapshot,128,96));
            check(onlyFeedback!=combined,"disabling one background control retains the other effect");model.EnableEffect(a,true);
            Set(model,b+"/_FeedbackRotation",-.2f);check(HashTarget(surface.Render(model,snapshot,128,96))!=combined,"negative feedback rotation remains editable after decoupling");Set(model,b+"/_FeedbackRotation",.2f);
            Set(model,a+"/_VfxOpacity",0);Set(model,b+"/_VfxOpacity",0);
            check(HashTarget(surface.Render(model,snapshot,128,96))==baseline,"zero effect strengths restore the exact image-only baseline");
            check(HashTarget(surface.Render(model,snapshot with{ChartSeconds=7,AudioSeconds=7},128,96))==baseline,"background controls stop at their own timeline intervals");
            Set(model,a+"/_VfxOpacity",1);Set(model,b+"/_VfxOpacity",1);
            string key=model.UpdateAnimatedParameter(b+"/_FeedbackRotation",new ParameterValue(new NVector4(-.4f,0,0,0)),4,null,true);model.Commit();
            var file=Path.Combine(output,"independent-background-effects.vfx.json");Store().Save(model.Session,file,model.Session.Revision);
            var reopened=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());reopened.Replace(Store().Open(file).Session);
            check(reopened.Keys.Any(k=>k.Id==key)&&HashTarget(surface.Render(reopened,snapshot,128,96))==HashTarget(surface.Render(model,snapshot,128,96)),"independent background instances and animation survive save and reopen");
            bool rejected=false;try{EffectPackage.FromDocument(model.Document.Runtime,folder,PreviewModel.Capabilities.Where(c=>c!=EffectLibrary.BackgroundCapability).ToArray(),false);}catch(Exception){rejected=true;}
            check(rejected,"old renderers reject unsupported background-control semantics instead of interpreting them as blit passes");
            var other=model.Library.Effects.First(e=>e.Id.StartsWith("bg-control-",StringComparison.Ordinal)&&e.BackgroundSource!=flow.BackgroundSource);
            string different=model.AddEffect(other,0);surface.Render(model,snapshot,128,96);
            check(surface.BackgroundGroupConflicts>0&&surface.ActiveBackgroundGroup==other.BackgroundSource,"overlapping incompatible original shader groups follow explicit stack precedence and report the conflict");model.EnableEffect(different,false);
            var previous=Model;var oldBody=timelineBody;float oldScroll=trackScroll;
            try {
                Model=model;timelineBody=new Rect(0,100,1000,500);trackScroll=0;var rows=TimelineRows();var target=rows.First(r=>r.Parameter!=null);float edge=timelineBody.y+target.Top+target.Height;
                check(ResizeRow(rows,new Vector2(80,edge-8*UiScale))?.Parameter==target.Parameter&&ResizeRow(rows,new Vector2(80,edge+8*UiScale))?.Parameter==target.Parameter,
                    "channel resizing accepts a wide hit band on both sides of the separator");
                check(ResizeRow(rows,new Vector2(80,edge-14*UiScale))==null,"channel resize hit bands do not capture ordinary row content");
            }finally{Model=previous;timelineBody=oldBody;trackScroll=oldScroll;}
            foreach(var preset in model.Library.Effects.Where(e=>e.Id.StartsWith("bg-preset-",StringComparison.Ordinal))) {
                var sample=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());sample.AddEffect(sample.Library.Effects.Single(e=>e.Id==preset.Id),0);
                surface.Render(sample,snapshot,96,64);check(!MagentaTarget(surface.Render(sample,snapshot,96,64))&&surface.LastPostProcessDraws==1,"background preset remains a single background draw: "+preset.Id);
            }
            var responses=new List<object>();
            foreach(var effect in model.Library.Effects.Where(e=>e.Id.StartsWith("bg-control-",StringComparison.Ordinal))) {
                var raw=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());raw.SetBackground(raw.Library.Effects.Single(e=>e.Id==effect.BackgroundSource));
                string neutral=HashTarget(surface.Render(raw,snapshot,96,64));
                var sample=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());sample.AddEffect(sample.Library.Effects.Single(e=>e.Id==effect.Id),0);
                var rendered=surface.Render(sample,snapshot,96,64);responses.Add(new{effect.Id,effect.Label,changed=HashTarget(rendered)!=neutral});
                check(!MagentaTarget(rendered)&&surface.LastPostProcessDraws==1,"extracted background control uses a supported original shader: "+effect.Id);
            }
            File.WriteAllText(Path.Combine(output,"background-responses.json"),Newtonsoft.Json.JsonConvert.SerializeObject(responses,Newtonsoft.Json.Formatting.Indented));
        }finally{surface.ApplyImage(null);surface.Draw(Model!,Bridge.Snapshot(),false);}
    }
}
