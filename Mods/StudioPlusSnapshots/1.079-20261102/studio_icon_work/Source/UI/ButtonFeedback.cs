using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  const double ButtonPulseSeconds=.18;
  struct ButtonPulse {
   internal int Control,Frame;internal string Identity,Panel,Tab;internal double Started;
  }
  readonly List<ButtonPulse> buttonPulses=new List<ButtonPulse>(16);
  ButtonPulse heldButton;int heldButtonHotControl;
  bool SameButton(ButtonPulse pulse,int control,string identity){return pulse.Control==control&&pulse.Identity==identity&&pulse.Panel==panel&&pulse.Tab==settingsTab;}
  static bool PulseExpired(ButtonPulse pulse,double now){return pulse.Started<0?Time.frameCount>pulse.Frame+2:now-pulse.Started>=ButtonPulseSeconds;}
  bool FeedbackButton(Rect rect,string identity,GUIContent content=null){
   if(timingPaintActive&&!TimingRectVisible(rect))return false;
   // An extra passive ID is requested on every GUI event. The native button
   // retains mouse capture, disabled handling, keyboard behavior and hit bounds.
   int control=GUIUtility.GetControlID(FocusType.Passive,rect);bool enabled=GUI.enabled;
   EventType type=Event.current.type;bool inside=rect.Contains(Event.current.mousePosition);int previousHot=GUIUtility.hotControl;
   bool clicked=GUI.Button(rect,content??GUIContent.none,GUIStyle.none);
   double now=Time.realtimeSinceStartupAsDouble;
   if(enabled&&type==EventType.MouseDown&&Event.current.button==0&&inside&&GUIUtility.hotControl!=0&&GUIUtility.hotControl!=previousHot){
    heldButton=new ButtonPulse{Control=control,Identity=identity,Panel=panel,Tab=settingsTab};heldButtonHotControl=GUIUtility.hotControl;
   }
   if(clicked&&enabled){
    // Keep only a small, bounded set of recent confirmations. A repeated click
    // restarts its pulse; painting never queues or repeats the action itself.
    for(int i=buttonPulses.Count-1;i>=0;i--)if(PulseExpired(buttonPulses[i],now)||SameButton(buttonPulses[i],control,identity))buttonPulses.RemoveAt(i);
    if(buttonPulses.Count==16)buttonPulses.RemoveAt(0);
    buttonPulses.Add(new ButtonPulse{Control=control,Identity=identity,Panel=panel,Tab=settingsTab,Started=-1,Frame=Time.frameCount});
   }
   if(enabled&&type==EventType.Repaint){
    float opacity=heldButtonHotControl!=0&&heldButtonHotControl==GUIUtility.hotControl&&SameButton(heldButton,control,identity)&&inside?.12f:0;
    for(int i=buttonPulses.Count-1;i>=0;i--){
     var pulse=buttonPulses[i];if(PulseExpired(pulse,now)){buttonPulses.RemoveAt(i);continue;}
     if(SameButton(pulse,control,identity)){
      // Start on the first painted confirmation, so a slow queued operation
      // cannot consume the whole flash before any updated frame is visible.
      if(pulse.Started<0){pulse.Started=now;buttonPulses[i]=pulse;}
      opacity=Mathf.Max(opacity,.26f*(1-(float)((now-pulse.Started)/ButtonPulseSeconds)));
     }
    }
    if(opacity>0)Fill(rect,new Color(1,1,1,opacity));
   }
   return clicked;
  }
 }
}
