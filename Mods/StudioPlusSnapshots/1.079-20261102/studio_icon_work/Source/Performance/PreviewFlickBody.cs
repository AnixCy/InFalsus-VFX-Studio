using System;
using System.Linq.Expressions;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.NativeVisual;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class PreviewFlickBody {
  internal static bool Enabled=true;
  static readonly Func<NoteRenderer,MeshBatch> surface=RendererAccess.Field<MeshBatch>("air");
  static readonly Func<NoteRenderer,bool> ghost=RendererAccess.Field<bool>("ghost");
  static readonly Func<NoteRenderer,SpcEvent,float> z=Z();
  static Func<NoteRenderer,SpcEvent,float> Z(){var r=Expression.Parameter(typeof(NoteRenderer));var e=Expression.Parameter(typeof(SpcEvent));return Expression.Lambda<Func<NoteRenderer,SpcEvent,float>>(Expression.Call(r,typeof(NoteRenderer).GetMethod("FlickZ",TimingGroups.F),e),r,e).Compile();}
  static bool Draw(NoteRenderer __instance,SpcEvent __0){
   var r=__instance;var e=__0;if(!Enabled||!PreviewFrustum.Playing||!NativeVisualPlugin.Active||!NativeVisualPlugin.NotesEnabled.Value||ghost(r))return true;
   var p=RendererAccess.Profile(r);var camera=RendererAccess.Camera(r);var range=ChartMath.FlickRange(e);bool right=e.Get(4)==4;
   float xl=StageSpace.AirX(p,range.Left),xr=StageSpace.AirX(p,range.Right),depth=z(r,e),y=p.skyHeight+.019f;var body=surface(r);
   Color color=right?p.rightFlick:p.leftFlick;const int count=48;
   for(int i=0;i<count;i++){float u=i/(float)count,v=(i+1)/(float)count;
    var a=new Vector3(Mathf.Lerp(right?xr:xl,right?xl:xr,u),y,depth);var b=new Vector3(Mathf.Lerp(right?xr:xl,right?xl:xr,v),y,depth);
    var c=FlickGeometry.BackVertex(p,camera,xl,xr,y,depth,right,v);var d=FlickGeometry.BackVertex(p,camera,xl,xr,y,depth,right,u);
    Color baseColor=color;baseColor.a=.54f;Color rim=Color.Lerp(color,Color.white,.45f);rim.a=.65f;
    body.Quad(a,b,c,d,baseColor,baseColor,rim,rim);
   }
   return false;
  }
  internal static void Install(Harmony h){h.Patch(typeof(NoteRenderer).GetMethod("DrawFlick",TimingGroups.F),new HarmonyMethod(typeof(PreviewFlickBody).GetMethod("Draw",TimingGroups.F)){priority=Priority.Last});}
 }
}
