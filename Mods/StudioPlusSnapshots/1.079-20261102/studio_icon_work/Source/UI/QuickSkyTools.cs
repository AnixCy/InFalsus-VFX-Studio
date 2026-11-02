using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  float quickToolsContentHeight=1176;bool quickSkySelection;int quickSkyCount;
  sealed class ForestPlan { internal int Id;internal string Scope;internal string[] Lines; }
  void InitQuickSkyTools(){harmony.Patch(Method("DrawAuthoringTools"),transpiler:new HarmonyMethod(typeof(NativeEditorUIPlugin).GetMethod("MoveLegacySkyTools",F)){priority=Priority.First});}
  static bool QuickSkyToolsAvailable(){return I!=null&&I.ready;}
  static IEnumerable<CodeInstruction> MoveLegacySkyTools(IEnumerable<CodeInstruction> input,ILGenerator generator){
   var code=input.ToList();int equal=code.FindIndex(c=>c.opcode==OpCodes.Ldstr&&(string)c.operand=="1. 等分切分（仅单条 SkyArea）"),repair=code.FindIndex(c=>c.opcode==OpCodes.Ldstr&&(string)c.operand=="3. 坐标修复");
   Func<CodeInstruction,string,string,bool> call=(c,type,name)=>{var m=c.operand as MethodInfo;return m!=null&&m.DeclaringType.FullName==type&&m.Name==name;};
   if(equal<0||repair<=equal)throw new MissingMethodException("Sky split UI anchors changed.");
   int start=code.FindLastIndex(equal,c=>call(c,"UnityEngine.GUILayout","Space"))-1,end=code.FindLastIndex(repair,c=>call(c,"UnityEngine.GUILayout","Space"))-1;
   int save=code.FindIndex(start,end-start,c=>call(c,"UnityEngine.GUI","get_enabled"));int restore=code.FindLastIndex(end-1,end-start,c=>call(c,"UnityEngine.GUI","set_enabled"));
   bool stored=save>=0&&code[save+1].opcode.Name.StartsWith("stloc"),stacked=save>=0&&code[save+1].opcode==OpCodes.Dup;
   if(start<0||end<=start||save<start||restore<start||!stored&&!stacked||!code[restore-1].opcode.Name.StartsWith("ldloc"))throw new MissingMethodException("Sky split GUI state anchors changed.");
   // Keep the original body for fallback UI. When the integrated quick panel
   // is active, only bypass the moved controls; preserve the later repair
   // controls' operationEnabled value and the outer GUI.enabled state. The
   // shipped optimized core keeps operationEnabled on the evaluation stack.
   var resume=generator.DefineLabel();code[end].labels.Add(resume);var original=generator.DefineLabel();
   var jump=new List<CodeInstruction>{new CodeInstruction(OpCodes.Call,typeof(NativeEditorUIPlugin).GetMethod("QuickSkyToolsAvailable",F)),new CodeInstruction(OpCodes.Brfalse,original),new CodeInstruction(code[save].opcode,code[save].operand)};
   if(stored)jump.Add(new CodeInstruction(code[save+1].opcode,code[save+1].operand));
   jump.Add(new CodeInstruction(code[restore-1].opcode,code[restore-1].operand));jump.Add(new CodeInstruction(code[restore].opcode,code[restore].operand));jump.Add(new CodeInstruction(OpCodes.Br,resume));
   jump[0].labels.AddRange(code[start].labels);code[start].labels.Clear();code[start].labels.Add(original);code.InsertRange(start,jump);return code;
  }
  void MatchedQuickSkyTools(){
   if(Event.current.type==EventType.Layout){var ids=Val<HashSet<int>>(boot,"selection");var selected=boot.Events.Where(e=>ids.Contains(e.SourceId)).ToArray();quickSkyCount=selected.Length;quickSkySelection=selected.Length>0&&selected.Length==ids.Count&&selected.All(e=>e.Kind==EventKind.SkyArea);}
   bool previous=GUI.enabled,available=previous&&boot.HasDocument&&!Transport.Playing&&!Val<bool>(boot,"guiWorkflowActive");
   try{
    MatchedSection(RainL("等分切分（单条 SkyArea）","Equal split (one SkyArea)"));
    GUI.enabled=available;MatchedField(RainL("分段数量 N","Piece count N"),"field_split_count",Val<int>(boot,"splitCount").ToString(),value=>{int n;if(!int.TryParse(value,out n)||n<2||n>SkySplitOperations.MaxPieces)throw new ArgumentException("N: 2..4096");Set(boot,"splitCount",n);});
    GUI.enabled=available&&quickSkySelection&&quickSkyCount==1;
    QuickSkyButtons(RainL("切成 N 条 SkyArea","Split into N SkyAreas"),false,RainL("转成 N 个 Flick","Convert to N Flicks"));
    QuickForestButton(RainL("记忆森林式拆解（等分）","Memory Forest split (equal)"),false);
    GUI.enabled=previous;MatchedSection(RainL("按当前网格切分（多条 SkyArea）","Current grid split (SkyAreas)"));
    var info=ContentRow(57);UiParagraph(new Rect(0,info.y+8*scale,405*scale,49*scale),RainL("当前细分：1/","Subdivision: 1/")+Prop(boot,"Division")+RainL(" 拍；两端不足一格也保留。"," beat; partial end intervals are retained."),13,Css("#8caab8"));
    GUI.enabled=available&&quickSkySelection;QuickSkyButtons(RainL("按网格切 SkyArea","Grid → SkyAreas"),true,RainL("按网格转 Flick","Grid → Flicks"));
    QuickForestButton(RainL("记忆森林式拆解（按网格）","Memory Forest split (grid)"),true);
    GUI.enabled=available;var direction=ContentRow(58);if(FlatButton(new Rect(0,direction.y+8*scale,405*scale,42*scale),RainL("生成 Flick 方向：","Flick direction: ")+(Val<int>(boot,"splitFlickDirection")==16?RainL("向左","Left"):RainL("向右","Right")),15))Queue(delegate{Set(boot,"splitFlickDirection",Val<int>(boot,"splitFlickDirection")==16?4:16);});
    GUI.enabled=previous;var help=ContentRow(165);UiParagraph(new Rect(0,help.y+8*scale,405*scale,157*scale),RainL("分别采样原来的左右曲线，坐标最多 128 份；小 SkyArea 两侧为 Linear，不指定组。记忆森林式拆解从最开始保留紫条，隔一条空一条。Flick 在每段起点采样，不额外生成尾点。","Both edges are sampled within 128 coordinate parts. Sky pieces use Linear edges and omit group assignment. Memory Forest split keeps every other piece starting from the first. Flicks sample interval starts; no extra tail Flick."),13,Css("#789eae"));
   }finally{GUI.enabled=previous;}
  }
  void QuickSkyButtons(string sky,bool grid,string flick){
   var row=ContentRow(58);if(FlatButton(new Rect(0,row.y+8*scale,197*scale,42*scale),sky,14))Command("SplitSelectedSky",grid,false);
   if(FlatButton(new Rect(208*scale,row.y+8*scale,197*scale,42*scale),flick,14))Command("SplitSelectedSky",grid,true);
  }
  void QuickForestButton(string label,bool grid){
   var row=ContentRow(58);if(FlatButton(new Rect(0,row.y+8*scale,405*scale,42*scale),label,14))Queue(delegate{SplitSelectedSkyForest(grid);});
  }
  void SplitSelectedSkyForest(bool grid){
   if(!(bool)Call(boot,"PrepareAuthoringCommand"))return;
   var ids=Val<HashSet<int>>(boot,"selection").ToArray();int[] generated=null;
   int splitCount=Val<int>(boot,"splitCount"),division=(int)Prop(boot,"Division");
   var authoringGrid=MainAuthoringGrid();
   if((bool)Call(boot,"Change",new Action<SpcDocument>(d=>generated=grid?ForestGrid(d,ids,authoringGrid,division):ForestEqual(d,ids,splitCount)))){
    Call(boot,"SetSelection",generated,generated[0]);Set(boot,"showNotes",true);
    Set(boot,"status",RainL("已生成 ","Created ")+generated.Length+" SkyArea"+RainL("（记忆森林式拆解），可撤销；保存才写盘。"," (Memory Forest split); one undo step, Save writes to disk."));
   }
  }
  internal static int[] ForestEqual(SpcDocument document,IEnumerable<int> ids,int count){
   if(count<2||count>SkySplitOperations.MaxPieces)throw new ArgumentException("N must be an integer in 2..4096.");
   var selected=ForestSelected(document,ids);if(selected.Length!=1)throw new ArgumentException("Equal splitting requires exactly one SkyArea.");
   var n=selected[0];var cuts=new double[count+1];cuts[0]=n.TimeMs;cuts[count]=n.EndMs;
   for(int i=1;i<count;i++)cuts[i]=Math.Round(n.TimeMs+n.DurationMs*i/count,10,MidpointRounding.AwayFromZero);
   string scope=TimingGroups.Model(document).Of(n.SourceId);
   return ForestApply(document,new[]{ForestMakePlan(document,n,cuts,scope)});
  }
  internal static int[] ForestGrid(SpcDocument document,IEnumerable<int> ids,AuthoringGrid grid,int division){
   if(grid==null)throw new ArgumentNullException("grid");var plans=new List<ForestPlan>();int total=0;
   var model=TimingGroups.Model(document);
   foreach(var n in ForestSelected(document,ids)){
    var cuts=new SortedSet<double>{n.TimeMs,n.EndMs};
    var lines=grid.Between(n.TimeMs,n.EndMs,division,SkySplitOperations.MaxPieces+2);
    foreach(var line in lines)if(line.TimeMs>n.TimeMs&&line.TimeMs<n.EndMs)cuts.Add(line.TimeMs);
    if(cuts.Count-1>SkySplitOperations.MaxPieces||lines.Count>=SkySplitOperations.MaxPieces+2)throw new ArgumentException("This grid creates more than 4096 pieces for one SkyArea. Use a coarser subdivision.");
    total+=cuts.Count-1;if(total>SkySplitOperations.MaxGenerated)throw new ArgumentException("More than 20000 generated notes. Split a smaller selection.");
    plans.Add(ForestMakePlan(document,n,cuts.ToArray(),model.Of(n.SourceId)));
   }
   return ForestApply(document,plans);
  }
  static SpcEvent[] ForestSelected(SpcDocument document,IEnumerable<int> ids){
   var wanted=new HashSet<int>(ids);var notes=document.ReadEvents().Where(n=>wanted.Contains(n.SourceId)).ToArray();
   if(notes.Length==0||notes.Length!=wanted.Count||notes.Any(n=>n.Kind!=EventKind.SkyArea))throw new ArgumentException("Select SkyAreas only.");
   foreach(var n in notes){
    string reason=ChartMath.InvalidReason(n)??AirReachBounds.Violation(n);
    if(reason!=null||n.DurationMs<=0)throw new ArgumentException(reason??"SkyArea needs a positive duration.");
    double progress;if(ChartMath.SkyMinimumWidth(n,out progress)<-1e-10)throw new ArgumentException("Crossed SkyArea edges must be fixed before splitting.");
   }
   return notes.OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
  }
  static ForestPlan ForestMakePlan(SpcDocument document,SpcEvent n,double[] cuts,string scope){
   int pieces=cuts.Length-1;
   for(int i=0;i<pieces;i++)if(cuts[i+1]-cuts[i]<.001)throw new ArgumentException("A split interval is shorter than 0.001 ms. Reduce N or use a coarser grid.");
   var original=document.ArgumentTokens(n.SourceId);var lines=new string[(pieces+1)/2];
   string raw=document.SourceLine(n.SourceId);int close=raw.LastIndexOf(')');
   string suffix=close>=0?raw.Substring(close+1):"";
   string indent=raw.Substring(0,raw.Length-raw.TrimStart().Length);
   for(int i=0,index=0;i<pieces;i+=2,index++){
    var start=ChartMath.SkyAt(n,cuts[i]);var a=RationalAirCoordinates.Sample(start,n.Get(2));
    var b=RationalAirCoordinates.Sample(ChartMath.SkyAt(n,cuts[i+1]),n.Get(2));var tokens=original.Take(10).ToList();
    tokens[0]=EditOperations.Number(cuts[i]);
    for(int k=0;k<3;k++){tokens[1+k]=a[k];tokens[4+k]=b[k];}
    tokens[7]="0";tokens[8]="0";tokens[9]=EditOperations.Number(cuts[i+1]-cuts[i]);
    lines[index]="skyarea("+string.Join(",",tokens)+")"+SkyGroupSyntax.ExtraProvenance(raw)+(index==0?suffix:"");
   }
   lines[0]=indent+lines[0];return new ForestPlan{Id=n.SourceId,Scope=scope,Lines=lines};
  }
  static int[] ForestApply(SpcDocument document,IEnumerable<ForestPlan> plans){
   var ids=new List<int>();string old=TimingGroups.AppendGroup;
   try{
    foreach(var plan in plans){
     TimingGroups.AppendGroup=plan.Scope;
     document.ReplaceSourceLine(plan.Id,plan.Lines[0]);ids.Add(plan.Id);
     for(int i=1;i<plan.Lines.Length;i++)ids.Add(document.Append(plan.Lines[i]));
    }
   }finally{TimingGroups.AppendGroup=old;}
   return ids.ToArray();
  }
 }
}
