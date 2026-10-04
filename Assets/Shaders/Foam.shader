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
                float r = length(i.uv);
                float t = _Time.y;
                float n = LLFbm2(i.positionWS.xz * 0.9 + float2(t * 0.25, -t * 0.18) + _Seed * 13.0);
                float surge = 0.5 + 0.5 * sin(t * 1.3 + _Seed * 7.0 - r * 6.0);
                float band = smoothstep(_Shape.x, _Shape.x + 0.25, r + n * 0.25) * (1.0 - smoothstep(_Shape.y - 0.3, _Shape.y, r + n * 0.15));
                float foam = band * smoothstep(0.35, 0.65, n + surge * 0.25);
                // Faint keeper's-chart ring so a charted hazard reads even away from the beam.
                float ink = (1.0 - smoothstep(0.015, 0.05, abs(r - _Shape.y * 0.98))) * 0.6;
                float beam = LLBeam2D(i.positionWS.xz);
                Light moon = GetMainLight();
                float3 lightCol = _LLAmbient.rgb * 2.0 + moon.color * _LLAmbient.w * 0.15 + _LLBeamColor.rgb * beam * 0.9 + _LLFlash;
                float3 col = _Color.rgb * foam * lightCol + _ChartColor.rgb * ink * 0.35;
                return half4(col * _Amount, 1);
            }
            ENDHLSL
        }
    }
}
