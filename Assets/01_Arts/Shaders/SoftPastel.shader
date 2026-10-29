// 소프트 파스텔(분필 파스텔) 테스트 셰이더. 수채화(WatercolorFlat)와 같은 머티리얼 속성을 써서 셰이더만 바꿔 끼울 수 있다.
// - 형태는 부드러운 그라데이션이다. 빛과 그늘 사이를 매끈하게 섞지 않고 종이 결 알갱이로 잇는다(파스텔이 결 위에만 묻는 것처럼).
// - 빛 쪽은 따뜻한 색으로 밝아지고, 그늘과 드리운 그림자는 차가운 색이 겹쳐진다(회색을 곱하지 않는다).
// - 빛을 향한 가장자리가 따뜻하게 빛나고, 윤곽은 알갱이로 흩어져 부드럽다.
// - 얼룩덜룩: 수채화 굽기 무늬로 밝기와 채도가 고르지 않고, 색 물방울 자리에 다른 색 조각이 섞인다(경계는 알갱이).
//   굽지 않은 바닥은 월드 좌표 잡음으로 얼룩만 진다.
// - 물번짐(켜고 끔): 수채화처럼 젖은 물감이 번진다. 이웃한 색이 저주파로 스며 섞이고(청록↔파랑처럼),
//   번진 자리 안쪽은 종이 쪽으로 옅어지며, 그 가장자리에 물감이 고여 꽃양배추 같은 테두리가 생긴다.
// - 빛 쪽 파스텔 조각: 굽기 무늬의 번짐 자리에 분홍·민트·하늘·라벤더가 비친다. 흰색에는 넣지 않는다.
// 알갱이는 화면 화소에 고정한다(카메라가 고정이라 종이 결처럼 보인다). 스타일 값은 씬의 PastelStyle 이 전역(_PS_*)으로 준다.
// 색 계산은 sRGB 에서 한다.
Shader "ThatGameJam/SoftPastel"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle] _Floor ("Floor (no rim or edge dissolve)", Float) = 0

        [Header(Kept for swapping with WatercolorFlat)]
        [Toggle] _HasPattern ("Has Pattern", Float) = 0
        [NoScaleOffset] _PatternTex ("Pattern (3D)", 3D) = "white" {}
        [NoScaleOffset] _ChaosTex ("Colour Chaos (3D)", 3D) = "gray" {}

        [Header(Surface)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/01_Arts/Shaders/BakedPatternUV.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE3D(_PatternTex);  // 수채화 굽기 무늬. r 얼룩, a 저주파 잡음
            SAMPLER(sampler_PatternTex);
            TEXTURE3D(_ChaosTex);    // r 색 물방울(0.5 = 없음)

            // WatercolorFlat 과 같은 머티리얼 상수 버퍼(SRP Batcher 와 그림자·깊이 패스 공유).
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Floor;
                half _HasPattern;
                half _Cutoff;
            CBUFFER_END

            half4 _PS_Light;   // rgb 빛 쪽 따뜻한 색(sRGB), a 세기
            half4 _PS_Shade;   // rgb 그늘 차가운 색(sRGB), a 세기
            half4 _PS_Tone;    // x 그늘 경계 위치, y 경계 폭, z 채도 추가
            half4 _PS_Grain;   // x 알갱이 크기(1080p 화소), y 경계 알갱이 정도, z 종이 결, w 윤곽 흩어짐 폭(1080p 화소)
            half4 _PS_Rim;     // rgb 가장자리 빛 색, a 세기
            half4 _PS_Chips;   // x 최소 크기, y 최대 크기(화면 높이 비율), z 생길 확률, w 얼룩 명도 변화
            half4 _PS_Haze;    // x 거리 안개 세기, y 시작 거리, z 다 바래는 데까지 거리, w 1 = Pastel Style 이 켜져 있음
            half4 _PS_HazeLow; // 지평선 쪽 공기 색(sRGB)
            half4 _PS_HazeHigh;// 위쪽 공기 색(sRGB)
            half4 _PS_Turn;    // x 그늘 색상을 돌리는 정도, y 보색 근처도 돌리는 정도(0 = 거의 안 돌리고 어둡게)
            half4 _PS_Water;   // x 켜짐(1), y 번짐 세기, z 색 스밈 범위(한 바퀴 = 1), w 번짐 테두리 세기
            half4 _PS_Mottle;  // x 얼룩 세기, y 색 조각 색상 이동(한 바퀴 = 1), z 바닥 얼룩 크기(월드), w 빛 쪽 파스텔 조각 세기

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float3 patternUV : TEXCOORD3; // 굽기 도구가 넣은 모델 기준 좌표(0~1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float3 patternUV : TEXCOORD4;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                output.patternUV = TwistPatternUV(input.patternUV);
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

            float Hash3(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // 파스텔 조각: 카메라가 보는 평면의 격자 칸마다 동그란 조각 하나(확률), 지름은 최소~최대 사이.
            // 칸 크기가 최대 지름이라 가까운 꼭짓점 쪽 2x2 칸만 보면 된다. 격자는 월드에 고정해서 한 모델의 여러 조각
            // (렌더러)에 걸쳐 이어진다. 크기는 화면 높이 비율에 맞추되 2의 거듭제곱 단계로 끊어서, 한 모델의 조각들이
            // 같은 단계를 쓰고 카메라가 움직여도 조각이 계속 변하지 않는다(실제 크기는 설정의 0.7~1.4배).
            // 가장자리는 넓게 흐려진다. 조각마다 분홍~라벤더 또는 민트~하늘.
            half PastelChips(float3 positionWS, out half hue)
            {
                float3 origin = GetObjectToWorldMatrix()._m03_m13_m23;
                float worldPerScreen = length(origin - GetCameraPositionWS()) * 2.0 / abs(UNITY_MATRIX_P[1][1]);
                float level = round(log2(max(_PS_Chips.y, 1e-4) * worldPerScreen));
                float cell = exp2(level);
                float2 q = float2(dot(positionWS, UNITY_MATRIX_V[0].xyz), dot(positionWS, UNITY_MATRIX_V[1].xyz)) / cell;
                float2 seed = level * float2(17.3, 5.9);
                float2 baseCell = floor(q - 0.5);
                half best = 0.0;
                hue = 0.9;
                for (int i = 0; i < 4; i++)
                {
                    float2 c = baseCell + float2(i & 1, (i >> 1) & 1);
                    float3 k = float3(c + seed, 0.0);
                    if (Hash3(k) > _PS_Chips.z)
                        continue;
                    float2 center = c + float2(Hash3(k + 11.1), Hash3(k + 23.7));
                    float radius = lerp(_PS_Chips.x, _PS_Chips.y, Hash3(k + 51.9)) / max(_PS_Chips.y, 1e-4) * 0.5;
                    half m = 1.0 - smoothstep(radius * 0.35, radius, length(q - center));
                    if (m > best)
                    {
                        best = m;
                        float t = Hash3(k + 71.3);
                        hue = t > 0.5 ? lerp(0.80, 0.97, t * 2.0 - 1.0) : lerp(0.38, 0.56, t * 2.0);
                    }
                }
                return best;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                clip(baseSample.a * _BaseColor.a - _Cutoff);
                #endif
                half3 albedo = LinearToSRGB(baseSample.rgb * _BaseColor.rgb);
                // 흰색(채도 낮고 밝은 물감)은 흰색 그대로 둔다: 채도 올림, 색 조각, 파스텔 조각을 받지 않는다.
                half3 baseHsv = RgbToHsv(albedo);
                half notWhite = saturate((baseHsv.y - 0.12) * 6.0 + (0.8 - baseHsv.z) * 6.0);
                albedo = saturate(lerp(Luminance(albedo).xxx, albedo, 1.0 + _PS_Tone.z * notWhite));
                half objectOnly = 1.0 - _Floor;

                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // 화면에 고정한 알갱이(1080p 기준 크기). 굵은 알갱이와 화소 단위의 고운 알갱이를 섞는다.
                float2 pixel = input.positionCS.xy * (1080.0 / _ScreenParams.y);
                half grain = ValueNoise(pixel / _PS_Grain.x) * 0.6 + Hash(floor(pixel)) * 0.4;

                // 얼룩덜룩: 밝기·채도가 고르지 않은 칠과 다른 색 조각. 구운 모델은 굽기 무늬, 바닥은 월드 잡음.
                half blotch, drop = 0.0;
                half4 pattern = 0.5; // r 얼룩, g 번짐 안쪽, b 번짐 테두리, a 저주파 잡음
                if (_HasPattern > 0.5)
                {
                    pattern = SAMPLE_TEXTURE3D(_PatternTex, sampler_PatternTex, input.patternUV);
                    blotch = pattern.r * 0.6 + pattern.a * 0.4;
                    drop = SAMPLE_TEXTURE3D(_ChaosTex, sampler_PatternTex, input.patternUV).r * 2.0 - 1.0;
                }
                else
                {
                    float2 p = input.positionWS.xz / _PS_Mottle.z;
                    blotch = ValueNoise(p) * 0.65 + ValueNoise(p * 2.3 + 5.1) * 0.35;
                    // 바닥은 굽지 않아서 번짐 무늬도 잡음으로 만든다.
                    half field = ValueNoise(p * 0.7 + 9.3) + (ValueNoise(p * 4.1 + 2.7) - 0.5) * 0.12;
                    half inside = smoothstep(0.6, 0.63, field);
                    pattern = half4(blotch, inside, smoothstep(0.56, 0.6, field) * (1.0 - inside), ValueNoise(p * 0.5 + 21.7));
                }
                half mottle = (blotch - 0.5) * 2.0 * _PS_Mottle.x;
                half3 albedoHsv = RgbToHsv(albedo);
                // 진한 쪽은 어둡게 하지 않고 파란빛으로 돌린다(명도는 그대로라 탁해지지 않는다). 옅은 쪽은 조금 밝아진다.
                half toBlue = 0.6 - albedoHsv.x;
                toBlue -= round(toBlue);
                albedoHsv.x = frac(albedoHsv.x + toBlue * saturate(mottle) * 0.5 * notWhite);
                albedoHsv.z = lerp(albedoHsv.z, 1.0, saturate(-mottle) * 0.3);
                // 명도 변화(Mottle Brightness): 진한 쪽은 어두워지고 옅은 쪽은 밝아진다. 채도를 같이 올려 회색빛이 되지 않게 한다.
                albedoHsv.z = saturate(albedoHsv.z * (1.0 - mottle * _PS_Chips.w * 0.5));
                albedoHsv.y = saturate(albedoHsv.y * (1.0 + saturate(mottle) * _PS_Chips.w * 0.4 * notWhite));
                // 색 조각: 물방울 자리에서 색상을 돌린다. 경계는 알갱이로 흩어진다.
                half patch = saturate(abs(drop) * 3.0 - 0.3 + (grain - 0.5) * _PS_Grain.y) * notWhite;
                albedoHsv.x = frac(albedoHsv.x + sign(drop) * patch * _PS_Mottle.y);
                // 색상만 돌리면 노랑·연두 쪽에서 명도가 모자라 올리브처럼 탁해진다. 조각은 조금 밝고 선명하게,
                // 노랑 근처(색상 0.1~0.22)로 간 조각은 명도를 더 올린다.
                half yellowish = smoothstep(0.06, 0.12, albedoHsv.x) * (1.0 - smoothstep(0.2, 0.28, albedoHsv.x));
                albedoHsv.z = lerp(albedoHsv.z, 1.0, patch * (0.25 + 0.5 * yellowish));
                albedoHsv.y = saturate(albedoHsv.y * (1.0 + patch * 0.2));

                // 물번짐: 색이 이웃 색으로 스미고(색상만 저주파로 오간다), 번진 테두리는 물감이 고여 조금 진하고 푸르다.
                half bloomRim = 0.0, bloomInside = 0.0;
                if (_PS_Water.x > 0.5)
                {
                    albedoHsv.x = frac(albedoHsv.x + (pattern.a - 0.5) * 2.0 * _PS_Water.z * notWhite);
                    bloomInside = pattern.g * _PS_Water.y;
                    bloomRim = pattern.b * _PS_Water.w;
                    half rimBlue = 0.6 - albedoHsv.x;
                    rimBlue -= round(rimBlue);
                    albedoHsv.x = frac(albedoHsv.x + rimBlue * bloomRim * 0.25 * notWhite);
                    albedoHsv.y = saturate(albedoHsv.y * (1.0 + bloomRim * 0.6));
                    // 흰색은 테두리에서 어두워지지 않는다(회색 선이 되지 않게).
                    albedoHsv.z = saturate(albedoHsv.z * (1.0 - bloomRim * 0.15 * notWhite));
                }
                albedo = HsvToRgb(albedoHsv);
                // 번진 자리 안쪽은 물에 밀려 종이 쪽으로 옅어진다.
                albedo = lerp(albedo, 1.0, saturate(bloomInside) * 0.6);

                // 윤곽을 알갱이로 흩어지게: 윤곽에 가까울수록 알갱이가 빠진다(불투명이라 잘라 낸다).
                half facingView = saturate(dot(normalWS, viewWS));
                if (_PS_Grain.w > 0 && objectOnly > 0.5)
                {
                    half pixels = facingView / max(fwidth(facingView), 1e-5) * (1080.0 / _ScreenParams.y);
                    clip(pixels / _PS_Grain.w - (1.0 - grain) * 0.9);
                }

                // 빛 받는 정도(부드러운 그라데이션)와 드리운 그림자. 바닥은 면 방향을 쓰지 않고 그림자만 쓴다.
                half wrap = lerp(1.0, dot(normalWS, mainLight.direction) * 0.5 + 0.5, objectOnly);
                half tone = wrap * lerp(1.0, mainLight.shadowAttenuation, smoothstep(0.1, 0.3, wrap));
                // 경계는 알갱이로: 알갱이만큼 위치를 흔든 뒤 부드럽게 나눈다.
                half toneGrain = tone + (grain - 0.5) * _PS_Grain.y * 0.6;
                half lit = smoothstep(_PS_Tone.x - _PS_Tone.y * 0.5, _PS_Tone.x + _PS_Tone.y * 0.5, toneGrain);

                // 그늘: 차가운 색을 한 겹 덧칠(곱하기)해 맑게 어둡다. 빛: 따뜻한 밝은 색 쪽으로, 빛을 정면으로 받을수록 많이.
                // 그늘: 그늘 색 쪽으로 색상환을 따라 돌리고 채도는 지키며 명도만 조금 내린다. 곱하거나 섞으면 보색끼리
                // 만나 회색·갈색으로 탁해진다(노란 길 + 파랑). 무채색(흰 눈)은 그늘 색을 그대로 띤다.
                half3 shadeHsv = RgbToHsv(albedo);
                half3 tintHsv = RgbToHsv(_PS_Shade.rgb);
                half turn = tintHsv.x - shadeHsv.x;
                turn -= round(turn);
                // 보색 근처(색상 차이가 반 바퀴에 가까울수록)는 덜 돌린다. 돌리면 노란 길이 분홍이 되기 때문이다.
                // 덜 돌린 만큼 명도를 조금 더 내려서, 노랑은 채도를 지킨 채 황토·머스터드처럼 어두워진다.
                half turnFade = lerp(1.0 - 4.0 * turn * turn, 1.0, _PS_Turn.y);
                half chroma = saturate(shadeHsv.y * 4.0);
                half turned = frac(shadeHsv.x + turn * turnFade * _PS_Shade.a * _PS_Turn.x);
                // 무채색 쪽은 그늘 색상으로 간다. 색상환의 가까운 쪽으로 섞어야 노랑→보라 사이 초록을 지나지 않는다.
                half toTint = tintHsv.x - turned;
                toTint -= round(toTint);
                shadeHsv.x = frac(turned + toTint * (1.0 - chroma));
                shadeHsv.y = max(shadeHsv.y, tintHsv.y * _PS_Shade.a);
                shadeHsv.z *= lerp(1.0, tintHsv.z * 0.85, _PS_Shade.a);
                shadeHsv.z *= 1.0 - (1.0 - turnFade) * chroma * 0.3 * _PS_Shade.a;
                half3 shadeColor = HsvToRgb(shadeHsv);
                half3 lightColor = lerp(albedo, _PS_Light.rgb, _PS_Light.a * saturate(wrap * 2.0 - 1.0) * objectOnly);
                half3 color = lerp(shadeColor, lightColor, lit);

                // 빛 쪽 파스텔 조각: 물체에 붙은 동그란 조각. 가장자리는 흐릿하게 번진다.
                if (_PS_Mottle.w > 0 && objectOnly > 0.5)
                {
                    half chipHue;
                    // 빛 쪽에서 가장 또렷하고 그늘 쪽으로 갈수록 옅어진다.
                    half chip = PastelChips(input.positionWS, chipHue) * lerp(0.35, 1.0, lit) * notWhite * _PS_Mottle.w;
                    color = lerp(color, HsvToRgb(half3(chipHue, 0.45, 1.0)), saturate(chip));
                }

                // 빛을 향한 가장자리가 따뜻하게 빛난다.
                half rim = pow(1.0 - facingView, 3.0) * saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5) * mainLight.shadowAttenuation;
                color = lerp(color, _PS_Rim.rgb, saturate(rim * _PS_Rim.a * 1.5) * objectOnly);

                // 종이 결: 알갱이만큼 살짝 밝고 어둡다.
                color *= 1.0 + (grain - 0.5) * 2.0 * _PS_Grain.z;

                // 거리 안개: 멀수록 공기 색으로 바랜다. 지평선 쪽(시선이 수평)은 아래 색, 올려다보는 쪽은 위 색.
                if (_PS_Haze.x > 0)
                {
                    half hazeAmount = saturate((length(input.positionWS - GetCameraPositionWS()) - _PS_Haze.y) / max(_PS_Haze.z, 0.1)) * _PS_Haze.x;
                    color = lerp(color, lerp(_PS_HazeLow.rgb, _PS_HazeHigh.rgb, saturate(-viewWS.y * 2.0)), hazeAmount);
                }

                color = SRGBToLinear(saturate(color));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "ThatGameJam/WatercolorFlat/SHADOWCASTER"
        UsePass "ThatGameJam/WatercolorFlat/DEPTHONLY"
        UsePass "ThatGameJam/WatercolorFlat/DEPTHNORMALS"
    }
}
