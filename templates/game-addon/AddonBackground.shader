Shader "InFalsusVFX/AddonBackground"
{
    Properties
    {
        _MainTex("Background",2D)="white" {}
        _Brightness("Brightness",Float)=1
        _Tint("Tint",Color)=(1,1,1,1)
        _Distortion("UV displacement",Float)=0.15
        _Frequency("Spatial frequency",Float)=6
        _Speed("Temporal frequency",Float)=0.3
        _OffsetX("Horizontal offset",Float)=0
        _OffsetY("Vertical offset",Float)=0
        _PatternMix("Pattern mix",Float)=1
        _PresentationTime("Presentation seconds",Float)=0
        _Aspect("Viewport aspect",Float)=1.7778
        _BaseColor("Base Color",Color)=(1,1,1,1)
        _Exposure("Exposure",Float)=0
        _Contrast("Contrast",Float)=1
        _Saturation("Saturation",Float)=1
        _GradeOpacity("Color grading opacity",Float)=1
        _GradeEnabled("Color grading enabled",Float)=0
        _GradeStart("Color grading start",Float)=0
        _GradeEnd("Color grading end",Float)=1000000000
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            sampler2D _MainTex;
            float4 _Tint,_BaseColor;
            float _Brightness,_Distortion,_Frequency,_Speed,_OffsetX,_OffsetY,_PatternMix,_PresentationTime,_Aspect;
            float _Exposure,_Contrast,_Saturation,_GradeOpacity,_GradeEnabled,_GradeStart,_GradeEnd;
            Output Vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            half4 Frag(Output i):SV_Target
            {
                float2 uv=i.uv+float2(_OffsetX,_OffsetY);
                float phase=_PresentationTime*_Speed;
                uv+=sin((uv.yx*max(abs(_Frequency),.001)+phase)*6.2831853)*(_Distortion*.025);
                float2 p=(uv-.5)*float2(_Aspect,1);
                float gradient=.22+.18*sin((p.x+p.y)*_Frequency+phase);
                float2 g=abs(frac((p+.5)*max(abs(_Frequency),.001))-.5);
                float gridLine=1-smoothstep(.012,.025,min(g.x,g.y));
                float3 color=lerp(tex2D(_MainTex,uv).rgb,(gradient+gridLine*.2).xxx,saturate(_PatternMix))*_Tint.rgb*_Brightness;
                float grey=dot(color,float3(.2126,.7152,.0722));
                float3 graded=(lerp(grey.xxx,color,_Saturation)-.18)*_Contrast+.18;
                graded*=exp2(_Exposure)*_BaseColor.rgb;
                float active=_GradeEnabled*step(_GradeStart,_PresentationTime)*(1-step(_GradeEnd,_PresentationTime));
                return half4(lerp(color,graded,saturate(_GradeOpacity)*active),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
