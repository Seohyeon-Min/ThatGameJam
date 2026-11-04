// 오클루전 페이드를 매끄럽게 만든다. OcclusionFadeFeature 가 후처리 전에 부른다.
// WatercolorFlat 의 나무는 새나 아기 앞, 화면 원 안에서 4x4 규칙 무늬로 화소를 뺀다(CameraFadeClip). 여기서 그 자리만
// 4x4 화소를 평균내 뺀 화소와 남은 화소를 섞는다. 무늬가 사라지고 나무가 반투명하게 보인다.
// 깊이 텍스처에는 나무가 빠지지 않고 다 있어서(DepthOnly 는 자르지 않는다) 뺀 구멍 자리도 나무로 잡힌다.
// 0 Copy, 1 Resolve(새 스텐실 64 와 아기 128 은 빼고)
Shader "Hidden/ThatGameJam/OcclusionFade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _WC_OcclusionTarget0;
            float4 _WC_OcclusionTarget1;
            float4 _OcclusionFade; // 반경(m), 부드러움(m) — 나무 머티리얼의 Fade Radius / Fade Softness 와 같게

            // WatercolorFlat 의 OcclusionVisible 과 같은 계산. 0 = 다 빠짐, 1 = 그대로.
            float Visible(float3 positionWS, float4 targetWS)
            {
                if (targetWS.w < 0.5)
                    return 1.0;
                float4 target = mul(UNITY_MATRIX_VP, float4(targetWS.xyz, 1));
                float4 here = mul(UNITY_MATRIX_VP, float4(positionWS, 1));
                if (target.w <= 0.0 || here.w > target.w - 0.3)
                    return 1.0;
                float2 pixels = (here.xy / here.w - target.xy / target.w) * 0.5 * _ScreenParams.xy;
                float pixelsPerMetre = 0.5 * _ScreenParams.y * abs(UNITY_MATRIX_P[1][1]) / (unity_OrthoParams.w > 0.5 ? 1.0 : target.w);
                float metres = length(pixels) / max(pixelsPerMetre, 1e-4);
                return saturate((metres - _OcclusionFade.x) / max(_OcclusionFade.y, 1e-4));
            }

            float4 FragCopy(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
            }

            float4 FragResolve(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 colour = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float rawDepth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float thinned = 1.0 - min(Visible(positionWS, _WC_OcclusionTarget0), Visible(positionWS, _WC_OcclusionTarget1));
                if (thinned <= 0.0)
                    return colour;

                // 이 화소가 든 4x4 칸의 평균: 규칙 무늬 한 주기를 정확히 덮어 무늬가 남지 않는다.
                float2 texel = 1.0 / _ScreenParams.xy;
                float3 sum = 0;
                [unroll]
                for (int y = -2; y < 2; y++)
                {
                    [unroll]
                    for (int x = -2; x < 2; x++)
                        sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(x, y) * texel, 0).rgb;
                }
                // 가장자리(거의 안 빠진 곳)까지 부드럽게 이어지도록 빠진 만큼보다 조금 넉넉히 섞는다.
                colour.rgb = lerp(colour.rgb, sum / 16.0, saturate(thinned * 3.0));
                return colour;
            }
        ENDHLSL

        Pass
        {
            Name "Copy"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy
            ENDHLSL
        }
        Pass
        {
            Name "Resolve"
            Stencil { Ref 0 ReadMask 192 Comp Equal }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragResolve
            ENDHLSL
        }
    }
}
