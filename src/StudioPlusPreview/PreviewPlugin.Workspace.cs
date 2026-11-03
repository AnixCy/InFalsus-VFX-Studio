using InFalsus.Presentation;
using UnityEngine;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    string librarySearch="";
    string selectedParameter="adjust/_Exposure",selectedKey="",selectedEffect="adjust",keyTimeText="",libraryFilter="效果",sidebar="效果库";
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
    float LibraryListTop=>libraryFilter=="自定义"?(sourceWorkspace==null?(sourceAdvanced?292:246):sourceAdvanced?352:304):128;
    void SelectNode(string id){selectedEffect=id;selectedParameter=selectedKey="";parameterPicker=false;rangeNode="";collapsedEffects.Remove(id);
        var node=Model?.Document.Runtime.Nodes.FirstOrDefault(n=>n.Id==id);if(node!=null){var directory=Model!.Definition(node).SourceDirectory;
            if(directory.Length>0&&sourceWorkspace?.Root!=directory)try{UseSource(SourceWorkspace.Open(directory));}catch(Exception e){sourceMessage=e.Message;}}}
    void AddLibraryEffect(EffectDefinition definition,double seconds,int index=-1)
    {
        SelectNode(Model!.AddEffect(definition,seconds,index));trackScroll=float.MaxValue;
        if(definition.BackgroundSource.Length>0&&definition.StrengthProperty.Length>0)selectedParameter=selectedEffect+"/"+definition.StrengthProperty;
        if(definition.Target=="postprocess")Bridge.SeekSeconds(Model.Document.Runtime.Nodes.Single(n=>n.Id==selectedEffect).StartSeconds);
        status=definition.BackgroundSource.Length>0?"已添加底图效果；组合冲突会在预览中提示。":definition.Target=="background"?"已替换底图材质；可撤销。":"已添加独立效果；选择参数并打关键帧后加入时间轴。";
    }
    void DrawWorkspace()
    {
        var theme=ui!;var model=Model!;float s=UiScale,w=Screen.width/s,h=Screen.height/s;
        var layout=WorkspaceLayout.Calculate(Screen.width,Screen.height,s);
        if(!model.Document.Runtime.Nodes.Any(n=>n.Id==selectedEffect))SelectNode(model.Document.Runtime.Nodes.First().Id);
        if(selectedKey.Length>0&&!model.Keys.Any(k=>k.Id==selectedKey)&&model.EditedKey!=selectedKey)selectedKey="";
        if(selectedParameter.Length>0&&!model.Catalog.Parameters.Any(p=>p.Id==selectedParameter))selectedParameter="";
        StudioUiTheme.Fill(new Rect(0,0,Screen.width,Screen.height),"#091923");
        if(theme.Button(R(12,10,100,32),"制谱视图",false,false))Queue(()=>{StopHeldPreview();CancelEffectDrag();Model!.Commit();Bridge.SetView(false);Surface?.Suspend();});
        if(theme.Button(R(124,10,70,32),"音频…"))Queue(ChooseWorkspaceAudio);
        if(theme.Button(R(202,10,70,32),"背景")) {sidebar="背景";SelectNode("background");}
        if(theme.Button(R(280,10,70,32),"打开"))Queue(LoadProject);
        if(theme.Button(R(358,10,70,32),Dirty?"保存 *":"保存"))Queue(SaveProject);
        if(theme.Button(R(436,10,70,32),"另存为"))Queue(SaveAs);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&model.Session.CanUndo;
        if(theme.Button(R(518,10,54,32),"撤销",false,false))Queue(model.Undo);
        GUI.enabled=enabled&&model.Session.CanRedo;
        if(theme.Button(R(578,10,54,32),"重做",false,false))Queue(model.Redo);GUI.enabled=enabled;
        bool compact=layout.Compact;float playX=compact?12:646,playY=compact?52:10,top=layout.Top;
        if(theme.Button(R(w-116,compact?52:10,104,32),"导出 addon"))Queue(BeginAddonExport);
        bool playing=Bridge.Snapshot().Transport==TransportState.Running;
        if(theme.Button(R(playX,playY,70,32),playing?"暂停":"播放",playing))Queue(ToggleWorkspacePlayback);
        theme.Label(R(playX+80,playY,115,32),CurrentSeconds.ToString("0.000",CI)+" s",20,"#d3edf0",true,2);
        if(theme.Button(R(playX+208,playY,120,32),"自动关键帧",autoKey,false))autoKey=!autoKey;
        theme.Rule(R(0,top,w,1));
        DrawLibrary(layout.Library);
        var stage=layout.Stage;
        float stageHeight=Mathf.Min(stage.height,stage.width*9/16);canvas=new Rect(stage.center.x-stageHeight*16/9/2,stage.center.y-stageHeight/2,stageHeight*16/9,stageHeight);
        StudioUiTheme.Fill(stage,"#061119");StudioUiTheme.Fill(canvas,"#000000");
        if(Event.current.type==EventType.Repaint&&Surface!=null) {
            try{var texture=Surface.Render(model,Bridge.Snapshot(),Mathf.RoundToInt(canvas.width),Mathf.RoundToInt(canvas.height));
                GUI.DrawTexture(canvas,texture,ScaleMode.ScaleToFit,false);RenderedFrames++;
                if(Surface.BackgroundGroupConflicts>0){var hint=new Rect(canvas.x,canvas.yMax-28*s,canvas.width,28*s);StudioUiTheme.Fill(hint,"#382c28dd");theme.Label(hint,"不同底图效果组重叠：以下方组为准",12,"#f2d4b1",false,2);}
                if(imagePath.Length==0&&Surface.BackgroundControlPasses>0&&!model.Definition(model.Document.Runtime.Nodes.Single(n=>n.Target=="background")).Original){StudioUiTheme.Fill(canvas,"#091923");theme.Label(canvas,"请先在背景栏选择图片",16,"#94b6c3",false,2);}}
            catch(Exception e){Error(e);}
        }
        DrawInspector(layout.Inspector);DrawTimeline(layout.Timeline);HandleLibraryDrop();
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
    string Category(EffectDefinition d)=>d.Category=="预设"?"预设":d.Category=="自定义"?"自定义":"效果";
    void DrawLibrary(Rect rect)
    {
        var theme=ui!;GUI.BeginGroup(rect);
        try {
            float w=rect.width/UiScale;theme.Panel(new Rect(0,0,rect.width,rect.height));
            if(theme.Button(R(12,9,(w-28)/2,30),"效果库",sidebar=="效果库",false,14))sidebar="效果库";
            if(theme.Button(R(16+(w-28)/2,9,(w-28)/2,30),"背景",sidebar=="背景",false,14)){sidebar="背景";SelectNode("background");}
            if(sidebar=="背景"){DrawBackgroundSettings(w,rect.height/UiScale);return;}
            var filters=new[]{"全部","效果","预设","自定义"};
            for(int i=0;i<4;i++)if(theme.Button(R(10+i*(w-20)/4,51,(w-24)/4,27),filters[i],libraryFilter==filters[i],false,12))libraryFilter=filters[i];
            if(libraryFilter=="自定义")DrawSourceCard(w);
            else{librarySearch=theme.Text(R(12,88,w-24,28),"vfx_library_search",librarySearch);if(librarySearch.Length==0)theme.Label(R(19,88,w-38,28),"搜索效果…",12,"#668795");}
            var available=Model!.Library.Effects.Where(d=>(d.Target=="postprocess"||d.Category=="自定义"&&d.Target=="background")&&d.Id!="utility");
            var definitions=available.Where(d=>d.SourceDirectory.Length==0||ReferenceEquals(d,available.Last(v=>string.Equals(v.SourceDirectory,d.SourceDirectory,StringComparison.OrdinalIgnoreCase))))
                .Where(d=>libraryFilter=="全部"||Category(d)==libraryFilter)
                .Where(d=>libraryFilter=="自定义"||librarySearch.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).All(q=>(d.Label+" "+d.Id).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0))
                .OrderBy(d=>d.Id=="bg-control-ephemeralamount"?0:d.Id=="bg-control-feedbackamount"?1:d.BackgroundSource.Length>0?2:3).ThenBy(d=>d.Label,StringComparer.OrdinalIgnoreCase).ToArray();
            if(Event.current.type==EventType.Repaint)libraryHits.Clear();
            if(rect.height/UiScale-LibraryListTop-14<30)return;
            libraryScroll=GUI.BeginScrollView(R(10,LibraryListTop,w-20,Mathf.Max(40,rect.height/UiScale-LibraryListTop-14)),libraryScroll,R(0,0,w-36,definitions.Length*55));
            try {
                for(int i=0;i<definitions.Length;i++) {
                    var definition=definitions[i];var item=R(0,i*55,w-38,48);StudioUiTheme.Fill(item,libraryCandidate==definition?"#244955":"#102a36");
                    int cardControl=GUIUtility.GetControlID(0x56464c+definition.Id.GetHashCode(),FocusType.Passive,item);
                    theme.Label(R(8,i*55+3,w-82,25),theme.Fit(definition.Label,(w-82)*UiScale),13,"#c9e9ee");
                    theme.Label(R(8,i*55+27,w-82,17),definition.BackgroundSource.Length>0?"底图效果 · 拖入时间轴":"效果 · 拖入时间轴",11,"#7c9dad");
                    if(theme.Button(R(w-68,i*55+8,27,30),"+",false,false,18))Queue(()=>AddLibraryEffect(definition,CurrentSeconds));
                    if(Event.current.type==EventType.Repaint)RegisterLibraryHit(definition,item,cardControl,rect,w);
                    var e=Event.current;
                    if(e.type==EventType.MouseDown&&e.button==0&&new Rect(item.x,item.y,item.width-31*UiScale,item.height).Contains(e.mousePosition)) {
                        FinishText();Model.Commit();GUI.FocusControl(null);
                        if(e.clickCount>1)Queue(()=>AddLibraryEffect(definition,CurrentSeconds));
                        else {libraryCandidate=definition;libraryDragging=false;libraryDragOrigin=dragPointer;
                            GUIUtility.hotControl=cardControl;}
                        e.Use();
                    }
                }
            }finally{GUI.EndScrollView();}
        }finally{GUI.EndGroup();}
    }
    void HandleLibraryDrop()
    {
        DrawDragFeedback();
    }
}
