using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–2「ほのおのやま」ブロックアウト（2026-10-29 てじゃ「試しに作ってみよう」→ 試して「しっくりこない、ちゃんとプロット決める」→ てじゃの流れで作り直し）。
// 熱のしくみ（FuwaHeat）：火を通るとふわふわが火の決まった所で止まって浮く／浮きすぎると燃える→撃って消す／燃え尽きると黒焦げ→一瞬止まってアウト。
// 流れ（ロビーの Slot1 方向に放射状、+Z が進む向き。y=0 がスタートの島の地面）：
//  ① スタート → くねくねの細い上り坂。角の地面に火種2つ（1つ目は撃って当たる、2つ目は坂の入口の噴気孔に斜めに吹っ飛ばされて乗る→待つと燃える→撃って消す、を体験）
//  ② 広い道（ゆるいS字）に溶岩の穴。火の玉が穴から穴へ往復（決まったタイミング、2つ・2つ・3つの穴でだんだん難しく）。当たると燃えてちょっと押される、人はちょっと吹っ飛ぶ
//  ③ 鋭角のグネグネ（登り）：丸い踊り場の火種で止めて切り返す ×3
//  ④ ほのおのまもの：ふつうのまものと同じ、体当たりで燃える。両脇の水たまりに落とすと倒せる（燃えたふわふわは水で消える）
//  ⑤ くねくね道（一部坂）：横の岩の穴から火炎放射（当たると黒焦げアウト、人はちょっと吹っ飛ぶ）。手前の火種で浮かせて待てる
//  ⑦ ゴール（コース1のゴールの島を写す）
// 道はシマシマ（赤系、Course2_2_PathA/B）。くねくね道は1本のつなぎ目なしのメッシュ（Route/Ribbon）、ジグザグは板と丸い踊り場
public static class FuwaCourse2_2Builder
{
    const string RootName = "FuwaCourse2_2";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float PathHW = 1.5f, StripeLen = 1.2f;
    public static float NarrowHW = 1.0f;                       // ① の細い坂の幅の半分
    public static float SeedHover = 1.3f;                      // 火種の止まる所（地面から）
    public static float TailWindStrength = 3.5f;
    public static float VentAt = 2.4f, VentFwd = 6f, VentUp = 3.5f;   // 噴気孔：長い坂の角からの距離、打ち出しの水平・上の速さ（8.4m 先で）
    public static float WideHW = 4.5f, WideEndHW = 3.2f, HoleR = 1.2f, LavaDepth = 0.45f, FireballTop = 2.55f, VertTop = 2.55f, FireballSize = 1.5f;   // ② 広い道・溶岩の穴・火の玉（てっぺんの道からの高さ・大きさ）。2026-10-30 てじゃ「穴の溶岩もうちょい上に」→ 0.9/0.5 → 0.45/0.35（火の玉のてっぺんは道からの高さで据え置き）
    public static float ChanHW = 0.6f, ChanLavaDepth = 0.35f, LavaFallLen = 6f;   // 溝の幅の半分・溝のある穴の溶岩の深さ（道の下の岩 0.6 より上＝ふちからそのまま流れ出る）・垂れる溶岩の長さ
    public static float PadR = 2.6f, PadIn = 1.6f, PadDrop = 0.03f, ZigLeg = 15f, ZigRise = 2.0f, ZigHW = 0.8f, ZigHeading = 71f, EndPadR = 3.4f, EndPadIn = 1.0f, FireWallH = 4.5f, FireWallOut = 1.0f, FireWallGap = 0.35f;   // 踊り場の半径・板のはしが踊り場に入る深さ（40° の板どうしが踊り場の外で重ならない：0.8*2/sin40=2.5）・板の長さ・上り・幅の半分・向き
    // （2026-10-30 てじゃ「もっと角度急に」→ 約155° 曲がる（道どうしが約26°）。板を踊り場の中心から通すと踊り場の外で重なるので、板の出る所は中心より前（+z）へ、着く所は後ろへ幅の半分ずらす（角で入る板と出る板が中心の前後に分かれる）。そのぶん板が寝るので向き ZigHeading は 71°。PadIn は板のはしが踊り場に入る深さ）
    // （2026-10-29 てじゃ「ジグザグ、ゆるすぎる」→ 細く（幅3m→1.6m）＋角を鋭く（120°→140° 曲がる）＋板を長く（10→13m）。ヘアピンの1本道も試したが、鋭い角を丸めると角が6mも内に入ってだめ）
    // ④ 2026-10-30 てじゃ「もうちょい広いエリア＋まものは4ひきくらい、周りに水じゃなくて所々に水たまり」→ 広い道の中に丸い水たまりの穴
    public static float PuddleR = 1.7f, MonsterFlameTouch = 0.95f;
    public static float ArenaHW = 6f, ArenaLen = 27f, ArenaShift = 6.5f, PondW = 3.5f, PondLen = 10f, PondSurface = -0.2f;
    // ⑤ 2026-10-30 てじゃ「最後はみじかめ、ちょっとの坂→火口→火口の周りに道、左右どっちから進んでもok。火口からは一定間隔でマグマが吹き出て道の一部にながれてくる、
    //    タイミングを見て進む。最後は道が合流してゴール」「マグマは即死」。火炎放射の坂（横・下・斜め、当たると吹っ飛んで炎上）は「びみょう」でやめた
    public static Vector3 VolcanoAt = new Vector3(18f, 0f, 14f);   // 火山の中心（ジグザグのてっぺんの踊り場から）。2026-10-30 てじゃ「山はもっとふとめに」→ 太くしたぶん右へ
    public static float VolcanoTopH = 16f, VolcanoTopR = 5f, VolcanoR0 = 12f, VolcanoBottom = -60f, RockFirstScale = 3f;   // 火口の高さ・半径、道の高さでの半径、すその下のはし、火口から飛んでくる1回の長さ（倍）
    // ⑤ 2026-10-30 てじゃ「まものからをもっと右に曲げて山に沿う感じに」「山から何個かさっきの火炎放射、あたれば炎上して飛ぶ、そのあとゴール」
    //   （火口のまわりの輪っかの道・マグマは「あんま面白くない」でやめた）。火山のまわりを右回りに沿う広めの道、山肌の岩から火炎放射
    public static float FlameExtraOff = 2.0f;   // 1.2→2.0（2026-11-04 てじゃ「火炎放射の間隔も長く」）
    public static float FlameKnockMin = 0.8f;   // 先端に当たっても最低これだけ横へ飛ばす（2026-11-02。2 だと Play で道のふちの 2.2m 外まで飛んだ）
    public static float FlameHW = 3f, FlameRise = 3f, FlameGap = 0.9f, FlameKnock = 6f, FlameKnockUp = 1.5f, FlameLand = -1.13f, FlameBallPerMeter = 0.755f,FlamePlayerLand = -1.14f, FlamePlayerPush = 6f, FlamePlayerPushUp = 4.2f, FlamePlayerPerMeter = 0.6f,   /* 人 7.5/5/0.72→6/4.2/0.6（2026-11-04 てじゃ「火炎放射のプレイヤーのふっ飛ばし力ダウン」） */ FlamePlayerMin = 0.8f,   /* 根元ほど強く、先ほど弱く。どこで当たっても道のふちくらいまで飛ぶ＝落ちることもある（2026-11-02 てじゃ「落ちるくらいでいいよ」「位置で強弱」） */ FlameLowH = 0.55f, FlameHighH = 2.95f;   // 上 6→3→1.5（てじゃ「上に飛ばしすぎかな」「まだ上にとびすぎ」）   // FlameLand：吹っ飛んで落ちる所の、外のふちからの残り
    public const int FlameRows = 3;   // 炎の壁の縦の本数
    public static Color MonsterCol = new Color(0.95f, 0.35f, 0.18f), MonsterTipCol = new Color(1f, 0.72f, 0.3f);
    public static float WalkW = 1.3f, WalkGap = 0.3f;   // 人の道（左）の幅と、本道とのすき間
    public static Color PathColA = new Color(1f, 0.56f, 0.50f), PathColB = new Color(0.94f, 0.44f, 0.38f);
    public static Color LavaCol = new Color(1f, 0.42f, 0.1f), RockCol = new Color(0.42f, 0.30f, 0.30f);

    static Material pathA, pathB, rockMat, lavaMat;
    static int pathLayer = 11;
    static FuwaHeat heat;
    static FuwaBall ballProxy;
    static List<FuwaCheckpoint> cps = new List<FuwaCheckpoint>();

    [MenuItem("FuwaCourse/ステージ2/2–2 ほのおのやま（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot1").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        root.rotation = Quaternion.LookRotation(spoke);
        root.position = new Vector3(lc.x, floorY - StartBelow, lc.z) + spoke * SpokeR;

        pathA = PathMat("Course2_2_PathA", PathColA); pathB = PathMat("Course2_2_PathB", PathColB);
        rockMat = PathSideMat();
        lavaMat = FlowLavaMat();
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        cps.Clear();

        // 熱（このコースに1つ）
        var hg = new GameObject("Heat"); hg.transform.SetParent(root, false);
        heat = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHeat>(hg); heat.ball = ballProxy;
        heat.burnSound = Sfx(hg.transform, "BurnSfx", "sfx_burn_start", Vector3.zero, 5f, 40f, 1f, false, 8f);
        heat.sideSpring = 1.6f;   // 横に引っぱるばねを弱く（2026-11-01 てじゃ「露骨に吸い寄せすぎる」、前は 4）
        heat.burnGrace = BurnGrace;
        heat.burnAfter = 3f;   // 浮いてから燃えだすまで（6→4→3、2026-11-03 てじゃ「３秒でもいいかも」。てじゃ「燃えるまでの時間も入り口付近のも含めてもうちょい短く」）
        BallBurnLook(hg.transform, heat);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(heat); FuwaStage2WaterTest.NoSync(heat);

        // ① スタート
        float z0 = StartSet(root);
        var path = new GameObject("Path").transform; path.SetParent(root, false);
        var walk = new GameObject("Walk").transform; walk.SetParent(root, false);
        var fire = new GameObject("Fire").transform; fire.SetParent(root, false);
        var up = Vector3.up;

        // ①〜② くねくねの細い上り坂 → 両脇が溶岩溜まりの直線（1本のつなぎ目なしの道）。上から見た図：てじゃ 2026-10-29
        //  橋 → 右ななめ上へ坂 → 角に火種1 → 北へ平ら → 左ななめ上へ長い坂（追い風が坂にそって吹き上げる）→ 角に火種2（風で運ばれて燃える→撃って消す）→ 右ななめ上へ坂 → 北へ平ら → 幅を広げて溶岩の直線
        float yL = 4.6f;
        var pts12 = new[] {
            new Vector3(0, 0, z0 - 0.5f), new Vector3(0, 0, z0 + 1.5f), new Vector3(4.5f, 1.6f, z0 + 7f), new Vector3(4.5f, 1.6f, z0 + 11f),
            new Vector3(-5f, 3.6f, z0 + 18f), new Vector3(-1f, yL, z0 + 22.5f), new Vector3(-1f, yL, z0 + 24f),
            new Vector3(-1f, yL, z0 + 30f), new Vector3(1.5f, yL, z0 + 40f), new Vector3(-1.5f, yL, z0 + 51f), new Vector3(0f, yL, z0 + 60f) };
        var hw12 = new[] { NarrowHW, NarrowHW, NarrowHW, NarrowHW, NarrowHW, NarrowHW, NarrowHW, WideHW, WideHW, WideHW, WideEndHW };
        var rad12 = new[] { 0f, 2f, 2f, 2f, 2f, 2f, 0f, 10f, 12f, 12f, 0f };   // [7] 広がった所の角も丸める（とがった角で上の面が重なってちらついた）
        var corners12 = new List<Vector3>();
        var r12 = Route(pts12, rad12, corners12, 0.12f);
        // ② 溶岩の穴：広い道（ゆるいS字）にあいた丸い穴（2026-10-29 てじゃの図「赤の部分くらい道を広げる、青◯を溶岩溜まりの穴、紫ラインを火球が往復、最後は3つの穴」「外の溶岩はいらんかも」）
        //  穴の中は溶岩（道の LavaDepth 下）。ふわふわが落ちたら黒焦げ→アウト、人もアウト
        var holeGroups = new[] {
            new[] { (z: 31.5f, x: 2.2f), (z: 36f, x: -2.0f) },
            new[] { (z: 42f, x: -2.6f), (z: 45f, x: 2.6f) },
            new[] { (z: 53f, x: 2.2f), (z: 56.5f, x: -2.0f) },
            // 真上に飛び上がる穴（てじゃ「思ったより簡単、垂直に飛び上がる穴を追加」「最後の3連がよわい、奥側で往復にして左手前の一個は垂直」）
            //  （2026-10-30 てじゃ「往復火の玉の外側を通れちゃう、垂直の穴を往復の外側の奥に」→ 往復が寄っていない側の道のふち寄りへ。てじゃの図から）
            new[] { (z: 48.1f, x: 3.0f) }, new[] { (z: 34.6f, x: 3.0f) }, new[] { (z: 39.0f, x: -3.1f) },
            new[] { (z: 51.5f, x: -3.1f) } };   // てじゃ「青◯に垂直を追加」：C の手前の穴の横、左のふち
        // 往復の穴（2つのグループ）は近いほうの道のふちまで溝をつなげて、溶岩を道の外へ垂れ流す（てじゃ「往復してるとこの穴、道の外までつなげて溶岩を垂れ流そう」）
        var holes = new List<Vector4>(); var chan = new List<int>(); var holeSide = new List<Vector3>(); var holeEdge = new List<float>();
        var holePts = new List<List<Vector3>>();
        foreach (var grp in holeGroups)
        {
            var hp = new List<Vector3>();
            foreach (var h in grp)
            {
                Frame(r12, z0 + h.z, out var hc, out var ht);
                var sd = Vector3.Cross(up, ht).normalized;
                var q = hc + sd * h.x; q.y = yL;
                holes.Add(new Vector4(q.x, q.y, q.z, HoleR)); hp.Add(q);
                chan.Add(grp.Length >= 2 ? (h.x > 0 ? 1 : -1) : 0);
                var near = r12.OrderBy(x => Flat(x.p - hc).sqrMagnitude).First();
                holeSide.Add(sd); holeEdge.Add(WAt(pts12, hw12, near.u) - Mathf.Abs(h.x));   // 穴の中心から道のふちまで
            }
            holePts.Add(hp);
        }
        for (int k = 0; k < holes.Count; k++)
            if (chan[k] != 0) foreach (var sg in new[] { -1f, 1f }) InsertRow(r12, (Vector3)holes[k], sg * ChanHW);
        // 穴をつないだ溶岩池（2026-10-31 てじゃの図「赤丸で繋がった溶岩池に」）。火の玉の出る穴の位置はそのまま。道の並び順に、z の小さい順
        var lavaRoot = new GameObject("LavaHoles").transform; lavaRoot.SetParent(root, false);
        var poolHoles = new[] { new[] { 0, 7 }, new[] { 1, 8, 2 }, new[] { 3, 6, 4 }, new[] { 9, 5 } };
        var pools = poolHoles.Select((g, pi) => new HolePool {
            c = g.Select(k => (Vector3)holes[k]).ToList(), rr = HoleR, amp = PoolWobble, ld = ChanLavaDepth, seed = pi * 1.7f + 0.4f, sx = chan[g.First(k => chan[k] != 0)],
            chanAt = g.Where(k => chan[k] != 0).Select(k => (Vector3)holes[k]).ToList() }).ToList();
        pools[1].bulge.Add(((Vector3)holes[1] + (Vector3)holes[8]) * 0.5f); pools[2].bulge.Add(((Vector3)holes[3] + (Vector3)holes[6]) * 0.5f);
        var d12 = Flat(r12[r12.Count - 1].p - r12[r12.Count - 2].p).normalized; var s12 = Vector3.Cross(Vector3.up, d12).normalized;   // 出口の線はジグザグの道の入口とぴったり合わせる
        Ribbon(path, "Path12", r12, hw12, pts12, pools, lavaRoot, false, null, s12);

        // 火種1・2：角の丸みのまん中（corners12 は角ごと、[0] は橋の先のちょっとした角）
        GroundFire(fire, "Seed1", corners12[1], (Flat(pts12[2] - pts12[1]).normalized - Flat(pts12[3] - pts12[2]).normalized).normalized);
        GroundFire(fire, "Seed2", corners12[3], (Flat(pts12[4] - pts12[3]).normalized - Flat(pts12[5] - pts12[4]).normalized).normalized);
        // 広い道の溶岩池の横の焚き火（2026-11-01 てじゃの図「赤◯に焚き火ほしい」）
        for (int k = 0; k < WideSeedSpots.Length; k++) { var q = WideSeedSpots[k]; GroundFire(fire, "WideSeed" + k, new Vector3(q.x, Along(r12, q).y, q.z), new Vector3(Mathf.Sign(q.x), 0f, 0f)); }
        // 噴気孔：左ななめ上の長い坂の入口に1つ。ぶわーっと斜め上に吹き出していて、乗ったふわふわを火種2へ斜めに吹っ飛ばす（FuwaLaunchPad）
        // （2026-10-29 てじゃ「中間だけ風あるのも不自然」→ 割れ目＋熱風 →「やっぱへん、ぶわーっと一個吹き出してるのを置いて斜めに吹っ飛ばす」）
        {
            var at = Along(r12, pts12[3] + (pts12[4] - pts12[3]).normalized * VentAt);
            BlastVent(fire, "Vent", at, corners12[3] + Vector3.up * 0.4f);
        }
        // 穴の溶岩（面・ふわふわの黒焦げ・人のアウト）
        // 穴と穴の間の細い所の当たり
        foreach (var pl in pools)
            for (int s = 0; s + 1 < pl.c.Count; s++)
            {
                var mid = (pl.c[s] + pl.c[s + 1]) * 0.5f; var seg = Flat(pl.c[s + 1] - pl.c[s]); var rs = Quaternion.LookRotation(seg); float wn = (HoleR + PoolWobble) * 2f;
                LavaBurn(lavaRoot, "LavaNeckBallOut" + pools.IndexOf(pl) + "_" + s, mid + Vector3.down * (ChanLavaDepth + BurnSink + 0.5f), new Vector3(wn, 1f, seg.magnitude), rs);
                Hazard(lavaRoot, "LavaNeckPlayerOut" + pools.IndexOf(pl) + "_" + s, mid + Vector3.down * (ChanLavaDepth + 0.5f), new Vector3(wn, 1.2f, seg.magnitude), true, rs);
            }
        foreach (var pl in pools)
            for (int s = 0; s < pl.bulge.Count; s++)
            {
                float bo = (PoolBulgeR + PoolWobble) * 2f;
                LavaBurn(lavaRoot, "LavaBulgeBallOut" + pools.IndexOf(pl) + "_" + s, pl.bulge[s] + Vector3.down * (ChanLavaDepth + BurnSink + 0.5f), new Vector3(bo, 1f, bo));
                Hazard(lavaRoot, "LavaBulgePlayerOut" + pools.IndexOf(pl) + "_" + s, pl.bulge[s] + Vector3.down * (ChanLavaDepth + 0.5f), new Vector3(bo, 1.2f, bo), true);
            }
        for (int k = 0; k < holes.Count; k++)
        {
            var hc = (Vector3)holes[k]; float ld = ChanLavaDepth;
            float holeOut = HoleR + PoolWobble;   // ふちがいちばんふくらんだ所まで
            LavaBurn(lavaRoot, "LavaBallOut" + k, hc + Vector3.down * (ld + BurnSink + 0.5f),
                 new Vector3(holeOut * 2f, 1f, holeOut * 2f));
            Hazard(lavaRoot, "LavaPlayerOut" + k, hc + Vector3.down * (ld + 0.5f), new Vector3(holeOut * 2f, 1.2f, holeOut * 2f), true);
            if (chan[k] == 0) continue;
            // 溝の溶岩（穴の中心→ふちの少し外）と、道の横に垂れる溶岩（仮：板）
            var od = holeSide[k] * chan[k]; float len = holeEdge[k] + 0.06f;
            var rot = Quaternion.LookRotation(od);
            var cm = hc + od * (len * 0.5f) + Vector3.down * ld;
            var strip = Box(lavaRoot, "LavaChannel" + k, cm - od * 0.1f + Vector3.down * 0.02f, new Vector3(ChanHW * 2f + 0.04f, 0.04f, len - 0.2f), lavaMat, 0);   // 道のふちより手前まで（道のふちは斜めなので、箱の角が道の外へ板のように出た）
            strip.transform.localRotation = rot; Object.DestroyImmediate(strip.GetComponent<Collider>());
            LavaBurn(lavaRoot, "LavaChannelBallOut" + k, cm + Vector3.down * (BurnSink + 0.5f), new Vector3(ChanHW * 2f, 1f, len), rot);
            Hazard(lavaRoot, "LavaChannelPlayerOut" + k, cm + Vector3.down * 0.5f, new Vector3(ChanHW * 2f, 1.2f, len), true, rot);
        }
        // 火の玉：穴から穴へ往復。だんだん難しく（速く・休みが短く・最後は3つの穴）。てっぺんは道の FireballTop 上
        {
            //  最後の往復もほかと同じ（火の玉2つ・速めにしたら「めちゃくちゃ球多い」「ほかといっしょでいいかも」）。真上のは道の VertTop 上まで
            var fbs = new[] { (fl: 1.3f, rest: 1.6f, ph: 0f, top: FireballTop), (fl: 1.1f, rest: 1.1f, ph: 0.7f, top: FireballTop), (fl: 1.0f, rest: 1.0f, ph: 1.9f, top: FireballTop),
                              (fl: 1.1f, rest: 0.6f, ph: 0.4f, top: VertTop), (fl: 1.3f, rest: 1.0f, ph: 1.3f, top: VertTop), (fl: 1.2f, rest: 0.8f, ph: 0.2f, top: VertTop), (fl: 1.2f, rest: 0.9f, ph: 0.9f, top: VertTop) };
            for (int k = 0; k < holePts.Count; k++)
            {
                float ld = ChanLavaDepth;
                var hs = holePts[k].Select(q => q + Vector3.down * ld).ToArray();
                Fireball(fire, "Fireball" + k, hs, fbs[k].fl, fbs[k].rest, fbs[k].ph, ld + fbs[k].top);
            }
        }
        // 火の玉の手前（2026-11-01 てじゃ「火球エリアの前、まものエリアの前」）。最初の穴は z0 + 31.5
        {
            // キノコは判定の 1.5m 先に並ぶので、その場所の道の中心と幅で置く（道が広がる所なので、手前の幅だと道に埋まった。2026-11-01 てじゃ「道に埋めたくない」）
            Frame(r12, z0 + 27.5f, out var cc, out var ct); var nn = r12.OrderBy(x => Flat(x.p - cc).sqrMagnitude).First(); var cd = Flat(ct).normalized;
            Checkpoint(root, "Checkpoint1", cc - cd * 1.5f, cd, WAt(pts12, hw12, nn.u));
        }
        Checkpoint(root, "Checkpoint2", new Vector3(0, yL, z0 + 59f), Vector3.forward, WideEndHW);

        // ③ 鋭角のグネグネ（登り）：丸い踊り場（角）の火種で止めて切り返す。角は約155° 曲がる（道どうしが約26°）
        //  はしの踊り場（Pad0・Pad4）は広い道がつながるので大きく（EndPadR）、広い道のはしが踊り場の中におさまる所に
        var pad = new Vector3(0, yL, z0 + 60f + EndPadIn);
        var pads = new List<(Vector3 c, float r)> { (pad, EndPadR) };
        var legs = new List<(Vector3 a, Vector3 b)>();
        for (int k = 0; k < 4; k++)
        {
            float hd = (k % 2 == 0 ? ZigHeading : -ZigHeading) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Sin(hd), 0, Mathf.Cos(hd));
            var nx = pad + d * ZigLeg + up * ZigRise;
            var sh = Vector3.forward * (ZigHW * 1.05f);
            var a0 = pad + sh; var b0 = nx - sh; var ld = (b0 - a0).normalized;
            var a = a0 + ld * PadIn; var b = b0 - ld * PadIn;
            legs.Add((a0, b0));
            if (k < 3)
            {
                // 火種は折り返しの内側の角から外へ（角の真上だと道からはみ出した）
                float hd2 = (k % 2 == 0 ? -ZigHeading : ZigHeading) * Mathf.Deg2Rad; var dn2 = new Vector3(Mathf.Sin(hd2), 0, Mathf.Cos(hd2));
                GroundFire(fire, "ZigSeed" + k, nx + (d - dn2).normalized * ZigSeedOut, (d - dn2).normalized);
            }
            pads.Add((nx, k == 3 ? EndPadR : PadR));
            pad = nx;
        }
        // 1個とばしで並んだ踊り場のまん中を横切る炎の壁（ふわふわが入ると即黒焦げ）：飛ばし越すショートカットを防ぐ
        //  （てじゃ「ショートカット防止に炎の壁でも敷いとくか、即黒焦げの」→ 踊り場の間に縦に置いたら「そこじゃなくてこの青ライン」＝V字のすき間を横切る向き）
        //  V の根元から両側の道のそばを 2 列で走り（FireStripGap、てじゃ「炎の壁がジグザグから遠すぎる」2026-10-31）、口で 1 本にまとめて列の外（FireWallOut）まで
        for (int k = 0; k + 2 < pads.Count; k++)
        {
            Vector3 a = pads[k].c, b = pads[k + 2].c, c = pads[k + 1].c, mid = (a + b) * 0.5f;
            var d = Flat(b - a).normalized; var n = Vector3.Cross(Vector3.up, d);
            if (Vector3.Dot(Flat(c - mid), n) < 0f) n = -n;   // n＝板の V の内向き
            FireWall(fire, "FireWall" + k, legs[k], legs[k + 1], mid, n, mid - n * (PadR + FireWallOut));
        }
        FireWallSound(fire);

        // 火の岩：コースの外の火山（ジグザグのてっぺんの右奥に、下からそびえる）の火口から飛んできて、坂の下る向きに板を横切ってはねながら落ちる。板のまん中を1列
        var vc = pad + VolcanoAt; var vtop = vc + up * (VolcanoTopH - 0.6f);
        var vfx = Volcano(fire, vc);
        CourseEmbers(fire, lavaRoot, pad, vc);
        BurntIsles(fire, lavaRoot);
        //  （2026-10-30 てじゃ「道に沿って落ちるとこの狭さじゃ回避できない、この方向に３個くらい列ずらしで落とす、ちょうどいい位置でバウンドして落ちてくる感じ」）
        {
            float x0 = legs[0].a.x, x1 = legs[0].b.x;
            float[] cols = { 0.33f, 0.67f };   // 2列を半周期ずらし（2026-10-31 てじゃ「2ラインあってもいいかも」。前は1列。3列は「きついか」→「やっぱ３列＋予告線」→「やっぱむずすぎる、３列やめとこう」）。予告（印・点線）は「びみょう」でなし
            for (int c = 0; c < cols.Length; c++)
            {
                float cx = Mathf.Lerp(x0, x1, cols[c]);
                var rp = new List<Vector3>(); Vector3 onLeg0 = Vector3.zero, onLeg1 = Vector3.zero;
                for (int k = legs.Count - 1; k >= 0; k--)
                {
                    float t = (cx - legs[k].a.x) / (legs[k].b.x - legs[k].a.x);
                    var q = Vector3.Lerp(legs[k].a, legs[k].b, t) + up * RockR;
                    if (k == 0) onLeg0 = q; if (k == 1) onLeg1 = q;
                    if (c == RockPassCol && k == 0) continue;   // 奥側の列はジグザグの最初の道ではねずに上を飛びこえて落ちる（ヒビもなし。2026-11-04 てじゃ）
                    rp.Add(q);
                }
                rp.Insert(0, vtop);   // 火山の火口から飛んでくる（岩柱→2026-10-30 てじゃ「コース外にある火山から飛んでくる感じに」）
                //  落ちる先は最初の道の点から。飛びこえる列はさらに RockPassFar 遠くへ（てじゃ「もっと遠くに飛ばそう」）
                var drop = onLeg0 + new Vector3(0, -10f, -4f); if (c == RockPassCol) drop += Flat(onLeg0 - onLeg1).normalized * RockPassFar;
                rp.Add(drop);
                LavaRock(fire, "LavaRock" + c, rp.ToArray(), c * RockPeriod / cols.Length, RockPeriod, RockFirstScale, 1.5f, vfx);
            }
            // 関係ない方へ飛んでって落ちていく岩 ×2（てじゃ「コースにいくのが一個、関係ないとこに飛んでくのが２個」）。周期をずらしてばらばらに見えるように
            LavaRock(fire, "LavaRockAway0", new[] { vtop, vtop + new Vector3(22f, -45f, 8f) }, 1.7f, 4.6f, 3.2f, 6f, vfx);
            LavaRock(fire, "LavaRockAway1", new[] { vtop, vtop + new Vector3(16f, -45f, -14f) }, 3.4f, 7.3f, 3.6f, 7f, vfx);
        }

        // ④ ほのおのまもの：広い平らな所に まもの4ひき・ところどころに水たまり（まものを落とすと倒れる・燃えたふわふわは消える）
        //  広い所と⑤の山ぞいの道は1本のつなぎ目なしの道。踊り場から広がって、火山のまわりを右回りに沿う（角度 θ は火山から見て、0 が奥 +z、+ が右回り）
        //  2026-10-30 てじゃ「流れを左にずらす」「まものエリアは山に沿うように」→「まものからをもっと右に曲げて山に沿う感じに」
        var ar = pad + Vector3.forward * EndPadIn;
        Vector3 Around(float th, float r, float y) { float a = th * Mathf.Deg2Rad; return new Vector3(vc.x + Mathf.Sin(a) * r, ar.y + y, vc.z + Mathf.Cos(a) * r); }
        var puddles = new[] { (x: 2.8f, z: 7.2f), (x: -3.2f, z: 11.5f), (x: 3.0f, z: 16.2f) };   // 5→3（てじゃ「水へらそ」）
        var monsters = new[] { (x: 0f, z: 5.5f), (x: -2.8f, z: 9.2f), (x: 2.6f, z: 11.5f), (x: 0.2f, z: 16.6f) };
        var pl5 = new List<Vector3> { ar, Around(-112f, 21.5f, 0f), Around(-96f, 21f, 0f), Around(-80f, 21f, 0f), Around(-64f, 20.5f, 0f) };
        var hl5 = new List<float> { EndPadR - 0.2f, ArenaHW, ArenaHW, ArenaHW, ArenaHW };
        // ⑤ 山ぞいの道：山肌（その辺りでいちばん出っぱった所）から FlameGap はなす。少しずつ上る
        const float Th0 = -48f, Th1 = 26f;
        float RoadR(float th, float y)
        {
            float m = 0f;
            for (float d = -20f; d <= 20f; d += 2f) m = Mathf.Max(m, VolcanoRAt(y, (th + d) * Mathf.Deg2Rad));
            return m + FlameGap + FlameHW;
        }
        float RoadY(float th) { return FlameRise * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Th0, Th1 - 8f, th)); }
        for (float th = Th0; th <= Th1 + 0.01f; th += 4f) { pl5.Add(Around(th, RoadR(th, RoadY(th)), RoadY(th))); hl5.Add(FlameHW); }
        {
            var e0 = pl5[pl5.Count - 1]; float a1 = Th1 * Mathf.Deg2Rad; var tg = new Vector3(Mathf.Cos(a1), 0f, -Mathf.Sin(a1));
            var e1 = e0 + tg * 4f; var e2 = e1 + new Vector3(2f, 0f, 6f); var e3 = e2 + new Vector3(0f, 0f, 5f);
            // 曲がり角の内側の V を埋める：角までは広い道の幅のまま、曲がってから細く（2026-10-30 てじゃ「カーブのVをちょっと埋めよう」。切り返しの火種は「やっぱいらん」）
            pl5.Add(e1); hl5.Add(FlameHW); pl5.Add(e2); hl5.Add((FlameHW + PathHW) * 0.5f); pl5.Add(e3); hl5.Add(PathHW);
        }
        var pts5 = pl5.ToArray(); var hw5 = hl5.ToArray();
        var rad5 = new float[pts5.Length]; rad5[pts5.Length - 3] = 6f; rad5[pts5.Length - 2] = 5f;
        rad5[4] = 3f; rad5[5] = 3f;   // まものエリアの終わりの折れ目。丸めないと内側のふちに切れ目が出た（2026-11-03 てじゃ「道の端にスキマ」）
        var r5 = Route(pts5, rad5, new List<Vector3>());
        // 曲がった道の上の (x 横, z 奥行き) の所
        Vector3 OnArena(float x, float z) { Frame(r5, ar.z + z, out var c, out var t); return c + Vector3.Cross(Vector3.up, t).normalized * x; }
        {   // まものの手前（最初のまものは z 5.5）
            Frame(r5, ar.z + 3f, out var cc, out var ct); var nn = r5.OrderBy(x => Flat(x.p - cc).sqrMagnitude).First(); var cd = Flat(ct).normalized;
            Checkpoint(root, "Checkpoint3", cc - cd * 1.5f, cd, WAt(pts5, hw5, nn.u));
        }
        var pholes = puddles.Select(q => { var c = OnArena(q.x, q.z); return new Vector4(c.x, c.y, c.z, PuddleR); }).ToList();
        // ジグザグ全体を 1 本の道に（2026-10-31 てじゃ「丸い部分なんか変えるか、ジグザグもふくめて」→ 案 A。丸い踊り場と板をやめた）
        var r12End = r12[r12.Count - 1].p; var d5 = Flat(r5[1].p - r5[0].p).normalized; var s5 = Vector3.Cross(Vector3.up, d5).normalized; float w5 = WAt(pts5, hw5, r5[0].u);
        ZigPath(path, r12End - s12 * WideEndHW, r12End + s12 * WideEndHW, d12, legs, pads.Select(q => q.c).ToList(), r5[0].p - s5 * w5, r5[0].p + s5 * w5, d5);
        Ribbon(path, "Path5", r5, hw5, pts5, pholes.Select((v, pi) => new HolePool { c = new List<Vector3> { (Vector3)v }, rr = v.w, ld = LavaDepth, amp = PuddleWobble, seed = pi * 2.3f + 1.1f }).ToList(), null, true, s5);
        for (int k = 0; k < pholes.Count; k++) Puddle(path, "Puddle" + k, (Vector3)pholes[k]);
        ArenaDeco(path, OnArena, pholes, vc);
        for (int k = 0; k < monsters.Length; k++) FireMonster(root, "FireMonster" + k, OnArena(monsters[k].x, monsters[k].z));
        // 火口からまもののエリアにも岩を1個（てじゃ「一個まものエリアにも落とそうか、まものも当たったら一応飛ぶ」）：山側から飛んできて2回はねて、外のふちから落ちる
        {
            var up1 = Vector3.up * RockR;
            var l3 = OnArena(-4.6f, 14.2f); Frame(r5, ar.z + 14.2f, out _, out var t3); var outw = -Vector3.Cross(Vector3.up, t3).normalized;
            var ps = new[] { vtop, OnArena(3.4f, 11.8f) + up1, OnArena(-0.6f, 13f) + up1, l3 + up1, l3 + outw * 4f + Vector3.down * 10f };
            LavaRock(fire, "LavaRockArena", ps, 4.1f, 6.7f, 2.4f, 2f, vfx);
            var lr = fire.Find("LavaRockArena").GetComponentInChildren<FuwaLavaRock>();
            lr.enemies = Enumerable.Range(0, monsters.Length).Select(k => root.Find("FireMonster" + k).GetComponent<FuwaEnemy>()).ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(lr);
        }
        {
            float a0 = (Th0 + 2f) * Mathf.Deg2Rad;
            Checkpoint(root, "Checkpoint4", Around(Th0 + 2f, RoadR(Th0 + 2f, 0f), 0f), new Vector3(Mathf.Cos(a0), 0f, -Mathf.Sin(a0)), FlameHW);
        }
        // 山肌から火炎の壁 3 枚（てじゃ「交互ばっかだとシステマチック」→ 周期・長さをばらばらに）。横（道を横切る）に縦に並んだ炎がいっしょに吹く。
        //  2026-10-30 斜め下→「斜めいらんかも」、低い・高いの交互→「いまいち」「上下セットの壁を3枚って形がいいかな」
        var jets = new[] { (th: -30f, per: 3.4f + FlameExtraOff, on: 1.4f, ph: 0f), (th: -8f, per: 3.0f + FlameExtraOff, on: 1.2f, ph: 0.9f), (th: 14f, per: 3.8f + FlameExtraOff, on: 1.6f, ph: 1.9f) };   // FlameExtraOff：根元で飛ばされて落ちてくるまでに 2 回目を食らわないように止まっている時間を長く（2026-11-02 てじゃ）
        for (int k = 0; k < jets.Length; k++)
        {
            var jt = jets[k]; float y = RoadY(jt.th);
            VolcanoJet(fire, "Flamethrower" + k, Around(jt.th, RoadR(jt.th, y) - FlameHW, y), RoadR(jt.th, y) - FlameHW, y, jt.th, jt.per, jt.on, jt.ph);
        }
        JetWarnAreas(fire);
        FireballWarns(fire);
        var p = pts5[pts5.Length - 1];

        // ⑦ ゴール
        GoalSet(root, p);

        // アウト（下に落ちた）：コースの下（コースの向きの箱、まわり 10m）。ほかのコース（2–1）にかからないように大きくしすぎない
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger || col.name == "VolcanoBody") continue;   // 火山（すそが深くて大きい）は入れない
            Bounds bb; if (col is BoxCollider bx) bb = new Bounds(bx.center, bx.size); else if (col is MeshCollider mc && mc.sharedMesh) bb = mc.sharedMesh.bounds; else continue;
            var t = col.transform; var e = bb.extents;
            for (int k = 0; k < 8; k++)
            {
                var c = bb.center + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
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
        ApplyMonsterAvoid(root);   // チェックポイントを作ったあとで
        FuwaStage2LobbyCourses.Run();
        FuwaCourse2_1Builder.FixNetworkIDs();
        RegisterBgm(root);
        FuwaStage2Static.Apply(root);
        RockCracks(fire);
        RockPathDots(fire);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[2–2] ゴールまで " + p.z.ToString("F0") + "m、高さ " + p.y.ToString("F1") + "m";
    }

    // ---- つなぎ目なしの道：折れ線の角を丸めた中心線（Route）にそった帯のメッシュ（Ribbon） ----
    struct RPt { public Vector3 p; public float u; }   // u：もとの折れ線にそった（水平の）長さ。高さ・幅は折れ線の点の値をこれで線形に
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static float[] Cum(Vector3[] pts) { var c = new float[pts.Length]; for (int i = 1; i < pts.Length; i++) c[i] = c[i - 1] + Flat(pts[i] - pts[i - 1]).magnitude; return c; }
    static float AtU(Vector3[] pts, float[] vals, float u)
    {
        var c = Cum(pts);
        for (int i = 1; i < pts.Length; i++) if (u <= c[i] || i == pts.Length - 1) return Mathf.Lerp(vals[i - 1], vals[i], Mathf.InverseLerp(c[i - 1], c[i], u));
        return vals[vals.Length - 1];
    }
    // 幅：点の間をなめらかに（線形だと広がる所の角がとがった）
    static float WAt(Vector3[] pts, float[] vals, float u)
    {
        var c = Cum(pts);
        for (int i = 1; i < pts.Length; i++) if (u <= c[i] || i == pts.Length - 1) return Mathf.Lerp(vals[i - 1], vals[i], Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c[i - 1], c[i], u)));
        return vals[vals.Length - 1];
    }
    // 角 i（両はし以外）を半径 rad[i] で丸める（短い辺に合わせて小さくする）。corners に角の丸みのまん中の点
    static List<RPt> Route(Vector3[] pts, float[] rad, List<Vector3> corners, float step = 0.25f)
    {
        int n = pts.Length; var cum = Cum(pts); var ys = pts.Select(q => q.y).ToArray();
        var res = new List<RPt>();
        void Add(Vector3 q, float u) { q.y = AtU(pts, ys, u); res.Add(new RPt { p = q, u = u }); }
        Vector3 cur = Flat(pts[0]); float cu = 0f;
        for (int i = 1; i < n; i++)
        {
            Vector3 a = Flat(pts[i - 1]), b = Flat(pts[i]); Vector3 d0 = (b - a).normalized;
            float tl = 0f, r = rad[i], th = 0f; Vector3 d1 = Vector3.zero;
            if (i < n - 1 && r > 0f)
            {
                d1 = (Flat(pts[i + 1]) - b).normalized;
                th = Vector3.Angle(d0, d1) * Mathf.Deg2Rad;
                if (th > 0.01f)
                {
                    tl = r * Mathf.Tan(th * 0.5f);
                    float lim = 0.45f * Mathf.Min((b - a).magnitude, (Flat(pts[i + 1]) - b).magnitude);
                    if (tl > lim) { tl = lim; r = tl / Mathf.Tan(th * 0.5f); }
                }
            }
            Vector3 e = b - d0 * tl; float eu = cum[i] - tl;
            int m = Mathf.Max(1, Mathf.CeilToInt((e - cur).magnitude / step));
            for (int k = res.Count == 0 ? 0 : 1; k <= m; k++) Add(Vector3.Lerp(cur, e, k / (float)m), Mathf.Lerp(cu, eu, k / (float)m));
            cur = e; cu = eu;
            if (tl > 0f)
            {
                var perp = (d1 - d0 * Vector3.Dot(d0, d1)).normalized;   // 曲がる内側
                var O = e + perp * r; var v0 = e - O; var t1 = b + d1 * tl;
                float sg = Mathf.Sign(Vector3.Cross(v0, t1 - O).y);
                int am = Mathf.Max(2, Mathf.CeilToInt(r * th / step));
                for (int k = 1; k <= am; k++)
                {
                    float f = k / (float)am;
                    Add(O + Quaternion.AngleAxis(sg * th * f * Mathf.Rad2Deg, Vector3.up) * v0, Mathf.Lerp(eu, cum[i] + tl, f));
                }
                var mid = O + Quaternion.AngleAxis(sg * th * 0.5f * Mathf.Rad2Deg, Vector3.up) * v0; mid.y = AtU(pts, ys, cum[i]);
                corners.Add(mid);
                cur = t1; cu = cum[i] + tl;
            }
            else if (i < n - 1) { var q = b; q.y = pts[i].y; corners.Add(q); }
        }
        return res;
    }
    // 中心線のうち q にいちばん近い点
    static Vector3 Along(List<RPt> r, Vector3 q) { return r.OrderBy(x => (Flat(x.p - q)).sqrMagnitude).First().p; }
    // 中心線が奥行き z を越える所の点と、進む向き（水平）
    static void Frame(List<RPt> r, float z, out Vector3 c, out Vector3 t)
    {
        int i = r.FindIndex(x => x.p.z >= z); if (i < 0) i = r.Count - 1; if (i < 1) i = 1;
        c = r[i].p; t = Flat(r[Mathf.Min(i + 1, r.Count - 1)].p - r[i - 1].p).normalized;
    }

    // 中心線に、o から進む向きに dist の所を横切る点を足す（溝のはしの線を道の点の列にのせる）
    static void InsertRow(List<RPt> r, Vector3 o, float dist)
    {
        for (int i = 0; i < r.Count - 1; i++)
        {
            if (Flat(r[i].p - o).magnitude > 4f) continue;   // 穴の近くだけ（遠くのくねくね道でも向きの計算が 0 をまたいで、関係ない所に足していた）
            var t = Flat(r[i + 1].p - r[i].p).normalized;
            float a = Vector3.Dot(Flat(o - r[i].p), t) - dist, b = Vector3.Dot(Flat(o - r[i + 1].p), t) - dist;
            if (a > 0f && b <= 0f)
            {
                float f = a / (a - b);
                if (f < 0.02f || f > 0.98f) return;   // ほぼ点の上なら足さない
                r.Insert(i + 1, new RPt { p = Vector3.Lerp(r[i].p, r[i + 1].p, f), u = Mathf.Lerp(r[i].u, r[i + 1].u, f) });
                return;
            }
        }
    }

    // 面を足していくメッシュ（面ごとに頂点を分ける＝角がくっきり。表は Cross(b-a,c-a) の向き）
    class MB
    {
        public List<Vector3> v = new List<Vector3>(); public List<int>[] t = { new List<int>(), new List<int>(), new List<int>() };
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int sub)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a), outward) < 0f) { var x = b; b = d; d = x; }
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t[sub].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        // uv つき（垂れる溶岩の x：列の根元 0〜先 1、y：ふちにそった長さ m）
        public List<Vector2> uv = new List<Vector2>();
        public void QuadUv(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward, int sub)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a), outward) < 0f) { var x = b; b = d; d = x; var y = ub; ub = ud; ud = y; }
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            t[sub].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        public Mesh Build(string name, int subs)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.subMeshCount = subs;
            if (uv.Count == v.Count && uv.Count > 0) m.SetUVs(0, uv);
            for (int s = 0; s < subs; s++) m.SetTriangles(t[s], s);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
    const string MeshDir22 = "Assets/FuwaStage2/Generated/Course2_2";
    static Mesh SaveMesh(Mesh m)
    {
        if (!AssetDatabase.IsValidFolder(MeshDir22)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Generated", "Course2_2");
        string p = MeshDir22 + "/" + m.name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old == null) { AssetDatabase.CreateAsset(m, p); return m; }
        old.Clear(false); old.indexFormat = m.indexFormat;
        old.SetVertices(m.vertices); old.subMeshCount = m.subMeshCount;
        for (int s = 0; s < m.subMeshCount; s++) old.SetTriangles(m.GetTriangles(s), s);
        old.normals = m.normals; if (m.uv.Length > 0) old.uv = m.uv; if (m.colors.Length > 0) old.colors = m.colors;   // UV と色も入れ直す（消えると炎の板が全部切り抜かれて見えなくなった）
        old.RecalculateBounds(); EditorUtility.SetDirty(old);
        return old;
    }
    static GameObject MeshObj(Transform parent, string name, Mesh vis, Mesh col, Material[] mats)
    {
        var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(parent, false); g.layer = pathLayer;
        g.GetComponent<MeshFilter>().sharedMesh = SaveMesh(vis);
        g.GetComponent<MeshRenderer>().sharedMaterials = mats;
        if (col != null) g.AddComponent<MeshCollider>().sharedMesh = SaveMesh(col);
        return g;
    }

    // 帯の道：上の面と横の帯（厚み T）はシマシマ、その下に岩（少し内側）。当たりは帯の部分だけ
    // holes：上の面にあける丸い穴（xyz＝上の面の中心、w＝半径。道のふちからはみ出さないこと）。穴の壁（岩）は LavaDepth＋0.3 下まで
    // 道にあける穴（2026-10-31 てじゃ「穴と溝が円と直線すぎる」「赤丸で繋がった溶岩池に」）：穴の中心をつないだ線のまわりの形。ふちはでこぼこ、穴と穴の間は少し細い。
    //  道の線ごとに「穴の左はし・右はし」を求めて作る（ふちの点は線の上だけ。線から外すと、となりの帯の長い辺とずれて道に切れ目が出た）
    public static float PoolWobble = 0.2f, PoolNeck = 0.3f, PoolThinGap = 1.2f, PoolBulgeR = 1.9f;   // PoolBulgeR：真ん中の 2 つの池のくびれを直径 3.8m に（2026-11-03 てじゃ「このルートでいけちゃうのを潰したい」。くびれは約 1.7m でジャンプで越せた）
    const int LavaShadowLayer = 14;   // PickupNoEnvironment：ふわふわ（17）・人（9, 10）と当たらない
    public const float BurnSink = 0.1f;   // ふわふわが黒焦げになる高さ：溶岩の面からこれだけ下に玉の底が入った時（2026-10-31 てじゃ「やたら高い位置でアウト」で 0.2 に→「ちょっぴり上げても」で 0.1。前は溶岩の 0.2 上だった）
    class HolePool
    {
        public List<Vector3> c = new List<Vector3>(); public float rr, amp, ld, seed; public int sx; public List<Vector3> chanAt = new List<Vector3>();
        public List<Vector3> bulge = new List<Vector3>();   // ジャンプで越せないようにふくらませる所（半径 PoolBulgeR）
        public float Sd(Vector3 p)
        {
            float best = float.MaxValue;
            if (c.Count == 1) best = Flat(p - c[0]).magnitude - rr;
            for (int k = 0; k + 1 < c.Count; k++)
            {
                var ab = Flat(c[k + 1] - c[k]); float t = Mathf.Clamp01(Vector3.Dot(Flat(p - c[k]), ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Flat(p - c[k] - ab * t).magnitude - rr * (1f - PoolNeck * Mathf.Sin(t * Mathf.PI)));
            }
            foreach (var b in bulge) best = Mathf.Min(best, Flat(p - b).magnitude - PoolBulgeR);
            if (amp <= 0f) return best;
            return best - amp * (0.5f * Mathf.Sin(1.6f * p.x + 0.7f * p.z + seed) + 0.3f * Mathf.Sin(-2.7f * p.x + 2.1f * p.z + seed * 2f) + 0.2f * Mathf.Sin(4.3f * p.x + 3.7f * p.z + seed * 3f));
        }
        // 線（中心 o、横の向き side、幅の半分 w）が穴を切る左はし・右はし。道のふちを少し残す
        public bool Span(Vector3 o, Vector3 side, float w, out float l, out float r)
        {
            l = r = 0f; float lim = w - 0.12f, x0 = float.NaN, x1 = float.NaN;
            for (float x = -lim; x <= lim; x += 0.05f) if (Sd(o + side * x) < 0f) { if (float.IsNaN(x0)) x0 = x; x1 = x; }
            if (float.IsNaN(x0)) return false;
            l = Edge(o, side, Mathf.Max(x0 - 0.05f, -lim), x0); r = Edge(o, side, Mathf.Min(x1 + 0.05f, lim), x1);
            return true;
        }
        float Edge(Vector3 o, Vector3 side, float outX, float inX)
        {
            if (Sd(o + side * outX) < 0f) return outX;
            for (int k = 0; k < 8; k++) { float m = (outX + inX) * 0.5f; if (Sd(o + side * m) < 0f) inX = m; else outX = m; }
            return inX;
        }
    }
    // 道のふちから垂れる溶岩：前の細い滝（LavaFallLook、下へ細くなって消える形）を、開いたふちの幅いっぱいに横へ引き伸ばした形（2026-11-03 てじゃ「最初の細くて消えてくかんじのやつを横に引き伸ばしたら」）。裏からも見える
    public static float CurtainLen = 5f; const int CurtainRows = 8;
    static void LavaCurtains(MB lava, List<RPt> r, Vector3[] side, float[] s, Vector3[] pts, float[] hw, List<(int i, int sx, float ld)> open)
    {
        foreach (var sx in new[] { -1, 1 })
        {
            var rows = open.Where(o => o.sx == sx).OrderBy(o => o.i).ToList();
            for (int a = 0; a < rows.Count;)
            {
                int b = a; while (b + 1 < rows.Count && rows[b + 1].i == rows[b].i + 1) b++;
                int i0 = rows[a].i, i1 = rows[b].i + 1; float s0 = s[i0], s1 = s[i1], ld = rows[a].ld;
                Vector3 Col(int j, float t)
                {
                    // 2–1 の滝のように幅いっぱいでまっすぐ落ちる（てじゃ「2-1の滝みたいなの」）。両はしだけ短くして、ふちの角につなげる
                    float edge = Mathf.Min(s[j] - s0, s1 - s[j]), len = CurtainLen * (0.15f + 0.85f * Mathf.Sqrt(Mathf.Clamp01(edge / 1.2f))) * (0.96f + 0.04f * Mathf.Sin(s[j] * 1.7f + s0));
                    var top = r[j].p + side[j] * (sx * WAt(pts, hw, r[j].u)) + Vector3.down * ld;
                    float bulge = 0.14f * Mathf.Sin(Mathf.Min(t * 3f, 1f) * Mathf.PI * 0.5f) * (1f - t * t) + 0.07f * Mathf.Sin(t * 7.5f) * t;
                    return top + side[j] * (sx * (-0.02f + bulge)) + Vector3.down * (len * t);
                }
                for (int j = i0; j < i1; j++)
                    for (int k = 0; k < CurtainRows; k++)
                    {
                        float t0 = k / (float)CurtainRows, t1 = (k + 1f) / CurtainRows;
                        var p00 = Col(j, t0); var p01 = Col(j, t1); var p10 = Col(j + 1, t0); var p11 = Col(j + 1, t1);
                        Vector2 u00 = new Vector2(t0, s[j]), u10 = new Vector2(t0, s[j + 1]), u11 = new Vector2(t1, s[j + 1]), u01 = new Vector2(t1, s[j]);
                        lava.QuadUv(p00, p10, p11, p01, u00, u10, u11, u01, side[j] * sx, 0);
                        lava.QuadUv(p00, p10, p11, p01, u00, u10, u11, u01, -side[j] * sx, 0);
                    }
                a = b + 1;
            }
        }
    }
    // 溝のふち：a（穴のふち）から b（道のふち）を折れ線にして、溝の中へでこぼこ出す。両はしは動かさない。道の幅は広がらない
    public static float ChanWobble = 0.24f;
    static Vector3[] ChanEdge(Vector3 a, Vector3 b, Vector3 inward, float seed)
    {
        const int N = 12;
        var e = new Vector3[N + 1]; var d = inward.normalized;
        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N, x = t * (b - a).magnitude;
            float n = 0.5f + 0.5f * (0.55f * Mathf.Sin(x * 2.3f + seed) + 0.3f * Mathf.Sin(x * 5.1f + seed * 1.9f) + 0.15f * Mathf.Sin(x * 11f + seed * 3.3f));
            e[i] = Vector3.Lerp(a, b, t) + d * (ChanWobble * n * Mathf.Sin(t * Mathf.PI));
        }
        return e;
    }
    // ふちのベベル（2 段で丸く）。e0, e1 は角の線、o0, o1 は外向き。面の内側は Bevel 内まで上の面のまま
    public const float Bevel = 0.09f; static readonly Vector3 BevelDown = Vector3.down * Bevel;
    static void BevelEdge(MB mb, Vector3 e0, Vector3 e1, Vector3 o0, Vector3 o1, int sub)
    {
        var in0 = e0 - o0 * Bevel; var in1 = e1 - o1 * Bevel;
        var mid0 = e0 - o0 * Bevel * 0.3f + BevelDown * 0.3f; var mid1 = e1 - o1 * Bevel * 0.3f + BevelDown * 0.3f;
        mb.Quad(in0, in1, mid1, mid0, o0 + Vector3.up * 1.6f, sub);
        mb.Quad(mid0, mid1, e1 + BevelDown, e0 + BevelDown, o0 + Vector3.up * 0.5f, sub);
    }
    // 道の下の岩：上は道の下のふち、下へ行くほど内側へ。深さは場所のノイズ（同じ位置は同じ深さなので、となりの帯とつながる）
    static Vector3 SkirtPoint(Vector3 p, Vector3 center, float f)
    {
        float d = 1.0f + 1.5f * Mathf.PerlinNoise(p.x * 0.33f + 9f, p.z * 0.33f + 4f) + 0.5f * Mathf.PerlinNoise(p.x * 1.2f + 2f, p.z * 1.2f + 7f);
        var inw = Flat(center - p); float room = inw.magnitude; if (room > 1e-4f) inw /= room;
        float inset = Mathf.Min(room * 0.8f, d * 0.55f);
        float wob = (Mathf.PerlinNoise(p.x * 1.7f + 5f, p.z * 1.7f + 1f) - 0.5f) * 0.3f * Mathf.Sin(f * Mathf.PI);
        return p + inw * (inset * f + wob) + Vector3.down * (d * f);
    }
    static void Skirt(MB mb, Vector3 r0, Vector3 r1, Vector3 l0, Vector3 l1, Vector3 outward)
    {
        var c0 = (r0 + l0) * 0.5f; var c1 = (r1 + l1) * 0.5f; var fs = new[] { 0f, 0.4f, 0.75f, 1f };
        for (int k = 0; k + 1 < fs.Length; k++)
        {
            mb.Quad(SkirtPoint(r0, c0, fs[k]), SkirtPoint(r1, c1, fs[k]), SkirtPoint(r1, c1, fs[k + 1]), SkirtPoint(r0, c0, fs[k + 1]), outward + Vector3.down * 0.2f, 2);
            mb.Quad(SkirtPoint(l0, c0, fs[k]), SkirtPoint(l1, c1, fs[k]), SkirtPoint(l1, c1, fs[k + 1]), SkirtPoint(l0, c0, fs[k + 1]), -outward + Vector3.down * 0.2f, 2);
        }
        mb.Quad(SkirtPoint(l0, c0, 1f), SkirtPoint(l1, c1, 1f), SkirtPoint(r1, c1, 1f), SkirtPoint(r0, c0, 1f), Vector3.down, 2);
    }
    static void SkirtEnd(MB mb, Vector3 l, Vector3 r, Vector3 o)
    {
        var c = (l + r) * 0.5f; var fs = new[] { 0f, 0.4f, 0.75f, 1f };
        for (int k = 0; k + 1 < fs.Length; k++) mb.Quad(SkirtPoint(l, c, fs[k]), SkirtPoint(r, c, fs[k]), SkirtPoint(r, c, fs[k + 1]), SkirtPoint(l, c, fs[k + 1]), o, 2);
    }
    static void Ribbon(Transform parent, string name, List<RPt> r, float[] hw, Vector3[] pts, List<HolePool> holes = null, Transform lavaParent = null, bool smoothSides = false, Vector3? firstSide = null, Vector3? lastSide = null)
    {
        const float T = 0.6f, U = 1.5f, In = 0.1f;
        int n = r.Count;
        var L = new Vector3[n]; var R = new Vector3[n]; var Li = new Vector3[n]; var Ri = new Vector3[n]; var side = new Vector3[n]; var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            var t = Flat(r[Mathf.Min(i + 1, n - 1)].p - r[Mathf.Max(i - 1, 0)].p).normalized;
            float w = WAt(pts, hw, r[i].u);
            // 幅が広い所の角で、道を横切る線どうしが内側で交差して面が折り重なった（2026-10-31）。線の向きを前後 w の範囲の平均にして交差しないように
            if (smoothSides)
            {
                float sum = 0f; var avg = Vector3.zero;
                for (int j = i - 1; j >= 0 && sum < w * 1.5f; j--) { var d = Flat(r[j + 1].p - r[j].p); avg += d; sum += d.magnitude; }
                sum = 0f;
                for (int j = i; j + 1 < n && sum < w * 1.5f; j++) { var d = Flat(r[j + 1].p - r[j].p); avg += d; sum += d.magnitude; }
                if (avg.sqrMagnitude > 1e-6f) t = avg.normalized;
            }
            side[i] = i == 0 && firstSide.HasValue ? firstSide.Value : i == n - 1 && lastSide.HasValue ? lastSide.Value : Vector3.Cross(Vector3.up, t).normalized;
            R[i] = r[i].p + side[i] * w; L[i] = r[i].p - side[i] * w;
            Ri[i] = r[i].p + side[i] * (w - In); Li[i] = r[i].p - side[i] * (w - In);
            if (i > 0) s[i] = s[i - 1] + Flat(r[i].p - r[i - 1].p).magnitude;
        }
        // ふちの線が急に折れ返る点をならす（2026-11-01 てじゃ「ゴールまえのカーブに隙間」：内側のふちが 5cm 逆に戻って、そこの壁の面が裏返って空が見えた）
        void SmoothKinks(Vector3[] e, Vector3[] ei)
        {
            for (int pass = 0; pass < 4; pass++)
                for (int i = 1; i + 1 < n; i++)
                {
                    var a = Flat(e[i] - e[i - 1]); var b = Flat(e[i + 1] - e[i]);
                    if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f || Vector3.Angle(a, b) < 50f) continue;
                    var d = (e[i - 1] + e[i + 1]) * 0.5f - e[i]; d.y = 0f;
                    e[i] += d; ei[i] += d;
                }
        }
        SmoothKinks(R, Ri); SmoothKinks(L, Li);
        int folds = 0; string foldAt = ""; for (int i = 0; i + 1 < n; i++) { var f = Flat(r[i + 1].p - r[i].p); if (Vector3.Dot(R[i + 1] - R[i], f) <= 0f || Vector3.Dot(L[i + 1] - L[i], f) <= 0f) { folds++; foldAt += " #" + i + "/" + n + r[i].p.ToString("F1") + " f=" + f.magnitude.ToString("F3"); } }
        if (folds > 0) Debug.LogWarning("[2–2] " + name + " 道の面の折り重なり " + folds + " か所" + foldAt);
        var vis = new MB(); var col = new MB(); var lava = new MB(); var dn = Vector3.down * T; var du = Vector3.down * (T + U);
        var openEdge = new List<(int i, int sx, float ld)>(); var curtain = new MB();
        for (int i = 0; i < n - 1; i++)
        {
            int st = Mathf.FloorToInt((s[i] + s[i + 1]) * 0.5f / StripeLen) % 2;
            // この帯にかかる穴：帯の手前と奥の線それぞれで、穴の中心の横の位置 c と、穴が線を切る半分の長さ h（線にかからなければ 0）
            // tip：穴の前後のはし（線にかからない側の線ではふちが1点 c に縮むので、本当のはしの点 m まで三角でうめる。うめないと帯の幅ぶんのトゲが出た）
            // ch：溝（穴から道のふち sx の側へ、幅 ChanHW*2）。溝の両はしの線は道の点の列に入れてある（InsertRows）ので、帯ごとに「溝の中か」で決まる
            var cut = new List<(float c0, float hl0, float hr0, float c1, float hl1, float hr1, Vector3 o, int tip, Vector3 m, int sx, bool ch0, bool ch1, float wd)>();
            if (holes != null)
                for (int hi = 0; hi < holes.Count; hi++)
                {
                    var pool = holes[hi]; int sx = pool.sx;
                    // 線（side の向き）に直角な向きで測る（帯ごとの進む向きで測ると、となりの帯と穴のふちの点がずれて壁にすき間ができた）
                    var T0 = Vector3.Cross(side[i], Vector3.up); var T1 = Vector3.Cross(side[i + 1], Vector3.up);
                    float w0h = WAt(pts, hw, r[i].u), w1h = WAt(pts, hw, r[i + 1].u);
                    bool in0 = pool.Span(r[i].p, side[i], w0h, out float l0h, out float r0h), in1 = pool.Span(r[i + 1].p, side[i + 1], w1h, out float l1h, out float r1h);
                    if (!in0 && !in1) continue;
                    int tip = !in0 ? 0 : !in1 ? 1 : -1;
                    float c0 = (l0h + r0h) * 0.5f, c1 = (l1h + r1h) * 0.5f, h0 = (r0h - l0h) * 0.5f, h1 = (r1h - l1h) * 0.5f;
                    var m = Vector3.zero;
                    if (tip >= 0)
                    {
                        // はしの点 m：穴が切れている線のまん中から、切れていない線へ向かって、ふちになる所。反対の線は越えない（越えると面が折り返す）
                        var from = tip == 0 ? r[i + 1].p + side[i + 1] * c1 : r[i].p + side[i] * c0;
                        var op = tip == 0 ? r[i].p : r[i + 1].p; var os = tip == 0 ? side[i] : side[i + 1];
                        float cOut = Vector3.Dot(Flat(from - op), os); var to = op + os * cOut;
                        float a = 0f, b = 1f; for (int q = 0; q < 8; q++) { float f = (a + b) * 0.5f; if (pool.Sd(Vector3.Lerp(from, to, f)) < 0f) a = f; else b = f; }
                        m = Vector3.Lerp(from, to, Mathf.Min(a, 0.96f)); m.y = op.y;
                        if (tip == 0) { c0 = cOut; h0 = 0f; } else { c1 = cOut; h1 = 0f; }
                    }
                    bool ch0 = false, ch1 = false;
                    foreach (var ca in pool.chanAt)
                    {
                        ch0 |= Mathf.Abs(Vector3.Dot(Flat(ca - r[i].p), T0)) <= ChanHW + 0.06f;   // 足した線は進む向きの測り方が少しちがうので余裕を（0.02 だと外れて上の面がかけらになった）
                        ch1 |= Mathf.Abs(Vector3.Dot(Flat(ca - r[i + 1].p), T1)) <= ChanHW + 0.06f;
                    }
                    // 池と道のふちの間の細い地面はなくして、溶岩を道のふちまでつなげる（2026-11-03 てじゃ「この青いところの細い地面全部いらんかも」）
                    if (sx != 0)
                    {
                        if (in0 && (sx > 0 ? w0h - r0h : w0h + l0h) <= PoolThinGap) ch0 = true;
                        if (in1 && (sx > 0 ? w1h - r1h : w1h + l1h) <= PoolThinGap) ch1 = true;
                    }
                    // 壁の内向きはこの帯の穴のまん中へ（細長い穴なので全体の中心だと向きをまちがえる）。はしの帯は切れている線のまん中（はしの点 m より外を指すと壁が裏返って抜けた）
                    var o = tip == 0 ? r[i + 1].p + side[i + 1] * c1 : tip == 1 ? r[i].p + side[i] * c0 : (r[i].p + side[i] * c0 + r[i + 1].p + side[i + 1] * c1) * 0.5f;
                    cut.Add((c0, h0, h0, c1, h1, h1, o, tip, m, sx, ch0, ch1, pool.ld + 0.3f));
                }
            cut.Sort((x, y) => x.c0.CompareTo(y.c0));
            Vector3 P0(float x) => r[i].p + side[i] * x;
            Vector3 P1(float x) => r[i + 1].p + side[i + 1] * x;
            float w0 = WAt(pts, hw, r[i].u), w1 = WAt(pts, hw, r[i + 1].u);
            bool openR = cut.Any(h => h.sx > 0 && h.ch0 && h.ch1), openL = cut.Any(h => h.sx < 0 && h.ch0 && h.ch1);
            foreach (var mb in new[] { vis, col })
            {
                int sub = mb == vis ? st : 0;
                void Top(float a0, float a1, float b0, float b1) { if (b0 - a0 > 1e-4f || b1 - a1 > 1e-4f) mb.Quad(P0(a0), P1(a1), P1(b1), P0(b0), Vector3.up, sub); }
                // 上の面：穴と穴の間を区切って（溝の帯は穴からふちまで切れている）
                float bl = mb == vis && !openL ? Bevel : 0f, br = mb == vis && !openR ? Bevel : 0f;   // ベベルは見た目だけ（当たりは今まで通りの角）
                float xa0 = -w0 + bl, xa1 = -w1 + bl;
                foreach (var h in cut)
                {
                    bool open = h.ch0 && h.ch1;
                    float l0 = open && h.sx < 0 ? -w0 : h.c0 - h.hl0, l1 = open && h.sx < 0 ? -w1 : h.c1 - h.hl1;
                    Top(xa0, xa1, l0, l1);
                    xa0 = open && h.sx > 0 ? w0 : h.c0 + h.hr0; xa1 = open && h.sx > 0 ? w1 : h.c1 + h.hr1;
                }
                Top(xa0, xa1, w0 - br, w1 - br);
                if (mb == vis)
                {
                    if (!openR) { BevelEdge(vis, R[i], R[i + 1], side[i], side[i + 1], sub); vis.Quad(R[i] + BevelDown, R[i + 1] + BevelDown, R[i + 1] + dn, R[i] + dn, side[i], sub); }
                    if (!openL) { BevelEdge(vis, L[i], L[i + 1], -side[i], -side[i + 1], sub); vis.Quad(L[i] + BevelDown, L[i + 1] + BevelDown, L[i + 1] + dn, L[i] + dn, -side[i], sub); }
                }
                else
                {
                    if (!openR) mb.Quad(R[i], R[i + 1], R[i + 1] + dn, R[i] + dn, side[i], sub);
                    if (!openL) mb.Quad(L[i], L[i + 1], L[i + 1] + dn, L[i] + dn, -side[i], sub);
                }
                // 穴の前後のはし：縮んだ点 c と、はしの点 m・反対の線のふち2点の間を三角でうめる
                foreach (var h in cut)
                {
                    if (h.tip < 0) continue;
                    Vector3 c = h.tip == 0 ? P0(h.c0) : P1(h.c1);
                    Vector3 e0 = h.tip == 0 ? P1(h.c1 - h.hl1) : P0(h.c0 - h.hl0), e1 = h.tip == 0 ? P1(h.c1 + h.hr1) : P0(h.c0 + h.hr0);
                    mb.Quad(c, e0, h.m, h.m, Vector3.up, sub);
                    mb.Quad(c, h.m, e1, e1, Vector3.up, sub);
                }
                // 穴の壁（内向き）。はしの帯はふち→m→ふち。溝の帯の溝側はなし
                foreach (var h in cut)
                {
                    var pd = Vector3.down * h.wd;
                    foreach (var sg in new[] { -1f, 1f })
                    {
                        if (h.ch0 && h.ch1 && sg == h.sx) continue;
                        Vector3 a = P0(h.c0 + (sg < 0f ? -h.hl0 : h.hr0)), b = P1(h.c1 + (sg < 0f ? -h.hl1 : h.hr1));
                        if (h.tip == 0) a = h.m; if (h.tip == 1) b = h.m;
                        if ((a - b).sqrMagnitude < 1e-6f) continue;
                        mb.Quad(a, b, b + pd, a + pd, Flat(h.o - (a + b) * 0.5f), mb == vis ? 2 : 0);
                    }
                    // 溝の横の壁：溝のはしの線（片方の線だけ溝の中）で、穴のふちから道のふちまで
                    if (h.ch0 != h.ch1)
                    {
                        bool atI = h.ch0;   // 溝のはしの線が i の側
                        Vector3 a = atI ? P0(h.c0 + (h.sx < 0 ? -h.hl0 : h.hr0)) : P1(h.c1 + (h.sx < 0 ? -h.hl1 : h.hr1)), b = atI ? P0(h.sx * w0) : P1(h.sx * w1);
                        var inward = atI ? Flat(r[i].p - r[i + 1].p) : Flat(r[i + 1].p - r[i].p);   // 溝の中へ
                        // 上の面は a–b まであるので、その先に a からの扇で足す（a–b の線に頂点を足さないのでつなぎ目ができない）。岩を足すふたにはしない
                        // ベベルの所は上の面がベベルの内側までなので、扇もそこまで。残りはベベルの形の壁でふさぐ（ななめに下げると上の面との間にすき間が出た）
                        float wr = atI ? w0 : w1; Vector3 Row(float x) => atI ? P0(x) : P1(x);
                        if (mb == vis) b = Row(h.sx * (wr - Bevel));
                        var e = ChanEdge(a, b, inward, b.x * 3.7f + b.z * 1.3f);
                        for (int q = 0; q < e.Length - 1; q++)
                        {
                            mb.Quad(a, e[q], e[q + 1], e[q + 1], Vector3.up, sub);
                            mb.Quad(e[q], e[q + 1], e[q + 1] + pd, e[q] + pd, inward, mb == vis ? 2 : 0);
                        }
                        if (mb == vis)
                        {
                            var m0 = Row(h.sx * (wr - Bevel * 0.3f)) + BevelDown * 0.3f; var m1 = Row(h.sx * wr) + BevelDown;
                            mb.Quad(b, m0, m0 + pd, b + pd, inward, 2);
                            mb.Quad(m0, m1, m1 + pd, m0 + pd, inward, 2);
                        }
                    }
                }
            }
            // 穴の中の溶岩の面（穴の形そのまま。壁の中へ少しもぐらせる）
            if (lavaParent != null)
                foreach (var h in cut)
                {
                    var ld = Vector3.down * (h.wd - 0.3f); const float Over = 0.03f;
                    if (h.tip < 0)
                    {
                        float lx0 = h.c0 - h.hl0 - Over, lx1 = h.c1 - h.hl1 - Over, rx0 = h.c0 + h.hr0 + Over, rx1 = h.c1 + h.hr1 + Over;
                        if (h.ch0 && h.ch1)
                        {
                            // 道のふちまで開いている帯：溶岩をふちまで広げる。垂れる溶岩はあとでふちの端から端まで続けて作る
                            if (h.sx > 0) { rx0 = w0; rx1 = w1; } else { lx0 = -w0; lx1 = -w1; }
                            openEdge.Add((i, h.sx, h.wd - 0.3f));
                        }
                        lava.Quad(P0(lx0) + ld, P1(lx1) + ld, P1(rx1) + ld, P0(rx0) + ld, Vector3.up, 0); continue;
                    }
                    var e0 = h.tip == 0 ? P1(h.c1 - h.hl1 - Over) : P0(h.c0 - h.hl0 - Over); var e1 = h.tip == 0 ? P1(h.c1 + h.hr1 + Over) : P0(h.c0 + h.hr0 + Over);
                    lava.Quad(e0 + ld, h.m + ld, e1 + ld, e1 + ld, Vector3.up, 0);
                }
            if (cut.Count == 0) col.Quad(L[i] + dn, L[i + 1] + dn, R[i + 1] + dn, R[i] + dn, Vector3.down, 0);
            // 下の岩：でこぼこにすぼまる（2026-10-31 てじゃ「下の土みたいなやつがぱっとしない」）
            Skirt(vis, Ri[i] + dn, Ri[i + 1] + dn, Li[i] + dn, Li[i + 1] + dn, side[i]);
            // 帯の下のはしと岩の間をふさぐ（下から空が線に見えた）
            vis.Quad(R[i] + dn, R[i + 1] + dn, Ri[i + 1] + dn, Ri[i] + dn, Vector3.down, 2);
            vis.Quad(L[i] + dn, L[i + 1] + dn, Li[i + 1] + dn, Li[i] + dn, Vector3.down, 2);
        }
        // はしのふた
        for (int e = 0; e < 2; e++)
        {
            int i = e == 0 ? 0 : n - 1; var o = Flat(r[e == 0 ? 0 : n - 1].p - r[e == 0 ? 1 : n - 2].p).normalized;
            int st = e == 0 ? 0 : Mathf.FloorToInt(s[n - 1] / StripeLen) % 2;
            vis.Quad(L[i] + side[i] * Bevel, R[i] - side[i] * Bevel, R[i] + dn, L[i] + dn, o, st); col.Quad(L[i], R[i], R[i] + dn, L[i] + dn, o, 0);
            SkirtEnd(vis, Li[i] + dn, Ri[i] + dn, o);
        }
        LavaCurtains(curtain, r, side, s, pts, hw, openEdge);
        MeshObj(parent, name, vis.Build("Course2_2_" + name, 3), col.Build("Course2_2_" + name + "_Col", 1), new[] { pathA, pathB, rockMat });
        if (lavaParent != null)
        {
            // うその影（FuwaGroundMarker）は真下へ線を飛ばすので、溶岩にも当たりがいる（無いと穴の上で影が消える）。ふわふわも人も通りぬけるレイヤーにする
            var lp = MeshObj(lavaParent, "LavaPools", lava.Build("Course2_2_LavaPools", 1), lava.Build("Course2_2_LavaPools_Col", 1), new[] { lavaMat });
            lp.layer = LavaShadowLayer;
            if (openEdge.Count > 0)
            {
                // 垂れる溶岩は別の物にして、ゆっくり波打たせる。流れも遅く（2026-11-03 てじゃ「カチッと固まってるの違和感」「表面の流れが早いかも」）
                var cg = MeshObj(lavaParent, "LavaCurtains", curtain.Build("Course2_2_LavaCurtains", 1), null, new[] { LavaCurtainMat(lavaParent.TransformPoint(r[openEdge[0].i].p + Vector3.down * openEdge[0].ld).y) });
                cg.layer = LavaShadowLayer; var cr = cg.GetComponent<MeshRenderer>(); cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cr.receiveShadows = false;
            }
            foreach (var mk in Object.FindObjectsOfType<FuwaGroundMarker>(true))
            {
                mk.groundMask |= 1 << LavaShadowLayer; EditorUtility.SetDirty(mk);
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(mk);
            }
        }
    }

    // ジグザグの 1 本道：折り返しは内側の角（板の内側のふちの交点）を軸に道を扇のように回して、外側がふくらむ。シマシマはつながり、下の岩も道と同じ。
    //  当たりも同じ形。折り返しの間は平らで、板の部分だけ坂（前の板より少し急）
    public static float ZigBulge = 2.9f, ZigSeedOut = 1.35f, ZigLedge = 1.15f;   // 折り返しの外の岩の足場の幅（2026-10-31 てじゃ「頂点の先ちょっとだけ広く」「道の周りに岩の足場みたいなの」）
    static void ZigPath(Transform parent, Vector3 startL, Vector3 startR, Vector3 startDir, List<(Vector3 a, Vector3 b)> legs, List<Vector3> nodes, Vector3 endL, Vector3 endR, Vector3 endDir)
    {
        const float T = 0.6f, In = 0.1f, Ease = 1.2f, FanS = 4f; float hw = ZigHW; int n = legs.Count;
        var dirs = legs.Select(l => Flat(l.b - l.a).normalized).ToArray(); var sides = dirs.Select(d => Vector3.Cross(Vector3.up, d).normalized).ToArray(); var lens = legs.Select(l => Flat(l.b - l.a).magnitude).ToArray();
        Vector3 At(int k, float s, float x) { var q = Flat(legs[k].a) + dirs[k] * s + sides[k] * x; return q; }
        var sStart = new float[n]; var sEnd = new float[n]; var pivot = new Vector3[n - 1]; var inner = new float[n - 1];
        sStart[0] = FanS; sEnd[n - 1] = lens[n - 1] - FanS;
        for (int k = 0; k + 1 < n; k++)
        {
            inner[k] = Vector3.Cross(dirs[k], dirs[k + 1]).y > 0f ? 1f : -1f;   // 右へ折り返すなら右が内側
            Vector3 p = At(k, 0f, inner[k] * hw), q = At(k + 1, 0f, inner[k] * hw); float den = dirs[k].x * dirs[k + 1].z - dirs[k].z * dirs[k + 1].x;
            float u = ((q.x - p.x) * dirs[k + 1].z - (q.z - p.z) * dirs[k + 1].x) / den;
            pivot[k] = p + dirs[k] * u; sEnd[k] = u; sStart[k + 1] = Vector3.Dot(pivot[k] - Flat(legs[k + 1].a), dirs[k + 1]);
        }
        var L = new List<Vector3>(); var R = new List<Vector3>(); var ledges = new List<(int i0, int i1, bool outerL)>();
        void Row(Vector3 l, Vector3 r, float y) { l.y = y; r.y = y; L.Add(l); R.Add(r); }
        Vector3 Bz(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) { float v = 1f - t; return v * v * v * a + 3f * v * v * t * b + 3f * v * t * t * c + t * t * t * d; }
        void Blend(Vector3 l0, Vector3 r0, Vector3 d0, Vector3 l1, Vector3 r1, Vector3 d1, float y, int m)
        {
            float lenL = (Flat(l1 - l0)).magnitude * 0.4f, lenR = (Flat(r1 - r0)).magnitude * 0.4f;
            for (int i = 1; i < m; i++) { float t = i / (float)m; Row(Bz(l0, l0 + d0 * lenL, l1 - d1 * lenL, l1, t), Bz(r0, r0 + d0 * lenR, r1 - d1 * lenR, r1, t), y); }
        }
        Row(startL, startR, startL.y);
        Blend(Flat(startL), Flat(startR), startDir, At(0, sStart[0], -hw), At(0, sStart[0], hw), dirs[0], startL.y, 16);
        for (int k = 0; k < n; k++)
        {
            float y0 = nodes[k].y, y1 = nodes[k + 1].y, span = sEnd[k] - sStart[k], e = Mathf.Min(Ease / span, 0.3f), peak = 1f / (1f - e);
            int m = Mathf.CeilToInt(span / 0.3f);
            for (int i = 0; i <= m; i++)
            {
                float t = i / (float)m, f = t < e ? peak * t * t / (2f * e) : t > 1f - e ? 1f - peak * (1f - t) * (1f - t) / (2f * e) : peak * (t - e * 0.5f), s = Mathf.Lerp(sStart[k], sEnd[k], t);
                Row(At(k, s, -hw), At(k, s, hw), Mathf.Lerp(y0, y1, f));
            }
            if (k + 1 >= n) break;
            // 折り返し：内側は軸の 1 点、外側はふくらむ曲線（まん中が火種から ZigBulge の所）
            float ik = inner[k]; Vector3 o0 = At(k, sEnd[k], -ik * hw), o1 = At(k + 1, sStart[k + 1], -ik * hw), c = Flat(nodes[k + 1]);
            float lo = 0.2f, hi = 12f; for (int q = 0; q < 24; q++) { float mid = (lo + hi) * 0.5f; if ((Bz(o0, o0 + dirs[k] * mid, o1 - dirs[k + 1] * mid, o1, 0.5f) - c).magnitude < ZigBulge) lo = mid; else hi = mid; }
            const int Fan = 28; int fan0 = L.Count - 1;
            for (int i = 1; i < Fan; i++)
            {
                var oe = Bz(o0, o0 + dirs[k] * lo, o1 - dirs[k + 1] * lo, o1, i / (float)Fan);
                if (ik > 0f) Row(oe, pivot[k], y1); else Row(pivot[k], oe, y1);
            }
            ledges.Add((fan0, L.Count, ik > 0f));
        }
        Blend(At(n - 1, sEnd[n - 1], -hw), At(n - 1, sEnd[n - 1], hw), dirs[n - 1], Flat(endL), Flat(endR), endDir, nodes[n].y, 16);
        Row(endL, endR, endL.y);
        var vis = new MB(); var col = new MB(); var dn = Vector3.down * T; int folds = 0; float sAcc = 0f;
        for (int i = 0; i + 1 < L.Count; i++)
        {
            Vector3 l0 = L[i], l1 = L[i + 1], r0 = R[i], r1 = R[i + 1], m0 = (l0 + r0) * 0.5f, m1 = (l1 + r1) * 0.5f, f = Flat(m1 - m0); float seg = f.magnitude;
            if (Vector3.Dot(l1 - l0, f) < -1e-4f || Vector3.Dot(r1 - r0, f) < -1e-4f) folds++;
            int st = Mathf.FloorToInt((sAcc + seg * 0.5f) / StripeLen) % 2; sAcc += seg;
            Vector3 o0 = Flat(l0 - r0).normalized, o1 = Flat(l1 - r1).normalized;
            col.Quad(l0, l1, r1, r0, Vector3.up, 0); col.Quad(l0 + dn, l1 + dn, r1 + dn, r0 + dn, Vector3.down, 0);
            col.Quad(l0, l1, l1 + dn, l0 + dn, o0, 0); col.Quad(r0, r1, r1 + dn, r0 + dn, -o0, 0);
            vis.Quad(l0 - o0 * Bevel, l1 - o1 * Bevel, r1 + o1 * Bevel, r0 + o0 * Bevel, Vector3.up, st);
            BevelEdge(vis, l0, l1, o0, o1, st); BevelEdge(vis, r0, r1, -o0, -o1, st);
            vis.Quad(l0 + BevelDown, l1 + BevelDown, l1 + dn, l0 + dn, o0, st); vis.Quad(r0 + BevelDown, r1 + BevelDown, r1 + dn, r0 + dn, -o0, st);
            Vector3 li0 = l0 - o0 * In + dn, li1 = l1 - o1 * In + dn, ri0 = r0 + o0 * In + dn, ri1 = r1 + o1 * In + dn;
            vis.Quad(l0 + dn, l1 + dn, li1, li0, Vector3.down, 2); vis.Quad(r0 + dn, r1 + dn, ri1, ri0, Vector3.down, 2);
            Skirt(vis, ri0, ri1, li0, li1, -o0);
        }
        if (folds > 0) Debug.LogWarning("[2–2] ZigPath 道の面の折り重なり " + folds + " か所");
        // 折り返しの外の岩の足場（てじゃ「すじまあるし、段差がめだつ、もっとつなぎ目ないようにくっつけて」）：別の段を横に置くのをやめて、道のベベルの下から直接ゆるく下がる岩の肩に。
        //  道ぞいに細くなって幅 0 で終わる（切り口なし）。下は道の下の岩（Skirt）の 0.75 の段の点へそのままつなぐ。道の坂の高さにもついていく
        foreach (var lg in ledges)
        {
            const int Ext = 15; const float Slope = 0.05f, Rim = 0.1f;
            int a0 = Mathf.Max(0, lg.i0 - Ext), a1 = Mathf.Min(L.Count - 1, lg.i1 + Ext);
            float Taper(int i) => Mathf.SmoothStep(0f, 1f, Mathf.Min(i - a0, a1 - i) / (float)Ext);
            (Vector3 top0, Vector3 top1, Vector3 rim, Vector3 mid, Vector3 low, Vector3 d) Prof(int i)
            {
                Vector3 O = lg.outerL ? L[i] : R[i], I = lg.outerL ? R[i] : L[i], D = Flat(O - I).normalized, down = Vector3.down; float tp = Taper(i);
                float w = ZigLedge * tp * (0.72f + 0.56f * Mathf.PerlinNoise(O.x * 0.9f + 3f, O.z * 0.9f + 8f));
                // 岩の上のふちは、はしでは道の側面の下から出て、だんだんベベルの下まで上がる（上から出すと下にピンクの三角が残って切れ目に見えた。2026-10-31 てじゃ）
                var t0 = O + down * Mathf.Lerp(T, Bevel, Mathf.SmoothStep(0f, 1f, tp / 0.7f)); var t1 = t0 + D * w + down * (Slope * tp); var rm = t1 + (D + down) * (Rim * tp);
                // 下へ内側へすぼめて、深さもでこぼこに（まっすぐの壁だと箱に見えた）
                float nz = Mathf.PerlinNoise(O.x * 1.3f + 11f, O.z * 1.3f + 2f); var md = O + D * (w * (0.15f + 0.35f * nz) - 0.02f * (1f - tp)) + down * (T + (0.25f + w * (0.45f + 0.7f * nz)) * tp);
                Vector3 li = O - D * In + dn, ri = I + D * In + dn;
                return (t0, t1, rm, md, SkirtPoint(li, (li + ri) * 0.5f, 0.75f), D);
            }
            for (int i = a0; i < a1; i++)
            {
                var p = Prof(i); var q = Prof(i + 1);
                vis.Quad(p.top0, q.top0, q.top1, p.top1, Vector3.up, 2); vis.Quad(p.top1, q.top1, q.rim, p.rim, p.d + Vector3.up, 2);
                vis.Quad(p.rim, q.rim, q.mid, p.mid, p.d, 2); vis.Quad(p.mid, q.mid, q.low, p.low, p.d + Vector3.down, 2);
                col.Quad(p.top0, q.top0, q.top1, p.top1, Vector3.up, 0); col.Quad(p.top1, q.top1, q.mid, p.mid, p.d, 0); col.Quad(p.mid, q.mid, q.low, p.low, p.d + Vector3.down, 0);
            }
        }
        MeshObj(parent, "ZigPath", vis.Build("Course2_2_ZigPath", 3), col.Build("Course2_2_ZigPath_Col", 1), new[] { pathA, pathB, rockMat });
    }

    // BGM（2026-11-01 てじゃ E「ようがんのこどう」、Tools/audio/make_bgm_hono.py の hono_e_long）。2–1 と同じく FuwaSystem/BGM に足して、作り直すたびに BallStart をつなぎ直す
    const string BgmClip = "Assets/FuwaStage2/Audio/bgm_hono_march4.wav";   // 太鼓と小さいベースで開始→重い行進→倍の速さ→太鼓の連打→行進→ちゃんと終わる（Tools/audio/make_bgm_hono_rhythm.py の march、2026-11-01）。前は bgm_hono_march、bgm_hono_long2、bgm_hono_long
    const float BgmGain = 2.5f;
    static void RegisterBgm(Transform root)
    {
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault(); if (!bgm) return;
        var start = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BallStart"); if (!start) return;
        var src = bgm.transform.Find("BGM_course2_1"); if (!src) return;
        var tr = bgm.transform.Find("BGM_course2_2");
        if (!tr) { tr = Object.Instantiate(src.gameObject, bgm.transform, false).transform; tr.name = "BGM_course2_2"; }
        var a = tr.GetComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmClip); EditorUtility.SetDirty(a);
        var so = new SerializedObject(bgm);
        var tracks = so.FindProperty("tracks"); var starts = so.FindProperty("starts");
        int idx = -1;
        for (int i = 0; i < tracks.arraySize; i++) if (tracks.GetArrayElementAtIndex(i).objectReferenceValue == a) idx = i;
        if (idx < 0) { idx = tracks.arraySize; tracks.arraySize++; tracks.GetArrayElementAtIndex(idx).objectReferenceValue = a; }
        while (starts.arraySize <= idx) { starts.arraySize++; starts.GetArrayElementAtIndex(starts.arraySize - 1).objectReferenceValue = null; }
        starts.GetArrayElementAtIndex(idx).objectReferenceValue = start;
        var gains = so.FindProperty("trackGains");   // てじゃ「もっと大きくて良い」。音源を上げるとつぶれるので再生の倍率で
        while (gains.arraySize <= idx) { gains.arraySize++; gains.GetArrayElementAtIndex(gains.arraySize - 1).floatValue = 1f; }
        gains.GetArrayElementAtIndex(idx).floatValue = BgmGain;
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bgm);
    }

    // 火種：地面の燃えさし＋炎＋熱の範囲（かがり火と同じ FuwaHeatSource、止まる所は地面の SeedHover 上）
    public static readonly Vector3[] WideSeedSpots = { new Vector3(-3.39f, 0f, 38.62f), new Vector3(3.93f, 0f, 47.23f) };
    // シーンの今の 2–2 に広い道の焚き火だけ足す（作り直さずに）
    public static string AddWideSeeds(Transform root)
    {
        var fire = root.Find("Fire"); heat = root.GetComponentInChildren<FuwaHeat>(true); rockMat = PathSideMat();
        var log = "";
        for (int k = 0; k < WideSeedSpots.Length; k++)
        {
            var old = fire.Find("WideSeed" + k); if (old) Object.DestroyImmediate(old.gameObject);
            var q = WideSeedSpots[k]; Physics.SyncTransforms();
            var from = root.TransformPoint(new Vector3(q.x, 20f, q.z));
            if (!Physics.Raycast(from, Vector3.down, out var hit, 40f, 1 << 11, QueryTriggerInteraction.Ignore)) { log += "道がない " + k + "\n"; continue; }
            var y = root.InverseTransformPoint(hit.point).y;
            GroundFire(fire, "WideSeed" + k, new Vector3(q.x, y, q.z), new Vector3(Mathf.Sign(q.x), 0f, 0f));
            log += "WideSeed" + k + " y=" + y.ToString("F2") + " 当たり=" + hit.collider.name + "\n";
        }
        return log;
    }
    static void GroundFire(Transform parent, string name, Vector3 lp, Vector3 outward)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = lp;
        SeedLook(g, outward);
        var z = new GameObject("HeatZone"); z.transform.SetParent(g, false); z.transform.localPosition = new Vector3(0, 0.9f, 0); z.layer = 4;
        var bc = z.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(2.0f, 1.8f, 2.0f);
        var hp = new GameObject("HoverPoint").transform; hp.SetParent(g, false); hp.localPosition = new Vector3(0, SeedHover, 0);
        var hs = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHeatSource>(z); hs.heat = heat; hs.hoverPoint = hp;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hs); FuwaStage2WaterTest.NoSync(hs);
        HeatWisp(g);
    }
    // 焚き火の上に立ちのぼる、ごく控えめな風の筋（2026-11-03 てじゃ「ささやかな風エフェクト」「ほんとちょっとね」）。噴気孔の SteamJet と同じ材質の細い筋。何回呼んでも同じ結果
    public static void HeatWisp(Transform g)
    {
        var old = g.Find("HeatWisp"); if (old) Object.DestroyImmediate(old.gameObject);
        var o = new GameObject("HeatWisp"); o.transform.SetParent(g, false); o.transform.localPosition = new Vector3(0f, 0.8f, 0f); o.transform.rotation = Quaternion.LookRotation(Vector3.up);
        var ps = o.AddComponent<ParticleSystem>();
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.2f); m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new Color(1f, 0.97f, 0.92f, 0.6f); m.maxParticles = 12; m.simulationSpace = ParticleSystemSimulationSpace.World;
        var e = ps.emission; e.rateOverTime = 8f;
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 5f; s.radius = 0.35f;
        var c = ps.colorOverLifetime; c.enabled = true; var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) }); c.color = gr;
        var t = ps.textureSheetAnimation; t.enabled = true; t.numTilesX = 4; t.numTilesY = 4; t.animation = ParticleSystemAnimationType.WholeSheet; t.frameOverTime = new ParticleSystem.MinMaxCurve(0f); t.startFrame = new ParticleSystem.MinMaxCurve(0f, 15f / 16f);
        var r = o.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = SteamMat(); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 9f; r.velocityScale = 0f; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play();
    }

    // 火種と噴気孔の見た目（2026-10-31 てじゃ「火種と噴気孔をきれいに」。前は平らな円盤とぼやけた粒）。当たりも遊びも変えない
    public static Material SteamMat()
    {
        const string mp = "Assets/FuwaStage2/Materials/Stage2_2_VolcanoSteam.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!mat) { mat = new Material(Shader.Find("FuwaCourse/SteamPuff")); mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/SteamPuffSheet.png"); AssetDatabase.CreateAsset(mat, mp); }
        return mat;
    }
    // 道の面の向きに合わせる（坂の上で平らな物を置くと半分うまった）。道の当たりがまだ無い所は上向き
    static Transform OnSurface(Transform g, string name)
    {
        var c = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform; c.SetParent(g, false);
        Physics.SyncTransforms();
        c.rotation = Physics.Raycast(g.position + Vector3.up, Vector3.down, out var hit, 2f, 1 << 11, QueryTriggerInteraction.Ignore) ? Quaternion.FromToRotation(Vector3.up, hit.normal) : Quaternion.identity;
        var r = c.GetComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return c;
    }
    // 火種：道にあけた穴の中の焚き火。炎はふわふわの止まる高さ（SeedHover）より低く（ふわふわが炎に隠れないように）。
    //  石やまきが道の上にあると障害物に見えた（2026-10-31 てじゃ「穴あけてそこで燃やす」）ので、道の上は低いふちだけ。穴は噴気孔の縦穴と同じステンシルで見せる（当たりは道のまま）
    public const float SeedPitDepth = 0.5f;
    static void SeedLook(Transform g, Vector3 outward)
    {
        var b0 = OnSurface(g, "Base"); var ol = b0.InverseTransformDirection(g.TransformDirection(outward)); ol.y = 0f; float a00 = Mathf.Atan2(ol.z, ol.x);
        const int Seg = 32; const float MouthY = 0.025f;
        float R(int n) { float t = n * Mathf.PI * 2f / Seg; return 0.5f + 0.03f * Mathf.Sin(3f * t + 1f) + 0.018f * Mathf.Sin(7f * t); }   // 一周でつながるよう整数倍の波
        Vector3 Q(int n, float r, float y)   // y は道の面からの高さ（角や坂の折れ目で道が平らでなく、ふちが浮いた）
        {
            float t = n * Mathf.PI * 2f / Seg; var lp = new Vector3(Mathf.Cos(t) * r, 0f, Mathf.Sin(t) * r);
            if (Physics.Raycast(b0.TransformPoint(lp) + Vector3.up, Vector3.down, out var hit, 2f, 1 << 11, QueryTriggerInteraction.Ignore)) lp.y = b0.InverseTransformPoint(hit.point).y;
            lp.y += y; return lp;
        }
        // ふち：道の上は低い焦げた輪だけ
        var mb = new MB();
        float Soil(int n) { float t = n * Mathf.PI * 2f / Seg; return Mathf.Min(R(n) * 1.26f + 0.17f + 0.07f * Mathf.Sin(2f * t + 0.5f) + 0.04f * Mathf.Sin(5f * t + 1.3f), 0.95f); }   // 土は穴より広く（2026-10-31 てじゃ「土をもっと横とかにも広げて」）
        for (int k = 0; k < Seg; k++)
        {
            mb.Quad(Q(k, Soil(k), -0.012f), Q(k + 1, Soil(k + 1), -0.012f), Q(k + 1, R(k + 1) * 1.26f, 0.02f), Q(k, R(k) * 1.26f, 0.02f), Vector3.up, 0);
            mb.Quad(Q(k, R(k) * 1.26f, 0.02f), Q(k + 1, R(k + 1) * 1.26f, 0.02f), Q(k + 1, R(k + 1) * 1.1f, 0.035f), Q(k, R(k) * 1.1f, 0.035f), Vector3.up, 0);
            mb.Quad(Q(k, R(k) * 1.1f, 0.035f), Q(k + 1, R(k + 1) * 1.1f, 0.035f), Q(k + 1, R(k + 1), MouthY), Q(k, R(k), MouthY), Vector3.up, 0);
        }
        b0.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mb.Build("Course2_2_SeedBase_" + g.name, 1));
        b0.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
        // 穴の口と内側。ふちと同じ点でつなぐ
        var mv = new List<Vector3> { Vector3.zero }; var mt = new List<int>();
        var sv = new List<Vector3>(); var suv = new List<Vector2>(); var st = new List<int>();
        var rings = new[] { (k: 1f, y: MouthY, d: 0f), (k: 0.95f, y: -SeedPitDepth * 0.5f, d: 0.5f), (k: 0.86f, y: -SeedPitDepth, d: 1f) };
        for (int k = 0; k <= Seg; k++)
        {
            mv.Add(Q(k, R(k), MouthY)); mv[0] = Vector3.up * Mathf.Max(k == 0 ? float.NegativeInfinity : mv[0].y, mv[k + 1].y);   // 口のまん中はふちの一番高い所に（道の折れ目が口を突き抜けないように）
            foreach (var q in rings) { sv.Add(Q(k, R(k) * q.k, q.y)); suv.Add(new Vector2(0f, q.d)); }
        }
        for (int k = 0; k < Seg; k++)
        {
            mt.AddRange(new[] { 0, k + 2, k + 1 });
            for (int j = 0; j + 1 < rings.Length; j++) { int a = k * rings.Length + j, n = rings.Length; st.AddRange(new[] { a, a + 1, a + n + 1, a, a + n + 1, a + n }); }
        }
        int fc = sv.Count; sv.Add(Vector3.down * SeedPitDepth); suv.Add(new Vector2(0f, 1f)); int fb = sv.Count;   // 底
        for (int k = 0; k <= Seg; k++) { sv.Add(Q(k, R(k) * 0.86f, -SeedPitDepth)); suv.Add(new Vector2(0f, 1f)); }
        for (int k = 0; k < Seg; k++) st.AddRange(new[] { fc, fb + k + 1, fb + k });
        Transform Part(string nm, Mesh me, params Material[] ms)
        {
            var c = new GameObject(nm, typeof(MeshFilter), typeof(MeshRenderer)).transform; c.SetParent(b0, false);
            c.GetComponent<MeshFilter>().sharedMesh = me; var r = c.GetComponent<MeshRenderer>(); r.sharedMaterials = ms; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return c;
        }
        var mouth = new Mesh { name = "Course2_2_SeedMouth_" + g.name }; mouth.SetVertices(mv); mouth.SetTriangles(mt, 0); mouth.RecalculateNormals(); mouth.RecalculateBounds();
        Part("Mouth", SaveMesh(mouth), AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/VentHoleMask.mat"));
        var shaft = new Mesh { name = "Course2_2_SeedPit_" + g.name }; shaft.SetVertices(sv); shaft.SetUVs(0, suv); shaft.SetTriangles(st, 0); shaft.RecalculateNormals(); shaft.RecalculateBounds();
        Part("Pit", SaveMesh(shaft), SeedPitMat());
        // まき：穴の底で組んで、先は道の面まで。炎に近い側は焦げ色、切り口は明るい木の色
        var lb = new MB(); const int Logs = 5, LogN = 7;
        for (int i = 0; i < Logs; i++)
        {
            float a = a00 + 0.4f + i * Mathf.PI * 2f / Logs + 0.12f * Mathf.Sin(i * 2.3f), reach = 0.43f + 0.02f * Mathf.Sin(i * 3.7f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var foot = dir * reach + Vector3.down * (SeedPitDepth - 0.06f); var tip = dir * 0.06f + Vector3.down * (0.05f + 0.03f * Mathf.Sin(i * 5.1f));
            var ax = (tip - foot).normalized; var s1 = Vector3.Cross(ax, dir).normalized; var s2 = Vector3.Cross(ax, s1);
            Vector3 Ring(float t, int k) { float b = k * Mathf.PI * 2f / LogN; return Vector3.Lerp(foot, tip, t) + (s1 * Mathf.Cos(b) + s2 * Mathf.Sin(b)) * Mathf.Lerp(0.075f, 0.05f, t); }
            var bands = new[] { (t0: 0f, t1: 0.55f, sub: 0), (t0: 0.55f, t1: 1f, sub: 1) };
            for (int k = 0; k < LogN; k++)
            {
                foreach (var bd in bands) lb.Quad(Ring(bd.t0, k), Ring(bd.t0, k + 1), Ring(bd.t1, k + 1), Ring(bd.t1, k), Ring(bd.t0, k) + Ring(bd.t0, k + 1) - foot * 2f, bd.sub);
                lb.Quad(Ring(0f, k), Ring(0f, k + 1), foot, foot, -ax, 2);
                lb.Quad(Ring(1f, k), Ring(1f, k + 1), tip, tip, ax, 1);
            }
        }
        Part("Logs", SaveMesh(lb.Build("Course2_2_SeedLogs_" + g.name, 3)), SeedPitToon("Stage2_2_FireLog", new Color(0.52f, 0.32f, 0.2f)), SeedPitToon("Stage2_2_FireLogChar", new Color(0.26f, 0.13f, 0.11f)), SeedPitToon("Stage2_2_FireLogEnd", new Color(0.62f, 0.45f, 0.32f)));
        var fl = new GameObject("Flames", typeof(MeshFilter), typeof(MeshRenderer)).transform; fl.SetParent(b0, false); fl.localPosition = Vector3.down * (SeedPitDepth - 0.1f); fl.localScale = Vector3.one * SeedFlameScale;   // 穴の底から道の上 1.1m ほどまで
        fl.GetComponent<MeshFilter>().sharedMesh = SeedFlameMesh(); var fr = fl.GetComponent<MeshRenderer>(); fr.sharedMaterial = SeedFlameMat(); fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fr.receiveShadows = false;
        var ps = FuwaStage2FireTest.FlamePS(g, "Sparks", 0.35f, 1.5f, 1.2f, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.32f, 0.06f));
        var m = ps.main; m.startColor = Color.white; m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); m.maxParticles = 12; var em = ps.emission; em.rateOverTime = 5f; ps.Play();
    }
    // 焚き火と燃える島の炎：FuwaCourse/FlameMesh（2026-11-01 てじゃの参考 GIF）。外のたまご＋中の黄色＋ちぎれて飛ぶかたまり。動きはシェーダーの頂点で作る
    //  レイで見る FlameVolume は見た目はよかったが、近づくと前の炎の 5〜6 倍重かった（てじゃ「重くなるのはやだ」）
    // 炎上の見た目（2026-11-01 てじゃ「フワフワの炎上とまものの炎上Particle、もうちょいいい感じに」→ 比較して案2）：
    // 焚き火と同じ炎のメッシュを頭の上に大きめに＋ふちのくっきりした粒。顔は隠さない。s は体の大きさ(m)
    public static float BurnGrace = 3f;   // 燃えてからアウトまで 3.5→3（てじゃ「ちょっとだけ短く」）
    public const float BallBurnS = 0.3f, MonsterBurnS = 0.46f;
    static ParticleSystem BurnBlobs(Transform parent, string name, float s, float rate)
    {
        var fo = new GameObject(name); fo.transform.SetParent(parent, false); fo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = fo.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f); m.startSpeed = new ParticleSystem.MinMaxCurve(s * 1.2f, s * 2.4f);
        m.startSize = new ParticleSystem.MinMaxCurve(s * 0.21f, s * 0.49f); m.startColor = Color.white; m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.05f; m.maxParticles = 30;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = s * 0.35f; sh.position = new Vector3(0f, 0f, s * 0.6f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(new Color32(0xFF, 0xE2, 0x7A, 0xFF), 0f), new GradientColorKey(new Color32(0xFF, 0xA1, 0x4D, 0xFF), 0.35f), new GradientColorKey(new Color32(0xF2, 0x60, 0x3A, 0xFF), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        var pr = fo.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = FlameBlobMat(); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        return ps;
    }
    static Material FlameBlobMat()
    {
        const string p = "Assets/FuwaStage2/Materials/Stage2_2_FlameBlob.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(Shader.Find("FuwaCourse/FlameBlob")); AssetDatabase.CreateAsset(m, p); }
        return m;
    }
    static GameObject BurnFlame(Transform parent, string name, float s)
    {
        var o = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(parent, false);
        o.transform.localPosition = Vector3.down * (s * 0.05f);
        var ls = parent.lossyScale; o.transform.localScale = new Vector3(s * 0.95f / ls.x, s * 0.95f / ls.y, s * 0.95f / ls.z);
        o.GetComponent<MeshFilter>().sharedMesh = IsleFlameMesh();
        var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = SeedFlameMat(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return o;
    }
    static void BallBurnLook(Transform hg, FuwaHeat h)
    {
        h.burnFx = BurnBlobs(hg, "BurnFx", BallBurnS, 0f); { var m = h.burnFx.main; m.playOnAwake = true; } h.burnFx.Play();
        h.burnRate = 12f;
        h.burnMesh = BurnFlame(hg, "BurnFlame", BallBurnS); h.burnMesh.SetActive(false);
        h.burnMeshOffset = Vector3.down * (BallBurnS * 0.05f);
    }
    static ParticleSystem MonsterBurnLook(FuwaEnemy e, Transform g)
    {
        var parent = e.visual ? e.visual : g;
        var ps = BurnBlobs(parent, "BodyFlame", MonsterBurnS, 12f); { var m = ps.main; m.playOnAwake = true; }
        e.flameMesh = BurnFlame(parent, "BodyFlameMesh", MonsterBurnS);
        return ps;
    }
    // ふっ飛ばし（てじゃ「ここのまもののふっ飛ばし力アップ、ふわふわもプレイヤも」）。前はふわふわ 3.2 / 上 0.8、人 3.6 / 上 4
    public static float MonsterBallKnock = 3.2f, MonsterBallKnockUp = 0.8f, MonsterPlayerKnock = 3.6f, MonsterPlayerKnockUp = 4f;   // 2026-11-01 に 1.5 倍にしたけど、当たり判定を広げたらエグいので戻した（2026-11-02 てじゃ）
    public static float MonsterChase = 11f;   // 気づいた後に追い続ける距離（前は 6.5）
    public static float MonsterLeash = 14f;   // 5.5→14（てじゃ「もっと追いかけても良いかも」）
    // ほのおのまものは家へ戻る時に水たまりをよける
    public static int ApplyMonsterAvoid(Transform root)
    {
        var zones = root.GetComponentsInChildren<FuwaWater>(true).Select(w => w.GetComponent<Collider>()).Where(c => c != null).ToArray();
        var cps = root.Find("Checkpoints");
        foreach (var e in root.GetComponentsInChildren<FuwaEnemy>(true))
        {
            e.avoidZones = zones;
            e.leashRange = MonsterLeash; e.chaseRange = MonsterChase;
            e.ballKnockback = MonsterBallKnock; e.ballKnockUp = MonsterBallKnockUp; e.knockback = MonsterPlayerKnock; e.knockUp = MonsterPlayerKnockUp;
            // 追いかけるのはチェックポイント3と4のあいだだけ（てじゃ「チェックポイント以降はダメ」）
            if (cps) { e.chaseLimitBack = cps.Find("Checkpoint3"); e.chaseLimit = cps.Find("Checkpoint4"); }
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(e);
        }
        return zones.Length;
    }
    // シーンの今の 2–2 の炎上の見た目だけ差し替える（作り直さずに）
    public static string ApplyBurnLook(Transform root)
    {
        var log = "";
        var h = root.GetComponentInChildren<FuwaHeat>(true);
        foreach (var n in new[] { "BurnFx", "BurnFlame" }) { var o = h.transform.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
        BallBurnLook(h.transform, h);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(h);
        foreach (var e in root.GetComponentsInChildren<FuwaEnemy>(true))
        {
            var parent = e.visual ? e.visual : e.transform;
            foreach (var n in new[] { "BodyFlame", "BodyFlameMesh" }) { var o = parent.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
            e.flameFx = MonsterBurnLook(e, e.transform);
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(e);
            log += e.name + " ";
        }
        return log;
    }
    public const float SeedFlameScale = 0.7f;   // 焚き火の炎の大きさ（2026-11-01 てじゃ「通れなさそうにみえる」）。島の炎は別
    public static Mesh SeedFlameMesh()
    {
        var f = new FlameMeshB();
        f.Egg(0f, Vector3.zero, 0.42f, 0.29f, 2.5f, 0.95f, 24, 24, 0f, 1.6f);
        f.Egg(1f, Vector3.zero, 0.38f, 0.17f, 1.8f, 0.9f, 12, 16, 0f, 1.6f);
        for (int n = 0; n < 5; n++) { float seed = (n + 0.37f) * 0.19f; f.Blob(new Vector3(Mathf.Sin(seed * 41f) * 0.14f, 0.92f, Mathf.Cos(seed * 29f) * 0.14f), seed, 0.6f); }
        return f.Build("Course2_2_FlameMesh", 0.7f);
    }
    // 燃える島の炎：焚き火と同じ形を粗く（2026-11-01 てじゃ「無駄なハイポリ」。島は道から遠いので 1836→約970 三角形）
    public static Mesh IsleFlameMesh()
    {
        var f = new FlameMeshB();
        f.Egg(0f, Vector3.zero, 0.42f, 0.29f, 2.5f, 0.95f, 16, 16, 0f, 1.6f);
        f.Egg(1f, Vector3.zero, 0.38f, 0.17f, 1.8f, 0.9f, 8, 10, 0f, 1.6f);
        for (int n = 0; n < 5; n++) { float seed = (n + 0.37f) * 0.19f; f.Blob(new Vector3(Mathf.Sin(seed * 41f) * 0.14f, 0.92f, Mathf.Cos(seed * 29f) * 0.14f), seed, 0.6f); }
        return f.Build("Course2_2_IsleFlameMesh", 0.7f);
    }
    // FlameMesh の材質。ノイズは使い回し、形の数字だけ変える
    static Material FlameMeshMat(string name, float noiseScale, float flow, float ampBase, float ampTop, float tear, float sway, float blobRise, float blobSize, float bias)
    {
        var m = FuwaStage2WaterTest.ToonMat(name, Color.white); m.shader = Shader.Find("FuwaCourse/FlameMesh"); FireFadeLook(m);
        m.SetTexture("_Noise", FlameNoise3D()); m.SetFloat("_Speed", 1f); m.SetFloat("_NoiseScale", noiseScale); m.SetFloat("_FlowSpeed", flow); m.SetFloat("_AmpBase", ampBase); m.SetFloat("_AmpTop", ampTop);
        m.SetFloat("_Tear", tear); m.SetFloat("_Sway", sway); m.SetFloat("_BlobRise", blobRise); m.SetFloat("_BlobSize", blobSize); m.SetFloat("_Bias", bias);
        // まわりの淡い色になじませる（2026-11-01 てじゃ「炎だけタッチが違う」→淡い色、先へ赤く、中の色のふちをぼかす）
        m.SetColor("_Orange", new Color32(0xFF, 0xA1, 0x4D, 0xFF)); m.SetColor("_OrangeTop", new Color32(0xF2, 0x60, 0x3A, 0xFF)); m.SetColor("_Yellow", new Color32(0xFF, 0xE2, 0x7A, 0xFF)); m.SetFloat("_CoreSoft", 1f); EditorUtility.SetDirty(m);
        return m;
    }
    // FlameMesh の形を組み立てる。uv.x：0 外、1 黄色、2 飛ぶ玉。色 r：根元 0〜先 1。飛ぶ玉は頂点＝出る場所、法線＝玉の形
    class FlameMeshB
    {
        public List<Vector3> v = new List<Vector3>(), nor = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<Color> col = new List<Color>(); public List<int> tr = new List<int>();
        public void Grid(int rows, int cols) { int b = v.Count - (rows + 1) * (cols + 1); for (int j = 0; j < rows; j++) for (int k = 0; k < cols; k++) { int q = b + j * (cols + 1) + k, u = q + cols + 1; tr.AddRange(new[] { q, u + 1, u, q, q + 1, u + 1 }); } }
        public void Add(Vector3 p, Vector3 n, float layer, float seed, float k) { v.Add(p); nor.Add(n); uv.Add(new Vector2(layer, seed)); col.Add(new Color(k, 0f, 0f)); }
        public void Egg(float layer, Vector3 c, float cy, float r, float up, float down, int rows, int seg, float y0, float h, float zScale = 1f, float nz = 1f, float seed = 0f, float taper = 1.7f)
        {
            for (int j = 0; j <= rows; j++)
            {
                float th = Mathf.Lerp(-90f, 90f, j / (float)rows) * Mathf.Deg2Rad, s = Mathf.Sin(th), co = Mathf.Cos(th);
                float y = cy + r * s * (s > 0f ? up : down), rad = r * (s > 0f ? Mathf.Pow(Mathf.Max(0f, co), taper) : co);
                for (int k = 0; k <= seg; k++)
                {
                    float a = k * Mathf.PI * 2f / seg; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var nn = (d * co + Vector3.up * s).normalized; nn.z *= nz; Add(c + new Vector3(d.x * rad, y, d.z * rad * zScale), nn, layer, seed, Mathf.Clamp01((y - y0) / h));
                }
            }
            Grid(rows, seg);
        }
        public void Blob(Vector3 anchor, float seed, float k)
        {
            const int Rows = 5, Seg = 6;
            for (int j = 0; j <= Rows; j++)
            {
                float th = Mathf.Lerp(-90f, 90f, j / (float)Rows) * Mathf.Deg2Rad;
                for (int q = 0; q <= Seg; q++) { float a = q * Mathf.PI * 2f / Seg; Add(anchor, new Vector3(Mathf.Cos(a) * Mathf.Cos(th), Mathf.Sin(th), Mathf.Sin(a) * Mathf.Cos(th)), 2f, seed, k); }
            }
            Grid(Rows, Seg);
        }
        // 頂点はシェーダーで動くので、そのぶん大きさの箱を広げる
        public Mesh Build(string name, float pad)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(nor); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tr, 0); m.RecalculateBounds();
            var s = SaveMesh(m); var b = s.bounds; b.Expand(pad * 2f); s.bounds = b; EditorUtility.SetDirty(s);
            return s;
        }
    }
    // タイルする立体ノイズ（Perlin 2 段）。炎のかたまりの元。1 回作ったら使い回す
    public static Texture3D FlameNoise3D()
    {
        const string p = "Assets/FuwaStage2/Textures/Stage2_FlameNoise3D.asset"; const int N = 64;
        var tex = AssetDatabase.LoadAssetAtPath<Texture3D>(p);
        if (tex) return tex;
        var rnd = new System.Random(20261101); const int G = 8;
        var grad = new Vector3[G * G * G];
        for (int i = 0; i < grad.Length; i++) grad[i] = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
        float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        float Perlin(Vector3 q, int f)   // f は 1 タイルのマス数（G の約数）
        {
            int s = G / f; int x = Mathf.FloorToInt(q.x), y = Mathf.FloorToInt(q.y), z = Mathf.FloorToInt(q.z); float dx = q.x - x, dy = q.y - y, dz = q.z - z;
            float Dot(int a, int b, int c) { var g = grad[((x + a) % f * s) + ((y + b) % f * s) * G + ((z + c) % f * s) * G * G + 0]; return g.x * (dx - a) + g.y * (dy - b) + g.z * (dz - c); }
            float u = Fade(dx), w = Fade(dy), r = Fade(dz);
            return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Dot(0, 0, 0), Dot(1, 0, 0), u), Mathf.Lerp(Dot(0, 1, 0), Dot(1, 1, 0), u), w),
                              Mathf.Lerp(Mathf.Lerp(Dot(0, 0, 1), Dot(1, 0, 1), u), Mathf.Lerp(Dot(0, 1, 1), Dot(1, 1, 1), u), w), r);
        }
        var d = new float[N * N * N]; float lo = 1e9f, hi = -1e9f;
        for (int z = 0; z < N; z++) for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            var q = new Vector3(x, y, z) / N; float n = Perlin(q * 4f, 4) + 0.45f * Perlin(q * 8f, 8);
            d[x + y * N + z * N * N] = n; lo = Mathf.Min(lo, n); hi = Mathf.Max(hi, n);
        }
        var col = new byte[d.Length]; for (int i = 0; i < d.Length; i++) col[i] = (byte)Mathf.RoundToInt(255f * (d[i] - lo) / (hi - lo));
        tex = new Texture3D(N, N, N, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Stage2_FlameNoise3D" };
        tex.SetPixelData(col, 0); tex.Apply(false, false);
        AssetDatabase.CreateAsset(tex, p);
        return tex;
    }
    // 炎を「形がゆれる」から「模様が上へ流れて削る」に（2026-11-01 てじゃ「揺れるだけじゃ違和感」）
    static void FlameFlow(Material m, float fx, float fy, float speed, float hot, float mid)
    {
        m.SetTexture("_FlowTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Stage2_LavaFlow.png"));
        m.SetFloat("_Flow", 1f); m.SetFloat("_FlowX", fx); m.SetFloat("_FlowY", fy); m.SetFloat("_FlowSpeed", speed); m.SetFloat("_BandHot", hot); m.SetFloat("_BandMid", mid); EditorUtility.SetDirty(m);
    }
    public static Material SeedFlameMat()
    {
        var m = FlameMeshMat("Stage2_2_SeedFlame", 0.75f, 0.75f, 0.1f, 0.42f, 0.75f, 0.05f, 0.58f, 1f, 1f);
        m.renderQueue = 2012; EditorUtility.SetDirty(m);   // 穴の中に見えるように縦穴のあと
        return m;
    }
    // 穴の中に描く物は縦穴（Geometry+11）のあとに描く
    static Material SeedPitToon(string name, Color c) { var m = FuwaStage2WaterTest.ToonMat(name, c); m.renderQueue = 2012; EditorUtility.SetDirty(m); return m; }
    static Material SeedPitMat()
    {
        const string mp = "Assets/FuwaStage2/Materials/Stage2_2_FirePit.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!mat) { mat = new Material(Shader.Find("FuwaCourse/VentShaft")); AssetDatabase.CreateAsset(mat, mp); }
        mat.SetColor("_Top", new Color(0.33f, 0.2f, 0.19f)); mat.SetColor("_Bottom", new Color(0.42f, 0.09f, 0.03f)); mat.SetFloat("_Falloff", 1.1f);   // 口はふちと同じ色、底は火に照らされて赤く EditorUtility.SetDirty(mat);
        return mat;
    }
    // 噴気孔：打ち出す向きに傾いた筒を道に埋めた、斜めの穴（2026-10-31 てじゃ「真っ直ぐなのに斜めに出るのへん、すこし埋めて斜めに吹き出す穴に」）。
    //  筒を丸ごと出すと道をふさいだので、道の上は後ろが少し高い低いふちだけ。奥は火種の穴と同じステンシルで見せる。当たりは道のまま。戻り値は湯気の出る所（g の座標）
    static Vector3 VentLook(Transform g, Vector3 launch)
    {
        Physics.SyncTransforms();
        var c = new GameObject("Crater", typeof(MeshFilter), typeof(MeshRenderer)).transform; c.SetParent(g, false);
        c.rotation = Physics.Raycast(g.position + Vector3.up, Vector3.down, out var gh, 2f, 1 << 11, QueryTriggerInteraction.Ignore) ? Quaternion.LookRotation(Vector3.ProjectOnPlane(g.forward, gh.normal), gh.normal) : g.rotation;
        var cr = c.GetComponent<MeshRenderer>(); cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cr.receiveShadows = false;
        var L = c.InverseTransformDirection(g.TransformDirection(launch)).normalized; var X = Vector3.Cross(Vector3.up, L).normalized; var U = Vector3.Cross(L, X).normalized; if (U.y < 0f) U = -U;
        const float Rin = 0.28f, Rout = 0.36f, BrowH = 0.08f, LipY = 0.02f; const int N = 28; float kU = L.y * 1.08f;   // 筒の断面を上下につぶして、道の面での穴を丸く（まん丸の筒だと斜めに切れて縦長でとがって見えた。2026-10-31 てじゃ）
        var A = new Vector3(0f, 0f, 0.15f);   // 筒の軸が道の面を通る所
        float Warp(int k) { float a = k * Mathf.PI * 2f / N; return 1f + 0.08f * Mathf.Sin(2f * a + 0.9f) + 0.06f * Mathf.Sin(3f * a + 2.1f) + 0.025f * Mathf.Sin(5f * a + 0.4f); }   // 穴の形のゆがみ（2026-10-31 てじゃ「ちょっと形をゆがませて」）
        Vector3 Pt(float sv, int k, float rr) { float a = k * Mathf.PI * 2f / N; return A + L * sv + (U * Mathf.Cos(a) * kU + X * Mathf.Sin(a)) * rr * Warp(k); }
        float SGround(int k, float rr) => -(rr * Warp(k) * kU * Mathf.Cos(k * Mathf.PI * 2f / N) * U.y) / L.y;
        // ふちを切る面：内側の穴の前のはしで LipY、後ろのはしで BrowH。前が低く後ろが高い
        float zB = Pt(SGround(0, Rin), 0, Rin).z, zF = Pt(SGround(N / 2, Rin), N / 2, Rin).z;
        float PlaneGap(Vector3 q) => q.y - Mathf.Lerp(LipY, BrowH, (zF - q.z) / (zF - zB));
        float SCut(int k, float rr) { float g0 = PlaneGap(Pt(0f, k, rr)), g1 = PlaneGap(Pt(1f, k, rr)); return -g0 / (g1 - g0); }
        // 筒の線が実際の道の面と交わる所。道は平面でないので、平面で計算すると斜めの筒では数 cm ずれて口と壁のあいだに道がのぞいた
        float SRoad(int k, float rr)
        {
            float sv = SGround(k, rr);
            for (int it = 0; it < 4; it++)
            {
                var q = Pt(sv, k, rr);
                if (!Physics.Raycast(c.TransformPoint(q) + Vector3.up, Vector3.down, out var rh, 2f, 1 << 11, QueryTriggerInteraction.Ignore)) break;
                sv += (c.InverseTransformPoint(rh.point).y - q.y) / L.y;
            }
            return sv;
        }
        float Rock(int k) { float a = k * Mathf.PI * 2f / N; return 1f + 0.06f * Mathf.Sin(2f * a + 0.7f) + 0.035f * Mathf.Sin(3f * a + 1.2f); }
        // 内側のふち：道の折れ目で道より下にならないように（口とのあいだに道がのぞいた）
        Vector3 InRim(int k)
        {
            var q = Pt(SCut(k, Rin), k, Rin);
            if (Physics.Raycast(c.TransformPoint(q) + Vector3.up, Vector3.down, out var rh, 2f, 1 << 11, QueryTriggerInteraction.Ignore)) q.y = Mathf.Max(q.y, c.InverseTransformPoint(rh.point).y + 0.03f);
            return q;
        }
        var mb = new MB();
        for (int k = 0; k < N; k++)
        {
            var ra = Pt(0f, k, 1f) + Pt(0f, k + 1, 1f) - A * 2f;
            float Ro(int n) => Rout * Rock(n);
            mb.Quad(Pt(SCut(k, Ro(k)), k, Ro(k)), Pt(SCut(k + 1, Ro(k + 1)), k + 1, Ro(k + 1)), Pt(SCut(k + 1, Ro(k + 1)) - 1.2f, k + 1, Ro(k + 1)), Pt(SCut(k, Ro(k)) - 1.2f, k, Ro(k)), ra, 0);   // 外側（ほとんど道の下）
            mb.Quad(InRim(k), InRim(k + 1), Pt(SCut(k + 1, Ro(k + 1)), k + 1, Ro(k + 1)), Pt(SCut(k, Ro(k)), k, Ro(k)), Vector3.up, 0);   // ふちの上面
            mb.Quad(InRim(k), InRim(k + 1), Pt(SRoad(k + 1, Rin) - 0.15f, k + 1, Rin), Pt(SRoad(k, Rin) - 0.15f, k, Rin), -ra, 1);   // 内側（道の上の分）
        }
        // 土：ふちのまわりの道の上に広げる。横へ広く、前後は短く。道の面に沿わせて、はしは道にもぐらせる
        float RoadY(Vector3 q) => Physics.Raycast(c.TransformPoint(new Vector3(q.x, 0f, q.z)) + Vector3.up, Vector3.down, out var sh, 2f, 1 << 11, QueryTriggerInteraction.Ignore) ? c.InverseTransformPoint(sh.point).y : 0f;
        Vector3 SoilPt(int k, float t, float yUp)
        {
            float a = k * Mathf.PI * 2f / N, ro = Rout * Rock(k); var q = Pt(SRoad(k, ro), k, ro); var d = new Vector3(q.x - A.x, 0f, q.z - A.z).normalized;
            float w = 0.24f * (1f + 0.2f * Mathf.Sin(2f * a + 1f) + 0.12f * Mathf.Sin(3f * a + 0.3f));
            if (Mathf.Abs(d.x) > 0.01f) w = Mathf.Min(w, Mathf.Max(0f, NarrowHW - 0.1f - Mathf.Abs(q.x)) / Mathf.Abs(d.x));
            q += d * w * t; q.y = RoadY(q) + yUp; return q;
        }
        // 内のはしは穴のふちまで。筒の上の面は低くて折れやすく、土を筒の外側で止めるとあいだに道がのぞいた
        Vector3 SoilIn(int k) { var q = Pt(SRoad(k, Rin), k, Rin); q.y = RoadY(q) + 0.03f; return q; }
        for (int k = 0; k < N; k++)
        {
            mb.Quad(SoilIn(k), SoilIn(k + 1), SoilPt(k + 1, 0f, 0.03f), SoilPt(k, 0f, 0.03f), Vector3.up, 0);
            mb.Quad(SoilPt(k, 0f, 0.03f), SoilPt(k + 1, 0f, 0.03f), SoilPt(k + 1, 0.55f, 0.022f), SoilPt(k, 0.55f, 0.022f), Vector3.up, 0);
            mb.Quad(SoilPt(k, 0.55f, 0.022f), SoilPt(k + 1, 0.55f, 0.022f), SoilPt(k + 1, 1f, -0.012f), SoilPt(k, 1f, -0.012f), Vector3.up, 0);
        }
        c.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mb.Build("Course2_2_VentCrater", 2));
        const string innerPath = "Assets/FuwaStage2/Materials/Stage2_2_VentInner.mat";
        var inner = AssetDatabase.LoadAssetAtPath<Material>(innerPath);
        if (inner && inner.shader != Shader.Find("Unlit/Color")) inner.shader = Shader.Find("Unlit/Color");   // Toon だと暗い色でも光って灰色に見えた
        if (!inner) { inner = new Material(Shader.Find("Unlit/Color")); AssetDatabase.CreateAsset(inner, innerPath); }
        inner.SetColor("_Color", new Color(0.33f, 0.2f, 0.19f));   // ステンシルの奥の口の色とそろえる
        EditorUtility.SetDirty(inner);
        cr.sharedMaterials = new[] { rockMat, inner };
        // 道の面の口：筒と道の交わり。少し大きくして、内側の壁とのあいだに道を残さない（外へはみ出した分はふちと壁に隠れる）
        const float MaskR = Rin * 1.02f;
        var mv = new List<Vector3> { Vector3.zero }; var mt = new List<int>(); float top = float.NegativeInfinity, cz = 0f;
        for (int k = 0; k < N; k++)
        {
            var q = Pt(SRoad(k, MaskR), k, MaskR); q.y = 0f;
            var qr = Pt(SCut(k, MaskR), k, MaskR); if (qr.z > q.z && Mathf.Cos(k * Mathf.PI * 2f / N) < 0f) q.z = qr.z;   // 前側の壁は道の上で前へ傾くので、ふちの真下まで口を広げる（壁の裏は見えず、すき間に道がのぞいた）
            cz += q.z / N;
            if (Physics.Raycast(c.TransformPoint(q) + Vector3.up, Vector3.down, out var hit, 2f, 1 << 11, QueryTriggerInteraction.Ignore)) q.y = c.InverseTransformPoint(hit.point).y;
            q.y += 0.012f; top = Mathf.Max(top, q.y); mv.Add(q);
        }
        mv[0] = new Vector3(0f, top, cz);
        bool flip = Vector3.Cross(mv[2] - mv[0], mv[1] - mv[0]).y < 0f;   // 上から見て表になる向きに
        for (int k = 0; k < N; k++) { int k0 = k + 1, k1 = (k + 1) % N + 1; mt.AddRange(flip ? new[] { 0, k0, k1 } : new[] { 0, k1, k0 }); }
        var mouth = new Mesh { name = "Course2_2_VentMouth" }; mouth.SetVertices(mv); mouth.SetTriangles(mt, 0); mouth.RecalculateNormals(); mouth.RecalculateBounds();
        // 道の下へ続く筒の内側とつきあたり
        const float SDeep = -1.4f; const int SR = 6;
        var sv = new List<Vector3>(); var suv = new List<Vector2>(); var st = new List<int>();
        for (int k = 0; k <= N; k++) for (int j = 0; j <= SR; j++) { float t = j / (float)SR; sv.Add(Pt(Mathf.Lerp(SRoad(k, Rin) + 0.05f, SDeep, t), k, Rin)); suv.Add(new Vector2(0f, t)); }
        bool sf = Vector3.Dot(Vector3.Cross(sv[SR + 1] - sv[0], sv[1] - sv[0]), sv[0] - (A + L * Vector3.Dot(sv[0] - A, L))) < 0f;   // 内向きが表
        for (int k = 0; k < N; k++) for (int j = 0; j < SR; j++) { int a0 = k * (SR + 1) + j, a1 = a0 + SR + 1; st.AddRange(sf ? new[] { a0, a1, a1 + 1, a0, a1 + 1, a0 + 1 } : new[] { a0, a0 + 1, a1 + 1, a0, a1 + 1, a1 }); }
        int cc = sv.Count; sv.Add(A + L * SDeep); suv.Add(new Vector2(0f, 1f));
        bool cf = Vector3.Dot(Vector3.Cross(sv[2 * SR + 1] - sv[cc], sv[SR] - sv[cc]), L) < 0f;   // 口の向きが表
        for (int k = 0; k < N; k++) { int e0 = k * (SR + 1) + SR, e1 = e0 + SR + 1; st.AddRange(cf ? new[] { cc, e0, e1 } : new[] { cc, e1, e0 }); }
        var shaft = new Mesh { name = "Course2_2_VentPit" }; shaft.SetVertices(sv); shaft.SetUVs(0, suv); shaft.SetTriangles(st, 0); shaft.RecalculateNormals(); shaft.RecalculateBounds();
        const string pitPath = "Assets/FuwaStage2/Materials/Stage2_2_VentPit.mat";
        var pit = AssetDatabase.LoadAssetAtPath<Material>(pitPath);
        if (!pit) { pit = new Material(Shader.Find("FuwaCourse/VentShaft")); AssetDatabase.CreateAsset(pit, pitPath); }
        pit.SetColor("_Top", new Color(0.33f, 0.2f, 0.19f)); pit.SetColor("_Bottom", new Color(0.015f, 0.008f, 0.008f)); pit.SetFloat("_Falloff", 1.5f);   // 口はふちと同じ色、奥へ暗く pit.SetFloat("_Facet", 0f); EditorUtility.SetDirty(pit);
        void Part(string nm, Mesh me, Material ma)
        {
            var o = new GameObject(nm, typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(c, false); o.GetComponent<MeshFilter>().sharedMesh = me;
            var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = ma; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        Part("Mouth", SaveMesh(mouth), AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/VentHoleMask.mat"));
        Part("Pit", SaveMesh(shaft), pit);
        return g.InverseTransformPoint(c.TransformPoint(A));
    }

    // 追い風（ステージ1の G3_CrossRight の風の箱を写す）：c を中心に、d の向き（坂にそって上向き）へ、長さ len・幅 w
    static void TailWind(Transform parent, string name, Vector3 c, Vector3 d, float len, float w)
    {
        var tmpl = Object.FindObjectsOfType<FuwaWindArea>(true).FirstOrDefault(x => x.transform.parent != null && x.transform.parent.name == "G3_CrossRight");
        if (!tmpl) { Debug.LogWarning("[2–2] 風のお手本（G3_CrossRight）がない"); return; }
        var zone = Object.Instantiate(tmpl.gameObject, parent, false); zone.name = name;
        float h = 2.5f;
        zone.transform.localRotation = Quaternion.LookRotation(d);
        zone.transform.localPosition = c + zone.transform.localRotation * Vector3.up * (h * 0.5f - 0.3f);
        zone.transform.localScale = new Vector3(w, h, len);
        var so = new SerializedObject(zone.GetComponent<FuwaWindArea>());
        so.FindProperty("localDirection").vector3Value = Vector3.forward;
        so.FindProperty("strength").floatValue = TailWindStrength;
        so.FindProperty("onSeconds").floatValue = 1f; so.FindProperty("offSeconds").floatValue = 0f;
        so.FindProperty("baseLevel").floatValue = 1f; so.FindProperty("warnSeconds").floatValue = 0f;
        so.FindProperty("gustCurve").animationCurveValue = AnimationCurve.Constant(0f, 1f, 1f);
        // 地面の＞＞＞の模様はなし（噴気孔の湯気で見せる）
        var decal = zone.transform.Find("WindDecal");
        if (decal) Object.DestroyImmediate(decal.gameObject);
        so.FindProperty("groundHighlight").objectReferenceValue = null;
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(zone.GetComponent<FuwaWindArea>());
    }

    // 噴気孔：道に半分埋まった斜めの岩の筒＋斜め上へ吹き出す湯気。ふわふわが入ると target（火種の上）へ斜めに打ち出す。
    // 飛び方（重力 1.5・空気抵抗：打ち出しから 1 秒は 0.3、そのあと 1.2）を計算して、8.4m 先・1.6m 上の火種の熱の範囲の 0.8〜1.4m の高さを通る速さ（水平 6・上 3.5）。距離に合わせて水平の速さを伸ばす
    // 効果音の AudioSource（2026-11-01 てじゃ「SEをふやそう」。音は Tools/audio/make_fire_sfx.py）。near まではそのままの音量、far で聞こえなくなる
    static AudioSource Sfx(Transform parent, string name, string clip, Vector3 lp, float near, float far, float volume, bool loop = false, float gain = 6f)
    {
        var o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.localPosition = lp;
        var a = o.AddComponent<AudioSource>();
        a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/" + clip + ".wav");
        a.loop = loop; a.playOnAwake = loop; a.spatialBlend = 1f; a.dopplerLevel = 0f; a.volume = volume; a.minDistance = near; a.maxDistance = far; a.rolloffMode = AudioRolloffMode.Linear;
        var sp = o.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
        sp.Gain = gain; sp.Near = near; sp.Far = far; sp.EnableSpatialization = true; sp.UseAudioSourceVolumeCurve = false;
        return a;
    }
    // 吹き出しの見える所ぜんぶを当たりに（2026-11-03 てじゃ「風があるエリアにふわふわが当たれば飛ぶように」「どこであたっても最終的に焚き火あたりに」）。
    // 当たりは打ち出す向きへ伸びる四角すい台（道の外まで）。蒸気の見た目は広げない（てじゃ「エフェクトはまえのままでいいかも」）。落ちる先は FuwaLaunchPad.target で計算する。何回呼んでも同じ結果
    public static float VentZoneLen = 5.5f, VentZoneBackZ = -0.8f, VentZoneBaseY = 0.6f, VentZoneBaseHW = 1.3f, VentZoneBaseHT = 0.8f, VentZoneTopHW = 2.6f, VentZoneTopHT = 2f;
    public static void VentBlastZone(FuwaLaunchPad lp, Vector3 targetWorld)
    {
        var z = lp.transform; var g = z.parent; var launch = lp.localDirection;
        var a = launch.normalized; var n = Vector3.Cross(a, Vector3.right).normalized; var side = Vector3.right;
        var b0 = new Vector3(0f, VentZoneBaseY, VentZoneBackZ) - z.localPosition; var b1 = b0 + a * VentZoneLen;
        var verts = new[] { b0 - side * VentZoneBaseHW - n * VentZoneBaseHT, b0 + side * VentZoneBaseHW - n * VentZoneBaseHT, b0 + side * VentZoneBaseHW + n * VentZoneBaseHT, b0 - side * VentZoneBaseHW + n * VentZoneBaseHT,
                            b1 - side * VentZoneTopHW - n * VentZoneTopHT, b1 + side * VentZoneTopHW - n * VentZoneTopHT, b1 + side * VentZoneTopHW + n * VentZoneTopHT, b1 - side * VentZoneTopHW + n * VentZoneTopHT };
        var m = new Mesh { name = "Course2_2_VentBlastZone" }; m.vertices = verts;
        m.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 };
        m = SaveMesh(m);
        foreach (var old in z.GetComponents<Collider>()) Object.DestroyImmediate(old);
        var mc = z.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.convex = true; mc.isTrigger = true;
        var tgt = g.Find("LandAt"); if (tgt == null) { tgt = new GameObject("LandAt").transform; tgt.SetParent(g, false); }
        tgt.position = targetWorld; lp.target = tgt;
        EditorUtility.SetDirty(z.gameObject);
    }
    static void BlastVent(Transform parent, string name, Vector3 at, Vector3 target)
    {
        var d = Flat(target - at); float dist = d.magnitude; d /= Mathf.Max(0.01f, dist);
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = at; g.localRotation = Quaternion.LookRotation(d);
        Vector3 launch = new Vector3(0, VentUp, VentFwd * dist / 8.4f);
        var ventMouth = VentLook(g, launch);
        // 湯気（打ち出す向きへ）
        var fo = new GameObject("Steam"); fo.transform.SetParent(g, false); fo.transform.localPosition = ventMouth;
        fo.transform.localRotation = Quaternion.LookRotation(launch.normalized);
        var ps = fo.AddComponent<ParticleSystem>();
        var main = ps.main; main.startLifetime = 0.9f; main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 14f); main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);   // 吹っ飛ぶ威力に見えるように激しく（2026-10-31 てじゃ） main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.98f, 0.94f, 0.9f), new Color(1f, 0.88f, 0.76f, 0.8f));
        main.maxParticles = 90; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.05f;
        var em = ps.emission; em.rateOverTime = 70f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 10, 0, 0.9f) });   // ときどき ぶわっ
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 7f; sh.radius = 0.18f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 3.5f; lim.dampen = 0.08f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.7f), new Keyframe(1, 1.9f)));
        var ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 4; ts.numTilesY = 4; ts.animation = ParticleSystemAnimationType.WholeSheet; ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f); ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 15f / 16f);
        fo.GetComponent<ParticleSystemRenderer>().sharedMaterial = SteamMat();   // ぼやけた丸→もくもく
        ps.Play();
        // 芯の勢い：細く伸びた速い筋
        var jo = new GameObject("SteamJet"); jo.transform.SetParent(g, false); jo.transform.localPosition = ventMouth; jo.transform.localRotation = fo.transform.localRotation;
        var js = jo.AddComponent<ParticleSystem>();
        var jm = js.main; jm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f); jm.startSpeed = new ParticleSystem.MinMaxCurve(12f, 18f); jm.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f); jm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        jm.startColor = new Color(1f, 0.97f, 0.92f, 0.85f); jm.maxParticles = 45; jm.simulationSpace = ParticleSystemSimulationSpace.World;
        var je = js.emission; je.rateOverTime = 90f;
        var jh = js.shape; jh.shapeType = ParticleSystemShapeType.Cone; jh.angle = 4f; jh.radius = 0.14f;
        var jc = js.colorOverLifetime; jc.enabled = true; var jg = new Gradient(); jg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }); jc.color = jg;
        var jt = js.textureSheetAnimation; jt.enabled = true; jt.numTilesX = 4; jt.numTilesY = 4; jt.animation = ParticleSystemAnimationType.WholeSheet; jt.frameOverTime = new ParticleSystem.MinMaxCurve(0f); jt.startFrame = new ParticleSystem.MinMaxCurve(0f, 15f / 16f);
        var jr = jo.GetComponent<ParticleSystemRenderer>(); jr.sharedMaterial = SteamMat(); jr.renderMode = ParticleSystemRenderMode.Stretch; jr.lengthScale = 9f; jr.velocityScale = 0f;
        js.Play();
        // 打ち出し
        var z = new GameObject("Blast"); z.transform.SetParent(g, false); z.transform.localPosition = new Vector3(0, 0.7f, 0); z.layer = 4;
                var lp = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaLaunchPad>(z);
        lp.launchSound = Sfx(g, "VentSfx", "sfx_vent_puff", Vector3.zero, 3f, 25f, 0.9f);
        lp.localDirection = launch; lp.speed = launch.magnitude; lp.cooldown = 1f; lp.flightSeconds = 1f; lp.flightDrag = 0.3f;
        VentBlastZone(lp, parent.TransformPoint(target));
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(lp); FuwaStage2WaterTest.NoSync(lp);
    }

    // 溶岩：ふわふわが入ると黒焦げ→アウト（いつも吹いている火柱あつかい＝FuwaFireJet の deadly、炎の粒なし）
    static void LavaBurn(Transform root, string name, Vector3 lp, Vector3 size, Quaternion? rot = null)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.transform.localPosition = lp; g.layer = 4;
        if (rot.HasValue) g.transform.localRotation = rot.Value;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        var j = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaFireJet>(g); j.heat = heat; j.deadly = true; j.period = 10f; j.onTime = 11f; j.warnTime = 0f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j); FuwaStage2WaterTest.NoSync(j);
    }

    // 炎の壁：a→b（下のはしの高さ）。ふわふわが入ると即黒焦げ（いつも吹いている火柱あつかい）。人は素通り
    static float SegDist(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    public static float FireStripGap = 0.75f;   // 炎の列から道のふちまで
    public struct WallPt { public Vector3 p; public float low, floor, top; }
    // 線 pts（x の小さい順）の x の所。前後の平均で角を丸める
    static WallPt WallAt(List<WallPt> pts, float x)
    {
        WallPt At(float q)
        {
            q = Mathf.Clamp(q, pts[0].p.x, pts[pts.Count - 1].p.x); int i = 0; while (i + 2 < pts.Count && q > pts[i + 1].p.x) i++;
            float t = Mathf.InverseLerp(pts[i].p.x, pts[i + 1].p.x, q);
            return new WallPt { p = Vector3.Lerp(pts[i].p, pts[i + 1].p, t), low = Mathf.Lerp(pts[i].low, pts[i + 1].low, t), floor = Mathf.Lerp(pts[i].floor, pts[i + 1].floor, t), top = Mathf.Lerp(pts[i].top, pts[i + 1].top, t) };
        }
        WallPt a = At(x - 0.7f), b = At(x), c = At(x + 0.7f);
        return new WallPt { p = new Vector3(x, 0f, (a.p.z + b.p.z + c.p.z) / 3f), low = b.low, floor = (a.floor + b.floor + c.floor) / 3f, top = (a.top + b.top + c.top) / 3f };
    }
    // legA は根元へ向かう足（a が外）、legB は根元から出る足（b が外）。ふわふわが入ると即黒焦げ（いつも吹いている火柱あつかい）。人は素通り
    static void FireWall(Transform parent, string name, (Vector3 a, Vector3 b) legA, (Vector3 a, Vector3 b) legB, Vector3 mid, Vector3 n, Vector3 tail)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = mid; g.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, n));   // +x が口の向き、-x が V の根元
        Vector3 fa = Flat(legA.a), na = Flat(legA.b), nb = Flat(legB.a), fb = Flat(legB.b), da = (fa - na).normalized, db = (fb - nb).normalized;
        Vector3 pa = Vector3.Cross(Vector3.up, da), pb = Vector3.Cross(Vector3.up, db); if (Vector3.Dot(pa, fb - fa) < 0f) pa = -pa; if (Vector3.Dot(pb, fa - fb) < 0f) pb = -pb;
        float off = ZigHW + FireStripGap, clear = ZigBulge + ZigLedge + 0.5f;
        Vector3 la = na + pa * off, lb = nb + pb * off;
        float den = da.x * db.z - da.z * db.x, ta = ((lb.x - la.x) * db.z - (lb.z - la.z) * db.x) / den;   // 2 列がぶつかる所＝根元
        Vector3 root = la + da * ta, ea = fa - da * clear + pa * off, eb = fb - db * clear + pb * off;
        float RoadA(Vector3 q) => Mathf.Lerp(legA.b.y, legA.a.y, Mathf.Clamp01(Vector3.Dot(q - na, da) / Vector3.Distance(fa, na)));
        float RoadB(Vector3 q) => Mathf.Lerp(legB.a.y, legB.b.y, Mathf.Clamp01(Vector3.Dot(q - nb, db) / Vector3.Distance(fb, nb)));
        var fork = Flat(mid) + n * (Vector3.Dot((ea + eb) * 0.5f - Flat(mid), n) - 1.8f);
        var inv = Quaternion.Inverse(g.localRotation); var o = Flat(mid);
        WallPt OnRoad(Vector3 q, float r) => new WallPt { p = inv * (q - o), low = r - 1f - mid.y, floor = r - 1.8f - mid.y, top = r + 3.2f - mid.y };
        // 口は 1 個とばしの踊り場の両方を守る高さ
        WallPt OnStem(Vector3 q) => new WallPt { p = inv * (q - o), low = -1f - ZigRise, floor = -3.8f, top = FireWallH - 1f };
        var ptsA = new List<WallPt> { OnRoad(root, RoadA(root)), OnRoad(ea, RoadA(ea)), OnStem(fork), OnStem(Flat(tail)) };
        var ptsB = new List<WallPt> { OnRoad(root, RoadB(root)), OnRoad(eb, RoadB(eb)), OnStem(fork) };
        int bi = 0;
        foreach (var pts in new[] { ptsA, ptsB })
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                WallPt u = pts[i], w = pts[i + 1]; var pu = new Vector3(u.p.x, (u.low + u.top) * 0.5f, u.p.z); var pw = new Vector3(w.p.x, (w.low + w.top) * 0.5f, w.p.z); var dir = (pw - pu).normalized; var fw = Vector3.Cross(dir, Vector3.up).normalized;
                LavaBurn(g, "Burn" + bi++, (pu + pw) * 0.5f, new Vector3((pw - pu).magnitude + 0.2f, Mathf.Max(u.top - u.low, w.top - w.low), 0.4f), Quaternion.LookRotation(fw, Vector3.Cross(fw, dir)));
            }
        FlameCurtain(g, name, ptsA, ptsB);
        FireWallRock(g, name, ptsA, ptsB);
        // 火の粉だけ粒で（前は太い粒のかたまりで炎を作っていて、ぼやけて赤いもやに見えた。2026-10-31）
        float x0 = ptsA[0].p.x, x1 = ptsA[3].p.x, len = x1 - x0;
        var ps = FuwaStage2FireTest.FlamePS(g, "Flame", 0.2f, 2.6f, 1.3f, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.28f, 0.05f));
        ps.transform.localPosition = new Vector3((x0 + x1) * 0.5f, -1.2f, 0);
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(len, 2f, 0.25f);
        var em = ps.emission; em.rateOverTime = 2.4f * len;
        var main = ps.main; main.startColor = Color.white; main.maxParticles = Mathf.CeilToInt(5f * len); main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
        ps.Play();
    }
    // 岩がはねる場所のヒビ（2026-11-02 てじゃ「もちょいシンプルな見た目」「メッシュで割りたい」）。太い割れ目が 3〜4 本だけ。
    //  道のメッシュは切らず、噴気孔と同じステンシルで見せる：Mouth（道の上の割れ目の形。VentHoleMask）＋ Shaft（その下の壁と底。VentShaft）。当たりは平らな道のまま、焼き直しもいらない。
    //  道のはしで切れる腕はそこで止める。LavaRock* の P1〜（最後は道の外なので除く）の真下に作る。作り直さずシーンだけ直す時もこれを呼ぶ
    public static float RockCrackDepth = 0.35f, RockCrackLift = 0.015f, RockBounceH = 0.74f;   // RockBounceH：はねる時の岩の中心の、道からの高さ（岩の下の半径 0.78。少しめりこむくらい）
    // ジグザグの岩（LavaRock0/1）とまもののエリアの岩（LavaRockArena）の通り道の点線（2026-11-04 てじゃ「この点線いいな」）。最初の道の上の板（P1）から落ちる先まで、FuwaLavaRock.At と同じ弧に RockDotGap おきに小さい玉。
    //  1 つのメッシュで、uv.x が通り道ぞいの距離。作り直さずシーンだけ直す時もこれを呼ぶ（はねる点を動かしたら呼び直す。RockCracks のあとに）
    public static float RockDotGap = 0.6f, RockDotR = 0.13f, RockDotSpeed = 15f, RockDotSpots = 3f, RockDotLead = 10f, RockDotFadeNear = 12f, RockDotFadeFar = 35f,   /* 遠くの点は薄く：12m から薄くなって 35m で見えない（2026-11-04 てじゃ「遠くのは見えづらく」） */ RockDotTail = 6f;   /* 2026-11-04 てじゃ「くどい、一箇所に、光ってる部分をすこしながく、岩のちょっと前を岩と同じくらいの速度で」→ 岩にくっつけて、岩の 4〜10m 先を光らせる（光の長さ 4→6。最初 0.5〜6.5m 先→「もうちょい手前」）。 2026-11-04 てじゃ「点滅速くして、消えてるときは見えないように」：10→18→26→15（光は同時に 3 か所）、ふだんの濃さ 0.8→0（光った所だけ見える） */
    public static string RockPathDots(Transform fire)
    {
        string mp = "Assets/FuwaStage2/Materials/Stage2_2_RockPathDots.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp); if (!mat) { mat = new Material(Shader.Find("FuwaCourse/RockPathDots")); AssetDatabase.CreateAsset(mat, mp); }
        mat.SetColor("_Color", new Color(1f, 0.62f, 0.05f, 1f)); mat.SetFloat("_Dim", 0f); mat.SetColor("_LitColor", new Color(1f, 0.95f, 0.25f, 1f)); mat.SetFloat("_FadeNear", RockDotFadeNear); mat.SetFloat("_FadeFar", RockDotFadeFar);
        mat.renderQueue = 3000; EditorUtility.SetDirty(mat);   // ふつうの半透明の順番で（炎の壁は不透明で深さを書くので、後に描けば壁の向こうの点は隠れる）。2005 にしたら、点のまわりで壁が抜けて向こうが見えた（2026-11-04 てじゃ「描画順がおかしい」）
        // 点の形：低い球（緯度 4・経度 8）
        var unitV = new List<Vector3>(); var unitT = new List<int>(); const int LA = 4, LO = 8;
        for (int a = 0; a <= LA; a++) for (int o = 0; o <= LO; o++) { float th = Mathf.PI * a / LA, ph = 2f * Mathf.PI * o / LO; unitV.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph))); }
        for (int a = 0; a < LA; a++) for (int o = 0; o < LO; o++) { int i0 = a * (LO + 1) + o, i1 = i0 + LO + 1; unitT.AddRange(new[] { i0, i0 + 1, i1, i0 + 1, i1 + 1, i1 }); }
        string res = "";
        foreach (var f in fire.GetComponentsInChildren<FuwaLavaRock>(true))
        {
            var g = f.transform.parent; if (!(g.name == "LavaRock0" || g.name == "LavaRock1" || g.name == "LavaRockArena")) continue;   // まもののエリアのも（2026-11-04 てじゃ「まもの側のにもつけて」）
            var old = g.Find("PathDots"); if (old) Object.DestroyImmediate(old.gameObject);
            // 弧をこまかく取って長さを測り、RockDotGap おきに点
            var pts = new List<Vector3>(); var ds = new List<float>(); float d = 0f; Vector3 prev = Vector3.zero; bool first = true;
            var hopTab = new List<float>();   // 光を岩にくっつける用：はねごとに u = 0〜1 を 8 等分した所の点線ぞいの距離
            //  火口（P0）から点を出す（2026-11-04 てじゃ「山から点だそ」。前は最初の板 P1 から）
            for (int k = 0; k < f.points.Length - 1; k++)
            {
                Vector3 a = f.points[k].position, b = f.points[k + 1].position; float top = Mathf.Max(a.y, b.y) + (k == 0 ? f.firstUp : f.hopUp);
                Vector3 m = (a + b) * 0.5f; m.y = 2f * top - (a.y + b.y) * 0.5f; hopTab.Add(d);
                for (int i = (k == 0 ? 0 : 1); i <= 200; i++) { float u = i / 200f, v = 1f - u; var p = v * v * a + 2f * v * u * m + u * u * b; if (!first) d += (p - prev).magnitude; first = false; prev = p; pts.Add(p); ds.Add(d); if (i > 0 && i % 25 == 0) hopTab.Add(d); }
            }
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); int dots = 0;
            for (float at = 0f; at <= d; at += RockDotGap)
            {
                int j = ds.BinarySearch(at); if (j < 0) j = ~j; j = Mathf.Clamp(j, 1, ds.Count - 1);
                float w = Mathf.InverseLerp(ds[j - 1], ds[j], at); var c = Vector3.Lerp(pts[j - 1], pts[j], w);
                int b0 = verts.Count; foreach (var uv0 in unitV) { verts.Add(g.InverseTransformPoint(c + uv0 * RockDotR)); uvs.Add(new Vector2(at, 0f)); }
                foreach (var ti in unitT) tris.Add(b0 + ti); dots++;
            }
            var mesh = new Mesh { name = "Course2_2_" + g.name + "_PathDots" }; mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var o = new GameObject("PathDots", typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(g, false); o.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
            var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.enabled = false;   // 岩が飛んでいる間だけ FuwaLavaRock が出す
            f.pathDots = r; f.pathLength = d; f.dotSpeed = RockDotSpeed; f.dotTail = RockDotTail; f.dotSpacing = 0f; f.dotFollow = true; f.dotLead = RockDotLead; f.dotHopTable = hopTab.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); EditorUtility.SetDirty(f);
            res += g.name + " 点 " + dots + " 長さ " + d.ToString("F1") + "m / ";
        }
        return res;
    }
    public static string RockCracks(Transform fire)
    {
        var old = fire.Find("RockCracks"); if (old) Object.DestroyImmediate(old.gameObject);
        var course = fire.root;
        var temps = new List<MeshCollider>();
        foreach (var n in new[] { "ZigPath", "Path12", "Path5" })
            foreach (var t in course.GetComponentsInChildren<Transform>(true))
                if (t.name == n && t.GetComponent<MeshFilter>()) { var c = t.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh; temps.Add(c); }
        Physics.SyncTransforms();
        bool OnRoad(Vector3 q, float from, out Vector3 hit)
        {
            hit = q; float best = float.MaxValue; bool ok = false;
            var ray = new Ray(new Vector3(q.x, from + 1.2f, q.z), Vector3.down);
            foreach (var c in temps) if (c.Raycast(ray, out var h, 4f) && h.distance < best && h.normal.y > 0.5f) { best = h.distance; hit = h.point + Vector3.up * RockCrackLift; ok = true; }
            return ok;
        }
        var mv = new List<Vector3>(); var mt = new List<int>(); var sv = new List<Vector3>(); var suv = new List<Vector2>(); var st = new List<int>();
        void Tri(List<Vector3> vs, List<int> ts, int a, int b, int c, Vector3 want) { if (Vector3.Dot(Vector3.Cross(vs[b] - vs[a], vs[c] - vs[a]), want) >= 0f) ts.AddRange(new[] { a, b, c }); else ts.AddRange(new[] { a, c, b }); }
        int count = 0; var rnd = new System.Random(2202); float R() => (float)rnd.NextDouble();
        // はねる点を今の道の高さにそろえる（板の線の計算だと道から 0.3〜1.5m ずれて、浮いて見えた。2026-11-02 てじゃ）
        foreach (var rock in fire.GetComponentsInChildren<FuwaLavaRock>(true))
            for (int pi = 1; pi + 1 < rock.points.Length; pi++)
                if (OnRoad(rock.points[pi].position, rock.points[pi].position.y, out var ground)) { var bp = rock.points[pi].position; bp.y = ground.y - RockCrackLift + RockBounceH; rock.points[pi].position = bp; }
        foreach (var rock in fire.GetComponentsInChildren<FuwaLavaRock>(true))
        {
            for (int pi = 1; pi + 1 < rock.points.Length; pi++)
            {
                var p = rock.points[pi].position; if (!OnRoad(p, p.y, out var center)) continue;
                // 3 パターンを順番に：0 長い 3 本、1 短い 4 本、2 長い 2 本＋短い枝 1 本（2026-11-02 てじゃ「長さとか角度とか変えて３パターンくらい」）
                int pat = count % 3, arms = pat == 1 ? 4 : 3; float a0 = R() * Mathf.PI * 2f; var loop = new List<Vector3>(); var hub = new List<Vector3>();
                for (int ai = 0; ai < arms; ai++)
                {
                    float ang = pat == 2 ? a0 + (ai == 0 ? 0f : ai == 1 ? 1.5f + R() * 0.5f : Mathf.PI + (R() - 0.5f) * 0.5f) : a0 + (ai + (R() - 0.5f) * 0.45f) / arms * Mathf.PI * 2f;
                    float len = pat == 0 ? Mathf.Lerp(1.0f, 1.35f, R()) : pat == 1 ? Mathf.Lerp(0.55f, 0.8f, R()) : (ai == 1 ? Mathf.Lerp(0.5f, 0.75f, R()) : Mathf.Lerp(1.25f, 1.5f, R()));
                    float hw0 = pat == 0 ? Mathf.Lerp(0.105f, 0.13f, R()) : pat == 1 ? Mathf.Lerp(0.075f, 0.095f, R()) : (ai == 1 ? 0.07f : Mathf.Lerp(0.095f, 0.115f, R()));
                    int segs = pat == 1 ? 3 : pat == 2 ? 5 : 4; float jag = pat == 2 ? 0.6f : 0.42f; var line = new List<Vector3>(); var q = p; float sign = R() < 0.5f ? 1f : -1f;
                    line.Add(p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.1f);
                    for (int s = 0; s < segs; s++) { sign = -sign; float g = ang + sign * Mathf.Lerp(0.18f, jag, R()); line.Add(line[line.Count - 1] + new Vector3(Mathf.Cos(g), 0f, Mathf.Sin(g)) * (len / segs)); }
                    var lefts = new List<Vector3>(); var rights = new List<Vector3>();
                    for (int s = 0; s < line.Count; s++)
                    {
                        var dir = (s + 1 < line.Count ? line[s + 1] - line[s] : line[s] - line[s - 1]).normalized; var side = new Vector3(-dir.z, 0f, dir.x);
                        float hw = hw0 * (1f - s / (float)(line.Count - 1));
                        if (!OnRoad(line[s] + side * Mathf.Max(hw, 0.02f), p.y, out var l) || !OnRoad(line[s] - side * Mathf.Max(hw, 0.02f), p.y, out var r)) break;
                        if (hw <= 0f) { OnRoad(line[s], p.y, out l); r = l; }
                        lefts.Add(l); rights.Add(r);
                    }
                    if (lefts.Count < 2) continue;
                    // 道のはしで止まった腕も先をとがらせる
                    int last = lefts.Count - 1; var tip = (lefts[last] + rights[last]) * 0.5f; lefts[last] = tip; rights[last] = tip;
                    for (int s = 0; s < last; s++)
                    {
                        int b = mv.Count; mv.Add(lefts[s]); mv.Add(rights[s]); mv.Add(rights[s + 1]); mv.Add(lefts[s + 1]);
                        Tri(mv, mt, b, b + 1, b + 2, Vector3.up); Tri(mv, mt, b, b + 2, b + 3, Vector3.up);
                        int f = sv.Count; var down = Vector3.down * RockCrackDepth; sv.Add(lefts[s] + down); sv.Add(rights[s] + down); sv.Add(rights[s + 1] + down); sv.Add(lefts[s + 1] + down);
                        for (int k = 0; k < 4; k++) suv.Add(new Vector2(0f, 1f));
                        Tri(sv, st, f, f + 1, f + 2, Vector3.up); Tri(sv, st, f, f + 2, f + 3, Vector3.up);
                    }
                    // 外周：右レールを先まで行って左レールで戻る。次の腕（角度が大きい方）の右レールへつなぐと交差しない
                    for (int s = 0; s <= last; s++) loop.Add(rights[s]);
                    for (int s = last - 1; s >= 0; s--) loop.Add(lefts[s]);
                    hub.Add(rights[0]); hub.Add(lefts[0]);
                }
                if (loop.Count < 3) continue;
                // まん中：腕の根元をつなぐ多角形
                int firstCenter = mv.Count; mv.Add(center);
                for (int i = 0; i < hub.Count; i++)
                {
                    int b = mv.Count; mv.Add(hub[i]); mv.Add(hub[(i + 1) % hub.Count]); Tri(mv, mt, firstCenter, b, b + 1, Vector3.up);
                    int f = sv.Count; var down = Vector3.down * RockCrackDepth; sv.Add(center + down); sv.Add(hub[i] + down); sv.Add(hub[(i + 1) % hub.Count] + down); for (int k = 0; k < 3; k++) suv.Add(new Vector2(0f, 1f)); Tri(sv, st, f, f + 1, f + 2, Vector3.up);
                }
                // 壁：腕の外周。内向きに面を向ける（口の中から見える）。となりの腕の根元どうしの間も閉じる
                void Wall(Vector3 u, Vector3 w, Vector3 inward)
                {
                    int b = sv.Count; var down = Vector3.down * RockCrackDepth; sv.Add(u); sv.Add(w); sv.Add(w + down); sv.Add(u + down);
                    suv.Add(new Vector2(0f, 0f)); suv.Add(new Vector2(0f, 0f)); suv.Add(new Vector2(0f, 1f)); suv.Add(new Vector2(0f, 1f));
                    Tri(sv, st, b, b + 1, b + 2, inward); Tri(sv, st, b, b + 2, b + 3, inward);
                }
                for (int i = 0; i < loop.Count; i++)
                {
                    var u = loop[i]; var w = loop[(i + 1) % loop.Count]; var mid = (u + w) * 0.5f; var e = w - u; e.y = 0f; if (e.sqrMagnitude < 0.000001f) continue;
                    var nrm = new Vector3(-e.z, 0f, e.x).normalized; var toC = center - mid; toC.y = 0f;
                    // 腕の中に向く側を選ぶ：外周の点列は左レール→先→右レールの順なので、向きは腕ごとに同じ（根元の間も同じ回り）
                    float area = 0f; for (int j = 0; j < loop.Count; j++) { var a = loop[j]; var c = loop[(j + 1) % loop.Count]; area += a.x * c.z - c.x * a.z; }
                    Wall(u, w, area > 0f ? nrm : -nrm);
                }
                count++;
            }
        }
        foreach (var c in temps) Object.DestroyImmediate(c);
        var loc = new System.Func<Vector3, Vector3>(fire.InverseTransformPoint);
        var mouth = new Mesh { name = "Course2_2_RockCrackMouth", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mouth.SetVertices(mv.Select(loc).ToList()); mouth.SetTriangles(mt, 0); mouth.RecalculateNormals(); mouth.RecalculateBounds();
        var shaft = new Mesh { name = "Course2_2_RockCrackShaft", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; shaft.SetVertices(sv.Select(loc).ToList()); shaft.SetUVs(0, suv); shaft.SetTriangles(st, 0); shaft.RecalculateNormals(); shaft.RecalculateBounds();
        var root = new GameObject("RockCracks").transform; root.SetParent(fire, false);
        void Part(string n, Mesh m, Material mat)
        {
            var o = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(root, false); o.GetComponent<MeshFilter>().sharedMesh = SaveMesh(m);
            var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        string shaftPath = "Assets/FuwaStage2/Materials/Stage2_2_RockCrackShaft.mat";
        var sm = AssetDatabase.LoadAssetAtPath<Material>(shaftPath); if (!sm) { sm = new Material(Shader.Find("FuwaCourse/VentShaft")); AssetDatabase.CreateAsset(sm, shaftPath); }
        sm.SetColor("_Top", new Color(0.82f, 0.43f, 0.4f)); sm.SetColor("_Bottom", new Color(0.5f, 0.22f, 0.22f)); sm.SetFloat("_Falloff", 1.2f);   /* 2026-11-02 てじゃ「ちょっと淡く」「道の素材に近づけたい」 */ sm.SetFloat("_Facet", 0.25f); EditorUtility.SetDirty(sm);
        Part("Mouth", mouth, AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/VentHoleMask.mat"));
        Part("Shaft", shaft, sm);
        return "ヒビ " + count + " か所, 口の三角形 " + mt.Count / 3 + ", 中の三角形 " + st.Count / 3;
    }

    // 火炎放射が当たる所の予兆：地面の赤いグラデ（FuwaFireJet.warnArea）。道の見た目のメッシュに沿わせて、道の外は作らない。作り直さずシーンだけ直す時もこれを呼ぶ
    public static string JetWarnAreas(Transform fire)
    {
        var course = fire.root; var temps = new List<MeshCollider>();
        foreach (var n in new[] { "ZigPath", "Path12", "Path5" })
            foreach (var t in course.GetComponentsInChildren<Transform>(true))
                if (t.name == n && t.GetComponent<MeshFilter>()) { var c = t.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh; temps.Add(c); }
        Physics.SyncTransforms();
        string mp = "Assets/FuwaStage2/Materials/Stage2_2_FireWarn.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp); if (!mat) { mat = new Material(Shader.Find("FuwaCourse/FireWarn")); AssetDatabase.CreateAsset(mat, mp); }
        mat.SetColor("_Color", new Color(1f, 0.82f, 0.1f, 1f)); mat.SetFloat("_Base", 0.5f); mat.SetFloat("_Sharp", 1.5f); WarnMulLook(mat); EditorUtility.SetDirty(mat);   // 山吹色。赤だと赤い道に溶けた（2026-11-03 てじゃ「もっと目立たせたい」）
        int made = 0, tris = 0;
        foreach (var j in fire.GetComponentsInChildren<FuwaFireJet>(true))
        {
            if (j.ballKnock <= 0f || j.knockOrigin == null) continue;
            var g = j.transform.parent; var old = g.Find("WarnArea"); if (old) Object.DestroyImmediate(old.gameObject);
            var bc = j.GetComponent<BoxCollider>(); const int NL = 26, NW = 7; float W = JetWarnW;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>(); var id = new int[NL + 1, NW + 1];
            float fromY = j.knockOrigin.position.y;
            // 根元は箱の始まりより JetWarnRootExt だけ山側から作って、道のふちで切る（2026-11-04 てじゃ「１本目（の根元）がきれてた」：道のふちが斜めで、片側がふちまで届いていなかった）
            float ext = JetWarnRootExt / bc.size.y;
            for (int l = 0; l <= NL; l++) for (int w = 0; w <= NW; w++)
            {
                float v = Mathf.Lerp(-ext, 1f, l / (float)NL), u = w / (float)NW;
                var q = j.transform.TransformPoint(bc.center + new Vector3((u - 0.5f) * W, (v - 0.5f) * bc.size.y, 0f)); id[l, w] = -1;
                // 道の外の点は、エリアのまん中へ向かって道のふちまで引き戻す（落とすと端がギザギザに切れた。2026-11-02 てじゃ）
                bool Down(Vector3 at, out Vector3 got)
                {
                    float best = float.MaxValue; bool found = false; got = at; var ray = new Ray(new Vector3(at.x, fromY + 1.5f, at.z), Vector3.down);
                    foreach (var c in temps) if (c.Raycast(ray, out var h, 4f) && h.distance < best && h.normal.y > 0.5f) { best = h.distance; got = h.point; found = true; }
                    return found;
                }
                bool ok = Down(q, out var hit);
                if (!ok)
                {
                    // まず同じ横位置のまま長さの向きに引き戻す（根元・先の切り口がふちにそろう）。それでもだめならまん中へ
                    var midU = j.transform.TransformPoint(bc.center + new Vector3((u - 0.5f) * W, 0f, 0f));
                    if (Down(midU, out var muHit))
                    {
                        float lo2 = 0f, hi2 = 1f; hit = muHit; for (int it = 0; it < 9; it++) { float m = (lo2 + hi2) * 0.5f; if (Down(Vector3.Lerp(q, midU, m), out var mh)) { hi2 = m; hit = mh; } else lo2 = m; }
                        id[l, w] = verts.Count; verts.Add(g.InverseTransformPoint(hit + Vector3.up * 0.015f)); uvs.Add(new Vector2(u, Mathf.Max(0f, v)));
                        continue;
                    }
                    var mid = j.transform.TransformPoint(bc.center); float lo = 0f, hi = 1f;   // lo：道の外、hi：まん中
                    if (!Down(mid, out var midHit)) continue;
                    hit = midHit; for (int it = 0; it < 7; it++) { float m = (lo + hi) * 0.5f; if (Down(Vector3.Lerp(q, mid, m), out var mh)) { hi = m; hit = mh; } else lo = m; }
                }
                id[l, w] = verts.Count; verts.Add(g.InverseTransformPoint(hit + Vector3.up * 0.015f)); uvs.Add(new Vector2(u, Mathf.Max(0f, v)));
            }
            void Tri(int a, int b, int c)
            {
                var ab = g.TransformPoint(verts[b]) - g.TransformPoint(verts[a]); var ac = g.TransformPoint(verts[c]) - g.TransformPoint(verts[a]);
                if (Vector3.Cross(ab, ac).y >= 0f) idx.AddRange(new[] { a, b, c }); else idx.AddRange(new[] { a, c, b });
            }
            for (int l = 0; l < NL; l++) for (int w = 0; w < NW; w++)
            {
                int a = id[l, w], b = id[l + 1, w], c = id[l + 1, w + 1], d = id[l, w + 1]; if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                Tri(a, b, c); Tri(a, c, d);
            }
            var mesh = new Mesh { name = "Course2_2_" + g.name + "_WarnArea" }; mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(idx, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var o = new GameObject("WarnArea", typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(g, false); o.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
            var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.enabled = false;
            j.warnArea = r; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j); EditorUtility.SetDirty(j);
            made++; tris += idx.Count / 3;
        }
        foreach (var c in temps) Object.DestroyImmediate(c);
        return "予兆エリア " + made + " 個, 三角形 " + tris;
    }

    // 火球の飛ぶ道の地面のグラデ（FuwaFireball.warnAreas）。穴と穴の間の帯だけ（真上に飛ぶ穴は、てじゃ「真上のはいらんかも」でなし）。道に沿わせて、道の外と穴の中は作らない。作り直さずシーンだけ直す時もこれを呼ぶ
    public static Color FireballWarnTint = new Color(1.15f, 1.7f, 0.4f, 1f);
    // 2026-11-04 てじゃ「予告線が出るのをちょっと速く」「ふちは色をちょっと濃く」：飛ぶ 0.4 秒前から出す、両はしを濃いオレンジ寄りに
    public static Color FireballWarnEdgeTint = new Color(1.2f, 1.05f, 0.15f, 1f);
    public static float FireballWarnLead = 0.4f, FireballWarnEdgeW = 0.3f;
    // 予告の地面を「道の色に掛ける＋濃いふち」に（火球も火炎放射も同じ。2026-11-04 てじゃ「火炎放射の予告線もこれで」）
    public static float FireballWarnBase = 0.2f;   // 帯の間の薄さ 0.5→0.2（2026-11-04 てじゃ「濃いとこと薄いところの差を」）
    public static float FireballWarnSpeed = 4.5f;   // 火球の帯の流れる速さ 10→6.5→4.5（2026-11-04 てじゃ「火球のグラデの動きの速さもうちょいおそく」×2）
    static void WarnMulLook(Material mat)
    {
        mat.SetFloat("_Mode", 1f); mat.SetColor("_Tint", FireballWarnTint); mat.SetColor("_EdgeTint", FireballWarnEdgeTint); mat.SetFloat("_EdgeW", FireballWarnEdgeW);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor); mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.SrcColor);
    }
    public static float FireballWarnW = FireballSize + 0.25f;   // 1.75m。火球の当たりの太さ（1.6m）にそろえた後、てじゃ「ほんのちょっぴり太く」（2026-11-04。最初は 1.9）
    public static string FireballWarns(Transform fire)
    {
        var course = fire.root; var temps = new List<MeshCollider>();
        foreach (var n in new[] { "ZigPath", "Path12", "Path5" })
            foreach (var t in course.GetComponentsInChildren<Transform>(true))
                if (t.name == n && t.GetComponent<MeshFilter>()) { var c = t.gameObject.AddComponent<MeshCollider>(); c.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh; temps.Add(c); }
        Physics.SyncTransforms();
        string mp = "Assets/FuwaStage2/Materials/Stage2_2_FireballWarn.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp); if (!mat) { mat = new Material(Shader.Find("FuwaCourse/FireWarn")); AssetDatabase.CreateAsset(mat, mp); }
        mat.SetColor("_Color", new Color(1f, 0.82f, 0.1f, 1f)); mat.SetFloat("_Base", FireballWarnBase); mat.SetFloat("_Sharp", 1.5f); mat.SetFloat("_Tip", 1f); mat.SetFloat("_RootFade", 0.1f); mat.SetFloat("_Speed", FireballWarnSpeed); WarnMulLook(mat); EditorUtility.SetDirty(mat);   // 道の色に掛けて、影の下に見えるように（2026-11-04 てじゃ「影より下にうつしたい」）
        int made = 0, tris = 0;
        foreach (var f in fire.GetComponentsInChildren<FuwaFireball>(true))
        {
            var g = f.transform.parent;
            for (int i = g.childCount - 1; i >= 0; i--) if (g.GetChild(i).name.StartsWith("WarnArea")) Object.DestroyImmediate(g.GetChild(i).gameObject);
            float fromY = f.holes[0].position.y;
            bool Down(Vector3 at, out Vector3 got)
            {
                float best = float.MaxValue; bool found = false; got = at; var ray = new Ray(new Vector3(at.x, fromY + 2.5f, at.z), Vector3.down);
                foreach (var c in temps) if (c.Raycast(ray, out var h, 5f) && h.distance < best && h.normal.y > 0.5f) { best = h.distance; got = h.point; found = true; }
                return found;
            }
            var rs = new List<Renderer>(); var lens = new List<float>();
            int segs = f.oneWay ? Mathf.Min(1, f.holes.Length - 1) : f.holes.Length - 1;   // 一方通行（奥→手前）は最初と最後の穴の間の 1 本だけ（2026-11-04 てじゃ「火球の下の予告線復活」）
            for (int s = 0; s < segs; s++)
            {
                Vector3 a = f.holes[s].position, b = f.holes[f.oneWay ? f.holes.Length - 1 : s + 1].position, along = Flat(b - a).normalized, side = Vector3.Cross(Vector3.up, along);
                float len = Flat(b - a).magnitude;
                int NL = Mathf.Max(8, Mathf.CeilToInt(len / 0.25f)), NW = 7;
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>(); var id = new int[NL + 1, NW + 1];
                for (int l = 0; l <= NL; l++) for (int w = 0; w <= NW; w++)
                {
                    float v = l / (float)NL, u = w / (float)NW; id[l, w] = -1; Vector3 q, inner;
                    { q = a + along * (v * len) + side * ((u - 0.5f) * FireballWarnW); inner = (a + b) * 0.5f + side * ((u - 0.5f) * FireballWarnW * 0.5f); }
                    if (!Down(q, out var hit))
                    {
                        if (!Down(inner, out var ih)) continue;
                        hit = ih; float lo = 0f, hi = 1f; for (int it = 0; it < 7; it++) { float m = (lo + hi) * 0.5f; if (Down(Vector3.Lerp(q, inner, m), out var mh)) { hi = m; hit = mh; } else lo = m; }
                    }
                    id[l, w] = verts.Count; verts.Add(g.InverseTransformPoint(hit + Vector3.up * 0.015f));
                    // 帯は道ぞいの距離で。穴のふちで引き戻した所も、その時の距離に合わせる
                    float vv = Mathf.Clamp01(Vector3.Dot(hit - a, along) / Mathf.Max(0.01f, len));
                    uvs.Add(new Vector2(u, vv));
                }
                void Tri(int p, int q2, int r2)
                {
                    var ab = verts[q2] - verts[p]; var ac = verts[r2] - verts[p];
                    if (Vector3.Cross(ab, ac).y >= 0f) idx.AddRange(new[] { p, q2, r2 }); else idx.AddRange(new[] { p, r2, q2 });
                }
                for (int l = 0; l < NL; l++) for (int w = 0; w < NW; w++)
                {
                    int p0 = id[l, w], p1 = id[l + 1, w], p2 = id[l + 1, w + 1], p3 = id[l, w + 1]; if (p0 < 0 || p1 < 0 || p2 < 0 || p3 < 0) continue;
                    Tri(p0, p1, p2); Tri(p0, p2, p3);
                }
                var mesh = new Mesh { name = "Course2_2_" + g.name + "_WarnArea" + s }; mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(idx, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var o = new GameObject("WarnArea" + s, typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(g, false); o.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
                var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.enabled = false;
                rs.Add(r); lens.Add(len); made++; tris += idx.Count / 3;
            }
            f.warnAreas = rs.ToArray(); f.warnLengths = lens.ToArray(); f.warnLead = FireballWarnLead;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); EditorUtility.SetDirty(f);
        }
        foreach (var c in temps) Object.DestroyImmediate(c);
        return "火球の予兆 " + made + " 個, 三角形 " + tris;
    }

    // 炎の壁の音は全部の壁で 1 つ。壁の当たり（Burn の箱）の線の上で、聞く人にいちばん近い所から鳴る（FuwaLineSound）。作り直さずシーンだけ直す時もこれを呼ぶ
    public static void FireWallSound(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
            if (c.name == "FireWallLoop") Object.DestroyImmediate(c.gameObject);
            else if (c.name.StartsWith("FireWall")) { var old = c.Find("WallLoop"); if (old) Object.DestroyImmediate(old.gameObject); }
        }
        var a = new List<Vector3>(); var b = new List<Vector3>();
        for (int i = 0; i < parent.childCount; i++)
        {
            var w = parent.GetChild(i); if (!w.name.StartsWith("FireWall")) continue;
            foreach (var bc in w.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!bc.name.StartsWith("Burn")) continue;
                var half = bc.transform.right * bc.size.x * 0.5f;
                a.Add(parent.InverseTransformPoint(bc.transform.position - half)); b.Add(parent.InverseTransformPoint(bc.transform.position + half));
            }
        }
        var src = Sfx(parent, "FireWallLoop", "sfx_loop_firewall", a.Count > 0 ? a[0] : Vector3.zero, 4f, 20f, 0.55f, true);
        var ls = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaLineSound>(src.gameObject); ls.segA = a.ToArray(); ls.segB = b.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ls); FuwaStage2WaterTest.NoSync(ls);
    }

    // 炎の壁の下の岩（2026-10-31 てじゃ「炎の下がぶら下がってる、岩を伸ばす？でも歩けちゃだめ」）。V のすき間を埋める 1 つのかたまりで、まん中は溶岩、ふちはとがった岩。炎は溶岩から立つ。
    //  当たりなし。-x がジグザグの V の根元で、そちらは道の下にもぐる
    static void FireWallRock(Transform g, string name, List<WallPt> ptsA, List<WallPt> ptsB)
    {
        float xRoot = ptsA[0].p.x, xFork = ptsB[ptsB.Count - 1].p.x;
        var mb = new MB(); float x0 = xRoot - 1.1f, x1 = ptsA[ptsA.Count - 1].p.x + 1.8f; int nx = Mathf.Max(4, Mathf.CeilToInt((x1 - x0) / 0.32f));
        float Rnd(float n) { float v = Mathf.Sin(n * 12.9898f + name.Length * 7.1f + name[name.Length - 1] * 3.3f) * 43758.5453f; return v - Mathf.Floor(v); }
        Vector3[] Ring(int i)
        {
            float x = Mathf.Lerp(x0, x1, i / (float)nx) + (i > 0 && i < nx ? (Rnd(i) - 0.5f) * 0.14f : 0f), e = Mathf.Clamp01((x1 - x) / 2.4f), root = Mathf.Clamp01((xRoot + 1f - x) / 2.1f);   // e：外のはしへ細く、root：V の根元で道の下へもぐる
            WallPt a = WallAt(ptsA, x), b = x < xFork ? WallAt(ptsB, x) : a; bool aLow = a.p.z <= b.p.z; WallPt lo = aLow ? a : b, hi = aLow ? b : a;
            float zc = (lo.p.z + hi.p.z) * 0.5f, half = (hi.p.z - lo.p.z) * 0.5f, t = Mathf.Lerp(0.015f, 1f, Mathf.SmoothStep(0f, 1f, e)), slit = Mathf.SmoothStep(0f, 1f, e / 0.35f);   // 炎のない外のはしは割れ目を閉じる（溶岩が岩の底より下へ抜けて、裏返って見えた。2026-10-31 てじゃ「端っこがぬけてる」）
            float w = (half + 0.5f + 0.08f * Mathf.Sin(x * 0.8f + 1f)) * t, lw = (half + 0.3f) * t * slit, rise = Mathf.SmoothStep(0f, 1f, root) * 0.6f, drop = (1f - e) * 1.1f;
            float topLo = lo.floor + 0.4f + rise - drop, topHi = hi.floor + 0.4f + rise - drop, lavaLo = Mathf.Lerp(topLo, lo.floor + rise, slit), lavaHi = Mathf.Lerp(topHi, hi.floor + rise, slit);
            float SpikeL = Mathf.Pow(Rnd(i * 1.3f + 3f), 2.2f) * 0.55f * e * (1f - root), SpikeR = Mathf.Pow(Rnd(i * 1.7f + 11f), 2.2f) * 0.55f * e * (1f - root);   // 根元のトゲは道を突き抜けた
            float depth = Mathf.Lerp(1.3f, 4.4f, Mathf.InverseLerp(x1, x0, x)) * (0.82f + 0.14f * Mathf.Sin(x * 0.9f + 0.4f) + 0.08f * Mathf.Sin(x * 2.3f)) * Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, e));
            if (Rnd(Mathf.Floor(i / 3f) + 40f) > 0.68f && i % 3 == 1) depth += 0.7f * e;   // ときどき長いトゲ
            float minTop = Mathf.Min(topLo, topHi), minLava = Mathf.Min(lavaLo, lavaHi);
            float bottom = Mathf.Min(minTop - depth * Mathf.Lerp(0.15f, 1f, e) - 0.04f, minLava - 0.3f * slit - 0.03f), flank = Mathf.Min(minTop - Mathf.Min(1.2f, depth * 0.55f) * Mathf.Lerp(0.15f, 1f, e), minLava - 0.1f * slit - 0.015f);
            return new[] { new Vector3(x, bottom, zc + 0.25f * Mathf.Sin(x * 1.1f) * t), new Vector3(x, flank, zc - w * (1.2f + 0.16f * Rnd(i * 2.1f))), new Vector3(x, topLo + SpikeL, zc - w), new Vector3(x, lavaLo + 0.1f * slit, zc - lw * 1.05f), new Vector3(x, lavaLo, zc - lw), new Vector3(x, lavaHi, zc + lw), new Vector3(x, lavaHi + 0.1f * slit, zc + lw * 1.05f), new Vector3(x, topHi + SpikeR, zc + w), new Vector3(x, flank, zc + w * (1.2f + 0.16f * Rnd(i * 2.7f))) };
        }
        var outs = new[] { new Vector3(0, -1, -1), new Vector3(0, 0.3f, -1), new Vector3(0, 0.6f, 1), new Vector3(0, 0.6f, 1), new Vector3(0, 1, 0), new Vector3(0, 0.6f, -1), new Vector3(0, 0.6f, -1), new Vector3(0, 0.3f, 1), new Vector3(0, -1, 1) };
        for (int i = 0; i < nx; i++)
        {
            var a = Ring(i); var b = Ring(i + 1);
            for (int k = 0; k < a.Length; k++) { int n = (k + 1) % a.Length; mb.Quad(a[k], b[k], b[n], a[n], outs[k], k == 4 ? 1 : 0); }
        }
        foreach (var (rg, xo) in new[] { (Ring(0), -1f), (Ring(nx), 1f) })
            for (int k = 0; k < rg.Length; k++) { int n = (k + 1) % rg.Length; var c = rg.Aggregate(Vector3.zero, (q, v) => q + v) / rg.Length; mb.Quad(rg[k], rg[n], c, c, new Vector3(xo, 0, 0), 0); }
        var o = new GameObject("Rock", typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(g, false);
        o.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mb.Build("Course2_2_" + name + "_Rock", 2));
        var r = o.GetComponent<MeshRenderer>(); r.sharedMaterials = new[] { rockMat, FlowLavaMat() }; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(o, StaticEditorFlags.BatchingStatic);
    }
    // 炎の壁：線 ptsA（根元→口の先）と ptsB（根元→合流）にそった横に広い 1 つの炎（FlameMesh、2026-11-01 てじゃ「横に広いデカい一つの炎」）。
    //  上のふちの大きなうねりはシェーダーの _Wave。高さは道にそって上る。下は岩の中で開いている
    public static void FlameCurtain(Transform g, string name, List<WallPt> ptsA, List<WallPt> ptsB)
    {
        var f = new FlameMeshB(); float xTail = ptsA[ptsA.Count - 1].p.x; int blobN = 0;
        void Tube(List<WallPt> line, float layer, float step, float width, float heightFrac, float[] ks)
        {
            float x0 = line[0].p.x, x1 = line[line.Count - 1].p.x; int nx = Mathf.Max(2, Mathf.CeilToInt((x1 - x0) / step));
            for (int j = 0; j <= nx; j++)
            {
                float x = Mathf.Lerp(x0, x1, j / (float)nx); var q = WallAt(line, x);
                float lobe = layer < 0.5f ? 1f : 0.62f + 0.38f * (0.5f + 0.5f * Mathf.Sin(x * 1.1f + 1.9f));   // 黄色は高低差をつける
                float y0 = q.floor - 0.4f, endK = Mathf.Clamp01(Mathf.Min(x - x0, x1 - x) / 1.4f), h = (q.top - y0) * heightFrac * lobe * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01((xTail - x) / 1f)) * Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, endK));   // はしは低く丸める（縦に切れて見えた）
                float w = width * Mathf.Clamp01(Mathf.Min(x - x0, x1 - x) / 0.7f + 0.12f);
                for (int c = 0; c < ks.Length; c++)
                {
                    float kk = ks[c], sgn = c * 2 < ks.Length - 1 ? 1f : (c * 2 == ks.Length - 1 ? 0f : -1f), ny = kk * kk * kk * kk;
                    // 横へのでこぼこは小さく（道へはみ出さない）、上は大きく
                    f.Add(new Vector3(x, y0 + h * kk, q.p.z + sgn * w * (1f - ny * 0.8f)), new Vector3(0f, ny, sgn * (1f - ny) * 0.45f), layer, 0f, kk * heightFrac);
                }
            }
            f.Grid(nx, ks.Length - 1);
        }
        var body = new[] { 0f, 0.35f, 0.65f, 0.85f, 1f, 0.85f, 0.65f, 0.35f, 0f };
        var core = new[] { 0f, 0.5f, 1f, 0.5f, 0f };
        foreach (var line in new[] { ptsA, ptsB })
        {
            Tube(line, 0f, 0.25f, 0.42f, 0.95f, body);
            Tube(line, 1f, 0.6f, 0.2f, 0.5f, core);
            for (float x = line[0].p.x + 0.8f; x < line[line.Count - 1].p.x - 0.4f; x += 3.2f, blobN++)
            {
                var q = WallAt(line, x); float seed = Mathf.Repeat((blobN + 0.37f) * 0.618f, 1f);
                f.Blob(new Vector3(x, q.top - 1.2f, q.p.z), seed, 0.8f);
            }
        }
        var mat = FlameMeshMat("Stage2_2_FlameCurtain", 0.3f, 0.36f, 0.12f, 0.6f, 0.35f, 0.18f, 2.8f, 3.6f, 1f); mat.SetFloat("_Pulse", 0f); mat.SetFloat("_Wave", 2.2f); mat.SetFloat("_WaveScale", 0.09f);
        mat.SetFloat("_StencilPass", (float)UnityEngine.Rendering.StencilOp.Replace); mat.renderQueue = 2013;   // 岩の影用の印。ほかの物のあとに描いて、隠れた所には印を付けない。穴やヒビ（2010〜2012）より後（2026-11-02 炎の向こうのヒビが見えた）、岩（2014）より前
        EditorUtility.SetDirty(mat);
        var c2 = new GameObject("Curtain", typeof(MeshFilter), typeof(MeshRenderer)); c2.transform.SetParent(g, false);
        c2.GetComponent<MeshFilter>().sharedMesh = f.Build("Course2_2_" + name + "_Curtain", 4f);
        var r = c2.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // 火の玉：穴（の溶岩の面）hs を順に往復する。てっぺんが道の FireballTop 上の弧
    public static float RockR = 0.9f, RockHop = 1.05f, RockPeriod = 8.9f;   // 2026-11-01 てじゃ「落ちる岩、もっと遅く」0.75→1.05、1周は飛ぶ時間 8.4 秒＋休み 0.5 秒（前は 6 秒＋0.5 秒）。2 列は半周ずらす
      // 予告線（点線）は試したけど「びみょう、やっぱなしに」（2026-10-30）   // 火の岩の半径（てじゃ「もっとデカく」0.45→0.9）・1回はねる時間・1周（降る〜休み）
    // コースの外の火山（2026-10-30 てじゃ「いまの岩の出る柱のちょい奥くらいにデカい火山」「下から浮いてる感じに」）：c（道の高さ）を中心に、
    //  すそは下の雲の中（VolcanoBottom）まで続いて、火口は道の VolcanoTopH 上。道の高さでの半径 VolcanoR0。VolcanoRAt は道の計算と Blender の形の土台（Tools/stage2_volcano.json）。変えたら JSON も書き出し直す。
    //  火口から岩が飛び出す時の火の粉を返す。のちのち「その山を登って通り過ぎてゴール」にしたい（途中は未定）
    static float VolcanoRAt(float h, float a)
    {
        float p = 0.85f, k = (VolcanoR0 - VolcanoTopR) / Mathf.Pow(VolcanoTopH, p);
        float r = VolcanoTopR + k * Mathf.Pow(Mathf.Max(0f, VolcanoTopH - h), p);
        // ノイズは円の上で取る（角度そのままだと 0° と 360° で値がちがって縦に割れ目が出た）
        float n = Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + 3.1f + h * 0.05f, Mathf.Sin(a) * 1.6f + 7.3f + h * 0.06f) - 0.5f;
        return r * (1f + 0.16f * n + 0.04f * Mathf.Sin(5f * a + h * 0.2f));
    }
    // 道の側面：下のほうが溶岩の照り返しで赤く光る（2026-10-31 てじゃ「岩はほどほどにしつつ盛っていこう」。シマシマの上の面はそのまま）
    public static Material PathSideMat()
    {
        var m = FuwaStage2WaterTest.ToonMat("Stage2_2_Rock", RockCol);
        // 火山と同じ暗い赤茶。下ほど少し溶岩の赤。段の模様と面のカクカクで岩らしく（2026-10-31）
        m.SetColor("_Color", new Color(0.3f, 0.17f, 0.17f)); m.SetFloat("_EmissionGradient", 0f); m.SetColor("_EmissionColor", Color.black);
        m.SetFloat("_NoiseStrength", 0.25f); m.SetFloat("_NoiseScale", 0.8f); m.SetFloat("_StrataStrength", 0.45f); m.SetFloat("_StrataScale", 1.5f); m.SetFloat("_Facet", 0.7f); m.SetFloat("_RimStrength", 0.06f);
        m.SetColor("_UnderColor", new Color(1.5f, 0.75f, 0.55f)); m.SetFloat("_UnderStrength", 0.7f);
        EditorUtility.SetDirty(m);
        return m;
    }
    // 道から落ちる溶岩：ただの板で途中で切れて見えたので、うねりながら細くなって先がとがる形に。先からしずくが落ちる（当たりはもともとなし）
    public static void LavaFallLook(GameObject fall)
    {
        const string path = "Assets/FuwaStage2/Generated/Course2_2/Course2_2_LavaFall.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = !mesh;
        {
            const int Rows = 14, Sides = 12;
            var v = new List<Vector3>(); var tr = new List<int>();
            for (int j = 0; j <= Rows; j++)
            {
                float t = j / (float)Rows;
                float w = 0.5f * (1f - Mathf.Pow(t, 2.4f)) * (0.88f + 0.12f * Mathf.Sin(t * 13f + 0.6f));
                float off = 0.07f * Mathf.Sin(t * 7.5f) * t;
                for (int i = 0; i < Sides; i++) { float a = i * Mathf.PI * 2f / Sides; v.Add(new Vector3(off + Mathf.Cos(a) * w, 0.5f - t, Mathf.Sin(a) * 0.5f * (1f - t * t))); }
            }
            for (int j = 0; j < Rows; j++) for (int i = 0; i < Sides; i++)
            {
                int a = j * Sides + i, b = j * Sides + (i + 1) % Sides, c = a + Sides, d = b + Sides;
                tr.AddRange(new[] { a, b, c, b, d, c });
            }
            // 上にふた（上からのぞくと中を通して空が見えた）
            int top = v.Count; v.Add(new Vector3(0f, 0.5f, 0f));
            for (int i = 0; i < Sides; i++) tr.AddRange(new[] { top, (i + 1) % Sides, i });
            if (isNew) mesh = new Mesh { name = "Course2_2_LavaFall" }; else mesh.Clear(false);
            mesh.SetVertices(v); mesh.SetTriangles(tr, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        }
        fall.GetComponent<MeshFilter>().sharedMesh = mesh;
        var s = fall.transform.localScale; const float Thick = 0.35f;
        fall.transform.localScale = new Vector3(s.x, s.y, Thick);
        var old = fall.transform.parent.Find(fall.name + "_Drips"); if (old) Object.DestroyImmediate(old.gameObject);
        var ps = FuwaStage2FireTest.FlamePS(fall.transform.parent, fall.name + "_Drips", 0.08f, 0.1f, 2f, new Color(1f, 0.7f, 0.2f), new Color(1f, 0.3f, 0.06f));
        ps.transform.position = fall.transform.TransformPoint(0.05f, -0.48f, 0f); ps.transform.rotation = Quaternion.LookRotation(Vector3.down);
        var m = ps.main; m.startColor = Color.white; m.startSpeed = 0.2f; m.startLifetime = 1.6f; m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f); m.gravityModifier = 0.45f; m.maxParticles = 8;
        var em = ps.emission; em.rateOverTime = 3f;
        var c2 = ps.colorOverLifetime; c2.enabled = true; var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.72f, 0.25f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.06f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        c2.color = gr;
        ps.Play();
    }
    // コースの前半のまわりに浮かべる、焦げた木の丸い浮島（2026-10-31 てじゃ「他のコースと統一感」→ やみのもりの木と同じメッシュ、丸い浮島、上は枯れた芝の色）。遠くの飾りなので当たりなし・影なし
    public static readonly (float x, float y, float z, float scale, float yaw, bool fire)[] BurntIsleSpots =
    {
        // 道に近づけた（x を 0.7 倍。2026-11-01 てじゃ「周りの浮島ちょっと遠い」）
        (-15.4f, -3f, -16f, 1.6f, 20f, false), (-21f, 3f, 2f, 2f, 130f, true), (-14f, -7f, 20f, 1.4f, 250f, false),
        (14.79f, 1f, -52.44f, 1.8f, 70f, true) /* 外周の 1 番目の火の島。スタートの島のすぐ横へ（2026-11-03 てじゃ「ちょっと遠くに」「もっとスタート地点側に」「もっと思いっきり」） */, (21.7f, -5f, -7f, 1.5f, 200f, false), (15.4f, 4f, 10f, 1.7f, 310f, true),
        // まものエリアから先にも足す（てじゃ「最後の火炎放射を超えた先が見た目が寂しい」）
        (46f, 9f, 59f, 1.8f, 40f, false), (52f, 15f, 79f, 2f, 160f, true), (40f, 5f, 99f, 1.6f, 280f, false), (16f, 17f, 97f, 1.7f, 90f, true),
        (-24f, 7f, 43f, 1.7f, 200f, false), (-26f, 11f, 69f, 1.8f, 330f, true),   // fire：木の代わりに燃えている島（2026-10-31 てじゃ「全部木じゃなくて燃えてる島も混ぜよ」）
    };
    // やみのもりの枯れ木のコピーで、蜘蛛の巣用に横へ長くのびた枝だけ短くする（2026-10-31 てじゃ「ちょうどいい長さに」）。太さと曲がりはそのまま、のびる向きだけ縮める
    public const float BurntBranchLength = 0.72f;
    public static Mesh BurntTreeMesh(Mesh src)
    {
        const string path = "Assets/FuwaStage2/Generated/Course2_2/Course2_2_BurntTree.asset";
        var vs = src.vertices; var tr = src.triangles;
        var p = new int[vs.Length]; for (int i = 0; i < p.Length; i++) p[i] = i;
        int F(int a) { while (p[a] != a) { p[a] = p[p[a]]; a = p[a]; } return a; }
        for (int i = 0; i < tr.Length; i += 3) { p[F(tr[i])] = F(tr[i + 1]); p[F(tr[i])] = F(tr[i + 2]); }
        // 枝は x が幹（-1.4 くらいまで）を大きく越えてのびた部品
        var maxX = new Dictionary<int, float>();
        for (int i = 0; i < vs.Length; i++) { int r = F(i); maxX[r] = maxX.TryGetValue(r, out var x) ? Mathf.Max(x, vs[i].x) : vs[i].x; }
        var branch = new List<int>(); for (int i = 0; i < vs.Length; i++) if (maxX[F(i)] > -0.5f) branch.Add(i);
        var from = Vector3.zero; var to = Vector3.zero; int nf = 0, nt = 0;
        foreach (int i in branch) { if (vs[i].x < -1.5f) { from += vs[i]; nf++; } else if (vs[i].x > -0.05f) { to += vs[i]; nt++; } }
        from /= Mathf.Max(nf, 1); to /= Mathf.Max(nt, 1);
        var d = (to - from).normalized; float k = BurntBranchLength / (to - from).magnitude;
        foreach (int i in branch) { var o = vs[i] - from; float a = Vector3.Dot(o, d); if (a > 0f) vs[i] += d * (a * k - a); }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); bool isNew = !mesh;
        if (isNew) mesh = new Mesh { name = "Course2_2_BurntTree" }; else mesh.Clear(false);
        mesh.SetVertices(vs); mesh.SetNormals(src.normals); mesh.SetUVs(0, src.uv); mesh.SetTriangles(tr, 0); mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }
    public static void BurntIsles(Transform fire, Transform lavaRoot)
    {
        var old = fire.Find("BurntIsles"); if (old) Object.DestroyImmediate(old.gameObject);
        Mesh M(string p) => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/" + p + ".asset");
        var deadTree = M("Generated/Lite/YamiDeadTree_Lite"); var disc = M("Meshes/IslandDisc/IslandDisc_r64"); var under = M("Meshes/FarIslands/IslandUnderLow_2");
        if (!deadTree || !disc || !under) return;
        deadTree = BurntTreeMesh(deadTree);
        var bark = FuwaStage2WaterTest.ToonMat("Stage2_2_BurntBark", new Color(0.2f, 0.15f, 0.15f));
        // 上の面は他のコースの芝（Goal_Ground）の色ちがいで、枯れた芝の色
        const string topPath = "Assets/FuwaStage2/Materials/Stage2_2_DryGrass.mat";
        var top = AssetDatabase.LoadAssetAtPath<Material>(topPath);
        if (!top) { top = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat")); AssetDatabase.CreateAsset(top, topPath); }
        top.SetColor("_Color", new Color(0.72f, 0.6f, 0.34f)); EditorUtility.SetDirty(top);
        // 燃えている島の上：焦げた芝
        const string scorchedPath = "Assets/FuwaStage2/Materials/Stage2_2_ScorchedGrass.mat";
        var scorched = AssetDatabase.LoadAssetAtPath<Material>(scorchedPath);
        if (!scorched) { scorched = new Material(top); AssetDatabase.CreateAsset(scorched, scorchedPath); }
        scorched.SetColor("_Color", new Color(0.3f, 0.2f, 0.17f)); EditorUtility.SetDirty(scorched);
        var isleFlame = IsleFlameMesh(); var flameMat = SeedFlameMat();   // 焚き火と同じ炎を粗くして大きくして使う
        var underMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/IslandUnder.mat");
        var g = new GameObject("BurntIsles").transform; g.SetParent(fire, false);
        var h = Vector3.zero; int n = 0; foreach (Transform c in lavaRoot) { h += c.localPosition; n++; } if (n > 0) h /= n;
        for (int i = 0; i < BurntIsleSpots.Length; i++)
        {
            var s = BurntIsleSpots[i];
            var isle = new GameObject("Isle" + i).transform; isle.SetParent(g, false); isle.localPosition = h + new Vector3(s.x, s.y, s.z); isle.localRotation = Quaternion.Euler(0f, s.yaw, 0f); isle.localScale = Vector3.one * s.scale;
            // やみのもりの ForestTree と同じ組み立て（木の根元の高さ・島の位置はメッシュごとに決まっている）
            //  葉のある木（YamiTree）は色を焦がすと黒いかたまりに見えたので、枯れ木（YamiDeadTree）だけ。島はやみのもりより広く（細い木に小さい島で棒に見えた）
            var ground = new Vector3(-1.6f, -1.38f, 0f);
            Transform Part(string name, Mesh mesh, Material mat, Vector3 lp, Quaternion lr, Vector3 ls)
            {
                var o = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(isle, false); o.transform.localPosition = lp; o.transform.localRotation = lr; o.transform.localScale = ls;
                o.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                return o.transform;
            }
            if (!s.fire) Part("Tree", deadTree, bark, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("IslandTop", disc, s.fire ? scorched : top, ground, Quaternion.identity, new Vector3(1.9f, 1f, 1.9f));
            Part("IslandUnder", under, underMat, ground, Quaternion.Euler(0f, 199f, 0f), new Vector3(1.2f, 1.1f, 1.2f));
            if (s.fire)
            {
                Part("Flames", isleFlame, flameMat, ground + Vector3.down * 0.32f, Quaternion.identity, new Vector3(2.1f, 2.1f, 2.1f));
                var ps = FuwaStage2FireTest.FlamePS(isle, "Embers", 0.6f, 1.2f, 1.6f, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.28f, 0.05f));
                ps.transform.localPosition = ground + Vector3.up * 0.3f;
                var m = ps.main; m.startColor = Color.white; m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f); m.maxParticles = 10;
                var em = ps.emission; em.rateOverTime = 4f;
                ps.Play();
            }
        }
    }
    // 空を舞う火の粉：ホール・ジグザグ・火山のまわりの 3 か所。光の粒だけなので当たりなし
    public static void CourseEmbers(Transform fire, Transform lavaRoot, Vector3 pad, Vector3 vc)
    {
        var old = fire.Find("Embers"); if (old) Object.DestroyImmediate(old.gameObject);
        var g = new GameObject("Embers").transform; g.SetParent(fire, false);
        var holes = Vector3.zero; int n = 0;
        foreach (Transform c in lavaRoot) { holes += c.localPosition; n++; }
        holes = n > 0 ? holes / n : pad;
        void Add(string name, Vector3 c, Vector3 size, float rate)
        {
            var ps = FuwaStage2FireTest.FlamePS(g, name, 1f, 0.5f, 6f, new Color(1f, 0.78f, 0.3f), new Color(1f, 0.32f, 0.06f));
            ps.transform.localPosition = c;
            var m = ps.main; m.startColor = Color.white; m.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8f); m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.36f); m.maxParticles = Mathf.CeilToInt(rate * 8f); m.prewarm = true; m.gravityModifier = -0.012f;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(size.x, size.z, size.y);
            var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.42f, 0.08f), 0.6f), new GradientColorKey(new Color(0.8f, 0.14f, 0.04f), 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var vl = ps.velocityOverLifetime; vl.enabled = true; vl.space = ParticleSystemSimulationSpace.World;
            vl.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f); vl.y = new ParticleSystem.MinMaxCurve(0.25f, 0.8f); vl.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            ps.Play();
        }
        Add("Holes", holes + Vector3.up * 3f, new Vector3(26f, 9f, 34f), 16f);
        Add("Zig", pad + Vector3.up * 2f + new Vector3(-6f, 0f, -8f), new Vector3(26f, 14f, 26f), 14f);
        Add("Volcano", vc + Vector3.up * 3f, new Vector3(48f, 20f, 48f), 26f);
    }
    // 火山の岩：丸くて暗い玉 → 角ばった溶岩の岩（Tools/stage2_lavarock.py、FuwaCourse/LavaRock。2026-10-31）。当たりは今まで通りスクリプトの球
    // 火の玉と同じ見た目は試して「ちょっと差がないとびみょう」（2026-10-31）→ 形は角ばった岩のまま、割れ目を太く・ふちを光らせて燃えている岩に
    public static void LavaRockLook(GameObject vis, string name)
    {
        var mesh = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaStage2/Models/Stage2_LavaRock.fbx").OfType<Mesh>().FirstOrDefault(x => x.name == "Stage2_LavaRock" + (name.EndsWith("1") ? 1 : 0));
        if (mesh) vis.GetComponent<MeshFilter>().sharedMesh = mesh;
        // 炎越しの影はジグザグに飛んでくる 2 個だけ（2026-11-02 てじゃ「影でみえるのはジグザグに影響する２個だけにしたい」）
        bool zigRock = name == "LavaRock0" || name == "LavaRock1";
        var m = FuwaStage2WaterTest.ToonMat(zigRock ? "Stage2_2_LavaRock" : "Stage2_2_LavaRockNoShade", new Color(0.35f, 0.12f, 0.08f));
        var sh = Shader.Find("FuwaCourse/LavaRock");
        if (sh) { m.shader = sh; m.SetTexture("_FlowTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Stage2_LavaFlow.png")); m.SetFloat("_Width", 0.42f); m.SetInt("_ShadeComp", (int)(zigRock ? UnityEngine.Rendering.CompareFunction.Equal : UnityEngine.Rendering.CompareFunction.Never)); m.renderQueue = 2014; EditorUtility.SetDirty(m); }   // 炎の壁のあと（炎越しの影のため）
        vis.GetComponent<Renderer>().sharedMaterial = m;
    }
    // 火の玉：のっぺりしたオレンジ→メラメラ（FuwaCourse/Fireball、2026-10-31）。尾は太く長く
    // 遠くの炎・火の玉を網目で薄く（FuwaFireFar.cginc の FuwaFireFadeClip。2026-11-04 てじゃ「炎重なってるとこ全般」「火球も」）
    public static float FireFadeNear = 14f, FireFadeFar = 40f, FireFadeMin = 0.3f;
    static void FireFadeLook(Material m) { m.SetFloat("_FireFadeNear", FireFadeNear); m.SetFloat("_FireFadeFar", FireFadeFar); m.SetFloat("_FireFadeMin", FireFadeMin); }
    public static Material FireballMat()
    {
        var m = FuwaStage2WaterTest.ToonMat("Stage2_2_Fireball", new Color(1f, 0.55f, 0.15f));
        var sh = Shader.Find("FuwaCourse/Fireball");
        if (sh) { m.shader = sh; m.SetTexture("_FlowTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Stage2_LavaFlow.png")); FireFadeLook(m); EditorUtility.SetDirty(m); }
        return m;
    }
    public static void FireballTrail(ParticleSystem trail)
    {
        var m = trail.main; m.startSize = new ParticleSystem.MinMaxCurve(0.75f, 1.25f); m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f); m.maxParticles = 80;
        var sh = trail.shape; sh.radius = 0.5f;
        var so = trail.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.25f)));
        // 尾：はじめは火の玉と同じ明るいオレンジのまま（すぐ赤く薄くなると、空の前でピンクの点に見えた）
        var col = trail.colorOverLifetime; col.enabled = true; var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.82f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.5f), new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
    }
    const float FireballTrailRate = 95f;
    // 岩の尾：火の玉と同じ色の変わり方で、岩の大きさに合わせる（前の太さは近くで赤いもやが岩より目立った）
    public static void LavaRockTrail(ParticleSystem trail)
    {
        FireballTrail(trail);
        var m = trail.main; m.startSize = new ParticleSystem.MinMaxCurve(RockR * 0.75f, RockR * 1.25f);
    }
    // 穴・みぞの溶岩：火山と同じ流れる溶岩（2026-10-31、前はベタ塗りのオレンジ）
    public static Material FlowLavaMat()
    {
        var m = FuwaStage2WaterTest.ToonMat("Stage2_2_Lava", LavaCol);
        var sh = Shader.Find("FuwaCourse/Lava");
        if (sh) { m.shader = sh; m.SetTexture("_FlowTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Stage2_LavaFlow.png")); EditorUtility.SetDirty(m); }
        return m;
    }
    public static float CurtainWave = 0.03f, CurtainWaveSpeed = 0.7f, CurtainFallSpeed = 0.1f, CurtainFray = 0.6f;   // Fray：先のこの割合で、水の滝みたいにちぎれて消える（2026-11-03 てじゃ「ポタポタなくして」「先を水の滝みたいに消える感じ」「速度はゆっくりめ」）
    public static Material LavaCurtainMat(float topY)
    {
        const string mp = "Assets/FuwaStage2/Materials/Stage2_2_LavaCurtain.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!m) { m = new Material(FlowLavaMat()); AssetDatabase.CreateAsset(m, mp); }
        m.SetFloat("_Wave", CurtainWave); m.SetFloat("_WaveSpeed", CurtainWaveSpeed); m.SetFloat("_WaveTop", topY); m.SetFloat("_FallSpeed", CurtainFallSpeed); m.SetFloat("_Fray", CurtainFray); EditorUtility.SetDirty(m);
        return m;
    }
    const string VolcanoModel ="Assets/FuwaStage2/Models/Stage2_Volcano.fbx", VolcanoMat = "Assets/FuwaStage2/Materials/Stage2_2_Volcano.mat";
    // 噴煙：蒸気のもくもく（FuwaCourse/SteamPuff、ソフトパーティクルなし）を暗くして大きく。下は火口の熱で赤く、上へ行くほど灰色で消える
    public static ParticleSystem VolcanoSmoke(Transform top)
    {
        const string mp = "Assets/FuwaStage2/Materials/Stage2_2_VolcanoSmoke.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!mat) { mat = new Material(Shader.Find("FuwaCourse/SteamPuff")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("FuwaCourse/SteamPuff");
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/SteamPuffSheet.png");
        mat.SetColor("_Color", Color.white); mat.SetColor("_ShadeColor", new Color(0.7f, 0.66f, 0.72f));
        EditorUtility.SetDirty(mat);
        var old = top.Find("Smoke"); if (old) Object.DestroyImmediate(old.gameObject);
        var ps = FuwaStage2FireTest.FlamePS(top, "Smoke", 2.2f, 3.4f, 13f, new Color(0.5f, 0.42f, 0.44f), new Color(0.72f, 0.68f, 0.72f));
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(8f, 11f); m.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 3.8f); m.startSize = new ParticleSystem.MinMaxCurve(6f, 8.5f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); m.maxParticles = 90; m.prewarm = true; m.gravityModifier = -0.012f;
        var em = ps.emission; em.rateOverTime = 8f;
        var sh = ps.shape; sh.angle = 13f; sh.radius = 2.2f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.5f, 0.28f), 0f), new GradientColorKey(new Color(0.66f, 0.58f, 0.62f), 0.14f), new GradientColorKey(new Color(0.93f, 0.9f, 0.93f), 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.08f), new GradientAlphaKey(0.62f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.5f, 1.5f), new Keyframe(1f, 2.4f)));
        // 上へ行くと少し風に流れる（全部まっすぐだと柱に見える）
        var vl = ps.velocityOverLifetime; vl.enabled = true; vl.space = ParticleSystemSimulationSpace.World;
        vl.x = new ParticleSystem.MinMaxCurve(0.9f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1.4f))); vl.y = new ParticleSystem.MinMaxCurve(0f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f))); vl.z = new ParticleSystem.MinMaxCurve(0.45f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1.4f)));
        var rl = ps.rotationOverLifetime; rl.enabled = true; rl.z = new ParticleSystem.MinMaxCurve(-0.14f, 0.14f);
        var ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 4; ts.numTilesY = 4; ts.animation = ParticleSystemAnimationType.WholeSheet;
        ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f); ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 15f / 16f); ts.cycleCount = 1;
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.sortMode = ParticleSystemSortMode.YoungestInFront; r.maxParticleSize = 4f;
        ps.Play();
        return ps;
    }
    static ParticleSystem Volcano(Transform parent, Vector3 c)
    {
        var g = new GameObject("Volcano").transform; g.SetParent(parent, false); g.localPosition = c;
        const int N = 56;
        // 輪の高さ：下はまばら、上ほど細かく
        var hs = new List<float>();
        for (float h = VolcanoBottom; h < VolcanoTopH - 0.01f; h += Mathf.Lerp(6f, 1.2f, Mathf.InverseLerp(VolcanoBottom, VolcanoTopH, h))) hs.Add(h);
        hs.Add(VolcanoTopH);
        Vector3 P(float a, float r, float y) => new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
        var mb = new MB();
        for (int i = 0; i < N; i++)
        {
            float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
            var o = P((a0 + a1) * 0.5f, 1f, 0f);
            // すその下のはし：下へとがって閉じる（浮いている島の底のように）
            mb.Quad(P(a0, VolcanoRAt(hs[0], a0), hs[0]), P(a1, VolcanoRAt(hs[0], a1), hs[0]), Vector3.up * (VolcanoBottom - 14f), Vector3.up * (VolcanoBottom - 14f), o + Vector3.down, 0);
            for (int j = 0; j + 1 < hs.Count; j++)
                mb.Quad(P(a0, VolcanoRAt(hs[j], a0), hs[j]), P(a1, VolcanoRAt(hs[j], a1), hs[j]), P(a1, VolcanoRAt(hs[j + 1], a1), hs[j + 1]), P(a0, VolcanoRAt(hs[j + 1], a0), hs[j + 1]), o + Vector3.up * 0.4f, 0);
            // 火口：ふち → 内側の壁 → 溶岩の池
            float t0 = VolcanoRAt(VolcanoTopH, a0), t1 = VolcanoRAt(VolcanoTopH, a1);
            float ih = VolcanoTopH - 0.3f, ph = VolcanoTopH - 1.6f;
            mb.Quad(P(a0, t0, VolcanoTopH), P(a1, t1, VolcanoTopH), P(a1, t1 - 0.5f, ih), P(a0, t0 - 0.5f, ih), Vector3.up, 0);
            mb.Quad(P(a0, t0 - 0.5f, ih), P(a1, t1 - 0.5f, ih), P(a1, t1 - 1.3f, ph - 0.4f), P(a0, t0 - 1.3f, ph - 0.4f), -o, 0);
            float q0 = Mathf.Lerp(t0 - 0.5f, t0 - 1.3f, (ih - ph) / (ih - ph + 0.4f)) + 0.05f, q1 = Mathf.Lerp(t1 - 0.5f, t1 - 1.3f, (ih - ph) / (ih - ph + 0.4f)) + 0.05f;
            var pc = Vector3.up * ph;
            mb.Quad(pc, pc + (P(a1, q1, ph) - pc) * 0.001f, P(a1, q1, ph), P(a0, q0, ph), Vector3.up, 1);
        }
        var body = new GameObject("VolcanoBody", typeof(MeshFilter), typeof(MeshRenderer)); body.transform.SetParent(g, false);
        // 本番の形は Blender（Tools/stage2_volcano.py → Models/Stage2_Volcano.fbx。2026-10-31 てじゃ「目玉からいこ」）。材質のオブジェクト座標を使うので Static にしない
        var model = AssetDatabase.LoadAssetAtPath<Mesh>(VolcanoModel);
        var vmat = AssetDatabase.LoadAssetAtPath<Material>(VolcanoMat);
        var mesh = model && vmat ? model : SaveMesh(mb.Build("Course2_2_Volcano", 2));
        body.GetComponent<MeshFilter>().sharedMesh = mesh;
        body.GetComponent<MeshRenderer>().sharedMaterials = model && vmat ? new[] { vmat } : new[] { FuwaStage2WaterTest.ToonMat("Stage2_2_PillarRock", new Color(0.45f, 0.3f, 0.27f)), lavaMat };
        body.AddComponent<MeshCollider>().sharedMesh = mesh;
        // 火口：いつもの赤い湯気と、岩が飛び出す時の火の粉
        var top = new GameObject("Crater").transform; top.SetParent(g, false); top.localPosition = Vector3.up * (VolcanoTopH - 1.5f);
        VolcanoSmoke(top);
        var glow = FuwaStage2FireTest.FlamePS(top, "Glow", VolcanoTopR * 0.6f, 1.2f, 1.6f, new Color(1f, 0.6f, 0.2f, 0.8f), new Color(0.45f, 0.2f, 0.12f, 0f));
        { var m = glow.main; m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); var e = glow.emission; e.rateOverTime = 10f; }
        var fx = FuwaStage2FireTest.FlamePS(top, "Launch", VolcanoTopR * 0.4f, 7f, 1.0f, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.25f, 0.05f));
        {
            var m = fx.main; m.loop = false; m.duration = 0.5f; m.playOnAwake = false; m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); m.gravityModifier = 0.8f;
            var sh = fx.shape; sh.angle = 30f; var e = fx.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });
        }
        return fx;
    }

    // period：1周の秒（飛んでる間＋休み）。first：最初（火口から）の1回の長さの倍率、firstUp：その1回の高さ。launch：火口で出す火の粉
    // 火山の岩ぜんぶの時間の倍率（2026-11-04 てじゃ「火山から出てる岩をすこしゆっくりに」1→1.15）。はねる時間・1周・ずらしを同じだけのばすので、岩どうしの間合いはそのまま
    public static float RockSlow = 1.15f;
    public static float RockPassFar = 5f, RockHopUp = 1.8f;   // RockHopUp：はねる高さ 1.2→1.8（2026-11-04 てじゃ「全体的にバウンド時の高度を上げよう」）
    public static int RockPassCol = 1;   // ジグザグの最初の道（legs[0]）ではねない列。-1 で全部はねる
    static void LavaRock(Transform parent, string name, Vector3[] ps, float phase, float period, float first, float firstUp, ParticleSystem launch)
    {
        phase *= RockSlow; period *= RockSlow; float hop = RockHop * RockSlow;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var ts = new Transform[ps.Length];
        for (int i = 0; i < ps.Length; i++) { ts[i] = new GameObject("P" + i).transform; ts[i].SetParent(g, false); ts[i].localPosition = ps[i]; }
        var mover = new GameObject("Rock").transform; mover.SetParent(g, false); mover.localPosition = ps[0];
        var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere); vis.name = "Visual"; vis.transform.SetParent(mover, false); vis.transform.localScale = new Vector3(1f, 0.85f, 0.95f) * (RockR * 2f);
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        LavaRockLook(vis, name);
        var trail = FuwaStage2FireTest.FlamePS(g, "Trail", RockR * 0.7f, 0.35f, 0.5f, new Color(1f, 0.7f, 0.2f), new Color(0.85f, 0.15f, 0.05f));
        LavaRockTrail(trail);
        var f = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaLavaRock>(mover.gameObject);
        f.heat = heat; f.ball = ballProxy; f.points = ts; f.visual = vis; f.trail = trail;
        f.bounceSound = Sfx(mover, "BounceSfx", "sfx_rock_bounce2", Vector3.zero, 8f, 45f, 1f, false, 12f);
        int hops = ps.Length - 1;
        f.hopTime = hop; f.hopUp = RockHopUp; f.lastHopScale = hops > 1 ? 2f : 1f; f.firstHopScale = first; f.firstUp = firstUp; f.launchFx = launch;
        float fly = 0f; for (int k = 0; k < hops; k++) fly += hop * (k == 0 ? first : 1f) * (k == hops - 1 ? f.lastHopScale : 1f);
        f.rest = Mathf.Max(0.5f, period - fly); f.phase = phase; f.hitRadius = RockR + 0.05f;
        f.ballKnock = 2.5f; f.ballKnockUp = 1.5f; f.playerKnock = 5f; f.playerKnockUp = 4f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); FuwaStage2WaterTest.NoSync(f);
    }

    // 火のオーラ（2026-11-02 てじゃ）：火球のまわりから出る炎のかたまり。ワールド座標の粒なので、飛んだ後ろに残って小さくなって消える
    //  メッシュの炎は「板が角度を変えてついてきてる」「切り替わりでぱきっとする」でやめた
    //  最高点の火球の下側が「目の高さ＋0.2m」になるのは FireballTop（半径 0.65＋0.2＋基準の目の高さ 1.6）。作り直さずシーンだけ直す時もこれを呼ぶ
    public static float FireballAuraH = 0f;   // 0＝当たりは見えてる火球だけ（2026-11-02 てじゃ。見えない当たりはなし。上を越すのは遅くなるのでアリ）
    public static void FireballAura(FuwaFireball f)
    {
        var mover = f.transform; var old = mover.Find("Aura"); if (old) Object.DestroyImmediate(old.gameObject); var oldFx = mover.Find("AuraFx"); if (oldFx) Object.DestroyImmediate(oldFx.gameObject);
        var fx = BurnBlobs(mover, "AuraFx", FireballSize, 0f); var fm = fx.main;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f); fm.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.35f); fm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.65f); fm.maxParticles = 48; fm.gravityModifier = 0f;
        var fs = fx.shape; fs.shapeType = ParticleSystemShapeType.Sphere; fs.radius = FireballSize * 0.38f; fs.position = Vector3.zero;
        var fem = fx.emission; fem.rateOverTime = 0f; fx.Play();
        f.aura = null; f.auraFx = fx; f.auraFxRate = 48f; f.auraHeight = FireballAuraH; f.auraTopRadius = 0.25f;
    }
    // 2026-11-04 てじゃ「火球のプレイヤーのふっ飛ばしをやや弱体、飛ぶ速度をやや遅く、間隔をちょっとあける」：人 6/4.3→5/3.7、飛ぶ時間 ×1.2、休み +0.5 秒
    public static float FireballSlow = 1.2f, FireballRestAdd = 0.5f, FireballPlayerKnock = 5f, FireballPlayerKnockUp = 3.7f;
    static void Fireball(Transform parent, string name, Vector3[] hs, float flight, float rest, float phase, float top)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var ts = new Transform[hs.Length];
        for (int i = 0; i < hs.Length; i++) { ts[i] = new GameObject("Hole" + i).transform; ts[i].SetParent(g, false); ts[i].localPosition = hs[i]; }
        var mover = new GameObject("Ball").transform; mover.SetParent(g, false); mover.localPosition = hs[0];
        var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere); vis.name = "Visual"; vis.transform.SetParent(mover, false); vis.transform.localScale = Vector3.one * FireballSize;
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        vis.GetComponent<Renderer>().sharedMaterial = FireballMat();
        var trail = FuwaStage2FireTest.FlamePS(g, "Trail", 0.4f, 0.4f, 0.6f, new Color(1f, 0.75f, 0.25f), new Color(0.9f, 0.2f, 0.05f));
        FireballTrail(trail);
        var f = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaFireball>(mover.gameObject);
        f.whoosh = Sfx(mover, "WhooshSfx", "sfx_fireball_bofu2", Vector3.zero, 8f, 30f, 1f, false, 10f);
        f.heat = heat; f.ball = ballProxy; f.holes = ts; f.oneWay = ts.Length > 1; f.visual = vis; f.trail = trail; f.trailRate = FireballTrailRate; FireballAura(f);
        f.arcHeight = top; f.flightTime = flight * FireballSlow; f.rest = rest + FireballRestAdd; f.phase = phase; f.hitRadius = FireballSize * 0.5f + 0.05f; f.ballKnock = 4.8f; f.ballKnockUp = 2f; f.playerKnock = FireballPlayerKnock; f.playerKnockUp = FireballPlayerKnockUp;   // 2026-11-02 てじゃ「もうちょいつよくていいかも」
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); FuwaStage2WaterTest.NoSync(f);
    }

    // 山肌の火炎の壁：道の内側のふち edge（道の面の高さ）の所、火山から外向き。FlameLowH〜FlameHighH に縦に FlameRows 本ならんでいっしょに吹いて、当たりは 1 つの縦長の箱。
    //  2026-10-30 上下 2 本→ てじゃ「もっと上まで炎あってもいいかも」で 3 本
    //  当たると燃えて外へ吹っ飛ぶ（FuwaFireJet.ballKnock）
    // 炎の亀裂（2026-10-31 てじゃ「山に亀裂いれてそっからふきだす」）：形は Tools/stage2_volcano.py の JETS（角度と道の高さをここと合わせる）。
    //  亀裂の奥の半径をメッシュから読んで、そこから炎を出す。当たりの箱と吹っ飛び方は道のふちからのまま
    public static float VolcanoCrackR(float th, float y)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(VolcanoModel);
        if (!mesh) return -1f;
        float best = -1f;
        foreach (var v in mesh.vertices)
        {
            if (Mathf.Abs(v.y - y) > 0.8f) continue;
            if (Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, th)) > 1.2f) continue;
            float r = new Vector2(v.x, v.z).magnitude;
            if (best < 0f || r < best) best = r;
        }
        return best;
    }
    public static void JetFromCrack(ParticleSystem flame, ParticleSystem warn, float back, float len)
    {
        flame.transform.localPosition = new Vector3(0f, -back, 0f); warn.transform.localPosition = new Vector3(0f, -back, 0f);
        var m = flame.main; m.startSpeed = (len + back) / 0.7f;
    }
    // 火炎放射の炎：亀裂から伸びる細長いたまご（FlameMesh）を行ごとに 1 本。y が炎の向き。止まると FuwaFireJet の _Burn で根元から燃えつきる
    // 炎の奥行き（2026-11-02 てじゃ「もっと火の奥行きあってもいい」）：道ぞいの向きに各列を太らせて、前後にずらす。当たりと予兆の幅もそれに合わせる
    public static float JetDepthScale = 1.35f, JetZoneW = 1.6f, JetWarnW = 2.2f, JetWarnRootExt = 1f;   /* 予告の地面の幅 2→2.2（2026-11-04 てじゃ「予告線だけちょっぴり太く」「判定はかえずに」） */ static readonly float[] JetRowX = { 0f, 0f, 0f };   // 太さだけ 1.35 倍、前後ずらしはなし（2026-11-02 てじゃ「前後ずらしなしで太さだけ」）
    public static float JetShadeRound = 0.7f, JetShadeBump = 0.7f;   // 外のオレンジの陰影。VR で遠近感を出す（2026-11-02 てじゃ）
    public static float JetRowDelay = 0.07f;   // 上下 3 本の炎の出るタイミングのずれ（2026-11-02 てじゃ「ほんのちょっと」）。下から順に
    // 炎の本体（全部の列が 1 つのメッシュ）を列ごとに分ける。形はそのまま、三角形を近い列へ振り分けるだけ。作り直さずシーンだけ直す時もこれを呼ぶ（rowZ は null なら Row* の位置から）
    public static string SplitJetBody(FuwaFireJet j, float[] rowZ = null)
    {
        var body = j.flameBody; var g = body.parent; if (rowZ == null) { var rows = new List<float>(); for (int i = 0; i < g.childCount; i++) if (g.GetChild(i).name.StartsWith("Row")) rows.Add(g.GetChild(i).localPosition.z); rowZ = rows.OrderBy(z => z).ToArray(); }
        for (int i = g.childCount - 1; i >= 0; i--) if (System.Text.RegularExpressions.Regex.IsMatch(g.GetChild(i).name, @"^FlameBody\d+$")) Object.DestroyImmediate(g.GetChild(i).gameObject);
        var mf = body.GetComponent<MeshFilter>(); var full = mf.sharedMesh; string baseName = full.name.Replace("_Row0", "");
        string sourcePath = "Assets/FuwaStage2/Generated/Course2_2/" + baseName + ".asset"; var src = AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath) ?? full;   // 元の全部入りから分ける
        var verts = src.vertices; var tris = src.triangles; var lists = new List<int>[rowZ.Length]; for (int i = 0; i < lists.Length; i++) lists[i] = new List<int>();
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            float z = (verts[tris[t]].z + verts[tris[t + 1]].z + verts[tris[t + 2]].z) / 3f; int best = 0; float bd = float.MaxValue;
            for (int r = 0; r < rowZ.Length; r++) { float d = Mathf.Abs(z - rowZ[r]); if (d < bd) { bd = d; best = r; } }
            lists[best].AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
        }
        var more = new List<Transform>();
        for (int r = 0; r < rowZ.Length; r++)
        {
            var m = Object.Instantiate(src); m.name = baseName + "_Row" + r; float rx = JetRowX[r % JetRowX.Length]; var moved = (Vector3[])verts.Clone(); for (int vi = 0; vi < moved.Length; vi++) moved[vi].x = moved[vi].x * JetDepthScale + rx; m.vertices = moved; m.SetTriangles(lists[r], 0, false); m = SaveMesh(m); var wide = src.bounds; wide.Expand(new Vector3(1.4f, 0f, 0f)); m.bounds = wide; EditorUtility.SetDirty(m);   // SaveMesh は bounds を計算し直すので、炎のゆらぎの余白を戻す
            var rowT = g.Find("Row" + r); if (rowT) { var rp = rowT.localPosition; rp.x = rx; rowT.localPosition = rp; }
            if (r == 0) { mf.sharedMesh = m; continue; }
            var o = Object.Instantiate(body.gameObject, g); o.name = "FlameBody" + r; o.transform.localPosition = body.localPosition; o.transform.localRotation = body.localRotation; o.transform.localScale = Vector3.one; o.GetComponent<MeshFilter>().sharedMesh = m; o.SetActive(false); more.Add(o.transform);
        }
        j.moreBodies = more.ToArray(); j.rowDelay = JetRowDelay;
        var zone = j.GetComponent<BoxCollider>(); var zs = zone.size; zs.x = JetZoneW; zone.size = zs; EditorUtility.SetDirty(zone);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j); EditorUtility.SetDirty(j);
        return g.name + " 列" + rowZ.Length + " 三角形" + string.Join("/", lists.Select(l => l.Count / 3));
    }
    static Transform JetBody(Transform g, string name, float[] backs, float[] rowZ, float len)
    {
        float maxBack = backs.Max(), L = maxBack + len;
        var f = new FlameMeshB();
        for (int r = 0; r < backs.Length; r++)
        {
            float y0 = maxBack - backs[r] - 1f, h = L - y0, cy = y0 + h * 0.2f; var c = new Vector3(0f, 0f, rowZ[r]);
            const float R = 0.56f, RCore = 0.28f;
            f.Egg(0f, c, cy, R, (L - cy) / R, (cy - y0) / R, 16, 10, y0, h);
            f.Egg(1f, c, cy, RCore, h * 0.5f / RCore, (cy - y0) * 0.7f / RCore, 8, 8, y0, h);
            for (int b = 0; b < 2; b++) { float seed = (r * 3 + b + 0.37f) * 0.137f; f.Blob(c + Vector3.up * (L - 1.6f), seed, 0.85f); }
        }
        var mat = FlameMeshMat("Stage2_2_JetFlame", 0.33f, 1.1f, 0.1f, 0.5f, 0.45f, 0.1f, 2.6f, 3.2f, 1.3f);
        mat.SetFloat("_ShadeRound", JetShadeRound); mat.SetFloat("_ShadeBump", JetShadeBump); EditorUtility.SetDirty(mat);
        var o = new GameObject("FlameBody", typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(g, false); o.transform.localPosition = Vector3.down * maxBack;
        o.GetComponent<MeshFilter>().sharedMesh = f.Build("Course2_2_" + name + "_Body", 3f); var mr = o.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;   /* 影は赤い予兆があるのでなし（2026-11-02 てじゃ） */
        o.SetActive(false);
        return o.transform;
    }
    static void VolcanoJet(Transform parent, string name, Vector3 edge, float edgeR, float roadY, float th, float period, float on, float phase)
    {
        float a = th * Mathf.Deg2Rad;
        var o = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); var back = -new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
        float len = FlameHW * 2f + 0.5f, top = FlameHighH + 1.4f, mid = (FlameLowH + FlameHighH) * 0.5f, half = (FlameHighH - FlameLowH) * 0.5f;
        // g：上（y）が炎の向き o、前（z）がワールドの上
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = edge + o * 0.02f + Vector3.up * mid; g.localRotation = Quaternion.LookRotation(Vector3.up, o);
        var flames = new ParticleSystem[FlameRows]; var warns = new ParticleSystem[FlameRows]; var backs = new float[FlameRows]; var rowZ = new float[FlameRows];
        for (int i = 0; i < FlameRows; i++)
        {
            var mt = new GameObject("Row" + i).transform; mt.SetParent(g, false); mt.localPosition = new Vector3(0, 0, Mathf.Lerp(-half, half, i / (FlameRows - 1f)));
            // 炎の粒は口から len 先まで（寿命 0.7 秒で届く速さ）
            flames[i] = FuwaStage2FireTest.FlamePS(mt, "Flame", 0.4f, len / 0.7f, 1.0f, new Color(1f, 0.8f, 0.25f), new Color(1f, 0.3f, 0.05f));
            { var sh = flames[i].shape; sh.angle = 9f; var mm = flames[i].main; mm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f); }   // 粒は火の粉だけ。炎は JetBody
            warns[i] = FuwaStage2FireTest.FlamePS(mt, "Warn", 0.3f, 1.2f, 0.5f, new Color(1f, 0.55f, 0.2f), new Color(0.5f, 0.2f, 0.1f));
            { var mm = warns[i].main; mm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); }
            float cr = VolcanoCrackR(th, roadY + Mathf.Lerp(FlameLowH, FlameHighH, i / (FlameRows - 1f)));
            backs[i] = cr > 0f ? Mathf.Clamp(edgeR - cr + 0.1f, 0.3f, 4f) : FlameGap; rowZ[i] = mt.localPosition.z;
            JetFromCrack(flames[i], warns[i], backs[i], len);
        }
        // 当たり：道の面の少し下から上の炎の 0.6 上まで
        var zone = new GameObject("JetZone"); zone.transform.SetParent(g, false); zone.transform.localPosition = new Vector3(0, len * 0.5f, 0); zone.layer = 4;
        var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(1.2f, len, FlameHighH + 0.6f + 0.05f);
        bc.center = new Vector3(0, 0, (FlameHighH + 0.6f - 0.05f) * 0.5f - mid);
        var j = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaFireJet>(zone);
        j.roar = Sfx(g, "RoarSfx", "sfx_fire_roar", new Vector3(0f, len * 0.5f, 0f), 8f, 45f, 1f, false, 12f);   // 近くでしか聞こえなかったので、岩のはねる音と同じ広さに（2026-11-02 てじゃ）
        j.heat = heat; j.ball = ballProxy; j.phase = phase; j.period = period; j.onTime = on; j.flameRate = 14f; j.flameBody = JetBody(g, name, backs, rowZ, len); SplitJetBody(j, rowZ);
        j.flame = flames[0]; j.warn = warns[0]; j.moreFlames = flames.Skip(1).ToArray(); j.moreWarns = warns.Skip(1).ToArray();
        // 吹っ飛ぶ向き：外（山から離れる向き）、外のふちの FlameLand 手前より先へは飛ばさない（てじゃ「落ちないくらい外に吹っ飛ばす」）。
        //  2026-11-01 てじゃ「3連炎が弱い」「あたるとコースのフチからちょっぴり出るくらいのとこに飛ばそう」→ FlameLand をマイナスに（ふちの 0.25〜0.7m 外に落ちる。Play で測った式：最遠＝off＋1.07×(reach−off)。人は今までどおりふちの手前）
        //  来た方へ足すのは「やっぱ手前に飛ばされるなぁ」でやめて、「ほんのちょっとだけ手前の角度も」で外向きを来た方へ 8° くらいかたむける（2026-10-30）。ワールドの向きで入れる
        j.knockDir = parent.TransformDirection((o + back * 0.14f).normalized);
        j.ballKnock = FlameKnock; j.ballKnockUp = FlameKnockUp;
        var ko = new GameObject("KnockOrigin").transform; ko.SetParent(parent, false); ko.localPosition = edge; ko.SetParent(g, true);
        j.knockOrigin = ko; j.knockReach = FlameHW * 2f - FlameLand; j.knockPerMeter = FlameBallPerMeter; j.knockMin = FlameKnockMin;   /* 2026-11-02 てじゃ「根本ほど強く、先端ほど弱く、どこからでも道の外、外側ほど落ちる位置もやや遠く」：根元 5.4m/s でふち +0.3m、先端 0.85m/s でふち +1m */   // 試したら（上 1.5 で）1m/s で 1.15m くらい飛んだ
        // 人：地面に立っていると横の速さは地面に消されるので、しっかり上へ浮かせて飛ばす（2026-10-30 てじゃ「地面にたってるとプレイヤーがほとんど飛ばされない」）。
        //  向きは外（「プレイヤも横に飛ばしたい」）、ふわふわと同じくふちの手前まで
        j.playerPush = FlamePlayerPush; j.playerPushUp = FlamePlayerPushUp; j.playerPushDir = parent.TransformDirection((o + back * 0.14f).normalized); j.playerPerMeter = FlamePlayerPerMeter; j.playerMin = FlamePlayerMin; j.playerReach = FlameHW * 2f - FlamePlayerLand;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j); FuwaStage2WaterTest.NoSync(j);
    }

    // 水たまり：まものを落とすと倒れる・燃えたふわふわは消える。2–1 と同じ水（ふわふわは浮く・人は浅瀬で遅くなる）。s は外側の向き（-1 左、+1 右）
    static void Pond(Transform parent, string name, Vector3 c, int s)
    {
        float y = c.y; var g = new GameObject(name).transform; g.SetParent(parent, false);
        // 底（道の 0.45m 下）と、外側・前後のふち（道と同じ高さ）
        var bed = Box(g, "Bed", new Vector3(c.x, y - 0.45f - 0.3f, c.z), new Vector3(PondW, 0.6f, PondLen), rockMat, pathLayer);
        var rimO = Box(g, "RimOut", new Vector3(c.x + s * (PondW * 0.5f - 0.25f), y - 0.3f, c.z), new Vector3(0.5f, 0.6f, PondLen), rockMat, pathLayer);
        Box(g, "RimFront", new Vector3(c.x - s * 0.25f, y - 0.3f, c.z - PondLen * 0.5f + 0.25f), new Vector3(PondW - 0.5f, 0.6f, 0.5f), rockMat, pathLayer);
        Box(g, "RimBack", new Vector3(c.x - s * 0.25f, y - 0.3f, c.z + PondLen * 0.5f - 0.25f), new Vector3(PondW - 0.5f, 0.6f, 0.5f), rockMat, pathLayer);
        var under = Box(g, "Under", new Vector3(c.x, y - 0.75f - 1.0f, c.z), new Vector3(PondW - 0.1f, 1.4f, PondLen - 0.1f), rockMat, 0); Object.DestroyImmediate(under.GetComponent<Collider>());
        float wx = PondW - 0.5f, wz = PondLen - 1f; var wc = new Vector3(c.x - s * 0.25f, y, c.z);
        var water = new GameObject("Water"); water.transform.SetParent(g, false); water.transform.localPosition = wc + Vector3.up * (PondSurface - 0.5f); water.layer = 4;
        var bc = water.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(wx, 1.0f, wz);
        var surf = GameObject.CreatePrimitive(PrimitiveType.Quad); surf.name = "Surface"; surf.transform.SetParent(water.transform, false);
        surf.transform.localPosition = Vector3.up * 0.5f; surf.transform.localRotation = Quaternion.Euler(90f, 0, 0); surf.transform.localScale = new Vector3(wx, wz, 1f);
        Object.DestroyImmediate(surf.GetComponent<Collider>());
        surf.GetComponent<Renderer>().sharedMaterial = FuwaCourse2_1Builder.WaterMat(0f, false);
        var w = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWater>(water); w.surface = surf.transform; w.heat = heat;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(w); FuwaStage2WaterTest.NoSync(w);
        var sw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaShallowWater>(water);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sw); FuwaStage2WaterTest.NoSync(sw);
    }

    // 道にあいた丸い穴の水たまり（Ribbon の穴、壁は LavaDepth＋0.3 下まで）：底（道の 0.45 下）と水。2–1 と同じ水（ふわふわは浮く・人は浅瀬で遅くなる）
    //  水の箱・面は四角で穴より大きいが、上は道より下なので道の下にかくれる（道のふちから PuddleR＋0.6 以上はなす）
    static void Puddle(Transform parent, string name, Vector3 c)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        Box(g, "Bed", c + Vector3.down * (0.45f + 0.15f), new Vector3((PuddleR + PuddleWobble) * 2f + 0.1f, 0.3f, (PuddleR + PuddleWobble) * 2f + 0.1f), rockMat, pathLayer);
        float wx = (PuddleR + PuddleWobble) * 2f + 0.1f;   // ふちのふくらみまで
        var water = new GameObject("Water"); water.transform.SetParent(g, false); water.transform.localPosition = c + Vector3.up * (PondSurface - 0.5f); water.layer = 4;
        var bc = water.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(PuddleR * 1.25f, 1.0f, PuddleR * 1.25f);   // 四角の角が穴に入るくらい（1.8 だと角が穴の外で、ふちでまものが落ちて壁に埋まった。ふちをでこぼこにしたので 1.4→1.25）
        var surf = GameObject.CreatePrimitive(PrimitiveType.Quad); surf.name = "Surface"; surf.transform.SetParent(water.transform, false);
        surf.transform.localPosition = Vector3.up * 0.5f; surf.transform.localRotation = Quaternion.Euler(90f, 0, 0); surf.transform.localScale = new Vector3(wx, wx, 1f);
        Object.DestroyImmediate(surf.GetComponent<Collider>());
        surf.GetComponent<Renderer>().sharedMaterial = FuwaCourse2_1Builder.WaterMat(0f, false);
        var w = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWater>(water); w.surface = surf.transform; w.heat = heat;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(w); FuwaStage2WaterTest.NoSync(w);
        var sw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaShallowWater>(water);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sw); FuwaStage2WaterTest.NoSync(sw);
        PuddleSteam(g, c + Vector3.up * PondSurface);
    }
    public const float PuddleWobble = 0.18f;
    // まもののエリアの飾り（2026-10-31）：ふちに卵と枯れ木。床の焦げ跡と割れ目は試して「いらんかも」でやめた。
    //  床の火は置かない（このコースの床の火は火種＝ギミックなので、まぎらわしい）
    static void ArenaDeco(Transform path, System.Func<float, float, Vector3> onArena, List<Vector4> holes, Vector3 volcano)
    {
        float sg = Vector3.Dot(Flat(onArena(1f, 10f) - onArena(0f, 10f)), Flat(volcano - onArena(0f, 10f))) > 0f ? 1f : -1f;   // 山側
        // 外のふちの枯れ木（当たりなし）
        var src = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Generated/Lite/YamiDeadTree_Lite.asset"); if (!src) return;
        var tree = BurntTreeMesh(src); var bark = FuwaStage2WaterTest.ToonMat("Stage2_2_BurntBark", new Color(0.2f, 0.15f, 0.15f));
        // 外のふちの卵（2026-10-31 てじゃ「コースの端に卵とか置いとくか、ちょっと色変えて」）：まもののすと同じ巣の卵セットの色ちがい。手前の枯れ木は画面のまん中で目立ちすぎたのでやめた
        var eggMesh = FuwaMamonoDeco.EggSets(new[] { onArena(-sg * 5f, 8.2f), onArena(-sg * 5.2f, 11f), onArena(sg * 4.9f, 13.7f) } /* 2つ目は岩のはねる所から離した（2026-11-02 てじゃ「岩が卵に直撃してる」。前は -4.9, 14.6） */, new[] { new Color(0.95f, 0.45f, 0.22f), new Color(0.98f, 0.7f, 0.28f), new Color(0.72f, 0.2f, 0.16f) }, MeshDir22 + "/Course2_2_ArenaEggs.asset");
        var eg = new GameObject("ArenaEggs", typeof(MeshFilter), typeof(MeshRenderer)); eg.transform.SetParent(path, false); eg.GetComponent<MeshFilter>().sharedMesh = eggMesh;
        var er = eg.GetComponent<MeshRenderer>(); er.sharedMaterial = AssetDatabase.FindAssets("Den_Twig t:Material").Select(g2 => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g2))).First(m => m.name == "Den_Twig"); er.receiveShadows = false;
        foreach (var q in new[] { (z: 21f, yaw: 200f, s: 0.95f) })
        {
            var o = new GameObject("ArenaTree", typeof(MeshFilter), typeof(MeshRenderer)).transform; o.SetParent(path, false);
            o.localScale = Vector3.one * q.s; o.localRotation = Quaternion.Euler(0f, q.yaw, 0f);
            o.localPosition = onArena(-sg * 5.35f, q.z) - o.localRotation * (new Vector3(-1.6f, -1.42f, 0f) * q.s);
            o.GetComponent<MeshFilter>().sharedMesh = tree; var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = bark; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
    // 水たまりの湯気：火山のそばの水なので、白いもくもくを少しだけ
    public static void PuddleSteam(Transform g, Vector3 at)
    {
        var ps = FuwaStage2FireTest.FlamePS(g, "Steam", PuddleR * 0.6f, 0.5f, 2.6f, Color.white, Color.white);
        ps.transform.localPosition = at;
        var m = ps.main; m.startColor = new Color(1f, 1f, 1f, 0.55f); m.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.95f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); m.maxParticles = 10; m.prewarm = true;
        var em = ps.emission; em.rateOverTime = 3f;
        var sh = ps.shape; sh.angle = 18f;
        var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));
        var ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 4; ts.numTilesY = 4; ts.animation = ParticleSystemAnimationType.WholeSheet; ts.frameOverTime = new ParticleSystem.MinMaxCurve(0f); ts.startFrame = new ParticleSystem.MinMaxCurve(0f, 15f / 16f);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = SteamMat();
        ps.Play();
    }

    // ほのおのまもの：コースのまもの（FuwaEnemy、寝てない・巣にかくれない物）を写して、赤く・炎をまとわせる。突進で燃える・水で倒れる
    // まものの目は 6〜9cm の球なので軽い球に（2026-11-01 てじゃ「無駄なハイポリ」。768→224 三角形）
    public static void LowEyes(Transform monster)
    {
        var low = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/LowSphere.asset"); if (!low) return;
        foreach (var mf in monster.GetComponentsInChildren<MeshFilter>(true))
            if ((mf.name == "EyeWhite" || mf.name == "Pupil") && mf.sharedMesh && mf.sharedMesh.name == "Sphere") mf.sharedMesh = low;
    }
    static void FireMonster(Transform root, string name, Vector3 lp)
    {
        var src = Object.FindObjectsOfType<FuwaEnemy>(true).FirstOrDefault(e => !e.sleeping && !e.hideInNest && e.transform.root.name.StartsWith("FuwaCourse") && !e.transform.root.name.StartsWith("FuwaCourse2") && e.ball != null);
        if (!src) { Debug.LogWarning("[2–2] まもののお手本がない"); return; }
        // お手本の足元の道からの高さ
        float lift = 0.5f;
        if (Physics.Raycast(src.transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, 10f, 1 << 11, QueryTriggerInteraction.Ignore)) lift = src.transform.position.y - hit.point.y;
        var g = Object.Instantiate(src.gameObject, root, false); g.name = name;
        LowEyes(g.transform);
        g.transform.localPosition = lp + Vector3.up * lift; g.transform.localRotation = Quaternion.LookRotation(Vector3.back);
        g.transform.localScale = src.transform.lossyScale;
        var e = g.GetComponent<FuwaEnemy>(); e.ball = ballProxy; e.igniteHeat = heat; e.flameTouchRadius = MonsterFlameTouch;
        // 赤く（水で消火されたら元の色に戻すので、差しかえた所と元の材質を覚える）
        var tR = new List<Renderer>(); var tS = new List<int>(); var tF = new List<Material>(); var tP = new List<Material>();
        if (e.visual)
            foreach (var r in e.visual.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (!ms[i] || !ms[i].shader.name.Contains("Fur")) continue;   // 毛だけ（足元の影などはそのまま）
                    string mp = "Assets/FuwaStage2/Materials/Stage2_2_FireMonster_" + r.name + "_" + i + ".mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                    if (!m) { m = new Material(ms[i]); AssetDatabase.CreateAsset(m, mp); }
                    else { m.shader = ms[i].shader; m.CopyPropertiesFromMaterial(ms[i]); }
                    foreach (var prop in new[] { "_Color", "_MainColor", "_BaseColor" }) if (m.HasProperty(prop)) m.SetColor(prop, MonsterCol);
                    if (m.HasProperty("_TipColor")) m.SetColor("_TipColor", MonsterTipCol);
                    EditorUtility.SetDirty(m);
                    tR.Add(r); tS.Add(i); tF.Add(m); tP.Add(ms[i]);
                    ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
        // 炎をまとう
        var ps = MonsterBurnLook(e, g.transform);
        // 水に落ちた時の「ジュッ」の湯気（1回だけぶわっ）
        var st = FuwaStage2FireTest.FlamePS(g.transform, "Steam", 0.3f, 1.6f, 1.1f, new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.88f, 0.92f, 0f));
        {
            var m = st.main; m.loop = false; m.playOnAwake = false; m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); m.maxParticles = 40;
            var em = st.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22), new ParticleSystem.Burst(0.15f, 10) });
            st.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        e.steamSound = Sfx(g.transform, "SteamSfx", "sfx_enemy_steam", Vector3.zero, 3f, 30f, 0.9f);
        e.flameFx = ps; e.steamFx = st; e.floatSink = -0.03f; e.floatBob = 0.05f; e.floatTilt = 25f; e.floatMaxR = PuddleR - 0.65f;
        e.tintRenderers = tR.ToArray(); e.tintSlots = tS.ToArray(); e.fireMats = tF.ToArray(); e.plainMats = tP.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(e);
        // ふわふわのまもの一覧に
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault();
        if (ball)
        {
            var so = new SerializedObject(ball); var arr = so.FindProperty("enemies");
            for (int k = arr.arraySize - 1; k >= 0; k--) if (arr.GetArrayElementAtIndex(k).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(k);
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = e;
            so.ApplyModifiedProperties(); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ball);
        }
    }

    // ---- 部品 ----
    static Material PathMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/" + name.Replace("2_2", "2_1") + ".mat");
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
    // シマシマの道：a（手前のはしのまん中、上の面の高さ）から dir へ len。当たりは1枚の箱、見た目は StripeLen ごとに A/B
    static void Strip(Transform parent, string name, Vector3 a, Vector3 dir, float len, float hw, bool walkOnly = false)
    {
        if (len <= 0.01f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = a; g.localRotation = Quaternion.LookRotation(dir);
        const float T = 0.6f;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = walkOnly ? 0 : pathLayer;
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -T * 0.5f, len * 0.5f); bc.size = new Vector3(hw * 2f, T, len);
        if (walkOnly) TopOut(col);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            var s = Box(g, "S" + k, new Vector3(0, -T * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, T, l), (k % 2 == 0) ? pathA : pathB, 0);
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        // 下の岩（道の下 1.5m）
        var u = Box(g, "Under", new Vector3(0, -T - 0.75f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.5f, len - 0.1f), rockMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }
    // 段のはしの岩の壁（下の段の高さ y から h 上まで、上の段の道の下に入る）
    static void Wall(Transform parent, string name, Vector3 at, float h, float hw)
    {
        var w = Box(parent, name, at + new Vector3(0, h * 0.5f - 0.3f, 0.25f), new Vector3(hw * 2f, h - 0.6f + 0.3f, 0.5f), rockMat, 0);
    }
    // 人だけの坂（a→b、上の面の高さ）。上にふわふわが乗ったらアウト
    static void Ramp(Transform parent, string name, Vector3 a, Vector3 b, float w)
    {
        var d = b - a; float len = d.magnitude;
        var g = Box(parent, name, (a + b) * 0.5f - Vector3.up * 0.15f, new Vector3(w, 0.3f, len), pathB, 0);
        g.transform.localRotation = Quaternion.LookRotation(d);
        TopOut(g);
    }
    // 人だけの細い橋（木の板の色、上にふわふわが乗ったらアウト）
    static void Bridge(Transform parent, string name, Vector3 a, Vector3 b)
    {
        var d = b - a; float len = d.magnitude;
        var wood = FuwaStage2WaterTest.ToonMat("Stage2_2_Wood", new Color(0.62f, 0.45f, 0.32f));
        var g = Box(parent, name, (a + b) * 0.5f - Vector3.up * 0.1f, new Vector3(WalkW, 0.2f, len), wood, 0);
        g.transform.localRotation = Quaternion.LookRotation(d);
        TopOut(g);
    }
    static void TopOut(GameObject g)
    {
        var to = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTopOut>(g);
        if (to) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(to); FuwaStage2WaterTest.NoSync(to); }
    }
    static void Hazard(Transform root, string name, Vector3 lp, Vector3 size, bool player, Quaternion? rot = null)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.transform.localPosition = lp; g.layer = player ? 0 : 4;
        if (rot.HasValue) g.transform.localRotation = rot.Value;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        if (player) { var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(g); pc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc); }
        else { var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(g); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    // スタート：コース1の Start を丸ごと写す（2–1 と同じ）。キノコの島の前にもう1本の木の橋。橋のはし（コースの道の始まり）の z を返す
    static float StartSet(Transform root)
    {
        var start = new GameObject("Start").transform; start.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–2] FuwaCourse/Start がない"); return 0f; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_2");
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea");
        var ground = area.Find("Ground"); var bridge = area.Find("Bridge"); var bridgeWood = area.Find("BridgeWood");
        var bc = bridge.GetComponent<BoxCollider>();
        float halfLen = (bc ? bc.size.z : 1f) * bridge.localScale.z * 0.5f;
        float frontZ = 2f * ground.localPosition.z - bridge.localPosition.z;
        // 島の地面の中心をコースの原点に
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
    static void GoalSet(Transform root, Vector3 p)
    {
        var goal = new GameObject("Goal").transform; goal.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–2] FuwaCourse/Goal がない"); return; }
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
        goal.localPosition = p + Vector3.forward * (far - 0.3f) + Vector3.up * 0.02f;
        FuwaCourse2_1Builder.FixCopied(goal, map);
        FuwaCourse2_1Builder.Stage2Grass(goal);
    }

    // チェックポイント：ステージ1の FuwaCourse3/Checkpoint1（光るキノコ＋丸太、通り抜けの判定）を写す。門の柱・横木は消す
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
        // てじゃが手で置いた位置（2026-11-01）。判定はキノコの位置に合わせる
        if (CpHand.TryGetValue(name, out var hand)) { if (hand.pos.HasValue) cp.transform.localPosition = hand.pos.Value; FitCheckpoint(cp.transform, hand.l, hand.r); }
        var fc = cp.GetComponentInChildren<FuwaCheckpoint>(true);
        if (fc) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc); cps.Add(fc); }
    }
    static readonly Dictionary<string, (Vector3? pos, Vector2 l, Vector2 r)> CpHand = new Dictionary<string, (Vector3? pos, Vector2 l, Vector2 r)> {
        { "Checkpoint1", (new Vector3(-1.13f, 4.6f, 30.75f), new Vector2(-3.04f, 1.25f), new Vector2(3.13f, 1.17f)) },
        { "Checkpoint3", (new Vector3(0.626f, 12.6f, 86.647f), new Vector2(-4.215f, 1.618f), new Vector2(3.793f, 1.355f)) },
        { "Checkpoint4", (null, new Vector2(-3.44f, 1.46f), new Vector2(3.704f, 0.868f)) } };
    // 左右のキノコ（x, z）の位置に、判定の位置・幅・向きと再開位置を合わせる。幅はキノコの間 - 0.3（ほかのチェックポイントと同じ割合）
    public static void FitCheckpoint(Transform cp, Vector2 l, Vector2 r)
    {
        var gm = cp.Find("GlowMushrooms");
        if (gm) { var a = gm.Find("ClusterL"); var b = gm.Find("ClusterR"); if (a) a.localPosition = new Vector3(l.x, a.localPosition.y, l.y); if (b) b.localPosition = new Vector3(r.x, b.localPosition.y, r.y); }
        var mid = (l + r) * 0.5f; var d = r - l;
        var trig = cp.Find("Trigger");
        if (trig)
        {
            trig.localPosition = new Vector3(mid.x, trig.localPosition.y, mid.y);
            trig.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
            trig.localScale = new Vector3(d.magnitude - 0.3f, trig.localScale.y, trig.localScale.z);
            EditorUtility.SetDirty(trig);
        }
        var rp = cp.Find("RespawnPoint");
        if (rp) { rp.localPosition = new Vector3(mid.x - 0.02f, rp.localPosition.y, mid.y - 0.6f); EditorUtility.SetDirty(rp); }
    }
    public static void FitHandPlacedCheckpoints(Transform cps) { foreach (var kv in CpHand) { var cp = cps.Find(kv.Key); if (cp) FitCheckpoint(cp, kv.Value.l, kv.Value.r); } }
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
