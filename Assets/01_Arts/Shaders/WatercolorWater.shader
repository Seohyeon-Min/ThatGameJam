// 수채화 물(개울, 연못). 평면(Unity Plane 등, UV 0~1)에 쓴다. 수채화가가 물을 칠하는 방식을 따른다.
// - 젖은 칠: 가운데(깊은 곳)는 진한 색, 물가(얕은 곳)는 옅은 색. 큰 번짐이 부드럽게 섞이고, 번진 자리는 종이 쪽으로 옅어진다.
// - 물빛은 칠이 아니라 칠하지 않고 남긴 흰 종이다: 흐름 방향으로 긴 흰 붓 자국이 천천히 흘러간다.
// - 물가: 모양은 UV 가장자리를 잡음으로 들쭉날쭉하게 잘라 만든다(깊이 텍스처를 쓰지 않아 가볍고, 바닥과 높이가 같아도 된다).
//   물가 바로 안쪽에 안료가 진하게 고이고, 그 안쪽은 흰 거품처럼 종이로 남는다.
// - 반사: 실제 반사 대신 비스듬히 볼수록 하늘·공기 색(WatercolorManager 의 Haze 색)을 비춘다.
// - 드리운 그림자는 땅보다 옅고 푸르다. 거리 안개는 WatercolorFlat 과 같다.
// 크기는 월드 단위(이 물의 크기에 맞춘다). 색 계산은 sRGB 에서 한다.
Shader "ThatGameJam/WatercolorWater"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.25, 0.55, 0.78, 1)
        _DeepColor2 ("Deep Color 2 (mixed in by the wash)", Color) = (0.2, 0.62, 0.66, 1)
        _ShallowColor ("Shallow Color", Color) = (0.62, 0.82, 0.9, 1)
        _DepthWidth ("Shallow Band Width (m)", Range(0.05, 5)) = 1.2

        [Header(Wash)]
        _WashSize ("Wash Blotch Size (m)", Range(0.2, 20)) = 3
        _WashBlooms ("Blooms (pale patches)", Range(0, 1)) = 0.6
        _BloomRim ("Bloom Rim Pigment", Range(0, 2)) = 0.8
        _WashUneven ("Uneven Pigment", Range(0, 1)) = 0.5
        _WashMix ("Colour Mixing", Range(0, 1)) = 0.6

        [Header(Paper Highlights)]
        _StreakAmount ("White Streaks", Range(0, 1)) = 0.5
        _StreakLength ("Streak Length (m)", Range(0.1, 10)) = 3
        _StreakWidth ("Streak Width (m)", Range(0.01, 1)) = 0.2
        _FlowDirection ("Flow Direction (XZ)", Vector) = (0, 1, 0, 0)
        _FlowSpeed ("Flow Speed (m/s)", Range(0, 2)) = 0.15

        [Header(Shore)]
        _ShoreRagged ("Shore Raggedness (m)", Range(0, 3)) = 0.9
        _ShoreNoiseSize ("Shore Noise Size (m)", Range(0.1, 10)) = 3
        _ShorePigment ("Shore Pigment", Range(0, 2)) = 0.8
        _ShorePigmentWidth ("Shore Pigment Width (m)", Range(0.005, 0.5)) = 0.06
        _ShoreFoam ("Shore Paper (foam)", Range(0, 1)) = 0.7
        _ShoreFoamWidth ("Shore Paper Width (m)", Range(0.01, 1)) = 0.18

        [Header(Reflection and Shadow)]
        _SkyReflection ("Sky Reflection", Range(0, 1)) = 0.5
        _SkyColor ("Sky Colour (above the haze)", Color) = (0.6, 0.76, 0.92, 1)
        _ShadowTint ("Cast Shadow Tint", Color) = (0.7, 0.8, 1, 1)
        _ShadowStrength ("Cast Shadow Strength", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _DeepColor2;
                half4 _ShallowColor;
                half _DepthWidth;
                half _WashSize;
                half _WashBlooms;
                half _WashMix;
                half _BloomRim;
                half _WashUneven;
                half _StreakAmount;
                half _StreakLength;
                half _StreakWidth;
                float4 _FlowDirection;
                half _FlowSpeed;
                half _ShoreRagged;
                half _ShoreNoiseSize;
                half _ShorePigment;
                half _ShorePigmentWidth;
                half _ShoreFoam;
                half _ShoreFoamWidth;
                half _SkyReflection;
                half4 _SkyColor;
                half4 _ShadowTint;
                half _ShadowStrength;
            CBUFFER_END

            // WatercolorManager 의 값.
            half _WC_PigmentThickness;
            half4 _WC_Haze;     // x 세기, y 시작 거리, z 다 바래는 데까지 거리
            half4 _WC_HazeLow;
            half4 _WC_HazeHigh;
            half _WC_Plain;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return Noise(p) * 0.57 + Noise(p * 2.03 + 11.7) * 0.29 + Noise(p * 4.01 + 23.1) * 0.14;
            }

            // 같은 물감을 더 쌓은 색: 거듭제곱으로 어두워져 채도가 오른다(회색을 곱하지 않는다).
            half3 Deepen(half3 c, half amount)
            {
                return pow(max(c, 1e-4), 1.0 + amount);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = input.positionWS;
                float2 world = positionWS.xz;

                // 물의 월드 크기(평면의 축 길이). UV 를 미터로 바꾸는 데 쓴다.
                float3x3 toWorld = (float3x3)GetObjectToWorldMatrix();
                float2 sizeWS = float2(length(toWorld._m00_m10_m20), length(toWorld._m02_m12_m22)) * 10.0;

                // 물가 모양: UV 가장자리까지의 거리(미터)에서 들쭉날쭉한 잡음을 빼고, 0 보다 작으면 물이 아니다.
                // 끝은 둥글게: 두 축의 거리를 부드럽게 합친다. 큰 들쭉날쭉함에 잔 들쭉날쭉함을 더한다.
                float2 edge = min(input.uv, 1.0 - input.uv) * sizeWS;
                float2 inside = max(_ShoreRagged + 0.5 - edge, 0.0);
                float toEdge = _ShoreRagged + 0.5 - length(inside);
                float ragged = Fbm(world / _ShoreNoiseSize + 5.3) * 0.75 + Fbm(world / (_ShoreNoiseSize * 0.25) + 2.9) * 0.25;
                float shore = toEdge - ragged * _ShoreRagged;
                clip(shore);

                half3 shallow = LinearToSRGB(_ShallowColor.rgb);
                half3 deep = LinearToSRGB(_DeepColor.rgb);
                half3 deep2 = LinearToSRGB(_DeepColor2.rgb);

                // 젖은 칠: 깊은 색 두 가지가 큰 번짐으로 섞이고, 얕은 띠는 물가에서 부드럽게 옅어진다.
                float2 flow = normalize(_FlowDirection.xy + 1e-5);
                float2 drift = flow * _FlowSpeed * _Time.y;
                half wash = Fbm((world - drift * 0.3) / _WashSize);
                half3 deepMix = lerp(deep, deep2, smoothstep(0.35, 0.65, wash) * _WashMix);
                half depthT = smoothstep(0.0, _DepthWidth, shore + (wash - 0.5) * _DepthWidth * 0.6);
                half3 color = lerp(shallow, deepMix, depthT);

                // 칠의 진하기가 고르지 않다: 물감이 고인 곳은 더 쌓인 색(채도 오름), 옅은 곳은 종이 쪽.
                half uneven = Fbm((world - drift * 0.2) / (_WashSize * 0.5) + 3.7) - 0.5;
                color = uneven > 0 ? Deepen(color, uneven * 2.0 * _WashUneven) : lerp(color, 1.0, -uneven * _WashUneven * 0.6);

                // 번진 자리(백런): 물에 밀려 안쪽은 종이 쪽으로 옅어지고, 밀린 안료가 가장자리에 고여 진한 테두리가 생긴다.
                half bloomField = Fbm((world - drift * 0.5) / (_WashSize * 1.3) + 17.1) + (Noise(world / (_WashSize * 0.15) + 8.1) - 0.5) * 0.08;
                half bloom = smoothstep(0.58, 0.62, bloomField);
                half bloomRim = smoothstep(0.54, 0.585, bloomField) * (1.0 - bloom);
                color = lerp(color, 1.0, bloom * _WashBlooms * 0.6);
                color = Deepen(color, bloomRim * _BloomRim * 0.6);

                // 반사: 비스듬히 볼수록 하늘과 공기 색을 비춘다. 지평선 쪽은 안개 색, 그 위는 하늘색.
                float3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
                half fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), viewWS)), 3.0);
                half3 sky = lerp(LinearToSRGB(_SkyColor.rgb), _WC_HazeLow.rgb, saturate(_WC_Haze.x) * 0.6);
                color = lerp(color, sky, fresnel * _SkyReflection);

                // 드리운 그림자: 땅보다 옅고 푸르게.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half shade = 1.0 - mainLight.shadowAttenuation;
                color *= lerp(1.0, LinearToSRGB(_ShadowTint.rgb), shade * _ShadowStrength);

                // 흰 붓 자국: 흐름 방향으로 긴 가로 붓질만큼 칠하지 않고 종이로 남긴다. 천천히 흘러간다.
                float2 across = float2(-flow.y, flow.x);
                float2 streakUV = float2(dot(world, flow) - _FlowSpeed * _Time.y, dot(world, across));
                streakUV /= float2(_StreakLength, _StreakWidth);
                half streak = Noise(streakUV) * 0.7 + Noise(streakUV * float2(1.9, 1.3) + 9.7) * 0.3;
                // 붓질 가장자리는 마른 붓처럼 거칠다(가는 결 잡음으로 흔든다).
                streak += (Noise(streakUV * float2(6.0, 3.0) + 4.1) - 0.5) * 0.12;
                half streakMask = smoothstep(1.0 - _StreakAmount * 0.45, 1.01 - _StreakAmount * 0.45, streak) * depthT;
                color = lerp(color, 1.0, streakMask);

                // 물가: 맨 가장자리에 안료가 진하게 고이고, 그 안쪽은 흰 거품처럼 종이로 남는다.
                half pigment = 1.0 - smoothstep(0.0, _ShorePigmentWidth, shore);
                half foamEdge = Fbm(world / (_ShoreNoiseSize * 0.4) + 31.7);
                half foam = (1.0 - smoothstep(_ShorePigmentWidth, _ShorePigmentWidth + _ShoreFoamWidth * (0.6 + foamEdge), shore)) * (1.0 - pigment);
                color = lerp(color, 1.0, foam * _ShoreFoam);
                color = Deepen(color, pigment * _ShorePigment * max(_WC_PigmentThickness, 0.5));

                // 거리 안개(WatercolorFlat 과 같다).
                if (_WC_Haze.x > 0)
                {
                    half hazeAmount = saturate((length(positionWS - GetCameraPositionWS()) - _WC_Haze.y) / max(_WC_Haze.z, 0.1)) * _WC_Haze.x;
                    color = lerp(color, lerp(_WC_HazeLow.rgb, _WC_HazeHigh.rgb, saturate(-viewWS.y * 2.0)), hazeAmount);
                }

                half3 linearColor = SRGBToLinear(saturate(color));
                // 씬 뷰 평범 모드에서는 물 색만.
                if (_WC_Plain > 0.5)
                    linearColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);
                return half4(MixFog(linearColor, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
}
