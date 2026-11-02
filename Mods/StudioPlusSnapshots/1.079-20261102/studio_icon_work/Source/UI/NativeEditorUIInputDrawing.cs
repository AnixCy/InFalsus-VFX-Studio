using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  readonly Dictionary<string,float> inputScroll=new Dictionary<string,float>();
  readonly Dictionary<string,int> inputDrag=new Dictionary<string,int>();
  float InputTextX(Rect content,int width,int align,float scroll){return content.x+(align==2&&width<content.width?content.width-width:0)-scroll;}
  int InputHitIndex(Rect rect,string text,string face,int pixels,int align,int padding,string key,float mouseX){
   text=text??"";float scroll;inputScroll.TryGetValue(key,out scroll);var content=new Rect(rect.x+padding,rect.y,rect.width-padding*2,rect.height);
   float x=InputTextX(content,UiFontRaster.Measure(text,face,pixels),align,scroll);
   for(int i=0;i<text.Length;i++){int width=UiFontRaster.Glyph(face,text[i],pixels).Advance;if(mouseX<x+width*.5f)return i;x+=width;}return text.Length;
  }
  void InputFill(Rect rect,Rect clip,Color color){float left=Mathf.Max(rect.x,clip.x),right=Mathf.Min(rect.xMax,clip.xMax),top=Mathf.Max(rect.y,clip.y),bottom=Mathf.Min(rect.yMax,clip.yMax);if(right>left&&bottom>top)Fill(Rect.MinMaxRect(left,top,right,bottom),color);}
  void PaintInput(Rect rect,string key,string text,string face,int pixels,int align,int padding,bool focused,TextEditor editor,Color ink,float? anchorTop=null){
   if(Event.current.type!=EventType.Repaint)return;text=text??"";
   var content=new Rect(rect.x+padding,rect.y,Mathf.Max(1,rect.width-padding*2),rect.height);
   int cursor=focused&&editor!=null?Mathf.Clamp(editor.cursorIndex,0,text.Length):0,selected=focused&&editor!=null?Mathf.Clamp(editor.selectIndex,0,text.Length):0;
   string composition=focused?Input.compositionString:"";if(!string.IsNullOrEmpty(composition)){text=text.Insert(cursor,composition);cursor+=composition.Length;selected=cursor;}
   var layout=UiFontRaster.Layout(text,face,pixels);var advances=layout.Advances;int width=layout.Width,top=layout.Top,bottom=layout.Ink?layout.Bottom:pixels;
   float scroll=0;if(focused){inputScroll.TryGetValue(key,out scroll);if(advances[cursor]-scroll>content.width-2)scroll=advances[cursor]-content.width+2;if(advances[cursor]-scroll<0)scroll=advances[cursor];scroll=Mathf.Max(0,scroll);}inputScroll[key]=scroll;
   float x=InputTextX(content,width,align,scroll);var origin=PixelOrigin(x,anchorTop.HasValue?anchorTop.Value-top:rect.center.y-(bottom-top)*.5f-top);float inkTop=origin.y+top,inkHeight=Mathf.Max(1,bottom-top);
   if(focused&&cursor!=selected){int start=Mathf.Min(cursor,selected),end=Mathf.Max(cursor,selected);InputFill(new Rect(origin.x+advances[start],inkTop-2,advances[end]-advances[start],inkHeight+4),content,Css("#24586b"));}
   // All states use the same already-supported raster drawing. Native IMGUI owns
   // keyboard editing and focus only; no runtime-created Font is involved.
   RasterLine(new Rect(origin.x,anchorTop.HasValue?origin.y+top:rect.y,Mathf.Max(content.width,width),anchorTop.HasValue?inkHeight:rect.height),text,face,pixels,ink,clip:content);
   if(focused){float caret=Mathf.Clamp(origin.x+advances[cursor],content.x,content.xMax-1);if(((int)(Time.realtimeSinceStartup*2)&1)==0||!string.IsNullOrEmpty(composition))InputFill(new Rect(caret,inkTop-2,1,inkHeight+4),content,Css("#d8faff"));
    if(!string.IsNullOrEmpty(composition))InputFill(new Rect(origin.x+advances[cursor-composition.Length],inkTop+inkHeight+1,advances[cursor]-advances[cursor-composition.Length],1),content,Css("#d8faff"));
    Input.compositionCursorPos=GUIUtility.GUIToScreenPoint(new Vector2(caret,inkTop+inkHeight+2));}
  }
  void DisposeInputFonts(){inputScroll.Clear();inputDrag.Clear();}
 }
}
