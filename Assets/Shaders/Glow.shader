// Camera-facing additive glow for lamps (running lights, windows, buoy lamps, the lantern).
// Quad mesh with corners at +-0.5; size comes from the object's scale. Pulled towards the camera
// a little so a lamp sitting on a mast is not clipped by it.
Shader "LL/Glow"
{
    Properties
    {
        [HDR] _Color("Colour", Color) = (1, 0.8, 0.5, 1)
        _Core("Core Sharpness", Float) = 18
        _Halo("Halo", Float) = 0.35
        _Pull("Pull Towards Camera", Float) = 0.6
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Core, _Halo, _Pull;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float3 scaleX = TransformObjectToWorldDir(float3(1, 0, 0), false);
                float size = length(scaleX);
                float3 camPos = GetCameraPositionWS();
                float3 toCam = normalize(camPos - center);
                center += toCam * _Pull;
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 ws = center + (right * v.positionOS.x + up * v.positionOS.y) * size;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.positionOS.xy * 2.0;
                float4 inst = UNITY_ACCESS_INSTANCED_PROP(Props, _InstColor);
                o.color = inst.a > 0 ? inst : _Color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                float core = exp(-r2 * _Core);
                float halo = exp(-r2 * 3.0) * _Halo;
                float a = (core + halo) * saturate(1.0 - r2);
                return half4(i.color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
}
