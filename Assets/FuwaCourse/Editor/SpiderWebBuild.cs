// SpiderWebBuilder の中身を作るエディタ側の処理と、インスペクター・メニュー。
// ・値を変えると、その巣のマテリアル・当たり判定・支え（木・浮島・糸）を作り直す
// ・メニュー「FuwaCourse/Spider Web/Rebuild All」でシーン中の巣をまとめて作り直す
// ・メニュー「GameObject/FuwaCourse/Spider Web」で新しい巣を置く
// 作ったメッシュとマテリアルは Assets/FuwaCourse/Generated/SpiderWebs/ に巣ごとに保存する。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SpiderWebBuild
{
    const string Folder = "Assets/FuwaCourse/Generated/SpiderWebs";
    const string MatDir = "Assets/FuwaCourse/Materials/";

    // ───────── メニュー ─────────

    [MenuItem("FuwaCourse/Spider Web/Rebuild All")]
    public static void RebuildAllMenu() { Debug.Log(RebuildAll()); }

    public static string RebuildAll()
    {
        var log = new System.Text.StringBuilder();
        // 非表示のコースも地面の判定（Raycast）ができるように、一時的に全部表示する
        var restore = new Dictionary<GameObject, bool>();
        foreach (var b in AllBuilders())
            for (Transform t = b.transform; t != null; t = t.parent)
                if (!t.gameObject.activeSelf && !restore.ContainsKey(t.gameObject)) { restore[t.gameObject] = false; t.gameObject.SetActive(true); }
        try
        {
            foreach (var b in AllBuilders()) log.AppendLine(Build(b));
        }
        finally
        {
            foreach (var kv in restore) kv.Key.SetActive(kv.Value);
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    [MenuItem("GameObject/FuwaCourse/Spider Web", false, 10)]
    static void CreateWeb(MenuCommand cmd)
    {
        var go = new GameObject("SpiderWeb");
        Undo.RegisterCreatedObjectUndo(go, "Create Spider Web");
        if (cmd.context is GameObject parent) go.transform.SetParent(parent.transform, false);
        else if (SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;

        var front = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(front.GetComponent<Collider>());
        front.name = "Front";
        front.transform.SetParent(go.transform, false);

        var hole = new GameObject("Hole").transform;
        hole.SetParent(go.transform, false);
        var ring = new GameObject("Ring");
        ring.transform.SetParent(hole, false);
        ring.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/WebRing.asset");
        ring.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "WebRing.mat");

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;

        var hazard = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(go);
        hazard.sticky = true;
        hazard.holeCenter = hole;
        hazard.playerSlow = 0.5f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hazard);

        var webHole = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWebHole>(go);
        webHole.webRenderers = new Renderer[] { front.GetComponent<Renderer>() };
        webHole.ring = ring.transform;
        webHole.hazard = hazard;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(webHole);

        var b = Undo.AddComponent<SpiderWebBuilder>(go);
        b.webRenderer = front.GetComponent<Renderer>();
        b.hole = hole;
        Build(b);
        Selection.activeGameObject = go;
    }

    // ───────── 本体 ─────────

    public static IEnumerable<SpiderWebBuilder> AllBuilders()
    {
        foreach (var b in Resources.FindObjectsOfTypeAll<SpiderWebBuilder>())
            if (b != null && b.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(b)) yield return b;
    }

    public static string Build(SpiderWebBuilder b)
    {
        EnsureAssetId(b);
        EnsureFolder();
        string id = b.assetId;
        Transform tr = b.transform;
        Vector2 hs = b.size * 0.5f;

        // 穴の大きさ：FuwaWebHole があればそちら（呼吸するなら一番大きい時で輪の出し方を決める）
        float hr = b.holeRadius, hrMax = b.holeRadius;
        var webHole = b.GetComponent<FuwaWebHole>();
        if (webHole != null)
        {
            hr = webHole.holeRadius;
            hrMax = webHole.breathe ? Mathf.Max(webHole.maxRadius, webHole.holeRadius) : webHole.holeRadius;
            // 金の輪もエディタ上で穴の大きさに合わせておく（実行中は FuwaWebHole が動かす）
            if (webHole.ring != null)
            {
                Undo.RecordObject(webHole.ring, "Spider Web");
                webHole.ring.localScale = Vector3.one * (hr * 0.98f / Mathf.Max(0.01f, webHole.ringInnerRadius));
            }
        }

        // マテリアル（巣ごとに1つ）
        var mat = LoadOrCreate<Material>(Folder + "/" + id + "_Web.mat", () => new Material(Shader.Find("FuwaCourse/SpiderWeb")));
        mat.SetColor("_Color", b.color);
        mat.SetVector("_Size", new Vector4(b.size.x, b.size.y, 0, 0));
        mat.SetVector("_HoleCenter", new Vector4(b.holeCenter.x, b.holeCenter.y, 0, 0));
        mat.SetFloat("_HoleRadius", hr);
        mat.SetFloat("_HoleRadiusMax", hrMax);
        mat.SetFloat("_LineWidth", b.lineWidth);
        mat.SetFloat("_Spokes", b.spokes);
        mat.SetFloat("_RingStart", b.ringStart);
        mat.SetFloat("_RingGrow", b.ringGrow);
        mat.SetFloat("_Seed", b.seed);
        mat.SetFloat("_OuterMin", b.outerMin);
        mat.SetFloat("_OuterMax", Mathf.Max(b.outerMin, b.outerMax));
        mat.SetFloat("_Round", b.cornerRound);
        {
            // 枠の多角形の角（シェーダーには穴の中心基準で渡す）
            var fc = b.polygonFrame ? FrameCorners(b) : new List<Vector2>();
            var fv = new Vector4[4];
            for (int k = 0; k < fc.Count && k < 8; k++)
            {
                var q = fc[k] - b.holeCenter;
                if (k % 2 == 0) { fv[k / 2].x = q.x; fv[k / 2].y = q.y; } else { fv[k / 2].z = q.x; fv[k / 2].w = q.y; }
            }
            mat.SetFloat("_FrameN", fc.Count);
            mat.SetVector("_F01", fv[0]); mat.SetVector("_F23", fv[1]); mat.SetVector("_F45", fv[2]); mat.SetVector("_F67", fv[3]);
        }
        EditorUtility.SetDirty(mat);

        if (b.webRenderer != null)
        {
            Undo.RecordObject(b.webRenderer, "Spider Web");
            b.webRenderer.sharedMaterial = mat;
            var wt = b.webRenderer.transform;
            Undo.RecordObject(wt, "Spider Web");
            Vector3 ps = wt.parent != null ? wt.parent.lossyScale : Vector3.one;
            Vector3 bs = tr.parent != null ? tr.parent.lossyScale : Vector3.one;
            wt.localScale = new Vector3(b.size.x * bs.x / ps.x, b.size.y * bs.y / ps.y, 1f);
            // 裏用の板（180度回した Back）は形が左右反転するので使わない。シェーダーが両面を描く
            var back = wt.parent != null ? wt.parent.Find("Back") : null;
            if (back != null && back.GetComponent<Renderer>() is Renderer br)
            {
                Undo.RecordObject(br, "Spider Web");
                br.sharedMaterial = mat;
                br.enabled = false;
            }
        }

        if (b.hole != null)
        {
            Undo.RecordObject(b.hole, "Spider Web");
            b.hole.position = tr.position + tr.rotation * new Vector3(b.holeCenter.x, b.holeCenter.y, 0);
        }

        var verts = Outline(b);

        if (b.fitCollider) FitCollider(b, verts, id);

        // 支え（木・糸）は「WebSupports」の下に作る。親の拡大縮小を打ち消して、中はメートルで扱う
        var sup = tr.Find("WebSupports");
        if (sup != null) Undo.DestroyObjectImmediate(sup.gameObject);
        var supGo = new GameObject("WebSupports");
        Undo.RegisterCreatedObjectUndo(supGo, "Spider Web");
        sup = supGo.transform;
        sup.SetParent(tr, false);
        Vector3 ls = tr.lossyScale;
        Vector3 pls = tr.parent != null ? tr.parent.lossyScale : Vector3.one;
        sup.localScale = new Vector3(pls.x / ls.x, pls.y / ls.y, pls.z / ls.z);

        int threads = BuildSupports(b, sup, verts, id);
        return tr.root.name + "/" + tr.name + " threads=" + threads;
    }

    static void EnsureAssetId(SpiderWebBuilder b)
    {
        bool dup = false;
        foreach (var o in AllBuilders()) if (o != b && o.assetId == b.assetId) dup = true;
        if (string.IsNullOrEmpty(b.assetId) || dup)
        {
            Undo.RecordObject(b, "Spider Web");
            b.assetId = "Web_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            EditorUtility.SetDirty(b);
        }
    }

    static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(Folder)) return;
        string cur = "Assets";
        foreach (var part in Folder.Substring("Assets/".Length).Split('/'))
        {
            string next = cur + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, part);
            cur = next;
        }
    }

    static T LoadOrCreate<T>(string path, System.Func<T> make) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null) { a = make(); AssetDatabase.CreateAsset(a, path); }
        return a;
    }

    // メッシュを保存（すでにあれば中身を差し替える。参照が切れないように）
    static Mesh SaveMesh(string path, Mesh src)
    {
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) { AssetDatabase.CreateAsset(src, path); return src; }
        ex.Clear();
        ex.indexFormat = src.indexFormat;
        ex.SetVertices(src.vertices);
        ex.SetNormals(src.normals);
        var u0 = new List<Vector4>(); src.GetUVs(0, u0); if (u0.Count > 0) ex.SetUVs(0, u0);
        var u1 = new List<Vector4>(); src.GetUVs(1, u1); if (u1.Count > 0) ex.SetUVs(1, u1);
        ex.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++) ex.SetTriangles(src.GetTriangles(s), s);
        ex.RecalculateBounds();
        EditorUtility.SetDirty(ex);
        return ex;
    }

    // 巣の外周の点（FuwaSpiderWeb シェーダーの outerRadius と同じ式）。巣の中心基準、メートル
    // 枠の多角形の角（板の中心基準、メートル）。穴の中心から見た角度順。板のフチの少し内側に置く
    public static List<Vector2> FrameCorners(SpiderWebBuilder b)
    {
        Vector2 hs = b.size * 0.5f, c = b.holeCenter;
        int n = Mathf.Clamp(b.frameCorners, 3, 8);
        var pts = new List<Vector2>();
        if (b.left.anchor == SpiderWebBuilder.AnchorType.Tree && b.right.anchor == SpiderWebBuilder.AnchorType.Tree)
        {
            // 両側に木がある大きい巣：木と木の間をふさぐ役目があるので、板いっぱいの6角形（四隅＋左右の幹の高さ）にする。
            // 上辺・下辺はまっすぐな橋糸、左右の角は幹へ。少しずつずらして四角すぎないように
            float s = b.seed;
            Vector2 J(float k, float amp) => new Vector2(Mathf.Sin(k * 1.7f + s * 0.9f), Mathf.Sin(k * 2.9f + s * 1.3f)) * amp;
            var raw = new List<Vector2>
            {
                // 上の2つは内側へ寄せて高さも変える（上辺が斜めの橋糸に）、左右は幹ぎわで高さ違い、下は道のふち
                new Vector2(hs.x * 0.62f, hs.y * 0.97f) + J(1, 0.05f * hs.x),
                new Vector2(-hs.x * 0.55f, hs.y * 0.82f) + J(2, 0.05f * hs.x),
                new Vector2(-hs.x * 0.98f, hs.y * (0.32f + 0.12f * Mathf.Sin(s))) + J(3, 0.02f * hs.x),
                new Vector2(-hs.x * 0.78f, -hs.y * 0.96f) + J(4, 0.04f * hs.x),
                new Vector2(hs.x * 0.85f, -hs.y * 0.9f) + J(5, 0.04f * hs.x),
                new Vector2(hs.x * 0.98f, hs.y * (0.12f + 0.12f * Mathf.Cos(s))) + J(6, 0.02f * hs.x),
            };
            foreach (var q in raw) pts.Add(new Vector2(Mathf.Clamp(q.x, -hs.x * 0.99f, hs.x * 0.99f), Mathf.Clamp(q.y, -hs.y * 0.99f, hs.y * 0.99f)));
            pts.Sort((p, q) => Mathf.Atan2(p.y - c.y, p.x - c.x).CompareTo(Mathf.Atan2(q.y - c.y, q.x - c.x)));
            return pts;
        }
        float a0 = 90f + 360f / n * 0.5f + 25f * Mathf.Sin(b.seed * 1.7f);
        for (int k = 0; k < n; k++)
        {
            float a = (a0 + 360f / n * k + 14f * Mathf.Sin(k * 2.3f + b.seed * 0.9f)) * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float tx = Mathf.Abs(d.x) > 1e-4f ? ((d.x > 0 ? hs.x : -hs.x) - c.x) / d.x : 1e4f;
            float ty = Mathf.Abs(d.y) > 1e-4f ? ((d.y > 0 ? hs.y : -hs.y) - c.y) / d.y : 1e4f;
            float f = 0.93f + 0.05f * Mathf.Sin(k * 1.37f + b.seed * 2.1f);
            pts.Add(c + d * Mathf.Min(tx, ty) * f);
        }
        return pts;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // 穴の中心 c から方向 d に進んで枠に当たるまでの距離（シェーダーの frameRayDist と同じ）
    static float FrameRayDist(List<Vector2> fc, Vector2 c, Vector2 d)
    {
        float best = 1e4f;
        for (int k = 0; k < fc.Count; k++)
        {
            Vector2 A = fc[k] - c, B = fc[(k + 1) % fc.Count] - c, e = B - A;
            float den = Cross(d, e);
            if (Mathf.Abs(den) < 1e-6f) continue;
            float t = Cross(A, e) / den, u = Cross(A, d) / den;
            if (t > 0 && u >= -1e-4f && u <= 1 + 1e-4f) best = Mathf.Min(best, t);
        }
        return best;
    }

    public static List<Vector2> Outline(SpiderWebBuilder b)
    {
        if (b.polygonFrame)
        {
            // 放射の糸の先＝枠に当たる所（当たり判定はこの点を結んだ形）
            var fc = FrameCorners(b);
            var vp = new List<Vector2>();
            for (int i = 0; i < b.spokes; i++)
            {
                float a = (i + Mathf.Sin(i * 1.93f + b.seed * 0.77f) * 0.18f) * 6.2831853f / b.spokes;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vp.Add(b.holeCenter + d * FrameRayDist(fc, b.holeCenter, d));
            }
            return vp;
        }
        Vector2 hs = b.size * 0.5f, c = b.holeCenter;
        float seed = b.seed, n = b.spokes, omin = b.outerMin, omax = Mathf.Max(b.outerMin, b.outerMax);
        Vector2 he = hs + new Vector2(Mathf.Abs(c.x), Mathf.Abs(c.y));
        var verts = new List<Vector2>();
        for (int i = 0; i < b.spokes; i++)
        {
            float k = i;
            float a = (i + Mathf.Sin(k * 1.93f + seed * 0.77f) * 0.18f) * 6.2831853f / n;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float tx = Mathf.Abs(d.x) > 1e-4f ? ((d.x > 0 ? hs.x : -hs.x) - c.x) / d.x : 1e4f;
            float ty = Mathf.Abs(d.y) > 1e-4f ? ((d.y > 0 ? hs.y : -hs.y) - c.y) / d.y : 1e4f;
            float te = 1f / Mathf.Sqrt(d.x * d.x / (he.x * he.x) + d.y * d.y / (he.y * he.y));
            float w = 0.65f * Mathf.Sin(k * 0.7f + seed * 0.9f) + 0.35f * Mathf.Sin(k * 2.399f + seed * 1.3f);
            verts.Add(c + d * Mathf.Min(Mathf.Min(tx, ty), te * b.cornerRound) * Mathf.Lerp(omin, omax, 0.5f + 0.5f * w));
        }
        return verts;
    }

    // 外周の形の板（厚み colliderDepth）を当たり判定に。トリガーなので凸形（MeshCollider convex）
    static void FitCollider(SpiderWebBuilder b, List<Vector2> verts, string id)
    {
        Transform tr = b.transform;
        Vector3 ls = tr.lossyScale, pls = tr.parent != null ? tr.parent.lossyScale : Vector3.one;
        Vector3 inv = new Vector3(pls.x / ls.x, pls.y / ls.y, pls.z / ls.z);
        float hz = b.colliderDepth * 0.5f;
        var V = new List<Vector3>();
        foreach (var v in verts) V.Add(Vector3.Scale(new Vector3(v.x, v.y, -hz), inv));
        foreach (var v in verts) V.Add(Vector3.Scale(new Vector3(v.x, v.y, hz), inv));
        int n = verts.Count; var T = new List<int>();
        for (int i = 1; i < n - 1; i++) { T.Add(0); T.Add(i + 1); T.Add(i); T.Add(n); T.Add(n + i); T.Add(n + i + 1); }
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; T.Add(i); T.Add(j); T.Add(n + i); T.Add(j); T.Add(n + j); T.Add(n + i); }
        var m = new Mesh(); m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals();
        var saved = SaveMesh(Folder + "/" + id + "_Collider.asset", m);

        bool trigger = true;
        var box = b.GetComponent<BoxCollider>();
        if (box != null) { trigger = box.isTrigger; Undo.DestroyObjectImmediate(box); }
        var mc = b.GetComponent<MeshCollider>();
        if (mc == null) mc = Undo.AddComponent<MeshCollider>(b.gameObject);
        else Undo.RecordObject(mc, "Spider Web");
        mc.sharedMesh = null;
        mc.convex = true;
        mc.isTrigger = trigger;
        mc.sharedMesh = saved;
    }

    // ───────── 支え（木・浮島・糸） ─────────

    class ThreadMesh
    {
        public List<Vector3> V = new List<Vector3>();
        public List<Vector4> U0 = new List<Vector4>(), U1 = new List<Vector4>();
        public List<int> T = new List<int>();
        public float width;
        public int count;

        void Seg(Vector3 a, Vector3 b)
        {
            int i0 = V.Count;
            V.Add(a); V.Add(a); V.Add(b); V.Add(b);
            U0.Add(new Vector4(-1, 0, width, 0)); U0.Add(new Vector4(1, 0, width, 0));
            U0.Add(new Vector4(-1, 1, width, 0)); U0.Add(new Vector4(1, 1, width, 0));
            U1.Add(b); U1.Add(b); U1.Add(a); U1.Add(a);
            T.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3 });
        }

        // 2点の間に張った糸。真ん中が少し垂れる
        public void Hang(Vector3 a, Vector3 b, float sagFrac)
        {
            count++;
            float sag = (b - a).magnitude * sagFrac;
            int segs = sagFrac > 0 ? 12 : 1;
            Vector3 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float s = (float)i / segs;
                Vector3 p = Vector3.Lerp(a, b, s) + Vector3.down * sag * 4 * s * (1 - s);
                Seg(prev, p);
                prev = p;
            }
        }
    }

    class Tree
    {
        public float x, z, fy, height, r0;
        public Vector3[] branch;   // 巣の上へ伸びる枝（根元→先）

        // 幹の中心（高さ y の所）と、その高さでの幹の半径
        public Vector3 TrunkPoint(float y, float sgn, float surface)
        {
            float t = Mathf.Clamp01((y - fy) / height);
            float cx = x + sgn * 0.18f * height / 6f * Mathf.Pow(t, 1.6f);
            float r = r0 * (0.5f + 0.5f * (1 - t));
            return new Vector3(cx - sgn * r * surface, y, z + 0.08f * Mathf.Sin(t * 3.1f));
        }

        public Vector3 OnBranch(float t)
        {
            float fi = Mathf.Clamp01(t) * (branch.Length - 1);
            int i0 = Mathf.Min(branch.Length - 2, (int)fi);
            return Vector3.Lerp(branch[i0], branch[i0 + 1], fi - i0);
        }
    }

    static int BuildSupports(SpiderWebBuilder b, Transform sup, List<Vector2> verts, string id)
    {
        Vector2 hs = b.size * 0.5f;
        float s0 = b.size.y / 6f;   // 標準（高さ6mの巨大な巣）に対する大きさ

        // 巣のてっぺん中央（穴の真上の外周の点）
        Vector2 top = verts[0]; float best = 999;
        foreach (var v in verts)
        {
            float ang = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(v.y - b.holeCenter.y, v.x - b.holeCenter.x) * Mathf.Rad2Deg, 90));
            if (ang < best) { best = ang; top = v; }
        }

        float floorY = FloorAt(sup, new Vector3(0, -hs.y + 0.3f, 0));
        if (float.IsNaN(floorY)) floorY = -hs.y;

        var th = new ThreadMesh { width = 0.012f + 0.01f * s0 };
        var sides = new[] { (b.left, -1f, "L"), (b.right, 1f, "R") };
        int treeCount = 0;
        foreach (var sd in sides) if (sd.Item1.anchor == SpiderWebBuilder.AnchorType.Tree) treeCount++;

        var trees = new Dictionary<float, Tree>();
        foreach (var (side, sgn, name) in sides)
        {
            if (side.anchor != SpiderWebBuilder.AnchorType.Tree) continue;
            float s = s0 * side.treeScale;
            // 幹は巣と同じ面に立てる（手前や奥にずらすと、糸が幹の手前を通って浮いて見える）
            var tree = new Tree { x = sgn * (hs.x + side.treeGap), z = 0f, r0 = 0.05f + 0.23f * s };
            float fy = FloorAt(sup, new Vector3(tree.x, floorY + 0.3f, tree.z));
            bool island = float.IsNaN(fy) && b.autoIsland;
            tree.fy = float.IsNaN(fy) ? floorY : fy;
            tree.height = (hs.y - tree.fy) + hs.y * 0.5f + side.treeExtraHeight;
            // 木が片側だけなら、枝を巣の真上まで伸ばして、てっぺんからまっすぐ吊る
            float tipX = (b.topThread && treeCount == 1) ? top.x : float.NaN;
            BuildTree(sup, b, id + "_Tree" + name, tree, sgn, 0.35f + 1.15f * s, island, tipX, name == "L" ? 1 : 7);
            trees[sgn] = tree;
        }

        if (b.polygonFrame)
        {
            int nThreads = FrameAnchors(b, sup, th, trees);
            return FinishThreads(sup, th, id, nThreads);
        }

        // てっぺんの糸
        if (b.topThread)
        {
            Vector3 t3 = new Vector3(top.x, top.y, 0);
            if (treeCount == 2) { th.Hang(t3, trees[-1f].branch[trees[-1f].branch.Length - 1], b.sag * 0.75f); th.Hang(t3, trees[1f].branch[trees[1f].branch.Length - 1], b.sag * 0.75f); }
            else if (treeCount == 1) { foreach (var t in trees.Values) { var tip = t.branch[t.branch.Length - 1]; th.Hang(t3, new Vector3(top.x, tip.y, tip.z), 0f); } }
        }

        foreach (var (side, sgn, name) in sides)
        {
            if (side.anchor == SpiderWebBuilder.AnchorType.Tree)
            {
                var tree = trees[sgn];
                // 上のほうの点 → 枝の途中
                var upper = verts.FindAll(v => v.x * sgn > hs.x * 0.35f && v.y > hs.y * 0.3f);
                upper.Sort((p, q) => (q.x * sgn).CompareTo(p.x * sgn));
                int nu = Mathf.Min(side.upperThreads, upper.Count);
                for (int i = 0; i < nu; i++)
                {
                    var v = upper[nu == 1 ? 0 : Mathf.RoundToInt(i * (upper.Count - 1) / (float)(nu - 1))];
                    float bt = nu == 1 ? 0.35f : Mathf.Lerp(0.25f, 0.6f, i / (float)(nu - 1));
                    th.Hang(new Vector3(v.x, v.y, 0), tree.OnBranch(bt), b.sag * 0.9f);
                }
                // 横の点 → 少し上の幹
                float lowLimit = tree.fy + 0.17f * b.size.y;
                var mid = verts.FindAll(v => v.x * sgn > hs.x * 0.4f && v.y <= hs.y * 0.3f && v.y > lowLimit);
                mid.Sort((p, q) => q.y.CompareTo(p.y));
                int nm = Mathf.Min(side.sideThreads, mid.Count);
                for (int i = 0; i < nm; i++)
                {
                    var v = mid[nm == 1 ? 0 : Mathf.RoundToInt(i * (mid.Count - 1) / (float)(nm - 1))];
                    float ay = Mathf.Min(v.y + 0.22f * b.size.y, tree.branch[0].y - 0.15f);
                    th.Hang(new Vector3(v.x, v.y, 0), tree.TrunkPoint(ay, sgn, 0.4f), b.sag * 1.1f);
                }
                // 下の角 → 幹の下のほう
                var low = verts.FindAll(v => v.x * sgn > hs.x * 0.6f && v.y <= lowLimit);
                low.Sort((p, q) => (q.x * sgn).CompareTo(p.x * sgn));
                int nb = Mathf.Min(side.bottomThreads, low.Count);
                for (int i = 0; i < nb; i++)
                {
                    var v = low[i];
                    float ay = Mathf.Max(v.y + 0.083f * b.size.y, tree.fy + 0.075f * b.size.y);
                    th.Hang(new Vector3(v.x, v.y, 0), tree.TrunkPoint(ay, sgn, 0.6f), b.sag);
                }
            }
            else if (side.anchor == SpiderWebBuilder.AnchorType.Rock)
            {
                // 岩側：巣の横の点から、岩の中へ短く
                var pts = verts.FindAll(v => v.x * sgn > hs.x * 0.4f);
                pts.Sort((p, q) => p.y.CompareTo(q.y));
                int n = Mathf.Min(side.sideThreads, pts.Count);
                for (int i = 0; i < n; i++)
                {
                    var v = pts[n == 1 ? pts.Count / 2 : Mathf.RoundToInt(i * (pts.Count - 1) / (float)(n - 1))];
                    th.Hang(new Vector3(v.x, v.y, 0), new Vector3(sgn * (hs.x + side.rockDepth), v.y + 0.05f * b.size.y, 0.05f), b.sag * 0.6f);
                }
            }
        }

        return FinishThreads(sup, th, id, th.count);
    }

    // 枠の角から支えへ、ピンと張った係留の糸を1本ずつ。
    // 木のある側：上の角→枝、下の角→幹。木のない側：上の角→枝（先のほう）、下の角→真下の地面（なければ幹の根元）
    static int FrameAnchors(SpiderWebBuilder b, Transform sup, ThreadMesh th, Dictionary<float, Tree> trees)
    {
        var fc = FrameCorners(b);
        foreach (var v in fc)
        {
            var p = new Vector3(v.x, v.y, 0);
            float sgn = v.x - b.holeCenter.x >= 0 ? 1f : -1f;
            bool upper = v.y > b.holeCenter.y;
            Vector3 target;
            bool centerLow = !upper && trees.Count == 2 && Mathf.Abs(v.x) < b.size.x * 0.25f;
            if (centerLow && GroundBelow(b, sup, p, 0f, out var gp))
            {
                // 両側に木がある巣の、真ん中あたりの下の角は真下の地面へ
                target = gp;
            }
            else if (trees.TryGetValue(sgn, out var tree))
            {
                if (upper && v.y > tree.branch[0].y - 0.35f * b.size.y) target = NearestOnBranch(tree, p + Vector3.up * 0.25f * b.size.y);
                else target = tree.TrunkPoint(Mathf.Min(v.y + 0.12f * b.size.y, tree.branch[0].y - 0.1f), sgn, 0.5f);
            }
            else
            {
                Tree any = null; foreach (var t in trees.Values) any = t;
                if (upper && any != null) target = NearestOnBranch(any, p + Vector3.up * 0.6f * b.size.y);
                else
                {
                    // 真下（少し外側）の地面を探す
                    if (GroundBelow(b, sup, p, sgn * 0.2f * b.size.x, out var g2)) target = g2;
                    else if (any != null) target = any.TrunkPoint(any.fy + 0.12f * b.size.y, -sgn, 0.6f);
                    else continue;
                }
            }
            th.Hang(p, target, 0.015f);
        }
        return th.count;
    }

    static bool GroundBelow(SpiderWebBuilder b, Transform sup, Vector3 p, float dx, out Vector3 ground)
    {
        Physics.SyncTransforms();
        var from = sup.TransformPoint(p + new Vector3(dx, 0, 0));
        if (Physics.Raycast(from, -sup.up, out RaycastHit hit, b.groundSearch, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(b.transform))
        {
            ground = sup.InverseTransformPoint(hit.point) + Vector3.down * 0.02f;
            return true;
        }
        ground = Vector3.zero;
        return false;
    }

    static Vector3 NearestOnBranch(Tree tree, Vector3 q)
    {
        Vector3 best = tree.OnBranch(1f); float bd = 1e9f;
        for (int i = 0; i <= 40; i++)
        {
            var pt = tree.OnBranch(i / 40f);
            float d = (pt - q).sqrMagnitude;
            if (d < bd) { bd = d; best = pt; }
        }
        return best;
    }

    static int FinishThreads(Transform sup, ThreadMesh th, string id, int count)
    {
        if (th.count > 0)
        {
            var m = new Mesh();
            m.SetVertices(th.V);
            m.SetNormals(th.V.ConvertAll(x => Vector3.back));
            m.SetUVs(0, th.U0);
            m.SetUVs(1, th.U1);
            m.SetTriangles(th.T, 0);
            var saved = SaveMesh(Folder + "/" + id + "_Threads.asset", m);
            // シェーダーが画面上で太らせるので、表示判定の箱を少し広げておく
            saved.bounds = new Bounds(saved.bounds.center, saved.bounds.size + Vector3.one * 0.5f);
            var go = new GameObject("Threads");
            go.transform.SetParent(sup, false);
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "WebThread.mat");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        return th.count;
    }

    static float FloorAt(Transform space, Vector3 localP)
    {
        if (!space.gameObject.activeInHierarchy) return float.NaN;
        Physics.SyncTransforms();
        Vector3 w = space.TransformPoint(localP + Vector3.up * 0.6f);
        if (Physics.Raycast(w, Vector3.down, out RaycastHit hit, 1.4f, ~0, QueryTriggerInteraction.Ignore))
            return space.InverseTransformPoint(hit.point).y;
        return float.NaN;
    }

    static Mesh Tube(Vector3[] cs, float[] rs, int sides, float jit, int seed, bool noJitterFirstRing = false)
    {
        var rnd = new System.Random(seed);
        var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
        for (int i = 0; i < cs.Length; i++)
        {
            Vector3 ax = (cs[Mathf.Min(i + 1, cs.Length - 1)] - cs[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 u = Vector3.Cross(ax, Mathf.Abs(ax.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(ax, u);
            // でこぼこは1周ぶんだけ作って、つなぎ目（s=sides）は s=0 と同じ値を使う（違う値だと縦に切れ目ができる）
            var jits = new float[sides];
            for (int s = 0; s < sides; s++) jits[s] = 1 + (jit > 0 && !(i == 0 && noJitterFirstRing) ? ((float)rnd.NextDouble() * 2 - 1) * jit : 0);
            for (int s = 0; s <= sides; s++)
            {
                float a = (s % sides) * Mathf.PI * 2 / sides;
                Vector3 d = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                float j = jits[s % sides];
                V.Add(cs[i] + d * rs[i] * j); N.Add(d); U.Add(new Vector2((float)s / sides, (float)i / (cs.Length - 1)));
            }
        }
        int w = sides + 1;
        for (int i = 0; i < cs.Length - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = i * w + s, bb = a + 1, c = a + w, d = c + 1;
                T.AddRange(new[] { a, bb, c, bb, d, c });   // 外向きが表
            }
        var m = new Mesh(); m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(T, 0);
        return m;
    }

    // 浮島の下の▼（土の帯→岩のとんがり）。top＝ふたの高さ（上の円盤の厚みの中）、radius＝上の円盤より少し小さく
    public static GameObject AddIslandUnder(Transform parent, Vector3 localTop, float radius, float depth, int seed)
    {
        var old = parent.Find("IslandUnder");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var u = new GameObject("IslandUnder", typeof(MeshFilter), typeof(MeshRenderer));
        u.transform.SetParent(parent, false);
        u.transform.localPosition = localTop;
        u.transform.localRotation = Quaternion.Euler(0, (seed * 47) % 360, 0);
        u.transform.localScale = new Vector3(radius, depth, radius);
        u.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/FarIslands/IslandUnder" + (radius < 5f ? "Low" : "") + "_" + (Mathf.Abs(seed) % 3) + ".asset");
        var mr = u.GetComponent<MeshRenderer>();
        mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/IslandUnder.mat");
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        u.layer = parent.gameObject.layer;
        return u;
    }

    static void BuildTree(Transform parent, SpiderWebBuilder b, string assetName, Tree tree, float sgn, float crownR, bool island, float tipX, int seed)
    {
        seed += Mathf.RoundToInt(b.seed * 13);
        var rnd = new System.Random(seed);
        var root = new GameObject(sgn < 0 ? "TreeL" : "TreeR").transform;
        root.SetParent(parent, false);
        Vector3 basePos = new Vector3(tree.x, tree.fy, tree.z);
        float height = tree.height, r0 = tree.r0;

        int n = 9; var cs = new Vector3[n]; var rs = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            cs[i] = basePos + new Vector3(sgn * 0.18f * height / 6f * Mathf.Pow(t, 1.6f), -0.15f + t * (height + 0.15f), 0.08f * Mathf.Sin(t * 3.1f));
            rs[i] = r0 * (0.5f + 0.5f * (1 - t)) * (t < 0.15f ? 1 + (0.15f - t) * 3.5f : 1);
        }
        var parts = new List<CombineInstance>();
        parts.Add(new CombineInstance { mesh = Tube(cs, rs, 10, 0.04f, seed), transform = Matrix4x4.identity });

        // 高い所から巣の上へ伸びる枝（先で少し垂れる）
        Vector3 bStart = cs[7];
        float bl = float.IsNaN(tipX) ? height * 0.34f : Mathf.Max(0.2f, (bStart.x - tipX) * sgn);
        var bp = new Vector3[5]; var br = new float[5];
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            bp[i] = bStart + new Vector3(-sgn * bl * t, bl * (0.32f * t - 0.12f * t * t), -bStart.z * t);
            br[i] = r0 * Mathf.Lerp(0.45f, 0.1f, t);
        }
        tree.branch = bp;
        parts.Add(new CombineInstance { mesh = Tube(bp, br, 7, 0, seed + 1), transform = Matrix4x4.identity });

        // 小枝（巣側の下のほう・外側）
        Vector3 sStart = cs[5]; Vector3 sDir = new Vector3(-sgn * 0.8f, 0.6f, 0.15f).normalized; float sl = height * 0.13f;
        parts.Add(new CombineInstance { mesh = Tube(new[] { sStart, sStart + sDir * sl * 0.5f, sStart + sDir * sl }, new[] { r0 * 0.35f, r0 * 0.22f, r0 * 0.08f }, 7, 0, seed + 4), transform = Matrix4x4.identity });
        Vector3 oStart = cs[6]; Vector3 oDir = new Vector3(sgn * 0.8f, 0.6f, 0.2f).normalized; float ol = height * 0.18f;
        parts.Add(new CombineInstance { mesh = Tube(new[] { oStart, oStart + oDir * ol * 0.5f, oStart + oDir * ol }, new[] { r0 * 0.38f, r0 * 0.25f, r0 * 0.1f }, 7, 0, seed + 2), transform = Matrix4x4.identity });
        if (b.bareTree) AddDeadTwigs(parts, cs, r0, height, sgn, seed);
        var barkMesh = new Mesh();
        barkMesh.CombineMeshes(parts.ToArray(), true, true);

        // 葉っぱ（球をいくつか重ねる）。Unity標準の球(768ポリ)は重いので、軽い球（正二十面体2分割＝320ポリ、直径1）を使う
        var sph = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/LeafBlob_Ico2.asset");
        Vector3 top = cs[n - 1];
        Vector3[] offs = { new Vector3(0, 0.35f, 0), new Vector3(0.75f, 0, 0.2f), new Vector3(-0.75f, 0.05f, -0.15f), new Vector3(0.1f, 0.1f, 0.7f), new Vector3(-0.15f, 0, -0.7f), new Vector3(0.2f, 0.75f, -0.1f) };
        float[] szs = { 1f, 0.72f, 0.72f, 0.68f, 0.68f, 0.6f };
        var blobs = new List<CombineInstance>();
        for (int i = 0; i < offs.Length; i++)
        {
            float r = crownR * szs[i] * (0.92f + 0.16f * (float)rnd.NextDouble());
            blobs.Add(new CombineInstance { mesh = sph, transform = Matrix4x4.TRS(top + offs[i] * crownR + Vector3.up * crownR * 0.35f, Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0), new Vector3(2 * r, 1.7f * r, 2 * r)) });
        }
        var leafMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        leafMesh.CombineMeshes(blobs.ToArray(), true, true);

        var subs = new List<CombineInstance> { new CombineInstance { mesh = barkMesh, transform = Matrix4x4.identity }, new CombineInstance { mesh = leafMesh, transform = Matrix4x4.identity } };
        var mats = new List<Material> { LoadMat("WebTree_Bark", new Color(0.52f, 0.38f, 0.28f)), LoadMat("WebTree_Leaves", new Color(0.55f, 0.78f, 0.47f)) };
        if (b.bareTree)
        {
            subs.RemoveAt(1);
            mats = new List<Material> { LoadMat("WebTree_DeadBark", new Color(0.42f, 0.37f, 0.36f)) };
        }

        if (island)
        {
            // 地面がない所は、小さい浮島（緑の地面＋下が岩）の上に立てる
            float ir = r0 * 4.2f;
            // 下の▼は、ほかの浮島（スタート/ゴール・遠景）と同じ見た目の IslandUnder を別オブジェクトで置く。
            // ふたは緑の円盤（上面 -0.04、下面 -0.10）の厚みの中、半径は円盤(ir*1.025)より小さく＝はみ出さない
            AddIslandUnder(root, basePos + new Vector3(0, -0.07f, 0), ir, ir * 1.3f, seed);
            var tmpC = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var cyl = tmpC.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmpC);
            var disc = new GameObject("IslandTop");
            disc.transform.SetParent(root, false);
            disc.transform.localPosition = basePos + new Vector3(0, -0.07f, 0);
            disc.transform.localScale = new Vector3(ir * 2.05f, 0.03f, ir * 2.05f);
            disc.AddComponent<MeshFilter>().sharedMesh = cyl;
            disc.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Goal_Ground.mat");
            // 浮島の上に乗れるように（緑の地面＝道と同じ扱い：ふわふわが触るとアウト）
            var dc = disc.AddComponent<MeshCollider>(); dc.sharedMesh = cyl; dc.convex = true;
            disc.layer = 11;
        }

        // 幹の当たり判定（枝と葉っぱはすり抜け）。ふわふわは岩と同じく跳ね返る
        {
            var tc = new GameObject("TrunkCollider");
            tc.transform.SetParent(root, false);
            Vector3 a = cs[0], bTop = cs[7];
            tc.transform.localPosition = (a + bTop) * 0.5f;
            tc.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (bTop - a).normalized);
            var cap = tc.AddComponent<CapsuleCollider>();
            cap.direction = 1; cap.radius = r0 * 0.8f; cap.height = (bTop - a).magnitude + cap.radius * 2f;
        }

        var full = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        full.CombineMeshes(subs.ToArray(), false, true);
        var saved = SaveMesh(Folder + "/" + assetName + ".asset", full);
        root.gameObject.AddComponent<MeshFilter>().sharedMesh = saved;
        root.gameObject.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
    }

    // 枯れ木のてっぺんの細い枝。巣（内側 -sgn）にはかからないよう、外側と前後へ上向きに広げる。2本は途中で枝分かれ
    static void AddDeadTwigs(List<CombineInstance> parts, Vector3[] cs, float r0, float height, float sgn, int seed)
    {
        var rnd = new System.Random(seed + 77);
        Vector3 top = cs[cs.Length - 1];
        float[] yaw = { 0f, 70f, -70f, 140f, -140f };   // 0=外側
        for (int i = 0; i < yaw.Length; i++)
        {
            float a = (yaw[i] + (float)(rnd.NextDouble() - 0.5) * 30f) * Mathf.Deg2Rad;
            float tilt = (35f + (float)rnd.NextDouble() * 25f) * Mathf.Deg2Rad;   // 真上からの傾き
            var dir = new Vector3(sgn * Mathf.Cos(a) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Sin(a) * Mathf.Sin(tilt)).normalized;
            float len = height * (0.12f + 0.09f * (float)rnd.NextDouble());
            Vector3 s = i == 0 ? top : Vector3.Lerp(cs[cs.Length - 2], top, 0.4f + 0.5f * (float)rnd.NextDouble());
            var bend = new Vector3((float)rnd.NextDouble() - 0.5f, 0.3f, (float)rnd.NextDouble() - 0.5f) * len * 0.25f;
            var p = new[] { s, s + dir * len * 0.5f + bend * 0.5f, s + dir * len + bend };
            parts.Add(new CombineInstance { mesh = Tube(p, new[] { r0 * 0.32f, r0 * 0.18f, r0 * 0.05f }, 6, 0, seed + 30 + i), transform = Matrix4x4.identity });
            if (i < 2)
            {
                var fdir = (dir + new Vector3(0, 0.2f, 0) + Quaternion.AngleAxis(50f * (i == 0 ? 1 : -1), Vector3.up) * dir).normalized;
                var fs = p[1]; float fl = len * 0.5f;
                parts.Add(new CombineInstance { mesh = Tube(new[] { fs, fs + fdir * fl * 0.5f, fs + fdir * fl }, new[] { r0 * 0.15f, r0 * 0.1f, r0 * 0.03f }, 5, 0, seed + 40 + i), transform = Matrix4x4.identity });
            }
        }
    }

    static Material LoadMat(string name, Color col)
    {
        string p = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard")) { color = col };
            m.SetFloat("_Glossiness", 0.05f);
            AssetDatabase.CreateAsset(m, p);
        }
        return m;
    }

    static Material FindMat(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }
}

[CustomEditor(typeof(SpiderWebBuilder))]
public class SpiderWebBuilderEditor : Editor
{
    bool _pending;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("値を変えると、巣・当たり判定・支えの木と糸がその場で作り直されます。巣を動かした後は「作り直す」を押してください。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck()) Schedule();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("作り直す")) foreach (var t in targets) SpiderWebBuild.Build((SpiderWebBuilder)t);
            if (GUILayout.Button("シーンの巣を全部作り直す")) Debug.Log(SpiderWebBuild.RebuildAll());
        }
    }

    // スライダーを動かしている間に何度も作らないよう、次のフレームに1回だけ
    void Schedule()
    {
        if (_pending) return;
        _pending = true;
        var ts = targets;
        EditorApplication.delayCall += () =>
        {
            _pending = false;
            foreach (var t in ts) if (t != null) SpiderWebBuild.Build((SpiderWebBuilder)t);
        };
    }
}
