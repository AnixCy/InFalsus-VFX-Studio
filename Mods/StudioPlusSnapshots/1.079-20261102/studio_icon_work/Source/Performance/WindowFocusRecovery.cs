using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class WindowFocusRecovery {
  sealed class State {internal bool ReleaseCapture,ResyncModifiers;}
  static readonly ConditionalWeakTable<StudioBootstrap,State> states=new ConditionalWeakTable<StudioBootstrap,State>();
  static readonly BindingFlags F=TimingGroups.F;
  const EventModifiers Keys=EventModifiers.Alt|EventModifiers.Control|EventModifiers.Shift|EventModifiers.Command;
  [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
  static bool unavailable;
  static bool Down(int key){return (GetAsyncKeyState(key)&0x8000)!=0;}
  static void Focus(StudioBootstrap __instance,bool __0){
   var state=states.GetValue(__instance,b=>new State());state.ReleaseCapture=true;state.ResyncModifiers=true;
   HandleOptimization.Invalidate(__instance);
  }
  internal static EventModifiers Reconcile(Event e,bool control,bool alt,bool shift,bool command){
   // Explicit transitions can precede the asynchronous Windows key snapshot.
   bool down=e.rawType==EventType.KeyDown,up=e.rawType==EventType.KeyUp;
   if(down||up){switch(e.keyCode){
    case KeyCode.LeftControl:case KeyCode.RightControl:control=down;break;
    case KeyCode.LeftAlt:case KeyCode.RightAlt:alt=down;break;
    case KeyCode.LeftShift:case KeyCode.RightShift:shift=down;break;
    case KeyCode.LeftWindows:case KeyCode.RightWindows:command=down;break;
   }}
   return (e.modifiers&~Keys)|(control?EventModifiers.Control:0)|(alt?EventModifiers.Alt:0)|(shift?EventModifiers.Shift:0)|(command?EventModifiers.Command:0);
  }
  static void BeforeGui(StudioBootstrap __instance){
   State state;if(!states.TryGetValue(__instance,out state))return;
   if(state.ReleaseCapture){
    // Control IDs may have changed while the application was unfocused. The
    // native ID comparison cannot release an orphaned IMGUI mouse capture.
    // Do this in OnGUI, before any button can acquire a fresh capture.
    GUIUtility.hotControl=0;state.ReleaseCapture=false;
   }
   var e=Event.current;if(!state.ResyncModifiers||e==null||(!e.isKey&&!e.isMouse&&e.type!=EventType.ScrollWheel)||
      Application.platform!=RuntimePlatform.WindowsPlayer||unavailable)return;
   try{e.modifiers=Reconcile(e,Down(0x11),Down(0x12),Down(0x10),Down(0x5b)||Down(0x5c));}
   catch(DllNotFoundException){unavailable=true;}catch(EntryPointNotFoundException){unavailable=true;}
  }
  internal static void Install(Harmony h){
   h.Patch(typeof(StudioBootstrap).GetMethod("OnApplicationFocus",F),postfix:new HarmonyMethod(typeof(WindowFocusRecovery).GetMethod("Focus",F)));
   // Run ahead of hotkey routing and all controls. Canonical events dispatched
   // internally by custom shortcuts never pass through this physical-input hook.
   h.Patch(typeof(StudioBootstrap).GetMethod("OnGUI",F),prefix:new HarmonyMethod(typeof(WindowFocusRecovery).GetMethod("BeforeGui",F)){priority=Priority.First+2000});
  }
 }
}
