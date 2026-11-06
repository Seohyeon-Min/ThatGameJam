// Forest mist: the dream fog's quiet sister (ForestMistFeature draws it while a ForestMist in the scene says the fog is in). It doesn't
// turn the picture into shadows: the scene stays as it is, and a clear blue-green mist lies low over the ground, thin near the camera and
// thicker into the distance, its wisps drifting slowly. Far things fade softly into it and take a little of its colour; it glows toward
// the main light, a few soft shafts stream from there, and small motes hang low in the air.
// How much: the ForestMist's weight (how far into the fog the bird is) times Mist Alpha. Lowering it only thins the mist; nothing brightens.
Shader "Hidden/ThatGameJam/ForestMist"
{
    Properties
    {
        _MistAlpha ("Mist Alpha (0 = none)", Range(0, 1)) = 1
        _MistMaxOpacity ("Thickest It Gets", Range(0, 1)) = 0.75

        [Header(Colour)]
        _MistColor ("Mist Colour (low)", Color) = (0.5, 0.72, 0.76, 1)
        _MistHighColor ("Mist Colour (higher up)", Color) = (0.68, 0.83, 0.86, 1)
        [HDR] _MistLightColor ("Colour Toward The Light", Color) = (0.86, 0.97, 1.0, 1)
        _MistLightGlow ("Glow Toward The Light", Range(0, 3)) = 0.7
        _SceneTint ("Tint The Scene Inside The Mist", Range(0, 1)) = 0.3

        [Header(How it lies)]
        _MistHeight ("Lies This High (metres over the fog field's ground)", Float) = 2.5
        _MistDensity ("Thickness At The Ground (per metre)", Range(0, 0.5)) = 0.07
        _HazeDensity ("Haze Above It (per metre)", Range(0, 0.1)) = 0.008
        _MistNear ("Clear Around The Camera (metres)", Float) = 5
        _MaxDistance ("Furthest Distance (metres)", Float) = 90

        [Header(Wisps)]
        _WispSize ("Wisp Size (metres)", Float) = 6
        _WispBreakup ("Wisps: How Patchy", Range(0, 1)) = 0.5
        _WispWind ("Wind (x, z metres a second)", Vector) = (0.25, 0, 0.15, 0)

        [Header(Light shafts)]
        _ShaftStrength ("Light Shafts", Range(0, 2)) = 0.4
        _ShaftLength ("Shaft Length (share of the screen)", Range(0.05, 1)) = 0.45

        [Header(Motes)]
        [HDR] _MoteColor ("Mote Colour", Color) = (0.8, 1.0, 1.0, 1)
        _MoteAmount ("How Many Motes", Range(0, 1)) = 0.15
        _MoteCellSize ("Mote Spacing (metres)", Float) = 1.2
        _MoteHeight ("Motes Up To (metres over the ground)", Float) = 4
        _MoteSpeed ("Mote Twinkle Speed", Float) = 1.2
        _MoteBrightness ("Mote Brightness", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        Pass
        {
            Name "ForestMist"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ForestMistWeight;  // ForestMist: 0 out of the fog, 1 fully in
            float _ForestMistGround;  // ForestMist: the fog field's ground height (world y)

            float _MistAlpha, _MistMaxOpacity;
            float4 _MistColor, _MistHighColor, _MistLightColor;
            float _MistLightGlow, _SceneTint;
            float _MistHeight, _MistDensity, _HazeDensity, _MistNear, _MaxDistance;
            float _WispSize, _WispBreakup;
            float4 _WispWind;
            float _ShaftStrength, _ShaftLength;
            float4 _MoteColor;
            float _MoteAmount, _MoteCellSize, _MoteHeight, _MoteSpeed, _MoteBrightness;

            bool IsSky(float raw)
            {
                #if UNITY_REVERSED_Z
                return raw <= 1e-6;
                #else
                return raw >= 1.0 - 1e-6;
                #endif
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // Mist per metre at a point: thickest on the ground, thinning out upward over Mist Height, broken into slow wisps; a thin
            // haze over all of it.
            float Density(float3 p, float time)
            {
                float above = max(p.y - _ForestMistGround, 0.0);
                float low = exp(-above / max(_MistHeight, 0.1));
                float3 q = (p - float3(_WispWind.x, 0.0, _WispWind.z) * time) / max(_WispSize, 0.1);
                q.y *= 2.5; // flatter wisps: they lie, rather than stand
                float wisp = Noise3(q) * 0.65 + Noise3(q * 2.3 + 5.1) * 0.35;
                wisp = lerp(1.0, smoothstep(0.25, 0.75, wisp) * 1.6, _WispBreakup);
                return low * wisp * _MistDensity + _HazeDensity;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                float amount = saturate(_ForestMistWeight * _MistAlpha);
                if (amount <= 0.0)
                    return half4(scene, 1.0);
                float time = _Time.y;

                float3 camera = GetCameraPositionWS();
                float raw = SampleSceneDepth(uv);
                bool sky = IsSky(raw);
                #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
                #endif
                float3 sight = normalize(ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP) - camera);
                float rayLength = _MaxDistance;
                if (!sky)
                    rayLength = min(distance(ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP), camera), _MaxDistance);

                // March the line of sight past the clear space round the camera, each pixel starting a little differently so the steps
                // don't show as bands.
                float start = min(_MistNear, rayLength);
                float stepLength = (rayLength - start) / 10.0;
                float jitter = Hash(uv * _ScreenParams.xy + frac(time) * 17.0);
                float depthInMist = 0.0;
                float heightSum = 0.0;
                [unroll] for (int i = 0; i < 10; i++)
                {
                    float3 p = camera + sight * (start + stepLength * (i + jitter));
                    float d = Density(p, time) * stepLength;
                    depthInMist += d;
                    heightSum += d * saturate((p.y - _ForestMistGround) / max(_MistHeight * 3.0, 0.1));
                }
                float opacity = (1.0 - exp(-depthInMist)) * _MistMaxOpacity;
                float high = depthInMist > 1e-4 ? heightSum / depthInMist : 0.0;

                // Its colour: bluer low, paler higher up, glowing toward the light as mist does with the sun behind it.
                Light sun = GetMainLight();
                float towardSun = saturate(dot(sight, sun.direction));
                half3 mist = lerp(_MistColor.rgb, _MistHighColor.rgb, high);
                mist = lerp(mist, _MistLightColor.rgb, saturate(pow(towardSun, 6.0) * _MistLightGlow));

                // A few soft shafts: from the light's place on the screen, how open the way to it is (sky or far away).
                half shafts = 0.0;
                float4 sunClip = mul(UNITY_MATRIX_VP, float4(camera + sun.direction * 1000.0, 1.0));
                if (_ShaftStrength > 0.0 && sunClip.w > 0.0)
                {
                    float2 sunUV = sunClip.xy / sunClip.w * 0.5 + 0.5;
                    #if UNITY_UV_STARTS_AT_TOP
                    sunUV.y = 1.0 - sunUV.y;
                    #endif
                    float2 toSun = (sunUV - uv) * _ShaftLength / 12.0;
                    float open = 0.0;
                    [unroll] for (int s = 1; s <= 12; s++)
                    {
                        float r = SampleSceneDepth(uv + toSun * (s + jitter));
                        open += IsSky(r) ? 1.0 : saturate(LinearEyeDepth(r, _ZBufferParams) / _MaxDistance);
                    }
                    open /= 12.0;
                    float nearSun = saturate(1.0 - length(sunUV - uv) * 1.2);
                    shafts = open * nearSun * nearSun * _ShaftStrength * saturate(depthInMist * 2.0);
                }

                // What lies inside the mist takes a little of its colour (never brighter), then the mist lies over it.
                half3 tinted = lerp(scene, scene * mist * 1.25, saturate(depthInMist) * _SceneTint);
                half3 colour = lerp(tinted, mist, opacity) + _MistLightColor.rgb * shafts;

                // Motes: at most one in each cell of the air, low over the ground, lit by how close the line of sight passes it.
                if (_MoteAmount > 0.0)
                {
                    float spacing = max(_MoteCellSize, 0.1);
                    float pixelPerMetre = 2.0 / (_ScreenParams.y * UNITY_MATRIX_P[1][1]);
                    float3 drift = float3(_WispWind.x, 0.1, _WispWind.z) * time * 0.5;
                    float3 origin = camera - drift;
                    float motes = 0.0;
                    [loop] for (int k = 0; k < 16; k++)
                    {
                        float3 at = origin + sight * (spacing * (k + 1.5));
                        float3 cell = floor(at / spacing);
                        float chance = Hash3(cell);
                        if (chance >= _MoteAmount)
                            continue;
                        float3 mote = (cell + float3(Hash3(cell + 3.1), Hash3(cell + 7.7), Hash3(cell + 11.3))) * spacing;
                        float moteAbove = mote.y + drift.y - _ForestMistGround;
                        if (moteAbove < 0.0 || moteAbove > _MoteHeight)
                            continue;
                        float3 toMote = mote - origin;
                        float along = dot(toMote, sight);
                        if (along <= 0.0 || along > rayLength)
                            continue;
                        float radius = along * pixelPerMetre * 1.2;
                        float3 off = toMote - sight * along;
                        float beat = saturate(sin(time * _MoteSpeed * (0.5 + Hash3(cell + 23.0)) + chance * 60.0));
                        motes += exp(-dot(off, off) / max(radius * radius, 1e-8)) * (0.3 + 0.7 * beat * beat);
                    }
                    colour += _MoteColor.rgb * motes * _MoteBrightness;
                }

                return half4(lerp(scene, colour, amount), 1.0);
            }
            ENDHLSL
        }
    }
}
