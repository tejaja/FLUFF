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
    // D（ふちが反った平たい傘）はやめた（2026-11-17 てじゃ「平たいやつあんまいらないな」）。メッシュは残してある
    // 2026-11-18 てじゃ「（おまけ本島のキノコと）この考え方できのこのたにも、つくりなおそ」「A（飾りの巨大キノコ）」：
    //  ベニテングタケ風（きれいな楕円のかさ・裏はなめらか・垂れるつば・ぷっくり根元）に。Blender の bigmush_build.py を軸だけ／かさだけで作り
    //  （FLUFF_models.blend、Islets の giant2_params、コレクション GiantMush2）、Tools/giantmush2.json → GiantStem2_A〜C／GiantCap2_A〜C。
    //  かさは A まるい若い／B 中くらい／C ひらいた。前の GiantStem_／GiantCap_ は残してある
    static readonly string[] Kinds = { "2_A", "2_B", "2_C" };
    [System.Serializable] class JItem { public string name; public float[] v, n, uv; public int[] s0, s1, s2; }
    [System.Serializable] class JFile { public JItem[] items; }
    static void ImportJson()
    {
        var path = System.IO.Path.Combine(Application.dataPath, "../Tools/giantmush2.json");
        if (!System.IO.File.Exists(path)) return;
        var f = JsonUtility.FromJson<JFile>(System.IO.File.ReadAllText(path));
        foreach (var it in f.items)
        {
            string ap = MeshDir + "/" + it.name + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(ap); bool isNew = !m;   // CopySerialized はサブメッシュが消えるので直接入れ直す
            if (isNew) m = new Mesh(); else m.Clear();
            m.name = it.name; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var vs = new Vector3[it.v.Length / 3]; var ns = new Vector3[vs.Length];
            for (int i = 0; i < vs.Length; i++) { vs[i] = new Vector3(it.v[i * 3], it.v[i * 3 + 1], it.v[i * 3 + 2]); ns[i] = new Vector3(it.n[i * 3], it.n[i * 3 + 1], it.n[i * 3 + 2]); }
            m.vertices = vs; m.normals = ns;
            if (it.uv != null && it.uv.Length == vs.Length * 2) { var us = new Vector2[vs.Length]; for (int i = 0; i < us.Length; i++) us[i] = new Vector2(it.uv[i * 2], it.uv[i * 2 + 1]); m.uv = us; }
            var subs = new[] { it.s0, it.s1 }.Where(x => x != null && x.Length > 0).ToArray();
            m.subMeshCount = subs.Length; for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(m, ap); else EditorUtility.SetDirty(m);
        }
    }

    [MenuItem("FuwaCourse/よく使う/きのこのたにの飾りの巨大キノコをBlender版にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        // メッシュ（取り込んだ fbx から、あれば上書き）
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Models", "GiantMush");
        // 取り込みのたびに GiantMush_1.fbx のように名前が増えるので、いちばん新しいもの
        string srcPath = System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(Src), "GiantMush*.fbx")
            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc).Select(p => p.Replace("\\", "/")).FirstOrDefault() ?? Src;
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
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
        ImportJson();
        var stems = Kinds.Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/GiantStem" + k + ".asset")).ToArray();
        var caps = Kinds.Select(k => AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/GiantCap" + k + ".asset")).ToArray();
        if (stems.Any(m => m == null) || caps.Any(m => m == null)) return "巨大キノコ：メッシュがない（先に Blender から取り込む）";

        var colors = new[]
        {
            FuwaLobbyPaths.FindMat("Mush_CapRed"),
            FuwaLobbyPaths.FindMat("MiniMush_CapOrange"),
            Mat("GiantMush_CapBrown", new Color(0.74f, 0.53f, 0.38f)),
            Mat("GiantMush_CapBlue", new Color(0.55f, 0.72f, 0.96f)),
            Mat("GiantMush_CapYellow", new Color(0.99f, 0.84f, 0.38f)),   // てじゃ「やっぱ黄色も欲しい」
        };
        colors = colors.Select(RimMat).ToArray();   // ふちのグラデつき（スタートのキノコと同じ感じ）
        var gill = UnderMat();   // 2026-11-18 裏のヒダのすじ模様（てじゃ「これをいい感じに目立たせられないもんかな」）
        var stemMat = FuwaLobbyPaths.FindMat("Mush_Stem");

        var root = GameObject.Find("FuwaCourse5").transform.Find("DecoGiantMushrooms");
        var list = root.Cast<Transform>().ToList();
        // 色は同じくらいずつ（並びはばらばら）
        var order = list.OrderBy(t => Pick(t.GetSiblingIndex() + "#order", 1 << 30)).ToList();
        var kindOrder = list.OrderBy(t => Pick(t.GetSiblingIndex() + "#kindorder", 1 << 30)).ToList();
        int n = 0;
        foreach (var m in list)
        {
            var st = m.Find("Stem"); var cp = m.Find("Cap"); if (st == null || cp == null) continue;
            string key = m.GetSiblingIndex().ToString();
            // 形の割り振り：2026-11-18 てじゃ「もうちょい平べったいのを多めに」→ 平たい（2_C）を半分、中くらい（2_B）3、まるい（2_A）3 の割合で
            int[] mix = { 2, 1, 2, 0, 2, 1, 2, 0, 2, 1, 2, 0 };
            int kind = mix[kindOrder.IndexOf(m) % mix.Length];
            // 1本ずつの指定（2026-11-17 てじゃ「もとのとんがってるのに戻し、かわりにその手前の茶色いとんがりを赤いドームに、一番手前の赤いやや平たいやつを茶色に」）
            //  1番目（(-42, 4, -32) 茶のベル）→ 赤のドーム、0番目（(-28, -1, -27) 赤の平たい）→ 茶
            int colorIdx = order.IndexOf(m) % colors.Length;
            if (m.GetSiblingIndex() == 1) { kind = 0; colorIdx = 0; }   // 2026-11-18 ドーム→ 新しい形ではまるい若いかさ（2_A）
            if (m.GetSiblingIndex() == 0) colorIdx = 2;
            // てじゃ「左のトンガリをオレンジに、右奥の平らなのを赤に」：7番目（(-44, 8, -84) 赤のベル）→ オレンジ、10番目（(-44, -6, -23) 青の平たい）→ 赤
            if (m.GetSiblingIndex() == 7) colorIdx = 1;
            if (m.GetSiblingIndex() == 10) colorIdx = 0;
            var stemMesh = stems[kind]; var capMesh = caps[kind];
            st.GetComponent<MeshFilter>().sharedMesh = stemMesh;
            st.GetComponent<MeshRenderer>().sharedMaterials = new[] { stemMat };
            cp.GetComponent<MeshFilter>().sharedMesh = capMesh;
            cp.GetComponent<MeshRenderer>().sharedMaterials = new[] { colors[colorIdx], gill };
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
            cp.localScale = Vector3.one * (4.25f * oldCap * 1.25f * size / capR);   // 1.1→1.25（2026-11-17 てじゃ「全体的にもうちょい傘の直径おおきく」）
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

    // かさの裏：放射状のすじ模様（Textures/MushGillStripes.png、UV の u がまわりの角度）。おまけ本島の大キノコも同じ材質
    public static Material UnderMat()
    {
        const string p = "Assets/FuwaCourse/Materials/MiniMush/BigMush_Under.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(FuwaLobbyPaths.FindMat("Mush_Gill")); AssetDatabase.CreateAsset(m, p); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/MushGillStripes.png");
        var ti = AssetImporter.GetAtPath("Assets/FuwaCourse/Textures/MushGillStripes.png") as TextureImporter;
        if (ti && (ti.wrapModeU != TextureWrapMode.Repeat || ti.wrapModeV != TextureWrapMode.Clamp)) { ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", new Vector2(GillStripes, 1f)); m.SetTextureOffset("_MainTex", Vector2.zero);
        EditorUtility.SetDirty(m); return m;
    }
    public static float GillStripes = 20f;

    // かさの表のふちのグラデ（2026-11-18 てじゃ「このキノコ（スタートのキノコ）のフチ側にグラデはいってるけど、デカいやつにもこんなかんじの」）。
    //  スタートのキノコの MushCapRimGrad と同じく、まん中〜0.75 はもとの色、ふちへ向かって少しあたたかく明るい色へ。色ごとにテクスチャと材質を作る
    //  （UV の u＝まん中0→ふち1。UV のないメッシュは使わない材質なので、ミニキノコ等の材質はそのまま）
    public static Material RimMat(Material src)
    {
        if (src == null) return null;
        string name = "BigMush_Cap_" + src.name, mp = "Assets/FuwaCourse/Materials/MiniMush/" + name + ".mat", tp = "Assets/FuwaCourse/Textures/" + name + "_Rim.png";
        Color b = src.HasProperty("_Color") ? src.color : Color.white;
        Color.RGBToHSV(b, out float h, out float sat, out float val);
        var rim = Color.HSVToRGB(Mathf.Repeat(h + RimHue, 1f), Mathf.Clamp01(sat * (1f - RimDesat)), Mathf.Clamp01(val + RimBright));
        var tex = new Texture2D(64, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 64; x++)
        {
            float u = x / 63f, k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RimStart, 1f, u));
            var c = Color.Lerp(b, rim, k); c.a = 1f;
            for (int y = 0; y < 4; y++) tex.SetPixel(x, y, c);
        }
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", tp), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(tp);
        var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
        if (ti) { ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, mp); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.name = name; m.color = Color.white;
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.SetTextureScale("_MainTex", Vector2.one); m.SetTextureOffset("_MainTex", Vector2.zero);
        EditorUtility.SetDirty(m); return m;
    }
    public static float RimStart = 0.72f, RimHue = 0.025f, RimDesat = 0.08f, RimBright = 0.05f;   // 波打ち（40 分割の1つおき）と同じ本数

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
