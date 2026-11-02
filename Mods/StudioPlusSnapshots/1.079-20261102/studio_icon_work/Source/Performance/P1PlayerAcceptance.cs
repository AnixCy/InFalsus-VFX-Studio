using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using InFalsusStudio.NativeEditorUI;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class P1PlayerAcceptance {
  static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,TimingGroups.F).Invoke(target,args);}
  static void Set(object target,string name,object value){target.GetType().GetField(name,TimingGroups.F).SetValue(target,value);}
  static void Require(bool value,string text,List<string> results){if(!value)throw new Exception(text);results.Add("PASS "+text);}
  static string Hits(NoteRenderer notes){var text=new System.Text.StringBuilder();foreach(var hit in (IEnumerable)GroupRuntime.Get(notes,"hits")){var type=hit.GetType();text.Append(type.GetField("Id",TimingGroups.F).GetValue(hit)).Append(':');foreach(string name in new[]{"A","B","C"})text.Append(((Vector3)type.GetField(name,TimingGroups.F).GetValue(hit)).ToString("F5")).Append('|');}return text.ToString();}
  internal static IEnumerator Run(StudioBootstrap boot,string root){
   var results=new List<string>();string error=null;bool done=false;var ui=NativeEditorUIPlugin.I;var transport=(StudioTransport)GroupRuntime.Get(boot,"transport");
   ui.Queue(delegate{try{
    var doc=SpcDocument.Parse("chart(120,4)\ntrack(0,1)\ntap(5000,1,1)\n");Call(boot,"LoadDocument",doc);ui.panel="时序";Set(boot,"showInspector",true);GroupRuntime.Activate(boot,"");Call(ui,"CaptureTimingEditor");var history=boot.History;
    Call(ui,"SelectStatement",history,"",1);Require((int)GroupRuntime.Get(boot,"selectedId")==1,"first event-row click selects the whole track row",results);
    Call(ui,"SelectStatement",history,"",1);Require((int)GroupRuntime.Get(boot,"selectedId")==-1,"second event-row click clears the whole track row",results);
    Call(ui,"SelectStatement",history,"",1);Call(ui,"CaptureTimingEditor");
    var draftType=typeof(StudioBootstrap).GetNestedType("FieldDraft",TimingGroups.F);var draft=Activator.CreateInstance(draftType,true);Set(draft,"Text","0.94");Set(draft,"Live","1");Set(draft,"Dirty",true);
    Set(draft,"Commit",new Action<string>(value=>Call(ui,"StatementEdit",history,"",new Action<SpcDocument>(next=>TimingStatements.SetCell(next,"",1,EventKind.Track,1,value)))));
    ((IDictionary)GroupRuntime.Get(boot,"fieldDrafts"))["field_timing_p1_acceptance"]=draft;
    bool handled=(bool)Call(boot,"HandleCommittedInput",new Event{type=EventType.KeyDown,keyCode=KeyCode.Return});Require(handled,"Enter goes through the actual queued timing-input hook",results);
   }catch(Exception ex){error=ex.ToString();}finally{done=true;}});
   while(!done)yield return null;yield return null;yield return null;
   if(error==null){try{
    Require(boot.History.Document.ReadEvents().Single(e=>e.Kind==EventKind.Track).Get(1)==.94&&(int)GroupRuntime.Get(boot,"selectedId")==-1,"Enter commits track parameter and clears row selection in Player",results);
   }catch(Exception ex){error=ex.ToString();}}
   done=false;ui.Queue(delegate{try{
    // Space is now the independent held-preview default; no mode checkbox.
    transport.Pause();transport.Seek(6282.125);bool typed=(bool)Call(ui,"SpaceKey",new Event{type=EventType.KeyDown,keyCode=KeyCode.Space},true);Require(!typed&&!transport.Playing,"typing Space in a field does not start preview",results);
    Call(ui,"SpaceKey",new Event{type=EventType.KeyDown,keyCode=KeyCode.Space},false);Require(transport.Playing&&!ui.formal,"held Space starts preview without hiding the editor",results);transport.Seek(6782.375);
    Call(ui,"SpaceKey",new Event{type=EventType.KeyUp,keyCode=KeyCode.Space},false);Require(!transport.Playing&&transport.TimeMs==6282.125,"Space release pauses actual transport and restores the exact origin",results);
   }catch(Exception ex){error=ex.ToString();}finally{done=true;}});while(!done)yield return null;
   if(error==null){try{
    ui.panel="";Set(boot,"showInspector",false);transport.Pause();Set(boot,"showFeedback",false);
    var text=new System.Text.StringBuilder("chart(120,4)\ntrack(0,1)\ntrack(5000,99999)\ntrack(5001,-1)\ntrack(15000,0)\ntrack(18000,2)\n");for(int i=0;i<300;i++)text.Append("tap(").Append(i*100).Append(",1,").Append(i%4+1).Append(")\n");text.Append("hold(4000,2,1,24000)\nskyarea(5000,50,100,20,75,100,10,1,2,4000,7)\nflick(5000,50,100,25,16)\ntiminggroup(ghost,noinput):\nhold(0,2,1,30000)\ngroupend()\ntiminggroup(fall,noclip):\ntrack(0,-1)\nhold(500,3,1,30000)\ngroupend()\ntiminggroup(vanish):\nhidegroup(1000,1)\nhidegroup(7000,0)\nskyarea(0,30,100,20,80,100,10,0,0,30000,7)\ngroupend()\n");
    var doc=SpcDocument.Parse(text.ToString());byte[] bytes=doc.ToBytes();Call(boot,"LoadDocument",doc);bool parity=true;
    foreach(double now in new[]{0d,4999d,5000d,5000.5d,5001d,6000d,15000d,18000d,29000d}){
     transport.Seek(now);P1Switches.Rendering=false;P1Switches.Scroll=false;Call(boot,"LateUpdate");var notes=(NoteRenderer)GroupRuntime.Get(boot,"notes");string before=Hits(notes);int count=notes.RenderedNotes,invalid=notes.InvalidNotes,bounds=notes.OutOfBoundsNotes;P1Switches.Rendering=true;P1Switches.Scroll=true;Call(boot,"LateUpdate");if(before!=Hits(notes)||count!=notes.RenderedNotes||invalid!=notes.InvalidNotes||bounds!=notes.OutOfBoundsNotes)parity=false;
    }
    Require(parity,"nine actual native-render passes preserve pick triangles to 5 decimals and all renderer diagnostics",results);Require(bytes.SequenceEqual(doc.ToBytes()),"rendering and preview never rewrite the fixture source",results);
   }catch(Exception ex){error=ex.ToString();}}
   P1Switches.Rendering=true;P1Switches.Scroll=true;if(error!=null)results.Add("FAIL "+error);File.WriteAllLines(Path.Combine(root,"player-acceptance.txt"),results.ToArray());
   if(error!=null)File.WriteAllText(Path.Combine(root,"ERROR.txt"),error);
  }
 }
}
