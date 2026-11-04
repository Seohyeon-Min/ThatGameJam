// 수채화 Kuwahara 후처리. WatercolorKuwaharaFeature가 패스 번호로 부른다.
// 모든 패스는 Blit.hlsl의 전체 화면 삼각형을 쓰고, 입력은 _BlitTexture로 받는다.
// WebGL2에는 컴퓨트 셰이더가 없어서 전부 프래그먼트 패스로 짰다.
//
// 0 Tensor        : 색 Sobel로 구조 텐서(E, F, G)와 깊이 경계를 구한다
// 1 TensorNormals : 0에 노멀 경계를 더한다
// 2 BlurH         : 텐서 가로 가우시안
// 3 BlurV         : 텐서 세로 가우시안 + 방향과 비등방성 계산
// 4 Classic       : 사분면 4개 Kuwahara (기준선)
// 5 Anisotropic8  : 타원 커널, 섹터 8개
// 6 Anisotropic4  : 타원 커널, 섹터 4개
// 7 Composite     : 번짐, 안료, 붓결, 종이를 입혀 원본 해상도로 출력
// 8 Copy          : 원본 그대로 (Characters Only 에서 캐릭터 밖)
// 9 CompositeCharacters : 7 과 같지만 캐릭터 자리(스텐실 128)에만
// 10 Strokes      : 4~6 대신 붓질을 새로 얹는다. 결을 따라 놓인 또렷한 색 조각들. 알파는 칠한 곳
// 11 CharacterMask: 캐릭터 자리(스텐실 128)를 1 로. 붓질이 캐릭터에서 시작해 밖으로 삐져나가게 한다
Shader "Hidden/ThatGameJam/WatercolorKuwahara"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        TEXTURE2D(_OrientTex);   // x, y: 접선 방향의 두 배 각 벡터(길이 = 비등방성), z: 기하 경계
        TEXTURE2D(_FilteredTex); // Kuwahara 결과 (작업 해상도)
        TEXTURE2D(_PaperTex);
        // 256x256 텍셀마다 독립 난수 4채널. 쌍선형으로 읽으면 텍셀 하나가 격자 한 칸인 value noise 가 된다.
        // 해시를 화소마다 계산하는 것보다 훨씬 싸다(합성 패스 1080p 기준 약 1.5 ms 가 줄었다).
        TEXTURE2D(_NoiseTex);

        float4 _WorkSize;    // 작업 해상도 w, h, 1/w, 1/h
        float4 _SourceSize;  // 원본 해상도 w, h, 1/w, 1/h
        float4 _Kuwahara;    // 반지름(작업 px), 샘플 간격(작업 px), 비등방 강도, 섹터 선명도 q
        float4 _Kuwahara2;   // 섹터 중심 겹침 zeta, 섹터 경계 eta, 격자 반경(정수), 경계 보존
        float4 _Style;       // 필터 강도, 색 단순화, 안료 얼룩, 번짐 거리(원본 px)
        float4 _Style2;      // 가장자리 어둡게, 종이 강도, 종이 크기, 붓결 강도
        float4 _Style3;      // 종이 텍스처 사용(0/1), 원본 px 스케일(높이/1080)
        float4 _Strokes;     // 붓질 간격(1080p px), 길이 비율, 너비 비율, 가장자리 거칠기
        float4 _Strokes2;    // 명도 흔들림, 색조 흔들림(라디안), 채도 흔들림, 형태 따르기
        float4 _Strokes3;    // 덮는 비율, 강한 붓질 비율, 강한 붓질 배수, 디테일 남기기
        float4 _Strokes4;    // 캐릭터 마스크 사용(0/1), 밖으로 삐져나가는 정도, 단색 면 매끄럽게
        float4 _StrokeAnchor; // 붓질 배치의 기준점(1080p px), 그곳의 1 m 당 px, 기준 있음(0/1)
        float4 _StrokeAxis;   // 기준의 오른쪽(화면, 단위 벡터)
        TEXTURE2D(_CharacterMaskTex);

        // 평탄한 곳의 작은 기울기 잡음이 방향을 흔들지 않도록 텐서 합에 더하는 값.
        #define ANISOTROPY_EPSILON 0.002
        // 경계 보존이 최대일 때 커널이 줄어드는 비율.
        #define EDGE_MIN_SCALE 0.2

        // ---------- 공용 ----------

        float3 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
        }

        // 원근과 직교 카메라 모두에서 눈 공간 깊이를 낸다.
        float SceneEyeDepth(float2 uv)
        {
            // LOD 0 으로 읽는다. 붓질 패스는 반복문 안에서 읽어서 미분값이 정의되지 않는다.
            float raw = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv), 0).r;
            if (unity_OrthoParams.w > 0.5)
            {
                #if UNITY_REVERSED_Z
                raw = 1.0 - raw;
                #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
            }
            return LinearEyeDepth(raw, _ZBufferParams);
        }

        // 깊이의 2차 미분으로 실루엣만 잡는다. 1차 차이는 비스듬한 바닥에서도 커지기 때문이다.
        float DepthEdge(float2 uv, float2 texel)
        {
            float c = SceneEyeDepth(uv);
            float l = SceneEyeDepth(uv - float2(texel.x, 0));
            float r = SceneEyeDepth(uv + float2(texel.x, 0));
            float d = SceneEyeDepth(uv - float2(0, texel.y));
            float u = SceneEyeDepth(uv + float2(0, texel.y));
            float laplacian = max(abs(l + r - 2.0 * c), abs(d + u - 2.0 * c));
            return smoothstep(0.02, 0.1, laplacian / max(c, 1e-3));
        }

        float NormalEdge(float2 uv, float2 texel)
        {
            float3 c = SampleSceneNormals(uv);
            float m = min(min(dot(c, SampleSceneNormals(uv - float2(texel.x, 0))),
                              dot(c, SampleSceneNormals(uv + float2(texel.x, 0)))),
                          min(dot(c, SampleSceneNormals(uv - float2(0, texel.y))),
                              dot(c, SampleSceneNormals(uv + float2(0, texel.y)))));
            return smoothstep(0.15, 0.5, 1.0 - m);
        }

        // 두 배 각 벡터에서 접선 단위 벡터를 되살린다. 부호가 없는 방향이라 보간해도 안전하다.
        float2 TangentFromDoubleAngle(float2 v)
        {
            float len = length(v);
            if (len < 1e-4)
                return float2(1, 0);
            float c = v.x / len;
            return float2(sqrt(saturate(0.5 + 0.5 * c)), (v.y >= 0 ? 1.0 : -1.0) * sqrt(saturate(0.5 - 0.5 * c)));
        }

        // 화면 공간에 고정된 결정적 잡음. 프레임마다 바뀌지 않아 깜빡이지 않는다.
        // cellPx: 격자 한 칸의 크기(1080p 기준 화소). 네 채널은 서로 독립인 잡음이다.
        float4 Noise(float2 pixel, float cellPx)
        {
            return SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_LinearRepeat, pixel / (cellPx * 256.0), 0);
        }

        // ---------- 0, 1: 구조 텐서 ----------

        float4 StructureTensor(float2 uv, bool useNormals)
        {
            float2 t = _WorkSize.zw;
            // 1 이 넘는 값이 기울기를 독차지하지 않게 자른다.
            float3 c00 = saturate(SampleSource(uv + t * float2(-1, -1)));
            float3 c10 = saturate(SampleSource(uv + t * float2( 0, -1)));
            float3 c20 = saturate(SampleSource(uv + t * float2( 1, -1)));
            float3 c01 = saturate(SampleSource(uv + t * float2(-1,  0)));
            float3 c21 = saturate(SampleSource(uv + t * float2( 1,  0)));
            float3 c02 = saturate(SampleSource(uv + t * float2(-1,  1)));
            float3 c12 = saturate(SampleSource(uv + t * float2( 0,  1)));
            float3 c22 = saturate(SampleSource(uv + t * float2( 1,  1)));

            float3 gx = (c20 + 2.0 * c21 + c22 - c00 - 2.0 * c01 - c02) * 0.25;
            float3 gy = (c02 + 2.0 * c12 + c22 - c00 - 2.0 * c10 - c20) * 0.25;

            float edge = DepthEdge(uv, t);
            if (useNormals)
                edge = max(edge, NormalEdge(uv, t));

            return float4(dot(gx, gx), dot(gx, gy), dot(gy, gy), edge);
        }

        float4 FragTensor(Varyings input) : SV_Target { return StructureTensor(input.texcoord, false); }
        float4 FragTensorNormals(Varyings input) : SV_Target { return StructureTensor(input.texcoord, true); }

        // ---------- 2, 3: 텐서 평활화 ----------

        // sigma 2 px 가우시안. 방향이 화소마다 튀지 않게 하는 핵심 단계다.
        float4 BlurTensor(float2 uv, float2 axis)
        {
            float4 sum = 0;
            float weightSum = 0;
            [unroll]
            for (int k = -4; k <= 4; k++)
            {
                float w = exp(-(k * k) / 8.0);
                sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + axis * k, 0) * w;
                weightSum += w;
            }
            return sum / weightSum;
        }

        float4 FragBlurH(Varyings input) : SV_Target
        {
            return BlurTensor(input.texcoord, float2(_WorkSize.z, 0));
        }

        float4 FragBlurV(Varyings input) : SV_Target
        {
            float4 s = BlurTensor(input.texcoord, float2(0, _WorkSize.w));
            float e = s.x, f = s.y, g = s.z;
            // (G - E, -2F)는 접선 방향 각의 두 배를 가리키고, 길이는 (l1 - l2)다.
            // 합으로 나누면 길이가 비등방성 A = (l1 - l2) / (l1 + l2)가 된다.
            float2 v = float2(g - e, -2.0 * f) / (e + g + ANISOTROPY_EPSILON);
            // 흐림으로 얇아진 경계 값을 다시 키운다.
            return float4(v, saturate(s.w * 3.0), 0);
        }

        // ---------- 4: Classic Kuwahara ----------

        float4 FragClassic(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float edge = SAMPLE_TEXTURE2D_LOD(_OrientTex, sampler_LinearClamp, uv, 0).z;
            float shrink = lerp(1.0, EDGE_MIN_SCALE, saturate(edge * _Kuwahara2.w));
            float2 step = _WorkSize.zw * _Kuwahara.x * shrink / _Kuwahara2.z;
            int extent = (int)_Kuwahara2.z;

            float3 mean[4] = { (float3)0, (float3)0, (float3)0, (float3)0 };
            float3 sq[4] = { (float3)0, (float3)0, (float3)0, (float3)0 };
            float count[4] = { 0, 0, 0, 0 };

            [loop]
            for (int j = -extent; j <= extent; j++)
            {
                [loop]
                for (int i = -extent; i <= extent; i++)
                {
                    float3 c = SampleSource(uv + float2(i, j) * step);
                    float3 c2 = c * c;
                    // 축 위의 샘플은 이웃한 두 사분면에 모두 들어간다(겹치는 영역).
                    float4 inQuadrant = float4(i <= 0 && j <= 0, i >= 0 && j <= 0, i <= 0 && j >= 0, i >= 0 && j >= 0);
                    [unroll]
                    for (int q = 0; q < 4; q++)
                    {
                        mean[q] += c * inQuadrant[q];
                        sq[q] += c2 * inQuadrant[q];
                        count[q] += inQuadrant[q];
                    }
                }
            }

            float3 result = 0;
            float minVariance = 1e9;
            [unroll]
            for (int k = 0; k < 4; k++)
            {
                float3 m = mean[k] / count[k];
                float v = dot(abs(sq[k] / count[k] - m * m), 1.0);
                if (v < minVariance)
                {
                    minVariance = v;
                    result = m;
                }
            }
            return float4(result, 1);
        }

        // ---------- 5, 6: Anisotropic Kuwahara ----------
        // Kyprianidis 외 (2010) 다항식 가중 버전. Blender 컴포지터와 같은 방식이다.
        // 단위 원판 위 고정 격자를 타원으로 늘려서 샘플한다. 그래서 샘플 수가 방향과 무관하게 일정하고,
        // 반복 횟수가 균일 변수로만 정해져 GPU 분기가 화소마다 갈리지 않는다.

        #define SQRT_HALF 0.70710678

        float Square(float x) { return x * x; }

        float4 AnisotropicKuwahara(float2 uv, const int sectors)
        {
            float4 orient = SAMPLE_TEXTURE2D_LOD(_OrientTex, sampler_LinearClamp, uv, 0);
            float anisotropy = saturate(length(orient.xy));
            float2 tangent = TangentFromDoubleAngle(orient.xy);

            float shrink = lerp(1.0, EDGE_MIN_SCALE, saturate(orient.z * _Kuwahara2.w));
            float stretch = 1.0 + _Kuwahara.z * anisotropy;
            float major = _Kuwahara.x * stretch * shrink;
            float minor = _Kuwahara.x / stretch * shrink;
            // 원판 좌표 (x, y)를 uv 오프셋으로 바꾸는 두 축. 넓이는 반지름^2으로 늘 같다.
            float2 axisX = tangent * major * _WorkSize.zw;
            float2 axisY = float2(-tangent.y, tangent.x) * minor * _WorkSize.zw;

            float zeta = _Kuwahara2.x;
            float eta = _Kuwahara2.y;
            int extent = (int)_Kuwahara2.z;
            float invExtent = 1.0 / _Kuwahara2.z;

            float3 center = SampleSource(uv);
            float3 mean[8];
            float3 sq[8];
            float weight[8];
            [unroll]
            for (int k = 0; k < 8; k++)
            {
                mean[k] = center / sectors;
                sq[k] = center * center / sectors;
                weight[k] = 1.0 / sectors;
            }

            // 원판은 점대칭이라 위쪽 반만 돌고 +d 와 -d 를 같이 읽는다.
            [loop]
            for (int j = 0; j <= extent; j++)
            {
                [loop]
                for (int i = -extent; i <= extent; i++)
                {
                    if (j == 0 && i <= 0)
                        continue;
                    float2 d = float2(i, j) * invExtent;
                    float r2 = dot(d, d);
                    if (r2 > 1.0)
                        continue;

                    float w[8];
                    float2 poly = zeta - eta * d * d;
                    if (sectors == 8)
                    {
                        w[0] = Square(max(0.0,  d.y + poly.x));
                        w[2] = Square(max(0.0, -d.x + poly.y));
                        w[4] = Square(max(0.0, -d.y + poly.x));
                        w[6] = Square(max(0.0,  d.x + poly.y));
                        float2 rd = SQRT_HALF * float2(d.x - d.y, d.x + d.y);
                        float2 rpoly = zeta - eta * rd * rd;
                        w[1] = Square(max(0.0,  rd.y + rpoly.x));
                        w[3] = Square(max(0.0, -rd.x + rpoly.y));
                        w[5] = Square(max(0.0, -rd.y + rpoly.x));
                        w[7] = Square(max(0.0,  rd.x + rpoly.y));
                    }
                    else
                    {
                        w[0] = Square(max(0.0,  d.y + poly.x));
                        w[1] = Square(max(0.0, -d.x + poly.y));
                        w[2] = Square(max(0.0, -d.y + poly.x));
                        w[3] = Square(max(0.0,  d.x + poly.y));
                        w[4] = w[5] = w[6] = w[7] = 0;
                    }

                    float wSum = 0;
                    [unroll]
                    for (int ws0 = 0; ws0 < sectors; ws0++)
                        wSum += w[ws0];
                    float g = exp(-PI * r2) / max(wSum, 1e-5);

                    float2 offset = axisX * d.x + axisY * d.y;
                    float3 cu = SampleSource(uv + offset);
                    float3 cl = SampleSource(uv - offset);
                    float3 cu2 = cu * cu;
                    float3 cl2 = cl * cl;

                    [unroll]
                    for (int sa = 0; sa < sectors; sa++)
                    {
                        float ws = w[sa] * g;
                        // -d 의 섹터 sa 가중치는 +d 의 반대편 섹터 가중치와 같다.
                        int o = (sa + sectors / 2) % sectors;
                        mean[sa] += cu * ws;
                        sq[sa] += cu2 * ws;
                        weight[sa] += ws;
                        mean[o] += cl * ws;
                        sq[o] += cl2 * ws;
                        weight[o] += ws;
                    }
                }
            }

            // 분산이 가장 작은 섹터 기준으로 상대 가중치를 매긴다. 1/sigma^q 를 그대로 쓰면 넘친다.
            float sigma[8];
            float minSigma = 1e9;
            [unroll]
            for (int sv = 0; sv < sectors; sv++)
            {
                mean[sv] /= weight[sv];
                float3 variance = abs(sq[sv] / weight[sv] - mean[sv] * mean[sv]);
                sigma[sv] = max(dot(sqrt(variance), 1.0), 0.02);
                minSigma = min(minSigma, sigma[sv]);
            }

            float3 result = 0;
            float total = 0;
            [unroll]
            for (int sb = 0; sb < sectors; sb++)
            {
                float a = pow(minSigma / sigma[sb], _Kuwahara.w);
                result += mean[sb] * a;
                total += a;
            }
            return float4(result / total, 1);
        }

        float4 FragAnisotropic8(Varyings input) : SV_Target { return AnisotropicKuwahara(input.texcoord, 8); }
        float4 FragAnisotropic4(Varyings input) : SV_Target { return AnisotropicKuwahara(input.texcoord, 4); }

        // ---------- 10: 붓질 ----------

        float4 StrokeHash(float2 p)
        {
            float4 p4 = frac(p.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
            p4 += dot(p4, p4.wzxy + 33.33);
            return frac((p4.xxyz + p4.yzzw) * p4.zywx);
        }

        // YIQ 공간에서 색조만 돌린다. 명도는 그대로다.
        float3 HueTurn(float3 c, float angle)
        {
            float y = dot(c, float3(0.299, 0.587, 0.114));
            float i = dot(c, float3(0.596, -0.274, -0.322));
            float q = dot(c, float3(0.211, -0.523, 0.312));
            float s = sin(angle), co = cos(angle);
            float2 iq = float2(i * co - q * s, i * s + q * co);
            return float3(y + 0.956 * iq.x + 0.621 * iq.y, y - 0.272 * iq.x - 0.647 * iq.y, y - 1.106 * iq.x + 1.703 * iq.y);
        }

        // 붓질이 놓이는 방향(부호 없는 단위 벡터, 화면 px 기준).
        // 형태: 깊이 등고선을 따라간다. 머리카락이 머리를 감싸 돌듯 면을 돌아간다.
        // 색: 구조 텐서의 접선. 평평한 색에서는 약해서, 그런 곳은 형태 쪽을 쓴다.
        float2 StrokeTangent(float2 uv)
        {
            float2 step = _SourceSize.zw * _Style3.y * 2.0;
            float depth = SceneEyeDepth(uv);
            float gx = SceneEyeDepth(uv + float2(step.x, 0)) - SceneEyeDepth(uv - float2(step.x, 0));
            float gy = SceneEyeDepth(uv + float2(0, step.y)) - SceneEyeDepth(uv - float2(0, step.y));
            float2 g = float2(gx, gy) / max(depth, 1e-3);
            float formStrength = saturate(length(g) * 400.0);
            float2 alongForm = length(g) > 1e-7 ? normalize(float2(-g.y, g.x)) : float2(1, 0);

            float4 orient = SAMPLE_TEXTURE2D_LOD(_OrientTex, sampler_LinearClamp, uv, 0);
            float2 alongColour = TangentFromDoubleAngle(orient.xy);
            float colourStrength = saturate(length(orient.xy));

            float formWeight = _Strokes2.w * formStrength;
            float colourWeight = (1.0 - _Strokes2.w) * colourStrength;
            if (formWeight + colourWeight < 1e-3)
                return formStrength > colourStrength ? alongForm : alongColour;
            if (dot(alongForm, alongColour) < 0)
                alongColour = -alongColour;
            return normalize(alongForm * formWeight + alongColour * colourWeight);
        }

        float3 StrokeColour(float2 centreUv, float2 tangentUv, float2 id)
        {
            // 붓에 묻은 물감: 중심과 붓질 양쪽의 평균. 가는 선 하나가 붓질 전체를 물들이지 않게 한다.
            float3 mid = SampleSource(centreUv);
            float3 ahead = SampleSource(centreUv + tangentUv);
            float3 behind = SampleSource(centreUv - tangentUv);
            // 붓질 옆으로 반 붓 길이(화소로 돌려 직각을 맞춘다).
            float2 tangentPx = tangentUv * _SourceSize.xy;
            float2 normalUv = float2(-tangentPx.y, tangentPx.x) * _SourceSize.zw * 0.5;
            float3 side = SampleSource(centreUv + normalUv);
            float3 c = (mid * 2.0 + ahead + behind) * 0.25;
            // 피부처럼 넓게 한 색인 곳: 붓질끼리 덜 달라서 한 면으로 녹아든다.
            float variation = max(max(length(ahead - mid), length(behind - mid)), length(side - mid));
            float vary = lerp(1.0 - _Strokes4.z, 1.0, saturate(variation * 8.0));
            float4 h = StrokeHash(id + 37.1);
            float boost = (h.w < _Strokes3.y ? _Strokes3.z : 1.0) * vary;
            c *= 1.0 + (h.x * 2.0 - 1.0) * _Strokes2.x * boost;
            c = HueTurn(c, (h.y * 2.0 - 1.0) * _Strokes2.y * boost);
            float luma = Luminance(c);
            c = lerp(luma.xxx, c, 1.0 + (h.z * 2.0 - 1.0) * _Strokes2.z * boost);
            return saturate(c);
        }

        // 화면(1080p px)과 붓질 배치 공간(캐릭터 위 cm)을 오간다. 배치가 캐릭터를 따라 움직이고 커지고 돌아서,
        // 붓질이 화면에 붙지 않고 캐릭터에 붙어 있다.
        float2 ToStrokeSpace(float2 pixel)
        {
            float2 d = pixel - _StrokeAnchor.xy;
            float2 axisUp = float2(-_StrokeAxis.y, _StrokeAxis.x);
            return float2(dot(d, _StrokeAxis.xy), dot(d, axisUp)) * (100.0 / _StrokeAnchor.z);
        }

        float2 FromStrokeSpace(float2 q)
        {
            float2 axisUp = float2(-_StrokeAxis.y, _StrokeAxis.x);
            return _StrokeAnchor.xy + (q.x * _StrokeAxis.xy + q.y * axisUp) * (_StrokeAnchor.z / 100.0);
        }

        float CharacterMask(float2 uv)
        {
            if (_Strokes4.x < 0.5)
                return 1.0;
            return SAMPLE_TEXTURE2D_LOD(_CharacterMaskTex, sampler_LinearClamp, uv, 0).r;
        }

        float4 FragStrokes(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float2 pixel = uv * _SourceSize.xy / _Style3.y;
            float2 pixelToUv = _Style3.y * _SourceSize.zw;
            float2 q = ToStrokeSpace(pixel);
            float cmToPixel = _StrokeAnchor.z / 100.0;
            float size = _Strokes.x;
            float halfLength = size * _Strokes.y * 0.5;
            float halfWidth = size * _Strokes.z * 0.5;
            // 밖으로 나가는 붓질은 자기 방향으로 이만큼 늘어난다.
            float stretch = max(_Strokes4.y, 1.0);
            float reachSq = (halfLength * 1.4 * stretch) * (halfLength * 1.4 * stretch);
            float depth = SceneEyeDepth(uv);
            float3 under = SampleSource(uv);
            bool inside = CharacterMask(uv) > 0.5;

            float2 cell = floor(q / size);
            float best = -1.0;
            float3 colour = under;
            [loop]
            for (int y = -3; y <= 3; y++)
            {
                [loop]
                for (int x = -3; x <= 3; x++)
                {
                    float2 id = cell + float2(x, y);
                    float4 h = StrokeHash(id);
                    if (h.w > _Strokes3.x)
                        continue;
                    float2 centre = (id + h.xy) * size;
                    float2 d = q - centre;
                    if (dot(d, d) > reachSq)
                        continue;
                    float priority = StrokeHash(id + 91.7).x;
                    if (priority <= best)
                        continue;
                    float2 centreUv = FromStrokeSpace(centre) * pixelToUv;
                    if (any(centreUv < 0.0) || any(centreUv > 1.0))
                        continue;
                    // 붓질은 캐릭터 위에서만 시작한다.
                    if (CharacterMask(centreUv) < 0.5)
                        continue;
                    // 캐릭터 안에서는 다른 면(앞에 놓인 팔 등)에서 시작한 붓질이 넘어오지 않는다.
                    if (inside && abs(SceneEyeDepth(centreUv) - depth) > depth * 0.05)
                        continue;
                    float2 t = StrokeTangent(centreUv);
                    float2 tq = normalize(float2(dot(t, _StrokeAxis.xy), dot(t, float2(-_StrokeAxis.y, _StrokeAxis.x))));
                    float2 n = float2(-tq.y, tq.x);
                    float lengthScale = lerp(0.6, 1.4, h.z);
                    float u = dot(d, tq) / (halfLength * lengthScale);
                    float v = dot(d, n) / halfWidth;
                    // 가장자리를 붓털 자국처럼 들쭉날쭉하게. 잡음도 캐릭터에 붙어 있어서 움직여도 끓지 않는다.
                    float rough = (Noise(q * (24.0 / size) + id * 13.7, 2.5).r - 0.5) * _Strokes.w * 1.5;
                    // 밖에서는 붓질 방향으로만 Spill 만큼 늘인 모양으로 잰다. 1 아래면 붓질 일부만 나간다.
                    if (inside ? u * u + v * v > 1.0 + rough
                               : (u / stretch) * (u / stretch) + v * v > min(_Strokes4.y, 1.0) * (1.0 + rough))
                        continue;
                    best = priority;
                    colour = StrokeColour(centreUv, t * pixelToUv * halfLength * cmToPixel * 0.5, id);
                }
            }

            // 눈, 꽃처럼 붓질과 확연히 다른 가는 자국은 비쳐 보이게 남긴다.
            if (inside && _Strokes3.w > 0)
            {
                float threshold = lerp(1.0, 0.12, _Strokes3.w);
                float differs = smoothstep(threshold, threshold + 0.08, abs(Luminance(under) - Luminance(colour)));
                colour = lerp(colour, under, differs);
            }
            // 알파: 칠한 곳. 캐릭터 밖에서 붓질이 닿지 않은 곳은 합성에서 원래대로 둔다.
            return float4(colour, inside || best >= 0.0 ? 1.0 : 0.0);
        }

        float4 FragCharacterMask(Varyings input) : SV_Target
        {
            return 1;
        }

        // ---------- 7: 수채화 합성 ----------

        float3 SampleFiltered(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_FilteredTex, sampler_LinearClamp, uv, 0).rgb;
        }

        // Bousseau 외 (2006) 안료 밀도 모델. d > 1 이면 진해지고 d < 1 이면 옅어진다. 흰색은 그대로 둔다.
        float3 ApplyDensity(float3 c, float d)
        {
            return c * (1.0 - (1.0 - c) * (d - 1.0));
        }

        float4 FragCopy(Varyings input) : SV_Target
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
        }

        float4 FragComposite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float4 originalSample = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
            float3 original = originalSample.rgb;
            float4 orient = SAMPLE_TEXTURE2D_LOD(_OrientTex, sampler_LinearClamp, uv, 0);
            float edge = saturate(orient.z * _Kuwahara2.w);

            // 해상도가 달라도 무늬 크기가 같도록 1080p 기준 화소 좌표를 쓴다.
            float2 pixel = uv * _SourceSize.xy / _Style3.y;

            // 안료 번짐: 저주파 잡음으로 필터 결과를 읽는 위치를 비튼다.
            // 평탄한 곳은 비틀어도 같은 색이라 색 경계만 불규칙하게 번진다. 실루엣 근처는 줄인다.
            float2 bleedPx = _Style.w * (1.0 - edge) * _SourceSize.zw * _Style3.y;
            float2 warpA = Noise(pixel, 37.0).rg * 2.0 - 1.0;
            float2 warpB = Noise(pixel + 71.0, 13.0).ba * 2.0 - 1.0;
            float3 color = 0.5 * (SampleFiltered(uv + warpA * bleedPx) + SampleFiltered(uv + warpB * bleedPx * 0.5));
            // 필터가 칠한 곳(Kuwahara 는 어디나 1, 붓질은 칠한 곳만 1).
            float painted = SAMPLE_TEXTURE2D_LOD(_FilteredTex, sampler_LinearClamp, uv, 0).a;

            color = lerp(original, color, _Style.x);
            // 실루엣, 눈, 가는 가지를 읽을 수 있게 경계에서 원본을 조금 되살린다.
            color = lerp(color, original, edge * 0.35);
            color = saturate(color);

            // 색 단순화: 명도를 부드러운 계단으로 묶는다. 계단 사이를 smoothstep 으로 이어 움직일 때 띠가 튀지 않는다.
            float simplify = _Style.y;
            if (simplify > 0)
            {
                float luma = Luminance(color);
                float x = luma * 5.0;
                float banded = (floor(x) + smoothstep(0.3, 0.7, frac(x))) / 5.0;
                float3 simplified = color * (banded / max(luma, 1e-4));
                // 묶는 과정에서 탁해지지 않게 채도를 조금 올린다.
                simplified = lerp(Luminance(simplified).xxx, simplified, 1.15);
                color = lerp(color, saturate(simplified), simplify);
            }

            // 색 경계의 기울기. 수채화에서 안료가 고이는 가장자리를 찾는다.
            float2 wt = _WorkSize.zw;
            float3 gx = SampleFiltered(uv + float2(wt.x, 0)) - SampleFiltered(uv - float2(wt.x, 0));
            float3 gy = SampleFiltered(uv + float2(0, wt.y)) - SampleFiltered(uv - float2(0, wt.y));
            float boundary = saturate((length(gx) + length(gy)) * 2.0);

            // 붓결: 접선 방향으로 잡음을 적분(LIC)해 결이 물체 윤곽을 따라가게 한다.
            float anisotropy = saturate(length(orient.xy));
            float2 tangent = TangentFromDoubleAngle(orient.xy);
            float brush = 0;
            [unroll]
            for (int k = -3; k <= 3; k++)
                brush += Noise(pixel + tangent * (k * 4.0), 4.0).r;
            brush = (brush / 7.0 - 0.5) * 2.5;

            float paper;
            if (_Style3.x > 0.5)
                paper = SAMPLE_TEXTURE2D_LOD(_PaperTex, sampler_LinearRepeat, pixel / (512.0 * _Style2.z), 0).r;
            else
                // 냉압지처럼 4~6 px 크기의 둥근 요철과 그보다 큰 얼룩을 섞는다. 더 잘면 필름 그레인처럼 보인다.
                paper = 0.6 * Noise(pixel, 5.0 * _Style2.z).g + 0.4 * Noise(pixel + 37.0, 14.0 * _Style2.z).b;

            // 세 옥타브를 한 번씩 읽어 r 은 안료 밀도, a 는 채도 얼룩에 쓴다.
            float4 lowFreq = Noise(pixel, 300.0) * 0.55 + Noise(pixel + 17.0, 150.0) * 0.3 + Noise(pixel + 41.0, 75.0) * 0.15;
            float pigment = lowFreq.r * 2.0 - 1.0;
            float saturationNoise = lowFreq.a * 2.0 - 1.0;

            float density = 1.0;
            density += _Style.z * 0.35 * pigment;
            density += _Style2.x * 0.6 * boundary;
            density += _Style2.w * 0.5 * anisotropy * brush;
            // 종이의 패인 곳(어두운 값)에 안료가 더 고인다.
            density += _Style2.y * 0.4 * (0.5 - paper) * 2.0;
            color = ApplyDensity(color, clamp(density, 0.0, 2.0));

            color = lerp(Luminance(color).xxx, color, 1.0 + _Style.z * 0.3 * saturationNoise);
            color *= 1.0 - _Style2.y * 0.06 * (1.0 - paper);

            return float4(lerp(original, saturate(color), painted), originalSample.a);
        }
        ENDHLSL

        Pass
        {
            Name "Tensor"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragTensor
            ENDHLSL
        }
        Pass
        {
            Name "TensorNormals"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragTensorNormals
            ENDHLSL
        }
        Pass
        {
            Name "BlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            ENDHLSL
        }
        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            ENDHLSL
        }
        Pass
        {
            Name "Classic"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragClassic
            ENDHLSL
        }
        Pass
        {
            Name "Anisotropic8"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAnisotropic8
            ENDHLSL
        }
        Pass
        {
            Name "Anisotropic4"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAnisotropic4
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
        Pass
        {
            Name "Copy"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy
            ENDHLSL
        }
        Pass
        {
            Name "CompositeCharacters"
            Stencil { Ref 128 ReadMask 128 Comp Equal }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
        Pass
        {
            Name "Strokes"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragStrokes
            ENDHLSL
        }
        Pass
        {
            Name "CharacterMask"
            Stencil { Ref 128 ReadMask 128 Comp Equal }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCharacterMask
            ENDHLSL
        }
    }
}
