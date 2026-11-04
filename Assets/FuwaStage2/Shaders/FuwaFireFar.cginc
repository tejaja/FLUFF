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
// 炎・火の玉（2026-11-04 てじゃ「色自体かえるか、遠くのはちょっと色を濃くしてみよう」）：近くの炎が目立つように、遠いほど濃い赤へ。
//  溶岩より近くから効かせる（FUWA_FLAME_DEEP_NEAR m から、END m でいちばん濃い）。いちばん遠くは前の FuwaFireCalm と同じくらいなじませる
#define FUWA_FLAME_DEEP_NEAR 6.0
#define FUWA_FLAME_DEEP_END 30.0
inline half3 FuwaFlameDeep(half3 c, float3 wp)
{
    float d = distance(_WorldSpaceCameraPos, wp);
    half deep = smoothstep(FUWA_FLAME_DEEP_NEAR, FUWA_FLAME_DEEP_END, d);
    c = lerp(c, c * half3(0.9, 0.55, 0.45), deep * 0.75);   // 明るさを落として赤寄りに（色の段差は残す）
    return FuwaFireCalm(c, FuwaFireFar(wp));
}
// 溶岩は炎より赤く（2026-10-31 てじゃ「溶岩もっと赤っぽく」）
inline half3 FuwaLavaCalm(half3 c, half far)
{
    c = lerp(c, half3(0.6, 0.07, 0.04), far * 0.8);
    return lerp(c, unity_FogColor.rgb, far * 0.12);
}
// 遠くの炎・火の玉を薄く（2026-11-04 てじゃ「炎重なってるとこ全般」「火球も」）。不透明のまま、細かい網目で間引く（並び順の問題が出ない）。
// _FireFadeMin が 1 なら何もしない。_FireFadeNear m から薄くなって _FireFadeFar m で _FireFadeMin の濃さ
float _FireFadeNear, _FireFadeFar, _FireFadeMin;
inline void FuwaFireFadeClip(float3 wp, float4 svpos)
{
    if (_FireFadeMin >= 0.999) return;
    half a = lerp(1, _FireFadeMin, smoothstep(_FireFadeNear, _FireFadeFar, distance(_WorldSpaceCameraPos, wp)));
    // 4x4 のベイヤーを計算で（配列は Quest/PICO の GLES で壊れやすいので使わない）
    float2 c = fmod(floor(svpos.xy), 4.0), lo = fmod(c, 2.0), hi = floor(c * 0.5);
    float bayer = 4.0 * (2.0 * abs(lo.x - lo.y) + lo.y) + (2.0 * abs(hi.x - hi.y) + hi.y);
    clip(a - (bayer + 0.5) / 16.0);
}
// 近くの炎だけ、ふちがゆっくり明滅する（2026-11-04 てじゃ「近い炎だけ目立たせる」「ステージ１の近いクモの巣が光るのはよかった」）。rim：ふちほど 1
half4 _NearGlow; float _GlowNear, _GlowFar, _GlowSpeed;
inline half3 FuwaFireNearGlow(half3 c, float3 wp, half rim)
{
    if (_NearGlow.a < 0.001) return c;
    float near = 1 - smoothstep(_GlowNear, max(_GlowFar, _GlowNear + 0.01), distance(_WorldSpaceCameraPos, wp));
    half pulse = 0.5 + 0.5 * sin(_Time.y * _GlowSpeed);
    // ふちに寄せる（面全体だと白っぽく色があせて見えた）
    return lerp(c, _NearGlow.rgb, saturate(near * pulse * _NearGlow.a * (0.08 + 0.92 * rim * rim)));
}
#endif
