using System.Globalization;
using InFalsus.Presentation;
using UnityEngine;
using NVector4 = System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    Rect window = new(60,60,360,600), button;
    bool opened = true, positioned, sliderEditing, textCommitQueued;
    Vector2 scroll;
    readonly Dictionary<string,string> drafts = new();
    string? textParameter, textKey;
    int textComponent;
    GUIStyle? windowStyle, labelStyle, captionStyle, buttonStyle, fieldStyle, toggleStyle;
    Texture2D? panelTexture;
    Font? editorFont;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    bool OwnsText => opened && GUI.GetNameOfFocusedControl().StartsWith("vfx_number_",StringComparison.Ordinal);
    internal bool Captures(Vector2 point) => Model != null && (button.Contains(point) || opened && window.Contains(point));
    void EnsureStyle()
    {
        if (windowStyle != null) return;
        editorFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI","Microsoft YaHei","Segoe UI" },16);
        panelTexture = new Texture2D(1,1) { hideFlags = HideFlags.HideAndDontSave };
        panelTexture.SetPixel(0,0,new Color(.047f,.12f,.16f,.98f)); panelTexture.Apply();
        windowStyle = new GUIStyle(GUI.skin.window) { font=editorFont,fontSize=17,alignment=TextAnchor.UpperLeft,
            padding=new RectOffset(12,12,30,12),border=new RectOffset(1,1,1,1) };
        windowStyle.normal.background=panelTexture; windowStyle.onNormal.background=panelTexture;
        windowStyle.normal.textColor=new Color(.82f,.96f,1);
        labelStyle=new GUIStyle(GUI.skin.label){font=editorFont,fontSize=14,alignment=TextAnchor.MiddleLeft};
        labelStyle.normal.textColor=new Color(.74f,.87f,.92f);
        captionStyle=new GUIStyle(labelStyle){fontSize=12,wordWrap=true};
        buttonStyle=new GUIStyle(GUI.skin.button){font=editorFont,fontSize=13};
        toggleStyle=new GUIStyle(GUI.skin.toggle){font=editorFont,fontSize=14};
        fieldStyle=new GUIStyle(GUI.skin.textField){font=editorFont,fontSize=13,alignment=TextAnchor.MiddleRight};
    }
    void DrawEditor()
    {
        if (Model == null) return;
        EnsureStyle();
        if (decision != PendingDecision.None) { DrawDecision(); return; }
        if (stopped || Bridge.Exporting || actionRunning || quitPermit) return;
        var viewport=Bridge.Owner!.ViewCamera.pixelRect;
        button=new Rect(Mathf.Clamp(viewport.xMax-90,12,Screen.width-86),Mathf.Max(10,Screen.height-viewport.yMax+12),76,28);
        if (!positioned) { window.x=button.xMax-window.width; window.y=button.yMax+8; positioned=true; }
        window.width=Mathf.Min(360,Screen.width-24);
        window.height=Mathf.Min(620,Screen.height-90);
        window.x=Mathf.Clamp(window.x,12,Mathf.Max(12,Screen.width-window.width-12));
        window.y=Mathf.Clamp(window.y,12,Mathf.Max(12,Screen.height-window.height-12));
        if (GUI.Button(button,opened?"收起演出":"演出参数",buttonStyle)) { opened=!opened; GUI.FocusControl(null); Queue(()=>Model.Commit()); }
        if (!opened) return;
        var raw=Event.current.rawType;
        window=GUI.Window(0x564658,window,DrawWindow,Dirty?"演出参数  *":"演出参数",windowStyle);
        if (sliderEditing && raw==EventType.MouseUp) { sliderEditing=false; Queue(()=>Model.Commit()); }
        if (textParameter != null && !OwnsText && GUIUtility.hotControl==0 && !textCommitQueued) {
            textCommitQueued=true; Queue(FinishText);
        }
    }
    void DrawWindow(int id)
    {
        var m=Model!;
        if (GUI.Button(new Rect(window.width-37,5,28,22),"×",buttonStyle)) { opened=false; GUI.FocusControl(null); Queue(()=>m.Commit()); }
        float w=window.width-24;
        if (GUI.Button(new Rect(12,32,w*.32f,27),"打开工程",buttonStyle)) Queue(LoadProject);
        if (GUI.Button(new Rect(16+w*.32f,32,w*.32f,27),"保存",buttonStyle)) Queue(SaveProject);
        if (GUI.Button(new Rect(20+w*.64f,32,w*.32f-8,27),"另存为",buttonStyle)) Queue(SaveAs);
        if (GUI.Button(new Rect(12,65,w*.58f,27),"背景图片…",buttonStyle)) Queue(ChooseImage);
        if (GUI.Button(new Rect(16+w*.58f,65,w*.42f-4,27),"内置背景",buttonStyle)) Queue(()=>{
            Surface!.ApplyImage(null);imagePath="";
            m.UpdateParameter("background/_PatternMix",new ParameterValue(new NVector4(1,0,0,0)));m.Commit();
        });
        previewEnabled=GUI.Toggle(new Rect(12,98,w,22),previewEnabled,"启用背景预览",toggleStyle);
        GUI.Label(new Rect(12,123,w,22),"谱面时刻  "+CurrentSeconds.ToString("0.000",CI)+" s",captionStyle);
        bool enabled=GUI.enabled;
        GUI.enabled=m.Session.CanUndo||m.IsEditing;
        if (GUI.Button(new Rect(12,150,w*.27f,25),"撤销",buttonStyle)) Queue(m.Undo);
        GUI.enabled=m.Session.CanRedo;
        if (GUI.Button(new Rect(16+w*.27f,150,w*.27f,25),"重做",buttonStyle)) Queue(m.Redo);
        GUI.enabled=enabled;
        if (GUI.Button(new Rect(20+w*.54f,150,w*.46f-8,25),"恢复默认",buttonStyle)) Queue(m.Reset);
        float content=0;
        foreach(var p in m.Catalog.Parameters) content+=p.Binding.Kind==ParameterKind.Color?114:43;
        Rect area=new(12,184,w,Mathf.Max(70,window.height-248));
        scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,w-18,content));
        try {
            m.Sample(CurrentSeconds);
            float y=0;
            for(int i=0;i<m.Catalog.Parameters.Count;i++) {
                var p=m.Catalog.Parameters[i];var value=m.Values[i].Number;
                if(p.Binding.Kind==ParameterKind.Color) {
                    for(int c=0;c<3;c++){Scalar(p.Id,p.Label+" "+new[]{"R","G","B"}[c],c,
                        c==0?value.X:c==1?value.Y:value.Z,(float)(p.SuggestedMin??0),(float)(p.SuggestedMax??2),y,w-18);y+=38;}
                } else {Scalar(p.Id,p.Label,0,value.X,(float)(p.SuggestedMin??0),(float)(p.SuggestedMax??1),y,w-18);y+=43;}
            }
        } finally {GUI.EndScrollView();}
        GUI.Label(new Rect(12,window.height-57,w,45),status,captionStyle);
        GUI.DragWindow(new Rect(0,0,window.width-42,27));
    }
    void Scalar(string id,string label,int component,float value,float min,float max,float y,float width)
    {
        string key=id+":"+component;
        GUI.Label(new Rect(0,y,width-83,21),label,labelStyle);
        string control="vfx_number_"+key;
        bool focused=GUI.GetNameOfFocusedControl()==control;
        string current=focused&&drafts.TryGetValue(key,out var draft)?draft:value.ToString("0.###",CI);
        GUI.SetNextControlName(control);
        string next=GUI.TextField(new Rect(width-80,y,78,23),current,fieldStyle);
        if(next!=current) {
            drafts[key]=next;textParameter=id;textComponent=component;textKey=key;textCommitQueued=false;
            if(float.TryParse(next,NumberStyles.Float,CI,out var n)&&float.IsFinite(n)) Change(id,component,n);
        }
        float start=Mathf.Clamp(value,min,max);
        float slide=GUI.HorizontalSlider(new Rect(0,y+26,width-3,13),start,min,max);
        if(slide!=start) { FinishText(); Change(id,component,slide);sliderEditing=true; }
        if(focused&&Event.current.type==EventType.KeyDown) {
            if(Event.current.keyCode==KeyCode.Return||Event.current.keyCode==KeyCode.KeypadEnter) {
                try {FinishText();status="参数已应用。";GUI.FocusControl(null);}catch(Exception e){Error(e);}
                Event.current.Use();
            }else if(Event.current.keyCode==KeyCode.Escape) {
                Model!.Cancel();drafts.Clear();textParameter=textKey=null;textCommitQueued=false;GUI.FocusControl(null);Event.current.Use();
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
    void ReleaseStyle() {if(panelTexture)Destroy(panelTexture);if(editorFont)Destroy(editorFont);}
}
