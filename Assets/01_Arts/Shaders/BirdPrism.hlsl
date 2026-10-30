#ifndef BIRD_PRISM_INCLUDED
#define BIRD_PRISM_INCLUDED

// Shared by the bird's body, fringe and flying pieces. The bird's rest-pose position comes from BirdPrismBaker, so the
// paint stays on the wing while it flaps.

float PrismHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float PrismNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(PrismHash(i), PrismHash(i + float2(1, 0)), f.x),
                lerp(PrismHash(i + float2(0, 1)), PrismHash(i + float2(1, 1)), f.x), f.y);
}

// Coordinates that radiate from the body along the wings: x around the body, y outward.
float2 PrismRadial(float3 rest)
{
    float2 flat2 = float2(abs(rest.x), rest.y);
    return float2(atan2(flat2.y, flat2.x + 1e-4) / PI, length(flat2));
}

#endif
