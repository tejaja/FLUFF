// 焼いた影（Hikari のシャドウマスク）を自作シェーダーで使うための共通処理。
// ・太陽は Mixed ライトのまま、明暗（トゥーンの段）はいつも通りリアルタイムで計算する
// ・動かない物（ライトマップが付いた物）は、影をシャドウマスク（焼いた影）から取る。まわりの明るさはライトマップ（焼いた間接光）
// ・それ以外（動く物）は今まで通り（リアルタイムの影＋ShadeSH9）
// 使い方：appdata に float2 uv1 : TEXCOORD1、v2f に FUWA_BAKED_COORDS(n)、vert で FUWA_BAKED_TRANSFER(o, v)
#ifndef FUWA_BAKED_INCLUDED
#define FUWA_BAKED_INCLUDED

#if defined(LIGHTMAP_ON)
    #define FUWA_BAKED_COORDS(idx) float2 lmuv : TEXCOORD##idx;
    #define FUWA_BAKED_TRANSFER(o, v) o.lmuv = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
    // 焼いた影（1=日なた、0=影）。太陽ひとつなので R チャンネル
    half FuwaBakedShadow(float2 lmuv)
    {
        return UNITY_SAMPLE_TEX2D_SAMPLER(unity_ShadowMask, unity_Lightmap, lmuv).r;
    }
    // 焼いた間接光（空・地面からの照り返し）
    half3 FuwaBakedAmbient(float2 lmuv)
    {
        return DecodeLightmap(UNITY_SAMPLE_TEX2D(unity_Lightmap, lmuv));
    }
#else
    #define FUWA_BAKED_COORDS(idx)
    #define FUWA_BAKED_TRANSFER(o, v)
#endif

#endif
