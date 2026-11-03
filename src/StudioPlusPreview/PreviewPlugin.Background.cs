using UnityEngine;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void ClearBackgroundImage(){Surface!.ApplyImage(null);imagePath="";status="已清除导入图片；效果和动画保留。";}
    void DrawBackgroundSettings(float w,float h)
    {
        var theme=ui!;theme.Label(R(14,54,w-28,28),"背景图片",17,"#c9e9ee");
        if(theme.Button(R(12,94,w-24,32),"选择图片…",false,true,14))Queue(ChooseImage);
        theme.Paragraph(R(14,140,w-28,66),imagePath.Length==0?"尚未导入图片":Path.GetFileName(imagePath),13);
        if(theme.Button(R(12,222,w-24,29),"清除导入图片",false,false,13))Queue(ClearBackgroundImage);
        theme.Rule(R(12,268,w-24,1));theme.Paragraph(R(14,284,w-28,80),"图片在这里设置。扭曲、旋转等效果从效果库添加，再到时间轴打帧。",13);
    }
    void DrawImageInspector(Rect rect)
    {
        GUI.BeginGroup(rect);try{var theme=ui!;float w=rect.width/UiScale;
            theme.Panel(new Rect(0,0,rect.width,rect.height));theme.Label(R(16,14,w-32,30),"背景图片",20,"#c9e9ee");
            theme.Paragraph(R(16,63,w-32,130),imagePath.Length==0?"选择本地 PNG/JPEG 图片，工程只保存路径。":imagePath,13);
            theme.Paragraph(R(16,204,w-32,75),"背景图片与效果分开管理。更换图片保留时间轴中的效果和关键帧。",13);
        }finally{GUI.EndGroup();}
    }
}
