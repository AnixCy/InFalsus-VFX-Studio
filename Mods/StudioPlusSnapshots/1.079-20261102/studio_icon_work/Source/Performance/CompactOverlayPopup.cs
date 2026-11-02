using System;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class CompactOverlayPopup {
  sealed class PendingTime {internal bool Valid;internal Vector2 Position;internal string Text;internal float Width;}
  static readonly ConditionalWeakTable<StudioBootstrap,PendingTime> pendingTimes=new ConditionalWeakTable<StudioBootstrap,PendingTime>();
  internal static void ClearTime(StudioBootstrap owner){PendingTime pending;if(pendingTimes.TryGetValue(owner,out pending))pending.Valid=false;}
  internal static bool DeferTime(StudioBootstrap owner,Vector2 pos,string text,float width,bool box){
   if(!box||string.IsNullOrEmpty(text)||!(text.StartsWith("GRID ",StringComparison.Ordinal)||text.StartsWith("FREE ",StringComparison.Ordinal))||!text.EndsWith(" ms",StringComparison.Ordinal))return false;
   var pending=pendingTimes.GetValue(owner,b=>new PendingTime());pending.Valid=true;pending.Position=pos;pending.Text=text;pending.Width=width;return true;
  }
  internal static void FlushTime(StudioBootstrap owner){
   PendingTime pending;if(!pendingTimes.TryGetValue(owner,out pending)||!pending.Valid)return;
   pending.Valid=false;TryDraw(owner,pending.Position,pending.Text,pending.Width,true);
  }
  // Reduce the original area by 25%, preserving its aspect ratio.
  internal const float SizeFactor=.8660254f,Width=138f*SizeFactor,Height=39f*SizeFactor;
  const float CoordinateScale=1f,CoordinateWidthPixels=120*CoordinateScale,CoordinateHeightPixels=34*CoordinateScale;
  static readonly Color ValueInk=new Color(224/255f,246/255f,1,1);
  static readonly Color AxisInk=new Color(108/255f,212/255f,1,1);
  static readonly Color EdgeInk=new Color(124/255f,224/255f,1,1);
  static GUIStyle axis,value,suffix,skyAxis,skyValue,skySuffix;static GUISkin lastSkin;static int lastScale;
  static GUIStyle TextStyle(StudioBootstrap owner,float size,float k,Color ink,bool bold){
   var style=new GUIStyle(OverlayAnnotationLayout.Style(owner,false,size,TextAnchor.MiddleLeft));
   style.fontSize=Mathf.Max(1,Mathf.RoundToInt(size*k));style.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
   style.alignment=TextAnchor.MiddleLeft;style.wordWrap=false;style.clipping=TextClipping.Overflow;
   style.padding=new RectOffset();style.margin=new RectOffset();style.contentOffset=Vector2.zero;
   style.fixedWidth=style.fixedHeight=0;style.normal.textColor=ink;return style;
  }
  static void EnsureStyles(StudioBootstrap owner,float k){
   int key=Mathf.RoundToInt(k*1000);if(axis!=null&&lastSkin==GUI.skin&&lastScale==key)return;
   lastSkin=GUI.skin;lastScale=key;axis=TextStyle(owner,13,k,AxisInk,true);
   value=TextStyle(owner,17,k,ValueInk,true);suffix=TextStyle(owner,12,k,ValueInk,false);
   skyAxis=TextStyle(owner,14,k,AxisInk,true);skyValue=TextStyle(owner,18,k,ValueInk,true);skySuffix=TextStyle(owner,13,k,ValueInk,false);
  }
  static void Fill(Rect rect,Color ink){Color previous=GUI.color;GUI.color=previous*ink;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;}
  static void Bracket(Rect rect,float k){
   float stroke=Mathf.Max(.75f,k),cap=4*k;
   Fill(rect,new Color(11/255f,27/255f,38/255f,.91f));
   Fill(new Rect(rect.x,rect.y,stroke,rect.height),EdgeInk);Fill(new Rect(rect.xMax-stroke,rect.y,stroke,rect.height),EdgeInk);
   Fill(new Rect(rect.x,rect.y,cap,stroke),EdgeInk);Fill(new Rect(rect.x,rect.yMax-stroke,cap,stroke),EdgeInk);
   Fill(new Rect(rect.xMax-cap,rect.y,cap,stroke),EdgeInk);Fill(new Rect(rect.xMax-cap,rect.yMax-stroke,cap,stroke),EdgeInk);
  }
  static void Label(Rect rect,string first,string number,string ending,float k,bool grid=false){
   GUIStyle axisStyle=grid?axis:skyAxis,valueStyle=grid?value:skyValue,suffixStyle=grid?suffix:skySuffix;
   var a=new GUIContent(first);var b=new GUIContent(number);var c=new GUIContent(ending);
   float aw=axisStyle.CalcSize(a).x,bw=valueStyle.CalcSize(b).x,cw=suffixStyle.CalcSize(c).x,gap=3*k;
   float firstGap=grid?axisStyle.CalcSize(new GUIContent(" ")).x:gap,total=aw+bw+cw+firstGap+gap;
   float available=rect.width-16*k,fit=grid?1:Mathf.Min(1,available/Mathf.Max(1,total));
   var matrix=GUI.matrix;var pivot=new Vector2(rect.center.x,rect.y);
   try{
    if(!grid)GUIUtility.ScaleAroundPivot(new Vector2(fit,1),pivot);
    float x=rect.center.x-total*.5f;
    GUI.Label(new Rect(x,rect.y,aw,rect.height),a,axisStyle);x+=aw+firstGap;
    GUI.Label(new Rect(x,rect.y,bw,rect.height),b,valueStyle);x+=bw+gap;
    GUI.Label(new Rect(x,rect.y,cw,rect.height),c,suffixStyle);
   }finally{GUI.matrix=matrix;}
  }
  static float GridWidth(string first,string number,float k){
   return Mathf.Ceil(axis.CalcSize(new GUIContent(first)).x+axis.CalcSize(new GUIContent(" ")).x+
    value.CalcSize(new GUIContent(number)).x+3*k+suffix.CalcSize(new GUIContent("ms")).x+10*k);
  }
  static Rect GridRect(StudioBootstrap owner,Vector2 pointer,float k,float width){
   Rect bounds=OverlayAnnotationLayout.Bounds(owner);float height=Height*k,gap=Mathf.Max(64,64*k);
   float x=pointer.x+gap;
   if(x+width>bounds.xMax)x=pointer.x-gap-width;
   x=Mathf.Clamp(x,bounds.xMin,Mathf.Max(bounds.xMin,bounds.xMax-width));
   float y=Mathf.Clamp(pointer.y,bounds.yMin,Mathf.Max(bounds.yMin,bounds.yMax-height));
   return new Rect(x,y,width,height);
  }
  internal static Rect CoordinateRect(Rect bounds,Vector2 anchor,out float opticalScale){
   // The same pixel-size box appears larger against the narrow distant track.
   // A continuous correction balances that cue without following full perspective.
   float depth=Mathf.InverseLerp(bounds.yMin+bounds.height*.08f,bounds.yMin+bounds.height*.78f,anchor.y);
   opticalScale=Mathf.Lerp(.82f,1,Mathf.SmoothStep(0,1,depth));
   float width=CoordinateWidthPixels*opticalScale,height=CoordinateHeightPixels*opticalScale;
   float x=Mathf.Clamp(anchor.x-width*.5f,bounds.xMin,Mathf.Max(bounds.xMin,bounds.xMax-width));
   float y=Mathf.Clamp(anchor.y-height-10,bounds.yMin,Mathf.Max(bounds.yMin,bounds.yMax-height));
   return new Rect(Mathf.Round(x),Mathf.Round(y),width,height);
  }
  static void Coordinate(StudioBootstrap owner,Vector2 pos,string number,float designWidth,float positionScale){
   // Anchor directly to the projected endpoint. Do not run the per-frame
   // collision search, whose discrete rows/columns jump while the track scrolls.
   var matrix=GUI.matrix;Vector3 anchor=matrix.MultiplyPoint3x4(new Vector3(pos.x+designWidth*.5f*positionScale,pos.y+49*positionScale,0));
   float opticalScale;var rect=CoordinateRect(OverlayAnnotationLayout.Bounds(owner),new Vector2(anchor.x,anchor.y),out opticalScale);
   OverlayAnnotationLayout.State(owner).Labels.Add(new OverlayAnnotationLayout.Annotation{Text="X "+number+" / 100",Rect=rect,Box=true});
   if(Event.current.type!=EventType.Repaint)return;
   try{
    GUI.matrix=Matrix4x4.TRS(new Vector3(rect.x,rect.y,0),Quaternion.identity,new Vector3(opticalScale,opticalScale,1));
    EnsureStyles(owner,CoordinateScale);var local=new Rect(0,0,CoordinateWidthPixels,CoordinateHeightPixels);Bracket(local,CoordinateScale);
    bool above=rect.center.y<anchor.y;float edge=above?local.yMax:local.y,tip=edge+(above?7:-7)/opticalScale;
    float stemX=Mathf.Clamp((anchor.x-rect.x)/opticalScale,4,local.xMax-4);
    Fill(new Rect(stemX,Mathf.Min(edge,tip),1/opticalScale,7/opticalScale),EdgeInk);Label(local,"X",number,"/100",CoordinateScale);
   }finally{GUI.matrix=matrix;}
  }
  internal static bool TryDraw(StudioBootstrap owner,Vector2 pos,string text,float designWidth,bool box){
   if(!box||string.IsNullOrEmpty(text))return false;
   bool coordinate=text.StartsWith("X ",StringComparison.Ordinal)&&text.EndsWith(" / 100",StringComparison.Ordinal);
   bool grid=(text.StartsWith("GRID ",StringComparison.Ordinal)||text.StartsWith("FREE ",StringComparison.Ordinal))&&text.EndsWith(" ms",StringComparison.Ordinal);
   if(!coordinate&&!grid)return false;
   float k=OverlayAnnotationLayout.Scale(owner);if(float.IsNaN(pos.x)||float.IsNaN(pos.y)||float.IsInfinity(pos.x)||float.IsInfinity(pos.y))return false;
   if(coordinate){Coordinate(owner,pos,text.Substring(2,text.Length-8).Trim(),designWidth,k);return true;}
   EnsureStyles(owner,k);Vector2 anchor;Rect preferred;string gridTitle=null,gridNumber=null;
   // Existing guide labels are offset 18/23 design pixels from the pointer.
   int start=text.IndexOf(' ')+1;gridTitle=text.Substring(0,start-1);gridNumber=text.Substring(start,text.Length-start-3).Trim();
   anchor=pos-new Vector2(18,23)*k;preferred=GridRect(owner,anchor,k,GridWidth(gridTitle,gridNumber,k));
   // Keep grid labels on a clear side of the pointer even when other labels
   // would make the generic collision layout move them back across it.
   Rect rect=preferred;
   OverlayAnnotationLayout.State(owner).Labels.Add(new OverlayAnnotationLayout.Annotation{Text=text,Rect=rect,Box=true});
   if(Event.current.type!=EventType.Repaint)return true;
   Bracket(rect,k);
   Label(rect,gridTitle,gridNumber,"ms",k,true);
   return true;
  }
 }
}
