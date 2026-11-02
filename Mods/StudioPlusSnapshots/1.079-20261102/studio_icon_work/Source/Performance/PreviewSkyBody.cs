using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.NativeVisual;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 // NativeAir replaces all legacy borders/ribs. Build its unchanged source
 // surface directly during playback; paused editing retains the legacy path.
 internal static class PreviewSkyBody {
  internal static bool Enabled=true;
  static readonly Func<NoteRenderer,MeshBatch> skin=RendererAccess.Field<MeshBatch>("skySkin");
  static readonly Func<NoteRenderer,bool> ghost=RendererAccess.Field<bool>("ghost"),hide=RendererAccess.Field<bool>("hidePast");
  static readonly Func<NoteRenderer,AutoplayCursor> cursor=RendererAccess.Field<AutoplayCursor>("visualCursor");
  static readonly Func<NoteRenderer,SpcEvent,bool> selected=Selected();
  static readonly Action<NoteRenderer,SpcEvent,double,double,int,List<double>> sample=Sample();
  static readonly Action<NoteRenderer> crossing=Crossing();
  static Func<NoteRenderer,SpcEvent,bool> Selected(){var r=Expression.Parameter(typeof(NoteRenderer));var e=Expression.Parameter(typeof(SpcEvent));return Expression.Lambda<Func<NoteRenderer,SpcEvent,bool>>(Expression.Call(r,typeof(NoteRenderer).GetMethod("IsSelected",TimingGroups.F),Expression.Field(e,"SourceId")),r,e).Compile();}
  static Action<NoteRenderer,SpcEvent,double,double,int,List<double>> Sample(){var r=Expression.Parameter(typeof(NoteRenderer));var e=Expression.Parameter(typeof(SpcEvent));var a=Expression.Parameter(typeof(double));var b=Expression.Parameter(typeof(double));var d=Expression.Parameter(typeof(int));var t=Expression.Parameter(typeof(List<double>));return Expression.Lambda<Action<NoteRenderer,SpcEvent,double,double,int,List<double>>>(Expression.Call(r,typeof(NoteRenderer).GetMethod("Sample",TimingGroups.F),e,a,b,d,t),r,e,a,b,d,t).Compile();}
  static Action<NoteRenderer> Crossing(){var r=Expression.Parameter(typeof(NoteRenderer));var f=Expression.Field(r,typeof(NoteRenderer).GetField("<CrossingSegments>k__BackingField",TimingGroups.F));return Expression.Lambda<Action<NoteRenderer>>(Expression.Block(Expression.PostIncrementAssign(f),Expression.Empty()),r).Compile();}
  static bool Draw(NoteRenderer __instance,SpcEvent __0){
   var r=__instance;var e=__0;
   if(!Enabled||!PreviewFrustum.Playing||!NativeVisualPlugin.Active||!NativeVisualPlugin.NotesEnabled.Value||ghost(r)||e.DurationMs<=0||selected(r,e))return true;
   var p=RendererAccess.Profile(r);var scroll=RendererAccess.Scroll(r);double now=RendererAccess.Now(r),begin=hide(r)?InFalsusStudio.Gameplay.GameplaySession.SkyClipBegin(e.TimeMs,now,e):e.TimeMs,end=e.EndMs;
   if(end<=begin)return true;
   bool active=NoteSkinPolicy.Active(e,now,cursor(r)!=null);var body=skin(r);
   var spans=ScrollVisibility.Between(scroll,begin,end,now,p.UnitsPerMs,p.stageNear,p.stageFar);
   var first=ChartMath.SkyAt(e,e.TimeMs);var last=ChartMath.SkyAt(e,e.EndMs);
   bool straight=first.Left==last.Left&&first.Right==last.Right;
   foreach(var span in spans){var samples=new List<double>{span.Start};if(straight)Straight(r,span.Start,span.End,0,samples);else sample(r,e,span.Start,span.End,0,samples);
    for(int i=0;i<samples.Count-1;i++){
     double ta=samples[i],tb=samples[i+1];var ra=ChartMath.SkyAt(e,ta);var rb=ChartMath.SkyAt(e,tb);
     if(ra.Width < -1e-6||rb.Width < -1e-6){crossing(r);continue;}
     var a=FastScrollHooks.SkyPoint(r,ra.Left,ta);var b=FastScrollHooks.SkyPoint(r,ra.Right,ta);var c=FastScrollHooks.SkyPoint(r,rb.Right,tb);var d=FastScrollHooks.SkyPoint(r,rb.Left,tb);
     float va=(float)((ta-e.TimeMs)/400),vb=(float)((tb-e.TimeMs)/400);
     body.QuadUv(a,b,c,d,new Vector2(0,va),new Vector2(1,va),new Vector2(1,vb),new Vector2(0,vb),Color.white,new Vector2(active?1:0,0));
    }
   }
   return false;
  }
  static void Straight(NoteRenderer r,double a,double b,int depth,List<double> target){bool split=Mathf.Abs(FastScrollHooks.WorldZ(r,b)-FastScrollHooks.WorldZ(r,a))>2f||b-a>80;if(split&&depth<10&&b-a>.2&&target.Count<4096){double mid=(a+b)*.5;Straight(r,a,mid,depth+1,target);Straight(r,mid,b,depth+1,target);}else target.Add(b);}
  internal static void Install(Harmony h){h.Patch(typeof(NoteRenderer).GetMethod("DrawSky",TimingGroups.F),new HarmonyMethod(typeof(PreviewSkyBody).GetMethod("Draw",TimingGroups.F)){priority=Priority.Last});}
 }
}
