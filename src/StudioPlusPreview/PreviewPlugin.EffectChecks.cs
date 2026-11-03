using InFalsus.Presentation;
using Newtonsoft.Json;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckEffectInstances(Action<bool,string> check,string output)
    {
        var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        var previous=Model;string previousFilter=libraryFilter,previousNode=selectedEffect,previousParameter=selectedParameter,previousKey=selectedKey;
        try {
            Model=model;collapsedEffects.Clear();
            check(TimelineRows().Count(r=>r.Parameter!=null)==2,"new workspace exposes only base color and exposure tracks");
            var ripple=model.Library.Effects.Single(e=>e.Id=="ripple");
            string a=model.AddEffect(ripple,2),b=model.AddEffect(ripple,2);
            check(a!=b&&model.Document.Runtime.Nodes.Length==4,"adding the same effect twice creates independent stack instances");
            check(TimelineRows().Count(r=>r.Parameter!=null)==2,"new effects do not populate the timeline with unused parameter tracks");
            var rows=TimelineRows().Select(r=>r.Node+":"+r.Parameter).ToArray();libraryFilter="预设";
            check(TimelineRows().Select(r=>r.Node+":"+r.Parameter).SequenceEqual(rows),"library category selection leaves the unified timeline intact");
            var snapshot=Bridge.Snapshot() with{AudioSeconds=1,ChartSeconds=1};
            string before=HashTarget(Surface!.Render(model,snapshot,96,64));
            check(Surface.ResourceCount==2,"out-of-range effects are not loaded or rendered before their first use");
            snapshot=snapshot with{AudioSeconds=3,ChartSeconds=3};
            Surface.Render(model,snapshot,96,64);
            check(Surface.NodeMaterial(a).GetInstanceID()!=Surface.NodeMaterial(b).GetInstanceID(),"repeated effect instances own separate materials");
            var key=model.UpdateAnimatedParameter(a+"/_Ripple",new ParameterValue(new NVector4(.06f,0,0,0)),4,null,true);model.Commit();
            check(TimelineRows().Any(r=>r.Parameter==a+"/_Ripple")&&!TimelineRows().Any(r=>r.Parameter==b+"/_Ripple"),"only the explicitly animated instance parameter creates a track");
            var anchor=model.Keys.Single(k=>k.ParameterId==a+"/_Ripple"&&k.Id!=key);
            check(anchor.Seconds==2,"first animation anchor uses the effect interval start");
            check(model.AddKey(a+"/_Ripple",0)==anchor.Id,"key insertion outside a clip clamps to its interval without creating an out-of-range key");
            long revision=model.Session.Revision;
            model.PreviewEffectRange(a,3,8,true);model.PreviewEffectRange(a,4,9,true);model.Commit();
            check(model.Session.Revision==revision+1&&model.Keys.Single(k=>k.Id==key).Seconds==6&&model.Keys.Single(k=>k.Id==anchor.Id).Seconds==4,
                "effect dragging shifts all keys once while preserving identity and one undo step");
            model.Undo();check(model.Keys.Single(k=>k.Id==key).Seconds==4,"one undo restores the effect interval and animation positions");
            model.PreviewEffectRange(a,3,8,true);model.Cancel();check(model.Document.Runtime.Nodes.Single(n=>n.Id==a).StartSeconds==2,"canceling an effect drag restores the interval");
            model.ReorderEffect(b,-1);check(Array.FindIndex(model.Document.Runtime.Nodes,n=>n.Id==b)<Array.FindIndex(model.Document.Runtime.Nodes,n=>n.Id==a),"effect composition order can be changed without replacing instances");
            model.RenameEffect(a,"Named effect");model.EnableEffect(a,false);
            check(model.Document.Runtime.Nodes.Single(n=>n.Id==a).Name=="Named effect"&&!model.IsActive(model.Document.Runtime.Nodes.Single(n=>n.Id==a),3),"instance naming and disable state are editable");
            model.RemoveEffect(a);check(!model.Keys.Any(k=>k.Id==key)&&model.Document.Runtime.Nodes.Any(n=>n.Id==b),"deleting an effect removes only its own animation");
            model.Undo();check(model.Keys.Any(k=>k.Id==key),"undo restores a deleted effect and all key identities");
            model.EnableEffect(a,true);model.EnableEffect(b,false);
            string active=HashTarget(Surface.Render(model,snapshot,96,64));model.EnableEffect(a,false);
            check(HashTarget(Surface.Render(model,snapshot,96,64))!=active,"effect disable bypasses its GPU pass");
            var outside=snapshot with{AudioSeconds=1,ChartSeconds=1};
            check(HashTarget(Surface.Render(model,outside,96,64))==before,"effects preserve the exact image outside their time interval");
            var file=Path.Combine(output,"effect-stack.vfx.json");Store().Save(model.Session,file,model.Session.Revision);
            var reopened=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());reopened.Replace(Store().Open(file).Session);
            check(reopened.Document.Runtime.Nodes.Select(n=>(n.Id,n.Name,n.Enabled,n.StartSeconds,n.EndSeconds)).SequenceEqual(model.Document.Runtime.Nodes.Select(n=>(n.Id,n.Name,n.Enabled,n.StartSeconds,n.EndSeconds))),"effect stack identity order intervals and enable state survive save and reopen");
            var fade=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            var neutral=Surface.Render(fade,snapshot,96,64);byte[] neutralBytes=TargetBytes(neutral);
            string normal=HashTarget(neutral);string fadeId=fade.AddEffect(fade.Library.Effects.Single(e=>e.Id=="fade"),0);
            fade.UpdateParameter(fadeId+"/_Fade",new ParameterValue(new NVector4(1,0,0,0)));fade.Commit();
            check(BlackTarget(Surface.Render(fade,snapshot,96,64)),"a standalone fade effect renders independently in the stack");
            fade.UpdateParameter(fadeId+"/_VfxOpacity",new ParameterValue(new NVector4(0,0,0,0)));fade.Commit();
            check(HashTarget(Surface.Render(fade,snapshot,96,64))==normal,"zero mix strength bypasses the effect exactly");
            fade.UpdateParameter(fadeId+"/_VfxOpacity",new ParameterValue(new NVector4(.5f,0,0,0)));fade.Commit();
            check(!BlackTarget(Surface.Render(fade,snapshot,96,64))&&HashTarget(Surface.Render(fade,snapshot,96,64))!=normal,"fractional mix composites the effect with the original image on the GPU");
            byte[] mixed=TargetBytes(Surface.Render(fade,snapshot,96,64));bool halfway=true;
            for(int i=0;i<mixed.Length;i++)if(i%4!=3&&Math.Abs(mixed[i]-neutralBytes[i]*.5)>3)halfway=false;
            check(halfway,"half-opacity fade numerically matches half of each source color channel");
            var order=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            string ring=order.AddEffect(order.Library.Effects.Single(e=>e.Id=="ring"),0);
            string lastFade=order.AddEffect(order.Library.Effects.Single(e=>e.Id=="fade"),0);
            order.UpdateParameter(lastFade+"/_Fade",new ParameterValue(new NVector4(1,0,0,0)));order.Commit();
            bool covered=BlackTarget(Surface.Render(order,snapshot,96,64));order.ReorderEffect(ring,1);
            check(covered&&!BlackTarget(Surface.Render(order,snapshot,96,64)),"reordering noncommuting effects changes actual GPU composition order");
            Model=order;AddLibraryEffect(order.Library.Effects.Single(e=>e.Id=="noise"),12);
            check(Math.Abs(Bridge.Snapshot().ChartSeconds-12)<.0001&&order.Document.Runtime.Nodes.Single(n=>n.Id==selectedEffect).StartSeconds==12,
                "adding from the library selects the effect and seeks to its start for immediate preview");Model=model;
            CheckCustomEffect(check,output,snapshot);
        }finally{Model=previous;libraryFilter=previousFilter;selectedEffect=previousNode;selectedParameter=previousParameter;selectedKey=previousKey;collapsedEffects.Clear();Surface!.Draw(Model!,Bridge.Snapshot(),false);}
    }
    void CheckCustomEffect(Action<bool,string> check,string output,PlaybackSnapshot snapshot)
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-effect-fixture");if(at<0)return;
        string file=args[at+1];byte[] original=File.ReadAllBytes(file);
        var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        int count=model.Library.Effects.Count;var effect=model.Library.Import(file,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        int importedCount=model.Library.Effects.Count;
        check(importedCount>=count&&importedCount<=count+1&&model.Document.Runtime.Nodes.Length==2,"importing a custom effect makes it available without changing the artwork");
        check(ReferenceEquals(effect,model.Library.Import(file,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString()))&&model.Library.Effects.Count==importedCount,"reimporting identical shader content deduplicates the local cache");
        var library=EffectLibrary.Load(folder);check(library.Effects.Any(e=>e.Id==effect.Id),"custom effect library metadata persists across editor reloads");
        string instance=model.AddEffect(effect,2);snapshot=snapshot with{ChartSeconds=3,AudioSeconds=3};
        string first=HashTarget(Surface!.Render(model,snapshot,96,64));
        check(HashTarget(Surface.Render(model,snapshot with{ChartSeconds=3.4,AudioSeconds=3.4},96,64))!=first,"imported procedural HLSL renders and uses the effect-local clock");
        model.UpdateParameter(instance+"/_Intensity",new ParameterValue(new NVector4(0,0,0,0)));model.Commit();
        check(HashTarget(Surface.Render(model,snapshot,96,64))!=first,"imported custom shader parameters affect actual GPU output");
        var descriptor=JsonConvert.DeserializeObject<CustomEffectDocument>(File.ReadAllText(file),CustomJsonForCheck())!;
        descriptor.UnityVersion="unsupported";string invalid=Path.Combine(output,"incompatible-effect.json");
        File.WriteAllText(invalid,JsonConvert.SerializeObject(descriptor,CustomJsonForCheck()));bool rejected=false;
        try{model.Library.Import(invalid,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());}catch(InvalidDataException){rejected=true;}
        check(rejected,"custom shader import rejects incompatible build versions before caching");
        string damaged=Path.Combine(folder,"UserEffects","damaged-test","effect.json");Directory.CreateDirectory(Path.GetDirectoryName(damaged)!);File.WriteAllText(damaged,"invalid JSON");
        var recovered=EffectLibrary.Load(folder);
        check(recovered.Warnings.Count>0&&recovered.Effects.Any(e=>e.Id==effect.Id),"a damaged custom cache entry does not disable the editor or valid effects");
        check(File.ReadAllBytes(file).SequenceEqual(original),"custom shader import leaves the author source descriptor untouched");
        int projectAt=Array.IndexOf(args,"-vfx-project-fixture");
        if(projectAt>=0) {
            var old=Store().Open(args[projectAt+1]);var migrated=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            migrated.Replace(old.Session);migrated.AddEffect(migrated.Library.Effects.Single(e=>e.Id==effect.Id),2);
            string migratedFile=Path.Combine(output,"legacy-with-custom.vfx.json");Store().Save(migrated.Session,migratedFile,migrated.Session.Revision);
            check(string.Equals(Path.GetFullPath(migrated.Session.Snapshot().AssetRoot),Path.GetFullPath(folder),StringComparison.OrdinalIgnoreCase)
                &&Store().Open(migratedFile).Session.Snapshot().Runtime.Nodes.Any(n=>n.EffectId==effect.Id),
                "an older project relinks the active asset cache and can save newly imported effects");
        }
    }
    static JsonSerializerSettings CustomJsonForCheck()=>new(){Converters={new Newtonsoft.Json.Converters.StringEnumConverter()}};
}
