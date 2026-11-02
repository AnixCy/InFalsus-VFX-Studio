using System;
using InFalsusStudio;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool formalScrubbing,formalScrubResume;
  internal sealed class FormalProgressState {internal bool Formal,Playing;}
  void TickFormalNavigation(){
   if(formalScrubbing&&boot!=null&&!Val<bool>(boot,"progressDragging")){formalScrubbing=false;formalScrubResume=false;}
  }
  void CancelFormalNavigation(){
   formalScrubbing=false;formalScrubResume=false;if(boot==null)return;
   Set(boot,"progressDragging",false);int control=Val<int>(boot,"progressControlId");if(control!=0&&GUIUtility.hotControl==control)GUIUtility.hotControl=0;
  }
  internal static partial class Hooks {
   static void BeforeFormalProgress(StudioBootstrap __instance,out FormalProgressState __state){
    __state=null;if(!Active(__instance)||!I.formal||I.Transport==null)return;
    __state=new FormalProgressState{Formal=true,Playing=I.Transport.Playing};
   }
   static void AfterFormalProgress(StudioBootstrap __instance,FormalProgressState __state){
    if(I==null||!I.ready||__state==null||!__state.Formal)return;
    bool dragging=Val<bool>(__instance,"progressDragging");
    if(dragging){if(!I.formalScrubbing){I.formalScrubbing=true;I.formalScrubResume=__state.Playing;}return;}
    if(I.formalScrubbing){bool resume=I.formalScrubResume;I.formalScrubbing=false;I.formalScrubResume=false;if(resume){I.Transport.Play();I.formal=true;}}
   }
   static bool FormalSceneEvent(StudioBootstrap __instance,Event __0){
    if(!Active(__instance)||(!I.formal&&(__0.type!=EventType.ScrollWheel||!I.Transport.Playing)))return true;
    if(__0.type==EventType.ScrollWheel&&!I.formalScrubbing&&GUIUtility.hotControl==0&&(bool)Prop(__instance,"HasDocument")&&!(bool)Call(__instance,"HudBlocksInput",__0.mousePosition)){
     var camera=__instance.ViewCamera;Vector2 screen=new Vector2(__0.mousePosition.x,Screen.height-__0.mousePosition.y);
     if(camera&&camera.pixelRect.Contains(screen)){
      bool playing=I.Transport.Playing;Call(__instance,"BrowseWheel",__0.delta.y);if(playing)I.Transport.Play();__0.Use();
     }
    }
    return !I.formal;
   }
  }
 }
}
