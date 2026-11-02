using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using InFalsusStudio.Core;
namespace InFalsusStudio.TimeGroups {
 // Nonofficial timinggroup(name): ... groupend(). Never reuse SkyArea groupid.
 internal sealed class GroupBlock {
  internal string Name;internal readonly HashSet<string> Properties=new HashSet<string>(StringComparer.Ordinal);internal bool NoInput {get{return Properties.Contains("noinput")||Properties.Contains("noclip");}}internal bool NoClip {get{return Properties.Contains("noclip");}}internal int Start=-1,End=-1;internal readonly HashSet<int> Members=new HashSet<int>();
 }
 internal sealed class GroupModel {
  internal SpcDocument Document;internal readonly Dictionary<int,GroupSourceRow> Source=new Dictionary<int,GroupSourceRow>();internal readonly Dictionary<string,Dictionary<EventKind,TimingStatement[]>> StatementRows=new Dictionary<string,Dictionary<EventKind,TimingStatement[]>>();internal readonly List<GroupBlock> Groups=new List<GroupBlock>();
  readonly Dictionary<string,GroupBlock> groupsByName=new Dictionary<string,GroupBlock>(StringComparer.Ordinal);
  internal readonly Dictionary<string,SpcEvent[]> StatementTracks=new Dictionary<string,SpcEvent[]>();
  internal readonly Dictionary<int,string> Membership=new Dictionary<int,string>();
  internal readonly HashSet<int> Markers=new HashSet<int>();internal readonly List<string> Errors=new List<string>();
  internal readonly Dictionary<int,VisibilityEvent> Visibility=new Dictionary<int,VisibilityEvent>();
  internal readonly Dictionary<string,VisibilityEvent[]> VisibilityByGroup=new Dictionary<string,VisibilityEvent[]>();
  internal List<SpcEvent> Events;internal readonly Dictionary<string,ScrollTimeline> Scrolls=new Dictionary<string,ScrollTimeline>();
  internal readonly Dictionary<string,SpcEvent[]> LocalTiming=new Dictionary<string,SpcEvent[]>();internal readonly Dictionary<string,BeatTimeline> Beats=new Dictionary<string,BeatTimeline>();
  internal bool Extended {get{return Markers.Count>0||Visibility.Count>0;}}
  internal GroupBlock Find(string name){GroupBlock block;return name!=null&&groupsByName.TryGetValue(name,out block)?block:null;}
  internal string Of(int id){string name;return Membership.TryGetValue(id,out name)?name:"";}
  internal GroupModel(SpcDocument document){
   Document=document;GroupBlock current=null;var source=GroupSourceSnapshot.Read(document);foreach(var row in source)Source.Add(row.Id,row);
   for(int i=0;i<source.Length;i++){
    int id=source[i].Id;string raw=source[i].Text;var start=TimingGroups.Start.Match(raw);
    if(start.Success){
     Markers.Add(id);string name=start.Groups[1].Value.Trim();
     if(current!=null)Errors.Add("第 "+(i+1)+" 行：时间组不能嵌套。");
     else {current=new GroupBlock{Name=name,Start=id};Groups.Add(current);foreach(string prop in start.Groups[2].Value.Split(new[]{',',' ','\t'},StringSplitOptions.RemoveEmptyEntries)){if(!Regex.IsMatch(prop,@"^[A-Za-z_][A-Za-z_0-9]*$"))Errors.Add("第 "+(i+1)+" 行：组属性语法无效。");else current.Properties.Add(prop);}
     if(!TimingGroups.ValidName(name))Errors.Add("第 "+(i+1)+" 行：时间组名称无效。");if(groupsByName.ContainsKey(name))Errors.Add("重复的时间组名称："+name);else groupsByName.Add(name,current);}
    }else if(TimingGroups.End.IsMatch(raw)){
     Markers.Add(id);if(current==null)Errors.Add("第 "+(i+1)+" 行：groupend() 没有对应的时间组。");else{current.End=id;current=null;}
    }else{
     Membership[id]=current==null?"":current.Name;if(current!=null)current.Members.Add(id);
     if(Regex.IsMatch(raw,@"^\s*hidegroup\s*\(")){VisibilityEvent v;if(VisibilityEvent.TryParse(id,current==null?"":current.Name,raw,out v))Visibility.Add(id,v);else Errors.Add("第 "+(i+1)+" 行：应为 hidegroup(ms,0/1)，时间须在 0～86400000 ms。");}
     if(current!=null&&Regex.IsMatch(raw,@"^\s*chart\s*\("))Errors.Add("第 "+(i+1)+" 行：chart 头部只能放在基础组。");
     if(Regex.IsMatch(raw,@"^\s*(timinggroup|groupend)\s*\("))Errors.Add("第 "+(i+1)+" 行：时间组语法无效。");
    }
   }
   if(current!=null)Errors.Add("时间组 "+current.Name+" 缺少 groupend()。");
   foreach(var g in Visibility.Values.GroupBy(v=>v.Name))VisibilityByGroup[g.Key]=g.OrderBy(v=>v.Time).ThenBy(v=>v.Id).ToArray();
  }
  // A clone has identical group boundaries/timing. Share immutable timeline data;
  // numeric timing/structural edits still invalidate this model in full.
  internal GroupModel(GroupModel prior,SpcDocument document){
   Document=document;
   foreach(var p in prior.Source)Source.Add(p.Key,p.Value);
   Groups.AddRange(prior.Groups);foreach(var p in prior.groupsByName)groupsByName.Add(p.Key,p.Value);foreach(var p in prior.Membership)Membership.Add(p.Key,p.Value);
   Markers.UnionWith(prior.Markers);Errors.AddRange(prior.Errors);
   foreach(var p in prior.Visibility)Visibility.Add(p.Key,p.Value);
   foreach(var p in prior.VisibilityByGroup)VisibilityByGroup.Add(p.Key,p.Value);
   foreach(var p in prior.Scrolls)Scrolls.Add(p.Key,p.Value);foreach(var p in prior.Beats)Beats.Add(p.Key,p.Value);
   foreach(var p in prior.LocalTiming)LocalTiming.Add(p.Key,p.Value);
  }
  internal SpcEvent[] Local(string name,bool notes){
   if(Events==null)return new SpcEvent[0];SpcEvent[] cached;if(!notes&&LocalTiming.TryGetValue(name,out cached))return cached;
   var result=new List<SpcEvent>();var own=Events.Where(e=>Of(e.SourceId)==name).ToArray();
   double firstBpm=own.Where(e=>e.Kind==EventKind.Bpm).Select(e=>e.TimeMs).DefaultIfEmpty(double.PositiveInfinity).Min(),firstTrack=own.Where(e=>e.Kind==EventKind.Track).Select(e=>e.TimeMs).DefaultIfEmpty(double.PositiveInfinity).Min();
   foreach(var e in Events){
    if(e.Kind==EventKind.Chart){result.Add(e);continue;}
    if(e.Kind==EventKind.Bpm||e.Kind==EventKind.Track){
     if(Of(e.SourceId)==name){result.Add(e);continue;}
     // Inherit base timing until this group supplies its first local override
     // of that event kind. Absolute times remain shared by every group.
     double first=e.Kind==EventKind.Bpm?firstBpm:firstTrack;
     if(name!=""&&Of(e.SourceId)==""&&e.TimeMs<first)result.Add(e);
    }else if(notes&&Of(e.SourceId)==name)result.Add(e);
   }var array=result.ToArray();if(!notes)LocalTiming[name]=array;return array;
  }
  internal ScrollTimeline Scroll(string name){ScrollTimeline value;if(!Scrolls.TryGetValue(name,out value)){bool old=TimingGroups.Bypass;TimingGroups.Bypass=true;try{value=new ScrollTimeline(Local(name,false));Scrolls.Add(name,value);}finally{TimingGroups.Bypass=old;}}return value;}
  internal BeatTimeline Beat(string name){BeatTimeline value;if(!Beats.TryGetValue(name,out value)){bool old=TimingGroups.Bypass;TimingGroups.Bypass=true;try{value=new BeatTimeline(Local(name,false));Beats.Add(name,value);}finally{TimingGroups.Bypass=old;}}return value;}
 }
 internal sealed class EventGroup {internal GroupModel Model;internal string Name;}
 internal static class TimingGroups {
  internal static readonly BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  internal static readonly Regex Start=new Regex(@"^\s*timinggroup\s*\(\s*([^(),:\r\n]*)(?:,([^()\r\n]*))?\s*\)\s*:\s*(?:(?://|#).*)?$",RegexOptions.CultureInvariant);
  internal static readonly Regex End=new Regex(@"^\s*groupend\s*\(\s*\)\s*;?\s*(?:(?://|#).*)?$",RegexOptions.CultureInvariant);
  internal static bool ValidName(string name){return Regex.IsMatch(name??"",@"^[\p{L}_][\p{L}\p{N}_-]{0,63}$",RegexOptions.CultureInvariant);}
  static readonly ConditionalWeakTable<SpcDocument,GroupModel> models=new ConditionalWeakTable<SpcDocument,GroupModel>();
  static readonly ConditionalWeakTable<SpcEvent,EventGroup> tags=new ConditionalWeakTable<SpcEvent,EventGroup>();
  internal static GroupModel Model(SpcDocument d){return models.GetValue(d,x=>new GroupModel(x));}
  internal static List<SpcEvent> Events(SpcDocument d){var m=Model(d);if(m.Events==null)d.ReadEvents();return m.Events;}
  internal static void CloneModel(SpcDocument before,SpcDocument after){GroupModel m;if(models.TryGetValue(before,out m))models.Add(after,new GroupModel(m,after));}
  internal static void NumericChanged(SpcDocument d,int id){
   GroupModel m;if(!models.TryGetValue(d,out m))return;GroupSourceRow r;
   if(!m.Source.TryGetValue(id,out r)||!Regex.IsMatch(r.Text,@"^\s*(tap|hold|skyarea|flick)\s*\(",RegexOptions.IgnoreCase)){Invalidate(d);return;}
   // Membership/visibility and timing are unchanged by note-number setters.
   m.Source[id]=new GroupSourceRow{Id=id,Index=r.Index,Text=d.SourceLine(id)};m.Events=null;
  }
  internal static void Invalidate(SpcDocument d){models.Remove(d);}
  internal static EventGroup Tag(SpcEvent e){EventGroup tag;return e!=null&&tags.TryGetValue(e,out tag)?tag:null;}
  internal static bool IsVisibility(SpcEvent e){var t=Tag(e);return t!=null&&t.Model.Visibility.ContainsKey(e.SourceId);}
  internal static bool NoInput(SpcEvent e){var t=Tag(e);var b=t==null||t.Model.Errors.Count>0?null:t.Model.Find(t.Name);return e!=null&&e.IsNote&&b!=null&&b.NoInput;}
  internal static bool NoClip(SpcEvent e){var t=Tag(e);var b=t==null||t.Model.Errors.Count>0?null:t.Model.Find(t.Name);return e!=null&&e.IsNote&&b!=null&&b.NoClip;}
  internal static bool Hidden(SpcEvent e,double now){var t=Tag(e);return t!=null&&t.Model.Errors.Count==0&&GroupProperties.Hidden(t.Model,t.Name,now);}
  internal static string NameOf(SpcEvent e){var tag=Tag(e);return tag==null?"":tag.Name;}
  internal static void TagEvent(SpcDocument d,SpcEvent e){var model=Model(d);tags.Remove(e);if(model.Extended)tags.Add(e,new EventGroup{Model=model,Name=model.Of(e.SourceId)});}
  internal static void Read(SpcDocument d,ref List<SpcEvent> result){
   var model=Model(d);model.Events=result;if(!model.Extended&&model.Errors.Count==0)return;
   result=result.Where(e=>!model.Markers.Contains(e.SourceId)).ToList();model.Events=result;
   foreach(var e in result){tags.Remove(e);tags.Add(e,new EventGroup{Model=model,Name=model.Of(e.SourceId)});}
   var handled=new HashSet<string>(model.Markers.Concat(model.Visibility.Keys).Select(id=>"Line "+(model.Source[id].Index+1)+":"));
   d.Diagnostics.RemoveAll(s=>{int colon=s.IndexOf(':');return colon>=0&&handled.Contains(s.Substring(0,colon+1));});
   foreach(var error in model.Errors)d.Diagnostics.Add("[非官方时间组] "+error);
  }
  internal static IEnumerable<SpcEvent> BaseTiming(IEnumerable<SpcEvent> source){return Bypass?source:source.Where(e=>(e.Kind!=EventKind.Bpm&&e.Kind!=EventKind.Track)||NameOf(e)=="").ToArray();}
  [ThreadStatic]internal static bool Bypass,CountBypass;
  [ThreadStatic]internal static string AppendGroup;
  [ThreadStatic]internal static bool ExplicitEdit;
  internal static StudioBootstrap Boot;
  sealed class Workspace {internal string Name="";}
  static readonly ConditionalWeakTable<EditHistory,Workspace> workspaces=new ConditionalWeakTable<EditHistory,Workspace>();
  internal static string Current {
   get{if(ReferenceEquals(Boot,null)||Boot.History==null)return "";var state=workspaces.GetValue(Boot.History,x=>new Workspace());if(state.Name!=""&&Model(Boot.History.Document).Find(state.Name)==null)state.Name="";return state.Name;}
   set{if(!ReferenceEquals(Boot,null)&&Boot.History!=null)workspaces.GetValue(Boot.History,x=>new Workspace()).Name=value??"";}
  }
  internal static void Validate(SpcDocument d){var model=Model(d);if(model.Errors.Count>0)throw new ArgumentException(string.Join("\n",model.Errors.ToArray()));}
  static readonly FieldInfo linesField=typeof(SpcDocument).GetField("lines",F);
  static IList Lines(SpcDocument d){return (IList)linesField.GetValue(d);}
  static int Id(object row){return (int)row.GetType().GetField("Id",F).GetValue(row);}
  static string Text(object row){return (string)row.GetType().GetField("Text",F).GetValue(row);}
  static void Text(object row,string value){row.GetType().GetField("Text",F).SetValue(row,value);}
  static string Eol(object row){return (string)row.GetType().GetField("Eol",F).GetValue(row);}
  static void Eol(object row,string value){row.GetType().GetField("Eol",F).SetValue(row,value);}
  static string Ending(IList rows){foreach(var row in rows)if(Eol(row).Length>0)return Eol(row);return "\n";}
  internal static void Move(SpcDocument d,IEnumerable<int> source,string target){
   Validate(d);var model=Model(d);if(target!=""&&model.Find(target)==null)throw new ArgumentException("目标时间组不存在。");
   var ids=new HashSet<int>(source);if(ids.Overlaps(model.Markers))throw new ArgumentException("不能把时间组边界作为物件移动。");
   var events=d.ReadEvents().ToDictionary(e=>e.SourceId);foreach(int id in ids){SpcEvent e;if(!events.TryGetValue(id,out e)||(!e.IsNote&&e.Kind!=EventKind.Bpm&&e.Kind!=EventKind.Track&&!model.Visibility.ContainsKey(id)))throw new ArgumentException("只能移动物件、BPM、track 和 hidegroup 事件。");}
   var rows=Lines(d);var moving=rows.Cast<object>().Where(r=>ids.Contains(Id(r))).ToArray();if(moving.Length==0)return;
   bool noFinal=rows.Count>0&&Eol(rows[rows.Count-1])=="";string ending=Ending(rows);
   foreach(var row in moving)rows.Remove(row);
   int insert=target==""?0:rows.Cast<object>().ToList().FindIndex(r=>Id(r)==model.Find(target).End);
   if(target==""){int first=model.Groups.Select(g=>rows.Cast<object>().ToList().FindIndex(r=>Id(r)==g.Start)).Where(i=>i>=0).DefaultIfEmpty(rows.Count).Min();insert=first;}
   if(insert<0)throw new ArgumentException("时间组边界已变化。");
   for(int i=0;i<moving.Length;i++){Eol(moving[i],ending);rows.Insert(insert+i,moving[i]);}
   for(int i=0;i<rows.Count-1;i++)if(Eol(rows[i])=="")Eol(rows[i],ending);
   if(noFinal&&rows.Count>0)Eol(rows[rows.Count-1],"");
   Invalidate(d);
  }
  internal static void Appended(SpcDocument d,int id,string call){
   Invalidate(d);if(ExplicitEdit||AppendGroup==null||!Regex.IsMatch(call,@"^\s*(tap|hold|skyarea|flick|bpm|track|hidegroup)\s*\(",RegexOptions.IgnoreCase))return;
   if(AppendGroup!="")Move(d,new[]{id},AppendGroup);else if(Model(d).Extended)Move(d,new[]{id},"");
  }
  internal static void MoveMany(SpcDocument d,int[] ids,string[] targets){
   if(ids.Length!=targets.Length)throw new ArgumentException("Group route length mismatch.");Validate(d);var model=Model(d);
   var routes=new Dictionary<int,string>();for(int i=0;i<ids.Length;i++){
    string target=targets[i]??"";if(target!=""&&model.Find(target)==null)throw new ArgumentException("粘贴目标时间组不存在。");
    if(model.Markers.Contains(ids[i])||!d.Contains(ids[i]))throw new ArgumentException("粘贴物件已变化。");routes.Add(ids[i],target);
   }
   var rows=Lines(d);bool noFinal=rows.Count>0&&Eol(rows[rows.Count-1])=="";string ending=Ending(rows);
   var buckets=new Dictionary<string,List<object>>(StringComparer.Ordinal);var retained=new List<object>(rows.Count);
   foreach(object row in rows){string target;if(routes.TryGetValue(Id(row),out target)){List<object> bucket;if(!buckets.TryGetValue(target,out bucket))buckets[target]=bucket=new List<object>();bucket.Add(row);}else retained.Add(row);}
   var endNames=model.Groups.ToDictionary(g=>g.End,g=>g.Name);int first=model.Groups.Select(g=>g.Start).FirstOrDefault();bool baseDone=false;
   var ordered=new List<object>(rows.Count);List<object> moving;
   foreach(var row in retained){int id=Id(row);
    if(!baseDone&&model.Groups.Count>0&&id==first){if(buckets.TryGetValue("",out moving))ordered.AddRange(moving);baseDone=true;}
    string name;if(endNames.TryGetValue(id,out name)&&buckets.TryGetValue(name,out moving))ordered.AddRange(moving);
    ordered.Add(row);
   }
   if(!baseDone&&buckets.TryGetValue("",out moving))ordered.AddRange(moving);
   if(ordered.Count!=rows.Count)throw new InvalidOperationException("Paste group routing lost a source row.");
   rows.Clear();foreach(var row in ordered)rows.Add(row);
   for(int i=0;i<rows.Count-1;i++)if(Eol(rows[i])=="")Eol(rows[i],ending);
   if(noFinal&&rows.Count>0)Eol(rows[rows.Count-1],"");Invalidate(d);
  }
  internal static bool AssignNotes(SpcDocument d,IEnumerable<int> source,string target){
   Validate(d);var model=Model(d);if(target!=""&&model.Find(target)==null)throw new ArgumentException("目标 timinggroup 已不存在，请重新选择。");
   var ids=new HashSet<int>(source);var notes=d.ReadEvents().Where(e=>e.IsNote&&ids.Contains(e.SourceId)).ToArray();
   if(notes.Length!=ids.Count)throw new ArgumentException("选中的物件已变化，请重新选择。");
   var changed=notes.Where(e=>model.Of(e.SourceId)!=target).Select(e=>e.SourceId).ToArray();if(changed.Length==0)return false;
   Move(d,changed,target);return true;
  }
  internal static string NextName(SpcDocument d){int n=1;while(Model(d).Find("group"+n)!=null)n++;return "group"+n;}
  internal static void Add(SpcDocument d,string name){
   Validate(d);if(!ValidName(name))throw new ArgumentException("名称限 1～64 个中文、字母、数字、下划线或连字符，首字符不能为数字。");
   if(Model(d).Find(name)!=null)throw new ArgumentException("时间组名称已存在。");
   bool old=ExplicitEdit;ExplicitEdit=true;try{d.Append("timinggroup("+name+"):");d.Append("groupend()");}finally{ExplicitEdit=old;}Invalidate(d);
  }
  internal static void Rename(SpcDocument d,string oldName,string next){
   Validate(d);if(!ValidName(next))throw new ArgumentException("时间组名称无效。");var model=Model(d);var block=model.Find(oldName);
   if(block==null||next!=oldName&&model.Find(next)!=null)throw new ArgumentException("时间组不存在或新名称重复。");
   var row=Lines(d).Cast<object>().First(r=>Id(r)==block.Start);string raw=Text(row);var m=Start.Match(raw);int at=m.Groups[1].Index;int front=m.Groups[1].Value.Length-m.Groups[1].Value.TrimStart().Length,back=m.Groups[1].Value.Length-m.Groups[1].Value.TrimEnd().Length;
   Text(row,raw.Substring(0,at)+m.Groups[1].Value.Substring(0,front)+next+(back>0?m.Groups[1].Value.Substring(m.Groups[1].Value.Length-back):"")+raw.Substring(at+m.Groups[1].Length));Invalidate(d);
  }
  internal static void DeleteGroup(SpcDocument d,string name){
   Validate(d);var block=Model(d).Find(name);if(block==null)throw new ArgumentException("基础组不能删除。");
   foreach(int id in block.Members.Concat(new[]{block.Start,block.End}).OrderByDescending(d.SourceIndex).ToArray())d.Delete(id);
   Invalidate(d);
  }
  internal static bool Sort(SpcDocument d){
   Validate(d);var model=Model(d);if(!model.Extended)return false;
   var timed=d.ReadEvents().Where(e=>e.Kind!=EventKind.Chart&&e.Kind!=EventKind.Unknown&&e.Kind!=EventKind.Beam&&AuthoringSafety.Finite(e.TimeMs)).ToDictionary(e=>e.SourceId,e=>e.TimeMs);
   foreach(var v in model.Visibility.Values)timed[v.Id]=v.Time;
   var rows=Lines(d);bool changed=false;int begin=0;
   for(int end=0;end<=rows.Count;end++){
    if(end<rows.Count&&!model.Markers.Contains(Id(rows[end])))continue;
    var slots=Enumerable.Range(begin,end-begin).Where(i=>timed.ContainsKey(Id(rows[i]))).ToArray();var original=slots.Select(i=>rows[i]).ToArray();var sorted=original.OrderBy(r=>timed[Id(r)]).ThenBy(r=>Id(r)).ToArray();var eols=original.Select(Eol).ToArray();
    if(!original.Select(Id).SequenceEqual(sorted.Select(Id))){changed=true;for(int i=0;i<slots.Length;i++){rows[slots[i]]=sorted[i];Eol(sorted[i],eols[i]);}}
    begin=end+1;
   }Invalidate(d);return changed;
  }
 }
}

