using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using InFalsusStudio.NativeEditorUI;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class EndpointDragMode {
  sealed class State {internal bool TimeOnly,DragTimeOnly;internal float NoticeUntil;}
  static readonly ConditionalWeakTable<StudioBootstrap,State> states=new ConditionalWeakTable<StudioBootstrap,State>();
  static readonly BindingFlags F=TimingGroups.F;
  delegate bool ResolveFn(StudioBootstrap boot,float depth,double preferred,out double time);
  static readonly ResolveFn resolve=(ResolveFn)Delegate.CreateDelegate(typeof(ResolveFn),typeof(StudioBootstrap).GetMethod("TimeFromDepth",F));
  internal static bool TimeOnly(StudioBootstrap boot){return states.GetValue(boot,b=>new State()).TimeOnly;}
  internal static bool Notice(StudioBootstrap boot){return Time.unscaledTime<states.GetValue(boot,b=>new State()).NoticeUntil;}
  internal static bool HandleKey(StudioBootstrap boot,Event e,bool textFocused){
   if(e.type!=EventType.KeyDown||e.keyCode!=KeyCode.T||!e.alt||e.control||e.command||e.shift||textFocused)return false;
   if(!boot.HasDocument||!Convert.ToBoolean(GroupRuntime.Get(boot,"showUi"))||boot.History.InTransaction||
      GroupRuntime.Get(boot,"gesture").ToString()!="None"||
      (bool)typeof(StudioBootstrap).GetProperty("WorkflowActive",F).GetValue(boot,null)||
      ((StudioTransport)GroupRuntime.Get(boot,"transport")).Playing)return false;
   var state=states.GetValue(boot,b=>new State());state.TimeOnly=!state.TimeOnly;state.NoticeUntil=Time.unscaledTime+2;
   e.Use();return true;
  }
  static void Begin(StudioBootstrap __instance){
   if(!__instance.History.InTransaction||GroupRuntime.Get(__instance,"dragEvent")==null)return;
   var state=states.GetValue(__instance,b=>new State());state.DragTimeOnly=state.TimeOnly;
  }
  // Called only by the existing endpoint branch. Plane projection and the
  // gesture/undo pipeline remain native; only the requested parameter family is written.
  static void Endpoint(SpcDocument doc,int id,bool tail,double time,double center,int lane,StudioBootstrap boot){
   var original=(SpcEvent)GroupRuntime.Get(boot,"dragEvent");
   if(original==null||original.SourceId!=id||(original.Kind!=EventKind.SkyArea&&original.Kind!=EventKind.Hold)){
    EditOperations.Endpoint(doc,id,tail,time,center,lane);return;
   }
   if(!states.GetValue(boot,b=>new State()).DragTimeOnly){
    if(original.Kind==EventKind.SkyArea){
     var range=ChartMath.SkyAt(original,tail?original.EndMs:original.TimeMs);
     RationalAirCoordinates.WriteEndpoint(doc,original,tail,AirReachBounds.CenterWithin(center,range.Width),Math.Abs(original.Get(tail?6:3)/original.Get(tail?5:2)));
    }else{
     int delta=(int)Math.Round((center-EditOperations.Center(original,tail))*4,MidpointRounding.AwayFromZero);
     EditOperations.Move(doc,new[]{id},0,delta,0);
    }
    return;
   }
   double head=tail?original.TimeMs:time,end=tail?time:original.EndMs;
   AuthoringSafety.Duration(head,end);
   if(end-head<.001)throw new ArgumentException("End must follow start. Choose a later beat line.");
   // Do not call the geometric endpoint writer, which may re-encode coordinate
   // ratios. Preserve every non-time token, including optional/unknown arguments.
   if(!tail)AuthoredNumber.Set(doc,id,0,head);
   AuthoredNumber.Set(doc,id,original.Kind==EventKind.Hold?3:9,end-head);
  }
  static bool MiddleBody(StudioBootstrap boot){var ui=NativeEditorUIPlugin.I;return ui!=null&&ui.MiddleDragging(boot)&&(NoteHandle)GroupRuntime.Get(boot,"dragHandle")==NoteHandle.Body;}
  static void Move(SpcDocument doc,IEnumerable<int> ids,double time,int lanes,double x,AuthoringGrid grid,int division,StudioBootstrap boot){
   if(MiddleBody(boot)){
    if(states.GetValue(boot,b=>new State()).DragTimeOnly){lanes=0;x=0;}
    else time=0;
   }
   EditOperations.PointerMove(doc,ids,time,lanes,x,grid,division);
  }
  static bool Resolve(StudioBootstrap boot,float depth,double preferred,out double time){
   var kind=(NoteHandle)GroupRuntime.Get(boot,"dragHandle");
   if((kind==NoteHandle.Head||kind==NoteHandle.Tail||MiddleBody(boot))&&!states.GetValue(boot,b=>new State()).DragTimeOnly){
    time=(double)GroupRuntime.Get(boot,"dragReferenceTime");return true;
   }
   return resolve(boot,depth,preferred,out time);
  }
  static void Guide(StudioBootstrap __instance){
   var kind=(NoteHandle)GroupRuntime.Get(__instance,"dragHandle");
   if(__instance.History.InTransaction&&(kind==NoteHandle.Head||kind==NoteHandle.Tail||MiddleBody(__instance))&&!states.GetValue(__instance,b=>new State()).DragTimeOnly)
    typeof(StudioBootstrap).GetField("guideTime",F).SetValue(__instance,GroupRuntime.Get(__instance,"dragReferenceTime"));
  }
  static IEnumerable<CodeInstruction> ResolveEndpointTime(IEnumerable<CodeInstruction> input){
   int count=0;foreach(var code in input){var call=code.operand as MethodInfo;
    if(call!=null&&call.DeclaringType==typeof(StudioBootstrap)&&call.Name=="TimeFromDepth"){
     code.opcode=OpCodes.Call;code.operand=typeof(EndpointDragMode).GetMethod("Resolve",F);count++;
    }yield return code;
   }if(count!=1)throw new MissingMethodException("Drag time-resolution call changed");
  }
  static FieldInfo[] OwnerPath(Type closure,int depth){
   if(depth>4)return null;var fields=closure.GetFields(F).Where(f=>!f.IsStatic).ToArray();
   var owner=fields.FirstOrDefault(f=>f.FieldType==typeof(StudioBootstrap));if(owner!=null)return new[]{owner};
   foreach(var field in fields.Where(f=>f.FieldType.DeclaringType==typeof(StudioBootstrap)&&f.FieldType.Name.Contains("DisplayClass"))){
    var path=OwnerPath(field.FieldType,depth+1);if(path!=null)return new[]{field}.Concat(path).ToArray();
   }return null;
  }
  static IEnumerable<CodeInstruction> ReplaceEndpoint(IEnumerable<CodeInstruction> input,MethodBase __originalMethod){
   var owner=OwnerPath(__originalMethod.DeclaringType,0);if(owner==null)throw new MissingFieldException("Endpoint closure owner changed");
   int count=0;foreach(var code in input){
    var call=code.operand as MethodInfo;
    if(call!=null&&call.DeclaringType==typeof(EditOperations)&&call.Name=="Endpoint"){
     var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(code.labels);code.labels.Clear();yield return load;foreach(var field in owner)yield return new CodeInstruction(OpCodes.Ldfld,field);
     code.opcode=OpCodes.Call;code.operand=typeof(EndpointDragMode).GetMethod("Endpoint",F);count++;
    }
    yield return code;
   }
   if(count!=1)throw new MissingMethodException("Endpoint preview call changed");
  }
  static IEnumerable<CodeInstruction> ReplaceMove(IEnumerable<CodeInstruction> input,MethodBase __originalMethod){
   var owner=OwnerPath(__originalMethod.DeclaringType,0);if(owner==null)throw new MissingFieldException("Move closure owner changed");int count=0;
   foreach(var code in input){var call=code.operand as MethodInfo;
    if(call!=null&&call.DeclaringType==typeof(EditOperations)&&call.Name=="PointerMove"){
     var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(code.labels);code.labels.Clear();yield return load;foreach(var field in owner)yield return new CodeInstruction(OpCodes.Ldfld,field);
     code.opcode=OpCodes.Call;code.operand=typeof(EndpointDragMode).GetMethod("Move",F);count++;
    }yield return code;
   }if(count!=1)throw new MissingMethodException("Body preview call changed");
  }
  internal static void Install(Harmony h){
   h.Patch(typeof(StudioBootstrap).GetMethod("BeginNoteDrag",F),postfix:new HarmonyMethod(typeof(EndpointDragMode).GetMethod("Begin",F)));
   h.Patch(typeof(StudioBootstrap).GetMethod("PreviewNoteDrag",F),postfix:new HarmonyMethod(typeof(EndpointDragMode).GetMethod("Guide",F)),transpiler:new HarmonyMethod(typeof(EndpointDragMode).GetMethod("ResolveEndpointTime",F)));
   // EditHistory.Preview's native action is a compiler-generated closure. Patch
   // the specific endpoint closure rather than replacing the gesture pipeline.
   var closures=typeof(StudioBootstrap).GetNestedTypes(F).SelectMany(t=>t.GetMethods(F))
    .Where(m=>m.Name.Contains("<PreviewNoteDrag>")&&m.GetMethodBody()!=null)
    .Where(m=>PatchProcessor.GetOriginalInstructions(m).Any(c=>c.operand is MethodInfo&&((MethodInfo)c.operand).DeclaringType==typeof(EditOperations)&&((MethodInfo)c.operand).Name=="Endpoint")).ToArray();
   if(closures.Length!=1)throw new MissingMethodException("Endpoint preview closure changed");
   h.Patch(closures[0],transpiler:new HarmonyMethod(typeof(EndpointDragMode).GetMethod("ReplaceEndpoint",F)));
   var moves=typeof(StudioBootstrap).GetNestedTypes(F).SelectMany(t=>t.GetMethods(F)).Where(m=>m.Name.Contains("<PreviewNoteDrag>")&&m.GetMethodBody()!=null)
    .Where(m=>PatchProcessor.GetOriginalInstructions(m).Any(c=>c.operand is MethodInfo&&((MethodInfo)c.operand).DeclaringType==typeof(EditOperations)&&((MethodInfo)c.operand).Name=="PointerMove")).ToArray();
   if(moves.Length!=1)throw new MissingMethodException("Body preview closure changed");
   h.Patch(moves[0],transpiler:new HarmonyMethod(typeof(EndpointDragMode).GetMethod("ReplaceMove",F)));
  }
 }
}
