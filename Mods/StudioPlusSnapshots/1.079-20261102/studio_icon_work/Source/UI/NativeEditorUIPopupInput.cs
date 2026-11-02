using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void InitPopupInput(){harmony.Patch(Method("HandleCommittedInput"),new HarmonyMethod(typeof(PopupInputHooks).GetMethod("Before",F)){priority=Priority.First+100});}
  void ReleasePopupFocus(){popupKeyboardOwner=false;popupFocusedField="";popupClearFocus=true;popupNumericControl=0;popupNumericKey="";numericPointer.End();}
  static class PopupInputHooks {
   static bool Before(Event __0,ref bool __result){
    if(I==null||!I.ready||I.boot==null)return true;
    bool owned=I.PopupOwnsTextFocus;string key=I.popupFocusedField;
    if(__0.type==EventType.MouseDown){
     bool inside=I.notePopupOpen&&I.notePopupRect.Contains(__0.mousePosition);
     if(owned){var drafts=(IDictionary)Get(I.boot,"fieldDrafts");if(drafts.Contains(key)){var draft=drafts[key];var rect=(Rect)draft.GetType().GetField("RootRect",F).GetValue(draft);if(!rect.Contains(__0.mousePosition))Call(I.boot,"QueueFieldCommit",key);}}
     I.popupKeyboardOwner=inside;
     if(!inside&&owned)I.ReleasePopupFocus();
    }
    if(!owned||__0.type!=EventType.KeyDown||!string.IsNullOrEmpty(Input.compositionString))return true;
    if(__0.keyCode!=KeyCode.Return&&__0.keyCode!=KeyCode.KeypadEnter&&__0.keyCode!=KeyCode.Escape)return true;
    if(__0.keyCode==KeyCode.Escape)Call(I.boot,"ForgetInputField",key);else Call(I.boot,"QueueFieldCommit",key);
    I.ReleasePopupFocus();Set(I.boot,"activeField","");__0.Use();__result=true;return false;
   }
  }
 }
}
