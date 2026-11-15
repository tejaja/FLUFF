// 2026-11-15 2–5 クリスタルの広場：セーフティーゾーン（バリアの影）の形。床・幕・衝撃波の壁・砂ぼこり・当たり（FuwaShockwave.Safe）で同じ形
//  てじゃ「まだ球の一番長い部分から影が出てない」「ちょっと凹んで見える」
//  前は「球のまわりの円」＋「真横から広がる線」で、つなぎ目が内側に折れていた
//  → クリスタル（光源）から、余白つきの円（半径 R0＝バリアの半径＋余白）に接する2本の線と円でつつむ形（ふつうの影の形。つなぎ目はなめらか）
//  q・b：クリスタルから見た位置（上から見た2D）。返り値：ふちまでの距離（m、＋なら外）
float FuwaSafeDist(float2 q, float2 b, float R0)
{
    float dn = length(q - b) - R0;
    float bl = length(b);
    if (bl <= R0 + 0.01) return dn;
    float2 bh = b / bl;
    float along = dot(q, bh), perp = length(q - bh * along);
    float sa = R0 / bl, ca = sqrt(1 - sa * sa);
    float at = bl - R0 * sa;                 // 接する所（クリスタルから見た奥行き）
    float dl = perp * ca - along * sa;       // 接する線までの距離
    return min(dn, max(dl, at - along));
}
