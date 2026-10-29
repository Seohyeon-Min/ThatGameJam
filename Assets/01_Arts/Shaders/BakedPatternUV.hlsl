#ifndef THATGAMEJAM_BAKED_PATTERN_UV_INCLUDED
#define THATGAMEJAM_BAKED_PATTERN_UV_INCLUDED

// 같은 프리팹의 인스턴스는 굽기 결과(메시, 3D 무늬, 머티리얼)를 하나 같이 쓴다. 그래도 서로 달라 보이도록 인스턴스마다
// 무늬 좌표를 뒤집고 민다. 인스턴스 번호(1~255, 0 = 그대로)는 WatercolorPaintedModel 이 렌더러의 셰이더 사용자 값
// (SetShaderUserValue)으로 준다. 머티리얼이 같아서 SRP Batcher 가 그대로 묶고, 위치와 상관없어서 움직여도 무늬가 미끄러지지 않는다.
// 3D 텍스처는 Mirror 로 감싸서 밀어도 경계가 끊기지 않는다. 위아래(y)는 그대로 둔다.
float3 TwistPatternUV(float3 uv)
{
    uint variation = unity_RendererUserValue & 255u;
    if (variation == 0u)
        return uv;
    if (variation & 1u)
        uv.x = 1.0 - uv.x;
    if (variation & 2u)
        uv.z = 1.0 - uv.z;
    uv.xz += frac(float2(variation * 0.618034, variation * 0.754878)) - 0.5;
    return uv;
}

#endif
