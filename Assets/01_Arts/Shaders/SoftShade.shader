// 부드러운 그늘 한 점: 쿼드 가운데가 가장 진하고 가장자리로 갈수록 투명해지는 둥근 그늘(텍스처 없이 UV로 계산).
// 밝은 배경에 묻히는 새 뒤에 깔아 새가 드러나게 한다(BirdShade). 땅에 잘리지 않게 깊이 검사 없이 그리고, 새와 캐릭터는
// 스텐실로 빼서 그 위로는 덮지 않는다.
// 수채화 베이크는 건너뛴다(WatercolorBake = Skip).
Shader "ThatGameJam/SoftShade"
{
    Properties
    {
        _Color ("Shade Colour (alpha = darkest)", Color) = (0.25, 0.2, 0.35, 0.35)
        _Softness ("Softness (1 = fades from the middle, 0 = hard edge)", Range(0.01, 1)) = 0.8
        _Fade ("Fade (set by BirdShade)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "WatercolorBake" = "Skip" }

        Pass
        {
            Name "Shade"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Never cut off by the ground or anything else it reaches into; instead it leaves out only the bird (BirdPrism's
            // stencil bit, 64) and the characters (the watercolour's Character bit, 128), which stay on top of it.
            ZTest Always
            Stencil { Ref 0 ReadMask 192 Comp Equal }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 0 in the middle, 1 at the circle's edge.
                float r = length(input.uv - 0.5) * 2.0;
                half a = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                return half4(_Color.rgb, _Color.a * a * a * _Fade);
            }
            ENDHLSL
        }
    }
}
