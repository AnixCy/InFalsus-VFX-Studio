using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  struct InputStyleKey:IEquatable<InputStyleKey>{
   internal int Pixels,Padding,Align;
   public bool Equals(InputStyleKey b){return Pixels==b.Pixels&&Padding==b.Padding&&Align==b.Align;}
   public override bool Equals(object b){return b is InputStyleKey&&Equals((InputStyleKey)b);}
   public override int GetHashCode(){return Pixels^(Padding<<12)^(Align<<24);}
  }
  readonly Dictionary<InputStyleKey,GUIStyle> inputStyles=new Dictionary<InputStyleKey,GUIStyle>();GUIStyle inputTemplate;
  GUIStyle InputStyle(int pixels,int padding,int align){
   if(inputTemplate!=number){inputStyles.Clear();inputTemplate=number;}
   var key=new InputStyleKey{Pixels=pixels,Padding=padding,Align=align};GUIStyle field;
   if(inputStyles.TryGetValue(key,out field))return field;
   if(inputStyles.Count>=64)inputStyles.Clear();
   field=new GUIStyle(number){font=textFont,fontSize=pixels,wordWrap=false,border=new RectOffset(),padding=new RectOffset(padding,padding,0,0),alignment=align==2?TextAnchor.MiddleRight:TextAnchor.MiddleLeft};
   foreach(var state in new[]{field.normal,field.hover,field.active,field.focused,field.onNormal,field.onHover,field.onActive,field.onFocused}){state.background=null;state.textColor=Color.clear;}
   inputStyles[key]=field;return field;
  }
 }
}
