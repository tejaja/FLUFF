// プレイヤーの足元のうそ影を「地面のシェーダー自身」が描く（2026-11-28 てじゃ「アバターに乗らないようにできないの？」）。
// 以前は BlobDecal（深度から地面を探して塗る箱）だったが、アバターも深度に入るので靴に影が乗り、段差や遠くで欠けていた。
// FuwaPlayerShadows が毎フレーム、近いプレイヤー16人まで「足元の地面の位置・影の半径・濃さ」を全シェーダー共通の値で渡す。
//  _UdonFuwaPB[k]  = (x, 地面の高さ, z, 半径)
//  _UdonFuwaPBA[k] = 濃さ（0〜1、高く飛ぶほど薄い）
//  _UdonFuwaPBCount = 人数（0 なら何もしない）
//  _UdonFuwaPBColor = 影の色、_UdonFuwaPBParam = (薄くし始める所, カーブ, 上の段の許し m, 下の段の許し m)
// スマホ・Quest はプレイヤーの影なし（人数が 0 のまま）。
#ifndef FUWA_PLAYER_BLOB_INCLUDED
#define FUWA_PLAYER_BLOB_INCLUDED

float4 _UdonFuwaPB[16];
float _UdonFuwaPBA[16];
float _UdonFuwaPBCount;
float4 _UdonFuwaPBColor;
float4 _UdonFuwaPBParam;

// 影の濃さ（0〜1）。wn は世界の法線（上向きの面だけ）
half FuwaPlayerBlob(float3 wp, float3 wn)
{
    half s = 0;
    #if !defined(SHADER_API_MOBILE)
    int n = (int)min(_UdonFuwaPBCount, 16);
    if (n <= 0) return 0;
    half up = smoothstep(0.55, 0.85, wn.y);
    if (up <= 0) return 0;
    float upTol = _UdonFuwaPBParam.z, downTol = _UdonFuwaPBParam.w;
    [loop] for (int k = 0; k < 16; k++)
    {
        if (k >= n) break;
        float4 p = _UdonFuwaPB[k];
        float2 d = (wp.xz - p.xz) / max(p.w, 1e-3);
        float r = dot(d, d);
        if (r >= 1) continue;
        r = sqrt(r);
        half a = pow(1 - smoothstep(_UdonFuwaPBParam.x, 1, r), _UdonFuwaPBParam.y);
        // 足元の地面から上は upTol まで（小さい段差の上の段）、下は downTol まで（段差の下の段）。それより離れた面（上の階・下の階）には落とさない
        float dy = wp.y - p.y;
        a *= (1 - smoothstep(upTol * 0.7, upTol, dy)) * smoothstep(-downTol, -downTol * 0.7, dy);
        a *= _UdonFuwaPBA[k];
        s = 1 - (1 - s) * (1 - a);
    }
    s *= up;
    #endif
    return s;
}

// ふつうの描き方（ForwardBase）：影の色へ寄せる。ForwardAdd（点光源の足し算）は暗くするだけ
#define FUWA_APPLY_PLAYER_BLOB(c, wp, wn) { half _pbs = FuwaPlayerBlob(wp, wn); c = lerp(c, _UdonFuwaPBColor.rgb, _pbs); }
#define FUWA_APPLY_PLAYER_BLOB_ADD(c, wp, wn) { c *= 1 - FuwaPlayerBlob(wp, wn); }

#endif
