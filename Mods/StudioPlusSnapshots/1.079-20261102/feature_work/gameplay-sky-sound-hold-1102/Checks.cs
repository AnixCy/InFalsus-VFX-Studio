using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using BepInEx;using BepInEx.Unity.Mono;using UnityEngine;using InFalsusStudio;using InFalsusStudio.Core;using InFalsusStudio.Gameplay;
[BepInPlugin("test.gameplay.sky.sound.hold","Sky sounds and Hold checks","1.0.0")]
public sealed class SkySoundHoldChecks:BaseUnityPlugin {
 const string Root=@"E:\RhythmGame\InFalsusStudio+\feature_work\gameplay-sky-sound-hold-1102";int count,failed;
#if BASELINE
 const string Report="baseline-checks.txt";
#else
 const string Report="checks.txt";
#endif
 void Check(bool ok,string name){count++;File.AppendAllText(Root+"/"+Report,(ok?"PASS ":"FAIL ")+name+"\n");if(!ok)failed++;}
 static NativeJudgeEngine Engine(string text){return new NativeJudgeEngine(SpcDocument.Parse("chart(120,4)\n"+text+"\n").ReadEvents(),0);}
 void Regression(){
  var e=Engine("hold(1000,1,1,1000)");var input=new GameplayInputSampler();var run=new GameplayJudgeRunner(e,input,-1,()=>{});run.ApplyAt(1003,new GameplayInputEvent{Lane=1,Held=true});run.Finish(2201);Check(e.Complete&&e.ExactPlus==e.Total&&e.Near==0,"normal +3 ms Hold head followed by uninterrupted hold never invents Near");
  e=Engine("hold(1000,1,1,1000)");input=new GameplayInputSampler();run=new GameplayJudgeRunner(e,input,-1,()=>{});run.ApplyAt(999,new GameplayInputEvent{Lane=1,Held=true});run.ApplyAt(1350,new GameplayInputEvent{Lane=1,Held=false});run.ApplyAt(1360,new GameplayInputEvent{Lane=1,Held=true});run.Finish(2201);Check(e.Complete&&e.Near==1&&e.Break==0,"actual 10 ms mid-Hold discontinuity applies one native Near penalty");
  e=Engine("flick(1000,50,100,20,4)\nflick(1500,50,100,20,4)");input=new GameplayInputSampler();run=new GameplayJudgeRunner(e,input,-1,()=>{});run.ApplyAt(989,new GameplayInputEvent{Mouse=true,X=.4});run.ApplyAt(990,new GameplayInputEvent{Mouse=true,X=.6});run.Finish(1801);Check(e.ExactPlus==1&&e.Break==1,"one swipe then no mouse motion cannot hit the next Flick");
 }
#if !BASELINE
 sealed class Sound {internal HitSoundKind Kind;internal double Target,Observed;internal int Id;}
 sealed class Probe {
  internal NativeJudgeEngine E;internal GameplayInputSampler Input;internal GameplayJudgeRunner Run;internal GameplaySoundRouter Router;internal List<Sound> Sounds=new List<Sound>();internal List<JudgeResult> Grades=new List<JudgeResult>();
  internal Probe(string text,int policy=1,double rate=1){E=Engine(text);Input=new GameplayInputSampler();Router=new GameplaySoundRouter(E,Input,(k,id,t,g,r)=>Sounds.Add(new Sound{Kind=k,Id=id,Target=t,Observed=E.Now}));Run=new GameplayJudgeRunner(E,Input,-1,()=>{Grades.AddRange(E.Results);Router.Tick(true,policy,rate);});}
  internal void Key(double t,int lane,bool down){Run.ApplyAt(t,new GameplayInputEvent{Lane=lane,Held=down});}
  internal void Mouse(double t,double x){Run.ApplyAt(t,new GameplayInputEvent{Mouse=true,X=x});}
  internal int Shots(HitSoundKind k){return Sounds.Count(s=>s.Kind==k);}
 }
 void Candidate(){
  foreach(int fps in new[]{30,60,144}){var p=new Probe("hold(1000,1,1,1000)");p.Key(1003,1,true);for(double t=1004;t<2300;t+=1000d/fps)p.Run.Finish(t);Check(p.E.Complete&&p.E.Near==0&&p.E.Break==0,"continuous slightly late Hold remains Exact+ at "+fps+" FPS");}
  {var p=new Probe("hold(1000,1,1,1000)");p.Key(1075,1,true);p.Run.Finish(2201);Check(p.E.Near==1&&p.Grades.Count(g=>!g.Tick.Head&&g.Grade<4)==0,"legitimate Near head does not penalize subsequent held ticks");}
  {var p=new Probe("hold(1000,1,1,1000)");p.Key(999,1,true);p.Key(1960,1,false);p.Run.Finish(2201);Check(p.E.Near==0&&!p.E.BySource.Values.First().MidRelease,"last 50 ms release does not arm the interior discontinuity penalty");}
  {var p=new Probe("hold(1000,1,2,1000)");p.Key(999,1,true);p.Key(1350,2,true);p.Key(1351,1,false);p.Run.Finish(2201);Check(p.E.ExactPlus==p.E.Total&&p.E.Near==0,"covered lanes hand over without an all-up gap");}
  {var p=new Probe("flick(1000,50,100,20,4)");p.Mouse(849,.4);p.Mouse(850,.6);p.Run.Finish(999);Check(p.Sounds.Count==0,"early Flick candidates stay silent before the note");p.Run.Finish(1301);Check(p.Shots(HitSoundKind.Flick)==1&&p.Sounds[0].Observed>=1000&&p.Sounds[0].Target==1000,"successful Flick emits its own sample once on committed judgement");}
  {var p=new Probe("flick(1000,50,100,20,16)");p.Mouse(989,.6);p.Mouse(990,.4);p.Run.Finish(1301);Check(p.Shots(HitSoundKind.Flick)==1&&p.E.Break==0,"opposite-direction Flick uses the same Flick sound mapping");}
  {var p=new Probe("flick(1000,50,100,20,4)");p.Run.Finish(1301);Check(p.Sounds.Count==0&&p.E.Break==1,"missed Flick emits no attack");}
  string chain="skyarea(1000,50,100,20,50,100,20,0,0,500,7)\nskyarea(1500,50,100,20,50,100,20,0,0,500,7)";
  {var p=new Probe(chain);p.Run.Finish(1001);p.Mouse(1100,.9);p.Mouse(1140,.5);p.Run.Finish(1601);Check(p.Shots(HitSoundKind.SkyHit)==1&&p.Router.Sky,"same-group segments and short excursions keep one native capture attack and one bed");}
  {var p=new Probe("skyarea(1000,50,100,20,50,100,20,0,0,500,7)\nskyarea(2200,50,100,20,50,100,20,0,0,500,7)");p.Run.Finish(2301);Check(p.Shots(HitSoundKind.SkyHit)==1,"same explicit group after a gap is not an editor chain-start attack");}
  {var p=new Probe("skyarea(1000,50,100,20,50,100,20,0,0,500,7)\nskyarea(1500,50,100,20,50,100,20,0,0,500,8)\nskyarea(2000,50,100,20,50,100,20,0,0,500,7)");p.Run.Finish(2301);Check(p.Shots(HitSoundKind.SkyHit)==3,"captured group changes including returning to a group produce native SkyHit attacks");}
  {var p=new Probe(chain,2);p.Run.Finish(1601);Check(p.Shots(HitSoundKind.SkyHit)==0&&p.Router.Sky,"head sound off preserves the independent Sky sustain bus");}
  {var p=new Probe(chain,0);p.Run.Finish(1601);Check(p.Shots(HitSoundKind.SkyHit)==2,"explicit every-segment preference still works");}
  {var p=new Probe("tap(1000,1,1)\ntap(1000,1,2)\ntap(1000,1,0)");p.Key(1000,1,true);p.Key(1000,2,true);p.Key(1000,0,true);Check(p.Shots(HitSoundKind.FloorHit)==1&&p.Shots(HitSoundKind.SideHit)==1,"native 10 ms coalescing merges same-sample chords and keeps different samples separate");}
  {var p=new Probe("tap(1000,1,1)\ntap(1015,1,2)",1,1);p.Key(1000,1,true);p.Key(1015,2,true);Check(p.Shots(HitSoundKind.FloorHit)==2,"batched event delivery retains attacks more than 10 ms apart");}
  {var p=new Probe("tap(1000,1,1)\ntap(1015,1,2)",1,2);p.Key(1000,1,true);p.Key(1015,2,true);Check(p.Shots(HitSoundKind.FloorHit)==1,"coalescing window follows real time at playback rate 2");}
  Check(NativeJudgeEngine.Grade(25)==6&&NativeJudgeEngine.Grade(25.01)==5&&NativeJudgeEngine.Grade(-50)==4&&NativeJudgeEngine.Grade(-50.01)==2&&NativeJudgeEngine.Grade(100)==3&&NativeJudgeEngine.Grade(100.01)==0,"Hold timing thresholds remain native 25 / 50 / 100 ms");
  foreach(string name in new[]{"SkyHit","Flick","SkyHold"}){var clip=Resources.Load<AudioClip>("InFalsusStudio/KeySounds/"+name);Check(clip!=null&&clip.name==name&&clip.samples>0,"runtime sample resource "+name+" exists and maps by name");}
 }
#endif
 IEnumerator Start(){if(!Environment.GetCommandLineArgs().Contains("-sky-sound-hold-checks"))yield break;File.WriteAllText(Root+"/"+Report,"");yield return new WaitForSeconds(2);try{Regression();
#if !BASELINE
 Candidate();
#endif
 }catch(Exception ex){Check(false,"unexpected exception "+ex);}File.AppendAllText(Root+"/"+Report,"DONE checks="+count+" failures="+failed+"\n");Application.Quit();}
}
