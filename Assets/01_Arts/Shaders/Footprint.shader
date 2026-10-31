// 눈 위 발자국(FootprintSystem 의 파티클). 눈 색에 곱하기로 얹어 눈의 빛, 그림자, 헤이즈를 그대로 물려받는다.
// 모양은 앞꿈치와 뒤꿈치 두 타원을 합친 것이고, 가장자리는 월드에 고정된 노이즈로 거칠어져 발자국마다 다르게 생긴다.
// 바깥쪽 테두리는 눈이 밀려 올라온 것처럼 살짝 밝다.
Shader "ThatGameJam/Footprint"
{
    Properties
    {
        _Tint ("Print Colour (multiplied onto the snow)", Color) = (0.62, 0.66, 0.9, 1)
        _Strength ("Strength", Range(0, 1)) = 0.8
        _EdgeRough ("Edge Roughness", Range(0, 1)) = 0.4
        _EdgeSize ("Edge Roughness Size (m)", Range(0.005, 0.1)) = 0.025
        _Grain ("Grain Inside", Range(0, 1)) = 0.25
        _RimLight ("Pushed-Up Snow Rim", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend DstColor Zero
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "Footprint"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Strength;
                half _EdgeRough;
                half _EdgeSize;
                half _Grain;
                half _RimLight;
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
                float3 positionWS : TEXCOORD1;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                // 앞꿈치와 뒤꿈치. 0 보다 작으면 발자국 안이다.
                float front = length((p - float2(0.0, 0.34)) / float2(0.72, 0.5));
                float heel = length((p - float2(0.0, -0.46)) / float2(0.55, 0.38));
                float shape = min(front, heel) - 1.0;

                float2 world = input.positionWS.xz / max(_EdgeSize, 1e-3);
                float rough = (Noise(world) - 0.5) * _EdgeRough;
                float inside = 1.0 - smoothstep(-0.06, 0.04, shape + rough);
                float rim = smoothstep(0.0, 0.08, shape + rough) * (1.0 - smoothstep(0.08, 0.3, shape + rough));

                float grain = 1.0 - _Grain * Noise(world * 2.3);
                half fade = input.color.a;
                half3 color = lerp(half3(1, 1, 1), _Tint.rgb, inside * grain * _Strength * fade);
                color += rim * _RimLight * fade;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
