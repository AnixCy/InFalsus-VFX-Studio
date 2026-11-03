using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    StudioShortcuts? shortcuts;
    readonly HashSet<KeyCode> playbackKeysDown=new();
    KeyCode? heldPreviewKey;
    double heldPreviewStart;
    int heldPreviewGeneration;
    bool heldPreviewStarted;

    static KeyCode PhysicalKey(Event e)=>e.isMouse?(KeyCode)((int)KeyCode.Mouse0+e.button):e.keyCode;
    void WorkspaceKeys()=>RouteWorkspaceEvent(Event.current,OwnsText);
    void RouteWorkspaceEvent(Event e,bool textFocused)
    {
        if(BlocksHostInput)return;
        bool release=e.type is EventType.KeyUp or EventType.MouseUp;
        bool down=e.type==EventType.KeyDown||e.type==EventType.MouseDown&&e.button>=2;
        if(!down&&!release)return;
        var key=PhysicalKey(e);
        if(release) {
            playbackKeysDown.Remove(key);
            if(heldPreviewKey==key){Queue(StopHeldPreview);e.Use();}
            return;
        }
        if(e.keyCode==KeyCode.Escape) {
            if(libraryCandidate!=null||draggingEffect){CancelEffectDrag();e.Use();}
            else if(heldPreviewKey.HasValue){Queue(StopHeldPreview);e.Use();}
            else if(Model!.IsEditing) {
                Model.Cancel();draggingKey=sliderEditing=keyTimeDirty=false;textParameter=textKey=null;
                drafts.Clear();GUIUtility.hotControl=0;e.Use();
            }
            return;
        }
        shortcuts??=StudioShortcuts.TryBind();
        string? action=shortcuts?.Find(e);
        // Numeric fields retain text undo, selection and clipboard shortcuts.
        if(textFocused&&action is not ("save" or "saveAs"))return;
        switch(action) {
            case "open":Queue(LoadProject);break;
            case "save":Queue(SaveProject);break;
            case "saveAs":Queue(SaveAs);break;
            case "undo":Queue(Model!.Undo);break;
            case "redo":Queue(Model!.Redo);break;
            case "delete":
                if(selectedKey.Length>0)Queue(()=>{Model!.DeleteKey(selectedKey);selectedKey="";});
                else if(selectedParameter.Length>0)Queue(()=>Model!.ClearTrack(selectedParameter));
                else if(Model!.Document.Runtime.Nodes.Any(n=>n.Id==selectedEffect&&n.Target=="postprocess"))Queue(()=>Model!.RemoveEffect(selectedEffect));
                break;
            case "previewHold":
                if(playbackKeysDown.Add(key)&&!heldPreviewKey.HasValue) {
                    heldPreviewKey=key;heldPreviewStarted=false;int generation=++heldPreviewGeneration;
                    Queue(()=>{
                        if(heldPreviewKey!=key||generation!=heldPreviewGeneration)return;
                        heldPreviewStart=Bridge.Snapshot().ChartSeconds;heldPreviewStarted=true;
                        Bridge.SeekSeconds(heldPreviewStart);Bridge.TogglePlayback();
                    });
                }
                break;
            case "previewToggle":
                if(playbackKeysDown.Add(key)&&!heldPreviewKey.HasValue)Queue(Bridge.TogglePlayback);
                break;
            default:return;
        }
        e.Use();
    }
    void StopHeldPreview()
    {
        if(!heldPreviewKey.HasValue)return;
        heldPreviewKey=null;heldPreviewGeneration++;playbackKeysDown.Clear();
        if(heldPreviewStarted){heldPreviewStarted=false;Bridge.SeekSeconds(heldPreviewStart);}
    }
    void OnApplicationFocus(bool focused)
    {
        if(focused)return;
        StopHeldPreview();playbackKeysDown.Clear();
        CancelEffectDrag();
    }
}
