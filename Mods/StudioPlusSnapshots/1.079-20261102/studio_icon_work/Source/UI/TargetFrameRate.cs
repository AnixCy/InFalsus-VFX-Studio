using System;
using BepInEx.Configuration;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 internal static class TargetFrameRatePolicy {
  internal static ConfigEntry<int> Setting;internal static ConfigEntry<bool> VerticalSync;
  static int originalTarget,originalSync,defaultTarget;static bool ready;
  internal static bool Allowed(int value){return value==60||value==120||value==170||value==-1;}
  internal static void Init(ConfigFile config){
   originalTarget=Application.targetFrameRate;originalSync=QualitySettings.vSyncCount;defaultTarget=Allowed(originalTarget)?originalTarget:-1;ready=true;
   Setting=config.Bind("Display","TargetFrameRate",defaultTarget,"Windows frame limit when VerticalSync is off: 60 / 120 / 170 / -1 (unlimited). Audio and song time are unchanged.");
   VerticalSync=config.Bind("Display","VerticalSync",true,"Synchronize rendering to the display refresh rate. Overrides the software frame cap while enabled; disable for the existing 60/120/170/unlimited targets.");
   if(!Allowed(Setting.Value))Setting.Value=defaultTarget;Tick();
  }
  internal static void Tick(){if(!ready||Setting==null)return;if(!Allowed(Setting.Value))Setting.Value=defaultTarget;int sync=VerticalSync.Value?1:0,target=VerticalSync.Value?-1:Setting.Value;if(QualitySettings.vSyncCount!=sync)QualitySettings.vSyncCount=sync;if(Application.targetFrameRate!=target)Application.targetFrameRate=target;}
  internal static void Dispose(){if(!ready)return;Application.targetFrameRate=originalTarget;QualitySettings.vSyncCount=originalSync;ready=false;Setting=null;VerticalSync=null;}
 }
 public sealed partial class NativeEditorUIPlugin {
  void TargetFrameRateChoice(){
   MatchedToggle("垂直同步（跟随屏幕）",TargetFrameRatePolicy.VerticalSync.Value,delegate(bool value){Queue(delegate{TargetFrameRatePolicy.VerticalSync.Value=value;TargetFrameRatePolicy.Tick();frameRate.Reset();});});
   Rect row=ContentRow(94);RowLine(row);UiText(new Rect(0,row.y,405*scale,36*scale),"目标帧率",17,Css("#a6c3d1"));
   int[] values={60,120,170,-1};string[] labels={"60 Hz","120 Hz","170 Hz","无限制"};bool before=GUI.enabled;GUI.enabled=before&&!TargetFrameRatePolicy.VerticalSync.Value;
   try{for(int i=0;i<values.Length;i++){int value=values[i];if(FlatButton(new Rect(i*104*scale,row.y+43*scale,93*scale,32*scale),labels[i],14,TargetFrameRatePolicy.Setting.Value==value))Queue(delegate{TargetFrameRatePolicy.Setting.Value=value;TargetFrameRatePolicy.Tick();frameRate.Reset();});}}finally{GUI.enabled=before;}
   Rect help=ContentRow(94);UiParagraph(new Rect(0,help.y+8*scale,405*scale,86*scale),"同步开启时跟随屏幕刷新率；关闭后使用上方帧率上限。同步可能增加少量输入延迟，可随时切换。",13,Css("#7c9dad"));
  }
 }
}
