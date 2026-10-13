using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 看板（角丸の板 RoundedPanel）を立体的にする試作（2026-10-11 てじゃ「看板が味気ない、枠に色だけなのがなぁ」）
// A：ぷっくり枠（板のふちに丸い筒）＋板の厚み　B：クッション（板ごとふくらむ）　C：内側に点線のステッチ
// 板の大きさ・角の丸み（RoundedPanel の角 64px / 256ppu ＝ 0.25 × スケール）から作る。看板の表は -z 側（文字が手前）
public static class FuwaSignStyle
{
    // 角丸の長方形の輪郭（中心原点、反時計回り）。角は seg 分割、辺は step ごとに点を入れる
    public static List<Vector2> Outline(float hw, float hh, float r, int seg = 10, float step = 0.05f)
    {
        var pts = new List<Vector2>();
        var cs = new[] { new Vector2(hw - r, hh - r), new Vector2(-(hw - r), hh - r), new Vector2(-(hw - r), -(hh - r)), new Vector2(hw - r, -(hh - r)) };
        for (int q = 0; q < 4; q++)
        {
            for (int k = 0; k <= seg; k++) { float a = (q * 90f + k * 90f / seg) * Mathf.Deg2Rad; pts.Add(cs[q] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            var from = pts[pts.Count - 1]; float a1 = (q + 1) * 90f * Mathf.Deg2Rad;
            var to = cs[(q + 1) % 4] + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
            int n = Mathf.Max(1, Mathf.RoundToInt((to - from).magnitude / step));
            for (int i = 1; i < n; i++) pts.Add(Vector2.Lerp(from, to, i / (float)n));
        }
        return pts;
    }

    // 輪郭の外向きの法線
    static Vector2[] Normals(List<Vector2> p)
    {
        int n = p.Count; var o = new Vector2[n];
        for (int i = 0; i < n; i++) { var t = (p[(i + 1) % n] - p[(i - 1 + n) % n]).normalized; o[i] = new Vector2(t.y, -t.x); }
        return o;
    }

    static Mesh Finish(List<Vector3> V, List<int> T, string name)
    {
        var m = new Mesh { name = name };
        if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // A：ふちに沿った丸い筒（断面は円、中心はふちの線上、z=z0）
    public static Mesh Tube(float hw, float hh, float r, float tr, float z0, int ring = 16)
    {
        var p = Outline(hw, hh, r, 12, 0.04f); var nr = Normals(p); int n = p.Count;
        var V = new List<Vector3>(); var T = new List<int>();
        for (int i = 0; i < n; i++)
            for (int k = 0; k < ring; k++)
            {
                float a = k / (float)ring * Mathf.PI * 2;
                var d = nr[i] * Mathf.Cos(a);
                V.Add(new Vector3(p[i].x + d.x * tr, p[i].y + d.y * tr, z0 + Mathf.Sin(a) * tr));
            }
        for (int i = 0; i < n; i++)
            for (int k = 0; k < ring; k++)
            {
                int a0 = i * ring + k, a1 = i * ring + (k + 1) % ring, b0 = ((i + 1) % n) * ring + k, b1 = ((i + 1) % n) * ring + (k + 1) % ring;
                T.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
        var m = Finish(V, T, "SignTube");
        if (Vector2.Dot(new Vector2(m.normals[0].x, m.normals[0].y), nr[0]) < 0) Flip(m);   // 外向きに
        return m;
    }

    // 板の厚み（角丸の板を z0〜z1 に押し出し。表 z0 は板の後ろ、裏 z1）
    public static Mesh Slab(float hw, float hh, float r, float z0, float z1)
    {
        var p = Outline(hw, hh, r, 12, 0.08f); int n = p.Count;
        var V = new List<Vector3>(); var T = new List<int>();
        // 側面（法線を分けるため上下の輪を別に）
        for (int i = 0; i < n; i++) { V.Add(new Vector3(p[i].x, p[i].y, z0)); V.Add(new Vector3(p[i].x, p[i].y, z1)); }
        for (int i = 0; i < n; i++) { int a = i * 2, b = ((i + 1) % n) * 2; T.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 }); }
        // 裏の面（扇）
        int c = V.Count; V.Add(new Vector3(0, 0, z1)); int s = V.Count;
        for (int i = 0; i < n; i++) V.Add(new Vector3(p[i].x, p[i].y, z1));
        for (int i = 0; i < n; i++) T.AddRange(new[] { c, s + (i + 1) % n, s + i });
        var m = Finish(V, T, "SignSlab");
        if (m.normals[c].z < 0) Flip(m);   // 裏の面は +z 向き（側面も同じ向きにそろう）
        return m;
    }

    // B：クッション。表（-z）が真ん中に向かってふくらむ。ふちは z0、真ん中は z0 - bulge。裏は平ら z1
    public static Mesh Cushion(float hw, float hh, float r, float z0, float z1, float bulge, float rise)
    {
        var p = Outline(hw, hh, r, 12, 0.06f); int n = p.Count;
        var V = new List<Vector3>(); var T = new List<int>();
        int rings = 14;
        // 輪郭をだんだん内側へ縮めた輪（ふちからの距離 d）で、ふくらみ = bulge * (1 - (1 - d/rise)^2)
        float maxIn = Mathf.Min(hw, hh) * 0.98f;
        for (int k = 0; k <= rings; k++)
        {
            float u = k / (float)rings; float d = maxIn * (1 - Mathf.Cos(u * Mathf.PI * 0.5f));
            float q = Mathf.Clamp01(d / rise); float z = z0 - bulge * Mathf.Sqrt(1 - (1 - q) * (1 - q));
            for (int i = 0; i < n; i++)
            {
                // 角の丸みを保って内側へ（ふちの点を中心へ向かって d だけ寄せる。四角の中心線に向かって詰める）
                var pt = p[i]; float sx = Mathf.Max(0f, hw - d) / hw, sy = Mathf.Max(0f, hh - d) / hh;
                V.Add(new Vector3(pt.x * sx, pt.y * sy, z));
            }
        }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < n; i++)
            {
                int a0 = k * n + i, a1 = k * n + (i + 1) % n, b0 = a0 + n, b1 = a1 + n;
                T.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        int c = V.Count; V.Add(new Vector3(0, 0, V[rings * n].z)); int last = rings * n;
        for (int i = 0; i < n; i++) T.AddRange(new[] { last + i, c, last + (i + 1) % n });
        var front = Finish(V, T, "SignCushion");
        // 表の面は -z を向くように
        var nn = front.normals; if (nn[c].z > 0) { var t = front.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } front.triangles = t; front.RecalculateNormals(); }
        return front;
    }

    // C：内側の点線（ステッチ）。ふちから inset 内側、小さい角丸の短い棒を並べる
    public static Mesh Stitch(float hw, float hh, float r, float inset, float z, float dash, float gap, float width)
    {
        float iw = hw - inset, ih = hh - inset, ir = Mathf.Max(0.02f, r - inset);
        var p = Outline(iw, ih, ir, 24, 0.01f); int n = p.Count;
        // 周長に沿って dash/gap
        var L = new float[n + 1]; for (int i = 0; i < n; i++) L[i + 1] = L[i] + (p[(i + 1) % n] - p[i]).magnitude;
        float total = L[n]; int count = Mathf.Max(4, Mathf.RoundToInt(total / (dash + gap))); float per = total / count, dl = per * dash / (dash + gap);
        Vector2 At(float s) { s = Mathf.Repeat(s, total); int i = 0; while (i < n - 1 && L[i + 1] < s) i++; float t = (s - L[i]) / Mathf.Max(1e-6f, L[i + 1] - L[i]); return Vector2.Lerp(p[i], p[(i + 1) % n], t); }
        var V = new List<Vector3>(); var T = new List<int>();
        for (int k = 0; k < count; k++)
        {
            float s0 = k * per; int segs = 4;
            int b = V.Count;
            for (int j = 0; j <= segs; j++)
            {
                float s = s0 + dl * j / segs; var a = At(s); var tg = (At(s + 0.005f) - At(s - 0.005f)).normalized; var nr = new Vector2(tg.y, -tg.x);
                V.Add(new Vector3(a.x + nr.x * width * 0.5f, a.y + nr.y * width * 0.5f, z));
                V.Add(new Vector3(a.x - nr.x * width * 0.5f, a.y - nr.y * width * 0.5f, z));
            }
            for (int j = 0; j < segs; j++) { int a0 = b + j * 2; T.AddRange(new[] { a0, a0 + 2, a0 + 1, a0 + 1, a0 + 2, a0 + 3 }); }
        }
        var m = Finish(V, T, "SignStitch");
        var nn = m.normals; if (nn.Length > 0 && nn[0].z > 0) { var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } m.triangles = t; m.RecalculateNormals(); }
        return m;
    }

    // 雲：輪郭に沿って丸いこぶを並べる（2026-10-12 てじゃ「もっとふわっと感」→ 雲型の B「大きくゆるい波」に決定。こぶが細かいと「もこもこが強すぎ」）
    // amp＝こぶの高さ、lambda＝こぶ1個の長さ、shape＝こぶの丸み（1＝半円、小さいほど平たくて谷がとがる）
    public static List<Vector2> Scallop(List<Vector2> p, float amp, float lambda, float shape)
    {
        int n = p.Count; var L = new float[n + 1];
        for (int i = 0; i < n; i++) L[i + 1] = L[i] + (p[(i + 1) % n] - p[i]).magnitude;
        float total = L[n]; int bumps = Mathf.Max(6, Mathf.RoundToInt(total / lambda)); float per = total / bumps;
        var nr = Normals(p); var o = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            float ph = Mathf.Repeat(L[i] / per, 1f);
            float b = Mathf.Pow(Mathf.Max(0f, 1f - (2f * ph - 1f) * (2f * ph - 1f)), shape);
            o.Add(p[i] + nr[i] * amp * b);
        }
        return o;
    }

    // まくら：輪郭から前（-z）と後ろ（+z）の両方にふくらませる。ふちは zc、前の真ん中は zc - front。
    // 輪郭を幅・高さそれぞれ同じ距離だけ内側へ寄せた輪を重ねる（Cushion と同じ作り）
    public static Mesh Pillow(List<Vector2> p, float hw, float hh, float zc, float front, float back, float rise, int rings = 18)
    {
        int n = p.Count; var V = new List<Vector3>(); var T = new List<int>();
        float maxIn = Mathf.Min(hw, hh) * 0.985f;
        for (int side = 0; side < 2; side++)
        {
            int b0 = V.Count; float sgn = side == 0 ? -1f : 1f, bul = side == 0 ? front : back;
            for (int k = 0; k <= rings; k++)
            {
                float d = maxIn * (1 - Mathf.Cos(k / (float)rings * Mathf.PI * 0.5f));
                float q = Mathf.Clamp01(d / rise); float z = zc + sgn * bul * Mathf.Sqrt(1 - (1 - q) * (1 - q));
                float sx = Mathf.Max(0f, hw - d) / hw, sy = Mathf.Max(0f, hh - d) / hh;
                for (int i = 0; i < n; i++) V.Add(new Vector3(p[i].x * sx, p[i].y * sy, z));
            }
            for (int k = 0; k < rings; k++)
                for (int i = 0; i < n; i++)
                {
                    int a0 = b0 + k * n + i, a1 = b0 + k * n + (i + 1) % n, c0 = a0 + n, c1 = a1 + n;
                    if (side == 0) T.AddRange(new[] { a0, c0, a1, a1, c0, c1 }); else T.AddRange(new[] { a0, a1, c0, a1, c1, c0 });
                }
            int c = V.Count, last = b0 + rings * n; V.Add(new Vector3(0, 0, V[last].z));
            for (int i = 0; i < n; i++) { if (side == 0) T.AddRange(new[] { last + i, c, last + (i + 1) % n }); else T.AddRange(new[] { last + i, last + (i + 1) % n, c }); }
        }
        var m = Finish(V, T, "SignPillow");
        // uv.y＝高さ（下はし0〜上はし1、SetHeightUV）。雲の下の方の水色の影に使う（FuwaToonLit の _UnderGrad）
        SetHeightUV(m);
        if (m.normals[(rings + 1) * n].z > 0) Flip(m);   // 前の真ん中が -z を向くように
        return m;
    }

    public static void SetHeightUV(Mesh m)
    {
        var b = m.bounds; var vs = m.vertices; var uv = new Vector2[vs.Length];
        for (int i = 0; i < vs.Length; i++) uv[i] = new Vector2(0f, Mathf.InverseLerp(b.min.y, b.max.y, vs[i].y));
        m.uv = uv;
    }

    static void Flip(Mesh m) { var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } m.triangles = t; m.RecalculateNormals(); }

    public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 lp)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = lp;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    public static Material Toon(Color c, string name)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Fuwa_Blower.mat");
        var m = new Material(src) { name = name }; m.SetColor("_Color", c);
        if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0.15f);
        return m;
    }

    // ---- 看板にかける／外す ----
    // 看板（Panel と Border の SpriteRenderer を子に持つ）に「SignDeco」を作る。Border は隠す（外すと元に戻る）。メッシュと材質は Generated/Signs に保存
    const string Dir = "Assets/FuwaCourse/Generated/Signs";

    static Mesh SaveMesh(Mesh m, string name)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Signs");
        string path = Dir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { old.Clear(); old.indexFormat = m.indexFormat; old.SetVertices(m.vertices); old.SetNormals(m.normals); if (m.uv.Length == m.vertexCount) old.SetUVs(0, m.uv); old.SetTriangles(m.triangles, 0); old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old; }
        m.name = name; AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material SaveMat(Color c, string kind, float shadow = -1f)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Signs");
        string path = Dir + "/Sign_" + kind + "_" + ColorUtility.ToHtmlStringRGB(c) + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = Toon(c, kind); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c); if (shadow >= 0f) { m.SetFloat("_ShadowStrength", shadow); m.SetFloat("_AmbientStrength", 1.2f); }
        EditorUtility.SetDirty(m);
        return m;
    }

    public static void Remove(Transform sign)
    {
        var d = sign.Find("SignDeco"); if (d != null) Undo.DestroyObjectImmediate(d.gameObject);
        foreach (var n in new[] { "Panel", "Border" }) { var t = sign.Find(n); if (t != null && !t.gameObject.activeSelf) { Undo.RecordObject(t.gameObject, "sign"); t.gameObject.SetActive(true); } }
    }

    public const float FrameScale = 0.7f;   // 枠の筒の太さ（今の平らな枠の幅に対して。てじゃ「枠をもちょい細く」）

    // style: "A"（ぷっくり枠＋厚み）/"B"（クッション）/"C"（ステッチ）/"AC"。accent は Border が無い看板の枠の色
    public static string Apply(Transform sign, string style, Color accent, float frameScale = FrameScale)
    {
        Remove(sign);
        var pt = sign.Find("Panel"); if (pt == null) return sign.name + ": no panel";
        var panel = pt.GetComponent<SpriteRenderer>(); var bt = sign.Find("Border");
        var border = bt != null ? bt.GetComponent<SpriteRenderer>() : null;
        Vector3 ps = pt.localScale;
        float hw = panel.size.x * ps.x * 0.5f, hh = panel.size.y * ps.y * 0.5f, r = 0.25f * ps.x, pz = pt.localPosition.z;
        Color bc = border != null ? border.color : accent; bc.a = 1f;
        Color pc = panel.color; pc.a = 1f;
        float tr = border != null ? (border.size.x * bt.localScale.x * 0.5f - hw) : 0.075f;   // 枠の筒の太さ＝今の枠の幅
        tr = Mathf.Clamp(tr, 0.05f, 0.12f) * frameScale;
        string key = sign.parent.name + "_" + sign.name + "_" + style;
        var deco = new GameObject("SignDeco").transform; Undo.RegisterCreatedObjectUndo(deco.gameObject, "sign");
        deco.SetParent(sign, false); deco.localPosition = new Vector3(pt.localPosition.x, pt.localPosition.y, 0f); deco.localRotation = pt.localRotation;
        if (style == "Cloud") return ApplyCloud(sign, deco, pt, bt, hw, hh, pz, pc, bc, key);
        bool a = style.Contains("A"), b = style.Contains("B"), c = style.Contains("C");
        if (a)
        {
            if (bt != null) { Undo.RecordObject(bt.gameObject, "sign"); bt.gameObject.SetActive(false); }
            Part(deco, "Frame", SaveMesh(Tube(hw, hh, r, tr, pz), key + "_Frame"), SaveMat(bc, "Frame"), Vector3.zero);
            // 板の厚みは枠の筒の中におさめる（はみ出すと横から見て裏板と枠の間にすき間が見えた。てじゃ）
            Part(deco, "Back", SaveMesh(Slab(hw - tr * 0.3f, hh - tr * 0.3f, Mathf.Max(0.02f, r - tr * 0.3f), pz + 0.005f, pz + tr * 0.8f), key + "_Back"), SaveMat(pc, "Back"), Vector3.zero);
        }
        if (b)
        {
            if (bt != null) { Undo.RecordObject(bt.gameObject, "sign"); bt.gameObject.SetActive(false); }
            Undo.RecordObject(pt.gameObject, "sign"); pt.gameObject.SetActive(false);
            var cm = SaveMat(pc, "Cushion", 0.35f);
            Part(deco, "Cushion", SaveMesh(Cushion(hw, hh, r, pz + 0.05f, pz + 0.09f, 0.045f, 0.35f), key + "_Cushion"), cm, Vector3.zero);
            Part(deco, "Back", SaveMesh(Slab(hw, hh, r, pz + 0.05f, pz + 0.09f), key + "_Back"), cm, Vector3.zero);
            Part(deco, "Piping", SaveMesh(Tube(hw, hh, r, 0.045f, pz + 0.05f), key + "_Piping"), SaveMat(bc, "Frame"), Vector3.zero);
        }
        if (c)
        {
            var sc = Color.Lerp(bc, Color.white, 0.35f);
            Part(deco, "Stitch", SaveMesh(Stitch(hw, hh, r, a ? tr + 0.055f : 0.1f, pz - 0.003f, 0.06f, 0.045f, 0.018f), key + "_Stitch"), SaveMat(sc, "Stitch"), Vector3.zero);
        }
        // 文字はステッチの内側におさめる（英語は長くて縫い目までいった。てじゃ）：枠の中の幅で自動縮小
        var text = sign.GetComponent<TMPro.TMP_Text>();
        if (text != null && c)
        {
            float inset = (a ? tr + 0.055f : 0.1f) + 0.12f;
            var rt = text.rectTransform;
            float sx = Mathf.Abs(rt.localScale.x) > 1e-4f ? Mathf.Abs(rt.localScale.x) : 1f;
            rt.sizeDelta = new Vector2((hw - inset) * 2f / sx, (hh + 0.3f) * 2f / sx);   // 縦は余裕をもたせて、横幅だけで縮むように
            if (!text.enableAutoSizing) { text.fontSizeMax = text.fontSize; text.enableAutoSizing = true; }
            text.fontSizeMin = text.fontSizeMax * 0.4f;
            text.enableWordWrapping = false;
            EditorUtility.SetDirty(text);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(sign.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sign.gameObject.scene);
        return sign.parent.name + "/" + sign.name + " -> " + style;
    }

    // 雲の看板：白い雲（前後にぷくっとふくらむ）＋後ろに一回り大きいコース色の雲（縁どり代わり）
    // 色の雲は影で暗くならないよう影を弱く（てじゃ「コースの色、影でくらくなる」）
    public static readonly Color CloudUnderColor = new Color(0.6f, 0.76f, 1f, 1f);
    // 色の雲の影：自分の色をかける（濃く・鮮やかになる）＋少し暗く
    public static Color RimUnderColor(Color c) { var u = Color.Lerp(Color.white, c, RimUnderTint) * RimUnderDark; u.a = 1f; return u; }
    public const float RimUnderTint = 0.6f, RimUnderDark = 0.9f;
    public const float CloudUnderGrad = 0.5f;   // 下から高さのこの割合まで、正面にもうっすら水色
    public const float CloudAmp = 0.07f, CloudLambda = 0.95f, CloudShape = 0.7f, CloudRound = 0.45f, CloudRim = 0.09f;
    static string ApplyCloud(Transform sign, Transform deco, Transform pt, Transform bt, float hw, float hh, float pz, Color pc, Color bc, string key)
    {
        Undo.RecordObject(pt.gameObject, "sign"); pt.gameObject.SetActive(false);
        if (bt != null) { Undo.RecordObject(bt.gameObject, "sign"); bt.gameObject.SetActive(false); }
        float ow = hw - CloudAmp * 0.8f, oh = hh - CloudAmp * 0.4f;
        var body = Scallop(Outline(ow, oh, CloudRound, 12, 0.025f), CloudAmp, CloudLambda, CloudShape);
        // 白い雲の下側に水色の影（2026-10-12 てじゃ「青っぽい影つけたらかわいい」）。光の向きに関係なく下向きの面だけ
        var cm = SaveMat(pc, "Cloud", 0.3f);
        cm.SetColor("_UnderColor", CloudUnderColor); cm.SetFloat("_UnderStrength", 1f); cm.SetFloat("_UnderGrad", CloudUnderGrad); EditorUtility.SetDirty(cm);
        Part(deco, "Cloud", SaveMesh(Pillow(body, hw, hh, pz + 0.13f, 0.11f, 0.08f, 0.32f, 14), key + "_Cloud"), cm, Vector3.zero);
        var rim = Scallop(Outline(ow + CloudRim, oh + CloudRim, CloudRound + CloudRim, 12, 0.025f), CloudAmp, CloudLambda, CloudShape);
        // 色の雲も下の方をその色の少し濃い色に（2026-10-12 てじゃ「後ろのカラーふわふわも下の方各色のちょっと影っぽく」）
        var rm = SaveMat(bc, "CloudRim", 0.2f);
        rm.SetColor("_UnderColor", RimUnderColor(bc)); rm.SetFloat("_UnderStrength", 1f); rm.SetFloat("_UnderGrad", CloudUnderGrad); EditorUtility.SetDirty(rm);
        Part(deco, "CloudRim", SaveMesh(Pillow(rim, hw + CloudRim, hh + CloudRim, pz + 0.27f, 0.06f, 0.06f, 0.2f, 8), key + "_CloudRim"), rm, Vector3.zero);
        // 文字は雲の内側におさめる（横幅だけで自動縮小）
        var text = sign.GetComponent<TMPro.TMP_Text>();
        if (text != null)
        {
            const float inset = 0.24f;
            var rt = text.rectTransform;
            float sx = Mathf.Abs(rt.localScale.x) > 1e-4f ? Mathf.Abs(rt.localScale.x) : 1f;
            Undo.RecordObject(rt, "sign"); Undo.RecordObject(text, "sign");
            rt.sizeDelta = new Vector2((hw - inset) * 2f / sx, (hh + 0.3f) * 2f / sx);
            if (!text.enableAutoSizing) { text.fontSizeMax = text.fontSize; text.enableAutoSizing = true; }
            text.fontSizeMin = text.fontSizeMax * 0.4f;
            text.enableWordWrapping = false;
            EditorUtility.SetDirty(text);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sign.gameObject.scene);
        return sign.parent.name + "/" + sign.name + " -> Cloud";
    }

    // ロビーのコース看板（Slot0〜5）とつぎのしまの看板を雲にする
    [MenuItem("FuwaCourse/コース看板を雲にする")]
    public static void CloudSignsMenu()
    {
        var log = new List<string>();
        foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(x => x.name == "Label").ToList())   // 先に集める（付け直しで古い SignDeco が消えるので）
        {
            if (t.name != "Label" || t.parent == null || t.parent.parent == null || t.parent.parent.name != "FuwaLobby") continue;
            if (!(t.parent.name.StartsWith("Slot") || t.parent.name == "Stage2Portal") || t.Find("Panel") == null) continue;
            log.Add(Apply(t, "Cloud", Color.white));
        }
        Debug.Log(string.Join("\n", log));
    }

    // ---- コース以外の看板：平らな色つき枠（前のコース看板と同じ作り：一回り大きい角丸の板を後ろに置く）----
    // 2026-10-11 てじゃ「コース以外は色つけるだけでいいかも、差別化」「枠ね、色はいったんおまかせ」
    public static string AddFlatBorder(Transform panelT, Color color)
    {
        var panel = panelT.GetComponent<SpriteRenderer>(); var board = panelT.parent;
        var tmpl = Object.FindObjectsOfType<SpriteRenderer>(true).First(r => r.name == "Border" && r.sharedMaterial != null && r.sharedMaterial.name == "PanelSpriteOneSided_Border");
        string bname = panelT.name == "Panel" ? "Border" : panelT.name + "Border";   // 1つの看板に板が2枚ある時（ゴールの左右の板）は別々に
        var bt = board.Find(bname);
        if (bt == null)
        {
            var go = Object.Instantiate(tmpl.gameObject, board); go.name = bname; Undo.RegisterCreatedObjectUndo(go, "border");
            bt = go.transform; bt.SetSiblingIndex(panelT.GetSiblingIndex() + 1);
        }
        Undo.RecordObject(bt, "border"); var br = bt.GetComponent<SpriteRenderer>(); Undo.RecordObject(br, "border");
        bt.gameObject.SetActive(true);
        float w = 0.07f / Mathf.Abs(panelT.localScale.x);              // 枠の幅（看板の親の座標で 7cm 相当）
        float k = (0.25f + w) / 0.25f;                                   // 角の丸みが板と同心になる倍率
        bt.localRotation = panelT.localRotation;
        bt.localScale = panelT.localScale * k;
        bt.localPosition = panelT.localPosition + new Vector3(0f, 0f, 0.006f);
        br.drawMode = SpriteDrawMode.Sliced; br.sprite = tmpl.sprite; br.sharedMaterial = tmpl.sharedMaterial;
        br.size = (panel.size + Vector2.one * 2f * w) / k;
        br.color = color; br.sortingOrder = panel.sortingOrder;
        EditorUtility.SetDirty(br);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
        return board.name + " border " + ColorUtility.ToHtmlStringRGB(color);
    }

    [MenuItem("FuwaCourse/看板に色つきの枠をつける（コース以外）")]
    public static void FlatBordersMenu() { Debug.Log(FlatBorders()); }

    // 看板ごとの枠の色（パスの末尾で探す）
    static readonly (string path, string hex)[] FlatTargets =
    {
        ("FuwaLobby/HowToPlay/Panel", "FF8C6B"),            // あそびかた：タイトルのオレンジに合わせて少しやわらかく
        ("FuwaLobby/PhotoGallery/Panel", "73AAE6"),          // 名場面：空色
        ("FuwaSystem/PhotoAlbum/GoalBoard/*/Panel", "73AAE6"),
        ("FuwaLobby/PhotoStudio/BestTimes/Panel", "E8B84A"), // 最速タイム：金色
        ("FuwaLobby/PhotoStudio/Monitor/Panel", "9E8CDB"),   // モニター：CAMボタンと同じむらさき
        ("FuwaHub/BackIsland/Credits/Panel", "EB8FBA"),
        // チュートリアルの説明看板は木の看板（SignModel）に貼ってあるので枠はつけない（つけたら板の後ろの木とちらついた）
    };

    public static string FlatBorders()
    {
        var log = new List<string>();
        var all = Object.FindObjectsOfType<Transform>(true);
        foreach (var (path, hex) in FlatTargets)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var col);
            var rx = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(path).Replace("\\*", "[^/]*") + "$");
            foreach (var t in all)
            {
                if (!t.name.StartsWith("Panel") || t.GetComponent<SpriteRenderer>() == null) continue;
                if (!rx.IsMatch(PathOf(t))) continue;
                log.Add(AddFlatBorder(t, col));
            }
        }
        return string.Join("\n", log);
    }

    static string PathOf(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
