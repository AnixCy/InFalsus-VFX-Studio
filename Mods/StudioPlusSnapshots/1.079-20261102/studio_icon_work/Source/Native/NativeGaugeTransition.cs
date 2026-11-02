using System;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
 // ClearStateContainer._yBA: intermediate states commit with zero-duration
 // fades; the final step uses 0.3s EaseType.SineInOut. Update moves number
 // alphas at 1/0.2 per second. Logical state/gauge remain in the judge engine.
 internal sealed class NativeGaugeTransition {
  internal static readonly string[] Images={"clearstate-indicator-negativeextra","clearstate-indicator-negative","clearstate-indicator-positive","clearstate-indicator-positiveextra","clearstate-indicator-fullclear","clearstate-ap"};
  internal readonly float[] Alpha=new float[6];readonly float[] from=new float[6];
  internal int Current=5,Target=5;internal float FullBacking=1,NormalBacking,PositiveNumber,NegativeNumber;
  double last=double.NaN,started;float fromFull,fromNormal;bool busy;
  internal NativeGaugeTransition(){Alpha[5]=1;}
  internal static float Curve(float t){return .5f-.5f*Mathf.Cos(Mathf.Clamp01(t)*Mathf.PI);}
  void Snap(int state){Current=state;for(int i=0;i<6;i++)Alpha[i]=i==state?1:0;FullBacking=state>=4?1:0;NormalBacking=1-FullBacking;busy=false;}
  internal void Advance(int target,double now){Target=Math.Max(0,Math.Min(5,target));if(double.IsNaN(now)||double.IsInfinity(now))return;if(!double.IsNaN(last)&&now<last){Snap(Target);PositiveNumber=Target==2||Target==3?1:0;NegativeNumber=Target<2?1:0;last=now;return;}
   float dt=double.IsNaN(last)?0:(float)(now-last);last=now;
   if(busy){float t=Curve((float)((now-started)/.3));for(int i=0;i<6;i++)Alpha[i]=Mathf.Lerp(from[i],i==Current?1:0,t);FullBacking=Mathf.Lerp(fromFull,Current>=4?1:0,t);NormalBacking=Mathf.Lerp(fromNormal,Current<4?1:0,t);if(now-started>=.3)busy=false;}
   if(!busy&&Current!=Target){
    if(Target>=4&&Target>Current)Snap(Target);
    else {int next=Current+Math.Sign(Target-Current);if(next!=Target)Snap(next);else{Array.Copy(Alpha,from,6);fromFull=FullBacking;fromNormal=NormalBacking;Current=next;started=now;busy=true;}}
   }
   if(Current>=4){PositiveNumber=NegativeNumber=0;}else{bool positive=Current>=2;PositiveNumber=Mathf.MoveTowards(PositiveNumber,positive?1:0,dt/.2f);NegativeNumber=Mathf.MoveTowards(NegativeNumber,positive?0:1,dt/.2f);}
  }
 }
}
