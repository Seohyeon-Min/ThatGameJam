// 수채화 느낌의 가벼운 불투명 셰이더. 텍스처 샘플은 Base Map 하나뿐이고 안료 얼룩은 ALU 잡음으로 만든다.
// - 명암: half-Lambert 를 부드러운 계단으로 묶고, 그림자 쪽은 Shadow Color 로 물들인다.
// - 안료 얼룩: 월드(또는 오브젝트) 공간 3D value noise. 물체에 붙어 있어 카메라가 움직여도 미끄러지지 않는다.
// - 붓결: 한 축으로 늘인 잡음. 같은 방향으로 칠한 붓자국처럼 보인다.
// - 실루엣: 가장자리를 살짝 진하게 해 물체 경계가 읽히게 한다.
// 조명은 메인 라이트(그림자 포함) + 추가 라이트(Forward+) + SH 앰비언트. 반사광과 스페큘러는 없다.
Shader "ThatGameJam/WatercolorLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.55, 0.6, 0.8, 1)

        [Header(Lighting)]
        _LightThreshold ("Light Threshold", Range(0, 1)) = 0.5
        _LightSoftness ("Light Softness", Range(0.01, 0.5)) = 0.2
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 1

        [Header(Pigment)]
        _PigmentStrength ("Pigment Strength", Range(0, 1)) = 0.5
        _PigmentScale ("Pigment Scale (world units)", Float) = 1.5
        [Toggle(_PIGMENT_OBJECT_SPACE)] _PigmentObjectSpace ("Pigment In Object Space", Float) = 0

        [Header(Brush)]
        _BrushStrength ("Brush Strength", Range(0, 1)) = 0.3
        _BrushScale ("Brush Scale (world units)", Float) = 0.25
        _BrushDirection ("Brush Direction", Vector) = (0, 1, 0, 0)

        [Header(Edges)]
        _EdgeDarkening ("Edge Darkening", Range(0, 1)) = 0.3

        [Header(Surface)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // SRP Batcher 를 쓰려면 모든 패스가 같은 머티리얼 상수 버퍼를 선언해야 한다.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half _LightThreshold;
            half _LightSoftness;
            half _AmbientStrength;
            half _PigmentStrength;
            float _PigmentScale;
            half _BrushStrength;
            float _BrushScale;
            float4 _BrushDirection;
            half _EdgeDarkening;
            half _Cutoff;
        CBUFFER_END

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

            #pragma shader_feature_local _PIGMENT_OBJECT_SPACE
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 noisePos : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 3D value noise. 해시 8 번. 텍스처 없이 끊김 없는 입체 잡음을 낸다.
            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x);
                float b = lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x);
                float c = lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x);
                float d = lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            // Bousseau 외 (2006) 안료 밀도 모델. d > 1 이면 진해지고, 흰색은 그대로 남는다.
            half3 ApplyDensity(half3 c, half d)
            {
                return c * (1.0 - (1.0 - c) * (d - 1.0));
            }

            // half-Lambert 를 부드러운 계단으로. offset 으로 경계를 잡음만큼 흔들어 붓으로 그은 듯한 명암 경계를 만든다.
            half Ramp(half ndotl, half offset)
            {
                half h = ndotl * 0.5 + 0.5 + offset;
                return smoothstep(_LightThreshold - _LightSoftness, _LightThreshold + _LightSoftness, h);
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
                #if defined(_PIGMENT_OBJECT_SPACE)
                // 움직이는 캐릭터는 오브젝트 공간을 써야 얼룩이 몸에 붙어 다닌다.
                output.noisePos = input.positionOS.xyz;
                #else
                output.noisePos = positions.positionWS;
                #endif
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
                half3 albedo = baseSample.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(input.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 저주파 안료 얼룩과, 한 축으로 늘인 붓결 잡음.
                half pigment = ValueNoise3(input.noisePos / _PigmentScale) * 2.0 - 1.0;
                float3 brushDir = normalize(_BrushDirection.xyz + 1e-5);
                float3 brushPos = input.noisePos / _BrushScale;
                // 붓 방향 성분을 줄여 그 방향으로 길쭉한 결을 만든다.
                brushPos -= brushDir * dot(brushPos, brushDir) * 0.85;
                half brush = ValueNoise3(brushPos) * 2.0 - 1.0;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ramp = Ramp(dot(normalWS, mainLight.direction), pigment * _PigmentStrength * 0.15);
                half lit = ramp * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                // 그림자를 검게 빼지 않고 Shadow Color 로 물들인다. 수채화는 그림자도 색이다.
                half3 lighting = lerp(_ShadowColor.rgb, 1.0, lit) * mainLight.color;

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half addRamp = Ramp(dot(normalWS, light.direction), 0);
                    lighting += light.color * addRamp * light.distanceAttenuation * light.shadowAttenuation;
                LIGHT_LOOP_END
                #endif

                lighting += SampleSH(normalWS) * _AmbientStrength;
                half3 color = albedo * lighting;

                // 실루엣 쪽 안료 고임. 시선과 거의 수직인 면만 진하게 한다.
                // 문턱을 높게 두는 이유: 비스듬히 보이는 넓은 바닥 전체가 어두워지지 않게 하려고.
                half rim = smoothstep(0.75, 1.0, 1.0 - saturate(dot(normalWS, viewWS)));
                half density = 1.0 + pigment * _PigmentStrength * 0.4 + brush * _BrushStrength * 0.3 + rim * _EdgeDarkening * 0.8;
                color = ApplyDensity(saturate(color), clamp(density, 0.0, 2.0));

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
