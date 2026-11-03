using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    bool addonDialog;
    AddonTarget[] addonTargets=Array.Empty<AddonTarget>();
    int addonTarget=-1;
    string addonSearch="",addonError="";
    Vector2 addonScroll;
    Func<string,string> addonFolderDialog=StudioBridge.Folder;
    string lastAddonExport="";
    void BeginAddonExport()
    {
        FinishText();Model!.Commit();StopHeldPreview();Bridge.Transport?.Pause();
        var file=Path.Combine(folder,"GameExport/targets.json");
        if(!File.Exists(file))throw new FileNotFoundException("测试包未附目标谱面目录。",file);
        addonTargets=JsonConvert.DeserializeObject<AddonTarget[]>(File.ReadAllText(file),Json)??Array.Empty<AddonTarget>();
        if(addonTarget>=addonTargets.Length)addonTarget=-1;
        addonError="";addonDialog=true;
    }
    void DrawAddonExport()
    {
        StudioUiTheme.Fill(new Rect(0,0,Screen.width,Screen.height),"#000000b8");
        float width=Mathf.Min(680*UiScale,Screen.width-24),height=Mathf.Min(550*UiScale,Screen.height-24);
        var rect=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
        GUI.ModalWindow(0x56465a,rect,DrawAddonWindow,GUIContent.none,GUIStyle.none);
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){addonDialog=false;Event.current.Use();}
    }
    void DrawAddonWindow(int id)
    {
        float w=Mathf.Min(680, (Screen.width-24)/UiScale),h=Mathf.Min(550,(Screen.height-24)/UiScale);var theme=ui!;
        theme.Panel(R(0,0,w,h));theme.Label(R(20,14,w-40,36),"导出游戏 addon",22,"#c9e9ee");
        theme.Paragraph(R(20,58,w-40,52),"选择目标谱面。当前支持通用背景图片、底色和曝光等基础参数；不改写谱面和音频。",14);
        addonSearch=theme.Number(R(20,117,w-40,29),"vfx_addon_search",addonSearch);
        var view=R(20,158,w-40,h-292);var matches=addonTargets.Select((t,i)=>(Target:t,Index:i))
            .Where(t=>addonSearch.Length==0||t.Target.Label.IndexOf(addonSearch,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
        addonScroll=GUI.BeginScrollView(view,addonScroll,new Rect(0,0,view.width-20,matches.Length*32*UiScale));
        try{for(int i=0;i<matches.Length;i++)if(theme.Button(R(0,i*32,w-66,29),matches[i].Target.Label,addonTarget==matches[i].Index,false,13))addonTarget=matches[i].Index;}
        finally{GUI.EndScrollView();}
        theme.Paragraph(R(20,h-126,w-40,62),addonError.Length>0?addonError:addonTarget>=0?"已选："+addonTargets[addonTarget].Label:"选择一个目标谱面；无 addon 时游戏仍正常游玩。",13,addonError.Length>0?"#efb0ba":"#a6c3d1");
        if(theme.Button(R(20,h-50,110,32),"取消"))addonDialog=false;
        bool enabled=GUI.enabled;GUI.enabled=enabled&&addonTarget>=0;
        if(theme.Button(R(w-190,h-50,170,32),"选择目录并导出…")) {
            addonDialog=false;
            actions.Enqueue(()=>{
                try {
                    ExportSelectedAddon();
                }catch(Exception e){addonError=e.Message;addonDialog=true;Logger.LogWarning("Addon export: "+e);}
            });
        }
        GUI.enabled=enabled;
    }
    void ExportSelectedAddon()
    {
        var target=addonTargets[addonTarget];
        var profile=JsonConvert.DeserializeObject<GameExportProfile>(File.ReadAllText(Path.Combine(folder,"GameExport/profile.json")),Json)!;
        BackgroundExport.Compile(Model!.Session.Snapshot(),target,profile.ColorSpace);
        string parent=addonFolderDialog("选择 addon 导出目录");if(parent.Length==0)return;
        var bundle=Path.Combine(folder,"GameExport",profile.ColorSpace,"background.bundle");
        var destination=Path.Combine(parent,"vfx-addon-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        BackgroundExport.Publish(Model.Session.Snapshot(),target,imagePath,bundle,profile.ColorSpace,destination,o=>JsonConvert.SerializeObject(o,Json));
        lastAddonExport=destination;status="Addon 已导出："+destination;
    }
    sealed class GameExportProfile {public string ColorSpace {get;set;}="Gamma";}
}
