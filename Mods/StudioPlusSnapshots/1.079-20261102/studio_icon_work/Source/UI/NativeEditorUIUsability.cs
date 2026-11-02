using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool HistoryAvailable(bool redo){var history=boot==null?null:boot.History;return history!=null&&(redo?history.RedoCount:history.UndoCount)>0;}
  void HistoryButton(Rect rect,bool redo){
   bool previous=GUI.enabled,available=HistoryAvailable(redo);GUI.enabled=previous&&available;
   try{string title=redo?"重做":"撤销",tip=available?title:redo?"重做 · 无可重做操作":"撤销 · 无可撤销操作";bool clicked=IconButton(rect,redo?"redo":"undo",tip,false);if(available&&clicked)Command("Undo",redo);}
   finally{GUI.enabled=previous;}
  }
  Vector2 HoverSize(string text,GUIStyle style){
   int pixels=FontPixels(17);float padding=24*scale,limit=Mathf.Max(padding+1,Mathf.Min(Screen.width-20,600*scale));
   float natural=0;foreach(var line in text.Split('\n'))natural=Mathf.Max(natural,UiFontRaster.Measure(line.TrimEnd('\r'),CNFace,pixels));
   float width=Mathf.Min(limit,Mathf.Max(padding+1,natural+padding));
   int lines=UiFontRaster.Wrap(text,CNFace,pixels,Mathf.Max(1,width-padding)).Length;
   return new Vector2(Mathf.Ceil(width),Mathf.Ceil(Mathf.Max(28*scale,lines*(pixels+3)+10*scale)));
  }
  string CurrentProjectFolder(){
   if(boot==null||!boot.HasDocument)return null;
   var project=Get(boot,"songProject") as SongFolderProject;
   string loaded=Val<string>(boot,"loadedPath"),folder=project!=null?project.Root:string.IsNullOrEmpty(loaded)?null:Path.GetDirectoryName(loaded);
   return string.IsNullOrEmpty(folder)||!Directory.Exists(folder)?null:Path.GetFullPath(folder);
  }
  static ProcessStartInfo ProjectFolderStartInfo(string folder){
   string full=Path.GetFullPath(folder);if(full.EndsWith("\\",StringComparison.Ordinal))full+=".";
   return new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),Arguments="\""+full+"\"",UseShellExecute=true};
  }
  void ProjectFolderButton(Rect rect){
   bool previous=GUI.enabled;GUI.enabled=previous&&CurrentProjectFolder()!=null;
   try{if(FlatButton(rect,"打开谱面文件夹",17))Queue(OpenProjectFolder);}finally{GUI.enabled=previous;}
  }
  void OpenProjectFolder(){
   string folder=CurrentProjectFolder();if(folder==null){Set(boot,"status",T("当前谱面文件夹不存在或尚未打开谱面。"));return;}
   try{using(Process.Start(ProjectFolderStartInfo(folder))){} }
   catch(Exception error){Set(boot,"status",T("无法打开谱面文件夹：")+error.Message);Logger.LogWarning("Unable to open current chart folder: "+error.Message);}
  }
 }
}
