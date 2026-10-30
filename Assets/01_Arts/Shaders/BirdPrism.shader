// 새 전용: 혼자 빛나는 구아슈/파스텔 새. 폴리곤 단위가 아니라 큰 물감 덩어리로 칠한다.
// - 하얗게 타는 중심: 몸통과 날개 앞날이 흰색, 그 바깥이 따뜻한 노랑, 그 바깥이 물감 덩어리다(BirdPrismBaker 가 열기를 정점에 굽는다).
// - 물감 덩어리: 몸에서 날개 끝으로 뻗는 붓 자국마다 라벤더, 파랑, 분홍, 노랑 중 한 색. 드물게 초록 포인트와 흰 자국이 낀다.
// - 글로우: 1 이 넘는 값을 내보내 볼륨의 블룸이 번지게 한다. 일부 붓 자국이 제 박자로 반짝인다.
// 금색 윤곽과 가루는 BirdPrismGlow, 흩날리는 조각은 BirdPrismPiece 가 그린다. 그림자와 안개는 받지 않는다.
Shader "ThatGameJam/BirdPrism"
{
    Properties
    {
        [Header(Paint Patches)]
        _Colour1 ("Colour 1", Color) = (0.74, 0.62, 1, 1)
        _Colour2 ("Colour 2", Color) = (0.45, 0.58, 1, 1)
        _Colour3 ("Colour 3", Color) = (1, 0.68, 0.84, 1)
        _Colour4 ("Colour 4", Color) = (1, 0.84, 0.42, 1)
        _Accent ("Accent Colour", Color) = (0.3, 0.85, 0.45, 1)
        _AccentShare ("Accent Share", Range(0, 0.3)) = 0.05
        _WhiteShare ("White Stroke Share", Range(0, 0.5)) = 0.08
        _StrokesAround ("Strokes Around The Body", Range(2, 40)) = 24
        _StrokesAlong ("Strokes Along The Wing", Range(1, 20)) = 8
        _StrokeWobble ("Stroke Wobble", Range(0, 1)) = 0.35
        _Bristle ("Bristle Grain", Range(0, 0.5)) = 0.14
        _BristleSize ("Bristle Fineness", Range(1, 30)) = 9

        [Header(White Hot Core)]
        _CoreColour ("Core Colour", Color) = (1, 0.98, 0.9, 1)
        _WarmColour ("Warm Colour", Color) = (1, 0.82, 0.42, 1)
        _HeatStart ("Warm Starts At (lower = wider)", Range(0, 1)) = 0.45
        _WarmWidth ("Warm Band Width", Range(0, 0.6)) = 0.15
        _HeatRough ("Heat Edge Roughness", Range(0, 1)) = 0.35

        [Header(Gouache)]
        _Terminator ("Light To Shade Position", Range(0, 1)) = 0.35
        _TransitionWidth ("Transition Colour Width", Range(0, 0.3)) = 0.06
        _ShadeDepth ("Shade Darkness", Range(0, 1)) = 0.22
        _ShadeColour ("Shade Colour", Color) = (0.45, 0.42, 0.95, 1)
        _ShadeTurn ("Shade Turns Toward Shade Colour", Range(0, 1)) = 0.4

        [Header(Glow)]
        _Glow ("Patch Glow (above 1 goes into the bloom)", Range(0, 8)) = 0.85
        _CoreGlow ("Core Glow", Range(0, 12)) = 2
        _Twinkle ("Twinkle", Range(0, 8)) = 1.5
        _TwinkleSpeed ("Twinkle Speed", Range(0, 8)) = 1.6
        _TwinkleShare ("Twinkling Share Of Strokes", Range(0, 1)) = 0.2

        [Header(Surface)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "BirdPrism.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Colour1;
            half4 _Colour2;
            half4 _Colour3;
            half4 _Colour4;
            half4 _Accent;
            half _AccentShare;
            half _WhiteShare;
            half _StrokesAround;
            half _StrokesAlong;
            half _StrokeWobble;
            half _Bristle;
            half _BristleSize;
            half4 _CoreColour;
            half4 _WarmColour;
            half _HeatStart;
            half _WarmWidth;
            half _HeatRough;
            half _Terminator;
            half _TransitionWidth;
            half _ShadeDepth;
            half4 _ShadeColour;
            half _ShadeTurn;
            half _Glow;
            half _CoreGlow;
            half _Twinkle;
            half _TwinkleSpeed;
            half _TwinkleShare;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // 비트 64: 금색 윤곽(BirdPrismGlow)이 몸 위에는 그려지지 않게 표시한다.
            Stencil { Ref 64 WriteMask 64 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 rest : TEXCOORD1;   // xyz 쉬는 자세 위치(날개 절반 길이가 1), w 열기
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 rest : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.rest = input.rest;
                return output;
            }

            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 rest = input.rest.xyz;
                float2 radial = PrismRadial(rest);

                // 붓 자국 한 칸: 몸에서 날개 끝으로 길게 뻗고, 경계는 일렁인다. 칸마다 색이 하나다.
                float wobble = PrismNoise(rest.xy * 7.0) - 0.5;
                float around = radial.x * _StrokesAround + wobble * _StrokeWobble * 2.0;
                float column = floor(around);
                float along = radial.y * _StrokesAlong + PrismHash(float2(column, 3.7)) * 7.0 + wobble * _StrokeWobble;
                float2 cell = float2(column + (rest.x < 0.0 ? 37.0 : 0.0), floor(along));
                float pick = PrismHash(cell);
                float kind = PrismHash(cell + 11.3);
                float shade = PrismHash(cell + 23.7);

                half3 patchColour = pick < 0.25 ? _Colour1.rgb : (pick < 0.5 ? _Colour2.rgb : (pick < 0.75 ? _Colour3.rgb : _Colour4.rgb));
                if (kind < _WhiteShare)
                    patchColour = _CoreColour.rgb;
                else if (kind < _WhiteShare + _AccentShare)
                    patchColour = _Accent.rgb;
                patchColour *= 0.9 + 0.2 * shade;

                // 붓결: 자국을 따라 긴 결. 물감의 짙고 옅음과 열기 경계의 거칠기에 함께 쓴다.
                float bristle = PrismNoise(float2(around * _BristleSize, along * 1.5));
                patchColour *= 1.0 + (bristle - 0.5) * 2.0 * _Bristle;

                // 구아슈 명암: 빛, 전환색, 그늘 세 단계. 그늘은 어두워지기보다 차가워진다.
                float3 normalWS = normalize(input.normalWS) * (frontFace ? 1.0 : -1.0);
                half ndotl = dot(normalWS, _MainLightPosition.xyz) * 0.5 + 0.5;
                half level = 0.5 * step(_Terminator - _TransitionWidth, ndotl) + 0.5 * step(_Terminator + _TransitionWidth, ndotl);
                half3 shadeTone = lerp(patchColour, _ShadeColour.rgb, _ShadeTurn) * (1.0 - _ShadeDepth);
                half3 color = lerp(shadeTone, patchColour, level);

                // 열기: 중심으로 갈수록 노랑, 그다음 흰색. 경계는 붓결로 갈라진다.
                float heat = input.rest.w + (bristle - 0.5) * _HeatRough;
                half warm = smoothstep(_HeatStart, _HeatStart + 0.04, heat);
                half core = smoothstep(_HeatStart + _WarmWidth, _HeatStart + _WarmWidth + 0.04, heat);
                color = lerp(color, _WarmColour.rgb, warm);
                color = lerp(color, _CoreColour.rgb, core);

                half3 glow = color * lerp(_Glow, _CoreGlow, core);

                // 반짝임: 일부 붓 자국이 제 박자로 잠깐씩 밝아진다.
                float pulse = pow(saturate(sin(_Time.y * _TwinkleSpeed + PrismHash(cell + 41.9) * 6.2831853) * 0.5 + 0.5), 8.0);
                glow += color * step(PrismHash(cell + 53.1), _TwinkleShare) * pulse * _Twinkle;

                return half4(glow, 1);
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
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half ShadowFrag() : SV_Target { return 0; }
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
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target { return half4(NormalizeNormalPerPixel(input.normalWS), 0.0); }
            ENDHLSL
        }
    }
}
