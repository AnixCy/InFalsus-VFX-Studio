using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace InFalsusStudio.NativeVisual {
// Original lane meshes and atlas UVs. Only the visible stage is rebuilt;
// StageSpace and the editor's selection/input geometry remain unchanged.
internal static class NativeTrack {
 private struct Vertex {internal Vector3 P;internal Vector2 U;internal Vertex(Vector3 p,Vector2 u){P=p;U=u;}}
 internal static void ClipMesh(Mesh mesh,Vector3[] vertices,Vector2[] uv,int[] indices,float near,float far){
  if(InFalsusStudio.Performance.PreviewMeshClip.Try(mesh,vertices,uv,indices,near,far))return;
  var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
  for(int i=0;i<indices.Length;i+=3){var poly=new List<Vertex>();for(int j=0;j<3;j++){int k=indices[i+j];poly.Add(new Vertex(vertices[k],uv[k]));}poly=Clip(poly,near,true);poly=Clip(poly,far,false);if(poly.Count<3)continue;int b=v.Count;foreach(var p in poly){v.Add(p.P);u.Add(p.U);}for(int j=1;j<poly.Count-1;j++){t.Add(b);t.Add(b+j);t.Add(b+j+1);}}
  mesh.Clear();mesh.SetVertices(v);mesh.SetUVs(0,u);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
 }
 private static List<Vertex> Clip(List<Vertex> poly,float z,bool lower){
  var result=new List<Vertex>();if(poly.Count==0)return result;var a=poly[poly.Count-1];bool ai=lower?a.P.z>=z:a.P.z<=z;
  foreach(var b in poly){bool bi=lower?b.P.z>=z:b.P.z<=z;if(ai!=bi){float f=(z-a.P.z)/(b.P.z-a.P.z);result.Add(new Vertex(Vector3.LerpUnclamped(a.P,b.P,f),Vector2.LerpUnclamped(a.U,b.U,f)));}if(bi)result.Add(b);a=b;ai=bi;}return result;
 }
 private static void Opaque(Material m){
  // A floor is opaque. Reusing a transparent Tap material in 1.0.0 let the
  // floor draw after distant notes and completely overwrite their pixels.
  m.renderQueue=2000;m.SetOverrideTag("RenderType","Opaque");m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
  foreach(var k in new[]{"_Surface","_SrcBlend","_DstBlend","_SrcBlendAlpha","_DstBlendAlpha","_ZWrite","_ZWriteControl"})if(m.HasProperty(k))m.SetFloat(k,k=="_SrcBlend"||k=="_SrcBlendAlpha"||k=="_ZWrite"||k=="_ZWriteControl"?1:0);
 }
 private static Material keyMaterial;private static AssetBundle keyBundle;
 private static readonly Dictionary<object,Slot[]> keyBackdropWalls=new Dictionary<object,Slot[]>();
 private static readonly Dictionary<object,Material[]> keys=new Dictionary<object,Material[]>();
 private static Material KeyMaterial(){
  if(keyMaterial)return keyMaterial;
  string file=Path.Combine(Path.GetDirectoryName(typeof(NativeTrack).Assembly.Location),"Assets","key-gamma.bundle");keyBundle=AssetBundle.LoadFromFile(file);
  if(!keyBundle)throw new Exception("Native key Gamma compatibility bundle missing");var shader=keyBundle.LoadAsset<Shader>("native/compatibility/key");
  if(!shader||!shader.isSupported)throw new Exception("Native key Gamma shader unsupported");
  // This material writes a composited stage pixel (Blend One Zero). Keep it
  // after the side walls, before every floor mark and judgement guide.
  keyMaterial=new Material(shader);keyMaterial.name="Native keybind indicator";keyMaterial.renderQueue=2901;return keyMaterial;
 }
 internal static void DrawKeyBackdrop(Action<Mesh,Matrix4x4,Material> draw){
  foreach(var row in keyBackdropWalls.Values)foreach(var slot in row)if(slot!=null&&slot.Used&&slot.Object.activeInHierarchy)draw(slot.Mesh,slot.Object.transform.localToWorldMatrix,slot.Material);
 }
 internal static void GameplayKeys(bool[] held){
  if(!NativeVisualPlugin.Ready)return;
  foreach(var row in keys.Values)for(int lane=0;lane<row.Length;lane++)if(row[lane]){
   var texture=NativeVisualPlugin.Textures[held!=null&&held[lane]?"keybind-indicators-v2-pressed":"keybind-indicators-v2-default"];
   if(row[lane].GetTexture("_MainTex")!=texture)row[lane].SetTexture("_MainTex",texture);
  }
 }
 private static readonly MaterialPropertyBlock keyProperties=new MaterialPropertyBlock();
 internal static void KeyBackground(Texture texture){foreach(var row in keys.Values)foreach(var material in row)if(material)material.SetTexture("_NativeKeyBackground",texture);foreach(var renderer in keyRenderers.Values)foreach(var item in renderer)if(item){keyProperties.SetTexture("_NativeKeyBackground",texture);item.SetPropertyBlock(keyProperties);}}
 private static readonly Dictionary<object,MeshRenderer[]> keyRenderers=new Dictionary<object,MeshRenderer[]>();
 private static readonly Dictionary<object,Material[]> walls=new Dictionary<object,Material[]>();
 private static readonly Dictionary<object,MeshRenderer[]> triangleRenderers=new Dictionary<object,MeshRenderer[]>();
 [Serializable] private sealed class TrianglePart {public string name;public Vector3[] vertices;public Vector2[] uv;public int[] triangles;}
 [Serializable] private sealed class TriangleData {public TrianglePart[] parts;}
 private static TriangleData triangleData;
 private static AssetBundle triangleBundle;private static Shader triangleShader;
 // Native GameCamera transform 2398. Shading reads its world coordinates,
 // while the editor's stage/camera continue to own projection and picking.
 private static readonly Matrix4x4 nativeCameraPose=Matrix4x4.TRS(new Vector3(0,0,-7.0200004578f),new Quaternion(-.2107276618f,0,0,.9775448442f),Vector3.one);
 internal static void LoadTriangles(string folder){
  triangleBundle=AssetBundle.LoadFromFile(Path.Combine(folder,"Assets","side-triangles.bundle"));if(!triangleBundle)throw new Exception("Native triangle vertex adapter missing");
  triangleShader=triangleBundle.LoadAsset<Shader>("native/compatibility/side-triangles");if(!triangleShader||!triangleShader.isSupported)throw new Exception("Native triangle vertex adapter unsupported");
  using(var r=new BinaryReader(File.OpenRead(Path.Combine(folder,"Assets","native-display.bin")))){
   if(r.ReadUInt32()!=0x3154444e)throw new Exception("Invalid native triangle catalog");triangleData=new TriangleData{parts=new TrianglePart[2]};
   for(int side=0;side<2;side++){
    var part=new TrianglePart{name=r.ReadString(),vertices=new Vector3[4],uv=new Vector2[4],triangles=new int[6]};triangleData.parts[side]=part;
    for(int i=0;i<4;i++){part.vertices[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());part.uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());}
    for(int i=0;i<6;i++){part.triangles[i]=r.ReadInt32();if(part.triangles[i]<0||part.triangles[i]>=4)throw new Exception("Invalid native triangle index");}
   }
   if(r.BaseStream.Position!=r.BaseStream.Length||!NativeVisualPlugin.Materials.ContainsKey("wallmaterial"))throw new Exception("Native side triangle resources missing");
  }
 }
 private static void BuildTriangles(VisualSet set,object renderer,object p,float near,float far,float half){
  // Native GameplayWall needs the original world Z and object X scale (25).
  // Keep these in UV.zw; the private vertex adapter preserves its fragment
  // bytecode, timemap animation and perspective interpolation. Baking only
  // geometry with an identity transform incorrectly stretches atlas columns.
  var renderers=new MeshRenderer[2];float size=half/.8f;
  for(int side=0;side<2;side++){
   var part=triangleData.parts[side];var slot=set.Take("sidetriangles"+side,NativeVisualPlugin.Mat("wallmaterial"));var vertices=new Vector3[part.vertices.Length];
   var attributes=new List<Vector4>(vertices.Length);
   for(int i=0;i<vertices.Length;i++){var v=part.vertices[i];vertices[i]=new Vector3(v.x*size,v.y*size,(v.z+1.5f)*far/20.181811f);attributes.Add(new Vector4(part.uv[i].x,part.uv[i].y,nativeCameraPose.MultiplyPoint3x4(v).z,25));}
   // Preserve the four native vertices. Extra CPU clipping would recompute the
   // nonlinear native vertex outputs at new points instead of interpolating
   // them as the original GPU does. Camera/frustum clipping remains active.
   if(slot.Material.shader!=triangleShader){var original=NativeVisualPlugin.Mat("wallmaterial");slot.Material.shader=triangleShader;foreach(string property in original.GetTexturePropertyNames()){var texture=original.GetTexture(property);if(texture)slot.Material.SetTexture(property,texture);}foreach(string property in new[]{"_Offset","_Threshold","_Cull"})slot.Material.SetFloat(property,original.GetFloat(property));}
   slot.Material.renderQueue=3001;slot.Mesh.Clear();slot.Mesh.vertices=vertices;slot.Mesh.SetUVs(0,attributes);slot.Mesh.triangles=part.triangles;slot.Mesh.RecalculateNormals();slot.Mesh.RecalculateBounds();
   renderers[side]=slot.Object.GetComponent<MeshRenderer>();renderers[side].enabled=NativeVisualPlugin.SideTriangles.Value;
  }
  triangleRenderers[renderer]=renderers;
 }
 internal static void TriangleVisibility(object renderer,double ms){
  if(renderer==null)return;MeshRenderer[] row;if(!triangleRenderers.TryGetValue(renderer,out row))return;
  bool show=NativeVisualPlugin.Active&&NativeVisualPlugin.StageEnabled.Value&&NativeVisualPlugin.SideTriangles.Value;
  foreach(var item in row)if(item){if(item.enabled!=show)item.enabled=show;if(show)item.sharedMaterial.SetFloat("_Offset",(float)(ms*-.0001));}
 }
 private static AssetBundle wallGammaBundle;private static Shader wallGammaShader;
 internal static bool NeedsGammaCapture {get{return wallGammaShader&&walls.Count>0&&NativeVisualPlugin.Active&&NativeVisualPlugin.StageEnabled.Value;}}
 private static void SideMaterial(Material material){
  if(!wallGammaShader){
   string file=Path.Combine(Path.GetDirectoryName(typeof(NativeTrack).Assembly.Location),"Assets","sidewall-gamma.bundle");
   wallGammaBundle=AssetBundle.LoadFromFile(file);if(!wallGammaBundle)throw new Exception("Native side-wall Gamma compatibility bundle missing");
   wallGammaShader=wallGammaBundle.LoadAsset<Shader>("native/compatibility/sidewall");if(!wallGammaShader||!wallGammaShader.isSupported)throw new Exception("Native side-wall Gamma shader unsupported");
  }
  if(material.shader!=wallGammaShader){var texture=material.GetTexture("_EnabledTexture");texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Bilinear;material.shader=wallGammaShader;material.mainTexture=texture;}material.renderQueue=2900;
  // The shader writes the native Gamma-composited pixel, so do not blend twice.
  // Its pass is fixed to Blend One Zero, ZWrite Off and ZTest LEqual.
 }
 internal static void WallViewport(int width,int height){
  var viewport=new Vector4(width,height,QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,0);
  foreach(var row in walls.Values)foreach(var material in row)if(material&&material.shader==wallGammaShader){var current=material.GetVector("_NativeComboViewport");viewport.w=current.w;material.SetVector("_NativeComboViewport",viewport);}
 }
 // Track._FBA 0x671c64..0x671dae: wrap time at 2 / speed, then
 // set BOTH enabled/disabled wall texture offsets to time * speed * .5.
 // Use the song transport for deterministic pause, seek and rate changes.
 internal static float WallPhase(double ms,float speed){double x=ms*.001*speed*.5;return (float)(x-Math.Floor(x));}
 internal static void Animate(object renderer,double ms,float speed){
  if(renderer==null||!NativeVisualPlugin.Ready||!NativeVisualPlugin.Active||!NativeVisualPlugin.StageEnabled.Value)return;
  Material[] row;if(!walls.TryGetValue(renderer,out row))return;
  var offset=new Vector2(0,WallPhase(ms,speed));
  foreach(var material in row)if(material){if(material.shader==wallGammaShader){var viewport=material.GetVector("_NativeComboViewport");viewport.w=offset.y;material.SetVector("_NativeComboViewport",viewport);}else{material.SetTextureOffset("_EnabledTexture",offset);material.SetTextureOffset("_DisabledTexture",offset);}}
 }
 internal static void Forget(object renderer){NativeKeyLabels.Forget(renderer);keyRenderers.Remove(renderer);keys.Remove(renderer);keyBackdropWalls.Remove(renderer);walls.Remove(renderer);triangleRenderers.Remove(renderer);}
 internal static void Dispose(){if(keyMaterial)UnityEngine.Object.Destroy(keyMaterial);keyMaterial=null;if(keyBundle){keyBundle.Unload(false);keyBundle=null;}keyRenderers.Clear();keys.Clear();keyBackdropWalls.Clear();walls.Clear();triangleRenderers.Clear();triangleData=null;triangleShader=null;if(triangleBundle){triangleBundle.Unload(false);triangleBundle=null;}wallGammaShader=null;if(wallGammaBundle){wallGammaBundle.Unload(false);wallGammaBundle=null;}}
 internal static void Build(object renderer,object p){
  if(!NativeVisualPlugin.Ready)return;var set=NativeVisualPlugin.Visuals(renderer);set.Begin();bool active=NativeVisualPlugin.Active&&NativeVisualPlugin.StageEnabled.Value;
  if(!active){set.End();return;}
  try{
   float near=NativeVisualPlugin.N(p,"stageNear"),far=NativeVisualPlugin.N(p,"stageFar"),half=NativeVisualPlugin.N(p,"centralHalfWidth"),side=NativeVisualPlugin.N(p,"sideRun");
   var surface=NativeVisualPlugin.T("StageSpace").GetMethod("Surface",NativeVisualPlugin.F);
   // Native global uniforms: six lanes, sixteen transition entries per lane.
   // Baseline stage is enabled; this adapter does not change chart track state.
   Shader.SetGlobalFloat("lowiro_LaneEnableStateDetails_DistanceFromJudgementToTrackEnd",far);
   Shader.SetGlobalFloatArray("lowiro_LaneEnableStateDetails_RelativeVisualTimes",Enumerable.Repeat(1e8f,96).ToArray());
   Shader.SetGlobalFloatArray("lowiro_LaneEnableStateDetails_NormalizedEnabled",Enumerable.Repeat(1f,96).ToArray());
   Backing(set,p,near,far,half,side);
   GapShadows(set,p,near,far,half);
   var wallMaterials=new Material[2];walls[renderer]=wallMaterials;var backdropWalls=new Slot[2];keyBackdropWalls[renderer]=backdropWalls;var keyMaterials=new Material[6];keys[renderer]=keyMaterials;var keyMeshes=new MeshRenderer[6];keyRenderers[renderer]=keyMeshes;
   for(int lane=0;lane<6;lane++){
    string meshName=lane==0?"shift_2":lane==5?"space_2":"lane_"+lane;
    var source=NativeVisualPlugin.Meshes[meshName];var s=set.Take("lane"+lane,NativeVisualPlugin.Mat(lane==0?"matid_trackwall1":lane==5?"matid_trackwall1_alt":"matid_trackfloor1"));
    // Keep the native enabled-stage texture and alpha. Reproduce the game's
    // Gamma blend locally instead of allowing Linear blending to brighten
    // the background through dark distant walls. Central lanes stay opaque.
    if(lane==0||lane==5)SideMaterial(s.Material);else Opaque(s.Material);
    if(s.Material.HasProperty("_LaneIndex"))s.Material.SetFloat("_LaneIndex",lane);if(s.Material.HasProperty("_IsFloorLane"))s.Material.SetFloat("_IsFloorLane",lane==0||lane==5?0:1);
    if(lane==0||lane==5){int sideIndex=lane==0?0:1;wallMaterials[sideIndex]=s.Material;backdropWalls[sideIndex]=s;}
    var native=source.vertices;var rotation=lane==0?new Quaternion(-.270598054f,.65328151f,.65328151f,-.270598054f):lane==5?new Quaternion(.65328151f,-.270598054f,-.270598054f,.65328145f):Quaternion.identity;
    for(int i=0;i<native.Length;i++)native[i]=rotation*native[i];
    float minX=native.Min(v=>v.x),maxX=native.Max(v=>v.x),minY=native.Min(v=>v.y),maxY=native.Max(v=>v.y);
    var vertices=new Vector3[native.Length];float scale=far/20.181811f;
    for(int i=0;i<native.Length;i++){
     float f=(native[i].x-minX)/(maxX-minX),x=lane==0?-half-side+f*side:lane==5?half+f*side:-half+(lane-1+f)*half*.5f;
     // Native floor source is centered at z=9.411811; judgment is z=-1.5.
     // Side source is centered at z=0. Preserve original atlas longitudinal UV.
     float z=(native[i].z+(lane==0||lane==5?1.5f:10.911811f))*scale;
     float expectedY=lane==0?Mathf.Lerp(maxY,minY,f):lane==5?Mathf.Lerp(minY,maxY,f):0;
     float relief=(native[i].y-expectedY)*half/.8f;
     vertices[i]=(Vector3)surface.Invoke(null,new object[]{p,x,z,(lane==0||lane==5?-.009f:0f)+relief});
    }
    ClipMesh(s.Mesh,vertices,source.uv,source.triangles,near,far);
    var judge=(Vector3)surface.Invoke(null,new object[]{p,lane==0?-half-side*.5f:lane==5?half+side*.5f:-half+(lane-.5f)*half*.5f,0f,0f});
    var end=judge;end.z=far;s.Material.SetVector("_JudgementLineWorldPosition",judge);s.Material.SetVector("_EndOfTrackWorldPosition",end);
    keyMaterials[lane]=KeyPanel(set,p,lane);
    keyMeshes[lane]=set.Slots.Values.First(s=>s.Used&&s.Material==keyMaterials[lane]).Object.GetComponent<MeshRenderer>();
   }
   Hooks.Judge(set,p,"judge-main","judge_main","matid_judgement",1);
   Hooks.Judge(set,p,"judge-left","judge_shift","matid_judgement",0);
   Hooks.Judge(set,p,"judge-right","judge_space","judgement_line_alt",5);
   NativeJudgementPresentation.SkyLine(set,p);
   NativeKeyLabels.Build(renderer,p);
   BuildTriangles(set,renderer,p,near,far,half);
   // Near longitudinal authoring details are omitted; beat grid and judgement guides remain.
   foreach(var n in new[]{"floor","detail","glow"})NativeVisualPlugin.Show(NativeVisualPlugin.Get(renderer,n),false);
  }catch(Exception e){NativeVisualPlugin.Instance.Error(e);foreach(var n in new[]{"floor","detail","glow"})NativeVisualPlugin.Show(NativeVisualPlugin.Get(renderer,n),true);}
  set.End();
 }
 private static void Backing(VisualSet set,object p,float near,float far,float half,float side){
  var s=set.Take("backing",NativeVisualPlugin.Mat("matid_notesingle"));Opaque(s.Material);s.Material.SetTexture("_BaseMap",Texture2D.whiteTexture);s.Material.SetTexture("_MainTex",Texture2D.whiteTexture);s.Material.SetColor("_BaseColor",Color.black);s.Material.SetColor("_Color",Color.black);
  var surface=NativeVisualPlugin.T("StageSpace").GetMethod("Surface",NativeVisualPlugin.F);var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var x=new[]{-half-side,-half,half,half+side};
  // A side backing would turn native wall alpha into black instead of letting
  // the background show through. Keep the approved central backing only.
  for(int i=1;i<2;i++){int b=v.Count;foreach(var point in new[]{new Vector2(x[i],near),new Vector2(x[i+1],near),new Vector2(x[i+1],far),new Vector2(x[i],far)}){v.Add((Vector3)surface.Invoke(null,new object[]{p,point.x,point.y,-.04f}));uv.Add(Vector2.zero);}t.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});}
  s.Mesh.Clear();s.Mesh.SetVertices(v);s.Mesh.SetUVs(0,uv);s.Mesh.SetTriangles(t,0);s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();
 }
 private static void GapShadows(VisualSet set,object p,float near,float far,float half){
  var s=set.Take("gap-shadow",NativeVisualPlugin.Mat("matid_notesingle"));Opaque(s.Material);
  s.Material.SetTexture("_BaseMap",Texture2D.whiteTexture);s.Material.SetTexture("_MainTex",Texture2D.whiteTexture);s.Material.SetColor("_BaseColor",Color.black);s.Material.SetColor("_Color",Color.black);
  var surface=NativeVisualPlugin.T("StageSpace").GetMethod("Surface",NativeVisualPlugin.F);var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();float width=half*.02f;
  // A narrow backing under each hinge closes relief/inset gaps. The six
  // original lane meshes, positions and UVs are never moved or stretched.
  foreach(int sign in new[]{-1,1})for(int segment=0;segment<2;segment++){
   float a=sign*half+(segment-1)*width,b=a+width;int start=vertices.Count;
   foreach(var point in new[]{new Vector2(a,near),new Vector2(b,near),new Vector2(b,far),new Vector2(a,far)}){vertices.Add((Vector3)surface.Invoke(null,new object[]{p,point.x,point.y,-.04f}));uv.Add(Vector2.zero);}
   triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
  }
  s.Mesh.Clear();s.Mesh.SetVertices(vertices);s.Mesh.SetUVs(0,uv);s.Mesh.SetTriangles(triangles,0);s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();
 }
 internal static Vector3 MapKeyPoint(object profile,Vector3 native,int lane){
  float half=NativeVisualPlugin.N(profile,"centralHalfWidth"),unit=half/.8f,z=(native.z+1.5f)*unit;
  if(lane!=0&&lane!=5)return new Vector3(native.x*unit,native.y*unit,z);
  // Native side lane is a 45-degree plane, .45 world units across its slope.
  // Express the authored point in its tangent/normal basis, then map the
  // tangent to the actual Studio side plane. Retain the native normal offset.
  float sign=lane==0?-1:1,run=NativeVisualPlugin.N(profile,"sideRun"),rise=NativeVisualPlugin.N(profile,"sideRise"),inv=.7071067811865475f;
  var relative=native-new Vector3(sign*.8f,0,0);float across=(sign*relative.x+relative.y)*inv/.45f,normal=(-sign*relative.x+relative.y)*inv*unit;
  var point=new Vector3(sign*(half+across*run),across*rise,z);return point+new Vector3(-sign*rise,run,0).normalized*normal;
 }
 private static Material KeyPanel(VisualSet set,object p,int lane){
  Texture2D texture;if(!NativeVisualPlugin.Textures.TryGetValue("keybind-indicators-v2-default",out texture))throw new Exception("Native key panel texture missing");
  var s=set.Take("key"+lane,KeyMaterial());s.Material.SetTexture("_MainTex",texture);
  // GameScene: UnpressedKeybind (quad) -> Keybind3DContainer -> lane ->
  // MasterScenev10; remove GameCamera rotation, then map native .8 half-width
  // and the native z=-1.5 judgment origin into Studio's stage coordinates.
  float half=NativeVisualPlugin.N(p,"centralHalfWidth"),unit=half/.8f;
  var localScale=new Vector3(1.8042196035385132f,2.4356961250305176f,1f);
  bool side=lane==0||lane==5;bool left=lane==0;
  var parentScale=side?new Vector3(.25f,.17507004737854004f,.17507004737854004f):new Vector3(.2f,.1400560438632965f,.1400560438632965f);
  var rotation=side?(left?new Quaternion(.6532814502716064f,-.27059799432754517f,-.27059799432754517f,.6532814502716064f):new Quaternion(.6532815098762512f,.27059805393218994f,.27059805393218994f,.6532815098762512f)):new Quaternion(.7071067690849304f,0,0,.7071067690849304f);
  float[] centers={0,-.5149999856948853f,-.17499999701976776f,.17499999701976776f,.5149999856948853f,0};
  var center=side?(left?new Vector3(-.8080060482025146f-.1668769121170044f,.6152811050415039f-.6153978705406189f+.16863572597503662f,9.411811828613281f-5.999999046325684f-4.829999923706055f):new Vector3(.8122532963752747f+.16699887812137604f,.6152811050415039f-.615189790725708f+.16851340234279633f,9.411811828613281f-5.999999046325684f-4.829999923706055f)):new Vector3(centers[lane],.16863572597503662f,9.411811828613281f-5.999999046325684f-5.010000228881836f);
  var quad=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};var v=new Vector3[4];
  // Shift the complete indicator toward the outside band of the
  // judgement line, so the outer bracket meets its translucent band.
  // Scale with the stage width and keep the native quad and UVs intact.
  for(int i=0;i<4;i++){var native=center+rotation*Vector3.Scale(parentScale,Vector3.Scale(localScale,quad[i]));v[i]=MapKeyPoint(p,native,lane);if(side)v[i].z+=.008f*unit;}
  s.Mesh.Clear();s.Mesh.vertices=v;s.Mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};s.Mesh.colors=Enumerable.Repeat(Color.white,4).ToArray();s.Mesh.triangles=new[]{0,2,1,0,3,2};s.Mesh.RecalculateNormals();s.Mesh.RecalculateBounds();
  return s.Material;
 }
 private static void KeepFrontLines(VisualSet set,object renderer,object p){
  var batch=NativeVisualPlugin.Get(renderer,"detail");var vertices=(List<Vector3>)NativeVisualPlugin.Get(batch,"Vertices");var triangles=(List<int>)NativeVisualPlugin.Get(batch,"Triangles");var colors=(List<Color>)NativeVisualPlugin.Get(batch,"Colors");var uv=(List<Vector2>)NativeVisualPlugin.Get(batch,"Uvs");float half=NativeVisualPlugin.N(p,"centralHalfWidth");var indices=new List<int>();
  for(int i=0;i<triangles.Count;i+=3){var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];if(Mathf.Min(a.z,b.z,c.z)>=.01f&&Mathf.Max(a.z,b.z,c.z)<=3.81f&&Mathf.Max(a.x,b.x,c.x)-Mathf.Min(a.x,b.x,c.x)<.02f&&Mathf.Max(Mathf.Abs(a.x),Mathf.Abs(b.x),Mathf.Abs(c.x))<half)indices.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});}
  var s=set.Take("front-lines",NativeVisualPlugin.Obj(batch).GetComponent<MeshRenderer>().sharedMaterial);s.Mesh.Clear();s.Mesh.SetVertices(vertices);s.Mesh.SetTriangles(indices,0);if(colors.Count==vertices.Count)s.Mesh.SetColors(colors);if(uv.Count==vertices.Count)s.Mesh.SetUVs(0,uv);s.Mesh.RecalculateBounds();
 }
 private static void KeepSkyGuide(VisualSet set,object renderer,object p){
  var batch=NativeVisualPlugin.Get(renderer,"glow");var vertices=(List<Vector3>)NativeVisualPlugin.Get(batch,"Vertices");var triangles=(List<int>)NativeVisualPlugin.Get(batch,"Triangles");var colors=(List<Color>)NativeVisualPlugin.Get(batch,"Colors");var uv=(List<Vector2>)NativeVisualPlugin.Get(batch,"Uvs");
  var s=set.Take("sky-guide",NativeVisualPlugin.Obj(batch).GetComponent<MeshRenderer>().sharedMaterial);float height=NativeVisualPlugin.N(p,"skyHeight")-.06f;var indices=new List<int>();for(int i=0;i<triangles.Count;i+=3)if(vertices[triangles[i]].y>=height&&vertices[triangles[i+1]].y>=height&&vertices[triangles[i+2]].y>=height){indices.Add(triangles[i]);indices.Add(triangles[i+1]);indices.Add(triangles[i+2]);}
  s.Mesh.Clear();s.Mesh.SetVertices(vertices);s.Mesh.SetTriangles(indices,0);if(colors.Count==vertices.Count)s.Mesh.SetColors(colors);if(uv.Count==vertices.Count)s.Mesh.SetUVs(0,uv);s.Mesh.RecalculateBounds();
 }
}
}
