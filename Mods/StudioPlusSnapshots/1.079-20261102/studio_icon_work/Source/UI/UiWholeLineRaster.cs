using System;
using System.Runtime.InteropServices;
namespace InFalsusStudio.NativeEditorUI {
 internal static partial class UiFontRaster {
  // The about title owns a padded, complete text canvas. Do not crop its glyph
  // quads or reconstruct UV rectangles from a fractional-height label box.
  internal static UiRasterGlyph WholeLine(string text,string face,int pixels){
   IntPtr dc=CreateCompatibleDC(IntPtr.Zero),font=IntPtr.Zero,bmp=IntPtr.Zero,oldFont=IntPtr.Zero,oldBmp=IntPtr.Zero;
   if(dc==IntPtr.Zero)throw new InvalidOperationException("Title font device context failed.");
   try{
    font=CreateFont(-pixels,0,0,0,400,0,0,0,1,0,0,4,0,face);
    if(font==IntPtr.Zero)throw new InvalidOperationException("Title font creation failed.");oldFont=SelectObject(dc,font);
    Size size;Metrics metrics;if(!GetTextExtentPoint32(dc,text,text.Length,out size)||!GetTextMetrics(dc,out metrics))throw new InvalidOperationException("Title measurement failed.");
    int padding=Math.Max(4,pixels/4),width=Math.Max(1,size.X)+padding*2+Math.Max(0,metrics.Overhang);
    int height=Math.Max(size.Y,metrics.Height)+padding*2+Math.Max(2,metrics.ExternalLeading);
    var header=new Header{Size=40,Width=width,Height=-height,Planes=1,Bits=32,ImageSize=(uint)(width*height*4)};
    IntPtr bits;bmp=CreateDIBSection(dc,ref header,0,out bits,IntPtr.Zero,0);if(bmp==IntPtr.Zero)throw new InvalidOperationException("Title bitmap failed.");
    oldBmp=SelectObject(dc,bmp);var data=new byte[width*height*4];Marshal.Copy(data,0,bits,data.Length);
    SetBkMode(dc,1);SetTextColor(dc,0xffffff);if(!TextOut(dc,padding,padding,text,text.Length))throw new InvalidOperationException("Title drawing failed.");
    GdiFlush();Marshal.Copy(bits,data,0,data.Length);int top=height,bottom=0,left=width;bool ink=false;
    for(int y=0;y<height;y++)for(int x=0;x<width;x++){
     int at=(y*width+x)*4;byte coverage=Math.Max(data[at],Math.Max(data[at+1],data[at+2]));
     if(coverage>0){top=Math.Min(top,y);bottom=Math.Max(bottom,y+1);left=Math.Min(left,x);ink=true;}
     data[at]=data[at+1]=data[at+2]=255;data[at+3]=coverage;
    }
    var rgba=new byte[data.Length];int stride=width*4;for(int y=0;y<height;y++)Buffer.BlockCopy(data,y*stride,rgba,(height-1-y)*stride,stride);
    return new UiRasterGlyph{Width=width,Height=height,Advance=size.X,Top=top,Bottom=bottom,Ink=ink,Padding=padding,Baseline=padding+metrics.Ascent,Left=left,Pixels=rgba};
   }finally{
    if(oldBmp!=IntPtr.Zero)SelectObject(dc,oldBmp);if(bmp!=IntPtr.Zero)DeleteObject(bmp);
    if(oldFont!=IntPtr.Zero)SelectObject(dc,oldFont);if(font!=IntPtr.Zero)DeleteObject(font);DeleteDC(dc);
   }
  }
 }
}
