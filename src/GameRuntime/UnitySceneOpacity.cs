extern alias UnityCore;
extern alias Il2CppBase;
using UnityCore::UnityEngine;
using UnityCore::UnityEngine.Rendering;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Object = UnityCore::UnityEngine.Object;

namespace InFalsus.Presentation.Game;

public interface ISceneOpacityBindings
{
    void BeginOpacityFrame();
    void ApplySceneOpacity(in SceneOpacityValues values);
    void RestoreSceneOpacity();
    void BeforeUiRender();
    void AfterUiRender();
}

/// <summary>Presentation-only ownership: native C2D alpha for HUD, private transparent materials for track surfaces.
/// Notes, cursor, gameplay enable flags and pause controls are never targeted.</summary>
public sealed class UnitySceneOpacity : IDisposable
{
    sealed class Hud { public Component Target=null!; public float Base, Written; public bool Owns; }
    sealed class Track { public Renderer Target=null!; public Material[] Original=Array.Empty<Material>(), Owned=Array.Empty<Material>(); public bool Enabled, Hidden, BlockOwned;
        public readonly MaterialPropertyBlock BaseBlock=new(),AppliedBlock=new(),CheckBlock=new(); }
    sealed class Shared { public Material Original=null!,Owned=null!; public int References; }
    readonly Dictionary<IntPtr,Shared> shared=new();
    readonly Hud[] hud = new Hud[4]; readonly List<Track> tracks = new();
    readonly Func<Component,float> read; readonly Action<Component,float> write;
    static readonly string[] HudPaths={"ScoreContainer","SongInfoContainer","ClearStateContainer","JudgementTextContainer"};
    static readonly string[] Colors={"_BaseColor","_Color","_FaceColor"};
    static string? AlphaColor(Material m){foreach(string p in Colors)if(m.HasProperty(p))return p;return null;}
    bool disposed;
    SceneOpacityValues current=SceneOpacityValues.Visible;
    public int TrackTargets => tracks.Count;
    public int OwnedMaterials => shared.Count;
    public UnitySceneOpacity(Transform uiRoot,Transform? trackRoot,Il2CppBase::Il2CppSystem.Type hudType,Func<Component,float> read,Action<Component,float> write)
    {
        (this.read,this.write)=(read,write);
        for(int i=0;i<hud.Length;i++) {
            // The restored GetComponent(string) overload uses an unavailable IL2CPP ReadOnlySpan method.
            var target=uiRoot.Find(HudPaths[i]); var component=target?target.GetComponent(hudType):null;
            hud[i]=new Hud{Target=component!};
        }
        if(trackRoot)foreach(var renderer in trackRoot!.GetComponentsInChildren<Renderer>(true)) {
            string path=Relative(renderer.transform,trackRoot);
            if(IsTrackPath(path))tracks.Add(new Track{Target=renderer});
        }
    }
    public static bool IsTrackPath(string path)
    {
        if(path.StartsWith("GAMEPLAY/",StringComparison.Ordinal)||path.Contains("SkyCursorExtras",StringComparison.Ordinal)||path.Contains("/v5-",StringComparison.Ordinal)||path.EndsWith("/TestGrid",StringComparison.Ordinal))return false;
        return path.StartsWith("MAIN_TRACK/",StringComparison.Ordinal)||path.StartsWith("SHIFT/",StringComparison.Ordinal)||path.StartsWith("SPACE/",StringComparison.Ordinal)
            ||path.StartsWith("FRAME/",StringComparison.Ordinal)||path.StartsWith("COLORED_RAILS/",StringComparison.Ordinal)||path.StartsWith("TRACK_",StringComparison.Ordinal)
            ||path.StartsWith("JUDGEMENT_LINE/",StringComparison.Ordinal);
    }
    static string Relative(Transform target,Transform root)
    {var names=new List<string>();for(var t=target;t&&t!=root;t=t.parent)names.Add(t.name);names.Reverse();return string.Join("/",names);}
    public void BeginFrame()
    {
        if(disposed)return;
        foreach(var h in hud)Restore(h);
        foreach(var t in tracks) {
            RestoreBlock(t);
            if(t.Hidden&&t.Target){if(!t.Target.enabled)t.Target.enabled=t.Enabled;t.Hidden=false;}
        }
    }
    void Restore(Hud h)
    {
        if(!h.Owns)return;
        if(h.Target&&Mathf.Abs(read(h.Target)-h.Written)<.000001f)write(h.Target,h.Base);
        h.Owns=false;
    }
    public void Apply(in SceneOpacityValues values)
    {
        if(disposed)return;
        for(int i=0;i<hud.Length;i++)if(!hud[i].Target&&values.At(i+1)<.999999f)throw new InvalidOperationException("Native HUD opacity target missing: "+HudPaths[i]);
        if(values.Track<.999999f&&tracks.Count==0)throw new InvalidOperationException("Native track opacity targets are unavailable.");
        current=values;ApplyHud(values);
        ApplyTrack(values.Track);
    }
    // Scope native alpha to the game's own layout flush. Restore authoring values after
    // calculation so game tweens never read our multiplied alpha as their next base.
    public void BeforeUiRender(){if(!disposed)ApplyHud(current);}
    public void AfterUiRender(){if(!disposed)foreach(var h in hud)Restore(h);}
    void ApplyHud(in SceneOpacityValues values)
    {
        for(int i=0;i<hud.Length;i++) {
            var h=hud[i];float opacity=Mathf.Clamp01(values.At(i+1));
            if(!h.Target){if(opacity<.999999f)throw new InvalidOperationException("Native HUD opacity target missing: "+HudPaths[i]);continue;}
            Restore(h);if(opacity>=.999999f)continue;
            h.Base=read(h.Target);h.Written=h.Base*opacity;write(h.Target,h.Written);h.Owns=true;
        }
    }
    void ApplyTrack(float value)
    {
        float alpha=Mathf.Clamp01(value);
        if(alpha>=.999999f&&shared.Count==0)return;
        if(alpha<.999999f&&tracks.Count==0)throw new InvalidOperationException("Native track opacity targets are unavailable.");
        foreach(var t in tracks) {
            if(!t.Target)continue;
            if(alpha>=.999999f){Release(t);continue;}
            bool ours=t.Owned.Length>0&&t.Target.sharedMaterial&&t.Target.sharedMaterial.Pointer==t.Owned[0].Pointer;
            if(!ours) {
                RestoreBlock(t);ReleaseOwned(t);t.Original=t.Target.sharedMaterials.ToArray();t.Owned=t.Original.Select(Acquire).ToArray();
                t.Target.sharedMaterials=new Il2CppReferenceArray<Material>(t.Owned);
            }
            ApplyBlock(t,alpha);
            if(alpha<=0&&!t.Hidden){t.Enabled=t.Target.enabled;t.Target.enabled=false;t.Hidden=true;}
        }
        foreach(var pair in shared.Values) {
            var m=pair.Owned;var original=pair.Original;
            if(original.HasProperty("_Opacity"))m.SetFloat("_Opacity",original.GetFloat("_Opacity")*alpha);
            else {var property=AlphaColor(original);if(property is not null){var color=original.GetColor(property);color.a*=alpha;m.SetColor(property,color);}}
            if(original.HasProperty("_EmissionColor")){var emission=original.GetColor("_EmissionColor");emission.r*=alpha;emission.g*=alpha;emission.b*=alpha;m.SetColor("_EmissionColor",emission);}
        }
    }
    Material Acquire(Material original)
    {
        if(!shared.TryGetValue(original.Pointer,out var entry)) {
            var m=new Material(original);
            if(m.HasProperty("_Surface"))m.SetFloat("_Surface",1);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");
            if(m.HasProperty("_SrcBlend"))m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
            if(m.HasProperty("_DstBlend"))m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            if(m.HasProperty("_ZWrite"))m.SetInt("_ZWrite",0);
            m.renderQueue=Math.Max((int)RenderQueue.Transparent,original.renderQueue);
            shared.Add(original.Pointer,entry=new Shared{Original=original,Owned=m});
        }
        entry.References++;return entry.Owned;
    }
    void ApplyBlock(Track t,float alpha)
    {
        RestoreBlock(t);t.Target.GetPropertyBlock(t.BaseBlock);t.Target.GetPropertyBlock(t.AppliedBlock);
        var original=t.Original.FirstOrDefault();if(original is null||!original)return;
        if(original.HasProperty("_Opacity")) {
            int id=Shader.PropertyToID("_Opacity");float value=t.BaseBlock.HasFloat(id)?t.BaseBlock.GetFloat(id):original.GetFloat(id);t.AppliedBlock.SetFloat(id,value*alpha);
        } else {var property=AlphaColor(original);if(property is not null){
            int id=Shader.PropertyToID(property);var color=t.BaseBlock.HasColor(id)?t.BaseBlock.GetColor(id):original.GetColor(id);color.a*=alpha;t.AppliedBlock.SetColor(id,color);
        }}
        t.Target.SetPropertyBlock(t.AppliedBlock);t.BlockOwned=true;
    }
    static void RestoreBlock(Track t)
    {
        if(!t.BlockOwned||!t.Target)return;t.Target.GetPropertyBlock(t.CheckBlock);bool owns=true;
        var original=t.Original.FirstOrDefault();
        if(original is not null&&original&&original.HasProperty("_Opacity")){int id=Shader.PropertyToID("_Opacity");owns=t.CheckBlock.HasFloat(id)&&Mathf.Abs(t.CheckBlock.GetFloat(id)-t.AppliedBlock.GetFloat(id))<.000001f;}
        else if(original is not null&&original){var property=AlphaColor(original);if(property is not null){int id=Shader.PropertyToID(property);owns=t.CheckBlock.HasColor(id)&&t.CheckBlock.GetColor(id)==t.AppliedBlock.GetColor(id);}}
        if(owns)t.Target.SetPropertyBlock(t.BaseBlock);t.BlockOwned=false;
    }
    void Release(Track t)
    {
        RestoreBlock(t);
        if(t.Target&&t.Hidden){if(!t.Target.enabled)t.Target.enabled=t.Enabled;t.Hidden=false;}
        if(t.Target&&t.Owned.Length>0) {
            var current=t.Target.sharedMaterials;bool ours=current.Length==t.Owned.Length;
            for(int i=0;ours&&i<current.Length;i++)ours=current[i]&&t.Owned[i]&&current[i].Pointer==t.Owned[i].Pointer;
            if(ours)t.Target.sharedMaterials=new Il2CppReferenceArray<Material>(t.Original);
        }
        ReleaseOwned(t);
    }
    void ReleaseOwned(Track t){foreach(var m in t.Original)if(shared.TryGetValue(m.Pointer,out var e)&&--e.References==0){Object.Destroy(e.Owned);shared.Remove(m.Pointer);}t.Owned=Array.Empty<Material>();t.Original=Array.Empty<Material>();}
    public void Dispose(){if(disposed)return;BeginFrame();foreach(var t in tracks)Release(t);disposed=true;}
}
