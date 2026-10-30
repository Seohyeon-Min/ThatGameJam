// 색이 비슷한 이웃끼리만 섞는 3x3 bilateral 블러. WatercolorBlurFeature 가 한 패스로 부른다.
// 색 차이가 임계값을 넘는 이웃은 가중치가 0 이 되어 대비가 큰 경계(실루엣, 눈, 가는 가지)는 그대로 남는다.
// 수채화 무늬(얼룩, 백런)는 화면에 고정되면 안 되므로 여기서 만들지 않는다. WatercolorBaker 가 모델마다 굽는다.
// 종이 질감은 반대로 화면에 고정해 곱한다. 원근 없이 평면으로 깔려 "종이 위에 그린 그림"처럼 보인다.
// 빛 쪽에 남긴 흰 부분(칠하지 않은 종이)에서는 종이 색과 결이 그대로 드러난다.
// 0 Blur      : 색만 본다
// 1 BlurDepth : 깊이 차이가 큰 이웃도 뺀다(색이 비슷한 다른 물체끼리 번지지 않게)
Shader "Hidden/ThatGameJam/WatercolorBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        float4 _BlurParams; // 샘플 간격 u, 샘플 간격 v, 1/색 임계값, 블러 강도
        float4 _BlurDepth;  // 깊이 임계값(상대)
        float4 _Paper;      // 종이 강도, 화면 높이에 타일이 몇 번 들어가는지, 화면 가로세로비, 종이 사용(0/1)
        float _WC_Tonemapping;  // WatercolorManager 의 톤매핑 정도(0~1)
        float4 _WC_Gouache;     // x 구아슈 정도. 불투명 물감은 종이 결을 덮어서 종이 질감이 약해진다.
        float4 _WC_GouacheEdge; // x 윤곽 오돌토돌함 폭(1080p 화소), z 붓 무늬 한 칸의 월드 크기(m)

        float EdgeHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float EdgeNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(EdgeHash(i), EdgeHash(i + float2(1, 0)), f.x), lerp(EdgeHash(i + float2(0, 1)), EdgeHash(i + float2(1, 1)), f.x), f.y);
        }

        // 구아슈 윤곽: 색이 크게 바뀌는 경계(실루엣, 그림자 경계)에서만, 마른 붓 결 모양으로 이웃 색을 끌어와 덮는다.
        // 물체 색이 바깥으로도 삐져나오고 바탕이 안으로도 파고들어 오돌토돌해진다. 깎아 내는 게 아니라서 가는 가지도 남는다.
        // 무늬는 깊이로 되살린 월드 위치에 붙인다(카메라가 보는 평면, 월드 고정). 그래서 카메라가 움직여도 무늬가
        // 화면에서 지글거리지 않고 그림처럼 제자리에 있다.
        float3 RoughEdges(float2 uv, float3 color)
        {
            float pixelsPer1080 = _ScreenParams.y / 1080.0;
            float rawDepth = SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
            rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
            #endif
            float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            // 무늬 한 칸의 크기는 월드 고정(_WC_GouacheEdge.z 미터). 거리로 맞추면 카메라 이동에 배율이 바뀌어 무늬가 미끄러진다.
            float2 p = float2(dot(positionWS, UNITY_MATRIX_V[0].xyz), dot(positionWS, UNITY_MATRIX_V[1].xyz)) / max(_WC_GouacheEdge.z, 1e-4);
            // 붓질 결: 가로로 길고 세로로 촘촘한 잡음 두 개로 옮길 방향과 거리를 정한다.
            float2 n = float2(EdgeNoise(p / float2(24.0, 6.0)), EdgeNoise(p / float2(24.0, 6.0) + 31.7)) - 0.5;
            float2 offset = n * 2.0 * _WC_GouacheEdge.x * pixelsPer1080 / _ScreenParams.xy;
            float3 moved = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, 0).rgb;
            float edge = smoothstep(0.04, 0.12, length(moved - color));
            return lerp(color, moved, edge * _WC_Gouache.x);
        }

        TEXTURE2D(_PaperTex);

        float SceneEyeDepth(float2 uv)
        {
            float raw = SampleSceneDepth(uv);
            if (unity_OrthoParams.w > 0.5)
            {
                #if UNITY_REVERSED_Z
                raw = 1.0 - raw;
                #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
            }
            return LinearEyeDepth(raw, _ZBufferParams);
        }

        float3 Bilateral(float2 uv, bool useDepth)
        {
            float3 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            float centerDepth = useDepth ? SceneEyeDepth(uv) : 0;
            float3 sum = center;
            float weightSum = 1.0;

            [unroll]
            for (int j = -1; j <= 1; j++)
            {
                [unroll]
                for (int i = -1; i <= 1; i++)
                {
                    if (i == 0 && j == 0)
                        continue;
                    float2 sampleUV = uv + float2(i, j) * _BlurParams.xy;
                    float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sampleUV, 0).rgb;
                    // 공간 가중치는 가우시안 근사(축 1, 대각 0.5), 색 가중치는 임계값에서 0 이 되는 이차 곡선.
                    float w = (i != 0 && j != 0) ? 0.5 : 1.0;
                    float similarity = saturate(1.0 - length(c - center) * _BlurParams.z);
                    w *= similarity * similarity;
                    if (useDepth)
                    {
                        float d = SceneEyeDepth(sampleUV);
                        w *= saturate(1.0 - abs(d - centerDepth) / (centerDepth * _BlurDepth.x));
                    }
                    sum += c * w;
                    weightSum += w;
                }
            }

            return lerp(center, sum / weightSum, _BlurParams.w);
        }

        float4 Watercolor(float2 uv, bool useDepth)
        {
            float alpha = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).a;
            float3 color = Bilateral(uv, useDepth);
            if (_WC_Gouache.x > 0 && _WC_GouacheEdge.x > 0)
                color = RoughEdges(uv, color);
            // URP 톤매핑에는 세기 조절이 없어서 여기서 Neutral 톤매핑을 원본과 섞는다(수채화 볼륨에서는 URP 톤매핑을 끈다).
            // 종이는 그 뒤에 곱해서, 종이 질감 자체는 톤매핑으로 눌리지 않는다.
            color = lerp(color, NeutralTonemap(color), _WC_Tonemapping);
            if (_Paper.w > 0.5)
            {
                // 물감은 종이 위에 투명하게 얹히므로 곱한다. 흰색(칠하지 않은 곳)은 종이 그대로가 된다.
                float2 paperUV = uv * float2(_Paper.z, 1.0) * _Paper.y;
                float3 paper = SAMPLE_TEXTURE2D_LOD(_PaperTex, sampler_LinearRepeat, paperUV, 0).rgb;
                color *= lerp(1.0, paper, _Paper.x * (1.0 - 0.75 * _WC_Gouache.x));
            }
            return float4(color, alpha);
        }

        float4 FragBlur(Varyings input) : SV_Target { return Watercolor(input.texcoord, false); }
        float4 FragBlurDepth(Varyings input) : SV_Target { return Watercolor(input.texcoord, true); }
        ENDHLSL

        Pass
        {
            Name "Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            Name "BlurDepth"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurDepth
            ENDHLSL
        }
    }
}
