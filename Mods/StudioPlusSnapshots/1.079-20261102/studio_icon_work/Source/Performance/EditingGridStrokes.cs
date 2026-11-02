using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class EditingGridStrokes {
  internal static float Pixels(float original){return Mathf.Min(original,2.5f)*1.4f;}
  internal static Color Stronger(Color color){
   float h,s,v;Color.RGBToHSV(color,out h,out s,out v);var result=Color.HSVToRGB(h,Mathf.Clamp01(s*1.22f),Mathf.Clamp01(v*1.14f));result.a=Mathf.Clamp01(color.a*1.18f);return result;
  }
  static void BeatStrip(MeshBatch batch,Camera camera,Vector3 a,Vector3 b,float pixels,Color color){batch.ScreenStrip(camera,a,b,Pixels(pixels),Stronger(color));}
  static IEnumerable<CodeInstruction> Thicken(IEnumerable<CodeInstruction> input){
   int count=0;foreach(var code in input){var call=code.operand as MethodInfo;if(call!=null&&call.DeclaringType==typeof(MeshBatch)&&call.Name=="ScreenStrip"&&call.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(Camera),typeof(Vector3),typeof(Vector3),typeof(float),typeof(Color)})){code.opcode=OpCodes.Call;code.operand=typeof(EditingGridStrokes).GetMethod("BeatStrip",TimingGroups.F);count++;}yield return code;}
   if(count!=1)throw new MissingMethodException("Beat-grid stroke call changed");
  }
  internal static void Install(Harmony harmony){harmony.Patch(typeof(NoteRenderer).GetMethod("DrawBeats",TimingGroups.F),transpiler:new HarmonyMethod(typeof(EditingGridStrokes).GetMethod("Thicken",TimingGroups.F)));BeatHoverIndex.Install(harmony);}
 }
 // The renderer owns beat positions. Cursor display reads this small screen-space
 // index instead of solving the chart's scroll timeline on every pointer movement.
 internal static class BeatHoverIndex {
  internal struct Segment {internal Vector2 A,D;internal float Inv,Z;internal double Time;internal Rect Bounds;}
  sealed class Index {
   internal readonly List<List<Segment>> Rows=new List<List<Segment>>();internal readonly Dictionary<double,float> Depths=new Dictionary<double,float>();
   internal bool Active,Valid,Picked;internal object History;internal int Revision,Width,Height;internal double Now,PickedTime;internal float PickedZ,Margin;internal Camera Camera;internal Matrix4x4 View,Projection;internal Rect Pixel;internal float Near;
  }
  const int RowHeight=32;
  static readonly ConditionalWeakTable<NoteRenderer,Index> indices=new ConditionalWeakTable<NoteRenderer,Index>();
  internal static long Captures,Queries,Candidates;
  static void Begin(NoteRenderer __instance,bool __0,bool __runOriginal){
   if(!__runOriginal)return;var index=indices.GetValue(__instance,r=>new Index());index.Active=index.Valid=index.Picked=false;
   foreach(var row in index.Rows)row.Clear();index.Depths.Clear();
   var ui=InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.I;var boot=ui==null?null:ui.boot;
   if(!__0||boot==null||!ui.ready||!ui.CrosshairEnabled||!boot.HasDocument||!ReferenceEquals(InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.Get(boot,"notes"),__instance)||((StudioTransport)InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.Get(boot,"transport")).Playing)return;
   index.Active=true;index.History=boot.History;index.Revision=boot.History.Revision;index.Now=RendererAccess.Now(__instance);index.Camera=RendererAccess.Camera(__instance);index.View=index.Camera.worldToCameraMatrix;index.Projection=index.Camera.projectionMatrix;index.Pixel=index.Camera.pixelRect;index.Near=index.Camera.nearClipPlane;index.Width=Screen.width;index.Height=Screen.height;index.Margin=32*Mathf.Max(1,Screen.height/1080f);
   int count=Screen.height/RowHeight+1;while(index.Rows.Count<count)index.Rows.Add(new List<Segment>());Captures++;
  }
  static void End(NoteRenderer __instance){Index index;if(indices.TryGetValue(__instance,out index)&&index.Active)index.Valid=index.Depths.Count>0;}
  static float Depth(NoteRenderer renderer,double time){float z=FastScrollHooks.WorldZ(renderer,time);Record(renderer,time,z);return z;}
  internal static void Record(NoteRenderer renderer,double time,float z){
   Index index;if(!indices.TryGetValue(renderer,out index)||!index.Active)return;index.Depths[time]=z;var p=RendererAccess.Profile(renderer);var camera=index.Camera;
   for(int lane=0;lane<6;lane++){
    var a=camera.WorldToScreenPoint(BeatGuideGeometry.Point(p,lane,0,z));var b=camera.WorldToScreenPoint(BeatGuideGeometry.Point(p,lane,1,z));if(a.z<=camera.nearClipPlane||b.z<=camera.nearClipPlane)continue;
    var start=new Vector2(a.x,index.Height-a.y);var end=new Vector2(b.x,index.Height-b.y);var delta=end-start;if(delta.sqrMagnitude<.01f)continue;
    var segment=new Segment{A=start,D=delta,Inv=1/delta.sqrMagnitude,Z=z,Time=time,Bounds=Rect.MinMaxRect(Mathf.Min(start.x,end.x)-index.Margin,Mathf.Min(start.y,end.y)-index.Margin,Mathf.Max(start.x,end.x)+index.Margin,Mathf.Max(start.y,end.y)+index.Margin)};
    int first=Mathf.Max(0,Mathf.FloorToInt(segment.Bounds.yMin/RowHeight)),last=Mathf.Min(index.Rows.Count-1,Mathf.FloorToInt(segment.Bounds.yMax/RowHeight));for(int row=first;row<=last;row++)index.Rows[row].Add(segment);
   }
  }
  static bool SameView(Index index){return index.Camera&&index.Width==Screen.width&&index.Height==Screen.height&&index.View==index.Camera.worldToCameraMatrix&&index.Projection==index.Camera.projectionMatrix&&index.Pixel==index.Camera.pixelRect&&index.Near==index.Camera.nearClipPlane;}
  internal static bool Replay(NoteRenderer renderer,bool snap){
   var ui=InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.I;var boot=ui==null?null:ui.boot;
   if(!snap||boot==null||!ui.ready||!ui.CrosshairEnabled||!boot.HasDocument)return true;
   if(!ReferenceEquals(InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.Get(boot,"notes"),renderer))return true;
   Index index;if(!indices.TryGetValue(renderer,out index)||(!index.Valid&&!index.Active)||!SameView(index)||index.Now!=RendererAccess.Now(renderer))return false;
   // StaticBeatMeshes already compared the beat/scroll/group/viewport key.
   // Numeric note edits may change History.Revision without changing this grid.
   index.History=boot.History;index.Revision=boot.History.Revision;return true;
  }
  static bool Ready(Index index){
   var ui=InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.I;var boot=ui==null?null:ui.boot;
   return index.Valid&&boot!=null&&ReferenceEquals(index.History,boot.History)&&index.Revision==boot.History.Revision&&index.Now==boot.CurrentMs&&SameView(index);
  }
  internal static bool Pick(NoteRenderer renderer,Vector2 pointer,float radius,out double time,out float depth){
   time=0;depth=0;Index index;if(!indices.TryGetValue(renderer,out index)||!Ready(index)||radius>index.Margin)return false;
   int row=Mathf.FloorToInt(pointer.y/RowHeight);if(row<0||row>=index.Rows.Count)return false;float best=radius*radius;bool found=false;Queries++;
   foreach(var segment in index.Rows[row]){if(!segment.Bounds.Contains(pointer))continue;Candidates++;var relative=pointer-segment.A;float along=Mathf.Clamp01(Vector2.Dot(relative,segment.D)*segment.Inv);float error=(relative-segment.D*along).sqrMagnitude;
    if(error<best||found&&Mathf.Abs(error-best)<.00001f&&Math.Abs(segment.Time-index.Now)<Math.Abs(time-index.Now)){best=error;time=segment.Time;depth=segment.Z;found=true;}
   }
   index.Picked=found;if(found){index.PickedTime=time;index.PickedZ=depth;}return found;
  }
  internal static bool DepthAt(NoteRenderer renderer,double time,out float depth){
   depth=0;Index index;if(!indices.TryGetValue(renderer,out index)||!Ready(index))return false;
   if(index.Picked&&index.PickedTime==time){depth=index.PickedZ;return true;}return index.Depths.TryGetValue(time,out depth);
  }
  static IEnumerable<CodeInstruction> Collect(IEnumerable<CodeInstruction> input){
   int count=0;foreach(var code in input){var method=code.operand as MethodInfo;
    if(method!=null&&(method.DeclaringType==typeof(NoteRenderer)&&method.Name=="Z"||method.DeclaringType==typeof(FastScrollHooks)&&method.Name=="WorldZ")){code.opcode=OpCodes.Call;code.operand=typeof(BeatHoverIndex).GetMethod("Depth",TimingGroups.F);count++;}yield return code;
   }if(count!=1)throw new MissingMethodException("Visible beat-depth call changed");
  }
  internal static void Install(Harmony harmony){harmony.Patch(typeof(NoteRenderer).GetMethod("DrawBeats",TimingGroups.F),prefix:new HarmonyMethod(typeof(BeatHoverIndex).GetMethod("Begin",TimingGroups.F)){priority=Priority.First+500},postfix:new HarmonyMethod(typeof(BeatHoverIndex).GetMethod("End",TimingGroups.F)),transpiler:new HarmonyMethod(typeof(BeatHoverIndex).GetMethod("Collect",TimingGroups.F)));}
 }
}
