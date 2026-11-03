using System.Diagnostics;
using System.Runtime.InteropServices;
using InFalsus.Presentation.Game;
using MelonLoader;
using MelonLoader.NativeUtils;

[assembly: MelonInfo(typeof(InFalsus.Presentation.Melon.Entry), "InFalsus Presentation", "0.7.0", "Local")]

namespace InFalsus.Presentation.Melon;

public sealed class Entry : MelonMod
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SeekCall(IntPtr scene);
    private RuntimeHost? _host;
    private NativeHook<SeekCall>? _seekHook;
    private SeekCall? _detour;
    private bool _hooked;
    public override void OnLateInitializeMelon()
    {
        try
        {
            var root = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
            _host = new(root, Path.Combine(root, "UserData", "InFalsusPresentation"), LoggerInstance.Msg);
            var bridge=(NativeGameBridge)_host.Bridge;
            if (bridge.Profile.PracticeSeekRva != 0)
            {
                bridge.VerifySeekPrefix();
                _detour = OnSeek;
                _seekHook = new()
                {
                    Target = bridge.BaseAddress + bridge.Profile.PracticeSeekRva,
                    Detour = Marshal.GetFunctionPointerForDelegate(_detour)
                };
                _seekHook.Attach(); _hooked = true;
                bridge.HasExactSeekSerial = true;
            }
        }
        catch (Exception e)
        {
            LoggerInstance.Error($"Presentation unavailable: {e.Message}"); Shutdown();
        }
    }
    // Runs after native game Update and its existing mouse/practice prefix chain.
    public override void OnLateUpdate() => _host?.Tick();
    private void OnSeek(IntPtr scene)
    {
        _seekHook!.Trampoline(scene);
        try
        {
            if (_host != null && ((NativeGameBridge)_host.Bridge).IsCommittedPracticeSeek(scene))
                ((NativeGameBridge)_host.Bridge).NotifySeekCommitted();
        }
        catch (Exception e) { LoggerInstance.Error($"Presentation seek notification: {e.Message}"); }
    }
    public override void OnDeinitializeMelon() => Shutdown();
    private void Shutdown()
    {
        try { if (_hooked) { _seekHook!.Detach(); _hooked = false; } }
        finally { _host?.Dispose(); _host = null; }
    }
}
