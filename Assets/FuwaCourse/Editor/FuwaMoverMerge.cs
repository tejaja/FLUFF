using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 「一緒に動く部品」を1つのメッシュにまとめて描画回数を減らす（2026-11-22 てじゃ「どうせ全部やるしまかせるよ」）。
// Android で測ると 2–4 のスタートが 490 batches でダントツ。見えている部品 1233 個のうち 743 個が動く物（歯車・ポンプ・跳ね橋）で、
// 動く物は静的バッチにならず、頂点が多いとダイナミックバッチングも効かないので、部品1つ＝描画1回になっていた。
//
// 決まり：
//  ・「動かす人」（mover）＝ U# が参照している Transform / GameObject / コンポーネントの持ち主、Rigidbody・Animator・UdonBehaviour の付いた物。
//    部品は「自分か親で一番近い mover」と一緒に動くので、同じ mover・同じレイヤー・同じ影の設定の部品を、マテリアルごとのサブメッシュにして1つにまとめる
//  ・まとめた物は mover の子「__Merged_n」（mover の座標で作ったメッシュ）。元の描画物は enabled=false にするだけ（当たり・スクリプトの参照はそのまま）
//  ・まとめない物：静的な物（静的バッチが受け持つ）／U# が直接参照している描画物（色・表示・影を変える）／実行中に子を探してさわるスクリプト（FuwaMaterialMerge の touchy）
//    と Animator の下／LODGroup の下／透明（キュー 2500 以上）／TextMeshPro／読めないメッシュ／1つしかない組
//  ・記録（record.json）で元に戻せる。マテリアルまとめ（FuwaMaterialMerge.Run）の最初で戻して、最後にかけ直す（元のメッシュが変わってもまとめ直される）
public static class FuwaMoverMerge
{
    const string Dir = "Assets/FuwaCourse/Generated/MoverMerge";
    static string RecPath => System.IO.Path.Combine(Application.dataPath, "..", Dir, "record.json");
    [System.Serializable] class FsRec { public string comp; public List<string> targets = new List<string>(); public List<Vector3> centers = new List<Vector3>(); public List<float> radii = new List<float>(); public List<int> stages = new List<int>(); }
    [System.Serializable] class Rec { public List<string> merged = new List<string>(); public List<string> disabled = new List<string>(); public List<FsRec> farShadows = new List<FsRec>(); }

    [MenuItem("FuwaCourse/よく使う/一緒に動く部品をまとめる（描画回数を減らす）")]
    static void Menu() { Debug.Log(Run(false)); }
    [MenuItem("FuwaCourse/よく使う/一緒に動く部品をまとめる（数えるだけ）")]
    static void MenuDry() { Debug.Log(Run(true)); }
    [MenuItem("FuwaCourse/よく使う/一緒に動く部品のまとめを元にもどす")]
    static void MenuRevert() { Debug.Log("[部品まとめ] 元にもどした " + Revert()); }

    // まとめのために enabled=false にした元の描画物（軽い版は __Merged_ ではなくこっちを1つずつ見る。2026-11-26 まとめごと拾ってスタートキノコの軸だけ残っていた）
    public static HashSet<Renderer> MergedSources()
    {
        var map = Resolve(Load().disabled);
        return new HashSet<Renderer>(map.Values.OfType<Renderer>().Where(r => r));
    }
    static Rec Load() { return System.IO.File.Exists(RecPath) ? JsonUtility.FromJson<Rec>(System.IO.File.ReadAllText(RecPath)) : new Rec(); }
    static void Save(Rec r) { if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "MoverMerge"); System.IO.File.WriteAllText(RecPath, JsonUtility.ToJson(r, true)); }

    static Dictionary<string, Object> Resolve(IEnumerable<string> keys)
    {
        var list = keys.Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
        var gids = new List<GlobalObjectId>(); var ks = new List<string>();
        foreach (var k in list) if (GlobalObjectId.TryParse(k, out var g)) { gids.Add(g); ks.Add(k); }
        var objs = new Object[gids.Count]; GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(gids.ToArray(), objs);
        var map = new Dictionary<string, Object>(); for (int i = 0; i < ks.Count; i++) map[ks[i]] = objs[i];
        return map;
    }

    public static int Revert()
    {
        var rec = Load(); int n = 0;
        var map = Resolve(rec.merged.Concat(rec.disabled).Concat(rec.farShadows.Select(f => f.comp)).Concat(rec.farShadows.SelectMany(f => f.targets)));
        // 遠くの影の一覧（FuwaFarShadows）を元の中身にもどす
        foreach (var f in rec.farShadows)
        {
            if (!map.TryGetValue(f.comp, out var co) || !(co is FuwaFarShadows fs) || !fs) continue;
            Undo.RecordObject(fs, "部品まとめを戻す");
            // 2026-11-26 記録をそのまま戻すと、まとめた後に一覧から外した物（チュートリアルの看板11枚の影、11/22）が戻ってしまった
            //  → 記録の物は「今も一覧にある」か「まとめで __Merged_ に置きかわった元」だけ戻す。まとめた後に足された物（__Merged_ 以外）は残す
            var now = fs.targets ?? new Renderer[0];
            var nowSet = new HashSet<Renderer>(now.Where(x => x));
            var srcSet = new HashSet<Renderer>(rec.disabled.Select(k => map.TryGetValue(k, out var o) ? o as Renderer : null).Where(x => x));
            var T = new List<Renderer>(); var C = new List<Vector3>(); var R = new List<float>(); var S = new List<int>(); var seen = new HashSet<Renderer>();
            for (int i = 0; i < f.targets.Count; i++)
            {
                var rr = map.TryGetValue(f.targets[i], out var o) ? o as Renderer : null;
                if (rr == null || !(nowSet.Contains(rr) || srcSet.Contains(rr)) || !seen.Add(rr)) continue;
                T.Add(rr); C.Add(f.centers[i]); R.Add(f.radii[i]); S.Add(i < f.stages.Count ? f.stages[i] : 0);
            }
            for (int i = 0; i < now.Length; i++)
            {
                var rr = now[i]; if (rr == null || rr.name.StartsWith("__Merged_") || !seen.Add(rr)) continue;
                T.Add(rr); C.Add(fs.centers != null && i < fs.centers.Length ? fs.centers[i] : rr.bounds.center); R.Add(fs.radii != null && i < fs.radii.Length ? fs.radii[i] : rr.bounds.extents.magnitude);
                S.Add(fs.targetStage != null && i < fs.targetStage.Length ? fs.targetStage[i] : 0);
            }
            fs.targets = T.ToArray(); fs.centers = C.ToArray(); fs.radii = R.ToArray(); fs.targetStage = S.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fs); EditorUtility.SetDirty(fs);
        }
        foreach (var k in rec.disabled) if (map.TryGetValue(k, out var o) && o is Renderer r && r) { Undo.RecordObject(r, "部品まとめを戻す"); r.enabled = true; n++; }
        foreach (var k in rec.merged) if (map.TryGetValue(k, out var o) && o is GameObject g && g) Undo.DestroyObjectImmediate(g);
        // 記録にない迷子の __Merged_ も消す
        foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(t => t && t.name.StartsWith("__Merged_") && t.GetComponent<MeshFilter>() && t.GetComponent<MeshFilter>().sharedMesh && AssetDatabase.GetAssetPath(t.GetComponent<MeshFilter>().sharedMesh).StartsWith(Dir)).ToList())
            Undo.DestroyObjectImmediate(t.gameObject);
        rec.merged.Clear(); rec.disabled.Clear(); rec.farShadows.Clear(); Save(rec);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return n;
    }

    public static string Run(bool dry)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!dry) Revert();
        var roots = scene.GetRootGameObjects();
        // U# の参照
        var refT = new HashSet<Transform>(); var refR = new HashSet<Object>();
        // 遠くの影の一覧（FuwaFarShadows）は影の出し入れだけなので、まとめた物に入れかえる（同じ一覧どうしだけまとめる）
        var fsOf = new Dictionary<Renderer, FuwaFarShadows>();
        foreach (var fs in roots.SelectMany(g => g.GetComponentsInChildren<FuwaFarShadows>(true)))
            if (fs.targets != null) foreach (var t in fs.targets) if (t && !fsOf.ContainsKey(t)) fsOf[t] = fs;
        foreach (var u in roots.SelectMany(g => g.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)))
        {
            if (u is FuwaFarShadows) continue;
            var so = new SerializedObject(u); var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null || it.propertyPath == "m_GameObject" || it.propertyPath == "m_Script") continue;
                var o = it.objectReferenceValue;
                if (o is Renderer || o is MeshFilter) { refR.Add(o); refR.Add(((Component)o).gameObject); refT.Add(((Component)o).transform); }
                else if (o is GameObject go) { refT.Add(go.transform); refR.Add(go); }
                else if (o is Component c) refT.Add(c.transform);
            }
        }
        // 子を探してさわる（GetComponentsInChildren・GetChild・Find）か、参照から GetComponent<Renderer> するスクリプトの下はまとめない。
        //  ほかの touchy（FuwaSugarPipe・FuwaCheckpoint など）は参照している描画物だけをさわるので、その描画物を外せば下の部品はまとめてよい（2026-11-22 調べた）
        var touchyNames = MoverBlock;
        var touchyRoots = new HashSet<Transform>(roots.SelectMany(g => g.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)).Where(u => touchyNames.Contains(u.GetType().Name)).Select(u => u.transform));
        bool IsMover(Transform t) => refT.Contains(t) || t.GetComponent<Rigidbody>() || t.GetComponent<Animator>() || t.GetComponent<VRC.Udon.UdonBehaviour>() || t.parent == null;
        bool Blocked(Transform t) { for (var p = t; p != null; p = p.parent) if (touchyRoots.Contains(p) || p.GetComponent<Animator>() || p.GetComponent<LODGroup>()) return true; return false; }

        var groups = new Dictionary<(Transform mover, int layer, UnityEngine.Rendering.ShadowCastingMode sh, bool recv, FuwaFarShadows fs), List<MeshRenderer>>();
        int candidates = 0;
        foreach (var r in roots.SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)) continue;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
            if (r.GetComponent<TMPro.TMP_Text>()) continue;
            if (refR.Contains(r) || refR.Contains(r.gameObject) || refR.Contains(mf)) continue;
            if (r.sharedMaterials.Any(m => !m || m.renderQueue >= 2500)) continue;
            // メッシュの座標が変わるので、自分の座標を使うシェーダー（水の流れ・模様の向き・根元のフェードなど）はまとめない。ToonLit だけ（2026-11-22 2–1 の水の壁が消えた）
            if (r.sharedMaterials.Any(m => m.shader == null || m.shader.name != "FuwaCourse/ToonLit" || FuwaMaterialMerge.UsesObjectSpace(m))) continue;
            if (mf.sharedMesh.subMeshCount < r.sharedMaterials.Length) continue;
            if (r.name.StartsWith("__Merged_") || Blocked(r.transform)) continue;
            // あそびばのドームのふちの輪（Locked*/Rim）は FuwaPlaygroundUnlocks が名前で探してふたといっしょに開くので、まとめない
            //（2026-11-27 てじゃ「ドームが開くアニメーションが2種類ある」：まとめた所だけ輪が動かず、透明なふただけ開いていた）
            if (r.name == "Rim" && r.transform.parent != null && r.transform.parent.name.StartsWith("Locked")) continue;
            var mover = r.transform; while (!IsMover(mover)) mover = mover.parent;
            // 自分自身が mover（スクリプトが回す歯車そのもの）でも、その子の部品と一緒にまとめてよい（まとめた物は mover の子なので一緒に回る）
            var key = (mover, r.gameObject.layer, r.shadowCastingMode, r.receiveShadows, fsOf.TryGetValue(r, out var fs0) ? fs0 : null);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
            list.Add(r); candidates++;
        }
        var useful = groups.Where(g => g.Value.Count >= 2).ToList();
        int before = useful.Sum(g => g.Value.Sum(r => r.sharedMaterials.Length));
        int after = useful.Sum(g => g.Value.SelectMany(r => r.sharedMaterials).Distinct().Count());
        var perRoot = useful.GroupBy(g => g.Key.mover.root.name).Select(x => x.Key + " " + x.Sum(g => g.Value.Sum(r => r.sharedMaterials.Length)) + "→" + x.Sum(g => g.Value.SelectMany(r => r.sharedMaterials).Distinct().Count())).ToList();
        string head = $"[部品まとめ]{(dry ? "（数えるだけ）" : "")} 組 {useful.Count}／描画 {before} → {after}（候補 {candidates}）\n  " + string.Join("\n  ", perRoot);
        if (dry) return head;

        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "MoverMerge");
        var rec = new Rec(); int gi = 0; var used = new HashSet<string>();
        // 遠くの影の一覧の元の中身を記録（戻す用）。作業用のリストに写して、まとめた物に入れかえていく
        var fsWork = new Dictionary<FuwaFarShadows, (List<Renderer> t, List<Vector3> c, List<float> r, List<int> st)>();
        foreach (var fs in useful.Select(g => g.Key.fs).Where(f => f != null).Distinct())
        {
            var t = fs.targets.ToList(); var c = (fs.centers ?? new Vector3[0]).ToList(); var rr = (fs.radii ?? new float[0]).ToList();
            var st = (fs.targetStage ?? new int[0]).ToList(); while (st.Count < t.Count) st.Add(0);
            var tid = new GlobalObjectId[t.Count]; GlobalObjectId.GetGlobalObjectIdsSlow(t.Select(x => (Object)x).ToArray(), tid);
            rec.farShadows.Add(new FsRec { comp = GlobalObjectId.GetGlobalObjectIdSlow(fs).ToString(), targets = tid.Select(x => x.ToString()).ToList(), centers = c.ToList(), radii = rr.ToList(), stages = st.ToList() });
            fsWork[fs] = (t, c, rr, st);
        }
        foreach (var g in useful)
        {
            var mover = g.Key.mover; var rs = g.Value;
            var mats = rs.SelectMany(r => r.sharedMaterials).Distinct().ToList();
            var w2l = mover.worldToLocalMatrix;
            // マテリアルごとに、元の各サブメッシュをまとめる（負の拡大の部品は面の向きを戻す）
            var perMat = new List<Mesh>(); var tmps = new List<Mesh>();
            foreach (var m in mats)
            {
                var cis = new List<CombineInstance>();
                foreach (var r in rs)
                {
                    var mf = r.GetComponent<MeshFilter>(); var src = mf.sharedMesh; var M = w2l * r.transform.localToWorldMatrix;
                    for (int s = 0; s < r.sharedMaterials.Length; s++)
                    {
                        if (r.sharedMaterials[s] != m) continue;
                        if (src.GetIndexCount(s) == 0) continue;   // 空のサブメッシュ（色まとめで中身がなくなった物）。混ざると2段目のまとめでほかの部品ごと消えた
                        var mesh = src; int sub = s;
                        if (M.determinant < 0f)
                        {
                            mesh = Object.Instantiate(src); tmps.Add(mesh);
                            for (int k = 0; k < mesh.subMeshCount; k++) { var t = mesh.GetTriangles(k); for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } mesh.SetTriangles(t, k); }
                        }
                        cis.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = M });
                    }
                }
                if (cis.Count == 0) { perMat.Add(null); continue; }
                var pm = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                pm.CombineMeshes(cis.ToArray(), true, true, false);
                perMat.Add(pm.vertexCount > 0 ? pm : null);
            }
            // 中身のないマテリアルは外す（サブメッシュとマテリアルの並びをそろえる）
            var keepIdx = Enumerable.Range(0, mats.Count).Where(i => perMat[i] != null).ToList();
            mats = keepIdx.Select(i => mats[i]).ToList(); perMat = keepIdx.Select(i => perMat[i]).ToList();
            if (perMat.Count == 0) { foreach (var t in tmps) Object.DestroyImmediate(t); continue; }
            // 頂点数で先に決める（まとめた後に indexFormat を変えると三角形が消えた）
            var fmt = perMat.Sum(pm => pm.vertexCount) > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            var merged = new Mesh { indexFormat = fmt };
            merged.CombineMeshes(perMat.Select(pm => new CombineInstance { mesh = pm, transform = Matrix4x4.identity }).ToArray(), false, false, false);
            merged.RecalculateBounds();
            foreach (var pm in perMat) Object.DestroyImmediate(pm); foreach (var t in tmps) Object.DestroyImmediate(t);
            // 名前：コースの名前＋mover の名前（重なったら番号）
            string baseName = Sanitize(mover.root.name + "_" + mover.name); string nm = baseName; int k2 = 1; while (!used.Add(nm)) nm = baseName + "_" + (++k2);
            string path = Dir + "/" + nm + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old) { old.Clear(); old.indexFormat = merged.indexFormat; old.SetVertices(merged.vertices); old.SetNormals(merged.normals); if (merged.tangents.Length > 0) old.SetTangents(merged.tangents); if (merged.colors32.Length > 0) old.SetColors(merged.colors32);
                for (int c = 0; c < 4; c++) { var u = new List<Vector4>(); merged.GetUVs(c, u); if (u.Count > 0) old.SetUVs(c, u); }
                old.subMeshCount = merged.subMeshCount; for (int s = 0; s < merged.subMeshCount; s++) old.SetTriangles(merged.GetTriangles(s), s); old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(merged); merged = old; }
            else { merged.name = nm; AssetDatabase.CreateAsset(merged, path); }
            var go = new GameObject("__Merged_" + (gi++)); Undo.RegisterCreatedObjectUndo(go, "部品まとめ");
            go.transform.SetParent(mover, false); go.layer = g.Key.layer;
            go.AddComponent<MeshFilter>().sharedMesh = merged;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats.ToArray();
            var first = rs[0];
            mr.shadowCastingMode = g.Key.sh; mr.receiveShadows = g.Key.recv; mr.lightProbeUsage = first.lightProbeUsage; mr.reflectionProbeUsage = first.reflectionProbeUsage;
            mr.probeAnchor = first.probeAnchor; mr.allowOcclusionWhenDynamic = first.allowOcclusionWhenDynamic;
            foreach (var r in rs) { Undo.RecordObject(r, "部品まとめ"); r.enabled = false; }
            if (g.Key.fs != null)
            {
                var w = fsWork[g.Key.fs]; int stage = 0; var set = new HashSet<Renderer>(rs);
                for (int i = w.t.Count - 1; i >= 0; i--) if (w.t[i] && set.Contains(w.t[i])) { stage = w.st[i]; w.t.RemoveAt(i); w.c.RemoveAt(i); w.r.RemoveAt(i); w.st.RemoveAt(i); }
                var b = mr.bounds; w.t.Add(mr); w.c.Add(b.center); w.r.Add(b.extents.magnitude); w.st.Add(stage);
            }
            rec.merged.Add(GlobalObjectId.GetGlobalObjectIdSlow(go).ToString());
        }
        foreach (var kv in fsWork)
        {
            var fs = kv.Key; Undo.RecordObject(fs, "部品まとめ");
            fs.targets = kv.Value.t.ToArray(); fs.centers = kv.Value.c.ToArray(); fs.radii = kv.Value.r.ToArray(); fs.targetStage = kv.Value.st.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fs); EditorUtility.SetDirty(fs);
        }
        var all = useful.SelectMany(g => g.Value).Cast<Object>().ToArray();
        var ids = new GlobalObjectId[all.Length]; GlobalObjectId.GetGlobalObjectIdsSlow(all, ids);
        rec.disabled = ids.Select(i => i.ToString()).ToList();
        Save(rec);
        // 使わなくなった古いメッシュを消す（組の数が変わると前の名前が残る）
        foreach (var gid in AssetDatabase.FindAssets("t:Mesh", new[] { Dir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(gid);
            if (!used.Contains(System.IO.Path.GetFileNameWithoutExtension(p))) AssetDatabase.DeleteAsset(p);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return head;
    }

    public static readonly HashSet<string> MoverBlock = new HashSet<string> {
        "FuwaBlower", "FuwaEnemy", "FuwaGoal", "FuwaGunShadows", "FuwaJumpBoost", "FuwaMama", "FuwaMovableMushroom", "FuwaPlaygroundPlush", "FuwaCloudBalloon",   // 子を探す
        "FuwaDemoStand", "FuwaHud", "FuwaPlaygroundFlameRock", "FuwaSharedBall", "FuwaFireJet", "FuwaPortal",                                                      // GetComponent<Renderer>
        "FuwaBall", "FuwaGhost", "FuwaSharedBallShadows", "FuwaPlayerShadows", "FuwaGunShowcase", "FuwaGunSummon", "FuwaUmbrella", "FuwaPhotoAlbum", "FuwaPhotoFluff", "FuwaPortalCams" };   // プレイヤー・銃・撮影まわり（念のため）
    static string Sanitize(string s) { foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace(' ', '_'); }
}
