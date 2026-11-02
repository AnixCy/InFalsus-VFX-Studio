using System;
using System.Collections.Generic;
namespace InFalsusStudio.NativeEditorUI {
 internal sealed class UiLineLayout {
  internal UiRasterGlyph[] Glyphs;internal int[] Advances;internal int Width,Top,Bottom,Baseline;internal bool Ink;
 }
 internal static partial class UiFontRaster {
  struct LayoutKey:IEquatable<LayoutKey>{
   internal string Text,Face;internal int Pixels;internal float Width;
   public bool Equals(LayoutKey b){return Text==b.Text&&Face==b.Face&&Pixels==b.Pixels&&Width.Equals(b.Width);}
   public override bool Equals(object b){return b is LayoutKey&&Equals((LayoutKey)b);}
   public override int GetHashCode(){return Text.GetHashCode()^Face.GetHashCode()^Pixels^Width.GetHashCode();}
  }
  static readonly BoundedLruCache<LayoutKey,UiLineLayout> layouts=new BoundedLruCache<LayoutKey,UiLineLayout>(512);
  static readonly BoundedLruCache<LayoutKey,string[]> wraps=new BoundedLruCache<LayoutKey,string[]>(512);
  internal static UiLineLayout Layout(string text,string face,int pixels){
   var key=new LayoutKey{Text=text,Face=face,Pixels=pixels};UiLineLayout result;
   if(layouts.TryGetValue(key,out result))return result;
   result=new UiLineLayout{Glyphs=new UiRasterGlyph[text.Length],Advances=new int[text.Length+1]};
   for(int i=0;i<text.Length;i++){
    var glyph=Glyph(face,text[i],pixels);result.Glyphs[i]=glyph;result.Advances[i+1]=result.Advances[i]+glyph.Advance;
    result.Baseline=Math.Max(result.Baseline,glyph.Baseline);
    if(glyph.Ink){result.Top=result.Ink?Math.Min(result.Top,glyph.Top):glyph.Top;result.Bottom=Math.Max(result.Bottom,glyph.Bottom);result.Ink=true;}
   }
   result.Width=result.Advances[text.Length];layouts[key]=result;return result;
  }
  internal static string[] Wrap(string text,string face,int pixels,float width){
   var key=new LayoutKey{Text=text,Face=face,Pixels=pixels,Width=width};string[] result;
   if(wraps.TryGetValue(key,out result))return result;
   result=WrapUncached(text,face,pixels,width);wraps[key]=result;return result;
  }
  static void ClearLayouts(){layouts.Clear();wraps.Clear();}
 }
}
