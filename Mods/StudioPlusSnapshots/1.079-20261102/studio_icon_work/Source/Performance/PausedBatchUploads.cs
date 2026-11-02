using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 // Compare the final CPU data after native overlay retirement. Shader state,
 // note picking, batch order and every source vertex remain untouched.
 internal static class PausedBatchUploads {
  internal static bool Enabled=true;internal static int Hits,Misses;
  sealed class Snapshot {internal readonly List<Vector3> V=new List<Vector3>();internal readonly List<Color> C=new List<Color>();internal readonly List<Vector2> U=new List<Vector2>(),S=new List<Vector2>();internal readonly List<int> I=new List<int>();}
  static readonly ConditionalWeakTable<MeshBatch,Snapshot> saved=new ConditionalWeakTable<MeshBatch,Snapshot>();
  static bool Same<T>(List<T> a,List<T> b){if(b==null||a.Count!=b.Count)return false;var equality=EqualityComparer<T>.Default;for(int i=0;i<b.Count;i++)if(!equality.Equals(a[i],b[i]))return false;return true;}
  static bool Commit(MeshBatch __instance,out bool __state){
   __state=false;if(!Enabled||!SkyMeshCache.Active){saved.Remove(__instance);return true;}
   Snapshot old;if(saved.TryGetValue(__instance,out old)&&Same(__instance.Vertices,old.V)&&Same(__instance.Colors,old.C)&&Same(__instance.Uvs,old.U)&&Same(__instance.States,old.S)&&Same(__instance.Triangles,old.I)){
    Hits++;bool active=__instance.Vertices.Count!=0;if(__instance.Object.activeSelf!=active)__instance.Object.SetActive(active);return false;
   }
   Misses++;__state=true;return true;
  }
  static void Copy<T>(List<T> source,List<T> target){target.Clear();target.AddRange(source);}
  static void Committed(MeshBatch __instance,bool __state){if(!__state)return;var s=saved.GetValue(__instance,b=>new Snapshot());Copy(__instance.Vertices,s.V);Copy(__instance.Colors,s.C);Copy(__instance.Uvs,s.U);Copy(__instance.States,s.S);Copy(__instance.Triangles,s.I);}
  internal static void Install(Harmony h){h.Patch(typeof(MeshBatch).GetMethod("Commit"),new HarmonyMethod(typeof(PausedBatchUploads).GetMethod("Commit",TimingGroups.F)){priority=Priority.Last},new HarmonyMethod(typeof(PausedBatchUploads).GetMethod("Committed",TimingGroups.F)));}
 }
}
