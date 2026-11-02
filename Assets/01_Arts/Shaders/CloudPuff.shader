// One puff of a painted cloud: a Quad that always faces the camera from its own centre, shaped into a lumpy ball. A cloud is a prefab
// of many of these at different heights and sizes, so the bird can fly between them and they pass by one at a time.
// The middle is opaque and the rim thins out. The tone comes from a ball's normal and the main light: cream, a middle tone and a lavender shade.
// Each puff differs by its position. Puffs near the camera fade away, so the view never fills with white while going through a cloud.
// Clouds are dark away from a point on the map (the baby) and clear up toward it, so the sky opens on the way there.
// Don't mark puffs Static: static batching bakes them into world space and the camera-facing turn breaks.
Shader "ThatGameJam/CloudPuff"
{
    Properties
    {
        [Header(Tone)]
        _LightColor ("Light", Color) = (1, 0.98, 0.92, 1)
        _MidColor ("Middle", Color) = (0.93, 0.92, 0.96, 1)
        _ShadeColor ("Shade", Color) = (0.76, 0.76, 0.9, 1)
        _LightStep ("Light Starts At", Range(0, 1)) = 0.62
        _ShadeStep ("Shade Ends At", Range(0, 1)) = 0.36
        _ToneSoftness ("Tone Edge Softness", Range(0.005, 0.3)) = 0.05
        _ToneNoise ("Tone Edge Roughness", Range(0, 1)) = 0.35

        [Header(Shape)]
        _LumpScale ("Lump Scale", Float) = 2.2
        _Lumpiness ("Lumpiness", Range(0, 2)) = 0.8
        _EdgePosition ("Edge Position", Range(0, 0.8)) = 0.22
        _EdgeSoftness ("Edge Softness", Range(0.005, 0.5)) = 0.1
        _RimAlpha ("Rim Opacity", Range(0, 1)) = 0.55
        _CoreWidth ("Rim Width", Range(0.01, 1)) = 0.25
        _BillowSpeed ("Billow Speed", Float) = 0.08

        [Header(Near The Camera)]
        _NearFadeStart ("Gone Within", Float) = 3
        _NearFadeEnd ("Full From", Float) = 9

        [Header(Dark Away From A Point)]
        _DarkColor ("Dark Colour", Color) = (0.36, 0.38, 0.52, 1)
        _DarkCentre ("Clear Centre (X and Z)", Vector) = (0, 0, 0, 0)
        _DarkRadius ("Fully Dark From", Float) = 50
        _DarkStrength ("Dark Strength", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "CloudPuff"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/01_Arts/Shaders/SunPool.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _MidColor;
                half4 _ShadeColor;
                half _LightStep;
                half _ShadeStep;
                half _ToneSoftness;
                half _ToneNoise;
                float _LumpScale;
                half _Lumpiness;
                half _EdgePosition;
                half _EdgeSoftness;
                half _RimAlpha;
                half _CoreWidth;
                float _BillowSpeed;
                float _NearFadeStart;
                float _NearFadeEnd;
                half4 _DarkColor;
                float4 _DarkCentre;
                float _DarkRadius;
                half _DarkStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;          // -1..1 across the puff
                float3 right : TEXCOORD1;
                float3 up : TEXCOORD2;
                float3 toCamera : TEXCOORD3;
                float seed : TEXCOORD4;
                float nearFade : TEXCOORD5;
                float darkness : TEXCOORD6;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                output.right = UNITY_MATRIX_V[0].xyz;
                output.up = UNITY_MATRIX_V[1].xyz;
                output.toCamera = UNITY_MATRIX_V[2].xyz;
                float3 positionWS = originWS + output.right * input.positionOS.x * scaleX + output.up * input.positionOS.y * scaleY;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * 2.0 - 1.0;
                output.seed = SunPoolHash(originWS.xz + originWS.y) * 100.0;
                float distanceToCamera = length(originWS - GetCameraPositionWS());
                output.nearFade = saturate((distanceToCamera - _NearFadeStart) / max(_NearFadeEnd - _NearFadeStart, 1e-3));
                // Across the ground only, since clouds are far above the point. From the puff's centre, so a puff is one tone.
                float farness = saturate(length(originWS.xz - _DarkCentre.xz) / max(_DarkRadius, 1e-3));
                output.darkness = farness * farness * (3.0 - 2.0 * farness) * _DarkStrength;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv;
                float r = length(p);

                // Each layer of lumps drifts its own way, so they churn in place instead of the whole pattern sliding across.
                float2 q = p * _LumpScale + input.seed;
                float t = _Time.y * _BillowSpeed;
                float lumps = SunPoolNoise(q + float2(t, -t * 0.6)) * 0.55
                    + SunPoolNoise(q * 2.1 + 7.3 + float2(-t * 1.3, t * 0.8)) * 0.3
                    + SunPoolNoise(q * 4.3 + 13.1 + float2(t * 1.7, t * 1.2)) * 0.15;

                // Lumpy outline, a thin rim and an opaque middle. Always gone by the Quad's edge.
                float field = 1.0 - r + (lumps - 0.5) * _Lumpiness;
                half shape = smoothstep(_EdgePosition, _EdgePosition + _EdgeSoftness, field);
                half core = smoothstep(_EdgePosition + _EdgeSoftness, _EdgePosition + _EdgeSoftness + _CoreWidth, field);
                half alpha = shape * lerp(_RimAlpha, 1.0, core) * saturate((1.0 - r) * 6.0) * input.nearFade;
                clip(alpha - 0.002);

                // Lit like a ball, in painted steps.
                float z = sqrt(saturate(1.0 - r * r));
                float3 normalWS = normalize(input.right * p.x + input.up * p.y + input.toCamera * z);
                half lit = dot(normalWS, GetMainLight().direction) * 0.5 + 0.5 + (lumps - 0.5) * _ToneNoise;
                half3 color = lerp(_ShadeColor.rgb, _MidColor.rgb, smoothstep(_ShadeStep - _ToneSoftness, _ShadeStep + _ToneSoftness, lit));
                color = lerp(color, _LightColor.rgb, smoothstep(_LightStep - _ToneSoftness, _LightStep + _ToneSoftness, lit));
                color = lerp(color, _DarkColor.rgb, input.darkness);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
