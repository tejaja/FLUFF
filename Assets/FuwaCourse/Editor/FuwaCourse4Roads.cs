using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// まもののすの最初と最後の木の橋を、ほかのコースと同じシマシマ道（巣の小枝っぽい茶色）に置きかえる（2026-10-09 てじゃ
// 「道と道で挟めば、道の途中にできた巣っぽい」）。見た目だけ差しかえ：当たり判定は橋のもの（BridgeA_Col / BridgeD_Col）をそのまま使う。
// 道の上面は橋の上面と同じ高さ。スタート島の側は島の下にもぐらせる、ゴールの側は島のふちまで。
// 巣とのつなぎ目は「道が割れて途切れていて、まものが小枝を並べてツタでしばってつないだ」形（2026-10-09 てじゃ「割れてる道に、なにかで繋いでるみたいな」）。
// 最初の巣の手前には、イバラを試せる小さい巣（NestStart。ほかの巣と同じく den.py で作る）：スタート → シマシマ道 → 小枝のつなぎ → 小さい巣（イバラ）→ 橋 BridgeA2 → 最初の巣
// （2026-10-09 てじゃ。場所が足りないので、スタート島以外のコース全体を奥へずらしてある）。
// 何度実行してもよい（道は作り直し、橋の見た目は非表示にするだけで消さない）
public static class FuwaCourse4Roads
{
    const float Thick = 0.3f, Bevel = 0.08f, Stripe = 0.6f;
    const int BevelSteps = 4, TopSteps = 14;
    const float GapA = 1.9f, GapD = 0.75f;   // 割れ目（小枝でつないだ所）の長さ
    const float IsletR = 2.0f;               // 最初の巣の手前の、イバラを試せる小さい巣の半径
    const float IsletBridge = 3.2f;          // 小さい巣 → 最初の巣 の木の橋の長さ
    const float RoadMin = 2.5f;              // スタート島からのシマシマ道の、最低の長さ
    const float JagMax = 0.36f;              // 割れたはしの、いちばん欠けている所の深さ
    const string MeshDir = "Assets/FuwaCourse/Meshes/Course4";

    [MenuItem("FuwaCourse/まもののす/最初と最後の橋をシマシマ道にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var c4 = GameObject.Find("FuwaCourse4").transform;
        var den = c4.Find("Den");
        Physics.SyncTransforms();   // 島を動かした直後でも、当たり判定を新しい位置で測れるように
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Course4");
        var matA = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/LobbyCourse4_PathA.mat");
        var matB = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/LobbyCourse4_PathB.mat");
        var log = new System.Text.StringBuilder();
        var startG = c4.Find("Start/StartArea/Ground"); var goalG = c4.Find("Goal/GoalGround");
        // (橋, 始まりの島, 終わりの島, 始まり側の床, 終わり側の床, …, 始まり側の割れ目, 終わり側の割れ目)
        // スタート → シマシマ道 → 小枝のつなぎ → イバラの小さい巣（NestStart。巣と橋は den.py で作る）→ 橋 BridgeA2 → 最初の巣
        var ns = den.Find("NestStart");
        log.Append(Road(den, "BridgeA", startG.position, ns.GetComponent<Renderer>().bounds.center, startG.GetComponent<Collider>(), den.Find("NestStart_Col").GetComponent<Collider>(), matA, matB, "Course4_Road_Start", 0f, GapA, 0f, null, out _));
        // 最初のイバラを小さい巣のまん中へ
        var sp = c4.Find("Spikes/SpikePatch0_Start");
        if (sp != null)
        {
            Undo.RecordObject(sp, "move bramble"); Undo.RecordObject(sp.gameObject, "show bramble");
            // 巣の入口（スタート側）をふさぐように置く：まん中だと巣1のけだまに、イバラの中にいるうちに見つかる（2026-10-09 てじゃ）
            var to = ns.GetComponent<Renderer>().bounds.center; var back = startG.position - to; back.y = 0f;
            to += back.normalized * 1.35f; to.y = sp.position.y; sp.position = to; sp.gameObject.SetActive(true);
        }
        log.Append(Road(den, "BridgeD", den.Find("MamaNestCenter").position, goalG.position, den.Find("MamaNest_Col").GetComponent<Collider>(), goalG.GetComponent<Collider>(), matA, matB, "Course4_Road_Goal", GapD, 0f, 0f, null, out _));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c4.gameObject.scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        m.name = name;
        string path = MeshDir + "/" + name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) { AssetDatabase.CreateAsset(m, path); return m; }
        ex.Clear(); EditorUtility.CopySerialized(m, ex); ex.name = name; return ex;
    }

    // a→b の向きに、橋の当たり判定の上面（幅・高さ）を測って道を作る。
    // fromCol があれば「a 側の島（巣）の床が終わる所」から、なければ橋のはしから（島の下にもぐる）。toCol も同じ。
    // gap0 / gap1：そのはしの手前で道を割って、その長さを小枝でつなぐ
    static string Road(Transform den, string bridge, Vector3 a3, Vector3 b3, Collider fromCol, Collider toCol, Material mA, Material mB, string name, float gap0, float gap1, float isletR, Transform isletLike, out Vector3? islet)
    {
        islet = null;
        var br = den.Find(bridge); var bcolT = den.Find(bridge + "_Col"); var bcol = bcolT.GetComponent<Collider>();
        bool bcolWasOn = bcolT.gameObject.activeSelf; bcolT.gameObject.SetActive(true);   // 測るあいだだけ（道の当たり判定に置きかえた後も測れるように）
        Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z), dir = (b - a).normalized, right = new Vector2(dir.y, -dir.x);
        float L = (b - a).magnitude;
        bool Hit(Collider c, Vector2 p, out float y) { y = 0f; if (c.Raycast(new Ray(new Vector3(p.x, 50f, p.y), Vector3.down), out var h, 100f)) { y = h.point.y; return true; } return false; }
        // 橋の範囲・幅・上面
        float s0 = 99f, s1 = -99f, xl = 99f, xr = -99f, top = -99f;
        for (float s = 0f; s <= L; s += 0.05f)
            for (float x = -3f; x <= 3f; x += 0.05f)
                if (Hit(bcol, a + dir * s + right * x, out var y)) { s0 = Mathf.Min(s0, s); s1 = Mathf.Max(s1, s); xl = Mathf.Min(xl, x); xr = Mathf.Max(xr, x); top = Mathf.Max(top, y); }
        // はし：島（巣）の床が終わる所。なければ橋のはし
        float e0 = s0, e1 = s1;
        if (fromCol != null) { for (float s = 0f; s <= L; s += 0.02f) if (!Hit(fromCol, a + dir * s, out _)) { e0 = s; break; } }
        if (toCol != null) { for (float s = L; s >= 0f; s -= 0.02f) if (!Hit(toCol, a + dir * s, out _)) { e1 = s + 0.02f; break; } }
        float halfW = (xr - xl) * 0.5f, cx = (xr + xl) * 0.5f;
        var o = a + right * cx;
        var pos = new Vector3(o.x, 0f, o.y); var rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
        var flags = GameObjectUtility.GetStaticEditorFlags(br.gameObject);

        GameObject Put(string n, Mesh m, Material[] mats)
        {
            var old = den.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(den, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            go.layer = br.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            return go;
        }
        // 小さい巣（isletR > 0）があるとき：巣の側から 木の橋 → 小さい巣 → 小枝のつなぎ → シマシマ道 の順に決める
        bool hasIslet = isletR > 0f && gap1 > 0f;
        float iFar = e1 - IsletBridge, sc = iFar - isletR, iNear = sc - isletR, rEnd = iNear - gap1;
        foreach (var n in new[] { name + "_Islet", name + "_Nestlet", name + "_Bridge", name + "_IsletCol", name + "_BridgeCol" }) { var oo = den.Find(n); if (oo != null) Object.DestroyImmediate(oo.gameObject); }
        var twigMat = br.GetComponent<Renderer>().sharedMaterial;
        if (hasIslet)
        {
            if (rEnd - e0 < RoadMin) Debug.LogWarning("まもののす：スタートの道が短い " + (rEnd - e0).ToString("F2") + "m（コースを奥へずらす）");
            var c2 = a + dir * sc + right * cx;
            var ig = new GameObject(name + "_Nestlet").transform; ig.SetParent(den, false);
            ig.SetPositionAndRotation(new Vector3(c2.x, top + 0.01f, c2.y), rot);   // 床は道より1cm上
            float gapHalf = Mathf.Asin(Mathf.Min(0.95f, (halfW + 0.15f) / isletR));
            var vis = new GameObject("Nest", typeof(MeshFilter), typeof(MeshRenderer)); vis.transform.SetParent(ig, false);
            vis.GetComponent<MeshFilter>().sharedMesh = SaveMesh(Nestlet(isletR, gapHalf, 4242), name + "_Nestlet");
            vis.GetComponent<MeshRenderer>().sharedMaterial = twigMat;
            vis.layer = br.gameObject.layer; GameObjectUtility.SetStaticEditorFlags(vis, flags);
            // 当たり判定：床（円盤）＋ふち（入口2か所はあける）
            var fl = new GameObject("FloorCol", typeof(MeshCollider)); fl.transform.SetParent(ig, false); fl.layer = bcolT.gameObject.layer;
            fl.GetComponent<MeshCollider>().sharedMesh = SaveMesh(Disc(isletR + 0.1f, 32), name + "_NestletFloorCol");
            int seg = 18;
            for (int k = 0; k < seg; k++)
            {
                float an = (k + 0.5f) * Mathf.PI * 2f / seg;
                float da = Mathf.Abs(Mathf.DeltaAngle(an * Mathf.Rad2Deg, 0f)) * Mathf.Deg2Rad, db = Mathf.Abs(Mathf.DeltaAngle(an * Mathf.Rad2Deg, 180f)) * Mathf.Deg2Rad;
                if (da < gapHalf + 0.05f || db < gapHalf + 0.05f) continue;   // 入口（道の向き＝ローカル z の ±）
                var rc = new GameObject("RimCol" + k, typeof(BoxCollider)); rc.transform.SetParent(ig, false); rc.layer = bcolT.gameObject.layer;
                rc.transform.localPosition = new Vector3(Mathf.Sin(an), 0f, Mathf.Cos(an)) * (isletR + 0.05f);
                rc.transform.localRotation = Quaternion.Euler(0f, an * Mathf.Rad2Deg, 0f);   // 箱の横（x）がふちにそう向き
                var bc = rc.GetComponent<BoxCollider>(); bc.size = new Vector3(2f * (isletR + 0.05f) * Mathf.Sin(Mathf.PI / seg) + 0.05f, 0.4f, 0.22f); bc.center = new Vector3(0f, 0.15f, 0f);
            }
            // 木の橋（小さい巣 → 最初の巣）
            Put(name + "_Bridge", SaveMesh(PlankBridge(iFar - 0.35f, e1 + 0.35f, halfW * 0.92f, top, 977), name + "_Bridge"), new[] { twigMat });
            islet = new Vector3(c2.x, top, c2.y);
        }
        // 道（割れ目のぶん短くして、割れた側のはしをギザギザに）。スタート島の側は島の下に 0.4m もぐらせる
        float roadStart = (fromCol != null && gap0 <= 0f) || hasIslet ? e0 - 0.4f : e0 + gap0;   // 割れ目のない島の側は、島の地面の下に 0.4m もぐらせる
        float roadEnd = hasIslet ? rEnd : e1 - gap1;
        Put(name, SaveMesh(Strip(roadStart, roadEnd, halfW, top, !hasIslet && gap0 > 0f, gap1 > 0f), name), new[] { mA, mB });
        // 割れ目をつなぐ小枝（見えるのは割れ目の中だけ。ツタは両がわの床・道の上まで）
        string pn = name + "_Patch";
        var oldP = den.Find(pn); if (oldP != null) Object.DestroyImmediate(oldP.gameObject);
        if (gap0 > 0f || gap1 > 0f)
        {
            float nearEnd = hasIslet ? iNear : e1;   // 割れ目の向こう（小さい巣か巣の床）のふち
            float p0 = gap0 > 0f ? e0 - 0.25f : nearEnd - gap1 - JagMax;
            float p1 = gap0 > 0f ? e0 + gap0 + JagMax : nearEnd + 0.25f;
            float v0 = gap0 > 0f ? e0 : nearEnd - gap1, v1 = gap0 > 0f ? e0 + gap0 : nearEnd;
            Put(pn, SaveMesh(Patch(p0, p1, v0, v1, halfW, top, name.Length * 7919 + 17), pn), new[] { twigMat });
        }
        br.gameObject.SetActive(false);   // 木の橋の見た目は消さずに非表示
        // 当たり判定：島（巣）どうしの間が橋より長い時や小さい巣がある時は、橋の当たり判定の代わりに道の形の箱を置く
        string cn = name + "_Col";
        var oldC = den.Find(cn); if (oldC != null) Object.DestroyImmediate(oldC.gameObject);
        void Box(string n, float z0b, float z1b, float w)
        {
            var cg = new GameObject(n, typeof(BoxCollider)); cg.transform.SetParent(den, false); cg.transform.SetPositionAndRotation(pos, rot);
            cg.layer = bcolT.gameObject.layer;
            var bx = cg.GetComponent<BoxCollider>(); bx.center = new Vector3(0f, top - Thick * 0.5f, (z0b + z1b) * 0.5f); bx.size = new Vector3(w, Thick, z1b - z0b);
        }
        if (hasIslet)
        {
            Box(cn, e0 - 0.3f, iNear + 0.3f, halfW * 2f);
            Box(name + "_BridgeCol", iFar - 0.3f, e1 + 0.3f, halfW * 2f * 0.92f);
            bcolWasOn = false;
        }
        else
        {
            var oldBC = den.Find(name + "_BridgeCol"); if (oldBC != null) Object.DestroyImmediate(oldBC.gameObject);
            bool boxed = e0 < s0 - 0.3f || e1 > s1 + 0.3f;
            if (boxed)
            {
                float need0 = fromCol != null ? e0 - 0.3f : s0, need1 = toCol != null ? e1 + 0.3f : s1;
                Box(cn, need0, need1, halfW * 2f);
            }
            bcolWasOn = !boxed;   // 箱にしない時は橋の当たり判定を使う
        }
        bcolT.gameObject.SetActive(bcolWasOn);
        return bridge + "→" + name + " " + e0.ToString("F2") + "〜" + e1.ToString("F2") + "m 幅" + (halfW * 2f).ToString("F2") + " 上面" + top.ToString("F2") + " / ";
    }

    static float Hash(int i) { float h = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }

    // ローカル：z が道の向き（z0〜z1）、x が横。上面 top、厚み Thick、上のふちを丸める。しまは Stripe ごと。
    // jag0 / jag1：そのはしを割れたギザギザにする（断面の点ごとに引っこめる量を変える）
    static Mesh Strip(float z0, float z1, float halfW, float top, bool jag0, bool jag1)
    {
        float bot = top - Thick;
        // 断面（閉じた輪）：左下 → 左の側面 → 丸め → 上面（細かく）→ 丸め → 右の側面 → 右下 → 底（細かく）
        var prof = new List<Vector4> { new Vector4(-halfW, bot, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-halfW + Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int k = 1; k < TopSteps; k++) prof.Add(new Vector4(Mathf.Lerp(-halfW + Bevel, halfW - Bevel, k / (float)TopSteps), top, 0, 1));
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(halfW - Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(halfW, bot, 1, 0));
        for (int k = 1; k < TopSteps; k++) prof.Add(new Vector4(Mathf.Lerp(halfW, -halfW, k / (float)TopSteps), bot, 0, -1));
        int P = prof.Count;
        // 引っこみ：となりどうしで大きくちがう＝ギザギザ。底は上より少し控えめ
        float Jag(int i) { float j = 0.05f + (JagMax - 0.05f) * Hash(i * 7 + 3); return prof[i].w < -0.5f ? j * 0.7f + 0.04f : j; }
        var cuts = new List<float> { z0 };
        for (float z = Mathf.Ceil(z0 / Stripe) * Stripe; z < z1 - 1e-3f; z += Stripe) if (z > z0 + 1e-3f) cuts.Add(z);
        cuts.Add(z1);
        // ギザギザのはしの手前の区切りは、引っこみより奥に（重ならないように）
        if (jag0) { cuts.RemoveAll(z => z > z0 && z < z0 + JagMax + 0.05f); cuts.Add(z0 + JagMax + 0.05f); }
        if (jag1) { cuts.RemoveAll(z => z < z1 && z > z1 - JagMax - 0.05f); cuts.Add(z1 - JagMax - 0.05f); }
        cuts.Sort();
        int last = cuts.Count - 1;
        float ZAt(int ring, int i) { float z = cuts[ring]; if (jag0 && ring == 0) z += Jag(i); if (jag1 && ring == last) z -= Jag(i); return z; }
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var A = new List<int>(); var B = new List<int>();
        for (int s = 0; s < last; s++)
        {
            var T = (Mathf.FloorToInt((cuts[s] + cuts[s + 1]) * 0.5f / Stripe) % 2 == 0) ? A : B;
            int b0 = V.Count;
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < P; i++) { var q = prof[i]; float z = ZAt(s + r, i); V.Add(new Vector3(q.x, q.y, z)); N.Add(new Vector3(q.z, q.w, 0)); UV.Add(new Vector2(q.x, z)); }
            for (int i = 0; i < P; i++) { int i1 = (i + 1) % P; int x0 = b0 + i, x1 = b0 + i1, y0 = b0 + P + i, y1 = b0 + P + i1; T.AddRange(new[] { x0, y0, x1, x1, y0, y1 }); }
        }
        // 両はしのふた：断面のまん中の点から扇形に（割れた面はでこぼこ）
        foreach (var (ring, sgn) in new[] { (0, -1f), (last, 1f) })
        {
            float zc = cuts[ring];
            var T = Mathf.FloorToInt((zc - sgn * 0.01f) / Stripe) % 2 == 0 ? A : B;
            float jc = (ring == 0 && jag0) ? JagMax * 0.5f : (ring == last && jag1) ? -JagMax * 0.5f : 0f;
            int cb = V.Count;
            V.Add(new Vector3(0f, (top + bot) * 0.5f, zc + jc)); N.Add(new Vector3(0, 0, sgn)); UV.Add(Vector2.zero);
            for (int i = 0; i < P; i++) { var q = prof[i]; V.Add(new Vector3(q.x, q.y, ZAt(ring, i))); N.Add(new Vector3(0, 0, sgn)); UV.Add(Vector2.zero); }
            for (int i = 0; i < P; i++) T.AddRange(new[] { cb, cb + 1 + i, cb + 1 + (i + 1) % P });
        }
        foreach (var T in new[] { A, B })
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 p0 = V[T[i]], p1 = V[T[i + 1]], p2 = V[T[i + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), N[T[i]] + N[T[i + 1]] + N[T[i + 2]]) < 0f) { int t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t; }
            }
        var m = new Mesh();
        m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV);
        m.subMeshCount = 2; m.SetTriangles(A, 0); m.SetTriangles(B, 1);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    // 頂点カラーの小枝・ツタを組み立てる（Den_Twig の材質用）
    class TwigMB
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>(); public List<Color> C = new List<Color>(); public List<int> T = new List<int>();
        // 先ほど細い丸太（両はしは明るめの切り口）。flatY < 1 で縦につぶす（板）
        public void Log(Vector3 p0, Vector3 p1, float r0, float r1, Color c, int seg, float flatY = 1f)
        {
            var ax = (p1 - p0).normalized; var u = Vector3.Cross(ax, Vector3.up); u = u.sqrMagnitude < 0.01f ? Vector3.right : u.normalized; var w = Vector3.Cross(u, ax);
            int b = V.Count;
            for (int e = 0; e < 2; e++)
                for (int k = 0; k < seg; k++)
                {
                    float an = k * Mathf.PI * 2f / seg; var n = u * Mathf.Cos(an) + w * Mathf.Sin(an);
                    var off = n * (e == 0 ? r0 : r1); off.y *= flatY;
                    V.Add((e == 0 ? p0 : p1) + off); N.Add(new Vector3(n.x, n.y / Mathf.Max(0.2f, flatY), n.z).normalized);
                    float sh = 0.9f + 0.1f * Mathf.Sin(an * 3f + p0.z * 9f + p0.x * 5f); C.Add(new Color(c.r * sh, c.g * sh, c.b * sh, 1f));
                }
            for (int k = 0; k < seg; k++) { int k1 = (k + 1) % seg; T.AddRange(new[] { b + k, b + seg + k, b + k1, b + k1, b + seg + k, b + seg + k1 }); }
            foreach (var (e, sgn) in new[] { (0, -1f), (1, 1f) })
            {
                int cb = V.Count; var cc = new Color(Mathf.Min(1f, c.r * 1.5f), Mathf.Min(1f, c.g * 1.5f), Mathf.Min(1f, c.b * 1.4f), 1f);
                V.Add(e == 0 ? p0 : p1); N.Add(ax * sgn); C.Add(cc);
                for (int k = 0; k < seg; k++) { V.Add(V[b + e * seg + k]); N.Add(ax * sgn); C.Add(cc); }
                for (int k = 0; k < seg; k++) T.AddRange(new[] { cb, cb + 1 + k, cb + 1 + (k + 1) % seg });
            }
        }
        // 点の列にそった管（ねじれないように前の輪から回す）。両はしの 0.12m は細く
        public void Tube(List<Vector3> path, float radius, Color c)
        {
            const int seg = 6;
            float total = 0f; var acc = new List<float> { 0f };
            for (int i = 1; i < path.Count; i++) { total += (path[i] - path[i - 1]).magnitude; acc.Add(total); }
            Vector3 prevT = (path[1] - path[0]).normalized;
            Vector3 u = Vector3.Cross(prevT, Vector3.up); u = u.sqrMagnitude < 1e-4f ? Vector3.right : u.normalized;
            int b0 = V.Count;
            for (int i = 0; i < path.Count; i++)
            {
                var tg = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                u = (Quaternion.FromToRotation(prevT, tg) * u).normalized; prevT = tg;
                var w = Vector3.Cross(tg, u);
                float edge = Mathf.Min(acc[i], total - acc[i]);
                float rr = radius * Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, edge / 0.12f));
                for (int k = 0; k < seg; k++)
                {
                    float an = k * Mathf.PI * 2f / seg; var n = u * Mathf.Cos(an) + w * Mathf.Sin(an);
                    V.Add(path[i] + n * rr); N.Add(n);
                    float sh = 0.88f + 0.12f * Mathf.Sin(acc[i] * 40f + an); C.Add(new Color(c.r * sh, c.g * sh, c.b * sh, 1f));
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
                for (int k = 0; k < seg; k++) { int k1 = (k + 1) % seg, a = b0 + i * seg; T.AddRange(new[] { a + k, a + seg + k, a + k1, a + k1, a + seg + k, a + seg + k1 }); }
            foreach (var (ri, sgn) in new[] { (0, -1f), (path.Count - 1, 1f) })
            {
                int cb = V.Count; var tg = (path[ri] - path[Mathf.Clamp(ri - (int)sgn, 0, path.Count - 1)]).normalized;
                V.Add(path[ri]); N.Add(tg); C.Add(c);
                for (int k = 0; k < seg; k++) T.AddRange(new[] { cb, b0 + ri * seg + k, b0 + ri * seg + (k + 1) % seg });
            }
        }
        public Mesh ToMesh()
        {
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 p0 = V[T[i]], p1 = V[T[i + 1]], p2 = V[T[i + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), N[T[i]] + N[T[i + 1]] + N[T[i + 2]]) < 0f) { int t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t; }
            }
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }
    }
    static readonly Color[] Browns = { new Color(0.47f, 0.32f, 0.21f), new Color(0.56f, 0.39f, 0.26f), new Color(0.40f, 0.27f, 0.18f), new Color(0.62f, 0.45f, 0.30f) };
    static readonly Color Vine = new Color(0.33f, 0.52f, 0.24f);

    // 割れ目をつなぐ小枝：道を横切る丸太（両はしは少し細い）を z0〜z1 にすき間なく並べ、左右2か所をツタでしばる。
    // 見えるのは v0〜v1（割れ目の中）の丸太だけ（巣の床や道の下に埋まる丸太は出さない。てじゃ）
    static Mesh Patch(float z0, float z1, float v0, float v1, float halfW, float top, int seed)
    {
        var rnd = new System.Random(seed);
        float R(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        var mb = new TwigMB();
        var logs = new List<(Vector3 c, float yaw, float r)>();
        float zz = z0;
        while (true)
        {
            float r = R(0.065f, 0.095f);
            if (zz + 2f * r > z1) break;
            zz += r;
            float len = halfW * 2f * R(0.92f, 1.12f), off = R(-0.12f, 0.12f), yaw = R(-7f, 7f) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var c = new Vector3(off, top - r * 0.55f + R(-0.01f, 0.015f), zz);
            float rA = r * R(0.75f, 0.95f), rB = r * R(0.75f, 0.95f); var col = Browns[rnd.Next(Browns.Length)];
            if (zz - r >= v0 && zz + r <= v1) { mb.Log(c - d * len * 0.5f, c + d * len * 0.5f, rA, rB, col, 7); logs.Add((c, yaw, (rA + rB) * 0.5f)); }
            zz += r + R(0f, 0.02f);
        }
        // ツタ：左右2本。1本のなめらかな管で、丸太の上をなぞって丸太のあいだで少したわむ。両はしの丸太にはひと巻きして、
        // 先は細くなりながら外側（道・巣の床の上）に少し垂らす（2026-10-09 てじゃ「つたも、もっと綺麗につなげられないかな」）
        if (logs.Count >= 2)
        {
            const float vr = 0.02f;
            foreach (float x0 in new[] { -halfW + 0.32f, halfW - 0.32f })
            {
                float x = x0 + R(-0.04f, 0.04f);
                Vector3 At(int i) { var l = logs[i]; return new Vector3(x, l.c.y, l.c.z + (x - l.c.x) * Mathf.Tan(l.yaw)); }
                var pts = new List<Vector3>();
                void Wrap(int i, float outward, float drift)
                {
                    var ce = At(i); float rr = logs[i].r + vr;
                    for (int k = 0; k <= 12; k++)
                    {
                        float t = k / 12f, an = Mathf.PI * 0.5f + outward * t * Mathf.PI * 2f;
                        pts.Add(ce + new Vector3(drift * (t - 0.5f), Mathf.Sin(an) * rr, Mathf.Cos(an) * rr));
                    }
                }
                int last = logs.Count - 1;
                var f = At(0); var l = At(last);
                pts.Add(new Vector3(x - 0.05f, top + vr * 0.6f, f.z - logs[0].r - 0.32f));
                pts.Add(new Vector3(x - 0.03f, top + vr * 0.8f, f.z - logs[0].r - 0.14f));
                Wrap(0, 1f, 0.07f);
                for (int i = 1; i <= last; i++)
                {
                    var a = At(i - 1); var b = At(i);
                    float ya = a.y + logs[i - 1].r + vr, yb = b.y + logs[i].r + vr;
                    pts.Add(new Vector3(x + R(-0.01f, 0.01f), Mathf.Min(ya, yb) - 0.018f, (a.z + b.z) * 0.5f));
                    if (i < last) pts.Add(new Vector3(x + R(-0.01f, 0.01f), yb, b.z));
                }
                Wrap(last, -1f, -0.07f);
                pts.Add(new Vector3(x + 0.03f, top + vr * 0.8f + 0.01f, l.z + logs[last].r + 0.14f));
                pts.Add(new Vector3(x + 0.05f, top + vr * 0.6f + 0.01f, l.z + logs[last].r + 0.32f));
                mb.Tube(Smooth(pts, 4), vr, Vine);
            }
        }
        return mb.ToMesh();
    }

    // 小さい巣：砂の床（円盤）＋ 下のおわん（小枝色）＋ 小枝を3段輪に積んだふち（ローカル z の ± の入口 gapHalf はあける）＋ 垂れた根っこ。床の上面が y=0
    static Mesh Nestlet(float R0, float gapHalf, int seed)
    {
        var rnd = new System.Random(seed);
        float Rn(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        var mb = new TwigMB();
        // 床
        Color sandA = new Color(0.82f, 0.70f, 0.52f), sandB = new Color(0.76f, 0.64f, 0.46f);
        int seg = 48, rings = 6;
        int fb = mb.V.Count;
        mb.V.Add(Vector3.zero); mb.N.Add(Vector3.up); mb.C.Add(sandA);
        for (int r = 1; r <= rings; r++)
            for (int k = 0; k < seg; k++)
            {
                float an = k * Mathf.PI * 2f / seg, rr = R0 * r / rings;
                mb.V.Add(new Vector3(Mathf.Sin(an) * rr, 0f, Mathf.Cos(an) * rr)); mb.N.Add(Vector3.up);
                mb.C.Add(Color.Lerp(sandA, sandB, 0.5f + 0.5f * Mathf.Sin(an * 5f + rr * 4f) * Mathf.Cos(rr * 3f - an * 2f)));
            }
        for (int k = 0; k < seg; k++) mb.T.AddRange(new[] { fb, fb + 1 + k, fb + 1 + (k + 1) % seg });
        for (int r = 1; r < rings; r++)
            for (int k = 0; k < seg; k++)
            {
                int a0 = fb + 1 + (r - 1) * seg + k, a1 = fb + 1 + (r - 1) * seg + (k + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                mb.T.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        // 下のおわん（外側へ少しふくらんで、下でしぼむ）
        var prof = new[] { new Vector2(R0 + 0.05f, -0.02f), new Vector2(R0 + 0.2f, -0.18f), new Vector2(R0 + 0.12f, -0.45f), new Vector2(R0 * 0.78f, -0.85f), new Vector2(R0 * 0.4f, -1.15f), new Vector2(0.02f, -1.3f) };
        int bb = mb.V.Count;
        for (int j = 0; j < prof.Length; j++)
        {
            var t2 = j == 0 ? prof[1] - prof[0] : j == prof.Length - 1 ? prof[j] - prof[j - 1] : prof[j + 1] - prof[j - 1];
            var n2 = new Vector2(-t2.y, t2.x).normalized; if (n2.x < 0f) n2 = -n2;
            for (int k = 0; k < seg; k++)
            {
                float an = k * Mathf.PI * 2f / seg;
                mb.V.Add(new Vector3(Mathf.Sin(an) * prof[j].x, prof[j].y, Mathf.Cos(an) * prof[j].x));
                mb.N.Add(new Vector3(Mathf.Sin(an) * n2.x, n2.y, Mathf.Cos(an) * n2.x).normalized);
                float st = 0.85f + 0.15f * Mathf.Sin(an * 23f + j * 1.7f);
                var c = Color.Lerp(Browns[2], Browns[0], 0.5f + 0.5f * Mathf.Sin(an * 7f + j)); mb.C.Add(new Color(c.r * st, c.g * st, c.b * st, 1f));
            }
        }
        for (int j = 0; j < prof.Length - 1; j++)
            for (int k = 0; k < seg; k++)
            {
                int a0 = bb + j * seg + k, a1 = bb + j * seg + (k + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                mb.T.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        // ふち：小枝を3段、輪に積む（入口はあける）
        bool InGap(float an) { float d0 = Mathf.Abs(Mathf.DeltaAngle(an * Mathf.Rad2Deg, 0f)) * Mathf.Deg2Rad, d1 = Mathf.Abs(Mathf.DeltaAngle(an * Mathf.Rad2Deg, 180f)) * Mathf.Deg2Rad; return d0 < gapHalf || d1 < gapHalf; }
        for (int layer = 0; layer < 3; layer++)
        {
            int n = Mathf.CeilToInt(Mathf.PI * 2f * R0 / 0.42f);
            for (int k = 0; k < n; k++)
            {
                float a0 = (k + Rn(-0.2f, 0.2f) + layer * 0.37f) * Mathf.PI * 2f / n, a1 = a0 + Mathf.PI * 2f / n * Rn(1.3f, 1.7f);
                if (InGap(a0) || InGap(a1) || InGap((a0 + a1) * 0.5f)) continue;
                float rr0 = R0 + Rn(-0.04f, 0.08f), rr1 = R0 + Rn(-0.04f, 0.08f), y = 0.05f + layer * 0.085f;
                var p0 = new Vector3(Mathf.Sin(a0) * rr0, y + Rn(-0.02f, 0.03f), Mathf.Cos(a0) * rr0);
                var p1 = new Vector3(Mathf.Sin(a1) * rr1, y + Rn(-0.02f, 0.03f), Mathf.Cos(a1) * rr1);
                float r = Rn(0.045f, 0.07f);
                mb.Log(p0, p1, r * Rn(0.7f, 1f), r * Rn(0.6f, 0.9f), Browns[rnd.Next(Browns.Length)], 6);
            }
        }
        // 入口のはしに、つき出た小枝を少し（ふちが急に切れて見えないように）
        foreach (float ga in new[] { gapHalf, -gapHalf, Mathf.PI + gapHalf, Mathf.PI - gapHalf })
            for (int q = 0; q < 2; q++)
            {
                var b = new Vector3(Mathf.Sin(ga), 0f, Mathf.Cos(ga));
                var p0 = b * (R0 + 0.05f) + Vector3.up * (0.08f + q * 0.1f);
                var p1 = p0 + Vector3.up * Rn(0.15f, 0.32f) + b * Rn(0.05f, 0.2f) + Vector3.Cross(Vector3.up, b) * Rn(-0.1f, 0.1f);
                mb.Log(p0, p1, 0.035f, 0.015f, Browns[rnd.Next(Browns.Length)], 5);
            }
        // 垂れた根っこ
        for (int k = 0; k < 7; k++)
        {
            float an = Rn(0f, Mathf.PI * 2f), rr = Rn(0.2f, R0 * 0.75f);
            var p0 = new Vector3(Mathf.Sin(an) * rr, -1.3f + (rr / R0) * 0.5f, Mathf.Cos(an) * rr);
            var p1 = p0 + new Vector3(Rn(-0.3f, 0.3f), -Rn(0.6f, 1.4f), Rn(-0.3f, 0.3f));
            mb.Log(p0, p1, 0.05f, 0.008f, new Color(0.30f, 0.22f, 0.24f), 5);
        }
        return mb.ToMesh();
    }

    // 当たり判定用の円盤（上向き、y=0）
    static Mesh Disc(float r, int seg)
    {
        var v = new List<Vector3> { Vector3.zero }; var t = new List<int>();
        for (int k = 0; k < seg; k++) { float an = k * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Sin(an) * r, 0f, Mathf.Cos(an) * r)); }
        for (int k = 0; k < seg; k++) t.AddRange(new[] { 0, 1 + (k + 1) % seg, 1 + k });
        // 上から当たるように、面を上向きに（裏向きだと上からの当たり判定が抜ける）
        if (Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]).y < 0f) for (int i = 0; i < t.Count; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 木の橋：道の向きに板（平たい丸太）を5本並べ、3か所をツタで横にしばる。板の上面は top より少しだけ上
    static Mesh PlankBridge(float z0, float z1, float halfW, float top, int seed)
    {
        var rnd = new System.Random(seed);
        float Rn(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        var mb = new TwigMB();
        int n = 5; float pw = halfW * 2f / n;
        var tops = new List<float>();
        for (int i = 0; i < n; i++)
        {
            float x = -halfW + pw * (i + 0.5f) + Rn(-0.03f, 0.03f), r = pw * 0.5f * Rn(0.92f, 1.0f), flat = 0.45f;
            float y = top - r * flat + 0.006f;
            mb.Log(new Vector3(x, y, z0 + Rn(-0.1f, 0.1f)), new Vector3(x + Rn(-0.04f, 0.04f), y, z1 + Rn(-0.1f, 0.1f)), r, r * Rn(0.85f, 1f), Browns[rnd.Next(Browns.Length)], 8, flat);
            tops.Add(y + r * flat);
        }
        // 横にしばるツタ（板の上をなぞって、両はしは板の下へ回りこむ）
        foreach (float tz in new[] { z0 + 0.6f, (z0 + z1) * 0.5f, z1 - 0.6f })
        {
            var pts = new List<Vector3>();
            float zj = tz + Rn(-0.08f, 0.08f);
            pts.Add(new Vector3(-halfW - 0.02f, top - 0.12f, zj));
            pts.Add(new Vector3(-halfW - 0.03f, top - 0.02f, zj));
            for (int i = 0; i < n; i++)
            {
                float x = -halfW + pw * (i + 0.5f);
                pts.Add(new Vector3(x, tops[i] + 0.018f, zj + Rn(-0.02f, 0.02f)));
                if (i < n - 1) pts.Add(new Vector3(x + pw * 0.5f, tops[i] + 0.004f, zj));
            }
            pts.Add(new Vector3(halfW + 0.03f, top - 0.02f, zj));
            pts.Add(new Vector3(halfW + 0.02f, top - 0.12f, zj));
            mb.Tube(Smooth(pts, 3), 0.02f, Vine);
        }
        return mb.ToMesh();
    }

    // 折れ線をなめらかに（Catmull-Rom、区間ごとに n 点）
    static List<Vector3> Smooth(List<Vector3> p, int n)
    {
        var o = new List<Vector3>();
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector3 a = p[Mathf.Max(0, i - 1)], b = p[i], c = p[i + 1], d = p[Mathf.Min(p.Count - 1, i + 2)];
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                o.Add(0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3));
            }
        }
        o.Add(p[p.Count - 1]);
        return o;
    }
}
