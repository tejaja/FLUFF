using UnityEngine;

// いわのみち：頂上の柱ゾーン手前の崩落（2026-10-05 に道と岩山を FuwaPitCut で切って谷にしたもの）の、道の切り抜きを作り直す。
// 道のメッシュはあとで FuwaCourse2Builder.Rebuild で作り直されて切れ目が消えていた（岩山の谷だけ残っていた。2026-10-12 てじゃが発見）。
// 切り口のギザギザは当時と同じ（同じ乱数・同じ形）。座標は当時のワールド座標なので、当時のコースの位置から今のコースの位置へ変換して使う
// （コース2はその後 x+14.04 / z+1.33 移動。回転は同じ）。岩山（Course2_mountain）は当時切ったまま残っているので切らない。
// FuwaIwaIslands.ApplySummitCut（頂上の温泉のための道の削り）から、道を書き戻す時に一緒に呼ぶ。
public static class FuwaCourse2Collapse
{
    // 当時（2026-10-05）の FuwaCourse2 の位置・回転と、切り抜きの値（当時のワールド座標）
    static readonly Vector3 OldRootPos = new Vector3(44.312717f, 1.5f, 16.566227f);
    static readonly Quaternion OldRootRot = new Quaternion(0f, 0.6234898f, 0f, 0.78183156f);
    static readonly Vector3 OldOrigin = new Vector3(100f, 0f, 0.95f);
    static readonly Vector3 OldAxis = new Vector3(1f, 0f, 0.08f);
    const float S0 = 0f, S1 = 2.6f, YBottom = 10.0f, HalfWidth = 6.5f;

    // 手前の切り口から谷の上へ突き出た舌のような足場（2026-10-13 てじゃ「黄色い道をこの赤枠の分伸ばして」。別の板を乗せたら取ってつけた感じだったので、切り口の形そのものを変える）。
    // 横 l（CutJagged の向き）の TongueL0〜TongueL1 で、手前の切り口を最大 TongueLen だけ先へ。両わきはなだらかに戻し、先はギザギザ
    const float TongueL0 = -0.3f, TongueL1 = 1.2f, TongueLen = 1.55f, TongueShoulder = 0.35f;
    static float Tongue(float l)
    {
        if (l < TongueL0 - TongueShoulder || l > TongueL1 + TongueShoulder) return 0f;
        float k = Mathf.Min(Mathf.InverseLerp(TongueL0 - TongueShoulder, TongueL0, l), Mathf.InverseLerp(TongueL1 + TongueShoulder, TongueL1, l));
        k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        // 先のギザギザ（決まった形：0.3m ごとに出たり引っこんだり）
        float u = (l - TongueL0) / 0.3f; float f = u - Mathf.Floor(u);
        float tooth = (1f - 4f * Mathf.Abs(f - 0.5f)) * 0.12f + Mathf.PerlinNoise(Mathf.Floor(u) * 0.91f, 0.37f) * -0.25f;
        return k * (TongueLen + tooth);
    }

    // 道のメッシュ（ローカル）から崩落の範囲を切り抜いた新しいメッシュを返す
    public static Mesh CutPath(Mesh src, Transform pathTr, Transform courseRoot, float maxEdge)
    {
        var map = courseRoot.localToWorldMatrix * Matrix4x4.TRS(OldRootPos, OldRootRot, Vector3.one).inverse;
        var origin = map.MultiplyPoint3x4(OldOrigin);
        var axis = map.MultiplyVector(OldAxis.normalized);
        // 切り口のギザギザ（当時と同じ作り方・同じ乱数）
        var rnd = new System.Random(57); float R() => (float)rnd.NextDouble();
        const float step = 0.3f, L0 = -6.75f; const int nE = 45;
        float[] j0 = new float[nE + 1], j1 = new float[nE + 1];
        for (int i = 0; i <= nE; i++)
        {
            float l = L0 + i * step; float w = Mathf.InverseLerp(1.4f, 2.6f, Mathf.Abs(l));
            float narrow = Mathf.Lerp(0.14f, 0.3f, w), widen = Mathf.Lerp(0.3f, 0.75f, w);
            float r0 = R(), r1 = R(); r0 = r0 * r0; r1 = r1 * r1;
            j0[i] = narrow - (narrow + widen) * r0; j1[i] = -narrow + (narrow + widen) * r1;
            if (i % 2 == 1) { j0[i] = Mathf.Min(j0[i], j0[i - 1] - 0.12f); j1[i] = Mathf.Max(j1[i], j1[i - 1] + 0.12f); }
            j0[i] = Mathf.Clamp(j0[i], -widen, narrow); j1[i] = Mathf.Clamp(j1[i], -narrow, widen);
        }
        float Interp(float[] arr, float l) { float f = (l - L0) / step; int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, nE - 1); return Mathf.Lerp(arr[i], arr[i + 1], Mathf.Clamp01(f - i)); }
        return FuwaPitCut.CutJagged(src, pathTr, origin, axis, l => S0 + Interp(j0, l) + Tongue(l), l => S1 + Interp(j1, l), YBottom, HalfWidth, maxEdge);
    }
}
