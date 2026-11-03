using System.Diagnostics;
using InFalsus.Presentation;
using Newtonsoft.Json;
using UnityEngine;
using Object=UnityEngine.Object;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    // Explicit bounded benchmark. Samples pixels numerically; no image files or audio.
    string performanceProject="",performanceOutput="";
    readonly List<object> performanceSamples=new();
    double[] performanceTimes=Array.Empty<double>();
    int performanceStep;
    double performanceStart;
    Texture2D? performancePixel;
    int performanceWarmupDraws;
    double performanceWarmupMs;
    void InitPerformance()
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-performance-project");if(at<0)return;
        performanceProject=Path.GetFullPath(args[at+1]);at=Array.IndexOf(args,"-vfx-performance-output");
        if(at<0)throw new ArgumentException("Performance output is required.");performanceOutput=Path.GetFullPath(args[at+1]);
        if(File.Exists(performanceOutput))throw new IOException("Performance output already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(performanceOutput)!);AudioListener.volume=0;
    }
    void TickPerformance()
    {
        if(performanceOutput.Length==0||Model==null||Surface==null||StudioUiTheme.TryBind()==null)return;
        try {
            if(performanceTimes.Length==0) {
                var data=Decode(File.ReadAllText(performanceProject));Model.Replace(Store().Open(performanceProject).Session);
                Surface.ApplyImage(data.BackgroundPath.Length>0?Surface.PrepareImage(data.BackgroundPath):null);imagePath=savedImagePath=data.BackgroundPath;
                selectedEffect="background";selectedParameter="background/_Brightness";selectedKey="";
                Bridge.SetView(true);autoSourceUpdate=false;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
                performanceTimes=Model.Keys.Select(k=>k.Seconds).Concat(Model.RenderDocument.Nodes.Select(n=>n.StartSeconds)).Append(0d).Distinct().OrderBy(t=>t).Take(512).ToArray();
                performancePixel=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);performanceStart=Time.realtimeSinceStartupAsDouble;
                if(Environment.GetCommandLineArgs().Contains("-vfx-performance-prime")) {
                    long warm=Stopwatch.GetTimestamp();
                    performanceWarmupDraws=Surface.PrimePlayback(Model,Bridge.Snapshot(),1920,1080);
                    performanceWarmupMs=(Stopwatch.GetTimestamp()-warm)*1000d/Stopwatch.Frequency;
                }
                return;
            }
            if(Time.realtimeSinceStartupAsDouble-performanceStart>45)throw new TimeoutException("Bounded render benchmark timed out.");
            double seconds=performanceTimes[performanceStep%performanceTimes.Length];
            double transportSeconds=seconds-Model.RenderDocument.OffsetSeconds;
            var snapshot=Bridge.Snapshot() with{AudioSeconds=transportSeconds,ChartSeconds=transportSeconds};
            long start=Stopwatch.GetTimestamp();Model.Sample(seconds);
            double evaluation=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
            start=Stopwatch.GetTimestamp();var target=Surface.Render(Model,snapshot,1920,1080);
            double submit=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
            var previous=RenderTexture.active;start=Stopwatch.GetTimestamp();
            try{RenderTexture.active=target;performancePixel!.ReadPixels(new Rect(0,0,1,1),0,0);performancePixel.Apply();}
            finally{RenderTexture.active=previous;}
            double completion=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
            performanceSamples.Add(new{pass=performanceStep<performanceTimes.Length?"cold":"warm",seconds,evaluationMs=evaluation,submitMs=submit,completionMs=completion});
            if(++performanceStep>=performanceTimes.Length*2)FinishPerformance(null);
        }catch(Exception e){FinishPerformance(e.ToString());}
    }
    void FinishPerformance(string? error)
    {
        File.WriteAllText(performanceOutput,JsonConvert.SerializeObject(new{ok=error==null,error,samples=performanceSamples,
            benchmark="isolated-evaluation-and-GPU-completion; not GUI FPS",unity=Application.unityVersion,width=1920,height=1080,
            graphics=SystemInfo.graphicsDeviceName,keys=Model!.Keys.Length,warmupDraws=performanceWarmupDraws,warmupMs=performanceWarmupMs,
            visualInspections=0,screenshots=0,audioPlayed=false},Formatting.Indented));
        if(performancePixel)Object.DestroyImmediate(performancePixel);performancePixel=null;
        performanceOutput="";quitPermit=true;quitPermitExpiresAfter=Time.frameCount+1;Application.Quit(error==null?0:2);
    }
}
