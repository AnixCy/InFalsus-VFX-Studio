using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  [Serializable] public sealed class ToolSplitPoint {public double time;public string[] endpoint;}
  [Serializable] public sealed class ToolSplitData {public int sourceId,groupId;public string scope,source;public string[] tokens;public ToolSplitPoint[] points;}
  static ToolSplitData[] SplitSources(ToolRequest request){return request.splits??(request.split==null?new ToolSplitData[0]:new[]{request.split});}
  void PrepareTimingSplit(ToolRequest request,SpcDocument document){
   var selected=Val<HashSet<int>>(boot,"selection");var notes=TimingGroups.Events(document).Where(n=>selected.Contains(n.SourceId)).OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
   if(notes.Length==0||notes.Length!=selected.Count||notes.Any(n=>n.Kind!=EventKind.SkyArea))throw new ArgumentException(RainL("请选择一根或多根 SkyArea，不要混选其他类型。","Select one or more SkyAreas without other note types."));
   var model=TimingGroups.Model(document);string kind=ToolString(request,"splitKind");if(kind!="both"&&kind!="track"&&kind!="bpm")throw new ArgumentException("Invalid split timing kind.");
   var used=new HashSet<int>(TimingGroups.Events(document).Where(n=>n.Kind==EventKind.SkyArea).Select(SkyGroups.GroupOf).Where(n=>n>=0));var plans=new List<ToolSplitData>();int pieces=0,nextGroup=0;
   request.splitStart=notes.Min(n=>n.TimeMs);request.splitEnd=notes.Max(n=>n.EndMs);
   foreach(var note in notes){
    string reason=ChartMath.InvalidReason(note)??AirReachBounds.Violation(note);double at;
    if(reason!=null||note.DurationMs<=0||ChartMath.SkyMinimumWidth(note,out at)<-1e-10)throw new ArgumentException(reason??RainL("所选 SkyArea 时长或边缘无效。","A selected SkyArea has invalid duration or crossed edges."));
    string scope=model.Of(note.SourceId);
    var cuts=model.Local(scope,false).Where(n=>(n.Kind==EventKind.Track&&(kind=="both"||kind=="track")||n.Kind==EventKind.Bpm&&(kind=="both"||kind=="bpm"))&&n.TimeMs>note.TimeMs&&n.TimeMs<note.EndMs).Select(n=>n.TimeMs).Concat(new[]{note.TimeMs,note.EndMs}).Distinct().OrderBy(t=>t).ToArray();
    if(cuts.Length<=2)continue;
    pieces+=cuts.Length-1;if(pieces>4096)throw new ArgumentException(RainL("总切分超过4096段，请缩小选择范围。","More than 4096 pieces in total; reduce the selection."));
    for(int i=1;i<cuts.Length;i++)if(cuts[i]-cuts[i-1]<.001)throw new ArgumentException(RainL("切分会产生小于 0.001ms 的段，已取消。","A split interval would be shorter than 0.001ms; cancelled."));
    int group=SkyGroups.GroupOf(note);if(group<0){while(used.Contains(nextGroup)){if(nextGroup==int.MaxValue)throw new ArgumentException("No group ID available.");nextGroup++;}group=nextGroup;used.Add(group);}
    string[] original=document.ArgumentTokens(note.SourceId);var points=new ToolSplitPoint[cuts.Length];
    for(int i=0;i<cuts.Length;i++)points[i]=new ToolSplitPoint{time=cuts[i],endpoint=i==0?original.Skip(1).Take(3).ToArray():i==cuts.Length-1?original.Skip(4).Take(3).ToArray():RationalAirCoordinates.Sample(ChartMath.SkyAt(note,cuts[i]),note.Get(2))};
    plans.Add(new ToolSplitData{sourceId=note.SourceId,groupId=group,scope=scope,source=document.SourceLine(note.SourceId),tokens=original,points=points});
   }
   if(plans.Count==0)throw new ArgumentException(RainL("所选紫条持续时间内没有所选时序事件，无需切分。","No selected timing events inside the selected SkyAreas; no split is needed."));
   request.splits=plans.ToArray();request.split=request.splits[0];
  }
  static void ValidateTimingSplit(string[] lines,ToolRequest request){
   var sources=SplitSources(request);if(sources.Length==0||sources.Any(s=>s==null||s.points==null||s.points.Length<3)||sources.Select(s=>s.sourceId).Distinct().Count()!=sources.Length||lines.Length!=sources.Sum(s=>s.points.Length-1)||lines.Length>4096)throw new InvalidDataException("Timing split count differs from requested cut points.");
   var notes=SpcDocument.Parse("chart(138,4)\n"+string.Join("\n",lines)).ReadEvents().Where(n=>n.Kind!=EventKind.Chart).ToArray();if(notes.Length!=lines.Length)throw new InvalidDataException("Invalid split statements.");
   int index=0;foreach(var source in sources)for(int i=0;i<source.points.Length-1;i++){
    var n=notes[index++];var a=source.points[i];var b=source.points[i+1];if(n.Kind!=EventKind.SkyArea||AuthoringSafety.Reason(n)!=null||ChartMath.InvalidReason(n)!=null||n.DurationMs<=0||Math.Abs(n.TimeMs-a.time)>1e-8||Math.Abs(n.EndMs-b.time)>1e-8||n.Get(7)!=0||n.Get(8)!=0||SkyGroups.GroupOf(n)!=source.groupId)throw new InvalidDataException("Split duration, easing, timing or group ID changed.");
    for(int j=0;j<3;j++)if(n.Get(1+j)!=double.Parse(a.endpoint[j],System.Globalization.CultureInfo.InvariantCulture)||n.Get(4+j)!=double.Parse(b.endpoint[j],System.Globalization.CultureInfo.InvariantCulture))throw new InvalidDataException("Split coordinates differ from the sampled original easing.");
   }
  }
  static void InsertTimingSplit(SpcDocument document,string[] lines,ToolRequest request){
   var sources=SplitSources(request);var model=TimingGroups.Model(document);
   foreach(var source in sources)if(!document.Contains(source.sourceId)||document.SourceLine(source.sourceId)!=source.source||model.Of(source.sourceId)!=source.scope)throw new InvalidOperationException("A selected SkyArea changed during generation.");
   int index=0;foreach(var source in sources){document.ReplaceSourceLine(source.sourceId,lines[index++]);var added=new List<int>();for(int i=1;i<source.points.Length-1;i++)added.Add(document.Append(lines[index++]));if(added.Count>0)TimingGroups.Move(document,added,source.scope);}
  }
 }
}
