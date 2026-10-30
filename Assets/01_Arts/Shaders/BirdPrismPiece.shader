// 새 주위로 흩날리는 조각과 가루(파티클). 색은 파티클 색에서 온다. 조각은 끝이 뾰족한 잎 모양으로 거칠게 칠하고 가운데가 희다.
// Petal 을 0 으로 두면 둥근 가루가 된다.
Shader "ThatGameJam/BirdPrismPiece"
{
    Properties
    {
        _Petal ("Petal Shape (0 = round dust)", Range(0, 1)) = 1
        _Glow ("Glow", Range(0, 8)) = 1.2
        _Whiten ("Whiten Toward The Centre", Range(0, 1)) = 0.35
        _Rough ("Edge Roughness", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Piece"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BirdPrism.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Petal;
                half _Glow;
                half _Whiten;
                half _Rough;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float inside;
                half mask;
                if (_Petal > 0.5)
                {
                    // 잎 모양: 위아래 끝으로 갈수록 좁아지고, 가장자리는 거칠다.
                    float width = max((1.0 - p.y * p.y) * 0.55, 1e-3);
                    inside = 1.0 - max(abs(p.x) / width, abs(p.y));
                    mask = step(0.0, inside + (PrismNoise(input.uv * 9.0) - 0.5) * _Rough);
                }
                else
                {
                    inside = 1.0 - length(p);
                    mask = smoothstep(0.0, 0.5, inside);
                }
                half3 color = lerp(input.color.rgb, 1.0, _Whiten * saturate(inside * 2.0)) * _Glow;
                return half4(color, mask * input.color.a);
            }
            ENDHLSL
        }
    }
}
