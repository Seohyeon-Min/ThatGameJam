// The gust that clears the snow, made visible: swirling brush strokes spiralling out from the centre with the spring ring's
// front (the _SpringRing global). Drawn on a flat quad that SeasonSnow lays above the ground.
// Each stroke is a crescent along a spiral: pointed at both ends, thickest early on, with torn edges and a dry-brush tail. Thick
// strokes and hair-thin lines mix, at different distances behind the front. All turn as the ring grows and thin out toward
// Fade Radius, so the swirl is gone while the ring calms on.
Shader "ThatGameJam/SnowWisps"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.95)
        _Strokes ("Strokes", Range(1, 24)) = 14
        _Width ("Thickest Stroke (world units)", Float) = 1.1
        _ThinShare ("Thin Line Bias", Range(1, 6)) = 2.5
        _ArcMin ("Shortest Arc (degrees)", Range(10, 360)) = 70
        _ArcMax ("Longest Arc (degrees)", Range(10, 360)) = 230
        _Spiral ("Spiral Opening", Range(0, 1)) = 0.35
        _Swirl ("Turn As The Ring Grows", Float) = 0.05
        _InnerReach ("Innermost Stroke (share of ring radius)", Range(0, 1)) = 0.3
        _Rag ("Torn Edges", Range(0, 1)) = 0.55
        _RagScale ("Tear Size", Float) = 18
        _DryBrush ("Dry Brush Tail", Range(0, 1)) = 0.8
        _Edge ("Edge Softness (world units)", Float) = 0.03
        _FadeRadius ("Gone By Ring Radius", Float) = 34
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/01_Arts/Shaders/SunPool.hlsl"

            float4 _SpringRing; // centre (xyz) and radius (w) while the ring plays

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Strokes;
                float _Width;
                float _ThinShare;
                float _ArcMin;
                float _ArcMax;
                float _Spiral;
                float _Swirl;
                float _InnerReach;
                float _Rag;
                float _RagScale;
                float _DryBrush;
                float _Edge;
                float _FadeRadius;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // How much of stroke i covers this point (r from the centre, at this angle), when the ring has this radius.
            float Stroke(float i, float r, float angle, float radius)
            {
                float arc = lerp(_ArcMin, _ArcMax, SunPoolHash(float2(i, 2.3))) * PI / 180.0;
                float start = SunPoolHash(float2(i, 7.9)) * TWO_PI + radius * _Swirl * TWO_PI * (0.7 + 0.6 * SunPoolHash(float2(i, 3.1)));
                float around = angle - start;
                around -= TWO_PI * floor(around / TWO_PI);
                float u = around / arc;
                if (u >= 1.0)
                    return 0.0;

                // The spiral: this stroke's distance from the centre opens up along its length.
                float reach = lerp(_InnerReach, 1.1, SunPoolHash(float2(i, 11.7)));
                float line01 = radius * reach * (1.0 + _Spiral * (u - 0.5));

                // Crescent: pointed at both ends, thickest early. Most strokes thin, a few thick.
                float width = _Width * (0.05 + pow(SunPoolHash(float2(i, 17.3)), _ThinShare)) * (0.3 + radius * 0.08);
                float halfWidth = width * pow(sin(PI * pow(u, 0.7)), 1.3);

                // Torn edges, different on the inside and the outside.
                float offset = r - line01;
                float side = offset < 0.0 ? 31.0 : 53.0;
                float tear = SunPoolNoise(float2(u * _RagScale, i * 7.1 + side));
                halfWidth *= 1.0 - _Rag * tear;

                float cover = 1.0 - smoothstep(max(halfWidth - _Edge, 0.0), halfWidth + _Edge, abs(offset));

                // Dry brush: toward the tail the stroke breaks into streaks along its length.
                float streaks = SunPoolNoise(float2(u * 3.0 + i, offset / max(width, 1e-3) * 5.0 + i * 3.7));
                cover *= 1.0 - _DryBrush * smoothstep(0.55, 1.0, u) * smoothstep(0.3, 0.6, streaks);

                return cover * lerp(0.55, 1.0, SunPoolHash(float2(i, 23.9)));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float radius = _SpringRing.w;
                half overall = 1.0 - smoothstep(_FadeRadius * 0.65, _FadeRadius, radius);
                if (radius <= 0.0 || overall <= 0.0)
                    return 0;

                float2 offset = input.positionWS.xz - _SpringRing.xz;
                float r = length(offset);
                // Nothing is drawn past the outermost stroke's widest reach.
                if (r > radius * 1.1 * (1.0 + _Spiral * 0.5) + _Width * (0.35 + radius * 0.08))
                    return 0;
                float angle = atan2(offset.y, offset.x);

                float cover = 0.0;
                int strokes = (int)_Strokes;
                for (int i = 0; i < strokes; i++)
                    cover = max(cover, Stroke(i + 1.0, r, angle, radius));

                half coverage = saturate(cover * overall * _Color.a);
                return half4(_Color.rgb * coverage, coverage);
            }
            ENDHLSL
        }
    }
}
