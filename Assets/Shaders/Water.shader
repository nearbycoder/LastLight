// The night sea. Gerstner swell, procedural ripples, moon glitter, the beam pooling on the water
// with glints on every ripple, reflections of ship lamps (Forward+ light loop) and foam wherever
// anything breaks the surface (depth-based intersection foam). Rendered early in the transparent
// queue so _CameraDepthTexture still holds the rocks, hulls and cliffs underneath it.
Shader "LL/Water"
{
    Properties
    {
        _DeepColor("Deep", Color) = (0.02, 0.05, 0.075, 1)
        _ShallowColor("Shallow", Color) = (0.05, 0.13, 0.15, 1)
        _SkyZenith("Sky Zenith (reflection)", Color) = (0.02, 0.035, 0.07, 1)
        _SkyHorizon("Sky Horizon (reflection)", Color) = (0.1, 0.15, 0.22, 1)
        _FoamColor("Foam", Color) = (0.75, 0.82, 0.9, 1)
        _WaveScale("Wave Scale", Float) = 0.35
        _Choppiness("Choppiness", Float) = 1
        _FarFade("Far fade start/end", Vector) = (260, 900, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }
        ZWrite On
        ZTest LEqual
        Blend Off
        Cull Back

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // Graphics fidelity: Low drops the finest detail, Ultra adds a finer sea; High is neither.
            #pragma multi_compile _ LL_FIDELITY_LOW LL_FIDELITY_ULTRA
            // The moon's shadows across its glitter (Ultra).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "LLCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _SkyZenith, _SkyHorizon, _FoamColor;
                float _WaveScale, _Choppiness;
                float4 _FarFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float crest : TEXCOORD3;
            };

            // Four Gerstner waves: direction (x, z), wavelength, steepness.
            static const float4 kWaves[4] =
            {
                float4(0.32, 0.95, 31.0, 0.16),
                float4(-0.55, 0.83, 17.0, 0.18),
                float4(0.85, 0.52, 11.0, 0.14),
                float4(-0.2, -0.98, 7.0, 0.10),
            };

            void Gerstner(float2 xz, float t, out float3 offset, out float3 normal, out float crest)
            {
                offset = 0;
                float3 n = float3(0, 1, 0);
                crest = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 d = normalize(kWaves[i].xy);
                    float k = 6.2831853 / kWaves[i].z;
                    float c = sqrt(9.8 / k) * 0.55;
                    float steep = kWaves[i].w * _Choppiness;
                    float a = steep / k * _WaveScale;
                    float f = k * (dot(d, xz) - c * t);
                    float s = sin(f), co = cos(f);
                    offset += float3(d.x * a * co * steep, a * s, d.y * a * co * steep);
                    n.x -= d.x * k * a * co;
                    n.z -= d.y * k * a * co;
                    n.y -= steep * k * a * s;
                    crest += s * steep;
                }
                normal = normalize(n);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 offset, normal;
                float crest;
                // Swell calms with distance so the horizon reads flat.
                float distFade = 1.0 - saturate((length(ws.xz) - 220.0) / 500.0);
                Gerstner(ws.xz, _Time.y, offset, normal, crest);
                ws += offset * distFade;
                o.positionWS = ws;
                o.normalWS = normalize(lerp(float3(0, 1, 0), normal, distFade));
                o.positionCS = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.crest = crest * distFade;
                return o;
            }

            // Small ripples: a handful of directional sines, analytic slope.
            float3 Ripples(float2 xz, float t, float strength)
            {
                float2 slope = 0;
                const float4 r[6] =
                {
                    float4(0.9, 0.4, 3.1, 1.3), float4(-0.6, 0.8, 2.3, -1.1), float4(0.2, -1.0, 1.7, 1.7),
                    float4(-0.95, -0.3, 1.21, -1.9), float4(0.7, -0.7, 0.83, 2.3), float4(-0.3, 0.95, 0.61, 2.9),
                };
                [unroll] for (int i = 0; i < 6; i++)
                {
                    float2 d = normalize(r[i].xy);
                    float k = 6.2831853 / r[i].z;
                    float f = k * dot(d, xz) + t * r[i].w;
                    slope += d * cos(f) * (0.11 + 0.02 * i);
                }
                float n = LLNoise2(xz * 0.35 + t * 0.15) - 0.5;
                slope += float2(n, -n) * 0.25;
                #if defined(LL_FIDELITY_ULTRA)
                // A finer cat's-paw octave: short, quick ripples that break the glints up further.
                const float4 f[4] =
                {
                    float4(0.8, -0.6, 0.43, 3.4), float4(-0.45, -0.9, 0.31, -3.9),
                    float4(0.25, 0.97, 0.23, 4.4), float4(-0.99, 0.15, 0.17, -5.1),
                };
                [unroll] for (int j = 0; j < 4; j++)
                {
                    float2 d = normalize(f[j].xy);
                    float k = 6.2831853 / f[j].z;
                    slope += d * cos(k * dot(d, xz) + t * f[j].w) * 0.045;
                }
                float n2 = LLNoise2(xz * 1.3 - t * 0.35) - 0.5;
                slope += float2(-n2, n2) * 0.1;
                #endif
                return normalize(float3(-slope.x * strength, 1.0, -slope.y * strength));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 pos = i.positionWS;
                float3 camPos = GetCameraPositionWS();
                float3 V = normalize(camPos - pos);
                float camDist = length(camPos - pos);
                float t = _Time.y;
                float detailFade = 1.0 - saturate((camDist - 120.0) / 360.0);
                float3 rip = Ripples(pos.xz, t, 0.3 * detailFade + 0.06);
                float3 N = normalize(float3(i.normalWS.x + rip.x, i.normalWS.y, i.normalWS.z + rip.z));

                // Large wind patches break up the surface.
                float wind = LLFbm2(pos.xz * 0.011 + float2(t * 0.008, t * 0.004));
                float ndv = saturate(dot(N, V));
                float fresnel = 0.025 + 0.975 * pow(1.0 - ndv, 5.0);

                Light moon = GetMainLight(TransformWorldToShadowCoord(pos));
                float moonB = _LLAmbient.w * moon.shadowAttenuation;
                float3 R = reflect(-V, N);
                float3 sky = lerp(_SkyHorizon.rgb, _SkyZenith.rgb, saturate(R.y * 2.0));
                sky = lerp(sky, float3(0.95, 0.55, 0.35), _LLDawn * saturate(1.0 - R.y * 3.0));
                float3 reflection = sky * (0.75 + wind * 0.6);

                // Moon glitter: a broad sparkle that follows the ripples.
                // Moon path: a smooth glow where a calm sea would mirror the moon, broken up by
                // sparse ripple sparkles inside it.
                float3 Rflat = reflect(-V, float3(0, 1, 0));
                float path = pow(saturate(dot(Rflat, moon.direction)), 10.0);
                float3 Hm = normalize(moon.direction + V);
                float nhm = saturate(dot(N, Hm));
                #if defined(LL_FIDELITY_LOW)
                float sparkleNoise = 0.35;
                #else
                float sparkleNoise = LLSmooth(0.7, 0.92, LLNoise2(pos.xz * 2.6 + float2(t * 0.9, -t * 0.6)));
                #endif
                float sparkle = pow(nhm, 600.0) * 7.0 * sparkleNoise;
                float3 moonGlint = moon.color * moonB * (path * (0.1 + wind * 0.12) + sparkle * (0.025 + path * 1.9));

                // Body colour: deep water, lighter on crests and in the wind patches.
                float3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(i.crest * 0.7 + 0.2 + (wind - 0.5) * 0.5));
                float3 ambient = _LLAmbient.rgb;

                // Shallows over rock and sand (from the depth texture).
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float surfDepth = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float under = max(0.0, sceneDepth - surfDepth) * saturate(V.y + 0.2);
                float shallow = saturate(1.0 - under / 4.5);

                // The beam: a warm pool, hottest near the lantern, textured by the ripples, glinting.
                float beam = LLBeam2D(pos.xz);
                float dist = length(pos.xz - _LLBeamOrigin.xz);
                float hot = 0.25 + 0.75 * exp(-dist / (_LLBeamParams.x * 0.3));
                float3 toLamp = normalize(_LLBeamOrigin.xyz - pos);
                float ndl = saturate(dot(N, toLamp));
                float mottle = 0.45 + 0.75 * LLFbm2(pos.xz * 0.11 + float2(t * 0.04, -t * 0.03)) * (0.7 + 0.6 * saturate(dot(N, toLamp) * 3.0 - 0.2));
                float3 Hb = normalize(toLamp + V);
                float nhb = saturate(dot(N, Hb));
                float glint = pow(nhb, 180.0) * 7.0 + pow(nhb, 22.0) * 0.4;
                float3 beamLight = _LLBeamColor.rgb * beam * (hot * mottle * (0.1 + 0.4 * ndl) + glint * (0.6 + hot * 0.6));
                beamLight += _LLBeamColor.rgb * beam * shallow * 0.55 * hot;   // the reef shows under the light
                float falseB = LLFalseBeams(pos);
                beamLight += _LLFalseColor.rgb * falseB * (0.1 + glint * 0.6);

                // Ship lamps and harbour lights reflecting on the swell.
                float3 lamps = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = pos;
                inputData.normalizedScreenSpaceUV = uv;
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light l = GetAdditionalLight(lightIndex, pos);
                    // A soft column where the calm sea would mirror the lamp, glittering with the
                    // ripples inside it. (Ripple slopes alone are box-bounded: rectangular glints.)
                    float3 H = normalize(l.direction + V);
                    float column = pow(saturate(dot(Rflat, l.direction)), 10.0);
                    float glitter = pow(saturate(dot(N, H)), 60.0);
                    float spec = column * (0.35 + glitter * 4.0);
                    lamps += l.color * l.distanceAttenuation * (spec + 0.03);
                LIGHT_LOOP_END
                #endif

                // Foam wherever anything breaks the surface.
                float foamNoise = LLNoise2(pos.xz * 1.4 + float2(t * 0.5, t * 0.27));
                float foam = saturate(1.0 - under / (0.25 + foamNoise * 0.35));
                foam *= LLSmooth(0.1, 0.6, foamNoise + foam * 0.5);
                body = lerp(body, _ShallowColor.rgb * 1.5, shallow * 0.35 * saturate(beam * 2.0 + _LLFlash));

                // Whitecaps when the swell is up (storm nights raise _WaveScale).
                float blow = saturate((_WaveScale - 0.45) * 3.0);
                // Streaks along the wind, breaking only on the highest crests, in patches.
                float2 wq = float2(pos.x * 0.18 + pos.z * 0.05, pos.z * 0.45 - pos.x * 0.02);
                float patches = LLSmooth(0.45, 0.8, LLNoise2(pos.xz * 0.025 + t * 0.03));
                float streak = LLNoise2(wq + float2(t * 0.5, t * 0.2));
                float lace = lerp(0.45, LLNoise2(wq * 2.2 - t * 0.6), detailFade);
                float caps = blow * patches * LLSmooth(0.78, 1.05, i.crest * 1.4 + streak * 0.55) * LLSmooth(0.4, 0.7, lace);
                foam = max(foam, caps * 0.7);
                float flash = _LLFlash;
                float3 col = body * (ambient * 1.4 + moon.color * moonB * 0.05 + flash * 0.5) + reflection * fresnel + moonGlint + beamLight + lamps;
                float3 foamLight = ambient * 2.2 + moon.color * moonB * 0.18 + _LLBeamColor.rgb * beam * hot * 0.9 + _LLFalseColor.rgb * falseB * 0.7 + flash + lamps * 0.5;
                // Foam is white water: never darker than the sea it breaks on.
                float3 foamCol = max(_FoamColor.rgb * foamLight, col * 1.5 + 0.015);
                col = lerp(col, foamCol, foam * 0.8);

                // Fade the far sea into the horizon haze.
                float far = saturate((camDist - _FarFade.x) / (_FarFade.y - _FarFade.x));
                col = lerp(col, _SkyHorizon.rgb * 0.95, far * 0.75);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
