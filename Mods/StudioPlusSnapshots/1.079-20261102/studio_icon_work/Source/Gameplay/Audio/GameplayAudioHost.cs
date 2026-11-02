using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.NativeEditorUI;
namespace InFalsusStudio.Gameplay {
 internal sealed class GameplayAudioHost:IDisposable {
  static long generation;internal readonly FmodGameplayBackend Backend;internal readonly GameplayTransport Transport;internal GameplayAudioScheduler Scheduler {get{return Backend.Scheduler;}}internal GameplayClockBridge Clock {get{return Backend.Clock;}}
  internal GameplayAudioHost(InFalsusStudio.StudioTransport editor,PcmSample[] samples,double entry){
   string runtime=Path.Combine(Path.GetDirectoryName(typeof(GameplayAudioHost).Assembly.Location),"Native","InfStudioFmod.dll");
   Backend=new FmodGameplayBackend(GameplayOptions.BufferFrames.Value,runtime);
   try{
    long before=Stopwatch.GetTimestamp();double unity=Time.realtimeSinceStartupAsDouble;long after=Stopwatch.GetTimestamp();Clock.BindInputClock(unity,before,after);Clock.Begin(Interlocked.Increment(ref generation));
    Transport=new GameplayTransport(Clock,entry,editor.Rate,editor.AudioOffsetMs,GameplayOptions.Latency.Value,GameplayOptions.VisualLatency.Value,Clock.Generation);Backend.Transport=Transport;Backend.Scheduler=new GameplayAudioScheduler(samples,Transport.Generation);
    PcmSample music=null;if(editor.Clip){var clip=editor.Clip;long size=(long)clip.samples*clip.channels;if(size>128L*1024*1024)throw new IOException("Music PCM exceeds the 512 MiB gameplay budget");var data=new float[(int)size];if(!(bool)typeof(AudioClip).GetMethod("GetData",new[]{typeof(float[]),typeof(int)}).Invoke(clip,new object[]{data,0}))throw new IOException("Cannot read music PCM for gameplay");music=new PcmSample(data,clip.channels,clip.frequency);}
    Backend.Music=new GameplayMusicPcm(music,Transport.Rate,entry+Transport.Offset,Backend.Frequency);UpdateOptions();
   }catch{Backend.Dispose();throw;}
  }
  internal bool Ready {get{return Clock.Ready&&Backend.Music.Ready;}}
  internal string Failure {get{return Backend.Failure??(Backend.Music.Error==null?null:Backend.Music.Error.Message);}}
  internal void UpdateOptions(){Backend.MusicVolume=GameplayOptions.Music.Value;var s=Scheduler;s.Master=GameplayOptions.Master.Value;s.Attack=GameplayOptions.Attack.Value;s.Sustain=GameplayOptions.Sustain.Value;s.Enabled=GameplayOptions.Sounds.Value;}
  internal void Start(){if(!Ready)throw new InvalidOperationException("Gameplay audio is still preparing");Transport.Start();}
  internal void Stop(){Transport.Playing=false;Scheduler.Clear();}
  public void Dispose(){Stop();Backend.Dispose();}
 }
}
