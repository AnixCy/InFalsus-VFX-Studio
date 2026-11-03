using InFalsus.Presentation.Authoring;
using InFalsus.Presentation;
using UnityEngine;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    internal sealed class EffectRow
    {internal readonly string Node;internal readonly string? Parameter;internal float Top,Height;internal EffectRow(string node,string? parameter){Node=node;Parameter=parameter;}}
    ParameterValue[][] curveSamples=Array.Empty<ParameterValue[]>();
    bool draggingEffect,resizingEffect;
    string draggedEffect="";
    double effectDragPointer,effectDragStart;
    double? effectDragEnd;
    List<EffectRow> TimelineRows()
    {
        var rows=new List<EffectRow>();
        foreach(var node in Model!.Document.Runtime.Nodes) {
            rows.Add(new(node.Id,null));
            if(!collapsedEffects.Contains(node.Id))
                rows.AddRange(Model.Catalog.Parameters.Where(p=>p.Binding.Node==node.Id&&Model.TrackVisible(p.Id)).Select(p=>new EffectRow(node.Id,p.Id)));
        }
        float top=0;foreach(var row in rows){row.Top=top;row.Height=(row.Parameter==null?31:ChannelHeight(row.Parameter))*UiScale;top+=row.Height;}
        return rows;
    }
    void DrawTimeline(Rect rect)
    {
        var theme=ui!;float s=UiScale,names=205*s,ruler=35*s;
        var rows=TimelineRows();var hoverResize=ResizeRow(rows,Event.current.mousePosition);
        if(Event.current.type==EventType.Repaint) {
            if(curveSamples.Length!=97||curveSamples[0].Length!=Model!.Values.Length)curveSamples=Enumerable.Range(0,97).Select(_=>new ParameterValue[Model!.Values.Length]).ToArray();
            for(int n=0;n<=96;n++)Model!.SampleCurve(timelineStart+timelineSpan*n/96,curveSamples[n]);
        }
        StudioUiTheme.Fill(rect,"#0c202b");theme.Rule(new Rect(rect.x,rect.y,rect.width,1));
        timelineRuler=new Rect(rect.x+names,rect.y+35*s,rect.width-names-20*s,ruler);
        timelineBody=new Rect(rect.x,rect.y+70*s,rect.width-16*s,rect.height-78*s);
        theme.Label(new Rect(12*s,rect.y+3*s,165*s,28*s),"效果与关键帧",18,"#c9e9ee");
        if(theme.Button(new Rect(207*s,rect.y+4*s,32*s,26*s),"−",false,false))timelineSpan=Math.Min(600,timelineSpan*1.5);
        if(theme.Button(new Rect(245*s,rect.y+4*s,32*s,26*s),"+",false,false))timelineSpan=Math.Max(.1,timelineSpan/1.5);
        if(theme.Button(new Rect(284*s,rect.y+4*s,65*s,26*s),"跟随",false,false))timelineStart=Math.Max(0,CurrentSeconds-timelineSpan*.2);
        float helpX=366;
        if(Model!.MissingAnimationStarts>0) {
            if(theme.Button(new Rect(359*s,rect.y+4*s,116*s,26*s),"补齐起点",false,true,13))
                Queue(()=>{int count=Model!.RepairAnimationStarts();status=$"已补齐 {count} 条动画起点，可撤销；保存才写入。";});
            helpX=490;
        }
        theme.Label(new Rect(helpX*s,rect.y+4*s,Mathf.Max(0,rect.width-(helpX+10)*s),26*s),"拖分隔线调轨高 · 拖关键帧调时刻/数值 · 紫色柄调曲线",12,"#7c9dad");
        double step=NiceStep(timelineSpan/10);
        for(double t=Math.Ceiling(timelineStart/step)*step;t<=timelineStart+timelineSpan;t+=step) {
            float x=TimeX(t);theme.Label(new Rect(x+3,timelineRuler.y,75*s,ruler),t.ToString("0.###",CI),12,"#7c9dad",true);
            StudioUiTheme.Fill(new Rect(x,timelineRuler.yMax,1,timelineBody.height),"#203b48");
        }
        float total=rows.Count==0?0:rows[^1].Top+rows[^1].Height;
        trackScroll=Mathf.Clamp(trackScroll,0,Mathf.Max(0,total-timelineBody.height));
        if(total>timelineBody.height)trackScroll=GUI.VerticalScrollbar(new Rect(rect.xMax-12*s,timelineBody.y,10*s,timelineBody.height),trackScroll,timelineBody.height,0,total);
        GUI.BeginGroup(timelineBody);
        try {
            foreach(var item in rows) {
                float y=item.Top-trackScroll,row=item.Height;if(y+row<0||y>timelineBody.height)continue;
                var node=Model.RenderDocument.Nodes.Single(n=>n.Id==item.Node);
                if(item.Parameter==null) {
                    StudioUiTheme.Fill(new Rect(0,y,rect.width,row),selectedEffect==node.Id&&selectedParameter.Length==0?"#23424e":"#122d38");
                    theme.Label(new Rect(8*s,y,20*s,row),collapsedEffects.Contains(node.Id)?"▸":"▾",13);
                    theme.Label(new Rect(30*s,y,names-55*s,row),theme.Fit(Model.NodeLabel(node),names-55*s),13,node.Enabled?"#c9e9ee":"#65828d");
                    theme.Label(new Rect(names-22*s,y,20*s,row),node.Enabled?"●":"○",12);
                    float left=Mathf.Max(names,TimeX(node.StartSeconds)-rect.x);
                    float right=node.EndSeconds.HasValue?TimeX(node.EndSeconds.Value)-rect.x:rect.width-12*s;
                    right=Mathf.Min(rect.width-12*s,right);
                    if(right>left) {
                        var clip=new Rect(left,y+4*s,right-left,row-8*s);StudioUiTheme.Fill(clip,node.Enabled?"#426576":"#243c46");
                        if(right-left>28*s)theme.Label(new Rect(left+8*s,y,right-left-12*s,row),theme.Fit(node.Target=="background"?"背景":Model.NodeLabel(node),right-left-12*s,12),12,"#d3edf0");
                        StudioUiTheme.Fill(new Rect(right-3*s,y+6*s,2*s,row-12*s),"#91bac4");
                    }
                }else {
                    var p=Model.Catalog.Get(item.Parameter);
                    if(p.Id==selectedParameter)StudioUiTheme.Fill(new Rect(0,y,rect.width,row),"#173743");
                    theme.Label(new Rect(30*s,y,names-40*s,row),theme.Fit(p.Label,names-40*s,12),12);
                    var plot=new Rect(names,y+7*s,timelineRuler.width,row-16*s);
                    if(Event.current.type==EventType.Repaint) {
                        DrawParameterCurve(p,plot);
                        if(p.Id==selectedParameter)DrawCurveHandles(item,plot);
                    }
                    var range=PlotRange(p.Id);
                    if(row>=60*s){theme.Label(new Rect(30*s,y+29*s,72*s,19*s),range.High.ToString("0.###",CI),10,"#6b8e9b",true);theme.Label(new Rect(110*s,y+29*s,80*s,19*s),range.Low.ToString("0.###",CI),10,"#6b8e9b",true);}
                    bool resizeHover=resizingChannel&&resizedChannel==p.Id||hoverResize?.Parameter==p.Id;
                    StudioUiTheme.Fill(new Rect(0,y+row-8*s,timelineBody.width,16*s),resizeHover?"#62b5c22e":"#00000000");
                    StudioUiTheme.Fill(new Rect(0,y+row-2*s,timelineBody.width,resizeHover?3*s:1),resizeHover?"#a5e5eb":"#385565");
                    theme.Label(new Rect(34*s,y+row-24*s,150*s,20*s),resizeHover?"↕ 拖动调高度 · 双击恢复":"↕",11,resizeHover?"#d1f5f7":"#7295a2");
                    foreach(var key in Model.Keys.Where(k=>k.ParameterId==p.Id)) {
                        double seconds=key.Id==selectedKey&&Model.MovingSeconds.HasValue?Model.MovingSeconds.Value:key.Seconds;
                        if(draggingEffect&&item.Node==draggedEffect&&!resizingEffect)seconds+=node.StartSeconds-effectDragStart;
                        float x=TimeX(seconds)-rect.x;if(x<names||x>rect.width)continue;
                        float value=Component(key.Id==selectedKey&&Model.EditedValue.HasValue?Model.EditedValue.Value.Number:key.Value.Number,PlotComponent(p.Id));
                        DrawDiamond(new Vector2(x,ValueY(p.Id,value,plot)),key.Id==selectedKey?8*s:6*s,key.Id==selectedKey?"#d8faff":"#82cfd6");
                    }
                }
                theme.Rule(new Rect(0,y+row-1,rect.width,1));
            }
            float head=TimeX(CurrentSeconds)-rect.x;
            if(head>=names&&head<rect.width)StudioUiTheme.Fill(new Rect(head,0,2*s,timelineBody.height),"#b4efef");
        }finally{GUI.EndGroup();}
    }
    void DrawParameterCurve(ParameterDescriptor p,Rect area)
    {
        int index=Model!.Catalog.Parameters.ToList().FindIndex(v=>v.Id==p.Id);
        var range=PlotRange(p.Id);int component=PlotComponent(p.Id);
        Vector2 previous=default;
        for(int n=0;n<=96;n++) {
            var point=new Vector2(area.x+area.width*n/96,area.yMax-(float)((Component(curveSamples[n][index].Number,component)-range.Low)/(range.High-range.Low))*area.height);
            point.y=Mathf.Clamp(point.y,area.y,area.yMax);
            if(n>0)DrawCurveLine(previous,point);previous=point;
        }
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
    static void DrawCurveLine(Vector2 from,Vector2 to,string color="#79b1bf")
    {
        var matrix=GUI.matrix;float length=(to-from).magnitude;
        try{GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg,from);StudioUiTheme.Fill(new Rect(from.x,from.y,length,1),color);}
        finally{GUI.matrix=matrix;}
    }
    void CancelEffectDrag()
    {
        if(draggingEffect||draggingKey||draggingCurve||libraryCandidate!=null){Model?.Cancel();GUIUtility.hotControl=0;}
        if(resizingChannel)channelHeights[resizedChannel]=resizeStartHeight;
        resizingChannel=draggingCurve=draggingKey=false;frozenPlot="";GUIUtility.hotControl=0;
        draggingEffect=false;libraryCandidate=null;libraryDragging=false;
    }
    void TimelineInput(List<EffectRow> rows,float row,float names)
    {
        if(libraryCandidate!=null)return;
        var e=Event.current;var point=e.mousePosition;
        if(e.type==EventType.ScrollWheel&&(timelineBody.Contains(point)||timelineRuler.Contains(point))) {
            if(e.control||e.command) {double at=XTime(point.x),fraction=(point.x-timelineRuler.x)/timelineRuler.width;
                timelineSpan=Math.Clamp(timelineSpan*Math.Pow(1.15,e.delta.y),.1,600);timelineStart=Math.Max(0,at-fraction*timelineSpan);}
            else if(e.shift)timelineStart=Math.Max(0,timelineStart+e.delta.y*timelineSpan*.05);else trackScroll+=e.delta.y*row;
            e.Use();return;
        }
        if(CurvePointer(e,rows))return;
        if(e.type==EventType.MouseDown&&e.button==0&&timelineRuler.Contains(point)) {
            FinishText();Model!.Commit();Bridge.SeekSeconds(XTime(point.x));GUI.FocusControl(null);e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==0&&timelineBody.Contains(point)) {
            FinishText();Model!.Commit();GUI.FocusControl(null);
            int index=RowAt(rows,point.y);if(index<0||index>=rows.Count)return;
            var item=rows[index];var node=Model.Document.Runtime.Nodes.Single(n=>n.Id==item.Node);selectedEffect=item.Node;rangeNode="";
            if(item.Parameter==null) {
                selectedParameter=selectedKey="";
                if(point.x<timelineBody.x+28*UiScale){if(!collapsedEffects.Add(node.Id))collapsedEffects.Remove(node.Id);}
                else if(point.x<timelineRuler.x&&point.x>timelineRuler.x-23*UiScale)Queue(()=>Model.EnableEffect(node.Id,!node.Enabled));
                else if(point.x>=timelineRuler.x) {
                    if(node.Target=="postprocess"&&XTime(point.x)>=node.StartSeconds&&(!node.EndSeconds.HasValue||XTime(point.x)<=node.EndSeconds)) {
                        draggingEffect=true;draggedEffect=node.Id;effectDragStart=node.StartSeconds;effectDragEnd=node.EndSeconds;effectDragPointer=XTime(point.x);
                        resizingEffect=node.EndSeconds.HasValue&&Math.Abs(point.x-TimeX(node.EndSeconds.Value))<8*UiScale;
                        Bridge.SeekSeconds(CurrentSeconds);GUIUtility.hotControl=timelineControl;
                    }else Bridge.SeekSeconds(XTime(point.x));
                }
            }else {
                if(selectedParameter!=item.Parameter)curveComponent=0;selectedParameter=item.Parameter;
                var key=Model.Keys.Where(k=>k.ParameterId==selectedParameter&&(new Vector2(TimeX(k.Seconds),ValueY(item.Parameter,Component(k.Value.Number,PlotComponent(item.Parameter)),CurveArea(item)))-point).magnitude<10*UiScale).OrderBy(k=>Math.Abs(TimeX(k.Seconds)-point.x)).FirstOrDefault();
                if(point.x>=timelineRuler.x&&key!=null){selectedKey=key.Id;Bridge.SeekSeconds(key.Seconds);draggingKey=true;FreezePlot(item.Parameter);GUIUtility.hotControl=timelineControl;}
                else {selectedKey="";if(point.x>=timelineRuler.x){Bridge.SeekSeconds(XTime(point.x));if(e.clickCount>1)selectedKey=Model.AddKey(selectedParameter,XTime(point.x));}}
            }
            e.Use();return;
        }
        if(draggingEffect&&e.type==EventType.MouseDrag) {
            try {
                double start=resizingEffect?effectDragStart:Math.Max(0,effectDragStart+XTime(point.x)-effectDragPointer);
                double? end=resizingEffect?Math.Max(effectDragStart+.05,XTime(point.x)):effectDragEnd.HasValue?effectDragEnd+start-effectDragStart:null;
                Model!.PreviewEffectRange(draggedEffect,start,end,!resizingEffect);
            }catch(Exception ex){Error(ex);}e.Use();
        }else if(draggingEffect&&e.rawType==EventType.MouseUp){draggingEffect=false;GUIUtility.hotControl=0;Model!.Commit();rangeNode="";e.Use();}
        else if(draggingKey&&e.type==EventType.MouseDrag) {
            try{var item=rows.Single(r=>r.Parameter==selectedParameter);Model!.MoveCurveKey(selectedKey,XTime(point.x),PlotComponent(selectedParameter),YValue(selectedParameter,point.y,CurveArea(item)));Bridge.SeekSeconds(XTime(point.x));}catch(Exception ex){Error(ex);}e.Use();
        }else if(draggingKey&&e.rawType==EventType.MouseUp){draggingKey=false;frozenPlot="";GUIUtility.hotControl=0;Model!.Commit();e.Use();}
    }
}
