Shader "InFalsusVFX/FlowFeedbackBackground"
{
    Properties
    {
        _MainTex("Background",2D)="white" {}
        _BaseColor("Base Color",Color)=(1,1,1,1)
        [HideInInspector] _Color("Native scene color",Color)=(1,1,1,1)
        _Brightness("Brightness",Float)=1
        _EphemeralAmount("Flow Distortion",Range(0,2))=0
        _FeedbackAmount("Nested Scale",Range(0,2))=0
        _FeedbackRotation("Nested Rotation",Range(-2,2))=0
        _Center("Transform Center",Vector)=(0.5,0.9,0,0)
        _PresentationTime("Presentation seconds",Float)=0
        _Aspect("Viewport aspect",Float)=1.7778
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            sampler2D _MainTex;
            float4 _BaseColor,_Center,_Color;
            float _Brightness,_EphemeralAmount,_FeedbackAmount,_FeedbackRotation,_PresentationTime,_Aspect;
            Output Vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}

            float2 Flow(float2 uv)
            {
                // One representative flow sample. Multi-sample chromatic treatment and
                // blue-noise phase modulation are deliberately omitted in this light version.
                const float a2=0.5625;
                const float3x3 field=float3x3(
                    -1.7888544,0,-0.8944272,
                    -0.7807201,0.9759001,1.5614402,
                    0.4364358,1.7457432,-0.8728716);
                float2 p=(uv-_Center.xy)*(1-0.3*_EphemeralAmount*a2);
                float3 v=float3(6*p,_PresentationTime-a2);
                float2 accumulated=0;
                [unroll]for(int i=0;i<4;i++){
                    float3 h=mul(field,v);
                    accumulated=2*accumulated+sin(h.xy);
                    v=h+cos(h);
                }
                return _Center.xy+p+0.1*_EphemeralAmount*a2*(accumulated/15);
            }
            float2 Nested(float2 uv)
            {
                float scale=1+2*max(0,_FeedbackAmount),angle=_FeedbackRotation;
                [unroll]for(int i=0;i<4;i++){
                    float2 p=(uv-_Center.xy)*float2(_Aspect,1);
                    float s,c;sincos(angle,s,c);
                    float2 rotated=float2(c*p.x+s*p.y,-s*p.x+c*p.y);
                    float2 candidate=_Center.xy+scale*rotated/float2(_Aspect,1);
                    if(all(candidate>0)&&all(candidate<1))uv=candidate;
                    scale=sqrt(scale);angle*=0.5;
                }
                return uv;
            }
            half4 Frag(Output i):SV_Target
            {
                float2 uv=i.uv;
                if(abs(_EphemeralAmount)>0.000001)uv=Flow(uv);
                if(abs(_FeedbackAmount)+abs(_FeedbackRotation)>0.000001)uv=Nested(uv);
                return tex2D(_MainTex,uv)*_BaseColor*_Color*half4(_Brightness.xxx,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
