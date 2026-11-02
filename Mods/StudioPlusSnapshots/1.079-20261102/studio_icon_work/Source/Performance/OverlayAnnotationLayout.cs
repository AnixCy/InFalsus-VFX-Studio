using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.NativeEditorUI;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class OverlayAnnotationLayout {
  static readonly BindingFlags F=TimingGroups.F;
  internal delegate GUIStyle StyleFn(StudioBootstrap owner,bool box,float size,TextAnchor anchor);
  delegate Rect ViewFn(StudioBootstrap owner);
  internal delegate float ScaleFn(StudioBootstrap owner);
  delegate void BuildFn(StudioBootstrap owner);
  internal static readonly StyleFn Style=(StyleFn)Delegate.CreateDelegate(typeof(StyleFn),typeof(StudioBootstrap).GetMethod("OverlayStyle",F));
  static readonly ViewFn View=(ViewFn)Delegate.CreateDelegate(typeof(ViewFn),typeof(StudioBootstrap).GetProperty("CameraGuiRect",F).GetGetMethod(true));
  internal static readonly ScaleFn Scale=(ScaleFn)Delegate.CreateDelegate(typeof(ScaleFn),typeof(StudioBootstrap).GetProperty("OverlayScale",F).GetGetMethod(true));
  static readonly BuildFn Build=(BuildFn)Delegate.CreateDelegate(typeof(BuildFn),typeof(StudioBootstrap).GetMethod("BuildHandles",F));
  static readonly Type PointType=typeof(StudioBootstrap).GetNestedType("HandlePoint",F);
  static readonly FieldInfo PointGui=PointType.GetField("Gui",F);
  internal struct Annotation {internal string Text;internal Rect Rect;internal bool Box;}
  internal sealed class LayoutState {internal readonly List<Rect> Markers=new List<Rect>(12);internal readonly List<Annotation> Labels=new List<Annotation>(24);}
  static readonly ConditionalWeakTable<StudioBootstrap,LayoutState> states=new ConditionalWeakTable<StudioBootstrap,LayoutState>();
  internal static LayoutState State(StudioBootstrap owner){return states.GetValue(owner,b=>new LayoutState());}
  static bool Active{get{return NativeEditorUIPlugin.I!=null&&NativeEditorUIPlugin.I.ready;}}
  static void BeginGui(StudioBootstrap __instance,bool __runOriginal){if(!__runOriginal||!Active)return;CompactOverlayPopup.ClearTime(__instance);var state=State(__instance);state.Labels.Clear();state.Markers.Clear();}
  static void BeforeHandles(StudioBootstrap __instance){
   if(!Active)return;Build(__instance);var state=State(__instance);state.Markers.Clear();float r=8*Scale(__instance);
   foreach(object handle in (IList)GroupRuntime.Get(__instance,"handles")){Vector2 p=(Vector2)PointGui.GetValue(handle);state.Markers.Add(new Rect(p.x-r,p.y-r,r*2,r*2));}
  }
  internal static Rect Bounds(StudioBootstrap owner){Rect rect=View(owner);float margin=4*Scale(owner);return Rect.MinMaxRect(rect.xMin+margin,rect.yMin+margin,rect.xMax-margin,rect.yMax-margin);}
  static Rect Clamp(Rect rect,Rect bounds){rect.x=Mathf.Clamp(rect.x,bounds.xMin,Mathf.Max(bounds.xMin,bounds.xMax-rect.width));rect.y=Mathf.Clamp(rect.y,bounds.yMin,Mathf.Max(bounds.yMin,bounds.yMax-rect.height));return rect;}
  internal static Rect Measure(StudioBootstrap owner,Vector2 pos,string text,float designWidth,bool box,float size){
   float k=Scale(owner);Rect bounds=Bounds(owner);var style=Style(owner,box,size,TextAnchor.MiddleLeft);var content=new GUIContent(text??"");
   float limit=Mathf.Max(1,Mathf.Min(designWidth*k,bounds.width));float width=Mathf.Min(limit,Mathf.Ceil(style.CalcSize(content).x));width=Mathf.Max(1,width);
   float height=Mathf.Max((box?32:25)*k,Mathf.Ceil(style.CalcHeight(content,width)+2*k));return Clamp(new Rect(pos.x,pos.y,width,Mathf.Min(height,bounds.height)),bounds);
  }
  static bool Measured(StudioBootstrap __instance,Vector2 __0,string __1,float __2,bool __3,float __4,ref Rect __result){if(!Active)return true;__result=Measure(__instance,__0,__1,__2,__3,__4);return false;}
  static bool Clear(LayoutState state,Rect rect,float gap){
   Rect expanded=new Rect(rect.x-gap,rect.y-gap,rect.width+2*gap,rect.height+2*gap);
   for(int i=0;i<state.Markers.Count;i++)if(expanded.Overlaps(state.Markers[i]))return false;
   for(int i=0;i<state.Labels.Count;i++)if(expanded.Overlaps(state.Labels[i].Rect))return false;return true;
  }
  internal static Rect Place(StudioBootstrap owner,Rect preferred){
   Rect bounds=Bounds(owner);preferred=Clamp(preferred,bounds);var state=State(owner);float k=Scale(owner),gap=4*k;
   if(Clear(state,preferred,gap))return preferred;
   // Deterministic nearby rows/columns keep drawing stable without moving any
   // actual handle or changing its mouse hit target.
   float dx=Mathf.Max(28*k,preferred.width*.5f+gap),dy=preferred.height+gap*2;
   for(int ring=1;ring<=48;ring++){
    for(int direction=0;direction<8;direction++){
     float x=direction==0||direction==1?0:direction==2||direction==4||direction==6?-ring*dx:ring*dx;
     float y=direction==0||direction==4||direction==5?ring*dy:direction==1||direction==6||direction==7?-ring*dy:0;
     Rect candidate=preferred;candidate.position+=new Vector2(x,y);candidate=Clamp(candidate,bounds);if(Clear(state,candidate,gap))return candidate;
    }
   }
   // A final scan also handles labels packed against a viewport corner.
   float step=Mathf.Max(8*k,preferred.height+gap*2);
   for(float y=bounds.yMin;y+preferred.height<=bounds.yMax;y+=step)for(float x=bounds.xMin;x+preferred.width<=bounds.xMax;x+=Mathf.Max(16*k,preferred.width+gap*2)){
    var candidate=new Rect(x,y,preferred.width,preferred.height);if(Clear(state,candidate,gap))return candidate;
   }
   return preferred;
  }
  static void Paint(StudioBootstrap owner,Rect rect,string text,bool box,float size,TextAnchor anchor,Color? ink=null){
   var style=Style(owner,box,size,anchor);Color previous=style.normal.textColor;
   try{if(ink.HasValue)style.normal.textColor=ink.Value;if(box)GUI.Box(rect,text,style);else GUI.Label(rect,text,style);}finally{style.normal.textColor=previous;}
   State(owner).Labels.Add(new Annotation{Text=text,Rect=rect,Box=box});
  }
  static bool Text(StudioBootstrap __instance,Vector2 __0,string __1,float __2,bool __3,float __4,TextAnchor __5){
   if(!Active)return true;
   if(__3&&!string.IsNullOrEmpty(__1)&&(__1.StartsWith("段选：",StringComparison.Ordinal)||__1.StartsWith("Range:",StringComparison.Ordinal)||(__1.StartsWith("Right curve | ",StringComparison.Ordinal)||__1.StartsWith("Left curve | ",StringComparison.Ordinal))&&__1.EndsWith(" | release to commit",StringComparison.Ordinal)))return false;
   if(CompactOverlayPopup.DeferTime(__instance,__0,__1,__2,__3))return false;
   if(CompactOverlayPopup.TryDraw(__instance,__0,__1,__2,__3))return false;Rect rect=Measure(__instance,__0,__1,__2,__3,__4);float k=Scale(__instance);
   if(__5==TextAnchor.MiddleCenter||__5==TextAnchor.UpperCenter||__5==TextAnchor.LowerCenter)rect.x=__0.x+(__2*k-rect.width)*.5f;
   else if(__5==TextAnchor.MiddleRight||__5==TextAnchor.UpperRight||__5==TextAnchor.LowerRight)rect.x=__0.x+__2*k-rect.width;
   rect=Place(__instance,rect);Paint(__instance,rect,__1,__3,__4,__5);return false;
  }
  static void PlacementCaption(StudioBootstrap owner,Vector2 pos,string text,float width,bool box,float font,TextAnchor anchor){
   if(!Active)typeof(StudioBootstrap).GetMethod("OverlayText",F).Invoke(owner,new object[]{pos,text,width,box,font,anchor});
  }
  static IEnumerable<CodeInstruction> RemovePlacementCaption(IEnumerable<CodeInstruction> input){
   var code=input.ToList();bool placement=false;int removed=0;
   foreach(var instruction in code){var method=instruction.operand as MethodInfo;
    if(method!=null&&method.DeclaringType==typeof(StudioBootstrap)&&method.Name=="get_PlacementInstruction")placement=true;
    if(placement&&method!=null&&method.DeclaringType==typeof(StudioBootstrap)&&method.Name=="OverlayText"){
     instruction.opcode=OpCodes.Call;instruction.operand=typeof(OverlayAnnotationLayout).GetMethod("PlacementCaption",F);placement=false;removed++;
    }yield return instruction;
   }
   if(removed!=1)throw new MissingMethodException("Placement caption drawing anchor changed");
  }
  internal static void Install(Harmony harmony){
   harmony.Patch(typeof(StudioBootstrap).GetMethod("OnGUI",F),prefix:new HarmonyMethod(typeof(OverlayAnnotationLayout).GetMethod("BeginGui",F)){priority=Priority.First});
   harmony.Patch(typeof(StudioBootstrap).GetMethod("DrawEditingOverlay",F),prefix:new HarmonyMethod(typeof(OverlayAnnotationLayout).GetMethod("BeforeHandles",F)));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("OverlayMeasuredRect",F),prefix:new HarmonyMethod(typeof(OverlayAnnotationLayout).GetMethod("Measured",F)));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("OverlayText",F),prefix:new HarmonyMethod(typeof(OverlayAnnotationLayout).GetMethod("Text",F)));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("DrawAuthoringOverlay",F),transpiler:new HarmonyMethod(typeof(OverlayAnnotationLayout).GetMethod("RemovePlacementCaption",F)));
  }
 }
}
