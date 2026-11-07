// The migrating birds' soft fluff, FeatherBird's second material, drawn over the feathers: the bird again, puffed out a little
// along its normals, see-through where it turns side on so its outline dissolves, and that edge broken up by a 3D noise fixed on
// the body, the way the soft snow does it (SoftSnow), so the outline is downy instead of a hard cut. Same colours and values as
// the feathers' material (FeatherBird.hlsl). Draws no depth or shadow.
Shader "ThatGameJam/FeatherBirdFluff"
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
        _RimDark ("Darker Toward The Outline (feathers only)", Range(0, 1)) = 0.15
        [Header(Fluff)]
        _Puff ("Puff Out Along The Normal (model units)", Float) = 0.035
        _FluffScale ("Fluff Cells Per Model Unit", Float) = 28
        _Fluff ("Fluff Amount (how ragged its edge)", Range(0, 1)) = 0.6
        _EdgeStart ("Edge Fade Starts (facing, 0 = side on)", Range(0, 1)) = 0.6
        _EdgeEnd ("Edge Fully Clear", Range(0, 1)) = 0.1
        _FluffOpacity ("Fluff Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "WatercolorBake" = "Skip" }

        Pass
        {
            Name "Fluff"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "FeatherBird.hlsl"

            Varyings Vert(Attributes input) { return PlumageVert(input, _Puff); }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 toCamera = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(abs(dot(normalWS, toCamera)));
                float3 cell = input.positionOS * _FluffScale;
                half noise = FeatherNoise(cell) * 0.65 + FeatherNoise(cell * 2.3 + 7.1) * 0.35;
                half alpha = smoothstep(_EdgeEnd, _EdgeStart, facing + (noise - 0.5) * _Fluff) * _FluffOpacity;
                clip(alpha - 0.01);
                half3 colour = PlumageLight(Plumage(input.positionOS, normalWS), normalWS);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
