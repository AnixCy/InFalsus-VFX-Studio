using System;
using System.Collections.Generic;
using System.Linq;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 // Explicit authoring operation. Existing assignments never participate as predecessors.
 // Allocate IDs globally to avoid collisions even with older readers that ignore timinggroups.
 public static class UngroupedSkyGrouping {
  public struct Result { public int Notes,Groups,Preserved; }
  public static Result Assign(SpcDocument document){
   TimingGroups.Validate(document);
   var all=document.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).ToArray();
   var notes=all.Where(SkyGroups.Automatic).OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
   var result=new Result{Notes=notes.Length,Preserved=all.Length-notes.Length};
   // Validate every candidate before mutating a token, including direct callers outside history.
   foreach(var n in notes){string reason=ChartMath.InvalidReason(n)??AuthoringSafety.Reason(n);if(reason!=null)
    throw new ArgumentException("Cannot group SkyArea at source line "+(document.SourceIndex(n.SourceId)+1)+": "+reason);}
   var used=new HashSet<int>(all.Select(SkyGroups.GroupOf).Where(g=>g>=0));
   var assignments=new Dictionary<int,int>();
   var tails=new Dictionary<string,Dictionary<long,List<SpcEvent>>>(StringComparer.Ordinal);
   var model=TimingGroups.Model(document);long next=0;
   foreach(var n in notes){
    string scope=model.Of(n.SourceId);Dictionary<long,List<SpcEvent>> scoped;
    if(!tails.TryGetValue(scope,out scoped))tails[scope]=scoped=new Dictionary<long,List<SpcEvent>>();
    SpcEvent prior=null;long tick=(long)Math.Floor(n.TimeMs/SkyGroups.TimeTolerance);
    for(long k=tick-1;k<=tick+1;k++){List<SpcEvent> row;if(!scoped.TryGetValue(k,out row))continue;
     foreach(var candidate in row)if(SkyGroups.Joins(candidate,n)&&(prior==null||candidate.TimeMs>prior.TimeMs||candidate.TimeMs==prior.TimeMs&&candidate.SourceId<prior.SourceId))prior=candidate;
    }
    int group;
    if(prior!=null)group=assignments[prior.SourceId];
    else {while(next<=int.MaxValue&&used.Contains((int)next))next++;if(next>int.MaxValue)throw new ArgumentException("No unused SkyArea group ID remains.");group=(int)next++;used.Add(group);result.Groups++;}
    assignments.Add(n.SourceId,group);
    long tail=(long)Math.Floor(n.EndMs/SkyGroups.TimeTolerance);List<SpcEvent> bucket;
    if(!scoped.TryGetValue(tail,out bucket))scoped[tail]=bucket=new List<SpcEvent>();bucket.Add(n);
   }
   foreach(var n in notes)document.SetOptionalNumber(n.SourceId,10,assignments[n.SourceId]);
   return result;
  }
 }
 public sealed partial class NativeEditorUIPlugin {
  void GroupUngroupedSky(){
   if(!(bool)Call(boot,"PrepareAuthoringCommand"))return;
   var result=default(UngroupedSkyGrouping.Result);
   if(!(bool)Call(boot,"Change",new Action<SpcDocument>(d=>result=UngroupedSkyGrouping.Assign(d))))return;
   bool cn=Val<bool>(boot,"chinese");
   Set(boot,"status",result.Notes==0?(cn?"没有未分组的 SkyArea；已有分组保持不变。":"No ungrouped SkyAreas; existing groups are unchanged."):
    (cn?"已将 "+result.Notes+" 条未分组 SkyArea 分为 "+result.Groups+" 组；保留 "+result.Preserved+" 条已有分组物件。可一次撤销，保存才写盘。":
    "Grouped "+result.Notes+" ungrouped SkyAreas into "+result.Groups+" groups; preserved "+result.Preserved+" already assigned notes. One undo step; Save writes disk."));
  }
  internal static partial class Hooks {
   static bool GroupUngroupedOnly(StudioBootstrap __instance){if(!Active(__instance))return true;I.GroupUngroupedSky();return false;}
  }
 }
}
