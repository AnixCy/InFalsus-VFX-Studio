using InFalsus.Presentation;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    PackageDocument? sceneDocument;
    SceneOpacityPlan? scenePreviewPlan;
    internal SceneOpacityValues ScenePreviewValues(double seconds)
    {
        var document=Model!.RenderDocument;
        if(!ReferenceEquals(sceneDocument,document)){sceneDocument=document;scenePreviewPlan=new SceneOpacityPlan(document);}
        return scenePreviewPlan!.Sample(seconds,Model.Values);
    }
    void DrawSceneReference()
    {
        var values=ScenePreviewValues(CurrentSeconds);
        if(scenePreviewPlan!.Count==0)return;
        var previous=GUI.color;var matrix=GUI.matrix;
        try {
            Vector2 Point(float x,float y)=>new(canvas.x+canvas.width*x,canvas.y+canvas.height*y);
            void Line(Vector2 a,Vector2 b,float alpha,float width=1.5f) {
                var delta=b-a;GUI.color=new Color(.48f,.9f,1,alpha*.8f);
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
                GUI.DrawTexture(new Rect(a.x,a.y,delta.magnitude,width*UiScale),Texture2D.whiteTexture);GUI.matrix=matrix;
            }
            for(int i=0;i<7;i++)Line(Point(.44f+i*.02f,.22f),Point(.12f+i*.1266f,.94f),values.Track,1.8f);
            Line(Point(.12f,.83f),Point(.88f,.83f),values.Track,3);
            Line(Point(.06f,.98f),Point(.39f,.22f),values.Track,2);
            Line(Point(.94f,.98f),Point(.61f,.22f),values.Track,2);
            void Label(float x,float y,float w,string text,float alpha,int size=20) {
                if(alpha<=0)return;GUI.color=Color.white;
                var rect=new Rect(canvas.x+x*canvas.width,canvas.y+y*canvas.height,w*canvas.width,.065f*canvas.height);
                StudioUiTheme.Fill(rect,"#081923"+Mathf.RoundToInt(alpha*190).ToString("X2"));
                ui!.Label(rect,text,size,"#c8f4ff"+Mathf.RoundToInt(alpha*255).ToString("X2"),false,1);
            }
            string title=Bridge.Owner?Convert.ToString(typeof(InFalsusStudio.StudioBootstrap).GetField("guiSongTitle",StudioBridge.Flags)?.GetValue(Bridge.Owner))??"":"";
            Label(.035f,.035f,.3f,"099'000'000",values.Score,22);
            Label(.65f,.035f,.31f,title.Length==0?"曲名资料":title,values.SongInfo,19);
            Label(.035f,.115f,.14f,"FL",values.ClearState,17);
            Label(.42f,.72f,.16f,"EXACT+",values.Judgement,18);
            ui!.Label(new Rect(canvas.x+8*UiScale,canvas.yMax-22*UiScale,200*UiScale,20*UiScale),"轨道 / UI 参照预览",11,"#88a8b6");
        }finally{GUI.color=previous;GUI.matrix=matrix;}
    }
}
