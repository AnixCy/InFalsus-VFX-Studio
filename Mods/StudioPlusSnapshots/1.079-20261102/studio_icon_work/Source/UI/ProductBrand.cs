using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 internal static class ProductBrand {
  internal const string Id="infstudioplus",Name="InFalsus Studio+",Version="1.0.7",Display=Name+" "+Version;
  internal const string InternalVersion="1.075";
  internal const string Attribution="Forked from Arthas-0/InFalsus-Studio";
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool SetWindowText(IntPtr window,string text);
  static bool titleApplied;static float nextTitleAttempt;
  internal static void Tick(){
   if(titleApplied||Time.unscaledTime<nextTitleAttempt)return;nextTitleAttempt=Time.unscaledTime+1;
   if(Application.platform!=RuntimePlatform.WindowsPlayer)return;
   using(var process=Process.GetCurrentProcess()){IntPtr window=process.MainWindowHandle;if(window!=IntPtr.Zero)titleApplied=SetWindowText(window,Display);}
  }
 }
 public sealed partial class NativeEditorUIPlugin {
  Texture2D aboutMascot,aboutTitleTexture,aboutAttributionTexture;UiRasterGlyph aboutTitleRaster,aboutAttributionRaster;int aboutTitlePixels,aboutAttributionPixels;float operationContentHeight=850;
  void EnsureAboutTitle(int pixels){
   if(aboutTitleTexture!=null&&aboutTitlePixels==pixels)return;
   if(aboutTitleTexture)Destroy(aboutTitleTexture);
   aboutTitleRaster=UiFontRaster.WholeLine(ProductBrand.Name+"  "+ProductBrand.Version,NumFace,pixels);
   aboutTitleTexture=new Texture2D(aboutTitleRaster.Width,aboutTitleRaster.Height,TextureFormat.RGBA32,false);
   aboutTitleTexture.LoadRawTextureData(aboutTitleRaster.Pixels);aboutTitleTexture.Apply();aboutTitleTexture.filterMode=FilterMode.Point;
   aboutTitleTexture.wrapMode=TextureWrapMode.Clamp;aboutTitlePixels=pixels;
  }
  void DrawAboutTitle(Rect rect){
   if(Event.current.type!=EventType.Repaint)return;
   int pixels=FontPixels(21);
   while(true){
    EnsureAboutTitle(pixels);if(aboutTitleRaster.Advance<=rect.width-16*scale||pixels<=12)break;pixels--;
   }
   var raster=aboutTitleRaster;
   Vector2 origin=PixelOrigin(rect.center.x-raster.Advance*.5f-raster.Padding,rect.center.y-(raster.Top+raster.Bottom)*.5f);
   var old=GUI.color;try{GUI.color=old*Css("#c9e9ee");GUI.DrawTexture(new Rect(origin.x,origin.y,raster.Width,raster.Height),aboutTitleTexture);}finally{GUI.color=old;}
  }
  void DrawAboutAttribution(Rect rect){
   if(Event.current.type!=EventType.Repaint)return;
   int pixels=FontPixels(13);
   if(aboutAttributionTexture==null||aboutAttributionPixels!=pixels){
    if(aboutAttributionTexture)Destroy(aboutAttributionTexture);
    aboutAttributionRaster=UiFontRaster.WholeLine(ProductBrand.Attribution,NumFace,pixels);
    aboutAttributionTexture=new Texture2D(aboutAttributionRaster.Width,aboutAttributionRaster.Height,TextureFormat.RGBA32,false);
    aboutAttributionTexture.LoadRawTextureData(aboutAttributionRaster.Pixels);aboutAttributionTexture.Apply();
    aboutAttributionTexture.filterMode=FilterMode.Point;aboutAttributionTexture.wrapMode=TextureWrapMode.Clamp;aboutAttributionPixels=pixels;
   }
   var raster=aboutAttributionRaster;
   Vector2 origin=PixelOrigin(rect.center.x-raster.Advance*.5f-raster.Padding,rect.center.y-(raster.Top+raster.Bottom)*.5f);
   var old=GUI.color;try{GUI.color=old*Css("#83a9ba");GUI.DrawTexture(new Rect(origin.x,origin.y,raster.Width,raster.Height),aboutAttributionTexture);}finally{GUI.color=old;}
  }
  void DrawBrandAbout(){
   if(aboutMascot==null){
    using(var resource=typeof(ProductBrand).Assembly.GetManifestResourceStream("infstudioplus.about.png")){
     if(resource!=null)using(var bytes=new MemoryStream()){resource.CopyTo(bytes);aboutMascot=new Texture2D(2,2,TextureFormat.RGBA32,false);typeof(ImageConversion).GetMethod("LoadImage",new[]{typeof(Texture2D),typeof(byte[]),typeof(bool)}).Invoke(null,new object[]{aboutMascot,bytes.ToArray(),false});PrepareAboutLineArt(aboutMascot);aboutMascot.filterMode=FilterMode.Bilinear;aboutMascot.wrapMode=TextureWrapMode.Clamp;}
    }
   }
   contentY+=24*scale;Rect row=ContentRow(288);float center=row.center.x;
   if(aboutMascot!=null&&Event.current.type==EventType.Repaint){var old=GUI.color;try{GUI.color=old*Css("#7ee4fd");GUI.DrawTexture(new Rect(center-105*scale,row.y,210*scale,210*scale),aboutMascot,ScaleMode.ScaleToFit,true);}finally{GUI.color=old;}}
   DrawAboutTitle(new Rect(0,row.y+211*scale,row.width,34*scale));
   DrawAboutAttribution(new Rect(0,row.y+251*scale,row.width,27*scale));
   operationContentHeight=contentY/scale+16;
  }
  void DisposeBrand(){if(aboutMascot)Destroy(aboutMascot);if(aboutTitleTexture)Destroy(aboutTitleTexture);if(aboutAttributionTexture)Destroy(aboutAttributionTexture);aboutMascot=aboutTitleTexture=aboutAttributionTexture=null;aboutTitleRaster=aboutAttributionRaster=null;}
 }
}
