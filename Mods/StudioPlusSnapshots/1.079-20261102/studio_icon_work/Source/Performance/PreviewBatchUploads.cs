using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.NativeVisual;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class PreviewBatchUploads {
  internal static bool Enabled=true;internal static long Skipped;
  sealed class State {internal MeshRenderer Renderer;}
  static readonly ConditionalWeakTable<MeshBatch,State> states=new ConditionalWeakTable<MeshBatch,State>();
  static bool Commit(MeshBatch __instance){
   if(!Enabled||!PreviewFrustum.Playing||!NativeVisualPlugin.Active||!NativeVisualPlugin.NotesEnabled.Value)return true;
   var renderer=states.GetValue(__instance,b=>new State{Renderer=b.Object.GetComponent<MeshRenderer>()}).Renderer;
   if(__instance.Triangles.Count!=0&&(renderer==null||renderer.enabled))return true;
   // CPU surfaces already supplied native notes and picking. These legacy
   // renderers are disabled, or have no triangles after native replacement.
   if(__instance.Object.activeSelf)__instance.Object.SetActive(false);Skipped++;return false;
  }
  internal static void Install(Harmony h){h.Patch(typeof(MeshBatch).GetMethod("Commit"),new HarmonyMethod(typeof(PreviewBatchUploads).GetMethod("Commit",TimingGroups.F)){priority=Priority.Last-100});}
 }
}
