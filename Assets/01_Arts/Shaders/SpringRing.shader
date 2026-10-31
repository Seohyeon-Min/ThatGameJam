// Spring ring (test): a glowing ring lying on the ground, growing from the object's position, with soft glowing orbs
// sitting along it. SpringRing builds the mesh by dropping each point straight down onto the ground, so both follow it.
// Band: uv.y goes across the ring (0 inside edge, 1 outside edge). Orbs: camera-facing quads; uv2.xy is the corner
// (-1..1), uv2.z is 1 for an orb, uv2.w its size scale. Rise: a soft glow rising from the ring's middle line, swaying
// slowly; uv2.z is 2, uv.y 0 at the foot and 1 at the top. Vertex alpha is 0 where a ray found no ground.
// Drag Progress (0 = centre, 1 = Max Radius) to see any moment, in Edit Mode too.
// Blend: the output is premultiplied, so Src One with Dst OneMinusSrcAlpha paints the colour over the scene (normal),
// One + OneMinusSrcColor brightens softly and never past white (screen), One + One adds (glow, harsh on bright ground).
Shader "ThatGameJam/SpringRing"
{
    Properties
    {
        _Progress ("Progress", Range(0, 1)) = 0.3
        [Enum(UnityEngine.Rendering.BlendMode)] _RingSrcBlend ("Blend Src (One)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _RingDstBlend ("Blend Dst (OneMinusSrcAlpha = normal, OneMinusSrcColor = screen, One = add)", Float) = 10
        _MaxRadius ("Max Radius", Float) = 40
        _Lift ("Lift Above Ground", Float) = 0.03
        _Duration ("Duration (seconds, 0 key)", Float) = 10
        [Header(Band)]
        [HDR] _Color ("Band Color", Color) = (1, 0.95, 0.75, 1)
        _BandIntensity ("Band Intensity", Range(0, 2)) = 0.5
        _Width ("Width", Float) = 0.8
        _Softness ("Edge Softness", Range(0.2, 6)) = 1.5
        [Header(Orbs)]
        [HDR] _OrbColor ("Orb Color", Color) = (1, 0.9, 0.95, 1)
        _OrbIntensity ("Orb Intensity", Range(0, 2)) = 0.6
        _OrbCount ("Orb Count", Range(8, 1024)) = 300
        _OrbSize ("Orb Size", Float) = 0.9
        _OrbSizeJitter ("Orb Size Jitter", Range(0, 1)) = 0.4
        _OrbSoftness ("Orb Softness", Range(0.3, 4)) = 1.6
        _OrbCore ("Orb Core Brightness", Range(0, 3)) = 0.8
        [Header(Rise)]
        [HDR] _RiseColor ("Rise Color", Color) = (1, 0.95, 0.8, 1)
        _RiseIntensity ("Rise Intensity", Range(0, 2)) = 0.4
        _RiseHeight ("Rise Height", Float) = 1.2
        _RiseFalloff ("Rise Upward Falloff", Range(0.2, 6)) = 2.2
        _RiseWobble ("Rise Height Wobble", Range(0, 1)) = 0.3
        _RiseShimmer ("Rise Shimmer", Range(0, 1)) = 0.3
        _RiseSpeed ("Rise Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend [_RingSrcBlend] [_RingDstBlend]
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Progress;
                half4 _Color;
                float _MaxRadius;
                float _Lift;
                float _Duration;
                half _BandIntensity;
                float _Width;
                half _Softness;
                half4 _OrbColor;
                half _OrbIntensity;
                float _OrbCount;
                float _OrbSize;
                half _OrbSizeJitter;
                half _OrbSoftness;
                half _OrbCore;
                half4 _RiseColor;
                half _RiseIntensity;
                float _RiseHeight;
                half _RiseFalloff;
                half _RiseWobble;
                half _RiseShimmer;
                float _RiseSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 uv2 : TEXCOORD1; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 orb : TEXCOORD1; half alpha : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (input.uv2.z > 1.5)
                {
                    // Rise: lift the top row; its height rises and falls slowly along the ring.
                    float angle = input.uv.x * TWO_PI;
                    float time = _Time.y * _RiseSpeed;
                    float wave = sin(angle * 5.0 + time * 0.6) * 0.6 + sin(angle * 11.0 - time * 1.1) * 0.4;
                    positionWS.y += input.uv.y * _RiseHeight * (1.0 + _RiseWobble * wave);
                }
                else if (input.uv2.z > 0.5)
                {
                    // Orb: spread the quad's corners around its centre, facing the camera.
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    float halfSize = _OrbSize * 0.5 * input.uv2.w;
                    positionWS += (right * input.uv2.x + up * input.uv2.y) * halfSize;
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.orb = input.uv2.xyz;
                output.alpha = input.color.a;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half glow;
                half3 color;
                if (input.orb.z > 1.5)
                {
                    // Rise: brightest at the foot, fading upward, with soft streaks drifting up.
                    float angle = input.uv.x * TWO_PI;
                    half y = input.uv.y;
                    half profile = pow(1.0 - y, _RiseFalloff);
                    half streak = 0.5 + 0.5 * sin(angle * 23.0 + y * 5.0 - _Time.y * _RiseSpeed * 2.0);
                    glow = profile * (1.0 - _RiseShimmer + _RiseShimmer * streak) * _RiseIntensity;
                    color = _RiseColor.rgb;
                }
                else if (input.orb.z > 0.5)
                {
                    // Soft round glow, a little brighter in the middle so it reads as a puffy ball.
                    half d = saturate(length(input.orb.xy));
                    half body = pow(1.0 - d * d, _OrbSoftness);
                    glow = body * (1.0 + _OrbCore * body * body) * _OrbIntensity;
                    color = _OrbColor.rgb;
                }
                else
                {
                    // Band: brightest along the middle of the ring, soft toward both edges.
                    glow = pow(saturate(1.0 - abs(input.uv.y * 2.0 - 1.0)), _Softness) * _BandIntensity;
                    color = _Color.rgb;
                }
                // Premultiplied: coverage in alpha, colour already scaled by it.
                half coverage = saturate(glow * input.alpha);
                return half4(color * coverage, coverage);
            }
            ENDHLSL
        }
    }
}
