using System;using System.Collections.Generic;using InFalsusStudio.Core;using InFalsusStudio.NativeEditorUI;using InFalsusStudio.NativeVisual;using UnityEngine;
namespace InFalsusStudio.Gameplay {
 internal sealed class GameplayPresentationBridge:IDisposable {
  internal struct Pulse {internal JudgeNote Note;internal double Time;}
  internal readonly List<Pulse> Pulses=new List<Pulse>();
  internal struct Judgement {internal JudgeNote Note;internal int Grade;internal float Started;internal double Error;}
  internal readonly Judgement[] Judgements=new Judgement[7];
  readonly double[] trackHitTimes={double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity};GUIStyle text;
  internal GameplayPresentationBridge(StudioBootstrap owner){}
  internal static void ResetScoreAnimation(){var type=typeof(NativeVisualPlugin).Assembly.GetType("InFalsusStudio.NativeVisual.NativePresentation");type.GetField("scoreTime",NativeEditorUIPlugin.F).SetValue(null,-1d);}
  internal void Tick(){var engine=GameplaySession.Engine;double now=GameplaySession.Now;
   foreach(var result in engine.Results){var note=result.Note.Source;int lane=note.Kind==EventKind.SkyArea||note.Kind==EventKind.Flick?6:note.Lane;Judgements[lane]=new Judgement{Note=result.Note,Grade=result.Grade,Started=Time.realtimeSinceStartup,Error=result.Error};if(result.Grade==1)continue;if(note.Kind==EventKind.Tap||note.Kind==EventKind.Hold)TrackHit(note,now);if(note.Kind!=EventKind.SkyArea)Pulses.Add(new Pulse{Note=result.Note,Time=now});}
   Pulses.RemoveAll(p=>now-p.Time>1500);foreach(var n in engine.Notes){if(n.Source.TimeMs>now)break;if(n.Source.Kind==EventKind.Hold&&n.Source.EndMs>=now&&engine.Holding(n,GameplaySession.InputState))TrackHit(n.Source,now);}
  }
  internal void StopSound(){if(GameplaySession.Audio!=null)GameplaySession.Audio.Stop();}
  public void Dispose(){Pulses.Clear();Array.Clear(Judgements,0,Judgements.Length);}
  internal static float PressPower(bool held,double elapsedMs){return held?(float)(1-Math.Min(.5,Math.Max(0,elapsedMs)/500)):0;}
  internal static float PressModulation(bool held,double elapsedMs){return held?(float)Math.Max(0,1-Math.Max(0,elapsedMs)/500):1;}
  // Track._FBA reads the last note-feedback timestamp at input slot +0x28,
  // not the key-change timestamp at +0x30. Plain key-down must not start a hit flash.
  internal static float FloorPressPower(bool held,double releasedMs,double hitMs){double gate=held?1:Math.Max(0,1-Math.Max(0,releasedMs)/500);return (float)(gate*(1-Math.Min(.5,Math.Max(0,hitMs)/500)));}
  internal static float FloorPressModulation(double hitMs){return (float)Math.Max(0,1-Math.Max(0,hitMs)/500);}
  void TrackHit(SpcEvent note,double now){var input=GameplaySession.InputState;int first=Math.Max(1,note.Lane),last=Math.Min(4,note.Lane+(int)Math.Ceiling(note.GroundWidth)-1);if(note.Lane==0||note.Lane==5)return;for(int lane=first;lane<=last;lane++)if(input.Pressed[lane]||note.Kind==EventKind.Hold&&input.Held[lane])trackHitTimes[lane]=now;}
  internal float[] Powers(){var p=new float[6];if(GameplaySession.Phase!=GameplayPhase.Playing)return p;var input=GameplaySession.InputState;double now=GameplaySession.Now;for(int i=0;i<6;i++)p[i]=i==0||i==5?PressPower(input.Held[i],now-input.LastPress[i]):FloorPressPower(input.Held[i],now-input.LastRelease[i],now-trackHitTimes[i]);return p;}
  internal float Modulation(int lane){return GameplaySession.Phase==GameplayPhase.Playing?FloorPressModulation(GameplaySession.Now-trackHitTimes[lane]):0;}
  internal void DrawHud(){
   // The preparation countdown is transient. Playing/results use only the
   // original score/combo HUD and native judgement artwork at the track.
   if(GameplaySession.Phase!=GameplayPhase.LeadIn)return;
   var ui=NativeEditorUIPlugin.I;float scale=Mathf.Max(.6f,Mathf.Min(Screen.width/1920f,Screen.height/1080f));
   if(text==null)text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(20*scale),wordWrap=false};
   string value=(ui.ChineseGameplay?"准备 — ":"Ready — ")+Math.Max(0,GameplaySession.LeadDeadline-Time.realtimeSinceStartupAsDouble).ToString("0.0")+" s";
   var rect=new Rect(0,155*scale,Screen.width,32*scale);text.normal.textColor=Color.black;var shadow=rect;shadow.x++;shadow.y++;GUI.Label(shadow,value,text);text.normal.textColor=Color.white;GUI.Label(rect,value,text);
  }
 }
}
