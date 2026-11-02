using System;
using System.Collections.Generic;
using System.Linq;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 // Exact count inputs, not a hash. Valid geometry changes keep the provisional
 // schedule; a changed validity reason, unknown record, timing or membership
 // rebuilds it. The extended-group path never certifies reference evidence.
 internal sealed class CountPartCache {
  internal static bool Enabled=true;
  sealed class Row {
   internal int Id;internal EventKind Kind;internal string Name,Invalid;
   internal double Time,Duration;internal double[] Args;
   internal Row(SpcEvent e){Id=e.SourceId;Kind=e.Kind;Name=e.Name;Time=e.TimeMs;Duration=e.DurationMs;Invalid=e.IsNote?ChartMath.InvalidReason(e):null;Args=(double[])e.Args.Clone();}
   internal bool Matches(SpcEvent e){return Id==e.SourceId&&Kind==e.Kind&&Name==e.Name&&Time.Equals(e.TimeMs)&&Duration.Equals(e.DurationMs)&&(Args.SequenceEqual(e.Args)||e.IsNote&&Invalid==ChartMath.InvalidReason(e));}
  }
  static readonly Dictionary<string,CountPartCache> entries=new Dictionary<string,CountPartCache>(StringComparer.Ordinal);
  readonly Row[] rows;readonly SpcEvent[] source;ProvisionalComboTimeline timeline;
  internal readonly ComboCountSummary Summary;
  CountPartCache(SpcEvent[] part){source=part.Select(e=>new SpcEvent{SourceId=e.SourceId,Kind=e.Kind,Name=e.Name,Args=(double[])e.Args.Clone()}).ToArray();rows=part.Select(e=>new Row(e)).ToArray();Summary=TickCountResearch.Analyze(part);}
  internal ProvisionalComboTimeline Timeline {get{if(timeline==null)timeline=new ProvisionalComboTimeline(source);return timeline;}}
  internal static CountPartCache Get(string name,SpcEvent[] part){
   var relevant=part.Where(e=>e.IsNote||e.Kind==EventKind.Chart||e.Kind==EventKind.Bpm||e.Kind==EventKind.Unknown).ToArray();
   if(!Enabled)return new CountPartCache(relevant);
   CountPartCache cached;
   if(entries.TryGetValue(name,out cached)&&cached.rows.Length==relevant.Length){bool same=true;for(int i=0;i<relevant.Length&&same;i++)same=cached.rows[i].Matches(relevant[i]);if(same)return cached;}
   if(entries.Count>128)entries.Clear();return entries[name]=new CountPartCache(relevant);
  }
  internal static ProvisionalComboNote Copy(ProvisionalComboNote n){return new ProvisionalComboNote{Id=n.Id,Kind=n.Kind,Start=n.Start,End=n.End,StartBpm=n.StartBpm,IntervalMs=n.IntervalMs,Total=n.Total,CrossesBpm=n.CrossesBpm};}
 }
}
