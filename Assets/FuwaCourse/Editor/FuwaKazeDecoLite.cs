using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// かぜのおかの大きいたんぽぽの「軽い版」（遠く用・配布用の板）。2026-10-16 てじゃ「配布用とかでガッツリポリ落とす」「このワールドの軽量化にも」。
// ・今のきれいなたんぽぽ（黄色い花・綿毛の頭・葉1枚）を撮って、透過つきの絵（アトラス）に焼く（黒背景と白背景で2回撮って透けぐあいを出す）
// ・ふつうの版と同じ乱数で同じ場所・同じ向きに、板で作る（Lite のときの Giant / Rosette / 頭の作り分けは FuwaKazeDeco.cs 側）
//   茎＝4角、葉＝板（3区間）、花＝円すいの板＋小さながく、綿毛の頭＝十字の板3枚
public static partial class FuwaKazeDeco
{
    static bool Lite;
    const string AtlasPath = "Assets/FuwaCourse/Textures/DandelionLiteAtlas.png";
    const int AtlasSize = 1024;
    // アトラスの区画（uv）
    static readonly Rect UvFlower = new Rect(0f, 0.5f, 0.5f, 0.5f);       // 花を真上から
    static readonly Rect UvPuff = new Rect(0.5f, 0.5f, 0.5f, 0.5f);       // 綿毛の頭を横から
    static readonly Rect UvLeaf = new Rect(0f, 64f / 1024f, 1f, 384f / 1024f);   // 葉を真上から（長さ方向が横）
    static readonly Rect UvSolid = new Rect(0f, 0f, 64f / 1024f, 64f / 1024f);   // 白一色（がく・茎の色は頂点カラー）
    const float FlowerFrame = 1.32f;   // 花の絵の半径（花の半径 r に対する比）
    const float PuffFrame = 1.08f;     // 綿毛の絵の半分の大きさ（頭の外側の半径に対する比）
    const float LeafBakeW = 0.18f, LeafFrameX0 = -0.02f, LeafFrameX1 = 1.02f;   // 葉は長さ1・幅 LeafBakeW で撮る
    static float LeafFrameHalfH => (LeafFrameX1 - LeafFrameX0) * 384f / 1024f * 0.5f;

    [MenuItem("FuwaCourse/済み（押す前に相談）/かぜのおか/たんぽぽの軽い版を作る（絵を撮る＋板）")]
    public static void MenuLite() { Debug.Log(BuildLite()); }

    public static string BuildLite()
    {
        var log = "hi: " + Run().Split('\n').Last();
        log += " | atlas: " + BakeAtlas();
        Lite = true;
        try { log += " | lite: " + Run(); }
        finally { Lite = false; }
        log += " | lod: " + SplitLod();
        return log;
    }

    // ---- 浮島ごとに分けて、近い＝ふつうの版／遠い＝板の版 を LODGroup で切りかえる ----
    // まとめメッシュ（Dandelions / PuffTufts / DandelionsLite）は作るためだけに残して非表示。各島に DandelionLOD（Plants・Tufts・Lite）を置く
    const float LiteSwitchDistance = 30f;   // 2026-10-16 てじゃ「きりかわるの近い」で 20→30   // このくらい（m）から板の版（縦の画角60度で計算。VRは広いのでもう少し近くで切りかわる）
    static string SplitLod()
    {
        var root = GameObject.Find(Course).transform;
        var isl = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "DandelionIslands");
        var src = new[] { ("Dandelions", "Plants"), ("PuffTufts", "Tufts"), ("DandelionsLite", "Lite") }
            .Select(x => (go: isl.Find(x.Item1), part: x.Item2)).Where(x => x.go != null).ToList();
        var made = new List<Renderer>();
        int islands = 0; long hiV = 0, liteV = 0;
        foreach (Transform it in isl)
        {
            if (!it.name.StartsWith("Island")) continue;
            islands++;
            var holder = it.Find("DandelionLOD");
            if (holder == null) { holder = new GameObject("DandelionLOD").transform; holder.SetParent(it, false); Undo.RegisterCreatedObjectUndo(holder.gameObject, "lod"); }
            holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity; holder.localScale = Vector3.one;
            var hiR = new List<Renderer>(); var liteR = new List<Renderer>();
            foreach (var (go, part) in src)
            {
                var mf = go.GetComponent<MeshFilter>(); var srcMr = go.GetComponent<MeshRenderer>();
                var mesh = ExtractIsland(mf.sharedMesh, mf.transform, holder, it.position, 4.3f, Dir + "/" + it.name + "_" + part + ".asset", part == "Tufts" ? 1f : part == "Lite" ? 5f : 0f);
                var t = holder.Find(part);
                if (t == null) { t = new GameObject(part, typeof(MeshFilter), typeof(MeshRenderer)).transform; t.SetParent(holder, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "lod"); }
                t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
                t.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = t.GetComponent<MeshRenderer>(); mr.sharedMaterial = srcMr.sharedMaterial;
                mr.shadowCastingMode = srcMr.shadowCastingMode; mr.receiveShadows = srcMr.receiveShadows;
                mr.lightProbeUsage = srcMr.lightProbeUsage; mr.reflectionProbeUsage = srcMr.reflectionProbeUsage;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);   // 風でゆれる・冠毛は物の座標頼み（静的バッチ不可）
                (part == "Lite" ? liteR : hiR).Add(mr);
                if (part == "Lite") liteV += mesh.vertexCount; else hiV += mesh.vertexCount;
            }
            var g = holder.GetComponent<LODGroup>(); if (g == null) g = Undo.AddComponent<LODGroup>(holder.gameObject);
            g.fadeMode = LODFadeMode.None;
            // 冠毛（Tufts）は遠くでも今の版のまま（板にすると色味が出せなかった）。軽くするのは草花（Plants）だけ
            var tuftR = hiR.Where(r => r.name == "Tufts").ToList(); var farR = liteR.Concat(tuftR).ToArray();
            g.SetLODs(new[] { new LOD(0.5f, hiR.ToArray()), new LOD(0.005f, farR) });
            // SetLODs で size が描く物全部の大きさに戻るので、そのあとの size で計算する。品質設定の LOD Bias（今は2）でも距離が倍になるので割る
            float h0 = Mathf.Clamp(g.size / (2f * LiteSwitchDistance * Mathf.Tan(30f * Mathf.Deg2Rad) * Mathf.Max(0.1f, QualitySettings.lodBias)), 0.005f, 0.95f);
            g.SetLODs(new[] { new LOD(h0, hiR.ToArray()), new LOD(Mathf.Min(0.004f, h0 * 0.5f), farR) });
            made.AddRange(hiR); made.AddRange(liteR);
        }
        // まとめメッシュは作るためだけ（表示しない）
        var oldR = src.Select(x => x.go.GetComponent<Renderer>()).ToList();
        foreach (var (go, _) in src) go.gameObject.SetActive(false);
        // 影の切りかえ（FarShadows）：まとめメッシュの代わりに島ごとの物を入れる
        var casters = made.Where(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off).ToList();
        foreach (var f in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            if (f.targets == null) continue;
            if (!f.targets.Any(r => oldR.Contains(r) || made.Contains(r))) continue;
            Undo.RecordObject(f, "FarShadows");
            var list = f.targets.Where(r => r != null && !oldR.Contains(r) && !made.Contains(r)).ToList(); list.AddRange(casters);
            f.targets = list.ToArray(); f.centers = f.targets.Select(r => r.bounds.center).ToArray(); f.radii = f.targets.Select(r => r.bounds.extents.magnitude).ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); EditorUtility.SetDirty(f);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "islands " + islands + " hi verts " + hiV + " lite verts " + liteV;
    }

    // まとめメッシュから、島の中心から半径 rad 以内の三角形だけを取り出して、holder のローカル座標のメッシュにする
    static Mesh ExtractIsland(Mesh src, Transform srcT, Transform holder, Vector3 c, float rad, string path, float expand)
    {
        var V = src.vertices; var N = src.normals; var C = src.colors; var T = src.triangles;
        var uv0 = new List<Vector4>(); src.GetUVs(0, uv0); var uv1 = new List<Vector4>(); src.GetUVs(1, uv1); var uv3 = new List<Vector4>(); src.GetUVs(3, uv3);
        var W = V.Select(v => srcT.TransformPoint(v)).ToArray();
        var map = new Dictionary<int, int>(); var tris = new List<int>();
        for (int t = 0; t < T.Length; t += 3)
        {
            var w = W[T[t]]; if (new Vector2(w.x - c.x, w.z - c.z).magnitude > rad) continue;
            for (int q = 0; q < 3; q++) { int o = T[t + q]; if (!map.TryGetValue(o, out var ni)) { ni = map.Count; map[o] = ni; } tris.Add(ni); }
        }
        var order = map.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
        m.Clear(); m.name = System.IO.Path.GetFileNameWithoutExtension(path);
        m.indexFormat = order.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(order.Select(o => holder.InverseTransformPoint(W[o])).ToList());
        if (N.Length > 0) m.SetNormals(order.Select(o => holder.InverseTransformDirection(srcT.TransformDirection(N[o])).normalized).ToList());
        if (C.Length > 0) m.SetColors(order.Select(o => C[o]).ToList());
        if (uv0.Count > 0) m.SetUVs(0, order.Select(o => uv0[o]).ToList());
        if (uv1.Count > 0) m.SetUVs(1, order.Select(o => uv1[o]).ToList());
        if (uv3.Count > 0) m.SetUVs(3, order.Select(o => uv3[o]).ToList());
        m.SetTriangles(tris, 0); m.RecalculateBounds();
        if (expand > 0f) { var b = m.bounds; b.Expand(expand); m.bounds = b; }   // こっちを向く板は描く時に広がる＋ゆれ
        EditorUtility.SetDirty(m);
        return m;
    }

    static Vector2 Uv(Rect r, float x, float y) { return new Vector2(r.x + r.width * x, r.y + r.height * y); }

    // ---- 板の版 ----

    // 花：円すいの板（まん中が少し高い。花びらが内ほど立ち上がる形に合わせる）＋小さながく（8角の円すい）
    static void FlowerLite(MB m, Vector3 c, float r)
    {
        const int n = 16; float R0 = r * FlowerFrame;
        int ci = m.Add(c + Vector3.up * r * 0.16f, Vector3.up, Color.white, Uv(UvFlower, 0.5f, 0.5f));
        for (int i = 0; i <= n; i++)
        {
            float a = i * Mathf.PI * 2 / n; float x = Mathf.Cos(a), z = Mathf.Sin(a);
            m.Add(c + new Vector3(x * R0, -r * 0.06f, z * R0), Vector3.up, Color.white, Uv(UvFlower, 0.5f + x * 0.5f, 0.5f + z * 0.5f));
        }
        for (int i = 0; i < n; i++) m.Tri(ci, ci + 2 + i, ci + 1 + i);
        // がく
        var g = StemG * 0.85f; const int k = 8; int bc = m.Add(c - Vector3.up * r * 0.15f, Vector3.down, g, Uv(UvSolid, 0.5f, 0.5f)); int rb = m.V.Count;
        for (int i = 0; i < k; i++) { float a = i * Mathf.PI * 2 / k; var o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); m.Add(c + o * r * 0.17f - Vector3.up * r * 0.01f, o, g, Uv(UvSolid, 0.5f, 0.5f)); }
        for (int i = 0; i < k; i++) m.Tri(bc, rb + i, rb + (i + 1) % k);
    }

    // 葉：ふつうの版と同じ中心線（根元から立ち上がって先へ垂れる）の上に、葉の絵を貼った板（4区間）。
    // F はふつうの版と同じ回数・順番で引く（あいだの短い葉の大きさが同じになるように）
    static void LeafLite(MB m, Vector3 at, float a0, float L, float W, float tone, float ang, float lph, float leafSway, RandF F)
    {
        var d = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var sd = new Vector3(-d.z, 0, d.x);
        {
            var o = new Vector2(at.x - _islC.x, at.z - _islC.z); var dd = new Vector2(d.x, d.z);
            float b2 = Vector2.Dot(o, dd), cc = o.sqrMagnitude - _islR * _islR, disc = b2 * b2 - cc;
            if (disc > 0) L = Mathf.Min(L, Mathf.Max(0.15f, -b2 + Mathf.Sqrt(disc) + 0.5f));
        }
        for (int i = 0; i < 16; i++) F(0f, 1f);   // ふつうの版の歯の形のぶん
        float droopEnd = F(12f, 26f) * Mathf.Deg2Rad, curl = F(-0.18f, 0.18f);
        float hw = W / LeafBakeW * LeafFrameHalfH;
        var col = Color.white * tone; col.a = 1f;
        int bi = m.V.Count; const int seg = 4;
        Vector3 Center(float u, out Vector3 sdj, out Vector3 nrm)
        {
            // 中心線を細かく積分して u の位置を出す
            var p = at + Vector3.up * 0.02f; float pu = 0f; Vector3 tan = d, hd = d;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(u) * 20f));
            for (int s = 1; s <= steps; s++)
            {
                float uu = u * s / steps;
                float th = ang - (ang + droopEnd) * Mathf.Pow(Mathf.Max(0f, uu), 1.4f);
                hd = Quaternion.AngleAxis(curl * uu * Mathf.Rad2Deg, Vector3.up) * d;
                tan = (hd * Mathf.Cos(th) + Vector3.up * Mathf.Sin(th)).normalized;
                p += tan * L * (uu - pu); pu = uu;
            }
            sdj = -Vector3.Cross(Vector3.up, hd).normalized; if (Vector3.Dot(sdj, sd) < 0) sdj = -sdj;
            nrm = Vector3.Cross(sdj, tan).normalized; if (nrm.y < 0) nrm = -nrm;
            return p;
        }
        for (int j = 0; j <= seg; j++)
        {
            float x = j / (float)seg, u = Mathf.Lerp(LeafFrameX0, LeafFrameX1, x);
            var c = Center(u, out var sdj, out var nrm);
            float uc = Mathf.Max(0f, u);
            m.Add(c - sdj * hw, nrm, col, Uv(UvLeaf, x, 0f)); m.Add(c + sdj * hw, nrm, col, Uv(UvLeaf, x, 1f));
            for (int q = 0; q < 2; q++) m.U3[m.V.Count - 1 - q] = new Vector4(leafSway * uc * uc, lph, 0, 0);
        }
        for (int j = 0; j < seg; j++) { int a = bi + j * 2, b = bi + (j + 1) * 2; m.Quad(a, a + 1, b + 1, b); }
    }

    // 綿毛の頭：横から撮った絵を貼った「こっちを向く板」1枚（茎の向きを軸に回る。シェーダーで向ける）。
    // 前は十字の板3枚だったが、横から見ると板がななめに薄く見えた（2026-10-16 てじゃ「見え方へん」）
    static void PuffLite(MB m, Vector3 hp, float hr, float ph)
    {
        var up = _headTan.TryGetValue(hp, out var tg) ? tg.normalized : Vector3.up;
        float h = HeadOuter(hr) * PuffFrame;
        int b = m.V.Count;
        var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        foreach (var cu in corners) { int i = m.Add(hp, up, Color.white, Uv(UvPuff, cu.x * 0.5f + 0.5f, cu.y * 0.5f + 0.5f)); m.UV1[i] = cu * h; m.U3[i] = new Vector4(1f, ph, 0, 0); }
        m.Quad(b, b + 1, b + 2, b + 3);
    }

    // まとめた板のメッシュを書き出す（ふつうの版の Dandelions は触らない）
    static string WriteLite(MB plants, Transform deco, Transform isl, Vector3 wind, System.Text.StringBuilder log)
    {
        var w2l = deco.worldToLocalMatrix;
        var m = new Mesh { name = "Kaze_Dandelions_Lite", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(plants.V.Select(x => w2l.MultiplyPoint3x4(x)).ToList()); m.SetNormals(plants.N.Select(x => w2l.MultiplyVector(x).normalized).ToList());
        m.SetColors(plants.C); m.SetUVs(0, plants.UV); m.SetUVs(1, plants.UV1); m.SetUVs(3, plants.U3); m.SetTriangles(plants.T, 0); m.RecalculateBounds();
        { var bb = m.bounds; bb.Expand(5f); m.bounds = bb; }   // こっちを向く板は頂点が頭の中心にあるので、広がるぶん
        string ap = Dir + "/Kaze_Dandelions_Lite.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
        if (old != null) { old.Clear(); EditorUtility.CopySerialized(m, old); m = old; } else AssetDatabase.CreateAsset(m, ap);
        string mp = "Assets/FuwaCourse/Materials/Kaze_DandelionLite.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("FuwaCourse/DandelionLite")); AssetDatabase.CreateAsset(mat, mp); }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        var hiMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Kaze_DandelionSway.mat");
        if (hiMat != null) { mat.SetFloat("_Wrap", hiMat.GetFloat("_Wrap")); mat.SetFloat("_Ambient", hiMat.GetFloat("_Ambient")); }   // 明るさをふつうの版とそろえる
        mat.SetFloat("_SwayAmp", SwayAmp); mat.SetFloat("_SwaySpeed", SwaySpeed); mat.SetVector("_SwayDir", new Vector4(wind.x, 0, wind.z, 0)); mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        var go = Child(isl, "DandelionsLite");
        go.GetComponent<MeshFilter>().sharedMesh = m; var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.receiveShadows = false;
        AssetDatabase.SaveAssets();
        return "lite verts " + m.vertexCount + " tris " + plants.T.Count / 3 + " " + log;
    }

    // ---- 絵を撮る ----

    static string BakeAtlas()
    {
        // 花・葉は光なしの色で撮る（軽い版のシェーダーで、ふつうの版と同じ光を当て直す。光ありで撮ると二重に暗くなった）
        var still = new Material(Shader.Find("Hidden/FuwaBakeUnlitVC"));
        const int layer = 31;
        var origin = new Vector3(0f, -800f, 0f);
        var atlas = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, true);
        var clear = new Color[AtlasSize * AtlasSize]; for (int i = 0; i < clear.Length; i++) clear[i] = new Color(1, 1, 1, 0);
        atlas.SetPixels(clear);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) atlas.SetPixel(x, y, Color.white);

        // 花（真上から）
        {
            var mb = new MB(); Flower(mb, Vector3.zero, 1f, null);
            var go = TempMesh(mb, origin, still, layer);
            Blit(atlas, go, origin + Vector3.up * 5f, Quaternion.LookRotation(Vector3.down, Vector3.forward), FlowerFrame, FlowerFrame, layer, 0, 512, 512, 512);
            Object.DestroyImmediate(go);
        }
        // 葉（真上から。長さ方向＝+X）
        {
            var keepC = _islC; var keepR = _islR; _islC = new Vector3(9999, 0, 9999); _islR = 1f;
            var fr = new System.Random(77); RandF F = (a, b) => a + (float)fr.NextDouble() * (b - a);
            var mb = new MB(); Leaf(mb, Vector3.zero, 0f, 1f, LeafBakeW, 1f, 0.02f, 0f, 0f, F);
            _islC = keepC; _islR = keepR;
            var go = TempMesh(mb, origin, still, layer);
            float cx = (LeafFrameX0 + LeafFrameX1) * 0.5f, hx = (LeafFrameX1 - LeafFrameX0) * 0.5f;
            // カメラの右＝+X（葉の根元→先が絵の左→右）、上＝+Z（v=1）
            Blit(atlas, go, origin + new Vector3(cx, 5f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward), hx, LeafFrameHalfH, layer, 0, 64, 1024, 384);
            Object.DestroyImmediate(go);
        }
        // 綿毛の頭（今のかぜのおかの1つ目の頭を横から。茎の向きを上に）
        if (_lastHeads.Count > 0)
        {
            var (hp, hr) = _lastHeads[0];
            var up = _headTan.TryGetValue(hp, out var tg) ? tg.normalized : Vector3.up;
            var side = Vector3.Cross(up, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(up, Vector3.up).normalized : Vector3.right;
            var fwd = Vector3.Cross(side, up).normalized;
            float h = HeadOuter(hr) * PuffFrame;
            var isl = GameObject.Find(Course).transform.GetComponentsInChildren<Transform>(true).First(t => t.name == "DandelionIslands");
            // 冠毛（PuffTufts）は遠くでも今の版を使うので、絵には芯と軸だけ撮る（板にすると冠毛のふわっとした色が出せなかった。2026-10-16 てじゃ「綿毛の色味がむずい」）
            var objs = new[] { isl.Find("Dandelions") }.Where(t => t != null).Select(t => t.gameObject).ToList();
            var keepL = objs.Select(o => o.layer).ToList(); var keepA = objs.Select(o => o.activeSelf).ToList();
            foreach (var o in objs) { o.layer = layer; o.SetActive(true); }   // まとめメッシュは LOD に分けた後は非表示なので、撮る間だけ出す
            try { Blit(atlas, null, hp - fwd * (h * 3f), Quaternion.LookRotation(fwd, up), h, h, layer, 512, 512, 512, 512, h * 2f, h * 4f); }
            finally { for (int i = 0; i < objs.Count; i++) { objs[i].layer = keepL[i]; objs[i].SetActive(keepA[i]); } }
        }
        atlas.Apply();
        System.IO.File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas); Object.DestroyImmediate(still);
        AssetDatabase.ImportAsset(AtlasPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 1024; ti.sRGBTexture = true;
        ti.SaveAndReimport();
        return "ok";
    }

    static GameObject TempMesh(MB mb, Vector3 origin, Material mat, int layer)
    {
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(mb.V); mesh.SetNormals(mb.N); mesh.SetColors(mb.C); mesh.SetUVs(3, mb.U3); mesh.SetTriangles(mb.T, 0); mesh.RecalculateBounds();
        var go = new GameObject("TmpBake", typeof(MeshFilter), typeof(MeshRenderer)); go.layer = layer; go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = origin; go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // 正投影で黒背景・白背景の2回撮り、透けぐあい（a = 1 - (白 - 黒)）と色（黒 / a）を出してアトラスへ
    static void Blit(Texture2D atlas, GameObject _, Vector3 camPos, Quaternion camRot, float halfW, float halfH, int layer, int px, int py, int pw, int ph, float near = 0.1f, float far = 20f)
    {
        var cg = new GameObject("TmpBakeCam") { hideFlags = HideFlags.HideAndDontSave }; var cam = cg.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(camPos, camRot); cam.orthographic = true; cam.orthographicSize = halfH; cam.aspect = halfW / halfH;
        cam.nearClipPlane = near; cam.farClipPlane = far; cam.cullingMask = 1 << layer; cam.clearFlags = CameraClearFlags.SolidColor; cam.allowMSAA = false;
        var rt = new RenderTexture(pw * 2, ph * 2, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
        Color[] Shot(Color bg)
        {
            cam.backgroundColor = bg; cam.Render();
            RenderTexture.active = rt; var t = new Texture2D(pw * 2, ph * 2, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, pw * 2, ph * 2), 0, 0); t.Apply(); RenderTexture.active = null;
            var px2 = t.GetPixels(); Object.DestroyImmediate(t); return px2;
        }
        var blk = Shot(Color.black); var wht = Shot(Color.white);
        cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(cg);
        for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                // 2×2 の平均（なめらかに）
                Color cb = Color.clear, cw = Color.clear;
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) { int i = (y * 2 + dy) * pw * 2 + x * 2 + dx; cb += blk[i]; cw += wht[i]; }
                cb /= 4f; cw /= 4f;
                float a = Mathf.Clamp01(1f - ((cw.r - cb.r) + (cw.g - cb.g) + (cw.b - cb.b)) / 3f);
                var c = a > 0.004f ? new Color(Mathf.Clamp01(cb.r / a), Mathf.Clamp01(cb.g / a), Mathf.Clamp01(cb.b / a), a) : new Color(1, 1, 1, 0);
                atlas.SetPixel(px + x, py + y, c);
            }
    }
}
