using InFalsus.Presentation;
using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;

public sealed partial class PreviewPlugin
{
    bool chartBackgroundEnabled;
    Rect chartBackgroundButton;
    ChartBackgroundDisplay? chartBackground;
    PackageDocument? chartBackgroundDocument;
    RenderTexture? chartBackgroundTexture;
    double chartBackgroundAudio=double.NaN,chartBackgroundSeconds=double.NaN;
    int chartBackgroundHeight;
    int chartBackgroundImageRevision;
    internal int ChartBackgroundRenders;

    void SetChartBackgroundPreview(bool enabled)
    {
        chartBackgroundEnabled=enabled;SavePreviewSettings();
        if(!enabled)HideChartBackground();
    }
    void HideChartBackground()
    {
        bool visible=chartBackground?.Visible==true;
        chartBackground?.Hide();chartBackgroundDocument=null;chartBackgroundTexture=null;
        if(visible&&!Bridge.AuthoringView)Surface?.Suspend();
    }
    void TickChartBackground()
    {
        if(!chartBackgroundEnabled||stopped||actionRunning||quitPermit||Bridge.AuthoringView||Bridge.Exporting
            ||Model==null||Surface==null||!Bridge.Owner||!Bridge.Owner!.ViewCamera.enabled) {
            HideChartBackground();return;
        }
        try{PresentChartBackground(Bridge.Snapshot());}
        catch(Exception e){HideChartBackground();Surface?.Suspend();chartBackgroundEnabled=false;Error(e);}
    }
    void PresentChartBackground(PlaybackSnapshot snapshot)
    {
        // Paused chart editing does not need to redraw the isolated effect pipeline.
        // A seek, source edit, view transition or resolution change invalidates this cache.
        if(!chartBackgroundTexture||!chartBackgroundTexture!.IsCreated()
            ||!ReferenceEquals(chartBackgroundDocument,Model!.RenderDocument)||Model.IsEditing
            ||chartBackgroundAudio!=snapshot.AudioSeconds||chartBackgroundSeconds!=snapshot.ChartSeconds
            ||chartBackgroundHeight!=previewHeight||chartBackgroundImageRevision!=Surface!.ImageRevision) {
            chartBackgroundTexture=RenderWorkspacePreview(snapshot);ChartBackgroundRenders++;
            chartBackgroundDocument=Model!.RenderDocument;chartBackgroundAudio=snapshot.AudioSeconds;
            chartBackgroundSeconds=snapshot.ChartSeconds;chartBackgroundHeight=previewHeight;
            chartBackgroundImageRevision=Surface!.ImageRevision;
        }
        chartBackground??=new ChartBackgroundDisplay(Bridge.Owner!.ViewCamera);
        chartBackground.Show(chartBackgroundTexture!);
    }
}
