using System;using System.IO;using BepInEx.Configuration;using HarmonyLib;using UnityEngine;using InFalsusStudio;using InFalsusStudio.NativeEditorUI;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using UnityEngine.Rendering.RenderGraphModule;
namespace InFalsusStudio.VideoRender {
 internal static class RenderClock {
  internal static bool Active;internal static double NowMs,Elapsed;internal static int Width=1920,Height=1080;
  internal static int SurfaceWidth{get{return CaptureCanvas.Active?Width:Screen.width;}}
  internal static int SurfaceHeight{get{return CaptureCanvas.Active?Height:Screen.height;}}
  internal static float Realtime{get{return Active?(float)Elapsed:UnityEngine.Time.realtimeSinceStartup;}}
  static readonly ClockPass shaderClock=new ClockPass();
  internal static void Dispose(){RenderPipelineManager.beginCameraRendering-=BeginCamera;}
  static void BeginCamera(ScriptableRenderContext context,Camera camera){if(Active&&camera.targetTexture!=null)camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(shaderClock);}
  internal static void Install(Harmony h){RenderPipelineManager.beginCameraRendering+=BeginCamera;var f=NativeEditorUIPlugin.F;h.Patch(typeof(StudioTransport).GetProperty("TimeMs").GetGetMethod(),new HarmonyMethod(typeof(RenderClock).GetMethod("Time",f)){priority=Priority.First});h.Patch(typeof(StudioBootstrap).GetMethod("LateUpdate",f),new HarmonyMethod(typeof(RenderClock).GetMethod("Late",f)){priority=Priority.First});}
  static bool Time(ref double __result){if(!Active)return true;__result=NowMs;return false;}
  static bool Late(){return !Active;}
  sealed class ClockPass:ScriptableRenderPass {
   sealed class Data{internal float elapsed,dt;}
   internal ClockPass(){renderPassEvent=RenderPassEvent.BeforeRenderingOpaques;}
   static Vector4 Clock(float t){return new Vector4(t/20,t,t*2,t*3);}
   public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame){Data data;using(var builder=graph.AddRasterRenderPass<Data>("Offline deterministic shader clock",out data)){data.elapsed=(float)Elapsed;data.dt=1f/Math.Max(1,FrameRate);builder.SetRenderAttachment(frame.Get<UniversalResourceData>().activeColorTexture,0,AccessFlags.ReadWrite);builder.AllowGlobalStateModification(true);builder.AllowPassCulling(false);builder.SetRenderFunc<Data>(delegate(Data d,RasterGraphContext ctx){float t=d.elapsed;ctx.cmd.SetGlobalVector("_Time",Clock(t));ctx.cmd.SetGlobalVector("_TimeParameters",new Vector4(t,Mathf.Sin(t),Mathf.Cos(t),0));ctx.cmd.SetGlobalVector("_LastTimeParameters",new Vector4(t-d.dt,Mathf.Sin(t-d.dt),Mathf.Cos(t-d.dt),0));ctx.cmd.SetGlobalVector("unity_DeltaTime",new Vector4(d.dt,1/d.dt,d.dt,1/d.dt));ctx.cmd.SetGlobalVector("_SinTime",new Vector4(Mathf.Sin(t/8),Mathf.Sin(t/4),Mathf.Sin(t/2),Mathf.Sin(t)));ctx.cmd.SetGlobalVector("_CosTime",new Vector4(Mathf.Cos(t/8),Mathf.Cos(t/4),Mathf.Cos(t/2),Mathf.Cos(t)));});}}
   public override void Execute(ScriptableRenderContext context,ref RenderingData data){var cmd=CommandBufferPool.Get("Offline shader clock");float t=(float)Elapsed;cmd.SetGlobalVector("_Time",Clock(t));cmd.SetGlobalVector("_TimeParameters",new Vector4(t,Mathf.Sin(t),Mathf.Cos(t),0));context.ExecuteCommandBuffer(cmd);CommandBufferPool.Release(cmd);}
  }
  internal static int FrameRate=60;

 }
 internal sealed class VideoRenderPreferences {
  internal ConfigEntry<int> Resolution,Fps,Encoder,Quality,Range;internal ConfigEntry<bool> Hud,Sounds,Fade;internal ConfigEntry<string> Ffmpeg;
  internal VideoRenderPreferences(ConfigFile c){Resolution=c.Bind("VideoRender","Resolution",1,"0=720p,1=1080p,2=1440p,3=4K");Fps=c.Bind("VideoRender","FPS",60,"30 / 60 / 120");Encoder=c.Bind("VideoRender","Encoder",0,"0=H.264 CPU,1=NVENC H.264,2=HEVC CPU");Quality=c.Bind("VideoRender","Quality",18,"CRF or CQ, 12..28");Range=c.Bind("VideoRender","Range",0,"0=whole,1=loop,2=custom");Hud=c.Bind("VideoRender","HUD",true,"Include game HUD");Sounds=c.Bind("VideoRender","KeySounds",true,"Mix chart key sounds offline");Fade=c.Bind("VideoRender","Fade",true,"Half-second opening/closing fades");Ffmpeg=c.Bind("VideoRender","FFmpeg","","Optional ffmpeg executable path");}
  internal string FindEncoder(){if(File.Exists(Ffmpeg.Value))return Path.GetFullPath(Ffmpeg.Value);string local=Path.Combine(BepInEx.Paths.GameRootPath,"Tools","ffmpeg.exe");if(File.Exists(local))return local;foreach(string path in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)){try{string candidate=Path.Combine(path.Trim('"'),"ffmpeg.exe");if(File.Exists(candidate))return candidate;}catch{}}return null;}
  internal static int FpsValue(int value){return value==30||value==120?value:60;}
 }
 internal sealed class RenderJob {
  internal double Start,End,Rate;internal int Width,Height,Fps,Codec,Crf,Frames;internal bool Hud,Sounds,Fade;internal string Encoder,Output,TempVideo,TempAudio;
  internal double Seconds{get{return (double)Frames/Fps;}}
  internal static int FrameCount(double start,double end,double rate,int fps){if(double.IsNaN(start)||double.IsNaN(end)||double.IsNaN(rate)||double.IsInfinity(end)||start<0||end<=start||rate<=0)throw new ArgumentException("Invalid render time range");double frames=Math.Ceiling((end-start)*fps/(1000*rate));if(frames<1||frames>int.MaxValue)throw new ArgumentException("Render range exceeds frame limit");return (int)frames;}
 }
}

