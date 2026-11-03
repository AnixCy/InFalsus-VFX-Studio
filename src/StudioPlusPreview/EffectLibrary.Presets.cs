using InFalsus.Presentation;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class EffectLibrary
{
    static readonly Dictionary<string,string> ParameterNames=new() {
        ["_CrashAmount"]="Screen Fracture",["_BlockGlitchAmount"]="Block Glitch",["_ChromaZoomAmount"]="Chromatic Zoom",
        ["_GlitchAmount"]="Digital Glitch",["_WarpAmount"]="UV Warp",["_WarpZoomAmount"]="Warp Zoom",
        ["_HnoiseAmount"]="Horizontal Noise",["_ScanlineAmount"]="Scanline Strength",["_BlurGlitch"]="Blur Glitch",
        ["_ChaosAmount"]="Chaos Distortion",["_ChaosPhase"]="Pattern Phase",["_ChaosKick"]="Kick Intensity",
        ["_EphemeralAmount"]="Transient Distortion",["_MalfunctionAmount"]="Signal Malfunction",
        ["_FeedbackAmount"]="Recursive UV Strength",["_FeedbackRotation"]="Recursive Rotation",
        ["_VerticalCorruptionAmount"]="Vertical Corruption",["_VerticalCorruptionPosition"]="Corruption Position",
        ["_BaseColor"]="Base Color",["_VignetteColor"]="Vignette Color"
    };
    static void NamePreset(EffectDefinition e)
    {
        e.LegacyLabel=e.Label;
        string family=e.Id.Split('-').ElementAtOrDefault(1)??"";
        string[] background={"Radial Rush / Cells","Aura / Horizontal Shift","Triangle Displacement / Impact","Radial Ripple / Rush",
            "Geometric Pattern Warp","Vertical Corruption / Impact","Network / Soft Cells","Luminance / Ripple",
            "Recursive UV / Chromatic Zoom","Directional Rush / Color Mix","Monochrome / Triangle Warp","Radial Rush / Cells II"};
        string[] post={"Screen Fracture / Vignette","Block Glitch / Fracture","Warp / Chromatic Zoom / Glitch","Chromatic Zoom / Blocks",
            "Signal Malfunction / Fracture","Digital Glitch / Fracture","Scanlines / Blur Glitch","Screen Fracture / Vignette II",
            "Digital Glitch / Fracture II","Block Glitch / Fracture II","Warp Zoom / Chaos / Glitch","Chromatic Zoom / Fracture"};
        if(int.TryParse(family,out var n)&&n>=1&&n<=12)e.Label=e.Target=="background"?background[n-1]:post[n-1];
        foreach(var p in e.Parameters)if(ParameterNames.TryGetValue(p.Property,out var name))p.Label=name;
    }
    static EffectParameter Copy(EffectParameter p)=>new(){Property=p.Property,Label=p.Label,Kind=p.Kind,Value=p.Value.ToArray(),Min=p.Min,Max=p.Max,Hidden=p.Hidden};
    void AddCuratedEffects()
    {
        void Add(string id,string label,string family,string property,double initial,string[]? related=null,string? keyword=null) {
            var source=Effects.Single(e=>e.Id=="official-"+family+"-postprocess");
            var visible=new HashSet<string>((related??Array.Empty<string>()).Append(property));
            string canonical=(keyword??property.TrimStart('_').Replace("Amount","")).Replace("_","").ToUpperInvariant();
            var parameters=source.Parameters.Select(Copy).ToArray();
            foreach(var p in parameters) {
                p.Hidden=!visible.Contains(p.Property);
                if(p.Property.StartsWith("_KW_",StringComparison.Ordinal)) {
                    string k=p.Property.Substring(4).Replace("USE_","").Replace("_","");
                    p.Value=new[]{k==canonical?1.0:-1.0};
                }else if(p.Property=="_BaseColor")p.Value=new[]{1.0,1.0,1.0,1.0};
                else if(!visible.Contains(p.Property))p.Value=p.Kind==ParameterKind.Float?new[]{0.0}:new[]{0.0,0.0,0.0,0.0};
                if(p.Property==property){p.Value=new[]{initial};p.Label="Strength";}
            }
            Effects.Add(new EffectDefinition{Id="effect-"+id,Label=label,Category="效果",Target="postprocess",Original=true,
                Bundle=source.Bundle,Material=source.Material,Keywords=source.Keywords.ToArray(),Parameters=parameters});
        }
        if(!Effects.Any(e=>e.Id=="official-01-postprocess"))return;
        Add("fracture","Screen Fracture","01","_CrashAmount",.25);
        Add("block-glitch","Block Glitch","02","_BlockGlitchAmount",.35);
        Add("chromatic-zoom","Chromatic Zoom","03","_ChromaZoomAmount",.3);
        Add("uv-warp","UV Warp","03","_WarpAmount",.3);
        Add("digital-glitch","Digital Glitch","06","_GlitchAmount",.35);
        Add("scanlines","Signal Scanlines","07","_ScanlineAmount",.4);
        Add("blur-glitch","Blur Glitch","07","_BlurGlitch",.4,keyword:"GLITCH");
        Add("warp-zoom","Warp Zoom","11","_WarpZoomAmount",.3);
        Add("horizontal-noise","Horizontal Noise","11","_HnoiseAmount",.25);
        Add("chaos","Chaos Distortion","11","_ChaosAmount",.3,new[]{"_ChaosPhase","_ChaosKick"});
        Add("malfunction","Signal Malfunction","05","_MalfunctionAmount",.3);
    }
}
