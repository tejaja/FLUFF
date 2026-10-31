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
    public static float WideHW = 4.5f, WideEndHW = 3.2f, HoleR = 1.2f, LavaDepth = 0.45f, FireballTop = 2.7f, VertTop = 2.6f, FireballSize = 1.3f;   // ② 広い道・溶岩の穴・火の玉（てっぺんの道からの高さ・大きさ）。2026-10-30 てじゃ「穴の溶岩もうちょい上に」→ 0.9/0.5 → 0.45/0.35（火の玉のてっぺんは道からの高さで据え置き）
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
    public static float FlameHW = 3f, FlameRise = 3f, FlameGap = 0.9f, FlameKnock = 6f, FlameKnockUp = 1.5f, FlameLand = 1.2f, FlameLowH = 0.55f, FlameHighH = 2.95f;   // 上 6→3→1.5（てじゃ「上に飛ばしすぎかな」「まだ上にとびすぎ」）   // FlameLand：吹っ飛んで落ちる所の、外のふちからの残り
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
        heat.burnAfter = 4f;   // 浮いてから燃えだすまで（6→4、てじゃ「燃えるまでの時間も入り口付近のも含めてもうちょい短く」）
        heat.burnFx = FuwaStage2FireTest.FlamePS(hg.transform, "BurnFx", 0.25f, 1.5f, 0.6f, new Color(1f, 0.6f, 0.15f), new Color(0.9f, 0.15f, 0.05f));
        { var m = heat.burnFx.main; m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f); }
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
        Ribbon(path, "Path12", r12, hw12, pts12, pools, lavaRoot);

        // 火種1・2：角の丸みのまん中（corners12 は角ごと、[0] は橋の先のちょっとした角）
        GroundFire(fire, "Seed1", corners12[1]);
        GroundFire(fire, "Seed2", corners12[3]);
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
            var fall = Box(lavaRoot, "LavaFall" + k, hc + od * (len - 0.12f) + Vector3.down * (ld + LavaFallLen * 0.5f + 0.04f), new Vector3(ChanHW * 2f, LavaFallLen, 0.06f), lavaMat, 0);   // 上のふたは溝の溶岩の下へ（上に出ると段に見えた）
            fall.transform.localRotation = rot; Object.DestroyImmediate(fall.GetComponent<Collider>());
            LavaFallLook(fall);
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
        Checkpoint(root, "Checkpoint1", new Vector3(0, yL, z0 + 59f), Vector3.forward, WideEndHW);

        // ③ 鋭角のグネグネ（登り）：丸い踊り場（角）の火種で止めて切り返す。角は約155° 曲がる（道どうしが約26°）
        //  はしの踊り場（Pad0・Pad4）は広い道がつながるので大きく（EndPadR）、広い道のはしが踊り場の中におさまる所に
        var pad = new Vector3(0, yL, z0 + 60f + EndPadIn);
        Pad(path, "Pad0", pad, EndPadR);
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
            ZigBoard(path, "Zig" + k, a0, b0, pad, k == 0 ? EndPadR : PadR, nx, k == 3 ? EndPadR : PadR, ZigHW);
            Pad(path, "Pad" + (k + 1), nx, k == 3 ? EndPadR : PadR);
            if (k < 3) GroundFire(fire, "ZigSeed" + k, nx);
            pads.Add((nx, k == 3 ? EndPadR : PadR));
            pad = nx;
        }
        // 1個とばしで並んだ踊り場のまん中を横切る炎の壁（ふわふわが入ると即黒焦げ）：飛ばし越すショートカットを防ぐ
        //  （てじゃ「ショートカット防止に炎の壁でも敷いとくか、即黒焦げの」→ 踊り場の間に縦に置いたら「そこじゃなくてこの青ライン」＝V字のすき間を横切る向き）
        //  踊り場の列の外（FireWallOut）から、板の V の根元ギリギリ（板・踊り場のふちまで FireWallGap）まで（てじゃ「もっとV字の根本ギリまでせめても」）
        for (int k = 0; k + 2 < pads.Count; k++)
        {
            Vector3 a = pads[k].c, b = pads[k + 2].c, c = pads[k + 1].c, mid = (a + b) * 0.5f;
            var d = Flat(b - a).normalized; var n = Vector3.Cross(Vector3.up, d);
            if (Vector3.Dot(Flat(c - mid), n) < 0f) n = -n;   // n＝板の V の内向き
            float lim = 0.25f + FireWallGap, s = 0f;   // 0.25＝燃える箱の厚みの半分
            while (s < 30f)
            {
                var e = Flat(mid + n * (s + 0.05f));
                if (SegDist(e, Flat(legs[k].a), Flat(legs[k].b)) < ZigHW + lim || SegDist(e, Flat(legs[k + 1].a), Flat(legs[k + 1].b)) < ZigHW + lim || (e - Flat(c)).magnitude < PadR + lim) break;
                s += 0.05f;
            }
            FireWall(fire, "FireWall" + k, mid - n * (PadR + FireWallOut), mid + n * s);
        }

        // 火の岩：コースの外の火山（ジグザグのてっぺんの右奥に、下からそびえる）の火口から飛んできて、坂の下る向きに板を横切ってはねながら落ちる。板のまん中を1列
        var vc = pad + VolcanoAt; var vtop = vc + up * (VolcanoTopH - 0.6f);
        var vfx = Volcano(fire, vc);
        CourseEmbers(fire, lavaRoot, pad, vc);
        BurntIsles(fire, lavaRoot);
        //  （2026-10-30 てじゃ「道に沿って落ちるとこの狭さじゃ回避できない、この方向に３個くらい列ずらしで落とす、ちょうどいい位置でバウンドして落ちてくる感じ」）
        {
            float x0 = legs[0].a.x, x1 = legs[0].b.x;
            float[] cols = { 0.5f };   // 1列（3列は「きついか」→「やっぱ３列＋予告線」→「やっぱむずすぎる、３列やめとこう」）。予告（印・点線）は「びみょう」でなし
            for (int c = 0; c < cols.Length; c++)
            {
                float cx = Mathf.Lerp(x0, x1, cols[c]);
                var rp = new List<Vector3>();
                for (int k = legs.Count - 1; k >= 0; k--)
                {
                    float t = (cx - legs[k].a.x) / (legs[k].b.x - legs[k].a.x);
                    rp.Add(Vector3.Lerp(legs[k].a, legs[k].b, t) + up * RockR);
                   
                }
                rp.Insert(0, vtop);   // 火山の火口から飛んでくる（岩柱→2026-10-30 てじゃ「コース外にある火山から飛んでくる感じに」）
                rp.Add(rp[rp.Count - 1] + new Vector3(0, -10f, -4f));
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
        var r5 = Route(pts5, rad5, new List<Vector3>());
        // 曲がった道の上の (x 横, z 奥行き) の所
        Vector3 OnArena(float x, float z) { Frame(r5, ar.z + z, out var c, out var t); return c + Vector3.Cross(Vector3.up, t).normalized * x; }
        var pholes = puddles.Select(q => { var c = OnArena(q.x, q.z); return new Vector4(c.x, c.y, c.z, PuddleR); }).ToList();
        Ribbon(path, "Path5", r5, hw5, pts5, pholes.Select(v => new HolePool { c = new List<Vector3> { (Vector3)v }, rr = v.w, ld = LavaDepth }).ToList());
        for (int k = 0; k < pholes.Count; k++) Puddle(path, "Puddle" + k, (Vector3)pholes[k]);
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
            Checkpoint(root, "Checkpoint2", Around(Th0 + 2f, RoadR(Th0 + 2f, 0f), 0f), new Vector3(Mathf.Cos(a0), 0f, -Mathf.Sin(a0)), FlameHW);
        }
        // 山肌から火炎の壁 3 枚（てじゃ「交互ばっかだとシステマチック」→ 周期・長さをばらばらに）。横（道を横切る）に縦に並んだ炎がいっしょに吹く。
        //  2026-10-30 斜め下→「斜めいらんかも」、低い・高いの交互→「いまいち」「上下セットの壁を3枚って形がいいかな」
        var jets = new[] { (th: -30f, per: 3.4f, on: 1.4f, ph: 0f), (th: -8f, per: 3.0f, on: 1.2f, ph: 0.9f), (th: 14f, per: 3.8f, on: 1.6f, ph: 1.9f) };
        for (int k = 0; k < jets.Length; k++)
        {
            var jt = jets[k]; float y = RoadY(jt.th);
            VolcanoJet(fire, "Flamethrower" + k, Around(jt.th, RoadR(jt.th, y) - FlameHW, y), RoadR(jt.th, y) - FlameHW, y, jt.th, jt.per, jt.on, jt.ph);
        }
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
        FuwaStage2LobbyCourses.Run();
        FuwaCourse2_1Builder.FixNetworkIDs();
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
        public Mesh Build(string name, int subs)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.subMeshCount = subs;
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
    public static float PoolWobble = 0.2f, PoolNeck = 0.3f;
    const int LavaShadowLayer = 14;   // PickupNoEnvironment：ふわふわ（17）・人（9, 10）と当たらない
    public const float BurnSink = 0.1f;   // ふわふわが黒焦げになる高さ：溶岩の面からこれだけ下に玉の底が入った時（2026-10-31 てじゃ「やたら高い位置でアウト」で 0.2 に→「ちょっぴり上げても」で 0.1。前は溶岩の 0.2 上だった）
    class HolePool
    {
        public List<Vector3> c = new List<Vector3>(); public float rr, amp, ld, seed; public int sx; public List<Vector3> chanAt = new List<Vector3>();
        public float Sd(Vector3 p)
        {
            float best = float.MaxValue;
            if (c.Count == 1) best = Flat(p - c[0]).magnitude - rr;
            for (int k = 0; k + 1 < c.Count; k++)
            {
                var ab = Flat(c[k + 1] - c[k]); float t = Mathf.Clamp01(Vector3.Dot(Flat(p - c[k]), ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Flat(p - c[k] - ab * t).magnitude - rr * (1f - PoolNeck * Mathf.Sin(t * Mathf.PI)));
            }
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
    static void Ribbon(Transform parent, string name, List<RPt> r, float[] hw, Vector3[] pts, List<HolePool> holes = null, Transform lavaParent = null)
    {
        const float T = 0.6f, U = 1.5f, In = 0.1f;
        int n = r.Count;
        var L = new Vector3[n]; var R = new Vector3[n]; var Li = new Vector3[n]; var Ri = new Vector3[n]; var side = new Vector3[n]; var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            var t = Flat(r[Mathf.Min(i + 1, n - 1)].p - r[Mathf.Max(i - 1, 0)].p).normalized;
            side[i] = Vector3.Cross(Vector3.up, t).normalized;
            float w = WAt(pts, hw, r[i].u);
            R[i] = r[i].p + side[i] * w; L[i] = r[i].p - side[i] * w;
            Ri[i] = r[i].p + side[i] * (w - In); Li[i] = r[i].p - side[i] * (w - In);
            if (i > 0) s[i] = s[i - 1] + Flat(r[i].p - r[i - 1].p).magnitude;
        }
        var vis = new MB(); var col = new MB(); var lava = new MB(); var dn = Vector3.down * T; var du = Vector3.down * (T + U);
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
                    if (h.tip < 0) { lava.Quad(P0(h.c0 - h.hl0 - Over) + ld, P1(h.c1 - h.hl1 - Over) + ld, P1(h.c1 + h.hr1 + Over) + ld, P0(h.c0 + h.hr0 + Over) + ld, Vector3.up, 0); continue; }
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
        MeshObj(parent, name, vis.Build("Course2_2_" + name, 3), col.Build("Course2_2_" + name + "_Col", 1), new[] { pathA, pathB, rockMat });
        if (lavaParent != null)
        {
            // うその影（FuwaGroundMarker）は真下へ線を飛ばすので、溶岩にも当たりがいる（無いと穴の上で影が消える）。ふわふわも人も通りぬけるレイヤーにする
            var lp = MeshObj(lavaParent, "LavaPools", lava.Build("Course2_2_LavaPools", 1), lava.Build("Course2_2_LavaPools_Col", 1), new[] { lavaMat });
            lp.layer = LavaShadowLayer;
            foreach (var mk in Object.FindObjectsOfType<FuwaGroundMarker>(true))
            {
                mk.groundMask |= 1 << LavaShadowLayer; EditorUtility.SetDirty(mk);
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(mk);
            }
        }
    }

    // 丸い踊り場（ジグザグの角）。上の面は道より PadDrop 下（道の板のはしが乗る＝同じ高さで重ならない）
    static void Pad(Transform parent, string name, Vector3 c, float PadR)
    {
        const float T = 0.6f, U = 1.5f; const int N = 40;
        c.y -= PadDrop;
        var vis = new MB(); var col = new MB(); var dn = Vector3.down * T; var du = Vector3.down * (T + U);
        for (int k = 0; k < N; k++)
        {
            float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
            var o0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var o1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            var p0 = c + o0 * PadR; var p1 = c + o1 * PadR; var q0 = c + o0 * (PadR - 0.1f); var q1 = c + o1 * (PadR - 0.1f);
            var om = (o0 + o1).normalized;
            foreach (var mb in new[] { vis, col })
            {
                if (mb == vis)
                {
                    float kb = (PadR - Bevel) / PadR; var i0 = c + (p0 - c) * kb; var i1 = c + (p1 - c) * kb;
                    mb.Quad(c, c, i1, i0, Vector3.up, 0);
                    BevelEdge(mb, p0, p1, o0, o1, 1);
                    mb.Quad(p0 + BevelDown, p1 + BevelDown, p1 + dn, p0 + dn, om, 1);
                    continue;
                }
                mb.Quad(c, c, p1, p0, Vector3.up, 0);
                mb.Quad(p0, p1, p1 + dn, p0 + dn, om, 0);
            }
            col.Quad(c + dn, c + dn, p1 + dn, p0 + dn, Vector3.down, 0);
            vis.Quad(c + dn, c + dn, p1 + dn, p0 + dn, Vector3.down, 2);
        }
        var g = MeshObj(parent, name, vis.Build("Course2_2_" + name, 3), col.Build("Course2_2_" + name + "_Col", 1), new[] { pathA, pathB, rockMat });
        // 下は他のコースと同じ浮島の形（2026-10-31、前はただの筒）。見た目だけ
        var under = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/FarIslands/IslandUnderLow_2.asset");
        if (under)
        {
            var u = new GameObject("Under", typeof(MeshFilter), typeof(MeshRenderer)).transform; u.SetParent(g.transform, false);
            float sx = (PadR - 0.08f) * 2f / Mathf.Max(under.bounds.size.x, under.bounds.size.z), depth = PadR * 0.85f, sy = depth / under.bounds.size.y;
            u.localScale = new Vector3(sx, sy, sx); u.localRotation = Quaternion.Euler(0f, name.GetHashCode() % 360, 0f);
            u.localPosition = new Vector3(c.x, c.y - T + 0.02f - under.bounds.max.y * sy, c.z);
            u.GetComponent<MeshFilter>().sharedMesh = under; u.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
        }
    }
    // ジグザグの板（2026-10-31、前は箱を踊り場に乗せただけで段があった）：両はしは踊り場の上の面の高さで平らに中まで入り、踊り場のふちから先だけ坂。角はなめらか
    static void ZigBoard(Transform parent, string name, Vector3 a0, Vector3 b0, Vector3 ca, float ra, Vector3 cb, float rb, float hw)
    {
        const float T = 0.6f, In = 0.1f, Down = 0.005f, Ease = 1.2f;
        var d = Flat(b0 - a0); float len = d.magnitude; d /= len; var side = Vector3.Cross(Vector3.up, d).normalized; var o = Flat(a0);
        float sA = 0f, sB = len;
        while (sA < len && Flat(o + d * sA - ca).magnitude < ra) sA += 0.02f;
        while (sB > 0f && Flat(o + d * sB - cb).magnitude < rb) sB -= 0.02f;
        float yA = ca.y - PadDrop - Down, yB = cb.y - PadDrop - Down, span = sB - sA, e = Mathf.Min(Ease / span, 0.3f), peak = 1f / (1f - e);
        float Y(float s)
        {
            float t = Mathf.Clamp01((s - sA) / span);
            float f = t < e ? peak * t * t / (2f * e) : t > 1f - e ? 1f - peak * (1f - t) * (1f - t) / (2f * e) : peak * (t - e * 0.5f);
            return Mathf.Lerp(yA, yB, f);
        }
        float s0 = sA - hw * 1.3f, s1 = sB + hw * 1.3f; int n = Mathf.CeilToInt((s1 - s0) / (StripeLen * 0.25f));
        var vis = new MB(); var col = new MB(); var dn = Vector3.down * T;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Lerp(s0, s1, i / (float)n), b = Mathf.Lerp(s0, s1, (i + 1) / (float)n);
            Vector3 P(float s, float x) { var q = o + d * s + side * x; q.y = Y(s); return q; }
            int st = Mathf.Abs(Mathf.FloorToInt((a + b) * 0.5f / StripeLen)) % 2;
            col.Quad(P(a, -hw), P(b, -hw), P(b, hw), P(a, hw), Vector3.up, 0);
            col.Quad(P(a, hw), P(b, hw), P(b, hw) + dn, P(a, hw) + dn, side, 0);
            col.Quad(P(a, -hw), P(b, -hw), P(b, -hw) + dn, P(a, -hw) + dn, -side, 0);
            col.Quad(P(a, -hw) + dn, P(b, -hw) + dn, P(b, hw) + dn, P(a, hw) + dn, Vector3.down, 0);
            vis.Quad(P(a, -hw + Bevel), P(b, -hw + Bevel), P(b, hw - Bevel), P(a, hw - Bevel), Vector3.up, st);
            BevelEdge(vis, P(a, hw), P(b, hw), side, side, st); BevelEdge(vis, P(a, -hw), P(b, -hw), -side, -side, st);
            vis.Quad(P(a, hw) + BevelDown, P(b, hw) + BevelDown, P(b, hw) + dn, P(a, hw) + dn, side, st);
            vis.Quad(P(a, -hw) + BevelDown, P(b, -hw) + BevelDown, P(b, -hw) + dn, P(a, -hw) + dn, -side, st);
            float w = hw - In;
            Skirt(vis, P(a, w) + dn, P(b, w) + dn, P(a, -w) + dn, P(b, -w) + dn, side);
            vis.Quad(P(a, hw) + dn, P(b, hw) + dn, P(b, w) + dn, P(a, w) + dn, Vector3.down, 2);
            vis.Quad(P(a, -hw) + dn, P(b, -hw) + dn, P(b, -w) + dn, P(a, -w) + dn, Vector3.down, 2);
        }
        MeshObj(parent, name, vis.Build("Course2_2_" + name, 3), col.Build("Course2_2_" + name + "_Col", 1), new[] { pathA, pathB, rockMat });
    }

    // 火種：地面の燃えさし＋炎＋熱の範囲（かがり火と同じ FuwaHeatSource、止まる所は地面の SeedHover 上）
    static void GroundFire(Transform parent, string name, Vector3 lp)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = lp;
        var ember = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ember.name = "Embers"; ember.transform.SetParent(g, false);
        ember.transform.localPosition = new Vector3(0, 0.02f, 0); ember.transform.localScale = new Vector3(1.3f, 0.02f, 1.3f);
        Object.DestroyImmediate(ember.GetComponent<Collider>());
        ember.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_2_Embers", new Color(0.35f, 0.12f, 0.08f));
        var ps = FuwaStage2FireTest.FlamePS(g, "Flame", 0.5f, 1.4f, 0.9f, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.08f));
        { var em = ps.emission; em.rateOverTime = 45f; var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); }
        var z = new GameObject("HeatZone"); z.transform.SetParent(g, false); z.transform.localPosition = new Vector3(0, 0.9f, 0); z.layer = 4;
        var bc = z.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(2.0f, 1.8f, 2.0f);
        var hp = new GameObject("HoverPoint").transform; hp.SetParent(g, false); hp.localPosition = new Vector3(0, SeedHover, 0);
        var hs = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHeatSource>(z); hs.heat = heat; hs.hoverPoint = hp;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hs); FuwaStage2WaterTest.NoSync(hs);
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

    // 噴気孔：道の面の黒い穴＋光るふち＋斜め上へ吹き出す湯気。ふわふわが入ると target（火種の上）へ斜めに打ち出す。
    // 飛び方（重力 1.5・空気抵抗：打ち出しから 1 秒は 0.3、そのあと 1.2）を計算して、8.4m 先・1.6m 上の火種の熱の範囲の 0.8〜1.4m の高さを通る速さ（水平 6・上 3.5）。距離に合わせて水平の速さを伸ばす
    static void BlastVent(Transform parent, string name, Vector3 at, Vector3 target)
    {
        var d = Flat(target - at); float dist = d.magnitude; d /= Mathf.Max(0.01f, dist);
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = at; g.localRotation = Quaternion.LookRotation(d);
        var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); hole.name = "Hole"; hole.transform.SetParent(g, false);
        hole.transform.localPosition = new Vector3(0, 0.02f, 0); hole.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
        Object.DestroyImmediate(hole.GetComponent<Collider>());
        hole.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_2_Crack", new Color(0.18f, 0.08f, 0.07f));
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rim.name = "Glow"; rim.transform.SetParent(g, false);
        rim.transform.localPosition = new Vector3(0, 0.035f, 0); rim.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
        Object.DestroyImmediate(rim.GetComponent<Collider>());
        rim.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_2_CrackGlow", new Color(1f, 0.45f, 0.12f));
        Vector3 launch = new Vector3(0, VentUp, VentFwd * dist / 8.4f);
        // 湯気（打ち出す向きへ）
        var fo = new GameObject("Steam"); fo.transform.SetParent(g, false); fo.transform.localPosition = new Vector3(0, 0.05f, 0);
        fo.transform.localRotation = Quaternion.LookRotation(launch.normalized);
        var ps = fo.AddComponent<ParticleSystem>();
        var main = ps.main; main.startLifetime = 1.1f; main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 6f); main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.92f, 0.7f), new Color(1f, 0.82f, 0.65f, 0.5f));
        main.maxParticles = 200; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -0.05f;
        var em = ps.emission; em.rateOverTime = 70f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = 0.35f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1.2f; lim.dampen = 0.08f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.2f)));
        fo.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        ps.Play();
        // 打ち出し
        var z = new GameObject("Blast"); z.transform.SetParent(g, false); z.transform.localPosition = new Vector3(0, 0.7f, 0); z.layer = 4;
        var bc = z.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(NarrowHW * 2f, 1.4f, 1.4f);
        var lp = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaLaunchPad>(z);
        lp.localDirection = launch; lp.speed = launch.magnitude; lp.cooldown = 1f; lp.flightSeconds = 1f; lp.flightDrag = 0.3f;
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

    static void FireWall(Transform parent, string name, Vector3 a, Vector3 b)
    {
        var d = Flat(b - a); float len = d.magnitude; if (len < 0.5f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = (a + b) * 0.5f; g.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, d.normalized));   // forward＝壁の厚みの向き、right＝壁の長さの向き
        LavaBurn(g, "Burn", new Vector3(0, FireWallH * 0.5f - 1f - ZigRise, 0), new Vector3(len, FireWallH + ZigRise, 0.5f));   // 下は1個とばしの低い方の踊り場（ZigRise 下）まで
        FlameCurtain(g, name, len);
        // 火の粉だけ粒で（前は太い粒のかたまりで炎を作っていて、ぼやけて赤いもやに見えた。2026-10-31）
        var ps = FuwaStage2FireTest.FlamePS(g, "Flame", 0.2f, 2.6f, 1.3f, new Color(1f, 0.8f, 0.3f), new Color(1f, 0.28f, 0.05f));
        ps.transform.localPosition = new Vector3(0, -0.8f, 0);
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(len, 0.15f, 0.25f);
        var em = ps.emission; em.rateOverTime = 2.4f * len;
        var main = ps.main; main.startColor = Color.white; main.maxParticles = Mathf.CeilToInt(5f * len); main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
        ps.Play();
    }
    // 炎の板：長さ len、高さは当たりと同じ。少しずらして 2 枚（前後で模様をずらして厚みを出す）
    public static void FlameCurtain(Transform g, string name, float len)
    {
        float y0 = FireWallH * 0.5f - 1f - ZigRise - (FireWallH + ZigRise) * 0.5f, y1 = y0 + FireWallH + ZigRise;   // Burn の箱と同じ高さの範囲
        var m = new Mesh { name = "Course2_2_" + name + "_Curtain" };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tr = new List<int>();
        // 縦に立てた帯を 1 枚足す。pts は上から見た線、h はその点の炎の高さの割合（はしを低くして板の切り口を見せない）
        void Sheet(List<Vector2> pts, List<float> h, List<float> us)
        {
            int b = v.Count;
            for (int k = 0; k < pts.Count; k++)
            {
                v.Add(new Vector3(pts[k].x, y0, pts[k].y)); uv.Add(new Vector2(us[k], 0f)); col.Add(new Color(h[k], 0f, 0f));
                v.Add(new Vector3(pts[k].x, y1, pts[k].y)); uv.Add(new Vector2(us[k], 1f)); col.Add(new Color(h[k], 0f, 0f));
            }
            for (int k = 0; k + 1 < pts.Count; k++) { int a = b + k * 2; tr.AddRange(new[] { a, a + 2, a + 3, a, a + 3, a + 1 }); }
        }
        // 長い帯 3 枚：奥行きをずらして、くねらせる（2026-10-31 てじゃ「横からみたとき板すぎる」）
        int sn = 0;
        foreach (var z0 in new[] { -0.2f, 0f, 0.2f })
        {
            var pts = new List<Vector2>(); var h = new List<float>(); var us = new List<float>();
            int nx = Mathf.Max(2, Mathf.CeilToInt(len / 0.35f));
            for (int k = 0; k <= nx; k++)
            {
                float x = Mathf.Lerp(-len * 0.5f, len * 0.5f, k / (float)nx), end = len * 0.5f - Mathf.Abs(x);
                pts.Add(new Vector2(x, z0 + 0.13f * Mathf.Sin(x * 2.1f + sn * 2f) + 0.06f * Mathf.Sin(x * 4.7f + sn)));
                h.Add(Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(end / 0.8f))); us.Add(x + sn * 3.7f);
            }
            Sheet(pts, h, us); sn++;
        }
        // 横切る短い帯：横から見ても炎の幅が見える。外へいくほど低い
        const float CrossHW = 0.5f; int ci = 0;
        for (float x = -len * 0.5f + 0.5f; x < len * 0.5f - 0.3f; x += 1.2f, ci++)
        {
            var pts = new List<Vector2>(); var h = new List<float>(); var us = new List<float>();
            for (int k = 0; k <= 8; k++)
            {
                float w = Mathf.Lerp(-CrossHW, CrossHW, k / 8f), f = w / CrossHW;
                pts.Add(new Vector2(x + 0.12f * Mathf.Sin(w * 5f + ci), w)); h.Add(Mathf.Lerp(0.15f, 0.92f, 1f - f * f)); us.Add(w * 2.6f + ci * 5.3f);
            }
            Sheet(pts, h, us);
        }
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var mat = FuwaStage2WaterTest.ToonMat("Stage2_2_FlameCurtain", Color.white);
        mat.shader = Shader.Find("FuwaCourse/FlameCurtain"); EditorUtility.SetDirty(mat);
        var c = new GameObject("Curtain", typeof(MeshFilter), typeof(MeshRenderer)); c.transform.SetParent(g, false);
        c.GetComponent<MeshFilter>().sharedMesh = SaveMesh(m);
        var r = c.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // 火の玉：穴（の溶岩の面）hs を順に往復する。てっぺんが道の FireballTop 上の弧
    public static float RockR = 0.9f, RockHop = 0.75f, RockPeriod = 5.75f;   // 予告線（点線）は試したけど「びみょう、やっぱなしに」（2026-10-30）   // 火の岩の半径（てじゃ「もっとデカく」0.45→0.9）・1回はねる時間・1周（降る〜休み）
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
        (-22f, -3f, -16f, 1.6f, 20f, false), (-30f, 3f, 2f, 2f, 130f, true), (-20f, -7f, 20f, 1.4f, 250f, false),
        (24f, 1f, -24f, 1.8f, 70f, true), (31f, -5f, -7f, 1.5f, 200f, false), (22f, 4f, 10f, 1.7f, 310f, true),   // fire：木の代わりに燃えている島（2026-10-31 てじゃ「全部木じゃなくて燃えてる島も混ぜよ」）
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
    // 島の上で燃える炎：輪にした帯を半径を変えて重ねる。帯のはしは低くして切り口を見せない。下のはしは島の中へもぐらせる
    static Mesh IsleFlameMesh()
    {
        const float H = 3.2f, Sink = 0.18f, TopK = 0.3f;   // 上ほどすぼまる（まっすぐ立てるとケーキに見えた）
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tr = new List<int>();
        var arcs = new[] { (r: 0.85f, a0: 0f, a1: 190f, hh: 0.55f), (r: 0.8f, a0: 165f, a1: 375f, hh: 0.62f), (r: 0.5f, a0: 70f, a1: 310f, hh: 0.8f), (r: 0.25f, a0: -40f, a1: 220f, hh: 1f) };   // 外の帯が広いと、下のはしが島の下の面から突き出た
        for (int s = 0; s < arcs.Length; s++)
        {
            var q = arcs[s]; int b = v.Count; const int N = 20;
            for (int k = 0; k <= N; k++)
            {
                float t = k / (float)N, a = Mathf.Lerp(q.a0, q.a1, t) * Mathf.Deg2Rad, h = Mathf.Lerp(0.2f, q.hh, Mathf.Clamp01(Mathf.Min(t, 1f - t) * 5f));
                float u = (a * q.r) * 3f + s * 4.3f;
                foreach (var e in new[] { 0f, 1f })
                {
                    float rr = q.r * Mathf.Lerp(1f, TopK, e); v.Add(new Vector3(Mathf.Cos(a) * rr, Mathf.Lerp(-Sink, H, e), Mathf.Sin(a) * rr)); uv.Add(new Vector2(u, e)); col.Add(new Color(h, 0f, 0f));
                }
            }
            for (int k = 0; k < N; k++) { int a = b + k * 2; tr.AddRange(new[] { a, a + 2, a + 3, a, a + 3, a + 1 }); }
        }
        var m = new Mesh { name = "Course2_2_IsleFlame" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m);
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
        var isleFlame = IsleFlameMesh(); var flameMat = FuwaStage2WaterTest.ToonMat("Stage2_2_IsleFlame", Color.white); flameMat.shader = Shader.Find("FuwaCourse/FlameCurtain"); flameMat.SetFloat("_Base", 0.22f); flameMat.SetFloat("_BandScale", 2.6f); flameMat.SetFloat("_BandByH", 1f); flameMat.SetFloat("_Sway", 0.3f); EditorUtility.SetDirty(flameMat);
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
                Part("Flames", isleFlame, flameMat, ground, Quaternion.identity, Vector3.one);
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
        var m = FuwaStage2WaterTest.ToonMat("Stage2_2_LavaRock", new Color(0.35f, 0.12f, 0.08f));
        var sh = Shader.Find("FuwaCourse/LavaRock");
        if (sh) { m.shader = sh; m.SetTexture("_FlowTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Stage2_LavaFlow.png")); m.SetFloat("_Width", 0.42f); EditorUtility.SetDirty(m); }
        vis.GetComponent<Renderer>().sharedMaterial = m;
    }
    // 火の玉：のっぺりしたオレンジ→メラメラ（FuwaCourse/Fireball、2026-10-31）。尾は太く長く
    public static Material FireballMat()
    {
        var m = FuwaStage2WaterTest.ToonMat("Stage2_2_Fireball", new Color(1f, 0.55f, 0.15f));
        var sh = Shader.Find("FuwaCourse/Fireball");
        if (sh) { m.shader = sh; m.SetTexture("_FlowTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Stage2_LavaFlow.png")); EditorUtility.SetDirty(m); }
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
    static void LavaRock(Transform parent, string name, Vector3[] ps, float phase, float period, float first, float firstUp, ParticleSystem launch)
    {
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
        int hops = ps.Length - 1;
        f.hopTime = RockHop; f.hopUp = 1.2f; f.lastHopScale = hops > 1 ? 2f : 1f; f.firstHopScale = first; f.firstUp = firstUp; f.launchFx = launch;
        float fly = 0f; for (int k = 0; k < hops; k++) fly += RockHop * (k == 0 ? first : 1f) * (k == hops - 1 ? f.lastHopScale : 1f);
        f.rest = Mathf.Max(0.5f, period - fly); f.phase = phase; f.hitRadius = RockR + 0.05f;
        f.ballKnock = 2.5f; f.ballKnockUp = 1.5f; f.playerKnock = 5f; f.playerKnockUp = 4f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); FuwaStage2WaterTest.NoSync(f);
    }

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
        f.heat = heat; f.ball = ballProxy; f.holes = ts; f.visual = vis; f.trail = trail; f.trailRate = FireballTrailRate;
        f.arcHeight = top; f.flightTime = flight; f.rest = rest; f.phase = phase; f.hitRadius = FireballSize * 0.5f + 0.05f; f.ballKnock = 3.2f; f.ballKnockUp = 1.5f; f.playerKnock = 6f; f.playerKnockUp = 4.5f;
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
    static void VolcanoJet(Transform parent, string name, Vector3 edge, float edgeR, float roadY, float th, float period, float on, float phase)
    {
        float a = th * Mathf.Deg2Rad;
        var o = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); var back = -new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
        float len = FlameHW * 2f + 0.5f, top = FlameHighH + 1.4f, mid = (FlameLowH + FlameHighH) * 0.5f, half = (FlameHighH - FlameLowH) * 0.5f;
        // g：上（y）が炎の向き o、前（z）がワールドの上
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = edge + o * 0.02f + Vector3.up * mid; g.localRotation = Quaternion.LookRotation(Vector3.up, o);
        var flames = new ParticleSystem[FlameRows]; var warns = new ParticleSystem[FlameRows];
        for (int i = 0; i < FlameRows; i++)
        {
            var mt = new GameObject("Row" + i).transform; mt.SetParent(g, false); mt.localPosition = new Vector3(0, 0, Mathf.Lerp(-half, half, i / (FlameRows - 1f)));
            // 炎の粒は口から len 先まで（寿命 0.7 秒で届く速さ）
            flames[i] = FuwaStage2FireTest.FlamePS(mt, "Flame", 0.4f, len / 0.7f, 1.0f, new Color(1f, 0.8f, 0.25f), new Color(1f, 0.3f, 0.05f));
            { var sh = flames[i].shape; sh.angle = 9f; var mm = flames[i].main; mm.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f); }
            warns[i] = FuwaStage2FireTest.FlamePS(mt, "Warn", 0.3f, 1.2f, 0.5f, new Color(1f, 0.55f, 0.2f), new Color(0.5f, 0.2f, 0.1f));
            float cr = VolcanoCrackR(th, roadY + Mathf.Lerp(FlameLowH, FlameHighH, i / (FlameRows - 1f)));
            JetFromCrack(flames[i], warns[i], cr > 0f ? Mathf.Clamp(edgeR - cr + 0.1f, 0.3f, 4f) : FlameGap, len);
        }
        // 当たり：道の面の少し下から上の炎の 0.6 上まで
        var zone = new GameObject("JetZone"); zone.transform.SetParent(g, false); zone.transform.localPosition = new Vector3(0, len * 0.5f, 0); zone.layer = 4;
        var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(1.2f, len, FlameHighH + 0.6f + 0.05f);
        bc.center = new Vector3(0, 0, (FlameHighH + 0.6f - 0.05f) * 0.5f - mid);
        var j = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaFireJet>(zone);
        j.heat = heat; j.ball = ballProxy; j.phase = phase; j.period = period; j.onTime = on; j.flameRate = 110f;
        j.flame = flames[0]; j.warn = warns[0]; j.moreFlames = flames.Skip(1).ToArray(); j.moreWarns = warns.Skip(1).ToArray();
        // 吹っ飛ぶ向き：外（山から離れる向き）、外のふちの FlameLand 手前より先へは飛ばさない（てじゃ「落ちないくらい外に吹っ飛ばす」）。
        //  来た方へ足すのは「やっぱ手前に飛ばされるなぁ」でやめて、「ほんのちょっとだけ手前の角度も」で外向きを来た方へ 8° くらいかたむける（2026-10-30）。ワールドの向きで入れる
        j.knockDir = parent.TransformDirection((o + back * 0.14f).normalized);
        j.ballKnock = FlameKnock; j.ballKnockUp = FlameKnockUp;
        var ko = new GameObject("KnockOrigin").transform; ko.SetParent(parent, false); ko.localPosition = edge; ko.SetParent(g, true);
        j.knockOrigin = ko; j.knockReach = FlameHW * 2f - FlameLand; j.knockPerMeter = 0.85f;   // 試したら（上 1.5 で）1m/s で 1.15m くらい飛んだ
        // 人：地面に立っていると横の速さは地面に消されるので、しっかり上へ浮かせて飛ばす（2026-10-30 てじゃ「地面にたってるとプレイヤーがほとんど飛ばされない」）。
        //  向きは外（「プレイヤも横に飛ばしたい」）、ふわふわと同じくふちの手前まで
        j.playerPush = 6f; j.playerPushUp = 4.5f; j.playerPushDir = parent.TransformDirection((o + back * 0.14f).normalized); j.playerPerMeter = 1.0f;
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
        Box(g, "Bed", c + Vector3.down * (0.45f + 0.15f), new Vector3(PuddleR * 2f, 0.3f, PuddleR * 2f), rockMat, pathLayer);
        float wx = PuddleR * 2f;
        var water = new GameObject("Water"); water.transform.SetParent(g, false); water.transform.localPosition = c + Vector3.up * (PondSurface - 0.5f); water.layer = 4;
        var bc = water.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(PuddleR * 1.4f, 1.0f, PuddleR * 1.4f);   // 四角の角が穴の円に入るくらい（1.8 だと角が穴の外で、ふちでまものが落ちて壁に埋まった）
        var surf = GameObject.CreatePrimitive(PrimitiveType.Quad); surf.name = "Surface"; surf.transform.SetParent(water.transform, false);
        surf.transform.localPosition = Vector3.up * 0.5f; surf.transform.localRotation = Quaternion.Euler(90f, 0, 0); surf.transform.localScale = new Vector3(wx, wx, 1f);
        Object.DestroyImmediate(surf.GetComponent<Collider>());
        surf.GetComponent<Renderer>().sharedMaterial = FuwaCourse2_1Builder.WaterMat(0f, false);
        var w = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWater>(water); w.surface = surf.transform; w.heat = heat;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(w); FuwaStage2WaterTest.NoSync(w);
        var sw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaShallowWater>(water);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sw); FuwaStage2WaterTest.NoSync(sw);
    }

    // ほのおのまもの：コースのまもの（FuwaEnemy、寝てない・巣にかくれない物）を写して、赤く・炎をまとわせる。突進で燃える・水で倒れる
    static void FireMonster(Transform root, string name, Vector3 lp)
    {
        var src = Object.FindObjectsOfType<FuwaEnemy>(true).FirstOrDefault(e => !e.sleeping && !e.hideInNest && e.transform.root.name.StartsWith("FuwaCourse") && !e.transform.root.name.StartsWith("FuwaCourse2") && e.ball != null);
        if (!src) { Debug.LogWarning("[2–2] まもののお手本がない"); return; }
        // お手本の足元の道からの高さ
        float lift = 0.5f;
        if (Physics.Raycast(src.transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, 10f, 1 << 11, QueryTriggerInteraction.Ignore)) lift = src.transform.position.y - hit.point.y;
        var g = Object.Instantiate(src.gameObject, root, false); g.name = name;
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
        var ps = FuwaStage2FireTest.FlamePS(e.visual ? e.visual : g.transform, "BodyFlame", 0.35f, 1.2f, 0.7f, new Color(1f, 0.75f, 0.25f), new Color(1f, 0.25f, 0.05f));
        { var em = ps.emission; em.rateOverTime = 30f; var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); }
        // 水に落ちた時の「ジュッ」の湯気（1回だけぶわっ）
        var st = FuwaStage2FireTest.FlamePS(g.transform, "Steam", 0.3f, 1.6f, 1.1f, new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.88f, 0.92f, 0f));
        {
            var m = st.main; m.loop = false; m.playOnAwake = false; m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); m.maxParticles = 40;
            var em = st.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22), new ParticleSystem.Burst(0.15f, 10) });
            st.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
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
