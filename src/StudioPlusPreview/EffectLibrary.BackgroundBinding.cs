using InFalsus.Presentation;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class EffectLibrary
{
    internal BackgroundBindingDocument BackgroundBinding(EffectDefinition definition)
    {
        var source=definition.BackgroundSource.Length==0?definition:Effects.Single(e=>e.Id==definition.BackgroundSource);
        return new(){SourceId=source.Id,StrengthProperty=definition.BackgroundSource.Length==0?"":definition.StrengthProperty,
            TextureProperty=source.Original?"_BaseMap":"_MainTex",Native=source.Original,Keywords=source.Keywords.ToArray(),
            ExclusiveKeywordGroups=source.ExclusiveKeywordGroups.Select(g=>g.ToArray()).ToArray(),
            Properties=source.Parameters.Where(p=>p.Property!="_VfxOpacity").Select(p=>new BackgroundPropertyDocument{Property=p.Property,Kind=p.Kind,Value=p.Value.ToArray()}).ToArray()};
    }
    internal BackgroundMaterialPlan BackgroundPlan(PackageDocument source)
    {
        // Derived render metadata; never write it into the artist's project.
        var doc=new PackageDocument{Nodes=source.Nodes.Where(n=>n.Target=="background"||Resolve(n).BackgroundSource.Length>0).Select(n=>new MaterialNodeDocument{
            Id=n.Id,Target=n.Target,Bundle=n.Bundle,Material=n.Material,Enabled=n.Enabled,StartSeconds=n.StartSeconds,EndSeconds=n.EndSeconds,BackgroundBinding=BackgroundBinding(Resolve(n))}).ToArray(),
            Channels=source.Channels,Requires=new[]{BackgroundMaterialPlan.Capability}};
        return new BackgroundMaterialPlan(doc);
    }
}
