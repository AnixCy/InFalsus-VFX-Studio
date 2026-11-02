using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class StaticBeatMeshes {
  internal static bool Enabled=true;internal static int Hits,Misses;
  sealed class Cache {internal object[] Key;internal Vector3[] V;internal Vector2[] U,S;internal Color[] C;internal int[] I;internal int Lines,Bars;internal bool Replay;}
  static readonly ConditionalWeakTable<NoteRenderer,Cache> cache=new ConditionalWeakTable<NoteRenderer,Cache>();
  static readonly System.Reflection.FieldInfo lines=typeof(NoteRenderer).GetField("<DrawnGridLines>k__BackingField",TimingGroups.F),bars=typeof(NoteRenderer).GetField("<DrawnMeasureLines>k__BackingField",TimingGroups.F);
  static object[] Key(NoteRenderer r,bool snap){
   var p=(StudioProfile)GroupRuntime.Get(r,"p");var camera=(Camera)GroupRuntime.Get(r,"camera");var boot=TimingGroups.Boot;var model=boot==null||boot.History==null?null:TimingGroups.Model(boot.History.Document);bool extended=model!=null&&model.Extended&&model.Errors.Count==0;
   return new object[]{GroupRuntime.Get(r,"scroll"),extended?model.Beat(TimingGroups.Current):(object)GroupRuntime.Get(r,"authoringGrid"),extended?model.Beat(""):null,extended?model.Scroll(""):null,extended?TimingGroups.Current:"",GroupRuntime.Get(r,"now"),GroupRuntime.Get(r,"gridEndTime"),GroupRuntime.Get(r,"gridDivision"),snap,p.UnitsPerMs,p.centralHalfWidth,p.sideRun,p.sideRise,p.stageNear,p.stageFar,camera.worldToCameraMatrix,camera.projectionMatrix,camera.pixelRect,camera.nearClipPlane,Screen.width,Screen.height};
  }
  internal static void Invalidate(NoteRenderer renderer){if(renderer!=null)cache.Remove(renderer);}
  internal static bool Replaying(NoteRenderer renderer){Cache c;return cache.TryGetValue(renderer,out c)&&c.Replay;}
  static bool Draw(NoteRenderer __instance,bool __0,out bool __state){
   Cache prior;if(cache.TryGetValue(__instance,out prior))prior.Replay=false;
   __state=false;var boot=TimingGroups.Boot;if(!Enabled||boot==null||((StudioTransport)GroupRuntime.Get(boot,"transport")).Playing)return true;var c=cache.GetValue(__instance,r=>new Cache());var key=Key(__instance,__0);var batch=(MeshBatch)GroupRuntime.Get(__instance,"grid");
   if(c.Key!=null&&c.Key.SequenceEqual(key)&&batch.Vertices.Count==0&&BeatHoverIndex.Replay(__instance,__0)){Hits++;c.Replay=true;batch.Vertices.AddRange(c.V);batch.Uvs.AddRange(c.U);batch.States.AddRange(c.S);batch.Colors.AddRange(c.C);batch.Triangles.AddRange(c.I);lines.SetValue(__instance,c.Lines);bars.SetValue(__instance,c.Bars);return false;}
   Misses++;c.Key=key;__state=true;return true;
  }
  static void Store(NoteRenderer __instance,bool __state){if(!__state)return;var c=cache.GetValue(__instance,r=>new Cache());var b=(MeshBatch)GroupRuntime.Get(__instance,"grid");c.V=b.Vertices.ToArray();c.U=b.Uvs.ToArray();c.S=b.States.ToArray();c.C=b.Colors.ToArray();c.I=b.Triangles.ToArray();c.Lines=(int)lines.GetValue(__instance);c.Bars=(int)bars.GetValue(__instance);}
  internal static void Install(Harmony h){h.Patch(typeof(NoteRenderer).GetMethod("DrawBeats",TimingGroups.F),new HarmonyMethod(typeof(StaticBeatMeshes).GetMethod("Draw",TimingGroups.F)){priority=Priority.First+1000},new HarmonyMethod(typeof(StaticBeatMeshes).GetMethod("Store",TimingGroups.F)));}
 }
}