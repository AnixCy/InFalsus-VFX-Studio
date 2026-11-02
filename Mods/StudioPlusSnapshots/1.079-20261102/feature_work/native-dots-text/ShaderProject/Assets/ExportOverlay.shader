Shader "InFalsusStudio/ExportOverlay" { Properties {_MainTex("Image",2D)="white"{} _ImageTint("Tint",Color)=(1,1,1,1)} SubShader {Cull Off ZWrite Off ZTest Always
HLSLINCLUDE
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _ImageTint;
float3 toGamma(float3 c){c=max(c,0);return lerp(c*12.92,1.055*pow(c,1./2.4)-.055,step(.0031308,c));}
float3 toLinear(float3 c){c=max(c,0);return lerp(c/12.92,pow((c+.055)/1.055,2.4),step(.04045,c));}
ENDHLSL
Pass {Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{float4 c=tex2D(_MainTex,i.uv);
#ifndef UNITY_COLORSPACE_GAMMA
c.rgb=toGamma(c.rgb);
#endif
return c*_ImageTint;}
ENDHLSL }
Pass {Blend Off
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{float4 c=tex2D(_MainTex,i.uv);
#ifndef UNITY_COLORSPACE_GAMMA
c.rgb=toGamma(c.rgb);
#endif
return c;}
ENDHLSL }
Pass {Blend Off
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{float4 c=tex2D(_MainTex,i.uv);
#ifndef UNITY_COLORSPACE_GAMMA
c.rgb=toLinear(c.rgb);
#endif
return c;}
ENDHLSL }
}}