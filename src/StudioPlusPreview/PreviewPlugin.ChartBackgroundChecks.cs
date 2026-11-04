using InFalsus.Presentation;
using InFalsus.Presentation.Authoring;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NVector4=System.Numerics.Vector4;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckChartBackground(Action<bool,string> check)
    {
        var savedModel=Model;var savedSurface=Surface;bool savedView=Bridge.AuthoringView,savedEnabled=chartBackgroundEnabled;
        int savedHeight=previewHeight;bool savedPreviewEnabled=previewEnabled;
        string? savedSettings=File.Exists(PreviewSettingsFile)?File.ReadAllText(PreviewSettingsFile):null;
        var camera=Bridge.Owner!.ViewCamera;var savedRect=camera.rect;var savedTarget=camera.targetTexture;
        var target=new RenderTexture(256,144,24,RenderTextureFormat.ARGBHalf);target.Create();
        string HostFrame() {
            try {
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                return HashTarget(target);
            }finally{camera.targetTexture=savedTarget;camera.rect=savedRect;}
        }
        try {
            HideChartBackground();Bridge.SetView(false);
            Model=new PreviewModel(folder,Application.unityVersion,QualitySettings.activeColorSpace.ToString(),legacy:true);
            previewHeight=720;
            chartBackgroundEnabled=false;TickChartBackground();
            check(chartBackground?.Visible!=true,"chart background defaults to the untouched host backdrop");
            string baseline=HostFrame();int before=ChartBackgroundRenders;
            using(var edit=Model.Session.BeginEdit(Model.Session.Revision,"chart preview fixture")) {
                edit.AddKey("background/_Brightness",KeyPosition.Seconds(0),new ParameterValue(new NVector4(.4f,0,0,0)),Easing.Linear);
                edit.AddKey("background/_Brightness",KeyPosition.Seconds(2),new ParameterValue(new NVector4(1.8f,0,0,0)),Easing.Linear);
                edit.Commit();
            }
            Model.Refresh();string artwork=JsonConvert.SerializeObject(Model.Session.Snapshot(),Json);
            long revision=Model.Session.Revision;bool dirty=Dirty;
            SetChartBackgroundPreview(true);TickChartBackground();
            check(chartBackground?.Visible==true&&chartBackground.Texture is RenderTexture,"chart toggle displays the isolated effect texture behind chart geometry");
            check(HostFrame()!=baseline,"enabled chart background changes actual host camera GPU output");
            check(camera.enabled&&camera.rect==savedRect&&Hooks.ChartTick(),"chart background keeps the host camera and chart editor update enabled");
            var snapshot=Bridge.Snapshot() with{ChartSeconds=1,AudioSeconds=1};
            PresentChartBackground(snapshot);string halfway=HashTarget(chartBackgroundTexture!);
            check(Math.Abs(Surface!.Material.GetFloat("_Brightness")-1.1f)<.0001,"chart background samples interpolated keyframes at the supplied chart progress");
            var first=chartBackgroundTexture;int count=ChartBackgroundRenders;PresentChartBackground(snapshot);
            check(ChartBackgroundRenders==count&&chartBackgroundTexture==first,"paused chart progress reuses both GPU output and render targets");
            PresentChartBackground(snapshot with{ChartSeconds=2,AudioSeconds=2,Transport=TransportState.Running});
            check(HashTarget(chartBackgroundTexture!)!=halfway,"changing playback progress updates the rendered effect");
            PresentChartBackground(snapshot);
            check(HashTarget(chartBackgroundTexture!)==halfway,"backward chart seek restores deterministic background output");
            Bridge.SetView(true);TickChartBackground();
            check(chartBackground?.Visible!=true&&!Hooks.ChartTick(),"authoring view hides chart display and retains exclusive chart-input isolation");
            Bridge.SetView(false);TickChartBackground();
            check(chartBackground?.Visible==true,"returning to chart view restores its enabled background preview");
            chartBackgroundEnabled=false;LoadPreviewSettings();
            check(chartBackgroundEnabled&&previewHeight==720,"chart preview preference persists alongside resolution without resetting it");
            SetChartBackgroundPreview(false);
            check(chartBackground?.Visible!=true&&(!first||!first!.IsCreated()),"turning chart preview off hides its display and releases render targets");
            check(HostFrame()==baseline,"turning chart preview off restores the exact default host camera pixels");
            check(Model.Session.Revision==revision&&Dirty==dirty&&JsonConvert.SerializeObject(Model.Session.Snapshot(),Json)==artwork,
                "view-only toggle and seeks leave animation data revision and dirty state unchanged");
            SetChartBackgroundPreview(true);Model.RenderDocument.Nodes.First(n=>n.Target=="background").Material="missing-chart-preview-fixture";
            TickChartBackground();
            check(!chartBackgroundEnabled&&chartBackground?.Visible!=true,"render failure disables chart preview and falls back to the host backdrop");
            check(HostFrame()==baseline&&camera.enabled,"render failure preserves default rendering and the live chart camera");
            check(ChartBackgroundRenders>before,"chart background exercised real rendering without playing audio");
        }finally {
            HideChartBackground();chartBackground?.Dispose();chartBackground=null;
            Surface=savedSurface;Model=savedModel;chartBackgroundEnabled=savedEnabled;previewHeight=savedHeight;previewEnabled=savedPreviewEnabled;
            Bridge.SetView(savedView);camera.targetTexture=savedTarget;camera.rect=savedRect;
            target.Release();DestroyImmediate(target);
            if(savedSettings==null){if(File.Exists(PreviewSettingsFile))File.Delete(PreviewSettingsFile);}
            else File.WriteAllText(PreviewSettingsFile,savedSettings);
        }
    }
}
