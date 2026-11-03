using System.Globalization;
using InFalsus.Presentation;
using UnityEngine;
using NVector4 = System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    Rect button;
    bool sliderEditing, textCommitQueued;
    readonly Dictionary<string,string> drafts = new();
    string? textParameter, textKey;
    int textComponent;
    StudioUiTheme? ui;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    float UiScale => ui?.Scale??1;
    Rect R(float x,float y,float width,float height) => new(x*UiScale,y*UiScale,width*UiScale,height*UiScale);
    bool OwnsText => GUI.GetNameOfFocusedControl().StartsWith("vfx_",StringComparison.Ordinal);
    internal bool Captures(Vector2 point) => Model != null && (Bridge.AuthoringView || button.Contains(point));
    void DrawEditor()
    {
        if(Model==null)return;
        if(ui!=null&&!ui.HasLiveHost)ui=null;
        ui??=StudioUiTheme.TryBind();if(ui==null)return;
        var previous=GUI.skin;
        try {
            ui.Use();
            ui.LayoutScale=WorkspaceLayout.ScaleFor(Screen.width,Screen.height,ui.BaseScale);
            if(decision!=PendingDecision.None){DrawDecision();return;}
            if(stopped||Bridge.Exporting||actionRunning||quitPermit)return;
            if(Bridge.AuthoringView){DrawWorkspace();return;}
            float s=UiScale,layout=Mathf.Min(Screen.width/2560f,Screen.height/1351f);
            var viewport=Bridge.Owner!.ViewCamera.pixelRect;
            button=new Rect(Mathf.Clamp(viewport.xMax-Mathf.Max(52,104*layout)-112*s,12,Screen.width-110*s),
                Mathf.Max(12,Screen.height-viewport.yMax+205*layout+8*s),100*s,32*s);
            if(ui.Button(button,"演出视图",false,false))Queue(()=>{Model.Commit();Bridge.SetView(true);});
        }finally{GUI.skin=previous;}
    }
    void Scalar(string id,string label,int component,float value,float min,float max,float y,float width)
    {
        string key=id+":"+component;
        ui!.Label(R(0,y,width-104,27),ui.Fit(label,(width-104)*UiScale,14),14);
        string control="vfx_number_"+key;
        bool focused=GUI.GetNameOfFocusedControl()==control;
        string current=focused&&drafts.TryGetValue(key,out var draft)?draft:value.ToString("0.###",CI);
        string next=ui.Number(R(width-96,y,94,27),control,current);
        if(next!=current) {
            drafts[key]=next;textParameter=id;textComponent=component;textKey=key;textCommitQueued=false;
            if(float.TryParse(next,NumberStyles.Float,CI,out var n)&&float.IsFinite(n))Change(id,component,n);
        }
        float start=Mathf.Clamp(value,min,max);
        float slide=GUI.HorizontalSlider(R(0,y+33,width-3,14),start,min,max);
        if(slide!=start){FinishText();Change(id,component,slide);sliderEditing=true;}
        if(focused&&Event.current.type==EventType.KeyDown) {
            if(Event.current.keyCode==KeyCode.Return||Event.current.keyCode==KeyCode.KeypadEnter) {
                try{FinishText();status="参数已应用。";GUI.FocusControl(null);}catch(Exception e){Error(e);}
                Event.current.Use();
            }else if(Event.current.keyCode==KeyCode.Escape) {
                Model!.Cancel();drafts.Clear();textParameter=textKey=null;textCommitQueued=false;
                GUI.FocusControl(null);Event.current.Use();
            }
        }
    }
    void Change(string id,int component,float value)
    {
        var model=Model!;model.Sample(CurrentSeconds);
        if(Bridge.AuthoringView&&selectedParameter!=id){selectedParameter=id;if(!model.Keys.Any(k=>k.Id==selectedKey&&k.ParameterId==id))selectedKey="";curveComponent=0;}
        int index=0;while(model.Catalog.Parameters[index].Id!=id)index++;
        var v=model.Values[index].Number;
        if(Bridge.AuthoringView) {
            var key=model.Keys.FirstOrDefault(k=>k.Id==selectedKey&&k.ParameterId==id);
            if(key!=null)v=model.EditedKey==selectedKey&&model.EditedValue.HasValue?model.EditedValue.Value.Number:key.Value.Number;
        }
        if(component==0)v.X=value;else if(component==1)v.Y=value;else if(component==2)v.Z=value;else v.W=value;
        if(Bridge.AuthoringView) {
            string key=model.UpdateAnimatedParameter(id,new ParameterValue(v),CurrentSeconds,selectedKey,autoKey);
            if(key.Length>0)selectedKey=key;
        }else model.UpdateParameter(id,new ParameterValue(v));
    }
    void FinishText()
    {
        if(keyTimeDirty) {
            if(!double.TryParse(keyTimeText,NumberStyles.Float,CI,out var seconds)||!double.IsFinite(seconds)||seconds<0)
                throw new FormatException("关键帧时间须为非负秒数。");
            Model!.MoveKey(selectedKey,seconds);Model.Commit();keyTimeDirty=false;
        }
        if(textParameter==null||textKey==null)return;
        if(!drafts.TryGetValue(textKey,out var text)||!float.TryParse(text,NumberStyles.Float,CI,out var number)||!float.IsFinite(number))
            throw new FormatException("请输入有限数值；按 Esc 可取消输入。");
        Change(textParameter,textComponent,number);Model!.Commit();textParameter=textKey=null;textCommitQueued=false;
    }
    void ReleaseStyle(){ui=null;}
}
