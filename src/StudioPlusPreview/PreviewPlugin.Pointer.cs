using UnityEngine;
namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    enum PointerShape{Default,Vertical,Horizontal,Move}
    PointerShape pointerShape;
    readonly Dictionary<PointerShape,Texture2D> pointerTextures=new();
    EffectRow? ResizeRow(List<EffectRow> rows,Vector2 point)
    {
        if(!timelineBody.Contains(point))return null;
        return rows.Where(r=>r.Parameter!=null&&Math.Abs(point.y-(timelineBody.y+r.Top+r.Height-trackScroll))<=10*UiScale)
            .OrderBy(r=>Math.Abs(point.y-(timelineBody.y+r.Top+r.Height-trackScroll))).FirstOrDefault();
    }
    void UpdatePointer()
    {
        if(ui==null||!Bridge.AuthoringView||BlocksHostInput||!Application.isFocused){SetPointer(PointerShape.Default);return;}
        var point=Event.current.mousePosition;var rows=TimelineRows();PointerShape next=PointerShape.Default;
        if(scrubbingTimeline||timelineRuler.Contains(point))next=PointerShape.Horizontal;
        else if(resizingChannel||ResizeRow(rows,point)!=null)next=PointerShape.Vertical;
        else if(draggingEffect)next=resizingEffect?PointerShape.Horizontal:PointerShape.Move;
        else if(draggingKey||draggingCurve||libraryDragging||libraryHits.Any(h=>h.Bounds.Contains(point)))next=PointerShape.Move;
        else if(timelineBody.Contains(point)&&point.x>=timelineRuler.x) {
            int i=RowAt(rows,point.y);
            if(i>=0) {
                var row=rows[i];var node=Model!.RenderDocument.Nodes.Single(n=>n.Id==row.Node);
                if(row.Parameter==null&&node.Target=="postprocess"&&XTime(point.x)>=node.StartSeconds&&(!node.EndSeconds.HasValue||XTime(point.x)<=node.EndSeconds))
                    next=node.EndSeconds.HasValue&&Math.Abs(point.x-TimeX(node.EndSeconds.Value))<8*UiScale?PointerShape.Horizontal:PointerShape.Move;
                else if(row.Parameter!=null) {
                    var area=CurveArea(row);
                    bool keyHit=Model.Keys.Any(k=>k.ParameterId==row.Parameter&&(new Vector2(TimeX(k.Seconds),ValueY(row.Parameter,Component(k.Value.Number,PlotComponent(row.Parameter)),area))-point).magnitude<10*UiScale);
                    bool handleHit=false;
                    if(row.Parameter==selectedParameter&&CurveSegment() is { } segment) {
                        var curve=BezierCurve.From(segment.Left);handleHit=(HandlePosition(segment.Left,segment.Right,curve.A,area)-point).magnitude<9*UiScale||(HandlePosition(segment.Left,segment.Right,curve.B,area)-point).magnitude<9*UiScale;
                    }
                    if(keyHit||handleHit)next=PointerShape.Move;
                }
            }
        }
        SetPointer(next);
    }
    void SetPointer(PointerShape next)
    {
        if(pointerShape==next)return;pointerShape=next;
        if(next==PointerShape.Default){Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);return;}
        if(!pointerTextures.TryGetValue(next,out var texture)) {
            texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="VFX pointer "+next,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point};
            var ink=new bool[32,32];
            void Mark(int x,int y){if(x>=0&&x<32&&y>=0&&y<32)ink[x,y]=true;}
            if(next is PointerShape.Vertical or PointerShape.Move) {
                for(int y=8;y<=24;y++)for(int x=15;x<=16;x++)Mark(x,y);
                for(int d=0;d<=5;d++)for(int x=15-d;x<=16+d;x++){Mark(x,5+d);Mark(x,26-d);}
            }
            if(next is PointerShape.Horizontal or PointerShape.Move) {
                for(int x=8;x<=24;x++)for(int y=15;y<=16;y++)Mark(x,y);
                for(int d=0;d<=5;d++)for(int y=15-d;y<=16+d;y++){Mark(5+d,y);Mark(26-d,y);}
            }
            var pixels=new Color32[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
                bool border=false;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<32&&y+dy>=0&&y+dy<32)border|=ink[x+dx,y+dy];
                pixels[y*32+x]=ink[x,y]?new Color32(235,252,255,255):border?new Color32(8,18,24,255):new Color32(0,0,0,0);
            }
            texture.SetPixels32(pixels);texture.Apply();pointerTextures.Add(next,texture);
        }
        Cursor.SetCursor(texture,new Vector2(16,16),CursorMode.Auto);
    }
    void ReleasePointers(){SetPointer(PointerShape.Default);foreach(var texture in pointerTextures.Values)Destroy(texture);pointerTextures.Clear();}
}
