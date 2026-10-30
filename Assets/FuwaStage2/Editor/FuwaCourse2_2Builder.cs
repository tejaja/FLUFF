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
    public static float ArenaHW = 6f, ArenaLen = 26f, PondW = 3.5f, PondLen = 10f, PondSurface = -0.2f;
    public static float FlamePeriod = 3.2f, FlameOn = 1.3f, SeedBefore = 3.5f;
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
        rockMat = FuwaStage2WaterTest.ToonMat("Stage2_2_Rock", RockCol);
        lavaMat = FuwaStage2WaterTest.ToonMat("Stage2_2_Lava", LavaCol);
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
        Ribbon(path, "Path12", r12, hw12, pts12, holes, chan);
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
        var lavaRoot = new GameObject("LavaHoles").transform; lavaRoot.SetParent(root, false);
        for (int k = 0; k < holes.Count; k++)
        {
            var hc = (Vector3)holes[k]; float ld = chan[k] != 0 ? ChanLavaDepth : LavaDepth;
            LavaDisk(lavaRoot, "Lava" + k, hc + Vector3.down * ld, HoleR + 0.02f);
            LavaBurn(lavaRoot, "LavaBallOut" + k, hc + Vector3.down * Mathf.Max(ld - 0.1f, 0.45f),   // 箱の角は穴の外にはみ出すので、上の面は道の 0.15 下より上にしない
                 new Vector3(HoleR * 2f, 0.6f, HoleR * 2f));
            Hazard(lavaRoot, "LavaPlayerOut" + k, hc + Vector3.down * (ld + 0.5f), new Vector3(HoleR * 2f, 1.2f, HoleR * 2f), true);
            if (chan[k] == 0) continue;
            // 溝の溶岩（穴の中心→ふちの少し外）と、道の横に垂れる溶岩（仮：板）
            var od = holeSide[k] * chan[k]; float len = holeEdge[k] + 0.06f;
            var rot = Quaternion.LookRotation(od);
            var cm = hc + od * (len * 0.5f) + Vector3.down * ld;
            var strip = Box(lavaRoot, "LavaChannel" + k, cm + Vector3.down * 0.02f, new Vector3(ChanHW * 2f + 0.04f, 0.04f, len), lavaMat, 0);
            strip.transform.localRotation = rot; Object.DestroyImmediate(strip.GetComponent<Collider>());
            var fall = Box(lavaRoot, "LavaFall" + k, hc + od * (len + 0.03f) + Vector3.down * (ld + LavaFallLen * 0.5f - 0.02f), new Vector3(ChanHW * 2f, LavaFallLen, 0.06f), lavaMat, 0);
            fall.transform.localRotation = rot; Object.DestroyImmediate(fall.GetComponent<Collider>());
            LavaBurn(lavaRoot, "LavaChannelBallOut" + k, cm + Vector3.down * 0.1f, new Vector3(ChanHW * 2f, 0.6f, len), rot);
            Hazard(lavaRoot, "LavaChannelPlayerOut" + k, cm + Vector3.down * 0.5f, new Vector3(ChanHW * 2f, 1.2f, len), true, rot);
        }
        // 火の玉：穴から穴へ往復。だんだん難しく（速く・休みが短く・最後は3つの穴）。てっぺんは道の FireballTop 上
        {
            //  最後の往復もほかと同じ（火の玉2つ・速めにしたら「めちゃくちゃ球多い」「ほかといっしょでいいかも」）。真上のは道の VertTop 上まで
            var fbs = new[] { (fl: 1.3f, rest: 1.6f, ph: 0f, top: FireballTop), (fl: 1.1f, rest: 1.1f, ph: 0.7f, top: FireballTop), (fl: 1.0f, rest: 1.0f, ph: 1.9f, top: FireballTop),
                              (fl: 1.1f, rest: 0.6f, ph: 0.4f, top: VertTop), (fl: 1.3f, rest: 1.0f, ph: 1.3f, top: VertTop), (fl: 1.2f, rest: 0.8f, ph: 0.2f, top: VertTop), (fl: 1.2f, rest: 0.9f, ph: 0.9f, top: VertTop) };
            for (int k = 0; k < holePts.Count; k++)
            {
                float ld = holePts[k].Count >= 2 ? ChanLavaDepth : LavaDepth;
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
            Strip(path, "Zig" + k, a, (b - a).normalized, (b - a).magnitude, ZigHW);
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

        // 火の岩：空から降ってきて、坂の下る向きに板を横切ってはねながら落ちる。板のまん中を1列
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
                rp.Insert(0, rp[0] + new Vector3(2.5f, 6f, 4f));   // 柱はまもののエリア（広い）にかからないよう横へ（3本並ぶ）
                rp.Add(rp[rp.Count - 1] + new Vector3(0, -10f, -4f));
                LavaRock(fire, "LavaRock" + c, rp.ToArray(), c * RockPeriod / cols.Length);
                RockPillar(fire, "RockPillar" + c, rp[0] + up * RockR);   // てじゃ「岩が出てるとこに岩柱でもつくるか」：岩は柱のてっぺんの溶岩から飛び出す
            }
        }

        // ④ ほのおのまもの：広い平らな所に まもの4ひき・ところどころに水たまり（まものを落とすと倒れる・燃えたふわふわは消える）
        //  広い所と⑤のくねくね道は1本のつなぎ目なしの道。踊り場から広がって、出口で細くなる
        var ar = pad + Vector3.forward * EndPadIn;
        var ae = ar + Vector3.forward * ArenaLen;
        var puddles = new[] { (x: 2.8f, z: 8.5f), (x: -3.2f, z: 13.5f), (x: 3.0f, z: 19f) };   // 5→3（てじゃ「水へらそ」）
        var monsters = new[] { (x: 0f, z: 6.5f), (x: -2.8f, z: 10.8f), (x: 2.6f, z: 13.5f), (x: 0.2f, z: 19.5f) };

        // ⑤ くねくね道（一部坂）：横の岩の穴から火炎放射（当たると黒焦げ→アウト、人はちょっと吹っ飛ぶ）。手前の火種で浮かせて待てる
        var pts5 = new[] { ar, ar + Vector3.forward * 4.5f, ae - Vector3.forward * 4.5f, ae, ae + new Vector3(0, 0, 3f), ae + new Vector3(4f, 0, 10f), ae + new Vector3(-3f, 1.5f, 18f), ae + new Vector3(3f, 3f, 26f), ae + new Vector3(0, 3f, 32f), ae + new Vector3(0, 3f, 35f) };
        var hw5 = pts5.Select(_ => PathHW).ToArray(); hw5[0] = EndPadR - 0.2f; hw5[1] = ArenaHW; hw5[2] = ArenaHW;
        var rad5 = new[] { 0f, 0f, 0f, 0f, 4f, 4f, 4f, 4f, 4f, 0f };
        var r5 = Route(pts5, rad5, new List<Vector3>());
        var pholes = puddles.Select(q => new Vector4(ar.x + q.x, ar.y, ar.z + q.z, PuddleR)).ToList();
        Ribbon(path, "Path5", r5, hw5, pts5, pholes, pholes.Select(_ => 0).ToList());
        for (int k = 0; k < pholes.Count; k++) Puddle(path, "Puddle" + k, (Vector3)pholes[k]);
        for (int k = 0; k < monsters.Length; k++) FireMonster(root, "FireMonster" + k, ar + new Vector3(monsters[k].x, 0, monsters[k].z));
        Checkpoint(root, "Checkpoint2", ae + Vector3.forward * 1.5f, Vector3.forward, PathHW);
        {
            var fts = new[] { (z: ae.z + 9f, side: 1f, ph: 0f), (z: ae.z + 17.5f, side: -1f, ph: 1.0f), (z: ae.z + 25.5f, side: 1f, ph: 2.0f) };
            for (int k = 0; k < fts.Length; k++)
            {
                Frame(r5, fts[k].z, out var c, out var t);
                var n = Vector3.Cross(up, t).normalized * fts[k].side;
                Flamethrower(fire, "Flamethrower" + k, c, -n, fts[k].ph);
                Frame(r5, fts[k].z - SeedBefore, out var sc, out _);
                GroundFire(fire, "FlameSeed" + k, sc);
            }
        }
        var p = pts5[pts5.Length - 1];

        // ⑦ ゴール
        GoalSet(root, p);

        // アウト（下に落ちた）：コースの下（コースの向きの箱、まわり 10m）。ほかのコース（2–1）にかからないように大きくしすぎない
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue;
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
        old.normals = m.normals; old.RecalculateBounds(); EditorUtility.SetDirty(old);
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
    static void Ribbon(Transform parent, string name, List<RPt> r, float[] hw, Vector3[] pts, List<Vector4> holes = null, List<int> chan = null)
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
        var vis = new MB(); var col = new MB(); var dn = Vector3.down * T; var du = Vector3.down * (T + U);
        for (int i = 0; i < n - 1; i++)
        {
            int st = Mathf.FloorToInt((s[i] + s[i + 1]) * 0.5f / StripeLen) % 2;
            // この帯にかかる穴：帯の手前と奥の線それぞれで、穴の中心の横の位置 c と、穴が線を切る半分の長さ h（線にかからなければ 0）
            // tip：穴の前後のはし（線にかからない側の線ではふちが1点 c に縮むので、本当のはしの点 m まで三角でうめる。うめないと帯の幅ぶんのトゲが出た）
            // ch：溝（穴から道のふち sx の側へ、幅 ChanHW*2）。溝の両はしの線は道の点の列に入れてある（InsertRows）ので、帯ごとに「溝の中か」で決まる
            var cut = new List<(float c0, float h0, float c1, float h1, Vector3 o, int tip, Vector3 m, int sx, bool ch0, bool ch1, float wd)>();
            if (holes != null)
                for (int hi = 0; hi < holes.Count; hi++)
                {
                    var hv = holes[hi];
                    var o = (Vector3)hv; float rr = hv.w; int sx = chan != null ? chan[hi] : 0;
                    // 線（side の向き）に直角な向きで測る（帯ごとの進む向きで測ると、となりの帯と穴のふちの点がずれて壁にすき間ができた）
                    var T0 = Vector3.Cross(side[i], Vector3.up); var T1 = Vector3.Cross(side[i + 1], Vector3.up);
                    float d0 = Vector3.Dot(Flat(o - r[i].p), T0);
                    float d1 = Vector3.Dot(Flat(o - r[i + 1].p), T1);
                    if (!(Mathf.Abs(d0) < rr || Mathf.Abs(d1) < rr)) continue;
                    int tip = Mathf.Abs(d0) >= rr ? 0 : Mathf.Abs(d1) >= rr ? 1 : -1;
                    var m = Vector3.zero;
                    if (tip == 0) { m = o - T0 * rr * Mathf.Sign(d0); m.y = r[i].p.y; }
                    if (tip == 1) { m = o - T1 * rr * Mathf.Sign(d1); m.y = r[i + 1].p.y; }
                    bool ch0 = sx != 0 && Mathf.Abs(d0) <= ChanHW + 0.06f, ch1 = sx != 0 && Mathf.Abs(d1) <= ChanHW + 0.06f;   // 足した線は進む向きの測り方が少しちがうので余裕を（0.02 だと外れて上の面がかけらになった）
                    cut.Add((Vector3.Dot(Flat(o - r[i].p), side[i]), Mathf.Sqrt(Mathf.Max(rr * rr - d0 * d0, 0f)),
                             Vector3.Dot(Flat(o - r[i + 1].p), side[i + 1]), Mathf.Sqrt(Mathf.Max(rr * rr - d1 * d1, 0f)), o, tip, m,
                             sx, ch0, ch1, (sx != 0 ? ChanLavaDepth : LavaDepth) + 0.3f));
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
                float xa0 = -w0, xa1 = -w1;
                foreach (var h in cut)
                {
                    bool open = h.ch0 && h.ch1;
                    float l0 = open && h.sx < 0 ? -w0 : h.c0 - h.h0, l1 = open && h.sx < 0 ? -w1 : h.c1 - h.h1;
                    Top(xa0, xa1, l0, l1);
                    xa0 = open && h.sx > 0 ? w0 : h.c0 + h.h0; xa1 = open && h.sx > 0 ? w1 : h.c1 + h.h1;
                }
                Top(xa0, xa1, w0, w1);
                if (!openR) mb.Quad(R[i], R[i + 1], R[i + 1] + dn, R[i] + dn, side[i], sub);
                if (!openL) mb.Quad(L[i], L[i + 1], L[i + 1] + dn, L[i] + dn, -side[i], sub);
                // 穴の前後のはし：縮んだ点 c と、はしの点 m・反対の線のふち2点の間を三角でうめる
                foreach (var h in cut)
                {
                    if (h.tip < 0) continue;
                    Vector3 c = h.tip == 0 ? P0(h.c0) : P1(h.c1);
                    Vector3 e0 = h.tip == 0 ? P1(h.c1 - h.h1) : P0(h.c0 - h.h0), e1 = h.tip == 0 ? P1(h.c1 + h.h1) : P0(h.c0 + h.h0);
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
                        Vector3 a = P0(h.c0 + sg * h.h0), b = P1(h.c1 + sg * h.h1);
                        if (h.tip == 0) a = h.m; if (h.tip == 1) b = h.m;
                        if ((a - b).sqrMagnitude < 1e-6f) continue;
                        mb.Quad(a, b, b + pd, a + pd, Flat(h.o - (a + b) * 0.5f), mb == vis ? 2 : 0);
                    }
                    // 溝の横の壁：溝のはしの線（片方の線だけ溝の中）で、穴のふちから道のふちまで
                    if (h.ch0 != h.ch1)
                    {
                        bool atI = h.ch0;   // 溝のはしの線が i の側
                        Vector3 a = atI ? P0(h.c0 + h.sx * h.h0) : P1(h.c1 + h.sx * h.h1), b = atI ? P0(h.sx * w0) : P1(h.sx * w1);
                        var inward = atI ? Flat(r[i].p - r[i + 1].p) : Flat(r[i + 1].p - r[i].p);   // 溝の中へ
                        mb.Quad(a, b, b + pd, a + pd, inward, mb == vis ? 2 : 0);
                    }
                }
            }
            if (cut.Count == 0) col.Quad(L[i] + dn, L[i + 1] + dn, R[i + 1] + dn, R[i] + dn, Vector3.down, 0);
            // 岩
            vis.Quad(Ri[i] + dn, Ri[i + 1] + dn, Ri[i + 1] + du, Ri[i] + du, side[i], 2);
            vis.Quad(Li[i] + dn, Li[i + 1] + dn, Li[i + 1] + du, Li[i] + du, -side[i], 2);
            vis.Quad(Li[i] + du, Li[i + 1] + du, Ri[i + 1] + du, Ri[i] + du, Vector3.down, 2);
        }
        // はしのふた
        for (int e = 0; e < 2; e++)
        {
            int i = e == 0 ? 0 : n - 1; var o = Flat(r[e == 0 ? 0 : n - 1].p - r[e == 0 ? 1 : n - 2].p).normalized;
            int st = e == 0 ? 0 : Mathf.FloorToInt(s[n - 1] / StripeLen) % 2;
            vis.Quad(L[i], R[i], R[i] + dn, L[i] + dn, o, st); col.Quad(L[i], R[i], R[i] + dn, L[i] + dn, o, 0);
            vis.Quad(Li[i] + dn, Ri[i] + dn, Ri[i] + du, Li[i] + du, o, 2);
        }
        MeshObj(parent, name, vis.Build("Course2_2_" + name, 3), col.Build("Course2_2_" + name + "_Col", 1), new[] { pathA, pathB, rockMat });
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
                mb.Quad(c, c, p1, p0, Vector3.up, 0);
                mb.Quad(p0, p1, p1 + dn, p0 + dn, om, mb == vis ? 1 : 0);
            }
            col.Quad(c + dn, c + dn, p1 + dn, p0 + dn, Vector3.down, 0);
            vis.Quad(q0 + dn, q1 + dn, q1 + du, q0 + du, om, 2);
            vis.Quad(c + du, c + du, q1 + du, q0 + du, Vector3.down, 2);
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
        var ps = FuwaStage2FireTest.FlamePS(g, "Flame", 0.2f, 3.4f, 1.8f, new Color(1f, 0.75f, 0.25f), new Color(0.9f, 0.15f, 0.05f));
        ps.transform.localPosition = new Vector3(0, -0.8f, 0);
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(len, 0.15f, 0.1f);
        var em = ps.emission; em.rateOverTime = 55f * len;
        var main = ps.main; main.maxParticles = Mathf.CeilToInt(90f * len); main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        ps.Play();
    }

    // 火の玉：穴（の溶岩の面）hs を順に往復する。てっぺんが道の FireballTop 上の弧
    public static float RockR = 0.9f, RockHop = 0.75f, RockPeriod = 5.75f;   // 予告線（点線）は試したけど「びみょう、やっぱなしに」（2026-10-30）   // 火の岩の半径（てじゃ「もっとデカく」0.45→0.9）・1回はねる時間・1周（降る〜休み）
    public static float PillarR = 1.7f, PillarH = 22f;
    // 岩の出てくる柱（ブロックアウト）：てっぺんは top、下は空へのびる。てっぺんに溶岩のくぼみ
    static void RockPillar(Transform parent, string name, Vector3 top)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder); col.name = "Pillar"; col.transform.SetParent(g, false);
        col.transform.localPosition = top + Vector3.down * (PillarH * 0.5f); col.transform.localScale = new Vector3(PillarR * 2f, PillarH * 0.5f, PillarR * 2f);
        Object.DestroyImmediate(col.GetComponent<Collider>());
        col.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_2_PillarRock", new Color(0.45f, 0.3f, 0.27f));
        LavaDisk(g, "Lava", top + Vector3.down * 0.05f, PillarR - 0.35f);
        var smoke = FuwaStage2FireTest.FlamePS(g, "Glow", PillarR * 0.6f, 0.6f, 0.8f, new Color(1f, 0.6f, 0.2f), new Color(0.6f, 0.15f, 0.05f));
        smoke.transform.localPosition = top; { var m = smoke.main; m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); var e = smoke.emission; e.rateOverTime = 12f; }
    }

    static void LavaRock(Transform parent, string name, Vector3[] ps, float phase)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var ts = new Transform[ps.Length];
        for (int i = 0; i < ps.Length; i++) { ts[i] = new GameObject("P" + i).transform; ts[i].SetParent(g, false); ts[i].localPosition = ps[i]; }
        var mover = new GameObject("Rock").transform; mover.SetParent(g, false); mover.localPosition = ps[0];
        var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere); vis.name = "Visual"; vis.transform.SetParent(mover, false); vis.transform.localScale = new Vector3(1f, 0.85f, 0.95f) * (RockR * 2f);
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        vis.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_2_LavaRock", new Color(0.35f, 0.12f, 0.08f));
        var trail = FuwaStage2FireTest.FlamePS(g, "Trail", RockR * 0.7f, 0.35f, 0.5f, new Color(1f, 0.7f, 0.2f), new Color(0.85f, 0.15f, 0.05f));
        { var m = trail.main; m.startSize = new ParticleSystem.MinMaxCurve(RockR * 0.9f, RockR * 1.6f); }
        var f = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaLavaRock>(mover.gameObject);
        f.heat = heat; f.ball = ballProxy; f.points = ts; f.visual = vis; f.trail = trail;
        int hops = ps.Length - 1;
        f.hopTime = RockHop; f.hopUp = 1.2f; f.lastHopScale = 2f; f.rest = Mathf.Max(0.5f, RockPeriod - (hops - 1 + f.lastHopScale) * RockHop); f.phase = phase; f.hitRadius = RockR + 0.05f;
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
        vis.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_2_Fireball", new Color(1f, 0.55f, 0.15f));
        var trail = FuwaStage2FireTest.FlamePS(g, "Trail", 0.4f, 0.4f, 0.6f, new Color(1f, 0.75f, 0.25f), new Color(0.9f, 0.2f, 0.05f));
        { var m = trail.main; m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); }
        var f = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaFireball>(mover.gameObject);
        f.heat = heat; f.ball = ballProxy; f.holes = ts; f.visual = vis; f.trail = trail;
        f.arcHeight = top; f.flightTime = flight; f.rest = rest; f.phase = phase; f.hitRadius = FireballSize * 0.5f + 0.05f; f.ballKnock = 3.2f; f.ballKnockUp = 1.5f; f.playerKnock = 6f; f.playerKnockUp = 4.5f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); FuwaStage2WaterTest.NoSync(f);
    }
    // 穴の中の溶岩の面（丸い板）
    static void LavaDisk(Transform parent, string name, Vector3 c, float r)
    {
        const int N = 40;
        var v = new List<Vector3> { Vector3.zero }; var tr = new List<int>();
        for (int k = 0; k <= N; k++) { float a = k * Mathf.PI * 2f / N; v.Add(new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r)); }
        for (int k = 1; k <= N; k++) tr.AddRange(new[] { 0, k, k + 1 });   // 上から見て時計回り＝上向き
        var m = new Mesh { name = "Course2_2_LavaDisk" }; m.SetVertices(v); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(parent, false); g.transform.localPosition = c;
        g.GetComponent<MeshFilter>().sharedMesh = SaveMesh(m);
        g.GetComponent<MeshRenderer>().sharedMaterial = lavaMat;
    }

    // 火炎放射：道の中心 c の横の岩の穴（口）から、dir（岩→道）の向きへ吹く
    static void Flamethrower(Transform parent, string name, Vector3 c, Vector3 dir, float phase)
    {
        // 岩は道のふちにぴったり、道の下の岩の底（2.1m 下）から 2m 上まで。口は岩の面
        var mouth = c - dir * (PathHW + 0.05f) + Vector3.up * 0.7f;
        var rock = Box(parent, name + "_Rock", c - dir * (PathHW + 0.67f) + Vector3.up * -0.05f, new Vector3(2.6f, 4.1f, 1.3f), rockMat, 0);
        rock.transform.localRotation = Quaternion.LookRotation(dir);
        FuwaStage2FireTest.Jet(parent, name, mouth, heat, phase);
        var g = parent.Find(name); g.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        var m = g.Find("Mouth"); Object.DestroyImmediate(m.GetComponent<Collider>()); m.localPosition = new Vector3(0, -0.15f, 0);
        var j = g.GetComponentInChildren<FuwaFireJet>(true);
        j.deadly = true; j.playerPush = 3f; j.playerPushUp = 1.5f; j.period = FlamePeriod; j.onTime = FlameOn;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(j);
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
        var bc = water.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(PuddleR * 1.8f, 1.0f, PuddleR * 1.8f);
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
        e.flameFx = ps; e.steamFx = st; e.floatSink = -0.03f; e.floatBob = 0.05f; e.floatTilt = 25f;
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
