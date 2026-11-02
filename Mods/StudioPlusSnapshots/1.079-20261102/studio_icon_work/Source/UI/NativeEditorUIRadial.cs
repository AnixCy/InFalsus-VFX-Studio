using System;
using System.Collections;
using System.Collections.Generic;
using InFalsusStudio;
using InFalsusStudio.Core;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  Texture2D radialRing;int radialRingRadius;
  bool radialGeometryValid,radialGeometryGrid;int radialGeometryTap,radialGeometryHold;float radialGeometryScale;RadialLevel radialGeometryLevel;Vector2 radialGeometryCenter;Rect radialGeometryView;
  readonly Dictionary<string,Texture2D> radialPlates=new Dictionary<string,Texture2D>();
  void DrawStyledRadial(){
   var radial=Val<RadialMenuState>(boot,"radial");if(!radial.Pressed||Event.current.type!=EventType.Repaint)return;
   float previousScale=scale;Color previousColor=GUI.color;
   try{
    scale=Convert.ToSingle(Prop(boot,"OverlayScale"));Vector2 center=Val<Vector2>(boot,"radialCenter");float radius=(float)OverlayMetrics.RadialRadius*scale;
    RadialItem hovered=(RadialItem)Call(boot,"HitRadial",Val<Vector2>(boot,"radialPointer"));
    // Paint the original-size Rects, including the Tap/Hold spacing adjustment.
    DrawRadialRing(center,radius);
    if(!DrawRadialLocator()){RadialPlate(new Rect(center.x-58*scale,center.y-30*scale,116*scale,60*scale),false,false);UiText(new Rect(center.x-55*scale,center.y-27*scale,110*scale,54*scale),"松开右键\n空白处取消",14,Css("#b5d2db"),CNFace,1);}
    foreach(object entry in (IEnumerable)Get(boot,"radialButtons")){
     Type type=entry.GetType();Rect rect=(Rect)type.GetField("Rect",F).GetValue(entry);var item=(RadialItem)type.GetField("Item",F).GetValue(entry);bool hover=item==hovered;
     RadialPlate(rect,hover,item==RadialItem.Delete);
     string key=RadialIcon(item),text=RadialCaption(radial.Level,item);float icon=20*scale,gap=9*scale;int pixels=FontPixels(16);
     string display=T(text);float textWidth=UiFontRaster.Wrap(display,CNFace,pixels,Mathf.Max(1,rect.width-42*scale)).Length>1?rect.width-42*scale:UiFontRaster.Measure(display,CNFace,pixels);
     float group=Mathf.Min(rect.width-16*scale,icon+gap+textWidth),left=rect.center.x-group*.5f;
     Color ink=hover?Css("#a7f5fa"):Css("#c7dee7");
     if(key!=null){GUI.color=previousColor*(item==RadialItem.Delete?Css("#d58b97"):ink);GUI.DrawTexture(new Rect(left,rect.center.y-icon*.5f,icon,icon),Icon(key));GUI.color=previousColor;left+=icon+gap;}
     else left=rect.x+8*scale;
     UiText(new Rect(left,rect.y+3*scale,rect.xMax-8*scale-left,rect.height-6*scale),text,16,ink,CNFace,key==null?1:0);
     if(hover){float progress=(float)radial.HoverProgress(Time.realtimeSinceStartupAsDouble);if(progress>0)Fill(new Rect(rect.x+4*scale,rect.yMax-4*scale,(rect.width-8*scale)*progress,2*scale),Css("#89e8e8"));}
    }
    string title=radial.Level==RadialLevel.Selection?"选区操作 · 在按钮上松开右键":radial.Level==RadialLevel.Home?"快捷菜单 · 在按钮上松开右键":radial.Level==RadialLevel.Point?"点立得 · 停留 0.5 秒展开，松开选择":radial.Level==RadialLevel.FlickDirection?"Flick 方向 · 松开选择":"默认宽度 · 仅中央四轨生效";
    float headerWidth=Mathf.Min(530*scale,UiFontRaster.Measure(T(title),CNFace,FontPixels(14))+24*scale);Rect header=new Rect(center.x-headerWidth*.5f,center.y-radius-82*scale,headerWidth,34*scale);RadialPlate(header,false,false);UiText(header,title,14,Css("#b5d2db"),CNFace,1);
    DrawRadialPointer();
   }finally{scale=previousScale;GUI.color=previousColor;}
  }
  void RadialPlate(Rect rect,bool hovered,bool destructive){
   int width=Mathf.Max(1,Mathf.CeilToInt(rect.width)),height=Mathf.Max(1,Mathf.CeilToInt(rect.height));string key=width+"x"+height+":"+Mathf.RoundToInt(scale*1000)+":"+hovered;Texture2D texture;
   if(!radialPlates.TryGetValue(key,out texture)){
    if(radialPlates.Count>64){foreach(var old in radialPlates.Values)if(old)Destroy(old);radialPlates.Clear();}
    Color fill=hovered?new Color(.055f,.19f,.24f,.97f):new Color(.035f,.105f,.15f,.91f),edge=hovered?Css("#7edfe7"):new Color(.33f,.53f,.61f,.73f);
    float corner=Mathf.Min(7*scale,height*.2f),thickness=hovered?Mathf.Max(1,1.5f*scale):Mathf.Max(1,scale);
    Vector2[] points={new Vector2(corner,0),new Vector2(width,0),new Vector2(width,height-corner),new Vector2(width-corner,height),new Vector2(0,height),new Vector2(0,corner)};
    var pixels=new Color32[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++){
     Vector2 point=new Vector2(x+.5f,y+.5f);float inside=float.PositiveInfinity;
     for(int i=0;i<points.Length;i++){var start=points[i];var delta=points[(i+1)%points.Length]-start;var relative=point-start;inside=Mathf.Min(inside,(delta.x*relative.y-delta.y*relative.x)/delta.magnitude);}
     Color color=Color.Lerp(fill,edge,Mathf.Clamp01(thickness-inside+.5f));color.a*=Mathf.Clamp01(inside+.5f);pixels[(height-1-y)*width+x]=color;
    }
    texture=new Texture2D(width,height,TextureFormat.RGBA32,false);texture.name="Quick Place cached plate";texture.hideFlags=HideFlags.HideAndDontSave;texture.SetPixels32(pixels);texture.Apply(false,true);texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;radialPlates.Add(key,texture);
   }
   GUI.DrawTexture(rect,texture);
  }
  void DrawRadialRing(Vector2 center,float radius){
   int extent=Mathf.CeilToInt(radius+Mathf.Max(1,scale)+2);
   if(!radialRing||radialRingRadius!=extent){
    if(radialRing)Destroy(radialRing);radialRingRadius=extent;int size=extent*2;var pixels=new Color32[size*size];float width=Mathf.Max(1,scale);
    for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=x+.5f-extent,dy=y+.5f-extent;float alpha=Mathf.Clamp01(width*.5f+.5f-Mathf.Abs(Mathf.Sqrt(dx*dx+dy*dy)-radius));pixels[y*size+x]=new Color(.35f,.68f,.75f,.28f*alpha);}
    radialRing=new Texture2D(size,size,TextureFormat.RGBA32,false);radialRing.SetPixels32(pixels);radialRing.Apply(false,true);radialRing.hideFlags=HideFlags.HideAndDontSave;radialRing.filterMode=FilterMode.Bilinear;
   }
   GUI.DrawTexture(new Rect(center.x-extent,center.y-extent,extent*2,extent*2),radialRing);
  }
  void DisposeRadialPlates(){DisposePointerStrokes();if(radialRing)Destroy(radialRing);radialRing=null;radialGeometryValid=false;foreach(var texture in radialPlates.Values)if(texture)Destroy(texture);radialPlates.Clear();if(radialPointerDot)Destroy(radialPointerDot);radialPointerDot=null;}
  string RadialCaption(RadialLevel level,RadialItem item){
   switch(item){
    case RadialItem.EnterPoint:return "点立得";case RadialItem.Mirror:return "镜像";case RadialItem.Cut:return "剪贴";case RadialItem.Copy:return "复制";case RadialItem.Delete:return "删除";case RadialItem.Align:return "对齐网格线";
    case RadialItem.Grid:return level==RadialLevel.Selection?"网格线":Val<bool>(boot,"showGrid")?"关闭网格线":"开启网格线";
    case RadialItem.Tap:return "Tap ["+Get(boot,"tapWidth")+"]";case RadialItem.Hold:return "Hold ["+Get(boot,"holdWidth")+"]";case RadialItem.Sky:return "SkyArea";case RadialItem.Flick:return "Flick";
    case RadialItem.NoCreate:return "不创建";case RadialItem.Exit:return "退出";case RadialItem.Left:return "向左 / 黄色";case RadialItem.Right:return "向右 / 绿色";
    default:return ((int)item-(int)RadialItem.Width1+1)+" 轨宽";
   }
  }
  static string RadialIcon(RadialItem item){switch(item){case RadialItem.EnterPoint:return "radial-point";case RadialItem.Grid:case RadialItem.Align:return "grid";case RadialItem.Mirror:return "mirror";case RadialItem.Copy:return "copy";case RadialItem.Cut:return "cut";case RadialItem.Delete:return "delete";case RadialItem.Exit:return "exit";case RadialItem.Sky:return "air";default:return null;}}
  internal static partial class Hooks {
   static bool BuildRadial(StudioBootstrap __instance){
    if(!Active(__instance))return true;var camera=__instance.ViewCamera;Rect view=camera.pixelRect;view.y=Screen.height-view.yMax;
    return !I.radialGeometryValid||I.radialGeometryView!=view||I.radialGeometryCenter!=Val<Vector2>(__instance,"radialCenter")||I.radialGeometryLevel!=Val<RadialMenuState>(__instance,"radial").Level||I.radialGeometryScale!=Convert.ToSingle(Prop(__instance,"OverlayScale"))||I.radialGeometryGrid!=Val<bool>(__instance,"showGrid")||I.radialGeometryTap!=Val<int>(__instance,"tapWidth")||I.radialGeometryHold!=Val<int>(__instance,"holdWidth")||((IList)Get(__instance,"radialButtons")).Count==0;
   }
   static void BuiltRadial(StudioBootstrap __instance){
    if(!Active(__instance))return;var camera=__instance.ViewCamera;I.radialGeometryView=camera.pixelRect;I.radialGeometryView.y=Screen.height-I.radialGeometryView.yMax;I.radialGeometryCenter=Val<Vector2>(__instance,"radialCenter");I.radialGeometryLevel=Val<RadialMenuState>(__instance,"radial").Level;I.radialGeometryScale=Convert.ToSingle(Prop(__instance,"OverlayScale"));I.radialGeometryGrid=Val<bool>(__instance,"showGrid");I.radialGeometryTap=Val<int>(__instance,"tapWidth");I.radialGeometryHold=Val<int>(__instance,"holdWidth");I.radialGeometryValid=true;
   }
   static readonly System.Reflection.FieldInfo radialButtonRect=typeof(StudioBootstrap).GetNestedType("RadialButton",F).GetField("Rect",F);
   static readonly System.Reflection.FieldInfo radialButtonItem=typeof(StudioBootstrap).GetNestedType("RadialButton",F).GetField("Item",F);
   static void CompactRadialButton(StudioBootstrap __instance){
    if(!Active(__instance))return;var level=Val<RadialMenuState>(__instance,"radial").Level;var buttons=(IList)Get(__instance,"radialButtons");if(buttons.Count==0)return;
    int index=buttons.Count-1;object button=buttons[index];var item=(RadialItem)radialButtonItem.GetValue(button);float offset=0;
    if(level==RadialLevel.Point&&(item==RadialItem.Tap||item==RadialItem.Hold))offset=item==RadialItem.Tap?-8:8;
    else if(level==RadialLevel.Selection&&(item==RadialItem.Delete||item==RadialItem.Copy))offset=item==RadialItem.Delete?-8:8;
    else return;
    Rect rect=(Rect)radialButtonRect.GetValue(button);float k=Convert.ToSingle(Prop(__instance,"OverlayScale"));rect.x+=offset*k;
    radialButtonRect.SetValue(button,rect);buttons[index]=button;
   }
   static bool StyledRadial(StudioBootstrap __instance){if(!Active(__instance)||I.formal)return true;I.DrawStyledRadial();return false;}
  }
 }
}
