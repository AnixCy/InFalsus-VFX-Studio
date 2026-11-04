using InFalsus.Presentation;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewModel
{
    public PackageDocument RenderDocument=>draftTimeline?.Document??Package.Document;
    public EffectDefinition Definition(MaterialNodeDocument node)=>Library.Resolve(node);
    public string NodeLabel(MaterialNodeDocument node){var definition=Definition(node);return node.Name.Length>0&&node.Name!=definition.LegacyLabel?node.Name:definition.Label;}
    double ParameterTime(string id,double seconds)
    {
        var node=Document.Runtime.Nodes.Single(n=>n.Id==Catalog.Get(id).Binding.Node);
        return node.Target=="postprocess"?Math.Clamp(seconds,node.StartSeconds,node.EndSeconds??double.MaxValue):seconds;
    }
    public bool TrackVisible(string id)=>Keys.Any(k=>k.ParameterId==id)||id is "adjust/_BaseColor" or "adjust/_Exposure"||SceneOpacityPlan.Properties.Any(p=>id.EndsWith("/"+p,StringComparison.Ordinal));
    static ChannelDocument[] Channels(string node,EffectDefinition definition)=>definition.Parameters.Select(p=>new ChannelDocument {
        Node=node,Property=p.Property,Kind=p.Kind,Default=p.Value.ToArray()}).ToArray();
    void Graph(string label,Action<PackageDocument> change)
    {
        Commit();var graph=Session.Snapshot().Runtime;change(graph);
        graph.Requires=graph.Requires.Append("effect.instances").Distinct().ToArray();
        using var edit=Session.BeginEdit(Session.Revision,label);edit.ReplaceGraph(graph,Hints(graph,Library));edit.Commit();Refresh();
    }
    public string AddEffect(EffectDefinition definition,double start,int index=-1)
    {
        if(definition.Target=="background"){SetBackground(definition);return "background";}
        string id="fx_"+Guid.NewGuid().ToString("N");start=Math.Max(0,Math.Round(start,3));
        Graph("添加效果",graph=>{
            var nodes=graph.Nodes.ToList();var node=new MaterialNodeDocument{Id=id,Target="postprocess",Bundle=definition.Bundle,
                Material=definition.Material,EffectId=definition.Id,Name=definition.Label,StartSeconds=start,EndSeconds=start+5};
            int at=index<0?nodes.Count:Math.Clamp(index,1,nodes.Count);nodes.Insert(at,node);graph.Nodes=nodes.ToArray();
            graph.Channels=graph.Channels.Concat(Channels(id,definition)).ToArray();
            if(definition.BackgroundSource.Length>0)graph.Requires=graph.Requires.Append(EffectLibrary.BackgroundCapability).Distinct().ToArray();
            if(definition.Material==SceneOpacityPlan.Material)graph.Requires=graph.Requires.Append(SceneOpacityPlan.Capability).Distinct().ToArray();
        });return id;
    }
    public void SetBackground(EffectDefinition definition)
    {
        if(definition.Target!="background")throw new ArgumentException("Not a background material.");
        Graph("替换底图材质",graph=>{
            var node=graph.Nodes.Single(n=>n.Target=="background");node.Material=definition.Material;node.Bundle=definition.Bundle;
            node.EffectId=definition.Id;node.Name=definition.Label;
            var channels=Channels(node.Id,definition);
            foreach(var c in channels) {
                var old=graph.Channels.FirstOrDefault(p=>p.Node==node.Id&&p.Property==c.Property&&p.Kind==c.Kind);
                if(old!=null){c.Default=old.Default.ToArray();c.Keys=old.Keys.Select(CloneKey).ToArray();}
            }
            graph.Channels=graph.Channels.Where(c=>c.Node!=node.Id).Concat(channels).ToArray();
        });
    }
    public void RemoveEffect(string id)=>Graph("删除效果",graph=>{
        if(graph.Nodes.Single(n=>n.Id==id).Target=="background")throw new InvalidOperationException("底图保留；可替换材质或恢复底图。");
        graph.Nodes=graph.Nodes.Where(n=>n.Id!=id).ToArray();graph.Channels=graph.Channels.Where(c=>c.Node!=id).ToArray();
    });
    public void EnableEffect(string id,bool enabled)=>Graph("切换效果",graph=>graph.Nodes.Single(n=>n.Id==id).Enabled=enabled);
    public void RenameEffect(string id,string name)=>Graph("重命名效果",graph=>graph.Nodes.Single(n=>n.Id==id).Name=name.Trim());
    public void ReorderEffect(string id,int step)=>Graph("调整合成顺序",graph=>{
        var nodes=graph.Nodes.ToList();int old=nodes.FindIndex(n=>n.Id==id);
        if(old<=0)return;var node=nodes[old];nodes.RemoveAt(old);nodes.Insert(Math.Clamp(old+step,1,nodes.Count),node);graph.Nodes=nodes.ToArray();
    });
    public void PreviewEffectRange(string id,double start,double? end,bool moveKeys)
    {
        Begin("effect:"+id);drag!.SetNodeRange(id,Math.Round(start,3),end.HasValue?Math.Round(end.Value,3):null,moveKeys);draftTimeline=drag.CompilePreview();
    }
    public void SetEffectRange(string id,double start,double? end,bool moveKeys=false)
    {PreviewEffectRange(id,start,end,moveKeys);Commit();}
    public int ReloadSource(string directory,EffectDefinition definition)
    {
        var ids=Document.Runtime.Nodes.Where(n=>string.Equals(Definition(n).SourceDirectory,directory,StringComparison.OrdinalIgnoreCase)).Select(n=>n.Id).ToArray();
        if(ids.Length==0)return 0;
        Graph("更新自定义效果",graph=>{
            foreach(var node in graph.Nodes.Where(n=>ids.Contains(n.Id))) {
                var old=Definition(node);var channels=Channels(node.Id,definition);
                foreach(var channel in channels) {
                    var previous=graph.Channels.FirstOrDefault(c=>c.Node==node.Id&&c.Property==channel.Property&&c.Kind==channel.Kind);
                    if(previous!=null){channel.Default=previous.Default.ToArray();channel.Keys=previous.Keys.Select(CloneKey).ToArray();}
                }
                graph.Channels=graph.Channels.Where(c=>c.Node!=node.Id).Concat(channels).ToArray();
                if(node.Name==old.Label)node.Name=definition.Label;
                node.Bundle=definition.Bundle;node.Material=definition.Material;node.EffectId=definition.Id;
            }
        });return ids.Length;
    }
    public bool IsActive(MaterialNodeDocument node,double seconds)=>node.Enabled&&seconds>=node.StartSeconds&&(!node.EndSeconds.HasValue||seconds<node.EndSeconds.Value);
    public float Opacity(string nodeId)
    {
        int index=Catalog.Parameters.ToList().FindIndex(p=>p.Id==nodeId+"/_VfxOpacity");
        return index<0?1:Math.Clamp(Values[index].Number.X,0,1);
    }
}
