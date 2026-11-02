using InFalsusStudio.Core;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
 internal static class NativeGroundProjection {
  // GameScene Track: safeAreaShadowPrefab=929 (SkySafeArea.isShadow=1),
  // flickNoteShadowPrefab=902, child ShadowQuad=903. Reuse their original
  // stencil materials, including their separate colors/alpha and queue 3006.
  internal const float GroundY=0;
  internal const float FlickWidthScale=1.125f,FlickDepth=.14f;
  static bool Visible(object renderer){return ((NoteRenderer)renderer).ShowGroundProjections&&!(bool)NativeVisualPlugin.Get(renderer,"ghost");}
  internal static void Sky(object renderer,object note,Range source){
   if(!Visible(renderer)||!source.Any||!InFalsusStudio.Performance.PreviewFrustum.Surface((NoteRenderer)renderer,source,true))return;
   // SkySafeArea._KZ calls the same _lA._aB mesh builder for body and
   // shadow. The shadow prefab lies on the floor and skips body color state.
   var slot=Hooks.Copy(renderer,source,"sky-projection-"+NativeVisualPlugin.Get(note,"SourceId"),NativeVisualPlugin.Mat("SkySafeShadowPrefabMat"));
   if(!slot.ReusedSource){var vertices=slot.Mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].y=GroundY;slot.Mesh.vertices=vertices;slot.Mesh.RecalculateBounds();}
  }
  internal static void Flick(object renderer,object note){
   if(!Visible(renderer))return;
   var n=(SpcEvent)note;var p=(StudioProfile)NativeVisualPlugin.Get(renderer,"p");var range=ChartMath.FlickRange(n);
   // Track._gBA 0x6764c9: root scale X = normalized width * 4.
   // ShadowQuad: X=.45, rotated X=90deg, Z offset=-.07; root Y=180deg.
   // Native floor width is 1.6, hence the deliberate 1.8/1.6 width ratio.
   float center=StageSpace.AirX(p,range.Center),half=(float)range.Width*p.centralHalfWidth*FlickWidthScale;
   float z=(float)NativeVisualPlugin.Call(renderer,"FlickZ",n),depth=FlickDepth*p.centralHalfWidth/.8f;
   var vertices=new[]{new Vector3(center-half,GroundY,z),new Vector3(center+half,GroundY,z),new Vector3(center+half,GroundY,z+depth),new Vector3(center-half,GroundY,z+depth)};
   var slot=NativeVisualPlugin.Visuals(renderer).Take("flick-projection",NativeVisualPlugin.Mat("FlickShadowPrefabMat"));
   NativeTrack.ClipMesh(slot.Mesh,vertices,new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},new[]{0,2,1,0,3,2},p.stageNear,p.stageFar);
  }
 }
}
