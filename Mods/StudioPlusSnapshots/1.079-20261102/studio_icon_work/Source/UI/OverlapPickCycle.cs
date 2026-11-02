using System;
using System.Collections.Generic;
using System.Linq;
using InFalsusStudio;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool overlapPicking,overlapPicked;Vector2 overlapPointer,overlapAnchor;
  int[] overlapCandidates=new int[0];object overlapHistory;int overlapRevision,overlapMask;
  double overlapTime;Matrix4x4 overlapView,overlapProjection;
  void ResetOverlapPick(){overlapCandidates=new int[0];overlapHistory=null;}
  void CycleOverlapCandidate(){
   if(boot==null||!boot.HasDocument||formal||Transport.Playing||boot.History.InTransaction)return;
   var candidates=FilterIds(Val<List<int>>(boot,"lastPick"),PickMask).Distinct().ToList();
   if(candidates.Count==0)return;
   int current=candidates.IndexOf(Val<int>(boot,"selectedId"));int next=(current+1)%candidates.Count;
   Set(boot,"lastPick",candidates);Set(boot,"cycleIndex",next);Call(boot,"Select",candidates[next]);
   Set(boot,"status",RainL("重叠候选 ","Overlap candidate ")+(next+1)+" / "+candidates.Count);
  }
  List<int> CycleOverlapPick(List<int> ids){
   if(!overlapPicking)return ids;overlapPicked=true;
   var history=boot.History;var camera=boot.ViewCamera;var selected=Val<HashSet<int>>(boot,"selection");int primary=Val<int>(boot,"selectedId");
   bool same=ReferenceEquals(overlapHistory,history)&&overlapRevision==history.Revision&&overlapMask==(int)PickMask&&overlapTime==boot.CurrentMs&&overlapView==camera.worldToCameraMatrix&&overlapProjection==camera.projectionMatrix&&(overlapPointer-overlapAnchor).sqrMagnitude<=36*scale*scale&&overlapCandidates.SequenceEqual(ids);
   int start=same&&selected.Count==1?ids.IndexOf(primary):-1;
   if(!same)overlapAnchor=overlapPointer;
   overlapCandidates=ids.ToArray();overlapHistory=history;overlapRevision=history.Revision;overlapMask=(int)PickMask;overlapTime=boot.CurrentMs;overlapView=camera.worldToCameraMatrix;overlapProjection=camera.projectionMatrix;
   if(start<0||ids.Count<2)return ids;
   // Rotate only the scene click result. Native hit geometry, Ctrl selection,
   // Alt handles, placement and direct PickAll callers retain their behavior.
   int next=(start+1)%ids.Count;var ordered=new List<int>(ids.Count);
   for(int i=0;i<ids.Count;i++)ordered.Add(ids[(next+i)%ids.Count]);return ordered;
  }
  internal static partial class Hooks {
   static void BeginOverlapPick(StudioBootstrap __instance,Event __0){
    if(!Active(__instance))return;I.overlapPicking=false;I.overlapPicked=false;
    if(__0.type!=EventType.MouseDown)return;
    bool plain=!I.formal&&__0.button==0&&!__0.alt&&!__0.shift&&!__0.control&&!__0.command&&!(bool)Prop(__instance,"EditHeld")&&(Get(__instance,"tool").ToString()=="Select"||Val<InFalsusStudio.Core.RadialMenuState>(__instance,"radial").InPointMode&&!Val<InFalsusStudio.Core.PointPlacement>(__instance,"pointPlacement").Pending)&&Get(__instance,"gesture").ToString()=="None"&&GUIUtility.hotControl==0&&(bool)Call(__instance,"AllowedCanvas",__0.mousePosition);
    if(!plain){I.ResetOverlapPick();return;}I.overlapPicking=true;I.overlapPointer=__0.mousePosition;
   }
   static void EndOverlapPick(){if(I==null)return;if(I.overlapPicking&&!I.overlapPicked)I.ResetOverlapPick();I.overlapPicking=false;}
  }
 }
}
