using InFalsus.Presentation;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    string selectedParameter="",selectedKey="",group="background",keyTimeText="";
    bool autoKey=true,keyTimeDirty,draggingKey;
    double timelineStart,timelineSpan=20;
    float trackScroll;
    Vector2 libraryScroll,inspectorScroll;
    Rect timelineBody,timelineRuler,canvas;
    int timelineControl;
    void DrawWorkspace()
    {
        var theme=ui!;var model=Model!;float s=UiScale,w=Screen.width/s,h=Screen.height/s;
        var parameters=model.Catalog.Parameters.Where(p=>p.Binding.Node==group).ToArray();
        if(selectedKey.Length>0&&!model.Keys.Any(k=>k.Id==selectedKey)&&model.EditedKey!=selectedKey)selectedKey="";
        if(!parameters.Any(p=>p.Id==selectedParameter))selectedParameter=parameters.FirstOrDefault()?.Id??"";
        WorkspaceKeys();
        StudioUiTheme.Fill(new Rect(0,0,Screen.width,Screen.height),"#091923");
        if(theme.Button(R(12,10,100,32),"制谱视图",false,false))Queue(()=>{StopHeldPreview();playbackKeysDown.Clear();Model!.Commit();Bridge.SetView(false);Surface?.Suspend();});
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
        float left=Mathf.Clamp(w*.18f,170,220),right=Mathf.Clamp(w*.28f,240,300),timelineHeight=Mathf.Clamp(h*.32f,220,360),bottom=h-timelineHeight;
        DrawLibrary(R(0,top+1,left,bottom-top-1));
        canvas=R(left+12,top+13,w-left-right-24,bottom-top-25);
        StudioUiTheme.Fill(canvas,"#000000");
        if(Event.current.type==EventType.Repaint&&Surface!=null) {
            try{var texture=Surface.Render(model,Bridge.Snapshot(),Mathf.RoundToInt(canvas.width),Mathf.RoundToInt(canvas.height));
                GUI.DrawTexture(canvas,texture,ScaleMode.ScaleToFit,false);RenderedFrames++;}
            catch(Exception e){Error(e);}
        }
        DrawInspector(R(w-right,top+1,right,bottom-top-1));
        DrawTimeline(R(0,bottom,w,timelineHeight),parameters);
        var raw=Event.current.rawType;
        if(sliderEditing&&raw==EventType.MouseUp){sliderEditing=false;Queue(()=>model.Commit());}
        if((textParameter!=null||keyTimeDirty)&&!OwnsText&&GUIUtility.hotControl==0&&!textCommitQueued){textCommitQueued=true;Queue(FinishText);}
    }
    void ChooseWorkspaceAudio()
    {
        string path=openDialog("选择本地音频","wav,ogg,mp3");if(path.Length==0)return;
        Bridge.LoadAudio(path);audioPath=Path.GetFullPath(path);status="已选择音频，正在读取；工程只引用路径。";
    }
    void DrawLibrary(Rect rect)
    {
        var theme=ui!;GUI.BeginGroup(rect);
        try {
            float w=rect.width/UiScale;
            theme.Panel(new Rect(0,0,rect.width,rect.height));theme.Label(R(16,10,w-32,30),"效果与轨道",20,"#c9e9ee");
            if(theme.Button(R(12,49,(w-32)/3,28),"背景",group=="background",false))SelectGroup("background");
            if(theme.Button(R(16+(w-32)/3,49,(w-32)/3,28),"后处理",group=="post",false))SelectGroup("post");
            if(theme.Button(R(20+(w-32)*2/3,49,(w-32)/3,28),"通用",group=="adjust",false))SelectGroup("adjust");
            theme.Rule(R(12,85,w-24,1));
            if(group=="adjust") {
                theme.Paragraph(R(16,100,w-32,140),"曝光、对比度、饱和度、缩放、旋转、色散、波动、扫描线、噪声、像素化、暗角、淡出、遮罩与光环。",14);
                return;
            }
            string target=group=="post"?"postprocess":"background";
            if(group=="background"&&theme.Button(R(12,94,w-24,28),"恢复材质底图",false,false,13))Queue(()=>{
                Surface!.ApplyImage(null);imagePath="";
                if(Model!.Catalog.Parameters.Any(p=>p.Id=="background/_PatternMix")) {
                    Model.UpdateParameter("background/_PatternMix",new ParameterValue(new System.Numerics.Vector4(1,0,0,0)));Model.Commit();
                }
            });
            var definitions=Model!.Library.Effects.Where(d=>d.Target==target&&d.Id!="utility").ToArray();
            string current=Model.Document.Runtime.Nodes.FirstOrDefault(n=>n.Id==group)?.Material??"";
            float listTop=group=="background"?132:98;
            libraryScroll=GUI.BeginScrollView(R(10,listTop,w-20,rect.height/UiScale-listTop-16),libraryScroll,R(0,0,w-36,(definitions.Length+1)*48));
            try {
                float y=0;
                if(group=="post") {
                    if(theme.Button(R(0,y,w-38,36),"无官谱后处理",current.Length==0))Queue(()=>Model!.SelectEffect(null,"postprocess"));y+=48;
                }
                foreach(var definition in definitions) {
                    if(theme.Button(R(0,y,w-38,38),definition.Label,current==definition.Material,true,13)) {
                        var picked=definition;Queue(()=>{Model!.SelectEffect(picked,target);selectedKey="";selectedParameter="";status="已更换材质；公共轨道保留，可撤销恢复。";});
                    }
                    y+=48;
                }
            }finally{GUI.EndScrollView();}
        }finally{GUI.EndGroup();}
    }
    void SelectGroup(string value){group=value;selectedKey="";selectedParameter=Model!.Catalog.Parameters.FirstOrDefault(p=>p.Binding.Node==value)?.Id??"";trackScroll=0;}
    void DrawInspector(Rect rect)
    {
        GUI.BeginGroup(rect);
        try {
            var theme=ui!;var model=Model!;float w=rect.width/UiScale,h=rect.height/UiScale;
            theme.Panel(new Rect(0,0,rect.width,rect.height));theme.Label(R(16,12,w-32,32),"参数与关键帧",20,"#c9e9ee");
            inspectorScroll=GUI.BeginScrollView(R(0,54,w,h-130),inspectorScroll,R(0,54,w-14,560));
            try {
            w-=14;
            var p=model.Catalog.Parameters.FirstOrDefault(p=>p.Id==selectedParameter);
            if(p==null){theme.Paragraph(R(16,65,w-32,80),"请选择效果材质与参数轨道。",14);return;}
            theme.Label(R(16,54,w-32,30),p.Label,17,"#c9e9ee");
            if(theme.Button(R(16,96,(w-40)/2,32),"◆ 添加/定位"))Queue(()=>selectedKey=model.AddKey(p.Id,CurrentSeconds));
            var marker=model.Keys.FirstOrDefault(k=>k.Id==selectedKey&&k.ParameterId==p.Id);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&marker!=null;
            if(theme.Button(R(24+(w-40)/2,96,(w-40)/2,32),"删除关键帧"))Queue(()=>{model.DeleteKey(selectedKey);selectedKey="";});GUI.enabled=enabled;
            float y=146;
            if(marker!=null) {
                theme.Label(R(16,y,80,28),"时间 / s",14);
                string current=keyTimeDirty?keyTimeText:(model.MovingSeconds??marker.Seconds).ToString("0.000",CI);
                string next=theme.Number(R(102,y,w-118,28),"vfx_key_time",current);
                if(next!=current) {keyTimeText=next;keyTimeDirty=true;textCommitQueued=false;
                    if(double.TryParse(next,System.Globalization.NumberStyles.Float,CI,out var t)&&double.IsFinite(t)&&t>=0)model.MoveKey(marker.Id,t);}
                if(GUI.GetNameOfFocusedControl()=="vfx_key_time"&&Event.current.type==EventType.KeyDown) {
                    if(Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter){try{FinishText();GUI.FocusControl(null);}catch(Exception ex){Error(ex);}Event.current.Use();}
                    else if(Event.current.keyCode==KeyCode.Escape){model.Cancel();keyTimeDirty=false;GUI.FocusControl(null);Event.current.Use();}
                }
                y+=44;
                if(PreviewModel.IsSwitch(p.Id)) {
                    theme.Paragraph(R(16,y,w-32,66),"离散开关：在关键帧处切换。渐变请调整对应 Amount 强度参数。",13);
                }else {
                var modes=new[]{Easing.Linear,Easing.Smooth,Easing.Hold,Easing.SineInOut,Easing.CubicInOut,Easing.BackOut};
                string[] names={"线性","平滑","保持","正弦","三次","回弹"};
                for(int i=0;i<modes.Length;i++) {
                    var mode=modes[i];if(theme.Button(R(16+(i%3)*(w-32)/3,y+(i/3)*34,(w-38)/3,28),names[i],marker.Ease==mode,false,13))
                        Queue(()=>model.SetEase(marker.Id,mode));
                }
                }
                y+=78;
            } else {theme.Paragraph(R(16,y,w-32,48),autoKey?"调整数值会在当前时刻建立关键帧。":"当前编辑默认值；动画参数请先选择关键帧。",13);y+=62;}
            model.Sample(CurrentSeconds);int index=model.Catalog.Parameters.ToList().FindIndex(x=>x.Id==p.Id);
            var value=marker!=null?model.EditedKey==marker.Id&&model.EditedValue.HasValue?model.EditedValue.Value.Number:marker.Value.Number:model.Values[index].Number;
            int components=p.Binding.Kind==ParameterKind.Float?1:4;
            if(p.Binding.Property.StartsWith("_KW_",StringComparison.Ordinal)) {
                string[] labels={"自动","关闭","开启"};float[] states={0,-1,1};
                for(int i=0;i<3;i++)if(theme.Button(R(16+i*(w-32)/3,y,(w-38)/3,32),labels[i],Math.Abs(value.X-states[i])<.1f))
                    {Change(p.Id,0,states[i]);model.Commit();}
                y+=56;
            }else for(int c=0;c<components;c++) {
                float number=c==0?value.X:c==1?value.Y:c==2?value.Z:value.W;
                GUI.BeginGroup(R(16,y,w-32,52));
                try{Scalar(p.Id,components==1?p.Label:p.Binding.Kind==ParameterKind.Color?"RGBA"[c].ToString():"XYZW"[c].ToString(),c,number,
                    (float)(p.SuggestedMin??0),(float)(p.SuggestedMax??1),0,w-32);}finally{GUI.EndGroup();}y+=56;
            }
            if(theme.Button(R(16,y+10,w-32,30),"清空本参数动画",false,false))Queue(()=>{model.ClearTrack(p.Id);selectedKey="";});
            }finally{GUI.EndScrollView();}
            theme.Rule(R(16,h-70,rect.width/UiScale-32,1));theme.Paragraph(R(16,h-60,rect.width/UiScale-32,54),status,13);
        }finally{GUI.EndGroup();}
    }
}
