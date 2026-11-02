using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  [Serializable] public sealed class ToolSource {
   public int sourceId,index;public string kind,scope,source,editable;public string[] tokens;
   public double start,end,left0,right0,left1,right1,easeL,easeR,split;public double[] grid;
  }
  [Serializable] public sealed class ToolRange {public double start,end;}
  [Serializable] public sealed class ToolOperation {
   public string action,scope,text,expected;public int sourceId;public string[] lines;
  }
  [Serializable] public sealed class ToolPatch {public int version=1;public ToolOperation[] operations;}
  static bool UniversalTool(ToolManifest m){return m.output=="spc";}
  static bool ToolGroupOption(ToolManifest m){return m.output=="blackline"||UniversalTool(m)&&m.groupMode=="noinput";}
  void CaptureUniversalPattern(ToolState state){
   var document=boot.History.Document;var ids=Val<HashSet<int>>(boot,"selection");var events=TimingGroups.Events(document).Where(e=>ids.Contains(e.SourceId)).ToArray();
   if(events.Length<1||events.Length>256||events.Length!=ids.Count||events.Any(e=>e.Kind!=EventKind.Flick&&e.Kind!=EventKind.SkyArea))throw new ArgumentException(RainL("请选择 1～256 个 Flick / SkyArea，不要混选其他物件。","Select 1..256 Flick / SkyArea notes without other note types."));
   foreach(var e in events){string reason=AuthoringSafety.Reason(e)??ChartMath.InvalidReason(e);double at;if(reason!=null||e.Kind==EventKind.SkyArea&&(e.DurationMs<1||ChartMath.SkyMinimumWidth(e,out at)<-1e-10))throw new ArgumentException(reason??"Invalid selected SkyArea duration or crossed edges.");}
   state.Sources=ToolSources(document).Where(r=>ids.Contains(r.sourceId)).OrderBy(r=>r.start).ThenBy(r=>r.sourceId).ToArray();
   File.WriteAllText(Path.Combine(state.Folder,"pattern.json"),ToolJson.Write(new ToolCapture{sources=state.Sources,segments=state.Segments}),new System.Text.UTF8Encoding(false));
   externalMessage=RainL("已原样捕获 "+state.Sources.Length+" 个物件；宽度、缓动、时长和类型均保留。","Captured "+state.Sources.Length+" original notes; width, easing, duration and type retained.");
  }
  ToolSource[] ToolSources(SpcDocument document){
   var model=TimingGroups.Model(document);var events=TimingGroups.Events(document).ToDictionary(e=>e.SourceId);
   return GroupSourceSnapshot.Read(document).Select(row=>{
    SpcEvent e;events.TryGetValue(row.Id,out e);var source=new ToolSource{sourceId=row.Id,index=row.Index,scope=model.Of(row.Id),source=row.Text,editable=GroupSourceSnapshot.Editable(model,row.Id),tokens=GroupSourceSnapshot.Tokens(model,row.Id),kind=e==null?"raw":e.Kind.ToString()};
    if(e!=null&&e.Kind!=EventKind.Unknown&&e.Kind!=EventKind.Beam&&AuthoringSafety.Reason(e)==null){source.start=e.TimeMs;source.end=e.EndMs;
     if(e.Kind==EventKind.SkyArea&&ChartMath.InvalidReason(e)==null){var a=ChartMath.SkyAt(e,e.TimeMs);var b=ChartMath.SkyAt(e,e.EndMs);source.left0=a.Left;source.right0=a.Right;source.left1=b.Left;source.right1=b.Right;source.easeL=e.Get(7);source.easeR=e.Get(8);source.split=e.Get(2);}
     else if(e.Kind==EventKind.Flick&&ChartMath.InvalidReason(e)==null){double c=e.Get(1)/e.Get(2),w=e.Get(3)/e.Get(2);source.left0=source.left1=c-w*.5;source.right0=source.right1=c+w*.5;source.split=e.Get(2);}
    }return source;
   }).ToArray();
  }
  void PrepareUniversalRequest(ToolRequest request,ToolState tool,SpcDocument document,string target,bool create){
   request.document=document.ToText();request.hasBom=document.HasUtf8Bom;request.rows=ToolSources(document);request.currentTime=Transport.TimeMs;request.currentGroup=TimingGroups.Current;request.targetGroup=target;request.createGroup=create;request.overflow=AirOverflow.Enabled;
   request.range=HasTimingRange?new ToolRange{start=timingRangeStart,end=timingRangeEnd}:null;
   var ids=Val<HashSet<int>>(boot,"selection");request.selection=request.rows.Where(r=>ids.Contains(r.sourceId)).OrderBy(r=>r.start).ThenBy(r=>r.sourceId).ToArray();request.capture=tool.Sources??new ToolSource[0];
   string selection=tool.Manifest.selection;
   if(!string.IsNullOrEmpty(selection)&&(request.selection.Length==0||request.selection.Length!=ids.Count||request.selection.Any(r=>selection=="skyarea"?r.kind!="SkyArea":selection=="air"?r.kind!="SkyArea"&&r.kind!="Flick":false)))throw new ArgumentException(RainL("请选择工具要求的物件，不要混选其他类型。","Select the note types requested by this tool without mixing other types."));
   request.skySources=request.selection.Where(r=>r.kind=="SkyArea").ToArray();
  }
  AuthoringGrid MainAuthoringGrid(){GroupRuntime.Sync(boot);return (AuthoringGrid)Get(boot,"authoringGrid");}
  static double[] MainGridTimes(AuthoringGrid grid,double start,double end,int division,int limit){
   var points=grid.Between(start,end,division,limit+1);
   if(points.Count>limit)throw new ArgumentException("Too many main-grid points. Use a coarser subdivision or a smaller range.");
   // TimeMs is the integer timestamp actually drawn by the main authoring grid.
   return points.Select(p=>p.TimeMs).Distinct().OrderBy(t=>t).ToArray();
  }
  void PrepareToolGrid(ToolRequest request,ToolState tool){
   string field=tool.Manifest.gridField;if(string.IsNullOrEmpty(field))return;
   int division=(int)Prop(boot,"Division");request.gridGroup=TimingGroups.Current;
   foreach(var value in request.values)if(value.key==field)value.value=division.ToString(CultureInfo.InvariantCulture);
   if(ToolString(request,"mode")=="count"||ToolString(request,"sampling")!=""&&ToolString(request,"sampling")!="beat")return;
   request.gridDivision=division;var grid=MainAuthoringGrid();
   if(UniversalTool(tool.Manifest)){
    foreach(var source in request.skySources??new ToolSource[0]){
     source.grid=MainGridTimes(grid,source.start,source.end,division,4096).Concat(new[]{source.start}).Distinct().OrderBy(t=>t).ToArray();
     if(source.grid.Length>4096)throw new ArgumentException("More than 4096 grid points for one selected SkyArea.");
    }
   }else{
    int start=ToolInt(request,"start"),end=ToolInt(request,"end");
    if(start<0||end<=start||end>AuthoringSafety.MaxTimelineMs)throw new ArgumentException("End must be after Start, within 0..86400000 ms.");
    request.grid=MainGridTimes(grid,start,end,division,10000);
   }
  }
  static ToolPatch ReadSpcOutput(string[] lines){
   string text=string.Join("\n",lines).TrimStart('\ufeff');ToolPatch patch;
   if(text.TrimStart().StartsWith("{",StringComparison.Ordinal)){patch=ToolJson.Read<ToolPatch>(text);if(patch==null||patch.version!=1||patch.operations==null)throw new InvalidDataException("Expected a version-1 SPC patch.");}
   else{
    // Plain SPC can include whole timinggroup blocks. Without group markers,
    // append to the requested target scope; no tool-specific statement whitelist.
    bool blocks=lines.Any(l=>TimingGroups.Start.IsMatch(l)||Regex.IsMatch(l,@"^\s*groupend\s*\("));
    patch=new ToolPatch{operations=new[]{new ToolOperation{action="append",lines=lines,scope=blocks?null:"$target"}}};
   }
   if(patch.operations.Length>64000)throw new InvalidDataException("More than 64000 patch operations.");
   int count=0;foreach(var op in patch.operations){if(op==null||!new[]{"append","replace","delete"}.Contains(op.action))throw new InvalidDataException("Unknown SPC patch action.");
    if(op.action=="append"){if(op.lines==null)op.lines=op.text==null?new string[0]:new[]{op.text};count+=op.lines.Length;foreach(string line in op.lines)CheckSpcLine(line);}
    else if(op.action=="replace"){CheckSpcLine(op.text);count++;}else count++;
    if(count>64000)throw new InvalidDataException("More than 64000 changed SPC rows.");
   }return patch;
  }
  static void CheckSpcLine(string line){
   if(line==null||line.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidDataException("Each SPC row must be one line without NUL.");
   string t=line.Trim();if(t.Length==0||t.StartsWith("//",StringComparison.Ordinal)||t.StartsWith("#",StringComparison.Ordinal))return;
   if(!Regex.IsMatch(line,@"^\s*[A-Za-z_][A-Za-z_0-9]*\s*\(.*\).*$"))throw new InvalidDataException("Expected an SPC statement or comment.");
  }
  // Constants avoid depending on another partial file's static field order.
  const BindingFlags ToolRowFlags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static readonly FieldInfo toolRows=typeof(SpcDocument).GetField("lines",ToolRowFlags);
  static readonly FieldInfo toolText=typeof(SpcDocument).GetNestedType("Line",ToolRowFlags).GetField("Text",ToolRowFlags);
  static int ToolRowId(object row){return (int)row.GetType().GetField("Id",F).GetValue(row);}
  static void AppendSpcRows(SpcDocument d,string[] lines,string scope){
   int insert=-1;var rows=(IList)toolRows.GetValue(d);
   if(scope!=null){var model=TimingGroups.Model(d);if(scope!=""&&model.Find(scope)==null)throw new InvalidDataException("Target timinggroup does not exist: "+scope);
    if(lines.Any(l=>TimingGroups.Start.IsMatch(l)||Regex.IsMatch(l,@"^\s*groupend\s*\(")))throw new InvalidDataException("Use a null scope to append whole timinggroup blocks.");
    insert=scope==""?model.Groups.Select(g=>d.SourceIndex(g.Start)).DefaultIfEmpty(rows.Count).Min():d.SourceIndex(model.Find(scope).End);
   }
   var added=new List<object>();foreach(string line in lines){d.Append(line);added.Add(rows[rows.Count-1]);}
   if(insert>=0){foreach(var row in added)rows.Remove(row);for(int i=0;i<added.Count;i++)rows.Insert(insert+i,added[i]);}
   TimingGroups.Invalidate(d);
  }
  internal static int InsertSpcOutput(SpcDocument d,string[] lines,string target,bool create,ToolRequest request){
   var patch=ReadSpcOutput(lines);var before=d.Clone();var touched=new HashSet<int>();var requested=(request.rows??new ToolSource[0]).ToDictionary(r=>r.sourceId);int count=0;bool old=TimingGroups.ExplicitEdit;TimingGroups.ExplicitEdit=true;
   try{
    if(create){TimingGroups.Add(d,target);GroupProperties.SetProperty(d,target,"noinput",true);}
    foreach(var op in patch.operations){
     if(op.action=="append"){AppendSpcRows(d,op.lines,op.scope=="$target"?target:op.scope);count+=op.lines.Length;continue;}
     ToolSource original;if(!requested.TryGetValue(op.sourceId,out original)||!before.Contains(op.sourceId)||before.SourceLine(op.sourceId)!=original.source||op.expected!=null&&op.expected!=original.source||!touched.Add(op.sourceId)||!d.Contains(op.sourceId))throw new InvalidDataException("Patch source is missing, changed, or targeted more than once.");
     if(op.action=="delete")d.Delete(op.sourceId);else toolText.SetValue(((IList)toolRows.GetValue(d))[d.SourceIndex(op.sourceId)],op.text);TimingGroups.Invalidate(d);count++;
    }
    TimingGroups.Validate(d);AuthoringSafety.ValidateChanged(before,d);
    foreach(var e in TimingGroups.Events(d)){
     if(before.Contains(e.SourceId)&&before.SourceLine(e.SourceId)==d.SourceLine(e.SourceId))continue;
     if(e.Kind==EventKind.Unknown||e.Kind==EventKind.Beam)continue;string reason=ChartMath.InvalidReason(e);
     if(reason!=null)throw new InvalidDataException(reason);if(e.Kind==EventKind.SkyArea&&e.DurationMs<=0)throw new InvalidDataException("Generated SkyAreas must have positive duration; zero-ms SkyAreas are disabled.");
     if((e.Kind==EventKind.SkyArea||e.Kind==EventKind.Flick)&&!AirOverflow.Enabled&&(reason=AirReachBounds.Violation(e))!=null)throw new InvalidDataException(reason);
    }return count;
   }finally{TimingGroups.ExplicitEdit=old;}
  }
 }
}
