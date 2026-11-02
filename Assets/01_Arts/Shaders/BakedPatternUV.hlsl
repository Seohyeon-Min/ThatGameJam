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

// 인스턴스마다 다른 0~1 값 세 개(같은 인스턴스는 늘 같다). 0 = 굽기를 공유하지 않는 모델이라 0.5(변화 없음).
float3 InstanceRandom()
{
    uint variation = unity_RendererUserValue & 255u;
    if (variation == 0u)
        return 0.5;
    return frac(float3(variation * 0.618034, variation * 0.754878, variation * 0.569840) + 0.31);
}

// 인스턴스마다 하얗게 바랜 정도(0 = 본색, 1 = 흰색). WatercolorTint 가 셰이더 사용자 값의 다음 8비트로 준다.
half InstanceWhiten()
{
    return ((unity_RendererUserValue >> 8) & 255u) / 255.0;
}

// 인스턴스마다 색상을 돌리는 양(한 바퀴 = 1). 그다음 8비트의 부호 있는 값(-127~127)이 -0.25~0.25 바퀴다(WatercolorTint.MostHueShift).
half InstanceHueShift()
{
    int shift = (int)((unity_RendererUserValue >> 16) & 255u);
    if (shift > 127)
        shift -= 256;
    return shift / 127.0 * 0.25;
}

#endif
