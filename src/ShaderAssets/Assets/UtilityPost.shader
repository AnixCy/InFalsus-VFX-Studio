Shader "InFalsusVFX/UtilityPost"
{
 Properties {
  _Exposure("Exposure",Float)=0 _Contrast("Contrast",Float)=1 _Saturation("Saturation",Float)=1
  _Zoom("Zoom",Float)=1 _Rotation("Rotation",Float)=0 _Chroma("Chroma",Float)=0
  _Ripple("Ripple",Float)=0 _RippleFrequency("Ripple frequency",Float)=20 _RippleSpeed("Ripple speed",Float)=2
  _Scanline("Scanline",Float)=0 _Noise("Noise",Float)=0 _PixelSize("Pixel size",Float)=1
  _Vignette("Vignette",Float)=0 _Fade("Fade",Float)=0 _Wipe("Wipe",Float)=0
  _Ring("Ring",Float)=0 _RingRadius("Ring radius",Float)=0.3 _RingWidth("Ring width",Float)=0.015
 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline"} Pass {
  Cull Off ZWrite Off ZTest Always
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _BlitTexture;
  float4 _BlitTexture_TexelSize;
  float _Exposure,_Contrast,_Saturation,_Zoom,_Rotation,_Chroma,_Ripple,_RippleFrequency,_RippleSpeed;
  float _Scanline,_Noise,_PixelSize,_Vignette,_Fade,_Wipe,_Ring,_RingRadius,_RingWidth,_PresentationTime;
  struct Varyings {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
  Varyings Vert(uint id:SV_VertexID){Varyings o;o.uv=float2((id<<1)&2,id&2);o.position=float4(o.uv*2-1,0,1);
  #if UNITY_UV_STARTS_AT_TOP
   o.uv.y=1-o.uv.y;
  #endif
   return o;}
  float Noise(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
  half4 Frag(Varyings i):SV_Target{
   float2 p=(i.uv-.5)/max(.01,_Zoom);float c=cos(_Rotation),s=sin(_Rotation);
   p=mul(float2x2(c,-s,s,c),p);float r=length(p);
   p+=p/max(r,.001)*sin(r*_RippleFrequency-_PresentationTime*_RippleSpeed)*_Ripple;
   float2 uv=p+.5;
   if(_PixelSize>1.01)uv=(floor(uv/(_BlitTexture_TexelSize.xy*_PixelSize))+.5)*(_BlitTexture_TexelSize.xy*_PixelSize);
   float2 offset=p*_Chroma;
   float3 color=tex2D(_BlitTexture,uv).rgb;
   if(abs(_Chroma)>.000001){color.r=tex2D(_BlitTexture,uv+offset).r;color.b=tex2D(_BlitTexture,uv-offset).b;}
   float grey=dot(color,float3(.2126,.7152,.0722));color=lerp(grey.xxx,color,_Saturation);
   color=(color-.18)*_Contrast+.18;color*=exp2(_Exposure);
   color*=1-_Scanline*(.5+.5*sin(i.uv.y/max(_BlitTexture_TexelSize.y,.00001)*3.14159));
   color+=(Noise(floor(i.uv/_BlitTexture_TexelSize.xy)+floor(_PresentationTime*120))-.5)*_Noise;
   color*=1-saturate(r*r*2*_Vignette);
   float ring=1-smoothstep(_RingWidth,_RingWidth+max(_BlitTexture_TexelSize.x,.001),abs(r-_RingRadius));
   color+=ring*_Ring*float3(.55,.25,1);
   float mask=_Wipe<=0?1:1-smoothstep((1-_Wipe)*.8-.02,(1-_Wipe)*.8,r);
   return half4(color*(1-saturate(_Fade))*mask,1);
  }
  ENDHLSL
 } }
 Fallback Off
}
