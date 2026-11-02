using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  readonly Dictionary<string,Rect> iconInk=new Dictionary<string,Rect>();
  Texture2D jacketSource,jacketFiltered;
  void FixInputStates(){
   foreach(var style in new[]{skin.textField,skin.textArea}){
    foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused}){state.background=fieldTex;state.textColor=Css("#d3edf0");}
    style.font=textFont;style.fontStyle=FontStyle.Normal;style.richText=false;style.contentOffset=Vector2.zero;style.border=new RectOffset(1,1,1,1);style.padding=new RectOffset(8,8,4,4);style.fixedHeight=0;style.fixedWidth=0;style.clipping=TextClipping.Clip;
   }
   skin.textField.alignment=TextAnchor.MiddleLeft;skin.textArea.alignment=TextAnchor.UpperLeft;
   skin.settings.cursorColor=Css("#d8faff");skin.settings.selectionColor=Css("#24586b");
  }
  void PaintFooterIcon(Rect box,string key){
   var texture=Icon(key);Rect ink;if(!iconInk.TryGetValue(key,out ink)){
    var data=texture.GetPixels32();int left=texture.width,right=0,top=texture.height,bottom=0;
    for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(data[y*texture.width+x].a>8){left=Mathf.Min(left,x);right=Mathf.Max(right,x+1);top=Mathf.Min(top,y);bottom=Mathf.Max(bottom,y+1);}
    ink=right>left&&bottom>top?new Rect(left/(float)texture.width,top/(float)texture.height,(right-left)/(float)texture.width,(bottom-top)/(float)texture.height):new Rect(0,0,1,1);iconInk[key]=ink;
   }
   // Recenter visible pixels, preserving the icon's original size and aspect.
   float dx=(.5f-ink.center.x)*box.width,dy=(ink.center.y-.5f)*box.height;
   box.x+=dx;box.y+=dy;var p=PixelOrigin(box.x,box.y);box.position=p;GUI.DrawTexture(box,texture);
  }
  Texture2D FilterJacket(Texture2D source){
   if(!source)return source;if(source==jacketSource&&jacketFiltered)return jacketFiltered;
   if(jacketFiltered)Destroy(jacketFiltered);jacketFiltered=null;jacketSource=source;
   try{
    // Covers load without mip levels in the base editor. A private copy prevents
    // undersampling at the small HUD size, without changing the source asset.
    if(!source.isReadable||(long)source.width*source.height>4194304)return source;
    jacketFiltered=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true);jacketFiltered.name="UI cover sampling";jacketFiltered.hideFlags=HideFlags.HideAndDontSave;
    jacketFiltered.SetPixels32(source.GetPixels32());jacketFiltered.Apply(true,false);jacketFiltered.filterMode=FilterMode.Trilinear;jacketFiltered.wrapMode=TextureWrapMode.Clamp;
    return jacketFiltered;
   }catch(Exception e){if(jacketFiltered)Destroy(jacketFiltered);jacketFiltered=null;Logger.LogWarning("Cover sampling retained: "+e.Message);return source;}
  }
  void DisposeLayoutFixes(){if(jacketFiltered)Destroy(jacketFiltered);jacketFiltered=null;jacketSource=null;iconInk.Clear();}
 }
}
