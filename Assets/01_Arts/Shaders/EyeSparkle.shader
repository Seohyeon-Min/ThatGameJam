// A round dot for particles, drawn without a texture, added onto the picture.
// Particle Color's alpha dims it. Drawn over everything, since it sits right on the face and a quick flash shouldn't be cut by it.
Shader "ThatGameJam/EyeSparkle"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.6, 1.45, 1.1, 1)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "EyeSparkle"
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
                half brightness = (1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, length(input.uv))) * input.color.a;
                return half4(_Color.rgb * input.color.rgb * brightness, 0);
            }
            ENDHLSL
        }
    }
}
