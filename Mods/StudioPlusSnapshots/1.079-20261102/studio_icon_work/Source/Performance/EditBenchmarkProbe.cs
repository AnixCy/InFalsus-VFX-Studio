using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using InFalsusStudio.Core;
using InFalsusStudio.NativeEditorUI;
using InFalsusStudio.TimeGroups;
using UnityEngine;

namespace InFalsusStudio.Performance {
 internal static class EditBenchmarkProbe {
  internal static bool Requested {get{return Environment.GetCommandLineArgs().Contains("-visualui-edit-probe");}}
  internal static bool WorkflowRequested {get{return Environment.GetCommandLineArgs().Contains("-visualui-workflow-probe");}}
  static string Arg(string name){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:"";}
  static string Source;
  static int[] Chosen,MirrorIds;
  static void QuickTimingTest(StringBuilder output){
   var original=SpcDocument.Parse(Source);
   var model=TimingGroups.Model(original);
   var timing=TimingGroups.Events(original).Where(e=>e.Kind==EventKind.Bpm||e.Kind==EventKind.Track).ToArray();
   if(timing.Length==0)throw new InvalidOperationException("No timing in FBD chart.");
   string scope=model.Of(timing[0].SourceId);
   var selected=timing.Where(e=>model.Of(e.SourceId)==scope).Take(8).ToArray();
   var rows=selected.Select(e=>new NativeEditorUIPlugin.ToolTimingRow{id=e.SourceId,source=original.SourceLine(e.SourceId),editable=original.EditableSourceLine(e.SourceId),time=e.TimeMs}).ToArray();
   double start=rows.Min(r=>r.time),end=rows.Max(r=>r.time),target=600000;
   var edit=new NativeEditorUIPlugin.ToolTimingData{action="copy",scope=scope,start=start,end=end,target=target,rows=rows};
   var request=new NativeEditorUIPlugin.ToolRequest{timingEdit=edit};
   string[] copied=rows.Select(r=>NativeEditorUIPlugin.CopyTimingLine(r,edit)).ToArray();
   var insert=typeof(NativeEditorUIPlugin).GetMethod("InsertTimingEdit",BindingFlags.Static|BindingFlags.NonPublic);
   var history=new EditHistory(original);
   history.Execute(d=>insert.Invoke(null,new object[]{d,copied,request}));
   if(history.UndoCount!=1)throw new InvalidOperationException("Quick Copy did not create one undo step.");
   var copiedDoc=history.Document;
   var copiedModel=TimingGroups.Model(copiedDoc);
   var newIds=Enumerable.Range(original.NextId,rows.Length).ToArray();
   for(int i=0;i<rows.Length;i++){
    int id=newIds[i];
    if(!copiedDoc.Contains(id)||copiedModel.Of(id)!=scope||copiedDoc.SourceLine(id)!=copied[i])
     throw new InvalidOperationException("Quick Copy changed event spacing, payload or timinggroup.");
   }
   if(!history.Undo()||history.Document.ToText()!=original.ToText())throw new InvalidOperationException("Quick Copy undo mismatch.");
   if(!history.Redo()||history.Document.ToText()!=copiedDoc.ToText())throw new InvalidOperationException("Quick Copy redo mismatch.");
   var deleted=new NativeEditorUIPlugin.ToolTimingData{action="delete",scope=scope,start=start,end=end,target=start,rows=rows};
   var deleteRequest=new NativeEditorUIPlugin.ToolRequest{timingEdit=deleted};
   var delHistory=new EditHistory(original);
   delHistory.Execute(d=>insert.Invoke(null,new object[]{d,new string[0],deleteRequest}));
   if(rows.Any(r=>delHistory.Document.Contains(r.id))||!delHistory.Undo()||delHistory.Document.ToText()!=original.ToText())
    throw new InvalidOperationException("Quick Delete or its undo mismatch.");
   output.Append("quick_timing_copy_delete_undo=PASS\n");
  }
  static string Execute(string test,bool optimized,out double elapsed){
   BatchEditing.Enabled=optimized;
   var doc=SpcDocument.Parse(Source);
   var clock=Stopwatch.StartNew();
   if(test=="paste"){
    var clip=new NoteClipboard();clip.Copy(doc,Chosen);clock.Restart();
    clip.Paste(doc,500000,true);
   }else if(test=="mirror"){
    clock.Restart();EditOperations.Mirror(doc,MirrorIds);
   }else if(test=="append"){
    clock.Restart();for(int i=0;i<600;i++)doc.Append("track("+(800000+i*18)+",1)");
   }else throw new ArgumentException(test);
   clock.Stop();elapsed=clock.Elapsed.TotalMilliseconds;
   return doc.ToText();
  }
  internal static IEnumerator Run(){
   string dir=Arg("-visualui-edit-probe"),chart=Arg("-visualui-edit-chart");
   try{
    AudioListener.volume=0f;
    Directory.CreateDirectory(dir);
    File.WriteAllText(Path.Combine(dir,"muted.txt"),"AudioListener.volume="+AudioListener.volume+"; no playback started.");
    Source=File.ReadAllText(chart);
    var parsed=SpcDocument.Parse(Source);var notes=parsed.ReadEvents().Where(e=>e.IsNote).ToArray();
    Chosen=notes.Take(192).Select(e=>e.SourceId).ToArray();
    MirrorIds=notes.Where(e=>e.Kind==EventKind.Tap||e.Kind==EventKind.Hold||e.Kind==EventKind.Flick).Take(192).Select(e=>e.SourceId).ToArray();
    if(Chosen.Length!=192||MirrorIds.Length!=192)throw new InvalidOperationException("FBD benchmark note selection too small.");
    var output=new StringBuilder("chart="+chart+"\nrows="+parsed.LineCount+"\nselected=192\nmuted="+(AudioListener.volume==0)+"\n");
    foreach(string test in new[]{"paste","mirror","append"}){
     double[] baseline=new double[3],improved=new double[3];
     for(int i=0;i<3;i++){
      string before=Execute(test,false,out baseline[i]);
      string after=Execute(test,true,out improved[i]);
      if(before!=after)throw new InvalidOperationException(test+" changed SPC text between native and optimized paths.");
     }
     Array.Sort(baseline);Array.Sort(improved);
     output.Append(test).Append("_native_ms=").Append(baseline[1].ToString("0.###")).Append('\n');
     output.Append(test).Append("_optimized_ms=").Append(improved[1].ToString("0.###")).Append('\n');
    }
    QuickTimingTest(output);
    BatchEditing.Enabled=true;
    File.WriteAllText(Path.Combine(dir,"results.txt"),output.ToString());
    File.WriteAllText(Path.Combine(dir,"DONE.txt"),"Native and optimized edits produced identical SPC text on FBD copy. Audio muted; source file never written.");
    Application.Quit(0);
   }catch(Exception e){BatchEditing.Enabled=true;File.WriteAllText(Path.Combine(dir,"ERROR.txt"),e.ToString());Application.Quit(3);}
   yield break;
  }
  internal static IEnumerator RunWorkflow(){
   string dir=Arg("-visualui-workflow-probe"),chart=Arg("-visualui-edit-chart");
   AudioListener.volume=0f;Directory.CreateDirectory(dir);
   File.WriteAllText(Path.Combine(dir,"muted.txt"),"AudioListener.volume="+AudioListener.volume+"; no playback started.");
   yield return null;yield return null;yield return null;
   var ui=NativeEditorUIPlugin.I;Exception failure=null;
   if(ui==null)failure=new InvalidOperationException("Editor UI missing.");
   var output=new StringBuilder();
   if(failure==null){
    var inner=ui.ProbeQuickWorkflow(SpcDocument.Parse(File.ReadAllText(chart)),output);
    while(true){
     bool next=false;object current=null;
     try{next=inner.MoveNext();if(next)current=inner.Current;}catch(Exception e){failure=e;}
     if(failure!=null||!next)break;
     yield return current;
    }
   }
   if(failure==null){File.WriteAllText(Path.Combine(dir,"results.txt"),output.ToString());File.WriteAllText(Path.Combine(dir,"DONE.txt"),"Quick timing workflow applied in isolated muted Player and was undone.");Application.Quit(0);}
   else{File.WriteAllText(Path.Combine(dir,"ERROR.txt"),failure.ToString());Application.Quit(3);}
  }
 }
}
