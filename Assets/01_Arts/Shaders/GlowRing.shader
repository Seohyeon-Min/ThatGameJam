// A soft glowing ring for particles, drawn without a texture, added onto the picture.
// Pairs with a particle's own Size/Color over Lifetime: as the quad grows the ring spreads outward and widens,
// while Color over Lifetime's alpha fades it out, giving a Sky-style expanding call pulse for free (no script).
Shader "ThatGameJam/GlowRing"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.7, 1.4, 0.9, 1)
        _InnerRadius ("Inner Radius", Range(0, 0.95)) = 0.65
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "GlowRing"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _InnerRadius;
                half _EdgeSoftness;
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
                half dist = length(input.uv);
                half outer = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, dist);
                half inner = smoothstep(_InnerRadius - _EdgeSoftness, _InnerRadius, dist);
                half ring = outer * inner * input.color.a;
                return half4(_Color.rgb * input.color.rgb * ring, 0);
            }
            ENDHLSL
        }
    }
}
