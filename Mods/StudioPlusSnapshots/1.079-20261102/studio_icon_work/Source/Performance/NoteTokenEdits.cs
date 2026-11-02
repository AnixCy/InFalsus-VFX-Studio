using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.Performance {
 // The native note operations and formatters stay in charge. Reuse only the
 // parsed token boundaries while their numeric setters edit the same row.
 internal static class NoteTokenEdits {
  internal static bool Enabled=true;
  sealed class Row {internal string Raw,Prefix,Suffix;internal string[] Tokens;}
  static readonly ConditionalWeakTable<SpcDocument,Dictionary<int,Row>> rows=new ConditionalWeakTable<SpcDocument,Dictionary<int,Row>>();
  static readonly Regex call=new Regex(@"^(\s*)([A-Za-z_][A-Za-z_0-9]*)(\s*)\((.*)\)(.*)$",RegexOptions.CultureInvariant);
  static readonly FieldInfo lines=typeof(SpcDocument).GetField("lines",TimingGroups.F),text=typeof(SpcDocument).GetNestedType("Line",TimingGroups.F).GetField("Text",TimingGroups.F);
  static bool Active(SpcDocument d){return Enabled&&DragPreview.Working==d;}
  static Row Get(SpcDocument d,int id){var cache=rows.GetValue(d,x=>new Dictionary<int,Row>());string raw=d.SourceLine(id);Row r;if(cache.TryGetValue(id,out r)&&r.Raw==raw)return r;
   string editable=d.EditableSourceLine(id);var m=call.Match(editable);if(!m.Success)return null;var a=m.Groups[4];r=new Row{Raw=raw,Prefix=editable.Substring(0,a.Index),Suffix=editable.Substring(a.Index+a.Length),Tokens=a.Value.Split(',')};cache[id]=r;return r;
  }
  static void Write(SpcDocument d,int id,Row r){r.Raw=r.Prefix+string.Join(",",r.Tokens)+r.Suffix;var source=(IList)lines.GetValue(d);text.SetValue(source[d.SourceIndex(id)],r.Raw);}
  static bool Tokens(SpcDocument __instance,int __0,ref string[] __result){if(!Active(__instance))return true;var r=Get(__instance,__0);__result=r==null?new string[0]:(string[])r.Tokens.Clone();return false;}
  static bool Token(SpcDocument __instance,int __0,int __1,string __2){
   if(!Active(__instance))return true;if(!__instance.Contains(__0))throw new ArgumentOutOfRangeException("sourceId");double number;
   if(!double.TryParse(__2,NumberStyles.Float,CultureInfo.InvariantCulture,out number)||double.IsNaN(number)||double.IsInfinity(number))throw new ArgumentException("Expected a finite invariant-culture number, e.g. 123.5.");
   var r=Get(__instance,__0);if(r==null)throw new ArgumentException("Not a numeric SPC call.");if(__1<0||__1>=r.Tokens.Length)throw new ArgumentOutOfRangeException("argIndex");
   string old=r.Tokens[__1];int front=old.Length-old.TrimStart().Length,back=old.Length-old.TrimEnd().Length;if(front==old.Length)back=0;
   r.Tokens[__1]=old.Substring(0,front)+__2.Trim()+(back>0?old.Substring(old.Length-back):"");Write(__instance,__0,r);return false;
  }
  static bool Optional(SpcDocument __instance,int __0,int __1,double __2){
   if(!Active(__instance))return true;if(double.IsNaN(__2)||double.IsInfinity(__2))throw new ArgumentException("Non-finite number.");if(!__instance.Contains(__0))throw new ArgumentOutOfRangeException("sourceId");var r=Get(__instance,__0);if(r==null)throw new ArgumentException("Not an SPC call.");
   if(__1<r.Tokens.Length){__instance.SetNumber(__0,__1,__2);return false;}if(__1>32)throw new ArgumentOutOfRangeException("argIndex");var a=new List<string>(r.Tokens);while(a.Count<=__1)a.Add("");a[__1]=__2.ToString("G17",CultureInfo.InvariantCulture);r.Tokens=a.ToArray();Write(__instance,__0,r);return false;
  }
  internal static void Install(Harmony h){h.Patch(typeof(SpcDocument).GetMethod("ArgumentTokens"),new HarmonyMethod(typeof(NoteTokenEdits).GetMethod("Tokens",TimingGroups.F)));h.Patch(typeof(SpcDocument).GetMethod("SetToken"),new HarmonyMethod(typeof(NoteTokenEdits).GetMethod("Token",TimingGroups.F)));h.Patch(typeof(SpcDocument).GetMethod("SetOptionalNumber"),new HarmonyMethod(typeof(NoteTokenEdits).GetMethod("Optional",TimingGroups.F)));}
 }
}