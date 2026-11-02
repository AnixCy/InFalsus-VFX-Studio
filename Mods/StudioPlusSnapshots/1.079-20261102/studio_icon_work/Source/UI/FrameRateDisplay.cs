using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 internal sealed class FrameRateSample {
  double elapsed;int frames;
  internal double Fps,Milliseconds;internal string Text="FPS --";
  internal void Reset(){elapsed=0;frames=0;Fps=0;Milliseconds=0;Text="FPS --";}
  internal bool Step(double seconds){
   if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0)return false;
   elapsed+=seconds;frames++;if(elapsed<.5)return false;
   Fps=frames/elapsed;Milliseconds=elapsed*1000/frames;elapsed=0;frames=0;
   Text="FPS "+Fps.ToString("0.0",CultureInfo.InvariantCulture)+"  /  "+Milliseconds.ToString("0.0",CultureInfo.InvariantCulture)+" ms";return true;
  }
 }
 public sealed partial class NativeEditorUIPlugin {
  ConfigEntry<bool> frameRateConfig,frameDetailsConfig;readonly FramePacingSample framePacing=new FramePacingSample();readonly FrameRateSample frameRate=new FrameRateSample();
  void InitFrameRate(){TargetFrameRatePolicy.Init(Config);PlaybackPresentationClock.Init(Config,harmony);frameDetailsConfig=Config.Bind("Display","ShowFrameTimeDetails",true,"With ShowFrameRate enabled, display a rolling five-second P99, worst frame and count of frames over 16.7ms. Uses bounded reusable arrays.");frameRateConfig=Config.Bind("Display","ShowFrameRate",false,"Show the measured frame rate and average frame time. Samples Update once per frame; does not change frame limits or VSync.");}
  void TickFrameRate(){TargetFrameRatePolicy.Tick();if(frameRateConfig==null||!frameRateConfig.Value){frameRate.Reset();framePacing.Reset();return;}frameRate.Step(Time.unscaledDeltaTime);if(frameDetailsConfig.Value)framePacing.Step(Time.unscaledDeltaTime);else framePacing.Reset();}
  void DrawFrameRate(){
   if(frameRateConfig==null||!frameRateConfig.Value||Event.current.type!=EventType.Repaint)return;
   float s=Mathf.Max(.8f,scale);bool chrome=!formal&&boot!=null&&Val<bool>(boot,"showUi");
   bool details=frameDetailsConfig.Value;float height=details?51*s:28*s;var rect=new Rect(chrome?leftRect.xMax+12*s:14*s,chrome?footerRect.y-height-6*s:Screen.height-height-12*s,details?Mathf.Min(480*s,Screen.width-90*s):230*s,height);
   Color before=GUI.color;try{GUI.color=Color.white;Fill(rect,Css("#091923dc"));UiText(new Rect(rect.x+8*s,rect.y,rect.width-16*s,28*s),frameRate.Text,14,Css("#bfeef0"),NumFace,0);if(details)UiText(new Rect(rect.x+8*s,rect.y+27*s,rect.width-16*s,22*s),framePacing.Text,12,Css("#a5c6d3"),NumFace,0);}finally{GUI.color=before;}
  }
 }
}
