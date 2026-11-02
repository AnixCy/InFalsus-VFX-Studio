using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using HarmonyLib;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  // JSON-populated DTO fields are assigned by the tool DTO codec.
  #pragma warning disable 0649
  [Serializable] public sealed class ToolOption {public string value,label,labelEn;}
  [Serializable] public sealed class ToolField {public string key,label,labelEn,type,defaultValue;public ToolOption[] options;}
  [Serializable] public sealed class ToolManifest {public int version,order;public string id,name,nameEn,script,output,description,descriptionEn;public bool capture;public string selection,gridField,groupMode,captureKind;public ToolField[] fields;}
  [Serializable] public sealed class ToolValue {public string key,value;}
  [Serializable] public sealed class ToolPreferences {public int version=1;public ToolValue[] values;public bool createGroup=true;}
  [Serializable] public sealed class ToolSegment {public int start,end,path,ease;public double x1,x2;}
  [Serializable] public sealed class ToolCapture {public int version=1;public ToolSegment[] segments;public ToolSource[] sources;}
  [Serializable] public sealed class ToolRequest {
   public string document,currentGroup,targetGroup;public bool hasBom,createGroup,overflow;public double currentTime;public ToolSource[] rows,selection,capture,skySources;public ToolRange range;public ToolTimingData timingEdit;public ToolSplitData split;public ToolSplitData[] splits;public double splitStart,splitEnd;public double[] grid;public int gridDivision;public string gridGroup;public int version=1,groupStart;public string tool;public ToolValue[] values;public ToolSegment[] segments;public double bpm,meter;
   // Preserve the existing editable MakeRain.ps1 request protocol after relocation.
   public int start,end,count,intervalMin,intervalMax;public double xMin,xMax;
  }
  [Serializable] public sealed class ToolInfo {public int segments,timings;public string message;}
  [Serializable] public sealed class LegacyRain {public int version=1,start=0,end=1000,count=200,intervalMin=50,intervalMax=150;public double xMin=-.5,xMax=1.5;public bool createGroup=true;}
  #pragma warning restore 0649
  sealed class ToolState {
   internal ToolManifest Manifest;internal string Folder;internal ConfigEntry<string> Saved;internal readonly Dictionary<string,string> Values=new Dictionary<string,string>();internal bool CreateGroup=true;internal ToolSegment[] Segments=new ToolSegment[0];internal ToolSource[] Sources=new ToolSource[0];
  }
  readonly List<ToolState> externalTools=new List<ToolState>();ToolState externalTool;bool externalMenu;string externalChoice="",externalMessage="";float externalContentHeight=600;
  [ThreadStatic] static bool rainInserting;
  internal string ExternalToolsFolder {get{return Path.Combine(BepInEx.Paths.GameRootPath,"ExternalTools");}}
  string RainL(string cn,string en){return boot==null||Val<bool>(boot,"chinese")?cn:en;}
  string ToolL(string cn,string en){return RainL(cn??en??"",string.IsNullOrEmpty(en)?cn??"":en);}
  void InitRain(){harmony.Patch(typeof(AuthoringSafety).GetMethod("BeforeCommit"),transpiler:new HarmonyMethod(typeof(NativeEditorUIPlugin).GetMethod("ToolNormalizationCall",F)));try{DiscoverExternalTools();}catch(Exception e){ToolError(e);}}
  static bool RainNormalize(){return !rainInserting;}
  static void NormalizeOutsideToolInsert(SpcDocument before,SpcDocument after){if(RainNormalize()&&InFalsusStudio.Performance.ClipboardGroups.ShouldNormalize(after))SkyGroups.NormalizeAuthoredChanges(before,after);}
  static IEnumerable<CodeInstruction> ToolNormalizationCall(IEnumerable<CodeInstruction> codes){
   // Skip the normalization call itself during generated insertion. Merely
   // skipping its original body still lets other optimizing prefixes mutate
   // group IDs; dense font edges exposed that path in the actual Player.
   bool replaced=false;foreach(var code in codes){var method=code.operand as MethodInfo;if(method!=null&&method.DeclaringType==typeof(SkyGroups)&&method.Name=="NormalizeAuthoredChanges"){code.opcode=OpCodes.Call;code.operand=typeof(NativeEditorUIPlugin).GetMethod("NormalizeOutsideToolInsert",F);replaced=true;}yield return code;}
   if(!replaced)throw new MissingMethodException("AuthoringSafety normalization call was not found.");
  }
  void DiscoverExternalTools(){
   if(rainProcess!=null)return;string selected=externalTool==null?"MakeRain":externalTool.Manifest.id;var loaded=new List<ToolState>();
   if(Directory.Exists(ExternalToolsFolder))foreach(string folder in Directory.GetDirectories(ExternalToolsFolder).OrderBy(p=>p,StringComparer.Ordinal)){
    string path=Path.Combine(folder,"tool.json");if(!File.Exists(path))continue;
    try{
     if(new FileInfo(path).Length>2000000)throw new InvalidDataException("tool.json is too large.");var m=ToolJson.Read<ToolManifest>(File.ReadAllText(path));ValidateToolManifest(m,folder);if(loaded.Any(t=>t.Manifest.id==m.id))throw new InvalidDataException("Duplicate tool id: "+m.id);
     var state=new ToolState{Manifest=m,Folder=folder,Saved=Config.Bind("ExternalTools",m.id+"Parameters","","Last confirmed parameters for "+m.id+"; one tool per root ExternalTools folder.")};LoadToolDefaults(state);
     if(!string.IsNullOrEmpty(state.Saved.Value))ApplyToolPreferences(state,ToolJson.Read<ToolPreferences>(state.Saved.Value));else if(m.id=="MakeRain"){string prior=Config.Bind("Scripts","MakeRainParameters","","Legacy Make Rain preferences; retained for migration.").Value;if(!string.IsNullOrEmpty(prior))ApplyLegacyRain(state,ToolJson.Read<LegacyRain>(prior));}
     LoadToolCapture(state);if(m.id!="DeleteTiming"&&m.id!="CopyTiming")loaded.Add(state);
    }catch(Exception e){Logger.LogWarning("External tool "+folder+": "+e.Message);}
   }
   externalTools.Clear();externalTools.AddRange(loaded.OrderBy(t=>t.Manifest.order).ThenBy(t=>t.Manifest.id,StringComparer.Ordinal));externalTool=externalTools.FirstOrDefault(t=>t.Manifest.id==selected)??externalTools.FirstOrDefault();externalMenu=false;externalChoice="";if(externalTool==null)externalMessage=RainL("ExternalTools 中没有可用的工具。","No usable tools found in ExternalTools.");
  }
  static void ValidateToolManifest(ToolManifest m,string folder){
   if(m==null||m.version!=1||!Regex.IsMatch(m.id??"",@"^[A-Za-z][A-Za-z0-9_]*$")||Path.GetFileName(folder)!=m.id||string.IsNullOrEmpty(m.name)||string.IsNullOrEmpty(m.nameEn))throw new InvalidDataException("Invalid tool identity.");
   if(string.IsNullOrEmpty(m.script)||Path.GetFileName(m.script)!=m.script||!m.script.EndsWith(".ps1",StringComparison.OrdinalIgnoreCase)||!File.Exists(Path.Combine(folder,m.script)))throw new InvalidDataException("Invalid tool script.");
   if((m.output!="blackline"&&m.output!="timing"&&m.output!="split"&&m.output!="timingedit"&&m.output!="spc")||m.fields==null||m.fields.Length>30||m.fields.Select(f=>f.key).Distinct(StringComparer.Ordinal).Count()!=m.fields.Length)throw new InvalidDataException("Invalid tool fields/output.");
   if(!string.IsNullOrEmpty(m.selection)&&!new[]{"any","skyarea","air"}.Contains(m.selection))throw new InvalidDataException("Invalid selection kind.");if(!string.IsNullOrEmpty(m.gridField)&&!m.fields.Any(f=>f.key==m.gridField))throw new InvalidDataException("Unknown gridField.");if(!string.IsNullOrEmpty(m.groupMode)&&m.groupMode!="noinput")throw new InvalidDataException("Invalid groupMode.");
   foreach(var f in m.fields){if(!Regex.IsMatch(f.key??"",@"^[A-Za-z][A-Za-z0-9_]*$")||!new[]{"time","integer","number","text","file","bool","choice"}.Contains(f.type))throw new InvalidDataException("Invalid field: "+f.key);if(f.type=="choice"&&(f.options==null||f.options.Length<1||f.options.Length>4096||!f.options.Any(o=>o.value==f.defaultValue)))throw new InvalidDataException("Invalid field choices.");}
  }
  void LoadToolDefaults(ToolState s){s.Values.Clear();foreach(var f in s.Manifest.fields)s.Values[f.key]=f.defaultValue??"";s.CreateGroup=true;string file=Path.Combine(s.Folder,"defaults.json");if(!File.Exists(file))return;if(new FileInfo(file).Length>100000)throw new InvalidDataException("defaults.json is too large.");string text=File.ReadAllText(file);if(s.Manifest.id=="MakeRain")ApplyLegacyRain(s,ToolJson.Read<LegacyRain>(text));else ApplyToolPreferences(s,ToolJson.Read<ToolPreferences>(text));}
  void ApplyLegacyRain(ToolState s,LegacyRain p){if(p==null||p.version!=1)throw new InvalidDataException("Invalid legacy Make Rain preferences.");foreach(string key in new[]{"start","end","count","intervalMin","intervalMax","xMin","xMax"})s.Values[key]=Convert.ToString(typeof(LegacyRain).GetField(key).GetValue(p),CultureInfo.InvariantCulture);s.CreateGroup=p.createGroup;}
  static void ApplyToolPreferences(ToolState s,ToolPreferences p){if(p==null||p.version!=1)return;foreach(var v in p.values??new ToolValue[0])if(s.Values.ContainsKey(v.key))s.Values[v.key]=v.value??"";s.CreateGroup=p.createGroup;}
  void SaveTool(ToolState s){s.Saved.Value=ToolJson.Write(new ToolPreferences{createGroup=s.CreateGroup,values=s.Manifest.fields.Select(f=>new ToolValue{key=f.key,value=s.Values[f.key]}).ToArray()});}
  void LoadToolCapture(ToolState s){string file=Path.Combine(s.Folder,"pattern.json");if(!s.Manifest.capture||!File.Exists(file))return;if(new FileInfo(file).Length>2000000)throw new InvalidDataException("Captured pattern is too large.");var p=ToolJson.Read<ToolCapture>(File.ReadAllText(file));if(p!=null&&p.version==1){if(p.segments!=null&&p.segments.Length<=4096)s.Segments=p.segments;if(p.sources!=null&&p.sources.Length<=256)s.Sources=p.sources;}}
  static string ToolString(ToolRequest r,string key){var v=r.values.FirstOrDefault(p=>p.key==key);return v==null?"":v.value;}
  static int ToolInt(ToolRequest r,string key){int value;if(!int.TryParse(ToolString(r,key),NumberStyles.Integer,CultureInfo.InvariantCulture,out value))throw new ArgumentException("Enter an integer: "+key);return value;}
  static double ToolDouble(ToolRequest r,string key){double value;if(!double.TryParse(ToolString(r,key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||!AuthoringSafety.Finite(value))throw new ArgumentException("Enter a finite number: "+key);return value;}
  void ToolCommit(ToolState s,ToolField f,string value){
   if(f.type=="integer"||f.type=="time"){int n;if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out n))throw new ArgumentException(RainL("请输入整数。","Enter an integer."));if(f.type=="time"&&(n<0||n>86400000))throw new ArgumentException("Time must be within 0..86400000 ms.");}
   if(f.type=="number"){double n;if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||!AuthoringSafety.Finite(n))throw new ArgumentException(RainL("请输入有限数值。","Enter a finite number."));}if(value.Length>1024)throw new ArgumentException("Field is too long.");s.Values[f.key]=value;SaveTool(s);
  }
  void ToolError(Exception e){externalMessage=RainL("外置工具：","External Tools: ")+e.Message;Logger.LogWarning(externalMessage);if(boot!=null)Set(boot,"status",externalMessage);}
 }
}
