using InFalsus.Presentation;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    int previewHeight=1440;
    static readonly int[] PreviewHeights={1440,1080,720};
    static readonly string[] PreviewLabels={"2K","1080p","720p"};
    sealed class PreviewSettings {public int Height=1440;}
    string PreviewSettingsFile=>Path.Combine(Environment.GetEnvironmentVariable("VFX_WORK_ROOT")??Path.GetFullPath(Path.Combine(folder,"../../../UserData/vfx")),"preview-render.json");
    static int ValidPreviewHeight(int height)=>height is 1440 or 1080 or 720?height:1440;
    (int Width,int Height) PreviewSize=>(previewHeight*16/9,previewHeight);
    void LoadPreviewSettings()
    {
        previewHeight=1440;
        try {
            if(!File.Exists(PreviewSettingsFile))return;
            if(new FileInfo(PreviewSettingsFile).Length>8192)throw new InvalidDataException("Preview settings are too large.");
            previewHeight=ValidPreviewHeight(JsonConvert.DeserializeObject<PreviewSettings>(File.ReadAllText(PreviewSettingsFile))?.Height??1440);
        }catch(Exception e){Logger.LogWarning("Preview resolution ignored: "+e.Message);}
    }
    void SetPreviewResolution(int height)
    {
        previewHeight=ValidPreviewHeight(height);
        string file=PreviewSettingsFile;Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            File.WriteAllText(temporary,JsonConvert.SerializeObject(new PreviewSettings{Height=previewHeight},Json));
            if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    void DrawPreviewResolution(Rect stage)
    {
        float s=UiScale,x=stage.xMax-192*s,y=stage.y+2*s;
        if(stage.width>304*s) {
            string audio=Bridge.Transport?.Clip?"音乐 · "+Bridge.Transport!.Clip.name:"静音时间轴";
            ui!.Label(new Rect(stage.x,y,stage.width-204*s,24*s),ui.Fit(audio,stage.width-204*s,12),12,"#7c9dad");
        }
        for(int i=0;i<3;i++) {
            int height=PreviewHeights[i];
            if(ui!.Button(new Rect(x+i*64*s,y,60*s,24*s),PreviewLabels[i],previewHeight==height,false,12))Queue(()=>SetPreviewResolution(height));
        }
    }
    RenderTexture RenderWorkspacePreview(PlaybackSnapshot snapshot)
    {
        var size=PreviewSize;return Surface!.Render(Model!,snapshot,size.Width,size.Height);
    }
}
