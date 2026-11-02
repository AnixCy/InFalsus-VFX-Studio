using UnityEngine;
using InFalsusStudio.NativeVisual;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void ReadabilityChoices(){
   MatchedToggle("判定显示",NativeReadability.Judgements.Value,v=>Queue(delegate{NativeReadability.Judgements.Value=v;}));
   MatchedToggle("轨道按键文字",NativeKeyLabels.Visible.Value,v=>Queue(delegate{NativeKeyLabels.Visible.Value=v;}));
  }
  void SideGuideChoice(){
   MatchedToggle("侧边音符视觉增强",NativeVisualPlugin.GetSideGuideMode()!=3,v=>Queue(delegate{NativeVisualPlugin.SetSideGuideMode(v?2:3);}));
  }
  void TrackLineChoices(){
   MatchedToggle("鼠标轨迹线",pointerTetherConfig.Value,v=>Queue(delegate{pointerTetherConfig.Value=v;}));
   MatchedToggle("轨道十字定位线",crosshairConfig.Value,v=>Queue(delegate{crosshairConfig.Value=v;}));
   MatchedToggle("轨道细线与高亮",NativeVisualPlugin.TrackLines.Value,delegate(bool value){Queue(delegate{NativeVisualPlugin.TrackLines.Value=value;});});
   MatchedToggle("侧轨红色三角形",NativeVisualPlugin.SideTriangles.Value,delegate(bool value){Queue(delegate{NativeVisualPlugin.SideTriangles.Value=value;});});
   MatchedToggle("血条",NativeVisualPlugin.ApGauge.Value,delegate(bool value){Queue(delegate{NativeVisualPlugin.ApGauge.Value=value;});});
  }
  void HitEffectChoice(){
   Rect row=ContentRow(62.796875f);RowLine(row);UiText(new Rect(0,row.y,156*scale,row.height),"打击特效",17,Css("#a6c3d1"));
   HitEffectStyle current=HitEffectSettings.Display(boot);
   if(FlatButton(new Rect(168*scale,row.y+15*scale,73*scale,33*scale),"无",15,current==HitEffectStyle.None))Queue(delegate{HitEffectSettings.Set(boot,HitEffectStyle.None);});
   if(FlatButton(new Rect(250*scale,row.y+15*scale,73*scale,33*scale),"简约",15,current==HitEffectStyle.Simple))Queue(delegate{HitEffectSettings.Set(boot,HitEffectStyle.Simple);});
   if(FlatButton(new Rect(332*scale,row.y+15*scale,73*scale,33*scale),"完全",15,current==HitEffectStyle.Full))Queue(delegate{HitEffectSettings.Set(boot,HitEffectStyle.Full);});
  }
  void CharacterEffectChoices(){
   Rect row=ContentRow(124);RowLine(row);UiText(new Rect(0,row.y+7*scale,405*scale,30*scale),"角色打击光效",17,Css("#a6c3d1"));
   HitCharacter current=NativeJudgementPresentation.Display(boot);
   HitCharacter[] choices={HitCharacter.Nia,HitCharacter.Mei,HitCharacter.Iris,HitCharacter.Ayame,HitCharacter.Story};
   for(int i=0;i<choices.Length;i++){
    HitCharacter role=choices[i];Rect buttonRect=new Rect(i*83*scale,row.y+43*scale,73*scale,33*scale);
    if(FlatButton(buttonRect,role.ToString(),15))Queue(delegate{NativeJudgementPresentation.Select(boot,role);});
    if(current==role)Border(buttonRect,NativeJudgementPresentation.Outline(role),2*scale);
   }
   UiText(new Rect(0,row.y+83*scale,405*scale,30*scale),"再次点击仅关闭角色光效",13,Css("#82aab9"));
  }
 }
}
