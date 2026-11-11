using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// きのこのたに：赤キノコの段々（CapStep1〜6）の直後にチェックポイントを足す
//（2026-11-11 てじゃ「赤きのこジャンプゾーンの直後にもチェックポイントほしい。ちょっとスペースがたりない」→ A 案「それ以降全体を後ろにずらす」）
//  着地の板（Deck5：奥行き 2.5m・幅 4.5m）から先（壊れた橋・道・GapCap・チェックポイント2/3・大木・ゴール…）を道の向き D に Shift m 奥へずらし、
//  空いたところ（キノコの傘と着地の板の間）に板を足して（Deck5Ext：着地の板の板を写して横に広げた物。門の両脇の光るキノコを置くため）、門（CheckpointDeck）を立てる
//  道のメッシュ・入口の板（KinokoEntrance の木の塔の手前の出っぱり）は、つながった塊ごとに、中心が奥側（x < SplitX）の物だけずらす
//  1回だけ実行（CheckpointDeck があったら何もしない）
//  ⚠️ FuwaKinokoEntrance（入口を作る）を実行し直すと道のメッシュと出っぱりが元の位置で作り直されるので、そのあと ShiftEntranceMeshes をもう一度呼ぶ
public static class FuwaKinokoDeckCheckpoint
{
    public static readonly Vector3 D = new Vector3(-0.7817f, 0f, -0.6237f).normalized;   // 道の向き（FuwaKinokoEntrance.D・チェックポイントの向きと同じ）
    static Vector3 S => Vector3.Cross(Vector3.up, D);
    public static float Shift = 3.0f;          // 奥へずらす長さ（＝足す板の奥行き）
    public const float SplitX = -45f;          // 道・入口の板の塊は、中心の x がこれより小さい物（奥側）だけずらす
    public static float ExtF0 = 2.07f;         // 着地の板の手前のふち（CapStep6 の中心から D 方向の距離）
    public static float DeckCenterS = -0.59f;  // 着地の板の横の中心（CapStep6 の中心から S 方向）
    public static float DeckHalfW = 2.25f;     // 着地の板の幅の半分
    public static float ExtHalfW = 3.5f;       // 足す板の幅の半分
    public static float FloorY = 15.80f;       // 着地の板の上面（判定）
    public static float CpF = 2.8f;            // 門の位置（D 方向）
    public static float ClusterX = 2.7f, ClusterZ = 0.9f, ClusterLift = 0.62f;   // 光るキノコ（門からの横・前・持ち上げ）。板の上なので根元の切り株が板の下に出ないよう持ち上げる
    const string ExtMeshPath = "Assets/FuwaCourse/Generated/Deck5Ext.asset";

    static readonly string[] MoveNames =
    {
        "Deck5", "Gimmick_DeckGust", "Gimmick3_Launch", "Checkpoint2", "Checkpoint3", "Gimmick4_Heavy", "Gimmick5_SpiralWind", "Goal",
        "WoodStairs2", "TopLanding", "Mushrooms/TowerMushroom", "Mushrooms/GoalMushroom",
        "Mushrooms/GapCap1", "Mushrooms/GapCap2", "Mushrooms/GapCap3", "Mushrooms/GapCap4",
        "PhotoPoints/PhotoPoint2", "PhotoPoints/PhotoPoint3",
    };

    [MenuItem("FuwaCourse/済み（押す前に相談）/きのこのたに：赤キノコの後にチェックポイント（先を3m奥へ）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var c5 = GameObject.Find("FuwaCourse5")?.transform; if (!c5) return "FuwaCourse5 がない";
        if (c5.Find("CheckpointDeck")) return "もう CheckpointDeck がある（実行済み）";
        var cap6 = c5.Find("Mushrooms/CapStep6"); var origin = cap6.position;
        Vector3 v = D * Shift; var log = new List<string>();

        // ---- 1. 先の物をずらす ----
        int moved = 0;
        foreach (var n in MoveNames) { var t = c5.Find(n); if (!t) { log.Add("ない:" + n); continue; } Undo.RecordObject(t, "shift"); t.position += v; moved++; }
        foreach (Transform t in c5.Find("Deco")) if (t.position.x < SplitX) { Undo.RecordObject(t, "shift"); t.position += v; moved++; }
        foreach (var t in c5.Find("KinokoEntrance").GetComponentsInChildren<Transform>(true))
            if (t.name == "FaceTab" || t.name == "TabDeck") { Undo.RecordObject(t, "shift"); t.position += v; moved++; }
        log.Add("ずらした物 " + moved);
        log.Add(ShiftEntranceMeshes());

        // ---- 2. 板を足す ----
        log.Add(BuildExtension(c5, origin));

        // ---- 3. 門 ----
        log.Add(BuildCheckpoint(c5, origin));

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        return "[きのこのたにのチェックポイント] " + string.Join(" / ", log);
    }

    // 道のメッシュ（見た目・判定）と入口の板の、奥側の塊だけずらす
    public static string ShiftEntranceMeshes()
    {
        var c5 = GameObject.Find("FuwaCourse5").transform; Vector3 v = D * Shift; var log = new List<string>();
        var pm = c5.Find("PathMesh");
        var mf = pm.GetComponent<MeshFilter>(); var mc = pm.GetComponent<MeshCollider>();
        log.Add("道 " + ShiftIslands(mf.sharedMesh, pm, v));
        if (mc.sharedMesh != mf.sharedMesh) log.Add("道の判定 " + ShiftIslands(mc.sharedMesh, pm, v));
        var m = mc.sharedMesh; mc.sharedMesh = null; mc.sharedMesh = m;
        var deck = c5.Find("KinokoEntrance/Deck");
        if (deck) log.Add("入口の板 " + ShiftIslands(deck.GetComponent<MeshFilter>().sharedMesh, deck, v));
        return string.Join("・", log);
    }

    static string ShiftIslands(Mesh m, Transform t, Vector3 worldShift)
    {
        var v = m.vertices; var groups = Islands(m, 200f); var lv = t.InverseTransformVector(worldShift); int n = 0;
        foreach (var g in groups)
        {
            Vector3 c = Vector3.zero; foreach (var i in g) c += t.TransformPoint(v[i]); c /= g.Count;
            if (c.x >= SplitX) continue;
            foreach (var i in g) v[i] += lv; n++;
        }
        m.vertices = v; m.RecalculateBounds(); EditorUtility.SetDirty(m);
        return n + "/" + groups.Count;
    }

    // つながった三角形の塊（同じ位置の頂点はつなぐ）
    static List<List<int>> Islands(Mesh m, float weld)
    {
        var v = m.vertices; var tri = m.triangles; var par = new int[v.Length];
        for (int i = 0; i < par.Length; i++) par[i] = i;
        int F(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        var key = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++) { var k = Vector3Int.RoundToInt(v[i] * weld); if (key.TryGetValue(k, out int j)) par[F(i)] = F(j); else key[k] = i; }
        for (int i = 0; i < tri.Length; i += 3) { par[F(tri[i + 1])] = F(tri[i]); par[F(tri[i + 2])] = F(tri[i]); }
        return Enumerable.Range(0, v.Length).GroupBy(F).Select(g => g.ToList()).ToList();
    }

    // 着地の板（ずらす前の位置で）の板を写して、横に広げて並べる＋下の桁（右の桁を写す・左は鏡写し）＋判定の箱
    public static string BuildExtension(Transform c5, Vector3 origin)
    {
        var old = c5.Find("Deck5Ext"); if (old) Object.DestroyImmediate(old.gameObject);   // 作り直し（外から参照されていない）
        var deckT = c5.Find("Deck5/Deck5"); var src = deckT.GetComponent<MeshFilter>().sharedMesh; var shiftV = D * Shift;
        var sv = src.vertices; var sn = src.normals; var suv = src.uv; var scol = src.colors; var stri = src.triangles;
        // ずらした後なので、元の位置＝今の位置 − shiftV
        Vector3 W(int i) => deckT.TransformPoint(sv[i]) - shiftV;
        float Fo(Vector3 p) => Vector3.Dot(p - origin, D);
        float So(Vector3 p) => Vector3.Dot(p - origin, S);
        var isl = Islands(src, 1000f);
        // 着地の板の、端から端まである板（24頂点の箱・幅 4m 以上・手前 2.5m の中）
        var planks = isl.Where(g => g.Count == 24).Select(g => (g, f0: g.Min(i => Fo(W(i))), f1: g.Max(i => Fo(W(i))), s0: g.Min(i => So(W(i))), s1: g.Max(i => So(W(i))), y: g.Max(i => W(i).y)))
            .Where(p => p.s1 - p.s0 > 4f && p.f1 < ExtF0 + 2.6f && p.y < 16f).OrderBy(p => p.f0).ToList();
        var beam = isl.FirstOrDefault(g => g.Count == 144);
        if (planks.Count < 3 || beam == null) return "板が見つからない（" + planks.Count + "）";
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var C = new List<Color>(); var T = new List<int>();
        var rnd = new System.Random(511);
        float R(float a) => (float)(rnd.NextDouble() * 2 - 1) * a;
        void Add(List<int> g, System.Func<Vector3, Vector3> map, System.Func<Vector3, Vector3> nmap, bool flip)
        {
            var idx = new Dictionary<int, int>();
            foreach (var i in g) { idx[i] = V.Count; V.Add(map(W(i))); N.Add(nmap(deckT.TransformDirection(sn[i])).normalized); UV.Add(suv.Length > 0 ? suv[i] : Vector2.zero); C.Add(scol.Length > 0 ? scol[i] : Color.white); }
            var set = new HashSet<int>(g);
            for (int k = 0; k < stri.Length; k += 3)
            {
                if (!set.Contains(stri[k])) continue;
                int a = idx[stri[k]], b = idx[stri[k + 1]], c = idx[stri[k + 2]];
                if (flip) { T.Add(a); T.Add(c); T.Add(b); } else { T.Add(a); T.Add(b); T.Add(c); }
            }
        }
        // 板：ピッチは元の板と同じくらい（Shift を割り切れる枚数）。横は ExtHalfW まで広げる
        float pitch0 = (planks.Last().f0 - planks.First().f0) / Mathf.Max(1, planks.Count - 1);
        int count = Mathf.Max(1, Mathf.RoundToInt(Shift / pitch0)); float pitch = Shift / count;
        float stretch = ExtHalfW / DeckHalfW;
        for (int j = 0; j < count; j++)
        {
            var p = planks[j % planks.Count];
            float fc = (p.f0 + p.f1) / 2, target = ExtF0 + pitch * (j + 0.5f);
            float dF = target - fc, dS = R(0.06f), dY = R(0.012f), yaw = R(1.2f);
            var rot = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 center = origin + D * fc + S * DeckCenterS; center.y = 0;
            Add(p.g, w =>
            {
                float f = Fo(w), s = So(w); float s2 = DeckCenterS + (s - DeckCenterS) * stretch + dS;
                var q = origin + D * (f + dF) + S * s2; q.y = w.y + dY;
                var piv = origin + D * target + S * DeckCenterS; piv.y = q.y;
                return piv + rot * (q - piv);
            }, n => rot * n, false);
        }
        // 桁：右の桁（板の +S 側の端の下）を、足す板の手前から奥まで。左は鏡写し
        float bf0 = beam.Min(i => Fo(W(i))); float bS = beam.Average(i => So(W(i)));
        float outS = Mathf.Sign(bS - DeckCenterS) * (ExtHalfW - DeckHalfW);   // 外へ広げた分（桁のある側へ）
        Add(beam, w => { var q = origin + D * (Fo(w) - bf0 + ExtF0) + S * (So(w) + outS); q.y = w.y; return q; }, n => n, false);
        Add(beam, w => { float s = 2 * DeckCenterS - (So(w) + outS); var q = origin + D * (Fo(w) - bf0 + ExtF0) + S * s; q.y = w.y; return q; },
            n => n - 2 * Vector3.Dot(n, S) * S, true);

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ExtMeshPath);
        if (!mesh) { mesh = new Mesh { name = "Deck5Ext" }; AssetDatabase.CreateAsset(mesh, ExtMeshPath); }
        mesh.Clear(false);
        var go = new GameObject("Deck5Ext"); Undo.RegisterCreatedObjectUndo(go, "ext");
        go.transform.SetParent(c5, false); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
        go.transform.SetSiblingIndex(c5.Find("Deck5").GetSiblingIndex() + 1);
        var vis = new GameObject("Deck5Ext"); vis.transform.SetParent(go.transform, false);
        var lv = V.Select(p => vis.transform.InverseTransformPoint(p)).ToArray();
        mesh.vertices = lv; mesh.normals = N.Select(n => vis.transform.InverseTransformDirection(n)).ToArray(); mesh.uv = UV.ToArray(); mesh.colors = C.ToArray(); mesh.triangles = T.ToArray();
        mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        vis.AddComponent<MeshFilter>().sharedMesh = mesh;
        vis.AddComponent<MeshRenderer>().sharedMaterials = deckT.GetComponent<MeshRenderer>().sharedMaterials;
        vis.layer = deckT.gameObject.layer; GameObjectUtility.SetStaticEditorFlags(vis, GameObjectUtility.GetStaticEditorFlags(deckT.gameObject));
        // 判定：上面 FloorY の薄い箱。奥は着地の板（ずらした後）に 5cm 重ねる
        var colGo = new GameObject("Deck5Ext_Col"); colGo.transform.SetParent(go.transform, false);
        var cc = origin + D * (ExtF0 + Shift / 2 + 0.025f) + S * DeckCenterS; cc.y = FloorY - 0.1f;
        colGo.transform.position = cc; colGo.transform.rotation = Quaternion.LookRotation(D);
        var bc = colGo.AddComponent<BoxCollider>(); bc.size = new Vector3(ExtHalfW * 2, 0.2f, Shift + 0.05f);
        colGo.layer = c5.Find("Deck5/Deck5_Col").gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(colGo, GameObjectUtility.GetStaticEditorFlags(c5.Find("Deck5/Deck5_Col").gameObject));
        return "板 " + count + "枚＋桁2（頂点 " + V.Count + "）";
    }

    // チェックポイント2を写して、足した板の上に。光るキノコは板の上（両脇）へ。ふわふわの一覧・遠くの影の一覧にも足す
    static string BuildCheckpoint(Transform c5, Vector3 origin)
    {
        var src = c5.Find("Checkpoint2");
        var cp = Object.Instantiate(src.gameObject, c5).transform; cp.name = "CheckpointDeck"; Undo.RegisterCreatedObjectUndo(cp.gameObject, "cp");
        cp.SetSiblingIndex(c5.Find("Checkpoint1").GetSiblingIndex() + 1);
        float rootDrop = src.Find("Post_L").GetComponent<Renderer>().bounds.min.y - src.position.y;   // 根元（道の高さ）からルートまで
        var p = origin + D * CpF + S * DeckCenterS; p.y = FloorY - rootDrop;
        cp.position = p; cp.rotation = src.rotation;
        var gm = cp.Find("GlowMushrooms");
        foreach (var (n, sgn) in new[] { ("ClusterL", -1f), ("ClusterR", 1f) })
        {
            var cl = gm.Find(n); if (!cl) continue;
            var lp = cl.localPosition; cl.localPosition = new Vector3(sgn * ClusterX, lp.y + ClusterLift, ClusterZ);
        }
        var fc = cp.GetComponentInChildren<FuwaCheckpoint>(true);
        UdonSharpEditorUtility.CopyProxyToUdon(fc); EditorUtility.SetDirty(fc);
        int balls = 0;
        foreach (var ball in Object.FindObjectsOfType<FuwaBall>(true))
        {
            if (ball.checkpoints == null || !ball.checkpoints.Any(c => c != null && c.transform.IsChildOf(c5))) continue;
            var so = new SerializedObject(ball); var arr = so.FindProperty("checkpoints");
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fc;
            so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(ball); EditorUtility.SetDirty(ball); balls++;
        }
        // 遠くの影の一覧：元（チェックポイント2）の物が入っていれば、写した先も
        int shadows = 0;
        foreach (var f in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            if (f.targets == null) continue;
            var list = f.targets.ToList(); int add = 0;
            foreach (var r in f.targets)
            {
                if (r == null || !r.transform.IsChildOf(src)) continue;
                var t = cp.Find(Rel(r.transform, src)); var rr = t ? t.GetComponent<Renderer>() : null;
                if (rr && !list.Contains(rr)) { rr.shadowCastingMode = r.shadowCastingMode; list.Add(rr); add++; }
            }
            if (add == 0) continue;
            Undo.RecordObject(f, "shadows");
            f.targets = list.ToArray(); f.centers = list.Select(x => x ? x.bounds.center : Vector3.zero).ToArray(); f.radii = list.Select(x => x ? x.bounds.extents.magnitude : 0f).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(f); EditorUtility.SetDirty(f); shadows += add;
        }
        return "門（ふわふわの一覧 " + balls + "・影 " + shadows + "）";
    }

    static string Rel(Transform t, Transform top) => t.parent == top ? t.name : Rel(t.parent, top) + "/" + t.name;
}
