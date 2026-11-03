using System.Numerics;
using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;

namespace InFalsus.Vfx.StudioPlus;

public sealed class EditorKey
{
    public string ParameterId="",Id="";
    public double Seconds;
    public ParameterValue Value;
    public Easing Ease;
    public CurvePointDocument[]? Curve;
}
public sealed partial class PreviewModel
{
    public static readonly string[] Capabilities={"clock.chart","clock.audio","background.material","shader.bundle","postprocess.blit.experimental","effect.instances",EffectLibrary.BackgroundCapability};
    public AuthoringSession Session {get;private set;}
    public ParameterCatalog Catalog {get;private set;}=null!;
    public EffectPackage Package {get;private set;}=null!;
    public AuthoringProjectDocument Document {get;private set;}=null!;
    public ParameterValue[] Values {get;private set;}=Array.Empty<ParameterValue>();
    public EditorKey[] Keys {get;private set;}=Array.Empty<EditorKey>();
    public EffectLibrary Library {get;}
    readonly string assetRoot;
    ParameterPreviewSession preview=null!;
    AuthoringEdit? drag;
    EffectPackage? draftTimeline;
    string? dragId,editedKey;
    long sequence;
    EditorKey[] missingStarts=Array.Empty<EditorKey>();
    public int MissingAnimationStarts=>missingStarts.Length;
    public static bool IsSwitch(string id)=>id.Contains("/_KW_",StringComparison.Ordinal);
    public bool IsEditing=>drag!=null;
    public string? EditedKey=>editedKey;
    public double? MovingSeconds {get;private set;}
    public ParameterValue? EditedValue {get;private set;}
    public PreviewModel(string assetRoot,string unityVersion,string colorSpace,bool legacy=false)
    {
        this.assetRoot=Path.GetFullPath(assetRoot);
        Library=EffectLibrary.Load(assetRoot);
        var template=Template(unityVersion,colorSpace);
        if(!legacy) {
            var color=Library.Effects.Single(e=>e.Id=="color-grade");
            var node=template.Nodes.Single(n=>n.Id=="adjust");node.EffectId=color.Id;node.Name=color.Label;
            template.Channels=template.Channels.Where(c=>c.Node!="adjust").Concat(Channels(node.Id,color)).ToArray();
        }
        var project=AuthoringProjects.Import(EffectPackage.FromDocument(template,assetRoot,Capabilities,false));
        project.ParameterHints=Hints(project.Runtime,Library);
        Session=new AuthoringSession(project,assetRoot,Capabilities,isSaved:true);Refresh();
    }
    public void Refresh()
    {
        Document=Session.Snapshot();Package=Session.CompileSnapshot(false);
        Catalog=new ParameterCatalog(Package,Document.ParameterHints);
        preview=new ParameterPreviewSession(Catalog,Session.Revision);Values=new ParameterValue[Catalog.Parameters.Count];sequence=0;
        var keys=new List<EditorKey>();
        foreach(var track in Document.Tracks) {
            var c=Document.Runtime.Channels.Single(c=>ParameterDescriptor.MakeId(c.Node,c.Property)==track.ParameterId);
            for(int i=0;i<c.Keys.Length;i++) {var k=c.Keys[i];keys.Add(new EditorKey{ParameterId=track.ParameterId,Id=track.KeyIds[i],
                Seconds=k.Seconds??Package.Tempo.ToSeconds(k.Beat!.Value),Value=Number(k.Value),Ease=k.Ease,Curve=k.Curve?.Select(v=>new CurvePointDocument{X=v.X,Y=v.Y}).ToArray()});}
        }
        Keys=keys.ToArray();
        missingStarts=Catalog.Parameters.Where(p=>!IsSwitch(p.Id)&&p.Binding.Kind!=ParameterKind.Choice)
            .Select(p=>(Parameter:p,Key:Keys.Where(k=>k.ParameterId==p.Id).OrderBy(k=>k.Seconds).FirstOrDefault()))
            .Where(p=>p.Key!=null&&p.Key.Seconds>Document.Runtime.Nodes.Single(n=>n.Id==p.Parameter.Binding.Node).StartSeconds&&p.Key.Ease!=Easing.Hold&&p.Key.Value!=p.Parameter.DefaultValue)
            .Select(p=>p.Key!).ToArray();
    }
    public void Sample(double seconds,bool committedOnly=false)
    {
        if(!committedOnly&&draftTimeline!=null)draftTimeline.Timeline.Sample(seconds,Values);
        else preview.SampleInto(seconds,Values,!committedOnly);
    }
    public void SampleCurve(double seconds,ParameterValue[] values)=>(draftTimeline??Package).Timeline.Sample(seconds,values);
    void Begin(string id)
    {
        if(drag!=null&&dragId!=id)Commit();
        if(drag==null){drag=Session.BeginEdit(Session.Revision,"调整演出");dragId=id;}
    }
    public void UpdateParameter(string id,ParameterValue value)
    {
        Begin(id);drag!.SetDefaultValue(id,value);
        preview.ReplaceOverrides(new PreviewEdit(preview.SessionId,Session.Revision,++sequence,new[]{new PreviewOverride(id,value)}));
    }
    public string UpdateAnimatedParameter(string id,ParameterValue value,double seconds,string? selectedKey,bool autoKey)
    {
        seconds=ParameterTime(id,seconds);
        Begin("value:"+id);
        if(editedKey==null) {
            var existing=Keys.FirstOrDefault(k=>k.ParameterId==id&&(k.Id==selectedKey||Math.Abs(k.Seconds-seconds)<.00001));
            if(existing!=null)editedKey=existing.Id;
            else if(autoKey) {
                seconds=Math.Round(seconds,3);AddAnimationStart(drag!,id,seconds);
                editedKey=drag!.AddKey(id,KeyPosition.Seconds(seconds),value,IsSwitch(id)?Easing.Hold:Easing.Linear);
            }
            else if(Keys.Any(k=>k.ParameterId==id)){Cancel();throw new InvalidOperationException("请选择关键帧，或启用自动关键帧。");}
        }
        if(editedKey!=null)drag!.SetKeyValue(editedKey,value);else drag!.SetDefaultValue(id,value);
        EditedValue=value;draftTimeline=drag!.CompilePreview();return editedKey??"";
    }
    public string AddKey(string id,double seconds)
    {
        Commit();seconds=Math.Round(ParameterTime(id,seconds),3);
        var existing=Keys.FirstOrDefault(k=>k.ParameterId==id&&Math.Abs(k.Seconds-seconds)<.00001);
        if(existing!=null)return existing.Id;
        Sample(seconds);int index=Catalog.Parameters.ToList().FindIndex(p=>p.Id==id);
        using var edit=Session.BeginEdit(Session.Revision,"添加关键帧");
        AddAnimationStart(edit,id,seconds);
        var key=edit.AddKey(id,KeyPosition.Seconds(seconds),Values[index],IsSwitch(id)?Easing.Hold:Easing.Linear);edit.Commit();Refresh();return key;
    }
    void AddAnimationStart(AuthoringEdit edit,string id,double seconds)
    {
        var parameter=Catalog.Parameters.Single(p=>p.Id==id);
        double start=Document.Runtime.Nodes.Single(n=>n.Id==parameter.Binding.Node).StartSeconds;
        if(seconds>start&&!IsSwitch(id)&&parameter.Binding.Kind!=ParameterKind.Choice&&!Keys.Any(k=>k.ParameterId==id))
            edit.AddKey(id,KeyPosition.Seconds(start),parameter.DefaultValue,Easing.Linear);
    }
    public int RepairAnimationStarts()
    {
        Commit();int count=missingStarts.Length;if(count==0)return 0;
        using var edit=Session.BeginEdit(Session.Revision,"补齐连续动画起点");
        foreach(var key in missingStarts)edit.AddKey(key.ParameterId,KeyPosition.Seconds(Document.Runtime.Nodes.Single(n=>n.Id==Catalog.Get(key.ParameterId).Binding.Node).StartSeconds),
            Catalog.Parameters.Single(p=>p.Id==key.ParameterId).DefaultValue,Easing.Linear);
        edit.Commit();Refresh();return count;
    }
    public void MoveKey(string key,double seconds)
    {
        Begin("move:"+key);drag!.MoveKey(key,KeyPosition.Seconds(Math.Max(0,Math.Round(seconds,3))));
        editedKey=key;MovingSeconds=seconds;draftTimeline=drag.CompilePreview();
    }
    public void DeleteKey(string key){Commit();using var e=Session.BeginEdit(Session.Revision,"删除关键帧");e.RemoveKey(key);e.Commit();Refresh();}
    public void SetEase(string key,Easing ease){Commit();using var e=Session.BeginEdit(Session.Revision,"设置缓动");e.SetKeyEase(key,IsSwitch(Keys.Single(k=>k.Id==key).ParameterId)?Easing.Hold:ease);e.Commit();Refresh();}
    public void ClearTrack(string id){Commit();using var e=Session.BeginEdit(Session.Revision,"清空参数轨道");foreach(var k in Keys.Where(k=>k.ParameterId==id))e.RemoveKey(k.Id);e.Commit();Refresh();}
    public void Commit()
    {
        if(drag==null)return;var e=drag;drag=null;dragId=editedKey=null;draftTimeline=null;MovingSeconds=null;EditedValue=null;
        try{e.Commit();}finally{e.Dispose();Refresh();}
    }
    public void Cancel(){drag?.Dispose();drag=null;dragId=editedKey=null;draftTimeline=null;MovingSeconds=null;EditedValue=null;Refresh();}
    public void Undo(){Commit();Session.Undo(Session.Revision);Refresh();}
    public void Redo(){Commit();Session.Redo(Session.Revision);Refresh();}
    public void Reset()
    {
        Commit();using var e=Session.BeginEdit(Session.Revision,"恢复参数默认值");
        foreach(var p in Catalog.Parameters) {
            var node=Document.Runtime.Nodes.Single(n=>n.Id==p.Binding.Node);
            e.SetDefaultValue(p.Id,Number(Library.Resolve(node).Parameters.Single(v=>v.Property==p.Binding.Property).Value));
        }
        e.Commit();Refresh();
    }
    public void SelectEffect(EffectDefinition? effect,string target)
    {
        Commit();var old=Document.Runtime;
        var bg=target=="background"?effect!:Library.Resolve(old.Nodes.Single(n=>n.Target=="background"));
        var pp=target=="postprocess"?effect:old.Nodes.FirstOrDefault(n=>n.Id=="post") is { } p?Library.Resolve(p):null;
        var runtime=Compose(old.UnityVersion,old.ColorSpace,bg,pp);
        runtime.Id=old.Id;runtime.TimeDomain=old.TimeDomain;runtime.OffsetSeconds=old.OffsetSeconds;runtime.Seed=old.Seed;
        runtime.SongKey=old.SongKey;runtime.Difficulty=old.Difficulty;runtime.ChartFingerprint=old.ChartFingerprint;
        runtime.Tempo=old.Tempo.Select(t=>new TempoDocument{Seconds=t.Seconds,Bpm=t.Bpm}).ToArray();
        foreach(var c in runtime.Channels) {
            var previous=old.Channels.FirstOrDefault(x=>x.Node==c.Node&&x.Property==c.Property&&x.Kind==c.Kind);
            if(previous!=null){c.Default=previous.Default.ToArray();c.Keys=previous.Keys.Select(CloneKey).ToArray();}
        }
        using var edit=Session.BeginEdit(Session.Revision,"更换效果材质");edit.ReplaceGraph(runtime,Hints(runtime,Library));edit.Commit();Refresh();
    }
    public void Replace(AuthoringSession session)
    {
        var package=session.CompileSnapshot(false);
        package.ValidateMaterialGraph(package.Document.UnityVersion,"StandaloneWindows64",package.Document.ColorSpace);
        foreach(var node in package.Document.Nodes) {
            var definition=Library.Resolve(node);
            if(definition.Material!=node.Material||definition.Bundle!=node.Bundle)throw new InvalidDataException("效果资源身份不匹配。");
            if(definition.Target!=node.Target)throw new InvalidDataException("效果节点目标不匹配。");
            foreach(var c in package.Document.Channels.Where(c=>c.Node==node.Id))
                if(!definition.Parameters.Any(p=>p.Property==c.Property&&p.Kind==c.Kind))throw new InvalidDataException("不支持的效果参数。");
        }
        Cancel();var document=session.Snapshot();
        if(!string.Equals(Path.GetFullPath(document.AssetRoot),assetRoot,StringComparison.OrdinalIgnoreCase)) {
            document.AssetRoot=assetRoot;
            session=new AuthoringSession(document,assetRoot,Capabilities,isSaved:!session.IsDirty);
        }
        Session=session;Refresh();
    }
    static KeyDocument CloneKey(KeyDocument k)=>new(){Seconds=k.Seconds,Beat=k.Beat,Value=k.Value.ToArray(),Ease=k.Ease,
        Curve=k.Curve?.Select(c=>new CurvePointDocument{X=c.X,Y=c.Y}).ToArray()};
    public static ParameterValue Number(double[] v)=>new(v.Length==1?new Vector4((float)v[0],0,0,0):new Vector4((float)v[0],(float)v[1],(float)v[2],(float)v[3]));
    public static PackageDocument Template(string unityVersion,string colorSpace)=>Compose(unityVersion,colorSpace,EffectLibrary.Background,null);
    static PackageDocument Compose(string version,string colorSpace,EffectDefinition bg,EffectDefinition? post)
    {
        var defs=new List<(string Id,EffectDefinition Def)>{("background",bg)};
        if(post!=null)defs.Add(("post",post));defs.Add(("adjust",EffectLibrary.Utility));
        return new PackageDocument{Id="studio-background-preview",UnityVersion=version,ColorSpace=colorSpace,TimeDomain=TimeDomain.Chart,Requires=Capabilities.Where(c=>c!=EffectLibrary.BackgroundCapability).ToArray(),
            Nodes=defs.Select(d=>new MaterialNodeDocument{Id=d.Id,Target=d.Def.Target,Bundle=d.Def.Bundle,Material=d.Def.Material}).ToArray(),
            Channels=defs.SelectMany(d=>d.Def.Parameters.Select(p=>new ChannelDocument{Node=d.Id,Property=p.Property,Kind=p.Kind,Default=p.Value.ToArray()})).ToArray()};
    }
    static ParameterPresentation[] Hints(PackageDocument document,EffectLibrary library)=>document.Channels.Select(c=>{
        var p=library.Resolve(document.Nodes.Single(n=>n.Id==c.Node)).Parameters.Single(p=>p.Property==c.Property);
        return new ParameterPresentation{Node=c.Node,Property=p.Property,Label=p.Label,Group=c.Node,SuggestedMin=p.Min,SuggestedMax=p.Max};
    }).ToArray();
}
