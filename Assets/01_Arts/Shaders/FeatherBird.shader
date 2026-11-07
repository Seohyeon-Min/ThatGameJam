// A wild bird's plumage, painted from the model's own coordinates (the bird model has no usable UVs). On the wings: long flight
// feathers side by side out along the span, darker toward their tips at the trailing edge, pale coverts in rows near the leading
// edge, and the wing tips darkening. A fan of feathers on the tail, small rounded ones down the back, a paler underside. Each
// feather a little lighter or darker than the next, with a thin dark line where they meet. Matte: no shine and no glow, so it does
// not look like the player's prism bird. Bold on purpose: the flock is seen small, from far off.
// The flight feathers are laid out by distance from the body's long axis, so they stay on the wing while it beats.
// For the migrating birds (MigratingBirds easter egg). Model space: x across the wings, y along the body (head +y), z thickness.
Shader "ThatGameJam/FeatherBird"
{
    Properties
    {
        _BaseColor ("Back", Color) = (0.55, 0.48, 0.40, 1)
        _BellyColor ("Underside", Color) = (0.80, 0.76, 0.70, 1)
        _TipColor ("Wing Tips", Color) = (0.22, 0.20, 0.19, 1)
        _TipFrom ("Tips Darken From (share of the half span)", Range(0, 1)) = 0.55
        _TipTo ("Tips Fully Dark At", Range(0, 1.3)) = 0.95
        _ShadeColor ("Shade (multiplies the side turned from the sun)", Color) = (0.62, 0.62, 0.78, 1)
        [Header(Wings)]
        _Trailing ("Trailing Edge (model y)", Float) = 0.22
        _Leading ("Leading Edge (model y)", Float) = 0.6
        _Coverts ("Coverts From (share of the chord from the trailing edge)", Range(0, 1)) = 0.55
        _CovertColor ("Coverts (near the leading edge)", Color) = (0.74, 0.66, 0.54, 1)
        _Quills ("Flight Feathers Across The Span (per model unit)", Float) = 7
        _CovertRows ("Covert Rows Across The Chord", Float) = 2
        [Header(Body And Tail)]
        _TailBase ("Tail Starts At (model y)", Float) = 0.15
        _TailFeathers ("Tail Feathers (per radian of its fan)", Float) = 5
        _BodyRows ("Body Feather Rows (per model unit)", Float) = 5
        [Header(Feathers)]
        _Round ("Feather Tip Roundness", Range(0, 1)) = 0.45
        _LineDark ("Feather Edge Darkness", Range(0, 1)) = 0.45
        _LineWidth ("Feather Edge Width (share of a feather)", Range(0.02, 0.5)) = 0.07
        _Vary ("Lighter / Darker Between Feathers", Range(0, 0.5)) = 0.14
        _HalfSpan ("Half Span (model units)", Float) = 1.05
        _RimDark ("Darker Toward The Outline", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _BellyColor;
            half4 _TipColor;
            half _TipFrom;
            half _TipTo;
            half4 _ShadeColor;
            float _Trailing;
            float _Leading;
            half _Coverts;
            half4 _CovertColor;
            float _Quills;
            float _CovertRows;
            float _TailBase;
            float _TailFeathers;
            float _BodyRows;
            half _Round;
            half _LineDark;
            half _LineWidth;
            half _Vary;
            float _HalfSpan;
            half _RimDark;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // The wings are single sheets.
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // A thin line on either side of each whole number of the coordinate.
            half Line(float coordinate, half width)
            {
                float f = frac(coordinate);
                // Widened to a pixel at least and softened over one, so a far-off bird's lines don't break into crawling dots.
                float pixel = fwidth(coordinate);
                float w = max(width, pixel);
                return (1.0 - smoothstep(w - pixel, w + pixel, min(f, 1.0 - f))) * saturate(width / max(pixel, 1e-5));
            }

            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 p = input.positionOS;
                float span = abs(p.x);
                float side = sign(p.x) * 17.0;
                // Out from the body's long axis: kept as the wing turns about it.
                float reach = length(p.xz);
                half wing = smoothstep(0.12, 0.24, span);

                // Wings: long flight feathers side by side out along the span, their lines running front to back and their tips
                // rounded at the trailing edge; nearer the leading edge, paler coverts in rows of small rounded feathers over them.
                float chord = saturate((p.y - _Trailing) / max(_Leading - _Trailing, 1e-3));
                float quill = reach * _Quills;
                float across = frac(quill) * 2.0 - 1.0;
                half covert = smoothstep(_Coverts - 0.05, _Coverts + 0.05, chord + _Round * 0.15 * across * across);
                half flightEdge = Line(quill, _LineWidth);
                float rowCoord = (chord - _Coverts) / max(1.0 - _Coverts, 1e-3) * _CovertRows + _Round * across * across;
                float covertAcross = frac(quill * 1.5 + floor(rowCoord) * 0.5) * 2.0 - 1.0;
                half covertEdge = max(Line(rowCoord + _Round * covertAcross * covertAcross, _LineWidth), covert * (1.0 - covert) * 4.0);
                half wingEdge = lerp(flightEdge, covertEdge, covert);
                float wingId = covert > 0.5 ? floor(rowCoord) * 31.0 + floor(quill * 1.5) : floor(quill);
                // The flight feathers darken toward their tips at the trailing edge.
                half trailing = (1.0 - covert) * (1.0 - smoothstep(0.0, 0.6, chord)) * 0.32;

                // Tail: a fan of feathers from its base. Body: small rounded feathers in rows down the back.
                half tail = smoothstep(_TailBase + 0.03, _TailBase - 0.05, p.y);
                float fan = atan2(p.x, _TailBase + 0.2 - p.y) * _TailFeathers;
                float bodyRow = -p.y * _BodyRows;
                float bodyAcross = frac(p.x * _BodyRows + floor(bodyRow) * 0.5) * 2.0 - 1.0;
                half bodyEdge = lerp(Line(bodyRow + _Round * bodyAcross * bodyAcross, _LineWidth) * 0.6, Line(fan, _LineWidth), tail);
                float bodyId = tail > 0.5 ? floor(fan) + 101.0 : floor(bodyRow) * 13.0 + floor(p.x * _BodyRows + floor(bodyRow) * 0.5);

                half edge = lerp(bodyEdge, wingEdge, wing);
                float id = wing > 0.5 ? wingId : bodyId;
                half vary = (Hash(float2(id, side)) - 0.5) * 2.0 * _Vary;

                float3 normalWS = normalize(input.normalWS) * (front ? 1.0 : -1.0);
                half3 colour = _BaseColor.rgb;
                colour = lerp(colour, _CovertColor.rgb, wing * covert);
                colour *= 1.0 - trailing * wing;
                colour = lerp(colour, _BellyColor.rgb, smoothstep(0.1, -0.4, normalWS.y));
                colour = lerp(colour, _TipColor.rgb, smoothstep(_TipFrom, _TipTo, span / _HalfSpan));
                colour *= 1.0 + vary;
                colour *= 1.0 - edge * _LineDark;

                Light sun = GetMainLight();
                half lit = smoothstep(-0.15, 0.35, dot(normalWS, sun.direction));
                colour = lerp(colour * _ShadeColor.rgb, colour, lit);

                float3 toCamera = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = 1.0 - saturate(abs(dot(normalWS, toCamera)));
                colour *= 1.0 - _RimDark * rim * rim * rim;
                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        // The flock's shadows sweep the ground.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment NullFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 NullFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment NullFrag
            #pragma multi_compile_instancing

            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half4 NullFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            NormalsVaryings NormalsVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                NormalsVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(NormalsVaryings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
