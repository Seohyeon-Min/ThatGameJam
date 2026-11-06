// A soft glowing ring for particles, drawn without a texture, added onto the picture.
// Pairs with a particle's own Size/Color over Lifetime: as the quad grows the ring spreads outward and widens,
// while Color over Lifetime's alpha fades it out, giving a Sky-style expanding call pulse for free (no script).
Shader "ThatGameJam/GlowRing"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.7, 1.4, 0.9, 1)
        _InnerRadius ("Inner Radius", Range(0, 0.95)) = 0.65
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.25
        // Set from a script (CryTail): xy the way the tail points across the particle, z 1 to draw it.
        [HideInInspector] _Tail ("Tail", Vector) = (0, 0, 0, 0)
        // How far the tail's point is from the middle, in ring radii. The particle is scaled up by as much to keep the ring's size.
        _TailTip ("Tail Tip", Range(1, 2)) = 1.45
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "GlowRing"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _InnerRadius;
                half _EdgeSoftness;
                float4 _Tail;
                float _TailTip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;      // -1..1 across the particle
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            // Signed distance to a triangle (Inigo Quilez's sdTriangle).
            float TriangleDistance(float2 p, float2 p0, float2 p1, float2 p2)
            {
                float2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
                float2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
                float2 pq0 = v0 - e0 * saturate(dot(v0, e0) / dot(e0, e0));
                float2 pq1 = v1 - e1 * saturate(dot(v1, e1) / dot(e1, e1));
                float2 pq2 = v2 - e2 * saturate(dot(v2, e2) / dot(e2, e2));
                float s = sign(e0.x * e2.y - e0.y * e2.x);
                float2 d = min(min(float2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                                   float2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                                   float2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
                return -sqrt(d.x) * sign(d.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance to the edge, in units of the ring's radius: the ring alone fills the particle. With the tail on, the ring
                // shrinks to make room for a point at the particle's edge toward _Tail.xy, like a speech bubble's.
                float2 p = input.uv;
                float dist = length(p) - 1.0;
                if (_Tail.z > 0.5)
                {
                    p *= _TailTip;
                    dist = length(p) - 1.0;
                    float2 toward = normalize(_Tail.xy);
                    float2 side = float2(-toward.y, toward.x);
                    // The sides meet the ring a little past where they would touch it, so the tail starts at a corner.
                    float2 baseMiddle = toward * 0.4;
                    float2 baseHalf = side * 0.69;
                    dist = min(dist, TriangleDistance(p, toward * _TailTip, baseMiddle + baseHalf, baseMiddle - baseHalf));
                }
                half outer = 1.0 - smoothstep(-_EdgeSoftness, 0.0, dist);
                half inner = smoothstep(_InnerRadius - 1.0 - _EdgeSoftness, _InnerRadius - 1.0, dist);
                half ring = outer * inner * input.color.a;
                return half4(_Color.rgb * input.color.rgb * ring, 0);
            }
            ENDHLSL
        }
    }
}
