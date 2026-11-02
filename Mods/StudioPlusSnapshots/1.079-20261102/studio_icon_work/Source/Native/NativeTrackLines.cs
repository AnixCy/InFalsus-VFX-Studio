using System;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
 // Original SectionControllableHighlightBlendedVariant, its three unmodified
 // floorline atlases and the native .4675..5325 UV strip. Split the geometry,
 // not the texture/shader, to expose the vertical and lateral marks separately.
 internal static class NativeTrackLines {
  static Material source;
  internal static void Init(){
   var dependency=NativeVisualPlugin.FeedbackBundle;
   source=dependency.LoadAsset<Material>("native/feedback/track-highlight");
   if(!source||!source.shader||!source.shader.isSupported||!source.HasProperty("_Progress")||!source.HasProperty("_UnderlyingBaseMap"))throw new Exception("Native TrackControllableHighlight material unavailable");
  }
  internal static float Fade(double now,double end){return (float)Math.Max(0,Math.Min(1,1-(now-end)/500));}
  internal static float SplitU(float uv){return (uv-.4675f)/.065f;}
  internal static void Draw(object renderer,object events,double now,bool feedback,double lowerBound){
   if(!NativeVisualPlugin.Active||!NativeVisualPlugin.StageEnabled.Value)return;
   bool vertical=NativeVisualPlugin.TrackLines.Value,horizontal=vertical;if(!vertical)return;
   var p=NativeVisualPlugin.Get(renderer,"p");float near=NativeVisualPlugin.N(p,"stageNear"),far=NativeVisualPlugin.N(p,"stageFar"),half=NativeVisualPlugin.N(p,"centralHalfWidth"),length=6*half/.8f;
   var powers=NativeHitEffects.TrackPowers(renderer,events,now,feedback,lowerBound);var bound=NativeVisualPlugin.T("StageSpace").GetMethod("FloorBound",NativeVisualPlugin.F);
   for(int lane=1;lane<=4;lane++)for(int part=0;part<3;part++){
    if(part==1?!vertical:!horizontal)continue;
    // Native line/highlight images keep a clear central gutter. .49/.51
    // separate the center strokes from the horizontal marks on both sides.
    float u0=part==0?.4675f:part==1?.49f:.51f,u1=part==0?.49f:part==1?.51f:.5325f;
    float t0=Mathf.Lerp(.0125f,.9875f,SplitU(u0)),t1=Mathf.Lerp(.0125f,.9875f,SplitU(u1));
    var slot=NativeVisualPlugin.Visuals(renderer).Take("trackmarks",source);
    slot.Material.renderQueue=2910;
    var l=(Vector3)bound.Invoke(null,new object[]{p,lane,1f,false,0f,0f});var r=(Vector3)bound.Invoke(null,new object[]{p,lane,1f,true,0f,0f});
    var a=Vector3.LerpUnclamped(l,r,t0);var b=Vector3.LerpUnclamped(l,r,t1);a.y+=.006f;b.y+=.006f;
    var c=b;var d=a;c.z=d.z=length;
    NativeTrack.ClipMesh(slot.Mesh,new[]{a,b,c,d},new[]{new Vector2(u0,0),new Vector2(u1,0),new Vector2(u1,1),new Vector2(u0,1)},new[]{0,2,1,0,3,2},near,far);
    slot.Material.SetFloat("_Cull",0);slot.Material.SetFloat("_BaseAlpha",1);slot.Material.SetFloat("_Progress",powers[lane]);slot.Material.SetFloat("_ModulateProgress",InFalsusStudio.Gameplay.GameplaySession.Active?InFalsusStudio.Gameplay.GameplaySession.Presentation.Modulation(lane):0);
   }
  }
  internal static void Dispose(){source=null;}
 }
 public sealed partial class NativeVisualPlugin {
  internal static BepInEx.Configuration.ConfigEntry<bool> TrackVertical,TrackHorizontal,TrackLines,SideTriangles;
  internal static AssetBundle FeedbackBundle;
  void InitTrackLines(){
   SideTriangles=Config.Bind("Display","NativeSideTriangles",true,"Show the original red triangle strips outside the side lanes. Rendering only.");
   TrackVertical=Config.Bind("Display","TrackVerticalLines",true,"Native center vertical floorline strokes and their hit highlight.");
   TrackHorizontal=Config.Bind("Display","TrackHorizontalLines",true,"Native horizontal floorline marks beside each center stroke and their hit highlight.");
   TrackLines=Config.Bind("Display","TrackLines",TrackVertical.Value||TrackHorizontal.Value,"Master switch for native floorline strokes, lateral marks and their hit highlight.");
   FeedbackBundle=deps.Find(b=>b.LoadAsset<Material>("native/feedback/track-highlight"));
   if(!FeedbackBundle)throw new Exception("Native track line dependency missing");NativeTrackLines.Init();
  }
 }
}

