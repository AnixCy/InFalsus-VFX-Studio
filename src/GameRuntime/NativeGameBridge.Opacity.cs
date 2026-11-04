extern alias UnityCore;
extern alias Il2CppBase;
using System.Runtime.InteropServices;
using UnityCore::UnityEngine;

namespace InFalsus.Presentation.Game;

public sealed unsafe partial class NativeGameBridge : ISceneOpacityBindings
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr MethodCall(IntPtr klass,[MarshalAs(UnmanagedType.LPUTF8Str)]string name,int arguments);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void AlphaCall(IntPtr self,float alpha,IntPtr method);
    AlphaCall? alphaSetter;
    Il2CppBase::Il2CppSystem.Type? opacityHudType;
    UnitySceneOpacity? sceneOpacity;
    public void BeginOpacityFrame()=>sceneOpacity?.BeginFrame();
    public void RestoreSceneOpacity(){sceneOpacity?.Dispose();sceneOpacity=null;}
    public void BeforeUiRender()=>sceneOpacity?.BeforeUiRender();
    public void AfterUiRender()=>sceneOpacity?.AfterUiRender();
    public IntPtr UiFlushMethod()
    {
        var klass=FindClasses("_wi")["_wi"];
        var method=Export<MethodCall>("il2cpp_class_get_method_from_name")(klass,"_ERA",1);
        if(method==IntPtr.Zero)throw new MissingMethodException("Native UI layout flush is unavailable.");
        var pointer=Marshal.ReadIntPtr(method);
        return pointer!=IntPtr.Zero?pointer:throw new InvalidOperationException("Native UI layout flush has no method pointer.");
    }
    public void ApplySceneOpacity(in SceneOpacityValues values)
    {
        if(values==SceneOpacityValues.Visible){RestoreSceneOpacity();return;}
        if(sceneOpacity==null) {
            if(Scene==IntPtr.Zero||_sceneObject==null)throw new InvalidOperationException("Native scene opacity is not ready.");
            if(alphaSetter==null) {
                var c2d=FindClasses("Constrained2D")["Constrained2D"];
                var field=Export<FieldCall>("il2cpp_class_get_field_from_name")(c2d,"constrained2DTransform");
                if(field==IntPtr.Zero||Export<OffsetCall>("il2cpp_field_get_offset")(field)!=0xc8)throw new InvalidOperationException("C2D opacity layout mismatch.");
                var method=Export<MethodCall>("il2cpp_class_get_method_from_name")(c2d,"_jrA",1);
                if(method==IntPtr.Zero)throw new MissingMethodException("Constrained2D._jrA");
                var pointer=Marshal.ReadIntPtr(method);
                if(pointer==IntPtr.Zero)throw new InvalidOperationException("C2D opacity method is unavailable.");
                alphaSetter=Marshal.GetDelegateForFunctionPointer<AlphaCall>(pointer);
                var nativeType=Export<NameCall>("il2cpp_class_get_type")(c2d);
                opacityHudType=new(Export<NameCall>("il2cpp_type_get_object")(nativeType));
            }
            var track=GameCamera?GameCamera!.transform.Find("master_scene_v10/MasterScenev10"):null;
            sceneOpacity=new UnitySceneOpacity(_sceneObject.transform,track,opacityHudType!,
                c=>*(float*)(c.Pointer+0x108),(c,a)=>alphaSetter(c.Pointer,a,IntPtr.Zero));
        }
        sceneOpacity.Apply(values);
    }
}
