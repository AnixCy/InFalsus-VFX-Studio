// Shared far focus, projected light strands, cellular ribs and bevel-like edge light.
// The background contains no imitation of chart-lane occlusion.
Shader "CustomVFX/PerspectiveCells"
{
 Properties {
  _Intensity("Master Amount",Range(0,1))=1
  _CenterX("Focus / X",Range(0,1))=0.5
  _CenterY("Focus / Y (top=0)",Range(0,1))=0.18
  _WaveAmount("Wave / Displacement",Range(0,0.12))=0
  _WavePhase("Wave / Phase (cycles)",Range(-16,16))=0
  _WaveFrequency("Wave / Density",Range(4,40))=18
  _LineAmount("Strands / Brightness",Range(0,2))=0.6
  [HDR] _LineColor("Strands / Color",Color)=(1,0.63,0.23,1)
  _LinePhase("Strands / Sway (cycles)",Range(-8,8))=0
  _LineWidth("Strands / Width",Range(0.0003,0.006))=0.0009
  _LineBend("Strands / Curvature",Range(0,0.3))=0.12
  _BaseDim("Background / Dimming",Range(0,0.85))=0.12
  _BackgroundTint("Background / Tint",Color)=(0.55,0.5,0.8,1)
  _Collapse("Transition / Inward Warp",Range(0,1))=0
  _PortalRadius("Transition / Reveal Radius",Range(0,1.5))=0
  _RushMix("Cell Structure / Visibility",Range(0,1))=0
  [HDR] _RushColor("Cell Structure / Violet",Color)=(0.24,0.055,0.68,1)
  _Travel("Cell Structure / Travel",Range(-128,128))=0
  _GridWidth("Cell Structure / Rib Width",Range(0.015,0.24))=0.11
  _GridScale("Cell Structure / Density",Range(0.3,2.5))=1.1
  _CellRandomness("Cell Structure / Irregularity",Range(0,1))=0.9
  _Reflection("Material / Reflection",Range(0,1))=0.65
 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline"} Pass {
  Cull Off ZWrite Off ZTest Always
  HLSLPROGRAM
  #pragma target 3.5
  #pragma vertex Vert
  #pragma fragment Frag
  #include "UnityCG.cginc"
  sampler2D _BlitTexture;
  float4 _BlitTexture_TexelSize,_LineColor,_RushColor,_BackgroundTint;
  float _Intensity,_CenterX,_CenterY,_WaveAmount,_WavePhase,_WaveFrequency;
  float _LineAmount,_LinePhase,_LineWidth,_LineBend,_BaseDim;
  float _Collapse,_PortalRadius,_RushMix,_Travel,_GridWidth,_GridScale;
  float _CellRandomness,_Reflection;
  struct Varyings {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
  Varyings Vert(uint id:SV_VertexID) {
   Varyings o;o.uv=float2((id<<1)&2,id&2);o.position=float4(o.uv*2-1,0,1);
   #if UNITY_UV_STARTS_AT_TOP
    o.uv.y=1-o.uv.y;
   #endif
   return o;
  }
  float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float Noise(float2 p) {
   float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
   return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
  }
  uint Scramble(uint x) {x^=x>>16;x*=2246822519u;x^=x>>13;x*=3266489917u;return x^(x>>16);}
  float2 CellHash(float2 cell) {
   // Ten periodic angular cells; no seam at atan2's branch cut.
   cell.x=cell.x-floor(cell.x/10)*10;
   uint2 k=(uint2)(int2)cell;
   uint seed=Scramble(k.x*1664525u+k.y*1013904223u+36969u);
   return float2(seed,Scramble(seed+362437u))*(1.0/4294967296.0);
  }
  struct CellSample {float edge,shade;float2 normal;};
  float2 LatticeVertex(float2 id) {
   float stagger=fmod(abs(id.y),2)*0.5;
   return float2(id.x+stagger,id.y*0.8660254)+(CellHash(id)-0.5)*_CellRandomness*0.65;
  }
  void EdgeCandidate(float2 a,float2 b,float factor,float shade,inout CellSample result) {
   float2 v=b-a;float t=saturate(-dot(a,v)/max(dot(v,v),0.00001));float2 q=a+t*v;
   float lengthQ=length(q),distance=lengthQ/factor;
   if(distance<result.edge){result.edge=distance;result.normal=q/max(lengthQ,0.0001);result.shade=shade;}
  }
  CellSample Cellular(float2 p) {
   // A triangulated, noise-displaced lattice: no square truss and no Voronoi honeycomb.
   // Model equivalent: scatter/jitter vertices -> triangulate -> thicken edges -> bevel.
   float2 base=floor(float2(p.x,p.y/0.8660254));
   CellSample result;result.edge=1e9;result.normal=0;result.shade=0;
   [loop] for(int y=-1;y<=1;y++) [loop] for(int x=-1;x<=1;x++) {
    float2 id=base+float2(x,y);float odd=fmod(abs(id.y),2);
    float2 a=LatticeVertex(id)-p;
    float2 b=LatticeVertex(id+float2(1,0))-p;
    float2 c=LatticeVertex(id+float2(odd,1))-p;
    float2 d=LatticeVertex(id+float2(odd-1,1))-p;
    float h0=CellHash(id+float2(3,17)).x,h1=CellHash(id+float2(7,31)).x,h2=CellHash(id+float2(9,43)).x;
    EdgeCandidate(a,b,lerp(0.65,1.25,h0),h0,result);
    EdgeCandidate(a,c,lerp(0.65,1.25,h1),h1,result);
    EdgeCandidate(a,d,lerp(0.65,1.25,h2),h2,result);
   }
   return result;
  }
  float ThinLine(float d,float width) {
   float aa=max(fwidth(d),0.0002);
   return 1-smoothstep(width,width+aa,abs(d))+exp2(-abs(d)/max(width*4,0.001))*0.16;
  }
  float3 GoldStrands(float2 p,float aspect) {
   // All strands share p=(0,0), the elevated far focus; curvature grows toward us.
   float t=p.y/max(0.2,1-_CenterY);
   if(t<=0)return 0;
   float result=0,phase=_LinePhase*6.283185;
   float spread=pow(t,0.92);
   [unroll] for(int k=0;k<8;k++) {
    float side=k<4?-1:1;
    float lane=fmod(k,4);
    float reach=side*(0.28+lane*0.19)*aspect;
    float bend=_LineBend*aspect*sin(phase*(0.36+lane*0.05)+lane*1.17+side*0.9+t*2.2);
    float path=reach*spread+bend*t*(1-t)*2.4;
    float slope=(reach*0.92*pow(max(t,0.0001),-0.08)+bend*(1-2*t)*2.4)/max(0.2,1-_CenterY);
    float distance=(p.x-path)*rsqrt(1+slope*slope);
    float width=_LineWidth*lerp(0.2,1.1,saturate(t));
    float visibility=smoothstep(0.008,0.10,t)*(0.18+0.82*sqrt(saturate(t)));
    float variation=0.55+0.45*pow(sin(phase*0.19+k*1.7),2);
    result+=ThinLine(distance,width)*visibility*variation;
   }
   return result*_LineColor.rgb*_LineAmount;
  }
  float3 DarkField(float2 p) {
   float phase=_Travel*0.018;
   float cloud=Noise(p*float2(4.2,3.1)+float2(phase,-phase*0.3));
   cloud+=0.4*Noise(p*float2(10.3,8.4)+float2(-phase*0.2,phase));
   float away=smoothstep(0.025,0.28,length(p));
   return float3(0.002,0.001,0.006)+float3(0.014,0.005,0.033)*pow(saturate(cloud*0.75),2)*away;
  }
  float3 CellularRush(float2 p) {
   float3 dark=DarkField(p);
   float radius=length(p),depth=1/max(radius,0.035);
   float angular=(atan2(p.y,p.x)+3.14159265)/6.2831853;
   float2 grid=float2(angular*10,(depth+_Travel)*0.65*_GridScale);
   CellSample cell=Cellular(grid);
   float angle=angular*6.2831853;
   float width=_GridWidth*(0.8+0.3*Noise(float2(p.x/max(radius,0.001)*1.7,grid.y*0.4+p.y/max(radius,0.001)*0.7)));
   float warpedEdge=cell.edge+width*(0.07*sin(angle*19+sin(grid.y*17))+0.035*sin(grid.y*37));
   // Analytic projection footprint avoids atan2 seam derivatives wiping out a scanline.
   float pixel=max(abs(_BlitTexture_TexelSize.y),0.00001);
   float footprint=length(float2(10/(6.2831853*max(radius,0.035)),0.65*_GridScale/max(radius*radius,0.001)))*pixel;
   float aa=max(footprint*0.45,0.0007);
   float bodyMask=1-smoothstep(width-aa,width+aa,warpedEdge);
   float bevel=saturate((warpedEdge-width*0.55)/max(width*0.45,0.001));
   float rim=exp2(-pow((warpedEdge-width*0.88)/max(width*0.07,aa),2));
   float2 gradient=float2(ddx(cell.edge),ddy(cell.edge));
   gradient*=rsqrt(max(dot(gradient,gradient),0.00000001));
   float3 normal=normalize(float3(gradient*bevel*0.85,lerp(1,0.45,bevel)));
   float3 lightDirection=normalize(float3(-0.4,0.5,0.8));
   float light=0.22+0.78*saturate(dot(normal,lightDirection));
   float specular=pow(saturate(dot(reflect(-lightDirection,normal),float3(0,0,1))),28);
   float crack=pow(abs(sin(angle*7+grid.y*13.1)),18);
   // Reuse the existing scene as a stylized environment reflection; no new texture asset.
   float2 reflectionUV=float2(frac(angular+cell.normal.x*0.08),saturate(0.5+sin(grid.y*0.17)*0.18+cell.normal.y*0.05));
   float3 environment=tex2D(_BlitTexture,reflectionUV).rgb;
   float environmentLuma=dot(environment,float3(0.2126,0.7152,0.0722));
   float grain=Noise(float2(p.x/max(radius,0.001)*13,grid.y*9+p.y/max(radius,0.001)*3));
   float3 face=_RushColor.rgb*(0.035+environmentLuma*_Reflection*0.24)*bodyMask*light*(0.8+grain*0.2);
   float3 edge=lerp(_RushColor.rgb,float3(0.8,0.63,1.2),0.8)*bodyMask*(rim*(0.75+crack*1.6)+specular*0.6);
   float3 glow=_RushColor.rgb*exp2(-abs(warpedEdge-width)/max(width*0.35,0.001))*0.025;
   float resolve=1-smoothstep(0.16,0.75,footprint);
   float fog=exp2(-depth*depth*0.03)*smoothstep(0.04,0.09,radius);
   return dark+(face+edge+glow)*resolve*fog;
  }
  half4 Frag(Varyings i):SV_Target {
   if(_Intensity<=0.0001)return half4(tex2D(_BlitTexture,i.uv).rgb,1);
   float aspect=abs(_BlitTexture_TexelSize.z/max(1,_BlitTexture_TexelSize.w));
   // Geometry uses top-origin display coordinates. Keep texture coordinates separate.
   float2 screenUV=float2(i.uv.x,1-i.uv.y);
   float2 center=float2(_CenterX,_CenterY),scale=float2(aspect,1),p=(screenUV-center)*scale;
   float radius=length(p),amount=saturate(_Intensity);
   float2 direction=p/max(radius,0.0001);
   float wave=sin(radius*_WaveFrequency-_WavePhase*6.283185)*exp2(-radius*1.4);
   float2 warped=p+direction*wave*_WaveAmount*amount;
   warped*=1+_Collapse*amount*exp2(-radius*2)*1.5;
   float2 sampleScreen=saturate(center+warped/scale);
   float3 color=tex2D(_BlitTexture,float2(sampleScreen.x,1-sampleScreen.y)).rgb*lerp(1,_BackgroundTint.rgb,amount)*(1-_BaseDim*amount);
   if(_LineAmount>0.0001)color+=GoldStrands(warped,aspect)*amount;
   if(_RushMix>0.0001&&amount>0.0001) {
    float reveal=(_PortalRadius<=0)?0:1-smoothstep(_PortalRadius-0.055,_PortalRadius,radius);
    float front=exp2(-pow((radius-_PortalRadius)/0.014,2));
    float halo=exp2(-abs(radius-_PortalRadius)/0.035)*0.12;
    float visible=smoothstep(0,0.04,_PortalRadius)*(1-smoothstep(1.25,1.5,_PortalRadius));
    color=lerp(color,CellularRush(p),reveal*saturate(_RushMix*amount));
    color+=(front+halo)*visible*float3(0.24,0.08,0.65)*_RushMix*amount*0.35;
   }
   return half4(color,1);
  }
  ENDHLSL
 } }
 Fallback Off
}
