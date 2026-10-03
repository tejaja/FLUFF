using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// かぜのみちの風の飾り（吹き流し・かざぐるま）を作って並べる。メッシュはここで生成して Meshes/WindDeco に保存。
// 柱は道の横の面にくっつけて立てる（道の上には乗せない）。動きは FuwaWindDeco（近くの FuwaWindArea に連動）。
public static class FuwaWindDecoBuilder
{
    const string MeshDir = "Assets/FuwaCourse/Meshes/WindDeco";
    const string MatDir = "Assets/FuwaCourse/Materials/";

    // ---- メッシュ ----
    static Mesh Save(Mesh m, string name)
    {
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "WindDeco");
        string p = MeshDir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        m.name = name;
        if (old != null) { EditorUtility.CopySerialized(m, old); EditorUtility.SetDirty(old); return old; }
        AssetDatabase.CreateAsset(m, p); return m;
    }

    // y方向の筒（下端 y0、上端 y1）
    static void Tube(List<Vector3> V, List<int> T, float r0, float r1, float y0, float y1, int seg, bool caps)
    {
        int b = V.Count;
        for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; V.Add(new Vector3(Mathf.Cos(a) * r0, y0, Mathf.Sin(a) * r0)); V.Add(new Vector3(Mathf.Cos(a) * r1, y1, Mathf.Sin(a) * r1)); }
        for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; int a0 = b + i * 2, a1 = a0 + 1, b0 = b + j * 2, b1 = b0 + 1; T.AddRange(new[] { a0, a1, b1, a0, b1, b0 }); }
        if (!caps) return;
        int c0 = V.Count; V.Add(new Vector3(0, y0, 0)); int c1 = V.Count; V.Add(new Vector3(0, y1, 0));
        for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; T.AddRange(new[] { c0, b + j * 2, b + i * 2 }); T.AddRange(new[] { c1, b + i * 2 + 1, b + j * 2 + 1 }); }
    }

    static Mesh Finish(List<Vector3> V, List<List<int>> subs, bool flat)
    {
        var m = new Mesh();
        if (flat)
        {
            // 面ごとに頂点を分けてカクカクに
            var nv = new List<Vector3>(); var ns = new List<List<int>>();
            foreach (var s in subs) { var t = new List<int>(); for (int i = 0; i < s.Count; i++) { t.Add(nv.Count); nv.Add(V[s[i]]); } ns.Add(t); }
            V = nv; subs = ns;
        }
        m.SetVertices(V); m.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 柱（高さ h）＋てっぺんの玉
    public static Mesh PoleMesh(float h)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        Tube(V, T, 0.035f, 0.03f, 0f, h, 10, true);
        // 玉
        int b = V.Count; int la = 6, lo = 10; float r = 0.06f;
        for (int i = 0; i <= la; i++) for (int j = 0; j < lo; j++) { float th = Mathf.PI * i / la, ph = Mathf.PI * 2 * j / lo; V.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * r, h + 0.04f + Mathf.Cos(th) * r, Mathf.Sin(th) * Mathf.Sin(ph) * r)); }
        for (int i = 0; i < la; i++) for (int j = 0; j < lo; j++) { int a = b + i * lo + j, c = b + i * lo + (j + 1) % lo, d = a + lo, e = c + lo; T.AddRange(new[] { a, c, e, a, e, d }); }
        return Finish(V, new List<List<int>> { T }, false);
    }

    // 吹き流し：ローカル +Z へ伸びる、口から先へ細くなる筒。縞（赤・白）を2つのサブメッシュに。口の輪は3つ目
    public static Mesh SockMesh()
    {
        const float L = 1.15f, R0 = 0.17f, R1 = 0.07f; const int seg = 14, bands = 5, per = 3;
        var V = new List<Vector3>(); var red = new List<int>(); var white = new List<int>(); var ring = new List<int>();
        int rows = bands * per + 1;
        for (int k = 0; k < rows; k++)
        {
            float t = k / (float)(rows - 1); float r = Mathf.Lerp(R0, R1, t);
            for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; V.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, t * L)); }
        }
        for (int k = 0; k < rows - 1; k++)
        {
            var lst = ((k / per) % 2 == 0) ? red : white;
            for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; int a = k * seg + i, b = k * seg + j, c = a + seg, d = b + seg; lst.AddRange(new[] { a, c, d, a, d, b }); }
        }
        // 口の金属の輪
        int rb = V.Count; float rr = 0.012f;
        for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            for (int j = 0; j < 6; j++) { float p = j * Mathf.PI * 2 / 6; V.Add(o * (R0 + Mathf.Cos(p) * rr) + new Vector3(0, 0, Mathf.Sin(p) * rr)); } }
        for (int i = 0; i < seg; i++) { int i2 = (i + 1) % seg; for (int j = 0; j < 6; j++) { int j2 = (j + 1) % 6; int a = rb + i * 6 + j, b = rb + i * 6 + j2, c = rb + i2 * 6 + j, d = rb + i2 * 6 + j2; ring.AddRange(new[] { a, b, d, a, d, c }); } }
        return Finish(V, new List<List<int>> { red, white, ring }, false);
    }

    // かざぐるま：中心から4枚の羽（角が手前へ反る）。羽ごとにサブメッシュ（色違い）＋中心の玉
    public static Mesh PinwheelMesh(float R)
    {
        var V = new List<Vector3>(); var subs = new List<List<int>>();
        for (int k = 0; k < 4; k++)
        {
            var T = new List<int>();
            float a0 = k * Mathf.PI / 2, a1 = a0 + Mathf.PI / 2;
            Vector3 P(float ang, float r, float z) => new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, z);
            // 正方形の紙を4つ折りにした羽：中心→外の角（反る）→次の辺の中ほど
            var c = P(0, 0, 0); var tip = P(a0, R, -0.12f); var mid = P((a0 + a1) * 0.5f, R * 0.72f, 0f); var half = P(a0 + 0.25f, R * 0.45f, -0.03f);
            int b = V.Count; V.Add(c); V.Add(half); V.Add(tip); V.Add(mid);
            T.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            subs.Add(T);
        }
        var hub = new List<int>(); int hb = V.Count; int la = 5, lo = 8; float r = 0.045f;
        for (int i = 0; i <= la; i++) for (int j = 0; j < lo; j++) { float th = Mathf.PI * i / la, ph = Mathf.PI * 2 * j / lo; V.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * r, Mathf.Sin(th) * Mathf.Sin(ph) * r, -0.02f + Mathf.Cos(th) * r)); }
        for (int i = 0; i < la; i++) for (int j = 0; j < lo; j++) { int a = hb + i * lo + j, cc = hb + i * lo + (j + 1) % lo, d = a + lo, e = cc + lo; hub.AddRange(new[] { a, cc, e, a, e, d }); }
        subs.Add(hub);
        return Finish(V, subs, true);
    }

    static Material Mat(string name, Color c, bool twoSided)
    {
        string p = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var tpl = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "WebTree_Leaves.mat");
        if (m == null) { m = new Material(tpl); AssetDatabase.CreateAsset(m, p); }
        else m.CopyPropertiesFromMaterial(tpl);
        m.SetColor("_Color", c); m.SetFloat("_Cull", twoSided ? 0f : 2f); m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- 並べる ----
    struct Spot { public float s; public float side; public bool sock; public string windPath; public Vector3 fixedDir; public int bridge; }   // bridge>0：人用の橋（かたまり3/4）の谷側のふちから、谷の上へ斜めに突き出す

    public static string Build()
    {
        var root = FuwaCourse1Builder.Root();
        var old = root.Find("WindDeco"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("WindDeco").transform; holder.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "winddeco");

        var poleSock = Save(PoleMesh(2.1f), "WindsockPole");
        var polePin = Save(PoleMesh(1.55f), "PinwheelPole");
        var sock = Save(SockMesh(), "Windsock");
        var pin = Save(PinwheelMesh(0.42f), "Pinwheel");
        var mPole = Mat("WindDeco_Pole", new Color(0.93f, 0.91f, 0.88f), false);
        var mRed = Mat("WindDeco_SockRed", new Color(0.95f, 0.35f, 0.38f), true);
        var mWhite = Mat("WindDeco_SockWhite", new Color(0.98f, 0.96f, 0.93f), true);
        var mRing = Mat("WindDeco_Ring", new Color(0.72f, 0.70f, 0.74f), false);
        var mWood = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Course_Wood.mat");
        var mP = new[] { Mat("WindDeco_PinPink", new Color(1f, 0.62f, 0.75f), true), Mat("WindDeco_PinYellow", new Color(1f, 0.88f, 0.42f), true),
                         Mat("WindDeco_PinMint", new Color(0.55f, 0.88f, 0.76f), true), Mat("WindDeco_PinLavender", new Color(0.72f, 0.64f, 0.95f), true) };

        var m = FuwaCourse1Builder.ToCourse(root);
        Vector3 W(int pc, float s, float lat, float dy) => root.TransformPoint(m.MultiplyPoint3x4(FuwaCourse1Builder.Point(pc, s, lat, dy)));
        int Piece(float s) { var P = FuwaCourse1Builder.Pieces; for (int i = 0; i < 3; i++) if (s >= P[i].x && s <= P[i].y) return i; return 0; }

        // 横風のエリアの中心に一番近い s
        float NearestS(Vector3 p) { float best = 0, bd = 1e9f; for (float s = 0; s < FuwaCourse1Builder.PathEnd; s += 0.25f) { var q = W(Piece(s), s, 0, 0); float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = s; } } return best; }

        var gk = root.Find("Gimmicks_Kaze");
        var spots = new List<Spot>();
        // 追い風：吹き流しとかざぐるまを左右交互に
        spots.Add(new Spot { s = 3.6f, side = 1, sock = true, windPath = "G1_Tailwind/Zone0" });
        spots.Add(new Spot { s = 6.8f, side = -1, sock = false, windPath = "G1_Tailwind/Zone1" });
        spots.Add(new Spot { s = 10.2f, side = 1, sock = false, windPath = "G1_Tailwind/Zone3" });
        spots.Add(new Spot { s = 12.8f, side = -1, sock = true, windPath = "G1_Tailwind/Zone4" });
        // 横風：風上側に吹き流し、風下側にかざぐるま
        foreach (var (g, z) in new[] { ("G2_CrossLeft", "G2_CrossLeft/CrossWind"), ("G3_CrossRight", "G3_CrossRight/CrossWind") })
        {
            var wa = gk.Find(z); if (wa == null) continue;
            float s = NearestS(wa.position);
            var area = wa.GetComponent<FuwaWindArea>();
            var dir = wa.TransformDirection(area.localDirection);
            // 道の右向き
            var r = (W(0, s, 1, 0) - W(0, s, 0, 0)).normalized;
            float upwind = Vector3.Dot(dir, r) > 0 ? -1 : 1;
            spots.Add(new Spot { s = s - 1.6f, side = upwind, sock = true, windPath = z });
            spots.Add(new Spot { s = s + 1.6f, side = -upwind, sock = false, windPath = z });
        }
        // 突風（谷の手前）：かざぐるま
        // 突風（谷）：人用の橋の谷側のふちから、谷の上へ斜めに突き出して、強風でぷるぷる
        spots.Add(new Spot { s = (FuwaCourse1Builder.GapA1 + FuwaCourse1Builder.GapB1) * 0.5f - 0.6f, sock = false, windPath = "G3_Gust1/GustZone", bridge = 3 });
        spots.Add(new Spot { s = (FuwaCourse1Builder.GapA2 + FuwaCourse1Builder.GapB2) * 0.5f - 0.6f, sock = false, windPath = "G4_Gust2/GustZone", bridge = 4 });

        string log = "";
        int n = 0;
        foreach (var sp in spots)
        {
            int pc = sp.bridge > 0 ? sp.bridge : Piece(sp.s);
            float hw = FuwaCourse1Builder.Width(pc, sp.s) * 0.5f;
            Vector3 basePos; Quaternion baseRot = Quaternion.identity; float poleScale = 1f;
            if (sp.bridge > 0)
            {
                // 橋の谷側（右）のふちの下から、谷の中ほどへ斜めに
                basePos = W(pc, sp.s, hw - 0.05f, -0.12f);
                var headPos = W(pc, sp.s + 0.3f, hw + 1.25f, 1.15f);
                var d = headPos - basePos;
                baseRot = Quaternion.FromToRotation(Vector3.up, d.normalized);
                poleScale = d.magnitude / 1.48f;
            }
            else
            {
                // 柱は道の横の面にくっつける（道のふちのすぐ外、下端は道の底）
                basePos = W(pc, sp.s, sp.side * (hw + 0.05f), -FuwaCourse1Builder.Thick);
            }
            var go = new GameObject((sp.sock ? "Windsock" : "Pinwheel") + n++);
            go.transform.SetParent(holder, true); go.transform.position = basePos; go.transform.rotation = baseRot;
            var deco = UdonSharpUndo.AddComponent<FuwaWindDeco>(go);
            var wa = gk.Find(sp.windPath); deco.wind = wa != null ? wa.GetComponent<FuwaWindArea>() : null;
            if (sp.sock)
            {
                var pole = Child(go.transform, "Pole", poleSock, new[] { mPole }, Vector3.zero);
                float top = 2.1f - 0.12f;
                var pivot = new GameObject("SockPivot").transform; pivot.SetParent(go.transform, false); pivot.localPosition = new Vector3(0, top, 0);
                var so = Child(pivot, "Sock", sock, new[] { mRed, mWhite, mRing }, new Vector3(0, 0, 0.03f));
                deco.sock = pivot;
            }
            else
            {
                var pole = Child(go.transform, "Pole", polePin, new[] { mWood }, Vector3.zero); pole.localScale = new Vector3(1, poleScale, 1);
                var head = new GameObject("Head").transform; head.SetParent(go.transform, false); head.localPosition = new Vector3(0, 1.48f * poleScale, 0);
                if (sp.bridge > 0) deco.shake = 1f;
                var rotor = new GameObject("Rotor").transform; rotor.SetParent(head, false); rotor.localPosition = new Vector3(0, 0, -0.06f);
                Child(rotor, "Blades", pin, new[] { mP[n % 4], mP[(n + 1) % 4], mP[(n + 2) % 4], mP[(n + 3) % 4], mPole }, Vector3.zero);
                deco.head = head; deco.rotor = rotor;
            }
            UdonSharpEditorUtility.CopyProxyToUdon(deco);
            log += go.name + " s=" + sp.s.ToString("F1") + " wind=" + (deco.wind != null ? sp.windPath : "none") + "\n";
        }
        EditorUtility.SetDirty(root.gameObject);
        AssetDatabase.SaveAssets();
        return log;
    }

    static Transform Child(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 lp)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.transform.localPosition = lp;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    [MenuItem("FuwaCourse/Course1/Build Wind Deco")]
    static void Menu() { Debug.Log(Build()); }
}
