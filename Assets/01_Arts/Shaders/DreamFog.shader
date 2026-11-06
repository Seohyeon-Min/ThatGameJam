// 꿈속 안개(Stage3 안개 구역): 화면 전체를 분홍~주황의 따뜻한 안개로 채우고, 새를 뺀 모든 것을 원근이 있는 그림자(실루엣)로 바꾼다.
// DreamFogFeature 가 투명 물체까지 그린 뒤에 그린다. 새는 BirdVisible 이 스텐실에 1 을 써 두어서 그 자리는 건너뛴다(새만 그대로).
// - 안개: 아래 주황, 위 분홍으로 넘어가는 색에, 빛(해) 자리의 빛번짐(진한 심과 넓은 무리)과 천천히 흐르는 옅은 색 얼룩을 더한다.
// - 그림자: 장면의 색은 쓰지 않고 깊이만 쓴다. 가까울수록 진한 그림자, 멀수록 안개에 녹는다(원근). 경계는 몇 번 나눠 읽어 번지고,
//   빛 가까이의 그림자는 빛에 먹힌다. 나무 꼭대기도 하늘도 같은 깊이 규칙이라 화면 전체가 안개로 찬다.
// - 빛줄기: 각 화소에서 빛 쪽으로 그림자를 몇 번 짚어, 그림자 사이로 새어 나오는 빛을 밝힌다.
// - 실크: 얇은 비단 천 몇 겹이 바람에 일렁이며 화면을 지나간다. 천이 접히는 결마다 빛이 맺혀 밝게 빛나고, 결을 따라 무지갯빛이
//   돌며, 빛 쪽일수록 더 환하다. 결 위와 빛 무리 안에서는 작은 빛 알갱이가 반짝였다 사라진다. 천은 그림자 위로도 지나간다.
// - 필름 같은 잔 알갱이를 얹는다.
// - 세기: _DreamFogWeight(구역에 들어선 정도) × 그 자리 안개 밀도(FogField 의 칸, 아이와 새가 걷어 낸 곳은 옅다).
// 색은 머티리얼(DreamFog.mat, DreamFog 컴포넌트의 Look)에서 고친다. 나머지 값은 DreamFog 가 전역으로 준다.
Shader "Hidden/ThatGameJam/DreamFog"
{
    Properties
    {
        _DreamAlpha ("Dream Fog Alpha (0 = none at all, just the path)", Range(0, 1)) = 1
        _DreamLow ("Fog Bottom", Color) = (1, 0.87, 0.8, 1)
        _DreamHigh ("Fog Top", Color) = (0.99, 0.86, 0.9, 1)
        _DreamBlob ("Drifting Patches (alpha = strength)", Color) = (0.93, 0.84, 0.97, 0.3)
        [HDR] _DreamSunColor ("Light", Color) = (1, 0.96, 0.86, 1)
        _DreamSunBrightness ("Light Brightness", Float) = 1.1
        _DreamShadow ("Shadows (alpha = how dark up close)", Color) = (0.8, 0.64, 0.7, 0.55)
        _DreamShadowNear ("Shadows Stay Full Within (metres of the bird's depth)", Float) = 0
        _DreamShadowMelt ("Then Vanish Into Fog Over (metres)", Float) = 5
        _DreamShadowFront ("In Front Of Bird, Vanish Over (metres toward camera)", Float) = 3
        _DreamShadowSoftness ("Shadow Edge Softness (pixels)", Range(0, 8)) = 3
        _DreamLightBleed ("Light Eats Nearby Shadows", Range(0, 2)) = 1
        _DreamBirdShade ("Shade Behind Bird And Baby (alpha = how dark)", Color) = (0.42, 0.28, 0.42, 0.45)
        _DreamBirdShadeSize ("Shade Behind Bird And Baby Size (metres)", Float) = 0.9
        _DreamFarBlur ("Blur Behind (pixels, at full distance)", Range(0, 40)) = 10
        _DreamTallShadow ("Tall Things Shadow (alpha = how dark)", Color) = (0.52, 0.36, 0.52, 0.8)
        _DreamTallFrom ("Tall Things From Height (metres over the ground)", Float) = 0.8
        _DreamTallTo ("Tall Things Full At Height (metres)", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        Pass
        {
            Name "DreamFog"
            // The bird (stencil 1 from BirdVisible) keeps its own colours.
            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _DreamFogWeight;     // 0 = off, 1 = full
            float _DreamAlpha;         // the material's overall amount: 0 = no dream fog, the scene as it is
            float4 _DreamLow;          // rgb fog colour at the bottom of the picture
            float4 _DreamHigh;         // rgb fog colour at the top
            float4 _DreamSunColor;     // rgb light colour
            float _DreamSunBrightness; // the light's strength
            float4 _DreamSun;          // xy light on the screen (0..1), z core size, w halo size (shares of the screen height)
            float4 _DreamBlob;         // rgb drifting colour patches, a strength
            float4 _DreamShadow;       // rgb shadow colour, a strength
            float _DreamShadowNear;    // metres past the bird (in depth) that stay fully shadow
            float _DreamFocusDepth;    // the bird's depth from the camera (DreamFog), so the perspective holds at any zoom
            float4 _DreamBird;         // xy the bird on the screen (0..1), z one metre at the bird's depth as a share of the screen's height
            float4 _DreamBaby;         // the same for the baby's middle, w 0 when she is behind the camera
            float4 _DreamBirdShade;    // rgb the soft shade behind the bird, a how dark
            float _DreamBirdShadeSize; // its radius, metres
            float _DreamShadowFront;   // metres over which shadows in front of the bird melt away toward the camera
            float _DreamShadowMelt;    // metres over which shadows melt into the fog after that
            float _DreamShadowSoftness;// edge softness (pixels)
            float _DreamLightBleed;    // how much the light eats nearby shadows
            float _DreamFarBlur;       // extra blur (pixels) for what lies far past the bird
            float4 _DreamTallShadow;   // rgb colour of what stands up out of the ground (the trees), a how dark
            float _DreamTallFrom;      // height over the ground (metres) where that starts
            float _DreamTallTo;        // and where it is full
            float4 _DreamRays;         // x strength of the light streaming between shadows, y grain, z cleared-fog floor, w unused
            float4 _DreamSilk;         // x strength of the silk veils, y folds per metre, z how fast the wind carries them (metres a second), w iridescence
            float4 _DreamSilkWind;     // xy which way the wind blows the silk in the world (x, z)
            float4 _DreamSilkPlanes;   // x the ground's height (world), y depth of the nearest silk slice, z how close the folds lie, w depth between slices
            float4 _DreamSilkFloat;    // x how high over the ground the silk starts, y how high it reaches (metres)
            float4 _DreamSparkle;      // x how many sparkles, y size of the 3D cells they hang in (metres), z how fast they twinkle, w brightness
            TEXTURE2D(_DreamFogMask);  // FogField's density cells: 1 = full fog, 0 = cleared
            float4 _DreamFogArea;      // xy area corner (world x, z), zw size

            bool IsSky(float raw)
            {
                #if UNITY_REVERSED_Z
                return raw <= 1e-6;
                #else
                return raw >= 1.0 - 1e-6;
                #endif
            }

            // How strongly something at this depth is a shadow: full around the bird's depth, melting into the fog behind it (Melt) and,
            // more gently, in front of it toward the camera (Front Fade), so near leaves don't stand as dark walls before the camera.
            float DepthFade(float distance)
            {
                float behind = exp(-max(distance - _DreamFocusDepth - _DreamShadowNear, 0.0) / max(_DreamShadowMelt, 1e-3));
                float front = exp(-max(_DreamFocusDepth - _DreamShadowNear - distance, 0.0) / max(_DreamShadowFront, 1e-3));
                return behind * front;
            }

            // How much of a shadow there is at this pixel: 1 near, melting into the fog with distance, 0 for the sky.
            float ShadowAt(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (IsSky(raw))
                    return 0.0;
                float distance = LinearEyeDepth(raw, _ZBufferParams);
                return DepthFade(distance);
            }

            // x: the shadow, as ShadowAt. y: that shadow again where something stands up out of the fog field's ground (a tree),
            // by its height over it (Tall Things), 0 on the ground itself.
            float2 ShadeAt(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (IsSky(raw))
                    return 0.0;
                float distance = LinearEyeDepth(raw, _ZBufferParams);
                float shadow = DepthFade(distance);
                #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float tall = smoothstep(_DreamTallFrom, max(_DreamTallTo, _DreamTallFrom + 0.01), world.y - _DreamSilkPlanes.x);
                return float2(shadow, shadow * tall);
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float time = _Time.y;

                // The fog itself: orange low, pink high, soft drifting patches, the light's core and halo.
                float drift = Noise(uv * 3.0 + float2(time * 0.03, time * 0.017));
                half3 fog = lerp(_DreamLow.rgb, _DreamHigh.rgb, saturate(uv.y * 0.9 + 0.05 + (drift - 0.5) * 0.25));
                float patches = Noise(uv * 1.7 + float2(-time * 0.012, time * 0.02) + 7.3);
                fog = lerp(fog, _DreamBlob.rgb, smoothstep(0.55, 0.85, patches) * _DreamBlob.a);
                float2 toSun = (uv - _DreamSun.xy) * float2(aspect, 1.0);
                float sunDistance = length(toSun);
                float core = exp(-sunDistance * sunDistance / max(_DreamSun.z * _DreamSun.z, 1e-5));
                float halo = exp(-sunDistance / max(_DreamSun.w, 1e-4));
                fog += _DreamSunColor.rgb * _DreamSunBrightness * (core * 1.2 + halo * 0.45);

                // Shadows, and what stands up out of the ground darker on top of them (ShadeAt). Blurred, the more the further past the
                // bird: near edges stay crisp, what is behind goes soft and then melts away. Sixteen taps on a spiral filling the disc,
                // turned differently at each pixel, so a wide blur reads as a soft smear with a little grain instead of copies of the shape.
                float2 centre = ShadeAt(uv);
                float2 texel = (_DreamShadowSoftness + _DreamFarBlur * (1.0 - centre.x)) / _ScreenParams.xy;
                float turn = Hash(uv * _ScreenParams.xy) * 6.283185;
                float2 shade = centre * (1.0 / 17.0);
                [unroll] for (int i = 0; i < 16; i++)
                {
                    float radius = sqrt((i + 0.5) / 16.0);
                    float angle = i * 2.399963 + turn;
                    shade += ShadeAt(uv + float2(cos(angle), sin(angle)) * radius * texel) * (1.0 / 17.0);
                }
                float shadow = shade.x;
                float tallShadow = shade.y;
                // Near the light the shadows are eaten by it.
                float eaten = 1.0 - saturate(core * _DreamLightBleed + halo * _DreamLightBleed * 0.3);
                shadow *= eaten;
                tallShadow *= eaten;

                // Light streaming between the shadows: how open the way to the light is, a few steps toward it.
                float open = 0.0;
                float2 step = (_DreamSun.xy - uv) / 10.0;
                [unroll] for (int s = 1; s <= 10; s++)
                    open += 1.0 - ShadowAt(uv + step * s);
                open /= 10.0;
                // Only brightens where the way is open: darkening where it's blocked drew a dirty dark tail behind everything, away
                // from the light.
                fog *= 1.0 + _DreamRays.x * halo * open;

                half3 dream = lerp(fog, _DreamShadow.rgb, saturate(shadow * _DreamShadow.a));

                // What stands up out of the ground (the trees) darker still: the ground itself keeps the soft shadow above.
                dream = lerp(dream, _DreamTallShadow.rgb, saturate(tallShadow) * _DreamTallShadow.a);

                // A soft shade behind the bird, so the white bird reads against the bright fog. The bird itself is skipped by the stencil,
                // so this shows only round it.
                float2 fromBird = (uv - _DreamBird.xy) * float2(aspect, 1.0);
                float shadeRadius = max(_DreamBirdShadeSize * _DreamBird.z, 1e-4);
                dream = lerp(dream, _DreamBirdShade.rgb, exp(-dot(fromBird, fromBird) / (shadeRadius * shadeRadius)) * _DreamBirdShade.a);
                // And behind the baby, who glows white over the fog (PrismOverlay).
                float2 fromBaby = (uv - _DreamBaby.xy) * float2(aspect, 1.0);
                float babyRadius = max(_DreamBirdShadeSize * _DreamBaby.z, 1e-4);
                dream = lerp(dream, _DreamBirdShade.rgb, exp(-dot(fromBaby, fromBaby) / (babyRadius * babyRadius)) * _DreamBirdShade.a * _DreamBaby.w);

                // Silk and sparkles float in the world, not on the screen: the silk is a field of curved folds in the air, read at three
                // depths along this pixel's line of sight. So it stays put as the camera moves (the near slice sliding past faster than
                // the far one), only the wind carries it, and it hangs between a low and a high height over the ground instead of lying on
                // it. A slice behind something nearer (a tree, the baby) is mostly hidden by it.
                float3 camera = GetCameraPositionWS();
                float3 sight = normalize(ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP) - camera);
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                float rawHere = SampleSceneDepth(uv);
                float sceneEye = IsSky(rawHere) ? 1e6 : LinearEyeDepth(rawHere, _ZBufferParams);
                float2 wind = normalize(_DreamSilkWind.xy + float2(1e-4, 0.0));
                float2 flow = wind * time * _DreamSilk.z;
                half sheen = 0.0;
                half foldHue = 0.0;
                [unroll] for (int v = 0; v < 3; v++)
                {
                    float eye = _DreamSilkPlanes.y + _DreamSilkPlanes.w * v;
                    float3 world = camera + sight * (eye / max(dot(sight, forward), 1e-3));
                    half hidden = eye > sceneEye ? 0.3 : 1.0;
                    float above = world.y - _DreamSilkPlanes.x;
                    half floating = smoothstep(_DreamSilkFloat.x - 1.0, _DreamSilkFloat.x + 1.0, above)
                                  * (1.0 - smoothstep(_DreamSilkFloat.y - 2.0, _DreamSilkFloat.y + 2.0, above));
                    float layer = v + 1.0;
                    float3 p = (world - float3(flow.x, 0.0, flow.y) * (0.8 + 0.3 * v)) * _DreamSilk.y * (0.8 + 0.25 * v) + v * 7.13;
                    // Billowing: the cloth bends with slow noise before its folds are read, in all three directions.
                    float3 warp = float3(Noise3(p * 0.6 + time * 0.07 * layer), Noise3(p * 0.6 + 3.7 - time * 0.05 * layer), Noise3(p * 0.6 + 9.1)) - 0.5;
                    // The folds lean with height too, so the sheets hang slanted in the air rather than lying flat.
                    float across = dot(p + warp * 2.2, float3(-wind.y, 0.45, wind.x)) + Noise3(p * 0.9) * 1.6;
                    // Folds: thin bright ridges where the cloth turns to the light, well apart, so the silk reads as a few loose
                    // folds rather than tight stripes.
                    float ridge = pow(saturate(1.0 - abs(sin(across * _DreamSilkPlanes.z + time * 0.4 * layer))), 10.0);
                    float veil = smoothstep(0.45, 0.8, Noise3(p * 0.45 + 11.0 * v)) * floating;
                    sheen += ridge * veil * hidden / layer;
                    foldHue += across * 0.15 * veil;
                }
                // Brighter toward the light, as sheer cloth with the sun behind it.
                half lit = 0.35 + 0.65 * saturate(halo * 1.5 + core);
                half3 rainbow = 0.5 + 0.5 * cos(6.28318 * (foldHue + time * 0.05 + float3(0.0, 0.33, 0.67)));
                half3 silkColour = lerp(_DreamSunColor.rgb, rainbow * 0.6 + _DreamSunColor.rgb * 0.6, _DreamSilk.w);
                dream += silkColour * sheen * lit * _DreamSilk.x;
                // A soft sheet of the cloth itself, lifting the picture a little where the veils lie.
                dream = lerp(dream, dream + _DreamHigh.rgb * 0.2, saturate(sheen) * _DreamSilk.x * 0.5);

                // Sparkles: a field of stars hung in the air in 3D, carried by the wind with the silk. The air is cut into cells of
                // Sparkle Spacing in x, y and z, each with at most one star somewhere inside; this pixel walks its line of sight through
                // the cells from near to far and lights each star by how close the line passes it. So stars keep their places in the
                // world: as the camera moves the near ones slide past faster than the far ones, and a tree or the baby in front hides
                // those behind. They gather into drifts with empty gaps (a large noise over the cells), most are tiny and now and then
                // one is bigger and brighter, each twinkles on its own beat, and only between the silk's low and high heights.
                float spacing = max(_DreamSparkle.y, 0.05);
                float3 windShift = float3(flow.x, 0.0, flow.y);
                float3 origin = camera - windShift;
                float rayStep = spacing / max(dot(sight, forward), 1e-3);
                float startAlong = max(_DreamSilkPlanes.y - 1.0, 0.5) / max(dot(sight, forward), 1e-3);
                // World size of one pixel per metre of depth, so a star is about a pixel or two at any distance.
                float pixelPerMetre = 2.0 / (_ScreenParams.y * UNITY_MATRIX_P[1][1]);
                float stars = 0.0;
                [loop] for (int k = 0; k < 24; k++)
                {
                    float3 at = origin + sight * (startAlong + rayStep * (k + 0.5));
                    float3 starCell = floor(at / spacing);
                    float chance = Hash3(starCell);
                    float drifts = smoothstep(0.4, 0.85, Noise3(starCell * 0.07));
                    if (chance >= _DreamSparkle.x * drifts * 2.0)
                        continue;
                    float3 star = (starCell + float3(Hash3(starCell + 3.1), Hash3(starCell + 7.7), Hash3(starCell + 11.3))) * spacing;
                    float3 toStar = star - origin;
                    float starEye = dot(toStar, forward);
                    float starAbove = star.y + windShift.y - _DreamSilkPlanes.x;
                    if (starEye <= 0.0 || starEye > sceneEye || starAbove < _DreamSilkFloat.x - 1.0 || starAbove > _DreamSilkFloat.y + 2.0)
                        continue;
                    float big = pow(Hash3(starCell + 19.7), 5.0);
                    float radius = starEye * pixelPerMetre * lerp(0.35, 1.6, big);
                    float3 off = toStar - sight * dot(toStar, sight);
                    float starLight = exp(-dot(off, off) / max(radius * radius, 1e-8));
                    float beat = saturate(sin(time * _DreamSparkle.z * (0.5 + Hash3(starCell + 23.0)) + chance * 60.0));
                    stars += starLight * lerp(0.45, 1.6, big) * (0.25 + 0.75 * pow(beat, 4.0));
                }
                float glow = 0.45 + 0.55 * saturate(sheen * 1.5 + halo);
                dream += _DreamSunColor.rgb * stars * glow * _DreamSparkle.w;
                dream += (Hash(uv * _ScreenParams.xy + frac(time) * 61.0) - 0.5) * _DreamRays.y;

                // As thick as the fog is where this pixel looks: thinner where the baby and the bird cleared it.
                float raw = SampleSceneDepth(uv);
                float local = 1.0;
                if (!IsSky(raw))
                {
                    float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                    float2 cell = (world.xz - _DreamFogArea.xy) / max(_DreamFogArea.zw, 1e-3);
                    if (all(cell >= 0.0) && all(cell <= 1.0))
                        local = lerp(_DreamRays.z, 1.0, SAMPLE_TEXTURE2D_LOD(_DreamFogMask, sampler_LinearClamp, cell, 0).r);
                }
                // Coming in, the scene first lifts into the fog's colours (a screen blend: brighter and clearer, never greyer), and the
                // shadowed dream comes over it later. A plain mix halfway in averaged the scene's cool shadows with the pink into grey.
                float w = saturate(_DreamFogWeight * local * _DreamAlpha);
                half3 lifted = 1.0 - (1.0 - scene) * (1.0 - saturate(fog) * 0.8);
                half3 start = lerp(scene, lifted, saturate(w * 2.0));
                return half4(lerp(start, dream, w * w), 1.0);
            }
            ENDHLSL
        }
    }
}
