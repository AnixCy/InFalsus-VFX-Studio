using InFalsus.Presentation;
using System.Text.RegularExpressions;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class EffectLibrary
{
    internal const string BackgroundCapability="effect.background-controls";
    void AddBackgroundControls()
    {
        var originals=Effects.Where(e=>e.Original&&e.Target=="background").ToArray();
        // Recovered variant-group metadata for this legacy shader family; export is generic.
        var recursive=originals.FirstOrDefault(e=>e.Id=="official-09-background");
        if(recursive!=null)recursive.ExclusiveKeywordGroups=new[]{new[]{"USE_FEEDBACK","USE_CHROMA_ZOOM","USE_SLASH_DISPLACE","USE_STATIC_NOISE"}};
        if(originals.Length==0)return;
        foreach(var source in originals)Effects.Add(new EffectDefinition{Id="bg-preset-"+source.Id.Split('-')[1],Label=source.Label,Category="预设",Target="postprocess",
            Bundle=source.Bundle,Material=source.Material,BackgroundSource=source.Id,Parameters=source.Parameters.Where(p=>!p.Property.StartsWith("_KW_",StringComparison.Ordinal)).Select(Copy).ToArray()});
        var preferred=new Dictionary<string,string>{["_EphemeralAmount"]="09",["_ChromaZoomAmount"]="09",["_FeedbackAmount"]="09",["_StaticNoiseAmount"]="09",["_SlashDisplaceAmount"]="09",
            ["_ImpactAmount"]="04",["_TriDisplaceAmount"]="04",["_Hnoise"]="10"};
        var related=new Dictionary<string,string[]>{["_FeedbackAmount"]=new[]{"_FeedbackRotation"},["_HMoveAmount"]=new[]{"_HMoveSeed"},
            ["_RippleAmount"]=new[]{"_RippleBeat"},["_VerticalCorruptionAmount"]=new[]{"_VerticalCorruptionPosition"}};
        var names=new Dictionary<string,string>{["_EphemeralAmount"]="Ephemeral / 流场扭曲",["_FeedbackAmount"]="Feedback / 嵌套缩放旋转",
            ["_ChromaZoomAmount"]="Background Chromatic Zoom",["_SlashDisplaceAmount"]="Slash Displacement",["_StaticNoiseAmount"]="Static Noise",
            ["_RushAmount"]="Radial Rush",["_CellAmount"]="Cell Pattern",["_AuraAmount"]="Aura",["_HMoveAmount"]="Horizontal Displacement",
            ["_TriDisplaceAmount"]="Triangle Displacement",["_ImpactAmount"]="Background Impact",["_CrashAmount"]="Background Fracture",
            ["_VerticalCorruptionAmount"]="Vertical Corruption",["_HackerTransitionAmount"]="Network Transition",["_BlurCell"]="Soft Cells",
            ["_BlurDistort"]="Soft Distortion",["_Monochrome"]="Monochrome",["_Hnoise"]="Background Horizontal Noise",["_ChugAmount"]="Directional Chug Vector"};
        var skip=new HashSet<string>{"_BaseColor","_FeedbackRotation","_HMoveSeed","_RippleBeat","_VerticalCorruptionPosition"};
        var properties=originals.SelectMany(e=>e.Parameters).Where(p=>!p.Property.StartsWith("_KW_",StringComparison.Ordinal)&&!skip.Contains(p.Property)).Select(p=>p.Property).Distinct().ToArray();
        foreach(var property in properties) {
            var source=originals.FirstOrDefault(e=>preferred.TryGetValue(property,out var family)&&e.Id=="official-"+family+"-background")??originals.First(e=>e.Parameters.Any(p=>p.Property==property));
            var chosen=new HashSet<string>((related.TryGetValue(property,out var extra)?extra:Array.Empty<string>()).Append(property));
            var parameters=source.Parameters.Where(p=>chosen.Contains(p.Property)).Select(Copy).ToArray();var primary=parameters.Single(p=>p.Property==property);
            if(primary.Kind==ParameterKind.Float){primary.Value=new[]{property=="_EphemeralAmount"?.5:.4};if(property=="_EphemeralAmount")primary.Max=2;}
            else primary.Value=new[]{.4,0.0,0.0,0.0};
            if(property=="_FeedbackAmount")parameters.Single(p=>p.Property=="_FeedbackRotation").Value=new[]{.2};
            primary.Label="Strength";
            string label=names.TryGetValue(property,out var name)?name:Regex.Replace(property.TrimStart('_').Replace("Amount",""),"([a-z])([A-Z])","$1 $2");
            Effects.Add(new EffectDefinition{Id="bg-control-"+property.TrimStart('_').ToLowerInvariant(),Label=label,Category="效果",Target="postprocess",Bundle=source.Bundle,
                Material=source.Material,BackgroundSource=source.Id,StrengthProperty=property,Parameters=parameters});
        }
    }
}
