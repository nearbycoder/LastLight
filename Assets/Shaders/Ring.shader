// Flat additive ring decal lying on the water: confidence arcs around ships, buoy auras, the
// foghorn shockwave, the harbour mouth. UVs span -1..1 across the quad.
//   _Ring: x inner radius, y outer radius (0..1 of the quad), z edge softness
//   _Arc:  x fill 0..1 (clockwise from north), y dash count (0 = solid), z pulse speed, w pulse depth
Shader "LL/Ring"
{
    Properties
    {
        [HDR] _Color("Colour", Color) = (1, 1, 1, 1)
        _Ring("Ring (inner, outer, softness)", Vector) = (0.82, 0.92, 0.04, 0)
        _Arc("Arc (fill, dashes, pulse speed, pulse depth)", Vector) = (1, 0, 0, 0)
        _Fill("Inner Fill", Float) = 0
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Ring, _Arc;
                float _Fill;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv);
                float soft = max(_Ring.z, 1e-3);
                float band = smoothstep(_Ring.x - soft, _Ring.x, r) * (1.0 - smoothstep(_Ring.y, _Ring.y + soft, r));
                // Angle clockwise from +V (north), 0..1.
                float ang = atan2(i.uv.x, i.uv.y) / 6.2831853;
                ang = frac(ang + 1.0);
                float arc = _Arc.x >= 0.999 ? 1.0 : smoothstep(_Arc.x + 0.004, _Arc.x - 0.004, ang);
                float dash = _Arc.y > 0 ? step(0.45, frac(ang * _Arc.y)) : 1.0;
                float pulse = 1.0 - _Arc.w * (0.5 + 0.5 * sin(_Time.y * _Arc.z));
                float fill = _Fill * (1.0 - smoothstep(_Ring.x - soft, _Ring.x, r)) * (0.4 + 0.6 * r);
                float a = (band * arc * dash + fill) * pulse;
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
}
