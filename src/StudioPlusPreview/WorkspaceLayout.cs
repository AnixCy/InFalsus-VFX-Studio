using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
internal sealed class WorkspaceLayout
{
    internal float Scale,Width,Height,Top,Left,Right,Bottom;
    internal bool Compact;
    internal Rect Library,Stage,Inspector,Timeline;
    internal static float ScaleFor(float width,float height,float preferred)=>Mathf.Min(preferred,Mathf.Max(.65f,Mathf.Min(width/1100f,height/650f)));
    internal static WorkspaceLayout Calculate(float width,float height,float scale)
    {
        float w=width/scale,h=height/scale;bool compact=w<1020;float top=compact?94:52;
        float left=Mathf.Clamp(w*.18f,180,240),right=Mathf.Clamp(w*.28f,250,320),timeline=Mathf.Clamp(h*.36f,240,400),bottom=h-timeline;
        return new WorkspaceLayout{Scale=scale,Width=w,Height=h,Compact=compact,Top=top,Left=left,Right=right,Bottom=bottom,
            Library=new Rect(0,(top+1)*scale,left*scale,(bottom-top-1)*scale),
            Stage=new Rect((left+12)*scale,(top+13)*scale,(w-left-right-24)*scale,(bottom-top-25)*scale),
            Inspector=new Rect((w-right)*scale,(top+1)*scale,right*scale,(bottom-top-1)*scale),
            Timeline=new Rect(0,bottom*scale,width,timeline*scale)};
    }
}
