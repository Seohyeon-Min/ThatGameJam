// Fog layer: an unlit transparent quad whose opacity comes from a FogField's density mask, sampled by world XZ.
Shader "ThatGameJam/Fog"
{
    Properties
    {
        _Color ("Color", Color) = (0.88, 0.9, 0.94, 1)
        _Alpha ("Layer Alpha", Range(0, 1)) = 0.35
        _FogMask ("Fog Mask", 2D) = "white" {}
        _FogArea ("Fog Area (min x, min z, size x, size z)", Vector) = (0, 0, 1, 1)
        _Density ("Density", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FogMask);
            SAMPLER(sampler_FogMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                float4 _FogArea;
                half _Density;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = (input.positionWS.xz - _FogArea.xy) / _FogArea.zw;
                half mask = SAMPLE_TEXTURE2D(_FogMask, sampler_FogMask, uv).r;
                // Slow drifting variation so the fog does not look like a flat sheet.
                half drift = sin(input.positionWS.x * 0.8 + _Time.y * 0.3) * sin(input.positionWS.z * 0.7 - _Time.y * 0.2) * 0.12;
                // Soft border, so the stacked layers do not show hard edges.
                half2 border = smoothstep(0, 0.06, uv) * smoothstep(0, 0.06, 1 - uv);
                return half4(_Color.rgb, saturate(mask * (_Alpha + drift) * _Density) * border.x * border.y);
            }
            ENDHLSL
        }
    }
}
