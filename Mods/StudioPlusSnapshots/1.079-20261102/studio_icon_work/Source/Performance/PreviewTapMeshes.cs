using InFalsusStudio.Core;
using InFalsusStudio.NativeVisual;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class PreviewTapMeshes {
  internal static bool Enabled=true;internal static long Hits;
  static readonly System.Func<NoteRenderer,bool> ghost=RendererAccess.Field<bool>("ghost");
  internal static bool Try(object renderer,object note,Material material,out Slot slot){
   slot=null;var r=(NoteRenderer)renderer;var e=(SpcEvent)note;
   if(!Enabled||!PreviewFrustum.Playing||e.Kind!=EventKind.Tap||ghost(r)||RendererAccess.Selected(r,e.SourceId))return false;
   var p=RendererAccess.Profile(r);float z=FastScrollHooks.WorldZ(r,e.TimeMs),width=(float)e.GroundWidth;
   if(z<p.stageNear||z+p.tapDepth>p.stageFar)return false;
   slot=NativeVisualPlugin.Visuals(renderer).Take("ground-"+e.SourceId,material);
   var shape=new Vector4(p.centralHalfWidth,p.sideRun,p.sideRise,p.tapWidthRatio);var lane=new Vector4(e.Lane,width,p.tapDepth,0);
   if(!slot.TapMeshReady||slot.TapShape!=shape||slot.TapLane!=lane){
    var source=NativeVisualPlugin.Meshes["note01"];var bounds=source.bounds;var vertices=source.vertices;float inset=(1-p.tapWidthRatio)*width*.5f;
    var a=StageSpace.FloorBound(p,e.Lane,width,false,0,inset);var b=StageSpace.FloorBound(p,e.Lane,width,true,0,inset);
    for(int i=0;i<vertices.Length;i++){var point=Vector3.Lerp(a,b,(vertices[i].x-bounds.min.x)/bounds.size.x);point.z=(bounds.max.y-vertices[i].y)/bounds.size.y*p.tapDepth;point.y+=vertices[i].z*((e.Lane==0||e.Lane==5)?2f:p.centralHalfWidth/.8f);vertices[i]=point;}
    var triangles=source.triangles;
    // Match ClipMesh's expanded triangle source and normals exactly, once.
    NativeTrack.ClipMesh(slot.Mesh,vertices,source.uv,triangles,float.MinValue,float.MaxValue);
    slot.TapShape=shape;slot.TapLane=lane;slot.TapMeshReady=true;
   }else Hits++;
   slot.Object.transform.localPosition=new Vector3(0,0,z);slot.ReusedSource=true;
   return true;
  }
 }
}
