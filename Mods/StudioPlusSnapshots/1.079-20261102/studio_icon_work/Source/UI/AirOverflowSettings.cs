using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio;
using UnityEngine;

namespace InFalsusStudio.NativeEditorUI {
 // The option changes air reach policy, never the chart, stage, cursor reach or grid.
 internal static class AirOverflow {
  internal static ConfigEntry<bool> Setting;
  internal static bool Enabled {get{return Setting!=null&&Setting.Value;}}
  internal static string RestrictedViolation(SpcEvent e){
   if(!AirReachBounds.IsAir(e)||ChartMath.InvalidReason(e)!=null)return null;
   var a=e.Kind==EventKind.Flick?ChartMath.FlickRange(e):ChartMath.SkyAt(e,e.TimeMs);
   var b=e.Kind==EventKind.Flick?a:ChartMath.SkyAt(e,e.EndMs);
   return !AirReachBounds.Inside(a)||!AirReachBounds.Inside(b)?"Air range exceeds the playable 0..100 boundary. Source retained; preview is clipped.":null;
  }
  internal static void Init(ConfigFile config,Harmony h){
   Setting=config.Bind("Authoring","AllowAirOverflow",false,"Allow SkyArea/Flick geometry and ordinary parameter edits outside normalized 0..1. No chart rewriting. Grid range is configured separately in the Grid panel.");
   Patch(h,typeof(AirReachBounds),"Violation","Violation");
   Patch(h,typeof(AirReachBounds),"Require","Require");
   Patch(h,typeof(AirReachBounds),"CenterWithin","Center");
   Patch(h,typeof(AirReachBounds),"Clamp","Unclamped");
   Patch(h,typeof(AirReachBounds),"ClampDelta","Delta");
   Patch(h,typeof(NoteProperties),"AirPercent","Percent");
   Patch(h,typeof(NoteRenderer),"SetAirClip","Clip");
   h.Patch(NativeEditorUIPlugin.Method("AirGuideLine"),prefix:new HarmonyMethod(typeof(AirOverflow).GetMethod("Guide",NativeEditorUIPlugin.F)));
   h.Patch(typeof(PointPlacement).GetMethod("ChooseX"),transpiler:new HarmonyMethod(typeof(AirOverflow).GetMethod("PlacementLimits",NativeEditorUIPlugin.F)));
   h.Patch(NativeEditorUIPlugin.Method("ClickPointPlacement"),prefix:new HarmonyMethod(typeof(AirOverflow).GetMethod("PlaceAir",NativeEditorUIPlugin.F)){priority=Priority.First});
  }
  static bool Guide(StudioBootstrap __instance,Vector3 __0,Vector3 __1,Color __2,float __3){
   if(!Enabled)return true;NativeEditorUIPlugin.Call(__instance,"GuideLine",__0,__1,__2,__3);return false;
  }
  static readonly BindingFlags F=NativeEditorUIPlugin.F;
  static void Draft(PointPlacement draft,string field,object value){typeof(PointPlacement).GetProperty(field,F).SetValue(draft,value,null);}
  internal static bool ChoosePosition(PointPlacement draft,double x){
   if(!Enabled||!AuthoringSafety.Finite(x)||!AirPlacement(draft)||!draft.ChoosingX)return false;
   if(draft.Phase==PlacementPhase.StartX){Draft(draft,"StartX",x);Draft(draft,"EndX",x);Draft(draft,"Phase",draft.Kind==EventKind.Flick?PlacementPhase.Ready:PlacementPhase.EndTime);}
   else{Draft(draft,"EndX",x);Draft(draft,"Phase",PlacementPhase.Ready);}return true;
  }
  // Own the click-to-X transition as well as its validation. Merely changing
  // ChooseX's bounds leaves the placement workflow using its bounded path.
  static bool PlaceAir(StudioBootstrap __instance,Event __0){
   var ui=NativeEditorUIPlugin.I;if(!Enabled||ui==null||!ui.ready||ui.formal)return true;
   string tool=NativeEditorUIPlugin.Get(__instance,"tool").ToString();if(tool!="Sky"&&tool!="FlickLeft"&&tool!="FlickRight")return true;
   var draft=(PointPlacement)NativeEditorUIPlugin.Get(__instance,"pointPlacement");bool accepted;
   if(draft.ChoosingX){
    if(!(bool)NativeEditorUIPlugin.Call(__instance,"AllowedCanvas",__0.mousePosition))return false;
    double time=draft.Phase==PlacementPhase.EndX?draft.EndMs:draft.StartMs;
    var p=__instance.Profile;float z=(float)NativeEditorUIPlugin.Call(__instance,"Depth",time);var camera=__instance.ViewCamera;
    var a=camera.WorldToScreenPoint(new Vector3(-p.centralHalfWidth,p.skyHeight+.019f,z));var b=camera.WorldToScreenPoint(new Vector3(p.centralHalfWidth,p.skyHeight+.019f,z));
    if(a.z<=camera.nearClipPlane||b.z<=camera.nearClipPlane||Math.Abs(b.x-a.x)<1)return false;
    double raw=(__0.mousePosition.x-a.x)/(b.x-a.x);
    double x=AirGridRange.SnapValue((AirAuthoringGrid)NativeEditorUIPlugin.Get(__instance,"airGrid"),raw);
    accepted=ChoosePosition(draft,x);
    NativeEditorUIPlugin.Set(__instance,"hoverX",x);NativeEditorUIPlugin.Set(__instance,"hoverTime",time);
   }else{
    NativeEditorUIPlugin.Call(__instance,"RefreshAuthoringHover",__0.mousePosition);
    if(!(bool)NativeEditorUIPlugin.Get(__instance,"hoverValid"))return false;
    accepted=draft.ChooseTime((double)NativeEditorUIPlugin.Get(__instance,"hoverTime"),(int)NativeEditorUIPlugin.Get(__instance,"hoverLane"),1);
   }
   if(!accepted)return false;
   if(draft.Phase!=PlacementPhase.Ready){NativeEditorUIPlugin.Set(__instance,"status",NativeEditorUIPlugin.Prop(__instance,"PlacementInstruction"));return false;}
   // Capture before entering the document transaction; draft lifecycle changes
   // during refresh must not change the parameters of the note being added.
   double start=draft.StartMs,end=draft.EndMs,sx=draft.StartX,ex=draft.EndX,width=(double)NativeEditorUIPlugin.Prop(__instance,"PlacementWidthNorm");
   EventKind kind=draft.Kind;int left=(int)NativeEditorUIPlugin.Get(__instance,"skyLeftEase"),right=(int)NativeEditorUIPlugin.Get(__instance,"skyRightEase"),id=-1;
   bool saved=(bool)NativeEditorUIPlugin.Call(__instance,"Change",new Action<SpcDocument>(d=>{
    id=EditOperations.Add(d,kind,start,end,1,1,sx,ex,width,tool=="FlickRight"?4:16,100);
    if(kind==EventKind.SkyArea){EditOperations.SetEase(d,new[]{id},false,left);EditOperations.SetEase(d,new[]{id},true,right);}
   }));
   if(saved){NativeEditorUIPlugin.Call(__instance,"SetSelection",new[]{id},id);((RadialMenuState)NativeEditorUIPlugin.Get(__instance,"radial")).InPointMode=true;NativeEditorUIPlugin.Set(__instance,"showNotes",true);NativeEditorUIPlugin.Call(__instance,"ResetPointDraft");NativeEditorUIPlugin.Set(__instance,"status","Placed "+kind+". "+NativeEditorUIPlugin.Prop(__instance,"PlacementInstruction"));}
   else Draft(draft,"Phase",kind==EventKind.SkyArea?PlacementPhase.EndX:PlacementPhase.StartX);
   return false;
  }
  static bool AirPlacement(PointPlacement draft){return Enabled&&(draft.Kind==EventKind.SkyArea||draft.Kind==EventKind.Flick);}
  static double PlacementMinimum(PointPlacement draft){return AirPlacement(draft)?double.MinValue:0;}
  static double PlacementMaximum(PointPlacement draft){return AirPlacement(draft)?double.MaxValue:1;}
  static IEnumerable<CodeInstruction> PlacementLimits(IEnumerable<CodeInstruction> input){
   int count=0;
   foreach(var code in input){
    if(code.opcode==OpCodes.Ldc_R8&&((double)code.operand==0||(double)code.operand==1)){
     var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(code.labels);code.labels.Clear();yield return load;
     code.opcode=OpCodes.Call;code.operand=typeof(AirOverflow).GetMethod((double)code.operand==0?"PlacementMinimum":"PlacementMaximum",NativeEditorUIPlugin.F);count++;
    }
    yield return code;
   }
   if(count!=2)throw new MissingMethodException("Point-placement coordinate bounds changed");
  }
  static void Patch(Harmony h,Type t,string method,string hook){
   h.Patch(t.GetMethod(method,NativeEditorUIPlugin.F),new HarmonyMethod(typeof(AirOverflow).GetMethod(hook,NativeEditorUIPlugin.F)));
  }
  static bool Violation(ref string __result){if(!Enabled)return true;__result=null;return false;}
  static bool Require(SkyRange __0){
   if(!Enabled)return true;
   if(!AuthoringSafety.Finite(__0.Left)||!AuthoringSafety.Finite(__0.Right)||__0.Left>__0.Right+AirReachBounds.Tolerance)throw new ArgumentException("Invalid air range.");
   return false;
  }
  static bool Unclamped(double __0,ref double __result){if(!Enabled)return true;if(!AuthoringSafety.Finite(__0))throw new ArgumentException("Non-finite air coordinate.");__result=__0;return false;}
  static bool Center(double __0,double __1,ref double __result){
   if(!Enabled)return true;
   if(!AuthoringSafety.Finite(__0)||!AuthoringSafety.Finite(__1)||__1<0)throw new ArgumentException("Invalid air position or width.");
   __result=__0;return false;
  }
  static bool Delta(double __1,ref double __result){
   if(!Enabled)return true;if(!AuthoringSafety.Finite(__1))throw new ArgumentException("Non-finite edit.");__result=__1;return false;
  }
  static void Clip(ref bool __0){if(Enabled)__0=false;}
  static readonly FieldInfo Initial=typeof(NoteProperties).GetField("initial",NativeEditorUIPlugin.F);
  static double Read(NoteProperties form,string key){
   double v;if(!double.TryParse(form.Values[key],NumberStyles.Float,CultureInfo.InvariantCulture,out v)||!AuthoringSafety.Finite(v))throw new ArgumentException("Invalid finite number: "+key);
   return v/100;
  }
  static bool Percent(NoteProperties __instance,SpcDocument __0,SpcEvent __1,bool __2,string __3,string __4){
   if(!Enabled)return true;
   var initial=(Dictionary<string,string>)Initial.GetValue(__instance);var values=__instance.Values;
   bool centerChanged=values[__3]!=initial[__3],widthChanged=values[__4]!=initial[__4];
   if(!centerChanged&&!widthChanged)return false;
   int c=__2?4:1;double split=__1.Get(c+1),center=centerChanged?Read(__instance,__3):__1.Get(c)/split,width=widthChanged?Read(__instance,__4):__1.Get(c+2)/split;
   if(widthChanged&&width<=0)throw new ArgumentException("Air width must be positive.");
   RationalAirCoordinates.WriteEndpoint(__0,__1,__2,center,width);return false;
  }
 }
 public sealed partial class NativeEditorUIPlugin {
  void InitAirOverflow(){AirOverflow.Init(Config,harmony);AirGridRange.Init(Config,harmony);}
  void SetAirOverflow(bool value){
   Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;Call(boot,"CancelGesture");AirOverflow.Setting.Value=value;});
  }
 }
}
