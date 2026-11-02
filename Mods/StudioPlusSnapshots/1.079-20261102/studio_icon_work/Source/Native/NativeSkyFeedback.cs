using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace InFalsusStudio.NativeVisual {
 // Native JudgementLineMeshEffect._Q/Update/_R: staggered geometry,
 // SV_VertexID / 3 structured opacity, fullscale and seven GPU blur passes.
 internal static class NativeSkyFeedback {
  internal const int Rows=8,Columns=90,PerRow=Columns*2+1,Count=Rows*PerRow;
  internal static float TailOpacity(double now,double end){return (float)Math.Max(0,Math.Min(1,1-(now-end)/300));}
  // Track.Update, retail RVA 0x66f530..0x66f5e9: the two sine waves modulate
  // both alpha AND the shader's white-core radius. A constant 1 makes it thicker.
  internal static float ContactOpacity(double now){float t=(float)(now*.001);return 1-.1f*((Mathf.Sin(t*109)*.5f+.5f)+(Mathf.Sin(t*34)*.5f+.5f));}
  internal static float Opacity(double now,double end){return now<end?ContactOpacity(now):TailOpacity(now,end);}
  // Scene 2380 -> JUDGE_SKY 3751: zero local offset; 2 x .144 unit quad.
  // GameCamera is the stage parent rotated -24.33 degrees; CoreScene's actual
  // CameraPerspective is unrotated. Include BOTH stage and quad rotations.
  internal static Vector3[] GlowVertices(float halfWidth,float skyHeight,Quaternion cameraRotation){
   float scale=halfWidth/.8f;var stage=cameraRotation*new Quaternion(-.21072766184806824f,0,0,.9775448441505432f);var rotation=stage*new Quaternion(.21072766184806824f,0,0,.9775448441505432f);
   Vector3 center=new Vector3(0,skyHeight,0),a=Vector3.right*scale,b=rotation*Vector3.up*(.072f*scale);
   return new[]{center-a-b,center+a-b,center+a+b,center-a+b};
  }
  internal static float Center(double left,double right){return (float)((Math.Max(0,Math.Min(1,left))+Math.Max(0,Math.Min(1,right)))*.5);}
  internal static float Width(double left,double right){return (float)Math.Max(0,Math.Min(1,right)-Math.Max(0,left));}
  internal static Vector3[] GridVertices(){
   var v=new Vector3[Count*3];int k=0;float dx=1f/Columns,dy=1f/Rows;
   for(int row=0;row<Rows;row++)for(int tri=0;tri<PerRow;tri++){
    float top=1-row*dy,bottom=top-dy,x=tri*dx*.5f;
    float a=(((tri&1)!=0)^((row&1)!=0))?bottom:top,b=a==top?bottom:top;
    v[k++]=new Vector3(x,a,0);v[k++]=new Vector3(x+dx*.5f,b,0);v[k++]=new Vector3(x+dx,a,0);
   }return v;
  }
  internal static float RowBias(int i){return Math.Abs((Rows-1)*.5f-i/PerRow)/(Rows-1);}
  internal static float TriangleX(int i){return (float)(i%PerRow)/PerRow;}
  // JudgementLineMeshEffect._Q (0x4763bf..0x476546): only height is
  // reduced from 2048 to 512. Width stays 2048 for both output textures.
  internal static int[] TargetSize(int cameraWidth){int width=Math.Max(128,(int)(2048d*Math.Max(1,cameraWidth)/3840d));int height=Math.Max(32,(int)(512d*Math.Max(1,cameraWidth)/3840d));return new[]{width,height};}
  internal struct Region {internal double Left,Right,Start,End;internal Region(double left,double right,double start,double end){Left=left;Right=right;Start=start;End=end;}}
  internal static bool Contains(IList<Region> regions,double x,double time){foreach(var r in regions)if(time>=r.Start&&(time<r.End||r.Start==r.End&&time==r.End)&&x>=r.Left&&x<=r.Right)return true;return false;}
  sealed class Grid {
   internal readonly Mesh Mesh;internal readonly Material Shape,Blur;internal readonly GraphicsBuffer Levels;
   internal RenderTexture Full,Sharp,Soft,HalfA,HalfB,QuarterA,QuarterB;internal Camera Camera;internal Slot Slot;
   readonly float[] levels=new float[Count];readonly double[] fired=new double[Count];
   long tick=long.MinValue;double painted=double.NaN;int fullSize;
   internal Grid(Slot slot){
    Slot=slot;Shape=new Material(NativeVisualPlugin.Mat("TestMeshJudgementLineEffect"));Blur=new Material(NativeVisualPlugin.Mat("Blur"));
    Levels=new GraphicsBuffer(GraphicsBuffer.Target.Structured,Count,4);Shape.SetBuffer("_TriId_To_Opacity",Levels);
    Shape.SetInteger("_Rows",Rows);Shape.SetInteger("_Columns",Columns);
    Mesh=new Mesh();Mesh.name="Native Sky 8 x 181 staggered triangles";Mesh.vertices=GridVertices();
    var indices=new int[Count*3];for(int i=0;i<indices.Length;i++)indices[i]=i;Mesh.triangles=indices;Mesh.RecalculateBounds();
    Blur.SetFloat("_VerticalBlur",.0001f);Blur.SetFloat("_HorizontalBlur",.0001f);
   }
   internal void Size(Camera camera){
    Camera=camera;var size=TargetSize(camera.pixelWidth);if(size[0]==fullSize&&Sharp&&Sharp.height==size[1])return;
    ReleaseTargets();fullSize=size[0];int width=size[0],height=size[1];
    Full=Target("Sky fullscale",width,width);Sharp=Target("Sky downscaled",width,height);Soft=Target("Sky blurred",width,height);
    HalfA=Target("Sky half A",width/2,height/2);HalfB=Target("Sky half B",width/2,height/2);QuarterA=Target("Sky quarter A",width/4,height/4);QuarterB=Target("Sky quarter B",width/4,height/4);
    Slot.Material.SetTexture("_DownscaledGrid",Sharp);Slot.Material.SetTexture("_DownscaledBlurredGrid",Soft);
   }
   internal void Update(double now,double left,double right,double start,double end,IList<Region> regions){
    if(now==painted)return;long current=(long)Math.Floor(now*.06);
    if(tick==long.MinValue||current<tick||current-tick>30){Array.Clear(levels,0,Count);for(int i=0;i<Count;i++)fired[i]=double.NegativeInfinity;tick=current-30;}
    for(long step=tick+1;step<=current;step++){
     float time=(float)(step/60d);
     for(int i=0;i<Count;i++){
      float x=TriangleX(i),bias=RowBias(i);bool inside=regions==null?step/60d*1000>=start&&step/60d*1000<end&&x>=left&&x<=right:Contains(regions,x,step/60d*1000);
      // Native Update 474d4b..474d63: xmm0 = id + time*19.5,
      // xmm1 = time*17.7. PerlinNoise is not symmetric in its arguments.
      float noise=Mathf.PerlinNoise(i+time*19.5f,time*17.7f)-bias*.5f;
      if(inside&&noise>.7f&&noise-bias>.2f){fired[i]=time;levels[i]=1;}
      else{double age=time-fired[i];levels[i]=age<.1?1:Mathf.Clamp01(1-(float)age*(inside?2.5f:5f));}
     }
    }tick=current;Levels.SetData(levels);painted=now;
   }
   void ReleaseTargets(){foreach(var t in new[]{Full,Sharp,Soft,HalfA,HalfB,QuarterA,QuarterB})if(t){t.Release();UnityEngine.Object.Destroy(t);}}
   internal void Dispose(){ReleaseTargets();Levels.Dispose();UnityEngine.Object.Destroy(Mesh);UnityEngine.Object.Destroy(Shape);UnityEngine.Object.Destroy(Blur);}
  }
  static readonly Dictionary<object,Dictionary<Slot,Grid>> grids=new Dictionary<object,Dictionary<Slot,Grid>>();
  static CommandBuffer commands;static bool subscribed,failed;
  static RenderTexture Target(string name,int width,int height){
   var t=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);t.name=name;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.useMipMap=false;t.autoGenerateMips=false;
   if(!t.Create()){UnityEngine.Object.Destroy(t);throw new Exception("Sky target failed: "+name);}return t;
  }
  internal static void Draw(object renderer,double left,double right,float opacity,double now,double start=double.NegativeInfinity,double end=double.PositiveInfinity,IList<Region> regions=null){
   if(failed||Width(left,right)<=0||opacity<=0)return;
   var slot=NativeVisualPlugin.Visuals(renderer).Take("skyglow",NativeVisualPlugin.Mat("SkyJudgementLineGlow Variant"));
   Dictionary<Slot,Grid> row;if(!grids.TryGetValue(renderer,out row)){row=new Dictionary<Slot,Grid>();grids.Add(renderer,row);}Grid grid;
   if(!row.TryGetValue(slot,out grid)){grid=new Grid(slot);row.Add(slot,grid);}
   var profile=NativeVisualPlugin.Get(renderer,"p");var camera=(Camera)NativeVisualPlugin.Get(renderer,"camera");if(!camera)return;
   grid.Size(camera);grid.Update(now,Math.Max(0,left),Math.Min(1,right),start,end,regions);
   slot.Mesh.Clear();slot.Mesh.vertices=GlowVertices(NativeVisualPlugin.N(profile,"centralHalfWidth"),NativeVisualPlugin.N(profile,"skyHeight"),camera.transform.rotation);slot.Mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};slot.Mesh.triangles=new[]{0,2,1,0,3,2};slot.Mesh.RecalculateNormals();slot.Mesh.RecalculateBounds();
   slot.Material.SetFloat("_CenterX",Center(left,right));slot.Material.SetFloat("_Width",Width(left,right));slot.Material.SetFloat("_Opacity",opacity);slot.Material.SetFloat("_Cull",0);
   if(!subscribed){commands=new CommandBuffer();commands.name="Native Sky grid composition";RenderPipelineManager.beginCameraRendering+=Render;subscribed=true;}
  }
  static void Blit(RenderTexture src,RenderTexture dst,Material blur,int pass){
   commands.SetRenderTarget(dst);commands.SetViewport(new Rect(0,0,dst.width,dst.height));commands.SetGlobalTexture("_BlitTexture",src);commands.SetGlobalVector("_BlitTexture_TexelSize",new Vector4(1f/src.width,1f/src.height,src.width,src.height));commands.SetGlobalVector("_BlitScaleBias",new Vector4(1,1,0,0));commands.DrawProcedural(Matrix4x4.identity,blur,pass,MeshTopology.Triangles,3,1);
  }
  static void Render(ScriptableRenderContext context,Camera camera){
   if(!NativeVisualPlugin.Active||NativeVisualPlugin.HitStyle.Value!=HitEffectStyle.Full)return;
   var scale=Shader.GetGlobalVector("_BlitScaleBias");var texel=Shader.GetGlobalVector("_BlitTexture_TexelSize");var texture=Shader.GetGlobalTexture("_BlitTexture");var screen=Shader.GetGlobalVector("_ScreenParams");
   try{
    commands.Clear();
    // Native CustomEffects/Blur scales its offsets by _ScreenParams / source
    // texture size. beginCameraRendering precedes URP camera setup, so supply
    // this camera's dimensions explicitly and restore the inherited globals.
    float width=Math.Max(1,camera.pixelWidth),height=Math.Max(1,camera.pixelHeight);commands.SetGlobalVector("_ScreenParams",new Vector4(width,height,1+1/width,1+1/height));
    foreach(var row in grids.Values)foreach(var g in row.Values){
     if(g.Camera!=camera||!g.Slot.Used||!g.Slot.Object.activeInHierarchy)continue;
     commands.SetRenderTarget(g.Full);commands.SetViewport(new Rect(0,0,g.Full.width,g.Full.height));commands.ClearRenderTarget(false,true,Color.clear);commands.DrawMesh(g.Mesh,Matrix4x4.identity,g.Shape,0,0);
     Blit(g.Full,g.Sharp,g.Blur,0);Blit(g.Sharp,g.HalfA,g.Blur,1);Blit(g.HalfA,g.QuarterA,g.Blur,0);Blit(g.QuarterA,g.QuarterB,g.Blur,1);Blit(g.QuarterB,g.HalfA,g.Blur,0);Blit(g.HalfA,g.HalfB,g.Blur,1);Blit(g.HalfB,g.Soft,g.Blur,1);
    }
    commands.SetGlobalVector("_BlitScaleBias",scale);commands.SetGlobalVector("_BlitTexture_TexelSize",texel);commands.SetGlobalTexture("_BlitTexture",texture?texture:Texture2D.blackTexture);commands.SetGlobalVector("_ScreenParams",screen);Graphics.ExecuteCommandBuffer(commands);
   }catch(Exception e){failed=true;foreach(var row in grids.Values)foreach(var g in row.Values)g.Slot.Object.SetActive(false);NativeVisualPlugin.Instance.Logger.LogError("Native Sky GPU composition failed; other UI/visual modules retained: "+e);}
  }
  internal static void Dispose(object renderer){Dictionary<Slot,Grid> row;if(grids.TryGetValue(renderer,out row)){foreach(var grid in row.Values)grid.Dispose();grids.Remove(renderer);}}
  internal static void DisposeAll(){if(subscribed){RenderPipelineManager.beginCameraRendering-=Render;subscribed=false;}if(commands!=null){commands.Release();commands=null;}foreach(var row in grids.Values)foreach(var grid in row.Values)grid.Dispose();grids.Clear();failed=false;}
 }
}
