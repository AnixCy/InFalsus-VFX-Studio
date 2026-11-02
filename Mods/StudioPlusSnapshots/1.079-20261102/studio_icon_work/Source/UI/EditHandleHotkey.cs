using System;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool handleHeld;HotkeyChord observedHandle;bool handleObserved;
  [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
  static bool PhysicalKey(KeyCode key,Event e){
   bool alt=key==KeyCode.LeftAlt||key==KeyCode.RightAlt;
   if(e!=null&&(e.rawType==EventType.KeyDown||e.rawType==EventType.KeyUp)&&
      (e.keyCode==key||alt&&(e.keyCode==KeyCode.LeftAlt||e.keyCode==KeyCode.RightAlt)))return e.rawType==EventType.KeyDown;
   if(e!=null&&HotkeyChord.MouseKey(key)&&(e.rawType==EventType.MouseDown||e.rawType==EventType.MouseUp)&&e.button==(int)key-(int)KeyCode.Mouse0)return e.rawType==EventType.MouseDown;
   int vk=0,n=(int)key;
   if(key>=KeyCode.A&&key<=KeyCode.Z)vk=n-(int)KeyCode.A+0x41;
   else if(key>=KeyCode.Alpha0&&key<=KeyCode.Alpha9)vk=n;
   else if(key>=KeyCode.F1&&key<=KeyCode.F15)vk=n-(int)KeyCode.F1+0x70;
   else if(key>=KeyCode.Keypad0&&key<=KeyCode.Keypad9)vk=n-(int)KeyCode.Keypad0+0x60;
   else switch(key){
    case KeyCode.LeftAlt:case KeyCode.RightAlt:vk=0x12;break;
    case KeyCode.LeftControl:case KeyCode.RightControl:vk=0x11;break;
    case KeyCode.LeftShift:case KeyCode.RightShift:vk=0x10;break;
    case KeyCode.Space:vk=0x20;break;case KeyCode.Backspace:vk=8;break;case KeyCode.Delete:vk=0x2e;break;
    case KeyCode.Insert:vk=0x2d;break;case KeyCode.Home:vk=0x24;break;case KeyCode.End:vk=0x23;break;
    case KeyCode.PageUp:vk=0x21;break;case KeyCode.PageDown:vk=0x22;break;
    case KeyCode.Mouse2:vk=4;break;case KeyCode.Mouse3:vk=5;break;case KeyCode.Mouse4:vk=6;break;
   }
   if(vk!=0&&Application.platform==RuntimePlatform.WindowsPlayer)try{return (GetAsyncKeyState(vk)&0x8000)!=0;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
   return alt?Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt):Input.GetKey(key);
  }
  internal static bool HeldChord(HotkeyChord chord,bool key,bool control,bool alt,bool shift){return key&&(!chord.Ctrl||control)&&(!chord.Alt||alt)&&(!chord.Shift||shift);}
  void ObserveHandleKey(Event e){
   RecoverMiddleRelease(e);
   var key=hotkeys["handles"];
   // A changed binding must be pressed afresh; it cannot inherit an old drag.
   if(!handleObserved||!observedHandle.Equals(key)){handleHeld=false;observedHandle=key;handleObserved=true;}
   if(!Application.isFocused||capturingHotkey!=null||formal){handleHeld=false;return;}
   bool focused=boot!=null&&(NativeTextInputFocused||PopupOwnsTextFocus||Get(boot,"hudField").ToString()!="None"||GUI.GetNameOfFocusedControl().StartsWith("field",StringComparison.Ordinal));
   handleHeld=!focused&&HeldChord(key,PhysicalKey(key.Key,e),PhysicalKey(KeyCode.LeftControl,e),PhysicalKey(KeyCode.LeftAlt,e),PhysicalKey(KeyCode.LeftShift,e));
  }
  void InitHandleHotkey(){
   InitMiddleHandleDrag();
   harmony.Patch(Method("OnGUI"),prefix:new HarmonyMethod(typeof(HandleKeyHooks).GetMethod("Observe",F)){priority=Priority.First+1500});
   harmony.Patch(typeof(InFalsusStudio.StudioBootstrap).GetProperty("EditHeld",F).GetGetMethod(true),prefix:new HarmonyMethod(typeof(HandleKeyHooks).GetMethod("Held",F)));
   harmony.Patch(Method("OnApplicationFocus"),postfix:new HarmonyMethod(typeof(HandleKeyHooks).GetMethod("Focus",F)));
  }
  static class HandleKeyHooks {
   static void Observe(InFalsusStudio.StudioBootstrap __instance,bool __runOriginal){if(__runOriginal&&I!=null&&I.ready){I.Bind(__instance);I.ObserveHandleKey(Event.current);}}
   static bool Held(ref bool __result){if(I==null||!I.ready)return true;__result=I.middleDragging||I.handleHeld&&Application.isFocused;return false;}
   static void Focus(bool __0){if(I!=null){if(!__0){I.handleHeld=false;I.middleDragging=false;}else if(!I.middleDragging)I.handleHeld=false;}}
  }
 }
}
