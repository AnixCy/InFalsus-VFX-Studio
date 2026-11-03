using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
internal readonly struct BezierCurve
{
    internal readonly Vector2 A,B;
    internal BezierCurve(Vector2 a,Vector2 b){A=new Vector2(Mathf.Clamp(a.x,.005f,.995f),Mathf.Clamp(a.y,-3,4));B=new Vector2(Mathf.Clamp(b.x,.005f,.995f),Mathf.Clamp(b.y,-3,4));}
    internal Vector2 Point(float t){float v=1-t;return 3*v*v*t*A+3*v*t*t*B+t*t*t*Vector2.one;}
    // A sampled parametric cubic uses the existing portable Curve contract.
    // The 1/3 and 2/3 samples also retain enough information to recover both handles.
    internal CurvePointDocument[] Samples(){var points=new CurvePointDocument[97];for(int i=0;i<=96;i++){var p=Point(i/96f);points[i]=new CurvePointDocument{X=i==0?0:i==96?1:p.x,Y=i==0?0:i==96?1:p.y};}return points;}
    internal static BezierCurve From(EditorKey key)
    {
        if(key.Ease==Easing.Curve&&key.Curve is {Length:97} points) {
            var a=new Vector2((float)points[32].X-1/27f,(float)points[32].Y-1/27f);
            var b=new Vector2((float)points[64].X-8/27f,(float)points[64].Y-8/27f);
            var candidate=new BezierCurve(3*a-1.5f*b,3*b-1.5f*a);
            var recovered=candidate.Samples();
            if(recovered.Zip(points,(x,y)=>Math.Abs(x.X-y.X)<.0001&&Math.Abs(x.Y-y.Y)<.0001).All(x=>x))return candidate;
        }
        return key.Ease switch{
            Easing.Smooth=>new(new Vector2(1/3f,0),new Vector2(2/3f,1)),
            Easing.SineInOut=>new(new Vector2(.37f,0),new Vector2(.63f,1)),
            Easing.CubicInOut=>new(new Vector2(.65f,0),new Vector2(.35f,1)),
            Easing.BackOut=>new(new Vector2(.18f,1.5f),new Vector2(.5f,1)),
            _=>new(new Vector2(1/3f,1/3f),new Vector2(2/3f,2/3f))};
    }
}
public sealed partial class PreviewModel
{
    internal void PreviewCurve(string key,BezierCurve curve)
    {
        var marker=Keys.Single(k=>k.Id==key);if(IsSwitch(marker.ParameterId))return;
        Begin("curve:"+key);drag!.SetKeyEase(key,Easing.Curve,curve.Samples());draftTimeline=drag.CompilePreview();
    }
    internal void MoveCurveKey(string key,double seconds,int component,float number)
    {
        var marker=Keys.Single(k=>k.Id==key);var value=marker.Value.Number;
        if(component==0)value.X=number;else if(component==1)value.Y=number;else if(component==2)value.Z=number;else value.W=number;
        seconds=Math.Round(ParameterTime(marker.ParameterId,seconds),3);
        Begin("curve-key:"+key);drag!.MoveKey(key,KeyPosition.Seconds(seconds));drag.SetKeyValue(key,new ParameterValue(value));
        editedKey=key;MovingSeconds=seconds;EditedValue=new ParameterValue(value);draftTimeline=drag.CompilePreview();
    }
}
