// 눈송이 전용 파티클 셰이더. 장면 포그와 상관없이 눈만의 거리 색을 쓴다.
// 카메라 가까이의 눈은 흰색 그대로, 멀어질수록 Far Color 로 섞이고 옅어진다.
// 카메라 바로 앞의 큰 눈송이는 Near Fade 거리 안에서 사라진다.
Shader "ThatGameJam/SnowParticle"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Near Color", Color) = (1, 1, 1, 0.9)

        [Header(Distance Tint)]
        _FarColor ("Far Color", Color) = (0.78, 0.8, 0.92, 1)
        _FarStart ("Far Start", Float) = 10
        _FarEnd ("Far End", Float) = 40
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.4

        [Header(Camera Near Fade)]
        _NearFadeStart ("Near Fade Start", Float) = 1
        _NearFadeEnd ("Near Fade End", Float) = 4

        [Header(Clearing Behind The Ring)]
        _RingClearSoftness ("Clearing Edge Softness", Float) = 2
        _RingClearLag ("Clearing Behind The Front", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Snow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _SpringRing; // SpringRing 재생 중의 중심(xyz)과 반지름(w). 0 이면 재생 중이 아니다

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _FarColor;
                float _FarStart;
                float _FarEnd;
                half _FarAlpha;
                float _NearFadeStart;
                float _NearFadeEnd;
                float _RingClearSoftness;
                float _RingClearLag;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float distance : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                // 눈이 갠다: 앞줄의 바람(SeasonSnow)에 밀려나지 않고 남은 눈송이는 앞줄이 지나간 뒤에 사라진다.
                if (_SpringRing.w > 0.0)
                {
                    float fromRing = length(positionWS.xz - _SpringRing.xz) - (_SpringRing.w - _RingClearLag);
                    output.color.a *= smoothstep(-_RingClearSoftness, _RingClearSoftness, fromRing);
                }
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.distance = length(positionWS - GetCameraPositionWS());
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;

                half far = saturate((input.distance - _FarStart) / max(_FarEnd - _FarStart, 1e-3));
                c.rgb = lerp(c.rgb, _FarColor.rgb * input.color.rgb, far * _FarColor.a);
                c.a *= lerp(1.0, _FarAlpha, far);

                half near = saturate((input.distance - _NearFadeStart) / max(_NearFadeEnd - _NearFadeStart, 1e-3));
                c.a *= near;
                return c;
            }
            ENDHLSL
        }
    }
}
