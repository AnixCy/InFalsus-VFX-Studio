using System;
using System.Diagnostics;
using System.Threading;
using InFalsusStudio.Core;
namespace InFalsusStudio.Gameplay {
 internal struct GameplayAudioCommand {internal long Generation,Sequence,InputTicks,ReceivedTicks,RequestTicks;internal double TargetFrame;internal int SourceId,SoundEpoch;internal HitSoundKind Kind;internal float Gain;}
 internal sealed class GameplayAudioDiagnostics {
  internal long Submitted,Mixed,Dropped,CallbackErrors,Underruns,LateInputs;internal double MaxSubmitMixMs;
  readonly double[] delays=new double[4096],delivery=new double[4096],dispatch=new double[4096];int count,timed;
  internal void Record(long request,long input,long received){if(input>0&&received>=input){int j=Interlocked.Increment(ref timed)-1;delivery[j%delivery.Length]=(received-input)*1000d/Stopwatch.Frequency;dispatch[j%dispatch.Length]=(request-received)*1000d/Stopwatch.Frequency;}double ms=(Stopwatch.GetTimestamp()-request)*1000d/Stopwatch.Frequency;int i=Interlocked.Increment(ref count)-1;delays[i%delays.Length]=ms;Interlocked.Increment(ref Mixed);if(ms>MaxSubmitMixMs)MaxSubmitMixMs=ms;}
  internal void ResetMeasurements(){Interlocked.Exchange(ref count,0);Interlocked.Exchange(ref Mixed,0);MaxSubmitMixMs=0;}
  internal double DeliveryP95(){return Percentile(delivery,timed);}
  internal double DispatchP95(){return Percentile(dispatch,timed);}
  static double Percentile(double[] source,int total){int n=Math.Min(total,source.Length);if(n<=0)return 0;var a=new double[n];Array.Copy(source,a,n);Array.Sort(a);return a[Math.Min(n-1,(int)(n*.95))];}
  internal double P95(){int n=Math.Min(Volatile.Read(ref count),delays.Length);if(n==0)return 0;var a=new double[n];Array.Copy(delays,a,n);Array.Sort(a);return a[Math.Min(n-1,(int)(n*.95))];}
 }
 internal sealed class GameplayAudioScheduler {
  struct Voice {internal PcmSample Sample;internal double Position,Start;internal float Gain;internal long Request,Input,Received;internal bool Counted;}
  readonly GameplayAudioCommand[] queue=new GameplayAudioCommand[2048];readonly Voice[] voices=new Voice[128];readonly PcmSample[] samples;readonly long generation;int read,write;long serial;
  internal readonly GameplayAudioDiagnostics Diagnostics=new GameplayAudioDiagnostics();
  int enabled,epoch,mixedEpoch;
  internal bool Enabled {get{return Volatile.Read(ref enabled)!=0;}set{int next=value?1:0;if(Volatile.Read(ref enabled)!=next){Interlocked.Increment(ref epoch);Volatile.Write(ref enabled,next);}}}internal volatile float Master,Attack,Sustain;internal volatile bool Floor,Sky;
  float floorGain,skyGain;double floorPhase,skyPhase;bool lastEnabled;
  internal GameplayAudioScheduler(PcmSample[] samples,long generation){this.samples=samples;this.generation=generation;}
  internal void Submit(HitSoundKind kind,int source,double target,long inputTicks,long receivedTicks=0){
   if(!Enabled)return;int next=(write+1)%queue.Length;if(next==Volatile.Read(ref read)){Interlocked.Increment(ref Diagnostics.Dropped);return;}
   queue[write]=new GameplayAudioCommand{Generation=generation,SoundEpoch=Volatile.Read(ref epoch),Sequence=++serial,SourceId=source,Kind=kind,Gain=1,TargetFrame=target,InputTicks=inputTicks,ReceivedTicks=receivedTicks,RequestTicks=Stopwatch.GetTimestamp()};Volatile.Write(ref write,next);Interlocked.Increment(ref Diagnostics.Submitted);
  }
  internal void Clear(){Enabled=false;Floor=Sky=false;}
  internal void Mix(float[] data,int frames,int channels,int hz,long start){
   bool enabled=Enabled;int currentEpoch=Volatile.Read(ref epoch);if(currentEpoch!=mixedEpoch){Array.Clear(voices,0,voices.Length);floorGain=skyGain=0;mixedEpoch=currentEpoch;}
   if(!enabled&&lastEnabled){Array.Clear(voices,0,voices.Length);floorGain=skyGain=0;}
   lastEnabled=enabled;
   while(read!=Volatile.Read(ref write)){
    var cmd=queue[read];int kind=(int)cmd.Kind;Volatile.Write(ref read,(read+1)%queue.Length);
    if(!enabled||cmd.SoundEpoch!=currentEpoch||cmd.Generation!=generation||kind<0||kind>=samples.Length||samples[kind]==null)continue;
    int slot=-1;for(int i=0;i<voices.Length;i++)if(voices[i].Sample==null){slot=i;break;}
    if(slot<0){Interlocked.Increment(ref Diagnostics.Dropped);continue;}
    voices[slot]=new Voice{Sample=samples[kind],Start=Math.Max(start,cmd.TargetFrame),Gain=cmd.Gain,Request=cmd.RequestTicks,Input=cmd.InputTicks,Received=cmd.ReceivedTicks};
   }
   if(!enabled)return;
   for(int i=0;i<voices.Length;i++){
    var v=voices[i];if(v.Sample==null)continue;int offset=(int)Math.Max(0,Math.Ceiling(v.Start-start));if(offset>=frames)continue;
    if(!v.Counted){Diagnostics.Record(v.Request,v.Input,v.Received);v.Counted=true;}
    double step=(double)v.Sample.Frequency/hz;
    for(int f=offset;f<frames&&v.Position<v.Sample.Frames;f++,v.Position+=step)for(int c=0;c<channels;c++)data[f*channels+c]+=v.Sample.At(v.Position,c,false)*v.Gain*Master*Attack;
    if(v.Position>=v.Sample.Frames)v.Sample=null;voices[i]=v;
   }
   Bed(data,frames,channels,hz,HitSoundKind.FloorHold,Floor,ref floorGain,ref floorPhase);
   Bed(data,frames,channels,hz,HitSoundKind.SkyHold,Sky,ref skyGain,ref skyPhase);
  }
  void Bed(float[] data,int frames,int channels,int hz,HitSoundKind kind,bool active,ref float gain,ref double phase){
   int k=(int)kind;if(k>=samples.Length||samples[k]==null)return;var sample=samples[k];float target=active?Master*Sustain:0,stepGain=1f/Math.Max(1,hz*.003f);double step=(double)sample.Frequency/hz;
   for(int f=0;f<frames;f++){gain=gain<target?Math.Min(target,gain+stepGain):Math.Max(target,gain-stepGain);if(gain<=0){phase=0;continue;}for(int c=0;c<channels;c++)data[f*channels+c]+=sample.At(phase,c,true)*gain;phase=(phase+step)%Math.Max(1,sample.Frames);}
  }
 }
}
