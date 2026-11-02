using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using InFalsusStudio.NativeEditorUI;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 internal static class PreviewGuiSnapshots {
  internal static bool Enabled=true;
  internal static bool NativeTimingView(){var ui=NativeEditorUIPlugin.I;return Enabled&&ui!=null&&ui.ready&&!ui.legacy&&(ui.panel=="时间组"||ui.panel=="时序");}
  internal static bool Capture(){if(NativeTimingView())return false;var ui=NativeEditorUIPlugin.I;return !Enabled||ui==null||!ui.ready||ui.legacy||!PreviewFrustum.Playing;}
  static bool Forms(){var ui=NativeEditorUIPlugin.I;return Capture()||ui==null||!ui.formal;}
  static IEnumerable<CodeInstruction> Frame(IEnumerable<CodeInstruction> input,ILGenerator generator){
   var code=input.ToList();int first=code.FindIndex(c=>c.opcode==OpCodes.Stfld&&(c.operand as FieldInfo)?.Name=="guiAudioClip");
   int end=code.FindIndex(c=>(c.operand as MethodInfo)?.Name=="CaptureSongGuiFrame");
   if(first<0||end<1||end<=first)throw new InvalidOperationException("Playback GUI snapshot layout changed");
   // Commands and control-tree flags still update every Layout. Only legacy
   // inspector data unused by the native playback UI waits until pause.
   var target=generator.DefineLabel();code[end-1].labels.Add(target);
   code.InsertRange(first+1,new[]{new CodeInstruction(OpCodes.Call,typeof(PreviewGuiSnapshots).GetMethod("Capture",TimingGroups.F)),new CodeInstruction(OpCodes.Brfalse,target)});
   // Native timing panels own their revision-cached data. They never consume
   // legacy primary/near-note/timing/safety inspector arrays. Keep GUI command
   // draining, control-tree flags and song/workspace capture on the normal path.
   int primary=code.FindIndex(c=>c.opcode==OpCodes.Stfld&&(c.operand as FieldInfo)?.Name=="guiPrimaryId");
   if(primary<3||code[primary-3].opcode!=OpCodes.Ldarg_0||code[primary-2].opcode!=OpCodes.Ldarg_0||code[primary-1].opcode!=OpCodes.Ldfld||(code[primary-1].operand as FieldInfo)?.Name!="selectedId")throw new InvalidOperationException("Native timing inspector snapshot layout changed");
   var skip=new CodeInstruction(OpCodes.Call,typeof(PreviewGuiSnapshots).GetMethod("NativeTimingView",TimingGroups.F));skip.labels.AddRange(code[primary-3].labels);code[primary-3].labels.Clear();code.InsertRange(primary-3,new[]{skip,new CodeInstruction(OpCodes.Brtrue,target)});
   return code;
  }
  internal static void Install(Harmony h){
   h.Patch(typeof(StudioBootstrap).GetMethod("BeginGuiFrame",TimingGroups.F),transpiler:new HarmonyMethod(typeof(PreviewGuiSnapshots).GetMethod("Frame",TimingGroups.F)));
   foreach(string name in new[]{"CaptureBatchProperties","CaptureAuthoringToolsFrame"})h.Patch(typeof(StudioBootstrap).GetMethod(name,TimingGroups.F),new HarmonyMethod(typeof(PreviewGuiSnapshots).GetMethod("Forms",TimingGroups.F)));
  }
 }
}
