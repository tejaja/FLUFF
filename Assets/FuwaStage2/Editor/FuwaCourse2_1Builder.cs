using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// コース 2–1「みずのこみち」（Water Path。2026-10-22 てじゃ「みずのこみちにしとくか」で みずのさと から改名。その前は 2026-10-19 に みずのたに→みずのさと）のブロックアウト（2026-10-19）。
// てじゃ「コース自体も全体的に流線的な感じにしたいな、水の流れを感じるような」→ 道も水も、ゆるくうねる1本の中心線（Catmull-Rom）にそって作る。
// 道はステージ1と同じシマシマ（Course2_1_PathA/B、いちばん濃い青）。水は要所だけ（てじゃ「水に乗せるのは要所要所だけ」）、人は水に入りたくない（強い減速）。
// 流れ：
//  ① スタートは崖の中の洞窟。口のすぐ前を滝が流れ落ちていて、ふわふわは口から滝に撃ち込むしかない（滝で池へ押し落とされる）。
//     人は洞窟の右の壁の脇道（ツタのカーテンをくぐる。人はすり抜け、ふわふわは跳ね返される。2026-10-22 クモの巣から変更）から出て、岸の道を下りる。
//     崖の上は水源の池（滝の元）。2026-10-19 てじゃ「スタートを今のスタートの下に洞窟をつくって…ふわふわを滝に打ち込むしかない形に、
//     人は滝のよこに脇道を作ってそこにクモの巣」「上の段は水源として使う」（前の作りだと岸の道ぞいに空中で運べて滝を迂回できた）
//  ② 池から川（流れあり）でふわふわが流される。人は右の岸を追いかける。1回目の渡り場で右岸の道が川で切れていて、
//     すぐ先の岩（StopRocks）で止まったふわふわを左岸の道へ撃ってすくい上げる。人は川をじゃぶじゃぶ渡る。
//     左岸を空中で運んで、2回目の渡り場で川を越えて右岸へ。川上へ跳ねる魚に当たると川上へ飛ばされて川に落ちる（2026-10-25 てじゃのスケッチ）。
//     間に合わないと川の終わりの崖の滝から谷へ落ちてアウト。右岸の道は谷を小さい橋で越える（2026-10-20 てじゃ。前は橋＋橋の下のトゲ）
//  ③ 蓮の池：道が途切れて、人は固定の蓮の葉を飛び石で渡る。ふわふわは池を撃って渡す
// ふわふわは地面（道も、水の底も）に触るとアウト、水に浮いている間はセーフ。水から出る時は撃って浮かせる（道を水の中まで伸ばすと触ってアウトになる）
//  ④ 貝の道（2026-10-21、前の「泡の段々」と入れかえ）：蓮の池から先は海っぽく（河口・浜辺へ）。細い堤の道の両わきで貝が左右交互に
//     パカッと開いて、道を横切る泡を吹く。ふわふわが泡に触れると包まれて流され、放っておくと反対側の滝へ運ばれて落下アウト。
//     割れる前に撃てば泡ごと押し戻せる（FuwaClamBubbles）。てじゃ「回避可能な風ギミックに近いイメージ、そのかわり当たると風より厄介」
//  ⑤ 滝の道（2026-10-21）：ぐねぐねの道のカーブの外側に、上から滝が落ちてきて、その下は道の横の水路。滝に当たると水路に落とされて浮き、
//     流れで運ばれて水路の端の滝から落ちたらアウト。落ちる前に撃ってすくい上げればセーフ。3セット、後ろほど滝が太くて水路が短い（FallsRoad）。
//     てじゃ「ぐねぐねの道+上から滝+川、ちょっと流れて下に落下する滝ってのを数セット…あたっても落ちるまでにすくい上げればセーフ」（かぜのおかの竜巻に近い流れ）
//  ⑥ よけられない滝 → 下の川を流される → 右岸のゴールの広場へ撃ち上げる。人は滝の左の岩のくぐり口（ツタのカーテン）を通って左岸を走る
// 道どうしのつなぎ目（岸の道→本道）は、岸の道を 1cm 低くして本道の中に埋める（同じ高さで重ねるとZファイト）
public static class FuwaCourse2_1Builder
{
    const string RootName = "FuwaCourse2_1";
    // 置き場所：ステージ1と同じく、ロビーのまん中からワープの台（Slot0）の向きに放射状。スタートはまん中から SpokeR m、ロビーの床の StartBelow m 下
    public const float SpokeR = 34.8f, StartBelow = 4f;
    const string MeshDir = "Assets/FuwaStage2/Generated/Course2_1";
    const float Thick = 0.3f, Bevel = 0.08f, Stripe = 0.75f;
    const int BevelSteps = 4;
    // 浅瀬は底から岸の道まで 0.4m（ふつうのジャンプ約0.46m で上がれる）。2026-10-23 てじゃ「各種浅瀬は全部道に上がりやすくしたい」「浅くする感じ」：
    // 前は 深さ0.6・ふち0.35・岸の道0.4（底から道まで約1m、石段の所しか上がれなかった）
    public const float WaterDepth = 0.25f, RimW = 0.45f, RimUp = 0.12f, BankTop = 0.15f, Base = -1.5f;
    public const float StartH = 2.2f, TerraceStep = 1.8f;
    // ⑥ ラスト（2026-10-22 てじゃ「3連滝のあとに、強制で滝にぶつかるゾーン→そのまま流されてく、川はゴール横を直進、途中で打ち上げてゴールへ、
    // プレイヤーは川岸を走って追いかける」）。位置は滝の道の終わり（sF1）からの距離、高さは滝の道（FallsH）からの高さ
    const float FinLand = 3f;                                  // 踊り場（滝の道のあと、広め）
    const float FinRoadEnd = 3.5f, WoodGap = 0.02f;            // 青い道の終わり（滝の膜の中）、青い道と木の足場のすき間
    const float FinCurtain = 3.7f, FinCurtainTop = 7f;         // よけられない滝のカーテンの位置と落ち口の高さ
    const float FinRiver0 = 3.5f, FinRiverSurf = -3f, FinRiverHW = 2.2f, FinRiverFlow = 1.2f;   // 下の川
    const float FinAfterBend = 8f;                             // カーブのあとの川の長さ（その先は滝）
    const float FinCliffLen = 6f;                              // 川の始まりの右の崖（カーテンの右を回り込めないように）の長さ
    const float FinWalkW = 1.8f, FinDownEnd = 8f;              // 左岸の人の道の幅と、踊り場から下りきる所（2026-10-23 12→8、てじゃ「坂急にしてもいい」）
    const float FinBankW = 0.6f;                               // 右岸の細い地面
    const float FinGateH = 2.6f, FinWallOut = 9.5f;            // 滝の左の岩の壁：人のくぐり口の高さ、壁の左のはし（中心線から）
    const float FinGoalOut = 22f, FinGoalUp = BankTop;   // 滝の道のアウトの終わり（踊り場から）、ゴールの島の川の水面からの高さ（2026-10-23 てじゃ「ゴール、川岸と同じくらいの高さにしよ」1.6→川岸）
    // 左岸の人の道の高さ：踊り場の高さから川岸の高さへ下りる
    // 左岸の道の高さ：踊り場から川岸の高さまで下りる（川に落ちた人が上がれるように。2026-10-23）。カーブの所から先はゴールの島の高さへ登る
    static float LeftBankH(float s) { return Mathf.Lerp(FallsH, FallsH + FinRiverSurf + BankTop, Mathf.Clamp01((s - sF1 - FinLand) / (FinDownEnd - FinLand))); }
    static float WalkToGoalH(float s) { return Ramp(s, sBend0, sWalkEnd - 0.6f, FallsH + FinRiverSurf + BankTop, walkEndH); }
    static float goalH, sBend0, sBend1, sFin1;   // ゴールの島の上の面、川のカーブの始まり・終わり、川の終わり（滝）
    static Vector2 goalC, goalD, walkP0; static float sWalkEnd;   // walkP0＝左岸の道のまっすぐな所の始まり（道の中心）、向きは goalD   // ゴールの島の中心・向き（入口の逆）、左岸の道の終わり
    // 洞窟：床＝StartH、天井の高さ CaveIn、壁の外側 CaveHW（道の幅6の外に厚み0.4）、口の半分の幅 MouthHW。上に屋根の板、その上が水源の池
    public const float CaveIn = 2.6f, CaveHW = 3.4f, MouthHW = 1.7f, RoofT = 0.3f, SideGapLen = 4.03f, SideGapH = 2.4f;   // SideGapLen：右の壁の当たり判定のすき間（ななめの穴が壁を通る長さ。2026-10-27 3.4→4.03）
    public const float FallsReach = 1.6f;   // 滝が落ち口からどれだけ前に落ちるか(m)。2026-10-23 2.8→1.6（てじゃ「ちょと遠くてふわふわが届かないことがおおい」）
    public static float SourceSurf { get { return StartH + CaveIn + RoofT + 0.3f + WaterDepth; } }

    // 中心線の点（コースのローカル x,z）。最初と最後はカーブの向きを決めるだけの点
    // 蓮の池と貝の道のあいだにまっすぐな道を WalkLead m 足す（2026-10-25 てじゃ「手間だけど、ちょっと直線を追加して」チェックポイントを置く所）。
    // Ctrl の 12 番から先を、11→12 の向きに WalkLead だけずらして、中心線をそのぶん長くする（貝の道から先の形はそのまま）
    public const float WalkLead = 4.5f, WalkToTer = 7.0f;   // WalkToTer：貝の道の始まりから段々（sTer0）まで（前の SI(11.8)−貝の道の始まり）
    // 貝の道と三連滝の道のあいだにも、まっすぐな道を FallsLead m（2026-10-25 てじゃ「貝と３連滝の間に直線追加してそこにチェックポイント」）。Ctrl の 15 番から先を 14→15 の向きにずらす
    public const float FallsLead = 4.5f;
    static Vector2[] ShiftFrom(Vector2[] a, int from, float lead)
    {
        var d = (a[from] - a[from - 1]).normalized;
        for (int i = from; i < a.Length; i++) a[i] += d * lead;
        return a;
    }
    static readonly Vector2[] Ctrl = ShiftFrom(ShiftFrom(new[]
    {
        // 川（4〜8）は 2026-10-25 にまっすぐ寄りに（てじゃのスケッチ「赤が道、青が川、２回渡る」。前は (-6,20),(-5,28),(1,34),(7,40),(8,48) の大きいS字）
        new Vector2(0, -6), new Vector2(0, 0), new Vector2(0, 6), new Vector2(-2, 13), new Vector2(-4, 20), new Vector2(-3.5f, 28),
        new Vector2(-2, 35), new Vector2(1, 42), new Vector2(4, 48), new Vector2(4, 55), new Vector2(-2, 61), new Vector2(-5, 68),
        new Vector2(-3, 76), new Vector2(2, 82), new Vector2(5, 89), new Vector2(5, 97),
        // ⑤ 滝の道（ぐねぐね。滝と水路は FallsSet0・FallsGap で置く）→ ⑥ ラスト
        // 2026-10-23 滝の道を短くしたので、(-1,116) から先はまっすぐ (0,136) へ（ラストの踊り場がカーブにかかって道が折れた。前は (4,122),(2,129)）
        // 同日 てじゃ「ラストの川の直線も短くしたい」：カーブから先を 7m 手前へ（約18m→約11m）
        new Vector2(0, 103), new Vector2(-4, 109), new Vector2(-1, 116), new Vector2(-0.5f, 122.5f), new Vector2(0, 129),
        // ⑥ ラスト：よけられない滝 → 下の川を流される。川は (0,136) から右へ半径6mで80°曲がって、まっすぐ滝へ。
        // 曲がった先の外側の岸にゴールの島（2026-10-23 てじゃのスケッチ「川を曲げる、島を曲がった先の川岸におく、川の先は滝」）
        new Vector2(0, 136), new Vector2(1.404f, 139.857f), new Vector2(4.958f, 141.909f),
        new Vector2(10.867f, 142.951f), new Vector2(16.776f, 143.993f), new Vector2(22.685f, 145.035f), new Vector2(28.594f, 146.077f),
    }, 12, WalkLead), 15, FallsLead);
    const int BendCtrl0 = 21, BendCtrl1 = 23;   // カーブの始まり・終わりの点（Ctrl の番号）

    // ---- 中心線 ----
    static List<float> _tu, _ts;
    static Vector2 Spline(float u)
    {
        int i = Mathf.Clamp(Mathf.FloorToInt(u), 1, Ctrl.Length - 3); float t = u - i;
        Vector2 p0 = Ctrl[i - 1], p1 = Ctrl[i], p2 = Ctrl[i + 1], p3 = Ctrl[i + 2];
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }
    static void Table()
    {
        if (_tu != null) return;
        _tu = new List<float>(); _ts = new List<float>();
        float s = 0; var prev = Spline(1);
        for (float u = 1; u <= Ctrl.Length - 2 + 1e-4f; u += 0.002f)
        {
            var p = Spline(u); s += (p - prev).magnitude; prev = p;
            _tu.Add(u); _ts.Add(s);
        }
    }
    public static float SI(float u) { Table(); int k = Mathf.Clamp(Mathf.RoundToInt((u - 1) / 0.002f), 0, _ts.Count - 1); return _ts[k]; }
    static float UofS(float s)
    {
        Table();
        int lo = 0, hi = _ts.Count - 1;
        if (s <= 0) return 1f - (-s) / Mathf.Max(0.01f, (Spline(1.002f) - Spline(1)).magnitude) * 0.002f;
        if (s >= _ts[hi]) return _tu[hi];
        while (hi - lo > 1) { int m = (lo + hi) / 2; if (_ts[m] < s) lo = m; else hi = m; }
        return Mathf.Lerp(_tu[lo], _tu[hi], (s - _ts[lo]) / Mathf.Max(1e-6f, _ts[hi] - _ts[lo]));
    }
    public static void Center(float s, out Vector2 p, out Vector2 d)
    {
        Table();
        float total = _ts[_ts.Count - 1];
        float sc = Mathf.Clamp(s, 0, total);
        float u = UofS(sc);
        p = Spline(u);
        var a = Spline(Mathf.Max(1, u - 0.004f)); var b = Spline(Mathf.Min(Ctrl.Length - 2, u + 0.004f));
        d = (b - a).normalized;
        p += d * (s - sc);   // 両はしの外はまっすぐ伸ばす
    }
    static Vector2 Right(Vector2 d) { return new Vector2(d.y, -d.x); }

    // ---- 区間（BuildAll で決める）----
    // 川の渡り場は2つ（2026-10-25 てじゃ「川で見てるのが長過ぎる」→ スケッチ「２回渡る」）。
    //  1回目：川の始まりから Cross1After m。右岸の道が川で切れていて、流れてきたふわふわを左岸の道へすくい上げる。人は川をじゃぶじゃぶ渡る
    //  2回目：川の終わりの Cross2Before m 手前。左岸の道が切れていて、空中で運んできたふわふわを右岸の道へ撃って渡す。川上へ跳ねる魚がじゃま（FuwaRiverFish）
    // 川の終わりは崖の滝で、その先は谷（GorgeLen m、落ちたらアウト）。右岸の道は谷を小さい橋で越える
    public const float Cross1After = 6f, Cross2Before = 5f, CrossGap = 2.4f, GorgeLen = 5.5f, GorgeDrop = 10f;   // Cross2Before 8→5・川の終わり3m手前へ（2026-10-25 てじゃ「川のオワリの滝、もうちょい手前でいい」。2回目の渡り場の位置はそのまま）
    static float sCross1, sCross2;
    static float b1WoodS0; static Func<float, float> b1OffF;
    // 迂回路の浮島：場所（中心線の s と横）、半径。BridgeIsland() で作る
    public static float BridgeIslandS = 8.0f, BridgeIslandLat = 9.6f, BridgeIslandR = 1.8f;   // 浮島（いまは使っていない）
    public static float BridgeBendS = 12.5f, BridgeBendLat = 9.0f, BridgeStraight = 4.5f;
    // ななめの脇道（ツタの穴）：ツタのまん中（root のローカル xz）、穴の向き、半分の幅。Tools/stage2_cave_rock.py の SIDE_V / SIDE_D / SIDE_HW と同じ
    public static readonly Vector2 SideV = new Vector2(3.95f, 3.89f), SideD = new Vector2(0.907f, 0.422f).normalized; public const float SideHW = 1.7f;   // 迂回路の木の橋の曲げ点（中心線の s と横）
    static Vector2 bridgeIslandC; static float bridgeIslandH;
    static float sPlaza0, sPlaza1, sPool1, sRiver1, sPond0, sPond1, sTer0, sGoal0, sEnd;
    static float[] terA = new float[3], terB = new float[3];

    class Piece
    {
        public string name; public float s0, s1;
        public Func<float, float> off, h, w;
        public bool round0 = true, round1 = true;
        // 横の位置は中心線に直角にとる（off で片側だけえぐる道。off の線の向きでとると、えぐれの所で断面がねじれて縞がゆがんだ）
        public bool centerLat;
        // 中心線にそわない道（null でなければ、off のかわりにこの位置。曲がる川からまっすぐ離れていく道など）
        public Func<float, Vector2> at;
        // 木の足場（見た目は WoodWalk の板。当たり判定は道と同じ PathMesh）
        public bool wood;
        // 木の板の見た目だけ持ち上げる高さ（洞窟の床の上にのせる所。null＝0）と、板を並べはじめる所（NaN＝s0）
        public Func<float, float> woodLift;
        public float woodS0 = float.NaN;
        // 板の向き（null＝道に直角）。中心線の右向きとの混ぜ具合 0〜1（1＝洞窟の向きに直角＝板がそろう）。桁を出しはじめる所（NaN＝s0）
        public Func<float, float> woodAlign;
        // 板のはしを道のへりの線にそわせる（台形の板。曲がっている所で板のはしがギザギザに飛び出さない）
        public bool woodEdgeCut;
        public bool collisionOnly;   // 当たり判定だけ（見た目は別に作る）
        // この横（中心線から）より内側には板を置かない（洞窟の床のはし。s が floorEdgeS1 より前だけ）
        public float floorEdge = float.NaN, floorEdgeS1;
        public float woodBeamS0 = float.NaN;
    }
    class Water
    {
        public string name; public float s0, s1, surf, flow;
        public Func<float, float> hw;
        public bool open0, open1;
        public bool rimL = true, rimR = true;
        public float bodyBottom = Base;
        // 深い水：人は落ちたらアウト（浅瀬の減速ではなく、最後の再開位置へ戻す）。2026-10-19 てじゃ「滝壺、深くして人は落ちたらアウトにしよう、現状つっきれてしまう」
        public bool deep;
        public float depth = WaterDepth;
        // 左のふちの切れ目（あふれて流れ落ちる所）。s の範囲、無しなら gapL1 <= gapL0
        public float gapL0, gapL1;
        // 中心線からの横のずれ（道の横の水路など）。0＝中心線にそう
        public Func<float, float> off = s => 0f;
        // 0より大きいと、底に当たったふわふわをアウトにせず、この速さ(m/s)で上へぽよんと戻す（浅い水路で、滝に押されて底まで沈んでも助かるように）
        public float bedBounce;
        // 底の穴（深い所）：s が hole0〜hole1 で、中心線から左 holeL・右 holeR の範囲は底が holeBed まで下がる。holeL が null なら穴なし
        public float hole0, hole1, holeBed;
        public Func<float, float> holeL, holeR;
        // 流れを中心線の逆向き（s が減る向き）に
        public bool back;
        // 深い水の終わり（open1）で、色を次の浅い水の色へなだらかに変える長さ(m)。0＝変えない（2026-10-27 てじゃ「深いとこと浅いとこの境目をもっと見栄え良く」）
        public float fade1;
    }

    static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
    static Func<float, float> Smoothed(Func<float, float> f, float r)
    {
        return s => { float sum = 0; int n = 9; for (int i = 0; i < n; i++) sum += f(s + Mathf.Lerp(-r, r, i / (float)(n - 1))); return sum / n; };
    }
    static float Ramp(float s, float a, float b, float va, float vb) { return Mathf.Lerp(va, vb, Smooth01((s - a) / (b - a))); }

    // 滝壺：滝の幅から急に広がる（ふくらみは手前寄り）。岸の道から向こう岸まではダッシュジャンプで届かない広さ（てじゃ「ここぐらいまでは届いちゃう、こっちルートは防ぎたい」）
    // ふくらみ 2026-10-25 4.3→3.2（滝つぼを短くしたので、横に広すぎないように）
    static float PoolHW(float s) { float t = Mathf.InverseLerp(sPlaza1, sPool1, s); return Mathf.Lerp(CaveHW - RimW, 1.8f, t) + 3.2f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(t), 0.65f))), 0.9f); }
    // 2回目の渡り場だけ川を CrossWiden m 広げる（前後 CrossWidenLen m でなだらかに）。魚と飛び石を並べる場所をとる
    //（2026-10-25 てじゃ「川広げないとちょっときついか、魚小さくすると地味すぎるし」）
    public static float CrossWiden = 1.0f, CrossWidenLen = 4.5f;
    static float RiverHW(float s)
    {
        float t = s - sPool1;
        float widen = CrossWiden * Smooth01(1f - Mathf.Abs(s - sCross2) / CrossWidenLen);
        return 1.8f + 0.25f * Mathf.Sin(t * 0.45f) - 0.4f * Smooth01((s - (sRiver1 - 2.5f)) / 2.5f) + widen;
    }
    // 蓮の池：まるっこい形（だ円より少しふっくら）。入口・出口は道の幅の口（2026-10-20 てじゃ「池自体もっと丸っこくしたいな」。前は sin で両はしがとがったレンズ形）
    static float PondHW(float s) { float u = Mathf.Abs(Mathf.InverseLerp(sPond0, sPond1, s) * 2f - 1f); return Mathf.Max(1.4f, 6.2f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(u, 2.3f)), 1f / 2.3f)); }
    // 貝の道：道の半分の幅、貝の置き場所（道のふちから外へ）、両わきの滝（道のふちから外へ）。sWalk0/1 と WalkH は Build で決める
    public static float WalkHW = 1.7f, ClamOut = 0.6f, FallsOut = 2.6f;
    static float sWalk0, sWalk1, sStraight0;
    static Func<float, float> WalkH;
    // 滝の道：道の半分の幅、セットの場所（中心線の u＝カーブのあたり）
    public static float RoadHW = 1.5f;
    // 2026-10-23 てじゃ「三連滝の間隔をもっと狭めたい」：前は中心線の u＝カーブのあたり（15.1/17.05/19、間隔約15m）。今は滝の道の始まりから FallsSet0 m、FallsGap m おき（滝の道もそのぶん短い）
    public static float FallsSet0 = 8f, FallsGap = 10f; const int FallsSets = 3;
    // 滝が道の外側から食い込む深さ（そこは道がえぐれて水路。残る道の幅＝2×RoadHW−これ）。後ろほど深く
    public static float[] CutDepth = { 1.6f, 1.9f, 2.2f };   // 2026-10-23 +0.4（幕をえぐれのふちから0.4m離したぶん、道を細く。てじゃ「もっと道細くても良い」）。残る道 1.4/1.1/0.8m
    // 水路の、滝より下流の長さ（ここで撃ってすくい上げる。滝の横ですくうと、また滝に当たって押し戻される）。後ろほど短く
    public static float[] ChanAfter = { 5f, 4f, 3f };
    // 水路の半分の幅（えぐれていない所）・道からの水面の低さ・流れ・深さ、滝の高さ（水の出どころはあとで。今は空中から）
    // 2026-10-22 てじゃ「深い水だと救出できないから浅くする」「下の水に飛び込んだあとにジャンプで戻れるくらいの高さに」：
    // 前は 深さ2m・水面が道の0.9m下（人は落ちたらアウト）。今は浅瀬（人は入れる）で、底から道の上まで 0.4m（ジャンプ約0.46m で上がれる）。
    // 浅いと滝に押されたふわふわが底に当たるので、底はアウトにせずぽよんと戻す（ChanBedBounce）
    public static float OverFallsGap = 0.4f;
    public static float ChanDepth = 0.28f, ChanHW = 1.1f, ChanDrop = 0.12f, ChanFlow = 1.3f, OverFallsH = 6f, ChanBedBounce = 1.5f;
    // 水路の流れを来た方（スタート側）へ（2026-10-22 てじゃ「流れを手前にする」）。水路の長い側・端の滝・道のえぐれの形も前後反転する
    public static bool FallsFlowBack = true;
    static float sF0, sF1, FallsH;
    static float[] setS, setO;   // セットの中心線の位置と、えぐる側（+1＝右）
    // 蓮の池の左のふちの、水があふれて流れ落ちる所（まん中の大きい蓮の花をよけて少し先）
    static float PondSpill() { return (sPond0 + sPond1) * 0.5f + 4f; }
    // 深い所の形：池の形をひとまわり小さくしただ円（ふちから PondShallowW は浅瀬）、外周を少しゆがませる（左右べつべつ）。浅瀬は最低 1.6m 残す
    //（2026-10-20 てじゃ「池の形に近い感じで真ん中は深い感じにしよう」「入り口と出口付近は丸く」「濃い部分の外周をちょっと歪ませて」）
    static float PondDeepMid => (sPond0 + sPond1) * 0.5f;
    static float PondDeepA => (sPond1 - sPond0) * 0.5f - PondShallowW;
    static float PondDeep0 => PondDeepMid - PondDeepA;
    static float PondDeep1 => PondDeepMid + PondDeepA;
    static float PondDeepHW(float s) { float u = (s - PondDeepMid) / PondDeepA; return u * u >= 1f ? 0f : Mathf.Min(PondHW(s) - PondShallowW, (PondHW(PondDeepMid) - PondShallowW) * Mathf.Sqrt(1f - u * u)); }
    static float PondWob(float s, float ph) { float x = (s - PondDeep0) / Mathf.Max(0.01f, PondDeep1 - PondDeep0); return 0.16f * Mathf.Sin(x * 9.4f + ph) + 0.09f * Mathf.Sin(x * 17.3f + ph * 2.3f); }
    static float PondDeepL(float s) { return Mathf.Max(0f, Mathf.Min(PondHW(s) - 1.6f, PondDeepHW(s) * (1f + PondWob(s, 0.7f)))); }
    static float PondDeepR(float s) { return Mathf.Max(0f, Mathf.Min(PondHW(s) - 1.6f, PondDeepHW(s) * (1f + PondWob(s, 2.9f)))); }
    // 深い所の底（2026-10-23 てじゃ「沈むっていうより触れたら上の方で突然アウト」→ 底に穴をあけて、沈んでから落ちた判定）
    public static float PondHoleBed = -1.6f, PondFallTop = -0.45f;
    // 蓮の葉のあいだ（ふちからふちまで）。VRChat の走りジャンプで約2.4m 飛べるので、着地の分を引いてギリ届くくらい
    public static float LilyGapMin = 1.6f, LilyGapMax = 1.9f;
    // 蓮の池のふちの浅瀬の幅（その内側は深い＝人は落ちたらアウト）
    public static float PondShallowW = 2.4f;   // 1.8→2.4（てじゃ「もうちょい浅瀬を広く」）
    static float WaterHW(float s)
    {
        if (s >= sPlaza1 && s < sPool1) return PoolHW(s);
        if (s >= sPool1 && s <= sRiver1) return RiverHW(s);
        return 0f;
    }

    [MenuItem("FuwaCourse/ステージ2/2–1 みずのこみち（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        _tu = null; Table();
        sEnd = SI(Ctrl.Length - 2);
        sPlaza0 = 0f; sPlaza1 = SI(2.0f);
        // 滝つぼ 2026-10-25 u3.8→3.1（約13m→約7m。てじゃ「滝壺がでかいのかな、待ち時間がやっぱながい」）
        sPool1 = SI(3.1f); sRiver1 = SI(7.3f) - 3f;
        sCross1 = sPool1 + Cross1After; sCross2 = sRiver1 - Cross2Before;
        sPond0 = SI(8.9f); sPond1 = SI(10.9f);
        sTer0 = sPond1 + RimW + WalkLead + WalkToTer;   // 前は SI(11.8)。まっすぐな道を足したぶん後ろへ
        const float TerLen = 5.5f;
        for (int k = 0; k < 3; k++) { terA[k] = sTer0 + k * (TerLen + RimW); terB[k] = terA[k] + TerLen; }
        sGoal0 = SI(21.2f);
        float b2 = terB[2];

        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot0").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        root.rotation = Quaternion.LookRotation(spoke);
        Center(1.0f, out var st0, out _);
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        root.position = lc + spoke * SpokeR - root.rotation * new Vector3(st0.x, 0, st0.y);
        root.position = new Vector3(root.position.x, floorY - StartBelow - StartH, root.position.z);
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Generated", "Course2_1");

        var pathA = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_1_PathA.mat");
        var pathB = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_1_PathB.mat");
        var ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_LobbyGround.mat");
        var bedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Lobby_Ring.mat");
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh");
        int pathLayer = pathRef ? pathRef.layer : 11;

        // ---- 道 ----
        var pieces = new List<Piece>();
        // M1 スタートの台（幅6）
        pieces.Add(new Piece { name = "M1_Start", s0 = sPlaza0 - 1.5f, s1 = sPlaza1, off = s => 0f, h = s => StartH, w = s => s <= M1MouthS ? 6f : M1MouthW, round0 = false });   // 2026-10-27 てじゃ「青い床がはみでてる」：洞窟の前の出口（半分の幅 1.8m）では床も出口の幅に（6m のままだと岩の前の面から角が出ていた）   // 後ろは洞窟の入口の敷居とつながる
        // B1 右の岸の道：台の右半分から出て、滝をよけ、池・川の岸をくねっと下りて、渡り場で川にぶつかって終わる。
        // B2 左の岸の道：渡り場の向こう岸から、川の終わりの崖の滝の横を通って谷を小さい橋で越え、本道に合流。
        // ふわふわは滝→川で流される。人は右岸を追いかけて、渡り場で待ちかまえて、流れてきたふわふわを左岸の道の続きへ撃ってすくい上げる。
        // もたもたしてると流されて川の終わりの滝から谷へ落ちてアウト（トゲはなし）。人は川を渡る（浅瀬で遅くなる）
        //（2026-10-20 てじゃ「道と川をクロスさせるけど、道は川でとぎれる、そのあたりで待ち構えてフワフワを川から川の対岸の道の続きにすくいあげる」
        // 「モタモタしてると川に流されて滝で落下アウト（ここのトゲはやめる）」「川に突っ込んでいいと思う、川で遅くなるのも体験できるし」。
        // 前は岸の道が途中で切れて浅瀬を歩き、橋で川をまたいで橋の下はトゲ、だった）
        float gorge1 = sRiver1 + GorgeLen;
        float mergeA = gorge1 + 0.5f, mergeB = gorge1 + 5f;
        float bankOff(float s) => RiverHW(Mathf.Clamp(s, sPool1, sRiver1)) + RimW + 1.55f;
        Func<float, float> b1t = s =>
        {
            // 洞窟の中（右半分）から右の壁の脇道を抜けて外へ。脇道のすき間（gapA〜）に入ってから外へ曲がる
            //（2026-10-27 てじゃ「この道が岩からはみ出してる」：前はすき間の手前から曲がりはじめて、道のはしが洞窟の壁から突き出ていた）
            float gA = sPlaza1 - 0.4f - SideGapLen;
            // まっすぐななめに（木の板がそろうように。2026-10-27）
            // 1.5＝洞窟の床（幅6）の右半分ぴったり。曲がりはじめを洞窟の中へ早めて、ゆるい S 字に（曲がりの半径が道の半分の幅より小さいと、木の板が内側で重なって飛び出した）
            // 洞窟の中の木を短く（2026-10-27 てじゃ「洞窟内の木もっとみじかく」）：曲がりはじめを遅くして、曲がりおわりを洞窟の外へ（S字の長さはそのまま＝板が重ならない）
            float r0 = gA - 0.4f, r1 = sPlaza1 + 1.6f;
            if (s < r1) return Mathf.Lerp(1.5f, 4.7f, Smooth01((s - r0) / (r1 - r0)));
            return Mathf.Max(Mathf.Lerp(4.7f, 3.4f, Smooth01((s - r1) / 6f)), WaterHW(Mathf.Min(s, sRiver1)) + RimW + 1.55f);
        };
        // 2026-10-25 道は 右岸(B1) → 1回目の渡り場 → 左岸(B2) → 2回目の渡り場 → 右岸(B3) → 谷を越えて本道（前は右岸 → 渡り場1つ → 左岸）
        Func<float, float> b2t = s => -bankOff(s);
        Func<float, float> b3t = s => Mathf.Lerp(bankOff(s), 0f, Smooth01((s - mergeA) / (mergeB - mergeA)));
        Func<float, float> b1h = s => Ramp(s, sPlaza1 + 0.3f, sPlaza1 + 10f, StartH - 0.01f, BankTop - 0.01f);
        // 迂回路のカーブをゆるく（2026-10-27 てじゃ「もいちょい曲線をゆるく」）：洞窟を出たあとは、まわりの一番外（窓の中の最大）をなめらかにした線
        //（ただ平均すると滝つぼのふくらみの所で道が水に寄るので、最大をとってから平均＝水からは前と同じかそれ以上はなれる）
        Func<float, float> b1Env = s => { float m = float.MinValue; for (int i = 0; i <= 12; i++) m = Mathf.Max(m, b1t(s + Mathf.Lerp(-2.5f, 2.5f, i / 12f))); return m; };
        var b1Near = Smoothed(b1t, 0.6f); var b1Far = Smoothed(b1Env, 2.5f);
        Func<float, float> b1off = s => Mathf.Lerp(b1Near(s), b1Far(s), Smooth01((s - sPlaza1 - 1.6f) / 2f));
        // 迂回路を作り直し（2026-10-27 てじゃ「この端自体ちょっとつくりなおそうか」→「浮島なしで、もっと直線より（ちょっと曲がる）」）：
        // 洞窟の床（右半分）→ ツタのすき間 → ゆるく曲がる1本の木の橋 → 1回目の渡り場（右岸の道のおわり）。形は2次ベジェ（A → 曲げ点 → E）
        b1OffF = b1off;
        {
            float gA2 = sPlaza1 - 0.4f - SideGapLen;
            Vector2 CL2(float s, float lat) { Center(s, out var p, out var d); return p + Right(d) * lat; }
            // 2026-10-27 てじゃ「穴とツタ自体を斜めに」：橋はツタのカーテンの線（SideV、SideD に直角）からまっすぐ始まる。3次ベジェ（ツタ → SideD の向きに出る → 曲げ点 → E）
            //「木の道も若干外よりに」→ 曲げ点を外へ
            //「こう繋がる感じに」→ ツタの穴からまっすぐ BridgeStraight m 出てから、ゆるく曲がって渡り場へ（まっすぐの所の続きの向きで曲がりはじめる）
            var A = SideV + SideD * BridgeStraight; var P1 = A + SideD * 3f; var Pm = CL2(BridgeBendS, BridgeBendLat);
            float sE = sCross1 - CrossGap * 0.5f; var E = CL2(sE, bankOff(sE));
            Vector2 Bz(float t) { float a = 1 - t; return a * a * a * A + 3 * a * a * t * P1 + 3 * a * t * t * Pm + t * t * t * E; }
            // 長さの表（u＝はじめからの長さ → t）
            var tu = new List<float> { 0f }; var tt = new List<float> { 0f };
            for (int k = 1; k <= 400; k++) { float t2 = k / 400f; tu.Add(tu[k - 1] + (Bz(t2) - Bz((k - 1) / 400f)).magnitude); tt.Add(t2); }
            float Lb = tu[400] + BridgeStraight;
            Func<float, Vector2> At = u =>
            {
                if (u <= BridgeStraight) return SideV + SideD * u;
                float uc = u - BridgeStraight; int k = 1; while (k < 400 && tu[k] < uc) k++; return Bz(Mathf.Lerp(tt[k - 1], tt[k], Mathf.InverseLerp(tu[k - 1], tu[k], uc)));
            };
            // 高さ：岩の穴を出るまで（ツタから 2.6m）は床の高さ、そこから渡り場まで同じ傾き
            float uOut = 2.6f;
            float hA = StartH - 0.01f, hE = BankTop - 0.01f;
            pieces.Add(new Piece { name = "B1_Bridge", s0 = 0f, s1 = Lb, off = s => 0f, at = At, w = s => 3f, h = u => u <= uOut ? hA : Mathf.Lerp(hA, hE, (u - uOut) / (Lb - uOut)),
                round0 = false, wood = true, woodEdgeCut = true, woodBeamS0 = uOut + 1.2f });
            // 洞窟の床からツタまでの当たり判定（見た目は SideAlcoveFloor）
            pieces.Add(new Piece { name = "B1_Alcove", s0 = 0f, s1 = 2.4f, off = s => 0f, at = u => SideV - SideD * (2.4f - u), w = s => SideHW * 2f, h = s => StartH, round0 = false, round1 = false, collisionOnly = true });
        }
        pieces.Add(new Piece { name = "B2_Bank", s0 = sCross1 + CrossGap * 0.5f, s1 = sCross2 - CrossGap * 0.5f, off = Smoothed(b2t, 1.0f), w = s => 3f, h = s => BankTop - 0.01f });
        pieces.Add(new Piece { name = "B3_Bank", s0 = sCross2 + CrossGap * 0.5f, s1 = mergeB, off = Smoothed(b3t, 1.0f), w = s => 3f, h = s => BankTop - 0.01f });
        // M2 谷の向こう → 蓮の池の手前（本道は水のふちの外側にぴったりつける。端は丸めない）
        pieces.Add(new Piece { name = "M2", s0 = mergeA, s1 = sPond0 - RimW, off = s => 0f, w = s => 3f, h = s => BankTop, round0 = false, round1 = false });
        // M3 貝の道：蓮の池の向こうから、細い堤の道をゆるく登って大きな滝の登り坂へ（両わきは崖の外＝落ちたらアウト）
        float topSurf = 2 * TerraceStep;
        sStraight0 = sPond1 + RimW; sWalk0 = sStraight0 + WalkLead; sWalk1 = b2 + RimW;
        // M2b 蓮の池の出口から貝の道までのまっすぐな道（チェックポイント2の所）
        pieces.Add(new Piece { name = "M2b_Straight", s0 = sStraight0, s1 = sWalk0, off = s => 0f, w = s => WalkHW * 2f, h = s => BankTop, round0 = false, round1 = false });
        float walkTop = topSurf + BankTop;
        WalkH = s => Ramp(s, sWalk0 + 1.5f, sWalk1 - 1.5f, BankTop, walkTop);
        pieces.Add(new Piece { name = "M3_ClamWalk", s0 = sWalk0, s1 = sWalk1, off = s => 0f, w = s => WalkHW * 2f, h = WalkH, round0 = false, round1 = false });
        // M4 滝の道：貝の道の高さのまま、ぐねぐね。最後のセットの水路の先まで
        sF0 = sWalk1 + FallsLead; FallsH = walkTop;
        // M3c 貝の道の終わりから三連滝の道までのまっすぐな道（チェックポイント3の所）
        pieces.Add(new Piece { name = "M3c_Straight", s0 = sWalk1, s1 = sF0, off = s => 0f, w = s => WalkHW * 2f, h = s => FallsH, round0 = false, round1 = false });
        setS = new float[FallsSets]; setO = new float[FallsSets];
        for (int i = 0; i < FallsSets; i++) { setS[i] = sF0 + FallsSet0 + FallsGap * i; setO[i] = OuterSide(setS[i]); }
        { int last = FallsSets - 1; sF1 = setS[last] + NotchEnd + ChanAfter[last] + 3f; }
        sGoal0 = sF1 + FinGoalOut; goalH = FallsH + FinRiverSurf + FinGoalUp;
        sBend0 = SI(BendCtrl0); sBend1 = SI(BendCtrl1); sFin1 = sBend1 + FinAfterBend;
        GoalSpot();
        pieces.Add(new Piece { name = "M4_FallsRoad", s0 = sF0, s1 = sF1, off = s => (Cut(s, -1f) - Cut(s, 1f)) * 0.5f, w = s => RoadW(s) - Cut(s, -1f) - Cut(s, 1f), h = s => FallsH, round0 = false, round1 = false, centerLat = true });
        // M5 踊り場 → M6 左の坂（川岸へ下りる）→ M7 左岸の道（人が走って追いかける）。川が右へ曲がる所から先は、まっすぐゴールの島の入口へ
        // 2026-10-27 てじゃ「青い道が滝にまっすぐ続き、滝で途切れる。左側はそのままゴールまで木製の足場でつなぐ」
        // → 青い道は滝の道の幅のまま滝の膜の中まで（FinRoadEnd）。左へ分かれる人の道（踊り場の左半分・坂・川岸・ゴールへの道）は木の足場
        pieces.Add(new Piece { name = "M5_Landing", s0 = sF1, s1 = sF1 + FinRoadEnd, off = s => 0f, w = s => RoadHW * 2f, h = s => FallsH, round0 = false, round1 = false });
        {
            float a = -RoadHW - WoodGap, b = -FinRiverHW - FinWalkW;   // 青い道の左のへり（少しすき間）〜人の道の左のへり
            pieces.Add(new Piece { name = "W5_Deck", s0 = sF1, s1 = sF1 + FinLand, off = s => (a + b) * 0.5f, w = s => a - b, h = s => FallsH, round0 = false, round1 = false, wood = true });
        }
        pieces.Add(new Piece { name = "M6_Down", s0 = sF1 + FinLand, s1 = sF1 + FinDownEnd, off = s => -FinRiverHW - FinWalkW * 0.5f, w = s => FinWalkW, h = LeftBankH, round0 = false, round1 = false, wood = true });
        pieces.Add(new Piece { name = "M7_LeftBank", s0 = sF1 + FinDownEnd, s1 = sBend0, off = s => -FinRiverHW - FinWalkW * 0.5f, w = s => FinWalkW, h = LeftBankH, round0 = false, round1 = false, wood = true });
        {
            Center(sBend0, out var wp, out var wd); var wq = wp + Right(wd) * (-FinRiverHW - FinWalkW * 0.5f); float ws = sBend0;
            pieces.Add(new Piece { name = "M8_ToGoal", s0 = ws, s1 = sWalkEnd, off = s => 0f, at = s => wq + wd * (s - ws), w = s => FinWalkW, h = WalkToGoalH, round0 = false, round1 = false, wood = true });
        }
        // 右岸のゴールの広場（M8_GoalPlaza）は 2026-10-23 にやめて、ゴールの島（GoalSet）に

        var pm = new GameObject("PathMesh"); pm.transform.SetParent(root, false); pm.layer = pathLayer;
        // 見た目用：洞窟の床（M1）は、木の分かれ道の所から右を分かれ道の内側のへりで切る（当たり判定は元の M1 のまま）
        var visPieces = new List<Piece>();
        foreach (var pc in pieces)
        {
            if (pc.wood || pc.collisionOnly) continue;
            if (pc.name != "M1_Start" || true) { visPieces.Add(pc); continue; }   // 2026-10-27 床を切る案はやめ（木は床のはしの外だけ＝下の WoodWalk の floorEdge）
            float cut = b1WoodS0;
            // 木の道の内側のへり（道に直角の断面の -1.5 の点）を、中心線の (s, 横) にうつした表。床の右のへりをそれに合わせる
            var wp = pieces.Find(q => q.name == "B1_BankWood"); var tS = new List<float>(); var tL = new List<float>();
            for (float s2 = cut - 1f; s2 <= pc.s1 + 1.5f; s2 += 0.05f)
            {
                var q3 = Point(wp, s2, -1.5f, 0); Center(s2, out var cp, out var cd);
                var dq = new Vector2(q3.x, q3.z) - cp; tS.Add(s2 + Vector2.Dot(dq, cd)); tL.Add(Vector2.Dot(dq, Right(cd)));
            }
            Func<float, float> right = s =>
            {
                int k = 1; while (k < tS.Count - 1 && tS[k] < s) k++;
                return Mathf.Min(3f, Mathf.Lerp(tL[k - 1], tL[k], Mathf.InverseLerp(tS[k - 1], tS[k], s)) - 0.005f);
            };
            visPieces.Add(new Piece { name = "M1_StartA", s0 = pc.s0, s1 = cut, off = pc.off, h = pc.h, w = pc.w, round0 = false, round1 = false });
            visPieces.Add(new Piece { name = "M1_StartB", s0 = cut, s1 = pc.s1, off = s => (right(s) - 3f) * 0.5f, h = pc.h, w = s => right(s) + 3f, round0 = false, round1 = pc.round1, centerLat = true });
        }
        var vis = new Mesh { name = "Course2_1Path_Bevel" }; BuildPathVisual(vis, visPieces);
        var col = new Mesh { name = "Course2_1Path" }; BuildPathCollider(col, pieces);
        vis = Save(vis, MeshDir + "/Course2_1Path_Bevel.asset"); col = Save(col, MeshDir + "/Course2_1Path.asset");
        pm.AddComponent<MeshFilter>().sharedMesh = vis;
        pm.AddComponent<MeshRenderer>().sharedMaterials = new[] { pathA, pathB };
        pm.AddComponent<MeshCollider>().sharedMesh = col;
        // 道の横の面にふわふわが当たってもアウトにしない（上に乗ったらアウト）。川から撃ち上げる時に道のふちにかすってアウト、をなくす
        //（2026-10-23 てじゃ「川から脱出するときに道の縁にあたってアウトになることあるかも」）
        { var to = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTopOut>(pm); if (to) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(to); NoSync(to); } }
        var woodPieces = pieces.FindAll(p => p.wood);   // 木の足場は岩山ができてから（板のはしを岩で切る）

        // ---- 水 ----
        var fx = FuwaStage2WaterTest.MakeFx(root);
        var ps0 = new GameObject("PlayerSplash"); ps0.transform.SetParent(root, false);
        var splash = ps0.AddComponent<AudioSource>(); splash.playOnAwake = false; splash.spatialBlend = 1f;
        splash.minDistance = 2f; splash.maxDistance = 25f; splash.rolloffMode = AudioRolloffMode.Linear;
        splash.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_water_splash.wav");
        var waters = new List<Water>
        {
            new Water { name = "W0_Source", s0 = sPlaza0 - 1.5f, s1 = sPlaza1, surf = SourceSurf, flow = 0f,   // 流れ 0.8→0（まん中の湧き水の波紋と流れの筋が重なってへん。2026-10-25 てじゃ）
                open1 = true, bodyBottom = StartH + CaveIn + RoofT,
                hw = s => Mathf.Lerp(CaveHW - RimW, MouthHW, Smooth01((s - (sPlaza1 - 3.5f)) / 3.5f)) },
            // 流れ 2026-10-25 0.9→1.3→1.6、川 1.1→1.4→1.6（てじゃ「川で見てるのが長過ぎる」。人は浅瀬を歩かず岸の道を走るので速くしてよい）
            new Water { name = "W1_Pool", s0 = sPlaza1, s1 = sPool1, surf = 0f, flow = 1.6f, hw = PoolHW, open0 = true, open1 = true, deep = true, depth = 2.5f, fade1 = 3.5f },
            // 終わりは崖の滝（open1）。下のかたまりを深くして、谷から見て崖に見えるように
            new Water { name = "W2_River", s0 = sPool1, s1 = sRiver1, surf = 0f, flow = 1.6f, hw = RiverHW, open0 = true, open1 = true, bodyBottom = -6f },
            new Water { name = "W3_LilyPond", s0 = sPond0, s1 = sPond1, surf = 0f, flow = 0f, hw = PondHW, gapL0 = PondSpill() - 1.1f, gapL1 = PondSpill() + 1.1f,
                hole0 = PondDeep0, hole1 = PondDeep1, holeL = PondDeepL, holeR = PondDeepR, holeBed = PondHoleBed },
        };
        var wroot = new GameObject("Water").transform; wroot.SetParent(root, false);
        foreach (var w in waters) BuildWater(wroot, w, bedMat, ground, fx, splash);

        // 渡り場の石段は 2026-10-23 にやめた（浅くしてどこからでも上がれるようになった。てじゃ「ふわふわぶつかるトラップになってる」）
        // 川の終わりの崖の滝 → 谷（下は何もない。ふわふわも人も落ちたらアウト）
        CurvedFalls(root, "Falls_River", sRiver1, 0.02f, -GorgeDrop, RiverHW(sRiver1) * 2f + 0.4f, 1.6f, 0.8f);   // 厚み 0.4→0.8（2026-10-25 てじゃ「各種滝、太くしたい」）
        {
            var gz = new GameObject("Gorge").transform; gz.SetParent(root, false);
            float gm = (sRiver1 + gorge1) * 0.5f;
            Center(gm, out var gp0, out var gd0);
            var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
            // ふわふわ：道の下 2m より下に落ちたらアウト。人：その少し下
            var bo = new GameObject("BallOut"); bo.transform.SetParent(gz, false); bo.layer = 4;
            bo.transform.localPosition = new Vector3(gp0.x, -4f, gp0.y); bo.transform.localRotation = Quaternion.LookRotation(new Vector3(gd0.x, 0, gd0.y));
            var bbc = bo.AddComponent<BoxCollider>(); bbc.isTrigger = true; bbc.size = new Vector3(24f, 4f, GorgeLen + 6f);
            var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(bo); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); NoSync(hz);
            var po = new GameObject("PlayerOut"); po.transform.SetParent(gz, false); po.layer = 0;
            po.transform.localPosition = new Vector3(gp0.x, -5f, gp0.y); po.transform.localRotation = bo.transform.localRotation;
            var pbc = po.AddComponent<BoxCollider>(); pbc.isTrigger = true; pbc.size = new Vector3(24f, 4f, GorgeLen + 6f);
            var pcc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(po); pcc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pcc); NoSync(pcc);
            // 谷の向こうの崖（本道の下）
            float fw = RiverHW(sRiver1) + RimW + 0.5f;
            Sweep(gz, "FarCliff", gorge1, sPond0 - RimW, s => -fw - 3.2f, s => fw, s => -6f, s => BankTop - Thick - 0.01f, ground, 0, true, true);
        }

        // 渡り場の飛び石、1回目の渡り場の先の岩、2回目の渡り場の魚
        CrossingStones(root, "Stones1", sCross1, 1f);
        CrossingStones(root, "Stones2", sCross2, -1f);
        StopRocks(root);
        RiverFish(root, fx);
        GuardFish(root, fx);
        // ④ 貝の道
        ClamWalk(root, ground);
        // ⑤ 滝の道（＋滝の方へ押す横風と吹き流し）
        FallsRoad(root, ground, bedMat, fx, splash);
        FallsWind(root);

        // ---- 地形のかたまり（ブロックアウト）----
        var terr = new GameObject("Terrain").transform; terr.SetParent(root, false);
        // ① スタートの洞窟（崖）：床の下・左右の壁・奥の壁・口の両わき・屋根。右の壁は脇道のすき間をあける
        float cs0 = sPlaza0 - 1.5f, cIn = StartH + CaveIn, wallB = StartH - Thick, gapA = sPlaza1 - 0.4f - SideGapLen, gapB = sPlaza1 - 0.4f;
        Sweep(terr, "CaveFloorCliff", cs0 - 0.4f, sPlaza1, s => -CaveHW, s => CaveHW, s => Base, s => wallB - 0.01f, ground, 0, true);
        // 奥の壁：まん中に人が歩いて入る入口（2026-10-23 スタートの島から木の橋で入ってくる）。入口の下は敷居
        Sweep(terr, "CaveBackL", cs0 - 0.4f, cs0, s => -CaveHW, s => -DoorHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveBackR", cs0 - 0.4f, cs0, s => DoorHW, s => CaveHW, s => wallB, s => cIn, ground, 0, true);
        if (StartH + DoorH < cIn - 0.01f) Sweep(terr, "CaveBackLintel", cs0 - 0.4f, cs0, s => -DoorHW, s => DoorHW, s => StartH + DoorH, s => cIn, ground, 0, true);
        Sweep(terr, "CaveDoorSill", cs0 - 0.4f, cs0, s => -DoorHW, s => DoorHW, s => wallB, s => StartH, ground, pathLayer, true);
        Sweep(terr, "CaveWallL", cs0, sPlaza1, s => -CaveHW, s => -3f, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveWallR_Back", cs0, gapA, s => 3f, s => CaveHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveWallR_Lintel", gapA, gapB, s => 3f, s => CaveHW, s => StartH + SideGapH, s => cIn, ground, 0, true);
        Sweep(terr, "CaveMouthL", gapB, sPlaza1, s => -3f, s => -MouthHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveMouthR", gapB, sPlaza1, s => MouthHW, s => CaveHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveRoof", cs0 - 0.4f, sPlaza1, s => -CaveHW, s => CaveHW, s => cIn, s => cIn + RoofT, ground, 0, true);
        // 脇道のツタのカーテン：ななめの穴の中、橋の向き（SideD）に直角（2026-10-27 てじゃ「穴とツタ自体を斜めに」）
        {
            var across = new Vector2(-SideD.y, SideD.x);
            VineCurtain(root, "SideVines", new Vector3(SideV.x, StartH + SideGapH, SideV.y), new Vector3(across.x, 0, across.y), SideHW * 2f, SideGapH, 2101);
        }
        // 洞窟の床のはしからツタまでは岩山の穴の床（岩）そのもの（2026-10-27 てじゃ「洞窟側で床がなくなるなら岩の床で」→ 板をのせると岩とめりこんだので、岩山の穴の床の高さを洞窟の床にそろえた）
        // 洞窟の見た目は Blender の岩山（箱の壁は当たり判定だけ残す）
        CaveRock(root, terr);
        // ① の滝：水源の池から、洞窟の口の前を下の池へ
        Center(sPlaza1, out var fp, out var fd);
        // 落ち口はRで前へ張り出して、洞窟の口から少し離れた所に落ちる（ふわふわが崖ぎわのふちに叩きつけられないように）。厚みもつける
        // 2026-10-19 てじゃ「滝にあたったふわふわが地面に激突するから、ちょっとRつけて遠くに落ちるように、厚みもほしい」
        // 落ち口は岩山の本当のふち（startLipZ、まっすぐ +z 向き）。前は中心線の sPlaza1（ふちより 0.75m 手前・少しななめ）で、滝が岩の床にもぐって片側に岩のとがりが出ていた（2026-10-27 てじゃ「入口のウラの滝上、いろいろきたない」）。
        // 落ちる所（滝つぼの位置）は前と同じになるように、のびる長さを短く
        { FuwaCourse2_1Builder.Center(sPlaza1, out var op, out var od); var olip = op + od * FallsReach;
          // 幅：上の平らな所が出口の壁から壁までになるように、丸い角のぶん外へふくらませる（角は壁の中にうまって、壁の下では外へ丸くふくらむ。
          // 2026-10-27 てじゃ「丸まってるのがいい、外側に膨らむように丸めて」。前は角が内側へ丸まって、角と壁のあいだにすき間が見えた）
          CurvedFalls(root, "Falls_Start", sPlaza1, SourceSurf + 0.005f, 0f, startLipHW * 2f + 2f * Mathf.Min(startLipHW, 0.45f) * FuwaStage2WaterTest.FallsRound, Mathf.Max(0.5f, olip.y - startLipZ), 0.9f, new Vector2(0f, startLipZ), Vector2.up, startLipZ - startWaterEndZ + 0.25f); }   // +0.25：滝の上の面を池の水面の下へ少し重ねる（さかい目にすき間の線が見えた。2026-10-27）   // 押す箱の幅も見た目の幅（はしに当てると押されずに岩に止まった。2026-10-27 てじゃ「ギリギリで当てると岩にあたって止まっちゃう」）   // 厚み 0.45→0.9（てじゃ「太いっていうより２重ってかんじ」）
        // ⑥ ラスト：よけられない滝、下の川、右岸のゴールの広場
        Finale(root, terr, ground, bedMat, fx, splash);

        // ---- 蓮の葉（固定）：池の手前の桟橋から向こうの坂まで ----
        // ジグザグにばらけて、葉っぱのふちからふちまで LilyGapMin〜Max（走ってジャンプでギリ届く）。ところどころ深い所（濃い色、人は落ちたらアウト）。
        // 浅い所に落ちたら、出口のふちの内側の石段まで歩いて上がる（2026-10-20 てじゃ「もうちょいバラバラに配置、ギリ届くくらいにして　所々深い場所も作ろう」）。
        // まん中の深い所を蓮で突っ切ると速い、外周の浅瀬をじゃぶじゃぶ回ると遅いけど安全（てじゃ「外周の蓮なくてもいいかもね」→ 寄り道の葉っぱはなし）
        var lilies = new GameObject("LilyPads").transform; lilies.SetParent(root, false);
        var pads = new List<Vector3>();   // x,z＝コースのローカル、y＝半径
        {
            var rng = new System.Random(2101);
            float Rnd() => (float)rng.NextDouble();
            Vector2 W(float s, float lat) { Center(s, out var p, out var d); return p + Right(d) * lat; }
            // よける所：右の水源の滝、左のあふれ口
            float smid = (sPond0 + sPond1) * 0.5f;
            var avoid = new[] { W(smid, PondHW(smid) - 0.5f), W(PondSpill(), -PondHW(PondSpill()) + 0.5f) };
            // 入口の岸のはし（中心線上）から始めて、出口の岸のはしへジャンプで届くまで
            Vector2 cur = W(sPond0 - RimW, 0f); float curR = 0f, curS = sPond0 - RimW; int side = Rnd() < 0.5f ? 1 : -1;
            for (int guard = 0; guard < 20; guard++)
            {
                if ((sPond1 + RimW) - curS - curR <= LilyGapMax - 0.1f && pads.Count > 0) break;   // 出口の岸に届く
                float r = 0.78f + 0.17f * Rnd(), gap = Mathf.Lerp(LilyGapMin, LilyGapMax, Rnd());
                float want = curR + gap + r;
                Vector2 best = Vector2.zero; float bestScore = float.MaxValue, bestS = 0;
                for (float s = curS + 0.5f; s < sPond1; s += 0.2f)
                {
                    float lim = PondHW(s) - r - 0.4f; if (lim < 0) continue;
                    for (float lat = -lim; lat <= lim; lat += 0.2f)
                    {
                        var q = W(s, lat);
                        float err = Mathf.Abs((q - cur).magnitude - want); if (err > 0.15f) continue;
                        bool bad = false;
                        foreach (var a in avoid) if ((q - a).magnitude < r + 1.6f) bad = true;
                        foreach (var pz in pads) if ((q - new Vector2(pz.x, pz.z)).magnitude < r + pz.y + 1.2f) bad = true;
                        if (bad) continue;
                        // ジグザグ：今の向きの側（side）へ。進みすぎず、横に振る
                        // まん中（深い所）を突っ切る：横の振れは小さめ
                        float wantLat = side * Mathf.Min(lim, 0.5f + 1.1f * Rnd());
                        float score = Mathf.Abs(lat - wantLat) + err * 4f + Mathf.Abs((s - curS) - want * 0.6f) * 0.5f;
                        if (score < bestScore) { bestScore = score; best = q; bestS = s; }
                    }
                }
                if (bestScore == float.MaxValue) break;
                pads.Add(new Vector3(best.x, r, best.y));
                cur = best; curR = r; curS = bestS; side = -side;
            }
            for (int i = 0; i < pads.Count; i++)
                FuwaStage2WaterTest.LilyPad(lilies, "Lily" + i, new Vector3(pads[i].x, 0.07f, pads[i].z), pads[i].y, i * 67f + 20f, false);
            // 大きい蓮の花（飾り）：葉っぱ・深い所・滝をよけてふちのそば
            for (int k = 0, tries = 0; k < 3 && tries < 400; tries++)
            {
                float s = Mathf.Lerp(sPond0 + 2.5f, sPond1 - 2.5f, Rnd());
                var q = W(s, (Rnd() < 0.5f ? 1f : -1f) * (PondHW(s) - 1.4f));
                bool bad = false;
                foreach (var a in avoid) if ((q - a).magnitude < 2.4f) bad = true;
                foreach (var pz in pads) if ((q - new Vector2(pz.x, pz.z)).magnitude < pz.y + 1.6f) bad = true;
                if (bad) continue;
                FuwaStage2WaterTest.BigLotus(lilies, "BigLotus" + k, new Vector3(q.x, 0.33f, q.y), 3.3f, k * 83f + 10f); k++;
            }
        }
        // 深い所：濃い色の円を水面のすぐ上に（見た目）＋人の落下判定（FuwaPlayerCatcher、水面の 0.15m 下から底まで）
        {
            var dg = new GameObject("DeepSpots").transform; dg.SetParent(root, false);
            var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
            // 材質：深い水の色、ふちは池の浅い色に溶かす（泡のふちはなし）
            const string dmp = "Assets/FuwaStage2/Materials/Stage2_WaterDeepSpot.mat";
            var shallowRef = WaterMat(0f, true, false);
            var deepMat = AssetDatabase.LoadAssetAtPath<Material>(dmp);
            if (!deepMat) { deepMat = new Material(shallowRef); AssetDatabase.CreateAsset(deepMat, dmp); }
            deepMat.CopyPropertiesFromMaterial(shallowRef);
            deepMat.SetColor("_Deep", DeepCenter); deepMat.SetFloat("_DeepWidth", 0.9f); deepMat.SetFloat("_FoamWidth", 0f); deepMat.SetFloat("_Ribbon", 1f);
            EditorUtility.SetDirty(deepMat);
            // 池の形をひとまわり小さくした深い所が真ん中に1つ（ふちから PondShallowW m は浅瀬）。2026-10-20 てじゃ「池の形に近い感じで真ん中は深い感じにしよう」（前は丸い深い所が3つ）
            // 入口・出口の側も丸く（池のはしはとがっているので、だ円と重ねて小さい方）。外周がぐるっと同じくらいの幅の浅瀬の道になる
            //（2026-10-20 てじゃ「入り口と出口付近は丸く　外周に浅瀬の道があるイメージ、あともうちょい浅瀬を広く」）
            float d0 = PondDeep0, d1 = PondDeep1;
            Func<float, float> hwL = PondDeepL, hwR = PondDeepR;
            {
                var V = new List<Vector3>(); var T = new List<int>(); var U1 = new List<Vector4>(); var U2 = new List<Vector2>();
                const int across = 10; int n = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / 0.2f));
                for (int k = 0; k <= n; k++)
                {
                    float kk = k / (float)n, s = PondDeepMid + PondDeepA * -Mathf.Cos(kk * Mathf.PI);   // はしほど細かく（丸く見えるように）
                    float l = Mathf.Max(0.0005f, hwL(s)), r = Mathf.Max(0.0005f, hwR(s)), mid = (r - l) * 0.5f, hw = (l + r) * 0.5f;
                    Center(s, out var p, out var d); var rr = Right(d);
                    for (int i = 0; i <= across; i++)
                    {
                        float lat = Mathf.Lerp(-l, r, i / (float)across); var q = p + rr * lat;
                        V.Add(new Vector3(q.x, 0.006f, q.y));
                        U1.Add(new Vector4(lat - mid, hw, 99f, 99f)); U2.Add(new Vector2(s, 0));   // ふちからの距離＝hw−|lat−mid|
                    }
                }
                for (int k = 0; k < n; k++)
                    for (int i = 0; i < across; i++)
                    {
                        int a = k * (across + 1) + i, b = a + across + 1;
                        AddTriDir(V, T, a, b, a + 1, Vector3.up); AddTriDir(V, T, a + 1, b, b + 1, Vector3.up);
                    }
                var dm = new Mesh { name = "Course2_1_PondDeep", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                dm.SetVertices(V); dm.SetTriangles(T, 0); dm.SetUVs(0, V.Select(v => Vector2.zero).ToList()); dm.SetUVs(1, U1); dm.SetUVs(2, U2);
                dm.normals = V.Select(v => Vector3.up).ToArray(); dm.RecalculateBounds();
                var deep = new GameObject("PondDeep"); deep.transform.SetParent(dg, false);
                deep.AddComponent<MeshFilter>().sharedMesh = Save(dm, MeshDir + "/PondDeep.asset");
                deep.AddComponent<MeshRenderer>().sharedMaterial = deepMat;
            }
            // 人の落下判定：中心線にそって 1.2m ごとの箱（幅はその区間のいちばん細い所。見た目の濃い所より少し内側）
            int nb = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / 1.2f));
            for (int k = 0; k < nb; k++)
            {
                float a = Mathf.Lerp(d0, d1, k / (float)nb), b = Mathf.Lerp(d0, d1, (k + 1) / (float)nb);
                float lm = float.MaxValue, rm = float.MaxValue;
                for (int i = 0; i <= 4; i++) { float si = Mathf.Lerp(a, b, i / 4f); lm = Mathf.Min(lm, hwL(si)); rm = Mathf.Min(rm, hwR(si)); }
                float hwm = (lm + rm) * 0.5f;
                if (hwm < 0.3f) continue;
                Center(a, out var pa, out _); Center(b, out var pb, out var db);
                var c = new GameObject("PlayerFall" + k); c.transform.SetParent(dg, false);
                var mid = (pa + pb) * 0.5f + Right(db) * ((rm - lm) * 0.5f);
                // 穴の底から PondFallTop まで（浅瀬の底より 0.2m 下＝穴に沈んでから。2026-10-23）
                c.transform.localPosition = new Vector3(mid.x, (PondFallTop + PondHoleBed) * 0.5f, mid.y);
                c.transform.localRotation = Quaternion.LookRotation(new Vector3(pb.x - pa.x, 0, pb.y - pa.y));
                var bc = c.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3((hwm - 0.05f) * 2f, PondFallTop - PondHoleBed, (pb - pa).magnitude);
                var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(c); pc.ball = ballProxy;
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); NoSync(pc);
            }
            // 出口の石段は 2026-10-23 にやめた（浅くしてどこからでも上がれる。ふわふわが引っかかる）
        }
        // 蓮の池の水源：右のふちの岩から小さな滝が流れ込む（上の川は谷に落ちるので、池には別の水源。2026-10-20 てじゃ「次の池には適当に水源作って流し込もう」）。
        // 置き場所は大きい蓮の花のない側（まん中の花は左）。岩の面は池のふちの内側ぎりぎり（ふちの内側の面と重ねるとZファイト）
        {
            float sm = (sPond0 + sPond1) * 0.5f;
            Center(sm, out var pp, out var pd); var pr = Right(pd);
            float face = PondHW(sm) - 0.03f, depthR = 3f, topY = 2.6f;
            var rc = pp + pr * (face + depthR * 0.5f);
            var rock = Box(terr, "PondSpringRock", new Vector3(rc.x, (Base - 0.4f + topY) * 0.5f, rc.y), new Vector3(3.6f, topY - Base + 0.4f, depthR), ground, 0);
            rock.transform.localRotation = Quaternion.LookRotation(new Vector3(pr.x, 0, pr.y));
            // 見た目は上が平らなごつごつ岩（2026-10-25、箱は当たり判定だけ）。上の面（メッシュの y 0.3）が topY にそろうように
            rock.GetComponent<MeshRenderer>().enabled = false;
            {
                float hgt = topY - Base + 0.4f;
                var rv = new GameObject("PondSpringRockVisual"); rv.transform.SetParent(terr, false);
                rv.AddComponent<MeshFilter>().sharedMesh = BoulderMesh(7, true);
                rv.AddComponent<MeshRenderer>().sharedMaterial = StoneMat();
                rv.transform.localRotation = rock.transform.localRotation;
                rv.transform.localScale = new Vector3(3.6f * 1.15f, hgt / 0.8f, depthR * 1.15f);
                rv.transform.localPosition = new Vector3(rc.x, topY - 0.3f * rv.transform.localScale.y + 0.02f, rc.y);
            }
            var fc = pp + pr * (face - 0.12f);
            FuwaStage2WaterTest.Waterfall(root, "Falls_PondSpring", new Vector3(fc.x, 0, fc.y), topY - 0.05f, 0f, 1.4f, new Vector3(-pr.x, 0, -pr.y));
            // 岩の上の小さな湧き水の池（見た目）
            var spring = GameObject.CreatePrimitive(PrimitiveType.Quad); spring.name = "PondSpringTop"; spring.transform.SetParent(terr, false);
            Object.DestroyImmediate(spring.GetComponent<Collider>());
            var sc2 = pp + pr * (face + 1.1f);
            spring.transform.localPosition = new Vector3(sc2.x, topY + 0.01f, sc2.y);
            spring.transform.localRotation = Quaternion.LookRotation(Vector3.down, new Vector3(-pr.x, 0, -pr.y));
            spring.transform.localScale = new Vector3(1.4f, 2.0f, 1f);
            spring.GetComponent<Renderer>().sharedMaterial = WaterMat(0f, false);
            // この水も、上に浮かぶ小さい青い球から（スタートの水源と同じ「ふしぎな青い球」）
            WaterOrb(terr, new Vector3(sc2.x, topY, sc2.y), "PondOrb", 1.6f, 0.26f, 0.2f);
        }
        // 反対側（左のふち）の切れ目から池の水があふれて、島の外へ流れ落ちる（飾り。2026-10-20 てじゃ「反対側あたりに流れ落ちてる場所もつくっとこ」）。
        // ふわふわが切れ目から外へ出ないように、見えない壁（当たるだけ、アウトにはしない）
        {
            float sg = PondSpill();
            Center(sg, out var gp3, out var gd3); var gr = Right(gd3);
            float hwg = PondHW(sg);
            var lipW = GameObject.CreatePrimitive(PrimitiveType.Quad); lipW.name = "PondSpillLip"; lipW.transform.SetParent(terr, false);
            Object.DestroyImmediate(lipW.GetComponent<Collider>());
            var lc2 = gp3 - gr * (hwg + RimW * 0.5f);
            lipW.transform.localPosition = new Vector3(lc2.x, 0.005f, lc2.y);
            lipW.transform.localRotation = Quaternion.LookRotation(Vector3.down, new Vector3(-gr.x, 0, -gr.y));
            lipW.transform.localScale = new Vector3(2.2f, RimW + 0.1f, 1f);
            lipW.GetComponent<Renderer>().sharedMaterial = WaterMat(0f, false);
            var oc = gp3 - gr * (hwg + RimW + 0.05f);
            FuwaStage2WaterTest.Waterfall(root, "Falls_PondSpill", new Vector3(oc.x, 0, oc.y), 0.02f, -8f, 2.0f, new Vector3(-gr.x, 0, -gr.y));
            var guard = Box(terr, "PondSpillGuard", new Vector3(lc2.x, 0.6f, lc2.y), new Vector3(2.4f, 1.3f, RimW), null, 0);
            guard.transform.localRotation = Quaternion.LookRotation(new Vector3(gr.x, 0, gr.y));
            Object.DestroyImmediate(guard.GetComponent<Renderer>()); Object.DestroyImmediate(guard.GetComponent<MeshFilter>());
        }

        // ---- チェックポイント（2026-10-25 てじゃ「チェックポイントおいてくかぁ」→ 4つ：魚の渡り場の先・蓮の池の先・貝の道の先・三連滝の先の踊り場）----
        {
            float s1 = sCross2 + CrossGap * 0.5f + 2.3f;   // 2026-10-25 てじゃ「ちょっと奥に」+1.2→+2.3
            Checkpoints(root, new[]
            {
                (s1, (Func<float, float>)(s => RiverHW(Mathf.Min(s, sRiver1)) + RimW + 1.55f), 1.5f, 1.5f, BankTop),
                (sStraight0 + 0.5f, (Func<float, float>)(s => 0f), WalkHW, WalkHW, BankTop),   // まっすぐな道の上（キノコは判定の約3m先＝貝の道の手前）
                // 水路の側のキノコは水路の外まで（てじゃ「右を完全に川の外側までもっていって」）
                (sWalk1 + 0.5f, (Func<float, float>)(s => 0f), WalkHW, WalkHW, FallsH),   // 貝の道と三連滝の道のあいだのまっすぐな道（水路の横だと配置がまずかった）
                (sF1 - 3.5f, (Func<float, float>)(s => 0f), RoadHW, RoadHW, FallsH),   // 踊り場の上だとキノコが左右の岩の壁にうまった→ 滝の道の終わりに（キノコは判定の約3m先に立つので、踊り場にかからないように）
            });
        }

        // ---- スタート・ゴール（2026-10-23 てじゃ「スタートはステージ1と同じように、ロビー戻りポータル島、スタート島、そこから洞窟に歩いて入っていく」
        //      「ゴールはとりあえず川の向こうに一旦ゴール島として浮かせてから考えよう」）----
        StartSet(root);
        RegisterBgm(root);
        var goal = new GameObject("Goal").transform; goal.SetParent(root, false);
        GoalSet(goal);

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
        // ロビーのワープ（WarpToCourse0）はスタートの BallStart/PlayerSpot を指しているので、作り直したらつなぎ直す
        //（2026-10-21 作り直したあとワープが効かず、キノコからふわふわが出なかった）
        // 道と水の下の岩の土台（浮島の形）。道・水がそろってから、上から測って作る
        WoodWalk(root, woodPieces);
        RockBases(root);
        PondSpillLipFalls(root);
        PondSpringFalls(root);
        AirFallsFray(root);
        FuwaStage2LobbyCourses.Run();
        FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[2–1] 長さ " + sEnd.ToString("F0") + "m  台→池 " + sPlaza1.ToString("F1") + " 川 " + sPool1.ToString("F1") + "–" + sRiver1.ToString("F1")
            + " 蓮 " + sPond0.ToString("F1") + "–" + sPond1.ToString("F1") + " 貝の道 " + sWalk0.ToString("F1") + "–" + sWalk1.ToString("F1") + " 滝の道 " + sF0.ToString("F1") + "–" + sF1.ToString("F1") + " ゴール " + sGoal0.ToString("F1");
    }

    // ---- スタート・ゴール（2026-10-23）----
    // ステージ1のコース1のスタート一式・ゴール一式を写して使う（てじゃ「最終的にはステージ1のスタートとゴールの配置を統一したい。小島から小島のやつ」）。
    // 写した物の中の参照は写した物へ付け替え、ワープの戻り先はステージ2のロビー、日本語・英語の切り替えとエアガンの台も登録する

    // 写す元 src の子（names が null なら全部）を dst の下に写す。元の物 → 写した物 の対応表を map に足す
    static void CopyChildren(Transform src, Transform dst, string[] names, Dictionary<Object, Object> map)
    {
        var list = names == null ? src.Cast<Transform>().ToList() : names.Select(n => src.Find(n)).Where(t => t).ToList();
        foreach (var s in list)
        {
            var c = Object.Instantiate(s.gameObject, dst, false); c.name = s.name;
            c.transform.localPosition = s.localPosition; c.transform.localRotation = s.localRotation; c.transform.localScale = s.localScale;
            var sAll = s.GetComponentsInChildren<Transform>(true); var cAll = c.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < sAll.Length && i < cAll.Length; i++)
            {
                map[sAll[i]] = cAll[i]; map[sAll[i].gameObject] = cAll[i].gameObject;
                var sc = sAll[i].GetComponents<Component>(); var cc = cAll[i].GetComponents<Component>();
                for (int k = 0; k < sc.Length && k < cc.Length; k++) if (sc[k]) map[sc[k]] = cc[k];
            }
        }
    }

    // 写した物の U# の参照を付け替える。ロビーへのワープはステージ2のロビーへ。日本語・英語の文字とエアガンの台を FuwaLanguage に登録
    static void FixCopied(Transform dst, Dictionary<Object, Object> map)
    {
        var lobby2 = GameObject.Find("FuwaStage2/Lobby")?.transform;
        foreach (var mb in dst.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true))
        {
            var so = new SerializedObject(mb); var it = so.GetIterator();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue && map.TryGetValue(it.objectReferenceValue, out var to)) it.objectReferenceValue = to;
            if (mb is FuwaGoal) so.FindProperty("album").objectReferenceValue = null;   // 名場面アルバムは2–1のフォトポイントができるまでなし
            if (mb is FuwaCourseWarp && lobby2)
            {
                so.FindProperty("courseStart").objectReferenceValue = lobby2.Find("BallPark");
                so.FindProperty("playerSpot").objectReferenceValue = lobby2.Find("LobbySpawn");
                so.FindProperty("gunSpot").objectReferenceValue = lobby2.Find("GunStand_Lobby/GunSpot");
                so.FindProperty("stage").intValue = 2;
            }
            so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(mb);
        }
        var lang = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(x => x && x.gameObject.scene.IsValid() && x.GetType().Name == "FuwaLanguage");
        if (!lang) return;
        var ls = new SerializedObject(lang);
        var texts = ls.FindProperty("texts"); var ja = ls.FindProperty("ja"); var en = ls.FindProperty("en");
        for (int k = texts.arraySize - 1; k >= 0; k--)
            if (texts.GetArrayElementAtIndex(k).objectReferenceValue == null) { texts.DeleteArrayElementAtIndex(k); ja.DeleteArrayElementAtIndex(k); en.DeleteArrayElementAtIndex(k); }
        int n0 = texts.arraySize;
        for (int k = 0; k < n0; k++)
        {
            var o = texts.GetArrayElementAtIndex(k).objectReferenceValue;
            if (!o || !map.TryGetValue(o, out var to)) continue;
            int j = texts.arraySize; texts.arraySize++; ja.arraySize++; en.arraySize++;
            texts.GetArrayElementAtIndex(j).objectReferenceValue = to;
            ja.GetArrayElementAtIndex(j).stringValue = ja.GetArrayElementAtIndex(k).stringValue;
            en.GetArrayElementAtIndex(j).stringValue = en.GetArrayElementAtIndex(k).stringValue;
        }
        var stands = ls.FindProperty("stands");
        for (int k = stands.arraySize - 1; k >= 0; k--) if (stands.GetArrayElementAtIndex(k).objectReferenceValue == null) stands.DeleteArrayElementAtIndex(k);
        foreach (var st in dst.GetComponentsInChildren<FuwaGunStand>(true)) { stands.arraySize++; stands.GetArrayElementAtIndex(stands.arraySize - 1).objectReferenceValue = st; }
        ls.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)lang);
    }

    // スタート：コース1の Start を丸ごと（ロビーへ戻るポータルの島 → 木の橋 → キノコの島。エアガンの台つき）。
    // キノコの島から、もう1本の木の橋で洞窟の奥の壁の入口へ。人は歩いて洞窟に入って行く
    // 洞窟の奥の壁の入口（半分の幅・高さ）。2026-10-25 岩山のアーチ（Stage2_CaveRock の Cut_Arch：幅±2.6・天井まで）に合わせて 1.3/2.4 → 2.6/2.6
    public static float M1MouthS = 5.5f, M1MouthW = 3.56f;   // 洞窟の前の出口の始まり（s）と、そこからの床の幅
    const float DoorHW = 2.25f, DoorH = 2.6f;   // 2026-10-27 2.6→2.25（岩山の入口のトンネルをせまくした。Tools/stage2_cave_rock.py の ARCH_HW と同じ）
    static float CaveBackS => sPlaza0 - 1.5f - 0.4f;   // 洞窟の奥の壁の外の面
    static void StartSet(Transform root)
    {
        var start = new GameObject("Start").transform; start.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–1] FuwaCourse/Start がない"); return; }
        var map = new Dictionary<Object, Object>();
        CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_1");   // GunStand_Course1 → GunStand_Course2_1
        // 島の地面の高さ（コース1の Start の座標で、キノコの根もと）→ 洞窟の床（StartH）にそろえる
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea");
        var ground = area.Find("Ground"); var bridge = area.Find("Bridge"); var bridgeWood = area.Find("BridgeWood");
        // キノコの島から洞窟への橋：ポータル側の橋を、島の中心で前に折り返した位置。橋の先は洞窟の入口に 0.5m 入る
        var bc = bridge.GetComponent<BoxCollider>();
        float halfLen = (bc ? bc.size.z : 1f) * bridge.localScale.z * 0.5f;
        float frontZ = 2f * ground.localPosition.z - bridge.localPosition.z;   // 洞窟側の橋の中心
        float doorZ = frontZ + halfLen - 0.5f;                                 // ここを洞窟の奥の壁の外の面に合わせる
        Center(CaveBackS - doorZ, out var sp, out var sd);
        start.localPosition = new Vector3(sp.x, StartH - islandTop, sp.y); start.localRotation = Quaternion.LookRotation(new Vector3(sd.x, 0, sd.y));
        foreach (var b in new[] { bridge, bridgeWood })
        {
            var c = Object.Instantiate(b.gameObject, area, false); c.name = b.name + "_Cave";
            c.transform.localPosition = new Vector3(b.localPosition.x, b.localPosition.y, 2f * ground.localPosition.z - b.localPosition.z);
            c.transform.localRotation = Quaternion.Euler(0, 180f, 0) * b.localRotation; c.transform.localScale = b.localScale;
        }
        FixCopied(start, map);
        Stage2Grass(start);
    }

    // ゴール：コース1の Goal を丸ごと（島・柵・リング・判定・キノコの輪・▼GOAL・CLEAR!・花火・ロビーへのワープ）。
    // 川が右へ曲がった先の、外側の岸に置く。左岸の道がまっすぐのびて島の入口（柵の切れ目＝島の後ろ）につながる
    //（2026-10-23 てじゃのスケッチ。前は川の右岸の向こうに浮かせた仮の島）
    const float GoalGap = 0.3f;   // 島の下の岩と、カーブの外側の岸とのすき間
    // 島の半径（Unity の座標で、島の中心から）。地面＝人が乗る所、下の岩＝いちばん外
    static float GoalR(string n)
    {
        var t = GameObject.Find("FuwaCourse/Goal")?.transform.Find(n); var mf = t ? t.GetComponent<MeshFilter>() : null;
        if (!mf || !mf.sharedMesh) return 3f;
        var e = mf.sharedMesh.bounds; var sc = t.localScale;
        return Mathf.Max(Mathf.Abs(e.max.x * sc.x), Mathf.Abs(e.min.x * sc.x), Mathf.Abs(e.max.z * sc.z), Mathf.Abs(e.min.z * sc.z));
    }
    // 島の置き場所：左岸の道（踊り場から下りきった所から先はまっすぐ）の延長上で、島の下の岩が川のカーブの外側の岸から GoalGap 離れる所まで進める
    static void GoalSpot()
    {
        float rUnder = Mathf.Max(GoalR("IslandUnder"), GoalR("GoalGround")), rGround = GoalR("GoalGround");
        // まっすぐな所はカーブの始まりから（その手前は川にそう。2026-10-23 滝の道を短くしてラストの川がくねくねした所から始まるようになった）
        Center(sBend0, out var wp, out var wd); var w0 = wp + Right(wd) * (-FinRiverHW - FinWalkW * 0.5f);
        float need = FinRiverHW + FinBankW + GoalGap + rUnder;
        float River(Vector2 q) { float m = float.MaxValue; for (float s = sF1 + FinRiver0; s <= sFin1; s += 0.1f) { Center(s, out var p, out _); m = Mathf.Min(m, (q - p).magnitude); } return m; }
        // 2026-10-26 てじゃ「島に続く道は木の橋にしよう」：シマシマの道は川岸からはなれた所（tLeave）で終わって、その先はスタートと同じ木の橋で島へ
        GoalBridgeDims(out float bridgeFar, out float bridgeTop);
        float tLeave = 0f;
        while (tLeave < 40f && River(w0 + wd * tLeave) < FinRiverHW + FinBankW + FinWalkW * 0.5f + GoalWalkClear) tLeave += 0.05f;
        float t = tLeave + bridgeFar - GoalWalkUnder;
        while (t < 60f && River(w0 + wd * t) < need) t += 0.05f;
        goalC = w0 + wd * t; goalD = wd; walkP0 = w0;
        sWalkEnd = sBend0 + t - bridgeFar + GoalWalkUnder;   // 道のはしは橋の下に少し入れる
        walkEndH = goalH + 0.02f + bridgeTop - 0.03f;        // 道のはしは橋の板の上の面より少し下（板がかぶさる）
    }
    const float GoalWalkClear = 0.5f, GoalWalkUnder = 0.3f;   // 道が川岸からはなれるすき間、道が橋の下に入る長さ
    static float walkEndH;
    // 橋の寸法（コース1のスタートの島の橋から、ゴールの島の地面の中心をもとに）：橋の遠いはしまでの距離、橋の上の面の高さ（ゴールの原点から）
    static void GoalBridgeDims(out float far, out float top)
    {
        far = 6.1f; top = 0.11f;
        var area = GameObject.Find("FuwaCourse/Start/StartArea")?.transform; var gg = GameObject.Find("FuwaCourse/Goal/GoalGround")?.transform;
        var ground = area ? area.Find("Ground") : null; var bridge = area ? area.Find("Bridge") : null; var bc = bridge ? bridge.GetComponent<BoxCollider>() : null;
        if (!ground || !bc || !gg) return;
        far = Mathf.Abs(ground.localPosition.z - bridge.localPosition.z) + bc.size.z * bridge.localScale.z * 0.5f;
        top = gg.localPosition.y + (bridge.localPosition.y - ground.localPosition.y) + (bc.center.y + bc.size.y * 0.5f) * bridge.localScale.y;
    }
    static void GoalSet(Transform goal)
    {
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–1] FuwaCourse/Goal がない"); return; }
        var map = new Dictionary<Object, Object>();
        CopyChildren(src, goal, null, map);
        goal.localPosition = new Vector3(goalC.x, goalH + 0.02f, goalC.y); goal.localRotation = Quaternion.LookRotation(new Vector3(goalD.x, 0, goalD.y));
        // 島の入口（後ろ＝-z）に木の橋（コース1のスタートの島の橋と同じ物を、同じ地面との位置関係で）
        var area = GameObject.Find("FuwaCourse/Start/StartArea")?.transform; var ground = area ? area.Find("Ground") : null; var gg = goal.Find("GoalGround");
        if (ground && gg)
            foreach (var nm in new[] { "Bridge", "BridgeWood" })
            {
                var b = area.Find(nm); if (!b) continue;
                var c = Object.Instantiate(b.gameObject, goal, false); c.name = nm;
                c.transform.localPosition = new Vector3(0f, gg.localPosition.y + b.localPosition.y - ground.localPosition.y, -Mathf.Abs(ground.localPosition.z - b.localPosition.z));
                c.transform.localRotation = b.localRotation; c.transform.localScale = b.localScale;
            }
        FixCopied(goal, map);
        Stage2Grass(goal);
    }
    // 島の芝をステージ2の青っぽい芝に（2026-10-26 てじゃ「ステージ２の芝は青っぽいのに統一」）
    static void Stage2Grass(Transform t)
    {
        var g2 = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_LobbyGround.mat"); if (!g2) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++) if (ms[i] && ms[i].name == "Goal_Ground") { ms[i] = g2; ch = true; }
            if (ch) r.sharedMaterials = ms;
        }
    }

    // ---- ④ 貝の道（2026-10-21）----
    // 細い堤の道（M3_ClamWalk）の下の土台、両わきの崖、貝（左右交互）、貝の反対側の滝、道の外に落ちた時のアウト判定、泡（FuwaClamBubbles）。
    // 見た目（砂浜・岩場・本物の貝）はあとでまとめて。今は形と動きだけ
    // 貝の道の長さに対する位置・置く側（-1＝左）・高い台か。ふつうの貝（左右交互）のあいだに、高い台の貝を足す
    // （2026-10-22 てじゃ「まだ上から回避できる」「各貝の間にさらに貝追加して、ぐっと高い位置を飛ぶ形に」「上回避するとそれにあたるみたいな」）
    public static float[] ClamAt = { 0.14f, 0.26f, 0.38f, 0.5f, 0.62f, 0.74f, 0.86f };   // 2026-10-23 0.1おき（約2.5m）→0.12おき（約3m）（てじゃ「貝は間隔ちょっと広げたい」）
    public static float[] ClamSide = { -1f, 1f, 1f, -1f, -1f, 1f, 1f };
    public static bool[] ClamHigh = { false, true, false, true, false, true, false };
    public static float ClamPeriod = 2.8f;
    // 泡の大きさ（直径）と包む距離。0.62 だと上から軽く越えられそう（2026-10-21 てじゃ「あわはもっとデカくしよ」）→ 1.5 → 1.8（2026-10-22 てじゃ「ちょっとサイズアップ」）
    public static float ClamBubbleSize = 1.8f, ClamCapture = 1.05f;
    // 貝の置き場所の高さ（道の上からの、貝の底の高さ）。前は道より 0.15m 低い岩の上で、泡の上を越えられそうだった（2026-10-22 てじゃ「貝をちょっと高い台に置く」）
    public static float ClamBase = 0.35f;
    // 高い台の貝の底の高さ（道の上から）。泡は道の上 3.15m あたりから出て、ふつうの泡の上（〜2.4m）を越えようとすると当たる
    public static float ClamHighBase = 2.2f;
    static float ClamPhase(int i)
    {
        int nLow = ClamHigh.Count(h => !h);
        float Low(int k) { int j = 0; for (int q = 0; q < k; q++) if (!ClamHigh[q]) j++; return j * ClamPeriod / nLow; }   // k 番目より前のふつうの貝の数で等分
        if (!ClamHigh[i]) return Low(i);
        int a = i - 1, b = i + 1;   // 両どなりはふつうの貝（並びは ふつう・高い・ふつう…）
        float m = (Low(a) + Low(b)) * 0.5f;
        return (m + ClamPeriod * 0.5f) % ClamPeriod;
    }
    // ---- スタートの岩山（2026-10-25）----
    // てじゃ「岩ベースにはしたいけど、最初の洞窟みたいになってるとこがロビーからみてダサい」「滝みえてほしい」→ 岩山＋ロビー側は大きな岩のアーチ＋てっぺんの池から細い滝2本。
    // 形は Blender（FLUFF_models.blend の Stage2Cave シーン、Stage2_CaveRock。CaveRock を Cut_* でくりぬく）で、座標はコースのローカルそのまま。
    // 箱の壁（CaveWall*・CaveBack*・CaveMouth*・CaveRoof・CaveFloorCliff・CaveDoorSill）は見た目だけ消して当たり判定は残す。岩山にも当たり判定（メッシュ）
    static float startLipZ, startLipHW = MouthHW, startWaterEndZ;
    const float ExitStripZ0 = 4.6f, ExitWaterEndZ = 6.2f;   // 水源の池の出口の水面の帯の始まり、終わり（Blender の出口の壁がまっすぐになる所 EX_Z1 と同じ）
    const string CaveRockModel = "Assets/FuwaStage2/Models/Stage2_CaveRock.fbx";
    // ロビー側の細い滝（岩山のてっぺんの溝 Cut_FallL/R）：x・溝の底の高さ
    static readonly (float x, float floor)[] BackFalls = { (-1.45f, 5.45f), (1.8f, 5.55f) };
    static void CaveRock(Transform root, Transform terr)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(CaveRockModel); if (!src) { Debug.LogWarning("[2–1] 岩山のモデルがない"); return; }
        var mesh = AssetDatabase.LoadAllAssetsAtPath(CaveRockModel).OfType<Mesh>().First();
        var go = new GameObject("CaveRock"); go.transform.SetParent(terr, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_CaveRock.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_LobbyGround.mat"),
        };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        // 影は両面で落とす（片面だと、くり抜いた薄い所・ふちから光がもれて、入口の橋の横にまだらの光の模様が出た。2026-10-27 てじゃ）
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        // 出口の本当のふち（池の床の高さの頂点で、いちばん前）
        startLipZ = mesh.vertices.Where(v => Mathf.Abs(v.x) < 1.2f && v.y < SourceSurf - 0.1f && v.y > SourceSurf - 1f).Select(v => v.z).DefaultIfEmpty(sPlaza1).Max();
        foreach (Transform c in terr)
            if (c.name.StartsWith("CaveWall") || c.name.StartsWith("CaveBack") || c.name.StartsWith("CaveMouth") || c.name == "CaveRoof" || c.name == "CaveFloorCliff" || c.name == "CaveDoorSill")
            { var r = c.GetComponent<MeshRenderer>(); if (r) r.enabled = false; }
        // 水源の池の芝色の箱（入れ物・ふち）は見た目だけ消して、底は岩の色に（2026-10-26 てじゃ「ベースになった芝のブロックが、各所で邪魔してる」：溝の前に芝の箱が顔を出していた）
        var src0 = terr.parent ? terr.parent.Find("Water/W0_Source") : null;
        if (src0)
        {
            foreach (var nm in new[] { "Body", "RimL", "RimR", "RimStart", "RimL_Rock", "RimR_Rock", "RimStart_Rock" }) { var t = src0.Find(nm); if (t && t.GetComponent<Renderer>()) t.GetComponent<Renderer>().enabled = false; }
            var bed0 = src0.Find("Bed"); if (bed0 && bed0.GetComponent<Renderer>()) bed0.GetComponent<Renderer>().sharedMaterial = RockBaseMat();
            SourceSurfaceToBasin(src0, go.GetComponent<MeshCollider>());
        }
        Spring(terr, new Vector3(0f, SourceSurf, 1.6f));
        // 水源の池の模様は、湧き水から外へ広がる輪と筋（2026-10-27 てじゃ「上の池の模様も、中心から外に広がるかんじに」）。ほかの止まった水と材質を分ける
        var srcSurf = src0 ? src0.Find("Surface") : null;
        if (srcSurf)
        {
            const string sp = "Assets/FuwaStage2/Materials/Stage2_WaterRibbon_SourceRadial.mat";
            var baseM = WaterMat(0f, true, false);
            var rm = AssetDatabase.LoadAssetAtPath<Material>(sp);
            if (!rm) { rm = new Material(baseM); AssetDatabase.CreateAsset(rm, sp); }
            rm.CopyPropertiesFromMaterial(baseM);
            var wc = terr.TransformPoint(new Vector3(0f, SourceSurf, 1.6f));
            rm.SetFloat("_DeepWidth", 0.3f);   // かべの近くだけ少し明るく（広いと滝の上の面と色がずれる）
            rm.SetFloat("_Radial", 1f); rm.SetVector("_RadialCenter", new Vector4(wc.x, wc.z, RippleFadeR0, RippleFadeR1));   // zw＝波紋を消す半径（滝の口は中心から 4.8〜5.2m）
            EditorUtility.SetDirty(rm);
            srcSurf.GetComponent<MeshRenderer>().sharedMaterial = rm;
            var ptm = AssetDatabase.LoadAssetAtPath<Material>(FallsPondTopMat); if (ptm) PondLookToFallsTop(ptm);
        }
        WaterOrb(terr, new Vector3(0f, SourceSurf, 1.6f));
        // ロビー側の細い滝：溝の水（池のふちから岩山の後ろのはしまで）＋落ちる滝
        var mesh2 = mesh; var b = mesh2.bounds;
        foreach (var (x, floor) in BackFalls)
        {
            // 岩山の後ろのはし（溝の所）：上から下へレイで探す代わりに、メッシュの頂点で一番後ろ（z が小さい）を見る
            float zBack = mesh2.vertices.Where(v => Mathf.Abs(v.x - x) < 0.35f && v.y > floor - 0.6f).Select(v => v.z).DefaultIfEmpty(b.min.z).Min();
            // 溝の水：池の側は池の水面のすぐ下から始めて（前は池の水面より上の高さで、池のはしで板のはしが浮いて見えた。2026-10-27 てじゃ）、
            // 後ろのはし（こぼれる所）は溝の底の少し上へ下がる。幅も溝の形にあわせて、池の側は広く後ろは細く
            var q = new GameObject("BackFallChannel"); q.transform.SetParent(terr, false);
            float z0 = -1.45f, yA = SourceSurf - 0.005f, yB = floor + 0.12f;
            var cm = new Mesh { name = "Course2_1_BackFallChannel" + (x < 0 ? "L" : "R") };
            cm.SetVertices(new List<Vector3> { new Vector3(x - 0.45f, yA, z0), new Vector3(x + 0.45f, yA, z0), new Vector3(x + 0.25f, yB, zBack), new Vector3(x - 0.25f, yB, zBack) });
            cm.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            cm.SetTriangles(new[] { 0, 3, 2, 0, 2, 1 }, 0); cm.RecalculateNormals(); cm.RecalculateBounds();
            if (Vector3.Dot(cm.normals[0], Vector3.up) < 0) { cm.SetTriangles(new[] { 0, 2, 3, 0, 1, 2 }, 0); cm.RecalculateNormals(); }
            // 池の水面と同じ材質・同じ uv（ふちの泡なし・同じ色・同じ波紋）にして、池からそのまま続く水に（2026-10-27 てじゃ「滝と水面の境目、もっときっちり繋ぐ」「細い滝の方も」：
            // 前は別の水の板で、色がちがって まわりに白いふちの線が出ていた）
            cm.SetUVs(1, Enumerable.Repeat(new Vector4(0f, 99f, 99f, 99f), 4).ToList()); cm.SetUVs(2, Enumerable.Repeat(Vector2.zero, 4).ToList());
            cm = Save(cm, MeshDir + "/BackFallChannel" + (x < 0 ? "L" : "R") + ".asset");
            q.AddComponent<MeshFilter>().sharedMesh = cm;
            var srcSurfMat = src0 && src0.Find("Surface") ? src0.Find("Surface").GetComponent<MeshRenderer>().sharedMaterial : WaterMat(0f, true, false);
            q.AddComponent<MeshRenderer>().sharedMaterial = srcSurfMat;
            FuwaStage2WaterTest.Waterfall(terr, "BackFall", new Vector3(x, 0, zBack), floor + 0.14f, -10f, 0.45f, Vector3.back);   // 下は岩山の下のとがりより下まで（空に落ちていく）
            // 見た目は溝の水からそのままふちをこぼれる膜に（2026-10-26 てじゃ「入口の手前の滝、上からみるときれてる」→ 前の筒の滝は上が開いていて、溝の水と離れて見えた）。霧・音は残す
            var bfs = terr.GetComponentsInChildren<Transform>(true).Where(t => t.name == "BackFall").ToList();
            var bc = bfs.Count > 0 ? bfs[bfs.Count - 1].Find("Curtain") : null; if (bc && bc.GetComponent<Renderer>()) bc.GetComponent<Renderer>().enabled = false;
            var lipB = new List<Vector3>(); var outB = new List<Vector3>();
            for (int k = 0; k <= 4; k++) { lipB.Add(new Vector3(x + Mathf.Lerp(-0.25f, 0.25f, k / 4f), floor + 0.12f, zBack)); outB.Add(Vector3.back); }
            // 落ちていく膜は、下でふくらんだ岩（入口のアーチの柱）より外を通るように、深さごとに岩のいちばん後ろ＋すき間ぶん外へ（2026-10-27 てじゃ「細い滝が岩貫通してる」）。
            // 外へのずれは深さとともに増えるだけ（へらない）＝水が外へ弧をえがいて落ちる形
            var verts = mesh2.vertices;
            var prof = new List<Vector2> { new Vector2(0.25f, 0.01f), new Vector2(0.02f, 0.01f) };
            float off = 0.1f;
            for (float d = 0.1f; d < 13f; d += d < 1f ? 0.15f : 0.5f)
            {
                float y = floor + 0.12f - d, need = 0f;
                foreach (var v in verts) if (Mathf.Abs(v.x - x) < 0.35f && Mathf.Abs(v.y - y) < 0.4f) need = Mathf.Max(need, zBack - v.z + 0.15f);
                off = Mathf.Max(off, need, 0.1f + 0.12f * Mathf.Sqrt(d));
                prof.Add(new Vector2(-off, -d));
            }
            // 段がつかないように、外へのずれを先読みでなめらかに（いちばん外へ出る所に向かって前からゆるく出していく）
            for (int k = prof.Count - 2; k >= 2; k--) prof[k] = new Vector2(Mathf.Min(prof[k].x, prof[k + 1].x * 0.85f + prof[k].x * 0.15f), prof[k].y);
            for (int k = 3; k < prof.Count; k++) prof[k] = new Vector2(Mathf.Min(prof[k].x, prof[k - 1].x), prof[k].y);
            prof.Add(new Vector2(prof[prof.Count - 1].x, float.NaN));
            var lf = LipFalls(terr, "BackFallLip" + (x < 0 ? "L" : "R"), "BackFallLip", lipB, outB, prof, -10f);
            // 落ち口の手前（prof[0]→prof[1] の 0.23m）は池と同じ見た目（大きい滝の _PondTop と同じ材質）
            var ptm = AssetDatabase.LoadAssetAtPath<Material>(FallsPondTopMat);
            if (ptm)
            {
                const string bp = "Assets/FuwaStage2/Materials/Stage2_WaterfallCurvedThick_PondTopThin.mat";
                var bm = AssetDatabase.LoadAssetAtPath<Material>(bp);
                if (!bm) { bm = new Material(ptm); AssetDatabase.CreateAsset(bm, bp); }
                bm.CopyPropertiesFromMaterial(ptm); bm.SetFloat("_PondTop", (prof[0] - prof[1]).magnitude);
                EditorUtility.SetDirty(bm);
                lf.GetComponent<MeshRenderer>().sharedMaterial = bm;
            }
        }
    }

    // 水源の池の水面を岩山のくぼみいっぱいに（2026-10-26 てじゃ「ふちとったところも水がういてる」）：
    // 前の水面は箱のふちの内側の大きさで、岩のくぼみはそれより広くて 0.57m 低いので、水面のはしが宙に浮いて見えた。
    // 池のまん中から、岩の上の面が水面より低い所をたどって広げる（岩の壁＝水面より高い所・岩のない所で止まる）。前後は元の水面の範囲まで（溝・出口へはみ出さない）
    static void SourceSurfaceToBasin(Transform src, MeshCollider rock)
    {
        var surfT = src.Find("Surface"); if (!surfT || !rock) return;
        var old = surfT.GetComponent<MeshFilter>().sharedMesh; var ov = old.vertices;
        float y = ov[0].y; float zMin = ov.Min(v => v.z), zMax = ov.Max(v => v.z), zEnd = Mathf.Max(zMax, startLipZ);   // 水面は岩のふち（滝の落ち口）まで
        float exitHW = ov.Where(v => v.z > zMax - 0.3f).Max(v => Mathf.Abs(v.x));
        Physics.SyncTransforms();
        // 出口のあたり（ExitStripZ0 から先）はマス目でなく、1列ずつ岩のふちまでの幅をはかった帯（マス目だとふちがカクカク。2026-10-27 てじゃ「水面も綺麗に岩にそうように」）。
        // 帯は ExitWaterEndZ まで。その先（落ち口まで）は洞窟の前の滝の上の面が池の側へのびて、水面のかわりになる
        const float cell = 0.1f; float x0 = -7f, z0 = zMin; int nx = Mathf.CeilToInt(14f / cell), nz = Mathf.FloorToInt((ExitStripZ0 - zMin) / cell);
        float zGrid = z0 + nz * cell;
        var open = new bool[nx, nz]; var seen = new bool[nx, nz];
        bool Low(int i, int j)
        {
            var p = src.TransformPoint(new Vector3(x0 + (i + 0.5f) * cell, y + 5f, z0 + (j + 0.5f) * cell));
            if (!rock.Raycast(new Ray(p, Vector3.down), out var h, 20f)) return false;
            // 出口の近く（元の水面の終わりから 1.2m）は、元の水面の幅まで（出口の横の、外へ下がっていく岩に広げない）
            float x = x0 + (i + 0.5f) * cell, z = z0 + (j + 0.5f) * cell;
            if (z > zMax - 1.2f && Mathf.Abs(x) > exitHW + 0.05f) return false;
            return src.InverseTransformPoint(h.point).y < y - 0.01f;
        }
        bool Wall(int i, int j)
        {
            var p = src.TransformPoint(new Vector3(x0 + (i + 0.5f) * cell, y + 5f, z0 + (j + 0.5f) * cell));
            return rock.Raycast(new Ray(p, Vector3.down), out var h, 20f) && src.InverseTransformPoint(h.point).y >= y - 0.01f;
        }
        var q = new Queue<Vector2Int>(); var st =new Vector2Int(Mathf.FloorToInt((0f - x0) / cell), Mathf.FloorToInt((1.6f - z0) / cell));
        q.Enqueue(st); seen[st.x, st.y] = true;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (!Low(c.x, c.y)) continue;
            open[c.x, c.y] = true;
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = c + d; if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen[n.x, n.y]) continue;
                seen[n.x, n.y] = true; q.Enqueue(n);
            }
        }
        // 1マス外側（岩の壁の中）まで広げて、はしのギザギザを壁にかくす。岩の外（下に岩がない）や出口の横には広げない
        var fill = (bool[,])open.Clone();
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            if (!open[i, j] && Wall(i, j)) for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) { int ii = i + a, jj = j + b; if (ii >= 0 && jj >= 0 && ii < nx && jj < nz && open[ii, jj]) fill[i, j] = true; }
        var V = new List<Vector3>(); var T = new List<int>(); var idx = new Dictionary<Vector2Int, int>();
        int Vid(int i, int j) { var k = new Vector2Int(i, j); if (!idx.TryGetValue(k, out var id)) { id = V.Count; idx[k] = id; V.Add(new Vector3(x0 + i * cell, y, z0 + j * cell)); } return id; }
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            if (fill[i, j]) { int a = Vid(i, j), b = Vid(i + 1, j), c = Vid(i + 1, j + 1), d = Vid(i, j + 1); T.AddRange(new[] { a, d, c, a, c, b }); }
        // 出口の幅：列ごとに、まん中から岩の上の面が水面より高くなる所（か岩がない所）まで。少し岩の下へ入れる
        float RowHW(float z, float sign)
        {
            float last = 0f;
            for (float x = 0f; x <= 3.6f; x += 0.01f)
            {
                var p = src.TransformPoint(new Vector3(sign * x, y + 5f, z));
                if (!rock.Raycast(new Ray(p, Vector3.down), out var h, 20f) || src.InverseTransformPoint(h.point).y >= y - 0.01f) break;
                last = x;
            }
            return last + 0.05f;
        }
        startLipHW = (RowHW(zEnd - 0.3f, -1f) + RowHW(zEnd - 0.3f, 1f)) * 0.5f - 0.05f;   // 落ち口の手前の水の半分の幅（滝の幅をこれに合わせる）
        // 帯は横に StripCols 列（ふちの泡を岩からの距離で出すので、横にも点が要る）
        const int StripCols = 12;
        int nRow = Mathf.Max(1, Mathf.CeilToInt((ExitWaterEndZ - zGrid) / 0.05f)); int prev = -1;
        for (int k = 0; k <= nRow; k++)
        {
            float z = Mathf.Min(zGrid + k * 0.05f, ExitWaterEndZ);
            float xl = -RowHW(z, -1f), xr = RowHW(z, 1f);
            int l = V.Count; for (int c = 0; c <= StripCols; c++) V.Add(new Vector3(Mathf.Lerp(xl, xr, c / (float)StripCols), y, z));
            if (prev >= 0) for (int c = 0; c < StripCols; c++) T.AddRange(new[] { prev + c, l + c, l + c + 1, prev + c, l + c + 1, prev + c + 1 });
            prev = l;
        }
        // 岩のかべまでの距離（ふちの白い泡用。2026-10-27 てじゃ「地面と水の境界だけ白いふちみたいなのつけたい、他の水面みたいに」）。
        // 5cm ごとに岩の上の面が水面より上の所（かべ）を集めて、点ごとにいちばん近いかべまでの距離。滝の口・細い溝の口はかべではないので泡なし
        const float ws = 0.05f, wMax = 1.2f;
        var wallSet = new HashSet<Vector2Int>();
        float zw1 = ExitWaterEndZ + 0.3f;
        for (float wx = -7f; wx <= 7f; wx += ws)
            for (float wz = zMin - 0.6f; wz <= zw1; wz += ws)
            {
                var p = src.TransformPoint(new Vector3(wx, y + 5f, wz));
                if (rock.Raycast(new Ray(p, Vector3.down), out var h, 20f) && src.InverseTransformPoint(h.point).y >= y - 0.01f)
                    wallSet.Add(new Vector2Int(Mathf.RoundToInt(wx / ws), Mathf.RoundToInt(wz / ws)));
            }
        int rr = Mathf.CeilToInt(wMax / ws);
        var wallD = new float[V.Count];
        for (int vi = 0; vi < V.Count; vi++)
        {
            var c0 = new Vector2Int(Mathf.RoundToInt(V[vi].x / ws), Mathf.RoundToInt(V[vi].z / ws));
            if (wallSet.Contains(c0)) { wallD[vi] = 0f; continue; }
            float best = wMax;
            for (int a2 = -rr; a2 <= rr; a2++) for (int b2 = -rr; b2 <= rr; b2++)
            {
                if (!wallSet.Contains(new Vector2Int(c0.x + a2, c0.y + b2))) continue;
                float d = new Vector2(V[vi].x - (c0.x + a2) * ws, V[vi].z - (c0.y + b2) * ws).magnitude - ws * 0.5f;
                if (d < best) best = d;
            }
            wallD[vi] = Mathf.Max(0f, best);
        }
        startWaterEndZ = ExitWaterEndZ;   // 池の水面はここまで。その先（落ち口まで）は滝の上の面（Falls_Start の flatBack）
        var m = new Mesh { name = "Course2_1_W0_Source_Surface", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(V); m.SetTriangles(T, 0); m.SetUVs(0, V.Select(v => Vector2.zero).ToList());
        // uv1.y＝かべまでの距離（シェーダーの edge＝uv1.y - |uv1.x|）。wMax で頭打ち
        m.SetUVs(1, wallD.Select(d => new Vector4(0f, d, 99f, 99f)).ToList()); m.SetUVs(2, V.Select(v => new Vector2(v.z, 0f)).ToList());
        m.normals = V.Select(v => Vector3.up).ToArray(); m.RecalculateBounds();
        m = Save(m, MeshDir + "/W0_Source_Surface.asset");
        surfT.GetComponent<MeshFilter>().sharedMesh = m;
    }

    // 水源の池のまん中の湧き水（2026-10-25 てじゃ「水源、まんなかから湧き出てるかんじにしよ」）：
    // 水面がぽこっと盛り上がった水のふくらみ＋そこから広がり続ける波紋＋白い泡の粒が湧いてはじける
    static void Spring(Transform terr, Vector3 p)
    {
        var g = new GameObject("Spring").transform; g.SetParent(terr, false); g.localPosition = p;
        // まん中のわき上がり：白いやわらかい丸がぽこぽこ（水の材質の球だと穴に見えた）
        var c = new GameObject("Boil"); c.transform.SetParent(g, false); c.transform.localPosition = new Vector3(0, 0.05f, 0); c.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var cp = c.AddComponent<ParticleSystem>();
        var cm = cp.main; cm.loop = true; cm.playOnAwake = true; cm.prewarm = true; cm.simulationSpace = ParticleSystemSimulationSpace.World;
        cm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); cm.startSpeed = 0f; cm.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        cm.maxParticles = 30; cm.startColor = new Color(0.92f, 0.98f, 1f, 0.5f); cm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var ce = cp.emission; ce.rateOverTime = 14f;
        var cs = cp.shape; cs.shapeType = ParticleSystemShapeType.Circle; cs.radius = 0.25f;
        var csz = cp.sizeOverLifetime; csz.enabled = true; csz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(1, 1.2f)));
        var cc = cp.colorOverLifetime; cc.enabled = true;
        var cg = new Gradient(); cg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1) });
        cc.color = cg;
        var cr = c.GetComponent<ParticleSystemRenderer>(); cr.renderMode = ParticleSystemRenderMode.Billboard; cr.alignment = ParticleSystemRenderSpace.Local;
        cr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat"); cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cr.receiveShadows = false;
        var ripMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_Ripple.mat");
        // 波紋：まん中から次々に広がる
        var r = new GameObject("Rings"); r.transform.SetParent(g, false); r.transform.localPosition = new Vector3(0, 0.04f, 0); r.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var rp = r.AddComponent<ParticleSystem>();
        var rm = rp.main; rm.loop = true; rm.playOnAwake = true; rm.prewarm = true; rm.simulationSpace = ParticleSystemSimulationSpace.World;
        rm.startLifetime = 2.2f; rm.startSpeed = 0f; rm.startSize = 1.0f; rm.maxParticles = 12; rm.startColor = new Color(1, 1, 1, 0.8f);
        var re = rp.emission; re.rateOverTime = 1.6f;
        var rsh = rp.shape; rsh.enabled = false;
        var rsz = rp.sizeOverLifetime; rsz.enabled = true; rsz.size = new ParticleSystem.MinMaxCurve(3.4f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(1, 1f)));
        var rc = rp.colorOverLifetime; rc.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(0.8f, 0.12f), new GradientAlphaKey(0f, 1) });
        rc.color = gr;
        var rr = r.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Billboard; rr.alignment = ParticleSystemRenderSpace.Local;
        rr.sharedMaterial = ripMat; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
        // 泡：ふくらみのまわりから湧いて、少し上がってはじける
        var b = new GameObject("Bubbles"); b.transform.SetParent(g, false); b.transform.localPosition = new Vector3(0, 0.08f, 0); b.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var bp = b.AddComponent<ParticleSystem>();
        var bm = bp.main; bm.loop = true; bm.playOnAwake = true; bm.prewarm = true; bm.simulationSpace = ParticleSystemSimulationSpace.World;
        bm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); bm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.45f); bm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        bm.maxParticles = 40; bm.startColor = new Color(0.95f, 1f, 1f, 0.9f); bm.gravityModifier = 0.15f;
        var be = bp.emission; be.rateOverTime = 18f;
        var bs = bp.shape; bs.shapeType = ParticleSystemShapeType.Circle; bs.radius = 0.45f; bs.radiusThickness = 1f;
        var bsz = bp.sizeOverLifetime; bsz.enabled = true; bsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.8f, 1f), new Keyframe(1, 0f)));
        var br = b.GetComponent<ParticleSystemRenderer>(); br.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
    }

    // ふしぎな青い球（2026-10-25 てじゃ「この空間に水がわいてるの納得できんな、不思議パワーがほしい、謎の青い球でも浮かせて水おとすか」）：
    // 池のまん中の上に光る青い球が浮かんで（FuwaHover でゆらゆら・くるくる）、球の下から水が池へ落ち続ける。落ちる所は湧き水の波紋（Spring）
    public static float OrbHeight = 2.6f, OrbRadius = 0.45f, OrbStream = 0.32f;
    static void WaterOrb(Transform terr, Vector3 pool) { WaterOrb(terr, pool, "WaterOrb", OrbHeight, OrbRadius, OrbStream); }
    static void WaterOrb(Transform terr, Vector3 pool, string name, float OrbHeight, float OrbRadius, float OrbStream)
    {
        var g = new GameObject(name).transform; g.SetParent(terr, false); g.localPosition = pool + Vector3.up * OrbHeight;
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere); orb.name = "Orb"; orb.transform.SetParent(g, false);
        Object.DestroyImmediate(orb.GetComponent<Collider>());
        orb.transform.localScale = Vector3.one * OrbRadius * 2f;
        const string mp = "Assets/FuwaStage2/Materials/Stage2_WaterOrb.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!mat) { mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Lobby_Ring.mat")); AssetDatabase.CreateAsset(mat, mp); }
        mat.SetColor("_Color", new Color(0.45f, 0.78f, 1f)); mat.SetColor("_EmissionColor", new Color(0.25f, 0.6f, 1f));
        if (mat.HasProperty("_RimColor")) { mat.SetColor("_RimColor", new Color(0.85f, 0.97f, 1f)); mat.SetFloat("_RimStrength", 0.8f); }
        EditorUtility.SetDirty(mat);
        var r = orb.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var hv = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHover>(orb);
        hv.amplitude = 0.15f; hv.period = 3.2f; hv.spinSpeed = 25f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hv); NoSync(hv);
        // まわりのぼんやりした光（やわらかい丸の粒）
        var gl = new GameObject("Glow"); gl.transform.SetParent(orb.transform, false);
        var gp = gl.AddComponent<ParticleSystem>();
        var gm = gp.main; gm.loop = true; gm.playOnAwake = true; gm.prewarm = true; gm.simulationSpace = ParticleSystemSimulationSpace.Local;
        gm.startLifetime = 1.6f; gm.startSpeed = 0f; gm.startSize = new ParticleSystem.MinMaxCurve(OrbRadius * 3.2f, OrbRadius * 4.2f); gm.maxParticles = 10;
        gm.startColor = new Color(0.45f, 0.8f, 1f, 0.35f);
        var ge = gp.emission; ge.rateOverTime = 5f;
        var gs = gp.shape; gs.enabled = false;
        var gc = gp.colorOverLifetime; gc.enabled = true;
        var gg = new Gradient(); gg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1) });
        gc.color = gg;
        var gr = gl.GetComponent<ParticleSystemRenderer>(); gr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; gr.receiveShadows = false;
        // 球の下から池へ落ちる水（まるい断面の滝）
        float len = OrbHeight - 0.02f;
        string key = "OrbStream_" + len.ToString("F2") + "_" + OrbStream.ToString("F2");
        var meshP = "Assets/FuwaStage2/Generated/Falls/" + key + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshP);
        if (!mesh)
        {
            var cs = new List<Vector3> { Vector3.down * len, Vector3.zero };
            mesh = FuwaStage2WaterTest.SweepFalls(key, cs, new List<Vector3> { Vector3.forward, Vector3.forward }, new List<Vector3> { Vector3.right, Vector3.right }, new List<float> { len, 0f }, len, OrbStream, OrbStream);
            AssetDatabase.CreateAsset(mesh, meshP);
        }
        var st = new GameObject("Stream"); st.transform.SetParent(g, false);
        st.AddComponent<MeshFilter>().sharedMesh = mesh;
        var sr = st.AddComponent<MeshRenderer>(); sr.sharedMaterial = FuwaStage2WaterTest.WaterfallThickMat(true); sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---- 道と水の下の岩の土台（2026-10-25 てじゃ「手前から順番にどんどんいこ」→ 滝つぼ〜川〜蓮の池）----
    // 前は水の入れ物（Water/*/Body）が下までまっすぐ伸びた柱みたいだった。かわりに、道・水のふちの下から始まって、下にいくほど
    // 細くなる浮島の形の岩（スタートの岩山と同じ色、内側も見える両面）を中心線にそって作る。
    // 幅と高さは、できあがった道・水の当たり判定を上からレイで測る（道の形を変えても作り直すだけでそろう）。当たり判定はなし（見た目だけ。前の入れ物の当たり判定は残す）
    // 区間：滝つぼ〜川の終わりの滝、谷の向こう〜蓮の池の終わり（谷で島が切れる）
    static void RockBases(Transform root)
    {
        var g = new GameObject("RockBase").transform; g.SetParent(root, false);
        Physics.SyncTransforms();
        float gorge1 = sRiver1 + GorgeLen;
        RockBase(root, g, "BaseA", sPlaza1 - BaseAFront, sRiver1 + 0.2f, 11, null, BaseAFront);
        // 2026-10-25 貝の道の下まで続ける（貝の道の両わきを岩の壁にしたのにあわせて）
        // 2026-10-26 滝の道（三連滝）の終わりまで続ける。水路の両はしは切れ目（breaks）にして、はばをまわりへ広げない
        //（広げると水路の端の滝 EndFalls が岩につきささる）
        var breaks = new List<float>();
        for (int i = 0; i < setS.Length; i++) { ChannelRange(i, out var c0, out var c1); breaks.Add(c0); breaks.Add(c1); }
        // 2026-10-26 ラストの川の終わり（sFin1）まで
        RockBase(root, g, "BaseB", gorge1 - 0.2f, sFin1, 23, breaks);
        foreach (var nm in new[] { "Finale/LeftBankBase", "Finale/W6_FinalRiver/Body" })
        { var t = root.Find(nm); if (t && t.GetComponent<Renderer>()) t.GetComponent<Renderer>().enabled = false; }
        var wbase = root.Find("ClamWalk/WalkBase"); if (wbase && wbase.GetComponent<Renderer>()) wbase.GetComponent<Renderer>().enabled = false;
        // 滝の道の青緑の箱の土台・水路の入れ物の見た目も消す（当たり判定は残す）
        foreach (var nm in new[] { "FallsRoad/StraightBase", "FallsRoad/RoadBase" })
        { var t = root.Find(nm); if (t && t.GetComponent<Renderer>()) t.GetComponent<Renderer>().enabled = false; }
        for (int i = 0; i < setS.Length; i++)
        { var t = root.Find("FallsRoad/Set" + i + "/Channel" + i + "/Body"); if (t && t.GetComponent<Renderer>()) t.GetComponent<Renderer>().enabled = false; }
        // 前の柱みたいな入れ物の見た目は消す（当たり判定は残す）
        foreach (var nm in new[] { "W1_Pool/Body", "W2_River/Body", "W3_LilyPond/Body", "W3_LilyPond/Body2", "W3_LilyPond/BodyL", "W3_LilyPond/BodyR", "W3_LilyPond/BodyHole" })
        { var t = root.Find("Water/" + nm); if (t && t.GetComponent<Renderer>()) t.GetComponent<Renderer>().enabled = false; }
        var fc = root.Find("Gorge/FarCliff"); if (fc && fc.GetComponent<Renderer>()) fc.GetComponent<Renderer>().enabled = false;
    }
    // 空中に落ちていく滝（下に水も岩もない）の下のはしを、遠景の浮島の滝のように白い霧にしてちぎれて消えるように（2026-10-27 てじゃ
    //「空中におちてる滝（細いのも含む）の一番下、遠景の浮島の滝の末端みたいに散らしたい」）。いちばん下の頂点の真下を測って、何もなければ空中の滝。
    // 材質は元の材質のコピー（名前に _Air）に _FrayLen を入れる（滝つぼに落ちる滝は今のまま）
    // 蓮の池のあふれ口の滝の見た目（岩の土台ができてから作る：土台の出っぱりをよけて落とすので）
    static void PondSpillLipFalls(Transform root)
    {
        // 見た目は池の水からそのまま低いふちを越えてこぼれる膜に（2026-10-27 てじゃ「蓮池の滝の上もへんかな」：前は池の上に置いた四角い水の板で、
        // まわりに白いふちの線が出て、滝と離れて見えた）。四角い板と前の滝の膜は見た目を消す（霧・音・押す箱は残す）
        var lipW = root.Find("Terrain/PondSpillLip"); if (lipW) lipW.GetComponent<Renderer>().enabled = false;
        float sg = PondSpill();
        var oldCur = root.Find("Falls_PondSpill/Curtain"); if (oldCur && oldCur.GetComponent<Renderer>()) oldCur.GetComponent<Renderer>().enabled = false;
        var lipS = new List<Vector3>(); var outS = new List<Vector3>();
        for (int k = 0; k <= 10; k++)
        {
            float s = Mathf.Lerp(sg - 1.08f, sg + 1.08f, k / 10f);
            Center(s, out var p4, out var d4); var r4 = Right(d4); var q4 = p4 - r4 * (PondHW(s) + RimW);
            lipS.Add(new Vector3(q4.x, 0f, q4.y)); outS.Add(new Vector3(-r4.x, 0, -r4.y));
        }
        // 断面：池の中（ふちの内がわ 0.15m）の水面のすぐ下 → 低いふちの上をこえて → 外へ弧をえがいて落ちる
        var profS = new List<Vector2> { new Vector2(RimW + 0.15f, -0.006f), new Vector2(0.02f, -0.006f), new Vector2(-0.1f, -0.08f) };
        // 外へのずれ：下の岩の土台（RockBase。この時はまだ作っていないので、前の作り直しのメッシュ）の出っぱりより外を通る（2026-10-27 岩のトゲが滝を突きぬけた）。
        // 深さごとに、あふれ口の幅の中で岩がいちばん外へ出ている所＋すき間。外へのずれは深さとともに増えるだけ
        var baseMeshes = new List<Vector3>();
        foreach (var nm in new[] { "RockBase/BaseA", "RockBase/BaseB" })
        { var bt2 = root.Find(nm); var bmf = bt2 ? bt2.GetComponent<MeshFilter>() : null; if (bmf && bmf.sharedMesh) foreach (var v in bmf.sharedMesh.vertices) baseMeshes.Add(root.InverseTransformPoint(bt2.TransformPoint(v))); }
        var lipMid = lipS[5]; var outMid = outS[5]; var alongMid = Vector3.Cross(Vector3.up, outMid).normalized;
        float offS = 0.1f;
        for (float d = 0.2f; d < 8f; d += d < 1.5f ? 0.2f : 0.5f)
        {
            float need = 0f;
            foreach (var v in baseMeshes)
            {
                var dv = v - lipMid; if (Mathf.Abs(Vector3.Dot(dv, alongMid)) > 1.4f || Mathf.Abs(v.y - (-d)) > 0.5f) continue;
                need = Mathf.Max(need, Vector3.Dot(dv, outMid) + 0.2f);
            }
            offS = Mathf.Max(offS, need, 0.1f + 0.15f * Mathf.Sqrt(d));
            profS.Add(new Vector2(-offS, -d));
        }
        for (int k = profS.Count - 2; k >= 3; k--) profS[k] = new Vector2(Mathf.Min(profS[k].x, profS[k + 1].x * 0.85f + profS[k].x * 0.15f), profS[k].y);
        profS.Add(new Vector2(profS[profS.Count - 1].x, float.NaN));
        var spillFalls = LipFalls(root, "PondSpillLip", "SpillLip", lipS, outS, profS, -8f);
        spillFalls.transform.SetParent(root.Find("Falls_PondSpill"), true);   // 点は root のローカルで作って、あとで滝の下へ（位置はそのまま）
        // 落ち口の手前（池の中・ふちの上）は池のふちの水の色（大きい滝の _PondTop と同じしくみ）
        const string spp = "Assets/FuwaStage2/Materials/Stage2_WaterfallCurvedThick_PondSpill.mat";
        var baseFm = FuwaStage2WaterTest.WaterfallThickMat(true);
        var sm2 = AssetDatabase.LoadAssetAtPath<Material>(spp);
        if (!sm2) { sm2 = new Material(baseFm); AssetDatabase.CreateAsset(sm2, spp); }
        sm2.CopyPropertiesFromMaterial(baseFm);
        var pondM = WaterMat(0f, true, false);
        sm2.SetFloat("_PondTop", RimW + 0.13f); sm2.SetColor("_PondColor", pondM.GetColor("_Shallow")); sm2.SetColor("_PondFoam", pondM.GetColor("_Foam"));
        sm2.SetFloat("_PondRadial", 0f); sm2.SetFloat("_PondSparkle", pondM.GetFloat("_Sparkle"));
        EditorUtility.SetDirty(sm2);
        spillFalls.GetComponent<MeshRenderer>().sharedMaterial = sm2;
    }
    // 蓮の池の水源の岩の小さな滝（2026-10-27 てじゃ「ここの滝もきれいになおそう」：前は岩の前に置いた箱の形の滝で、岩の上と離れて見えた）。
    // 上の青い球から落ちた水が、岩のまるい上の面をうすく広がって流れ、岩のかたからこぼれて池へ落ちる膜にする。
    // 列ごとに岩の面を上から測って沿わせ（面から SpringFilm 浮かせる）、岩から離れたら岩のふくらみ（横から測る）より外を通って水面まで
    public static float SpringFilm = 0.04f, SpringClear = 0.14f;
    static void PondSpringFalls(Transform root)
    {
        var fs = root.Find("Falls_PondSpring"); var rv = root.Find("Terrain/PondSpringRockVisual"); if (!fs || !rv) return;
        var cur = fs.Find("Curtain"); if (cur) cur.GetComponent<Renderer>().enabled = false;
        var top = root.Find("Terrain/PondSpringTop"); if (top) top.GetComponent<Renderer>().enabled = false;   // 岩の上の面の下にうもれていた
        var mc = rv.gameObject.AddComponent<MeshCollider>(); Physics.SyncTransforms();
        var o = fs.position; var f = fs.forward; var r = fs.right; float y0 = root.position.y;   // 池の水面＝root の y 0
        // 青い球の真下（岩の上）から、だんだん広がって岩のかたへ
        const float dStart = 1.22f, xc = -0.15f; const int nc = 11;
        float WidthAt(float d) { return Mathf.Lerp(0.6f, 0.16f, Mathf.InverseLerp(0.45f, dStart, d)); }
        Vector3 At(float d, float x, float y) { return new Vector3(o.x, y, o.z) - f * d + r * x; }
        var cols = new List<List<Vector3>>();
        for (int k = 0; k < nc; k++)
        {
            float u = Mathf.Lerp(-1f, 1f, k / (float)(nc - 1));
            var pts = new List<Vector3>();
            float d = dStart; Vector3 last = Vector3.zero, tan = -f;
            // 岩の上：0.06m ずつ外へ。面の向きが急になったら（または面がなくなったら）はなれる
            while (d > -1.5f)
            {
                float x = xc + u * WidthAt(d);
                if (!mc.Raycast(new Ray(At(d, x, y0 + 10f), Vector3.down), out var hit, 20f) || hit.normal.y < 0.55f) break;
                var q = hit.point + hit.normal * SpringFilm;
                if (pts.Count > 0) tan = (q - last).normalized;
                pts.Add(q); last = q; d -= 0.06f;
            }
            // 空中：はなれた所の向きで放物線（少し外へ）。岩のふくらみ（横から測る）＋すき間より外
            float vx = Mathf.Max(0.35f, Vector3.Dot(tan, f) * 1.6f), vy = Mathf.Min(0f, tan.y * 1.6f);
            var rel = last - new Vector3(o.x, last.y, o.z);
            float outD = Vector3.Dot(rel, f), xl = Vector3.Dot(rel, r);
            float t = 0f, prevOut = outD, yFloor = y0 - 0.06f;
            for (int it = 0; it < 400; it++)
            {
                t += 0.04f; float yy = last.y + vy * t - 4.9f * t * t; float od = outD + vx * t;
                float hy = Mathf.Max(yy, yFloor);
                var from = new Vector3(o.x, hy, o.z) + f * 3f + r * xl;
                if (mc.Raycast(new Ray(from, -f), out var h2, 6f)) od = Mathf.Max(od, Vector3.Dot(h2.point - new Vector3(o.x, hy, o.z), f) + SpringClear);
                od = Mathf.Max(od, prevOut); prevOut = od;
                pts.Add(new Vector3(o.x, hy, o.z) + f * od + r * xl);
                if (yy <= yFloor) break;
            }
            cols.Add(pts);
        }
        Object.DestroyImmediate(mc);
        // 列ごとに点の数がちがうので、長さで同じ数に並べ直す（上→下。上のほう＝岩の上と落ち口を細かく）
        const int rows = 48;
        var grid = new Vector3[nc, rows];
        for (int k = 0; k < nc; k++)
        {
            var pts = cols[k]; var cum = new float[pts.Count]; for (int i = 1; i < pts.Count; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            float tot = cum[pts.Count - 1];
            for (int j = 0; j < rows; j++)
            {
                float a = j / (float)(rows - 1); float L = tot * (a * a * 0.35f + a * 0.65f);
                int i = 1; while (i < pts.Count - 1 && cum[i] < L) i++;
                float tt = Mathf.InverseLerp(cum[i - 1], cum[i], L);
                grid[k, j] = root.InverseTransformPoint(Vector3.Lerp(pts[i - 1], pts[i], tt));
            }
        }
        // 表＝岩から外（上・池の側）
        var fl = root.InverseTransformDirection(f);
        var spr = LipFallsMesh(root, "PondSpringFalls", "SpringFilm", grid, (k, j) =>
        {
            var n = Vector3.Cross(grid[k, j + 1] - grid[k, j], grid[k + 1, j] - grid[k, j]);
            return Vector3.Dot(n, fl + Vector3.up) >= 0f ? n : -n;
        });
        spr.transform.SetParent(fs, true);
        // 滝つぼ：着水点のしぶき（大きい滝の PoolFoam と同じ。小さい滝なので小さめ）
        var land = fs.InverseTransformPoint(root.TransformPoint(grid[nc / 2, rows - 1]));
        WaterDecal(fs, "PoolFoam", new Vector3(land.x, 0.012f, land.z), Vector3.forward, -1.6f, 1.6f, -0.4f, 1.9f, 0, 0.6f, 1.2f, new Color(0.45f, 0.78f, 0.95f));
        var mist = fs.Find("Mist"); if (mist) mist.localPosition = new Vector3(land.x, 0.05f, land.z + 0.15f);
    }
    public static float AirFrayLen = 6f;
    static void AirFallsFray(Transform root)
    {
        Physics.SyncTransforms();
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var m = mr.sharedMaterial; if (!m || m.shader.name != "FuwaStage2/Waterfall") continue;
            if (m.name.EndsWith("_Air")) { var bp = AssetDatabase.GetAssetPath(m).Replace("_Air.mat", ".mat"); m = AssetDatabase.LoadAssetAtPath<Material>(bp); if (!m) continue; mr.sharedMaterial = m; }
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var low = Vector3.zero; float ly = float.MaxValue;
            foreach (var v in mf.sharedMesh.vertices) { var w = mr.transform.TransformPoint(v); if (w.y < ly) { ly = w.y; low = w; } }
            // 水面（トリガー）も「下に何かある」に数える。滝の押す箱（FuwaWaterfall）とその滝自身は数えない（ゲートの滝は水面に落ちていた）
            bool below = false;
            foreach (var h in Physics.RaycastAll(low + Vector3.up * 0.3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Collide))
            {
                if (h.collider.GetComponent<FuwaWaterfall>() || h.collider.transform.IsChildOf(mr.transform.parent)) continue;
                // トリガーは水だけ（エリアの大きいトリガーは数えない）
                if (h.collider.isTrigger && !(h.collider.GetComponent<FuwaWater>() || h.collider.GetComponentInParent<FuwaWater>())) continue;
                below = true; break;
            }
            // いちばん下の点のすぐそば（水の入れ物・岩）にも何もないか（ゲートの滝は川のはしのすぐ外に落ちていて、真下だけ見ると空中になった）
            foreach (var c in Physics.OverlapSphere(low, 1.0f, ~0, QueryTriggerInteraction.Collide))
            {
                if (c.GetComponent<FuwaWaterfall>() || c.transform.IsChildOf(mr.transform.parent)) continue;
                if (c.isTrigger && !(c.GetComponent<FuwaWater>() || c.GetComponentInParent<FuwaWater>())) continue;
                below = true; break;
            }
            if (below) continue;
            string p = AssetDatabase.GetAssetPath(m); if (string.IsNullOrEmpty(p)) continue;
            string ap = p.Replace(".mat", "_Air.mat");
            var am = AssetDatabase.LoadAssetAtPath<Material>(ap);
            if (!am) { am = new Material(m); AssetDatabase.CreateAsset(am, ap); }
            am.CopyPropertiesFromMaterial(m); am.SetFloat("_FrayLen", AirFrayLen); EditorUtility.SetDirty(am);
            mr.sharedMaterial = am;
        }
    }
    static Material RockBaseMat()
    {
        const string p = "Assets/FuwaStage2/Materials/Stage2_RockBase.mat";
        var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_CaveRock.mat");
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, p); }
        m.CopyPropertiesFromMaterial(src);
        m.SetFloat("_Cull", 0f); EditorUtility.SetDirty(m);   // 道と水のすき間から内側が見えるので両面
        return m;
    }
    // 片側の断面（ふちから内側のとがりへ）：(ふちから外へのずれ or とがりへ寄る割合, 高さ)。f<0 はふちから外へ -f m、f>=0 は中央へ f の割合
    static readonly (float f, float down, bool fromEdge)[] BaseProfile =
    {
        // ふちから外の面は、1つ目だけ固定（ふちの 0.12m 下）、2つ目からは厚みに対する割合（深い水の所で入れ物の壁がはみ出さないように）
        (-0.12f, 0.12f, true), (-0.25f, 0.3f, true), (-0.18f, 0.62f, true), (0.3f, 0.82f, false), (0.65f, 0.95f, false),
    };
    // 土台の厚み（ふちからとがりまで、m）。2026-10-25 てじゃ「分厚いと岩の道とかぶるから、かなり薄めにいこう」4.8〜6.6 → 1.6〜2.2
    public static float BaseDepth = 1.6f, BaseDepthVar = 0.6f;
    public static float BaseTaperW = 0.45f, BaseTaperY = 0.6f;
    public static float BaseAFront = 1.5f;   // 滝つぼの下の土台を洞窟の岩の中へのばす長さ(m)
    static void RockBase(Transform root, Transform g, string name, float s0, float s1, int seed, List<float> breaks = null, float frontFill = 0f)
    {
        const float step = 0.5f, scanW = 14f, scanStep = 0.2f, brk = 0.05f;
        var ss = new List<float>(); for (float s = s0; s <= s1 + 1e-3f; s += step) ss.Add(s);
        // 切れ目の前後にも測る所を足す（はばが切れ目でまっすぐ変わるように）
        if (breaks != null)
        {
            foreach (var b in breaks) { if (b - brk > s0 && b - brk < s1) ss.Add(b - brk); if (b + brk > s0 && b + brk < s1) ss.Add(b + brk); }
            ss.Sort();
            for (int i = ss.Count - 1; i > 0; i--) if (ss[i] - ss[i - 1] < 0.02f) ss.RemoveAt(i);
        }
        bool Crosses(float a, float b) { if (breaks == null) return false; float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b); foreach (var x in breaks) if (x > lo && x < hi) return true; return false; }
        int n = ss.Count;
        var L = new float[n]; var R = new float[n]; var yL = new float[n]; var yR = new float[n]; var ok = new bool[n];
        var yMin = new float[n];   // いちばん低い所（深い水の底）。土台はそれより下まで
        for (int i = 0; i < n; i++)
        {
            Center(ss[i], out var p, out var d); var r = Right(d);
            float lo = float.MaxValue, hi = float.MinValue, ylo = 0, yhi = 0, ymin = float.MaxValue;
            for (float lat = -scanW; lat <= scanW; lat += scanStep)
            {
                var q = p + r * lat;
                var from = root.TransformPoint(new Vector3(q.x, 20f, q.y));
                // その点のいちばん上（ふち・道の上の面）と、いちばん下（深い水の底）。RaycastAll の順番はばらばらなので全部見る
                float yTop = float.MinValue;
                foreach (var h in Physics.RaycastAll(from, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var t = h.collider.transform;
                    // 2026-10-26 てじゃ「いまある青道の下の岩もとっちゃうか」：シマシマの道は測らない（水の下だけ岩）
                    bool path = false;
                    bool water = t.parent && t.parent.parent && t.parent.parent.name == "Water" && t.parent.parent.parent == root && t.parent.name != "W0_Source";
                    // 滝の道の水路（FallsRoad/SetN/ChannelN の底・ふち）
                    bool chan = t.parent && t.parent.parent && t.parent.parent.parent && t.parent.parent.parent.name == "FallsRoad" && t.parent.parent.parent.parent == root && t.parent.name.StartsWith("Channel");
                    // ラストの川（Finale/W6_FinalRiver の底・ふち）と両岸
                    bool fin = (t.parent && t.parent.name == "Finale" && t.parent.parent == root && (t.name == "RightBank" || t.name == "LeftBank"))
                        || (t.parent && t.parent.parent && t.parent.parent.name == "Finale" && t.parent.parent.parent == root && t.parent.name.StartsWith("W6_"));
                    if (!path && !water && !chan && !fin) continue;
                    float y = root.InverseTransformPoint(h.point).y;
                    ymin = Mathf.Min(ymin, y); yTop = Mathf.Max(yTop, y);
                }
                if (yTop == float.MinValue) continue;
                if (lat < lo) { lo = lat; ylo = yTop; }
                if (lat > hi) { hi = lat; yhi = yTop; }
            }
            ok[i] = hi > lo; L[i] = lo; R[i] = hi; yL[i] = ylo; yR[i] = yhi; yMin[i] = ymin;
        }
        // 前のはしをのばす（frontFill m）：滝つぼの前のはしは、底の板にしか当たらず（ふちがない）土台の上が底の高さになって、
        // 洞窟の岩とのあいだにすき間があき、底の板の前のはしも顔を出していた（2026-10-27 てじゃ「滝の下あたり、底の土がはみだしてる」「穴もあいてる」）。
        // ふちに当たった最初の所の はば・高さで、前（洞窟の岩の中）までのばす
        if (frontFill > 0f)
        {
            int rf = -1;
            for (int i = 0; i < n; i++) if (ok[i] && Mathf.Min(yL[i], yR[i]) > yMin[i] + 1f) { rf = i; break; }
            if (rf > 0)
                for (int i = 0; i < rf; i++) { ok[i] = true; L[i] = L[rf]; R[i] = R[rf]; yL[i] = yL[rf]; yR[i] = yR[rf]; yMin[i] = yMin[rf]; }
        }
        // 抜けた所：短い（1m まで）なら近くで埋める、長い所（水のない道だけの所）は岩を切る
        var run = new int[n];   // 何番目の岩のかたまりか（-1＝岩なし）
        {
            int r = -1; bool inRun = false;
            for (int i = 0; i < n; i++)
            {
                if (!ok[i])
                {
                    int j = i; while (j < n && !ok[j]) j++;
                    bool shortGap = inRun && j < n && j - i <= 2;
                    if (!shortGap) { for (int k = i; k < j; k++) run[k] = -1; inRun = false; i = j - 1; continue; }
                    for (int k = i; k < j; k++) { run[k] = r; L[k] = L[i - 1]; R[k] = R[i - 1]; yL[k] = yL[i - 1]; yR[k] = yR[i - 1]; yMin[k] = yMin[i - 1]; }
                    i = j - 1; continue;
                }
                if (!inRun) { r++; inRun = true; }
                run[i] = r;
            }
        }
        // はばと深い所は、まわりから なだらかに（2026-10-27 てじゃ「岩の上が蓋が開いてるような感じ」：前は前後2mの箱で広げていて、
        // 滝つぼ→川のように はば・深さが変わる所で土台が段になり、段の所で外の面が折れ返ってぱっくり割れて見えた）
        // はばは 1m 進むごとに BaseTaperW、深さは BaseTaperY までしか変わらない（広い・深い所から離れるほど だんだん狭く・浅く）
        var L2 = new float[n]; var R2 = new float[n]; var deepT = new float[n];
        for (int i = 0; i < n; i++)
        {
            L2[i] = L[i]; R2[i] = R[i]; deepT[i] = yMin[i];
            for (int j = 0; j < n; j++)
            {
                if (run[j] != run[i] || j == i) continue;
                float ds = Mathf.Abs(ss[j] - ss[i]); if (ds > 10f) continue;
                deepT[i] = Mathf.Min(deepT[i], yMin[j] + BaseTaperY * ds);
                if (Crosses(ss[i], ss[j])) continue;
                L2[i] = Mathf.Min(L2[i], L[j] + BaseTaperW * ds); R2[i] = Mathf.Max(R2[i], R[j] - BaseTaperW * ds);
            }
        }
        var rng = new System.Random(seed);
        float nOff = (float)rng.NextDouble() * 100f;
        int cols = BaseProfile.Length * 2 + 1;
        var V = new List<Vector3>(); var T = new List<int>();
        var LA = new float[n, cols]; var YA = new float[n, cols];
        for (int i = 0; i < n; i++)
        {
            float s = ss[i];
            if (run[i] < 0) continue;
            int ra = i, rb = i; while (ra > 0 && run[ra - 1] == run[i]) ra--; while (rb < n - 1 && run[rb + 1] == run[i]) rb++;
            float end = Smooth01(Mathf.Min(s - ss[ra], ss[rb] - s) / 2.5f);   // 両はしはすぼめて浅く
            float mid = (L2[i] + R2[i]) * 0.5f;
            // はばはすぼめない（すぼめると深い水の入れ物の壁がはみ出した）。両はしは浅くするだけ
            float l = L2[i], rr = R2[i];
            float top = Mathf.Min(yL[i], yR[i]);
            float depth = (BaseDepth + BaseDepthVar * Mathf.PerlinNoise(nOff, s * 0.08f)) * Mathf.Lerp(0.6f, 1f, end);
            // 深い水（滝つぼ・蓮の池の深い所）の底がはみ出さないように（前後 2m のいちばん深い所＋0.5m）
            float deep = deepT[i];
            depth = Mathf.Max(depth, top - deep + 0.5f);
            for (int k = 0; k < cols; k++)
            {
                float lat, y;
                if (k == BaseProfile.Length) { lat = mid; y = top - depth; }
                else
                {
                    bool right = k > BaseProfile.Length;
                    var pr = BaseProfile[right ? cols - 1 - k : k];
                    float edge = right ? rr : l, ey = right ? yR[i] : yL[i], outw = right ? 1f : -1f;
                    // ふちのすぐ下は本当のふち（L/R）に。なだらかに広げた はば（L2/R2）は下の点から（ふちの点ごと広げると、ふちと土台のあいだの上が開いて中が見えた）
                    float edge0 = right ? R[i] : L[i];
                    if (k == 0 || k == cols - 1) edge = edge0; else if (k == 1 || k == cols - 2) edge = Mathf.Lerp(edge0, edge, 0.4f);
                    if (pr.fromEdge)
                    {
                        lat = edge - outw * pr.f;
                        y = k == 0 || k == cols - 1 ? ey - pr.down : Mathf.Min(ey - 0.3f, top - depth * pr.down);
                        if (k == 2 || k == cols - 3) y = Mathf.Min(y, deep - 0.2f);   // 外の面の下のはしは、深い水の底より下（入れ物の壁の角が出ないように）
                    }
                    // 底の面も深い水の底より下（2026-10-27 滝つぼの終わりで、底の点が水の底より上にきて入れ物の底が突き出ていた）
                    else { lat = edge + (mid - edge) * pr.f; y = Mathf.Min(top - depth * pr.down, deep - 0.4f); }
                    // ごつごつ：ふちのすぐ下以外はノイズで出し入れ
                    float amt = k == 0 || k == cols - 1 ? 0.05f : 0.3f;
                    lat += outw * (Mathf.PerlinNoise(nOff + s * 0.55f, k * 1.7f) - 0.5f) * amt * 2f;
                    y += (Mathf.PerlinNoise(nOff + 40f + s * 0.4f, k * 2.3f) - 0.5f) * (k == 0 || k == cols - 1 ? 0f : 0.35f);
                }
                LA[i, k] = lat; YA[i, k] = y;
            }
        }
        // ふちより下の点は、前後となじませる（外へ・下へだけ動かす＝くぼんだ折れ目を埋める。内へは動かさないので水の入れ物ははみ出さない）
        //（2026-10-27 はば・深さが変わる所の折れ目がまだ谷になって見えた）
        for (int it = 0; it < 4; it++)
        {
            var la2 = (float[,])LA.Clone(); var ya2 = (float[,])YA.Clone();
            for (int i = 1; i < n - 1; i++)
            {
                if (run[i] < 0 || run[i - 1] != run[i] || run[i + 1] != run[i]) continue;
                bool across = Crosses(ss[i - 1], ss[i + 1]);
                for (int k = 1; k < cols - 1; k++)
                {
                    float y = (YA[i - 1, k] + 2f * YA[i, k] + YA[i + 1, k]) * 0.25f; ya2[i, k] = Mathf.Min(YA[i, k], y);
                    if (across || k == BaseProfile.Length) continue;
                    float lat = (LA[i - 1, k] + 2f * LA[i, k] + LA[i + 1, k]) * 0.25f;
                    la2[i, k] = k < BaseProfile.Length ? Mathf.Min(LA[i, k], lat) : Mathf.Max(LA[i, k], lat);
                }
            }
            LA = la2; YA = ya2;
        }
        for (int i = 0; i < n; i++)
        {
            Center(ss[i], out var p, out var d); var r = Right(d);
            for (int k = 0; k < cols; k++)
            {
                if (run[i] < 0) { V.Add(Vector3.zero); continue; }
                var q = p + r * LA[i, k]; V.Add(new Vector3(q.x, YA[i, k], q.y));
            }
        }
        for (int i = 0; i < n - 1; i++)
        {
            if (run[i] < 0 || run[i + 1] != run[i]) continue;
            for (int k = 0; k < cols - 1; k++)
            {
                int a = i * cols + k, b = a + cols;
                T.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        }
        // かたまりごとの両はしのふた（扇）
        var caps = new List<int>();
        for (int i = 0; i < n; i++) { if (run[i] < 0) continue; if (i == 0 || run[i - 1] != run[i]) caps.Add(i); if (i == n - 1 || run[i + 1] != run[i]) caps.Add(i); }
        foreach (int i in caps)
        {
            var cen = Vector3.zero; for (int k = 0; k < cols; k++) cen += V[i * cols + k]; cen /= cols;
            int ci = V.Count; V.Add(cen);
            for (int k = 0; k < cols - 1; k++) T.AddRange(new[] { ci, i * cols + k, i * cols + k + 1 });
        }
        // 角ばった面（岩らしく）：三角形ごとに頂点を分ける
        var V2 = new List<Vector3>(); var T2 = new List<int>();
        for (int i = 0; i < T.Count; i++) { V2.Add(V[T[i]]); T2.Add(i); }
        var mesh = new Mesh { name = "Course2_1_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V2); mesh.SetTriangles(T2, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(g, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = RockBaseMat();
    }

    // ---- BGM（2026-10-25 てじゃ「音楽先につくっちゃうか」→ 候補C「すいめんのひかり」の笛ひかえめ版 C3、Tools/audio/make_bgm_mizu.py の mizu_c_long）----
    // FuwaSystem/BGM に曲（BGM_course2_1、ほかの曲の AudioSource を写す）を足して、FuwaBgm の tracks と starts（このコースの BallStart）をつなぐ。
    // 作り直すと BallStart が新しくなるので毎回つなぎ直す
    const string BgmClip = "Assets/FuwaStage2/Audio/bgm_mizu_long.wav";
    static void RegisterBgm(Transform root)
    {
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault(); if (!bgm) return;
        var start = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BallStart"); if (!start) return;
        var src = bgm.transform.Find("BGM_mix"); if (!src) return;
        var tr = bgm.transform.Find("BGM_course2_1");
        if (!tr)
        {
            tr = Object.Instantiate(src.gameObject, bgm.transform, false).transform; tr.name = "BGM_course2_1";
        }
        var a = tr.GetComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmClip); EditorUtility.SetDirty(a);
        var so = new SerializedObject(bgm);
        var tracks = so.FindProperty("tracks"); var starts = so.FindProperty("starts");
        int idx = -1;
        for (int i = 0; i < tracks.arraySize; i++) if (tracks.GetArrayElementAtIndex(i).objectReferenceValue == a) idx = i;
        if (idx < 0) { idx = tracks.arraySize; tracks.arraySize++; tracks.GetArrayElementAtIndex(idx).objectReferenceValue = a; }
        while (starts.arraySize <= idx) { starts.arraySize++; starts.GetArrayElementAtIndex(starts.arraySize - 1).objectReferenceValue = null; }
        starts.GetArrayElementAtIndex(idx).objectReferenceValue = start;
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bgm);
    }

    // ---- 川の小物の岩（2026-10-25 てじゃ「手前から順番に」→ 止める岩・飛び石を、岩山と同じ色のごつごつした岩に。前は紫の球）----
    // 半径 0.5 の正二十面体を2回分けた球を、ノイズでごつごつさせた形。面は三角形ごとに頂点を分けて角ばらせる。flatTop は上を平らに（飛び石）
    static Material StoneMat() { return AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_CaveRock.mat"); }
    static Mesh BoulderMesh(int k, bool flatTop)
    {
        string path = MeshDir + "/Boulder" + k + (flatTop ? "_Flat" : "") + ".asset";
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var V = new List<Vector3> { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
            new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        var F = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        for (int it = 0; it < 2; it++)
        {
            var mid = new Dictionary<long, int>(); var F2 = new List<int>();
            int Mid(int a, int b) { long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (mid.TryGetValue(key, out int m)) return m; V.Add((V[a] + V[b]) * 0.5f); mid[key] = V.Count - 1; return V.Count - 1; }
            for (int i = 0; i < F.Count; i += 3)
            {
                int a = F[i], b = F[i + 1], c = F[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                F2.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            F = F2;
        }
        float off = k * 17.3f;
        for (int i = 0; i < V.Count; i++)
        {
            var n = V[i].normalized;
            // ゆるいでこぼこだけ（強いと水晶みたいにトゲトゲした。2026-10-25）
            float d = 1f + (Mathf.PerlinNoise(n.x * 1.2f + off, n.y * 1.2f + n.z * 0.7f) - 0.5f) * 0.32f + (Mathf.PerlinNoise(n.z * 2.2f + off, n.x * 2.2f + 5f) - 0.5f) * 0.08f;
            var q = n * 0.5f * d;
            if (flatTop && q.y > 0.3f) q.y = 0.3f + (q.y - 0.3f) * 0.15f;
            V[i] = q;
        }
        // 角ばった面：三角形ごとに頂点を分ける。表は外向き（Cross(b-a,c-a) が中心から外を向くように並べる）
        var V2 = new List<Vector3>(); var T2 = new List<int>();
        for (int i = 0; i < F.Count; i += 3)
        {
            Vector3 a = V[F[i]], b = V[F[i + 1]], c = V[F[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) { var tmp = b; b = c; c = tmp; }
            V2.Add(a); V2.Add(b); V2.Add(c);
            T2.AddRange(new[] { i, i + 1, i + 2 });
        }
        var m2 = new Mesh { name = "Boulder" + k + (flatTop ? "_Flat" : "") };
        m2.SetVertices(V2); m2.SetTriangles(T2, 0); m2.RecalculateNormals(); m2.RecalculateBounds();
        return Save(m2, path);
    }

    // ---- チェックポイント（2026-10-25）----
    // ステージ1の FuwaCourse3/Checkpoint1（光るキノコ＋丸太が道の両わき、通り抜けの判定、戻る場所）を写す。使っていない門の柱・横木は消す。
    // cps＝(中心線の位置 s, 横のずれ, 道の半分の幅, 道の上の面の高さ)。キノコは道のふちから CpOut 外。ふわふわの checkpoints にも登録（やり直しで光を消す）
    public static float CpOut = 0.6f;   // 0.35→0.6（てじゃ「道にうまらないように」）
    // lat＝中心線からの横のずれ（s ごと）、hwL/hwR＝左右それぞれ、道（または水路）のふちまでの距離
    static void Checkpoints(Transform root, (float s, Func<float, float> lat, float hwL, float hwR, float h)[] cps)
    {
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1");
        if (tmpl == null) { Debug.LogWarning("[2–1] チェックポイントのお手本（FuwaCourse3/Checkpoint1）がない"); return; }
        var g = new GameObject("Checkpoints").transform; g.SetParent(root, false);
        var made = new List<FuwaCheckpoint>();
        for (int i = 0; i < cps.Length; i++)
        {
            var (s, latF, hwL, hwR, h) = cps[i];
            float lat = latF(s), hw = Mathf.Min(hwL, hwR);
            Center(s, out var p, out var d); var q = p + Right(d) * lat;
            var cp = Object.Instantiate(tmpl, g, false); cp.name = "Checkpoint" + (i + 1);
            cp.transform.localPosition = new Vector3(q.x, h, q.y);
            cp.transform.localRotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
            foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
            // 通り抜けの判定は道の幅いっぱい（＋少し）
            var trig = cp.transform.Find("Trigger");
            if (trig) trig.localScale = new Vector3(hw * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
            // キノコの群れ：道のふちの外（お手本は道の半分の幅 約1.6m・ふちから約0.35m 外）
            var gm = cp.transform.Find("GlowMushrooms");
            if (gm)
                foreach (Transform cl in gm)
                {
                    // 群れは判定の約3m先に立つので、その先の中心線（曲がっている所でも）にそって道のふちの外へ（2026-10-25 てじゃ「左が離れてて右がめり込んでる」）
                    float sx = Mathf.Sign(cl.localPosition.x), sc2 = s + cl.localPosition.z;
                    Center(sc2, out var p2, out var d2);
                    var q2 = p2 + Right(d2) * (latF(sc2) + sx * ((sx > 0 ? hwR : hwL) + CpOut));
                    var w = root.TransformPoint(new Vector3(q2.x, h, q2.y));
                    var lp2 = cp.transform.InverseTransformPoint(w);
                    cl.localPosition = new Vector3(lp2.x, cl.localPosition.y, lp2.z);
                }
            var fc = cp.GetComponentInChildren<FuwaCheckpoint>(true);
            if (fc) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fc); NoSync(fc); made.Add(fc); }
        }
        // ふわふわの checkpoints に登録（作り直しで消えた古い物＝null は外す）
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault();
        if (ball)
        {
            var so = new SerializedObject(ball); var arr = so.FindProperty("checkpoints");
            for (int k = arr.arraySize - 1; k >= 0; k--) if (arr.GetArrayElementAtIndex(k).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(k);
            foreach (var fc in made) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fc; }
            so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ball);
        }
    }

    // ---- 渡り場の飛び石（2026-10-25）----
    // てじゃ「水の減速がけっこうなストレス」→「減速有りで飛び石かな」「ふわふわ当たってセーフなら」。
    // 切れた岸の道のはしから向こう岸の道のはしへ、川を斜めに横切って StoneCount 個。上の面は岸の道と同じ高さ、レイヤー0（ふわふわが当たっても乗ってもアウトにならない）。
    // from＝出発する岸（+1＝右岸、-1＝左岸）
    public static int StoneCount = 2;   // 3→2（てじゃ「石２個でいいかも」）
    // StoneSpread：川の流れの向きにどれだけずらして並べるか（片側、m）。0.5→2.5（てじゃ「飛び石、斜めぎみに配置しよう」。石2個で前後に約1.7mずれる）
    public static float StoneR = 0.42f, StoneSpread = 2.5f;
    static void CrossingStones(Transform root, string name, float sc, float from)
    {
        var g = new GameObject(name).transform; g.SetParent(root, false);
        // 円柱のメッシュは作った円柱から借りる（GetBuiltinResource("Cylinder.fst") は null で、当たり判定がなく石に乗れなかった）
        var tmpCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder); var cyl = tmpCyl.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmpCyl);
        var rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < StoneCount; i++)
        {
            StoneAt(sc, from, i, out float s, out float lat);
            Center(s, out var p, out var d); var q = p + Right(d) * lat;
            var st = new GameObject("Stone" + i); st.transform.SetParent(g, false);
            st.transform.localPosition = new Vector3(q.x, BankTop, q.y);
            st.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            // 当たり判定：平たい円柱（上の面＝岸の道の高さ、下は川底の下まで）
            var cg = new GameObject("Collider"); cg.transform.SetParent(st.transform, false);
            float h = BankTop + WaterDepth + 0.1f;
            cg.transform.localPosition = new Vector3(0, -h * 0.5f, 0); cg.transform.localScale = new Vector3(StoneR * 2f, h * 0.5f, StoneR * 2f);
            cg.AddComponent<MeshCollider>().sharedMesh = cyl; cg.GetComponent<MeshCollider>().convex = true;
            // 見た目：上が平たい丸い岩
            // 見た目：上が平たいごつごつした岩（岩山・土台と同じ色）
            var v = new GameObject("Rock"); v.transform.SetParent(st.transform, false);
            v.AddComponent<MeshFilter>().sharedMesh = BoulderMesh(i + (from > 0 ? 0 : 3), true);
            v.AddComponent<MeshRenderer>().sharedMaterial = StoneMat();
            float r = StoneR * (1.05f + 0.1f * (float)rng.NextDouble());
            // 上の平らな面（メッシュの y 0.3×0.42≒0.13）が岸の道の高さ（＝当たり判定の上の面）にそろうように
            v.transform.localPosition = new Vector3(0, -0.135f, 0); v.transform.localScale = new Vector3(r * 2.1f, 0.42f, r * 1.9f);
        }
    }

    // ---- 1回目の渡り場の先の岩（2026-10-25）----
    // 川を横切って岩が並んでいて、流れてきたふわふわはここで止まる（水は岩のすき間を抜ける）。止まったふわふわを左岸の道へすくい上げる。
    // てじゃ「そのまま川で2回目まで行けちゃう」→「アウトにならなくていいから岩で止まるようにしようか、まだ水の顔見せだし」。
    // 当たり判定は川幅いっぱいの見えない箱（レイヤー0＝当たるだけ、アウトにはならない）。見た目は丸い岩を並べる（仮）
    public static float StopAfter = 3.5f, StopTop = 0.7f;   // 0.8→3.5（てじゃ「もうちょい奥じゃないとプレイヤがわたりにくい」）
    static void StopRocks(Transform root)
    {
        var g = new GameObject("StopRocks").transform; g.SetParent(root, false);
        float s = sCross1 + CrossGap * 0.5f + StopAfter;
        Center(s, out var p, out var d); var r = Right(d);
        float hw = RiverHW(s) + RimW;
        var col = Box(g, "Block", new Vector3(p.x, (StopTop - WaterDepth - 0.1f) * 0.5f, p.y), new Vector3(hw * 2f, StopTop + WaterDepth + 0.1f, 0.7f), null, 0);
        col.transform.localRotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
        Object.DestroyImmediate(col.GetComponent<MeshRenderer>()); Object.DestroyImmediate(col.GetComponent<MeshFilter>());
        var rng = new System.Random(2511);
        int n = Mathf.CeilToInt(hw * 2f / 0.62f);
        for (int i = 0; i < n; i++)
        {
            float lat = Mathf.Lerp(-hw + 0.3f, hw - 0.3f, i / (float)(n - 1));
            float sz = 0.72f + 0.22f * (float)rng.NextDouble();
            var q = p + r * lat + d * (0.12f * ((float)rng.NextDouble() - 0.5f));
            var b = new GameObject("Rock" + i); b.transform.SetParent(g, false);
            b.AddComponent<MeshFilter>().sharedMesh = BoulderMesh(i % 6, false);
            b.AddComponent<MeshRenderer>().sharedMaterial = StoneMat();
            b.transform.localPosition = new Vector3(q.x, StopTop - sz * 0.5f + 0.05f, q.y);
            b.transform.localRotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y)) * Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            b.transform.localScale = new Vector3(sz * 1.1f, sz, sz * 0.9f);
        }
    }

    // ---- 2回目の渡り場の魚（2026-10-25）----
    // 2回目の渡り場の飛び石1個につき1匹。横の筋は FishLanes（石の外側）のまま、前後は石と同じ所が跳ぶ頂点になるように、FishSpan m 下流から上流へ跳ねる
    //（てじゃ「ちょうど石の位置がジャンプの頂点になるくらいに調整、あともっと高く飛ばそう、間隔はいまのままで」「高さと前後のはなし」「ラインは石にあわせないで」）。
    // 仮の形（あとで Blender）。2026-10-25 3匹→2匹（「むずすぎる」「２匹でいいきがする」）。石の上に立っている間は当たらない（safeSpots）
    public static float[] FishLanes = { -1.9f, 1.9f };
    public static float FishEdgeClear = 0.75f;   // 魚が出る・もぐる所と川のふちとのすき間（魚の体の半分くらい）   // ±1.0→±1.9（川を広げて、飛び石の外側を跳ぶ）。i 番目の魚が i 番目の石（左から）
    // 魚ごとのずらし（秒）
    public static float[] FishPhases = { 0f, -0.7f };
    // 2026-10-25 てじゃ「魚がよわい、もっとでかくして幅をとりたい、高さも３匹バラバラに」→ 大きさ FishScale 倍・跳ぶ長さ 1.7→2.4・高さは魚ごと
    public static float FishSpan = 2.4f, FishPeriod = 3.2f, FishDepth = 0.15f, FishScale = 2f, FishHit = 1.0f;
    public static float[] FishHeights = { 2.4f, 3.0f };   // 1.3/1.8 → 2.4/3.0（「もっと高く」）
    // 魚ごとの周期（秒）。高い方が長い（「高いほど周期を長く」で 0.8＋高さ×1.4 にした値を、高さを上げた後もそのまま）
    public static float[] FishPeriods = { 2.62f, 3.32f };
    // 飛び石の場所（CrossingStones と同じ式）：sc＝渡り場、from＝出発する岸、i 番目の石の中心線の位置 s と横のずれ lat
    static void StoneAt(float sc, float from, int i, out float s, out float lat)
    {
        float k = (i + 1f) / (StoneCount + 1f);
        lat = from * Mathf.Lerp(RiverHW(sc) + RimW, -(RiverHW(sc) + RimW), k);
        s = sc + Mathf.Lerp(-StoneSpread, StoneSpread, k);
    }
    static void RiverFish(Transform root, FuwaWaterFx fx)
    {
        var g = new GameObject("RiverFish").transform; g.SetParent(root, false);
        var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        Center(sCross2, out var cp, out var cd); var cr = Right(cd);
        var crossing = new GameObject("Crossing").transform; crossing.SetParent(g, false);
        crossing.localPosition = new Vector3(cp.x, 0f, cp.y);
        crossing.localRotation = Quaternion.LookRotation(new Vector3(-cd.x, 0, -cd.y));   // +Z＝川上
        // 材質は Blender の並び（胴・おなか・ひれ・目・目の光）
        var fishMesh = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaStage2/Models/Stage2_Fish.fbx").OfType<Mesh>().First();
        var fishMats = new[] { FlatMat("Stage2_FishBody", new Color(0.98f, 0.56f, 0.45f)), FlatMat("Stage2_FishBelly", new Color(1f, 0.9f, 0.8f)),
            FlatMat("Stage2_FishFin", new Color(0.9f, 0.42f, 0.36f)), FlatMat("Stage2_FishEye", new Color(0.12f, 0.12f, 0.16f)), FlatMat("Stage2_FishEyeHi", Color.white) };
        int n = FishLanes.Length;
        var fish = new Transform[n]; var from = new Transform[n]; var to = new Transform[n];
        for (int i = 0; i < n; i++)
        {
            // 頂点の前後＝i 番目の石（2回目の渡り場は左岸から出発＝from -1、石0が左）
            float sTop = sCross2;
            if (i < StoneCount) StoneAt(sCross2, -1f, i, out sTop, out _);
            Center(sTop + FishSpan, out var pa, out var da); Center(sTop - FishSpan, out var pb, out var db);
            // 出る所・もぐる所は、川のふちの岩から魚の体の半分以上はなす（川は渡り場の前後でせまくなるので、ふちにかすって痛そうだった。2026-10-27 てじゃ）
            float LaneAt(float ss) => Mathf.Sign(FishLanes[i]) * Mathf.Min(Mathf.Abs(FishLanes[i]), RiverHW(ss) - FishEdgeClear);
            var qa = pa + Right(da) * LaneAt(sTop + FishSpan); var qb = pb + Right(db) * LaneAt(sTop - FishSpan);
            var a = new GameObject("From" + i).transform; a.SetParent(g, false); a.localPosition = new Vector3(qa.x, -FishDepth, qa.y);
            var b = new GameObject("To" + i).transform; b.SetParent(g, false); b.localPosition = new Vector3(qb.x, -FishDepth, qb.y);
            from[i] = a; to[i] = b;
            // 魚：Blender（FLUFF_models.blend の Stage2Fish シーン、Stage2_Fish。+Z が頭）。2026-10-25 仮のカプセルから差し替え
            var f = new GameObject("Fish" + i).transform; f.SetParent(g, false); f.localPosition = a.localPosition;
            f.gameObject.AddComponent<MeshFilter>().sharedMesh = fishMesh;
            var fmr = f.gameObject.AddComponent<MeshRenderer>(); fmr.sharedMaterials = fishMats; fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            f.localScale = Vector3.one * FishScale;
            f.gameObject.SetActive(false);
            fish[i] = f;
        }
        AudioSource Snd(string name, string clip, float vol)
        {
            var go = new GameObject(name); go.transform.SetParent(g, false);
            var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 1f; s.minDistance = 2f; s.maxDistance = 20f; s.rolloffMode = AudioRolloffMode.Linear;
            s.volume = vol; s.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clip); return s;
        }
        var rf = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaRiverFish>(g.gameObject);
        rf.ball = ballProxy; rf.fx = fx; rf.crossing = crossing;
        rf.fish = fish; rf.jumpFrom = from; rf.jumpTo = to; rf.phases = (float[])FishPhases.Clone(); rf.period = FishPeriod;
        // 飛び石の上は安全（てじゃ「魚、飛び石の上は安全な感じにしたい」）。CrossingStones を先に作っておく
        var stones2 = root.Find("Stones2");
        if (stones2) { rf.safeSpots = stones2.Cast<Transform>().ToArray(); rf.safeRadius = StoneR + 0.15f; }
        rf.jumpHeights = (float[])FishHeights.Clone(); rf.periods = (float[])FishPeriods.Clone(); rf.hitRadius = FishHit; rf.playerHitRadius = FishHit * 0.8f;
        rf.jumpSound = Snd("JumpSfx", "Assets/FuwaStage2/Audio/sfx_water_splash_soft.wav", 0.6f);
        rf.hitSound = Snd("HitSfx", "Assets/FuwaCourse/Audio/sfx_boing.wav", 0.8f);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(rf); NoSync(rf);
    }

    // ---- 1回目の渡り場の先の岩の列の番の魚（2026-10-27）----
    // てじゃ「この岩、ふわふわ飛び越えて進めちゃう」「見通し悪くなるのやだなぁ　岩の上をふわふわが通ると魚が飛び出して押し返すとか」「プレイヤにあたってもいいかも」。
    // 岩の列の上の空中を川下へ通ろうとするふわふわに、岩の下流側の水面から魚が飛び出して当たり、川上へ押し返す（FuwaGuardFish）。魚は2回目の渡り場と同じ見た目
    static void GuardFish(Transform root, FuwaWaterFx fx)
    {
        var g = new GameObject("GuardFish").transform; g.SetParent(root, false);
        float s = sCross1 + CrossGap * 0.5f + StopAfter;
        Center(s, out var p, out var d);
        var line = new GameObject("Line").transform; line.SetParent(g, false);
        line.localPosition = new Vector3(p.x, 0f, p.y);
        line.localRotation = Quaternion.LookRotation(new Vector3(-d.x, 0, -d.y));   // +Z＝川上
        var fishMesh = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaStage2/Models/Stage2_Fish.fbx").OfType<Mesh>().First();
        var fishMats = new[] { FlatMat("Stage2_FishBody", new Color(0.98f, 0.56f, 0.45f)), FlatMat("Stage2_FishBelly", new Color(1f, 0.9f, 0.8f)),
            FlatMat("Stage2_FishFin", new Color(0.9f, 0.42f, 0.36f)), FlatMat("Stage2_FishEye", new Color(0.12f, 0.12f, 0.16f)), FlatMat("Stage2_FishEyeHi", Color.white) };
        var f = new GameObject("Fish").transform; f.SetParent(g, false);
        f.gameObject.AddComponent<MeshFilter>().sharedMesh = fishMesh;
        var fmr = f.gameObject.AddComponent<MeshRenderer>(); fmr.sharedMaterials = fishMats; fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        f.localScale = Vector3.one * FishScale; f.gameObject.SetActive(false);
        AudioSource Snd(string name, string clip, float vol)
        {
            var go = new GameObject(name); go.transform.SetParent(g, false);
            var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 1f; a.minDistance = 2f; a.maxDistance = 20f; a.rolloffMode = AudioRolloffMode.Linear;
            a.volume = vol; a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clip); return a;
        }
        var gf = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaGuardFish>(g.gameObject);
        gf.ball = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>(); gf.fx = fx; gf.fish = f; gf.line = line;
        gf.halfWidth = RiverHW(s) + RimW; gf.minHeight = StopTop + 0.2f; gf.rockTop = StopTop;
        gf.jumpSound = Snd("JumpSfx", "Assets/FuwaStage2/Audio/sfx_water_splash_soft.wav", 0.6f);
        gf.hitSound = Snd("HitSfx", "Assets/FuwaCourse/Audio/sfx_boing.wav", 0.8f);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(gf); NoSync(gf);
    }

    static void ClamWalk(Transform root, Material ground)
    {
        var g = new GameObject("ClamWalk").transform; g.SetParent(root, false);
        var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        float len = sWalk1 - sStraight0;   // 土台とアウト判定は、手前のまっすぐな道（sStraight0〜）から
        // 道の下の土台（堤）と、両わきの崖（滝が流れ落ちる岩）
        Sweep(g, "WalkBase", sStraight0, sWalk1, s => -WalkHW, s => WalkHW, s => -8f, s => WalkH(s) - Thick - 0.01f, ground, 0, true);
        // 両わきの岩の壁。上は一面の水で、道の側のふちから全部カーテンの滝で落ちる（ClamWall）
        foreach (float o in new[] { -1f, 1f }) ClamWall(root, g, o);
        // アウト判定：道のふちの外の下（ふわふわ＝FuwaHazard、人＝FuwaPlayerCatcher）。4m ごとの箱
        var outs = new GameObject("Outs").transform; outs.SetParent(g, false);
        int nSeg = Mathf.Max(1, Mathf.CeilToInt(len / 4f));
        for (int k = 0; k < nSeg; k++)
        {
            float a = Mathf.Lerp(sStraight0, sWalk1, k / (float)nSeg), b = Mathf.Lerp(sStraight0, sWalk1, (k + 1) / (float)nSeg), m = (a + b) * 0.5f;
            Center(m, out var cp, out var cd); var cr = Right(cd);
            float segLen = b - a + 0.4f, wOut = FallsOut + 0.3f;
            foreach (float o in new[] { -1f, 1f })
            {
                var c = cp + cr * (o * (WalkHW + 0.15f + wOut * 0.5f));
                var bo = new GameObject((o < 0 ? "BallOutL" : "BallOutR") + k); bo.transform.SetParent(outs, false); bo.layer = 4;
                bo.transform.localPosition = new Vector3(c.x, WalkH(m) - 2.4f, c.y); bo.transform.localRotation = Quaternion.LookRotation(new Vector3(cd.x, 0, cd.y));
                var bbc = bo.AddComponent<BoxCollider>(); bbc.isTrigger = true; bbc.size = new Vector3(wOut, 2.4f, segLen);
                var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(bo); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); NoSync(hz);
                var po = new GameObject((o < 0 ? "PlayerOutL" : "PlayerOutR") + k); po.transform.SetParent(outs, false);
                po.transform.localPosition = new Vector3(c.x, WalkH(m) - 4.2f, c.y); po.transform.localRotation = bo.transform.localRotation;
                var pbc = po.AddComponent<BoxCollider>(); pbc.isTrigger = true; pbc.size = new Vector3(wOut, 2.4f, segLen);
                var pcc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(po); pcc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pcc); NoSync(pcc);
            }
        }
        // 貝（仮：つぶした球の上下の殻。ふたは後ろのちょうつがいで開く）と、反対側の滝
        var shellMat = FlatMat("Stage2_ClamShell", new Color(0.96f, 0.80f, 0.76f));
        var innerMat = FlatMat("Stage2_ClamInner", new Color(0.98f, 0.95f, 0.97f));
        // 泡の材質：しゃぼんの虹色はやめて、全体をうっすら薄い青に（2026-10-22 てじゃ「しゃぼんっぽい虹色を消してもうちょい青っぽい」「全体的に薄青」「弱めの」）。
        // れんしゅう等の Bubble3D とは別の材質（無い時だけ作る。色はこの材質で調整）
        const string cbPath = "Assets/FuwaStage2/Materials/Stage2_ClamBubble.mat";
        var bubbleMat = AssetDatabase.LoadAssetAtPath<Material>(cbPath);
        if (!bubbleMat)
        {
            bubbleMat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Bubble3D.mat")) { name = "Stage2_ClamBubble" };
            bubbleMat.SetFloat("_Iris", 0f); bubbleMat.SetColor("_Tint", new Color(0.58f, 0.8f, 1f, 1f)); bubbleMat.SetFloat("_FillAlpha", 0.12f);
            AssetDatabase.CreateAsset(bubbleMat, cbPath);
        }
        int n = ClamAt.Length;
        var clams = new Transform[n]; var lids = new Transform[n]; var mouths = new Transform[n]; var phases = new float[n];
        var cg = new GameObject("Clams").transform; cg.SetParent(g, false);
        for (int i = 0; i < n; i++)
        {
            float side = ClamSide[i];
            float baseH = ClamHigh[i] ? ClamHighBase : ClamBase;
            float sc = Mathf.Lerp(sWalk0, sWalk1, ClamAt[i]);
            Center(sc, out var p, out var d); var r = Right(d);
            float y = WalkH(sc);
            var c2 = p + r * (side * (WalkHW + ClamOut));
            // 台の岩（上が貝の底＝道より baseH 高い。下は道の 1.65m 下まで）
            float rockH = baseH + 1.65f;
            var rock = Box(cg, "ClamRock" + i, new Vector3(c2.x, y + baseH - rockH * 0.5f, c2.y), new Vector3(1.4f, rockH, 1.3f), ground, 0);
            rock.transform.localRotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
            // 見た目は上の平たいごつごつ岩（箱は当たり判定だけ）。2026-10-25 壁を岩にしたのにあわせて
            rock.GetComponent<Renderer>().enabled = false;
            var bm = BoulderMesh(10 + i, true); var bb = bm.bounds;
            var rv = new GameObject("ClamRockVisual" + i); rv.transform.SetParent(cg, false);
            rv.AddComponent<MeshFilter>().sharedMesh = bm; rv.AddComponent<MeshRenderer>().sharedMaterial = StoneMat();
            float sy = (rockH + 0.6f) / bb.size.y;
            rv.transform.localRotation = rock.transform.localRotation * Quaternion.Euler(0, 37f * i, 0);
            rv.transform.localScale = new Vector3(1.75f / bb.size.x, sy, 1.65f / bb.size.z);
            rv.transform.localPosition = new Vector3(c2.x, y + baseH - 0.03f - bb.max.y * sy, c2.y);
            var clam = new GameObject("Clam" + i).transform; clam.SetParent(cg, false);
            clam.localPosition = new Vector3(c2.x, y + baseH, c2.y);
            clam.localRotation = Quaternion.LookRotation(new Vector3(-side * r.x, 0, -side * r.y));   // +Z＝道を横切る向き
            var bot = GameObject.CreatePrimitive(PrimitiveType.Sphere); bot.name = "ShellBottom"; bot.transform.SetParent(clam, false);
            Object.DestroyImmediate(bot.GetComponent<Collider>());
            bot.transform.localPosition = new Vector3(0, 0.12f, 0); bot.transform.localScale = new Vector3(1.0f, 0.3f, 0.85f);
            bot.GetComponent<Renderer>().sharedMaterial = shellMat;
            var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere); inner.name = "Inner"; inner.transform.SetParent(clam, false);
            Object.DestroyImmediate(inner.GetComponent<Collider>());
            inner.transform.localPosition = new Vector3(0, 0.2f, 0.02f); inner.transform.localScale = new Vector3(0.82f, 0.12f, 0.68f);
            inner.GetComponent<Renderer>().sharedMaterial = innerMat;
            var lid = new GameObject("LidHinge").transform; lid.SetParent(clam, false); lid.localPosition = new Vector3(0, 0.18f, -0.38f);
            var top = GameObject.CreatePrimitive(PrimitiveType.Sphere); top.name = "ShellTop"; top.transform.SetParent(lid, false);
            Object.DestroyImmediate(top.GetComponent<Collider>());
            top.transform.localPosition = new Vector3(0, 0.06f, 0.38f); top.transform.localScale = new Vector3(1.0f, 0.28f, 0.85f);
            top.GetComponent<Renderer>().sharedMaterial = shellMat;
            var mouth = new GameObject("Mouth").transform; mouth.SetParent(clam, false); mouth.localPosition = new Vector3(0, 0.95f, 0.75f);
            clams[i] = clam; lids[i] = lid; mouths[i] = mouth;
            // 泡を出すタイミング：ふつうの貝は前と同じ（ふつうの貝どうしで 1 周を等分）。高い貝は両どなりのふつうの貝のまん中の、ちょうど半周ずらし
            //（2026-10-22 てじゃ「感覚はちょうど低いやつと逆になる感じ」：低いのが出ていない時に高いのが出る）
            phases[i] = ClamPhase(i);
        }
        // 泡（貝ごとに3つ）・割れた時の粒・音
        var bg = new GameObject("Bubbles").transform; bg.SetParent(g, false);
        const int per = 3;
        var bubbles = new Transform[n * per];
        for (int k = 0; k < bubbles.Length; k++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "Bubble" + k; b.transform.SetParent(bg, false);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.GetComponent<Renderer>().sharedMaterial = bubbleMat;
            b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.SetActive(false); bubbles[k] = b.transform;
        }
        // 泡の地面の影（泡1つにつき1つのデカールの箱。うすい水色の輪＋まん中の光。FuwaStage2/BubbleShadow）。2026-10-22 てじゃ「地面に影ないのつらい」
        const string bsPath = "Assets/FuwaStage2/Materials/Stage2_BubbleShadow.mat";
        var shadowMat = AssetDatabase.LoadAssetAtPath<Material>(bsPath);
        if (!shadowMat) { shadowMat = new Material(Shader.Find("FuwaStage2/BubbleShadow")) { name = "Stage2_BubbleShadow" }; AssetDatabase.CreateAsset(shadowMat, bsPath); }
        var sg = new GameObject("BubbleShadows").transform; sg.SetParent(g, false);
        var shadows = new Renderer[bubbles.Length];
        for (int k = 0; k < shadows.Length; k++)
        {
            var sh = GameObject.CreatePrimitive(PrimitiveType.Cube); sh.name = "Shadow" + k; sh.transform.SetParent(sg, false);
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            var sr = sh.GetComponent<Renderer>(); sr.sharedMaterial = shadowMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
            sr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; sr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            sr.enabled = false; shadows[k] = sr;
        }
        var pop = new GameObject("PopFx"); pop.transform.SetParent(g, false);
        var pp = pop.AddComponent<ParticleSystem>(); pp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var pm = pp.main; pm.loop = false; pm.playOnAwake = false; pm.simulationSpace = ParticleSystemSimulationSpace.World;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); pm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); pm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        pm.startColor = new Color(0.9f, 1f, 1f, 0.95f); pm.maxParticles = 60; pm.gravityModifier = 0.3f;
        var pem = pp.emission; pem.enabled = false;
        var psh = pp.shape; psh.shapeType = ParticleSystemShapeType.Sphere; psh.radius = 0.28f;
        var ppr = pop.GetComponent<ParticleSystemRenderer>(); ppr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        ppr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AudioSource Snd(string name, string clip)
        {
            var go = new GameObject(name); go.transform.SetParent(g, false);
            var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 1f; a.minDistance = 2f; a.maxDistance = 20f; a.rolloffMode = AudioRolloffMode.Linear;
            a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clip); return a;
        }
        var cb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaClamBubbles>(g.gameObject);
        cb.ball = ballProxy; cb.clams = clams; cb.lids = lids; cb.mouths = mouths; cb.phases = phases; cb.period = ClamPeriod;
        cb.bubbles = bubbles; cb.bubblesPerClam = per; cb.shadows = shadows;
        cb.bubbleSize = ClamBubbleSize; cb.captureRadius = ClamCapture;
        // 吹き出し口から反対側の滝まで（ここで割れて落ちる）
        cb.bubbleRange = 2f * WalkHW + ClamOut + FallsOut - 0.2f;
        cb.popFx = pp; cb.popSound = Snd("PopSfx", "Assets/FuwaStage2/Audio/sfx_bubble_pop.wav"); cb.captureSound = Snd("CaptureSfx", "Assets/FuwaStage2/Audio/sfx_bubble_wrap.wav");
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(cb); NoSync(cb);
    }

    // ---- 貝の道の両わきの岩の壁と、上の水・カーテンの滝（2026-10-25）----
    // てじゃ「壁のうちがわ、全部まるっと滝にしちゃうとか」「壁は岩で」「上は一面水で、数カ所青いたま浮かせとこ」：
    // 前は箱の崖＋貝の反対側だけ 5m の滝。今は浮島みたいに下がすぼまった岩の壁（土台と同じ色）の上が一面の水（道にそってゆるい坂、奥から手前へ流れる）で、
    // 道の側のふちから全部カーテンの滝で落ちる。滝の中はふわふわを押し落とす（前の滝と同じ FuwaWaterfall を 2.5m ごと）。上に青い球をいくつか
    // WallIn＝道の中心線から壁のふちまで、WallW＝壁の上の幅、WallSurf＝道からの水面の高さ、WallBottom＝滝の下のはし
    public static float WallIn = 4.55f, WallW = 2.9f, WallSurf = 4.3f, WallBottom = -8f;
    public static float WallFlow = 0.8f;
    // 球の高さ（水面から）。2.0 だと下の水の筋が街灯の柱みたいだった（2026-10-25 てじゃ）→ 1.0
    public static float WallOrbHeight = 1.0f;
    // 球の場所（壁の長さの割合）。WallDiv 番目の球の下が水の分かれ目（そこから両はしへ流れて、はしの切れ目から滝で落ちる）
    public static float[] WallOrbsL = { 0.2f, 0.55f }, WallOrbsR = { 0.4f, 0.85f };
    public static int WallDivL = 1, WallDivR = 0;
    // はしの切れ目の滝の幅（壁の上の水の幅に対する割合）
    public static float WallEndSpill = 1f;
    // 壁のはしの丸み（上から見て、はしから WallRoundLen m で四分円）。2026-10-25 てじゃ「壁のはしが縦長の柱っぽい」→ 丸くすぼめる
    public static float WallRoundLen = 2f, WallTopMin = 0.55f;
    static float WallRound(float s, float s0, float s1)
    {
        float t = Mathf.Clamp01(Mathf.Min(s - s0, s1 - s) / WallRoundLen);
        return Mathf.Max(0.12f, Mathf.Sqrt(1f - (1f - t) * (1f - t)));
    }
    static void ClamWall(Transform root, Transform g, float o)
    {
        string nm = o < 0 ? "WallL" : "WallR";
        float s0 = sWalk0 + 0.3f, s1 = sWalk1 - 0.3f;
        const float step = 0.5f;
        var ss = new List<float>(); for (float s = s0; s < s1 - 1e-3f; s += step) ss.Add(s); ss.Add(s1);
        int n = ss.Count;
        float nOff = o < 0 ? 13.7f : 71.3f;
        Func<float, float> surf = s => WalkH(Mathf.Clamp(s, sWalk0, sWalk1)) + WallSurf;
        // 壁の上の幅（少しゆらす）。上の水・滝の幅も同じ値から（前は水だけゆらさない幅で、狭い所で滝のはしが外のふちの岩をつき抜けた）
        Func<float, float> wNoise = s => WallW + 0.35f * (Mathf.PerlinNoise(nOff, s * 0.15f) - 0.5f);
        // 岩：断面（ふちから外へ d、水面からの高さ y）を中心線にそって並べる。最後の点のあと最初に戻って閉じる
        var V = new List<Vector3>(); var T = new List<int>(); int cols = 0;
        var cen = new List<Vector3>();
        for (int i = 0; i < n; i++)
        {
            float s = ss[i]; Center(s, out var p, out var d); var r = Right(d) * o;
            float sf = surf(s), w = wNoise(s), wy = WalkH(Mathf.Clamp(s, sWalk0, sWalk1));
            // 両はしは切れ目（せき止めず、上の水がそのまま滝でこぼれる）。2026-10-25 てじゃ「入口みたいに切れ目つけて垂らす？」
            bool cap = false;
            float rf = WallRound(s, s0, s1);
            float fl = cap ? 0.25f : -0.3f, lip = cap ? 0.25f : -0.06f;
            var prof = new List<Vector2>
            {
                new Vector2(0f, lip), new Vector2(0.25f, fl), new Vector2(w - 0.45f, fl), new Vector2(w - 0.4f, 0.25f), new Vector2(w, 0.3f),
                new Vector2(w + 0.35f, -0.6f), new Vector2(w + 0.5f, -2.5f), new Vector2(w + 0.2f, wy - 5f - sf), new Vector2(w * 0.5f, wy - 11.5f - sf),
                new Vector2(0.35f, WallBottom - 0.5f - sf), new Vector2(0.18f, -1.2f),
            };
            cols = prof.Count;
            var c = Vector3.zero;
            for (int k = 0; k < cols; k++)
            {
                var pr = prof[k];
                bool fixedPt = k <= 4;   // 上（ふち・水の底・外のふち）はでこぼこさせない
                float dd = pr.x + (fixedPt ? 0f : (Mathf.PerlinNoise(nOff + s * 0.5f, k * 1.9f) - 0.5f) * 0.5f);
                float yy = pr.y + (fixedPt ? 0f : (Mathf.PerlinNoise(nOff + 30f + s * 0.4f, k * 2.7f) - 0.5f) * 0.6f);
                // はしは丸くすぼめる：道の側のふちはそのまま、外へ出る分と下へ伸びる分を縮める
                // 上（k 1〜4）は切れ目の水の幅を残す
                // 外の面の上の方（k 5・6）も上と同じ幅から縮め始める（2026-10-25 てじゃ「端にとがってるのが」→ 前は外のふちだけはしで外に残って、下がえぐれてツノになってた）
                float topK = Mathf.Max(rf, WallTopMin);
                dd *= k <= 5 ? topK : k == 6 ? Mathf.Lerp(topK, rf, 0.5f) : rf; if (k >= 6) yy *= Mathf.Lerp(0.3f, 1f, rf);
                var q = p + r * (WallIn + dd);
                var v = new Vector3(q.x, sf + yy, q.y); V.Add(v); c += v;
            }
            cen.Add(c / cols);
        }
        for (int i = 0; i < n - 1; i++)
            for (int k = 0; k < cols; k++)
            {
                int k2 = (k + 1) % cols;
                int a = i * cols + k, b = a + cols, a2 = i * cols + k2, b2 = a2 + cols;
                var mid = (cen[i] + cen[i + 1]) * 0.5f;
                var want = (V[a] + V[a2] + V[b] + V[b2]) * 0.25f - mid;
                AddTriDir(V, T, a, a2, b, want); AddTriDir(V, T, a2, b2, b, want);
            }
        foreach (int i in new[] { 0, n - 1 })
        {
            int ci = V.Count; V.Add(cen[i]);
            var want = cen[i] - cen[i == 0 ? 1 : n - 2];
            for (int k = 0; k < cols; k++) AddTriDir(V, T, ci, i * cols + k, i * cols + (k + 1) % cols, want);
        }
        var V2 = new List<Vector3>(); var T2 = new List<int>();
        for (int i = 0; i < T.Count; i++) { V2.Add(V[T[i]]); T2.Add(i); }
        var mesh = new Mesh { name = "Course2_1_Clam" + nm, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V2); mesh.SetTriangles(T2, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/Clam" + nm + ".asset");
        var go = new GameObject(nm); go.transform.SetParent(g, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = RockBaseMat();
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        // 上の水面（見た目だけ）：ふちの少し外から外のふちの内側まで。流れは手前（s が減る＝下り）へ
        float wa = s0, wb = s1;
        float sDiv = Mathf.Lerp(wa, wb, (o < 0 ? WallOrbsL : WallOrbsR)[o < 0 ? WallDivL : WallDivR]);
        Func<float, float> waterW = s => Mathf.Max(0.3f, (wNoise(s) - 0.45f) * Mathf.Max(WallRound(s, s0, s1), WallTopMin) - 0.08f);
        {
            var SV = new List<Vector3>(); var ST = new List<int>(); var U1 = new List<Vector4>(); var U2 = new List<Vector2>();
            const int across = 6;
            int m = Mathf.Max(1, Mathf.CeilToInt((wb - wa) / 0.25f));
            for (int k = 0; k <= m; k++)
            {
                float s = Mathf.Lerp(wa, wb, k / (float)m); Center(s, out var p, out var d); var r = Right(d) * o;
                float dA = -0.03f, dB = waterW(s), hw = (dB - dA) * 0.5f;
                for (int i = 0; i <= across; i++)
                {
                    float dd = Mathf.Lerp(dA, dB, i / (float)across);
                    var q = p + r * (WallIn + dd); SV.Add(new Vector3(q.x, surf(s), q.y));
                    U1.Add(new Vector4(dd - (dA + hw), hw, s - wa, wb - s)); U2.Add(new Vector2(Mathf.Abs(s - sDiv), 0));   // 分かれ目から両はしへ
                }
            }
            for (int k = 0; k < m; k++)
                for (int i = 0; i < across; i++)
                {
                    int a = k * (across + 1) + i, b = a + across + 1;
                    AddTriDir(SV, ST, a, b, a + 1, Vector3.up); AddTriDir(SV, ST, a + 1, b, b + 1, Vector3.up);
                }
            var sm = new Mesh { name = "Course2_1_Clam" + nm + "_Water", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            sm.SetVertices(SV); sm.SetTriangles(ST, 0); sm.SetUVs(0, SV.Select(v => Vector2.zero).ToList()); sm.SetUVs(1, U1); sm.SetUVs(2, U2);
            sm.normals = SV.Select(v => Vector3.up).ToArray(); sm.RecalculateBounds();
            sm = Save(sm, MeshDir + "/Clam" + nm + "_Water.asset");
            var wgo = new GameObject(nm + "_Water"); wgo.transform.SetParent(g, false);
            wgo.AddComponent<MeshFilter>().sharedMesh = sm; wgo.AddComponent<MeshRenderer>().sharedMaterial = WaterMat(WallFlow, true);
        }

        // カーテンの滝：水面からふちを回りこんで、真下へ。断面（ふちからの d、水面からの y。NaN＝下のはし）
        // ふちの線は、はしの切れ目（はしの面）→ 角 → 道の側のふち → 角 → もう一方のはしの切れ目、と1枚でつなぐ（2026-10-25 てじゃ「はしの滝が不自然」→ 箱の滝をやめて同じ形で回りこむ）
        {
            var prof = new List<Vector2> { new Vector2(0.3f, 0.012f), new Vector2(0.02f, 0.012f), new Vector2(-0.18f, -0.15f), new Vector2(-0.3f, -0.6f), new Vector2(-0.34f, -1.5f), new Vector2(-0.35f, float.NaN) };
            int pc = prof.Count;
            // ふちの線：(位置（水面の高さ）, 外向き（水が落ちる向き。角はななめで長さ √2＝角をとがらせる）)
            var lip = new List<Vector3>(); var outs = new List<Vector3>();
            void EndEdge(float se, bool first)
            {
                Center(se, out var p, out var d); var r = Right(d) * o; var t3 = new Vector3(d.x, 0, d.y) * (first ? -1f : 1f);
                float ww = waterW(se) * WallEndSpill; int cnt = Mathf.Max(2, Mathf.CeilToInt(ww / 0.3f));
                for (int k = 0; k < cnt; k++)
                {
                    float dd = first ? Mathf.Lerp(ww, 0f, k / (float)cnt) : Mathf.Lerp(0f, ww, (k + 1) / (float)cnt);
                    if (!first && k == cnt - 1) dd = ww;
                    var q = p + r * (WallIn + dd); lip.Add(new Vector3(q.x, surf(se), q.y)); outs.Add(t3);
                }
            }
            void Corner(float se, bool first)
            {
                Center(se, out var p, out var d); var r = Right(d) * o; var t3 = new Vector3(d.x, 0, d.y) * (first ? -1f : 1f);
                var q = p + r * WallIn; lip.Add(new Vector3(q.x, surf(se), q.y)); outs.Add((t3 - new Vector3(r.x, 0, r.y)));   // 長さ √2
            }
            EndEdge(wa, true); Corner(wa, true);
            int m = Mathf.Max(1, Mathf.CeilToInt((wb - wa) / 0.4f));
            for (int k = 1; k < m; k++)
            {
                float s = Mathf.Lerp(wa, wb, k / (float)m); Center(s, out var p, out var d); var r = Right(d) * o;
                var q = p + r * WallIn; lip.Add(new Vector3(q.x, surf(s), q.y)); outs.Add(-new Vector3(r.x, 0, r.y));
            }
            Corner(wb, false); EndEdge(wb, false);
            LipFalls(g, "Clam" + nm + "_Falls", nm + "_Falls", lip, outs, prof, WallBottom);
        }

        // 押す範囲：2.5m ごとの箱（前の滝と同じ。厚み 0.9m、水面の少し上から下のはしの 0.35m 上まで）
        var pushes = new GameObject(nm + "_Push").transform; pushes.SetParent(g, false);
        int nSeg = Mathf.Max(1, Mathf.CeilToInt((wb - wa) / 2.5f));
        for (int k = 0; k < nSeg; k++)
        {
            float a = Mathf.Lerp(wa, wb, k / (float)nSeg), b = Mathf.Lerp(wa, wb, (k + 1) / (float)nSeg), mm = (a + b) * 0.5f;
            Center(a, out var pa, out var da); Center(b, out var pb, out var db);
            pa += Right(da) * o * (WallIn - 0.25f); pb += Right(db) * o * (WallIn - 0.25f);
            Center(mm, out _, out var dm); var rm = Right(dm) * o;
            float top = surf(mm) + 0.4f, bot = WallBottom + 0.35f;
            var t = new GameObject("Push" + k); t.transform.SetParent(pushes, false); t.layer = 4;
            var mid2 = (pa + pb) * 0.5f;
            t.transform.localPosition = new Vector3(mid2.x, (top + bot) * 0.5f, mid2.y);
            t.transform.localRotation = Quaternion.LookRotation(new Vector3(-rm.x, 0, -rm.y));   // +Z＝道の方（前の滝と同じ）
            var bc = t.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3((pb - pa).magnitude + 0.05f, top - bot, 0.9f);
            var wf = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWaterfall>(t);
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(wf); NoSync(wf);
        }

        // 音（ふちにそって何か所か）
        for (int k = 0; k < 3; k++)
        {
            float s = Mathf.Lerp(wa, wb, (k + 0.5f) / 3f); Center(s, out var p, out var d); var q = p + Right(d) * o * (WallIn - 0.3f);
            var au = new GameObject(nm + "_Sound" + k); au.transform.SetParent(g, false); au.transform.localPosition = new Vector3(q.x, WalkH(s) + 0.5f, q.y);
            var a = au.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_waterfall_loop.wav");
            a.loop = true; a.playOnAwake = true; a.spatialBlend = 1f; a.minDistance = 1.5f; a.maxDistance = 14f; a.rolloffMode = AudioRolloffMode.Linear; a.volume = 0.1f;
        }

        // 青い球（水面の上に浮かんで、水を落とす）。落ちる所は湧き水の波紋
        foreach (float at in o < 0 ? WallOrbsL : WallOrbsR)
        {
            float s = Mathf.Lerp(wa, wb, at); Center(s, out var p, out var d); var q = p + Right(d) * o * (WallIn + WallW * 0.45f);
            var pos = new Vector3(q.x, surf(s), q.y);
            var sp = new GameObject(nm + "_Orb" + at.ToString("F2")).transform; sp.SetParent(g, false);
            Spring(sp, pos);
            WaterOrb(sp, pos, "WaterOrb", WallOrbHeight, 0.36f, 0.24f);
        }
    }

    // 水の端の滝（Waterfall で作った g の "EndFalls"）の見た目を、ふちからこぼれる膜にする。押す範囲・霧・音は Waterfall のまま
    // （2026-10-26 前の箱の滝だと、落ち口と滝のすき間から底の切り口が板に見えた）。c＝落ち口のまん中、o＝落ちる向き、r＝横、hw＝半分の幅
    static void EndLipFalls(Transform g, string meshName, Vector2 c, Vector2 o, Vector2 r, float hw, float surf, float bottom)
    {
        var cur = g.Find("EndFalls/Curtain"); if (cur && cur.GetComponent<Renderer>()) cur.GetComponent<Renderer>().enabled = false;
        var lip = new List<Vector3>(); var outs = new List<Vector3>();
        int n = Mathf.Max(2, Mathf.CeilToInt(hw * 2f / 0.3f));
        for (int k = 0; k <= n; k++) { var q = c + r * Mathf.Lerp(-hw, hw, k / (float)n); lip.Add(new Vector3(q.x, surf, q.y)); outs.Add(new Vector3(o.x, 0, o.y)); }
        LipFalls(g, meshName, "EndFallsLip", lip, outs, new List<Vector2> { new Vector2(0.3f, 0.012f), new Vector2(0.02f, 0.012f), new Vector2(-0.18f, -0.15f), new Vector2(-0.3f, -0.6f), new Vector2(-0.34f, -1.5f), new Vector2(-0.35f, float.NaN) }, bottom);
    }

    // ---- ふちからこぼれる水の膜（貝の道の壁・ラストの滝の門）----
    // lip＝ふちの線（水面の高さ）、outs＝水が出ていく向き（ふちごと）、prof＝断面（ふちから内へ x、水面からの y。NaN＝bottomY まで）
    // flip＝内向きの面（厚みのある滝の裏側。材質は表だけ描くので、裏から見えるように別に作る）
    static GameObject LipFalls(Transform g, string meshName, string goName, List<Vector3> lip, List<Vector3> outs, List<Vector2> prof, float bottomY, bool flip = false)
    {
        int nl = lip.Count, pc = prof.Count;
        var grid = new Vector3[nl, pc]; var lens = new float[nl, pc]; var along = new float[nl];
        float maxLen = 0f; Vector3 prev = Vector3.zero;
        for (int k = 0; k < nl; k++)
        {
            float sf = lip[k].y;
            for (int j = 0; j < pc; j++)
            {
                var pr = prof[j]; float yy = float.IsNaN(pr.y) ? bottomY - sf : pr.y;
                var q = lip[k] - outs[k] * pr.x; grid[k, j] = new Vector3(q.x, sf + yy, q.z);
            }
            // 横の長さ（ふちにそった m）
            var lipP = grid[k, 1];
            along[k] = k == 0 ? 0f : along[k - 1] + (lipP - prev).magnitude; prev = lipP;
            lens[k, pc - 1] = 0f;
            for (int j = pc - 2; j >= 0; j--) lens[k, j] = lens[k, j + 1] + (grid[k, j] - grid[k, j + 1]).magnitude;
            maxLen = Mathf.Max(maxLen, lens[k, 0]);
        }
        return LipFallsMesh(g, meshName, goName, grid, (k, j) =>
        {
            var ow = (outs[k] + outs[k + 1]).normalized;
            var t = prof[j + 1] - prof[j]; if (float.IsNaN(t.y)) t = new Vector2(t.x, -1f);
            var want = -ow * t.y + Vector3.up * -t.x;   // 断面の (dy, -dd)：外（水の落ちる側）・上を向く面
            return flip ? -want : want;
        });
    }
    // 格子の点（[横の列, 上→下]）から滝の膜のメッシュを作る。want(k, j)＝その四角の表の向き
    static GameObject LipFallsMesh(Transform g, string meshName, string goName, Vector3[,] grid, System.Func<int, int, Vector3> want)
    {
        int nl = grid.GetLength(0), pc = grid.GetLength(1);
        var lens = new float[nl, pc]; var along = new float[nl];
        float maxLen = 0f; Vector3 prev = Vector3.zero;
        for (int k = 0; k < nl; k++)
        {
            var lipP = grid[k, 1];
            along[k] = k == 0 ? 0f : along[k - 1] + (lipP - prev).magnitude; prev = lipP;
            lens[k, pc - 1] = 0f;
            for (int j = pc - 2; j >= 0; j--) lens[k, j] = lens[k, j + 1] + (grid[k, j] - grid[k, j + 1]).magnitude;
            maxLen = Mathf.Max(maxLen, lens[k, 0]);
        }
        var FV = new List<Vector3>(); var FT = new List<int>(); var F1 = new List<Vector2>(); var F2 = new List<Vector2>(); var FC = new List<Color>();
        for (int k = 0; k < nl; k++)
            for (int j = 0; j < pc; j++)
            {
                FV.Add(grid[k, j]);
                // 下からの長さ：上（落ち口）を全体の長さにそろえる（落ち口の泡がどこも同じ太さに）
                F1.Add(new Vector2(along[k], maxLen - (lens[k, 0] - lens[k, j]))); F2.Add(new Vector2(along[nl - 1], maxLen)); FC.Add(Color.white);
            }
        for (int k = 0; k < nl - 1; k++)
            for (int j = 0; j < pc - 1; j++)
            {
                int a = k * pc + j, b = a + pc;
                var w = want(k, j);
                AddTriDir(FV, FT, a, b, a + 1, w); AddTriDir(FV, FT, a + 1, b, b + 1, w);
            }
        var fm = new Mesh { name = "Course2_1_" + meshName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        // uv0.x＝その列のいちばん下の「下からの長さ」（列ごとに長さがちがうので、下のはしの霧（_FrayLen）を列ごとの下はしから測る。2026-10-27 てじゃ「横長の滝、手前だけ先端が水平に切れてる」）
        var F0 = new List<Vector2>(); for (int k = 0; k < nl; k++) for (int j = 0; j < pc; j++) F0.Add(new Vector2(maxLen - lens[k, 0], 0f));
        fm.SetVertices(FV); fm.SetTriangles(FT, 0); fm.SetUVs(0, F0); fm.SetUVs(1, F1); fm.SetUVs(2, F2); fm.SetColors(FC);
        fm.RecalculateNormals(); fm.RecalculateBounds();
        fm = Save(fm, MeshDir + "/" + meshName + ".asset");
        var fgo = new GameObject(goName); fgo.transform.SetParent(g, false);
        fgo.AddComponent<MeshFilter>().sharedMesh = fm;
        var fr = fgo.AddComponent<MeshRenderer>(); fr.sharedMaterial = FuwaStage2WaterTest.WaterfallThickMat(true); fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return fgo;
    }

    // ---- ⑤ 滝の道（2026-10-21）----
    // 道（M4_FallsRoad）の下の土台、カーブの外側から道に食い込む滝のカーテン＋その下のえぐれた道に入り込む水路（流れて端で落ちる滝）×3、道の外に落ちた時のアウト判定。
    // 2026-10-21 てじゃ「道を削るように滝が入り込んでていいよ」「横向きに」「滝のカーテンで遮るイメージ」「とりあえず空中からおとしといていい」：
    // 滝の幕は道を横切る向きで、外側から CutDepth だけ食い込む。その下の道はえぐれて水路（水に落ちるので、道に叩きつけられてアウトにはならない）。
    // ふわふわは残りの道の幅を通り抜ける。当たったら水路に浮いて流されるので、滝より下流ですくい上げればセーフ。
    // 水路は道のすぐ横（道の側のふちはなし＝道の土台が壁。ふちに乗るとアウトなので、滝に押された所にふちを置かない）
    static float RoadW(float s) { return Mathf.Lerp(WalkHW * 2f, RoadHW * 2f, Smooth01((s - sF0) / 2f)); }
    // えぐれ（滝の中心からの距離 x）：上流は短く立ち上げ、下流は長くゆるく戻す（流されたふわふわが角に引っかからず外へすべるように）
    const float NotchUp = 0.6f, NotchDown = 0.9f, NotchEnd = 2.1f;
    static float NotchShape(float x)
    {
        if (x < -NotchUp - 0.25f || x > NotchEnd) return 0f;
        return Mathf.Min(Smooth01((x + NotchUp + 0.25f) / 0.25f), 1f - Smooth01((x - NotchDown) / (NotchEnd - NotchDown)));
    }
    // side の側のえぐれの深さ
    static float Cut(float s, float side)
    {
        float c = 0f;
        if (setS == null) return 0f;
        for (int i = 0; i < setS.Length; i++) if (setO[i] == side) c += CutDepth[i] * NotchShape(FallsFlowBack ? setS[i] - s : s - setS[i]);
        return c;
    }
    // カーブの外側（+1＝右）：中心線の向きが左へ回っていれば右が外
    static float OuterSide(float s)
    {
        Center(s - 2f, out _, out var d1); Center(s + 2f, out _, out var d2);
        return d1.x * d2.y - d1.y * d2.x > 0f ? 1f : -1f;
    }
    // i 番目の水路の中心線の範囲（c0 < c1）
    static void ChannelRange(int i, out float c0, out float c1)
    {
        float fb = FallsFlowBack ? -1f : 1f;
        c0 = setS[i] - fb * (NotchUp + 1.2f); c1 = setS[i] + fb * (NotchEnd + ChanAfter[i]);
        if (c0 > c1) { float tmp = c0; c0 = c1; c1 = tmp; }
    }
    static void FallsRoad(Transform root, Material ground, Material bedMat, FuwaWaterFx fx, AudioSource splash)
    {
        var g = new GameObject("FallsRoad").transform; g.SetParent(root, false);
        var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        Sweep(g, "StraightBase", sWalk1, sF0, s => -WalkHW, s => WalkHW, s => -8f, s => FallsH - Thick - 0.01f, ground, 0, true);   // 手前のまっすぐな道の土台
        Sweep(g, "RoadBase", sF0, sF1, s => -RoadW(s) * 0.5f + Cut(s, -1f), s => RoadW(s) * 0.5f - Cut(s, 1f), s => -8f, s => FallsH - Thick - 0.01f, ground, 0, true);
        float surf = FallsH - ChanDrop, outer = RoadHW + ChanHW * 2f;
        for (int i = 0; i < setS.Length; i++)
        {
            float sc = setS[i], o = setO[i];
            // 流れの上流（滝より少し手前）から下流（滝のあと ChanAfter）まで。流れが逆向きなら前後反転
            float fb = FallsFlowBack ? -1f : 1f;
            ChannelRange(i, out var c0, out var c1);
            var set = new GameObject("Set" + i).transform; set.SetParent(g, false);
            // 水路（道の側のふちなし＝道の土台のえぐれにそう、端は開いていて落ちる）
            Func<float, float> inner = s => RoadHW - Cut(s, o);
            var w = new Water { name = "Channel" + i, s0 = c0, s1 = c1, surf = surf, flow = ChanFlow, hw = s => (outer - inner(s)) * 0.5f, off = s => o * (outer + inner(s)) * 0.5f,
                open0 = FallsFlowBack, open1 = !FallsFlowBack, back = FallsFlowBack, bodyBottom = -8f, rimL = o > 0f ? false : true, rimR = o > 0f ? true : false,
                // 浅瀬：人は入って歩ける。滝に押されて底まで沈んだふわふわは、底でぽよんと戻す（前は深さ2mで人は落ちたらアウト）
                deep = false, depth = ChanDepth, bedBounce = ChanBedBounce };
            BuildWater(set, w, bedMat, ground, fx, splash);
            // 水路の端の滝（下は何もない → 落ちたらアウト）
            Center(FallsFlowBack ? c0 : c1, out var ep, out var ed); var er = Right(ed);
            var ec = ep + er * (o * (RoadHW + ChanHW));
            var eo = ed * fb;   // 落ちる向き＝流れの向き
            FuwaStage2WaterTest.Waterfall(set, "EndFalls", new Vector3(ec.x, 0, ec.y), surf + 0.02f, -10f, ChanHW * 2f + 0.3f, new Vector3(eo.x, 0, eo.y));
            EndLipFalls(set, "Chan" + i + "_EndFalls", ec, eo, er, ChanHW, surf, -10f);
            // 滝のカーテン：道を横切る向きで、外側から食い込む（幕の面は道の進む向きを向く）。押す厚み 0.9m がえぐれの中に収まるように少し手前に
            Center(sc - fb * 0.3f, out var fp, out var fd); var fr = Right(fd);
            // 幕の内側のはしは、えぐれのふちから OverFallsGap だけ水の上へ（ふちギリギリで当たると道に叩きつけられた。2026-10-23 てじゃ「余裕もたせよう」）
            float ia = RoadHW - CutDepth[i] + OverFallsGap, width = outer - ia;
            var fc = fp + fr * (o * (ia + width * 0.5f));
            FuwaStage2WaterTest.Waterfall(set, "OverFalls", new Vector3(fc.x, 0, fc.y), FallsH + OverFallsH, surf, width, new Vector3(fd.x, 0, fd.y));
        }
        // アウト判定：道の下（ふわふわ＝FuwaHazard、人＝FuwaPlayerCatcher）。4m ごと、道と水路より下
        var outs = new GameObject("Outs").transform; outs.SetParent(g, false);
        float o0 = sWalk1, o1 = sGoal0;   // 手前のまっすぐな道の下から
        int nSeg = Mathf.Max(1, Mathf.CeilToInt((o1 - o0) / 4f));
        for (int k = 0; k < nSeg; k++)
        {
            float a = Mathf.Lerp(o0, o1, k / (float)nSeg), b = Mathf.Lerp(o0, o1, (k + 1) / (float)nSeg), m = (a + b) * 0.5f;
            Center(m, out var cp, out var cd);
            var rot = Quaternion.LookRotation(new Vector3(cd.x, 0, cd.y));
            float segLen = b - a + 1f;
            var bo = new GameObject("BallOut" + k); bo.transform.SetParent(outs, false); bo.layer = 4;
            bo.transform.localPosition = new Vector3(cp.x, FallsH - 6.5f, cp.y); bo.transform.localRotation = rot;
            var bbc = bo.AddComponent<BoxCollider>(); bbc.isTrigger = true; bbc.size = new Vector3(26f, 2.4f, segLen);
            var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(bo); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); NoSync(hz);
            var po = new GameObject("PlayerOut" + k); po.transform.SetParent(outs, false);
            po.transform.localPosition = new Vector3(cp.x, FallsH - 8.5f, cp.y); po.transform.localRotation = rot;
            var pbc = po.AddComponent<BoxCollider>(); pbc.isTrigger = true; pbc.size = new Vector3(26f, 2.4f, segLen);
            var pcc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(po); pcc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pcc); NoSync(pcc);
        }
    }

    // ---- ⑤ 滝の道の横風（2026-10-25）----
    // てじゃ「三連滝が地味」→「風でもふかせる」「ふわふわでいいかな」「ふいたりやんだりさせよう」「吹き流しもセットで」。
    // 滝ごとに、道の上を滝の側へ押す横風（FuwaWindArea、吹いたりやんだり、滝ごとに周期をずらす）。
    // 見た目はステージ1のかぜのみちの横風（G3_CrossRight の地面の＞＞＞と風の粒）を写す。道の内側のふちに吹き流し（Windsock4 を写す）
    // 強さ：5 だと1回の強風で空中のふわふわが横に約5.3m流された（ClientSim）→3.5→2.6（てじゃ「風がややつよい」。1回で約2.8m の見込み）
    public static float FallsWindLen = 6f, FallsWindWidth = 7.5f, FallsWindStrength = 2.6f;
    // 吹き流しの浮島：道のふちから外へ（中心まで）、道の高さからの上下
    public static float SockIsleOut = 2.2f, SockIsleUp = -0.4f;
    public static float FallsWindOn = 1.6f, FallsWindOff = 2.4f;
    public static float[] FallsWindPhase = { 0f, 1.3f, 2.6f };
    static void FallsWind(Transform root)
    {
        var tmplZone = Object.FindObjectsOfType<FuwaWindArea>(true).FirstOrDefault(w => w.transform.parent != null && w.transform.parent.name == "G3_CrossRight");
        var tmplSock = Object.FindObjectsOfType<FuwaWindDeco>(true).FirstOrDefault(d => d.name == "Windsock4");
        var tmplIsle = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "TreeR" && t.parent != null && t.parent.name == "WebSupports" && t.Find("IslandUnder") != null && t.Find("IslandTop") != null);
        var stage2Grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_LobbyGround.mat");
        if (tmplZone == null) { Debug.LogWarning("[2–1] 横風のお手本（G3_CrossRight）がない"); return; }
        var g = new GameObject("FallsWind").transform; g.SetParent(root, false);
        const string decalDir = "Assets/FuwaCourse/Generated/WindDecal";
        Vector3 tScale = tmplZone.transform.lossyScale;
        for (int i = 0; i < setS.Length; i++)
        {
            float sc = setS[i], o = setO[i];
            Center(sc, out var p, out var d); var r = Right(d);
            var wind = new Vector3(r.x, 0, r.y) * o;               // 滝の側（外側）へ
            var along = new Vector3(d.x, 0, d.y);
            var set = new GameObject("Wind" + i).transform; set.SetParent(g, false);
            // 風の箱：ローカル z＝風の向き、x＝道にそう。お手本と同じ高さ 5m、底は道の 0.5m 下
            var zone = Object.Instantiate(tmplZone.gameObject, set, false); zone.name = "CrossWind";
            zone.transform.localPosition = new Vector3(p.x, FallsH - 0.5f + tScale.y * 0.5f, p.y);
            zone.transform.localRotation = Quaternion.LookRotation(wind);
            zone.transform.localScale = new Vector3(FallsWindLen, tScale.y, FallsWindWidth);
            var wa = zone.GetComponent<FuwaWindArea>();
            var so = new SerializedObject(wa);
            so.FindProperty("localDirection").vector3Value = Vector3.forward;
            so.FindProperty("strength").floatValue = FallsWindStrength;
            so.FindProperty("onSeconds").floatValue = FallsWindOn;
            so.FindProperty("offSeconds").floatValue = FallsWindOff;
            so.FindProperty("phaseOffset").floatValue = FallsWindPhase[i % FallsWindPhase.Length];
            so.FindProperty("baseLevel").floatValue = 0f;
            so.FindProperty("warnSeconds").floatValue = 0.6f;
            // 吹き始めはすっと強く、終わりぎわに弱まる（お手本の G3_CrossRight はずっと弱い風なので、強さのカーブは平らな 0 だった）
            so.FindProperty("gustCurve").animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
            // 地面の＞＞＞：子の WindDecal の材質を滝ごとに（向きは風のエリアのローカルなので同じでいいが、作り直しで共有を避ける）
            var decal = zone.transform.Find("WindDecal");
            if (decal != null)
            {
                var src = decal.GetComponent<Renderer>().sharedMaterial;
                string mp = decalDir + "/WindDecal_Course2_1_FallsWind" + i + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, mp); }
                m.CopyPropertiesFromMaterial(src); m.SetVector("_WindDir", new Vector4(0, 0, 1, 0)); EditorUtility.SetDirty(m);
                decal.GetComponent<Renderer>().sharedMaterial = m;
                so.FindProperty("groundHighlight").objectReferenceValue = decal.GetComponent<Renderer>();
            }
            // 風の粒：お手本の粒を写して、箱の大きさに合わせる
            if (tmplZone.visual != null)
            {
                var vis = Object.Instantiate(tmplZone.visual, set, false); vis.name = "WindParticles";
                vis.transform.position = zone.transform.position; vis.transform.rotation = zone.transform.rotation;
                var ps = vis.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    float kx = FallsWindLen / tScale.x, kz = FallsWindWidth / tScale.z;
                    var sh = ps.shape; var ss = sh.scale; sh.scale = new Vector3(ss.x * kx, ss.y, ss.z * kz);
                    var em = ps.emission; em.rateOverTimeMultiplier *= kx * kz;
                }
                so.FindProperty("visual").objectReferenceValue = vis;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(wa); NoSync(wa);
            // 吹き流し：道の内側（滝と反対）の外に浮かべた小さい浮島の上（道のふちに立てたら「邪魔」だった。2026-10-25 てじゃ「浮島つくってそこに浮かせよう」）。
            // 浮島はクモの巣の木の小島（WebSupports/TreeR）の上の円盤と下の▼を写す。上の面はステージ2の芝の色
            if (tmplSock != null)
            {
                Center(sc, out var sp, out var sd); var sr = Right(sd);
                var q = sp + sr * (-o * (RoadHW + SockIsleOut));
                var isle = new GameObject("SockIsland").transform; isle.SetParent(set, false);
                isle.localPosition = new Vector3(q.x, FallsH + SockIsleUp, q.y);
                float topY = 0f;
                if (tmplIsle != null)
                {
                    // 上の円盤の中心を浮島の原点に（お手本の木の原点は円盤からずれている）
                    var off = tmplIsle.Find("IslandTop").localPosition;
                    foreach (var part in new[] { "IslandTop", "IslandUnder" })
                    {
                        var srcT = tmplIsle.Find(part); if (srcT == null) continue;
                        var c = Object.Instantiate(srcT.gameObject, isle, false); c.name = part;
                        c.transform.localPosition = srcT.localPosition - off; c.transform.localRotation = srcT.localRotation; c.transform.localScale = srcT.localScale;
                        GameObjectUtility.SetStaticEditorFlags(c, 0);
                        if (part == "IslandTop")
                        {
                            var ir = c.GetComponent<Renderer>(); if (ir && stage2Grass) ir.sharedMaterial = stage2Grass;
                            topY = ir ? ir.localBounds.max.y * srcT.localScale.y : 0f;
                        }
                    }
                }
                var sock = Object.Instantiate(tmplSock.gameObject, isle, false); sock.name = "Windsock";
                sock.transform.localPosition = new Vector3(0f, topY, 0f);
                var wd = sock.GetComponent<FuwaWindDeco>(); wd.wind = wa;
                FuwaWindDecoBuilder.AlignSockArm(wd);
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(wd); NoSync(wd);
            }
        }
    }

    // ---- ⑥ ラスト（2026-10-22 てじゃ「3連滝のあとに、強制で滝にぶつかるゾーン作る→そのまま流されてく、川はゴール横を直進、
    //      途中で打ち上げてゴールへ　プレイヤーは川岸を走って追いかける」。前の試作＝段々の池＋泡の柱＋水車は「川を登らせるのは変」でやめた）----
    // 踊り場の先は道の幅いっぱいの滝のカーテン（よけられない。左は岩の壁で、人の坂の所はツタのくぐり口、右は崖）。
    // ふわふわは押し落とされて下の川へ。川はまっすぐ流れて、右岸の一段高いゴールの広場の横を通り、終わりは滝で落ちてアウト。
    // 人は左の坂を下りて左岸の道（M7_LeftBank）を走り、流れてくるふわふわを右の広場へ撃ち上げる
    static void Finale(Transform root, Transform terr, Material ground, Material bedMat, FuwaWaterFx fx, AudioSource splash)
    {
        var g = new GameObject("Finale").transform; g.SetParent(root, false);
        float H = FallsH, surf = H + FinRiverSurf;
        float r0 = sF1 + FinRiver0, r1 = sFin1;
        // 川（浅い：人が落ちても歩ける。ふちはなし＝両岸の地面がふち。終わりは開いていて滝で落ちる）
        BuildWater(g, new Water { name = "W6_FinalRiver", s0 = r0, s1 = r1, surf = surf, flow = FinRiverFlow, hw = s => FinRiverHW,
            rimL = false, rimR = false, open1 = true, bodyBottom = -6f }, bedMat, ground, fx, splash);
        Center(r1, out var ep, out var ed);
        FuwaStage2WaterTest.Waterfall(g, "EndFalls", new Vector3(ep.x, 0, ep.y), surf + 0.02f, surf - 10f, FinRiverHW * 2f + 0.3f, new Vector3(ed.x, 0, ed.y));
        // 見た目はふちからこぼれる膜に（2026-10-26 てじゃ「木の板いらんよね」→ 前の箱の滝だと、落ち口と滝のすき間から川の底の切り口が板に見えた）。押す範囲・霧・音は残す
        EndLipFalls(g, "FinalEndFalls", ep, ed, Right(ed), FinRiverHW, surf, surf - 10f);
        // 右岸：川ぞいの細い地面（ふわふわが乗るとアウト）
        RockSweep(g, "RightBank", r0 + FinCliffLen, r1, s => FinRiverHW, s => FinRiverHW + FinBankW, s => surf - WaterDepth - 1.5f, s => surf + BankTop, 11, true);
        // 左岸の道の下の土台（道と川のあいだのすき間を埋める）。カーブの始まりまで。その先の道は川から離れていく桟橋
        Sweep(g, "LeftBankBase", sF1 + FinLand, sBend0, s => -FinRiverHW - FinWalkW, s => -FinRiverHW, s => surf - WaterDepth - 1.5f, s => LeftBankH(s) - Thick - 0.01f, ground, 0, true);
        // カーブの外側の岸：右岸と同じ細い地面
        // 道がまだ岸の上にある所は道の内側のへりで切る（同じ高さで重なるとZファイト）
        float WalkInnerLat(float s)
        {
            Center(s, out var p, out var d); var n = Right(goalD); var q0 = walkP0 + n * (FinWalkW * 0.5f);
            float den = Vector2.Dot(Right(d), n); return Mathf.Abs(den) < 1e-4f ? -99f : Vector2.Dot(q0 - p, n) / den;
        }
        RockSweep(g, "LeftBank", sBend0, r1, s => -FinRiverHW, s => Mathf.Min(Mathf.Max(-FinRiverHW - FinBankW, WalkInnerLat(s)), -FinRiverHW - 0.001f), s => surf - WaterDepth - 1.5f, s => surf + BankTop, 11, true);
        // 滝のカーテン：道の幅いっぱい（踊り場の先、川の始まりの上）。落ち口は高く、川の水面まで
        Center(sF1 + FinCurtain, out var cp, out var cd); var cr = Right(cd);
        FuwaStage2WaterTest.Waterfall(g, "ForcedFalls", new Vector3(cp.x, 0, cp.y), H + FinCurtainTop, surf, FinRiverHW * 2f + 0.6f, new Vector3(cd.x, 0, cd.y));
        // 回り込み防止：左は岩の壁（滝の落ち口より高い）。人の坂の所だけくぐり口をあけて、ツタのカーテン（人はすり抜け、ふわふわは跳ね返る）、右は崖
        // （2026-10-22 てじゃ「滝の左をふわふわを防がないとな」。前は見えないアウトだけだった）
        float wa = sF1 + FinCurtain - 0.2f, wb = sF1 + FinCurtain + 0.6f, wm = (wa + wb) * 0.5f;
        float gIn = -(FinRiverHW + 0.25f), gOut = -(FinRiverHW + FinWalkW + 0.4f), gTop = LeftBankH(wm) + FinGateH;
        // 2026-10-26 てじゃ「とりあえずA」：前の箱の壁・崖（FinalWallL・FinalWallL_Lintel・FinalCliffR）を、上が一面の水の岩に（FinaleGate）
        FinaleGate(g, H, surf, sF1 + FinCurtain, wa, wb, gIn, gOut, gTop, r0);
        var oldFalls = g.Find("ForcedFalls/Curtain"); if (oldFalls && oldFalls.GetComponent<Renderer>()) oldFalls.GetComponent<Renderer>().enabled = false;   // 見た目は FinaleGate の膜（押す範囲・霧・音は残す）
        Center(wm, out var vp, out var vd); var vq = vp + Right(vd) * ((gIn + gOut) * 0.5f);
        VineCurtain(g, "GateVines", new Vector3(vq.x, gTop, vq.y), new Vector3(Right(vd).x, 0, Right(vd).y), gIn - gOut, FinGateH, 2106);
        // 壁のさらに左（空中）を回り込むふわふわはアウト（ふつうは届かない保険）
        var by = new GameObject("BypassOut"); by.transform.SetParent(g, false); by.layer = 4;
        var bl = cp + cr * (-FinWallOut - 2.5f);
        by.transform.localPosition = new Vector3(bl.x, (surf + H + FinCurtainTop) * 0.5f, bl.y); by.transform.localRotation = Quaternion.LookRotation(new Vector3(cd.x, 0, cd.y));
        var bbc = by.AddComponent<BoxCollider>(); bbc.isTrigger = true; bbc.size = new Vector3(5f, H + FinCurtainTop - surf, 0.8f);
        var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(by); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); NoSync(hz);
        // アウト判定：川より下（ふわふわ＝FuwaHazard、人＝FuwaPlayerCatcher）。滝の道のアウトの続き（広場の始まり〜川の終わりの先）
        var outs = new GameObject("Outs").transform; outs.SetParent(g, false);
        var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        float o0 = sGoal0, o1 = r1 + 8f;
        int nSeg = Mathf.Max(1, Mathf.CeilToInt((o1 - o0) / 4f));
        for (int k = 0; k < nSeg; k++)
        {
            float a = Mathf.Lerp(o0, o1, k / (float)nSeg), b = Mathf.Lerp(o0, o1, (k + 1) / (float)nSeg), m = (a + b) * 0.5f;
            Center(m, out var op, out var od);
            var rot = Quaternion.LookRotation(new Vector3(od.x, 0, od.y));
            var bo = new GameObject("BallOut" + k); bo.transform.SetParent(outs, false); bo.layer = 4;
            bo.transform.localPosition = new Vector3(op.x, surf - 5f, op.y); bo.transform.localRotation = rot;
            var c1 = bo.AddComponent<BoxCollider>(); c1.isTrigger = true; c1.size = new Vector3(30f, 2.4f, b - a + 1f);
            var h1 = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(bo); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(h1); NoSync(h1);
            var po = new GameObject("PlayerOut" + k); po.transform.SetParent(outs, false);
            po.transform.localPosition = new Vector3(op.x, surf - 7f, op.y); po.transform.localRotation = rot;
            var c2 = po.AddComponent<BoxCollider>(); c2.isTrigger = true; c2.size = new Vector3(30f, 2.4f, b - a + 1f);
            var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(po); pc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); NoSync(pc);
        }
    }

    // ---- ラストの滝の門（2026-10-26 てじゃ「とりあえずA」）----
    // 左の壁（人のくぐり口つき）と右の崖をごつごつの岩に。上はどちらも水をためたくぼみで、滝の所だけ岩と岩のあいだに水がかかって（Slab）、
    // その前のふちから道の幅いっぱいに滝がこぼれる。水がかかっている所の後ろ・下も水の膜でつつむ。水の上に青い球を2つ。
    // 岩は前の箱の当たり判定をつつむ形（内側＝川・くぐり口の面はまっすぐ、外の面だけでこぼこ）で、当たり判定も岩のメッシュ
    public static float GateRim = 0.2f, GateFloor = 0.25f, GateSlabLen = 1.4f, GateEndRound = 2.5f;
    public static float GateLeftOut = 9.8f, GateRightOut = 7.5f, GateLeftExtra = 1.3f;
    static void FinaleGate(Transform g, float H, float surf, float cs, float wa, float wb, float gIn, float gOut, float gTop, float r0)
    {
        var gate = new GameObject("Gate").transform; gate.SetParent(g, false);
        float yTop = H + FinCurtainTop, sl0 = cs - GateSlabLen, sl1 = cs;
        float Floor(float s, float s0, float s1) => Mathf.Lerp(yTop + GateRim, yTop - GateFloor, Smooth01(Mathf.Min(s - s0, s1 - s) / 0.8f));
        // 2026-10-27 てじゃ「アーチの上がガタガタ」「抜けた先の右の崖が真っ直ぐきれてて違和感、ツタの裏側もカクッと」→
        // ・水がかかる所（Slab）の内側のふちの高さを段でなく なめらかに（橋のふちの上がり方とそろえる）
        // ・ふちの上・内の面にでこぼこ（まっすぐな線をなくす）
        // ・下流のはしは上から見て半円に丸める（内側も外側も。前は外側だけ細くなって、内側の面がまっすぐ切れていた）
        // ・くぐり口の天井の角を丸く、口のはしは天井が上がって開く
        float SlabW(float s) => s < sl0 ? Smooth01((s - sl0 + 0.3f) / 0.7f) : s <= sl1 ? 1f : 1f - Smooth01((s - sl1) / 0.5f);
        float RimN(float s, float k) => Mathf.PerlinNoise(17.3f + s * 0.55f, k) * 0.3f;
        float Calm(float s) => s < sl0 ? Smooth01((sl0 - 0.3f - s) / 1f) : s > sl1 ? Smooth01((s - sl1 - 0.5f) / 1f) : 0f;   // 橋のふちとつながる所はでこぼこなし
        float Bump(float s) => Smooth01((s - sl1 - 0.8f) / 0.8f);   // 内の面のでこぼこ：滝のはしと接する所は平らのまま
        float RfU(float s, float a) => Mathf.Max(0.12f, Quarter(Mathf.Clamp01((s - a) / GateEndRound)));
        float RfD(float s, float b, float rad) => Mathf.Max(0.05f, Quarter(Mathf.Clamp01((b - s) / rad)));
        // 左：内側（川の側）gIn、くぐり口の壁 gOut、外のはし -GateLeftOut。断面は 上のくぼみ → 外の面 → 下のとがり → くぐり口の壁 → くぐり口の天井 → 内の面
        // くぐり口より外の岩（gOut〜外のはし）のまん中 cS・半幅 hS で、下流のはしを半円に
        float cS = (gOut - GateLeftOut) * 0.5f, hS = (gOut + GateLeftOut) * 0.5f;
        float l0 = wa - GateLeftExtra, l1 = sl1 + 0.7f + hS;
        float WallL(float s) => cS + hS * RfD(s, l1, hS);
        float OutL(float s) => Mathf.Max(cS - hS * RfD(s, l1, hS), gOut + (-GateLeftOut - gOut) * RfU(s, l0));
        float LintelL(float s) { float w = WallL(s); return w + (gIn - w) * RfD(s, l1, hS); }
        float CeilL(float s) => gTop + 0.6f * (1f - RfD(s, l1, hS));
        float RidgeL(float s) => Mathf.Min(0.3f, (LintelL(s) - OutL(s)) * 0.25f);
        // 右：内側（川の側）FinRiverHW、外のはし GateRightOut。下のはしは川の底より下。下流のはしは右岸の地面の上で半円に
        float rin = FinRiverHW, cR = (rin + GateRightOut) * 0.5f, hR = (GateRightOut - rin) * 0.5f;
        float r0s = sF1 + 0.2f, r1s = r0 + FinCliffLen + hR;
        float InR(float s) => cR - hR * RfD(s, r1s, hR);
        float OutR(float s) => Mathf.Min(cR + hR * RfD(s, r1s, hR), rin + (GateRightOut - rin) * RfU(s, r0s));
        float RidgeR(float s) => Mathf.Min(0.3f, (OutR(s) - InR(s)) * 0.25f);
        // くぼみの水（ふちの内側）。滝の所は内側のふちまで（水がかかっている所とつながる）。はしの細い所は水なし
        // （はしの丸い所まで水を張ると、まっすぐな水のはしが丸いふちから はみ出して見えた）
        // 下流の半円のはしは、幅がほぼ全部ある所までで水を止める（半円はすぐ幅が戻るので、幅2.2mで止めると先っぽまで水が来た）
        float WaterEnd(float s1, float dir, Func<float, float> width, float need) { float s = s1; while (Mathf.Abs(s - s1) < 6f && width(s) < need) s -= 0.05f * dir; return dir < 0f ? Mathf.Max(s, s1 + 0.4f) : Mathf.Min(s, s1 - 0.4f); }
        float lw0 = WaterEnd(l0, -1f, s => LintelL(s) - OutL(s), 2.2f), lw1 = WaterEnd(l1, 1f, s => LintelL(s) - OutL(s), 0.85f * (gIn + GateLeftOut));
        float rw0 = WaterEnd(r0s, -1f, s => OutR(s) - InR(s), 2.2f), rw1 = WaterEnd(r1s, 1f, s => OutR(s) - InR(s), 0.85f * (GateRightOut - rin));
        // くぼみの底は水のはしの外では 両側のふちの高さの間まで上げる（丸めたはしの先で、水のない底がふちの切れ込みに見えた）
        float Floor2(float s, float w0, float w1, float rimIn, float rimOut) => Mathf.Lerp((rimIn + rimOut) * 0.5f, yTop - GateFloor, Smooth01(Mathf.Min(s - w0, w1 - s) / 0.8f));
        CragRock(gate, "GateRockL", l0, l1, 311, (s, rf) =>
        {
            float li = LintelL(s), wi = WallL(s), ol = OutL(s), ce = CeilL(s), sw = SlabW(s), rr = RidgeL(s), bp = Bump(s);
            float it = Mathf.Lerp(yTop + GateRim + RimN(s, 0.4f) * Calm(s), yTop - 0.03f, sw), ot = yTop + GateRim + RimN(s, 3.1f);
            float fl = Floor2(s, lw0 - 0.1f, lw1 + 0.1f, Mathf.Max(it, yTop + GateRim), ot);
            float fy = LeftBankH(Mathf.Clamp(s, sF1, sF1 + FinDownEnd)) - 0.6f;
            float fr = Mathf.Min(0.45f, (li - wi) * 0.35f), bv = Mathf.Min(0.3f, (li - wi) * 0.25f);
            var pr = new List<(float, float, bool)>
            {
                (li, it, false), (li - rr, fl, false), (ol + rr, fl, false), (ol, ot, false),
                // 下にいくほど細く（浮島みたいに。まっすぐ下までだと柱に見えた）
                (ol + 0.15f, yTop - 1.5f, true), (wi + (ol - wi) * 0.85f, H - 1.5f, true), (wi + (ol - wi) * 0.55f, surf - 1f, true), (wi + (ol - wi) * 0.25f, surf - 4.5f, true),
                (wi, fy, false), (wi - RimN(s, 5.7f), Mathf.Lerp(fy + 0.6f, ce, 0.55f), false),
            };
            // くぐり口の壁→天井の角は丸く（へこんだ角なので口の中に少し出るだけ）
            for (int a = 0; a <= 4; a++) { float th = Mathf.PI * (1f - a / 8f); pr.Add((wi + fr + fr * Mathf.Cos(th), ce - fr + fr * Mathf.Sin(th), false)); }
            pr.Add(((wi + fr + li - bv) * 0.5f, ce + RimN(s, 7.9f) * 0.4f, false));
            // 天井→内の面の出っぱった角は削って丸く
            pr.Add((li - bv, ce, false)); pr.Add((li - bv * 0.3f, ce + bv * 0.3f, false)); pr.Add((li, ce + bv, false));
            pr.Add((li - bp * RimN(s, 9.2f) * 1.3f, Mathf.Lerp(ce + bv, it, 0.5f), false));
            return pr;
        }, -1f, false, 0.2f);
        CragRock(gate, "GateRockR", r0s, r1s, 517, (s, rf) =>
        {
            float ir = InR(s), ro = OutR(s), sw = SlabW(s), rr = RidgeR(s), bp = Bump(s);
            float it = Mathf.Lerp(yTop + GateRim + RimN(s, 1.3f) * Calm(s), yTop - 0.03f, sw), ot = yTop + GateRim + RimN(s, 4.4f);
            float fl = Floor2(s, rw0 - 0.1f, rw1 + 0.1f, Mathf.Max(it, yTop + GateRim), ot);
            return new List<(float, float, bool)>
            {
                (ir, it, false), (ir + rr, fl, false), (ro - rr, fl, false), (ro, ot, false),
                (ro - 0.15f, yTop - 1.5f, true), (ir + (ro - ir) * 0.85f, H - 1.5f, true), (ir + (ro - ir) * 0.55f, surf - 1f, true), (ir + (ro - ir) * 0.3f, surf - 4.5f, true),
                (ir, surf - 2.5f, false), (ir, surf + 0.3f, false),
                // 川に面した内の面のでこぼこ（水ぎわは川のはしのまま、岩の中へ引っこむだけ）
                (ir + bp * RimN(s, 6.6f) * 1.6f, Mathf.Lerp(surf + 0.3f, it, 0.35f), false), (ir + bp * RimN(s, 8.1f) * 1.6f, Mathf.Lerp(surf + 0.3f, it, 0.72f), false),
            };
        }, 1f, false, 0.2f);
        BasinWater(gate, "GateWaterL", lw0, lw1, s => Mathf.Lerp(LintelL(s) - RidgeL(s) * 0.2f, gIn, SlabW(s)), s => OutL(s) + RidgeL(s) * 0.2f, yTop, cs);
        BasinWater(gate, "GateWaterR", rw0, rw1, s => Mathf.Lerp(InR(s) + RidgeR(s) * 0.2f, rin, SlabW(s)), s => OutR(s) - RidgeR(s) * 0.2f, yTop, cs);
        BasinWater(gate, "GateWaterSlab", sl0, sl1, s => gIn, s => rin, yTop, 99f);
        // 前のふちから滝（川の水面まで）と、水がかかっている所の後ろ・下をつつむ膜
        var lipF = new List<Vector3>(); var outF = new List<Vector3>();
        float la = gIn, lb = rin;   // 両わきの岩の面まで（はみ出すと岩の上に滝のはしが見えた）
        int nLip = Mathf.CeilToInt((lb - la) / 0.3f);
        for (int k = 0; k <= nLip; k++)
        {
            float lat = Mathf.Lerp(la, lb, k / (float)nLip);
            Center(sl1, out var p1, out var d1); var q1 = p1 + Right(d1) * lat; lipF.Add(new Vector3(q1.x, yTop, q1.y)); outF.Add(new Vector3(d1.x, 0, d1.y));
        }
        LipFalls(gate, "GateFalls", "GateFalls", lipF, outF, new List<Vector2> { new Vector2(0.3f, 0.012f), new Vector2(0.02f, 0.012f), new Vector2(-0.18f, -0.15f), new Vector2(-0.3f, -0.6f), new Vector2(-0.34f, -1.5f), new Vector2(-0.35f, float.NaN) }, surf);
        // 滝の裏の面（上流の踊り場から見える側）：岩の橋の中から川まで
        LipFalls(gate, "GateFallsBack", "GateFallsBack", lipF, outF, new List<Vector2> { new Vector2(0.05f, -1.3f), new Vector2(0.03f, -2f), new Vector2(0.02f, float.NaN) }, surf, true);
        // 真ん中の岩の橋（2026-10-26 てじゃ「最後の滝、だいぶ無理あるかたち」→ 前は水だけが空中にかかって、後ろ・下を水の膜で巻いていた）。
        // 上は水の底（後ろのはしはせき止めるふち）、下はゆるいアーチ。左右のはしは両わきの岩にうめる
        CragRock(gate, "GateBridge", sl0 - 0.3f, sl1, 733, (s, rf) =>
        {
            float top = Mathf.Lerp(yTop + GateRim, yTop - GateFloor, Smooth01((s - sl0 + 0.3f) / 0.7f)), mid = (gIn + rin) * 0.5f;
            return new List<(float, float, bool)>
            {
                (gIn - 0.3f, top, false), (rin + 0.3f, top, false), (rin + 0.3f, yTop - 2.6f, false), (rin - 0.7f, yTop - 2.0f, true),
                (mid, yTop - 1.5f, true), (gIn + 0.7f, yTop - 2.0f, true), (gIn - 0.3f, yTop - 2.6f, false),
            };
        }, 0f, false);
        // 青い球：左右のくぼみの上に1つずつ（球の下は湧き水）
        foreach (var (s, lat) in new[] { (cs + 0.2f, (gOut - GateLeftOut) * 0.5f), (cs + 2.6f, (rin + GateRightOut) * 0.5f) })
        {
            Center(s, out var p, out var d); var q = p + Right(d) * lat; var pos = new Vector3(q.x, yTop, q.y);
            var sp = new GameObject("GateOrb" + (lat < 0 ? "L" : "R")).transform; sp.SetParent(gate, false);
            Spring(sp, pos);
            WaterOrb(sp, pos, "WaterOrb", WallOrbHeight, 0.36f, 0.24f);
        }
        // 見た目と当たり判定は Blender でひとつにした岩（Stage2_GateRock.fbx。FLUFF_models.blend の Stage2Gate シーン：Src_* をつないで Cut_* でくりぬく）。
        // 上の Gate* の岩は型の元として作るだけで、見た目・当たり判定は消す
        var gateModel = AssetDatabase.LoadAllAssetsAtPath(GateRockModel).OfType<Mesh>().FirstOrDefault();
        if (gateModel)
        {
            foreach (var nm in new[] { "GateRockL", "GateRockR", "GateBridge" })
            {
                var t = gate.Find(nm); if (!t) continue;
                if (t.GetComponent<Renderer>()) t.GetComponent<Renderer>().enabled = false;
                if (t.GetComponent<Collider>()) t.GetComponent<Collider>().enabled = false;
            }
            var gr = new GameObject("GateRock"); gr.transform.SetParent(gate, false);
            gr.AddComponent<MeshFilter>().sharedMesh = gateModel; gr.AddComponent<MeshRenderer>().sharedMaterial = RockBaseMat();
            gr.AddComponent<MeshCollider>().sharedMesh = gateModel;
        }
        if (GateExportPath != null) ExportGateShapes(gate, GateExportPath, H, surf, yTop, sl0, sl1, gIn, gOut, rin, l0, l1, r0s, r1s, lw0, lw1, rw0, rw1,
            WallL, CeilL, s => Mathf.Lerp(LintelL(s) - RidgeL(s) * 0.2f, gIn, SlabW(s)), s => OutL(s) + RidgeL(s) * 0.2f,
            s => Mathf.Lerp(InR(s) + RidgeR(s) * 0.2f, rin, SlabW(s)), s => OutR(s) - RidgeR(s) * 0.2f);
    }

    // ---- ラストの滝の門を Blender でひとつの岩にするための型（2026-10-27 てじゃ「アーチで繫いでる岩がとってつけたような感じ」→ A：Blenderで作り直す）----
    // 元の形（左右の岩・橋の土台）と、くりぬく型（Cut_*：岩のすき間＋アーチの下・くぐり口・上の水のくぼみ）をコースのローカル座標で JSON に書き出す。
    // くりぬく型は今の当たり判定より外へしか広げない（でこぼこは岩の中へ引っこむ向きだけ）
    public static string GateExportPath;
    const string GateRockModel = "Assets/FuwaStage2/Models/Stage2_GateRock.fbx";
    public static float GateArchTop = 1.5f, GateArchSpring = 3.3f;   // アーチの下：まん中は yTop-1.5、岩の面で yTop-3.3（面に沿って立ち上がる）
    static void ExportGateShapes(Transform gate, string path, float H, float surf, float yTop, float sl0, float sl1, float gIn, float gOut, float rin,
        float l0, float l1, float r0s, float r1s, float lw0, float lw1, float rw0, float rw1,
        Func<float, float> wallL, Func<float, float> ceilL, Func<float, float> lInA, Func<float, float> lInB, Func<float, float> rInA, Func<float, float> rInB)
    {
        var sb = new System.Text.StringBuilder(); sb.Append("{\"objects\":[");
        bool first = true;
        void Emit(string name, List<Vector3> V, List<int> T)
        {
            if (!first) sb.Append(','); first = false;
            sb.Append("{\"name\":\"").Append(name).Append("\",\"v\":[");
            for (int i = 0; i < V.Count; i++) { if (i > 0) sb.Append(','); sb.Append(V[i].x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(V[i].y.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(V[i].z.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)); }
            sb.Append("],\"t\":[").Append(string.Join(",", T)).Append("]}");
        }
        // 中心線にそって断面（横, 高さ）をつなぐ閉じた形（断面は反時計回りでも時計回りでもよい：向きは Blender 側で直す）
        void Loft(string name, List<float> ss, Func<float, List<Vector2>> prof)
        {
            var V = new List<Vector3>(); var T = new List<int>(); int cols = 0;
            foreach (var s in ss)
            {
                Center(s, out var p, out var d); var r = Right(d); var pr = prof(s); cols = pr.Count;
                foreach (var q in pr) { var w = p + r * q.x; V.Add(new Vector3(w.x, q.y, w.y)); }
            }
            int n = ss.Count;
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < cols; k++)
                {
                    int k2 = (k + 1) % cols, a = i * cols + k, b = a + cols, a2 = i * cols + k2, b2 = a2 + cols;
                    T.AddRange(new[] { a, a2, b, a2, b2, b });
                }
            foreach (int i in new[] { 0, n - 1 })
            {
                var c = Vector3.zero; for (int k = 0; k < cols; k++) c += V[i * cols + k]; int ci = V.Count; V.Add(c / cols);
                for (int k = 0; k < cols; k++) T.AddRange(i == 0 ? new[] { ci, i * cols + (k + 1) % cols, i * cols + k } : new[] { ci, i * cols + k, i * cols + (k + 1) % cols });
            }
            Emit(name, V, T);
        }
        List<float> Steps(float a, float b, float step, params float[] extra)
        {
            var l = new List<float>(); for (float s = a; s < b - 1e-3f; s += step) l.Add(s); l.Add(b);
            foreach (var e in extra) if (e > a && e < b) { l.Add(e - 0.01f); l.Add(e); }
            return l.Distinct().OrderBy(x => x).ToList();
        }
        float N(float s, float k) => Mathf.PerlinNoise(31.7f + s * 0.5f, k);
        // 元の形：左右の岩はいまのメッシュそのまま
        foreach (var nm in new[] { "GateRockL", "GateRockR" })
        {
            var m = gate.Find(nm).GetComponent<MeshFilter>().sharedMesh; Emit("Src_" + nm, m.vertices.ToList(), m.triangles.ToList());
        }
        // 橋の土台：箱（上はせき止めるふちの高さ、下はアーチの根元より下）。アーチは Cut_Gap でくりぬく
        float bs0 = sl0 - 0.3f;
        Loft("Src_Bridge", Steps(bs0, sl1, 0.2f), s => new List<Vector2> { new Vector2(gIn - 0.4f, yTop + GateRim), new Vector2(rin + 0.4f, yTop + GateRim), new Vector2(rin + 0.4f, yTop - GateArchSpring - 1f), new Vector2(gIn - 0.4f, yTop - GateArchSpring - 1f) });
        // 岩のすき間（川・滝・踊り場の右の岩の面まで）。橋の所はアーチの下だけ。でこぼこは下流の川ぞいだけ（滝のはし・踊り場のふちは平らのまま）
        float g0 = r0s - 2f, g1 = Mathf.Max(l1, r1s) + 3f, bA = bs0 - 0.4f, bB = sl1 - 0.02f;
        Loft("Cut_Gap", Steps(g0, g1, 0.2f, bA, bB), s =>
        {
            // 上流（踊り場の横・アーチの下）もでこぼこ。平らのままは滝のはしが接する所（落ち口の前後）だけ。
            // でこぼこは いちばん下の段（踊り場+1.3m くらい）より上で、上のふち（水のくぼみのへり）の手前まで
            bool br = s >= bA && s <= bB; float nz = Mathf.Max(Smooth01((s - sl1 - 0.8f) / 0.8f), Smooth01((sl1 - 0.5f - s) / 0.6f));
            float top = br ? yTop - GateArchSpring : yTop + 6f, fTop = br ? top - 0.4f : yTop - 1f, fEnd = br ? top - 0.15f : yTop - 0.2f;
            float fLow = s > sl1 ? surf + 0.8f : H + 1.2f;   // 下流は川の水面の上から、上流は踊り場の上1.2mから
            var pr = new List<Vector2> { new Vector2(gIn, surf - 12f), new Vector2(rin, surf - 12f), new Vector2(rin, surf + 0.3f), new Vector2(rin, fLow) };
            for (int k = 1; k <= 3; k++) pr.Add(new Vector2(rin + nz * N(s, k) * 0.5f, Mathf.Lerp(fLow, fTop, k / 4f)));
            pr.Add(new Vector2(rin, fEnd)); pr.Add(new Vector2(rin, top));
            // 上の辺：橋の所はアーチ、ほかは空の高さ（点の数はどの断面も同じにする）
            float mid = (gIn + rin) * 0.5f, hw = (rin - gIn) * 0.5f;
            for (int k = 1; k < 12; k++)
            {
                float u = Mathf.Cos(Mathf.PI * k / 12f);   // 1 → -1（右 → 左）
                pr.Add(br ? new Vector2(mid + hw * u, yTop - GateArchTop - (GateArchSpring - GateArchTop) * (1f - Mathf.Sqrt(1f - u * u)))
                          : new Vector2(mid + hw * u, yTop + 6f));
            }
            pr.Add(new Vector2(gIn, top)); pr.Add(new Vector2(gIn, fEnd));
            for (int k = 3; k >= 1; k--) pr.Add(new Vector2(gIn - nz * N(s, k + 7f) * 0.5f, Mathf.Lerp(fLow, fTop, k / 4f)));
            pr.Add(new Vector2(gIn, fLow)); pr.Add(new Vector2(gIn, surf + 0.3f));
            return pr;
        });
        // くぐり口（人の道）：壁→天井の角は丸く。壁・天井は岩の中へ少しでこぼこ
        Loft("Cut_Walk", Steps(l0 - 3f, l1 + 3f, 0.2f), s =>
        {
            float wi = wallL(s), ce = ceilL(s), fy = LeftBankH(Mathf.Clamp(s, sF1, sF1 + FinDownEnd)) - 1.6f, fr = 0.45f;
            var pr = new List<Vector2> { new Vector2(gIn + 0.5f, fy), new Vector2(gIn + 0.5f, ce + 0.3f), new Vector2((wi + gIn) * 0.5f, ce + N(s, 3f) * 0.15f) };
            for (int a = 4; a >= 0; a--) { float th = Mathf.PI * (1f - a / 8f); pr.Add(new Vector2(wi + fr + fr * Mathf.Cos(th), ce - fr + fr * Mathf.Sin(th))); }
            pr.Add(new Vector2(wi - N(s, 5f) * 0.3f, Mathf.Lerp(fy + 1f, ce, 0.5f)));
            pr.Add(new Vector2(wi, fy));
            return pr;
        });
        // 上の水のくぼみ：水面のはしより5cm内側（水のはしは岩の中にかくれる）、底は yTop-GateFloor
        float fl = yTop - GateFloor;
        Loft("Cut_BasinL", Steps(lw0, lw1, 0.2f), s => { float a = lInA(s), b = lInB(s); a = a >= gIn - 0.01f ? gIn + 0.3f : a - 0.05f; return new List<Vector2> { new Vector2(b + 0.05f, fl), new Vector2(a, fl), new Vector2(a, yTop + 5f), new Vector2(b + 0.05f, yTop + 5f) }; });
        Loft("Cut_BasinR", Steps(rw0, rw1, 0.2f), s => { float a = rInA(s), b = rInB(s); a = a <= rin + 0.01f ? rin - 0.3f : a + 0.05f; return new List<Vector2> { new Vector2(a, fl), new Vector2(b - 0.05f, fl), new Vector2(b - 0.05f, yTop + 5f), new Vector2(a, yTop + 5f) }; });
        Loft("Cut_Slab", Steps(sl0, sl1 + 0.5f, 0.2f), s => new List<Vector2> { new Vector2(gIn, fl), new Vector2(rin, fl), new Vector2(rin, yTop + 5f), new Vector2(gIn, yTop + 5f) });
        sb.Append("]}");
        System.IO.File.WriteAllText(path, sb.ToString());
        Debug.Log("[2–1] 門の型を書き出し: " + path);
    }
    static float Quarter(float t) => Mathf.Sqrt(1f - (1f - t) * (1f - t));
    // 岩のはしの丸み（上から見て四分円、0.12〜1）
    static float GateRf(float s, float s0, float s1)
    {
        float t = Mathf.Clamp01(Mathf.Min(s - s0, s1 - s) / Mathf.Min(GateEndRound, (s1 - s0) * 0.5f));
        return Mathf.Max(0.12f, Mathf.Sqrt(1f - (1f - t) * (1f - t)));
    }
    // 中心線にそったごつごつの岩。prof(s, はしの丸み)＝断面の点 (横, 高さ, でこぼこさせるか) を閉じた順に。side＝外の向き（-1＝左）
    static void CragRock(Transform g, string name, float s0, float s1, int seed, Func<float, float, List<(float lat, float y, bool free)>> prof, float side, bool round = true, float step = 0.4f)
    {
        var ss = new List<float>(); for (float s = s0; s < s1 - 1e-3f; s += step) ss.Add(s); ss.Add(s1);
        int n = ss.Count, cols = 0;
        float nOff = seed * 0.731f;
        var V = new List<Vector3>(); var T = new List<int>(); var cen = new List<Vector3>(); var outN = new List<Vector3[]>();
        for (int i = 0; i < n; i++)
        {
            float s = ss[i]; Center(s, out var p, out var d); var r = Right(d); var r3 = new Vector3(r.x, 0, r.y);
            float rf = round ? GateRf(s, s0, s1) : 1f;
            var pr = prof(s, rf); cols = pr.Count;
            var c = Vector3.zero; var p2 = new Vector2[cols];
            for (int k = 0; k < cols; k++)
            {
                var (lat, y, free) = pr[k];
                // 外へだけでこぼこ（前の当たり判定の箱より内側に入らない）
                if (free) { lat += side * Mathf.PerlinNoise(nOff + s * 0.45f, k * 1.9f) * 0.6f; y += (Mathf.PerlinNoise(nOff + 30f + s * 0.35f, k * 2.7f) - 0.5f) * 0.8f; }
                p2[k] = new Vector2(lat, y);
                var q = p + r * lat; var v = new Vector3(q.x, y, q.y); V.Add(v); c += v;
            }
            cen.Add(c / cols);
            // 面の外向き：断面の辺ごとに（くぐり口のようなへこみは、まん中から見た向きだと裏返る）
            float area = 0f; for (int k = 0; k < cols; k++) { var a = p2[k]; var b = p2[(k + 1) % cols]; area += a.x * b.y - b.x * a.y; }
            var on = new Vector3[cols];
            for (int k = 0; k < cols; k++)
            {
                var e = p2[(k + 1) % cols] - p2[k]; var o2 = area > 0f ? new Vector2(e.y, -e.x) : new Vector2(-e.y, e.x);
                on[k] = r3 * o2.x + Vector3.up * o2.y;
            }
            outN.Add(on);
        }
        for (int i = 0; i < n - 1; i++)
            for (int k = 0; k < cols; k++)
            {
                int k2 = (k + 1) % cols, a = i * cols + k, b = a + cols, a2 = i * cols + k2, b2 = a2 + cols;
                var want = outN[i][k] + outN[i + 1][k];
                AddTriDir(V, T, a, a2, b, want); AddTriDir(V, T, a2, b2, b, want);
            }
        foreach (int i in new[] { 0, n - 1 })
        {
            int ci = V.Count; V.Add(cen[i]); var want = cen[i] - cen[i == 0 ? 1 : n - 2];
            for (int k = 0; k < cols; k++) AddTriDir(V, T, ci, i * cols + k, i * cols + (k + 1) % cols, want);
        }
        var V2 = new List<Vector3>(); var T2 = new List<int>();
        for (int i = 0; i < T.Count; i++) { V2.Add(V[T[i]]); T2.Add(i); }
        var mesh = new Mesh { name = "Course2_1_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V2); mesh.SetTriangles(T2, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(g, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = RockBaseMat();
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
    // くぼみの水面（見た目だけ）。横 latA〜latB、流れは sTo へ向かう（99＝前へ）
    static void BasinWater(Transform g, string name, float s0, float s1, Func<float, float> latA, Func<float, float> latB, float y, float sTo)
    {
        var SV = new List<Vector3>(); var ST = new List<int>(); var U1 = new List<Vector4>(); var U2 = new List<Vector2>();
        const int across = 6;
        int m = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 0.25f));
        for (int k = 0; k <= m; k++)
        {
            float s = Mathf.Lerp(s0, s1, k / (float)m); Center(s, out var p, out var d); var r = Right(d);
            float a = latA(s), b = latB(s), hw = Mathf.Abs(b - a) * 0.5f;
            for (int i = 0; i <= across; i++)
            {
                float lat = Mathf.Lerp(a, b, i / (float)across);
                var q = p + r * lat; SV.Add(new Vector3(q.x, y, q.y));
                bool slab = sTo > 90f;
                U1.Add(new Vector4(slab ? 0f : (i / (float)across - 0.5f) * 2f * hw, slab ? 99f : hw, slab ? 99f : s - s0, slab ? 99f : s1 - s)); U2.Add(new Vector2(slab ? s : -Mathf.Abs(s - sTo), 0));
            }
        }
        for (int k = 0; k < m; k++)
            for (int i = 0; i < across; i++)
            {
                int a = k * (across + 1) + i, b = a + across + 1;
                AddTriDir(SV, ST, a, b, a + 1, Vector3.up); AddTriDir(SV, ST, a + 1, b, b + 1, Vector3.up);
            }
        var sm = new Mesh { name = "Course2_1_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        sm.SetVertices(SV); sm.SetTriangles(ST, 0); sm.SetUVs(0, SV.Select(v => Vector2.zero).ToList()); sm.SetUVs(1, U1); sm.SetUVs(2, U2);
        sm.normals = SV.Select(v => Vector3.up).ToArray(); sm.RecalculateBounds();
        sm = Save(sm, MeshDir + "/" + name + ".asset");
        var wgo = new GameObject(name); wgo.transform.SetParent(g, false);
        wgo.AddComponent<MeshFilter>().sharedMesh = sm; wgo.AddComponent<MeshRenderer>().sharedMaterial = WaterMat(WallFlow, true);
    }

    // ---- ツタのカーテン（2026-10-22 てじゃ：滝の左をふわふわが回り込めないように。入口の脇道のクモの巣もこれにそろえる）----
    // 人はすり抜け（Walkthrough レイヤー＝プレイヤーと当たらない）、ふわふわは当たると ぽよんと跳ね返る（FuwaBounce。アウトにはしない）。
    // 見た目は Stage2Water.fbx の Vine_<長さ×10>_<A/B>（Blenderで作った1本のツタ。上が原点で下へ垂れる）を並べて1つのメッシュにまとめる。
    // top＝カーテンの上の辺のまん中（parent のローカル）、across＝横の向き
    static readonly float[] VineLens = { 0.8f, 1.1f, 1.6f, 2.2f, 2.8f, 3.4f };
    static GameObject VineCurtain(Transform parent, string name, Vector3 top, Vector3 across, float width, float height, int seed)
    {
        across = across.normalized;
        var face = Vector3.Cross(across, Vector3.up).normalized;
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = top; g.transform.localRotation = Quaternion.LookRotation(face);   // ローカル x＝横、y＝上、z＝面の向き
        var fbx = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaStage2/Models/Stage2Water.fbx");
        var mats = new[] { FlatMat("Stage2_VineStem", new Color(0.27f, 0.50f, 0.24f)), FlatMat("Stage2_VineLeaf", new Color(0.42f, 0.74f, 0.34f)), FlatMat("Stage2_VineLeafLight", new Color(0.62f, 0.86f, 0.42f)) };
        string[] matNames = { "Vine_Stem", "Vine_Leaf", "Vine_LeafLight" };
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new[] { new List<int>(), new List<int>(), new List<int>() };
        var rng = new System.Random(seed);
        float Rnd(float a, float b) => a + (b - a) * (float)rng.NextDouble();
        void Strand(float x, float z, float want)
        {
            float L = VineLens.OrderBy(l => Mathf.Abs(want / l - 1f)).First();
            // 名前は先に決める（前は比べるたびに A/B を引き直していて、メッシュの並び順しだいで見つからず、ツタが何本か抜けていた）
            string want2 = "Vine_" + Mathf.RoundToInt(L * 10f) + "_" + (rng.Next(2) == 0 ? "A" : "B");
            var src = fbx.OfType<Mesh>().FirstOrDefault(m => m.name == want2);
            var mr = fbx.OfType<GameObject>().FirstOrDefault(o => src && o.name == src.name)?.GetComponent<MeshRenderer>();
            if (!src || !mr) { Debug.LogWarning("[2–1] ツタのメッシュがありません: Vine_" + Mathf.RoundToInt(L * 10f)); return; }
            var m4 = Matrix4x4.TRS(new Vector3(x, Rnd(-0.04f, 0f), z), Quaternion.Euler(0, Rnd(0f, 360f), 0), new Vector3(1f, want / L, 1f));
            int b = V.Count;
            foreach (var v in src.vertices) V.Add(m4.MultiplyPoint3x4(v));
            foreach (var nn in src.normals) N.Add(m4.inverse.transpose.MultiplyVector(nn).normalized);
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                int k = Array.IndexOf(matNames, mr.sharedMaterials[sm].name); if (k < 0) k = 1;
                foreach (var t in src.GetTriangles(sm)) T[k].Add(b + t);
            }
        }
        // 長いツタ：10cmおきに前後2列、地面にはつかない長さ（すき間から向こうが少し見える）。上のふちには短いツタをもう1段かさねて葉っぱを茂らせる
        int n = Mathf.Max(2, Mathf.RoundToInt(width / 0.1f));
        for (int i = 0; i < n; i++)
            Strand(-width * 0.5f + (i + 0.5f) * width / n + Rnd(-0.03f, 0.03f), (i % 2 == 0 ? 1f : -1f) * Rnd(0.03f, 0.08f), height * Rnd(0.68f, 0.93f));
        int nf = Mathf.Max(2, Mathf.RoundToInt(width / 0.14f));
        for (int i = 0; i < nf; i++)
            Strand(-width * 0.5f + (i + 0.5f) * width / nf + Rnd(-0.04f, 0.04f), Rnd(-0.1f, 0.1f), height * Rnd(0.25f, 0.45f));
        string path = MeshDir + "/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = !mesh;
        if (isNew) mesh = new Mesh(); else mesh.Clear(false);
        mesh.name = "Course2_1_" + name; mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.subMeshCount = 3;
        for (int k = 0; k < 3; k++) mesh.SetTriangles(T[k], k);
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterials = mats;
        // 当たり：カーテンの厚み 0.35m の箱。面の向きで跳ね返す
        var hit = new GameObject("BallBlock"); hit.transform.SetParent(g.transform, false); hit.layer = 17;   // Walkthrough
        hit.transform.localPosition = new Vector3(0, -height * 0.5f, 0);
        var bc = hit.AddComponent<BoxCollider>(); bc.size = new Vector3(width, height, 0.35f);
        var bo = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBounce>(hit);
        bo.alwaysUp = false; bo.useContactNormal = true; bo.bounceSpeed = 1.5f; bo.keepTangent = 0.5f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bo); NoSync(bo);
        return g;
    }

    // FuwaCourse/ToonLit の単色の材質（水のテスト場の ToonMat と同じ作り）
    static Material FlatMat(string name, Color c)
    {
        string p = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Lobby_Ring.mat")); AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Color", c); EditorUtility.SetDirty(m);
        return m;
    }

    // 曲がった滝：落ち口(sLip, top)から水平に出て、放物線で reach m 先の bottom に落ちる。前後2枚の水の膜（厚み thick）＋放物線にそった押す箱
    static void CurvedFalls(Transform root, string name, float sLip, float top, float bottom, float width, float reach, float thick, Vector2? lipAt = null, Vector2? lipDir = null, float flatBack = 0f, float pushWidth = 0f)
    {
        Center(sLip, out var lp2, out var ld2);
        if (lipAt.HasValue) { lp2 = lipAt.Value; ld2 = lipDir.Value; }
        var f = new Vector3(ld2.x, 0, ld2.y); var rgt = new Vector3(ld2.y, 0, -ld2.x);
        var lip = new Vector3(lp2.x, top, lp2.y);
        float drop = top - bottom, g = 9.8f, tEnd = Mathf.Sqrt(2f * drop / g), v0 = reach / tEnd;
        // 落ち口の少し手前（水源の池の上）から始めて、ふちを丸く越える
        var pts = new List<Vector3> { lip - f * 0.5f };
        const int N = 28;
        for (int i = 0; i <= N; i++) { float t = tEnd * i / N; pts.Add(lip + f * (v0 * t) + Vector3.down * (0.5f * g * t * t)); }
        var len = new float[pts.Count]; for (int i = 1; i < pts.Count; i++) len[i] = len[i - 1] + (pts[i] - pts[i - 1]).magnitude;
        float total = len[pts.Count - 1];
        Vector3 Nrm(int i) { var tg = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized; var n = Vector3.Cross(rgt, tg).normalized; if (Vector3.Dot(n, f) + n.y < 0) n = -n; return n; }   // 外向き（前・上）
        // 厚みのある閉じた形（2026-10-25 てじゃ「最初の滝も、太いっていうより２重ってかんじ」。前は透ける膜が前後に2枚だけ）。
        // 断面は角の丸い形（「まるみつけられる？」、FuwaStage2WaterTest.FallsSection）を放物線にそって並べる
        var nrms = Enumerable.Range(0, pts.Count).Select(Nrm).ToList();
        // 断面のまん中が水面の高さだと、上の半分（厚みの半分）が水面から浮いて、裏から見ると水面と滝の上がずれて見えた（2026-10-27 てじゃ）。
        // 全体を厚みの半分だけ真下へ（上の面＝水面。落ちている所は横にずれないので、前の面は落ち口の岩より前のまま）。手前のはしは水の下へ
        //（落ち口の先で元の線へ戻すと外の面がこぶに、面の向きに内側へずらすと落ち口の岩が滝の前に出た）
        var mid = new List<Vector3>(); var vnrm = nrms; var vlen = len; float vtotal = total; List<float> frontK = null; List<float> meshThicks = null; float lipLen = 0f;
        if (flatBack > 0f)
        {
            // 滝の上の面を池の側へ flatBack だけ平らにのばして、そこから先は池の水面のかわり（池の水面はその手前で終わる。2026-10-27 てじゃ「滝の上に池をかぶせたみたい」）。
            // はしは水の下へもぐらせる（口のあいた断面が見えないように）
            var vp = new List<Vector3> { lip - f * (flatBack + 0.3f) + Vector3.down * 0.15f, lip - f * flatBack };   // もぐる深さ 0.35→0.15（平らな所を薄くしたので、深いと洞窟の出口の天井から下へ出た）
            for (int i = 1; i < pts.Count; i++) vp.Add(pts[i]);
            vlen = new float[vp.Count]; for (int i = 1; i < vp.Count; i++) vlen[i] = vlen[i - 1] + (vp[i] - vp[i - 1]).magnitude; vtotal = vlen[vp.Count - 1];
            vnrm = Enumerable.Range(0, vp.Count).Select(i => { var tg = (vp[Mathf.Min(i + 1, vp.Count - 1)] - vp[Mathf.Max(i - 1, 0)]).normalized; var n = Vector3.Cross(rgt, tg).normalized; if (Vector3.Dot(n, f) + n.y < 0) n = -n; return i == 1 ? Vector3.up : n; }).ToList();
            // 落ち口の裏（下の面）をなだらかに（2026-10-27 てじゃ「滝の裏からみたとき上が違和感」「なだらかにしよう」）：
            // 前は厚みが一定で、平らな所の下の面と落ちる所の裏の面が落ち口の下で角になり、裏から見ると平らな板の底に見えた。
            // 線を細かく取りなおして、表の面（前と同じ所）はそのまま、裏の面の線だけならして丸める（落ち口の下で厚くなって、斜めのなだらかな面になる）
            var rsP = new List<Vector3> { vp[0] };
            for (int i = 1; i < vp.Count; i++)
            {
                int k = Mathf.Max(1, Mathf.CeilToInt((vp[i] - vp[i - 1]).magnitude / 0.08f));
                for (int j = 1; j <= k; j++) rsP.Add(Vector3.Lerp(vp[i - 1], vp[i], j / (float)k));
            }
            int nr = rsP.Count;
            var rn = new List<Vector3>();
            for (int i = 0; i < nr; i++)
            {
                var tg = (rsP[Mathf.Min(i + 1, nr - 1)] - rsP[Mathf.Max(i - 1, 0)]).normalized; var n = Vector3.Cross(rgt, tg).normalized;
                if (Vector3.Dot(n, f) + n.y < 0) n = -n;
                rn.Add(n);
            }
            // 落ち口までの長さ（vp[2]＝落ち口）と、各点の長さ
            lipLen = 0f; for (int i = 1; i < 3; i++) lipLen += (vp[i] - vp[i - 1]).magnitude;
            var al = new float[nr]; for (int i = 1; i < nr; i++) al[i] = al[i - 1] + (rsP[i] - rsP[i - 1]).magnitude;
            // 落ち口より手前（平らな所と、池の下へもぐる所）は断面を水平のまま（向きを変えると裏の面が折れ返った）
            for (int i = 0; i < nr; i++) if (al[i] < lipLen - 0.02f) rn[i] = Vector3.up;
            var outer = new List<Vector3>(); var inner = new List<Vector3>();
            // 落ち口より手前（岩の上を流れる平らな所）は薄く FlatThick（2026-10-27 てじゃ「滝裏に変な岩の線」：厚み 0.9m のままだと洞窟の出口の天井の岩（厚み 0.8m 弱）より下へ
            // 滝の底がはみ出して、その手前を落ち口の岩のふちが横切って線に見えた）。落ち口の先は今まで通り thick
            for (int i = 0; i < nr; i++)
            {
                var o = rsP[i] + Vector3.down * (thick * 0.5f) + rn[i] * (thick * 0.5f);
                float tk = al[i] < lipLen - 0.02f ? FlatThick : thick;
                outer.Add(o); inner.Add(o - rn[i] * tk);
            }
            // ならすのは落ち口の手前 UnderSmoothBack m から先だけ（池の下の所は動かさない）
            for (int it = 0; it < UnderSmoothIter; it++)
            {
                var nx = new List<Vector3>(inner);
                for (int i = 1; i < nr - 1; i++) if (al[i] > lipLen - UnderSmoothBack) nx[i] = (inner[i - 1] + 2f * inner[i] + inner[i + 1]) * 0.25f;
                inner = nx;
            }
            // 断面は 表の点 と ならした裏の点 を結ぶ向きに（表の面の向きのままだと、落ち口の曲がりで厚い所の断面どうしが交差して、裏に折れ目の線が出た）
            var thicks = new List<float>(); mid.Clear(); vnrm = new List<Vector3>();
            for (int i = 0; i < nr; i++)
            {
                var d = outer[i] - inner[i]; float tk = Mathf.Max(FlatThick * 0.9f, d.magnitude);
                var ax = d.magnitude > 1e-4f ? d / d.magnitude : rn[i];
                thicks.Add(tk); vnrm.Add(ax); mid.Add(outer[i] - ax * (tk * 0.5f));
            }
            vlen = new float[nr]; for (int i = 1; i < nr; i++) vlen[i] = vlen[i - 1] + (rsP[i] - rsP[i - 1]).magnitude; vtotal = vlen[nr - 1];
            meshThicks = thicks;
        }
        else for (int i = 0; i < pts.Count; i++) mid.Add(pts[i] + Vector3.down * (thick * 0.5f + (i == 0 ? 0.15f : 0f)));
        var mesh = FuwaStage2WaterTest.SweepFalls("Course2_1_" + name, mid, vnrm, Enumerable.Repeat(rgt, mid.Count).ToList(), vlen, vtotal, width, thick, frontK, meshThicks);
        mesh = Save(mesh, MeshDir + "/" + name + ".asset");
        var g0 = new GameObject(name).transform; g0.SetParent(root, false);
        var cur = new GameObject("Curtain"); cur.transform.SetParent(g0, false);
        cur.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mat = FuwaStage2WaterTest.WaterfallThickMat(true);
        if (flatBack > 0f)
        {
            // 落ち口の手前の平らな所は池の水の色に（池から落ち口まで同じ水に見えるように。2026-10-27 てじゃ「落ち口ちょっときになる」：池と滝の上の面のさかい目が線に見えた）
            const string pp = FallsPondTopMat;
            var pm = AssetDatabase.LoadAssetAtPath<Material>(pp);
            if (!pm) { pm = new Material(mat); AssetDatabase.CreateAsset(pm, pp); }
            pm.CopyPropertiesFromMaterial(mat);
            pm.SetFloat("_PondTop", lipLen);
            PondLookToFallsTop(pm);
            mat = pm;
        }
        var mr = cur.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 押す箱：放物線を 0.8m ごとに区切って、膜にそわせる（箱の +Z＝外向き＝FuwaWaterfall が前へ押す向き）。下は滝つぼの水面の 0.35m 上まで
        float seg = 0.8f; int ns = Mathf.Max(1, Mathf.CeilToInt(total / seg));
        for (int k = 0; k < ns; k++)
        {
            float la = total * k / ns, lb = total * (k + 1) / ns;
            Vector3 P(float l) { int i = 1; while (i < pts.Count - 1 && len[i] < l) i++; float u = Mathf.InverseLerp(len[i - 1], len[i], l); return Vector3.Lerp(pts[i - 1], pts[i], u); }
            var pa = P(la); var pb = P(lb);
            if (Mathf.Min(pa.y, pb.y) < bottom + 0.35f) { if (Mathf.Max(pa.y, pb.y) < bottom + 0.35f) continue; }
            var tg = (pb - pa).normalized; var n = Vector3.Cross(rgt, tg).normalized; if (Vector3.Dot(n, f) + n.y < 0) n = -n;
            var box = new GameObject("Push" + k); box.transform.SetParent(g0, false); box.layer = 4;
            box.transform.localPosition = (pa + pb) * 0.5f; box.transform.localRotation = Quaternion.LookRotation(n, tg);
            var bc = box.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(pushWidth > 0f ? pushWidth : width, (pb - pa).magnitude + 0.05f, thick + 0.5f);
            var wf = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWaterfall>(box);
            wf.forwardPush = 3.5f;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(wf); NoSync(wf);
        }
        // しぶきの霧と音：水のテスト場の滝を落ちる所に置いて、膜と押す箱だけ外す
        var land = pts[pts.Count - 1];
        FuwaStage2WaterTest.Waterfall(g0, "Splash", new Vector3(land.x, 0, land.z), bottom + 0.1f, bottom, width, f);
        var sp = g0.Find("Splash");
        foreach (var n in new[] { "Curtain", "Push" }) { var c = sp.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        // 水面に重ねる泡（2026-10-27 てじゃ「滝と水面の境目をもっと見栄え良く」「滝が出来るほうもなんかいいかんじに」）：
        // 落ち口の手前は 落ち口へ吸い寄せられる筋と きわの明るい帯、滝つぼ（下に水面がある滝だけ）は白い泡と泡立った水と広がる輪
        float hw = width * 0.5f;
        WaterDecal(g0, "LipFoam", lip + Vector3.up * 0.012f, -f, -hw, hw, 0f, LipDecalReach + flatBack, 1, hw, LipDecalReach + flatBack, new Color(0.72f, 0.93f, 0.99f), flatBack);
        if (bottom > -1f)
            WaterDecal(g0, "PoolFoam", new Vector3(land.x, bottom + 0.012f, land.z), f, -hw - SplashReach, hw + SplashReach, -thick * 0.5f - 1.2f, SplashReach * 1.6f, 0, hw, SplashReach, new Color(0.45f, 0.78f, 0.95f));
    }
    public static float RippleFadeR0 = 2.8f, RippleFadeR1 = 4.2f;
    const string FallsPondTopMat = "Assets/FuwaStage2/Materials/Stage2_WaterfallCurvedThick_PondTop.mat";
    // 滝の上の平らな所を、水源の池の水面と同じ見た目に（色・波紋・きらめきの値を池の材質からうつす。2026-10-27 てじゃ「滝と水面の境目、もっときっちり繋ぐ方法」）
    static void PondLookToFallsTop(Material pm)
    {
        var pond = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_WaterRibbon_SourceRadial.mat");
        if (!pond) pond = WaterMat(0f, true, false);
        pm.SetColor("_PondColor", pond.GetColor("_Deep")); pm.SetColor("_PondFoam", pond.GetColor("_Foam"));
        pm.SetFloat("_PondStreak", pond.GetFloat("_StreakStrength")); pm.SetFloat("_PondSparkle", pond.GetFloat("_Sparkle"));
        pm.SetFloat("_PondRadial", pond.HasProperty("_Radial") ? pond.GetFloat("_Radial") : 0f);
        pm.SetVector("_RadialCenter", pond.GetVector("_RadialCenter")); pm.SetFloat("_RadialSpeed", pond.GetFloat("_RadialSpeed"));
        EditorUtility.SetDirty(pm);
    }
    public static float SplashReach = 3.2f, LipDecalReach = 2.6f;
    public static int UnderSmoothIter = 120; public static float UnderSmoothBack = 0.3f, FlatThick = 0.45f;   // 落ち口の裏の面をならす回数（多いほど丸みが大きい）
    // 平らな格子の板（uv＝ローカルの (x, z) m。+Z＝dir）。材質は FuwaStage2/WaterDecal
    static void WaterDecal(Transform parent, string name, Vector3 pos, Vector3 dir, float x0, float x1, float z0, float z1, int mode, float halfW, float reach, Color aerated, float band = 0f)
    {
        const float st = 0.25f;
        int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / st)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / st));
        var V = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz);
                V.Add(new Vector3(x, 0, z)); UV.Add(new Vector2(x, z));
            }
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + nx + 1;
                T.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var mesh = new Mesh { name = "Course2_1_" + parent.name + "_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetUVs(0, UV); mesh.SetTriangles(T, 0);
        mesh.normals = V.Select(v => Vector3.up).ToArray(); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/" + parent.name + "_" + name + ".asset");
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z).normalized, Vector3.up);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        string p = "Assets/FuwaStage2/Materials/Stage2_" + parent.name + "_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var sh = Shader.Find("FuwaStage2/WaterDecal");
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh; m.SetFloat("_Mode", mode); m.SetFloat("_HalfW", halfW); m.SetFloat("_Reach", reach); m.SetColor("_Aerated", aerated); m.SetFloat("_Band", band);
        EditorUtility.SetDirty(m);
        mr.sharedMaterial = m;
    }

    // トゲの植物：川幅いっぱい、水面の下から上まで。見た目はまもののすのイバラのパッチを並べる。当たり（FuwaSpike）は1つ
    static void Thorns(Transform root, string name, float s, float surf, float latA, float latB, float len)
    {
        var src = GameObject.Find("FuwaCourse4/Spikes/SpikePatch0_Start"); if (!src) return;
        Center(s, out var tp, out var td);
        var tr = new GameObject(name).transform; tr.SetParent(root, false);
        tr.localPosition = new Vector3(tp.x, surf, tp.y);
        tr.localRotation = Quaternion.LookRotation(new Vector3(td.x, 0, td.y));   // +Z＝流れの向き
        float hw = (latB - latA) * 0.5f, mid = (latA + latB) * 0.5f;
        int n = Mathf.Max(2, Mathf.RoundToInt(hw * 2f / 1.5f));
        for (int i = 0; i < n; i++)
        {
            var th = Object.Instantiate(src, tr); th.name = "Patch" + i; th.SetActive(true);
            th.transform.localPosition = new Vector3(mid + Mathf.Lerp(-hw + 0.7f, hw - 0.7f, i / (float)(n - 1)), 0f, (i % 2 == 0 ? -0.35f : 0.35f) * len / 3f);
            th.transform.localRotation = Quaternion.Euler(0, 90f + i * 37f, 0); th.transform.localScale = new Vector3(0.75f, 1.25f, 0.75f);
            var bs = th.transform.Find("Base"); if (bs) bs.gameObject.SetActive(false);
            var t0 = th.transform.Find("Trigger");
            if (i == 0 && t0)
            {
                t0.SetParent(tr, false); t0.localPosition = new Vector3(mid, 0.25f, 0); t0.localRotation = Quaternion.identity; t0.localScale = Vector3.one;
                var bc = t0.GetComponent<BoxCollider>(); bc.center = Vector3.zero; bc.size = new Vector3(hw * 2f, 1.3f, len);
            }
            else if (t0) Object.DestroyImmediate(t0.gameObject);
        }
    }

    // ---- 道のメッシュ（FuwaCourse3Builder と同じ断面：端は丸め、縞は0.75mごと。縞はその道の実際の長さで数える）----
    static Vector2 PieceC(Piece pc, float s) { if (pc.at != null) return pc.at(s); Center(s, out var p, out var d); return p + Right(d) * pc.off(s); }
    static Vector3 Point(Piece pc, float s, float lat, float dy)
    {
        if (pc.centerLat)
        {
            Center(s, out var cp, out var cd); var cq = cp + Right(cd) * (pc.off(s) + lat);
            return new Vector3(cq.x, pc.h(s) + dy, cq.y);
        }
        var c = PieceC(pc, s); var t = PieceC(pc, s + 0.05f) - PieceC(pc, s - 0.05f);
        t = t.sqrMagnitude > 1e-8f ? t.normalized : Vector2.up;
        var q = c + Right(t) * lat;
        return new Vector3(q.x, pc.h(s) + dy, q.y);
    }
    static Vector3 Tangent3(Piece pc, float s)
    {
        var a = Point(pc, s - 0.05f, pc.centerLat ? -pc.off(s - 0.05f) : 0f, 0); var b = Point(pc, s + 0.05f, pc.centerLat ? -pc.off(s + 0.05f) : 0f, 0);
        return (b - a).normalized;
    }
    static Vector3 Normal(Piece pc, float s, float nx, float ny)
    {
        var t = Tangent3(pc, s);
        var right = new Vector3(t.z, 0, -t.x).normalized;
        var up = Vector3.Cross(t, right).normalized;
        if (up.y < 0) up = -up;
        return (right * nx + up * ny).normalized;
    }
    static List<Vector4> Profile(float w)
    {
        float W = w * 0.5f;
        var prof = new List<Vector4> { new Vector4(-W, -Thick, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-W + Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(W - Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(W, -Thick, 1, 0));
        return prof;
    }
    static void AddTriDir(List<Vector3> V, List<int> T, int a, int b, int c, Vector3 want)
    {
        var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
        if (Vector3.Dot(n, want) >= 0) T.AddRange(new[] { a, b, c }); else T.AddRange(new[] { a, c, b });
    }

    // ---- 木の足場（2026-10-27）：ゴールの島の橋（WoodVC）と同じ作り＝頂点カラーの板を並べて、下に2本の桁。材質も同じ Den_Twig ----
    // 板の上の面は道の高さ（当たり判定は PathMesh のまま）。ゴールへの道の終わりは、見た目だけ島の橋の板の下までのばす（道のはしのままだと すき間が見えた）
    const float GoalWoodLap = 0.35f;
    const float PlankPitch = 0.32f, PlankT = 0.09f, BeamW = 0.16f, BeamH = 0.18f, BeamIn = 0.22f;
    // 迂回路の浮島（ゴールの島と同じ作りの小さい版：芝の丸い台＋下の岩）。台の上はふわふわが乗るとアウト（道と同じ FuwaTopOut）
    static void BridgeIsland(Transform root)
    {
        var goal = root.Find("Goal"); var gg = goal ? goal.Find("GoalGround") : null; var gu = goal ? goal.Find("IslandUnder") : null;
        if (!gg || !gu) { Debug.LogWarning("[2–1] 浮島の元（Goal の GoalGround / IslandUnder）がない"); return; }
        var isl = new GameObject("BridgeIsland").transform; isl.SetParent(root, false);
        isl.localPosition = new Vector3(bridgeIslandC.x, bridgeIslandH + 0.02f, bridgeIslandC.y);
        float k = BridgeIslandR * 2f / gg.localScale.x;
        var top = Object.Instantiate(gg.gameObject, isl, false); top.name = "Ground";
        top.transform.localPosition = gg.localPosition; top.transform.localScale = new Vector3(gg.localScale.x * k, gg.localScale.y, gg.localScale.z * k);
        var under = Object.Instantiate(gu.gameObject, isl, false); under.name = "Under";
        under.transform.localPosition = new Vector3(0f, gu.localPosition.y, 0f); under.transform.localScale = gu.localScale * k;
        var to = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTopOut>(top); if (to) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(to); NoSync(to); }
        GameObjectUtility.SetStaticEditorFlags(top, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        GameObjectUtility.SetStaticEditorFlags(under, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    // 洞窟の床のはし（横 3m）から、ななめの穴の中のツタの線までの青い床（見た目だけ。当たり判定は B1_Alcove）。縞は洞窟の床（M1）とそろえる
    static void SideAlcoveFloor(Transform root, Material a, Material b)
    {
        var across = new Vector2(-SideD.y, SideD.x);
        Vector2 back0 = SideV - across * SideHW, front0 = SideV + across * SideHW;
        // 穴のへり（SideD の向きの線）と床のはし x=3 の交わり（ここは中心線が z 方向にまっすぐ＝横＝x）
        Vector2 OnEdge(Vector2 q) => q + SideD * ((3f - q.x) / SideD.x);
        var poly = new List<Vector2> { OnEdge(back0), back0, front0, OnEdge(front0) };
        float s0 = sPlaza0 - 1.5f;
        var V = new List<Vector3>(); var TA = new List<int>(); var TB = new List<int>();
        float zMin = poly.Min(q => q.y), zMax = poly.Max(q => q.y);
        int k0 = Mathf.FloorToInt((zMin - s0) / Stripe), k1 = Mathf.FloorToInt((zMax - s0) / Stripe);
        for (int k = k0; k <= k1; k++)
        {
            float za = s0 + k * Stripe, zb = za + Stripe;
            // 帯 za..zb で多角形を切る
            var c = ClipZ(ClipZ(poly, za, true), zb, false);
            if (c.Count < 3) continue;
            int bi = V.Count; foreach (var q in c) V.Add(new Vector3(q.x, StartH, q.y));
            var list = (k % 2 + 2) % 2 == 0 ? TA : TB;
            for (int i = 1; i < c.Count - 1; i++) AddTriDir(V, list, bi, bi + i, bi + i + 1, Vector3.up);
        }
        var m = new Mesh { name = "Course2_1_SideAlcoveFloor" };
        m.SetVertices(V); m.subMeshCount = 2; m.SetTriangles(TA, 0); m.SetTriangles(TB, 1);
        m.SetUVs(0, V.Select(v => new Vector2(v.x, v.z)).ToList()); m.RecalculateNormals(); m.RecalculateBounds();
        m = Save(m, MeshDir + "/SideAlcoveFloor.asset");
        var go = new GameObject("SideAlcoveFloor"); go.transform.SetParent(root, false); go.layer = 11;
        go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterials = new[] { a, b };
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
    }
    // 多角形を z = z0 の線で切る（keepAbove＝z0 より大きい側を残す）
    static List<Vector2> ClipZ(List<Vector2> poly, float z0, bool keepAbove)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var p = poly[i]; var q = poly[(i + 1) % poly.Count];
            bool pin = keepAbove ? p.y >= z0 : p.y <= z0, qin = keepAbove ? q.y >= z0 : q.y <= z0;
            if (pin) o.Add(p);
            if (pin != qin) o.Add(Vector2.Lerp(p, q, (z0 - p.y) / (q.y - p.y)));
        }
        return o;
    }

    static void WoodWalk(Transform root, List<Piece> pieces)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color32>(); var T = new List<int>();
        var rockCol = root.Find("Terrain/CaveRock")?.GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        var rnd = new System.Random(2701);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        Color32 Wood(float r, bool under) { float k = under ? 0.8f : 1f; return new Color32((byte)(r * k), (byte)(r * 0.71f * k), (byte)(r * 0.47f * k), (byte)(under ? 204 : 255)); }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Color32 col)
        {
            int i = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            for (int k = 0; k < 4; k++) { N.Add(n); C.Add(col); }
            AddTriDir(V, T, i, i + 1, i + 2, n); AddTriDir(V, T, i, i + 2, i + 3, n);
        }
        // 箱：中心 c、軸 x/y/z（単位）、半分の大きさ e
        void Box(Vector3 c, Vector3 x, Vector3 y, Vector3 z, Vector3 e, float r)
        {
            Vector3 P(float sx, float sy, float sz) => c + x * (e.x * sx) + y * (e.y * sy) + z * (e.z * sz);
            Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), y, Wood(r, false));
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), -y, Wood(r, true));
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -x, Wood(r, false));
            Quad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), x, Wood(r, false));
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), z, Wood(r, false));
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -z, Wood(r, false));
        }
        foreach (var pc in pieces)
        {
            float s0 = float.IsNaN(pc.woodS0) ? pc.s0 : pc.woodS0, s1 = pc.name == "M8_ToGoal" ? pc.s1 + GoalWoodLap : pc.s1;
            Func<float, float> lift = pc.woodLift ?? (s => 0f);
            bool keepRight = pc.name == "W5_Deck";   // 右は青い道と接するので、板をはみ出させない
            // 坂も含めた長さで板を等間隔に
            var ss = new List<float>(); var dd = new List<float>();
            int n = Mathf.Max(2, Mathf.CeilToInt((s1 - s0) / 0.05f));
            for (int k = 0; k <= n; k++)
            {
                float s = Mathf.Lerp(s0, s1, k / (float)n); ss.Add(s);
                dd.Add(k == 0 ? 0f : dd[k - 1] + (Point(pc, s, 0, 0) - Point(pc, ss[k - 1], 0, 0)).magnitude);
            }
            float len = dd[n];
            float SAt(float d) { int k = 1; while (k < n && dd[k] < d) k++; return Mathf.Lerp(ss[k - 1], ss[k], Mathf.InverseLerp(dd[k - 1], dd[k], d)); }
            int np = Mathf.Max(1, Mathf.RoundToInt(len / PlankPitch)); float pitch = len / np;
            for (int i = 0; i < np; i++)
            {
                float s = SAt((i + 0.5f) * pitch), w = pc.w(s);
                var t = Tangent3(pc, s); var x = Normal(pc, s, 1, 0); var y = Normal(pc, s, 0, 1);
                if (pc.woodEdgeCut)
                {
                    // 前後のはしの線（s ± 板の半分）それぞれで、道のへり ±w/2 の点をつなぐ台形。上下に厚み
                    float sa = SAt(i * pitch + 0.015f), sb = SAt((i + 1) * pitch - 0.015f);
                    float r0 = R(140f, 190f); var col = Wood(r0, false);
                    var yy = y * (PlankT * 0.5f);
                    Vector3 Pt(float ss2, float lat) => Point(pc, ss2, lat, lift(ss2) - PlankT * 0.5f);
                    float wa = pc.w(sa) * 0.5f, wb = pc.w(sb) * 0.5f;
                    var fL = Pt(sa, -wa); var fR = Pt(sa, wa); var bR = Pt(sb, wb); var bL = Pt(sb, -wb);
                    // 板ごとにはしの出っぱり・高さ・ねじれを少しずつ（2026-10-27 てじゃ「木の橋はちょっと凸凹感」）。はじめの板（ツタの線）ははしをそろえる
                    if (i > 0)
                    {
                        var acr = (fR - fL).normalized; float jL = R(-0.05f, 0.09f), jR = R(-0.05f, 0.09f);
                        fL -= acr * jL; bL -= acr * jL; fR += acr * jR; bR += acr * jR;
                        // 極わずかな角度でガタつき（2026-10-27 てじゃ「木の板に極僅かに角度つけてガタツキ感」）：左右のねじれ ±1.5cm・前後の傾き ±1cm・高さ
                        var dy = y * R(-0.008f, 0.014f); var tw = y * R(-0.015f, 0.015f); var pt = y * R(-0.01f, 0.01f);
                        fL += dy + tw - pt; bL += dy + tw + pt; fR += dy - tw - pt; bR += dy - tw + pt;
                    }
                    if (!float.IsNaN(pc.floorEdge))
                    {
                        // 中心線からの横の位置
                        // 中心線の (s, 横) へ：近い s をくりかえし寄せる（at の道は s が中心線の s ではないので）
                        float SC(Vector3 q) { float sc = 4f; for (int it2 = 0; it2 < 6; it2++) { Center(sc, out var cp, out var cd); sc += Vector2.Dot(new Vector2(q.x, q.z) - cp, cd); } return sc; }
                        float CL(Vector3 q) { Center(SC(q), out var cp, out var cd); return Vector2.Dot(new Vector2(q.x, q.z) - cp, Right(cd)); }
                        float fe = pc.floorEdge + 0.005f;
                        bool Inside(Vector3 q) => SC(q) < pc.floorEdgeS1 && CL(q) < fe;
                        // 左（内側）のはしを、床のはしの線まで右へ寄せる
                        Vector3 Pull(Vector3 l, Vector3 r)
                        {
                            if (!Inside(l)) return l;
                            if (Inside(r)) return r;
                            float a = 0f, c = 1f;
                            for (int it = 0; it < 20; it++) { float m = (a + c) * 0.5f; if (Inside(Vector3.Lerp(l, r, m))) a = m; else c = m; }
                            return Vector3.Lerp(l, r, c);
                        }
                        if (Inside(fR) && Inside(bR)) continue;
                        fL = Pull(fL, fR); bL = Pull(bL, bR);
                        if ((fR - fL).magnitude < 0.05f && (bR - bL).magnitude < 0.05f) continue;
                    }
                    // 岩で切る（はしが岩にささらないように）
                    if (rockCol != null)
                    {
                        Vector3 Clip(Vector3 from, Vector3 to)
                        {
                            var wo = root.TransformPoint(from + y * 0.02f); var dir = root.TransformDirection(to - from); float len2 = dir.magnitude;
                            if (len2 > 1e-4f && rockCol.Raycast(new Ray(wo, dir / len2), out var hh, len2)) return from + (to - from) * Mathf.Max(0f, (hh.distance - 0.03f) / len2);
                            return to;
                        }
                        var cf = (fL + fR) * 0.5f; var cb = (bL + bR) * 0.5f;
                        fL = Clip(cf, fL); fR = Clip(cf, fR); bL = Clip(cb, bL); bR = Clip(cb, bR);
                    }
                    var tz2 = ((bL + bR) - (fL + fR)).normalized;
                    Quad(fL + yy, fR + yy, bR + yy, bL + yy, y, col);
                    Quad(fL - yy, bL - yy, bR - yy, fR - yy, -y, Wood(r0, true));
                    Quad(fL - yy, fR - yy, fR + yy, fL + yy, -tz2, col);
                    Quad(bL - yy, bL + yy, bR + yy, bR - yy, tz2, col);
                    Quad(fL - yy, fL + yy, bL + yy, bL - yy, -x, col);
                    Quad(fR - yy, bR - yy, bR + yy, fR + yy, x, col);
                    continue;
                }
                float al = pc.woodAlign != null ? Mathf.Clamp01(pc.woodAlign(s)) : 0f;
                if (al > 0f)
                {
                    // 板の向きを洞窟の右向きに寄せ、はしは道のへりの線で切る（平行四辺形）
                    Center(s, out _, out var cd); var cr3 = new Vector3(cd.y, 0, -cd.x);
                    var dx = Vector3.Slerp(x, cr3, al).normalized; var dz = Vector3.Cross(dx, y).normalized; if (Vector3.Dot(dz, t) < 0) dz = -dz;
                    float cosA = Mathf.Max(0.3f, Vector3.Dot(dx, x)), hl = w * 0.5f / cosA, hz = (pitch - 0.03f) * 0.5f, r0 = R(140f, 190f);
                    var c = Point(pc, s, 0f, lift(s) - PlankT * 0.5f);
                    // 前後のはしの線それぞれで、道のへり（x 方向に ±w/2）までの長さ：へりの線は道の向き t にそっているので、dz のずれぶん補正
                    float slip = Vector3.Dot(dz, x) * hz / cosA;
                    float uL = hl, uR = hl;
                    // 岩で切る（板のはしが岩にささらないように）
                    if (rockCol != null)
                    {
                        // メッシュは root のローカル。レイはワールドで
                        var wo = root.TransformPoint(c + y * 0.02f); var wd = root.TransformDirection(dx);
                        if (rockCol.Raycast(new Ray(wo, wd), out var hR, hl + 1f)) uR = Mathf.Min(uR, hR.distance - 0.03f);
                        if (rockCol.Raycast(new Ray(wo, -wd), out var hL, hl + 1f)) uL = Mathf.Min(uL, hL.distance - 0.03f);
                    }
                    Vector3 Cn(float u, float zf) => c + dx * (u - zf * slip) + dz * (zf * hz);
                    var tFL = Cn(-uL, -1f); var tFR = Cn(uR, -1f); var tBR = Cn(uR, 1f); var tBL = Cn(-uL, 1f);
                    // はしが岩で切られた所は、前後とも同じ長さ（平行四辺形をやめる）
                    if (uL < hl) { tFL = c - dx * uL - dz * hz; tBL = c - dx * uL + dz * hz; }
                    if (uR < hl) { tFR = c + dx * uR - dz * hz; tBR = c + dx * uR + dz * hz; }
                    var up = y * (PlankT * 0.5f); var col = Wood(r0, false);
                    Quad(tFL + up, tFR + up, tBR + up, tBL + up, y, col);
                    Quad(tFL - up, tBL - up, tBR - up, tFR - up, -y, Wood(r0, true));
                    Quad(tFL - up, tFR - up, tFR + up, tFL + up, -dz, col);
                    Quad(tBL - up, tBL + up, tBR + up, tBR - up, dz, col);
                    Quad(tFL - up, tFL + up, tBL + up, tBL - up, -dx, col);
                    Quad(tFR - up, tBR - up, tBR + up, tFR + up, dx, col);
                    continue;
                }
                float yaw = R(-0.8f, 0.8f) * Mathf.Deg2Rad; var tz = (t * Mathf.Cos(yaw) + x * Mathf.Sin(yaw)).normalized; var tx = Vector3.Cross(y, tz).normalized;
                if (Vector3.Dot(tx, x) < 0) tx = -tx;
                float eL = R(-0.01f, 0.06f), eR = keepRight ? R(-0.05f, -0.01f) : R(-0.01f, 0.06f);
                var cc = Point(pc, s, (eR - eL) * 0.5f, lift(s) - PlankT * 0.5f + R(-0.006f, 0.004f));
                Box(cc, tx, y, tz, new Vector3(w * 0.5f + (eL + eR) * 0.5f, PlankT * 0.5f, (pitch - 0.03f) * 0.5f - R(0f, 0.015f)), R(140f, 190f));
            }
            // 桁：板の下に2本（左右のへりから BeamIn 内側）
            foreach (var side in new[] { -1f, 1f })
            {
                float br = R(90f, 105f);
                float bs0 = float.IsNaN(pc.woodBeamS0) ? s0 : Mathf.Max(s0, pc.woodBeamS0);
                int m = Mathf.Max(1, Mathf.CeilToInt((s1 - bs0) / 0.25f));
                Vector3[] Ring(float s)
                {
                    float lat = side * (pc.w(s) * 0.5f - BeamIn), l = lift(s);
                    return new[] { Point(pc, s, lat - BeamW * 0.5f, l - PlankT), Point(pc, s, lat + BeamW * 0.5f, l - PlankT), Point(pc, s, lat + BeamW * 0.5f, l - PlankT - BeamH), Point(pc, s, lat - BeamW * 0.5f, l - PlankT - BeamH) };
                }
                var prev = Ring(bs0);
                Quad(prev[0], prev[1], prev[2], prev[3], -Tangent3(pc, bs0), Wood(br, false));
                for (int k = 1; k <= m; k++)
                {
                    float s = Mathf.Lerp(bs0, s1, k / (float)m); var cur = Ring(s);
                    for (int f = 0; f < 4; f++)
                    {
                        int g = (f + 1) % 4; var mid = (prev[f] + prev[g] + cur[f] + cur[g]) * 0.25f;
                        var ctr = (prev[0] + prev[2] + cur[0] + cur[2]) * 0.25f;
                        Quad(prev[f], prev[g], cur[g], cur[f], (mid - ctr).normalized, Wood(br, f == 2));
                    }
                    prev = cur;
                }
                Quad(prev[0], prev[1], prev[2], prev[3], Tangent3(pc, s1), Wood(br, false));
            }
            // 橋の先っぽをこわれて崩れた感じに（2026-10-27 てじゃ「最初の木の橋の先端、壊れて崩れたみたいにしたい」「足場は短くしたくないから追加する感じで」）：
            // 橋の終わり（渡り場の手前）の先に、見た目だけの折れた板・たれ下がった板・折れた桁を足す（当たり判定なし。歩ける所は今のまま）
            if (pc.name == "B1_Bridge")
            {
                var t = Tangent3(pc, s1); var x = Normal(pc, s1, 1, 0); var y = Normal(pc, s1, 0, 1);
                float hw = pc.w(s1) * 0.5f, pp = PlankPitch, l = lift(s1);
                var c0 = Point(pc, s1, 0f, l - PlankT * 0.5f);
                // a から dir の向きに長さ len、幅 wid の板（side＝幅の向きの目安）
                void Slab(Vector3 a, Vector3 dir, Vector3 side, float len, float wid, float th, float r)
                {
                    dir.Normalize(); var up = Vector3.Cross(dir, side).normalized; if (Vector3.Dot(up, y) < 0f) up = -up;
                    var sd = Vector3.Cross(up, dir).normalized;
                    Box(a + dir * (len * 0.5f), sd, up, dir, new Vector3(wid * 0.5f, th * 0.5f, len * 0.5f), r);
                }
                float bl = -hw + BeamIn, brt = hw - BeamIn, by = -PlankT - BeamH * 0.5f;
                // 桁：左はもう少し先まで残って折れた先、右は根元で折れて下へ
                Slab(c0 + x * bl + y * (by + PlankT * 0.5f) - t * 0.02f, t - y * 0.06f, x, 0.75f, BeamW, BeamH, 95f);
                Slab(c0 + x * brt + y * (by + PlankT * 0.5f) - t * 0.02f, t * 0.45f - y, x, 0.55f, BeamW, BeamH, 92f);
                // 1枚目：左から 2/3 だけ残った板（少し傾いて、右はしは折れて欠けた）
                Slab(c0 + t * (pp * 0.5f) + x * (-hw - 0.04f) - y * 0.005f, x - y * 0.05f + t * 0.06f, t, hw * 2f * 0.68f, pp - 0.035f, PlankT, 165f);
                // 2枚目：左の桁の上に短く残った板（先が下がる）
                Slab(c0 + t * (pp * 1.5f + 0.01f) + x * (-hw + 0.02f) - y * 0.02f, x - y * 0.22f + t * 0.03f, t, 0.85f, pp - 0.04f, PlankT, 150f);
                // 3枚目：1枚目の右の欠けた所から、右の折れた桁ぞいに垂れ下がる板
                Slab(c0 + t * (pp * 0.6f) + x * (hw * 0.45f) - y * 0.03f, -y + x * 0.35f + t * 0.3f, t, 0.8f, pp - 0.04f, PlankT, 175f);
                // 4枚目：2枚目の先からぶら下がる短い板
                Slab(c0 + t * (pp * 1.6f) + x * (-hw + 0.8f) - y * 0.2f, -y + x * 0.15f + t * 0.45f, t, 0.55f, pp - 0.05f, PlankT, 140f);
                // 折れ口のささくれ（細い木っ端）
                Slab(c0 + t * (pp * 0.5f) + x * (-hw + hw * 2f * 0.66f) - y * 0.02f, x + t * 0.25f - y * 0.1f, t, 0.22f, 0.035f, PlankT * 0.7f, 185f);
                Slab(c0 + t * (pp * 0.55f) + x * (-hw + hw * 2f * 0.64f) - y * 0.025f, x - t * 0.3f - y * 0.2f, t, 0.16f, 0.03f, PlankT * 0.6f, 170f);
                Slab(c0 + t * (pp * 1.5f) + x * (-hw + 0.84f) - y * 0.2f, x * 0.6f - y + t * 0.2f, t, 0.14f, 0.03f, PlankT * 0.6f, 160f);
            }
        }
        var mesh = new Mesh { name = "Course2_1WoodWalk" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetColors(C); mesh.SetTriangles(T, 0); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/Course2_1WoodWalk.asset");
        var go = new GameObject("WoodWalk"); go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Generated/Den/Den_Twig.mat");
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    static void BuildPathVisual(Mesh mesh, List<Piece> pieces)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
        var A = new List<int>(); var Bt = new List<int>();
        const float step = 0.25f;
        foreach (var pc in pieces)
        {
            float s0p = pc.s0, s1p = pc.s1;
            bool b0 = pc.round0, b1 = pc.round1;
            var st = new List<Vector4>();
            if (b0) for (int k = BevelSteps; k >= 1; k--) { float th = (Mathf.PI / 2) * k / BevelSteps; st.Add(new Vector4(s0p + Bevel * (1 - Mathf.Sin(th)), Bevel * (1 - Mathf.Cos(th)), th, -1)); }
            float a0 = b0 ? s0p + Bevel : s0p, a1 = b1 ? s1p - Bevel : s1p;
            int n = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / step));
            for (int k = 0; k <= n; k++) st.Add(new Vector4(Mathf.Lerp(a0, a1, k / (float)n), 0, 0, 0));
            if (b1) for (int k = 1; k <= BevelSteps; k++) { float th = (Mathf.PI / 2) * k / BevelSteps; st.Add(new Vector4(s1p - Bevel * (1 - Mathf.Sin(th)), Bevel * (1 - Mathf.Cos(th)), th, 1)); }
            // その道の実際の長さ（縞の間隔をそろえる）
            var dist = new float[st.Count];
            Vector2 DC(float s) { if (!pc.centerLat) return PieceC(pc, s); Center(s, out var p0, out _); return p0; }
            for (int r = 1; r < st.Count; r++) dist[r] = dist[r - 1] + (DC(st[r].x) - DC(st[r - 1].x)).magnitude;
            var rings = new List<int>(); int P = 0;
            for (int r = 0; r < st.Count; r++)
            {
                var sv = st[r];
                float s = sv.x, inset = sv.y, th = sv.z, dir = sv.w;
                var prof = Profile(pc.w(s)); P = prof.Count;
                var t3 = Tangent3(pc, s);
                rings.Add(V.Count);
                foreach (var q in prof)
                {
                    V.Add(Point(pc, s, q.x - q.z * inset, q.y - q.w * inset));
                    var nn = Normal(pc, s, q.z, q.w) * Mathf.Cos(th) + t3 * dir * Mathf.Sin(th);
                    N.Add(nn.normalized); UV.Add(new Vector2(q.x, dist[r]));
                }
            }
            for (int r = 0; r < st.Count - 1; r++)
            {
                var list = (Mathf.FloorToInt((dist[r] + 0.001f) / Stripe) % 2 == 0) ? A : Bt;
                int ra = rings[r], rb = rings[r + 1];
                for (int i = 0; i < P - 1; i++) { int x0 = ra + i, x1 = ra + i + 1, y0 = rb + i, y1 = rb + i + 1; list.AddRange(new[] { x0, y0, x1, x1, y0, y1 }); }
                int bb = V.Count;
                foreach (var idx in new[] { ra, rb })
                {
                    V.Add(V[idx]); N.Add(Vector3.down); UV.Add(Vector2.zero);
                    V.Add(V[idx + P - 1]); N.Add(Vector3.down); UV.Add(Vector2.zero);
                }
                AddTriDir(V, list, bb, bb + 1, bb + 2, Vector3.down); AddTriDir(V, list, bb + 1, bb + 3, bb + 2, Vector3.down);
            }
            foreach (var (ri, sign) in new[] { (0, -1f), (st.Count - 1, 1f) })
            {
                int baseR = rings[ri]; float s = st[ri].x;
                int cb = V.Count; var nrm = Tangent3(pc, s) * sign;
                for (int i = 0; i < P; i++) { V.Add(V[baseR + i]); N.Add(nrm); UV.Add(Vector2.zero); }
                for (int i = 1; i < P - 1; i++) AddTriDir(V, A, cb, cb + i, cb + i + 1, nrm);
            }
        }
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV);
        mesh.subMeshCount = 2; mesh.SetTriangles(A, 0); mesh.SetTriangles(Bt, 1);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
    }

    static void BuildPathCollider(Mesh mesh, List<Piece> pieces)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        const float step = 0.5f;
        foreach (var pc in pieces)
        {
            float s0p = pc.s0, s1p = pc.s1;
            int n = Mathf.CeilToInt((s1p - s0p) / step);
            int first = V.Count;
            for (int k = 0; k <= n; k++)
            {
                float s = Mathf.Min(s1p, s0p + k * step), W = pc.w(s) * 0.5f;
                V.Add(Point(pc, s, -W, 0)); V.Add(Point(pc, s, W, 0)); V.Add(Point(pc, s, W, -Thick)); V.Add(Point(pc, s, -W, -Thick));
            }
            for (int k = 0; k < n; k++)
            {
                int a = first + k * 4, b = a + 4;
                for (int f = 0; f < 4; f++) { int f1 = (f + 1) % 4; T.AddRange(new[] { a + f, b + f, a + f1, a + f1, b + f, b + f1 }); }
            }
            int e = first + n * 4;
            T.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            T.AddRange(new[] { e, e + 2, e + 1, e, e + 3, e + 2 });
        }
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }

    // ---- 中心線にそった箱（横 latA〜latB、高さ yBot〜yTop）。見た目＋当たり ----
    static GameObject Sweep(Transform parent, string name, float s0, float s1, Func<float, float> latA, Func<float, float> latB,
        Func<float, float> yBot, Func<float, float> yTop, Material mat, int layer, bool collider, bool topOut = false)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        const float step = 0.5f;
        int n = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / step));
        Vector3 P(float s, float lat, float y) { Center(s, out var p, out var d); var q = p + Right(d) * lat; return new Vector3(q.x, y, q.y); }
        Vector3[] Ring(float s) { return new[] { P(s, latA(s), yTop(s)), P(s, latB(s), yTop(s)), P(s, latB(s), yBot(s)), P(s, latA(s), yBot(s)) }; }
        var rings = new List<Vector3[]>();
        for (int k = 0; k <= n; k++) rings.Add(Ring(Mathf.Lerp(s0, s1, k / (float)n)));
        Vector3[] outs = { Vector3.up, Vector3.zero, Vector3.down, Vector3.zero };
        for (int k = 0; k < n; k++)
        {
            var ra = rings[k]; var rb = rings[k + 1];
            var mid = (ra[0] + ra[1] + ra[2] + ra[3] + rb[0] + rb[1] + rb[2] + rb[3]) / 8f;
            for (int f = 0; f < 4; f++)
            {
                int f1 = (f + 1) % 4; int b = V.Count;
                V.Add(ra[f]); V.Add(ra[f1]); V.Add(rb[f1]); V.Add(rb[f]);
                var fc = (ra[f] + ra[f1] + rb[f1] + rb[f]) / 4f;
                AddTriDir(V, T, b, b + 1, b + 2, fc - mid); AddTriDir(V, T, b, b + 2, b + 3, fc - mid);
            }
        }
        foreach (var (r, sgn) in new[] { (rings[0], -1f), (rings[n], 1f) })
        {
            int b = V.Count; foreach (var q in r) V.Add(q);
            Center(sgn < 0 ? s0 : s1, out _, out var d); var want = new Vector3(d.x, 0, d.y) * sgn;
            AddTriDir(V, T, b, b + 1, b + 2, want); AddTriDir(V, T, b, b + 2, b + 3, want);
        }
        var mesh = new Mesh { name = "Course2_1_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/" + parent.name + "_" + name + ".asset");
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.layer = layer;
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
        if (topOut)
        {
            var to = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTopOut>(g);
            if (to) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(to); NoSync(to); }
        }
        return g;
    }

    // ---- 岩のふち（2026-10-26 てじゃ「コース全体で元の芝ブロックがじゃま」「上面が岩のメッシュで整えよう」）----
    // 水のふち・川岸の芝色の箱は当たり判定だけ残して見た目を消し、かわりに岩の土台と同じ色のごつごつしたふちを置く。
    // 内側（水の側）の面はまっすぐ下へ（水面とすき間ができないように）、上はでこぼこで少し丸く、外の面は少しふくらんで下へ
    // 箱を作ってから見た目を岩にする（当たり判定・アウト判定は前の箱のまま）
    static GameObject RockSweep(Transform parent, string name, float s0, float s1, Func<float, float> latIn, Func<float, float> latOut,
        Func<float, float> yBot, Func<float, float> yTop, int layer, bool topOut, bool low = false)
    {
        var box = Sweep(parent, name, s0, s1, s => Mathf.Min(latIn(s), latOut(s)), s => Mathf.Max(latIn(s), latOut(s)), yBot, yTop, RockBaseMat(), layer, true, topOut);
        box.GetComponent<Renderer>().enabled = false;
        var inner = new List<Vector2>(); var outN = new List<Vector2>(); var wid = new List<float>(); var top = new List<float>(); var bot = new List<float>();
        int n = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 0.35f));
        for (int k = 0; k <= n; k++)
        {
            float s = Mathf.Lerp(s0, s1, k / (float)n); Center(s, out var p, out var d); var r = Right(d);
            float a = latIn(s), b = latOut(s);
            inner.Add(p + r * a); outN.Add(r * Mathf.Sign(b - a == 0f ? 1f : b - a)); wid.Add(Mathf.Abs(b - a)); top.Add(yTop(s)); bot.Add(yBot(s));
        }
        RockRimVis(parent, name + "_Rock", inner, outN, wid, top, bot, name.GetHashCode() & 0xffff, low);
        return box;
    }
    // 横切りのふち（池の始まり・終わり）：s の所で latA〜latB、外は back（-1＝手前へ）
    static GameObject RockSweepAcross(Transform parent, string name, float sIn, float sOut, float latA, float latB, Func<float, float> yBot, float yTop, int layer, bool topOut)
    {
        var box = Sweep(parent, name, Mathf.Min(sIn, sOut), Mathf.Max(sIn, sOut), s => latA, s => latB, yBot, s => yTop, RockBaseMat(), layer, true, topOut);
        box.GetComponent<Renderer>().enabled = false;
        var inner = new List<Vector2>(); var outN = new List<Vector2>(); var wid = new List<float>(); var top = new List<float>(); var bot = new List<float>();
        Center(sIn, out var p, out var d); var r = Right(d); var o = d * Mathf.Sign(sOut - sIn);
        int n = Mathf.Max(1, Mathf.CeilToInt((latB - latA) / 0.35f));
        for (int k = 0; k <= n; k++)
        {
            float lat = Mathf.Lerp(latA, latB, k / (float)n);
            inner.Add(p + r * lat); outN.Add(o); wid.Add(Mathf.Abs(sOut - sIn)); top.Add(yTop); bot.Add(yBot(sIn));
        }
        RockRimVis(parent, name + "_Rock", inner, outN, wid, top, bot, name.GetHashCode() & 0xffff, false);
        return box;
    }
    const float RimRockDrop = 0.6f;
    static void RockRimVis(Transform parent, string name, List<Vector2> inner, List<Vector2> outN, List<float> wid, List<float> top, List<float> bot, int seed, bool low)
    {
        int n = inner.Count; float nOff = seed * 0.0137f;
        var rings = new List<Vector3[]>(); float along = 0f;
        for (int k = 0; k < n; k++)
        {
            if (k > 0) along += (inner[k] - inner[k - 1]).magnitude;
            float N(int j) => Mathf.PerlinNoise(nOff + along * 0.9f, j * 1.37f + 3.1f) - 0.5f;   // -0.5〜0.5
            // 下は RimRockDrop まで（2026-10-27 てじゃ「岩の上が蓋が開いてるような感じ」：深い水のふちの柱が土台の岩（RockBase）の下へつき出て、
            // 水の切りかわり（滝つぼ→川など）で柱のはしの面がぱっくり見えていた。その下は RockBase がつつむ）
            float w = wid[k], yt = top[k], yb = Mathf.Max(bot[k], yt - RimRockDrop);
            // 両はしは丸くすぼめる（つなぎ目でとがらないように、少し低く）
            float endK = Mathf.Clamp01(Mathf.Min(k, n - 1 - k) / 2f);
            // 両はしは下も浅く（となりの浅い水のふちとつながる所で、深いふちのはしの面が下につき出ないように）
            yb = Mathf.Max(yb, Mathf.Lerp(yt - 0.5f, yb, endK));
            float bump = low ? 0f : 0.06f;
            var pr = new (float x, float y)[]
            {
                (0f, yb),
                (0f, yt - (low ? 0.02f : 0.05f)),
                (w * 0.18f, yt + bump * (0.4f + N(1)) * endK),
                (w * 0.6f, yt + bump * (0.6f + N(2)) * endK - (1f - endK) * 0.04f),
                (w + 0.06f + N(3) * 0.08f, yt - 0.12f + N(4) * 0.04f),
                (w + 0.14f + N(5) * 0.12f, yt - 0.5f),
                (w + 0.04f, yb),
            };
            var ring = new Vector3[pr.Length];
            for (int j = 0; j < pr.Length; j++) { var q = inner[k] + outN[k] * pr[j].x; ring[j] = new Vector3(q.x, pr[j].y, q.y); }
            rings.Add(ring);
        }
        var V = new List<Vector3>(); var T = new List<int>();
        int m = rings[0].Length;
        for (int k = 0; k < n - 1; k++)
        {
            var ra = rings[k]; var rb = rings[k + 1];
            var mid = Vector3.zero; for (int j = 0; j < m; j++) mid += ra[j] + rb[j]; mid /= 2f * m;
            for (int j = 0; j < m; j++)
            {
                int j1 = (j + 1) % m; int b = V.Count;
                V.Add(ra[j]); V.Add(ra[j1]); V.Add(rb[j1]); V.Add(rb[j]);
                var fc = (ra[j] + ra[j1] + rb[j1] + rb[j]) / 4f;
                AddTriDir(V, T, b, b + 1, b + 2, fc - mid); AddTriDir(V, T, b, b + 2, b + 3, fc - mid);
            }
        }
        foreach (int k in new[] { 0, n - 1 })
        {
            var r = rings[k]; var c = Vector3.zero; foreach (var q in r) c += q; c /= m;
            var want = c - (k == 0 ? Avg(rings[1]) : Avg(rings[n - 2]));
            int ci = V.Count; V.Add(c);
            for (int j = 0; j < m; j++) { int b = V.Count; V.Add(r[j]); V.Add(r[(j + 1) % m]); AddTriDir(V, T, ci, b, b + 1, want); }
        }
        var mesh = new Mesh { name = "Course2_1_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/" + parent.name + "_" + name + ".asset");
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = RockBaseMat();
    }
    static Vector3 Avg(Vector3[] a) { var c = Vector3.zero; foreach (var q in a) c += q; return c / a.Length; }

    // ---- 水：水面の帯（流れの筋とふちの泡が帯にそう）・底・ふち・トリガーの箱の列 ----
    static void BuildWater(Transform parent, Water w, Material bedMat, Material rimMat, FuwaWaterFx fx, AudioSource splash)
    {
        var g = new GameObject(w.name).transform; g.SetParent(parent, false);
        float bed = w.surf - w.depth;
        float bodyBot = Mathf.Min(w.bodyBottom, bed - 1.3f);
        // 水面
        var V = new List<Vector3>(); var T = new List<int>(); var U1 = new List<Vector4>(); var U2 = new List<Vector2>(); var U3 = new List<Vector2>();
        const float step = 0.25f; const int across = 8;
        int n = Mathf.Max(1, Mathf.CeilToInt((w.s1 - w.s0) / step));
        for (int k = 0; k <= n; k++)
        {
            float s = Mathf.Lerp(w.s0, w.s1, k / (float)n), hw = w.hw(s);
            Center(s, out var p, out var d); var r = Right(d);
            for (int i = 0; i <= across; i++)
            {
                float lat = Mathf.Lerp(-hw, hw, i / (float)across);
                var q = p + r * (lat + w.off(s)); V.Add(new Vector3(q.x, w.surf, q.y));
                U1.Add(new Vector4(lat, hw, w.open0 ? 99f : s - w.s0, w.open1 ? 99f : w.s1 - s)); U2.Add(new Vector2(w.back ? -s : s, w.fade1 > 0f ? w.s1 - s : 99f));
                // 左のふちの切れ目（あふれ口）では、ふちの泡を消す（水がそのまま滝へこぼれるので。はしは 0.3m でなだらかに戻す）
                float nf = 0f;
                if (w.gapL1 > w.gapL0 && lat < 0f) nf = Mathf.Clamp01(Mathf.Min(s - w.gapL0, w.gapL1 - s) / 0.3f);
                U3.Add(new Vector2(nf, 0f));
            }
        }
        for (int k = 0; k < n; k++)
            for (int i = 0; i < across; i++)
            {
                int a = k * (across + 1) + i, b = a + across + 1;
                AddTriDir(V, T, a, b, a + 1, Vector3.up); AddTriDir(V, T, a + 1, b, b + 1, Vector3.up);
            }
        var sm = new Mesh { name = "Course2_1_" + w.name + "_Surface", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        sm.SetVertices(V); sm.SetTriangles(T, 0); sm.SetUVs(0, V.Select(v => Vector2.zero).ToList()); sm.SetUVs(1, U1); sm.SetUVs(2, U2); sm.SetUVs(3, U3);
        sm.normals = V.Select(v => Vector3.up).ToArray(); sm.RecalculateBounds();
        sm = Save(sm, MeshDir + "/" + w.name + "_Surface.asset");
        var surf = new GameObject("Surface"); surf.transform.SetParent(g, false);
        surf.AddComponent<MeshFilter>().sharedMesh = sm; surf.AddComponent<MeshRenderer>().sharedMaterial = WaterMat(w.flow, true, w.deep);
        if (w.deep)
        {
            // 浅い水の色（ふつうの水の材質と同じ）へ、終わりの fade1 m でなだらかに
            var dm = surf.GetComponent<MeshRenderer>().sharedMaterial; var sh = WaterMat(w.flow, true, false);
            dm.SetFloat("_OpenFade", w.fade1); dm.SetColor("_OpenDeep", sh.GetColor("_Deep")); dm.SetColor("_OpenShallow", sh.GetColor("_Shallow")); dm.SetFloat("_OpenDeepWidth", sh.GetFloat("_DeepWidth"));
            EditorUtility.SetDirty(dm);
        }
        // 底（地面＝ふわふわが触るとアウト）と、その下のかたまり（横に当たるだけ）
        var beds = new List<GameObject>();
        Func<float, float> bedA = s => w.off(s) - w.hw(s) - 0.01f, bedB = s => w.off(s) + w.hw(s) + 0.01f;
        Func<float, float> bodyA = s => w.off(s) - w.hw(Mathf.Clamp(s, w.s0, w.s1)) - (w.rimL ? RimW : 0f), bodyB = s => w.off(s) + w.hw(Mathf.Clamp(s, w.s0, w.s1)) + (w.rimR ? RimW : 0f);
        float b0 = w.s0 - (w.open0 ? 0f : RimW), b1 = w.s1 + (w.open1 ? 0f : RimW);
        if (w.holeL == null)
        {
            beds.Add(Sweep(g, "Bed", w.s0, w.s1, bedA, bedB, s => bed - 0.3f, s => bed, bedMat, 11, true));
            Sweep(g, "Body", b0, b1, bodyA, bodyB, s => bodyBot, s => bed - 0.3f, rimMat, 0, true, true);
        }
        else
        {
            // 穴の前後はふつうの底、穴の所は左右の帯と、下がった穴の底。下のかたまりも同じように分けて、穴の下だけ深くまで
            float bodyHole = Mathf.Min(bodyBot, w.holeBed - 1.3f);
            Func<float, float> hl = s => w.off(s) - w.holeL(s), hr = s => w.off(s) + w.holeR(s);
            beds.Add(Sweep(g, "Bed", w.s0, w.hole0, bedA, bedB, s => bed - 0.3f, s => bed, bedMat, 11, true));
            beds.Add(Sweep(g, "Bed2", w.hole1, w.s1, bedA, bedB, s => bed - 0.3f, s => bed, bedMat, 11, true));
            beds.Add(Sweep(g, "BedL", w.hole0, w.hole1, bedA, hl, s => bed - 0.3f, s => bed, bedMat, 11, true));
            beds.Add(Sweep(g, "BedR", w.hole0, w.hole1, hr, bedB, s => bed - 0.3f, s => bed, bedMat, 11, true));
            beds.Add(Sweep(g, "HoleBed", w.hole0, w.hole1, hl, hr, s => w.holeBed - 0.3f, s => w.holeBed, bedMat, 11, true));
            Sweep(g, "Body", b0, w.hole0, bodyA, bodyB, s => bodyBot, s => bed - 0.3f, rimMat, 0, true, true);
            Sweep(g, "Body2", w.hole1, b1, bodyA, bodyB, s => bodyBot, s => bed - 0.3f, rimMat, 0, true, true);
            Sweep(g, "BodyL", w.hole0, w.hole1, bodyA, hl, s => bodyBot, s => bed - 0.3f, rimMat, 0, true, true);
            Sweep(g, "BodyR", w.hole0, w.hole1, hr, bodyB, s => bodyBot, s => bed - 0.3f, rimMat, 0, true, true);
            Sweep(g, "BodyHole", w.hole0, w.hole1, hl, hr, s => bodyHole, s => w.holeBed - 0.3f, rimMat, 0, true, true);
        }
        // 浅瀬は全部（2026-10-23 浅くしたので、流されたふわふわが底に当たりやすい）
        if (!w.deep && w.bedBounce <= 0f) w.bedBounce = ChanBedBounce;
        if (w.bedBounce > 0f)
            foreach (var bedGo in beds)
            {
                // 地面のレイヤーのまま（真下の道として覚える＝道の外でアウト、にならない）。当たった時だけ跳ねる
                var bb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBounce>(bedGo);
                bb.bounceSpeed = w.bedBounce; bb.keepTangent = 0.8f; bb.alwaysUp = false; bb.useContactNormal = true;
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bb); NoSync(bb);
            }
        // ふち（上に乗るとアウト）
        float rimTop = w.surf + RimUp;
        if (w.rimL && w.gapL1 > w.gapL0)
        {
            // 切れ目は水面すれすれの低いふち（水があふれて流れ落ちて見える）
            RockSweep(g, "RimL", w.s0, w.gapL0, s => w.off(s) - w.hw(s), s => w.off(s) - w.hw(s) - RimW, s => bed - 0.3f, s => rimTop, 0, true);
            RockSweep(g, "RimL2", w.gapL1, w.s1, s => w.off(s) - w.hw(s), s => w.off(s) - w.hw(s) - RimW, s => bed - 0.3f, s => rimTop, 0, true);
            RockSweep(g, "RimL_Lip", w.gapL0, w.gapL1, s => w.off(s) - w.hw(s), s => w.off(s) - w.hw(s) - RimW, s => bed - 0.3f, s => w.surf - 0.04f, 0, true, true);
        }
        else if (w.rimL) RockSweep(g, "RimL", w.s0, w.s1, s => w.off(s) - w.hw(s), s => w.off(s) - w.hw(s) - RimW, s => bed - 0.3f, s => rimTop, 0, true);
        if (w.rimR) RockSweep(g, "RimR", w.s0, w.s1, s => w.off(s) + w.hw(s), s => w.off(s) + w.hw(s) + RimW, s => bed - 0.3f, s => rimTop, 0, true);
        if (!w.open0) RockSweepAcross(g, "RimStart", w.s0, w.s0 - RimW, w.off(w.s0) - w.hw(w.s0) - (w.rimL ? RimW : 0f), w.off(w.s0) + w.hw(w.s0) + (w.rimR ? RimW : 0f), s => bed - 0.3f, rimTop, 0, true);
        if (!w.open1) RockSweepAcross(g, "RimEnd", w.s1, w.s1 + RimW, w.off(w.s1) - w.hw(w.s1) - (w.rimL ? RimW : 0f), w.off(w.s1) + w.hw(w.s1) + (w.rimR ? RimW : 0f), s => bed - 0.3f, rimTop, 0, true);
        // 水面の高さの目印（FuwaWater が見る）
        var sy = new GameObject("SurfaceY").transform; sy.SetParent(g, false); sy.localPosition = new Vector3(0, w.surf, 0);
        // トリガー：中心線にそって 2.5m 以下の向きつきの箱を並べる（流れの向き＝箱の +Z）
        var areas = new List<BoxCollider>();
        int nseg = Mathf.Max(1, Mathf.CeilToInt((w.s1 - w.s0) / 2.5f));
        for (int k = 0; k < nseg; k++)
        {
            float a = Mathf.Lerp(w.s0, w.s1, k / (float)nseg), b = Mathf.Lerp(w.s0, w.s1, (k + 1) / (float)nseg), m = (a + b) * 0.5f;
            // 横の範囲（ずれ＋幅）をこの区間で調べて、その真ん中の線で箱を並べる（横にずれた水路を中心線の長さで並べると、
            // カーブの外側で箱のあいだにすき間ができて、ふわふわが浮かずに沈んだ。えぐれで幅が変わる所も全部おおう）
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i <= 8; i++) { float si = Mathf.Lerp(a, b, i / 8f); lo = Mathf.Min(lo, w.off(si) - w.hw(si)); hi = Mathf.Max(hi, w.off(si) + w.hw(si)); }
            float latc = (lo + hi) * 0.5f, hwm = (hi - lo) * 0.5f;
            Center(a, out var pa, out var da); Center(b, out var pb, out var db);
            pa += Right(da) * latc; pb += Right(db) * latc;
            var seg = new GameObject("Seg" + k); seg.transform.SetParent(g, false); seg.layer = 4;
            var mid2 = (pa + pb) * 0.5f;
            seg.transform.localPosition = new Vector3(mid2.x, (bed + w.surf) / 2f, mid2.y);
            seg.transform.localRotation = Quaternion.LookRotation(new Vector3(pb.x - pa.x, 0, pb.y - pa.y));
            var bc = seg.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(hwm * 2f, w.surf - bed, (pb - pa).magnitude + 0.02f);
            var fw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWater>(seg);
            fw.surface = sy; fw.flowDirection = w.back ? Vector3.back : Vector3.forward; fw.flowSpeed = w.flow; fw.fx = fx;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fw); NoSync(fw);
            areas.Add(bc);
        }
        if (w.deep)
        {
            // 深い水：水面の少し下から底までの人用の落下判定（Default レイヤーのトリガー。Water レイヤーだと人のトリガーが来ないかもしれないので分ける）
            var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
            foreach (var a in areas)
            {
                var c = new GameObject("PlayerFall"); c.transform.SetParent(a.transform, false); c.layer = 0;
                var bc = c.AddComponent<BoxCollider>(); bc.isTrigger = true;
                float top = 0.15f;   // 水面からこれだけ下で落ちた判定（足がつかる程度では反応しない…けど深いので歩いては入れない）
                bc.size = new Vector3(a.size.x, w.depth - top, a.size.z);
                bc.center = new Vector3(0, -top * 0.5f, 0);   // 区切りの箱（中心＝水の厚みのまん中）の、底から水面の top 下まで
                var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(c);
                pc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); NoSync(pc);
            }
            return;
        }
        // 人には浅瀬（1つで全部の箱を見る）
        var sw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaShallowWater>(g.gameObject);
        sw.fx = fx; sw.splashSound = splash; sw.areas = areas.ToArray();
        sw.softClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_water_splash_soft.wav");
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sw); NoSync(sw);
    }

    static GameObject Box(Transform p, string name, Vector3 lp, Vector3 size, Material m, int layer)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false);
        g.transform.localPosition = lp; g.transform.localScale = size; g.layer = layer;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    // 帯の水面（_Ribbon=1）は流れの速さごとに1つ。四角い板の水面（滝つぼ）は水のテスト場と同じ材質
    // 深い水（人は落ちたらアウト）は濃い色（2026-10-19 てじゃ「アウトのエリアの色を濃くしよう」）
    public static Color DeepCenter = new Color(0.07f, 0.30f, 0.62f), DeepEdge = new Color(0.20f, 0.50f, 0.80f);
    static Material WaterMat(float flowSpeed, bool ribbon, bool deep = false)
    {
        string p = "Assets/FuwaStage2/Materials/Stage2_Water" + (ribbon ? "Ribbon_" : "_") + (deep ? "Deep_" : "") + (flowSpeed > 0.001f ? "Flow" + flowSpeed.ToString("0.0") : "Still") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var sh = Shader.Find("FuwaStage2/Water");
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh; m.SetFloat("_FlowSpeed", flowSpeed); m.SetFloat("_Ribbon", ribbon ? 1f : 0f);
        if (deep) { m.SetColor("_Deep", DeepCenter); m.SetColor("_Shallow", DeepEdge); m.SetFloat("_DeepWidth", 1.2f); }
        EditorUtility.SetDirty(m);
        return m;
    }

    // 作り直すと Udon の付いた物が消えて新しくなるので、シーンのネットワークIDに消えた物（InvalidObject）が残ってビルドが止まる
    //（2026-10-21「Build was blocked by the SDK callback」）。SDK のチェック（ConfigureNetworkIDs）を回すと消えた物を外して新しい物に番号をつける。
    // 1回目で直して、2回目でエラー0を確かめる
    static void FixNetworkIDs()
    {
        var desc = Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>(); if (!desc) return;
        var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VRC.SDKBase.Network.NetworkIDAssignment")).FirstOrDefault(x => x != null); if (t == null) return;
        var m = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).FirstOrDefault(x => x.Name == "ConfigureNetworkIDs"); if (m == null) return;
        var setErrT = m.GetParameters()[2].ParameterType.GetElementType();
        int left = 0;
        for (int i = 0; i < 2; i++)
        {
            var args = new object[] { desc, null, Array.CreateInstance(setErrT, 0) };
            m.Invoke(null, args);
            left = 0; foreach (var e in (System.Collections.IEnumerable)args[1]) left++;
        }
        EditorUtility.SetDirty(desc);
        if (left > 0) Debug.LogWarning("[2–1] ネットワークIDのエラーが " + left + " 個残っています");
    }

    static void NoSync(UdonSharp.UdonSharpBehaviour b)
    {
        var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        if (ub) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
    }

    // 既存のアセットがあれば中身だけ入れ替える（参照が外れないように）
    static Mesh Save(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null)
        {
            old.Clear(false); FuwaBevelTools.CopyInto(old, m);
            for (int ch = 1; ch <= 3; ch++) { var u = new List<Vector4>(); m.GetUVs(ch, u); if (u.Count == m.vertexCount) old.SetUVs(ch, u); }   // CopyInto は uv0 だけ（水面の帯は uv1/uv2、あふれ口の泡消しは uv3 を使う）
            old.UploadMeshData(false); EditorUtility.SetDirty(old); return old;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
