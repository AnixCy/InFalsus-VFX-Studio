using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using InFalsusStudio;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 internal sealed class SpacePreviewState {
  internal bool Active;internal double Start;internal EditHistory History;internal KeyCode Key;
  internal bool Begin(EditHistory history,double time){if(Active)return false;Active=true;Start=time;History=history;return true;}
  internal bool End(EditHistory history,out double time){time=Start;bool restore=Active&&ReferenceEquals(history,History);Active=false;History=null;return restore;}
 }
 public sealed partial class NativeEditorUIPlugin {
  readonly SpacePreviewState spacePreview=new SpacePreviewState();readonly HashSet<KeyCode> previewKeysDown=new HashSet<KeyCode>();
  void InitSpacePreview(){
   harmony.Patch(Method("TogglePlayback07"),transpiler:new HarmonyMethod(typeof(PreviewInputHooks).GetMethod("PreservePointDraft",F)));
   harmony.Patch(Method("ResetEditingForDocument"),prefix:new HarmonyMethod(typeof(PreviewInputHooks).GetMethod("DocumentChanging",F)));
   harmony.Patch(Method("HandleCommittedInput"),new HarmonyMethod(typeof(PreviewInputHooks).GetMethod("Committed",F)){priority=Priority.First});
   harmony.Patch(typeof(NoteRenderer).GetMethod("Draw",F),prefix:new HarmonyMethod(typeof(PreviewInputHooks).GetMethod("FloatingVisible",F)){priority=Priority.First+2000});
   harmony.Patch(Method("DrawWorkflowOverlay"),transpiler:new HarmonyMethod(typeof(PreviewInputHooks).GetMethod("RemoveFloatingCaption",F)));
  }
  bool SpaceKey(Event e,bool textFocused){if(spacePreview.Active&&e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){StopSpacePreview();previewKeysDown.Clear();e.Use();return true;}return false;}
  void HandlePreviewAction(string id,HotkeyChord chord,Event e){
   if(!previewKeysDown.Add(chord.Key)){e.Use();return;}if(spacePreview.Active){e.Use();return;}
   if(!boot.HasDocument||!(bool)Call(boot,"CommitAllInputFieldsNow")){e.Use();return;}
   if(id=="previewToggle"){RequestPlay(true);e.Use();return;}
   double start=boot.CurrentMs;if(Transport.Playing)Transport.Pause();
   previewRequest=true;try{Call(boot,"TogglePlayback07");if(Transport.Playing){spacePreview.Begin(boot.History,start);spacePreview.Key=chord.Key;formal=false;}}finally{previewRequest=false;}
   e.Use();
  }
  void StopSpacePreview(){
   if(boot==null||!spacePreview.Active)return;double start;bool restore=spacePreview.End(boot.History,out start);Transport.Pause();
   if(restore){Transport.Seek(start);Set(boot,"timeText",start.ToString("0.##########",CultureInfo.InvariantCulture));}formal=false;
  }
  static bool legacySpaceInput=true;static System.Reflection.PropertyInfo inputKeyboard,inputSpace,inputPressed;
  bool SpaceStillHeld(){
   if(legacySpaceInput){try{return Input.GetKey(spacePreview.Key);}catch(InvalidOperationException){legacySpaceInput=false;var keyboard=Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");if(keyboard!=null){inputKeyboard=keyboard.GetProperty("current");inputSpace=keyboard.GetProperty("spaceKey");if(inputSpace!=null)inputPressed=inputSpace.PropertyType.GetProperty("isPressed");}}}
   if(HotkeyChord.MouseKey(spacePreview.Key)){
    var mouse=Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");if(mouse==null)return true;var current=mouse.GetProperty("current").GetValue(null,null);if(current==null)return false;
    string button=spacePreview.Key==KeyCode.Mouse2?"middleButton":spacePreview.Key==KeyCode.Mouse3?"backButton":spacePreview.Key==KeyCode.Mouse4?"forwardButton":null;
    if(button==null)return true;var property=mouse.GetProperty(button);if(property==null)return true;var control=property.GetValue(current,null);return control!=null&&(bool)property.PropertyType.GetProperty("isPressed").GetValue(control,null);
   }
   if(inputKeyboard!=null){var physical=spacePreview.Key;string key=physical.ToString();string property=key.StartsWith("Alpha")?"digit"+key.Substring(5)+"Key":key.StartsWith("Keypad")?"numpad"+key.Substring(6)+"Key":char.ToLowerInvariant(key[0])+key.Substring(1)+"Key";inputSpace=inputKeyboard.DeclaringType.GetProperty(property);if(inputSpace!=null)inputPressed=inputSpace.PropertyType.GetProperty("isPressed");}
   if(inputKeyboard!=null&&inputSpace!=null&&inputPressed!=null){var keyboard=inputKeyboard.GetValue(null,null);return keyboard!=null&&(bool)inputPressed.GetValue(inputSpace.GetValue(keyboard,null),null);}
   return true; // KeyUp and focus-loss paths still terminate preview.
  }
  void TickSpacePreview(){if(spacePreview.Active&&(!Application.isFocused||!ReferenceEquals(spacePreview.History,boot.History)||spacePreview.Key!=hotkeys["previewHold"].Key||!SpaceStillHeld()))StopSpacePreview();}
  void OnApplicationFocus(bool focused){if(!focused){capturingHotkey=null;previewKeysDown.Clear();}if(!focused&&spacePreview.Active)StopSpacePreview();}
  void CommitTimingAndDeselect(EditHistory history,string name){
   StatementGuard(history,name);if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
   Call(boot,"Select",-1);statementFocus=-1;GUI.FocusControl(null);numericPointer.End();
  }
  void RefreshFloatingPointer(){
   if(!authoringPointerValid||!Val<bool>(boot,"showUi")||Val<RadialMenuState>(boot,"radial").Pressed||boot.History.InTransaction)return;
   var pointer=authoringGuiPointer;var camera=boot.ViewCamera;
   if(!camera||!camera.pixelRect.Contains(new Vector2(pointer.x,Screen.height-pointer.y))||Blocked(pointer)||(bool)Call(boot,"HudBlocksInput",pointer))return;
   var pending=Get(boot,"floating") as FloatingNotes;if(pending==null||!ReferenceEquals(Get(boot,"floatingHistory"),boot.History))return;
   // Resolve against the current playback clock on every rendered frame.
   // The native picker retains the same free-time/grid snapping as placement.
   object[] args={pointer,boot.CurrentMs,0d,0};if(!(bool)Call(boot,"PointerAuthoringTime",args))return;
   double target=(double)args[2];Set(boot,"hoverPointer",pointer);Set(boot,"hoverValid",true);Set(boot,"hoverTime",target);
   Set(boot,"guideActive",true);Set(boot,"guideAir",false);Set(boot,"guideTime",target);
   if(Val<bool>(boot,"floatingValid")&&Val<double>(boot,"floatingTarget")==target)return;
   try{
    var preview=pending.Preview(target);Set(boot,"ghostEvents",preview);((NoteRenderer)Get(boot,"ghostRenderer")).UpdateConnections(preview);
    Set(boot,"floatingTarget",target);Set(boot,"floatingValid",true);
   }catch(Exception e){Set(boot,"floatingValid",false);Set(boot,"ghostEvents",new SpcEvent[0]);Set(boot,"status",e.Message);}
  }
  static class PreviewInputHooks {
   static void FloatingVisible(NoteRenderer __instance,ref bool __4){
    if(I==null||!I.ready||I.formal||I.boot==null)return;
    var owner=I.boot;
    if(ReferenceEquals(__instance,Get(owner,"ghostRenderer"))&&Get(owner,"floating")!=null&&ReferenceEquals(Get(owner,"floatingHistory"),owner.History)){
     I.RefreshFloatingPointer();__4=owner.HasDocument&&Val<bool>(owner,"showNotes")&&Val<bool>(owner,"floatingValid");
    }
   }
   static void WorkflowCaption(StudioBootstrap owner,Vector2 pos,string text,float width,bool box,float font,TextAnchor anchor){
    if(I==null||!I.ready||Get(owner,"floating")==null)Call(owner,"OverlayText",pos,text,width,box,font,anchor);
   }
   static IEnumerable<CodeInstruction> RemoveFloatingCaption(IEnumerable<CodeInstruction> input){
    int count=0;foreach(var code in input){var method=code.operand as MethodInfo;
     if(method!=null&&method.DeclaringType==typeof(StudioBootstrap)&&method.Name=="OverlayText"){code.opcode=OpCodes.Call;code.operand=typeof(PreviewInputHooks).GetMethod("WorkflowCaption",F);count++;}
     yield return code;
    }if(count!=1)throw new MissingMethodException("Floating workflow caption anchor changed");
   }
   static void DocumentChanging(){if(I!=null){I.StopSpacePreview();I.previewKeysDown.Clear();}}
   static readonly FieldInfo[] placementFields=typeof(PointPlacement).GetFields(BindingFlags.Instance|BindingFlags.NonPublic);
   static void CancelForPlayback(StudioBootstrap owner){
    bool informal=I!=null&&I.ready&&(I.previewRequest||I.keyboardDepth>0);
    if(informal&&(Val<bool>(owner,"rangeActive")||Get(owner,"floating")!=null)&&!owner.History.InTransaction&&Get(owner,"gesture").ToString()=="None"){
     // Copy/cut/paste floaters are pending workflows just like range selection.
     // CancelGesture releases input capture, but its CancelWorkflow must not
     // discard the pending payload, cut-only hiding, ghost or cancel selection.
     string[] fields={"rangeActive","rangeHasStart","rangeStart","workflowSelection","workflowPrimary",
      "floating","floatingHistory","floatingHidden","ghostEvents","floatingTarget","floatingValid"};
     var saved=new object[fields.Length];for(int i=0;i<fields.Length;i++)saved[i]=Get(owner,fields[i]);
     var ids=new List<int>(Val<HashSet<int>>(owner,"selection")).ToArray();int primary=Val<int>(owner,"selectedId");var rangeHistory=owner.History;
     try{Call(owner,"CancelGesture");}finally{
      if(ReferenceEquals(rangeHistory,owner.History)){for(int i=0;i<fields.Length;i++)Set(owner,fields[i],saved[i]);Call(owner,"SetSelection",ids,primary);}
     }
     return;
    }
    var draft=(PointPlacement)Get(owner,"pointPlacement");
    bool preserve=I!=null&&I.ready&&(I.previewRequest||I.keyboardDepth>0)&&draft.Pending&&
      (draft.Kind==EventKind.Hold||draft.Kind==EventKind.SkyArea)&&!owner.History.InTransaction&&Get(owner,"gesture").ToString()=="None";
    if(!preserve){Call(owner,"CancelGesture");return;}
    var values=new object[placementFields.Length];for(int i=0;i<values.Length;i++)values[i]=placementFields[i].GetValue(draft);
    var history=owner.History;
    try{Call(owner,"CancelGesture");}finally{
     if(ReferenceEquals(history,owner.History))for(int i=0;i<values.Length;i++)placementFields[i].SetValue(draft,values[i]);
    }
   }
   static IEnumerable<CodeInstruction> PreservePointDraft(IEnumerable<CodeInstruction> input){
    int count=0;foreach(var code in input){var method=code.operand as MethodInfo;
     if(method!=null&&method.DeclaringType==typeof(StudioBootstrap)&&method.Name=="CancelGesture"){code.opcode=OpCodes.Call;code.operand=typeof(PreviewInputHooks).GetMethod("CancelForPlayback",F);count++;}
     yield return code;
    }if(count!=1)throw new MissingMethodException("Preview cancellation anchor changed");
   }
   static bool Committed(Event __0,ref bool __result){
    if(I==null||!I.ready||I.boot==null||I.panel!="时序"||__0.type!=EventType.KeyDown||(__0.keyCode!=KeyCode.Return&&__0.keyCode!=KeyCode.KeypadEnter)||!string.IsNullOrEmpty(Input.compositionString))return true;
    string focus=GUI.GetNameOfFocusedControl();if(I.statementSelected<0&&!focus.StartsWith("field_timing_",StringComparison.Ordinal))return true;
    var history=I.statementHistory;string name=I.statementGroup;I.Queue(delegate{I.CommitTimingAndDeselect(history,name);});__0.Use();__result=true;return false;
   }
  }
 }
}
