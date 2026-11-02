using System;using System.Collections.Generic;using InFalsusStudio.Core;
namespace InFalsusStudio.Gameplay {
 // Gameplay routing follows GameScene._Mk / _VD._Oz in 1.07b.
 // Chart-driven editor audition remains in HitSoundPlan and is independent.
 internal sealed class GameplaySoundRouter {
  readonly NativeJudgeEngine engine;readonly GameplayInputSampler input;
  readonly Action<HitSoundKind,int,double,long,long> emit;
  readonly HashSet<int> played=new HashSet<int>();
  readonly double[] lastShot={double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity};
  int capturedNote=-1,capturedGroup=-1;internal bool Floor,Sky;
  internal GameplaySoundRouter(NativeJudgeEngine engine,GameplayInputSampler input,Action<HitSoundKind,int,double,long,long> emit){this.engine=engine;this.input=input;this.emit=emit;}
  void Shot(HitSoundKind kind,int id,double time,double rate,long generated=0,long received=0,bool once=true){
   if(once&&!played.Add(id))return;
   // The native scene coalesces each sample independently within 10 real ms.
   // Use event time, so batching input after a slow frame cannot merge distant hits.
   int k=(int)kind;double real=time/Math.Max(.001,rate);
   if(Math.Abs(real-lastShot[k])<=10)return;lastShot[k]=real;
   emit(kind,id,time,generated,received);
  }
  internal void Tick(bool holdAttack,int skyPolicy,double rate){
   double now=engine.Now;
   foreach(var result in engine.Results){if(result.Grade<=1)continue;var note=result.Note.Source;
    if(note.Kind==EventKind.Tap||note.Kind==EventKind.Hold&&result.Tick.Head&&holdAttack)
     Shot(note.Lane==0||note.Lane==5?HitSoundKind.SideHit:HitSoundKind.FloorHit,note.SourceId,result.Tick.Time+result.Error,rate,result.InputTicks,result.ReceivedTicks);
    // Native Flick audio is emitted only after successful judgement is committed;
    // its requested time is the note time, never the first early swipe candidate.
    else if(note.Kind==EventKind.Flick)Shot(HitSoundKind.Flick,note.SourceId,result.Tick.Time,rate,result.InputTicks,result.ReceivedTicks);
   }
   Floor=false;JudgeNote captured=null;
   foreach(var n in engine.Notes){var e=n.Source;if(e.TimeMs>now+200)break;
    if(e.Kind==EventKind.SkyArea&&now>=e.TimeMs&&now<e.EndMs&&engine.SkyActive(e.SourceId)){
     if(skyPolicy==0)Shot(HitSoundKind.SkyHit,e.SourceId,now,rate);
     if(n.Id>=capturedNote&&(captured==null||n.Id>captured.Id))captured=n;
    }
    if(e.Kind==EventKind.Hold&&now>=e.TimeMs&&now<e.EndMs&&engine.Holding(n,input))Floor=true;
   }
   // Native event 6 compares the last captured GROUP, not a connected source head.
   // Short excursions and consecutive segments in the same group stay continuous.
   if(captured!=null){bool changed=captured.Group!=capturedGroup;capturedNote=captured.Id;capturedGroup=captured.Group;
    if(changed&&skyPolicy==1)Shot(HitSoundKind.SkyHit,captured.Source.SourceId,now,rate,once:false);
   }
   Sky=engine.ActiveSky!=null;
  }
 }
 internal sealed class GameplaySoundController {
  readonly GameplayAudioHost host;readonly GameplaySoundRouter router;
  internal GameplaySoundController(GameplayAudioHost host,NativeJudgeEngine engine,GameplayInputSampler input){this.host=host;router=new GameplaySoundRouter(engine,input,Shot);}
  void Shot(HitSoundKind kind,int id,double time,long generated,long received){host.Scheduler.Submit(kind,id,host.Transport.FrameAtJudge(time),generated,received);}
  internal void Tick(){host.UpdateOptions();router.Tick(GameplayOptions.HoldAttack.Value,GameplayOptions.SkyAttack.Value,host.Transport.Rate);host.Scheduler.Floor=router.Floor;host.Scheduler.Sky=router.Sky;}
 }
}
