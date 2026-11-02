using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 internal static class CommitValidation {
  internal static bool Enabled=true;
  static IEnumerable<SpcEvent> Changed(SpcDocument before,SpcDocument after){
   var prior=TimingGroups.Model(before);var next=TimingGroups.Model(after);
   foreach(var row in next.Source.Values){
    GroupSourceRow old;if(prior.Source.TryGetValue(row.Id,out old)&&old.Text==row.Text)continue;
    var note=DocumentAcceleration.EventAt(after,row.Id);if(note!=null)yield return note;
   }
  }
  static bool Validate(SpcDocument __0,SpcDocument __1){
   if(!Enabled||DragPreview.Working==__1)return true;
   foreach(var e in Changed(__0,__1)){
    string reason=AuthoringSafety.Reason(e);if(reason!=null)throw new ArgumentException(reason);
    if(e.Kind==EventKind.Unknown&&(e.Name=="hold"||e.Name=="skyarea"||e.Name=="tap"||e.Name=="flick"||e.Name=="bpm"||e.Name=="track"||e.Name=="chart"))throw new ArgumentException("Invalid numeric statement. Previous value retained.");
   }return false;
  }
  internal static HashSet<string> ChangedSkyScopes(SpcDocument before,SpcDocument after){
   var names=new HashSet<string>(StringComparer.Ordinal);var model=TimingGroups.Model(after);
   foreach(var e in Changed(before,after)){
    if(e.Kind!=EventKind.SkyArea||SkyGroups.GroupOf(e)<0||ChartMath.InvalidReason(e)!=null)continue;
    var old=before.Contains(e.SourceId)?DocumentAcceleration.EventAt(before,e.SourceId):null;
    if(old==null||old.Kind!=EventKind.SkyArea||Enumerable.Range(0,10).Any(i=>old.Get(i)!=e.Get(i)))names.Add(model.Of(e.SourceId));
   }return names;
  }
  internal static void Install(Harmony h){h.Patch(typeof(AuthoringSafety).GetMethod("ValidateChanged"),new HarmonyMethod(typeof(CommitValidation).GetMethod("Validate",TimingGroups.F)));}
 }
 internal static class EventValues {
  internal static bool Same(SpcEvent[] a,SpcEvent[] b){
   if(a.Length!=b.Length)return false;
   for(int i=0;i<a.Length;i++)if(a[i].SourceId!=b[i].SourceId||a[i].Kind!=b[i].Kind||a[i].Name!=b[i].Name||!a[i].Args.SequenceEqual(b[i].Args))return false;
   return true;
  }
 }
}
