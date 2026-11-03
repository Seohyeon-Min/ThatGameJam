// 잎 카드: 나무 모델의 잎(LeafCard_*)마다 따로 있는 사각형을 자기 중심(오브젝트 원점)에서 카메라 쪽으로 세운다.
// 나머지는 예전 잎 셰이더 그래프(SG_LeafWind)와 같다: 같은 텍스처 이름, 정점이 X 로 흔들리고, 흐르는 노이즈 두 겹으로 텍스처 좌표를
// 일렁여서 잎 덩어리가 뭉실뭉실 움직인다. 빛은 카드의 원래 방향으로 받아서 카드마다 밝기가 달라 덩어리감이 남는다.
// 그림자는 원래 방향의 카드로 드리운다(예전 그림자 모양 그대로). Static Batching 을 켜면 원점 정보가 사라져 동작하지 않는다.
Shader "ThatGameJam/LeafCard"
{
    Properties
    {
        [MainTexture] _GrassTexture ("Texture", 2D) = "white" {}
        _GrassWindMask ("Wind Mask", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _SwaySpeed ("Sway Speed", Float) = 0.5
        _SwayAmount ("Sway Amount (object space)", Float) = 0.08
        _BoilSpeed ("Boil Speed (UV per second)", Vector) = (0.08, 0.056, 0, 0)
        _BoilScale ("Boil Noise Scale", Float) = 10
        _BoilAmount ("Boil Amount (UV)", Float) = 0.1
        _Roll ("Random Roll (share of a turn)", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"

        TEXTURE2D(_GrassTexture);
        SAMPLER(sampler_GrassTexture);
        TEXTURE2D(_GrassWindMask);
        SAMPLER(sampler_GrassWindMask);

        CBUFFER_START(UnityPerMaterial)
            float4 _GrassTexture_ST;
            float4 _GrassWindMask_ST;
            half _Cutoff;
            float _SwaySpeed;
            float _SwayAmount;
            float4 _BoilSpeed;
            float _BoilScale;
            float _BoilAmount;
            float _Roll;
            half _Brightness;
        CBUFFER_END

        // Shader Graph 의 Simple Noise(Deterministic) 와 같은 값.
        float ValueNoise(float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
            float r0, r1, r2, r3;
            Hash_Tchou_2_1_float(i, r0);
            Hash_Tchou_2_1_float(i + float2(1.0, 0.0), r1);
            Hash_Tchou_2_1_float(i + float2(0.0, 1.0), r2);
            Hash_Tchou_2_1_float(i + float2(1.0, 1.0), r3);
            return lerp(lerp(r0, r1, f.x), lerp(r2, r3, f.x), f.y);
        }

        float SimpleNoise(float2 uv, float scale)
        {
            float value = 0.0;
            for (int octave = 0; octave < 3; octave++)
            {
                float frequency = pow(2.0, octave);
                value += ValueNoise(uv * (scale / frequency)) * pow(0.5, 3 - octave);
            }
            return value;
        }

        // 예전 그래프의 흔들림: 바람 마스크 r 만큼 오브젝트 X 로.
        float3 Sway(float3 positionOS, float2 uv)
        {
            float mask = SAMPLE_TEXTURE2D_LOD(_GrassWindMask, sampler_GrassWindMask, uv, 0).r;
            positionOS.x += mask * sin(_Time.y * _SwaySpeed) * _SwayAmount;
            return positionOS;
        }

        // 카드(XY 평면 사각형)를 원점에서 카메라의 오른쪽/위 방향으로 펼친다. 크기는 오브젝트 배율을 따른다.
        // 카드마다 화면에서 도는 각도가 달라서(원점 위치로 정한다, 늘 같다) 잎 텍스처가 모두 같은 방향으로 서지 않는다.
        float3 CardPositionWS(float3 positionOS)
        {
            float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
            float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
            float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
            float roll = frac(sin(dot(originWS, float3(12.9898, 78.233, 37.719))) * 43758.5453) * _Roll * TWO_PI;
            float2 flat = float2(positionOS.x * scaleX, positionOS.y * scaleY);
            float s = sin(roll), c = cos(roll);
            flat = float2(flat.x * c - flat.y * s, flat.x * s + flat.y * c);
            return originWS + UNITY_MATRIX_V[0].xyz * flat.x + UNITY_MATRIX_V[1].xyz * flat.y;
        }

        // 예전 그래프의 뭉실뭉실: 시간에 따라 흐르는 노이즈 두 겹으로 텍스처 좌표를 민다.
        float2 BoilUV(float2 uv)
        {
            float2 flowing = uv + _Time.y * _BoilSpeed.xy;
            float x = SimpleNoise(flowing, _BoilScale);
            float y = SimpleNoise(flowing + float2(17.3, 5.1), _BoilScale);
            return uv + (float2(x, y) - 1.0) * _BoilAmount;
        }

        half4 SampleLeaf(float2 uv)
        {
            half4 albedo = SAMPLE_TEXTURE2D(_GrassTexture, sampler_GrassTexture, BoilUV(uv));
            clip(albedo.a - _Cutoff);
            return albedo;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.uv = TRANSFORM_TEX(input.uv, _GrassTexture);
                output.positionWS = CardPositionWS(Sway(input.positionOS.xyz, input.uv));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                // The card's own facing, turned to the camera's side as a two-sided leaf shows it.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS = dot(normalWS, GetWorldSpaceViewDir(output.positionWS)) < 0 ? -normalWS : normalWS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SampleLeaf(input.uv);
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 diffuse = mainLight.color * saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half3 colour = albedo.rgb * _Brightness * (diffuse + SampleSH(normalWS));
                return half4(MixFog(colour, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
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

            // The card as modelled, not turned to the camera, so the shadows keep their old shapes.
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(Sway(input.positionOS.xyz, input.uv));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = TRANSFORM_TEX(input.uv, _GrassTexture);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                SampleLeaf(input.uv);
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
            #pragma vertex Vert
            #pragma fragment Frag
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformWorldToHClip(CardPositionWS(Sway(input.positionOS.xyz, input.uv)));
                output.uv = TRANSFORM_TEX(input.uv, _GrassTexture);
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                SampleLeaf(input.uv);
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
            #pragma vertex Vert
            #pragma fragment Frag
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = CardPositionWS(Sway(input.positionOS.xyz, input.uv));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _GrassTexture);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS = dot(normalWS, GetWorldSpaceViewDir(positionWS)) < 0 ? -normalWS : normalWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                SampleLeaf(input.uv);
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
