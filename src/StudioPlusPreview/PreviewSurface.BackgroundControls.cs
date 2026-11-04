using InFalsus.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace InFalsus.Vfx.StudioPlus;
internal sealed partial class PreviewSurface
{
    sealed class BackgroundFrame
    {
        internal MaterialNodeDocument Node=null!,Input=null!;
        internal EffectDefinition Source=null!,InputDefinition=null!;
        internal BackgroundMaterialPlan.State State=null!;
    }
    BackgroundFrame? backgroundFrame;
    BackgroundMaterialPlan? backgroundPlan;
    PackageDocument? backgroundPlanDocument;
    internal int BackgroundControlPasses=>backgroundFrame==null?0:1;
    internal string ActiveBackgroundGroup=>backgroundFrame?.Source.Id??"";
    internal int BackgroundGroupConflicts {get;private set;}
    BackgroundFrame? ResolveBackgroundControls(PreviewModel model,double seconds)
    {
        if(!ReferenceEquals(backgroundPlanDocument,model.RenderDocument)) {
            backgroundPlanDocument=model.RenderDocument;backgroundPlan=model.Library.BackgroundPlan(model.RenderDocument);
        }
        var state=backgroundPlan!.Sample(seconds,model.Values);BackgroundGroupConflicts=backgroundPlan.Conflicts;
        if(state==backgroundPlan.Base)return null;
        var source=model.Library.Effects.Single(e=>e.Id==state!.Definition.SourceId);
        var original=model.RenderDocument.Nodes.Single(n=>n.Target=="background");
        return new BackgroundFrame{Source=source,Input=original,InputDefinition=model.Definition(original),State=state!,
            Node=new MaterialNodeDocument{Id=original.Id,Target="background",Bundle=source.Bundle,Material=source.Material,EffectId=source.Id,Enabled=original.Enabled,
                StartSeconds=original.StartSeconds,EndSeconds=original.EndSeconds}};
    }
    Texture BackgroundInputTexture()
    {
        if(imageOverride)return ownedTexture!;
        var definition=backgroundFrame!.InputDefinition;
        if(!definition.Original)return Texture2D.whiteTexture;
        if(!bundles.TryGetValue(definition.Bundle,out var bundle)) {
            bundle=AssetBundle.LoadFromFile(EffectPackage.ResolveAsset(folder,definition.Bundle));
            if(!bundle)throw new InvalidDataException("无法加载旧工程底图资源。");bundles.Add(definition.Bundle,bundle);
        }
        return bundle.LoadAsset<Material>(definition.Material).GetTexture("_BaseMap")??Texture2D.whiteTexture;
    }
    void ApplyBackgroundControls(PreviewModel model)
    {
        if(backgroundFrame==null||!resources.TryGetValue("background",out var resource))return;
        var state=backgroundFrame.State;var material=resource.Material;
        for(int i=0;i<state.Values.Length;i++) {
            var p=state.Definition.Properties[i];if(p.Property.StartsWith("_KW_",StringComparison.Ordinal))continue;
            var v=state.Values[i].Number;
            if(p.Kind==ParameterKind.Color)material.SetColor(Id(p.Property),new Color(v.X,v.Y,v.Z,v.W));
            else if(p.Kind==ParameterKind.Vector)material.SetVector(Id(p.Property),new Vector4(v.X,v.Y,v.Z,v.W));
            else material.SetFloat(Id(p.Property),v.X);
        }
        for(int i=0;i<state.Keywords.Length;i++) {
            var local=new LocalKeyword(material.shader,state.Definition.Keywords[i]);if(local.isValid)material.SetKeyword(local,state.Keywords[i]);
        }
        ApplyBackgroundTexture();
    }
}
