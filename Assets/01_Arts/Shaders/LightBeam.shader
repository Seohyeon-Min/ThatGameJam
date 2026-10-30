// Light marking the baby's destination, like sun through a gap in the clouds (komorebi): a faint slanted shaft and, strongest
// of all, a pool of light painted only on the ground. LightBeam builds one mesh holding both.
// Shaft (uv2.z = 0): position is the centre of a row on the axis, normal is the axis, uv2.x the signed half width and uv2.y
// the distance along the axis. The shader spreads each row across the view, so the shaft looks round from any side.
// Soft wobbling edges, faint streaks drifting down, optional grouped washes, and a warmer band just inside the edge.
// Pool (uv2.z = 1): a grid already lying on the ground. uv2.xy is -1..1 across it, uv2.w is 0 where a ray found no ground.
// Roughly round with lumpy, cloud-like painted edges and a few loose patches, drifting slowly as if under moving clouds.
// Blend: the output is premultiplied, so Src One with Dst OneMinusSrcAlpha paints the colour over the scene (normal),
// One + OneMinusSrcColor brightens softly and never past white (screen), One + One adds (glow, harsh on bright ground).
// Normal is the default: on near-white snow, screen and add barely show, while a warm cream wash still reads.
// Don't mark the object Static: static batching bakes the mesh into world space and the camera-facing turn breaks.
Shader "ThatGameJam/LightBeam"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Blend Src (One)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Blend Dst (OneMinusSrcAlpha = normal, OneMinusSrcColor = screen, One = add)", Float) = 10
        [Header(Ground Pool)]
        [HDR] _PoolColor ("Pool Color", Color) = (1, 0.97, 0.82, 1)
        [HDR] _PoolRimColor ("Pool Edge Color", Color) = (1, 0.88, 0.62, 1)
        _PoolIntensity ("Pool Intensity", Range(0, 1)) = 0.95
        _PoolBlobSize ("Lump Size (world units)", Float) = 0.9
        _PoolRagged ("Lumpiness", Range(0, 2)) = 1.2
        _PoolThreshold ("Pool Shrink", Range(0, 0.9)) = 0.28
        _PoolEdge ("Pool Edge Softness", Range(0.005, 0.3)) = 0.03
        _PoolHalo ("Soft Glow Around", Range(0, 1)) = 0.25
        _PoolDrift ("Drift Speed", Float) = 0.04
        [Header(Shaft)]
        [HDR] _Color ("Shaft Color", Color) = (1, 0.95, 0.78, 1)
        _Intensity ("Shaft Intensity", Range(0, 2)) = 0.18
        _CoreBoost ("Core Brightness", Range(0, 1)) = 0.4
        _EdgeSoftness ("Edge Softness", Range(0.02, 1)) = 0.8
        _EdgeWobble ("Edge Wobble", Range(0, 0.5)) = 0.12
        _WobbleScale ("Wobble Scale (per unit along the shaft)", Float) = 0.12
        [HDR] _RimColor ("Pigment Band Color", Color) = (1, 0.86, 0.62, 1)
        _RimStrength ("Pigment Band Strength", Range(0, 1)) = 0.35
        _Streaks ("Streak Strength", Range(0, 1)) = 0.6
        _StreakCount ("Streak Count", Float) = 7
        _StreakSpeed ("Streak Fall Speed", Float) = 0.6
        _Steps ("Wash Steps (0 = smooth)", Range(0, 8)) = 0
        _StepSoftness ("Wash Step Softness", Range(0.05, 1)) = 0.35
        _GroundFade ("Ground Fade Height", Float) = 1.5
        _GroundAlpha ("Alpha At Ground", Range(0, 1)) = 0.5
        [Header(Life)]
        _Pulse ("Pulse", Range(0, 0.5)) = 0.08
        _PulseSpeed ("Pulse Speed", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/01_Arts/Shaders/SunPool.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PoolColor;
                half4 _PoolRimColor;
                half _PoolIntensity;
                float _PoolBlobSize;
                half _PoolRagged;
                half _PoolThreshold;
                half _PoolEdge;
                half _PoolHalo;
                float _PoolDrift;
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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;      // shaft: x -1..1 across it, y 0 foot .. 1 top
                float4 extra : TEXCOORD1;   // uv2 as it came
                float2 worldXZ : TEXCOORD2; // pool: anchors the lumps to the world
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.extra = input.uv2;
                output.worldXZ = positionWS.xz;
                output.uv = float2(input.uv.x * 2.0 - 1.0, input.uv.y);
                if (input.uv2.z < 0.5)
                {
                    // Shaft: step sideways from the axis, across the view.
                    float3 axis = normalize(TransformObjectToWorldDir(input.normalOS));
                    float3 side = cross(axis, GetCameraPositionWS() - positionWS);
                    side = dot(side, side) > 1e-6 ? normalize(side) : UNITY_MATRIX_V[0].xyz;
                    positionWS += side * input.uv2.x;
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Pool(Varyings input, half pulse)
            {
                float r = length(input.extra.xy);
                float field = SunPoolField(input.worldXZ, r, _PoolBlobSize, _PoolRagged, _PoolDrift);
                half lit = smoothstep(_PoolThreshold - _PoolEdge, _PoolThreshold + _PoolEdge, field);
                half halo = smoothstep(_PoolThreshold - 0.3, _PoolThreshold, field) * _PoolHalo;
                half inner = smoothstep(_PoolThreshold, _PoolThreshold + 0.35, field);
                half3 color = lerp(_PoolRimColor.rgb, _PoolColor.rgb, inner);
                // Wherever no ground was found, it's gone too.
                half fade = SunPoolRimFade(r) * input.extra.w;
                half coverage = saturate(max(lit * _PoolIntensity * pulse, halo) * fade);
                return half4(color * coverage, coverage);
            }

            half4 Shaft(Varyings input, half pulse)
            {
                float time = _Time.y;
                float x = input.uv.x;
                float h = input.extra.y;

                // Each edge wobbles on its own, slowly, like a wet boundary that never quite settles.
                float sideSeed = x < 0.0 ? 3.7 : 11.3;
                float wobble = (SunPoolNoise(float2(h * _WobbleScale - time * 0.05, sideSeed)) * 2.0 - 1.0) * _EdgeWobble;
                float d = abs(x) / (1.0 + wobble);

                // Body: full in the middle, soft toward the edges, a little brighter along the core.
                half body = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, d);
                body *= 1.0 + _CoreBoost * (1.0 - d * d);

                // Faint streaks drifting down through the column.
                float streak = SunPoolNoise(float2(x * _StreakCount, h * 0.08 + time * _StreakSpeed * 0.08));
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

                // Settles softly onto the ground instead of a hard cut.
                half ground = lerp(_GroundAlpha, 1.0, saturate(h / max(_GroundFade, 1e-3)));

                half coverage = saturate(body * ground * pulse * _Intensity * _Color.a);
                return half4(color * coverage, coverage);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed * TWO_PI);
                return input.extra.z > 0.5 ? Pool(input, pulse) : Shaft(input, pulse);
            }
            ENDHLSL
        }
    }
}
