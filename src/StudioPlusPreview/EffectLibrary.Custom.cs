using System.Security.Cryptography;
using System.Text.RegularExpressions;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

public sealed class CustomEffectDocument
{
    public int FormatVersion {get;set;}=1;
    public string UnityVersion {get;set;}="";
    public string Platform {get;set;}="StandaloneWindows64";
    public string ColorSpace {get;set;}="Linear";
    public EffectDefinition Effect {get;set;}=new();
}
public sealed partial class EffectLibrary
{
    static readonly JsonSerializerSettings CustomJson=new(){MaxDepth=24,TypeNameHandling=TypeNameHandling.None,Formatting=Formatting.Indented,
        Converters={new StringEnumConverter{AllowIntegerValues=false}}};
    static CustomEffectDocument ReadCustom(string file)
    {
        if(new FileInfo(file).Length>256*1024)throw new InvalidDataException("效果描述超过 256 KiB。");
        return JsonConvert.DeserializeObject<CustomEffectDocument>(File.ReadAllText(file),CustomJson)??throw new InvalidDataException("无效的效果描述。");
    }
    static void Validate(CustomEffectDocument document,string version,string colorSpace)
    {
        var e=document.Effect;
        if(document.FormatVersion!=1||document.UnityVersion!=version||document.Platform!="StandaloneWindows64"||document.ColorSpace!=colorSpace)
            throw new InvalidDataException("效果构建版本、平台或色彩空间与当前预览不一致。");
        if(e==null||string.IsNullOrWhiteSpace(e.Id)||e.Id.Length>100||string.IsNullOrWhiteSpace(e.Label)||e.Label.Length>100
            ||e.Target is not ("background" or "postprocess")||string.IsNullOrWhiteSpace(e.Material)||e.Parameters==null||e.Parameters.Length>128
            ||e.Keywords==null||e.Keywords.Length>32||e.Keywords.Any(k=>!Regex.IsMatch(k,@"^[A-Z][A-Z0-9_]{0,95}$")))
            throw new InvalidDataException("效果名称、类型或参数目录无效。");
        if(e.Parameters.Any(p=>p==null||!Regex.IsMatch(p.Property,@"^_[A-Za-z0-9_]{1,95}$")||p.Property.StartsWith("_Vfx",StringComparison.Ordinal)
            ||ParameterDescriptor.IsClockProperty(p.Property)||!Enum.IsDefined(typeof(ParameterKind),p.Kind)||p.Kind==ParameterKind.Choice||p.Value==null||p.Value.Length!=(p.Kind==ParameterKind.Float?1:4)
            ||p.Value.Any(v=>!double.IsFinite(v)||Math.Abs(v)>float.MaxValue)||!double.IsFinite(p.Min)||!double.IsFinite(p.Max)||p.Min>p.Max)
            ||e.Parameters.Select(p=>p.Property).Distinct().Count()!=e.Parameters.Length)
            throw new InvalidDataException("效果参数需为有限的 Float、Vector 或 Color，且名称唯一。");
    }
    void LoadCustom(string folder)
    {
        var directory=Path.Combine(folder,"UserEffects");if(!Directory.Exists(directory))return;
        foreach(var file in Directory.GetFiles(directory,"effect.json",SearchOption.AllDirectories)) {
            try {
            var document=ReadCustom(file);Validate(document,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
            EffectPackage.ResolveAsset(folder,document.Effect.Bundle);document.Effect.Original=false;document.Effect.Category="自定义";
            if(Effects.Any(e=>e.Id==document.Effect.Id))throw new InvalidDataException("重复的自定义效果标识。");
            Effects.Add(document.Effect);
            }catch(Exception e){Warnings.Add(Path.GetFileName(Path.GetDirectoryName(file))+": "+e.Message);}
        }
    }
    public EffectDefinition Import(string file,string folder,string version,string colorSpace)
    {
        var document=ReadCustom(file);Validate(document,version,colorSpace);var effect=document.Effect;
        string bundle=EffectPackage.ResolveAsset(Path.GetDirectoryName(Path.GetFullPath(file))!,effect.Bundle);
        if(new FileInfo(bundle).Length>64*1024*1024)throw new InvalidDataException("自定义效果资源包不能超过 64 MiB。");
        using var digest=SHA256.Create();string hash=BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(bundle)
            .Concat(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(document,CustomJson))).ToArray())).Replace("-","").ToLowerInvariant();
        string id="user-"+hash.Substring(0,20);var existing=Effects.FirstOrDefault(e=>e.Id==id);if(existing!=null)return existing;
        var temporary=AssetBundle.LoadFromFile(bundle);if(!temporary)throw new InvalidDataException("无法加载效果 Bundle。");
        try {
            var asset=temporary.LoadAsset<UnityEngine.Object>(effect.Material);
            var shader=asset is Shader s?s:asset is Material material?material.shader:null;
            if(!shader||!shader!.isSupported)throw new InvalidDataException("效果入口不是受支持的 Shader 或 Material。");
            foreach(var p in effect.Parameters.Where(p=>!p.Property.StartsWith("_KW_",StringComparison.Ordinal))) {
                int index=shader.FindPropertyIndex(p.Property);
                if(index<0)throw new InvalidDataException("Shader 未暴露参数："+p.Property);
                var kind=shader.GetPropertyType(index);
                if(p.Kind==ParameterKind.Color&&kind!=UnityEngine.Rendering.ShaderPropertyType.Color
                    ||p.Kind==ParameterKind.Vector&&kind!=UnityEngine.Rendering.ShaderPropertyType.Vector
                    ||p.Kind==ParameterKind.Float&&kind is not (UnityEngine.Rendering.ShaderPropertyType.Float or UnityEngine.Rendering.ShaderPropertyType.Range or UnityEngine.Rendering.ShaderPropertyType.Int))
                    throw new InvalidDataException("Shader 参数类型不匹配："+p.Property);
            }
        }finally{temporary.Unload(true);}
        string relative="UserEffects/"+id;string destination=Path.Combine(folder,relative);Directory.CreateDirectory(destination);
        string cached=Path.Combine(destination,"effect.bundle");if(!File.Exists(cached))File.Copy(bundle,cached);
        effect.Id=id;effect.Bundle=relative+"/effect.bundle";effect.Original=false;effect.Category="自定义";
        File.WriteAllText(Path.Combine(destination,"effect.json"),JsonConvert.SerializeObject(document,CustomJson));
        Prepare(effect);Effects.Add(effect);return effect;
    }
}
