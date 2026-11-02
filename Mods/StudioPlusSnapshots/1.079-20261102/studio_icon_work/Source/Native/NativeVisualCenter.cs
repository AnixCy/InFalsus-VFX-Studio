// Port of GameScene BackgroundDiamond (10803), Game.dll BackgroundDiamond._H/_I.
// Native GPU programs and backing texture; no Unity ParticleSystem approximation.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace InFalsusStudio.NativeVisual {
internal sealed class CenterAnimationClock {
 internal double Phase {get;private set;}
 double previous=double.NaN;bool wasRunning;
 internal double Advance(double now,bool running){
  if(double.IsNaN(previous))Phase=now;
  else if(running&&wasRunning&&now>=previous)Phase+=now-previous;
  previous=now;wasRunning=running;return Phase;
 }
 internal void Reset(){previous=double.NaN;Phase=0;wasRunning=false;}
}
internal static class NativeCenter {
 private static GameObject output;
 private static Mesh quad,triangles,hexagon,rings;
 private static Material shape,blur,composite;
 private static RenderTexture mask,halfA,halfB,quarterA,quarterB;
 private static GraphicsBuffer clock;
 private static readonly Vector4[] clockData=new Vector4[9];
 private static Camera view;
 private static CommandBuffer commands;
 private static bool subscribed,failed;
 private static bool warned;
 private static readonly CenterAnimationClock animation=new CenterAnimationClock();
 private static double phase,paintedPhase=double.NaN;private static int paintedWidth,paintedHeight;
 internal static long Compositions;
 internal static void Draw(object boot){
  bool enabled=NativeVisualPlugin.Active&&NativeVisualPlugin.CenterEnabled.Value&&!failed;
  var transport=(InFalsusStudio.StudioTransport)NativeVisualPlugin.Get(boot,"transport");
  phase=InFalsusStudio.VideoRender.RenderClock.Active?InFalsusStudio.VideoRender.RenderClock.Elapsed:animation.Advance(Time.timeAsDouble,enabled&&(!NativeVisualPlugin.PauseCenterInEditor.Value||(transport!=null&&transport.Playing)));
  if(!enabled){if(output)output.SetActive(false);return;}
  try{
   Material source;
   if(!NativeVisualPlugin.Materials.TryGetValue("backgrounddiamond",out source)||!source||!source.shader||!source.shader.isSupported)throw new Exception("Central BackgroundDiamond unavailable; other modules remain active");
   if(!NativeVisualPlugin.Materials.TryGetValue("blur",out source)||!source||!source.shader||!source.shader.isSupported)throw new Exception("Central Blur unavailable; other modules remain active");
   var camera=(Camera)NativePresentation.Value(boot,"ViewCamera");if(!camera)return;
   if(!output)Create(boot);
   view=camera;output.SetActive(true);
   var profile=NativeVisualPlugin.Get(boot,"Profile");float far=NativeVisualPlugin.N(profile,"stageFar");
   var end=camera.WorldToViewportPoint(new Vector3(0,0,far));
   var bounds=NativeViewportBounds(camera.aspect);
   float depth=Mathf.Clamp(end.z+3,camera.nearClipPlane+.2f,camera.farClipPlane-.5f);
   // Native C2D layout uses a 3840 x 2160 reference viewport. Position and size
   // are independent of the track endpoint; depth only keeps track occlusion.
   quad.vertices=new[]{camera.ViewportToWorldPoint(new Vector3(bounds.xMin,bounds.yMin,depth)),camera.ViewportToWorldPoint(new Vector3(bounds.xMax,bounds.yMin,depth)),camera.ViewportToWorldPoint(new Vector3(bounds.xMax,bounds.yMax,depth)),camera.ViewportToWorldPoint(new Vector3(bounds.xMin,bounds.yMax,depth))};
   quad.RecalculateBounds();
  }catch(Exception e){Fail(e);}
 }
 // GameScene C2D 8617 -> container 9006 -> sprite 9927. CoreScene.Update
 // (RVA 0x6e3ed0) sets logical pixels to 3840 x 2160; _wi._jRA
 // (0x514d50) converts sizeInPixels * renderableScale to the sprite size.
 // Container top alignment: +0.5 of root height, offset +400 logical pixels.
 // Sprite top anchor: -0.5 of its 800-pixel height. These offsets cancel.
 internal static Rect NativeViewportBounds(float aspect){
  const float referenceWidth=3840f,referenceHeight=2160f;
  const float containerOffsetY=400f,spritePixels=100f,scaleX=12f,scaleY=8f;
  // Match the native 16:9 reference frame without altering the Studio camera.
  float fitY=Mathf.Min(1f,aspect/(referenceWidth/referenceHeight));
  float fitX=fitY*(referenceWidth/referenceHeight)/aspect;
  float width=spritePixels*scaleX/referenceWidth*fitX;
  float height=spritePixels*scaleY/referenceHeight*fitY;
  float containerY=.5f+containerOffsetY/referenceHeight;
  float centerY=.5f+(containerY-.5f*spritePixels*scaleY/referenceHeight)*fitY;
  return new Rect(.5f-width*.5f,centerY-height*.5f,width,height);
 }
 private static void Create(object boot){
  shape=new Material(NativeVisualPlugin.Mat("BackgroundDiamond"));
  blur=new Material(NativeVisualPlugin.Mat("Blur"));
  composite=new Material(NativeVisualPlugin.Mat("BackgroundDiamondOutput Variant"));
  if(shape.passCount!=4||blur.passCount!=5||!composite.HasProperty("_BlurredTexture"))throw new Exception("Native BackgroundDiamond shader interface changed");
  triangles=MakeTriangles();hexagon=MakeHexagon();rings=MakeRings();
  mask=Target("Native center mask",1200,800);halfA=Target("Native center half A",600,400);halfB=Target("Native center half B",600,400);quarterA=Target("Native center quarter A",300,200);quarterB=Target("Native center quarter B",300,200);
  clock=new GraphicsBuffer(GraphicsBuffer.Target.Constant,9,16);
  shape.SetConstantBuffer("UnityPerCamera",clock,0,144);
  blur.SetFloat("_VerticalBlur",.017f*.011f);blur.SetFloat("_HorizontalBlur",.017f*.011f);
  // Draw into the background before the shared Gamma capture. Central-floor
  // depth still masks it; the side walls compose over its captured pixels.
  composite.SetTexture("_BlurredTexture",mask);composite.renderQueue=2450;
  output=new GameObject("NativeVisual animated BackgroundDiamond");
  var stage=NativeVisualPlugin.Get(boot,"stage");output.layer=NativeVisualPlugin.Obj(NativeVisualPlugin.Get(stage,"floor")).layer;
  quad=new Mesh();quad.name="Native BackgroundDiamond output quad";
  quad.vertices=new Vector3[4];quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};quad.triangles=new[]{0,2,1,0,3,2};quad.RecalculateNormals();
  output.AddComponent<MeshFilter>().sharedMesh=quad;
  var renderer=output.AddComponent<MeshRenderer>();renderer.sharedMaterial=composite;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
  commands=new CommandBuffer();commands.name="Native BackgroundDiamond composition";
  RenderPipelineManager.beginCameraRendering+=Render;subscribed=true;
 }
 private static RenderTexture Target(string name,int width,int height){
  var t=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);t.name=name;t.useMipMap=false;t.autoGenerateMips=false;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;
  if(!t.Create()){UnityEngine.Object.Destroy(t);throw new Exception("Center render target creation failed: "+name);}return t;
 }
 private static Vector3 Corner(int i,float radius){float a=i*Mathf.PI/3f;return new Vector3((float)Math.Sin(a)*radius*(2f/3f),(float)Math.Cos(a)*radius,0);}
 private static Mesh MakeTriangles(){
  var v=new Vector3[300];var uv=new List<Vector3>(300);var ix=new int[300];var random=new System.Random();
  for(int i=0;i<100;i++){
   float radius=((float)random.NextDouble()*.5f+.02f)*.5f;
   double angle=(random.NextDouble()>.5?.5:1.5)*Math.PI;
   for(int j=0;j<3;j++){int k=i*3+j;double a=angle+j*(2*Math.PI/3);v[k]=new Vector3((float)Math.Sin(a)*radius*(2f/3f),(float)Math.Cos(a)*radius,0);uv.Add(new Vector3(i,i,i));ix[k]=k;}
  }
  var m=new Mesh();m.name="Native 100 triangle particles";m.vertices=v;m.SetUVs(1,uv);m.triangles=ix;m.RecalculateBounds();return m;
 }
 private static Mesh MakeHexagon(){
  var v=new Vector3[18];var ix=new int[18];
  for(int i=0;i<6;i++){int b=i*3;v[b]=Vector3.zero;v[b+1]=Corner(i,1);v[b+2]=Corner(i+1,1);ix[b]=b;ix[b+1]=b+1;ix[b+2]=b+2;}
  var m=new Mesh();m.name="Native hexagon radius 1";m.vertices=v;m.triangles=ix;m.RecalculateBounds();return m;
 }
 private static Mesh MakeRings(){
  var v=new Vector3[72];var uv=new List<Vector4>(72);var ix=new int[72];
  for(int r=0;r<2;r++)for(int i=0;i<6;i++){
   Vector3 innerA=Corner(i,.9f),innerB=Corner(i+1,.9f),outerA=Corner(i,1),outerB=Corner(i+1,1);
   Vector3 deltaA=outerA-innerA,deltaB=outerB-innerB;
   var points=new[]{innerB,innerA,outerA,outerA,outerB,innerB};var delta=new[]{deltaB,deltaA,Vector3.zero,Vector3.zero,Vector3.zero,deltaB};
   for(int j=0;j<6;j++){int b=r*36+i*6+j;v[b]=points[j];uv.Add(new Vector4(delta[j].x,delta[j].y,r,2.3f));ix[b]=b;}
  }
  var m=new Mesh();m.name="Native two expanding hexagon rings";m.vertices=v;m.SetUVs(0,uv);m.triangles=ix;m.RecalculateBounds();return m;
 }
 private static void Render(ScriptableRenderContext context,Camera camera){
  if(failed||camera!=view||!output||!output.activeSelf||!NativeVisualPlugin.Active||!NativeVisualPlugin.CenterEnabled.Value)return;
  try{
   bool targetsReady=mask.IsCreated()&&halfA.IsCreated()&&halfB.IsCreated()&&quarterA.IsCreated()&&quarterB.IsCreated();
   if(targetsReady&&phase==paintedPhase&&camera.pixelWidth==paintedWidth&&camera.pixelHeight==paintedHeight)return;
   if(!targetsReady)foreach(var target in new[]{mask,halfA,halfB,quarterA,quarterB})if(!target.IsCreated()&&!target.Create())throw new Exception("Center render target restoration failed");
   float t=(float)phase;clockData[0]=new Vector4(t/20,t,t*2,t*3);clock.SetData(clockData);
   var oldScreen=Shader.GetGlobalVector("_ScreenParams");var oldScale=Shader.GetGlobalVector("_BlitScaleBias");var oldTexel=Shader.GetGlobalVector("_BlitTexture_TexelSize");var oldTexture=Shader.GetGlobalTexture("_BlitTexture");
   commands.Clear();commands.SetGlobalVector("_ScreenParams",new Vector4(camera.pixelWidth,camera.pixelHeight,1+1f/camera.pixelWidth,1+1f/camera.pixelHeight));
   commands.SetRenderTarget(mask);commands.SetViewport(new Rect(0,0,1200,800));commands.ClearRenderTarget(false,true,Color.clear);
   // Native callback <>c._j, RVA 0x47ade0: white triangles / rings /
   // black triangle cutouts / solid hexagon. Vertex shaders use clip space.
   commands.DrawMesh(triangles,Matrix4x4.identity,shape,0,0);
   commands.DrawMesh(rings,Matrix4x4.identity,shape,0,3);
   commands.DrawMesh(triangles,Matrix4x4.identity,shape,0,1);
   commands.DrawMesh(hexagon,Matrix4x4.identity,shape,0,2);
   Blit(mask,halfA,0);Blit(halfA,halfB,1);Blit(halfB,quarterA,0);Blit(quarterA,quarterB,1);Blit(quarterB,halfA,0);Blit(halfA,halfB,1);Blit(halfB,mask,1);
   commands.SetGlobalVector("_ScreenParams",oldScreen);commands.SetGlobalVector("_BlitScaleBias",oldScale);commands.SetGlobalVector("_BlitTexture_TexelSize",oldTexel);commands.SetGlobalTexture("_BlitTexture",oldTexture?oldTexture:Texture2D.blackTexture);
   // Execute before URP sets the main camera targets/viewport; no camera stack,
   // projection matrix or gameplay globals are modified by the center pass.
   Graphics.ExecuteCommandBuffer(commands);
   paintedPhase=phase;paintedWidth=camera.pixelWidth;paintedHeight=camera.pixelHeight;Compositions++;
  }catch(Exception e){Fail(e);}
 }
 private static void Blit(RenderTexture source,RenderTexture dest,int pass){
  commands.SetRenderTarget(dest);commands.SetViewport(new Rect(0,0,dest.width,dest.height));commands.SetGlobalTexture("_BlitTexture",source);commands.SetGlobalVector("_BlitTexture_TexelSize",new Vector4(1f/source.width,1f/source.height,source.width,source.height));commands.SetGlobalVector("_BlitScaleBias",new Vector4(1,1,0,0));commands.DrawProcedural(Matrix4x4.identity,blur,pass,MeshTopology.Triangles,3,1);
 }
 private static void Fail(Exception e){failed=true;if(output)output.SetActive(false);if(!warned){warned=true;NativeVisualPlugin.Instance.ReportCenterFailure(e);}}
 internal static void Dispose(){
  if(subscribed){RenderPipelineManager.beginCameraRendering-=Render;subscribed=false;}
  if(commands!=null){commands.Release();commands=null;}if(clock!=null){clock.Dispose();clock=null;}
  foreach(var t in new[]{mask,halfA,halfB,quarterA,quarterB})if(t){t.Release();UnityEngine.Object.Destroy(t);}
  foreach(var o in new UnityEngine.Object[]{output,quad,triangles,hexagon,rings,shape,blur,composite})if(o)UnityEngine.Object.Destroy(o);
  output=null;quad=triangles=hexagon=rings=null;shape=blur=composite=null;mask=halfA=halfB=quarterA=quarterB=null;view=null;failed=false;warned=false;
  animation.Reset();phase=0;paintedPhase=double.NaN;paintedWidth=paintedHeight=0;Compositions=0;
 }
}
public sealed partial class NativeVisualPlugin {
 internal void ReportCenterFailure(Exception e){Logger.LogError("Native central background failed (other visual modules retained): "+e);}
}
}
