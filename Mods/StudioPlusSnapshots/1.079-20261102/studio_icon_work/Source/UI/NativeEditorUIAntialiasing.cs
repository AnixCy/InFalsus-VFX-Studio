using System;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.NativeVisual;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void InitAntialiasing(){AntialiasingSettings.Init(Config);harmony.Patch(Method("LineGui"),new HarmonyMethod(typeof(AntiAliasHooks).GetMethod("Line",F)));}
  void TickAntialiasing(){if(boot!=null)AntialiasingSettings.Tick((Camera)Prop(boot,"ViewCamera"));}
  void AntialiasingChoice(){
   var row=ContentRow(103);RowLine(row);UiText(new Rect(0,row.y,405*scale,30*scale),"抗锯齿",17,Css("#a6c3d1"));
   string[] names={"无","FXAA","SMAA","MSAA 4×"};for(int i=0;i<names.Length;i++){var mode=(DisplayAntialiasing)i;bool enabled=GUI.enabled;GUI.enabled=enabled&&(mode!=DisplayAntialiasing.MSAA4||AntialiasingSettings.CanMsaa);try{if(FlatButton(new Rect(i*104*scale,row.y+36*scale,93*scale,32*scale),names[i],14,AntialiasingSettings.Setting.Value==mode))Queue(delegate{AntialiasingSettings.Setting.Value=mode;});}finally{GUI.enabled=enabled;}}
   UiText(new Rect(0,row.y+72*scale,405*scale,29*scale),AntialiasingSettings.Status.Length>0?AntialiasingSettings.Status:"SMAA 推荐 · 同时平滑编辑辅助线",12,Css("#789eae"));
  }
  static class AntiAliasHooks {static bool Line(Vector2 __0,Vector2 __1,Color __2,float __3){return !AntialiasingSettings.DrawLine(__0,__1,__2,__3);}}
 }
}
