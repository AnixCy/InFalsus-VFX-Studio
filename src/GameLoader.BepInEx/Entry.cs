extern alias UnityCore;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using InFalsus.Presentation.Game;
using UnityCore::UnityEngine;
using Object=UnityCore::UnityEngine.Object;

namespace InFalsus.Presentation.BepInEx;

[BepInPlugin("local.InFalsus.Presentation","InFalsus Presentation","0.9.0")]
public sealed class Entry:BasePlugin
{
    PresentationDriver? driver;
    public RuntimeHost? Host=>driver?.Host;
    public override void Load(){driver=AddComponent<PresentationDriver>();driver.Report=s=>Log.LogInfo(s);}
    public override bool Unload(){if(driver){driver!.Shutdown();Object.Destroy(driver);}driver=null;return true;}
}
public sealed class PresentationDriver:MonoBehaviour
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void SeekCall(IntPtr scene);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void UiCall(byte force,IntPtr method);
    public Action<string> Report= _=>{};
    RuntimeHost? host;NativeDetour<SeekCall>? seek;NativeDetour<UiCall>? ui;
    internal RuntimeHost? Host{[Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]get=>host;}
    SeekCall? seekCallback;UiCall? uiCallback;bool attempted;
    public PresentationDriver(IntPtr pointer):base(pointer){}
    void Update()
    {
        if(!attempted&&Time.frameCount>2){attempted=true;Initialize();}
        host?.BeforeUpdate();
    }
    void LateUpdate()=>host?.Tick();
    void Initialize()
    {
        try {
            string root=Paths.GameRootPath;
            host=new(root,Path.Combine(root,"UserData/InFalsusPresentation"),Report);
            var bridge=(NativeGameBridge)host.Bridge;
            uiCallback=(force,method)=>{try{host?.BeforeUiRender();}catch(Exception e){Report(e.Message);}try{ui!.Trampoline(force,method);}finally{try{host?.AfterUiRender();}catch(Exception e){Report(e.Message);}}};
            ui=new(bridge.UiFlushMethod(),uiCallback);ui.Attach();
            if(bridge.Profile.PracticeSeekRva!=0) {
                bridge.VerifySeekPrefix();
                seekCallback=scene=>{seek!.Trampoline(scene);if(bridge.IsCommittedPracticeSeek(scene))bridge.NotifySeekCommitted();};
                seek=new(bridge.BaseAddress+bridge.Profile.PracticeSeekRva,seekCallback);seek.Attach();bridge.HasExactSeekSerial=true;
            }
            Report("BepInEx presentation ready; chart packs discovered automatically.");
        }catch(Exception e){Report("Presentation unavailable: "+e);Shutdown();}
    }
    public void Shutdown(){try{ui?.Dispose();seek?.Dispose();}finally{ui=null;seek=null;host?.Dispose();host=null;}}
    void OnDestroy()=>Shutdown();
    void OnApplicationQuit()=>Shutdown();
}

// BepInEx ships Dobby. Only this loader adapter depends on its native detour API.
internal static class DobbyApi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal delegate int HookCall(IntPtr target,IntPtr detour,out IntPtr trampoline);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal delegate int DestroyCall(IntPtr target);
    static readonly IntPtr library=NativeLibrary.Load(Path.Combine(Paths.BepInExAssemblyDirectory,"dobby.dll"));
    internal static readonly HookCall Hook=Marshal.GetDelegateForFunctionPointer<HookCall>(NativeLibrary.GetExport(library,"DobbyHook"));
    internal static readonly DestroyCall Destroy=Marshal.GetDelegateForFunctionPointer<DestroyCall>(NativeLibrary.GetExport(library,"DobbyDestroy"));
}
internal sealed class NativeDetour<T>:IDisposable where T:Delegate
{
    readonly IntPtr target;readonly T callback;bool attached;
    internal T Trampoline{get;private set;}=null!;
    internal NativeDetour(IntPtr target,T callback){this.target=target;this.callback=callback;}
    internal void Attach(){if(attached)throw new InvalidOperationException("Hook already attached.");if(DobbyApi.Hook(target,Marshal.GetFunctionPointerForDelegate(callback),out var p)!=0||p==IntPtr.Zero)throw new InvalidOperationException("Native detour failed.");Trampoline=Marshal.GetDelegateForFunctionPointer<T>(p);attached=true;}
    public void Dispose(){if(!attached)return;if(DobbyApi.Destroy(target)!=0)throw new InvalidOperationException("Native detour removal failed.");attached=false;GC.KeepAlive(callback);}
}
