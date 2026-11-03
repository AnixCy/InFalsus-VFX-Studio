using Newtonsoft.Json;

namespace InFalsus.Vfx.StudioPlus;
internal sealed class SourceWorkspace
{
    public int FormatVersion {get;set;}=1;
    public string Name {get;set;}="My Effect";
    public string Shader {get;set;}="Effect.shader";
    public string Descriptor {get;set;}="effect.json";
    public int CompilerVersion {get;set;}
    [JsonIgnore] internal string Root="";
    internal static SourceWorkspace Open(string root)
    {
        root=Path.GetFullPath(root);var file=Path.Combine(root,"workspace.vfxsource.json");
        if(new FileInfo(file).Length>32*1024)throw new InvalidDataException("模板工程描述过大。");
        var workspace=JsonConvert.DeserializeObject<SourceWorkspace>(File.ReadAllText(file))??throw new InvalidDataException("无效的模板工程。");
        if(workspace.FormatVersion!=1)throw new InvalidDataException("模板工程版本不支持。");
        InFalsus.Presentation.EffectPackage.ResolveAsset(root,workspace.Shader);
        InFalsus.Presentation.EffectPackage.ResolveAsset(root,workspace.Descriptor);
        workspace.Root=root;return workspace;
    }
    internal static SourceWorkspace Create(string root,string tools)
    {
        root=Path.GetFullPath(root);if(Directory.Exists(root)||File.Exists(root))throw new IOException("目标目录已存在；不会覆盖已有源码。");
        Directory.CreateDirectory(root);var workspace=new SourceWorkspace{Root=root,Name=Path.GetFileName(root),CompilerVersion=2};
        Directory.CreateDirectory(Path.Combine(root,".vfx/scripts"));Directory.CreateDirectory(Path.Combine(root,".vfx/templates/custom-effect"));
        File.Copy(Path.Combine(tools,"templates/custom-effect/ProceduralPulse.shader"),Path.Combine(root,"Effect.shader"));
        File.Copy(Path.Combine(tools,"templates/custom-effect/effect.json"),Path.Combine(root,"effect.json"));
        var descriptor=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(root,"effect.json")));
        descriptor["Effect"]!["Id"]="source-"+Guid.NewGuid().ToString("N");descriptor["Effect"]!["Label"]=workspace.Name;
        descriptor["AutoParameters"]=true;descriptor["Effect"]!["Parameters"]=new Newtonsoft.Json.Linq.JArray();
        File.WriteAllText(Path.Combine(root,"effect.json"),descriptor.ToString(Formatting.Indented));
        File.Copy(Path.Combine(tools,"templates/custom-effect/BuildCustomEffect.cs"),Path.Combine(root,".vfx/templates/custom-effect/BuildCustomEffect.cs"));
        File.Copy(Path.Combine(tools,"scripts/Build-CustomEffect.ps1"),Path.Combine(root,".vfx/scripts/Build-CustomEffect.ps1"));
        File.WriteAllText(Path.Combine(root,"workspace.vfxsource.json"),JsonConvert.SerializeObject(workspace,Formatting.Indented));
        File.WriteAllText(Path.Combine(root,".vfx/Build.ps1"),BuildScript);
        File.WriteAllText(Path.Combine(root,"Build.cmd"),"@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0.vfx\\Build.ps1\"\r\npause\r\n");
        Directory.CreateDirectory(Path.Combine(root,".vscode"));
        File.WriteAllText(Path.Combine(root,".vscode/tasks.json"),JsonConvert.SerializeObject(new{
            version="2.0.0",tasks=new[]{new{label="Build VFX",type="process",command="powershell.exe",
                args=new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File","${workspaceFolder}/.vfx/Build.ps1"},
                group=new{kind="build",isDefault=true},problemMatcher=Array.Empty<string>(),runOptions=new{instanceLimit=1}}}},Formatting.Indented));
        File.WriteAllText(Path.Combine(root,"README.txt"),"Edit Effect.shader in any editor and save. The VFX editor builds and updates it automatically.\r\nDeclare animatable Float, Range, Color and Vector fields in Shader Properties; labels, defaults and ranges are discovered automatically.\r\nCompiler errors keep the previous working preview. Matching animation parameters survive reload.\r\nAdvanced: effect.json controls effect identity/target; AutoParameters=false enables manual parameter descriptions. Build.cmd or VS Code Ctrl+Shift+B can build without the VFX editor.\r\n.vfx/cache and .vfx/build are generated compiler files and revisions.\r\n");
        return workspace;
    }
    internal void UpgradeCompiler(string tools)
    {
        if(CompilerVersion>=2)return;
        string backup=Path.Combine(Root,".vfx/tool-backups",DateTime.UtcNow.Ticks.ToString());Directory.CreateDirectory(backup);
        foreach(var pair in new[]{("scripts/Build-CustomEffect.ps1","scripts/Build-CustomEffect.ps1"),("templates/custom-effect/BuildCustomEffect.cs","templates/custom-effect/BuildCustomEffect.cs")}) {
            string target=Path.Combine(Root,".vfx",pair.Item1);if(File.Exists(target))File.Copy(target,Path.Combine(backup,Path.GetFileName(target)));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Path.Combine(tools,pair.Item2),target,true);
        }
        string descriptor=Path.Combine(Root,Descriptor);var json=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(descriptor));
        if(json["AutoParameters"]==null){File.Copy(descriptor,Path.Combine(backup,"effect.json"));json["AutoParameters"]=true;File.WriteAllText(descriptor,json.ToString());}
        CompilerVersion=2;File.WriteAllText(Path.Combine(Root,"workspace.vfxsource.json"),JsonConvert.SerializeObject(this,Formatting.Indented));
    }
    internal string LatestFile=>Path.Combine(Root,".vfx/latest-build.json");
    internal string StatusFile=>Path.Combine(Root,".vfx/build-status.json");
    internal bool HasCurrentBuild=>File.Exists(LatestFile)&&SourceFiles().All(p=>File.GetLastWriteTimeUtc(p)<=File.GetLastWriteTimeUtc(LatestFile));
    internal string[] SourceFiles()
    {
        var shader=Path.Combine(Root,Shader);return Directory.GetFiles(Path.GetDirectoryName(shader)!).Where(p=>Path.GetExtension(p).ToLowerInvariant() is ".hlsl" or ".cginc")
            .Append(shader).Append(Path.Combine(Root,Descriptor)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
    }
    internal string? LatestDescriptor()
    {
        if(!File.Exists(LatestFile))return null;
        var latest=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(LatestFile));
        if(latest==null||!latest.TryGetValue("Descriptor",out var relative))throw new InvalidDataException("构建结果描述无效。");
        return InFalsus.Presentation.EffectPackage.ResolveAsset(Root,relative);
    }
    internal const string BuildScript=@"
param([string]$UnityEditor=$env:UNITY_EDITOR)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskProject=Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'workspace.vfxsource.json') -Encoding UTF8|ConvertFrom-Json
$taskLock=$null
try{$taskLock=[IO.File]::Open((Join-Path $PSScriptRoot 'build.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Another build is already running.'}
$taskStatus=Join-Path $PSScriptRoot 'build-status.json'
try {
 @{State='building';StartedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath $taskStatus -Encoding UTF8
 $taskVersion=[DateTime]::UtcNow.Ticks.ToString()
 $taskOutput=Join-Path $PSScriptRoot ('build/'+$taskVersion)
 & (Join-Path $PSScriptRoot 'scripts/Build-CustomEffect.ps1') -ShaderPath (Join-Path $taskRoot $taskProject.Shader) -DescriptorPath (Join-Path $taskRoot $taskProject.Descriptor) -OutputDirectory $taskOutput -WorkDirectory (Join-Path $PSScriptRoot 'cache') -ReuseProject -UnityEditor $UnityEditor | Out-File -LiteralPath (Join-Path $PSScriptRoot 'build-output.txt') -Encoding UTF8
 if(-not(Test-Path -LiteralPath (Join-Path $taskOutput 'build-ok.txt'))){throw 'Build did not produce a valid effect.'}
 @{Descriptor=('.vfx/build/'+$taskVersion+'/effect.json')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'latest-build.json') -Encoding UTF8
 @{State='ready';Output=$taskOutput}|ConvertTo-Json|Set-Content -LiteralPath $taskStatus -Encoding UTF8
}catch {
 @{State='failed';Error=$_.Exception.Message}|ConvertTo-Json|Set-Content -LiteralPath $taskStatus -Encoding UTF8
 throw
}finally{if($taskLock){$taskLock.Dispose()}}
";
}
