using System;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
 internal enum ProgressBarStyle { External, Embedded }
 public sealed partial class NativeVisualPlugin {
  internal static ConfigEntry<ProgressBarStyle> ProgressStyle;
  void InitProgressStyle(){
   ProgressStyle=Config.Bind("Display","ProgressBarStyle",ProgressBarStyle.External,"External: existing wide bar below the song card. Embedded: continuous native thin bar touching the difficulty plate.");
   if(ProgressStyle.Value!=ProgressBarStyle.External&&ProgressStyle.Value!=ProgressBarStyle.Embedded)ProgressStyle.Value=ProgressBarStyle.External;
  }
 }
 // Pure geometry is shared by painting, seeking and input blocking.
 internal static class ProgressGeometry {
  internal static float Pixel(float value){return (float)Math.Round(value);}
  internal static Rect Embedded(Rect jacketFrame,Rect difficultyStrip,Rect nativeLine,float scale){
   float left=jacketFrame.xMax,right=nativeLine.xMax,bottom=difficultyStrip.y+.5f,height=Math.Max(1,2.5f*scale);
   return new Rect(left,bottom-height,Math.Max(0,right-left),height);
  }
  internal static Rect Hit(Rect line,float artistBottom,float scale){
   float top=Math.Max(artistBottom+1,line.yMax-Math.Max(10,12*scale));
   top=Math.Min(top,line.y);
   return new Rect(line.x,top,line.width,Math.Max(line.height,line.yMax-top));
  }
  internal static double TimeAt(float x,Rect line,double duration){
   double ratio=Math.Max(0,Math.Min(1,(x-line.x)/Math.Max(1,line.width)));
   return ratio*Math.Max(1,duration);
  }
  internal static float Ratio(double now,double duration){return (float)Math.Max(0,Math.Min(1,now/Math.Max(1,duration)));}
  internal static Rect Fill(Rect line,double now,double duration){return new Rect(line.x,line.y,line.width*Ratio(now,duration),line.height);}
  internal static Rect Point(Rect line,double now,double duration,float scale){float size=29.5f*scale;return new Rect(line.x+line.width*Ratio(now,duration)-size*.5f,line.center.y-size*.5f,size,size);}
 }
 internal static class NativeProgress {
  // Original Constrained2D offsets (-950,-167) to (-253,-167), mapped /2.
  static readonly float[] nativeSlot={1445,83.5f,348.5f,2.5f};
  internal static bool Embedded{get{return NativeVisualPlugin.ProgressStyle!=null&&NativeVisualPlugin.ProgressStyle.Value==ProgressBarStyle.Embedded;}}
  internal static Rect Line(object boot){
   float scale=NativeVisualPlugin.N(boot,"hudScale");
   // All four native difficulty textures start the horizontal plate at row 105/128.
   Rect plate=NativePresentation.R(boot,NativePresentation.L.difficulty);
   Rect strip=new Rect(plate.x,plate.y+plate.height*(105f/128f),plate.width,0);
   return ProgressGeometry.Embedded(NativePresentation.R(boot,NativePresentation.L.jacketFrame),strip,NativePresentation.R(boot,nativeSlot),scale);
  }
  internal static Rect Hit(object boot){
   if(!Embedded)return NativePresentation.R(boot,NativePresentation.L.progress);
   if(!(bool)NativeVisualPlugin.Get(boot,"guiShowSongHud"))return Rect.zero;
   Rect artist=NativePresentation.R(boot,NativePresentation.L.artist);float scale=NativeVisualPlugin.N(boot,"hudScale");
   // The readable UI keeps small artist text at >=13px. Do not steal its input.
   float artistBottom=artist.y+Math.Max(13,12.8f*scale);
   return ProgressGeometry.Hit(Line(boot),artistBottom,scale);
  }
  internal static void SetStyle(object boot,ProgressBarStyle style){
   if(NativeVisualPlugin.ProgressStyle.Value==style)return;
   if(boot!=null){int control=(int)NativeVisualPlugin.Get(boot,"progressControlId");NativeVisualPlugin.Set(boot,"progressDragging",false);if(GUIUtility.hotControl==control)GUIUtility.hotControl=0;}
   NativeVisualPlugin.ProgressStyle.Value=style;
   if(boot!=null)NativeVisualPlugin.Set(boot,"progressRect",Hit(boot));
  }
  internal static void Draw(object boot){
   Rect hit=Hit(boot);if(hit.width<=0||hit.height<=0)return;
   Rect line=Line(boot);float scale=NativeVisualPlugin.N(boot,"hudScale");double length=Math.Max(1,Convert.ToDouble(NativeVisualPlugin.Get(boot,"durationMs"))),now=NativeVisualPlugin.P(boot,"CurrentMs");
   NativePresentation.Fill(line,new Color(72/255f,96/255f,107/255f));
   NativePresentation.Fill(ProgressGeometry.Fill(line,now,length),new Color(.96f,.98f,1));
   Texture2D point;if(NativePresentation.Images.TryGetValue("song-progress-point",out point))GUI.DrawTexture(ProgressGeometry.Point(line,now,length,scale),point,ScaleMode.StretchToFill,true);
   bool dragging=(bool)NativeVisualPlugin.Get(boot,"progressDragging");
   if(!InFalsusStudio.Gameplay.GameplaySession.Active&&(bool)NativePresentation.Value(boot,"HasDocument")&&(dragging||(NativeVisualPlugin.Get(boot,"hudField").ToString()=="None"&&hit.Contains(Event.current.mousePosition))))DrawTime(boot,line,now,length,scale,dragging);
  }
  internal static void DrawExport(object boot){
   Rect line=Line(boot);float scale=NativeVisualPlugin.N(boot,"hudScale");double length=Math.Max(1,Convert.ToDouble(NativeVisualPlugin.Get(boot,"durationMs"))),now=InFalsusStudio.VideoRender.RenderClock.NowMs;
   NativePresentation.Fill(line,new Color(72/255f,96/255f,107/255f));NativePresentation.Fill(ProgressGeometry.Fill(line,now,length),new Color(.96f,.98f,1));
   Texture2D point;if(NativePresentation.Images.TryGetValue("song-progress-point",out point))InFalsusStudio.VideoRender.CaptureCanvas.Draw(ProgressGeometry.Point(line,now,length,scale),point,Color.white);
  }
  static void DrawTime(object boot,Rect line,double now,double duration,float scale,bool dragging){
   double target=now;
   if(!dragging)target=PreviewTime(boot,ProgressGeometry.TimeAt(Event.current.mousePosition.x,line,duration));
   string text=target.ToString("0",CultureInfo.InvariantCulture)+" / "+duration.ToString("0",CultureInfo.InvariantCulture)+" ms";
   float size=Math.Max(13,14*scale),width=Math.Min(Screen.width-16,text.Sum(c=>GdiText.Glyph("Bai Jamjuree Medium",c,NativePresentation.L.fallbackFaces).Advance)*size/72f+28),height=Math.Max(26,30*scale);
   float x=Math.Max(8,Math.Min(Screen.width-width-8,Event.current.mousePosition.x-width*.5f)),y=Math.Max(4,line.y-height-7);
   Rect box=new Rect(ProgressGeometry.Pixel(x),ProgressGeometry.Pixel(y),ProgressGeometry.Pixel(width),ProgressGeometry.Pixel(height));
   NativePresentation.Fill(box,new Color(.035f,.10f,.13f,.98f));NativePresentation.Fill(new Rect(box.x,box.yMax-1,box.width,1),new Color(.45f,.72f,.77f));
   NativePresentation.ProgressText(box,text,size,new Color(.87f,.96f,.98f));
  }
  internal static double PreviewTime(object boot,double target){return (bool)NativeVisualPlugin.Get(boot,"showGrid")?Convert.ToDouble(NativeVisualPlugin.Call(boot,"SnapTime",target,false)):InFalsusStudio.Core.AuthoringGrid.RoundMs(target);}
 }
}
