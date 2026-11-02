using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class DragPreview {
  internal static bool Enabled=true;
  static readonly BindingFlags F=TimingGroups.F;
  [ThreadStatic]static StudioBootstrap active;
  [ThreadStatic]static int previewRevision;
  [ThreadStatic]internal static HashSet<int> EditingIds;
  [ThreadStatic]internal static SpcDocument Working;
  [ThreadStatic]internal static HashSet<int> Written;
  [ThreadStatic]static int[] scopedIds;
  // Linked-node edits share the existing transactional scratch documents and
  // incremental render refresh without borrowing the native handle fields.
  internal static void PrepareLinked(StudioBootstrap boot,int[] ids){
   if(!Enabled||!boot.History.InTransaction)return;var original=(SpcDocument)start.GetValue(boot.History);var state=scratch.GetValue(boot.History,x=>new Scratch());
   if(state.Start!=original){state.Start=original;state.A=state.B=null;state.AD.Clear();state.BD.Clear();state.Dirty=null;}
   if(state.A==null)state.A=original.Clone();if(state.B==null)state.B=original.Clone();if(state.Dirty==null)state.Dirty=new DragDirtyState(original,(byte[])Get(boot,"savedBytes"));foreach(int id in ids){state.A.SourceIndex(id);state.B.SourceIndex(id);}
  }
  internal static void PreviewLinked(StudioBootstrap boot,int[] ids,Action<SpcDocument> edit){
   var previous=active;var previousIds=scopedIds;int previousRevision=previewRevision;
   active=Enabled?boot:null;scopedIds=ids;previewRevision=boot.History.Revision;
   try{boot.History.Preview(edit);Call(boot,"AfterDocumentEdit");}
   finally{active=previous;scopedIds=previousIds;previewRevision=previousRevision;Working=null;Written=null;EditingIds=null;}
  }
  static object Get(object o,string n){return GroupRuntime.Get(o,n);}static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
  static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
  static readonly FieldInfo start=typeof(EditHistory).GetField("transactionStart",F),document=typeof(EditHistory).GetField("<Document>k__BackingField",F),revision=typeof(EditHistory).GetField("<Revision>k__BackingField",F);
  static HashSet<int> latestIds;
  static StudioBootstrap changedBoot;static EditHistory changedHistory;static int changedRevision=-1;
  sealed class Scratch {internal SpcDocument Start,A,B;internal DragDirtyState Dirty;internal HashSet<int> AD=new HashSet<int>(),BD=new HashSet<int>();}
  static readonly ConditionalWeakTable<EditHistory,Scratch> scratch=new ConditionalWeakTable<EditHistory,Scratch>();
  static void BeginPreview(StudioBootstrap __instance,out StudioBootstrap __state){__state=active;active=Enabled?__instance:null;previewRevision=__instance.History.Revision;
   var ids=new HashSet<int>((int[])Get(__instance,"dragIds"));if(__instance.Events.Any(e=>ids.Contains(e.SourceId)&&!e.IsNote))active=null;
  }
  static Exception EndPreview(StudioBootstrap __state,Exception __exception){active=__state;Working=null;Written=null;EditingIds=null;return __exception;}
  static bool Preview(EditHistory __instance,Action<SpcDocument> __0){
   if(active==null||active.History!=__instance||!__instance.InTransaction)return true;
   // Native edit closure still runs from the original gesture. Never accumulate
   // rounded mouse deltas, or alter the source/undo start document.
   var ids=scopedIds??(int[])Get(active,"dragIds");var events=(List<SpcEvent>)Get(active,"events");
   var selection=new HashSet<int>(ids);if(scopedIds==null)selection.Add((int)Get(active,"dragPrimary"));
   if(events.Any(e=>selection.Contains(e.SourceId)&&!e.IsNote))return true;
   var original=(SpcDocument)start.GetValue(__instance);var state=scratch.GetValue(__instance,h=>new Scratch());
   if(state.Start!=original){state.Start=original;state.A=state.B=null;state.AD.Clear();state.BD.Clear();state.Dirty=null;}
   bool a=state.A!=__instance.Document;SpcDocument next=a?state.A:state.B;
   if(next==null){next=original.Clone();if(a)state.A=next;else state.B=next;}
   var restore=a?state.AD:state.BD;DocumentAcceleration.Restore(next,original,restore);
   Working=next;Written=new HashSet<int>();EditingIds=selection;
   try{
    try{__0(next);}finally{EditingIds=null;TimingGroups.Model(next).Events=null;}
    var prior=__instance.Document==state.A?state.AD:__instance.Document==state.B?state.BD:new HashSet<int>();
    if(Written.Concat(prior).Distinct().All(id=>next.SourceLine(id)==__instance.Document.SourceLine(id)))return false;
    if(__instance.BeforeCommit!=null)__instance.BeforeCommit(original,next);
    latestIds=new HashSet<int>(Written.Concat(prior));DocumentAcceleration.RestoreDiagnostics(next,original);
   }finally{if(a)state.AD=Written;else state.BD=Written;Working=null;Written=null;EditingIds=null;}
   document.SetValue(__instance,next);revision.SetValue(__instance,__instance.Revision+1);return false;
  }
  static bool AfterEdit(StudioBootstrap __instance){
   if(active!=__instance||!__instance.History.InTransaction){RenderIndex.Invalidate((List<SpcEvent>)Get(__instance,"events"));return true;}
   var h=__instance.History;if(h.Revision==previewRevision)return false;if(changedBoot==__instance&&changedHistory==h&&changedRevision==h.Revision)return false;
   var current=(List<SpcEvent>)Get(__instance,"events");var model=TimingGroups.Model(h.Document);
   if(latestIds==null)return true;RenderIndex.BeginPreview(current);var changed=new List<int>();var before=new Dictionary<int,SpcEvent>();var after=new Dictionary<int,SpcEvent>();bool any=false;
   for(int i=0;i<current.Count;i++)if(latestIds.Contains(current[i].SourceId)){
    var next=DocumentAcceleration.EventAt(h.Document,current[i].SourceId);if(next==null||!next.IsNote)return true;
    if(!next.Args.SequenceEqual(current[i].Args)){before[next.SourceId]=current[i];after[next.SourceId]=next;TimingGroups.TagEvent(h.Document,next);current[i]=next;changed.Add(i);any=true;}
   }
   model.Events=current;
   if(any){
    // Updating list elements must also retire feedback's captured note objects.
    // Rebuild lazily on the next feedback request, not at mouse polling frequency.
    GroupProperties.InvalidateFeedback(current);InFalsusStudio.NativeVisual.NativeHitEffects.Invalidate(Get(__instance,"hitFeedback"));
    RenderIndex.Changed(current,changed);var renderer=(NoteRenderer)Get(__instance,"notes");if(!InFalsusStudio.NativeVisual.NativeAir.UpdateConnections(renderer,before,after))renderer.UpdateConnections(current);
    double duration=Math.Max(10000,RenderIndex.PreviewEnd(current)+3000);
    var transport=(StudioTransport)Get(__instance,"transport");if(transport.Clip!=null)duration=Math.Max(duration,transport.Clip.length*1000.0-transport.AudioOffsetMs);Set(__instance,"durationMs",duration);
    Call(__instance,"RefreshSelectionFields");Call(__instance,"UpdateFollowedGroupAfterEdit");
    var state=scratch.GetValue(h,x=>new Scratch());if(state.Dirty==null)state.Dirty=new DragDirtyState((SpcDocument)start.GetValue(h),(byte[])Get(__instance,"savedBytes"));Set(__instance,"documentDirty",state.Dirty.Update(h.Document,latestIds));
   }
   changedBoot=__instance;changedHistory=h;changedRevision=h.Revision;
   // Commit/Cancel invoke the original full refresh outside this scope before
   // playback, Save, difficulty switch or Undo can observe deferred summaries.
   return false;
  }
  internal static void Mutated(SpcDocument d,int id){if(Working==d&&Written!=null)Written.Add(id);}
  static bool Validate(SpcDocument __0,SpcDocument __1){
   if(Working!=__1||Written==null)return true;
   // Only note-number setters can enter this scope; normalization writes are
   // tracked too, so propagated group IDs receive the same native validation.
   foreach(int id in Written){if(__0.SourceLine(id)==__1.SourceLine(id))continue;var e=DocumentAcceleration.EventAt(__1,id);if(e==null)continue;
    string reason=AuthoringSafety.Reason(e);if(reason!=null)throw new ArgumentException(reason);
    if(e.Kind==EventKind.Unknown&&(e.Name=="hold"||e.Name=="skyarea"||e.Name=="tap"||e.Name=="flick"||e.Name=="bpm"||e.Name=="track"||e.Name=="chart"))throw new ArgumentException("Invalid numeric statement. Previous value retained.");
   }return false;
  }
  static bool HiddenLegacyForm(StudioBootstrap __instance){
   var ui=InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.I;
   return ui==null||!ui.ready||ui.legacy||!__instance.History.InTransaction||(Get(__instance,"dragEvent")==null&&!ui.SkyNodeDragging(__instance));
  }
  static void Prepare(StudioBootstrap __instance){
   if(!Enabled||!__instance.History.InTransaction||Get(__instance,"dragEvent")==null)return;
   var ids=new HashSet<int>((int[])Get(__instance,"dragIds"));if(__instance.Events.Any(e=>ids.Contains(e.SourceId)&&!e.IsNote))return;
   var h=__instance.History;var original=(SpcDocument)start.GetValue(h);var state=scratch.GetValue(h,x=>new Scratch());
   if(state.Start!=original){state.Start=original;state.A=state.B=null;state.AD.Clear();state.BD.Clear();state.Dirty=null;}
   if(state.A==null)state.A=original.Clone();if(state.B==null)state.B=original.Clone();if(state.Dirty==null)state.Dirty=new DragDirtyState(original,(byte[])Get(__instance,"savedBytes"));
   if(__instance.Events.Any(n=>ids.Contains(n.SourceId)&&n.Kind==EventKind.SkyArea&&SkyGroups.GroupOf(n)>=0)){DragNormalization.Warm(original,state.A);DragNormalization.Warm(original,state.B);}
   int id=(int)Get(__instance,"dragPrimary");state.A.SourceIndex(id);state.B.SourceIndex(id);
  }
  internal static void Install(Harmony h){
   h.Patch(typeof(StudioBootstrap).GetMethod("BeginNoteDrag",F),postfix:new HarmonyMethod(typeof(DragPreview).GetMethod("Prepare",F)));
   foreach(string name in new[]{"CaptureBatchProperties","CaptureAuthoringToolsFrame"})h.Patch(typeof(StudioBootstrap).GetMethod(name,F),new HarmonyMethod(typeof(DragPreview).GetMethod("HiddenLegacyForm",F)));
   h.Patch(typeof(StudioBootstrap).GetMethod("PreviewNoteDrag",F),new HarmonyMethod(typeof(DragPreview).GetMethod("BeginPreview",F)),null,null,new HarmonyMethod(typeof(DragPreview).GetMethod("EndPreview",F)),null);
   h.Patch(typeof(AuthoringSafety).GetMethod("ValidateChanged"),new HarmonyMethod(typeof(DragPreview).GetMethod("Validate",F)));
   h.Patch(typeof(EditHistory).GetMethod("Preview"),new HarmonyMethod(typeof(DragPreview).GetMethod("Preview",F)));
   h.Patch(typeof(StudioBootstrap).GetMethod("AfterDocumentEdit",F),new HarmonyMethod(typeof(DragPreview).GetMethod("AfterEdit",F)));
  }
 }
}
