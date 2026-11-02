using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
 internal static class NativeHudControls {
  internal static Rect TitleRect(Rect title,Rect artist,float scale){float top=title.y-2*scale,bottom=Math.Min(title.yMax,artist.y-2*scale-1);return new Rect(title.x,top,title.width,Math.Max(1,bottom-top));}
  internal static Rect ArtistRect(Rect title,Rect artist,float scale){return new Rect(title.x,artist.y-2*scale,artist.width,Math.Max(13,12.8f*scale)+4*scale);}
  // Native painting replaces only the original paint branch. Original buttons,
  // metadata drafts and their control IDs execute in every GUI pass.
  internal static EventType OriginalPaintEvent(Event current){return NativePresentation.Hud?EventType.Layout:current.type;}
  internal static IEnumerable<CodeInstruction> PreserveControls(IEnumerable<CodeInstruction> codes){
   foreach(var code in codes){var method=code.operand as MethodInfo;if(method!=null&&method.DeclaringType==typeof(Event)&&method.Name=="get_type"){code.opcode=OpCodes.Call;code.operand=typeof(NativeHudControls).GetMethod("OriginalPaintEvent",NativeVisualPlugin.F);}yield return code;}
  }
 }
}
