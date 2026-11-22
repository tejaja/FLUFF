using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 2026-11-19 てじゃ「デカい軽量化やるか」「マテリアルとかそのへん」→ A「色だけちがう材質をまとめる」
//  動かない物（BatchingStatic）の FuwaCourse/ToonLit の材質のうち、色（_Color）以外が全部同じ仲間を1つの材質にまとめる。
//  色は頂点の色（mesh.colors）に入れて、まとめた材質は _UseVertexColor（_FUWA_VCOLOR）でかける。同じ材質になるので静的バッチでいっしょに描ける。
//  ・動く物（静的でない物）・スクリプトが参照している描画物は対象外（材質を入れかえる仕組みとぶつからないように）
//  ・オブジェクト座標を使う材質（_PatternFromBase・_PatternFromUV3・_BaseFadeLength・_FlowStrength）は対象外（静的バッチで模様がずれる）
//  ・メッシュは色をつけた写しを Assets/FuwaCourse/Generated/MatMerge に作る（同じメッシュ・同じ色の組は使い回す）
//  ビルダーで作り直すと元の材質に戻るので、そのあとまたメニューを押す（何度押してもいい）
public static class FuwaMaterialMerge
{
    const string Dir = "Assets/FuwaCourse/Generated/MatMerge";
    const string ShaderName = "FuwaCourse/ToonLit";

    [MenuItem("FuwaCourse/よく使う/色ちがいの材質をまとめる（軽量化）")]
    static void Menu() { Debug.Log(Run(false)); }
    [MenuItem("FuwaCourse/よく使う/色ちがいの材質をまとめる（数えるだけ）")]
    static void MenuDry() { Debug.Log(Run(true)); }
    [MenuItem("FuwaCourse/よく使う/色ちがいの材質のまとめを元にもどす")]
    static void MenuRevert() { Debug.Log("[材質まとめ] 元にもどした " + Revert()); }

    // 変換した描画物の元のメッシュ・材質の記録（record.json）。まとめ直す時はいったん元にもどしてから作る
    [System.Serializable] class Rec { public string r, mesh; public string[] mats; }
    [System.Serializable] class RecFile { public List<Rec> items = new List<Rec>(); }
    static string RecPath => Dir + "/record.json";
    static string Gid(Object o) => GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
    static Object FromGid(string s) => GlobalObjectId.TryParse(s, out var g) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g) : null;
    static RecFile LoadRec() { var f = System.IO.Path.Combine(Application.dataPath, "..", RecPath); return System.IO.File.Exists(f) ? JsonUtility.FromJson<RecFile>(System.IO.File.ReadAllText(f)) : new RecFile(); }
    static void SaveRec(RecFile r) { var f = System.IO.Path.Combine(Application.dataPath, "..", RecPath); System.IO.File.WriteAllText(f, JsonUtility.ToJson(r, true)); }
    public static int Revert()
    {
        var rec = LoadRec(); int n = 0;
        // 2026-11-22 1個ずつ FromGid（GlobalObjectIdentifierToObjectSlow）で引くと 2600 個で20分以上固まった → ID をまとめて1回で引く
        var keys = rec.items.SelectMany(it => new[] { it.r, it.mesh }.Concat(it.mats)).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var gids = new List<GlobalObjectId>(); var gkeys = new List<string>();
        foreach (var k in keys) if (GlobalObjectId.TryParse(k, out var g)) { gids.Add(g); gkeys.Add(k); }
        var objs = new Object[gids.Count]; GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(gids.ToArray(), objs);
        var map = new Dictionary<string, Object>(); for (int i = 0; i < gkeys.Count; i++) map[gkeys[i]] = objs[i];
        Object Get(string s) => !string.IsNullOrEmpty(s) && map.TryGetValue(s, out var o) ? o : null;
        foreach (var it in rec.items)
        {
            var r = Get(it.r) as MeshRenderer; if (!r) continue;
            var mf = r.GetComponent<MeshFilter>(); var mesh = Get(it.mesh) as Mesh;
            if (!mf || !mesh || !mf.sharedMesh || !mf.sharedMesh.name.StartsWith("VC_")) continue;   // 作り直しで元にもどっている物はそのまま
            Undo.RecordObject(mf, "材質まとめを戻す"); Undo.RecordObject(r, "材質まとめを戻す");
            mf.sharedMesh = mesh; r.sharedMaterials = it.mats.Select(m => Get(m) as Material).ToArray(); n++;
        }
        rec.items.Clear(); SaveRec(rec);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return n;
    }

    static string Sig(Material m)
    {
        var sh = m.shader; var parts = new List<string> { string.Join(",", m.shaderKeywords.OrderBy(k => k)), m.renderQueue.ToString() };
        for (int i = 0; i < sh.GetPropertyCount(); i++)
        {
            var n = sh.GetPropertyName(i); var t = sh.GetPropertyType(i); if (n == "_Color") continue;
            if (t == UnityEngine.Rendering.ShaderPropertyType.Texture) parts.Add(n + "=" + (m.GetTexture(n) ? AssetDatabase.GetAssetPath(m.GetTexture(n)) + m.GetTexture(n).name : "-") + m.GetTextureScale(n) + m.GetTextureOffset(n));
            else if (t == UnityEngine.Rendering.ShaderPropertyType.Color || t == UnityEngine.Rendering.ShaderPropertyType.Vector) parts.Add(n + "=" + m.GetVector(n).ToString("F4"));
            else parts.Add(n + "=" + m.GetFloat(n).ToString("F4"));
        }
        return string.Join("|", parts);
    }

    public static bool UsesObjectSpace(Material m)
    {
        float F(string n) => m.HasProperty(n) ? m.GetFloat(n) : 0f;
        return F("_PatternFromBase") > 0.5f || F("_PatternFromUV3") > 0.5f || F("_BaseFadeLength") > 0f || F("_FlowStrength") > 0f;
    }

    static string Hash(string s) { unchecked { uint h = 2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } return h.ToString("x8"); } }

    public static bool IncludeMoving = true;
    // 実行中にマテリアル・メッシュをさわる（子を探して色を変える等）スクリプト。FuwaMoverMerge でも使う
    public static readonly HashSet<string> TouchyNames = new HashSet<string> { "FuwaBall", "FuwaBlower", "FuwaCheckpoint", "FuwaCourseWarp", "FuwaDemoStand", "FuwaEnemy", "FuwaFade", "FuwaFollowCam", "FuwaGhost", "FuwaGoal", "FuwaGroundMarker", "FuwaGunShadows", "FuwaGunShowcase", "FuwaGunSummon", "FuwaHud", "FuwaJumpBoost", "FuwaMama", "FuwaMovableMushroom", "FuwaPhotoAlbum", "FuwaPlayerShadows", "FuwaPlaygroundFlameRock", "FuwaPlaygroundPlush", "FuwaPlaygroundRing", "FuwaSharedBall", "FuwaSharedBallShadows", "FuwaThrowRock", "FuwaUmbrella", "FuwaWebHole", "FuwaWindArea", "FuwaClamBubbles", "FuwaCloudBalloon", "FuwaFireball", "FuwaFireJet", "FuwaLavaRock", "FuwaPhotoFluff", "FuwaPortal", "FuwaPortalCams", "FuwaShield", "FuwaShieldGate", "FuwaShockwave", "FuwaSizeRune", "FuwaSugarPipe" };

    // まとめたマテリアルの GPU インスタンシングを、元のマテリアルに合わせる（動く物で、元がオンだった数が多ければオン）。
    //  2026-11-21 てじゃ「なんでバッチふえるんだろ」：動く物（同じ形のキノコ・草の茂みなど）は元のマテリアルのインスタンシングでまとめ描きされていたのに、
    //  まとめたマテリアルはオフにしていたので batches が増えた（ステージ1の3・5 +70〜80）。全部オンだと 2–4・2–5 は逆に少し増えた → 元に合わせる
    public static string MatchInstancing()
    {
        var rec = LoadRec(); var on = new Dictionary<Material, int>(); var off = new Dictionary<Material, int>();
        // 1 個ずつ GlobalObjectIdentifierToObjectSlow で引くと何分も固まった → シーンの描画物の ID をまとめて作って引く。マテリアルは GUID から読む
        var rs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var ids = new GlobalObjectId[rs.Length]; GlobalObjectId.GetGlobalObjectIdsSlow(rs, ids);
        var byId = new Dictionary<string, MeshRenderer>(); for (int i = 0; i < rs.Length; i++) byId[ids[i].ToString()] = rs[i];
        var matCache = new Dictionary<string, Material>();
        Material Mat(string gidStr)
        {
            if (string.IsNullOrEmpty(gidStr)) return null;
            if (matCache.TryGetValue(gidStr, out var mm)) return mm;
            var parts = gidStr.Split('-'); mm = parts.Length > 3 ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(parts[2])) : null;
            matCache[gidStr] = mm; return mm;
        }
        foreach (var it in rec.items)
        {
            if (!byId.TryGetValue(it.r, out var r) || !r) continue;
            if (GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)) continue;
            var cm = r.sharedMaterials;
            for (int s = 0; s < cm.Length && s < it.mats.Length; s++)
            {
                var om = Mat(it.mats[s]); if (!cm[s] || !om || om == cm[s]) continue;
                // ステージ2のコース（FuwaCourse2_*）の動く物は、測るとインスタンシングなしの方が軽かった（2–3 341↔458、2–4 788↔818 batches）→ オフに数える
                bool st2 = r.transform.root.name.StartsWith("FuwaCourse2_");
                var d = om.enableInstancing && !st2 ? on : off; d[cm[s]] = (d.TryGetValue(cm[s], out var v) ? v : 0) + 1;
            }
        }
        var log = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { Dir }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)); if (!m) continue;
            int a = on.TryGetValue(m, out var x) ? x : 0, b = off.TryGetValue(m, out var y) ? y : 0;
            bool want = a > b;
            if (m.enableInstancing != want) { m.enableInstancing = want; EditorUtility.SetDirty(m); }
            if (a + b > 0) log.Add(m.name + " " + (want ? "on" : "off") + "(" + a + "/" + b + ")");
        }
        AssetDatabase.SaveAssets();
        return "インスタンシング: " + string.Join(", ", log);
    }
    public static string Run(bool dry)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int reverted = 0;
        if (!dry) FuwaMoverMerge.Revert();   // 2026-11-22 部品まとめは元のメッシュ・マテリアルから作るので、いったん戻して最後にかけ直す
        if (!dry && AssetDatabase.IsValidFolder(Dir)) reverted = Revert();   // 前のまとめをいったん元にもどしてから作り直す（何度押しても同じ結果）
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).ToList();
        // スクリプト（U#）が参照している描画物・その GameObject は対象外
        var refd = new HashSet<Object>();
        //  （影を切りかえるだけの FuwaFarShadows・FuwaTierShadows と、自分の GameObject（m_GameObject）は材質にさわらないので数えない）
        var safe = new HashSet<string> { "FuwaFarShadows", "FuwaTierShadows" };
        foreach (var u in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)))
        {
            if (safe.Contains(u.GetType().Name)) continue;
            var so = new SerializedObject(u); var it = so.GetIterator();
            while (it.Next(true)) if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null && it.propertyPath != "m_GameObject") refd.Add(it.objectReferenceValue);
        }
        // 2026-11-21 てじゃ「マテリアルまとめ、全コース見直して」→ 案A：動く物（静的でない物）もまとめる（マテリアルの切り替え setpass を減らす）。
        //  ただし、実行中にマテリアル・メッシュをさわるスクリプト（自分で子を探して色を変える物など）の下と、Animator の下は外す
        var touchy = TouchyNames;
        var touchyRoots = new HashSet<Transform>();
        foreach (var u in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)))
            if (touchy.Contains(u.GetType().Name)) touchyRoots.Add(u.transform);
        bool UnderTouchy(Transform t)
        {
            for (var p = t; p != null; p = p.parent) { if (touchyRoots.Contains(p) || p.GetComponent<Animator>()) return true; }
            return false;
        }
        bool Target(MeshRenderer r)
        {
            bool st = GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic);
            if (!st && (!IncludeMoving || UnderTouchy(r.transform))) return false;
            if (r.GetComponent<TMPro.TMP_Text>()) return false;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return false;
            if (refd.Contains(r) || refd.Contains(r.gameObject) || refd.Contains(mf)) return false;
            return true;
        }
        var targets = all.Where(Target).ToList();
        // 仲間（色以外同じ）を作る：対象の描画物で使われている ToonLit の材質だけで数える
        var used = targets.SelectMany(r => r.sharedMaterials).Where(m => m && m.shader.name == ShaderName && !UsesObjectSpace(m) && !m.IsKeywordEnabled("_FUWA_VCOLOR")).Distinct().ToList();
        var groups = used.GroupBy(Sig).Where(g => g.Count() > 1).ToList();
        var toMerged = new Dictionary<Material, Material>();
        if (!dry && !AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "MatMerge");
        foreach (var g in groups)
        {
            var key = Hash(g.Key); var first = g.OrderBy(m => m.name).First();
            Material merged = null;
            if (!dry)
            {
                string path = Dir + "/ToonLitVC_" + key + ".mat";
                merged = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!merged) { merged = new Material(first); AssetDatabase.CreateAsset(merged, path); }
                else merged.CopyPropertiesFromMaterial(first);
                merged.name = "ToonLitVC_" + key;
                merged.SetColor("_Color", Color.white); merged.SetFloat("_UseVertexColor", 1f); merged.EnableKeyword("_FUWA_VCOLOR");
                merged.enableInstancing = false;   // あとで MatchInstancing で元に合わせる
                EditorUtility.SetDirty(merged);
            }
            foreach (var m in g) toMerged[m] = merged;
        }
        var rec = dry ? null : LoadRec();
        var meshCache = new Dictionary<string, Mesh>();
        int changed = 0, newMeshes = 0;
        var pending = new List<(MeshRenderer r, Mesh src, Material[] mats)>();
        foreach (var r in targets)
        {
            var mats = r.sharedMaterials; if (!mats.Any(m => m && toMerged.ContainsKey(m))) continue;
            changed++;
            if (dry) continue;
            var mf = r.GetComponent<MeshFilter>(); var src = mf.sharedMesh;
            int subN = Mathf.Min(src.subMeshCount, mats.Length);
            // サブメッシュごとの色（まとめない所は null＝元の頂点の色のまま）
            var cols = new Color?[src.subMeshCount];
            // 色は材質の見た目の色（sRGB）のまま入れる。リニアへの変換はシェーダーでする（Color32 でも暗い色がずれない）
            for (int s = 0; s < subN; s++) if (mats[s] && toMerged.ContainsKey(mats[s])) cols[s] = mats[s].color;
            // ノーマルマップ（_FUWA_BUMP）以外の ToonLit は接線を使わない。ほかのシェーダーの材質がまざっている時は残す
            bool keepT = mats.Any(m => m && (m.shader.name != ShaderName || m.IsKeywordEnabled("_FUWA_BUMP")));
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(src, out string sg, out long sid);
            string ck = sg + "#" + sid + "#" + src.name + "#" + (keepT ? "T" : "") + "#" + string.Join(";", cols.Select(c => c.HasValue ? ((Color32)c.Value).ToString() : "-"));
            if (!meshCache.TryGetValue(ck, out var vm))
            {
                string mpath = Dir + "/VC_" + Sanitize(src.name) + "_" + Hash(ck) + ".asset";
                vm = AssetDatabase.LoadAssetAtPath<Mesh>(mpath);
                bool isNew = !vm; if (isNew) vm = new Mesh(); else vm.Clear();
                Build(src, cols, vm, keepT);
                vm.name = "VC_" + src.name;
                if (isNew) { AssetDatabase.CreateAsset(vm, mpath); newMeshes++; } else EditorUtility.SetDirty(vm);
                meshCache[ck] = vm;
            }
            pending.Add((r, src, mats));   // 記録の ID はあとでまとめて作る（1個ずつだと遅い）
            Undo.RecordObject(mf, "材質まとめ"); Undo.RecordObject(r, "材質まとめ");
            mf.sharedMesh = vm;
            var nm = (Material[])mats.Clone();
            for (int s = 0; s < nm.Length; s++) if (nm[s] && toMerged.TryGetValue(nm[s], out var mm)) nm[s] = mm;
            r.sharedMaterials = nm;
        }
        if (!dry)
        {
            var objsForId = pending.SelectMany(p => new Object[] { p.r, p.src }.Concat(p.mats.Where(m => m))).Distinct().ToArray();
            var idArr = new GlobalObjectId[objsForId.Length]; GlobalObjectId.GetGlobalObjectIdsSlow(objsForId, idArr);
            var idOf = new Dictionary<Object, string>(); for (int i = 0; i < objsForId.Length; i++) idOf[objsForId[i]] = idArr[i].ToString();
            foreach (var p in pending) rec.items.Add(new Rec { r = idOf[p.r], mesh = idOf[p.src], mats = p.mats.Select(m => m ? idOf[m] : "").ToArray() });
            SaveRec(rec);
            Debug.Log(MatchInstancing());
            // 2026-11-22 アップのたびにビルド前の FuwaMeshWeldAll が VC_ の写しの重複頂点をまとめて保存し直し、まとめ直すとまた戻る（git に毎回数百ファイルの差分）
            //  → まとめた直後にここでもまとめておく（結果は同じ）
            Debug.Log(FuwaMeshWeldAll.Run());
            Debug.Log(FuwaMoverMerge.Run(false));
            // 使わなくなったメッシュの写しを消す
            var keep = new HashSet<string>(meshCache.Values.Select(AssetDatabase.GetAssetPath));
            foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { Dir })) { var pth = AssetDatabase.GUIDToAssetPath(g); if (!keep.Contains(pth)) AssetDatabase.DeleteAsset(pth); }
            AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
        int before = used.Count, after = used.Count - groups.Sum(g => g.Count()) + groups.Count;
        return "[材質まとめ]" + (dry ? "（数えるだけ）" : "") + " 仲間 " + groups.Count + "（" + groups.Sum(g => g.Count()) + " 材質）／ 静的な ToonLit の材質 " + before + " → " + after + "／ 描画物 " + changed + "／ 新しいメッシュ " + newMeshes + "（使い回し込み " + meshCache.Count + "）";
    }

    static string Sanitize(string s) { foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }

    // サブメッシュごとに頂点を分けて（ほかのサブメッシュと共有している頂点は複製）、色を入れる
    static void Build(Mesh src0, Color?[] cols, Mesh dst, bool keepT)
    {
        // Read/Write がオフのメッシュ（fbx の MiniMush など）は直接読めない（読むと空になる）→ 写しを作って読む
        var src = src0.isReadable ? src0 : Object.Instantiate(src0);
        try { BuildFrom(src, cols, dst, keepT); } finally { if (src != src0) Object.DestroyImmediate(src); }
    }
    static void BuildFrom(Mesh src, Color?[] cols, Mesh dst, bool keepT)
    {
        var v = src.vertices; var n = src.normals; var t = keepT ? src.tangents : new Vector4[0]; var oc = src.colors32;
        var uvs = new List<Vector4>[8]; var dim = new int[8];
        for (int c = 0; c < 8; c++)
        {
            var l = new List<Vector4>(); src.GetUVs(c, l); uvs[c] = l.Count == v.Length ? l : null;
            var attr = (UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + c);
            dim[c] = src.HasVertexAttribute(attr) ? src.GetVertexAttributeDimension(attr) : 0;
        }
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<Vector4>(); var C = new List<Color32>(); var U = new List<Vector4>[8];
        for (int c = 0; c < 8; c++) U[c] = uvs[c] != null ? new List<Vector4>() : null;
        var subs = new List<int[]>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var map = new Dictionary<int, int>(); var tri = src.GetIndices(s); var nt = new int[tri.Length];
            for (int k = 0; k < tri.Length; k++)
            {
                int o = tri[k];
                if (!map.TryGetValue(o, out int ni))
                {
                    ni = V.Count; map[o] = ni; V.Add(v[o]);
                    if (n.Length == v.Length) N.Add(n[o]);
                    if (t.Length == v.Length) T.Add(t[o]);
                    C.Add(cols[s].HasValue ? (Color32)cols[s].Value : (oc.Length == v.Length ? oc[o] : new Color32(255, 255, 255, 255)));
                    for (int c = 0; c < 8; c++) if (U[c] != null) U[c].Add(uvs[c][o]);
                }
                nt[k] = ni;
            }
            subs.Add(nt);
        }
        dst.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        dst.SetVertices(V);
        if (N.Count == V.Count) dst.SetNormals(N);
        if (T.Count == V.Count) dst.SetTangents(T);
        dst.SetColors(C);
        // uv は元と同じ次元で入れる（全部 4 次元にすると容量が倍になる）
        for (int c = 0; c < 8; c++)
        {
            if (U[c] == null) continue;
            if (dim[c] <= 2) dst.SetUVs(c, U[c].Select(q => new Vector2(q.x, q.y)).ToList());
            else if (dim[c] == 3) dst.SetUVs(c, U[c].Select(q => new Vector3(q.x, q.y, q.z)).ToList());
            else dst.SetUVs(c, U[c]);
        }
        dst.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) dst.SetIndices(subs[s], src.GetTopology(s), s);
        dst.bounds = src.bounds;
    }
}
