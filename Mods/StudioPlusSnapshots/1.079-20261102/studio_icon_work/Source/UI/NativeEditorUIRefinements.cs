using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using InFalsusStudio;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  ConfigEntry<bool> cursorVisibleConfig,chineseConfig;int numericControl,numericSelectOnMouseUp;string numericKey="";readonly HashSet<int> numericControls=new HashSet<int>();
  void InitRefinements(){
   cursorVisibleConfig=Config.Bind("Display","ShowAutoplayCursor",true,"Only show/hide the cursor mesh; autoplay, note clipping, sounds and feedback remain controlled by the left rail.");
   chineseConfig=Config.Bind("Interface","Chinese",true,"Chinese/English UI language, including original advanced panels.");
   harmony.Patch(typeof(NoteRenderer).GetMethod("DrawCursor",F),new HarmonyMethod(typeof(RefinementHooks).GetMethod("CursorDrawing",F)){priority=Priority.First});
   harmony.Patch(Method("DrawSongPanel"),null,null,new HarmonyMethod(typeof(RefinementHooks).GetMethod("RemoveSongAutoplay",F)));
   harmony.Patch(Method("DrawHelp07"),null,null,new HarmonyMethod(typeof(RefinementHooks).GetMethod("RemoveHelpLanguage",F)));
  }
  string T(string text){return UILocalization.Get(text,boot==null||Val<bool>(boot,"chinese"));}
  void ResetInputFocus(){if(GUIUtility.keyboardControl==0||!numericControls.Contains(GUIUtility.keyboardControl)){numericControl=0;numericSelectOnMouseUp=0;numericKey="";}}
  void SetLanguage(bool chinese){Queue(delegate{chineseConfig.Value=chinese;Set(boot,"chinese",chinese);});}
  void LanguageChoice(){
   Rect row=ContentRow(62.796875f);RowLine(row);UiText(new Rect(0,row.y,220*scale,row.height),"界面语言",17,Css("#a6c3d1"));
   bool chinese=Val<bool>(boot,"chinese");
   if(FlatButton(new Rect(237*scale,row.y+16*scale,77*scale,31*scale),"中",16,chinese))SetLanguage(true);
   if(FlatButton(new Rect(326*scale,row.y+16*scale,79*scale,31*scale),"EN",15,!chinese))SetLanguage(false);
  }
  static Texture2D ThinBar(bool vertical,int pixels,Color color){
   var texture=new Texture2D(12,12,TextureFormat.RGBA32,false);
   var data=new Color[144];int low=(12-pixels)/2;
   for(int y=0;y<12;y++)for(int x=0;x<12;x++)if((vertical?x:y)>=low&&(vertical?x:y)<low+pixels)data[y*12+x]=color;
   texture.SetPixels(data);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Point;texture.hideFlags=HideFlags.HideAndDontSave;return texture;
  }
  static void BarStyle(GUIStyle style,Texture2D texture){
   foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})state.background=texture;
   style.border=new RectOffset();style.margin=new RectOffset();style.padding=new RectOffset();
  }
  void SetupFineBars(){
   BarStyle(skin.verticalScrollbar,ThinBar(true,2,Css("#547f8b")));
   BarStyle(skin.verticalScrollbarThumb,ThinBar(true,4,Css("#b4e4ef")));
   BarStyle(skin.horizontalScrollbar,ThinBar(false,2,Css("#547f8b")));
   BarStyle(skin.horizontalScrollbarThumb,ThinBar(false,4,Css("#b4e4ef")));
   skin.verticalScrollbar.fixedWidth=10*scale;skin.verticalScrollbarThumb.fixedWidth=10*scale;skin.verticalScrollbarThumb.fixedHeight=0;
   skin.horizontalScrollbar.fixedHeight=10*scale;skin.horizontalScrollbarThumb.fixedHeight=10*scale;skin.horizontalScrollbarThumb.fixedWidth=0;
   foreach(var style in new[]{skin.verticalScrollbarUpButton,skin.verticalScrollbarDownButton,skin.horizontalScrollbarLeftButton,skin.horizontalScrollbarRightButton}){BarStyle(style,null);style.fixedWidth=0;style.fixedHeight=0;}
   BarStyle(skin.horizontalSlider,ThinBar(false,2,Css("#6b9ca9")));
   BarStyle(skin.horizontalSliderThumb,Tex(Css("#bfeef3")));
   skin.horizontalSlider.fixedHeight=14*scale;skin.horizontalSliderThumb.fixedWidth=4*scale;skin.horizontalSliderThumb.fixedHeight=14*scale;
   BarStyle(skin.verticalSlider,ThinBar(true,2,Css("#6b9ca9")));
   BarStyle(skin.verticalSliderThumb,Tex(Css("#bfeef3")));
   skin.verticalSlider.fixedWidth=14*scale;skin.verticalSliderThumb.fixedWidth=14*scale;skin.verticalSliderThumb.fixedHeight=4*scale;
  }
  static bool HiddenToggle(bool current,string label,GUILayoutOption[] options){return current;}
  static bool HiddenButton(string label,GUILayoutOption[] options){return false;}
  internal static class RefinementHooks {
   static bool CursorDrawing(){return I==null||!I.ready||I.cursorVisibleConfig.Value;}
   static IEnumerable<CodeInstruction> RemoveSongAutoplay(IEnumerable<CodeInstruction> instructions){return RemoveDuplicate(instructions,"autoCursor","Toggle","HiddenToggle");}
   static IEnumerable<CodeInstruction> RemoveHelpLanguage(IEnumerable<CodeInstruction> instructions){return RemoveDuplicate(instructions,"chinese","Button","HiddenButton");}
   static IEnumerable<CodeInstruction> RemoveDuplicate(IEnumerable<CodeInstruction> instructions,string field,string target,string replacement){
    bool found=false;foreach(var code in instructions){
     var operand=code.operand as FieldInfo;if(code.opcode==OpCodes.Ldfld&&operand!=null&&operand.Name==field)found=true;
     var method=code.operand as MethodInfo;
     if(found&&method!=null&&method.DeclaringType==typeof(GUILayout)&&method.Name==target){code.operand=typeof(NativeEditorUIPlugin).GetMethod(replacement,F);code.opcode=OpCodes.Call;found=false;}
     yield return code;
    }
   }
  }
 }
}
