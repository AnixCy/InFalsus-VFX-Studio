using System;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  internal static bool TimingBorderMasks=true;
  struct TimingBorderKey:IEquatable<TimingBorderKey> {
   internal int W,H,L,R,T,B;
   public bool Equals(TimingBorderKey b){return W==b.W&&H==b.H&&L==b.L&&R==b.R&&T==b.T&&B==b.B;}
   public override bool Equals(object b){return b is TimingBorderKey&&Equals((TimingBorderKey)b);}
   public override int GetHashCode(){return W^(H<<12)^(L<<2)^(R<<5)^(T<<8)^(B<<10);}
  }
  readonly Dictionary<TimingBorderKey,Texture2D> timingBorders=new Dictionary<TimingBorderKey,Texture2D>();
  readonly Queue<TimingBorderKey> timingBorderOrder=new Queue<TimingBorderKey>();
  bool PaintTimingBorder(Rect rect,Color color,float thickness){
   if(!TimingBorderMasks)return false;
   if(Event.current.type!=EventType.Repaint||!TimingRectVisible(rect))return true;
   // Preserve the covered screen pixels at fractional UI scales and scroll offsets.
   var start=GUIUtility.GUIToScreenPoint(rect.position);var end=GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax,rect.yMax));
   int x=Mathf.RoundToInt(start.x),y=Mathf.RoundToInt(start.y),right=Mathf.RoundToInt(end.x),bottom=Mathf.RoundToInt(end.y);
   var key=new TimingBorderKey{W=right-x,H=bottom-y,L=Mathf.RoundToInt(start.x+thickness)-x,R=right-Mathf.RoundToInt(end.x-thickness),T=Mathf.RoundToInt(start.y+thickness)-y,B=bottom-Mathf.RoundToInt(end.y-thickness)};
   if(key.W<=0||key.H<=0||key.W>4096||key.H>4096)return false;
   Texture2D texture;if(!timingBorders.TryGetValue(key,out texture)){
    if(timingBorders.Count>=64){var oldest=timingBorderOrder.Dequeue();Destroy(timingBorders[oldest]);timingBorders.Remove(oldest);}
    var bytes=new byte[key.W*key.H*4];for(int row=0;row<key.H;row++)for(int col=0;col<key.W;col++){int at=(row*key.W+col)*4;bytes[at]=bytes[at+1]=bytes[at+2]=255;bytes[at+3]=(byte)(col<key.L||col>=key.W-key.R||row<key.B||row>=key.H-key.T?255:0);}
    texture=new Texture2D(key.W,key.H,TextureFormat.RGBA32,false);texture.LoadRawTextureData(bytes);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Point;texture.hideFlags=HideFlags.HideAndDontSave;timingBorders.Add(key,texture);timingBorderOrder.Enqueue(key);
   }
   var previous=GUI.color;GUI.color=previous*color;try{GUI.DrawTexture(new Rect(rect.x+x-start.x,rect.y+y-start.y,key.W,key.H),texture);}finally{GUI.color=previous;}return true;
  }
  void DisposeTimingBorders(){foreach(var texture in timingBorders.Values)if(texture)Destroy(texture);timingBorders.Clear();timingBorderOrder.Clear();}
 }
}
