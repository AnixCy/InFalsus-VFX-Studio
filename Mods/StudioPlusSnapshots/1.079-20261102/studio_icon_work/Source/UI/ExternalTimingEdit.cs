using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  [Serializable] public sealed class ToolTimingRow {public int id;public string source,editable;public double time;}
  [Serializable] public sealed class ToolTimingData {public string action,scope;public double start,end,target;public ToolTimingRow[] rows;}
  EditHistory timingRangeHistory;double timingRangeStart,timingRangeEnd;bool timingRangeReady;
  void RememberTimingRange(){timingRangeStart=Math.Min(Val<double>(boot,"rangeStart"),Val<double>(boot,"hoverTime"));timingRangeEnd=Math.Max(Val<double>(boot,"rangeStart"),Val<double>(boot,"hoverTime"));timingRangeHistory=boot.History;timingRangeReady=true;}
  bool HasTimingRange {get{return timingRangeReady&&ReferenceEquals(timingRangeHistory,boot.History);}}
  void TimingRangePanel(){
   var row=ContentRow(70);RowLine(row);UiText(new Rect(0,row.y,405*scale,31*scale),HasTimingRange?RainL("段落范围：","Selected range: ")+DisplayPrecision.Number(timingRangeStart.ToString("R",CultureInfo.InvariantCulture),4)+" – "+DisplayPrecision.Number(timingRangeEnd.ToString("R",CultureInfo.InvariantCulture),4)+" ms":RainL("尚未选择段落时间范围。","No time range selected."),13,Css("#a6c3d1"));
   if(FlatButton(new Rect(0,row.y+32*scale,405*scale,32*scale),RainL("在轨道上选择起止时间","Select start and end on the track"),14))Queue(delegate{Call(boot,"BeginRangeSelection");panel="";});
  }
  void PrepareTimingEdit(ToolRequest request,SpcDocument document){
   if(!HasTimingRange)throw new ArgumentException(RainL("请先使用段落选择点击开始和结束时间。","Use Range Selection to pick a start and an end first."));
   string kind=ToolString(request,"eventType");if(kind!="both"&&kind!="track"&&kind!="bpm")throw new ArgumentException("Invalid timing kind.");string scope=TimingGroups.Current;var model=TimingGroups.Model(document);
   var rows=TimingGroups.Events(document).Where(n=>model.Of(n.SourceId)==scope&&n.TimeMs>=timingRangeStart&&n.TimeMs<=timingRangeEnd&&(n.Kind==EventKind.Track&&(kind=="both"||kind=="track")||n.Kind==EventKind.Bpm&&(kind=="both"||kind=="bpm"))).OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).Select(n=>new ToolTimingRow{id=n.SourceId,source=document.SourceLine(n.SourceId),editable=document.EditableSourceLine(n.SourceId),time=n.TimeMs}).ToArray();
   if(rows.Length==0)throw new ArgumentException(RainL("当前时间组在所选范围内没有匹配的 timing。","No matching timing events in the current timinggroup and selected range."));if(rows.Length>32000)throw new ArgumentException("More than 32000 timing events.");
   bool copy=request.tool=="CopyTiming";double target=copy?ToolInt(request,"target"):timingRangeStart;if(copy&&(target<0||target+timingRangeEnd-timingRangeStart>86400000))throw new ArgumentException("Copied range must stay within 0..86400000 ms.");
   request.timingEdit=new ToolTimingData{action=copy?"copy":"delete",scope=scope,start=timingRangeStart,end=timingRangeEnd,target=target,rows=rows};
  }
  ToolTimingData CaptureQuickTiming(string action){
   if(!HasTimingRange)throw new ArgumentException(RainL("请先选择时间范围。","Select a time range first."));
   var document=boot.History.Document;var model=TimingGroups.Model(document);string scope=TimingGroups.Current;
   var rows=TimingGroups.Events(document).Where(n=>model.Of(n.SourceId)==scope&&n.TimeMs>=timingRangeStart&&n.TimeMs<=timingRangeEnd&&(n.Kind==EventKind.Track||n.Kind==EventKind.Bpm))
    .OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).Select(n=>new ToolTimingRow{id=n.SourceId,source=document.SourceLine(n.SourceId),editable=document.EditableSourceLine(n.SourceId),time=n.TimeMs}).ToArray();
   if(rows.Length==0)throw new ArgumentException(RainL("当前时间组的选区没有 BPM 或 Track。","No BPM or Track events in this range of the current timinggroup."));
   if(rows.Length>32000)throw new ArgumentException("More than 32000 timing events.");
   return new ToolTimingData{action=action,scope=scope,start=timingRangeStart,end=timingRangeEnd,target=timingRangeStart,rows=rows};
  }
  internal static string CopyTimingLine(ToolTimingRow row,ToolTimingData data){string source=row.editable??row.source;return Regex.Replace(source,@"^(\s*(?:track|bpm)\s*\(\s*)[^,]+",m=>m.Groups[1].Value+(row.time+data.target-data.start).ToString("0.##########",CultureInfo.InvariantCulture));}
  static void ValidateTimingEdit(string[] lines,ToolRequest request){var data=request.timingEdit;if(data==null||data.rows==null||data.rows.Length==0||data.rows.Length>32000||lines.Length!=(data.action=="delete"?0:data.rows.Length))throw new InvalidDataException("Timing edit output count changed.");if(data.action!="delete"&&data.action!="copy")throw new InvalidDataException("Invalid timing action.");if(data.action=="copy")for(int i=0;i<lines.Length;i++)if(lines[i]!=CopyTimingLine(data.rows[i],data))throw new InvalidDataException("Copied timing must retain every value and relative time.");}
  static void InsertTimingEdit(SpcDocument document,string[] lines,ToolRequest request){var data=request.timingEdit;var model=TimingGroups.Model(document);foreach(var row in data.rows)if(!document.Contains(row.id)||document.SourceLine(row.id)!=row.source||model.Of(row.id)!=data.scope)throw new InvalidOperationException("Source timing changed during generation.");if(data.action=="delete"){foreach(var row in data.rows)document.Delete(row.id);}else{var ids=new List<int>();foreach(string line in lines)ids.Add(document.Append(line));TimingGroups.Move(document,ids,data.scope);}}
 }
}
