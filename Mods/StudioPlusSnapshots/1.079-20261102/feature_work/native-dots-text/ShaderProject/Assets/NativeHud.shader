Shader "InFalsusStudio/NativeHudSdf" {
Properties { _MainTex("SDF",2D)="black"{} _Bloom("Bloom",2D)="black"{} }
SubShader { Cull Off ZWrite Off ZTest Always
Pass { Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _Top,_Bottom,_Clip,_Baseline,_GlyphBounds;float4 _MainTex_TexelSize;
struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 pixel:TEXCOORD1;};
V vert(appdata_base v){V o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.pixel=v.vertex.xy;return o;}
float4 frag(V i):SV_Target{clip(i.pixel.x-_Clip.x);clip(_Clip.z-i.pixel.x);clip(i.pixel.y-_Clip.y);clip(_Clip.w-i.pixel.y);float2 footprint=(abs(ddx(i.uv))+abs(ddy(i.uv)))*_MainTex_TexelSize.zw;float aa=max(length(footprint)/32,.0001);float2 uv=clamp(i.uv,_GlyphBounds.xy,_GlyphBounds.zw);float d=tex2D(_MainTex,uv).a;float inside=all(i.uv>=_GlyphBounds.xy)&&all(i.uv<=_GlyphBounds.zw);float coverage=saturate((d-.5)/aa+.5)*inside;float t=saturate((i.pixel.y-_Baseline.x)/max(.001,_Baseline.z));float4 color=lerp(_Top,_Bottom,t);color.a*=coverage;return color;}
ENDHLSL }
Pass { Blend One OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex,_Bloom;float _OutputGamma;
float4 frag(v2f_img i):SV_Target{float4 c=tex2D(_MainTex,i.uv);float3 rgb=c.rgb+pow(max(tex2D(_Bloom,i.uv).rgb,0),2)*2;
#ifdef UNITY_COLORSPACE_GAMMA
return float4(rgb,c.a);
#else
if(_OutputGamma>.5)return float4(rgb,c.a);
float3 straight=c.rgb/max(c.a,.0001);float3 linearRgb=lerp(straight/12.92,pow((straight+.055)/1.055,2.4),step(.04045,straight));float3 glow=pow(max(tex2D(_Bloom,i.uv).rgb,0),2)*2;float3 glowLinear=lerp(glow/12.92,pow((glow+.055)/1.055,2.4),step(.04045,glow));return float4(linearRgb*c.a+glowLinear,c.a);
#endif
}
ENDHLSL }
Pass { Blend Off
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _MainTex_TexelSize;float _Threshold;
float4 frag(v2f_img i):SV_Target{float3 c=tex2D(_MainTex,i.uv).rgb;c=min(c,65472);float brightness=max(c.r,max(c.g,c.b));float soft=clamp(brightness-2.53715524+1.26857762,0,2.53715524);soft=soft*soft/(5.07431048+.00001);float contribution=max(soft,brightness-2.53715524)/max(brightness,.00001);return float4(sqrt(max(c*lerp(1,contribution,_Threshold),0)),0);}
ENDHLSL }
Pass { Blend Off
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex,_Bloom;float _OutputGamma;
float4 frag(v2f_img i):SV_Target{return float4(sqrt(max(lerp(pow(max(tex2D(_MainTex,i.uv).rgb,0),2),pow(max(tex2D(_Bloom,i.uv).rgb,0),2),.78889998),0)),0);}
ENDHLSL }

Pass { Blend Off
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _MainTex_TexelSize;
float4 frag(v2f_img i):SV_Target{float2 d=float2(_MainTex_TexelSize.x*2,0);float3 c=pow(max(tex2D(_MainTex,i.uv).rgb,0),2)*.22702703;c+=(pow(max(tex2D(_MainTex,i.uv-d).rgb,0),2)+pow(max(tex2D(_MainTex,i.uv+d).rgb,0),2))*.19459459;c+=(pow(max(tex2D(_MainTex,i.uv-d*2).rgb,0),2)+pow(max(tex2D(_MainTex,i.uv+d*2).rgb,0),2))*.12162162;c+=(pow(max(tex2D(_MainTex,i.uv-d*3).rgb,0),2)+pow(max(tex2D(_MainTex,i.uv+d*3).rgb,0),2))*.05405405;c+=(pow(max(tex2D(_MainTex,i.uv-d*4).rgb,0),2)+pow(max(tex2D(_MainTex,i.uv+d*4).rgb,0),2))*.01621622;return float4(sqrt(max(c,0)),0);}
ENDHLSL }
Pass { Blend Off
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _MainTex_TexelSize;
float4 frag(v2f_img i):SV_Target{float2 d=float2(0,_MainTex_TexelSize.y);float3 c=pow(max(tex2D(_MainTex,i.uv).rgb,0),2)*.22702703;c+=(pow(max(tex2D(_MainTex,i.uv-d*1.38461538).rgb,0),2)+pow(max(tex2D(_MainTex,i.uv+d*1.38461538).rgb,0),2))*.31621622;c+=(pow(max(tex2D(_MainTex,i.uv-d*3.23076923).rgb,0),2)+pow(max(tex2D(_MainTex,i.uv+d*3.23076923).rgb,0),2))*.07027027;return float4(sqrt(max(c,0)),0);}
ENDHLSL }

} }
