using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 // Reuse the already submitted meshes/hit triangles when a drag remains in the
 // same snapped state. Native shader materials remain live on their renderers.
 internal static class PausedNoteMeshes {
  internal static bool Enabled=true;
  sealed class Cache {internal object[] Args;internal EditHistory History;internal int Revision,W,H,SideMode;internal Matrix4x4 View,Projection;internal Vector4 Geometry,Stage;internal HashSet<int> Selected,Ignored;internal bool Ready,Reuse;}
  static readonly ConditionalWeakTable<NoteRenderer,Cache> caches=new ConditionalWeakTable<NoteRenderer,Cache>();
  internal static bool Reusing(object renderer){Cache c;return renderer is NoteRenderer&&caches.TryGetValue((NoteRenderer)renderer,out c)&&c.Reuse;}
  static bool SetSame(ISet<int> set,HashSet<int> previous){return set==null?previous==null:previous!=null&&previous.SetEquals(set);}
  static bool Draw(NoteRenderer __instance,object[] __args){
   var c=caches.GetValue(__instance,r=>new Cache());c.Reuse=false;var boot=TimingGroups.Boot;
   if(!Enabled||!SkyMeshCache.Active){c.Ready=false;return true;}
   var p=(StudioProfile)GroupRuntime.Get(__instance,"p");var camera=(Camera)GroupRuntime.Get(__instance,"camera");var geometry=new Vector4(p.centralHalfWidth,p.skyHeight,p.sideRun,p.sideRise);var stage=new Vector4(p.stageNear,p.stageFar,p.UnitsPerMs,__instance.ShowGroundProjections?1:0);
   bool same=c.Ready&&c.SideMode==InFalsusStudio.NativeVisual.NativeVisualPlugin.GetSideGuideMode()&&c.History==boot.History&&c.Revision==boot.History.Revision&&c.W==Screen.width&&c.H==Screen.height&&c.Geometry==geometry&&c.Stage==stage&&c.View==camera.worldToCameraMatrix&&c.Projection==camera.projectionMatrix&&SetSame((ISet<int>)__args[9],c.Selected)&&SetSame((ISet<int>)__args[15],c.Ignored);
   if(same)for(int i=0;i<__args.Length;i++)if(i!=9&&i!=15&&!Equals(c.Args[i],__args[i])){same=false;break;}
   if(same){c.Reuse=true;return false;}
   c.SideMode=InFalsusStudio.NativeVisual.NativeVisualPlugin.GetSideGuideMode();c.Args=(object[])__args.Clone();c.History=boot.History;c.Revision=boot.History.Revision;c.W=Screen.width;c.H=Screen.height;c.Geometry=geometry;c.Stage=stage;c.View=camera.worldToCameraMatrix;c.Projection=camera.projectionMatrix;c.Selected=__args[9]==null?null:new HashSet<int>((ISet<int>)__args[9]);c.Ignored=__args[15]==null?null:new HashSet<int>((ISet<int>)__args[15]);c.Ready=true;return true;
  }
  static Exception Failed(NoteRenderer __instance,Exception __exception){if(__exception!=null)caches.Remove(__instance);return __exception;}
  internal static void Install(Harmony h){h.Patch(typeof(NoteRenderer).GetMethod("Draw"),new HarmonyMethod(typeof(PausedNoteMeshes).GetMethod("Draw",TimingGroups.F)){priority=Priority.First+1000},null,null,new HarmonyMethod(typeof(PausedNoteMeshes).GetMethod("Failed",TimingGroups.F)),null);}
 }
}
