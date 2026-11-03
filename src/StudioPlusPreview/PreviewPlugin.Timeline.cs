using InFalsus.Presentation.Authoring;
using InFalsus.Presentation;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    ParameterValue[] curveSamples=Array.Empty<ParameterValue>();
    void DrawTimeline(Rect rect,ParameterDescriptor[] parameters)
    {
        var theme=ui!;float s=UiScale,row=29*s,names=175*s,ruler=35*s;
        StudioUiTheme.Fill(rect,"#0c202b");theme.Rule(new Rect(rect.x,rect.y,rect.width,1));
        timelineRuler=new Rect(rect.x+names,rect.y+35*s,rect.width-names-12*s,ruler);
        timelineBody=new Rect(rect.x,rect.y+70*s,rect.width,rect.height-78*s);
        theme.Label(new Rect(12*s,rect.y+3*s,150*s,28*s),"参数时间轴",18,"#c9e9ee");
        if(theme.Button(new Rect(180*s,rect.y+4*s,32*s,26*s),"−",false,false))timelineSpan=Math.Min(600,timelineSpan*1.5);
        if(theme.Button(new Rect(218*s,rect.y+4*s,32*s,26*s),"+",false,false))timelineSpan=Math.Max(.1,timelineSpan/1.5);
        if(theme.Button(new Rect(262*s,rect.y+4*s,70*s,26*s),"跟随",false,false))timelineStart=Math.Max(0,CurrentSeconds-timelineSpan*.2);
        theme.Label(new Rect(350*s,rect.y+4*s,rect.width-360*s,26*s),"双击添加 · 拖动移动 · Ctrl+滚轮缩放 · Shift+滚轮平移",12,"#7c9dad");
        double step=NiceStep(timelineSpan/10);
        for(double t=Math.Ceiling(timelineStart/step)*step;t<=timelineStart+timelineSpan;t+=step) {
            float x=TimeX(t);theme.Label(new Rect(x+3, timelineRuler.y,75*s,ruler),t.ToString("0.###",CI),12,"#7c9dad",true);
            StudioUiTheme.Fill(new Rect(x,timelineRuler.yMax,1,timelineBody.height),"#203b48");
        }
        float maxScroll=Mathf.Max(0,parameters.Length*row-timelineBody.height);trackScroll=Mathf.Clamp(trackScroll,0,maxScroll);
        GUI.BeginGroup(timelineBody);
        try {
            int first=Mathf.Max(0,Mathf.FloorToInt(trackScroll/row)),last=Mathf.Min(parameters.Length,first+Mathf.CeilToInt(timelineBody.height/row)+2);
            for(int i=first;i<last;i++) {
                var parameter=parameters[i];float y=i*row-trackScroll;
                if(parameter.Id==selectedParameter)StudioUiTheme.Fill(new Rect(0,y,rect.width,row),"#173743");
                theme.Label(new Rect(12*s,y,names-22*s,row),parameter.Label,13);
                theme.Rule(new Rect(0,y+row-1,rect.width,1));
                if(parameter.Id==selectedParameter&&Event.current.type==EventType.Repaint) {
                    if(curveSamples.Length!=Model!.Values.Length)curveSamples=new ParameterValue[Model.Values.Length];
                    int index=Model.Catalog.Parameters.ToList().FindIndex(p=>p.Id==parameter.Id);
                    double lo=parameter.SuggestedMin??0,hi=parameter.SuggestedMax??1;
                    foreach(var k in Model.Keys.Where(k=>k.ParameterId==parameter.Id)){lo=Math.Min(lo,k.Value.Number.X);hi=Math.Max(hi,k.Value.Number.X);}
                    if(hi<=lo)hi=lo+1;Vector2 previous=default;
                    for(int n=0;n<=48;n++) {
                        double time=timelineStart+timelineSpan*n/48;Model.SampleCurve(time,curveSamples);
                        var point=new Vector2(names+(rect.width-names-12*s)*n/48,
                            y+row-4*s-(float)Math.Clamp((curveSamples[index].Number.X-lo)/(hi-lo),0,1)*(row-8*s));
                        if(n>0)DrawCurveLine(previous,point);previous=point;
                    }
                }
                foreach(var key in Model!.Keys.Where(k=>k.ParameterId==parameter.Id)) {
                    double seconds=key.Id==selectedKey&&Model.MovingSeconds.HasValue?Model.MovingSeconds.Value:key.Seconds;
                    float x=TimeX(seconds)-rect.x;if(x<names||x>rect.width)continue;
                    DrawDiamond(new Vector2(x,y+row*.5f),key.Id==selectedKey?6*s:4*s,key.Id==selectedKey?"#d8faff":"#82cfd6");
                }
            }
            float head=TimeX(CurrentSeconds)-rect.x;
            if(head>=names&&head<rect.width)StudioUiTheme.Fill(new Rect(head,0,2*s,timelineBody.height),"#b4efef");
        }finally{GUI.EndGroup();}
        TimelineInput(parameters,row,names);
    }
    float TimeX(double time)=>timelineRuler.x+(float)((time-timelineStart)/timelineSpan)*timelineRuler.width;
    double XTime(float x)=>Math.Max(0,timelineStart+(x-timelineRuler.x)/timelineRuler.width*timelineSpan);
    static double NiceStep(double step){double n=Math.Pow(10,Math.Floor(Math.Log10(Math.Max(step,.0001))));double v=step/n;return(v<=1?1:v<=2?2:v<=5?5:10)*n;}
    static void DrawDiamond(Vector2 center,float size,string color)
    {
        if(Event.current.type!=EventType.Repaint)return;var matrix=GUI.matrix;
        try{GUIUtility.RotateAroundPivot(45,center);StudioUiTheme.Fill(new Rect(center.x-size*.5f,center.y-size*.5f,size,size),color);}
        finally{GUI.matrix=matrix;}
    }
    static void DrawCurveLine(Vector2 from,Vector2 to)
    {
        var matrix=GUI.matrix;float length=(to-from).magnitude;
        try{GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg,from);
            StudioUiTheme.Fill(new Rect(from.x,from.y,length,1),"#79b1bf");}
        finally{GUI.matrix=matrix;}
    }
    void TimelineInput(ParameterDescriptor[] parameters,float row,float names)
    {
        var e=Event.current;var point=e.mousePosition;
        timelineControl=GUIUtility.GetControlID(0x564654,FocusType.Passive);
        if(e.type==EventType.ScrollWheel&&(timelineBody.Contains(point)||timelineRuler.Contains(point))) {
            if(e.control||e.command) {
                double at=XTime(point.x),fraction=(point.x-timelineRuler.x)/timelineRuler.width;
                timelineSpan=Math.Clamp(timelineSpan*Math.Pow(1.15,e.delta.y),.1,600);timelineStart=Math.Max(0,at-fraction*timelineSpan);
            }else if(e.shift)timelineStart=Math.Max(0,timelineStart+e.delta.y*timelineSpan*.05);
            else trackScroll+=e.delta.y*row;
            e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==0&&timelineRuler.Contains(point)) {
            FinishText();Model!.Commit();Bridge.SeekSeconds(XTime(point.x));GUI.FocusControl(null);e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==0&&timelineBody.Contains(point)) {
            FinishText();Model!.Commit();GUI.FocusControl(null);
            int index=(int)((point.y-timelineBody.y+trackScroll)/row);if(index<0||index>=parameters.Length)return;
            selectedParameter=parameters[index].Id;
            var key=Model.Keys.Where(k=>k.ParameterId==selectedParameter&&Math.Abs(TimeX(k.Seconds)-point.x)<8*UiScale)
                .OrderBy(k=>Math.Abs(TimeX(k.Seconds)-point.x)).FirstOrDefault();
            if(point.x>=timelineRuler.x&&key!=null) {
                selectedKey=key.Id;Bridge.SeekSeconds(key.Seconds);draggingKey=true;GUIUtility.hotControl=timelineControl;
            }else if(point.x>=timelineRuler.x) {
                selectedKey="";Bridge.SeekSeconds(XTime(point.x));
                if(e.clickCount>1)selectedKey=Model.AddKey(selectedParameter,XTime(point.x));
            }else selectedKey="";
            e.Use();return;
        }
        if(draggingKey&&e.type==EventType.MouseDrag) {
            try{Model!.MoveKey(selectedKey,XTime(point.x));Bridge.SeekSeconds(XTime(point.x));}catch(Exception ex){Error(ex);}
            e.Use();
        }else if(draggingKey&&e.rawType==EventType.MouseUp) {
            draggingKey=false;GUIUtility.hotControl=0;Model!.Commit();e.Use();
        }
    }
}
