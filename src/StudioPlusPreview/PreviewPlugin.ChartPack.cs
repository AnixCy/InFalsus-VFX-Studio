using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    bool addonWholeSong=true,addonZip=true;
    string addonSongDirectory="";
    int addonDifficulty=3;
    int addonChartFormat;
    static string?[] PackFields(string text,string key)
    {
        var token=JObject.Parse(text)[key];if(token==null)return Array.Empty<string?>();
        string? Scalar(JToken t)=>t.Type==JTokenType.Null?null:t.Type==JTokenType.String?(string?)t:throw new InvalidDataException("曲包配置字段类型错误。");
        return token is JArray array?array.Select(Scalar).ToArray():new[]{Scalar(token)};
    }
    void DrawSongPackageWindow(float w,float h)
    {
        var theme=ui!;theme.Panel(R(0,0,w,h));theme.Label(R(20,14,w-40,36),"导出完整曲包",22,"#c9e9ee");
        theme.Paragraph(R(20,62,w-40,62),"选择已保存的 Studio 歌曲工程，或已有可游玩曲包。自动编译全部有效难度，携带歌曲、曲绘和当前难度演出；玩家解压到 Charts 即可。",14);
        if(theme.Button(R(20,138,180,32),"选择谱面曲包…")) {
            addonDialog=false;actions.Enqueue(()=>{try{string selected=addonFolderDialog("选择 Studio 歌曲工程或可游玩曲包文件夹");if(selected.Length>0){if(!StudioSongPackage.CanRead(selected))throw new InvalidDataException("请选择含 infalsus.studio.json 或 if 的歌曲文件夹。");addonSongDirectory=selected;addonError="";}}catch(Exception e){addonError=e.Message;}finally{addonDialog=true;}});
        }
        theme.Paragraph(R(20,182,w-40,54),addonSongDirectory.Length>0?addonSongDirectory:"尚未选择歌曲。导出读取磁盘中已保存的谱面和歌曲资料。",13);
        theme.Label(R(20,253,110,30),"当前演出难度",14);
        for(int i=0;i<4;i++)if(theme.Button(R(142+i*92,251,84,32),ChartPack.DifficultyNames[i],addonDifficulty==i,false,14))addonDifficulty=i;
        theme.Label(R(20,309,110,30),"输出格式",14);
        if(theme.Button(R(142,307,110,32),"ZIP",addonZip,false,14))addonZip=true;
        if(theme.Button(R(260,307,110,32),"文件夹",!addonZip,false,14))addonZip=false;
        theme.Label(R(20,358,110,30),"谱面格式",14);
        string[] formats={"跟随工程","ICP1","SPC2 · 社区"};
        for(int i=0;i<formats.Length;i++)if(theme.Button(R(142+i*130,356,122,32),formats[i],addonChartFormat==i,false,13))addonChartFormat=i;
        theme.Paragraph(R(20,h-139,w-40,72),addonError.Length>0?addonError:"保留全部已声明难度、音频及曲绘。已有随谱演出保留，其余难度按原谱面游玩；玩家首次安装一次通用 Mod。",13,addonError.Length>0?"#efb0ba":"#a6c3d1");
        if(theme.Button(R(20,h-50,92,32),"取消"))addonDialog=false;
        if(theme.Button(R(124,h-50,142,32),"独立演出…",false,false,13)){addonWholeSong=false;addonError="";}
        bool enabled=GUI.enabled;GUI.enabled=enabled&&addonSongDirectory.Length>0;
        if(theme.Button(R(w-190,h-50,170,32),"导出曲包…")) {
            addonDialog=false;actions.Enqueue(()=>{try{ExportSongPackage();}catch(Exception e){addonError=e.Message;addonDialog=true;Logger.LogWarning("Chart package export: "+e);}});
        }
        GUI.enabled=enabled;
    }
    void ExportSongPackage()
    {
        var profile=JsonConvert.DeserializeObject<GameExportProfile>(File.ReadAllText(Path.Combine(folder,"GameExport/profile.json")),Json)!;
        using var prepared=StudioSongPackage.Prepare(addonSongDirectory,Model!.Document.ProjectId,Path.Combine(folder,"GameExport/ChartCompiler"),addonChartFormat==0?null:addonChartFormat==2);
        var song=ChartPack.ReadSong(prepared.Root,PackFields);
        if(string.IsNullOrWhiteSpace(song.Charts[addonDifficulty]))throw new InvalidDataException("所选难度没有谱面。");
        string parent=addonFolderDialog("选择完整曲包输出位置");if(parent.Length==0)return;
        string name="Song_"+song.Id+"_"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,4)+(addonZip?".zip":"");
        string destination=Path.Combine(parent,name);
        var project=MaterialExportProject(Model.Session.Snapshot());var context=MaterialExportContext(project,profile.ColorSpace);
        if(project.Runtime.TimeDomain==InFalsus.Presentation.TimeDomain.Chart)project.Runtime.OffsetSeconds-=prepared.ChartOffsetSeconds;
        ChartPackExport.Publish(prepared.Root,addonDifficulty,project,imagePath,Path.Combine(folder,"GameExport",profile.ColorSpace,"background.bundle"),profile.ColorSpace,
            destination,addonZip,o=>JsonConvert.SerializeObject(o,Json),PackFields,t=>JsonConvert.DeserializeObject<ChartPackManifest>(t,Json)!,t=>JsonConvert.DeserializeObject<AddonManifest>(t,Json)!,context);
        lastAddonExport=destination;status="完整曲包已导出："+destination;
    }
}
