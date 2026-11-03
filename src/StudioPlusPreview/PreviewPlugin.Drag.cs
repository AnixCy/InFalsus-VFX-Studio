using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    sealed class LibraryHit
    {internal Rect Bounds;internal EffectDefinition Effect=null!;internal int Control;}
    readonly List<LibraryHit> libraryHits=new();
    Vector2 dragPointer,guiScreenOrigin;
    void RegisterLibraryHit(EffectDefinition definition,Rect item,int control,Rect outer,float w)
    {
        var start=GUIUtility.GUIToScreenPoint(item.position)-guiScreenOrigin;
        var bounds=new Rect(start.x,start.y,item.width-31*UiScale,item.height);
        var view=new Rect(outer.x+10*UiScale,outer.y+LibraryListTop*UiScale,(w-20)*UiScale,Mathf.Max(40*UiScale,outer.height-(LibraryListTop+14)*UiScale));
        bounds=Rect.MinMaxRect(Mathf.Max(bounds.xMin,view.xMin),Mathf.Max(bounds.yMin,view.yMin),Mathf.Min(bounds.xMax,view.xMax),Mathf.Min(bounds.yMax,view.yMax));
        if(bounds.width>0&&bounds.height>0)libraryHits.Add(new LibraryHit{Bounds=bounds,Effect=definition,Control=control});
    }
    void RoutePointerEvent(Event e)
    {
        // Register at a stable point for every event, so the rest of the control tree keeps its IDs.
        timelineControl=GUIUtility.GetControlID(0x564654,FocusType.Passive);
        guiScreenOrigin=GUIUtility.GUIToScreenPoint(Vector2.zero);
        if(ui==null||BlocksHostInput)return;
        if(e.isMouse||e.type==EventType.ScrollWheel)dragPointer=e.mousePosition;
        if(e.type==EventType.MouseDown&&e.button==0&&sidebar=="效果库") {
            var hit=libraryHits.FirstOrDefault(h=>h.Bounds.Contains(dragPointer));
            if(hit!=null) {
                FinishText();Model!.Commit();GUI.FocusControl(null);
                if(e.clickCount>1)Queue(()=>AddLibraryEffect(hit.Effect,CurrentSeconds));
                else{libraryCandidate=hit.Effect;libraryDragging=false;libraryDragOrigin=dragPointer;GUIUtility.hotControl=hit.Control;}
                e.Use();return;
            }
        }
        if(libraryCandidate!=null) {
            if(e.type==EventType.MouseDrag) {
                libraryDragging|=(dragPointer-libraryDragOrigin).magnitude>5*UiScale;e.Use();return;
            }
            if(e.type==EventType.MouseUp&&e.button==0) {
                var definition=libraryCandidate;bool valid=libraryDragging&&DropTarget(dragPointer);
                double at=DropTime(dragPointer);int index=DropIndex(dragPointer);
                libraryCandidate=null;libraryDragging=false;GUIUtility.hotControl=0;
                if(valid)Queue(()=>AddLibraryEffect(definition,at,index));e.Use();return;
            }
        }
        TimelineInput(TimelineRows(),31*UiScale,205*UiScale);
    }
    bool DropTarget(Vector2 point)=>timelineBody.Contains(point)||timelineRuler.Contains(point);
    double DropTime(Vector2 point)=>point.x>=timelineRuler.x?XTime(point.x):CurrentSeconds;
    int DropIndex(Vector2 point)
    {
        var rows=TimelineRows();int row=RowAt(rows,point.y);
        if(row<0||row>=rows.Count)return Model!.Document.Runtime.Nodes.Length;
        return Array.FindIndex(Model!.Document.Runtime.Nodes,n=>n.Id==rows[row].Node)+1;
    }
    void DrawDragFeedback()
    {
        if(Event.current.type!=EventType.Repaint||ui==null)return;
        if(!libraryDragging&&!draggingEffect)return;
        float s=UiScale;string title,detail;bool valid=true;double start,end;
        if(libraryDragging&&libraryCandidate!=null) {
            title=libraryCandidate.Label;valid=DropTarget(dragPointer);start=DropTime(dragPointer);end=start+5;
            detail=valid?$"新增轨道  {start:0.000} — {end:0.000} s":"拖到时间轴放置 · Esc 取消";
            if(valid) {
                var rows=TimelineRows();int row=RowAt(rows,dragPointer.y);
                float endOfRow=row<0?(rows.Count==0?0:rows[^1].Top+rows[^1].Height):rows[row].Top+rows[row].Height;
                float y=Mathf.Clamp(timelineBody.y+endOfRow-trackScroll,timelineBody.y,timelineBody.yMax-3*s);
                StudioUiTheme.Fill(new Rect(timelineBody.x,y,timelineBody.width,2*s),"#b4f2f5");
                float x=Mathf.Clamp(TimeX(start),timelineRuler.x,timelineRuler.xMax),right=Mathf.Clamp(TimeX(end),x,timelineRuler.xMax);
                StudioUiTheme.Fill(new Rect(x,Mathf.Min(y+3*s,timelineBody.yMax-24*s),Mathf.Max(4*s,right-x),22*s),"#63b7d455");
            }
        }else {
            var node=Model!.RenderDocument.Nodes.Single(n=>n.Id==draggedEffect);title=Model.NodeLabel(node);start=node.StartSeconds;
            detail=resizingEffect?$"结束  {node.EndSeconds:0.000} s · 时长 {node.EndSeconds-start:0.000} s":$"移动  {start:0.000} — {(node.EndSeconds.HasValue?node.EndSeconds.Value.ToString("0.000",CI):"∞")} s";
            float x=Mathf.Clamp(TimeX(resizingEffect?node.EndSeconds??start:start),timelineRuler.x,timelineRuler.xMax);
            StudioUiTheme.Fill(new Rect(x,timelineRuler.y,2*s,timelineRuler.height+timelineBody.height),"#b4f2f5");
        }
        float width=Mathf.Min(310*s,Screen.width-16*s),height=58*s;
        var box=new Rect(Mathf.Clamp(dragPointer.x+16*s,8*s,Screen.width-width-8*s),Mathf.Clamp(dragPointer.y+18*s,8*s,Screen.height-height-8*s),width,height);
        StudioUiTheme.Fill(box,valid?"#123747f5":"#482d38f5");StudioUiTheme.Fill(new Rect(box.x,box.y,3*s,box.height),valid?"#b4eff5":"#ef9da8");
        ui.Label(new Rect(box.x+12*s,box.y+5*s,box.width-24*s,23*s),ui.Fit(title,box.width-24*s,14),14,"#e0f8fb");
        ui.Label(new Rect(box.x+12*s,box.y+30*s,box.width-24*s,22*s),detail,12,"#a6ced6");
    }
}
