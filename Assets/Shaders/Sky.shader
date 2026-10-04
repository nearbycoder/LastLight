// Night sky: deep gradient, twinkling stars, the moon with its halo and thin moonlit cloud.
// _LLDawn (0..1) blends towards the sunrise used by the ending.
Shader "LL/Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.008, 0.014, 0.035, 1)
        _Horizon("Horizon", Color) = (0.05, 0.085, 0.14, 1)
        _MoonColor("Moon", Color) = (0.85, 0.9, 1.0, 1)
        _MoonSize("Moon Size", Float) = 0.9993
        _StarDensity("Star Density", Float) = 1
        _DawnHorizon("Dawn Horizon", Color) = (1.0, 0.55, 0.32, 1)
        _DawnZenith("Dawn Zenith", Color) = (0.25, 0.38, 0.62, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "LLCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _MoonColor, _DawnHorizon, _DawnZenith;
                float _MoonSize, _StarDensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Stars(float3 d)
            {
                float s = 0;
                [unroll] for (int layer = 0; layer < 2; layer++)
                {
                    float scale = layer == 0 ? 180.0 : 420.0;
                    float3 p = d * scale;
                    float3 cell = floor(p);
                    float h = LLHash31(cell + layer * 17.0);
                    float threshold = layer == 0 ? 0.985 : 0.992;
                    if (h > threshold)
                    {
                        float3 c = cell + 0.5 + (float3(LLHash31(cell + 3.1), LLHash31(cell + 7.7), LLHash31(cell + 11.3)) - 0.5) * 0.6;
                        float dist = length(p - c);
                        float tw = 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 4.0) + h * 40.0);
                        float b = (h - threshold) / (1.0 - threshold);
                        s += smoothstep(0.45, 0.0, dist) * tw * (0.35 + b * 1.6) * (layer == 0 ? 1.0 : 0.55);
                    }
                }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float dawn = _LLDawn;
                float3 zen = lerp(_Zenith.rgb, _DawnZenith.rgb, dawn);
                float3 hor = lerp(_Horizon.rgb, _DawnHorizon.rgb, dawn);
                float3 sky = lerp(hor, zen, pow(saturate(h), 0.45));
                if (h < 0) sky = hor * 0.8;

                Light moon = GetMainLight();
                float md = dot(d, moon.direction);
                // Halo and disc.
                sky += _MoonColor.rgb * (pow(saturate(md), 18.0) * 0.07 + pow(saturate(md), 400.0) * 0.25) * (1.0 - dawn);
                float disc = smoothstep(_MoonSize, _MoonSize + 0.00015, md);
                float crater = LLNoise2(d.xy * 900.0) * 0.25 + 0.75;
                sky += _MoonColor.rgb * disc * 2.6 * crater * (1.0 - dawn * 0.8);

                // Stars fade near the horizon, behind the moon's glare and at dawn.
                float starMask = saturate(h * 6.0) * (1.0 - pow(saturate(md), 8.0)) * (1.0 - dawn);
                sky += Stars(d) * starMask * _StarDensity * float3(0.9, 0.95, 1.0);

                // Thin cloud lit by the moon.
                if (h > 0.0)
                {
                    float2 uv = d.xz / (h + 0.12) * 1.3 + float2(_Time.y * 0.003, _Time.y * 0.001);
                    float c = smoothstep(0.5, 0.82, LLFbm2(uv));
                    c *= smoothstep(0.0, 0.25, h);
                    float3 cloud = lerp(hor * 1.2, _MoonColor.rgb * 0.16, pow(saturate(md * 0.5 + 0.5), 4.0));
                    cloud = lerp(cloud, float3(1.0, 0.7, 0.55), dawn * 0.6);
                    sky = lerp(sky, cloud, c * 0.75);
                }
                sky += float3(0.6, 0.7, 0.9) * _LLFlash * 0.6;
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
