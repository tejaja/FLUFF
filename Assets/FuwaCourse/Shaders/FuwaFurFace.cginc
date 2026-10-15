// ふわふわの顔（お日さま顔：たて線の目2つ＋ほっぺ）。毛玉シェーダーの最後のパスとして、毛を全部描いたあとに描く
// 顔は毛先のちょっと外側の球面に描くので毛に隠れない。手前の別の物にはふつうに隠れる
float _FaceOn;      // 0=顔なし
float _FaceMode;    // 0=いつもこっちを向く、1=玉にくっついて一緒に回る（オブジェクトの+Zが顔の向き）
float _FaceSize;    // 顔の広さ（玉の中心から見た半分の角度のsin）
float _FaceLift;    // 毛先からさらに外へ浮かせる量（毛の長さに対する割合）。描く板の位置（毛に隠れないため）
float _FaceDepth;   // 顔の絵が乗って見える球の大きさ（毛の長さに対する割合）。見た目の毛玉のふくらみに合わせる
float _FaceStyle;    // 目の形（下の fragFace を見て）
float _FaceCheekSize;
fixed4 _FaceEyeColor;
fixed4 _FaceCheekColor;

// 毛なし版（FuwaFurPlain）は先に FUR_H を決めてから include する
#ifndef FUR_H
#define FUR_H (1.0 + _FaceLift)
#endif
#include "FuwaFur.cginc"

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
    // 描く板は毛先の外側だけど、絵は見た目の毛玉のふくらみ（内側の球）に貼ってあるように見せる：
    // 視線と内側の球の交点を求めて、そこの向きで絵を決める（外側の球にそのまま描くと、回った時に顔が玉からはみ出して見える）
    float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
    float rf = scale * (0.5 + _FurLength * _FaceDepth);
    float3 ro = _WorldSpaceCameraPos;
    float3 rd = normalize(i.worldPos - ro);
    float3 oc = ro - center;
    float bq = dot(oc, rd);
    float disc = bq * bq - (dot(oc, oc) - rf * rf);
    if (disc <= 0) discard;
    float3 od = normalize(oc + rd * (-bq - sqrt(disc)));

    // 顔の向き(F)と上(U)
    float3 F, U;
    if (_FaceMode < 0.5)
    {
        // VRでは左右の目の真ん中から見た向きにする（目ごとに顔がずれないように）
        #if defined(USING_STEREO_MATRICES)
        float3 cam = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
        #else
        float3 cam = _WorldSpaceCameraPos;
        #endif
        F = normalize(cam - center);
        // 上はワールドの上（頭をかしげても顔は傾かない）。真上・真下から見た時だけカメラの上を使う
        float3 up = abs(F.y) > 0.97 ? normalize(UNITY_MATRIX_V[1].xyz) : float3(0, 1, 0);
        U = normalize(up - F * dot(up, F));
    }
    else
    {
        F = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));
        float3 up = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
        U = normalize(up - F * dot(up, F));
    }
    float facing = dot(od, F);
    if (facing <= 0) discard;
    float3 R = cross(U, -F);
    float2 uv = float2(dot(od, R), dot(od, U)) / _FaceSize;   // 顔の真ん中=(0,0)、ふち=±1
    if (dot(uv, uv) > 1.6) discard;

    // 目（_FaceStyle）：0=たて線 1=にっこり閉じ目（^ ^） 2=点 3=点＋小さい口。ほっぺ：横長のだ円（大きさ _FaceCheekSize）
    float2 pe = float2(abs(uv.x) - 0.30, uv.y - 0.10);
    float dEye;
    if (_FaceStyle < 0.5) dEye = sdCapsuleY(pe, 0.11, 0.06);
    else if (_FaceStyle < 1.5)
    {
        // 上に丸い弧（中心を少し下げた円の上半分）。端は丸く
        float2 q = pe - float2(0, -0.05);
        dEye = q.y >= 0 ? abs(length(q) - 0.12) - 0.035 : length(float2(abs(q.x) - 0.12, q.y)) - 0.035;
    }
    else dEye = length(pe) - 0.07;
    if (_FaceStyle > 2.5)
    {
        // 小さいにっこり口：大きい円の下の方だけを使ったゆるやかな弧（2026-10-15 てじゃ「曲線をもうちょいゆるやかに」）。
        // 円の半径 r、使う範囲は真下から左右 ma ラジアン。いちばん下は y=-0.11、幅は前の口（半円、半径0.09）とほぼ同じ
        const float r = 0.17, ma = 0.56;
        float2 m = uv - float2(0, -0.11 + r);
        float2 endp = float2(sin(ma), -cos(ma)) * r;
        float dMouth = (m.y < 0 && abs(m.x) <= -m.y * tan(ma)) ? abs(length(m) - r) - 0.026 : length(float2(abs(m.x), m.y) - endp) - 0.026;
        dEye = min(dEye, dMouth);
    }
    float ck = max(_FaceCheekSize, 0.01);
    float2 pc = float2(abs(uv.x) - 0.56, uv.y + 0.16) / (float2(0.17, 0.11) * ck);
    float dCheek = _FaceCheekSize > 0.01 ? (length(pc) - 1.0) * 0.11 * ck : 1.0;   // 0=ほっぺなし

    float aa = max(fwidth(uv.x), 1e-4) * 0.75;
    float eyeA = 1.0 - smoothstep(-aa, aa, dEye);
    float cheekA = (1.0 - smoothstep(-aa * 1.5, aa * 1.5, dCheek)) * _FaceCheekColor.a;
    // 玉のふち（見た目の輪郭）に回り込むほど薄く
    float edge = smoothstep(0.12, 0.35, dot(od, -rd));

    float3 col = lerp(_FaceCheekColor.rgb, _FaceEyeColor.rgb, eyeA);
    float a = saturate(max(eyeA * _FaceEyeColor.a, cheekA)) * edge;
    if (a < 0.003) discard;

    // 明るさは毛と同じ考え方（暗い場所では顔も暗く）
    float3 l = normalize(_WorldSpaceLightPos0.xyz);
    float ndl = saturate(dot(od, l) * 0.6 + 0.4);
    col *= saturate(ndl * _LightColor0.rgb + ShadeSH9(float4(od, 1)));

    fixed4 c = fixed4(col, a * _Alpha);
    UNITY_APPLY_FOG(i.fogCoord, c);
    return c;
}
