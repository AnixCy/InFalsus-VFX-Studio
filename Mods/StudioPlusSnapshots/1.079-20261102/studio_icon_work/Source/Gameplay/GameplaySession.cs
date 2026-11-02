using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.NativeEditorUI;
using UnityEngine;
[assembly:InternalsVisibleTo("GameplayChecks")]
namespace InFalsusStudio.Gameplay {
 internal enum GameplayPhase { LeadIn,Playing,Settled }
 internal static class GameplaySession {
  internal static bool Active {get{return Owner!=null;}}
  internal static StudioBootstrap Owner;
  internal static NativeJudgeEngine Engine;
  internal static GameplayInputSampler InputState;
  internal static GameplayPresentationBridge Presentation;
  internal static GameplayAudioHost Audio;static GameplayInputTimeline inputTimeline;static GameplayJudgeRunner runner;static GameplaySoundController soundController;
  internal static GameplayPhase Phase;
  internal static double EntryTime,Now,LeadDeadline,EndTime;
  static EditHistory history;static int[] selection;static int primary;
  static bool wasFormal,showUi,showNotes,autoCursor,wasHidePast,wasCursorVisible=true;static string panel;
  static Harmony harmonyRef;static bool trailPatched;static FieldInfo trailInstanceField,trailSuspendedField;static MethodInfo trailClearMethod;
  internal static void Install(Harmony h){
   harmonyRef=h;
   Patch(h,typeof(StudioBootstrap),"LateUpdate","Tick");Patch(h,typeof(StudioBootstrap),"OnGUI","OnGui",Priority.First+3000);
   foreach(string method in new[]{"HandleSceneEvent","TickSession","EnsureBrowseGridLock","HandleDetailTimeline","DrawEditingOverlay","DrawWorkflowOverlay","DrawAuthoringOverlay","HandleRadialEvent","RefreshAuthoringHover","ObserveEditorModifiers"})Patch(h,typeof(StudioBootstrap),method,"AllowEditor");
   Patch(h,typeof(StudioBootstrap),"ResetEditingForDocument","DocumentChanging");Patch(h,typeof(StudioBootstrap),"OnApplicationFocus","FocusChanged");
   Patch(h,typeof(StudioBootstrap),"OnDestroy","DocumentChanging");
   Patch(h,typeof(StudioKeySounds),"Tick","AllowAutoplaySound");
   h.Patch(typeof(NoteSkinPolicy).GetMethod("Active"),prefix:H("NoteActive"));
   // Run Notes prefix at Priority.Last so PausedNoteMeshes.Draw(object[] __args) cannot overwrite ref arguments (__5=removePast, __15=ignored).
   h.Patch(typeof(NoteRenderer).GetMethod("Draw"),prefix:H("Notes",Priority.Last));
   h.Patch(typeof(NoteRenderer).GetMethod("DrawGround",NativeEditorUIPlugin.F),prefix:H("PerNoteClip",Priority.First));
   h.Patch(typeof(NoteRenderer).GetMethod("DrawSky",NativeEditorUIPlugin.F),prefix:H("PerNoteClip",Priority.First));
   h.Patch(typeof(NoteRenderer).GetMethod("DrawSky",NativeEditorUIPlugin.F),transpiler:H("SkyClipTime"));
   h.Patch(typeof(FloorComboRenderer).GetMethod("Draw"),prefix:H("Combo",Priority.First+2000));
   GameplayPresentationAdapter.Install(h);
   TryPatchCursorTrail();
  }
  static void TryPatchCursorTrail(){
   if(trailPatched||harmonyRef==null)return;
   foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()){
    var t=asm.GetType("InfStudio.CursorTrail.CursorTrail",false);if(t==null)continue;
    trailInstanceField=t.GetField("Instance",NativeEditorUIPlugin.F);
    trailSuspendedField=t.GetField("suspended",NativeEditorUIPlugin.F);
    trailClearMethod=t.GetMethod("Clear",NativeEditorUIPlugin.F);
    foreach(string mName in new[]{"Update","OnGUI","DrawNow","AfterBootstrapOnGUI"}){
     var m=t.GetMethod(mName,NativeEditorUIPlugin.F);if(m!=null)harmonyRef.Patch(m,prefix:H("AllowEditor",Priority.First+3000));
    }
    trailPatched=true;break;
   }
  }
  static void SuppressCursorTrail(bool suppress){
   TryPatchCursorTrail();
   if(trailInstanceField==null)return;
   var inst=trailInstanceField.GetValue(null);if(inst==null)return;
   if(suppress&&trailClearMethod!=null)trailClearMethod.Invoke(inst,null);
   if(trailSuspendedField!=null)trailSuspendedField.SetValue(inst,suppress);
  }
  static HarmonyMethod H(string name,int priority=Priority.First){return new HarmonyMethod(typeof(GameplaySession).GetMethod(name,NativeEditorUIPlugin.F)){priority=priority};}
  static void Patch(Harmony h,Type t,string name,string prefix,int priority=Priority.First){var m=t.GetMethod(name,NativeEditorUIPlugin.F);if(m==null)throw new MissingMethodException(t.Name,name);h.Patch(m,prefix:H(prefix,priority));}
  static StudioTransport Transport {get{return NativeEditorUIPlugin.Val<StudioTransport>(Owner,"transport");}}
  internal static bool Enter(StudioBootstrap owner){
   var ui=NativeEditorUIPlugin.I;
   if(Active||!owner||!owner.HasDocument||ui==null||!ui.ready||!ui.EnableGameplayMode)return false;
   if(!(bool)NativeEditorUIPlugin.Call(owner,"CommitAllInputFieldsNow")||!(bool)NativeEditorUIPlugin.Call(owner,"CommitPendingNoteNow"))return false;
   double entry=owner.CurrentMs;
   ui.EndPreviewForGameplay();var tr=NativeEditorUIPlugin.Val<StudioTransport>(owner,"transport");tr.Pause();tr.Seek(entry);
   NativeEditorUIPlugin.Call(owner,"CancelGesture");NativeEditorUIPlugin.Call(owner,"CancelHudField");NativeEditorUIPlugin.Call(owner,"CancelRadial");
   // Resolve automatic authoring groups on a disposable syntax copy only.
   NativeJudgeEngine engine;
   try{
    var document=owner.History.Document.Clone();
    var all=document.ReadEvents();var source=NativeEditorUIPlugin.Val<List<SpcEvent>>(owner,"events").ToDictionary(e=>e.SourceId);
    all=all.Where(e=>!e.IsNote||!InFalsusStudio.TimeGroups.TimingGroups.NoInput(source[e.SourceId])).ToList();
    engine=new NativeJudgeEngine(all,entry);
   }catch(Exception e){NativeEditorUIPlugin.Set(owner,"status",e.Message);return false;}
   history=owner.History;selection=NativeEditorUIPlugin.Val<HashSet<int>>(owner,"selection").ToArray();primary=NativeEditorUIPlugin.Val<int>(owner,"selectedId");
   wasFormal=ui.formal;panel=ui.panel;showUi=NativeEditorUIPlugin.Val<bool>(owner,"showUi");showNotes=NativeEditorUIPlugin.Val<bool>(owner,"showNotes");autoCursor=NativeEditorUIPlugin.Val<bool>(owner,"autoCursor");wasHidePast=NativeEditorUIPlugin.Val<bool>(owner,"hidePast");wasCursorVisible=Cursor.visible;
   try{
    Owner=owner;EntryTime=Now=entry;Engine=engine;InputState=new GameplayInputSampler();InputState.Prime();InputState.Sample(entry,owner.ViewCamera,owner.Profile);
    Phase=GameplayPhase.LeadIn;Presentation=new GameplayPresentationBridge(owner);
    var sounds=NativeEditorUIPlugin.Val<StudioKeySounds>(owner,"keySounds");sounds.StopAll();sounds.GetType().GetMethod("DecodeReadySamples",NativeEditorUIPlugin.F).Invoke(sounds,null);var samples=(PcmSample[])sounds.GetType().GetField("samples",NativeEditorUIPlugin.F).GetValue(sounds);
    Audio=new GameplayAudioHost(tr,samples,entry);inputTimeline=new GameplayInputTimeline(Audio.Clock,InputState,owner.ViewCamera,owner.Profile);soundController=new GameplaySoundController(Audio,Engine,InputState);GameplayPresentationAdapter.Owner=tr;LeadDeadline=Time.realtimeSinceStartupAsDouble+1;
    EndTime=Math.Max(engine.LastTime+201,NativeEditorUIPlugin.Val<double>(owner,"durationMs"));
    if(tr.Clip)EndTime=Math.Max(EndTime,tr.Clip.length*1000-tr.AudioOffsetMs);
    NativeEditorUIPlugin.Call(owner,"SetSelection",new int[0],-1);
    NativeEditorUIPlugin.Set(owner,"showNotes",true);NativeEditorUIPlugin.Set(owner,"autoCursor",false);NativeEditorUIPlugin.Set(owner,"hidePast",false);
    Cursor.visible=false;SuppressCursorTrail(true);
    ui.formal=true;ui.panel="";GameplayPresentationBridge.ResetScoreAnimation();PlaybackPresentationClock.Reset();return true;
   }catch(Exception e){if(ui.Logger!=null)ui.Logger.LogError("Gameplay Enter failed: "+e);Exit();NativeEditorUIPlugin.Set(owner,"status",e.Message);return false;}
  }
  internal static void Exit(bool restore=true){
   if(!Active)return;var owner=Owner;var ui=NativeEditorUIPlugin.I;
   try{
    GameplayPresentationAdapter.Owner=null;if(inputTimeline!=null){inputTimeline.Dispose();inputTimeline=null;}if(Audio!=null){var d=Audio.Scheduler.Diagnostics;if(ui!=null)ui.Logger.LogInfo("Gameplay FMOD: "+Audio.Backend.Output+" "+Audio.Backend.Frequency+"Hz "+Audio.Backend.BlockFrames+"x"+Audio.Backend.BufferCount+"; event->receive P95="+d.DeliveryP95().ToString("0.###")+"ms, receive->submit P95="+d.DispatchP95().ToString("0.###")+"ms; shot submit->mix P95="+d.P95().ToString("0.###")+"ms, max="+d.MaxSubmitMixMs.ToString("0.###")+"ms, mixed="+d.Mixed+", dropped="+d.Dropped+", underruns="+d.Underruns+", lateInput="+d.LateInputs);Audio.Dispose();Audio=null;}Transport.Pause();if(Presentation!=null)Presentation.Dispose();InFalsusStudio.NativeVisual.NativeTrack.GameplayKeys(null);
    NativeEditorUIPlugin.Set(owner,"showUi",showUi);NativeEditorUIPlugin.Set(owner,"showNotes",showNotes);NativeEditorUIPlugin.Set(owner,"autoCursor",autoCursor);NativeEditorUIPlugin.Set(owner,"hidePast",wasHidePast);
    Cursor.visible=wasCursorVisible;SuppressCursorTrail(false);
    if(restore&&ReferenceEquals(history,owner.History)){
     Transport.Seek(EntryTime);NativeEditorUIPlugin.Set(owner,"timeText",EntryTime.ToString("0.##########",System.Globalization.CultureInfo.InvariantCulture));
     NativeEditorUIPlugin.Call(owner,"SetSelection",selection,primary);
    }
    if(ui!=null){ui.formal=wasFormal;ui.panel=panel;ui.ClearGameplayKeys();}
    NativeEditorUIPlugin.Set(owner,"cursorPose",new CursorPose(.5,-1,"Idle"));
    NativeEditorUIPlugin.Set(owner,"status",ui!=null&&ui.ChineseGameplay?"已退出内置游玩，返回开始位置。":"Gameplay ended; returned to entry position.");
   }finally{if(InputState!=null)InputState.RestoreMouse();GameplayPresentationAdapter.Owner=null;Owner=null;Engine=null;Presentation=null;InputState=null;runner=null;soundController=null;history=null;selection=null;Cursor.visible=true;PlaybackPresentationClock.Reset();GameplayPresentationBridge.ResetScoreAnimation();}
  }
  static void Tick(StudioBootstrap __instance){
   if(!Active||Owner!=__instance)return;
   try{
    var ui=NativeEditorUIPlugin.I;
    if(ui==null||!ui.EnableGameplayMode||!ReferenceEquals(history,Owner.History)){Exit(ReferenceEquals(history,Owner.History));return;}
    Cursor.visible=false;SuppressCursorTrail(true);NativeEditorUIPlugin.Set(Owner,"hidePast",false);Audio.UpdateOptions();if(Audio.Failure!=null)throw new InvalidOperationException(Audio.Failure);if(Audio.Clock.Stale)throw new InvalidOperationException("Gameplay audio device stopped responding");
    if(Phase==GameplayPhase.LeadIn){if(Time.realtimeSinceStartupAsDouble<LeadDeadline||!Audio.Ready)return;InputState.Left.Clear();InputState.Right.Clear();Audio.Start();inputTimeline.Begin();runner=new GameplayJudgeRunner(Engine,InputState,EntryTime-Audio.Transport.Latency-1,JudgeSlice);Phase=GameplayPhase.Playing;}
    if(Phase!=GameplayPhase.Playing)return;
    runner.Frame(Audio.Transport.JudgeNow,inputTimeline,Audio.Transport);Now=runner.Time;inputTimeline.Center(Owner.ViewCamera);InFalsusStudio.NativeVisual.NativeTrack.GameplayKeys(InputState.Held);
    NativeEditorUIPlugin.Set(Owner,"cursorPose",new CursorPose(InputState.X,-1,Engine.ActiveSky!=null?"Sky center":"Manual"));
    if(Now>=EndTime){if(!Engine.Complete)runner.Finish(Math.Max(Now,Engine.LastTime+301));Now=runner.Time;Audio.Stop();Phase=GameplayPhase.Settled;InFalsusStudio.NativeVisual.NativeTrack.GameplayKeys(null);}
   }catch(Exception e){if(NativeEditorUIPlugin.I!=null)NativeEditorUIPlugin.I.Logger.LogError("Gameplay stopped: "+e);Exit();}
  }
  static void JudgeSlice(){if(soundController!=null)soundController.Tick();if(Presentation!=null)Presentation.Tick();}
  internal static bool HandleEvent(Event e){if(!Active)return false;if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){Exit();e.Use();return true;}if(e.isKey||e.isMouse||e.type==EventType.ScrollWheel){e.Use();return true;}return false;}
  static bool OnGui(StudioBootstrap __instance){if(!Active||Owner!=__instance)return true;
   HandleEvent(Event.current);
   // Even Esc's event remains isolated after Exit restores editing.
   if(!Active)return false;
   Cursor.visible=false;
   var old=GUI.skin;bool enabled=GUI.enabled;
   try{NativeEditorUIPlugin.Call(Owner,"LayoutHud");NativeEditorUIPlugin.Call(Owner,"LayoutChrome");NativeEditorUIPlugin.Call(Owner,"DrawLetterboxMask");GUI.enabled=false;NativeEditorUIPlugin.Call(Owner,"DrawKeyLabels");NativeEditorUIPlugin.Call(Owner,"DrawSongHud");NativeEditorUIPlugin.Call(Owner,"DrawSongProgress");GUI.enabled=enabled;Presentation.DrawHud();}
   finally{GUI.enabled=enabled;GUI.skin=old;}return false;
  }
  static bool NoteActive(SpcEvent __0,double __1,ref bool __result){
   if(!Active)return true;JudgeNote note;__result=false;
   if(Phase==GameplayPhase.Playing&&__1>=__0.TimeMs&&__1<__0.EndMs&&Engine.BySource.TryGetValue(__0.SourceId,out note))
    __result=__0.Kind==EventKind.Hold?Engine.Holding(note,InputState):__0.Kind==EventKind.SkyArea&&Engine.SkyActive(__0.SourceId);
   return false;
  }
  static bool AllowEditor(){return !Active;}
  static bool AllowAutoplaySound(){return !Active;}
  static void DocumentChanging(StudioBootstrap __instance){if(Active&&Owner==__instance)Exit(false);}
  static void FocusChanged(bool __0){if(!__0&&Active)Exit();}
  static void PerNoteClip(NoteRenderer __instance,SpcEvent __0){
   if(!Active||__0==null)return;
   if(InFalsusStudio.TimeGroups.TimingGroups.NoInput(__0)){InFalsusStudio.NativeVisual.NativeVisualPlugin.Set(__instance,"hidePast",!InFalsusStudio.TimeGroups.TimingGroups.NoClip(__0));return;}
   if(__0.Kind==EventKind.SkyArea){InFalsusStudio.NativeVisual.NativeVisualPlugin.Set(__instance,"hidePast",Engine.SkyFrontier(__0.SourceId,__0.TimeMs)>__0.TimeMs);return;}
   bool clip=false;
   if(__0.Kind==EventKind.Hold){
    JudgeNote jn;
    if(Engine!=null&&Engine.BySource.TryGetValue(__0.SourceId,out jn)){
     bool active=Phase==GameplayPhase.Playing&&Now>=__0.TimeMs&&Now<=__0.EndMs&&(__0.Kind==EventKind.Hold?Engine.Holding(jn,InputState):Engine.Captured&&Engine.Cooldown==0);
     if(active)jn.WasActive=true;
     if(jn.WasActive&&Now>=__0.EndMs)Engine.ConsumedNotes.Add(__0.SourceId);
     clip=active||jn.WasActive;
    }
   }
   InFalsusStudio.NativeVisual.NativeVisualPlugin.Set(__instance,"hidePast",clip);
  }
  internal static double SkyClipBegin(double start,double now,SpcEvent note){if(!Active||note==null||note.Kind!=EventKind.SkyArea||InFalsusStudio.TimeGroups.TimingGroups.NoInput(note))return Math.Max(start,now);return Math.Min(note.EndMs,Engine.SkyFrontier(note.SourceId,start));}
  static IEnumerable<CodeInstruction> SkyClipTime(IEnumerable<CodeInstruction> codes){bool replaced=false;var max=typeof(Math).GetMethod("Max",new[]{typeof(double),typeof(double)});foreach(var code in codes){if(!replaced&&Equals(code.operand,max)){yield return new CodeInstruction(OpCodes.Ldarg_1);code.opcode=OpCodes.Call;code.operand=typeof(GameplaySession).GetMethod("SkyClipBegin",NativeEditorUIPlugin.F);replaced=true;}yield return code;}if(!replaced)throw new InvalidOperationException("Sky clip start calculation changed");}
  static void Notes(NoteRenderer __instance,ref bool __5,ref bool __6,ref int __7,ref float __8,ref ISet<int> __9,ref AutoplayCursor __12,ref bool __13,ref ISet<int> __15){
   if(!Active)return;
   NativeEditorUIPlugin.Set(Owner,"cursorPose",new CursorPose(InputState.X,-1,Engine.ActiveSky!=null?"Sky center":"Manual"));__5=false;__6=false;__7=-1;__8=(float)InputState.X;__9=null;__12=null;__13=false;__15=Engine.ConsumedNotes;
  }
  static void Combo(ref int __0,ref bool __1){if(Active){__0=Engine.Combo;__1=NativeEditorUIPlugin.Val<bool>(Owner,"showCenterCombo");}}
 }
}
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  internal bool EnableGameplayMode {get{return gameplayEnabled.Value;}}
  internal bool ChineseGameplay {get{return Val<bool>(boot,"chinese");}}
  internal void SetGameplayEnabled(bool enabled){gameplayEnabled.Value=enabled;if(!enabled)InFalsusStudio.Gameplay.GameplaySession.Exit();}
  internal void EndPreviewForGameplay(){if(spacePreview.Active)StopSpacePreview();previewKeysDown.Clear();CancelFormalNavigation();}
  internal void ClearGameplayKeys(){previewKeysDown.Clear();capturingHotkey=null;}
 }
}
