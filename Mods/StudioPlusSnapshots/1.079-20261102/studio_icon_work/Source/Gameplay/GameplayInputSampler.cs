using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.Gameplay {
 internal sealed class GameplayInputSampler {
  internal readonly bool[] Held=new bool[6],Pressed=new bool[6],Consumed=new bool[6];
  internal readonly double[] LastPress=new double[6],LastRelease=new double[6];
  internal double X=.5,PreviousX=.5;internal long EventTicks,EventReceivedTicks;
  readonly bool[] previous=new bool[6];bool mouseReady;Vector2 initialMouse,lastMouse;
  internal readonly SwipeTracker Left=new SwipeTracker(),Right=new SwipeTracker();
  static readonly KeyCode[] keys={KeyCode.LeftShift,KeyCode.A,KeyCode.S,KeyCode.D,KeyCode.F,KeyCode.Space};
  internal GameplayInputSampler(){for(int i=0;i<6;i++)LastPress[i]=LastRelease[i]=double.NegativeInfinity;}
  internal void Prime(){for(int i=0;i<6;i++)previous[i]=Held[i]=Read(keys[i])||(i==0&&Read(KeyCode.RightShift));}
  static bool legacy=true;
  static UnityEngine.InputSystem.Controls.KeyControl Control(KeyCode code){var keyboard=UnityEngine.InputSystem.Keyboard.current;if(keyboard==null)return null;UnityEngine.InputSystem.Key key;return Enum.TryParse(code.ToString(),out key)?keyboard[key]:null;}
  internal static bool Read(KeyCode key){
   if(legacy)try{return Input.GetKey(key);}catch(InvalidOperationException){legacy=false;}
   var control=Control(key);return control!=null&&control.isPressed;
  }
  static bool Down(KeyCode key){if(legacy)try{return Input.GetKeyDown(key);}catch(InvalidOperationException){legacy=false;}var control=Control(key);return control!=null&&control.wasPressedThisFrame;}
  static Vector2 Mouse(){var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse!=null)return mouse.position.ReadValue();if(legacy)try{return Input.mousePosition;}catch(InvalidOperationException){legacy=false;}return Vector2.zero;}
  internal void MouseDelta(double now,double pixels,double span,double multiplier){if(Math.Abs(span)>.001)Move(now,Math.Max(0,Math.Min(1,X+pixels/span*Math.Max(.1,Math.Min(5,multiplier)))));}
  internal void RestoreMouse(){if(mouseReady&&UnityEngine.InputSystem.Mouse.current!=null)UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(initialMouse);mouseReady=false;}
  internal void Sample(double now,Camera camera,StudioProfile profile){
   for(int i=0;i<6;i++){SetLane(i,Read(keys[i])||(i==0&&Read(KeyCode.RightShift)),now);if(Down(keys[i])||(i==0&&Down(KeyCode.RightShift))){Pressed[i]=true;LastPress[i]=now;}}
   if(!camera){Move(now,X);return;}var mouse=Mouse();var a=camera.WorldToScreenPoint(new Vector3(StageSpace.AirX(profile,0),profile.skyHeight,0));var b=camera.WorldToScreenPoint(new Vector3(StageSpace.AirX(profile,1),profile.skyHeight,0));double span=b.x-a.x;
   if(!mouseReady){initialMouse=lastMouse=mouse;mouseReady=true;if(Math.Abs(span)>.001)Move(now,Math.Max(0,Math.Min(1,(mouse.x-a.x)/span)));}
   else MouseDelta(now,mouse.x-lastMouse.x,span,GameplayOptions.Sensitivity.Value);
   lastMouse=mouse;var device=UnityEngine.InputSystem.Mouse.current;if(device!=null&&Application.isFocused){lastMouse=camera.pixelRect.center;device.WarpCursorPosition(lastMouse);}
  }
  internal void ClearEdges(){Array.Clear(Pressed,0,Pressed.Length);Array.Clear(Consumed,0,Consumed.Length);}
  internal void SetLane(int lane,bool held,double now){Held[lane]=held;Pressed[lane]=held&&!previous[lane];Consumed[lane]=false;if(Pressed[lane])LastPress[lane]=now;if(!held&&previous[lane])LastRelease[lane]=now;previous[lane]=held;}
  internal void Move(double now,double x){PreviousX=X;double dx=x-X;
   if(dx<0){Left.Add(now,X,x);Right.Clear();}else if(dx>0){Right.Add(now,X,x);Left.Clear();}
   X=x;Left.Trim(now);Right.Trim(now);
  }
 }
 internal sealed class SwipeTracker {
  struct Segment {internal double Time,A,B;}
  readonly List<Segment> rows=new List<Segment>();
  internal void Clear(){rows.Clear();}
  internal void Add(double now,double a,double b){rows.Add(new Segment{Time=now,A=a,B=b});}
  internal void Trim(double now){int i=0;while(i<rows.Count&&rows[i].Time<now-50)i++;if(i>0)rows.RemoveRange(0,i);}
  internal bool Crosses(double left,double right){if(rows.Count==0)return false;double lo=1,hi=0;foreach(var p in rows){lo=Math.Min(lo,Math.Min(p.A,p.B));hi=Math.Max(hi,Math.Max(p.A,p.B));}return hi-lo>.001&&hi>=left&&lo<=right;}
 }
}
