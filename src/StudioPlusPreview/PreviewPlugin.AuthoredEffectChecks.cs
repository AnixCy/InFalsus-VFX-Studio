using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    sealed class EffectAuthoringSpec
    {
        public int FormatVersion=1;
        public string Name="",SourceDirectory="",BackgroundPath="";
        public double DurationSeconds {get;set;}
        public Dictionary<string,double[]> Defaults=new();
        public EffectAuthoringTrack[] Tracks=Array.Empty<EffectAuthoringTrack>();
        public double[] CheckTimes=Array.Empty<double>();
    }
    sealed class EffectAuthoringTrack {public string Property="";public EffectAuthoringKey[] Keys=Array.Empty<EffectAuthoringKey>();}
    sealed class EffectAuthoringKey {public double Seconds {get;set;}public double[] Value=Array.Empty<double>();public Easing Ease=Easing.Linear;}
    void CheckAuthoredEffect(Action<bool,string> check,string output,Dictionary<string,string> hashes)
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-author-spec");if(at<0)return;
        string file=Path.GetFullPath(args[at+1]);if(new FileInfo(file).Length>256*1024)throw new InvalidDataException("Authoring spec exceeds 256 KiB.");
        byte[] original=File.ReadAllBytes(file);
        var spec=JsonConvert.DeserializeObject<EffectAuthoringSpec>(File.ReadAllText(file),Json)??throw new InvalidDataException("Invalid authoring spec.");
        if(spec.FormatVersion!=1||spec.Name.Length is <1 or >100||!double.IsFinite(spec.DurationSeconds)||spec.DurationSeconds is <=0 or >3600
            ||spec.Tracks.Length>128||spec.CheckTimes.Length is <2 or >16)throw new InvalidDataException("Invalid authoring spec limits.");
        string directory=Path.GetDirectoryName(file)!;
        string Resolve(string path)=>Path.IsPathRooted(path)?Path.GetFullPath(path):EffectPackage.ResolveAsset(directory,path,false);
        var workspace=SourceWorkspace.Open(Resolve(spec.SourceDirectory));
        var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        var definition=model.Library.Import(workspace.LatestDescriptor()??throw new InvalidDataException("Build the source first."),folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        model.Library.LinkSource(definition,workspace.Root,folder);model.RemoveEffect("adjust");
        string node=model.AddEffect(definition,0);model.SetEffectRange(node,0,spec.DurationSeconds);model.RenameEffect(node,spec.Name);
        using(var edit=model.Session.BeginEdit(model.Session.Revision,"导入参数编排")) {
            edit.SetDefaultValue("background/_PatternMix",PreviewModel.Number(new[]{0d}));
            edit.SetDefaultValue("background/_Distortion",PreviewModel.Number(new[]{0d}));
            foreach(var value in spec.Defaults)edit.SetDefaultValue(node+"/"+value.Key,PreviewModel.Number(value.Value));
            foreach(var track in spec.Tracks) {
                string parameter=node+"/"+track.Property;var descriptor=model.Catalog.Get(parameter);
                foreach(var key in track.Keys) {
                    if(!double.IsFinite(key.Seconds)||key.Seconds<0||key.Seconds>spec.DurationSeconds||key.Value.Length!=(descriptor.Binding.Kind==ParameterKind.Float?1:4))
                        throw new InvalidDataException("Invalid key: "+track.Property);
                    edit.AddKey(parameter,KeyPosition.Seconds(key.Seconds),PreviewModel.Number(key.Value),key.Ease);
                }
            }
            edit.Commit();
        }
        model.Refresh();string background=Resolve(spec.BackgroundPath);Surface!.ApplyImage(Surface.PrepareImage(background));
        check(model.Keys.Length==spec.Tracks.Sum(t=>t.Keys.Length),"declarative authoring uses shared transactions and preserves every requested key");
        check(model.MissingAnimationStarts==0,"authored effect tracks have explicit starting anchors");
        var snapshot=Bridge.Snapshot();
        string Render(double seconds)=>HashTarget(Surface.Render(model,snapshot with{AudioSeconds=seconds,ChartSeconds=seconds},512,288));
        var samples=spec.CheckTimes.Select(Render).ToArray();
        check(samples.Distinct().Count()==samples.Length,"authored effect sections produce distinct real GPU states");
        check(Render(spec.CheckTimes[0])==samples[0],"authored effect seeks back to exactly the same GPU output");
        check(Surface.LastPostProcessDraws==1&&Surface.ResourceCount==2,"combined effect uses one postprocess draw plus its background material");
        string target=Path.Combine(output,"AuthoredEffect.vfx.json");
        var store=new ProjectFileStore(p=>JsonConvert.SerializeObject(new ProjectFile{Project=p,BackgroundPath=background},Json),text=>Decode(text).Project,PreviewModel.Capabilities);
        store.Save(model.Session,target,model.Session.Revision);
        var opened=store.Open(target);var reopened=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());reopened.Replace(opened.Session);
        check(reopened.Keys.Select(k=>k.Id).SequenceEqual(model.Keys.Select(k=>k.Id)),"authored project saves and reopens through the actual editor codec with stable key identities");
        check(HashTarget(Surface.Render(reopened,snapshot with{ChartSeconds=spec.CheckTimes[0],AudioSeconds=spec.CheckTimes[0]},512,288))==samples[0],"reopened custom shader project restores its exact GPU state");
        check(File.ReadAllBytes(file).SequenceEqual(original),"authoring never rewrites its input specification");
        for(int i=0;i<samples.Length;i++)hashes["authored-"+spec.CheckTimes[i].ToString(CI)]=samples[i];
        File.WriteAllText(Path.Combine(output,"authored-effect.json"),JsonConvert.SerializeObject(new{project=target,node,source=workspace.Root,effect=definition.Id,
            parameters=definition.Parameters.Where(p=>!p.Hidden).Select(p=>new{p.Property,p.Label,p.Kind,p.Min,p.Max}),keys=model.Keys.Length,
            checkTimes=spec.CheckTimes,postprocessDraws=1,backgroundDraws=1,visualAcceptance="pending",audioPlayed=false,gameStarted=false},Json));
        if(args.Contains("-vfx-author-frames")) {
            string frames=Path.Combine(output,"frames");Directory.CreateDirectory(frames);
            var previous=RenderTexture.active;
            var pixels=new Texture2D(960,540,TextureFormat.RGBAFloat,false,true);
            var png=new Texture2D(960,540,TextureFormat.RGBA32,false);
            try {
                foreach(double seconds in spec.CheckTimes) {
                    var targetFrame=Surface.Render(model,snapshot with{AudioSeconds=seconds,ChartSeconds=seconds},960,540);
                    RenderTexture.active=targetFrame;pixels.ReadPixels(new Rect(0,0,960,540),0,0,false);pixels.Apply();
                    var colors=pixels.GetPixels();
                    if(QualitySettings.activeColorSpace==ColorSpace.Linear)for(int n=0;n<colors.Length;n++)colors[n]=colors[n].gamma;
                    png.SetPixels(colors);png.Apply();
                    File.WriteAllBytes(Path.Combine(frames,seconds.ToString("0.000",CI)+".png"),ImageConversion.EncodeToPNG(png));
                }
            }finally{RenderTexture.active=previous;DestroyImmediate(pixels);DestroyImmediate(png);}
        }
        Surface.ApplyImage(null);
    }
}
