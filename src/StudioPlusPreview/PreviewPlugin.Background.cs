using UnityEngine;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    Vector2 backgroundScroll;
    void ClearBackgroundImage(){Surface!.ApplyImage(null);imagePath="";status="已清除导入图片；效果和动画保留。";}
    void ChooseBrightImage()
    {
        var path=openDialog("选择 Bright 亮图","png,jpg,jpeg");if(path.Length==0)return;
        var image=Surface!.PrepareImage(path,portable:true);Surface.ApplyBrightImage(image);brightImagePath=Path.GetFullPath(path);
        status="亮图已载入；使用 Bright 强度控制混合。";
    }
    void ClearBrightImage(){Surface!.ApplyBrightImage(null);brightImagePath="";status="已恢复效果自带亮图；Bright 动画保留。";}
    void DrawBackgroundSettings(float w,float h)
    {
        backgroundScroll=GUI.BeginScrollView(R(0,48,w,Mathf.Max(1,h-48)),backgroundScroll,R(0,0,w-19,500));
        try {
            var theme=ui!;w-=19;theme.Label(R(14,8,w-28,28),"背景图片",17,"#c9e9ee");
            if(theme.Button(R(12,46,w-24,32),"选择图片…",false,true,14))Queue(ChooseImage);
            theme.Paragraph(R(14,88,w-28,48),imagePath.Length==0?"尚未导入图片":Path.GetFileName(imagePath),13);
            if(theme.Button(R(12,142,w-24,29),"清除导入图片",false,false,13))Queue(ClearBackgroundImage);
            theme.Rule(R(12,187,w-24,1));theme.Label(R(14,200,w-28,28),"Bright 亮图",17,"#c9e9ee");
            if(theme.Button(R(12,238,w-24,32),"选择亮图…",false,true,14))Queue(ChooseBrightImage);
            theme.Paragraph(R(14,280,w-28,48),brightImagePath.Length==0?"使用效果自带亮图":Path.GetFileName(brightImagePath),13);
            if(theme.Button(R(12,334,w-24,29),"恢复默认亮图",false,false,13))Queue(ClearBrightImage);
            theme.Paragraph(R(14,382,w-28,90),"Bright 强度控制主图与亮图的混合。未使用 Bright 的材质不受影响；更换主图会保留亮图。",13);
        }finally{GUI.EndScrollView();}
    }
    void DrawImageInspector(Rect rect)
    {
        GUI.BeginGroup(rect);try{var theme=ui!;float w=rect.width/UiScale;
            theme.Panel(new Rect(0,0,rect.width,rect.height));theme.Label(R(16,14,w-32,30),"背景图片",20,"#c9e9ee");
            theme.Paragraph(R(16,63,w-32,130),imagePath.Length==0?"选择本地 PNG/JPEG 图片，工程只保存路径。":imagePath,13);
            theme.Label(R(16,204,w-32,28),"Bright 亮图",17,"#c9e9ee");
            theme.Paragraph(R(16,244,w-32,100),brightImagePath.Length==0?"使用效果自带亮图；在左侧背景栏选择自定义亮图。":brightImagePath,13);
            theme.Paragraph(R(16,364,w-32,75),"图片与效果分开管理。更换或清除亮图保留 Bright 的强度和关键帧。",13);
        }finally{GUI.EndGroup();}
    }
}
