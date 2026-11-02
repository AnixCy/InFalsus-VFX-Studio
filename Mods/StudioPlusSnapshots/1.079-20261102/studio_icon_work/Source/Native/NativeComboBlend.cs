using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
namespace InFalsusStudio.NativeVisual {
// Native FastText blends in Gamma (original PlayerSettings=0). The editor
// renders in Linear. Capture the stage before notes, reproduce that Gamma
// blend locally for numerals and side walls, then return to scene space.
internal static class NativeComboBlend {
 static AssetBundle bundle;
 internal static Shader Shader;
 static Camera view;
 static Material comboMaterial;
 static bool wanted,subscribed;
 static CopyStagePass pass;
 internal static void Init(string presentationFolder){
  string path=Path.Combine(Path.GetDirectoryName(presentationFolder),"Assets","combo-gamma.bundle");
  bundle=AssetBundle.LoadFromFile(path);if(!bundle)throw new Exception("Native combo color compatibility bundle missing");
  Shader=bundle.LoadAsset<Shader>("native/compatibility/combo");if(!Shader||!Shader.isSupported)throw new Exception("Native combo Gamma blend shader unsupported");
  pass=new CopyStagePass();RenderPipelineManager.beginCameraRendering+=Begin;subscribed=true;
 }
 internal static void View(Camera camera){view=camera;if(camera)SetViewport(camera.pixelWidth,camera.pixelHeight);}
 internal static void SetViewport(int width,int height){if(comboMaterial)comboMaterial.SetVector("_NativeComboViewport",new Vector4(width,height,QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,0));NativeTrack.WallViewport(width,height);}
 internal static void Bind(Material material){comboMaterial=material;if(view)SetViewport(view.pixelWidth,view.pixelHeight);}
 internal static void Request(bool active){wanted=active;}
 static void Begin(ScriptableRenderContext context,Camera camera){
  if(camera!=view||!NativeVisualPlugin.Active||!((wanted&&NativeVisualPlugin.ComboEnabled.Value)||NativeTrack.NeedsGammaCapture))return;
  pass.Prepare(camera);camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
 }
 sealed class CopyStagePass:ScriptableRenderPass {
  sealed class CopyData {internal TextureHandle source;}
  RenderTexture texture,keyTexture;
  RTHandle handle,keyHandle;
  RTHandle source;
  internal void Prepare(Camera camera){
   int width=camera.pixelWidth,height=camera.pixelHeight;
   if(keyTexture&&(keyTexture.width!=width||keyTexture.height!=height)){keyHandle.Release();UnityEngine.Object.Destroy(keyTexture);keyTexture=null;keyHandle=null;}
   if(!keyTexture){keyTexture=new RenderTexture(width,height,0,RenderTextureFormat.ARGBHalf);keyTexture.name="Native key backdrop";keyTexture.filterMode=FilterMode.Point;keyTexture.wrapMode=TextureWrapMode.Clamp;keyTexture.Create();keyHandle=RTHandles.Alloc(keyTexture);}
   NativeTrack.KeyBackground(keyTexture);
  }
  internal CopyStagePass(){renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;ConfigureInput(ScriptableRenderPassInput.Color);}
  public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData){
   var resources=frameData.Get<UniversalResourceData>();var input=resources.activeColorTexture;var desc=graph.GetTextureDesc(input);desc.name="Native combo stage color";desc.clearBuffer=false;desc.msaaSamples=MSAASamples.None;
   var output=graph.CreateTexture(desc);
   if(comboMaterial)comboMaterial.SetVector("_NativeComboViewport",new Vector4(desc.width,desc.height,QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,0));
   NativeTrack.WallViewport(desc.width,desc.height);
   CopyData data;
   using(var builder=graph.AddRasterRenderPass<CopyData>("Native combo Gamma background",out data)){
    data.source=input;builder.UseTexture(input,AccessFlags.Read);builder.SetRenderAttachment(output,0);builder.SetGlobalTextureAfterPass(output,UnityEngine.Shader.PropertyToID("_NativeComboBackground"));builder.AllowPassCulling(false);
    builder.SetRenderFunc<CopyData>(delegate(CopyData d,RasterGraphContext context){Blitter.BlitTexture(context.cmd,d.source,new Vector4(1,1,0,0),0,false);});
   }
   // Keys need the same background as their transparent draw: opaque stage
   // plus the two side walls. Build that backdrop without changing scene order.
   var keyOutput=graph.ImportTexture(keyHandle);
   using(var builder=graph.AddRasterRenderPass<CopyData>("Native key Gamma background",out data)){
    data.source=input;builder.UseTexture(input,AccessFlags.Read);builder.UseTexture(output,AccessFlags.Read);builder.SetRenderAttachment(keyOutput,0);builder.SetGlobalTextureAfterPass(keyOutput,UnityEngine.Shader.PropertyToID("_NativeKeyBackground"));builder.AllowPassCulling(false);
    builder.SetRenderFunc<CopyData>(delegate(CopyData d,RasterGraphContext context){Blitter.BlitTexture(context.cmd,d.source,new Vector4(1,1,0,0),0,false);NativeTrack.DrawKeyBackdrop(delegate(Mesh mesh,Matrix4x4 matrix,Material material){context.cmd.DrawMesh(mesh,matrix,material,0,0);});});
   }
  }
  public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData data){
   source=view.GetUniversalAdditionalCameraData().scriptableRenderer.cameraColorTargetHandle;
   if(source==null||!source.rt)throw new Exception("Combo color capture requires the camera intermediate target");
   var desc=source.rt.descriptor;desc.depthBufferBits=0;desc.msaaSamples=1;desc.bindMS=false;desc.useMipMap=false;desc.autoGenerateMips=false;
   if(texture&&(texture.width!=desc.width||texture.height!=desc.height||texture.graphicsFormat!=desc.graphicsFormat)){Release();}
   if(!texture){texture=new RenderTexture(desc);texture.name="Native combo background before notes";texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;if(!texture.Create())throw new Exception("Combo background allocation failed");handle=RTHandles.Alloc(texture);}
   if(!keyTexture){keyTexture=new RenderTexture(desc);keyTexture.name="Native key background including side walls";keyTexture.filterMode=FilterMode.Point;keyTexture.wrapMode=TextureWrapMode.Clamp;if(!keyTexture.Create())throw new Exception("Key background allocation failed");keyHandle=RTHandles.Alloc(keyTexture);}
   NativeTrack.KeyBackground(keyTexture);
   if(comboMaterial)comboMaterial.SetVector("_NativeComboViewport",new Vector4(texture.width,texture.height,QualitySettings.activeColorSpace==ColorSpace.Linear?1:0,0));
   NativeTrack.WallViewport(texture.width,texture.height);
  }
  public override void Execute(ScriptableRenderContext context,ref RenderingData data){
   if(!texture||handle==null)return;
   var cmd=CommandBufferPool.Get("Native combo Gamma background");
   try{Blitter.BlitCameraTexture(cmd,source,handle);cmd.SetGlobalTexture("_NativeComboBackground",handle.nameID);context.ExecuteCommandBuffer(cmd);cmd.Clear();Blitter.BlitCameraTexture(cmd,source,keyHandle);cmd.SetRenderTarget(keyHandle.nameID);NativeTrack.DrawKeyBackdrop(delegate(Mesh mesh,Matrix4x4 matrix,Material material){cmd.DrawMesh(mesh,matrix,material,0,0);});cmd.SetGlobalTexture("_NativeKeyBackground",keyHandle.nameID);context.ExecuteCommandBuffer(cmd);}finally{CommandBufferPool.Release(cmd);}
  }
  internal void Release(){if(keyHandle!=null){keyHandle.Release();keyHandle=null;}if(keyTexture){keyTexture.Release();UnityEngine.Object.Destroy(keyTexture);keyTexture=null;}if(handle!=null){handle.Release();handle=null;}if(texture){texture.Release();UnityEngine.Object.Destroy(texture);texture=null;}}
 }
 internal static void Dispose(){if(subscribed){RenderPipelineManager.beginCameraRendering-=Begin;subscribed=false;}wanted=false;view=null;if(pass!=null){pass.Release();pass=null;}Shader=null;if(bundle){bundle.Unload(false);bundle=null;}}
}
}

