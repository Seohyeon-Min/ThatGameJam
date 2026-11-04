// 꿈속 안개(Stage3 안개 구역): 화면 전체를 분홍~주황의 따뜻한 안개로 채우고, 새를 뺀 모든 것을 원근이 있는 그림자(실루엣)로 바꾼다.
// DreamFogFeature 가 투명 물체까지 그린 뒤에 그린다. 새는 BirdVisible 이 스텐실에 1 을 써 두어서 그 자리는 건너뛴다(새만 그대로).
// - 안개: 아래 주황, 위 분홍으로 넘어가는 색에, 빛(해) 자리의 빛번짐(진한 심과 넓은 무리)과 천천히 흐르는 옅은 색 얼룩을 더한다.
// - 그림자: 장면의 색은 쓰지 않고 깊이만 쓴다. 가까울수록 진한 그림자, 멀수록 안개에 녹는다(원근). 경계는 몇 번 나눠 읽어 번지고,
//   빛 가까이의 그림자는 빛에 먹힌다. 나무 꼭대기도 하늘도 같은 깊이 규칙이라 화면 전체가 안개로 찬다.
// - 빛줄기: 각 화소에서 빛 쪽으로 그림자를 몇 번 짚어, 그림자 사이로 새어 나오는 빛을 밝힌다.
// - 필름 같은 잔 알갱이를 얹는다.
// - 세기: _DreamFogWeight(구역에 들어선 정도) × 그 자리 안개 밀도(FogField 의 칸, 아이와 새가 걷어 낸 곳은 옅다).
// 값은 DreamFog 가 전역으로 준다.
Shader "Hidden/ThatGameJam/DreamFog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        Pass
        {
            Name "DreamFog"
            // The bird (stencil 1 from BirdVisible) keeps its own colours.
            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _DreamFogWeight;     // 0 = off, 1 = full
            float4 _DreamLow;          // rgb fog colour at the bottom of the picture
            float4 _DreamHigh;         // rgb fog colour at the top
            float4 _DreamSunColor;     // rgb light colour, a strength
            float4 _DreamSun;          // xy light on the screen (0..1), z core size, w halo size (shares of the screen height)
            float4 _DreamBlob;         // rgb drifting colour patches, a strength
            float4 _DreamShadow;       // rgb shadow colour, a strength
            float4 _DreamDepth;        // x metres from the camera that stay fully shadow, y metres over which shadows melt into the fog,
                                       // z edge softness (pixels), w how much the light eats nearby shadows
            float4 _DreamRays;         // x strength of the light streaming between shadows, y grain, z cleared-fog floor, w unused
            TEXTURE2D(_DreamFogMask);  // FogField's density cells: 1 = full fog, 0 = cleared
            float4 _DreamFogArea;      // xy area corner (world x, z), zw size

            bool IsSky(float raw)
            {
                #if UNITY_REVERSED_Z
                return raw <= 1e-6;
                #else
                return raw >= 1.0 - 1e-6;
                #endif
            }

            // How much of a shadow there is at this pixel: 1 near, melting into the fog with distance, 0 for the sky.
            float ShadowAt(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (IsSky(raw))
                    return 0.0;
                float distance = LinearEyeDepth(raw, _ZBufferParams);
                return exp(-max(distance - _DreamDepth.x, 0.0) / max(_DreamDepth.y, 1e-3));
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float time = _Time.y;

                // The fog itself: orange low, pink high, soft drifting patches, the light's core and halo.
                float drift = Noise(uv * 3.0 + float2(time * 0.03, time * 0.017));
                half3 fog = lerp(_DreamLow.rgb, _DreamHigh.rgb, saturate(uv.y * 0.9 + 0.05 + (drift - 0.5) * 0.25));
                float patches = Noise(uv * 1.7 + float2(-time * 0.012, time * 0.02) + 7.3);
                fog = lerp(fog, _DreamBlob.rgb, smoothstep(0.55, 0.85, patches) * _DreamBlob.a);
                float2 toSun = (uv - _DreamSun.xy) * float2(aspect, 1.0);
                float sunDistance = length(toSun);
                float core = exp(-sunDistance * sunDistance / max(_DreamSun.z * _DreamSun.z, 1e-5));
                float halo = exp(-sunDistance / max(_DreamSun.w, 1e-4));
                fog += _DreamSunColor.rgb * _DreamSunColor.a * (core * 1.2 + halo * 0.45);

                // Shadows, their edges blurred a little: the middle and eight around it.
                float2 texel = _DreamDepth.z / _ScreenParams.xy;
                float shadow = ShadowAt(uv) * 0.2;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float angle = i * 0.785398;
                    shadow += ShadowAt(uv + float2(cos(angle), sin(angle)) * texel) * 0.1;
                }
                // Near the light the shadows are eaten by it.
                shadow *= 1.0 - saturate(core * _DreamDepth.w + halo * _DreamDepth.w * 0.3);

                // Light streaming between the shadows: how open the way to the light is, a few steps toward it.
                float open = 0.0;
                float2 step = (_DreamSun.xy - uv) / 10.0;
                [unroll] for (int s = 1; s <= 10; s++)
                    open += 1.0 - ShadowAt(uv + step * s);
                open /= 10.0;
                fog *= lerp(1.0 - _DreamRays.x * 0.5, 1.0 + _DreamRays.x * halo, open);

                half3 dream = lerp(fog, _DreamShadow.rgb, saturate(shadow * _DreamShadow.a));
                dream += (Hash(uv * _ScreenParams.xy + frac(time) * 61.0) - 0.5) * _DreamRays.y;

                // As thick as the fog is where this pixel looks: thinner where the baby and the bird cleared it.
                float raw = SampleSceneDepth(uv);
                float local = 1.0;
                if (!IsSky(raw))
                {
                    float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                    float2 cell = (world.xz - _DreamFogArea.xy) / max(_DreamFogArea.zw, 1e-3);
                    if (all(cell >= 0.0) && all(cell <= 1.0))
                        local = lerp(_DreamRays.z, 1.0, SAMPLE_TEXTURE2D_LOD(_DreamFogMask, sampler_LinearClamp, cell, 0).r);
                }
                return half4(lerp(scene, dream, saturate(_DreamFogWeight * local)), 1.0);
            }
            ENDHLSL
        }
    }
}
