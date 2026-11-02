using System;
using System.Diagnostics;
using System.Threading;
namespace InFalsusStudio.Gameplay {
 // Immutable playback epochs, with a seqlock snapshot written only by the mixer.
 internal sealed class GameplayClockBridge {
  struct Segment {internal long Ticks;internal double Frame,Speed;}
  readonly Segment[] history=new Segment[1024];readonly double[] errors=new double[31],ordered=new double[31];long sequence,ticks,firstTicks;int hz,block,head=-1,errorCount,errorHead;double unityOrigin;long tickOrigin;
  internal long Generation {get;private set;}
  internal int Frequency {get{return Volatile.Read(ref hz);}}
  internal int BlockFrames {get{return Volatile.Read(ref block);}}
  internal bool Ready {get{return Interlocked.Read(ref firstTicks)!=0;}}
  internal long LastCallbackTicks {get{return Interlocked.Read(ref ticks);}}
  internal void BindInputClock(double unitySeconds,long before,long after){unityOrigin=unitySeconds;tickOrigin=before+(after-before)/2;}
  internal long InputTicks(double unitySeconds){return tickOrigin+(long)Math.Round((unitySeconds-unityOrigin)*Stopwatch.Frequency);}
  internal void Begin(long generation){Generation=generation;}
  internal void Publish(long dspFrame,long stamp,int sampleRate,int blockFrames){
   // Callback delivery can be bursty. Change clock frequency continuously, never
   // replace the time value with a late callback's block-start observation.
   Interlocked.Increment(ref sequence);double frame=dspFrame,speed=sampleRate;
   if(head>=0){var old=history[head];frame=old.Frame+(stamp-old.Ticks)*old.Speed/Stopwatch.Frequency;double error=dspFrame-frame;errors[errorHead++%errors.Length]=error;errorCount=Math.Min(errors.Length,errorCount+1);Array.Copy(errors,ordered,errorCount);Array.Sort(ordered,0,errorCount);double median=ordered[errorCount/2];speed=sampleRate+Math.Max(-sampleRate*.002,Math.Min(sampleRate*.002,median*2));}
   head=(head+1)%history.Length;history[head]=new Segment{Ticks=stamp,Frame=frame,Speed=speed};Interlocked.Exchange(ref ticks,stamp);Volatile.Write(ref hz,sampleRate);Volatile.Write(ref block,blockFrames);Interlocked.Increment(ref sequence);Interlocked.CompareExchange(ref firstTicks,stamp,0);
  }
  internal double FrameAt(long stamp){
   while(true){long a=Interlocked.Read(ref sequence);if((a&1)!=0)continue;int current=head;if(current<0)throw new InvalidOperationException("Audio clock has not received a mixer sample");var segment=history[current];for(int k=0;k<history.Length-1&&stamp<segment.Ticks;k++){int prev=(current-1+history.Length)%history.Length;var candidate=history[prev];if(candidate.Ticks==0)break;segment=candidate;current=prev;}
    double frame=segment.Frame+(stamp-segment.Ticks)*segment.Speed/Stopwatch.Frequency;if(a==Interlocked.Read(ref sequence))return frame;
   }
  }
  internal double FrameNow {get{return FrameAt(Stopwatch.GetTimestamp());}}
  internal bool Stale {get{return Ready&&(Stopwatch.GetTimestamp()-LastCallbackTicks)/(double)Stopwatch.Frequency>.25;}}
 }
 internal sealed class GameplayTransport {
  readonly GameplayClockBridge clock;internal readonly double Entry,Rate,Offset,Latency,VisualOffset;internal double StartFrame;internal bool Playing;internal readonly long Generation;
  internal GameplayTransport(GameplayClockBridge clock,double entry,double rate,double offset,double latency,double visual,long generation){this.clock=clock;Entry=entry;Rate=rate;Offset=offset;Latency=latency;VisualOffset=visual;Generation=generation;}
  internal double RawAt(double frame){return Entry+(frame-StartFrame)*1000*Rate/clock.Frequency;}
  internal double JudgeAt(double frame){return RawAt(frame)-Latency;}
  internal double JudgeNow {get{return JudgeAt(clock.FrameNow);}}
  internal double DisplayNow {get{return JudgeNow-VisualOffset;}}
  internal double FrameAtJudge(double ms){return StartFrame+(ms+Latency-Entry)*clock.Frequency/(1000*Rate);}
  internal void Start(){StartFrame=Math.Ceiling(clock.FrameNow+clock.BlockFrames*3);Playing=true;}
 }
}
