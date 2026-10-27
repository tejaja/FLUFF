// ステージ2の水のシェーダーで共通の計算（2026-10-27）。
// 水源の池の水面（FuwaStage2/Water）と、その先の滝の上の平らな所（FuwaStage2/Waterfall の _PondTop）で同じ式を使って、
// 波紋・きらめきが境目をまたいでそのまま続くようにする（てじゃ「滝と水面の境目、もっときっちり繋ぐ方法ってある？」）
#ifndef FUWA_STAGE2_WATER_COMMON
#define FUWA_STAGE2_WATER_COMMON

float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float Noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
}
// x の向きに per マスでくり返すノイズ（角度でつかう。ぐるっと一周しても継ぎ目が出ない）
float NoiseWrapX(float2 p, float per)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3 - 2 * f);
    float i0 = fmod(i.x + per * 100, per), i1 = fmod(i.x + 1 + per * 100, per);
    return lerp(lerp(Hash(float2(i0, i.y)), Hash(float2(i1, i.y)), u.x), lerp(Hash(float2(i0, i.y + 1)), Hash(float2(i1, i.y + 1)), u.x), u.y);
}
// 湧き水（center、ワールドの xz）から外へ広がっていく波の輪（ところどころ切れる）。0〜1
float RadialRipple(float2 wxz, float2 center, float speed)
{
    float2 dv = wxz - center;
    float r = length(dv);
    float ang = (atan2(dv.y, dv.x) / 6.2831853 + 0.5) * 24.0;   // 0〜24 のマス
    float t = _Time.y * speed;
    float wob = (NoiseWrapX(float2(ang * 0.5, r * 0.6), 12.0) - 0.5) * 0.35;
    float ph = frac((r + wob) * 0.85 - t * 0.85);
    float ring = smoothstep(0.0, 0.04, ph) * (1 - smoothstep(0.04, 0.11, ph));
    ring *= smoothstep(0.2, 0.4, NoiseWrapX(float2(ang, r * 0.5 - t * 0.6), 24.0));
    ring *= smoothstep(0.25, 0.9, r);   // まん中（湧き水のふくらみ）は出さない
    return saturate(ring);
}
// 水面のきらめき（ワールドの xz の 20cm マスごとに、たまにきらっと）。0〜1
float WaterSparkle(float2 wxz)
{
    float2 cell = floor(wxz * 5.0);
    float h = Hash(cell);
    float tw = saturate(sin(_Time.y * (1.5 + h * 2.0) + h * 40.0) * 8.0 - 7.0);
    float2 f = frac(wxz * 5.0) - 0.5;
    return tw * (1 - smoothstep(0.05, 0.2, length(f))) * step(0.65, h);
}

#endif
