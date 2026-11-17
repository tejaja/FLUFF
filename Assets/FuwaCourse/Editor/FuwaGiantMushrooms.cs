using System.Linq;
using UnityEditor;
using UnityEngine;

// きのこのたに（FuwaCourse5）の飾りの巨大キノコ（DecoGiantMushrooms）を Blender で作ったキノコに差しかえる
//（2026-11-17 てじゃ「きのこのたにのデカキノコもちょっといいかんじにするかあ」「飾りのだけね」「ここは別種あってもいいかもね」
//  「色もオレンジもふやそ」「茶とか青とか」「ピンククリームを消してそっちふやすか」「傘の大きさとか角度にもムラをつけたいね」）。
// ・形：Models/GiantMush/GiantStem_A〜D（軸：ゆるくカーブ・根元が太い、A と C はフリルのつば）と GiantCap_A〜D（A 平たい／B ベル型／C ドーム／D ふちが反った傘。裏はひだ）
//   Blender のテキスト giantmush_build.py（シーンの "giant_params"）で作った
// ・色：赤・オレンジ・茶・パステル青を同じくらいずつ。かさの大きさ（±15%）・傾き（5〜15°）・形は場所から決まる（何度やっても同じ）
// ・島・位置・軸の大きさは今のまま。光は焼いた光（lightmap）ではなくライトプローブで受ける（形が変わって前の焼きが合わないので）
// 足場のキノコ（FuwaCourse5/Mushrooms）は変えない
public static class FuwaGiantMushrooms
{
    const string Src = "Assets/Generated/Imported/GiantMush.fbx";
    const string MeshDir = "Assets/FuwaCourse/Models/GiantMush";
    const string MatDir = "Assets/FuwaCourse/Materials/MiniMush";
    static readonly string[] Kinds = { "A", "B", "C", "D" };

    [MenuItem("FuwaCourse/よく使う/きのこのたにの飾りの巨大キノコをBlender版にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        // メッシュ（取り込んだ fbx から、あれば上書き）
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Models", "GiantMush");
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Src);
        if (go != null)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.sharedMesh; string p = MeshDir + "/" + mf.transform.name + ".asset";
                var ex = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                if (ex == null) { ex = new Mesh(); AssetDatabase.CreateAsset(ex, p); }
                ex.Clear(); ex.name = mf.transform.name;
                ex.SetVertices(src.vertices); ex.SetNormals(src.normals); ex.subMeshCount = src.subMeshCount;
                for (int i = 0; i < src.subMeshCount; i++) ex.SetTriangles(src.GetTriangles(i), i);
                ex.RecalculateBounds(); EditorUtility.SetDirty(ex);
            }
        var stems = Kinds.Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/GiantStem_" + k + ".asset")).ToArray();
        var caps = Kinds.Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/GiantCap_" + k + ".asset")).ToArray();
        if (stems.Any(m => m == null) || caps.Any(m => m == null)) return "巨大キノコ：メッシュがない（先に Blender から取り込む）";

        var colors = new[]
        {
            FuwaLobbyPaths.FindMat("Mush_CapRed"),
            FuwaLobbyPaths.FindMat("MiniMush_CapOrange"),
            Mat("GiantMush_CapBrown", new Color(0.74f, 0.53f, 0.38f)),
            Mat("GiantMush_CapBlue", new Color(0.55f, 0.72f, 0.96f)),
        };
        var gill = FuwaLobbyPaths.FindMat("Mush_Gill");
        var stemMat = FuwaLobbyPaths.FindMat("Mush_Stem");

        var root = GameObject.Find("FuwaCourse5").transform.Find("DecoGiantMushrooms");
        var list = root.Cast<Transform>().ToList();
        // 色は同じくらいずつ（並びはばらばら）
        var order = list.OrderBy(t => Pick(t.GetSiblingIndex() + "#order", 1 << 30)).ToList();
        int n = 0;
        foreach (var m in list)
        {
            var st = m.Find("Stem"); var cp = m.Find("Cap"); if (st == null || cp == null) continue;
            string key = m.GetSiblingIndex().ToString();
            int kind = Pick(key + "#kind", Kinds.Length);
            var stemMesh = stems[kind]; var capMesh = caps[kind];
            st.GetComponent<MeshFilter>().sharedMesh = stemMesh;
            st.GetComponent<MeshRenderer>().sharedMaterials = new[] { stemMat };
            cp.GetComponent<MeshFilter>().sharedMesh = capMesh;
            cp.GetComponent<MeshRenderer>().sharedMaterials = new[] { colors[order.IndexOf(m) % colors.Length], gill };
            // 軸のてっぺん（いちばん上の輪の中心）にかさを乗せる
            var vs = stemMesh.vertices; float top = vs.Max(v => v.y);
            var ring = vs.Where(v => v.y > top - 0.05f).ToArray();
            var c = Vector3.zero; foreach (var v in ring) c += v; c /= ring.Length;
            var topLocal = st.localPosition + st.localRotation * Vector3.Scale(c, st.localScale);
            // かさの大きさ・傾きのむら
            float size = 0.85f + 0.30f * Pick(key + "#size", 1000) / 999f;
            // かさの傾きは軸のてっぺんの向きに合わせる（2026-11-17 てじゃ「軸の傾きと傘の傾きはあわせてほしい」。前はばらばらに 5〜15°）
            float step = top / 16f;   // 軸の輪の間隔（Blender で 16 段）
            var below = vs.Where(v => v.y > top - step * 1.5f && v.y < top - step * 0.5f).ToArray();
            var cb = Vector3.zero; foreach (var v in below) cb += v; cb /= Mathf.Max(1, below.Length);
            var axis = st.localRotation * Vector3.Scale(c - cb, st.localScale);
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.up;
            float dir = Pick(key + "#dir", 360);
            // かさの半径は前のかさ（GiantCapDeco 半径 4.25 × 前の倍率。前の倍率は軸の太さから：細いのは同じ、太いのはそれより大きめだった）の 1.1 倍くらい
            float sx = st.localScale.x, oldCap = sx <= 1f ? sx : 1f + (sx - 1f) * 1.9f;
            float capR = capMesh.bounds.extents.x;
            cp.localScale = Vector3.one * (4.25f * oldCap * 1.1f * size / capR);
            cp.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized) * Quaternion.AngleAxis(dir, Vector3.up);
            cp.localPosition = topLocal - cp.localRotation * Vector3.up * 0.05f;
            // 焼いた光の代わりにライトプローブ
            foreach (var r in new[] { st.GetComponent<MeshRenderer>(), cp.GetComponent<MeshRenderer>() })
            {
                var f = GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~StaticEditorFlags.ContributeGI;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, f);
                r.receiveGI = ReceiveGI.LightProbes; r.lightmapIndex = -1;
                EditorUtility.SetDirty(r);
            }
            n++;
        }
        AssetDatabase.SaveAssets();
        string rc = FuwaFarViewSetup.RefreshShadowCenters();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "巨大キノコ：" + n + "本。" + rc;
    }

    static Material Mat(string name, Color c)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(FuwaLobbyPaths.FindMat("Mush_CapRed")); AssetDatabase.CreateAsset(m, p); }
        m.color = c; EditorUtility.SetDirty(m);
        return m;
    }

    static int Pick(string s, int n)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char ch in "giant#" + s) { h ^= ch; h *= 16777619; }
            h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
            return (int)(h % (uint)n);
        }
    }
}
