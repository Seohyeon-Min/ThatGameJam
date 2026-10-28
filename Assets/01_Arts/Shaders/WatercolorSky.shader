// 수채화 하늘(스카이박스). 위로 갈수록 진해지는 하늘색 물감 한 번 칠을 흉내 낸다.
// - 칠은 고르지 않다: 저주파 잡음으로 진한 곳과 옅은 곳이 생긴다(같은 물감을 더 쌓은 것처럼, 회색을 곱하지 않는다).
// - 구름은 칠하지 않고 남긴 종이다. 구름 가장자리 바로 바깥에 안료가 고여 진한 테두리(백런)가 생긴다.
// - 지평선 쪽은 종이 흰색으로 바랜다. 설산 배경의 흰 바닥과 이어진다.
// 무늬는 하늘 방향에 붙어 있어 카메라를 돌려도 하늘에 고정된다. 잡음은 몇 옥타브만 계산해서 가볍다(웹 대상).
// 색 계산은 WatercolorFlat 과 같이 sRGB 에서 한다.
Shader "ThatGameJam/WatercolorSky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.55, 0.72, 0.9, 1)
        _HorizonColor ("Horizon Color", Color) = (0.82, 0.9, 0.97, 1)
        _GradientPower ("Gradient (higher = colour stays pale longer)", Range(0.2, 4)) = 1.2
        _HorizonPaper ("Horizon Paper Height", Range(0, 0.5)) = 0.08

        [Header(Wash)]
        _WashUneven ("Uneven Wash", Range(0, 1)) = 0.5
        _WashScale ("Wash Blotch Size", Range(0.1, 5)) = 1

        [Header(Clouds (unpainted paper))]
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.35
        _CloudScale ("Cloud Size", Range(0.1, 5)) = 1
        _CloudSoftness ("Cloud Edge Softness", Range(0.005, 0.3)) = 0.04
        _CloudRim ("Cloud Rim Pigment", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half _GradientPower;
                half _HorizonPaper;
                half _WashUneven;
                half _WashScale;
                half _CloudCover;
                half _CloudScale;
                half _CloudSoftness;
                half _CloudRim;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // 3 옥타브. 0~1.
            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.57 + ValueNoise(p * 2.03 + 11.7) * 0.29 + ValueNoise(p * 4.01 + 23.1) * 0.14;
            }

            // 같은 물감을 더 쌓은 색: 거듭제곱으로 어두워져 채도가 오른다. 회색을 곱하지 않는다.
            half3 Deepen(half3 c, half amount)
            {
                return pow(max(c, 1e-4), 1.0 + amount);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                half height = d.y;

                // 하늘을 머리 위 평면에 펼친 좌표. 지평선 쪽으로 갈수록 무늬가 길게 눕는다(원근).
                float2 sky = d.xz / max(d.y + 0.2, 0.05);

                half3 top = LinearToSRGB(_TopColor.rgb);
                half3 horizon = LinearToSRGB(_HorizonColor.rgb);
                half3 color = lerp(horizon, top, pow(saturate(height), 1.0 / _GradientPower));

                // 고르지 않은 칠: 진한 곳은 물감이 더 쌓이고, 옅은 곳은 종이 쪽으로 바랜다.
                half wash = Fbm(sky * 0.6 / _WashScale + 3.1) - 0.5;
                color = wash > 0 ? Deepen(color, wash * 2.0 * _WashUneven) : lerp(color, 1.0, -wash * _WashUneven);

                // 구름: 칠하지 않고 남긴 종이. 가장자리 바로 바깥에 안료가 고인 테두리.
                half cloudNoise = Fbm(sky * 0.9 / _CloudScale);
                half edge = 1.0 - _CloudCover;
                half cloud = smoothstep(edge - _CloudSoftness, edge + _CloudSoftness, cloudNoise);
                half rim = smoothstep(edge - _CloudSoftness * 4.0, edge - _CloudSoftness, cloudNoise) * (1.0 - cloud);
                color = Deepen(color, rim * _CloudRim * 0.6);
                // 지평선 가까이는 구름이 흐려진다(멀어서 납작하고 옅게).
                cloud *= smoothstep(0.0, 0.15, height);

                // 지평선 쪽 종이 띠. 지평선 아래도 종이로 둔다(설원 바닥과 이어짐).
                half paper = 1.0 - smoothstep(0.0, max(_HorizonPaper, 1e-3), height);
                color = lerp(color, 1.0, max(cloud, paper));

                return half4(SRGBToLinear(saturate(color)), 1);
            }
            ENDHLSL
        }
    }
}
