using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class DragInput {
  internal static bool Enabled=true;
  sealed class Pending {internal EditHistory History;internal object Anchor;internal Vector2 Point,Applied;internal EventModifiers Modifiers;internal bool Queued,Any,HasApplied;}
  static readonly ConditionalWeakTable<StudioBootstrap,Pending> pending=new ConditionalWeakTable<StudioBootstrap,Pending>();
  [ThreadStatic]static bool flushing;
  internal static int AppliedCount;
  static readonly MethodInfo preview=typeof(StudioBootstrap).GetMethod("PreviewNoteDrag",TimingGroups.F);
  static bool Queue(StudioBootstrap __instance,Event __0){
   if(!Enabled||flushing||__0==null||__0.type!=EventType.MouseDrag||!__instance.History.InTransaction)return true;
   var p=pending.GetValue(__instance,x=>new Pending());p.History=__instance.History;p.Anchor=GroupRuntime.Get(__instance,"dragEvent");p.Point=(Vector2)GroupRuntime.Get(__instance,"pointerNow");p.Modifiers=__0.modifiers;p.Queued=p.Any=true;return false;
  }
  static void Flush(StudioBootstrap __instance){
   Pending p;if(!pending.TryGetValue(__instance,out p)||!p.Queued)return;p.Queued=false;
   if(p.History!=__instance.History||!p.History.InTransaction||p.Anchor==null||!ReferenceEquals(p.Anchor,GroupRuntime.Get(__instance,"dragEvent"))){p.Any=false;return;}
   typeof(StudioBootstrap).GetField("pointerNow",TimingGroups.F).SetValue(__instance,p.Point);
   flushing=true;try{preview.Invoke(__instance,new object[]{new Event{type=EventType.MouseDrag,button=0,modifiers=p.Modifiers,mousePosition=p.Point}});AppliedCount++;p.Applied=p.Point;p.HasApplied=true;}finally{flushing=false;}
  }
  static void Release(StudioBootstrap __instance){
   Pending p;if(!pending.TryGetValue(__instance,out p)||!p.Any)return;
   // MouseUp updates pointerNow before ReleaseGesture. Include its final
   // coordinate even when it arrives before the queued frame preview.
   var last=(Vector2)GroupRuntime.Get(__instance,"pointerNow");if(!p.HasApplied||last!=p.Applied){p.Point=last;p.Queued=true;}Flush(__instance);p.Any=false;
  }
  static void Cancel(StudioBootstrap __instance){pending.Remove(__instance);}
  static HarmonyMethod H(string n){return new HarmonyMethod(typeof(DragInput).GetMethod(n,TimingGroups.F)){priority=Priority.First+500};}
  internal static void Install(Harmony h){h.Patch(preview,H("Queue"));h.Patch(typeof(StudioBootstrap).GetMethod("LateUpdate",TimingGroups.F),H("Flush"));h.Patch(typeof(StudioBootstrap).GetMethod("ReleaseGesture",TimingGroups.F),H("Release"));foreach(string n in new[]{"CancelGesture","BeginNoteDrag"})h.Patch(typeof(StudioBootstrap).GetMethod(n,TimingGroups.F),H("Cancel"));}
 }
}