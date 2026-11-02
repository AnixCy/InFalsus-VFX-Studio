using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  Process rainProcess;EditHistory rainHistory;int rainRevision;string rainTarget,rainCurrent,rainRequestPath,rainOutputPath;bool rainNewGroup,rainEstimate;float rainStarted;ToolState rainTool;ToolRequest rainRequest;readonly StringBuilder toolErrors=new StringBuilder();
  void StartExternalTool(bool estimate){
   if(rainProcess!=null||externalTool==null||!boot.HasDocument||Transport.Playing)return;if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
   try{
    var tool=externalTool;if((tool.Manifest.output=="split"||UniversalTool(tool.Manifest))&&!(bool)Call(boot,"CommitPendingNoteNow"))return;if(tool.Manifest.output=="blackline"&&!AirOverflow.Enabled)throw new ArgumentException(RainL("请先在设置 → 操作开启天空超界。","Enable out-of-bounds sky notes under Settings > Controls first."));
    Call(boot,"CancelGesture");var history=boot.History;TimingGroups.Validate(history.Document);string current=TimingGroups.Current;bool create=ToolGroupOption(tool.Manifest)&&tool.CreateGroup;string target=create?TimingGroups.NextName(history.Document):current;
    if(ToolGroupOption(tool.Manifest)&&!create&&(target==""||!TimingGroups.Model(history.Document).Find(target).NoInput))throw new ArgumentException(RainL("请选择 noinput / noclip 组或开启新建组。","Select a noinput/noclip group or enable Create Group."));
    double max=TimingGroups.Events(history.Document).Where(e=>e.Kind==EventKind.SkyArea).Select(e=>e.Get(10,-1)).DefaultIfEmpty(-1).Max();if(tool.Manifest.output=="blackline"&&max>int.MaxValue-16001)throw new ArgumentException("No groupid range is available.");
    var beat=TimingGroups.Model(history.Document).Beat(current).Points.LastOrDefault(p=>p.Time<=Transport.TimeMs);var request=new ToolRequest{tool=tool.Manifest.id,groupStart=max<int.MaxValue?(int)Math.Floor(max)+1:-1,values=tool.Manifest.fields.Select(f=>new ToolValue{key=f.key,value=tool.Values[f.key]}).ToArray(),segments=tool.Segments,bpm=beat.Bpm,meter=beat.Meter};
    if(UniversalTool(tool.Manifest))PrepareUniversalRequest(request,tool,history.Document,target,create);
    PrepareToolGrid(request,tool);
    if(tool.Manifest.output=="split")PrepareTimingSplit(request,history.Document);
    if(tool.Manifest.output=="timingedit")PrepareTimingEdit(request,history.Document);
    if(tool.Manifest.id=="MakeRain"){request.start=ToolInt(request,"start");request.end=ToolInt(request,"end");request.count=ToolInt(request,"count");request.intervalMin=ToolInt(request,"intervalMin");request.intervalMax=ToolInt(request,"intervalMax");request.xMin=ToolDouble(request,"xMin");request.xMax=ToolDouble(request,"xMax");}
    string cache=Path.Combine(BepInEx.Paths.GameRootPath,"VisualUIData","ScriptCache");Directory.CreateDirectory(cache);string token=Guid.NewGuid().ToString("N");rainRequestPath=Path.Combine(cache,token+".request.json");rainOutputPath=Path.Combine(cache,token+".spc");File.WriteAllText(rainRequestPath,ToolJson.Write(request),new UTF8Encoding(false));
    string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");var info=new ProcessStartInfo(powershell,"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+RainQuote(Path.Combine(tool.Folder,tool.Manifest.script))+" -RequestPath "+RainQuote(rainRequestPath)+" -OutputPath "+RainQuote(rainOutputPath)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=tool.Folder};
    rainHistory=history;rainRevision=history.Revision;rainCurrent=current;rainTarget=target;rainNewGroup=create;rainTool=tool;rainRequest=request;rainEstimate=estimate;rainStarted=Time.realtimeSinceStartup;lock(toolErrors)toolErrors.Length=0;
    rainProcess=new Process{StartInfo=info};rainProcess.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)Logger.LogDebug("External tool: "+e.Data);};rainProcess.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null){lock(toolErrors)if(toolErrors.Length<3000)toolErrors.AppendLine(e.Data);Logger.LogWarning("External tool: "+e.Data);}};
    if(!rainProcess.Start())throw new IOException("Could not start Windows PowerShell.");rainProcess.BeginOutputReadLine();rainProcess.BeginErrorReadLine();SaveTool(tool);externalMessage=RainL("正在生成…","Generating…");GUI.FocusControl(null);
   }catch(Exception e){CancelRain(false);ToolError(e);}
  }
  static string RainQuote(string value){if(value.IndexOf('"')>=0)throw new ArgumentException("Unexpected quote in script path.");return "\""+value+"\"";}
  void TickRain(){
   if(rainProcess==null)return;
   try{
    if(boot==null||!ReferenceEquals(boot.History,rainHistory)||boot.History.Revision!=rainRevision||TimingGroups.Current!=rainCurrent||rainRequest.gridDivision>0&&(int)Prop(boot,"Division")!=rainRequest.gridDivision){CancelRain(true);return;}if(!rainProcess.HasExited){if(Time.realtimeSinceStartup-rainStarted>120)throw new TimeoutException("External tool timed out; chart unchanged.");return;}
    rainProcess.WaitForExit();int exit=rainProcess.ExitCode;rainProcess.Dispose();rainProcess=null;if(exit!=0){string errors;lock(toolErrors)errors=toolErrors.ToString();throw new IOException("External tool failed: "+errors);}
    if(!File.Exists(rainOutputPath)||new FileInfo(rainOutputPath).Length>8000000)throw new IOException("Missing or oversized script output.");string[] lines=File.ReadAllLines(rainOutputPath);if(rainTool.Manifest.output!="spc")lines=lines.Where(s=>!string.IsNullOrWhiteSpace(s)).ToArray();ValidateExternalOutput(lines,rainRequest,rainTool.Manifest.output);int spcCount=rainTool.Manifest.output=="spc"?InsertSpcOutput(rainHistory.Document.Clone(),lines,rainTarget,rainNewGroup,rainRequest):0;
    var history=rainHistory;int revision=rainRevision;string target=rainTarget,current=rainCurrent;bool create=rainNewGroup,estimate=rainEstimate;var request=rainRequest;string output=rainTool.Manifest.output;string message="";int generatedCount=-1;string summary=rainOutputPath+".info.json";if(File.Exists(summary)&&new FileInfo(summary).Length<10000){var data=ToolJson.Read<ToolInfo>(File.ReadAllText(summary));if(data!=null){message=data.message??"";if(data.segments>0)generatedCount=data.segments;}}CleanupRainFiles();
    Queue(delegate{try{
     if(!ReferenceEquals(boot.History,history)||history.Revision!=revision||TimingGroups.Current!=current)throw new InvalidOperationException(RainL("生成期间谱面或时间组发生变化，请重新执行。","The chart or timinggroup changed during generation. Run again."));if(Transport.Playing)throw new InvalidOperationException(RainL("请暂停后重新执行。","Pause playback and run again."));if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;if(history.Revision!=revision)throw new InvalidOperationException("The chart changed after committing fields; run again.");
     int count=output=="spc"?(generatedCount>=0?generatedCount:spcCount):output=="timingedit"?request.timingEdit.rows.Length:output=="blackline"?lines.Length/2:lines.Length;if(!estimate){bool previous=rainInserting;rainInserting=true;try{GroupChange(history,d=>InsertExternalOutput(d,lines,target,create,request,output));}finally{rainInserting=previous;}}
     externalMessage=(output=="timingedit"?(estimate?RainL("预计处理 ","Would process "):RainL("已处理 ","Processed ")):(estimate?RainL("预计生成 ","Would generate "):RainL("已生成 ","Generated ")))+count+(output=="blackline"?RainL(" 条黑线。"," blackline segments."):output=="split"?RainL(" 段 SkyArea。"," SkyArea pieces."):output=="spc"?(generatedCount>=0?RainL(" 个物件。"," objects."):RainL(" 行谱面操作。"," SPC row operations.")):RainL(" 个时序事件。"," timing events."))+"\n"+message+"\n"+(estimate?RainL("谱面未修改。","Chart unchanged."):RainL("Ctrl+Z 一次撤销；Ctrl+S 保存。","Ctrl+S saves; Ctrl+Z undoes this batch."));Set(boot,"status",externalMessage);
    }catch(Exception e){ToolError(e);}});
   }catch(Exception e){CancelRain(false);ToolError(e);}
  }
  internal static void InsertExternalOutput(SpcDocument d,string[] lines,string target,bool create,ToolRequest request,string output){
   bool previous=TimingGroups.ExplicitEdit;TimingGroups.ExplicitEdit=true;
   try{
    if(output=="spc"){InsertSpcOutput(d,lines,target,create,request);return;}
    if(output=="timingedit"){InsertTimingEdit(d,lines,request);return;}
    if(output=="split"){InsertTimingSplit(d,lines,request);return;}
    if(create){TimingGroups.Add(d,target);GroupProperties.SetProperty(d,target,"noinput",true);}
    if(output=="timing"&&ToolString(request,"replace")=="true"){var kind=ToolString(request,"eventType")=="bpm"?EventKind.Bpm:EventKind.Track;int begin=ToolInt(request,"start"),end=ToolInt(request,"end");var model=TimingGroups.Model(d);var ids=TimingGroups.Events(d).Where(e=>e.Kind==kind&&model.Of(e.SourceId)==target&&e.TimeMs>=begin&&e.TimeMs<=end).Select(e=>e.SourceId).ToArray();foreach(int id in ids)d.Delete(id);}
    var inserted=new List<int>();foreach(string line in lines)inserted.Add(d.Append(line));TimingGroups.Move(d,inserted,target);
   }finally{TimingGroups.ExplicitEdit=previous;}
  }
  internal static void ValidateExternalOutput(string[] lines,ToolRequest r,string output){
   if(output=="spc"){ReadSpcOutput(lines);return;}
   if(output=="timingedit"){ValidateTimingEdit(lines,r);return;}
   if(output=="split"){ValidateTimingSplit(lines,r);return;}
   if(lines.Length<1||lines.Length>32000||lines.Any(s=>s.Contains("\n")||s.Contains("\r")||(output=="blackline"?!s.StartsWith("skyarea(",StringComparison.Ordinal):!s.StartsWith(ToolString(r,"eventType")=="bpm"?"bpm(":"track(",StringComparison.Ordinal))))throw new InvalidDataException("Unexpected external-tool statements.");
   var parsed=SpcDocument.Parse("chart(138,4)\n"+string.Join("\n",lines)).ReadEvents().Where(e=>e.Kind!=EventKind.Chart).ToArray();if(parsed.Length!=lines.Length||parsed.Any(e=>AuthoringSafety.Reason(e)!=null||ChartMath.InvalidReason(e)!=null))throw new InvalidDataException("Invalid external-tool output.");
   if(output=="timing"){int start=ToolInt(r,"start"),end=ToolInt(r,"end");var kind=ToolString(r,"eventType")=="bpm"?EventKind.Bpm:EventKind.Track;if(parsed.Any(e=>e.Kind!=kind||e.TimeMs<start||e.TimeMs>end))throw new InvalidDataException("Generated timing outside requested range/kind.");return;}
   if(parsed.Any(e=>e.Kind!=EventKind.SkyArea||e.DurationMs<1||e.Get(2)!=100||e.Get(5)!=100||e.Get(3)!=1||e.Get(6)!=1))throw new InvalidDataException("Blacklines must be positive-duration width-1 SkyAreas at split 100.");
   foreach(var group in parsed.GroupBy(e=>e.Get(10,-1))){var pair=group.ToArray();if(pair.Length!=2||group.Key<r.groupStart||group.Key>=r.groupStart+16000||group.Key!=Math.Truncate(group.Key))throw new InvalidDataException("Invalid blackline groupid pair.");var helper=pair.FirstOrDefault(e=>e.Get(1)==10000&&e.Get(4)==10000&&e.DurationMs==1);if(helper==null)throw new InvalidDataException("Missing hidden head.");var note=pair.First(e=>!ReferenceEquals(e,helper));if(helper.TimeMs!=Math.Max(0,note.TimeMs-1)||Math.Min(note.Get(1),note.Get(4))< -2000||Math.Max(note.Get(1),note.Get(4))>2000||pair[0]!=helper)throw new InvalidDataException("Invalid hidden-head ordering or X bounds.");}
   if(r.tool=="MakeRain"&&(lines.Length!=r.count*2||parsed.Any(e=>e.TimeMs<r.start||e.EndMs>r.end)))throw new InvalidDataException("Make Rain count/time bounds changed.");
  }
  void CleanupRainFiles(){foreach(string path in new[]{rainRequestPath,rainOutputPath,rainOutputPath==null?null:rainOutputPath+".info.json"})if(path!=null)try{if(File.Exists(path))File.Delete(path);}catch(Exception e){Logger.LogWarning("Tool cache cleanup: "+e.Message);}rainRequestPath=rainOutputPath=null;}
  void CancelRain(bool announce){if(rainProcess!=null){try{if(!rainProcess.HasExited){rainProcess.Kill();rainProcess.WaitForExit(1000);}}catch(Exception e){Logger.LogWarning("Cancel external tool: "+e.Message);}finally{rainProcess.Dispose();rainProcess=null;}}CleanupRainFiles();if(announce)externalMessage=RainL("已取消生成，谱面未修改。","Generation cancelled; chart unchanged.");}
 }
}
