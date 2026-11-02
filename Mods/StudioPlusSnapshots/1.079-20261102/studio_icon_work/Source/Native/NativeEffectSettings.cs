using System;
using BepInEx.Configuration;
namespace InFalsusStudio.NativeVisual {
 internal enum HitEffectStyle { None, Simple, Full }
 public sealed partial class NativeVisualPlugin {
  internal static ConfigEntry<HitEffectStyle> HitStyle;
  void InitHitStyle(){
   HitStyle=Config.Bind("Display","HitEffectStyle",HitEffectStyle.Simple,"None: hide feedback. Simple: existing lightweight feedback. Full: native hit clips and Sky judgement-line glow.");
   if(!Enum.IsDefined(typeof(HitEffectStyle),HitStyle.Value))HitStyle.Value=HitEffectStyle.Simple;
   NativeHitEffects.Init(folder);
  }
 }
 internal static class HitEffectSettings {
  internal static HitEffectStyle Display(object boot){return !(bool)NativeVisualPlugin.Get(boot,"showFeedback")?HitEffectStyle.None:NativeVisualPlugin.HitStyle.Value;}
  internal static void Set(object boot,HitEffectStyle style){NativeVisualPlugin.HitStyle.Value=style;NativeVisualPlugin.Set(boot,"showFeedback",style!=HitEffectStyle.None);}
 }
}
