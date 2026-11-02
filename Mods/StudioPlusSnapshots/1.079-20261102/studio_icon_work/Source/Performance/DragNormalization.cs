using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 // Private mutable working nodes, never returned by ReadEvents or used by the
 // renderer. Only a numeric captured drag can enter this path; source membership
 // and the transaction-start document are immutable for its lifetime.
 internal static class DragNormalization {
  sealed class Baseline {
   internal readonly Dictionary<int,SpcEvent> Notes;internal readonly HashSet<int> Valid;
   internal Baseline(SpcDocument d){Notes=d.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).ToDictionary(n=>n.SourceId);Valid=new HashSet<int>(Notes.Values.Where(n=>ChartMath.InvalidReason(n)==null).Select(n=>n.SourceId));}
  }
  sealed class Scratch {
   internal SpcDocument Original;internal Baseline Base;internal Dictionary<int,SpcEvent> Nodes;
   internal HashSet<int> Dirty=new HashSet<int>(),Invalid=new HashSet<int>(),Affected=new HashSet<int>();
   internal readonly Dictionary<long,List<SpcEvent>> Tails=new Dictionary<long,List<SpcEvent>>();internal readonly Stack<List<SpcEvent>> Pool=new Stack<List<SpcEvent>>();internal readonly List<SpcEvent> Ordered=new List<SpcEvent>();
  }
  static readonly ConditionalWeakTable<SpcDocument,Baseline> originals=new ConditionalWeakTable<SpcDocument,Baseline>();
  static readonly ConditionalWeakTable<SpcDocument,Scratch> working=new ConditionalWeakTable<SpcDocument,Scratch>();
  static Scratch Prepare(SpcDocument original,SpcDocument next){
   var s=working.GetValue(next,d=>new Scratch());
   if(s.Original!=original){
    s.Original=original;s.Base=originals.GetValue(original,d=>new Baseline(d));s.Nodes=new Dictionary<int,SpcEvent>();s.Dirty.Clear();s.Invalid.Clear();
    foreach(var p in s.Base.Notes){var n=p.Value;var copy=new SpcEvent{SourceId=n.SourceId,Kind=n.Kind,Name=n.Name,Args=n.Args};TimingGroups.TagEvent(next,copy);s.Nodes[p.Key]=copy;}
   }
   return s;
  }
  internal static void Warm(SpcDocument original,SpcDocument next){Prepare(original,next);}
  internal static void Normalize(SpcDocument original,SpcDocument next){
   var s=Prepare(original,next);
   foreach(int id in s.Dirty)s.Nodes[id].Args=s.Base.Notes[id].Args;s.Dirty.Clear();s.Invalid.Clear();
   foreach(int id in DragPreview.Written){SpcEvent node;if(!s.Nodes.TryGetValue(id,out node))continue;var current=DocumentAcceleration.EventAt(next,id);s.Dirty.Add(id);
    if(current==null||current.Kind!=EventKind.SkyArea){s.Invalid.Add(id);continue;}node.Args=current.Args;
    if(ChartMath.InvalidReason(node)!=null)s.Invalid.Add(id);
   }
   var notes=s.Ordered;notes.Clear();foreach(var n in s.Nodes.Values)if(SkyGroups.GroupOf(n)>=0&&!s.Invalid.Contains(n.SourceId)&&(s.Dirty.Contains(n.SourceId)||s.Base.Valid.Contains(n.SourceId)))notes.Add(n);notes.Sort((a,b)=>{int order=a.TimeMs.CompareTo(b.TimeMs);return order!=0?order:a.SourceId.CompareTo(b.SourceId);});
   var affected=s.Affected;affected.Clear();var tails=s.Tails;foreach(var row in tails.Values){row.Clear();s.Pool.Push(row);}tails.Clear();
   foreach(var n in notes){var old=s.Base.Notes[n.SourceId];for(int i=0;i<10;i++)if(old.Get(i)!=n.Get(i)){affected.Add(n.SourceId);break;}
    long key=(long)Math.Floor(n.EndMs/SkyGroups.TimeTolerance);List<SpcEvent> row;if(!tails.TryGetValue(key,out row))tails[key]=row=s.Pool.Count>0?s.Pool.Pop():new List<SpcEvent>();row.Add(n);
   }
   foreach(var n in notes){
    long key=(long)Math.Floor(n.TimeMs/SkyGroups.TimeTolerance);SpcEvent prior=null;bool touched=affected.Contains(n.SourceId);
    for(long k=key-1;k<=key+1;k++){List<SpcEvent> row;if(!tails.TryGetValue(k,out row))continue;foreach(var p in row){if(!SkyGroups.Joins(p,n))continue;if(affected.Contains(p.SourceId))touched=true;if(prior==null||p.TimeMs>prior.TimeMs||p.TimeMs==prior.TimeMs&&p.SourceId<prior.SourceId)prior=p;}}
    if(!touched||prior==null)continue;int group=SkyGroups.GroupOf(prior);
    if(SkyGroups.GroupOf(n)!=group){next.SetOptionalNumber(n.SourceId,10,group);if(ReferenceEquals(n.Args,s.Base.Notes[n.SourceId].Args))n.Args=(double[])n.Args.Clone();n.Args[10]=group;s.Dirty.Add(n.SourceId);}affected.Add(n.SourceId);
   }
  }
 }
}
