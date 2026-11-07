// A wild bird's plumage, painted from the model's own coordinates (the bird model has no usable UVs). On the wings: long flight
// feathers side by side out along the span, darker toward their tips at the trailing edge, pale coverts in rows near the leading
// edge, and the wing tips darkening. A fan of feathers on the tail, small rounded ones down the back, a paler underside. Each
// feather a little lighter or darker than the next, with a thin dark line where they meet. Matte: no shine and no glow, so it does
// not look like the player's prism bird. Bold on purpose: the flock is seen small, from far off.
// The flight feathers are laid out by distance from the body's long axis, so they stay on the wing while it beats.
// The plumage itself is in FeatherBird.hlsl; the soft fluff over it is a second material (FeatherBirdFluff), whose values are
// listed here too so both materials can carry the same ones.
// For the migrating birds (MigratingBirds easter egg).
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
        _Wrap ("Light Wrap (softer shading)", Range(0, 1)) = 0.5
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
        _RimDark ("Darker Toward The Outline", Range(0, 1)) = 0.15
        [Header(Fluff (the FeatherBirdFluff material))]
        _Puff ("Puff Out Along The Normal (model units)", Float) = 0.035
        _FluffScale ("Fluff Cells Per Model Unit", Float) = 28
        _Fluff ("Fluff Amount (how ragged its edge)", Range(0, 1)) = 0.6
        _EdgeStart ("Edge Fade Starts (facing, 0 = side on)", Range(0, 1)) = 0.6
        _EdgeEnd ("Edge Fully Clear", Range(0, 1)) = 0.1
        _FluffOpacity ("Fluff Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

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
            #include "FeatherBird.hlsl"

            Varyings Vert(Attributes input) { return PlumageVert(input, 0.0); }

            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS) * (front ? 1.0 : -1.0);
                half3 colour = PlumageLight(Plumage(input.positionOS, normalWS), normalWS);
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
            #include "FeatherBird.hlsl"

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
            #include "FeatherBird.hlsl"

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
            #include "FeatherBird.hlsl"

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
