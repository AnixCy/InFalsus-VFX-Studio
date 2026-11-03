using InFalsus.Presentation;
using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckAddonExport(Action<bool,string> assert,string output)
    {
        var previousModel=Model;var previousImage=imagePath;var previousPicker=addonFolderDialog;
        try {
            Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());imagePath="";
            string before=JsonConvert.SerializeObject(Model.Session.Snapshot(),Json);
            BeginAddonExport();
            assert(addonDialog&&BlocksHostInput&&addonTargets.Length>0,"addon export view binds prepared target catalog and blocks chart input");
            RouteWorkspaceEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Space},false);
            assert(!Bridge.Transport!.Playing,"export modal cannot start audio via a workspace shortcut");
            assert(!MayQuit()&&!addonDialog,"close while exporting dismisses one dialog without recursive quit confirmation");
            addonTarget=0;addonFolderDialog=_=>output;ExportSelectedAddon();
            var addon=AddonPackage.Load(Path.Combine(lastAddonExport,"addon.json"),
                text=>JsonConvert.DeserializeObject<AddonManifest>(text,Json)!,text=>JsonConvert.DeserializeObject<PackageDocument>(text,Json)!,AddonPackage.Capabilities);
            assert(addon.Document.Nodes.Length==1&&addon.Document.Nodes[0].Target=="background","actual Studio export action produces a playable single-background package");
            assert(JsonConvert.SerializeObject(Model.Session.Snapshot(),Json)==before&&!Model.Session.IsDirty,"Studio export preserves source document, saved state and undo history");
            Model.AddEffect(Model.Library.Effects.Single(e=>e.Id=="ripple"),0);
            bool rejected=false;try{BackgroundExport.Compile(Model.Session.Snapshot(),addonTargets[0],"Gamma");}catch(NotSupportedException){rejected=true;}
            assert(rejected,"unsupported effect from actual editor is rejected rather than silently dropped");
        } finally {Model=previousModel;imagePath=previousImage;addonFolderDialog=previousPicker;addonDialog=false;addonTarget=-1;}
    }
}
