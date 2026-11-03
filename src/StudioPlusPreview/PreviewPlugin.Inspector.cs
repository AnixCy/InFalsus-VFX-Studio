using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    long rangeRevision=-1;
    float inspectorContentHeight=650;
    void DrawInspector(Rect rect)
    {
        GUI.BeginGroup(rect);
        try {
            var theme=ui!;var model=Model!;var node=model.RenderDocument.Nodes.Single(n=>n.Id==selectedEffect);
            float w=rect.width/UiScale,h=rect.height/UiScale;
            theme.Panel(new Rect(0,0,rect.width,rect.height));theme.Label(R(16,10,w-32,30),node.Target=="background"?"背景参数":"效果检查器",20,"#c9e9ee");
            inspectorScroll=GUI.BeginScrollView(R(0,47,w,Mathf.Max(60,h-110)),inspectorScroll,R(0,47,w-14,Mathf.Max(h-110,inspectorContentHeight)));w-=14;
            try {
                if(rangeNode!=node.Id||!OwnsText&&(rangeRevision!=model.Session.Revision||draggingEffect)) {
                    rangeNode=node.Id;rangeRevision=model.Session.Revision;rangeStart=node.StartSeconds.ToString("0.###",CI);
                    rangeEnd=node.EndSeconds?.ToString("0.###",CI)??"";rangeName=model.NodeLabel(node);
                }
                rangeName=theme.Text(R(16,49,w-68,28),"vfx_effect_name",rangeName);
                if(theme.Button(R(w-48,49,32,28),"↵",false,false))Queue(()=>model.RenameEffect(node.Id,rangeName));
                if(theme.Button(R(16,87,68,28),node.Enabled?"启用":"停用",node.Enabled,false))Queue(()=>model.EnableEffect(node.Id,!node.Enabled));
                if(node.Target=="postprocess") {
                    if(theme.Button(R(90,87,38,28),"↑",false,false))Queue(()=>model.ReorderEffect(node.Id,-1));
                    if(theme.Button(R(134,87,38,28),"↓",false,false))Queue(()=>model.ReorderEffect(node.Id,1));
                    if(theme.Button(R(180,87,w-196,28),"删除",false,false))Queue(()=>model.RemoveEffect(node.Id));
                    theme.Label(R(16,126,55,27),"开始 s",12);rangeStart=theme.Number(R(75,126,w-91,27),"vfx_effect_start",rangeStart);
                    theme.Label(R(16,159,55,27),"结束 s",12);rangeEnd=theme.Number(R(75,159,w-91,27),"vfx_effect_end",rangeEnd);
                    if(theme.Button(R(16,195,w-32,28),"应用区间 · 空结束为持续",false,false,12))Queue(()=>{
                        if(!double.TryParse(rangeStart,System.Globalization.NumberStyles.Float,CI,out var start))throw new FormatException("开始时刻无效。");
                        double? end=rangeEnd.Trim().Length==0?null:double.Parse(rangeEnd,CI);model.SetEffectRange(node.Id,start,end,true);
                    });
                }
                float y=node.Target=="postprocess"?239:132;
                var parameters=model.Catalog.Parameters.Where(p=>p.Binding.Node==node.Id&&!model.Definition(node).Parameters.Single(v=>v.Property==p.Binding.Property).Hidden).ToArray();
                var parameter=parameters.FirstOrDefault(p=>p.Id==selectedParameter);
                if(theme.Button(R(16,y,w-32,32),theme.Fit(parameter?.Label??"选择参数",(w-60)*UiScale,14)+"  ▾",parameterPicker,false,14))parameterPicker=!parameterPicker;y+=43;
                if(parameterPicker) {
                    pickerScroll=GUI.BeginScrollView(R(12,y,w-24,155),pickerScroll,R(0,0,w-42,parameters.Length*31));
                    try{for(int i=0;i<parameters.Length;i++) {
                        var p=parameters[i];if(theme.Button(R(0,i*31,w-44,28),theme.Fit(p.Label,(w-60)*UiScale),selectedParameter==p.Id,false,13))
                            {FinishText();model.Commit();selectedParameter=p.Id;selectedKey="";parameterPicker=false;}
                    }}finally{GUI.EndScrollView();}y+=166;
                }
                if(parameter==null){theme.Paragraph(R(16,y,w-32,90),"选择参数可调节数值。点击 ◆ 或启用自动关键帧后调整，参数才会加入时间轴。",13);y+=96;}
                else DrawParameterInspector(parameter,w,ref y);
                if(libraryFilter=="自定义"||model.Definition(node).SourceDirectory.Length>0)DrawSourceControls(w,ref y);
                inspectorContentHeight=Mathf.Max(200,y+40-47);
            }finally{GUI.EndScrollView();}
            theme.Rule(R(16,h-59,rect.width/UiScale-32,1));theme.Paragraph(R(16,h-51,rect.width/UiScale-32,46),status,12);
        }finally{GUI.EndGroup();}
    }
    void DrawParameterInspector(ParameterDescriptor p,float w,ref float y)
    {
        var theme=ui!;var model=Model!;
        if(theme.Button(R(16,y,(w-40)/2,29),"◆ 关键帧",false,false,13))Queue(()=>selectedKey=model.AddKey(p.Id,CurrentSeconds));
        var marker=model.Keys.FirstOrDefault(k=>k.Id==selectedKey&&k.ParameterId==p.Id);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&marker!=null;
        if(theme.Button(R(24+(w-40)/2,y,(w-40)/2,29),"删除帧",false,false,13))Queue(()=>{model.DeleteKey(selectedKey);selectedKey="";});GUI.enabled=enabled;y+=43;
        if(marker!=null) {
            theme.Label(R(16,y,68,27),"时间 / s",13);
            string current=keyTimeDirty?keyTimeText:(model.MovingSeconds??marker.Seconds).ToString("0.000",CI);
            string next=theme.Number(R(88,y,w-104,27),"vfx_key_time",current);
            if(next!=current){keyTimeText=next;keyTimeDirty=true;textCommitQueued=false;
                if(double.TryParse(next,System.Globalization.NumberStyles.Float,CI,out var t)&&double.IsFinite(t)&&t>=0)model.MoveKey(marker.Id,t);}
            if(GUI.GetNameOfFocusedControl()=="vfx_key_time"&&Event.current.type==EventType.KeyDown) {
                if(Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter){try{FinishText();GUI.FocusControl(null);}catch(Exception ex){Error(ex);}Event.current.Use();}
                else if(Event.current.keyCode==KeyCode.Escape){model.Cancel();keyTimeDirty=false;GUI.FocusControl(null);Event.current.Use();}
            }
            y+=40;
            if(PreviewModel.IsSwitch(p.Id)){theme.Paragraph(R(16,y,w-32,48),"离散开关；渐变请调整 Amount 或混合强度。",12);y+=54;}
            else {
                var modes=new[]{Easing.Linear,Easing.Smooth,Easing.Hold,Easing.SineInOut,Easing.CubicInOut,Easing.BackOut};
                string[] names={"线性","平滑","保持","正弦","三次","回弹"};
                for(int i=0;i<modes.Length;i++){var mode=modes[i];if(theme.Button(R(16+i%3*(w-32)/3,y+i/3*32,(w-38)/3,27),names[i],marker.Ease==mode,false,12))Queue(()=>model.SetEase(marker.Id,mode));}
                y+=70;
            }
        }
        model.Sample(CurrentSeconds);int index=model.Catalog.Parameters.ToList().FindIndex(x=>x.Id==p.Id);
        var value=marker!=null?model.EditedKey==marker.Id&&model.EditedValue.HasValue?model.EditedValue.Value.Number:marker.Value.Number:model.Values[index].Number;
        int components=p.Binding.Kind==ParameterKind.Float?1:4;
        if(PreviewModel.IsSwitch(p.Id)) {
            string[] labels={"自动","关闭","开启"};float[] states={0,-1,1};
            for(int i=0;i<3;i++)if(theme.Button(R(16+i*(w-32)/3,y,(w-38)/3,30),labels[i],Math.Abs(value.X-states[i])<.1f))
                {Change(p.Id,0,states[i]);model.Commit();}y+=45;
        }else for(int c=0;c<components;c++) {
            float number=c==0?value.X:c==1?value.Y:c==2?value.Z:value.W;
            GUI.BeginGroup(R(16,y,w-32,52));try{Scalar(p.Id,components==1?p.Label:p.Binding.Kind==ParameterKind.Color?"RGBA"[c].ToString():"XYZW"[c].ToString(),c,number,
                (float)(p.SuggestedMin??0),(float)(p.SuggestedMax??1),0,w-32);}finally{GUI.EndGroup();}y+=56;
        }
        if(theme.Button(R(16,y+8,w-32,28),"移除本参数动画",false,false,12))Queue(()=>{model.ClearTrack(p.Id);selectedKey="";});
        y+=48;
    }
}
