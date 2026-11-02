using System;
using System.Collections.Generic;
using System.Linq;
using InFalsusStudio.Core;
namespace InFalsusStudio.Gameplay {
 internal sealed class JudgeTick {internal double Time,Offset;internal bool Head,Tail,Done,Skipped;internal int Candidate;internal double Error;}
 internal sealed class JudgeNote {internal SpcEvent Source;internal int Id,Group,Next;internal bool MidRelease,WasActive,HoldInputActive;internal JudgeTick[] Ticks;}
 internal struct JudgeResult {internal JudgeNote Note;internal JudgeTick Tick;internal int Grade;internal double Time,Error;internal long InputTicks,ReceivedTicks;}
 // The two upgrade masks are literal constants from _VD._pz / _VD._nz.
 // Runtime tables are private copies. Stable source IDs never double as native note IDs.
 internal sealed partial class NativeJudgeEngine {
  internal readonly JudgeNote[] Notes;
  internal readonly Dictionary<int,JudgeNote> BySource=new Dictionary<int,JudgeNote>();
  internal readonly List<JudgeResult> Results=new List<JudgeResult>();
  GameplayInputSampler eventInput;
  internal readonly HashSet<int> ConsumedNotes=new HashSet<int>();
  internal int Total,Settled,Combo,MaxCombo,ExactPlus,Exact,Near,Break;
  internal int MaxGroup=-1,MaxCapturedId=-1,HighestSeenId=-1;
  internal bool Captured;internal double Capacity=300,Cooldown,Clock,Now;
  internal JudgeNote ActiveSky;
  readonly double entryTime;int first;double previousNow=double.NegativeInfinity;
  internal long Score {get{return Total==0?0:(long)Math.Ceiling(100000000m/Total*(ExactPlus+Exact+Near*.5m))+ExactPlus;}}
  // _Uh..ctor / _pMA (1.07b): start at +15; each settled tick is
  // applied in native processing order, with clamping after every tick.
  internal double Gauge=15;
  internal int GaugeHundredths {get{return Math.Max(-1500,Math.Min(1500,(int)Math.Round(Gauge*100,MidpointRounding.ToEven)));}}
  internal int GaugeValue {get{return GaugeHundredths/100;}}
  // ClearStateContainer._xBA: AP=5, FC=4, positive+/positive=3/2,
  // negative/negative+=1/0. Near ends AP; the first Break ends FC.
  internal int GaugeState {get{if(Break==0)return Near==0?5:4;int value=GaugeValue;return value<=-6?0:value<0?1:value>=7?3:2;}}
  internal bool Complete {get{return Settled==Total;}}
  internal double LastTime {get;private set;}
  internal NativeJudgeEngine(IEnumerable<SpcEvent> source,double entry){
   entryTime=entry;var all=source.ToArray();var beats=new BeatTimeline(all);var notes=new List<JudgeNote>();int id=0;
   foreach(var e in all.Where(e=>e.IsNote).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId)){
    if(ChartMath.InvalidReason(e)!=null)continue;
    double bpm=beats.Points[0].Bpm;foreach(var p in beats.Points){if(p.Time>e.TimeMs)break;bpm=p.Bpm;}
    var ticks=Ticks(e,bpm).ToArray();foreach(var t in ticks){t.Skipped=t.Time<entry;t.Done=t.Skipped;if(!t.Skipped)Total++;}
    var n=new JudgeNote{Source=new SpcEvent{SourceId=e.SourceId,Kind=e.Kind,Name=e.Name,Args=(double[])e.Args.Clone()},Id=id++,Group=SkyGroups.GroupOf(e),Ticks=ticks};
    var tag=InFalsusStudio.TimeGroups.TimingGroups.Tag(e);if(tag!=null)InFalsusStudio.TimeGroups.TimingGroups.TagEvent(tag.Model.Document,n.Source);
    while(n.Next<ticks.Length&&ticks[n.Next].Done)n.Next++;
    notes.Add(n);BySource.Add(n.Source.SourceId,n);if(n.Next<ticks.Length)LastTime=Math.Max(LastTime,ticks[ticks.Length-1].Time);
    if((e.Kind==EventKind.Tap||e.Kind==EventKind.Flick)&&e.TimeMs<entry)ConsumedNotes.Add(e.SourceId);
   }Notes=notes.ToArray();Clock=entry;Now=entry;BuildSkyGroups();
  }
  internal static IEnumerable<JudgeTick> Ticks(SpcEvent e,double bpm){
   if(e.Kind==EventKind.Tap||e.Kind==EventKind.Flick){yield return new JudgeTick{Time=e.TimeMs,Head=true,Tail=true};yield break;}
   var offsets=new List<double>();if(e.Kind==EventKind.Hold)offsets.Add(0);
   double interval=(bpm<249.99?30000d:60000d)/bpm,d=e.DurationMs;
   if(!AuthoringSafety.Finite(interval)||interval<=0)throw new ArgumentException("Invalid judgement BPM");
   if(d/interval>1000000)throw new ArgumentException("Too many judgement ticks in a single note");
   for(int k=1;k*interval<=d;k++)offsets.Add(Math.Floor(k*interval));
   double last=offsets.Count==0?0:offsets[offsets.Count-1];if(d-last>=2)offsets.Add(d);
   for(int i=0;i<offsets.Count;i++)yield return new JudgeTick{Offset=offsets[i],Time=e.TimeMs+offsets[i],Head=e.Kind==EventKind.Hold&&i==0,Tail=i==offsets.Count-1};
  }
  internal static int Grade(double delta,bool flick=false){double a=Math.Abs(delta);if(a<=(flick?50:25))return 6;if(a<=(flick?100:50))return delta<0?4:5;if(a<=(flick?200:100))return delta<0?2:3;return 0;}
  internal static bool Upgrade(int next,int current,bool late=false){ulong mask=late?0x1fdf9f1e3c181UL:0xfcf8f060c080UL;return (mask&(1UL<<(next*7+current)))!=0;}
  void Settle(JudgeNote n,JudgeTick t,int grade,double error){
   if(t.Done)return;t.Done=true;t.Candidate=grade;t.Error=error;Settled++;
   if(grade==1){Break++;Combo=0;}else{Combo++;MaxCombo=Math.Max(MaxCombo,Combo);if(grade==6)ExactPlus++;else if(grade>=4)Exact++;else Near++;}
   Gauge=Math.Max(-15,Math.Min(15,Gauge+(grade==1?-1:Total>0?(100d/Total)*(grade>=4?1:.5):0)));
   if(grade>1&&(n.Source.Kind==EventKind.Tap||n.Source.Kind==EventKind.Flick||t.Tail&&(n.Source.Kind==EventKind.Hold||n.Source.Kind==EventKind.SkyArea)))ConsumedNotes.Add(n.Source.SourceId);
   Results.Add(new JudgeResult{Note=n,Tick=t,Grade=grade,Time=Now,Error=error,InputTicks=eventInput==null?0:eventInput.EventTicks,ReceivedTicks=eventInput==null?0:eventInput.EventReceivedTicks});
   while(n.Next<n.Ticks.Length&&n.Ticks[n.Next].Done)n.Next++;
  }
  internal bool Holding(JudgeNote n,GameplayInputSampler input){int firstLane=n.Source.Lane,last=firstLane==0||firstLane==5?firstLane:Math.Min(4,firstLane+(int)Math.Ceiling(n.Source.GroundWidth)-1);for(int lane=Math.Max(0,firstLane);lane<=last;lane++)if(input.Held[lane])return true;return false;}
  double Released(JudgeNote n,GameplayInputSampler input){double last=double.NegativeInfinity;int a=n.Source.Lane,b=a==0||a==5?a:Math.Min(4,a+(int)Math.Ceiling(n.Source.GroundWidth)-1);for(int i=Math.Max(0,a);i<=b;i++)last=Math.Max(last,input.LastRelease[i]);return last;}
  internal bool EventDriven;internal double InputGraceMs;
  internal void BeginBatch(){Results.Clear();}
  internal void Step(double now,GameplayInputSampler input){BeginBatch();Advance(now,input);}
  internal void Advance(double now,GameplayInputSampler input){
   eventInput=input;if(now<previousNow)throw new InvalidOperationException("Gameplay clock moved backwards");previousNow=Now=now;ActiveSky=null;
   // _TD._bz expires motion even when no new mouse event arrives.
   input.Left.Trim(now);input.Right.Trim(now);
   UpdateSkyGroups(now,input);
   // Sky -> ground -> Flick; each group is committed only once per frame.
   for(int i=first;i<Notes.Length;i++){var n=Notes[i];if(n.Source.TimeMs>now+200)break;if(n.Next<n.Ticks.Length&&n.Source.Kind==EventKind.SkyArea)Sky(n,input);}
   for(int i=first;i<Notes.Length;i++){var n=Notes[i];if(n.Source.TimeMs>now+150)break;if(n.Next<n.Ticks.Length&&(n.Source.Kind==EventKind.Tap||n.Source.Kind==EventKind.Hold))Ground(n,input);}
   for(int i=first;i<Notes.Length;i++){var n=Notes[i];if(n.Source.TimeMs>now+200)break;if(n.Next<n.Ticks.Length&&n.Source.Kind==EventKind.Flick)Flick(n,input);}
   while(first<Notes.Length&&Notes[first].Next>=Notes[first].Ticks.Length)first++;
  }
  internal void SubmitPress(double time,GameplayInputSampler input){
   eventInput=input;for(int i=first;i<Notes.Length;i++){var n=Notes[i];var e=n.Source;if(e.TimeMs>time+150)break;if(e.Kind!=EventKind.Tap&&e.Kind!=EventKind.Hold)continue;if(n.Next>=n.Ticks.Length)continue;var t=n.Ticks[n.Next];if(!t.Head||t.Done)continue;double dt=time-t.Time;if(dt< -150||dt>100)continue;int a=e.Lane,b=a==0||a==5?a:Math.Min(4,a+(int)Math.Ceiling(e.GroundWidth)-1);for(int lane=Math.Max(0,a);lane<=b;lane++)if(input.Pressed[lane]&&!input.Consumed[lane]){input.Consumed[lane]=true;Settle(n,t,Grade(dt)==0?1:Grade(dt),dt);break;}}
  }
  void Ground(JudgeNote n,GameplayInputSampler input){
   var e=n.Source;bool held=Holding(n,input);double release=Released(n,input);
   if(e.Kind==EventKind.Hold){
    // 1.07b _Oz/_Uz: a discontinuity requires membership in the previous
    // active-hold set. Never arm a penalty while waiting for the head press.
    var head=n.Ticks[0];bool active=held&&(head.Done||head.Candidate!=0)&&Now>=e.TimeMs-100&&Now<=e.EndMs+100;
    if(n.HoldInputActive&&!active&&Now>=e.TimeMs&&e.EndMs-Now>50)n.MidRelease=true;
    n.HoldInputActive=active;
   }
   for(int i=n.Next;i<n.Ticks.Length;i++){
    var t=n.Ticks[i];if(t.Done)continue;double dt=Now-t.Time;if(dt< -150)break;
    if(t.Head){
     if(dt>100+InputGraceMs){Settle(n,t,1,dt);continue;}
     if(EventDriven)continue;
     int a=e.Lane,b=a==0||a==5?a:Math.Min(4,a+(int)Math.Ceiling(e.GroundWidth)-1);
     for(int lane=Math.Max(0,a);lane<=b;lane++)if(input.Pressed[lane]&&!input.Consumed[lane]){input.Consumed[lane]=true;Settle(n,t,Grade(dt)==0?1:Grade(dt),dt);break;}
     continue;
    }
    // The closest valid lane is either held now or was released near this tick.
    double effective=held?dt:release-t.Time;int candidate=Grade(effective);
    if(candidate!=0&&Upgrade(candidate,t.Candidate)){t.Candidate=candidate;t.Error=effective;}
    if(dt>=0&&(t.Candidate==6||dt>100+(t.Candidate==0?InputGraceMs:0))){
     int g=t.Candidate==0?1:t.Candidate;
     if(n.MidRelease&&!t.Tail){if(Upgrade(g,2))g=2;n.MidRelease=false;}
     Settle(n,t,g,t.Candidate==0?dt:t.Error);
    }
   }
  }
  void Flick(JudgeNote n,GameplayInputSampler input){var t=n.Ticks[n.Next];double dt=Now-t.Time;int timeGrade=Grade(dt,true);bool hit=false;
   if(timeGrade!=0){var range=ChartMath.FlickRange(n.Source);hit=(n.Source.Get(4)==16?input.Left:input.Right).Crosses(range.Left,range.Right);}
   bool possible=Upgrade(timeGrade,t.Candidate,dt>=0);
   if(possible&&(hit||timeGrade==0&&dt>200)){t.Candidate=timeGrade;t.Error=dt;}
   if(dt>=0&&(t.Candidate==6||!Upgrade(timeGrade,t.Candidate,true)||dt>200))Settle(n,t,t.Candidate==0?1:t.Candidate,t.Candidate==0?dt:t.Error);if(t.Candidate>1&&dt>=0)ConsumedNotes.Add(n.Source.SourceId);
  }
  // Compatibility diagnostic for standalone budget checks; gameplay uses group slots.
  internal void AdvanceSky(double target,bool inside){var b=new SkyBudget{Time=Clock,Capacity=Capacity,Cooldown=Cooldown,Captured=Captured};SkyBudget.Advance(ref b,target,inside?1:0);Clock=b.Time;Capacity=b.Capacity;Cooldown=b.Cooldown;Captured=b.Captured;}
 }
}
