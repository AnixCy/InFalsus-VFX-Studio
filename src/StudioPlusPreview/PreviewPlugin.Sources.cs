using System.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    SourceWorkspace? sourceWorkspace;
    Process? sourceBuild;
    bool autoSourceUpdate=true,sourceAdvanced;
    readonly SourceChanges sourceChanges=new();
    InFalsus.Presentation.Authoring.AuthoringSession? attachSourceSession;
    string attachSourceRoot="",sourcePhase="尚未选择效果";
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
            if(saved!=null){autoSourceUpdate=saved.AutoUpdate;if(Directory.Exists(saved.Directory)){sourceWorkspace=SourceWorkspace.Open(saved.Directory);sourceWorkspace.UpgradeCompiler(CustomTools);sourceChanges.Reset(SourceChanges.Fingerprint(sourceWorkspace),sourceWorkspace.HasCurrentBuild);sourcePhase=Path.GetFileName(sourceWorkspace.Root);}}
        }}catch(Exception e){Logger.LogWarning("Source workspace preference ignored: "+e.Message);}
    }
    void UseSource(SourceWorkspace workspace)
    {
        workspace.UpgradeCompiler(CustomTools);
        sourceWorkspace=workspace;sourceFingerprint=sourceStatusFingerprint="";sourceMessage="编辑 Effect.shader 并保存；参数从 Properties 自动读取。";sourcePhase="等待保存";sourceChanges.Reset(SourceChanges.Fingerprint(workspace),workspace.HasCurrentBuild);libraryFilter="自定义";
        SaveSourcePreferences();
    }
    void SaveSourcePreferences()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SourcePreferences)!);
        File.WriteAllText(SourcePreferences,JsonConvert.SerializeObject(new SourcePreference{Directory=sourceWorkspace?.Root??"",AutoUpdate=autoSourceUpdate}));
    }
    void CreateSourceWorkspace()
    {CreateSourceWorkspace(false);}
    void CreateBackgroundWorkspace()=>CreateSourceWorkspace(true);
    void CreateSourceWorkspace(bool background)
    {
        string parent=StudioBridge.Folder("选择新模板的父文件夹");if(parent.Length==0)return;
        string root=Path.Combine(parent,"MyEffect");int number=2;while(Directory.Exists(root)||File.Exists(root))root=Path.Combine(parent,"MyEffect"+number++);
        UseSource(SourceWorkspace.Create(root,CustomTools,background));attachSourceSession=Model!.Session;attachSourceRoot=root;OpenSourceInEditor();BuildSourceWorkspace();
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
        if(code!=null)Process.Start(new ProcessStartInfo(code,"--new-window "+Quote(sourceWorkspace.Root)+" --goto "+Quote(Path.Combine(sourceWorkspace.Root,sourceWorkspace.Shader))){UseShellExecute=false});
        else try{Process.Start(new ProcessStartInfo(Path.Combine(sourceWorkspace.Root,sourceWorkspace.Shader)){UseShellExecute=true});}catch{Process.Start(new ProcessStartInfo(sourceWorkspace.Root){UseShellExecute=true});}
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
        sourceBuild?.Dispose();sourceChanges.Attempt();sourceBuild=Process.Start(start);sourcePhase="正在编译…";sourceMessage="正在编译，完成后自动更新预览…";
    }
    void TickSourceWorkspace()
    {
        if(sourceBuild is {HasExited:true}){if(sourceBuild.ExitCode==0)sourceMessage="编译完成，正在更新…";sourceBuild.Dispose();sourceBuild=null;}
        if(sourceWorkspace==null||Model==null||Surface==null||BlocksHostInput||Model.IsEditing||Time.realtimeSinceStartupAsDouble<sourcePoll)return;
        sourcePoll=Time.realtimeSinceStartupAsDouble+.5;
        try {
            sourceChanges.Observe(sourceChanges.Read(sourceWorkspace),Time.realtimeSinceStartupAsDouble);
            bool externalBuilding=false;
            if(File.Exists(sourceWorkspace.StatusFile)) {
                string stateText=File.ReadAllText(sourceWorkspace.StatusFile);
                var sourceState=Newtonsoft.Json.Linq.JObject.Parse(stateText);externalBuilding=(string?)sourceState["State"]=="building";
                if(stateText!=sourceStatusFingerprint) {
                    var state=Newtonsoft.Json.Linq.JObject.Parse(stateText);string? name=(string?)state["State"];
                    if(name=="building"){sourcePhase="正在编译…";sourceMessage="正在编译，完成后自动更新预览…";if(sourceBuild==null)sourceChanges.Attempt();}
                    if(name=="failed"){sourcePhase="编译失败 · 保留原预览";sourceMessage=(string?)state["Error"]??"请查看编译日志。";}
                    sourceStatusFingerprint=stateText;
                }
            }
            if(autoSourceUpdate&&Bridge.AuthoringView&&sourceBuild==null&&!externalBuilding&&sourceChanges.Ready(Time.realtimeSinceStartupAsDouble)&&!TestMode) {
                if(FindUnity()!=null)BuildSourceWorkspace();else{sourcePhase="需设置 Unity 编译器";sourceMessage="展开高级选项，点重新编译并选择匹配的 Unity.exe；之后保存即可自动更新。";sourceChanges.Attempt();}
            }
            if(!File.Exists(sourceWorkspace.LatestFile))return;
            string fingerprint=File.ReadAllText(sourceWorkspace.LatestFile);if(fingerprint==sourceFingerprint)return;
            var file=sourceWorkspace.LatestDescriptor();if(file==null)return;
            Surface.ReleaseSource(sourceWorkspace.Root);
            var definition=Model.Library.Import(file,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            Model.Library.LinkSource(definition,sourceWorkspace.Root,folder);
            int count=Model.ReloadSource(sourceWorkspace.Root,definition);sourceFingerprint=fingerprint;sourcePhase="编译成功 · 预览已更新";
            if(count==0&&ReferenceEquals(attachSourceSession,Model.Session)&&attachSourceRoot==sourceWorkspace.Root){AddLibraryEffect(definition,CurrentSeconds);count=1;}
            attachSourceRoot="";attachSourceSession=null;
            sourceMessage=count>0?$"已更新 {count} 个实例；匹配参数动画保留，可撤销。":"构建已加入效果库；拖入时间轴开始使用。";
            status=sourceMessage;
        }catch(Exception e){sourcePhase="更新失败 · 保留原预览";sourceMessage=e.Message;}
    }
    void DrawSourceControls(float w,ref float y)
    {
        var theme=ui!;theme.Rule(R(16,y,w-32,1));y+=10;theme.Label(R(16,y,w-32,25),"Shader 源码",15,"#c9e9ee");y+=32;
        theme.Paragraph(R(16,y,w-32,36),sourceWorkspace==null?"在自定义页创建 Shader 效果。":Path.Combine(sourceWorkspace.Root,sourceWorkspace.Shader),12);y+=42;
        theme.Paragraph(R(16,y,w-32,58),sourceMessage,12);y+=66;
    }
    void DrawSourceCard(float w)
    {
        var theme=ui!;
        if(theme.Button(R(12,88,(w-30)/2,29),"＋ 新建背景…",false,true,13))Queue(CreateBackgroundWorkspace);
        if(theme.Button(R(18+(w-30)/2,88,(w-30)/2,29),"新建后处理…",false,false,13))Queue(CreateSourceWorkspace);
        if(theme.Button(R(12,123,w-24,29),"打开效果文件夹…",false,false,13))Queue(OpenSourceWorkspace);
        if(sourceWorkspace==null)theme.Paragraph(R(14,163,w-28,48),"创建效果 → 编辑 Shader → 保存。参数自动出现，可直接打关键帧。",12);
        else {
            theme.Label(R(14,160,w-28,24),theme.Fit("当前："+Path.GetFileName(sourceWorkspace.Root),(w-28)*UiScale,13),13,"#c9e9ee");
            if(theme.Button(R(12,190,(w-30)/2,29),"编辑代码",false,true,13))Queue(OpenSourceInEditor);
            if(theme.Button(R(18+(w-30)/2,190,(w-30)/2,29),autoSourceUpdate?"自动更新 ✓":"自动更新",autoSourceUpdate,false,12)){autoSourceUpdate=!autoSourceUpdate;Queue(SaveSourcePreferences);}
            theme.Paragraph(R(14,226,w-28,43),sourcePhase,12);
        }
        float y=sourceWorkspace==null?213:274;
        if(theme.Button(R(12,y,w-24,23),sourceAdvanced?"高级选项 ▴":"高级选项 ▾",false,false,12))sourceAdvanced=!sourceAdvanced;
        if(sourceAdvanced) {
            bool enabled=GUI.enabled;GUI.enabled=enabled&&sourceWorkspace!=null&&sourceBuild==null;
            if(theme.Button(R(12,y+30,(w-30)/2,29),"重新编译",false,false,12))Queue(BuildSourceWorkspace);GUI.enabled=enabled;
            if(theme.Button(R(18+(w-30)/2,y+30,(w-30)/2,29),"导入编译包…",false,false,12))Queue(ImportCustomEffect);
        }
    }
}
