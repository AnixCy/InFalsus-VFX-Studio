extern alias UnityCore;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityCore::UnityEngine;

namespace InFalsus.Vfx.GameChecks
{
    [BepInPlugin("local.infalsus.vfx.checks","VFX isolated lifecycle checks","0.9.0")]
    [BepInDependency("local.InFalsus.Presentation")]
    public sealed class CheckPlugin:BasePlugin
    {
        public override void Load(){BepCheckMod.Logger=new(Log);AddComponent<CheckDriver>();}
    }
    public sealed class CheckDriver:MonoBehaviour
    {
        Entry? check;
        public CheckDriver(IntPtr p):base(p){}
        void Update(){if(check==null&&Time.frameCount>10){check=new();check.OnLateInitializeMelon();}check?.OnUpdate();}
        void LateUpdate()=>check?.OnLateUpdate();
        void OnApplicationQuit()=>check?.OnDeinitializeMelon();
    }
    public abstract class BepCheckMod
    {
        internal static CheckLogger Logger=null!;
        public CheckLogger LoggerInstance=>Logger;
        public static IEnumerable<object> RegisteredMelons=>IL2CPPChainloader.Instance.Plugins.Values.Select(v=>v.Instance);
        public virtual void OnLateInitializeMelon(){}public virtual void OnUpdate(){}public virtual void OnLateUpdate(){}public virtual void OnDeinitializeMelon(){}
    }
    public sealed class CheckLogger
    {
        readonly BepInEx.Logging.ManualLogSource log;public CheckLogger(BepInEx.Logging.ManualLogSource log)=>this.log=log;
        public void Msg(string text)=>log.LogInfo(text);public void Error(object error)=>log.LogError(error);
    }
}
// The standalone UI frame probe is not run alongside an actual player entry.
// Keep the shared harness compilable without bringing MelonLoader into BepInEx.
namespace MelonLoader.NativeUtils
{
    public sealed class NativeHook<T> where T:Delegate
    {
        public IntPtr Target{get;set;}public IntPtr Detour{get;set;}public T Trampoline=>throw new NotSupportedException();
        public void Attach()=>throw new NotSupportedException("Use the loaded BepInEx player hook.");public void Detach(){}
    }
}
