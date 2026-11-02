using UnityEngine;
using InFalsusStudio.Performance;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void DrawEndpointDragMode(){
   if(formal||!boot.HasDocument||!Val<bool>(boot,"showNotes"))return;
   bool endpoint=EndpointDragMode.Notice(boot)||(bool)Prop(boot,"EditHeld");
   bool nodeNotice=Time.unscaledTime<skyNodeNoticeUntil;
   if(!nodeNotice&&!endpoint&&!skyNodeMode)return;
   string text=nodeNotice||!endpoint?(skyNodeMode?"节点编辑：开启 · ":"节点编辑：关闭 · ")+HotkeyLabel("skyNode")+" 切换":(EndpointDragMode.TimeOnly(boot)?"时间拖动 · ":"位置拖动 · ")+HotkeyLabel("endpoint")+" 切换";
   Rect song=Val<Rect>(boot,"hudSongRect");
   float width=UiFontRaster.Measure(T(text),CNFace,FontPixels(15))+16*scale;
   var area=new Rect(Mathf.Max(viewport.xMin,song.xMax-width),song.yMax+5*scale,width,26*scale);
   Fill(area,Css("#091c25ef"));
   UiText(area,text,15,Css("#d3edf0"),CNFace,1);
  }
 }
}
