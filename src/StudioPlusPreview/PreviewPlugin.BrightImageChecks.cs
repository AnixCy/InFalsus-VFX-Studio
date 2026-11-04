using InFalsus.Presentation;
using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckBrightImage(Action<bool,string> check,string output)
    {
        var oldModel=Model;var picker=openDialog;var saver=saveDialog;
        var old=(imagePath,savedImagePath,brightImagePath,savedBrightImagePath,projectFile,fileVersion,audioPath,savedAudioPath);
        string dir=Path.Combine(output,"BrightImage");Directory.CreateDirectory(dir);
        string MakeImage(string name,Color color) {
            var image=new Texture2D(16,16,TextureFormat.RGBA32,false);image.SetPixels(Enumerable.Repeat(color,256).ToArray());image.Apply();
            string file=Path.Combine(dir,name+".png");File.WriteAllBytes(file,ImageConversion.EncodeToPNG(image));DestroyImmediate(image);return file;
        }
        var snapshot=Bridge.Snapshot() with{ChartSeconds=2,AudioSeconds=2};
        string Render()=>HashTarget(Surface!.Render(Model!,snapshot,256,144));
        try {
            Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            brightImagePath=savedBrightImagePath=imagePath=savedImagePath=audioPath=savedAudioPath=projectFile="";fileVersion=null;
            Surface!.ApplyBrightImage(null);
            string main=MakeImage("main",new Color(.1f,.2f,.3f,1)),bright=MakeImage("bright",new Color(1,.1f,.2f,1));
            openDialog=(_,_)=>main;ChooseImage();
            var definition=Model.Library.Effects.Single(e=>e.Id=="bg-control-brightamount");string id=Model.AddEffect(definition,0);
            Model.UpdateParameter(id+"/_BrightAmount",new ParameterValue(new NVector4(1,0,0,0)));Model.Commit();
            string baseline=Render();var defaultTexture=Surface.Material.GetTexture("_BrightMap");
            string artwork=JsonConvert.SerializeObject(Model.Session.Snapshot(),Json);long revision=Model.Session.Revision;
            openDialog=(_,_)=>bright;ChooseBrightImage();string changed=Render();
            check(changed!=baseline&&Surface.Material.GetTexture("_BrightMap")!=defaultTexture,"custom Bright image reaches the original background Shader and changes real GPU pixels");
            check(brightImagePath==bright&&Model.Session.Revision==revision&&JsonConvert.SerializeObject(Model.Session.Snapshot(),Json)==artwork,"choosing a Bright image preserves every effect parameter and keyframe");
            check(Surface.ResourceCount==2&&Surface.LastPostProcessDraws==1,"custom Bright image adds no material instance or postprocess pass");
            ClearBrightImage();check(Render()==baseline&&Surface.Material.GetTexture("_BrightMap")==defaultTexture,"clearing Bright image restores the exact material-default GPU output");
            openDialog=(_,_)=>bright;ChooseBrightImage();
            openDialog=(_,_)=>"";ChooseBrightImage();check(brightImagePath==bright&&Render()==changed,"canceling the Bright image picker preserves the selected texture");
            string invalid=Path.Combine(dir,"invalid.png");File.WriteAllText(invalid,"not an image");bool rejected=false;
            openDialog=(_,_)=>invalid;try{ChooseBrightImage();}catch{rejected=true;}
            check(rejected&&brightImagePath==bright&&Render()==changed,"invalid Bright image selection preserves the previous working GPU texture");
            string saved=Path.Combine(dir,"saved.vfx.json");saveDialog=_=>saved;
            check(TrySaveProject()&&!Dirty&&Decode(File.ReadAllText(saved)).BrightBackgroundPath==bright,"saving the project persists the Bright image path and clears its dirty state");
            ClearBrightImage();check(Dirty,"clearing a saved Bright image is tracked as an unsaved change");
            openDialog=(_,_)=>saved;OpenProjectFile();check(brightImagePath==bright&&Render()==changed&&!Dirty,"opening a saved project restores its custom Bright image without changing animation");
            var document=JObject.Parse(File.ReadAllText(saved));document.Remove("BrightBackgroundPath");
            string legacy=Path.Combine(dir,"legacy.vfx.json");File.WriteAllText(legacy,document.ToString());
            openDialog=(_,_)=>legacy;OpenProjectFile();check(brightImagePath.Length==0&&Render()==baseline,"legacy projects without a Bright image field retain the original default picture");
            openDialog=(_,_)=>bright;ChooseBrightImage();
            document["BrightBackgroundPath"]=Path.Combine(dir,"missing.png");string missing=Path.Combine(dir,"missing.vfx.json");File.WriteAllText(missing,document.ToString());
            var session=Model.Session;string currentFile=projectFile;rejected=false;
            openDialog=(_,_)=>missing;try{OpenProjectFile();}catch{rejected=true;}
            check(rejected&&ReferenceEquals(session,Model.Session)&&projectFile==currentFile&&Render()==changed,"missing Bright image on project open leaves the current session and pictures untouched");
            openDialog=(_,_)=>main;ChooseImage();check(brightImagePath==bright&&Render()==changed,"replacing the main background retains the independently configured Bright image");
            var target=new AddonTarget{SongKey="game:2",Difficulty=3,Label="Bright fixture"};
            var project=MaterialExportProject(Model.Session.Snapshot());var context=MaterialExportContext(project,"Gamma");
            string addon=Path.Combine(dir,"Addon");
            BackgroundExport.Publish(project,target,imagePath,"unused","Gamma",addon,o=>JsonConvert.SerializeObject(o,Json),context);
            var package=AddonPackage.Load(Path.Combine(addon,"addon.json"),s=>JsonConvert.DeserializeObject<AddonManifest>(s,Json)!,s=>JsonConvert.DeserializeObject<PackageDocument>(s,Json)!,AddonPackage.Capabilities);
            var source=package.Document.Nodes.Single(n=>n.BackgroundBinding?.TextureBindings.ContainsKey("_BrightMap")==true);
            string relative=source.Textures[source.BackgroundBinding!.TextureBindings["_BrightMap"]].Substring(5);
            check(AddonPackage.Hash(Path.Combine(addon,relative))==AddonPackage.Hash(bright)&&package.Document.Requires.Contains(BackgroundMaterialPlan.TextureCapability),"actual editor export includes the exact Bright image as a portable validated binding");
            check(Directory.GetFiles(Path.Combine(addon,"materials")).Length==2&&Directory.GetFiles(Path.Combine(addon,"images")).Length==1,"Bright export retains a single input/source material pair and only the referenced auxiliary image");
            bool oldPlayer=false;try{EffectPackage.FromDocument(package.Document,addon,AddonPackage.Capabilities.Where(c=>c!=BackgroundMaterialPlan.TextureCapability).ToArray(),false);}catch(NotSupportedException){oldPlayer=true;}
            check(oldPlayer,"old player capabilities reject custom Bright packages explicitly");
            Model.EnableEffect(id,false);var inactive=MaterialExportProject(Model.Session.Snapshot());var inactiveContext=MaterialExportContext(inactive,"Gamma");
            var inactiveExport=BackgroundExport.Compile(inactive,target,"Gamma",inactiveContext);
            check(!inactiveExport.Requires.Contains(BackgroundMaterialPlan.TextureCapability)&&!inactiveExport.Nodes.Any(n=>n.BackgroundBinding?.TextureBindings.Count>0),"unused Bright settings do not add image resources or require a newer player");
        }finally {
            Surface!.ApplyBrightImage(null);Surface.ApplyImage(null);Model=oldModel;
            (imagePath,savedImagePath,brightImagePath,savedBrightImagePath,projectFile,fileVersion,audioPath,savedAudioPath)=old;
            if(imagePath.Length>0)Surface.ApplyImage(Surface.PrepareImage(imagePath));
            if(brightImagePath.Length>0)Surface.ApplyBrightImage(Surface.PrepareImage(brightImagePath));
            openDialog=picker;saveDialog=saver;Surface.Draw(Model!,Bridge.Snapshot(),false);
        }
    }
}
