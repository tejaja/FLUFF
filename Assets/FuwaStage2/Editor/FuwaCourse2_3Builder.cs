using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–3「こおりのみち」ブロックアウト（2026-11-03 てじゃ「ほらちゃうしな」で こおりのほら から名前を変えた）（2026-11-03 てじゃの構成案の2回目で作り直し。洞くつ・地底湖・ぐねぐね道の版はなくした）。
// 決めごと：氷の道では人が滑る（FuwaIceSkate、氷のテスト場の C の方式）。ふわふわは道と同じで、触れたらアウト。
// 流れ（ロビーの Slot2 方向に放射状、+Z が進む向き。y=0 がスタートの島の地面）：
//  ① スタート ② 広い氷のエリア（前半 障害物よけ、後半 割れた氷と吹雪） ③ 大きな池の流氷（前半 止まる、後半 動く）
//  ④ つららの並木（いろんな角度に曲がる道） ⑤ ゴール前の坂（雪だるまの雪玉、左右に弧の氷の道） → ゴール
// 道は銀のシマシマ、氷はつるっとした水色、雪は白。曲がった道・割れた氷・流氷はメッシュ（Meshes/Course2_3）。仕掛け（吹雪・動く流氷・つらら・雪玉）はまだ目印だけ
public static class FuwaCourse2_3Builder
{
    const string RootName = "FuwaCourse2_3";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float StripeLen = 1.2f;
    public static float IntroHW = 2.5f;
    // ② 広い氷のエリア（前半 障害物、後半 割れた氷＋吹雪）
    // 2026-11-03 てじゃ「せっかくツルツル滑る床できたから、もっと広い場所ほしい」→ 幅10m→24m のスケートリンクに。入口・出口はじょじょに広がる/せばまる
    public static float WideHW = 12f, ObsLen = 32f, CrackLen = 32f, CrackT = 1.0f, CrackWaterY = -0.7f, WideFlareLen = 9f;
    // 割れた所（1m のます目）は CrackHoleRatio くらいの割合で乱数の穴。手前のはしから奥のはしまで歩ける道が必ず残るように1つずつ確かめて開ける
    public static float CrackHoleRatio = 0.24f; public static int CrackSeed = 2563;
    // 2026-11-03 てじゃ「いっそ大改造して、広大な広い氷の中を行ったり来たりする感じにしよう　割れ目と氷水で区切って」「区切りは超えられない」「ギミックは考え直そう」
    //  → 大きな凍った湖を水の割れ目でジグザグのレーンに区切る。曲がり角だけ氷がつながる。前の ②〜⑤ は LakeLayout=false で戻せる
    //  水の幅は人が跳んで越えられない幅（滑りの最高 4m/s × 跳んでいる 0.6 秒 ≒ 2.5m）より広く
    public static bool LakeLayout = true;
    public static int LakeW = 50, LaneW = 10, ChanW = 5, LakeLanes = 4, TurnGap = 10;
    // 2026-11-03 てじゃ「氷のテスト用に、そのコース全面クソデカ氷にしといて」→ 水の区切りなしの1枚の氷（PlainW×PlainD）。デザインが決まったら false に
    public static bool LakePlain = true; public static int PlainW = 60, PlainD = 70;
    // 2026-11-04 てじゃのラフ（仮）：氷水の湖に氷の島。島どうしは滑らない道（シマシマ）でつなぐ。黄＝ゆるい氷の坂（高い所）、茶＝ギリギリ登れるキツい坂、矢印＝動く氷の島。
    //  まわりはジャンプで届かない所に白い芝の岸。LakeMap が true ならこれ（LakePlain より先）。座標は湖の入口（スタートの道のはし）から X 右・Z 奥（m）
    //  島の形はラフの絵を 1px＝0.075m で写して、つながっていない島どうしが跳んで届かないように少しずらした（ななめの帯と流氷は +6m 右）
    public static bool LakeMap = true;
    public static float MapGapMin = 6f;      // つながっていない島どうし、これより近いと警告（滑りの最高 8m/s で跳んでも約 4.8m）
    public static float ShoreGap = 7.5f;     // いちばん外の物から雪の岸の水ぎわまで（届かない）
    public static float ShoreWidth = 8f;     // 雪の岸の幅（水ぎわから外へ）
    // ラフの形を入口の道の先（MapPivot）を中心に回す（＋が左回り）。左どなりの 2–2 にかぶったので右へ倒した（2026-11-04）
    public static float MapRot = -25f; public static Vector2 MapPivot = new Vector2(0f, 10.5f);
    public static float MapCell = 0.5f;      // 氷の島のメッシュのます目
    public static float MapFloeR = 2.8f;
    // 島（外のふち・穴）
    public static Vector2[] MapI1 = { new Vector2(-38.6f, 26.3f), new Vector2(-21.4f, 28.5f), new Vector2(-7.1f, 24.8f), new Vector2(3.4f, 25.5f), new Vector2(6.4f, 16.5f), new Vector2(1.9f, 11.3f), new Vector2(-4.1f, 7.5f), new Vector2(-10.9f, 1.5f), new Vector2(-16.9f, -0.75f), new Vector2(-26.6f, 2.3f), new Vector2(-36.4f, 9.75f), new Vector2(-40.1f, 18f) };
    public static Vector2[] MapRing = { new Vector2(-34.1f, 57f), new Vector2(-29.6f, 60.75f), new Vector2(-21.4f, 57.75f), new Vector2(-13.1f, 54.75f), new Vector2(-7.9f, 49.5f), new Vector2(-7.1f, 42f), new Vector2(-9.4f, 36f), new Vector2(-19.1f, 33f), new Vector2(-28.1f, 32.25f), new Vector2(-33.4f, 36f), new Vector2(-34.9f, 46.5f) };
    public static Vector2[] MapRingHole = { new Vector2(-22.1f, 51f), new Vector2(-14.6f, 49.5f), new Vector2(-11.6f, 45f), new Vector2(-14.6f, 39f), new Vector2(-21.4f, 38.25f), new Vector2(-25.9f, 43.5f), new Vector2(-24.4f, 49.5f) };
    public static Vector2[] MapBand = { new Vector2(-4.9f, 75.75f), new Vector2(4.9f, 73.5f), new Vector2(5.6f, 64.5f), new Vector2(13.9f, 57f), new Vector2(21.4f, 46.5f), new Vector2(25.1f, 36f), new Vector2(22.9f, 28.5f), new Vector2(14.6f, 27.75f), new Vector2(8.6f, 36f), new Vector2(1.9f, 46.5f), new Vector2(-4.9f, 56.25f), new Vector2(-7.9f, 64.5f) };
    public static Vector2[] MapI4 = { new Vector2(17.6f, 68.3f), new Vector2(25.9f, 66f), new Vector2(31.9f, 61.5f), new Vector2(34.1f, 57f), new Vector2(27.4f, 54.75f), new Vector2(21.4f, 57f), new Vector2(18.4f, 62.25f) };
    // 坂（線分 a→b からの距離で：plateau 以内はてっぺん、そこから run で 0 まで下がる。角度＝atan(h/run)）
    //  ゆるい丘（I1 の左下）約 12°、輪っかの左のキツい坂 約 28°（31°より急だと歩いて登れない）、帯のまん中のゆるい坂 約 13°
    public static (Vector2 a, Vector2 b, float plateau, float run, float h)[] MapHills = {
        (new Vector2(-33f, 13f), new Vector2(-22f, 5f), 3f, 12f, 2.5f),
        (new Vector2(-37f, 46.5f), new Vector2(-24f, 46.5f), 1.5f, 4.1f, 2.2f),
        (new Vector2(4.4f, 46.1f), new Vector2(20.6f, 53.9f), 1f, 11f, 2.5f) };
    // 滑らない道（つなぎ）：a→b、半分の幅
    public static (Vector2 a, Vector2 b, float hw)[] MapLinks = {
        (new Vector2(-26f, 27.4f), new Vector2(-26f, 33.0f), 3f),     // I1 → 輪っか
        (new Vector2(-24.3f, 58.3f), new Vector2(-7.0f, 67.2f), 3f),  // 輪っか → 帯
        (new Vector2(21.5f, 67.2f), new Vector2(21.5f, 76f), 3f) };   // I4 → ゴールの橋
    // つなぎの道の曲がり（まん中が a→b の左へ何 m ふくらむか。0 でまっすぐ）。輪っか→帯は外側へぐいっと曲げる（2026-11-04 てじゃ「左上の長い道をもっと外側に角度つけて、ぐいっと全体的に曲げよう」）
    public static float[] MapLinkBend = { 0f, 0f, 0f };
    // 輪っか→帯の長い道から先（帯・流氷・動く島・右上の島・ゴールへの道・帯の坂）を、長い道の始まり（輪っかのはし）を中心に回す（＋が左回り＝外側へ起こす）。
    //  2026-11-04 てじゃ「道はまっすぐのまま、右側の氷とかを全体的にまげる」「左上の長い道をもっと外側に角度つけて」
    public static float MapBendDeg = 20f, MapMoveGap = 1.8f;
    // つなぎの道のまん中の線（曲がりは 2 次ベジェ、1m くらいおき）
    static List<Vector2> LinkPts(int k)
    {
        var l = MapLinks[k]; float bend = k < MapLinkBend.Length ? MapLinkBend[k] : 0f;
        if (Mathf.Abs(bend) < 0.01f) return new List<Vector2> { l.a, l.b };
        var d = (l.b - l.a); var nl = new Vector2(-d.y, d.x).normalized; var ctl = (l.a + l.b) * 0.5f + nl * bend * 2f;   // ベジェのまん中はふくらみの 2 倍で引く
        int n = Mathf.Max(8, Mathf.CeilToInt(d.magnitude * 1.3f)); var pts = new List<Vector2>();
        for (int i = 0; i <= n; i++) { float t = i / (float)n, u = 1f - t; pts.Add(u * u * l.a + 2f * u * t * ctl + t * t * l.b); }
        return pts;
    }
    // つなぎの道の形（島からくりぬく）：まん中の線の左右に hw。横の向きは前後の点から（Ribbon と同じ）
    static Vector2[] LinkOutline(int k)
    {
        var pts = LinkPts(k); float hw = MapLinks[k].hw; var L = new List<Vector2>(); var R = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++)
        {
            var tg = pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]; tg.Normalize(); var rt = new Vector2(tg.y, -tg.x);
            L.Add(pts[i] - rt * hw); R.Add(pts[i] + rt * hw);
        }
        R.Reverse(); L.AddRange(R); return L.ToArray();
    }
    // 流氷（滑る）。最後の1つは左右に動く（しくみはまだ。いまは右のはしで止めてある）：move の2点の間
    public static Vector2[] MapFloes = { new Vector2(28.5f, 28f), new Vector2(33.5f, 33.5f), new Vector2(36f, 40f) };
    public static Vector2 MapMoveA = new Vector2(41f, 48f), MapMoveB = new Vector2(30f, 51f);
    // 前半の障害物（x＝まん中からの横、z＝氷の手前から、k 0=氷の岩 1=結晶の柱）
    public static (float x, float z, int k)[] Obstacles = {
        (-5.5f, 4f, 0), (3.0f, 5.5f, 1), (8.5f, 4.5f, 0), (-9.5f, 8f, 1), (-1.0f, 9f, 0), (6.0f, 10.5f, 1),
        (-6.0f, 13.5f, 0), (2.5f, 14f, 0), (9.5f, 15f, 1), (-2.5f, 18.5f, 1), (-9.0f, 19f, 0), (5.5f, 19.5f, 0),
        (0.5f, 23f, 1), (-6.5f, 24.5f, 0), (8.0f, 25f, 1), (3.5f, 28.5f, 0), (-3.5f, 29f, 0), (10f, 29.5f, 0) };
    // ③ 大きな池と流氷（x, z＝池の手前からの位置、r＝半径、type 0=滑らない 1=滑る 2=半分ずつ、move＝動く幅（0 は止まっている））
    public static float PondHW = 8f, PondLen = 34.3f, PondWaterY = -0.5f, FloeTop = -0.3f;
    public static (float x, float z, float r, int type, float move)[] Floes = {
        (0f, 2.6f, 1.6f, 0, 0f), (-2.0f, 6.4f, 1.5f, 1, 0f), (0.5f, 9.8f, 1.7f, 2, 0f), (2.8f, 13.3f, 1.4f, 1, 0f), (0.6f, 16.9f, 1.6f, 0, 0f),
        (-1.0f, 20.6f, 1.5f, 1, 1.2f), (1.2f, 24.2f, 1.6f, 2, 1.5f), (-0.8f, 27.9f, 1.5f, 0, 1.2f), (0.6f, 31.6f, 1.7f, 2, 0.8f) };
    // ④ つららの並木（len＝まっすぐの長さ、trees＝そこに立てる枯れ木の数、bend＝そのあと曲がる角度 ＋が右）
    public static float TreeHW = 2.2f, TreePadHW = 1.6f, TreeBendR = 5f;
    public static (float len, int trees, float bend)[] TreeSegs = { (8f, 1, 30f), (9f, 2, -45f), (8f, 1, 35f), (9f, 2, -25f), (6f, 1, 0f) };
    // ⑤ ゴール前（ゆるい上り坂、まん中の雪道と左右の弧の氷の道）
    // 両わきの氷の幅：手前 SideWBottom → 奥 SideWTop（ハの字）、SideBulge＝外のふちのふくらみ（0 でまっすぐ）
    // まん中の雪道の半分の幅も 手前 LaneHWBottom → 奥 LaneHWTop（2026-11-03 てじゃ「道ごと広げて」）
    public static float PlazaLen = 12f;   // 坂の手前の広場（細い道から坂の下の幅まで、じょじょに広がる）
    public static float FinalHW = 4.5f, LaneHWBottom = 7.0f, LaneHWTop = 1.0f, SideWBottom = 6.0f, SideWTop = 2.0f, SideBulge = 0f, FlareK = 2.2f, FinalLen = 34f, FinalRise = 3f, FinalTopLen = 6f;
    public static Color TreeCol = new Color(0.38f, 0.33f, 0.31f), IceRockCol = new Color(0.72f, 0.82f, 0.92f);
    // 道の色（2026-10-19 決めた 2–3 の色 #B4C2D2 から）。ふつうの道＝銀のシマシマ、氷＝つるっとした水色、雪（目印の玉）＝白
    public static Color PathColA = new Color(0.84f, 0.88f, 0.93f), PathColB = new Color(0.71f, 0.76f, 0.82f);
    public static Color IceCol = new Color(0.62f, 0.86f, 0.98f);
    public static Color SnowColA = new Color(0.97f, 0.98f, 1f), SnowColB = new Color(0.88f, 0.91f, 0.95f);
    public static Color RockCol = new Color(0.46f, 0.50f, 0.60f), CrystalCol = new Color(0.62f, 0.84f, 1f), MarkCol = new Color(1f, 0.5f, 0.2f);

    static Material pathA, pathB, iceFlat, snowA, snowB, rockMat, crystalMat, markMat, treeMat, iceRockMat;
    static int meshNo;
    const string MeshDir = "Assets/FuwaStage2/Meshes/Course2_3";
    static int pathLayer = 11;
    static FuwaBall ballProxy;
    static List<FuwaCheckpoint> cps = new List<FuwaCheckpoint>();
    static List<Collider> iceCols = new List<Collider>();

    [MenuItem("FuwaCourse/ステージ2/2–3 こおりのみち（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot2").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        root.rotation = Quaternion.LookRotation(spoke);
        root.position = new Vector3(lc.x, floorY - StartBelow, lc.z) + spoke * SpokeR;

        pathA = PathMat("Course2_3_PathA", PathColA); pathB = PathMat("Course2_3_PathB", PathColB);   // ロビーの道もこの色（FuwaStage2LobbyCourses）
        iceFlat = PathMat("Course2_3_Ice", IceCol);
        // 2026-11-03 てじゃ「テストコースみたいな白い芝みたいなのにしといて、動きがわかりづらい」→ 氷のテスト場の床（IceTest_IceC）の地面の模様をうつす（色は 2–3 の氷のまま）
        var testIce = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/IceTest_IceC.mat");
        if (testIce)
        {
            iceFlat.SetTexture("_GroundTex", testIce.GetTexture("_GroundTex"));
            foreach (var pn in new[] { "_GroundPatch", "_GroundPatchScale", "_GroundMidScale", "_GroundSteps", "_GroundFine", "_GroundFineScale", "_GroundFineFade", "_GroundFineCrisp" }) iceFlat.SetFloat(pn, testIce.GetFloat(pn));
            iceFlat.SetColor("_GroundTintA", testIce.GetColor("_GroundTintA")); iceFlat.SetColor("_GroundTintB", testIce.GetColor("_GroundTintB"));
            EditorUtility.SetDirty(iceFlat);
        }
        snowA = PathMat("Course2_3_SnowA", SnowColA); snowB = PathMat("Course2_3_SnowB", SnowColB);
        rockMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Rock", RockCol);
        crystalMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Crystal", CrystalCol);
        markMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Mark", MarkCol);
        treeMat = FuwaStage2WaterTest.ToonMat("Stage2_3_DeadTree", TreeCol);
        iceRockMat = FuwaStage2WaterTest.ToonMat("Stage2_3_IceRock", IceRockCol);
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Meshes")) AssetDatabase.CreateFolder("Assets/FuwaStage2", "Meshes");
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Meshes", "Course2_3");
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        cps.Clear(); iceCols.Clear();

        // 2026-11-03 てじゃの構成案（2回目）：
        //  ①スタート ②広めの氷のエリア：前半は障害物をよける、後半は吹雪（左右交互にたまに、ややつよい・短い）＋ところどころ割れていて下は水
        //  ③大きな池：流氷をジャンプで渡る、前半は止まっている・後半は動く ④つららの並木：いろんな角度に曲がる道
        //  ⑤ゴール前：雪だるま1体が雪玉をまっすぐ転がす（どんどん巨大に）。雪玉の通るまん中は雪道（滑らない）、左右に弧を描く細い氷の道で迂回できる。雪だるまの横を通り越して後ろにゴール
        // 決めたこと：吹雪はふわふわだけ押す（人は押さない）。水は人が落ちたらアウト、ふわふわが触れたら凍ってアウト。
        //  流氷は滑る・滑らない・半分ずつをまぜる、動く流氷は乗っている人も運ぶ、ふわふわは流氷に触れたらアウト（足場＝道あつかい）。
        //  つららで吹き飛ぶのはふわふわ（人は平気）、残ったつららはしばらくで溶ける、近づくと落ちる。雪玉はふわふわも人も吹き飛ばす
        float z0 = StartSet(root);
        var path = new GameObject("Path").transform; path.SetParent(root, false);
        var deco = new GameObject("Marks").transform; deco.SetParent(root, false);
        var waterT = new GameObject("Water").transform; waterT.SetParent(root, false);
        meshNo = 0;
        var p = new Vector3(0, 0, z0 - 0.5f);
        var dir = Vector3.forward;

        Strip(path, "Intro", p, dir, 5f, IntroHW, false); p += dir * 5f;
        if (LakeLayout)
        {
            if (!LakeMap) Flare(path, "LakeIn", ref p, dir, 6f, IntroHW, 5f);
            Lake(root, path, waterT, ref p, ref dir);
        }
        else
        {
        // ② 広い氷のエリア 前半：氷の岩と結晶の柱をよける
        Flare(path, "WideIn", ref p, dir, WideFlareLen, IntroHW, WideHW);
        var iceAt = p;
        Strip(path, "WideIce", p, dir, ObsLen, WideHW, true); p += dir * ObsLen;
        var obstacles = Obstacles;
        var obsT = new GameObject("Obstacles").transform; obsT.SetParent(root, false);
        for (int k = 0; k < obstacles.Length; k++)
        {
            var at = iceAt + new Vector3(obstacles[k].x, 0, obstacles[k].z);
            if (obstacles[k].k == 1) Crystal(obsT, "Crystal" + k, at, 0.55f, 2.6f);
            else IceRock(obsT, "IceRock" + k, at, 1.1f + 0.25f * (k % 3), k * 37f);
        }
        // ② 後半：割れた氷（1m のます目で穴、下は水）＋吹雪
        var crackAt = p;
        CrackedFloor(path, "CrackedIce", crackAt, CrackLen, WideHW, CrackHoleCells(Mathf.RoundToInt(WideHW * 2f), Mathf.RoundToInt(CrackLen)));
        Water(waterT, "CrackWater", crackAt + new Vector3(0, CrackWaterY, CrackLen * 0.5f), WideHW * 2f, CrackLen);
        var bz = new GameObject("BlizzardZone"); bz.transform.SetParent(root, false);   // 吹雪のかかる所（仕掛けはあとで。ふわふわだけ押す）
        bz.transform.localPosition = crackAt + new Vector3(0, 3f, CrackLen * 0.5f);
        var bzc = bz.AddComponent<BoxCollider>(); bzc.isTrigger = true; bzc.size = new Vector3(WideHW * 2f + 6f, 8f, CrackLen);
        p += dir * CrackLen;
        Strip(path, "WideOut", p, dir, 3f, WideHW, false); p += dir * 3f;
        Checkpoint(root, "Checkpoint1", p - dir * 1.5f, dir, WideHW);
        Flare(path, "WideNarrow", ref p, dir, WideFlareLen, WideHW, IntroHW);

        // ③ 大きな池：流氷をジャンプで渡る（前半は止まっている、後半は動く）
        Strip(path, "PondIn", p, dir, 3f, IntroHW, false); p += dir * 3f;
        var pondAt = p;
        Water(waterT, "PondWater", pondAt + new Vector3(0, PondWaterY, PondLen * 0.5f), PondHW * 2f, PondLen);
        var basin = Box(waterT, "PondBasin", pondAt + new Vector3(0, PondWaterY - 0.05f - 0.75f, PondLen * 0.5f), new Vector3(PondHW * 2f + 0.6f, 1.5f, PondLen), rockMat, 0);
        Object.DestroyImmediate(basin.GetComponent<Collider>());   // 人は水に沈んで下のアウトへ
        var floeT = new GameObject("Floes").transform; floeT.SetParent(root, false);
        for (int k = 0; k < Floes.Length; k++)
        {
            var f = Floes[k];
            Floe(floeT, "Floe" + k + (f.move > 0 ? "_Move" : ""), pondAt + new Vector3(f.x, FloeTop, f.z), f.r, f.type, 2562 + k * 17);
        }
        p += dir * PondLen;
        Strip(path, "PondOut", p, dir, 3f, IntroHW, false); p += dir * 3f;
        Checkpoint(root, "Checkpoint2", p - dir * 1.5f, dir, IntroHW);

        // ④ つららの並木：いろんな角度に曲がる道。まっすぐの所の横に枯れ木（1本ずつ雪の台にのせる）、枝の先から道の上へつらら（いまは目印）
        var trees = new GameObject("DeadTrees").transform; trees.SetParent(root, false);
        var icicles = new GameObject("Icicles").transform; icicles.SetParent(root, false);
        int treeNo = 0;
        for (int k = 0; k < TreeSegs.Length; k++)
        {
            var seg = TreeSegs[k];
            var segAt = p; var segDir = dir; var segR = Vector3.Cross(Vector3.up, segDir);
            Strip(path, "TreeRoad" + k, p, dir, seg.len, TreeHW, false); p += dir * seg.len;
            // 曲がる外側（＝見通しのいい側）と内側に交互。曲がる直前・直後はさける
            for (int j = 0; j < seg.trees; j++)
            {
                float side = (treeNo % 2 == 0) ? -1f : 1f;
                float along = seg.len * (j + 1f) / (seg.trees + 1f);
                float off = TreeHW + TreePadHW;
                Strip(path, "TreePad" + treeNo, segAt + segDir * (along - TreePadHW) + segR * side * off - Vector3.up * 0.25f, segDir, TreePadHW * 2f, TreePadHW, false, true);
                var tips = DeadTree(trees, "DeadTree" + treeNo, segAt + segDir * along + segR * side * off, -segR * side, segDir, treeNo, off);
                for (int t = 0; t < tips.Length; t++) Icicle(icicles, "Icicle" + treeNo + "_" + t, tips[t], 1.6f - 0.3f * t);
                treeNo++;
            }
            if (Mathf.Abs(seg.bend) > 0.5f) Arc(path, "TreeBend" + k, ref p, ref dir, TreeBendR, seg.bend, TreeHW, false);
        }
        Checkpoint(root, "Checkpoint3", p - dir * 1.5f, dir, TreeHW);

        // ⑤ ゴール前：ゆるい上り坂。まん中は雪道（雪玉の通り道・滑らない）、左右に弧を描く細い氷の道。てっぺんに雪だるま、その後ろにゴール
        // 2026-11-03 てじゃ「氷と道はつなげてほしい、ハの字みたいに手前ほど広く」→ 雪道の両わきに氷をくっつけて、外のふちは手前ほど外へ（雪玉が大きくなる手前ほどよけやすい）
        //  さらに「道ごと広げて」→ まん中の雪道も手前ほど広いハの字に（雪道は左右の半分ずつ、まん中で継ぎ目なしにくっつける）
        //  てじゃ「手前のつながり部分、いきなりドンと広がるんじゃなくてワンクッション広場を置こう」→ 細い道 → 広場 → 坂の下の台
        //  「じょじょに広くなってくように」→ 広場は道の幅から坂の下の幅まで、なめらかに（smoothstep）広がる。左右の半分をまん中でくっつける
        Flare(path, "FinalPlaza", ref p, dir, PlazaLen, TreeHW, LaneHWBottom + SideWBottom);
        Strip(path, "FinalBase", p, dir, 3f, LaneHWBottom + SideWBottom, false); p += dir * 3f;
        var fAt = p; var fDir = dir; var fR = Vector3.Cross(Vector3.up, fDir);
        var rise = new Vector3(0, FinalRise, 0) + fDir * FinalLen;
        int fn = Mathf.Max(4, Mathf.RoundToInt(FinalLen / StripeLen));
        foreach (float side in new[] { -1f, 1f })
        {
            var mid = new List<Vector3>(); var inner = new List<Vector3>(); var outer = new List<Vector3>();
            for (int i = 0; i <= fn; i++)
            {
                float t = (float)i / fn;
                var c = fAt + rise * t;
                // てじゃ「それぞれの直線を曲げたい、手前にくるほど角度が急に」→ 幅は (1-t)^FlareK で開く（ラッパの口のよう）
                float f = Mathf.Pow(1f - t, FlareK);
                float lw = LaneHWTop + (LaneHWBottom - LaneHWTop) * f;
                float w = SideWTop + (SideWBottom - SideWTop) * f + SideBulge * Mathf.Sin(Mathf.PI * t);
                mid.Add(c); inner.Add(c + fR * side * lw); outer.Add(c + fR * side * (lw + w));
            }
            Band(path, side < 0 ? "SnowLaneL" : "SnowLaneR", mid, inner, false, true, 0f);
            Band(path, side < 0 ? "SideIceL" : "SideIceR", inner, outer, true);
        }
        p += rise;
        Strip(path, "FinalTop", p, dir, FinalTopLen, FinalHW, false, true);
        Snowman(deco, "Snowman", p + dir * 2.2f, -dir);
        var sbm = GameObject.CreatePrimitive(PrimitiveType.Sphere); sbm.name = "SnowballSource"; sbm.transform.SetParent(deco, false);
        sbm.transform.localPosition = p + dir * 0.9f + Vector3.up * 0.45f; sbm.transform.localScale = Vector3.one * 0.9f; sbm.GetComponent<Renderer>().sharedMaterial = snowA;
        Object.DestroyImmediate(sbm.GetComponent<Collider>());
        p += dir * FinalTopLen;
        }

        // ゴール（道の向きがまっすぐ前でない時も、ゴールの島は道の向きに置く）
        GoalSet(root, p, dir);

        // 人が滑る
        var sk = new GameObject("IceSkate"); sk.transform.SetParent(root, false);
        var skate = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaIceSkate>(sk);
        skate.ice = iceCols.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(skate); FuwaStage2WaterTest.NoSync(skate);
        foreach (var mi in root.GetComponentsInChildren<FuwaMovingIce>(true)) { mi.skate = skate; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(mi); }
        foreach (var ti in root.GetComponentsInChildren<FuwaTiltIce>(true)) { ti.skate = skate; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ti); }

        // アウト（下に落ちた）：コースの下（コースの向きの箱、まわり 10m）
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var cl in root.GetComponentsInChildren<Collider>(true))
        {
            if (cl.isTrigger) continue;
            Vector3 cc, e; var t = cl.transform;
            if (cl is BoxCollider bc) { cc = bc.center; e = bc.size * 0.5f; }
            else if (cl is MeshCollider mc && mc.sharedMesh) { cc = mc.sharedMesh.bounds.center; e = mc.sharedMesh.bounds.extents; }
            else continue;
            for (int k = 0; k < 8; k++)
            {
                var c = cc + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                var lp = root.InverseTransformPoint(t.TransformPoint(c));
                if (first) { lb = new Bounds(lp, Vector3.zero); first = false; } else lb.Encapsulate(lp);
            }
        }
        var outSize = new Vector3(lb.size.x + 20f, 2f, lb.size.z + 20f);
        Hazard(root, "BallOut", new Vector3(lb.center.x, -7f, lb.center.z), outSize, false);
        Hazard(root, "PlayerOut", new Vector3(lb.center.x, -9f, lb.center.z), outSize, true);

        RegisterCheckpoints();
        // ステージ2にいる時だけ表示
        var visb = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "FuwaAreaVisibility");
        if (visb)
        {
            var so = new SerializedObject(visb); var arr = so.FindProperty("stage2Objects");
            bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == root.gameObject) has = true;
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
            if (!has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject; }
            so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)visb);
        }
        RegisterBgm(root);
        FuwaStage2LobbyCourses.Run();
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "[2–3] ゴールの位置 " + p.ToString("F1") + "、向き " + dir.ToString("F2") + "、範囲 " + lb.min.ToString("F0") + "〜" + lb.max.ToString("F0") + "、氷の当たり " + iceCols.Count + " 個";
    }

    // BGM（2026-11-05 てじゃ B「ゆきのよる」、Tools/audio/make_bgm_kori.py の kori_b_long）。2–1・2–2 と同じく FuwaSystem/BGM に足して、作り直すたびに BallStart をつなぎ直す
    const string BgmClip = "Assets/FuwaStage2/Audio/bgm_kori_long.wav";
    const float BgmGain = 1f;
    public static void RegisterBgm(Transform root)
    {
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault(); if (!bgm) return;
        var start = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BallStart"); if (!start) return;
        var src = bgm.transform.Find("BGM_course2_1"); if (!src) return;
        var tr = bgm.transform.Find("BGM_course2_3");
        if (!tr) { tr = Object.Instantiate(src.gameObject, bgm.transform, false).transform; tr.name = "BGM_course2_3"; }
        var a = tr.GetComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmClip); EditorUtility.SetDirty(a);
        var so = new SerializedObject(bgm);
        var tracks = so.FindProperty("tracks"); var starts = so.FindProperty("starts");
        int idx = -1;
        for (int i = 0; i < tracks.arraySize; i++) if (tracks.GetArrayElementAtIndex(i).objectReferenceValue == a) idx = i;
        if (idx < 0) { idx = tracks.arraySize; tracks.arraySize++; tracks.GetArrayElementAtIndex(idx).objectReferenceValue = a; }
        while (starts.arraySize <= idx) { starts.arraySize++; starts.GetArrayElementAtIndex(starts.arraySize - 1).objectReferenceValue = null; }
        starts.GetArrayElementAtIndex(idx).objectReferenceValue = start;
        var gains = so.FindProperty("trackGains");
        while (gains.arraySize <= idx) { gains.arraySize++; gains.GetArrayElementAtIndex(gains.arraySize - 1).floatValue = 1f; }
        gains.GetArrayElementAtIndex(idx).floatValue = BgmGain;
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bgm);
    }

    // ---- 部品 ----
    static Material PathMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_1_PathA.mat");
            m = new Material(src); AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_Color", c);
        if (m.HasProperty("_UnderColor")) m.SetColor("_UnderColor", Color.Lerp(c, Color.white, 0.4f));
        EditorUtility.SetDirty(m);
        return m;
    }
    static GameObject Box(Transform p, string name, Vector3 lp, Vector3 size, Material m, int layer)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false);
        g.transform.localPosition = lp; g.transform.localScale = size; g.layer = layer;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }
    // シマシマの道：a（手前のはしのまん中、上の面の高さ）から dir へ len。当たりは1枚の箱、見た目は StripeLen ごとに A/B。ice なら氷（人が滑る）、snow なら白
    static void Strip(Transform parent, string name, Vector3 a, Vector3 dir, float len, float hw, bool ice, bool snow = false)
    {
        if (len <= 0.01f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = a; g.localRotation = Quaternion.LookRotation(dir);
        const float T = 0.6f;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -T * 0.5f, len * 0.5f); bc.size = new Vector3(hw * 2f, T, len);
        if (ice) iceCols.Add(bc);
        // ふつうの道＝銀のシマシマ、氷＝シマシマなしのつるっとした水色（1枚）
        var ma = ice ? iceFlat : pathA; var mb = ice ? iceFlat : pathB;
        if (snow) { ma = snowA; mb = snowB; }
        int n = ice ? 1 : Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            var s = Box(g, "S" + k, new Vector3(0, -T * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, T, l), (k % 2 == 0) ? ma : mb, 0);
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        var u = Box(g, "Under", new Vector3(0, -T - 0.75f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.5f, len - 0.1f), rockMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }
    // まっすぐな道を (長さ, 氷か) の順に切れ目なく並べる。p は最後のはしまで進む
    static void Seq(Transform parent, string name, ref Vector3 p, Vector3 dir, float hw, params (float len, bool ice)[] parts)
    {
        for (int k = 0; k < parts.Length; k++) { Strip(parent, name + "_" + k + (parts[k].ice ? "_Ice" : ""), p, dir, parts[k].len, hw, parts[k].ice); p += dir * parts[k].len; }
    }
    // 角の踊り場（道の幅の正方形）。入ってきた向き from のはし p から、出ていく向き to のはしを返す
    static Vector3 Corner(Transform parent, string name, Vector3 p, Vector3 from, Vector3 to, float hw, bool ice)
    {
        Strip(parent, name, p, from, hw * 2f, hw, ice);
        var c = p + from * hw;
        return c + to * hw;
    }
    // ---- 曲がった道 ----
    // p（はしのまん中、上の面の高さ）から dir の向きに入って、半径 R（道のまん中）で ang 度曲がる（＋が右）。p と dir は出口へ進む。
    // 見た目は1つのメッシュ（ふつうの道ならシマシマを2つのサブメッシュ、氷なら1つ）、当たりは同じ形の MeshCollider。はしのふたは作らない（となりの道とくっつく）
    static void Arc(Transform parent, string name, ref Vector3 p, ref Vector3 dir, float R, float ang, float hw, bool ice)
    {
        const float T = 0.6f;
        float s = Mathf.Sign(ang), a = Mathf.Abs(ang);
        var right0 = Vector3.Cross(Vector3.up, dir);
        var c = p + right0 * s * R;
        var v0 = p - c;
        float arcLen = a * Mathf.Deg2Rad * R;
        int n = Mathf.Max(4, Mathf.CeilToInt(a / 5f));                          // 5 度ごと
        int stripes = ice ? 1 : Mathf.Max(1, Mathf.RoundToInt(arcLen / StripeLen));
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        // 見た目（上の面＋両側＋下の面）と、下の岩（少し内側）
        var top = NewMesh(name); var under = NewMesh(name + "_Under");
        var tv = new List<Vector3>(); var tA = new List<int>(); var tB = new List<int>();
        var uv = new List<Vector3>(); var uT = new List<int>();
        for (int i = 0; i < n; i++)
        {
            float t0 = a * i / n, t1 = a * (i + 1) / n;
            var q0 = Quaternion.Euler(0, s * t0, 0); var q1 = Quaternion.Euler(0, s * t1, 0);
            var m0 = c + q0 * v0; var m1 = c + q1 * v0;
            var r0 = q0 * right0; var r1 = q1 * right0;
            int stripe = Mathf.Min(stripes - 1, Mathf.FloorToInt((t0 + t1) * 0.5f / a * stripes));
            var tris = (stripe % 2 == 0) ? tA : tB;
            Slab(tv, tris, m0, m1, r0, r1, hw, 0f, -T, Vector3.up);
            Slab(uv, uT, m0, m1, r0, r1, hw - 0.1f, -T, -T - 1.5f, Vector3.up);
        }
        top.SetVertices(tv);
        if (ice || tB.Count == 0) { top.subMeshCount = 1; top.SetTriangles(tA.Concat(tB).ToList(), 0); }
        else { top.subMeshCount = 2; top.SetTriangles(tA, 0); top.SetTriangles(tB, 1); }
        top.RecalculateNormals(); top.RecalculateBounds();
        under.SetVertices(uv); under.SetTriangles(uT, 0); under.RecalculateNormals(); under.RecalculateBounds();
        var vis = new GameObject("S"); vis.transform.SetParent(g, false);
        vis.AddComponent<MeshFilter>().sharedMesh = top;
        vis.AddComponent<MeshRenderer>().sharedMaterials = ice ? new[] { iceFlat } : (top.subMeshCount == 2 ? new[] { pathA, pathB } : new[] { pathA });
        var un = new GameObject("Under"); un.transform.SetParent(g, false);
        un.AddComponent<MeshFilter>().sharedMesh = under; un.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var mc = col.AddComponent<MeshCollider>(); mc.sharedMesh = top;
        if (ice) iceCols.Add(mc);
        var q = Quaternion.Euler(0, s * a, 0);
        p = c + q * v0; dir = (q * dir).normalized;
    }
    // 曲がった板の1こま：m0→m1（まん中の線）、r0/r1（横の向き）、上の面 y0・下の面 y1（m の高さから）。上・下・外側・内側の4面
    static void Slab(List<Vector3> v, List<int> tris, Vector3 m0, Vector3 m1, Vector3 r0, Vector3 r1, float hw, float y0, float y1, Vector3 up)
    {
        Vector3 L0 = m0 - r0 * hw, R0 = m0 + r0 * hw, L1 = m1 - r1 * hw, R1 = m1 + r1 * hw;
        Vector3 Y0 = up * y0, Y1 = up * y1;
        var fwd = (m1 - m0).normalized;
        Quad(v, tris, L0 + Y0, R0 + Y0, R1 + Y0, L1 + Y0, up);                 // 上
        Quad(v, tris, L0 + Y1, L1 + Y1, R1 + Y1, R0 + Y1, -up);                // 下
        Quad(v, tris, R0 + Y0, R0 + Y1, R1 + Y1, R1 + Y0, (r0 + r1).normalized);   // 右の側面
        Quad(v, tris, L0 + Y0, L1 + Y0, L1 + Y1, L0 + Y1, -(r0 + r1).normalized);  // 左の側面
    }
    // 四角（a b c d の順にまわる）。表は want の向き（Unity は Cross(b-a, c-a) の向きが表）
    static void Quad(List<Vector3> v, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
    {
        int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f;
        if (!flip) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
    }
    // メッシュはアセットに（シーンを重くしない）。同じ名前があれば中身を入れ直す（Clear(false)：前の列を残さない）
    static Mesh NewMesh(string name)
    {
        string path = MeshDir + "/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m) { m.Clear(false); m.name = name; return m; }
        m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); return m;
    }

    // 曲がった細い道：pts（まん中の線、上の面の高さ）にそった板。横の向きは前後の点から（つなぎ目で角がそろう）。ice なら氷、snow なら雪、ほかはシマシマ
    static void Ribbon(Transform parent, string name, List<Vector3> pts, float hw, bool ice, bool snow = false)
    {
        const float T = 0.6f;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var rights = new Vector3[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            var tg = pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]; tg.y = 0f;
            rights[i] = Vector3.Cross(Vector3.up, tg.normalized).normalized;
        }
        var top = NewMesh(name); var under = NewMesh(name + "_Under");
        var tv = new List<Vector3>(); var tA = new List<int>(); var tB = new List<int>();
        var uv = new List<Vector3>(); var uT = new List<int>();
        float run = 0f;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float segLen = (pts[i + 1] - pts[i]).magnitude;
            int stripe = Mathf.FloorToInt((run + segLen * 0.5f) / StripeLen); run += segLen;
            var tris = (!ice && !snow && stripe % 2 == 1) || (snow && stripe % 2 == 1) ? tB : tA;
            Slab(tv, tris, pts[i], pts[i + 1], rights[i], rights[i + 1], hw, 0f, -T, Vector3.up);
            Slab(uv, uT, pts[i], pts[i + 1], rights[i], rights[i + 1], hw - 0.1f, -T, -T - 1.5f, Vector3.up);
        }
        top.SetVertices(tv);
        if (ice || tB.Count == 0) { top.subMeshCount = 1; top.SetTriangles(tA.Concat(tB).ToList(), 0); }
        else { top.subMeshCount = 2; top.SetTriangles(tA, 0); top.SetTriangles(tB, 1); }
        top.RecalculateNormals(); top.RecalculateBounds();
        under.SetVertices(uv); under.SetTriangles(uT, 0); under.RecalculateNormals(); under.RecalculateBounds();
        var vis = new GameObject("S"); vis.transform.SetParent(g, false);
        vis.AddComponent<MeshFilter>().sharedMesh = top;
        var ma = snow ? snowA : pathA; var mb = snow ? snowB : pathB;
        vis.AddComponent<MeshRenderer>().sharedMaterials = ice ? new[] { iceFlat } : (top.subMeshCount == 2 ? new[] { ma, mb } : new[] { ma });
        var un = new GameObject("Under"); un.transform.SetParent(g, false);
        un.AddComponent<MeshFilter>().sharedMesh = under; un.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var mc = col.AddComponent<MeshCollider>(); mc.sharedMesh = top;
        if (ice) iceCols.Add(mc);
    }

    // 内と外のふちの線で決める板（雪道のわきにくっつける氷）。内側の側面・はしのふたは作らない（となりとくっつく）。下に岩
    // snow なら雪のシマシマ（1こまごとに A/B）。innerInset＝下の岩の内側のふちを内へずらす量（雪道の半分どうしは 0、氷は雪道の岩にそろえて 0.1）
    static void Band(Transform parent, string name, List<Vector3> inner, List<Vector3> outer, bool ice, bool snow = false, float innerInset = 0.1f)
    {
        const float T = 0.6f;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var top = NewMesh(name); var under = NewMesh(name + "_Under");
        var v = new List<Vector3>(); var t = new List<int>(); var t2 = new List<int>(); var uv = new List<Vector3>(); var ut = new List<int>();
        var dn = Vector3.down * T; var dn2 = Vector3.down * (T + 1.5f);
        for (int i = 0; i + 1 < inner.Count; i++)
        {
            Vector3 a = inner[i], b = outer[i], c = outer[i + 1], d = inner[i + 1];
            var outw = ((b - a) + (c - d)); outw.y = 0f; outw.Normalize();
            var tt = (!ice && i % 2 == 1) ? t2 : t;
            Quad(v, tt, a, b, c, d, Vector3.up);
            Quad(v, tt, a + dn, d + dn, c + dn, b + dn, Vector3.down);
            Quad(v, tt, b, b + dn, c + dn, c, outw);
            Vector3 ai = a - outw * innerInset, di = d - outw * innerInset, bo = b - outw * 0.1f, co = c - outw * 0.1f;
            Quad(uv, ut, ai + dn, bo + dn, co + dn, di + dn, Vector3.up);
            Quad(uv, ut, ai + dn2, di + dn2, co + dn2, bo + dn2, Vector3.down);
            Quad(uv, ut, bo + dn, bo + dn2, co + dn2, co + dn, outw);
        }
        top.SetVertices(v);
        if (t2.Count == 0) { top.subMeshCount = 1; top.SetTriangles(t, 0); } else { top.subMeshCount = 2; top.SetTriangles(t, 0); top.SetTriangles(t2, 1); }
        top.RecalculateNormals(); top.RecalculateBounds();
        under.SetVertices(uv); under.SetTriangles(ut, 0); under.RecalculateNormals(); under.RecalculateBounds();
        var vis = new GameObject("S"); vis.transform.SetParent(g, false);
        vis.AddComponent<MeshFilter>().sharedMesh = top;
        var ma = snow ? snowA : pathA; var mb = snow ? snowB : pathB;
        vis.AddComponent<MeshRenderer>().sharedMaterials = ice ? new[] { iceFlat } : (t2.Count == 0 ? new[] { ma } : new[] { ma, mb });
        var un = new GameObject("Under"); un.transform.SetParent(g, false);
        un.AddComponent<MeshFilter>().sharedMesh = under; un.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var mc = col.AddComponent<MeshCollider>(); mc.sharedMesh = top;
        if (ice) iceCols.Add(mc);
    }

    // 凍った湖：p（手前のふちのまん中）から奥へ。LakeLanes 本のレーン（幅 LaneW）を ChanW の水で区切る。
    //  1本目は入口（まん中）から右へ、右はしで曲がって2本目は左へ…と交互。曲がり角は TurnGap ぶん水がない（氷がつながる）
    //  床は1枚の氷（割れた氷と同じ作り方、水の所が穴）、下に湖ぜんぶの水。出口は最後のレーンの曲がり角の側の奥のふち
    static void Lake(Transform root, Transform path, Transform waterT, ref Vector3 p, ref Vector3 dir)
    {
        if (LakeMap) { if (MapVersion == 2) LakeFromMap2(root, path, waterT, ref p, ref dir); else LakeFromMap(root, path, waterT, ref p, ref dir); return; }
        if (LakePlain)
        {
            var at0 = p;
            Strip(path, "LakeIce", at0, dir, PlainD, PlainW * 0.5f, true);
            // 坂のテスト（2026-11-03 てじゃ「角度の強さに応じてずり落ちる」「キツすぎると登れない」）：10・20・30・40 度の氷の山（高さ 2.5m）
            //  てじゃ「きつい坂、一番緩い坂と先端あわせといて、めっちゃ高くなっていいから」→ 上り坂の長さ（横）を 10 度の山にそろえて、きついほど高く（40 度で約 12m）
            float[] angs = { 10f, 20f, 30f, 40f };
            float hillRun = 2.5f / Mathf.Tan(10f * Mathf.Deg2Rad), hillTop = 3f, hillStartZ = 12f;
            for (int k = 0; k < angs.Length; k++)
            {
                float h = hillRun * Mathf.Tan(angs[k] * Mathf.Deg2Rad);
                IceHill(path, "SlopeTest" + (int)angs[k], at0 + new Vector3(-21f + k * 14f, 0, hillStartZ + hillRun + hillTop * 0.5f), 6f, h, angs[k], hillTop);
            }
            p = at0 + dir * PlainD;
            Flare(path, "LakeOut", ref p, dir, 6f, 5f, IntroHW);
            Strip(path, "LakeExit", p, dir, 3f, IntroHW, false); p += dir * 3f;
            return;
        }
        int nx = LakeW, nz = LakeLanes * LaneW + (LakeLanes - 1) * ChanW;
        var holes = new List<(int, int, int, int)>();
        for (int k = 0; k < LakeLanes - 1; k++)
        {
            int z = (k + 1) * LaneW + k * ChanW;
            bool turnRight = k % 2 == 0;
            holes.Add(turnRight ? (0, z, nx - TurnGap, ChanW) : (TurnGap, z, nx - TurnGap, ChanW));
        }
        var at = p;
        CrackedFloor(path, "LakeIce", at, nz, nx * 0.5f, holes.ToArray());
        Water(waterT, "LakeWater", at + new Vector3(0, CrackWaterY, nz * 0.5f), nx, nz);
        // 曲がり角のチェックポイント（水の横の、氷がつながっている所）
        for (int k = 0; k < LakeLanes - 1; k++)
        {
            float z = (k + 1) * LaneW + k * ChanW + ChanW * 0.5f;
            float x = (k % 2 == 0) ? nx * 0.5f - TurnGap * 0.5f : -nx * 0.5f + TurnGap * 0.5f;
            Checkpoint(root, "Checkpoint" + (k + 1), at + new Vector3(x, 0, z), Vector3.forward, TurnGap * 0.5f);
        }
        // 出口：最後のレーンは、ひとつ前の曲がり角と反対のはしへ向かう
        bool lastGoesRight = (LakeLanes - 1) % 2 == 0;
        float ex = (lastGoesRight ? 1f : -1f) * (nx * 0.5f - LaneW * 0.5f);
        p = at + new Vector3(ex, 0, nz); dir = Vector3.forward;
        Flare(path, "LakeOut", ref p, dir, 6f, LaneW * 0.5f, IntroHW);
        Strip(path, "LakeExit", p, dir, 3f, IntroHW, false); p += dir * 3f;
    }
    // ---- ラフの湖（LakeMap）----
    static bool InPoly(Vector2[] poly, Vector2 q)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (((poly[i].y > q.y) != (poly[j].y > q.y)) && (q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
        return c;
    }
    static Vector2 NearestOnPoly(Vector2[] poly, Vector2 q, out float d)
    {
        d = float.MaxValue; var best = q;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[j]; var b = poly[i]; var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            var c = a + ab * t; float dd = (q - c).magnitude; if (dd < d) { d = dd; best = c; }
        }
        return best;
    }
    static float SegDist(Vector2 a, Vector2 b, Vector2 q) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return (q - (a + ab * t)).magnitude; }
    static float MapHeight(Vector2 q)
    {
        float h = 0f;
        for (int k = 0; k < MapHills.Length; k++)
        {
            var hl = MapHills[k];
            // その島の上だけ（となりの島まで坂がはみ出さないように。2026-11-04 てじゃ「次の丸い島まで影響でてる」）
            if (MapHillOnly != null && k < MapHillOnly.Length && MapHillOnly[k] != null && !InPoly(MapHillOnly[k], q)) { NearestOnPoly(MapHillOnly[k], q, out float dd); if (dd > 0.6f) continue; }
            float d = SegDist(hl.a, hl.b, q); h = Mathf.Max(h, hl.h * (1f - Mathf.Clamp01((d - hl.plateau) / hl.run)));
        }
        if (map2Ridge) h = Mathf.Max(h, Map2Ridge(q));
        return h;
    }
    // 氷の島（ます目のメッシュ）：ます目のまん中が中にあるものを残す。外に出ているかどの点はふちの線の上へ寄せる（ふちがギザギザにならない）。
    //  上の面の高さは MapHeight、ふちから下へ側面（-CrackT まで）、下の面
    //  cut：島からくりぬく形（穴・つなぎの道の長方形）。つなぎの道は島に重ねず、くりぬいた所にぴったり入る（重なりのチラつき・すき間なし）
    static void MapIsland(Transform parent, string name, Vector3 o, Vector2[] outer, List<Vector2[]> cut)
    {
        float cs = MapCell;
        float minX = outer.Min(q => q.x) - cs, maxX = outer.Max(q => q.x) + cs, minZ = outer.Min(q => q.y) - cs, maxZ = outer.Max(q => q.y) + cs;
        int nx = Mathf.CeilToInt((maxX - minX) / cs), nz = Mathf.CeilToInt((maxZ - minZ) / cs);
        System.Func<Vector2, bool> Inside = q => InPoly(outer, q) && !cut.Any(c => InPoly(c, q));
        var solid = new bool[nx, nz];
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++) solid[x, z] = Inside(new Vector2(minX + (x + 0.5f) * cs, minZ + (z + 0.5f) * cs));
        // かどの点：外なら近いふちへ
        var corner = new Vector2[nx + 1, nz + 1];
        for (int x = 0; x <= nx; x++) for (int z = 0; z <= nz; z++)
        {
            var q = new Vector2(minX + x * cs, minZ + z * cs);
            if (!Inside(q))
            {
                // 外ならふちへ、くりぬいた形の中ならその形のふちへ（近い方）
                Vector2 best = q; float bd = float.MaxValue;
                if (!InPoly(outer, q)) { best = NearestOnPoly(outer, q, out bd); }
                foreach (var c in cut) if (InPoly(c, q)) { var b = NearestOnPoly(c, q, out float db); if (db < bd) { bd = db; best = b; } }
                q = best;
            }
            corner[x, z] = q;
        }
        System.Func<int, int, bool> S = (x, z) => x >= 0 && x < nx && z >= 0 && z < nz && solid[x, z];
        System.Func<Vector2, float, Vector3> V = (q, y) => new Vector3(q.x, y, q.y);
        float T = CrackT;
        var v = new List<Vector3>(); var t = new List<int>();
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
        {
            if (!solid[x, z]) continue;
            Vector2 c00 = corner[x, z], c10 = corner[x + 1, z], c11 = corner[x + 1, z + 1], c01 = corner[x, z + 1];
            float h00 = MapHeight(c00), h10 = MapHeight(c10), h11 = MapHeight(c11), h01 = MapHeight(c01);
            Quad(v, t, V(c00, h00), V(c10, h10), V(c11, h11), V(c01, h01), Vector3.up);
            Quad(v, t, V(c00, -T), V(c01, -T), V(c11, -T), V(c10, -T), Vector3.down);
            if (!S(x - 1, z)) Quad(v, t, V(c00, h00), V(c01, h01), V(c01, -T), V(c00, -T), Vector3.left);
            if (!S(x + 1, z)) Quad(v, t, V(c10, h10), V(c10, -T), V(c11, -T), V(c11, h11), Vector3.right);
            if (!S(x, z - 1)) Quad(v, t, V(c00, h00), V(c00, -T), V(c10, -T), V(c10, h10), Vector3.back);
            if (!S(x, z + 1)) Quad(v, t, V(c01, h01), V(c11, h11), V(c11, -T), V(c01, -T), Vector3.forward);
        }
        var m = NewMesh(name);
        m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = o; g.layer = pathLayer;
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = iceFlat;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m; iceCols.Add(mc);
    }
    // 割れ目の板：ます目に合わせず、ふちの点そのままで作る（ギザギザのかどが丸まってこぶになり、閉じた時にすき間が残ったので）。
    //  poly は CrackSlab の形（下のふち n+1 点 → 上のふち n+1 点を逆順）
    //  moving：動く板。模様はふつうワールドの位置で描くので、動くと模様だけその場に残って上の人が床より速く見えた（2026-11-04 てじゃ「床の模様が動く床と連動してない」）
    //   → 止まっている時（まん中）のワールド位置を uv3 に入れて、模様をそこから取る材質（_PatternFromUV3）に。止まっている板とも模様がつながる
    static void CrackSlabMesh(Transform parent, string name, Vector3 o, Vector2[] poly, bool moving = false)
    {
        int n1 = poly.Length / 2; float T = CrackT;
        var bot = poly.Take(n1).ToArray(); var top = poly.Skip(n1).Reverse().ToArray();
        System.Func<Vector2, float, Vector3> V = (q, y) => new Vector3(q.x, y, q.y);
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < n1 - 1; i++)
        {
            Vector2 b0 = bot[i], b1 = bot[i + 1], t1 = top[i + 1], t0 = top[i];
            Quad(v, t, V(b0, MapHeight(b0)), V(b1, MapHeight(b1)), V(t1, MapHeight(t1)), V(t0, MapHeight(t0)), Vector3.up);
            Quad(v, t, V(b0, -T), V(t0, -T), V(t1, -T), V(b1, -T), Vector3.down);
            // 手前・奥のふち（割れ目の壁）
            var nb = new Vector3(b1.y - b0.y, 0f, -(b1.x - b0.x)).normalized; if (nb.z > 0f) nb = -nb;
            Quad(v, t, V(b0, MapHeight(b0)), V(b0, -T), V(b1, -T), V(b1, MapHeight(b1)), nb);
            var nt = new Vector3(t1.y - t0.y, 0f, -(t1.x - t0.x)).normalized; if (nt.z < 0f) nt = -nt;
            Quad(v, t, V(t0, MapHeight(t0)), V(t1, MapHeight(t1)), V(t1, -T), V(t0, -T), nt);
        }
        // 左右のはし
        Quad(v, t, V(bot[0], MapHeight(bot[0])), V(top[0], MapHeight(top[0])), V(top[0], -T), V(bot[0], -T), Vector3.left);
        Quad(v, t, V(bot[n1 - 1], MapHeight(bot[n1 - 1])), V(bot[n1 - 1], -T), V(top[n1 - 1], -T), V(top[n1 - 1], MapHeight(top[n1 - 1])), Vector3.right);
        var m = NewMesh(name);
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = o; g.layer = pathLayer;
        var mat = iceFlat;
        if (moving)
        {
            m.SetUVs(3, v.Select(q => g.transform.TransformPoint(q)).ToList());
            const string mp = "Assets/FuwaStage2/Materials/Course2_3_IceMoving.mat";
            mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!mat) { mat = new Material(iceFlat); AssetDatabase.CreateAsset(mat, mp); }
            mat.CopyPropertiesFromMaterial(iceFlat); mat.SetFloat("_PatternFromUV3", 1f); EditorUtility.SetDirty(mat);
        }
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m; iceCols.Add(mc);
    }
    // 雪の岸：まわりの物（島・つなぎ・流氷・スタートとゴールの島）から ShoreGap 以上はなれて、湖の外側（中心から見て物より外）の所。
    //  水ぎわは水の下からなだらかに上がる（水面で切れて、ふちが自然な形に）。外のはしは下へ落ちる
    //  avoid：となりのコースの物（湖の入口からの XZ の四角）。AvoidGap 以内には岸も水も作らない（2–2 のスタートの島にかぶった）
    public static float AvoidGap = 2f;   // 8→4→2。8 だと I1 と 2–2 のスタートの間に岸が入らず水が切れた（I1 から ShoreGap＋岸＋これ が入るように）
    //  nWater：pts の先頭から何個を水の浅い色（島のまわり）に使うか（スタート・ゴールの島のあたりや動く島の通り道は除く）
    static void MapShore(Transform parent, Transform waterT, Vector3 o, List<Vector2> pts, List<float> rads, Vector2 center, List<Rect> avoid, int nWater)
    {
        System.Func<Vector2, float> AvD = q => { float best = float.MaxValue; foreach (var rc in avoid) { float dx = Mathf.Max(rc.xMin - q.x, 0f, q.x - rc.xMax), dz = Mathf.Max(rc.yMin - q.y, 0f, q.y - rc.yMax); best = Mathf.Min(best, dx * dx + dz * dz); } return Mathf.Sqrt(best); };
        System.Func<Vector2, bool> Near = q => AvD(q) < AvoidGap;
        //  となりのコースの近くは、水を岸で閉じる（水が途中で切れて見えないように。2026-11-04 てじゃ「左下、水がきれてるのがきになる」）：AvoidGap+ShoreClose より近い所は岸にして、AvoidGap で外へ落とす
        const float ShoreClose = 3f;
        float cs = 1f;
        float minX = pts.Min(q => q.x) - ShoreGap - ShoreWidth - 4f, maxX = pts.Max(q => q.x) + ShoreGap + ShoreWidth + 4f;
        float minZ = pts.Min(q => q.y) - ShoreGap - ShoreWidth - 4f, maxZ = pts.Max(q => q.y) + ShoreGap + ShoreWidth + 4f;
        int nx = Mathf.CeilToInt((maxX - minX) / cs), nz = Mathf.CeilToInt((maxZ - minZ) / cs);
        // 中心から見た向きごとの、いちばん遠い物（5°ごと、前後 20° でならす）
        const int NA = 72; var far = new float[NA];
        for (int i = 0; i < pts.Count; i++) { var d = pts[i] - center; int ai = Mathf.FloorToInt((Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 360f) % 360f / 5f) % NA; far[ai] = Mathf.Max(far[ai], d.magnitude + rads[i]); }
        var farS = new float[NA]; for (int i = 0; i < NA; i++) { float mx = 0f; for (int k = -4; k <= 4; k++) mx = Mathf.Max(mx, far[(i + k + NA) % NA]); farS[i] = mx; }
        System.Func<Vector2, float> Dist = q => { float d = float.MaxValue; for (int i = 0; i < pts.Count; i++) d = Mathf.Min(d, (q - pts[i]).magnitude - rads[i]); return d; };
        System.Func<Vector2, float> Far = q => { var d = q - center; float a = (Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 360f) % 360f / 5f; int i0 = Mathf.FloorToInt(a) % NA; float f = a - Mathf.Floor(a); return Mathf.Lerp(farS[i0], farS[(i0 + 1) % NA], f); };
        var hgt = new float[nx + 1, nz + 1];
        for (int x = 0; x <= nx; x++) for (int z = 0; z <= nz; z++)
        {
            var q = new Vector2(minX + x * cs, minZ + z * cs);
            float dist = Dist(q), r = (q - center).magnitude, fr = Far(q);
            // 内側（物の並びより中）は岸にしない
            float av = AvD(q);
            //  岸は「いちばん近い物からの距離」だけで決める（ShoreGap から上がって、ShoreGap＋ShoreWidth で外へ落ちる）。コースの形にそった岸になる（中心から見た向きで決めるのは南西でうまくいかなかった）
            float rise = Mathf.Clamp01((dist - ShoreGap) / 3f);
            float edge = Mathf.Min(Mathf.Clamp01((ShoreGap + ShoreWidth - dist) / 3f), Mathf.Clamp01((av - AvoidGap) / 2f));   // 外のはしで落ちる（となりのコースの手前でも）
            float y = Mathf.Lerp(-1.4f, 0.35f, rise);
            y = Mathf.Lerp(-6f, y, edge);
            hgt[x, z] = y;
        }
        // 水の面：岸の外のはしより内側だけ（四角い水が岸の外に見えないように）
        //  水のシェーダーは「uv × 板の大きさ」をふちからの距離にして浅い色→深い色にするので、メッシュを 1/K にして K 倍に置き、uv に「島からの距離 / K」を入れる（島のまわりが浅く見える）
        const float K = 40f;
        var wv = new List<Vector3>(); var wt = new List<int>(); var wuv = new List<Vector2>();
        var dcache = new float[nx + 1, nz + 1];
        System.Func<Vector2, float> DistW = q => { float d = float.MaxValue; for (int i = 0; i < nWater; i++) d = Mathf.Min(d, (q - pts[i]).magnitude - rads[i]); return d; };
        for (int x = 0; x <= nx; x++) for (int z = 0; z <= nz; z++) dcache[x, z] = Mathf.Clamp(DistW(new Vector2(minX + x * cs, minZ + z * cs)) / K, 0f, 0.5f);
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
        {
            var qc = new Vector2(minX + (x + 0.5f) * cs, minZ + (z + 0.5f) * cs);
            if (Dist(qc) > ShoreGap + ShoreWidth * 0.5f || AvD(qc) < AvoidGap) continue;
            // 岸が水より高い所（水が見えない所）は作らない。となりのコースの近くでも、岸の下に隠れるまで水を続ける（水のふちがギザギザに見えないように）
            if (Mathf.Min(Mathf.Min(hgt[x, z], hgt[x + 1, z]), Mathf.Min(hgt[x + 1, z + 1], hgt[x, z + 1])) > CrackWaterY + 0.05f) continue;
            float x0 = minX + x * cs, z0 = minZ + z * cs;
            Quad(wv, wt, new Vector3(x0, CrackWaterY, z0) / K, new Vector3(x0 + cs, CrackWaterY, z0) / K, new Vector3(x0 + cs, CrackWaterY, z0 + cs) / K, new Vector3(x0, CrackWaterY, z0 + cs) / K, Vector3.up);
            float u00 = dcache[x, z], u10 = dcache[x + 1, z], u11 = dcache[x + 1, z + 1], u01 = dcache[x, z + 1];
            wuv.Add(new Vector2(u00, u00)); wuv.Add(new Vector2(u10, u10)); wuv.Add(new Vector2(u11, u11)); wuv.Add(new Vector2(u01, u01));
        }
        {
            var wm = NewMesh("LakeWater"); wm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; wm.SetVertices(wv); wm.SetUVs(0, wuv); wm.SetTriangles(wt, 0); wm.RecalculateNormals(); wm.RecalculateBounds();
            var wg = new GameObject("LakeWater"); wg.transform.SetParent(waterT, false); wg.transform.localPosition = o; wg.transform.localScale = Vector3.one * K;
            wg.AddComponent<MeshFilter>().sharedMesh = wm;
            var wmat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_Water_Still.mat");
            var wr = wg.AddComponent<MeshRenderer>(); wr.sharedMaterial = wmat ? wmat : crystalMat; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var v = new List<Vector3>(); var t = new List<int>();
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
        {
            float a = hgt[x, z], b = hgt[x + 1, z], c = hgt[x + 1, z + 1], d = hgt[x, z + 1];
            if (Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d)) < CrackWaterY - 0.05f) continue;   // ぜんぶ水の下なら作らない
            float x0 = minX + x * cs, z0 = minZ + z * cs;
            if (Near(new Vector2(x0 + cs * 0.5f, z0 + cs * 0.5f))) continue;
            Quad(v, t, new Vector3(x0, a, z0), new Vector3(x0 + cs, b, z0), new Vector3(x0 + cs, c, z0 + cs), new Vector3(x0, d, z0 + cs), Vector3.up);
        }
        var m = NewMesh("Shore");
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject("Shore"); g.transform.SetParent(parent, false); g.transform.localPosition = o; g.layer = pathLayer;
        var turf = PathMat("Course2_3_SnowTurf", SnowColA);
        var testIce = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/IceTest_IceC.mat");
        if (testIce)
        {
            turf.SetTexture("_GroundTex", testIce.GetTexture("_GroundTex"));
            foreach (var pn in new[] { "_GroundPatch", "_GroundPatchScale", "_GroundMidScale", "_GroundSteps", "_GroundFine", "_GroundFineScale", "_GroundFineFade", "_GroundFineCrisp" }) turf.SetFloat(pn, testIce.GetFloat(pn));
            turf.SetColor("_GroundTintA", testIce.GetColor("_GroundTintA")); turf.SetColor("_GroundTintB", testIce.GetColor("_GroundTintB")); EditorUtility.SetDirty(turf);
        }
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = turf;
        g.AddComponent<MeshCollider>().sharedMesh = m;
    }
    static Vector2 MapRotPt(Vector2 q) { var d = q - MapPivot; float a = MapRot * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a); return MapPivot + new Vector2(d.x * c - d.y * sn, d.x * sn + d.y * c); }
    static Vector2[] MapRotPoly(Vector2[] poly) { return poly.Select(MapRotPt).ToArray(); }
    static void LakeFromMap(Transform root, Transform path, Transform waterT, ref Vector3 p, ref Vector3 dir)
    {
        // ラフの座標を回してから使う（入口の道はまっすぐのまま）
        var sI1 = MapI1; var sRing = MapRing; var sHole = MapRingHole; var sBand = MapBand; var sI4 = MapI4; var sHills = MapHills; var sLinks = MapLinks; var sFloes = MapFloes; var sMA = MapMoveA; var sMB = MapMoveB;
        var origBand = sBand; var origI4 = sI4; var origFloes = sFloes; var origMA = sMA; var origMB = sMB;
        // 先に、長い道から先を曲げる（ラフの座標のまま）
        var bp = sLinks[1].a; float ba = MapBendDeg * Mathf.Deg2Rad, bc = Mathf.Cos(ba), bs = Mathf.Sin(ba);
        System.Func<Vector2, Vector2> Bend = q => { var d = q - bp; return bp + new Vector2(d.x * bc - d.y * bs, d.x * bs + d.y * bc); };
        //  ゴール前の島（I4）とゴールへの道は向きを変えずに、回した先の場所へずらすだけ（2026-11-04 てじゃ「ゴール前の氷の島、角度いらんよ」）。動く島の左のはしは、I4 の近いふちから MapMoveGap あけた所に置き直す
        var i4c = new Vector2(sI4.Average(q => q.x), sI4.Average(q => q.y)); var i4Move = Bend(i4c) - i4c;
        sI4 = sI4.Select(q => q + i4Move).ToArray();
        sBand = sBand.Select(Bend).ToArray(); sFloes = sFloes.Select(Bend).ToArray(); sMA = Bend(sMA);
        { var nI4 = NearestOnPoly(sI4, sMA, out float dI4); var dv = (nI4 - sMA).normalized; sMB = nI4 - dv * (MapFloeR + MapMoveGap); }
        sHills = sHills.Select((h, i) => i == 2 ? (Bend(h.a), Bend(h.b), h.plateau, h.run, h.h) : h).ToArray();
        sLinks = sLinks.Select((l, i) => i == 1 ? (l.a, Bend(l.b), l.hw) : i == 2 ? (l.a + i4Move, l.b + i4Move, l.hw) : l).ToArray();
        var keepLinks = MapLinks; var keepHills = MapHills;
        MapI1 = MapRotPoly(sI1); MapRing = MapRotPoly(sRing); MapRingHole = MapRotPoly(sHole); MapBand = MapRotPoly(sBand); MapI4 = MapRotPoly(sI4);
        MapHills = sHills.Select(h => (MapRotPt(h.a), MapRotPt(h.b), h.plateau, h.run, h.h)).ToArray();
        MapLinks = sLinks.Select(l => (MapRotPt(l.a), MapRotPt(l.b), l.hw)).ToArray();
        MapFloes = MapRotPoly(sFloes); MapMoveA = MapRotPt(sMA); MapMoveB = MapRotPt(sMB);
        try { LakeFromMapBody(root, path, waterT, ref p, ref dir); }
        finally { MapI1 = sI1; MapRing = sRing; MapRingHole = sHole; MapBand = origBand; MapI4 = origI4; MapHills = keepHills; MapLinks = keepLinks; MapFloes = origFloes; MapMoveA = origMA; MapMoveB = origMB; }
    }
    static void LakeFromMapBody(Transform root, Transform path, Transform waterT, ref Vector3 p, ref Vector3 dir)
    {
        var o = p;   // 湖の入口
        // スタートの道 → I1（入口の道を少し広げてから）
        Flare(path, "MapIn", ref p, Vector3.forward, 3f, IntroHW, 3f);
        float inLen = 10.5f - 3f; Strip(path, "MapInStrip", p, Vector3.forward, inLen, 3f, false); p += Vector3.forward * inLen;
        var isl = new GameObject("Islands").transform; isl.SetParent(path, false);
        // つなぎの道の長方形（島からくりぬく）。入口の道も
        var cuts = new List<Vector2[]> { MapRingHole, new[] { new Vector2(-3f, 3f), new Vector2(3f, 3f), new Vector2(3f, 10.5f), new Vector2(-3f, 10.5f) } };
        for (int k = 0; k < MapLinks.Length; k++) cuts.Add(LinkOutline(k));
        MapIsland(isl, "MapI1", o, MapI1, cuts);
        MapIsland(isl, "MapRing", o, MapRing, cuts);
        MapIsland(isl, "MapBand", o, MapBand, cuts);
        MapIsland(isl, "MapI4", o, MapI4, cuts);
        for (int k = 0; k < MapLinks.Length; k++)
        {
            var l = MapLinks[k]; var lp = LinkPts(k);
            if (lp.Count == 2)
            {
                var a3 = o + new Vector3(l.a.x, 0, l.a.y); var d3 = new Vector3(l.b.x - l.a.x, 0, l.b.y - l.a.y);
                Strip(path, "MapLink" + k, a3, d3.normalized, d3.magnitude, l.hw, false);
                Checkpoint(root, "Checkpoint" + (k + 1), a3 + d3 * 0.5f, d3.normalized, l.hw);
            }
            else
            {
                Ribbon(path, "MapLink" + k, lp.Select(q => o + new Vector3(q.x, 0, q.y)).ToList(), l.hw, false);
                int mi = lp.Count / 2; var td = lp[mi + 1] - lp[mi - 1];
                Checkpoint(root, "Checkpoint" + (k + 1), o + new Vector3(lp[mi].x, 0, lp[mi].y), new Vector3(td.x, 0, td.y).normalized, l.hw);
            }
        }
        var fl = new GameObject("Floes").transform; fl.SetParent(path, false);
        for (int k = 0; k < MapFloes.Length; k++) Floe(fl, "MapFloe" + k, o + new Vector3(MapFloes[k].x, 0, MapFloes[k].y), MapFloeR, 1, 7100 + k);
        Floe(fl, "MapFloeMove", o + new Vector3(MapMoveA.x, 0, MapMoveA.y), MapFloeR, 1, 7199);
        // 動く島の行き来の目印（仮）：細い線
        {
            var a3 = o + new Vector3(MapMoveA.x, -0.25f, MapMoveA.y); var b3 = o + new Vector3(MapMoveB.x, -0.25f, MapMoveB.y);
            var mk = GameObject.CreatePrimitive(PrimitiveType.Cube); mk.name = "MoveRouteMark"; mk.transform.SetParent(fl, false);
            mk.transform.localPosition = (a3 + b3) * 0.5f; mk.transform.localRotation = Quaternion.LookRotation(b3 - a3); mk.transform.localScale = new Vector3(0.15f, 0.05f, (b3 - a3).magnitude);
            mk.GetComponent<Renderer>().sharedMaterial = markMat; Object.DestroyImmediate(mk.GetComponent<Collider>());
        }
        // つながっていない島どうしの近さ（跳んで届かないか）を調べる
        var groups = new List<(string n, List<Vector2> pts, float r)>
        {
            ("I1", MapI1.ToList(), 0f), ("Ring", MapRing.ToList(), 0f), ("Band", MapBand.ToList(), 0f), ("I4", MapI4.ToList(), 0f),
            ("F0", new List<Vector2> { MapFloes[0] }, MapFloeR), ("F1", new List<Vector2> { MapFloes[1] }, MapFloeR), ("F2", new List<Vector2> { MapFloes[2] }, MapFloeR),
            ("FMa", new List<Vector2> { MapMoveA }, MapFloeR), ("FMb", new List<Vector2> { MapMoveB }, MapFloeR) };
        var linked = new HashSet<string> { "I1-Ring", "Ring-Band", "Band-F0", "F0-F1", "F1-F2", "F2-FMa", "FMa-FMb", "FMb-I4" };
        string warn = "";
        for (int i = 0; i < groups.Count; i++) for (int j = i + 1; j < groups.Count; j++)
        {
            string key = groups[i].n + "-" + groups[j].n; if (linked.Contains(key)) continue;
            float best = float.MaxValue;
            foreach (var q in groups[i].pts) { float d; if (groups[j].pts.Count > 2) { NearestOnPoly(groups[j].pts.ToArray(), q, out d); } else d = (q - groups[j].pts[0]).magnitude; best = Mathf.Min(best, d); }
            foreach (var q in groups[j].pts) { float d; if (groups[i].pts.Count > 2) { NearestOnPoly(groups[i].pts.ToArray(), q, out d); } else d = (q - groups[i].pts[0]).magnitude; best = Mathf.Min(best, d); }
            best -= groups[i].r + groups[j].r;
            if (best < MapGapMin) warn += " " + key + "=" + best.ToString("F1") + "m";
        }
        if (warn != "") Debug.LogWarning("[2–3] つながっていない島が近い:" + warn);
        // 雪の岸：まわりの物の点（島のふちを 1m おき・つなぎ・流氷・スタートとゴールの島のあたり）
        var sp = new List<Vector2>(); var sr = new List<float>();
        foreach (var poly in new[] { MapI1, MapRing, MapBand, MapI4 })
            for (int i = 0; i < poly.Length; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Length]; int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude)); for (int k = 0; k < n; k++) { sp.Add(Vector2.Lerp(a, b, k / (float)n)); sr.Add(0f); } }
        for (int li = 0; li < MapLinks.Length; li++)
        {
            var lp = LinkPts(li);
            for (int i = 0; i + 1 < lp.Count; i++) { int n = Mathf.Max(1, Mathf.CeilToInt((lp[i + 1] - lp[i]).magnitude)); for (int k = 0; k < n; k++) { sp.Add(Vector2.Lerp(lp[i], lp[i + 1], k / (float)n)); sr.Add(MapLinks[li].hw); } }
            sp.Add(lp[lp.Count - 1]); sr.Add(MapLinks[li].hw);
        }
        foreach (var f in MapFloes) { sp.Add(f); sr.Add(MapFloeR); }
        sp.Add(MapMoveA); sr.Add(MapFloeR);
        int nWater = sp.Count;
        for (int k = 0; k <= 10; k++) { sp.Add(Vector2.Lerp(MapMoveA, MapMoveB, k / 10f)); sr.Add(MapFloeR); }
        for (float z = -12f; z <= 10.5f; z += 1f) { sp.Add(new Vector2(0f, z)); sr.Add(z < -1f ? 4.5f : 3f); }   // スタートの島（半径 4.5m くらい）と橋
        var gEnd = MapLinks[MapLinks.Length - 1].b; var gDir = (MapLinks[MapLinks.Length - 1].b - MapLinks[MapLinks.Length - 1].a).normalized;
        for (float z = 0f; z <= 18f; z += 1f) { sp.Add(gEnd + gDir * z); sr.Add(z > 6f ? 6f : 3f); }   // ゴールの橋と島
        var center = new Vector2(sp.Average(q => q.x), sp.Average(q => q.y));
        // となりのコース（2–1・2–2）の物をよける
        var avoid = new List<Rect>();
        foreach (var nm in new[] { "FuwaCourse2_1", "FuwaCourse2_2" })
        {
            var other = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(gg => gg.name == nm); if (!other) continue;
            foreach (var rr in other.GetComponentsInChildren<Renderer>(true))
            {
                var bb = rr.bounds; if (bb.size.magnitude > 200f) continue;
                // 大きいメッシュ（曲がった道など）は四角だと広すぎるので、頂点ごとに小さい四角（2m おきくらいに間引く）
                var mfl = rr.GetComponent<MeshFilter>();
                if (bb.size.magnitude > 20f && mfl && mfl.sharedMesh && mfl.sharedMesh.isReadable)
                {
                    var vv = mfl.sharedMesh.vertices; var seen = new HashSet<(int, int)>();
                    foreach (var v0 in vv)
                    {
                        var lq = root.InverseTransformPoint(rr.transform.TransformPoint(v0)) - o; var key = (Mathf.RoundToInt(lq.x / 2f), Mathf.RoundToInt(lq.z / 2f));
                        if (!seen.Add(key) || lq.x < -150f || lq.x > 150f || lq.z < -60f || lq.z > 200f) continue;
                        avoid.Add(Rect.MinMaxRect(lq.x - 1f, lq.z - 1f, lq.x + 1f, lq.z + 1f));
                    }
                    continue;
                }
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (var cx in new[] { bb.min.x, bb.max.x }) foreach (var cz in new[] { bb.min.z, bb.max.z })
                { var lq = root.InverseTransformPoint(new Vector3(cx, bb.center.y, cz)) - o; x0 = Mathf.Min(x0, lq.x); x1 = Mathf.Max(x1, lq.x); z0 = Mathf.Min(z0, lq.z); z1 = Mathf.Max(z1, lq.z); }
                if (x1 < -150f || x0 > 150f || z1 < -60f || z0 > 200f) continue;
                avoid.Add(Rect.MinMaxRect(x0, z0, x1, z1));
            }
        }
        MapShore(path, waterT, o, sp, sr, center, avoid, nWater);
        // 水のアウト（見た目は MapShore の水の面）：岸のいちばん外まで
        float wMinX = sp.Min(q => q.x) - ShoreGap - ShoreWidth, wMaxX = sp.Max(q => q.x) + ShoreGap + ShoreWidth;
        float wMinZ = -6f, wMaxZ = sp.Max(q => q.y) + ShoreGap + ShoreWidth;   // 手前はスタートの島の手前まで（となりの 2–2 のスタートの下まで広げない）
        var wc = o + new Vector3((wMinX + wMaxX) * 0.5f, CrackWaterY, (wMinZ + wMaxZ) * 0.5f); float ww = wMaxX - wMinX, wl = wMaxZ - wMinZ;
        Hazard(waterT, "LakeWater_BallOut", wc, new Vector3(ww, 0.3f, wl), false);
        Hazard(waterT, "LakeWater_PlayerOut", wc + Vector3.down * 0.9f, new Vector3(ww, 0.6f, wl), true);
        // 出口：I4 → ゴールの橋のつなぎの先
        p = o + new Vector3(gEnd.x, 0, gEnd.y); dir = new Vector3(gDir.x, 0, gDir.y);
    }
    // ---- ラフ 2 回目（2026-11-04 てじゃ「隣と近くてきつい、構成かえる」）：縦長。まわりの水・雪の岸はなし（2–1 みずのこみちとかぶる）、島の間は空（落ちたらアウト）、水は島の穴の中だけ。
    //  座標はラフの絵（上から撮った 2–3 の画像に描いたもの）から、入口の道のはしを原点に X 右・Z 奥（m）。回さない
    //  ① 下の大きい氷（切れこみの水・小さい穴）→ 曲がった道 → ② 丸い山の島（まん中が高いドーム）→ 曲がった道 → ③ だ円の輪っか（穴、オレンジの坂＝まん中がいちばん高くて両はしへ下る）
    //  → 道 → ④ 上の大きい氷（穴 2 つ）→ 流氷 3 つ → 上下に動く島 → ⑤ ゴール前の氷 → 道 → ゴール
    public static int MapVersion = 2;
    public static float Map2EntryLen = 1.5f;
    public static Vector2[] M2_I1 = { new Vector2(-24.2f, 37.5f), new Vector2(-10.35f, 53.08f), new Vector2(11.58f, 47.31f), new Vector2(20.23f, 54.23f), new Vector2(28.31f, 49.62f), new Vector2(20.81f, 35.77f), new Vector2(13.89f, 28.27f), new Vector2(16.77f, 18.46f), new Vector2(8.12f, 0.57f), new Vector2(-10.5f, 0.57f), new Vector2(-15.5f, 14f), new Vector2(-23f, 29f) };   // 左下を細く（2026-11-04 てじゃ「この赤いとこいらんかな」）
    // 切れこみの水から右下へ、島を切って外までつなぐ（てじゃの赤いところ）。下の島は左側だけでつながる「C」の形に
    public static bool M2_I1Water = false, M2_RingPond = false, M2_RingAsBand = true;
    public static float M2_RingBandHW = 8f, M2_RingBandExt = 5f;
    public static int M2_ObsCount = 22, M2_ObsSeed = 2304; public static float M2_ObsGapStart = 9f, M2_ObsGapEnd = 3.6f;
    public static Vector2 M2_I1BandA = new Vector2(-2.6f, 30.2f), M2_I1BandB = new Vector2(30f, 10.2f); public static float M2_I1BandHW = 6.5f;
    public static Vector2[] M2_I1Cut = { new Vector2(-15.54f, 38.08f), new Vector2(-5.73f, 41.54f), new Vector2(4.65f, 36.34f), new Vector2(13.89f, 30.57f), new Vector2(12.73f, 25.96f), new Vector2(3.5f, 27.11f), new Vector2(-10.35f, 31.73f) };
    public static Vector2[] M2_I1Hole = { new Vector2(12.73f, 38.65f), new Vector2(11.96f, 36.28f), new Vector2(9.94f, 34.81f), new Vector2(7.44f, 34.81f), new Vector2(5.43f, 36.28f), new Vector2(4.65f, 38.65f), new Vector2(5.43f, 41.03f), new Vector2(7.44f, 42.49f), new Vector2(9.94f, 42.49f), new Vector2(11.96f, 41.03f) };
    public static Vector2 M2_MC = new Vector2(2.35f, 71.54f); public static float M2_MR = 13.27f;
    public static Vector2 M2_RC = new Vector2(-11.27f, 99f); public static float M2_RRx = 22.5f, M2_RRy = 12.1f, M2_RRot = 33f;   // だ円の輪っか（Rot：+X から左回りの角度）
    public static Vector2 M2_RHC = new Vector2(-6.9f, 96.4f); public static float M2_RHx = 12.1f, M2_RHy = 4.4f;
    public static Vector2[] M2_T = { new Vector2(-40.93f, 130.4f), new Vector2(-36.89f, 135.01f), new Vector2(-21.89f, 137.32f), new Vector2(-5.73f, 133.86f), new Vector2(8.12f, 133.86f), new Vector2(12.73f, 129.24f), new Vector2(12.73f, 124.05f), new Vector2(5.81f, 120.01f), new Vector2(-11.5f, 117.7f), new Vector2(-28.81f, 118.86f), new Vector2(-40.35f, 123.47f) };
    public static Vector2[] M2_TH1 = { new Vector2(-29.97f, 135.01f), new Vector2(-21.89f, 135.01f), new Vector2(-21.89f, 128.09f), new Vector2(-29.97f, 128.09f) };
    public static Vector2[] M2_TH2 = { new Vector2(-14.96f, 126.93f), new Vector2(-6.89f, 126.93f), new Vector2(-6.89f, 120.01f), new Vector2(-14.96f, 120.01f) };
    // ゴール前の氷：ラフより 3.5m 奥（上の大きい氷から跳んで届かないように）
    // 2026-11-04 てじゃ「坂のぼり降り以降の展開がびみょう」→「C（動く氷をメイン）」「かなりゆっくり」：上の島 → ゆっくり動く氷の島 3 つ（左右→前後→左右）→ ゴール前の氷
    public static bool M2_Movers = false;   // 2026-11-04 てじゃ「島わたり自体厳しそう」で割れ目の道（M2_Cracks）に
    // 割れ目ができたり閉じたりする大きい氷の道（てじゃ「大きめの島で区切って、割れ目ができたり閉じたり」）：幅 X0〜X1、Z0 から M2_CrackN 枚、
    //  動かない板と前後に動く板が交互（1・3 枚目が動く）。動く板が奥へ寄ると手前の割れ目が開いて奥が閉じる（0〜2×g0）。割れ目はギザギザでかみ合う
    public static bool M2_Cracks = true;
    public static float M2_CrackX0 = -28f, M2_CrackX1 = -2f, M2_CrackZ0 = 139.5f, M2_CrackSlabL = 7f, M2_CrackGap = 2.5f, M2_CrackPeriod = 14f, M2_CrackZig = 0.8f, M2_CrackZigLen = 2.6f;
    public static int M2_CrackN = 5;
    static float CrackZig(float x, int c) { float t = (x / M2_CrackZigLen + c * 0.37f) % 1f; if (t < 0f) t += 1f; return M2_CrackZig * (Mathf.Abs(t * 2f - 1f) * 2f - 1f); }
    // 板 k の形（止まっている時）。下のふちは割れ目 k-1、上のふちは割れ目 k のギザギザ
    static Vector2[] CrackSlab(int k)
    {
        float zb = M2_CrackZ0 + k * (M2_CrackSlabL + M2_CrackGap), zt = zb + M2_CrackSlabL;
        var bot = new List<Vector2>(); var top = new List<Vector2>();
        int n = Mathf.Max(2, Mathf.CeilToInt((M2_CrackX1 - M2_CrackX0) / (M2_CrackZigLen * 0.5f)));
        for (int i = 0; i <= n; i++)
        {
            float x = Mathf.Lerp(M2_CrackX0, M2_CrackX1, i / (float)n);
            bot.Add(new Vector2(x, zb + (k > 0 ? CrackZig(x, k - 1) : 0f)));
            top.Add(new Vector2(x, zt + (k < M2_CrackN - 1 ? CrackZig(x, k) : 0f)));
        }
        top.Reverse(); bot.AddRange(top); return bot.ToArray();
    }
    public static (Vector2 c, Vector2 dir, float amp, float period, float phase)[] M2_Mov = {
        (new Vector2(-20f, 142.5f), new Vector2(1f, 0f), 4f, 22f, 0f),
        (new Vector2(-8f, 151f), new Vector2(0f, 1f), 3f, 18f, 6f),
        (new Vector2(3f, 161f), new Vector2(1f, 0f), 4f, 26f, 3f) };
    // 2026-11-04 てじゃ「ちょとむずすぎそう」→ 島を大きく（半径 3→4.5）・動く幅を小さく（12/8/12→8/6/8m）。はしで次の島と 1m くらいまで近づく
    public static float M2_MovR = 4.5f;
    public static Vector2[] M2_G2 = { new Vector2(7f, 166.5f), new Vector2(12f, 164.5f), new Vector2(20f, 165.5f), new Vector2(23f, 169.5f), new Vector2(20f, 173.5f), new Vector2(12f, 174.5f), new Vector2(7f, 171.5f) };
    public static (Vector2[] pts, float hw) M2_L4b = (new[] { new Vector2(15f, 174f), new Vector2(15f, 182f) }, 3f);
    public static Vector2[] M2_G = { new Vector2(-23.62f, 151.78f), new Vector2(-14.96f, 155.82f), new Vector2(-3.42f, 156.98f), new Vector2(1.19f, 153.51f), new Vector2(-1.12f, 147.17f), new Vector2(-12.66f, 144.86f), new Vector2(-21.31f, 147.17f) };
    public static Vector2[] M2_Floes = { new Vector2(-44.39f, 135.59f), new Vector2(-41.51f, 143.09f), new Vector2(-36.66f, 147.94f) };
    public static Vector2 M2_MoveA = new Vector2(-29.97f, 148.86f), M2_MoveB = new Vector2(-29.97f, 162.71f);
    // つなぎの道（点をなめらかにつなぐ）
    public static (Vector2[] pts, float hw)[] M2_Links = {
        (new[] { new Vector2(21.96f, 52.2f), new Vector2(24.85f, 58.85f), new Vector2(20.23f, 64.04f), new Vector2(14.6f, 66.6f) }, 2.6f),
        (new[] { new Vector2(-9.2f, 74.3f), new Vector2(-18.43f, 75.58f), new Vector2(-26.5f, 80.2f), new Vector2(-30.4f, 86.5f) }, 2.6f),
        (new[] { new Vector2(6.3f, 109.3f), new Vector2(5.81f, 120.3f) }, 3f),
        (new[] { new Vector2(-4.58f, 156.3f), new Vector2(-4.58f, 163.9f) }, 3f) };
    // 丸い山（ドーム）と、オレンジの坂（線にそって、まん中がいちばん高く両はしへ下る。横は SideRun で落ちる）
    public static float M2_DomeH = 3f, M2_DomePlateau = 1.5f, M2_DomeRun = 10f;
    // 最初の島の最後のカーブのあと（上の横向きの所）のゆるい坂：のぼって下りる低い丘（約 12°）（2026-11-04 てじゃ「最後のカーブ後、ちょとゆるい坂にしよ」）
    public static Vector2 M2_I1HillA = new Vector2(8f, 34f), M2_I1HillB = new Vector2(8f, 58f); public static float M2_I1HillPlateau = 1f, M2_I1HillRun = 9f, M2_I1HillH = 2f;
    public static Vector2 M2_RidgeA = new Vector2(-28.24f, 90.01f), M2_RidgeB = new Vector2(2.35f, 110.2f);
    public static float M2_RidgeH = 7f, M2_RidgeHalfW = 8f, M2_RidgeSideRun = 0.5f;   // 横いっぱい（帯の幅 M2_RingBandHW と同じ）。前は 4＋3 で横に細道が残った
    static Vector2[] Ellipse(Vector2 c, float rx, float ry, float rotDeg, int n)
    {
        var o = new Vector2[n]; float a = rotDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        for (int i = 0; i < n; i++) { float t = Mathf.PI * 2f * i / n; float x = rx * Mathf.Cos(t), y = ry * Mathf.Sin(t); o[i] = c + new Vector2(x * ca - y * sa, x * sa + y * ca); }
        return o;
    }
    // なめらかな線（Catmull-Rom、1m くらいおき）
    static List<Vector2> SmoothPts(Vector2[] k)
    {
        if (k.Length < 3) return k.ToList();
        var o = new List<Vector2>();
        for (int i = 0; i + 1 < k.Length; i++)
        {
            Vector2 p0 = k[Mathf.Max(0, i - 1)], p1 = k[i], p2 = k[i + 1], p3 = k[Mathf.Min(k.Length - 1, i + 2)];
            int n = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude));
            for (int j = 0; j < n; j++) { float t = j / (float)n, t2 = t * t, t3 = t2 * t; o.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3)); }
        }
        o.Add(k[k.Length - 1]); return o;
    }
    static Vector2[] PathOutline(List<Vector2> pts, float hw)
    {
        var L = new List<Vector2>(); var R = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++)
        {
            var tg = pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]; tg.Normalize(); var rt = new Vector2(tg.y, -tg.x);
            L.Add(pts[i] - rt * hw); R.Add(pts[i] + rt * hw);
        }
        R.Reverse(); L.AddRange(R); return L.ToArray();
    }
    static float Map2Ridge(Vector2 q)
    {
        var ab = M2_RidgeB - M2_RidgeA; float len = ab.magnitude; var u = ab / len; var d = q - M2_RidgeA;
        float t = Vector2.Dot(d, u) / len, side = Mathf.Abs(d.x * u.y - d.y * u.x);
        if (t < 0f || t > 1f) return 0f;
        float along = 1f - Mathf.Abs(2f * t - 1f);
        float sideF = Mathf.Clamp01((M2_RidgeHalfW + M2_RidgeSideRun - side) / M2_RidgeSideRun);
        return M2_RidgeH * along * sideF;
    }
    // 穴の水（見た目の面＋アウト）：穴の形の外わくの四角で（島の中に隠れる）
    //  見た目は穴の形（まん中からの扇。四角だと島の外にはみ出した）。水のシェーダーは uv×板の大きさをふちからの距離にするので、1/K の形を K 倍に置いて uv はふちで 0・まん中で 0.5 に
    static void HoleWater(Transform waterT, Vector3 o, string name, Vector2[] hole)
    {
        const float K = 8f;
        var c = new Vector2(hole.Average(q => q.x), hole.Average(q => q.y));
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        v.Add(new Vector3(c.x, CrackWaterY, c.y) / K); uv.Add(new Vector2(0.5f, 0.5f));
        foreach (var q in hole) { var e = q + (q - c).normalized * 0.3f; v.Add(new Vector3(e.x, CrackWaterY, e.y) / K); uv.Add(Vector2.zero); }   // ふちは少し外へ（島の側面の中に入れてすき間なし）
        for (int i = 0; i < hole.Length; i++) { int a = 1 + i, b = 1 + (i + 1) % hole.Length; if (Vector3.Cross(v[a] - v[0], v[b] - v[0]).y >= 0) t.AddRange(new[] { 0, a, b }); else t.AddRange(new[] { 0, b, a }); }
        var m = NewMesh(name); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name); g.transform.SetParent(waterT, false); g.transform.localPosition = o; g.transform.localScale = Vector3.one * K;
        g.AddComponent<MeshFilter>().sharedMesh = m;
        var wm = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_Water_Still.mat");
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = wm ? wm : crystalMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        float x0 = hole.Min(q => q.x), x1 = hole.Max(q => q.x), z0 = hole.Min(q => q.y), z1 = hole.Max(q => q.y);
        var wc = o + new Vector3((x0 + x1) * 0.5f, CrackWaterY, (z0 + z1) * 0.5f);
        Hazard(waterT, name + "_BallOut", wc, new Vector3(x1 - x0, 0.3f, z1 - z0), false);
        Hazard(waterT, name + "_PlayerOut", wc + Vector3.down * 0.9f, new Vector3(x1 - x0, 0.6f, z1 - z0), true);
    }
    // 2026-11-04 てじゃ「2-3ちょっとデカすぎる」「スタートゴール以外を縮める」「四角い池があるエリア、いらんかも」：
    //  ・M2_Top＝false で上の島（四角い池 2 つ）をなくして、帯の坂のあとから割れ目の道へまっすぐ
    //  ・M2_Scale 倍に縮める（入口からの位置・島の形・坂の長さと高さ＝坂の角度は同じ）。道の幅・置き物のすき間・割れ目の開く幅・ギザギザは遊びやすさのため縮めない
    //  データはラフを写した大きさのまま。作る間だけ縮めた値に入れかえて、最後に戻す
    public static bool M2_Top = false;
    public static float M2_Scale = 0.75f;
    public static float M2_FieldLink = 14f;   // 上の島なしの時：帯から割れ目の道への道の長さ（L3 の始まりから）。8 にすると帯のはしの角が 1 枚目の板に 0.3m まで近づいた（2026-11-05）
    // つなぎの道を短く（縮めたあとの長さで M2_LinkCut[k] m 短く）：その先の島・道を道の向きに手前へ寄せる。曲がりの形はそのまま縮める
    // 2026-11-05 てじゃ「島同士をつなぐ道が無駄に長く感じるなあ」→ 最初の島→丸い山 15.5→10m、丸い山→坂の帯 19.6→8.6m（中心線）。
    //  寄せると坂の帯が最初の島に近づく：{7,11} だと 4.3m で跳んで届いたので、すき間 6.8m になる {5.5,11} に
    public static float[] M2_LinkCut = { 0f, 0f };   // 相談中：寄せると島どうしが道以外の所で近づく（{5.5,11} で 0.2m・0.1m まで）
    static float SmoothLen(Vector2[] k) { var q = SmoothPts(k); float l = 0f; for (int i = 0; i + 1 < q.Count; i++) l += (q[i + 1] - q[i]).magnitude; return l; }
    // つなぎの道 k を cut m 短くした点と、その先を寄せる量
    static Vector2[] ShrinkLink(Vector2[] pts, float cut, out Vector2 shift)
    {
        int n = pts.Length; var cum = new float[n]; for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
        float L0 = SmoothLen(pts), target = Mathf.Max(1f, L0 - cut); var chord = pts[n - 1] - pts[0];
        Vector2[] Make(float f) => pts.Select((q, i) => q - chord * f * (cum[i] / cum[n - 1])).ToArray();
        float a = 0f, b = 0.98f; for (int it = 0; it < 30; it++) { float m = (a + b) * 0.5f; if (SmoothLen(Make(m)) > target) a = m; else b = m; }
        shift = -chord * a; return Make(a);
    }
    // ---- 2026-11-05 の配置（てじゃの絵）：島どうしの道をまっすぐ短く（約6〜7m）、3島目は坂をやめてシーソー ----
    //  てじゃ「島同士をつなぐ道が無駄に長く感じる」→ 案C（道の付け方を変える）→ 絵「形はこんな感じにしたい、3島目の坂をちょっと中身を変えたい、ずっと坂ギミックだし」→ 床の傾きが変わるシーソー（案A）
    //  最初の島の出口に小さい出っぱり → 道 → 丸い山（右へ寄せた）→ 道 → 小さい丸い足場 → 斜めの板（シーソー）→ 角の丸い足場 → まっすぐ上の板（シーソー）→ 丸い足場 → 道 → 割れ目の道
    //  座標は縮めたあと（M2_Scale をかけたあと）の値。I1 は出口に出っぱり（中心 (16.6,40.6) 半径 2.6）をつけた形
    public static bool M2_V3 = true;
    static readonly Vector2[] V3_I1 = { new Vector2(-18.15f, 28.12f), new Vector2(-7.76f, 39.81f), new Vector2(8.69f, 35.48f), new Vector2(15.17f, 40.67f), new Vector2(14.16f, 41.49f), new Vector2(14.35f, 41.90f), new Vector2(14.61f, 42.27f), new Vector2(14.93f, 42.59f), new Vector2(15.30f, 42.85f), new Vector2(15.71f, 43.04f), new Vector2(16.15f, 43.16f), new Vector2(16.60f, 43.20f), new Vector2(17.05f, 43.16f), new Vector2(17.49f, 43.04f), new Vector2(17.90f, 42.85f), new Vector2(18.27f, 42.59f), new Vector2(18.59f, 42.27f), new Vector2(18.85f, 41.90f), new Vector2(19.04f, 41.49f), new Vector2(19.16f, 41.05f), new Vector2(19.20f, 40.60f), new Vector2(19.16f, 40.15f), new Vector2(19.04f, 39.71f), new Vector2(18.85f, 39.30f), new Vector2(18.59f, 38.93f), new Vector2(21.23f, 37.22f), new Vector2(15.61f, 26.83f), new Vector2(10.42f, 21.20f), new Vector2(12.58f, 13.85f), new Vector2(6.09f, 0.43f), new Vector2(-7.88f, 0.43f), new Vector2(-11.62f, 10.50f), new Vector2(-17.25f, 21.75f) };
    public static Vector2 V3_Nub = new Vector2(16.6f, 40.6f); public static float V3_NubR = 2.6f;
    public static Vector2 V3_DomeC = new Vector2(5.3f, 55.3f);
    public static Vector2 V3_Pad0 = new Vector2(-5.4f, 72.3f); public static float V3_PadR = 2.6f, V3_HubR = 3.5f;
    public static Vector2 V3_TiltDir = new Vector2(14.9f, 20.7f); public static float V3_HubDist = 25.5f;   // 斜めの板の向き・入口の足場の中心から角の足場の中心まで
    public static float V3_PanelHW = 5f, V3_PanelB = 9f, V3_PanelGap = 0.05f, V3_FieldLink = 6f, V3_LinkIn = 1.2f;   // 板の半分の幅・まっすぐ上の板の長さ・足場とのすき間・割れ目の道への道の長さ・道が島に入りこむ長さ
    public static float V3_TiltDeg = 10f, V3_TiltPeriod = 8f;   // シーソーの傾き（±度）と 1 往復の秒数。2 枚は半周ずらす
    // 角の足場の中心・出口の足場の中心
    static Vector2 V3Hub() { var u = V3_TiltDir.normalized; return V3_Pad0 + u * V3_HubDist; }
    static Vector2 V3PadTop() { var h = V3Hub(); return new Vector2(h.x, h.y + V3_HubR + V3_PanelGap + V3_PanelB + V3_PanelGap + V3_PadR); }
    static void M2ApplyV3()
    {
        M2_I1 = V3_I1; M2_MC = V3_DomeC;
        Vector2 C = M2_MC; float R = M2_MR;
        var d0 = (C - V3_Nub).normalized; var d1 = (V3_Pad0 - C).normalized;
        var top = V3PadTop();
        var l0 = (new[] { V3_Nub + d0 * (V3_NubR - V3_LinkIn), C - d0 * (R - V3_LinkIn) }, 3f);
        var l1 = (new[] { C + d1 * (R - V3_LinkIn), V3_Pad0 - d1 * (V3_PadR - V3_LinkIn) }, 3f);
        var l2 = (new[] { new Vector2(top.x, top.y + V3_PadR - V3_LinkIn), new Vector2(top.x, top.y + V3_PadR + V3_FieldLink) }, 3f);
        M2_Links = new[] { l0, l1, l2, l2 };   // 2 番は割れ目の道への道の始まり（作る所で toField に置きかわる）、3 番は使わない
        float w = M2_CrackX1 - M2_CrackX0;
        M2_CrackX0 = top.x - w * 0.5f; M2_CrackX1 = top.x + w * 0.5f; M2_CrackZ0 = top.y + V3_PadR + V3_FieldLink;
    }
    // シーソーの板：s から u の向きに長さ len、半分の幅 hw。まん中を軸に傾ける親（FuwaTiltIce）の下に、止まっている時の形で作る
    static Vector2[] TiltPanelPoly(Vector2 s, Vector2 u, float len, float hw) { var n = new Vector2(-u.y, u.x) * hw; var e = s + u * len; return new[] { s - n, e - n, e + n, s + n }; }
    static void TiltPanel(Transform parent, string name, Vector3 o, Vector2 s, Vector2 u, float len, float hw, float phase)
    {
        var c2 = s + u * (len * 0.5f);
        var pv = new GameObject(name).transform; pv.SetParent(parent, false);
        pv.localPosition = o + new Vector3(c2.x, 0f, c2.y);
        var baseRot = Quaternion.LookRotation(new Vector3(u.x, 0f, u.y)); pv.localRotation = baseRot;
        float T = CrackT, hl = len * 0.5f;
        var v = new List<Vector3>(); var t = new List<int>();
        Vector3 a = new Vector3(-hw, 0, -hl), b = new Vector3(hw, 0, -hl), c = new Vector3(hw, 0, hl), d = new Vector3(-hw, 0, hl), dn = Vector3.down * T;
        Quad(v, t, a, d, c, b, Vector3.up);
        Quad(v, t, a + dn, b + dn, c + dn, d + dn, Vector3.down);
        Quad(v, t, a, b, b + dn, a + dn, Vector3.back); Quad(v, t, c, d, d + dn, c + dn, Vector3.forward);
        Quad(v, t, b, c, c + dn, b + dn, Vector3.right); Quad(v, t, d, a, a + dn, d + dn, Vector3.left);
        var m = NewMesh(name);
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name + "Ice"); g.transform.SetParent(pv, false); g.layer = pathLayer;
        // 動く板と同じ：止まっている時のワールド位置を uv3 に入れて、模様を板にくっつける
        m.SetUVs(3, v.Select(q => g.transform.TransformPoint(q)).ToList());
        const string mp = "Assets/FuwaStage2/Materials/Course2_3_IceMoving.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!mat) { mat = new Material(iceFlat); AssetDatabase.CreateAsset(mat, mp); }
        mat.CopyPropertiesFromMaterial(iceFlat); mat.SetFloat("_PatternFromUV3", 1f); EditorUtility.SetDirty(mat);
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m; iceCols.Add(mc);
        var ti = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTiltIce>(pv.gameObject);
        ti.amplitude = V3_TiltDeg; ti.period = V3_TiltPeriod; ti.phase = phase; ti.baseRotation = baseRot;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ti); FuwaStage2WaterTest.NoSync(ti);
    }
    static void M2Shrink()
    {
        var links = M2_Links.ToArray();
        for (int k = 0; k < M2_LinkCut.Length && k < links.Length; k++)
        {
            if (M2_LinkCut[k] <= 0f) continue;
            float before = SmoothLen(links[k].pts);
            links[k] = (ShrinkLink(links[k].pts, M2_LinkCut[k], out var sh), links[k].hw);
            // その先：丸い山（最初の道のあとだけ）、あとのつなぎの道、坂の帯（輪っか）と坂
            if (k == 0) M2_MC += sh;
            for (int j = k + 1; j < links.Length; j++) links[j] = (links[j].pts.Select(q => q + sh).ToArray(), links[j].hw);
            M2_RC += sh; M2_RHC += sh; M2_RidgeA += sh; M2_RidgeB += sh;
            Debug.Log("[2–3] つなぎの道" + k + " " + before.ToString("F1") + "→" + SmoothLen(links[k].pts).ToString("F1") + "m（中心線）");
        }
        M2_Links = links;
    }
    static System.Action M2ApplyScale(float k)
    {
        var bI1 = M2_I1; var bBA = M2_I1BandA; var bBB = M2_I1BandB; float bBHW = M2_I1BandHW; var bCut = M2_I1Cut; var bHole = M2_I1Hole;
        var bMC = M2_MC; float bMR = M2_MR; var bRC = M2_RC; float bRRx = M2_RRx, bRRy = M2_RRy; var bRHC = M2_RHC; float bRHx = M2_RHx, bRHy = M2_RHy;
        float bRBHW = M2_RingBandHW, bRBE = M2_RingBandExt; var bLinks = M2_Links;
        float bDH = M2_DomeH, bDP = M2_DomePlateau, bDR = M2_DomeRun; var bHA = M2_I1HillA; var bHB = M2_I1HillB; float bHP = M2_I1HillPlateau, bHR = M2_I1HillRun, bHH = M2_I1HillH;
        var bRA = M2_RidgeA; var bRB = M2_RidgeB; float bRH = M2_RidgeH, bRHW = M2_RidgeHalfW;
        float bX0 = M2_CrackX0, bX1 = M2_CrackX1, bZ0 = M2_CrackZ0, bSL = M2_CrackSlabL;
        System.Func<Vector2[], Vector2[]> P = a => a.Select(q => q * k).ToArray();
        M2_I1 = P(M2_I1); M2_I1BandA *= k; M2_I1BandB *= k; M2_I1BandHW *= k; M2_I1Cut = P(M2_I1Cut); M2_I1Hole = P(M2_I1Hole);
        M2_MC *= k; M2_MR *= k; M2_RC *= k; M2_RRx *= k; M2_RRy *= k; M2_RHC *= k; M2_RHx *= k; M2_RHy *= k;
        M2_RingBandHW *= k; M2_RingBandExt *= k; M2_Links = M2_Links.Select(l => (P(l.pts), l.hw)).ToArray();
        M2_DomeH *= k; M2_DomePlateau *= k; M2_DomeRun *= k; M2_I1HillA *= k; M2_I1HillB *= k; M2_I1HillPlateau *= k; M2_I1HillRun *= k; M2_I1HillH *= k;
        M2_RidgeA *= k; M2_RidgeB *= k; M2_RidgeH *= k; M2_RidgeHalfW *= k;
        float w = (M2_CrackX1 - M2_CrackX0) * k; M2_CrackSlabL *= k;
        if (M2_V3) M2ApplyV3();
        else if (!M2_Top && !M2_Movers) M2Shrink();   // 上の島・動く島の版は寄せる物が足りないので縮めない
        if (M2_V3) { }
        else if (!M2_Top) { var s0 = M2_Links[2].pts[0]; M2_CrackX0 = s0.x - w * 0.5f; M2_CrackX1 = s0.x + w * 0.5f; M2_CrackZ0 = s0.y + M2_FieldLink; }
        else { float c = (M2_CrackX0 + M2_CrackX1) * 0.5f * k; M2_CrackX0 = c - w * 0.5f; M2_CrackX1 = c + w * 0.5f; M2_CrackZ0 *= k; }
        return () =>
        {
            M2_I1 = bI1; M2_I1BandA = bBA; M2_I1BandB = bBB; M2_I1BandHW = bBHW; M2_I1Cut = bCut; M2_I1Hole = bHole;
            M2_MC = bMC; M2_MR = bMR; M2_RC = bRC; M2_RRx = bRRx; M2_RRy = bRRy; M2_RHC = bRHC; M2_RHx = bRHx; M2_RHy = bRHy;
            M2_RingBandHW = bRBHW; M2_RingBandExt = bRBE; M2_Links = bLinks;
            M2_DomeH = bDH; M2_DomePlateau = bDP; M2_DomeRun = bDR; M2_I1HillA = bHA; M2_I1HillB = bHB; M2_I1HillPlateau = bHP; M2_I1HillRun = bHR; M2_I1HillH = bHH;
            M2_RidgeA = bRA; M2_RidgeB = bRB; M2_RidgeH = bRH; M2_RidgeHalfW = bRHW;
            M2_CrackX0 = bX0; M2_CrackX1 = bX1; M2_CrackZ0 = bZ0; M2_CrackSlabL = bSL;
        };
    }
    static void LakeFromMap2(Transform root, Transform path, Transform waterT, ref Vector3 p, ref Vector3 dir)
    {
        var o = p;
        var unscale = M2ApplyScale(M2_Scale);
        var keepHills = MapHills; MapHills = new[] { (M2_MC, M2_MC, M2_DomePlateau, M2_DomeRun, M2_DomeH), (M2_I1HillA, M2_I1HillB, M2_I1HillPlateau, M2_I1HillRun, M2_I1HillH) }; map2Ridge = !M2_V3;
        MapHillOnly = new[] { Ellipse(M2_MC, M2_MR, M2_MR, 0f, 40), M2_I1 };
        try
        {
            // 入口の道 → 下の大きい氷
            Strip(path, "MapInStrip", p, Vector3.forward, Map2EntryLen, IntroHW, false); p += Vector3.forward * Map2EntryLen;
            var R = Ellipse(M2_RC, M2_RRx, M2_RRy, M2_RRot, 40); var RH = Ellipse(M2_RHC, M2_RHx, M2_RHy, M2_RRot, 28); var M = Ellipse(M2_MC, M2_MR, M2_MR, 0f, 40);
            var links = M2_Links.ToArray(); if (M2_Movers) links[links.Length - 1] = M2_L4b;
            var G = M2_Movers ? M2_G2 : M2_G;
            if (M2_Cracks)
            {
                // 上の島 → 1 枚目の板（まっすぐの道）、最後の板 → ゴール
                float xm = (M2_CrackX0 + M2_CrackX1) * 0.5f, zEnd = M2_CrackZ0 + M2_CrackN * M2_CrackSlabL + (M2_CrackN - 1) * M2_CrackGap;
                var toField = M2_Top ? (new[] { new Vector2(xm, 134.8f * M2_Scale), new Vector2(xm, M2_CrackZ0) }, 3f) : (new[] { new Vector2(xm, M2_Links[2].pts[0].y), new Vector2(xm, M2_CrackZ0) }, 3f);
                var toGoal = (new[] { new Vector2(xm, zEnd), new Vector2(xm, zEnd + 8f) }, 3f);
                var ll = links.ToList(); ll[ll.Count - 1] = toField; ll.Add(toGoal);
                if (!M2_Top) ll.RemoveAt(2);   // 上の島なし：帯 → 割れ目の道（toField が L3 の始まりから）
                links = ll.ToArray();
                G = CrackSlab(M2_CrackN - 1);
            }
            var linkPts = links.Select(l => SmoothPts(l.pts)).ToList();
            Vector2[] I1Band;
            { var d = (M2_I1BandB - M2_I1BandA).normalized; var nn = new Vector2(-d.y, d.x) * M2_I1BandHW; I1Band = new[] { M2_I1BandA - nn, M2_I1BandA + nn, M2_I1BandB + nn, M2_I1BandB - nn }; }
            // 下の島の水（切れこみ・小さい穴）は一旦なし（2026-11-04 てじゃ「手前の水は一旦けしていいよ」）。M2_I1Water で戻せる
            var cuts = new List<Vector2[]> { I1Band, RH, new[] { new Vector2(-IntroHW, 0f), new Vector2(IntroHW, 0f), new Vector2(IntroHW, Map2EntryLen), new Vector2(-IntroHW, Map2EntryLen) } };
            if (M2_I1Water) { cuts.Add(M2_I1Cut); cuts.Add(M2_I1Hole); }
            if (M2_Top) { cuts.Add(M2_TH1); cuts.Add(M2_TH2); }
            if (M2_RingAsBand && !M2_RingPond) cuts.Remove(RH);   // 帯にした時は池の穴もなし
            if (!M2_RingAsBand)
            // 輪っかの大きい坂の横の細道をなくす（2026-11-04 てじゃ「でかい坂のよこの細道いらんかもな」）：坂のすその外側（輪っかの外のふち側）を、坂の長さぶん切る
            {
                var ab = M2_RidgeB - M2_RidgeA; var u = ab.normalized; var nn = new Vector2(-u.y, u.x); float w0 = M2_RidgeHalfW + M2_RidgeSideRun, w1 = w0 + 20f;
                var a0 = M2_RidgeA + u * 0.5f; var b0 = M2_RidgeB - u * 0.5f;
                cuts.Add(new[] { a0 + nn * w0, b0 + nn * w0, b0 + nn * w1, a0 + nn * w1 });
                // 穴の側（池のほう）も切る（2026-11-04 てじゃ「右の池のほうもいらんね」）→ 輪っかは坂の道だけ。池の水もなし
                if (!M2_RingPond) cuts.Add(new[] { a0 - nn * w0, a0 - nn * w1, b0 - nn * w1, b0 - nn * w0 });
            }
            for (int k = 0; k < linkPts.Count; k++) cuts.Add(PathOutline(linkPts[k], links[k].hw));
            var isl = new GameObject("Islands").transform; isl.SetParent(path, false);
            MapIsland(isl, "MapI1", o, M2_I1, cuts);
            MapIsland(isl, "MapDome", o, M, cuts);
            // 輪っかは坂の道だけの広い帯に（2026-11-04 てじゃ「ニ択やめて広くしよ」）：坂の線にそった長方形（両はしは道とつながるまでのばす）。坂は横いっぱいの幅
            if (M2_RingAsBand)
            {
                var ub = (M2_RidgeB - M2_RidgeA).normalized; var nb = new Vector2(-ub.y, ub.x) * M2_RingBandHW;
                var a1 = M2_RidgeA - ub * M2_RingBandExt; var b1 = M2_RidgeB + ub * M2_RingBandExt;
                R = new[] { a1 - nb, b1 - nb, b1 + nb, a1 + nb };
            }
            if (M2_V3)
            {
                // 3 島目：足場（入口・角・出口）は止まっている丸い島、そのあいだの 2 枚がシーソー（逆向きのタイミング）
                var u = V3_TiltDir.normalized; var hub = V3Hub(); var top = V3PadTop();
                MapIsland(isl, "MapPad0", o, Ellipse(V3_Pad0, V3_PadR, V3_PadR, 0f, 28), cuts);
                MapIsland(isl, "MapHub", o, Ellipse(hub, V3_HubR, V3_HubR, 0f, 32), cuts);
                MapIsland(isl, "MapPad1", o, Ellipse(top, V3_PadR, V3_PadR, 0f, 28), cuts);
                var sA = V3_Pad0 + u * (V3_PadR + V3_PanelGap); float lenA = V3_HubDist - V3_PadR - V3_HubR - 2f * V3_PanelGap;
                var sB = hub + Vector2.up * (V3_HubR + V3_PanelGap);
                TiltPanel(path, "TiltA", o, sA, u, lenA, V3_PanelHW, 0f);
                TiltPanel(path, "TiltB", o, sB, Vector2.up, V3_PanelB, V3_PanelHW, V3_TiltPeriod * 0.5f);
                v3Polys = new List<(string n, Vector2[] poly)> { ("Pad0", Ellipse(V3_Pad0, V3_PadR, V3_PadR, 0f, 28)), ("TiltA", TiltPanelPoly(sA, u, lenA, V3_PanelHW)), ("Hub", Ellipse(hub, V3_HubR, V3_HubR, 0f, 32)), ("TiltB", TiltPanelPoly(sB, Vector2.up, V3_PanelB, V3_PanelHW)), ("Pad1", Ellipse(top, V3_PadR, V3_PadR, 0f, 28)) };
            }
            else MapIsland(isl, "MapRing", o, R, cuts);
            if (M2_Top) MapIsland(isl, "MapTop", o, M2_T, cuts);
            if (M2_Cracks)
            {
                for (int k = 0; k < M2_CrackN; k++)
                {
                    var poly = CrackSlab(k);
                    if (k % 2 == 0) { CrackSlabMesh(isl, "CrackSlab" + k, o, poly); continue; }
                    // 動く板：板のまん中に動く親を置いて、その下に島を作る
                    float zb = M2_CrackZ0 + k * (M2_CrackSlabL + M2_CrackGap);
                    var c3 = o + new Vector3((M2_CrackX0 + M2_CrackX1) * 0.5f, 0f, zb + M2_CrackSlabL * 0.5f);
                    var mv = new GameObject("CrackMover" + k).transform; mv.SetParent(path, false); mv.localPosition = c3;
                    CrackSlabMesh(mv, "CrackSlab" + k, o - c3, poly, true);
                    var mi = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaMovingIce>(mv.gameObject);
                    mi.a = c3 - Vector3.forward * M2_CrackGap; mi.b = c3 + Vector3.forward * M2_CrackGap; mi.period = M2_CrackPeriod; mi.phase = (k / 2) % 2 == 0 ? 0f : M2_CrackPeriod * 0.5f;
                    UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(mi); FuwaStage2WaterTest.NoSync(mi);
                }
            }
            else MapIsland(isl, "MapGoalIce", o, G, cuts);
            for (int k = 0; k < linkPts.Count; k++)
            {
                var lp = linkPts[k]; float hw = links[k].hw;
                if (lp.Count == 2)
                {
                    var a3 = o + new Vector3(lp[0].x, 0, lp[0].y); var d3 = new Vector3(lp[1].x - lp[0].x, 0, lp[1].y - lp[0].y);
                    Strip(path, "MapLink" + k, a3, d3.normalized, d3.magnitude, hw, false);
                    if (k < linkPts.Count - 1) Checkpoint(root, "Checkpoint" + (k + 1), a3 + d3 * 0.5f, d3.normalized, hw);
                }
                else
                {
                    Ribbon(path, "MapLink" + k, lp.Select(q => o + new Vector3(q.x, 0, q.y)).ToList(), hw, false);
                    int mi = lp.Count / 2; var td = lp[mi + 1] - lp[mi - 1];
                    Checkpoint(root, "Checkpoint" + (k + 1), o + new Vector3(lp[mi].x, 0, lp[mi].y), new Vector3(td.x, 0, td.y).normalized, hw);
                }
            }
            // 最初の島の置き物（2026-11-04 てじゃ「最初の島、すこしオブジェクトを置いていこう」）：氷の岩と結晶の柱を、ふち・入口・出口から離して、おたがい M2_ObsGap 以上あけて乱数で
            {
                var obsT = new GameObject("Obstacles").transform; obsT.SetParent(root, false);
                var rnd = new System.Random(M2_ObsSeed); var placed = new List<Vector2>();
                var cand = new List<Vector2>();
                for (float x = -30f * M2_Scale; x <= 30f * M2_Scale; x += 1f) for (float z = 0f; z <= 56f * M2_Scale; z += 1f) cand.Add(new Vector2(x + (float)rnd.NextDouble() * 0.8f - 0.4f, z + (float)rnd.NextDouble() * 0.8f - 0.4f));
                for (int i = cand.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var tmp = cand[i]; cand[i] = cand[j]; cand[j] = tmp; }
                var exitA = linkPts[0][0];
                foreach (var q in cand)
                {
                    if (placed.Count >= M2_ObsCount) break;
                    if (!InPoly(M2_I1, q) || InPoly(I1Band, q)) continue;
                    NearestOnPoly(M2_I1, q, out float de); if (de < 2.5f) continue;
                    NearestOnPoly(I1Band, q, out float db); if (db < 2.5f) continue;
                    if ((q - new Vector2(0f, Map2EntryLen)).magnitude < 7f || (q - exitA).magnitude < 7f) continue;
                    // 後半ほどつめる（てじゃ「後半ほど難しくするかんじに」）：進み具合は Z（スタート 0 → 出口 55 くらい）
                    float prog = Mathf.Clamp01(q.y / (55f * M2_Scale)), gap = Mathf.Lerp(M2_ObsGapStart, M2_ObsGapEnd, prog);
                    if (placed.Any(pq => (pq - q).magnitude < gap)) continue;
                    placed.Add(q);
                    var at = o + new Vector3(q.x, MapHeight(q), q.y);
                    if (placed.Count % 3 == 0) Crystal(obsT, "Crystal" + placed.Count, at, 0.5f + (float)rnd.NextDouble() * 0.3f, 2.2f + (float)rnd.NextDouble() * 1.2f);
                    else IceRock(obsT, "IceRock" + placed.Count, at, 1.3f + (float)rnd.NextDouble() * 0.8f, (float)rnd.NextDouble() * 360f);
                }
            }
            var fl = new GameObject("Floes").transform; fl.SetParent(path, false);
            if (M2_Movers)
            {
                for (int k = 0; k < M2_Mov.Length; k++)
                {
                    var mv = M2_Mov[k]; var c3 = o + new Vector3(mv.c.x, 0, mv.c.y); var d3 = new Vector3(mv.dir.x, 0, mv.dir.y).normalized * mv.amp;
                    Floe(fl, "MovingIce" + k, c3, M2_MovR, 1, 7400 + k);
                    var mg = fl.Find("MovingIce" + k).gameObject;
                    var mi = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaMovingIce>(mg);
                    mi.a = c3 - d3; mi.b = c3 + d3; mi.period = mv.period; mi.phase = mv.phase;
                    UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(mi); FuwaStage2WaterTest.NoSync(mi);
                    // 動く道すじの目印（仮）
                    var mk = GameObject.CreatePrimitive(PrimitiveType.Cube); mk.name = "MoveRouteMark" + k; mk.transform.SetParent(fl, false);
                    mk.transform.localPosition = c3 + Vector3.down * 0.9f; mk.transform.localRotation = Quaternion.LookRotation(d3); mk.transform.localScale = new Vector3(0.12f, 0.04f, d3.magnitude * 2f + M2_MovR * 2f);
                    mk.GetComponent<Renderer>().sharedMaterial = markMat; Object.DestroyImmediate(mk.GetComponent<Collider>());
                }
            }
            else if (!M2_Cracks)
            {
            for (int k = 0; k < M2_Floes.Length; k++) Floe(fl, "MapFloe" + k, o + new Vector3(M2_Floes[k].x, 0, M2_Floes[k].y), MapFloeR, 1, 7300 + k);
            Floe(fl, "MapFloeMove", o + new Vector3(M2_MoveA.x, 0, M2_MoveA.y), MapFloeR, 1, 7399);
            }
            if (!M2_Movers && !M2_Cracks)
            {
                var a3 = o + new Vector3(M2_MoveA.x, -0.25f, M2_MoveA.y); var b3 = o + new Vector3(M2_MoveB.x, -0.25f, M2_MoveB.y);
                var mk = GameObject.CreatePrimitive(PrimitiveType.Cube); mk.name = "MoveRouteMark"; mk.transform.SetParent(fl, false);
                mk.transform.localPosition = (a3 + b3) * 0.5f; mk.transform.localRotation = Quaternion.LookRotation(b3 - a3); mk.transform.localScale = new Vector3(0.15f, 0.05f, (b3 - a3).magnitude);
                mk.GetComponent<Renderer>().sharedMaterial = markMat; Object.DestroyImmediate(mk.GetComponent<Collider>());
            }
            // 穴の水
            if (M2_I1Water) { HoleWater(waterT, o, "HoleI1Cut", M2_I1Cut); HoleWater(waterT, o, "HoleI1", M2_I1Hole); }
            if (M2_RingPond) HoleWater(waterT, o, "HoleRing", RH); if (M2_Top) { HoleWater(waterT, o, "HoleTop1", M2_TH1); HoleWater(waterT, o, "HoleTop2", M2_TH2); }
            // つながっていない島どうしの近さ
            var groups = new List<(string n, Vector2[] poly, Vector2 c, float r)>
            {
                ("I1", M2_I1, Vector2.zero, 0f), ("Dome", M, Vector2.zero, 0f), ("Goal", G, Vector2.zero, 0f) };
            if (!M2_V3) groups.Add(("Ring", R, Vector2.zero, 0f));
            else foreach (var vp in v3Polys) groups.Add((vp.n, vp.poly, Vector2.zero, 0f));
            if (M2_Top) groups.Add(("Top", M2_T, Vector2.zero, 0f));
            var linked = new HashSet<string> { "I1-Dome", "Dome-Ring", "Ring-Top", "Dome-Pad0", "Pad0-TiltA", "TiltA-Hub", "Hub-TiltB", "TiltA-TiltB", "TiltB-Pad1" };
            if (M2_Cracks) { for (int k = 0; k < M2_CrackN - 1; k++) groups.Add(("S" + k, CrackSlab(k), Vector2.zero, 0f)); linked.Add(M2_V3 ? "Pad1-S0" : M2_Top ? "Top-S0" : "Ring-S0"); linked.Add("S0-S1"); linked.Add("S1-S2"); linked.Add("S2-S3"); linked.Add("S3-Goal"); }
            if (M2_Movers)
            {
                // 動く島はそれぞれ両はしで
                for (int k = 0; k < M2_Mov.Length; k++) { var mv = M2_Mov[k]; var dd = mv.dir.normalized * mv.amp; groups.Add(("M" + k + "a", null, mv.c - dd, M2_MovR)); groups.Add(("M" + k + "b", null, mv.c + dd, M2_MovR)); linked.Add("M" + k + "a-M" + k + "b"); }
                linked.Add("Top-M0a"); linked.Add("Top-M0b"); linked.Add("M0b-M1a"); linked.Add("M1b-M2a"); linked.Add("M2b-Goal");
            }
            else if (!M2_Cracks)
            {
                groups.Add(("F0", null, M2_Floes[0], MapFloeR)); groups.Add(("F1", null, M2_Floes[1], MapFloeR)); groups.Add(("F2", null, M2_Floes[2], MapFloeR)); groups.Add(("FM", null, M2_MoveA, MapFloeR));
                foreach (var kk in new[] { "Top-F0", "F0-F1", "F1-F2", "F2-FM", "Goal-FM" }) linked.Add(kk);
            }
            string warn = "", linkedGaps = "";
            for (int i = 0; i < groups.Count; i++) for (int j = i + 1; j < groups.Count; j++)
            {
                var A = groups[i]; var B = groups[j];
                if (linked.Contains(A.n + "-" + B.n) || linked.Contains(B.n + "-" + A.n))
                {
                    // 道でつないだ島どうしの水の幅（＝見えるつなぎの道の長さの目安）
                    if (A.poly != null && B.poly != null) { float bl = float.MaxValue; foreach (var q in A.poly) { NearestOnPoly(B.poly, q, out float d); bl = Mathf.Min(bl, d); } foreach (var q in B.poly) { NearestOnPoly(A.poly, q, out float d); bl = Mathf.Min(bl, d); } linkedGaps += " " + A.n + "-" + B.n + "=" + bl.ToString("F1") + "m"; }
                    continue;
                }
                float best = float.MaxValue;
                if (A.poly != null && B.poly != null) { foreach (var q in A.poly) { NearestOnPoly(B.poly, q, out float d); best = Mathf.Min(best, d); } foreach (var q in B.poly) { NearestOnPoly(A.poly, q, out float d); best = Mathf.Min(best, d); } }
                else if (A.poly != null) { NearestOnPoly(A.poly, B.c, out float d); best = d - B.r; }
                else if (B.poly != null) { NearestOnPoly(B.poly, A.c, out float d); best = d - A.r; }
                else best = (A.c - B.c).magnitude - A.r - B.r;
                if (best < MapGapMin) warn += " " + A.n + "-" + B.n + "=" + best.ToString("F1") + "m";
            }
            if (warn != "") Debug.LogWarning("[2–3] つながっていない島が近い:" + warn);
            Debug.Log("[2–3] 道でつないだ島どうしの水の幅:" + linkedGaps);
            var last = linkPts[linkPts.Count - 1]; var gEnd = last[last.Count - 1]; var gDir = (last[last.Count - 1] - last[last.Count - 2]).normalized;
            p = o + new Vector3(gEnd.x, 0, gEnd.y); dir = new Vector3(gDir.x, 0, gDir.y);
        }
        finally { MapHills = keepHills; map2Ridge = false; MapHillOnly = null; unscale(); }
    }
    static bool map2Ridge;
    static List<(string n, Vector2[] poly)> v3Polys = new List<(string n, Vector2[] poly)>();
    static Vector2[][] MapHillOnly;
    // 氷の山（台形の柱）：c（まん中、地面の高さ）を中心に +Z へ 上り坂 → 平ら top → 下り坂。幅 w、高さ h、角度 ang。下の面は作らない（床の中）
    static void IceHill(Transform parent, string name, Vector3 c, float w, float h, float ang, float top)
    {
        float run = h / Mathf.Tan(ang * Mathf.Deg2Rad), L = run * 2f + top, hw = w * 0.5f;
        float z0 = -L * 0.5f, z1 = z0 + run, z2 = z1 + top, z3 = z2 + run;
        var v = new List<Vector3>(); var t = new List<int>();
        System.Func<float, float, float, Vector3> P = (x, y, z) => new Vector3(x, y, z);
        Quad(v, t, P(-hw, 0, z0), P(hw, 0, z0), P(hw, h, z1), P(-hw, h, z1), new Vector3(0, 1, -Mathf.Tan(ang * Mathf.Deg2Rad)));   // 上り
        Quad(v, t, P(-hw, h, z1), P(hw, h, z1), P(hw, h, z2), P(-hw, h, z2), Vector3.up);                                           // てっぺん
        Quad(v, t, P(-hw, h, z2), P(hw, h, z2), P(hw, 0, z3), P(-hw, 0, z3), new Vector3(0, 1, Mathf.Tan(ang * Mathf.Deg2Rad)));    // 下り
        foreach (float sx in new[] { -1f, 1f })
        {
            float x = sx * hw;
            Quad(v, t, P(x, 0, z0), P(x, h, z1), P(x, h, z2), P(x, 0, z3), new Vector3(sx, 0, 0));
        }
        var m = NewMesh(name); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = c; g.layer = pathLayer;
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = iceFlat;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m; iceCols.Add(mc);
    }
    // じょじょに幅が変わる道（シマシマ）：半分の幅 w0 → w1 を smoothstep で。左右の半分をまん中で継ぎ目なしにくっつける。p は先へ進む
    static void Flare(Transform parent, string name, ref Vector3 p, Vector3 dir, float len, float w0, float w1)
    {
        var pr = Vector3.Cross(Vector3.up, dir);
        int pn = Mathf.Max(4, Mathf.RoundToInt(len / StripeLen));
        foreach (float side in new[] { -1f, 1f })
        {
            var mid = new List<Vector3>(); var edge = new List<Vector3>();
            for (int i = 0; i <= pn; i++)
            {
                float t = (float)i / pn, s = t * t * (3f - 2f * t);
                var c = p + dir * (len * t);
                mid.Add(c); edge.Add(c + pr * side * Mathf.Lerp(w0, w1, s));
            }
            Band(parent, name + (side < 0 ? "L" : "R"), mid, edge, false, false, 0f);
        }
        p += dir * len;
    }
    // 割れた所のます目：乱数で 1〜3×1〜2 の穴を開けていく。開けると手前のはしから奥のはしへ歩けなくなる穴はやめる（4方向のとなりで調べる）
    static (int ix, int iz, int w, int h)[] CrackHoleCells(int nx, int nz)
    {
        var rnd = new System.Random(CrackSeed);
        var solid = new bool[nx, nz];
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++) solid[x, z] = true;
        var list = new List<(int, int, int, int)>();
        int want = Mathf.RoundToInt(nx * nz * CrackHoleRatio), open = 0;
        for (int tries = 0; tries < 4000 && open < want; tries++)
        {
            int w = rnd.Next(1, 4), h = rnd.Next(1, 3);
            if (rnd.Next(2) == 0) { var tmp = w; w = h; h = tmp; }
            int ix = rnd.Next(0, nx - w + 1), iz = rnd.Next(1, nz - h);   // 手前と奥の1列は残す
            bool hit = false;
            for (int x = ix - 1; x <= ix + w && !hit; x++) for (int z = iz - 1; z <= iz + h && !hit; z++)
                if (x >= 0 && x < nx && z >= 0 && z < nz && !solid[x, z]) hit = true;   // ほかの穴とくっつけない（1ますあける）
            if (hit) continue;
            for (int x = ix; x < ix + w; x++) for (int z = iz; z < iz + h; z++) solid[x, z] = false;
            if (!Crossable(solid, nx, nz)) { for (int x = ix; x < ix + w; x++) for (int z = iz; z < iz + h; z++) solid[x, z] = true; continue; }
            list.Add((ix, iz, w, h)); open += w * h;
        }
        return list.ToArray();
    }
    static bool Crossable(bool[,] solid, int nx, int nz)
    {
        var seen = new bool[nx, nz]; var q = new Queue<(int, int)>();
        for (int x = 0; x < nx; x++) if (solid[x, 0]) { seen[x, 0] = true; q.Enqueue((x, 0)); }
        while (q.Count > 0)
        {
            var (x, z) = q.Dequeue(); if (z == nz - 1) return true;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = x + dx, b = z + dz;
                if (a < 0 || a >= nx || b < 0 || b >= nz || seen[a, b] || !solid[a, b]) continue;
                seen[a, b] = true; q.Enqueue((a, b));
            }
        }
        return false;
    }

    // 割れた氷の床：a（手前のはしのまん中）から +Z へ len、幅 hw×2。1m のます目で holes の所に穴（下は水）。
    // 1つのメッシュ：のこったます目の上・下の面と、穴・外のふちにだけ側面（となり合うます目の間には面を作らない＝重なりなし）
    static void CrackedFloor(Transform parent, string name, Vector3 a, float len, float hw, (int ix, int iz, int w, int h)[] holes)
    {
        int nx = Mathf.RoundToInt(hw * 2f), nz = Mathf.RoundToInt(len);
        var solid = new bool[nx, nz];
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++) solid[x, z] = true;
        foreach (var h in holes) for (int x = h.ix; x < h.ix + h.w; x++) for (int z = h.iz; z < h.iz + h.h; z++) if (x >= 0 && x < nx && z >= 0 && z < nz) solid[x, z] = false;
        System.Func<int, int, bool> S = (x, z) => x >= 0 && x < nx && z >= 0 && z < nz && solid[x, z];
        float cw = hw * 2f / nx, cl = len / nz, T = CrackT;
        var v = new List<Vector3>(); var t = new List<int>();
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
        {
            if (!solid[x, z]) continue;
            float x0 = -hw + x * cw, x1 = x0 + cw, z0 = z * cl, z1 = z0 + cl;
            Quad(v, t, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), Vector3.up);
            Quad(v, t, new Vector3(x0, -T, z0), new Vector3(x0, -T, z1), new Vector3(x1, -T, z1), new Vector3(x1, -T, z0), Vector3.down);
            if (!S(x - 1, z)) Quad(v, t, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x0, -T, z1), new Vector3(x0, -T, z0), Vector3.left);
            if (!S(x + 1, z)) Quad(v, t, new Vector3(x1, 0, z0), new Vector3(x1, -T, z0), new Vector3(x1, -T, z1), new Vector3(x1, 0, z1), Vector3.right);
            if (!S(x, z - 1)) Quad(v, t, new Vector3(x0, 0, z0), new Vector3(x0, -T, z0), new Vector3(x1, -T, z0), new Vector3(x1, 0, z0), Vector3.back);
            if (!S(x, z + 1)) Quad(v, t, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, -T, z1), new Vector3(x0, -T, z1), Vector3.forward);
        }
        var m = NewMesh(name);
        m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = a;
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = iceFlat;
        g.layer = pathLayer;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m;
        iceCols.Add(mc);
    }

    // 水：見た目の面（上向き）＋ふわふわのアウト（水面）＋人のアウト（水の下）
    static void Water(Transform parent, string name, Vector3 center, float w, float l)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name; q.transform.SetParent(parent, false);
        q.transform.localPosition = center; q.transform.localRotation = Quaternion.Euler(90f, 0, 0); q.transform.localScale = new Vector3(w, l, 1f);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        var wm = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_Water_Still.mat");
        q.GetComponent<Renderer>().sharedMaterial = wm ? wm : crystalMat;
        Hazard(parent, name + "_BallOut", center, new Vector3(w, 0.3f, l), false);
        Hazard(parent, name + "_PlayerOut", center + Vector3.down * 0.9f, new Vector3(w, 0.6f, l), true);
    }

    // 流氷：少しでこぼこした多角形の板（上の面 c.y、厚み 0.8）。type 0＝上に雪（滑らない）、1＝氷（滑る）、2＝線で半分に分けて片方が氷・片方が雪
    static void Floe(Transform parent, string name, Vector3 c, float r, int type, int seed)
    {
        var rnd = new System.Random(seed);
        System.Func<float, float, float> R = (lo, hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        const int N = 8; const float T = 0.8f;
        var poly = new List<Vector2>();
        float a0 = R(0f, 360f);
        for (int i = 0; i < N; i++) { float ang = (a0 + 360f * i / N + R(-10f, 10f)) * Mathf.Deg2Rad; float rr = r * R(0.86f, 1.06f); poly.Add(new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr)); }
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = c;
        if (type == 2)
        {
            float cut = R(0f, 180f) * Mathf.Deg2Rad; var n = new Vector2(Mathf.Cos(cut), Mathf.Sin(cut));
            FloePiece(g, name + "_Ice", Clip(poly, n), T, true);
            FloePiece(g, name + "_Snow", Clip(poly, -n), T, false);
        }
        else FloePiece(g, name + (type == 1 ? "_Ice" : "_Snow"), poly, T, type == 1);
    }
    // 凸の多角形を n・p ≥ 0 の側だけ残して切る
    static List<Vector2> Clip(List<Vector2> poly, Vector2 n)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var A = poly[i]; var B = poly[(i + 1) % poly.Count];
            float da = Vector2.Dot(A, n), db = Vector2.Dot(B, n);
            if (da >= 0f) o.Add(A);
            if ((da >= 0f) != (db >= 0f)) o.Add(A + (B - A) * (da / (da - db)));
        }
        return o;
    }
    static void FloePiece(Transform g, string name, List<Vector2> poly, float T, bool ice)
    {
        var v = new List<Vector3>(); var top = new List<int>(); var side = new List<int>();
        Vector2 cen = Vector2.zero; foreach (var q in poly) cen += q; cen /= poly.Count;
        for (int i = 0; i < poly.Count; i++)
        {
            var A = poly[i]; var B = poly[(i + 1) % poly.Count];
            Vector3 a = new Vector3(A.x, 0, A.y), b = new Vector3(B.x, 0, B.y), cc = new Vector3(cen.x, 0, cen.y);
            int k = v.Count; v.Add(cc); v.Add(a); v.Add(b);   // 上（扇）
            if (Vector3.Cross(a - cc, b - cc).y >= 0) top.AddRange(new[] { k, k + 1, k + 2 }); else top.AddRange(new[] { k, k + 2, k + 1 });
            var dn = Vector3.down * T;
            k = v.Count; v.Add(cc + dn); v.Add(a + dn); v.Add(b + dn);   // 下
            if (Vector3.Cross(a - cc, b - cc).y < 0) side.AddRange(new[] { k, k + 1, k + 2 }); else side.AddRange(new[] { k, k + 2, k + 1 });
            var outw = ((a + b) * 0.5f - cc).normalized;
            Quad(v, side, a, b, b + dn, a + dn, outw);   // 側面
        }
        var m = NewMesh(name);
        m.SetVertices(v); m.subMeshCount = 2; m.SetTriangles(top, 0); m.SetTriangles(side, 1); m.RecalculateNormals(); m.RecalculateBounds();
        var p = new GameObject(name); p.transform.SetParent(g, false); p.layer = pathLayer;
        p.AddComponent<MeshFilter>().sharedMesh = m;
        p.AddComponent<MeshRenderer>().sharedMaterials = new[] { ice ? iceFlat : snowA, iceFlat };
        var mc = p.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.convex = true;
        if (ice) iceCols.Add(mc);
    }

    // ---- 置き物 ----
    // 氷の岩（よける障害物）：角を落とした箱を少し傾けて。当たりあり（ふわふわはアウトにしない）
    static void IceRock(Transform parent, string name, Vector3 at, float size, float yaw)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = at + Vector3.up * (size * 0.38f);
        g.transform.localRotation = Quaternion.Euler(8f, yaw, 12f);
        g.transform.localScale = new Vector3(size * 1.2f, size, size);
        g.GetComponent<Renderer>().sharedMaterial = iceRockMat;
    }
    // 枯れ木：太い幹＋道の上へ張り出す枝2本。枝の先（つららの下がる所）を返す
    static Vector3[] DeadTree(Transform parent, string name, Vector3 basePos, Vector3 toRoad, Vector3 along, int k, float off)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = basePos;
        float h = 7.5f + (k % 3) * 0.6f;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.name = "Trunk"; trunk.transform.SetParent(g, false);
        trunk.transform.localPosition = Vector3.up * (h * 0.5f - 1f); trunk.transform.localScale = new Vector3(0.9f, h * 0.5f + 0.5f, 0.9f);
        trunk.GetComponent<Renderer>().sharedMaterial = treeMat;
        var tips = new List<Vector3>();
        var branches = new[] { (y: h - 2.6f, reach: off - 0.6f, fwd: -0.8f, rise: 0.9f), (y: h - 1.2f, reach: off + 0.6f, fwd: 0.9f, rise: 0.7f) };
        foreach (var b in branches)
        {
            var from = Vector3.up * b.y;
            var to = from + toRoad * b.reach + along * b.fwd + Vector3.up * b.rise;
            var br = GameObject.CreatePrimitive(PrimitiveType.Cube); br.name = "Branch"; br.transform.SetParent(g, false);
            br.transform.localPosition = (from + to) * 0.5f; br.transform.localRotation = Quaternion.LookRotation(to - from);
            br.transform.localScale = new Vector3(0.32f, 0.32f, (to - from).magnitude + 0.2f);
            br.GetComponent<Renderer>().sharedMaterial = treeMat;
            tips.Add(basePos + to);
        }
        return tips.ToArray();
    }
    // つらら（いまは目印：枝の先から下がるとがった柱。当たりなし）
    static void Icicle(Transform parent, string name, Vector3 tip, float len)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = tip;
        g.AddComponent<MeshFilter>().sharedMesh = IcicleMesh();
        g.AddComponent<MeshRenderer>().sharedMaterial = crystalMat;
        g.transform.localScale = new Vector3(1f, len, 1f);
    }
    static Mesh IcicleMesh()
    {
        var m = NewMesh("Icicle");
        const int N = 6; const float r = 0.22f;
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < N; i++)
        {
            float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
            var p0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r); var p1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
            var tip = new Vector3(0, -1f, 0);
            int b = v.Count; v.Add(p0); v.Add(p1); v.Add(tip);
            var nrm = Vector3.Cross(p1 - p0, tip - p0);
            var outw = (p0 + p1) * 0.5f;
            if (Vector3.Dot(nrm, outw) >= 0) t.AddRange(new[] { b, b + 1, b + 2 }); else t.AddRange(new[] { b, b + 2, b + 1 });
            int c = v.Count; v.Add(p0); v.Add(p1); v.Add(Vector3.zero);   // ふた（上）
            if (Vector3.Cross(p1 - p0, Vector3.zero - p0).y >= 0) t.AddRange(new[] { c, c + 1, c + 2 }); else t.AddRange(new[] { c, c + 2, c + 1 });
        }
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    // 氷の池：雪道の上に薄い楕円の氷（道の面から 2cm、下の面は道とぴったりで裏向き）。人だけ滑る、ふわふわは道と同じで触れたらアウト
    static void Pond(Transform parent, string name, Vector3 at, float rx, float rz, Vector3 fwd)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = at + Vector3.up * 0.01f; g.transform.localRotation = Quaternion.LookRotation(fwd);
        g.transform.localScale = new Vector3(rx * 2f, 0.01f, rz * 2f);
        g.GetComponent<Renderer>().sharedMaterial = iceFlat;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.layer = pathLayer;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; mc.convex = true;
        iceCols.Add(mc);
    }
    // 雪だるま（いまは目印。当たりなし）：face の向きに顔
    static void Snowman(Transform parent, string name, Vector3 at, Vector3 face)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = at; g.localRotation = Quaternion.LookRotation(face);
        var parts = new[] { (y: 0.65f, d: 1.4f), (y: 1.65f, d: 1.0f), (y: 2.4f, d: 0.7f) };
        foreach (var pt in parts)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = "Body"; s.transform.SetParent(g, false);
            s.transform.localPosition = Vector3.up * pt.y; s.transform.localScale = Vector3.one * pt.d;
            s.GetComponent<Renderer>().sharedMaterial = snowA; Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube); nose.name = "Nose"; nose.transform.SetParent(g, false);
        nose.transform.localPosition = new Vector3(0, 2.4f, 0.4f); nose.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);
        nose.GetComponent<Renderer>().sharedMaterial = markMat; Object.DestroyImmediate(nose.GetComponent<Collider>());
    }

    // 結晶の柱（よける障害物。当たりあり、ふわふわが当たってもアウトにはしない）
    static void Crystal(Transform parent, string name, Vector3 at, float r, float h)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = at + Vector3.up * (h * 0.5f); g.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
        g.GetComponent<Renderer>().sharedMaterial = crystalMat;
    }
    static void Hazard(Transform root, string name, Vector3 lp, Vector3 size, bool player)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.transform.localPosition = lp; g.layer = player ? 0 : 4;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        if (player) { var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(g); pc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc); }
        else { var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(g); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    // スタート：コース1の Start を丸ごと写す（2–1・2–2 と同じ）。キノコの島の前にもう1本の木の橋。橋のはし（コースの道の始まり）の z を返す
    static float StartSet(Transform root)
    {
        var start = new GameObject("Start").transform; start.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–3] FuwaCourse/Start がない"); return 0f; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_3");
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea");
        var ground = area.Find("Ground"); var bridge = area.Find("Bridge"); var bridgeWood = area.Find("BridgeWood");
        var bc = bridge.GetComponent<BoxCollider>();
        float halfLen = (bc ? bc.size.z : 1f) * bridge.localScale.z * 0.5f;
        float frontZ = 2f * ground.localPosition.z - bridge.localPosition.z;
        start.localPosition = new Vector3(-ground.localPosition.x, -islandTop, -ground.localPosition.z);
        foreach (var b in new[] { bridge, bridgeWood })
        {
            var c = Object.Instantiate(b.gameObject, area, false); c.name = b.name + "_Front";
            c.transform.localPosition = new Vector3(b.localPosition.x, b.localPosition.y, 2f * ground.localPosition.z - b.localPosition.z);
            c.transform.localRotation = Quaternion.Euler(0, 180f, 0) * b.localRotation; c.transform.localScale = b.localScale;
        }
        FuwaCourse2_1Builder.FixCopied(start, map);
        FuwaCourse2_1Builder.Stage2Grass(start);
        return frontZ + halfLen - ground.localPosition.z;
    }

    // ゴール：コース1の Goal を丸ごと。道のはし p の先に木の橋 → 島
    static void GoalSet(Transform root, Vector3 p, Vector3 dir)
    {
        var goal = new GameObject("Goal").transform; goal.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–3] FuwaCourse/Goal がない"); return; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, goal, null, map);
        var area = GameObject.Find("FuwaCourse/Start/StartArea")?.transform; var ground = area ? area.Find("Ground") : null; var gg = goal.Find("GoalGround");
        float far = 6.1f;
        if (ground && gg)
        {
            var br = area.Find("Bridge"); var bc = br ? br.GetComponent<BoxCollider>() : null;
            if (bc) far = Mathf.Abs(ground.localPosition.z - br.localPosition.z) + bc.size.z * br.localScale.z * 0.5f;
            foreach (var nm in new[] { "Bridge", "BridgeWood" })
            {
                var b = area.Find(nm); if (!b) continue;
                var c = Object.Instantiate(b.gameObject, goal, false); c.name = nm;
                c.transform.localPosition = new Vector3(0f, gg.localPosition.y + b.localPosition.y - ground.localPosition.y, -Mathf.Abs(ground.localPosition.z - b.localPosition.z));
                c.transform.localRotation = b.localRotation; c.transform.localScale = b.localScale;
            }
        }
        goal.localPosition = p + dir * (far - 0.3f) + Vector3.up * 0.02f;
        goal.localRotation = Quaternion.LookRotation(dir);
        FuwaCourse2_1Builder.FixCopied(goal, map);
        FuwaCourse2_1Builder.Stage2Grass(goal);
    }

    // チェックポイント：ステージ1の FuwaCourse3/Checkpoint1 を写す（2–2 と同じ）
    static void Checkpoint(Transform root, string name, Vector3 at, Vector3 dir, float hw)
    {
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1"); if (!tmpl) return;
        var g = root.Find("Checkpoints"); if (!g) { g = new GameObject("Checkpoints").transform; g.SetParent(root, false); }
        var cp = Object.Instantiate(tmpl, g, false); cp.name = name;
        cp.transform.localPosition = at; cp.transform.localRotation = Quaternion.LookRotation(dir);
        foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        var trig = cp.transform.Find("Trigger");
        if (trig) trig.localScale = new Vector3(hw * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
        var gm = cp.transform.Find("GlowMushrooms");
        if (gm) foreach (Transform cl in gm) cl.localPosition = new Vector3(Mathf.Sign(cl.localPosition.x) * (hw + 0.55f), cl.localPosition.y, 1.5f);
        var fc = cp.GetComponentInChildren<FuwaCheckpoint>(true);
        if (fc) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc); cps.Add(fc); }
    }
    static void RegisterCheckpoints()
    {
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(); if (!ball) return;
        var so = new SerializedObject(ball); var arr = so.FindProperty("checkpoints");
        for (int k = arr.arraySize - 1; k >= 0; k--) if (arr.GetArrayElementAtIndex(k).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(k);
        foreach (var fc in cps) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fc; }
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ball);
    }
}
