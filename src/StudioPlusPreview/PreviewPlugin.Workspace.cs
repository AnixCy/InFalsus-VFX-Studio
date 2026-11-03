using InFalsus.Presentation;
using UnityEngine;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    string selectedParameter="adjust/_Exposure",selectedKey="",selectedEffect="adjust",keyTimeText="",libraryFilter="全部";
    bool autoKey=true,keyTimeDirty,draggingKey,parameterPicker;
    double timelineStart,timelineSpan=20;
    float trackScroll;
    Vector2 libraryScroll,inspectorScroll,pickerScroll;
    Rect timelineBody,timelineRuler,canvas;
    int timelineControl;
    readonly HashSet<string> collapsedEffects=new();
    EffectDefinition? libraryCandidate;
    Vector2 libraryDragOrigin;
    bool libraryDragging;
    string rangeNode="",rangeStart="",rangeEnd="",rangeName="";
    void SelectNode(string id){selectedEffect=id;selectedParameter=selectedKey="";parameterPicker=false;rangeNode="";collapsedEffects.Remove(id);}
    void AddLibraryEffect(EffectDefinition definition,double seconds)
    {
        SelectNode(Model!.AddEffect(definition,seconds));trackScroll=float.MaxValue;
        if(definition.Target=="postprocess")Bridge.SeekSeconds(Model.Document.Runtime.Nodes.Single(n=>n.Id==selectedEffect).StartSeconds);
        status=definition.Target=="background"?"已替换底图材质；可撤销。":"已添加独立效果；选择参数并打关键帧后加入时间轴。";
    }
    void DrawWorkspace()
    {
        var theme=ui!;var model=Model!;float s=UiScale,w=Screen.width/s,h=Screen.height/s;
        if(!model.Document.Runtime.Nodes.Any(n=>n.Id==selectedEffect))SelectNode(model.Document.Runtime.Nodes.First().Id);
        if(selectedKey.Length>0&&!model.Keys.Any(k=>k.Id==selectedKey)&&model.EditedKey!=selectedKey)selectedKey="";
        if(selectedParameter.Length>0&&!model.Catalog.Parameters.Any(p=>p.Id==selectedParameter))selectedParameter="";
        WorkspaceKeys();StudioUiTheme.Fill(new Rect(0,0,Screen.width,Screen.height),"#091923");
        if(theme.Button(R(12,10,100,32),"制谱视图",false,false))Queue(()=>{StopHeldPreview();CancelEffectDrag();Model!.Commit();Bridge.SetView(false);Surface?.Suspend();});
        if(theme.Button(R(124,10,70,32),"音频…"))Queue(ChooseWorkspaceAudio);
        if(theme.Button(R(202,10,70,32),"图片…"))Queue(ChooseImage);
        if(theme.Button(R(280,10,70,32),"打开"))Queue(LoadProject);
        if(theme.Button(R(358,10,70,32),Dirty?"保存 *":"保存"))Queue(SaveProject);
        if(theme.Button(R(436,10,70,32),"另存为"))Queue(SaveAs);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&model.Session.CanUndo;
        if(theme.Button(R(518,10,54,32),"撤销",false,false))Queue(model.Undo);
        GUI.enabled=enabled&&model.Session.CanRedo;
        if(theme.Button(R(578,10,54,32),"重做",false,false))Queue(model.Redo);GUI.enabled=enabled;
        bool compact=w<1020;float playX=compact?12:646,playY=compact?52:10,top=compact?94:52;
        bool playing=Bridge.Snapshot().Transport==TransportState.Running;
        if(theme.Button(R(playX,playY,70,32),playing?"暂停":"播放",playing))Queue(Bridge.TogglePlayback);
        theme.Label(R(playX+80,playY,115,32),CurrentSeconds.ToString("0.000",CI)+" s",20,"#d3edf0",true,2);
        if(theme.Button(R(playX+208,playY,120,32),"自动关键帧",autoKey,false))autoKey=!autoKey;
        theme.Rule(R(0,top,w,1));
        float left=Mathf.Clamp(w*.18f,180,240),right=Mathf.Clamp(w*.28f,250,320),timelineHeight=Mathf.Clamp(h*.36f,240,400),bottom=h-timelineHeight;
        DrawLibrary(R(0,top+1,left,bottom-top-1));
        canvas=R(left+12,top+13,w-left-right-24,bottom-top-25);StudioUiTheme.Fill(canvas,"#000000");
        if(Event.current.type==EventType.Repaint&&Surface!=null) {
            try{var texture=Surface.Render(model,Bridge.Snapshot(),Mathf.RoundToInt(canvas.width),Mathf.RoundToInt(canvas.height));
                GUI.DrawTexture(canvas,texture,ScaleMode.ScaleToFit,false);RenderedFrames++;}
            catch(Exception e){Error(e);}
        }
        DrawInspector(R(w-right,top+1,right,bottom-top-1));DrawTimeline(R(0,bottom,w,timelineHeight));HandleLibraryDrop();
        if(sliderEditing&&Event.current.rawType==EventType.MouseUp){sliderEditing=false;Queue(()=>model.Commit());}
        if((textParameter!=null||keyTimeDirty)&&!OwnsText&&GUIUtility.hotControl==0&&!textCommitQueued){textCommitQueued=true;Queue(FinishText);}
    }
    void ChooseWorkspaceAudio()
    {
        string path=openDialog("选择本地音频","wav,ogg,mp3");if(path.Length==0)return;
        Bridge.LoadAudio(path);audioPath=Path.GetFullPath(path);status="已选择音频，正在读取；工程只引用路径。";
    }
    void ImportCustomEffect()
    {
        string file=openDialog("导入自定义效果描述 effect.json","json");if(file.Length==0)return;
        var definition=Model!.Library.Import(file,folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString());
        libraryFilter="自定义";status="已导入“"+definition.Label+"”；拖到时间轴添加实例。";
    }
    void OpenCustomTemplates()
    {
        string directory=Path.GetFullPath(Path.Combine(folder,"../../..","CustomEffects"));
        if(!Directory.Exists(directory))throw new DirectoryNotFoundException("测试包缺少 CustomEffects 模板目录。");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory){UseShellExecute=true});
    }
    string Category(EffectDefinition d)=>d.Original?"官谱":d.Category=="自定义"?"自定义":"通用";
    void DrawLibrary(Rect rect)
    {
        var theme=ui!;GUI.BeginGroup(rect);
        try {
            float w=rect.width/UiScale;theme.Panel(new Rect(0,0,rect.width,rect.height));theme.Label(R(16,10,w-32,30),"效果库",20,"#c9e9ee");
            if(theme.Button(R(12,48,w-24,29),"导入自定义效果…",false,false,14))Queue(ImportCustomEffect);
            if(theme.Button(R(12,82,w-24,26),"程序化效果模板 / 构建",false,false,13))Queue(OpenCustomTemplates);
            var filters=new[]{"全部","通用","官谱","自定义"};
            for(int i=0;i<4;i++)if(theme.Button(R(10+i*(w-20)/4,118,(w-24)/4,27),filters[i],libraryFilter==filters[i],false,12))libraryFilter=filters[i];
            var definitions=Model!.Library.Effects.Where(d=>d.Id!="utility"&&(libraryFilter=="全部"||Category(d)==libraryFilter)).ToArray();
            libraryScroll=GUI.BeginScrollView(R(10,157,w-20,Mathf.Max(40,rect.height/UiScale-171)),libraryScroll,R(0,0,w-36,definitions.Length*55));
            try {
                for(int i=0;i<definitions.Length;i++) {
                    var definition=definitions[i];var item=R(0,i*55,w-38,48);StudioUiTheme.Fill(item,libraryCandidate==definition?"#244955":"#102a36");
                    int cardControl=GUIUtility.GetControlID(0x56464c+definition.Id.GetHashCode(),FocusType.Passive,item);
                    theme.Label(R(8,i*55+3,w-82,25),definition.Label,13,"#c9e9ee");
                    theme.Label(R(8,i*55+27,w-82,17),definition.Target=="background"?"底图材质 · 替换背景":"效果 · 拖入时间轴",11,"#7c9dad");
                    if(theme.Button(R(w-68,i*55+8,27,30),"+",false,false,18))Queue(()=>AddLibraryEffect(definition,CurrentSeconds));
                    var e=Event.current;
                    if(e.type==EventType.MouseDown&&e.button==0&&new Rect(item.x,item.y,item.width-31*UiScale,item.height).Contains(e.mousePosition)) {
                        FinishText();Model.Commit();GUI.FocusControl(null);
                        if(e.clickCount>1)Queue(()=>AddLibraryEffect(definition,CurrentSeconds));
                        else {libraryCandidate=definition;libraryDragging=false;libraryDragOrigin=GUIUtility.GUIToScreenPoint(e.mousePosition);
                            GUIUtility.hotControl=cardControl;}
                        e.Use();
                    }
                }
            }finally{GUI.EndScrollView();}
        }finally{GUI.EndGroup();}
    }
    void HandleLibraryDrop()
    {
        if(libraryCandidate==null)return;var e=Event.current;
        if(e.type==EventType.MouseDrag&&(GUIUtility.GUIToScreenPoint(e.mousePosition)-libraryDragOrigin).magnitude>5*UiScale){libraryDragging=true;e.Use();}
        if(libraryDragging) {
            var p=e.mousePosition;var box=new Rect(p.x+12*UiScale,p.y+10*UiScale,210*UiScale,32*UiScale);
            StudioUiTheme.Fill(box,"#244955");ui!.Label(box,libraryCandidate.Label,13,"#d8faff");
        }
        if(e.rawType!=EventType.MouseUp)return;
        var candidate=libraryCandidate;bool drop=libraryDragging&&(timelineBody.Contains(e.mousePosition)||timelineRuler.Contains(e.mousePosition)||candidate.Target=="background"&&canvas.Contains(e.mousePosition));
        double seconds=e.mousePosition.x>=timelineRuler.x?XTime(e.mousePosition.x):CurrentSeconds;
        libraryCandidate=null;libraryDragging=false;GUIUtility.hotControl=0;
        if(drop)Queue(()=>AddLibraryEffect(candidate,seconds));e.Use();
    }
}
