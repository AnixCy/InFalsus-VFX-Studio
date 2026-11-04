using System.IO.Compression;
using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckChartPackageExport(Action<bool,string> check,string output)
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-chart-package-source");if(at<0)return;
        var previous=Model;string priorDirectory=addonSongDirectory,priorImage=imagePath;var picker=addonFolderDialog;
        int priorDifficulty=addonDifficulty;bool priorWhole=addonWholeSong,priorZip=addonZip;
        try {
            Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());imagePath="";
            addonSongDirectory=args[at+1];addonDifficulty=3;addonWholeSong=true;addonZip=true;addonFolderDialog=_=>output;
            var song=ChartPack.ReadSong(addonSongDirectory,PackFields);string hash=AddonPackage.Hash(Path.Combine(song.Root,"if"));
            string before=JsonConvert.SerializeObject(Model.Session.Snapshot(),Json);
            BeginAddonExport();check(addonDialog&&addonWholeSong&&BlocksHostInput,"default export view offers a whole-song package without needing a machine target catalog");
            ExportSongPackage();check(File.Exists(lastAddonExport)&&lastAddonExport.EndsWith(".zip",StringComparison.OrdinalIgnoreCase),"actual Studio action exports a complete song ZIP on Mono");
            using(var archive=ZipFile.OpenRead(lastAddonExport)) {
                check(archive.Entries.Any(e=>e.FullName.EndsWith("/if"))&&archive.Entries.Any(e=>e.FullName.EndsWith("/vfx/chart-pack.json")),"Studio ZIP carries native chart metadata and colocated VFX bindings");
                check(archive.Entries.Any(e=>e.FullName.EndsWith("/audio.wav"))&&archive.Entries.Count(e=>e.FullName.EndsWith(".spc"))==2,"complete package retains audio and all declared difficulty charts");
            }
            check(AddonPackage.Hash(Path.Combine(song.Root,"if"))==hash&&JsonConvert.SerializeObject(Model.Session.Snapshot(),Json)==before&&!Model.Session.IsDirty,"whole-song export preserves the source song and VFX authoring state");
            addonDifficulty=0;bool blocked=false;try{ExportSongPackage();}catch(InvalidDataException){blocked=true;}
            check(blocked,"missing difficulty is reported before creating a distribution package");
            string creator=Path.Combine(output,"StudioSource");Directory.CreateDirectory(creator);
            File.Copy(Path.Combine(song.Root,"audio.wav"),Path.Combine(creator,"audio.wav"));File.Copy(Path.Combine(song.Root,"jacket.png"),Path.Combine(creator,"jacket.png"));
            Directory.CreateDirectory(Path.Combine(creator,".ifstudio/Charts"));
            for(int i=2;i<4;i++)File.WriteAllText(Path.Combine(creator,".ifstudio/Charts/"+i+".spc"),"chart(120,4)\ntap(1000,1,1)\n");
            File.WriteAllText(Path.Combine(creator,"infalsus.studio.json"),JsonConvert.SerializeObject(new{format="InFalsusStudioSong",version=1,title="Creator Fixture",artist="Fixture",audio="audio.wav",jacket="jacket.png",
                audioOffsetMs=125,previewStartSeconds=0,previewEndSeconds=1,difficulties=new[]{new{index=2,rating="1",designer="Fixture",working=".ifstudio/Charts/2.spc",jacket="jacket.png"},new{index=3,rating="1",designer="Fixture",working=".ifstudio/Charts/3.spc",jacket="jacket.png"}}}));
            string sourceText=File.ReadAllText(Path.Combine(creator,".ifstudio/Charts/3.spc"));
            string temporary;
            using(var prepared=StudioSongPackage.Prepare(creator,Model.Document.ProjectId,Path.Combine(folder,"GameExport/ChartCompiler"))) {
                temporary=prepared.Root;
                var compiledSong=ChartPack.ReadSong(prepared.Root,PackFields);
                check(compiledSong.Charts.Count(c=>c!=null)==2&&compiledSong.Id.StartsWith("vfx_",StringComparison.Ordinal),"saved Studio project compiles all populated rated difficulties without an installed Python");
                check(PackFields(File.ReadAllText(Path.Combine(prepared.Root,"if")),"jacket").All(j=>!string.IsNullOrWhiteSpace(j)),"export metadata supplies display jackets for absent difficulties required by the native chart loader");
                check(Math.Abs(prepared.ChartOffsetSeconds-.125)<.000001,"creator compiler reports the one applied chart-to-audio offset");
                byte[] bytes=File.ReadAllBytes(Path.Combine(prepared.Root,compiledSong.Charts[3]!));
                check(System.Text.Encoding.ASCII.GetString(bytes,0,4)=="ICP1","plaintext authoring chart becomes a playable ICP1 binary");
            }
            check(!Directory.Exists(temporary)&&File.ReadAllText(Path.Combine(creator,".ifstudio/Charts/3.spc"))==sourceText,"temporary compilation tree is released without editing the Studio source");
            using(var prepared=StudioSongPackage.Prepare(creator,Model.Document.ProjectId,Path.Combine(folder,"GameExport/ChartCompiler"),true)) {
                var compiledSong=ChartPack.ReadSong(prepared.Root,PackFields);byte[] bytes=File.ReadAllBytes(Path.Combine(prepared.Root,compiledSong.Charts[3]!));
                check(System.Text.Encoding.ASCII.GetString(bytes,0,4)=="SPC2","explicit community export compiles SPC2 without rewriting parser settings in the saved project");
            }
            addonSongDirectory=creator;addonDifficulty=3;imagePath=Path.Combine(creator,"jacket.png");ExportSongPackage();
            check(File.Exists(lastAddonExport),"actual whole-song action accepts a Studio source project directly");
            using(var archive=ZipFile.OpenRead(lastAddonExport)) {
                var entry=archive.Entries.Single(e=>e.FullName.EndsWith("/vfx/FBD/runtime.json"));
                using var reader=new StreamReader(entry.Open());var document=JsonConvert.DeserializeObject<InFalsus.Presentation.PackageDocument>(reader.ReadToEnd(),Json)!;
                double expected=Model.Document.Runtime.OffsetSeconds-(Model.Document.Runtime.TimeDomain==InFalsus.Presentation.TimeDomain.Chart?.125:0);
                check(Math.Abs(document.OffsetSeconds-expected)<.000001,"chart-domain VFX offset compensates compilation once; audio domain remains unchanged");
            }
        }finally{Model=previous;addonSongDirectory=priorDirectory;imagePath=priorImage;addonFolderDialog=picker;addonDifficulty=priorDifficulty;addonWholeSong=priorWhole;addonZip=priorZip;addonDialog=false;}
    }
}
