Shader "InFalsusStudio/NativeKeyTextGamma" {
 Properties { _MainTex("Private SDF atlas",2D)="black"{} }
 SubShader { Tags {"Queue"="Transparent+1" "RenderType"="Transparent"} Pass {
 Cull Off ZWrite Off ZTest LEqual Blend One Zero
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 4.0
 #include "UnityCG.cginc"
 sampler2D _MainTex,_NativeLabelBackground;
 float4 _MainTex_TexelSize;
 struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;};
 V vert(appdata_base i){V o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.texcoord.xy;o.screen=ComputeScreenPos(o.position);return o;}
 float3 gamma(float3 x){x=max(0,x);return lerp(x*12.92,1.055*pow(x,1./2.4)-.055,step(.0031308,x));}
 float3 toLinear(float3 x){x=max(0,x);return lerp(x/12.92,pow((x+.055)/1.055,2.4),step(.04045,x));}
 float4 frag(V i):SV_Target {
  float distance=tex2D(_MainTex,i.uv).r-.5;
  float2 pixelUv=i.uv*_MainTex_TexelSize.zw;
  float coverage=saturate(.5+distance*32/max(length(fwidth(pixelUv)),.0001));
  float4 bg=tex2D(_NativeLabelBackground,i.screen.xy/i.screen.w);
  float3 color=float3(1.2691348791122437,2.3396224975585938,2.3396224975585938);
  float a=coverage*.3607843220233917;
  #ifdef UNITY_COLORSPACE_GAMMA
  return float4(lerp(bg.rgb,color,a),bg.a);
  #else
  return float4(toLinear(lerp(gamma(bg.rgb),color,a)),bg.a);
  #endif
 }
 ENDHLSL
 } }
}
