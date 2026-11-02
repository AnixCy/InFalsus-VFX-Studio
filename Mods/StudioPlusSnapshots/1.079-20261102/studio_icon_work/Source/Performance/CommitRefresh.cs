using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using InFalsusStudio.NativeVisual;
namespace InFalsusStudio.Performance {
 // A completed numeric note edit cannot alter timing, row structure or group
 // policy. Keep their existing timelines and update only changed event values.
 // Any structural/raw/timing edit falls through to the original full refresh.
 internal static class CommitRefresh {
  internal static bool Enabled=true;
  [ThreadStatic]static bool skipCursor;
  sealed class State {internal SpcDocument Document;internal GroupModel Model;internal Dictionary<int,SpcEvent> Notes;}
  static readonly ConditionalWeakTable<StudioBootstrap,State> states=new ConditionalWeakTable<StudioBootstrap,State>();
  static readonly BindingFlags F=TimingGroups.F;
  static object Get(object o,string name){return GroupRuntime.Get(o,name);}
  static void Set(object o,string name,object value){o.GetType().GetField(name,F).SetValue(o,value);}
  static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,F).Invoke(o,args);}
  static bool Cursor(){return !skipCursor;}
  static bool SoundChanged(SpcEvent a,SpcEvent b){
   if(TimingGroups.NoInput(b))return false;
   if(a.TimeMs!=b.TimeMs||a.DurationMs!=b.DurationMs||ChartMath.InvalidReason(a)!=ChartMath.InvalidReason(b))return true;
   if(b.Kind==EventKind.Tap||b.Kind==EventKind.Hold)return (a.Lane==0||a.Lane==5)!=(b.Lane==0||b.Lane==5);
   if(b.Kind==EventKind.Flick)return false;
   return Enumerable.Range(0,11).Any(i=>i!=7&&i!=8&&a.Get(i,-1)!=b.Get(i,-1));
  }
  static void Refreshed(StudioBootstrap __instance){
   if(__instance.History==null)return;var s=states.GetValue(__instance,b=>new State());
   s.Document=__instance.History.Document;s.Model=TimingGroups.Model(s.Document);s.Notes=__instance.Events.ToDictionary(e=>e.SourceId);
  }
  static bool AfterEdit(StudioBootstrap __instance){
   if(!Enabled||__instance.History==null||__instance.History.InTransaction)return true;
   State state;if(!states.TryGetValue(__instance,out state)||state.Document==null||ReferenceEquals(state.Document,__instance.History.Document))return true;
   var doc=__instance.History.Document;var next=TimingGroups.Model(doc);var prior=state.Model;
   if(doc.NextId!=state.Document.NextId||doc.LineCount!=state.Document.LineCount||prior.Errors.Count>0||next.Errors.Count>0||prior.Source.Count!=next.Source.Count)return true;
   var before=new Dictionary<int,SpcEvent>();var after=new Dictionary<int,SpcEvent>();
   foreach(var row in next.Source.Values){
    GroupSourceRow old;if(!prior.Source.TryGetValue(row.Id,out old)||old.Index!=row.Index)return true;
    if(old.Text==row.Text)continue;SpcEvent original;
    if(!state.Notes.TryGetValue(row.Id,out original)||!original.IsNote||prior.Of(row.Id)!=next.Of(row.Id))return true;
    var note=DocumentAcceleration.EventAt(doc,row.Id);if(note==null||note.Kind!=original.Kind)return true;
    before.Add(row.Id,original);after.Add(row.Id,note);
   }
   if(after.Count==0)return true;
   var current=(List<SpcEvent>)Get(__instance,"events");var changed=new List<int>();
   if(current.Count!=state.Notes.Count)return true;
   for(int i=0;i<current.Count;i++){SpcEvent note;if(after.TryGetValue(current[i].SourceId,out note)){current[i]=note;changed.Add(i);}}
   if(changed.Count!=after.Count)return true;
   // Retag unchanged render-owned events too, so they do not retain old group
   // models. No mutable SpcDocument.ReadEvents result is shared with the cache.
   var tagged=current;TimingGroups.Read(doc,ref tagged);next.Events=current;
   RenderIndex.Invalidate(current);GroupProperties.InvalidateFeedback(current);
   double duration=Math.Max(10000,current.Where(e=>e.IsNote&&AuthoringSafety.Reason(e)==null).Select(e=>e.EndMs).DefaultIfEmpty(0).Max()+3000);
   var transport=(StudioTransport)Get(__instance,"transport");if(transport!=null&&transport.Clip!=null)duration=Math.Max(duration,transport.Clip.length*1000.0-transport.AudioOffsetMs);Set(__instance,"durationMs",duration);
   bool previous=skipCursor;skipCursor=!after.Values.Any(e=>(e.Kind==EventKind.SkyArea||e.Kind==EventKind.Flick)&&!TimingGroups.NoInput(e));
   try{Call(__instance,"RefreshSongDerivedState");}finally{skipCursor=previous;}
   if(after.Values.Any(e=>SoundChanged(before[e.SourceId],e)))Call(__instance,"RebuildKeySoundPlan");
   Call(__instance,"ApplyViewNormalization");
   var renderer=(NoteRenderer)Get(__instance,"notes");if(renderer!=null&&!NativeAir.UpdateConnections(renderer,before,after))renderer.UpdateConnections(current);
   var feedback=(HitFeedbackRenderer)Get(__instance,"hitFeedback");if(feedback!=null){NativeHitEffects.Invalidate(feedback);if(after.Values.Any(e=>e.Kind==EventKind.SkyArea&&!TimingGroups.NoInput(e)))feedback.Rebuild(current);}
   Call(__instance,"RefreshSelectionFields");Call(__instance,"UpdateFollowedGroupAfterEdit");
   byte[] saved=(byte[])Get(__instance,"savedBytes");Set(__instance,"documentDirty",saved==null||!doc.ToBytes().SequenceEqual(saved));
   Call(__instance,"SyncActiveSongDraft");Call(__instance,"RefreshProjectDirty");Refreshed(__instance);return false;
  }
  internal static void Install(Harmony h){
   h.Patch(typeof(StudioBootstrap).GetMethod("RefreshDocument",F),null,new HarmonyMethod(typeof(CommitRefresh).GetMethod("Refreshed",F)){priority=Priority.Last});
   h.Patch(typeof(StudioBootstrap).GetMethod("AfterDocumentEdit",F),new HarmonyMethod(typeof(CommitRefresh).GetMethod("AfterEdit",F)){priority=Priority.First-1});
   h.Patch(typeof(AutoplayCursor).GetMethod("Rebuild"),new HarmonyMethod(typeof(CommitRefresh).GetMethod("Cursor",F)){priority=Priority.First+1});
  }
 }
}
