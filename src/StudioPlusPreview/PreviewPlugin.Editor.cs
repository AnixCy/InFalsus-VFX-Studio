using System.Globalization;
using InFalsus.Presentation;
using UnityEngine;
using NVector4 = System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    Rect window = new(60,60,400,660), button;
    bool opened = true, positioned, sliderEditing, textCommitQueued;
    Vector2 scroll;
    readonly Dictionary<string,string> drafts = new();
    string? textParameter, textKey;
    int textComponent;
    StudioUiTheme? ui;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    float UiScale => ui!.Scale;
    Rect R(float x,float y,float width,float height) => new(x*UiScale,y*UiScale,width*UiScale,height*UiScale);
    bool OwnsText => opened && GUI.GetNameOfFocusedControl().StartsWith("vfx_number_",StringComparison.Ordinal);
    internal bool Captures(Vector2 point) => Model != null && (button.Contains(point) || opened && window.Contains(point));
    void DrawEditor()
    {
        if (Model == null) return;
        if (ui != null && !ui.HasLiveHost) ui = null;
        ui ??= StudioUiTheme.TryBind();
        if (ui == null) return;
        var previous = GUI.skin;
        try { ui.Use(); DrawThemedEditor(); }
        finally { GUI.skin = previous; }
    }
    void DrawThemedEditor()
    {
        if (decision != PendingDecision.None) { DrawDecision(); return; }
        if (stopped || Bridge.Exporting || actionRunning || quitPermit) return;
        float s=UiScale;
        var viewport=Bridge.Owner!.ViewCamera.pixelRect;
        float rail=Mathf.Max(52,104*Mathf.Min(Screen.width/2560f,Screen.height/1351f));
        button=new Rect(Mathf.Clamp(viewport.xMax-rail-112*s,12,Screen.width-110*s),
            Mathf.Max(12,Screen.height-viewport.yMax+205*Mathf.Min(Screen.width/2560f,Screen.height/1351f)+8*s),100*s,32*s);
        window.width=Mathf.Min(400*s,Screen.width-24);
        window.height=Mathf.Min(660*s,Screen.height-110*s);
        if (!positioned) { window.x=button.xMax-window.width; window.y=button.yMax+8*s; positioned=true; }
        window.x=Mathf.Clamp(window.x,12,Mathf.Max(12,Screen.width-window.width-12));
        window.y=Mathf.Clamp(window.y,12,Mathf.Max(12,Screen.height-window.height-12));
        if (ui!.Button(button,opened?"收起演出":"演出参数",opened,false)) {
            opened=!opened; GUI.FocusControl(null); Queue(()=>Model!.Commit());
        }
        if (!opened) return;
        var raw=Event.current.rawType;
        window=GUI.Window(0x564658,window,DrawWindow,GUIContent.none,GUIStyle.none);
        if (sliderEditing && raw==EventType.MouseUp) { sliderEditing=false; Queue(()=>Model!.Commit()); }
        if (textParameter != null && !OwnsText && GUIUtility.hotControl==0 && !textCommitQueued) {
            textCommitQueued=true; Queue(FinishText);
        }
    }
    void DrawWindow(int id)
    {
        var m=Model!;var theme=ui!;
        float w=window.width/UiScale,h=window.height/UiScale,body=w-40;
        theme.Panel(new Rect(0,0,window.width,window.height));
        theme.Label(R(20,10,w-78,40),Dirty?"演出参数  *":"演出参数",22,"#c9e9ee");
        if (theme.Button(R(w-52,12,32,32),"×",false,false,22)) {
            opened=false;GUI.FocusControl(null);Queue(()=>m.Commit());
        }
        theme.Rule(R(20,54,body,1));
        float col=(body-16)/3;
        if(theme.Button(R(20,67,col,32),"打开工程"))Queue(LoadProject);
        if(theme.Button(R(28+col,67,col,32),"保存"))Queue(SaveProject);
        if(theme.Button(R(36+col*2,67,col,32),"另存为"))Queue(SaveAs);
        if(theme.Button(R(20,108,body*.57f,32),"背景图片…"))Queue(ChooseImage);
        if(theme.Button(R(28+body*.57f,108,body*.43f-8,32),"内置背景"))Queue(()=>{
            Surface!.ApplyImage(null);imagePath="";
            m.UpdateParameter("background/_PatternMix",new ParameterValue(new NVector4(1,0,0,0)));m.Commit();
        });
        previewEnabled=theme.Toggle(R(20,149,body,30),"背景预览",previewEnabled);
        theme.Label(R(20,182,body*.45f,26),"谱面时刻",13,"#7c9dad");
        theme.Label(R(20+body*.45f,182,body*.55f,26),CurrentSeconds.ToString("0.000",CI)+" s",17,"#d3edf0",true,2);
        theme.Rule(R(20,214,body,1));
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&(m.Session.CanUndo||m.IsEditing);
        if(theme.Button(R(20,225,col,30),"撤销",false,false))Queue(m.Undo);
        GUI.enabled=enabled&&m.Session.CanRedo;
        if(theme.Button(R(28+col,225,col,30),"重做",false,false))Queue(m.Redo);
        GUI.enabled=enabled;
        if(theme.Button(R(36+col*2,225,col,30),"恢复默认",false,false))Queue(m.Reset);
        theme.Rule(R(20,265,body,1));
        float content=0;
        foreach(var p in m.Catalog.Parameters)content+=p.Binding.Kind==ParameterKind.Color?156:52;
        scroll=GUI.BeginScrollView(R(20,276,body,Mathf.Max(70,h-342)),scroll,R(0,0,body-16,content));
        try {
            m.Sample(CurrentSeconds);float y=0;
            for(int i=0;i<m.Catalog.Parameters.Count;i++) {
                var p=m.Catalog.Parameters[i];var value=m.Values[i].Number;
                if(p.Binding.Kind==ParameterKind.Color) {
                    for(int c=0;c<3;c++) {
                        Scalar(p.Id,p.Label+" "+"RGB"[c],c,c==0?value.X:c==1?value.Y:value.Z,
                            (float)(p.SuggestedMin??0),(float)(p.SuggestedMax??2),y,body-16);y+=52;
                    }
                }else {
                    Scalar(p.Id,p.Label,0,value.X,(float)(p.SuggestedMin??0),(float)(p.SuggestedMax??1),y,body-16);y+=52;
                }
            }
        }finally{GUI.EndScrollView();}
        theme.Rule(R(20,h-58,body,1));
        theme.Paragraph(R(20,h-49,body,43),status);
        GUI.DragWindow(R(0,0,w-58,52));
    }
    void Scalar(string id,string label,int component,float value,float min,float max,float y,float width)
    {
        string key=id+":"+component;
        ui!.Label(R(0,y,width-104,27),label);
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
        int index=0;while(model.Catalog.Parameters[index].Id!=id)index++;
        var v=model.Values[index].Number;
        if(component==0)v.X=value;else if(component==1)v.Y=value;else if(component==2)v.Z=value;else v.W=value;
        model.UpdateParameter(id,new ParameterValue(v));
    }
    void FinishText()
    {
        if(textParameter==null||textKey==null)return;
        if(!drafts.TryGetValue(textKey,out var text)||!float.TryParse(text,NumberStyles.Float,CI,out var number)||!float.IsFinite(number))
            throw new FormatException("请输入有限数值；按 Esc 可取消输入。");
        Change(textParameter,textComponent,number);Model!.Commit();textParameter=textKey=null;textCommitQueued=false;
    }
    void ReleaseStyle(){ui=null;}
}
