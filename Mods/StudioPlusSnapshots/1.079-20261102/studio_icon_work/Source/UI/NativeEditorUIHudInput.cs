using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.NativeVisual;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void InitHudMetadataInput(){
   harmony.Patch(Method("DrawSongHud"),transpiler:new HarmonyMethod(typeof(HudInputHooks).GetMethod("RemoveDesignerButton",F)));
   harmony.Patch(Method("BeginHudField"),new HarmonyMethod(typeof(HudInputHooks).GetMethod("AllowHudField",F)));
   harmony.Patch(Method("DrawHudEditField"),new HarmonyMethod(typeof(HudInputHooks).GetMethod("AllowHudInput",F)));
   harmony.Patch(Method("DrawHudEditField"),transpiler:new HarmonyMethod(typeof(HudInputHooks).GetMethod("AdaptField",F)));
   harmony.Patch(Method("BeginHudField"),postfix:new HarmonyMethod(typeof(HudInputHooks).GetMethod("ResetDraftView",F)));
   harmony.Patch(Method("HandleCommittedInput"),new HarmonyMethod(typeof(HudInputHooks).GetMethod("BeforeCommitted",F)),new HarmonyMethod(typeof(HudInputHooks).GetMethod("AfterCommitted",F)));
  }
  static bool HudMetadataButton(Rect rect,GUIContent content,GUIStyle style){
   if(I!=null&&I.ready&&content.tooltip=="Edit Chart Designer")return false;
   return GUI.Button(rect,content,style);
  }
  static string HudMetadataField(StudioBootstrap owner,Rect rect,string key,string live,Action<string> commit,bool area,bool empty){
   if(I==null||!I.ready||area)return (string)Call(owner,"CommittedField",rect,key,live,commit,area,empty);
   I.Bind(owner);string kind=Get(owner,"hudField").ToString();
   float hudScale=Val<float>(owner,"hudScale");int pixels;string face;
   if(kind=="Title"){pixels=Mathf.Max(13,Mathf.RoundToInt(17.5f*hudScale));face=NativePresentation.L.titleFace;}
   else if(kind=="Artist"){pixels=Mathf.Max(13,Mathf.RoundToInt(13.75f*hudScale));face=NativePresentation.L.artistFace;if(NativePresentation.Hud)rect.x=I.NativeSlot("title").x;}
   else if(kind=="Level"){pixels=Mathf.Max(10,Mathf.Min(Mathf.Max(12,Mathf.RoundToInt(10.3f*hudScale)),Mathf.FloorToInt(rect.height-3)));face="Bai Jamjuree SemiBold";}
   else{pixels=Mathf.Max(13,Mathf.RoundToInt(13.75f*hudScale));face=CNFace;}
   int padding=(int)(5*I.scale);rect.x-=padding;rect.width+=padding*2;
   float? inkTop=NativePresentation.Hud&&(kind=="Title"||kind=="Artist")?(float?)I.NativeSlot(kind=="Title"?"title":"artist").y:null;
   return I.UiInput(rect,key,live,commit,19,0,true,empty,face,false,pixels,inkTop);
  }
  internal static class HudInputHooks {
   static bool AllowHudField(object __0){return I==null||!I.ready||__0.ToString()!="Designer";}
   static bool AllowHudInput(StudioBootstrap __instance){return I==null||!I.ready||Get(__instance,"hudField").ToString()!="Designer";}
   static IEnumerable<CodeInstruction> RemoveDesignerButton(IEnumerable<CodeInstruction> codes){
    foreach(var code in codes){var method=code.operand as MethodInfo;if(method!=null&&method.DeclaringType==typeof(GUI)&&method.Name=="Button"&&method.GetParameters().Length==3&&method.GetParameters()[1].ParameterType==typeof(GUIContent)){code.opcode=OpCodes.Call;code.operand=typeof(NativeEditorUIPlugin).GetMethod("HudMetadataButton",F);}yield return code;}
   }
   static bool BeforeCommitted(StudioBootstrap __instance,Event __0,ref bool __result,out string __state){
    __state=null;if(I==null||!I.ready||Get(__instance,"hudField").ToString()=="None")return true;
    I.Bind(__instance);
    // The original generic field handler consumes Escape before the HUD handler
    // sees it. Give the existing HUD cancel transaction its Escape first.
    if(__0.type==EventType.KeyDown&&__0.keyCode==KeyCode.Escape){__result=(bool)Call(__instance,"HandleHudEditingKeys",__0);return false;}
    if(__0.type==EventType.MouseDown&&__0.button==0&&NativePresentation.Hud){
     string current=Get(__instance,"hudField").ToString();
     foreach(string kind in new[]{"Title","Artist","Level"}){
      Rect hit=I.NativeSlot(kind=="Title"?"title":kind=="Artist"?"artist":"level");
      float scale=Val<float>(__instance,"hudScale");
      if(kind=="Title")hit=NativeHudControls.TitleRect(I.NativeSlot("title"),I.NativeSlot("artist"),scale);
      else if(kind=="Artist")hit=NativeHudControls.ArtistRect(I.NativeSlot("title"),I.NativeSlot("artist"),scale);
      if(kind!=current&&hit.Contains(__0.mousePosition)){__state=kind;break;}
     }
    }
    return true;
   }
   static void AfterCommitted(StudioBootstrap __instance,bool __result,string __state){
    if(__state==null||!__result||I==null||!I.ready)return;string next=__state;
    I.Queue(delegate{if(Get(__instance,"hudField").ToString()=="None")Call(__instance,"BeginHudField",Enum.Parse(Field("hudField").FieldType,next));});
   }
   static IEnumerable<CodeInstruction> AdaptField(IEnumerable<CodeInstruction> codes){
    foreach(var code in codes){var method=code.operand as MethodInfo;if(method!=null&&method.DeclaringType==typeof(StudioBootstrap)&&method.Name=="CommittedField"){code.opcode=OpCodes.Call;code.operand=typeof(NativeEditorUIPlugin).GetMethod("HudMetadataField",F);}yield return code;}
   }
   static void ResetDraftView(){if(I==null)return;I.inputScroll.Remove("field_hud_metadata");I.inputDrag.Remove("field_hud_metadata");if(I.numericKey=="field_hud_metadata"){I.numericControl=0;I.numericSelectOnMouseUp=0;I.numericKey="";}}
  }
 }
}
