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
        Blend One OneMinusSrcAlpha
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

            // Foam lace: thin bright veins where a noise field crosses its midline.
            float Lace(float2 p)
            {
                float n = LLNoise2(p) * 0.65 + LLNoise2(p * 2.1 + 9.3) * 0.35;
                return LLSmooth(0.72, 0.97, 1.0 - abs(n - 0.5) * 2.0);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;
                float2 wp = i.positionWS.xz;
                float len = length(uv);
                float2 dir = uv / max(len, 1e-4);
                float edge = _Shape.y * 0.5;
                // A ragged outline: the rock's reach varies around it and drifts slowly.
                float reach = LLNoise2(dir * 1.8 + _Seed * 5.3 + t * 0.04) * 0.7 + LLNoise2(dir * 4.1 + _Seed) * 0.3;
                float edgeR = edge * (0.6 + 0.8 * reach);
                float r = len + (LLNoise2(wp * 0.6 + t * 0.1) - 0.5) * 0.12;

                // Wet rock tips breaking the surface in the middle.
                float rockN = LLNoise2(wp * 0.9 + _Seed * 3.7);
                float rock = LLSmooth(0.55, 0.7, rockN) * (1.0 - LLSmooth(edgeR * 0.45, edgeR * 0.8, r));

                // Breakers around the rock, a seething lace over it, and surges rolling out on some sides.
                float churn = LLFbm2(wp * 1.1 + float2(t * 0.45, -t * 0.35) + _Seed * 7.0);
                float lace = Lace(wp * 0.95 + float2(t * 0.22, -t * 0.17) + _Seed * 2.0);
                float breaker = exp(-pow((r - edgeR * 0.85) / 0.15, 2.0)) * (0.2 + churn * 0.6 + lace * 0.6);
                float over = (1.0 - LLSmooth(edgeR * 0.4, edgeR * 1.05, r)) * (0.05 + lace * 0.85 + churn * 0.2);
                float surge = 0.0;
                [unroll] for (int k = 0; k < 2; k++)
                {
                    float st = frac(t * 0.3 + _Seed * 0.37 + k * 0.5);
                    float side = LLSmooth(0.35, 0.65, LLNoise2(dir * 1.5 + _Seed * 2.9 + k * 4.1 + floor(t * 0.3 + _Seed * 0.37 + k * 0.5) * 3.3));
                    surge += exp(-pow((r - edgeR * (0.9 + st * 0.6)) / 0.06, 2.0)) * (1.0 - st) * side;
                }
                surge *= 0.35 + lace;
                float foam = saturate(LLSmooth(0.3, 0.95, breaker + over + surge * 0.8) * (0.55 + 0.45 * churn));
                foam *= 1.0 - LLSmooth(0.8, 0.98, len);
                rock *= 1.0 - foam * 0.7;

                float beam = LLBeam2D(wp);
                Light moon = GetMainLight();
                float3 lightCol = float3(0.22, 0.25, 0.3) + moon.color * _LLAmbient.w * 0.18 + _LLBeamColor.rgb * beam * 0.85 + _LLFlash;
                float3 rockCol = float3(0.035, 0.04, 0.05) + _LLBeamColor.rgb * beam * 0.08 + _LLFlash * 0.2;
                float3 col = _Color.rgb * foam * lightCol + rockCol * rock;
                float alpha = saturate(foam * 0.85 + rock * 0.9);
                return half4(col * _Amount, alpha * _Amount);
            }
            ENDHLSL
        }
    }
}
