using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    bool scrubbingTimeline;
    double scrubStartSeconds;

    void SelectNode(string id,bool expand=true)
    {
        // Commit against the old key before changing any selection identity.
        FinishText();Model?.Commit();
        if(selectedEffect!=id){inspectorScroll=Vector2.zero;curveComponent=0;shaderParameterSearch="";}
        selectedEffect=id;selectedParameter=selectedKey="";parameterPicker=false;rangeNode="";
        if(expand)collapsedEffects.Remove(id);
        if(id!="background")sidebar="效果库";
        var node=Model?.Document.Runtime.Nodes.FirstOrDefault(n=>n.Id==id);
        if(node==null)return;
        string directory=Model!.Definition(node).SourceDirectory;
        if(directory.Length>0&&sourceWorkspace?.Root!=directory)
            try{UseSource(SourceWorkspace.Open(directory));}catch(Exception e){sourceMessage=e.Message;}
    }
    void SelectTimelineNode(string id)
    {
        FinishText();Model?.Commit();
        if(selectedEffect!=id)SelectNode(id,false);
        sidebar="效果库";rangeNode="";
    }
    void OpenBackgroundSidebar(){SelectNode("background");sidebar="背景";}
    void FocusShaderParameter(string id)
    {
        var descriptor=Model!.Catalog.Get(id);
        var node=Model.RenderDocument.Nodes.Single(n=>n.Id==descriptor.Binding.Node);
        if(Model.Definition(node).SourceDirectory.Length==0)return;
        shaderParameterSearch=descriptor.Binding.Property;inspectorScroll=Vector2.zero;
    }
    System.Numerics.Vector4 InspectorValue(ParameterDescriptor parameter)
    {
        var key=Model!.Keys.FirstOrDefault(k=>k.Id==selectedKey&&k.ParameterId==parameter.Id);
        if(key!=null)return Model.EditedKey==key.Id&&Model.EditedValue.HasValue?Model.EditedValue.Value.Number:key.Value.Number;
        int index=Model.Catalog.Parameters.ToList().FindIndex(p=>p.Id==parameter.Id);
        return Model.Values[index].Number;
    }
    void SeekWorkspace(double seconds){Bridge.SeekSeconds(seconds);CurrentSeconds=Bridge.Snapshot().ChartSeconds;}
    void BeginTimelineScrub(float x)
    {
        FinishText();Model!.Commit();StopHeldPreview();
        scrubStartSeconds=Bridge.Snapshot().ChartSeconds;scrubbingTimeline=true;
        SeekWorkspace(XTime(Mathf.Clamp(x,timelineRuler.xMin,timelineRuler.xMax)));
    }
    void EndTimelineScrub(bool cancel=false)
    {
        if(!scrubbingTimeline)return;
        scrubbingTimeline=false;
        if(cancel)SeekWorkspace(scrubStartSeconds);
        GUIUtility.hotControl=0;
    }
    bool ScrubPointer(Event e)
    {
        if(!scrubbingTimeline)return false;
        if(e.type==EventType.MouseDrag&&e.button==0) {
            SeekWorkspace(XTime(Mathf.Clamp(e.mousePosition.x,timelineRuler.xMin,timelineRuler.xMax)));e.Use();return true;
        }
        if(e.rawType==EventType.MouseUp&&e.button==0){EndTimelineScrub();e.Use();return true;}
        if(e.type==EventType.ScrollWheel){e.Use();return true;}
        return false;
    }
}
