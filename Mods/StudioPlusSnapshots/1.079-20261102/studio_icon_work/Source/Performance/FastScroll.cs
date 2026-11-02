using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using System.Linq;
using System.Reflection.Emit;
using InFalsusStudio.TimeGroups;
using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio.Performance {
 internal static class P1Switches {internal static bool Scroll=true,Rendering=true;}
 internal struct AccurateSum {
  internal double Hi,Lo;
  internal void Add(double x){Add(new AccurateSum{Hi=x});}
  internal void Add(AccurateSum b){double s=Hi+b.Hi,v=s-Hi,e=(Hi-(s-v))+(b.Hi-v)+Lo+b.Lo;Hi=s+e;Lo=e-(Hi-s);}
  internal double Value {get{return Hi+Lo;}}
 }
 // Local interval sums only: never subtract huge whole-song position prefixes.
 // Leaves contain 32 original affine products; a balanced tree stores hi/lo sums.
 internal sealed class FastScroll {
  internal readonly double[] Time,Speed;readonly AccurateSum[] tree;readonly int leaves;
  const int Block=32;
  static readonly FieldInfo points=typeof(ScrollTimeline).GetField("points",BindingFlags.Instance|BindingFlags.NonPublic);
  static readonly ConditionalWeakTable<ScrollTimeline,FastScroll> cache=new ConditionalWeakTable<ScrollTimeline,FastScroll>();
  internal static FastScroll For(ScrollTimeline scroll){return cache.GetValue(scroll,s=>new FastScroll(s));}
  FastScroll(ScrollTimeline scroll){
   var source=(List<ScrollTimeline.Point>)points.GetValue(scroll);Time=new double[source.Count];Speed=new double[source.Count];for(int i=0;i<Time.Length;i++){Time[i]=source[i].Time;Speed[i]=source[i].Speed;}
   leaves=1;while(leaves<Math.Max(1,(Time.Length-1+Block-1)/Block))leaves*=2;tree=new AccurateSum[leaves*2];
   for(int i=0;i+1<Time.Length;i++)tree[leaves+i/Block].Add((Time[i+1]-Time[i])*Speed[i]);
   for(int i=leaves-1;i>0;i--){tree[i]=tree[i*2];tree[i].Add(tree[i*2+1]);}
  }
  internal int Index(double time){int lo=0,hi=Time.Length;while(lo<hi){int m=(lo+hi)/2;if(Time[m]<=time)lo=m+1;else hi=m;}return Math.Max(0,lo-1);}
  AccurateSum Blocks(int begin,int end){var left=new AccurateSum();var right=new AccurateSum();for(int a=leaves+begin,b=leaves+end;a<b;a/=2,b/=2){if((a&1)!=0)left.Add(tree[a++]);if((b&1)!=0){var part=tree[--b];part.Add(right);right=part;}}left.Add(right);return left;}
  internal double Delta(double time,double now){
   if(time==now)return 0;if(!AuthoringSafety.Finite(time)||!AuthoringSafety.Finite(now))return double.NaN;
   double a=Math.Min(time,now),b=Math.Max(time,now);int first=Index(a),last=Index(b);
   if(last-first<Block*2){double sum=0,correction=0;int i=first;while(a<b){double segmentEnd=i+1<Time.Length?Math.Min(b,Time[i+1]):b;double term=(segmentEnd-a)*Speed[i]-correction,next=sum+term;correction=(next-sum)-term;sum=next;a=segmentEnd;if(i+1<Time.Length)i++;else break;}return time>=now?sum:-sum;}
   var total=new AccurateSum();total.Add((Time[first+1]-a)*Speed[first]);int begin=first+1,end=last;
   while(begin<end&&begin%Block!=0){total.Add((Time[begin+1]-Time[begin])*Speed[begin]);begin++;}
   int fullEnd=end-end%Block;if(begin<fullEnd){total.Add(Blocks(begin/Block,fullEnd/Block));begin=fullEnd;}
   while(begin<end){total.Add((Time[begin+1]-Time[begin])*Speed[begin]);begin++;}
   total.Add((b-Time[last])*Speed[last]);return time>=now?total.Value:-total.Value;
  }
  internal bool Intersects(double start,double end,double now,double units,double near,double far){
   if(end<=start||units<=0)return false;
   for(int i=Index(start);i<Time.Length&&Time[i]<end;i++){double a=Math.Max(start,Time[i]),b=i+1<Time.Length?Math.Min(end,Time[i+1]):end;if(b<=a)continue;double za=Delta(a,now)*units,zb=Delta(b,now)*units;if(AuthoringSafety.Finite(za)&&AuthoringSafety.Finite(zb)&&Math.Max(za,zb)>=near&&Math.Min(za,zb)<=far)return true;}return false;
  }
  internal List<ScrollVisibility.Slice> Between(double start,double end,double now,double units,double near,double far){
   var output=new List<ScrollVisibility.Slice>();
   if(!AuthoringSafety.Finite(start)||!AuthoringSafety.Finite(end)||!AuthoringSafety.Finite(now)||!AuthoringSafety.Finite(units)||units<=0||!AuthoringSafety.Finite(near)||!AuthoringSafety.Finite(far)||far<near||end<start)return output;
   for(int i=Index(start);i<Time.Length&&Time[i]<end;i++){double a=Math.Max(start,Time[i]),b=i+1<Time.Length?Math.Min(end,Time[i+1]):end;if(b<=a)continue;double z=Delta(a,now)*units,slope=Speed[i]*units;if(!AuthoringSafety.Finite(z)||!AuthoringSafety.Finite(slope))continue;if(Math.Abs(slope)<1e-15){if(z>=near&&z<=far)output.Add(new ScrollVisibility.Slice(a,b));continue;}double n=a+(near-z)/slope,f=a+(far-z)/slope,first=Math.Max(a,Math.Min(n,f)),last=Math.Min(b,Math.Max(n,f));if(last>first)output.Add(new ScrollVisibility.Slice(first,last));}return output;
  }
 }
 internal static class FastScrollHooks {
  static readonly BindingFlags F=BindingFlags.Static|BindingFlags.NonPublic;
  internal static void Install(Harmony h){foreach(string name in new[]{"Intersects","Between"}){Type target=typeof(ScrollVisibility);h.Patch(target.GetMethod(name),new HarmonyMethod(typeof(FastScrollHooks).GetMethod(name,F)));}
   foreach(string name in new[]{"Visible","DrawGround","DrawSky","DrawFlick","DrawBeats","DrawSkyBrackets","DrawCursor","Sample"})h.Patch(typeof(NoteRenderer).GetMethod(name,TimingGroups.F),transpiler:new HarmonyMethod(typeof(FastScrollHooks).GetMethod("MathCalls",TimingGroups.F)));
  }
  static bool Safe(double a,double b){return AuthoringSafety.Finite(a)&&AuthoringSafety.Finite(b)&&Math.Abs(a)<=1e12&&Math.Abs(b)<=1e12;}
  internal static double Compute(ScrollTimeline timeline,double time,double now){return P1Switches.Scroll&&Safe(time,now)?FastScroll.For(timeline).Delta(time,now):timeline.Delta(time,now);}
  internal static float WorldZ(NoteRenderer renderer,double time){return (float)(Compute(RendererAccess.Scroll(renderer),time,RendererAccess.Now(renderer))*RendererAccess.Profile(renderer).UnitsPerMs);}
  internal static Vector3 SkyPoint(NoteRenderer renderer,double norm,double time){var p=RendererAccess.Profile(renderer);return new Vector3(StageSpace.AirX(p,norm),p.skyHeight+.006f,WorldZ(renderer,time));}
  static IEnumerable<CodeInstruction> MathCalls(IEnumerable<CodeInstruction> input){foreach(var c in input){var m=c.operand as MethodInfo;string name=null;if(m!=null&&m.DeclaringType==typeof(ScrollTimeline)&&m.Name=="Delta")name="Compute";if(m!=null&&m.DeclaringType==typeof(NoteRenderer)&&m.Name=="Z")name="WorldZ";if(m!=null&&m.DeclaringType==typeof(NoteRenderer)&&m.Name=="SkyPoint")name="SkyPoint";if(name!=null){c.opcode=OpCodes.Call;c.operand=typeof(FastScrollHooks).GetMethod(name,TimingGroups.F);}yield return c;}}

  static bool Intersects(ScrollTimeline __0,double __1,double __2,double __3,double __4,double __5,double __6,ref bool __result){if(!P1Switches.Scroll||__0==null||!Safe(__1,__2)||!Safe(__3,__4))return true;__result=FastScroll.For(__0).Intersects(__1,__2,__3,__4,__5,__6);return false;}
  static bool Between(ScrollTimeline __0,double __1,double __2,double __3,double __4,double __5,double __6,ref List<ScrollVisibility.Slice> __result){if(!P1Switches.Scroll||__0==null||!Safe(__1,__2)||!Safe(__3,__4))return true;__result=FastScroll.For(__0).Between(__1,__2,__3,__4,__5,__6);return false;}
 }
}
