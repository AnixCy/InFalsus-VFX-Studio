using System;
using System.Collections.Generic;
using HarmonyLib;
using InFalsusStudio;
using InFalsusStudio.Core;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 internal struct PlacementLocator {
  internal bool Valid,Air,DirectDepth,Snapped;internal double Time,X;internal int Lane;internal Vector3 Point;
 }
 public sealed partial class NativeEditorUIPlugin {
  BepInEx.Configuration.ConfigEntry<bool> pointerTetherConfig,crosshairConfig;
  delegate bool LocatorPlaneFn(StudioBootstrap boot,Vector2 pointer,bool air,int lane,out Vector3 point);
  delegate bool LocatorFloorFn(StudioBootstrap boot,Vector2 pointer,out Vector3 point,out int lane);
  delegate void LocatorLineFn(StudioBootstrap boot,Vector2 a,Vector2 b,Color color,float width);
  LocatorPlaneFn locatorPlane;LocatorFloorFn locatorFloor;LocatorLineFn locatorLine;
  internal static bool PassivePointerOptimization=true;internal static long PassiveMovesSkipped,RadialMovesSkipped;
  Action<StudioBootstrap,Vector2> writeRadialPointer;
  bool pointerUiCaptured;
  Vector2 authoringGuiPointer;bool authoringPointerValid;
  internal bool CrosshairEnabled {get{return crosshairConfig!=null&&crosshairConfig.Value&&boot!=null&&Val<RadialMenuState>(boot,"radial").InPointMode;}}
  bool crosshairPointMode;
  void TickCrosshairMode(){
   if(boot==null)return;bool active=Val<RadialMenuState>(boot,"radial").InPointMode;
   if(active==crosshairPointMode)return;crosshairPointMode=active;CrosshairSettingChanged();
  }
  void CrosshairSettingChanged(){
   if(boot==null)return;Set(boot,"guideActive",false);InFalsusStudio.Performance.StaticBeatMeshes.Invalidate((NoteRenderer)Get(boot,"notes"));
  }
  Vector2 radialGuideStart;bool radialGuideStarted;Texture2D radialPointerDot;
  readonly List<Rect> locatorObstacles=new List<Rect>(12);
  struct LocatorStroke {internal Vector2 A,B;internal Color Color;internal float Width;internal bool Start,End,PadStart,PadEnd;}
  readonly List<LocatorStroke> locatorStrokes=new List<LocatorStroke>(32);
  List<Vector2> locatorIntervals=new List<Vector2>(12),locatorNext=new List<Vector2>(12);
  static readonly Color LocatorCyan=new Color(184/255f,240/255f,244/255f,.98f),LocatorBlue=new Color(76/255f,200/255f,1,.98f),LocatorShadow=new Color(.015f,.05f,.075f,.92f);
  static readonly Color LocatorDot=new Color(212/255f,1,246/255f,1);
  void InitQuickPlaceGuides(){
   pointerTetherConfig=Config.Bind("Display","PointerTether",true,"Show the bright-blue pointer tether in Quick Place menus.");
   crosshairConfig=Config.Bind("Display","TrackCrosshair",true,"Show pale-cyan track crosshairs. Hidden while creating air notes; transverse lines remain visible when snapped, and free mode retains both axes.");
   crosshairConfig.SettingChanged+=delegate{CrosshairSettingChanged();};
   locatorPlane=(LocatorPlaneFn)Delegate.CreateDelegate(typeof(LocatorPlaneFn),Method("PlanePoint"));locatorFloor=(LocatorFloorFn)Delegate.CreateDelegate(typeof(LocatorFloorFn),Method("FloorPoint"));locatorLine=(LocatorLineFn)Delegate.CreateDelegate(typeof(LocatorLineFn),Method("ClippedGuiLine"));
   var owner=System.Linq.Expressions.Expression.Parameter(typeof(StudioBootstrap));var point=System.Linq.Expressions.Expression.Parameter(typeof(Vector2));
   writeRadialPointer=System.Linq.Expressions.Expression.Lambda<Action<StudioBootstrap,Vector2>>(System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Field(owner,Field("radialPointer")),point),owner,point).Compile();
   harmony.Patch(typeof(NoteRenderer).GetMethod("Draw",F),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("PreviewGrid",F)));
   harmony.Patch(Method("OnGUI"),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("PassiveMove",F)){priority=Priority.First+3000});
   harmony.Patch(Method("RefreshAuthoringHover"),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("HoverWork",F)){priority=Priority.First+1000});
   harmony.Patch(Method("DrawAuthoringOverlay"),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("PaintOnly",F)));
   harmony.Patch(Method("DrawPointPreview"),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("BeginPointPreview",F)),finalizer:new HarmonyMethod(typeof(LocatorHooks).GetMethod("EndPointPreview",F)));
   harmony.Patch(Method("GuideLine"),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("PlacementGuide",F)));
   harmony.Patch(Method("DrawTimeGuide"),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("TimeGuide",F)));
   harmony.Patch(Method("DrawAuthoringOverlay"),postfix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("GridHover",F)));
   Patch("BuildRadialButtons","BuildRadial","BuiltRadial");
   harmony.Patch(Method("HandleRadialEvent"),prefix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("BeforeMenu",F)),postfix:new HarmonyMethod(typeof(LocatorHooks).GetMethod("AfterMenu",F)));
  }
  PlacementLocator LocatePoint(Vector2 pointer){
   var result=new PlacementLocator();string tool=Get(boot,"tool").ToString();result.Air=tool=="Sky"||tool=="FlickLeft"||tool=="FlickRight";
   Vector3 point;int pickedLane=1;if(result.Air?!locatorPlane(boot,pointer,true,1,out point):!locatorFloor(boot,pointer,out point,out pickedLane))return result;
   result.Point=point;result.Lane=pickedLane;result.X=(point.x/Profile.centralHalfWidth+1)*.5;result.Valid=result.DirectDepth=true;return result;
  }
  object freeHoverHistory;ScrollTimeline freeHoverScroll;int freeHoverRevision,freeHoverWidth,freeHoverHeight;
  bool freeHoverCached,freeHoverValid;double freeHoverNow,freeHoverLimit,freeHoverTime;Vector2 freeHoverPointer;
  Matrix4x4 freeHoverView,freeHoverProjection;Rect freeHoverPixel;Vector4 freeHoverGeometry;
  internal static long FreeHoverResolves;
  internal bool TryFreeHoverTime(Vector2 pointer,out double time){
   var camera=boot.ViewCamera;var p=Profile;var scroll=(ScrollTimeline)Prop(boot,"EffectiveScroll");double now=boot.CurrentMs,limit=Math.Max(Val<double>(boot,"durationMs")+60000,now+60000);
   var geometry=new Vector4(p.centralHalfWidth,p.sideRun,p.sideRise,p.UnitsPerMs);
   if(freeHoverCached&&ReferenceEquals(freeHoverHistory,boot.History)&&freeHoverRevision==boot.History.Revision&&ReferenceEquals(freeHoverScroll,scroll)&&freeHoverNow==now&&freeHoverLimit==limit&&freeHoverPointer==pointer&&freeHoverWidth==Screen.width&&freeHoverHeight==Screen.height&&freeHoverView==camera.worldToCameraMatrix&&freeHoverProjection==camera.projectionMatrix&&freeHoverPixel==camera.pixelRect&&freeHoverGeometry==geometry){time=freeHoverTime;return freeHoverValid;}
   freeHoverCached=true;freeHoverHistory=boot.History;freeHoverRevision=boot.History.Revision;freeHoverScroll=scroll;freeHoverNow=now;freeHoverLimit=limit;freeHoverPointer=pointer;freeHoverWidth=Screen.width;freeHoverHeight=Screen.height;freeHoverView=camera.worldToCameraMatrix;freeHoverProjection=camera.projectionMatrix;freeHoverPixel=camera.pixelRect;freeHoverGeometry=geometry;
   Vector3 point;int lane;freeHoverValid=false;freeHoverTime=time=0;
   if(!locatorFloor(boot,pointer,out point,out lane)||p.UnitsPerMs<=0)return false;
   int candidates;bool interval;double resolved;FreeHoverResolves++;
   double target=scroll.PositionAt(now)+point.z/p.UnitsPerMs;
   freeHoverValid=EditTimeResolver.Resolve(scroll,target,now,limit,out resolved,out candidates,out interval);
   if(!freeHoverValid){
    // Ray/projection coordinates are floats; a stop has exactly one depth.
    // Retain the native inverse rules, allowing only subpixel float roundoff
    // at a stopped depth when the unadjusted target has no solution.
    double position=scroll.PositionAt(now);
    foreach(var stop in scroll.Points)if(Math.Abs(stop.Speed)<1e-12){
     var atStop=point;atStop.z=(float)((stop.Position-position)*p.UnitsPerMs);var projected=camera.WorldToScreenPoint(atStop);
     if(projected.z<=camera.nearClipPlane||(new Vector2(projected.x,Screen.height-projected.y)-pointer).sqrMagnitude>.0025f)continue;
     if(EditTimeResolver.Resolve(scroll,stop.Position,now,limit,out resolved,out candidates,out interval)){freeHoverValid=true;break;}
    }
   }
   if(freeHoverValid)freeHoverTime=AuthoringGrid.RoundMs(resolved);time=freeHoverTime;return freeHoverValid;
  }
  internal static List<Vector3[]> LocatorSegments(StudioProfile p,PlacementLocator locator,float z,double low,double high){
   var lines=new List<Vector3[]>();var point=locator.Point;point.z=z;
   if(locator.Air){lines.Add(new[]{new Vector3(StageSpace.AirX(p,low),point.y,z),new Vector3(StageSpace.AirX(p,high),point.y,z)});}
   else {
    var a=BeatGuideGeometry.Point(p,locator.Lane,0,z);var b=BeatGuideGeometry.Point(p,locator.Lane,1,z);
    float u=Mathf.Abs(b.x-a.x)<1e-7f?0:(point.x-a.x)/(b.x-a.x),lift=point.y-Mathf.LerpUnclamped(a.y,b.y,u);
    for(int lane=0;lane<6;lane++){var l=BeatGuideGeometry.Point(p,lane,0,z);var r=BeatGuideGeometry.Point(p,lane,1,z);l.y+=lift;r.y+=lift;lines.Add(new[]{l,r});}
   }
   lines.Add(new[]{new Vector3(point.x,point.y,p.stageNear),new Vector3(point.x,point.y,p.stageFar)});return lines;
  }
  internal static bool ClipLocatorNear(Camera camera,ref Vector3 a,ref Vector3 b){
   var position=camera.transform.position;var forward=camera.transform.forward;float near=camera.nearClipPlane+.001f;
   float da=Vector3.Dot(a-position,forward)-near,db=Vector3.Dot(b-position,forward)-near;
   if(da<0&&db<0)return false;if(da<0)a=Vector3.LerpUnclamped(a,b,da/(da-db));else if(db<0)b=Vector3.LerpUnclamped(b,a,db/(db-da));return true;
  }
  static bool LocatorBound(float p,float q,ref float low,ref float high){if(Mathf.Abs(p)<1e-7f)return q>=0;float v=q/p;if(p<0){if(v>high)return false;if(v>low)low=v;}else{if(v<low)return false;if(v<high)high=v;}return true;}
  void LocatorLine(Vector2 a,Vector2 b,Color color,float width){
   var delta=b-a;locatorIntervals.Clear();locatorIntervals.Add(new Vector2(0,1));
   foreach(var obstacle in locatorObstacles){if(obstacle.width<=0||obstacle.height<=0)continue;float lo=0,hi=1;
    if(!LocatorBound(-delta.x,a.x-obstacle.xMin,ref lo,ref hi)||!LocatorBound(delta.x,obstacle.xMax-a.x,ref lo,ref hi)||!LocatorBound(-delta.y,a.y-obstacle.yMin,ref lo,ref hi)||!LocatorBound(delta.y,obstacle.yMax-a.y,ref lo,ref hi))continue;
    locatorNext.Clear();foreach(var span in locatorIntervals){if(hi<=span.x||lo>=span.y)locatorNext.Add(span);else{if(lo>span.x)locatorNext.Add(new Vector2(span.x,lo));if(hi<span.y)locatorNext.Add(new Vector2(hi,span.y));}}
    var swap=locatorIntervals;locatorIntervals=locatorNext;locatorNext=swap;if(locatorIntervals.Count==0)return;
   }
   foreach(var span in locatorIntervals){if(span.y-span.x<.000001f)continue;locatorStrokes.Add(new LocatorStroke{A=a+delta*span.x,B=a+delta*span.y,Color=color,Width=width,Start=span.x==0,End=span.y==1});}
  }
  void JoinLocatorLines(){
   // Join only shared authored endpoints. Never extend across a UI-clipped gap.
   for(int i=0;i<locatorStrokes.Count;i++){
    var s=locatorStrokes[i];bool start=false,end=false;
    for(int j=0;j<locatorStrokes.Count;j++){if(i==j)continue;var other=locatorStrokes[j];
     start|=s.Start&&(other.Start&&(s.A-other.A).sqrMagnitude<.000001f||other.End&&(s.A-other.B).sqrMagnitude<.000001f);
     end|=s.End&&(other.Start&&(s.B-other.A).sqrMagnitude<.000001f||other.End&&(s.B-other.B).sqrMagnitude<.000001f);
    }
    s.PadStart=start;s.PadEnd=end;locatorStrokes[i]=s;
   }
  }
  void FlushLocatorLines(){
   JoinLocatorLines();
   foreach(var s in locatorStrokes)PaintLocatorStroke(s,LocatorShadow,s.Width+Mathf.Max(2,2*scale));
   foreach(var s in locatorStrokes)PaintLocatorStroke(s,s.Color,s.Width);
  }
  void PaintLocatorStroke(LocatorStroke stroke,Color color,float width){
   var pad=(stroke.B-stroke.A).normalized*.5f;locatorLine(boot,stroke.A-(stroke.PadStart?pad:Vector2.zero),stroke.B+(stroke.PadEnd?pad:Vector2.zero),color,width);
  }
  void LocatorWorldLine(Vector3 a,Vector3 b){
   var camera=boot.ViewCamera;if(!ClipLocatorNear(camera,ref a,ref b))return;var x=camera.WorldToScreenPoint(a);var y=camera.WorldToScreenPoint(b);
   LocatorLine(new Vector2(x.x,Screen.height-x.y),new Vector2(y.x,Screen.height-y.y),LocatorCyan,Mathf.Max(1.3f,1.7f*scale));
  }
  bool DrawLocator(PlacementLocator locator){
   if(!locator.Valid)return false;float previous=scale;scale=Convert.ToSingle(Prop(boot,"OverlayScale"));
   try{
    locatorStrokes.Clear();locatorObstacles.Clear();locatorObstacles.Add(leftRect);locatorObstacles.Add(rightRect);locatorObstacles.Add(footerRect);
    if(!string.IsNullOrEmpty(panel))locatorObstacles.Add(panelRect);if(notePopupOpen)locatorObstacles.Add(notePopupRect);
    if(WaveVisible)locatorObstacles.Add(waveRect);
    if(Val<bool>(boot,"guiShowSongHud")){locatorObstacles.Add(Val<Rect>(boot,"hudSongRect"));locatorObstacles.Add(Val<Rect>(boot,"hudScoreRect"));}
    // The four central lanes are collinear: draw one continuous center stroke.
    float z=locator.DirectDepth?locator.Point.z:(float)Call(boot,"Depth",locator.Time);var p=Profile;var point=locator.Point;point.z=z;
    {
     if(locator.Air)LocatorWorldLine(new Vector3(StageSpace.AirX(p,AirGridRange.Low),point.y,z),new Vector3(StageSpace.AirX(p,AirGridRange.High),point.y,z));
     else {var a=BeatGuideGeometry.Point(p,locator.Lane,0,z);var b=BeatGuideGeometry.Point(p,locator.Lane,1,z);float u=Mathf.Abs(b.x-a.x)<1e-7f?0:(point.x-a.x)/(b.x-a.x),lift=point.y-Mathf.LerpUnclamped(a.y,b.y,u);
      for(int lane=0;lane<6;lane++){int last=lane==1?4:lane;var l=BeatGuideGeometry.Point(p,lane,0,z);var r=BeatGuideGeometry.Point(p,last,1,z);l.y+=lift;r.y+=lift;LocatorWorldLine(l,r);lane=last;}
     }
    }
    LocatorWorldLine(new Vector3(point.x,point.y,p.stageNear),new Vector3(point.x,point.y,p.stageFar));
    FlushLocatorLines();return true;
   }finally{scale=previous;}
  }
  void DrawRangeTimeGuide(double time){
   float previous=scale;scale=Convert.ToSingle(Prop(boot,"OverlayScale"));
   try{
    locatorStrokes.Clear();locatorObstacles.Clear();float z=(float)Call(boot,"Depth",time);
    // A single center stroke avoids the four independently rasterized lane seams.
    for(int lane=0;lane<6;lane++){int last=lane==1?4:lane;
     var a=BeatGuideGeometry.Point(Profile,lane,0,z);var b=BeatGuideGeometry.Point(Profile,last,1,z);lane=last;
     var camera=boot.ViewCamera;if(!ClipLocatorNear(camera,ref a,ref b))continue;
     var x=camera.WorldToScreenPoint(a);var y=camera.WorldToScreenPoint(b);
     LocatorLine(new Vector2(x.x,Screen.height-x.y),new Vector2(y.x,Screen.height-y.y),new Color(1,.85f,.22f,.97f),3*scale);
    }
    JoinLocatorLines();foreach(var stroke in locatorStrokes)PaintLocatorStroke(stroke,stroke.Color,stroke.Width);
   }finally{scale=previous;}
  }
  bool DrawRadialLocator(){
   var radial=Val<RadialMenuState>(boot,"radial");if(!crosshairConfig.Value||CreatingAir||!radial.Pressed||formal||!Val<bool>(boot,"showUi")||Transport.Playing||!radial.InPointMode||radial.Level==RadialLevel.Selection||radial.Level==RadialLevel.Home)return false;
   return DrawLocator(LocatePoint(Val<Vector2>(boot,"radialPointer")));
  }
  bool CreatingAir {get{string tool=Get(boot,"tool").ToString();return tool=="Sky"||tool=="FlickLeft"||tool=="FlickRight";}}
  bool GridHoverVisible {get{return CrosshairEnabled&&GridHoverState;}}
  bool GridHoverState {get{return ready&&!formal&&boot!=null&&boot.HasDocument&&Val<bool>(boot,"showUi")&&!CreatingAir&&!Val<PointPlacement>(boot,"pointPlacement").Pending&&(Get(boot,"tool").ToString()=="Select"||Val<bool>(boot,"hoverValid"))&&!Val<RadialMenuState>(boot,"radial").Pressed&&!(bool)Prop(boot,"EditHeld")&&!(bool)Prop(boot,"WorkflowActive")&&(!Val<bool>(boot,"showGrid")||Val<bool>(boot,"guideActive"));}}
  PlacementLocator GridHoverLocator(Vector2 pointer,double time){
   var locator=LocatePoint(pointer);locator.Time=time;locator.Snapped=Val<bool>(boot,"showGrid");float z;
   // Grid depths belong to the already rendered grid. Drawing never resolves SV/time.
   if(locator.Snapped&&InFalsusStudio.Performance.BeatHoverIndex.DepthAt((NoteRenderer)Get(boot,"notes"),time,out z))locator.Point.z=z;
   return locator;
  }
  void DrawPreviewPlacement(){
   if(Event.current==null||Event.current.type!=EventType.Repaint||!ready||formal||boot==null||!boot.HasDocument||!Transport.Playing||!Val<bool>(boot,"showUi")||Val<RadialMenuState>(boot,"radial").Pressed)return;
   var pointer=authoringPointerValid?authoringGuiPointer:Event.current.mousePosition;var camera=boot.ViewCamera;
   if(!camera||!camera.pixelRect.Contains(new Vector2(pointer.x,Screen.height-pointer.y))||Blocked(pointer)||(bool)Call(boot,"HudBlocksInput",pointer)||(bool)Prop(boot,"EditHeld"))return;
   double time;if(!TryFreeHoverTime(pointer,out time))return;
   if(Val<bool>(boot,"showGrid"))time=Convert.ToDouble(Call(boot,"SnapTime",time,false));
   var locator=LocatePoint(pointer);if(!locator.Valid)return;locator.Time=time;locator.DirectDepth=false;locator.Snapped=Val<bool>(boot,"showGrid");
   var draft=Val<PointPlacement>(boot,"pointPlacement");
   if(draft.ChoosingX)time=draft.Phase==PlacementPhase.EndX?draft.EndMs:draft.StartMs;
   if(CreatingAir){Call(boot,"DrawTimeGuide",time,true);if(draft.ChoosingX)Call(boot,"DrawAirRuler",time,locator.X,true);}
   else if(CrosshairEnabled)DrawLocator(locator);
   if(Get(boot,"tool").ToString()=="Select")return;
   var oldTime=Get(boot,"hoverTime");var oldLane=Get(boot,"hoverLane");var oldX=Get(boot,"hoverX");var oldValid=Get(boot,"hoverValid");
   try{Set(boot,"hoverTime",time);Set(boot,"hoverLane",locator.Lane);Set(boot,"hoverX",locator.X);Set(boot,"hoverValid",true);Call(boot,"DrawPointPreview");}
   finally{Set(boot,"hoverTime",oldTime);Set(boot,"hoverLane",oldLane);Set(boot,"hoverX",oldX);Set(boot,"hoverValid",oldValid);}
  }
  Texture2D pointerStrokeBlue,pointerStrokeShadow;float pointerStrokeBlueWidth=-1,pointerStrokeShadowWidth=-1;
  void SmoothPointerStroke(Vector2 a,Vector2 b,Color color,float width,ref Texture2D texture,ref float cachedWidth){
   var delta=b-a;float length=delta.magnitude;if(length<.01f)return;
   // GUI is composited after the camera's MSAA/FXAA pass. Filter the stroke's
   // own alpha coverage, retaining one screen-pixel transition on both edges.
   float extent=width+2;
   if(!texture||Mathf.Abs(cachedWidth-width)>.01f){
    if(texture)Destroy(texture);cachedWidth=width;const int height=64;var pixels=new Color[height];
    for(int y=0;y<height;y++){float distance=Mathf.Abs(((y+.5f)/height-.5f)*extent);pixels[y]=new Color(1,1,1,Mathf.Clamp01(width*.5f+.5f-distance));}
    texture=new Texture2D(1,height,TextureFormat.RGBA32,false);texture.name="Quick Place antialiased stroke";texture.SetPixels(pixels);texture.Apply(false,true);texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;texture.hideFlags=HideFlags.HideAndDontSave;
   }
   var previousMatrix=GUI.matrix;var previousColor=GUI.color;
   try{GUI.color=previousColor*color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y-extent*.5f,length,extent),texture);}
   finally{GUI.matrix=previousMatrix;GUI.color=previousColor;}
  }
  void DisposePointerStrokes(){if(pointerStrokeBlue)Destroy(pointerStrokeBlue);if(pointerStrokeShadow)Destroy(pointerStrokeShadow);pointerStrokeBlue=pointerStrokeShadow=null;}
  void DrawRadialPointer(){
   if(InFalsusStudio.Gameplay.GameplaySession.Active||!pointerTetherConfig.Value||!radialGuideStarted||formal||Transport.Playing||!Val<RadialMenuState>(boot,"radial").Pressed)return;
   var end=Val<Vector2>(boot,"radialPointer");
   // This pass follows every plate and caption, including the Home menu.
   SmoothPointerStroke(radialGuideStart,end,LocatorShadow,Mathf.Max(3,4*scale),ref pointerStrokeShadow,ref pointerStrokeShadowWidth);
   SmoothPointerStroke(radialGuideStart,end,LocatorBlue,Mathf.Max(1.5f,2*scale),ref pointerStrokeBlue,ref pointerStrokeBlueWidth);
   if(!radialPointerDot){
    const int size=32;var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=x+.5f-size*.5f,dy=y+.5f-size*.5f;pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(size*.5f-Mathf.Sqrt(dx*dx+dy*dy)));}
    radialPointerDot=new Texture2D(size,size,TextureFormat.RGBA32,false);radialPointerDot.SetPixels(pixels);radialPointerDot.Apply(false,true);radialPointerDot.hideFlags=HideFlags.HideAndDontSave;radialPointerDot.filterMode=FilterMode.Bilinear;
   }
   var previous=GUI.color;GUI.color=LocatorDot;
   try{float startRadius=Mathf.Max(2.5f,3*scale),endRadius=Mathf.Max(4,5*scale);GUI.DrawTexture(new Rect(radialGuideStart.x-startRadius,radialGuideStart.y-startRadius,startRadius*2,startRadius*2),radialPointerDot);GUI.DrawTexture(new Rect(end.x-endRadius,end.y-endRadius,endRadius*2,endRadius*2),radialPointerDot);}finally{GUI.color=previous;}
  }
  static class LocatorHooks {
   static void PreviewGrid(NoteRenderer __instance,ref bool __6){
    if(I==null||!I.ready||I.boot==null||I.formal||!I.boot.HasDocument||!I.Transport.Playing)return;
    if(ReferenceEquals(Get(I.boot,"notes"),__instance)&&Val<bool>(I.boot,"showGrid"))__6=true;
   }
   [ThreadStatic]static bool drawingHoldPreview;
   static void BeginPointPreview(StudioBootstrap __instance,out bool __state){__state=drawingHoldPreview;drawingHoldPreview=I!=null&&I.ready&&Get(__instance,"tool").ToString()=="Hold";}
   static Exception EndPointPreview(bool __state,Exception __exception){drawingHoldPreview=__state;return __exception;}
   static bool PlacementGuide(StudioBootstrap __instance,Vector3 __0,Vector3 __1,Color __2,float __3){
    if(!drawingHoldPreview||I==null||!I.ready)return true;
    var camera=__instance.ViewCamera;if(!ClipLocatorNear(camera,ref __0,ref __1))return false;
    var a=camera.WorldToScreenPoint(__0);var b=camera.WorldToScreenPoint(__1);
    if(!AuthoringSafety.Finite(a.x)||!AuthoringSafety.Finite(a.y)||!AuthoringSafety.Finite(b.x)||!AuthoringSafety.Finite(b.y))return false;
    I.locatorLine(__instance,new Vector2(a.x,Screen.height-a.y),new Vector2(b.x,Screen.height-b.y),__2,__3*(float)Prop(__instance,"OverlayScale"));return false;
   }
   // A passive MouseMove needs no separate IMGUI control walk. Repaint sees the
   // latest pointer every frame. Never skip capture, press/release, wheel or keys.
   static bool PassiveMove(StudioBootstrap __instance){
    if(!PassivePointerOptimization||I==null||!I.ready||I.legacy||Event.current==null)return true;
    I.Bind(__instance);InFalsusStudio.Performance.NativePointerMessages.RefreshGuiPointer(Event.current);I.authoringGuiPointer=Event.current.mousePosition;I.authoringPointerValid=true;if((Event.current.rawType==EventType.Repaint||Event.current.rawType==EventType.Layout)&&Val<RadialMenuState>(__instance,"radial").Pressed)I.writeRadialPointer(__instance,Event.current.mousePosition);
    var e=Event.current;var type=e.rawType;if(type!=EventType.MouseMove&&type!=EventType.MouseDrag)return true;
    if(I.middleDragging)return true;
    if(Val<RadialMenuState>(__instance,"radial").Pressed){
     // Held-menu movement is only a pointer sample. No forms, picking, timing,
     // menu rebuilding or command queues run at mouse polling frequency.
     I.writeRadialPointer(__instance,e.mousePosition);RadialMovesSkipped++;e.Use();return false;
    }
    // A held button during formal playback is still a MouseDrag. It has no
    // editing meaning unless a real UI/progress/scene gesture owns capture.
    if(GUIUtility.hotControl!=0||I.formalScrubbing||Val<bool>(__instance,"progressDragging")||(bool)Prop(__instance,"PointerEditGesture")||Get(__instance,"gesture").ToString()!="None")return true;
    PassiveMovesSkipped++;return false;
   }
   static bool HoverWork(StudioBootstrap __instance){
    if(I==null||!I.ready)return true;
    if(I.formal&&((StudioTransport)Get(__instance,"transport")).Playing){Set(__instance,"guideActive",false);Set(__instance,"hoverValid",false);return false;}
    return Event.current==null||Event.current.type!=EventType.Layout;
   }
   static bool PaintOnly(){return I==null||!I.ready||Event.current==null||Event.current.type==EventType.Repaint;}
   static bool TimeGuide(StudioBootstrap __instance,double __0,bool __1){
    if(I!=null&&I.ready&&ReferenceEquals(I.boot,__instance)&&Val<bool>(__instance,"rangeActive")&&!__1){
     if(Event.current!=null&&Event.current.type==EventType.Repaint)I.DrawRangeTimeGuide(__0);return false;
    }
    return I==null||!I.ready||!I.CrosshairEnabled||!I.GridHoverState;
   }

   static void GridHover(){
    if(I==null||Event.current.type!=EventType.Repaint)return;
    try{
     if(!I.GridHoverVisible)return;
     var pointer=Val<Vector2>(I.boot,"hoverPointer");if(!(bool)Call(I.boot,"AllowedCanvas",pointer))return;
     I.DrawLocator(I.GridHoverLocator(pointer,Val<double>(I.boot,"guideTime")));
    }finally{InFalsusStudio.Performance.CompactOverlayPopup.FlushTime(I.boot);}
   }
   static void BeforeMenu(Event __0,out Vector2? __state){__state=null;if(!InFalsusStudio.Gameplay.GameplaySession.Active&&I!=null&&I.ready&&I.pointerTetherConfig.Value&&I.boot!=null&&__0.rawType==EventType.MouseDown&&__0.button==1&&!Val<RadialMenuState>(I.boot,"radial").Pressed)__state=__0.mousePosition;}
   static void AfterMenu(Vector2? __state){if(InFalsusStudio.Gameplay.GameplaySession.Active||I==null||!I.ready||!I.pointerTetherConfig.Value){if(I!=null)I.radialGuideStarted=false;return;}var radial=Val<RadialMenuState>(I.boot,"radial");if(__state.HasValue&&radial.Pressed){I.radialGuideStart=__state.Value;I.radialGuideStarted=true;}if(!radial.Pressed)I.radialGuideStarted=false;}
  }
 }
}
