using System.IO.Compression;
using InFalsus.Presentation;
using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckMaterialExport(Action<bool,string> check,string output)
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-chart-package-source");if(at<0)return;
        var old=Model;string oldImage=imagePath,oldSong=addonSongDirectory;int oldDifficulty=addonDifficulty;bool oldZip=addonZip;var picker=addonFolderDialog;
        try {
            Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            void Set(string id,float value){Model.UpdateParameter(id,new ParameterValue(new NVector4(value,0,0,0)));Model.Commit();}
            Set("background/_PatternMix",0);Set("background/_Distortion",0);
            string flow=Model.AddEffect(Model.Library.Effects.Single(e=>e.Id=="bg-control-ephemeralamount"),0);
            string feedback=Model.AddEffect(Model.Library.Effects.Single(e=>e.Id=="bg-control-feedbackamount"),0);
            Model.UpdateAnimatedParameter(flow+"/_EphemeralAmount",new ParameterValue(new NVector4(1,0,0,0)),2,null,true);Model.Commit();
            Set(feedback+"/_FeedbackRotation",-.2f);
            var other=Model.Library.Effects.First(e=>e.Id.StartsWith("bg-control-",StringComparison.Ordinal)&&e.BackgroundSource!=Model.Library.Effects.Single(e=>e.Id=="bg-control-ephemeralamount").BackgroundSource);
            Model.AddEffect(other,6);
            imagePath=Path.Combine(args[at+1],"jacket.png");addonSongDirectory=args[at+1];addonDifficulty=3;addonZip=true;addonFolderDialog=_=>output;
            string before=JsonConvert.SerializeObject(Model.Session.Snapshot(),Json);ExportSongPackage();
            using var archive=ZipFile.OpenRead(lastAddonExport);var entry=archive.Entries.Single(e=>e.FullName.EndsWith("/vfx/FBD/runtime.json"));
            using var reader=new StreamReader(entry.Open());var doc=JsonConvert.DeserializeObject<PackageDocument>(reader.ReadToEnd(),Json)!;
            check(doc.Requires.Contains(BackgroundMaterialPlan.Capability)&&doc.Nodes.Count(BackgroundMaterialPlan.IsControl)==3,"actual whole-song export carries generic background controls across more than one shader family");
            var plan=new BackgroundMaterialPlan(doc);var exported=EffectPackage.FromDocument(doc,output,AddonPackage.Capabilities,false);
            var sampled=new ParameterValue[doc.Channels.Length];exported.Timeline.Sample(1,sampled);var state=plan.Sample(1,sampled)!;
            var expected=Model.Library.BackgroundPlan(Model.RenderDocument);Model.Sample(1);var expectedState=expected.Sample(1,Model.Values)!;
            check(state.Definition.SourceId==expectedState.Definition.SourceId&&state.Values.SequenceEqual(expectedState.Values)&&state.Keywords.SequenceEqual(expectedState.Keywords),"export and preview share exact material selection, values and keywords at the same time");
            check(plan.States.Count==3&&archive.Entries.Count(e=>e.FullName.EndsWith(".bundle"))==3,"two controls share their source material and only referenced source bundles are packaged");
            check(before==JsonConvert.SerializeObject(Model.Session.Snapshot(),Json),"generic background export does not rewrite the authoring document or its keyframes");
            Surface!.Render(Model,Bridge.Snapshot() with{ChartSeconds=1,AudioSeconds=1},96,64);
            File.WriteAllText(Path.Combine(output,"material-runtime-expectations.json"),JsonConvert.SerializeObject(new{Shader=Surface.Material.shader.name,Property="_EphemeralAmount",Texture="_BaseMap"},Json));
            File.Copy(lastAddonExport,Path.Combine(output,"MaterialFixture.zip"));
            Set("adjust/_Exposure",.5f);bool blocked=false;try{ExportSongPackage();}catch(NotSupportedException){blocked=true;}
            check(blocked,"non-neutral unsupported postprocess grading is reported rather than silently discarded");
            int sourceAt=Array.IndexOf(args,"-vfx-material-source");
            if(sourceAt>=0) {
                var workspace=SourceWorkspace.Open(args[sourceAt+1]);Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
                var definition=Model.Library.Import(workspace.LatestDescriptor()!,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
                Model.Library.LinkSource(definition,workspace.Root,folder);Model.SetBackground(definition);
                string sourceHash=AddonPackage.Hash(Path.Combine(workspace.Root,workspace.Shader));
                Set("background/_Weave",.6f);ExportSongPackage();
                using(var custom=ZipFile.OpenRead(lastAddonExport)) {
                    var runtime=custom.Entries.Single(e=>e.FullName.EndsWith("/runtime.json"));using var input=new StreamReader(runtime.Open());var compiled=JsonConvert.DeserializeObject<PackageDocument>(input.ReadToEnd(),Json)!;
                    check(compiled.Nodes.Single().BackgroundBinding!.Properties.Any(p=>p.Property=="_Weave")&&compiled.UnityVersion=="6000.3.9f1","unrelated custom background shader builds for the game and exports its discovered properties without an effect-specific adapter");
                    check(custom.Entries.Count(e=>e.FullName.EndsWith(".bundle"))==1&&!custom.Entries.Any(e=>e.FullName.EndsWith(".shader")),"custom background package contains one precompiled material bundle and no creator compiler/source dependency");
                }
                var receipts=Directory.GetFiles(Path.Combine(folder,"GameExport/Compiled"),"export-ok.json",SearchOption.AllDirectories).ToDictionary(p=>p,File.GetLastWriteTimeUtc);
                ExportSongPackage();check(Directory.GetFiles(Path.Combine(folder,"GameExport/Compiled"),"export-ok.json",SearchOption.AllDirectories).Length==receipts.Count&&receipts.All(p=>File.GetLastWriteTimeUtc(p.Key)==p.Value)&&AddonPackage.Hash(Path.Combine(workspace.Root,workspace.Shader))==sourceHash,"unchanged custom source reuses its game build and never edits the original shader");
                File.Copy(lastAddonExport,Path.Combine(output,"CustomMaterialFixture.zip"));
            }
        }finally{Model=old;imagePath=oldImage;addonSongDirectory=oldSong;addonDifficulty=oldDifficulty;addonZip=oldZip;addonFolderDialog=picker;addonDialog=false;Surface!.Draw(Model!,Bridge.Snapshot(),false);}
    }
}
