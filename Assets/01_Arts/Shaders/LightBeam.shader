// Light beam marking the baby's destination: one column of light falling from above the clouds (off screen) to the ground.
// LightBeam builds the mesh: a ribbon whose x is the offset across the beam (world units, already tapered) and whose y is
// the height above the foot. The shader turns the ribbon around the object's up axis so it always faces the camera,
// so the column looks round from any side. Painted look: wobbling soft edges, faint vertical streaks drifting down,
// tones grouped into a few washes, and a slightly warmer pigment band just inside the edge, like a dried watercolour rim.
// Blend: the output is premultiplied, so Src One with Dst OneMinusSrcAlpha paints the colour over the scene (normal),
// One + OneMinusSrcColor brightens softly and never past white (screen), One + One adds (glow, harsh on bright ground).
// Normal is the default: on near-white snow, screen and add barely show, while a warm cream wash still reads.
// Don't mark the object Static: static batching bakes the ribbon into world space and the camera-facing turn breaks.
Shader "ThatGameJam/LightBeam"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Blend Src (One)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Blend Dst (OneMinusSrcAlpha = normal, OneMinusSrcColor = screen, One = add)", Float) = 10
        [HDR] _Color ("Color", Color) = (1, 0.95, 0.78, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.7
        _CoreBoost ("Core Brightness", Range(0, 1)) = 0.4
        [Header(Edges)]
        _EdgeSoftness ("Edge Softness", Range(0.02, 1)) = 0.45
        _EdgeWobble ("Edge Wobble", Range(0, 0.5)) = 0.12
        _WobbleScale ("Wobble Scale (per unit height)", Float) = 0.12
        [HDR] _RimColor ("Pigment Band Color", Color) = (1, 0.86, 0.62, 1)
        _RimStrength ("Pigment Band Strength", Range(0, 1)) = 0.35
        [Header(Streaks)]
        _Streaks ("Streak Strength", Range(0, 1)) = 0.35
        _StreakCount ("Streak Count", Float) = 7
        _StreakSpeed ("Streak Fall Speed", Float) = 0.6
        [Header(Washes)]
        _Steps ("Wash Steps (0 = smooth)", Range(0, 8)) = 4
        _StepSoftness ("Wash Step Softness", Range(0.05, 1)) = 0.35
        [Header(Ground And Life)]
        _GroundFade ("Ground Fade Height", Float) = 1.5
        _GroundAlpha ("Alpha At Ground", Range(0, 1)) = 0.5
        _Pulse ("Pulse", Range(0, 0.5)) = 0.08
        _PulseSpeed ("Pulse Speed", Float) = 0.25
        [Header(Reveal)]
        _Reveal ("Reveal (0 hidden, 1 reached the ground)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _CoreBoost;
                half _EdgeSoftness;
                half _EdgeWobble;
                float _WobbleScale;
                half4 _RimColor;
                half _RimStrength;
                half _Streaks;
                float _StreakCount;
                float _StreakSpeed;
                half _Steps;
                half _StepSoftness;
                float _GroundFade;
                half _GroundAlpha;
                half _Pulse;
                float _PulseSpeed;
                half _Reveal;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;      // x: -1..1 across the beam, y: 0 foot .. 1 top
                float height : TEXCOORD1;   // world units above the foot
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                // Point on the beam's axis at this height, then step sideways across the view.
                float3 axisPoint = TransformObjectToWorld(float3(0, input.positionOS.y, 0));
                float3 axis = normalize(mul((float3x3)GetObjectToWorldMatrix(), float3(0, 1, 0)));
                float3 toCamera = GetCameraPositionWS() - axisPoint;
                float3 side = cross(axis, toCamera);
                side = dot(side, side) > 1e-6 ? normalize(side) : UNITY_MATRIX_V[0].xyz;
                float3 positionWS = axisPoint + side * input.positionOS.x;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = float2(input.uv.x * 2.0 - 1.0, input.uv.y);
                output.height = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float x = input.uv.x;
                float h = input.height;

                // Each edge wobbles on its own, slowly, like a wet boundary that never quite settles.
                float sideSeed = x < 0.0 ? 3.7 : 11.3;
                float wobble = (ValueNoise(float2(h * _WobbleScale - time * 0.05, sideSeed)) * 2.0 - 1.0) * _EdgeWobble;
                float d = abs(x) / (1.0 + wobble);

                // Body: full in the middle, soft toward the edges, a little brighter along the core.
                half body = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, d);
                body *= 1.0 + _CoreBoost * (1.0 - d * d);

                // Faint vertical streaks drifting down through the column.
                float streak = ValueNoise(float2(x * _StreakCount, h * 0.08 + time * _StreakSpeed * 0.08));
                body *= 1.0 - _Streaks * streak;

                // Group the tone into a few washes, each with a soft painted edge.
                if (_Steps > 0.5)
                {
                    float v = saturate(body) * _Steps;
                    float stepEdge = smoothstep(0.5 - _StepSoftness * 0.5, 0.5 + _StepSoftness * 0.5, frac(v));
                    body = (floor(v) + stepEdge) / _Steps;
                }

                // Warmer pigment band just inside the edge, where a watercolour wash dries darker.
                half band = smoothstep(1.0 - _EdgeSoftness - 0.2, 1.0 - _EdgeSoftness, d) * (1.0 - smoothstep(1.0 - _EdgeSoftness * 0.5, 1.0, d));
                half3 color = lerp(_Color.rgb, _RimColor.rgb, saturate(band * _RimStrength * 2.0));
                body += band * _RimStrength * 0.3;

                // Settles softly onto the ground instead of a hard cut, and breathes slowly.
                half ground = lerp(_GroundAlpha, 1.0, saturate(h / max(_GroundFade, 1e-3)));
                half pulse = 1.0 + _Pulse * sin(time * _PulseSpeed * TWO_PI);

                // Comes down from the top: everything above the front shows, with a soft leading edge.
                const float revealEdge = 0.15;
                half reveal = saturate((input.uv.y - 1.0 + _Reveal * (1.0 + revealEdge)) / revealEdge);

                half coverage = saturate(body * ground * pulse * reveal * _Intensity * _Color.a);
                return half4(color * coverage, coverage);
            }
            ENDHLSL
        }
    }
}
