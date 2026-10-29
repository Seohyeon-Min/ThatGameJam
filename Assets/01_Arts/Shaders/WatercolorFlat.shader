// 평면에 칠한 수채화처럼 보이는 셰이더. 무늬는 매 프레임 계산하지 않고 굽기 도구가 만든 3D 텍스처를 한 번 읽는다.
// - 색은 세 가지뿐이다: 본색, 빛(칠하지 않은 종이색), 드리운 그림자.
//   밝기 폭이 좁은 파스텔 수채화: 명암을 밝기보다 색 온도로 나타낸다. 빛(종이)에서 일정 거리까지의 띠는
//   밝고 옅은 크림색 쪽으로 바래고(빛에 가까울수록 강하게) 파스텔 색 조각이 흩어진다. 드리운 그림자는 거의
//   어두워지지 않고 채도를 지킨 맑은 청록·파랑·보라가 된다. 안료가 쌓여 진해진 곳은 진한 만큼 파랑 쪽으로 돈다.
// - 모든 폭(경계 띠, 윤곽, 종이 경계 부드러움, 크림 띠)은 화면 높이 비율이다. 화소마다 미분(fwidth)으로 바꿔서
//   작은 물체와 큰 물체가 화면에서 같은 굵기의 디테일을 갖는다.
//   면 방향에 따른 어둠(형태 그림자)은 없다. 빛을 등진 면도 본색이다.
// - 빛: 빛을 정면으로 받는 쪽을 흰색(종이)으로 남긴다. 화면 전체에 곱하는 종이 질감이 그 자리에 드러난다.
// - 덩어리: 굽기 도구가 가까운 같은 색 조각들의 노멀을 덩어리 노멀로 바꿔 둔다. 그래서 빛 영역과 윤곽이 덩어리를 따른다.
// - 무늬: 정점마다 구워 둔 "모델 기준 좌표"(UV3)로 모델의 3D 무늬 텍스처를 읽는다.
//   조각 경계를 넘어 이어지고, 정점에 붙어 있어 애니메이션 중에도 몸을 따라간다.
//   같은 프리팹의 인스턴스는 굽기 결과를 같이 쓰고, 인스턴스마다 좌표를 뒤집고 밀어 다르게 보인다(BakedPatternUV.hlsl).
//   r: 안료 얼룩, g: 백런 안쪽(옅어짐), b: 백런 테두리(진해짐), a: 경계 안료 축적용 저주파 잡음 N
// - 경계 안료 축적: 빛(종이)과 색의 경계 바로 안쪽 색 쪽에 안료가 몰린다. 진하기 = E(d) * (0.6 + 0.4 N).
// - 진해질 때는 회색으로 탁해지지 않고 채도가 올라가며 어두워진다(같은 물감이 더 쌓인 색).
// - 드리운 그림자도 수채화처럼 칠한다: 경계를 붓 자국처럼 흔들고, 경계에 안료가 고이고, 안쪽 진하기가 고르지 않다.
//   바닥(Floor 체크)은 굽지 않고 바닥 무늬를 쓴다. 원근이 있어도 앞뒤 얼룩이 화면에서 같은 크기로 보여
//   한 폭의 그림처럼 보이고, 무늬는 바닥에 붙어 있다. 그 밖의 굽지 않은 물체는 무늬가 없다.
//   그림자 안에서는 밝기를 나누지 않는다(종이 영역, 크림 띠, 빛 경계 띠 없음). 나머지 무늬는 똑같이 적용된다.
// 머티리얼은 색(Base Map x Base Color)만 갖는다. 텍스처가 있으면 모든 계산이 그 텍스처 색을 기준으로 한다.
// 칠하는 방식(안료 두께, 빛 쪽 크림색, 그늘 색 등)은 씬의 WatercolorManager 가 전역 값(_WC_*)으로 정한다.
// 굽지 않은 모델(Has Pattern 꺼짐)은 얼룩과 백런 없이 그린다.
Shader "ThatGameJam/WatercolorFlat"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Toggle] _Floor ("Floor (wide flat surface: no paper, outline or boundary band)", Float) = 0

        [Header(Baked by WatercolorBaker)]
        [Toggle] _HasPattern ("Has Pattern", Float) = 0
        [NoScaleOffset] _PatternTex ("Pattern (3D)", 3D) = "white" {}
        [NoScaleOffset] _ChaosTex ("Colour Chaos (3D)", 3D) = "gray" {}

        [Header(Surface)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Header(Shadows)]
        _SelfShadowSkip ("Ignore Own Shadow Within (m)", Range(0, 3)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/01_Arts/Shaders/BakedPatternUV.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE3D(_PatternTex);
        SAMPLER(sampler_PatternTex);
        TEXTURE3D(_ChaosTex); // r: 색 물방울(0.5 = 변화 없음), g: 연필 덩어리
        TEXTURE2D(_WC_PencilStrokes); // 연필 선 텍스처(나란한 줄 위에 가로로 그은 선들, r 가장 가까운 선 중심까지 거리 g 필압). WatercolorManager 가 한 번 만든다.
        #define PENCIL_MAX_DISTANCE 8.0

        // SRP Batcher 를 쓰려면 모든 패스가 같은 머티리얼 상수 버퍼를 선언해야 한다.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Floor;
            half _HasPattern;
            half _Cutoff;
            half _SelfShadowSkip;
        CBUFFER_END

        // WatercolorManager 가 정하는 전역 값. 모든 수채화 머티리얼이 같이 따른다.
        half _WC_PigmentThickness;
        half _WC_BoundaryPigment;
        half _WC_BoundaryWidth;   // 화면 높이 비율
        half _WC_OutlineWidth;    // 화면 높이 비율
        half _WC_WarmWidth;       // 빛 쪽 크림 띠 폭, 화면 높이 비율
        half4 _WC_LightCream;     // rgb 빛 쪽이 바래 가는 크림색(sRGB), a 바래는 정도
        half4 _WC_LightShade;     // x 빛 쪽 파스텔 조각 세기, y 그늘이 어두워지는 정도, zw 그늘 색상 범위(한 바퀴 = 1)
        half _WC_PastelShadows;   // 1 = 맑은 그늘 색(Shade), 0 = 예전 그림자(Accumulate)
        half _WC_ShadowSaturation; // 드리운 그림자 채도, -1~1 (0 = 그대로)
        half _WC_OutlinePigment;
        half _WC_Blotches;
        half _WC_Backruns;
        half _WC_BlotchSaturation;
        half4 _WC_InstanceColour; // 굽기를 공유하는 인스턴스마다 색 차이: x 색상(한 바퀴 = 1), y 밝기, z 채도 // 얼룩·백런이 어두워진 만큼 채도를 올리는 정도
        half _WC_CoolShift;
        half _WC_ShadowDepth;
        half _WC_ReceiveShadows;
        half _WC_PaperThreshold;
        half _WC_PaperSoftness;   // 화면 높이 비율
        half _WC_Saturation;      // -1~1
        half _WC_Brightness;      // -1~1
        half _WC_Contrast;        // -1~1
        half _WC_ChaosHue;        // 색 물방울의 최대 색상 이동(한 바퀴 = 1)
        half _WC_ShadowEdge;
        half _WC_ShadowRagged;
        half _WC_ShadowUneven;
        float4 _WC_Pencil;        // 연필 세기, 연필 진하기, 기본 각도(라디안), 선 굵기(1080p 화소 = 텍스처 칸)
        float4 _WC_PencilScale;   // 연필 선 텍스처 한 장의 월드 크기 = x * 거리 + y
        float4 _WC_FormLight;     // 형태용 빛 방향(빛 쪽을 향함), w = 1 이면 사용. 0 이면 주 광원 방향을 쓴다.
        float4 _WC_PencilZone;    // 형태 그림자 쪽 경계가 흐려지는 폭(화면 높이 비율), 그림자 쪽에 모이는 정도, 크기별 영향력
        #define PENCIL_TILE_SHARE (512.0 / 1080.0) // 선 텍스처 한 장이 화면 높이에서 차지하는 비율
        #define PENCIL_FULL_SIZE 0.25              // 화면 높이의 이 비율 이상인 조각은 선 길이를 줄이지 않는다

        // 연필 선: 카메라가 보는 평면에 그은 선이라 종이에 그린 것처럼 보이고, 위치는 물체에 붙어 있다
        // (물체 원점 기준). 물체까지 거리로 크기를 정해서 가깝든 멀든 화면에서 같은 길이다.
        half PencilStrokes(float3 positionWS, half angle)
        {
            float3 rel = positionWS - GetObjectToWorldMatrix()._m03_m13_m23;
            float3 right = UNITY_MATRIX_V[0].xyz;
            float3 up = UNITY_MATRIX_V[1].xyz;
            float2 p = float2(dot(rel, right), dot(rel, up));
            float s, c;
            sincos(angle, s, c);
            p = float2(c * p.x + s * p.y, -s * p.x + c * p.y);

            // 크기는 물체마다 하나(원점까지 거리)라서 한 물체 안에서 두 배율을 섞지 않는다. 섞으면 선이 겹쳐 얼룩이 된다.
            float distance = length(GetObjectToWorldMatrix()._m03_m13_m23 - GetCameraPositionWS());
            float tile = max(_WC_PencilScale.x * distance + _WC_PencilScale.y, 1e-4);
            // 화면에서 작은 조각일수록 선을 짧게 긋는다. 조각 크기는 렌더러 경계 상자의 가장 긴 변이고,
            // 선 방향으로만 줄여서 굵기와 줄 간격은 그대로다.
            float3 bounds = unity_RendererBounds_Max.xyz - unity_RendererBounds_Min.xyz;
            float screenShare = max(bounds.x, max(bounds.y, bounds.z)) / tile * PENCIL_TILE_SHARE;
            float shorten = max(pow(saturate(screenShare / PENCIL_FULL_SIZE), _WC_PencilZone.z), 0.15);
            half2 stroke = SAMPLE_TEXTURE2D(_WC_PencilStrokes, sampler_LinearRepeat, p / (tile * float2(shorten, 1.0))).rg;
            // 선 중심까지 거리로 굵기를 여기서 정한다. 그래서 굵기를 바꿔도 선은 제자리에서 굵기만 바뀌고,
            // 가장자리는 화면 한 화소 폭으로 또렷하다.
            float lineDistance = stroke.r * PENCIL_MAX_DISTANCE;
            half cover = saturate((_WC_Pencil.w * 0.5 - lineDistance) / max(fwidth(lineDistance), 1e-3) + 0.5);
            return cover * stroke.g;
        }

        float4 _WC_FloorDetail;   // 바닥 얼룩 하나의 월드 크기 = x * 거리 + y (카메라마다 WatercolorManager 가 정한다)
        TEXTURE2D(_WC_FloorPattern); // 바닥 무늬. 한 장에 얼룩이 FLOOR_CELLS x FLOOR_CELLS 개라 반복이 화면 여러 개만큼 떨어져 있다.
        #define FLOOR_CELLS 24.0

        // 바닥 무늬: 거리에 따라 필요한 얼룩 크기를 구하고, 그 사이의 두 배율(2의 거듭제곱)로 읽어 부드럽게 섞는다.
        // 그래서 앞뒤 얼룩이 화면에서 같은 크기다. 배율마다 위치를 다르게 밀어 두 배율의 반복 지점이 겹치지 않게 하고,
        // 섞으면 대비가 줄어서 다시 늘려 준다.
        half4 FloorPattern(float3 positionWS)
        {
            float distance = length(positionWS - GetCameraPositionWS());
            float wanted = max(_WC_FloorDetail.x * distance + _WC_FloorDetail.y, 1e-3);
            float level = log2(wanted);
            float baseLevel = floor(level);
            float blend = level - baseLevel;
            float cellA = exp2(baseLevel);
            float2 offsetA = frac(baseLevel * float2(0.6180, 0.7549));
            float2 offsetB = frac((baseLevel + 1.0) * float2(0.6180, 0.7549));
            half4 a = SAMPLE_TEXTURE2D(_WC_FloorPattern, sampler_LinearRepeat, positionWS.xz / (cellA * FLOOR_CELLS) + offsetA);
            half4 b = SAMPLE_TEXTURE2D(_WC_FloorPattern, sampler_LinearRepeat, positionWS.xz / (cellA * 2.0 * FLOOR_CELLS) + offsetB);
            half4 mixed = lerp(a, b, blend);
            half restore = rsqrt(max(blend * blend + (1.0 - blend) * (1.0 - blend), 0.5));
            return saturate((mixed - 0.5) * restore + 0.5);
        }
        half _WC_Plain;           // 1 = 씬 뷰를 수채화 없이 평범하게 그린다(Plain Scene View)

        // 장면의 빛과 공기 색(Scene Colour). 물체 안이 아니라 장면 전체에 걸친 색의 흐름이다.
        half4 _WC_SceneColour;    // x 빛·그늘 색 흐름 세기, y 흐름 크기(월드 단위), z 하늘·땅 반사색 세기, w 경계 보색 강조 세기
        half4 _WC_LightTintA;     // 빛 쪽 색 두 가지(sRGB). 자리마다 둘 사이를 오간다.
        half4 _WC_LightTintB;
        half4 _WC_ShadeTintA;     // 그늘 쪽 색 두 가지(sRGB)
        half4 _WC_ShadeTintB;
        half4 _WC_SkyBounce;      // 위를 보는 그늘이 받는 하늘색
        half4 _WC_GroundBounce;   // 아래·옆을 보는 그늘이 받는 땅색
        half4 _WC_EdgeAccent;     // 빛 경계 띠의 보색 강조 색
        half4 _WC_Haze;           // x 세기, y 시작 거리, z 다 바래는 데까지 거리
        half4 _WC_HazeLow;        // 지평선 쪽 공기 색
        half4 _WC_HazeHigh;       // 위쪽 공기 색

        float SceneHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float SceneNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(SceneHash(i), SceneHash(i + float2(1, 0)), f.x),
                lerp(SceneHash(i + float2(0, 1)), SceneHash(i + float2(1, 1)), f.x), f.y);
        }

        void AlphaClip(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
            #endif
        }
        ENDHLSL

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
                float3 patternUV : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            #define HUE_BLUE 0.61

            // 색상환에서 target 쪽으로 가까운 방향으로 돌린다. amount 1 = 최대 30도.
            // target 의 거의 반대편 색(파랑에 대한 주황)은 어느 쪽으로 돌려도 빨강이나 초록을 지나므로 거의 돌리지 않고,
            // 90도쯤 떨어진 색을 가장 많이 돌린다. 채도가 거의 없는 색(회색, 흰색)은 target 색을 살짝 섞기만 한다.
            // 입력은 sRGB. 선형 공간에서 색상과 채도를 다루면 채도가 눈에 과하게 보인다.
            half3 ShiftHue(half3 c, half target, half amount)
            {
                half3 hsv = RgbToHsv(c);
                half d = target - hsv.x;
                d -= round(d);
                half oppositeFade = 1.0 - 4.0 * d * d;
                hsv.x = frac(hsv.x + clamp(d * oppositeFade, -1.0 / 12.0, 1.0 / 12.0) * amount);
                half3 rotated = HsvToRgb(hsv);
                half3 tinted = c * lerp(1.0, HsvToRgb(half3(target, 1.0, 1.0)), amount * 0.3);
                return lerp(tinted, rotated, saturate(hsv.y * 4.0));
            }

            // 안료가 더 쌓인 색(얼룩, 백런 테두리, 경계 띠, 윤곽, 드리운 그림자): 진해질수록 파랑 쪽으로 돌고,
            // 거듭제곱으로 어두워져 채널 비율이 벌어지며 채도가 오른다. 채도는 조금만 더 올린다(많이 올리면 형광색).
            // 검정이나 회색을 곱하지 않으므로 탁해지지 않는다. 흰색(종이)은 그대로다.
            half3 Accumulate(half3 c, half amount)
            {
                c = ShiftHue(c, HUE_BLUE, saturate(amount) * _WC_CoolShift);
                half3 deep = pow(max(c, 1e-4), 1.0 + amount * 1.5);
                half l = Luminance(deep);
                return saturate(l + (deep - l) * (1.0 + amount * 0.1));
            }

            // 드리운 그림자: 그늘 색(hue) 쪽으로 색상환을 따라 돌린 색. RGB 로 섞거나 곱하면 보색끼리 만나
            // 회색·올리브로 탁해지므로(갈색 + 파랑) 색상만 돌리고, 채도는 지키고(무채색은 그늘 색을 옅게 띤다),
            // 명도는 조금만 내린다. 그래서 탁하거나 무겁지 않은 맑은 중간 밝기가 된다.
            // 도는 방향은 그늘 색 범위의 가운데(mid)를 기준으로 정한다. 범위 끝 색마다 따로 정하면 같은 색이 자리마다
            // 반대로 돌아(갈색이 한쪽은 초록, 한쪽은 보라) 얼룩덜룩해진다.
            half3 Shade(half3 c, half amount, half hue, half mid)
            {
                amount = saturate(amount);
                half3 hsv = RgbToHsv(c);
                half d = mid - hsv.x;
                d -= round(d);
                d += hue - mid;
                half turned = frac(hsv.x + d * saturate(amount * 1.5));
                // 채도가 거의 없는 색은 원래 색상이 의미 없으니 그늘 색상을 그대로 쓴다.
                hsv.x = lerp(hue, turned, saturate(hsv.y * 4.0));
                // 채도는 지키되 너무 진한 색은 중간 채도로 눌러 맑은 그늘이 되게 한다.
                hsv.y = clamp(hsv.y, 0.3 * amount, lerp(1.0, 0.5, amount));
                hsv.z *= 1.0 - amount * _WC_LightShade.y;
                return HsvToRgb(hsv);
            }

            // 빛 쪽 파스텔 색 조각: 색 물방울(drop, -1~1, 0 = 물방울 없음) 자리의 밝고 옅은 색.
            // 색상은 원래 색과 상관없이 정한다(종이와 흰 물감에는 쓰지 않는다). 물방울 부호로 차가운 쪽(연두~하늘)과
            // 따뜻한 쪽(라벤더~분홍)을 고르고, 그 안에서는 저주파 잡음(noise)으로 천천히 바꾼다.
            // 물방울 값으로 바로 색상을 정하면 한 물방울 안에 무지개가 생긴다.
            half3 PastelChip(half drop, half noise)
            {
                half hue = drop > 0.0 ? lerp(0.36, 0.56, noise) : lerp(0.74, 0.95, noise);
                return HsvToRgb(half3(hue, 0.3, 1.0));
            }

            // 물에 밀려 안료가 옅어진 색: 종이(흰색) 쪽으로 간다.
            half3 Dilute(half3 c, half amount)
            {
                return lerp(c, 1.0, saturate(amount));
            }

            // 화면 높이 비율(0~1)로 정한 폭을 value 의 단위로 바꾼다. value 가 한 화소에 변하는 양(fwidth)을 쓴다.
            // 그래서 같은 비율이면 물체 크기, 거리와 상관없이 화면에서 같은 폭이 된다.
            half ScreenWidth(half value, half screenFraction)
            {
                return max(fwidth(value), 1e-5) * screenFraction * _ScreenParams.y;
            }

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
                output.patternUV = TwistPatternUV(input.patternUV);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                clip(baseSample.a * _BaseColor.a - _Cutoff);
                #endif
                // 색 계산(색상 회전, 안료 쌓기)은 눈에 보이는 대로 동작하도록 sRGB 에서 하고 마지막에 선형으로 돌린다.
                half3 albedo = LinearToSRGB(baseSample.rgb * _BaseColor.rgb);
                // 매니저의 색 보정은 물감 색(본색)에만 건다. 그림자와 안료 축적은 보정된 색에서 계산되고,
                // 칠하지 않은 종이(흰색)는 영향을 받지 않는다. 화면 전체에 거는 Color Adjustments 와 다른 점이다.
                albedo = (albedo - 0.5) * (1.0 + _WC_Contrast) + 0.5;
                albedo *= 1.0 + _WC_Brightness;
                albedo = saturate(lerp(Luminance(albedo).xxx, albedo, 1.0 + _WC_Saturation));
                // 인스턴스마다 색 차이: 같은 나무라도 그루마다 색상·밝기·채도가 조금씩 다르다(무채색은 색상이 거의 안 바뀐다).
                if (any(_WC_InstanceColour.xyz > 0))
                {
                    half3 instance = InstanceRandom() * 2.0 - 1.0;
                    half3 instanceHsv = RgbToHsv(albedo);
                    instanceHsv.x = frac(instanceHsv.x + instance.x * _WC_InstanceColour.x);
                    instanceHsv.z = saturate(instanceHsv.z * (1.0 + instance.y * _WC_InstanceColour.y));
                    instanceHsv.y = saturate(instanceHsv.y * (1.0 + instance.z * _WC_InstanceColour.z));
                    albedo = HsvToRgb(instanceHsv);
                }

                // 색 카오스: 같은 색 안에 떨어뜨린 다른 색 물방울. 굽기 때 정한 덩어리와 물방울 모양대로 색상을 돌린다.
                // 채도가 거의 없는 색(회색, 흰색)은 색상이 의미가 없어서 거의 바뀌지 않는다(빛 쪽 파스텔 조각은 따로 보인다).
                half drop = 0.0;
                if (_HasPattern > 0.5)
                    drop = SAMPLE_TEXTURE3D(_ChaosTex, sampler_PatternTex, input.patternUV).r * 2.0 - 1.0;
                if (_HasPattern > 0.5 && _WC_ChaosHue > 0)
                {
                    half3 hsv = RgbToHsv(albedo);
                    hsv.x = frac(hsv.x + drop * _WC_ChaosHue);
                    albedo = HsvToRgb(hsv);
                }

                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 자기 그림자 무시: 그림자를 빛 쪽으로 이만큼 띄운 지점에서 확인한다. 그 거리 안의 가림(자기 몸)은
                // 그림자를 만들지 않고, 더 멀리 있는 것(나무 등)이 드리운 그림자는 그대로 받는다.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS + _MainLightPosition.xyz * _SelfShadowSkip));

                // 씬 뷰 평범 모드: 본색에 단순한 명암과 그림자만. 작업하는 사람 눈이 편하도록.
                if (_WC_Plain > 0.5)
                {
                    half lambert = saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                    half3 plain = baseSample.rgb * _BaseColor.rgb * (0.45 + 0.55 * lambert);
                    return half4(MixFog(plain, input.fogFactor), 1);
                }
                // 드리운 그림자만 쓴다. 면 방향으로 어두워지지 않는다.
                // 빛을 등진 면은 늘 자기 그림자 안이라, 그대로 받으면 볼록한 물체에 형태 그림자가 되살아난다.
                // 그래서 빛을 등진 면(덩어리 노멀 기준)은 그림자를 받지 않는다. 다른 물체가 드리운 그림자는
                // 빛을 향한 면과 바닥에만 남는다.
                half facing = smoothstep(0.15, 0.45, dot(normalWS, mainLight.direction));

                // 무늬: 구운 모델은 3D 무늬, 바닥은 바닥 무늬, 그 밖의 굽지 않은 물체는 무늬 없음(0.5).
                half4 pattern = 0.5;
                if (_HasPattern > 0.5)
                    pattern = SAMPLE_TEXTURE3D(_PatternTex, sampler_PatternTex, input.patternUV);
                else if (_Floor > 0.5)
                    pattern = FloorPattern(input.positionWS);

                // 드리운 그림자. 경계를 잡음으로 흔들어(붓 자국) 그림자 맵의 매끈한 선이 보이지 않게 한다.
                half shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _WC_ReceiveShadows * facing);
                half cast = saturate((shadowAtten - 0.5) * (1.0 + 2.0 * _WC_ShadowRagged) + 0.5 + (pattern.r - 0.5) * _WC_ShadowRagged);
                // 바닥은 윤곽, 경계 띠, 종이 영역, 크림 띠, 파스텔 조각을 쓰지 않는다. 넓은 면에서 윤곽(시선 각도 기준)은 멀수록
                // 진해지는 그라데이션이 되고, 종이 영역은 바닥 전체를 하얗게 만든다.
                half objectOnly = 1.0 - _Floor;


                // 빛이 받는 정도. 경계 위치는 구운 얼룩으로 살짝 흔든다(Boundary Distortion).
                // 형태(종이, 크림 띠, 경계 띠, 연필 쪽)는 형태용 빛을 따른다. 드리운 그림자와 위의 facing 은
                // 그림자 맵을 만든 주 광원을 따라야 서로 맞는다.
                float3 formLightDir = _WC_FormLight.w > 0.5 ? _WC_FormLight.xyz : mainLight.direction;
                half ndotlRaw = dot(normalWS, formLightDir) * 0.5 + 0.5;
                half ndotl = ndotlRaw + (pattern.r - 0.5) * 0.12;
                half paperSoft = ScreenWidth(ndotlRaw, _WC_PaperSoftness);
                half paperEdge = _WC_PaperThreshold - paperSoft;

                // 빛 띠: 빛(종이) 경계에서 화면 비율로 정한 거리까지, 빛에 가까울수록 밝고 옅은 크림색 쪽으로 바랜다.
                // 노랗게 물들이지 않으니 파랑이 녹색으로, 흰색이 탁한 노랑으로 가지 않는다. 바닥에는 쓰지 않는다.
                half warmZone = smoothstep(paperEdge - ScreenWidth(ndotlRaw, _WC_WarmWidth), paperEdge, ndotl) * objectOnly;
                half3 lightColor = lerp(albedo, _WC_LightCream.rgb, _WC_LightCream.a * warmZone);
                // 빛 쪽 파스텔 조각: 색 물방울 자리에 옅은 파스텔 색이 비친다. 물방울이 진할수록 또렷하다.
                // 흰색(채도와 밝기로 본 흰 물감)에는 넣지 않는다. 흰 새처럼 흰 물체는 깨끗하게 남는다.
                half3 albedoHsv = RgbToHsv(albedo);
                half notWhite = saturate(albedoHsv.y * 5.0 + (1.0 - albedoHsv.z) * 5.0);
                half chip = saturate(abs(drop) * 2.5) * _WC_LightShade.x * notWhite;
                lightColor = lerp(lightColor, PastelChip(drop, pattern.a), chip * warmZone * 0.8);
                // 그림자 안쪽 진하기는 고르지 않다(물감이 고르게 펴지지 않은 것처럼).
                half shadowAmount = _WC_ShadowDepth * (1.0 + (pattern.a - 0.5) * 2.0 * _WC_ShadowUneven);
                half3 shadowColor;
                if (_WC_PastelShadows > 0.5)
                {
                    // 맑은 그늘: 거의 어두워지지 않는 그늘 색. 그늘 색상은 구운 저주파 잡음으로 범위 안에서 섞인다.
                    half shadowHue = lerp(_WC_LightShade.z, _WC_LightShade.w, saturate((pattern.a - 0.5) * 2.5 + 0.5 + (pattern.r - 0.5) * 0.6));
                    shadowColor = Shade(albedo, shadowAmount, shadowHue, (_WC_LightShade.z + _WC_LightShade.w) * 0.5);
                }
                else
                {
                    // 예전 그림자: 회색을 곱하지 않고 같은 물감을 더 쌓은 것처럼(그래서 파랑 쪽으로) 어둡게 한다.
                    shadowColor = Accumulate(albedo, shadowAmount);
                }
                shadowColor = saturate(lerp(Luminance(shadowColor).xxx, shadowColor, 1.0 + _WC_ShadowSaturation));
                half3 color = lerp(shadowColor, lightColor, cast);
                // N: 0~1 저주파 잡음. 경계를 따라 안료가 진한 곳과 거의 안 쌓인 곳이 생기게 한다.
                half unevenness = 0.6 + 0.4 * pattern.a;

                half accumulate = 0.0;
                half pigmentAccumulate = 0.0; // 그중 얼룩과 백런 테두리 몫
                half dilute = 0.0;
                // 얼룩과 백런: 구운 모델과 바닥에 쓴다.
                if (_HasPattern > 0.5 || _Floor > 0.5)
                {
                    half blotch = (pattern.r * 2.0 - 1.0) * 0.35 * _WC_Blotches;
                    pigmentAccumulate = max(blotch, 0.0) + pattern.b * 0.9 * _WC_Backruns;
                    accumulate += pigmentAccumulate;
                    dilute += max(-blotch, 0.0) + pattern.g * 0.3 * _WC_Backruns;
                }
                // 덩어리 바깥 윤곽에만 안료가 고인다(노멀이 덩어리 노멀이라 조각 사이에는 생기지 않는다).
                // 폭은 화면 비율: 윤곽에서 몇 화소 안쪽까지인지로 정한다.
                half facingView = saturate(dot(normalWS, viewWS));
                half rim = 1.0 - saturate(facingView / ScreenWidth(facingView, _WC_OutlineWidth));
                // 화면에서 가는 조각(팔, 다리, 가지)은 몸 전체가 윤곽 띠 안에 들어가 통째로 진해지고 파래진다.
                // 그래서 조각(렌더러 경계 상자의 가장 짧은 변)이 윤곽 폭의 몇 배보다 가늘면 윤곽 안료를 줄인다.
                float3 pieceSize = unity_RendererBounds_Max.xyz - unity_RendererBounds_Min.xyz;
                float pieceWidth = min(pieceSize.x, min(pieceSize.y, pieceSize.z));
                float worldPerScreen = length(input.positionWS - GetCameraPositionWS()) * 2.0 / abs(UNITY_MATRIX_P[1][1]);
                half thinFade = smoothstep(4.0, 10.0, pieceWidth / max(worldPerScreen * _WC_OutlineWidth, 1e-5));
                accumulate += rim * _WC_OutlinePigment * 0.6 * unevenness * objectOnly * thinFade;

                // 빛: 빛을 정면으로 받는 쪽은 칠하지 않은 종이로 남긴다. 그림자가 드리운 곳에는 남기지 않는다.
                half paper = smoothstep(paperEdge, _WC_PaperThreshold + paperSoft, ndotl) * cast * objectOnly;
                // 경계 안료 축적 E(d): 종이 경계에 가까워질수록 커지는 색 쪽의 띠(폭은 화면 비율). 종이 안으로는 넘어가지 않는다.
                half band = smoothstep(paperEdge - ScreenWidth(ndotlRaw, _WC_BoundaryWidth), paperEdge, ndotl) * (1.0 - paper) * cast * objectOnly;
                accumulate += band * _WC_BoundaryPigment * unevenness;
                // 그림자 경계에 고인 안료: 그림자 쪽 경계의 좁은 띠. 바닥에도 적용한다.
                half shade = 1.0 - cast;
                half shadowEdge = smoothstep(0.15, 0.6, shade) * (1.0 - smoothstep(0.75, 1.0, shade));
                accumulate += shadowEdge * _WC_ShadowEdge * unevenness;

                color = Dilute(saturate(color), dilute * 0.5);
                half luminanceBefore = Luminance(color);
                color = Accumulate(color, accumulate * _WC_PigmentThickness);
                // 얼룩과 백런 테두리는 어두워진 만큼 채도를 더 올린다(Blotch Saturation). 어두워진 양 중 얼룩·백런 몫만 쓴다.
                half darkened = saturate(luminanceBefore - Luminance(color)) * saturate(pigmentAccumulate / max(accumulate, 1e-4));
                color = saturate(lerp(Luminance(color).xxx, color, 1.0 + darkened * 4.0 * _WC_BlotchSaturation));
                // 종이(흰색)에는 파스텔 조각을 넣지 않는다.
                color = lerp(color, 1.0, paper);

                // 장면의 빛 색 흐름: 아주 큰 월드 잡음으로 빛 쪽은 두 따뜻한 색, 그늘 쪽은 두 차가운 색 사이를 자리마다 오간다.
                // 곱해서 물들이므로 탁해지지 않고, 흰 종이도 빛 색을 받는다(흰색이 순수한 흰색이 아니다).
                // 빛과 그늘의 나눔은 형태 빛 방향과 드리운 그림자로 정하지만, 어둡게 하지 않고 색 온도만 바꾼다.
                half formLit = smoothstep(0.35, 0.65, ndotlRaw) * cast;
                if (_WC_SceneColour.x > 0)
                {
                    float2 sceneP = input.positionWS.xz / max(_WC_SceneColour.y, 0.1);
                    half warm = SceneNoise(sceneP) * 0.7 + SceneNoise(sceneP * 2.1 + 7.3) * 0.3;
                    half cool = SceneNoise(sceneP + 31.7) * 0.7 + SceneNoise(sceneP * 2.3 + 13.1) * 0.3;
                    half3 lightTint = lerp(_WC_LightTintA.rgb, _WC_LightTintB.rgb, smoothstep(0.3, 0.7, warm));
                    half3 shadeTint = lerp(_WC_ShadeTintA.rgb, _WC_ShadeTintB.rgb, smoothstep(0.3, 0.7, cool));
                    color = lerp(color, color * lerp(shadeTint, lightTint, formLit), _WC_SceneColour.x);
                }
                // 하늘·땅 반사색: 그늘 면이 위를 보면 하늘색, 아래·옆을 보면 따뜻한 땅색을 받는다.
                if (_WC_SceneColour.z > 0)
                {
                    half3 bounce = lerp(_WC_GroundBounce.rgb, _WC_SkyBounce.rgb, saturate(normalWS.y * 0.5 + 0.5));
                    color = lerp(color, color * bounce, _WC_SceneColour.z * (1.0 - formLit));
                }
                // 경계 보색 강조: 빛 경계 띠에 따뜻한 보색을 얇게 얹는다.
                color = lerp(color, _WC_EdgeAccent.rgb, band * _WC_SceneColour.w * 0.6);

                // 색연필: 구운 연필 덩어리 안에서만 선이 보인다. 선 색은 그 자리 물감 색을 더 쌓은 색이라 색마다 다르다.
                // 빛 쪽에 남긴 흰 종이에는 긋지 않는다. 선은 주로 형태 그림자 쪽(덩어리 노멀이 빛을 등진 쪽)에 모인다.
                // 칠은 형태 그림자를 쓰지 않으니, 연필이 그 자리의 어둠을 대신 그리는 셈이다. 경계는 화면 비율 폭으로
                // 흐려지고 무늬로 흔들어 매끈한 선이 보이지 않게 한다.
                if (_HasPattern > 0.5 && _WC_Pencil.x > 0)
                {
                    half3 pencilMap = SAMPLE_TEXTURE3D(_ChaosTex, sampler_PatternTex, input.patternUV).rgb;
                    // 각도 흔들림은 선 텍스처 안에서 선 뭉치마다 따로 들어가 있다.
                    half angle = _WC_Pencil.z;
                    half fade = ScreenWidth(ndotlRaw, _WC_PencilZone.x);
                    half shadowSide = 1.0 - smoothstep(0.5 - fade, 0.5 + fade, ndotlRaw + (pattern.r - 0.5) * 0.15);
                    half zoneWeight = lerp(1.0, shadowSide, _WC_PencilZone.y);
                    half stroke = PencilStrokes(input.positionWS, angle) * pencilMap.g * zoneWeight * _WC_Pencil.x * (1.0 - paper);
                    // 지금 칠해진 색 위에 쌓는다. 그림자처럼 이미 어두운 곳에서도 선이 그 색보다 진하게 보인다.
                    half3 lead = Accumulate(saturate(color), _WC_Pencil.y);
                    color = lerp(color, lead, saturate(stroke));
                }

                // 거리 안개: 멀수록 공기 색으로 바랜다. 지평선 쪽(시선이 수평)은 아래 색, 올려다보는 쪽은 위 색.
                if (_WC_Haze.x > 0)
                {
                    half hazeAmount = saturate((length(input.positionWS - GetCameraPositionWS()) - _WC_Haze.y) / max(_WC_Haze.z, 0.1)) * _WC_Haze.x;
                    half3 haze = lerp(_WC_HazeLow.rgb, _WC_HazeHigh.rgb, saturate(-viewWS.y * 2.0));
                    color = lerp(color, haze, hazeAmount);
                }

                color = SRGBToLinear(color);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

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
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                AlphaClip(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

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

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                AlphaClip(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

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
                float3 normalWS : TEXCOORD1;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                AlphaClip(input.uv);
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
                #else
                return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
