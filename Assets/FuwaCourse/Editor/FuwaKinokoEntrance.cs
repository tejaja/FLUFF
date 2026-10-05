using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// きのこのたにの入口（スタートの先〜チェックポイント1の手前）を作る。何度実行しても同じ結果。
// 入口（平ら）→ すき間（下から生えたキノコの傘で渡る）→ 木の坂 → すき間（次の坂の正面にサルノコシカケ）→ 木の坂 → 上の段
// ・道のメッシュ（見た目と判定）からこの区間を切り取って、木の床（板張り）と判定の箱を置く
// ・道は一直線（O から D の向き）。d＝その向きの距離、s＝横の距離
public static class FuwaKinokoEntrance
{
    static readonly Vector3 O = new Vector3(-22.920f, 0f, -44.641f);
    static readonly Vector3 D = new Vector3(-0.7817f, 0f, -0.6237f).normalized;
    static Vector3 S => Vector3.Cross(Vector3.up, D);

    // 区間（d）と高さ
    public static float CutA = -6.0f, CutB = 6.6f;          // 道を切り取る範囲
    public static float FloorY = 8.0f;                       // 入口の高さ
    public static float Gap1A = -6.0f, Gap1B = -4.4f;        // すき間1（キノコ）
    public static float SlopeA0 = -4.4f, SlopeA1 = -0.4f, FlatA1 = 0.8f, YA1 = 9.8f;   // 坂A → 平ら
    public static float Gap2B = 2.4f;                        // すき間2（サルノコシカケ）＝ FlatA1〜Gap2B
    public static float YB0 = 10.2f, YB1 = 12.10f;           // 坂B（Gap2B〜CutB）
    public static float HalfW = 1.5f;
    public static float CapScale = 0.6f, ShelfScale = 0.32f, ShelfTopY = 10.0f;

    const string MeshDir = "Assets/FuwaCourse/Meshes";

    [MenuItem("FuwaCourse/きのこのたにの入口を作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    static Vector3 W(float d, float s, float y) { var p = O + D * d + S * s; p.y = y; return p; }
    static float Dd(Vector3 w) => Vector3.Dot(w - O, D);
    static float Ss(Vector3 w) => Vector3.Dot(w - O, S);

    public static string Run()
    {
        var c5 = GameObject.Find("FuwaCourse5").transform;
        var pm = c5.Find("PathMesh"); var log = "";

        // ---- 1. 道を切り取る（元のメッシュは残して、切った物を別のアセットに） ----
        var mf = pm.GetComponent<MeshFilter>(); var mc = pm.GetComponent<MeshCollider>();
        var visSrc = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Bevel/Course5_Path_Bevel_Stairs3.asset");
        var colSrc = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Bevel/Course5_Path_Cut3.asset");
        mf.sharedMesh = CutMesh(visSrc, pm, MeshDir + "/Bevel/Course5_Path_Bevel_Entrance.asset", out int cv);
        var colMesh = CutMesh(colSrc, pm, MeshDir + "/Bevel/Course5_Path_CutEntrance.asset", out int cc);
        mc.sharedMesh = null; mc.sharedMesh = colMesh;
        var bs = pm.Find("BallStairs1"); if (bs) Object.DestroyImmediate(bs.gameObject);
        log += "cut vis " + cv + " col " + cc + " / ";

        // ---- 2. 木の床 ----
        var old = c5.Find("KinokoEntrance"); if (old) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("KinokoEntrance").transform; root.SetParent(c5, true);
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Generated/Den/Den_Twig.mat");
        var rnd = new System.Random(5);
        var mb = new MB();
        // 坂A＋平らA
        Deck(mb, SlopeA0, FloorY, SlopeA1, YA1, rnd); Deck(mb, SlopeA1, YA1, FlatA1, YA1, rnd);
        // 坂B
        Deck(mb, Gap2B, YB0, CutB + 0.05f, YB1, rnd);
        // 切り口のふた（入口の先、上の段の手前）と、坂Bの正面（サルノコシカケが生える板）
        EndCap(mb, Gap1A, FloorY, 1.0f);
        EndCap(mb, CutB, YB1, 1.0f);
        FrontBoards(mb, FlatA1, YA1, 0.9f, -1f, rnd);     // 平らAの先（すき間2の手前）
        FrontBoards(mb, Gap2B, YB0, 1.3f, 1f, rnd);        // 坂Bの正面（サルノコシカケの生える所）
        FrontBoards(mb, SlopeA0, FloorY, 0.7f, 1f, rnd);   // 坂Aの下のふち
        var mesh = mb.Build("KinokoEntrance_Deck", root);
        var go = new GameObject("Deck", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat; go.isStatic = true;

        // ---- 3. 判定（箱） ----
        var cols = new GameObject("Colliders").transform; cols.SetParent(root, false); cols.gameObject.layer = 11;
        SlopeBox(cols, "SlopeA", SlopeA0, FloorY, SlopeA1, YA1);
        SlopeBox(cols, "FlatA", SlopeA1, YA1, FlatA1, YA1);
        SlopeBox(cols, "SlopeB", Gap2B, YB0, CutB + 0.05f, YB1);

        // ---- 4. キノコとサルノコシカケ ----
        var mush = c5.Find("Mushrooms");
        foreach (var n in new[] { "PitCap2", "PitCap3" }) { var t = mush.Find(n); if (t) Object.DestroyImmediate(t.gameObject); }
        var cap = mush.Find("PitCap1") ?? mush.Find("EntranceCap");
        if (cap != null)
        {
            cap.name = "EntranceCap"; cap.localScale = Vector3.one * CapScale;
            cap.position = W((Gap1A + Gap1B) * 0.5f, 0f, FloorY);
            var st = cap.Find("Stem"); if (st) { st.localScale = Vector3.one; st.localPosition = new Vector3(0, -3.28f, 0); }
            var j = cap.GetComponentInChildren<FuwaJumpBoost>(); j.radius = 1.15f * CapScale;
            if (j.boostRings) { var m = j.boostRings.main; m.startSize = j.radius * 2f * 1.3f; }
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j);
        }
        var shelf = mush.Find("PitShelf");
        if (shelf != null)
        {
            shelf.localScale = Vector3.one * ShelfScale; shelf.rotation = Quaternion.LookRotation(-D);
            var top = shelf.Find("Top");
            shelf.position += W(Gap2B - 0.8f, 0f, ShelfTopY) - top.position;
            // 奥のふちを坂Bの正面に少しめりこませる
            Physics.SyncTransforms();
            float maxD = -99; foreach (var r in shelf.Find("Visual").GetComponentsInChildren<MeshFilter>()) foreach (var v in r.sharedMesh.vertices) maxD = Mathf.Max(maxD, Dd(r.transform.TransformPoint(v)));
            shelf.position += D * (Gap2B + 0.08f - maxD);
            var j = shelf.GetComponentInChildren<FuwaJumpBoost>(); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j);
        }

        // ---- 5. もう使わない物 ----
        var ws = c5.Find("WoodStairs1"); if (ws) ws.gameObject.SetActive(false);
        Physics.SyncTransforms();
        int hidden = 0;
        foreach (var r in c5.Find("Deco").GetComponentsInChildren<Renderer>())
        {
            var b = r.bounds; float d = Dd(b.center), s = Ss(b.center);
            if (d < CutA - 0.3f || d > CutB + 0.3f || Mathf.Abs(s) > 2.2f || b.size.magnitude > 4f) continue;
            // 下に地面が無い、または床に埋まっている小物は隠す
            bool ground = Physics.Raycast(new Vector3(b.center.x, b.min.y + 0.25f, b.center.z), Vector3.down, out var h, 0.5f, 1 << 11 | 1, QueryTriggerInteraction.Ignore);
            bool buried = Physics.Raycast(new Vector3(b.center.x, b.max.y + 3f, b.center.z), Vector3.down, out var h2, 3f, 1 << 11 | 1, QueryTriggerInteraction.Ignore) && h2.point.y > b.max.y - 0.05f;
            if (!ground || buried) { r.gameObject.SetActive(false); hidden++; }
        }
        log += "deco hidden " + hidden;
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c5.gameObject.scene);
        return log;
    }

    static Mesh CutMesh(Mesh src, Transform t, string path, out int removed)
    {
        var v = src.vertices; var keep = new List<List<int>>(); removed = 0;
        for (int sm = 0; sm < src.subMeshCount; sm++)
        {
            var tr = src.GetTriangles(sm); var k = new List<int>();
            for (int i = 0; i < tr.Length; i += 3)
            {
                var c = (t.TransformPoint(v[tr[i]]) + t.TransformPoint(v[tr[i + 1]]) + t.TransformPoint(v[tr[i + 2]])) / 3f;
                float d = Dd(c), s = Ss(c);
                if (d > CutA && d < CutB && Mathf.Abs(s) < 1.85f && c.y > 6f && c.y < 13.5f) { removed++; continue; }
                k.Add(tr[i]); k.Add(tr[i + 1]); k.Add(tr[i + 2]);
            }
            keep.Add(k);
        }
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
        m.Clear(); m.name = System.IO.Path.GetFileNameWithoutExtension(path); m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = v; if (src.normals.Length == v.Length) m.normals = src.normals;
        if (src.uv.Length == v.Length) m.uv = src.uv; if (src.uv2.Length == v.Length) m.uv2 = src.uv2; if (src.colors.Length == v.Length) m.colors = src.colors;
        m.subMeshCount = keep.Count; for (int i = 0; i < keep.Count; i++) m.SetTriangles(keep[i], i);
        m.RecalculateBounds(); EditorUtility.SetDirty(m);
        return m;
    }

    // ---- 木の部品 ----
    class MB
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>(); public List<Color> C = new List<Color>(); public List<int> T = new List<int>();
        // 中心・3軸（半分の長さつき）の箱。下向きの面は少し暗く
        public void Box(Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, Color col)
        {
            Vector3[] n = { ax.normalized, -ax.normalized, ay.normalized, -ay.normalized, az.normalized, -az.normalized };
            foreach (var f in n)
            {
                Vector3 u, w;
                if (f == ax.normalized || f == -ax.normalized) { u = ay; w = az; }
                else if (f == ay.normalized || f == -ay.normalized) { u = az; w = ax; }
                else { u = ax; w = ay; }
                Vector3 fc = c + (f == ax.normalized ? ax : f == -ax.normalized ? -ax : f == ay.normalized ? ay : f == -ay.normalized ? -ay : f == az.normalized ? az : -az);
                var cc = f.y < -0.5f ? col * 0.8f : col; cc.a = 1;
                int b = V.Count;
                V.Add(fc - u - w); V.Add(fc + u - w); V.Add(fc + u + w); V.Add(fc - u + w);
                for (int i = 0; i < 4; i++) { N.Add(f); C.Add(cc); }
                if (Vector3.Dot(Vector3.Cross(V[b + 1] - V[b], V[b + 2] - V[b]), f) > 0) T.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                else T.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
        }
        public Mesh Build(string name, Transform space)
        {
            string path = MeshDir + "/" + name + ".asset"; var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
            m.Clear(); m.name = name; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V.Select(p => space.InverseTransformPoint(p)).ToList()); m.SetNormals(N.Select(x => space.InverseTransformDirection(x)).ToList());
            m.SetColors(C); m.SetTriangles(T, 0); m.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(m); EditorUtility.SetDirty(m); return m;
        }
    }

    static readonly Color[] PlankCols = { new Color(0.695f, 0.509f, 0.343f), new Color(0.663f, 0.472f, 0.311f), new Color(0.62f, 0.44f, 0.29f), new Color(0.72f, 0.54f, 0.37f) };
    static readonly Color BeamCol = new Color(0.368f, 0.252f, 0.174f);
    static Color Plank(System.Random r) { var c = PlankCols[r.Next(PlankCols.Length)] * (0.93f + 0.14f * (float)r.NextDouble()); c.a = 1; return c; }

    // d0〜d1 を y0〜y1 で結ぶ板張りの床（板は横向き）＋両側の桁
    static void Deck(MB mb, float d0, float y0, float d1, float y1, System.Random rnd)
    {
        var a = W(d0, 0, y0); var b = W(d1, 0, y1); var along = (b - a); float len = along.magnitude; var fw = along / len;
        var up = Vector3.Cross(fw, S).normalized; if (up.y < 0) up = -up;
        const float pw = 0.28f, th = 0.08f; int n = Mathf.Max(1, Mathf.RoundToInt(len / pw)); float step = len / n;
        // 板ごとに：はしを不ぞろいに欠けさせ（左右の長さがばらばら）、少し傾けてガタつかせる。見た目だけ（判定は箱のまま）
        float R(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        for (int i = 0; i < n; i++)
        {
            float cutL = rnd.NextDouble() < 0.35 ? R(0.05f, 0.28f) : R(0f, 0.04f);
            float cutR = rnd.NextDouble() < 0.35 ? R(0.05f, 0.28f) : R(0f, 0.04f);
            float half = HalfW - (cutL + cutR) * 0.5f; float shift = (cutL - cutR) * 0.5f;
            var c = a + fw * (step * (i + 0.5f) + R(-0.015f, 0.015f)) - up * (th * 0.5f) + S * shift + up * R(-0.012f, 0.025f);
            var q = Quaternion.AngleAxis(R(-2.2f, 2.2f), fw) * Quaternion.AngleAxis(R(-1.8f, 1.8f), up) * Quaternion.AngleAxis(R(-1.5f, 1.5f), S);
            mb.Box(c, q * (S * half), q * (up * (th * 0.5f)), q * (fw * (step * 0.5f - 0.012f)), Plank(rnd));
        }
        foreach (var sg in new[] { -1f, 1f })
        {
            var c = (a + b) * 0.5f + S * sg * (HalfW - 0.42f) - up * (th + 0.15f);   // 板のはしが欠けても見えないように内側に
            mb.Box(c, S * 0.07f, up * 0.15f, fw * (len * 0.5f), BeamCol);
        }
    }

    static void EndCap(MB mb, float d, float topY, float h)
    {
        var c = W(d, 0, topY - h * 0.5f);
        mb.Box(c, S * (HalfW + 0.05f), Vector3.up * (h * 0.5f), D * 0.08f, BeamCol);
    }

    // 段の正面に横板を積む（dir：板を置く側 -1＝手前、1＝奥）
    static void FrontBoards(MB mb, float d, float topY, float h, float dir, System.Random rnd)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(h / 0.3f)); float bh = h / n;
        for (int i = 0; i < n; i++)
        {
            var c = W(d + dir * 0.04f, 0, topY - 0.08f - bh * (i + 0.5f));
            var col = BeamCol * (1.05f + 0.1f * (float)rnd.NextDouble()); col.a = 1;
            mb.Box(c, S * (HalfW + 0.02f), Vector3.up * (bh * 0.5f - 0.01f), D * 0.04f, col);
        }
    }

    static void SlopeBox(Transform parent, string name, float d0, float y0, float d1, float y1)
    {
        var a = W(d0, 0, y0); var b = W(d1, 0, y1); var along = b - a; float len = along.magnitude;
        var go = new GameObject(name); go.transform.SetParent(parent, true); go.layer = 11;
        var fw = along / len; var up = Vector3.Cross(fw, S).normalized; if (up.y < 0) up = -up;
        go.transform.position = (a + b) * 0.5f - up * 0.15f; go.transform.rotation = Quaternion.LookRotation(fw, up);
        var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(HalfW * 2f, 0.3f, len);
        go.isStatic = true;
    }
}
