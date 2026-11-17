using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 各コースの「床だけの軽い版」を作る（プレイ中に他のコースの代わりに見せる）。
// 大きい物（道・島・足場・岩・大木・巨大キノコ）だけを集めて間引き、色は頂点カラーにして1コース1メッシュにまとめる。
// 飾り・柵・ギミック・半透明・パーティクルは入れない。コースを直したらもう一度実行する（何度実行しても同じ結果）
public static class FuwaCourseLite
{
    static readonly string[] Courses = { "FuwaCourse", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5", "FuwaCourse0" };
    const string Dir = "Assets/FuwaCourse/Generated/Lite";
    const float MinSize = 2.5f;   // これより小さい物は入れない(m、いちばん長い辺)
    const float Cell = 0.35f;     // 間引きの細かさ(m)
    const float DecoCell = 0.7f;  // 飾り（まわりの浮島）の間引きの細かさ(m)
    const float NestCell = 0.6f;  // まもののすの浮いた巣は向き・色で分けずにまとめる（根っこは入れない）（2026-10-22 てじゃ「まものの巣ふてるなぁ、浮いてる巣をもっと簡略化」）

    [MenuItem("FuwaCourse/よく使う/コースの軽い版を作る")]
    public static void Run() { Run(Courses, "CourseLites", Object.FindObjectsOfType<FuwaCourseLod>(true).FirstOrDefault(l => l.stage <= 1)); }

    // 2026-11-15 てじゃ「ステージ2の各コースの軽量化バージョンつくろか」→ ステージ2用（CourseLites2 と、ステージ2用の FuwaCourseLod＝CourseLodStage2）
    static readonly string[] Courses2 = { "FuwaCourse2_1", "FuwaCourse2_2", "FuwaCourse2_3", "FuwaCourse2_4", "FuwaCourse2_5" };
    [MenuItem("FuwaCourse/ステージ2/ステージ2のコースの軽い版を作る")]
    public static void RunStage2()
    {
        var all = Resources.FindObjectsOfTypeAll<FuwaCourseLod>().Where(l => l && l.gameObject.scene.IsValid()).ToList();
        var main = all.FirstOrDefault(l => l.stage <= 1);
        var lod2 = all.FirstOrDefault(l => l.stage == 2);
        if (lod2 == null && main != null)
        {
            var go = new GameObject("CourseLodStage2"); go.transform.SetParent(main.transform.parent, false);
            lod2 = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaCourseLod>(go);
            lod2.ball = main.ball; lod2.areaVisibility = main.areaVisibility; lod2.soloArea = main.soloArea; lod2.stage = 2; lod2.follow = main;
            lod2.onlyMobile = main.onlyMobile; lod2.interval = main.interval; lod2.nearCourseDist = main.nearCourseDist; lod2.cellSize = main.cellSize; lod2.mode = main.mode;
            lod2.savePlayerData = false; lod2.lobbyNearRadius = 0f;
            lod2.lobbyStopBehaviours = new VRC.Udon.UdonBehaviour[0]; lod2.lobbyHideObjects = new GameObject[0];
        }
        Run(Courses2, "CourseLites2", lod2);
        // CourseLites2 はステージ2のエリアの時だけ（エリア表示の stage2Objects に入れる）
        var av = lod2 != null ? lod2.areaVisibility : null; var h = GameObject.Find("CourseLites2");
        if (av != null && h != null && (av.stage2Objects == null || !av.stage2Objects.Contains(h)))
        {
            av.stage2Objects = (av.stage2Objects ?? new GameObject[0]).Concat(new[] { h }).ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(av); EditorUtility.SetDirty(av);
        }
        if (lod2 != null) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(lod2); EditorUtility.SetDirty(lod2); }
        SetupJoinStage2(lod2);
    }

    // 2026-11-15 てじゃ「空中から入ってもコースに入った判定（BGMとか切替わる）やつも、ステージ2の各コースとロビーにいれよ」
    //  → ステージ1の CourseJoin を写した CourseJoinStage2（stage=2）。roots＝ステージ2の5コース＋ロビー（FuwaStage2）、warps＝ロビーの各コースへのワープ・ロビーへ戻るワープ
    static void SetupJoinStage2(FuwaCourseLod lod2)
    {
        var joins = Resources.FindObjectsOfTypeAll<FuwaCourseJoin>().Where(j => j && j.gameObject.scene.IsValid()).ToList();
        var main = joins.FirstOrDefault(j => j.stage <= 1); if (main == null || lod2 == null) return;
        var j2 = joins.FirstOrDefault(j => j.stage == 2);
        if (j2 == null)
        {
            var go = new GameObject("CourseJoinStage2"); go.transform.SetParent(main.transform.parent, false);
            j2 = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaCourseJoin>(go);
        }
        j2.ball = main.ball; j2.areaVisibility = main.areaVisibility; j2.soloArea = main.soloArea; j2.stage = 2;
        j2.dwell = main.dwell; j2.joinDist = main.joinDist; j2.interval = main.interval; j2.lod = lod2;
        var warps = Resources.FindObjectsOfTypeAll<FuwaCourseWarp>().Where(w => w && w.gameObject.scene.IsValid()).ToList();
        var lobbyRoot = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t && t.gameObject.scene.IsValid() && t.parent == null && t.name == "FuwaStage2");
        var roots = new List<Transform>(); var lites = new List<Transform>(); var ws = new List<FuwaCourseWarp>();
        for (int i = 0; i < lod2.fullRoots.Length; i++)
        {
            var r = lod2.fullRoots[i].transform; roots.Add(r);
            lites.Add(lod2.liteRoots != null && i < lod2.liteRoots.Length && lod2.liteRoots[i] != null ? lod2.liteRoots[i].transform : null);
            // ロビーにある、このコースのスタートへ行くワープ
            ws.Add(warps.FirstOrDefault(w => w.courseStart != null && w.courseStart.root == r && w.transform.root == lobbyRoot));
        }
        roots.Add(lobbyRoot); lites.Add(null);
        ws.Add(warps.FirstOrDefault(w => w.courseStart != null && lobbyRoot != null && w.courseStart.root == lobbyRoot && w.name.StartsWith("WarpToLobby")));
        j2.roots = roots.ToArray(); j2.liteRoots = lites.ToArray(); j2.warps = ws.ToArray();
        // ロビーの床の箱（大きい当たり）
        var mins = new List<Vector3>(); var maxs = new List<Vector3>();
        if (lobbyRoot != null)
            foreach (var c in lobbyRoot.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && (c.name == "LobbyFloor" || c.name == "Ground")) { mins.Add(c.bounds.min); maxs.Add(c.bounds.max); }
        j2.lobbyMin = mins.ToArray(); j2.lobbyMax = maxs.ToArray();
        j2.areaMin = new Vector3[0]; j2.areaMax = new Vector3[0]; j2.areaIds = new int[0]; j2.areaWarps = new FuwaCourseWarp[0];   // エリアの切り替えはステージ1の方だけ
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j2); EditorUtility.SetDirty(j2);
        Debug.Log("[ステージ2の入場判定] " + string.Join(", ", ws.Select((w, i) => roots[i].name + "→" + (w ? w.name : "なし"))) + " / ロビーの床 " + mins.Count);
    }

    public static void Run(string[] courses, string holderName, FuwaCourseLod lodTarget)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Lite");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var holder = roots.FirstOrDefault(g => g.name == holderName);
        if (holder == null) { holder = new GameObject(holderName); Undo.RegisterCreatedObjectUndo(holder, "lite"); }
        holder.transform.position = Vector3.zero; holder.transform.rotation = Quaternion.identity; holder.transform.localScale = Vector3.one;
        // 材質は軽い版専用（頂点カラー＋ToonLit と同じ光り方）。前は Den_Twig（InkCap）を借りていて影が黒っぽかった
        const string MatPath = Dir + "/CourseLite.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("FuwaCourse/LiteVC")) { name = "CourseLite", enableInstancing = true }; AssetDatabase.CreateAsset(mat, MatPath); }
        var log = new System.Text.StringBuilder();
        var starts = new List<Transform>(); var fulls = new List<GameObject>(); var lites = new List<GameObject>();
        foreach (var cn in courses)
        {
            var root = roots.FirstOrDefault(g => g.name == cn); if (root == null) continue;
            var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>(); var S = new List<float>(); int used = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                if (mr.name.EndsWith("_Far")) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                string path = mr.transform.name; for (var p = mr.transform.parent; p != null && p != root.transform; p = p.parent) path = p.name + "/" + path;
                // 飾りの中でも遠くから形がわかる物は入れる：まわりの浮島（やみのもりの木の下の小さい島も）・まもののすの巣（2026-10-22 てじゃ「浮島そのものが消えるのちょと気なる」「木だけういてる」）
                bool keep = mr.name == "IslandTop" || mr.name == "IslandUnder" || path.Contains("Deco/Mamono_") && !path.Contains("Eggs") && !path.Contains("Roots")   // たまご・根っこはいらない
                    || path.Contains("CloudBalloon/Balloon") && !mr.name.Contains("Shadow");   // 2–4 の気球（2026-11-17 てじゃ「2-4の軽量版、気球もきえてるのさみしいかも」）。動くけど遠くからはいつもの位置に見えればいい
                var b = mr.bounds; float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                bool deco = size < MinSize || path.Contains("Deco/") || path.Contains("Fence") || path.Contains("/Web") || mr.name.Contains("Web") || path.Contains("Wind") || path.Contains("Gimmick");   // 2026-11-15 "Web" は巣だけ（2–5 の中段の島 I2_WebWalk がまるごと消えていた）
                if (deco && !(keep && size >= 1f)) continue;
                float cell = !deco ? Cell : mr.name == "Mamono_Nests" ? NestCell : DecoCell;
                if (mr.sharedMaterials.Any(m => m != null && m.renderQueue >= 2450)) continue;
                var mesh = mf.sharedMesh;
                if (!mesh.isReadable)
                {   // 2026-11-15 FBX の読めないメッシュ（2–5 の石像など）は読めるように読み込み設定を変える
                    var ip = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
                    if (ip != null && !ip.isReadable) { ip.isReadable = true; ip.SaveAndReimport(); mesh = mf.sharedMesh; }
                    if (mesh == null || !mesh.isReadable) continue;
                }
                var mv = mesh.vertices; var mn = mesh.normals; var mc = mesh.colors; var muv = mesh.uv; var l2w = mr.transform.localToWorldMatrix;
                var n2w = l2w.inverse.transpose;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var m = s < mr.sharedMaterials.Length ? mr.sharedMaterials[s] : mr.sharedMaterials.LastOrDefault();
                    Color mcol = m != null && m.HasProperty("_Color") ? m.color : Color.white; mcol.a = 1;
                    bool useVc = mc.Length == mv.Length && m != null && m.shader.name.Contains("InkCap");
                    // 島の下（FarIsland）も頂点カラー。こっちは色をそのまま（リニアで）使うシェーダーなので、軽い版の書き方（ガンマ）に直す
                    bool useVcLinear = mc.Length == mv.Length && m != null && m.shader.name.Contains("FarIsland");
                    // 色がテクスチャに入っている物（大きいきのこのかさのグラデ）は、頂点の UV の所の色をかける（2026-10-22 軽い版で赤いかさが白かった）
                    // 気球の皮は細い縫い目の線がテクスチャにあって、頂点で拾うと太い黒いすじになったので色だけ（2026-11-17）
                    var tex = m != null && m.HasProperty("_MainTex") && muv.Length == mv.Length && !path.Contains("CloudBalloon") ? Readable(m.mainTexture) : null;
                    Vector2 ts = tex != null ? m.mainTextureScale : Vector2.one, to = tex != null ? m.mainTextureOffset : Vector2.zero;
                    var tri = mesh.GetTriangles(s); int bi = V.Count; var map = new Dictionary<int, int>();
                    foreach (var t in tri)
                    {
                        if (!map.TryGetValue(t, out int j))
                        {
                            j = V.Count; V.Add(l2w.MultiplyPoint3x4(mv[t])); S.Add(cell);
                            N.Add(mn.Length == mv.Length ? n2w.MultiplyVector(mn[t]).normalized : Vector3.zero);
                            var col = useVc ? new Color(mc[t].r, mc[t].g, mc[t].b, 1) : useVcLinear ? new Color(mc[t].r, mc[t].g, mc[t].b, 1).gamma : mcol;
                            if (tex != null) { var uv = Vector2.Scale(muv[t], ts) + to; col *= tex.GetPixelBilinear(uv.x, uv.y); }
                            col.a = deco ? 0 : 1;   // a=0 は飾り：SetBounds の床の箱（近づいたら戻す・入場判定）に入れない。間引きも粗く
                            C.Add(col); map[t] = j;
                        }
                        T.Add(j);
                    }
                    // 裏表：拡大率が負の物は向きを反転
                    if (l2w.determinant < 0) for (int k = T.Count - tri.Length; k < T.Count; k += 3) { var x = T[k + 1]; T[k + 1] = T[k + 2]; T[k + 2] = x; }
                }
                used++;
            }
            // 頂点を格子にまとめて間引く（位置が同じ格子の頂点は1つに。色は平均）。
            // ただし向き（法線）や色が大きく違う頂点はまとめない：薄い板（スタートの島の芝 15cm）の表と裏がまとまって面がガタガタに傾き、
            // 放射状の影が出たり、芝が下がって本体では隠れている道の端が見えたりした（2026-10-22 てじゃ）
            // 位置は「同じ所にあった頂点どうし（角ばった岩の折れ目など）」が別々のまとまりになっても1つにそろえる：
            // まとまりごとに平均を取ると、折れ目の両側が別の位置に動いて空がすける割れ目になった（2026-10-22 てじゃ「軽量版の洞窟とかが隙間バキバキに」）
            var cellMap = new Dictionary<(Vector3Int, Vector3Int, Vector3Int), int>(); var NC = new List<Color>(); var cnt = new List<int>(); var remap = new int[V.Count];
            var parent = new List<int>(); var weld = new Dictionary<Vector3Int, int>();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int i = 0; i < V.Count; i++)
            {
                // 巣・根っこは向きや色で分けない（分けると細い枝の面が残りまくって減らなかった）
                var k = S[i] == NestCell ? (Vector3Int.FloorToInt(V[i] / S[i]), Vector3Int.zero, new Vector3Int(-1, -1, -1))
                    : (Vector3Int.FloorToInt(V[i] / S[i]), Vector3Int.RoundToInt(N[i] * 1.5f), Vector3Int.RoundToInt(new Vector3(C[i].r, C[i].g, C[i].b + C[i].a * 10f) * 6f));
                if (!cellMap.TryGetValue(k, out int j)) { j = NC.Count; cellMap[k] = j; NC.Add(Color.clear); cnt.Add(0); parent.Add(j); }
                NC[j] += C[i]; cnt[j]++; remap[i] = j;
                var w = Vector3Int.RoundToInt(V[i] * 1000f);   // 1mm 以内は同じ所
                if (weld.TryGetValue(w, out int o)) { int ra = Find(o), rb = Find(j); if (ra != rb) parent[rb] = ra; } else weld[w] = j;
            }
            for (int j = 0; j < NC.Count; j++) NC[j] /= cnt[j];
            var NV = new Vector3[NC.Count]; var gv = new Vector3[NC.Count]; var gc = new int[NC.Count];
            for (int i = 0; i < V.Count; i++) { int g = Find(remap[i]); gv[g] += V[i]; gc[g]++; }
            for (int j = 0; j < NC.Count; j++) { int g = Find(j); NV[j] = gv[g] / gc[g]; }
            var NT = new List<int>();
            for (int k = 0; k < T.Count; k += 3)
            {
                int a = remap[T[k]], b2 = remap[T[k + 1]], c = remap[T[k + 2]];
                int ga = Find(a), gb = Find(b2), gc2 = Find(c); if (ga == gb || gb == gc2 || ga == gc2) continue;
                NT.Add(a); NT.Add(b2); NT.Add(c);
            }
            // 面ごとに頂点を分けて、法線は面の向き（つぶした頂点で法線をなめらかにすると、放射状のヘンな影が出るので）
            var FV = new List<Vector3>(); var FN = new List<Vector3>(); var FC = new List<Color>(); var FT = new List<int>();
            for (int k = 0; k < NT.Count; k += 3)
            {
                var a = NV[NT[k]]; var b2 = NV[NT[k + 1]]; var c = NV[NT[k + 2]]; var fn = Vector3.Cross(b2 - a, c - a); if (fn.sqrMagnitude < 1e-10f) continue; fn.Normalize();
                int bi2 = FV.Count; FV.Add(a); FV.Add(b2); FV.Add(c); FN.Add(fn); FN.Add(fn); FN.Add(fn);
                foreach (var idx in new[] { NT[k], NT[k + 1], NT[k + 2] }) { var col = NC[idx]; col.a = col.a < 0.5f ? 0 : 1; FC.Add(col); }
                FT.Add(bi2); FT.Add(bi2 + 1); FT.Add(bi2 + 2);
            }
            var lite = new Mesh { name = cn + "_Lite", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            lite.SetVertices(FV); lite.SetNormals(FN); lite.SetColors(FC);   // 色はガンマのまま（InkCap シェーダーが中で直す）
            lite.SetTriangles(FT, 0);
            lite.RecalculateBounds();
            string ap = Dir + "/" + cn + "_Lite.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(lite, old); lite = old; } else AssetDatabase.CreateAsset(lite, ap);
            var go = holder.transform.Find(cn + "_Lite")?.gameObject;
            if (go == null) { go = new GameObject(cn + "_Lite"); go.transform.SetParent(holder.transform, false); go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>(); }
            go.GetComponent<MeshFilter>().sharedMesh = lite; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            go.SetActive(false);
            var st = root.transform.Find("BallStart"); if (st == null) st = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BallStart");
            starts.Add(st); fulls.Add(root); lites.Add(go);
            log.Append(cn + ": parts " + used + " tris " + (NT.Count / 3) + "\n");
        }
        // 切り替えスクリプトの設定
        var lod = lodTarget;
        if (lod != null)
        {
            var so = new SerializedObject(lod);
            void SetArr(string name, Object[] arr) { var p = so.FindProperty(name); p.arraySize = arr.Length; for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i]; }
            SetArr("starts", starts.ToArray()); SetArr("fullRoots", fulls.ToArray()); SetArr("liteRoots", lites.ToArray());
            so.ApplyModifiedProperties();
            SetBounds(lod);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[軽い版] " + log);
    }

    // テクスチャの色を読めるコピー（読み込み設定が Read/Write でなくても読めるように、GPU で写して読む。色はガンマのまま）
    static readonly Dictionary<Texture, Texture2D> _readable = new Dictionary<Texture, Texture2D>();
    static Texture2D Readable(Texture t)
    {
        if (t == null) return null;
        if (_readable.TryGetValue(t, out var r) && r != null) return r;
        var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(t, rt); RenderTexture.active = rt;
        r = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false, false) { wrapMode = t.wrapMode, filterMode = FilterMode.Bilinear };
        r.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0); r.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        _readable[t] = r; return r;
    }

    // 近づいたら本体に戻す判定用：各コースの床（軽い版のメッシュの頂点）を cellSize 角のマスに分けて、マスごとに中の頂点を囲む箱を持たせる。
    // コース全体の箱1個だと隣のコースと重なって、普通に遊んでいても隣が戻りっぱなしになったので。マスそのままの箱だと実際より最大10m近く見えた
    public static string SetBounds(FuwaCourseLod lod)
    {
        Undo.RecordObject(lod, "lite cells");
        float cs = lod.cellSize > 0f ? lod.cellSize : 10f;
        var mins = new List<Vector3>(); var maxs = new List<Vector3>(); var course = new List<int>(); var log = new System.Text.StringBuilder();
        for (int i = 0; i < lod.liteRoots.Length; i++)
        {
            var mf = lod.liteRoots[i] != null ? lod.liteRoots[i].GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) continue;
            var boxes = new Dictionary<Vector3Int, Bounds>(); var tr = lod.liteRoots[i].transform;
            var verts = mf.sharedMesh.vertices; var cols = mf.sharedMesh.colors;
            for (int vi = 0; vi < verts.Length; vi++)
            {
                if (cols.Length == verts.Length && cols[vi].a < 0.5f) continue;   // まわりの浮島などの飾りは床に数えない
                var v = tr.TransformPoint(verts[vi]); var k = Vector3Int.FloorToInt(v / cs);
                if (boxes.TryGetValue(k, out var b)) { b.Encapsulate(v); boxes[k] = b; } else boxes[k] = new Bounds(v, Vector3.zero);
            }
            foreach (var b in boxes.Values) { mins.Add(b.min); maxs.Add(b.max); course.Add(i); }
            log.Append(lod.liteRoots[i].name + " " + boxes.Count + "マス\n");
        }
        lod.cellMin = mins.ToArray(); lod.cellMax = maxs.ToArray(); lod.cellCourse = course.ToArray();

        // 隣のコース（床の箱どうしが neighborDist 以内）。ふつうではコースの中にいても隣は本体のまま
        int n = lod.fullRoots.Length; var bits = new int[n];
        for (int a = 0; a < mins.Count; a++)
            for (int b = 0; b < mins.Count; b++)
            {
                int ca = course[a], cb = course[b]; if (ca == cb || ((bits[ca] >> cb) & 1) == 1) continue;
                var gap = Vector3.Max(Vector3.zero, Vector3.Max(mins[b] - maxs[a], mins[a] - maxs[b]));
                if (gap.magnitude <= lod.neighborDist) { bits[ca] |= 1 << cb; bits[cb] |= 1 << ca; }
            }
        lod.neighborBits = bits;
        for (int i = 0; i < n; i++)
            log.Append(lod.fullRoots[i].name + " の隣: " + string.Join(",", Enumerable.Range(0, n).Where(j => ((bits[i] >> j) & 1) == 1).Select(j => lod.fullRoots[j].name)) + "\n");

        // ロビーで止める物がどのコースの物か（ふつうでコースの中にいる時、そのコースの物は止めない）
        int CourseOf(Transform t) { for (int i = 0; i < n; i++) if (lod.fullRoots[i] != null && t.IsChildOf(lod.fullRoots[i].transform)) return i; return -1; }
        lod.stopCourse = (lod.lobbyStopBehaviours ?? new VRC.Udon.UdonBehaviour[0]).Select(b => b != null ? CourseOf(b.transform) : -1).ToArray();
        lod.hideCourse = (lod.lobbyHideObjects ?? new GameObject[0]).Select(o => o != null ? CourseOf(o.transform) : -1).ToArray();

        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(lod); EditorUtility.SetDirty(lod);
        return log.ToString();
    }
}
