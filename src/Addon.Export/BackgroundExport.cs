using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;

namespace InFalsus.Vfx.Addon;

/// <summary>Explicit, bounded first export target. It never edits the source project or chart.</summary>
public static class BackgroundExport
{
    static readonly HashSet<string> BackgroundProperties = new() {
        "_Brightness","_Tint","_Distortion","_Frequency","_Speed","_OffsetX","_OffsetY","_PatternMix" };
    static readonly HashSet<string> GradeProperties = new() { "_BaseColor","_Exposure","_Contrast","_Saturation","_VfxOpacity" };

    public static PackageDocument Compile(AuthoringProjectDocument project, AddonTarget target, string colorSpace)
    {
        if(string.IsNullOrWhiteSpace(target.SongKey)||target.Difficulty<0||target.Difficulty>3
            ||!target.SongKey.StartsWith("game:",StringComparison.Ordinal)&&string.IsNullOrWhiteSpace(target.ChartFingerprint))
            throw new InvalidDataException("请选择有效目标谱面；自制谱必须具有指纹。");
        if(colorSpace is not ("Gamma" or "Linear"))throw new InvalidDataException("目标色彩空间无效。");
        var source=AuthoringProjects.Copy(project).Runtime;
        var background=source.Nodes.SingleOrDefault(n=>n.Target=="background")??throw new InvalidDataException("缺少背景。");
        if(background.Bundle!="preview.bundle"||background.Material!="vfx/parameter-preview"||!background.Enabled
            ||background.StartSeconds!=0||background.EndSeconds.HasValue)
            throw new NotSupportedException("当前游戏导出只支持始终启用的通用背景；不支持原始或自定义背景材质。");
        var enabled=source.Nodes.Where(n=>n.Enabled&&n.Target!="background").ToArray();
        if(enabled.Length>1||enabled.Any(n=>n.Bundle!="preview.bundle"||n.Material!="vfx/utility-post"||n.EffectId!="color-grade"))
            throw new NotSupportedException("当前游戏导出只支持通用背景和一个“色彩与曝光”；请停用其他效果后导出。");
        var channels=new List<ChannelDocument>();
        foreach(var channel in source.Channels.Where(c=>c.Node==background.Id)) {
            if(!BackgroundProperties.Contains(channel.Property))throw new NotSupportedException("尚不支持背景参数："+channel.Property);
            channel.Node="background";channels.Add(channel);
        }
        if(enabled.Length==1) {
            var grade=enabled[0];
            foreach(var channel in source.Channels.Where(c=>c.Node==grade.Id)) {
                if(!GradeProperties.Contains(channel.Property))throw new NotSupportedException("尚不支持色彩参数："+channel.Property);
                channel.Node="background";if(channel.Property=="_VfxOpacity")channel.Property="_GradeOpacity";
                channels.Add(channel);
            }
            channels.Add(Constant("_GradeStart",grade.StartSeconds));channels.Add(Constant("_GradeEnd",grade.EndSeconds??1e20));
            channels.Add(Constant("_GradeEnabled",1));
        } else channels.Add(Constant("_GradeEnabled",0));
        return new PackageDocument {
            Id="addon-"+project.ProjectId,SongKey=target.SongKey,Difficulty=target.Difficulty,ChartFingerprint=target.ChartFingerprint,
            TimeDomain=source.TimeDomain,OffsetSeconds=source.OffsetSeconds,Seed=source.Seed,Tempo=source.Tempo,
            UnityVersion="6000.3.9f1",Platform="StandaloneWindows64",ColorSpace=colorSpace,
            Requires=AddonPackage.Capabilities.Where(c=>c!=AddonPackage.ImageCapability).ToArray(),
            Nodes=new[]{new MaterialNodeDocument{Id="background",Target="background",Bundle="background.bundle",Material=AddonPackage.MaterialPath}},
            Channels=channels.ToArray()
        };
    }
    static ChannelDocument Constant(string name,double value)=>new(){Node="background",Property=name,Default=new[]{value}};

    /// <summary>Build beside the destination, then publish in one rename. Existing outputs are never replaced.</summary>
    public static string Publish(AuthoringProjectDocument project,AddonTarget target,string backgroundImage,string bundle,
        string colorSpace,string destination,Func<object,string> encode)
    {
        destination=Path.GetFullPath(destination);
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("导出目标已存在，请选择新目录。");
        // Reject unsupported graphs before touching output or reading media.
        var runtime=Compile(project,target,colorSpace);
        if(!File.Exists(bundle))throw new FileNotFoundException("缺少匹配游戏的预编译背景资源。",bundle);
        var parent=Path.GetDirectoryName(destination)!;Directory.CreateDirectory(parent);
        var staging=Path.Combine(parent,".vfx-export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try {
            var manifest=new AddonManifest();
            File.Copy(bundle,Path.Combine(staging,"background.bundle"));manifest.Files.Add("background.bundle",AddonPackage.Hash(Path.Combine(staging,"background.bundle")));
            if(backgroundImage.Length>0) {
                if(new FileInfo(backgroundImage).Length>16*1024*1024)throw new InvalidDataException("背景图片超过 16 MiB。");
                var ext=Path.GetExtension(backgroundImage).ToLowerInvariant();
                if(ext is not (".png" or ".jpg" or ".jpeg"))throw new NotSupportedException("背景只支持 PNG/JPEG。");
                ImageHeader.Validate(File.ReadAllBytes(backgroundImage));
                var relative="background"+ext;File.Copy(backgroundImage,Path.Combine(staging,relative));
                manifest.Files.Add(relative,AddonPackage.Hash(Path.Combine(staging,relative)));
                runtime.Nodes[0].Textures.Add("image","file:"+relative);
                runtime.Channels=runtime.Channels.Append(new ChannelDocument{Node="background",Property="_MainTex",Kind=ParameterKind.Choice,
                    DefaultChoice="image"}).ToArray();runtime.Requires=runtime.Requires.Append(AddonPackage.ImageCapability).ToArray();
            }
            EffectPackage.FromDocument(runtime,staging,AddonPackage.Capabilities);
            var runtimePath=Path.Combine(staging,manifest.Runtime);WriteDocument(runtimePath,encode(runtime));
            manifest.Files.Add(manifest.Runtime,AddonPackage.Hash(runtimePath));
            WriteDocument(Path.Combine(staging,"addon.json"),encode(manifest));
            Directory.Move(staging,destination);return Path.Combine(destination,"addon.json");
        } finally {
            if(Directory.Exists(staging)) {
                var cleanup=Path.GetFullPath(staging);
                if(Path.GetDirectoryName(cleanup)!=parent||!Path.GetFileName(cleanup).StartsWith(".vfx-export-",StringComparison.Ordinal)
                    ||(File.GetAttributes(cleanup)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Refusing cleanup outside the owned staging directory.");
                Directory.Delete(cleanup,true);
            }
        }
    }
    static void WriteDocument(string path,string text)
    {
        if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("导出编码器没有生成有效内容。");
        if(System.Text.Encoding.UTF8.GetByteCount(text)>AddonPackage.MaximumDocumentBytes)
            throw new InvalidDataException("导出文档超过玩家端 4 MiB 限制；请精简关键帧或提高烘焙容差。");
        File.WriteAllText(path,text,new System.Text.UTF8Encoding(false));
    }
}
