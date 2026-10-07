using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// きのこのたにの入口（スタートの先〜チェックポイント1の手前）を作る。何度実行しても同じ結果。
// 入口（平ら）→ すき間（下から生えたキノコの傘で渡る）→ 木の坂 → すき間（次の坂の正面にサルノコシカケ）→ 木の坂 → 上の段
// ・道のメッシュ（見た目と判定）からこの区間を切り取って、木の床（板張り）と判定の箱を置く
//   2026-10-07：切り取りを「三角形ごと消す」から「切り口の面でぴったり切る」に変更（ギザギザのすき間が出ていた）。
//   切り口には道の断面と同じ形の木の板（ふた）を張って、中が見えないように。坂Bはその板にぴったり突き当てる
// ・道は一直線（O から D の向き）。d＝その向きの距離、s＝横の距離
public static class FuwaKinokoEntrance
{
    static readonly Vector3 O = new Vector3(-22.920f, 0f, -44.641f);
    static readonly Vector3 D = new Vector3(-0.7817f, 0f, -0.6237f).normalized;
    static Vector3 S => Vector3.Cross(Vector3.up, D);

    // 区間（d）と高さ
    public static float CutA = -6.0f, CutB = 6.6f;          // 道を切り取る範囲
    public static float FloorY = 8.0f;                       // 入口の高さ
    // 2026-10-07：キノコを使わないと渡れないように、すき間を広げて向こう岸を高くした
    // （ふつうのジャンプは高さ約0.46m・キノコの上は4.2で約0.9m）。キノコの傘も床より上にして、乗るのに小ジャンプが要る
    public static float Gap1A = -6.0f, Gap1B = -3.4f;        // すき間1（キノコ）2.6m
    public static float YA0 = 8.7f;                          // 坂Aの始まりは入口より0.7m高い（キノコなしでは届かない）
    public static float SlopeA0 = -3.4f, SlopeA1 = -0.4f, FlatA1 = 0.8f, YA1 = 9.8f;   // 坂A → 平ら
    public static float Gap2B = 3.6f;                        // すき間2（サルノコシカケ）＝ FlatA1〜Gap2B 2.8m
    public static float YB0 = 10.5f;                         // 坂B（Gap2B〜CutB）の始まり。平らAより0.7m高い
    public static float YB1;                                 // 坂Bの終わり＝上の段の切り口の上面の3cm下（実行時に道の形から決める）
    public static float FaceT = 0.06f;                       // 切り口のふたの板の厚み
    public static float ClipHalfW = 2.6f;                    // 切り取る道の横幅（半分）。上の段は±2.25
    static float ClipCA, ClipCB, ClipY0 = 6f, ClipY1 = 13.5f;   // ClipMesh が切る区間（d）と高さ
    // 木の塔の手前の道の先っぽ（細い出っぱり）を板張りに（2026-10-07）
    public static float TabCut = 46.45f, TabEnd = 47.3f, TabHalfW = 1.3f, TabTopY = 18.17f;
    public static float HalfW = 1.5f;
    public static float CapLift = 0.6f;    // キノコの傘を床より上に（小ジャンプで乗る。0.5以下だとVRChatでは歩いて登れる）
    public static float CapScale = 0.6f, ShelfScale = 0.5f, ShelfTopY = 9.75f;
    public static float ShelfOut = 0.8f;   // サルノコシカケが平らAの先の壁（手前側）から出る長さ
    public static float ShelfImpulse = 4.6f;   // サルノコシカケの上のジャンプ力（高さ約1.08m）

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
        var faceA = new List<Vector3>(); var faceB = new List<Vector3>();
        ClipCA = CutA; ClipCB = CutB; ClipHalfW = 2.6f; ClipY0 = 6f; ClipY1 = 13.5f;
        var vis1 = ClipMesh(visSrc, pm, MeshDir + "/Bevel/Course5_Path_Bevel_Entrance.asset", faceA, faceB, out int cv);
        var col1 = ClipMesh(colSrc, pm, MeshDir + "/Bevel/Course5_Path_CutEntrance.asset", null, null, out int cc);
        // 木の塔の手前の出っぱり：TabCut から先を切り取る（先は何もないので CB は遠く）
        var faceT = new List<Vector3>();
        ClipCA = TabCut; ClipCB = 1000f; ClipHalfW = 1.5f; ClipY0 = 16f; ClipY1 = 19.5f;
        mf.sharedMesh = ClipMesh(vis1, pm, MeshDir + "/Bevel/Course5_Path_Bevel_Entrance2.asset", faceT, null, out int tv);
        var colMesh = ClipMesh(col1, pm, MeshDir + "/Bevel/Course5_Path_CutEntrance2.asset", null, null, out int tc);
        log += "tab clip vis " + tv + " col " + tc + " face " + faceT.Count + " / ";
        // 坂Bの終わりの高さ＝上の段の切り口の上面（床の幅の中）の3cm下
        float topB = -99; foreach (var q in faceB) if (Mathf.Abs(Ss(q)) < HalfW) topB = Mathf.Max(topB, q.y);
        YB1 = topB - 0.03f;
        mc.sharedMesh = null; mc.sharedMesh = colMesh;
        var bs = pm.Find("BallStairs1"); if (bs) Object.DestroyImmediate(bs.gameObject);
        log += "clip vis " + cv + " col " + cc + " faceA " + faceA.Count + " faceB " + faceB.Count + " YB1 " + YB1.ToString("F3") + " / ";

        // ---- 2. 木の床 ----
        var old = c5.Find("KinokoEntrance"); if (old) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("KinokoEntrance").transform; root.SetParent(c5, true);
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Generated/Den/Den_Twig.mat");
        var rnd = new System.Random(5);
        var mb = new MB();
        // 坂A＋平らA
        Deck(mb, SlopeA0, YA0, SlopeA1, YA1, rnd); Deck(mb, SlopeA1, YA1, FlatA1, YA1, rnd);
        // 坂B
        Deck(mb, Gap2B, YB0, CutB - FaceT, YB1, rnd);
        // 道の切り口のふた（道の断面と同じ形の板。入口の先と上の段の手前）
        var hullA = Hull(faceA); var hullB = Hull(faceB);
        FacePlate(mb, hullA, CutA, CutA + FaceT); FacePlate(mb, hullB, CutB - FaceT, CutB);
        // 木の塔の手前：切り口のふた＋板張りの床（他と同じくガタつかせる）
        var hullT = Hull(faceT);
        FacePlate(mb, hullT, TabCut, TabCut + FaceT);
        float hw = HalfW; HalfW = TabHalfW;
        Deck(mb, TabCut + FaceT, TabTopY, TabEnd, TabTopY, rnd);
        // 道と同じ厚みの木の箱にする：先っぽと両横に横板を積む（下端は道の底にそろえる）
        float tabH = TabTopY - (hullT.Count > 0 ? hullT.Min(h => h.y) : TabTopY - 0.8f);
        FrontBoards(mb, TabEnd, TabTopY, tabH, -1f, rnd);
        SideBoards(mb, TabCut + FaceT, TabEnd, TabTopY, tabH, rnd);
        HalfW = hw;
        // 段の正面：2026-10-07 横板を積んだ壁 → 上の横木から縦の板が崩れて垂れている見た目に（判定の壁はそのまま）
        var rndH = new System.Random(11);
        HangingBoards(mb, FlatA1, YA1, 1.2f, -1f, rndH);     // 平らAの先（すき間2の手前、サルノコシカケの生える所）
        HangingBoards(mb, Gap2B, YB0, 1.6f, 1f, rndH);       // 坂Bの正面
        HangingBoards(mb, SlopeA0, YA0, 1.4f, 1f, rndH);     // 坂Aの正面（入口より高い段）
        var mesh = mb.Build("KinokoEntrance_Deck", root);
        var go = new GameObject("Deck", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat; go.isStatic = true;

        // ---- 3. 判定（箱） ----
        var cols = new GameObject("Colliders").transform; cols.SetParent(root, false); cols.gameObject.layer = 11;
        SlopeBox(cols, "SlopeA", SlopeA0, YA0, SlopeA1, YA1);
        SlopeBox(cols, "FlatA", SlopeA1, YA1, FlatA1, YA1);
        SlopeBox(cols, "SlopeB", Gap2B, YB0, CutB - FaceT, YB1);
        FaceBox(cols, "FaceA", hullA, CutA, CutA + FaceT);
        FaceBox(cols, "FaceB", hullB, CutB - FaceT, CutB);
        FaceBox(cols, "FaceTab", hullT, TabCut, TabCut + FaceT);
        { float hw2 = HalfW; HalfW = TabHalfW; SlopeBox(cols, "TabDeck", TabCut + FaceT, TabTopY, TabEnd, TabTopY); HalfW = hw2; }
        // 段の正面の壁（届かなかった時に板の中へすり抜けないように）
        WallBox(cols, "WallA", SlopeA0, YA0, 1.4f, 1f);
        WallBox(cols, "WallFlatA", FlatA1, YA1, 1.2f, -1f);
        WallBox(cols, "WallB", Gap2B, YB0, 1.6f, 1f);

        // ---- 4. キノコとサルノコシカケ ----
        var mush = c5.Find("Mushrooms");
        foreach (var n in new[] { "PitCap2", "PitCap3" }) { var t = mush.Find(n); if (t) Object.DestroyImmediate(t.gameObject); }
        var cap = mush.Find("PitCap1") ?? mush.Find("EntranceCap");
        if (cap != null)
        {
            cap.name = "EntranceCap"; cap.localScale = Vector3.one * CapScale;
            cap.position = W((Gap1A + Gap1B) * 0.5f, 0f, FloorY + CapLift);
            var st = cap.Find("Stem"); if (st) { st.localScale = Vector3.one; st.localPosition = new Vector3(0, -3.28f, 0); }
            var j = cap.GetComponentInChildren<FuwaJumpBoost>(); j.radius = 1.15f * CapScale;
            if (j.boostRings) { var m = j.boostRings.main; m.startSize = j.radius * 2f * 1.3f; }
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j);
        }
        var shelf = mush.Find("PitShelf");
        if (shelf != null)
        {
            // 平らAの先の壁（手前側）から、すき間の中へ生やす。ここでキノコジャンプして奥の坂Bへ
            shelf.localScale = Vector3.one * ShelfScale; shelf.rotation = Quaternion.LookRotation(D);
            var top = shelf.Find("Top");
            // ジャンプの判定の中心を傘の真ん中寄り（壁の近く）にして、半径を傘全体に。壁ぎわに立っても上がるように
            var jb = shelf.GetComponentInChildren<FuwaJumpBoost>();
            top.localPosition = new Vector3(0f, 0f, 2.6f);
            jb.transform.localPosition = new Vector3(0f, jb.transform.localPosition.y, 2.6f);
            jb.radius = 1.9f * ShelfScale;
            shelf.position += W(FlatA1 + ShelfOut, 0f, ShelfTopY) - top.position;
            // 手前のふちを平らAの壁に少しめりこませる
            Physics.SyncTransforms();
            float minD = 99; foreach (var r in shelf.Find("Visual").GetComponentsInChildren<MeshFilter>()) foreach (var v in r.sharedMesh.vertices) minD = Mathf.Min(minD, Dd(r.transform.TransformPoint(v)));
            shelf.position += D * (FlatA1 - 0.08f - minD);
            var j = shelf.GetComponentInChildren<FuwaJumpBoost>(); j.jumpImpulse = ShelfImpulse; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j);
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
        // 木の塔の手前の板張りの上の草・下に垂れたツタは抜く
        foreach (var r in c5.GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform.IsChildOf(root) || r.transform.IsChildOf(pm)) continue;
            var bc = r.bounds.center; float d = Dd(bc), s2 = Ss(bc);
            if (d > TabCut - 0.3f && d < TabEnd + 0.3f && Mathf.Abs(s2) < TabHalfW + 0.3f && bc.y > 16.5f && bc.y < 19.5f && r.bounds.size.magnitude < 2f)   // 下に垂れたツタも
            { r.gameObject.SetActive(false); hidden++; }
        }
        log += "deco hidden " + hidden;
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c5.gameObject.scene);
        return log;
    }

    // 道のメッシュを、CutA〜CutB の区間（横 ClipHalfW 以内、高さ 6〜13.5m）だけ、切り口の面でぴったり切り取る。
    // 区間をまたぐ三角形は面で分割して外側だけ残す（頂点の法線・UV・色は補間）。切り口の上に乗った点を faceA/faceB に集める
    static Mesh ClipMesh(Mesh src, Transform t, string path, List<Vector3> faceA, List<Vector3> faceB, out int changed)
    {
        var P = new List<Vector3>(src.vertices);
        var Nn = src.normals.Length == P.Count ? new List<Vector3>(src.normals) : null;
        var U0 = src.uv.Length == P.Count ? new List<Vector2>(src.uv) : null;
        var U1 = src.uv2.Length == P.Count ? new List<Vector2>(src.uv2) : null;
        var Cl = src.colors.Length == P.Count ? new List<Color>(src.colors) : null;
        var Wd = P.Select(v => t.TransformPoint(v)).ToList();
        changed = 0;
        int Lerp(int a, int b, float k)
        {
            P.Add(Vector3.Lerp(P[a], P[b], k)); Wd.Add(Vector3.Lerp(Wd[a], Wd[b], k));
            if (Nn != null) Nn.Add(Vector3.Lerp(Nn[a], Nn[b], k).normalized);
            if (U0 != null) U0.Add(Vector2.Lerp(U0[a], U0[b], k));
            if (U1 != null) U1.Add(Vector2.Lerp(U1[a], U1[b], k));
            if (Cl != null) Cl.Add(Color.Lerp(Cl[a], Cl[b], k));
            return P.Count - 1;
        }
        // 多角形を面で切って片側だけ残す（keepBelow：d <= plane の側を残す）
        List<int> ClipPoly(List<int> poly, float plane, bool keepBelow, List<Vector3> onFace)
        {
            var o = new List<int>();
            for (int i = 0; i < poly.Count; i++)
            {
                int a = poly[i], b = poly[(i + 1) % poly.Count];
                float da = Dd(Wd[a]) - plane, db = Dd(Wd[b]) - plane;
                if (keepBelow) { da = -da; db = -db; }   // 「0以上が残す側」にそろえる
                bool ia = da >= 0, ib = db >= 0;
                if (ia) o.Add(a);
                if (ia != ib)
                {
                    int n = Lerp(a, b, da / (da - db));
                    o.Add(n); if (onFace != null) onFace.Add(Wd[n]);
                }
            }
            return o;
        }
        var subs = new List<List<int>>();
        for (int sm = 0; sm < src.subMeshCount; sm++)
        {
            var tr = src.GetTriangles(sm); var k = new List<int>();
            for (int i = 0; i < tr.Length; i += 3)
            {
                int a = tr[i], b = tr[i + 1], c = tr[i + 2];
                var cen = (Wd[a] + Wd[b] + Wd[c]) / 3f;
                float d0 = Mathf.Min(Dd(Wd[a]), Mathf.Min(Dd(Wd[b]), Dd(Wd[c]))), d1 = Mathf.Max(Dd(Wd[a]), Mathf.Max(Dd(Wd[b]), Dd(Wd[c])));
                bool inBand = Mathf.Abs(Ss(cen)) < ClipHalfW && cen.y > ClipY0 && cen.y < ClipY1;
                if (!inBand || d1 <= ClipCA || d0 >= ClipCB) { k.Add(a); k.Add(b); k.Add(c); continue; }
                changed++;
                var tri = new List<int> { a, b, c };
                foreach (var piece in new[] { ClipPoly(tri, ClipCA, true, faceA), ClipPoly(tri, ClipCB, false, faceB) })
                    for (int j = 1; j + 1 < piece.Count; j++) { k.Add(piece[0]); k.Add(piece[j]); k.Add(piece[j + 1]); }
            }
            subs.Add(k);
        }
        // もともと切り口の面の上にあった頂点も集める
        for (int i = 0; i < src.vertexCount; i++)
        {
            var w = Wd[i]; if (Mathf.Abs(Ss(w)) >= ClipHalfW || w.y <= ClipY0 || w.y >= ClipY1) continue;
            if (faceA != null && Mathf.Abs(Dd(w) - ClipCA) < 0.002f) faceA.Add(w);
            if (faceB != null && Mathf.Abs(Dd(w) - ClipCB) < 0.002f) faceB.Add(w);
        }
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
        m.Clear(); m.name = System.IO.Path.GetFileNameWithoutExtension(path); m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(P); if (Nn != null) m.SetNormals(Nn); if (U0 != null) m.SetUVs(0, U0); if (U1 != null) m.SetUVs(1, U1); if (Cl != null) m.SetColors(Cl);
        m.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
        m.RecalculateBounds(); EditorUtility.SetDirty(m);
        return m;
    }

    // 切り口の面の上の点を (横s, 高さy) の凸包にする（道の断面＝角の丸い長方形）。反時計回り
    static List<Vector2> Hull(List<Vector3> pts)
    {
        var p = pts.Select(w => new Vector2(Mathf.Round(Ss(w) * 1000f) / 1000f, Mathf.Round(w.y * 1000f) / 1000f)).Distinct().OrderBy(v => v.x).ThenBy(v => v.y).ToList();
        if (p.Count < 3) return p;
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var h = new List<Vector2>();
        foreach (var v in p) { while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], v) <= 1e-6f) h.RemoveAt(h.Count - 1); h.Add(v); }
        int lo = h.Count + 1;
        for (int i = p.Count - 2; i >= 0; i--) { var v = p[i]; while (h.Count >= lo && Cross(h[h.Count - 2], h[h.Count - 1], v) <= 1e-6f) h.RemoveAt(h.Count - 1); h.Add(v); }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    // 道の断面の形の板（d0〜d1 の厚み）。切り口をふさぐ
    static void FacePlate(MB mb, List<Vector2> hull, float d0, float d1)
    {
        if (hull.Count < 3) return;
        var col = BeamCol; col.a = 1;
        var f0 = hull.Select(h => W(d0, h.x, h.y)).ToList(); var f1 = hull.Select(h => W(d1, h.x, h.y)).ToList();
        mb.Poly(f1, D, col);    // 奥向きの面
        mb.Poly(f0, -D, col);   // 手前向きの面
        for (int i = 0; i < hull.Count; i++)
        {
            int j = (i + 1) % hull.Count;
            var e = hull[j] - hull[i]; var n2 = new Vector2(e.y, -e.x).normalized;   // 反時計回りの外向き
            var nw = (S * n2.x + Vector3.up * n2.y).normalized;
            mb.Poly(new List<Vector3> { f0[i], f0[j], f1[j], f1[i] }, nw, nw.y < -0.5f ? col * 0.8f : col);
        }
    }

    static void FaceBox(Transform parent, string name, List<Vector2> hull, float d0, float d1)
    {
        if (hull.Count < 3) return;
        float s0 = hull.Min(h => h.x), s1 = hull.Max(h => h.x), y0 = hull.Min(h => h.y), y1 = hull.Max(h => h.y);
        var go = new GameObject(name); go.transform.SetParent(parent, true); go.layer = 11;
        go.transform.position = W((d0 + d1) * 0.5f, (s0 + s1) * 0.5f, (y0 + y1) * 0.5f); go.transform.rotation = Quaternion.LookRotation(D, Vector3.up);
        var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(s1 - s0, y1 - y0, d1 - d0);
        go.isStatic = true;
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
        // 平らな凸多角形を1枚。法線 n の側から見て表になるように並べる
        public void Poly(List<Vector3> pts, Vector3 n, Color col)
        {
            col.a = 1; int b0 = V.Count;
            foreach (var p in pts) { V.Add(p); N.Add(n); C.Add(col); }
            bool flip = Vector3.Dot(Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]), n) < 0;
            for (int i = 1; i + 1 < pts.Count; i++) { if (!flip) T.AddRange(new[] { b0, b0 + i, b0 + i + 1 }); else T.AddRange(new[] { b0, b0 + i + 1, b0 + i }); }
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

    // 段の正面に、上の横木から縦の板がぶら下がる（dir：板を置く側 -1＝手前、1＝奥）。
    // 板ごとに長さ・幅・傾きをばらつかせ、ところどころ抜け・短く折れ・外へぷらんと垂れた板を混ぜる。見た目だけ
    static void HangingBoards(MB mb, float d, float topY, float h, float dir, System.Random rnd)
    {
        float R(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        var outN = D * dir;                          // 板の外向き（すき間側）
        float hingeY = topY - 0.1f;                  // 板の上端（横木の中ほど）
        // 上の横木（板を打ちつけている桁）
        mb.Box(W(d + dir * 0.07f, 0, topY - 0.11f), S * (HalfW + 0.04f), Vector3.up * 0.075f, D * 0.05f, BeamCol);
        float x = -HalfW + 0.02f;
        while (x < HalfW - 0.06f)
        {
            float w = Mathf.Min(R(0.17f, 0.27f), HalfW - x); float cx = x + w * 0.5f; x += w + R(0.004f, 0.02f);
            bool edge = cx < -HalfW + 0.3f || cx > HalfW - 0.3f;
            double roll = rnd.NextDouble();
            if (!edge && roll < 0.13) continue;                        // 抜けた板
            float len = roll < 0.33 ? h * R(0.3f, 0.55f) : h * R(0.72f, 1.02f);   // 折れて短い板／ほぼ全長
            float swing = roll > 0.82 ? R(9f, 22f) : R(-2.5f, 4f);      // 外へぷらんと垂れる板
            var hinge = W(d + dir * 0.02f, cx, hingeY);
            var q = Quaternion.AngleAxis(swing * dir, S) * Quaternion.AngleAxis(R(-3.5f, 3.5f), outN) * Quaternion.AngleAxis(R(-2.5f, 2.5f), Vector3.up);
            var down = q * Vector3.down; var side = q * S; var face = q * outN;
            var c = hinge + down * (len * 0.5f) + face * 0.025f;
            var col = Plank(rnd) * 0.82f; col.a = 1;
            mb.Box(c, side * (w * 0.5f - 0.004f), down * (len * 0.5f), face * 0.025f, col);
        }
    }

    // 段の正面の縦の壁（dir：-1＝手前の段の先、1＝奥の段の正面）
    static void WallBox(Transform parent, string name, float d, float topY, float h, float dir)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, true); go.layer = 11;
        go.transform.position = W(d + dir * 0.1f, 0, topY - 0.15f - h * 0.5f); go.transform.rotation = Quaternion.LookRotation(D, Vector3.up);
        var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(HalfW * 2f, h, 0.2f);
        go.isStatic = true;
    }

    // 平らな床の両横に横板を積む（d0〜d1、上端 topY から高さ h）
    static void SideBoards(MB mb, float d0, float d1, float topY, float h, System.Random rnd)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(h / 0.3f)); float bh = h / n;
        foreach (var sg in new[] { -1f, 1f })
            for (int i = 0; i < n; i++)
            {
                var c = W((d0 + d1) * 0.5f, sg * (HalfW - 0.02f), topY - 0.08f - bh * (i + 0.5f));
                var col = BeamCol * (1.05f + 0.1f * (float)rnd.NextDouble()); col.a = 1;
                mb.Box(c, S * 0.04f, Vector3.up * (bh * 0.5f - 0.01f), D * ((d1 - d0) * 0.5f), col);
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
