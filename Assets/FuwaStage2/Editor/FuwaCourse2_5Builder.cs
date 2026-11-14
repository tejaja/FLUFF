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
    public static float HW = 2.4f, StripeLen = 1.2f;   // 2026-11-13 島の道 2→2.4
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

        pathA = PathMat("Course2_5_PathA", PathColA); pathB = PathMat("Course2_5_PathB", PathColB);   // ロビーの道もこの色（FuwaStage2LobbyCourses）
        stone = FuwaStage2WaterTest.ToonMat("Ruins_Stone", new Color(0.86f, 0.74f, 0.62f));
        terra = FuwaStage2WaterTest.ToonMat("Ruins_Terracotta", new Color(0.82f, 0.48f, 0.33f));
        terraD = FuwaStage2WaterTest.ToonMat("Ruins_TerracottaDark", new Color(0.62f, 0.34f, 0.24f));
        moss = FuwaStage2WaterTest.ToonMat("Ruins_Moss", new Color(0.55f, 0.72f, 0.45f));
        underM = FuwaStage2WaterTest.ToonMat("Ruins_Under", new Color(0.72f, 0.58f, 0.48f));
        glow = FuwaStage2WaterTest.ToonMat("Ruins_PortalRim", new Color(0.55f, 0.9f, 1f));
        glow.SetColor("_EmissionColor", new Color(0.35f, 0.8f, 1f) * 1.4f); EditorUtility.SetDirty(glow);
        iron = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        maskM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Ruins_PortalMask.mat");
        if (!maskM) { maskM = new Material(Shader.Find("Fuwa/PortalMask")); AssetDatabase.CreateAsset(maskM, "Assets/FuwaStage2/Materials/Ruins_PortalMask.mat"); }
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Meshes", "Course2_5");
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballT = Resources.FindObjectsOfTypeAll<FuwaBall>().FirstOrDefault(b => b.gameObject.scene.IsValid() && b.transform.parent && b.transform.parent.name == "FuwaSystem");
        ballR = ballT ? ballT.GetComponent<SphereCollider>().radius * ballT.transform.lossyScale.x : 0.15f;
        cps.Clear(); segNo = 0; stencilNo = 10; FuwaPortalTest.clones.Clear(); camPortals.Clear(); gateZ.Clear();
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
        Run(() => { ArchBridge(12f, 0f, true, null); });
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
        Run(() => { S(1f); ArchGate(); Curtain("C1_Normal", 0); S(1.5f); S(5f); warpA = BigPortal(isl, "WarpA", P, 180f); });   // 2026-11-14 てじゃ「下層の普通もどしの後も、ポータルまでの距離ちょい伸ばしたい」白→ワープ 3.5 → 6.5m
        // 2026-11-14 てじゃ「下の地面は道に沿った部分以外なくして、床下のゴール部分は石像の下においてみよ」→ 道ぞいの帯＋石像の台地（中に空洞）
        var cav = PathHugRock(i1);
        // ---- 空中の巣の回廊（①の島の +x・-z の角の上から、-x へ ①の上をわたる）----
        //  2026-11-14 ①が短くなったので、前の位置（①の範囲から決めていた所）に固定
        P = WebWalkStart; th = 200f;
        { // 2026-11-14 てじゃ「その島、石像基準に反時計回りに回り込むようにちょっとだけ動かして」→ 石像を中心に上から見て反時計回りに WebWalkOrbit°（向きもいっしょに）
            float o = WebWalkOrbit * Mathf.Deg2Rad, dx = P.x - MushroomAt.x, dz = P.z - MushroomAt.y;
            P = new Vector3(MushroomAt.x + dx * Mathf.Cos(o) - dz * Mathf.Sin(o), P.y, MushroomAt.y + dx * Mathf.Sin(o) + dz * Mathf.Cos(o)); th += WebWalkOrbit; }
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
        float wh0 = WebH, ch0 = CurtainH, cth0 = CurtainTrigH; WebH = TunnelH - 0.15f; CurtainH = TunnelH - 0.4f; CurtainTrigH = TunnelH + 7f;   // 当たりも低く（真上の上の段の島まで届かないように）
        Run(() => { runA = isl; warpAin = BigPortal(isl, "WarpAIn", Vector3.zero, 0f); ArrivalRoof(false); S(2.5f); CPi("Checkpoint1", 0.8f); S(1.5f);
            // 2026-11-14 てじゃ「最初のクモの巣やっぱいらんかもね」→ ふつうの大きさの巣（左2つ）はなし
            // 2026-11-14 てじゃ「大きくなったあとのクモの巣をもう一個ふやしたい、入ってきた側の道を伸ばして追加」→ 下2つ（上を越える）を足す
            // 2026-11-14 てじゃ「そもそも順番かえよ、今下段2個→上段2個のとこを逆に」→ 右下 → 上2つ → 下2つ
            // 2026-11-14 てじゃ「上段2個、いみないかもな、左側2個にしよう」→ 右下 → 左2つ → 下2つ
            // 2026-11-14 てじゃ「序盤のも、1〜4枚じゃなくてデカイやつの一部が大きく破れてる形式に」→ 大きい巣1枚に大きな裂け目（右下ななめ／左たて／上よこ）。前の版：右下 / 左2 / 下2（WebGates）
            S(1f); Curtain("C2_Big", 1); S(1.5f);
            TearGates("B1", new[] { new Tear(0.9f, -1.9f, 2.5f, -0.6f, 1.0f, true), new Tear(-2.3f, -1.1f, -2.3f, 1.1f, 0.95f, true), new Tear(-1.6f, 1.55f, 1.6f, 1.55f, 0.95f, false) }); });
        T(-40f);
        //  2026-11-14 てじゃ「クモの巣もっと小さくしよ、4つで埋まるくらい、それを組み合わせて使う、左下だけ、右下と左上、一箇所しかあいてない、とか」
        //  → 断面を田の字に4分割した小さい巣（WebGates）。前の版：左右交互の大きい巣（WebPits）＋天井の巣（LowWeb）
        // 2026-11-14 てじゃ「カーブ後の蜘蛛の巣の位置に、小さくなるカーテンつけよう」→ 曲がってすぐの門（左下＋右上）を青カーテンに
        // 2026-11-14 てじゃ「青カーテン以降の蜘蛛の巣、もっと小さいスキマを小さいふわふわで通り抜ける感じにしよう」→ 4つの門を「小さい穴1つの巣」に（穴の位置は 左下・右上・まん中・右下）
        //  前の版：右上だけあき / 右2 / 右下＋左上 / 左上だけあき（WebGates B2・B3・B4）
        Run(() => { runB = isl; S(1f); S(WebGateGap / 2f); Curtain("C2_Small", -1); S(WebGateGap / 2f);
            // 2026-11-14 てじゃ「スキマ通し、穴よりもさっきみたいなクモの巣が複数あるとこのスキマを通す感じに」→ 四角い巣2枚の間の細いスキマ（左寄りたて／上よこ／右寄りたて／下よこ）。前の版：穴1つの巣（HoleGates）
            // 2026-11-14 てじゃ「1枚ものを一部を裂くイメージ」「1〜2ライン裂けてる感じ」「斜めもあり」→ 細い裂け目（たて左／ななめ上／ななめ右／よこ下）。前の版：スキマ（SlitGates）
            TearGates("B2", new[] { new Tear(-1.6f, -0.5f, -1.6f, 0.9f, 0.38f, false), new Tear(-0.7f, 1.3f, 1.0f, 1.9f, 0.38f, false) }); S(1f);
            TearGates("B4", new[] { new Tear(1.3f, -1.3f, 2.2f, 0.2f, 0.38f, false), new Tear(-0.9f, -1.6f, 0.9f, -1.6f, 0.38f, false) });
            S(0.3f); Curtain("C2_Normal", 0); S(6.5f); });   // 2026-11-14 てじゃ「最後のクモの巣なくしてそこ普通もどしでいいかも」「カーテンポータル間を広げたいからクモの巣消してカーテン手前に」→ 白は最後の門のすぐ後、白→ワープW 6.5m（前は左下だけの門 B5 → 白 → 2.5m）
        WebH = wh0; CurtainH = ch0; CurtainTrigH = cth0;
        // 2026-11-14 てじゃ「クモの巣ゾーン、壁である必要なくなったし柱式にもどしたい」（カメラ式ポータルで空が見えても問題なくなった）→ 列柱＋巣の門（柱2本＋はり）
        if (CorridorPillars) CorridorColonnade(runA, runB); else CorridorTunnel(runA, runB, -40f, -1.6f);
        { // CP の光るキノコは道のふちの外（壁の中）にあったので、トンネルの中の壁ぎわへ
            var cpg = runA.Find("Checkpoints/Checkpoint1/GlowMushrooms");
            if (cpg) foreach (Transform c in cpg) c.localPosition = new Vector3(Mathf.Sign(c.localPosition.x) * (HW - 0.3f), c.localPosition.y + 0.66f, c.localPosition.z);
        }
        Transform warpW = null;
        Run(() => { warpW = BigPortal(isl, "WarpW", P, 180f); });
        HW = hwSave; pitTable = null; TileL = tileL0;
        // 2026-11-14 落とし穴の下に地面があると落ちても立ててしまうので、回廊の下の地面はなし（トンネルだけ浮いている）
        //IslandRock(iw, 1.5f, false, 3f, 180f + WebWalkOrbit);
        // ---- 上の段（①②の上、向きを変える）----
        // てじゃ「上の二島、下の二島とクロスしたかんじのほうがいい」：上から見て X 字。下の段のスタート→ワープA の向き d に直角（n）で、まん中の上を横切る
        //  2026-11-14 ①が短くなって②の島もなくなったので、前の位置（スタート→前のワープA のまん中の上）に固定
        P = UpperStart; th = UpperStartHeading;
        float upTh = th;
        var i3 = Island("I3_Gravity");
        Transform warpWin = null;
        Run(() => { warpWin = BigPortal(isl, "WarpWIn", Vector3.zero, 0f); S(2f); CPi("Checkpoint2", 0.8f); S(1.5f); ArchGate(); Curtain("C3_Small", -1); S(1.5f); GravRun("S", 9f); S(1.2f); ArchGate(); Curtain("C3_Big", 1); S(1.5f); GravRun("B", 9f); S(2f); });
        IslandRock(i3, 3f, false, 5f, upTh);
        Island("Bridge3");
        Run(() => { ArchBridge(14f, 2f, false, null); });
        var i4 = Island("I4_Choice");
        Run(() => { S(1.5f); CPi("Checkpoint3", 0.8f); S(1f); Choice("C4a"); S(1f); Stones("M", 13.2f); S(2f); });
        T(-90f);
        Run(() => { S(1f); Choice("C4b"); S(1f); WebRun("M", 6f, 1.8f); S(2f); });
        T(90f);
        Run(() => { S(1f); Choice("C4c"); S(1f); GravRun("M", 7f); S(2f); });
        Transform warpB = null;
        Run(() => { warpB = BigPortal(isl, "WarpB", P, 180f); });
        IslandRock(i4, 3f, false, 5f, upTh);
        // ---- ①の島の中の空洞（ゴール）----
        var gc = Island("Cavern");
        P = cav.entry; th = cav.heading;
        Transform warpBin = null;
        Run(() => { warpBin = BigPortal(isl, "WarpBIn", Vector3.zero, 0f); S(2.5f); });
        var goalP = P;
        GoalSet(gc, P, Dir(th));
        if (warpA && warpAin) Link(warpA, warpAin, iw, null, true, true);
        if (warpW && warpWin) Link(warpW, warpWin, i3, null, true, true);
        if (warpB && warpBin) Link(warpB, warpBin, gc, null, true, true);
        // 2026-11-13 てじゃ「ここにきのこ石像をおこう」（①の島の上、石の道と上の段と巣の回廊にかこまれた広場）「サイズ調整いるかも」
        if (!MushStatue(new Vector3(MushroomAt.x, i1Top, MushroomAt.y)))
            GiantMushroom(new Vector3(MushroomAt.x, i1Top, MushroomAt.y), MushroomH, MushroomCapR, false);
        DecoIslets();
        log.Append("下の段 2・上の段 2・空洞のゴール ");
        return goalP;
    }

    // 島の岩（中に空洞）：上の地面の板、まわりの岩の壁、空洞の床。-x がわは洞窟の口（外から見える・光が入る）
    //  返り値：出口のポータルを置く所（空洞の +x の壁の内がわ、床の高さ）と、そこから進む向き（-x）
    // 空中の巣の回廊の置き場所：①の島の道の範囲の +x・-z の角から WebWalkOut、高さ WebWalkUp（てじゃの図の青◯）
    public static Vector3 WebWalkOut = new Vector3(2f, 0f, -2f); public static float WebWalkUp = 12f;   // （前の決め方、今は WebWalkStart に固定）
    public static Vector3 WebWalkStart = new Vector3(55.152f, 12f, 14.939f); public static float WebWalkOrbit = 25f, WebWalkBack = 0f;   // 巣の門を1つ足して +3.8、最初の巣をなくして −3.8   // 2026-11-14 15° → トンネルが伸びたので「また反時計回りにまわして」25°
    public static Vector3 UpperStart = new Vector3(49.0348f, 32.8087f, 17.1244f); public static float UpperStartHeading = 134.3111f;
    // ワープの到着地点の屋根（てじゃ「屋根付きのポータル到着地点」）：ポータルを囲む4本柱＋屋根。P は今の位置（ポータルの面）
    static void ArrivalRoof(bool full = true)
    {
        float hx = HW + 0.35f, z0 = P.z - 1.6f, z1 = P.z + 1.4f, h = FuwaPortalTest.HoleH + 1.1f;
        var g = new GameObject("ArrivalRoof").transform; g.SetParent(isl, false);
        // 床：道（P から先）と重ならないように、ポータルのうしろの部分と、道の左右の外だけ（同じ高さの面が重なるとちらつく）
        float fw = hx + 0.3f, zb = z0 - 0.3f, zf = z1 + 0.3f;
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
    public static float CaveMouthW = 9f; public static bool GoalOpen = true;
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
            float room = Mathf.Max(Mathf.Abs(v) - hx, Mathf.Abs(u) - hz);
            float mouth = Mathf.Max(Mathf.Abs(v) - CaveMouthW / 2f, Mathf.Max(-u, u - (StatueRockR + 6f)));
            return Mathf.Min(room, mouth);
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
            if (L.cave && L.y0 < cy1 + 0.01f && L.y1 > cy0 - 0.01f) f = Mathf.Max(f, -dCave(q));
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
        for (int k = 0; k < layers.Count; k++)
        {
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
        // 上の面
        int a = -1;
        for (int i = i0; i <= i1; i++)
        {
            bool vis = i < i1 && !Full(above, i, j);
            if (vis && a < 0) a = i;
            if (!vis && a >= 0) { RockQuad(v, tTop, P3(a, j, y1), P3(i, j, y1), P3(i, j + 1, y1), P3(a, j + 1, y1), Vector3.up); a = -1; }
        }
        a = -1;
        for (int i = i0; i <= i1; i++)
        {
            bool vis = i < i1 && !Full(below, i, j);
            if (vis && a < 0) a = i;
            if (!vis && a >= 0) { RockQuad(v, tBot, P3(a, j, y0), P3(i, j, y0), P3(i, j + 1, y0), P3(a, j + 1, y0), Vector3.down); a = -1; }
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
        var mats = smr.sharedMaterials.Select(m =>
        {
            string n = m ? m.name : "";
            if (n.Contains("Moss")) return moss;
            if (n.Contains("Dark")) return FuwaStage2WaterTest.ToonMat("Ruins_StatueDark", new Color(0.45f, 0.42f, 0.42f));
            if (n.Contains("Base")) return FuwaStage2WaterTest.ToonMat("Ruins_StatueBase", new Color(0.80f, 0.74f, 0.66f));
            return FuwaStage2WaterTest.ToonMat("Ruins_StatueStone", new Color(0.88f, 0.84f, 0.78f));
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
        DecoIslets();
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
    static void ArchBridge(float len, float rise, bool pits, float[] windsAt)
    {
        float hw0 = HW; HW = BridgeHW;
        float y0 = P.y; float z0 = P.z;
        if (pits && Mathf.Abs(rise) < 0.01f) WindRun("Bridge", len, windsAt ?? new float[0]);
        else S(len, rise);
        // 橋脚とアーチ（見た目だけ）
        int n = Mathf.Max(2, Mathf.RoundToInt(len / 4f));
        for (int i = 0; i <= n; i++)
        {
            float z = z0 + len * i / n, y = y0 + rise * i / n;
            foreach (float sx in new[] { -1f, 1f })
            {
                var pier = Box(isl, "Pier", new Vector3(sx * (HW - 0.35f), y - 0.6f - 3.5f, z), new Vector3(0.7f, 7f, 0.9f), stone, 0); Object.DestroyImmediate(pier.GetComponent<Collider>());
            }
            if (i < n)
            {
                float zm = z0 + len * (i + 0.5f) / n, ym = y0 + rise * (i + 0.5f) / n;
                var arch = Box(isl, "Arch", new Vector3(0f, ym - 1.3f, zm), new Vector3(2f * HW, 0.5f, len / n - 0.9f), stone, 0); Object.DestroyImmediate(arch.GetComponent<Collider>());
            }
        }
        // 低いへり
        foreach (float sx in new[] { -1f, 1f })
        {
            var rail = Box(isl, "Parapet", new Vector3(sx * (HW + 0.1f), y0 + rise / 2f + 0.15f, z0 + len / 2f), new Vector3(0.2f, 0.3f, len), stone, 0);
            rail.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(rise, len) * Mathf.Rad2Deg, 0f, 0f);
        }
        HW = hw0;
    }

    // 崩れたアーチの門（カーテンの柱の外に、上が少し欠けたアーチ）
    static void ArchGate()
    {
        float w = 2f * HW + 1.6f, h = CurtainH + 1.2f;
        foreach (float sx in new[] { -1f, 1f }) Box(isl, "GatePillar", new Vector3(sx * (w / 2f), P.y + h / 2f - 0.6f, P.z), new Vector3(0.9f, h + 0.6f, 1.1f), terra, 0);
        Box(isl, "GateTopL", new Vector3(-w / 4f - 0.3f, P.y + h + 0.2f, P.z), new Vector3(w / 2f, 0.8f, 1.1f), terra, 0);
        var br = Box(isl, "GateTopBroken", new Vector3(w / 4f + 0.2f, P.y + h + 0.1f, P.z), new Vector3(w / 4f, 0.7f, 1.1f), terraD, 0);
        br.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
    }

    // ---- 列柱の回廊（2026-11-14 てじゃ「柱式にもどしたい」）：道の両わきに柱、巣の門の所は柱2本＋上のはり（巣はその間に張る）----
    public static bool CorridorPillars = true, GateBeams = false, PlainColumns = false;
    public static float PillarStep = 2.6f, PillarSize = 0.8f;
    static void CorridorColonnade(Transform runA, Transform runB)
    {
        float H = TunnelH, xc = HW + 0.5f;   // 巣の横のはし（HW+0.5）が柱のまん中に来る
        foreach (var run in new[] { runA, runB })
        {
            var g = new GameObject("Colonnade").transform; g.SetParent(run, false);
            float end = LocalBounds(run, run).max.z, z0 = run == runA ? 2.6f : 0.6f;   // A はワープの到着の床のあとから
            var gz = gateZ.Where(x => x.run == run).Select(x => x.z).ToList();
            foreach (float z in gz)
            {
                foreach (float sx in new[] { -1f, 1f })
                    Box(g, "GatePillar", new Vector3(sx * xc, (H + 0.6f) / 2f - 0.6f + 0.3f, z), new Vector3(PillarSize, H + 0.9f, PillarSize), stone, 0);
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
    static void DecoIslets()
    {
        var g = new GameObject("DecoIslets").transform; g.SetParent(islandsT, false);
        var all = LocalBounds(islandsT, root);
        var rnd = new System.Random(5);
        // 道にかぶらないように：島ごとの範囲（少し広げる）に入ったら引き直す（2026-11-13 空中の回廊を足したら小島とかぶりそうだった）
        var keep = new List<Bounds>();
        foreach (Transform ch in islandsT) if (ch != g) { var b = LocalBounds(ch, root); if (b.size.sqrMagnitude > 0.01f) { b.Expand(new Vector3(8f, 10f, 8f)); keep.Add(b); } }
        for (int i = 0; i < 9; i++)
        {
            var c = Vector3.zero;
            for (int k = 0; k < 30; k++)
            {
                c = new Vector3(Mathf.Lerp(all.min.x - 8f, all.max.x + 8f, (float)rnd.NextDouble()), Mathf.Lerp(-6f, 16f, (float)rnd.NextDouble()), Mathf.Lerp(all.min.z, all.max.z, (float)rnd.NextDouble()));
                if (!keep.Any(kb => kb.Contains(c))) break;
            }
            float s = 2f + 3f * (float)rnd.NextDouble();
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
    static void CPi(string name, float along)
    {
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1"); if (!tmpl) return;
        var g = isl.Find("Checkpoints"); if (!g) { g = new GameObject("Checkpoints").transform; g.SetParent(isl, false); }
        var cp = Object.Instantiate(tmpl, g, false); cp.name = name;
        cp.transform.localPosition = P + Dir(th) * along; cp.transform.localRotation = Quaternion.LookRotation(Dir(th));
        foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        var trig = cp.transform.Find("Trigger");
        if (trig) trig.localScale = new Vector3(HW * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
        var gm = cp.transform.Find("GlowMushrooms");
        if (gm) foreach (Transform cl in gm) cl.localPosition = new Vector3(Mathf.Sign(cl.localPosition.x) * (HW + 0.55f), cl.localPosition.y, 1.5f);
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
        foreach (float sx in new[] { -1f, 1f })
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
        vm.mainTexture = curtainFade; vm.SetColor("_TintColor", new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f, 0.45f)); EditorUtility.SetDirty(vm);
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
    static void WindRun(string tag, float len, float[] windsAt)
    {
        float z0 = P.z; int rows = Mathf.Max(1, Mathf.RoundToInt(len / TileL)); float rl = len / rows;
        // 回廊（pitTable あり）はマスの長さをぴったり TileL に（穴の長さを決めたいので）。あまりは最後の穴なしの列に
        float extra = 0f; if (pitTable != null) { rows = Mathf.Max(1, Mathf.FloorToInt(len / TileL)); rl = TileL; extra = len - rows * rl; }
        int cols = 4; float cw = 2f * HW / cols;
        var g = new GameObject("WindRun_" + tag).transform; g.SetParent(isl, false);
        if (extra > 0.01f) for (int c = 0; c < cols; c++) Box(g, "Tile", new Vector3(-HW + cw * (c + 0.5f), P.y - 0.3f, z0 + rows * rl + extra * 0.5f), new Vector3(cw, 0.6f, extra), rows % 2 == 0 ? pathA : pathB, pathLayer);
        for (int r = 0; r < rows; r++)
        {
            var tbl = pitTable ?? PitRows; var holes = tbl[(pitRow++) % tbl.Length];
            for (int c = 0; c < cols; c++)
            {
                if (holes.Contains(c)) continue;
                Box(g, "Tile", new Vector3(-HW + cw * (c + 0.5f), P.y - 0.3f, z0 + rl * (r + 0.5f)), new Vector3(cw, 0.6f, rl), r % 2 == 0 ? pathA : pathB, pathLayer);   // 2026-11-14 タイルの間の 4cm のすき間から空が線に見えたのでなし
            }
        }
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
                Box(g, "Stone", new Vector3(-hwF + cw * (c + 0.5f), y - 0.3f, zc + rl * 0.5f), new Vector3(cw - 0.04f, 0.6f, rl - 0.04f), r % 2 == 0 ? pathA : pathB, pathLayer);
            }
            zc += rl;
        }
        len = zc - z0;   // 切れ目で長さが変わる
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
        var wa = w.GetComponentInChildren<FuwaWindArea>(true);
        if (wa) { wa.strength = WeakWind; wa.onSeconds = WeakWindOn; wa.offSeconds = WeakWindOff; wa.phaseOffset = phase; wa.alternate = true; UdonSharpEditorUtility.CopyProxyToUdon(wa); FuwaStage2WaterTest.NoSync(wa); }
        log.Append("弱い横風" + tag + " ");
    }

    static void WindStatues(Transform g, float z, float phase)
    {
        var tmpl = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "Gimmick1_CrossWind" && t.parent && t.parent.name == "BuildTemplates" && t.parent.parent && t.parent.parent.name == "FuwaCourse" && t.gameObject.scene.IsValid());
        if (!tmpl) { log.Append("（横風の見本がない）"); return; }
        var w = Object.Instantiate(tmpl.gameObject, g, false); w.name = "CrossWind"; w.SetActive(true);
        w.transform.localPosition = new Vector3(0f, P.y, z); w.transform.localRotation = Quaternion.identity;
        foreach (Transform c in w.transform) c.localPosition = new Vector3(c.localPosition.x, 2.6f, c.localPosition.z);
        var cwT = w.transform.Find("CrossWind"); if (cwT) cwT.localScale = new Vector3(2f * HW + 10f, 5.2f, 3.6f);
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
            float x0 = left ? -HW - 0.5f : -HW + gap, x1 = left ? HW - gap : HW + 0.5f;
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
            float x0 = left ? -HW - 0.5f : -HW + gap, x1 = left ? HW - gap : HW + 0.5f;
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
    static readonly List<(Transform run, float z)> gateZ = new List<(Transform, float)>();
    static void WebGates(string tag, float len, int[] patterns, bool pits)
    {
        float z0 = P.z; len = patterns.Length * WebGateGap;
        var g = new GameObject("WebGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, hMid = TunnelH / 2f, xo = HW + 0.5f;   // 横は壁に 0.5m めりこませる
        for (int i = 0; i < patterns.Length; i++)
        {
            float z = z0 + len * (i + 0.5f) / patterns.Length; int pt = patterns[i];
            gateZ.Add((isl, z));
            for (int c = 0; c < 4; c++)
            {
                if ((pt & (1 << c)) == 0) continue;
                bool right = (c & 1) != 0, up = (c & 2) != 0;
                float x0 = right ? 0f : -xo, x1 = right ? xo : 0f, y0 = up ? hMid : 0f, y1 = up ? hTop : hMid;
                Web(g, "Web" + i + "_" + (up ? "U" : "L") + (right ? "R" : "L"), new Vector3((x0 + x1) / 2f, P.y + (y0 + y1) / 2f, z), Quaternion.identity, new Vector2(x1 - x0, y1 - y0), false, 0.6f);
            }
        }
        // 床：門ごとの区間（WebGateGap）で作る。2026-11-14 てじゃ「穴の次が下段2個クモの巣はキツい」→ 下2つが埋まった門（上を越える）の区間は穴なし
        if (!pits) { S(len); return; }
        for (int i = 0; i < patterns.Length; i++)
        {
            bool lowBlocked = (patterns[i] & (WLL | WLR)) == (WLL | WLR);
            if (lowBlocked) S(WebGateGap); else WindRun("Pits_" + tag + "_" + i, WebGateGap, new float[0]);
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
    public static bool OvalPortals = true;
    public static float OvalLift = 0.15f;   // 輪の下のはしの高さ
    static float OvalA => FuwaPortalTest.HalfW;
    static float OvalB => FuwaPortalTest.HoleH / 2f;
    static Vector3 OvalC => new Vector3(0f, OvalLift + OvalB, 0f);
    static void OvalStyle(Transform p)
    {
        var fr = p.Find("PortalFrame"); if (fr) Object.DestroyImmediate(fr.gameObject);
        var ring = new GameObject("OvalFrame").transform; ring.SetParent(p, false); ring.localPosition = OvalC;
        var o = new GameObject("StoneRing"); o.transform.SetParent(ring, false);
        o.AddComponent<MeshFilter>().sharedMesh = EllipseTube(OvalA + 0.24f, OvalB + 0.24f, 0.17f, 64, 10, "Portal_OvalStone");
        o.AddComponent<MeshRenderer>().sharedMaterial = stone;
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
    public static int PortalRTSize = 1024;
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
            c.enabled = false; c.targetTexture = rt; c.clearFlags = CameraClearFlags.Skybox; c.nearClipPlane = 0.05f; c.farClipPlane = 2000f;
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
        f.AddComponent<MeshFilter>().sharedMesh = d; f.AddComponent<MeshRenderer>().sharedMaterial = terraD;
        var bk = new GameObject("Back"); bk.transform.SetParent(n, false); bk.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        bk.AddComponent<MeshFilter>().sharedMesh = d; bk.AddComponent<MeshRenderer>().sharedMaterial = stone;
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
    static void Web(Transform parent, string name, Vector3 lp, Quaternion lr, Vector2 size, bool tear, float playerSlow, bool smallPasses = false, bool rect = false)
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

    // 裂けた巣の門（2026-11-14 てじゃ「1枚ものを一部を裂くイメージ」「1〜2ライン裂けてる感じ」「斜めもあり」「序盤のも、1〜4枚じゃなくてデカイやつの一部が大きく破れてる形式に」）
    //  道いっぱいの四角い巣1枚に、tearA→tearB の線のまわり tearW の裂け目（巣の中心から、x 右・y 上、m）。当たりは裂け目のカプセル（ふわふわの半径込み）
    struct Tear { public Vector2 a, b; public float w; public bool pits; public Tear(float ax, float ay, float bx, float by, float w, bool pits) { a = new Vector2(ax, ay); b = new Vector2(bx, by); this.w = w; this.pits = pits; } }
    static void TearGates(string tag, Tear[] tears)
    {
        float z0 = P.z;
        var g = new GameObject("TearGates_" + tag).transform; g.SetParent(isl, false);
        float hTop = TunnelH - 0.1f, xo = HW + 0.5f;
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
        float hTop = TunnelH - 0.1f, xo = HW + 0.5f, hw = SlitW / 2f;
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
        float hTop = TunnelH - 0.1f, xo = HW + 0.5f;
        for (int i = 0; i < holes.Length; i++)
        {
            float z = z0 + WebGateGap * (i + 0.5f);
            gateZ.Add((isl, z));
            HoleWeb(g, "HoleWeb" + i, new Vector3(0f, P.y + hTop / 2f, z), new Vector2(2f * xo, hTop), holes[i], SmallHoleR);
        }
        S(len);
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
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = pathB;
        g.AddComponent<MeshCollider>().sharedMesh = m;
        P = inner + q * (P - inner);
        th += deg;
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
        var u = Box(g, "Under", new Vector3(0, -Tk - 0.6f, len * 0.5f), new Vector3(hw * 2f - 0.3f, 1.2f, Mathf.Max(0.1f, len - 0.2f)), underM, 0);
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
