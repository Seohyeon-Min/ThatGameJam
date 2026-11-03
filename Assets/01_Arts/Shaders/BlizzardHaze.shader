// 눈보라 안개: 눈과 함께 바람에 쓸려 가는 옅은 안개 덩어리(파티클). 텍스처 없이 그린다.
// - 모양: 가운데가 진하고 가장자리로 부드럽게 사라지는 덩어리를, 월드 위치로 읽는 흐르는 노이즈로 찢어 솜처럼 일렁이게 한다.
//   노이즈가 월드에 붙어 있어서 덩어리가 지나가며 안개가 그 사이를 흘러가는 것처럼 보인다.
// - 땅, 나무와 만나는 곳은 깊이로 부드럽게 사라지고(soft particle), 카메라 바로 앞에서도 사라져 화면을 덮지 않는다.
// - 파티클 색(Start Color, Color over Lifetime)의 알파가 진하기다.
// - 시즌이 올라 눈이 그칠 때(SeasonSnow): 이 파티클은 Snowfall 의 자식이라 눈과 함께 새로 생기지 않고, 봄 링(_SpringRing)이
//   지나간 자리에서는 눈송이(SnowParticle)와 같은 거리 뒤에서 함께 걷힌다. 링이 끝나면 남은 것은 눈과 함께 지워진다.
Shader "ThatGameJam/BlizzardHaze"
{
    Properties
    {
        _Color ("Haze Colour", Color) = (0.93, 0.95, 1, 1)
        _Density ("Density", Range(0, 2)) = 1
        _Edge ("Blob Edge Softness", Range(0.05, 1)) = 0.6
        _NoiseScale ("Wisp Size (cells per metre)", Float) = 0.35
        _NoiseFlow ("Wisp Flow (world units per second)", Vector) = (0.4, 0, -2.5, 0)
        _Tear ("How Much The Wisps Tear It", Range(0, 1)) = 0.7
        _SoftDepth ("Soft Fade Into The Ground (metres)", Float) = 1.5
        _NearFade ("Fade Near The Camera (metres)", Vector) = (1.5, 5, 0, 0)
        _RingClearSoftness ("Clearing Edge Softness", Float) = 4
        _RingClearLag ("Clearing Behind The Front", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Haze"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Density;
                half _Edge;
                float _NoiseScale;
                float4 _NoiseFlow;
                half _Tear;
                float _SoftDepth;
                float4 _NearFade;
                float _RingClearSoftness;
                float _RingClearLag;
            CBUFFER_END

            float4 _SpringRing; // SpringRing 재생 중의 중심(xyz)과 반지름(w). 0 이면 재생 중이 아니다

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
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.uv = input.uv * 2.0 - 1.0;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // The blob: full in the middle, gone at its rim.
                half blob = 1.0 - smoothstep(1.0 - _Edge, 1.0, length(input.uv));

                // Wisps flowing through it with the wind, stuck to the world.
                float3 p = (input.positionWS - _Time.y * _NoiseFlow.xyz) * _NoiseScale;
                float wisps = Noise(p) * 0.6 + Noise(p * 2.7 + 13.1) * 0.3 + Noise(p * 6.1 + 41.7) * 0.1;
                half torn = saturate(lerp(1.0, smoothstep(0.25, 0.85, wisps), _Tear));

                // Soft where it meets the ground or a tree, and gone right in front of the camera.
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float ownDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                half soft = saturate((sceneDepth - ownDepth) / max(_SoftDepth, 1e-3));
                half near = saturate((ownDepth - _NearFade.x) / max(_NearFade.y - _NearFade.x, 1e-3));

                // The spring ring clears it as it does the snow: gone a little behind the ring's front.
                half cleared = 1.0;
                if (_SpringRing.w > 0.0)
                {
                    float fromRing = length(input.positionWS.xz - _SpringRing.xz) - (_SpringRing.w - _RingClearLag);
                    cleared = smoothstep(-_RingClearSoftness, _RingClearSoftness, fromRing);
                }

                half alpha = saturate(blob * torn * soft * near * cleared * input.color.a * _Density);
                half3 colour = _Color.rgb * input.color.rgb;
                return half4(MixFog(colour, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
