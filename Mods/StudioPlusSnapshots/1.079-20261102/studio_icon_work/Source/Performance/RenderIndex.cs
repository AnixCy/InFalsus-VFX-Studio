using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 internal sealed class RenderMeta {
  internal SpcEvent Note;internal string Safety,Invalid,Bounds;internal int Order;
  internal RenderMeta(RenderMeta source,int order){Note=source.Note;Safety=source.Safety;Invalid=source.Invalid;Bounds=source.Bounds;Order=order;}
  internal RenderMeta(SpcEvent e){Note=e;Safety=AuthoringSafety.Reason(e);Invalid=ChartMath.InvalidReason(e);Bounds=Invalid==null?InFalsusStudio.NativeEditorUI.AirOverflow.RestrictedViolation(e):null;}
 }
 internal sealed class NoteIntervalIndex {
  internal readonly RenderMeta[] Notes;readonly double[] maxEnd;readonly int size;
  internal NoteIntervalIndex(IEnumerable<RenderMeta> notes,Func<RenderMeta,double> end=null){Notes=notes.OrderBy(n=>n.Note.TimeMs).ThenBy(n=>n.Order).ToArray();size=1;while(size<Notes.Length)size*=2;maxEnd=Enumerable.Repeat(double.NegativeInfinity,size*2).ToArray();for(int i=0;i<Notes.Length;i++)maxEnd[size+i]=end==null?Notes[i].Note.EndMs:end(Notes[i]);for(int i=size-1;i>0;i--)maxEnd[i]=Math.Max(maxEnd[i*2],maxEnd[i*2+1]);}
  internal void Query(double begin,double end,HashSet<int> result){if(Notes.Length>0&&end>=begin)Visit(1,0,size,begin,end,result);}
  void Visit(int node,int left,int right,double begin,double end,HashSet<int> result){if(left>=Notes.Length||maxEnd[node]<begin||Notes[left].Note.TimeMs>end)return;if(right-left==1){result.Add(Notes[left].Order);return;}int mid=(left+right)/2;Visit(node*2,left,mid,begin,end,result);Visit(node*2+1,mid,right,begin,end,result);}
 }
 internal sealed class RenderSource {
  internal readonly RenderMeta[] Notes;internal readonly RenderMeta[] Flicks;internal readonly Dictionary<int,RenderMeta> ByOrder=new Dictionary<int,RenderMeta>();internal readonly Dictionary<int,RenderMeta> ById=new Dictionary<int,RenderMeta>();
  internal readonly Dictionary<string,NoteIntervalIndex> Groups=new Dictionary<string,NoteIntervalIndex>();internal readonly SpcEvent[] Grid;
  internal readonly int Invalid,Bounds;internal readonly double End;internal int Count;internal SpcEvent First;
  internal RenderSource(IList<SpcEvent> source){
   Count=source.Count;First=Count>0?source[0]:null;var notes=new List<RenderMeta>();SpcEvent max=null;
   for(int i=0;i<source.Count;i++){var e=source[i];if(!e.IsNote)continue;var n=new RenderMeta(RenderIndex.Meta(e),i);notes.Add(n);ByOrder[i]=n;ById[e.SourceId]=n;if(n.Safety==null&&(max==null||e.EndMs>max.EndMs))max=e;if(n.Invalid!=null)Invalid++;else if(n.Bounds!=null)Bounds++;}
   Notes=notes.ToArray();Flicks=Notes.Where(n=>n.Invalid==null&&n.Note.Kind==EventKind.Flick).ToArray();Grid=max==null?new SpcEvent[0]:new[]{max};End=max==null?0:max.EndMs;
   foreach(var group in Notes.Where(n=>n.Invalid==null).GroupBy(n=>TimingGroups.NameOf(n.Note)))Groups[group.Key]=new NoteIntervalIndex(group);
  }
  internal void Query(NoteIntervalIndex notes,ScrollTimeline scroll,double now,double units,double near,double far,HashSet<int> result){
   var timeline=FastScroll.For(scroll);double end=Math.Max(End,now);
   for(int i=0;i<timeline.Time.Length;i++){
    double a=Math.Max(0,timeline.Time[i]),b=i+1<timeline.Time.Length?Math.Min(end,timeline.Time[i+1]):end;if(b<a)continue;
    double z=timeline.Delta(a,now)*units,slope=timeline.Speed[i]*units;if(!AuthoringSafety.Finite(z)||!AuthoringSafety.Finite(slope)){foreach(var n in notes.Notes)result.Add(n.Order);return;}
    double padding=Math.Max(1e-6,Math.Abs(z)*2e-14),lo=near-padding,hi=far+padding;
    if(Math.Abs(slope)<1e-15){if(z>=lo&&z<=hi)notes.Query(a,b,result);continue;}
    double x=a+(lo-z)/slope,y=a+(hi-z)/slope;double first=Math.Max(a,Math.Min(x,y)),last=Math.Min(b,Math.Max(x,y));if(last>=first)notes.Query(first,last,result);
   }
  }
 }
 internal sealed class RenderFrame {
  internal RenderSource Source,ContactSource;internal AutoplayCursor ContactCursor;internal NoteIntervalIndex Contacts;internal int ContactRevision;internal readonly HashSet<int> Seen=new HashSet<int>();internal readonly List<SpcEvent> Candidates=new List<SpcEvent>();internal readonly Dictionary<SpcEvent,double> Depths=new Dictionary<SpcEvent,double>();
 }
 internal static class RenderIndex {
  sealed class CursorRevision {internal int Value;}
  static readonly ConditionalWeakTable<AutoplayCursor,CursorRevision> cursors=new ConditionalWeakTable<AutoplayCursor,CursorRevision>();
  static void CursorRebuilt(AutoplayCursor __instance){cursors.GetValue(__instance,c=>new CursorRevision()).Value++;}
  static readonly ConditionalWeakTable<SpcEvent,RenderMeta> metadata=new ConditionalWeakTable<SpcEvent,RenderMeta>();
  static readonly ConditionalWeakTable<IList<SpcEvent>,RenderSource> sources=new ConditionalWeakTable<IList<SpcEvent>,RenderSource>();
  static readonly ConditionalWeakTable<NoteRenderer,RenderFrame> frames=new ConditionalWeakTable<NoteRenderer,RenderFrame>();
  internal static RenderMeta Meta(SpcEvent e){return metadata.GetValue(e,x=>new RenderMeta(x));}
  internal static RenderSource Source(IList<SpcEvent> events){PreviewIndex preview;if(previews.TryGetValue(events,out preview))return preview.Source;RenderSource s;if(sources.TryGetValue(events,out s)&&(s.Count!=events.Count||!ReferenceEquals(s.First,events.Count>0?events[0]:null))){sources.Remove(events);s=null;}if(s==null)s=sources.GetValue(events,x=>new RenderSource(x));return s;}
  sealed class PreviewIndex {internal RenderSource Source;internal readonly HashSet<int> Orders=new HashSet<int>();internal SpcEvent[] Grid;}
  static readonly ConditionalWeakTable<IList<SpcEvent>,PreviewIndex> previews=new ConditionalWeakTable<IList<SpcEvent>,PreviewIndex>();
  internal static void BeginPreview(IList<SpcEvent> events){previews.GetValue(events,e=>new PreviewIndex{Source=Source(e),Grid=Source(e).Grid});}
  internal static void Changed(IList<SpcEvent> events,IEnumerable<int> orders){var p=previews.GetValue(events,e=>new PreviewIndex{Source=Source(e)});var edited=orders.ToArray();p.Orders.UnionWith(edited);SpcEvent max=p.Grid!=null&&p.Grid.Length>0?p.Grid[0]:null;
   if(max==null||edited.Any(i=>events[i].SourceId==max.SourceId)){max=null;foreach(var n in events)if(n.IsNote&&Meta(n).Safety==null&&(max==null||n.EndMs>max.EndMs))max=n;}
   else foreach(int i in edited){var n=events[i];if(n.IsNote&&Meta(n).Safety==null&&n.EndMs>max.EndMs)max=n;}
   p.Grid=max==null?new SpcEvent[0]:new[]{max};
  }
  internal static double PreviewEnd(IList<SpcEvent> events){PreviewIndex p;return previews.TryGetValue(events,out p)&&p.Grid.Length>0?p.Grid[0].EndMs:0;}
  internal static void Invalidate(IList<SpcEvent> events){previews.Remove(events);sources.Remove(events);}
  static RenderFrame Frame(NoteRenderer renderer){return frames.GetValue(renderer,r=>new RenderFrame());}
  internal static IList<SpcEvent> GridSource(NoteRenderer renderer,IList<SpcEvent> events){if(!P1Switches.Rendering)return events;var frame=Frame(renderer);frame.Source=Source(events);PreviewIndex preview;return previews.TryGetValue(events,out preview)?preview.Grid:frame.Source.Grid;}
  internal static IList<SpcEvent> Candidates(NoteRenderer renderer,IList<SpcEvent> events){
   var f=Frame(renderer);f.Depths.Clear();if(!P1Switches.Rendering||(bool)GroupRuntime.Get(renderer,"ghost"))return events;
   var source=Source(events);f.Source=source;if(source.Notes.Length<128)return events;
   var p=(StudioProfile)GroupRuntime.Get(renderer,"p");double units=p.UnitsPerMs,now=(double)GroupRuntime.Get(renderer,"now");if(units<=0||!AuthoringSafety.Finite(units)||!AuthoringSafety.Finite(now))return events;
   f.Seen.Clear();f.Candidates.Clear();bool linear=!ReferenceEquals(TimingGroups.Boot,null)&&(bool)GroupRuntime.Get(TimingGroups.Boot,"linearTimeLayout");
   var fallback=(ScrollTimeline)GroupRuntime.Get(renderer,"scroll");
   foreach(var group in source.Groups){var sample=group.Value.Notes[0].Note;var tag=TimingGroups.Tag(sample);var scroll=!linear&&tag!=null&&tag.Model.Extended&&tag.Model.Errors.Count==0?tag.Model.Scroll(tag.Name):fallback;source.Query(group.Value,scroll,now,units,p.stageNear-p.tapDepth,p.stageFar,f.Seen);}
   // A Flick may be held at Z=0 until its visual swipe contact; its original
   // time can be outside every affine visible window after a scroll impulse.
   var cursor=(AutoplayCursor)GroupRuntime.Get(renderer,"visualCursor");if((bool)GroupRuntime.Get(renderer,"hidePast")&&cursor!=null){int revision=cursors.GetValue(cursor,c=>new CursorRevision()).Value;if(f.Contacts==null||f.ContactSource!=source||f.ContactCursor!=cursor||f.ContactRevision!=revision){f.Contacts=new NoteIntervalIndex(source.Flicks,n=>cursor.VisualContactTime(n.Note));f.ContactSource=source;f.ContactCursor=cursor;f.ContactRevision=revision;}f.Contacts.Query(now,now,f.Seen);}
   PreviewIndex preview;if(previews.TryGetValue(events,out preview))f.Seen.UnionWith(preview.Orders);
   foreach(int order in f.Seen.OrderBy(i=>i))f.Candidates.Add(events[order]);return f.Candidates;
  }
  internal static bool Compare(NoteRenderer renderer,SpcEvent a,SpcEvent b,out int result){
   result=0;RenderFrame f;if(!P1Switches.Rendering||!frames.TryGetValue(renderer,out f))return false;double now=(double)GroupRuntime.Get(renderer,"now"),x,y;
   if(!f.Depths.TryGetValue(a,out x)){x=FastScrollHooks.Compute(GroupRuntime.For(renderer,a),(a.TimeMs+a.EndMs)*.5,now);f.Depths[a]=x;}
   if(!f.Depths.TryGetValue(b,out y)){y=FastScrollHooks.Compute(GroupRuntime.For(renderer,b),(b.TimeMs+b.EndMs)*.5,now);f.Depths[b]=y;}result=y.CompareTo(x);return true;
  }
  internal static string Safety(SpcEvent e){return P1Switches.Rendering?Meta(e).Safety:AuthoringSafety.Reason(e);}
  static string Invalid(SpcEvent e){return P1Switches.Rendering?Meta(e).Invalid:ChartMath.InvalidReason(e);}
  internal static string Bounds(SpcEvent e){return InFalsusStudio.NativeEditorUI.AirOverflow.Enabled?null:P1Switches.Rendering?Meta(e).Bounds:AirReachBounds.Violation(e);}
  static void Diagnostics(NoteRenderer __instance,IList<SpcEvent> __0,bool __4,ISet<int> __15){
   if(!P1Switches.Rendering)return;RenderFrame f;if(!frames.TryGetValue(__instance,out f)||f.Source==null)return;var s=f.Source;int invalid=s.Invalid,bounds=s.Bounds;
   if(__15!=null)foreach(int id in __15){RenderMeta m;if(s.ById.TryGetValue(id,out m)){if(m.Invalid!=null)invalid--;else if(m.Bounds!=null)bounds--;}}
   typeof(NoteRenderer).GetField("<InvalidNotes>k__BackingField",TimingGroups.F).SetValue(__instance,__4?invalid:0);typeof(NoteRenderer).GetField("<OutOfBoundsNotes>k__BackingField",TimingGroups.F).SetValue(__instance,__4&&!InFalsusStudio.NativeEditorUI.AirOverflow.Enabled?bounds:0);
  }
  static IEnumerable<CodeInstruction> Draw(IEnumerable<CodeInstruction> input){
   var code=input.ToList();int loops=0;for(int i=0;i<code.Count;i++){
    var c=code[i];var next=i+1<code.Count?code[i+1].operand as MethodInfo:null;
    if(c.opcode==OpCodes.Ldarg_1&&next!=null&&next.Name=="GetEnumerator"){
     var first=new CodeInstruction(OpCodes.Ldarg_0);first.labels.AddRange(c.labels);first.blocks.AddRange(c.blocks);yield return first;yield return new CodeInstruction(OpCodes.Ldarg_1);yield return new CodeInstruction(OpCodes.Call,typeof(RenderIndex).GetMethod(loops++==0?"GridSource":"Candidates",TimingGroups.F));continue;
    }
    var method=c.operand as MethodInfo;if(method!=null&&method.DeclaringType==typeof(AuthoringSafety)&&method.Name=="Reason")c.operand=typeof(RenderIndex).GetMethod("Safety",TimingGroups.F);
    if(method!=null&&method.DeclaringType==typeof(ChartMath)&&method.Name=="InvalidReason")c.operand=typeof(RenderIndex).GetMethod("Invalid",TimingGroups.F);
    if(method!=null&&method.DeclaringType==typeof(AirReachBounds)&&method.Name=="Violation")c.operand=typeof(RenderIndex).GetMethod("Bounds",TimingGroups.F);yield return c;
   }
   if(loops!=2)throw new InvalidOperationException("NoteRenderer traversal layout changed; render index not applied.");
  }
  static IEnumerable<CodeInstruction> GuiDiagnostics(IEnumerable<CodeInstruction> input){
   foreach(var c in input){var m=c.operand as MethodInfo;
    if(m!=null&&m.DeclaringType==typeof(AuthoringSafety)&&m.Name=="Reason")c.operand=typeof(RenderIndex).GetMethod("Safety",TimingGroups.F);
    if(m!=null&&m.DeclaringType==typeof(AirReachBounds)&&m.Name=="Violation")c.operand=typeof(RenderIndex).GetMethod("Bounds",TimingGroups.F);yield return c;
   }
  }
  internal static void Install(Harmony h){
   foreach(var type in new[]{typeof(StudioBootstrap)}.Concat(typeof(StudioBootstrap).GetNestedTypes(TimingGroups.F)))foreach(var m in type.GetMethods(TimingGroups.F))if(m.Name=="BeginGuiFrame"||m.Name.Contains("<BeginGuiFrame>"))h.Patch(m,transpiler:new HarmonyMethod(typeof(RenderIndex).GetMethod("GuiDiagnostics",TimingGroups.F)));
   h.Patch(typeof(AutoplayCursor).GetMethod("Rebuild"),postfix:new HarmonyMethod(typeof(RenderIndex).GetMethod("CursorRebuilt",TimingGroups.F)));h.Patch(typeof(NoteRenderer).GetMethod("Draw"),postfix:new HarmonyMethod(typeof(RenderIndex).GetMethod("Diagnostics",TimingGroups.F)),transpiler:new HarmonyMethod(typeof(RenderIndex).GetMethod("Draw",TimingGroups.F)));}
 }
}
