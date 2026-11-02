using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 // Cache syntax parsing, never the caller-owned mutable event objects. Text, IDs,
 // separators and ordering remain owned by the unmodified concrete document.
 internal static class DocumentAcceleration {
  internal static bool Enabled=true;
  static readonly BindingFlags F=TimingGroups.F;
  static readonly FieldInfo lines=typeof(SpcDocument).GetField("lines",F),recovered=typeof(SpcDocument).GetField("<RecoveredIcpTimingCount>k__BackingField",F);
  static readonly Type row=lines.FieldType.GetGenericArguments()[0];
  static readonly FieldInfo id=row.GetField("Id",F),text=row.GetField("Text",F);
  sealed class Parsed {internal string Text;internal SpcEvent Event;internal string[] Diagnostics;internal int Recovered;}
  sealed class State {internal Dictionary<int,int> Index;internal int Count=-1,Slots;internal readonly List<int> Removed=new List<int>();internal Dictionary<int,Parsed> Parsed=new Dictionary<int,Parsed>();}
  static readonly ConditionalWeakTable<SpcDocument,State> states=new ConditionalWeakTable<SpcDocument,State>();
  static State Of(SpcDocument d){return states.GetValue(d,x=>new State());}
  internal static void Appended(SpcDocument d,int key,int priorCount){
   var s=Of(d);
   if(s.Index!=null&&s.Count==priorCount){s.Index[key]=s.Slots++;s.Count=priorCount+1;}
   else{s.Index=null;s.Count=-1;}
  }
  internal static void Invalidated(SpcDocument d,int deleted){
   var s=Of(d);int slot;
   if(s.Index!=null&&s.Index.TryGetValue(deleted,out slot)&&s.Count==d.LineCount+1){s.Index.Remove(deleted);int at=s.Removed.BinarySearch(slot);s.Removed.Insert(at<0?~at:at,slot);s.Count=d.LineCount;}
   else if(s.Count!=d.LineCount){s.Index=null;s.Count=-1;s.Removed.Clear();}
   s.Parsed.Remove(deleted);
  }
  static bool SourceIndex(SpcDocument __instance,int __0,ref int __result){
   if(!Enabled)return true;var s=Of(__instance);var rows=(IList)lines.GetValue(__instance);int at;
   if(s.Index!=null&&s.Count==rows.Count){if(!s.Index.TryGetValue(__0,out at)){__result=-1;return false;}int removed=s.Removed.BinarySearch(at);at-=removed<0?~removed:removed;if(at>=0&&at<rows.Count&&(int)id.GetValue(rows[at])==__0){__result=at;return false;}}
   s.Index=new Dictionary<int,int>(rows.Count);s.Count=s.Slots=rows.Count;s.Removed.Clear();for(int i=0;i<rows.Count;i++)s.Index[(int)id.GetValue(rows[i])]=i;
   __result=s.Index.TryGetValue(__0,out at)?at:-1;return false;
  }
  [ThreadStatic]static bool bypass;
  internal static bool ParsingRaw {get{return bypass;}}
  static Parsed ParsedAt(SpcDocument d,int key,string raw){var s=Of(d);Parsed p;
   if(!s.Parsed.TryGetValue(key,out p)||p.Text!=raw){
    bypass=true;try{bool guard=raw.StartsWith("\uFEFF",StringComparison.Ordinal);var one=SpcDocument.Parse(guard?"// cache row guard\n"+raw:raw);var events=one.ReadEvents();p=new Parsed{Text=raw,Event=events.FirstOrDefault(),Recovered=one.RecoveredIcpTimingCount,Diagnostics=one.Diagnostics.Where(x=>x!="No valid chart(bpm,beats) header.").Select(x=>guard&&x.StartsWith("Line 2:",StringComparison.Ordinal)?"Line 1"+x.Substring(6):x).ToArray()};s.Parsed[key]=p;}finally{bypass=false;}
   }
   return p;
  }
  internal static void RestoreDiagnostics(SpcDocument d,SpcDocument original){d.Diagnostics.Clear();d.Diagnostics.AddRange(original.Diagnostics);recovered.SetValue(d,original.RecoveredIcpTimingCount);}
  internal static void Restore(SpcDocument target,SpcDocument original,IEnumerable<int> ids){var rows=(IList)lines.GetValue(target);foreach(int key in ids){int at=target.SourceIndex(key);if(at<0)throw new InvalidOperationException("Drag source structure changed.");text.SetValue(rows[at],original.SourceLine(key));TimingGroups.NumericChanged(target,key);}}
  internal static SpcEvent EventAt(SpcDocument d,int key){var p=ParsedAt(d,key,d.SourceLine(key));return p.Event==null?null:new SpcEvent{SourceId=key,Kind=p.Event.Kind,Name=p.Event.Name,Args=(double[])p.Event.Args.Clone()};}
  static bool Read(SpcDocument __instance,ref List<SpcEvent> __result){
   if(!Enabled||bypass)return true;var s=Of(__instance);var rows=(IList)lines.GetValue(__instance);var result=new List<SpcEvent>(DragPreview.EditingIds==null?rows.Count:DragPreview.EditingIds.Count);__instance.Diagnostics.Clear();int recovery=0;
   var keys=DragPreview.EditingIds==null?Enumerable.Range(0,rows.Count):DragPreview.EditingIds.Select(__instance.SourceIndex).Where(i=>i>=0).OrderBy(i=>i);
   foreach(int i in keys){
    int key=(int)id.GetValue(rows[i]);string raw=(string)text.GetValue(rows[i]);Parsed p;
    if(DragPreview.EditingIds!=null&&!DragPreview.EditingIds.Contains(key))continue;
    p=ParsedAt(__instance,key,raw);
    if(p.Event!=null)result.Add(new SpcEvent{SourceId=key,Kind=p.Event.Kind,Name=p.Event.Name,Args=(double[])p.Event.Args.Clone()});
    foreach(string diagnostic in p.Diagnostics)__instance.Diagnostics.Add(diagnostic.StartsWith("Line 1:",StringComparison.Ordinal)?"Line "+(i+1)+diagnostic.Substring(6):diagnostic);
    recovery+=p.Recovered;
   }
   if(!result.Any(e=>e.Kind==EventKind.Chart))__instance.Diagnostics.Add("No valid chart(bpm,beats) header.");recovered.SetValue(__instance,recovery);__result=result;return false;
  }
  static void Cloned(SpcDocument __instance,SpcDocument __result){TimingGroups.CloneModel(__instance,__result);var s=Of(__instance);if(s.Parsed.Count==0)__instance.ReadEvents();states.Add(__result,new State{Parsed=new Dictionary<int,Parsed>(s.Parsed)});}
  static bool Normalize(SpcDocument __0,SpcDocument __1){
   if(!Enabled)return true;
   if(DragPreview.Working==__1&&DragPreview.Written!=null&&!DragPreview.Written.Any(id=>{var n=EventAt(__1,id);return n!=null&&n.Kind==EventKind.SkyArea&&SkyGroups.GroupOf(n)>=0;}))return false;
   if(DragPreview.Working==__1&&DragPreview.Written!=null){DragNormalization.Normalize(__0,__1);return false;}
   var impacted=CommitValidation.ChangedSkyScopes(__0,__1);
   if(impacted.Count==0)return false;
   var previous=__0.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).ToDictionary(n=>n.SourceId);
   var notes=__1.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea&&impacted.Contains(TimingGroups.NameOf(n))&&SkyGroups.GroupOf(n)>=0&&ChartMath.InvalidReason(n)==null).OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
   var affected=new HashSet<int>();var tails=new Dictionary<long,List<SpcEvent>>();
   foreach(var n in notes){SpcEvent old;if(!previous.TryGetValue(n.SourceId,out old)||Enumerable.Range(0,10).Any(i=>old.Get(i)!=n.Get(i)))affected.Add(n.SourceId);long key=(long)Math.Floor(n.EndMs/SkyGroups.TimeTolerance);List<SpcEvent> bucket;if(!tails.TryGetValue(key,out bucket))tails[key]=bucket=new List<SpcEvent>();bucket.Add(n);}
   foreach(var n in notes){
    long key=(long)Math.Floor(n.TimeMs/SkyGroups.TimeTolerance);SpcEvent prior=null;bool touched=affected.Contains(n.SourceId);
    for(long k=key-1;k<=key+1;k++){List<SpcEvent> bucket;if(!tails.TryGetValue(k,out bucket))continue;foreach(var p in bucket){if(!SkyGroups.Joins(p,n))continue;if(affected.Contains(p.SourceId))touched=true;if(prior==null||p.TimeMs>prior.TimeMs||p.TimeMs==prior.TimeMs&&p.SourceId<prior.SourceId)prior=p;}}
    if(!touched||prior==null)continue;int g=SkyGroups.GroupOf(prior);if(SkyGroups.GroupOf(n)!=g){__1.SetOptionalNumber(n.SourceId,10,g);n.Args[10]=g;}affected.Add(n.SourceId);
   }return false;
  }
  internal static void Install(Harmony h){
   h.Patch(typeof(SpcDocument).GetMethod("SourceIndex"),new HarmonyMethod(typeof(DocumentAcceleration).GetMethod("SourceIndex",F)));
   h.Patch(typeof(SpcDocument).GetMethod("ReadEvents"),new HarmonyMethod(typeof(DocumentAcceleration).GetMethod("Read",F)));
   h.Patch(typeof(SpcDocument).GetMethod("Clone"),null,new HarmonyMethod(typeof(DocumentAcceleration).GetMethod("Cloned",F)));
   h.Patch(typeof(SkyGroups).GetMethod("NormalizeAuthoredChanges"),new HarmonyMethod(typeof(DocumentAcceleration).GetMethod("Normalize",F)));
  }
 }
}
