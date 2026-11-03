using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void HelpParagraph(string text,float width,ref float y)
    {
        float height=ui!.ParagraphHeight(text,(width-32)*UiScale,12)/UiScale;
        ui.Paragraph(R(16,y,width-32,height),text,12);y+=height+12;
    }
    void DrawEffectTooltip()
    {
        if(Event.current.type!=EventType.Repaint||ui==null||GUIUtility.hotControl!=0||libraryCandidate!=null||sidebar!="效果库")return;
        var hit=libraryHits.FirstOrDefault(h=>h.Bounds.Contains(Event.current.mousePosition));if(hit==null)return;
        float s=UiScale,width=Mathf.Min(420*s,Screen.width-24*s);
        string text=EffectNotes.Help(hit.Effect);
        float height=Mathf.Min(Screen.height-24*s,ui.ParagraphHeight(text,width-24*s,12)+39*s);
        var mouse=Event.current.mousePosition;
        var box=new Rect(Mathf.Clamp(mouse.x+18*s,8*s,Screen.width-width-8*s),Mathf.Clamp(mouse.y+20*s,8*s,Screen.height-height-8*s),width,height);
        StudioUiTheme.Fill(box,"#0d2733fa");StudioUiTheme.Fill(new Rect(box.x,box.y,2*s,box.height),"#87cbd6");
        ui.Label(new Rect(box.x+12*s,box.y+5*s,width-24*s,25*s),ui.Fit(hit.Effect.Label,width-24*s,14),14,"#d6f3f5");
        ui.Paragraph(new Rect(box.x+12*s,box.y+33*s,width-24*s,height-37*s),text,12,"#a7c6d0");
    }
}
