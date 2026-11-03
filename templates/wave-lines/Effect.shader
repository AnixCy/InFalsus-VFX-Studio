// One image sample, six analytic light curves, optional triangular tunnel.
// Phase and Travel are authored coordinates: changing playback speed never jumps their phase.
Shader "CustomVFX/WaveLines"
{
 Properties {
  _Intensity("Master Amount",Range(0,1))=1
  _CenterX("Center / X",Range(0,1))=0.5
  _CenterY("Center / Y",Range(0,1))=0.48
  _WaveAmount("Wave / Displacement",Range(0,0.12))=0
  _WavePhase("Wave / Phase (cycles)",Range(-16,16))=0
  _WaveFrequency("Wave / Density",Range(4,40))=18
  _LineAmount("Lines / Brightness",Range(0,2))=0.65
  [HDR] _LineColor("Lines / Color",Color)=(1,0.58,0.16,1)
  _LinePhase("Lines / Phase (cycles)",Range(-8,8))=0
  _LineWidth("Lines / Width",Range(0.0005,0.008))=0.0014
  _LineBend("Lines / Curvature",Range(0,0.3))=0.1
  _BaseDim("Background / Dimming",Range(0,0.85))=0.16
  _Collapse("Transition / Inward Warp",Range(0,1))=0
  _PortalRadius("Transition / Reveal Radius",Range(0,1.5))=0
  _RushMix("Tunnel / Visibility",Range(0,1))=0
  [HDR] _RushColor("Tunnel / Color",Color)=(0.48,0.07,1,1)
  _Travel("Tunnel / Travel (cells)",Range(-128,128))=0
  _GridWidth("Tunnel / Line Width",Range(0.003,0.04))=0.012
 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline"} Pass {
  Cull Off ZWrite Off ZTest Always
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex Vert
  #pragma fragment Frag
  #include "UnityCG.cginc"
  sampler2D _BlitTexture;
  float4 _BlitTexture_TexelSize,_LineColor,_RushColor;
  float _Intensity,_CenterX,_CenterY,_WaveAmount,_WavePhase,_WaveFrequency;
  float _LineAmount,_LinePhase,_LineWidth,_LineBend,_BaseDim;
  float _Collapse,_PortalRadius,_RushMix,_Travel,_GridWidth;
  struct Varyings {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
  Varyings Vert(uint id:SV_VertexID) {
   Varyings o;o.uv=float2((id<<1)&2,id&2);o.position=float4(o.uv*2-1,0,1);
   #if UNITY_UV_STARTS_AT_TOP
    o.uv.y=1-o.uv.y;
   #endif
   return o;
  }
  float ThinLine(float distance,float width) {
   float aa=max(fwidth(distance),0.00025);
   float core=1-smoothstep(width,width+aa,abs(distance));
   float glow=exp2(-abs(distance)/max(width*4,0.001));
   return core+glow*0.18;
  }
  float3 GoldLines(float2 p) {
   float result=0;
   // Fixed-size loop; no particle buffers, meshes, history or secondary textures.
   [unroll] for(int k=0;k<6;k++) {
    float phase=_LinePhase*6.283185;
    float angle=k*0.53+0.18*sin(phase*0.37+k);
    float sn=sin(angle),cs=cos(angle);
    float2 q=float2(cs*p.x-sn*p.y,sn*p.x+cs*p.y);
    float offset=(k-2.5)*0.13+0.065*sin(phase+k*1.73);
    float bend=_LineBend*sin(q.x*3.2+phase+k*1.41);
    float d=q.y-offset-bend;
    float taper=exp2(-q.x*q.x*1.2);
    result+=ThinLine(d,_LineWidth)*taper*(0.55+0.45*sin(k*2.0+phase*0.19)*sin(k*2.0+phase*0.19));
   }
   // Reduce glare around the play area while keeping long peripheral strands.
   float centerQuiet=1-0.7*exp2(-dot(p*float2(3,2),p*float2(3,2)));
   return result*centerQuiet*_LineColor.rgb*_LineAmount;
  }
  float3 Tunnel(float2 p) {
   // Perspective intersection with three fixed walls. The game camera never moves.
   const float2 n0=float2(0,1),n1=float2(0.866025,-0.5),n2=float2(-0.866025,-0.5);
   float d0=dot(p,n0),d1=dot(p,n1),d2=dot(p,n2);
   float2 n=d0>d1?(d0>d2?n0:n2):(d1>d2?n1:n2);
   float depth=1/max(dot(p,n),0.035);
   float2 wall=p*depth;
   float2 q=float2(dot(wall,float2(-n.y,n.x))*1.3,(depth+_Travel)*0.7);
   float3 lattice=float3(q.x+q.y*0.57735,q.x-q.y*0.57735,q.y*1.1547);
   float3 d=abs(frac(lattice+0.5)-0.5);
   float3 aa=max(fwidth(lattice),0.0001);
   float3 edges=1-smoothstep(_GridWidth,_GridWidth+aa,d);
   float edgeValue=max(edges.x,max(edges.y,edges.z));
   float haze=exp2(-depth*0.1)*smoothstep(0.03,0.13,length(p));
   float3 dark=float3(0.009,0.003,0.022);
   return dark+_RushColor.rgb*edgeValue*haze*1.7;
  }
  half4 Frag(Varyings i):SV_Target {
   if(_Intensity<=0.0001)return half4(tex2D(_BlitTexture,i.uv).rgb,1);
   float aspect=abs(_BlitTexture_TexelSize.z/max(1,_BlitTexture_TexelSize.w));
   float2 center=float2(_CenterX,_CenterY),scale=float2(aspect,1),p=(i.uv-center)*scale;
   float radius=length(p),amount=saturate(_Intensity);
   float2 direction=p/max(radius,0.0001);
   float wave=sin(radius*_WaveFrequency-_WavePhase*6.283185)*exp2(-radius*1.4);
   float2 warped=p+direction*wave*_WaveAmount*amount;
   // A readable inward pull before an expanding soft reveal; no stencil dependency.
   warped*=1+_Collapse*amount*exp2(-radius*2)*1.5;
   float2 uv=saturate(center+warped/scale);
   float3 color=tex2D(_BlitTexture,uv).rgb*(1-_BaseDim*amount);
   if(_LineAmount>0.0001)color+=GoldLines(warped)*amount;
   if(_RushMix>0.0001&&amount>0.0001) {
    float edge=0.06;
    float reveal=(_PortalRadius<=0)?0:1-smoothstep(_PortalRadius-edge,_PortalRadius,radius);
    float front=1-smoothstep(0.008,0.04,abs(radius-_PortalRadius));
    float portalVisible=smoothstep(0,0.05,_PortalRadius)*(1-smoothstep(1.2,1.5,_PortalRadius));
    color=lerp(color,Tunnel(p),reveal*saturate(_RushMix*amount));
    color+=front*portalVisible*_RushColor.rgb*0.6*_RushMix*amount;
   }
   return half4(color,1);
  }
  ENDHLSL
 } }
 Fallback Off
}
