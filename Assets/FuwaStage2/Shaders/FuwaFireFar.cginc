// 2–2 の炎・火の玉・溶岩の遠くの見え方（2026-10-31 てじゃ「火のエフェクト全般、遠目で見た時に目立ちすぎ」）。
// 自分で光るので遠くでも明るいまま浮いて見えた。遠いほど色の段差をなくして、落ち着いた色と霧の色に寄せる。近くは変えない
#ifndef FUWA_FIRE_FAR_INCLUDED
#define FUWA_FIRE_FAR_INCLUDED
#define FUWA_FIRE_FAR_NEAR 18.0
#define FUWA_FIRE_FAR_END 70.0
inline half FuwaFireFar(float3 wp) { return smoothstep(FUWA_FIRE_FAR_NEAR, FUWA_FIRE_FAR_END, distance(_WorldSpaceCameraPos, wp)); }
inline half3 FuwaFireCalm(half3 c, half far)
{
    c = lerp(c, half3(0.72, 0.3, 0.16), far * 0.8);
    return lerp(c, unity_FogColor.rgb, far * 0.12);
}
#endif
