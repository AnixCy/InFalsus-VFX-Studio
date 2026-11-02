using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  internal static bool StaticTextRuns=true;
  sealed class TextRun {internal Texture2D Texture;internal int Left,Width,Height;}
  readonly Dictionary<string,TextRun> textRuns=new Dictionary<string,TextRun>();
  readonly Dictionary<string,TextRun> timingTextRuns=new Dictionary<string,TextRun>();
  readonly Queue<string> timingTextRunOrder=new Queue<string>();bool timingStaticCellText;
  void RememberTextRun(Dictionary<string,TextRun> cache,string key,TextRun run){
   if(ReferenceEquals(cache,timingTextRuns)){
    // Scrolling/editing can expose arbitrary values. Bound their texture memory.
    if(cache.Count>=256){string oldest=timingTextRunOrder.Dequeue();TextRun previous=cache[oldest];if(previous!=null&&previous.Texture)Destroy(previous.Texture);cache.Remove(oldest);}
    timingTextRunOrder.Enqueue(key);
   }
   cache.Add(key,run);
  }
  TextRun StaticRun(string text,string face,int pixels,UiRasterGlyph[] glyphs){
   if(!StaticTextRuns||text.Length<2)return null;
   // Continuously changing counters use glyph textures, without texture churn.
   bool digits=false;foreach(char ch in text)if(char.IsDigit(ch)){digits=true;break;}
   if(digits&&!timingStaticCellText)return null;
   var cache=digits?timingTextRuns:textRuns;
   string key=face+"\0"+pixels+"\0"+text;TextRun run;if(cache.TryGetValue(key,out run))return run;
   if(!digits&&cache.Count>=256)return null;
   int advance=0,left=0,right=0,height=0;
   foreach(var g in glyphs){if(g.Ink){left=Math.Min(left,advance-g.Padding);right=Math.Max(right,advance-g.Padding+g.Width);height=Math.Max(height,g.Height);}advance+=g.Advance;}
   int width=right-left;if(width<=0||width>4096||height==0)return null;
   var bytes=new byte[width*height*4];for(int i=0;i<bytes.Length;i+=4)bytes[i]=bytes[i+1]=bytes[i+2]=255;
   advance=0;
   foreach(var g in glyphs){if(g.Ink)for(int y=0;y<g.Height;y++)for(int x=0;x<g.Width;x++){
    byte alpha=g.Pixels[(y*g.Width+x)*4+3];if(alpha==0)continue;int index=((height-g.Height+y)*width+advance-g.Padding-left+x)*4+3;
    // Overlapping ink must keep its original separate alpha blending.
    if(bytes[index]!=0){RememberTextRun(cache,key,null);return null;}bytes[index]=alpha;
   }advance+=g.Advance;}
   var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);texture.LoadRawTextureData(bytes);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Point;texture.hideFlags=HideFlags.HideAndDontSave;
   run=new TextRun{Texture=texture,Left=left,Width=width,Height=height};RememberTextRun(cache,key,run);return run;
  }
  bool PaintStaticRun(Rect rect,string text,string face,int pixels,UiRasterGlyph[] glyphs,float x,float y,Rect? clip){
   var run=StaticRun(text,face,pixels,glyphs);if(run==null)return false;
   var quad=new Rect(x+run.Left,y,run.Width,run.Height);
   if(!clip.HasValue){GUI.DrawTexture(quad,run.Texture);return true;}
   Rect c=clip.Value;float left=Mathf.Max(quad.x,c.x),right=Mathf.Min(quad.xMax,c.xMax),top=Mathf.Max(quad.y,c.y),bottom=Mathf.Min(quad.yMax,c.yMax);
   if(right>left&&bottom>top)GUI.DrawTextureWithTexCoords(new Rect(left,top,right-left,bottom-top),run.Texture,new Rect((left-quad.x)/quad.width,1-(bottom-quad.y)/quad.height,(right-left)/quad.width,(bottom-top)/quad.height));
   return true;
  }
  void DisposeTextRuns(){foreach(var cache in new[]{textRuns,timingTextRuns}){foreach(var run in cache.Values)if(run!=null&&run.Texture)Destroy(run.Texture);cache.Clear();}timingTextRunOrder.Clear();}
 }
}
