// 足元のうそ影を「地面のシェーダー自身」が描く（2026-11-28 てじゃ「アバターに乗らないようにはできないの？」「ふわふわもこれつかうのはよくない？」）。
// 以前は BlobDecal（深度から地面を探して塗る箱）だったが、アバターも深度に入るので靴に影が乗り、段差や遠くで欠けていた。
// スクリプトが毎フレーム、影の「足元の地面の位置・半径・向き・濃さ」を全シェーダー共通の値で渡す。種類ごとに別の値：
//  プレイヤー（FuwaPlayerShadows）  _UdonFuwaPB[16] / _UdonFuwaPQ[16] / _UdonFuwaPBCount
//  同期ふわふわ（FuwaSharedBallShadows） _UdonFuwaBB[16] / _UdonFuwaBQ[16] / _UdonFuwaBBCount
//  自分のふわふわ（FuwaGroundMarker）・自分の銃（FuwaGunShadows）  _UdonFuwaLB/_UdonFuwaLQ、_UdonFuwaGB/_UdonFuwaGQ（1個ずつ、濃さ0で無し）
//  B = (x, 地面の高さ, z, 横の半径)、Q = (前の向き x, z, 前後の長さ/横幅, 濃さ)
//  色：_UdonFuwaPBColor（プレイヤー）・_UdonFuwaBBColor（ほか）、ぼかし：_UdonFuwaPBParam・_UdonFuwaBBParam = (薄くし始める所, カーブ, 上の段の許し m, 下の段の許し m)
// スマホ・Quest は今までの BlobDecal の深度なし版のまま（ここは何もしない）。
#ifndef FUWA_PLAYER_BLOB_INCLUDED
#define FUWA_PLAYER_BLOB_INCLUDED

float4 _UdonFuwaPB[16];
float4 _UdonFuwaPQ[16];
float _UdonFuwaPBCount;
float4 _UdonFuwaBB[16];
float4 _UdonFuwaBQ[16];
float _UdonFuwaBBCount;
float4 _UdonFuwaLB, _UdonFuwaLQ, _UdonFuwaGB, _UdonFuwaGQ;
float4 _UdonFuwaPBColor, _UdonFuwaBBColor;
float4 _UdonFuwaPBParam, _UdonFuwaBBParam;

half FuwaBlobOne(float3 wp, float4 b, float4 q, float4 prm)
{
    if (q.w <= 0) return 0;
    float2 dxz = wp.xz - b.xz;
    float2 f = q.xy;
    float2 d = float2(dxz.x * f.y - dxz.y * f.x, dot(dxz, f) / max(q.z, 1e-3)) / max(b.w, 1e-3);
    float r = dot(d, d);
    if (r >= 1) return 0;
    r = sqrt(r);
    half a = pow(1 - smoothstep(prm.x, 1, r), prm.y);
    // 足元の地面から上は prm.z まで（小さい段差の上の段）、下は prm.w まで（段差の下の段）。それより離れた面（上の階・下の階）には落とさない
    float dy = wp.y - b.y;
    a *= (1 - smoothstep(prm.z * 0.7, prm.z, dy)) * smoothstep(-prm.w, -prm.w * 0.7, dy);
    return a * q.w;
}

// 影の色へ寄せる
float3 FuwaBlobApply(float3 c, float3 wp, float3 wn, bool add)
{
    #if !defined(SHADER_API_MOBILE)
    half up = smoothstep(0.55, 0.85, wn.y);
    if (up <= 0) return c;
    // プレイヤー
    half sp = 0;
    int n = (int)min(_UdonFuwaPBCount, 16);
    [loop] for (int k = 0; k < 16; k++)
    {
        if (k >= n) break;
        half a = FuwaBlobOne(wp, _UdonFuwaPB[k], _UdonFuwaPQ[k], _UdonFuwaPBParam);
        sp = 1 - (1 - sp) * (1 - a);
    }
    // ふわふわ・銃
    half sb = 0;
    n = (int)min(_UdonFuwaBBCount, 16);
    [loop] for (int j = 0; j < 16; j++)
    {
        if (j >= n) break;
        half a = FuwaBlobOne(wp, _UdonFuwaBB[j], _UdonFuwaBQ[j], _UdonFuwaBBParam);
        sb = 1 - (1 - sb) * (1 - a);
    }
    sb = 1 - (1 - sb) * (1 - FuwaBlobOne(wp, _UdonFuwaLB, _UdonFuwaLQ, _UdonFuwaBBParam));
    sb = 1 - (1 - sb) * (1 - FuwaBlobOne(wp, _UdonFuwaGB, _UdonFuwaGQ, _UdonFuwaBBParam));
    sp *= up; sb *= up;
    if (add) return c * (1 - sp) * (1 - sb);   // 点光源の足し算のパスは暗くするだけ
    c = lerp(c, _UdonFuwaPBColor.rgb, sp);
    c = lerp(c, _UdonFuwaBBColor.rgb, sb);
    #endif
    return c;
}

#define FUWA_APPLY_PLAYER_BLOB(c, wp, wn) { c = FuwaBlobApply(c, wp, wn, false); }
#define FUWA_APPLY_PLAYER_BLOB_ADD(c, wp, wn) { c = FuwaBlobApply(c, wp, wn, true); }

#endif
