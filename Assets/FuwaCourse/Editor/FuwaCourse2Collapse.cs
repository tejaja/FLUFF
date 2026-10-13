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
    internal const float TongueL0 = 0.0f, TongueL1 = 0.95f, TongueLen = 1.55f, TongueShoulder = 0.55f;   // 2026-10-13 てじゃ「もうちょい細く」：幅1.5→0.95m   // 肩は広め（元の切り口となだらかにつながるように。2026-10-13 てじゃ「つなぎ目にちょっと違和感」）
    // 舌の所か（0〜1）
    static float TongueK(float l)
    {
        float k = Mathf.Min(Mathf.InverseLerp(TongueL0 - TongueShoulder, TongueL0, l), Mathf.InverseLerp(TongueL1 + TongueShoulder, TongueL1, l));
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
    }

    internal static float Tongue(float l)
    {
        if (l < TongueL0 - TongueShoulder || l > TongueL1 + TongueShoulder) return 0f;
        float k = Mathf.Min(Mathf.InverseLerp(TongueL0 - TongueShoulder, TongueL0, l), Mathf.InverseLerp(TongueL1 + TongueShoulder, TongueL1, l));
        k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        // 先の形：細かいギザギザではなく、大きなかたまりが欠けたような、ゆるいでこぼこ（2026-10-13 てじゃ「もっとギザギザしてない感じ」）。
        // 0.6m ごとの高さを決めて、そのあいだはなめらかにつなぐ
        float u = (l - TongueL0) / 0.6f; int i0 = Mathf.FloorToInt(u); float f = Mathf.SmoothStep(0f, 1f, u - i0);
        float h0 = Mathf.PerlinNoise(i0 * 0.77f, 0.41f) - 0.5f, h1 = Mathf.PerlinNoise((i0 + 1) * 0.77f, 0.41f) - 0.5f;
        float lump = Mathf.Lerp(h0, h1, f) * 0.55f;
        return k * (TongueLen + lump);
    }

    // 崩落の切り抜きの基準（ワールド）。origin と、谷を横切る向き axis（水平）
    internal static (Vector3 origin, Vector3 axis) Frame(Transform courseRoot)
    {
        var map = courseRoot.localToWorldMatrix * Matrix4x4.TRS(OldRootPos, OldRootRot, Vector3.one).inverse;
        var ax = map.MultiplyVector(OldAxis.normalized); ax.y = 0f;
        return (map.MultiplyPoint3x4(OldOrigin), ax.normalized);
    }

    // ---- 谷の切り口の線（2026-10-13 作り直し）----
    // てじゃ「頂上後の崖周りが道も岩もところどころ乱れてる」「岩の割れ目そのものもちょっと荒い」「隙間、はみ出し系は徹底的に洗って」：
    // 前は道（細かいギザギザ）・山の穴（10-05 に別のギザギザで切ったまま）・谷の壁と底（PitRock、手で作った別の岩）がそれぞれ別の線で、すき間・はみ出し・トゲが出ていた。
    // 今は「手前の線 Near(l)」「奥の線 Far(l)」の2本だけ。山・山の当たり判定・道・谷の壁と底を全部この線で切ってふさぐ（FuwaCourse2Valley・FuwaCutCap）。
    // 線は当時の山の穴のふち（ギザギザ）を 8cm 外から包んで、なめらかにしたもの（測った値を固定。横 l は -7〜7m を 0.2m おき）。谷の幅は平均 2.9m → 約 3.4m
    internal const float CurveL0 = -7f, CurveStep = 0.2f;
    static readonly float[] NearCurve = { 0.010f,0.010f,0.010f,0.010f,0.010f,0.010f,0.010f,0.010f,0.009f,0.008f,0.005f,-0.001f,-0.013f,-0.033f,-0.067f,-0.119f,-0.199f,-0.314f,-0.459f,-0.609f,-0.718f,-0.743f,-0.670f,-0.529f,-0.373f,-0.247f,-0.166f,-0.128f,-0.122f,-0.138f,-0.173f,-0.224f,-0.283f,-0.340f,-0.382f,-0.400f,-0.396f,-0.378f,-0.358f,-0.347f,-0.356f,-0.387f,-0.433f,-0.481f,-0.514f,-0.520f,-0.500f,-0.464f,-0.430f,-0.406f,-0.393f,-0.381f,-0.362f,-0.331f,-0.287f,-0.230f,-0.164f,-0.100f,-0.048f,-0.012f,0.009f,0.021f,0.026f,0.029f,0.030f,0.030f,0.030f,0.030f,0.030f,0.030f,0.030f };
    static readonly float[] FarCurve = { 1.800f,1.800f,1.800f,1.800f,1.800f,1.800f,1.800f,1.800f,1.800f,1.804f,1.811f,1.827f,1.865f,1.939f,2.075f,2.293f,2.581f,2.885f,3.134f,3.297f,3.392f,3.452f,3.486f,3.473f,3.399f,3.275f,3.130f,2.996f,2.900f,2.851f,2.844f,2.872f,2.920f,2.970f,3.005f,3.017f,3.008f,2.984f,2.955f,2.925f,2.900f,2.881f,2.872f,2.874f,2.890f,2.921f,2.970f,3.036f,3.112f,3.180f,3.222f,3.225f,3.190f,3.132f,3.071f,3.021f,2.988f,2.969f,2.958f,2.954f,2.951f,2.950f,2.950f,2.950f,2.950f,2.950f,2.950f,2.950f,2.950f,2.950f,2.950f };
    static float Sample(float[] a, float l) { float f = (l - CurveL0) / CurveStep; int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, a.Length - 2); return Mathf.Lerp(a[i], a[i + 1], Mathf.Clamp01(f - i)); }
    internal static float Near(float l) => Sample(NearCurve, l);
    internal static float Far(float l) => Sample(FarCurve, l);
    // 道の手前の切り口：舌の所だけ谷へ突き出す。奥の切り口が前より奥へ下がった分（Far - 前の道の奥の切り口）だけ舌も伸ばして、跳ぶ距離は前と同じ
    internal static float PathNear(float l)
    {
        var (_, j1) = Jags();
        float k = TongueK(l);
        return Near(l) * (1f - k) + Tongue(l) + k * (Far(l) - (S1 + FarEdge(j1, l)));
    }
    internal const float ValleyBottom = YBottom, ValleyHalfWidth = HalfWidth;
    public static string LastCapLog = "";

    // 道のメッシュ（ローカル）から崩落の範囲を切り抜いて、切り口をふさいだ新しいメッシュを返す
    public static Mesh CutPath(Mesh src, Transform pathTr, Transform courseRoot, float maxEdge)
    {
        var (origin, axis) = Frame(courseRoot);
        var cut = FuwaPitCut.CutJagged(src, pathTr, origin, axis, PathNear, Far, YBottom, HalfWidth, maxEdge);
        var f = new FuwaCutCap.Frame { origin = origin, axis = axis, side = new Vector3(-axis.z, 0f, axis.x), e0 = PathNear, e1 = Far, yBottom = YBottom };
        var (ct, cb, cl) = FuwaCutCap.Cap(cut, pathTr, f, 0, 0f, 0f, 1); LastCapLog += "path cap tris " + ct + (cb > 0 ? " UNRESOLVED " + cb + " " + cl : "") + "; ";
        return cut;
    }

    // 切り口のギザギザ（当時と同じ作り方・同じ乱数）
    const float JStep = 0.3f, JL0 = -6.75f; const int JN = 45;
    static (float[] j0, float[] j1) Jags()
    {
        var rnd = new System.Random(57); float R() => (float)rnd.NextDouble();
        float[] j0 = new float[JN + 1], j1 = new float[JN + 1];
        for (int i = 0; i <= JN; i++)
        {
            float l = JL0 + i * JStep; float w = Mathf.InverseLerp(1.4f, 2.6f, Mathf.Abs(l));
            float narrow = Mathf.Lerp(0.14f, 0.3f, w), widen = Mathf.Lerp(0.3f, 0.75f, w);
            float r0 = R(), r1 = R(); r0 = r0 * r0; r1 = r1 * r1;
            j0[i] = narrow - (narrow + widen) * r0; j1[i] = -narrow + (narrow + widen) * r1;
            if (i % 2 == 1) { j0[i] = Mathf.Min(j0[i], j0[i - 1] - 0.12f); j1[i] = Mathf.Max(j1[i], j1[i - 1] + 0.12f); }
            j0[i] = Mathf.Clamp(j0[i], -widen, narrow); j1[i] = Mathf.Clamp(j1[i], -narrow, widen);
        }
        return (j0, j1);
    }
    // 向こう岸の切り口：細かいギザギザだと、歯ごとに光の当たり方が変わって縦の筋のように見え、汚く見えた（2026-10-13 てじゃ「筋もあるしちょっときたなく」）。
    // 足場と同じく、大きくゆるいでこぼこにする（元のギザギザは2割だけ残す）
    static float FarEdge(float[] j1, float l)
    {
        float u = l / 0.7f; int i0 = Mathf.FloorToInt(u); float f = Mathf.SmoothStep(0f, 1f, u - i0);
        float h0 = Mathf.PerlinNoise(i0 * 0.63f, 1.7f) - 0.5f, h1 = Mathf.PerlinNoise((i0 + 1) * 0.63f, 1.7f) - 0.5f;
        return Interp(j1, l) * 0.2f + Mathf.Lerp(h0, h1, f) * 0.5f + 0.15f;
    }

    static float Interp(float[] arr, float l) { float f = (l - JL0) / JStep; int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, JN - 1); return Mathf.Lerp(arr[i], arr[i + 1], Mathf.Clamp01(f - i)); }

    // 手前の切り口の位置（切り抜きの軸に沿った距離 s、横 l は CutJagged の向き）。足場の下の岩（FuwaCourse2Ledge）が使う
    internal static float NearEdge(float l) => PathNear(l);
}
