Shader "CustomVFX/ProceduralPulse"
{
 Properties {
  _Color("Color",Color)=(0.3,0.15,1,1)
  _Intensity("Intensity",Range(0,2))=0.5
  _Frequency("Frequency",Range(1,30))=8
  _Speed("Speed",Range(0,8))=1
 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline"} Pass {
  Cull Off ZWrite Off ZTest Always
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #include "UnityCG.cginc"
  sampler2D _BlitTexture;
  float4 _Color;float _Intensity,_Frequency,_Speed,_EffectTime;
  struct Varyings{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
  Varyings Vert(uint id:SV_VertexID){Varyings o;o.uv=float2((id<<1)&2,id&2);o.position=float4(o.uv*2-1,0,1);
  #if UNITY_UV_STARTS_AT_TOP
   o.uv.y=1-o.uv.y;
  #endif
   return o;}
  half4 Frag(Varyings i):SV_Target {
   float2 p=i.uv-.5;float r=length(p);
   float field=pow(saturate(.5+.5*sin(r*_Frequency*6.283185-_EffectTime*_Speed)),10);
   float3 source=tex2D(_BlitTexture,i.uv).rgb;
   return half4(source+field*_Intensity*_Color.rgb,1);
  }
  ENDHLSL
 } }
 Fallback Off
}
