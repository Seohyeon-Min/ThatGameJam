// 새 전용 윤곽: 새를 몸 중심에서 살짝 키운 껍질을 더하기로 그려, 실루엣 밖으로 금빛 붓 가루가 튀어나온다.
// 안쪽 껍질은 촘촘하고 바깥 껍질은 듬성하다. 새 본체(BirdPrism) 위에 두 번째 머티리얼로 얹는다.
Shader "ThatGameJam/BirdPrismGlow"
{
    Properties
    {
        [HDR] _FringeColour ("Fringe Colour", Color) = (1.6, 0.8, 0.25, 1)
        _FringeSize ("Fringe Length (share of the bird)", Range(0, 0.3)) = 0.05
        _FringePush ("Fringe Push Along Normals (m)", Range(0, 0.1)) = 0.012
        _FringeCoverage ("Fringe Coverage", Range(0, 1)) = 0.55
        _FringeGrain ("Fringe Stroke Fineness", Range(5, 120)) = 45
        _ChalkGrain ("Chalk Grain", Range(0, 1)) = 0.45
        _OuterStrength ("Outer Shell Strength", Range(0, 1)) = 0.5
        _CentreLift ("Bird Centre Height Above Its Origin (m)", Range(-1, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        // 몸(BirdPrism)이 칠한 자리는 건너뛰어, 실루엣 밖으로만 나온다.
        Stencil { Ref 64 ReadMask 64 Comp NotEqual }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "BirdPrism.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _FringeColour;
            half _FringeSize;
            half _FringePush;
            half _FringeCoverage;
            half _FringeGrain;
            half _ChalkGrain;
            half _OuterStrength;
            half _CentreLift;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 rest : TEXCOORD1;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 rest : TEXCOORD0;
        };

        Varyings Shell(Attributes input, float grow)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 centre = TransformObjectToWorld(float3(0, 0, 0)) + float3(0, _CentreLift, 0);
            positionWS = centre + (positionWS - centre) * (1.0 + _FringeSize * grow);
            positionWS += TransformObjectToWorldNormal(input.normalOS) * _FringePush * grow;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.rest = input.rest.xyz;
            return output;
        }

        half4 Fringe(Varyings input, half coverage, half strength)
        {
            float2 radial = PrismRadial(input.rest);
            // 몸에서 뻗는 짧은 붓 자국에 분필 같은 알갱이를 얹는다.
            float stroke = PrismNoise(float2(radial.x * _FringeGrain, radial.y * _FringeGrain * 0.2));
            float grain = PrismHash(floor(input.rest.xy * _FringeGrain * 6.0));
            half mask = step(1.0 - coverage, stroke) * (1.0 - _ChalkGrain * step(grain, 0.6));
            return half4(_FringeColour.rgb * mask * strength, 0);
        }
        ENDHLSL

        Pass
        {
            Name "Inner"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            Varyings Vert(Attributes input) { return Shell(input, 1.0); }
            half4 Frag(Varyings input) : SV_Target { return Fringe(input, _FringeCoverage, 1.0); }
            ENDHLSL
        }

        Pass
        {
            Name "Outer"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            Varyings Vert(Attributes input) { return Shell(input, 2.2); }
            half4 Frag(Varyings input) : SV_Target { return Fringe(input, _FringeCoverage * 0.5, _OuterStrength); }
            ENDHLSL
        }
    }
}
