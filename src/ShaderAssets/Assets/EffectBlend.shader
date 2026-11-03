Shader "InFalsusVFX/EffectBlend"
{
 SubShader { Tags {"RenderPipeline"="UniversalPipeline"} Pass {
  Cull Off ZWrite Off ZTest Always
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #include "UnityCG.cginc"
  sampler2D _BlitTexture,_VfxBeforeTexture;float _VfxBlend;
  struct Varyings{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
  Varyings Vert(uint id:SV_VertexID){Varyings o;o.uv=float2((id<<1)&2,id&2);o.position=float4(o.uv*2-1,0,1);
  #if UNITY_UV_STARTS_AT_TOP
   o.uv.y=1-o.uv.y;
  #endif
   return o;}
  half4 Frag(Varyings i):SV_Target{return lerp(tex2D(_VfxBeforeTexture,i.uv),tex2D(_BlitTexture,i.uv),saturate(_VfxBlend));}
  ENDHLSL
 } }
 Fallback Off
}
