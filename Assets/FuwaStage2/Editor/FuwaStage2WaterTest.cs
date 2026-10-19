using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 水のギミックのテスト場（2026-10-17）。ステージ2のロビーの東 30m に、池 → 小川（流れ）→ 滝つぼ の小さなコースを置く。
// 触り心地を決めたら、本番のコース（2–1 みずのたに）に同じ部品で組む。テスト場はあとで消す
public static class FuwaStage2WaterTest
{
    const string RootName = "FuwaStage2_WaterTest";
    public static Vector3 Offset = new Vector3(30f, 0f, 0f);   // ステージ2のロビーから
    public static float WaterDepth = 0.6f, StreamSpeed = 1.6f;

    [MenuItem("FuwaCourse/ステージ2/水のテスト場を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "Lobby" && t.parent && t.parent.name == "FuwaStage2");
        if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;
        root.position = lobby.position + Offset;
        var ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_LobbyGround.mat");
        var bedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Lobby_Ring.mat");
        var water = WaterMat();
        _fx = MakeFx(root);
        // プレイヤーが水に入った時の「じゃぶ」（FuwaShallowWater がその人の所へ動かして鳴らす。1つを全部の水で使う）
        var ps0 = new GameObject("PlayerSplash"); ps0.transform.SetParent(root, false);
        _playerSplash = ps0.AddComponent<AudioSource>(); _playerSplash.playOnAwake = false; _playerSplash.spatialBlend = 1f;
        _playerSplash.minDistance = 2f; _playerSplash.maxDistance = 25f; _playerSplash.rolloffMode = AudioRolloffMode.Linear;
        _playerSplash.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_water_splash.wav");
        float y0 = 0.1f;   // ロビーの床の高さ（水面）

        // 人の足場：ロビーから池まで
        Box(root, "Walkway", new Vector3(-9f, y0 - 0.25f, -3f), new Vector3(14f, 0.5f, 2f), ground, 11, false);
        // 池（10×10、深さ WaterDepth）
        Pool(root, "Pool", new Vector3(0, 0, 0), new Vector3(10f, 0, 10f), y0, water, bedMat, ground, Vector3.zero, 0f);
        // 小川（幅3、長さ20、+X へ流れる）
        Pool(root, "Stream", new Vector3(15f, 0, 0), new Vector3(20f, 0, 3f), y0, water, bedMat, ground, Vector3.right, StreamSpeed);
        // 小川の横の人の道
        Box(root, "StreamPath", new Vector3(15f, y0 - 0.25f, -3.5f), new Vector3(20f, 0.5f, 2f), ground, 11, false);
        // 1.5m 下の滝つぼ（流れて落ちる）
        Pool(root, "Basin", new Vector3(30f, -1.5f, 0), new Vector3(10f, 0, 10f), y0 - 1.5f, water, bedMat, ground, Vector3.right, 0.5f);   // 滝つぼからゆっくり流れ出す
        Box(root, "BasinPath", new Vector3(30f, y0 - 1.5f - 0.25f, -6.5f), new Vector3(10f, 0.5f, 3f), ground, 11, false);
        // 小川の落ち口の滝（小川の終わり x=25 から滝つぼへ）
        Waterfall(root, "Falls", new Vector3(25.15f, 0, 0), y0 + 0.05f, y0 - 1.5f, 3f, Vector3.right);
        // 蓮の葉：足場から池の向こうへ飛び石のように
        var lilies = new GameObject("LilyPads").transform; lilies.SetParent(root, false);
        Vector3[] lp = { new Vector3(-3.2f, 0, -2.2f), new Vector3(-1.3f, 0, -1.0f), new Vector3(0.8f, 0, 0.3f), new Vector3(2.6f, 0, 1.8f) };
        float[] lr = { 0.85f, 0.75f, 0.9f, 0.8f };
        // 葉っぱの上は乗る所なので花は乗せない（2026-10-17 てじゃ「葉っぱから生えてるの変、でっかいのを葉っぱの脇に」）
        for (int i = 0; i < lp.Length; i++) LilyPad(lilies, "Lily" + i, new Vector3(lp[i].x, y0 + 0.07f, lp[i].z), lr[i], i * 67f + 20f, false);
        // 泡で上の段へ：池の北のふちに崖（高さ1.8m）、その上に小さい池。崖の手前の水底から泡の柱
        float up = 1.8f;
        TopOutWall(root, "Cliff", new Vector3(0, (y0 - WaterDepth + y0 + up + 0.35f) / 2f, 6.0f), new Vector3(10f, up + 0.35f + WaterDepth, 1.0f), ground);
        Pool(root, "UpperPool", new Vector3(0, up, 9.5f), new Vector3(8f, 0, 6f), y0 + up, water, bedMat, ground, Vector3.zero, 0f);
        BubbleColumn(root, "Bubbles", new Vector3(0, 0, 4.6f), y0 - WaterDepth, y0 + up + 0.75f, Vector3.forward);
        // 大きい蓮の花：水から茎で立ち上がって、葉っぱの脇に
        Vector3[] fp = { new Vector3(-4.3f, 0, -0.7f), new Vector3(0.1f, 0, 2.1f), new Vector3(3.8f, 0, -0.3f) };
        float[] fs = { 3.6f, 3.0f, 3.3f };
        for (int i = 0; i < fp.Length; i++) BigLotus(lilies, "BigLotus" + i, new Vector3(fp[i].x, y0 + 0.1f * fs[i], fp[i].z), fs[i], i * 83f + 10f);
        // テスト用のふわふわの置き場（池の真ん中の上）
        var spot = new GameObject("TestBallStart").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(-3f, y0 + 1.2f, 0f);
        // 押すとふわふわが出る台（テスト用、公開前に消す。2026-10-17 てじゃ「ふわふわがなくてためせない」）：桟橋の上に3つ並べる
        BallStand(root, "BallStand_Pool", new Vector3(-4.0f, y0, -3.6f), spot, "ふわふわ：池", new Color(0.55f, 0.8f, 1f));
        var sStream = new GameObject("TestBallStream").transform; sStream.SetParent(root, false); sStream.localPosition = new Vector3(6.5f, y0 + 1.0f, 0f);
        BallStand(root, "BallStand_Stream", new Vector3(6.5f, y0, -3.6f), sStream, "ふわふわ：小川→滝", new Color(0.45f, 0.65f, 1f));
        var sBubble = new GameObject("TestBallBubble").transform; sBubble.SetParent(root, false); sBubble.localPosition = new Vector3(0f, y0 + 1.0f, 3.3f);
        BallStand(root, "BallStand_Bubble", new Vector3(-6.0f, y0, -3.6f), sBubble, "ふわふわ：泡", new Color(0.8f, 0.95f, 1f));

        // ステージ2にいる時だけ表示
        var vis = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "FuwaAreaVisibility");
        if (vis)
        {
            var so = new SerializedObject(vis); var arr = so.FindProperty("stage2Objects");
            bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == root.gameObject) has = true;
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
            if (!has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject; }
            so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)vis);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[水のテスト場] " + root.position;
    }

    // 水の入れ物：底（地面＝ふわふわが触るとアウト。浮力で届かない深さ）、ふち（横は当たるだけ・上に乗るとアウト）、水（トリガー＋見た目）
    static void Pool(Transform root, string name, Vector3 c, Vector3 size, float surfaceY, Material water, Material bed, Material rim, Vector3 flowDir, float flowSpeed)
    {
        var g = new GameObject(name).transform; g.SetParent(root, false); g.localPosition = new Vector3(c.x, 0, c.z);
        float bedTop = surfaceY - WaterDepth;
        Box(g, "Bed", new Vector3(0, bedTop - 0.25f, 0), new Vector3(size.x, 0.5f, size.z), bed, 11, false);
        // ふち（流れの向きの両はしは開けておく＝流れ込み・流れ出し）
        bool alongX = Mathf.Abs(flowDir.x) > 0.5f;
        float rh = 0.35f;   // 水面からのふちの高さ
        float rimY = (bedTop + surfaceY + rh) / 2f, rimH = surfaceY + rh - bedTop;
        if (!(alongX))
        {
            TopOutWall(g, "RimE", new Vector3(size.x / 2 + 0.25f, rimY, 0), new Vector3(0.5f, rimH, size.z + 1f), rim);
            TopOutWall(g, "RimW", new Vector3(-size.x / 2 - 0.25f, rimY, 0), new Vector3(0.5f, rimH, size.z + 1f), rim);
        }
        TopOutWall(g, "RimN", new Vector3(0, rimY, size.z / 2 + 0.25f), new Vector3(size.x, rimH, 0.5f), rim);
        TopOutWall(g, "RimS", new Vector3(0, rimY, -size.z / 2 - 0.25f), new Vector3(size.x, rimH, 0.5f), rim);
        // 水（トリガー。水面より上にも少し伸ばして、跳ねた玉もつかまえる）
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube); w.name = "Water"; w.transform.SetParent(g, false);
        w.transform.localPosition = new Vector3(0, (bedTop + surfaceY) / 2f, 0); w.transform.localScale = new Vector3(size.x, surfaceY - bedTop, size.z);
        Object.DestroyImmediate(w.GetComponent<MeshRenderer>()); Object.DestroyImmediate(w.GetComponent<MeshFilter>());
        w.layer = 4;   // Water レイヤー
        var bc = w.GetComponent<BoxCollider>(); bc.isTrigger = true;
        // 水面の見た目：Quad を倒して、ローカル +Y を流れの向きに（シェーダーが筋を流す向き）
        var surf = GameObject.CreatePrimitive(PrimitiveType.Quad); surf.name = "Surface"; surf.transform.SetParent(g, false);
        Object.DestroyImmediate(surf.GetComponent<Collider>());
        float yaw = flowDir.sqrMagnitude > 0.001f ? Mathf.Atan2(flowDir.x, flowDir.z) * Mathf.Rad2Deg : 0f;
        surf.transform.localPosition = new Vector3(0, surfaceY, 0);
        surf.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
        bool along = Mathf.Abs(flowDir.x) > 0.5f;
        surf.transform.localScale = along ? new Vector3(size.z, size.x, 1f) : new Vector3(size.x, size.z, 1f);
        surf.GetComponent<Renderer>().sharedMaterial = WaterSurfaceMat(flowSpeed);
        var fw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWater>(w);
        fw.surface = surf.transform; fw.flowDirection = flowDir; fw.flowSpeed = flowSpeed; fw.fx = _fx;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fw);
        NoSync(fw);
        // 人には浅瀬（遅くなる・ふわっと跳ぶ）
        var sw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaShallowWater>(w);
        sw.fx = _fx; sw.splashSound = _playerSplash;
        sw.softClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_water_splash_soft.wav");
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sw);
        NoSync(sw);
    }
    static AudioSource _playerSplash;

    static void TopOutWall(Transform p, string name, Vector3 lp, Vector3 size, Material m)
    {
        var w = Box(p, name, lp, size, m, 0, false);
        var to = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTopOut>(w);
        if (to) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(to); NoSync(to); }
    }

    // 押すとふわふわが spot に出る台（テスト用）：細い柱＋上に色つきの玉。押す所は玉
    static void BallStand(Transform root, string name, Vector3 lp, Transform spot, string label, Color col)
    {
        var g = new GameObject(name).transform; g.SetParent(root, false); g.localPosition = lp;
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); post.name = "Post"; post.transform.SetParent(g, false);
        post.transform.localPosition = new Vector3(0, 0.45f, 0); post.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
        Object.DestroyImmediate(post.GetComponent<Collider>());
        post.GetComponent<Renderer>().sharedMaterial = ToonMat("Stage2_TestPost", new Color(0.9f, 0.9f, 0.92f));
        var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere); knob.name = "Button"; knob.transform.SetParent(g, false);
        knob.transform.localPosition = new Vector3(0, 1.0f, 0); knob.transform.localScale = Vector3.one * 0.22f;
        knob.GetComponent<Renderer>().sharedMaterial = ToonMat("Stage2_TestButton_" + name, col);
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(b => b.transform.parent && b.transform.parent.name == "FuwaSystem") ?? Object.FindObjectOfType<FuwaBall>(true);
        var s = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTestBallSpot>(knob);
        s.ball = ball; s.spot = spot; s.stage = 2;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(s); NoSync(s);
        var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(s); if (ub) { ub.interactText = label; EditorUtility.SetDirty(ub); }
    }

    // エディタから足した U# は同期が Continuous のままになる（memory の罠）→ 同期しないに
    static void NoSync(UdonSharp.UdonSharpBehaviour b)
    {
        var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        if (ub) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
    }

    static GameObject Box(Transform p, string name, Vector3 lp, Vector3 size, Material m, int layer, bool trigger)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false);
        g.transform.localPosition = lp; g.transform.localScale = size; g.layer = layer;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        g.GetComponent<BoxCollider>().isTrigger = trigger;
        return g;
    }

    static FuwaWaterFx _fx;

    // 蓮の葉：切れ込みのある丸い葉（まん中が少し盛り上がる）。人が乗れる当たり＋乗ったかを見るトリガー＋ FuwaLilyPad
    public static GameObject LilyPad(Transform parent, string name, Vector3 lp, float radius, float yaw, bool flower)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(0, yaw, 0); g.transform.localScale = Vector3.one * radius;
        var mesh = LilyMesh();
        // 見た目は子の Leaf（ゆらゆらは見た目だけ。当たり判定は動かない。2026-10-19 てじゃ「蓮はただの固定でいいかも」）
        var leaf = new GameObject("Leaf"); leaf.transform.SetParent(g.transform, false);
        leaf.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = leaf.AddComponent<MeshRenderer>(); mr.sharedMaterial = LilyMat();
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true;
        var tr = g.AddComponent<BoxCollider>(); tr.isTrigger = true; tr.center = new Vector3(0, 0.35f / radius, 0); tr.size = new Vector3(1.7f, 0.5f / radius, 1.7f);
        if (flower)
        {
            // 蓮の花（Blender：FLUFF_models.blend の Stage2Water シーン → Models/Stage2Water.fbx）。葉っぱのふち寄りに
            var lotusMesh = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaStage2/Models/Stage2Water.fbx").OfType<Mesh>().FirstOrDefault(m => m.name == "LotusFlower");
            if (lotusMesh)
            {
                var f = new GameObject("Lotus"); f.transform.SetParent(leaf.transform, false);
                f.transform.localPosition = new Vector3(0.35f, 0.02f, -0.25f); f.transform.localRotation = Quaternion.Euler(0, 40, 0);
                f.transform.localScale = Vector3.one * (1.15f / radius);
                f.AddComponent<MeshFilter>().sharedMesh = lotusMesh;
                f.AddComponent<MeshRenderer>().sharedMaterials = new[] { ToonMat("Stage2_LotusCenter", new Color(0.98f, 0.80f, 0.32f)), ToonMat("Stage2_LotusOuter", new Color(0.95f, 0.50f, 0.68f)),
                    ToonMat("Stage2_LotusInner", new Color(0.99f, 0.70f, 0.82f)), ToonMat("Stage2_LotusCore", new Color(1f, 0.88f, 0.93f)) };
            }
        }
        var pad = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaLilyPad>(g);
        pad.radius = radius; pad.visual = leaf.transform;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pad); NoSync(pad);
        return g;
    }

    // 半径1の葉。切れ込み（25度）はローカル +Z 側。上の面はまん中が 0.04 盛り上がる、厚み 0.035
    static Mesh LilyMesh()
    {
        const string p = "Assets/FuwaStage2/Generated/LilyPad.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (m) return m;
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Generated")) AssetDatabase.CreateFolder("Assets/FuwaStage2", "Generated");
        int seg = 72; float notch = 25f * Mathf.Deg2Rad, th = 0.035f, dome = 0.04f;
        var V = new List<Vector3>(); var T = new List<int>();
        float a0 = notch / 2f, a1 = Mathf.PI * 2f - notch / 2f;
        // 上の面：まん中＋外周
        int cTop = V.Count; V.Add(new Vector3(0, dome, 0));
        int ringTop = V.Count;
        for (int i = 0; i <= seg; i++) { float a = Mathf.Lerp(a0, a1, i / (float)seg); float wav = 1f + 0.012f * Mathf.Sin(a * 7f); V.Add(new Vector3(Mathf.Sin(a) * wav, 0, Mathf.Cos(a) * wav)); }
        int cBot = V.Count; V.Add(new Vector3(0, -th, 0));
        int ringBot = V.Count;
        for (int i = 0; i <= seg; i++) { var t = V[ringTop + i]; V.Add(new Vector3(t.x, -th, t.z)); }
        for (int i = 0; i < seg; i++)
        {
            // 上（表は上向き：Unity の表は Cross(b-a, c-a) の向き）
            T.Add(cTop); T.Add(ringTop + i); T.Add(ringTop + i + 1);
            // 下
            T.Add(cBot); T.Add(ringBot + i + 1); T.Add(ringBot + i);
            // 横
            int a = ringTop + i, b = ringTop + i + 1, c = ringBot + i, d = ringBot + i + 1;
            T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d);
        }
        // 切れ込みの2つの面
        T.Add(cTop); T.Add(cBot); T.Add(ringTop); T.Add(ringTop); T.Add(cBot); T.Add(ringBot);
        T.Add(cTop); T.Add(ringTop + seg); T.Add(cBot); T.Add(ringTop + seg); T.Add(ringBot + seg); T.Add(cBot);
        m = new Mesh { name = "LilyPad" };
        m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    // 大きい蓮の花（花＋茎。Blender の Stage2Water → Models/Stage2Water.fbx）。茎は水の中へのびる（水面の板で隠れる）
    public static GameObject BigLotus(Transform parent, string name, Vector3 lp, float scale, float yaw)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaStage2/Models/Stage2Water.fbx").OfType<Mesh>().ToList();
        var flowerMesh = all.FirstOrDefault(m => m.name == "LotusFlower"); var stemMesh = all.FirstOrDefault(m => m.name == "LotusStem");
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(0, yaw, 0); g.transform.localScale = Vector3.one * scale;
        if (flowerMesh)
        {
            var f = new GameObject("Flower"); f.transform.SetParent(g.transform, false);
            f.AddComponent<MeshFilter>().sharedMesh = flowerMesh;
            f.AddComponent<MeshRenderer>().sharedMaterials = new[] { ToonMat("Stage2_LotusCenter", new Color(0.98f, 0.80f, 0.32f)), ToonMat("Stage2_LotusOuter", new Color(0.95f, 0.50f, 0.68f)),
                ToonMat("Stage2_LotusInner", new Color(0.99f, 0.70f, 0.82f)), ToonMat("Stage2_LotusCore", new Color(1f, 0.88f, 0.93f)) };
        }
        if (stemMesh)
        {
            var s = new GameObject("Stem"); s.transform.SetParent(g.transform, false);
            s.AddComponent<MeshFilter>().sharedMesh = stemMesh;
            s.AddComponent<MeshRenderer>().sharedMaterial = ToonMat("Stage2_LotusStem", new Color(0.36f, 0.64f, 0.32f));
        }
        // ふわふわが当たっても跳ね返るだけの当たり（花の大きさの円柱。アウトにはしない）
        var cc = g.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 0.05f, 0); cc.radius = 0.17f; cc.height = 0.2f; cc.direction = 1;
        return g;
    }

    // 泡の柱：bottom（水底）から top まで。forward＝てっぺんで放り出す向き
    public static GameObject BubbleColumn(Transform parent, string name, Vector3 lp, float bottom, float top, Vector3 forward)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(lp.x, bottom, lp.z); g.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
        float h = top - bottom;
        g.layer = 4;
        var cc = g.AddComponent<CapsuleCollider>(); cc.isTrigger = true; cc.direction = 1; cc.radius = 0.55f; cc.height = h; cc.center = new Vector3(0, h / 2f, 0);
        // 吹き出し口：水底の丸い石
        var vent = GameObject.CreatePrimitive(PrimitiveType.Cylinder); vent.name = "Vent"; vent.transform.SetParent(g.transform, false);
        Object.DestroyImmediate(vent.GetComponent<Collider>());
        vent.transform.localPosition = new Vector3(0, 0.03f, 0); vent.transform.localScale = new Vector3(0.9f, 0.04f, 0.9f);
        vent.GetComponent<Renderer>().sharedMaterial = ToonMat("Stage2_BubbleVent", new Color(0.55f, 0.62f, 0.72f));
        // 立ちのぼる小さい泡（いつも）
        var bub = new GameObject("RisingBubbles"); bub.transform.SetParent(g.transform, false); bub.transform.localPosition = new Vector3(0, 0.05f, 0);
        bub.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = bub.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = true; main.prewarm = true; main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.4f); main.startLifetime = h / 1.1f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.maxParticles = 80; main.startColor = new Color(1, 1, 1, 0.9f);
        var em = ps.emission; em.rateOverTime = 6f;   // 14→6（2026-10-17 てじゃ「泡、もっとすくなくてもいいかも」）
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 3f; sh.radius = 0.3f;
        var no = ps.noise; no.enabled = true; no.strength = 0.15f; no.frequency = 0.8f;
        var pr = bub.GetComponent<ParticleSystemRenderer>(); pr.renderMode = ParticleSystemRenderMode.Mesh;
        pr.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"); pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Bubble3D.mat");
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        // ふわふわを包む泡
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "WrapBubble"; b.transform.SetParent(g.transform, false);
        Object.DestroyImmediate(b.GetComponent<Collider>());
        b.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Bubble3D.mat");
        b.SetActive(false);
        // 割れた時の粒
        var pop = new GameObject("PopFx"); pop.transform.SetParent(g.transform, false);
        var pp = pop.AddComponent<ParticleSystem>(); pp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var pm = pp.main; pm.loop = false; pm.playOnAwake = false; pm.simulationSpace = ParticleSystemSimulationSpace.World;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); pm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); pm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        pm.startColor = new Color(0.9f, 1f, 1f, 0.95f); pm.maxParticles = 40; pm.gravityModifier = 0.3f;
        var pem = pp.emission; pem.enabled = false;
        var psh = pp.shape; psh.shapeType = ParticleSystemShapeType.Sphere; psh.radius = 0.28f;
        var ppr = pop.GetComponent<ParticleSystemRenderer>(); ppr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        ppr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var popA = pop.AddComponent<AudioSource>(); popA.playOnAwake = false; popA.spatialBlend = 1f; popA.minDistance = 2f; popA.maxDistance = 20f; popA.rolloffMode = AudioRolloffMode.Linear;
        popA.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_bubble_pop.wav");
        var capA = g.AddComponent<AudioSource>(); capA.playOnAwake = false; capA.spatialBlend = 1f; capA.minDistance = 2f; capA.maxDistance = 20f; capA.rolloffMode = AudioRolloffMode.Linear;
        capA.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_bubble_wrap.wav");
        var col = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBubbleColumn>(g);
        col.bubble = b.transform; col.popFx = pp; col.popSound = popA; col.captureSound = capA;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(col); NoSync(col);
        return g;
    }

    // ステージ1と同じトゥーン（FuwaCourse/ToonLit）の単色の材質
    static Material ToonMat(string name, Color c)
    {
        string p = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Lobby_Ring.mat");
            m = new Material(src); AssetDatabase.CreateAsset(m, p);
        }
        m.SetColor("_Color", c); EditorUtility.SetDirty(m);
        return m;
    }

    static Material LilyMat()
    {
        const string p = "Assets/FuwaStage2/Materials/Stage2_LilyPad.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat");
            m = new Material(src); m.SetTexture("_GroundTex", null);
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetColor("_Color", new Color(0.42f, 0.76f, 0.40f)); m.SetColor("_TopColor", new Color(0.50f, 0.82f, 0.45f));
        EditorUtility.SetDirty(m);
        return m;
    }

    // 滝：落ち口(top)から滝つぼの水面(bottom)まで。width＝幅、outward＝滝が落ちていく前の向き（水が流れてくる向き）
    public static void Waterfall(Transform root, string name, Vector3 lp, float top, float bottom, float width, Vector3 outward)
    {
        var g = new GameObject(name).transform; g.SetParent(root, false); g.localPosition = new Vector3(lp.x, 0, lp.z);
        g.localRotation = Quaternion.LookRotation(outward, Vector3.up);   // ローカル +Z＝水の進む向き
        float h = top - bottom;
        // 見た目：縦の板（落ち口から少し前に出て、滝つぼへ）
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Curtain"; q.transform.SetParent(g, false);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.localPosition = new Vector3(0, bottom + h / 2f, 0.15f); q.transform.localRotation = Quaternion.Euler(0, 180, 0);
        q.transform.localScale = new Vector3(width, h, 1f);
        q.GetComponent<Renderer>().sharedMaterial = WaterfallMat();
        // 押す範囲（厚み 0.9m、落ち口の少し上から滝つぼの水面まで）
        var t = new GameObject("Push"); t.transform.SetParent(g, false); t.layer = 4;
        // 下は滝つぼの水面の 0.35m 上まで（水面まで押すと滝の真下で浮いたまま押され続けた）
        t.transform.localPosition = new Vector3(0, bottom + 0.35f + (h - 0.35f + 0.4f) / 2f, 0.3f);
        var bc = t.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(width, h - 0.35f + 0.4f, 0.9f);
        var wf = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWaterfall>(t);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(wf); NoSync(wf);
        // しぶきの霧（滝つぼに当たる所でずっと）
        var m = new GameObject("Mist"); m.transform.SetParent(g, false); m.transform.localPosition = new Vector3(0, bottom + 0.05f, 0.4f);
        var ps = m.AddComponent<ParticleSystem>();
        var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); main.maxParticles = 60; main.startColor = new Color(1, 1, 1, 0.35f);
        var em = ps.emission; em.rateOverTime = 14f * width / 3f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(width, 0.1f, 0.3f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local; vel.y = new ParticleSystem.MinMaxCurve(0.35f); vel.x = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0.15f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(0.5f, 0.25f), new GradientAlphaKey(0f, 1) });
        col.color = gr;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(1, 1.4f)));
        var pr = m.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        // 音（いつも鳴っている、近くで聞こえる）
        var a = m.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_waterfall_loop.wav");
        a.loop = true; a.playOnAwake = true; a.spatialBlend = 1f; a.minDistance = 1.5f; a.maxDistance = 18f; a.rolloffMode = AudioRolloffMode.Linear; a.volume = 0.35f;
    }

    static Material WaterfallMat()
    {
        const string p = "Assets/FuwaStage2/Materials/Stage2_Waterfall.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var sh = Shader.Find("FuwaStage2/Waterfall");
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh; EditorUtility.SetDirty(m);
        return m;
    }

    // 「ちゃぽん」：音＋しぶき（白い粒が上に跳ねて落ちる）＋波紋（水面に平らな輪が広がる）。1つだけ作って全部の水から呼ぶ
    public static FuwaWaterFx MakeFx(Transform parent)
    {
        var go = new GameObject("WaterFx"); go.transform.SetParent(parent, false);
        var defMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        // しぶき
        var d = new GameObject("Drops"); d.transform.SetParent(go.transform, false); d.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var dp = d.AddComponent<ParticleSystem>(); dp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = dp.main; main.loop = false; main.playOnAwake = false; main.duration = 1f; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.065f); main.gravityModifier = 0.55f; main.maxParticles = 60;
        main.startColor = new Color(0.95f, 1f, 1f, 0.95f);
        var em = dp.emission; em.enabled = false;
        var sh = dp.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 28f; sh.radius = 0.07f;
        var sol = dp.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.3f)));
        var dr = d.GetComponent<ParticleSystemRenderer>(); dr.sharedMaterial = defMat; dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; dr.receiveShadows = false;
        // 波紋：平らに寝かせた輪が広がって消える
        var r = new GameObject("Ripple"); r.transform.SetParent(go.transform, false); r.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var rp = r.AddComponent<ParticleSystem>(); rp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var rm = rp.main; rm.loop = false; rm.playOnAwake = false; rm.duration = 1f; rm.simulationSpace = ParticleSystemSimulationSpace.World;
        rm.startLifetime = 1.0f; rm.startSpeed = 0f; rm.startSize = 0.35f; rm.maxParticles = 8; rm.startColor = new Color(1, 1, 1, 0.9f);
        var rem = rp.emission; rem.enabled = false;
        var rsh = rp.shape; rsh.enabled = false;
        var rsol = rp.sizeOverLifetime; rsol.enabled = true; rsol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(1, 1f)));
        var rcol = rp.colorOverLifetime; rcol.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1) });
        rcol.color = gr;
        rm.startSize = 0.9f;
        var rr = r.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Billboard; rr.alignment = ParticleSystemRenderSpace.Local;
        rr.sharedMaterial = RippleMat(); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
        // 音
        var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 1f; a.minDistance = 2f; a.maxDistance = 25f; a.rolloffMode = AudioRolloffMode.Linear;
        a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_water_plop.wav");
        var fx = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWaterFx>(go);
        fx.drops = dp; fx.ripple = rp; fx.sound = a;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fx); NoSync(fx);
        return fx;
    }

    static Material RippleMat()
    {
        const string p = "Assets/FuwaStage2/Materials/Stage2_Ripple.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var sh = Shader.Find("Mobile/Particles/Alpha Blended");
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh; m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Ripple.png");
        EditorUtility.SetDirty(m);
        return m;
    }

    // 水面の材質：流れの速さごとに1つ（止まった水・流れる水）
    static Material WaterSurfaceMat(float flowSpeed)
    {
        string p = "Assets/FuwaStage2/Materials/Stage2_Water_" + (flowSpeed > 0.001f ? "Flow" + flowSpeed.ToString("0.0") : "Still") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var sh = Shader.Find("FuwaStage2/Water");
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh; m.SetFloat("_FlowSpeed", flowSpeed);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WaterMat()
    {
        const string p = "Assets/FuwaStage2/Materials/Test_Water.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse")); AssetDatabase.CreateAsset(m, p); }
        m.color = new Color(0.45f, 0.78f, 0.95f, 0.55f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
