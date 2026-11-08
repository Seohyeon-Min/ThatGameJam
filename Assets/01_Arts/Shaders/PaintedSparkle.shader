// A round dot for particles, drawn without a texture, laid over the picture instead of added onto it (EyeSparkle adds): its own
// colour shows over white snow too, where an added yellow only turns white. Particle Color's alpha dims it. Above 1 the colour
// still glows (Bloom). For the easter eggs' sparkle (EggSparkle).
Shader "ThatGameJam/PaintedSparkle"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (2.2, 1.8, 0.15, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.4
        _Cover ("Cover (0 = only added on, 1 = covers what is under it)", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "PaintedSparkle"
            Tags { "LightMode" = "UniversalForward" }
            // Premultiplied: the colour is added, and Cover takes away that much of what is under it first.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EdgeSoftness;
                half _Cover;
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
                float2 uv : TEXCOORD0;      // -1..1 across the particle
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half amount = (1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, length(input.uv))) * input.color.a;
                return half4(_Color.rgb * input.color.rgb * amount, amount * _Cover);
            }
            ENDHLSL
        }
    }
}
