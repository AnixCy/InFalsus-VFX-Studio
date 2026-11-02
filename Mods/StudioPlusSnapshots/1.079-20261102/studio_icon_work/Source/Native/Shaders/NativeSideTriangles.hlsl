// GameplayWall vertex program 1, with native shading coordinates supplied
// separately from the editor's projection coordinates. Fragment code is native.
cbuffer Globals : register(b0) { float4 Frame[83]; };
cbuffer UnityPerDraw : register(b1) { float4 Draw[7]; };
struct Input { float3 position:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float4 uv:TEXCOORD0; };
struct Output { float4 position:SV_POSITION;float4 uv:INTERP0;float4 effect:INTERP1;float3 world:INTERP2;float3 normal:INTERP3; };
Output main(Input i) {
 Output o;
 float3 w=(Draw[0]*i.position.x+Draw[1]*i.position.y+Draw[2]*i.position.z+Draw[3]).xyz;
 o.position=Frame[79]*w.x+Frame[80]*w.y+Frame[81]*w.z+Frame[82];
 o.uv=i.uv;
 float q=min((abs(i.uv.z+10)+8)*(1.0/24.0),1.0);
 float s=q*q*(3-2*q);
 float a=saturate((1.025-s)*10);
 o.effect=float4(a*a*(3-2*a),2+10*s,i.uv.w/(3+10*s),0);
 o.world=w;
 o.normal=normalize(float3(dot(i.normal,Draw[4].xyz),dot(i.normal,Draw[5].xyz),dot(i.normal,Draw[6].xyz)));
 return o;
}
