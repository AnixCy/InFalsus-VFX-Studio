using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using InFalsusStudio;
using InFalsusStudio.Core;
using InFalsusStudio.Performance;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool middleDragging,middleDispatch,middleReleaseCapture;int middleControlId,middleOwnerControl,middleMoves;
  Vector2 middleLastPointer;
  static readonly FieldInfo editGateHeld=typeof(StudioBootstrap).Assembly.GetType("InFalsusStudio.EditModifierGate").GetField("<Held>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
  internal static bool MiddleHandleBinding(HotkeyChord chord){return chord.Key==KeyCode.Mouse2;}
  void SyncHandleGate(){if(boot!=null)editGateHeld.SetValue(Get(boot,"editModifier"),middleDragging||handleHeld);}
  void InitMiddleHandleDrag(){
   if(editGateHeld==null)throw new MissingFieldException("Edit modifier state changed");
   harmony.Patch(Method("OnGUI"),prefix:new HarmonyMethod(typeof(MiddleDragHooks).GetMethod("Dispatch",F)){priority=Priority.First+1250});
   harmony.Patch(Method("ObserveEditorModifiers"),prefix:new HarmonyMethod(typeof(MiddleDragHooks).GetMethod("Modifiers",F)));
   foreach(string name in new[]{"CancelGesture","ReleaseGesture","ResetEditingForDocument"})harmony.Patch(Method(name),postfix:new HarmonyMethod(typeof(MiddleDragHooks).GetMethod("Ended",F)));
  }
  internal bool MiddleDragging(StudioBootstrap owner){return middleDragging&&ReferenceEquals(owner,boot);}
  [System.Diagnostics.Conditional("INFSTUDIO_POINTER_TRACE")]
  void MiddleTrace(string message){
   try{string folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"VisualUIData","Diagnostics");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"middle-drag.txt");if(File.Exists(path)&&new FileInfo(path).Length>65536)File.WriteAllText(path,"");File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
  void EndMiddleOwnership(){middleDragging=false;middleReleaseCapture=true;handleHeld=false;SyncHandleGate();}
  bool BeginMiddleDrag(Event e){
   if(!MiddleHandleBinding(hotkeys["handles"])||formal||Transport.Playing||capturingHotkey!=null||!Val<bool>(boot,"showUi")||!Val<bool>(boot,"showNotes")||Val<bool>(boot,"guiShowReleaseNotes")||
      boot.History.InTransaction||(bool)Prop(boot,"PointerEditGesture")||(bool)Prop(boot,"WorkflowActive")||Val<RadialMenuState>(boot,"radial").Pressed||!(bool)Call(boot,"AllowedCanvas",e.mousePosition))return false;
   var key=hotkeys["handles"];if(!HeldChord(key,true,e.control||e.command,e.alt,e.shift))return false;
   MiddleTrace("press pointer="+e.mousePosition+" hot="+GUIUtility.hotControl+" owner="+middleControlId);
   if(!(bool)Call(boot,"CommitAllInputFieldsNow")||!(bool)Call(boot,"CommitPendingNoteNow")){MiddleTrace("rejected pending input");return true;}
   ReleasePopupFocus();middleDragging=true;handleHeld=true;SyncHandleGate();HandleOptimization.Invalidate(boot);
   int id=Val<int>(boot,"selectedId");NoteHandle kind=NoteHandle.Body;bool hit=false,locked=false;
   Call(boot,"BuildHandles");float scale=Convert.ToSingle(Prop(boot,"OverlayScale")),best=144*scale*scale;
   foreach(object item in (IEnumerable)Get(boot,"handles")){
    var type=item.GetType();var point=(Vector2)type.GetField("Gui",F).GetValue(item);float distance=(point-e.mousePosition).sqrMagnitude;
    if(distance<best){best=distance;kind=(NoteHandle)type.GetField("Kind",F).GetValue(item);locked=(bool)type.GetField("Locked",F).GetValue(item);hit=true;}
   }
   if(!hit){
    var notes=(NoteRenderer)Get(boot,"notes");var ray=(Ray)Call(boot,"PointerRay",e.mousePosition);var picks=notes.PickAll(ray,Val<int>(boot,"pickFilter"));
    if(picks.Count==0){MiddleTrace("no handle or note hit");EndMiddleOwnership();return true;}
    id=picks[0];if(!Val<HashSet<int>>(boot,"selection").Contains(id))Call(boot,"Select",id);
    else {Set(boot,"selectedId",id);Call(boot,"RefreshSelectionFields");Call(boot,"FollowSelectedSkyGroup");}
    kind=NoteHandle.Body;
   }
   if(locked||id<0){MiddleTrace("locked handle id="+id);EndMiddleOwnership();return true;}
   middleDispatch=true;try{Call(boot,"BeginNoteDrag",e,id,kind);}finally{middleDispatch=false;}
   if(!boot.History.InTransaction||!(bool)Prop(boot,"PointerEditGesture")){MiddleTrace("begin rejected: "+Get(boot,"status"));EndMiddleOwnership();return true;}
   // Own capture independently of the native left-button scene control.
   middleOwnerControl=middleControlId;Set(boot,"capturedControl",middleOwnerControl);GUIUtility.hotControl=middleOwnerControl;
   middleLastPointer=e.mousePosition;middleMoves=0;MiddleTrace("begin id="+id+" handle="+kind+" owner="+middleOwnerControl);return true;
  }
  void PreviewMiddleDrag(Event e){
   Set(boot,"pointerNow",e.mousePosition);float scale=Convert.ToSingle(Prop(boot,"OverlayScale"));
   if((e.mousePosition-Val<Vector2>(boot,"pointerDown")).sqrMagnitude>9*scale*scale)Set(boot,"moved",true);
   if(!Val<bool>(boot,"moved")||e.mousePosition==middleLastPointer)return;
   middleLastPointer=e.mousePosition;middleMoves++;var preview=new Event(e){type=EventType.MouseDrag,button=2};
   middleDispatch=true;try{Call(boot,"PreviewNoteDrag",preview);}finally{middleDispatch=false;}
   if(middleMoves==1)MiddleTrace("first motion pointer="+e.mousePosition);
  }
  bool RouteMiddleDrag(Event e){
   if(middleDispatch||boot==null||!boot.HasDocument)return false;var type=e.rawType;
   if(type==EventType.MouseDown&&e.button==2){bool handled=BeginMiddleDrag(e);if(handled)e.Use();return handled;}
   if(!middleDragging)return false;
   if(type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){Call(boot,"CancelGesture");MiddleTrace("cancel Escape");e.Use();return true;}
   if(type==EventType.MouseMove||type==EventType.MouseDrag){PreviewMiddleDrag(e);e.Use();return true;}
   if(type==EventType.MouseUp&&e.button==2){
    PreviewMiddleDrag(e);Set(boot,"pointerNow",e.mousePosition);middleDispatch=true;
    try{Call(boot,"ReleaseGesture");MiddleTrace("release moves="+middleMoves+" pointer="+e.mousePosition+" undo="+boot.History.UndoCount);}finally{middleDispatch=false;EndMiddleOwnership();}
    e.Use();return true;
   }
   if(type==EventType.ScrollWheel||type==EventType.MouseDown||type==EventType.MouseUp){e.Use();return true;}return false;
  }
  void RecoverMiddleRelease(Event e){
   // A physical MouseUp ends the gesture. Polling VK_MBUTTON during Layout
   // could release it before Unity delivered its first movement event.
   if(middleDragging&&boot!=null&&!Application.isFocused){Call(boot,"CancelGesture");MiddleTrace("cancel focus");EndMiddleOwnership();}
  }
  static class MiddleDragHooks {
   static void Dispatch(StudioBootstrap __instance,bool __runOriginal){
    if(!__runOriginal||I==null||!I.ready)return;I.Bind(__instance);
    // Allocate on every GUI pass, not only mouse events or Repaint.
    I.middleControlId=GUIUtility.GetControlID("InfStudio.MiddleHandle".GetHashCode(),FocusType.Passive);
    if(I.middleReleaseCapture){if(GUIUtility.hotControl==I.middleOwnerControl)GUIUtility.hotControl=0;I.middleReleaseCapture=false;}
    if(I.middleDragging&&GUIUtility.hotControl!=I.middleOwnerControl)GUIUtility.hotControl=I.middleOwnerControl;
    var e=Event.current;if(e.type==EventType.Used)return;bool down=e.rawType==EventType.MouseDown&&e.button==2;if(I.RouteMiddleDrag(e)&&down)GUI.FocusControl(null);
   }
   static bool Modifiers(StudioBootstrap __instance){
    if(I==null||!I.ready)return true;I.Bind(__instance);I.SyncHandleGate();
    if(!I.middleDragging&&!I.handleHeld&&(bool)Prop(__instance,"PointerEditGesture"))Call(__instance,"ReleaseGesture");return false;
   }
   static void Ended(){if(I!=null&&!I.middleDispatch&&I.middleDragging){I.MiddleTrace("native gesture ended");I.EndMiddleOwnership();}}
  }
 }
}
