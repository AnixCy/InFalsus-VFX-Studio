using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace InFalsusStudio.NativeVisual {
 internal enum DisplayAntialiasing {None,FXAA,SMAA,MSAA4}
 internal static class AntialiasingSettings {
  internal static ConfigEntry<DisplayAntialiasing> Setting;internal static string Status="";internal static bool CanMsaa;
  static Camera camera;static UniversalAdditionalCameraData extra;static UniversalRenderPipelineAsset source,clone;static RenderPipelineAsset originalQuality;
  static AntialiasingMode originalAA;static AntialiasingQuality originalQualityAA;static bool originalPost,originalMsaa;static DisplayAntialiasing applied=(DisplayAntialiasing)(-1);static string rendererMode="";
  static readonly Dictionary<int,Texture2D> lines=new Dictionary<int,Texture2D>();
  internal static void Init(ConfigFile config){Setting=config.Bind("Display","Antialiasing",DisplayAntialiasing.None,"None / FXAA / SMAA high / MSAA4. Uses a private runtime pipeline copy when needed; original assets stay untouched. Unsupported MSAA falls back to SMAA.");}
  internal static void Tick(Camera current){
   if(!current||Setting==null)return;
   if(camera!=current){Restore();camera=current;extra=current.GetUniversalAdditionalCameraData();originalAA=extra.antialiasing;originalQualityAA=extra.antialiasingQuality;originalPost=extra.renderPostProcessing;originalMsaa=current.allowMSAA;originalQuality=QualitySettings.renderPipeline;source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;applied=(DisplayAntialiasing)(-1);}
   if(applied==Setting.Value)return;
   Apply(Setting.Value);
  }
  static void Apply(DisplayAntialiasing requested){
   if(!source){Status="当前渲染管线不支持抗锯齿设置";applied=requested;return;}
   var renderer=source.scriptableRenderer;var property=renderer.GetType().GetProperty("renderingModeActual",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);rendererMode=property==null?"unknown":Convert.ToString(property.GetValue(renderer,null));
   var descriptor=new RenderTextureDescriptor(32,32,camera.allowHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default,24){msaaSamples=4};int supported=SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
   CanMsaa=rendererMode.IndexOf("Forward",StringComparison.OrdinalIgnoreCase)>=0&&supported>=4;
   var mode=requested;if(mode==DisplayAntialiasing.MSAA4&&!CanMsaa)mode=DisplayAntialiasing.SMAA;
   int samples=mode==DisplayAntialiasing.MSAA4?4:1;
   if(source.msaaSampleCount!=samples||clone){if(!clone){clone=UnityEngine.Object.Instantiate(source);clone.name=source.name+" (VisualUI private AA)";clone.hideFlags=HideFlags.HideAndDontSave;}clone.msaaSampleCount=samples;if(QualitySettings.renderPipeline!=clone)QualitySettings.renderPipeline=clone;}
   camera.allowMSAA=mode==DisplayAntialiasing.MSAA4;
   extra.antialiasing=mode==DisplayAntialiasing.FXAA?AntialiasingMode.FastApproximateAntialiasing:mode==DisplayAntialiasing.SMAA?AntialiasingMode.SubpixelMorphologicalAntiAliasing:AntialiasingMode.None;
   extra.antialiasingQuality=AntialiasingQuality.High;extra.renderPostProcessing=mode==DisplayAntialiasing.FXAA||mode==DisplayAntialiasing.SMAA||originalPost;
   Status=requested!=mode?"MSAA 4× 不可用，已使用 SMAA":"";applied=requested;
  }
  internal static string Diagnostic{get{return "requested="+(Setting==null?"unbound":Setting.Value.ToString())+" renderer="+rendererMode+" msaa="+(clone?clone.msaaSampleCount:source?source.msaaSampleCount:0)+" cameraAA="+(extra?extra.antialiasing.ToString():"missing")+" supported4="+CanMsaa+" status="+Status;}}
  internal static bool SmoothLines{get{return Setting!=null&&Setting.Value!=DisplayAntialiasing.None;}}
  internal static bool DrawLine(Vector2 a,Vector2 b,Color color,float thickness){
   if(!SmoothLines)return false;if(Event.current.type!=EventType.Repaint)return true;
   int key=Mathf.Max(1,Mathf.RoundToInt(thickness*8));Texture2D texture;
   if(!lines.TryGetValue(key,out texture)){float width=key/8f,height=width+2;var pixels=new Color32[128];for(int y=0;y<64;y++){float distance=Math.Abs(((y+.5f)/64f-.5f)*height);byte alpha=(byte)Mathf.RoundToInt(Mathf.Clamp01(width*.5f+.5f-distance)*255);pixels[y*2]=pixels[y*2+1]=new Color32(255,255,255,alpha);}texture=new Texture2D(2,64,TextureFormat.RGBA32,false);texture.name="VisualUI AA guide line";texture.SetPixels32(pixels);texture.Apply(false,true);texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;texture.hideFlags=HideFlags.HideAndDontSave;lines.Add(key,texture);}
   var delta=b-a;if(delta.sqrMagnitude<1e-10f)return true;var matrix=GUI.matrix;var tint=GUI.color;
   try{GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);float height=key/8f+2;GUI.DrawTexture(new Rect(a.x,a.y-height*.5f,delta.magnitude,height),texture);}finally{GUI.matrix=matrix;GUI.color=tint;}return true;
  }
  static void Restore(){if(camera&&extra){extra.antialiasing=originalAA;extra.antialiasingQuality=originalQualityAA;extra.renderPostProcessing=originalPost;camera.allowMSAA=originalMsaa;}if(clone){if(QualitySettings.renderPipeline==clone)QualitySettings.renderPipeline=originalQuality;UnityEngine.Object.Destroy(clone);}clone=null;source=null;camera=null;extra=null;}
  internal static void Dispose(){Restore();foreach(var texture in lines.Values)if(texture)UnityEngine.Object.Destroy(texture);lines.Clear();}
 }
}
