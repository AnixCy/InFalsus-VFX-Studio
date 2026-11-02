using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using InFalsusStudio;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 internal static class SharedPreferences {
  internal static string Root {get;private set;}
  static ConfigFile config;static ManualLogSource logger;static Harmony harmony;
  static readonly object gate=new object();static readonly Dictionary<ConfigDefinition,ConfigEntryBase> dirty=new Dictionary<ConfigDefinition,ConfigEntryBase>();
  static float nextWrite;static bool warned;
  static readonly HashSet<StudioBootstrap> restored=new HashSet<StudioBootstrap>();
  internal static ConfigFile Open(ConfigFile legacy,BepInPlugin metadata,ManualLogSource log){
   logger=log;string isolated=Environment.GetEnvironmentVariable("INFSTUDIO_CONFIG_ROOT");
   Root=Path.GetFullPath(string.IsNullOrWhiteSpace(isolated)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"infstudio"):isolated);
   Directory.CreateDirectory(Root);Directory.CreateDirectory(Path.Combine(Root,"backups"));string path=Path.Combine(Root,"settings.cfg");
   if(!File.Exists(path)&&File.Exists(legacy.ConfigFilePath)){
    string text=File.ReadAllText(legacy.ConfigFilePath);Atomic(path,text);File.WriteAllText(Path.Combine(Root,"migration.json"),JsonUtility.ToJson(new Migration{source=legacy.ConfigFilePath,utc=DateTime.UtcNow.ToString("o")},true));
   }
   config=new ConfigFile(path,false,metadata);config.SaveOnConfigSet=false;
   config.SettingChanged+=delegate(object sender,SettingChangedEventArgs e){lock(gate){dirty[e.ChangedSetting.Definition]=e.ChangedSetting;nextWrite=Time.realtimeSinceStartup+.35f;}};
   harmony=new Harmony("local.infalsusstudio.sharedpreferences");
   harmony.Patch(NativeEditorUIPlugin.Method("get_PreferencesPath"),prefix:H("PresentationPath"));
   harmony.Patch(NativeEditorUIPlugin.Method("InitializePresentation"),prefix:H("BeforePresentation"));
   harmony.Patch(typeof(StudioBootstrap).GetMethod("Initialize",NativeEditorUIPlugin.F),postfix:H("Initialized"));
   harmony.Patch(NativeEditorUIPlugin.Method("ChooseBackground"),prefix:H("ChooseBackground"));
   foreach(string name in new[]{"DrawCalibration","ChooseProfile"})harmony.Patch(NativeEditorUIPlugin.Method(name),postfix:H("CameraEdited"));
   return config;
  }
  [Serializable]sealed class Migration {public int version=1;public string source,utc;}
  static HarmonyMethod H(string name){return new HarmonyMethod(typeof(SharedPreferences).GetMethod(name,NativeEditorUIPlugin.F));}
  internal static string Put(string text,string section,string key,string value){
   string nl=text.Contains("\r\n")?"\r\n":"\n";var block=Regex.Match(text,@"(?ms)^\["+Regex.Escape(section)+@"\][^\r\n]*(?:\r?\n|\z).*?(?=^\[|\z)");string line=key+" = "+value;
   if(!block.Success)return text+(text.Length>0&&!text.EndsWith("\n",StringComparison.Ordinal)?nl:"")+nl+"["+section+"]"+nl+line+nl;
   string body=block.Value;var found=Regex.Match(body,@"(?m)^[ \t]*"+Regex.Escape(key)+@"[ \t]*=[^\r\n]*");
   body=found.Success?body.Substring(0,found.Index)+line+body.Substring(found.Index+found.Length):body+(body.EndsWith("\n",StringComparison.Ordinal)?"":nl)+line+nl;
   return text.Substring(0,block.Index)+body+text.Substring(block.Index+block.Length);
  }
  internal static void Atomic(string path,string text){
   Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{File.WriteAllText(tmp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(tmp,path,Path.Combine(Root,"backups",Path.GetFileName(path)+".bak"),true);else File.Move(tmp,path);}
   finally{if(File.Exists(tmp))File.Delete(tmp);}
  }
  internal static void Complete(){
   string text=File.Exists(config.ConfigFilePath)?File.ReadAllText(config.ConfigFilePath):"";
   lock(gate)foreach(var entry in config){string section=entry.Key.Section,key=entry.Key.Key;var block=Regex.Match(text,@"(?ms)^\["+Regex.Escape(section)+@"\][^\r\n]*(?:\r?\n|\z).*?(?=^\[|\z)");if(!block.Success||!Regex.IsMatch(block.Value,@"(?m)^[ \t]*"+Regex.Escape(key)+@"[ \t]*="))dirty[entry.Key]=entry.Value;}
   TryFlush();
  }
  internal static void Store(ConfigEntryBase entry){lock(gate)dirty[entry.Definition]=entry;Flush();}
  internal static void Flush(){
   if(config==null)return;lock(gate){if(dirty.Count==0)return;
    using(var fileLock=new FileStream(Path.Combine(Root,"settings.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
     string text=File.Exists(config.ConfigFilePath)?File.ReadAllText(config.ConfigFilePath):"";
     foreach(var item in dirty)text=Put(text,item.Key.Section,item.Key.Key,item.Value.GetSerializedValue());
     Atomic(config.ConfigFilePath,text);dirty.Clear();warned=false;
    }
   }
  }
  static void TryFlush(){try{Flush();}catch(Exception e){if(!warned){logger.LogWarning("User preferences retained for retry: "+e.Message);warned=true;}}}
  internal static void Tick(){if(config!=null&&Time.realtimeSinceStartup>=nextWrite){nextWrite=Time.realtimeSinceStartup+.5f;TryFlush();}}
  internal static void Dispose(){cameraDue=0;TickCamera(false);TryFlush();if(harmony!=null)harmony.UnpatchSelf();config=null;dirty.Clear();restored.Clear();}
  static bool PresentationPath(ref string __result){__result=Path.Combine(Root,"presentation.json");return false;}
  static void BeforePresentation(){
   // Isolated checks never import the account's production presentation data.
   if(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INFSTUDIO_CONFIG_ROOT")))return;
   string old=Path.Combine(Application.persistentDataPath,"presentation.v09.json"),next=Path.Combine(Root,"presentation.json");
   try{if(!File.Exists(next)&&File.Exists(old)&&new FileInfo(old).Length<65536)Atomic(next,File.ReadAllText(old));
    old=Path.Combine(Application.persistentDataPath,"infalsus-camera-profile.json");next=Path.Combine(Root,"camera.json");if(!File.Exists(next)&&File.Exists(old)&&new FileInfo(old).Length<65536){var state=new CameraState();JsonUtility.FromJsonOverwrite(File.ReadAllText(old),state);if(state.Valid())Atomic(next,JsonUtility.ToJson(state,true));}
   }catch(Exception e){logger.LogWarning("Presentation migration skipped: "+e.Message);}
  }
  [Serializable]sealed class CameraState {
   public int version=1;public float verticalFov,cameraPitch,cameraHeight,cameraBack,sideRise,sideRun,skyHeight,flickHeightUnits,flickCapSkew,stageFar;public bool lock16By9;
   internal bool Valid(){return (version==1||version==2)&&(version==1||Range(stageFar,10,2000))&&Range(verticalFov,15,100)&&Range(cameraPitch,0,90)&&Range(cameraHeight,.1f,100)&&Range(cameraBack,.1f,100)&&Range(sideRise,0,10)&&Range(sideRun,.01f,10)&&Range(skyHeight,.01f,cameraHeight-.05f)&&Range(flickHeightUnits,.03f,.9f)&&Range(flickCapSkew,0,2);}
   static bool Range(float x,float low,float high){return !float.IsNaN(x)&&!float.IsInfinity(x)&&x>=low&&x<=high;}
   internal CameraState(){}
   internal CameraState(StudioProfile p){version=2;stageFar=p.stageFar;lock16By9=p.lock16By9;verticalFov=p.verticalFov;cameraPitch=p.cameraPitch;cameraHeight=p.cameraHeight;cameraBack=p.cameraBack;sideRise=p.sideRise;sideRun=p.sideRun;skyHeight=p.skyHeight;flickHeightUnits=p.flickHeightUnits;flickCapSkew=p.flickCapSkew;}
   internal void Apply(StudioProfile p){if(version>=2){p.stageFar=stageFar;p.lock16By9=lock16By9;}p.verticalFov=verticalFov;p.cameraPitch=cameraPitch;p.cameraHeight=cameraHeight;p.cameraBack=cameraBack;p.sideRise=sideRise;p.sideRun=sideRun;p.skyHeight=skyHeight;p.flickHeightUnits=flickHeightUnits;p.flickCapSkew=flickCapSkew;}
  }
  static void Initialized(StudioBootstrap __instance){
   if(!restored.Add(__instance))return;string path=Path.Combine(Root,"camera.json");
   try{if(!File.Exists(path)){NativeEditorUIPlugin.SetTrackView(__instance.Profile,true);__instance.RebuildGeometry();}else if(new FileInfo(path).Length<65536){var state=JsonUtility.FromJson<CameraState>(File.ReadAllText(path));if(state!=null&&state.Valid()){state.Apply(__instance.Profile);__instance.RebuildGeometry();}}}catch(Exception e){logger.LogWarning("Camera preferences ignored: "+e.Message);}
  }
  static string cameraPending;static float cameraDue;
  static void CameraEdited(StudioBootstrap __instance){
   var ui=NativeEditorUIPlugin.I;if(ui==null||!ui.ready||ui.panel!="设置"||ui.settingsTab!="相机参数")return;
   RecordCamera(__instance);
  }
  internal static void RecordCamera(StudioBootstrap owner){
   var state=new CameraState(owner.Profile);if(!state.Valid())return;string text=JsonUtility.ToJson(state,true);
   if(cameraPending==text)return;cameraPending=text;cameraDue=Time.realtimeSinceStartup+.5f;
  }
  internal static void TickCamera(bool captured){if(cameraPending==null||captured||Time.realtimeSinceStartup<cameraDue)return;try{string path=Path.Combine(Root,"camera.json");if(!File.Exists(path)||File.ReadAllText(path)!=cameraPending)Atomic(path,cameraPending);}catch(Exception e){logger.LogWarning("Camera preferences save failed: "+e.Message);}cameraPending=null;}
  static bool ChooseBackground(StudioBootstrap __instance){
   string folder=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Background");
   if(!Directory.Exists(folder)){NativeEditorUIPlugin.Set(__instance,"status","制谱器根目录的 Background 文件夹缺失，请恢复安装目录。");return false;}
   var dialogs=typeof(StudioBootstrap).Assembly.GetType("InFalsusStudio.StudioFileDialogs");string path=(string)dialogs.GetMethod("Open",NativeEditorUIPlugin.F).Invoke(null,new object[]{"选择背景 PNG / JPG",folder,"png,jpg,jpeg"});if(path.Length>0)NativeEditorUIPlugin.Call(__instance,"SetBackground",path,true);return false;
  }
 }
}
