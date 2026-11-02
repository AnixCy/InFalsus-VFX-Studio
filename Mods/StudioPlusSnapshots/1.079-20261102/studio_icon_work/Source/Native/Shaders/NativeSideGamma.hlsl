cbuffer UnityPerMaterial:register(b1){float4 Parameters;float4 Unused;};
Texture2D<float4> Atlas:register(t0);
Texture2D<float4> Background:register(t1);
SamplerState Sampler:register(s0);
struct Input{float4 position:SV_POSITION;float2 uv:INTERP0;};
float3 toGamma(float3 x){x=max(0,x);return lerp(x*12.92,1.055*pow(x,1./2.4)-.055,step(.0031308,x));}
float3 toLinear(float3 x){x=max(0,x);return lerp(x/12.92,pow((x+.055)/1.055,2.4),step(.04045,x));}
float4 main(Input i):SV_Target{
 float2 uv=i.uv+float2(0,Parameters.w);
 // Match the native repeat sampler even on the editor's clamped atlas binding.
 float4 ink=Atlas.SampleGrad(Sampler,frac(uv),ddx(uv),ddy(uv));
 float4 bg=Background.Sample(Sampler,i.position.xy/Parameters.xy);
 float3 baseGamma=Parameters.z>.5?toGamma(bg.rgb):bg.rgb;
 float3 inkGamma=Parameters.z>.5?toGamma(ink.rgb):ink.rgb;
 float3 blended=lerp(baseGamma,inkGamma,saturate(ink.a));
 return float4(Parameters.z>.5?toLinear(blended):blended,bg.a);
}
