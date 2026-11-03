Shader "InFalsusVFX/ParameterPreview"
{
    Properties
    {
        _MainTex("Background", 2D) = "white" {}
        _Brightness("Brightness", Float) = 1
        _Tint("Tint", Color) = (0.35,0.7,1,1)
        _Distortion("UV displacement", Float) = 0.15
        _Frequency("Spatial frequency", Float) = 6
        _Speed("Temporal frequency", Float) = 0.3
        _OffsetX("Horizontal offset", Float) = 0
        _OffsetY("Vertical offset", Float) = 0
        _PatternMix("Pattern mix", Float) = 1
        _PresentationTime("Presentation seconds", Float) = 0
        _Aspect("Viewport aspect", Float) = 1.7778
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            sampler2D _MainTex;
            float4 _Tint;
            float _Brightness,_Distortion,_Frequency,_Speed,_OffsetX,_OffsetY,_PatternMix,_PresentationTime,_Aspect;
            Output Vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            half4 Frag(Output i):SV_Target
            {
                float2 uv=i.uv+float2(_OffsetX,_OffsetY);
                float phase=_PresentationTime*_Speed;
                uv+=sin((uv.yx*max(abs(_Frequency),0.001)+phase)*6.2831853)*(_Distortion*0.025);
                float2 p=(uv-0.5)*float2(_Aspect,1);
                float gradient=0.22+0.18*sin((p.x+p.y)*_Frequency+phase);
                float2 g=abs(frac((p+0.5)*max(abs(_Frequency),0.001))-0.5);
                float gridLine=1-smoothstep(0.012,0.025,min(g.x,g.y));
                float3 pattern=(gradient+gridLine*0.2).xxx;
                float3 image=tex2D(_MainTex,uv).rgb;
                return half4(lerp(image,pattern,saturate(_PatternMix))*_Tint.rgb*_Brightness,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
