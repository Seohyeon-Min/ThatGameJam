// The migrating birds' plumage, shared by FeatherBird (the feathers) and FeatherBirdFluff (the soft fluff drawn over them). Both
// materials carry the same values (BuildMigratingBirdLook copies them across). Model space: x across the wings, y along the body
// (head +y), z thickness.
#ifndef FEATHER_BIRD_INCLUDED
#define FEATHER_BIRD_INCLUDED

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
    half _Wrap;
    float _Puff;
    float _FluffScale;
    half _Fluff;
    half _EdgeStart;
    half _EdgeEnd;
    half _FluffOpacity;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionOS : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Puffed out along the normal by this many model units (the fluff; 0 for the feathers).
Varyings PlumageVert(Attributes input, float puff)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionOS = input.positionOS.xyz + normalize(input.normalOS) * puff;
    output.positionOS = positionOS;
    output.positionWS = TransformObjectToWorld(positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

float FeatherHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float FeatherNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n = dot(i, float3(1.0, 57.0, 113.0));
    float4 a = frac(sin(float4(n, n + 1.0, n + 57.0, n + 58.0)) * 43758.5453);
    float4 b = frac(sin(float4(n + 113.0, n + 114.0, n + 170.0, n + 171.0)) * 43758.5453);
    float4 c = lerp(a, b, f.z);
    float2 d = lerp(c.xy, c.zw, f.y);
    return lerp(d.x, d.y, f.x);
}

// A thin line on either side of each whole number of the coordinate. Widened to a pixel at least and softened over one, so a
// far-off bird's lines don't break into crawling dots.
half FeatherLine(float coordinate, half width)
{
    float f = frac(coordinate);
    float pixel = fwidth(coordinate);
    float w = max(width, pixel);
    return (1.0 - smoothstep(w - pixel, w + pixel, min(f, 1.0 - f))) * saturate(width / max(pixel, 1e-5));
}

// The plumage's colour at this point of the model, before the light.
half3 Plumage(float3 p, float3 normalWS)
{
    float span = abs(p.x);
    float side = sign(p.x) * 17.0;
    // Out from the body's long axis: kept as the wing turns about it.
    float reach = length(p.xz);
    half wing = smoothstep(0.12, 0.24, span);

    // Wings: long flight feathers side by side out along the span, their lines running front to back and their tips rounded at
    // the trailing edge; nearer the leading edge, paler coverts in rows of small rounded feathers over them.
    float chord = saturate((p.y - _Trailing) / max(_Leading - _Trailing, 1e-3));
    float quill = reach * _Quills;
    float across = frac(quill) * 2.0 - 1.0;
    half covert = smoothstep(_Coverts - 0.05, _Coverts + 0.05, chord + _Round * 0.15 * across * across);
    half flightEdge = FeatherLine(quill, _LineWidth);
    float rowCoord = (chord - _Coverts) / max(1.0 - _Coverts, 1e-3) * _CovertRows + _Round * across * across;
    float covertAcross = frac(quill * 1.5 + floor(rowCoord) * 0.5) * 2.0 - 1.0;
    half covertEdge = max(FeatherLine(rowCoord + _Round * covertAcross * covertAcross, _LineWidth), covert * (1.0 - covert) * 4.0);
    half wingEdge = lerp(flightEdge, covertEdge, covert);
    float wingId = covert > 0.5 ? floor(rowCoord) * 31.0 + floor(quill * 1.5) : floor(quill);
    // The flight feathers darken toward their tips at the trailing edge.
    half trailing = (1.0 - covert) * (1.0 - smoothstep(0.0, 0.6, chord)) * 0.32;

    // Tail: a fan of feathers from its base. Body: small rounded feathers in rows down the back.
    half tail = smoothstep(_TailBase + 0.03, _TailBase - 0.05, p.y);
    float fan = atan2(p.x, _TailBase + 0.2 - p.y) * _TailFeathers;
    float bodyRow = -p.y * _BodyRows;
    float bodyAcross = frac(p.x * _BodyRows + floor(bodyRow) * 0.5) * 2.0 - 1.0;
    half bodyEdge = lerp(FeatherLine(bodyRow + _Round * bodyAcross * bodyAcross, _LineWidth) * 0.6, FeatherLine(fan, _LineWidth), tail);
    float bodyId = tail > 0.5 ? floor(fan) + 101.0 : floor(bodyRow) * 13.0 + floor(p.x * _BodyRows + floor(bodyRow) * 0.5);

    half edge = lerp(bodyEdge, wingEdge, wing);
    float id = wing > 0.5 ? wingId : bodyId;
    half vary = (FeatherHash(float2(id, side)) - 0.5) * 2.0 * _Vary;

    half3 colour = _BaseColor.rgb;
    colour = lerp(colour, _CovertColor.rgb, wing * covert);
    colour *= 1.0 - trailing * wing;
    colour = lerp(colour, _BellyColor.rgb, smoothstep(0.1, -0.4, normalWS.y));
    colour = lerp(colour, _TipColor.rgb, smoothstep(_TipFrom, _TipTo, span / _HalfSpan));
    colour *= 1.0 + vary;
    colour *= 1.0 - edge * _LineDark;
    return colour;
}

// Soft light: wrapped round the form, the side turned from the sun a cool shade, not dark.
half3 PlumageLight(half3 colour, float3 normalWS)
{
    Light sun = GetMainLight();
    half lit = smoothstep(0.0, 1.0, saturate((dot(normalWS, sun.direction) + _Wrap) / (1.0 + _Wrap)));
    return lerp(colour * _ShadeColor.rgb, colour, lit);
}

#endif
