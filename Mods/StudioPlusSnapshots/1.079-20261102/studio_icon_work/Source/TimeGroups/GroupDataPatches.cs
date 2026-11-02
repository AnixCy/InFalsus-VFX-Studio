using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.Performance;
namespace InFalsusStudio.TimeGroups {
 internal static class GroupDataPatches {
  static HarmonyMethod H(string name){return new HarmonyMethod(typeof(GroupDataPatches).GetMethod(name,TimingGroups.F));}
  internal static void Install(Harmony harmony){
   harmony.Patch(typeof(SpcDocument).GetMethod("ReadEvents"),null,H("Read"));
   foreach(string name in new[]{"ReplaceSourceLine","Delete"})harmony.Patch(typeof(SpcDocument).GetMethod(name),H("Dirty"));
   foreach(string name in new[]{"SetToken","SetOptionalNumber"})harmony.Patch(typeof(SpcDocument).GetMethod(name),null,H("NumericChanged"));
   harmony.Patch(typeof(SpcDocument).GetMethod("Append"),null,H("Append"));
   harmony.Patch(typeof(SpcDocument).GetMethod("SortTimedStatements"),H("Sort"),H("Sorted"));
   foreach(var type in new[]{typeof(ScrollTimeline),typeof(BeatTimeline)})harmony.Patch(type.GetConstructor(new[]{typeof(IEnumerable<SpcEvent>)}),H("BaseTimeline"));
   foreach(string method in new[]{"Execute","Preview"})harmony.Patch(typeof(EditHistory).GetMethod(method),H("EditBegin"),null,null,H("EditEnd"),null);
   harmony.Patch(typeof(AuthoringSafety).GetMethod("BeforeCommit"),H("ValidateEdit"));
   harmony.Patch(typeof(SkyGroups).GetMethod("Heads"),H("Heads"));
   harmony.Patch(typeof(SkyGroups).GetMethod("Joins"),H("Joins"));
   harmony.Patch(typeof(TickCountResearch).GetMethod("Analyze"),H("Analyze"));
   harmony.Patch(typeof(ProvisionalComboTimeline).GetConstructor(new[]{typeof(IEnumerable<SpcEvent>)}),H("Combo"));
   harmony.Patch(typeof(ChartHealthCheck).GetMethod("Run"),null,H("Health"));
   harmony.Patch(typeof(TimingReferenceMerge).GetMethod("Prepare"),H("PrepareTiming"));
  }
  static void Dirty(SpcDocument __instance){TimingGroups.Invalidate(__instance);}
  static void Read(SpcDocument __instance,ref List<SpcEvent> __result){if(!InFalsusStudio.Performance.DocumentAcceleration.ParsingRaw)TimingGroups.Read(__instance,ref __result);}
  static void NumericChanged(SpcDocument __instance,int __0){TimingGroups.NumericChanged(__instance,__0);InFalsusStudio.Performance.DragPreview.Mutated(__instance,__0);}
  static void Append(SpcDocument __instance,string __0,int __result){TimingGroups.Appended(__instance,__result,__0);}
  static void Sorted(SpcDocument __instance,bool __result){if(__result)TimingGroups.Invalidate(__instance);}
  static bool Sort(SpcDocument __instance,ref bool __result){var m=TimingGroups.Model(__instance);if(!m.Extended&&m.Errors.Count==0)return true;__result=TimingGroups.Sort(__instance);return false;}
  static void BaseTimeline(ref IEnumerable<SpcEvent> __0){__0=TimingGroups.BaseTiming(__0);}
  static void EditBegin(EditHistory __instance,out string __state){
   __state=TimingGroups.AppendGroup;TimingGroups.AppendGroup=!ReferenceEquals(TimingGroups.Boot,null)&&ReferenceEquals(TimingGroups.Boot.History,__instance)?TimingGroups.Current:null;
  }
  static Exception EditEnd(string __state,Exception __exception){TimingGroups.AppendGroup=__state;return __exception;}
  static void ValidateEdit(SpcDocument __1){TimingGroups.Validate(__1);}
  [ThreadStatic]static bool headBypass;
  sealed class HeadMemo {internal SpcEvent[] Notes;internal HashSet<int> Heads;}
  static readonly Dictionary<string,HeadMemo> headMemo=new Dictionary<string,HeadMemo>();
  static bool Heads(IEnumerable<SpcEvent> __0,ref HashSet<int> __result){
   if(headBypass)return true;var events=__0.ToArray();if(!events.Any(e=>TimingGroups.NameOf(e)!=""))return true;
   __result=new HashSet<int>();headBypass=true;try{foreach(var group in events.GroupBy(TimingGroups.NameOf)){
    var sky=group.Where(n=>n.Kind==EventKind.SkyArea).ToArray();HeadMemo memo;
    if(!headMemo.TryGetValue(group.Key,out memo)||!EventValues.Same(sky,memo.Notes)){memo=new HeadMemo{Notes=sky,Heads=SkyGroups.Heads(sky)};headMemo[group.Key]=memo;}
    __result.UnionWith(memo.Heads);
   }if(headMemo.Count>128)headMemo.Clear();}finally{headBypass=false;}return false;
  }
  static bool Joins(SpcEvent __0,SpcEvent __1,ref bool __result){if(TimingGroups.NameOf(__0)==TimingGroups.NameOf(__1))return true;__result=false;return false;}
  static GroupModel Extended(SpcEvent[] events){return events.Select(TimingGroups.Tag).Where(t=>t!=null&&t.Model.Extended).Select(t=>t.Model).FirstOrDefault();}
  sealed class PreparedParts {internal SpcEvent[] Events;internal KeyValuePair<string,SpcEvent[]>[] Parts;}
  static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GroupModel,PreparedParts> preparedParts=new System.Runtime.CompilerServices.ConditionalWeakTable<GroupModel,PreparedParts>();
  static IEnumerable<KeyValuePair<string,SpcEvent[]>> Parts(SpcEvent[] events,GroupModel model){
   var cache=preparedParts.GetValue(model,m=>new PreparedParts());
   if(cache.Events!=null&&events.SequenceEqual(cache.Events))return cache.Parts;
   var groups=new Dictionary<string,List<SpcEvent>>(StringComparer.Ordinal);var names=new List<string>();
   foreach(var e in events){if(e.Kind==EventKind.Chart)continue;string name=TimingGroups.NameOf(e);List<SpcEvent> rows;
    if(!groups.TryGetValue(name,out rows)){groups[name]=rows=new List<SpcEvent>();names.Add(name);}rows.Add(e);
   }
   if(!groups.ContainsKey("")){groups[""]=new List<SpcEvent>();names.Add("");}
   var parts=new List<KeyValuePair<string,SpcEvent[]>>();
   foreach(string name in names){var block=model.Find(name);bool noinput=model.Errors.Count==0&&block!=null&&block.NoInput;
    parts.Add(new KeyValuePair<string,SpcEvent[]>(name,model.Local(name,false).Concat(groups[name].Where(e=>e.Kind!=EventKind.Bpm&&e.Kind!=EventKind.Track&&!model.Visibility.ContainsKey(e.SourceId)&&!(e.IsNote&&noinput))).ToArray()));
   }
   cache.Events=events;cache.Parts=parts.ToArray();return cache.Parts;
  }
  static bool Analyze(IEnumerable<SpcEvent> __0,ref ComboCountSummary __result){
   if(TimingGroups.CountBypass)return true;var events=__0.ToArray();var model=Extended(events);if(model==null)return true;
   var sum=new ComboCountSummary();TimingGroups.CountBypass=true;
   try{foreach(var part in Parts(events,model)){var s=CountPartCache.Get(part.Key,part.Value).Summary;checked{sum.Tap+=s.Tap;sum.Hold+=s.Hold;sum.SkyArea+=s.SkyArea;sum.Flick+=s.Flick;}sum.Supported&=s.Supported;sum.BpmChanges|=s.BpmChanges;if(!s.Supported)sum.Warning=s.Warning;}}
   finally{TimingGroups.CountBypass=false;}
   sum.Verified=false;sum.Evidence="";if(sum.Supported)sum.Warning="非官方时间组：按各组起点 BPM 估算物量；尚未在目标壳子验证判定规则。";__result=sum;return false;
  }
  static void Set(object obj,string name,object value){obj.GetType().GetField(name,TimingGroups.F).SetValue(obj,value);}
  static bool Combo(ProvisionalComboTimeline __instance,IEnumerable<SpcEvent> __0){
   if(TimingGroups.CountBypass)return true;var events=__0.ToArray();var model=Extended(events);if(model==null)return true;
   var notes=new List<ProvisionalComboNote>();var total=new ComboAtTime();bool supported=true;string warning="非官方时间组：各组独立 BPM 的候选计数，尚非本体判定认证。";TimingGroups.CountBypass=true;
   try{foreach(var part in Parts(events,model)){var t=CountPartCache.Get(part.Key,part.Value).Timeline;supported&=t.Supported;if(!t.Supported)warning=t.Warning;notes.AddRange(t.Notes.Select(CountPartCache.Copy));}}
   finally{TimingGroups.CountBypass=false;}
   notes=notes.OrderBy(n=>n.Start).ThenBy(n=>n.Id).ToList();foreach(var n in notes)total.Add(n.Kind,n.Total);
   Set(__instance,"notes",notes);Set(__instance,"<Supported>k__BackingField",supported);Set(__instance,"<Warning>k__BackingField",warning);Set(__instance,"<Totals>k__BackingField",total);return false;
  }
  static void Health(SpcDocument __0,ref ChartIssue[] __result){
   var model=TimingGroups.Model(__0);if(!model.Extended||model.Errors.Count>0)return;
   // Inspect the complete document, never the active group or current visibility.
   // hidegroup changes rendering only; its input notes retain native constraints.
   var events=TimingGroups.Events(__0);var byId=events.ToDictionary(e=>e.SourceId);
   var result=__result.Where(i=>!(i.Kind==ChartIssueKind.UnknownStatement&&i.SourceIds.All(id=>model.Markers.Contains(id)||model.Visibility.ContainsKey(id)))&&
    !(i.Kind==ChartIssueKind.SkyAreaTooShort&&i.SourceIds.All(id=>byId.ContainsKey(id)&&PassiveVisualSky(byId[id])))).ToList();
   var covered=new HashSet<string>(result.SelectMany(i=>i.SourceIds.Select(id=>i.Kind+":"+id)));
   foreach(var note in events.Where(n=>(n.Kind==EventKind.Hold||n.Kind==EventKind.SkyArea)&&n.DurationMs<5&&!PassiveVisualSky(n))){
    var kind=note.Kind==EventKind.Hold?ChartIssueKind.HoldTooShort:ChartIssueKind.SkyAreaTooShort;
    if(covered.Add(kind+":"+note.SourceId))result.Add(new ChartIssue(kind,new[]{note.SourceId},new[]{__0.SourceIndex(note.SourceId)+1},note.TimeMs,__0.SourceLine(note.SourceId)));
   }
   __result=result.OrderBy(i=>i.Lines.Min()).ThenBy(i=>i.Kind).ToArray();
  }
  static bool PassiveVisualSky(SpcEvent note){return note.Kind==EventKind.SkyArea&&note.DurationMs>0&&TimingGroups.NoInput(note);}
  internal static string HealthLabel(StudioBootstrap boot,string text){
   if(boot==null||!boot.HasDocument||string.IsNullOrEmpty(text))return text;
   string marker="\n源行：";int at=text.LastIndexOf(marker,StringComparison.Ordinal);
   if(at<0){marker="\nSource lines: ";at=text.LastIndexOf(marker,StringComparison.Ordinal);}if(at<0)return text;
   if(!ReferenceEquals(GroupRuntime.Get(boot,"checkHistory"),boot.History)||(int)GroupRuntime.Get(boot,"checkRevision")!=boot.History.Revision)return text;
   int line;if(!int.TryParse(text.Substring(at+marker.Length),out line)||line<1||line>boot.History.Document.LineCount)return text;
   var document=boot.History.Document;var model=TimingGroups.Model(document);if(model.Errors.Count>0||!model.Extended)return text;
   int id=document.LineIdAt(line-1);var note=TimingGroups.Events(document).FirstOrDefault(n=>n.SourceId==id);
   if(note==null||(note.Kind!=EventKind.Hold&&note.Kind!=EventKind.SkyArea)||note.DurationMs>=5||PassiveVisualSky(note))return text;
   string group=model.Of(id);bool hidden=model.VisibilityByGroup.ContainsKey(group),cn=(bool)GroupRuntime.Get(boot,"chinese");
   var ci=System.Globalization.CultureInfo.InvariantCulture;
   string detail=cn?"时间组："+(group==""?"默认group":group)+" · 持续 "+note.DurationMs.ToString("0.##########",ci)+" ms":
    "Timinggroup: "+(group==""?"default":group)+" · duration "+note.DurationMs.ToString("0.##########",ci)+" ms";
   if(hidden)detail+=cn?"\nhidegroup 只隐藏显示，物件仍参与判定。":"\nhidegroup hides visuals only; this note still participates in judgement.";
   if(note.Kind==EventKind.SkyArea&&Math.Round(note.DurationMs,MidpointRounding.ToEven)<2)
    detail+=cn?"\n按整数毫秒写出后不足 2 ms，可能没有判定事件，存在本体加载失败风险。":"\nBelow 2 ms after integer-ms encoding: may produce no judgement events and fail to load in the game.";
   return text+"\n"+detail;
  }
  static bool PrepareTiming(SpcDocument __0,SpcDocument __1,ref string[] __result){
   var target=TimingGroups.Model(__0);var reference=TimingGroups.Model(__1);if(!target.Extended&&!reference.Extended)return true;TimingGroups.Validate(__0);TimingGroups.Validate(__1);
   string name=TimingGroups.Current;var dst=__0.ReadEvents().Where(e=>e.Kind==EventKind.Chart||TimingGroups.NameOf(e)==name).ToArray();
   string sourceName=reference.Find(name)!=null?name:"";var src=__1.ReadEvents().Where(e=>e.Kind==EventKind.Chart||TimingGroups.NameOf(e)==sourceName).ToArray();
   var a=src.FirstOrDefault(e=>e.Kind==EventKind.Chart);var b=dst.FirstOrDefault(e=>e.Kind==EventKind.Chart);
   if(a==null||b==null||a.Get(0)!=b.Get(0)||a.Get(1)!=b.Get(1)||src.Any(e=>e.Name=="icp_unknown_note")||dst.Any(e=>e.Name=="icp_unknown_note")||TickCountResearch.SemanticSignature(src.Where(e=>e.IsNote))!=TickCountResearch.SemanticSignature(dst.Where(e=>e.IsNote)))throw new ArgumentException("配对谱面的当前时间组内容不匹配，已取消补入。");
   var missing=new List<string>();var known=dst.Where(e=>e.Kind==EventKind.Bpm).ToList();
   foreach(var e in src.Where(e=>e.Kind==EventKind.Bpm).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId)){
    if(AuthoringSafety.Reason(e)!=null||e.Get(1)<=0)throw new ArgumentException("配对谱面的 BPM 超出支持范围。");
    var at=known.Where(x=>x.TimeMs==e.TimeMs).ToArray();if(at.Any(x=>x.Get(1)!=e.Get(1)||x.Get(2,-1)!=e.Get(2,-1)||x.Get(3,-1)!=e.Get(3,-1)))throw new ArgumentException("当前时间组存在冲突的 BPM，请手动处理。");
    if(at.Length==0){missing.Add(__1.SourceLine(e.SourceId));known.Add(e);}
   }__result=missing.ToArray();return false;
  }
 }
}

