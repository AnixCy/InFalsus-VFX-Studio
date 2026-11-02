using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class AltHandleLayout {
  static readonly BindingFlags F=TimingGroups.F;
  static readonly Type PointType=typeof(StudioBootstrap).GetNestedType("HandlePoint",F);
  static readonly FieldInfo Kind=PointType.GetField("Kind",F),Label=PointType.GetField("Label",F),Locked=PointType.GetField("Locked",F);
  static readonly Color Blue=new Color(0,1,1,1),Yellow=new Color(1,1,0,1),Grey=new Color(.60f,.60f,.65f,.80f);
  static bool IsTime(NoteHandle kind,string label){return (kind==NoteHandle.Head||kind==NoteHandle.Tail)&&label!="MOVE";}
  internal static Color Ink(NoteHandle kind,string label,bool locked=false){return locked?Grey:IsTime(kind,label)?Yellow:Blue;}
  static Color PointInk(Color original,NoteHandle kind,string label){return Ink(kind,label);}
  // Caption removal is applied on every GUI event, keeping the control tree
  // consistent while retaining the original handle coordinates and hit tests.
  static void HandleText(StudioBootstrap owner,Vector2 pos,string text,float width,bool box,float size,TextAnchor anchor,NoteHandle kind,bool locked){}
  static void DrawPoint(Rect rect,Texture texture){
   Vector2 center=rect.center;rect.width*=.75f;rect.height*=.75f;rect.center=center;Color fill=GUI.color;float border=Mathf.Max(1,rect.width*.15f);
   try{GUI.color=new Color(.015f,.035f,.055f,1);GUI.DrawTexture(new Rect(rect.x-border,rect.y-border,rect.width+2*border,rect.height+2*border),Texture2D.whiteTexture);GUI.color=fill;GUI.DrawTexture(rect,texture);}finally{GUI.color=fill;}
  }
  static IEnumerable<CodeInstruction> ColorHandles(IEnumerable<CodeInstruction> input){
   var code=input.ToList();int locked=code.FindIndex(c=>c.opcode==OpCodes.Ldfld&&Equals(c.operand,Locked));if(locked<1||!code[locked-1].opcode.Name.StartsWith("ldloc"))throw new MissingMethodException("Handle drawing local changed");
   var local=code[locked-1];int colors=0,labels=0,points=0;
   for(int i=0;i<code.Count;i++){
    var current=code[i];var ctor=current.operand as ConstructorInfo;var call=current.operand as MethodInfo;
    if(ctor!=null&&ctor.DeclaringType==typeof(Color)&&i>=4&&Float(code[i-4],.3f)&&Float(code[i-3],.95f)&&Float(code[i-2],1)&&Float(code[i-1],.95f)){
     yield return current;yield return new CodeInstruction(local.opcode,local.operand);yield return new CodeInstruction(OpCodes.Ldfld,Kind);yield return new CodeInstruction(local.opcode,local.operand);yield return new CodeInstruction(OpCodes.Ldfld,Label);yield return new CodeInstruction(OpCodes.Call,typeof(AltHandleLayout).GetMethod("PointInk",F));colors++;continue;
    }
    if(call!=null&&call.DeclaringType==typeof(StudioBootstrap)&&call.Name=="OverlayText"){
     var load=new CodeInstruction(local.opcode,local.operand);load.labels.AddRange(current.labels);current.labels.Clear();yield return load;yield return new CodeInstruction(OpCodes.Ldfld,Kind);yield return new CodeInstruction(local.opcode,local.operand);yield return new CodeInstruction(OpCodes.Ldfld,Locked);
     current.opcode=OpCodes.Call;current.operand=typeof(AltHandleLayout).GetMethod("HandleText",F);labels++;
    }
    if(call!=null&&call.DeclaringType==typeof(GUI)&&call.Name=="DrawTexture"&&call.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(Rect),typeof(Texture)})){
     current.opcode=OpCodes.Call;current.operand=typeof(AltHandleLayout).GetMethod("DrawPoint",F);points++;
    }
    yield return current;
   }
   if(colors!=1||labels!=1||points!=1)throw new MissingMethodException("Handle drawing anchors changed");
  }
  static bool Float(CodeInstruction code,float value){return code.opcode==OpCodes.Ldc_R4&&Math.Abs((float)code.operand-value)<.0001f;}
  internal static void Install(Harmony harmony){harmony.Patch(typeof(StudioBootstrap).GetMethod("DrawEditingOverlay",F),transpiler:new HarmonyMethod(typeof(AltHandleLayout).GetMethod("ColorHandles",F)));}
 }
}
