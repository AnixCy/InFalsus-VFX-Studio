Shader "InFalsusStudio/TestFloorSampling" {
 Properties { _MainTex("Floor",2D)="black"{} _DecodeAfter("Decode gamma after filter",Float)=0 _TrialDotGain("Approximate dot trial",Float)=0 _TrackUnit("Track unit",Float)=2.5 }
 SubShader { Tags {"Queue"="Geometry" "RenderType"="Opaque"} Pass {
 Cull Off ZWrite On ZTest LEqual
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex;float _DecodeAfter,_TrialDotGain,_TrackUnit;
 struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
 V vert(appdata_base i){V o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.texcoord.xy;o.world=mul(unity_ObjectToWorld,i.vertex).xyz;return o;}
 float3 toLinear(float3 x){return lerp(x/12.92,pow((x+.055)/1.055,2.4),step(.04045,x));}
 float4 frag(V i):SV_Target {
  float4 p=tex2D(_MainTex,i.uv);
  if(_TrialDotGain>0){
   float2 stage=i.world.xz*2.5/max(_TrackUnit,.001);
   float3 points=0;
   for(int layer=0;layer<4;layer++){
   float2 uv=float2(.42+frac(stage.x*.65+layer*.271)*.14,frac(stage.y*.055+layer*.173));
   float3 center=tex2Dlod(_MainTex,float4(uv,0,0)).rgb;
   float2 delta=float2(2./2048.,0);
   float3 average=(tex2Dlod(_MainTex,float4(uv+delta,0,0)).rgb+tex2Dlod(_MainTex,float4(uv-delta,0,0)).rgb+tex2Dlod(_MainTex,float4(uv+delta.yx,0,0)).rgb+tex2Dlod(_MainTex,float4(uv-delta.yx,0,0)).rgb)*.25;
   points=max(points,max(0,center-average-.001));
   }
   float2 radius=float2(stage.x/1.35,(stage.y-7.5)/4.5);
   float mask=exp(-dot(radius*radius,radius*radius));
   p.rgb+=points*_TrialDotGain*mask;
  }
  return float4(_DecodeAfter>.5?toLinear(p.rgb):p.rgb,1);
 }
 ENDHLSL
 } }
}
