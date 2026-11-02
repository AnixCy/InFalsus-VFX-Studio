using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
namespace InFalsusStudio.NativeEditorUI {
 internal sealed class UiRasterGlyph {
  internal int Width,Height,Advance,Top,Bottom,Padding,Baseline,Left;
  internal bool Ink;internal byte[] Pixels;
 }
 // Rasterize at the final integer pixel size, rather than shrinking a 72px bitmap.
 internal static partial class UiFontRaster {
  [StructLayout(LayoutKind.Sequential)]struct Size {internal int X,Y;}
  [StructLayout(LayoutKind.Sequential)]struct Header {internal uint Size;internal int Width,Height;internal ushort Planes,Bits;internal uint Compression,ImageSize;internal int XPels,YPels;internal uint Colors,Important;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Metrics {internal int Height,Ascent,Descent,InternalLeading,ExternalLeading,AverageWidth,MaximumWidth,Weight,Overhang,AspectX,AspectY;internal ushort First,Last,Default,Break;internal byte Italic,Underline,Strike,Pitch,Charset;}
  [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr hdc);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr hdc);
  [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateFont(int h,int w,int e,int o,int weight,uint italic,uint underline,uint strike,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool GetTextExtentPoint32(IntPtr dc,string text,int count,out Size size);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool GetTextMetrics(IntPtr dc,out Metrics metrics);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern uint GetGlyphIndices(IntPtr dc,string text,int count,[Out]ushort[] indices,uint flags);
  [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr dc,ref Header info,uint usage,out IntPtr bits,IntPtr section,uint offset);
  [DllImport("gdi32.dll")]static extern uint SetTextColor(IntPtr dc,uint color);
  [DllImport("gdi32.dll")]static extern int SetBkMode(IntPtr dc,int mode);
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool TextOut(IntPtr dc,int x,int y,string text,int count);
  [DllImport("gdi32.dll")]static extern bool GdiFlush();
  struct GlyphKey:IEquatable<GlyphKey>{internal string Face;internal int Pixels;internal char Character;public bool Equals(GlyphKey b){return Face==b.Face&&Pixels==b.Pixels&&Character==b.Character;}public override bool Equals(object b){return b is GlyphKey&&Equals((GlyphKey)b);}public override int GetHashCode(){return Face.GetHashCode()^Pixels^(Character<<16);}}
  static readonly Dictionary<GlyphKey,UiRasterGlyph> letters=new Dictionary<GlyphKey,UiRasterGlyph>();
  internal static int Pixels(float design,float scale) {
   int minimum=design<=14?13:design<20?15:18;
   return Math.Max(minimum,(int)Math.Round(design*scale));
  }
  internal static UiRasterGlyph Glyph(string face,char ch,int pixels) {
   var key=new GlyphKey{Face=face,Pixels=pixels,Character=ch};UiRasterGlyph result;if(letters.TryGetValue(key,out result))return result;
   IntPtr dc=CreateCompatibleDC(IntPtr.Zero),font=IntPtr.Zero,bmp=IntPtr.Zero,oldFont=IntPtr.Zero,oldBmp=IntPtr.Zero;
   if(dc==IntPtr.Zero)throw new InvalidOperationException("UI font device context failed.");
   try {
    foreach(string candidate in new[]{face,"Chiron Hei HK N","OT-PUD Shin Go Pr6N R","Microsoft YaHei UI","Segoe UI"}.Distinct()) {
     if(font!=IntPtr.Zero){SelectObject(dc,oldFont);DeleteObject(font);}
     font=CreateFont(-pixels,0,0,0,400,0,0,0,1,0,0,4,0,candidate);
     if(font==IntPtr.Zero)throw new InvalidOperationException("UI font creation failed.");
     oldFont=SelectObject(dc,font);var indices=new ushort[1];GetGlyphIndices(dc,ch.ToString(),1,indices,1);if(indices[0]!=0xffff)break;
    }
    Size size;Metrics metrics;if(!GetTextExtentPoint32(dc,ch.ToString(),1,out size)||!GetTextMetrics(dc,out metrics))throw new InvalidOperationException("UI glyph measurement failed.");
    int padding=Math.Max(2,pixels/8),width=Math.Max(8,size.X+padding*2),height=Math.Max(pixels*2,size.Y+padding*2);
    var header=new Header{Size=40,Width=width,Height=-height,Planes=1,Bits=32,ImageSize=(uint)(width*height*4)};
    IntPtr bits;bmp=CreateDIBSection(dc,ref header,0,out bits,IntPtr.Zero,0);if(bmp==IntPtr.Zero)throw new InvalidOperationException("UI glyph bitmap failed.");
    oldBmp=SelectObject(dc,bmp);var data=new byte[width*height*4];Marshal.Copy(data,0,bits,data.Length);
    SetBkMode(dc,1);SetTextColor(dc,0xffffff);if(!TextOut(dc,padding,padding,ch.ToString(),1))throw new InvalidOperationException("UI glyph drawing failed.");
    GdiFlush();Marshal.Copy(bits,data,0,data.Length);int top=height,bottom=0,left=width;bool ink=false;
    for(int y=0;y<height;y++)for(int x=0;x<width;x++){int j=(y*width+x)*4;byte coverage=Math.Max(data[j],Math.Max(data[j+1],data[j+2]));if(coverage>0){top=Math.Min(top,y);bottom=Math.Max(bottom,y+1);left=Math.Min(left,x);ink=true;}data[j]=data[j+1]=data[j+2]=255;data[j+3]=coverage;}
    var rgba=new byte[data.Length];int stride=width*4;for(int y=0;y<height;y++)Buffer.BlockCopy(data,y*stride,rgba,(height-1-y)*stride,stride);
    result=new UiRasterGlyph{Width=width,Height=height,Advance=size.X,Top=ink?top:0,Bottom=bottom,Ink=ink,Padding=padding,Baseline=padding+metrics.Ascent,Left=ink?left:padding,Pixels=rgba};letters.Add(key,result);return result;
   } finally {
    if(oldBmp!=IntPtr.Zero)SelectObject(dc,oldBmp);if(bmp!=IntPtr.Zero)DeleteObject(bmp);
    if(oldFont!=IntPtr.Zero)SelectObject(dc,oldFont);if(font!=IntPtr.Zero)DeleteObject(font);DeleteDC(dc);
   }
  }
  internal static int Measure(string text,string face,int pixels){return Layout(text,face,pixels).Width;}
  internal static string Ellipsize(string text,string face,int pixels,float width){if(Measure(text,face,pixels)<=width)return text;while(text.Length>0&&Measure(text+"…",face,pixels)>width)text=text.Substring(0,text.Length-1);return text+"…";}
  static string[] WrapUncached(string text,string face,int pixels,float width){
   var lines=new List<string>();string line="";int used=0;
   for(int i=0;i<text.Length;){char ch=text[i];if(ch=='\r'){i++;continue;}if(ch=='\n'){lines.Add(line.TrimEnd());line="";used=0;i++;continue;}
    int end=i+1;if(ch>' '&&ch<'\x7f')while(end<text.Length&&text[end]>' '&&text[end]<'\x7f')end++;
    string token=text.Substring(i,end-i);int tokenWidth=Measure(token,face,pixels);
    if(line.Length>0&&used+tokenWidth>width){lines.Add(line.TrimEnd());line="";used=0;}
    foreach(char c in token){int advance=Glyph(face,c,pixels).Advance;if(line.Length>0&&used+advance>width){lines.Add(line.TrimEnd());line="";used=0;}if(line.Length==0&&c==' ')continue;line+=c;used+=advance;}i=end;
   }
   if(line.Length>0)lines.Add(line);return lines.ToArray();
  }
  internal static void Clear(){letters.Clear();ClearLayouts();}
 }
}
