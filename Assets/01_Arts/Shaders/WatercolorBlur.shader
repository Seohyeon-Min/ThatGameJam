// 색이 비슷한 이웃끼리만 섞는 3x3 bilateral 블러. WatercolorBlurFeature 가 한 패스로 부른다.
// 색 차이가 임계값을 넘는 이웃은 가중치가 0 이 되어 대비가 큰 경계(실루엣, 눈, 가는 가지)는 그대로 남는다.
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
        float4 _BlurDepth;  // 깊이 임계값(상대), 사용 안 함

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

        float4 Bilateral(float2 uv, bool useDepth)
        {
            float4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
            float centerDepth = useDepth ? SceneEyeDepth(uv) : 0;
            float3 sum = center.rgb;
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
                    float similarity = saturate(1.0 - length(c - center.rgb) * _BlurParams.z);
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

            float3 blurred = sum / weightSum;
            return float4(lerp(center.rgb, blurred, _BlurParams.w), center.a);
        }

        float4 FragBlur(Varyings input) : SV_Target { return Bilateral(input.texcoord, false); }
        float4 FragBlurDepth(Varyings input) : SV_Target { return Bilateral(input.texcoord, true); }
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
