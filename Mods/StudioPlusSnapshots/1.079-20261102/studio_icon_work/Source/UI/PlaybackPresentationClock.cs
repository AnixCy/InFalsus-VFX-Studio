using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio;
namespace InFalsusStudio.NativeEditorUI {
 // DSP remains authoritative for audio scheduling, pause/seek, loops and rate changes.
 // Only presentation consumers receive a continuously advancing, gently corrected clock.
 internal sealed class PresentationClockFilter {
  double dsp,wall;bool ready;
  internal void Reset(){ready=false;}
  internal double Step(double observed,double realtime,double blockSeconds){
   double dt=realtime-wall;
   if(!ready||dt<0||dt>.25||Math.Abs(observed-dsp)>.25){ready=true;wall=realtime;dsp=observed;return dsp;}
   wall=realtime;double predicted=dsp+dt;
   // A one-second correction avoids reproducing the audio buffer's stair steps.
   double adjustment=(observed-predicted)*(1-Math.Exp(-dt));
   adjustment=Math.Max(-dt*.1,Math.Min(dt*.1,adjustment));
   dsp=Math.Min(predicted+adjustment,observed+Math.Max(.05,blockSeconds*2));
   return dsp;
  }
 }
 internal static class PlaybackPresentationClock {
  static readonly PresentationClockFilter filter=new PresentationClockFilter();
  static readonly FieldInfo transportField=typeof(StudioBootstrap).GetField("transport",NativeEditorUIPlugin.F);
  static StudioTransport current;static int revision=-1,frame=-1;static double value,blockSeconds=.022;internal static double outputLatencySeconds=.08533333333333333;
  internal static void Init(ConfigFile config,Harmony harmony){
   DeviceChanged(false);AudioSettings.OnAudioConfigurationChanged+=DeviceChanged;
   var f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
   harmony.Patch(typeof(StudioBootstrap).GetMethod("LateUpdate",f),transpiler:new HarmonyMethod(typeof(PlaybackPresentationClock).GetMethod("VisualTimeCalls",f)));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("TickSession",f),transpiler:new HarmonyMethod(typeof(PlaybackPresentationClock).GetMethod("SessionTimeCalls",f)));
   harmony.Patch(typeof(StudioBootstrap).GetProperty("CurrentMs").GetGetMethod(),new HarmonyMethod(typeof(PlaybackPresentationClock).GetMethod("CurrentTime",f)));
  }
  static void DeviceChanged(bool changed){int length,count;AudioSettings.GetDSPBufferSize(out length,out count);if(AudioSettings.outputSampleRate>0){blockSeconds=(double)length/AudioSettings.outputSampleRate;outputLatencySeconds=(double)(length*Math.Max(1,count))/AudioSettings.outputSampleRate;}Reset();}
  internal static void Reset(){filter.Reset();current=null;revision=-1;frame=-1;}
  internal static double TimeFor(StudioTransport transport){if(InFalsusStudio.Gameplay.GameplayPresentationAdapter.Owns(transport))return InFalsusStudio.Gameplay.GameplayPresentationAdapter.DisplayTime;return BaseTimeFor(transport);}
  internal static double BaseTimeFor(StudioTransport transport){
   if(InFalsusStudio.VideoRender.RenderClock.Active)return InFalsusStudio.VideoRender.RenderClock.NowMs;
   if(transport==null)return transport==null?0:transport.TimeMs;
   if(!transport.Playing){Reset();return transport.TimeMs;}
   if(!ReferenceEquals(current,transport)||revision!=transport.Revision){filter.Reset();current=transport;revision=transport.Revision;frame=-1;}
   if(frame==Time.frameCount)return value;
   double smoothed=filter.Step(AudioSettings.dspTime,Time.realtimeSinceStartupAsDouble,blockSeconds);
   value=Math.Max(transport.PlaybackStartMs,transport.ChartTimeAtDsp(smoothed));frame=Time.frameCount;return value;
  }
  static bool CurrentTime(StudioBootstrap __instance,ref double __result){
   var transport=(StudioTransport)transportField.GetValue(__instance);
   if(InFalsusStudio.Gameplay.GameplayPresentationAdapter.Owns(transport)){__result=InFalsusStudio.Gameplay.GameplayPresentationAdapter.DisplayTime;return false;}
   if(transport==null||!transport.Playing)return true;
   __result=TimeFor(transport);return false;
  }
  static IEnumerable<CodeInstruction> VisualTimeCalls(IEnumerable<CodeInstruction> codes){
   var getter=typeof(StudioTransport).GetProperty("TimeMs").GetGetMethod();var replacement=typeof(PlaybackPresentationClock).GetMethod("TimeFor",NativeEditorUIPlugin.F);int found=0;
   foreach(var code in codes){if(Equals(code.operand,getter)){
     // The first read is the real end-of-song guard; all later reads feed visual consumers.
     if(found++>0){code.opcode=OpCodes.Call;code.operand=replacement;}
    }yield return code;
   }
   if(found!=5)throw new InvalidOperationException("Playback visual-time call sites changed; expected five transport reads.");
  }
  static double AuthoritativeTime(StudioBootstrap boot){var transport=(StudioTransport)transportField.GetValue(boot);return transport==null?boot.CurrentMs:transport.TimeMs;}
  static IEnumerable<CodeInstruction> SessionTimeCalls(IEnumerable<CodeInstruction> codes){
   var getter=typeof(StudioBootstrap).GetProperty("CurrentMs").GetGetMethod();var replacement=typeof(PlaybackPresentationClock).GetMethod("AuthoritativeTime",NativeEditorUIPlugin.F);int found=0;
   foreach(var code in codes){if(Equals(code.operand,getter)){found++;code.opcode=OpCodes.Call;code.operand=replacement;}yield return code;}
   if(found!=2)throw new InvalidOperationException("Loop time call sites changed; expected two authoritative reads.");
  }
  internal static void Dispose(){AudioSettings.OnAudioConfigurationChanged-=DeviceChanged;Reset();}
 }
}
