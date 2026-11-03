using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using Newtonsoft.Json;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    void CheckTimelineAndEffects(Action<bool,string> check,string output,Dictionary<string,string> hashes)
    {
        var timeline=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        string id="background/_Brightness";
        int Index(string parameter)=>timeline.Catalog.Parameters.ToList().FindIndex(p=>p.Id==parameter);
        float Sample(double time){timeline.Sample(time);return timeline.Values[Index(id)].Number.X;}
        var first=timeline.AddKey(id,0);
        timeline.UpdateAnimatedParameter(id,new ParameterValue(new NVector4(.5f,0,0,0)),0,first,true);timeline.Commit();
        var second=timeline.AddKey(id,2);
        timeline.UpdateAnimatedParameter(id,new ParameterValue(new NVector4(1.5f,0,0,0)),2,second,true);timeline.Commit();
        check(Math.Abs(Sample(1)-1)<.0001,"timeline interpolates a real keyed parameter");
        timeline.SetEase(first,Easing.Hold);check(Math.Abs(Sample(1)-.5)<.0001,"hold interpolation applies between keyframes");
        timeline.Undo();check(Math.Abs(Sample(1)-1)<.0001,"undo restores interpolation mode");
        timeline.MoveKey(second,4);check(Math.Abs(Sample(2)-1)<.0001,"moving a key previews the new time before commit");
        timeline.Cancel();check(timeline.Keys.Single(k=>k.Id==second).Seconds==2&&Math.Abs(Sample(1)-1)<.0001,"cancel key drag restores time and samples");
        long revision=timeline.Session.Revision;
        timeline.MoveKey(second,3);timeline.MoveKey(second,4);timeline.MoveKey(second,5);timeline.Commit();
        check(timeline.Session.Revision==revision+1&&timeline.Keys.Single(k=>k.Id==second).Seconds==5,"one complete key drag publishes one revision");
        timeline.Undo();check(timeline.Keys.Single(k=>k.Id==second).Seconds==2,"one undo restores the entire key drag");
        bool duplicate=false;try{timeline.MoveKey(second,0);}catch(AuthoringEditException){duplicate=true;}finally{timeline.Cancel();}
        check(duplicate&&timeline.Keys.Length==2,"duplicate key time is rejected without changing the artwork");
        check(timeline.AddKey(id,2)==second,"adding at an existing key selects it without duplication");
        var file=Path.Combine(output,"timeline.vfx.json");Store().Save(timeline.Session,file,timeline.Session.Revision);
        var opened=Store().Open(file);var restored=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        restored.Replace(opened.Session);restored.Sample(1);
        check(restored.Keys.Select(k=>k.Id).SequenceEqual(timeline.Keys.Select(k=>k.Id))&&Math.Abs(restored.Values[Index(id)].Number.X-1)<.0001,
            "animated project saves and reopens with stable key identities and exact samples");
        timeline.DeleteKey(second);check(timeline.Keys.Length==1,"selected key is deleted");timeline.Undo();
        check(timeline.Keys.Any(k=>k.Id==second),"undo recovers the deleted key identity");
        string colorId="background/_Tint";
        string colorKey=timeline.AddKey(colorId,0);
        timeline.UpdateAnimatedParameter(colorId,new ParameterValue(new NVector4(1,.2f,.3f,.4f)),0,colorKey,true);timeline.Commit();
        string colorEnd=timeline.AddKey(colorId,2);
        timeline.UpdateAnimatedParameter(colorId,new ParameterValue(new NVector4(.5f,.8f,.9f,.7f)),2,colorEnd,true);timeline.Commit();
        var priorModel=Model;string priorKey=selectedKey;double priorTime=CurrentSeconds;
        try {
            Model=timeline;selectedKey=colorKey;CurrentSeconds=1;Bridge.SetView(true);
            Change(colorId,0,1.7f);Change(colorId,1,.6f);timeline.Commit();
            var changed=timeline.Keys.Single(k=>k.Id==colorKey).Value.Number;
            check(Math.Abs(changed.X-1.7)<.0001&&Math.Abs(changed.Y-.6)<.0001&&Math.Abs(changed.Z-.3)<.0001&&Math.Abs(changed.W-.4)<.0001,
                "editing selected key components preserves its other values away from the playhead");
        }finally{Model=priorModel;selectedKey=priorKey;CurrentSeconds=priorTime;Bridge.SetView(false);}

        var legacy=timeline.Session.Snapshot();legacy.Runtime.Nodes=legacy.Runtime.Nodes.Where(n=>n.Id=="background").ToArray();
        legacy.Runtime.Channels=legacy.Runtime.Channels.Where(c=>c.Node=="background").ToArray();
        legacy.Tracks=legacy.Tracks.Where(t=>t.ParameterId.StartsWith("background/",StringComparison.Ordinal)).ToArray();
        legacy.ParameterHints=legacy.ParameterHints.Where(p=>p.Node=="background").ToArray();
        var legacyModel=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        legacyModel.Replace(new AuthoringSession(legacy,folder,PreviewModel.Capabilities,isSaved:true));
        check(legacyModel.Package.Document.Nodes.Length==1&&legacyModel.Keys.Length==4,"earlier single-background project schema remains editable");

        bool camera=Bridge.Owner!.ViewCamera.enabled;var history=Bridge.Owner.History;
        Bridge.SetView(true);
        check(!Bridge.Owner.ViewCamera.enabled&&!Hooks.ChartTick(),"authoring view disables the chart camera and chart/native update loops");
        check(!Hooks.GuiBefore(Bridge.Owner,out _)&&!Hooks.Scene(new Event{type=EventType.MouseDown})&&!Hooks.Keys(new Event{type=EventType.KeyDown,keyCode=KeyCode.A}),
            "authoring view suppresses the chart GUI and note edit input");
        Bridge.SeekSeconds(1.234);check(Math.Abs(Bridge.Snapshot().ChartSeconds-1.234)<.00001,"authoring playhead seeks precisely without a chart");
        Bridge.TogglePlayback();check(Bridge.Snapshot().Transport==TransportState.Running&&!Bridge.Transport!.Playing,"silent timeline playback does not start audio transport");
        Bridge.SeekSeconds(0);Bridge.SetView(false);
        check(Bridge.Owner.ViewCamera.enabled==camera&&Hooks.ChartTick()&&ReferenceEquals(history,Bridge.Owner.History),"returning to chart restores the camera and preserves chart history");

        var neutral=Surface!.Render(timeline,Bridge.Snapshot(),96,64);string normal=HashTarget(neutral);
        timeline.UpdateParameter("adjust/_Fade",new ParameterValue(new NVector4(1,0,0,0)));timeline.Commit();
        var faded=Surface.Render(timeline,Bridge.Snapshot(),96,64);
        check(BlackTarget(faded),"utility fade renders to black on the actual GPU");
        timeline.UpdateParameter("adjust/_Fade",new ParameterValue(new NVector4(0,0,0,0)));timeline.Commit();
        check(HashTarget(Surface.Render(timeline,Bridge.Snapshot(),96,64))==normal,"reset utility fade exactly restores the isolated preview");
        var originals=timeline.Library.Effects.Where(e=>e.Original).ToArray();
        string commonKey=timeline.AddKey("adjust/_Exposure",.25);
        check(originals.Length==24,"local library contains twelve original background/postprocess pairs");
        foreach(var background in originals.Where(e=>e.Target=="background")) {
            timeline.SelectEffect(background,"background");
            var post=originals.Single(e=>e.Id==background.Id.Replace("-background","-postprocess"));
            timeline.SelectEffect(post,"postprocess");
            var target=Surface.Render(timeline,Bridge.Snapshot(),96,64);
            check(Surface.Material.shader.isSupported&&Surface.ResourceCount==3,"original material pair loads and renders: "+background.Id);
            hashes[background.Id]=HashTarget(target);
            check(!MagentaTarget(target),"original material pair avoids the error shader: "+background.Id);
        }
        check(timeline.Keys.Any(k=>k.Id==commonKey&&k.ParameterId=="adjust/_Exposure"),"shared adjustment key identities survive background and postprocess changes");
        var feedback=originals.First(e=>e.Target=="background"&&e.Parameters.Any(p=>p.Property=="_FeedbackAmount"));
        timeline.SelectEffect(feedback,"background");timeline.SelectEffect(null,"postprocess");
        string baseHash=HashTarget(Surface.Render(timeline,Bridge.Snapshot(),96,64));
        timeline.UpdateParameter("background/_FeedbackAmount",new ParameterValue(new NVector4(.4f,0,0,0)));timeline.Commit();
        check(HashTarget(Surface.Render(timeline,Bridge.Snapshot(),96,64))!=baseHash,"an original keyword-controlled feedback shader responds to its parameter");
        timeline.UpdateParameter("background/_KW_USE_FEEDBACK",new ParameterValue(new NVector4(-1,0,0,0)));timeline.Commit();
        check(HashTarget(Surface.Render(timeline,Bridge.Snapshot(),96,64))==baseHash,"explicit keyword off overrides a nonzero effect strength");
        string toggle=timeline.AddKey("background/_KW_USE_FEEDBACK",0);
        check(timeline.Keys.Single(k=>k.Id==toggle).Ease==Easing.Hold,"keyword keys default to hold interpolation");
        var previousModel=Model;var previousOpen=openDialog;string previousImage=imagePath;
        try {
            Model=timeline;openDialog=(_,__)=>Path.Combine(output,"image-check.png");ChooseImage();
            check(Surface.Material.GetTexture("_BaseMap").width==2&&!timeline.IsEditing,
                "custom image loading supports an original shader without built-in pattern parameters");
        }finally{Model=previousModel;openDialog=previousOpen;imagePath=previousImage;Surface.ApplyImage(null);}
        timeline.SelectEffect(EffectLibrary.Background,"background");
        timeline.Undo();check(timeline.Package.Document.Nodes[0].Material==feedback.Material,"material graph changes are undoable");
        timeline.Redo();check(timeline.Package.Document.Nodes[0].Material==EffectLibrary.Background.Material,"material graph changes support redo");
        var moving=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        moving.UpdateParameter("background/_Speed",new ParameterValue(new NVector4(0,0,0,0)));moving.Commit();
        moving.UpdateParameter("adjust/_Ripple",new ParameterValue(new NVector4(.04f,0,0,0)));moving.Commit();
        var start=Bridge.Snapshot() with{ChartSeconds=0,AudioSeconds=0};
        string wave=HashTarget(Surface.Render(moving,start,96,64));
        check(HashTarget(Surface.Render(moving,start with{ChartSeconds=2,AudioSeconds=2},96,64))!=wave,
            "time-driven utility ripple uses the seekable authoring clock");
        Surface.Draw(Model!,Bridge.Snapshot(),false);
    }
    static byte[] TargetBytes(RenderTexture target)
    {
        var old=RenderTexture.active;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);return pixels.GetRawTextureData();}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static string HashTarget(RenderTexture target){using var hash=System.Security.Cryptography.SHA256.Create();return BitConverter.ToString(hash.ComputeHash(TargetBytes(target))).Replace("-","");}
    static bool BlackTarget(RenderTexture target){var bytes=TargetBytes(target);for(int i=0;i<bytes.Length;i+=4)if(bytes[i]>2||bytes[i+1]>2||bytes[i+2]>2)return false;return true;}
    static bool MagentaTarget(RenderTexture target){var b=TargetBytes(target);int count=0;for(int i=0;i<b.Length;i+=4)if(b[i]>250&&b[i+1]<3&&b[i+2]>250)count++;return count>b.Length/4*.95;}
}
