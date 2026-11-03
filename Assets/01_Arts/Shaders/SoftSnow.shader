// 부드러운 눈 덩어리: 아이 몸에 얹힌 눈(Snow_Clumps)처럼 작은 덩어리가 많은 메시를 한 덩어리 눈처럼 보이게 한다.
// - 가장자리: 화면에서 옆으로 누운 면일수록 투명해져 덩어리마다의 윤곽(가생이)이 사라지고, 겹친 덩어리가 서로 스며든다.
//   경계는 몸에 붙은 3D 노이즈로 흔들어 솜처럼 뭉실뭉실한 테두리가 된다(몸을 따라 움직이고 미끄러지지 않는다).
// - 빛: 빛을 감싸 들어가게(wrap) 받아 명암이 부드럽게 넘어가고, 그늘은 옅은 하늘색이다.
// - 깊이를 쓰지 않아 수채화 윤곽선이 그려지지 않고, 그림자도 드리우지 않는다(딱딱한 그림자 가장자리가 생기지 않게).
//   다른 물체의 그림자는 절반 세기로 받는다.
// 수채화 베이크(WatercolorBaker)는 이 셰이더의 렌더러를 건너뛴다(WatercolorBake = Skip 태그).
Shader "ThatGameJam/SoftSnow"
{
    Properties
    {
        _Color ("Snow Colour", Color) = (0.97, 0.98, 1, 1)
        _ShadeColor ("Shade Colour", Color) = (0.72, 0.8, 0.92, 1)
        _Wrap ("Light Wrap (softer shading)", Range(0, 1)) = 0.6
        _Opacity ("Opacity", Range(0, 1)) = 1
        _EdgeStart ("Edge Fade Starts (facing, 0 = side on)", Range(0, 1)) = 0.55
        _EdgeEnd ("Edge Fully Clear", Range(0, 1)) = 0.12
        _FluffScale ("Fluff Cells Per Metre", Float) = 40
        _Fluff ("Fluff Amount", Range(0, 1)) = 0.5
        _Puff ("Puff Out Along Normal (object units)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "WatercolorBake" = "Skip" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _ShadeColor;
            half _Wrap;
            half _Opacity;
            half _EdgeStart;
            half _EdgeEnd;
            float _FluffScale;
            half _Fluff;
            float _Puff;
        CBUFFER_END

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // 0~1 value noise in 3D.
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

        // How opaque the snow is here: clear where the surface turns side on to the camera, the edge shaken by noise stuck to the body.
        half SnowAlpha(float3 positionOS, float3 normalWS, float3 positionWS)
        {
            float3 viewWS = normalize(GetWorldSpaceViewDir(positionWS));
            float fluff = Noise(positionOS * _FluffScale) * 0.65 + Noise(positionOS * _FluffScale * 2.3) * 0.35;
            float facing = saturate(dot(normalize(normalWS), viewWS)) + (fluff - 0.5) * _Fluff * 0.6;
            return smoothstep(_EdgeEnd, _EdgeStart, facing) * _Opacity;
        }
        ENDHLSL

        // First, the solid middle of the snow writes depth alone, so of the clumps overlapping on screen only the nearest is painted
        // and the soft edges lie over it. URP draws this pass (SRPDefaultUnlit) before the lit one.
        Pass
        {
            Name "SnowDepth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
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
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS = input.positionOS.xyz + input.normalOS * _Puff;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Turned and scaled with the body but not moved, so the fluff is a size in metres and sticks to her.
                output.positionOS = mul((float3x3)GetObjectToWorldMatrix(), positionOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(SnowAlpha(input.positionOS, input.normalWS, input.positionWS) - 0.99);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS = input.positionOS.xyz + input.normalOS * _Puff;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Turned and scaled with the body but not moved, so the fluff is a size in metres and sticks to her.
                output.positionOS = mul((float3x3)GetObjectToWorldMatrix(), positionOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                half alpha = SnowAlpha(input.positionOS, normalWS, input.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half wrapped = saturate((dot(normalWS, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
                // Shadows soften too: half of the shade they would give.
                half lit = wrapped * lerp(1.0, mainLight.shadowAttenuation, 0.5);
                half3 colour = lerp(_ShadeColor.rgb, _Color.rgb, lit) * lerp(1.0, mainLight.color, 0.5);
                colour += SampleSH(normalWS) * 0.15;
                return half4(MixFog(colour, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
