// Soft round particles. "LL/ParticleLit" is alpha-blended and lit like everything else (night
// ambient, moon, the beam, lightning) so wakes, spray and smoke brighten when the light passes;
// "LL/ParticleAdd" is additive and self-lit for sparks, flares and embers.
Shader "LL/ParticleLit"
{
    Properties
    {
        _Softness("Softness", Float) = 2.0
        _Streak("Streak (rain)", Float) = 0
        _LightBoost("Light Boost", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LLCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Softness, _Streak, _LightBoost;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 light : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.color = v.color;
                o.uv = v.uv;
                Light moon = GetMainLight();
                float beam = LLBeam3D(ws);
                o.light = _LLAmbient.rgb * 2.4 + moon.color * _LLAmbient.w * 0.25 + _LLBeamColor.rgb * beam * 0.9
                        + _LLFalseColor.rgb * LLFalseBeams(ws) * 0.6 + _LLFlash * 1.5;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                if (_Streak > 0) d.x *= 3.0;
                float a = saturate(1.0 - dot(d, d));
                a = pow(a, _Softness);
                return half4(i.color.rgb * i.light * _LightBoost, i.color.a * a);
            }
            ENDHLSL
        }
    }
}
