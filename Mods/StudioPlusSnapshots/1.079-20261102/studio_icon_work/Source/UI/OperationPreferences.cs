using System;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using InFalsusStudio;
using InFalsusStudio.Core;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  [Serializable] sealed class OperationState {
   public int version=1,beatSubdivision=4,airSubdivision=20,tapWidth=1,holdWidth=1,leftEase,rightEase,flickDirection=16,skyAttack=1;
   public double airStep=5,skyWidth=10,flickWidth=25,playbackRate=1,wheelUnits=3;
   public float scrollSpeed=1,musicVolume=1,keyMaster=.65f,keyAttack=.85f,keyHold=.35f;
   public bool enableGameplayMode=true;
   public bool beatGrid=true,hidePlayed=true,measureLines=true,airShadows=true,centerCombo=true,uniformScroll,waveform,autoplay=true,keyEnabled=true,holdAttack=true,newPasteGroups=true;
  }
  double flickWidthPercent=25;
  ConfigEntry<bool> gameplayEnabled;
  ConfigEntry<string> operationStateConfig;StudioBootstrap operationStateBoot;bool operationSaveFailed;
  void InitOperationPreferences(){
   InFalsusStudio.Gameplay.GameplayOptions.Bind(Config);
   gameplayEnabled=Config.Bind("Operations","enableGameplayMode",true,"Enable built-in gameplay from the configured shortcut; Esc returns to editing.");
   harmony.Patch(typeof(StudioBootstrap).GetProperty("PlacementWidthNorm",F).GetGetMethod(true),prefix:new HarmonyMethod(typeof(OperationHooks).GetMethod("FlickWidth",F)));
   operationStateConfig=Config.Bind("Operations","SavedOperationSettings","","Editor operation preferences committed on successful Save / Ctrl+S. Includes subdivision, placement defaults, scroll/playback speed, audio and display flags. No chart data. Existing SPC session sidecars retain their explicit per-chart overrides.");
   harmony.Patch(Method("SavePresentationPreferences"),postfix:new HarmonyMethod(typeof(OperationHooks).GetMethod("Saved",F)));
   harmony.Patch(Method("SaveWorkingCopy"),prefix:new HarmonyMethod(typeof(OperationHooks).GetMethod("SaveEmpty",F)));
   harmony.Patch(Method("FinishOpeningFolder"),prefix:new HarmonyMethod(typeof(OperationHooks).GetMethod("BeforeOpen",F)),postfix:new HarmonyMethod(typeof(OperationHooks).GetMethod("AfterOpen",F)));
  }
  static bool InRange(double value,double low,double high){return !double.IsNaN(value)&&!double.IsInfinity(value)&&value>=low&&value<=high;}
  static bool ValidOperations(OperationState s){
   return s!=null&&s.version==1&&s.beatSubdivision>=1&&s.beatSubdivision<=1024&&s.airSubdivision>=0&&s.airSubdivision<=4096&&InRange(s.airStep,s.airSubdivision>0?.0001:.1,100)&&s.tapWidth>=1&&s.tapWidth<=4&&s.holdWidth>=1&&s.holdWidth<=4&&InRange(s.skyWidth,0,100)&&s.skyWidth>0&&InRange(s.flickWidth,0,100)&&s.flickWidth>0&&s.leftEase>=0&&s.leftEase<=2&&s.rightEase>=0&&s.rightEase<=2&&(s.flickDirection==4||s.flickDirection==16)&&InRange(s.scrollSpeed,.1,10)&&InRange(s.playbackRate,.1,4)&&InRange(s.wheelUnits,.1,10)&&InRange(s.musicVolume,0,1)&&InRange(s.keyMaster,0,1)&&InRange(s.keyAttack,0,1)&&InRange(s.keyHold,0,1)&&s.skyAttack>=0&&s.skyAttack<=2;
  }
  OperationState CaptureOperations(StudioBootstrap b){
   var grid=(AirAuthoringGrid)Get(b,"airGrid");var tr=(StudioTransport)Get(b,"transport");var sound=(StudioKeySounds)Get(b,"keySounds");var p=(StudioProfile)Get(b,"Profile");var wheel=(WheelStepAccumulator)Get(b,"browseWheel");
   return new OperationState{enableGameplayMode=EnableGameplayMode,beatSubdivision=(int)Prop(b,"Division"),airSubdivision=grid.Division,airStep=(double)grid.StepPercent,tapWidth=Val<int>(b,"tapWidth"),holdWidth=Val<int>(b,"holdWidth"),skyWidth=Val<double>(b,"skyWidthPercent"),flickWidth=flickWidthPercent,leftEase=Val<int>(b,"skyLeftEase"),rightEase=Val<int>(b,"skyRightEase"),flickDirection=Val<int>(b,"lastFlickDirection"),scrollSpeed=p.scrollMultiplier,playbackRate=tr.Rate,wheelUnits=wheel.UnitsPerStep,musicVolume=tr.Volume,keyMaster=sound.MasterVolume,keyAttack=sound.ShotVolume,keyHold=sound.HoldVolume,keyEnabled=sound.Enabled,skyAttack=Convert.ToInt32(Get(b,"skyAttackPolicy")),holdAttack=Val<bool>(b,"groundHoldAttacks"),beatGrid=Val<bool>(b,"showGrid"),hidePlayed=Val<bool>(b,"hidePast"),measureLines=Val<bool>(b,"showMeasureLines"),airShadows=Val<bool>(b,"showAirShadows"),centerCombo=Val<bool>(b,"showCenterCombo"),uniformScroll=Val<bool>(b,"linearTimeLayout"),waveform=Val<bool>(b,"showDetailTimeline"),autoplay=Val<bool>(b,"autoCursor"),newPasteGroups=Val<bool>(b,"newPasteGroups")};
  }
  void ApplyOperations(StudioBootstrap b,OperationState s){
   // Set the final grid flag first, avoiding a temporary snap at startup.
   Set(b,"showGrid",s.beatGrid);Call(b,"SetSubdivision",s.beatSubdivision);
   var grid=(AirAuthoringGrid)Get(b,"airGrid");if(s.airSubdivision>0)grid.SetDivision(s.airSubdivision);else grid.SetStep((decimal)s.airStep);
   flickWidthPercent=s.flickWidth;Set(b,"tapWidth",s.tapWidth);Set(b,"holdWidth",s.holdWidth);Set(b,"skyWidthPercent",s.skyWidth);Set(b,"skyLeftEase",s.leftEase);Set(b,"skyRightEase",s.rightEase);Set(b,"lastFlickDirection",s.flickDirection);
   ((StudioProfile)Get(b,"Profile")).scrollMultiplier=s.scrollSpeed;
   var tr=(StudioTransport)Get(b,"transport");tr.Volume=s.musicVolume;tr.SetRate(s.playbackRate);((WheelStepAccumulator)Get(b,"browseWheel")).Configure(s.wheelUnits);
   var sound=(StudioKeySounds)Get(b,"keySounds");sound.Enabled=s.keyEnabled;sound.MasterVolume=s.keyMaster;sound.ShotVolume=s.keyAttack;sound.HoldVolume=s.keyHold;
   Set(b,"skyAttackPolicy",(SkyAttackPolicy)s.skyAttack);Set(b,"groundHoldAttacks",s.holdAttack);Call(b,"RebuildKeySoundPlan");
   Set(b,"hidePast",s.hidePlayed);Set(b,"showMeasureLines",s.measureLines);Set(b,"showAirShadows",s.airShadows);Set(b,"showCenterCombo",s.centerCombo);Set(b,"linearTimeLayout",s.uniformScroll);Set(b,"showDetailTimeline",s.waveform);Set(b,"autoCursor",s.autoplay);Set(b,"newPasteGroups",s.newPasteGroups);
   Set(b,"divisionInput",s.beatSubdivision.ToString(CultureInfo.InvariantCulture));Set(b,"xStepText",grid.Division.ToString(CultureInfo.InvariantCulture));Set(b,"skyWidthText",s.skyWidth.ToString("0.##########",CultureInfo.InvariantCulture));
  }
  void RestoreOperationPreferences(){
   if(operationStateConfig==null||operationStateBoot==boot||!Val<bool>(boot,"initialized"))return;
   operationStateBoot=boot;string json=operationStateConfig.Value;if(string.IsNullOrEmpty(json)){memoryHistory=boot.History;memorySnapshot=JsonUtility.ToJson(CaptureOperations(boot));return;}
   try{
    if(json.Length>16384||!json.TrimStart().StartsWith("{",StringComparison.Ordinal))throw new InvalidDataException("Invalid operation preferences JSON");
    var state=new OperationState{version=0};JsonUtility.FromJsonOverwrite(json,state);
    if(!ValidOperations(state))throw new InvalidDataException("Unsupported or out-of-range operation preferences");
    ApplyOperations(boot,state);memoryHistory=boot.History;memorySnapshot=JsonUtility.ToJson(CaptureOperations(boot));
   }catch(Exception e){Logger.LogWarning("Operation preferences ignored; editor defaults retained: "+e.Message);}
  }
  static string PutOperationSetting(string text,string serialized){
   string newline=text.Contains("\r\n")?"\r\n":"\n";
   var section=Regex.Match(text,@"(?ms)^\[Operations\][^\r\n]*(?:\r?\n|\z).*?(?=^\[|\z)");
   string line="SavedOperationSettings = "+serialized;
   if(!section.Success)return text+(text.Length>0&&!text.EndsWith("\n",StringComparison.Ordinal)?newline:"")+newline+"[Operations]"+newline+line+newline;
   string body=section.Value;var key=Regex.Match(body,@"(?m)^[ \t]*SavedOperationSettings[ \t]*=[^\r\n]*");
   body=key.Success?body.Substring(0,key.Index)+line+body.Substring(key.Index+key.Length):body+(body.EndsWith("\n",StringComparison.Ordinal)?"":newline)+line+newline;
   return text.Substring(0,section.Index)+body+text.Substring(section.Index+section.Length);
  }
  bool SaveOperationPreferences(StudioBootstrap b){
   if(InFalsusStudio.Gameplay.GameplaySession.Active)return true;
   string previous=operationStateConfig.Value;
   try{var state=CaptureOperations(b);if(!ValidOperations(state))throw new InvalidDataException("Operation settings contain an unsupported value");operationStateConfig.Value=JsonUtility.ToJson(state);SharedPreferences.Store(operationStateConfig);operationSaveFailed=false;return true;}
   catch(Exception e){operationStateConfig.Value=previous;operationSaveFailed=true;Logger.LogWarning("Operation settings retained: "+e.Message);return false;}
  }
  EditHistory memoryHistory;string memorySnapshot;float nextMemorySample;
  void TickPreferenceMemory(){
   if(InFalsusStudio.Gameplay.GameplaySession.Active)return;
   if(boot==null||!Val<bool>(boot,"initialized")||operationStateBoot!=boot)return;
   if(Time.realtimeSinceStartup<nextMemorySample)return;nextMemorySample=Time.realtimeSinceStartup+.5f;
   SharedPreferences.TickCamera(pointerUiCaptured);
   if(pointerUiCaptured||boot.History!=null&&boot.History.InTransaction)return;
   string snapshot=JsonUtility.ToJson(CaptureOperations(boot));
   // Project/session restoration establishes a baseline, not new global defaults.
   if(memorySnapshot==null||!ReferenceEquals(memoryHistory,boot.History)){memoryHistory=boot.History;memorySnapshot=snapshot;return;}
   if(snapshot!=memorySnapshot&&SaveOperationPreferences(boot))memorySnapshot=snapshot;
   if(Val<bool>(boot,"presentationDirty"))Call(boot,"SavePresentationPreferences");
  }
  internal void FlushPreferenceMemory(){
   if(InFalsusStudio.Gameplay.GameplaySession.Active)return;
   if(boot==null||operationStateBoot!=boot||!Val<bool>(boot,"initialized"))return;
   if(ReferenceEquals(memoryHistory,boot.History)&&!(boot.History!=null&&boot.History.InTransaction))SaveOperationPreferences(boot);
   if(Val<bool>(boot,"presentationDirty"))Call(boot,"SavePresentationPreferences" );
  }
  static class OperationHooks {
   static bool FlickWidth(StudioBootstrap __instance,ref double __result){if(I==null||!I.ready)return true;string tool=Get(__instance,"tool").ToString();if(tool!="FlickLeft"&&tool!="FlickRight")return true;__result=I.flickWidthPercent/100;return false;}
   static void BeforeOpen(StudioBootstrap __instance,out bool __state){__state=Val<bool>(__instance,"autoCursor");}
   static void AfterOpen(StudioBootstrap __instance,bool __result,bool __state){if(__result&&I!=null&&I.ready)Set(__instance,"autoCursor",__state);}
   static void Saved(StudioBootstrap __instance){if(I!=null&&I.ready)I.SaveOperationPreferences(__instance);}
   static bool SaveEmpty(StudioBootstrap __instance,bool __0){
    if(I==null||!I.ready||__instance.HasDocument||__0)return true;
    if(!(bool)Call(__instance,"CommitAllInputFieldsNow"))return false;
    Call(__instance,"SavePresentationPreferences");
    if(!I.operationSaveFailed)Set(__instance,"status",I.T("操作设置已保存。"));return false;
   }
  }
 }
}
