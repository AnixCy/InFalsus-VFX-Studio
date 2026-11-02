using System;using System.Collections.Generic;using System.Diagnostics;
using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
namespace InFalsusStudio.Gameplay {
 internal struct GameplayInputEvent {internal double Time,X;internal long Received,Generated,Sequence;internal int Lane;internal bool Held,Mouse;}
 internal sealed class GameplayInputTimeline:IDisposable {
  readonly GameplayInputEvent[] ring=new GameplayInputEvent[8192];readonly bool[] keyState=new bool[7];readonly GameplayClockBridge clock;readonly GameplayInputSampler state;int read,write;long sequence;Vector2 lastPosition,warpPosition;double span=1;bool warpPending;
  static readonly Key[] keys={Key.LeftShift,Key.A,Key.S,Key.D,Key.F,Key.Space,Key.RightShift};
  internal long Dropped,SeenEvents,CapturedEvents;internal double LastEventTime;internal double X=.5;internal bool Recording;
  internal GameplayInputTimeline(GameplayClockBridge clock,GameplayInputSampler state,Camera camera,InFalsusStudio.StudioProfile profile){
   this.clock=clock;this.state=state;var mouse=Mouse.current;if(mouse!=null)lastPosition=mouse.position.ReadValue();
   if(camera){var a=camera.WorldToScreenPoint(new Vector3(InFalsusStudio.StageSpace.AirX(profile,0),profile.skyHeight,0));var b=camera.WorldToScreenPoint(new Vector3(InFalsusStudio.StageSpace.AirX(profile,1),profile.skyHeight,0));span=Math.Max(1,Math.Abs(b.x-a.x));}
   var keyboard=Keyboard.current;if(keyboard!=null)for(int i=0;i<keys.Length;i++)keyState[i]=keyboard[keys[i]].isPressed;
   X=state.X;InputSystem.onEvent+=OnEvent;
  }
  internal void Begin(){read=write=0;X=state.X;Recording=true;state.Prime();}
  void Push(GameplayInputEvent e){LastEventTime=e.Time;CapturedEvents++;if(!Recording)return;int next=(write+1)%ring.Length;if(next==read){Dropped++;return;}e.Sequence=++sequence;e.Received=Stopwatch.GetTimestamp();e.Generated=clock.InputTicks(e.Time);ring[write]=e;write=next;}
  void OnEvent(InputEventPtr e,InputDevice device){SeenEvents++;
   if(!e.IsA<StateEvent>()&&!e.IsA<DeltaStateEvent>())return;
   var keyboard=device as Keyboard;
   if(keyboard!=null){for(int i=0;i<keys.Length;i++){float value;if(!keyboard[keys[i]].ReadValueFromEvent(e,out value))continue;bool held=value>=.5f;if(held==keyState[i])continue;bool before=keyState[0]||keyState[6];keyState[i]=held;int lane=i==6?0:i;if(lane==0){held=keyState[0]||keyState[6];if(held==before)continue;}Push(new GameplayInputEvent{Time=e.time,Lane=lane,Held=held});}}
   var mouse=device as Mouse;if(mouse!=null){Vector2 pos;if(!mouse.position.ReadValueFromEvent(e,out pos))return;if(warpPending&&(pos-warpPosition).sqrMagnitude<.25f){lastPosition=pos;warpPending=false;return;}double dx=pos.x-lastPosition.x;lastPosition=pos;if(Math.Abs(dx)<.0001)return;X=Math.Max(0,Math.Min(1,X+dx/span*GameplayOptions.Sensitivity.Value));Push(new GameplayInputEvent{Time=e.time,X=X,Mouse=true});}
  }
  internal void Drain(List<GameplayInputEvent> target){target.Clear();while(read!=write){target.Add(ring[read]);read=(read+1)%ring.Length;}target.Sort(Compare);}
  static int Compare(GameplayInputEvent a,GameplayInputEvent b){int c=a.Time.CompareTo(b.Time);return c!=0?c:a.Sequence.CompareTo(b.Sequence);}
  internal double JudgeTime(GameplayInputEvent e,GameplayTransport transport){return transport.JudgeAt(clock.FrameAt(clock.InputTicks(e.Time)));}
  internal void Center(Camera camera){var mouse=Mouse.current;if(mouse==null||!camera||!Application.isFocused)return;warpPosition=camera.pixelRect.center;lastPosition=warpPosition;warpPending=true;mouse.WarpCursorPosition(warpPosition);}
  public void Dispose(){Recording=false;InputSystem.onEvent-=OnEvent;}
 }
 internal sealed class GameplayJudgeRunner {
  readonly NativeJudgeEngine engine;readonly GameplayInputSampler input;readonly Action consume;readonly List<GameplayInputEvent> pending=new List<GameplayInputEvent>(256);readonly Queue<GameplayInputEvent> history=new Queue<GameplayInputEvent>();double simulation,nextGrid;
  internal double Time {get{return simulation;}}
  internal GameplayJudgeRunner(NativeJudgeEngine engine,GameplayInputSampler input,double start,Action consume){this.engine=engine;this.input=input;this.consume=consume;simulation=start;nextGrid=Math.Floor(start)+1;engine.EventDriven=true;engine.InputGraceMs=100;}
  void Evaluate(double time){input.ClearEdges();engine.BeginBatch();engine.Advance(time,input);simulation=time;GameplaySession.Now=time;consume();}
  void AdvanceTo(double target){while(nextGrid<=target){if(nextGrid>simulation)Evaluate(nextGrid);nextGrid++;}if(target>simulation)Evaluate(target);}
  readonly List<GameplayInputEvent> future=new List<GameplayInputEvent>();
  internal void ApplyAt(double time,GameplayInputEvent e){
   if(time>=simulation)AdvanceTo(time);else if(GameplaySession.Audio!=null)GameplaySession.Audio.Scheduler.Diagnostics.LateInputs++;
   input.ClearEdges();input.EventTicks=e.Generated;input.EventReceivedTicks=e.Received;if(e.Mouse)input.Move(time,e.X);else input.SetLane(e.Lane,e.Held,time);
   engine.BeginBatch();if(!e.Mouse&&e.Held)engine.SubmitPress(time,input);engine.Advance(simulation,input);GameplaySession.Now=simulation;consume();
  }
  internal void Frame(double now,GameplayInputTimeline timeline,GameplayTransport transport){
   timeline.Drain(pending);if(future.Count>0){pending.AddRange(future);future.Clear();pending.Sort((a,b)=>{int c=a.Time.CompareTo(b.Time);return c!=0?c:a.Sequence.CompareTo(b.Sequence);});}
   foreach(var e in pending){double time=timeline.JudgeTime(e,transport);if(transport.FrameAtJudge(time)<transport.StartFrame)continue;if(time>now){future.Add(e);continue;}ApplyAt(time,e);history.Enqueue(e);}
   while(history.Count>0&&history.Peek().Time<TimeNowUnity()-2)history.Dequeue();AdvanceTo(now);
  }
  static double TimeNowUnity(){return UnityEngine.Time.realtimeSinceStartupAsDouble;}
  internal void Finish(double target){AdvanceTo(target);}
 }
}
