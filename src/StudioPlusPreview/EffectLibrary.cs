using InFalsus.Presentation;
using Newtonsoft.Json;

namespace InFalsus.Vfx.StudioPlus;

public sealed class EffectParameter
{
    public string Property { get; set; }="";
    public string Label { get; set; }="";
    public ParameterKind Kind { get; set; }
    public double[] Value { get; set; }=new[]{0.0};
    public double Min { get; set; }
    public double Max { get; set; }=1;
}
public sealed class EffectDefinition
{
    public string Id { get; set; }="";
    public string Label { get; set; }="";
    public string Target { get; set; }="background";
    public string Bundle { get; set; }="preview.bundle";
    public string Material { get; set; }="vfx/parameter-preview";
    public bool Original { get; set; }
    public string Category { get; set; }="";
    public string[] Keywords { get; set; }=Array.Empty<string>();
    public EffectParameter[] Parameters { get; set; }=Array.Empty<EffectParameter>();
}
public sealed partial class EffectLibrary
{
    public List<EffectDefinition> Effects { get; set; }=new();
    public List<string> Warnings {get;}=new();
    public static EffectDefinition Background => new() {Id="builtin",Label="通用背景",Parameters=new[]{
        P("_Brightness","亮度",1,0,3),new EffectParameter{Property="_Tint",Label="颜色",Kind=ParameterKind.Color,Value=new[]{1.0,1.0,1.0,1.0},Max=2},
        P("_Distortion","UV 位移强度",.15,0,1),P("_Frequency","空间频率",6,1,24),P("_Speed","时间频率",.3,0,2),
        P("_OffsetX","水平偏移",0,-1,1),P("_OffsetY","垂直偏移",0,-1,1),P("_PatternMix","内置图案混合",1,0,1)}};
    public static EffectDefinition Utility => new(){Id="utility",Label="通用调整",Target="postprocess",Material="vfx/utility-post",Parameters=new[]{
        new EffectParameter{Property="_BaseColor",Label="Base Color",Kind=ParameterKind.Color,Value=new[]{1.0,1.0,1.0,1.0},Max=2},
        P("_Exposure","曝光",0,-3,3),P("_Contrast","对比度",1,0,2),P("_Saturation","饱和度",1,0,2),
        P("_Zoom","缩放",1,.25,3),P("_Rotation","旋转",0,-3.14,3.14),P("_Chroma","径向色散",0,0,.05),
        P("_Ripple","径向波动",0,0,.1),P("_RippleFrequency","波动频率",20,1,60),P("_RippleSpeed","波动速度",2,0,10),
        P("_Scanline","扫描线",0,0,1),P("_Noise","噪声",0,0,1),P("_PixelSize","像素化",1,1,80),
        P("_Vignette","暗角",0,0,1),P("_Fade","淡出",0,0,1),P("_Wipe","径向遮罩",0,0,1),
        P("_Ring","环形光效",0,0,2),P("_RingRadius","光环半径",.3,0,1),P("_RingWidth","光环宽度",.015,.001,.15)}};
    static IEnumerable<EffectDefinition> BuiltinEffects()
    {
        var utility=Utility;
        EffectDefinition Part(string id,string name,string[] properties,string? strength=null,double initial=0) {
            var parameters=utility.Parameters.Where(p=>properties.Contains(p.Property)).Select(p=>new EffectParameter {
                Property=p.Property,Label=p.Label,Kind=p.Kind,Value=p.Property==strength?new[]{initial}:p.Value.ToArray(),Min=p.Min,Max=p.Max }).ToArray();
            return new EffectDefinition{Id=id,Label=name,Category="通用",Target="postprocess",Material=utility.Material,Parameters=parameters};
        }
        yield return Part("color-grade","色彩与曝光",new[]{"_BaseColor","_Exposure","_Contrast","_Saturation"});
        yield return Part("transform","画面变换",new[]{"_Zoom","_Rotation"});
        yield return Part("chroma","径向色散",new[]{"_Chroma"},"_Chroma",.012);
        yield return Part("ripple","径向波动",new[]{"_Ripple","_RippleFrequency","_RippleSpeed"},"_Ripple",.025);
        yield return Part("scanline","扫描线",new[]{"_Scanline"},"_Scanline",.2);
        yield return Part("noise","噪声",new[]{"_Noise"},"_Noise",.1);
        yield return Part("pixelate","像素化",new[]{"_PixelSize"},"_PixelSize",6);
        yield return Part("vignette","暗角",new[]{"_Vignette"},"_Vignette",.4);
        yield return Part("fade","淡出",new[]{"_Fade"});
        yield return Part("wipe","径向遮罩",new[]{"_Wipe"});
        yield return Part("ring","环形光效",new[]{"_Ring","_RingRadius","_RingWidth"},"_Ring",1);
    }
    internal static EffectParameter Opacity()=>P("_VfxOpacity","混合强度",1,0,1);
    internal static void Prepare(EffectDefinition effect)
    {
        if(effect.Target=="postprocess"&&!effect.Parameters.Any(p=>p.Property=="_VfxOpacity"))
            effect.Parameters=effect.Parameters.Append(Opacity()).ToArray();
        foreach(var parameter in effect.Parameters)if(parameter.Property.StartsWith("_KW_",StringComparison.Ordinal))parameter.Min=-1;
    }
    static EffectParameter P(string name,string label,double value,double min,double max)=>new(){Property=name,Label=label,Value=new[]{value},Min=min,Max=max};
    public static EffectLibrary Load(string folder)
    {
        var library=new EffectLibrary();library.Effects.Add(Background);library.Effects.Add(Utility);
        library.Effects.AddRange(BuiltinEffects());
        var file=Path.Combine(folder,"Effects/effects.json");
        if(File.Exists(file)) {
            var external=JsonConvert.DeserializeObject<EffectLibrary>(File.ReadAllText(file),new JsonSerializerSettings{MaxDepth=24,TypeNameHandling=TypeNameHandling.None})!;
            foreach(var effect in external.Effects) {
                if(!effect.Original||!effect.Bundle.StartsWith("Effects/",StringComparison.Ordinal)||effect.Parameters.Length>256)
                    throw new InvalidDataException("无效的本地效果目录。");
                EffectPackage.ResolveAsset(folder,effect.Bundle);
                library.Effects.Add(effect);
            }
        }
        library.LoadCustom(folder);
        foreach(var effect in library.Effects)Prepare(effect);
        return library;
    }
    public EffectDefinition Get(string material)=>Effects.First(e=>e.Material==material);
    public EffectDefinition Resolve(MaterialNodeDocument node)=>(node.EffectId.Length>0?Effects.FirstOrDefault(e=>e.Id==node.EffectId):
        Effects.FirstOrDefault(e=>e.Material==node.Material&&e.Bundle==node.Bundle))??throw new InvalidDataException("缺少效果资源，请先导入："+node.EffectId);
}
