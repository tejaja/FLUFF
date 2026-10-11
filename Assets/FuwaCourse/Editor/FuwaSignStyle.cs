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
        if (old != null) { old.Clear(); old.indexFormat = m.indexFormat; old.SetVertices(m.vertices); old.SetNormals(m.normals); old.SetTriangles(m.triangles, 0); old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old; }
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
        bool a = style.Contains("A"), b = style.Contains("B"), c = style.Contains("C");
        if (a)
        {
            if (bt != null) { Undo.RecordObject(bt.gameObject, "sign"); bt.gameObject.SetActive(false); }
            Part(deco, "Frame", SaveMesh(Tube(hw, hh, r, tr, pz), key + "_Frame"), SaveMat(bc, "Frame"), Vector3.zero);
            Part(deco, "Back", SaveMesh(Slab(hw, hh, r, pz + 0.005f, pz + 0.07f), key + "_Back"), SaveMat(pc, "Back"), Vector3.zero);
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
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(sign.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sign.gameObject.scene);
        return sign.parent.name + "/" + sign.name + " -> " + style;
    }

    // ---- コース以外の看板：平らな色つき枠（前のコース看板と同じ作り：一回り大きい角丸の板を後ろに置く）----
    // 2026-10-11 てじゃ「コース以外は色つけるだけでいいかも、差別化」「枠ね、色はいったんおまかせ」
    public static string AddFlatBorder(Transform panelT, Color color)
    {
        var panel = panelT.GetComponent<SpriteRenderer>(); var board = panelT.parent;
        var tmpl = Object.FindObjectsOfType<SpriteRenderer>(true).First(r => r.name == "Border" && r.sharedMaterial != null && r.sharedMaterial.name == "PanelSpriteOneSided_Border");
        var bt = board.Find("Border");
        if (bt == null)
        {
            var go = Object.Instantiate(tmpl.gameObject, board); go.name = "Border"; Undo.RegisterCreatedObjectUndo(go, "border");
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
        ("FuwaVersusLobby/HowToPlayVersus/Panel", "FF8C6B"),
        ("FuwaVersusLobby/MatchPanel/Board/Panel", "F78C4C"), // たいせん：ハブのたいせんゲートと同じオレンジ
        ("FuwaLobby/PhotoGallery/Panel", "73AAE6"),          // 名場面：空色
        ("FuwaSystem/PhotoAlbum/GoalBoard/Panel", "73AAE6"),
        ("FuwaLobby/PhotoStudio/BestTimes/Panel", "E8B84A"), // 最速タイム：金色
        ("FuwaLobby/PhotoStudio/CamInfo/Panel", "9E8CDB"),   // CAM：ボタンと同じむらさき
        ("FuwaHub/BackIsland/Credits/Panel", "EB8FBA"),
        ("FuwaCourse0/Tutorial/Entrance/GunSign/Panel", "EB8FBA"),   // チュートリアルの説明看板：コース0の色
        ("FuwaCourse0/Tutorial/Sign_ShootMushroom/Panel", "EB8FBA"),
        ("FuwaCourse0/Tutorial/Sign_MoreTricks/Panel", "EB8FBA"),
        ("FuwaCourse0/Tutorial/Bay_*/Sign/Panel", "EB8FBA"),
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
                if (t.name != "Panel" || t.GetComponent<SpriteRenderer>() == null) continue;
                if (!rx.IsMatch(PathOf(t))) continue;
                log.Add(AddFlatBorder(t, col));
            }
        }
        return string.Join("\n", log);
    }

    static string PathOf(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
