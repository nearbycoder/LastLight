// The bright core of the beam right out of the lens: an additive cone that is brightest along
// its axis (soft view-dependent edges) and fades with length. The long shaft itself comes from
// the volumetric Atmosphere pass.
Shader "LL/BeamCore"
{
    Properties
    {
        [HDR] _Color("Colour", Color) = (1.4, 1.1, 0.7, 1)
        _Intensity("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float facing = abs(dot(normalize(i.normalWS), V));
                float edge = pow(facing, 2.5);
                float along = i.uv.y;
                float fade = (1.0 - along) * (1.0 - along) * smoothstep(0.0, 0.06, along);
                return half4(_Color.rgb * edge * fade * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
