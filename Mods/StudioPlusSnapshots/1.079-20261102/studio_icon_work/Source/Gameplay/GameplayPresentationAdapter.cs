using System;using System.Reflection;using HarmonyLib;using InFalsusStudio;using InFalsusStudio.NativeEditorUI;
namespace InFalsusStudio.Gameplay {
 internal static class GameplayPresentationAdapter {
  internal static StudioTransport Owner;internal static bool Owns(StudioTransport transport){return transport!=null&&ReferenceEquals(transport,Owner)&&GameplaySession.Audio!=null;}
  internal static double DisplayTime {get{var a=GameplaySession.Audio;if(a==null||!a.Ready||GameplaySession.Phase==GameplayPhase.LeadIn)return GameplaySession.EntryTime;if(GameplaySession.Phase==GameplayPhase.Settled)return GameplaySession.Now-a.Transport.VisualOffset;return a.Transport.DisplayNow;}}
  internal static void Install(Harmony h){var f=NativeEditorUIPlugin.F;foreach(var property in new[]{"TimeMs","Playing","PlaybackStartMs"})h.Patch(typeof(StudioTransport).GetProperty(property).GetGetMethod(),prefix:new HarmonyMethod(typeof(GameplayPresentationAdapter).GetMethod(property,f)){priority=Priority.First+4000});foreach(var name in new[]{"Play","Pause","Seek","SetRate","SetAudioOffset"})h.Patch(typeof(StudioTransport).GetMethod(name),prefix:new HarmonyMethod(typeof(GameplayPresentationAdapter).GetMethod("Mutation",f)){priority=Priority.First+4000});}
  static bool TimeMs(StudioTransport __instance,ref double __result){if(!Owns(__instance))return true;__result=DisplayTime;return false;}
  static bool Playing(StudioTransport __instance,ref bool __result){if(!Owns(__instance))return true;__result=GameplaySession.Phase!=GameplayPhase.Settled;return false;}
  static bool PlaybackStartMs(StudioTransport __instance,ref double __result){if(!Owns(__instance))return true;__result=GameplaySession.EntryTime;return false;}
  static bool Mutation(StudioTransport __instance){return !Owns(__instance);}
 }
}
