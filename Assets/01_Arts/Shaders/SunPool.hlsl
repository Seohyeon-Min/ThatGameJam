// The lumpy shape of a pool of sunlight on the ground. LightBeam.shader paints it on the ground and WatercolorFlat.shader
// lights the baby with it, so both must use the same shape to line up.
#ifndef THATGAMEJAM_SUN_POOL_INCLUDED
#define THATGAMEJAM_SUN_POOL_INCLUDED

float SunPoolHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SunPoolNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = SunPoolHash(i);
    float b = SunPoolHash(i + float2(1, 0));
    float c = SunPoolHash(i + float2(0, 1));
    float d = SunPoolHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// worldXZ: the point on the ground. r: its distance from the pool's centre, 1 at the pool's reach.
// Round overall, with the noise pushing the edge in and out into lumps and a few loose patches. Higher is further inside.
float SunPoolField(float2 worldXZ, float r, float blobSize, float ragged, float drift)
{
    float2 q = worldXZ / max(blobSize, 1e-3) + _Time.y * drift * float2(0.7, 0.4);
    float lumps = SunPoolNoise(q) * 0.55 + SunPoolNoise(q * 2.1 + 7.3) * 0.3 + SunPoolNoise(q * 4.3 + 13.1) * 0.15;
    return 1.0 - r + (lumps - 0.5) * ragged;
}

// Always gone by the pool's reach, whatever the noise says.
float SunPoolRimFade(float r)
{
    return saturate((1.0 - r) * 8.0);
}

#endif
