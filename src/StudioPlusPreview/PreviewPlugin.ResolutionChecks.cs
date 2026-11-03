using Newtonsoft.Json;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckPreviewResolution(Action<bool,string> check)
    {
        int saved=previewHeight;string file=PreviewSettingsFile;
        string? original=File.Exists(file)?File.ReadAllText(file):null;
        long revision=Model!.Session.Revision;bool dirty=Dirty;var snapshot=Bridge.Snapshot();
        try {
            foreach(int height in new[]{1440,1080,720}) {
                SetPreviewResolution(height);var target=RenderWorkspacePreview(snapshot);
                check(target.width==height*16/9&&target.height==height,"preview selection renders a real "+(height*16/9)+"x"+height+" GPU target");
            }
            int instance=RenderWorkspacePreview(snapshot).GetInstanceID();
            check(RenderWorkspacePreview(snapshot).GetInstanceID()==instance,"unchanged preview resolution reuses its render target");
            previewHeight=1440;LoadPreviewSettings();
            check(previewHeight==720,"preview resolution survives a settings reload");
            File.WriteAllText(file,JsonConvert.SerializeObject(new PreviewSettings{Height=999}));LoadPreviewSettings();
            check(PreviewSize==(2560,1440),"unsupported saved resolution falls back to 2K");
            check(Model.Session.Revision==revision&&Dirty==dirty,"preview resolution changes leave project history and dirty state unchanged");
        }finally {
            previewHeight=saved;
            if(original==null){if(File.Exists(file))File.Delete(file);}else File.WriteAllText(file,original);
        }
    }
}
