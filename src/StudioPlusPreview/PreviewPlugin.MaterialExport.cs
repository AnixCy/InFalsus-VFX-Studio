using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using InFalsus.Vfx.Addon;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    AuthoringProjectDocument MaterialExportProject(AuthoringProjectDocument project)
    {
        // The old compact builtin path remains unchanged.
        if(!project.Runtime.Nodes.Any(n=>n.Enabled&&(Model!.Definition(n).BackgroundSource.Length>0||n.Target=="background"&&n.Material!="vfx/parameter-preview")))return project;
        foreach(var node in project.Runtime.Nodes.Where(n=>n.Target=="background"||n.Enabled&&Model!.Definition(n).BackgroundSource.Length>0))
            node.BackgroundBinding=Model!.Library.BackgroundBinding(Model.Definition(node));
        project.Runtime.Requires=project.Runtime.Requires.Append(BackgroundMaterialPlan.Capability).Distinct().ToArray();
        return project;
    }
    BackgroundExportContext? MaterialExportContext(AuthoringProjectDocument project,string colorSpace)
    {
        if(!project.Runtime.Nodes.Any(n=>n.BackgroundBinding!=null))return null;
        return new BackgroundExportContext(folder,node=>{
            var effect=Model!.Definition(node);
            if(effect.Id=="builtin")return new BackgroundExportAsset(Path.Combine(folder,"GameExport",colorSpace,"background.bundle"),AddonPackage.MaterialPath);
            if(effect.Original||effect.BackgroundSource.Length>0)return new BackgroundExportAsset(EffectPackage.ResolveAsset(folder,node.Bundle),node.Material);
            return CompileBackgroundSource(effect,colorSpace);
        });
    }
    sealed class GameToolchain {public string UnityEditor="";}
    BackgroundExportAsset CompileBackgroundSource(EffectDefinition effect,string colorSpace)
    {
        if(effect.SourceDirectory.Length==0)throw new InvalidDataException("自定义背景需要关联 Shader 源码，才能生成匹配游戏的资源。请通过“打开效果”链接源码模板；编译包仍可用于预览。");
        var workspace=SourceWorkspace.Open(effect.SourceDirectory);
        if(!workspace.HasCurrentBuild||sourceBuild!=null)throw new InvalidDataException("请等待 Shader 预览构建成功后再导出；失败或尚未完成的源码不会替换当前预览。");
        string descriptor=workspace.LatestDescriptor()??throw new InvalidDataException("自定义背景缺少已成功构建的源码版本。");
        var latest=JsonConvert.DeserializeObject<CustomEffectDocument>(File.ReadAllText(descriptor))!;
        string previewBundle=EffectPackage.ResolveAsset(Path.GetDirectoryName(descriptor)!,latest.Effect.Bundle);
        if(AddonPackage.Hash(previewBundle)!=AddonPackage.Hash(EffectPackage.ResolveAsset(folder,effect.Bundle)))throw new InvalidDataException("预览尚未更新到最新 Shader；请等待更新后再导出。");
        string script=Path.Combine(CustomTools,"scripts/Build-GameAddonAssets.ps1");
        if(!File.Exists(script))throw new FileNotFoundException("创作工具缺少游戏材质构建器；请更新完整编辑器。",script);
        string fingerprint=SourceChanges.Fingerprint(workspace);
        using var hash=SHA256.Create();string key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(fingerprint+colorSpace+"6000.3.9f1"+AddonPackage.Hash(script)+AddonPackage.Hash(Path.Combine(CustomTools,"templates/game-addon/BuildAddonAssets.cs"))))).Replace("-","").ToLowerInvariant();
        string cache=Path.Combine(folder,"GameExport/Compiled",key),bundle=Path.Combine(cache,"background.bundle"),receipt=Path.Combine(cache,"export-ok.json");
        BackgroundExportAsset Asset(){var info=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Path.Combine(cache,"build-info.json")))!;return new(bundle,AddonPackage.MaterialPath,info.TryGetValue("textureProperty",out var property)?property:"_MainTex");}
        if(File.Exists(receipt)&&File.Exists(bundle)&&JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(receipt))!.TryGetValue("sha256",out var previous)&&previous==AddonPackage.Hash(bundle))return Asset();
        string preference=Path.Combine(folder,"GameExport/toolchain.json"),unity="";
        if(File.Exists(preference))unity=JsonConvert.DeserializeObject<GameToolchain>(File.ReadAllText(preference))?.UnityEditor??"";
        if(!File.Exists(unity)) {
            string previewPreference=Path.Combine(CustomTools,"local/custom-toolchain.json");
            if(File.Exists(previewPreference)) {
                string preview=JsonConvert.DeserializeObject<GameToolchain>(File.ReadAllText(previewPreference))?.UnityEditor??"";
                if(File.Exists(preview))unity=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(preview)!)!)!,"6000.3.9f1/Editor/Unity.exe");
            }
        }
        if(!File.Exists(unity))unity=openDialog("选择 Unity 6000.3.9f1 的 Unity.exe","exe");
        if(!File.Exists(unity)||!FileVersionInfo.GetVersionInfo(unity).ProductVersion.StartsWith("6000.3.9f1",StringComparison.Ordinal))throw new InvalidDataException("游戏背景构建需要 Unity 6000.3.9f1；已有导出和源码不变。");
        Directory.CreateDirectory(Path.GetDirectoryName(preference)!);File.WriteAllText(preference,JsonConvert.SerializeObject(new GameToolchain{UnityEditor=unity}));
        status="正在构建游戏背景材质；后续相同源码将复用缓存…";
        Directory.CreateDirectory(cache);
        var start=new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File "+Quote(script)+" -GenericMaterial -UnityEditor "+Quote(unity)+" -ColorSpace "+colorSpace+" -ShaderSource "+Quote(Path.Combine(workspace.Root,workspace.Shader))+" -OutputDirectory "+Quote(cache))
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)};
        using var process=Process.Start(start)??throw new IOException("游戏材质构建器未能启动。");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(180000)){process.Kill();throw new IOException("游戏材质构建超时；原工程和现有导出未变。");}
        string log=output.GetAwaiter().GetResult()+error.GetAwaiter().GetResult();
        if(process.ExitCode!=0||!File.Exists(bundle))throw new InvalidDataException("游戏背景构建失败："+log.Substring(Math.Max(0,log.Length-1600)));
        if(SourceChanges.Fingerprint(workspace)!=fingerprint)throw new IOException("构建期间 Shader 源码发生变化，请等待预览更新后重试。");
        File.WriteAllText(receipt,JsonConvert.SerializeObject(new Dictionary<string,string>{{"sha256",AddonPackage.Hash(bundle)}}));
        return Asset();
    }
}
