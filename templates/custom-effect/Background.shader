Shader "CustomVFX/Background"
{
 Properties {
  _MainTex("Background image",2D)="white" {}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _Weave("Weave strength",Range(0,1))=0.1
  _Density("Density",Range(1,30))=8
  [HideInInspector] _Color("Native scene color",Color)=(1,1,1,1)
  [HideInInspector] _PresentationTime("Time",Float)=0
 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"} Pass {
  Tags {"LightMode"="SRPDefaultUnlit"} Cull Back ZWrite On ZTest LEqual
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #include "UnityCG.cginc"
  sampler2D _MainTex;float4 _BaseColor,_Color;float _Weave,_Density,_PresentationTime;
  struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
  struct Output {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
  Output Vert(Input v){Output o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
  half4 Frag(Output i):SV_Target {
   float2 uv=i.uv;uv.x+=sin(uv.y*_Density+_PresentationTime)*_Weave*.03;
   return tex2D(_MainTex,uv)*_BaseColor*_Color;
  }
  ENDHLSL
 } }
 Fallback Off
}
