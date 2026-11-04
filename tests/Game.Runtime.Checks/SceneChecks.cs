extern alias UnityCore;
extern alias Il2CppBase;
using System.Runtime.InteropServices;
using System.Text.Json;
using InFalsus.Presentation;
using InFalsus.Presentation.Game;
using InFalsus.Vfx.Addon;
using UnityCore::UnityEngine;
using Object=UnityCore::UnityEngine.Object;
using MelonLoader.NativeUtils;

namespace InFalsus.Vfx.GameChecks;
public sealed partial class Entry
{
    UnitySceneOpacity? frameOpacity;
    Component? frameHud;
    GameObject? frameRoot;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void UiFlushFixture(byte force,IntPtr method);
    NativeHook<UiFlushFixture>? frameHook;
    UiFlushFixture? frameDetour;
    public override void OnUpdate()=>frameOpacity?.BeginFrame();
    void StartOpacityFrameProbe(Il2CppBase::Il2CppSystem.Type type)
    {
        frameRoot=new GameObject("Scene frame opacity fixture");
        var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="ScoreContainer";go.transform.SetParent(frameRoot.transform,false);
        frameHud=go.AddComponent(type);var p=frameHud.Pointer;
        var barrier=Marshal.GetDelegateForFunctionPointer<BarrierFixture>(System.Runtime.InteropServices.NativeLibrary.GetExport(native!.BaseAddress,"il2cpp_gc_wbarrier_set_field"));
        barrier(p,p+0x158,go.transform.Pointer);barrier(p,p+0x160,go.GetComponent<MeshRenderer>().Pointer);barrier(p,p+0x168,go.GetComponent<MeshFilter>().Pointer);
        Marshal.GetDelegateForFunctionPointer<TypeFixture>(native.BaseAddress+0x5021a0)(p,1,IntPtr.Zero);
        Marshal.GetDelegateForFunctionPointer<ResetFixture>(native.BaseAddress+0x502110)(p,IntPtr.Zero);
        var setter=Marshal.GetDelegateForFunctionPointer<AlphaFixture>(native.BaseAddress+0x503550);setter(p,.8f,IntPtr.Zero);
        frameOpacity=new(frameRoot.transform,null,type,c=>BitConverter.Int32BitsToSingle(Marshal.ReadInt32(c.Pointer+0x108)),(c,a)=>setter(c.Pointer,a,IntPtr.Zero));
        frameDetour=(force,method)=>{frameOpacity?.BeforeUiRender();try{frameHook!.Trampoline(force,method);}finally{frameOpacity?.AfterUiRender();}};
        frameHook=new(){Target=native.UiFlushMethod(),Detour=Marshal.GetFunctionPointerForDelegate(frameDetour)};frameHook.Attach();
    }
    void StopOpacityFrameProbe()
    {
        frameHook?.Detach();frameHook=null;frameOpacity?.Dispose();frameOpacity=null;if(frameRoot)Object.Destroy(frameRoot!);frameRoot=null;frameHud=null;
    }
    void AddSceneFixture()
    {
        var path=Path.Combine(fixture,"runtime.json");
        var doc=JsonSerializer.Deserialize<PackageDocument>(File.ReadAllText(path),Json)!;
        doc.Requires=doc.Requires.Concat(new[]{"effect.instances",SceneOpacityPlan.Capability}).Distinct().ToArray();
        for(int i=0;i<5;i++) {
            string id="scene-"+i;
            doc.Nodes=doc.Nodes.Append(new MaterialNodeDocument{Id=id,Target="postprocess",Bundle="background.bundle",
                Material=SceneOpacityPlan.Material,EffectId=SceneOpacityPlan.EffectIds[i]}).ToArray();
            doc.Channels=doc.Channels.Append(new ChannelDocument{Node=id,Property=SceneOpacityPlan.Properties[i],
                Kind=ParameterKind.Float,Default=new[]{1d},Keys=new[]{
                    new KeyDocument{Seconds=0,Value=new[]{1d},Ease=Easing.Linear},
                    new KeyDocument{Seconds=2,Value=new[]{0d},Ease=Easing.Linear}}}).ToArray();
        }
        File.WriteAllText(path,JsonSerializer.Serialize(doc,Json));
        var manifestPath=Path.Combine(fixture,"addon.json");
        var manifest=JsonSerializer.Deserialize<AddonManifest>(File.ReadAllText(manifestPath),Json)!;
        manifest.Files["runtime.json"]=AddonPackage.Hash(path);File.WriteAllText(manifestPath,JsonSerializer.Serialize(manifest,Json));
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void AlphaFixture(IntPtr self,float value,IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void BarrierFixture(IntPtr self,IntPtr field,IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void ResetFixture(IntPtr self,IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void TypeFixture(IntPtr self,byte type,IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr CalculatedFixture(IntPtr self,IntPtr method);
    float CalculatedOpacity(Component component)
    {
        var p=Marshal.GetDelegateForFunctionPointer<CalculatedFixture>(native!.BaseAddress+0x503600)(component.Pointer,IntPtr.Zero);
        return BitConverter.Int32BitsToSingle(Marshal.ReadInt32(p+0x10));
    }
    void CheckSceneTargets(Il2CppBase::Il2CppSystem.Type type)
    {
        var root=new GameObject("Scene opacity fixture");var trackRoot=new GameObject("MasterScenev10");
        var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Sprites/Default");
        if(shader is null)throw new InvalidOperationException("No alpha fixture shader");
        var material=new Material(shader);int colorId=Shader.PropertyToID(material.HasProperty("_BaseColor")?"_BaseColor":"_Color");
        material.SetColor(colorId,new Color(.2f,.5f,.8f,.8f));
        var setter=Marshal.GetDelegateForFunctionPointer<AlphaFixture>(native!.BaseAddress+0x503550);
        var barrier=Marshal.GetDelegateForFunctionPointer<BarrierFixture>(System.Runtime.InteropServices.NativeLibrary.GetExport(native.BaseAddress,"il2cpp_gc_wbarrier_set_field"));
        Component Hud(string name) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;go.transform.SetParent(root.transform,false);
            var c=go.AddComponent(type);barrier(c.Pointer,c.Pointer+0x158,go.transform.Pointer);
            barrier(c.Pointer,c.Pointer+0x160,go.GetComponent<MeshRenderer>().Pointer);barrier(c.Pointer,c.Pointer+0x168,go.GetComponent<MeshFilter>().Pointer);
            Marshal.GetDelegateForFunctionPointer<TypeFixture>(native.BaseAddress+0x5021a0)(c.Pointer,1,IntPtr.Zero);
            Marshal.GetDelegateForFunctionPointer<ResetFixture>(native.BaseAddress+0x502110)(c.Pointer,IntPtr.Zero);
            setter(c.Pointer,.8f,IntPtr.Zero);return c;
        }
        float Read(Component c)=>BitConverter.Int32BitsToSingle(Marshal.ReadInt32(c.Pointer+0x108));
        void Write(Component c,float a)=>setter(c.Pointer,a,IntPtr.Zero);
        Renderer Quad(string group,string name) {
            var parent=trackRoot.transform.Find(group);
            if(!parent){var go=new GameObject(group);go.transform.SetParent(trackRoot.transform,false);parent=go.transform;}
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name=name;quad.transform.SetParent(parent,false);
            var renderer=quad.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
        }
        var hud=new[]{Hud("ScoreContainer"),Hud("SongInfoContainer"),Hud("ClearStateContainer"),Hud("JudgementTextContainer")};
        var pause=Hud("PauseLayerContainer");var lane=Quad("MAIN_TRACK","LANE_1");var lane2=Quad("MAIN_TRACK","LANE_2");
        var note=Quad("GAMEPLAY","note2");var cursor=Quad("JUDGEMENT_LINE","SkyCursorExtras");
        using var controller=new UnitySceneOpacity(root.transform,trackRoot.transform,type,Read,Write);
        try {
            Check(controller.TrackTargets==2,"track filter excludes note and sky cursor renderers");
            controller.Apply(new(.5f,.5f,.25f,.75f,0));
            Check(Math.Abs(Read(hud[0])-.4f)<.0001&&Math.Abs(Read(hud[1])-.2f)<.0001&&Math.Abs(Read(hud[2])-.6f)<.0001&&Read(hud[3])==0,"actual native C2D alpha receives independent multiplicative HUD controls");
            Check(Read(pause)==.8f&&note.enabled&&cursor.enabled,"pause controls, notes and cursor are untouched");
            Check(controller.OwnedMaterials==1&&lane.sharedMaterial.Pointer==lane2.sharedMaterial.Pointer,"track renderers share one private material clone per native material");
            Check(lane.sharedMaterial.renderQueue>=3000,"faded track draws after opaque background rather than being overwritten by it");
            Check(Math.Abs(material.GetColor(colorId).a-.8f)<.0001&&Math.Abs(lane.sharedMaterial.GetColor(colorId).a-.4f)<.0001,"track fade changes its private material and preserves original asset");
            controller.BeginFrame();Check(hud.All(c=>Math.Abs(Read(c)-.8f)<.0001),"next frame restores native base alpha before game updates");
            Write(hud[0],.6f);controller.Apply(new(.5f,.5f,1,1,1));controller.Apply(new(.5f,.5f,1,1,1));
            Check(Math.Abs(Read(hud[0])-.3f)<.0001,"native alpha updates are respected and repeated sampling does not compound opacity");
            controller.BeginFrame();var block=new MaterialPropertyBlock();block.SetColor(colorId,new Color(.9f,.3f,.1f,.6f));lane.SetPropertyBlock(block);
            controller.Apply(new(.5f,1,1,1,1));lane.GetPropertyBlock(block);
            Check(Math.Abs(block.GetColor(colorId).a-.3f)<.0001,"native renderer property-block alpha is faded continuously");
            controller.BeginFrame();lane.GetPropertyBlock(block);
            Check(Math.Abs(block.GetColor(colorId).a-.6f)<.0001,"native property block is restored before next frame");
            controller.Apply(new(0,1,1,1,1));Check(!lane.enabled&&!lane2.enabled&&note.enabled&&cursor.enabled,"zero opacity hides track rendering only");
            controller.BeginFrame();Check(lane.enabled&&lane2.enabled,"frame restore recovers track renderer visibility");
            controller.Apply(new(.5f,.5f,1,1,1));Write(hud[0],.7f);controller.Dispose();
            Check(Math.Abs(Read(hud[0])-.7f)<.0001,"release preserves a newer external HUD alpha owner");
            Check(controller.OwnedMaterials==0&&lane.sharedMaterial.Pointer==material.Pointer&&lane2.sharedMaterial.Pointer==material.Pointer,"release restores original track materials and frees private clones");
        }finally{controller.Dispose();Object.Destroy(root);Object.Destroy(trackRoot);Object.Destroy(material);}
    }
}
