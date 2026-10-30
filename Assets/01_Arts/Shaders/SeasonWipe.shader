// Shows the old season's look outside the spring ring, as if the new look spreads out from its centre. SeasonWipeFeature copies
// the opaque scene once, in the old look (_BlitTexture), then draws this before the transparents each frame while the ring plays:
// the live picture underneath is already in the new look. Snow, wisps and the ring are transparents, so they stay live on top.
// The camera may move: each pixel finds its point on the ground, then where that point was in the photo (_SeasonWipeVP).
Shader "Hidden/ThatGameJam/SeasonWipe"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SeasonWipe"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Assets/01_Arts/Shaders/SunPool.hlsl"

            float4 _SpringRing;     // centre (xyz) and radius (w) while the ring plays
            float4x4 _SeasonWipeVP; // the camera's view-projection when the photo was taken
            float4 _SeasonWipe;     // x edge softness, y lump height, z lump size (world units), w unused
            float4 _SeasonWipeFade; // x ring radius where the photo starts fading out, y where it's gone

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;

                // This pixel's point on the ground at the ring's height; straight ahead where it looks above the horizon.
                float3 onView = ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                float3 camera = GetCameraPositionWS();
                float3 direction = onView - camera;
                float along = direction.y < -1e-4 ? (_SpringRing.y - camera.y) / direction.y : 1000.0;
                float3 ground = camera + direction * along;

                float2 photoUV = ComputeNormalizedDeviceCoordinates(ground, _SeasonWipeVP);
                if (any(photoUV < 0.0) || any(photoUV > 1.0))
                    return 0;

                // Outside the ring only, with a lumpy edge rather than a clean circle.
                float2 lumpAt = ground.xz / max(_SeasonWipe.z, 1e-3);
                float lumps = SunPoolNoise(lumpAt) * 0.6 + SunPoolNoise(lumpAt * 2.3 + 5.1) * 0.4;
                float fromEdge = length(ground.xz - _SpringRing.xz) - _SpringRing.w + (lumps - 0.5) * _SeasonWipe.y;
                half outside = smoothstep(-_SeasonWipe.x, _SeasonWipe.x, fromEdge);
                half fade = 1.0 - smoothstep(_SeasonWipeFade.x, _SeasonWipeFade.y, _SpringRing.w);

                half3 photo = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, photoUV, 0).rgb;
                return half4(photo, outside * fade);
            }
            ENDHLSL
        }
    }
}
