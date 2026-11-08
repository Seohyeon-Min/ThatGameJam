// UI용 부드러운 그늘: Image 가운데가 가장 진하고 가장자리로 갈수록 투명해지는 둥근 그늘(텍스처 없이 UV로 계산).
// 흰 바탕의 엔딩 카드 위에서 흰 아이콘(리플레이 버튼) 뒤에 깔아 아이콘이 드러나게 한다. 색과 투명도는 Image의 Color.
// CanvasGroup의 알파도 받는다(정점 색).
Shader "ThatGameJam/SoftShadeUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Softness ("Softness (1 = fades from the middle, 0 = hard edge)", Range(0.01, 1)) = 0.85

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float _Softness;

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                // 0 in the middle, 1 at the circle's edge.
                float r = length(input.uv - 0.5) * 2.0;
                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                return fixed4(input.color.rgb, input.color.a * a * a);
            }
            ENDCG
        }
    }
}
