// A round sparkle for particles over a soft patch of shade: the shade, wider than the sparkle, dims what is under it toward
// Shade Colour, and the bright dot is added on top of it. Over white snow an added sparkle alone turns white and is lost (as
// EyeSparkle's does); the shade around it gives it a darker ground to show against. Particle Color's alpha dims both.
// Drawn over everything, like EyeSparkle. For the hint on things the bird can be clicked onto (InteractiveSparkle).
Shader "ThatGameJam/ShadedSparkle"
{
    Properties
    {
        [HDR] _Color ("Sparkle Colour", Color) = (3.4, 2.9, 0.25, 1)
        _CoreSize ("Sparkle Size (share of the particle; the rest is shade)", Range(0.1, 1)) = 0.4
        _EdgeSoftness ("Sparkle Edge Softness", Range(0.01, 1)) = 0.4
        _ShadeColor ("Shade Colour (a: how far toward it)", Color) = (0.06, 0.14, 0.45, 0.9)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ShadedSparkle"
            Tags { "LightMode" = "UniversalForward" }
            // Premultiplied: what is under it goes toward the shade by the alpha, then the sparkle is added.
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
                half _CoreSize;
                half _EdgeSoftness;
                half4 _ShadeColor;
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
                half r = length(input.uv);
                half fade = input.color.a;
                // The shade: a soft blur (a bell curve) from the middle out to the particle's edge, where it reaches nothing, so no
                // rim shows.
                const half falloff = 1.5;
                half bell = (exp(-falloff * r * r) - exp(-falloff)) / (1.0 - exp(-falloff));
                half shade = saturate(bell) * _ShadeColor.a * fade;
                half core = (1.0 - smoothstep(_CoreSize * (1.0 - _EdgeSoftness), _CoreSize, r)) * fade;
                half3 colour = _ShadeColor.rgb * shade + _Color.rgb * input.color.rgb * core;
                return half4(colour, shade);
            }
            ENDHLSL
        }
    }
}
