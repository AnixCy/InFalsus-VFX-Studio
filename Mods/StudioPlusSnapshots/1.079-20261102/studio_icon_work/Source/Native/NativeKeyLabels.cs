using System;
using System.IO;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using InFalsusStudio;
namespace InFalsusStudio.NativeVisual {
// The glyph atlas is generated from the original OTF. The six bounds and
// transforms below come from the paused native game's FastText/Letter chain.
internal static class NativeKeyLabels {
 internal static ConfigEntry<bool> Visible;
 static AssetBundle bundle;static Material material;static Texture2D atlas;static Camera view;static LabelPass pass;
 static readonly Dictionary<object,Mesh[]> meshes=new Dictionary<object,Mesh[]>();
 static readonly Vector4[] bounds={
  new Vector4(-.2476889491f,.1807248294f,.2523111105f,-.0255893562f),
  new Vector4(-.1367460787f,.3089843690f,.1366914064f,-.0437499993f),
  new Vector4(-.1016503870f,.3089843690f,.1034277380f,-.0437499993f),
  new Vector4(-.1129023284f,.3089843690f,.1277226508f,-.0437499993f),
  new Vector4(-.0815663934f,.3089843690f,.1016367227f,-.0437499993f),
  new Vector4(-.2501763105f,.1423137933f,.2498237193f,-.0201506261f)};
 static byte[] Resource(string name){using(var stream=typeof(NativeKeyLabels).Assembly.GetManifestResourceStream(name)){if(stream==null)throw new Exception("Native key text resource missing: "+name);using(var ms=new MemoryStream()){stream.CopyTo(ms);return ms.ToArray();}}}
 internal static void Init(ConfigFile cfg,Harmony harmony){
  Visible=cfg.Bind("Display","ShowKeyLabels",true,"Show native track key labels.");
  var load=typeof(AssetBundle).GetMethod("LoadFromMemory",new[]{typeof(byte[]),typeof(uint)});if(load==null)throw new Exception("Native key text loader missing");bundle=(AssetBundle)load.Invoke(null,new object[]{Resource("infstudioplus.keytext.bundle"),0u});if(!bundle)throw new Exception("Native key text shader bundle missing");
  var shader=bundle.LoadAsset<Shader>("native/key-text");if(!shader||!shader.isSupported)throw new Exception("Native key text shader unsupported");
  atlas=new Texture2D(2,2,TextureFormat.RGBA32,false,true);var loadImage=typeof(ImageConversion).GetMethod("LoadImage",new[]{typeof(Texture2D),typeof(byte[]),typeof(bool)});if(loadImage==null||!(bool)loadImage.Invoke(null,new object[]{atlas,Resource("infstudioplus.keytext.png"),true}))throw new Exception("Native key text atlas invalid");atlas.wrapMode=TextureWrapMode.Clamp;atlas.filterMode=FilterMode.Bilinear;
  material=new Material(shader);material.SetTexture("_MainTex",atlas);material.renderQueue=3001;
  pass=new LabelPass();RenderPipelineManager.beginCameraRendering+=Begin;
  harmony.Patch(typeof(StudioBootstrap).GetMethod("DrawKeyLabels",NativeVisualPlugin.F),prefix:new HarmonyMethod(typeof(NativeKeyLabels).GetMethod("OldLabels",NativeVisualPlugin.F)));
 }
 static bool OldLabels(){return Visible!=null&&Visible.Value&&!(NativeVisualPlugin.Active&&NativeVisualPlugin.StageEnabled.Value&&material);}
 internal static void View(Camera camera){view=camera;}
 static void Begin(ScriptableRenderContext context,Camera camera){if(camera!=view||!NativeVisualPlugin.Active||!NativeVisualPlugin.StageEnabled.Value||!Visible.Value||meshes.Count==0)return;pass.Prepare(camera);camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);}
 internal static void Build(object owner,object profile){
  Mesh[] row;if(!meshes.TryGetValue(owner,out row)){row=new Mesh[6];for(int i=0;i<6;i++){row[i]=new Mesh();row[i].name="Native key text "+i;}meshes.Add(owner,row);}
  for(int lane=0;lane<6;lane++){
   var b=bounds[lane];bool side=lane==0||lane==5;
   Vector3 origin=lane==0?new Vector3(-.9748829603f,.1685189592f,-1.4269406427f):lane==5?new Vector3(.9792521745f,.1686047173f,-1.4269406446f):new Vector3(new[]{0f,-.5149999857f,-.1749999970f,.1749999970f,.5149999857f,0f}[lane],.1686357254f,-1.6149941709f);
   Vector3 xaxis=lane==0?new Vector3(.1767767255f,-.1767766502f,0):lane==5?new Vector3(.1767766932f,.1767767052f,0):new Vector3(.200000003f,0,0);
   Vector3 yaxis=side?new Vector3(0,0,lane==0?.1750700221f:.1750700607f):new Vector3(0,0,.1400560391f);
   var local=new[]{new Vector2(b.x,b.w),new Vector2(b.z,b.w),new Vector2(b.z,b.y),new Vector2(b.x,b.y)};var vertices=new Vector3[4];
   for(int i=0;i<4;i++)vertices[i]=NativeTrack.MapKeyPoint(profile,origin+xaxis*local[i].x+yaxis*local[i].y,lane);
   float x0=(512+b.x*800)/1024,x1=(512+b.z*800)/1024;float top=lane*320+(320-(b.y-b.w)*800)*.5f,bottom=top+(b.y-b.w)*800;
   float y0=1-bottom/1920,y1=1-top/1920;
   row[lane].Clear();row[lane].vertices=vertices;row[lane].uv=new[]{new Vector2(x0,y0),new Vector2(x1,y0),new Vector2(x1,y1),new Vector2(x0,y1)};row[lane].triangles=new[]{0,2,1,0,3,2};row[lane].RecalculateBounds();
  }
 }
 static void Draw(Action<Mesh,Matrix4x4,Material> draw){foreach(var row in meshes.Values)foreach(var mesh in row)if(mesh)draw(mesh,Matrix4x4.identity,material);}
 internal static void Forget(object owner){Mesh[] row;if(!meshes.TryGetValue(owner,out row))return;foreach(var m in row)if(m)UnityEngine.Object.Destroy(m);meshes.Remove(owner);}
 internal static void Dispose(){RenderPipelineManager.beginCameraRendering-=Begin;foreach(var row in meshes.Values)foreach(var m in row)if(m)UnityEngine.Object.Destroy(m);meshes.Clear();if(pass!=null)pass.Release();pass=null;view=null;if(material)UnityEngine.Object.Destroy(material);material=null;if(atlas)UnityEngine.Object.Destroy(atlas);atlas=null;if(bundle)bundle.Unload(false);bundle=null;Visible=null;}
 sealed class LabelPass:ScriptableRenderPass {
  sealed class Data{internal TextureHandle source;}
  RTHandle source,handle;RenderTexture texture;
  internal LabelPass(){renderPassEvent=RenderPassEvent.AfterRenderingTransparents;ConfigureInput(ScriptableRenderPassInput.Color);}
  internal void Prepare(Camera camera){}
  public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData){
   var resources=frameData.Get<UniversalResourceData>();var input=resources.activeColorTexture;var desc=graph.GetTextureDesc(input);desc.name="Native key text complete scene";desc.clearBuffer=false;desc.msaaSamples=MSAASamples.None;var copy=graph.CreateTexture(desc);Data data;
   using(var builder=graph.AddRasterRenderPass<Data>("Native key text background",out data)){data.source=input;builder.UseTexture(input,AccessFlags.Read);builder.SetRenderAttachment(copy,0);builder.SetGlobalTextureAfterPass(copy,Shader.PropertyToID("_NativeLabelBackground"));builder.AllowPassCulling(false);builder.SetRenderFunc<Data>(delegate(Data d,RasterGraphContext ctx){Blitter.BlitTexture(ctx.cmd,d.source,new Vector4(1,1,0,0),0,false);});}
   using(var builder=graph.AddRasterRenderPass<Data>("Native key text",out data)){data.source=copy;builder.UseTexture(copy,AccessFlags.Read);builder.SetRenderAttachment(input,0,AccessFlags.ReadWrite);builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Read);builder.AllowPassCulling(false);builder.SetRenderFunc<Data>(delegate(Data d,RasterGraphContext ctx){Draw(delegate(Mesh mesh,Matrix4x4 matrix,Material m){ctx.cmd.DrawMesh(mesh,matrix,m,0,0);});});}
  }
  public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData data){source=view.GetUniversalAdditionalCameraData().scriptableRenderer.cameraColorTargetHandle;if(source==null||!source.rt)throw new Exception("Native key text needs the camera intermediate target");var desc=source.rt.descriptor;desc.depthBufferBits=0;desc.msaaSamples=1;desc.bindMS=false;desc.useMipMap=false;desc.autoGenerateMips=false;if(texture&&(texture.width!=desc.width||texture.height!=desc.height||texture.graphicsFormat!=desc.graphicsFormat))Release();if(!texture){texture=new RenderTexture(desc);texture.name="Native key text complete scene";texture.filterMode=FilterMode.Point;texture.Create();handle=RTHandles.Alloc(texture);}}
  public override void Execute(ScriptableRenderContext context,ref RenderingData data){if(handle==null)return;var cmd=CommandBufferPool.Get("Native key text");try{Blitter.BlitCameraTexture(cmd,source,handle);cmd.SetGlobalTexture("_NativeLabelBackground",handle.nameID);cmd.SetRenderTarget(source.nameID,view.GetUniversalAdditionalCameraData().scriptableRenderer.cameraDepthTargetHandle.nameID);Draw(delegate(Mesh mesh,Matrix4x4 matrix,Material m){cmd.DrawMesh(mesh,matrix,m,0,0);});context.ExecuteCommandBuffer(cmd);}finally{CommandBufferPool.Release(cmd);}}
  internal void Release(){if(handle!=null){handle.Release();handle=null;}if(texture){texture.Release();UnityEngine.Object.Destroy(texture);texture=null;}}
 }
}
}
