// Vketブース版のふわふわの顔（元は FLUFF の FuwaFurFace.cginc）。FurOnePass の最後のパスとして、毛を全部描いたあとに描く
// 顔は毛先のちょっと外側の球面に描くので毛に隠れない。手前の別の物にはふつうに隠れる（2026-10-30 てじゃ「Vketブースもかえないとな」）
// 層を重ねたメッシュ（uv4.x＝層の高さ）のうち、いちばん外の層（1）だけを毛先の外 (1+_FaceLift) に出して使い、ほかの層はつぶして描かない
float _FaceOn;      // 0=顔なし
float4 _FaceFwd;    // 顔の向き（この物のローカルの向き。ブースは左右反転があるので行列ごとかける）
float _FaceTilt;    // 顔の傾き（度、顔の向きまわり）
float _FaceSize;    // 顔の広さ（玉の中心から見た半分の角度のsin）
float _FaceLift;    // 毛先からさらに外へ浮かせる量（毛の長さに対する割合）。描く板の位置（毛に隠れないため）
float _FaceDepth;   // 顔の絵が乗って見える球の大きさ（毛の長さに対する割合）。見た目の毛玉のふくらみに合わせる
float _FaceStyle;   // 目の形（下の fragFace を見て）
float _FaceCheekSize;
float _FaceHit;     // 1=撃たれた顔 ＞＜（ブースは撃たれた瞬間なので固定で 1）
fixed4 _FaceEyeColor;
fixed4 _FaceCheekColor;

#define FUR_ONE_PASS
#include "FuwaFur.cginc"

v2f vertFace (appdata v)
{
    if (v.uv4.x < 0.999 || _FaceOn < 0.5)
    {
        // いちばん外以外の層は1点につぶす（面積ゼロで描かれない）
        v2f o = (v2f)0;
        o.pos = float4(0, 0, -2, 1);
        return o;
    }
    v.uv4.x = 1.0 + _FaceLift;
    return vert(v);
}

// 線分 a-b までの距離
float sdSeg(float2 p, float2 a, float2 b)
{
    float2 pa = p - a, ba = b - a;
    return length(pa - ba * saturate(dot(pa, ba) / dot(ba, ba)));
}

// 角を丸めた縦長の棒（カプセル）までの距離
float sdCapsuleY(float2 p, float halfLen, float r)
{
    p.y -= clamp(p.y, -halfLen, halfLen);
    return length(p) - r;
}

fixed4 fragFace (v2f i) : SV_Target
{
    if (_FaceOn < 0.5) discard;
    float3 center = unity_ObjectToWorld._m03_m13_m23;
    // 視線と内側の球（見た目の毛玉のふくらみ）の交点の向きで絵を決める
    float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
    float rf = scale * (0.5 + _FurLength * _FaceDepth);
    float3 ro = _WorldSpaceCameraPos;
    float3 rdw = normalize(i.worldPos - ro);
    float3 oc = ro - center, rd = rdw;
    // ポヨン（_Squash）でつぶれている分は、視線をつぶす前の形の空間に戻してから球と交わらせる（つぶれた向きに顔が玉からはみ出さないように）
    float sw = _Squash.w;
    if (_SquashWobble.x > 0) { float wt = _Time.y * _SquashWobble.y; sw += _SquashWobble.x * (sin(wt) * 0.75 + sin(wt * 1.93 + 0.7) * 0.25); }
    if (abs(sw) > 0.0001)
    {
        float3 sqDir = mul((float3x3)unity_ObjectToWorld, _Squash.xyz); sqDir = dot(sqDir, sqDir) > 1e-8 ? normalize(sqDir) : float3(0, 1, 0);
        float ka = 1.0 / max(1.0 - sw, 0.2), ks = 1.0 / (1.0 + sw * 0.5);
        float a1 = dot(oc, sqDir); oc = sqDir * a1 * ka + (oc - sqDir * a1) * ks;
        float a2 = dot(rd, sqDir); rd = sqDir * a2 * ka + (rd - sqDir * a2) * ks;
        rd = normalize(rd);
    }
    float bq = dot(oc, rd);
    float disc = bq * bq - (dot(oc, oc) - rf * rf);
    if (disc <= 0) discard;
    float3 od = normalize(oc + rd * (-bq - sqrt(disc)));

    // 顔の向き(F)と上(U)：上はワールドの上、そこから _FaceTilt だけ顔の向きまわりに傾ける
    float3 F = normalize(mul((float3x3)unity_ObjectToWorld, _FaceFwd.xyz));
    float3 up = abs(F.y) > 0.97 ? float3(0, 0, 1) : float3(0, 1, 0);
    float3 U = normalize(up - F * dot(up, F));
    float3 R = cross(U, -F);
    float ta = radians(_FaceTilt), tc = cos(ta), ts = sin(ta);
    float3 U2 = U * tc - R * ts; R = R * tc + U * ts; U = U2;
    float facing = dot(od, F);
    if (facing <= 0) discard;
    float2 uv = float2(dot(od, R), dot(od, U)) / _FaceSize;   // 顔の真ん中=(0,0)、ふち=±1
    if (dot(uv, uv) > 1.6) discard;

    // 目（_FaceStyle）：0=たて線 1=にっこり閉じ目（^ ^） 2=点 3=点＋小さい口。ほっぺ：横長のだ円（大きさ _FaceCheekSize）
    float2 pe = float2(abs(uv.x) - 0.30, uv.y - 0.10);
    float dEye;
    if (_FaceStyle < 0.5) dEye = sdCapsuleY(pe, 0.11, 0.06);
    else if (_FaceStyle < 1.5)
    {
        float2 q = pe - float2(0, -0.05);
        dEye = q.y >= 0 ? abs(length(q) - 0.12) - 0.035 : length(float2(abs(q.x) - 0.12, q.y)) - 0.035;
    }
    else dEye = length(pe) - 0.07;
    if (_FaceStyle > 2.5)
    {
        const float r = 0.17, ma = 0.56;
        float2 m = uv - float2(0, -0.11 + r);
        float2 endp = float2(sin(ma), -cos(ma)) * r;
        float dMouth = (m.y < 0 && abs(m.x) <= -m.y * tan(ma)) ? abs(length(m) - r) - 0.026 : length(float2(abs(m.x), m.y) - endp) - 0.026;
        dEye = min(dEye, dMouth);
    }
    float ck = max(_FaceCheekSize, 0.01);
    if (_FaceHit > 0.5)
    {
        // 撃たれた顔：ぎゅっと閉じた目（真ん中向きの「＞ ＜」）＋への字口
        float2 q = float2(abs(uv.x) - 0.30, uv.y - 0.10);   // +x=外側
        float dE = min(sdSeg(q, float2(0.09, 0.075), float2(-0.07, 0.0)), sdSeg(q, float2(-0.07, 0.0), float2(0.09, -0.075))) - 0.032;
        const float hr = 0.17, hma = 0.56;
        float2 hm = uv - float2(0, -0.09 - hr);
        float2 hend = float2(sin(hma), cos(hma)) * hr;
        float dM = (hm.y > 0 && abs(hm.x) <= hm.y * tan(hma)) ? abs(length(hm) - hr) - 0.028 : length(float2(abs(hm.x), hm.y) - hend) - 0.028;
        dEye = min(dE, dM);
    }
    float2 pc = float2(abs(uv.x) - 0.56, uv.y + 0.16) / (float2(0.17, 0.11) * ck);
    float dCheek = _FaceCheekSize > 0.01 ? (length(pc) - 1.0) * 0.11 * ck : 1.0;

    float aa = max(fwidth(uv.x), 1e-4) * 0.75;
    float eyeA = 1.0 - smoothstep(-aa, aa, dEye);
    float cheekA = (1.0 - smoothstep(-aa * 1.5, aa * 1.5, dCheek)) * _FaceCheekColor.a;
    // 玉のふち（見た目の輪郭）に回り込むほど薄く
    float edge = smoothstep(0.12, 0.35, dot(od, -rd));

    float3 col = lerp(_FaceCheekColor.rgb, _FaceEyeColor.rgb, eyeA);
    float a = saturate(max(eyeA * _FaceEyeColor.a, cheekA)) * edge;
    if (a < 0.003) discard;

    // 明るさは毛と同じ（ブースの固定の太陽と空の色）
    float ndl = saturate(dot(od, FUWA_BOOTH_SUN_DIR) * 0.6 + 0.4);
    col *= saturate(ndl * FUWA_BOOTH_SUN_COLOR + FuwaBoothAmbient(od));
    return fixed4(col, a);
}
