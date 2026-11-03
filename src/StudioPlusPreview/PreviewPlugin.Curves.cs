using InFalsus.Presentation;
using Newtonsoft.Json;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    readonly Dictionary<string,float> channelHeights=new();
    bool resizingChannel,draggingCurve;
    string resizedChannel="",curveKey="";
    float resizeStartHeight,resizeStartY;
    int curveHandle,curveComponent;
    BezierCurve liveCurve;
    string frozenPlot="";double frozenLow,frozenHigh;
    const float DefaultChannelHeight=76;
    string TimelinePreferences=>Path.Combine(Environment.GetEnvironmentVariable("VFX_WORK_ROOT")??Path.GetFullPath(Path.Combine(folder,"../../../UserData/vfx")),"timeline-layout.json");
    void LoadTimelinePreferences()
    {
        try{if(File.Exists(TimelinePreferences))foreach(var pair in JsonConvert.DeserializeObject<Dictionary<string,float>>(File.ReadAllText(TimelinePreferences))??new())
            if(float.IsFinite(pair.Value))channelHeights[pair.Key]=Math.Clamp(pair.Value,44,420);}catch(Exception e){Logger.LogWarning("Timeline layout ignored: "+e.Message);}
    }
    void SaveTimelinePreferences(){Directory.CreateDirectory(Path.GetDirectoryName(TimelinePreferences)!);File.WriteAllText(TimelinePreferences,JsonConvert.SerializeObject(channelHeights.Take(512).ToDictionary(p=>p.Key,p=>p.Value)));}
    float ChannelHeight(string id)=>channelHeights.TryGetValue(id,out var height)?height:DefaultChannelHeight;
    int RowAt(List<EffectRow> rows,float screenY){float y=screenY-timelineBody.y+trackScroll;return rows.FindIndex(r=>y>=r.Top&&y<r.Top+r.Height);}
    Rect CurveArea(EffectRow row)=>new(timelineRuler.x,timelineBody.y+row.Top-trackScroll+7*UiScale,timelineRuler.width,row.Height-16*UiScale);
    static float Component(System.Numerics.Vector4 value,int component)=>component==0?value.X:component==1?value.Y:component==2?value.Z:value.W;
    int PlotComponent(string id)=>Model!.Catalog.Get(id).Binding.Kind==ParameterKind.Float?0:curveComponent;
    (double Low,double High) PlotRange(string id)
    {
        if(frozenPlot==id)return(frozenLow,frozenHigh);
        var keys=Model!.Keys.Where(k=>k.ParameterId==id).ToArray();var p=Model.Catalog.Get(id);int component=PlotComponent(id);
        double low=keys.Length>0?keys.Min(k=>Component(k.Value.Number,component)):p.SuggestedMin??0;
        double high=keys.Length>0?keys.Max(k=>Component(k.Value.Number,component)):p.SuggestedMax??1;
        foreach(var key in keys.Where(k=>k.Ease==Easing.Curve)) {
            var next=keys.Where(k=>k.Seconds>key.Seconds).OrderBy(k=>k.Seconds).FirstOrDefault();if(next==null)continue;
            float a=Component(key.Value.Number,component),d=Component(next.Value.Number,component)-a;
            foreach(var point in key.Curve??Array.Empty<CurvePointDocument>()){low=Math.Min(low,a+d*point.Y);high=Math.Max(high,a+d*point.Y);}
        }
        if(selectedParameter==id&&CurveSegment() is { } segment) {
            float a=Component(segment.Left.Value.Number,component),d=Component(segment.Right.Value.Number,component)-a;var curve=draggingCurve?liveCurve:BezierCurve.From(segment.Left);
            low=Math.Min(low,Math.Min(a+d*curve.A.y,a+d*curve.B.y));high=Math.Max(high,Math.Max(a+d*curve.A.y,a+d*curve.B.y));
        }
        if(high-low<.01){low-=.5;high+=.5;}double pad=(high-low)*.15;return(low-pad,high+pad);
    }
    float ValueY(string id,float value,Rect area){var range=PlotRange(id);return area.yMax-(float)((value-range.Low)/(range.High-range.Low))*area.height;}
    float YValue(string id,float y,Rect area){var range=PlotRange(id);return(float)(range.Low+(area.yMax-y)/area.height*(range.High-range.Low));}
    void FreezePlot(string id){var range=PlotRange(id);frozenLow=range.Low;frozenHigh=range.High;frozenPlot=id;}
    (EditorKey Left,EditorKey Right)? CurveSegment()
    {
        var left=Model!.Keys.FirstOrDefault(k=>k.Id==selectedKey&&k.ParameterId==selectedParameter);if(left==null||PreviewModel.IsSwitch(left.ParameterId))return null;
        var right=Model.Keys.Where(k=>k.ParameterId==left.ParameterId&&k.Seconds>left.Seconds).OrderBy(k=>k.Seconds).FirstOrDefault();
        return right==null?null:(left,right);
    }
    Vector2 HandlePosition(EditorKey left,EditorKey right,Vector2 control,Rect area)
    {
        float a=Component(left.Value.Number,PlotComponent(left.ParameterId)),b=Component(right.Value.Number,PlotComponent(right.ParameterId));
        return new Vector2(TimeX(left.Seconds+(right.Seconds-left.Seconds)*control.x),ValueY(left.ParameterId,a+(b-a)*control.y,area));
    }
    bool CurvePointer(Event e,List<EffectRow> rows)
    {
        var point=e.mousePosition;
        if(resizingChannel) {
            if(e.type==EventType.MouseDrag){channelHeights[resizedChannel]=Math.Clamp(resizeStartHeight+(point.y-resizeStartY)/UiScale,44,420);e.Use();return true;}
            if(e.rawType==EventType.MouseUp){resizingChannel=false;GUIUtility.hotControl=0;Queue(SaveTimelinePreferences);e.Use();return true;}
        }
        if(draggingCurve) {
            if(e.type==EventType.MouseDrag&&CurveSegment() is { } segment) {
                var row=rows.Single(r=>r.Parameter==segment.Left.ParameterId);var area=CurveArea(row);
                float a=Component(segment.Left.Value.Number,PlotComponent(segment.Left.ParameterId)),delta=Component(segment.Right.Value.Number,PlotComponent(segment.Right.ParameterId))-a;
                var handle=new Vector2((float)((XTime(point.x)-segment.Left.Seconds)/(segment.Right.Seconds-segment.Left.Seconds)),(YValue(segment.Left.ParameterId,point.y,area)-a)/delta);
                liveCurve=curveHandle==0?new BezierCurve(handle,liveCurve.B):new BezierCurve(liveCurve.A,handle);
                Model!.PreviewCurve(curveKey,liveCurve);e.Use();return true;
            }
            if(e.rawType==EventType.MouseUp){draggingCurve=false;frozenPlot="";GUIUtility.hotControl=0;Model!.Commit();e.Use();return true;}
        }
        if(e.type!=EventType.MouseDown||e.button!=0||!timelineBody.Contains(point))return false;
        var resize=ResizeRow(rows,point);
        if(resize!=null) {
            var target=resize;
            if(e.clickCount>1){channelHeights.Remove(target.Parameter!);Queue(SaveTimelinePreferences);}
            else{resizingChannel=true;resizedChannel=target.Parameter!;resizeStartHeight=ChannelHeight(target.Parameter!);resizeStartY=point.y;GUIUtility.hotControl=timelineControl;}
            e.Use();return true;
        }
        int index=RowAt(rows,point.y);if(index<0)return false;var item=rows[index];
        if(item.Parameter!=selectedParameter||point.x<timelineRuler.x||CurveSegment() is not { } pair)return false;
        if(Math.Abs(Component(pair.Right.Value.Number,PlotComponent(pair.Right.ParameterId))-Component(pair.Left.Value.Number,PlotComponent(pair.Left.ParameterId)))<.000001)return false;
        var curve=BezierCurve.From(pair.Left);var plot=CurveArea(item);
        for(int i=0;i<2;i++)if((HandlePosition(pair.Left,pair.Right,i==0?curve.A:curve.B,plot)-point).magnitude<9*UiScale) {
            FinishText();Model!.Commit();FreezePlot(item.Parameter);draggingCurve=true;curveKey=pair.Left.Id;curveHandle=i;liveCurve=curve;
            GUI.FocusControl(null);GUIUtility.hotControl=timelineControl;e.Use();return true;
        }
        return false;
    }
    void DrawCurveHandles(EffectRow row,Rect localArea)
    {
        if(row.Parameter!=selectedParameter||CurveSegment() is not { } pair||Math.Abs(Component(pair.Right.Value.Number,PlotComponent(pair.Right.ParameterId))-Component(pair.Left.Value.Number,PlotComponent(pair.Left.ParameterId)))<.000001)return;
        var curve=draggingCurve?liveCurve:BezierCurve.From(pair.Left);
        var area=new Rect(timelineRuler.x,localArea.y+timelineBody.y,localArea.width,localArea.height);
        Vector2 Offset(Vector2 v)=>v-new Vector2(timelineBody.x,timelineBody.y);
        var start=new Vector2(TimeX(pair.Left.Seconds),ValueY(row.Parameter!,Component(pair.Left.Value.Number,PlotComponent(pair.Left.ParameterId)),area));
        var end=new Vector2(TimeX(pair.Right.Seconds),ValueY(row.Parameter!,Component(pair.Right.Value.Number,PlotComponent(pair.Right.ParameterId)),area));
        for(int i=0;i<2;i++) {
            var handle=HandlePosition(pair.Left,pair.Right,i==0?curve.A:curve.B,area);if(handle.x<timelineRuler.x||handle.x>timelineRuler.xMax)continue;
            DrawCurveLine(Offset(i==0?start:end),Offset(handle),"#d1a5ea");DrawDiamond(Offset(handle),6*UiScale,"#e4b8fa");
        }
    }
}
