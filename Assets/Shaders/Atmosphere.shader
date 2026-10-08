// Volumetric night air: a ray-march through the low sea mist and drifting fog banks, lit by the
// lighthouse beam (analytic, matching the gameplay wedge), the wreckers' false lights, the
// lantern's own halo, moonlight and lightning. Runs as a Full Screen Pass before post-processing,
// so bloom picks up the shafts. The sea is not in the depth texture (it renders after the copy),
// so rays stop at the y = 0 plane analytically.
Shader "LL/Atmosphere"
{
    Properties
    {
        _Steps("Steps", Float) = 28
        _HazeDensity("Haze Density", Float) = 0.010
        _HazeHeight("Haze Height", Float) = 9
        _FogDensity("Fog Bank Density", Float) = 0.11
        _ScatterGain("Scatter Gain", Float) = 1.0
        _Extinction("Haze Extinction", Float) = 0.35
        _FogExtinction("Fog Extinction", Float) = 1.0
        _AmbientScatter("Ambient Scatter", Color) = (0.05, 0.07, 0.1, 1)
        _LanternGlow("Lantern Glow", Float) = 1.0
        _BeamScatter("Beam Scatter", Float) = 1.6
        _HazeNoise("Haze Noise", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Atmosphere"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment frag
            // Graphics fidelity Ultra: a finer octave in the fog banks.
            #pragma multi_compile _ LL_FIDELITY_ULTRA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "LLCommon.hlsl"

            float _Steps, _HazeDensity, _HazeHeight, _FogDensity, _ScatterGain, _Extinction, _FogExtinction, _LanternGlow, _BeamScatter, _HazeNoise;
            float4 _AmbientScatter;
            float _LLAtmoSteps, _LLFrameIndex;

            // The haze wisp noise doubles as the fog's coarse octave; only the fine one is extra.
            float FogNoise(float3 p, float wisp)
            {
                float3 q = p * float3(0.103, 0.207, 0.103) + float3(_Time.y * 0.08, 0, _Time.y * 0.046) + 5.1;
                float n = wisp * 0.62 + LLNoise3(q) * 0.38;
                #if defined(LL_FIDELITY_ULTRA)
                // Curling detail at a few metres, drifting faster than the bank it's in.
                float3 q2 = q * 2.7 + float3(-_Time.y * 0.11, 0.0, _Time.y * 0.07) + 11.3;
                n = n * 0.86 + (LLNoise3(q2) - 0.5) * 0.24 + 0.07;
                #endif
                return saturate(n * 1.8 - 0.3);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float raw = SampleSceneDepth(uv);
                float3 posWS = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 ro = GetCameraPositionWS();
                float3 rd = posWS - ro;
                float sceneDist = length(rd);
                rd /= max(sceneDist, 1e-4);
                #if UNITY_REVERSED_Z
                bool sky = raw <= 1e-6;
                #else
                bool sky = raw >= 1.0 - 1e-6;
                #endif
                if (sky) sceneDist = 2000.0;
                if (rd.y < -1e-4) sceneDist = min(sceneDist, (ro.y - 0.0) / -rd.y); // the sea plane

                const float top = 42.0;
                float t0 = 0.0, t1 = min(sceneDist, 700.0);
                if (rd.y < 0.0)
                {
                    if (ro.y > top) t0 = (ro.y - top) / -rd.y;
                }
                else
                {
                    if (ro.y > top) return color;
                    t1 = min(t1, (top - ro.y) / max(rd.y, 1e-4));
                }
                if (t1 <= t0) return color;

                int steps = (int)(_LLAtmoSteps > 0 ? _LLAtmoSteps : _Steps);
                float dt = (t1 - t0) / steps;
                #if defined(LL_FIDELITY_ULTRA)
                float jitter = LLIGN(input.positionCS.xy + _LLFrameIndex * 5.588238);
                #else
                float jitter = LLIGN(input.positionCS.xy + frac(_Time.y * 7.0) * 64.0);
                #endif
                float transmittance = 1.0;
                float3 scatter = 0;
                Light moon = GetMainLight();
                float3 ambientLight = _AmbientScatter.rgb + moon.color * 0.04 * _LLAmbient.w;
                float3 lantern = _LLBeamOrigin.xyz;

                [loop] for (int k = 0; k < steps; k++)
                {
                    float t = t0 + (k + jitter) * dt;
                    float3 p = ro + rd * t;
                    float haze = _HazeDensity * _LLHaze * exp(-max(p.y, 0.0) / _HazeHeight);
                    float wisp = LLNoise3(p * float3(0.028, 0.08, 0.028) + float3(_Time.y * 0.03, 0.0, _Time.y * 0.012));
                    haze *= lerp(1.0, wisp * 1.8, _HazeNoise);
                    float bank = LLFogBankDensity(p);
                    float fog = 0.0;
                    if (bank > 0.001) fog = bank * FogNoise(p, wisp) * _FogDensity;
                    float dens = haze + fog;

                    // Beam in-scatter with a forward-scattering lobe towards the viewer.
                    float beam = LLBeam3D(p);
                    float3 lightDir = normalize(p - lantern);
                    float cosT = dot(lightDir, -rd);
                    float phase = 0.55 + 1.6 * pow(saturate(cosT), 6.0);
                    float3 light = _LLBeamColor.rgb * beam * phase * _BeamScatter;
                    // Fog banks catch the moon: a soft grey veil over the dark sea.
                    float fogShare = fog / max(dens, 1e-5);
                    light += (moon.color * 0.2 * _LLAmbient.w + _LLAmbient.rgb * 2.0) * fogShare;
                    light += _LLFalseColor.rgb * LLFalseBeams(p) * 0.9;
                    float dl = length(p - lantern);
                    light += _LLBeamColor.rgb * _LLBeamOrigin.w * _LanternGlow * 3.0 / (1.0 + dl * dl * 0.09);
                    light += ambientLight + float3(0.7, 0.8, 1.0) * _LLFlash * 2.5;

                    scatter += transmittance * dens * light * dt * _ScatterGain;
                    transmittance *= exp(-(haze * _Extinction + fog * _FogExtinction) * dt);
                    if (transmittance < 0.01) break;
                }
                return half4(color.rgb * transmittance + scatter, color.a);
            }
            ENDHLSL
        }
    }
}
