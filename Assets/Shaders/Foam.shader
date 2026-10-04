// Breaking foam around a charted reef (or along a shoal): a noisy, animated band that only shows
// while the hazard is charted, brightest where the beam falls on it. UVs span -1..1.
//   _Shape: x inner radius, y outer radius, z aspect (x stretch for shoals)
Shader "LL/Foam"
{
    Properties
    {
        _Color("Foam", Color) = (0.85, 0.92, 1, 1)
        _Shape("Shape (inner, outer, stretch)", Vector) = (0.25, 0.95, 1, 0)
        _Amount("Charted Amount", Range(0, 1)) = 1
        _Seed("Seed", Float) = 0
        _ChartColor("Chart Ink", Color) = (0.45, 0.75, 1.0, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
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
                half4 _Color, _ChartColor;
                float4 _Shape;
                float _Amount, _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;
                float2 wp = i.positionWS.xz;
                // Organic, wobbling outline around the hazard.
                float wob = (LLFbm2(wp * 0.35 + float2(t * 0.12, _Seed * 3.1)) - 0.5) * 0.32;
                float r = length(uv) + wob;
                float edge = _Shape.y * 0.5;
                // White water hugging the rock, and surges rolling outwards.
                float hug = exp(-pow((r - edge) / 0.16, 2.0));
                float surgeT = frac(t * 0.32 + _Seed * 0.37);
                float surge = exp(-pow((r - (edge + surgeT * 0.42)) / 0.05, 2.0)) * (1.0 - surgeT);
                float surgeT2 = frac(t * 0.32 + _Seed * 0.37 + 0.5);
                surge += exp(-pow((r - (edge + surgeT2 * 0.42)) / 0.05, 2.0)) * (1.0 - surgeT2);
                float churn = LLFbm2(wp * 1.3 + float2(t * 0.5, -t * 0.4) + _Seed * 7.0);
                float inner = (1.0 - smoothstep(edge * 0.4, edge, r)) * 0.55;   // broken water over the rock
                float spray = LLNoise2(wp * 4.0 + float2(t * 1.3, t * 0.7) + _Seed);
                float field = hug * (0.35 + churn * 1.1) + surge * 0.7 * churn + inner * churn;
                float foam = smoothstep(0.42, 0.72, field) * (0.7 + 0.3 * spray);
                foam *= 1.0 - smoothstep(0.82, 0.98, length(uv));
                float beam = LLBeam2D(wp);
                Light moon = GetMainLight();
                float3 lightCol = float3(0.22, 0.25, 0.3) + moon.color * _LLAmbient.w * 0.18 + _LLBeamColor.rgb * beam * 0.85 + _LLFlash;
                float3 col = _Color.rgb * foam * lightCol;
                return half4(col * _Amount, 1);
            }
            ENDHLSL
        }
    }
}
