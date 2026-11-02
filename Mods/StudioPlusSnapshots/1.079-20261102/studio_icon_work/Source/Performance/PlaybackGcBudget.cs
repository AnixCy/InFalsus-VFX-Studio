using BepInEx.Configuration;
using InFalsusStudio.TimeGroups;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
namespace InFalsusStudio.Performance {
 // Keep automatic incremental GC enabled. Shorter slices reduce playback
 // stalls; growing heaps restore the engine's original collection budget.
 internal static class PlaybackGcBudget {
  internal static ConfigEntry<bool> Enabled;internal static bool Applied;
  internal static long StartHeap,PeakHeap;static ulong original,assigned;static double next;
  internal static void Init(ConfigFile config){Enabled=config.Bind("Display","LowLatencyPlaybackGc",true,"Shorter automatic incremental GC slices during playback. Restore the engine budget on pause or memory pressure; never disable automatic collection.");}
  internal static void Tick(){
   var boot=TimingGroups.Boot;bool playing=Enabled!=null&&Enabled.Value&&boot!=null&&((StudioTransport)GroupRuntime.Get(boot,"transport")).Playing;
   if(!playing||!GarbageCollector.isIncremental||GarbageCollector.GCMode!=GarbageCollector.Mode.Enabled){Restore();return;}
   if(!Applied){original=GarbageCollector.incrementalTimeSliceNanoseconds;assigned=original;StartHeap=PeakHeap=Profiler.GetMonoUsedSizeLong();next=0;Applied=true;}
   if(GarbageCollector.incrementalTimeSliceNanoseconds!=assigned)original=GarbageCollector.incrementalTimeSliceNanoseconds;
   if(Time.realtimeSinceStartupAsDouble<next)return;next=Time.realtimeSinceStartupAsDouble+.5;
   long heap=Profiler.GetMonoUsedSizeLong();PeakHeap=System.Math.Max(PeakHeap,heap);
   long growth=PeakHeap-StartHeap;ulong budget=growth>32L*1024*1024?1500000UL:growth>16L*1024*1024?750000UL:300000UL;
   assigned=growth>64L*1024*1024?original:System.Math.Min(original,budget);
   GarbageCollector.incrementalTimeSliceNanoseconds=assigned;
  }
  internal static void Restore(){if(!Applied)return;if(GarbageCollector.incrementalTimeSliceNanoseconds==assigned)GarbageCollector.incrementalTimeSliceNanoseconds=original;Applied=false;}
  internal static void Dispose(){Restore();Enabled=null;}
 }
}
