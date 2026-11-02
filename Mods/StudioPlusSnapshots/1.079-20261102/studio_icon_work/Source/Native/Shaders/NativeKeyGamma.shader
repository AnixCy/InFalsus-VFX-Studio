Shader "InFalsusStudio/NativeKeyGamma" {
 Properties { _MainTex("Native key",2D)="white"{} _NativeKeyBackground("Background",2D)="black"{} }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Pass {
 Cull Off ZWrite Off ZTest LEqual Blend One Zero
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 4.0
 #include "UnityCG.cginc"
 sampler2D _MainTex,_NativeKeyBackground;float4 _NativeKeyBackground_TexelSize;
 struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;};
 V vert(appdata_base i){V o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.texcoord.xy;o.screen=ComputeScreenPos(o.position);return o;}
 float3 gamma(float3 x){x=max(0,x);return lerp(x*12.92,1.055*pow(x,1./2.4)-.055,step(.0031308,x));}
 float3 toLinear(float3 x){x=max(0,x);return lerp(x/12.92,pow((x+.055)/1.055,2.4),step(.04045,x));}
 float4 frag(V i):SV_Target {float4 ink=tex2D(_MainTex,i.uv);float4 bg=tex2D(_NativeKeyBackground,i.screen.xy/i.screen.w);
 #ifdef UNITY_COLORSPACE_GAMMA
 return float4(lerp(bg.rgb,ink.rgb,ink.a),bg.a);
 #else
 return float4(toLinear(lerp(gamma(bg.rgb),gamma(ink.rgb),ink.a)),bg.a);
 #endif
 }
 ENDHLSL
 } }
}

