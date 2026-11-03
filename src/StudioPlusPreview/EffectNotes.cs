using Newtonsoft.Json;

namespace InFalsus.Vfx.StudioPlus;
internal sealed class EffectNote
{
    public string Summary {get;set;}="";
    public string Details {get;set;}="";
    public string Caution {get;set;}="";
    [JsonIgnore] internal bool Known=true;
    string? text;
    [JsonIgnore] internal string Text=>text??=string.Join("\n",new[]{Summary,Details,Caution}.Where(s=>s.Length>0));
}
internal static class EffectNotes
{
    sealed class Family {public string Background="";public string Postprocess="";}
    sealed class Catalog
    {
        public int SchemaVersion {get;set;}
        public Dictionary<string,EffectNote> Effects=new(),BackgroundControls=new(),CustomMaterials=new();
        public Dictionary<string,string> Parameters=new();
        public Dictionary<string,Family> PresetFamilies=new();
        public Dictionary<string,Dictionary<string,string>> ParameterOverrides=new();
    }
    static readonly Catalog Data=Load();
    static readonly Dictionary<EffectDefinition,EffectNote> Cache=new();
    static readonly Dictionary<EffectDefinition,string> HelpCache=new();
    static Catalog Load()
    {
        using var stream=typeof(EffectNotes).Assembly.GetManifestResourceStream("InFalsus.Vfx.EffectNotes.json")
            ??throw new InvalidDataException("Missing effect reference data.");
        using var text=new StreamReader(stream);var data=JsonConvert.DeserializeObject<Catalog>(text.ReadToEnd())!;
        if(data.SchemaVersion!=1)throw new InvalidDataException("Unsupported effect reference data.");return data;
    }
    internal static string Scope(EffectDefinition effect)=>effect.Target=="background"||effect.BackgroundSource.Length>0?"底图材质 · 只改变背景":"后处理 · 处理前面已合成的预览画面";
    internal static EffectNote For(EffectDefinition effect)
    {
        if(Cache.TryGetValue(effect,out var found))return found;
        EffectNote note;
        if(Data.Effects.TryGetValue(effect.Id,out found))note=found;
        else if(effect.StrengthProperty.Length>0&&Data.BackgroundControls.TryGetValue(effect.StrengthProperty,out found))note=found;
        else if(Data.CustomMaterials.TryGetValue(effect.Material,out found))note=found;
        else {
            string source=effect.BackgroundSource.Length>0?effect.BackgroundSource:effect.Id;
            string family=source.Split('-').ElementAtOrDefault(1)??"";
            bool background=effect.Target=="background"||effect.BackgroundSource.Length>0;
            note=Data.PresetFamilies.TryGetValue(family,out var preset)?new EffectNote{
                Summary=background?preset.Background:preset.Postprocess,
                Details="组合预设保留多组参数；各参数可独立打关键帧。",
                Caution=background?"跨原始材质组存在组合限制；辅助贴图仍来自该材质。":"具体空间遮罩与响应由所选材质决定。"}
                :new EffectNote{Known=false,Summary="自定义 Shader 效果。",Details="画面表现由源码决定；当前没有可核实的内置说明，请打开源码确认。"};
        }
        Cache.Add(effect,note);return note;
    }
    internal static bool HasParameter(string property)=>property.StartsWith("_KW_",StringComparison.Ordinal)||property=="_VfxOpacity"||Data.Parameters.ContainsKey(property);
    internal static string Help(EffectDefinition effect)
    {
        if(!HelpCache.TryGetValue(effect,out var text)){text=Scope(effect)+"\n"+For(effect).Text;HelpCache.Add(effect,text);}return text;
    }
    internal static string Parameter(EffectDefinition effect,string property)
    {
        if(!For(effect).Known)return "自定义参数；具体含义以 Shader 源码为准。";
        if(Data.ParameterOverrides.TryGetValue(effect.Material,out var overrides)&&overrides.TryGetValue(property,out var specific))return specific;
        if(property.StartsWith("_KW_",StringComparison.Ordinal))return "离散分支开关：自动按 Amount 判断，也可关闭或开启。渐变请调整强度参数。";
        if(property=="_VfxOpacity")return effect.BackgroundSource.Length>0?"对底图控制参数与其中性值插值；不会新增一层图像混合 Pass。":"0 绕过该效果，1 完全应用；中间值把处理结果与输入画面混合。";
        if(Data.Parameters.TryGetValue(property,out var text))return text;
        return "自定义参数；具体含义以 Shader 源码为准。";
    }
}
