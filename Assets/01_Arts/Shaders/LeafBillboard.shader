// 잎 빌보드: 오브젝트 원점을 기준으로 Quad를 카메라 쪽으로 세운다.
// Quad와 기본 Plane에 쓴다. Static Batching을 켜면 원점 정보가 사라져 동작하지 않는다.
Shader "ThatGameJam/LeafBillboard"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
        CBUFFER_END

        // 로컬 정점을 카메라의 오른쪽/위 방향으로 펼친다.
        // Quad는 XY, 기본 Plane은 XZ 평면이라 Y와 Z를 더해 둘 다 받는다.
        float3 BillboardPositionWS(float3 positionOS)
        {
            float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
            float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
            float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
            float scaleZ = length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z));
            float3 rightWS = UNITY_MATRIX_V[0].xyz;
            float3 upWS = UNITY_MATRIX_V[1].xyz;
            float up = positionOS.y * scaleY + positionOS.z * scaleZ;
            return originWS + rightWS * positionOS.x * scaleX + upWS * up;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = BillboardPositionWS(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                // 잎은 항상 카메라를 보므로 법선도 카메라 방향으로 둔다.
                float3 normalWS = -UNITY_MATRIX_V[2].xyz;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 diffuse = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5)
                              * mainLight.shadowAttenuation;
                half3 ambient = SampleSH(normalWS);
                return half4(albedo.rgb * (diffuse + ambient), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // 그림자 패스에서는 뷰 행렬이 라이트 기준이라 잎이 라이트를 향해 선다.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = BillboardPositionWS(input.positionOS.xyz);
                float3 normalWS = -UNITY_MATRIX_V[2].xyz;
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
