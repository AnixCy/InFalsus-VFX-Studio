Shader "InFalsusStudio/SideGuide" {Properties {_MainTex("Guide",2D)="white"{} _Tint("Tint",Color)=(1,1,1,1) _XBounds("Clip",Vector)=(-100,100,-100,100)} SubShader {Tags {"Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha Pass {
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _Tint,_XBounds;struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};V vert(appdata_base v){V o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}float4 frag(V i):SV_Target{clip(i.world.x-_XBounds.x);clip(_XBounds.y-i.world.x);clip(i.world.z-_XBounds.z);clip(_XBounds.w-i.world.z);return tex2D(_MainTex,i.uv)*_Tint;}
ENDHLSL
}}}