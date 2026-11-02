using System;
namespace InFalsusStudio.NativeEditorUI {
 // Own the pointer selection through MouseUp: native IMGUI double-click and
 // select-all-on-release must not restore the selection that a second click cleared.
 internal sealed class NumericSelectionGesture {
  internal bool Active,All;internal int Anchor,Cursor;
  int length;string key;
  internal bool Is(string name){return Active&&key==name;}
  int Clamp(int n){return Math.Max(0,Math.Min(length,n));}
  internal void Begin(string name,bool focused,bool shift,int oldAnchor,int index,int count){
   Active=true;key=name;length=count;All=!focused;Cursor=Clamp(index);Anchor=focused&&shift?Clamp(oldAnchor):Cursor;
  }
  internal void Drag(int index){if(!Active)return;All=false;Cursor=Clamp(index);}
  internal void End(){Active=false;}
 }
}
