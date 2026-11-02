using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 internal static class ClipboardGroups {
  internal sealed class Snapshot {
   internal string[] Names;
   internal readonly Dictionary<string,string> Headers=new Dictionary<string,string>(StringComparer.Ordinal);
  }
  static readonly ConditionalWeakTable<NoteClipboard,Snapshot> snapshots=new ConditionalWeakTable<NoteClipboard,Snapshot>();
  static readonly ConditionalWeakTable<SpcDocument,object> pasted=new ConditionalWeakTable<SpcDocument,object>();
  static void Copied(NoteClipboard __instance,SpcDocument __0,IEnumerable<int> __1){
   snapshots.Remove(__instance);if(__instance.Count==0)return;
   // Use exactly the order used by NoteClipboard.Copy, not the input ID order.
   var selected=new HashSet<int>(__1);var model=TimingGroups.Model(__0);
   var notes=TimingGroups.Events(__0).Where(e=>e.IsNote&&selected.Contains(e.SourceId)).ToArray();
   if(notes.Length!=__instance.Count)throw new InvalidOperationException("Clipboard membership capture does not match copied notes.");
   var snapshot=new Snapshot{Names=notes.Select(e=>model.Of(e.SourceId)).ToArray()};
   foreach(string name in snapshot.Names.Distinct())if(name!=""){
    var block=model.Find(name);if(block==null)throw new InvalidOperationException("Copied timing group is missing.");
    snapshot.Headers.Add(name,__0.SourceLine(block.Start));
   }
   snapshots.Add(__instance,snapshot);
  }
  internal static Snapshot Of(NoteClipboard clipboard){Snapshot result;return snapshots.TryGetValue(clipboard,out result)?result:null;}
  internal static void Prepare(SpcDocument doc,Snapshot snapshot){
   if(snapshot==null)return;TimingGroups.Validate(doc);var model=TimingGroups.Model(doc);
   // Existing groups keep their properties and timings. Missing groups get
   // the captured header (including noinput/noclip), without copying timings.
   foreach(var group in snapshot.Headers)if(model.Find(group.Key)==null){doc.Append(group.Value);doc.Append("groupend()");}
  }
  internal static void Placed(SpcDocument doc,int[] ids,Snapshot snapshot){
   if(snapshot!=null){if(ids.Length!=snapshot.Names.Length)throw new InvalidOperationException("Clipboard membership changed.");TimingGroups.MoveMany(doc,ids,snapshot.Names);}
   pasted.Remove(doc);pasted.Add(doc,new object());
  }
  internal static bool ShouldNormalize(SpcDocument doc){
   // A paste must retain the captured group IDs even when its new endpoints
   // happen to touch another authored group. Consume this only for this draft.
   object marker;if(!pasted.TryGetValue(doc,out marker))return true;pasted.Remove(doc);return false;
  }
  static void FloatingPlaced(FloatingNotes __instance,SpcDocument __0){if(__instance.Cut){pasted.Remove(__0);pasted.Add(__0,new object());}}
  internal static void Install(Harmony harmony){
   harmony.Patch(typeof(NoteClipboard).GetMethod("Copy"),postfix:new HarmonyMethod(typeof(ClipboardGroups).GetMethod("Copied",TimingGroups.F)));
   harmony.Patch(typeof(FloatingNotes).GetMethod("Place"),postfix:new HarmonyMethod(typeof(ClipboardGroups).GetMethod("FloatingPlaced",TimingGroups.F)));
  }
 }
}
