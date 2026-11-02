using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using InFalsusStudio.Core;
namespace InFalsusStudio.TimeGroups {
 internal sealed class VisibilityEvent {
  internal int Id;internal string Name;internal double Time;internal bool Hidden;
  static readonly Regex Syntax=new Regex(@"^\s*hidegroup\s*\(\s*([^,()]*)\s*,\s*([^,()]*)\s*\)\s*;?\s*(?:(?://|#).*)?$",RegexOptions.CultureInvariant);
  internal static bool TryParse(int id,string name,string raw,out VisibilityEvent result){
   result=null;var m=Syntax.Match(raw);double time,value;
   if(!m.Success||!double.TryParse(m.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out time)||!double.TryParse(m.Groups[2].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||!AuthoringSafety.Finite(time)||time<0||time>AuthoringSafety.MaxTimelineMs||(value!=0&&value!=1))return false;
   result=new VisibilityEvent{Id=id,Name=name,Time=time,Hidden=value==1};return true;
  }
 }
 internal static class GroupProperties {
  static HarmonyMethod H(string name){return new HarmonyMethod(typeof(GroupProperties).GetMethod(name,TimingGroups.F)){priority=Priority.First};}
  internal static void Install(Harmony harmony){
   harmony.Patch(typeof(NoteRenderer).GetMethod("Visible",TimingGroups.F),H("Visible"));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("AddHandle",TimingGroups.F),H("Handle"));
   harmony.Patch(typeof(AutoplayCursor).GetMethod("Rebuild"),H("JudgmentSource"));
   harmony.Patch(typeof(HitSoundPlan).GetConstructors().Single(),H("JudgmentSource"));
   harmony.Patch(typeof(HitFeedbackRenderer).GetMethod("Rebuild"),H("FeedbackSource"));
   harmony.Patch(typeof(HitFeedbackRenderer).GetMethod("Draw"),H("FeedbackSource"));
   harmony.Patch(typeof(NoteSkinPolicy).GetMethod("Active"),H("Active"));
  }
  internal static bool Hidden(GroupModel model,string name,double now){
   VisibilityEvent[] events;if(!model.VisibilityByGroup.TryGetValue(name,out events))return false;
   int lo=0,hi=events.Length;while(lo<hi){int m=(lo+hi)/2;if(events[m].Time<=now)lo=m+1;else hi=m;}return lo>0&&events[lo-1].Hidden;
  }
  internal static void SetProperty(SpcDocument d,string name,string property,bool enabled){
   TimingGroups.Validate(d);if(property!="noinput"&&property!="noclip")throw new ArgumentException("未知的组属性。");
   // One authored transaction: enabling either mode removes the other flag.
   // Existing documents are never normalized merely by opening them.
   if(enabled)WriteProperty(d,name,property=="noclip"?"noinput":"noclip",false);
   WriteProperty(d,name,property,enabled);
  }
  static void WriteProperty(SpcDocument d,string name,string property,bool enabled){
   TimingGroups.Validate(d);if(property!="noinput"&&property!="noclip")throw new ArgumentException("未知的组属性。");
   var block=TimingGroups.Model(d).Find(name);if(block==null)throw new ArgumentException("默认group 无组头属性。");
   string raw=d.SourceLine(block.Start);var m=TimingGroups.Start.Match(raw);
   // Only edit the requested property. Keep the name, every other property,
   // unknown tokens, header suffix, source identity and line ending intact.
   string tail=m.Groups[2].Success?m.Groups[2].Value:"";
   var tokens=Regex.Matches(tail,@"[^,\s]+").Cast<Match>().Where(x=>x.Value==property).OrderByDescending(x=>x.Index).ToArray();
   if(enabled){if(tokens.Length>0)return;int at=m.Groups[1].Index+m.Groups[1].Length+(m.Groups[2].Success?1+m.Groups[2].Length:0);raw=raw.Insert(at,","+property);}
   else {
    if(tokens.Length==0)return;
    foreach(var token in tokens){int at=m.Groups[2].Index+token.Index,len=token.Length;int start=at;while(start>m.Groups[2].Index&&char.IsWhiteSpace(raw[start-1]))start--;if(start>m.Groups[2].Index&&raw[start-1]==',')start--;else {int end=at+len;while(end<raw.Length&&char.IsWhiteSpace(raw[end]))end++;if(end<raw.Length&&raw[end]==',')len=end-at+1;}
     raw=raw.Remove(start,at+len-start);
    }
    var next=TimingGroups.Start.Match(raw);if(next.Success&&next.Groups[2].Success&&string.IsNullOrWhiteSpace(next.Groups[2].Value))raw=raw.Remove(next.Groups[2].Index-1,next.Groups[2].Length+1);
   }
   d.ReplaceSourceLine(block.Start,raw);TimingGroups.Validate(d);
  }
  internal static IEnumerable<SpcEvent> Judged(IEnumerable<SpcEvent> source){return source.Where(e=>!TimingGroups.NoInput(e));}
  static void JudgmentSource(ref IEnumerable<SpcEvent> __0){__0=Judged(__0).ToArray();}
  sealed class FeedbackCache {internal IList<SpcEvent> Filtered;internal SpcEvent First;internal int Count=-1;}
  static readonly ConditionalWeakTable<IList<SpcEvent>,FeedbackCache> feedback=new ConditionalWeakTable<IList<SpcEvent>,FeedbackCache>();
  internal static void InvalidateFeedback(IList<SpcEvent> source){feedback.Remove(source);}
  internal static IList<SpcEvent> Feedback(IList<SpcEvent> source){var cache=feedback.GetValue(source,s=>new FeedbackCache());var first=source.Count==0?null:source[0];if(cache.Count!=source.Count||!ReferenceEquals(cache.First,first)){cache.Count=source.Count;cache.First=first;cache.Filtered=source.Any(TimingGroups.NoInput)?Judged(source).ToArray():source;}return cache.Filtered;}
  static void FeedbackSource(ref IList<SpcEvent> __0){__0=Feedback(__0);}
  static bool Visible(NoteRenderer __instance,SpcEvent __0,ref bool __result){var tag=TimingGroups.Tag(__0);if(tag==null||tag.Model.Visibility.Count==0)return true;if(!TimingGroups.Hidden(__0,(double)GroupRuntime.Get(__instance,"now")))return true;__result=false;return false;}
  static bool Handle(StudioBootstrap __instance,SpcEvent __0){return !TimingGroups.Hidden(__0,__instance.CurrentMs);}
  static bool Active(SpcEvent __0,ref bool __result){if(!TimingGroups.NoInput(__0))return true;__result=false;return false;}
  internal sealed class RenderState {internal ScrollTimeline Scroll;internal bool HidePast;internal AutoplayCursor Cursor;}
 }
}
