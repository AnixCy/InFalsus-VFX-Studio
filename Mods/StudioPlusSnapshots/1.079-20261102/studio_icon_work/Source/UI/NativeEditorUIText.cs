using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  readonly Dictionary<UiRasterGlyph,Texture2D> uiTextures=new Dictionary<UiRasterGlyph,Texture2D>();
  Type nativePresentation;MethodInfo nativeRect;float hudRowRight,hudRowBaseline;int hudRowPixels;
  int FontPixels(float design){return UiFontRaster.Pixels(design,scale);}
  void ApplyReadableStyles(){
   foreach(var s in new[]{skin.label,skin.button,skin.textField,skin.textArea,skin.toggle,skin.box})s.fontSize=FontPixels(17);
   label.fontSize=FontPixels(17);caption.fontSize=FontPixels(13);caption.normal.textColor=Css("#a6c3d1");heading.fontSize=FontPixels(24);button.fontSize=FontPixels(16);selected.fontSize=button.fontSize;number.fontSize=FontPixels(26);
   FixInputStates();
  }
  Texture2D GlyphTexture(UiRasterGlyph glyph){
   Texture2D texture;if(uiTextures.TryGetValue(glyph,out texture))return texture;
   texture=new Texture2D(glyph.Width,glyph.Height,TextureFormat.RGBA32,false);texture.LoadRawTextureData(glyph.Pixels);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Point;texture.hideFlags=HideFlags.HideAndDontSave;uiTextures.Add(glyph,texture);return texture;
  }
  Vector2 PixelOrigin(float x,float y){var p=GUIUtility.GUIToScreenPoint(new Vector2(x,y));return GUIUtility.ScreenToGUIPoint(new Vector2(Mathf.Round(p.x),Mathf.Round(p.y)));}
  float FooterValueBottom(){
   var ink="100".Select(ch=>UiFontRaster.Glyph(NumFace,ch,FontPixels(26))).Where(g=>g.Ink).ToArray();
   int top=ink.Min(g=>g.Top),bottom=ink.Max(g=>g.Bottom);float center=footerRect.y+(38.59375f+17.5f)*scale;
   return PixelOrigin(0,center-(bottom-top)*.5f-top).y+bottom;
  }
  float CompactFooterOffset(){return 8*scale;}
  float FooterAnnotationCenter(){var glyphs="7/7".Select(ch=>UiFontRaster.Glyph(NumFace,ch,FontPixels(11))).Where(g=>g.Ink).ToArray();return FooterValueBottom()-(glyphs.Max(g=>g.Bottom)-glyphs.Min(g=>g.Top))*.5f;}
  void RasterLine(Rect rect,string text,string face,int pixels,Color color,int align=0,int vertical=0,float baseline=0,Rect? clip=null){
   if(Event.current.type!=EventType.Repaint||string.IsNullOrEmpty(text))return;
   var layout=UiFontRaster.Layout(text,face,pixels);var glyphs=layout.Glyphs;int width=layout.Width,top=layout.Top,bottom=layout.Bottom;
   float x=rect.x+(align==1?(rect.width-width)*.5f:align==2?rect.width-width:0);
   if(vertical==1&&align==0&&glyphs.Length>0)x-=glyphs[0].Left-glyphs[0].Padding;
   int fontBaseline=layout.Baseline;
   float y=vertical==1?rect.y-top:vertical==2?baseline-fontBaseline:vertical==3?baseline-bottom:rect.center.y-(bottom-top)*.5f-top;
   var origin=PixelOrigin(x,y);x=origin.x;y=origin.y;var old=GUI.color;GUI.color=old*color;
   // Painting must never allocate control IDs, even when clipping is required.
   try{if(PaintStaticRun(rect,text,face,pixels,glyphs,x,y,clip))return;foreach(var glyph in glyphs){if(glyph.Ink){Rect quad=new Rect(x-glyph.Padding,y,glyph.Width,glyph.Height);
    if(clip.HasValue){Rect c=clip.Value;float left=Mathf.Max(quad.x,c.x),right=Mathf.Min(quad.xMax,c.xMax),topEdge=Mathf.Max(quad.y,c.y),bottomEdge=Mathf.Min(quad.yMax,c.yMax);
     if(right>left&&bottomEdge>topEdge)GUI.DrawTextureWithTexCoords(new Rect(left,topEdge,right-left,bottomEdge-topEdge),GlyphTexture(glyph),new Rect((left-quad.x)/quad.width,1-(bottomEdge-quad.y)/quad.height,(right-left)/quad.width,(bottomEdge-topEdge)/quad.height));
    }else GUI.DrawTexture(quad,GlyphTexture(glyph));}x+=glyph.Advance;}}finally{GUI.color=old;}
  }
  void RasterText(Rect rect,string text,float design,Color color,string face,int align){
   if(Event.current.type!=EventType.Repaint||string.IsNullOrEmpty(text))return;text=T(text);int pixels=FontPixels(design);
   var lines=UiFontRaster.Wrap(text,face,pixels,rect.width);float lineHeight=pixels+3;
   if(lines.Length==1)RasterLine(rect,lines[0],face,pixels,color,align,clip:rect);else{float top=Mathf.Max(0,(rect.height-lines.Length*lineHeight)*.5f);for(int i=0;i<lines.Length;i++)RasterLine(new Rect(rect.x,rect.y+top+i*lineHeight,rect.width,lineHeight),lines[i],face,pixels,color,align,clip:rect);}
  }
  void InitTypography(){
   nativePresentation=typeof(InFalsusStudio.NativeVisual.NativePresentation);
   nativeRect=nativePresentation.GetMethod("R",F);
   foreach(string name in new[]{"InkText","RowText","ProgressText"}){
    var method=nativePresentation.GetMethod(name,F);if(method!=null)harmony.Patch(method,new HarmonyMethod(typeof(TypographyHooks).GetMethod(name,F)){priority=Priority.Last});
   }
   var hud=nativePresentation.GetMethod("DrawHud",F);if(hud!=null)harmony.Patch(hud,new HarmonyMethod(typeof(TypographyHooks).GetMethod("BeforeHud",F)),finalizer:new HarmonyMethod(typeof(TypographyHooks).GetMethod("AfterHud",F)));
  }
  Rect NativeSlot(string name){var layout=nativePresentation.GetField("L",F).GetValue(null);var slot=(float[])layout.GetType().GetField(name,F).GetValue(layout);return (Rect)nativeRect.Invoke(null,new object[]{boot,slot});}
  void DisposeTypography(){DisposeTextRuns();DisposeTimingBorders();foreach(var texture in uiTextures.Values)if(texture)Destroy(texture);uiTextures.Clear();UiFontRaster.Clear();}
  internal static class TypographyHooks {
   static bool Enabled{get{return I!=null&&I.ready&&I.boot!=null&&Event.current!=null;}}
   static bool InkText(Rect __0,string __1,string __2,float __3,Color __4,bool __5){
    if(!Enabled)return true;if(__1=="未打开谱面"&&!I.boot.HasDocument)__1=I.T(__1);int pixels=Mathf.Max(13,Mathf.RoundToInt(__3));Rect artist=I.NativeSlot("artist");if(Mathf.Abs(__0.x-artist.x)<.25f&&Mathf.Abs(__0.y-artist.y)<.25f)__0.x=I.NativeSlot("title").x;
    string text=UiFontRaster.Ellipsize(__1??"",__2,pixels,__0.width);I.RasterLine(__0,text,__2,pixels,__4,__5?1:0,1);return false;
   }
   static bool RowText(Rect __0,string __1,string __2,float __3,Color __4,float __5,bool __6){
    if(!Enabled)return true;Rect code=I.NativeSlot("difficultyText");
    bool first=Mathf.Abs(code.x-__0.x)<.25f&&Mathf.Abs(code.y-__0.y)<.25f;
    if(first){
     // difficulty is the whole texture, including its tall decorative backing.
     // difficultyText identifies the actual bottom colour strip containing the label.
     Rect badge=code;I.hudRowPixels=Mathf.Max(10,Mathf.Min(Mathf.Max(12,Mathf.RoundToInt(__3)),Mathf.FloorToInt(badge.height-3)));
     var reference="FBD13SPEED1.00".Select(ch=>UiFontRaster.Glyph(__2,ch,I.hudRowPixels)).Where(g=>g.Ink).ToArray();
     I.hudRowBaseline=badge.center.y-(reference.Min(g=>g.Top)+reference.Max(g=>g.Bottom))*.5f+reference[0].Baseline;
     I.hudRowRight=__0.x+UiFontRaster.Measure(__1,__2,I.hudRowPixels);
    }
    int pixels=I.hudRowPixels>0?I.hudRowPixels:12;int width=UiFontRaster.Measure(__1,__2,pixels);
    if(!first){__0.x=Mathf.Max(__0.x,I.hudRowRight+(__1=="SPEED"?8:5));__0.width=Mathf.Max(__0.width,width);I.hudRowRight=__0.x+__0.width;}
    if(__1=="SPEED")__4=new Color(.72f,.82f,.86f,__4.a);
    I.RasterLine(__0,__1,__2,pixels,__4,__6?1:0,2,I.hudRowBaseline);return false;
   }
   // NativeVisual still owns the progress bar and its seeking/contrast outline.
   static bool ProgressText(Rect __0,string __1,float __2,Color __3){if(!Enabled)return true;I.RasterLine(__0,__1,NumFace,Mathf.Max(13,Mathf.RoundToInt(__2)),__3,1);return false;}
   static void BeforeHud(object __0,out Texture2D __state){__state=null;if(Enabled&&Event.current.type==EventType.Repaint){var source=Get(__0,"songJacket") as Texture2D;var filtered=I.FilterJacket(source);if(filtered!=source){__state=source;Set(__0,"songJacket",filtered);}}}
   static void AfterHud(object __0,Texture2D __state){if(__state!=null)Set(__0,"songJacket",__state);}
  }
 }
}
