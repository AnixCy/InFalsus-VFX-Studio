using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio.TimeGroups {
 internal struct ScopedGuide {internal BeatGridPoint Point;internal double Z;}
 internal static class GroupMeasureLines {
  internal static void Install(Harmony h){h.Patch(typeof(NoteRenderer).GetMethod("DrawBeats",TimingGroups.F),new HarmonyMethod(typeof(GroupMeasureLines).GetMethod("Draw",TimingGroups.F)));}
  // Same visibility integration and strongest-position rule as the editor.
  // Persistent bars always use the default group's beat AND scroll timelines.
  // Local authoring beats remain local, but never acquire bar styling/counts.
  internal static ScopedGuide[] Guides(GroupModel model,string name,ScrollTimeline editingScroll,bool snap,double now,float units,float near,float far,double end,int division){
   var result=new Dictionary<double,ScopedGuide>();
   Action<ScrollTimeline,AuthoringGrid,bool> collect=(scroll,grid,bars)=>{
    var seen=new HashSet<double>();double low=near/units,high=far/units;
    for(int i=0;i<scroll.Points.Count;i++){
     var point=scroll.Points[i];double a=Math.Max(0,point.Time),relative=InFalsusStudio.Performance.FastScrollHooks.Compute(scroll,point.Time,now),b=i+1<scroll.Points.Count?scroll.Points[i+1].Time:end;
     if(Math.Abs(point.Speed)<1e-12){if(relative<low||relative>high)continue;}
     else {double t0=point.Time+(low-relative)/point.Speed,t1=point.Time+(high-relative)/point.Speed;a=Math.Max(a,Math.Min(t0,t1));b=i+1<scroll.Points.Count?Math.Min(b,Math.Max(t0,t1)):Math.Max(t0,t1);}
     if(b<a)continue;
     foreach(var original in bars?grid.BarsBetween(a,b):grid.Between(a,b,division)){
      var line=original;if(!seen.Add(line.TimeMs))continue;
      if(!bars&&line.IsBar)line.IsBar=false;
      double z=InFalsusStudio.Performance.FastScrollHooks.Compute(scroll,line.TimeMs,now)*units;if(z<near-.01f||z>far+.01f)continue;
      double key=Math.Round(z,7);ScopedGuide previous;
      if(!result.TryGetValue(key,out previous)||line.Priority>previous.Point.Priority||line.Priority==previous.Point.Priority&&line.Segment>previous.Point.Segment)result[key]=new ScopedGuide{Point=line,Z=z};
     }
    }
   };
   if(snap)collect(editingScroll,new AuthoringGrid(model.Beat(name)),false);
   // Linear-time layout intentionally changes depth mapping, not musical phase.
   bool linear=!ReferenceEquals(TimingGroups.Boot,null)&&(bool)GroupRuntime.Get(TimingGroups.Boot,"linearTimeLayout");
   collect(linear?editingScroll:model.Scroll(""),new AuthoringGrid(model.Beat("")),true);
   return result.Values.OrderByDescending(g=>g.Z).ToArray();
  }
  static bool Draw(NoteRenderer __instance,bool __0){
   // Harmony can still run this replacement prefix after another prefix has
   // replayed the complete grid. Never append the timegroup guides twice.
   if(InFalsusStudio.Performance.StaticBeatMeshes.Replaying(__instance))return false;
   var boot=TimingGroups.Boot;if(ReferenceEquals(boot,null)||boot.History==null)return true;
   var model=TimingGroups.Model(boot.History.Document);if(!model.Extended||model.Errors.Count>0)return true;
   var p=(StudioProfile)GroupRuntime.Get(__instance,"p");var grid=(MeshBatch)GroupRuntime.Get(__instance,"grid");var camera=(Camera)GroupRuntime.Get(__instance,"camera");
   var guides=Guides(model,TimingGroups.Current,(ScrollTimeline)GroupRuntime.Get(__instance,"scroll"),__0,(double)GroupRuntime.Get(__instance,"now"),p.UnitsPerMs,p.stageNear,p.stageFar,(double)GroupRuntime.Get(__instance,"gridEndTime"),(int)GroupRuntime.Get(__instance,"gridDivision"));
   foreach(var g in guides){ InFalsusStudio.Performance.BeatHoverIndex.Record(__instance,g.Point.TimeMs,(float)g.Z);
    var line=g.Point;Color c=new Color(.55f,.55f,.55f,.65f);
    if(__0)switch(line.Kind){case BeatGridKind.Bar:c=new Color(.80f,.50f,1,1);break;case BeatGridKind.Quarter:c=new Color(.16f,.55f,1,1);break;case BeatGridKind.Eighth:c=new Color(.25f,1,.43f,.93f);break;case BeatGridKind.Sixteenth:c=new Color(1,.38f,.45f,.93f);break;default:c=new Color(1,1,1,.88f);break;}
    float pixels=!__0?1.15f:line.IsBar?3.5f:line.IsBeat?2.5f:1.65f;
    for(int lane=0;lane<6;lane++)grid.ScreenStrip(camera,BeatGuideGeometry.Point(p,lane,0,(float)g.Z),BeatGuideGeometry.Point(p,lane,1,(float)g.Z),InFalsusStudio.Performance.EditingGridStrokes.Pixels(pixels),InFalsusStudio.Performance.EditingGridStrokes.Stronger(c));
   }
   typeof(NoteRenderer).GetField("<DrawnGridLines>k__BackingField",TimingGroups.F).SetValue(__instance,guides.Length);
   typeof(NoteRenderer).GetField("<DrawnMeasureLines>k__BackingField",TimingGroups.F).SetValue(__instance,guides.Count(g=>g.Point.IsBar));return false;
  }
 }
}
