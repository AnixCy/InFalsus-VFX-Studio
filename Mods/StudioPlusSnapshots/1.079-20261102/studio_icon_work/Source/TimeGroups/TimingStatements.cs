using System;
using System.Globalization;
using System.Linq;
using InFalsusStudio.Core;
namespace InFalsusStudio.TimeGroups {
 internal sealed class TimingStatement {
  internal int Id;internal EventKind Kind;internal bool Inherited;
  internal string[] Cells;internal string Raw,Editable;
  internal double Time;
  internal SpcEvent Source;internal GroupModel Model;internal double Meter;
 }
 internal static class TimingStatements {
  static string N(double value){return AuthoredNumber.Format(value);}
  static void Scope(SpcDocument d,string name){TimingGroups.Validate(d);if(name!=""&&TimingGroups.Model(d).Find(name)==null)throw new ArgumentException("timinggroup 已变化，请重新选择。");}
  static SpcEvent Require(SpcDocument d,string name,int id,EventKind kind){
   Scope(d,name);var e=d.ReadEvents().FirstOrDefault(x=>x.SourceId==id);var model=TimingGroups.Model(d);
   if(e==null||e.Kind!=kind||(kind!=EventKind.Chart&&kind!=EventKind.Bpm&&kind!=EventKind.Track&&!(kind==EventKind.Unknown&&model.Visibility.ContainsKey(id)))||model.Of(id)!=name||kind==EventKind.Chart&&name!="")throw new ArgumentException("时序语句已变化，请重新选择。");return e;
  }
  static TimingStatement Row(SpcDocument d,SpcEvent e,double meter){
   var model=TimingGroups.Model(d);return new TimingStatement{Id=e.SourceId,Kind=e.Kind,Time=e.TimeMs,Raw=model.Source[e.SourceId].Text,Source=e,Model=model,Meter=meter};
  }
  internal static void Materialize(TimingStatement row){
   if(row.Cells!=null)return;var e=row.Source;var model=row.Model;double meter=row.Meter;var tokens=GroupSourceSnapshot.Tokens(model,e.SourceId);var cells=new string[e.Kind==EventKind.Track||e.Kind==EventKind.Unknown?2:3];
   for(int c=0;c<cells.Length;c++){
    if(e.Kind==EventKind.Chart&&c==0){cells[c]="0";continue;}
    int index=e.Kind==EventKind.Chart?c-1:c;
    cells[c]=c==2&&e.Get(index,-1)<=0?N(meter):index<tokens.Length&&!string.IsNullOrWhiteSpace(tokens[index])?tokens[index].Trim():N(c==2?meter:e.Get(index));
   }
   row.Cells=cells;row.Editable=GroupSourceSnapshot.Editable(model,e.SourceId);
  }
  internal static BeatTimeline.Point BeatAt(BeatTimeline beat,double time){var points=beat.Points;int lo=0,hi=points.Count;while(lo<hi){int mid=(lo+hi)/2;if(points[mid].Time<=time)lo=mid+1;else hi=mid;}return points[Math.Max(0,lo-1)];}
  internal static SpcEvent[] Tracks(GroupModel model,string name){SpcEvent[] tracks;if(!model.StatementTracks.TryGetValue(name,out tracks)){tracks=model.Local(name,false).Where(e=>e.Kind==EventKind.Track).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId).ToArray();model.StatementTracks.Add(name,tracks);}return tracks;}
  internal static TimingStatement[] Capture(SpcDocument d,string name,EventKind kind,double now,bool lazy=false){
   Scope(d,name);var all=TimingGroups.Events(d);var model=TimingGroups.Model(d);var beat=model.Beat(name);double at=Math.Max(0,now);
   System.Collections.Generic.Dictionary<EventKind,TimingStatement[]> cache;if(!model.StatementRows.TryGetValue(name,out cache)){cache=new System.Collections.Generic.Dictionary<EventKind,TimingStatement[]>();model.StatementRows.Add(name,cache);}TimingStatement[] rows;bool cached=cache.TryGetValue(kind,out rows);if(cached&&rows.Length>0){if(!lazy)foreach(var row in rows)Materialize(row);return rows;}
   var local=cached?new SpcEvent[0]:all.Where(e=>e.Kind==kind&&(kind!=EventKind.Unknown||model.Visibility.ContainsKey(e.SourceId))&&(kind==EventKind.Chart?name=="":model.Of(e.SourceId)==name)).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId).ToArray();
   if(local.Length>0){rows=local.Select(e=>Row(d,e,BeatAt(beat,Math.Max(0,e.TimeMs)).Meter)).ToArray();if(!lazy)foreach(var row in rows)Materialize(row);cache[kind]=rows;return rows;}
   cache[kind]=new TimingStatement[0];if(kind==EventKind.Chart)return cache[kind];
   if(kind==EventKind.Unknown)return new[]{new TimingStatement{Id=-3,Kind=kind,Inherited=true,Time=at,Cells=new[]{N(at),"0"}}};
   var point=BeatAt(beat,at);SpcEvent source=null;if(kind==EventKind.Track){var tracks=Tracks(model,name);int lo=0,hi=tracks.Length;while(lo<hi){int mid=(lo+hi)/2;if(tracks[mid].TimeMs<=at)lo=mid+1;else hi=mid;}if(lo>0)source=tracks[lo-1];}
   return new[]{new TimingStatement{Id=kind==EventKind.Bpm?-1:-2,Kind=kind,Inherited=true,Time=at,Cells=kind==EventKind.Bpm?new[]{N(at),N(point.Bpm),N(point.Meter)}:new[]{N(at),N(source==null?1:source.Get(1))}}};
  }
  internal static void SetCell(SpcDocument d,string name,int id,EventKind kind,int column,string text){
   var e=Require(d,name,id,kind);int index=kind==EventKind.Chart?column-1:column;
   if(index<0||column>=(kind==EventKind.Track||kind==EventKind.Unknown?2:3))throw new ArgumentException("该字段不可编辑。");
   double value;if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||!AuthoringSafety.Finite(value))throw new ArgumentException("请输入有效数值。");
   if(index==0&&kind!=EventKind.Chart)AuthoringSafety.Time(value);
   else if(kind==EventKind.Unknown&&value!=0&&value!=1)throw new ArgumentException("hidegroup 只接受 0（显示）或 1（隐藏）。");
   else if(kind!=EventKind.Track&&kind!=EventKind.Unknown&&value<=0)throw new ArgumentException("BPM / 拍数须大于 0。");
   if(index>=e.Args.Length)d.SetOptionalNumber(id,index,value);else AuthoredNumber.Set(d,id,index,value);
   string reason=AuthoringSafety.Reason(d.ReadEvents().First(x=>x.SourceId==id));if(reason!=null)throw new ArgumentException(reason);
  }
  internal static double NextTime(SpcDocument d,string name,double from,bool snap,int division){
   Scope(d,name);d.ReadEvents();AuthoringSafety.Time(from);
   double next=snap?new AuthoringGrid(TimingGroups.Model(d).Beat(name)).Adjacent(from,1,Math.Max(1,division)):from+1;
   if(next<=from)next=from+1;return AuthoringSafety.Time(next);
  }
  internal static int Duplicate(SpcDocument d,string name,int id,EventKind kind,bool snap,int division,double now){
   Scope(d,name);SpcEvent source=null;TimingStatement seed=null;
   if(id>=0)source=Require(d,name,id,kind);
   else {seed=Capture(d,name,kind,now).SingleOrDefault(r=>r.Inherited);if(seed==null)throw new ArgumentException("继承行已变化，请重新选择。");}
   double time=NextTime(d,name,source==null?seed.Time:source.TimeMs,snap,division);string raw;
   if(source!=null&&kind!=EventKind.Chart)raw=d.SourceLine(id);
   else {var row=source==null?seed:Row(d,source,4);Materialize(row);string[] cells=row.Cells;raw=(kind==EventKind.Track?"track(":kind==EventKind.Unknown?"hidegroup(":"bpm(")+N(time)+","+cells[1]+(kind==EventKind.Track||kind==EventKind.Unknown?"":","+cells[2])+")";}
   bool old=TimingGroups.ExplicitEdit;TimingGroups.ExplicitEdit=true;int added;
   try{added=d.Append(raw);if(source!=null&&kind!=EventKind.Chart)AuthoredNumber.Set(d,added,0,time);TimingGroups.Move(d,new[]{added},name);}
   finally{TimingGroups.ExplicitEdit=old;}
   return added;
  }
  internal static int Delete(SpcDocument d,string name,int id,EventKind kind){
   Require(d,name,id,kind);if(kind==EventKind.Chart)throw new ArgumentException("全谱初始 BPM / 拍号不能删除。");
   var rows=d.ReadEvents().Where(e=>e.Kind==kind&&(kind!=EventKind.Unknown||TimingGroups.Model(d).Visibility.ContainsKey(e.SourceId))&&TimingGroups.Model(d).Of(e.SourceId)==name).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId).Select(e=>e.SourceId).ToArray();int index=Array.IndexOf(rows,id);d.Delete(id);
   return rows.Length<=1?-1:rows[index+1<rows.Length?index+1:index-1];
  }
  internal static void Source(SpcDocument d,string name,int id,EventKind kind,string expected,string text){
   Require(d,name,id,kind);if(d.SourceLine(id)!=expected)throw new ArgumentException("源语句已变化，请重新选择。");
   d.ReplaceSourceLine(id,text);var e=d.ReadEvents().FirstOrDefault(x=>x.SourceId==id);
   if(e==null||(kind==EventKind.Chart?e.Kind!=EventKind.Chart:kind==EventKind.Unknown?!TimingGroups.IsVisibility(e):e.Kind!=EventKind.Bpm&&e.Kind!=EventKind.Track))throw new ArgumentException("此处只能编辑当前时序语句。");
   string reason=AuthoringSafety.Reason(e);if(reason!=null)throw new ArgumentException(reason);Scope(d,name);
  }
 }
}
