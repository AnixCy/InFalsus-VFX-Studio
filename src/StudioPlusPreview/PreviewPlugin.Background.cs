using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    Vector2 backgroundScroll;
    void DrawBackgroundSettings(float w,float h)
    {
        var theme=ui!;theme.Label(R(14,51,w-28,25),"背景图片",16,"#c9e9ee");
        if(theme.Button(R(12,85,w-24,31),"导入图片…",false,true,14))Queue(ChooseImage);
        theme.Paragraph(R(14,124,w-28,37),imagePath.Length==0?"使用材质自带底图":Path.GetFileName(imagePath),12);
        if(theme.Button(R(12,166,w-24,28),"恢复材质底图",false,false,13))Queue(()=>{
            Surface!.ApplyImage(null);imagePath="";
            if(Model!.Catalog.Parameters.Any(p=>p.Id=="background/_PatternMix")){Model.UpdateParameter("background/_PatternMix",new InFalsus.Presentation.ParameterValue(new System.Numerics.Vector4(1,0,0,0)));Model.Commit();}
        });
        theme.Rule(R(12,204,w-24,1));theme.Label(R(14,216,w-28,25),"背景材质",16,"#c9e9ee");
        var definitions=Model!.Library.Effects.Where(e=>e.Target=="background").ToArray();
        var current=Model.Definition(Model.Document.Runtime.Nodes.Single(n=>n.Target=="background")).Id;
        backgroundScroll=GUI.BeginScrollView(R(10,252,w-20,Mathf.Max(40,h-264)),backgroundScroll,R(0,0,w-38,definitions.Length*42));
        try{for(int i=0;i<definitions.Length;i++) {
            var definition=definitions[i];if(theme.Button(R(0,i*42,w-40,35),theme.Fit(definition.Label,(w-56)*UiScale,12),current==definition.Id,true,12))
                Queue(()=>{Model!.SetBackground(definition);SelectNode("background");status="已替换背景材质；效果栈保持，可撤销。";});
        }}finally{GUI.EndScrollView();}
    }
}
