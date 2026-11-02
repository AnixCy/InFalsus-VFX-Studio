using System;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  static void PrepareAboutLineArt(Texture2D image){
   // Decode the authored line art as a tintable stroke mask. Discard low-alpha
   // matte residue and tiny detached fragments before displaying it at 170 px.
   var pixels=image.GetPixels32();int width=image.width,height=image.height;
   for(int i=0;i<pixels.Length;i++){
    var p=pixels[i];int alpha=p.a<=128||(p.r<40&&p.b>200)?0:Math.Min(255,(p.a-128)*2);
    pixels[i]=new Color32(255,255,255,(byte)alpha);
   }
   var visited=new bool[pixels.Length];var queue=new int[pixels.Length];
   for(int start=0;start<pixels.Length;start++){
    if(visited[start]||pixels[start].a==0)continue;
    int read=0,count=1;queue[0]=start;visited[start]=true;
    while(read<count){
     int at=queue[read++],x=at%width,y=at/width;
     for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
      int nx=x+dx,ny=y+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;
      int next=ny*width+nx;if(visited[next]||pixels[next].a==0)continue;visited[next]=true;queue[count++]=next;
     }
    }
    if(count<48)for(int i=0;i<count;i++)pixels[queue[i]].a=0;
   }
   image.SetPixels32(pixels);image.Apply();
  }
 }
}
