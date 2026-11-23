using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ミニキノコ（ゴールの妖精の輪・スタートのまわり・ロビーのクリアの印）を Blender で作ったキノコに差しかえる
//（2026-11-17 てじゃ「ゴールとかクリアフラグとかスタート地点のミニキノコ、さすがにシンプルすぎる」「Blenderがいいな」「ミニだけでいいね」
//  「できれば同一種の個体差くらいにとどめたい」「色は赤～黃でむらがあっていいよ」「イボはいらないよ」）。
// ・形：Models/MiniMush/MiniMush_A〜D（同じ種類：厚めの平たいかさ＋白いつば。背の高さ・軸の曲がり・かさの開きだけちがう）。Blender の minimush_build.py で作った
// ・1本ずつ Stem＋Cap（円柱＋つぶした球）を、Body（1つのメッシュ、材質は 軸／かさ／ひだ）に置きかえる。かさの幅は今のかさの幅に合わせる
// ・形・かさの色（赤〜黄の5段階）・向きは場所から決まる（何度やっても同じになる）
// ・Cap についていた見えなくなる距離（LODGroup）と、近くだけ影を出す設定（SmallShadows など FuwaFarShadows）は Body に引きつぐ
// 何度実行してもよい。コースのビルダー・クリアの印のビルダーで作り直したら、また実行する
public static class FuwaMiniMushrooms
{
    const string MeshDir = "Assets/FuwaCourse/Models/MiniMush";
    const string MatDir = "Assets/FuwaCourse/Materials/MiniMush";
    static readonly string[] Kinds = { "A", "B", "C", "D" };

    [MenuItem("FuwaCourse/よく使う/ミニキノコをBlender版にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static IEnumerable<Transform> Roots()
    {
        return Object.FindObjectsOfType<Transform>(true).Where(t =>
            (t.name == "FairyRing" && t.parent != null && t.parent.name == "Goal") ||
            (t.name == "Mushrooms" && t.parent != null && t.parent.name == "StartArea") ||
            t.name == "ClearMushrooms");
    }

    public static string Run()
    {
        var meshes = Kinds.Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/MiniMush_" + k + ".asset")).ToArray();
        // かさの色は前と同じパステル（赤・ピンク・クリーム）をまぜる（2026-11-17 てじゃ「リアリティはでたけどもとのパステルカラー感にもどすかぁ」。一度は赤〜黄の5段階にした）
        // 2026-11-17 てじゃ「オレンジ色も追加してみよ」：パステルのオレンジ（MiniMush_CapOrange）も
        var caps = new[] { "Mush_CapRed", "Mush_CapPink", "Mush_CapCream", "MiniMush_CapOrange" }.Select(FuwaLobbyPaths.FindMat).ToArray();
        var gill = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/MiniMush_Gill.mat");
        var stem = FuwaLobbyPaths.FindMat("Mush_Stem");
        if (meshes.Any(m => m == null) || caps.Any(m => m == null) || gill == null || stem == null) return "ミニキノコ：メッシュか材質が見つからない";

        RememberOwners();
        var shadows = Object.FindObjectsOfType<FuwaFarShadows>(true).ToList();
        var swap = new Dictionary<int, Renderer>();   // 古い Cap の InstanceID → 新しい Body
        int made = 0, roots = 0;
        foreach (var root in Roots().ToList())
        {
            roots++;
            // 色は1か所（輪・スタート・クリアの印）の中で同じくらいずつになるように配る（ばらばらに選ぶと、輪によってはオレンジが2本だけ等にかたよった。2026-11-17 てじゃ「オレンジすくないない？」）
            var kids = root.Cast<Transform>().ToList();
            var order = kids.OrderBy(t => Pick(AnimationUtility.CalculateTransformPath(t, null) + "#order", 1 << 30)).ToList();
            int off = Pick(AnimationUtility.CalculateTransformPath(root, null) + "#off", caps.Length);
            var colOf = new Dictionary<Transform, int>();
            for (int i = 0; i < order.Count; i++) colOf[order[i]] = (i + off) % caps.Length;
            foreach (Transform m in kids)
            {
                string path = AnimationUtility.CalculateTransformPath(m, null);
                int kind = Pick(path + "#kind", meshes.Length);
                int col = colOf[m];
                float yaw = Pick(path + "#yaw", 360);
                var done = m.Find("Body");
                if (done != null)
                {
                    // 差しかえ済み：形・色・向きだけ選び直す（かさの幅は今のまま）
                    var dm = done.GetComponent<MeshFilter>(); float w = done.localScale.x * dm.sharedMesh.bounds.size.z;
                    dm.sharedMesh = meshes[kind];
                    done.localScale = Vector3.one * (w / meshes[kind].bounds.size.z);
                    done.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    done.GetComponent<MeshRenderer>().sharedMaterials = new[] { stem, caps[col], gill };
                    var dg = done.GetComponent<LODGroup>(); if (dg != null) dg.RecalculateBounds();
                    EditorUtility.SetDirty(done.gameObject);
                    continue;
                }
                var cap = m.Find("Cap"); var st = m.Find("Stem");
                if (cap == null || st == null) continue;
                var capR = cap.GetComponent<MeshRenderer>();
                // 今のかさの幅（キノコの local の大きさ）
                var cb = capR.GetComponent<MeshFilter>().sharedMesh.bounds;
                float capW = Mathf.Max(cb.size.x * cap.localScale.x, cb.size.z * cap.localScale.z);
                var mesh = meshes[kind];
                float s = capW / mesh.bounds.size.z;   // メッシュの z の幅＝かさの直径（軸の曲がりは x）

                var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
                body.transform.SetParent(m, false);
                body.transform.localPosition = new Vector3(0f, st.localPosition.y - st.localScale.y, 0f);   // 軸の下のはし＝地面
                body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                body.transform.localScale = Vector3.one * s;
                body.layer = cap.gameObject.layer;
                GameObjectUtility.SetStaticEditorFlags(body, GameObjectUtility.GetStaticEditorFlags(cap.gameObject));
                body.GetComponent<MeshFilter>().sharedMesh = mesh;
                var br = body.GetComponent<MeshRenderer>();
                br.sharedMaterials = new[] { stem, caps[col], gill };
                br.shadowCastingMode = capR.shadowCastingMode; br.receiveShadows = capR.receiveShadows;
                br.lightProbeUsage = capR.lightProbeUsage; br.reflectionProbeUsage = capR.reflectionProbeUsage;
                var og = cap.GetComponent<LODGroup>();
                if (og != null)
                {
                    var g = body.AddComponent<LODGroup>();
                    g.fadeMode = og.fadeMode;
                    var lods = og.GetLODs();
                    g.SetLODs(new[] { new LOD(lods.Length > 0 ? lods[0].screenRelativeTransitionHeight : 0.02f, new Renderer[] { br }) });
                    g.RecalculateBounds();
                }
                swap[capR.GetInstanceID()] = br;
                Object.DestroyImmediate(cap.gameObject); Object.DestroyImmediate(st.gameObject);
                made++;
            }
        }
        // 影の切り替えの対象：Cap → Body、Stem は外す（消したので null になっている所を詰める）
        int sw = 0;
        foreach (var f in shadows)
        {
            if (f.targets == null) continue;
            var list = new List<Renderer>();
            foreach (var r in f.targets)
            {
                if (r == null) continue;   // さっき消した Stem・Cap（null になっている）
                list.Add(r);
            }
            // null になった所は上で消えたので、Body を足す（差しかえ前にこの FuwaFarShadows が持っていた Cap の分だけ）
            f.targets = list.ToArray();
            EditorUtility.SetDirty(f);
        }
        foreach (var f in shadows)
        {
            var have = new HashSet<Renderer>(f.targets ?? new Renderer[0]);
            var add = new List<Renderer>();
            foreach (var kv in swap) if (kv.Value != null && _owner.TryGetValue(kv.Key, out var owners) && owners.Contains(f.GetInstanceID()) && !have.Contains(kv.Value)) add.Add(kv.Value);
            if (add.Count == 0) continue;
            f.targets = f.targets.Concat(add).ToArray(); sw += add.Count;
            EditorUtility.SetDirty(f);
        }
        _owner.Clear();
        string lc = LobbyClearShadows();
        string rc = FuwaFarViewSetup.RefreshShadowCenters();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return "ミニキノコ：" + roots + "か所、" + made + "本を差しかえ（影の切り替えに " + sw + "）。" + lc + rc;
    }

    // 2026-11-23 てじゃ「ロビーのクリア済みのミニキノコに影つけると負荷どうかな」→ 小物と同じ「12m 以内だけ影」（SmallShadows）に入れる
    //  （形16種・材質1つでインスタンシングなので影の描く回数は少ない。Quest・スマホは品質設定で影なし）。SmallShadows はオフから始めて近づくとオンにする
    [MenuItem("FuwaCourse/ロビー/クリアのミニキノコに近くだけ影")]
    public static void LobbyClearShadowsMenu() { Debug.Log(LobbyClearShadows() + FuwaFarViewSetup.RefreshShadowCenters()); UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty(); }
    public static string LobbyClearShadows()
    {
        var small = Resources.FindObjectsOfTypeAll<FuwaFarShadows>().FirstOrDefault(f => f && f.name == "SmallShadows" && f.gameObject.scene.IsValid());
        if (!small) return "（SmallShadows がない）";
        var bodies = Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "ClearMushrooms" && (t.root.name == "FuwaLobby" || t.root.name == "FuwaStage2"))
            .SelectMany(t => t.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.name == "Body").ToList();
        var have = new HashSet<Renderer>(small.targets.Where(r => r));
        var t2 = small.targets.ToList(); var c2 = small.centers.ToList(); var r2 = small.radii.ToList(); int add = 0;
        foreach (var b in bodies)
        {
            b.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; EditorUtility.SetDirty(b);   // 近づいた時に SmallShadows がオンにする
            if (have.Contains(b)) continue;
            var bb = b.bounds; t2.Add(b); c2.Add(bb.center); r2.Add(bb.extents.magnitude); add++;
        }
        small.targets = t2.ToArray(); small.centers = c2.ToArray(); small.radii = r2.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(small); EditorUtility.SetDirty(small);
        return "ロビーのクリアのミニキノコ " + bodies.Count + "本を近くだけ影に（新しく " + add + "）。";
    }

    // 差しかえ前に、どの FuwaFarShadows がどの Cap を持っていたかを覚える（Run の最初に呼ぶ）
    static readonly Dictionary<int, HashSet<int>> _owner = new Dictionary<int, HashSet<int>>();
    public static void RememberOwners()
    {
        _owner.Clear();
        foreach (var f in Object.FindObjectsOfType<FuwaFarShadows>(true))
            if (f.targets != null)
                foreach (var r in f.targets)
                    if (r != null && r.name == "Cap")
                    {
                        if (!_owner.TryGetValue(r.GetInstanceID(), out var set)) _owner[r.GetInstanceID()] = set = new HashSet<int>();
                        set.Add(f.GetInstanceID());
                    }
    }

    // 場所の名前から 0〜n-1 を決める（FNV-1a ＋ かき混ぜ。前の単純なハッシュは近い名前で同じ色にかたよった）
    static int Pick(string s, int n)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
            return (int)(h % (uint)n);
        }
    }
}
