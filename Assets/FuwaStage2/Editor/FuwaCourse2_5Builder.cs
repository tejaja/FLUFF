using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–5「そらのいせき」ブロックアウト
// 2026-11-13 練り直し（てじゃ）：島を細かく 1島1ギミック、島どうしはポータル。大きさは島の中の「光のカーテン」で強制的に変わる（青＝小・橙＝大・白＝もどる）
//  変わる前と後で同じギミックを体験する。① 横風の島（石像から左右交互の強風＋落とし穴の道：ふつう → 大 → 小）
//  ② 巣の島（巣のスキマをぬう：ふつう → 小 → 大）③ 重力の島（小 → 大）④ 最後：横風・巣・重力を短く、入口に青と橙のカーテンが並んで選べる → ゴール
//  島は右（+x）へ並べる。次の島は前の島のポータルの面より手前（-z）に置く（穴の向こうに本物の次の島が見えないように）
//  ※ 下は 2026-11-11 の古い流れの説明（コードは 2026-11-13 に島の作りへ置きかえ）
//  ① スタート小島 → ② はじめてのポータル（屋根つきの回廊のつきあたりのポータル → 上の島の部屋）CP1
//  → ③ ちいさくなる門（青い輪 → 鉄格子。人は門の横のクモの巣をくぐる → 白い輪）
//  → ④ でかくなる（オレンジの輪 → 強い横風の細い橋 → 吸いこむ渦のあいだ → 白い輪）CP2
//  → ⑤ 切りかえ（オレンジ → クモの巣の壁（でかいと破れる）→ 鳥（でかいと持てない）→ 白 → 追い風で島から島へ（でかいと落ちる））CP3
//  → ⑥ ポータル＋切りかえ（青 → 小さい窓のポータル（ふわふわ）・クモの巣の扉のポータル（人）→ 上の部屋 → オレンジ → 横風の上り坂）CP4 → ⑦ ゴール
//  考え方（てじゃ）：でかい＝ギミックに強いけど重い、小さい・ふつう＝風に乗れる。地面に着いたらアウトはいつもどおり
//  道はシマシマの若草色（てじゃ「あえて緑」）、遺跡はテラコッタ
//  道の作り方は 2–4 と同じ：P＝いまの道のはし（コースの座標、上の面の高さ）、th＝進む向き（上から見て x→z の反時計回り、度）
//  ロビーの Slot4 の方向に放射状。+Z がロビーから外へ。y=0 がスタートの島の地面
public static class FuwaCourse2_5Builder
{
    const string RootName = "FuwaCourse2_5";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float HW = 2.4f, StripeLen = 1.2f; public static bool UnderFull = true;   // 2026-11-13 島の道 2→2.4
    public static Color PathColA = new Color(0.910f, 0.851f, 0.710f), PathColB = new Color(0.839f, 0.757f, 0.588f);   // #E8D9B5 / #D6C196（2026-11-13 てじゃ「道、ベージュ系にしてみよ」。前は深緑 #7DB866 / #67A552、その前 #A6DB7E / #8CCB66）
    const string MeshDir = "Assets/FuwaStage2/Meshes/Course2_5";
    const float WT = 0.5f;   // 部屋の壁の厚み

    static Material pathA, pathB, stone, terra, terraD, moss, glow, maskM, iron, underM;
    static int pathLayer = 11;
    static FuwaBall ballT;
    static float ballR;
    static List<FuwaCheckpoint> cps = new List<FuwaCheckpoint>();
    static Transform root, pathT, secT, viewT;
    static Vector3 P; static float th;
    static int segNo, stencilNo;
    static System.Text.StringBuilder log;

    [MenuItem("FuwaCourse/ステージ2/2–5 そらのいせき（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        root = new GameObject(RootName).transform;
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot4").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        root.rotation = Quaternion.LookRotation(spoke);
        root.position = new Vector3(lc.x, floorY - StartBelow, lc.z) + spoke * SpokeR;

        pathA = PathMat("Course2_5_PathA", PathColA); pathB = PathMat("Course2_5_PathB", PathColB);
        PavingOn(pathA); PavingOn(pathB);   // ロビーの道もこの色（FuwaStage2LobbyCourses）
        stone = FuwaStage2WaterTest.ToonMat("Ruins_Stone", new Color(0.86f, 0.74f, 0.62f));
        // 2026-11-15 てじゃ「いまテラコッタカラーなところ、もっとベージュに近い色がいい、道のベージュとはちょっとかえて」→ 少しピンク寄りの砂岩ベージュ（道は黄色寄り）
        //  前：0.82/0.48/0.33・0.62/0.34/0.24
        terra = FuwaStage2WaterTest.ToonMat("Ruins_Terracotta", TerraCol);
        terraD = FuwaStage2WaterTest.ToonMat("Ruins_TerracottaDark", TerraDCol);
        // 2026-11-15 色あわせ（てじゃ「全部いこ」）：C 草を少し鮮やかに（前 0.55/0.72/0.45）、B 岩の下向きの面をだんだん暗く（奥行き）
        moss = FuwaStage2WaterTest.ToonMat("Ruins_Moss", new Color(0.5f, 0.76f, 0.4f));
        GrassPattern(moss);
        underM = FuwaStage2WaterTest.ToonMat("Ruins_Under", new Color(0.72f, 0.58f, 0.48f));
        if (underM.HasProperty("_UnderStrength")) { underM.SetColor("_UnderColor", new Color(0.62f, 0.5f, 0.48f)); underM.SetFloat("_UnderStrength", 0.55f); underM.SetFloat("_UnderGrad", 0.6f); EditorUtility.SetDirty(underM); }
        glow = FuwaStage2WaterTest.ToonMat("Ruins_PortalRim", new Color(0.55f, 0.9f, 1f));
        glow.SetColor("_EmissionColor", new Color(0.35f, 0.8f, 1f) * 1.4f); EditorUtility.SetDirty(glow);
        iron = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        maskM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Ruins_PortalMask.mat");
        if (!maskM) { maskM = new Material(Shader.Find("Fuwa/PortalMask")); AssetDatabase.CreateAsset(maskM, "Assets/FuwaStage2/Materials/Ruins_PortalMask.mat"); }
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Meshes", "Course2_5");
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballT = Resources.FindObjectsOfTypeAll<FuwaBall>().FirstOrDefault(b => b.gameObject.scene.IsValid() && b.transform.parent && b.transform.parent.name == "FuwaSystem");
        ballR = ballT ? ballT.GetComponent<SphereCollider>().radius * ballT.transform.lossyScale.x : 0.15f;
        cps.Clear(); segNo = 0; stencilNo = 10; archGateNo = 0; colFix.Clear(); stripeCache.Clear(); FuwaPortalTest.clones.Clear(); camPortals.Clear(); gateZ.Clear();
        log = new System.Text.StringBuilder();

        float R0 = StartSet(root);
        pathT = new GameObject("Path").transform; pathT.SetParent(root, false);
        secT = new GameObject("Sections").transform; secT.SetParent(root, false);
        viewT = new GameObject("PortalViews").transform; viewT.SetParent(root, false);
        th = 90f; P = Vector3.zero;
        IslandJoin(pathT, "StartJoin", Vector3.zero, Dir(th), R0, HW, pathB, FuwaCourse2_4Builder.JoinInset);
        P = Dir(th) * R0;

        S(StartRun);
        var goalAt = Layout == 4 ? BuildV4() : LayoutV3 ? BuildV3() : BuildIslands();
        SetupPortalCams();

        // アウト（下に落ちた）：コースの下（まわり 12m）
        var lb = LocalCollidersBounds();
        var outSize = new Vector3(lb.size.x + 24f, 2f, lb.size.z + 24f);
        float outY = lb.min.y - 6f;
        Hazard(root, "BallOut", new Vector3(lb.center.x, outY, lb.center.z), outSize, false);
        Hazard(root, "PlayerOut", new Vector3(lb.center.x, outY - 2f, lb.center.z), outSize, true);

        RegisterCheckpoints();
        RegisterVisibility();
        FuwaPortalTest.LinkBlowers();
        FuwaStage2LobbyCourses.Run();
        LinkRecords();
        RegisterBgm(root);
        FuwaStage2CopyShadows.Run();   // 写したスタート・ゴールの飾りを「近くで影を出す」一覧へ（2026-11-11）
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "[2–5] " + log + " ゴール " + goalAt.ToString("F1") + " 範囲 " + lb.min.ToString("F0") + "〜" + lb.max.ToString("F0");
    }

    // BGM（2026-11-13 てじゃ「ancient ruins cover」、よそで作った曲をループ化：A案 イントロ42.92s＋56.48sループ（16小節、68BPM）。元の曲と候補は Claude_Workspace/fluff_bgm/ruins）
    const string BgmClip = "Assets/FuwaStage2/Audio/ruins_ancient_introloop.wav";
    const float BgmGain = 1.35f, BgmLoopStart = 36.62f, BgmLoopEnd = 93.092f;   // A案 42.92–99.40 は「つなぎきつい」→ 2回目と1回目がいちばん似てる 36.6s 付近の拍の頭でつなぐ C案
    public static void RegisterBgm(Transform courseRoot)
    {
        FuwaStage2Bgm.Register(courseRoot, "BGM_course2_5", "BGM_course2_4", BgmClip, BgmGain, BgmLoopStart, BgmLoopEnd);
    }

    // ================= 2026-11-13 夜 練り直し 3（てじゃ）：下の段 ①②（右へのびる）→ ワープ → 上の段 ③④（①②の上空、向きを変える）→ ワープ → ①の島の中の空洞（ゴール）=====
    public static int Layout = 4;
    public static float UpperUp = 22f, UpperHeading = 60f, UpperCrossBack = 22f;   // 上の段のスタートを、交わる所から手前へ
    public static Vector3 CavernSize = new Vector3(16f, 7f, 16f);

    static Vector3 BuildV4()
    {
        islandsT = new GameObject("Islands").transform; islandsT.SetParent(root, false);
        runNo = 0;
        // ---- 下の段 ----
        Island("Bridge1");
        Run(() => { ArchBridge(12f, 0f, true, null, true); });
        // 2026-11-13 てじゃ「正直風はもう良いかな」「序盤はデカくしたり小さくしたりしてぐねぐねの穴だらけの道をアスレチックするだけでもいい」
        //  → ①は ぐねぐねの穴あきの道：ふつう → 大 → 小
        var i1 = Island("I1_StatueHill");
        // 2026-11-14 てじゃ「下層、最初の橋抜けたら巨大カーテン、今の巨大カーテンの位置に小さくするやつをおこう」→ 橋のあと 橙（大）→ 飛び石 → 青（小）→ 飛び石 → 白
        Run(() => { S(1f); ArchGate(); Curtain("C1_Big", 1); S(1.5f); StonesWind("N", 17.6f, 0f); S(1f); ArchGate(); Curtain("C1_Small", -1); S(1.5f); S(3f, 1f); });
        T(-45f);
        Run(() => { StonesWind("B", 24.2f, 1.1f); S(3f, 1.5f); });
        T(-45f);
        // 2026-11-14 てじゃ「最初の小さくなるところやめてもとに戻すやつにする、その後すぐポータルにする、下の小さくなるエリアはなし」
        //  → 大きい飛び石のあと、白カーテン（戻る）→ すぐワープA。小さいカーテン・小さい飛び石・橋2・②の島はなし
        Transform warpA = null;
        Run(() => { S(1f); ArchGate(); Curtain("C1_Normal", 0); S(1.5f); S(5f); warpA = BigPortal(isl, "WarpA", P, 180f); if (WarpWBack > 0f) S(WarpWBack); if (RuinEnds) RuinedEnd("RuinEnd", P, Vector3.forward, HW, HW, false, 101); });   // 2026-11-15 てじゃ「他のポータルもそうかも」→ うしろに道（S(5) はシマ4本＝B で終わる）   // 2026-11-14 てじゃ「下層の普通もどしの後も、ポータルまでの距離ちょい伸ばしたい」白→ワープ 3.5 → 6.5m
        // 2026-11-14 てじゃ「下の地面は道に沿った部分以外なくして、床下のゴール部分は石像の下においてみよ」→ 道ぞいの帯＋石像の台地（中に空洞）
        var cav = PathHugRock(i1);
        // ---- 空中の巣の回廊（①の島の +x・-z の角の上から、-x へ ①の上をわたる）----
        //  2026-11-14 ①が短くなったので、前の位置（①の範囲から決めていた所）に固定
        P = WebWalkStart; th = 200f;
        { // 2026-11-14 てじゃ「その島、石像基準に反時計回りに回り込むようにちょっとだけ動かして」→ 石像を中心に上から見て反時計回りに WebWalkOrbit°（向きもいっしょに）
            float o = WebWalkOrbit * Mathf.Deg2Rad, dx = P.x - MushroomAt.x, dz = P.z - MushroomAt.y;
            P = new Vector3(MushroomAt.x + dx * Mathf.Cos(o) - dz * Mathf.Sin(o), P.y, MushroomAt.y + dx * Mathf.Sin(o) + dz * Mathf.Cos(o)); th += WebWalkOrbit;
            // 2026-11-15 てじゃ「ちょっぴり石像よりに」→ 石像の方へ WebWalkInward（向きはそのまま）
            var rad = new Vector2(P.x - MushroomAt.x, P.z - MushroomAt.y).normalized; P -= new Vector3(rad.x, 0f, rad.y) * WebWalkInward; }
        P -= Dir(th) * WebWalkBack;   // 2026-11-14 巣の門を1つ足したぶん、到着側をうしろへ（曲がり角から先は前と同じ所）
        var iw = Island("I2_WebWalk");
        Transform warpAin = null;
        // 2026-11-14 てじゃ「この道、全体的に石の壁で包まれた道みたいなのにしたい」「屋根もあっていい、たまに崩れてちょっと見えてる程度」「ギミック入ったんけしていいよ、一旦」
        //  → 石のトンネル（CorridorTunnel）。巣・カーテン・門・柱の列は一旦なし（長さは前と同じ）。到着所の屋根もトンネルに入れる（床だけ残す）
        //  前の版：ArrivalRoof → CP → 白 巣N → 右40° → 青 巣S → 橙 巣B
        Transform runA = null, runB = null;
        // 2026-11-14 てじゃ「ポータル後にまず通常状態で1個避ける、その後に巨大化、その後巨大化のまま避けながら進む、ポータル前に通常に戻る」
        //  「床もちょい崩れさせて、上部の蜘蛛の巣を下からくぐるとかもあってもいいかも」「落とし穴もほしい」
        //  → ふつうで巣1枚 → 橙 → 大きいまま 左右交互の巣＋落とし穴の床 → 天井から垂れた巣をくぐる → … → 白 → ワープW。巣・カーテンはトンネルの高さに収める
        float hwSave = HW; HW = TunnelHW;
        pitTable = BigPitRows; pitRow = 0; float tileL0 = TileL; TileL = PitTileL;   // 2026-11-14 てじゃ「穴をもっと奥にデカく」「穴は全部」→ 回廊の落とし穴は全部大きい（4マス中3マス×2列）   // 2026-11-14 てじゃ「このトンネルもっと太くても良いかも、横も上も」→ 回廊の道（＝トンネルの中）のはばを広く
        float wh0 = WebH, ch0 = CurtainH, cth0 = CurtainTrigH; WebH = TunnelH - 0.15f; CurtainH = CorridorPillars ? ch0 : TunnelH - 0.4f; CurtainTrigH = TunnelH + 7f;   // 2026-11-15 てじゃ「カーテンとか柱、低いかも、あわせよう」→ 柱式（屋根なし）なら下の段と同じ高さ   // 当たりも低く（真上の上の段の島まで届かないように）
        // 2026-11-15 てじゃ「道をすこし伸ばしてポータルの位置も後ろに」「イラストみたいにちょっと曲げて」
        //  → 到着ポータル＋まっすぐ WarpAInBack ＋ 左へ WarpAInBend° 曲がって、前の道のはじまり（ここから先は前と同じ所）につなぐ
        if (WarpAInBack > 0f)
        {
            float sg = Mathf.Sign(WarpAInBend == 0f ? 1f : WarpAInBend), thE = th, thS = th - WarpAInBend;
            var inner = P + Dir(thE + 90f) * HW * sg;
            var p1 = WarpAInBend == 0f ? P : inner - Dir(thS + 90f) * HW * sg;
            P = p1 - Dir(thS) * WarpAInBack; th = thS;
            Run(() => { warpAin = BigPortal(isl, "WarpAIn", Vector3.zero, 0f); ArrivalRoof(false, true); if (RuinEnds) RuinedEnd("RuinEnd", new Vector3(0f, P.y, ArrivalBackZ()), Vector3.back, HW, HW, false, 102); S(WarpAInBack); });   // 「延長で縞がおかしい」→ 広い床をやめ、うしろの床は道はば（B）
            if (WarpAInBend != 0f) T(WarpAInBend);
        }
        Run(() => { runA = isl; if (WarpAInBack <= 0f) { warpAin = BigPortal(isl, "WarpAIn", Vector3.zero, 0f); ArrivalRoof(false); } CPi("Checkpoint1", 0.8f, true); S(4f);   // 2026-11-15 てじゃ「チェックポイントもうちょい手前に、ちゃんと丸太を道の外に」→ 2.5m 手前（曲がり角のすぐ先）。前：S(2.5) CP S(1.5)
            // 2026-11-14 てじゃ「最初のクモの巣やっぱいらんかもね」→ ふつうの大きさの巣（左2つ）はなし
            // 2026-11-14 てじゃ「大きくなったあとのクモの巣をもう一個ふやしたい、入ってきた側の道を伸ばして追加」→ 下2つ（上を越える）を足す
            // 2026-11-14 てじゃ「そもそも順番かえよ、今下段2個→上段2個のとこを逆に」→ 右下 → 上2つ → 下2つ
            // 2026-11-14 てじゃ「上段2個、いみないかもな、左側2個にしよう」→ 右下 → 左2つ → 下2つ
            // 2026-11-14 てじゃ「序盤のも、1〜4枚じゃなくてデカイやつの一部が大きく破れてる形式に」→ 大きい巣1枚に大きな裂け目（右下ななめ／左たて／上よこ）。前の版：右下 / 左2 / 下2（WebGates）
            S(1f); ArchGate(); Curtain("C2_Big", 1); S(1.5f + MidCurtainGap);   // 2026-11-15 てじゃ「その段のカーテンのアーチもしたのと揃える」→ 下の段と同じ溝の柱＋くさび石のアーチ
            // 2026-11-14 てじゃ「（裂け目より）小さい巣が複数あるやつ」→ 田の字にもどす（右下 / 左2つ / 下2つ）。裂け目の版：TearGates B1
            WebGates("B1", 5.6f, new[] { WLR, WLL | WUL, WLL | WLR }, true); });
        T(-40f);
        //  2026-11-14 てじゃ「クモの巣もっと小さくしよ、4つで埋まるくらい、それを組み合わせて使う、左下だけ、右下と左上、一箇所しかあいてない、とか」
        //  → 断面を田の字に4分割した小さい巣（WebGates）。前の版：左右交互の大きい巣（WebPits）＋天井の巣（LowWeb）
        // 2026-11-14 てじゃ「カーブ後の蜘蛛の巣の位置に、小さくなるカーテンつけよう」→ 曲がってすぐの門（左下＋右上）を青カーテンに
        // 2026-11-14 てじゃ「青カーテン以降の蜘蛛の巣、もっと小さいスキマを小さいふわふわで通り抜ける感じにしよう」→ 4つの門を「小さい穴1つの巣」に（穴の位置は 左下・右上・まん中・右下）
        //  前の版：右上だけあき / 右2 / 右下＋左上 / 左上だけあき（WebGates B2・B3・B4）
        Run(() => { runB = isl; S(1f); S(WebGateGap / 2f); ArchGate(); Curtain("C2_Small", -1); S(WebGateGap / 2f + MidCurtainGap);
            // 2026-11-14 てじゃ「スキマ通し、穴よりもさっきみたいなクモの巣が複数あるとこのスキマを通す感じに」→ 四角い巣2枚の間の細いスキマ（左寄りたて／上よこ／右寄りたて／下よこ）。前の版：穴1つの巣（HoleGates）
            // 2026-11-14 てじゃ「1枚ものを一部を裂くイメージ」「1〜2ライン裂けてる感じ」「斜めもあり」→ 細い裂け目（たて左／ななめ上／ななめ右／よこ下）。前の版：スキマ（SlitGates）
            // 2026-11-14 てじゃ「小さいゾーンも、小さい巣をもっとつめて、形を変えてスキマを調整」→ 3×3 のつめた巣＋1か所のスキマ（たて中段左 / よこ上右 / たて下段右 / よこ下左）
            // 2026-11-14 てじゃ「（3×3 は）規則的すぎる」→ ばらばらにつめた小さい巣＋スキマ1か所（左たて / 右上ななめ / まん中よこ / 右下ななめ）。3×3 の版：PackedGates
            // 2026-11-14 てじゃ「むしろ最初のクモの巣4枚並べてできたスキマみたいな感じがいい」→ 六角形っぽい巣でうめて、角のスキマを通す（2×2 / 3×2 / 2×3 / 3×2 ずらし）。ランダムの版：OrganicGates
            // 2026-11-14 てじゃ「もっと丁寧に、スキマのある巣を1から作ってみて」→ ステンドグラスの巣（ボロノイ）＋さかい目1本だけのスキマ。六角形を並べた版：HexGridGates
            VoronoiGates("B2", new[] {
                new[] { new Vector2(-2.5f, 1.4f), new Vector2(0.2f, 1.1f), new Vector2(2.6f, 1.6f), new Vector2(-2.2f, 4.0f), new Vector2(0.4f, 3.7f), new Vector2(2.8f, 4.2f) },
                new[] { new Vector2(-2.8f, 2.6f), new Vector2(-0.6f, 0.9f), new Vector2(-0.3f, 3.9f), new Vector2(1.9f, 2.2f), new Vector2(2.9f, 4.5f), new Vector2(2.7f, 0.4f) } },
                new[] { new Vector2Int(1, 4), new Vector2Int(0, 2) }); S(1f);
            VoronoiGates("B4", new[] {
                new[] { new Vector2(-2.6f, 0.8f), new Vector2(-1.0f, 3.0f), new Vector2(-3.0f, 4.5f), new Vector2(1.2f, 1.2f), new Vector2(1.5f, 4.2f), new Vector2(3.2f, 2.6f) } },
                new[] { new Vector2Int(3, 5) });
            // 2026-11-14 てじゃ「小のほうの蜘蛛の巣、奥の一個けそう ながいかも」→ B4 の2門目（左上ななめ）を外した（道も 3.8m 短く）
            //  外した門の種：(-2.0,1.8) (0.8,0.7) (0.5,3.0) (-1.8,4.4) (2.8,2.0) (2.6,4.6)、スキマ (2,3)
            // 2026-11-14 てじゃ「道は縮めなくてよかった、カーテンまでの間があいててOK」→ 外した門のぶん（WebGateGap）道を足して長さは元のまま
            S(WebGateGap + MidCurtainGap);
            S(0.3f); ArchGate(); Curtain("C2_Normal", 0); S(6.6f); });   // 6.5→6.6：シマを偶数（A…B）にして、ポータルのうしろの道（A から）と交互にする   // 2026-11-14 てじゃ「最後のクモの巣なくしてそこ普通もどしでいいかも」「カーテンポータル間を広げたいからクモの巣消してカーテン手前に」→ 白は最後の門のすぐ後、白→ワープW 6.5m（前は左下だけの門 B5 → 白 → 2.5m）
        WebH = wh0; CurtainH = ch0; CurtainTrigH = cth0;
        // 2026-11-14 てじゃ「クモの巣ゾーン、壁である必要なくなったし柱式にもどしたい」（カメラ式ポータルで空が見えても問題なくなった）→ 列柱＋巣の門（柱2本＋はり）
        if (CorridorPillars) CorridorColonnade(runA, runB); else CorridorTunnel(runA, runB, -40f, -1.6f);
        if (!CorridorPillars) { // CP の光るキノコは道のふちの外（壁の中）にあったので、トンネルの中の壁ぎわへ（2026-11-15 柱式ではやらない：てじゃ「ちゃんと丸太を道の外に」）
            var cpg = runA.Find("Checkpoints/Checkpoint1/GlowMushrooms");
            if (cpg) foreach (Transform c in cpg) c.localPosition = new Vector3(Mathf.Sign(c.localPosition.x) * (HW - 0.3f), c.localPosition.y + 0.66f, c.localPosition.z);
        }
        Transform warpW = null;
        Run(() => { warpW = BigPortal(isl, "WarpW", P, 180f); if (WarpWBack > 0f) S(WarpWBack); if (RuinEnds) RuinedEnd("RuinEnd", P, Vector3.forward, HW, HW, false, 103); });   // 2026-11-15 てじゃ「ポータルぎりぎりすぎるから道ちょっぴり伸ばして」→ ポータルのうしろに道
        if (MidRock) MidTierRock(iw, "I2_Rock");   // 2026-11-15 てじゃ「穴が開いてるところ以外に下に島つけよ」→「ポータル付近のとこだけ島にしよう」（出口のポータルができたあと）
        HW = hwSave; pitTable = null; TileL = tileL0;
        // 2026-11-14 落とし穴の下に地面があると落ちても立ててしまうので、回廊の下の地面はなし（トンネルだけ浮いている）
        //IslandRock(iw, 1.5f, false, 3f, 180f + WebWalkOrbit);
        // ---- 上の段（①②の上、向きを変える）----
        // てじゃ「上の二島、下の二島とクロスしたかんじのほうがいい」：上から見て X 字。下の段のスタート→ワープA の向き d に直角（n）で、まん中の上を横切る
        //  2026-11-14 ①が短くなって②の島もなくなったので、前の位置（スタート→前のワープA のまん中の上）に固定
        P = UpperStart; th = UpperStartHeading;
        { // 2026-11-15 てじゃ「ここから石像からみて後ろ側にまっすぐずらしてみて」→ 石像の背中の向きへ UpperBackShift m（向きはそのまま）
            var back = -(Quaternion.Euler(0f, StatueYaw, 0f) * Vector3.forward); back.y = 0f; back.Normalize();
            P += back * UpperBackShift; }
        float upTh = th;
        var i3 = Island("I3_Gravity");
        Transform warpWin = null;
        // 2026-11-14 てじゃ「上段いくか、ギミック全部かえたい、カーテンギミックはナシ、柱が倒れてくギミック」
        //  「前半は柱が倒れてきて邪魔な感じ、近づくとグラグラして道側にバタンと倒れてくる、あたるとふわふわもプレイヤーも大きく吹っ飛ぶ、ほぼアウトなくらい」
        //  「後半は同じように柱が倒れるが、倒れた後の柱に乗って進む」「最後は崩れて登りきれない階段に柱が倒れて登ってポータル」
        //  → ③ 倒れてくる柱（FallRun）→ 橋 → ④ すき間に倒れて橋になる柱（PillarGap）→ 崩れた階段＋坂になる柱（BrokenStairs）→ ワープB
        //  吹っ飛んで落ちるように、上の段の島の下の地面（IslandRock）はなし（道だけ浮いている）
        //  前の版：③ 白 → 青 GravRun S → 橙 GravRun B / ④ えらぶ道 3本（Stones / WebRun / GravRun）
        // 2026-11-14 てじゃ「（倒れる柱は）ふわふわのシステムと相性悪いな、やっぱべつのにしよ」→ 動く石板 →「ふわふわがついてこないからやっぱきびしい」
        //  →「（回る石の腕と飛び出す石ブロックの）2と3の複合、2も当たったら弾かれるくらいでいい」
        //  → ③ 回る石の腕 3本 → 橋 → ④ 飛び出す石ブロック → 45° → 腕とブロックのまざり → ワープB
        //  柱の版：③ FallRun F1 / ④ PillarGap G1・G2・T(-45)・G3 → BrokenStairs。石板の版：③ SideSlabs A / ④ UpSlabs B → T(-45) → FerrySlab C
        // 2026-11-15 てじゃ「上段のワープ後に、なんらかのギミックでふわふわにバリア付与」「そのままちょっと進むと円形の広場、中央には浮いたクリスタル」
        //  「一定間隔で広場全体に放射状に衝撃波」「ふわふわのバリアの周囲+後ろ側はセーフゾーン」「広場に入る時に最初に一発」
        //  → ③ ワープ → CP2 → バリアの門 → 円形の広場（クリスタルの衝撃波）→ 出口でバリアが消える。回る腕の版：Arms_A（SpinArm ×3、20m）
        shieldT = EnsureShield();
        Run(() => { warpWin = BigPortal(isl, "WarpWIn", Vector3.zero, 0f); ArrivalRoof(false, true); if (RuinEnds) RuinedEnd("RuinEnd", new Vector3(0f, P.y, ArrivalBackZ()), Vector3.back, HW, HW, true, 104); S(2f); CPi("Checkpoint2", 0.8f); S(2.5f);   // ArrivalRoof：着いた所のうしろにも道はばの床（2026-11-15 ここに入れたコメントで CP2 と S(2.5) が消えていたのを戻す）
            S(1.5f); ShieldGateArch("ShieldGate"); S(5f);
            CrystalPlaza("CrystalPlaza");
            // 2026-11-15 てじゃ「広場のつなぎ目がちょっとシンプルすぎる」→ 広場と橋の間の 3m のただの道をやめて、橋を広場のふちから直接（橋は 3m 長く＝先は同じ所）
            // 2026-11-15 てじゃ「（S(0.4) の道の）上が飛び出てる」→ 道ははさまない。橋の欄干の外のはし（BridgeHW+0.27）が広場の円のふちに来る所から橋（まん中は床の下にかくれる）
            if (PlazaBridgeDirect) { if (ShieldOffArch) ShieldGateArch("ShieldOffGate", false, BridgeHW + 0.8f); else ShieldOff("ShieldOff"); P += Dir(th) * PlazaBridgeAdvance(); } else { S(1.5f); ShieldOff("ShieldOff"); S(1.5f); } });   // 解除のアーチは広場のふちギリギリ
        Island("Bridge3");
        Run(() => { if (PlazaBridgeDirect) ArchBridge(14f + 3f - PlazaBridgeAdvance(), 2f, false, null, false, true); else ArchBridge(14f, 2f, false, null); });   // 先のはしは前と同じ所
        // 2026-11-15 てじゃ「ここ（クリスタルの広場）通り過ぎて橋の先はもうゴール行きポータルでいいかも」→ 橋のすぐ先にワープB
        //  前の版：I4_Bumpers（CP3 → 飛び出す石ブロック Pistons_B → T(-45) → 腕とブロックのまざり Mix_C → ワープB）
        Island("I4_End");
        Transform warpB = null;
        // 2026-11-16 てじゃ「ポータルがある道の橋、ただのぶつぎりをやめたい」→ 箱の道（S(3)）をやめて平らな橋のつづき（アーチ1つ）、ワープBのうしろで崩れて終わる
        if (RuinEnds) Run(() => { ArchBridge(3f + WarpWBack, 0f, false, null, false, true, true); warpB = BigPortal(isl, "WarpB", P - Vector3.forward * WarpWBack, 180f); RuinedEnd("RuinEnd", P, Vector3.forward, BridgeHW, BridgeHW + 0.21f, true, 105); });
        else Run(() => { S(3f); warpB = BigPortal(isl, "WarpB", P, 180f); if (WarpWBack > 0f) S(WarpWBack); });   // うしろに道（S(3) はシマ2本＝B で終わる）
        // ---- ①の島の中の空洞（ゴール）----
        var gc = Island("Cavern");
        P = cav.entry; th = cav.heading;
        Transform warpBin = null;
        Run(() => { warpBin = BigPortal(isl, "WarpBIn", Vector3.zero, 0f); ArrivalRoof(false, true);
            if (RuinEnds)   // 2026-11-16 てじゃ「ゴール前のやつはクリスタルだけでいいよ」→ 道はそのまま、うしろの床にクリスタルが落ちている
            {
                var cg = new GameObject("FallenCrystals").transform; cg.SetParent(isl, false);
                float zb = ArrivalBackZ();
                RuinCrystals(cg, "GoalFallen", new List<(Vector3 p, Vector3 e, float s, int x)> {
                    (new Vector3(-HW * 0.55f, 0.04f, zb + 0.55f), new Vector3(0f, 30f, 80f), 0.24f, 0),
                    (new Vector3(HW * 0.6f, 0.02f, zb + 0.9f), new Vector3(8f, -70f, 74f), 0.18f, 0),
                    (new Vector3(HW * 0.2f, -0.06f, zb + 0.25f), new Vector3(-40f, 15f, 10f), 0.32f, 2) });
            }
            S(2.5f); });
        var goalP = P;
        GoalSet(gc, P, Dir(th));
        if (warpA && warpAin) Link(warpA, warpAin, iw, null, true, true);
        if (warpW && warpWin) Link(warpW, warpWin, i3, null, true, true);
        if (warpB && warpBin) Link(warpB, warpBin, gc, null, true, true);
        // 2026-11-13 てじゃ「ここにきのこ石像をおこう」（①の島の上、石の道と上の段と巣の回廊にかこまれた広場）「サイズ調整いるかも」
        if (!MushStatue(new Vector3(MushroomAt.x, i1Top, MushroomAt.y)))
            GiantMushroom(new Vector3(MushroomAt.x, i1Top, MushroomAt.y), MushroomH, MushroomCapR, false);
        BrokenBridge(i1);
        FixColumnGround();
        ApplyFarFade();
        DecoIslets();
        if (ColumnLods) AddColumnLods();
        if (CoursePerf) Debug.Log(FuwaCourse2_4Perf.Apply(root));
        if (CoursePerf && TierShadows) SetupTierShadows();   // 2026-11-15 てじゃ「一旦このコースの軽量化しよ」→ 2–4 と同じ（静的バッチ・小物の影は近くだけ・大物は遠いと影オフ）
        Debug.Log(FuwaWindDecalHeight.BakeAll(root));   // 2026-11-15 風の地面の模様：地面の高さマップ（アバターに写らない）
        log.Append("下の段 2・上の段 2・空洞のゴール ");
        return goalP;
    }

    // 島の岩（中に空洞）：上の地面の板、まわりの岩の壁、空洞の床。-x がわは洞窟の口（外から見える・光が入る）
    //  返り値：出口のポータルを置く所（空洞の +x の壁の内がわ、床の高さ）と、そこから進む向き（-x）
    // 空中の巣の回廊の置き場所：①の島の道の範囲の +x・-z の角から WebWalkOut、高さ WebWalkUp（てじゃの図の青◯）
    public static Vector3 WebWalkOut = new Vector3(2f, 0f, -2f); public static float WebWalkUp = 12f;   // （前の決め方、今は WebWalkStart に固定）
    public static Vector3 WebWalkStart = new Vector3(55.152f, 12f, 14.939f); public static float WebWalkOrbit = 50f, WebWalkBack = 0f, WebWalkInward = 2.5f;   // 2026-11-15 中段を長くしたので「また道を反時計回りに移動」25→35→「もっと」50   // 巣の門を1つ足して +3.8、最初の巣をなくして −3.8   // 2026-11-14 15° → トンネルが伸びたので「また反時計回りにまわして」25°
    public static float MidCurtainGap = 1.5f, MidWebExtra = 1.4f;   // MidWebExtra：てじゃ「クモの巣同士もね」→ 門と門の間を 1.4m 足す（穴の区間はそのまま、前後に道）   // 2026-11-15 てじゃ「いまさらだけど中段の蜘蛛の巣とカーテンの間広げたいな」→ カーテンと巣の間を 1.5m ずつ足す
    public static float UpperBackShift = 10f, WarpAInBack = 3.6f, WarpWBack = 1.3f, WarpAInBend = 25f;   // 3.6m＝シマ3本（A B A）→ 曲がり角（B）→ 先の道（A…）で交互がくずれない
    public static Vector3 UpperStart = new Vector3(39.5948f, 32.8087f, 24.0744f); public static float UpperStartHeading = 134.3111f;   // 2026-11-15 てじゃ「この広場は石像の真上くらいにしよう」→ 広場のまん中が MushroomAt の真上に来るよう (-9.44, 0, +6.95) ずらした（前は 49.0348, 32.8087, 17.1244）
    // ワープの到着地点の屋根（てじゃ「屋根付きのポータル到着地点」）：ポータルを囲む4本柱＋屋根。P は今の位置（ポータルの面）
    public static float ArrivalBackLen = 0f;   // 0＝前のまま（1.9m）。てじゃ「そうじゃなくて 道をすこし伸ばしてポータルの位置も後ろに」→ WarpAInBack で道ごと下げる
    static float ArrivalBackZ() => Mathf.Min(P.z - 1.6f - 0.3f, P.z - ArrivalBackLen);   // ArrivalRoof のうしろの床のはし
    static void ArrivalRoof(bool full = true, bool narrow = false)
    {
        float hx = HW + 0.35f, z0 = P.z - 1.6f, z1 = P.z + 1.4f, h = FuwaPortalTest.HoleH + 1.1f;
        var g = new GameObject("ArrivalRoof").transform; g.SetParent(isl, false);
        // 床：道（P から先）と重ならないように、ポータルのうしろの部分と、道の左右の外だけ（同じ高さの面が重なるとちらつく）
        float fw = hx + 0.3f, zb = Mathf.Min(z0 - 0.3f, P.z - ArrivalBackLen), zf = z1 + 0.3f;   // 2026-11-15 てじゃ「中段のポータル、ちょっと後ろに道を伸ばしておいてほしい」→ うしろの床 1.9m → ArrivalBackLen
        if (narrow)
        {
            Box(g, "FloorBack", new Vector3(0f, P.y - 0.3f, (zb + P.z) / 2f), new Vector3(2f * HW, 0.6f, P.z - zb), pathB, pathLayer);
            // 道（Strip）と同じ下の台（つなぎ目に切れ目が出ないように同じ大きさ）
            var ub = Box(g, "FloorBackUnder", new Vector3(0f, P.y - 0.6f - 0.6f, (zb + P.z) / 2f), new Vector3(2f * HW - 0.3f, 1.2f, P.z - zb), underM, 0); Object.DestroyImmediate(ub.GetComponent<Collider>());
            return;
        }
        Box(g, "FloorBack", new Vector3(0f, P.y - 0.3f, (zb + P.z) / 2f), new Vector3(2f * fw, 0.6f, P.z - zb), pathB, pathLayer);
        foreach (float sx in new[] { -1f, 1f })
            Box(g, "FloorSide", new Vector3(sx * (HW + fw) / 2f, P.y - 0.3f, (P.z + zf) / 2f), new Vector3(fw - HW, 0.6f, zf - P.z), pathB, pathLayer);
        if (!full) return;   // 2026-11-14 回廊をトンネルにしたので、柱と屋根はなし（床だけ）
        foreach (float sx in new[] { -1f, 1f }) foreach (float z in new[] { z0, z1 })
            Box(g, "Pillar", new Vector3(sx * hx, P.y + h / 2f, z), new Vector3(0.6f, h, 0.6f), stone, 0);
        Box(g, "Beam", new Vector3(0f, P.y + h + 0.2f, (z0 + z1) / 2f), new Vector3(2f * hx + 1.2f, 0.4f, z1 - z0 + 1.2f), terra, 0);
        // 切妻の屋根（2枚の板）
        foreach (float sx in new[] { -1f, 1f })
        {
            var rf = Box(g, "Roof", new Vector3(sx * (hx + 0.6f) * 0.5f, P.y + h + 0.95f, (z0 + z1) / 2f), new Vector3((hx + 0.6f) * 1.18f, 0.3f, z1 - z0 + 1.8f), terraD, 0);
            rf.transform.localRotation = Quaternion.Euler(0f, 0f, -sx * 32f);
        }
    }

    // ---- 道ぞいの岩＋石像の台地（2026-11-14）----
    //  2D の符号つき距離（中が −）を層ごとに作って、マーチングトライアングルで板にする。下の層ほど細い（段々の先細り）
    //  道：その島の Run ごとの当たりの範囲（Run の向きの四角）から PathRockM。石像：MushroomAt を中心に StatueRockR
    //  空洞：石像の真下、口は -z（スタートの方から見える）。出口のポータルは空洞の +z の壁の内がわ、-z へ進む
    public static bool StoneRims = false;
    public static float PathRockM = 1.6f, StatueRockR = 17f, RockCell = 0.8f, RockNoise = 0.9f;
    public static float CaveMouthW = 9f; public static bool GoalOpen = true, CaveRound = true, CaveSkirt = true, SkirtSpikes = false, GrassLip = true, CaveMouthLintel = false; public static float CaveCeilBump = 0.7f, CaveDomeUp = 0.9f; public static int CaveWallRocks = 70;   // CaveMouthLintel＝口の上の岩の天井（てじゃ「そっちじゃなくて下」→ もとにもどす）
    static (Vector3 entry, float heading) PathHugRock(Transform island)
    {
        var rk = new GameObject("Rock").transform; rk.SetParent(island, false);
        // 2026-11-14 てじゃ「（飛び石の下に地面があると）道の穴があんまいみない」→「普通の道のとこだけ地面がある感じ」
        //  → 地面はふつうの道（Run の中の Road*、島の Turn*＝曲がり角）の下だけ。飛び石の下は底なし
        //  形：それぞれの道の部品の当たりの角（コースの座標の xz）の凸包
        var polys = new List<Vector2[]>();
        float pathMinY = float.MaxValue;
        void AddHull(Transform part)
        {
            var pts = new List<Vector2>();
            foreach (var cl in part.GetComponentsInChildren<Collider>(true))
            {
                if (cl.isTrigger) continue;
                var t = cl.transform; IEnumerable<Vector3> cs;
                if (cl is BoxCollider bc) cs = Enumerable.Range(0, 8).Select(k => bc.center + Vector3.Scale(bc.size * 0.5f, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                else if (cl is MeshCollider mc && mc.sharedMesh) cs = mc.sharedMesh.vertices;
                else continue;
                foreach (var c in cs) { var p = root.InverseTransformPoint(t.TransformPoint(c)); pts.Add(new Vector2(p.x, p.z)); }
            }
            if (pts.Count >= 3) polys.Add(Hull2D(pts));
        }
        foreach (Transform ch in island)
        {
            if (ch.name.StartsWith("Run"))
            {
                var lb = LocalBounds(ch, root); if (lb.size.sqrMagnitude > 0.01f) pathMinY = Mathf.Min(pathMinY, lb.min.y);
                foreach (Transform part in ch) if (part.name.StartsWith("Road")) AddHull(part);
            }
            else if (ch.name.StartsWith("Turn")) AddHull(ch);
        }
        float top = pathMinY - 0.55f; i1Top = top;
        var sc = MushroomAt;
        System.Func<Vector2, float> dPath = q =>
        {
            float d = float.MaxValue;
            foreach (var poly in polys) d = Mathf.Min(d, PolySdf(poly, q));
            return d;
        };
        // ふちのゆらぎ（ゆるい波を2つ）
        System.Func<Vector2, float> noise = q => RockNoise * (0.6f * Mathf.Sin(q.x * 0.23f + 1.3f) * Mathf.Cos(q.y * 0.19f - 0.7f) + 0.4f * Mathf.Sin(q.x * 0.11f - q.y * 0.13f + 2.1f));
        System.Func<Vector2, float> dStat = q => (q - sc).magnitude;
        // 空洞（石像の真下）：部屋＋口（外までぬける）
        //  2026-11-14 てじゃ「石像の下のゴール、石像からみて後ろ側が開いてる感じにしたい」→ 口は石像の背中の向き（前は -z＝スタート側＝石像の正面側）
        float cy1 = top - 1.5f, cy0 = cy1 - CavernSize.y;
        float hx = CavernSize.x / 2f, hz = CavernSize.z / 2f;
        var back3 = -(Quaternion.Euler(0f, StatueYaw, 0f) * Vector3.forward);
        var mdir = new Vector2(back3.x, back3.z).normalized;   // 口の向き（外へ）
        var mper = new Vector2(-mdir.y, mdir.x);
        System.Func<Vector2, float> dCave = q =>
        {
            var d = q - sc; float u = Vector2.Dot(d, mdir), v = Vector2.Dot(d, mper);
            // 2026-11-15 てじゃ「ゴール周りの空間、四角くくりぬかれてるけどまるくしたい」→ 丸い部屋（ふちを少しゆらす）
            float room = CaveRound ? d.magnitude - (hx + 0.6f * (Mathf.PerlinNoise(q.x * 0.35f + 3f, q.y * 0.35f + 7f) - 0.5f)) : Mathf.Max(Mathf.Abs(v) - hx, Mathf.Abs(u) - hz);
            float mouth = Mathf.Max(Mathf.Abs(v) - CaveMouthW / 2f, Mathf.Max(-u, u - (StatueRockR + 6f)));
            return Mathf.Min(room, mouth);
        };
        System.Func<Vector2, float> dCaveRoom = q =>
        {
            var d = q - sc; float u = Vector2.Dot(d, mdir), v = Vector2.Dot(d, mper);
            return CaveRound ? d.magnitude - (hx + 0.6f * (Mathf.PerlinNoise(q.x * 0.35f + 3f, q.y * 0.35f + 7f) - 0.5f)) : Mathf.Max(Mathf.Abs(v) - hx, Mathf.Abs(u) - hz);
        };
        // 層：(上, 下, 道のはば足し, 台地の半径, 空洞をくりぬく)
        var layers = new List<(float y1, float y0, float pm, float sr, bool cave)>
        {
            (top, top - 1.5f, PathRockM, StatueRockR, false),            // 地面（空洞の天井）
            (top - 1.5f, top - 3.5f, PathRockM - 1.2f, StatueRockR, true),
            (top - 3.5f, top - 6f, PathRockM - 3f, StatueRockR - 0.6f, true),
            (cy1 - 3.5f, cy0, -99f, StatueRockR - 1.2f, true),
            (cy0, cy0 - 1.2f, -99f, StatueRockR - 2f, false),            // 空洞の床
            (cy0 - 1.2f, cy0 - 4.5f, -99f, StatueRockR - 5.5f, false),
            (cy0 - 4.5f, cy0 - 8.5f, -99f, StatueRockR - 9.5f, false),
        };
        // 3 番目の層が 2 番目の層の下（top-6）と空洞の層の上（cy1-3.5 = top-5）で重ならないように：空洞の層の上は top-6 に
        layers[3] = (top - 6f, cy0, -99f, StatueRockR - 1.2f, true);
        // 2026-11-14 てじゃ「ゴール周りの土っぽい部分（を）抜けてる感じにしたい」→ 石像の台地は上の地面の板だけ、空洞の土の壁・床・下の岩はなし。
        //  → 同日「周りを囲う感じは残したい」→ 空洞のまわりの土の壁（口つき）は残して、空洞の床と下の岩だけなし（下が抜けてる）。
        //  ゴールは壁に囲まれた中に浮いた床（写したゴールの GoalGround＋道）（GoalOpen）
        if (GoalOpen) layers.RemoveRange(4, layers.Count - 4);
        System.Func<int, Vector2, float> F = (k, q) =>
        {
            var L = layers[k];
            float n = noise(q);
            float f = Mathf.Min(dPath(q) - L.pm + n, dStat(q) - L.sr + n * 1.6f);
            // 2026-11-15 てじゃ「ゴール後の空間もへんだな」→ 口は下の層だけ（いちばん上の空洞の層 k==1 は部屋だけくりぬく＝口の上に岩の天井が残る、細い帯が浮いて見えない）
            if (L.cave && L.y0 < cy1 + 0.01f && L.y1 > cy0 - 0.01f) f = Mathf.Max(f, -((k == 1 && CaveMouthLintel) ? dCaveRoom(q) : dCave(q)));
            return f;
        };
        // 範囲
        var bb = LocalBounds(island, root);
        float x0 = Mathf.Min(bb.min.x, sc.x - StatueRockR) - 4f, x1 = Mathf.Max(bb.max.x, sc.x + StatueRockR) + 4f;
        float z0 = Mathf.Min(bb.min.z, sc.y - StatueRockR - 8f) - 4f, z1 = Mathf.Max(bb.max.z, sc.y + StatueRockR) + 4f;
        int nx = Mathf.CeilToInt((x1 - x0) / RockCell), nz = Mathf.CeilToInt((z1 - z0) / RockCell);
        var grids = new float[layers.Count][,];
        for (int k = 0; k < layers.Count; k++)
        {
            var g = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++)
            {
                float f = F(k, new Vector2(x0 + i * RockCell, z0 + j * RockCell));
                if (Mathf.Abs(f) < 1e-4f) f = 1e-4f;
                g[i, j] = f;
            }
            grids[k] = g;
        }
        var v = new List<Vector3>(); var tTop = new List<int>(); var tSide = new List<int>();
        Vector3 P3(int i, int j, float y) => new Vector3(x0 + i * RockCell, y, z0 + j * RockCell);
        bool Full(float[,] g, int i, int j) => g != null && g[i, j] < 0f && g[i + 1, j] < 0f && g[i, j + 1] < 0f && g[i + 1, j + 1] < 0f;
        var segs = new List<Vector3[]>();
        for (int k = 0; k < layers.Count; k++)
        {
            rimSegs = (k == 0 && GrassLip) ? segs : null;
            var L = layers[k]; var g = grids[k];
            var above = k > 0 ? grids[k - 1] : null; var below = k + 1 < layers.Count ? grids[k + 1] : null;
            var capTop = k == 0 ? tTop : tSide;
            for (int j = 0; j < nz; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= nx; i++)
                {
                    bool full = i < nx && Full(g, i, j);
                    if (full && runStart < 0) runStart = i;
                    if (!full && runStart >= 0)
                    {
                        // まるごと中のマスは横につなげて1枚（上の層・下の層にかくれる所はぬく）
                        RockRun(v, capTop, tSide, P3, runStart, i, j, L.y1, L.y0, above, below, Full);
                        runStart = -1;
                    }
                    if (i < nx && !full)
                    {
                        // ふちのマス：三角2つに分けて切る
                        RockTri(v, capTop, tSide, P3(i, j, 0), P3(i + 1, j, 0), P3(i + 1, j + 1, 0), g[i, j], g[i + 1, j], g[i + 1, j + 1], L.y1, L.y0);
                        RockTri(v, capTop, tSide, P3(i, j, 0), P3(i + 1, j + 1, 0), P3(i, j + 1, 0), g[i, j], g[i + 1, j + 1], g[i, j + 1], L.y1, L.y0);
                    }
                }
            }
        }
        rimSegs = null;
        if (GrassLip && segs.Count > 0) GrassLipMesh(segs, rk, "I1_GrassLip");
        if (CaveCeilBump > 0f)
        {   // 2026-11-15 てじゃ「天井を少しでこぼこに」：空洞の天井（地面の層の下の面 top-1.5）を下へぼこぼこ垂らす。壁ぎわは 0（壁の上とつながる）
            float yc = top - 1.5f;
            for (int i = 0; i < v.Count; i++)
            {
                var p3 = v[i]; if (Mathf.Abs(p3.y - yc) > 0.01f) continue;
                var q2 = new Vector2(p3.x, p3.z); float dc = dCave(q2); if (dc >= 0f) continue;
                float fade = Mathf.Clamp01(-dc / 2.2f);
                // 2026-11-15 てじゃ「中心ほど凹んでるドーム状に」：まん中ほど上へ（地面の上から 0.3m は残す）＋小さいでこぼこ
                float rr = Mathf.Clamp01((q2 - sc).magnitude / hx);
                float dome = CaveDomeUp * (1f - rr * rr);
                float bump = Mathf.PerlinNoise(p3.x * 0.45f + 11f, p3.z * 0.45f + 5f) * 0.7f + Mathf.PerlinNoise(p3.x * 1.1f + 2f, p3.z * 1.1f + 9f) * 0.3f;
                p3.y += (dome - CaveCeilBump * (bump - 0.35f)) * fade; v[i] = p3;
            }
        }
        if (BottomBulge > 0f) BulgeBottoms(v, layers.Select(L => L.y0).ToArray(), F, 1, 2, BottomBulge, BottomBulgeR);   // 道の下（層1・2の下の面）。いちばん下の空洞の輪の層はふち（CaveSkirt）があるのでなし
        if (RockRugged) RockDisplace(v, top, x0, z0);
        // 2026-11-15 てじゃ「壁もまったいらなのもきになる」：空洞の壁に岩のでっぱりを埋めこむ（壁から半分出る）＋天井の近くから下がる岩
        if (CaveWallRocks > 0)
        {
            var cv = new List<Vector3>(); var ct = new List<int>(); var crnd = new System.Random(808);
            float mouthAng = Mathf.Atan2(mdir.y, mdir.x);
            for (int k = 0; k < CaveWallRocks; k++)
            {
                float ang = (float)crnd.NextDouble() * Mathf.PI * 2f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, mouthAng * Mathf.Rad2Deg)) < 30f) continue;   // 口のあたりはなし
                var dir2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float sz = 1.4f + 1.6f * (float)crnd.NextDouble();
                float y = Mathf.Lerp(cy0 + 0.3f, cy1 - 0.4f, (float)crnd.NextDouble());
                var c2 = sc + dir2 * (hx + 0.15f);
                // 平たいでっぱり（横に広く、壁から出るのは浅く）を重ねて、壁の面そのものがゴツゴツに見えるように
                RockChunk(cv, ct, new Vector3(c2.x, y, c2.y), new Vector3(sz * 1.7f, sz * (1.0f + 0.6f * (float)crnd.NextDouble()), sz * 0.55f),
                    Quaternion.LookRotation(new Vector3(dir2.x, 0f, dir2.y)) * Quaternion.Euler((float)crnd.NextDouble() * 30f - 15f, 0f, (float)crnd.NextDouble() * 40f), crnd);
            }
            // 天井の近く（壁から少し内）から下がる岩
            for (int k = 0; k < 9; k++)
            {
                float ang = (float)crnd.NextDouble() * Mathf.PI * 2f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, mouthAng * Mathf.Rad2Deg)) < 35f) continue;
                var c2 = sc + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (hx - 1.2f - 1.5f * (float)crnd.NextDouble());
                float len = 1.2f + 1.4f * (float)crnd.NextDouble();
                RockChunk(cv, ct, new Vector3(c2.x, cy1 - len * 0.35f, c2.y), new Vector3(0.9f, len, 0.9f), Quaternion.Euler(0f, (float)crnd.NextDouble() * 360f, 0f), crnd);
            }
            // 2026-11-15 てじゃ「裏がぱきっと真っ平らなのもきになる」：空洞のまわりの輪の底に、下へ垂れるゴツゴツのふち（まん中のゴールの下は抜けたまま）
            if (GoalOpen && CaveSkirt)
            {
                int N = 72; float rOut = StatueRockR - 1.2f - 1.0f, rIn = hx - 0.35f;   // 2026-11-15 てじゃ「だいぶずれてる」→ 外のふちは島の底の内がわから出す（外に出すと島の壁とのあいだにすき間）
                var rings = new List<Vector3[]>();
                float[] rs = { rOut, rOut - 1.6f, (rOut + rIn) * 0.5f + 0.8f, (rOut + rIn) * 0.5f - 1.2f, rIn + 0.6f, rIn };
                float[] ys = { cy0 + 0.35f, cy0 - 1.6f, cy0 - 3.4f, cy0 - 3.0f, cy0 - 1.2f, cy0 + 0.3f };
                for (int ri = 0; ri < rs.Length; ri++)
                {
                    var ring = new Vector3[N];
                    for (int i = 0; i < N; i++)
                    {
                        float ang = Mathf.PI * 2f * i / N; var d2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        bool edge = ri == 0 || ri == rs.Length - 1;
                        float nr = edge ? 0f : (Mathf.PerlinNoise(d2.x * 2f + ri * 3.1f, d2.y * 2f + 1.7f) - 0.5f) * 2.2f;
                        float ny = edge ? 0f : (Mathf.PerlinNoise(d2.x * 2.6f + 7.3f + ri, d2.y * 2.6f + 4.1f) - 0.5f) * 2.4f;
                        var q2 = sc + d2 * (rs[ri] + nr);
                        ring[i] = new Vector3(q2.x, ys[ri] + ny, q2.y);
                    }
                    rings.Add(ring);
                }
                // 2026-11-15 てじゃ「ゴールからみて抜けてるはずなのに、下のほうに壁がつながってる」→ 口の下（口の向き・はば）はふちを切る。切り口はふさぐ
                bool InMouth(int i) { float ang = Mathf.PI * 2f * (i + 0.5f) / N; var d2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)); return Vector2.Dot(d2, mdir) > 0f && Mathf.Abs(Vector2.Dot(d2, mper)) * rIn < CaveMouthW / 2f + 1.2f; }
                for (int i = 0; i < N; i++)
                {
                    int ip = (i + N - 1) % N;
                    if (InMouth(i) == InMouth(ip)) continue;
                    // i の線（i-1 と i の間）が切り口：6つの輪の i 番目の点で多角形のふた
                    var poly = rings.Select(rg => rg[i]).ToList();
                    var cen = poly.Aggregate(Vector3.zero, (x, y) => x + y) / poly.Count;
                    float angI = Mathf.PI * 2f * i / N; var tang = new Vector3(-Mathf.Sin(angI), 0f, Mathf.Cos(angI));
                    var want = InMouth(i) ? tang : -tang;   // 切った側（口の方）を向く
                    for (int k = 0; k < poly.Count; k++) Tri(cv, ct, cen, poly[k], poly[(k + 1) % poly.Count], want);
                }
                for (int ri = 0; ri + 1 < rings.Count; ri++)
                    for (int i = 0; i < N; i++)
                    {
                        if (InMouth(i)) continue;
                        int j = (i + 1) % N;
                        var A = rings[ri][i]; var B = rings[ri][j]; var C = rings[ri + 1][j]; var D = rings[ri + 1][i];
                        var mid = (A + B + C + D) * 0.25f; var radial = new Vector3(mid.x - sc.x, 0f, mid.z - sc.y).normalized;
                        var want = (ri < 2 ? radial : (ri == 2 ? Vector3.zero : -radial)) + Vector3.down;   // 外の帯は外下向き、内の帯は内下向き
                        Tri(cv, ct, A, B, C, want); Tri(cv, ct, A, C, D, want);
                    }
                // ところどころ下がる岩（2026-11-15 てじゃ「下の細かい破片みたいなのいらんかも」→ なし）
                if (SkirtSpikes) for (int k = 0; k < 14; k++)
                {
                    float ang = (float)crnd.NextDouble() * Mathf.PI * 2f; var d2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    var q2 = sc + d2 * Mathf.Lerp(rIn + 1.5f, rOut - 2f, (float)crnd.NextDouble());
                    float len = 2f + 2.5f * (float)crnd.NextDouble();
                    RockChunk(cv, ct, new Vector3(q2.x, cy0 - 2.6f - len * 0.4f, q2.y), new Vector3(1.6f, len, 1.6f), Quaternion.Euler(0f, (float)crnd.NextDouble() * 360f, 0f), crnd);
                }
            }
            if (RockRugged) RockDisplace(cv, top, x0, z0);   // 島の岩と同じずらし（島はゴツゴツで横にずれているので、そろえないとずれて見える）
            var cm = SaveMesh("I1_CaveRocks", cv, ct);
            var cgo = new GameObject("CaveRocks"); cgo.transform.SetParent(rk, false);
            cgo.AddComponent<MeshFilter>().sharedMesh = cm; cgo.AddComponent<MeshRenderer>().sharedMaterial = underM;
        }   // 2026-11-15 見た目クオリティ：段々の平らな壁 → ゴツゴツ（上のふちは動かさない）
        var mesh = SaveRockMesh("I1_Rock", v, tTop, tSide);
        var go = new GameObject("RockMesh"); go.transform.SetParent(rk, false); go.layer = pathLayer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { moss, underM };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;   // 地面も空洞もふわふわが着いたらアウト（前の Top・CaveFloor と同じ道のレイヤー）
        log.Append("道ぞいの岩 " + (tTop.Count + tSide.Count) / 3 + "三角 ");
        // 出口のポータルは口と反対の壁から少し離す（だ円の輪が壁にめりこまないように）、口の方へ進む
        var ent = sc - mdir * (hz - 1.5f);
        return (new Vector3(ent.x, cy0, ent.y), Mathf.Atan2(mdir.y, mdir.x) * Mathf.Rad2Deg);
    }

    // 草のへり（上の層の輪郭 segs から：外へ少しはみ出して下へ垂れる）。点ごとに外向きを平均してつなぎ目にすき間なし
    static void GrassLipMesh(List<Vector3[]> segs, Transform rk, string meshName)
    {   // 2026-11-15 てじゃ「島の上の草っぽいとこのフチがぱきっとしてる」→ ふちに草のへり（外へ少しはみ出して下へ垂れる）。点ごとに外向きを平均してつなぎ目にすき間なし
            var avg = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 p3) => new Vector3Int(Mathf.RoundToInt(p3.x * 200f), Mathf.RoundToInt(p3.y * 200f), Mathf.RoundToInt(p3.z * 200f));
            foreach (var sg in segs) foreach (int e2 in new[] { 0, 1 }) { var kk = Key(sg[e2]); avg[kk] = (avg.TryGetValue(kk, out var a0) ? a0 : Vector3.zero) + sg[2]; }
            var lv = new List<Vector3>(); var lt = new List<int>();
            Vector3 Out(Vector3 p3, float o, float dn) { var n3 = avg[Key(p3)]; n3.y = 0f; n3 = n3.sqrMagnitude > 1e-6f ? n3.normalized : Vector3.zero;
                float w = Mathf.PerlinNoise(p3.x * 0.7f + 3f, p3.z * 0.7f + 8f); return p3 + n3 * o * (0.7f + 0.6f * w) + Vector3.down * dn * (0.7f + 0.6f * w); }
            foreach (var sg in segs)
            {
                Vector3 a1 = sg[0], b1 = sg[1];
                Vector3 a2 = Out(a1, 0.22f, 0.12f), b2 = Out(b1, 0.22f, 0.12f);   // 少し外・少し下（ふくらみ）
                Vector3 a3 = Out(a1, 0.26f, 0.42f), b3 = Out(b1, 0.26f, 0.42f);   // 垂れたはし
                var on = sg[2];
                RockQuad(lv, lt, a1, b1, b2, a2, on + Vector3.up * 1.2f); RockQuad(lv, lt, a2, b2, b3, a3, on);
                // 裏（下から見ても抜けない）
                RockQuad(lv, lt, a1, b1, b2, a2, -(on + Vector3.up * 1.2f)); RockQuad(lv, lt, a2, b2, b3, a3, -on);
            }
            var lm = SaveMesh(meshName, lv, lt);
            var lgo = new GameObject("GrassLip"); lgo.transform.SetParent(rk, false);
            lgo.AddComponent<MeshFilter>().sharedMesh = lm; lgo.AddComponent<MeshRenderer>().sharedMaterial = moss;
            }

    // ===== 2026-11-15 中段（空中の巣の回廊）の下の島 =====
    //  てじゃ「穴が開いてるところ以外に下に島つけよ」→ ふつうの道（Road*・Turn*・到着の床）の下だけ、下の段と同じ作りの岩（段々に細る・ゴツゴツ・草のへり）
    //  落とし穴の列（WindRun_Pits*）の下はくりぬく（MidRockPitClear のゆとり）＝落ちたらそのまま落ちる
    public static bool MidRock = true; public static float MidRockPortalR = 6f; public static int MidRockLayers = 3; public static float MidRockTipIn = 3.0f, MidRockSag = 0f, MidRockDepth = 5.4f; public static float MidRockPitClear = 0.7f, MidRockM = 1.3f, MidRockTopNoise = 0.15f;   // 2026-11-15 てじゃ「やっぱここは島やめるか」「ポータル付近のとこだけ島にしよう」→ ポータルから MidRockPortalR 以内の道の部品だけ。2026-11-15 てじゃ「島のフチは柱の外側ギリギリくらいに、外側から通れるとまずい」→ 上の面のふちは道のはし＋1.3（柱の台の外がわ）±0.15   // MidRockLayers：てじゃ「段はおおくなくていいなぁ」7→3
    static void MidTierRock(Transform island, string meshName)
    {
        var rk = new GameObject("Rock").transform; rk.SetParent(island, false);
        var polys = new List<Vector2[]>(); var pits = new List<Vector2[]>();
        float pathMinY = float.MaxValue;
        Vector2[] Hull(Transform part, bool countY)
        {
            var pts = new List<Vector2>();
            foreach (var cl in part.GetComponentsInChildren<Collider>(true))
            {
                if (cl.isTrigger || cl.gameObject.layer != pathLayer) continue;
                var t = cl.transform; IEnumerable<Vector3> cs;
                if (cl is BoxCollider bc) cs = Enumerable.Range(0, 8).Select(k => bc.center + Vector3.Scale(bc.size * 0.5f, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                else if (cl is MeshCollider mc && mc.sharedMesh) cs = mc.sharedMesh.vertices;
                else continue;
                foreach (var c in cs) { var p = root.InverseTransformPoint(t.TransformPoint(c)); pts.Add(new Vector2(p.x, p.z)); if (countY) pathMinY = Mathf.Min(pathMinY, p.y); }
            }
            return pts.Count >= 3 ? Hull2D(pts) : null;
        }
        // ポータル（到着・出口）の近くの部品だけ（てじゃ「ポータル付近のとこだけ島にしよう」）
        var portals = island.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Warp") && x.parent && x.parent.name.StartsWith("Run")).Select(x => root.InverseTransformPoint(x.position)).ToList();
        bool Near(Transform part)
        {
            if (MidRockPortalR <= 0f) return true;
            var b = LocalBounds(part, root);
            foreach (var pp in portals) { var c = b.ClosestPoint(new Vector3(pp.x, b.center.y, pp.z)); if (new Vector2(c.x - pp.x, c.z - pp.z).magnitude <= MidRockPortalR) return true; }
            return false;
        }
        foreach (Transform ch in island)
        {
            if (ch.name.StartsWith("Run"))
                foreach (Transform part in ch)
                {
                    if ((part.name.StartsWith("Road") || part.name == "ArrivalRoof") && Near(part)) { var h = Hull(part, true); if (h != null) polys.Add(h); }
                    else if (part.name.StartsWith("WindRun_Pits")) { var h = Hull(part, false); if (h != null) pits.Add(h); }
                }
            else if (ch.name.StartsWith("Turn") && Near(ch)) { var h = Hull(ch, true); if (h != null) polys.Add(h); }
        }
        if (polys.Count == 0) return;
        float top = pathMinY + 0.05f;   // 道の下の面（道の厚み 0.6 の下）にぴったり
        System.Func<Vector2, float> noise = q => RockNoise * (0.6f * Mathf.Sin(q.x * 0.23f + 1.3f) * Mathf.Cos(q.y * 0.19f - 0.7f) + 0.4f * Mathf.Sin(q.x * 0.11f - q.y * 0.13f + 2.1f));
        System.Func<Vector2, float> dPath = q => { float d = float.MaxValue; foreach (var poly in polys) d = Mathf.Min(d, PolySdf(poly, q)); return d; };
        System.Func<Vector2, float> dPit = q => { float d = float.MaxValue; foreach (var poly in pits) d = Mathf.Min(d, PolySdf(poly, q)); return d; };
        // 2026-11-15 てじゃ「この道の島の下部分もパッキリしすぎ」→ 下へ細ってとがる＋段のふちを上下にゆらす（「段はおおくなくていい」→ 3段）
        var layers = new List<(float y1, float y0, float pm)>();
        {
            int NL = MidRockLayers; float y = top;
            for (int k = 0; k < NL; k++)
            {
                float u = (float)k / (NL - 1);
                float h = k == 0 ? 1.3f : (MidRockDepth - 1.3f) / Mathf.Max(1, NL - 1);   // 段の数をかえても深さは同じ
                float pm = MidRockM - (MidRockM + MidRockTipIn) * Mathf.Pow(u, 1.25f);
                layers.Add((y, y - h, pm)); y -= h;
            }
        }
        System.Func<int, Vector2, float> F = (k, q) =>
        {
            float f = dPath(q) - layers[k].pm + noise(q) * (k == 0 ? MidRockTopNoise / RockNoise : 1f);
            if (pits.Count > 0) f = Mathf.Max(f, -(dPit(q) - MidRockPitClear - 0.25f * k));   // 穴の列の下はなし（下ほど少し広くあける）
            return f;
        };
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        foreach (var poly in polys) foreach (var q in poly) { x0 = Mathf.Min(x0, q.x); x1 = Mathf.Max(x1, q.x); z0 = Mathf.Min(z0, q.y); z1 = Mathf.Max(z1, q.y); }
        x0 -= MidRockM + 3f; x1 += MidRockM + 3f; z0 -= MidRockM + 3f; z1 += MidRockM + 3f;
        int nx = Mathf.CeilToInt((x1 - x0) / RockCell), nz = Mathf.CeilToInt((z1 - z0) / RockCell);
        var grids = new float[layers.Count][,];
        for (int k = 0; k < layers.Count; k++)
        {
            var g = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++) { float f = F(k, new Vector2(x0 + i * RockCell, z0 + j * RockCell)); if (Mathf.Abs(f) < 1e-4f) f = 1e-4f; g[i, j] = f; }
            grids[k] = g;
        }
        var v = new List<Vector3>(); var tTop = new List<int>(); var tSide = new List<int>();
        Vector3 P3(int i, int j, float y) => new Vector3(x0 + i * RockCell, y, z0 + j * RockCell);
        bool Full(float[,] g, int i, int j) => g != null && g[i, j] < 0f && g[i + 1, j] < 0f && g[i, j + 1] < 0f && g[i + 1, j + 1] < 0f;
        var segs = new List<Vector3[]>();
        for (int k = 0; k < layers.Count; k++)
        {
            rimSegs = (k == 0 && GrassLip) ? segs : null;
            var L = layers[k]; var g = grids[k];
            var above = k > 0 ? grids[k - 1] : null; var below = k + 1 < layers.Count ? grids[k + 1] : null;
            var capTop = k == 0 ? tTop : tSide;
            for (int j = 0; j < nz; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= nx; i++)
                {
                    bool full = i < nx && Full(g, i, j);
                    if (full && runStart < 0) runStart = i;
                    if (!full && runStart >= 0) { RockRun(v, capTop, tSide, P3, runStart, i, j, L.y1, L.y0, above, below, Full); runStart = -1; }
                    if (i < nx && !full)
                    {
                        RockTri(v, capTop, tSide, P3(i, j, 0), P3(i + 1, j, 0), P3(i + 1, j + 1, 0), g[i, j], g[i + 1, j], g[i + 1, j + 1], L.y1, L.y0);
                        RockTri(v, capTop, tSide, P3(i, j, 0), P3(i + 1, j + 1, 0), P3(i, j + 1, 0), g[i, j], g[i + 1, j + 1], g[i, j + 1], L.y1, L.y0);
                    }
                }
            }
        }
        rimSegs = null;
        if (GrassLip && segs.Count > 0) GrassLipMesh(segs, rk, meshName + "_GrassLip");
        if (BottomBulge > 0f) BulgeBottoms(v, layers.Select(L => L.y0).ToArray(), F, 0, layers.Count - 1, BottomBulge, BottomBulgeR);
        if (RockRugged) RockDisplace(v, top, x0, z0);
        // 段のふち（上の面より下）を上下にゆらす：位置だけで決まるずらしなので、となりの面と同じ点は同じだけ動く（すき間なし）
        if (MidRockSag > 0f)
            for (int i = 0; i < v.Count; i++)
            {
                var p3 = v[i]; if (p3.y > top - 0.05f) continue;
                float depth = Mathf.Clamp01((top - p3.y) / 2f);
                float sagN = Mathf.PerlinNoise(p3.x * 0.33f + 5.1f, p3.z * 0.33f + 2.7f) * 0.7f + Mathf.PerlinNoise(p3.x * 0.9f + 1.3f, p3.z * 0.9f + 8.2f) * 0.3f;
                p3.y -= MidRockSag * sagN * depth; v[i] = p3;
            }
        var mesh = SaveRockMesh(meshName, v, tTop, tSide);
        var go = new GameObject("RockMesh"); go.transform.SetParent(rk, false); go.layer = pathLayer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { moss, underM };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        // 島の座標がコースの座標とちがっても合うように（頂点はコースの座標）
        rk.position = root.position; rk.rotation = root.rotation;
        log.Append("中段の岩 " + (tTop.Count + tSide.Count) / 3 + "三角 ");
    }

    // 凸包（反時計回り）と、凸多角形までの符号つき距離（中が −）
    static Vector2[] Hull2D(List<Vector2> pts)
    {
        var p = pts.Distinct().OrderBy(a => a.x).ThenBy(a => a.y).ToList();
        if (p.Count < 3) return p.ToArray();
        float Cr(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var h = new List<Vector2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = h.Count;
            foreach (var pt in pass == 0 ? p : Enumerable.Reverse(p))
            {
                while (h.Count >= start + 2 && Cr(h[h.Count - 2], h[h.Count - 1], pt) <= 0f) h.RemoveAt(h.Count - 1);
                h.Add(pt);
            }
            h.RemoveAt(h.Count - 1);
        }
        return h.ToArray();
    }
    static float PolySdf(Vector2[] poly, Vector2 q)
    {
        float dmin = float.MaxValue; bool inside = true;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Length]; var e = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, e) / Mathf.Max(e.sqrMagnitude, 1e-6f));
            dmin = Mathf.Min(dmin, (q - (a + e * t)).magnitude);
            if (e.x * (q.y - a.y) - e.y * (q.x - a.x) < 0f) inside = false;   // 反時計回りなら中は左
        }
        return inside ? -dmin : dmin;
    }

    // まるごと中のマスの横つながり（i0〜i1-1）：上の面・下の面。上の層／下の層にまるごとかくれるマスはぬく
    static void RockRun(List<Vector3> v, List<int> tTop, List<int> tBot, System.Func<int, int, float, Vector3> P3, int i0, int i1, int j, float y1, float y0,
        float[,] above, float[,] below, System.Func<float[,], int, int, bool> Full)
    {
        // 2026-11-15 マスごとの四角に（横につなげると、岩をゴツゴツにずらした時につなぎ目にすき間ができた＝T字の継ぎ目）
        for (int i = i0; i < i1; i++)
        {
            if (!Full(above, i, j)) RockQuad(v, tTop, P3(i, j, y1), P3(i + 1, j, y1), P3(i + 1, j + 1, y1), P3(i, j + 1, y1), Vector3.up);
            if (!Full(below, i, j)) RockQuad(v, tBot, P3(i, j, y0), P3(i + 1, j, y0), P3(i + 1, j + 1, y0), P3(i, j + 1, y0), Vector3.down);
        }
    }
    static void RockQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
    {
        RockAddTri(v, t, a, b, c, want); RockAddTri(v, t, a, c, d, want);
    }
    static void RockAddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 want)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var s = b; b = c; c = s; }
        int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }
    // ふちの三角：中（f<0）の部分を切り出して上の面・下の面、切り口の線から横の壁
    static List<Vector3[]> rimSegs;   // 上の層の輪郭（草のへり用）。null なら記録しない
    static void RockTri(List<Vector3> v, List<int> tTop, List<int> tSide, Vector3 p0, Vector3 p1, Vector3 p2, float f0, float f1, float f2, float y1, float y0)
    {
        var ps = new[] { p0, p1, p2 }; var fs = new[] { f0, f1, f2 };
        var poly = new List<Vector3>(); var cut = new List<Vector3>();
        for (int e = 0; e < 3; e++)
        {
            int n = (e + 1) % 3;
            if (fs[e] < 0f) poly.Add(ps[e]);
            if ((fs[e] < 0f) != (fs[n] < 0f))
            {
                var x = Vector3.Lerp(ps[e], ps[n], fs[e] / (fs[e] - fs[n]));
                poly.Add(x); cut.Add(x);
            }
        }
        if (poly.Count < 3) return;
        for (int k = 1; k + 1 < poly.Count; k++)
        {
            RockAddTri(v, tTop, poly[0] + Vector3.up * y1, poly[k] + Vector3.up * y1, poly[k + 1] + Vector3.up * y1, Vector3.up);
            RockAddTri(v, tSide, poly[0] + Vector3.up * y0, poly[k] + Vector3.up * y0, poly[k + 1] + Vector3.up * y0, Vector3.down);
        }
        if (cut.Count == 2)
        {
            // 外向き：外（f>0）の角の平均の方
            var outC = Vector3.zero; int no = 0; for (int e = 0; e < 3; e++) if (fs[e] >= 0f) { outC += ps[e]; no++; }
            outC /= no; var mid = (cut[0] + cut[1]) / 2f; var on = outC - mid; on.y = 0f;
            var seg = cut[1] - cut[0]; var nrm = new Vector3(seg.z, 0f, -seg.x); if (Vector3.Dot(nrm, on) < 0f) nrm = -nrm;
            RockQuad(v, tSide, cut[0] + Vector3.up * y1, cut[1] + Vector3.up * y1, cut[1] + Vector3.up * y0, cut[0] + Vector3.up * y0, nrm);
            if (rimSegs != null) rimSegs.Add(new[] { cut[0] + Vector3.up * y1, cut[1] + Vector3.up * y1, nrm.normalized });
        }
    }
    // 2026-11-15 てじゃ「下段も島もだけど、真下が真っ平らなのがきになる」
    //  下向きの面（層 k の下の面のうち、下の層 k+1 がついていない所）を下へふくらませる。ふち（F(k)=0）と下の層のふち（F(k+1)=0）では 0 → 継ぎ目は動かない
    //  下へ動かすだけなので、下の層の壁は面をつきぬける向き（すき間にならない）。RockDisplace の前に呼ぶ（格子の座標で F を使う）
    public static float BottomBulge = 1.3f, BottomBulgeR = 2.2f;
    static void BulgeBottoms(List<Vector3> v, float[] y0s, System.Func<int, Vector2, float> F, int kFrom, int kTo, float K, float R)
    {
        int nL = y0s.Length;
        for (int i = 0; i < v.Count; i++)
        {
            var p = v[i];
            for (int k = kFrom; k <= kTo && k < nL; k++)
            {
                if (Mathf.Abs(p.y - y0s[k]) > 1e-3f) continue;
                var q = new Vector2(p.x, p.z);
                float fk = F(k, q); if (fk >= 0f) break;
                float fn = k + 1 < nL ? F(k + 1, q) : 99f; if (fn <= 0f) break;
                float s1 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-fk / R)), s2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fn / R));
                float nz = Mathf.PerlinNoise(p.x * 0.31f + 13.7f, p.z * 0.31f + 4.4f) * 0.75f + Mathf.PerlinNoise(p.x * 0.8f + 2.9f, p.z * 0.8f + 19.1f) * 0.25f;
                p.y -= K * s1 * s2 * (0.45f + 1.0f * nz); v[i] = p;
                break;
            }
        }
    }
    // 岩の横と下をゴツゴツに：場所だけで決まるずらし（同じ場所の点は同じだけ動く＝すき間ができない）。上の面（topY）のふちは動かさず、下へいくほど大きく
    public static bool RockRugged = true;
    public static float RockRugAmp = 0.9f, RockRugFine = 0.25f, RockRugY = 0f;   // 2026-11-15 てじゃ「島の下の土、下から見るとスキマとかガタガタ」→ 上下と細かいゆらしをやめる（層の境目の面がずれてすき間になっていた）
    static void RockDisplace(List<Vector3> v, float topY, float ox, float oz)
    {
        for (int i = 0; i < v.Count; i++)
        {
            var p = v[i];
            float depth = topY - p.y; if (depth < 0.05f) continue;
            float a = Mathf.Clamp01(depth / 1.4f);
            float x = p.x - ox, z = p.z - oz;
            float n1 = Mathf.PerlinNoise(x * 0.23f + 11.3f, z * 0.23f + 3.7f + p.y * 0.15f) - 0.5f;
            float n2 = Mathf.PerlinNoise(x * 0.23f + 41.1f + p.y * 0.15f, z * 0.23f + 17.9f) - 0.5f;
            float f1 = Mathf.PerlinNoise(x * 0.9f + 5.1f, z * 0.9f + p.y * 0.7f + 2.2f) - 0.5f;
            float f2 = Mathf.PerlinNoise(x * 0.9f + p.y * 0.7f + 9.4f, z * 0.9f + 7.7f) - 0.5f;
            float ny = Mathf.PerlinNoise(x * 0.5f + 23.3f, z * 0.5f + 31.1f) - 0.5f;
            p.x += (n1 * RockRugAmp * 2f + f1 * RockRugFine * 2f) * a;
            p.z += (n2 * RockRugAmp * 2f + f2 * RockRugFine * 2f) * a;
            p.y += ny * RockRugY * 2f * a;
            v[i] = p;
        }
    }
    static Mesh SaveRockMesh(string name, List<Vector3> v, List<int> t0, List<int> t1)
    {
        string path = MeshDir + "/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!m) { m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.Clear(false);
        m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v); m.subMeshCount = 2; m.SetTriangles(t0, 0); m.SetTriangles(t1, 1);
        m.RecalculateNormals(); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    static (Vector3 entry, float heading) IslandRockHollow(Transform island, float margin)
    {
        var b = LocalBounds(island, root);
        var rk = new GameObject("Rock").transform; rk.SetParent(island, false);
        float top = b.min.y - 0.55f; i1Top = top;
        Vector3 size = new Vector3(Mathf.Max(b.size.x + 2f * margin, CavernSize.x + 4f), 1f, Mathf.Max(b.size.z + 2f * margin, CavernSize.z + 4f));
        Box(rk, "Top", new Vector3(b.center.x, top - 0.5f, b.center.z), size, moss, pathLayer);
        // 空洞
        float wt = 2f, cy1 = top - 1.5f, cy0 = cy1 - CavernSize.y;
        var c = new Vector3(b.center.x, (cy0 + cy1) / 2f, b.center.z);
        float hx = CavernSize.x / 2f, hz = CavernSize.z / 2f;
        Box(rk, "CaveCeiling", new Vector3(c.x, (cy1 + top - 1f) / 2f, c.z), new Vector3(size.x * 0.95f, top - 1f - cy1 + 0.01f, size.z * 0.95f), underM, 0);
        Box(rk, "CaveFloor", new Vector3(c.x, cy0 - 0.5f, c.z), new Vector3(CavernSize.x + 2f * wt, 1f, CavernSize.z + 2f * wt), underM, pathLayer);
        Box(rk, "CaveWallE", new Vector3(c.x + hx + wt / 2f, c.y, c.z), new Vector3(wt, CavernSize.y, CavernSize.z + 2f * wt), underM, 0);
        Box(rk, "CaveWallN", new Vector3(c.x, c.y, c.z + hz + wt / 2f), new Vector3(CavernSize.x, CavernSize.y, wt), underM, 0);
        Box(rk, "CaveWallS", new Vector3(c.x, c.y, c.z - hz - wt / 2f), new Vector3(CavernSize.x, CavernSize.y, wt), underM, 0);
        // 口（-x）の上下だけ岩：口の上のはり
        Box(rk, "CaveMouthTop", new Vector3(c.x - hx - wt / 2f, cy1 - 1f, c.z), new Vector3(wt, 2f, CavernSize.z + 2f * wt), underM, 0);
        // 空洞の下の先細りの岩
        var u = Box(rk, "Under", new Vector3(c.x, cy0 - 1f - 3f, c.z), new Vector3(size.x * 0.6f, 6f, size.z * 0.6f), underM, 0); Object.DestroyImmediate(u.GetComponent<Collider>());
        return (new Vector3(c.x + hx, cy0, c.z), 180f);
    }

    // シンボルの巨大なキノコの石像（てじゃ「どっかにデカいシンボルてきな石像がほしい、キノコ型とか」）：下の段の右に、自分の浮島の上に立つ。上の段の高さまで届く
    // 2026-11-14 てじゃの絵「きのこの帽子の女の子の石像」（目を閉じて小さいきのこを抱える、苔、アーチの台座）を Blender で作った（Tools/blender/mush_statue.blend、MushStatue）
    //  「イボはなくてもいい」「◯模様自体なしでいい」「縦に潰れてる」→ 水玉なし・体を縦にのばした版。正面（モデルの +z）はスタートの方へ向ける
    public static float StatueScale = 9f, StatueYaw = 225.42f;   // 2026-11-14 てじゃがシーンで決めた大きさ（9倍＝高さ約24m）と向き（コースの座標）
    const string StatueFbx = "Assets/FuwaStage2/Models/MushStatue.fbx";
    public static bool StatueMossFaces = false;   // true で前の「面ごとの苔」にもどす
    static Material AltarStoneMat(Material stoneSrc)
    {
        string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(stoneSrc)).Replace('\\', '/');
        string path = dir + "/Ruins_AltarStone.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(stoneSrc); AssetDatabase.CreateAsset(m, path); } else m.CopyPropertiesFromMaterial(stoneSrc);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/IceDetail.png");
        if (!tex || !m.HasProperty("_GroundTex")) return stoneSrc;
        Color sc = stoneSrc.color, mc = new Color(0.5f, 0.7f, 0.4f);
        m.SetTexture("_GroundTex", tex);
        m.SetFloat("_GroundPatch", 1f); m.SetFloat("_GroundPatchScale", 2.6f); m.SetFloat("_GroundMidScale", 0.9f); m.SetFloat("_GroundSteps", 2f);   // 2段＝苔か石かくっきり（3段だと中間の濁った色が広く出た）
        m.SetColor("_GroundTintA", new Color(mc.r / sc.r, mc.g / sc.g, mc.b / sc.b)); m.SetColor("_GroundTintB", Color.white); m.SetFloat("_GroundFine", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }
    static void StatueMoss(Material m, Color stoneCol)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/IceDetail.png");
        if (!tex || !m.HasProperty("_GroundTex")) return;
        var mossCol = new Color(0.55f, 0.72f, 0.45f);
        m.SetTexture("_GroundTex", tex);
        m.SetFloat("_GroundPatch", StatueMossFaces ? 0f : 1f); m.SetFloat("_GroundPatchScale", 13f); m.SetFloat("_GroundMidScale", 4f); m.SetFloat("_GroundSteps", 3f);
        m.SetColor("_GroundTintA", new Color(mossCol.r / stoneCol.r, mossCol.g / stoneCol.g, mossCol.b / stoneCol.b));   // まだらの濃い所＝苔
        m.SetColor("_GroundTintB", Color.white);
        m.SetFloat("_GroundFine", 0f);
        StatueShade(m);
        EditorUtility.SetDirty(m);
    }
    // 2026-11-15 てじゃ「石像のっぺりにみえちゃうな」→ 形はそのまま、材質で立体感：面の角ばり・石の色むら・濃いめの影・下向きの面を暗く
    static void StatueShade(Material m)
    {
        if (!m.HasProperty("_Facet")) return;
        m.SetFloat("_Facet", 0f);   // 角ばりは Blender で面ごとにフラットに書き出した法線で出す（シェーダーの角ばりは三角ごとなので四角い面に斜めの線が出た）
        m.SetFloat("_NoiseStrength", 0.13f); m.SetFloat("_NoiseScale", 0.32f);
        // 2026-11-15 色あわせ A：影が紫っぽく暗い → あたたかく・少し明るく（前：影の濃さ 0.92・下向き 紫グレー 0.58/0.52/0.66 の 0.5）
        m.SetFloat("_ShadowStrength", 0.8f); m.SetFloat("_Wrap", 0.22f);
        if (m.HasProperty("_ShadowTint")) m.SetColor("_ShadowTint", new Color(0.74f, 0.72f, 0.76f));   // グレーに合わせて色みを弱く
        m.SetColor("_UnderColor", new Color(0.7f, 0.68f, 0.69f)); m.SetFloat("_UnderStrength", 0.42f); m.SetFloat("_UnderGrad", 0.5f);
        EditorUtility.SetDirty(m);
    }
    static bool MushStatue(Vector3 baseP)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(StatueFbx); if (!src) return false;
        var smf = src.GetComponentInChildren<MeshFilter>(); var smr = smf ? smf.GetComponent<MeshRenderer>() : null; if (!smr) return false;
        var mesh = smf.sharedMesh;
        var g = new GameObject("MushStatue").transform; g.SetParent(islandsT, false); g.localPosition = baseP;
        g.localRotation = Quaternion.Euler(0f, StatueYaw, 0f);
        g.localScale = Vector3.one * StatueScale;
        g.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        // 材質：Blender の名前 → ステージ2のトゥーン材質
        // 2026-11-15 苔：面ごとの四角い苔（MS_Moss の面）はやめて石に。かわりに石の材質の「上を向いた面」に、ワールドの xz のまだらで苔の色をのせる（FuwaToonLit の地面の模様、IceDetail の G/B）
        Color stoneC = new Color(0.78f, 0.77f, 0.75f), baseC = new Color(0.69f, 0.67f, 0.64f);   // 2026-11-15 てじゃ「石像もっとグレーっぽく」（前 0.83/0.78/0.71・0.76/0.69/0.61）   // 少し落ち着かせた（前は 0.88/0.84/0.78・0.80/0.74/0.66）
        var stoneM = FuwaStage2WaterTest.ToonMat("Ruins_StatueStone", stoneC);
        var baseM = FuwaStage2WaterTest.ToonMat("Ruins_StatueBase", baseC);
        StatueMoss(stoneM, stoneC); StatueMoss(baseM, baseC);
        var mats = smr.sharedMaterials.Select(m =>
        {
            string n = m ? m.name : "";
            if (n.Contains("Moss")) return StatueMossFaces ? moss : stoneM;
            if (n.Contains("Dark")) { var dm = FuwaStage2WaterTest.ToonMat("Ruins_StatueDark", new Color(0.45f, 0.42f, 0.42f)); StatueShade(dm); return dm; }
            if (n.Contains("Base")) return baseM;
            return stoneM;
        }).ToArray();
        g.gameObject.AddComponent<MeshRenderer>().sharedMaterials = mats;
        g.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;   // 人がぶつかる（ふわふわが乗ったら島の地面と同じくアウトにはならない＝レイヤー0）
        return true;
    }
    public static float MushroomH = 20f, MushroomCapR = 7.5f;   // 前は 30／11（自分の浮島の上）。①の島の広場に移して小さく
    public static Vector2 MushroomAt = new Vector2(25.9f, 38.1f);   // 2026-11-14 てじゃがシーンで動かした位置  // コースの座標（x, z）
    static float i1Top;
    static void GiantMushroom(Vector3 baseP, float MushroomH, float MushroomCapR, bool islet)
    {
        var g = new GameObject("GiantMushroom").transform; g.SetParent(islandsT, false); g.localPosition = baseP;
        if (islet)
        {
            Box(g, "IsleTop", new Vector3(0f, -0.5f, 0f), new Vector3(16f, 1f, 16f), moss, 0);
            var u1 = Box(g, "IsleUnder", new Vector3(0f, -5f, 0f), new Vector3(13f, 8f, 13f), underM, 0); Object.DestroyImmediate(u1.GetComponent<Collider>());
            var u2 = Box(g, "IsleUnder2", new Vector3(0f, -11f, 0f), new Vector3(7f, 6f, 7f), underM, 0); Object.DestroyImmediate(u2.GetComponent<Collider>());
        }
        else Box(g, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(MushroomCapR * 0.9f, 0.6f, MushroomCapR * 0.9f), stone, 0);   // 台座
        var stem = FuwaPortalTest.Prim(PrimitiveType.Cylinder, g, "Stem", new Vector3(0f, MushroomH * 0.4f, 0f), new Vector3(MushroomCapR * 0.38f, MushroomH * 0.4f, MushroomCapR * 0.38f), stone);
        var ring = FuwaPortalTest.Prim(PrimitiveType.Cylinder, g, "StemRing", new Vector3(0f, MushroomH * 0.62f, 0f), new Vector3(MushroomCapR * 0.55f, 0.5f, MushroomCapR * 0.55f), terra);
        var cap = FuwaPortalTest.Prim(PrimitiveType.Sphere, g, "Cap", new Vector3(0f, MushroomH * 0.85f, 0f), new Vector3(2f * MushroomCapR, MushroomH * 0.32f, 2f * MushroomCapR), terra);
        var under = FuwaPortalTest.Prim(PrimitiveType.Cylinder, g, "Gills", new Vector3(0f, MushroomH * 0.78f, 0f), new Vector3(2f * MushroomCapR * 0.92f, 0.3f, 2f * MushroomCapR * 0.92f), stone);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            FuwaPortalTest.Prim(PrimitiveType.Sphere, g, "Spot", new Vector3(Mathf.Cos(a) * MushroomCapR * 0.55f, MushroomH * 0.95f, Mathf.Sin(a) * MushroomCapR * 0.55f), new Vector3(MushroomCapR * 0.22f, MushroomCapR * 0.11f, MushroomCapR * 0.22f), stone);
        }
    }

    // ================= 2026-11-13 夕 練り直し 2：大きい島3つ＋アーチ橋＋てっぺんの石像の島（参考画像の雰囲気）=====
    //  てじゃ「今回は島に小分けじゃなくてもいい、多少はほしい」「ワープは一回くらい」
    //  1 石像の丘：丘のまわりを上りながら回る道、石像の頭の横風、アーチの門のカーテン（ふつう→大→小）
    //  2 柱の神殿：列柱の間の巣（ふつう→小→大）→ 魔法陣の広場の重力（小→大）
    //  3 えらびの遺跡：門で青か橙をえらぶ ×3（横風→巣→重力）→ はしのワープ（仮：ステンシルのポータル。あとでカメラ式に）→ ★てっぺんの石像の島（ゴール）
    //  ブロックアウトの書き方：まっすぐの区間は Run() で「その向きの小さい入れ物」の中に、+z 向きのつもりで作る（WindRun・Curtain などは +z 前提）
    public static bool LayoutV3 = true;
    public static float BridgeHW = 1.6f, TopIslandUp = 32f;
    static Transform islandRoot;
    static int runNo;
    static readonly List<Transform> deckOf1 = new List<Transform>(), deckOf2 = new List<Transform>(), deckOf3 = new List<Transform>();

    static Vector3 BuildV3()
    {
        islandsT = new GameObject("Islands").transform; islandsT.SetParent(root, false);
        runNo = 0;
        // ---- スタート → こわれたアーチ橋（穴あき、石像の横風）----
        Island("Bridge1");
        Run(() => { ArchBridge(12f, 0f, true, new[] { 6f }); });
        // ---- 1 石像の丘 ----
        var i1 = Island("I1_StatueHill");
        Run(() => { S(2f); WindRun("N", 6f, new[] { 3f }); S(1f); ArchGate(); Curtain("C1_Big", 1); S(1.5f); S(3f, 1f); });
        T(-90f);
        Run(() => { WindRun("B", 8f, new[] { 2.5f, 6f }); S(3f, 1.5f); });
        T(90f);
        Run(() => { S(1f); ArchGate(); Curtain("C1_Small", -1); S(1.5f); WindRun("S", 8f, new[] { 2.5f, 6f }); S(3f, 1.5f); });
        T(90f);
        Run(() => { S(5f); });
        T(-90f);
        IslandRock(i1, 3.5f, false);   // 丘のかたまりは道にかぶったのでいったんなし
        // ---- 橋 2 ----
        Island("Bridge2");
        Run(() => { ArchBridge(14f, 2f, false, null); });
        // ---- 2 柱の神殿 ----
        var i2 = Island("I2_Temple");
        Run(() => { S(1.5f); CPi("Checkpoint1", 0.8f); S(1.5f); ArchGate(); Curtain("C2_Normal", 0); S(1.2f); Colonnade(23f); WebRun("N", 7f, 1.7f); S(1.2f); ArchGate(); Curtain("C2_Small", -1); S(1.2f); WebRun("S", 7f, 1.7f); S(1.2f); ArchGate(); Curtain("C2_Big", 1); S(1.2f); WebRun("B", 7f, 1.9f); S(2f); });
        T(-90f);
        Run(() => { S(2f); CPi("Checkpoint2", 0.8f); S(1.5f); ArchGate(); Curtain("C2_GSmall", -1); S(1.5f); GravRun("S", 9f); S(1.2f); ArchGate(); Curtain("C2_GBig", 1); S(1.5f); GravRun("B", 9f); S(2f); });
        T(90f);
        Run(() => { S(3f); });
        IslandRock(i2, 3f, false);
        var templeTop = LocalBounds(i2, root);
        // ---- 橋 3 ----
        Island("Bridge3");
        Run(() => { ArchBridge(16f, 3f, false, null); });
        // ---- 3 えらびの遺跡 ----
        var i3 = Island("I3_Choice");
        Run(() => { S(1.5f); CPi("Checkpoint3", 0.8f); S(1f); Choice("C3a"); S(1f); WindRun("M", 6f, new[] { 2f, 4.5f }); S(2f); });
        T(90f);
        Run(() => { S(1f); Choice("C3b"); S(1f); WebRun("M", 6f, 1.8f); S(2f); });
        T(-90f);
        Run(() => { S(1f); Choice("C3c"); S(1f); GravRun("M", 7f); S(2f); });
        Transform warpA = null;
        Run(() => { warpA = BigPortal(isl, "WarpOut", P, 180f); });
        IslandRock(i3, 3f, false);
        // ---- ★ てっぺんの石像の島（神殿の上、ワープの出口）----
        var top = Island("I4_TopStatue");
        var topAt = new Vector3(templeTop.center.x, templeTop.max.y + TopIslandUp, templeTop.center.z);
        P = topAt; th = 90f;
        Transform warpB = null;
        Run(() => { warpB = BigPortal(isl, "WarpIn", Vector3.zero, 0f); S(2f); CPi("Checkpoint4", 0.6f); S(4f); });
        float lastTh = th; var goalP = P;
        GoalSet(top, P, Dir(th));
        // 石像の頭と手（飾り）
        var hd = new GameObject("GiantHead").transform; hd.SetParent(top, false); hd.localPosition = topAt + new Vector3(-7f, 0f, 6f);
        Box(hd, "Head", new Vector3(0f, 3.5f, 0f), new Vector3(5f, 7f, 5f), stone, 0);
        Box(hd, "Nose", new Vector3(2.6f, 3.6f, 0f), new Vector3(0.6f, 1.4f, 1f), stone, 0);
        foreach (float sz in new[] { -1f, 1f }) Box(hd, "Hand", new Vector3(6f, -3f, sz * 6f), new Vector3(3f, 1.2f, 2f), stone, 0);
        IslandRock(top, 4f, false, 6f);
        if (warpA && warpB) Link(warpA, warpB, top, null, true, true);
        FixColumnGround();
        ApplyFarFade();
        DecoIslets();
        if (ColumnLods) AddColumnLods();
        if (CoursePerf) Debug.Log(FuwaCourse2_4Perf.Apply(root));
        if (CoursePerf && TierShadows) SetupTierShadows();   // 2026-11-15 てじゃ「一旦このコースの軽量化しよ」→ 2–4 と同じ（静的バッチ・小物の影は近くだけ・大物は遠いと影オフ）
        Debug.Log(FuwaWindDecalHeight.BakeAll(root));   // 2026-11-15 風の地面の模様：地面の高さマップ（アバターに写らない）
        log.Append("島 3＋てっぺん ");
        return goalP;
    }

    // 島（の入れ物）をはじめる：P・th はそのまま（コースの座標）
    static Transform Island(string name)
    {
        islandRoot = new GameObject(name).transform; islandRoot.SetParent(islandsT, false);
        isl = islandRoot; pathT = islandRoot; secT = islandRoot;
        return islandRoot;
    }
    // まっすぐの区間：P に th 向きの入れ物を置いて、その中で +z 向きに作る。終わったら P を進める（th はそのまま）
    static void Run(System.Action body)
    {
        var seg = new GameObject("Run" + (runNo++)).transform; seg.SetParent(islandRoot, false);
        seg.localPosition = P; seg.localRotation = Quaternion.LookRotation(Dir(th));
        float th0 = th; isl = seg; pathT = seg; secT = seg; P = Vector3.zero; th = 90f;
        body();
        var end = seg.localPosition + seg.localRotation * P;
        isl = islandRoot; pathT = islandRoot; secT = islandRoot; P = end; th = th0;
    }

    // アーチ橋：deck（穴あきなら WindRun のタイル）＋下に橋脚とアーチ。windsAt で石像の横風
    public static bool ShockDark = true, ShockSkyDim = true, ShieldVeil = true, ShieldOffArch = true; public static Color ShieldOffCol = new Color(0.86f, 0.78f, 1f, 1f); public static float ShockEarly = 5f, ShieldSafePad = 0.25f; public static bool ShockCorridor = true; static float shockCorridorLen; public static Color ShockDarkCol = new Color(0.10f, 0.04f, 0.16f, 1f), ShockDarkCore = new Color(0.32f, 0.10f, 0.42f, 1f);
    public static bool PlazaBridgeDirect = true, PlazaLedgeUnderEntry = true;
    // 広場の出口（床のふちの 0.4 手前）から、橋の欄干の外のはしが床の円にのる所まで
    static float PlazaBridgeAdvance() { float xo = BridgeHW + 0.27f; return Mathf.Sqrt(Mathf.Max(0f, PlazaR * PlazaR - xo * xo)) - (PlazaR - 0.4f); }
    public static float ParapetSink = 0.15f;
    static void ArchBridge(float len, float rise, bool pits, float[] windsAt, bool stubGap = false, bool noFirstPier = false, bool ruinEnd = false)
    {
        float hw0 = HW; HW = BridgeHW;
        float y0 = P.y; float z0 = P.z;
        // 崩れた参道の橋（BrokenBridge）がつながる所は +x の欄干をあける（コースの座標 → この Run の座標）
        Vector2 gapPlusX = Vector2.zero;
        if (stubGap) { float lz = isl.InverseTransformPoint(root.TransformPoint(new Vector3(BridgeHW, 0f, BrokenBridgeZ))).z; gapPlusX = new Vector2(lz - 1.7f, lz + 1.7f); }
        if (pits && Mathf.Abs(rise) < 0.01f) WindRun("Bridge", len, windsAt ?? new float[0]);
        else S(len, rise);
        // 2026-11-15 てじゃ「石像前の橋、へんな縦筋」→ 穴あきの道の横の Rail（区切りごとの見た目だけの横木）が橋の横の板より外に出て継ぎ目が見えていた。橋では消す（当たりはない）
        foreach (var rl in isl.GetComponentsInChildren<Transform>(true).Where(x => x.name == "Rail" && x.parent && x.parent.name.StartsWith("WindRun_Bridge")).ToList()) Object.DestroyImmediate(rl.gameObject);
        // 2026-11-15 見た目クオリティ（C）：橋脚（はばいっぱいの石の壁、下へ細くなって崩れて終わる）＋橋脚の間の石のアーチ（上は壁＝スパンドレル、ふちにくさび石の輪）
        //  ＋道のはしの下のふち飾り（コーニス）＋欄干（低い壁＋笠石、ところどころ崩れて切れている）
        int n = Mathf.Max(ruinEnd && len < 6f ? 1 : 2, Mathf.RoundToInt(len / 4f));
        System.Func<float, float> DeckY = z => y0 + rise * (z - z0) / len;            // 道の上の面
        const float Tk = 0.6f;                                                          // 道の厚み
        float pw = 1.0f, xo = HW + 0.05f, xr = HW + 0.11f;
        string bn = "Br" + (runNo) + "_" + Mathf.RoundToInt(len * 10f);
        {
            var v = new List<Vector3>(); var t = new List<int>();     // 石（橋脚・スパンドレル・アーチの下）
            var rv = new List<Vector3>(); var rt = new List<int>();   // テラコッタ（くさび石の輪・コーニス）
            var rnd = new System.Random(Mathf.RoundToInt(len * 100f + rise * 10f));
            void Quad(List<Vector3> vv, List<int> tt, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want) { Tri(vv, tt, a, b, c, want); Tri(vv, tt, a, c, d, want); }
            // 2026-11-15 てじゃ「最初の橋の裏側がおかしい」→ 穴あきの橋は、穴から中が見えないように：アーチ・橋脚を両はしのリブ（xi〜xo）だけにして、まん中は下まで抜ける。リブの上もふさぐ
            bool open = pits && OpenPitBridge;
            float xi = HW - 0.55f;
            // 橋脚
            for (int i = 0; i <= n; i++)
            {
                if (noFirstPier && i == 0) continue;   // 広場のふちから出る橋：はじめの橋脚はなし（広場の下のドームが支え）
                float z = z0 + len * i / n - (ruinEnd && i == n ? RuinPierBack : 0f), yt = DeckY(z) - Tk;   // 崩れたはしの橋脚は少し下げて、床が先へはみ出す
                float dep = 4.2f + 1.6f * (float)rnd.NextDouble();
                float bx = (float)rnd.NextDouble() - 0.5f, bz = (float)rnd.NextDouble() - 0.5f;
                foreach (float cxp in open ? new[] { -(xi + xo) / 2f, (xi + xo) / 2f } : new[] { 0f })
                {
                    float hx0 = open ? (xo - xi) / 2f : xo, hz0 = pw / 2f;
                    // 2026-11-15 てじゃ「橋の下の柱も、ツンツンじゃなくて折れた後みたいなかんじにしよ」→ 先が1点にとがる形 → まっすぐ下りて（少しだけ細る）ギザギザに折れて終わる
                    //  まわりを 1辺 PS こまに分けた輪：上（道の下の面）→ 下（折れ口、こまごとに高さがちがう）。折れ口はまん中から扇でふさぐ
                    const int PS = 3; int PM = PS * 4;
                    var cornersT = new[] { new Vector2(-hx0, -hz0), new Vector2(hx0, -hz0), new Vector2(hx0, hz0), new Vector2(-hx0, hz0) };
                    var ringT = new Vector3[PM]; var ringB = new Vector3[PM]; var ringM = new Vector3[PM];
                    // 2026-11-15 てじゃ「すきまある」→ 細る柱だと、アーチの横の壁のはし（まっすぐ下りる）との間にくさび形のすき間。アーチの足もと（yt-1.7）より下までは細らせない
                    float yMid = yt - 1.9f;
                    float jph = (float)rnd.NextDouble() * 6.28f, jph2 = (float)rnd.NextDouble() * 6.28f;
                    float yb = yt - dep * 0.8f, tap = 0.9f;
                    var sh = new Vector2(bx * (open ? 0.05f : 0.15f), bz * 0.08f);   // 下の方が少しずれる（古い感じ）
                    for (int k = 0; k < PM; k++)
                    {
                        int side = k / PS; float f = (k % PS) / (float)PS;
                        var q = Vector2.Lerp(cornersT[side], cornersT[(side + 1) % 4], f);
                        ringT[k] = new Vector3(cxp + q.x, yt, z + q.y);
                        ringM[k] = new Vector3(cxp + q.x, yMid, z + q.y);
                        // 2026-11-15 てじゃ「折れてるところギザギザすぎる、もっとゆるやかに」→ こまごとの乱数をやめ、ゆるい斜め＋小さいうねり
                        float thk = Mathf.PI * 2f * k / PM;
                        float jag = 0.32f * Mathf.Sin(thk + jph) + 0.07f * Mathf.Sin(2f * thk + jph2);
                        ringB[k] = new Vector3(cxp + q.x * tap + sh.x, yb + jag, z + q.y * tap + sh.y);
                    }
                    var cB = new Vector3(cxp + sh.x, yb - 0.04f + 0.08f * (float)rnd.NextDouble(), z + sh.y);
                    for (int k = 0; k < PM; k++)
                    {
                        int j = (k + 1) % PM;
                        var mid2 = (ringT[k] + ringT[j]) * 0.5f; var outw = new Vector3(mid2.x - cxp, 0f, mid2.z - z);
                        Quad(v, t, ringT[k], ringT[j], ringM[j], ringM[k], outw);
                        Quad(v, t, ringM[k], ringM[j], ringB[j], ringB[k], outw);
                        Tri(v, t, ringB[k], ringB[j], cB, Vector3.down);
                    }
                    var top = new[] { new Vector3(cxp - hx0, yt, z - hz0), new Vector3(cxp + hx0, yt, z - hz0), new Vector3(cxp + hx0, yt, z + hz0), new Vector3(cxp - hx0, yt, z + hz0) };
                    if (open) Quad(v, t, top[0], top[1], top[2], top[3], Vector3.up);   // 上（穴から見える）
                }
            }
            // 橋脚の間のアーチ
            for (int i = 0; i < n; i++)
            {
                float za = z0 + len * i / n + pw / 2f, zb = z0 + len * (i + 1) / n - pw / 2f - (ruinEnd && i + 1 == n ? RuinPierBack : 0f);
                int K = 12;
                var sof = new Vector3[K + 1];   // アーチの下の線（x=0 の断面）
                for (int k = 0; k <= K; k++)
                {
                    float z = Mathf.Lerp(za, zb, (float)k / K), tt = (z - (za + zb) / 2f) / ((zb - za) / 2f);
                    float yd = DeckY(z) - Tk;
                    float y = yd - 1.7f + (1.7f - 0.3f) * Mathf.Sqrt(Mathf.Max(0f, 1f - tt * tt));
                    sof[k] = new Vector3(0f, y, z);
                }
                for (int k = 0; k < K; k++)
                {
                    Vector3 a = sof[k], b = sof[k + 1];
                    float ya = DeckY(a.z) - Tk, yb = DeckY(b.z) - Tk;
                    // 下の面（穴あきの橋は両はしのリブだけ＋リブの内がわの壁と上の面）
                    if (!open) Quad(v, t, new Vector3(-xo, a.y, a.z), new Vector3(xo, a.y, a.z), new Vector3(xo, b.y, b.z), new Vector3(-xo, b.y, b.z), Vector3.down);
                    else foreach (float sx in new[] { -1f, 1f })
                    {
                        Quad(v, t, new Vector3(sx * xi, a.y, a.z), new Vector3(sx * xo, a.y, a.z), new Vector3(sx * xo, b.y, b.z), new Vector3(sx * xi, b.y, b.z), Vector3.down);
                        Quad(v, t, new Vector3(sx * xi, a.y, a.z), new Vector3(sx * xi, b.y, b.z), new Vector3(sx * xi, yb, b.z), new Vector3(sx * xi, ya, a.z), new Vector3(-sx, 0f, 0f));
                        Quad(v, t, new Vector3(sx * xi, ya, a.z), new Vector3(sx * xo, ya, a.z), new Vector3(sx * xo, yb, b.z), new Vector3(sx * xi, yb, b.z), Vector3.up);
                    }
                    // 横の壁（スパンドレル）
                    foreach (float sx in new[] { -1f, 1f })
                        Quad(v, t, new Vector3(sx * xo, a.y, a.z), new Vector3(sx * xo, b.y, b.z), new Vector3(sx * xo, yb, b.z), new Vector3(sx * xo, ya, a.z), new Vector3(sx, 0f, 0f));
                    // くさび石の輪（横の壁から 6cm 出る、はば 0.35）
                    var nrm = Vector3.Cross(Vector3.right, (b - a).normalized); if (nrm.y < 0) nrm = -nrm;   // アーチの線の外向き（上がわ）
                    float rw = 0.35f;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Vector3 a0 = new Vector3(sx * xr, a.y, a.z), b0 = new Vector3(sx * xr, b.y, b.z), a1 = a0 + nrm * rw, b1 = b0 + nrm * rw;
                        Quad(rv, rt, a0, b0, b1, a1, new Vector3(sx, 0f, 0f));
                        Quad(rv, rt, new Vector3(sx * xo, a.y, a.z), new Vector3(sx * xo, b.y, b.z), b0, a0, Vector3.down);
                        Quad(rv, rt, new Vector3(sx * xo, a1.y, a1.z), new Vector3(sx * xo, b1.y, b1.z), b1, a1, nrm);
                        // 石の継ぎ目に見える小さい切れ目（2つおきに少しへこませた面を足す…はやめて、色の段だけ）
                    }
                }
            }
            // 道のはしの横をおおう板（タイルの継ぎ目が縦筋に見えていた）：スパンドレルと同じ面（xo）で、道の下の面〜上の面。上はふさぐ
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 A2(float z, float dy, float x) => new Vector3(x, DeckY(z) + dy, z);
                var spans = new List<Vector2> { new Vector2(z0, z0 + len) };
                if (sx > 0 && gapPlusX.y > gapPlusX.x) spans = new List<Vector2> { new Vector2(z0, Mathf.Max(z0, gapPlusX.x)), new Vector2(Mathf.Min(z0 + len, gapPlusX.y), z0 + len) };   // 崩れた参道の橋がつながる所はあける（床と重ならないように）
                foreach (var sp in spans)
                {
                    if (sp.y - sp.x < 0.01f) continue;
                    Quad(v, t, A2(sp.x, -Tk, sx * xo), A2(sp.y, -Tk, sx * xo), A2(sp.y, 0f, sx * xo), A2(sp.x, 0f, sx * xo), new Vector3(sx, 0f, 0f));
                    Quad(v, t, A2(sp.x, 0f, sx * HW), A2(sp.y, 0f, sx * HW), A2(sp.y, 0f, sx * xo), A2(sp.x, 0f, sx * xo), Vector3.up);
                }
            }
            // コーニス（道のはしの下）
            foreach (float sx in new[] { -1f, 1f })
            {
                float x0 = sx * HW, x1 = sx * (HW + 0.14f);
                Vector3 A(float z, float dy, float x) => new Vector3(x, DeckY(z) + dy, z);
                Quad(rv, rt, A(z0, -0.38f, x1), A(z0 + len, -0.38f, x1), A(z0 + len, -0.12f, x1), A(z0, -0.12f, x1), new Vector3(sx, 0f, 0f));
                Quad(rv, rt, A(z0, -0.12f, x0), A(z0 + len, -0.12f, x0), A(z0 + len, -0.12f, x1), A(z0, -0.12f, x1), Vector3.up);
                Quad(rv, rt, A(z0, -0.38f, x0), A(z0 + len, -0.38f, x0), A(z0 + len, -0.38f, x1), A(z0, -0.38f, x1), Vector3.down);
            }
            MeshObj(isl, "BridgeStone", SaveMesh(bn + "_Stone", v, t), stone, Vector3.zero, 0, true);
            MeshObj(isl, "BridgeTrim", SaveMesh(bn + "_Trim", rv, rt), terraD, Vector3.zero, 0, true);
        }
        // 欄干：2m ほどの区切りごとに低い壁＋笠石。ところどころ崩れて切れている（橋の両はしはつなぐ）。gapPlusX の所は道の外へ橋の残骸がつながるのであけておく
        {
            var rnd = new System.Random(Mathf.RoundToInt(len * 37f + rise * 11f));
            float ang = -Mathf.Atan2(rise, len) * Mathf.Rad2Deg;
            var bvv = new List<Vector3>(); var btt = new List<int>();   // 崩れた欄干（ガタガタの壁）とがれき
            var pvv = new List<Vector3>(); var ptt = new List<int>(); var cvv = new List<Vector3>(); var ctt = new List<int>();   // ふつうの欄干の壁・笠石
            foreach (float sx in new[] { -1f, 1f })
            {
                int segs = Mathf.Max(3, Mathf.RoundToInt(len / 2f));
                int broken = ruinEnd && segs <= 3 ? -1 : 1 + rnd.Next(segs - 2);
                for (int s = 0; s < segs; s++)
                {
                    float za = z0 + len * s / segs, zb = z0 + len * (s + 1) / segs;
                    if (sx > 0 && gapPlusX.y > gapPlusX.x && zb > gapPlusX.x && za < gapPlusX.y) continue;
                    float zm = (za + zb) / 2f, L = zb - za;   // 2026-11-15 てじゃ「へんな縦筋」→ 区切りのすき間（0.06）をなくす
                    bool rampEnd = ruinEnd && s == segs - 1;   // 2026-11-16 崩れた道のはし（RuinedEnd）へ向かって欄干も低く崩れていく
                    bool br = s == broken || rampEnd;
                    float hgt = 0.38f;
                    if (!br)
                    {   // ふつうの区切り：壁と笠石は1つのメッシュにまとめる（別々の箱だと継ぎ目が縦筋に見えた）。当たりは見えない箱
                        var w = Box(isl, "ParapetCol", new Vector3(sx * (HW + 0.1f), DeckY(zm) + hgt / 2f, zm), new Vector3(0.22f, hgt, L), null, 0);
                        w.transform.localRotation = Quaternion.Euler(ang, 0f, 0f); Object.DestroyImmediate(w.GetComponent<MeshRenderer>()); Object.DestroyImmediate(w.GetComponent<MeshFilter>());
                        // 2026-11-15 てじゃ「クリスタル広場後の橋にスキマ」：道の板のふちは面取りで少し低く、欄干の下のはしとの間から外（空）が見えた → 欄干の壁を道の中まで（ParapetSink）下げる
                        FieldSlab(pvv, ptt, new Vector3(sx * (HW + 0.1f), DeckY(za) - ParapetSink, za), new Vector3(0f, rise / len, 1f), Vector3.up, Vector3.right, new[] { 0f, zb - za }, new[] { hgt + ParapetSink, hgt + ParapetSink }, new[] { 0f, 0f }, -0.11f, 0.11f);
                        FieldSlab(cvv, ctt, new Vector3(sx * (HW + 0.1f), DeckY(za) + hgt, za), new Vector3(0f, rise / len, 1f), Vector3.up, Vector3.right, new[] { 0f, zb - za }, new[] { 0.1f, 0.1f }, new[] { 0f, 0f }, -0.16f, 0.16f);
                    }
                    else
                    {   // 崩れた区切り：上のふちがガタガタに欠けて低くなった壁（両はしは高いまま、まん中ほど低い）＋岩のかけら
                        int nn = 14; var aa = Lin(0f, zb - za, nn);
                        var ee = new float[nn]; var dd = new float[nn];
                        for (int k = 0; k < nn; k++)
                        {
                            float u = (float)k / (nn - 1), mid = rampEnd ? Mathf.Clamp01((u - 0.2f) / 0.6f) : 1f - Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.22f);   // 両はし0 → まん中1（rampEnd：根もと0 → 先1）
                            ee[k] = Mathf.Lerp(hgt, 0.06f + 0.14f * (float)rnd.NextDouble(), mid) + (mid > 0.05f && mid < 0.95f ? ((float)rnd.NextDouble() - 0.5f) * 0.12f : 0f);
                            dd[k] = mid > 0.05f ? (float)rnd.NextDouble() * 0.05f : 0f;
                        }
                        ee[0] = hgt; if (!rampEnd) ee[nn - 1] = hgt;
                        for (int k = 0; k < nn; k++) ee[k] += ParapetSink;
                        FieldSlab(bvv, btt, new Vector3(sx * (HW + 0.1f), DeckY(za) - ParapetSink, za), new Vector3(0f, rise / len, 1f), Vector3.up, Vector3.right, aa, ee, dd, -0.11f, 0.11f);
                        if (Pebbles) for (int k = 0; k < 3; k++)
                        {
                            float rz = za + L * (0.3f + 0.4f * (float)rnd.NextDouble());
                            RockChunk(bvv, btt, new Vector3(sx * (HW - 0.15f - 0.25f * (float)rnd.NextDouble()), DeckY(rz) + 0.08f, rz), Vector3.one * (0.16f + 0.14f * (float)rnd.NextDouble()),
                                Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, 0f), rnd);
                        }
                    }
                }
            }
            if (bvv.Count > 0) MeshObj(isl, "ParapetBroken", SaveMesh(bn + "_Broken", bvv, btt), stone, Vector3.zero, 0, true);
            if (pvv.Count > 0) MeshObj(isl, "Parapet", SaveMesh(bn + "_Parapet", pvv, ptt), stone, Vector3.zero, 0, true);
            if (cvv.Count > 0) MeshObj(isl, "ParapetCap", SaveMesh(bn + "_ParapetCap", cvv, ctt), terraD, Vector3.zero, 0, true);
        }
        HW = hw0;
    }

    // 崩れたアーチの門（カーテンの柱の外に、上が少し欠けたアーチ）
    static int archGateNo;
    static void ArchGate()
    {
        float w = 2f * HW + 1.6f, h = CurtainH + 1.2f;
        if (!ArchGateNew)
        {
            foreach (float sx in new[] { -1f, 1f }) Box(isl, "GatePillar", new Vector3(sx * (w / 2f), P.y + h / 2f - 0.6f, P.z), new Vector3(0.9f, h + 0.6f, 1.1f), terra, 0);
            Box(isl, "GateTopL", new Vector3(-w / 4f - 0.3f, P.y + h + 0.2f, P.z), new Vector3(w / 2f, 0.8f, 1.1f), terra, 0);
            var br = Box(isl, "GateTopBroken", new Vector3(w / 4f + 0.2f, P.y + h + 0.1f, P.z), new Vector3(w / 4f, 0.7f, 1.1f), terraD, 0);
            br.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            return;
        }
        // 2026-11-15 見た目クオリティ（3）：四角い柱と板 → 溝のある柱2本（根もとは道の下の面）＋平たいくさび石のアーチ（1つは外がわが欠ける）
        int no = archGateNo++;
        var g = new GameObject("ArchGate").transform; g.SetParent(isl, false); g.localPosition = new Vector3(0f, P.y, P.z);
        float cx = w / 2f, colH = h - 0.6f + 0.6f;   // 道の下（-0.6）から h まで
        foreach (float sx in new[] { -1f, 1f })
        {
            var co = new GameObject(sx < 0 ? "ColumnL" : "ColumnR"); co.transform.SetParent(g, false); co.transform.localPosition = new Vector3(sx * cx, -0.6f, 0f);
            float ch = colH;
            co.AddComponent<MeshFilter>().sharedMesh = ColumnMesh("ArchGateColumn" + no + (sx < 0 ? "L" : "R"), 700 + no * 2 + (sx < 0 ? 0 : 1), ch, 0);
            colFix.Add((co.transform, "ArchGateColumn" + no + (sx < 0 ? "L" : "R"), 700 + no * 2 + (sx < 0 ? 0 : 1), ch, 0, HW));   // 2026-11-15 てじゃ「はしらういてる」→ 岩ができたあとに地面までのばす
            co.AddComponent<MeshRenderer>().sharedMaterials = new[] { terra, stone };
            var cc = co.AddComponent<CapsuleCollider>(); cc.radius = 0.4f; cc.height = ch; cc.center = Vector3.up * ch / 2f;
        }
        // 平たいアーチ：柱の上（y0）から rise だけ上がる円弧
        float y0 = colH - 0.6f, rise = 1.3f, R = (cx * cx + rise * rise) / (2f * rise), cyc = y0 + rise - R;
        float th0 = Mathf.Asin(Mathf.Clamp(cx / R, -1f, 1f));   // 縦からの角度
        var v = new List<Vector3>(); var t = new List<int>(); var kv = new List<Vector3>(); var kt = new List<int>();
        var rnd = new System.Random(1300 + no);
        int n = 11, brk = 2 + rnd.Next(2) * (n - 5);
        float thick = 0.75f, d = 0.5f;
        for (int i = 0; i < n; i++)
        {
            float a0 = Mathf.PI / 2f + th0 - 2f * th0 * i / n, a1 = Mathf.PI / 2f + th0 - 2f * th0 * (i + 1) / n;
            bool key = i == n / 2;
            float rIn = R - thick * 0.5f, ro = R + thick * (key ? 0.75f : 0.5f) * (0.96f + 0.06f * (float)rnd.NextDouble());
            Vector3 Q(float a, float r, float z) => new Vector3(Mathf.Cos(a) * r, cyc + Mathf.Sin(a) * r, z);
            var tv = key ? kv : v; var tt = key ? kt : t;
            float dd = key ? d + 0.08f : d;
            int m = i == brk ? 6 : 2;   // 欠けたくさび石は外がわをガタガタに
            var outerF = new Vector3[m]; var outerB = new Vector3[m];
            for (int k = 0; k < m; k++)
            {
                float aa = Mathf.Lerp(a1, a0, (float)k / (m - 1));
                float rr = (i == brk && k > 0 && k < m - 1) ? R + thick * (-0.15f + 0.5f * (float)rnd.NextDouble()) : ro;
                outerF[k] = Q(aa, rr, -dd + (i == brk ? (float)rnd.NextDouble() * 0.1f : 0f)); outerB[k] = Q(aa, rr, dd);
            }
            var polyF = new List<Vector3> { Q(a0, rIn, -dd), Q(a1, rIn, -dd) }; polyF.AddRange(outerF);
            var polyB = new List<Vector3> { Q(a0, rIn, dd), Q(a1, rIn, dd) }; polyB.AddRange(outerB);
            // 2026-11-15 てじゃ「アーチ上部の欠けてるところに穴」：欠けた石の外がわはへこんだ多角形なので、
            //  まん中からの扇（ふた）と「まん中から外向き」の横の面の向きが、へこみの所で裏返って穴になっていた
            //  → ふたは耳切りで三角に分ける。横の面の外向きは多角形の回る向きから決める
            var tris = EarClipXY(polyF);
            foreach (var tr in tris) { Tri(tv, tt, polyF[tr.x], polyF[tr.y], polyF[tr.z], Vector3.back); Tri(tv, tt, polyB[tr.x], polyB[tr.y], polyB[tr.z], Vector3.forward); }
            float area2 = 0f; for (int k = 0; k < polyF.Count; k++) { var p0 = polyF[k]; var p1 = polyF[(k + 1) % polyF.Count]; area2 += p0.x * p1.y - p1.x * p0.y; }
            float ori = area2 >= 0f ? 1f : -1f;   // 1＝反時計回り（xy）
            for (int k = 0; k < polyF.Count; k++)
            {
                int k2 = (k + 1) % polyF.Count;
                var e = polyF[k2] - polyF[k];
                var outN = new Vector3(e.y, -e.x, 0f) * ori;
                Tri(tv, tt, polyF[k], polyF[k2], polyB[k2], outN); Tri(tv, tt, polyF[k], polyB[k2], polyB[k], outN);
            }
        }
        MeshObj(g, "Arch", SaveMesh("ArchGateArch" + no, v, t), terra, Vector3.zero, 0, true);
        MeshObj(g, "Keystone", SaveMesh("ArchGateKey" + no, kv, kt), stone, Vector3.zero, 0, true);
        curtainOnArch = true; curtainArchTop = colH - 0.6f;
    }
    // 2026-11-15 てじゃ「クモの巣とかカーテン、奥のやつが見えすぎてるのがきになる」「重なる時」→ 2–5 の巣・カーテンだけ、遠いほどうすく
    public static float FarFadeStart = 3f, FarFadeEnd = 8f, FarFadeMin = 0.35f;   // 8→16 → 5→11（もうちょい強く）→ てじゃ「もっと強めるかわりに、薄さの最大値を減らそう」
    static void ApplyFarFade()
    {
        var done = new HashSet<Material>(); int n = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || done.Contains(m)) continue;
                if (m.shader.name != "FuwaCourse/SpiderWeb" && m.shader.name != "Fuwa/SizeCurtain") continue;
                done.Add(m);
                m.SetFloat("_FarFadeStart", FarFadeStart); m.SetFloat("_FarFadeEnd", FarFadeEnd); m.SetFloat("_FarFadeMin", FarFadeMin);
                EditorUtility.SetDirty(m); n++;
            }
        log.Append("遠くでうすく " + n + " ");
    }
    // 柱の根もとの下に地面（当たり）があれば、そこまでの距離（少し埋める）。何もない・遠い（空中）なら 0
    public static float ColumnMaxDrop = 6f, ColumnBury = 0.02f, ColumnCornerTol = 0.1f;
    static List<(Transform co, string nm, int seed, float h, int kind, float hw)> colFix = new List<(Transform, string, int, float, int, float)>();
    public static bool ColumnCorbels = true; public static float CorbelDepth = 1.7f;
    // 岩（RockMesh）などができたあとで：記録した柱の根もとが浮いていたら、下へのばして作り直す（てっぺんの高さはそのまま）
    static void FixColumnGround()
    {
        int n = 0;
        foreach (var c in colFix)
        {
            if (!c.co) continue;
            float drop = ColumnGroundDrop(c.co, out bool any);
            if (!any && ColumnCorbels) { ColumnCorbel(c.co, c.nm, c.hw); continue; }   // 2026-11-15 てじゃ「柱が浮いてるのをなんとかしたいけど外側は歩かせたくない」→ 道の横から出る石の持ち送り（上は柱の台の下にかくれる、外は急な斜め＝乗れない）
            if (drop == 0f) continue;
            c.co.localPosition += Vector3.down * drop; float h = c.h + drop;
            c.co.GetComponent<MeshFilter>().sharedMesh = ColumnMesh(c.nm, c.seed, h, c.kind);
            var cc = c.co.GetComponent<CapsuleCollider>(); if (cc) { cc.height = h; cc.center = Vector3.up * h / 2f; }
            n++;
        }
        colFix.Clear();
        log.Append("柱の根もと" + n + " ");
    }
    static float ColumnGroundDrop(Transform co) => ColumnGroundDrop(co, out _);
    static float ColumnGroundDrop(Transform co, out bool any)
    {
        any = false;
        Physics.SyncTransforms();
        var p = co.position; float sc = Mathf.Max(1e-3f, co.lossyScale.y);
        float best = -1e9f, dC = 0f;
        // 根もとの台の四すみ近くと まん中で調べて、一番遠い所まで（台のはしが浮かないように）
        foreach (var o in new[] { Vector3.zero, new Vector3(0.45f, 0, 0.45f), new Vector3(-0.45f, 0, 0.45f), new Vector3(0.45f, 0, -0.45f), new Vector3(-0.45f, 0, -0.45f) })
        {
            var q = p + co.TransformVector(o) + Vector3.up * 0.3f * sc;
            var hits = Physics.RaycastAll(q, Vector3.down, (ColumnMaxDrop + 0.3f) * sc, ~0, QueryTriggerInteraction.Ignore);
            float d = -1f;
            // 2026-11-15 てじゃ「柱の支えが貫通してる」→ 島の上の面は根もとより 5cm 上にあるので、少し上（-0.2 まで）の地面も「ある」にする（前はなし扱い→持ち送りが島をつきぬけた）
            bool hitAny = false;
            foreach (var hh in hits) { if (hh.collider.transform.IsChildOf(co)) continue; float dd = hh.distance / sc - 0.3f; if (dd < -0.2f) continue; if (!hitAny || dd < d) d = dd; hitAny = true; }
            if (!hitAny) continue;
            // （地面が根もとより上なら d はマイナス＝柱を上げる）
            if (o == Vector3.zero) { any = true; dC = d; }
            // 2026-11-15 てじゃ「まだある」→ 島のふちの柱：すみの1つが上の層のふちの外に出て、下の層（1m 以上下）に当たって柱ごと沈んでいた
            //  → まん中が当たっている時は、まん中より ColumnCornerTol 以上深いすみは見ない（台のはしが少し浮くだけ）
            if (o != Vector3.zero && any && d > dC + ColumnCornerTol) continue;
            best = Mathf.Max(best, d);
        }
        // 2026-11-15 てじゃ「下の板部分うまってる」→ 埋めるのは 2cm だけ（前は 12cm で台の板がうまって見えた）。地面が少し上なら上げる
        if (best < -1e8f) return 0f;
        float drop = best + ColumnBury;
        return Mathf.Abs(drop) > 0.01f ? drop : 0f;
    }
    // 空中の道のわきの柱の下：道の横の面から出る石の持ち送り（柱の座標。原点＝柱の根もとのまん中、x は道を横切る向き）
    //  上の面は柱の台（0.54 角）の下にかくれる、外の面は下へ道の方へ急に斜め（乗れない）、内の面は道の横の面より少し中
    static void ColumnCorbel(Transform co, string nm, float hw)
    {
        float sgn = Mathf.Sign(co.localPosition.x == 0f ? 1f : co.localPosition.x);
        float e = Mathf.Abs(co.localPosition.x) - hw;            // 道のはし → 柱のまん中
        float xi = -sgn * (e + 0.06f), xo = sgn * 0.5f, xb = xi + sgn * 0.32f;
        float zt = 0.5f, zb = 0.3f, D = CorbelDepth, y0 = -0.002f;
        var T = new[] { new Vector3(xi, y0, -zt), new Vector3(xo, y0, -zt), new Vector3(xo, y0, zt), new Vector3(xi, y0, zt) };
        var B = new[] { new Vector3(xi, y0 - D, -zb), new Vector3(xb, y0 - D, -zb), new Vector3(xb, y0 - D, zb), new Vector3(xi, y0 - D, zb) };
        var v = new List<Vector3>(); var t = new List<int>();
        var cen = (T.Aggregate(Vector3.zero, (a, b) => a + b) + B.Aggregate(Vector3.zero, (a, b) => a + b)) / 8f;
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { var w = (a + b + c + d) * 0.25f - cen; Tri(v, t, a, b, c, w); Tri(v, t, a, c, d, w); }
        Q(T[0], T[1], T[2], T[3]); Q(B[0], B[1], B[2], B[3]);
        for (int k = 0; k < 4; k++) { int j = (k + 1) % 4; Q(T[k], T[j], B[j], B[k]); }
        var go = new GameObject("Corbel"); go.transform.SetParent(co, false);
        go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(nm + "_Corbel", v, t);
        go.AddComponent<MeshRenderer>().sharedMaterial = stone;
    }
    public static bool CoursePerf = true; public static float PortalCamFar = 600f;
    public static bool ColumnFlatBreak = true; public static float ColumnRingStep = 1.1f;
    public static bool ArchGateNew = true, StoneLook = true, CurtainPattern = true, OpenPitBridge = true;
    public static Color TerraCol = new Color(0.84f, 0.68f, 0.58f), TerraDCol = new Color(0.69f, 0.53f, 0.45f);
    static bool curtainOnArch; static float curtainArchTop;   // ArchGate のすぐあとの Curtain は柱の間に張る

    // ---- 列柱の回廊（2026-11-14 てじゃ「柱式にもどしたい」）：道の両わきに柱、巣の門の所は柱2本＋上のはり（巣はその間に張る）----
    // 巣の横のはし：トンネルの時は壁に 0.5 めりこませる。柱式は柱（まん中 HW+0.5）の胴の表面まで（2026-11-15 てじゃ「クモの巣が柱にめりこんでるのきになる」）
    public static float ColShaftR = 0.3f;
    static float WebXO => HW + (CorridorPillars ? 0.5f - ColShaftR : 0.5f);
    public static bool CorridorPillars = true, GateBeams = false, PlainColumns = false;
    public static float PillarStep = 2.6f, PillarSize = 0.8f;
    static void CorridorColonnade(Transform runA, Transform runB)
    {
        float H = Mathf.Max(TunnelH, CurtainH), xc = HW + 0.5f;   // 巣の横のはし（HW+0.5）が柱のまん中に来る。2026-11-15 高さは下の段のアーチの柱にそろえる（てっぺん CurtainH+0.6）
        foreach (var run in new[] { runA, runB })
        {
            var g = new GameObject("Colonnade").transform; g.SetParent(run, false);
            float end = LocalBounds(run, run).max.z, z0 = run == runA ? 2.6f : 0.6f;   // A はワープの到着の床のあとから
            var gz = gateZ.Where(x => x.run == run).Select(x => x.z).ToList();
            int pi = 0;
            foreach (float z in gz)
            {
                // 2026-11-15 見た目クオリティ：角柱 → 溝のある古い柱（ColumnMesh、根もとは道の下の面の高さ）。巣の上のはしより高いまま、完全と上が折れたのをまぜる
                foreach (float sx in new[] { -1f, 1f })
                {
                    int seed = (run == runA ? 100 : 200) + pi;
                    int kind = (seed * 7) % 3 == 0 ? 1 : 0;
                    float ch = H + 1.2f;   // 道の下（-0.6）から、てっぺんは H+0.6 くらい
                    var co = new GameObject("GatePillar"); co.transform.SetParent(g, false); co.transform.localPosition = new Vector3(sx * xc, -0.6f, z);
                    co.transform.localRotation = Quaternion.identity;   // 2026-11-15 てじゃ「向きがバラバラ」→ 台と柱頭の四角を道の向きにそろえる
                    co.AddComponent<MeshFilter>().sharedMesh = ColumnMesh("CorridorColumn" + seed, seed, ch, kind);
                    co.AddComponent<MeshRenderer>().sharedMaterials = new[] { terra, stone };
                    var cc = co.AddComponent<CapsuleCollider>(); cc.radius = 0.4f; cc.height = ch; cc.center = Vector3.up * ch / 2f;
                    colFix.Add((co.transform, "CorridorColumn" + seed, seed, ch, kind, HW));
                    pi++;
                }
                if (GateBeams) Box(g, "GateBeam", new Vector3(0f, H + 0.25f, z), new Vector3(2f * xc + PillarSize + 0.4f, 0.5f, PillarSize + 0.1f), terra, 0);   // 2026-11-14 てじゃ「上の板いらんかも」→ なし
            }
            if (PlainColumns) for (float z = z0; z < end - 0.4f; z += PillarStep)   // 2026-11-14 てじゃ「柱おおい、クモの巣まとめて柱へらそう」→ ふつうの柱はなし、巣の門の柱だけ
            {
                if (gz.Any(q => Mathf.Abs(q - z) < 1.2f)) continue;   // 門の柱と近すぎる所はぬく
                foreach (float sx in new[] { -1f, 1f })
                    Box(g, "Column", new Vector3(sx * xc, (H + 0.6f) / 2f - 0.6f, z), new Vector3(PillarSize * 0.85f, H + 0.6f, PillarSize * 0.85f), stone, 0);
            }
        }
        log.Append("列柱 ");
    }

    // ---- 石のトンネルの回廊（2026-11-14 てじゃ「石の壁で包まれた道」「屋根もあっていい、たまに崩れてちょっと見えてる程度」）----
    //  runA →（turnDeg の曲がり角）→ runB の道を、両わきの壁（道のふちにぴったり）＋平らな屋根でつつむ。
    //  SegL ごとに区切って、たまに片側の壁が低く崩れて、その上の屋根も半分ない（外と空がちょっと見える）
    //  同じ高さの面が重なるとちらつくので、runB の壁・屋根は +0.03、曲がり角の外の壁は1つおきに +0.04、曲がり角の屋根は +0.06
    public static float TunnelHW = 3.3f;   // トンネルの中のはばの半分（前は島の道と同じ 2.4）
    public static float TunnelH = 5.4f, TunnelWall = 0.6f, TunnelRoof = 0.45f, TunnelSegL = 2.4f, TunnelBreak = 0.2f;
    public static int TunnelRuinAfter = 2; public static float TunnelRuinKeep = 0.35f, TunnelRoofKeep = 0.6f;   // 何個目の崩れから屋根がほとんどなくなるか、そこから先で屋根が残る割合
    static void CorridorTunnel(Transform runA, Transform runB, float turnDeg, float zStartA)
    {
        var rnd = new System.Random(77);
        var rndR = new System.Random(78);   // 屋根の崩れ用（rnd とは別にして、壁の崩れる場所が変わらないように）
        int breaks = 0;
        float wt = TunnelWall, H = TunnelH;
        void Seg(Transform run, string nm, float z0, float z1, float dy, bool canBreak, float innerTrimEnd, float innerTrimStart, float innerSx)
        {
            var g = new GameObject(nm).transform; g.SetParent(run, false);
            int n = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / TunnelSegL)); float sl = (z1 - z0) / n;
            int lastBreak = -9;
            for (int i = 0; i < n; i++)
            {
                float a = z0 + i * sl, b = a + sl;
                int brk = 0;   // 0 なし、-1 左、+1 右
                if (canBreak && i > 0 && i < n - 1 && i - lastBreak > 2 && rnd.NextDouble() < TunnelBreak) { brk = rnd.Next(2) == 0 ? -1 : 1; lastBreak = i; breaks++; }
                bool ruined = breaks >= TunnelRuinAfter;   // 2026-11-14 てじゃ「2個目の天井崩れたあたりから天井かなり崩そう、逆にちょっと天井のこってる、くらい」
                foreach (float sx in new[] { -1f, 1f })
                {
                    float wa = a, wb = b;
                    if (sx == innerSx && i == n - 1) wb -= innerTrimEnd;   // 曲がり角の内がわ：となりの壁と重ならないように切る
                    if (sx == innerSx && i == 0) wa += innerTrimStart;
                    float x = sx * (HW + wt / 2f);
                    if (brk == (int)sx)
                    {
                        // 崩れた壁：低い段＋もう少し高い段（ギザギザ）
                        float h1 = 1.1f + (float)rnd.NextDouble() * 0.9f, h2 = h1 + 0.5f + (float)rnd.NextDouble() * 0.6f;
                        bool firstHalf = rnd.Next(2) == 0; float m = (wa + wb) / 2f;
                        Box(g, "WallBroken", new Vector3(x, -0.6f + (h1 + 0.6f) / 2f + dy, firstHalf ? (m + wb) / 2f : (wa + m) / 2f), new Vector3(wt, h1 + 0.6f, (wb - wa) / 2f), stone, 0);
                        Box(g, "WallBroken", new Vector3(x, -0.6f + (h2 + 0.6f) / 2f + dy, firstHalf ? (wa + m) / 2f : (m + wb) / 2f), new Vector3(wt, h2 + 0.6f, (wb - wa) / 2f), stone, 0);
                        // 崩れたかけら（壁の上に転がってる。当たりなし）
                        var ch = Box(g, "Rubble", new Vector3(x + sx * 0.2f, h1 + 0.15f + dy, m + ((float)rnd.NextDouble() - 0.5f) * sl * 0.4f), new Vector3(0.5f, 0.3f, 0.6f), stone, 0);
                        ch.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 60f - 30f, (float)rnd.NextDouble() * 20f - 10f); Object.DestroyImmediate(ch.GetComponent<Collider>());
                    }
                    else Box(g, "Wall", new Vector3(x, -0.6f + (H + 0.6f) / 2f + dy, (wa + wb) / 2f), new Vector3(wt, H + 0.6f, wb - wa), stone, 0);
                }
                // 屋根：崩れた区切りは、崩れた側の半分がない
                float rx0 = -(HW + wt), rx1 = HW + wt;
                if (brk == -1) rx0 = 0.4f; else if (brk == 1) rx1 = -0.4f;
                float ra = a, rb = b;
                if (ruined && brk == 0)
                {
                    // ほとんど崩れた屋根：TunnelRuinKeep の割合だけ、片側の壁から出っぱる短い屋根が残る（長さも少し短い）
                    if (rndR.NextDouble() > TunnelRuinKeep) continue;
                    bool fromLeft = rndR.Next(2) == 0; float w = (2f * HW + 2f * wt) * (0.35f + 0.35f * (float)rndR.NextDouble());
                    if (fromLeft) rx1 = rx0 + w; else rx0 = rx1 - w;
                    float cut = sl * 0.3f * (float)rndR.NextDouble(); if (rndR.Next(2) == 0) ra += cut; else rb -= cut;
                }
                else if (ruined) continue;   // 崩れた壁の所は屋根なし
                else if (brk == 0 && TunnelRoofKeep < 1f)
                {
                    // 2026-11-14 てじゃ「屋根きっちり覆わなくてもいいかもな」→ 前半も TunnelRoofKeep の割合だけ残す（残る屋根も幅・長さが少し欠ける）
                    if (rndR.NextDouble() > TunnelRoofKeep) continue;
                    if (rndR.Next(3) > 0)
                    {
                        bool fromLeft = rndR.Next(2) == 0; float w = (2f * HW + 2f * wt) * (0.55f + 0.4f * (float)rndR.NextDouble());
                        if (fromLeft) rx1 = rx0 + w; else rx0 = rx1 - w;
                    }
                    float cut = sl * 0.25f * (float)rndR.NextDouble(); if (rndR.Next(2) == 0) ra += cut; else rb -= cut;
                }
                Box(g, "Roof", new Vector3((rx0 + rx1) / 2f, H + TunnelRoof / 2f + dy, (ra + rb) / 2f), new Vector3(rx1 - rx0, TunnelRoof, rb - ra), terraD, 0);
            }
        }
        float endA = LocalBounds(runA, runA).max.z, endB = LocalBounds(runB, runB).max.z;
        float sg = Mathf.Sign(turnDeg);       // − なら右へ曲がる（内がわは右 = +x）
        float innerSx = -sg;
        float trim = 0f;   // 内がわの壁は角で重なる（切るとすき間があいた）。重なりは runB の +0.03 でちらつかない
        Seg(runA, "TunnelA", zStartA, endA, 0f, true, trim, 0f, innerSx);
        Seg(runB, "TunnelB", 0f, endB, 0.03f, true, 0f, trim, innerSx);
        // ポータルのうしろの壁
        Box(runA, "TunnelBackWall", new Vector3(0f, -0.6f + (H + 0.6f) / 2f, zStartA - wt / 2f), new Vector3(2f * (HW + wt), H + 0.6f, wt), stone, 0);
        // 曲がり角：外がわの壁（円弧）と屋根（扇）。runA の座標で、内がわの角（道のふち）を中心に回る
        {
            var g = new GameObject("TunnelTurn").transform; g.SetParent(runA, false);
            var pivot = new Vector3(innerSx * HW, 0f, endA);
            int n = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(turnDeg) / 7.5f));
            float R = 2f * HW + wt / 2f, step = Mathf.Abs(turnDeg) / n;
            var outDir = new Vector3(-innerSx, 0f, 0f);
            for (int i = 0; i < n; i++)
            {
                float ang = -turnDeg * (i + 0.5f) / n;
                var rot = Quaternion.Euler(0f, ang, 0f);
                float len = 2f * (R + wt / 2f) * Mathf.Sin(step * 0.5f * Mathf.Deg2Rad) + 0.02f;
                var w = Box(g, "WallArc", pivot + rot * (outDir * R) + Vector3.up * (-0.6f + (H + 0.6f) / 2f + 0.04f + (i % 2) * 0.04f), new Vector3(wt, H + 0.6f, len), stone, 0);
                w.transform.localRotation = rot;
            }
            // 屋根の扇（メッシュ）
            var v = new List<Vector3>(); var tr = new List<int>(); float Ro = 2f * HW + wt, y0 = H + 0.06f, y1 = H + TunnelRoof + 0.06f;
            var arc = new List<Vector3>();
            for (int i = 0; i <= n; i++) arc.Add(pivot + Quaternion.Euler(0f, -turnDeg * i / n, 0f) * (outDir * Ro));
            for (int i = 0; i < n; i++)
            {
                Tri(v, tr, pivot + Vector3.up * y1, arc[i] + Vector3.up * y1, arc[i + 1] + Vector3.up * y1, Vector3.up);
                Tri(v, tr, pivot + Vector3.up * y0, arc[i] + Vector3.up * y0, arc[i + 1] + Vector3.up * y0, Vector3.down);
                var nrm = (arc[i] + arc[i + 1]) * 0.5f - pivot; nrm.y = 0f;
                Tri(v, tr, arc[i] + Vector3.up * y1, arc[i + 1] + Vector3.up * y1, arc[i + 1] + Vector3.up * y0, nrm);
                Tri(v, tr, arc[i] + Vector3.up * y1, arc[i + 1] + Vector3.up * y0, arc[i] + Vector3.up * y0, nrm);
            }
            var m = SaveMesh("TunnelTurnRoof", v, tr);
            var rg = new GameObject("RoofFan"); rg.transform.SetParent(g, false);
            rg.AddComponent<MeshFilter>().sharedMesh = m; rg.AddComponent<MeshRenderer>().sharedMaterial = terraD; rg.AddComponent<MeshCollider>().sharedMesh = m;
        }
        log.Append("トンネル ");
    }

    // 列柱：道の両わきに柱（len の間）
    static void Colonnade(float len)
    {
        for (float z = 0.8f; z < len; z += 2.6f)
            foreach (float sx in new[] { -1f, 1f })
                Box(isl, "Column", new Vector3(sx * (HW + 0.9f), P.y + 2.6f, P.z + z), new Vector3(0.8f, 5.8f, 0.8f), stone, 0);
    }

    // 島の岩：その島の道の範囲の下に、平らな岩の台＋先細りの岩。hill なら内がわに丘
    static void IslandRock(Transform island, float margin, bool hill, float depth = 5f, float heading = 90f)
    {
        // heading：島の道のおおよその向き（斜めの島は、その向きの箱で岩を作る。前はコースの軸の箱で、斜めの島がでかい四角になった）
        var rk = new GameObject("Rock").transform; rk.SetParent(island, false); rk.localRotation = Quaternion.LookRotation(Dir(heading));
        var b = LocalBounds(island, rk);
        if (b.size.sqrMagnitude < 0.01f) { Object.DestroyImmediate(rk.gameObject); return; }
        float top = b.min.y - 0.55f;
        Box(rk, "Top", new Vector3(b.center.x, top - 0.5f, b.center.z), new Vector3(b.size.x + 2f * margin, 1f, b.size.z + 2f * margin), moss, pathLayer);   // 地面（ふわふわが着いたらアウト＝道と同じレイヤー）
        var u1 = Box(rk, "Under1", new Vector3(b.center.x, top - 1f - depth / 2f, b.center.z), new Vector3((b.size.x + 2f * margin) * 0.85f, depth, (b.size.z + 2f * margin) * 0.85f), underM, 0); Object.DestroyImmediate(u1.GetComponent<Collider>());
        var u2 = Box(rk, "Under2", new Vector3(b.center.x, top - 1f - depth - depth * 0.4f, b.center.z), new Vector3((b.size.x + 2f * margin) * 0.5f, depth * 0.8f, (b.size.z + 2f * margin) * 0.5f), underM, 0); Object.DestroyImmediate(u2.GetComponent<Collider>());
        if (hill) Box(rk, "Hill", new Vector3(b.center.x, (top + b.max.y) / 2f - 0.5f, b.center.z), new Vector3(Mathf.Max(2f, b.size.x - 7f), b.max.y - top - 1f, Mathf.Max(2f, b.size.z - 7f)), moss, 0);
    }

    // 飾りの小さい浮島（当たりなし）
    // Blender の浮島（Islet0〜3、上の面が y=0、半径およそ 1、深さおよそ 2）
    static Mesh IsletMesh(int i)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FuwaStage2/Models/Islets.fbx"); if (!go) return null;
        var mfs = go.GetComponentsInChildren<MeshFilter>(); if (mfs.Length == 0) return null;
        return mfs[((i % mfs.Length) + mfs.Length) % mfs.Length].sharedMesh;
    }
    static void DecoIslets()
    {
        var g = new GameObject("DecoIslets").transform; g.SetParent(islandsT, false);
        var all = LocalBounds(islandsT, root);
        var rnd = new System.Random(5);
        // 道にかぶらないように：島ごとの範囲（少し広げる）に入ったら引き直す（2026-11-13 空中の回廊を足したら小島とかぶりそうだった）
        var keep = new List<Bounds>();
        foreach (Transform ch in islandsT) if (ch != g) { var b = LocalBounds(ch, root); if (b.size.sqrMagnitude > 0.01f) { b.Expand(new Vector3(8f, 10f, 8f)); keep.Add(b); } }
        var rndY = new System.Random(55);
        Physics.SyncTransforms();
        for (int i = 0; i < 9; i++)
        {
            var c = Vector3.zero;
            float s = 2f + 3f * (float)rnd.NextDouble();
            for (int k = 0; k < 60; k++)
            {
                c = new Vector3(Mathf.Lerp(all.min.x - 8f, all.max.x + 8f, (float)rnd.NextDouble()), Mathf.Lerp(-6f, 16f, (float)rnd.NextDouble()), Mathf.Lerp(all.min.z, all.max.z, (float)rnd.NextDouble()));
                if (keep.Any(kb => kb.Contains(c))) continue;
                // 2026-11-15 小島が道やアーチにかぶった → まわり（島の大きさ＋4m、下へ島の深さぶん）に当たりがあるならやり直す
                var wc = root.TransformPoint(c + Vector3.down * s * 0.4f);
                if (Physics.CheckCapsule(wc + Vector3.up * (s * 0.6f + 4f), wc - Vector3.up * (s * 0.6f + 2f), s * 0.6f + 4f, ~0, QueryTriggerInteraction.Collide)) continue;
                break;
            }
            var islet = IsletMesh(i);
            if (islet)
            {   // 2026-11-15 見た目クオリティ：箱2つ → Blender の浮島（Models/Islets.fbx、草のふた＋ゴツゴツの岩）
                var io = MeshObj(g, "Islet", islet, moss, c, 0, true);
                io.GetComponent<MeshRenderer>().sharedMaterials = new[] { moss, underM };
                io.transform.localScale = Vector3.one * (s * 0.5f); io.transform.localRotation = Quaternion.Euler(0f, (float)rndY.NextDouble() * 360f, 0f);
                continue;
            }
            var t = Box(g, "IsletTop", c, new Vector3(s, 0.5f, s * 0.8f), moss, 0); Object.DestroyImmediate(t.GetComponent<Collider>());
            var u = Box(g, "IsletUnder", c + Vector3.down * s * 0.45f, new Vector3(s * 0.8f, s * 0.8f, s * 0.65f), underM, 0); Object.DestroyImmediate(u.GetComponent<Collider>());
        }
    }

    // ================= 2026-11-13 島とカーテン =================
    public static float StartRun = 4f, IslandGapX = 6f, FirstMinX = 22f, IslandDY = 2f, IslandGapZ = 3f;
    static Transform isl, islandsT;
    static Texture2D curtainFade;
    public static float WinHalf = 0.12f, WinY = 1.3f;   // 小さい窓のポータル（今は使っていない）

    static Vector3 BuildIslands()
    {
        islandsT = new GameObject("Islands").transform; islandsT.SetParent(root, false);
        var A0 = BigPortal(pathT, "Portal0", P, 180f);   // スタートの道のつきあたり
        var list = new List<(Transform island, Transform B, Transform A)>();
        Transform B; Transform A;
        // ① 横風の島：ふつう → 大 → 小
        B = BeginIsland("I1_Wind");
        S(2f);
        WindRun("N", 7f, new[] { 3.5f });
        S(1.2f); Curtain("C1_Big", 1); S(1.2f);
        WindRun("B", 9f, new[] { 2.5f, 6.5f });
        S(1.2f); Curtain("C1_Small", -1); S(1.2f);
        WindRun("S", 9f, new[] { 2.5f, 6.5f });
        S(2.5f); A = EndIsland(); list.Add((isl, B, A));
        // ② 巣の島：ふつう → 小 → 大
        B = BeginIsland("I2_Webs");
        S(1.5f); CPi("Checkpoint1", 0.8f); S(1.5f);
        Curtain("C2_Normal", 0); S(1.2f);
        WebRun("N", 7f, 1.7f);
        S(1.2f); Curtain("C2_Small", -1); S(1.2f);
        WebRun("S", 7f, 1.7f);
        S(1.2f); Curtain("C2_Big", 1); S(1.2f);
        WebRun("B", 7f, 1.9f);
        S(2.5f); A = EndIsland(); list.Add((isl, B, A));
        // ③ 重力の島：小 → 大
        B = BeginIsland("I3_Gravity");
        S(1.5f); CPi("Checkpoint2", 0.8f); S(1.5f);
        Curtain("C3_Small", -1); S(1.5f);
        GravRun("S", 10f);
        S(1.2f); Curtain("C3_Big", 1); S(1.5f);
        GravRun("B", 10f);
        S(2.5f); A = EndIsland(); list.Add((isl, B, A));
        // ④ 最後：えらぶカーテン＋短いギミック
        B = BeginIsland("I4a_Wind"); S(1.5f); CPi("Checkpoint3", 0.8f); S(1f); Choice("C4a"); S(1f); WindRun("M", 7f, new[] { 2f, 5.5f }); S(2.5f); A = EndIsland(); list.Add((isl, B, A));
        B = BeginIsland("I4b_Webs"); S(1.5f); CPi("Checkpoint4", 0.8f); S(1f); Choice("C4b"); S(1f); WebRun("M", 7f, 1.8f); S(2.5f); A = EndIsland(); list.Add((isl, B, A));
        B = BeginIsland("I4c_Gravity"); S(1.5f); CPi("Checkpoint5", 0.8f); S(1f); Choice("C4c"); S(1f); GravRun("M", 8f); S(2f);
        var goalLocal = P;
        GoalSet(isl, P, Dir(th));
        var last = isl;
        list.Add((isl, B, null));
        // 置く：前のポータル A の面より手前（-z）、前の島の右
        var prevA = A0; float prevMaxX = LocalBounds(root, root).max.x;
        for (int i = 0; i < list.Count; i++)
        {
            var (island, b, a) = list[i];
            var lb = LocalBounds(island, island);
            var pa = root.InverseTransformPoint(prevA.position);
            float x = prevMaxX + IslandGapX - lb.min.x;
            if (i == 0) x = Mathf.Max(x, FirstMinX - lb.min.x);
            island.localPosition = new Vector3(x, pa.y + IslandDY, pa.z - IslandGapZ - lb.max.z);
            Link(prevA, b, island, null, true, true);
            prevMaxX = x + lb.max.x;
            if (a) prevA = a;
        }
        log.Append("島 " + list.Count + " ");
        return root.InverseTransformPoint(last.TransformPoint(goalLocal));
    }

    // 島をはじめる：入口のポータル B（出る側、z=0 の面）。道はそこから +z へ
    static Transform BeginIsland(string name)
    {
        isl = new GameObject(name).transform; isl.SetParent(islandsT, false);
        pathT = isl; secT = isl; P = Vector3.zero; th = 90f;
        return BigPortal(isl, "PortalIn", Vector3.zero, 0f);
    }
    // 島のおわり：道のはしにポータル A（入る側）
    static Transform EndIsland() => BigPortal(isl, "PortalOut", P, 180f);

    // 島の中のチェックポイント（島の子に置く）
    static void CPi(string name, float along, bool logsOutside = false)
    {
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1"); if (!tmpl) return;
        var g = isl.Find("Checkpoints"); if (!g) { g = new GameObject("Checkpoints").transform; g.SetParent(isl, false); }
        var cp = Object.Instantiate(tmpl, g, false); cp.name = name;
        cp.transform.localPosition = P + Dir(th) * along; cp.transform.localRotation = Quaternion.LookRotation(Dir(th));
        foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        var trig = cp.transform.Find("Trigger");
        if (trig) trig.localScale = new Vector3(HW * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
        var gm = cp.transform.Find("GlowMushrooms");
        if (gm) foreach (Transform cl in gm)
            cl.localPosition = logsOutside
                ? new Vector3(Mathf.Sign(cl.localPosition.x) * (HW + 1.05f), -0.45f, 1.5f)   // 丸太（はば約2m）が道の外、道の横の面にそって少し下（空中の道なので地面はない）
                : new Vector3(Mathf.Sign(cl.localPosition.x) * (HW + 0.55f), cl.localPosition.y, 1.5f);
        var fc = cp.GetComponentInChildren<FuwaCheckpoint>(true);
        if (fc) { UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc); cps.Add(fc); }
    }

    // ---- 光のカーテン：柱2本の間の光の幕。ふわふわがくぐると大きさが変わる（FuwaSizeRune を幕に）。飛びこえ・横からまわれないよう、当たりは横 TrigW・高さ TrigH ----
    public static float CurtainH = 6.5f, CurtainTrigH = 40f, CurtainTrigW = 20f;
    static void Curtain(string name, int mode, float xc = 0f, float w = -1f, float tx0 = float.NaN, float tx1 = float.NaN)
    {
        if (w < 0f) w = 2f * HW;
        if (float.IsNaN(tx0)) { tx0 = xc - CurtainTrigW / 2f; tx1 = xc + CurtainTrigW / 2f; }
        var col = FuwaSizeTest.RuneColor(mode);
        var c = new GameObject(name).transform; c.SetParent(isl, false); c.localPosition = new Vector3(xc, P.y, P.z); c.gameObject.layer = 2;
        var capM = FuwaStage2WaterTest.ToonMat("Ruins_CurtainCap_" + mode, Color.Lerp(col, Color.white, 0.3f));
        capM.SetColor("_EmissionColor", col * 1.2f); EditorUtility.SetDirty(capM);
        var caps = new List<Renderer>();
        bool onArch = curtainOnArch; curtainOnArch = false;
        if (onArch)
        {   // 2026-11-15 溝の柱のアーチの門の中：細い柱はなし。幕は柱と柱の間いっぱい、光る石は柱頭の上に浮かぶ結晶
            float cxA = HW + 0.8f;
            foreach (float sx in new[] { -1f, 1f })
            {
                var gem = MeshObj(c, "Cap", CrystalClusterMesh("CurtainGem", 21, 0.42f, 0), capM, new Vector3(sx * cxA - xc, curtainArchTop + 0.55f, 0f), 0, false);
                caps.Add(gem.GetComponent<Renderer>());
            }
            w = 2f * cxA - 0.75f;
        }
        else foreach (float sx in new[] { -1f, 1f })
        {
            Box(c, "Post", new Vector3(sx * (w / 2f + 0.3f), CurtainH / 2f - 0.3f, 0f), new Vector3(0.5f, CurtainH + 0.6f, 0.5f), stone, 0);
            var cap = Box(c, "Cap", new Vector3(sx * (w / 2f + 0.3f), CurtainH + 0.45f, 0f), new Vector3(0.62f, 0.3f, 0.62f), capM, 0);
            Object.DestroyImmediate(cap.GetComponent<Collider>()); caps.Add(cap.GetComponent<Renderer>());
        }
        // 幕（下がこく、上へ消える）
        if (!curtainFade) curtainFade = CurtainFadeTex();
        string mp = "Assets/FuwaStage2/Materials/Ruins_Curtain_" + mode + ".mat";
        var vm = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!vm) { vm = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")); AssetDatabase.CreateAsset(vm, mp); }
        if (CurtainPattern && Shader.Find("Fuwa/SizeCurtain"))
        {   // 2026-11-15 てじゃ「カーテンをもっと目立つようにグラデ演出、巨大化は中心から外に広がる円、小型化は逆、通常化はうずまき」
            vm.shader = Shader.Find("Fuwa/SizeCurtain"); vm.enableInstancing = true;
            vm.SetColor("_Color", Color.Lerp(col, Color.white, 0.15f)); vm.SetFloat("_Mode", mode); vm.SetFloat("_Alpha", 0.7f); vm.SetFloat("_Freq", mode == 0 ? 3.5f : 5f); vm.SetFloat("_Speed", 0.6f);
            vm.SetColor("_EmissionColor", col * 1.2f);
        }
        else { vm.mainTexture = curtainFade; vm.SetColor("_TintColor", new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f, 0.45f)); }
        EditorUtility.SetDirty(vm);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Veil"; Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(c, false); q.transform.localPosition = new Vector3(0f, CurtainH / 2f, 0f); q.transform.localScale = new Vector3(w, CurtainH, 1f);
        var qr = q.GetComponent<MeshRenderer>(); qr.sharedMaterial = vm; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; qr.receiveShadows = false;
        // 当たり（FuwaSizeRune と同じ物に）
        var bc = c.gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true;
        bc.size = new Vector3(tx1 - tx0, CurtainTrigH, 0.4f); bc.center = new Vector3((tx0 + tx1) / 2f - xc, CurtainTrigH / 2f - 6f, 0f);
        var ps = new GameObject("Burst").AddComponent<ParticleSystem>(); ps.transform.SetParent(c, false); ps.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        var main = ps.main; main.playOnAwake = false; main.loop = false; main.startLifetime = 0.8f; main.startSpeed = 2.2f; main.startSize = 0.14f; main.startColor = Color.Lerp(col, Color.white, 0.3f); main.maxParticles = 64; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(w, 2.5f, 0.1f);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Rune_Glyph_" + mode + ".mat");
        ps.Stop();
        var sr = UdonSharpUndo.AddComponent<FuwaSizeRune>(c.gameObject);
        if (CurtainPattern) caps.Add(qr);   // くぐった時に幕も光る（_EmissionColor の明るさ）
        sr.mode = mode; sr.glyph = null; sr.glows = caps.ToArray(); sr.burst = ps; sr.glowColor = col;
        UdonSharpEditorUtility.CopyProxyToUdon(sr); FuwaStage2WaterTest.NoSync(sr);
    }
    static Texture2D CurtainFadeTex()
    {
        const string path = "Assets/FuwaStage2/Textures/Curtain_Fade.png";
        var t = new Texture2D(4, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) { float v = y / 63f; float a = Mathf.Clamp01(1f - v) * (0.55f + 0.45f * Mathf.Clamp01(v * 6f)); for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1f, 1f, 1f, a)); }
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Textures")) AssetDatabase.CreateFolder("Assets/FuwaStage2", "Textures");
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path); ti.wrapMode = TextureWrapMode.Clamp; ti.alphaIsTransparency = true; ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // えらぶカーテン：まん中の仕切りの左に青（小）、右に橙（大）
    static void Choice(string name)
    {
        float len = 3.2f;
        Box(isl, name + "_Divider", new Vector3(0f, P.y + 2.5f, P.z + len / 2f), new Vector3(0.4f, 5f, len), stone, 0);
        float z = P.z; P += Vector3.forward * (len / 2f);
        Curtain(name + "_Small", -1, -HW / 2f, HW - 0.1f, -CurtainTrigW / 2f, 0f);
        Curtain(name + "_Big", 1, HW / 2f, HW - 0.1f, 0f, CurtainTrigW / 2f);
        P = new Vector3(P.x, P.y, z);
        S(len);
    }

    // ---- 横風：落とし穴だらけの道（タイル）＋左右の石像から交互の強風（ステージ1の横風を写す）----
    public static float WindStrength = 5.5f, WindOn = 0.8f, WindOff = 2.2f, TileL = 1.2f;
    static readonly int[][] PitRows = { new int[0], new[] { 0, 1 }, new int[0], new[] { 2, 3 }, new int[0], new[] { 1, 2 }, new int[0], new[] { 0 }, new[] { 3 } };
    static int pitRow;
    static int[][] pitTable;   // null なら PitRows（2026-11-14 回廊の奥は BigPitRows）
    // 2026-11-14 てじゃ「広げた穴デカすぎかも」「ちょっとだけ縮めて」→「（穴の長さ）2マスがムリだけど1マスがヌルすぎ」
    //  → 回廊ではタイルの長さを PitTileL（1.7m）にして、穴は1列（=1.7m、前は 1.2m×2列=2.4m）。はばは 3マス と 2マス を交互
    public static float PitTileL = 1.7f;
    static readonly int[][] BigPitRows = { new int[0], new[] { 0, 1, 2 }, new int[0], new[] { 2, 3 }, new int[0], new[] { 1, 2, 3 }, new int[0], new[] { 0, 1 } };
    // 箱（上の左右のふちだけ面取り。left/right で選ぶ）
    static void EdgeChamferBox(List<Vector3> v, List<int> t, Vector3 c3, Vector3 sz, float ch, bool left, bool right)
    {
        float hx = sz.x / 2f, hy = sz.y / 2f, hz = sz.z / 2f;
        var sec = new List<Vector2> { new Vector2(-hx, -hy) };
        if (left) { sec.Add(new Vector2(-hx, hy - ch)); sec.Add(new Vector2(-hx + ch, hy)); } else sec.Add(new Vector2(-hx, hy));
        if (right) { sec.Add(new Vector2(hx - ch, hy)); sec.Add(new Vector2(hx, hy - ch)); } else sec.Add(new Vector2(hx, hy));
        sec.Add(new Vector2(hx, -hy));
        int n = sec.Count;
        Vector3 Pt(Vector2 s2, float z) => c3 + new Vector3(s2.x, s2.y, z);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var mid = (sec[i] + sec[j]) * 0.5f;
            Tri(v, t, Pt(sec[i], -hz), Pt(sec[j], -hz), Pt(sec[j], hz), new Vector3(mid.x, mid.y, 0f)); Tri(v, t, Pt(sec[i], -hz), Pt(sec[j], hz), Pt(sec[i], hz), new Vector3(mid.x, mid.y, 0f));
        }
        for (int i = 1; i + 1 < n; i++) { Tri(v, t, Pt(sec[0], -hz), Pt(sec[i], -hz), Pt(sec[i + 1], -hz), Vector3.back); Tri(v, t, Pt(sec[0], hz), Pt(sec[i], hz), Pt(sec[i + 1], hz), Vector3.forward); }
    }
    static void WindRun(string tag, float len, float[] windsAt)
    {
        float z0 = P.z; int rows = Mathf.Max(1, Mathf.RoundToInt(len / TileL)); float rl = len / rows;
        // 回廊（pitTable あり）はマスの長さをぴったり TileL に（穴の長さを決めたいので）。あまりは最後の穴なしの列に
        float extra = 0f; if (pitTable != null) { rows = Mathf.Max(1, Mathf.FloorToInt(len / TileL)); rl = TileL; extra = len - rows * rl; }
        int cols = 4; float cw = 2f * HW / cols;
        var g = new GameObject("WindRun_" + tag).transform; g.SetParent(isl, false);
        var tA = new List<Vector3>(); var tAt = new List<int>(); var tB = new List<Vector3>(); var tBt = new List<int>();
        // 2026-11-15 面取り：両はしの列のタイルは外がわの上のふちを面取りした見た目（当たりは箱のまま）
        void TileAt(Vector3 c3, Vector3 sz3, bool even, int c)
        {
            var b = Box(g, "Tile", c3, sz3, even ? pathA : pathB, pathLayer);
            if (PathChamfer <= 0f || (c != 0 && c != cols - 1)) return;
            Object.DestroyImmediate(b.GetComponent<MeshRenderer>()); Object.DestroyImmediate(b.GetComponent<MeshFilter>());
            EdgeChamferBox(even ? tA : tB, even ? tAt : tBt, c3, sz3, PathChamfer, c == 0, c == cols - 1);
        }
        if (extra > 0.01f) for (int c = 0; c < cols; c++) TileAt(new Vector3(-HW + cw * (c + 0.5f), P.y - 0.3f, z0 + rows * rl + extra * 0.5f), new Vector3(cw, 0.6f, extra), rows % 2 == 0, c);
        for (int r = 0; r < rows; r++)
        {
            var tbl = pitTable ?? PitRows; var holes = tbl[(pitRow++) % tbl.Length];
            for (int c = 0; c < cols; c++)
            {
                if (holes.Contains(c)) continue;
                TileAt(new Vector3(-HW + cw * (c + 0.5f), P.y - 0.3f, z0 + rl * (r + 0.5f)), new Vector3(cw, 0.6f, rl), r % 2 == 0, c);   // 2026-11-14 タイルの間の 4cm のすき間から空が線に見えたのでなし
            }
        }
        if (tA.Count > 0) MeshObj(g, "TilesEdgeA", SaveMesh("WindRun_" + tag + "_" + runNo + "_EA", tA, tAt), pathA, Vector3.zero, 0, true);
        if (tB.Count > 0) MeshObj(g, "TilesEdgeB", SaveMesh("WindRun_" + tag + "_" + runNo + "_EB", tB, tBt), pathB, Vector3.zero, 0, true);
        foreach (float sx in new[] { -1f, 1f })
        {
            var rail = Box(g, "Rail", new Vector3(sx * (HW + 0.12f), P.y - 0.45f, z0 + len / 2f), new Vector3(0.24f, 0.9f, len), underM, 0); Object.DestroyImmediate(rail.GetComponent<Collider>());
        }
        int k = 0; foreach (float a in windsAt) WindStatues(g, z0 + a, (k++) * 1.5f);
        P += Vector3.forward * len;
    }
    // ---- 崩れた石だたみの飛び石（2026-11-13 てじゃ「曲がり方がなんか不自然」→ 道は曲げない）----
    //  まっすぐで広い石だたみ（StoneCols×1.2m）のタイルが崩れ落ちて、残った所がくねくねの飛び石ルート。石はふつうの四角
    //  ルートのまん中 m(r) が左右にゆれる（sin＋ゆらぎ）。各列 1〜2 マス、前の列と辺で接する。はしの列は道のはば分
    public static int StoneCols = 4, StoneGapEvery = 3, StoneGapRows = 1; public static float StonePeriod = 6f, StoneCell = 2.2f, StoneGapLen = 2.2f;   // 2026-11-13 てじゃ「（切れ目 2.2m が）ちょうどいい、マス目全部これに」→ 1マス 2.2m（はば 4マス 8.8m）   // 切れ目の長さ：1.5m はダッシュで越えられる、3m は届かない（てじゃ）→ 2.2m   // 2026-11-13 てじゃ「ちょっぴり1マスを大きく」1.2 → 1.5（はば 6×1.2 → 5×1.5）   // 切れ目：石 StoneGapEvery 列ごとに StoneGapRows 列まるごと穴
    static void Stones(string tag, float len)
    {
        int cols = StoneCols; float cw = StoneCell, hwF = cols * cw / 2f;
        int rows = Mathf.Max(3, Mathf.RoundToInt(len / StoneCell)); float rl = len / rows;
        int seed = WiggleSeed; foreach (char ch in tag) seed = seed * 31 + ch; var rnd = new System.Random(seed);
        float ph = (float)rnd.NextDouble() * Mathf.PI * 2f, amp = (cols - 1) / 2f;
        var g = new GameObject("Stones_" + tag).transform; g.SetParent(isl, false);
        var stA = new List<Vector3>(); var stAt = new List<int>(); var stB = new List<Vector3>(); var stBt = new List<int>(); var stU = new List<Vector3>(); var stUt = new List<int>();
        var rnd2 = new System.Random(seed + 99);   // 見た目用（石の並びの乱数 rnd は変えない）
        float z0 = P.z, y = P.y, zc = z0; bool[] prev = null; bool lastGap = false; int gapLeft = 0, sinceGap = 0; int full = cols, f0 = 0;   // はしの列は全部（石だたみのはば）
        for (int r = 0; r < rows; r++)
        {
            var solid = new bool[cols];
            if (r == 0 || r == rows - 1) { for (int c = f0; c < f0 + full; c++) solid[c] = true; }
            // 2026-11-13 てじゃ「繋げずに途中切ってもいいよ」→ ときどき1列まるごと穴（1.2m を飛びこす）。続けない・はしの近くはなし
            //  → 「1マスの穴、ダッシュで超えられちゃう」→ 切れ目は StoneGapRows 列ぶん
            else if (gapLeft > 0) { gapLeft--; zc += StoneGapLen; continue; }
            else if (r + StoneGapRows < rows - 1 && sinceGap >= StoneGapEvery + rnd.Next(2)) { lastGap = true; sinceGap = 0; gapLeft = StoneGapRows - 1; zc += StoneGapLen; continue; }   // 運まかせだと切れ目のない区間ができた → 石 StoneGapEvery〜+1 列ごとに必ず
            else
            {
                float m = (cols - 1) / 2f + amp * Mathf.Sin(ph + 2f * Mathf.PI * r / StonePeriod) * 0.9f + ((float)rnd.NextDouble() - 0.5f) * 0.8f;
                int w = rnd.Next(10) < WiggleWide ? 2 : 1;
                int a0 = w == 2 ? Mathf.FloorToInt(m) : Mathf.RoundToInt(m); a0 = Mathf.Clamp(a0, 0, cols - w);
                for (int c = a0; c < a0 + w; c++) solid[c] = true;
                // 前の列と辺で接していなければ、近い方へのばす
                bool ok = false; for (int c = 0; c < cols; c++) if (solid[c] && prev[c]) ok = true;
                if (!ok)
                {
                    int best = -1, bd = 99; for (int c = 0; c < cols; c++) if (prev[c]) { int d = c < a0 ? a0 - c : c - (a0 + w - 1); if (d < bd) { bd = d; best = c; } }
                    for (int c = Mathf.Min(best, a0); c <= Mathf.Max(best, a0 + w - 1); c++) solid[c] = true;
                }
            }
            prev = solid; lastGap = false; sinceGap++;
            for (int c = 0; c < cols; c++)
            {
                if (!solid[c]) continue;
                var sc3 = new Vector3(-hwF + cw * (c + 0.5f), y - 0.3f, zc + rl * 0.5f); var ss3 = new Vector3(cw - 0.04f, 0.6f, rl - 0.04f);
                var sb = Box(g, "Stone", sc3, ss3, r % 2 == 0 ? pathA : pathB, pathLayer);
                if (StoneLook)
                {   // 2026-11-15 見た目クオリティ（2）：ただの箱 → 上のふちを面取りした石の板＋下にゴツゴツの岩（当たりは前の箱のまま）
                    Object.DestroyImmediate(sb.GetComponent<MeshRenderer>()); Object.DestroyImmediate(sb.GetComponent<MeshFilter>());
                    ChamferBoxInto(r % 2 == 0 ? stA : stB, r % 2 == 0 ? stAt : stBt, sc3, ss3, 0.08f);
                    RockChunk(stU, stUt, sc3 + Vector3.down * (0.3f + 0.6f), new Vector3(cw * 0.78f, 1.5f, rl * 0.78f), Quaternion.Euler(0f, (float)rnd2.NextDouble() * 360f, 0f), rnd2);
                }
            }
            zc += rl;
        }
        len = zc - z0;   // 切れ目で長さが変わる
        if (StoneLook && stA.Count > 0)
        {
            MeshObj(g, "StonesA", SaveMesh("Stones_" + tag + "_A", stA, stAt), pathA, Vector3.zero, 0, true);
            if (stB.Count > 0) MeshObj(g, "StonesB", SaveMesh("Stones_" + tag + "_B", stB, stBt), pathB, Vector3.zero, 0, true);
            MeshObj(g, "StonesUnder", SaveMesh("Stones_" + tag + "_U", stU, stUt), underM, Vector3.zero, 0, true);
        }
        // 崩れた石だたみのふち（低い石のへり、見た目だけ）→ 2026-11-14 てじゃ「道にある横の枠みたいなのもいらんかも」でなし（StoneRims）
        if (StoneRims) foreach (float sx in new[] { -1f, 1f })
        {
            var rim = Box(g, "Rim", new Vector3(sx * (hwF + 0.15f), y - 0.45f, z0 + len / 2f), new Vector3(0.3f, 0.9f, len), underM, 0); Object.DestroyImmediate(rim.GetComponent<Collider>());
        }
        log.Append("飛び石" + tag + " ");
        P += Vector3.forward * len;
    }

    // ---- ぐねぐねの穴あきの道（2026-11-13）：くねくね曲がる道をタイルに切って、ところどころ穴。
    //  中心線 x(z) = amp·sin(2π·waves·z/len)·sin(π·z/len)（はしは向きが 0 → 前後のまっすぐの道とつながる）
    //  タイルは中心線にそった曲がった箱（となりのタイルとすき間・重なりなし）。A/B の列ごとに1つのメッシュ＋当たり
    public static float WiggleTile = 1.2f;   // はばは道と同じ HW（4列）
    public static int WiggleCols = 4, WiggleSeed = 25, WiggleWide = 6;   // WiggleWide：10回に何回 2マスはば
    //  中心線は半径 WiggleR の円弧を左右交互につなぐ（turns の角度、合計 0 なら出口は入口と同じ向き）。z は中心線にそった長さ
    public static float WiggleR = 6f;
    static void Wiggle(string tag, float[] turns)
    {
        float y = P.y, hw = HW; int cols = WiggleCols; float cw = 2f * hw / cols;
        // 中心線を細かく取っておく
        const float ds = 0.05f; var cp = new List<Vector3>(); var chd = new List<float>();
        Vector3 pos = P; float hd = 0f; cp.Add(pos); chd.Add(hd);
        foreach (float tdeg in turns)
        {
            float arc = WiggleR * Mathf.Abs(tdeg) * Mathf.Deg2Rad; int n = Mathf.Max(1, Mathf.CeilToInt(arc / ds));
            for (int i = 0; i < n; i++)
            {
                float hm = hd + tdeg * Mathf.Deg2Rad * (i + 0.5f) / n;
                pos += new Vector3(Mathf.Sin(hm), 0f, Mathf.Cos(hm)) * (arc / n); cp.Add(pos);
                hd += tdeg * Mathf.Deg2Rad / n; chd.Add(hd);
            }
        }
        float len = (cp.Count - 1) * 0f; { float L = 0f; for (int i = 1; i < cp.Count; i++) L += (cp[i] - cp[i - 1]).magnitude; len = L; }
        float stepL = len / (cp.Count - 1);
        int rows = Mathf.Max(1, Mathf.RoundToInt(len / WiggleTile)); float rl = len / rows;
        Vector3 C(float z) { float f = Mathf.Clamp(z / stepL, 0f, cp.Count - 1.001f); int i = (int)f; return Vector3.Lerp(cp[i], cp[i + 1], f - i); }
        Vector3 N(float z) { float f = Mathf.Clamp(z / stepL, 0f, cp.Count - 1.001f); int i = (int)f; float a = Mathf.Lerp(chd[i], chd[i + 1], f - i); return new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)); }
        Vector3 Pt(float z, float u) => C(z) + N(z) * u;
        log.Append("ぐねぐね" + tag + " " + len.ToString("F1") + "m ");
        var g = new GameObject("Wiggle_" + tag).transform; g.SetParent(isl, false);
        int seed = WiggleSeed; foreach (char ch in tag) seed = seed * 31 + ch; var rnd = new System.Random(seed);
        var vA = new List<Vector3>(); var tA = new List<int>(); var vB = new List<Vector3>(); var tB = new List<int>();
        bool[] prev = null; const float Tk = 0.6f, gap = 0.02f; const int sub = 3;
        for (int r = 0; r < rows; r++)
        {
            // 穴：はし2列・3列おきに全部ある列。ほかは1つか2つ穴、前の列とつながる所を1つは残す
            var solid = new bool[cols]; for (int c = 0; c < cols; c++) solid[c] = true;
            // 2026-11-13 てじゃ「細かすぎて走ってぬけられるかも」「穴があいてるというより、飛び石状態になってるくらい抜いてもいい」
            //  → 列ごとに 1〜2 マスだけ残す（ほかは穴）。前の列と1マス以上は辺で接して、左右にずれていく。はしの列は全部
            if (r >= 1 && r < rows - 1)
            {
                int pa = 0, pb = cols - 1; for (int c = 0; c < cols; c++) if (prev[c]) { pa = c; break; } for (int c = cols - 1; c >= 0; c--) if (prev[c]) { pb = c; break; }
                int w = rnd.Next(10) < WiggleWide ? 2 : 1, a0 = 0;
                for (int k = 0; k < 20; k++)
                {
                    a0 = rnd.Next(cols - w + 1);
                    bool overlap = a0 <= pb && a0 + w - 1 >= pa, same = a0 == pa && w == pb - pa + 1;
                    if (overlap && !same) break;
                }
                for (int c = 0; c < cols; c++) solid[c] = c >= a0 && c < a0 + w;
            }
            prev = solid;
            var v = r % 2 == 0 ? vA : vB; var t = r % 2 == 0 ? tA : tB;
            float za = rl * r + gap, zb = rl * (r + 1) - gap;
            for (int c = 0; c < cols; c++)
            {
                if (!solid[c]) continue;
                float ua = -hw + cw * c + gap, ub = -hw + cw * (c + 1) - gap;
                // 上の面・下の面・左右の面（sub 分割）、前後のふた
                for (int i = 0; i < sub; i++)
                {
                    float z1 = Mathf.Lerp(za, zb, (float)i / sub), z2 = Mathf.Lerp(za, zb, (float)(i + 1) / sub);
                    Vector3 a = Pt(z1, ua), b = Pt(z1, ub), cc = Pt(z2, ub), d = Pt(z2, ua), dn = Vector3.down * Tk;
                    Quad(v, t, a, b, cc, d, Vector3.up); Quad(v, t, a + dn, d + dn, cc + dn, b + dn, Vector3.down);
                    Quad(v, t, a, d, d + dn, a + dn, -N((z1 + z2) / 2f)); Quad(v, t, b, b + dn, cc + dn, cc, N((z1 + z2) / 2f));
                }
                { Vector3 a = Pt(za, ua), b = Pt(za, ub), dn = Vector3.down * Tk; var f = (Pt(za, 0f) - Pt(za + 0.01f, 0f)).normalized; Quad(v, t, a, a + dn, b + dn, b, f); }
                { Vector3 a = Pt(zb, ua), b = Pt(zb, ub), dn = Vector3.down * Tk; var f = (Pt(zb, 0f) - Pt(zb - 0.01f, 0f)).normalized; Quad(v, t, a, b, b + dn, a + dn, f); }
            }
        }
        foreach (var (nm, v, t, m) in new[] { ("A", vA, tA, pathA), ("B", vB, tB, pathB) })
        {
            if (v.Count == 0) continue;
            var o = new GameObject("Tiles" + nm); o.transform.SetParent(g, false); o.layer = pathLayer;
            var me = SaveMesh("Wiggle_" + tag + "_" + nm + "_" + (segNo++), v, t);
            o.AddComponent<MeshFilter>().sharedMesh = me; o.AddComponent<MeshRenderer>().sharedMaterial = m; o.AddComponent<MeshCollider>().sharedMesh = me;
        }
        P = cp[cp.Count - 1]; P.y = y;
    }
    static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var s = b; b = d; d = s; }
        int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    // 飛び石＋その上の弱い横風（2026-11-14 てじゃ「かなり弱めの横風を吹かせよう」）：横風の見本の風だけ（石像なし）を飛び石の長さいっぱいに
    public static bool ShockSafeVeil = true;   // 2026-11-15 セーフの影のふちに白い幕
    public static float WeakWind = 1.6f, WeakWindOn = 1.4f, WeakWindOff = 1.6f;   // 前の横風は 5.5（大きいと ×0.3、小さいと ×1.6）
    static void StonesWind(string tag, float len, float phase)
    {
        float z0 = P.z; Stones(tag, len); float L = P.z - z0;
        var tmpl = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "Gimmick1_CrossWind" && t.parent && t.parent.name == "BuildTemplates" && t.parent.parent && t.parent.parent.name == "FuwaCourse" && t.gameObject.scene.IsValid());
        if (!tmpl) { log.Append("（横風の見本がない）"); return; }
        var w = Object.Instantiate(tmpl.gameObject, isl, false); w.name = "WeakWind_" + tag; w.SetActive(true);
        w.transform.localPosition = new Vector3(0f, P.y, z0 + L / 2f); w.transform.localRotation = Quaternion.identity;
        foreach (Transform c in w.transform) c.localPosition = new Vector3(c.localPosition.x, 2.6f, c.localPosition.z);
        var cwT = w.transform.Find("CrossWind"); if (cwT) cwT.localScale = new Vector3(StoneCols * StoneCell + 10f, 5.2f, L);
        // 見た目（風の粒・木の葉・地面の光）も長さに合わせてのばす（見本は 5m ぶん）
        float f = L / 5f;
        var gh = w.transform.Find("GroundHighlight"); if (gh) gh.localScale = Vector3.Scale(gh.localScale, new Vector3(1f, 1f, f));
        foreach (var ps in w.GetComponentsInChildren<ParticleSystem>(true))
        {
            var sh = ps.shape; sh.scale = Vector3.Scale(sh.scale, new Vector3(1f, 1f, f));
            var em = ps.emission; em.rateOverTimeMultiplier *= f;
            var mn = ps.main; mn.maxParticles = Mathf.CeilToInt(mn.maxParticles * f);
        }
        BlueWindDecal(w);
        var wa = w.GetComponentInChildren<FuwaWindArea>(true);
        if (wa) { wa.strength = WeakWind; wa.onSeconds = WeakWindOn; wa.offSeconds = WeakWindOff; wa.phaseOffset = phase; wa.alternate = true; UdonSharpEditorUtility.CopyProxyToUdon(wa); FuwaStage2WaterTest.NoSync(wa); }
        log.Append("弱い横風" + tag + " ");
    }

    // 2026-11-15 てじゃ「2-5の風の地面エフェクト青くしてほしい」：見本（1-1の横風）の緑の材質を共有していた → 2-5用の青い材質に（ほかのコースの青と同じ色）
    static readonly Color WindDecalBlue = new Color(0.4f, 0.78f, 1f, 0.3f);
    static void BlueWindDecal(GameObject w)
    {
        const string mp = "Assets/FuwaCourse/Generated/WindDecal/WindDecal_Course2_5_Wind.mat";
        foreach (var r in w.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name != "WindDecal" || !r.sharedMaterial) continue;
            //  色・帯の形は今の 1-1 の横風（G2_CrossLeft）にそろえる（見本の材質は古いまま）。風の向きだけ見本のを使う
            var src = r.sharedMaterial;
            var now = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Generated/WindDecal/WindDecal_G2_CrossLeft_CrossWind.mat");
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!m) { m = new Material(now ? now : src); AssetDatabase.CreateAsset(m, mp); }
            else if (now) m.CopyPropertiesFromMaterial(now);
            m.shader = src.shader;
            m.SetVector("_WindDir", src.GetVector("_WindDir")); m.SetFloat("_FlowDir", src.GetFloat("_FlowDir"));
            if (!now) m.color = WindDecalBlue;
            EditorUtility.SetDirty(m);
            r.sharedMaterial = m;
        }
    }

    static void WindStatues(Transform g, float z, float phase)
    {
        var tmpl = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "Gimmick1_CrossWind" && t.parent && t.parent.name == "BuildTemplates" && t.parent.parent && t.parent.parent.name == "FuwaCourse" && t.gameObject.scene.IsValid());
        if (!tmpl) { log.Append("（横風の見本がない）"); return; }
        var w = Object.Instantiate(tmpl.gameObject, g, false); w.name = "CrossWind"; w.SetActive(true);
        w.transform.localPosition = new Vector3(0f, P.y, z); w.transform.localRotation = Quaternion.identity;
        foreach (Transform c in w.transform) c.localPosition = new Vector3(c.localPosition.x, 2.6f, c.localPosition.z);
        var cwT = w.transform.Find("CrossWind"); if (cwT) cwT.localScale = new Vector3(2f * HW + 10f, 5.2f, 3.6f);
        BlueWindDecal(w);
        var wa = w.GetComponentInChildren<FuwaWindArea>(true);
        if (wa) { wa.strength = WindStrength; wa.onSeconds = WindOn; wa.offSeconds = WindOff; wa.phaseOffset = phase; wa.alternate = true; UdonSharpEditorUtility.CopyProxyToUdon(wa); FuwaStage2WaterTest.NoSync(wa); }
        // 石像（左右）：台＋胴＋頭、道の方を向いた口
        foreach (float sx in new[] { -1f, 1f })
        {
            float x = sx * (HW + 2.4f);
            Box(g, "StatueBase", new Vector3(x, P.y - 0.3f, z), new Vector3(2f, 0.6f, 2f), underM, 0);
            Box(g, "StatueBody", new Vector3(x, P.y + 1.3f, z), new Vector3(1.1f, 2.6f, 1.1f), stone, 0);
            Box(g, "StatueHead", new Vector3(x, P.y + 3.1f, z), new Vector3(1.2f, 1.1f, 1.2f), terra, 0);
            var mouth = Box(g, "StatueMouth", new Vector3(x - sx * 0.6f, P.y + 2.95f, z), new Vector3(0.12f, 0.45f, 0.7f), terraD, 0); Object.DestroyImmediate(mouth.GetComponent<Collider>());
        }
    }

    // ---- 巣のスキマ：左右交互に道をふさぐ巣（破れない、人はすり抜け）。gap＝あいている幅 ----
    public static float WebH = 5f, WebStep = 2.2f;
    static void WebRun(string tag, float len, float gap)
    {
        float z0 = P.z;
        var g = new GameObject("WebRun_" + tag).transform; g.SetParent(isl, false);
        int n = Mathf.Max(1, Mathf.FloorToInt((len - 1f) / WebStep));
        for (int i = 0; i < n; i++)
        {
            float z = z0 + 1.2f + i * WebStep; bool left = i % 2 == 0;
            float x0 = left ? -WebXO : -HW + gap, x1 = left ? HW - gap : WebXO;
            Web(g, "Web" + i, new Vector3((x0 + x1) / 2f, P.y + WebH / 2f, z), Quaternion.identity, new Vector2(x1 - x0, WebH), false, 0.6f);
        }
        S(len);
    }

    // 巣（左右交互）＋落とし穴の床（WindRun のタイル道、風なし）（2026-11-14 回廊）
    static void WebPits(string tag, float len, float gap)
    {
        float z0 = P.z;
        var g = new GameObject("WebRun_" + tag).transform; g.SetParent(isl, false);
        int n = Mathf.Max(1, Mathf.FloorToInt((len - 1f) / WebStep));
        for (int i = 0; i < n; i++)
        {
            float z = z0 + 1.2f + i * WebStep; bool left = i % 2 == 0;
            float x0 = left ? -WebXO : -HW + gap, x1 = left ? HW - gap : WebXO;
            Web(g, "Web" + i, new Vector3((x0 + x1) / 2f, P.y + WebH / 2f, z), Quaternion.identity, new Vector2(x1 - x0, WebH), false, 0.6f);
        }
        WindRun("Pits_" + tag, len, new float[0]);
    }
    // 巣の門（2026-11-14）：トンネルの断面を田の字に4分割、埋まっているマスに1枚ずつ小さい巣。patterns は門ごとのビット（WLL 左下・WLR 右下・WUL 左上・WUR 右上）
    //  len の中に等間隔で門を並べる。pits なら床は落とし穴のタイル
    const int WLL = 1, WLR = 2, WUL = 4, WUR = 8;
    //  2026-11-14 てじゃ「クモの巣にひっかかったときにショットで解除したときに飛び出したふわふわがまたひっかからないような距離に」
    //  → はがれた時（来た側へ webPopSpeed 2.6・上 1.4）の飛ぶ距離を Play で測ったら、ふつうも大きいも中心で約 2.1m（3秒でほぼ止まる）。
    //    大きい玉の半径 0.45 ×2 ＋余裕で、門と門の間は WebGateGap（len は使わず、門の数 × WebGateGap。前後の区間との間も半分ずつで WebGateGap 以上）
    public static float WebGateGap = 3.8f;
    public static bool WebEyeHeight = true; public static float WebMidAboveEye = 1.1f, WebMidMin = 1.5f, WebMidMax = 3.2f;   // 田の字のまん中の線 = 目の高さ + 1.1（1.5〜3.2）
    static readonly List<(Transform run, float z)> gateZ = new List<(Transform, float)>();
    static void WebGates(string tag, float len, int[] patterns, bool pits)
    {
        float z0 = P.z; len = patterns.Length * (WebGateGap + MidWebExtra);
        var g = new GameObject("WebGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, hMid = TunnelH / 2f, xo = WebXO;   // 横は壁に 0.5m めりこませる
        for (int i = 0; i < patterns.Length; i++)
        {
            float z = z0 + len * (i + 0.5f) / patterns.Length; int pt = patterns[i];
            gateZ.Add((isl, z));
            for (int c = 0; c < 4; c++)
            {
                if ((pt & (1 << c)) == 0) continue;
                bool right = (c & 1) != 0, up = (c & 2) != 0;
                float x0 = right ? 0f : -xo, x1 = right ? xo : 0f, y0 = up ? hMid : 0f, y1 = up ? hTop : hMid;
                var sh = ShapeFor(tag, i, c);   // 2026-11-14 てじゃ「序盤の蜘蛛の巣もちょっと種類まばらにしよう」
                if (tag == "B1" && i == 0) sh = (6, 3f);   // てじゃ「最初の巣の形べつなのがいい」→ 六角A
                Web(g, "Web" + i + "_" + (up ? "U" : "L") + (right ? "R" : "L"), new Vector3((x0 + x1) / 2f, P.y + (y0 + y1) / 2f, z), Quaternion.identity, new Vector2(x1 - x0, y1 - y0), false, 0.6f, false, false, sh.c, sh.s);
            }
        }
        // 2026-11-15 てじゃ「クモの巣の上を通るところ、アバターの大きさで難易度変わるのがヤだな」「いっそアバターの高さでクモの巣の位置かえちゃうとか」
        //  → まん中の線を自分の目の高さ＋WebMidAboveEye に（FuwaWebEyeHeight、自分の画面だけ）
        if (WebEyeHeight)
        {
            var we = UdonSharpUndo.AddComponent<FuwaWebEyeHeight>(g.gameObject);
            we.lower = g.Cast<Transform>().Where(x => x.name.Contains("_L")).ToArray();
            we.upper = g.Cast<Transform>().Where(x => x.name.Contains("_U")).ToArray();
            we.baseY = P.y; we.hMid = hMid; we.hTop = hTop; we.midAboveEye = WebMidAboveEye; we.minMid = WebMidMin; we.maxMid = WebMidMax;
            UdonSharpEditorUtility.CopyProxyToUdon(we); FuwaStage2WaterTest.NoSync(we);
        }
        // 床：門ごとの区間（WebGateGap）で作る。2026-11-14 てじゃ「穴の次が下段2個クモの巣はキツい」→ 下2つが埋まった門（上を越える）の区間は穴なし
        if (!pits) { S(len); return; }
        for (int i = 0; i < patterns.Length; i++)
        {
            bool lowBlocked = (patterns[i] & (WLL | WLR)) == (WLL | WLR);
            if (lowBlocked) S(WebGateGap + MidWebExtra);
            else { if (MidWebExtra > 0f) S(MidWebExtra / 2f); WindRun("Pits_" + tag + "_" + i, WebGateGap, new float[0]); if (MidWebExtra > 0f) S(MidWebExtra / 2f); }   // 穴の区間はそのまま、前後に道を足す
        }
    }

    // 天井から垂れた巣：道のはばいっぱい、下 LowWebGap だけあいている（下をくぐる）
    public static float LowWebGap = 1.7f;
    static void LowWeb(string tag)
    {
        var g = new GameObject("LowWeb_" + tag).transform; g.SetParent(isl, false);
        float h = TunnelH - LowWebGap;
        Web(g, "Web", new Vector3(0f, P.y + LowWebGap + h / 2f, P.z + 0.6f), Quaternion.identity, new Vector2(2f * HW + 1f, h), false, 0.6f);
    }

    // ---- 重力：重いゾーン（ステージ1のドームを写す、キノコはなし）----
    public static float GravScale = 0.75f;
    static void GravRun(string tag, float len)
    {
        float z0 = P.z;
        var tmpl = GameObject.Find("FuwaCourse5/Gimmick4_Heavy");
        S(len);
        if (!tmpl) { log.Append("（重いゾーンの見本がない）"); return; }
        int k = 0;
        foreach (float a in new[] { 0.28f, 0.75f })
        {
            var d = Object.Instantiate(tmpl, isl, false); d.name = "Heavy_" + tag + (k++);
            var ink = d.transform.Find("InkCap"); if (ink) Object.DestroyImmediate(ink.gameObject);
            d.transform.localScale = Vector3.one * GravScale;
            d.transform.localPosition = new Vector3(0f, P.y - 1.3f * GravScale, z0 + len * a); d.transform.localRotation = Quaternion.identity;
        }
    }

    // 当たり（トリガー以外）の範囲（space の座標）。of＝root の時は島の下はのぞく
    static Bounds LocalBounds(Transform of, Transform space)
    {
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var cl in of.GetComponentsInChildren<Collider>(true))
        {
            if (cl.isTrigger) continue;
            if (of == root && islandsT && cl.transform.IsChildOf(islandsT)) continue;
            Vector3 cc, ex; var t = cl.transform;
            if (cl is BoxCollider bc) { cc = bc.center; ex = bc.size * 0.5f; }
            else if (cl is MeshCollider mc && mc.sharedMesh) { cc = mc.sharedMesh.bounds.center; ex = mc.sharedMesh.bounds.extents; }
            else continue;
            for (int k = 0; k < 8; k++)
            {
                var c = cc + new Vector3((k & 1) == 0 ? -ex.x : ex.x, (k & 2) == 0 ? -ex.y : ex.y, (k & 4) == 0 ? -ex.z : ex.z);
                var lp = space.InverseTransformPoint(t.TransformPoint(c));
                if (first) { lb = new Bounds(lp, Vector3.zero); first = false; } else lb.Encapsulate(lp);
            }
        }
        return lb;
    }

    // ================= 部品 =================

    struct DoorSpec { public char side; public float off, w, h, y; public bool floor; }
    // side：B＝うしろ（入口）、F＝前（つきあたり）、K＝うしろ（B と同じ壁、ポータル用の名前）、L＝左、R＝右。off は壁にそった中心のずれ（前の壁・うしろの壁は +x、左右の壁は +z）
    //  floor：壁の厚みの所に床を入れる（外へ出る出入口だけ。道が入ってくる入口・ポータル（面の奥に床があるとコピーとちらつく）は入れない）
    static DoorSpec Door(char side, float off, float w, float h, float y = 0f, bool floor = false) => new DoorSpec { side = side == 'K' ? 'B' : side, off = off, w = w, h = h, y = y, floor = floor };
    // ポータルの穴の所の壁のすき間：枠より少しだけ大きく（壁のすき間の面が枠の中にかくれる。同じ面だとちらつく）
    static DoorSpec PortalGap(char side, float off) => Door(side, off, 2f * FuwaPortalTest.HalfW + 0.1f, FuwaPortalTest.HoleH + 0.05f);
    static DoorSpec WindowGap(char side, float off) => Door(side, off, 2f * WinHalf + 0.02f, 2f * WinHalf + 0.02f, WinY - 0.01f);
    // コースの座標のずれ（部屋の向き heading 基準：x＝右、y＝上、z＝前）
    static Vector3 Off(float heading, Vector3 local) => Dir(heading - 90f) * local.x + Vector3.up * local.y + Dir(heading) * local.z;

    // 部屋：c＝床のまん中（コースの座標、床の上の面）、heading＝前の向き。床はシマの道（w × len）、まわりに厚み WT の壁、上に屋根
    //  open＝true：入る側（2026-11-11 てじゃ「ポータル、入る側は囲わなくてもいいかも」）。前の壁（ポータルのある壁）だけ立てて、ほかの壁・屋根はなし
    static Transform Room(string name, Vector3 c, float heading, float w, float len, float h, DoorSpec[] doors, bool open = false)
    {
        var r = new GameObject(name).transform; r.SetParent(secT, false); r.localPosition = c; r.localRotation = Quaternion.LookRotation(Dir(heading));
        Strip(r, "Floor", new Vector3(0f, 0f, -len / 2f), Vector3.forward * len, w / 2f, false);
        var walls = new GameObject("Walls").transform; walls.SetParent(r, false);
        float hx = w / 2f, hz = len / 2f;
        // 前・うしろの壁（x にそって、角をふくむ）
        foreach (char side in open ? new[] { 'F' } : new[] { 'F', 'B' })
        {
            float z = side == 'F' ? hz + WT / 2f : -hz - WT / 2f;
            WallLine(walls, "Wall" + side, -hx - WT, hx + WT, h, doors.Where(d => d.side == side).ToArray(), (a, b, y0, y1) => WallBox(walls, "Wall" + side, new Vector3((a + b) / 2f, (y0 + y1) / 2f, z), new Vector3(b - a, y1 - y0, WT)));
        }
        // 左・右の壁（z にそって、角はのぞく）
        foreach (char side in open ? new char[0] : new[] { 'L', 'R' })
        {
            float x = side == 'L' ? -hx - WT / 2f : hx + WT / 2f;
            WallLine(walls, "Wall" + side, -hz, hz, h, doors.Where(d => d.side == side).ToArray(), (a, b, y0, y1) => WallBox(walls, "Wall" + side, new Vector3(x, (y0 + y1) / 2f, (a + b) / 2f), new Vector3(WT, y1 - y0, b - a)));
        }
        // 屋根
        if (!open) Box(r, "Roof", new Vector3(0f, h + 0.3f, 0f), new Vector3(w + 2f * WT + 0.4f, 0.6f, len + 2f * WT + 0.4f), terra, 0);
        else Box(r, "WallCap", new Vector3(0f, h + 0.2f, hz + WT / 2f), new Vector3(w + 2f * WT + 0.3f, 0.4f, WT + 0.3f), terra, 0);   // 壁のてっぺんのふち
        // 出入口の床（壁の厚みの所）
        foreach (var d in doors)
        {
            if (!d.floor) continue;
            if (d.side == 'L' || d.side == 'R')
            {
                float x = d.side == 'L' ? -hx - WT / 2f : hx + WT / 2f;
                Strip(r, "DoorFloor" + d.side, new Vector3(x, 0f, d.off - d.w / 2f), Vector3.forward * d.w, WT / 2f, false);
            }
            else
            {
                float z = d.side == 'F' ? hz : -hz - WT;
                Strip(r, "DoorFloor" + d.side, new Vector3(d.off, 0f, z), Vector3.forward * WT, d.w / 2f, false);
            }
        }
        return r;
    }

    // 壁の1本：a〜b（壁にそった座標）、高さ -0.6〜h。doors の所をあける（出入口の下 y より下と、上 y+h より上は壁）
    static void WallLine(Transform parent, string name, float a, float b, float h, DoorSpec[] doors, System.Action<float, float, float, float> box)
    {
        float y0 = -0.6f;
        var ds = doors.OrderBy(d => d.off).ToList();
        float cur = a;
        foreach (var d in ds)
        {
            float da = d.off - d.w / 2f, db = d.off + d.w / 2f;
            if (da > cur + 1e-3f) box(cur, da, y0, h);
            if (d.y > 0.01f) box(da, db, y0, d.y);          // 窓の下
            if (d.y + d.h < h - 1e-3f) box(da, db, d.y + d.h, h);   // 出入口の上
            cur = db;
        }
        if (b > cur + 1e-3f) box(cur, b, y0, h);
    }

    static void WallBox(Transform parent, string name, Vector3 lp, Vector3 size)
    {
        if (size.x < 1e-3f || size.y < 1e-3f || size.z < 1e-3f) return;
        Box(parent, name, lp, size, stone, 0);
    }

    // 横にのびる壁のかたまり（門・アーチ用）：x0〜x1、y0〜y1、厚み t（z のまん中 0）
    static void Block(Transform parent, string name, float x0, float x1, float y0, float y1, float t, Material m)
    {
        Box(parent, name, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f), new Vector3(x1 - x0, y1 - y0, t), m, 0);
    }

    // 部屋の下の岩（ブロックアウト：うすい石の台＋先細りの岩）
    static void Under(Transform room, float w, float len, float depth)
    {
        var u = Box(room, "Under", new Vector3(0f, -0.6f - depth / 2f, 0f), new Vector3(w * 0.92f, depth, len * 0.92f), underM, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
        var u2 = Box(room, "Under2", new Vector3(0f, -0.6f - depth - depth * 0.4f, 0f), new Vector3(w * 0.55f, depth * 0.8f, len * 0.55f), underM, 0);
        Object.DestroyImmediate(u2.GetComponent<Collider>());
    }

    // 人もふわふわも通れるポータル（試作と同じ枠・穴）
    static Transform BigPortal(Transform room, string name, Vector3 lp, float yaw)
    {
        var p = FuwaPortalTest.Portal(room, name, lp, yaw, stone, terraD, glow, maskM, ++stencilNo);
        if (OvalPortals) OvalStyle(p);
        return p;
    }

    // ===== だ円の輪のポータル（2026-11-13 てじゃ「ポータルの見た目、空中に浮いた縦長の◯枠がいい」）=====
    //  柱と屋根の枠をやめて、道の上に少し浮いた縦長のだ円の輪（外に石の輪・内に光るふち）。穴（ステンシルの面）もだ円
    //  人・ふわふわを運ぶ判定（FuwaPortal の halfW × height の四角）はそのまま（だ円の外の角を通ると運ばれるが、ブロックアウトなのでよしとする）
    public static bool OvalPortals = true, PortalBlocks = true, PortalCradle = true, PortalOuterBand = true, PortalJointGlow = true; public static float PortalBlockBevel = 0.045f;
    // 6面体（q：0〜3 が前の面、4〜7 が後ろの面、同じ並び）を面取りして入れる。面ごとに角を2本の辺にそって b だけ内へ → 面・辺の帯・角の三角
    static void ChamferHexInto(List<Vector3> v, List<int> t, Vector3[] q, float b, Vector3 cen)
    {
        int[][] F = { new[] { 0, 1, 2, 3 }, new[] { 7, 6, 5, 4 }, new[] { 0, 4, 5, 1 }, new[] { 3, 2, 6, 7 }, new[] { 0, 3, 7, 4 }, new[] { 1, 5, 6, 2 } };
        var ins = new Dictionary<(int f, int c), Vector3>();
        for (int f = 0; f < 6; f++)
            for (int k = 0; k < 4; k++)
            {
                int c = F[f][k], pv = F[f][(k + 3) % 4], nx = F[f][(k + 1) % 4];
                ins[(f, c)] = q[c] + (q[pv] - q[c]).normalized * b + (q[nx] - q[c]).normalized * b;
            }
        // 面
        for (int f = 0; f < 6; f++)
        {
            var a = ins[(f, F[f][0])]; var b1 = ins[(f, F[f][1])]; var c = ins[(f, F[f][2])]; var d = ins[(f, F[f][3])];
            var w = (a + c) * 0.5f - cen; Tri(v, t, a, b1, c, w); Tri(v, t, a, c, d, w);
        }
        // 辺の帯（2つの面が共有する辺）
        for (int f = 0; f < 6; f++)
            for (int g2 = f + 1; g2 < 6; g2++)
            {
                var sh = F[f].Intersect(F[g2]).ToArray(); if (sh.Length != 2) continue;
                var a = ins[(f, sh[0])]; var b1 = ins[(f, sh[1])]; var c = ins[(g2, sh[1])]; var d = ins[(g2, sh[0])];
                var w = (a + b1 + c + d) * 0.25f - cen; Tri(v, t, a, b1, c, w); Tri(v, t, a, c, d, w);
            }
        // 角の三角
        for (int c = 0; c < 8; c++)
        {
            var fs = Enumerable.Range(0, 6).Where(f => F[f].Contains(c)).ToArray();
            var a = ins[(fs[0], c)]; var b1 = ins[(fs[1], c)]; var d = ins[(fs[2], c)];
            Tri(v, t, a, b1, d, (a + b1 + d) / 3f - cen);
        }
    }
    public static float OvalLift = 0.15f;   // 輪の下のはしの高さ
    static float OvalA => FuwaPortalTest.HalfW;
    static float OvalB => FuwaPortalTest.HoleH / 2f;
    static Vector3 OvalC => new Vector3(0f, (PortalFloating ? PortalFloatLift : OvalLift) + OvalB, 0f);
    // 2026-11-15 てじゃ「ポータル、石の枠なくして ポータル自体が浮いてる感じのほうが映えるかも 現状だとワープしてる感うすい」
    //  → 石の枠・台座・踏み石なし。穴が道から浮いて、ふちに光の帯（Fuwa/PortalRim）、ふちから中へ吸いこまれる光の粒、下の地面に光のたまり。穴の中はふちほどうず（Fuwa/PortalCam）
    public static bool PortalFloating = true;
    public static float PortalFloatLift = 0.35f, PortalRimW = 0.5f;
    public static Color PortalRimCol = new Color(0.45f, 0.85f, 1f, 1f);
    static void FloatingStyle(Transform p)
    {
        foreach (var nm in new[] { "PortalFrame", "OvalFrame", "FloatFrame" }) { var fr = p.Find(nm); if (fr) Object.DestroyImmediate(fr.gameObject); }
        var ring = new GameObject("FloatFrame").transform; ring.SetParent(p, false); ring.localPosition = OvalC;
        float A = OvalA + 0.06f, B = OvalB + 0.06f;
        // 穴
        var hole = p.Find("PortalHole");
        if (hole)
        {
            hole.localPosition = OvalC; hole.localRotation = Quaternion.Euler(0f, 180f, 0f); hole.localScale = Vector3.one;
            hole.GetComponent<MeshFilter>().sharedMesh = EllipseDisc(A, B, 64, "Portal_OvalHole");
            var hm = hole.GetComponent<MeshRenderer>().sharedMaterial;
            if (hm && hm.HasProperty("_EA")) { hm.SetFloat("_EA", A); hm.SetFloat("_EB", B); hm.SetColor("_EdgeGlow", PortalRimCol); EditorUtility.SetDirty(hm); }
        }
        // ふちの光の帯（穴のふちの少し内がわ〜外へ PortalRimW）
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int N = 128; float rin = -0.03f, rout = PortalRimW;
            for (int k = 0; k <= N; k++)
            {
                float a = 2f * Mathf.PI * k / N;
                var nOut = new Vector2(Mathf.Cos(a) / A, Mathf.Sin(a) / B).normalized;
                var e = new Vector2(Mathf.Cos(a) * A, Mathf.Sin(a) * B);
                var pi = e + nOut * rin; var po = e + nOut * rout;
                v.Add(new Vector3(pi.x, pi.y, 0f)); v.Add(new Vector3(po.x, po.y, 0f));
                uv.Add(new Vector2((float)k / N, 0f)); uv.Add(new Vector2((float)k / N, 1f));
            }
            for (int k = 0; k < N; k++) { int a = k * 2; t.Add(a); t.Add(a + 1); t.Add(a + 3); t.Add(a); t.Add(a + 3); t.Add(a + 2); }
            var mesh = SaveMeshUV("Portal_FloatRim", v, t, uv, v.Select(_ => Vector3.back).ToList());
            var rm = FxMat("Ruins_PortalFloatRim", "Fuwa/PortalRim"); rm.SetColor("_Color", PortalRimCol); rm.SetFloat("_Mode", 0f); rm.SetFloat("_Strength", 1.3f); EditorUtility.SetDirty(rm);
            var ro = MeshObj(ring, "Rim", mesh, rm, Vector3.zero, 0, false);
            var rr = ro.GetComponent<Renderer>(); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
        }
        // 下の地面の光のたまり
        {
            float yG = -OvalC.y + 0.03f, w = A * 2f + 1.4f, d = 2.2f;
            var v = new List<Vector3> { new Vector3(-w / 2f, yG, -d / 2f), new Vector3(w / 2f, yG, -d / 2f), new Vector3(w / 2f, yG, d / 2f), new Vector3(-w / 2f, yG, d / 2f) };
            var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var t = new List<int> { 0, 2, 1, 0, 3, 2 };
            var mesh = SaveMeshUV("Portal_FloatPool", v, t, uv, v.Select(_ => Vector3.up).ToList());
            var pm = FxMat("Ruins_PortalPool", "Fuwa/PortalRim"); pm.SetColor("_Color", PortalRimCol); pm.SetFloat("_Mode", 1f); pm.SetFloat("_Strength", 0.8f); EditorUtility.SetDirty(pm);
            var po = MeshObj(ring, "GroundGlow", mesh, pm, Vector3.zero, 0, false);
            var pr = po.GetComponent<Renderer>(); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        }
        // ふちから中へ吸いこまれる光の粒
        {
            var go = new GameObject("Sparkles"); go.transform.SetParent(ring, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(-0.6f, -0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.95f, 1f, 1f), new Color(0.45f, 0.85f, 1f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 60; main.playOnAwake = true; main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission; em.rateOverTime = 26f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1f; sh.radiusThickness = 0f; sh.arc = 360f;
            sh.scale = new Vector3(A + 0.08f, B + 0.08f, 1f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mp = "Assets/FuwaStage2/Materials/Ruins_PortalSpark.mat";
            var sm = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!sm) { sm = new Material(Shader.Find("Legacy Shaders/Particles/Additive")); AssetDatabase.CreateAsset(sm, mp); }
            sm.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"); sm.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); EditorUtility.SetDirty(sm);
            r.sharedMaterial = sm; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
    }
    static void OvalStyle(Transform p)
    {
        if (PortalFloating) { FloatingStyle(p); return; }
        var fr = p.Find("PortalFrame"); if (fr) Object.DestroyImmediate(fr.gameObject);
        var ring = new GameObject("OvalFrame").transform; ring.SetParent(p, false); ring.localPosition = OvalC;
        var o = new GameObject("StoneRing"); o.transform.SetParent(ring, false);
        if (PortalBlocks)
        {   // 2026-11-15 見た目クオリティ（4）：つるっとした石の輪 → 石のブロックを組んだだ円の枠（継ぎ目あり、てっぺんに要石と光る石、下に2つの台）
            var bv = new List<Vector3>(); var bt = new List<int>(); var kv = new List<Vector3>(); var kt = new List<int>();
            int n = 16; float gapA = 0.012f, ain = OvalA + 0.08f, bin = OvalB + 0.08f, w0 = 0.42f, dz = 0.26f;
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.PI / 2f + 2f * Mathf.PI * (i - 0.5f) / n + gapA, a1 = Mathf.PI / 2f + 2f * Mathf.PI * (i + 0.5f) / n - gapA;
                bool key = i == 0;
                if (PortalCradle && (i == n / 2 - 1 || i == n / 2 || i == n / 2 + 1)) continue;   // 下の3つは台座（PortalCradle）にする
                float wo = key ? w0 + 0.22f : (PortalJointGlow ? w0 + 0.02f : w0 + ((i % 2 == 0) ? 0.04f : -0.02f));   // 継ぎ目の光をそろえるので石の出っぱりもそろえる
                float dd = key ? dz + 0.06f : dz;
                Vector3 E(float a, float ex, float z) => new Vector3(Mathf.Cos(a) * (ain + ex), Mathf.Sin(a) * (bin + ex), z);
                // 2026-11-15 てじゃ「枠自体ももうちょいいじろ」→ 石ごとに少し前後にずらす（手で積んだ感じ）＋面取り（継ぎ目が V の溝）
                float zs = PortalBlockBevel > 0f ? ((i * 37 % 7) - 3) * 0.008f : 0f;
                var q = new[] { E(a0, 0f, -dd + zs), E(a1, 0f, -dd + zs), E(a1, wo, -dd + zs), E(a0, wo, -dd + zs), E(a0, 0f, dd + zs), E(a1, 0f, dd + zs), E(a1, wo, dd + zs), E(a0, wo, dd + zs) };
                var cen = E((a0 + a1) / 2f, wo / 2f, zs);
                var tv = key ? kv : bv; var tt = key ? kt : bt;
                if (PortalBlockBevel > 0f) ChamferHexInto(tv, tt, q, PortalBlockBevel, cen);
                else
                {
                    int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 3, 2, 6, 7 }, new[] { 0, 3, 7, 4 }, new[] { 1, 2, 6, 5 } };
                    foreach (var fc in faces) { var fcen = (q[fc[0]] + q[fc[1]] + q[fc[2]] + q[fc[3]]) * 0.25f; Tri(tv, tt, q[fc[0]], q[fc[1]], q[fc[2]], fcen - cen); Tri(tv, tt, q[fc[0]], q[fc[2]], q[fc[3]], fcen - cen); }
                }
            }
            // 外まわりの細いふち（テラコッタ、石より少しおくに引っこんだ帯＝枠が2段に見える）。下の台座の所はなし
            if (PortalOuterBand)
            {
                var ov = new List<Vector3>(); var ot = new List<int>();
                float r0 = w0 - 0.06f, r1 = w0 + 0.13f, zb = dz * 0.55f, chb = 0.03f;
                int nb = 64; float skip0 = Mathf.PI * 1.5f - Mathf.PI * 2f * 1.5f / n, skip1 = Mathf.PI * 1.5f + Mathf.PI * 2f * 1.5f / n;
                Vector3 Eb(float a, float ex, float z) => new Vector3(Mathf.Cos(a) * (ain + ex), Mathf.Sin(a) * (bin + ex), z);
                var bc0 = Vector3.zero;
                for (int k = 0; k < nb; k++)
                {
                    float a0 = 2f * Mathf.PI * k / nb, a1 = 2f * Mathf.PI * (k + 1) / nb, am = (a0 + a1) / 2f;
                    float amw = am < Mathf.PI * 0.5f ? am + Mathf.PI * 2f : am;
                    if (PortalCradle && amw > skip0 && amw < skip1) continue;
                    var nOut = new Vector3(Mathf.Cos(am) / ain, Mathf.Sin(am) / bin, 0f).normalized;
                    // 前の面（面取りで外へ）・外の面・後ろの面
                    Vector3 F0a = Eb(a0, r0, -zb), F0b = Eb(a1, r0, -zb), F1a = Eb(a0, r1 - chb, -zb), F1b = Eb(a1, r1 - chb, -zb);
                    Vector3 Oa = Eb(a0, r1, -zb + chb), Ob = Eb(a1, r1, -zb + chb), Oa2 = Eb(a0, r1, zb - chb), Ob2 = Eb(a1, r1, zb - chb);
                    Vector3 B1a = Eb(a0, r1 - chb, zb), B1b = Eb(a1, r1 - chb, zb), B0a = Eb(a0, r0, zb), B0b = Eb(a1, r0, zb);
                    Tri(ov, ot, F0a, F0b, F1b, Vector3.back); Tri(ov, ot, F0a, F1b, F1a, Vector3.back);
                    Tri(ov, ot, F1a, F1b, Ob, nOut + Vector3.back); Tri(ov, ot, F1a, Ob, Oa, nOut + Vector3.back);
                    Tri(ov, ot, Oa, Ob, Ob2, nOut); Tri(ov, ot, Oa, Ob2, Oa2, nOut);
                    Tri(ov, ot, Oa2, Ob2, B1b, nOut + Vector3.forward); Tri(ov, ot, Oa2, B1b, B1a, nOut + Vector3.forward);
                    Tri(ov, ot, B1a, B1b, B0b, Vector3.forward); Tri(ov, ot, B1a, B0b, B0a, Vector3.forward);
                }
                if (PortalCradle)   // 台座の所で切った両はしのふた
                    foreach (var (ae, sg) in new[] { (skip0, 1f), (skip1 - Mathf.PI * 2f, -1f) })
                    {
                        var sec = new[] { Eb(ae, r0, -zb), Eb(ae, r1 - chb, -zb), Eb(ae, r1, -zb + chb), Eb(ae, r1, zb - chb), Eb(ae, r1 - chb, zb), Eb(ae, r0, zb) };
                        var sc2 = sec.Aggregate(Vector3.zero, (x, y) => x + y) / sec.Length;
                        var tg = new Vector3(-Mathf.Sin(ae) * ain, Mathf.Cos(ae) * bin, 0f).normalized * sg;   // 切った側（台座の方）
                        for (int k = 0; k < sec.Length; k++) Tri(ov, ot, sc2, sec[k], sec[(k + 1) % sec.Length], tg);
                    }
                MeshObj(ring, "OuterBand", SaveMesh("Portal_OvalBand", ov, ot), terraD, Vector3.zero, 0, true);
            }
            // 2026-11-15 てじゃ「ちらっとみえる水色のはなんだろ」→ B「わざと見せる、綺麗にととのえよう」
            //  石の継ぎ目ごとに、光る細い板（継ぎ目の溝の奥、石のはばいっぱい）＝継ぎ目から光がもれている線
            if (PortalJointGlow)
            {
                var jv = new List<Vector3>(); var jt = new List<int>();
                float hw2 = 0.011f, zf = dz - 0.035f, r0j = -0.02f, r1j = w0 - 0.03f;
                Vector3 Ej(float a, float ex, float z) => new Vector3(Mathf.Cos(a) * (ain + ex), Mathf.Sin(a) * (bin + ex), z);
                for (int i = 0; i < n; i++)
                {
                    // i と i+1 の間（台座の中の継ぎ目はなし。台座のはしの2つはあり）
                    int j = (i + 1) % n;
                    bool inCr(int k) => PortalCradle && (k == n / 2 - 1 || k == n / 2 || k == n / 2 + 1);
                    if (inCr(i) && inCr(j)) continue;
                    float aj = Mathf.PI / 2f + 2f * Mathf.PI * (i + 0.5f) / n;
                    var tg = new Vector3(-Mathf.Sin(aj) * ain, Mathf.Cos(aj) * bin, 0f).normalized * hw2;
                    var c0 = Ej(aj, r0j, 0f); var c1 = Ej(aj, r1j, 0f);
                    var q8 = new[] { c0 - tg + Vector3.back * zf, c0 + tg + Vector3.back * zf, c1 + tg + Vector3.back * zf, c1 - tg + Vector3.back * zf,
                                     c0 - tg + Vector3.forward * zf, c0 + tg + Vector3.forward * zf, c1 + tg + Vector3.forward * zf, c1 - tg + Vector3.forward * zf };
                    var cc = (c0 + c1) * 0.5f;
                    int[][] fcs = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 3, 2, 6, 7 }, new[] { 0, 3, 7, 4 }, new[] { 1, 2, 6, 5 } };
                    foreach (var fc in fcs) { var fcen = (q8[fc[0]] + q8[fc[1]] + q8[fc[2]] + q8[fc[3]]) * 0.25f; Tri(jv, jt, q8[fc[0]], q8[fc[1]], q8[fc[2]], fcen - cc); Tri(jv, jt, q8[fc[0]], q8[fc[2]], q8[fc[3]], fcen - cc); }
                }
                MeshObj(ring, "JointGlow", SaveMesh("Portal_OvalJointGlow", jv, jt), glow, Vector3.zero, 0, false);
            }
            o.AddComponent<MeshFilter>().sharedMesh = SaveMesh("Portal_OvalBlocks", bv, bt);
            o.AddComponent<MeshRenderer>().sharedMaterial = stone;
            MeshObj(ring, "Keystone", SaveMesh("Portal_OvalKey", kv, kt), terra, Vector3.zero, 0, true);
            foreach (float sz in new[] { -1f, 1f })
            {
                var gem = MeshObj(ring, "KeyGem", CrystalClusterMesh("PortalGem", 41, 0.2f, 0), CrystalMat(), new Vector3(0f, bin + 0.33f, sz * 0.33f), 0, false);
                gem.transform.localRotation = Quaternion.Euler(90f * sz, 0f, 0f);
            }
            // 2026-11-15 てじゃ「ポータル枠の下の接続部分がちょっと雑」→ 下の3つのブロック＋2つの台（ななめのブロックに箱がささっていた）をやめて、
            //  1つの台座：上の面が輪の内がわのカーブ（だ円）、横はまっすぐ下へ、下に一段広い踏み石
            if (PortalCradle)
            {
                float yBot = -OvalC.y, stepH = 0.05f, wc = w0 + 0.02f;   // 輪の下のはしは道から 0.07 しかないので、踏み石はうすい板
                float ta = Mathf.PI / 2f + 2f * Mathf.PI * (n / 2 - 1.5f) / n + gapA, tb = Mathf.PI / 2f + 2f * Mathf.PI * (n / 2 + 1.5f) / n - gapA;
                Vector2 In(float a) => new Vector2(Mathf.Cos(a) * ain, Mathf.Sin(a) * bin);
                Vector2 Out(float a) => new Vector2(Mathf.Cos(a) * (ain + wc), Mathf.Sin(a) * (bin + wc));
                var poly = new List<Vector2>();
                var oa = Out(ta); var ob = Out(tb);
                float xl = Mathf.Min(oa.x, ob.x), xr = Mathf.Max(oa.x, ob.x), yb = yBot + stepH;
                poly.Add(new Vector2(xl, yb)); poly.Add(oa);
                int m = 14; for (int k = 0; k <= m; k++) { var pi2 = In(Mathf.Lerp(ta, tb, (float)k / m)); pi2.y = Mathf.Max(pi2.y, yb + 0.015f); poly.Add(pi2); }
                poly.Add(ob); poly.Add(new Vector2(xr, yb));
                var cv = new List<Vector3>(); var ct = new List<int>();
                var fanC = new Vector2(0f, yb);
                float cz = dz + 0.02f;
                for (int k = 0; k < poly.Count; k++)
                {
                    var a2v = poly[k]; var b2v = poly[(k + 1) % poly.Count];
                    // 前後の面（下のまん中から扇）
                    if (k < poly.Count - 1)
                    {
                        Tri(cv, ct, new Vector3(fanC.x, fanC.y, -cz), new Vector3(a2v.x, a2v.y, -cz), new Vector3(b2v.x, b2v.y, -cz), Vector3.back);
                        Tri(cv, ct, new Vector3(fanC.x, fanC.y, cz), new Vector3(a2v.x, a2v.y, cz), new Vector3(b2v.x, b2v.y, cz), Vector3.forward);
                    }
                    // まわりの面
                    // 外向き：0＝左の横、1＝左のはし（となりのブロックへ）、2〜m+1＝上のカーブ（穴の中心へ）、m+2＝右のはし、m+3＝右の横、最後＝底
                    var mid2 = (a2v + b2v) * 0.5f; Vector3 w;
                    if (k == 0) w = Vector3.left;
                    else if (k == 1) w = new Vector3(Mathf.Sin(ta) * ain, -Mathf.Cos(ta) * bin, 0f);
                    else if (k <= m + 1) w = new Vector3(-mid2.x, -mid2.y, 0f);
                    else if (k == m + 2) w = new Vector3(-Mathf.Sin(tb) * ain, Mathf.Cos(tb) * bin, 0f);
                    else if (k == m + 3) w = Vector3.right;
                    else w = Vector3.down;
                    Tri(cv, ct, new Vector3(a2v.x, a2v.y, -cz), new Vector3(b2v.x, b2v.y, -cz), new Vector3(b2v.x, b2v.y, cz), w);
                    Tri(cv, ct, new Vector3(a2v.x, a2v.y, -cz), new Vector3(b2v.x, b2v.y, cz), new Vector3(a2v.x, a2v.y, cz), w);
                }
                MeshObj(ring, "Cradle", SaveMesh("Portal_OvalCradle", cv, ct), stone, Vector3.zero, 0, true);
                // 踏み石（道の上、台座より少し広い）
                // 2026-11-15 てじゃ「下の台もただの四角い板なのやだなあ」→ だ円の踏み石（上のふちを面取り）、3つの石に分けて継ぎ目の溝
                {
                    float ex = (xr - xl) / 2f + 0.32f, ez = cz + 0.22f, ch = 0.035f, yT = yBot + stepH, y0s = yBot - 0.03f;
                    var sv = new List<Vector3>(); var st = new List<int>();
                    int N = 48; float seamA = 0.035f;   // 継ぎ目（角度のすき間、ラジアン）
                    // 石の分け目：左右のはし（x = ±ex*0.42 の所）で3つ。角度で分ける
                    float sA = Mathf.Acos(0.42f);
                    float[][] parts = { new[] { -sA, sA }, new[] { sA, Mathf.PI - sA }, new[] { Mathf.PI - sA, Mathf.PI + sA }, new[] { Mathf.PI + sA, 2f * Mathf.PI - sA } };
                    // 前後の長い石（0・2）と左右の石（1・3）… x 方向に長いだ円なので、左右のはし（1・3 ではなく 0 と 2）が端の石
                    foreach (var pr in parts)
                    {
                        float a0 = pr[0] + seamA * 0.5f, a1 = pr[1] - seamA * 0.5f;
                        int nn = Mathf.Max(3, Mathf.CeilToInt((a1 - a0) / (2f * Mathf.PI) * N));
                        Vector3 Pt(float a, float sc, float y) => new Vector3(Mathf.Cos(a) * ex * sc, y, Mathf.Sin(a) * ez * sc);
                        float inS = 1f - ch / Mathf.Min(ex, ez);
                        var cTop = new Vector3(0f, yT, 0f);
                        for (int k = 0; k < nn; k++)
                        {
                            float b0 = Mathf.Lerp(a0, a1, (float)k / nn), b1 = Mathf.Lerp(a0, a1, (float)(k + 1) / nn);
                            var t0 = Pt(b0, inS, yT); var t1 = Pt(b1, inS, yT);
                            var m0 = Pt(b0, 1f, yT - ch); var m1 = Pt(b1, 1f, yT - ch);
                            var d0 = Pt(b0, 1f, y0s); var d1 = Pt(b1, 1f, y0s);
                            var ow = new Vector3(Mathf.Cos((b0 + b1) / 2f), 0f, Mathf.Sin((b0 + b1) / 2f));
                            Tri(sv, st, cTop, t0, t1, Vector3.up);
                            Tri(sv, st, t0, t1, m1, ow + Vector3.up); Tri(sv, st, t0, m1, m0, ow + Vector3.up);
                            Tri(sv, st, m0, m1, d1, ow); Tri(sv, st, m0, d1, d0, ow);
                        }
                        // 継ぎ目の切り口（まん中から外へ、石の横の面）
                        foreach (var (ae, sg) in new[] { (a0, -1f), (a1, 1f) })
                        {
                            var tg = new Vector3(-Mathf.Sin(ae), 0f, Mathf.Cos(ae)) * sg;
                            var pT = Pt(ae, inS, yT); var pM = Pt(ae, 1f, yT - ch); var pD = Pt(ae, 1f, y0s);
                            var c0 = new Vector3(0f, y0s, 0f);
                            Tri(sv, st, cTop, pT, pM, tg); Tri(sv, st, cTop, pM, pD, tg); Tri(sv, st, cTop, pD, c0, tg);
                        }
                    }
                    MeshObj(ring, "Step", SaveMesh("Portal_OvalStep", sv, st), terra, Vector3.zero, 0, true);
                }
            }
            // 下の2つの台（道の上から輪の下まで）
            if (!PortalCradle) foreach (float sx in new[] { -1f, 1f })
            {
                float ang = Mathf.PI * 1.5f + sx * 0.42f;
                var bp = new Vector3(Mathf.Cos(ang) * (ain + w0 * 0.5f), Mathf.Sin(ang) * (bin + w0 * 0.5f), 0f);
                float yBot = -OvalC.y, hgt = bp.y - yBot + 0.15f;
                var ft = Box(ring, "Foot", new Vector3(bp.x, yBot + hgt / 2f, 0f), new Vector3(0.55f, hgt, 0.7f), terra, 0); Object.DestroyImmediate(ft.GetComponent<Collider>());
            }
        }
        else
        {
            o.AddComponent<MeshFilter>().sharedMesh = EllipseTube(OvalA + 0.24f, OvalB + 0.24f, 0.17f, 64, 10, "Portal_OvalStone");
            o.AddComponent<MeshRenderer>().sharedMaterial = stone;
        }
        var g = new GameObject("GlowRim"); g.transform.SetParent(ring, false);
        g.AddComponent<MeshFilter>().sharedMesh = EllipseTube(OvalA + 0.04f, OvalB + 0.04f, 0.06f, 64, 8, "Portal_OvalGlow");
        g.AddComponent<MeshRenderer>().sharedMaterial = glow;
        // 穴：だ円の面（Quad と同じく -z が表 → 180°回して手前へ）
        var hole = p.Find("PortalHole");
        if (hole)
        {
            hole.localPosition = OvalC; hole.localRotation = Quaternion.Euler(0f, 180f, 0f); hole.localScale = Vector3.one;
            hole.GetComponent<MeshFilter>().sharedMesh = EllipseDisc(OvalA + 0.06f, OvalB + 0.06f, 64, "Portal_OvalHole");
        }
    }
    // だ円の面（xy、中心 0、表は -z）
    static Mesh EllipseDisc(float a, float b, int n, string name)
    {
        var v = new List<Vector3> { Vector3.zero }; var t = new List<int>();
        for (int i = 0; i < n; i++) { float ang = 2f * Mathf.PI * i / n; v.Add(new Vector3(Mathf.Cos(ang) * a, Mathf.Sin(ang) * b, 0f)); }
        for (int i = 0; i < n; i++) { int i1 = 1 + i, i2 = 1 + (i + 1) % n; t.Add(0); t.Add(i2); t.Add(i1); }   // -z 向き
        var m = SaveMesh(name, v, t); return m;
    }
    // だ円にそった管（xy の だ円、半径 r の管）
    static Mesh EllipseTube(float a, float b, float r, int n, int k, string name)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float ang = 2f * Mathf.PI * i / n; var c = new Vector3(Mathf.Cos(ang) * a, Mathf.Sin(ang) * b, 0f);
            var tg = new Vector3(-Mathf.Sin(ang) * a, Mathf.Cos(ang) * b, 0f).normalized; var nOut = new Vector3(tg.y, -tg.x, 0f);
            for (int j = 0; j <= k; j++) { float ph = 2f * Mathf.PI * j / k; v.Add(c + (nOut * Mathf.Cos(ph) + Vector3.forward * Mathf.Sin(ph)) * r); }
        }
        int row = k + 1;
        for (int i = 0; i < n; i++) for (int j = 0; j < k; j++)
        {
            int a0 = i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
            t.Add(a0); t.Add(a1); t.Add(b0); t.Add(a1); t.Add(b1); t.Add(b0);
        }
        return SaveMesh(name, v, t);
    }

    // 小さい窓のポータル：枠（4本の石）＋穴。lp は窓の下のまん中。PortalHole の大きさだけ変える
    static Transform WindowPortal(Transform room, string name, Vector3 lp, float yaw)
    {
        int st = ++stencilNo;
        var p = new GameObject(name).transform; p.SetParent(room, false); p.localPosition = lp; p.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var fr = new GameObject("PortalFrame").transform; fr.SetParent(p, false);
        float d = WT + 0.2f, f = 0.12f, s = 2f * WinHalf;
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(-WinHalf - f / 2f, WinHalf, 0f), new Vector3(f, s + 2f * f, d), glow);
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(WinHalf + f / 2f, WinHalf, 0f), new Vector3(f, s + 2f * f, d), glow);
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(0f, -f / 2f, 0f), new Vector3(s, f, d), glow);
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(0f, s + f / 2f, 0f), new Vector3(s, f, d), glow);
        var m = GameObject.CreatePrimitive(PrimitiveType.Quad); m.name = "PortalHole"; Object.DestroyImmediate(m.GetComponent<Collider>());
        m.transform.SetParent(p, false); m.transform.localPosition = new Vector3(0f, WinHalf, 0f); m.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        m.transform.localScale = new Vector3(s, s, 1f);
        var mr = m.GetComponent<MeshRenderer>(); var mm = new Material(maskM) { name = "Ruins_PortalMask_" + st }; mm.SetFloat("_StencilRef", st);
        string mp = "Assets/FuwaStage2/Materials/Ruins_PortalMask_" + st + ".mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(mp); if (old) { old.CopyPropertiesFromMaterial(mm); old.shader = mm.shader; mm = old; } else AssetDatabase.CreateAsset(mm, mp);
        mr.sharedMaterial = mm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return p;
    }

    // ポータルの組：A の奥に B の部屋のコピー、B の奥に A の部屋のコピー。人を運ぶ（players）・ふわふわを運ぶ（balls）
    // ===== カメラ式ポータル（2026-11-14 てじゃ「ワープ前にカーテンとかクモの巣とかみえない」「明るさ」→ A）=====
    public static bool CameraPortals = true, TwoWayPortals = true;
    public static int PortalRTSize = 1280;   // 2026-11-15 1024→1280：カメラは穴の大きさに合わせて一部だけに描く（FuwaPortalCams）。近くはくっきり、遠くは小さく
    static readonly List<(Transform entry, Transform exit, MeshRenderer hole)> camPortals = new List<(Transform, Transform, MeshRenderer)>();
    static void SetupPortalCams()
    {
        if (!CameraPortals || camPortals.Count == 0) return;
        var g = new GameObject("PortalCams"); g.transform.SetParent(root, false);
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/RenderTextures")) AssetDatabase.CreateFolder("Assets/FuwaStage2", "RenderTextures");
        Camera MakeCam(string n)
        {
            string rp = "Assets/FuwaStage2/RenderTextures/PortalCam" + n + ".renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rp);
            if (!rt) { rt = new RenderTexture(PortalRTSize, PortalRTSize, 24, RenderTextureFormat.ARGB32); AssetDatabase.CreateAsset(rt, rp); }
            if (rt.width != PortalRTSize) { rt.Release(); rt.width = PortalRTSize; rt.height = PortalRTSize; }
            rt.antiAliasing = 2; rt.useMipMap = false; rt.wrapMode = TextureWrapMode.Clamp; EditorUtility.SetDirty(rt);
            var c = new GameObject("Cam" + n).AddComponent<Camera>(); c.transform.SetParent(g.transform, false);
            c.enabled = false; c.targetTexture = rt; c.clearFlags = CameraClearFlags.Skybox; c.nearClipPlane = 0.05f; c.farClipPlane = PortalCamFar;   // 2026-11-15 軽量化：2000 → PortalCamFar（穴の向こうに見えるのはほぼコースの中）
            c.cullingMask = ~((1 << 5) | (1 << 12));   // UI・UiMenu は映さない
            c.allowHDR = false; c.allowMSAA = true; c.useOcclusionCulling = false;
            return c;
        }
        var cl = MakeCam("L"); var cr = MakeCam("R");
        foreach (var (_, _, h) in camPortals) { h.sharedMaterial.SetTexture("_TexL", cl.targetTexture); h.sharedMaterial.SetTexture("_TexR", cr.targetTexture); EditorUtility.SetDirty(h.sharedMaterial); }
        var pc = UdonSharpUndo.AddComponent<FuwaPortalCams>(g);
        pc.entries = camPortals.Select(x => x.entry).ToArray(); pc.exits = camPortals.Select(x => x.exit).ToArray(); pc.holes = camPortals.Select(x => x.hole).ToArray();
        pc.camL = cl; pc.camR = cr;
        UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc);
        log.Append("カメラ式ポータル " + camPortals.Count + " ");
    }

    static void Link(Transform A, Transform B, Transform roomOfB, Transform roomOfA, bool players, bool balls, float halfW = -1f, float height = -1f)
    {
        int sa = (int)A.Find("PortalHole").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_StencilRef");
        int sb = (int)B.Find("PortalHole").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_StencilRef");
        if (CameraPortals)
        {
            // 2026-11-14 カメラ式：穴は Fuwa/PortalCam（出口側のカメラの映像）。コピーとふわふわのうつしはなし
            var hr = A.Find("PortalHole").GetComponent<MeshRenderer>();
            string mp = "Assets/FuwaStage2/Materials/Ruins_PortalCam_" + A.name + ".mat";
            var cm = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!cm) { cm = new Material(Shader.Find("Fuwa/PortalCam")); AssetDatabase.CreateAsset(cm, mp); }
            cm.shader = Shader.Find("Fuwa/PortalCam"); cm.SetFloat("_Active", 0f); EditorUtility.SetDirty(cm);
            hr.sharedMaterial = cm;
            camPortals.Add((A, B, hr));
            if (TwoWayPortals)
            {
                // 2026-11-14 てじゃ「これなら戻りのポータルうつしてもいいかも」「行き来できてもいいかも」→ 出口側の穴にも来た側を映して、くぐったら戻れる
                var hb = B.Find("PortalHole").GetComponent<MeshRenderer>();
                string mpb = "Assets/FuwaStage2/Materials/Ruins_PortalCam_" + B.name + ".mat";
                var cmb = AssetDatabase.LoadAssetAtPath<Material>(mpb);
                if (!cmb) { cmb = new Material(Shader.Find("Fuwa/PortalCam")); AssetDatabase.CreateAsset(cmb, mpb); }
                cmb.shader = Shader.Find("Fuwa/PortalCam"); cmb.SetFloat("_Active", 0f); EditorUtility.SetDirty(cmb);
                hb.sharedMaterial = cmb;
                camPortals.Add((B, A, hb));
            }
        }
        else FuwaPortalTest.Copy(roomOfB, A, B, viewT, "ViewThrough_" + A.name, sa);
        // 一方通行（2026-11-11 てじゃ「戻れないようにしとこ、前のが見えると空がみえちゃう」）：出る側 B は穴（向こうの見え方）をなくして、奥の暗いくぼみだけ
        bool two = CameraPortals && TwoWayPortals;
        if (!two)
        {
            B.Find("PortalHole").gameObject.SetActive(false);
            if (OvalPortals && halfW <= 0f) OvalBack(B); else Niche(B, halfW > 0f ? halfW : FuwaPortalTest.HalfW, height > 0f ? height : FuwaPortalTest.HoleH);
        }
        foreach (var (en, ex, sref) in two ? new[] { (A, B, sa), (B, A, sb) } : new[] { (A, B, sa) })
        {
            var pt = UdonSharpUndo.AddComponent<FuwaPortal>(en.gameObject);
            pt.entry = en; pt.exit = ex;
            pt.halfW = halfW > 0f ? halfW : FuwaPortalTest.HalfW; pt.height = height > 0f ? height : FuwaPortalTest.HoleH;
            pt.ball = balls ? ballT : null; pt.ballRadius = ballR;
            if (balls && !CameraPortals)
            {
                pt.ghost = FuwaPortalTest.BallGhost(viewT, ballT, en, sref, false);
                pt.nearGhost = FuwaPortalTest.BallGhost(viewT, ballT, en, sref, true);
            }
            UdonSharpEditorUtility.CopyProxyToUdon(pt); FuwaStage2WaterTest.NoSync(pt);
            // 人を運ばない窓：FuwaPortal は人も見るので、穴の高さを人の頭が届かない大きさにしてある（窓は人が通れないので頭が入ることはない）
            if (!players) { }
        }
    }

    // だ円の輪の出る側：輪の中を暗い石の円盤でふさぐ（前から見ると暗い、うしろは石の色）
    static void OvalBack(Transform B)
    {
        var n = new GameObject("ExitBack").transform; n.SetParent(B, false); n.localPosition = OvalC + new Vector3(0f, 0f, -0.05f);
        var d = EllipseDisc(OvalA + 0.1f, OvalB + 0.1f, 64, "Portal_OvalBack");
        var f = new GameObject("Front"); f.transform.SetParent(n, false); f.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Material fm = terraD, bm = stone;
        if (PortalFloating)
        {   // 浮いたポータルの裏：石ではなく、待機のうず（穴と同じ見た目）
            var mp = "Assets/FuwaStage2/Materials/Ruins_PortalBack.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!m) { m = new Material(Shader.Find("Fuwa/PortalCam")); AssetDatabase.CreateAsset(m, mp); }
            m.SetFloat("_Active", 0f); EditorUtility.SetDirty(m); fm = m; bm = m;
        }
        f.AddComponent<MeshFilter>().sharedMesh = d; f.AddComponent<MeshRenderer>().sharedMaterial = fm;
        var bk = new GameObject("Back"); bk.transform.SetParent(n, false); bk.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        bk.AddComponent<MeshFilter>().sharedMesh = d; bk.AddComponent<MeshRenderer>().sharedMaterial = bm;
    }

    // 出る側のくぼみ：壁のすき間の奥を暗い石でふさぐ（人の体・ふわふわが面の奥に少し出ても当たらない深さ 1.3m）
    static void Niche(Transform B, float hw, float hh)
    {
        var n = new GameObject("ExitNiche").transform; n.SetParent(B, false);
        float depth = 1.3f, t = 0.2f, w = hw + 0.06f;
        Box(n, "Back", new Vector3(0f, hh / 2f - 0.3f, -depth - t / 2f), new Vector3(2f * w + 2f * t, hh + 0.6f + t, t), terraD, 0);
        foreach (float sx in new[] { -1f, 1f }) Box(n, "Side", new Vector3(sx * (w + t / 2f), hh / 2f - 0.3f, -depth / 2f), new Vector3(t, hh + 0.6f, depth), terraD, 0);
        Box(n, "Top", new Vector3(0f, hh + t / 2f + 0.01f, -depth / 2f), new Vector3(2f * w + 2f * t, t, depth), terraD, 0);
    }

    // 大きさのルーン（くぐる向き＝進む向き）。いまの P に置く
    static void Rune(string name, int mode)
    {
        FuwaSizeTest.RuneAt(secT, name, P + Vector3.up * FuwaSizeTest.RuneHeight, Quaternion.LookRotation(Dir(th)), mode, FuwaSizeTest.RuneColor(mode));
    }

    // 風のゾーン（ふわふわだけ）：c＝まん中（コースの座標）、heading の向きの箱、dirLocal はその箱の中での向き（+z が進む向き、+x が右）
    static void WindZone(string name, Vector3 c, float heading, Vector3 size, Vector3 dirLocal, float strength)
    {
        var w = new GameObject(name); w.transform.SetParent(secT, false); w.layer = 2;
        w.transform.localPosition = c; w.transform.localRotation = Quaternion.LookRotation(Dir(heading));
        var bc = w.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        var wa = UdonSharpUndo.AddComponent<FuwaWindArea>(w);
        wa.localDirection = dirLocal.normalized; wa.strength = strength; wa.offSeconds = 0f; wa.baseLevel = 1f;
        UdonSharpEditorUtility.CopyProxyToUdon(wa); FuwaStage2WaterTest.NoSync(wa);
        // 目印：風の筋（うすい板、ブロックアウト用）
        var m = FuwaGearTest.Mat("SizeTest_WindMark", new Color(0.8f, 0.95f, 1f));
        var rot = Quaternion.LookRotation(dirLocal.normalized);
        for (int k = 0; k < 8; k++)
        {
            var p = new Vector3(Mathf.Lerp(-size.x * 0.4f, size.x * 0.4f, (k * 0.37f) % 1f), Mathf.Lerp(-size.y * 0.3f, size.y * 0.3f, (k * 0.61f) % 1f), Mathf.Lerp(-size.z * 0.4f, size.z * 0.4f, (k * 0.23f + 0.1f) % 1f));
            var s = FuwaPortalTest.Prim(PrimitiveType.Cube, w.transform, "Streak", p, new Vector3(0.03f, 0.03f, 1.2f), m); s.localRotation = rot;
        }
    }

    // 吸いこむ渦（捕まえるつむじ風）：c＝底のまん中（コースの座標、道の高さ）
    static void Whirl(string name, Vector3 c)
    {
        var src = GameObject.Find("FuwaCourse/Gimmicks_Kaze/G5_SmallWhirls/Whirl0");
        if (!src) { log.Append("（つむじ風の見本がない）"); return; }
        var g = Object.Instantiate(src, secT, false); g.name = name;
        g.transform.localPosition = c + Vector3.down * 0.5f; g.transform.localRotation = Quaternion.LookRotation(Dir(th));
        var ww = g.GetComponent<FuwaWhirlwind>();
        if (ww)
        {
            ww.trap = true; ww.trapPull = 3.2f; ww.radius = 2.6f;
            UdonSharpEditorUtility.CopyProxyToUdon(ww); FuwaStage2WaterTest.NoSync(ww);
        }
        // 判定の筒を道の上まで広げる
        var bc = g.GetComponentInChildren<BoxCollider>(true);
        if (bc) { bc.size = new Vector3(5.6f, 5f, 5.6f); bc.center = new Vector3(0f, 2.5f, 0f); }
        // 足場（渦の下の小さい浮島、見た目だけ）
        var isl = FuwaPortalTest.Prim(PrimitiveType.Cylinder, secT, name + "_Isle", c + Vector3.down * 0.9f, new Vector3(2.2f, 0.4f, 2.2f), underM);
    }

    // 鳥：道の上で待つ（止まり木＝c）→ ふわふわをつかむと道の外（左）へ運んで落とす。でかいと持てない
    static void Bird(string name, Vector3 c)
    {
        var t = new GameObject(name).transform; t.SetParent(secT, false);
        var body = new GameObject("Body").transform; body.SetParent(t, false);
        var bm = FuwaStage2WaterTest.ToonMat("Ruins_Bird", new Color(0.36f, 0.32f, 0.42f));
        var bk = FuwaStage2WaterTest.ToonMat("Ruins_BirdBeak", new Color(1f, 0.75f, 0.3f));
        FuwaPortalTest.Prim(PrimitiveType.Sphere, body, "Torso", Vector3.zero, new Vector3(0.7f, 0.55f, 0.9f), bm);
        FuwaPortalTest.Prim(PrimitiveType.Sphere, body, "Head", new Vector3(0f, 0.3f, 0.45f), new Vector3(0.42f, 0.42f, 0.42f), bm);
        var beak = FuwaPortalTest.Prim(PrimitiveType.Cube, body, "Beak", new Vector3(0f, 0.27f, 0.72f), new Vector3(0.12f, 0.1f, 0.25f), bk);
        Transform Wing(string n, float sx)
        {
            var pivot = new GameObject(n).transform; pivot.SetParent(body, false); pivot.localPosition = new Vector3(sx * 0.3f, 0.1f, 0f);
            FuwaPortalTest.Prim(PrimitiveType.Cube, pivot, "Feather", new Vector3(sx * 0.6f, 0f, 0f), new Vector3(1.2f, 0.06f, 0.6f), bm);
            return pivot;
        }
        var wl = Wing("WingL", -1f); var wr = Wing("WingR", 1f);
        var side = Dir(th + 90f);   // 左（コースの外）へ運ぶ
        var w = new[] { c, c + side * 4f + Vector3.up * 1.5f, c + side * 10f + Vector3.up * 2f, c + side * 16f + Vector3.up * 1f };
        var fb = UdonSharpUndo.AddComponent<FuwaBird>(t.gameObject);
        fb.ball = ballT; fb.body = body; fb.wingL = wl; fb.wingR = wr;
        fb.path = w.Select(p => root.TransformPoint(p)).ToArray();
        fb.carrySpeed = 3.5f; fb.returnSpeed = 6f; fb.grabRadius = 1.8f; fb.cooldown = 1.5f; fb.cantLiftBig = true;
        body.position = fb.path[0];
        UdonSharpEditorUtility.CopyProxyToUdon(fb); FuwaStage2WaterTest.NoSync(fb);
    }

    // クモの巣（人はすり抜け＝トリガー、ふわふわはくっつく）。tear＝でかいと破れる。lp は巣のまん中、面の向きは lr の forward
    static void Web(Transform parent, string name, Vector3 lp, Quaternion lr, Vector2 size, bool tear, float playerSlow, bool smallPasses = false, bool rect = false, int corners = 0, float shapeSeed = float.NaN)
    {
        var tmpl = Resources.FindObjectsOfTypeAll<SpiderWebBuilder>().FirstOrDefault(b => b && b.gameObject.scene.IsValid() && b.name == "BigWeb" && b.transform.parent && b.transform.parent.name == "Gimmick6_Window");
        if (!tmpl) { log.Append("（クモの巣の見本がない）"); return; }
        var g = Object.Instantiate(tmpl.gameObject, parent, false); g.name = name; g.SetActive(true);
        g.transform.localPosition = lp; g.transform.localRotation = lr; g.transform.localScale = Vector3.one;
        var wh = g.GetComponent<FuwaWebHole>(); if (wh) Object.DestroyImmediate(wh);
        var b = g.GetComponent<SpiderWebBuilder>();
        b.size = size; b.holeCenter = Vector2.zero; b.holeRadius = 0.0f; b.spokes = 20; b.floorShadow = false; b.autoIsland = false;
        b.left.anchor = SpiderWebBuilder.AnchorType.None; b.right.anchor = SpiderWebBuilder.AnchorType.None; b.topThread = false;
        b.outerMin = 0.96f; b.outerMax = 1f; b.cornerRound = 0.15f;
        // rect：角までうまる四角い巣（見本の polygonFrame をやめる）。スキマの門用
        if (rect) { b.polygonFrame = false; b.outerMin = 0.99f; b.cornerRound = 8f; b.spokes = 32; }
        // 2026-11-14 てじゃ「クモの巣の形を何パターンかつくろう」：枠の角の数（3〜8）と形の種（角の向き・ゆらぎ）
        if (corners > 0) { b.polygonFrame = true; b.frameCorners = corners; }
        if (!float.IsNaN(shapeSeed)) b.seed = shapeSeed;
        b.assetId = "Web_2_5_" + parent.name + "_" + name;
        SpiderWebBuild.Build(b);
        var hz = g.GetComponent<FuwaHazard>();
        if (hz)
        {
            hz.holeCenter = null; hz.sticky = true; hz.playerSlow = playerSlow; hz.playerSlowLinger = 0f;
            hz.tearWhenBig = tear; hz.smallPasses = smallPasses;
            if (tear) hz.tearHide = g.transform.Cast<Transform>().Where(x => x.name == "Front" || x.name == "Back" || x.name == "WebSupports").Select(x => x.gameObject).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz);
        }
    }

    // ===== 回る石の腕＋飛び出す石ブロック（2026-11-14 てじゃ「2と3の複合」「当たったら弾かれるくらいでいい」）=====
    //  動く石板は「ふわふわがついてこないからきびしい、ついてくるのもへん」でやめ（コードは残置）。道は動かさず、まわりが動く
    //  FuwaRuinsBumper：サーバー時刻で全員同じ動き。腕・ブロックはトリガーだけ（道レイヤーではない）：当たると弾く、アウトにはしない
    public static float ArmLow = 0.3f, ArmHigh = 2.3f, ArmT = 0.4f;
    public static float PistonPeriod = 3.2f, PistonOut = 3.0f;
    static FuwaRuinsBumper BumperOn(GameObject g)
    {
        var rb = g.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var bp = UdonSharpUndo.AddComponent<FuwaRuinsBumper>(g);
        var snd = new GameObject("Hit"); snd.transform.SetParent(g.transform, false);
        var au = snd.AddComponent<AudioSource>(); au.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_rock_bounce.wav"); au.playOnAwake = false; au.volume = 0.8f;
        au.spatialBlend = 1f; au.rolloffMode = AudioRolloffMode.Linear; au.minDistance = 2f; au.maxDistance = 25f; au.dopplerLevel = 0f;
        bp.hitSound = au;
        return bp;
    }
    // 回る石の腕：道のまん中の柱に blades 本の腕。dz＝今の P からの距離、deg＝度/秒（＋/−で回る向き）
    static void SpinArm(Transform g, string name, float dz, float deg, int blades, float phaseDeg)
    {
        float y = P.y, z = P.z + dz, L = HW + 0.3f;
        var col = Box(g, name + "_Column", new Vector3(0f, y + 1.7f, z), new Vector3(0.9f, 3.4f, 0.9f), stone, 0);
        var cap = Box(g, name + "_Cap", new Vector3(0f, y + 3.5f, z), new Vector3(1.2f, 0.3f, 1.2f), terra, 0); Object.DestroyImmediate(cap.GetComponent<Collider>());
        var m = new GameObject(name); m.transform.SetParent(g, false); m.transform.localPosition = new Vector3(0f, y, z);
        for (int k = 0; k < blades; k++)
        {
            var bl = new GameObject("Blade" + k).transform; bl.SetParent(m.transform, false); bl.localRotation = Quaternion.Euler(0f, 360f * k / blades, 0f);
            var v = Box(bl, "Stone", new Vector3(0f, (ArmLow + ArmHigh) / 2f, 0.45f + (L - 0.45f) / 2f), new Vector3(ArmT, ArmHigh - ArmLow, L - 0.45f), terra, 0);
            v.GetComponent<Collider>().isTrigger = true;
            // 腕の上下のふち（見た目だけ）
            foreach (float yy in new[] { ArmLow + 0.08f, ArmHigh - 0.08f })
            { var e = Box(bl, "Rim", new Vector3(0f, yy, 0.45f + (L - 0.45f) / 2f), new Vector3(ArmT + 0.06f, 0.16f, L - 0.4f), terraD, 0); Object.DestroyImmediate(e.GetComponent<Collider>()); }
        }
        var bp = BumperOn(m); bp.mode = 0; bp.degPerSec = deg; bp.phaseDeg = phaseDeg;
        UdonSharpEditorUtility.CopyProxyToUdon(bp); FuwaStage2WaterTest.NoSync(bp);
    }
    // 飛び出す石ブロック：道のわき（sd ＋1 右 / −1 左）の柱から、道の上へ PistonOut 突き出す。h＝ブロックのまん中の高さ（道から）、ph＝タイミング（秒）
    static void Piston(Transform g, string name, float dz, float sd, float h, float ph)
    {
        float y = P.y, z = P.z + dz, xp = sd * (HW + 0.8f);
        Box(g, name + "_Pillar", new Vector3(xp, y + 2.1f, z), new Vector3(1.6f, 4.2f, 1.6f), stone, 0);
        var cap = Box(g, name + "_Cap", new Vector3(xp, y + 4.35f, z), new Vector3(1.9f, 0.3f, 1.9f), terra, 0); Object.DestroyImmediate(cap.GetComponent<Collider>());
        var m = new GameObject(name); m.transform.SetParent(g, false);
        var a = new Vector3(xp, y + h, z); var b = new Vector3(xp - sd * PistonOut, y + h, z);
        m.transform.localPosition = a;
        var v = Box(m.transform, "Block", Vector3.zero, new Vector3(1.4f, 1.1f, 1.1f), terraD, 0); v.GetComponent<Collider>().isTrigger = true;
        var face = Box(m.transform, "Face", new Vector3(-sd * 0.72f, 0f, 0f), new Vector3(0.06f, 0.8f, 0.8f), terra, 0); Object.DestroyImmediate(face.GetComponent<Collider>());
        var bp = BumperOn(m); bp.mode = 1; bp.a = a; bp.b = b; bp.period = PistonPeriod; bp.phase = ph;
        UdonSharpEditorUtility.CopyProxyToUdon(bp); FuwaStage2WaterTest.NoSync(bp);
    }

    // ===== 動く石板（2026-11-14 てじゃ「上下左右移動する石板の道はありだなぁ」）=====
    //  柱（FallingPillar）は「ふわふわのシステムと相性悪い」でやめ（コードは残置）。2–3 の動く氷と同じく、サーバー時刻で全員同じ動き・乗っている人を運ぶ（FuwaMovingSlab、上下も運ぶ）
    //  石板の上の面は道と同じシマシマ（当たりは道レイヤー：ふわふわが触れたらアウト＝上がってくる石板にも注意）
    public static float SlabL = 3.2f, SlabW = 3.4f, SlabGap = 0.4f;
    public static float SideAmp = 4.2f, SidePeriod = 8f, SideHold = 2.4f, SideStagger = 1.0f;
    public static float UpRise = 1.5f, UpPeriod = 7f, UpHold = 2f;
    public static float FerryPeriod = 11f, FerryHold = 2.2f;
    static Transform Slab(Transform parent, string name, Vector3 a, Vector3 b, float period, float phase, float hold, float len, float wid)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = a;
        int n = Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        for (int k = 0; k < n; k++)
        {
            var st = Box(g.transform, "S" + k, new Vector3(0f, -0.3f, -len / 2f + len / n * (k + 0.5f)), new Vector3(wid, 0.6f, len / n), (k % 2 == 0) ? pathA : pathB, 0);
            Object.DestroyImmediate(st.GetComponent<Collider>());
        }
        var col = new GameObject("Col"); col.layer = pathLayer; col.transform.SetParent(g.transform, false);
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, -0.3f, 0f); bc.size = new Vector3(wid, 0.6f, len);
        var u1 = Box(g.transform, "Under", new Vector3(0f, -1f, 0f), new Vector3(wid - 0.3f, 0.8f, len - 0.3f), stone, 0); Object.DestroyImmediate(u1.GetComponent<Collider>());
        var u2 = Box(g.transform, "Under2", new Vector3(0f, -1.75f, 0f), new Vector3(wid * 0.55f, 0.7f, len * 0.55f), underM, 0); Object.DestroyImmediate(u2.GetComponent<Collider>());
        var ms = UdonSharpUndo.AddComponent<FuwaMovingSlab>(g);
        ms.a = a; ms.b = b; ms.period = period; ms.phase = phase; ms.hold = hold;
        UdonSharpEditorUtility.CopyProxyToUdon(ms); FuwaStage2WaterTest.NoSync(ms);
        return g.transform;
    }
    // 左右にスライドする石板 count 枚（道の列にそろう時だけ渡れる。そろうタイミングを1枚ずつ SideStagger 秒ずらす、左右交互に出ていく）
    static void SideSlabs(string tag, int count)
    {
        float z0 = P.z, y = P.y, step = SlabL + SlabGap;
        var g = new GameObject("SideSlabs_" + tag).transform; g.SetParent(isl, false);
        for (int i = 0; i < count; i++)
        {
            float zc = z0 + SlabGap / 2f + step * i + SlabL / 2f, sd = (i % 2 == 0) ? 1f : -1f;
            Slab(g, "Slab" + i, new Vector3(0f, y, zc), new Vector3(sd * SideAmp, y, zc), SidePeriod, -SideStagger * i, SideHold, SlabL, SlabW);
        }
        P += Dir(th) * (step * count + SlabGap);
    }
    // 上下する石板 count 枚で UpRise ずつ登る（となりどうし、片方が上・片方が下の時に高さがそろう）
    static void UpSlabs(string tag, int count)
    {
        float z0 = P.z, y = P.y, step = SlabL + SlabGap;
        var g = new GameObject("UpSlabs_" + tag).transform; g.SetParent(isl, false);
        for (int i = 0; i < count; i++)
        {
            float zc = z0 + SlabGap / 2f + step * i + SlabL / 2f;
            Slab(g, "Slab" + i, new Vector3(0f, y + UpRise * i, zc), new Vector3(0f, y + UpRise * (i + 1), zc), UpPeriod, (i % 2 == 0) ? 0f : UpPeriod * 0.5f, UpHold, SlabL, SlabW);
        }
        P += Dir(th) * (step * count + SlabGap) + Vector3.up * UpRise * count;
    }
    // 前後に行き来する石板1枚で、gap の大きいすき間をわたる
    static void FerrySlab(string tag, float gap)
    {
        float z0 = P.z, y = P.y;
        var g = new GameObject("Ferry_" + tag).transform; g.SetParent(isl, false);
        Slab(g, "Slab", new Vector3(0f, y, z0 + SlabGap / 2f + SlabL / 2f), new Vector3(0f, y, z0 + gap - SlabGap / 2f - SlabL / 2f), FerryPeriod, 0f, FerryHold, SlabL, SlabW);
        P += Dir(th) * gap;
    }

    // ===== 倒れる柱（2026-11-14 てじゃ「上段いくか、ギミック全部かえたい、カーテンギミックはナシ、柱が倒れてくギミック」）=====
    //  前半：道のわきの柱が、近づくとグラグラ → 道へバタン（当たるとふわふわもプレイヤーも大きく吹っ飛ぶ）。少しすると起き上がる
    //  後半：道のすき間に柱が倒れて、はまって橋になる（手前から向こうへ／向こうからこっちへ）
    //  最後：崩れて登りきれない階段に、上の台の柱が倒れて坂になる → 登ってポータル
    public static float PillarW = 1.1f, BridgePillarW = 1.6f, BridgePillarT = 1.0f, PillarArmR = 4.5f;
    // 柱1本：parent の中の lp（ちょうつがい＝柱の倒れる側の下のはし）、yaw（parent の座標で、倒れる向き）
    //  柱の体は ちょうつがい から見て x ±wx/2・y 0〜L・z −t〜0（倒れる向きのうしろに立つ）。endOff：倒れきった時のちょうつがいのずれ（倒れる向きの座標）
    static FuwaFallingPillar FallingPillar(Transform parent, string name, Vector3 lp, float yaw, float L, float wx, float t, Vector3 armLp, float armR, float fallAngle, Vector3 endOff, float lieTime, bool deco)
    {
        var r = new GameObject(name).transform; r.SetParent(parent, false);
        r.localPosition = lp; r.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var hinge = new GameObject("Hinge"); hinge.transform.SetParent(r, false);
        var body = Box(hinge.transform, "Body", new Vector3(0f, L / 2f, -t / 2f), new Vector3(wx, L, t), stone, pathLayer);
        if (deco)
        {
            // 柱頭と台座（見た目だけ）
            foreach (var (y, h) in new[] { (L - 0.22f, 0.44f), (0.16f, 0.32f) })
            { var d = Box(hinge.transform, "Band", new Vector3(0f, y, -t / 2f), new Vector3(wx + 0.24f, h, t + 0.24f), terra, 0); Object.DestroyImmediate(d.GetComponent<Collider>()); }
        }
        else
        {
            // 両はしの色ちがい（橋・坂の柱。でっぱらないように面にそろえる）
            foreach (float y in new[] { 0.2f, L - 0.2f })
            { var d = Box(hinge.transform, "End", new Vector3(0f, y, -t / 2f), new Vector3(wx + 0.02f, 0.4f, t + 0.02f), terra, 0); Object.DestroyImmediate(d.GetComponent<Collider>()); }
        }
        var hit = hinge.AddComponent<BoxCollider>(); hit.isTrigger = true; hit.center = new Vector3(0f, L / 2f, -t / 2f); hit.size = new Vector3(wx + 0.3f, L + 0.2f, t + 0.3f);
        var rb = hinge.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var arm = new GameObject("Arm").transform; arm.SetParent(parent, false); arm.localPosition = armLp; arm.SetParent(r, true);
        AudioSource Snd(string n, string clip, bool loop, float vol)
        {
            var go = new GameObject(n); go.transform.SetParent(r, false); go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var a = go.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clip); a.loop = loop; a.playOnAwake = false; a.volume = vol;
            a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 3f; a.maxDistance = 35f; a.dopplerLevel = 0f;
            return a;
        }
        var fp = UdonSharpUndo.AddComponent<FuwaFallingPillar>(hinge);
        fp.arm = arm; fp.armRadius = armR; fp.ball = ballT ? ballT.transform : null;
        fp.fallAngle = fallAngle; fp.startPos = Vector3.zero; fp.endPos = endOff; fp.lieTime = lieTime;
        fp.body = body; fp.bodyLayer = pathLayer;
        fp.rumble = Snd("Rumble", "Assets/FuwaStage2/Audio/Gimmick2_4/G24_rumble_E.wav", true, 0.55f);
        fp.thud = Snd("Thud", "Assets/FuwaStage2/Audio/sfx_rock_bounce.wav", false, 1f);
        UdonSharpEditorUtility.CopyProxyToUdon(fp); FuwaStage2WaterTest.NoSync(fp);
        return fp;
    }

    // 前半：道のわきの柱。pillars：x＝区間の始めからの距離、y＝わき（+1 右 / −1 左）
    static void FallRun(string tag, float len, Vector2[] pillars)
    {
        float z0 = P.z, y = P.y, W = PillarW, L = 2f * HW + 0.8f;
        var g = new GameObject("FallRun_" + tag).transform; g.SetParent(isl, false);
        S(len);
        for (int i = 0; i < pillars.Length; i++)
        {
            float z = z0 + pillars[i].x, sd = Mathf.Sign(pillars[i].y);
            var pl = Box(g, "Plinth", new Vector3(sd * (HW + 0.02f + W / 2f), y - 0.9f, z - W / 2f * 0f), new Vector3(W + 0.5f, 1.8f, W + 0.5f), terraD, 0);
            FallingPillar(g, "Pillar" + i, new Vector3(sd * (HW + 0.02f), y, z - 0f), sd > 0f ? -90f : 90f, L, W, W, new Vector3(0f, y, z - 2.5f), PillarArmR, 90f, Vector3.zero, 1.6f, true);
        }
    }

    // 後半：道のすき間（gap）に柱が倒れて、はまって橋になる。fromFar：向こう岸の柱がこっちへ倒れる（近くにいると当たる）
    static void PillarGap(string tag, float gap, bool fromFar)
    {
        float z0 = P.z, y = P.y, t = BridgePillarT, e = 0.5f, L = gap + 2f * e;
        var g = new GameObject("PillarGap_" + tag).transform; g.SetParent(isl, false);
        if (!fromFar) FallingPillar(g, "Pillar", new Vector3(0f, y, z0), 0f, L, BridgePillarW, t, new Vector3(0f, y, z0 - 3f), 3.5f, 90f, new Vector3(0f, -t, -e), 0f, false);
        else FallingPillar(g, "Pillar", new Vector3(0f, y, z0 + gap), 180f, L, BridgePillarW, t, new Vector3(0f, y, z0 - 1.5f), 3.5f, 90f, new Vector3(0f, -t, -e), 0f, false);
        P += Dir(th) * gap;
    }

    // 最後：崩れた階段（見た目は段、当たりは坂）→ 踊り場 → 崩れたすき間（StairGapG）→ StairUpH 高い上の台。上の台のふちの柱が踊り場へ倒れて坂になる
    public static float StairGapG = 3.6f, StairUpH = 1.8f, StairTopLen = 7f;
    static void BrokenStairs()
    {
        const float rise = 0.15f, tread = 0.4f; const int n = 16;
        float hL = n * rise, z0 = P.z, y0 = P.y, bot = y0 - 1.8f, w = 2f * HW;
        var g = new GameObject("BrokenStairs").transform; g.SetParent(isl, false);
        for (int i = 0; i < n; i++)
        {
            float top = y0 + rise * (i + 1);
            var st = Box(g, "Step" + i, new Vector3(0f, (top + bot) / 2f, z0 + tread * (i + 0.5f)), new Vector3(w, top - bot, tread), (i % 2 == 0) ? pathA : pathB, 0);
            Object.DestroyImmediate(st.GetComponent<Collider>());
        }
        { // 坂の当たり：(z0 − tread/2, y0) → (z0 + (n − 0.5)·tread, y0 + hL)
            Vector3 a = new Vector3(0f, y0, z0 - tread / 2f), b = new Vector3(0f, y0 + hL, z0 + (n - 0.5f) * tread);
            var d = b - a; var c = new GameObject("StairSlope"); c.layer = pathLayer; c.transform.SetParent(g, false);
            c.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            c.transform.localPosition = (a + b) / 2f + c.transform.localRotation * new Vector3(0f, -0.2f, 0f);
            var bc = c.AddComponent<BoxCollider>(); bc.size = new Vector3(w, 0.4f, d.magnitude);
        }
        float zl0 = z0 + (n - 0.5f) * tread, zl1 = z0 + n * tread + 1.6f, yl = y0 + hL;
        var land = Box(g, "Landing", new Vector3(0f, (yl + bot) / 2f, (z0 + n * tread + zl1) / 2f), new Vector3(w, yl - bot, zl1 - z0 - n * tread), pathA, 0);
        Object.DestroyImmediate(land.GetComponent<Collider>());
        { var c = new GameObject("LandingCol"); c.layer = pathLayer; c.transform.SetParent(g, false); var bc = c.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, yl - 0.3f, (zl0 + zl1) / 2f); bc.size = new Vector3(w, 0.6f, zl1 - zl0); }
        // 崩れた段（すき間の下のほうに、かたむいた段のかけら。当たりなし）
        var rnd = new System.Random(11);
        for (int i = 0; i < 5; i++)
        {
            float z = zl1 + StairGapG * (i + 0.5f) / 5f, yy = yl - 1.2f - 0.9f * (float)rnd.NextDouble();
            var rb = Box(g, "Rubble" + i, new Vector3(((float)rnd.NextDouble() - 0.5f) * w * 0.7f, yy, z), new Vector3(0.9f + (float)rnd.NextDouble(), 0.35f, 0.5f + 0.3f * (float)rnd.NextDouble()), (i % 2 == 0) ? pathA : pathB, 0);
            rb.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 30f - 15f, (float)rnd.NextDouble() * 40f - 20f, (float)rnd.NextDouble() * 30f - 15f);
            Object.DestroyImmediate(rb.GetComponent<Collider>());
        }
        // 上の台
        float zT = zl1 + StairGapG, yT = yl + StairUpH;
        var blk = Box(g, "TopBlock", new Vector3(0f, (yT - 0.6f + bot) / 2f, zT + 1.5f), new Vector3(w, yT - 0.6f - bot, 3f), stone, 0);
        Object.DestroyImmediate(blk.GetComponent<Collider>());
        // 坂になる柱：上の台のふち（zT）に立って、踊り場へ倒れる。上の面が、台のふち → 踊り場のすき間側から 0.8m の所 を結ぶ
        float D = StairGapG + 0.8f, th0 = Mathf.Atan2(StairUpH, D), t = BridgePillarT + 0.2f;
        float L = Mathf.Sqrt(D * D + StairUpH * StairUpH) + 0.7f;
        FallingPillar(g, "RampPillar", new Vector3(0f, yT, zT), 180f, L, BridgePillarW + 0.3f, t, new Vector3(0f, yl, z0 + n * tread), 2.5f,
            90f + th0 * Mathf.Rad2Deg, new Vector3(0f, -t * Mathf.Cos(th0), -t * Mathf.Sin(th0)), 0f, false);
        P = new Vector3(0f, yT, zT);
        S(StairTopLen);
    }

    // ===== ステンドグラスの巣の門（2026-11-14 てじゃ「もっと丁寧に、スキマのある巣を1から作ってみて」）=====
    //  門の四角を、種の点からボロノイ分割した凸の多角形（ステンドグラス）に分けて、多角形1つ＝巣1枚。巣の枠（多角形の枠の糸）は多角形ぴったり、当たりも同じ多角形
    //  スキマは gap の2つの多角形のさかい目だけ、両がわを VoronoiGap/2 ずつ引っこめて作る。ほかのさかい目はぴったり（すき間なし）
    public static float VoronoiGap = 0.55f;
    public static float CellWebHalfT = 0.06f;
    public static float CellWebInset = 0.03f;
    static List<Vector2> ClipHalf(List<Vector2> poly, Vector2 m, Vector2 n, float off)
    {
        // dot(p - m, n) <= off の側を残す
        var o = new List<Vector2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            float da = Vector2.Dot(a - m, n) - off, db = Vector2.Dot(b - m, n) - off;
            if (da <= 0f) o.Add(a);
            if ((da <= 0f) != (db <= 0f)) o.Add(a + (b - a) * (da / (da - db)));
        }
        return o;
    }
    static List<Vector2> SimplifyPoly(List<Vector2> q, int maxN)
    {
        // 近すぎる点をまとめて、角が maxN を超える時は いちばんまっすぐな角 から消す
        var p = new List<Vector2>(q);
        for (int i = p.Count - 1; i >= 0 && p.Count > 3; i--) if ((p[i] - p[(i + 1) % p.Count]).magnitude < 0.06f) p.RemoveAt(i);
        while (p.Count > maxN)
        {
            int best = 0; float bestA = -1f;
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[(i + p.Count - 1) % p.Count]; var b = p[i]; var c = p[(i + 1) % p.Count];
                float score = 180f - Vector2.Angle(b - a, c - b);
                if (score > bestA) { bestA = score; best = i; }
            }
            p.RemoveAt(best);
        }
        return p;
    }
    public static bool VoronoiCoplanar = true; public static float CellFrameInset = 0.022f;   // 枠の糸の見える半分の太さ（_LineWidth 0.0165 ×0.5 ×1.7 ＋ふち）
    static void VoronoiGates(string tag, Vector2[][] seedsPerGate, Vector2Int[] gapPairs)
    {
        float z0 = P.z, len = seedsPerGate.Length * (WebGateGap + MidWebExtra);
        var g = new GameObject("VoronoiGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = WebXO;
        for (int gi = 0; gi < seedsPerGate.Length; gi++)
        {
            float z = z0 + (WebGateGap + MidWebExtra) * (gi + 0.5f);
            gateZ.Add((isl, z));
            var sd = seedsPerGate[gi]; var gp = gapPairs[gi];
            for (int i = 0; i < sd.Length; i++)
            {
                var poly = new List<Vector2> { new Vector2(-xo, 0f), new Vector2(xo, 0f), new Vector2(xo, hTop), new Vector2(-xo, hTop) };
                for (int j = 0; j < sd.Length && poly.Count >= 3; j++)
                {
                    if (j == i) continue;
                    var n = (sd[j] - sd[i]).normalized; var m = (sd[i] + sd[j]) * 0.5f;
                    bool isGap = (gp.x == i && gp.y == j) || (gp.x == j && gp.y == i);
                    poly = ClipHalf(poly, m, n, isGap ? -VoronoiGap * 0.5f : 0f);
                }
                if (poly.Count < 3) continue;
                poly = SimplifyPoly(poly, 8);
                var sh = ShapeFor(tag, gi, i);
                // 2026-11-15 てじゃ「小さくなって進むとこのクモの巣、重なってるとこがZファイト気味」：となりの巣の枠の糸が同じ所に重なり、4cm 前後にずらしてあるだけだったので
                //  見る向きで前後が入れかわってチラついた → 巣は同じ面に置いて、枠の糸を糸の太さの半分だけ内がわへ（となりどうし糸がくっつくだけで重ならない）
                CellWeb(g, "Web" + gi + "_" + i, poly, VoronoiCoplanar ? z : z + ((i % 3) - 1) * 0.04f, sh.s);
            }
        }
        S(len);
    }
    // 多角形 poly（断面の座標：x 右・y 床から上）ぴったりの巣
    static void CellWeb(Transform parent, string name, List<Vector2> poly, float z, float seed)
    {
        var tmpl = Resources.FindObjectsOfTypeAll<SpiderWebBuilder>().FirstOrDefault(b => b && b.gameObject.scene.IsValid() && b.name == "BigWeb" && b.transform.parent && b.transform.parent.name == "Gimmick6_Window");
        if (!tmpl) { log.Append("（クモの巣の見本がない）"); return; }
        Vector2 mn = poly[0], mx = poly[0], cen = Vector2.zero;
        foreach (var q in poly) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); cen += q; }
        cen /= poly.Count;
        var bc = (mn + mx) * 0.5f; var size = (mx - mn) + Vector2.one * 0.1f;
        var g = Object.Instantiate(tmpl.gameObject, parent, false); g.name = name; g.SetActive(true);
        g.transform.localPosition = new Vector3(bc.x, P.y + bc.y, z); g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
        var wh = g.GetComponent<FuwaWebHole>(); if (wh) Object.DestroyImmediate(wh);
        var b = g.GetComponent<SpiderWebBuilder>();
        b.size = size; b.holeCenter = cen - bc; b.holeRadius = 0f; b.spokes = 18 + (int)(Mathf.Min(size.x, size.y) * 4f); b.floorShadow = false; b.autoIsland = false;
        b.left.anchor = SpiderWebBuilder.AnchorType.None; b.right.anchor = SpiderWebBuilder.AnchorType.None; b.topThread = false;
        b.polygonFrame = true; b.frameCorners = Mathf.Clamp(poly.Count, 3, 8); b.seed = seed; b.outerMin = 0.99f; b.outerMax = 1f;
        b.assetId = "Web_2_5_" + parent.name + "_" + name;
        SpiderWebBuild.Build(b);
        // 枠の多角形を、自分の多角形にさしかえる（巣の中心＝重心から見た角、角度の順）
        var rel = poly.Select(q => q - cen).OrderBy(v => Mathf.Atan2(v.y, v.x)).ToList();
        if (CellFrameInset > 0f)
        {   // 枠の糸の見える太さの半分だけ内がわへ（となりの巣の枠と重ならないように）
            int rn = rel.Count; var ins = new List<Vector2>();
            for (int k = 0; k < rn; k++)
            {
                var a = rel[(k + rn - 1) % rn]; var v = rel[k]; var c = rel[(k + 1) % rn];
                var e1 = (v - a).normalized; var e2 = (c - v).normalized;
                var n1 = new Vector2(-e1.y, e1.x); var n2 = new Vector2(-e2.y, e2.x);   // 角度の順＝反時計回りなので左が内がわ
                ins.Add(v + (n1 + n2) / Mathf.Max(0.2f, 1f + Vector2.Dot(n1, n2)) * CellFrameInset);
            }
            rel = ins;
        }
        if (b.webRenderer)
        {
            var m = b.webRenderer.sharedMaterial; var fv = new Vector4[4];
            for (int k = 0; k < 8; k++) { var v = k < rel.Count ? rel[k] : Vector2.zero; if (k % 2 == 0) { fv[k / 2].x = v.x; fv[k / 2].y = v.y; } else { fv[k / 2].z = v.x; fv[k / 2].w = v.y; } }
            m.SetFloat("_FrameN", rel.Count); m.SetVector("_F01", fv[0]); m.SetVector("_F23", fv[1]); m.SetVector("_F45", fv[2]); m.SetVector("_F67", fv[3]);
            EditorUtility.SetDirty(m);
        }
        // 当たり：多角形の板
        // 2026-11-14 てじゃ「ギリギリだと通り過ぎた後に裏側の上部にくっつく」→ 前は厚み 0.4（巣の面の奥 0.2 まで当たり）で、抜けかけのふわふわが面のうしろでくっついて見えた
        //  → 厚みを CellWebHalfT（面のすぐ前後だけ）にして、ふちも CellWebInset だけ内がわへ（見た目の枠の糸にかすったくらいは通す）
        var outline = rel.Select(v => v + cen - bc).ToList();
        {
            int on = outline.Count; var ins = new List<Vector2>();
            for (int k = 0; k < on; k++)
            {
                var a = outline[(k + on - 1) % on]; var v = outline[k]; var c = outline[(k + 1) % on];
                var e1 = (v - a).normalized; var e2 = (c - v).normalized;
                var n1 = new Vector2(-e1.y, e1.x); var n2 = new Vector2(-e2.y, e2.x);
                ins.Add(v + (n1 + n2) / Mathf.Max(0.2f, 1f + Vector2.Dot(n1, n2)) * CellWebInset);
            }
            outline = ins;
        }
        var V = new List<Vector3>();
        foreach (var v in outline) V.Add(new Vector3(v.x, v.y, -CellWebHalfT));
        foreach (var v in outline) V.Add(new Vector3(v.x, v.y, CellWebHalfT));
        int nn = outline.Count; var T = new List<int>();
        for (int i = 1; i < nn - 1; i++) { T.Add(0); T.Add(i + 1); T.Add(i); T.Add(nn); T.Add(nn + i); T.Add(nn + i + 1); }
        for (int i = 0; i < nn; i++) { int j = (i + 1) % nn; T.Add(i); T.Add(j); T.Add(nn + i); T.Add(j); T.Add(nn + j); T.Add(nn + i); }
        var cm = SaveMesh("WebCell_" + parent.name + "_" + name, V, T);
        var mc = g.GetComponent<MeshCollider>(); if (!mc) mc = g.AddComponent<MeshCollider>();
        mc.sharedMesh = null; mc.convex = true; mc.isTrigger = true; mc.sharedMesh = cm;
        var hz = g.GetComponent<FuwaHazard>();
        if (hz) { hz.holeCenter = null; hz.sticky = true; hz.playerSlow = 0.6f; hz.playerSlowLinger = 0f; hz.tearWhenBig = false; hz.smallPasses = false; UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    // 巣の形の見本（2026-11-14 てじゃ「クモの巣の形を何パターンかつくろう」→ 8つ見せて「ぜんぶまぜよ」）：(枠の角の数, 形の種)
    static readonly (int c, float s)[] WebShapes = { (4, 1f), (4, 6f), (5, 2f), (5, 7f), (6, 3f), (6, 9f), (7, 4f), (8, 5f) };
    static (int c, float s) ShapeFor(string tag, int gate, int cell) { int h = (tag.GetHashCode() & 0x7fff) * 31 + gate * 17 + cell * 7; return WebShapes[(h % WebShapes.Length + WebShapes.Length) % WebShapes.Length]; }

    // 六角形っぽい巣をすき間なく並べた門（2026-11-14 てじゃ「むしろ最初のクモの巣4枚並べてできたスキマみたいな感じがいいかも」）
    //  見本の巣（六角形っぽい外形）をマスいっぱいに並べると、巣と巣の角に自然にスキマができる → そこを小さいふわふわで通す
    //  grids：門ごとに (列, 段, 段ごとのずらし 0〜0.5, 予備)。ずらすと段ごとに巣が半マスずれる（レンガ）
    public static float HexGridGrow = 1.15f;
    static void HexGridGates(string tag, Vector4[] grids)
    {
        float z0 = P.z, len = grids.Length * WebGateGap;
        var g = new GameObject("HexGridGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = WebXO;
        for (int i = 0; i < grids.Length; i++)
        {
            float z = z0 + WebGateGap * (i + 0.5f);
            gateZ.Add((isl, z));
            int cols = (int)grids[i].x, rows = (int)grids[i].y; float shift = grids[i].z;
            float cw = 2f * xo / cols, rh = hTop / rows;
            for (int r = 0; r < rows; r++)
            {
                float off = (r % 2 == 1) ? shift * cw : 0f;
                // ずらした段は、左はしに半端なマスができるので1つ多く作る（はみ出しは柱の中）
                int n = cols + (off > 0.01f ? 1 : 0);
                for (int c = 0; c < n; c++)
                {
                    float x0 = -xo + c * cw - off, x1 = x0 + cw;
                    x0 = Mathf.Max(x0, -xo - 0.3f); x1 = Mathf.Min(x1, xo + 0.3f);
                    if (x1 - x0 < 0.6f) continue;
                    var sh = ShapeFor(tag, i, c * 10 + r);
                    // 2026-11-14 てじゃ「小さいゾーン、もっとスキマ細くてもいいかも」→ 巣をマスより HexGridGrow 倍大きくして、となりと少し重ねる（角のスキマが細くなる）
                    Web(g, "Web" + i + "_" + c + r, new Vector3((x0 + x1) / 2f, P.y + rh * (r + 0.5f), z + ((c + r) % 2) * 0.04f), Quaternion.identity, new Vector2(x1 - x0, rh) * HexGridGrow, false, 0.6f, false, false, sh.c, sh.s);
                }
            }
        }
        S(len);
    }

    // ばらばらにつめた小さい巣の門（2026-11-14 てじゃ「（3×3 は）規則的すぎる」）：
    //  大きさ・縦横比・かたむきがバラバラの小さい巣（六角形っぽい見本の形）を、断面がうまるまでランダムに足す。スキマ（カプセル）にかかる巣は入れない
    //  gap：(中心 x, 床からの y, 向き°（0=よこ、90=たて）, 半分の長さ)。はば SlitW。重なる巣は前後に少しずらす（同じ面だとちらつく）
    public static int OrganicMax = 18; public static bool OrganicRect = true;   // 四角い巣（角までうまる）でつめる。六角形だとスキマのまわりが大きくあいた   // 1門の巣の枚数の上限（最初は上限なしで40枚以上になってぎっしりすぎた）
    static void OrganicGates(string tag, Vector4[] gaps, int seed)
    {
        float z0 = P.z, len = gaps.Length * WebGateGap;
        var g = new GameObject("OrganicGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = WebXO, gw = SlitW / 2f;
        var rnd = new System.Random(seed);
        float R(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
        for (int gi = 0; gi < gaps.Length; gi++)
        {
            float z = z0 + WebGateGap * (gi + 0.5f);
            gateZ.Add((isl, z));
            var gp = gaps[gi]; var gc = new Vector2(gp.x, gp.y); float ga = gp.z * Mathf.Deg2Rad; var gd = new Vector2(Mathf.Cos(ga), Mathf.Sin(ga)); float gh = gp.w;
            float GapDist(Vector2 q) { var d = q - gc; float t = Mathf.Clamp(Vector2.Dot(d, gd), -gh, gh); return (d - gd * t).magnitude; }
            // 断面の点（0.3m おき）。スキマのまわり（gw+0.15）はうめなくてよい
            var pts = new List<Vector2>();
            for (float x = -xo + 0.15f; x < xo; x += 0.3f) for (float y = 0.15f; y < hTop; y += 0.3f) { var q = new Vector2(x, y); if (GapDist(q) > gw + 0.12f) pts.Add(q); }
            var covered = new bool[pts.Count];
            var webs = new List<(Vector2 c, Vector2 s, float rot)>();
            bool Inside(Vector2 q, Vector2 c, Vector2 sz, float rot, float k) { var d = q - c; float cr = Mathf.Cos(-rot), sr = Mathf.Sin(-rot); var l = new Vector2(d.x * cr - d.y * sr, d.x * sr + d.y * cr); return Mathf.Abs(l.x) <= sz.x * 0.5f * k && Mathf.Abs(l.y) <= sz.y * 0.5f * k; }
            bool HitsGap(Vector2 c, Vector2 sz, float rot)
            {
                // 巣の四角（見た目は内がわの六角形だが、当たりの余裕をみて四角の 0.95）の中の点がスキマにかかるか
                for (int ix = 0; ix <= 6; ix++) for (int iy = 0; iy <= 6; iy++)
                {
                    var l = new Vector2((ix / 6f - 0.5f) * sz.x * 0.95f, (iy / 6f - 0.5f) * sz.y * 0.95f);
                    float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                    var q = c + new Vector2(l.x * cr - l.y * sr, l.x * sr + l.y * cr);
                    if (GapDist(q) < gw) return true;
                }
                return false;
            }
            for (int tries = 0; tries < 300 && webs.Count < OrganicMax; tries++)
            {
                int u = -1; for (int k = 0; k < pts.Count; k++) if (!covered[k]) { u = k; break; }
                if (u < 0) break;
                // まだうまっていない点のどれか（最初の点に近い所から少しランダムに）
                var cands = Enumerable.Range(0, pts.Count).Where(k => !covered[k]).ToList();
                // うまっていない点のうち、今ある巣の中心からいちばん遠い点（すき間なく広がるように）。少しだけランダム
                var p0 = pts[cands.OrderByDescending(k => webs.Count == 0 ? (float)rnd.NextDouble() : webs.Min(w => (w.c - pts[k]).sqrMagnitude) + (float)rnd.NextDouble() * 0.5f).First()];
                float sz = R(2.0f, 3.2f), asp = R(0.75f, 1.3f), rot = R(-25f, 25f) * Mathf.Deg2Rad;
                var c = p0 + new Vector2(R(-0.3f, 0.3f), R(-0.3f, 0.3f));
                var size = new Vector2(sz * asp, sz / asp);
                // スキマにかかる時は、スキマから遠ざける向きに少しずらしてから小さくする
                for (int mv = 0; mv < 4 && HitsGap(c, size, rot); mv++) { var away = c - gc; away -= gd * Vector2.Dot(away, gd); c += (away.sqrMagnitude > 1e-4f ? away.normalized : Vector2.up) * 0.25f; }
                while (HitsGap(c, size, rot) && size.x > 0.5f) size *= 0.88f;
                if (size.x <= 0.5f || HitsGap(c, size, rot)) { covered[u] = true; continue; }   // スキマのすぐそばでうめられない点はあきらめる
                // 床より下・上はみ出しすぎは中へ寄せる
                c.y = Mathf.Clamp(c.y, size.y * 0.3f, hTop - size.y * 0.3f);
                if (HitsGap(c, size, rot)) { covered[u] = true; continue; }
                webs.Add((c, size, rot));
                for (int k = 0; k < pts.Count; k++) if (!covered[k] && Inside(pts[k], c, size, rot, OrganicRect ? 0.97f : 0.85f)) covered[k] = true;
                covered[u] = true;
            }
            for (int wi = 0; wi < webs.Count; wi++)
            {
                var w = webs[wi]; float dz = (wi % 5 - 2) * 0.05f;
                Web(g, "Web" + gi + "_" + wi, new Vector3(w.c.x, P.y + w.c.y, z + dz), Quaternion.Euler(0f, 0f, w.rot * Mathf.Rad2Deg), w.s, false, 0.6f, false, OrganicRect);
            }
            log.Append("巣の門" + tag + gi + ":" + webs.Count + "枚 ");
        }
        S(len);
    }

    // つめた小さい巣の門（2026-11-14 てじゃ「小さい巣が複数あるやつ、小さいゾーンも、小さい巣をもっとつめて、形を変えてスキマを調整」）
    //  断面を 3×3 の四角い巣でぎっしりうめて（列のはば・段の高さは門ごとに少しバラバラ）、さかい目の1か所だけ SlitW のスキマ
    //  slit：(たて=0/よこ=1, さかい目の番号 1〜2, そのさかい目にそったマスの番号 0〜2, 種)
    static void PackedGates(string tag, Vector4[] slits)
    {
        float z0 = P.z, len = slits.Length * WebGateGap;
        var g = new GameObject("PackedGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = WebXO, hs = SlitW / 2f;
        for (int i = 0; i < slits.Length; i++)
        {
            float z = z0 + WebGateGap * (i + 0.5f);
            gateZ.Add((isl, z));
            var rnd = new System.Random((int)slits[i].w * 131 + i * 7 + 3);
            float J() => 1f + ((float)rnd.NextDouble() - 0.5f) * 0.4f;   // ±20%
            float[] cw = { J(), J(), J() }, rh = { J(), J(), J() };
            float cs = cw.Sum(), rs = rh.Sum();
            var xs = new float[4]; xs[0] = -xo; for (int c = 0; c < 3; c++) xs[c + 1] = xs[c] + cw[c] / cs * 2f * xo;
            var ys = new float[4]; ys[0] = 0f; for (int r = 0; r < 3; r++) ys[r + 1] = ys[r] + rh[r] / rs * hTop;
            bool vertical = slits[i].x < 0.5f; int line = (int)slits[i].y, cell = (int)slits[i].z;
            for (int c = 0; c < 3; c++) for (int r = 0; r < 3; r++)
            {
                float x0 = xs[c], x1 = xs[c + 1], y0 = ys[r], y1 = ys[r + 1];
                // スキマ：たてなら line 本目のたての線（xs[line]）の、r==cell のマスの左右を hs ずつ引っこめる。よこなら ys[line] の c==cell
                if (vertical && r == cell) { if (c == line - 1) x1 -= hs; if (c == line) x0 += hs; }
                if (!vertical && c == cell) { if (r == line - 1) y1 -= hs; if (r == line) y0 += hs; }
                Web(g, "Web" + i + "_" + c + r, new Vector3((x0 + x1) / 2f, P.y + (y0 + y1) / 2f, z), Quaternion.identity, new Vector2(x1 - x0, y1 - y0), false, 0.6f, false, true);
            }
        }
        S(len);
    }

    // 裂けた巣の門（2026-11-14 てじゃ「1枚ものを一部を裂くイメージ」「1〜2ライン裂けてる感じ」「斜めもあり」「序盤のも、1〜4枚じゃなくてデカイやつの一部が大きく破れてる形式に」）
    //  道いっぱいの四角い巣1枚に、tearA→tearB の線のまわり tearW の裂け目（巣の中心から、x 右・y 上、m）。当たりは裂け目のカプセル（ふわふわの半径込み）
    struct Tear { public Vector2 a, b; public float w; public bool pits; public Tear(float ax, float ay, float bx, float by, float w, bool pits) { a = new Vector2(ax, ay); b = new Vector2(bx, by); this.w = w; this.pits = pits; } }
    static void TearGates(string tag, Tear[] tears)
    {
        float z0 = P.z;
        var g = new GameObject("TearGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = WebXO;
        for (int i = 0; i < tears.Length; i++)
        {
            var T = tears[i]; float z = z0 + WebGateGap * (i + 0.5f);
            gateZ.Add((isl, z));
            TearWeb(g, "TearWeb" + i, new Vector3(0f, P.y + hTop / 2f, z), new Vector2(2f * xo, hTop), T.a, T.b, T.w);
        }
        for (int i = 0; i < tears.Length; i++) { if (tears[i].pits) WindRun("Pits_" + tag + "_" + i, WebGateGap, new float[0]); else S(WebGateGap); }
    }
    static void TearWeb(Transform parent, string name, Vector3 lp, Vector2 size, Vector2 ta, Vector2 tb, float tw)
    {
        var tmpl = Resources.FindObjectsOfTypeAll<SpiderWebBuilder>().FirstOrDefault(b => b && b.gameObject.scene.IsValid() && b.name == "BigWeb" && b.transform.parent && b.transform.parent.name == "Gimmick6_Window");
        if (!tmpl) { log.Append("（クモの巣の見本がない）"); return; }
        var g = Object.Instantiate(tmpl.gameObject, parent, false); g.name = name; g.SetActive(true);
        g.transform.localPosition = lp; g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
        var wh = g.GetComponent<FuwaWebHole>(); if (wh) Object.DestroyImmediate(wh);
        var b = g.GetComponent<SpiderWebBuilder>();
        var mid = (ta + tb) * 0.5f;
        // 放射の糸の中心は裂け目のまん中（裂け目が巣の模様になじむ）、丸い穴はなし
        b.size = size; b.holeCenter = mid; b.holeRadius = 0f; b.spokes = 40; b.floorShadow = false; b.autoIsland = false;
        b.left.anchor = SpiderWebBuilder.AnchorType.None; b.right.anchor = SpiderWebBuilder.AnchorType.None; b.topThread = false;
        b.polygonFrame = false; b.outerMin = 0.99f; b.outerMax = 1f; b.cornerRound = 8f; b.spacingRef = 0f;
        b.assetId = "Web_2_5_" + parent.name + "_" + name;
        SpiderWebBuild.Build(b);
        if (b.webRenderer)
        {
            var m = b.webRenderer.sharedMaterial;
            m.SetVector("_TearA", new Vector4(ta.x, ta.y, 0f, 0f)); m.SetVector("_TearB", new Vector4(tb.x, tb.y, 0f, 0f)); m.SetFloat("_TearW", tw);
            EditorUtility.SetDirty(m);
        }
        var hole = b.hole; if (hole) { var ring = hole.Find("Ring"); if (ring) ring.gameObject.SetActive(false); }   // 金の輪はなし
        var hz = g.GetComponent<FuwaHazard>();
        if (hz)
        {
            var hc = new GameObject("TearCenter").transform; hc.SetParent(g.transform, false);
            var dir = tb - ta; float half = dir.magnitude / 2f;
            hc.localPosition = new Vector3(mid.x, mid.y, 0f);
            hc.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);   // local x = 裂け目の向き
            hz.holeCenter = hc; hz.holeRadius = tw * 0.9f; hz.holeHalfLen = half; hz.holeBallFit = 1f;   // ギザギザの内がわ（0.82〜1倍）くらいで判定
            hz.sticky = true; hz.playerSlow = 0.6f; hz.playerSlowLinger = 0f; hz.tearWhenBig = false; hz.smallPasses = false;
            UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz);
        }
    }

    // スキマの門（2026-11-14 てじゃ「穴よりも、さっきみたいなクモの巣が複数あるとこのスキマを通す感じにしよ」）：四角い巣2枚の間に細いスキマ
    //  slits：門ごとに (たて=0/よこ=1, スキマの中心：たてなら x、よこなら 床からの y)。スキマのはば SlitW
    public static float SlitW = 0.8f;
    static void SlitGates(string tag, Vector3[] slits)
    {
        float z0 = P.z, len = slits.Length * WebGateGap;
        var g = new GameObject("SlitGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = WebXO, hw = SlitW / 2f;
        for (int i = 0; i < slits.Length; i++)
        {
            float z = z0 + WebGateGap * (i + 0.5f);
            gateZ.Add((isl, z));
            bool vertical = slits[i].x < 0.5f; float c = slits[i].y;
            var rects = vertical
                ? new[] { new Vector4(-xo, c - hw, 0f, hTop), new Vector4(c + hw, xo, 0f, hTop) }
                : new[] { new Vector4(-xo, xo, 0f, c - hw), new Vector4(-xo, xo, c + hw, hTop) };
            for (int k = 0; k < rects.Length; k++)
            {
                var r = rects[k];   // x0, x1, y0, y1
                if (r.y - r.x < 0.3f || r.w - r.z < 0.3f) continue;
                Web(g, "Web" + i + "_" + k, new Vector3((r.x + r.y) / 2f, P.y + (r.z + r.w) / 2f, z), Quaternion.identity, new Vector2(r.y - r.x, r.w - r.z), false, 0.6f, false, true);
            }
        }
        S(len);
    }

    // 小さい穴のあいた1枚の巣（2026-11-14 てじゃ「青カーテン以降の蜘蛛の巣、もっと小さいスキマを小さいふわふわで通り抜ける感じに」）
    //  hole：巣の中心から見た穴の中心、r：穴の半径。当たりはふわふわの半径込み（holeBallFit=1）＝小さいほど通りやすい、大きいのは通れない
    public static float SmallHoleR = 0.5f;   // てじゃ「ギリすぎるとムズすぎるかも」→ 直径1m（小さいは余裕、ふつうも通れる、大きいはほぼ無理）
    static void HoleWeb(Transform parent, string name, Vector3 lp, Vector2 size, Vector2 hole, float r)
    {
        var tmpl = Resources.FindObjectsOfTypeAll<SpiderWebBuilder>().FirstOrDefault(b => b && b.gameObject.scene.IsValid() && b.name == "BigWeb" && b.transform.parent && b.transform.parent.name == "Gimmick6_Window");
        if (!tmpl) { log.Append("（クモの巣の見本がない）"); return; }
        var g = Object.Instantiate(tmpl.gameObject, parent, false); g.name = name; g.SetActive(true);
        g.transform.localPosition = lp; g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
        var wh = g.GetComponent<FuwaWebHole>(); if (wh) Object.DestroyImmediate(wh);
        var b = g.GetComponent<SpiderWebBuilder>();
        // spokes 48：糸の先が四角のふちにのる。本数が少ないと角が大きく欠けた（当たりも欠けた）
        b.size = size; b.holeCenter = hole; b.holeRadius = r; b.spokes = 48; b.floorShadow = false; b.autoIsland = false;
        b.left.anchor = SpiderWebBuilder.AnchorType.None; b.right.anchor = SpiderWebBuilder.AnchorType.None; b.topThread = false;
        // 角まで四角くうめる：見本の BigWeb は polygonFrame（枠の多角形、角6つ）なので、そのままだと外形がひし形っぽくなって角があいた
        b.polygonFrame = false; b.outerMin = 0.99f; b.outerMax = 1f; b.cornerRound = 8f; b.spacingRef = 1.2f;
        b.assetId = "Web_2_5_" + parent.name + "_" + name;
        SpiderWebBuild.Build(b);
        var hz = g.GetComponent<FuwaHazard>();
        if (hz)
        {
            Transform hc = b.hole;
            if (!hc) { hc = new GameObject("HoleCenter").transform; hc.SetParent(g.transform, false); }
            hc.localPosition = new Vector3(hole.x, hole.y, 0f); hc.localRotation = Quaternion.identity;
            var ring = hc.Find("Ring"); if (ring) ring.localScale = Vector3.one * (r / 0.92f);   // 金の輪の内がわの半径 0.92（FuwaWebHole.ringInnerRadius）を穴にあわせる
            hz.holeCenter = hc; hz.holeRadius = r; hz.holeBallFit = 1f;
            hz.sticky = true; hz.playerSlow = 0.6f; hz.playerSlowLinger = 0f; hz.tearWhenBig = false; hz.smallPasses = false;
            UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz);
        }
    }
    // 小さい穴の巣の門：道いっぱいの巣に穴1つ。holes は門ごとの穴の中心（巣の中心から、x 右・y 上）。床は穴なし（狙う所なので）
    static void HoleGates(string tag, Vector2[] holes)
    {
        float z0 = P.z, len = holes.Length * WebGateGap;
        var g = new GameObject("HoleGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = WebXO;
        for (int i = 0; i < holes.Length; i++)
        {
            float z = z0 + WebGateGap * (i + 0.5f);
            gateZ.Add((isl, z));
            HoleWeb(g, "HoleWeb" + i, new Vector3(0f, P.y + hTop / 2f, z), new Vector2(2f * xo, hTop), holes[i], SmallHoleR);
        }
        S(len);
    }

    // ================= 崩れた参道の橋（2026-11-15 てじゃ「石像の島から手前の道へ、かつて橋があったんだろうな、って感じの残骸」「ジャンプで届かないように」）=================
    //  石像の正面（台地のふち）から、最初のアーチ橋（Bridge1）の横へ。道側の崩れ残りはアーチ橋に直角（つなぎ目をきれいに）、島側は台地のふちから道側の先っぽへ向ける
    //  すき間は約12m（ジャンプで届かない）。まん中に折れた橋脚とずり落ちた床のかけら（道より 3.2m 低く、道側の先から 5m 以上）、空中で止まった石（当たりなし）
    //  床は石（もう道じゃない）、当たりはレイヤー0（立てるけど道あつかいじゃない）
    public static float BrokenBridgeZ = 14f, BrokenRoadStub = 2.4f, BrokenIslandStub = 4f, BrokenStatueBack = 1.5f;   // 島側の床が橋台のうしろへ出る長さ（2026-11-15 てじゃ「直で橋つなぐのちがう、島の先端に橋、石像までは道」→ 1.5 にもどして、その先は StatueApproach の階段と道）
    // 橋のたもとから石像の台座まで：階段で台地へ下りる → シマシマの道 → 台座の段へ上がる短い階段（見た目だけ、当たりはレイヤー0）
    //  台座の正面の段（上の面 -0.55、台地から 0.9）は橋台のうしろ 4.9m から（Play で測った）
    public static float ApproachSlabD = 4.9f, ApproachSlabTop = -0.55f;   // ApproachSlabTop は道の高さ 0（＝橋が道の高さの時）から見た値
    public static bool BrokenIslandLow = true, StatueApproachPath = false, Pebbles = false, BrokenMidPier = false;   // Pebbles：地面・橋の上の石ころ（てじゃ「石ころもいらないかな」→ なし。空中で止まった石は残す）
    static void StatueApproach(Transform isb, float yTop, float W, float back)
    {
        var g = new GameObject("StatueApproach").transform; g.SetParent(isb, false);
        float pw = W - 0.5f;
        float drop = 0f - (yTop + 0.06f);
        int nDown = drop > 0.1f ? 5 : 0; float rise = nDown > 0 ? drop / nDown : 0f, run = 0.32f;   // 橋が台地の高さなら下りる階段はなし
        float z = -back;
        for (int k = 1; k <= nDown; k++)
        {
            float top = -rise * k, z0 = z - run;
            Box(g, "StepDown" + k, new Vector3(0f, (top + yTop - 0.3f) / 2f, (z + z0) / 2f), new Vector3(pw, top - (yTop - 0.3f), run), k % 2 == 0 ? pathA : pathB, 0);
            z = z0;
        }
        // 道（シマシマ）：地面に貼るだけ（2026-11-15 てじゃ「そこからは地面に道を貼るだけでいい」）。台座の正面の段の下まで少しもぐらせる
        float zEnd = -(ApproachSlabD + 0.1f);
        int si = 0;
        for (float zz = z; zz > zEnd + 0.01f; si++)
        {
            float z1 = Mathf.Max(zEnd, zz - StripeLen * 0.5f);
            Box(g, "Path" + si, new Vector3(0f, yTop + 0.06f - 0.15f, (zz + z1) / 2f), new Vector3(pw, 0.3f, zz - z1), si % 2 == 0 ? pathA : pathB, 0);
            zz = z1;
        }
    }

    static void BrokenBridge(Transform island)
    {
        var g = new GameObject("BrokenBridge").transform; g.SetParent(island, false);
        var sc = new Vector3(MushroomAt.x, 0f, MushroomAt.y);
        var f = Quaternion.Euler(0f, StatueYaw, 0f) * Vector3.forward; f.y = 0f; f.Normalize();
        // 台地のふち：石像の正面の向きに外へ、下向きレイで台地（道レイヤーの RockMesh）に当たる最後の所
        Physics.SyncTransforms();
        float tEdge = 16f, yTop = i1Top;
        for (float t = 8f; t < 30f; t += 0.1f)
        {
            var p = root.TransformPoint(sc + f * t + Vector3.up * 8f);
            if (Physics.Raycast(p, -root.up, out var h, 40f, 1 << pathLayer, QueryTriggerInteraction.Ignore) && h.collider.name == "RockMesh" && root.InverseTransformPoint(h.point).y > i1Top - 0.3f) { tEdge = t; yTop = root.InverseTransformPoint(h.point).y; }   // 2026-11-15 岩の横をゴツゴツにしたら、下の横の面に当たって橋が下へずれた → 台地の上の面だけ
        }
        var a = sc + f * (tEdge - 0.5f);                                  // 島側の橋台（ふちから少し内）
        float roadX = BridgeHW;                                           // アーチ橋の床のはし（ここから外へ）
        var tip = new Vector3(roadX + BrokenRoadStub, 0f, BrokenBridgeZ); // 道側の崩れ残りの先
        var dir = tip - a; dir.y = 0f; float L = dir.magnitude; dir /= L;
        const float W = 3.0f, T = 0.6f;
        var deckM = stone; var edgeM = terraD;

        // ---- 道側：アーチ橋に直角（+x）----
        var rs = new GameObject("RoadStub").transform; rs.SetParent(g, false);
        rs.localPosition = new Vector3(roadX, 0f, BrokenBridgeZ); rs.localRotation = Quaternion.LookRotation(Vector3.right);
        {
            float s = BrokenRoadStub;
            // 2026-11-15 てじゃ「キューブが抜けた感がやだ」→ 床の先はギザギザに割れたふち（FieldSlab）、ふちの上は少し欠け落ちる
            var rr = new System.Random(311);
            var dv = new List<Vector3>(); var dt = new List<int>(); var ev = new List<Vector3>(); var et = new List<int>();
            int nn = 15; var xs = Lin(-W / 2f, W / 2f, nn);
            var ends = Jag(rr, nn, s - 0.35f, 0.42f); var drops = new float[nn]; for (int k = 0; k < nn; k++) rr.NextDouble();   // 2026-11-15 てじゃ「上は平らがいい」→ 上のふちの欠けなし
            FieldSlab(dv, dt, Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, xs, ends, drops, -T, 0f);
            // 欄干の名ごり：左は先へいくほどガタガタに低く、右は根もとだけ
            foreach (float sx in new[] { -1f, 1f })
            {
                float len2 = sx < 0 ? s - 0.75f : 0.9f; int m2 = 9; var zz = Lin(0f, len2, m2); var hh = new float[m2]; var d2 = new float[m2];
                for (int k = 0; k < m2; k++) { float u = (float)k / (m2 - 1); hh[k] = Mathf.Lerp(0.3f, 0.05f + 0.1f * (float)rr.NextDouble(), Mathf.Clamp01((u - 0.45f) / 0.55f)) + (u > 0.45f ? ((float)rr.NextDouble() - 0.5f) * 0.08f : 0f); d2[k] = u > 0.45f ? (float)rr.NextDouble() * 0.05f : 0f; }
                FieldSlab(ev, et, new Vector3(sx * (W / 2f - 0.12f), 0f, 0f), Vector3.forward, Vector3.up, Vector3.right, zz, hh, d2, -0.12f, 0.12f);
            }
            if (Pebbles) for (int k = 0; k < 3; k++) RockChunk(dv, dt, new Vector3(((float)rr.NextDouble() - 0.5f) * 2f, 0.07f, s - 0.9f - 0.5f * (float)rr.NextDouble()), Vector3.one * (0.14f + 0.12f * (float)rr.NextDouble()), Quaternion.Euler((float)rr.NextDouble() * 360f, (float)rr.NextDouble() * 360f, 0f), rr);
            MeshObj(rs, "Deck", SaveMesh("BrokenRoadDeck", dv, dt), deckM, Vector3.zero, 0, true);
            MeshObj(rs, "Edges", SaveMesh("BrokenRoadEdges", ev, et), edgeM, Vector3.zero, 0, true);
            var dcol = rs.gameObject.AddComponent<BoxCollider>(); dcol.center = new Vector3(0f, -T / 2f, (s - 0.6f) / 2f); dcol.size = new Vector3(W, T, s - 0.6f);
            var cb = Box(rs, "Corbel", new Vector3(0f, -T - 0.7f, 0.45f), new Vector3(W - 0.5f, 1.4f, 0.9f), underM, 0); cb.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        }
        // ---- 島側：台地から立ち上がる橋台＋突き出して折れた床 ----
        var isb = new GameObject("IslandStub").transform; isb.SetParent(g, false);
        // 2026-11-15 てじゃ「橋自体を島の高さまでおとして島のフチまでにして」→ 島側の崩れ残りは台地の高さ（上の面が台地より 6cm 上）、床は台地のふちから
        float dyL = BrokenIslandLow ? yTop + 0.06f : 0f;
        isb.localPosition = new Vector3(a.x, dyL, a.z); isb.localRotation = Quaternion.LookRotation(dir);
        {
            float s = BrokenIslandStub;
            float yL = yTop - dyL;   // この中から見た台地の上の面
            // 2026-11-15 てじゃ「ちゃんと石像につないでほしい」→ 床を石像の台座の正面（BrokenStatueBack m うしろ）まで伸ばし、その下は台地から立ち上がる石の土手
            //  「橋の下の台がZファイト」→ 橋台の上の面と笠石の上の面が同じ高さだった：橋台は笠石の下まで
            float back = BrokenIslandLow ? 0f : BrokenStatueBack;
            float ab0 = yL - 1.5f, ab1 = -T - 0.24f;
            Box(isb, "Abutment", new Vector3(0f, (ab0 + ab1) / 2f, (-back + 0.7f) / 2f), new Vector3(W + 0.8f, ab1 - ab0, back + 0.7f), deckM, 0);
            Box(isb, "AbutmentCap", new Vector3(0f, -T - 0.12f, (-back + 0.8f) / 2f), new Vector3(W + 1.0f, 0.24f, back + 0.8f), edgeM, 0);
            var ri = new System.Random(733);
            var iv = new List<Vector3>(); var it = new List<int>(); var iev = new List<Vector3>(); var iet = new List<int>();
            { int nn = 15; var xs = Lin(-W / 2f, W / 2f, nn); var ends = Jag(ri, nn, s + back - 0.3f, 0.5f); var drops = new float[nn]; for (int k = 0; k < nn; k++) ri.NextDouble();
              FieldSlab(iv, it, new Vector3(0f, 0f, -back), Vector3.right, Vector3.forward, Vector3.up, xs, ends, drops, -T, 0f); }
            // 下の斜めの支え（アーチの名ごり）
            var st = Box(isb, "Strut", new Vector3(0f, -T - 0.9f, 1.0f), new Vector3(W - 0.6f, 0.6f, 2.4f), underM, 0); st.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float len2 = sx < 0 ? s + back - 0.9f : back + 1.5f; int m2 = 13; var zz = Lin(0f, len2, m2); var hh = new float[m2]; var d2 = new float[m2];
                for (int k = 0; k < m2; k++) { float u = (float)k / (m2 - 1); hh[k] = Mathf.Lerp(0.3f, 0.05f + 0.1f * (float)ri.NextDouble(), Mathf.Clamp01((u - 0.55f) / 0.45f)) + (u > 0.55f ? ((float)ri.NextDouble() - 0.5f) * 0.08f : 0f); d2[k] = u > 0.55f ? (float)ri.NextDouble() * 0.05f : 0f; }
                FieldSlab(iev, iet, new Vector3(sx * (W / 2f - 0.12f), 0f, -back), Vector3.forward, Vector3.up, Vector3.right, zz, hh, d2, -0.12f, 0.12f);
            }
            MeshObj(isb, "Deck", SaveMesh("BrokenIslandDeck", iv, it), deckM, Vector3.zero, 0, true);
            MeshObj(isb, "Edges", SaveMesh("BrokenIslandEdges", iev, iet), edgeM, Vector3.zero, 0, true);
            // 苔（横の面から少し外に出して重ならないように）
            foreach (float sx in new[] { -1f, 1f })
            { var mo = Box(isb, "Moss", new Vector3(sx * (W / 2f + 0.43f), ab1 - 0.55f, -0.6f + 0.3f * sx), new Vector3(0.06f, 0.7f, 1.1f), moss, 0); Object.DestroyImmediate(mo.GetComponent<Collider>()); }
            // 台地の上のがれき
            var rnd0 = new System.Random(77);
            var rbv = new List<Vector3>(); var rbt = new List<int>();
            for (int i = 0; i < 5; i++)
                RockChunk(rbv, rbt, new Vector3((i % 2 == 0 ? -1f : 1f) * (W / 2f + 0.9f + (float)rnd0.NextDouble() * 1.2f), yL + 0.1f, -0.8f - (float)rnd0.NextDouble() * 3.5f), Vector3.one * (0.3f + 0.3f * (float)rnd0.NextDouble()),
                    Quaternion.Euler((float)rnd0.NextDouble() * 360f, (float)rnd0.NextDouble() * 360f, 0f), rnd0);
            if (Pebbles) MeshObj(isb, "Rubble", SaveMesh("BrokenIslandRubble", rbv, rbt), deckM, Vector3.zero, 0, true);   // 2026-11-15 てじゃ「石ころもいらないかな」
            if (StatueApproachPath) StatueApproach(isb, yL, W, back);   // 2026-11-15 てじゃ「道無くていいかもな」→ なし
        }
        // ---- まん中：折れた橋脚＋ずり落ちた床のかけら ----
        var islandTip = a + dir * BrokenIslandStub;
        var mid = Vector3.Lerp(islandTip, tip, 0.45f);
        var md = new GameObject("MidPier").transform; md.SetParent(g, false);
        md.localPosition = new Vector3(mid.x, 0f, mid.z); md.localRotation = Quaternion.LookRotation(dir);
        {
            if (BrokenMidPier)   // 2026-11-15 てじゃ「この柱いらんね」→ なし（床のかけらは空中に浮いたまま）
            {
                Box(md, "Pier", new Vector3(0f, -7.5f, 0f), new Vector3(1.4f, 8f, 1.4f), underM, 0);
                var top = Box(md, "PierTop", new Vector3(0.15f, -3.35f, 0.1f), new Vector3(1.3f, 0.5f, 1.3f), deckM, 0); top.transform.localRotation = Quaternion.Euler(9f, 20f, -14f);
            }
            var rf = new System.Random(555);
            var fv = new List<Vector3>(); var ft = new List<int>();
            int nn = 13; var xs = Lin(-(W - 0.2f) / 2f, (W - 0.2f) / 2f, nn);
            foreach (float dirz in new[] { 1f, -1f })
            {
                var ends = Jag(rf, nn, 1.2f, 0.4f); var drops = new float[nn]; for (int k = 0; k < nn; k++) rf.NextDouble();
                FieldSlab(fv, ft, Vector3.zero, Vector3.right * dirz, Vector3.forward * dirz, Vector3.up, xs, ends, drops, -T, 0f);
            }
            var fo = MeshObj(md, "DeckFragment", SaveMesh("BrokenFragment", fv, ft), deckM, new Vector3(-0.3f, -2.7f, 0.2f), 0, true);
            fo.transform.localRotation = Quaternion.Euler(10f, 14f, -13f);
            var fc = fo.AddComponent<BoxCollider>(); fc.center = new Vector3(0f, -T / 2f, 0f); fc.size = new Vector3(W - 0.4f, T, 2f);
        }
        // ---- 空中で止まった石（当たりなし）----
        var rnd = new System.Random(4242);
        var fsv = new List<Vector3>(); var fst = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            float u = 0.1f + 0.8f * i / 7f;
            if (Mathf.Abs(u - 0.45f) < 0.08f) u += 0.1f;
            var p = Vector3.Lerp(islandTip, tip, u);
            var c = new Vector3(p.x, -1.2f - (float)rnd.NextDouble() * 1.8f, p.z) + Vector3.Cross(Vector3.up, dir) * (((float)rnd.NextDouble() - 0.5f) * 1.8f);
            float sz = 0.6f + 0.6f * (float)rnd.NextDouble();
            RockChunk(fsv, fst, c, new Vector3(sz * 1.3f, sz * 0.6f, sz), Quaternion.Euler((float)rnd.NextDouble() * 60f, (float)rnd.NextDouble() * 180f, (float)rnd.NextDouble() * 50f), rnd);
        }
        MeshObj(g, "FloatingStones", SaveMesh("BrokenFloatingStones", fsv, fst), deckM, Vector3.zero, 0, true);
        float gap = (tip - islandTip).magnitude;
        log.Append("崩れた橋 すき間" + gap.ToString("F1") + "m ");
    }

    // ================= 崩れた道のはし（2026-11-16）=================
    //  てじゃ「ポータルがある道の橋、ただのぶつぎりをやめたい。道は崩れていて、いくつかクリスタルが落ちてる感じ」「道の端ね」
    //  → ポータルのうしろの道のはしを：シマの板と下の石が段ちがいにガタガタに割れたふち＋折れて落ちていく床のかけら＋落ちていく石（橋だけ）＋落ちたクリスタル
    //  「ゴール前のはクリスタルだけ」→ WarpBIn は RuinCrystals だけ
    public static bool RuinEnds = true; public static float RuinPierBack = 0.8f, RuinCrystalScale = 1.3f;
    // name：入れ物の名前、o：道の上の面のはしのまん中（isl の座標）、fwd：外向き、hw：シマの板の半はば、xo：下の石の半はば、bridge：空中（かけら・石・クリスタルが落ちていく）か島の上（地面に転がる）
    static void RuinedEnd(string name, Vector3 o, Vector3 fwd, float hw, float xo, bool bridge, int seed)
    {
        var g = new GameObject(name).transform; g.SetParent(isl, false);
        g.localPosition = o; g.localRotation = Quaternion.LookRotation(fwd);
        var rnd = new System.Random(seed);
        const float Tk = 0.6f;
        // 2026-11-16 てじゃ「急に道が２層構造になったうえに細くなって欠けてるようにみえる」「２層にしなくていいし、欠け方はもっと荒くて良い」
        //  → 道と同じ厚み・同じはば・同じ色の1枚が、大きくガタガタに割れて終わる（点を少なく、ふれはばを大きく。上のふちも大きめに欠ける）
        var sv = new List<Vector3>(); var st = new List<int>();   // 石（橋の欄干の下の帯）
        var tv = new List<Vector3>(); var tt = new List<int>();   // 道
        int nn = 9;
        var eS = Jag(rnd, nn, 0.95f, 0.85f); for (int k = 0; k < nn; k++) eS[k] = Mathf.Clamp(eS[k], 0.12f, 1.9f);
        var dS = new float[nn]; for (int k = 0; k < nn; k++) dS[k] = (float)rnd.NextDouble() < 0.5f ? 0.04f + (float)rnd.NextDouble() * 0.16f : 0f;
        var xsR = Lin(-hw, hw, nn);
        FieldSlab(tv, tt, Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, xsR, eS, dS, -Tk, 0f);
        if (xo > hw + 0.01f) foreach (float sx in new[] { -1f, 1f })   // 橋：道の外（欄干の下）の帯も同じふちで
        {
            int ie = sx < 0 ? 0 : nn - 1;
            FieldSlab(sv, st, Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, sx < 0 ? new[] { -xo, -hw } : new[] { hw, xo }, sx < 0 ? new[] { eS[ie] * 0.7f, eS[ie] } : new[] { eS[ie], eS[ie] * 0.7f }, new[] { dS[ie], dS[ie] }, -Tk, 0f);
        }
        if (bridge)   // 橋：床の下の石（アーチの名ごり）がガタガタに垂れ下がって、下げた橋脚の上と道の下の台（Under）のはしをかくす
        {
            var hv = new List<Vector3>(); var ht = new List<int>();
            var eH = Jag(rnd, 13, 1.1f, 0.6f); for (int k = 0; k < 13; k++) eH[k] = Mathf.Clamp(eH[k], 0.35f, 2.0f);
            FieldSlab(hv, ht, new Vector3(0f, -Tk + 0.02f, 0f), Vector3.right, Vector3.down, Vector3.forward, Lin(-xo + 0.04f, xo - 0.04f, 13), eH, new float[13], -RuinPierBack - 0.1f, -0.05f);
            MeshObj(g, "HangingStone", SaveMesh(name + "_" + seed + "_Hang", hv, ht), stone, Vector3.zero, 0, true);
        }
        if (!bridge)   // 島の道：下の台（Under）も割れて終わる
        {
            var eU = Jag(rnd, 9, 0.35f, 0.3f); for (int k = 0; k < 9; k++) eU[k] = Mathf.Clamp(eU[k], 0.05f, 0.7f);
            var uv = new List<Vector3>(); var ut = new List<int>();
            FieldSlab(uv, ut, Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, Lin(-(hw - 0.15f), hw - 0.15f, 9), eU, new float[9], -Tk - 1.2f, -Tk);
            MeshObj(g, "UnderBroken", SaveMesh(name + "_" + seed + "_Under", uv, ut), underM, Vector3.zero, 0, true);
        }
        if (sv.Count > 0) MeshObj(g, "Stone", SaveMesh(name + "_" + seed + "_Stone", sv, st), stone, Vector3.zero, 0, true);
        MeshObj(g, "Road", SaveMesh(name + "_" + seed + "_Tiles", tv, tt), pathA, Vector3.zero, 0, true);
        { float eMin = eS.Min(); if (eMin > 0.05f) { var bc = g.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, -Tk / 2f, eMin / 2f); bc.size = new Vector3(2f * xo, Tk, eMin); } }

        // 折れたかけら（両がわが荒く割れた道の1枚。道と同じ厚み・色）
        float eAvg = eS.Average();
        Transform Fragment(string fn, float len, float w, int s2)
        {
            var r2 = new System.Random(s2);
            var fv = new List<Vector3>(); var ft = new List<int>();
            int m = 7; var xs = Lin(-w / 2f, w / 2f, m);
            foreach (float dz in new[] { 1f, -1f })
            {
                var e1 = Jag(r2, m, len / 2f, 0.45f); for (int k = 0; k < m; k++) e1[k] = Mathf.Max(0.15f, e1[k]);
                var d1 = new float[m]; for (int k = 0; k < m; k++) d1[k] = (float)r2.NextDouble() < 0.5f ? 0.05f + (float)r2.NextDouble() * 0.15f : 0f;
                FieldSlab(fv, ft, Vector3.zero, Vector3.right * dz, Vector3.forward * dz, Vector3.up, xs, e1, d1, -Tk, 0f);
            }
            var fr = new GameObject(fn).transform; fr.SetParent(g, false);
            MeshObj(fr, "Road", SaveMesh(name + "_" + seed + "_" + fn, fv, ft), pathA, Vector3.zero, 0, true);
            return fr;
        }
        var crys = new List<(Vector3 p, Vector3 e, float s, int x)>();
        // 床の上に落ちたクリスタル（ポータルのうしろ〜割れたふち）。寝かせて少しうめる
        crys.Add((new Vector3(-hw * 0.45f, 0.05f, -0.55f), new Vector3(0f, 35f, 82f), 0.24f, 0));
        crys.Add((new Vector3(hw * 0.5f, 0.03f, eAvg * 0.35f - 0.15f), new Vector3(10f, -60f, 76f), 0.18f, 0));
        // 割れたふちに刺さった大きめの塊
        crys.Add((new Vector3(hw * 0.12f, -0.05f, Mathf.Max(0.1f, eS.Min() - 0.05f)), new Vector3(-48f, 20f, 8f), 0.34f, 2));
        if (bridge)
        {
            var f1 = Fragment("FallingSlab", 1.6f, 2f * hw * 0.75f, seed + 1);
            f1.localPosition = new Vector3(-0.35f, -1.5f, eAvg + 1.4f); f1.localRotation = Quaternion.Euler(28f, 14f, -11f);
            var f2 = Fragment("FallingSlab2", 0.9f, 1.1f, seed + 2);
            f2.localPosition = new Vector3(0.9f, -3.4f, eAvg + 2.9f); f2.localRotation = Quaternion.Euler(-35f, 50f, 22f);
            // 落ちていく石（当たりなし）
            var rv = new List<Vector3>(); var rt = new List<int>();
            for (int i = 0; i < 7; i++)
            {
                float u = i / 6f;
                var c = new Vector3(((float)rnd.NextDouble() - 0.5f) * 2.6f * hw, -0.7f - u * 3.6f - (float)rnd.NextDouble() * 0.6f, eAvg + 0.6f + u * 3.2f + ((float)rnd.NextDouble() - 0.5f) * 0.8f);
                float sz = 0.22f + 0.35f * (float)rnd.NextDouble();
                RockChunk(rv, rt, c, new Vector3(sz * 1.3f, sz * 0.7f, sz), Quaternion.Euler((float)rnd.NextDouble() * 90f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 60f), rnd);
            }
            MeshObj(g, "FallingStones", SaveMesh(name + "_" + seed + "_Fall", rv, rt), stone, Vector3.zero, 0, true);
            // 石といっしょに落ちていくクリスタル
            crys.Add((new Vector3(-hw * 0.55f, -2.2f, eAvg + 2.0f), new Vector3(35f, 70f, 120f), 0.22f, 0));
            crys.Add((new Vector3(hw * 0.35f, -0.95f, eAvg + 0.75f), new Vector3(-20f, 10f, 55f), 0.16f, 0));
            crys.Add((new Vector3(0.2f, -4.4f, eAvg + 3.6f), new Vector3(60f, -30f, 15f), 0.2f, 1));
        }
        else
        {
            // 島の上：割れたふちの先で、折れたかけらが前へ傾いて地面にめりこむ（ふちにくっついたまま。島のはしでも宙に浮かない）
            var f1 = Fragment("SaggedSlab", 1.3f, 2f * hw * 0.7f, seed + 1);
            float pitch = 20f, half = 0.65f;
            f1.localPosition = new Vector3(0.1f, -0.16f - half * Mathf.Sin(pitch * Mathf.Deg2Rad), eAvg + 0.08f + half * Mathf.Cos(pitch * Mathf.Deg2Rad)); f1.localRotation = Quaternion.Euler(pitch, 9f, 4f);
            crys.Add((new Vector3(-hw * 0.4f, -0.3f, eAvg + 0.75f), new Vector3(25f, -40f, 80f), 0.2f, 0));
        }
        RuinCrystals(g, name + "_" + seed, crys);
    }
    // 落ちたクリスタル（p：位置、e：回転、s：大きさ、x：小さい結晶の数）。当たりなし
    static void RuinCrystals(Transform g, string key, List<(Vector3 p, Vector3 e, float s, int x)> list)
    {
        var cm = CrystalMat();
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            var o = MeshObj(g, "Crystal" + i, CrystalClusterMesh(key + "_Crys" + i, 300 + i * 17 + key.Length, c.s * RuinCrystalScale, c.x), cm, c.p, 0, false);
            o.transform.localRotation = Quaternion.Euler(c.e);
        }
    }

    // ================= バリア＋クリスタルの広場（2026-11-15 てじゃの案）=================
    //  「上段のワープ後に、なんらかのギミックでふわふわにバリア付与」「そのままちょっと進むと円形の広場、中央には浮いたクリスタル」
    //  「一定間隔で広場全体に放射状に衝撃波、プレイヤーは吹き飛ばされる、ほぼアウト」「ふわふわのバリアの周囲+後ろ側はセーフゾーン」
    //  バリアの門（ShieldGateArch）→ 広場（CrystalPlaza、FuwaShockwave）→ 出口でバリアが消える（ShieldOff）
    public static float PlazaR = 9f, PlazaRaise = 0.12f, PlazaLedge = 1.5f, CrystalH = 3f;
    const string GimAudio = "Assets/FuwaStage2/Audio/Gimmick2_5/";
    static readonly Color ShieldCol = new Color(0.45f, 0.9f, 1f, 1f);
    static FuwaShield shieldT;
    public static bool ShieldShatter = true;   // 床にふれたら跳ねる（3回でパリン）。false で床の判定ごとなし

    // ふわふわの子にバリアの球（なければ作る）。コースを作り直しても残る（ふわふわは FuwaSystem の物）
    static FuwaShield EnsureShield()
    {
        if (!ballT) return null;
        var tr = ballT.transform.Find("Shield");
        GameObject g;
        if (!tr)
        {
            g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name = "Shield";
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(ballT.transform, false);
        }
        else g = tr.gameObject;
        g.layer = ballT.gameObject.layer;
        g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one * 3f;
        string mp = "Assets/FuwaStage2/Materials/Ruins_Shield.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!m) { m = new Material(Shader.Find("Fuwa/Shield")); AssetDatabase.CreateAsset(m, mp); }
        m.enableInstancing = true; EditorUtility.SetDirty(m);
        var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.enabled = false;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var sh = g.GetComponent<FuwaShield>(); if (!sh) sh = UdonSharpUndo.AddComponent<FuwaShield>(g);
        sh.ball = ballT; sh.rend = mr; sh.baseScale = 3f; sh.spinFollow = 0.1f;
        sh.onSound = Sfx(g.transform, "OnSfx", "sfx_shield_on.wav", 0.8f, 0.5f, 20f);
        sh.offSound = Sfx(g.transform, "OffSfx", "sfx_shield_off.wav", 0.7f, 0.5f, 20f);
        sh.blockSound = Sfx(g.transform, "BlockSfx", "sfx_shield_block.wav", 0.8f, 0.5f, 25f);
        // 2026-11-15 てじゃ「バリアは地面に接触でパリンと割れる」：道レイヤーにふれたら割れる、かけら＋音
        sh.groundMask = ShieldShatter ? 1 << pathLayer : 0;   // 同日「割れなしでいいかもな」→「床についたらカキンって跳ねる、1回で黄、2回で赤、3回でパリン」
        sh.bouncesToBreak = 3;
        sh.bounceSound = Sfx(g.transform, "BounceSfx", "sfx_shield_bounce.wav", 0.85f, 0.5f, 25f);
        sh.shatterSound = Sfx(g.transform, "ShatterSfx", "sfx_shield_shatter.wav", 0.9f, 0.5f, 25f);
        { var old = g.transform.Find("ShatterFx"); if (old) Object.DestroyImmediate(old.gameObject); }
        var sfx = Sparks(g.transform, "ShatterFx", Vector3.zero, 0.75f, 3.2f, 0.09f, 80);
        { var pm = sfx.main; pm.loop = false; pm.gravityModifier = 0.9f; pm.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 4.2f); pm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
          pm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); pm.scalingMode = ParticleSystemScalingMode.Local;   // 親（バリア）のスケールは効かない
          var s = sfx.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.4f; s.radiusThickness = 0.1f; }
        sh.shatter = sfx;
        UdonSharpEditorUtility.CopyProxyToUdon(sh); FuwaStage2WaterTest.NoSync(sh);
        return sh;
    }
    static AudioSource Sfx(Transform parent, string name, string clip, float vol, float spatial, float maxD)
    {
        var tr = parent.Find(name); var g = tr ? tr.gameObject : new GameObject(name);
        g.transform.SetParent(parent, false);
        var au = g.GetComponent<AudioSource>(); if (!au) au = g.AddComponent<AudioSource>();
        au.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(GimAudio + clip); au.playOnAwake = false; au.loop = false; au.volume = vol;
        au.spatialBlend = spatial; au.rolloffMode = AudioRolloffMode.Linear; au.minDistance = 3f; au.maxDistance = maxD; au.dopplerLevel = 0f;
        return au;
    }

    // とがった結晶（上下の六角すい）。h1＝上の高さ、h0＝下の高さ、r＝胴の半径。面ごとに角ばった見た目
    static Mesh CrystalMesh(string name, float r, float h1, float h0, int sides = 6)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        var top = Vector3.up * h1; var bot = Vector3.down * h0;
        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
            var p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r); var p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            var mid = (p0 + p1) * 0.5f;
            Tri(v, t, top, p0, p1, mid.normalized + Vector3.up * 0.5f);
            Tri(v, t, bot, p0, p1, mid.normalized - Vector3.up * 0.5f);
        }
        return SaveMesh(name, v, t);
    }

    // ---- 2026-11-15 崩れの形（てじゃ「橋の崩れとかアーチの崩れも、キューブが抜けた感なのがやだな」）----
    //  FieldSlab：A 方向に並んだ点ごとに、B 方向へ e[i] まで伸びる板（C 方向の厚み c0〜c1）。e をガタガタにすると割れたふち、d[i] でふちの上（c1 側）を欠け落とす
    //  床の折れた先（A=横、B=前、C=上）・欄干の崩れ（A=前、B=上、C=横）などに使う
    static void FieldSlab(List<Vector3> v, List<int> t, Vector3 o, Vector3 axA, Vector3 axB, Vector3 axC, float[] a, float[] e, float[] d, float c0, float c1)
    {
        Vector3 Pt(float aa, float bb, float cc) => o + axA * aa + axB * bb + axC * cc;
        int n = a.Length;
        for (int i = 0; i + 1 < n; i++)
        {
            float a0 = a[i], a1 = a[i + 1], e0 = e[i], e1 = e[i + 1], d0 = d[i], d1 = d[i + 1];
            // 上（c1）・下（c0）
            Tri(v, t, Pt(a0, 0, c1), Pt(a1, 0, c1), Pt(a1, e1, c1 - d1), axC); Tri(v, t, Pt(a0, 0, c1), Pt(a1, e1, c1 - d1), Pt(a0, e0, c1 - d0), axC);
            Tri(v, t, Pt(a0, 0, c0), Pt(a1, 0, c0), Pt(a1, e1, c0), -axC); Tri(v, t, Pt(a0, 0, c0), Pt(a1, e1, c0), Pt(a0, e0, c0), -axC);
            // 割れたふち（B の先）
            var en = Vector3.Cross(axC, Pt(a1, e1, 0) - Pt(a0, e0, 0)); if (Vector3.Dot(en, axB) < 0) en = -en;
            Tri(v, t, Pt(a0, e0, c0), Pt(a1, e1, c0), Pt(a1, e1, c1 - d1), en); Tri(v, t, Pt(a0, e0, c0), Pt(a1, e1, c1 - d1), Pt(a0, e0, c1 - d0), en);
            // 根もと（B=0）
            Tri(v, t, Pt(a0, 0, c0), Pt(a1, 0, c0), Pt(a1, 0, c1), -axB); Tri(v, t, Pt(a0, 0, c0), Pt(a1, 0, c1), Pt(a0, 0, c1), -axB);
        }
        // 両はし（A の最初と最後）
        foreach (int k in new[] { 0, n - 1 })
        {
            var w = k == 0 ? -axA : axA;
            Tri(v, t, Pt(a[k], 0, c0), Pt(a[k], e[k], c0), Pt(a[k], e[k], c1 - d[k]), w); Tri(v, t, Pt(a[k], 0, c0), Pt(a[k], e[k], c1 - d[k]), Pt(a[k], 0, c1), w);
        }
    }
    // ガタガタの値：n 点、base のまわりに amp のばらつき（となりどうし少しなめらか）。両はしだけ endVal に固定したい時は fixEnds
    static float[] Jag(System.Random rnd, int n, float baseV, float amp, float fixEnds = float.NaN)
    {
        var r = new float[n]; float prev = 0f;
        for (int i = 0; i < n; i++) { float x = (float)rnd.NextDouble() * 2f - 1f; prev = prev * 0.35f + x * 0.65f; r[i] = baseV + amp * prev; }
        if (!float.IsNaN(fixEnds)) { r[0] = fixEnds; r[n - 1] = fixEnds; }
        return r;
    }
    static float[] Lin(float a0, float a1, int n) { var r = new float[n]; for (int i = 0; i < n; i++) r[i] = Mathf.Lerp(a0, a1, (float)i / (n - 1)); return r; }
    // 角ばった岩のかけら（ゆがめた二十面体）
    static void RockChunk(List<Vector3> v, List<int> t, Vector3 c, Vector3 size, Quaternion rot, System.Random rnd)
    {
        float g = (1f + Mathf.Sqrt(5f)) / 2f;
        var p = new[] { new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0), new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g), new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1) };
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var q = new Vector3[12];
        for (int i = 0; i < 12; i++) { var d = p[i].normalized * (0.75f + 0.4f * (float)rnd.NextDouble()); q[i] = c + rot * Vector3.Scale(d, size * 0.5f); }
        for (int i = 0; i < f.Length; i += 3) Tri(v, t, q[f[i]], q[f[i + 1]], q[f[i + 2]], (q[f[i]] + q[f[i + 1]] + q[f[i + 2]]) / 3f - c);
    }

    // ---- 2026-11-15 見た目クオリティ（4）：溝のある古い柱 ----
    //  kind 0：完全（土台＋胴＋柱頭）、1：折れ（土台＋胴、上がギザギザ）、2：切り株（短い折れ）、3：倒れた胴のかけら（胴だけ、上がギザギザ）
    //  原点は根もとのまん中（kind 3 は胴の下の面）。サブメッシュ 0＝胴（テラコッタ）、1＝土台・柱頭（石）
    static void LatheInto(List<Vector3> v, List<int> t, float[][] prof, int seg)   // prof: (r, z) を下から
    {
        for (int k = 0; k + 1 < prof.Length; k++)
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 p00 = d0 * prof[k][0] + Vector3.up * prof[k][1], p01 = d1 * prof[k][0] + Vector3.up * prof[k][1];
                Vector3 p10 = d0 * prof[k + 1][0] + Vector3.up * prof[k + 1][1], p11 = d1 * prof[k + 1][0] + Vector3.up * prof[k + 1][1];
                var mid = (d0 + d1) * 0.5f;
                var o = mid * (prof[k + 1][1] - prof[k][1]) + Vector3.up * (prof[k][0] - prof[k + 1][0]);
                if (o.sqrMagnitude < 1e-8f) o = mid;
                Tri(v, t, p00, p01, p11, o); Tri(v, t, p00, p11, p10, o);
            }
    }
    static void ChamferBoxInto(List<Vector3> v, List<int> t, Vector3 c, Vector3 sz, float ch)
    {   // 上のふちを面取りした箱（底なし）
        float hx = sz.x / 2f, hz = sz.z / 2f, y0 = c.y - sz.y / 2f, y1 = c.y + sz.y / 2f;
        var off = new Vector3(c.x, 0f, c.z);
        var lo = new[] { new Vector3(-hx, y0, -hz), new Vector3(hx, y0, -hz), new Vector3(hx, y0, hz), new Vector3(-hx, y0, hz) };
        var mi = new[] { new Vector3(-hx, y1 - ch, -hz), new Vector3(hx, y1 - ch, -hz), new Vector3(hx, y1 - ch, hz), new Vector3(-hx, y1 - ch, hz) };
        var up = new[] { new Vector3(-hx + ch, y1, -hz + ch), new Vector3(hx - ch, y1, -hz + ch), new Vector3(hx - ch, y1, hz - ch), new Vector3(-hx + ch, y1, hz - ch) };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4; var o = (lo[i] + lo[j]) * 0.5f; o.y = 0f;
            Tri(v, t, lo[i] + off, lo[j] + off, mi[j] + off, o); Tri(v, t, lo[i] + off, mi[j] + off, mi[i] + off, o);
            Tri(v, t, mi[i] + off, mi[j] + off, up[j] + off, o + Vector3.up * o.magnitude); Tri(v, t, mi[i] + off, up[j] + off, up[i] + off, o + Vector3.up * o.magnitude);
        }
        Tri(v, t, up[0] + off, up[1] + off, up[2] + off, Vector3.up); Tri(v, t, up[0] + off, up[2] + off, up[3] + off, Vector3.up);
        // 底（2026-11-15 てじゃ「柱、下から見るとスキマが」→ 下から見ると中が透けていた）
        Tri(v, t, lo[0] + off, lo[1] + off, lo[2] + off, Vector3.down); Tri(v, t, lo[0] + off, lo[2] + off, lo[3] + off, Vector3.down);
    }
    // 2026-11-15 軽量化：柱の遠い版（ColumnLite）。ふつうの版を作るたびに _Lite も作って覚えておき、ビルドの最後に LODGroup をつける
    public static bool ColumnLods = true; public static float ColumnLodScreen = 0.1f;
    static readonly Dictionary<Mesh, Mesh> columnLite = new Dictionary<Mesh, Mesh>();
    static Mesh ColumnMesh(string name, int seed, float h, int kind) => ColumnMesh(name, seed, h, kind, false);
    // 2026-11-15 軽量化：段ごとの影（FuwaTierShadows）。FarShadows に入った 2–5 の物をこちらへ移す
    public static bool TierShadows = true; public static float[] TierYs = { -161f, -145f };
    static void SetupTierShadows()
    {
        var far = Resources.FindObjectsOfTypeAll<FuwaFarShadows>().FirstOrDefault(f => f && f.name == "FarShadows" && f.gameObject.scene.IsValid());
        if (!far) { log.Append("（FarShadows がない）"); return; }
        var keepT = new List<Renderer>(); var keepC = new List<Vector3>(); var keepR = new List<float>();
        var mine = new List<Renderer>();
        for (int i = 0; i < far.targets.Length; i++)
        {
            var r = far.targets[i]; if (!r) continue;
            if (r.transform.IsChildOf(root)) mine.Add(r);
            else { keepT.Add(r); keepC.Add(far.centers[i]); keepR.Add(far.radii[i]); }
        }
        far.targets = keepT.ToArray(); far.centers = keepC.ToArray(); far.radii = keepR.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(far); EditorUtility.SetDirty(far);
        var old = root.Find("TierShadows"); if (old) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("TierShadows"); go.transform.SetParent(root, false);
        var ts = UdonSharpUndo.AddComponent<FuwaTierShadows>(go);
        ts.targets = mine.ToArray();
        ts.centers = mine.Select(r => r.bounds.center).ToArray();
        ts.radii = mine.Select(r => r.bounds.extents.magnitude).ToArray();
        ts.tiers = mine.Select(r => { float y = r.bounds.min.y; int t = 0; for (int k = 0; k < TierYs.Length; k++) if (y >= TierYs[k]) t = k + 1; return t; }).ToArray();
        ts.tierY = TierYs; ts.shadowDistance = far.shadowDistance; ts.perFrame = 120;
        UdonSharpEditorUtility.CopyProxyToUdon(ts); FuwaStage2WaterTest.NoSync(ts);
        log.Append("段ごとの影 " + mine.Count + "（段0 " + ts.tiers.Count(x => x == 0) + "・段1 " + ts.tiers.Count(x => x == 1) + "・段2 " + ts.tiers.Count(x => x == 2) + "） ");
    }

    // ビルドの最後：ふつうの版の柱に「遠い版」の子と LODGroup（画面の高さ ColumnLodScreen より小さいと遠い版）
    static void AddColumnLods()
    {
        int n = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh || !columnLite.TryGetValue(mf.sharedMesh, out var lm) || !lm) continue;
            var r = mf.GetComponent<MeshRenderer>(); if (!r) continue;
            var old = mf.transform.Find("Lite"); if (old) Object.DestroyImmediate(old.gameObject);
            var lg0 = mf.GetComponent<LODGroup>(); if (lg0) Object.DestroyImmediate(lg0);
            var lo = new GameObject("Lite"); lo.transform.SetParent(mf.transform, false); lo.layer = mf.gameObject.layer;
            lo.AddComponent<MeshFilter>().sharedMesh = lm;
            var lr = lo.AddComponent<MeshRenderer>(); lr.sharedMaterials = r.sharedMaterials; lr.shadowCastingMode = r.shadowCastingMode; lr.receiveShadows = r.receiveShadows;
            var lg = mf.gameObject.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(ColumnLodScreen, new Renderer[] { r }), new LOD(0f, new Renderer[] { lr }) });
            lg.RecalculateBounds();
            n++;
        }
        columnLite.Clear();
        log.Append("柱の遠い版 " + n + " ");
    }
    static Mesh ColumnMesh(string name, int seed, float h, int kind, bool lite)
    {
        if (!lite && ColumnLods) { var lm = ColumnMesh(name + "_Lite", seed, h, kind, true); var fm = ColumnMesh(name, seed, h, kind, true, false); columnLite[fm] = lm; return fm; }
        return ColumnMesh(name, seed, h, kind, lite, false);
    }
    static Mesh ColumnMesh(string name, int seed, float h, int kind, bool lite, bool _)
    {
        var rnd = new System.Random(seed);
        var sv = new List<Vector3>(); var stt = new List<int>();   // 胴（なめらか）
        var fv = new List<Vector3>(); var ft = new List<int>();    // 胴のふた（平ら・胴の材質）
        var bv = new List<Vector3>(); var bt = new List<int>();    // 土台・柱頭（平ら・石）
        int Seg = lite ? 20 : 80; const int Flutes = 20;   // 1本の溝に4つ（溝と細い山がはっきり見えるように）。遠い版は溝なし 20 角
        int lat = lite ? 10 : 24;
        float z0 = 0f;
        if (kind != 3)
        {
            ChamferBoxInto(bv, bt, new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.2f, 1.1f), 0.04f);
            LatheInto(bv, bt, new[] { new[] { 0.5f, 0.2f }, new[] { 0.53f, 0.25f }, new[] { 0.5f, 0.31f }, new[] { 0.43f, 0.34f }, new[] { 0.39f, 0.36f } }, lat);
            z0 = 0.36f;
            // 2026-11-15 てじゃ「柱の根本ニスキマ」：土台の輪の上（半径 0.39）があいていて、胴の溝（半径 0.33〜0.37）とのすき間から中が見えた → 輪の上にふた
            for (int i = 0; i < lat; i++)
            {
                float a0 = Mathf.PI * 2f * i / lat, a1 = Mathf.PI * 2f * (i + 1) / lat;
                Tri(bv, bt, new Vector3(Mathf.Cos(a0) * 0.395f, z0, Mathf.Sin(a0) * 0.395f), new Vector3(Mathf.Cos(a1) * 0.395f, z0, Mathf.Sin(a1) * 0.395f), new Vector3(0f, z0, 0f), Vector3.up);
            }
        }
        bool full = kind == 0;
        float zs = full ? h - 0.45f : h;                   // 胴のてっぺん（折れなら一番高い所の目安）
        float ja = (float)rnd.NextDouble() * 6.28f, jb = (float)rnd.NextDouble() * 6.28f, jc = (float)rnd.NextDouble() * 6.28f;
        // 2026-11-15 てじゃ「道の柱も、折れてるところギザギザすぎる、もっとゆるやかに」→ 細かい波（7・13）をやめて、ゆるい斜め＋小さいうねり
        float jagAmp = kind == 3 ? 0.13f : (kind == 2 ? 0.15f : 0.22f);
        if (ColumnFlatBreak) jagAmp *= 1.3f;   // 平面なのでかたむきを少し大きく
        System.Func<float, float> Jag = th => Mathf.Clamp(jagAmp * (0.5f + 0.42f * Mathf.Sin(th + ja) + 0.1f * Mathf.Sin(2f * th + jb) + 0.03f * Mathf.Sin(3f * th + jc)), 0f, jagAmp * 1.25f);
        System.Func<float, float, float> Rad = (z, th) =>
        {
            float u = Mathf.Clamp01((z - z0) / 4.2f);
            float rb = Mathf.Lerp(0.37f, 0.32f, u) + 0.012f * Mathf.Sin(Mathf.PI * u);
            if (lite) return rb * 0.94f;   // 遠い版：溝なし（溝の平均くらいの太さ）
            float c = Mathf.Abs(Mathf.Sin(Flutes * th * 0.5f));      // 0＝溝の間の細い山、1＝溝の底
            float r = rb * (1f - 0.1f * Mathf.Pow(c, 0.6f));
            float jz = (z - z0) % 1.1f; if (jz < 0.05f && z - z0 > 0.2f) r *= 0.93f;   // 石の継ぎ目
            return r;
        };
        var zl = new List<float>();
        float topRegular = full ? zs : zs - jagAmp * 1.3f;
        for (float z = z0; z < topRegular - 0.05f; z += lite ? 2.2f : ColumnRingStep) zl.Add(z);   // 2026-11-15 軽量化：0.25 → ColumnRingStep（ふくらみはゆるいので石の継ぎ目の輪＋ときどき）
        if (!lite) for (float j = 1.1f; z0 + j + 0.05f < topRegular; j += 1.1f) { zl.Add(z0 + j - 0.001f); zl.Add(z0 + j + 0.001f); zl.Add(z0 + j + 0.049f); zl.Add(z0 + j + 0.051f); }
        zl.Add(topRegular); zl.Sort();
        int rings = zl.Count;
        bool jag = !full;
        int totalRings = rings + (jag ? 1 : 0);
        for (int k = 0; k < totalRings; k++)
            for (int i = 0; i < Seg; i++)
            {
                float th = Mathf.PI * 2f * i / Seg;
                float z = k < rings ? zl[k] : zs - Jag(th);
                float r = Rad(z, th) * (k == rings && jag ? 0.97f : 1f);
                if (k == rings && jag && ColumnFlatBreak)
                {   // 2026-11-15 てじゃ「柱の断面のこのすじ消せない？」→ 折れ口を1枚のかたむいた平面に（点の位置で高さを決める＝扇の三角がみんな同じ面の向き）
                    r = Rad(zs, th) * 0.97f;
                    float px = Mathf.Cos(th) * r, pz = Mathf.Sin(th) * r;
                    z = zs - jagAmp * (0.55f + 0.45f * (px * Mathf.Cos(ja) + pz * Mathf.Sin(ja)) / 0.36f);
                    sv.Add(new Vector3(px, z, pz));
                    continue;
                }
                sv.Add(new Vector3(Mathf.Cos(th) * r, z, Mathf.Sin(th) * r));
            }
        for (int k = 0; k + 1 < totalRings; k++)
            for (int i = 0; i < Seg; i++)
            {
                int j = (i + 1) % Seg;
                int a0 = k * Seg + i, a1 = k * Seg + j, b0 = (k + 1) * Seg + i, b1 = (k + 1) * Seg + j;
                stt.Add(a0); stt.Add(b1); stt.Add(a1); stt.Add(a0); stt.Add(b0); stt.Add(b1);
            }
        int last = (totalRings - 1) * Seg;
        if (jag)
        {   // ギザギザのふち → 少しへこんだまん中（面ごとに角ばる）
            var cen = ColumnFlatBreak ? new Vector3(0f, zs - jagAmp * 0.55f, 0f) : new Vector3(0.04f, zs - jagAmp * 0.6f, -0.03f);   // 平面の上（まん中の高さ）
            for (int i = 0; i < Seg; i++) { int j = (i + 1) % Seg; Tri(fv, ft, sv[last + i], sv[last + j], cen, Vector3.up); }
        }
        else
        {
            var cen = Vector3.up * zs;
            for (int i = 0; i < Seg; i++) { int j = (i + 1) % Seg; Tri(fv, ft, sv[last + i], sv[last + j], cen, Vector3.up); }
            LatheInto(bv, bt, new[] { new[] { 0.32f, zs }, new[] { 0.35f, zs + 0.06f }, new[] { 0.44f, zs + 0.17f }, new[] { 0.5f, zs + 0.24f }, new[] { 0.42f, zs + 0.25f } }, lat);
            ChamferBoxInto(bv, bt, new Vector3(0f, zs + 0.25f + 0.1f, 0f), new Vector3(1.08f, 0.2f, 1.08f), 0.035f);
        }
        if (kind == 3) for (int i = 0; i < Seg; i++) { int j = (i + 1) % Seg; Tri(fv, ft, sv[i], sv[j], Vector3.up * z0, Vector3.down); }
        var allV = new List<Vector3>(sv); int fo = allV.Count; allV.AddRange(fv); int bo = allV.Count; allV.AddRange(bv);
        var t0 = new List<int>(stt); t0.AddRange(ft.Select(x => x + fo));
        var t1 = bt.Select(x => x + bo).ToList();
        string path = MeshDir + "/" + name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!ex) { ex = new Mesh { name = name }; AssetDatabase.CreateAsset(ex, path); }
        ex.Clear(false);
        ex.SetVertices(allV); ex.subMeshCount = 2; ex.SetTriangles(t0, 0); ex.SetTriangles(t1, 1);
        ex.RecalculateNormals(); ex.RecalculateBounds(); EditorUtility.SetDirty(ex);
        return ex;
    }

    // ---- 2026-11-15 見た目クオリティ：結晶の塊・祭壇（てじゃ「見た目クオリティあげてくか」）----
    // 六角柱の両はしがとがった結晶1本（平らな面）。p0＝根もと、axis＝伸びる向き、r＝太さ、body＝胴の長さ、tipA＝先のとがり、tipB＝根もとのとがり（0 なら平ら）
    static void CrystalPrism(List<Vector3> v, List<int> t, Vector3 p0, Vector3 axis, float r, float body, float tipA, float tipB, System.Random rnd, int sides = 6)
    {
        axis.Normalize();
        var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        var side2 = Vector3.Cross(axis, side);
        float rot = (float)rnd.NextDouble() * Mathf.PI * 2f;
        var ringA = new Vector3[sides]; var ringB = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = rot + Mathf.PI * 2f * i / sides;
            float jr = r * (0.88f + 0.24f * (float)rnd.NextDouble());
            var d = (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * jr;
            ringB[i] = p0 + d;
            ringA[i] = p0 + axis * body + d * (0.92f + 0.1f * (float)rnd.NextDouble());
        }
        var tipP = p0 + axis * (body + tipA) + (side * ((float)rnd.NextDouble() - 0.5f) + side2 * ((float)rnd.NextDouble() - 0.5f)) * r * 0.25f;
        var botP = p0 - axis * tipB;
        var cA = p0 + axis * body; var cB = p0;
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            var outw = ((ringA[i] + ringA[j] + ringB[i] + ringB[j]) * 0.25f - (cA + cB) * 0.5f);
            Tri(v, t, ringB[i], ringB[j], ringA[j], outw); Tri(v, t, ringB[i], ringA[j], ringA[i], outw);
            Tri(v, t, ringA[i], ringA[j], tipP, (ringA[i] + ringA[j]) * 0.5f - cA + axis * r);
            if (tipB > 0.001f) Tri(v, t, ringB[i], ringB[j], botP, (ringB[i] + ringB[j]) * 0.5f - cB - axis * r);
            else Tri(v, t, ringB[i], ringB[j], cB, -axis);
        }
    }
    // 結晶の塊：大きい1本（上下にとがる）＋まわりに小さいのを extra 本、斜めに生やす（浮いているので下向きのもある）
    static Mesh CrystalClusterMesh(string name, int seed, float s, int extra)
    {
        var v = new List<Vector3>(); var t = new List<int>(); var rnd = new System.Random(seed);
        float r = 0.42f * s, bodyL = 0.95f * s;
        CrystalPrism(v, t, Vector3.down * 0.45f * s, Vector3.up + new Vector3(0.05f, 0f, 0.03f), r, bodyL, 0.7f * s, 0.6f * s, rnd);
        for (int k = 0; k < extra; k++)
        {
            float a = Mathf.PI * 2f * (k + 0.3f * (float)rnd.NextDouble()) / extra;
            var outw = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            bool down = k % 3 == 2;
            float h = down ? -0.35f * s : (-0.2f + 0.45f * (float)rnd.NextDouble()) * s;
            var axis = (outw * (0.7f + 0.3f * (float)rnd.NextDouble()) + Vector3.up * (down ? -0.75f : 0.55f + 0.4f * (float)rnd.NextDouble())).normalized;
            float rr = (0.13f + 0.1f * (float)rnd.NextDouble()) * s;
            var p0 = outw * r * 0.45f + Vector3.up * h;   // 大きい結晶の中から生やす（すき間なし）
            CrystalPrism(v, t, p0, axis, rr, (0.35f + 0.3f * (float)rnd.NextDouble()) * s, (0.22f + 0.15f * (float)rnd.NextDouble()) * s, 0f, rnd);
        }
        return SaveMesh(name, v, t);
    }
    static Material CrystalMat()
    {
        var m = FxMat("Ruins_CrystalFacet", "Fuwa/Crystal");
        m.SetColor("_Color", new Color(0.62f, 0.95f, 1f)); m.SetColor("_Deep", new Color(0.12f, 0.38f, 0.75f));
        m.SetColor("_EmissionColor", ShieldCol * 0.45f); EditorUtility.SetDirty(m);
        return m;
    }

    // 祭壇：八角形の3段（角が少し欠けた不ぞろいな形）、中段の上にルーンの溝（溝の底は rune で光る）、上の段にクリスタルを支えていた石の爪
    static (Mesh stone, Mesh rune) AltarMesh(string name)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        var rv = new List<Vector3>(); var rt = new List<int>();
        var rnd = new System.Random(808);
        const int N = 8;
        float rot = Mathf.PI / 8f;
        System.Func<int, float, Vector3> Dir8 = (i, r) => { float a = rot + Mathf.PI * 2f * i / N; return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); };
        // 段：下の半径 r、高さ y0〜y1、上のふちを面取り c。角ごとに欠け（半径をばらつかせる）。上の面は rInner まで
        void Tier(float r, float y0, float y1, float c, float rInner)
        {
            var jit = new float[N]; for (int i = 0; i < N; i++) jit[i] = 0.94f + 0.06f * (float)rnd.NextDouble();
            jit[rnd.Next(N)] *= 0.9f;
            if (rInner > 0.001f) for (int i = 0; i < N; i++) jit[i] = Mathf.Max(jit[i], (rInner + 0.04f) / (r - c));
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                Vector3 a0 = Dir8(i, r * jit[i]), a1 = Dir8(j, r * jit[j]);
                Vector3 b0 = Dir8(i, (r - c) * jit[i]), b1 = Dir8(j, (r - c) * jit[j]);
                var o = (a0 + a1) * 0.5f;
                Tri(v, t, a0 + Vector3.up * y0, a1 + Vector3.up * y0, a1 + Vector3.up * (y1 - c), o); Tri(v, t, a0 + Vector3.up * y0, a1 + Vector3.up * (y1 - c), a0 + Vector3.up * (y1 - c), o);
                Tri(v, t, a0 + Vector3.up * (y1 - c), a1 + Vector3.up * (y1 - c), b1 + Vector3.up * y1, o + Vector3.up * o.magnitude); Tri(v, t, a0 + Vector3.up * (y1 - c), b1 + Vector3.up * y1, b0 + Vector3.up * y1, o + Vector3.up * o.magnitude);
                Vector3 c0 = Dir8(i, rInner), c1 = Dir8(j, rInner);
                if (rInner > 0.001f) { Tri(v, t, b0 + Vector3.up * y1, b1 + Vector3.up * y1, c1 + Vector3.up * y1, Vector3.up); Tri(v, t, b0 + Vector3.up * y1, c1 + Vector3.up * y1, c0 + Vector3.up * y1, Vector3.up); }
                else Tri(v, t, b0 + Vector3.up * y1, b1 + Vector3.up * y1, Vector3.up * y1, Vector3.up);
            }
        }
        Tier(1.62f, 0f, 0.28f, 0.06f, 0f);
        float gy = 0.62f, gd = 0.05f, g0 = 1.04f, g1 = 1.18f;
        Tier(1.32f, 0.28f, gy, 0.05f, g1);
        // 溝：外の壁・内の壁（石）、底（光る）、溝の内がわの上の面（石。上の段の下にかくれる所もあるが向きが同じで重ならない高さ）
        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N;
            Vector3 o0 = Dir8(i, g1), o1 = Dir8(j, g1), i0 = Dir8(i, g0), i1 = Dir8(j, g0);
            Tri(v, t, o0 + Vector3.up * gy, o1 + Vector3.up * gy, o1 + Vector3.up * (gy - gd), -(o0 + o1));
            Tri(v, t, o0 + Vector3.up * gy, o1 + Vector3.up * (gy - gd), o0 + Vector3.up * (gy - gd), -(o0 + o1));
            Tri(v, t, i0 + Vector3.up * gy, i1 + Vector3.up * gy, i1 + Vector3.up * (gy - gd), i0 + i1);
            Tri(v, t, i0 + Vector3.up * gy, i1 + Vector3.up * (gy - gd), i0 + Vector3.up * (gy - gd), i0 + i1);
            Tri(rv, rt, o0 + Vector3.up * (gy - gd), o1 + Vector3.up * (gy - gd), i1 + Vector3.up * (gy - gd), Vector3.up);
            Tri(rv, rt, o0 + Vector3.up * (gy - gd), i1 + Vector3.up * (gy - gd), i0 + Vector3.up * (gy - gd), Vector3.up);
            // 溝の内がわ（g0〜上の段のふち 1.0）の上の面
            Vector3 k0 = Dir8(i, 0.8f), k1 = Dir8(j, 0.8f);   // 上の段の下にかくれるところまで
            Tri(v, t, i0 + Vector3.up * gy, i1 + Vector3.up * gy, k1 + Vector3.up * gy, Vector3.up); Tri(v, t, i0 + Vector3.up * gy, k1 + Vector3.up * gy, k0 + Vector3.up * gy, Vector3.up);
        }
        Tier(1.0f, gy, 0.84f, 0.05f, 0f);
        // 石の爪 4本（上の段のふちから立ち上がって内がわへかたむく、1本は折れている）
        for (int k = 0; k < 4; k++)
        {
            float a = Mathf.PI / 4f + Mathf.PI / 2f * k;
            var outw = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var tan = new Vector3(-outw.z, 0f, outw.x);
            bool broken = k == 2;
            float h = broken ? 0.4f : 0.8f + 0.12f * (float)rnd.NextDouble();   // クリスタルにはふれない（浮いている感じを残す）
            var b = outw * 0.74f + Vector3.up * 0.84f;
            var top = outw * (broken ? 0.68f : 0.5f) + Vector3.up * (0.84f + h);
            float w0 = 0.16f, w1 = broken ? 0.13f : 0.05f, d0 = 0.2f, d1 = broken ? 0.16f : 0.07f;
            var q = new Vector3[8];
            q[0] = b - tan * w0 - outw * d0; q[1] = b + tan * w0 - outw * d0; q[2] = b + tan * w0 + outw * d0; q[3] = b - tan * w0 + outw * d0;
            q[4] = top - tan * w1 - outw * d1; q[5] = top + tan * w1 - outw * d1; q[6] = top + tan * w1 + outw * d1; q[7] = top - tan * w1 + outw * d1;
            var cen = (b + top) * 0.5f;
            int[][] faces = { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }, new[] { 4, 5, 6, 7 } };
            foreach (var fc in faces)
            {
                var fcen = (q[fc[0]] + q[fc[1]] + q[fc[2]] + q[fc[3]]) * 0.25f;
                Tri(v, t, q[fc[0]], q[fc[1]], q[fc[2]], fcen - cen); Tri(v, t, q[fc[0]], q[fc[2]], q[fc[3]], fcen - cen);
            }
        }
        return (SaveMesh(name + "_Stone", v, t), SaveMesh(name + "_Rune", rv, rt));
    }

    // uv・法線つきで保存（輪・帯用）
    static Mesh SaveMeshUV(string name, List<Vector3> v, List<int> t, List<Vector2> uv, List<Vector3> n)
    {
        string path = MeshDir + "/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!m) { m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.Clear(false);
        m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
        if (n != null) m.SetNormals(n); else m.RecalculateNormals();
        m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }
    // 輪（上向きの面）：r0〜r1、a0〜a1（ラジアン）、高さ y。uv.y は外のふち 0 → 内のふち 1。side＝外のふちの側面を下へ（高さ）
    static void AnnulusInto(List<Vector3> v, List<int> t, List<Vector2> uv, float r0, float r1, float a0, float a1, float y, float side, int seg)
    {
        for (int i = 0; i < seg; i++)
        {
            float aa = Mathf.Lerp(a0, a1, (float)i / seg), ab = Mathf.Lerp(a0, a1, (float)(i + 1) / seg);
            Vector3 da = new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa)), db = new Vector3(Mathf.Cos(ab), 0f, Mathf.Sin(ab));
            int k = v.Count;
            v.Add(da * r0 + Vector3.up * y); v.Add(da * r1 + Vector3.up * y); v.Add(db * r1 + Vector3.up * y); v.Add(db * r0 + Vector3.up * y);
            uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1));
            // 上向き（反時計回りの角度で a が増える → 外→次の外の順で上向き）
            t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 3); t.Add(k + 2);
            if (side > 0f)
            {
                int s = v.Count;
                v.Add(da * r1 + Vector3.up * y); v.Add(db * r1 + Vector3.up * y); v.Add(db * r1 + Vector3.up * (y - side)); v.Add(da * r1 + Vector3.up * (y - side));
                uv.Add(Vector2.zero); uv.Add(Vector2.zero); uv.Add(Vector2.zero); uv.Add(Vector2.zero);
                t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            }
        }
    }
    static void FixWinding(List<Vector3> v, List<int> t, System.Func<Vector3, Vector3> want)
    {
        for (int i = 0; i < t.Count; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), want((a + b + c) / 3f)) < 0f) { int s = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = s; }
        }
    }
    static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat, Vector3 lp, int layer, bool shadows)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.layer = layer;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        if (!shadows) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }
        return g;
    }
    static Material GlowMat(string name, Color c, float e)
    {
        var m = FuwaStage2WaterTest.ToonMat(name, Color.Lerp(c, Color.white, 0.35f));
        m.SetColor("_EmissionColor", c * e); EditorUtility.SetDirty(m);
        return m;
    }
    static Material FxMat(string name, string shader)
    {
        string mp = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, mp); }
        m.enableInstancing = true; EditorUtility.SetDirty(m);
        return m;
    }
    static ParticleSystem Sparks(Transform parent, string name, Vector3 lp, float life, float speed, float size, int max)
    {
        var ps = new GameObject(name).AddComponent<ParticleSystem>(); ps.transform.SetParent(parent, false); ps.transform.localPosition = lp;
        var main = ps.main; main.playOnAwake = false; main.loop = true; main.startLifetime = life; main.startSpeed = speed; main.startSize = size;
        main.startColor = Color.Lerp(ShieldCol, Color.white, 0.35f); main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Rune_Glyph_-1.mat");
        ps.Stop();
        return ps;
    }

    // バリアの門：崩れた石のアーチ＋てっぺんに浮いた小さいクリスタル。くぐると FuwaShieldGate がバリアをつける。当たりは道のはばより広く・高く（よけられない）
    static void ShieldGateArch(string name, bool give = true, float cxIn = -1f)
    {
        // 2026-11-15 てじゃ「反対側は広場ギリギリにバリア解除のアーチ置こう」→ give=false で解除の門（色ちがい、アセット名も別）
        string pre = give ? "Gate" : "OffGate"; Color gcol = give ? ShieldCol : ShieldOffCol;
        // 2026-11-15 見た目クオリティ（5）：四角い門 → 溝のある柱2本＋半円の石のアーチ（くさび石）、要石の上に石の爪で支えた小さいクリスタル、要石に光る石
        float cx = cxIn > 0f ? cxIn : HW + 0.8f, colH = 3.6f, rIn = cx - 0.45f, rOut = cx + 0.45f;
        var g = new GameObject(name).transform; g.SetParent(isl, false); g.localPosition = new Vector3(0f, P.y, P.z); g.gameObject.layer = 2;
        foreach (float sx in new[] { -1f, 1f })
        {
            var co = new GameObject(sx < 0 ? "ColumnL" : "ColumnR"); co.transform.SetParent(g, false); co.transform.localPosition = new Vector3(sx * cx, 0f, 0f);
            co.transform.localRotation = Quaternion.identity;   // 2本の向きをそろえる
            co.AddComponent<MeshFilter>().sharedMesh = ColumnMesh(pre + "Column" + (sx < 0 ? "L" : "R"), sx < 0 ? 31 : 32, colH, 0);
            co.AddComponent<MeshRenderer>().sharedMaterials = new[] { terra, stone };
            var cc = co.AddComponent<CapsuleCollider>(); cc.radius = 0.4f; cc.height = colH; cc.center = Vector3.up * colH / 2f;
        }
        // くさび石のアーチ（半円、要石はひとまわり大きく上へ出る）
        Transform keyT;
        {
            var v = new List<Vector3>(); var t = new List<int>(); var kv = new List<Vector3>(); var kt = new List<int>();
            var rnd = new System.Random(55);
            int n = 13; float d = 0.5f, y0 = colH;
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n;
                bool key = i == n / 2;
                float ro = rOut * (key ? 1.12f : 0.97f + 0.05f * (float)rnd.NextDouble());
                float dd = key ? d + 0.08f : d;
                Vector3 P0(float a, float r, float z) => new Vector3(Mathf.Cos(a) * r, y0 + Mathf.Sin(a) * r, z);
                if (i == 3)
                {   // 2026-11-15 てじゃ「キューブが抜けた感がやだ」→ 1つだけ短いくさび石 → 外がわがガタガタに欠けたくさび石
                    int m = 6; var outer = new Vector3[m]; var ofr = new Vector3[m];
                    for (int k = 0; k < m; k++)
                    {
                        float aa = Mathf.Lerp(a1, a0, (float)k / (m - 1));
                        float rr = (k == 0 || k == m - 1) ? rOut * (0.9f + 0.06f * (float)rnd.NextDouble()) : rOut * (0.72f + 0.2f * (float)rnd.NextDouble());
                        float zf = (float)rnd.NextDouble() * 0.12f;   // 正面がわは少し深く欠ける
                        outer[k] = P0(aa, rr, -dd + zf); ofr[k] = P0(aa, rr * (0.97f + 0.03f * (float)rnd.NextDouble()), dd);
                    }
                    Vector3 i0f = P0(a0, rIn, -dd), i1f = P0(a1, rIn, -dd), i0b = P0(a0, rIn, dd), i1b = P0(a1, rIn, dd);
                    var cenF = (i0f + i1f + outer.Aggregate(Vector3.zero, (x, y) => x + y)) / (m + 2);
                    var cenB = (i0b + i1b + ofr.Aggregate(Vector3.zero, (x, y) => x + y)) / (m + 2);
                    var cen3 = (cenF + cenB) * 0.5f;
                    // 正面・裏（中心から扇）
                    var polyF = new List<Vector3> { i0f, i1f }; polyF.AddRange(outer);
                    var polyB = new List<Vector3> { i0b, i1b }; polyB.AddRange(ofr);
                    for (int k = 0; k < polyF.Count; k++)
                    {
                        int k2 = (k + 1) % polyF.Count;
                        Tri(v, t, cenF, polyF[k], polyF[k2], Vector3.back); Tri(v, t, cenB, polyB[k], polyB[k2], Vector3.forward);
                    }
                    // まわりの面（内がわの弧・欠けた外がわ・両はしの切り口）
                    for (int k = 0; k < polyF.Count; k++)
                    {
                        int k2 = (k + 1) % polyF.Count;
                        var mid3 = (polyF[k] + polyF[k2] + polyB[k] + polyB[k2]) * 0.25f;
                        Tri(v, t, polyF[k], polyF[k2], polyB[k2], mid3 - cen3); Tri(v, t, polyF[k], polyB[k2], polyB[k], mid3 - cen3);
                    }
                    continue;
                }
                var q = new[] { P0(a0, rIn, -dd), P0(a1, rIn, -dd), P0(a1, ro, -dd), P0(a0, ro, -dd), P0(a0, rIn, dd), P0(a1, rIn, dd), P0(a1, ro, dd), P0(a0, ro, dd) };
                var cen = P0((a0 + a1) / 2f, (rIn + ro) / 2f, 0f);
                int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 3, 2, 6, 7 }, new[] { 0, 3, 7, 4 }, new[] { 1, 2, 6, 5 } };
                var tv = key ? kv : v; var tt = key ? kt : t;
                foreach (var fc in faces)
                {
                    var fcen = (q[fc[0]] + q[fc[1]] + q[fc[2]] + q[fc[3]]) * 0.25f;
                    Tri(tv, tt, q[fc[0]], q[fc[1]], q[fc[2]], fcen - cen); Tri(tv, tt, q[fc[0]], q[fc[2]], q[fc[3]], fcen - cen);
                }
            }
            MeshObj(g, "Arch", SaveMesh(pre + "Arch", v, t), terra, Vector3.zero, 0, true);
            keyT = MeshObj(g, "Keystone", SaveMesh(pre + "Keystone", kv, kt), stone, Vector3.zero, 0, true).transform;
        }
        float keyTop = colH + rOut * 1.12f;
        // 要石の光る石（正面と裏、半分うまっている）
        var runeM = GlowMat(give ? "Ruins_ShieldRune" : "Ruins_ShieldOffRune", gcol, 1.4f);
        var glows = new List<Renderer>();
        foreach (float sz in new[] { -1f, 1f })
        {
            var gem = MeshObj(g, "Gem", CrystalClusterMesh("GateGem", 9, 0.22f, 0), CrystalMat(), new Vector3(0f, colH + (rIn + rOut * 1.12f) / 2f, sz * 0.55f), 0, false);
            gem.transform.localRotation = Quaternion.Euler(90f * sz, 0f, 0f);
            glows.Add(gem.GetComponent<Renderer>());
        }
        // 要石の上：石の爪3本と、浮いた小さいクリスタル
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int k = 0; k < 3; k++)
            {
                float a = Mathf.PI * 2f * k / 3f + 0.3f;
                var outw = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var tan = new Vector3(-outw.z, 0f, outw.x);
                var b = outw * 0.32f + Vector3.up * keyTop; var top = outw * 0.2f + Vector3.up * (keyTop + 0.75f);
                var q = new[] { b - tan * 0.09f - outw * 0.1f, b + tan * 0.09f - outw * 0.1f, b + tan * 0.09f + outw * 0.1f, b - tan * 0.09f + outw * 0.1f,
                                top - tan * 0.03f - outw * 0.03f, top + tan * 0.03f - outw * 0.03f, top + tan * 0.03f + outw * 0.03f, top - tan * 0.03f + outw * 0.03f };
                var cen = (b + top) * 0.5f;
                int[][] faces = { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }, new[] { 4, 5, 6, 7 } };
                foreach (var fc in faces) { var fcen = (q[fc[0]] + q[fc[1]] + q[fc[2]] + q[fc[3]]) * 0.25f; Tri(v, t, q[fc[0]], q[fc[1]], q[fc[2]], fcen - cen); Tri(v, t, q[fc[0]], q[fc[2]], q[fc[3]], fcen - cen); }
            }
            MeshObj(g, "Claws", SaveMesh(pre + "Claws", v, t), stone, Vector3.zero, 0, true);
        }
        float h = keyTop - 0.45f;   // 下の粒の位置用（クリスタルは keyTop+0.75）
        var crysM = CrystalMat();
        var cr = MeshObj(g, "Crystal", CrystalClusterMesh("GateCrystalCluster", 3, 0.42f, 0), crysM, new Vector3(0f, keyTop + 0.85f, 0f), 0, false);
        glows.Add(cr.GetComponent<Renderer>());
        var beam = Sparks(g, "Beam", new Vector3(0f, keyTop + 0.85f, 0f), 0.4f, 11f, 0.12f, 80);
        var bsh = beam.shape; bsh.shapeType = ParticleSystemShapeType.Cone; bsh.angle = 6f; bsh.radius = 0.1f;
        var bm = beam.main; bm.loop = false;
        // 当たり
        var bc = g.gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true;
        bc.size = new Vector3(CurtainTrigW, CurtainTrigH, 0.4f); bc.center = new Vector3(0f, CurtainTrigH / 2f - 6f, 0f);
        var sg = UdonSharpUndo.AddComponent<FuwaShieldGate>(g.gameObject);
        // 2026-11-15 てじゃ「バリア付与カーテンとこもなんかほしいな」「バリア模様？」→ アーチの口いっぱいに六角形のバリアの膜（Fuwa/ShieldVeil）。くぐった時は門の光といっしょに光る
        if (ShieldVeil)
        {
            float hw = cx - 0.2f, rr = rIn + 0.05f; int m = 24;
            var vv = new List<Vector3> { new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f), new Vector3(hw, colH, 0f), new Vector3(-hw, colH, 0f) };
            var tt = new List<int> { 0, 1, 2, 0, 2, 3 };
            int c0 = vv.Count; vv.Add(new Vector3(0f, colH, 0f));
            for (int k = 0; k <= m; k++) { float a = Mathf.PI * k / m; vv.Add(new Vector3(Mathf.Cos(a) * rr, colH + Mathf.Sin(a) * rr, 0f)); }
            for (int k = 0; k < m; k++) { tt.Add(c0); tt.Add(c0 + 1 + k); tt.Add(c0 + 2 + k); }
            var vm = SaveMesh(pre + "ShieldVeil", vv, tt);
            var veilM = FxMat(give ? "Ruins_ShieldVeil" : "Ruins_ShieldOffVeil", "Fuwa/ShieldVeil"); veilM.SetColor("_Color", gcol); veilM.SetFloat("_Width", 2f * hw); veilM.SetFloat("_Height", colH + rr); veilM.SetFloat("_ArchR", rr); veilM.SetFloat("_RimW", 0.9f); EditorUtility.SetDirty(veilM);
            var vo = MeshObj(g, "Veil", vm, veilM, Vector3.zero, 2, false);
            var vr = vo.GetComponent<Renderer>(); vr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; vr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            glows.Add(vr);
        }
        sg.shield = shieldT; sg.give = give; sg.crystal = cr.transform; sg.glows = glows.ToArray(); sg.glowColor = gcol; sg.beam = beam;
        UdonSharpEditorUtility.CopyProxyToUdon(sg); FuwaStage2WaterTest.NoSync(sg);
    }
    // バリアが消える所（見た目なし）
    static void ShieldOff(string name)
    {
        var g = new GameObject(name); g.transform.SetParent(isl, false); g.transform.localPosition = new Vector3(0f, P.y, P.z); g.layer = 2;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true;
        bc.size = new Vector3(CurtainTrigW, CurtainTrigH, 0.4f); bc.center = new Vector3(0f, CurtainTrigH / 2f - 6f, 0f);
        var sg = UdonSharpUndo.AddComponent<FuwaShieldGate>(g);
        sg.shield = shieldT; sg.give = false;
        UdonSharpEditorUtility.CopyProxyToUdon(sg); FuwaStage2WaterTest.NoSync(sg);
    }

    // 円形の広場：今の P（道のはし）から +z へ。床は道より PlazaRaise 高い石の台（道のはしは台の下にもぐる＝ちらつき・すき間なし）
    //  床は同心円のシマシマ（道と同じ A/B）、まん中に台座とクリスタル、台座のまわりに光るルーンの輪、外に一段低い石のふち（入口・出口はあける）と崩れた柱
    //  終わったら P は反対がわの道のはじまり
    // ===== 2026-11-15 上段の広場の下：石のブロックを積んだ逆さドーム =====
    //  てじゃ「上段の広場の下の部分、土じゃなくてブロックのドームみたいな感じにしたい」
    //  だ円の断面（ふちの半径 Rr・深さ D）を段（PlazaDomeRows）に分けて、段ごとにブロック（はば約 PlazaDomeBlockW、段ごとに半分ずらす＝れんが積み）
    //  ブロックは表の面＋まわりの面取り（ふちで PlazaDomeBevel だけ中へ）＝となりとの間が V の溝。溝の奥は暗い裏の殻（すき間から空が見えない）
    //  いちばん下はまるい要石（八角すい）＋下向きの小さいクリスタル
    public static bool PlazaDome = true; public static int PlazaDomeRows = 8; public static float PlazaDomeDepth = 5.6f, PlazaDomeBlockW = 1.5f, PlazaDomeBevel = 0.1f, PlazaDomeThetaMax = 76f;
    static void PlazaBlockDome(Transform g, string name, float Rr, float uy)
    {
        float D = PlazaDomeDepth, bev = PlazaDomeBevel;
        var rnd = new System.Random(4242);
        // 断面：θ（0＝ふち、上）→ 半径 Rr cosθ、高さ uy − D sinθ。法線（外・下向き）
        Vector3 S(float th, float ph, float off)
        {
            float r = Rr * Mathf.Cos(th), y = uy - D * Mathf.Sin(th);
            var n2 = new Vector2(Mathf.Cos(th) / Rr, -Mathf.Sin(th) / D).normalized;   // (外, 上)
            r += n2.x * off; y += n2.y * off;
            return new Vector3(Mathf.Cos(ph) * r, y, Mathf.Sin(ph) * r);
        }
        var stoneB = FuwaStage2WaterTest.ToonMat("Ruins_DomeStoneB", Color.Lerp(stone.color, terraD.color, 0.35f) * 0.95f);
        var vA = new List<Vector3>(); var tA = new List<int>(); var vB = new List<Vector3>(); var tB = new List<int>();
        float thMax = PlazaDomeThetaMax * Mathf.Deg2Rad;
        int rows = PlazaDomeRows;
        // 段の境目：下ほど少しせまく（上のほうが大きい石）
        var ths = new float[rows + 1];
        for (int k = 0; k <= rows; k++) { float u = (float)k / rows; ths[k] = thMax * (u * 0.8f + 0.2f * u * u); }
        for (int k = 0; k < rows; k++)
        {
            float t0 = ths[k], t1 = ths[k + 1], tm = (t0 + t1) * 0.5f;
            float rm = Rr * Mathf.Cos(tm);
            int n = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * rm / PlazaDomeBlockW));
            float phOff = (k % 2) * Mathf.PI / n + 0.37f;
            // 弧の長さ（面取りのはばを角度にする）
            float arcTh = Vector3.Distance(S(t0, 0f, 0f), S(t1, 0f, 0f));
            for (int i = 0; i < n; i++)
            {
                float p0 = phOff + 2f * Mathf.PI * i / n, p1 = phOff + 2f * Mathf.PI * (i + 1) / n;
                float arcPh = rm * (p1 - p0);
                float bw = Mathf.Min(0.16f, Mathf.Min(arcTh, arcPh) * 0.18f);
                float dT = (t1 - t0) * bw / arcTh, dP = (p1 - p0) * bw / arcPh;
                float lift = 0.02f + 0.06f * (float)rnd.NextDouble();      // 石ごとに少し出っぱり方をかえる
                // ふち（溝の底）と、表の面の角
                var E = new[] { S(t0, p0, -bev), S(t0, p1, -bev), S(t1, p1, -bev), S(t1, p0, -bev) };
                var I = new[] { S(t0 + dT, p0 + dP, lift), S(t0 + dT, p1 - dP, lift), S(t1 - dT, p1 - dP, lift), S(t1 - dT, p0 + dP, lift) };
                var nrm = (I[0] + I[1] + I[2] + I[3]) * 0.25f - new Vector3(0f, uy + D * 0.35f, 0f);   // 外（下・横）向き
                bool alt = rnd.NextDouble() < 0.3;
                var vv = alt ? vB : vA; var tt = alt ? tB : tA;
                Tri(vv, tt, I[0], I[1], I[2], nrm); Tri(vv, tt, I[0], I[2], I[3], nrm);
                for (int e = 0; e < 4; e++)
                {
                    int f = (e + 1) % 4;
                    Tri(vv, tt, E[e], E[f], I[f], nrm); Tri(vv, tt, E[e], I[f], I[e], nrm);
                }
            }
        }
        // 裏の殻（溝の奥・継ぎ目のすき間をふさぐ、暗い）
        var vK = new List<Vector3>(); var tK = new List<int>();
        {
            int nP = 96, nT = 24;
            for (int a = 0; a < nT; a++)
                for (int i = 0; i < nP; i++)
                {
                    float t0 = thMax * a / nT, t1 = thMax * (a + 1) / nT, p0 = 2f * Mathf.PI * i / nP, p1 = 2f * Mathf.PI * (i + 1) / nP;
                    var A = S(t0, p0, -bev * 0.8f); var B = S(t0, p1, -bev * 0.8f); var C = S(t1, p1, -bev * 0.8f); var Dd = S(t1, p0, -bev * 0.8f);
                    var w = (A + C) * 0.5f - new Vector3(0f, uy + D * 0.35f, 0f);
                    Tri(vK, tK, A, B, C, w); Tri(vK, tK, A, C, Dd, w);
                }
        }
        // いちばん下：八角の要石（下へ細る）＋ふたの石
        {
            float yb = uy - D * Mathf.Sin(thMax), rb = Rr * Mathf.Cos(thMax) + 0.15f;
            int m = 8; var top = new Vector3[m]; var mid = new Vector3[m]; var bot = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                float a = 2f * Mathf.PI * (i + 0.5f) / m; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                top[i] = d * rb + Vector3.up * (yb + 0.25f); mid[i] = d * (rb * 0.92f) + Vector3.up * (yb - 0.35f); bot[i] = d * (rb * 0.5f) + Vector3.up * (yb - 1.1f);
            }
            var c0 = Vector3.up * (yb - 1.25f);
            for (int i = 0; i < m; i++)
            {
                int j = (i + 1) % m;
                var o1 = (top[i] + top[j]) * 0.5f; o1.y = 0f; var o2 = (mid[i] + mid[j]) * 0.5f; o2.y = -0.4f * o2.magnitude;
                Tri(vA, tA, top[i], top[j], mid[j], o1); Tri(vA, tA, top[i], mid[j], mid[i], o1);
                Tri(vA, tA, mid[i], mid[j], bot[j], o2 + Vector3.down); Tri(vA, tA, mid[i], bot[j], bot[i], o2 + Vector3.down);
                Tri(vA, tA, bot[i], bot[j], c0, Vector3.down);
            }
            // 下向きの小さいクリスタル（広場のクリスタルとおそろい）
            var cr = MeshObj(g, "DomeCrystal", CrystalClusterMesh("PlazaDomeCrystal", 77, 1.1f, 0), CrystalMat(), new Vector3(0f, yb - 1.15f, 0f), 0, false);
            cr.transform.localRotation = Quaternion.Euler(180f, 20f, 0f);
        }
        // 2026-11-15 てじゃ「上の方にすきまみえる」→ ドームの上は開いていて、いちばん上の溝から中（裏の殻は外向きだけ＝中から見ると透明）を通して空が見えた
        //  → ふち（uy）にふた：下向き（中から見える、暗い）＋上向き（入口・出口のふちがない所で上から見える、石）
        var vL = new List<Vector3>(); var tL = new List<int>();
        {
            int nP = 96;
            for (int i = 0; i < nP; i++)
            {
                float p0 = 2f * Mathf.PI * i / nP, p1 = 2f * Mathf.PI * (i + 1) / nP;
                var c = new Vector3(0f, uy - 0.005f, 0f);
                var a0 = new Vector3(Mathf.Cos(p0) * (Rr + 0.02f), uy - 0.005f, Mathf.Sin(p0) * (Rr + 0.02f)); var a1 = new Vector3(Mathf.Cos(p1) * (Rr + 0.02f), uy - 0.005f, Mathf.Sin(p1) * (Rr + 0.02f));
                Tri(vK, tK, c, a0, a1, Vector3.down);
                Tri(vL, tL, c + Vector3.up * 0.005f, a0 + Vector3.up * 0.005f, a1 + Vector3.up * 0.005f, Vector3.up);
            }
        }
        var go = new GameObject("BlockDome"); go.transform.SetParent(g, false);
        MeshObj(go.transform, "Lid", SaveMesh(name + "_DomeLid", vL, tL), stone, Vector3.zero, 0, true);
        MeshObj(go.transform, "BlocksA", SaveMesh(name + "_DomeA", vA, tA), stone, Vector3.zero, 0, true);
        if (vB.Count > 0) MeshObj(go.transform, "BlocksB", SaveMesh(name + "_DomeB", vB, tB), stoneB, Vector3.zero, 0, true);
        MeshObj(go.transform, "Backing", SaveMesh(name + "_DomeBack", vK, tK), underM, Vector3.zero, 0, true);
    }

    static void CrystalPlaza(string name)
    {
        float R = PlazaR, cz = P.z + R - 0.4f, top = P.y + PlazaRaise;
        var g = new GameObject(name).transform; g.SetParent(isl, false); g.localPosition = new Vector3(0f, top, cz);
        // ---- 床 ----
        const int Seg = 96;
        float runeR0 = 1.7f, runeR1 = 2.2f;
        var bands = new List<(float r0, float r1, int mat)>();   // mat 0=A 1=B 2=ルーン 3=石（台座の下）
        bands.Add((0f, runeR0, 3)); bands.Add((runeR0, runeR1, 2));
        { float r = runeR1; int k = 0; while (r < R - 0.01f) { float r1 = Mathf.Min(R, r + StripeLen); if (R - r1 < 0.5f) r1 = R; bands.Add((r, r1, k % 2)); r = r1; k++; } }
        var runeM = GlowMat("Ruins_PlazaRune", ShieldCol, 0.6f);
        Material[] mats = { pathA, pathB, runeM, stone };
        var runeRends = new List<Renderer>();
        for (int mi = 0; mi < 4; mi++)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            foreach (var b in bands) if (b.mat == mi) AnnulusInto(v, t, uv, Mathf.Max(0.001f, b.r0), b.r1, 0f, Mathf.PI * 2f, 0f, Mathf.Abs(b.r1 - R) < 0.001f ? 0.6f + PlazaRaise : 0f, Seg);
            if (v.Count == 0) continue;
            FixWinding(v, t, c => c.y > -0.001f && new Vector2(c.x, c.z).magnitude < R - 0.001f ? Vector3.up : new Vector3(c.x, 0f, c.z));
            var mo = MeshObj(g, "Floor" + "ABRS"[mi], SaveMeshUV(name + "_Floor" + mi, v, t, uv, null), mats[mi], Vector3.zero, 0, true);
            if (mi == 2) runeRends.Add(mo.GetComponent<Renderer>());
        }
        // 当たり（道レイヤー＝ふわふわが触れたらアウト）：48 角の柱
        {
            var v = new List<Vector3>(); var t = new List<int>(); int n = 48; float y0 = -0.6f - PlazaRaise;
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.PI * 2f * i / n, a1 = Mathf.PI * 2f * (i + 1) / n;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * R; var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * R;
                Tri(v, t, Vector3.zero, d0, d1, Vector3.up);
                Tri(v, t, Vector3.up * y0, d0 + Vector3.up * y0, d1 + Vector3.up * y0, Vector3.down);
                Tri(v, t, d0, d1, d1 + Vector3.up * y0, d0 + d1); Tri(v, t, d0, d1 + Vector3.up * y0, d0 + Vector3.up * y0, d0 + d1);
            }
            var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
            col.AddComponent<MeshCollider>().sharedMesh = SaveMesh(name + "_Col", v, t);
        }
        // ---- 外のふち（一段低い石の輪、入口・出口はあける）----
        {
            // 2026-11-15 てじゃ「継ぎ目われてる」→ ふちの切れ目は角度（放射状）で切っていて、外ほど道との間がひらいていた（くさび形のすき間）
            //  → 切れ目を道（入口）・橋（出口）のはばでまっすぐに切る＋切り口にふた
            float ly = -0.25f, hwE = HW + 0.02f, hwX = PlazaBridgeDirect ? BridgeHW + 0.05f : HW + 0.02f;   // 橋の横の壁（xo＝BridgeHW+0.05）にぴったり
            float gapE = Mathf.Asin(Mathf.Min(0.95f, hwE / R)), gapX = Mathf.Asin(Mathf.Min(0.95f, hwX / R));
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            // 入口は -z（角度 270°）、出口は +z（90°）
            float e = Mathf.PI * 1.5f, x = Mathf.PI * 0.5f;
            // 2026-11-15 てじゃ「手前側のつなぎ、まわりの茶色部分の裏がみえてる」→ 入口は切らずに道の下を通す（ふちの上の面は道の上の面より 13cm 下＝道の板の中にかくれる）
            if (PlazaLedgeUnderEntry) AnnulusInto(v, t, uv, R, R + PlazaLedge, x + gapX, x + Mathf.PI * 2f - gapX, ly, 0.5f, 80);
            else
            {
                AnnulusInto(v, t, uv, R, R + PlazaLedge, x + gapX, e - gapE, ly, 0.5f, 40);
                AnnulusInto(v, t, uv, R, R + PlazaLedge, e + gapE, x + Mathf.PI * 2f - gapX, ly, 0.5f, 40);
            }
            // 切れ目の近くの点を、道のはしの線（x = ±hw）の上へ（外の円の上のまま）
            for (int k = 0; k < v.Count; k++)
            {
                var c = v[k]; if (PlazaLedgeUnderEntry && c.z < 0f) continue; float hw = c.z < 0f ? hwE : hwX;
                if (Mathf.Abs(c.x) < hw)
                {
                    float rr = new Vector2(c.x, c.z).magnitude; float sx = Mathf.Sign(c.x == 0f ? 1f : c.x);
                    c.x = sx * hw; c.z = Mathf.Sign(c.z) * Mathf.Sqrt(Mathf.Max(0f, rr * rr - hw * hw)); v[k] = c;
                }
            }
            FixWinding(v, t, c => new Vector2(c.x, c.z).magnitude < R + PlazaLedge - 0.001f ? Vector3.up : new Vector3(c.x, 0f, c.z));
            // 切り口のふた（4つ：入口の左右・出口の左右）。内（R）〜外（R+PlazaLedge）、上 ly 〜 下 ly-0.5
            foreach (var (hw, sz) in new[] { (hwE, -1f), (hwX, 1f) })
                foreach (float sx in new[] { -1f, 1f })
                {
                    if (PlazaLedgeUnderEntry && sz < 0f) continue;
                    float zi = sz * Mathf.Sqrt(Mathf.Max(0f, R * R - hw * hw)), zo = sz * Mathf.Sqrt(Mathf.Max(0f, (R + PlazaLedge) * (R + PlazaLedge) - hw * hw));
                    var a0 = new Vector3(sx * hw, ly, zi); var a1 = new Vector3(sx * hw, ly, zo); var b1 = new Vector3(sx * hw, ly - 0.5f, zo); var b0 = new Vector3(sx * hw, ly - 0.5f, zi);
                    int k0 = v.Count; v.Add(a0); v.Add(a1); v.Add(b1); v.Add(b0); for (int q = 0; q < 4; q++) uv.Add(Vector2.zero);
                    var want = new Vector3(-sx, 0f, 0f);   // 道の方へ向く
                    if (Vector3.Dot(Vector3.Cross(a1 - a0, b1 - a0), want) < 0f) { t.Add(k0); t.Add(k0 + 2); t.Add(k0 + 1); t.Add(k0); t.Add(k0 + 3); t.Add(k0 + 2); }
                    else { t.Add(k0); t.Add(k0 + 1); t.Add(k0 + 2); t.Add(k0); t.Add(k0 + 2); t.Add(k0 + 3); }
                }
            var lm = SaveMeshUV(name + "_Ledge", v, t, uv, null);
            var lo = MeshObj(g, "Ledge", lm, stone, Vector3.zero, pathLayer, true);
            lo.AddComponent<MeshCollider>().sharedMesh = lm;
        }
        // ---- 下の岩（浮いた島らしく先細り）----
        //  2026-11-15 見た目クオリティ（6）：円柱の積み重ね → Blender で作ったゴツゴツの浮き島の岩（BlenderSource/FLUFF_models.blend の PlazaRock シーン → Models/PlazaRock.fbx → 子の変換を焼いた PlazaRockBaked）
        //  上の口（半径 10.4）がふちの下（-0.75）にぴったり。ふち（R+PlazaLedge）より少し大きく広げて、下から見てすき間が出ないように
        var rockMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/PlazaRockBaked.asset");
        float[] ur = { R + PlazaLedge - 0.05f, R - 0.6f, R - 3f, R - 5.8f }, uh = { 1.2f, 1.6f, 1.8f, 2f };
        float uy = -0.75f;   // ふちの側面の下から（ふちの下の面のかわり）
        if (PlazaDome) { PlazaBlockDome(g, name, R + PlazaLedge, uy); ur = new float[0]; }
        else if (rockMesh)
        {
            var ro = new GameObject("UnderRock"); ro.transform.SetParent(g, false); ro.transform.localPosition = new Vector3(0f, uy, 0f);
            float k = (R + PlazaLedge + 0.06f) / 10.4f; ro.transform.localScale = new Vector3(k, 1f, k);
            ro.AddComponent<MeshFilter>().sharedMesh = rockMesh; ro.AddComponent<MeshRenderer>().sharedMaterials = new[] { stone, underM };
            ur = new float[0];
        }
        for (int i = 0; i < ur.Length; i++)
        {
            var cy = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cy.name = "Under" + i; Object.DestroyImmediate(cy.GetComponent<Collider>());
            cy.transform.SetParent(g, false); cy.transform.localPosition = new Vector3(0f, uy - uh[i] / 2f, 0f);
            cy.transform.localScale = new Vector3(2f * ur[i], uh[i] / 2f, 2f * ur[i]); cy.transform.localRotation = Quaternion.Euler(0f, i * 17f, 0f);
            cy.GetComponent<Renderer>().sharedMaterial = i == 0 ? stone : underM;
            uy -= uh[i];
        }
        // ---- 崩れた柱（ふちの上、入口・出口の近くはなし）----
        //  2026-11-15 見た目クオリティ（4）：角柱 → 溝のある古い柱（ColumnMesh）。完全・折れ・切り株をまぜ、1本の横に倒れた胴のかけら
        var rnd = new System.Random(515);
        int np = 10, ci = 0;
        int[] kinds = { 0, 1, 2, 0, 1, 0, 2, 1 };
        float[] hts = { 4.6f, 3.1f, 1.1f, 4.6f, 2.3f, 4.6f, 0.9f, 3.6f };
        for (int i = 0; i < np; i++)
        {
            float a = Mathf.PI * 2f * (i + 0.5f) / np;
            float fromX = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90f)), fromE = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f));
            if (fromX < 28f || fromE < 28f) continue;
            float rr = R + PlazaLedge * 0.55f;
            var pos = new Vector3(Mathf.Cos(a) * rr, -0.25f, Mathf.Sin(a) * rr);
            int kind = kinds[ci % kinds.Length]; float hh = hts[ci % hts.Length];
            var cm = ColumnMesh("PlazaColumn" + ci, 900 + ci, hh, kind);
            var co = new GameObject("Column" + ci); co.transform.SetParent(g, false); co.transform.localPosition = pos;
            rnd.NextDouble();   // 乱数の並びを前と同じに保つ
            co.transform.localRotation = Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z));   // 2026-11-15 四角い台・柱頭を広場のまん中に向けてそろえる
            co.AddComponent<MeshFilter>().sharedMesh = cm; co.AddComponent<MeshRenderer>().sharedMaterials = new[] { terra, stone };
            var cc = co.AddComponent<CapsuleCollider>(); cc.radius = 0.4f; cc.height = hh; cc.center = Vector3.up * hh / 2f;
            if (kind == 2)
            { // 切り株の横に倒れた胴のかけら
                var dm = ColumnMesh("PlazaDrum" + ci, 950 + ci, 1.5f, 3);
                var dobj = new GameObject("FallenDrum" + ci); dobj.transform.SetParent(g, false);
                var tang = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                dobj.transform.localPosition = pos + tang * 1.0f + Vector3.up * 0.33f;
                dobj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, tang) * Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                dobj.AddComponent<MeshFilter>().sharedMesh = dm; dobj.AddComponent<MeshRenderer>().sharedMaterials = new[] { terra, stone };
                var dc = dobj.AddComponent<CapsuleCollider>(); dc.radius = 0.34f; dc.height = 1.5f; dc.center = Vector3.up * 0.75f;
            }
            ci++;
        }
        // ---- 台座とクリスタル ----
        // 2026-11-15 見た目クオリティ：円柱 → 八角形3段の祭壇（欠け・ルーンの溝・石の爪）。当たりは円柱のまま（見た目なし）
        var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ped.name = "Pedestal"; ped.transform.SetParent(g, false);
        ped.transform.localPosition = new Vector3(0f, 0.42f, 0f); ped.transform.localScale = new Vector3(3.1f, 0.42f, 3.1f);
        Object.DestroyImmediate(ped.GetComponent<MeshRenderer>()); Object.DestroyImmediate(ped.GetComponent<MeshFilter>());
        var altar = AltarMesh(name + "_Altar");
        // 2026-11-15 てじゃ「クリスタル台座のコケが雑」：四角い板の苔 → 石像と同じやり方（上を向いた面に、まだらに苔の色。FuwaToonLit の地面のムラ）
        MeshObj(g, "Altar", altar.stone, AltarStoneMat(stone), Vector3.zero, 0, true);
        var altarRune = MeshObj(g, "AltarRune", altar.rune, runeM, Vector3.zero, 0, false);
        runeRends.Add(altarRune.GetComponent<Renderer>());
        var crysM = CrystalMat();
        var pivot = new GameObject("Crystal").transform; pivot.SetParent(g, false); pivot.localPosition = new Vector3(0f, CrystalH, 0f);
        var core = MeshObj(pivot, "Core", CrystalClusterMesh("PlazaCrystalCluster", 7, 1.3f, 0)   /* 2026-11-15 てじゃ「クリスタル本体にトゲトゲいらんかも」→ 小さい結晶なし */, crysM, Vector3.zero, 0, false);
        var crystalRends = new List<Renderer> { core.GetComponent<Renderer>() };
        var shardTs = new List<Transform>();
        for (int i = 0; i < 3; i++)
        {
            float a = Mathf.PI * 2f * i / 3f;
            var sh = MeshObj(pivot, "Shard" + i, CrystalClusterMesh("PlazaShard" + i, 50 + i, 0.32f, 0), crysM, new Vector3(Mathf.Cos(a) * 1.25f, -0.2f + 0.25f * i, Mathf.Sin(a) * 1.25f), 0, false);
            sh.transform.localRotation = Quaternion.Euler(12f * (i - 1), 0f, 18f);
            crystalRends.Add(sh.GetComponent<Renderer>()); shardTs.Add(sh.transform);
        }
        // 2026-11-15 てじゃ「クリスタル本体にもエネルギーためてる感じ」：まわりの光（オーラ）＋斜めの光の輪2本
        var auraM = FxMat("Ruins_CrystalAura", "Fuwa/EnergyGlow"); auraM.SetColor("_Color", ShieldCol); EditorUtility.SetDirty(auraM);
        var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere); aura.name = "Aura"; Object.DestroyImmediate(aura.GetComponent<Collider>());
        aura.transform.SetParent(pivot, false); aura.transform.localScale = Vector3.one * 1.6f;
        { var r = aura.GetComponent<MeshRenderer>(); r.sharedMaterial = auraM; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; }
        // 2026-11-15 てじゃ「チャージでクリスタルも黒オーラみたいな色つけよ」「撃ったらもどる」→ ためる間だけ黒紫のもや（Fuwa/DarkAura）
        MeshRenderer darkAuraR = null;
        if (ShockDark)
        {
            var da = GameObject.CreatePrimitive(PrimitiveType.Sphere); da.name = "DarkAura"; Object.DestroyImmediate(da.GetComponent<Collider>());
            da.transform.SetParent(pivot, false); da.transform.localScale = Vector3.one * 1.4f;
            var daM = FxMat("Ruins_CrystalDarkAura", "Fuwa/DarkAura"); EditorUtility.SetDirty(daM);
            darkAuraR = da.GetComponent<MeshRenderer>(); darkAuraR.sharedMaterial = daM; darkAuraR.enabled = false;
            darkAuraR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; darkAuraR.receiveShadows = false; darkAuraR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; darkAuraR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        // ---- 衝撃波の見た目 ----
        var waveM = FxMat("Ruins_Shockwave", "Fuwa/Shockwave"); waveM.SetFloat("_Alpha", 0f);   // 濃さはスクリプトが MPB で（エディタでは見えない）
        Mesh band;
        {
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>(); var nn = new List<Vector3>(); int n = 96;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 2f * i / n; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d); v.Add(d + Vector3.up); uv.Add(new Vector2((float)i / n, 0f)); uv.Add(new Vector2((float)i / n, 1f)); nn.Add(d); nn.Add(d);
                if (i < n) { int k = i * 2; t.Add(k); t.Add(k + 1); t.Add(k + 3); t.Add(k); t.Add(k + 3); t.Add(k + 2); }
            }
            band = SaveMeshUV("ShockwaveBand", v, t, uv, nn);
        }
        Mesh ring;
        {
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            AnnulusInto(v, t, uv, 0.72f, 1f, 0f, Mathf.PI * 2f, 0f, 0f, 96);
            var nn = v.Select(_ => Vector3.up).ToList();
            ring = SaveMeshUV("ShockwaveRing", v, t, uv, nn);
        }
        // 2026-11-15 てじゃ「衝撃波が青なのも見えづらさのひとつかも」「いっそ黒っぽいオーラだとどう見えるかな」→ 衝撃波（壁・床の輪・残像・床の範囲）だけ黒っぽい紫（ShockDark）
        var waveDM = waveM;
        if (ShockDark)
        {
            waveDM = FxMat("Ruins_ShockwaveDark", "Fuwa/Shockwave"); waveDM.SetFloat("_Alpha", 0f);
            waveDM.SetColor("_Color", ShockDarkCol); waveDM.SetColor("_Core", ShockDarkCore); EditorUtility.SetDirty(waveDM);
        }
        var wave = MeshObj(g, "Wave", band, waveDM, Vector3.zero, 0, false);
        var waveF = MeshObj(g, "WaveFloor", ring, waveDM, new Vector3(0f, 0.02f, 0f), 0, false);
        var chg = MeshObj(g, "ChargeRing", ring, waveM, new Vector3(0f, 0.03f, 0f), 0, false);
        // 7：砂ぼこりの輪（壁のあとを追う、低い）と壁の残像
        var dustM = FxMat("Ruins_DustRing", "Fuwa/DustRing"); dustM.SetFloat("_Alpha", 0f); dustM.SetColor("_Color", new Color(0.74f, 0.63f, 0.5f)); EditorUtility.SetDirty(dustM);
        var dust = MeshObj(g, "DustRing", band, dustM, Vector3.zero, 0, false);
        var echo = MeshObj(g, "WaveEcho", band, waveDM, Vector3.zero, 0, false);
        foreach (var o in new[] { dust, echo }) { var r = o.GetComponent<Renderer>(); r.enabled = false; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; }
        foreach (var o in new[] { wave, waveF, chg }) { var r = o.GetComponent<Renderer>(); r.enabled = false; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off; }
        // 粒：ため（まわりから集まる）とドン（はじける）
        var cps = Sparks(pivot, "ChargeFx", Vector3.zero, 0.7f, -4.2f, 0.1f, 120);
        { var s = cps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 3f; var em = cps.emission; em.rateOverTime = 70f; em.enabled = false; var m = cps.main; m.simulationSpace = ParticleSystemSimulationSpace.Local; cps.Play(); }
        var bps = Sparks(pivot, "BlastFx", Vector3.zero, 0.7f, 9f, 0.16f, 120);
        { var s = bps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.5f; var m = bps.main; m.loop = false; }
        // ---- しくみ ----
        // 床の魔法陣（3）：床にぴったりの板（Offset で手前に描く）。ためると外から内へ光が走る
        Renderer circleR;
        {
            var v = new List<Vector3> { new Vector3(-R, 0f, -R), new Vector3(R, 0f, -R), new Vector3(R, 0f, R), new Vector3(-R, 0f, R) };
            var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var t = new List<int> { 0, 2, 1, 0, 3, 2 };
            var cm = SaveMeshUV(name + "_Circle", v, t, uv, Enumerable.Repeat(Vector3.up, 4).ToList());
            var mcM = FxMat("Ruins_MagicCircle", "Fuwa/MagicCircle"); mcM.SetFloat("_Radius", R); mcM.SetFloat("_R0", 2.5f); EditorUtility.SetDirty(mcM);
            var co = MeshObj(g, "MagicCircle", cm, mcM, Vector3.zero, 0, false);
            circleR = co.GetComponent<Renderer>(); circleR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; circleR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        var orbitTs = new List<Transform>(); var orbitRs = new List<Renderer>();
        {
            Mesh thin;
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            AnnulusInto(v, t, uv, 0.9f, 1f, 0f, Mathf.PI * 2f, 0f, 0f, 64);
            thin = SaveMeshUV("CrystalOrbitRing", v, t, uv, v.Select(_ => Vector3.up).ToList());
            var tilts = new[] { new Vector3(62f, 0f, 18f), new Vector3(-55f, 40f, -25f) };
            for (int i = 0; i < 2; i++)
            {
                var ro = MeshObj(pivot, "OrbitRing" + i, thin, waveM, Vector3.zero, 0, false);
                ro.transform.localRotation = Quaternion.Euler(tilts[i]); ro.transform.localScale = new Vector3(2.2f, 1f, 2.2f);
                var r = ro.GetComponent<Renderer>(); r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                orbitTs.Add(ro.transform); orbitRs.Add(r);
            }
        }
        var sw = UdonSharpUndo.AddComponent<FuwaShockwave>(g.gameObject);
        sw.magicCircle = circleR;
        sw.dustRing = dust.transform; sw.dustRenderer = dust.GetComponent<Renderer>(); sw.waveEcho = echo.transform; sw.echoRenderer = echo.GetComponent<Renderer>();
        sw.shards = shardTs.ToArray(); sw.auraRenderer = aura.GetComponent<Renderer>(); sw.orbitRings = orbitTs.ToArray(); sw.orbitRingRenderers = orbitRs.ToArray();
        sw.ball = ballT; sw.shield = shieldT; sw.radius = R;
        sw.nearPad = ShieldSafePad;   // 2026-11-15 てじゃ「フワフワバリアの範囲、わずかに狭く」0.8 → 0.6 →（影を接する線にして広がったので「ちょっと狭く」）0.25
        sw.shadowPad = sw.nearPad;
        // 2026-11-15 てじゃ「初回チャージ、チャージが速すぎて気になる、チャージ時間揃える代わりにもっと手前から察知してチャージ開始」
        //  → 1発目のためも chargeTime と同じ。ふわふわが広場のふちの ShockEarly m 手前に来たらためはじめ（止まるのはそれより外 ShockEarly+0.5）
        sw.firstChargeTime = sw.chargeTime; sw.enterMargin = ShockEarly; sw.leaveMargin = ShockEarly + 0.5f;
        // 入口の道も当たる（バリアの門より奥＝門の 0.4m 手前まで）。向きは広場のまん中→入口（g の -z）
        {
            var gateT = isl.Find("ShieldGate");
            Vector3 cW = g.position, dW = g.TransformDirection(Vector3.back); dW.y = 0f; dW.Normalize();
            float glen = gateT ? Vector3.Dot(gateT.position - cW, dW) - 0.4f : R + 3f;
            if (ShockCorridor) { sw.corridorDir = dW; sw.corridorHalfW = HW + 0.3f; sw.corridorLen = glen; }
            shockCorridorLen = ShockCorridor ? glen : 0f;
        }   // 2026-11-15 てじゃ「後ろの影の出始めが、バリアの中心じゃないんだな」→ 影のはばの始まりをバリアのまわりのセーフ（半径＋nearPad）とそろえる（線がいちばん太い所からそのまま後ろへ）
        sw.crystal = pivot; sw.crystalGlows = crystalRends.ToArray(); sw.glowColor = ShieldCol; sw.runeGlows = runeRends.ToArray();
        sw.wave = wave.transform; sw.waveFloor = waveF.transform; sw.chargeRing = chg.transform;
        sw.waveRenderers = new[] { wave.GetComponent<Renderer>(), waveF.GetComponent<Renderer>() }; sw.chargeRenderer = chg.GetComponent<Renderer>();
        sw.chargeParticles = cps; sw.blastParticles = bps;
        if (darkAuraR != null) { sw.darkAura = darkAuraR; sw.chargeTint = new Color(0.62f, 0.2f, 0.95f, 1f); }
        {   // 2026-11-15 てじゃ「衝撃波の範囲、もっと目立たせたいな、特に床」→ 床にかぶせる円盤（Fuwa/ShockFloor）
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            AnnulusInto(v, t, uv, 0.001f, R, 0f, Mathf.PI * 2f, 0f, 0f, 96);
            if (shockCorridorLen > R)
            {   // 入口の道の上（g の -z 向き、道はば）
                float hwc = HW, z1 = -(R - 0.6f), z2 = -shockCorridorLen; int k0 = v.Count;
                v.Add(new Vector3(-hwc, -0.12f, z1)); v.Add(new Vector3(hwc, -0.12f, z1)); v.Add(new Vector3(hwc, -0.12f, z2)); v.Add(new Vector3(-hwc, -0.12f, z2));
                for (int q = 0; q < 4; q++) uv.Add(Vector2.zero);
                t.Add(k0); t.Add(k0 + 1); t.Add(k0 + 2); t.Add(k0); t.Add(k0 + 2); t.Add(k0 + 3);
            }
            FixWinding(v, t, _ => Vector3.up);
            var zm = SaveMeshUV(name + "_ShockZone", v, t, uv, v.Select(_ => Vector3.up).ToList());
            var zM = FxMat("Ruins_ShockFloor", "Fuwa/ShockFloor"); zM.SetColor("_Color", ShockDark ? ShockDarkCol : ShieldCol); zM.SetFloat("_Radius", Mathf.Max(R, shockCorridorLen)); EditorUtility.SetDirty(zM);
            var zo = MeshObj(g, "ShockZone", zm, zM, new Vector3(0f, 0.025f, 0f), 0, false);
            var zr = zo.GetComponent<Renderer>(); zr.enabled = false; zr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; zr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            sw.floorZone = zr;
            if (ShockSafeVeil)
            {   // 2026-11-15 てじゃ「セーフティーゾーンの周りにうっすら白いヴェールほしい」：影のふちに沿って立つ幕の型（形はシェーダーが毎フレーム作る）
                var vv = new List<Vector3>(); var vt = new List<int>(); var vuv = new List<Vector2>();
                const int VN = 160;
                for (int q = 0; q <= VN; q++) { float u = q / (float)VN; vv.Add(new Vector3(u, 0f, 0f)); vv.Add(new Vector3(u, 1f, 0f)); vuv.Add(new Vector2(u, 0f)); vuv.Add(new Vector2(u, 1f)); }
                for (int q = 0; q < VN; q++) { int a = q * 2; vt.Add(a); vt.Add(a + 1); vt.Add(a + 3); vt.Add(a); vt.Add(a + 3); vt.Add(a + 2); }
                var vm = SaveMeshUV(name + "_SafeVeil", vv, vt, vuv, vv.Select(_ => Vector3.forward).ToList());
                vm.bounds = new Bounds(Vector3.zero, Vector3.one * 200f); EditorUtility.SetDirty(vm);   // 形はシェーダーで動くので、見えなくならないよう大きく
                var vM = FxMat("Ruins_SafeVeil", "Fuwa/SafeVeil"); vM.SetFloat("_Len", Mathf.Max(R, shockCorridorLen) * 2.2f); vM.SetFloat("_Alpha", 0.5f); EditorUtility.SetDirty(vM);
                var vo = MeshObj(g, "SafeVeil", vm, vM, new Vector3(0f, 0.025f, 0f), 0, false);
                var vr = vo.GetComponent<Renderer>(); vr.enabled = false; vr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; vr.receiveShadows = false;
                vr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; vr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                sw.safeVeil = vr;
            }
        }
        if (ShockSkyDim)
        {   // 2026-11-15 てじゃ「チャージで空もちょっと暗くするとか」→ 空だけ暗くする大きい球（広場のまわり 400m、内がわ）
            var sp = GameObject.CreatePrimitive(PrimitiveType.Sphere); sp.name = "SkyDim"; Object.DestroyImmediate(sp.GetComponent<Collider>());
            sp.transform.SetParent(g, false); sp.transform.localPosition = Vector3.zero; sp.transform.localScale = Vector3.one * 800f;
            var sdM = FxMat("Ruins_SkyDim", "Fuwa/SkyDim"); sdM.SetColor("_Color", ShockDark ? new Color(0.06f, 0.02f, 0.10f, 1f) : new Color(0.05f, 0.08f, 0.16f, 1f)); EditorUtility.SetDirty(sdM);
            var sr = sp.GetComponent<MeshRenderer>(); sr.sharedMaterial = sdM; sr.enabled = false;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false; sr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; sr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            sw.skyDim = sr;
        }
        sw.chargeSound = Sfx(pivot, "ChargeSfx", "sfx_crystal_charge.wav", 0.9f, 0.7f, 45f);
        sw.boomSound = Sfx(pivot, "BoomSfx", "sfx_shockwave_boom.wav", 1f, 0.6f, 60f);
        sw.hitSound = Sfx(g, "BlownSfx", "sfx_blown.wav", 0.9f, 0f, 30f);
        UdonSharpEditorUtility.CopyProxyToUdon(sw); FuwaStage2WaterTest.NoSync(sw);
        P = new Vector3(P.x, P.y, cz + R - 0.4f);
    }

    // ================= 道（2–4 と同じ） =================
    static Vector3 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

    static void S(float len, float rise = 0f, bool under = true)
    {
        var d = Dir(th) * len + Vector3.up * rise;
        Strip(pathT, "Road" + (segNo++) + (rise != 0f ? "_Slope" : ""), P, d, HW, under);
        P += d;
    }

    static void T(float deg)
    {
        float sg = Mathf.Sign(deg);
        var L = Dir(th + 90f);
        var inner = P + L * HW * sg;
        var q = Quaternion.Euler(0f, -deg, 0f);
        var name = "Turn" + (segNo++);
        var g = new GameObject(name); g.transform.SetParent(pathT, false); g.layer = pathLayer;
        const float Tk = 0.6f; int n = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(deg) / 7.5f));
        var v = new List<Vector3>(); var tr = new List<int>();
        Vector3 r0 = P - L * HW * sg - inner;
        var top = new List<Vector3>();
        for (int i = 0; i <= n; i++) top.Add(inner + Quaternion.Euler(0f, -deg * i / n, 0f) * r0);
        for (int pass = 0; pass < 2; pass++)
        {
            var off = pass == 0 ? Vector3.zero : Vector3.down * Tk;
            for (int i = 0; i < n; i++) Tri(v, tr, inner + off, top[i] + off, top[i + 1] + off, pass == 0 ? Vector3.up : Vector3.down);
        }
        for (int i = 0; i < n; i++)
        {
            var a = top[i]; var b = top[i + 1]; var dn = Vector3.down * Tk;
            var nrm = ((a + b) * 0.5f - inner); nrm.y = 0f;
            Tri(v, tr, a, b, b + dn, nrm); Tri(v, tr, a, b + dn, a + dn, nrm);
        }
        var m = SaveMesh(name, v, tr);
        var vis = m;
        if (PathChamfer > 0f)
        {   // 2026-11-15 面取り：外がわの弧の上のふちを面取り（見た目だけ。当たりは前の形）
            float c = PathChamfer; var v2 = new List<Vector3>(); var t2 = new List<int>();
            var tin = new List<Vector3>(); var tout = new List<Vector3>();
            for (int i = 0; i <= n; i++) { var rr = top[i] - inner; float L2 = rr.magnitude; tin.Add(inner + rr * ((L2 - c) / L2)); tout.Add(top[i] + Vector3.down * c); }
            for (int i = 0; i < n; i++)
            {
                Tri(v2, t2, inner, tin[i], tin[i + 1], Vector3.up);
                Tri(v2, t2, inner + Vector3.down * Tk, top[i] + Vector3.down * Tk, top[i + 1] + Vector3.down * Tk, Vector3.down);
                var nrm = ((top[i] + top[i + 1]) * 0.5f - inner); nrm.y = 0f;
                Tri(v2, t2, tin[i], tin[i + 1], tout[i + 1], nrm + Vector3.up * nrm.magnitude); Tri(v2, t2, tin[i], tout[i + 1], tout[i], nrm + Vector3.up * nrm.magnitude);
                var dn = Vector3.down * Tk;
                Tri(v2, t2, tout[i], tout[i + 1], top[i + 1] + dn, nrm); Tri(v2, t2, tout[i], top[i + 1] + dn, top[i] + dn, nrm);
            }
            vis = SaveMesh(name + "_Vis", v2, t2);
        }
        g.AddComponent<MeshFilter>().sharedMesh = vis; g.AddComponent<MeshRenderer>().sharedMaterial = pathB;
        g.AddComponent<MeshCollider>().sharedMesh = m;
        P = inner + q * (P - inner);
        th += deg;
    }

    // xy 平面の単純な多角形（へこみあり）を三角に分ける（耳切り）。返り値は頂点番号の組
    static List<Vector3Int> EarClipXY(List<Vector3> poly)
    {
        var res = new List<Vector3Int>(); int n = poly.Count; if (n < 3) return res;
        var idx = Enumerable.Range(0, n).ToList();
        float area2 = 0f; for (int k = 0; k < n; k++) { var a = poly[k]; var b = poly[(k + 1) % n]; area2 += a.x * b.y - b.x * a.y; }
        float ori = area2 >= 0f ? 1f : -1f;
        float Cross(Vector3 a, Vector3 b, Vector3 c) => ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * ori;
        bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c) => Cross(a, b, p) >= -1e-7f && Cross(b, c, p) >= -1e-7f && Cross(c, a, p) >= -1e-7f;
        int guard = 0;
        while (idx.Count > 3 && guard++ < 1000)
        {
            bool cut = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                if (Cross(poly[ia], poly[ib], poly[ic]) <= 1e-7f) continue;   // へこんだ角
                bool any = false;
                foreach (int j in idx) { if (j == ia || j == ib || j == ic) continue; if (Inside(poly[j], poly[ia], poly[ib], poly[ic])) { any = true; break; } }
                if (any) continue;
                res.Add(new Vector3Int(ia, ib, ic)); idx.RemoveAt(i); cut = true; break;
            }
            if (!cut) break;
        }
        if (idx.Count == 3) res.Add(new Vector3Int(idx[0], idx[1], idx[2]));
        return res;
    }
    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 want)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var s = b; b = c; c = s; }
        int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static Mesh SaveMesh(string name, List<Vector3> v, List<int> t)
    {
        string path = MeshDir + "/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!m) { m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.Clear(false);
        m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    // シマシマの道：a（手前のはしのまん中、上の面）から d だけ。当たりは1枚の箱、見た目は StripeLen ごとに A/B
    // 面取りしたシマの1枚：上の面は x=±(hw-c)、両はしの角は斜めに c 下がる。vUp＝この中から見た「上」×厚み（坂でもまっすぐ下へ厚み）
    public static float PathChamfer = 0.09f;
    static Dictionary<string, Mesh> stripeCache = new Dictionary<string, Mesh>();
    static Mesh ChamferStripe(float hw, float z0, float z1, Vector3 vUp, float c)
    {
        bool flat = Mathf.Abs(vUp.x) < 1e-4f && Mathf.Abs(vUp.z) < 1e-4f;
        string key = flat ? ("PathStripe_" + hw.ToString("F2") + "_" + (z1 - z0).ToString("F3") + "_" + vUp.y.ToString("F2") + "_" + c.ToString("F2")) : null;
        if (key != null && stripeCache.TryGetValue(key, out var cached) && cached) return cached;
        if (key != null) { float L = z1 - z0; z0 = 0f; z1 = L; }   // 平らな物は形が同じなので使い回す（位置は z0 で動かす…はできないので下でずらす）
        var vn = vUp.normalized; float tk = vUp.magnitude;
        var v = new List<Vector3>(); var t = new List<int>();
        Vector3 T(float x, float z) => new Vector3(x, 0f, z);
        // 断面（左から右）：下左、上の角下左、上左、上右、上の角下右、下右
        Vector3[] Sec(float z) => new[] { T(-hw, z) - vUp, T(-hw, z) - vn * c, T(-hw + c, z), T(hw - c, z), T(hw, z) - vn * c, T(hw, z) - vUp };
        var A = Sec(z0); var B = Sec(z1);
        var cen = (T(0, (z0 + z1) / 2f) - vUp * 0.5f);
        for (int i = 0; i < 6; i++)
        {
            int j = (i + 1) % 6;
            var mid = (A[i] + A[j] + B[i] + B[j]) * 0.25f;
            Tri(v, t, A[i], A[j], B[j], mid - cen); Tri(v, t, A[i], B[j], B[i], mid - cen);
        }
        // 両はしのふた（となりのシマ・道とくっつく面）
        var cA = T(0, z0) - vUp * 0.5f; var cB = T(0, z1) - vUp * 0.5f;
        for (int i = 0; i < 6; i++) { int j = (i + 1) % 6; Tri(v, t, cA, A[i], A[j], Vector3.back); Tri(v, t, cB, B[i], B[j], Vector3.forward); }
        if (key == null) { var me = new Mesh { name = "PathStripeSlope" }; me.SetVertices(v); me.SetTriangles(t, 0); me.RecalculateNormals(); me.RecalculateBounds(); return me; }
        var m = SaveMesh(key, v, t); stripeCache[key] = m; return m;
    }
    static void Strip(Transform parent, string name, Vector3 a, Vector3 d, float hw, bool under = true)
    {
        float len = d.magnitude; if (len <= 0.01f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = a; g.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
        const float Tk = 0.6f;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -Tk * 0.5f, len * 0.5f); bc.size = new Vector3(hw * 2f, Tk, len);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        bool slope = Mathf.Abs(d.y) > 0.001f;
        var vUp = g.InverseTransformDirection(parent.TransformDirection(Vector3.up)).normalized * Tk;
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            if (PathChamfer > 0f)
            {   // 2026-11-15 てじゃ「道の角も面取りして」：両はしの上のふちを面取り（シマの区切りはそのまま＝溝なし）
                var cm = ChamferStripe(hw, l * k, l * (k + 1), slope ? vUp : new Vector3(0f, Tk, 0f), PathChamfer);
                var so2 = new GameObject("S" + k); so2.transform.SetParent(g, false);
                if (!slope) so2.transform.localPosition = new Vector3(0f, 0f, l * k);   // 平らな物は 0〜l の形を使い回すので位置でずらす
                so2.AddComponent<MeshFilter>().sharedMesh = cm; so2.AddComponent<MeshRenderer>().sharedMaterial = (k % 2 == 0) ? pathA : pathB;
                continue;
            }
            if (!slope)
            {
                var s = Box(g, "S" + k, new Vector3(0, -Tk * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, Tk, l), (k % 2 == 0) ? pathA : pathB, 0);
                Object.DestroyImmediate(s.GetComponent<Collider>());
                continue;
            }
            float z0 = l * k, z1 = l * (k + 1);
            Vector3 T(float x, float z) => new Vector3(x, 0f, z);
            var p = new[] { T(-hw, z0), T(hw, z0), T(hw, z1), T(-hw, z1) };
            var q = p.Select(x => x - vUp).ToArray();
            var vv = new List<Vector3>(); var tt = new List<int>();
            void F(Vector3 a2, Vector3 b2, Vector3 c2, Vector3 e2, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(b2 - a2, c2 - a2), want) < 0f) { var tmp = b2; b2 = e2; e2 = tmp; }
                int i0 = vv.Count; vv.Add(a2); vv.Add(b2); vv.Add(c2); vv.Add(e2); tt.Add(i0); tt.Add(i0 + 1); tt.Add(i0 + 2); tt.Add(i0); tt.Add(i0 + 2); tt.Add(i0 + 3);
            }
            var up = Vector3.up; var fw = Vector3.forward;
            F(p[0], p[1], p[2], p[3], up); F(q[0], q[3], q[2], q[1], -vUp);
            F(p[0], p[3], q[3], q[0], Vector3.left); F(p[1], q[1], q[2], p[2], Vector3.right);
            F(p[0], q[0], q[1], p[1], -fw); F(p[3], p[2], q[2], q[3], fw);
            var me = new Mesh { name = "SlopeStripe" }; me.SetVertices(vv); me.SetTriangles(tt, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var so = new GameObject("S" + k); so.transform.SetParent(g, false);
            so.AddComponent<MeshFilter>().sharedMesh = me; so.AddComponent<MeshRenderer>().sharedMaterial = (k % 2 == 0) ? pathA : pathB;
        }
        if (!under) return;
        // 道の下の岩（空に浮いた遺跡の道。ブロックアウトなので箱）
        var u = Box(g, "Under", new Vector3(0, -Tk - 0.6f, len * 0.5f), new Vector3(hw * 2f - 0.3f, 1.2f, Mathf.Max(0.1f, len - (UnderFull ? 0f : 0.2f))), underM, 0);   // 2026-11-15 てじゃ「繋いだ分の下に切れ目」→ 前後 0.1 ずつ短くて、つなぎ目ごとに切れ目があった
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }

    // ================= スタート・ゴール・チェックポイント（2–4 と同じ） =================
    static void CP(string name, float along, float hw = 0f, float mushZ = 1.5f)
    {
        if (hw <= 0f) hw = HW;
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1"); if (!tmpl) return;
        var g = root.Find("Checkpoints"); if (!g) { g = new GameObject("Checkpoints").transform; g.SetParent(root, false); }
        var cp = Object.Instantiate(tmpl, g, false); cp.name = name;
        cp.transform.localPosition = P + Dir(th) * along; cp.transform.localRotation = Quaternion.LookRotation(Dir(th));
        foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        var trig = cp.transform.Find("Trigger");
        if (trig) trig.localScale = new Vector3(hw * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
        var gm = cp.transform.Find("GlowMushrooms");
        if (gm) foreach (Transform cl in gm) cl.localPosition = new Vector3(Mathf.Sign(cl.localPosition.x) * (hw + 0.55f), cl.localPosition.y, mushZ);
        var fc = cp.GetComponentInChildren<FuwaCheckpoint>(true);
        if (fc) { UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc); cps.Add(fc); }
    }

    static void RegisterCheckpoints()
    {
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(); if (!ball) return;
        var so = new SerializedObject(ball); var arr = so.FindProperty("checkpoints");
        for (int k = arr.arraySize - 1; k >= 0; k--) if (arr.GetArrayElementAtIndex(k).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(k);
        foreach (var fc in cps) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fc; }
        so.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon(ball);
    }

    static void RegisterVisibility()
    {
        var visb = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "FuwaAreaVisibility");
        if (!visb) return;
        var so = new SerializedObject(visb); var arr = so.FindProperty("stage2Objects");
        bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == root.gameObject) has = true;
        for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
        if (!has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject; }
        so.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)visb);
    }

    // さいそくタイムの記録係（Records2）の 2–5 のスタートをつなぐ（作り直すと同期物のIDが変わるので、名前と同じく中身だけ書き換え）
    static void LinkRecords()
    {
        var rec = Object.FindObjectsOfType<FuwaRecords>(true).FirstOrDefault(r => r.name == "Records2"); if (!rec || rec.courseStarts == null || rec.courseStarts.Length < 5) return;
        rec.courseStarts[4] = root.Find("Start/BallStart");
        EditorUtility.SetDirty(rec); UdonSharpEditorUtility.CopyProxyToUdon(rec);
    }

    static float StartSet(Transform r)
    {
        var start = new GameObject("Start").transform; start.SetParent(r, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–5] FuwaCourse/Start がない"); return 3f; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_5");
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea"); var ground = area.Find("Ground");
        start.localPosition = new Vector3(-ground.localPosition.x, -islandTop, -ground.localPosition.z);
        FuwaCourse2_1Builder.FixCopied(start, map);
        FuwaCourse2_1Builder.Stage2Grass(start);
        return ground.localScale.x * 0.5f;
    }

    static Vector3 GoalSet(Transform r, Vector3 p, Vector3 dir)
    {
        var goal = new GameObject("Goal").transform; goal.SetParent(r, false);
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–5] FuwaCourse/Goal がない"); return p; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, goal, null, map);
        var gg = goal.Find("GoalGround");
        float far = gg ? gg.localScale.x * 0.5f : 6.1f;
        goal.localPosition = p + dir * far + Vector3.up * 0.02f;
        goal.localRotation = Quaternion.LookRotation(dir);
        IslandJoin(pathT, "GoalJoin", p + dir * far, -dir, far, HW, pathB, FuwaCourse2_4Builder.JoinInset);
        FuwaCourse2_1Builder.FixCopied(goal, map);
        FuwaCourse2_1Builder.Stage2Grass(goal);
        return p + dir * far;
    }

    static void IslandJoin(Transform parent, string name, Vector3 c, Vector3 dir, float R, float hw, Material mat, float inset = 0f)
    {
        const float Tk = 0.6f; int nx = 16;
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = c; g.transform.localRotation = Quaternion.LookRotation(dir); g.layer = pathLayer;
        var v = new List<Vector3>(); var t = new List<int>();
        // 2026-11-11 てじゃ「入口のつなぎ目にスキマ」：島のふちは丸めてあるので、スタート・ゴールの島は中へ inset 入れる（2–4 の FuwaCourse2_4Builder.JoinInset と同じ）
        float Ri = R - inset;
        float Zb(float x) => Mathf.Sqrt(Mathf.Max(0f, Ri * Ri - x * x));
        for (int i = 0; i < nx; i++)
        {
            float x0 = Mathf.Lerp(-hw, hw, i / (float)nx), x1 = Mathf.Lerp(-hw, hw, (i + 1) / (float)nx);
            Vector3 a = new Vector3(x0, 0, Zb(x0)), b = new Vector3(x1, 0, Zb(x1)), cc = new Vector3(x1, 0, R), d = new Vector3(x0, 0, R), dn = Vector3.down * Tk;
            Tri(v, t, a, b, cc, Vector3.up); Tri(v, t, a, cc, d, Vector3.up);
            Tri(v, t, a + dn, cc + dn, b + dn, Vector3.down); Tri(v, t, a + dn, d + dn, cc + dn, Vector3.down);
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            Vector3 a = new Vector3(sx * hw, 0, Zb(hw)), b = new Vector3(sx * hw, 0, R), dn = Vector3.down * Tk;
            Tri(v, t, a, b, b + dn, new Vector3(sx, 0, 0)); Tri(v, t, a, b + dn, a + dn, new Vector3(sx, 0, 0));
        }
        var m = SaveMesh(name, v, t);
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        g.AddComponent<MeshCollider>().sharedMesh = m;
    }

    static void Hazard(Transform r, string name, Vector3 lp, Vector3 size, bool player)
    {
        var g = new GameObject(name); g.transform.SetParent(r, false); g.transform.localPosition = lp; g.layer = player ? 0 : 4;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        if (player) { var pc = UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(g); pc.ball = ballT; UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc); }
        else { var hz = UdonSharpUndo.AddComponent<FuwaHazard>(g); UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    // コースの当たり（トリガー以外）の範囲（コースの座標）
    static Bounds LocalCollidersBounds()
    {
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var cl in root.GetComponentsInChildren<Collider>(true))
        {
            if (cl.isTrigger) continue;
            Vector3 cc, ex; var t = cl.transform;
            if (cl is BoxCollider bc) { cc = bc.center; ex = bc.size * 0.5f; }
            else if (cl is MeshCollider mc && mc.sharedMesh) { cc = mc.sharedMesh.bounds.center; ex = mc.sharedMesh.bounds.extents; }
            else continue;
            for (int k = 0; k < 8; k++)
            {
                var c = cc + new Vector3((k & 1) == 0 ? -ex.x : ex.x, (k & 2) == 0 ? -ex.y : ex.y, (k & 4) == 0 ? -ex.z : ex.z);
                var lp = root.InverseTransformPoint(t.TransformPoint(c));
                if (first) { lb = new Bounds(lp, Vector3.zero); first = false; } else lb.Encapsulate(lp);
            }
        }
        return lb;
    }

    // 2026-11-15 見た目クオリティ（B）：道の上の面に石だたみ（FuwaToonLit の地面の模様を使う。ワールドの xz で取るので、シマシマの区切りをまたいでも石がつながる）
    //  模様は Tools/make_ruin_paving.py の Textures/RuinPaving.png（R＝石だたみ、G・B＝ムラ）。シマシマの色（_Color）はそのまま
    public static bool PathPaving = false;   // 2026-11-15 てじゃ「うーん道の模様いらんかな」→ なし（模様を切るだけ、コードは残す）
    static void PavingOn(Material m)
    {
        if (!PathPaving) { if (m.HasProperty("_GroundFine")) { m.SetFloat("_GroundFine", 0f); m.SetFloat("_GroundPatch", 0f); EditorUtility.SetDirty(m); } return; }
        const string tp = "Assets/FuwaStage2/Textures/RuinPaving.png";
        var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
        if (ti && (ti.sRGBTexture || ti.wrapMode != TextureWrapMode.Repeat)) { ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; ti.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); if (!tex || !m.HasProperty("_GroundTex")) return;
        m.SetTexture("_GroundTex", tex);
        m.SetFloat("_GroundFine", 0.55f); m.SetFloat("_GroundFineScale", 4f); m.SetFloat("_GroundFineFade", 70f); m.SetFloat("_GroundFineCrisp", 0f); m.SetFloat("_GroundFineMask", 0f);
        m.SetFloat("_GroundPatch", 0.18f); m.SetFloat("_GroundPatchScale", 11f); m.SetFloat("_GroundMidScale", 3.3f); m.SetFloat("_GroundSteps", 0f);
        m.SetColor("_GroundTintA", new Color(1.06f, 1.04f, 0.98f)); m.SetColor("_GroundTintB", new Color(0.9f, 0.88f, 0.84f));
        EditorUtility.SetDirty(m);
    }

    // 2026-11-15 てじゃ「草に模様つけよ」：ゴール・ロビーの床と同じ地面の模様（FuwaCourse/Textures/GroundDetail.png）を草の色で。明るい・濃いのまだら＋細かい草の筋
    static void GrassPattern(Material m)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/GroundDetail.png");
        if (!tex || !m.HasProperty("_GroundTex")) return;
        m.SetTexture("_GroundTex", tex);
        m.SetFloat("_GroundPatch", 0.8f); m.SetFloat("_GroundPatchScale", 9f); m.SetFloat("_GroundMidScale", 2.6f); m.SetFloat("_GroundSteps", 3f);
        m.SetColor("_GroundTintA", new Color(1.12f, 1.08f, 0.88f)); m.SetColor("_GroundTintB", new Color(0.72f, 0.86f, 0.7f));
        m.SetFloat("_GroundFine", 0.38f); m.SetFloat("_GroundFineScale", 13f); m.SetFloat("_GroundFineCrisp", 1f); m.SetFloat("_GroundFineFade", 30f); m.SetFloat("_GroundFineMask", 0f);
        EditorUtility.SetDirty(m);
    }
    static Material PathMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_1_PathA.mat"); m = new Material(src); AssetDatabase.CreateAsset(m, path); }
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
}
