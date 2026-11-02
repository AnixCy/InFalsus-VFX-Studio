// Render-only Harmony adapter, compatible with C# 5 / Unity Mono.
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using InFalsusStudio.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace InFalsusStudio.NativeVisual {
public sealed partial class NativeVisualPlugin:MonoBehaviour {
 internal BepInEx.Configuration.ConfigFile Config;internal BepInEx.Logging.ManualLogSource Logger;
 internal void Initialize(BepInEx.Configuration.ConfigFile config,BepInEx.Logging.ManualLogSource logger){Config=config;Logger=logger;InitializeEngine();}

 internal static NativeVisualPlugin Instance;
 internal static ConfigEntry<bool> Enabled,StageEnabled,NotesEnabled,CursorEnabled,EffectsEnabled,BloomEnabled;
 internal static ConfigEntry<float> BloomIntensity;
 internal static bool Ready;
 internal static AssetBundle Bundle;
 internal static Assembly Studio;
 internal static readonly BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 internal static Dictionary<string,Material> Materials=new Dictionary<string,Material>();
 internal static Dictionary<string,Mesh> Meshes=new Dictionary<string,Mesh>();
 internal static Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>();
 internal static Dictionary<object,VisualSet> Sets=new Dictionary<object,VisualSet>();
 private readonly List<AssetBundle> deps=new List<AssetBundle>();
 private Harmony harmony; private Volume volume; private string folder; private bool warned;
 private static Dictionary<Type,Dictionary<string,Func<object,object>>> getters=new Dictionary<Type,Dictionary<string,Func<object,object>>>();
 private static Dictionary<Type,Dictionary<string,Func<object,object>>> propertyGetters=new Dictionary<Type,Dictionary<string,Func<object,object>>>();
 private static Dictionary<Type,Dictionary<string,MethodInfo>> methods=new Dictionary<Type,Dictionary<string,MethodInfo>>();
 private static object Read(object o,string n,bool property){
  Dictionary<string,Func<object,object>> row;var t=o.GetType();var cache=property?propertyGetters:getters;if(!cache.TryGetValue(t,out row)){row=new Dictionary<string,Func<object,object>>();cache.Add(t,row);}string key=n;Func<object,object> fn;
  if(!row.TryGetValue(key,out fn)){var p=Expression.Parameter(typeof(object));var cast=Expression.Convert(p,t);Expression body=property?(Expression)Expression.Property(cast,t.GetProperty(n,F)):Expression.Field(cast,t.GetField(n,F));fn=Expression.Lambda<Func<object,object>>(Expression.Convert(body,typeof(object)),p).Compile();row.Add(key,fn);}return fn(o);
 }
 internal static object Get(object o,string n){return Read(o,n,false);}
 internal static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 internal static object Call(object o,string n,params object[] args){Dictionary<string,MethodInfo> row;var t=o.GetType();if(!methods.TryGetValue(t,out row)){row=new Dictionary<string,MethodInfo>();methods.Add(t,row);}MethodInfo m;if(!row.TryGetValue(n,out m)){m=t.GetMethod(n,F);row.Add(n,m);}return m.Invoke(o,args);}
 internal static Type T(string n){return Studio.GetType("InFalsusStudio."+n,true);}
 internal static float N(object o,string n){return Convert.ToSingle(Get(o,n));}
 internal static double P(object o,string n){return Convert.ToDouble(Read(o,n,true));}
 internal static float Z(object o,double t){var renderer=o as NoteRenderer;return renderer==null?(float)Call(o,"Z",t):InFalsusStudio.Performance.FastScrollHooks.WorldZ(renderer,t);}
 internal static bool Active{get{return Ready&&Enabled.Value;}}
 internal static Material Mat(string n){return Materials[n.ToLowerInvariant()];}
 internal static Shader FindKeyShader(){
  foreach(var bundle in Instance.deps){var shader=bundle.LoadAsset<Shader>("native/dependency/-8263998776326408654");if(shader&&shader.name=="Unlit/StenciledUnlitSpriteVertexColorMaskable")return shader;}
  return null;
 }
 internal static GameObject Obj(object b){return (GameObject)Get(b,"Object");}
 internal static void Show(object b,bool v){if(b!=null)Obj(b).GetComponent<MeshRenderer>().enabled=v;}
 internal static VisualSet Visuals(object o){VisualSet v;if(!Sets.TryGetValue(o,out v)){var b=Obj(Get(o,o.GetType().Name=="StageRenderer"?"floor":o.GetType().Name=="HitFeedbackRenderer"?"glow":"air"));v=new VisualSet(b.transform.parent,b.layer);Sets.Add(o,v);}return v;}
 internal void Error(Exception e){if(!warned){Logger.LogError("Visual adapter failed, original renderer retained: "+e);warned=true;}Ready=false;NativeHitEffects.DisposeAll();NativeSkyFeedback.DisposeAll();if(!string.IsNullOrEmpty(folder))File.WriteAllText(Path.Combine(folder,"compatibility.txt"),"FAIL\r\n"+e);if(harmony!=null)harmony.UnpatchSelf();foreach(var pair in Sets){pair.Value.Hide();var o=pair.Key;foreach(var n in new[]{"groundSkins","skySkin","floor","detail","glow"}){var f=o.GetType().GetField(n,F);if(f==null)continue;var v=f.GetValue(o);if(v is Array)foreach(var b in (Array)v)Show(b,true);else Show(v,true);}}}
 private void InitializeEngine(){
  Instance=this;folder=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
  Enabled=Config.Bind("Visual","Enabled",true,"Rendering only; no chart or editing changes.");
  StageEnabled=Config.Bind("Visual","NativeStage",true,"Native stage material.");
  NotesEnabled=Config.Bind("Visual","NativeNotes",true,"Native note materials and Tap mesh.");
  CursorEnabled=Config.Bind("Visual","NativeCursor",true,"Native cursor.");
  EffectsEnabled=Config.Bind("Visual","NativeEffects",true,"Original judgement particles with native material; no hit animation replacement.");
  BloomEnabled=Config.Bind("Visual","Bloom",true,"HDR bloom on the stage camera.");
  BloomIntensity=Config.Bind("Visual","BloomIntensity",0.3f,"Intensity 0..2; visual approximation, not a native parameter dump.");
  try{
   Studio=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Assembly-CSharp");Validate();
   var assets=Path.Combine(folder,"Assets");foreach(var path in Directory.GetFiles(Path.Combine(assets,"Dependencies"),"*.bundle")){var b=AssetBundle.LoadFromFile(path);if(!b)throw new Exception("Dependency failed "+path);deps.Add(b);}
   Bundle=AssetBundle.LoadFromFile(Path.Combine(assets,"nativevisual.bundle"));if(!Bundle)throw new Exception("Bundle load failed");
   foreach(var name in Bundle.GetAllAssetNames()){
    if(name.StartsWith("native/material/")){
     string key=name.Substring(16);var m=Bundle.LoadAsset<Material>(name);
     bool optionalCenter=key=="backgrounddiamond"||key=="blur";
     bool usable=m&&m.shader&&m.shader.isSupported&&m.shader.name!="Hidden/InternalErrorShader";
     if(!usable){if(optionalCenter){Logger.LogWarning("Optional central background material unavailable; existing stage/note/HUD modules retained: "+name);continue;}throw new Exception("Unsupported native material "+name);}
     Materials.Add(key,m);
    }
    else if(name.StartsWith("native/mesh/"))Meshes.Add(name.Substring(12),ReadMesh(Path.Combine(assets,"Meshes",name.Substring(12)+".mesh")));
    else if(name.StartsWith("native/texture/"))Textures.Add(name.Substring(15),Bundle.LoadAsset<Texture2D>(name));
    else if(name.StartsWith("native/texture2d/"))Textures.Add(name.Substring(17),Bundle.LoadAsset<Texture2D>(name));
   }
   NativeAir.ValidateAssets();InitHitStyle();NativeJudgementPresentation.Init(Config,folder);InitTrackLines();NativeTrack.LoadTriangles(folder);Ready=true;harmony=new Harmony("local.infalsusstudio.nativevisual");
   Patch("NoteRenderer","Draw","BeginNotes","EndNotes");Patch("NoteRenderer","UpdateConnections",null,"AirConnections");Patch("NoteRenderer","DrawGround","BeginGround","EndGround");harmony.Patch(T("NoteRenderer").GetMethod("DrawGround",F),new HarmonyMethod(typeof(Hooks.NativeTapGeometry).GetMethod("Begin",F)){priority=Priority.First},null,null,new HarmonyMethod(typeof(Hooks.NativeTapGeometry).GetMethod("End",F)),null);Patch("NoteRenderer","DrawSky","BeginSky","EndSky");Patch("NoteRenderer","DrawFlick","BeginFlick","EndFlick");Patch("NoteRenderer","DrawCursor","BeginCursor","EndCursor");Patch("NoteRenderer","Dispose",null,"DisposeSet");Patch("StageRenderer","Build","BeginStage","BuildStage");Patch("StageRenderer","Dispose",null,"DisposeSet");Patch("StageSpace","ApplyCamera",null,"ApplyCamera");
   Patch("HitFeedbackRenderer","Draw","BeginEffects","EndEffects");Patch("HitFeedbackRenderer","Rebuild",null,"RebuildEffects");Patch("HitFeedbackRenderer","TriangleParticle","BeginParticle","EndParticle");Patch("HitFeedbackRenderer","Dispose",null,"DisposeSet");
   Patch("MeshBatch","Commit","BeforeCommit",null);Patch("MeshBatch","Clear","ClearBatch",null);
   Patch("NoteRenderer","DrawGroundProjection","GroundProjection",null);
   Patch("HitFeedbackRenderer","GroundTap","BeginGroundEffect","EndGroundEffect");Patch("HitFeedbackRenderer","GroundHold","BeginGroundEffect","EndGroundEffect");
   Patch("MeshBatch","Strip","EffectStrip",null);Patch("MeshBatch","Triangle","EffectTriangle",null);
   Patch("HitFeedbackRenderer","Flick","BeginFlickEffect","EndFlickEffect");Patch("MeshBatch","ScreenStrip","FlickEffectBeam",null);
   Patch("StudioBootstrap","LateUpdate",null,"TrackFrame");InitPresentation();NativeReadability.Init(Config,harmony,folder);
   Logger.LogInfo("Native visual ready: "+Materials.Count+" materials, "+Meshes.Count+" meshes; Unity="+Application.unityVersion+" pipeline="+GraphicsSettings.currentRenderPipeline);
   File.WriteAllText(Path.Combine(folder,"compatibility.txt"),"PASS\r\nUnity="+Application.unityVersion+"\r\nNativeMaterials="+Materials.Count+"\r\nNativeMeshes="+Meshes.Count+"\r\nUTC="+DateTime.UtcNow.ToString("o"));
  }catch(Exception e){Error(e);File.WriteAllText(Path.Combine(folder,"compatibility.txt"),"FAIL\r\n"+e);}
  if(Arg("-nativevisual-probe",null)!=null)StartCoroutine(Probe());
  if(Arg("-nativevisual-test",null)!=null)StartCoroutine(SelfTest());
 }
 private void Validate(){
  foreach(var n in new[]{"NoteRenderer","StageRenderer","StageSpace","StudioProfile","MeshBatch"})T(n);
  foreach(var n in new[]{"groundSkins","skySkin","air","edges","grid","hits","p","now","hidePast","camera","ghost","connections"})if(T("NoteRenderer").GetField(n,F)==null)throw new Exception("Renderer layout changed: "+n);
  foreach(var n in new[]{"Vertices","Triangles","Uvs","Colors","Object"})if(T("MeshBatch").GetField(n,F)==null)throw new Exception("Batch layout changed: "+n);
  foreach(var n in new[]{"Commit","Clear"})if(T("MeshBatch").GetMethod(n,F)==null)throw new Exception("Batch commit interface changed: "+n);
  if(T("StageSpace").GetMethod("Surface",F).GetParameters().Length!=4)throw new Exception("Surface signature changed");
  foreach(var method in new[]{"DrawGround","DrawSky","DrawFlick"})if(T("NoteRenderer").GetMethod(method,F).GetParameters().Length!=1)throw new Exception("Note render signature changed: "+method);
  foreach(var field in new[]{"centralHalfWidth","stageNear","stageFar","sideRun","sideRise","tapDepth","tapWidthRatio","skyHeight"})if(T("StudioProfile").GetField(field,F)==null)throw new Exception("Profile layout changed: "+field);
  if(T("HitFeedbackRenderer").GetMethod("TriangleParticle",F)==null)throw new Exception("Particle render layout changed");
  NativeAir.Validate();
 }
 private static Mesh ReadMesh(string path){using(var r=new BinaryReader(File.OpenRead(path))){if(r.ReadUInt32()!=0x314d564e)throw new Exception("Invalid native mesh");int vc=r.ReadInt32(),tc=r.ReadInt32();if(vc<0||vc>100000||tc<0||tc>1000000)throw new Exception("Invalid mesh counts");var v=new Vector3[vc];var uv=new Vector2[vc];for(int i=0;i<vc;i++){v[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());}var t=new int[tc];for(int i=0;i<tc;i++){t[i]=r.ReadInt32();if(t[i]<0||t[i]>=vc)throw new Exception("Invalid native mesh index");}var m=new Mesh();m.name=Path.GetFileNameWithoutExtension(path);m.indexFormat=IndexFormat.UInt32;m.vertices=v;m.uv=uv;m.triangles=t;m.RecalculateNormals();m.RecalculateBounds();return m;}}
 private void Patch(string t,string n,string pre,string post){harmony.Patch(T(t).GetMethod(n,F),pre==null?null:new HarmonyMethod(typeof(Hooks).GetMethod(pre,F)),post==null?null:new HarmonyMethod(typeof(Hooks).GetMethod(post,F)));}
 internal void Post(Camera camera){
  if(!camera)return;camera.allowHDR=true;
  if(!volume){var go=new GameObject("NativeVisual Bloom");DontDestroyOnLoad(go);volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;var p=ScriptableObject.CreateInstance<VolumeProfile>();var b=p.Add<Bloom>(true);b.threshold.Override(1f);b.intensity.Override(Mathf.Clamp(BloomIntensity.Value,0,2));b.scatter.Override(.65f);volume.sharedProfile=p;}
  volume.enabled=Active&&BloomEnabled.Value;if(volume.enabled)camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
 }
 private IEnumerator Probe(){yield return null;yield return null;var boot=UnityEngine.Object.FindFirstObjectByType(T("StudioBootstrap"));if(boot!=null&&Ready){Call(Get(boot,"stage"),"Build",Get(boot,"Profile"));Call(boot,"LateUpdate");}foreach(var m in Materials)Logger.LogInfo(m.Key+" shader="+m.Value.shader.name);foreach(var m in Meshes)Logger.LogInfo(m.Key+" bounds="+m.Value.bounds);Application.Quit(Ready?0:2);}
 internal static string Arg(string n,string fallback){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,n);return i>=0?(i+1<a.Length&&!a[i+1].StartsWith("-")?a[i+1]:""):fallback;}
 private void OnDestroy(){NativeReadability.Dispose();NativeTrack.Dispose();NativeHitEffects.DisposeAll();NativeSkyFeedback.DisposeAll();NativePresentation.Dispose();NativeJudgementPresentation.Dispose();Ready=false;if(harmony!=null)harmony.UnpatchSelf();foreach(var v in Sets.Values)v.Dispose();Sets.Clear();foreach(var m in Meshes.Values)Destroy(m);Meshes.Clear();if(volume){Destroy(volume.sharedProfile);Destroy(volume.gameObject);}if(Bundle)Bundle.Unload(false);foreach(var d in deps)if(d)d.Unload(false);}
}
internal sealed class Range{
 internal object Batch;internal int V,I;
 internal Range(object b){Batch=b;V=((List<Vector3>)NativeVisualPlugin.Get(b,"Vertices")).Count;I=((List<int>)NativeVisualPlugin.Get(b,"Triangles")).Count;}
 internal bool Any{get{return ((List<Vector3>)NativeVisualPlugin.Get(Batch,"Vertices")).Count>V;}}
}
internal sealed class Slot{
 internal bool UsingPreviewCopy;internal List<Vector3> PreviewVertices,PreviewNormals;internal List<Vector2> PreviewUvs;internal List<int> PreviewIndices;internal List<Color> PreviewColors;
 internal readonly List<Vector2> SkyUv0=new List<Vector2>(),SkyUv1=new List<Vector2>(),SkyUv2=new List<Vector2>();
 internal bool SkyMaterialReady,SkyMaterialGhost,SkyMaterialFirst,SkyMaterialLast;
 internal bool TapMeshReady;internal Vector4 TapShape,TapLane;
 internal readonly Dictionary<string,Vector4> SelectionColors=new Dictionary<string,Vector4>();internal readonly Dictionary<string,float> SelectionFloats=new Dictionary<string,float>();
 internal readonly Dictionary<string,Texture> SelectionTextures=new Dictionary<string,Texture>();
 internal GameObject Object;internal Mesh Mesh;internal Material Material;internal bool Used;internal int NoteId=-1;
 internal bool ReusedSource;internal Vector3[] SourceVertices;internal Vector2[] SourceUvs,SourceStates;internal int[] SourceTriangles;internal double SkyStart,SkyEnd;internal float SkyScale;
 internal Slot(Transform p,string n,Material m){Object=new GameObject("NativeVisual "+n);Object.transform.SetParent(p,false);Mesh=new Mesh();Mesh.name=Object.name;Mesh.indexFormat=IndexFormat.UInt32;Object.AddComponent<MeshFilter>().sharedMesh=Mesh;var r=Object.AddComponent<MeshRenderer>();Material=new Material(m);r.sharedMaterial=Material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
 internal void Dispose(){UnityEngine.Object.Destroy(Object);UnityEngine.Object.Destroy(Mesh);UnityEngine.Object.Destroy(Material);}
}
internal sealed class VisualSet{
 internal Transform Parent;internal int Layer;internal Dictionary<string,Slot> Slots=new Dictionary<string,Slot>();private Dictionary<string,int> counters=new Dictionary<string,int>();
 internal VisualSet(Transform p,int layer){Parent=p;Layer=layer;}
 internal void Begin(){counters.Clear();foreach(var s in Slots.Values)s.Used=false;}
 internal Slot Take(string n,Material m){string kind=n.Split('-')[0]+"/"+m.name;int index;counters.TryGetValue(kind,out index);counters[kind]=index+1;string key=kind+"/"+index;if(InFalsusStudio.Performance.SkyMeshCache.Active&&(n.StartsWith("sky-",StringComparison.Ordinal)||n.StartsWith("ground-",StringComparison.Ordinal)))key="drag-note/"+n+"/"+m.name;Slot s;if(!Slots.TryGetValue(key,out s)){s=new Slot(Parent,n,m);s.Object.layer=Layer;Slots.Add(key,s);}NativePresentation.RestoreSelectionTint(s);if(s.TapMeshReady)s.Object.transform.localPosition=Vector3.zero;s.Used=true;if(!s.Object.activeSelf)s.Object.SetActive(true);return s;}
 internal void End(){foreach(var s in Slots.Values)if(s.Object.activeSelf!=s.Used)s.Object.SetActive(s.Used);if(!InFalsusStudio.Performance.SkyMeshCache.Active)foreach(string key in Slots.Keys.Where(k=>k.StartsWith("drag-note/",StringComparison.Ordinal)).ToArray()){Slots[key].Dispose();Slots.Remove(key);}}
 internal void Hide(){foreach(var s in Slots.Values)s.Object.SetActive(false);}
 internal void Dispose(){foreach(var s in Slots.Values)s.Dispose();Slots.Clear();}
}
internal static class Hooks{
 [ThreadStatic] private static int groundEffectDepth;
 [ThreadStatic] private static int flickEffectDepth;
 internal static void BeginFlickEffect(){flickEffectDepth++;}
 internal static void EndFlickEffect(){flickEffectDepth=Math.Max(0,flickEffectDepth-1);}
 internal static bool FlickEffectBeam(){return !(NativeVisualPlugin.Active&&flickEffectDepth>0);}
 internal static bool GroundProjection(){return !Notes;}
 internal static void BeginGroundEffect(){groundEffectDepth++;}
 internal static void EndGroundEffect(){groundEffectDepth=Math.Max(0,groundEffectDepth-1);}
 internal static bool EffectStrip(Vector3 __0,Vector3 __1){
  // GroundTap's two tall trails run from the judgement line into the track.
  // Keep the horizontal line flash and the separately emitted particles.
  return !(NativeVisualPlugin.Active&&groundEffectDepth>0&&Mathf.Abs(__0.x-__1.x)<.03f&&Mathf.Abs(__0.z-__1.z)>.15f);
 }
 internal static bool EffectTriangle(Vector3 __0,Vector3 __1,Vector3 __2){
  // GroundHold's translucent raised fan is the same vertical effect.
  return !(NativeVisualPlugin.Active&&groundEffectDepth>0&&Mathf.Abs(__0.z-__1.z)<.001f&&__2.y>Mathf.Max(__0.y,__1.y)+.025f&&Mathf.Abs(__2.z-__0.z)>.15f);
 }
 private struct DeferredRange {internal int Start,Count;internal DeferredRange(int start,int count){Start=start;Count=count;}}
 private static readonly Dictionary<object,List<DeferredRange>> deferred=new Dictionary<object,List<DeferredRange>>();
 private static bool Notes{get{return NativeVisualPlugin.Active&&NativeVisualPlugin.NotesEnabled.Value;}}
 internal static void BeginNotes(object __instance,object __0){if(InFalsusStudio.Performance.PausedNoteMeshes.Reusing(__instance))return;if(NativeVisualPlugin.Ready)try{NativeVisualPlugin.Visuals(__instance).Begin();NativePresentation.BeginNoteFrame(__instance);NativeAir.EnsureConnections(__instance,__0);}catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static void AirConnections(object __instance,object __0){if(NativeVisualPlugin.Ready)try{NativeAir.Connections(__instance,__0);}catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static void EndNotes(object __instance){if(InFalsusStudio.Performance.PausedNoteMeshes.Reusing(__instance))return;if(!NativeVisualPlugin.Ready)return;try{if(Notes)NativeAir.HeadPicks(__instance);NativePresentation.SelectionOutlines(__instance);foreach(var b in (Array)NativeVisualPlugin.Get(__instance,"groundSkins"))NativeVisualPlugin.Show(b,!Notes);NativeVisualPlugin.Show(NativeVisualPlugin.Get(__instance,"skySkin"),!Notes);NativeVisualPlugin.Visuals(__instance).End();}catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static class NativeTapGeometry {
 internal struct TapGeometryScope {internal StudioProfile Profile;internal float Depth,WidthRatio;}
 internal static float NativeTapDepth(StudioProfile p,float z,Mesh source){
  float unit=p.centralHalfWidth/.8f,track=Mathf.Max(.001f,Mathf.Abs(p.stageFar));float ratio=Mathf.Abs(z)/track;
  return source.bounds.size.y*unit*(1+2*ratio);
 }
 internal static void Begin(NoteRenderer __instance,SpcEvent __0,out TapGeometryScope __state){
  __state=new TapGeometryScope();if(!Notes||__0.Kind!=EventKind.Tap||__0.Lane<0||__0.Lane>5)return;
  var p=(StudioProfile)NativeVisualPlugin.Get(__instance,"p");var mesh=NativeVisualPlugin.Meshes["note01"];__state.Profile=p;__state.Depth=p.tapDepth;__state.WidthRatio=p.tapWidthRatio;
  float z=NativeVisualPlugin.Z(__instance,__0.TimeMs);p.tapDepth=NativeTapDepth(p,z,mesh);if(__0.Lane>=1&&__0.Lane<=4)p.tapWidthRatio=mesh.bounds.size.x/.4f;
 }
 internal static Exception End(TapGeometryScope __state,Exception __exception){if(__state.Profile){__state.Profile.tapDepth=__state.Depth;__state.Profile.tapWidthRatio=__state.WidthRatio;}return __exception;}
 }
 internal static void BeginGround(object __instance,ref Range[] __state){if(Notes)__state=((Array)NativeVisualPlugin.Get(__instance,"groundSkins")).Cast<object>().Select(b=>new Range(b)).ToArray();}
 internal static void EndGround(object __instance,object __0,Range[] __state){if(__state==null)return;try{
  NativeSideGuides.Draw(__instance,__0,__state.Any(r=>r.Any));
  int lane=(int)__0.GetType().GetProperty("Lane").GetValue(__0,null);bool hold=NativeVisualPlugin.Get(__0,"Kind").ToString()=="Hold";
  string mat=hold?(lane==0?"HoldNoteShaderGraphPurpleVariantA":lane==5?"HoldNoteShaderGraphPurpleVariantAltA":"HoldNoteShaderGraph VariantA"):(lane==0?"matid_notesidesingle":lane==5?"matid_notesidesingle_alt":NativeVisualPlugin.P(__0,"GroundWidth")>1?"matid_notespan":"matid_notesingle");
  foreach(var r in __state)if(r.Any){Slot s;if(!InFalsusStudio.Performance.PreviewTapMeshes.Try(__instance,__0,NativeVisualPlugin.Mat(mat),out s)){s=Copy(__instance,r,"ground-"+NativeVisualPlugin.Get(__0,"SourceId"),NativeVisualPlugin.Mat(mat));if(!s.ReusedSource){if(hold)Hold(__instance,__0,r,s);else Tap(__instance,__0,s,lane);}else if(hold)s.Material.SetFloat("_Held",(bool)NativeVisualPlugin.Call(__instance,"Active",__0)?1:0);}if((bool)NativeVisualPlugin.Call(__instance,"IsSelected",(int)NativeVisualPlugin.Get(__0,"SourceId")))NativeAir.Outline(__instance,s);NativePresentation.DimSelected(__instance,__0,s);}
 }catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static void BeginSky(object __instance,ref Range[] __state){if(Notes)__state=NativeAir.Capture(__instance,"skySkin");}
 internal static void EndSky(object __instance,object __0,Range[] __state){if(__state==null)return;try{NativeAir.Sky(__instance,__0,__state);NativeGroundProjection.Sky(__instance,__0,__state[0]);}catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static void BeginFlick(object __instance,ref Range[] __state){if(Notes)__state=NativeAir.Capture(__instance,"air");}
 internal static void EndFlick(object __instance,object __0,Range[] __state){if(__state==null||!__state[0].Any)return;try{NativeAir.Flick(__instance,__0,__state);NativeGroundProjection.Flick(__instance,__0);}catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static void BeginCursor(object __instance,ref Range[] __state){if(NativeVisualPlugin.Active&&NativeVisualPlugin.CursorEnabled.Value)__state=new[]{new Range(NativeVisualPlugin.Get(__instance,"air")),new Range(NativeVisualPlugin.Get(__instance,"edges")),new Range(NativeVisualPlugin.Get(__instance,"grid"))};}
 internal static void EndCursor(object __instance,float __0,Range[] __state){if(__state==null||!__state[0].Any)return;try{
  NativeJudgementPresentation.Cursor(__instance,__0);foreach(var r in __state)Remove(r);
 }catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static void Remove(Range r){
  // Draw() reads these triangles AFTER DrawSky/DrawFlick returns, to create
  // pick geometry and ground projections. Retire appearance only at Commit.
  int count=((List<int>)NativeVisualPlugin.Get(r.Batch,"Triangles")).Count-r.I;
  if(count<=0)return;List<DeferredRange> row;if(!deferred.TryGetValue(r.Batch,out row)){row=new List<DeferredRange>();deferred.Add(r.Batch,row);}row.Add(new DeferredRange(r.I,count));
 }
 internal static void ClearBatch(object __instance){deferred.Remove(__instance);}
 internal static void BeforeCommit(object __instance){
  List<DeferredRange> row;if(!deferred.TryGetValue(__instance,out row))return;deferred.Remove(__instance);
  if(!NativeVisualPlugin.Active)return;
  try{var t=(List<int>)NativeVisualPlugin.Get(__instance,"Triangles");foreach(var r in row.OrderByDescending(r=>r.Start)){if(r.Start<0||r.Start+r.Count>t.Count)throw new Exception("Deferred visual range changed");t.RemoveRange(r.Start,r.Count);}}
  catch(Exception e){NativeVisualPlugin.Instance.Error(e);}
 }
 internal static Slot Copy(object o,Range r,string n,Material m){
  var v=(List<Vector3>)NativeVisualPlugin.Get(r.Batch,"Vertices");var uv=(List<Vector2>)NativeVisualPlugin.Get(r.Batch,"Uvs");var t=(List<int>)NativeVisualPlugin.Get(r.Batch,"Triangles");var s=NativeVisualPlugin.Visuals(o).Take(n,m);int count=v.Count-r.V;
  s.UsingPreviewCopy=false;
  s.TapMeshReady=false;
  if(n.StartsWith("sky-",StringComparison.Ordinal)&&InFalsusStudio.Performance.PreviewMeshUpload.Copy((NoteRenderer)o,r,s))return s;
  bool cached=InFalsusStudio.Performance.SkyMeshCache.Active&&(n.StartsWith("sky-",StringComparison.Ordinal)||n.StartsWith("ground-",StringComparison.Ordinal));
  var states=(List<Vector2>)NativeVisualPlugin.Get(r.Batch,"States");
  s.ReusedSource=cached&&s.SourceVertices!=null&&s.SourceVertices.Length==count&&s.SourceTriangles.Length==t.Count-r.I;
  if(s.ReusedSource)for(int i=0;i<count;i++)if(!s.SourceVertices[i].Equals(v[r.V+i])||!s.SourceUvs[i].Equals(uv[r.V+i])||!s.SourceStates[i].Equals(states[r.V+i])){s.ReusedSource=false;break;}
  if(s.ReusedSource)for(int i=0;i<s.SourceTriangles.Length;i++)if(s.SourceTriangles[i]!=t[r.I+i]-r.V){s.ReusedSource=false;break;}
  if(!s.ReusedSource){
   var vertices=v.GetRange(r.V,count);var uvs=uv.Count>=v.Count?uv.GetRange(r.V,count):null;var triangles=new int[t.Count-r.I];for(int i=0;i<triangles.Length;i++)triangles[i]=t[r.I+i]-r.V;
   s.Mesh.Clear();s.Mesh.SetVertices(vertices);if(uvs!=null)s.Mesh.SetUVs(0,uvs);s.Mesh.SetTriangles(triangles,0);s.Mesh.colors=Enumerable.Repeat(Color.white,count).ToArray();s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();
   if(cached){s.SourceVertices=vertices.ToArray();s.SourceUvs=uvs.ToArray();s.SourceStates=states.GetRange(r.V,count).ToArray();s.SourceTriangles=triangles;}
  }
  var ghost=o.GetType().GetField("ghost",NativeVisualPlugin.F);if(ghost!=null&&(bool)ghost.GetValue(o)){
   foreach(var name in new[]{"_BaseColor","_Color","_EdgeCenterColor"})if(s.Material.HasProperty(name)){var color=m.GetColor(name);color.a*=.35f;s.Material.SetColor(name,color);}
   foreach(var name in new[]{"_BaseAlpha","_Opacity"})if(s.Material.HasProperty(name))s.Material.SetFloat(name,.35f);
  }
  return s;
 }
 private static void Hold(object o,object note,Range range,Slot s){
  // Native atlas: the left column is the uninterrupted body; the right
  // column contains the cap artwork. Studio's UV0 covers the entire atlas.
  // States.y identifies body (0), head (1), and tail (2) independently.
  var states=(List<Vector2>)NativeVisualPlugin.Get(range.Batch,"States");var uv=s.Mesh.uv;
  if(states.Count<range.V+uv.Length)throw new Exception("Hold part metadata missing");
  for(int i=0;i<uv.Length;i++){
   int part=Mathf.RoundToInt(states[range.V+i].y);float x=uv[i].x,y=uv[i].y;
   if(part==0)uv[i]=new Vector2(Mathf.Lerp(.05750825f,.44249174f,x),y);
   else if(part==1)uv[i]=new Vector2(Mathf.Lerp(.55750823f,.94249177f,x),Mathf.Lerp(.13885517f,.19999999f,y));
   else if(part==2)uv[i]=new Vector2(Mathf.Lerp(.55750805f,.94249195f,x),Mathf.Lerp(.84313464f,.85686541f,y));
   else throw new Exception("Unknown Hold atlas part "+part);
  }
  s.Mesh.uv=uv;s.Material.SetFloat("_Held",(bool)NativeVisualPlugin.Call(o,"Active",note)?1:0);
 }
 private static void Tap(object o,object note,Slot s,int lane){
  var p=NativeVisualPlugin.Get(o,"p");var src=NativeVisualPlugin.Meshes["note01"];var bounds=src.bounds;var v=src.vertices;float z=NativeVisualPlugin.Z(o,NativeVisualPlugin.P(note,"TimeMs")),width=(float)NativeVisualPlugin.P(note,"GroundWidth"),depth=NativeVisualPlugin.N(p,"tapDepth");
  float inset=(1-NativeVisualPlugin.N(p,"tapWidthRatio"))*width*.5f;
  var floor=NativeVisualPlugin.T("StageSpace").GetMethod("FloorBound",NativeVisualPlugin.F);var a=(Vector3)floor.Invoke(null,new object[]{p,lane,width,false,z,inset});var b=(Vector3)floor.Invoke(null,new object[]{p,lane,width,true,z,inset});
  // GroundCap anchors its FRONT edge at Z(TimeMs), and extends to Z+depth.
  // Centering the imported mesh at Z shifted every Tap by half its depth.
  for(int i=0;i<v.Length;i++){var pos=Vector3.Lerp(a,b,(v[i].x-bounds.min.x)/bounds.size.x);pos.z=z+(bounds.max.y-v[i].y)/bounds.size.y*depth;pos.y+=v[i].z*((lane==0||lane==5)?2f:NativeVisualPlugin.N(p,"centralHalfWidth")/.8f);v[i]=pos;}
  var triangles=src.triangles;
  NativeTrack.ClipMesh(s.Mesh,v,src.uv,triangles,NativeVisualPlugin.N(p,"stageNear"),NativeVisualPlugin.N(p,"stageFar"));
 }
 internal static void BeginStage(object __instance){foreach(var n in new[]{"floor","detail","glow"})NativeVisualPlugin.Show(NativeVisualPlugin.Get(__instance,n),true);}
 internal static void BuildStage(object __instance,object __0){NativeTrack.Build(__instance,__0);}
 internal static void TrackFrame(InFalsusStudio.StudioBootstrap __instance){if(NativeVisualPlugin.Ready){var stage=NativeVisualPlugin.Get(__instance,"stage");NativeTrack.Animate(stage,__instance.CurrentMs,__instance.Profile.scrollMultiplier);NativeTrack.TriangleVisibility(stage,__instance.CurrentMs);}}
 internal static void Judge(VisualSet set,object p,string name,string mesh,string material,int lane){
  var source=NativeVisualPlugin.Meshes[mesh];var s=set.Take(name,NativeVisualPlugin.Mat(material));var b=source.bounds;var v=source.vertices;float half=NativeVisualPlugin.N(p,"centralHalfWidth"),side=NativeVisualPlugin.N(p,"sideRun"),rise=NativeVisualPlugin.N(p,"sideRise");
  s.Material.renderQueue=2920;
  for(int i=0;i<v.Length;i++){
   if(lane==1)v[i]=new Vector3(v[i].x*half/b.extents.x,.015f+v[i].y*2.5f,v[i].z*2.5f);
   else{float u=(v[i].x-b.min.x)/b.size.x;v[i]=new Vector3(lane==0?-half-side+u*side:half+u*side,.015f+(lane==0?1-u:u)*rise,v[i].z*2.5f);}
  }
  s.Mesh.Clear();s.Mesh.vertices=v;s.Mesh.uv=source.uv;s.Mesh.triangles=source.triangles;s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();
 }
 internal static void ApplyCamera(Camera __0){if(NativeVisualPlugin.Ready){NativeVisualPlugin.Instance.Post(__0);NativeJudgementPresentation.CameraChanged(__0);}}
 internal static bool BeginEffects(object __instance,object __0,double __1,ref bool __2,object __3,double __4){
  if(!NativeVisualPlugin.Ready)return true;
  try{
   NativeVisualPlugin.Visuals(__instance).Begin();
   if(NativeVisualPlugin.Active&&NativeVisualPlugin.HitStyle.Value==HitEffectStyle.None)__2=false;
   NativeTrackLines.Draw(__instance,__0,__1,__2,__4);
   if(InFalsusStudio.Gameplay.GameplaySession.Active){NativeHitEffects.DrawGameplay(__instance,__1,InFalsusStudio.NativeEditorUI.NativeEditorUIPlugin.Val<bool>(InFalsusStudio.Gameplay.GameplaySession.Owner,"showFeedback"));return false;}
   if(NativeVisualPlugin.Active)NativeHitEffects.DrawCharacterBursts(__instance,__0,__1,__2,__4);
   if(NativeVisualPlugin.Active&&NativeVisualPlugin.HitStyle.Value==HitEffectStyle.Full){NativeHitEffects.Draw(__instance,__0,__1,__2,__3,__4);return false;}
   NativeHitEffects.Hide(__instance);
  }catch(Exception e){NativeHitEffects.Hide(__instance);NativeVisualPlugin.Instance.Error(e);}
  return true;
 }
 internal static void RebuildEffects(object __instance){NativeHitEffects.Invalidate(__instance);}
 internal static void EndEffects(object __instance){if(NativeVisualPlugin.Ready)NativeVisualPlugin.Visuals(__instance).End();}
 internal static void BeginParticle(object __instance,ref Range __state){if(NativeVisualPlugin.Active&&NativeVisualPlugin.EffectsEnabled.Value)__state=new Range(NativeVisualPlugin.Get(__instance,"glow"));}
 internal static void EndParticle(object __instance,Range __state){if(__state==null||!__state.Any)return;try{
  var s=Copy(__instance,__state,"particle",NativeVisualPlugin.Mat("TriangleJudgementLineParticleBase"));var colors=(List<Color>)NativeVisualPlugin.Get(__state.Batch,"Colors");s.Material.SetColor("_Color",colors[__state.V]);var uv=new Vector2[s.Mesh.vertexCount];for(int i=0;i<uv.Length;i++)uv[i]=i%3==0?new Vector2(.5f,1):i%3==1?new Vector2(0,0):new Vector2(1,0);s.Mesh.uv=uv;Remove(__state);
 }catch(Exception e){NativeVisualPlugin.Instance.Error(e);}}
 internal static void DisposeSet(object __instance){NativeTrack.Forget(__instance);NativeHitEffects.Dispose(__instance);NativeSkyFeedback.Dispose(__instance);NativeAir.Dispose(__instance);NativePresentation.DisposeNotes(__instance);VisualSet s;if(NativeVisualPlugin.Sets.TryGetValue(__instance,out s)){NativeJudgementPresentation.Forget(s);s.Dispose();NativeVisualPlugin.Sets.Remove(__instance);}}
}
}
