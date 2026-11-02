using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
namespace InFalsusStudio.Performance {
 internal sealed class P1FrameSample {
  internal int Frame;internal double FrameMs,Late,Notes,Effects,Gui,Gpu=-1,Main=-1,Render=-1;internal long Gc=-1,Heap=-1,HeapDelta;internal int Collections;
  internal double Handles,Rulers,Styles,Preview,Refresh;internal long HandlesGc=-1,RulersGc=-1,StylesGc=-1,PreviewGc=-1,RefreshGc=-1;
 }
 internal struct P1SectionState {internal long Tick,Bytes;}
 internal static class P1Profiler {
  internal static bool Active;internal static string LastReport="";
  static readonly List<P1FrameSample> samples=new List<P1FrameSample>(4096);static P1FrameSample current;
  static Func<long> allocated;static bool allocationChecked;static long previousAllocated,previousHeap;static int previousCollections;
  static Func<long> CreateAllocationReader(){var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static);if(method==null)return null;var read=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);long before=read();var calibration=new byte[65536];long after=read();GC.KeepAlive(calibration);return after-before>=65536?read:null;}
  static ProfilerRecorder gc,gpu,main,render;static string output,label;static double end;static int startedFrame;static readonly FrameTiming[] timing=new FrameTiming[1];
  internal static void Begin(string name,string path,double seconds){
   if(Active)Finish();if(!allocationChecked){allocated=CreateAllocationReader();allocationChecked=true;}label=name;output=path;Directory.CreateDirectory(path);samples.Clear();current=null;startedFrame=Time.frameCount;end=Time.realtimeSinceStartupAsDouble+seconds;Active=true;previousAllocated=allocated==null?0:allocated();previousHeap=Profiler.GetMonoUsedSizeLong();previousCollections=GC.CollectionCount(0);
   try{gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);}catch{gc=default(ProfilerRecorder);}
   try{gpu=ProfilerRecorder.StartNew(ProfilerCategory.Render,"GPU Frame Time",1);}catch{gpu=default(ProfilerRecorder);}
   try{main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"CPU Main Thread Frame Time",1);}catch{main=default(ProfilerRecorder);}
   try{render=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"CPU Render Thread Frame Time",1);}catch{render=default(ProfilerRecorder);}
  }
  // Unity exposes the preceding completed frame's interval at the next Update.
  // Attach it to that frame's CPU sections, and exclude both partial boundaries.
  static P1FrameSample Current(){if(current==null||current.Frame!=Time.frameCount){if(current!=null&&current.Frame!=startedFrame){current.FrameMs=Time.unscaledDeltaTime*1000;samples.Add(current);}current=new P1FrameSample{Frame=Time.frameCount,FrameMs=-1};}return current;}
  internal static void Tick(){
   if(!Active)return;if(Time.realtimeSinceStartupAsDouble>=end){Finish();return;}var s=Current();if(gc.Valid)s.Gc=gc.LastValue;else if(allocated!=null){long total=allocated();s.Gc=Math.Max(0,total-previousAllocated);previousAllocated=total;}s.Heap=Profiler.GetMonoUsedSizeLong();s.HeapDelta=s.Heap-previousHeap;previousHeap=s.Heap;int collections=GC.CollectionCount(0);s.Collections=collections-previousCollections;previousCollections=collections;if(gpu.Valid&&gpu.LastValue>0)s.Gpu=gpu.LastValue/1000000d;if(main.Valid&&main.LastValue>0)s.Main=main.LastValue/1000000d;if(render.Valid&&render.LastValue>0)s.Render=render.LastValue/1000000d;
   if(FrameTimingManager.IsFeatureEnabled()){
    FrameTimingManager.CaptureFrameTimings();if(FrameTimingManager.GetLatestTimings(1,timing)>0){if(timing[0].gpuFrameTime>0)s.Gpu=timing[0].gpuFrameTime;s.Main=timing[0].cpuMainThreadFrameTime;s.Render=timing[0].cpuRenderThreadFrameTime;}
   }
   if(Time.realtimeSinceStartupAsDouble>=end)Finish();
  }
  static string N(double n){return n<0?"":n.ToString("R",CultureInfo.InvariantCulture);}
  static double Percentile(IEnumerable<double> values,double q){var sorted=values.Where(v=>v>=0).OrderBy(v=>v).ToArray();return sorted.Length==0?-1:sorted[(int)Math.Ceiling((sorted.Length-1)*q)];}
  internal static string Finish(){
   if(!Active)return LastReport;Active=false;bool gcRecorded=gc.Valid;if(current!=null){if(current.Frame!=Time.frameCount&&current.Frame!=startedFrame){current.FrameMs=Time.unscaledDeltaTime*1000;samples.Add(current);}current=null;}if(gc.Valid)gc.Dispose();if(gpu.Valid)gpu.Dispose();if(main.Valid)main.Dispose();if(render.Valid)render.Dispose();
   string safe=string.Concat(label.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));string csv=Path.Combine(output,safe+".csv");
   var lines=new List<string>{"frame,frame_ms,late_update_ms,notes_ms,effects_ms,gui_ms,gc_bytes,gpu_ms_delayed,cpu_main_ms_delayed,cpu_render_ms_delayed,managed_heap_bytes,managed_heap_net_change_bytes,gc_collections,handles_ms,rulers_ms,overlay_styles_ms,drag_preview_ms,document_refresh_ms"};
   foreach(var s in samples)lines.Add(string.Join(",",new[]{s.Frame.ToString(),N(s.FrameMs),N(s.Late),N(s.Notes),N(s.Effects),N(s.Gui),s.Gc<0?"":s.Gc.ToString(),N(s.Gpu),N(s.Main),N(s.Render),s.Heap<0?"":s.Heap.ToString(),s.Heap<0?"":s.HeapDelta.ToString(),s.Collections.ToString(),N(s.Handles),N(s.Rulers),N(s.Styles),N(s.Preview),N(s.Refresh)}));File.WriteAllLines(csv,lines.ToArray());
   string summary=label+","+samples.Count+","+N(samples.Count==0?-1:samples.Average(s=>s.FrameMs))+","+N(Percentile(samples.Select(s=>s.FrameMs),.95))+","+N(Percentile(samples.Select(s=>s.FrameMs),.99))+","+N(samples.Count==0?-1:samples.Max(s=>s.FrameMs))+","+N(samples.Count==0?-1:samples.Average(s=>s.Late))+","+N(samples.Count==0?-1:samples.Average(s=>s.Notes))+","+N(samples.Count==0?-1:samples.Average(s=>s.Gui))+","+N(Percentile(samples.Select(s=>s.Gpu),.5))+","+samples.Count(s=>s.Gpu>=0)+","+N(Percentile(samples.Select(s=>(double)s.Gc),.5))+","+samples.Count(s=>s.Gc>=0);
   string report=Path.Combine(output,"summary.csv");if(!File.Exists(report))File.WriteAllText(report,"case,samples,mean_frame_ms,p95_ms,p99_ms,max_ms,mean_late_ms,mean_notes_ms,mean_gui_ms,gpu_median_ms,gpu_samples,gc_median_bytes,gc_samples\r\n");File.AppendAllText(report,summary+"\r\n");
   Section("BuildHandles",s=>s.Handles,s=>s.HandlesGc);Section("DrawAirRuler",s=>s.Rulers,s=>s.RulersGc);Section("OverlayStyle",s=>s.Styles,s=>s.StylesGc);Section("DragPreview",s=>s.Preview,s=>s.PreviewGc);Section("AfterDocumentEdit",s=>s.Refresh,s=>s.RefreshGc);
   File.WriteAllText(Path.Combine(output,"counter-capabilities.txt"),"GCSource="+(gcRecorded?"ProfilerRecorder(all threads)":allocated!=null?"GC.GetAllocatedBytesForCurrentThread(main thread delta; calibrated)":"unavailable (thread allocation counter failed a 64KiB calibration)")+"\r\nHeap=Profiler.GetMonoUsedSizeLong; NET change is not allocation count; GC.CollectionCount(0) tracked separately\r\nGPUFrameTime=ProfilerRecorder; asynchronous, ns converted to ms\r\nCPUFrameTime=ProfilerRecorder internal frame timing counters\r\n");LastReport=csv;UnityEngine.Debug.Log("P1 performance capture: "+summary+"; frame timing values are delayed and include sampling overhead; unavailable counters are blank.");return csv;
  }
  static void Section(string name,Func<P1FrameSample,double> cpu,Func<P1FrameSample,long> bytes){string path=Path.Combine(output,"sections.csv");if(!File.Exists(path))File.WriteAllText(path,"case,section,frames,mean_ms,p95_ms,p99_ms,max_ms,allocation_median_bytes,allocation_samples\r\n");File.AppendAllText(path,label+","+name+","+samples.Count+","+N(samples.Count==0?-1:samples.Average(cpu))+","+N(Percentile(samples.Select(cpu),.95))+","+N(Percentile(samples.Select(cpu),.99))+","+N(samples.Count==0?-1:samples.Max(cpu))+","+N(Percentile(samples.Select(s=>(double)bytes(s)),.5))+","+samples.Count(s=>bytes(s)>=0)+"\r\n");}
  static void BeginSection(out P1SectionState __state){__state=Active?new P1SectionState{Tick=Stopwatch.GetTimestamp(),Bytes=allocated==null?-1:allocated()}:default(P1SectionState);}
  static void Add(ref long target,long bytes){if(bytes>=0)target=Math.Max(0,target)+bytes;}
  static Exception EndSection(MethodBase __originalMethod,P1SectionState __state,Exception __exception){if(__state.Tick!=0&&Active){double ms=(Stopwatch.GetTimestamp()-__state.Tick)*1000d/Stopwatch.Frequency;long bytes=__state.Bytes<0||allocated==null?-1:Math.Max(0,allocated()-__state.Bytes);var s=Current();switch(__originalMethod.DeclaringType.Name+"."+__originalMethod.Name){case "NoteRenderer.Draw":s.Notes+=ms;break;case "HitFeedbackRenderer.Draw":s.Effects+=ms;break;case "StudioBootstrap.OnGUI":s.Gui+=ms;break;case "StudioBootstrap.BuildHandles":s.Handles+=ms;Add(ref s.HandlesGc,bytes);break;case "StudioBootstrap.DrawAirRuler":s.Rulers+=ms;Add(ref s.RulersGc,bytes);break;case "StudioBootstrap.OverlayStyle":s.Styles+=ms;Add(ref s.StylesGc,bytes);break;case "StudioBootstrap.PreviewNoteDrag":case "StudioBootstrap.PreviewEaseSelector":s.Preview+=ms;Add(ref s.PreviewGc,bytes);break;case "StudioBootstrap.AfterDocumentEdit":s.Refresh+=ms;Add(ref s.RefreshGc,bytes);break;default:s.Late+=ms;break;}}return __exception;}
  internal static void Install(Harmony h){var f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;foreach(var item in new[]{Tuple.Create(typeof(NoteRenderer),"Draw"),Tuple.Create(typeof(HitFeedbackRenderer),"Draw"),Tuple.Create(typeof(StudioBootstrap),"LateUpdate"),Tuple.Create(typeof(StudioBootstrap),"OnGUI"),Tuple.Create(typeof(StudioBootstrap),"BuildHandles"),Tuple.Create(typeof(StudioBootstrap),"DrawAirRuler"),Tuple.Create(typeof(StudioBootstrap),"OverlayStyle"),Tuple.Create(typeof(StudioBootstrap),"PreviewNoteDrag"),Tuple.Create(typeof(StudioBootstrap),"AfterDocumentEdit")})h.Patch(item.Item1.GetMethod(item.Item2,f),new HarmonyMethod(typeof(P1Profiler).GetMethod("BeginSection",f)){priority=Priority.First},null,null,new HarmonyMethod(typeof(P1Profiler).GetMethod("EndSection",f)),null);}
 }
}
