using InFalsus.Presentation;
using UnityEngine;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckInteraction(Action<bool,string> check)
    {
        var previousModel=Model;var previousSelection=(selectedEffect,selectedParameter,selectedKey,sidebar);
        var previousRuler=timelineRuler;double previousStart=timelineStart,previousSpan=timelineSpan;
        var previousScroll=inspectorScroll;var previousCollapsed=collapsedEffects.ToArray();
        var previousTime=Bridge.Snapshot().ChartSeconds;bool previousView=Bridge.AuthoringView;
        var transport=Bridge.Transport!;var originalClip=transport.Clip;float volume=transport.Volume;double offset=transport.AudioOffsetMs;
        AudioClip? silent=null;
        try {
            var model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString(),legacy:true);
            Model=model;Bridge.SetView(true);SeekWorkspace(5);selectedEffect="adjust";selectedParameter="adjust/_Exposure";
            string id=selectedParameter,key=model.AddKey(id,10);selectedKey=key;
            Change(id,0,1.25f);textParameter=id;textKey=id+":0";textComponent=0;drafts[textKey]="1.25";
            SelectNode("background");
            check(model.Keys.Single(k=>k.Id==key).Value.Number.X==1.25f&&!model.Keys.Any(k=>k.ParameterId==id&&k.Seconds==5),
                "changing the selected effect commits a numeric draft to its original key rather than creating a key at the playhead");
            selectedEffect="adjust";selectedParameter=id;selectedKey=key;textParameter=id;textKey=id+":0";drafts[textKey]="invalid";
            long revision=model.Session.Revision;bool rejected=false;
            try{OpenBackgroundSidebar();}catch(FormatException){rejected=true;}
            check(rejected&&selectedEffect=="adjust"&&selectedKey==key&&textParameter==id&&model.Session.Revision==revision,
                "invalid numeric input preserves selection and draft before a sidebar switch");
            textParameter=textKey=null;drafts.Clear();model.Cancel();
            selectedEffect="background";sidebar="背景";inspectorScroll=new Vector2(0,420);collapsedEffects.Add("adjust");SelectTimelineNode("adjust");
            check(selectedEffect=="adjust"&&sidebar=="效果库","selecting an effect track exits the image-only inspector");
            check(inspectorScroll==Vector2.zero&&collapsedEffects.Contains("adjust"),"effect selection resets inspector scrolling without forcing a collapsed track open");
            selectedParameter=id;selectedKey=key;model.Sample(5);var parameter=model.Catalog.Get(id);
            check(InspectorValue(parameter).X==1.25f,"all inspector variants show the selected key value even away from the playhead");
            Change(id,0,1.5f);check(InspectorValue(parameter).X==1.5f,"selected-key inspector values follow an uncommitted parameter edit");model.Cancel();
            timelineRuler=new Rect(200,100,800,30);timelineStart=0;timelineSpan=20;SeekWorkspace(2);revision=model.Session.Revision;
            BeginTimelineScrub(400);
            check(scrubbingTimeline&&Math.Abs(CurrentSeconds-5)<.001&&Bridge.Snapshot().Transport==TransportState.Frozen,"ruler dragging begins with an immediate paused seek");
            var drag=new Event{type=EventType.MouseDrag,button=0,mousePosition=new Vector2(600,300)};ScrubPointer(drag);
            check(drag.type==EventType.Used&&Math.Abs(CurrentSeconds-10)<.001,"continuous ruler dragging follows horizontal movement outside the ruler vertically");
            ScrubPointer(new Event{type=EventType.MouseDrag,button=0,mousePosition=new Vector2(2000,300)});
            check(Math.Abs(CurrentSeconds-20)<.001,"scrubbing beyond the view edge clamps to its visible time range");
            ScrubPointer(new Event{type=EventType.MouseUp,button=1});check(scrubbingTimeline,"another mouse button cannot release the active left-button scrub");
            ScrubPointer(new Event{type=EventType.MouseUp,button=0,mousePosition=Vector2.zero});
            check(!scrubbingTimeline&&Math.Abs(CurrentSeconds-20)<.001,"scrub release outside the timeline retains the final seek and clears capture");
            SeekWorkspace(2);BeginTimelineScrub(400);CancelEffectDrag();
            check(!scrubbingTimeline&&Math.Abs(CurrentSeconds-2)<.001,"canceling a ruler drag restores its exact initial time");
            SeekWorkspace(7);BeginTimelineScrub(400);OnApplicationFocus(false);
            check(!scrubbingTimeline&&Math.Abs(CurrentSeconds-7)<.001,"focus loss safely cancels ruler capture and restores its initial time");
            check(model.Session.Revision==revision&&!model.IsEditing,"timeline scrubbing does not add animation edits or undo transactions");
            silent=AudioClip.Create("VFX silent transport fixture",48000,1,48000,false);transport.Pause();transport.Volume=0;transport.SetClip(silent);
            transport.SetClip(null);SeekWorkspace(.4);Bridge.TogglePlayback();transport.SetClip(silent);
            check(Bridge.Snapshot().Transport==TransportState.Frozen,"loading music cannot retain a running flag from the silent timeline");
            Bridge.TogglePlayback();Bridge.TogglePlayback();
            check(!transport.Playing&&Bridge.Snapshot().Transport==TransportState.Frozen,"music pause clears the silent fallback state after a source change");
            transport.SetAudioOffset(250);SeekWorkspace(1);transport.Play();bool ended=Bridge.FinishPlaybackIfEnded();
            check(ended&&!transport.Playing&&Math.Abs(transport.TimeMs-750)<.001,"audio completion freezes at the exact chart time including positive audio offset");
            Bridge.TogglePlayback();
            check(transport.Playing&&transport.TimeMs<100&&ReferenceEquals(transport.Clip,silent),"playing from the audio end restarts the same loaded transport from zero");
            transport.Pause();transport.SetAudioOffset(-250);SeekWorkspace(2);transport.Play();Bridge.FinishPlaybackIfEnded();
            check(!transport.Playing&&Math.Abs(transport.TimeMs-1250)<.001,"audio completion also honors a negative audio offset");
            SeekWorkspace(.3);check(Math.Abs(CurrentSeconds-Bridge.Snapshot().ChartSeconds)<.000001,"seek updates the displayed playhead immediately from the shared music clock");
        }finally {
            EndTimelineScrub();StopHeldPreview();transport.Pause();transport.SetClip(originalClip);transport.SetAudioOffset(offset);transport.Volume=volume;
            if(silent)DestroyImmediate(silent);
            textParameter=textKey=null;keyTimeDirty=false;drafts.Clear();textCommitQueued=false;
            Model=previousModel;(selectedEffect,selectedParameter,selectedKey,sidebar)=previousSelection;
            timelineRuler=previousRuler;timelineStart=previousStart;timelineSpan=previousSpan;inspectorScroll=previousScroll;
            collapsedEffects.Clear();foreach(var item in previousCollapsed)collapsedEffects.Add(item);
            SeekWorkspace(previousTime);Bridge.SetView(previousView);
        }
    }
}
