using InFalsus.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
internal sealed partial class PreviewSurface
{
    sealed class BackgroundFrame
    {
        internal MaterialNodeDocument Node=null!,Input=null!;
        internal EffectDefinition Source=null!,InputDefinition=null!;
        internal MaterialNodeDocument[] Operators=Array.Empty<MaterialNodeDocument>();
    }
    BackgroundFrame? backgroundFrame;
    internal int BackgroundControlPasses=>backgroundFrame==null?0:1;
    internal string ActiveBackgroundGroup=>backgroundFrame?.Source.Id??"";
    internal int BackgroundGroupConflicts {get;private set;}
    BackgroundFrame? ResolveBackgroundControls(PreviewModel model,double seconds)
    {
        var active=model.RenderDocument.Nodes.Where(n=>model.Definition(n).BackgroundSource.Length>0&&model.IsActive(n,seconds)&&model.Opacity(n.Id)>0).ToArray();
        BackgroundGroupConflicts=Math.Max(0,active.Select(n=>model.Definition(n).BackgroundSource).Distinct().Count()-1);
        if(active.Length==0)return null;
        var source=model.Library.Effects.Single(e=>e.Id==model.Definition(active[^1]).BackgroundSource);
        var original=model.RenderDocument.Nodes.Single(n=>n.Target=="background");
        return new BackgroundFrame{Source=source,Input=original,InputDefinition=model.Definition(original),
            Node=new MaterialNodeDocument{Id=original.Id,Target="background",Bundle=source.Bundle,Material=source.Material,EffectId=source.Id,Enabled=original.Enabled,
                StartSeconds=original.StartSeconds,EndSeconds=original.EndSeconds},
            Operators=active.Where(n=>model.Definition(n).BackgroundSource==source.Id).ToArray()};
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
        var state=backgroundFrame;var values=state.Source.Parameters.ToDictionary(p=>p.Property,p=>PreviewModel.Number(p.Value));
        for(int i=0;i<model.Values.Length;i++) {
            var p=model.Catalog.Parameters[i];if(p.Binding.Node=="background"&&values.ContainsKey(p.Binding.Property))values[p.Binding.Property]=model.Values[i];
        }
        // Carry the independent image tint/brightness when the image-only material is the source.
        if(!state.InputDefinition.Original) {
            NVector4 tint=NVector4.One;float brightness=1;
            for(int i=0;i<model.Values.Length;i++) {var p=model.Catalog.Parameters[i];if(p.Binding.Node!="background")continue;
                if(p.Binding.Property=="_Tint")tint=model.Values[i].Number;if(p.Binding.Property=="_Brightness")brightness=model.Values[i].Number.X;}
            values["_BaseColor"]=new ParameterValue(new NVector4(tint.X*brightness,tint.Y*brightness,tint.Z*brightness,tint.W));
        }
        string lastMain="";
        foreach(var node in state.Operators) {
            var definition=model.Definition(node);float opacity=model.Opacity(node.Id);
            for(int i=0;i<model.Values.Length;i++) {
                var p=model.Catalog.Parameters[i];if(p.Binding.Node!=node.Id||p.Binding.Property=="_VfxOpacity")continue;
                var value=model.Values[i];
                if(p.Binding.Property==definition.StrengthProperty||definition.StrengthProperty.Length==0) {
                    var neutral=state.Source.Parameters.FirstOrDefault(v=>v.Property==p.Binding.Property);
                    if(neutral!=null)value=new ParameterValue(NVector4.Lerp(PreviewModel.Number(neutral.Value).Number,value.Number,opacity));
                }
                values[p.Binding.Property]=value;
            }
            if(state.Source.Id=="official-09-background") {
                string main=definition.StrengthProperty switch{"_FeedbackAmount"=>"USE_FEEDBACK","_ChromaZoomAmount"=>"USE_CHROMA_ZOOM","_SlashDisplaceAmount"=>"USE_SLASH_DISPLACE","_StaticNoiseAmount"=>"USE_STATIC_NOISE",_=>""};
                if(main.Length>0&&values.TryGetValue(definition.StrengthProperty,out var amount)&&Math.Abs(amount.Number.X)>.000001)lastMain=main;
            }
        }
        var material=resource.Material;
        if(state.Source.Id=="official-09-background") {
            string[] primary={"_FeedbackAmount","_ChromaZoomAmount","_SlashDisplaceAmount","_StaticNoiseAmount"};
            if(primary.Count(p=>values.TryGetValue(p,out var v)&&Math.Abs(v.Number.X)>.000001)>1)BackgroundGroupConflicts++;
        }
        foreach(var p in state.Source.Parameters.Where(p=>!p.Property.StartsWith("_KW_",StringComparison.Ordinal)))if(values.TryGetValue(p.Property,out var value)) {
            var v=value.Number;
            if(p.Kind==ParameterKind.Color)material.SetColor(Id(p.Property),new Color(v.X,v.Y,v.Z,v.W));
            else if(p.Kind==ParameterKind.Vector)material.SetVector(Id(p.Property),new Vector4(v.X,v.Y,v.Z,v.W));
            else material.SetFloat(Id(p.Property),v.X);
        }
        foreach(var keyword in state.Source.Keywords) {
            string canonical=keyword.Replace("USE_","").Replace("_","");bool enabled=false;
            foreach(var pair in values)if(!pair.Key.StartsWith("_KW_",StringComparison.Ordinal)&&pair.Key.Replace("_","").Replace("Amount","").ToUpperInvariant()==canonical)enabled|=Math.Abs(pair.Value.Number.X)>.000001;
            foreach(var node in state.Operators) {
                string primary=model.Definition(node).StrengthProperty;if(primary.Length==0||!values.TryGetValue(primary,out var value))continue;
                string name=primary.Replace("_","").Replace("Amount","").ToUpperInvariant();
                if(name==canonical||canonical.Length>=4&&name.StartsWith(canonical,StringComparison.Ordinal))enabled|=value.Number.LengthSquared()>.00000001f;
            }
            if(values.TryGetValue("_KW_"+keyword,out var control)){if(control.Number.X>.5f)enabled=true;else if(control.Number.X<-.5f)enabled=false;}
            if(lastMain.Length>0&&keyword is "USE_FEEDBACK" or "USE_CHROMA_ZOOM" or "USE_SLASH_DISPLACE" or "USE_STATIC_NOISE")enabled&=keyword==lastMain;
            var local=new LocalKeyword(material.shader,keyword);if(local.isValid)material.SetKeyword(local,enabled);
        }
        ApplyBackgroundTexture();
    }
}
