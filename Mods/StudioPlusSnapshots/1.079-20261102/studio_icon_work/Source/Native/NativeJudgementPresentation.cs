using System;
using System.IO;
using System.Collections.Generic;
using BepInEx.Configuration;
using InFalsusStudio.Core;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
 internal enum HitCharacter { None, Nia, Mei, Iris, Ayame, Story }
 // Scene-only data: original mesh, UV, material, local pose and HDR palette.
 // No prefab scripts, character portraits, gameplay input or chart mutations.
 internal static class NativeJudgementPresentation {
  [Serializable] internal sealed class Pose {public Vector3 position,scale;public Quaternion rotation;}
  [Serializable] internal sealed class Geometry {public Vector3[] vertices;public Vector2[] uv;public int[] triangles;}
  [Serializable] internal sealed class Part {public string name,material;public int gameObject;public Pose[] chain;public Geometry mesh;[NonSerialized]public Vector3[] local;}
  [Serializable] internal sealed class Palette {public string name;public int sourceIndex;public Color color,flare,beam;}
  [Serializable] internal sealed class Catalog {public float nativeHalfWidth;public Vector3 skyAnchor,cursorRoot;public Quaternion stageRotation;public Part[] parts;public Palette[] characters;}
  internal static ConfigEntry<HitCharacter> Character;
  internal static Catalog Data;
  static readonly Dictionary<int,Part> parts=new Dictionary<int,Part>();
  sealed class SlotGeometry {internal int Id;internal Vector3[] Vertices;internal Vector3 Left,Right;internal float Factor;internal bool BurstReady;internal HitCharacter Palette;internal int Seed=int.MinValue;}
  static readonly Dictionary<Slot,SlotGeometry> geometry=new Dictionary<Slot,SlotGeometry>();
  static readonly Dictionary<HitCharacter,Palette> palettes=new Dictionary<HitCharacter,Palette>();
  static readonly int[] cursorParts={2025,1175,1456};
  static Texture2D cursorBeamTexture;
  sealed class SkyStage {internal Slot Slot;internal object Profile;}
  static readonly List<SkyStage> stages=new List<SkyStage>();
  static InFalsusStudio.StudioBootstrap boot;
  internal static void Init(ConfigFile config,string folder){
   Character=config.Bind("Display","HitCharacter",HitCharacter.Story,"Native character hit layer. Click the selected character again to disable only this layer; the main hit-effect quality setting is independent.");
   if(!Enum.IsDefined(typeof(HitCharacter),Character.Value))Character.Value=HitCharacter.Story;
   Data=ReadCatalog(Path.Combine(folder,"Assets/native-presentation.bin"));
   if(Data==null||Data.characters==null||Data.characters.Length!=5||Data.parts==null||Math.Abs(Data.nativeHalfWidth-.8f)>.00001f)throw new Exception("Native judgement presentation data missing: "+(Data==null?"null":Data.nativeHalfWidth+" / palettes="+(Data.characters==null?-1:Data.characters.Length)+" / parts="+(Data.parts==null?-1:Data.parts.Length)));
   foreach(var p in Data.parts){
    if(p.mesh==null||p.mesh.vertices==null||p.mesh.vertices.Length==0||p.mesh.uv.Length!=p.mesh.vertices.Length)throw new Exception("Invalid native presentation mesh "+p.name);
    NativeVisualPlugin.Mat(p.material);p.local=new Vector3[p.mesh.vertices.Length];
    for(int i=0;i<p.local.Length;i++){Vector3 v=p.mesh.vertices[i];foreach(var pose in p.chain)v=pose.position+pose.rotation*Vector3.Scale(v,pose.scale);p.local[i]=v;}
    parts.Add(p.gameObject,p);
   }
   foreach(int id in new[]{2025,1175,1456,1786,32,55,63,28})if(!parts.ContainsKey(id))throw new Exception("Missing native presentation part "+id);
   foreach(var palette in Data.characters)palettes.Add((HitCharacter)Enum.Parse(typeof(HitCharacter),palette.name),palette);
  }
  static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException("Native presentation count out of bounds");return n;}
  static string Text(BinaryReader r){int n=Count(r,1024);var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return System.Text.Encoding.UTF8.GetString(b);}
  static Vector3 Vector(BinaryReader r){return new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
  static Color ColorValue(BinaryReader r){return new Color(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
  internal static Catalog ReadCatalog(string path){using(var r=new BinaryReader(File.OpenRead(path))){
   if(r.ReadUInt32()!=0x31504a4e)throw new InvalidDataException("Invalid native presentation catalog");
   var d=new Catalog{nativeHalfWidth=r.ReadSingle(),skyAnchor=Vector(r),cursorRoot=Vector(r),stageRotation=new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle())};d.characters=new Palette[Count(r,5)];
   for(int i=0;i<d.characters.Length;i++)d.characters[i]=new Palette{name=Text(r),sourceIndex=r.ReadInt32(),color=ColorValue(r),flare=ColorValue(r),beam=ColorValue(r)};
   d.parts=new Part[Count(r,32)];for(int i=0;i<d.parts.Length;i++){
    var p=new Part{name=Text(r),gameObject=r.ReadInt32(),material=Text(r)};p.chain=new Pose[Count(r,16)];
    for(int j=0;j<p.chain.Length;j++)p.chain[j]=new Pose{position=Vector(r),rotation=new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),scale=Vector(r)};
    p.mesh=new Geometry();int n=Count(r,4096);p.mesh.vertices=new Vector3[n];p.mesh.uv=new Vector2[n];for(int j=0;j<n;j++)p.mesh.vertices[j]=Vector(r);for(int j=0;j<n;j++)p.mesh.uv[j]=new Vector2(r.ReadSingle(),r.ReadSingle());p.mesh.triangles=new int[Count(r,24576)];for(int j=0;j<p.mesh.triangles.Length;j++){int index=r.ReadInt32();if(index<0||index>=n)throw new InvalidDataException("Native mesh index out of bounds");p.mesh.triangles[j]=index;}d.parts[i]=p;
   }if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Unexpected native presentation data tail");return d;
  }}
  internal static HitCharacter Display(object editor){return Character.Value;}
  internal static void Select(object editor,HitCharacter value){
   bool off=Display(editor)==value;
   Character.Value=off?HitCharacter.None:value;
  }
  internal static Palette Colors(HitCharacter value){Palette valueFor;return palettes.TryGetValue(value,out valueFor)?valueFor:null;}
  internal static Color Outline(HitCharacter value){var p=Colors(value);if(p==null)return Color.clear;float maximum=Math.Max(.0001f,Math.Max(p.color.r,Math.Max(p.color.g,p.color.b)));return new Color(p.color.r/maximum,p.color.g/maximum,p.color.b/maximum,1);}
  static Slot Take(VisualSet set,int id,string name){
   var p=parts[id];var s=set.Take(name,NativeVisualPlugin.Mat(p.material));SlotGeometry previous;
   if(!geometry.TryGetValue(s,out previous)||previous.Id!=id){s.Mesh.Clear();s.Mesh.vertices=p.local;s.Mesh.uv=p.mesh.uv;s.Mesh.triangles=p.mesh.triangles;s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();geometry[s]=new SlotGeometry{Id=id};}
   return s;
  }
  static void SkyPose(Slot s,object profile,Quaternion rotation,Vector3 localOffset){
   float scale=NativeVisualPlugin.N(profile,"centralHalfWidth")/Data.nativeHalfWidth;
   // CoreScene CameraPerspective is unrotated; GameCamera in GameScene is a
   // tilted STAGE parent, not a Camera component. Preserve that extra basis.
   rotation=rotation*Data.stageRotation;
   var tr=s.Object.transform;tr.localPosition=new Vector3(0,NativeVisualPlugin.N(profile,"skyHeight"),0)+rotation*(localOffset*scale);tr.localRotation=rotation;tr.localScale=Vector3.one*scale;
  }
  internal static void SkyLine(VisualSet set,object profile){
   var s=Take(set,1786,"sky-judgement-native");var camera=Camera.main;
   SkyPose(s,profile,camera?camera.transform.rotation:Quaternion.identity,Vector3.zero);
   stages.RemoveAll(row=>!row.Slot.Object||ReferenceEquals(row.Slot,s));stages.Add(new SkyStage{Slot=s,Profile=profile});
  }
  internal static void CameraChanged(Camera camera){
   if(!NativeVisualPlugin.Active||!camera)return;
   foreach(var row in stages)if(row.Slot.Object&&row.Slot.Used)SkyPose(row.Slot,row.Profile,camera.transform.rotation,Vector3.zero);
  }
  internal static void Cursor(object renderer,float normalized){
   var set=NativeVisualPlugin.Visuals(renderer);var profile=NativeVisualPlugin.Get(renderer,"p");var camera=(Camera)NativeVisualPlugin.Get(renderer,"camera");if(!camera)return;
   float x=InFalsusStudio.StageSpace.AirX((InFalsusStudio.StudioProfile)profile,AirReachBounds.Clamp(normalized));
   if(!boot)boot=UnityEngine.Object.FindFirstObjectByType<InFalsusStudio.StudioBootstrap>();
   var pose=boot?(CursorPose)NativeVisualPlugin.Get(boot,"cursorPose"):new CursorPose(normalized,-1,"Idle");
   bool active=pose.Phase=="Sky center"||(pose.Phase!=null&&(pose.Phase.StartsWith("Flick sweep")||pose.Phase.StartsWith("Mixed Flick")));
   if(InFalsusStudio.Gameplay.GameplaySession.Active)active=InFalsusStudio.Gameplay.GameplaySession.Engine.ActiveSky!=null;
   foreach(int id in cursorParts){
    var s=Take(set,id,id==2025?"cursor-body-native":id==1175?"cursor-line-native":"cursor-point-native");
    SkyPose(s,profile,camera.transform.rotation,id==2025?Data.cursorRoot-Data.skyAnchor:Vector3.zero);
    s.Object.transform.localPosition+=Vector3.right*x;
    if(id==1175)s.Material.SetTexture("_MainTex",CursorBeamTexture());
    if(id==2025){var texture=NativeVisualPlugin.Textures[active?"cursor_active_basemap":"cursor_basemap"];var emission=NativeVisualPlugin.Textures[active?"cursor_active_emissive":"cursor_emissive"];s.Material.SetTexture("_BaseMap",texture);s.Material.SetTexture("_MainTex",texture);s.Material.SetTexture("_EmissionMap",emission);}
   }
  }
  internal static float CursorBeamFade(float v){return Mathf.SmoothStep(0,1,Mathf.Clamp01((1-v)/.18f));}
  static Texture2D CursorBeamTexture(){
   if(cursorBeamTexture)return cursorBeamTexture;
   // The native DXT5 texture has nonzero alpha at its far edge. Keep its RGB,
   // core, width and native UVs; feather only the last 18% to exact zero.
   // Read back once into a private linear texture, never once per frame and
   // never modify the shared bundle texture or any other native material.
   var source=NativeVisualPlugin.Mat("cursor-line-yellow").GetTexture("_MainTex");
   var previous=RenderTexture.active;var temporary=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
   Texture2D texture=null;
   try{
    Graphics.Blit(source,temporary);RenderTexture.active=temporary;
    texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
    texture.name="Native cursor beam with transparent far edge";texture.hideFlags=HideFlags.HideAndDontSave;
    texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);
    var pixels=texture.GetPixels32();for(int y=0;y<source.height;y++){float fade=CursorBeamFade((float)y/(source.height-1));for(int x=0;x<source.width;x++){int i=y*source.width+x;pixels[i].a=(byte)Mathf.RoundToInt(pixels[i].a*fade);}}
    texture.SetPixels32(pixels);texture.Apply(false,true);texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=source.filterMode;texture.anisoLevel=source.anisoLevel;
    cursorBeamTexture=texture;return texture;
   }catch{if(texture)UnityEngine.Object.Destroy(texture);throw;}
   finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);}
  }
  // Track._iBA: .5-second tween to .999, with random seed/flare direction.
  // Using chart time keeps scrubbing deterministic; native textures/shader animate it.
  internal static float BurstProgress(double ageMs){return (float)Math.Max(0,Math.Min(.999,ageMs*.999/500));}
  internal static void Burst(object renderer,int lane,int width,double ageMs,int seed){
   var colors=Colors(Character.Value);if(colors==null||ageMs<0||ageMs>=500)return;
   width=lane==0||lane==5?1:Math.Max(1,Math.Min(4-lane+1,width));int id=width==1?32:width==2?55:width==3?63:28;
   var profile=(InFalsusStudio.StudioProfile)NativeVisualPlugin.Get(renderer,"p");var set=NativeVisualPlugin.Visuals(renderer);var slot=Take(set,id,"character-burst-native");
   var left=InFalsusStudio.StageSpace.FloorBound(profile,lane,width,false,0,0);var right=InFalsusStudio.StageSpace.FloorBound(profile,lane,width,true,0,0);var center=(left+right)*.5f;
   var p=parts[id];float nativeWidth=p.chain[0].scale.x;float nativeCenter=p.chain[0].position.x;
   // Recenter the source quad to each actual lane footprint, preserving its
   // native -2.01 vs -1.5 placement and one-unit longitudinal depth.
   var cached=geometry[slot];float factor=profile.centralHalfWidth/Data.nativeHalfWidth;
   if(!cached.BurstReady||cached.Left!=left||cached.Right!=right||cached.Factor!=factor){
    if(cached.Vertices==null)cached.Vertices=new Vector3[p.local.Length];var axis=(right-left).normalized;float span=(right-left).magnitude*((lane==0||lane==5?.42f:nativeWidth)/(.4f*width));
    for(int i=0;i<cached.Vertices.Length;i++){var v=p.local[i];cached.Vertices[i]=center+axis*((v.x-nativeCenter)/nativeWidth*span)+Vector3.up*(v.y*factor)+Vector3.forward*((v.z+1.5f)*factor);}
    slot.Mesh.vertices=cached.Vertices;slot.Mesh.RecalculateBounds();cached.Left=left;cached.Right=right;cached.Factor=factor;cached.BurstReady=true;
   }
   if(cached.Palette!=Character.Value){slot.Material.SetColor("_Color",colors.color);slot.Material.SetColor("_FlareColor",colors.flare);slot.Material.SetColor("_BeamColor",colors.beam);cached.Palette=Character.Value;}
   slot.Material.SetFloat("_Progress",BurstProgress(ageMs));
   if(cached.Seed!=seed){float random=(float)(Math.Abs(Math.Sin(seed*12.9898+78.233)*43758.5453)%1);slot.Material.SetFloat("_RandomSeed",random);slot.Material.SetFloat("_FlareFlip",random>.5f?1:0);cached.Seed=seed;}slot.Material.SetFloat("_Cull",0);
  }
  internal static void Forget(VisualSet set){foreach(var slot in set.Slots.Values)geometry.Remove(slot);stages.RemoveAll(row=>!row.Slot.Object||!geometry.ContainsKey(row.Slot));}
  internal static void Dispose(){if(cursorBeamTexture)UnityEngine.Object.Destroy(cursorBeamTexture);cursorBeamTexture=null;parts.Clear();geometry.Clear();palettes.Clear();stages.Clear();boot=null;Data=null;}
 }
}
