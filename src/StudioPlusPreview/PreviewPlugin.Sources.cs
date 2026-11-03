using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    SourceWorkspace? sourceWorkspace;
    Process? sourceBuild;
    bool autoSourceUpdate=true;
    double sourcePoll;
    string sourceFingerprint="",sourceStatusFingerprint="",sourceMessage="新建模板，用熟悉的编辑器修改源码。";
    sealed class SourcePreference{public string Directory="";public bool AutoUpdate=true;}
    string CustomTools=>Path.GetFullPath(Path.Combine(folder,"../../..","CustomEffects"));
    string SourcePreferences=>Path.Combine(Environment.GetEnvironmentVariable("VFX_WORK_ROOT")??Path.GetFullPath(Path.Combine(folder,"../../../UserData/vfx")),"source-workspace.json");
    static string Quote(string value)=>"\""+value.Replace("\"","\\\"")+"\"";
    void InitSourceWorkspace()
    {
        try{if(File.Exists(SourcePreferences)) {
            var saved=JsonConvert.DeserializeObject<SourcePreference>(File.ReadAllText(SourcePreferences));
            if(saved!=null){autoSourceUpdate=saved.AutoUpdate;if(Directory.Exists(saved.Directory))sourceWorkspace=SourceWorkspace.Open(saved.Directory);}
        }}catch(Exception e){Logger.LogWarning("Source workspace preference ignored: "+e.Message);}
    }
    void UseSource(SourceWorkspace workspace)
    {
        sourceWorkspace=workspace;sourceFingerprint=sourceStatusFingerprint="";sourceMessage="修改源码后 Ctrl+Shift+B 构建；预览自动更新。";libraryFilter="自定义";
        SaveSourcePreferences();
    }
    void SaveSourcePreferences()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SourcePreferences)!);
        File.WriteAllText(SourcePreferences,JsonConvert.SerializeObject(new SourcePreference{Directory=sourceWorkspace?.Root??"",AutoUpdate=autoSourceUpdate}));
    }
    void CreateSourceWorkspace()
    {
        string parent=StudioBridge.Folder("选择新模板的父文件夹");if(parent.Length==0)return;
        string root=Path.Combine(parent,"MyEffect");int number=2;while(Directory.Exists(root)||File.Exists(root))root=Path.Combine(parent,"MyEffect"+number++);
        UseSource(SourceWorkspace.Create(root,CustomTools));OpenSourceInEditor();
    }
    void OpenSourceWorkspace()
    {
        string root=StudioBridge.Folder("打开效果源码模板文件夹");if(root.Length==0)return;UseSource(SourceWorkspace.Open(root));
    }
    static string? FindCode()
    {
        var paths=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs/Microsoft VS Code/Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft VS Code/Code.exe")};
        return paths.FirstOrDefault(File.Exists);
    }
    void OpenSourceInEditor()
    {
        if(sourceWorkspace==null)return;var code=FindCode();
        if(code!=null)Process.Start(new ProcessStartInfo(code,"--new-window "+Quote(sourceWorkspace.Root)){UseShellExecute=false});
        else Process.Start(new ProcessStartInfo(sourceWorkspace.Root){UseShellExecute=true});
    }
    string? FindUnity()
    {
        var candidates=new List<string>();string? environment=Environment.GetEnvironmentVariable("UNITY_EDITOR");if(environment!=null)candidates.Add(environment);
        foreach(var file in new[]{Path.Combine(CustomTools,"local/custom-toolchain.json"),sourceWorkspace==null?"":Path.Combine(sourceWorkspace.Root,".vfx/local/custom-toolchain.json")})
            try{if(File.Exists(file)){var saved=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(file));if(saved!=null&&saved.TryGetValue("UnityEditor",out var editor))candidates.Add(editor);}}catch{}
        foreach(var drive in Directory.GetLogicalDrives())candidates.Add(Path.Combine(drive,"Unity",Application.unityVersion,"Editor/Unity.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Unity/Hub/Editor",Application.unityVersion,"Editor/Unity.exe"));
        foreach(var path in candidates)try{if(File.Exists(path)&&FileVersionInfo.GetVersionInfo(path).ProductVersion?.StartsWith(Application.unityVersion,StringComparison.Ordinal)==true)return path;}catch{}
        return null;
    }
    void BuildSourceWorkspace()
    {
        if(sourceWorkspace==null||sourceBuild is {HasExited:false})return;
        string? unity=FindUnity();if(unity==null){unity=openDialog("选择 Unity "+Application.unityVersion+" 的 Unity.exe","exe");if(unity.Length==0)return;}
        if(FileVersionInfo.GetVersionInfo(unity).ProductVersion?.StartsWith(Application.unityVersion,StringComparison.Ordinal)!=true)throw new InvalidOperationException("Unity 版本与预览不一致。");
        var start=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File "+Quote(Path.Combine(sourceWorkspace.Root,".vfx/Build.ps1"))+" -UnityEditor "+Quote(unity)) {
            WorkingDirectory=sourceWorkspace.Root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        sourceBuild?.Dispose();sourceBuild=Process.Start(start);sourceMessage="正在构建，完成后自动更新预览…";
    }
    void TickSourceWorkspace()
    {
        if(sourceBuild is {HasExited:true}){sourceMessage=sourceBuild.ExitCode==0?"构建完成，正在更新…":"构建失败；打开 .vfx/build-status.json 查看原因。";sourceBuild.Dispose();sourceBuild=null;}
        if(!autoSourceUpdate||sourceWorkspace==null||Model==null||Surface==null||BlocksHostInput||Model.IsEditing||Time.realtimeSinceStartupAsDouble<sourcePoll)return;
        sourcePoll=Time.realtimeSinceStartupAsDouble+.5;
        try {
            if(File.Exists(sourceWorkspace.StatusFile)) {
                string stateText=File.ReadAllText(sourceWorkspace.StatusFile);
                if(stateText!=sourceStatusFingerprint) {
                    var state=Newtonsoft.Json.Linq.JObject.Parse(stateText);string? name=(string?)state["State"];
                    if(name=="building")sourceMessage="正在构建，完成后自动更新预览…";
                    if(name=="failed")sourceMessage="构建失败，原效果保留："+(string?)state["Error"];
                    sourceStatusFingerprint=stateText;
                }
            }
            if(!File.Exists(sourceWorkspace.LatestFile))return;
            string fingerprint=File.ReadAllText(sourceWorkspace.LatestFile);if(fingerprint==sourceFingerprint)return;
            var file=sourceWorkspace.LatestDescriptor();if(file==null)return;
            Surface.ReleaseSource(sourceWorkspace.Root);
            var definition=Model.Library.Import(file,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            Model.Library.LinkSource(definition,sourceWorkspace.Root,folder);
            int count=Model.ReloadSource(sourceWorkspace.Root,definition);sourceFingerprint=fingerprint;
            sourceMessage=count>0?$"已更新 {count} 个实例；匹配参数动画保留，可撤销。":"构建已加入效果库；拖入时间轴开始使用。";
            status=sourceMessage;
        }catch(Exception e){sourceMessage="更新失败，保留原效果："+e.Message;}
    }
    void DrawSourceControls(float w,ref float y)
    {
        var theme=ui!;theme.Rule(R(16,y,w-32,1));y+=10;theme.Label(R(16,y,w-32,25),"源码工程",15,"#c9e9ee");y+=32;
        theme.Paragraph(R(16,y,w-32,36),sourceWorkspace==null?"从效果库新建或打开模板。":Path.GetFileName(sourceWorkspace.Root),12);y+=42;
        if(sourceWorkspace!=null) {
            if(theme.Button(R(16,y,w-32,27),autoSourceUpdate?"构建后自动更新：开":"构建后自动更新：关",autoSourceUpdate,false,12)){autoSourceUpdate=!autoSourceUpdate;Queue(SaveSourcePreferences);}y+=35;
        }
        theme.Paragraph(R(16,y,w-32,58),sourceMessage,12);y+=66;
    }
}
