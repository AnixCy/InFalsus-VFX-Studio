using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;

namespace InFalsusStudio.Performance {
 // Keep SPC's original undo, validation and group hooks. Only avoid full-chart
 // searches for each newly appended note or each selected mirror source.
 internal static class BatchEditing {
  internal static bool Enabled=true;
  static readonly BindingFlags F=TimingGroups.F;
  static readonly FieldInfo clipboardLines=typeof(NoteClipboard).GetField("lines",F);
  static void Append(SpcDocument __instance,int __result){if(Enabled)DocumentAcceleration.Appended(__instance,__result,__instance.LineCount-1);}
  static void Delete(SpcDocument __instance,int __0){DocumentAcceleration.Invalidated(__instance,__0);}
  static bool Paste(NoteClipboard __instance,SpcDocument __0,double __1,bool __2,ref int[] __result){
   var snapshot=ClipboardGroups.Of(__instance);if(!Enabled&&snapshot==null)return true;
   var lines=(List<string>)clipboardLines.GetValue(__instance);
   if(lines.Count==0){__result=new int[0];return false;}
   if(double.IsNaN(__1)||double.IsInfinity(__1)||__1<0)throw new ArgumentException("Paste time must be nonnegative.");
   double delta=__1-__instance.StartMs;var result=new int[lines.Count];
   string target=TimingGroups.AppendGroup;bool explicitEdit=TimingGroups.ExplicitEdit;
   TimingGroups.ExplicitEdit=true;
   try{ClipboardGroups.Prepare(__0,snapshot);for(int i=0;i<lines.Count;i++){
    int id=__0.Append(lines[i]);
    var note=DocumentAcceleration.EventAt(__0,id);
    if(note==null)throw new InvalidOperationException("Pasted note could not be parsed.");
    AuthoredNumber.Set(__0,id,0,note.TimeMs+delta);
    result[i]=id;
   }}finally{TimingGroups.ExplicitEdit=explicitEdit;}
   // Route the complete paste once, after all IDs exist. Explicit group-aware
   // callers retain their own placement policy, including an explicitly empty group.
   if(snapshot==null&&!explicitEdit&&target!=null){var model=TimingGroups.Model(__0);if(target!=""||model.Extended)TimingGroups.Move(__0,result,target);}
   ClipboardGroups.Placed(__0,result,snapshot);
   __result=result;return false;
  }
  static bool Mirror(SpcDocument __0,IEnumerable<int> __1){
   if(!Enabled)return true;
   var ids=new HashSet<int>(__1);
   foreach(int id in ids.Where(__0.Contains).OrderBy(__0.SourceIndex)){
    var e=DocumentAcceleration.EventAt(__0,id);
    if(e==null||!e.IsNote)continue;
    if(ChartMath.InvalidReason(e)!=null)throw new ArgumentException("Invalid event cannot be mirrored.");
    if(e.Kind==EventKind.Tap||e.Kind==EventKind.Hold)
     AuthoredNumber.Set(__0,id,e.Kind==EventKind.Tap?2:1,e.Lane==0?5:e.Lane==5?0:6-e.Lane-e.GroundWidth);
    else if(e.Kind==EventKind.Flick){
     AuthoredNumber.Set(__0,id,1,e.Get(2)-e.Get(1));
     AuthoredNumber.Set(__0,id,4,e.Get(4)==4?16:4);
    }else{
     AuthoredNumber.Set(__0,id,1,e.Get(2)-e.Get(1));
     AuthoredNumber.Set(__0,id,4,e.Get(5)-e.Get(4));
     AuthoredNumber.Set(__0,id,7,e.Get(8));
     AuthoredNumber.Set(__0,id,8,e.Get(7));
    }
   }
   return false;
  }
  internal static void Install(Harmony h){
   ClipboardGroups.Install(h);
   h.Patch(typeof(SpcDocument).GetMethod("Append"),null,new HarmonyMethod(typeof(BatchEditing).GetMethod("Append",F)));
   h.Patch(typeof(SpcDocument).GetMethod("Delete"),null,new HarmonyMethod(typeof(BatchEditing).GetMethod("Delete",F)));
   h.Patch(typeof(NoteClipboard).GetMethod("Paste"),new HarmonyMethod(typeof(BatchEditing).GetMethod("Paste",F)));
   h.Patch(typeof(EditOperations).GetMethod("Mirror"),new HarmonyMethod(typeof(BatchEditing).GetMethod("Mirror",F)));
  }
 }
}
