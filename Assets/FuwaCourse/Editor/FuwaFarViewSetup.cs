using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 遠くから見た時の軽量化（ロビー＋ひとりでのコース）。
// ・飾り（草・花・ツタ・小石・小さいキノコの輪・看板・モンスターの目）は、画面上で小さくなったら描かない（LODGroup）
//   風の道・風車・足場・モンスターの体などギミックは消さない
// ・ポリゴンの多い物（光るキノコ・角を丸めた柵など）は、遠くでは間引いた軽い形（子の「〜_Far」）に切り替える
// ・影を落とす物は全部 FuwaFarShadows に登録して、自分から遠い物は影を切る
// コースを作り直したらもう一度実行する（何度実行しても同じ結果になる）
public static class FuwaFarViewSetup
{
    static readonly string[] Areas = { "FuwaLobby", "FuwaCourse", "FuwaCourse0", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5" };

    // 遠くで消す飾り（親をたどった名前のどこかに当てはまれば対象）
    static readonly Regex DecoPath = new Regex(@"(^|/)(Deco|FairyRing|StartArea/.*/(Cap|Stem)$|Label|Panel|EyeWhite|Pupil|FloorShadow)(/|$|\d)");
    // 画面の高さに対する割合がこれより小さくなったら消す
    const float CullHeight = 0.01f;

    // 軽い形を作る対象：これ以上の三角形数で、いちばん長い辺がこれ以下（道のような長い物は、上にいても遠い扱いになるので除く）
    const int HeavyTris = 4000;
    const float HeavyMaxSize = 20f;
    // 間引きの細かさ(m)。これより近い頂点はまとめる
    internal const float FarCell = 0.05f;
    // 「物の太さ」に対する画面の割合がこれより小さくなったら軽い形にする
    const float FarHeight = 0.04f;
    const string FarDir = "Assets/FuwaCourse/Generated/Far";
    const string FarSuffix = "_Far";

    [MenuItem("FuwaCourse/済み（押す前に相談）/遠景の軽量化を設定")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = new Dictionary<string, GameObject>();
        foreach (var g in scene.GetRootGameObjects()) roots[g.name] = g;
        var arrayRefs = new List<(SerializedObject, string)>();
        var scripted = ScriptReferenced(scene, arrayRefs);

        int lod = 0, far = 0;
        var made = new Dictionary<Mesh, Mesh>();
        var farOf = new Dictionary<Renderer, Renderer>();
        var eyes = new Dictionary<Transform, List<Renderer>>();
        foreach (var a in Areas)
        {
            if (!roots.TryGetValue(a, out var root)) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r.name.EndsWith(FarSuffix)) continue;
                // かぜのおかのたんぽぽは島ごとの LODGroup（DandelionLOD：近い＝ふつう／遠い＝板）で切りかえるので、ここでは触らない（2026-10-16）
                if (r.GetComponentsInParent<LODGroup>(true).Any(g => g.name == "DandelionLOD")) continue;
                // 名場面ボード（かってにスクショ）は FuwaPhotoAlbumBuilder.OneLod でボードごとまとめて消すので、部品ごとには付けない（2026-10-12）
                if (r.GetComponentsInParent<Transform>(true).Any(t => t.name == "PhotoGallery")) continue;
                // モンスターの目（白目・黒目）は目ごとにまとめて消す。1つずつだと小さい黒目が先に消えて白目をむく（2026-10-09）
                if ((r.name == "EyeWhite" || r.name == "Pupil") && r.transform.parent != null && r.transform.parent.name == "Eyes")
                {
                    if (!eyes.TryGetValue(r.transform.parent, out var list)) eyes[r.transform.parent] = list = new List<Renderer>();
                    list.Add(r); continue;
                }
                if (IsDeco(r.transform, root.transform)) { SetCull(r); lod++; }
                // チュートリアルの見本（FuwaDemoStand の中）は遠くでは見た目ごと消すので、軽い形はいらない（2026-10-11 てじゃ）
                else if (r is MeshRenderer dm && r.GetComponentInParent<FuwaDemoStand>(true) != null) RemoveFar(dm);
                else if (r is MeshRenderer mr && IsHeavy(r, scripted))
                {
                    var f = SetFar(mr, made);
                    if (f != null) { farOf[r] = f; far++; }
                }
            }
        }
        foreach (var kv in eyes)
        {
            foreach (var r in kv.Value) { var old = r.GetComponent<LODGroup>(); if (old != null) Undo.DestroyObjectImmediate(old); }
            var g = kv.Key.GetComponent<LODGroup>();
            if (g == null) g = Undo.AddComponent<LODGroup>(kv.Key.gameObject); else Undo.RecordObject(g, "LOD");
            g.fadeMode = LODFadeMode.None;
            g.SetLODs(new[] { new LOD(CullHeight, kv.Value.ToArray()) });
            g.RecalculateBounds();
            lod++;
        }
        // スクリプトの Renderer[]（チェックポイントで光らせる物など）に元の物が入っていたら、軽い形も足す
        foreach (var (so, path) in arrayRefs)
        {
            so.Update();
            var arr = so.FindProperty(path);
            // 外した軽い形が入っていた所（空になった要素）は詰める。Udon で null を触ると止まるので
            for (int i = arr.arraySize - 1; i >= 0; i--)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null && arr.GetArrayElementAtIndex(i).objectReferenceInstanceIDValue != 0)
                    arr.DeleteArrayElementAtIndex(i);
            var have = new HashSet<Object>();
            for (int i = 0; i < arr.arraySize; i++) have.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);
            int n0 = arr.arraySize;
            for (int i = 0; i < n0; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue is Renderer src && farOf.TryGetValue(src, out var fr) && !have.Contains(fr))
                {
                    arr.InsertArrayElementAtIndex(arr.arraySize);
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fr;
                    have.Add(fr);
                }
            so.ApplyModifiedProperties();
        }

        var shadowTargets = new List<Renderer>();
        foreach (var a in Areas)
        {
            if (!roots.TryGetValue(a, out var root)) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) && r.shadowCastingMode == ShadowCastingMode.On) shadowTargets.Add(r);
        }

        var holder = roots.TryGetValue("FuwaSystem", out var sys) ? sys.transform.Find("FarShadows") : null;
        if (holder == null)
        {
            holder = new GameObject("FarShadows").transform;
            holder.SetParent(sys != null ? sys.transform : null, false);
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "FarShadows");
        }
        var fs = holder.GetComponent<FuwaFarShadows>();
        if (fs == null) fs = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaFarShadows>(holder.gameObject);
        Undo.RecordObject(fs, "FarShadows");
        fs.targets = shadowTargets.ToArray();
        fs.centers = new Vector3[fs.targets.Length];
        fs.radii = new float[fs.targets.Length];
        for (int i = 0; i < fs.targets.Length; i++)
        {
            Bounds b = fs.targets[i].bounds;
            fs.centers[i] = b.center;
            fs.radii[i] = b.extents.magnitude;
        }
        EditorUtility.SetDirty(fs);
        // ほかの影の切り替え（SmallShadows など、別に作った物）も、物の位置を今の位置に合わせ直す。
        // 位置が古いままだと、動かした物（れんしゅうのゴールのキノコなど）がずっと「遠い」扱いになって影が出なかった（2026-10-08）
        foreach (var other in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            if (other == fs || other.targets == null) continue;
            Undo.RecordObject(other, "FarShadows");
            var keep = other.targets.Where(r => r != null).ToArray();
            other.targets = keep;
            other.centers = new Vector3[keep.Length];
            other.radii = new float[keep.Length];
            for (int i = 0; i < keep.Length; i++) { Bounds b = keep[i].bounds; other.centers[i] = b.center; other.radii[i] = b.extents.magnitude; }
            EditorUtility.SetDirty(other);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[遠景] 飾りのLOD {lod}個、軽い形への切り替え {far}個、影を切る対象 {fs.targets.Length}個");
    }

    // 影の切り替え（FuwaFarShadows：FarShadows・SmallShadows など全部）の「物の位置」だけを今の位置に合わせ直す。対象の顔ぶれは変えない。
    // コースを引っ越した後に位置が古いままで、目の前の物まで「遠い」扱いで影が消えていた
    //（2026-10-13 てじゃ「コースのきのこだけ傘の下のヒラヒラが明るい」：FarShadows 633個のうち535個が古い位置、最大163mずれ）。
    // 物を動かしたらこれを実行する。非表示の物も、メッシュの大きさから位置を出す
    [MenuItem("FuwaCourse/よく使う/影の切り替えの位置を今の位置に合わせ直す")]
    public static void RefreshShadowCentersMenu() { Debug.Log(RefreshShadowCenters()); }

    public static string RefreshShadowCenters()
    {
        var log = new List<string>();
        foreach (var f in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            if (f.targets == null) continue;
            Undo.RecordObject(f, "FarShadows");
            var oldC = new Dictionary<Renderer, Vector3>();
            for (int i = 0; i < f.targets.Length && f.centers != null && i < f.centers.Length; i++) if (f.targets[i] != null) oldC[f.targets[i]] = f.centers[i];
            var keep = f.targets.Where(r => r != null).ToArray();
            int moved = 0;
            f.targets = keep;
            f.centers = new Vector3[keep.Length];
            f.radii = new float[keep.Length];
            for (int i = 0; i < keep.Length; i++)
            {
                var b = WorldBounds(keep[i]);
                f.centers[i] = b.center; f.radii[i] = b.extents.magnitude;
                if (!oldC.TryGetValue(keep[i], out var oc) || Vector3.Distance(oc, b.center) > 0.5f) moved++;
            }
            EditorUtility.SetDirty(f);
            log.Add(f.name + " " + keep.Length + "個（位置が変わった " + moved + "）");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return "影の切り替えの位置: " + string.Join(" / ", log);
    }

    // 非表示（renderer.bounds が空になる）でも、メッシュの大きさをワールドに直して使う
    static Bounds WorldBounds(Renderer r)
    {
        if (r.enabled && r.gameObject.activeInHierarchy) return r.bounds;
        Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
        if (m == null) return r.bounds;
        var lb = m.bounds; var l2w = r.transform.localToWorldMatrix;
        var b = new Bounds(l2w.MultiplyPoint3x4(lb.center), Vector3.zero);
        for (int k = 0; k < 8; k++)
            b.Encapsulate(l2w.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
        return b;
    }

    static bool IsDeco(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (var c = t; c != null && c != root; c = c.parent) parts.Insert(0, c.name);
        return DecoPath.IsMatch(string.Join("/", parts));
    }

    static void SetCull(Renderer r)
    {
        var g = r.GetComponent<LODGroup>();
        if (g == null) g = Undo.AddComponent<LODGroup>(r.gameObject);
        else Undo.RecordObject(g, "LOD");
        g.fadeMode = LODFadeMode.None;
        g.SetLODs(new[] { new LOD(CullHeight, new[] { r }) });
        g.RecalculateBounds();
    }

    // スクリプトから触られている物（見た目を変えたり動かしたりする）は、軽い形に差し替えると動きがずれるので除く
    static HashSet<GameObject> ScriptReferenced(UnityEngine.SceneManagement.Scene scene, List<(SerializedObject, string)> arrayRefs)
    {
        var set = new HashSet<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is VRC.Udon.UdonBehaviour || mb is FuwaFarShadows) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.isArray && (it.arrayElementType == "PPtr<Renderer>" || it.arrayElementType == "PPtr<$Renderer>")) { arrayRefs.Add((so, it.propertyPath)); continue; }
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    if (it.propertyPath.Contains(".Array.data[") && it.objectReferenceValue is Renderer) continue;   // Renderer[] の中身は上で扱う
                    var o = it.objectReferenceValue;
                    GameObject go = o is GameObject g ? g : (o is Component c && !(c is MonoBehaviour) ? c.gameObject : null);
                    // エリアごと表示を切り替える参照（ルート）は数えない
                    if (go != null && go.transform.parent != null) set.Add(go);
                }
            }
        return set;
    }

    static int Tris(Mesh m)
    {
        long t = 0;
        for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
        return (int)t;
    }

    static bool IsHeavy(Renderer r, HashSet<GameObject> scripted)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;   // エディタでは読み取り不可のメッシュも読める
        if (Tris(mf.sharedMesh) < HeavyTris) return false;
        var s = r.bounds.size;
        if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) > HeavyMaxSize) return false;
        // 自分と親だけ見る（コースのまとまりごと参照されていることがあるので、それより上は見ない）
        if (scripted.Contains(r.gameObject)) return false;
        if (r.transform.parent != null && scripted.Contains(r.transform.parent.gameObject)) return false;
        return true;
    }

    static Renderer SetFar(MeshRenderer r, Dictionary<Mesh, Mesh> made)
    {
        var src = r.GetComponent<MeshFilter>().sharedMesh;
        var ls = r.transform.lossyScale;
        if (!made.TryGetValue(src, out var lite))
        {
            lite = Simplify(src, new Vector3(FarCell / Mathf.Abs(ls.x), FarCell / Mathf.Abs(ls.y), FarCell / Mathf.Abs(ls.z)));
            lite.name = src.name + FarSuffix;
            if (Tris(lite) > Tris(src) * 0.7f) { made[src] = null; RemoveFar(r); return null; }   // ほとんど減らないなら切り替えない
            // 間引いて穴（元にない開いた辺）ができたら使わない（光るキノコのかさが遠くで欠けて見えた）
            if (OpenEdges(lite) > OpenEdges(src)) { made[src] = null; RemoveFar(r); return null; }
            if (!AssetDatabase.IsValidFolder(FarDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Far");
            string path = FarDir + "/" + lite.name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(lite, old); lite = old; }
            else AssetDatabase.CreateAsset(lite, path);
            made[src] = lite;
        }
        if (lite == null) { RemoveFar(r); return null; }

        var child = r.transform.Find(r.name + FarSuffix);
        if (child == null)
        {
            child = new GameObject(r.name + FarSuffix).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "far");
            child.SetParent(r.transform, false);
        }
        child.gameObject.layer = r.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(child.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject));
        var mf = child.GetComponent<MeshFilter>();
        if (mf == null) mf = child.gameObject.AddComponent<MeshFilter>();
        var mr = child.GetComponent<MeshRenderer>();
        if (mr == null) mr = child.gameObject.AddComponent<MeshRenderer>();
        mf.sharedMesh = lite;
        mr.enabled = true;
        mr.sharedMaterials = r.sharedMaterials;
        mr.shadowCastingMode = r.shadowCastingMode;
        mr.receiveShadows = r.receiveShadows;
        mr.lightProbeUsage = r.lightProbeUsage;

        var g = r.GetComponent<LODGroup>();
        if (g == null) g = Undo.AddComponent<LODGroup>(r.gameObject);
        else Undo.RecordObject(g, "LOD");
        g.fadeMode = LODFadeMode.None;
        g.SetLODs(new[] { new LOD(FarHeight, new Renderer[] { r }), new LOD(0f, new Renderer[] { mr }) });
        g.RecalculateBounds();
        // 柵のように広くて低い物でも「太さ」で切り替わるように、大きさはいちばん短い辺の長さにする
        var s = r.bounds.size;
        g.size = Mathf.Max(0.5f, Mathf.Min(s.x, Mathf.Min(s.y, s.z))) / Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))));
        return mr;
    }

    // 前に作った軽い形を使わないようにする（切り替えの LODGroup を外して、子の「〜_Far」は表示しない）。
    // 子そのものはチェックポイントの光らせる物・影焼きの一覧などから参照されているので消さない
    static void RemoveFar(MeshRenderer r)
    {
        var child = r.transform.Find(r.name + FarSuffix);
        if (child != null && child.TryGetComponent<MeshRenderer>(out var fmr)) { Undo.RecordObject(fmr, "far off"); fmr.enabled = false; }
        var g = r.GetComponent<LODGroup>();
        if (g != null && g.lodCount == 2) Undo.DestroyObjectImmediate(g);
    }

    // 開いた辺（三角形1枚にしか使われていない辺）の数。同じ位置の頂点は1つとみなす
    static int OpenEdges(Mesh m)
    {
        var v = m.vertices;
        var id = new int[v.Length];
        var weld = new Dictionary<Vector3, int>();
        for (int i = 0; i < v.Length; i++) { if (!weld.TryGetValue(v[i], out int k)) { k = weld.Count; weld[v[i]] = k; } id[i] = k; }
        var edges = new Dictionary<long, int>();
        for (int s = 0; s < m.subMeshCount; s++)
        {
            var t = m.GetTriangles(s);
            for (int i = 0; i + 2 < t.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    int a = id[t[i + j]], b = id[t[i + (j + 1) % 3]];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edges[key] = edges.TryGetValue(key, out int n) ? n + 1 : 1;
                }
        }
        int open = 0;
        foreach (var c in edges.Values) if (c == 1) open++;
        return open;
    }

    // 頂点を格子でまとめて間引く（向きが逆の面は別にまとめて、薄い物の表裏がつぶれないようにする）
    internal static Mesh Simplify(Mesh src, Vector3 cell)
    {
        var v = src.vertices; var n = src.normals; var uv = src.uv; var col = src.colors;
        bool hasN = n != null && n.Length == v.Length, hasUV = uv != null && uv.Length == v.Length, hasC = col != null && col.Length == v.Length;
        // 向きは「同じ位置の頂点をまとめた平均の法線」で決める。UVの継ぎ目などで同じ位置に頂点が2つあると、
        // 法線が少し違うだけで別々のまとまりに入ってずれ、すき間（欠け）ができていた（光るキノコのかさ）
        var posNormal = new Dictionary<Vector3, Vector3>();
        if (hasN) for (int i = 0; i < v.Length; i++) posNormal[v[i]] = (posNormal.TryGetValue(v[i], out var sum) ? sum : Vector3.zero) + n[i];
        var map = new Dictionary<(int, int, int, int), int>();
        var remap = new int[v.Length];
        var P = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var C = new List<Color>(); var cnt = new List<int>();
        for (int i = 0; i < v.Length; i++)
        {
            int dir = 0;
            if (hasN)
            {
                var a = posNormal[v[i]];
                if (a.sqrMagnitude < 0.25f) a = n[i];   // 表裏が同じ位置にある薄い物は、打ち消し合うので自分の法線で
                float ax = Mathf.Abs(a.x), ay = Mathf.Abs(a.y), az = Mathf.Abs(a.z);
                dir = ax >= ay && ax >= az ? (a.x > 0 ? 0 : 1) : ay >= az ? (a.y > 0 ? 2 : 3) : (a.z > 0 ? 4 : 5);
            }
            var key = (Mathf.FloorToInt(v[i].x / cell.x), Mathf.FloorToInt(v[i].y / cell.y), Mathf.FloorToInt(v[i].z / cell.z), dir);
            if (!map.TryGetValue(key, out int k))
            {
                k = P.Count; map[key] = k;
                P.Add(Vector3.zero); N.Add(Vector3.zero); U.Add(hasUV ? uv[i] : Vector2.zero); C.Add(hasC ? col[i] : Color.white); cnt.Add(0);
            }
            remap[i] = k;
            P[k] += v[i]; if (hasN) N[k] += n[i]; cnt[k]++;
        }
        for (int k = 0; k < P.Count; k++) { P[k] /= cnt[k]; N[k] = N[k].sqrMagnitude > 1e-12f ? N[k].normalized : Vector3.up; }

        var m = new Mesh { indexFormat = P.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.SetVertices(P);
        if (hasN) m.SetNormals(N);
        if (hasUV) m.SetUVs(0, U);
        if (hasC) m.SetColors(C);
        m.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var t = src.GetTriangles(s);
            var T = new List<int>(); var seen = new HashSet<(int, int, int)>();
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int a = remap[t[i]], b = remap[t[i + 1]], c = remap[t[i + 2]];
                if (a == b || b == c || a == c) continue;
                // 同じ三角形の重複を除く（回転した並びも同じとみなす）
                var key = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
                if (!seen.Add(key)) continue;
                T.Add(a); T.Add(b); T.Add(c);
            }
            m.SetTriangles(T, s);
        }
        m.RecalculateBounds();
        if (!hasN) m.RecalculateNormals();
        return m;
    }
}
