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
        { "Vine", new Color(0.55f, 0.78f, 0.5f) }, { "Leaf", new Color(0.62f, 0.88f, 0.55f) },
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
    public static string Decorate(Transform root, int seed = 3, float spacingMin = 2f, float spacingMax = 4.5f, float sizeMul = 1.6f)
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
                if (pick < 0.32f) name = "GrassSmall";
                else if (pick < 0.55f) name = "GrassLarge";
                else if (pick < 0.75f) name = new[] { "FlowersWhite", "FlowersYellow", "FlowersPink" }[rnd.Next(3)];
                else if (pick < 0.87f) name = "Pebbles";
                else name = vineDone ? "GrassSmall" : "Vines";
                var pf = Prefab(name); if (pf == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, g);
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
                    float foot = (name == "GrassSmall" ? 0.08f : name == "GrassLarge" ? 0.12f : name == "Pebbles" ? 0.26f : 0.1f) * sc;
                    // となりと重ならない半径（葉や花の広がりまで含めた実寸から）
                    float clear = (name == "GrassSmall" ? 0.11f : name == "GrassLarge" ? 0.26f : name == "Pebbles" ? 0.25f : 0.16f) * sc;
                    var q = p + along * R(-0.6f, 0.6f) * sizeMul - outDir * (foot + R(0.02f, 0.12f));
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
        EditorUtility.SetDirty(root.gameObject);
        return "edges " + edges.Count + " clusters " + clusters + " items " + items;
    }
}
