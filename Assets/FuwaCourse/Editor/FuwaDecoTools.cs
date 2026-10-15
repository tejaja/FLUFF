using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 道のふちの飾り（Blender の deco.py で作った草・花・小石・ツタ）を取り込んで、コースの道のふちに「ところどころ」置く。
// どのコースでも使えるように、道の当たり判定(PathMesh の MeshCollider)を上から調べてふちを見つける。
public static class FuwaDecoTools
{
    const string MeshDir = "Assets/FuwaCourse/Meshes/Deco";
    const string MatDir = "Assets/FuwaCourse/Materials/Deco";
    const string PrefabDir = "Assets/FuwaCourse/Prefabs/Deco";

    static readonly Dictionary<string, Color> MatColors = new Dictionary<string, Color>
    {
        { "Grass", new Color(0.62f, 0.86f, 0.55f) }, { "GrassDark", new Color(0.47f, 0.74f, 0.47f) }, { "Stem", new Color(0.5f, 0.75f, 0.45f) },
        { "PetalWhite", new Color(0.98f, 0.97f, 0.94f) }, { "PetalYellow", new Color(1f, 0.9f, 0.5f) }, { "PetalPink", new Color(1f, 0.74f, 0.84f) },
        { "FlowerCenter", new Color(1f, 0.82f, 0.35f) }, { "Pebble", new Color(0.78f, 0.76f, 0.8f) }, { "Moss", new Color(0.56f, 0.78f, 0.5f) },
        { "Vine", new Color(0.55f, 0.78f, 0.5f) }, { "Leaf", new Color(0.62f, 0.88f, 0.55f) }, { "MossDeep", new Color(0.36f, 0.54f, 0.4f) },
        { "Stone", new Color(0.68f, 0.62f, 0.71f) }, { "StoneDark", new Color(0.55f, 0.5f, 0.6f) },
    };

    static void EnsureDir(string path)
    {
        var parts = path.Split('/'); string cur = parts[0];
        for (int i = 1; i < parts.Length; i++) { string next = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]); cur = next; }
    }

    static Material Mat(string name)
    {
        string path = MatDir + "/Deco_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/Deco")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", MatColors.ContainsKey(name) ? MatColors[name] : Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    // JSON（{'mats': [...], 'props': {名前: {'v','t','s'}}}）を、メッシュ・材質・プレハブにする
    public static string Import(string jsonPath)
    {
        EnsureDir(MeshDir); EnsureDir(MatDir); EnsureDir(PrefabDir);
        var json = System.IO.File.ReadAllText(jsonPath);
        // 材質名
        int mi = json.IndexOf("\"mats\""); int ma = json.IndexOf('[', mi), mb = json.IndexOf(']', ma);
        var mats = new List<string>();
        foreach (var s in json.Substring(ma + 1, mb - ma - 1).Split(',')) mats.Add(s.Trim().Trim('"'));
        string log = "";
        int pi = json.IndexOf("\"props\"");
        int cur = json.IndexOf('{', pi) + 1;
        while (true)
        {
            int q0 = json.IndexOf('"', cur); if (q0 < 0) break;
            int q1 = json.IndexOf('"', q0 + 1);
            string name = json.Substring(q0 + 1, q1 - q0 - 1);
            int vi = json.IndexOf("\"v\"", q1), ti = json.IndexOf("\"t\"", vi), si = json.IndexOf("\"s\"", ti), end = json.IndexOf('}', si);
            var vs = Nums(json.Substring(vi + 3, ti - vi - 3)); var ts = Nums(json.Substring(ti + 3, si - ti - 3)); var ss = Nums(json.Substring(si + 3, end - si - 3));
            // 材質ごとのサブメッシュ（三角形ごとに頂点を分けてフラットに）
            var used = new List<int>(); foreach (var x in ss) if (!used.Contains((int)x)) used.Add((int)x);
            used.Sort();
            var V = new List<Vector3>(); var subs = new List<List<int>>(); foreach (var u in used) subs.Add(new List<int>());
            bool smooth = name == "Pebbles";   // 小石と苔はなめらかに（頂点を共有して法線をならす）
            if (smooth)
            {
                for (int i = 0; i + 2 < vs.Count; i += 3) V.Add(new Vector3(vs[i], vs[i + 1], vs[i + 2]));
                for (int t = 0; t < ss.Count; t++) { int sub = used.IndexOf((int)ss[t]); for (int k = 0; k < 3; k++) subs[sub].Add((int)ts[t * 3 + k]); }
            }
            else
            for (int t = 0; t < ss.Count; t++)
            {
                int sub = used.IndexOf((int)ss[t]);
                for (int k = 0; k < 3; k++) { int idx = (int)ts[t * 3 + k]; V.Add(new Vector3(vs[idx * 3], vs[idx * 3 + 1], vs[idx * 3 + 2])); subs[sub].Add(V.Count - 1); }
            }
            var mesh = new Mesh { name = "Deco_" + name };
            mesh.SetVertices(V); mesh.subMeshCount = used.Count;
            for (int k = 0; k < used.Count; k++) mesh.SetTriangles(subs[k], k);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string mp = MeshDir + "/Deco_" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
            if (old != null) { FuwaBevelTools.CopyInto(old, mesh); EditorUtility.SetDirty(old); mesh = old; } else AssetDatabase.CreateAsset(mesh, mp);
            var go = new GameObject("Deco_" + name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            var mm = new Material[used.Count]; for (int k = 0; k < used.Count; k++) mm[k] = Mat(mats[used[k]]);
            mr.sharedMaterials = mm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Deco_" + name + ".prefab");
            Object.DestroyImmediate(go);
            log += name + "(" + ss.Count + ") ";
            cur = end + 1;
            // 次のプロップの手前の '}' を越える
            int nextQuote = json.IndexOf('"', cur);
            int closeProps = json.IndexOf('}', cur);
            if (nextQuote < 0 || (closeProps >= 0 && closeProps < nextQuote)) break;
        }
        AssetDatabase.SaveAssets();
        return log;
    }

    static List<float> Nums(string s)
    {
        var l = new List<float>(); var cur = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsDigit(ch) || ch == '-' || ch == '.' || ch == 'e' || ch == 'E' || ch == '+') cur.Append(ch);
            else if (cur.Length > 0) { l.Add(float.Parse(cur.ToString(), System.Globalization.CultureInfo.InvariantCulture)); cur.Clear(); }
        }
        if (cur.Length > 0) l.Add(float.Parse(cur.ToString(), System.Globalization.CultureInfo.InvariantCulture));
        return l;
    }

    static GameObject Prefab(string name) { return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Deco_" + name + ".prefab"); }

    // コースの道のふちに飾りを置く（root の下の Deco を作り直す）。
    // spacingMin〜Max(m) おきに 2〜4個のかたまり。スタート・ゴール・チェックポイントのまわりは空ける
    // style: "meadow"＝草・花・小石・ツタ（今まで）、"forest"＝草と苔（花なし）、"rock"＝小石だけ
    public static string Decorate(Transform root, int seed = 3, float spacingMin = 2f, float spacingMax = 4.5f, float sizeMul = 1.6f, string style = "meadow")
    {
        var old = root.Find("Deco"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("Deco").transform; holder.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "deco");
        var col = root.Find("PathMesh").GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        var b = col.bounds;
        // 1. 上から格子状に調べて、道の上面のふち（となりが道でない所）を集める
        const float step = 0.25f;
        var hits = new Dictionary<Vector2Int, Vector3>();
        int nx = Mathf.CeilToInt(b.size.x / step) + 2, nz = Mathf.CeilToInt(b.size.z / step) + 2;
        for (int i = -1; i < nx; i++) for (int k = -1; k < nz; k++)
        {
            var o = new Vector3(b.min.x + i * step, b.max.y + 2f, b.min.z + k * step);
            if (col.Raycast(new Ray(o, Vector3.down), out var h, b.size.y + 4f) && h.normal.y > 0.7f) hits[new Vector2Int(i, k)] = h.point;
        }
        var edges = new List<(Vector3 p, Vector3 outDir)>();
        var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };
        foreach (var kv in hits)
        {
            Vector3 outDir = Vector3.zero; int miss = 0;
            foreach (var d in dirs) if (!hits.ContainsKey(kv.Key + d)) { outDir += new Vector3(d.x, 0, d.y); miss++; }
            if (miss == 0 || outDir.sqrMagnitude < 0.01f) continue;
            // 格子の点はふちから最大25cm内側なので、外へ少しずつ進めて本当のふち（上面が終わる所）を探す
            var od = outDir.normalized; var e = kv.Value;
            for (int st = 0; st < 20; st++)
            {
                var nxt = e + od * 0.02f;
                if (col.Raycast(new Ray(nxt + Vector3.up * 1f, Vector3.down), out var hh2, 1.3f) && hh2.normal.y > 0.7f) e = hh2.point; else break;
            }
            edges.Add((e, od));
        }
        // 2. 空けておく場所（スタート・ゴール・チェックポイント・ワープ）
        var avoid = new List<Vector3>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "BallStart" || t.name == "GoalTrigger" || t.name == "WarpToLobby_Trigger" || t.name == "ClusterL" || t.name == "ClusterR" || t.name == "VentHole" || (t.parent != null && t.parent.name.StartsWith("Checkpoint") && t.name == "Trigger")) avoid.Add(t.position);
        var rnd = new System.Random(seed);
        float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
        // 3. ふちの点をランダムな順で見て、間をあけてかたまりを置く
        var order = new List<int>(); for (int i = 0; i < edges.Count; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = order[i]; order[i] = order[j]; order[j] = t; }
        var centers = new List<Vector3>(); int clusters = 0, items = 0;
        var occupied = new List<(Vector3 p, float r)>();   // 置いた草・花・小石の足元（重ならないように）
        foreach (var oi in order)
        {
            var (p, outDir) = edges[oi];
            float need = R(spacingMin, spacingMax);
            bool ok = true;
            foreach (var c in centers) if ((c - p).sqrMagnitude < need * need) { ok = false; break; }
            foreach (var a in avoid) if ((a - p).sqrMagnitude < 3.2f * 3.2f) { ok = false; break; }
            if (!ok) continue;
            centers.Add(p); clusters++;
            var g = new GameObject("Cluster" + clusters).transform; g.SetParent(holder, true); g.position = p;
            var along = Vector3.Cross(Vector3.up, outDir).normalized;
            int n = rnd.Next(3, 6);
            bool vineDone = false;
            for (int k = 0; k < n; k++)
            {
                float pick = (float)rnd.NextDouble();
                string name;
                if (style == "forest")
                {
                    if (pick < 0.3f) name = "GrassSmall";
                    else if (pick < 0.5f) name = "GrassLarge";
                    else if (pick < 0.72f) name = "MossSmall";
                    else if (pick < 0.88f) name = "MossLarge";
                    else name = vineDone ? "MossSmall" : "Vines";
                }
                else if (style == "rock")
                {
                    if (pick < 0.5f) name = "StonesSmall";
                    else if (pick < 0.85f) name = "StonesMid";
                    else name = "StoneLarge";
                }
                else
                {
                    if (pick < 0.32f) name = "GrassSmall";
                    else if (pick < 0.55f) name = "GrassLarge";
                    else if (pick < 0.75f) name = new[] { "FlowersWhite", "FlowersYellow", "FlowersPink" }[rnd.Next(3)];
                    else if (pick < 0.87f) name = "Pebbles";
                    else name = vineDone ? "GrassSmall" : "Vines";
                }
                var pf = Prefab(name); if (pf == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, g);
                // 森（やみのもり）の苔は、暗い道になじむ深い緑に
                if (style == "forest" && name.StartsWith("Moss")) foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>()) { var mm = mr.sharedMaterials; for (int q2 = 0; q2 < mm.Length; q2++) mm[q2] = Mat("MossDeep"); mr.sharedMaterials = mm; }
                if (name == "Vines")
                {
                    vineDone = true;
                    // ふちの角から外へ垂らす（上面からほんの少し下げて、ふちの丸めに隠す）
                    var vp = p + along * R(-0.3f, 0.3f);
                    if (col.Raycast(new Ray(vp + Vector3.up * 1f, Vector3.down), out var vh, 2f)) vp = vh.point;
                    inst.transform.position = vp - outDir * 0.05f + Vector3.up * 0.005f;
                    inst.transform.rotation = Quaternion.LookRotation(outDir, Vector3.up);
                    inst.transform.localScale = Vector3.one * R(0.85f, 1.2f) * sizeMul;
                }
                else
                {
                    // ふちの内側ギリギリ（道の上0〜20cm内側）。少し沈めて浮いて見えないように
                    float sc = R(0.8f, 1.25f) * sizeMul;
                    // 足元の広がり（この半径の中が全部道の上になるようにする＝空中に生えない）
                    float foot = (name == "GrassSmall" ? 0.08f : name == "GrassLarge" ? 0.12f : name == "Pebbles" ? 0.26f
                        : name == "MossSmall" ? 0.16f : name == "MossLarge" ? 0.36f : name == "StonesSmall" ? 0.2f : name == "StonesMid" ? 0.32f : name == "StoneLarge" ? 0.26f : 0.1f) * sc;
                    // となりと重ならない半径（葉や花の広がりまで含めた実寸から）
                    float clear = (name == "GrassSmall" ? 0.11f : name == "GrassLarge" ? 0.26f : name == "Pebbles" ? 0.25f
                        : name == "MossSmall" ? 0.16f : name == "MossLarge" ? 0.36f : name == "StonesSmall" ? 0.22f : name == "StonesMid" ? 0.34f : name == "StoneLarge" ? 0.28f : 0.16f) * sc;
                    // 苔と石は内側への入り方もばらつかせて、ふちに沿って一列に並ばないように
                    float inward = (name.StartsWith("Moss") || name.StartsWith("Stone")) ? R(0.02f, 0.55f) : R(0.02f, 0.12f);
                    var q = p + along * R(-0.6f, 0.6f) * sizeMul - outDir * (foot + inward);
                    bool placed = false;
                    for (int tries = 0; tries < 12 && !placed; tries++)
                    {
                        bool allOn = true; float topY = -1e9f, lowY = 1e9f;
                        for (int a = 0; a <= 8 && allOn; a++)
                        {
                            var o = a == 8 ? q : q + Quaternion.Euler(0, a * 45f, 0) * Vector3.forward * (foot + 0.09f);   // 見た目のふちの丸め(8cm)の分も余裕をみる
                            if (col.Raycast(new Ray(o + Vector3.up * 1f, Vector3.down), out var hf, 2f) && hf.normal.y > 0.7f) { topY = Mathf.Max(topY, hf.point.y); lowY = Mathf.Min(lowY, hf.point.y); }
                            else allOn = false;
                        }
                        bool free = true;
                        foreach (var oc in occupied) if (new Vector2(oc.p.x - q.x, oc.p.z - q.z).magnitude < oc.r + clear + 0.02f) { free = false; break; }
                        if (allOn && topY - lowY < 0.04f && free) { q.y = lowY; placed = true; }
                        else if (!free) q += along * (R(0, 1) < 0.5f ? -1f : 1f) * (clear + 0.1f);   // となりと重なる：ふちに沿ってずらす
                        else q -= outDir * 0.05f;   // 内側へずらしてやり直す
                    }
                    if (!placed) { Object.DestroyImmediate(inst); continue; }
                    occupied.Add((q, clear));
                    inst.transform.position = q + Vector3.down * 0.01f;
                    inst.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);
                    inst.transform.localScale = Vector3.one * sc;
                }
                items++;
            }
        }
        int removed = RemoveOverlaps(holder, style == "rock");
        // 苔・石は間引いてまばらに（近くに同じ種類があれば消す＋ランダムに少し減らす）
        removed += Sparsify(holder, new[] { "Deco_Moss", "Deco_Stone" }, 2.6f, 0.75f, seed + 101);
        FitVinesToWall(holder, root);
        EditorUtility.SetDirty(root.gameObject);
        return "edges " + edges.Count + " clusters " + clusters + " items " + items + " removed(overlap) " + removed;
    }

    [MenuItem("FuwaCourse/コースのツルを壁にそわせる")]
    static void MenuFitAllVines()
    {
        int n = 0, total = 0;
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Deco" || t.name == "FinalDeco") { var (a, b) = FitVinesToWallCount(t, go.transform); n += a; total += b; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"ツルを壁にそわせた：{n}/{total}");
    }

    public static int FitVinesToWall(Transform holder, Transform root) { return FitVinesToWallCount(holder, root).Item1; }

    // 垂らしたツタを、道・島の側面にそわせる（2026-10-15 てじゃ「コースの飾りのツル、結構浮いてること多い」）。
    // 前は「下の方が壁にめり込んでいたら丸ごと外へずらす」だけだったので、下に向かって広がる壁だと根元ごと外へ押し出されて浮き、
    // まっすぐな壁でもツタのカーテン（根元から外へ約0.1×大きさ）が壁から離れて見えていた。
    // 今は：ツタの真横から内向きに線を飛ばして、高さごとの壁の位置を測る → 壁の傾きに合わせてツタを外へ傾け（0〜40°）、
    // カーテンの内側が一番出っぱった所の壁に1cmで触れる位置まで寄せる。根元（上の端）はふちの上のまま
    static (int, int) FitVinesToWallCount(Transform holder, Transform root)
    {
        Physics.SyncTransforms();
        int moved = 0, total = 0;
        foreach (var vf in holder.GetComponentsInChildren<MeshFilter>(true))
        {
            if (vf.sharedMesh == null || !vf.sharedMesh.name.StartsWith("Deco_Vines")) continue;
            total++;
            var inst = vf.transform; while (inst.parent != null && inst.parent != holder && !inst.parent.name.StartsWith("Cluster") && !inst.parent.name.StartsWith("Grass")) inst = inst.parent;
            var outDir = inst.forward; outDir.y = 0; if (outDir.sqrMagnitude < 1e-4f) continue; outDir.Normalize();
            var along = Vector3.Cross(Vector3.up, outDir);
            float sc = inst.lossyScale.x;
            Vector3 a = inst.position;
            // ツタがぶら下がっている物（根元の真下の道・島）。壁はこの物だけで測る（下や奥にある別の島をツタの壁と間違えないように）
            Collider host = null;
            for (float back = 0.15f; back <= 2.5f && host == null; back += 0.25f)   // 外へ押し出されて浮いているツタもあるので、内側へずらしながら探す
                foreach (var hit in Physics.RaycastAll(a + Vector3.up * 0.3f - outDir * back, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.transform.IsChildOf(root) && !hit.collider.transform.IsChildOf(holder) && hit.collider.GetComponentInParent<Rigidbody>() == null && !hit.collider.isTrigger) { host = hit.collider; break; }
            if (host == null) continue;
            // ツタの頂点（根元から見た形。上の端3cmは道の中に隠れてよいので除く）
            var lv = new List<Vector3>();
            var verts = vf.sharedMesh.vertices;
            for (int i = 0; i < verts.Length; i += 8)
            {
                var l = inst.InverseTransformPoint(vf.transform.TransformPoint(verts[i])) * sc;
                if (l.y < -0.03f) lv.Add(l);
            }
            // 置き方の候補：ふちに沿って少しずらす（-0.6〜0.6m）×向きを少し回す（-30〜30°）。
            // カーブだとまっすぐなツタの片側だけ壁に触れて反対側が浮くので、壁との平均のすき間が一番小さい置き方を選ぶ
            //（2026-10-15 てじゃ「カーブにあるやつ浮きガチ」）
            float bestScore = float.PositiveInfinity; Vector3 bestPos = inst.position; Quaternion bestRot = inst.rotation; bool any = false;
            for (int si = -3; si <= 3; si++)
                for (int yi = -3; yi <= 3; yi++)
                {
                    float slide = si * 0.2f;
                    var od = Quaternion.Euler(0f, yi * 10f, 0f) * outDir;
                    var bp = a + along * slide;
                    // 根元の高さ：道の上面から2cm下（ふちより内側に入った根元の上の端が、道の上にちょこんと出ないように）
                    float topY = float.NaN;
                    for (float back = 0.1f; back <= 2.5f; back += 0.2f) if (host.Raycast(new Ray(new Vector3(bp.x, a.y + 0.5f, bp.z) - od * back, Vector3.down), out var th, 1f)) { topY = th.point.y; break; }
                    if (float.IsNaN(topY) || Mathf.Abs(topY - a.y) > 0.3f) continue;   // 道の上でない・段差の所はやめる
                    var a0 = new Vector3(bp.x, topY - 0.02f, bp.z);
                    // ふち（上面のすぐ下の側面）の位置：根元から外向きの距離
                    if (!host.Raycast(new Ray(a0 + Vector3.down * 0.02f + od * 2f, -od), out var eh, 5f)) continue;
                    float edge = Vector3.Dot(eh.point - a0, od);
                    // 傾き 0〜40°：めり込まずにカーテンが壁に1cmで触れる寄せ量。根元がふちより外へ出ない一番小さい傾き
                    for (int deg = 0; deg <= 40; deg += 8)
                    {
                        var rot = Quaternion.LookRotation(od, Vector3.up) * Quaternion.Euler(-deg, 0f, 0f);
                        float c = float.NegativeInfinity;
                        var gw = new List<(float vz, float w)>();
                        foreach (var l in lv)
                        {
                            var p = a0 + rot * l;
                            if (!host.Raycast(new Ray(p + od * 1.5f, -od), out var wh, 3f)) continue;   // その高さ・横に壁がない（薄い道の下など）
                            float w = Vector3.Dot(wh.point - a0, od), vz = Vector3.Dot(p - a0, od);
                            gw.Add((vz, w));
                            c = Mathf.Max(c, w + 0.01f - vz);
                        }
                        if (gw.Count == 0) c = edge - 0.05f;   // どこにも壁がない：根元をふちの少し内側に
                        if (c > edge + 0.02f) continue;          // 根元がふちより外へ出る＝浮く。もっと傾ける
                        float mean = 0f; foreach (var (vz, w) in gw) mean += vz + c - w; mean = gw.Count > 0 ? mean / gw.Count : 0.2f;
                        float score = mean + Mathf.Abs(slide) * 0.03f + Mathf.Abs(yi) * 0.004f + deg * 0.0005f;   // 同じくらいなら元の場所・向きに近い方
                        if (score < bestScore) { bestScore = score; bestPos = a0 + od * c; bestRot = rot; any = true; }
                        break;
                    }
                }
            if (!any) { Object.DestroyImmediate(inst.gameObject); moved++; continue; }   // どこに置いてもそわせられない：消す
            if ((bestPos - inst.position).sqrMagnitude > 1e-6f || Quaternion.Angle(bestRot, inst.rotation) > 0.1f) { inst.SetPositionAndRotation(bestPos, bestRot); moved++; }
        }
        return (moved, total);
    }

    // 垂らしたツタが道・島の側面にめり込んでいたら、めり込まない所まで外へ出す（古いやり方。今は FitVinesToWall）
    // （外から内向きに線を飛ばして、頂点より手前で地面に当たる＝めり込み）
    public static int PushVinesOut(Transform holder, Transform root)
    {
        int moved = 0;
        foreach (var vf in holder.GetComponentsInChildren<MeshFilter>(true))
        {
            if (vf.sharedMesh == null || !vf.sharedMesh.name.StartsWith("Deco_Vines")) continue;
            var inst = vf.transform; while (inst.parent != null && inst.parent != holder && !inst.parent.name.StartsWith("Cluster")) inst = inst.parent;
            var outDir = inst.forward; outDir.y = 0; if (outDir.sqrMagnitude < 1e-4f) continue; outDir.Normalize();
            var vs = vf.sharedMesh.vertices; float pen = 0;
            for (int i = 0; i < vs.Length; i += 2)
            {
                var w = vf.transform.TransformPoint(vs[i]);
                foreach (var h in Physics.RaycastAll(w + outDir * 3f, -outDir, 3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!h.collider.transform.IsChildOf(root) || h.collider.GetComponentInParent<Rigidbody>() != null) continue;
                    pen = Mathf.Max(pen, 3f - h.distance);
                }
            }
            if (pen > 0.003f) { inst.position += outDir * (pen + 0.02f); moved++; }
        }
        return moved;
    }

    public static int Sparsify(Transform holder, string[] prefixes, float minDist, float keepProb, int seed)
    {
        var rnd = new System.Random(seed);
        var items = new List<Transform>();
        foreach (Transform c in holder) foreach (Transform it in c) foreach (var pf in prefixes) if (it.name.StartsWith(pf)) { items.Add(it); break; }
        for (int i = items.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = items[i]; items[i] = items[j]; items[j] = t; }
        var kept = new List<Vector3>(); int removed = 0;
        foreach (var it in items)
        {
            var p = it.position; bool near = false;
            foreach (var k in kept) if (new Vector2(k.x - p.x, k.z - p.z).magnitude < minDist) { near = true; break; }
            if (near || rnd.NextDouble() > keepProb) { Object.DestroyImmediate(it.gameObject); removed++; }
            else kept.Add(p);
        }
        return removed;
    }

    // 置いた飾りのうち、ギミック・木・巣・キノコ・橋・スタート/ゴールなどの当たり判定にめり込むものを消す。
    // 大きい見えない判定（風・重力・落下判定など）と道そのものは無視。rockOk なら岩・山・トンネルに触れるのはOK（岩のきわの小石は自然）
    public static int RemoveOverlaps(Transform holder, bool rockOk)
    {
        Physics.SyncTransforms();
        int removed = 0;
        var items = new List<Transform>();
        foreach (Transform c in holder) foreach (Transform it in c) items.Add(it);
        foreach (var it in items)
        {
            var rs = it.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            bool hit = false;
            foreach (var c in Physics.OverlapBox(b.center, b.extents * 0.85f, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
            {
                if (c == null || c.transform.IsChildOf(holder)) continue;
                string n = c.name;
                if (n == "PathMesh" || n == "FallCatcher") continue;
                if (c.isTrigger && c.bounds.size.magnitude > 6f) continue;   // 風・重力・すき間判定など大きい見えない判定
                if (n.Contains("Zone") || n.Contains("Wind") || n.Contains("Gust") || n.Contains("Whirl") || n.Contains("Updraft") || n.Contains("Heavy")) continue;
                if (rockOk && (n == "Mountain" || n == "Tunnel" || n == "CaveMouth" || n == "Rubble" || n == "ClimbSteps" || n == "Pillars" || n == "VentRims")) continue;
                hit = true; break;
            }
            if (hit) { Object.DestroyImmediate(it.gameObject); removed++; }
        }
        return removed;
    }
}
