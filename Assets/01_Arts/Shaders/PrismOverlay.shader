// Prism overlay: makes something glow white through the dream fog, like the bird. PC_Renderer's BabyPrism feature draws the baby (the Baby
// layer) again with this after the dream fog, so in the fog she shines instead of turning into a shadow.
// - Mostly white: a soft white glow over all of her, above 1 so the bloom catches it.
// - Only toward her edges a thin pastel shimmer turns through the rainbow (by the angle she's seen at, drifting slowly), as on the bird.
// - Anything in front of her (a tree) hides her: she is tested against the camera depth texture, which holds every tree whole. The depth
//   buffer alone is not enough: the occlusion fade drops a tree's pixels in front of her (CameraFadeClip), so her depth is left there.
// - How strong: _PrismAmount (DreamFog: how far the fog is in) times Opacity.
Shader "ThatGameJam/PrismOverlay"
{
    Properties
    {
        _CoreColour ("Glow Colour", Color) = (1, 0.98, 0.95, 1)
        _CoreGlow ("Glow (above 1 goes into the bloom)", Range(0, 6)) = 1.4
        _EdgeShimmer ("Pastel Shimmer On The Edges", Range(0, 1)) = 0.45
        _EdgeWidth ("Shimmer Width", Range(0.5, 8)) = 2.5
        _ShimmerSpeed ("Shimmer Speed", Range(0, 2)) = 0.25
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PrismOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColour;
                half _CoreGlow, _EdgeShimmer, _EdgeWidth, _ShimmerSpeed, _Opacity;
            CBUFFER_END

            float _PrismAmount; // DreamFog: 0 out of the fog, 1 fully in

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.eyeDepth = -TransformWorldToView(output.positionWS).z;
                return output;
            }

            // A soft pastel rainbow: hue 0..1 to a colour that is never far from white.
            half3 Pastel(float hue)
            {
                half3 rainbow = saturate(abs(frac(hue + float3(0.0, 0.667, 0.333)) * 6.0 - 3.0) - 1.0);
                return lerp(half3(1, 1, 1), rainbow, 0.45);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // Out of the fog: nothing at all.
                clip(_PrismAmount * _Opacity - 0.001);
                // Hidden where something nearer stands in front (a tree), whether or not it was drawn there.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                clip(sceneDepth - (input.eyeDepth - 0.05));

                float3 normal = normalize(input.normalWS);
                float3 toEye = normalize(GetCameraPositionWS() - input.positionWS);
                float facing = saturate(dot(normal, toEye));
                float edge = pow(1.0 - facing, _EdgeWidth);

                half3 colour = _CoreColour.rgb * _CoreGlow;
                float hue = facing * 1.5 + input.positionWS.y * 0.6 + _Time.y * _ShimmerSpeed;
                colour = lerp(colour, Pastel(hue) * _CoreGlow, saturate(edge * 1.5) * _EdgeShimmer);

                return half4(colour, saturate(_PrismAmount * _Opacity));
            }
            ENDHLSL
        }
    }
}
