using System;using System.Collections.Generic;using System.Linq;using InFalsusStudio.Core;using UnityEngine;
namespace InFalsusStudio.Gameplay {
 // Literal SPC2 slot arithmetic. New depletion starts 500ms at this sample,
 // without retroactively spending the rest of dt in the newly-created cooldown.
 internal struct SkyBudget {
  internal double Time,Capacity,Cooldown;internal bool Captured;
  internal static void Advance(ref SkyBudget b,double target,int coverage){double dt=target-b.Time;if(dt<0||double.IsNaN(dt))return;double previous=b.Cooldown;b.Cooldown=Math.Max(0,previous-dt);if(previous>0&&b.Cooldown==0){b.Capacity=300;dt-=previous;}
   if(coverage!=2){if(coverage==1&&b.Cooldown==0)b.Capacity=Math.Min(300,b.Capacity+dt);else b.Capacity=Math.Max(0,b.Capacity-2*dt);if(b.Capacity<=0&&b.Cooldown==0){b.Cooldown=500;b.Captured=false;}}
   if(b.Cooldown>0)b.Captured=false;else if(coverage==1&&b.Capacity>0)b.Captured=true;b.Time=target;
  }
 }
 internal sealed class SkyGroupState {
  internal int Id;internal JudgeNote[] Members;internal SkyBudget Current,Previous;internal bool Initialized,HasPrevious,Active;internal double ClipUntil=double.NegativeInfinity;
  internal int Coverage(double time,double x){bool active=false;foreach(var n in Members){var e=n.Source;if(time<e.TimeMs)break;if(time>e.EndMs||time==e.EndMs&&e.EndMs>e.TimeMs)continue;active=true;var range=ChartMath.SkyAt(e,time);if(x>=range.Left-.02&&x<=range.Right+.02)return 1;}return active?0:2;}
  internal void Update(double now,double x){if(!Initialized){Current=new SkyBudget{Time=now,Capacity=300};Initialized=true;}if(now<Current.Time)return;if(now>Current.Time){Previous=Current;HasPrevious=true;}int coverage=Coverage(now,x);Active=coverage!=2;SkyBudget.Advance(ref Current,now,coverage);if(Ready)ClipUntil=Math.Max(ClipUntil,now);}
  internal bool Ready{get{return Initialized&&Active&&Current.Captured&&Current.Cooldown==0;}}
  internal int Judge(JudgeNote note,double sample,double x){SkyBudget b;if(HasPrevious&&sample<Previous.Time){if(note.Source.EndMs<Current.Time)return 1;b=Current;}else{b=HasPrevious?Previous:new SkyBudget{Time=sample,Capacity=300};SkyBudget.Advance(ref b,sample,Coverage(sample,x));}if(b.Cooldown>0)return 1;if(b.Captured)return 6;return note.Source.EndMs<=Current.Time?1:0;}
  internal float Red{get{if(!Initialized||!Active)return 0;double r=1-Current.Capacity/300;return r<=.25?0:(float)(r*r);}}
 }
 internal sealed partial class NativeJudgeEngine {
  internal readonly Dictionary<int,SkyGroupState> SkyGroupsById=new Dictionary<int,SkyGroupState>();
  void BuildSkyGroups(){var sky=Notes.Where(n=>n.Source.Kind==EventKind.SkyArea).ToArray();var parent=new Dictionary<int,int>();var labels=new Dictionary<int,int>();var explicitGroups=new Dictionary<int,int>();foreach(var n in sky){parent[n.Id]=n.Id;labels[n.Id]=n.Group;}
   Func<int,int> root=null;root=id=>{int p=parent[id];if(p!=id)parent[id]=root(p);return parent[id];};Action<int,int> join=(left,right)=>{left=root(left);right=root(right);if(left==right||labels[left]>=0&&labels[right]>=0&&labels[left]!=labels[right])return;parent[right]=left;if(labels[left]<0)labels[left]=labels[right];};
   foreach(var n in sky)if(n.Group>=0){int first;if(explicitGroups.TryGetValue(n.Group,out first))join(first,n.Id);else explicitGroups[n.Group]=n.Id;}
   for(int i=1;i<sky.Length;i++)if(Math.Abs(sky[i].Source.TimeMs-sky[i-1].Source.EndMs)<=2&&(sky[i].Group<0||sky[i-1].Group<0))join(sky[i-1].Id,sky[i].Id);
   var roots=new Dictionary<int,int>();foreach(var n in sky){int r=root(n.Id),id;if(!roots.TryGetValue(r,out id))roots[r]=id=n.Id;n.Group=id;var args=n.Source.Args;if(args.Length<11){Array.Resize(ref args,11);n.Source.Args=args;}n.Source.Args[10]=id;}
   foreach(var g in sky.GroupBy(n=>n.Group))SkyGroupsById[g.Key]=new SkyGroupState{Id=g.Key,Members=g.ToArray()};
  }
  void UpdateSkyGroups(double now,GameplayInputSampler input){Captured=false;foreach(var state in SkyGroupsById.Values){state.Update(now,input.X);if(state.Active){Capacity=state.Current.Capacity;Cooldown=state.Current.Cooldown;Clock=state.Current.Time;Captured|=state.Ready;if(state.Ready)foreach(var n in state.Members)if(now>=n.Source.TimeMs&&now<n.Source.EndMs)ActiveSky=n;}}}
  internal SkyGroupState SkyState(int sourceId){JudgeNote n;SkyGroupState g;return BySource.TryGetValue(sourceId,out n)&&n.Source.Kind==EventKind.SkyArea&&SkyGroupsById.TryGetValue(n.Group,out g)?g:null;}
  internal bool SkyActive(int sourceId){var state=SkyState(sourceId);return state!=null&&state.Ready;}
  internal double SkyFrontier(int sourceId,double start){var state=SkyState(sourceId);return state==null?start:Math.Max(start,state.ClipUntil);}
  void Sky(JudgeNote n,GameplayInputSampler input){var e=n.Source;if(Now<e.TimeMs)return;var g=SkyState(e.SourceId);if(g==null)return;for(int i=n.Next;i<n.Ticks.Length;i++){var t=n.Ticks[i];if(t.Done)continue;if(t.Time>Now)break;double sample=t.Time;if(sample==e.EndMs&&e.EndMs>e.TimeMs)sample-=.001;int grade=g.Judge(n,sample,input.X);if(grade!=0)Settle(n,t,grade,0);}
   if(g.Ready&&Now<e.EndMs){ActiveSky=n;Capacity=g.Current.Capacity;Cooldown=g.Current.Cooldown;Captured=true;Clock=g.Current.Time;}
  }
 }
 internal static class SkyGameplayPalette {
  internal static readonly Color Main=new Color(.688345373f,.627936959f,.971698105f,1),MainRed=new Color(.839622617f,.502981484f,.629674256f,1);
  internal static readonly Color Edge=new Color(1.35792816f,1.15355456f,4.23709488f,1),EdgeRed=new Color(3.65158081f,.809548557f,.94217658f,1);
  internal static readonly Color Glow=new Color(.36470589f,.196078435f,.850980401f,0),Back=new Color(.496224105f,.441527218f,.773584902f,.129411772f),BackRed=new Color(.909803927f,.701960802f,1,.098039217f);
  static float Root(float x){return Mathf.Sign(x)*Mathf.Pow(Mathf.Abs(x),1f/3f);}
  static Vector3 Lab(Color c){float l=Root(.412221462f*c.r+.536332548f*c.g+.0514459945f*c.b),m=Root(.211903498f*c.r+.680699527f*c.g+.10739696f*c.b),s=Root(.0883024633f*c.r+.28171885f*c.g+.629978716f*c.b);return new Vector3(.210454255f*l+.793617785f*m-.00407204684f*s,1.9779985f*l-2.42859221f*m+.45059371f*s,.0259040371f*l+.782771766f*m-.808675766f*s);}
  internal static Color Blend(Color from,Color to,float t){if(t<=0)return from;if(t>=1)return to;Vector3 c=Vector3.LerpUnclamped(Lab(from),Lab(to),t);float l=c.x+.396337777f*c.y+.215803757f*c.z,m=c.x-.105561346f*c.y-.0638541728f*c.z,s=c.x-.0894841775f*c.y-1.29148555f*c.z;l=l*l*l;m=m*m*m;s=s*s*s;return new Color(4.0767417f*l-3.3077116f*m+.230969936f*s,-1.26843798f*l+2.60975742f*m-.341319382f*s,-.00419608643f*l-.703418612f*m+1.70761466f*s,Mathf.LerpUnclamped(from.a,to.a,t));}
  internal static void Apply(Material material,int sourceId,bool ghost){var g=GameplaySession.Active?GameplaySession.Engine.SkyState(sourceId):null;float t=ghost||g==null?0:g.Red;var main=Blend(Main,MainRed,t);var edge=Blend(Edge,EdgeRed,t);var back=Blend(Back,BackRed,t);var glow=Glow;if(ghost){main.a*=.35f;edge.a*=.35f;back.a*=.35f;glow.a*=.35f;}material.SetVector("_Color",(Vector4)main);material.SetVector("_EdgeCenterColor",(Vector4)edge);material.SetVector("_EdgeGlowColor",(Vector4)glow);material.SetVector("_FrontBackColor",(Vector4)back);}
 }
}
