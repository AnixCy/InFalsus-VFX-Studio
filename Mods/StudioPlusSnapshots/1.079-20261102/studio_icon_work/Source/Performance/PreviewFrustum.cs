using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class PreviewFrustum {
  internal static bool Enabled=true;internal static long Culled;
  sealed class Cache {internal int Frame=-1;internal Matrix4x4 Matrix;internal readonly Vector4[] Planes=new Vector4[6];}
  static readonly ConditionalWeakTable<Camera,Cache> cameras=new ConditionalWeakTable<Camera,Cache>();
  internal static bool Playing {get{var boot=TimingGroups.Boot;return Enabled&&boot!=null&&((StudioTransport)GroupRuntime.Get(boot,"transport")).Playing;}}
  internal static bool Visible(Camera camera,Bounds bounds){
   if(!Enabled||camera==null)return true;var c=cameras.GetValue(camera,x=>new Cache());var matrix=camera.cullingMatrix;
   if(c.Frame!=Time.frameCount||c.Matrix!=matrix){var w=matrix.GetRow(3);c.Planes[0]=w+matrix.GetRow(0);c.Planes[1]=w-matrix.GetRow(0);c.Planes[2]=w+matrix.GetRow(1);c.Planes[3]=w-matrix.GetRow(1);c.Planes[4]=w+matrix.GetRow(2);c.Planes[5]=w-matrix.GetRow(2);c.Frame=Time.frameCount;c.Matrix=matrix;}
   var center=bounds.center;var extent=bounds.extents;
   foreach(var plane in c.Planes){float distance=plane.x*center.x+plane.y*center.y+plane.z*center.z+plane.w,radius=Mathf.Abs(plane.x)*extent.x+Mathf.Abs(plane.y)*extent.y+Mathf.Abs(plane.z)*extent.z;if(distance+radius<-.00001f){Culled++;return false;}}return true;
  }
  internal static bool Surface(NoteRenderer renderer,InFalsusStudio.NativeVisual.Range range,bool floor){
   if(!Playing)return true;var vertices=((MeshBatch)range.Batch).Vertices;if(vertices.Count<=range.V)return false;
   Vector3 min=vertices[range.V],max=min;for(int i=range.V+1;i<vertices.Count;i++){min=Vector3.Min(min,vertices[i]);max=Vector3.Max(max,vertices[i]);}
   if(floor)min.y=max.y=0;var bounds=new Bounds((min+max)*.5f,max-min);bounds.Expand(.002f);return Visible(RendererAccess.Camera(renderer),bounds);
  }
  static bool Note(NoteRenderer __instance,SpcEvent __0,ref bool __result){
   if(!Playing||!AirReachBounds.IsAir(__0))return true;
   // Native 0/1/2 boundary easing stays between endpoint extrema. Unknown
   // curves retain the original renderer, including its full safety handling.
   if(__0.Kind==EventKind.SkyArea&&(__0.Get(7)<0||__0.Get(7)>2||__0.Get(8)<0||__0.Get(8)>2))return true;
   var a=__0.Kind==EventKind.Flick?ChartMath.FlickRange(__0):ChartMath.SkyAt(__0,__0.TimeMs);var b=__0.Kind==EventKind.Flick?a:ChartMath.SkyAt(__0,__0.EndMs);
   double lo=Math.Min(a.Left,b.Left),hi=Math.Max(a.Right,b.Right);if(!AuthoringSafety.Finite(lo)||!AuthoringSafety.Finite(hi)||hi<lo)return true;
   var p=RendererAccess.Profile(__instance);float left=StageSpace.AirX(p,lo),right=StageSpace.AirX(p,hi),near=p.stageNear-p.tapDepth-.25f,far=p.stageFar+.25f;
   var bounds=new Bounds(new Vector3((left+right)*.5f,p.skyHeight*.5f,(near+far)*.5f),new Vector3(Mathf.Abs(right-left)+p.centralHalfWidth*.5f,p.skyHeight+2,far-near));
   if(Visible(RendererAccess.Camera(__instance),bounds))return true;__result=false;return false;
  }
  internal static void Install(Harmony h){h.Patch(typeof(NoteRenderer).GetMethod("Visible",TimingGroups.F),new HarmonyMethod(typeof(PreviewFrustum).GetMethod("Note",TimingGroups.F)){priority=Priority.Last});}
 }
}
