using UnityEngine;
using InFalsusStudio.NativeVisual;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void ProgressStyleChoice(){
   Rect row=ContentRow(62.796875f);RowLine(row);UiText(new Rect(0,row.y,156*scale,row.height),"进度条样式",17,Css("#a6c3d1"));
   var current=NativeVisualPlugin.ProgressStyle.Value;
   if(FlatButton(new Rect(168*scale,row.y+15*scale,112*scale,33*scale),"内嵌细条",15,current==ProgressBarStyle.Embedded))Queue(delegate{NativeProgress.SetStyle(boot,ProgressBarStyle.Embedded);});
   if(FlatButton(new Rect(293*scale,row.y+15*scale,112*scale,33*scale),"下方宽条",15,current==ProgressBarStyle.External))Queue(delegate{NativeProgress.SetStyle(boot,ProgressBarStyle.External);});
  }
 }
}
