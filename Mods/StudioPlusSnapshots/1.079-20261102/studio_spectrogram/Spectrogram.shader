Shader "InFalsusStudio/Spectrogram" {
 Properties {_MainTex("Intensity",2D)="black"{} _Opacity("Opacity",Range(0,1))=.5 _Mirror("Mirror",Float)=0 _TimeUv("Time range",Vector)=(0,1,0,0)}
 SubShader {
  Tags {"Queue"="Transparent-95" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
  Pass {HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize,_TimeUv;float _Opacity,_Mirror;
   struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   V vert(appdata_base v){V o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
   float Cell(float2 cell){cell=clamp(floor(cell),float2(0,0),_MainTex_TexelSize.zw-1);return tex2Dlod(_MainTex,float4((cell+.5)*_MainTex_TexelSize.xy,0,0)).r;}
   float Coverage(float e){if(e<=.4)return smoothstep(.10,.72,e);float t=saturate((e-.4)/.6);float a=smoothstep(.10,.72,.4);float slope=6*((.4-.10)/.62)*(1-(.4-.10)/.62)/.62;return saturate((2*t*t*t-3*t*t+1)*a+(t*t*t-2*t*t+t)*.6*slope+(-2*t*t*t+3*t*t));}
   float4 frag(V i):SV_Target {
    float2 uv=float2(lerp(i.uv.x,1-i.uv.x,step(.5,_Mirror)),lerp(_TimeUv.x,_TimeUv.y,i.uv.y));
    float e=saturate(Cell(uv*_MainTex_TexelSize.zw));
    float3 rose=float3(.702,.267,.392),coral=float3(1,.451,.310),gold=float3(1,.839,.200),light=float3(1,1,.502);
    float3 color=e<.55?lerp(rose,coral,e/.55):e<.85?lerp(coral,gold,(e-.55)/.30):lerp(gold,light,saturate((e-.85)/.15));
    #ifndef UNITY_COLORSPACE_GAMMA
    color=GammaToLinearSpace(color);
    #endif
    // Suppress faint diffuse skirts while keeping meaningful cells legible on the dark floor.
    return float4(color,_Opacity*Coverage(e));
   }
   ENDHLSL
  }
 }
}
