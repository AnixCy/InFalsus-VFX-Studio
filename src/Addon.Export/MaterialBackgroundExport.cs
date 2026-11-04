using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;

namespace InFalsus.Vfx.Addon;

public sealed class BackgroundExportAsset
{
    public string File{get;}public string Material{get;}public string? TextureProperty{get;}
    public BackgroundExportAsset(string file,string material,string? textureProperty=null)=>(File,Material,TextureProperty)=(Path.GetFullPath(file),material,textureProperty);
}
/// <summary>The creator host resolves compatible, precompiled resources. Never enters the player.</summary>
public sealed class BackgroundExportContext
{
    public string AssetRoot{get;}
    readonly Func<MaterialNodeDocument,BackgroundExportAsset> resolve;
    readonly Dictionary<string,BackgroundExportAsset> cached=new(StringComparer.Ordinal);
    internal readonly Dictionary<string,string> Files=new(StringComparer.Ordinal);
    public BackgroundExportContext(string root,Func<MaterialNodeDocument,BackgroundExportAsset> resolve)=>(AssetRoot,this.resolve)=(Path.GetFullPath(root),resolve);
    internal void Bind(MaterialNodeDocument node)
    {
        string key=node.Bundle+":"+node.Material;
        if(!cached.TryGetValue(key,out var asset)){asset=resolve(node);cached.Add(key,asset);}
        if(new FileInfo(asset.File).Length>64L*1024*1024||string.IsNullOrWhiteSpace(asset.Material))throw new InvalidDataException("背景资源包过大或材质入口缺失。");
        string relative="materials/"+AddonPackage.Hash(asset.File).Substring(0,32)+".bundle";
        Files[relative]=asset.File;node.Bundle=relative;node.Material=asset.Material;
        if(asset.TextureProperty!=null)node.BackgroundBinding!.TextureProperty=asset.TextureProperty;
    }
}
internal static class MaterialBackgroundExport
{
    internal static PackageDocument Compile(AuthoringProjectDocument project,AddonTarget target,string colorSpace,BackgroundExportContext context)
    {
        var source=AuthoringProjects.Copy(project).Runtime;
        var background=source.Nodes.Single(n=>n.Target=="background");
        if(!background.Enabled||background.StartSeconds!=0||background.EndSeconds.HasValue||background.BackgroundBinding==null)throw new NotSupportedException("背景材质必须始终启用，并带有导出资源描述。");
        var nodes=new List<MaterialNodeDocument>();
        foreach(var node in source.Nodes.Where(n=>n.Enabled)) {
            if(node.Target=="background"||BackgroundMaterialPlan.IsControl(node)||SceneOpacityPlan.IsControl(node)){nodes.Add(node);continue;}
            // A neutral default grading item is a no-op, not an implicit extra rendering stage.
            if(node.EffectId=="color-grade"&&source.Channels.Where(c=>c.Node==node.Id).All(NeutralGrade))continue;
            throw new NotSupportedException("此项需要额外渲染阶段，尚未进入背景材质导出："+(node.Name.Length>0?node.Name:node.EffectId)+"。请停用该项；背景 Shader 参数可直接导出。");
        }
        foreach(var node in nodes.Where(n=>!SceneOpacityPlan.IsControl(n)))context.Bind(node);
        foreach(var node in nodes.Where(SceneOpacityPlan.IsControl))node.Bundle=background.Bundle;
        source.Nodes=nodes.ToArray();source.Channels=source.Channels.Where(c=>nodes.Any(n=>n.Id==c.Node)).ToArray();
        source.Id="addon-"+project.ProjectId;source.SongKey=target.SongKey;source.Difficulty=target.Difficulty;source.ChartFingerprint=target.ChartFingerprint;
        source.UnityVersion="6000.3.9f1";source.Platform="StandaloneWindows64";source.ColorSpace=colorSpace;
        source.Requires=new[]{"clock.chart","clock.audio","background.material","shader.bundle","effect.instances",BackgroundMaterialPlan.Capability}
            .Concat(nodes.Any(SceneOpacityPlan.IsControl)?new[]{SceneOpacityPlan.Capability}:Array.Empty<string>()).ToArray();
        _=new BackgroundMaterialPlan(source);return source;
    }
    static bool NeutralGrade(ChannelDocument c)
    {
        if(c.Property=="_VfxOpacity")return true;
        double neutral=c.Property switch{"_BaseColor"=>1,"_Contrast"=>1,"_Saturation"=>1,"_Exposure"=>0,_=>double.NaN};
        return !double.IsNaN(neutral)&&c.Default.All(v=>v==neutral)&&c.Keys.All(k=>k.Value.All(v=>v==neutral));
    }
}
