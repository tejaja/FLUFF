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
//  ② 池からうねる川（流れあり）でふわふわが流される。人は右の岸を追いかける。下流の渡り場で右岸の道が川で切れていて、
//     待ちかまえて流れてきたふわふわを左岸の道の続きへ撃ってすくい上げる。人は川をじゃぶじゃぶ渡る。
//     間に合わないと川の終わりの崖の滝から谷へ落ちてアウト。左岸の道は谷を小さい橋で越える（2026-10-20 てじゃ。前は橋＋橋の下のトゲ）
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
    const float FinCurtain = 3.7f, FinCurtainTop = 7f;         // よけられない滝のカーテンの位置と落ち口の高さ
    const float FinRiver0 = 3.5f, FinRiverSurf = -3f, FinRiverHW = 2.2f, FinRiverFlow = 1.2f;   // 下の川
    const float FinAfterBend = 8f;                             // カーブのあとの川の長さ（その先は滝）
    const float FinCliffLen = 6f;                              // 川の始まりの右の崖（カーテンの右を回り込めないように）の長さ
    const float FinWalkW = 1.8f, FinDownEnd = 12f;             // 左岸の人の道の幅と、踊り場から下りきる所（そこからはまっすぐゴールの島へ）
    const float FinBankW = 0.6f;                               // 右岸の細い地面
    const float FinGateH = 2.6f, FinWallOut = 9.5f;            // 滝の左の岩の壁：人のくぐり口の高さ、壁の左のはし（中心線から）
    const float FinGoalOut = 22f, FinGoalUp = BankTop;   // 滝の道のアウトの終わり（踊り場から）、ゴールの島の川の水面からの高さ（2026-10-23 てじゃ「ゴール、川岸と同じくらいの高さにしよ」1.6→川岸）
    // 左岸の人の道の高さ：踊り場の高さから川岸の高さへ下りる
    // 左岸の道の高さ：踊り場から川岸の高さまで下りる（川に落ちた人が上がれるように。2026-10-23）。カーブの所から先はゴールの島の高さへ登る
    static float LeftBankH(float s) { return Mathf.Lerp(FallsH, FallsH + FinRiverSurf + BankTop, Mathf.Clamp01((s - sF1 - FinLand) / (FinDownEnd - FinLand))); }
    static float WalkToGoalH(float s) { return Ramp(s, sBend0, sWalkEnd - 0.6f, FallsH + FinRiverSurf + BankTop, goalH); }
    static float goalH, sBend0, sBend1, sFin1;   // ゴールの島の上の面、川のカーブの始まり・終わり、川の終わり（滝）
    static Vector2 goalC, goalD, walkP0; static float sWalkEnd;   // walkP0＝左岸の道のまっすぐな所の始まり（道の中心）、向きは goalD   // ゴールの島の中心・向き（入口の逆）、左岸の道の終わり
    // 洞窟：床＝StartH、天井の高さ CaveIn、壁の外側 CaveHW（道の幅6の外に厚み0.4）、口の半分の幅 MouthHW。上に屋根の板、その上が水源の池
    public const float CaveIn = 2.6f, CaveHW = 3.4f, MouthHW = 1.7f, RoofT = 0.3f, SideGapLen = 3.4f, SideGapH = 2.4f;
    public const float FallsReach = 1.6f;   // 滝が落ち口からどれだけ前に落ちるか(m)。2026-10-23 2.8→1.6（てじゃ「ちょと遠くてふわふわが届かないことがおおい」）
    public static float SourceSurf { get { return StartH + CaveIn + RoofT + 0.3f + WaterDepth; } }

    // 中心線の点（コースのローカル x,z）。最初と最後はカーブの向きを決めるだけの点
    static readonly Vector2[] Ctrl =
    {
        new Vector2(0, -6), new Vector2(0, 0), new Vector2(0, 6), new Vector2(-2, 13), new Vector2(-6, 20), new Vector2(-5, 28),
        new Vector2(1, 34), new Vector2(7, 40), new Vector2(8, 48), new Vector2(4, 55), new Vector2(-2, 61), new Vector2(-5, 68),
        new Vector2(-3, 76), new Vector2(2, 82), new Vector2(5, 89), new Vector2(5, 97),
        // ⑤ 滝の道（ぐねぐね。カーブ 15.1 / 17.05 / 19 に滝と水路）→ ⑥ 登ってゴール
        new Vector2(0, 103), new Vector2(-4, 109), new Vector2(-1, 116), new Vector2(4, 122), new Vector2(2, 129), new Vector2(0, 136),
        // ⑥ ラスト：よけられない滝 → 下の川を流される。川は (0,143) から右へ半径6mで80°曲がって、まっすぐ滝へ。
        // 曲がった先の外側の岸にゴールの島（2026-10-23 てじゃのスケッチ「川を曲げる、島を曲がった先の川岸におく、川の先は滝」）
        new Vector2(0, 143), new Vector2(1.404f, 146.857f), new Vector2(4.958f, 148.909f),
        new Vector2(10.867f, 149.951f), new Vector2(16.776f, 150.993f), new Vector2(22.685f, 152.035f), new Vector2(28.594f, 153.077f),
    };
    const int BendCtrl0 = 22, BendCtrl1 = 24;   // カーブの始まり・終わりの点（Ctrl の番号）

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
    // 川の渡り場（右岸の道が川で切れて、人は川をじゃぶじゃぶ渡って左岸の道へ）は川の終わりの CrossBefore m 手前。
    // 川の終わりは崖の滝で、その先は谷（GorgeLen m、落ちたらアウト）。左岸の道は谷を小さい橋で越える
    public const float CrossBefore = 9f, CrossGap = 2.4f, GorgeLen = 5.5f, GorgeDrop = 10f;
    static float sCross;
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
        // 流れを中心線の逆向き（s が減る向き）に
        public bool back;
    }

    static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
    static Func<float, float> Smoothed(Func<float, float> f, float r)
    {
        return s => { float sum = 0; int n = 9; for (int i = 0; i < n; i++) sum += f(s + Mathf.Lerp(-r, r, i / (float)(n - 1))); return sum / n; };
    }
    static float Ramp(float s, float a, float b, float va, float vb) { return Mathf.Lerp(va, vb, Smooth01((s - a) / (b - a))); }

    // 滝壺：滝の幅から急に広がる（ふくらみは手前寄り）。岸の道から向こう岸まではダッシュジャンプで届かない広さ（てじゃ「ここぐらいまでは届いちゃう、こっちルートは防ぎたい」）
    static float PoolHW(float s) { float t = Mathf.InverseLerp(sPlaza1, sPool1, s); return Mathf.Lerp(CaveHW - RimW, 1.8f, t) + 4.3f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(t), 0.65f))), 0.9f); }
    static float RiverHW(float s) { float t = s - sPool1; return 1.8f + 0.25f * Mathf.Sin(t * 0.45f) - 0.4f * Smooth01((s - (sRiver1 - 2.5f)) / 2.5f); }
    // 蓮の池：まるっこい形（だ円より少しふっくら）。入口・出口は道の幅の口（2026-10-20 てじゃ「池自体もっと丸っこくしたいな」。前は sin で両はしがとがったレンズ形）
    static float PondHW(float s) { float u = Mathf.Abs(Mathf.InverseLerp(sPond0, sPond1, s) * 2f - 1f); return Mathf.Max(1.4f, 6.2f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(u, 2.3f)), 1f / 2.3f)); }
    // 貝の道：道の半分の幅、貝の置き場所（道のふちから外へ）、両わきの滝（道のふちから外へ）。sWalk0/1 と WalkH は Build で決める
    public static float WalkHW = 1.7f, ClamOut = 0.6f, FallsOut = 2.6f;
    static float sWalk0, sWalk1;
    static Func<float, float> WalkH;
    // 滝の道：道の半分の幅、セットの場所（中心線の u＝カーブのあたり）
    public static float RoadHW = 1.5f;
    public static float[] FallsSetU = { 15.1f, 17.05f, 19f };
    // 滝が道の外側から食い込む深さ（そこは道がえぐれて水路。残る道の幅＝2×RoadHW−これ）。後ろほど深く
    public static float[] CutDepth = { 1.2f, 1.5f, 1.8f };
    // 水路の、滝より下流の長さ（ここで撃ってすくい上げる。滝の横ですくうと、また滝に当たって押し戻される）。後ろほど短く
    public static float[] ChanAfter = { 5f, 4f, 3f };
    // 水路の半分の幅（えぐれていない所）・道からの水面の低さ・流れ・深さ、滝の高さ（水の出どころはあとで。今は空中から）
    // 2026-10-22 てじゃ「深い水だと救出できないから浅くする」「下の水に飛び込んだあとにジャンプで戻れるくらいの高さに」：
    // 前は 深さ2m・水面が道の0.9m下（人は落ちたらアウト）。今は浅瀬（人は入れる）で、底から道の上まで 0.4m（ジャンプ約0.46m で上がれる）。
    // 浅いと滝に押されたふわふわが底に当たるので、底はアウトにせずぽよんと戻す（ChanBedBounce）
    public static float ChanDepth = 0.28f, ChanHW = 1.1f, ChanDrop = 0.12f, ChanFlow = 1.3f, OverFallsH = 6f, ChanBedBounce = 1.5f;
    // 水路の流れを来た方（スタート側）へ（2026-10-22 てじゃ「流れを手前にする」）。水路の長い側・端の滝・道のえぐれの形も前後反転する
    public static bool FallsFlowBack = true;
    static float sF0, sF1, FallsH;
    static float[] setS, setO;   // セットの中心線の位置と、えぐる側（+1＝右）
    // 蓮の池の左のふちの、水があふれて流れ落ちる所（まん中の大きい蓮の花をよけて少し先）
    static float PondSpill() { return (sPond0 + sPond1) * 0.5f + 4f; }
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
        sPool1 = SI(3.8f); sRiver1 = SI(7.3f);
        sCross = sRiver1 - CrossBefore;
        sPond0 = SI(8.9f); sPond1 = SI(10.9f);
        sTer0 = SI(11.8f);
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
        pieces.Add(new Piece { name = "M1_Start", s0 = sPlaza0 - 1.5f, s1 = sPlaza1, off = s => 0f, h = s => StartH, w = s => 6f, round0 = false });   // 後ろは洞窟の入口の敷居とつながる
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
            // 洞窟の中（右半分）から右の壁の脇道を抜けて外へ
            if (s < sPlaza1) return Mathf.Lerp(1.5f, 4.7f, Smooth01((s - (sPlaza1 - 4.6f)) / 4.1f));
            return Mathf.Max(Mathf.Lerp(4.7f, 3.4f, Smooth01((s - sPlaza1) / 6f)), WaterHW(Mathf.Min(s, sRiver1)) + RimW + 1.55f);
        };
        Func<float, float> b2t = s => -Mathf.Lerp(bankOff(s), 0f, Smooth01((s - mergeA) / (mergeB - mergeA)));
        Func<float, float> b1h = s => Ramp(s, sPlaza1 + 0.3f, sPlaza1 + 10f, StartH - 0.01f, BankTop - 0.01f);
        pieces.Add(new Piece { name = "B1_Bank", s0 = sPlaza1 - 4.6f, s1 = sCross - CrossGap * 0.5f, off = Smoothed(b1t, 1.2f), w = s => 3f, h = b1h });
        pieces.Add(new Piece { name = "B2_Bank", s0 = sCross + CrossGap * 0.5f, s1 = mergeB, off = Smoothed(b2t, 1.0f), w = s => 3f, h = s => BankTop - 0.01f });
        // M2 谷の向こう → 蓮の池の手前（本道は水のふちの外側にぴったりつける。端は丸めない）
        pieces.Add(new Piece { name = "M2", s0 = mergeA, s1 = sPond0 - RimW, off = s => 0f, w = s => 3f, h = s => BankTop, round0 = false, round1 = false });
        // M3 貝の道：蓮の池の向こうから、細い堤の道をゆるく登って大きな滝の登り坂へ（両わきは崖の外＝落ちたらアウト）
        float topSurf = 2 * TerraceStep;
        sWalk0 = sPond1 + RimW; sWalk1 = b2 + RimW;
        float walkTop = topSurf + BankTop;
        WalkH = s => Ramp(s, sWalk0 + 1.5f, sWalk1 - 1.5f, BankTop, walkTop);
        pieces.Add(new Piece { name = "M3_ClamWalk", s0 = sWalk0, s1 = sWalk1, off = s => 0f, w = s => WalkHW * 2f, h = WalkH, round0 = false, round1 = false });
        // M4 滝の道：貝の道の高さのまま、ぐねぐね。最後のセットの水路の先まで
        sF0 = sWalk1; FallsH = walkTop;
        setS = new float[FallsSetU.Length]; setO = new float[FallsSetU.Length];
        for (int i = 0; i < FallsSetU.Length; i++) { setS[i] = SI(FallsSetU[i]); setO[i] = OuterSide(setS[i]); }
        { int last = FallsSetU.Length - 1; sF1 = setS[last] + NotchEnd + ChanAfter[last] + 3f; }
        sGoal0 = sF1 + FinGoalOut; goalH = FallsH + FinRiverSurf + FinGoalUp;
        sBend0 = SI(BendCtrl0); sBend1 = SI(BendCtrl1); sFin1 = sBend1 + FinAfterBend;
        GoalSpot();
        pieces.Add(new Piece { name = "M4_FallsRoad", s0 = sF0, s1 = sF1, off = s => (Cut(s, -1f) - Cut(s, 1f)) * 0.5f, w = s => RoadW(s) - Cut(s, -1f) - Cut(s, 1f), h = s => FallsH, round0 = false, round1 = false, centerLat = true });
        // M5 踊り場 → M6 左の坂（川岸へ下りる）→ M7 左岸の道（人が走って追いかける）。川が右へ曲がる所から先は、まっすぐゴールの島の入口へ
        pieces.Add(new Piece { name = "M5_Landing", s0 = sF1, s1 = sF1 + FinLand, off = s => -2f, w = s => 8.4f, h = s => FallsH, round0 = false, round1 = false });
        pieces.Add(new Piece { name = "M6_Down", s0 = sF1 + FinLand, s1 = sF1 + FinDownEnd, off = s => -FinRiverHW - FinWalkW * 0.5f, w = s => FinWalkW, h = LeftBankH, round0 = false, round1 = false });
        {
            Center(sF1 + FinDownEnd, out var wp, out var wd); var wq = wp + Right(wd) * (-FinRiverHW - FinWalkW * 0.5f); float ws = sF1 + FinDownEnd;
            pieces.Add(new Piece { name = "M7_LeftBank", s0 = ws, s1 = sWalkEnd, off = s => 0f, at = s => wq + wd * (s - ws), w = s => FinWalkW, h = WalkToGoalH, round0 = false, round1 = false });
        }
        // 右岸のゴールの広場（M8_GoalPlaza）は 2026-10-23 にやめて、ゴールの島（GoalSet）に

        var pm = new GameObject("PathMesh"); pm.transform.SetParent(root, false); pm.layer = pathLayer;
        var vis = new Mesh { name = "Course2_1Path_Bevel" }; BuildPathVisual(vis, pieces);
        var col = new Mesh { name = "Course2_1Path" }; BuildPathCollider(col, pieces);
        vis = Save(vis, MeshDir + "/Course2_1Path_Bevel.asset"); col = Save(col, MeshDir + "/Course2_1Path.asset");
        pm.AddComponent<MeshFilter>().sharedMesh = vis;
        pm.AddComponent<MeshRenderer>().sharedMaterials = new[] { pathA, pathB };
        pm.AddComponent<MeshCollider>().sharedMesh = col;
        // 道の横の面にふわふわが当たってもアウトにしない（上に乗ったらアウト）。川から撃ち上げる時に道のふちにかすってアウト、をなくす
        //（2026-10-23 てじゃ「川から脱出するときに道の縁にあたってアウトになることあるかも」）
        { var to = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTopOut>(pm); if (to) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(to); NoSync(to); } }

        // ---- 水 ----
        var fx = FuwaStage2WaterTest.MakeFx(root);
        var ps0 = new GameObject("PlayerSplash"); ps0.transform.SetParent(root, false);
        var splash = ps0.AddComponent<AudioSource>(); splash.playOnAwake = false; splash.spatialBlend = 1f;
        splash.minDistance = 2f; splash.maxDistance = 25f; splash.rolloffMode = AudioRolloffMode.Linear;
        splash.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaStage2/Audio/sfx_water_splash.wav");
        var waters = new List<Water>
        {
            new Water { name = "W0_Source", s0 = sPlaza0 - 1.5f, s1 = sPlaza1, surf = SourceSurf, flow = 0.8f, open1 = true, bodyBottom = StartH + CaveIn + RoofT,
                hw = s => Mathf.Lerp(CaveHW - RimW, MouthHW, Smooth01((s - (sPlaza1 - 3.5f)) / 3.5f)) },
            new Water { name = "W1_Pool", s0 = sPlaza1, s1 = sPool1, surf = 0f, flow = 0.9f, hw = PoolHW, open0 = true, open1 = true, deep = true, depth = 2.5f },
            // 終わりは崖の滝（open1）。下のかたまりを深くして、谷から見て崖に見えるように
            new Water { name = "W2_River", s0 = sPool1, s1 = sRiver1, surf = 0f, flow = 1.1f, hw = RiverHW, open0 = true, open1 = true, bodyBottom = -6f },   // 1.6→1.1：浅瀬を歩く人が置いていかれないように
            new Water { name = "W3_LilyPond", s0 = sPond0, s1 = sPond1, surf = 0f, flow = 0f, hw = PondHW, gapL0 = PondSpill() - 1.1f, gapL1 = PondSpill() + 1.1f },
        };
        var wroot = new GameObject("Water").transform; wroot.SetParent(root, false);
        foreach (var w in waters) BuildWater(wroot, w, bedMat, ground, fx, splash);

        // 渡り場の石段は 2026-10-23 にやめた（浅くしてどこからでも上がれるようになった。てじゃ「ふわふわぶつかるトラップになってる」）
        // 川の終わりの崖の滝 → 谷（下は何もない。ふわふわも人も落ちたらアウト）
        CurvedFalls(root, "Falls_River", sRiver1, 0.02f, -GorgeDrop, RiverHW(sRiver1) * 2f + 0.4f, 1.6f, 0.4f);
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

        // ④ 貝の道
        ClamWalk(root, ground);
        // ⑤ 滝の道
        FallsRoad(root, ground, bedMat, fx, splash);

        // ---- 地形のかたまり（ブロックアウト）----
        var terr = new GameObject("Terrain").transform; terr.SetParent(root, false);
        // ① スタートの洞窟（崖）：床の下・左右の壁・奥の壁・口の両わき・屋根。右の壁は脇道のすき間をあける
        float cs0 = sPlaza0 - 1.5f, cIn = StartH + CaveIn, wallB = StartH - Thick, gapA = sPlaza1 - 0.4f - SideGapLen, gapB = sPlaza1 - 0.4f;
        Sweep(terr, "CaveFloorCliff", cs0 - 0.4f, sPlaza1, s => -CaveHW, s => CaveHW, s => Base, s => wallB - 0.01f, ground, 0, true);
        // 奥の壁：まん中に人が歩いて入る入口（2026-10-23 スタートの島から木の橋で入ってくる）。入口の下は敷居
        Sweep(terr, "CaveBackL", cs0 - 0.4f, cs0, s => -CaveHW, s => -DoorHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveBackR", cs0 - 0.4f, cs0, s => DoorHW, s => CaveHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveBackLintel", cs0 - 0.4f, cs0, s => -DoorHW, s => DoorHW, s => StartH + DoorH, s => cIn, ground, 0, true);
        Sweep(terr, "CaveDoorSill", cs0 - 0.4f, cs0, s => -DoorHW, s => DoorHW, s => wallB, s => StartH, ground, pathLayer, true);
        Sweep(terr, "CaveWallL", cs0, sPlaza1, s => -CaveHW, s => -3f, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveWallR_Back", cs0, gapA, s => 3f, s => CaveHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveWallR_Lintel", gapA, gapB, s => 3f, s => CaveHW, s => StartH + SideGapH, s => cIn, ground, 0, true);
        Sweep(terr, "CaveMouthL", gapB, sPlaza1, s => -3f, s => -MouthHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveMouthR", gapB, sPlaza1, s => MouthHW, s => CaveHW, s => wallB, s => cIn, ground, 0, true);
        Sweep(terr, "CaveRoof", cs0 - 0.4f, sPlaza1, s => -CaveHW, s => CaveHW, s => cIn, s => cIn + RoofT, ground, 0, true);
        // 脇道のツタのカーテン：右の壁のすき間いっぱい（2026-10-22 てじゃ「入口のクモの巣もこれにすれば分かるか」→ ラストの滝の左とそろえる）
        {
            float gm = (gapA + gapB) * 0.5f;
            Center(gm, out var wp, out var wd); var wr = Right(wd);
            var wq = wp + wr * (3f + (CaveHW - 3f) * 0.5f);
            VineCurtain(root, "SideVines", new Vector3(wq.x, StartH + SideGapH, wq.y), new Vector3(wd.x, 0, wd.y), gapB - gapA, SideGapH, 2101);
        }
        // ① の滝：水源の池から、洞窟の口の前を下の池へ
        Center(sPlaza1, out var fp, out var fd);
        // 落ち口はRで前へ張り出して、洞窟の口から少し離れた所に落ちる（ふわふわが崖ぎわのふちに叩きつけられないように）。厚みもつける
        // 2026-10-19 てじゃ「滝にあたったふわふわが地面に激突するから、ちょっとRつけて遠くに落ちるように、厚みもほしい」
        CurvedFalls(root, "Falls_Start", sPlaza1, SourceSurf + 0.02f, 0f, MouthHW * 2f + 0.4f, FallsReach, 0.45f);
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
            float dMid = (sPond0 + sPond1) * 0.5f, ea = (sPond1 - sPond0) * 0.5f - PondShallowW, eb = PondHW(dMid) - PondShallowW;
            Func<float, float> dhw = s => { float u = (s - dMid) / ea; return u * u >= 1f ? 0f : Mathf.Min(PondHW(s) - PondShallowW, eb * Mathf.Sqrt(1f - u * u)); };
            float d0 = dMid - ea, d1 = dMid + ea;
            // 外周を少しゆがませる：左右べつべつに、ゆるい波を足す（はしで 0 になる掛け算なので丸いはしは保つ）。浅瀬は最低 1.6m 残す
            //（2026-10-20 てじゃ「濃い部分の外周をちょっと歪ませて」）
            float Wob(float s, float ph) { float x = (s - d0) / Mathf.Max(0.01f, d1 - d0); return 0.16f * Mathf.Sin(x * 9.4f + ph) + 0.09f * Mathf.Sin(x * 17.3f + ph * 2.3f); }
            Func<float, float> hwL = s => Mathf.Min(PondHW(s) - 1.6f, dhw(s) * (1f + Wob(s, 0.7f)));
            Func<float, float> hwR = s => Mathf.Min(PondHW(s) - 1.6f, dhw(s) * (1f + Wob(s, 2.9f)));
            {
                var V = new List<Vector3>(); var T = new List<int>(); var U1 = new List<Vector4>(); var U2 = new List<Vector2>();
                const int across = 10; int n = Mathf.Max(1, Mathf.CeilToInt((d1 - d0) / 0.2f));
                for (int k = 0; k <= n; k++)
                {
                    float kk = k / (float)n, s = dMid + ea * -Mathf.Cos(kk * Mathf.PI);   // はしほど細かく（丸く見えるように）
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
                if (hwm < 0.5f) continue;
                Center(a, out var pa, out _); Center(b, out var pb, out var db);
                var c = new GameObject("PlayerFall" + k); c.transform.SetParent(dg, false);
                var mid = (pa + pb) * 0.5f + Right(db) * ((rm - lm) * 0.5f);
                c.transform.localPosition = new Vector3(mid.x, (-0.1f - WaterDepth - 0.3f) * 0.5f, mid.y);
                c.transform.localRotation = Quaternion.LookRotation(new Vector3(pb.x - pa.x, 0, pb.y - pa.y));
                var bc = c.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3((hwm - 0.3f) * 2f, WaterDepth + 0.2f, (pb - pa).magnitude);
                var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(c); pc.ball = ballProxy;
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); NoSync(pc);
            }
            // 浅い所に落ちた人が出口の岸に上がる石段（出口のふちの内側、3段）。ふわふわが上に乗るとアウト
            float[] tops = { Mathf.Lerp(-WaterDepth, BankTop, 0.25f), Mathf.Lerp(-WaterDepth, BankTop, 0.5f), Mathf.Lerp(-WaterDepth, BankTop, 0.75f) };
            for (int i = 0; i < 3; i++)
            {
                int k = i; float a = sPond1 - 0.5f * (3 - k), b = a + 0.5f;
                Sweep(dg, "ExitStep" + i, a, b, s => -1.2f, s => 1.2f, s => -WaterDepth - 0.05f, s => tops[k], ground, 0, true, true);
            }
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

        // ---- スタート・ゴール（2026-10-23 てじゃ「スタートはステージ1と同じように、ロビー戻りポータル島、スタート島、そこから洞窟に歩いて入っていく」
        //      「ゴールはとりあえず川の向こうに一旦ゴール島として浮かせてから考えよう」）----
        StartSet(root);
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
    const float DoorHW = 1.3f, DoorH = 2.4f;   // 洞窟の奥の壁の入口（半分の幅・高さ）
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
        Center(sF1 + FinDownEnd, out var wp, out var wd); var w0 = wp + Right(wd) * (-FinRiverHW - FinWalkW * 0.5f);
        float need = FinRiverHW + FinBankW + GoalGap + rUnder;
        float River(Vector2 q) { float m = float.MaxValue; for (float s = sF1 + FinRiver0; s <= sFin1; s += 0.1f) { Center(s, out var p, out _); m = Mathf.Min(m, (q - p).magnitude); } return m; }
        float t = rGround + 0.5f;
        while (t < 40f && River(w0 + wd * t) < need) t += 0.05f;
        goalC = w0 + wd * t; goalD = wd; walkP0 = w0;
        sWalkEnd = sF1 + FinDownEnd + t - rGround + 0.15f;   // 道の角まで島の地面にとどくように少し入れる（道は島より2cm低い）
    }
    static void GoalSet(Transform goal)
    {
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–1] FuwaCourse/Goal がない"); return; }
        var map = new Dictionary<Object, Object>();
        CopyChildren(src, goal, null, map);
        goal.localPosition = new Vector3(goalC.x, goalH + 0.02f, goalC.y); goal.localRotation = Quaternion.LookRotation(new Vector3(goalD.x, 0, goalD.y));
        FixCopied(goal, map);
    }

    // ---- ④ 貝の道（2026-10-21）----
    // 細い堤の道（M3_ClamWalk）の下の土台、両わきの崖、貝（左右交互）、貝の反対側の滝、道の外に落ちた時のアウト判定、泡（FuwaClamBubbles）。
    // 見た目（砂浜・岩場・本物の貝）はあとでまとめて。今は形と動きだけ
    // 貝の道の長さに対する位置・置く側（-1＝左）・高い台か。ふつうの貝（左右交互）のあいだに、高い台の貝を足す
    // （2026-10-22 てじゃ「まだ上から回避できる」「各貝の間にさらに貝追加して、ぐっと高い位置を飛ぶ形に」「上回避するとそれにあたるみたいな」）
    public static float[] ClamAt = { 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
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
    static void ClamWalk(Transform root, Material ground)
    {
        var g = new GameObject("ClamWalk").transform; g.SetParent(root, false);
        var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        float len = sWalk1 - sWalk0;
        // 道の下の土台（堤）と、両わきの崖（滝が流れ落ちる岩）
        Sweep(g, "WalkBase", sWalk0, sWalk1, s => -WalkHW, s => WalkHW, s => -8f, s => WalkH(s) - Thick - 0.01f, ground, 0, true);
        foreach (float o in new[] { -1f, 1f })
        {
            float a = WalkHW + FallsOut + 0.35f, b = WalkHW + FallsOut + 3f;
            Sweep(g, o < 0 ? "CliffL" : "CliffR", sWalk0 + 0.3f, sWalk1 - 0.3f, s => o < 0 ? -b : a, s => o < 0 ? -a : b, s => -8f, s => WalkH(s) + 4.5f, ground, 0, true);
        }
        // アウト判定：道のふちの外の下（ふわふわ＝FuwaHazard、人＝FuwaPlayerCatcher）。4m ごとの箱
        var outs = new GameObject("Outs").transform; outs.SetParent(g, false);
        int nSeg = Mathf.Max(1, Mathf.CeilToInt(len / 4f));
        for (int k = 0; k < nSeg; k++)
        {
            float a = Mathf.Lerp(sWalk0, sWalk1, k / (float)nSeg), b = Mathf.Lerp(sWalk0, sWalk1, (k + 1) / (float)nSeg), m = (a + b) * 0.5f;
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
            // 反対側の滝：崖から道の方へ流れ出て、下の海へ落ちる（泡に運ばれたふわふわはここで押し落とされる）
            float o = -side;
            var fc = p + r * (o * (WalkHW + FallsOut));
            FuwaStage2WaterTest.Waterfall(g, "SideFalls" + i, new Vector3(fc.x, 0, fc.y), y + 4.2f, -8f, 5f, new Vector3(-o * r.x, 0, -o * r.y));
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
    static void FallsRoad(Transform root, Material ground, Material bedMat, FuwaWaterFx fx, AudioSource splash)
    {
        var g = new GameObject("FallsRoad").transform; g.SetParent(root, false);
        var ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        Sweep(g, "RoadBase", sF0, sF1, s => -RoadW(s) * 0.5f + Cut(s, -1f), s => RoadW(s) * 0.5f - Cut(s, 1f), s => -8f, s => FallsH - Thick - 0.01f, ground, 0, true);
        float surf = FallsH - ChanDrop, outer = RoadHW + ChanHW * 2f;
        for (int i = 0; i < setS.Length; i++)
        {
            float sc = setS[i], o = setO[i];
            // 流れの上流（滝より少し手前）から下流（滝のあと ChanAfter）まで。流れが逆向きなら前後反転
            float fb = FallsFlowBack ? -1f : 1f;
            float c0 = sc - fb * (NotchUp + 1.2f), c1 = sc + fb * (NotchEnd + ChanAfter[i]);
            if (c0 > c1) { float tmp = c0; c0 = c1; c1 = tmp; }
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
            // 滝のカーテン：道を横切る向きで、外側から食い込む（幕の面は道の進む向きを向く）。押す厚み 0.9m がえぐれの中に収まるように少し手前に
            Center(sc - fb * 0.3f, out var fp, out var fd); var fr = Right(fd);
            float ia = RoadHW - CutDepth[i], width = outer - ia;
            var fc = fp + fr * (o * (ia + width * 0.5f));
            FuwaStage2WaterTest.Waterfall(set, "OverFalls", new Vector3(fc.x, 0, fc.y), FallsH + OverFallsH, surf, width, new Vector3(fd.x, 0, fd.y));
        }
        // アウト判定：道の下（ふわふわ＝FuwaHazard、人＝FuwaPlayerCatcher）。4m ごと、道と水路より下
        var outs = new GameObject("Outs").transform; outs.SetParent(g, false);
        float o0 = sF0, o1 = sGoal0;
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
        // 右岸：川ぞいの細い地面（ふわふわが乗るとアウト）
        Sweep(g, "RightBank", r0 + FinCliffLen, r1, s => FinRiverHW, s => FinRiverHW + FinBankW, s => surf - WaterDepth - 1.5f, s => surf + BankTop, ground, 11, true, true);
        // 左岸の道の下の土台（道と川のあいだのすき間を埋める）。カーブの始まりまで。その先の道は川から離れていく桟橋
        Sweep(g, "LeftBankBase", sF1 + FinLand, sBend0, s => -FinRiverHW - FinWalkW, s => -FinRiverHW, s => surf - WaterDepth - 1.5f, s => LeftBankH(s) - Thick - 0.01f, ground, 0, true);
        // カーブの外側の岸：右岸と同じ細い地面
        // 道がまだ岸の上にある所は道の内側のへりで切る（同じ高さで重なるとZファイト）
        float WalkInnerLat(float s)
        {
            Center(s, out var p, out var d); var n = Right(goalD); var q0 = walkP0 + n * (FinWalkW * 0.5f);
            float den = Vector2.Dot(Right(d), n); return Mathf.Abs(den) < 1e-4f ? -99f : Vector2.Dot(q0 - p, n) / den;
        }
        Sweep(g, "LeftBank", sBend0, r1, s => Mathf.Min(Mathf.Max(-FinRiverHW - FinBankW, WalkInnerLat(s)), -FinRiverHW - 0.001f), s => -FinRiverHW, s => surf - WaterDepth - 1.5f, s => surf + BankTop, ground, 11, true, true);
        // 滝のカーテン：道の幅いっぱい（踊り場の先、川の始まりの上）。落ち口は高く、川の水面まで
        Center(sF1 + FinCurtain, out var cp, out var cd); var cr = Right(cd);
        FuwaStage2WaterTest.Waterfall(g, "ForcedFalls", new Vector3(cp.x, 0, cp.y), H + FinCurtainTop, surf, FinRiverHW * 2f + 0.6f, new Vector3(cd.x, 0, cd.y));
        // 回り込み防止：左は岩の壁（滝の落ち口より高い）。人の坂の所だけくぐり口をあけて、ツタのカーテン（人はすり抜け、ふわふわは跳ね返る）、右は崖
        // （2026-10-22 てじゃ「滝の左をふわふわを防がないとな」。前は見えないアウトだけだった）
        float wa = sF1 + FinCurtain - 0.2f, wb = sF1 + FinCurtain + 0.6f, wm = (wa + wb) * 0.5f;
        float gIn = -(FinRiverHW + 0.25f), gOut = -(FinRiverHW + FinWalkW + 0.4f), gTop = LeftBankH(wm) + FinGateH;
        float wallTop = H + FinCurtainTop + 1f, wallBot = surf - WaterDepth - 1.5f;
        Sweep(terr, "FinalWallL", wa, wb, s => -FinWallOut, s => gOut, s => wallBot, s => wallTop, ground, 0, true);
        Sweep(terr, "FinalWallL_Lintel", wa, wb, s => gOut, s => gIn, s => gTop, s => wallTop, ground, 0, true);
        Center(wm, out var vp, out var vd); var vq = vp + Right(vd) * ((gIn + gOut) * 0.5f);
        VineCurtain(g, "GateVines", new Vector3(vq.x, gTop, vq.y), new Vector3(Right(vd).x, 0, Right(vd).y), gIn - gOut, FinGateH, 2106);
        // 壁のさらに左（空中）を回り込むふわふわはアウト（ふつうは届かない保険）
        var by = new GameObject("BypassOut"); by.transform.SetParent(g, false); by.layer = 4;
        var bl = cp + cr * (-FinWallOut - 2.5f);
        by.transform.localPosition = new Vector3(bl.x, (surf + H + FinCurtainTop) * 0.5f, bl.y); by.transform.localRotation = Quaternion.LookRotation(new Vector3(cd.x, 0, cd.y));
        var bbc = by.AddComponent<BoxCollider>(); bbc.isTrigger = true; bbc.size = new Vector3(5f, H + FinCurtainTop - surf, 0.8f);
        var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(by); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); NoSync(hz);
        Sweep(terr, "FinalCliffR", sF1 - 2f, r0 + FinCliffLen, s => FinRiverHW, s => FinRiverHW + 5f, s => Base - 4f, s => H + FinCurtainTop + 1f, ground, 0, true);
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
            var src = fbx.OfType<Mesh>().FirstOrDefault(m => m.name == "Vine_" + Mathf.RoundToInt(L * 10f) + "_" + (rng.Next(2) == 0 ? "A" : "B"));
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
    static void CurvedFalls(Transform root, string name, float sLip, float top, float bottom, float width, float reach, float thick)
    {
        Center(sLip, out var lp2, out var ld2);
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
        var V = new List<Vector3>(); var T = new List<int>(); var U1 = new List<Vector2>(); var U2 = new List<Vector2>();
        const int cols = 6;
        foreach (float side in new[] { 1f, -1f })   // 外側の膜・内側の膜
        {
            int b0 = V.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                var c = pts[i] + Nrm(i) * (thick * 0.5f * side);
                for (int k = 0; k <= cols; k++)
                {
                    float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, k / (float)cols);
                    V.Add(c + rgt * x); U1.Add(new Vector2(x + width * 0.5f, total - len[i])); U2.Add(new Vector2(width, total));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < cols; k++)
                {
                    int a = b0 + i * (cols + 1) + k, b = a + cols + 1;
                    T.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
        }
        var mesh = new Mesh { name = "Course2_1_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.SetUVs(0, U1.Select(u => Vector2.zero).ToList()); mesh.SetUVs(1, U1); mesh.SetUVs(2, U2);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = Save(mesh, MeshDir + "/" + name + ".asset");
        var g0 = new GameObject(name).transform; g0.SetParent(root, false);
        var cur = new GameObject("Curtain"); cur.transform.SetParent(g0, false);
        cur.AddComponent<MeshFilter>().sharedMesh = mesh;
        const string mp = "Assets/FuwaStage2/Materials/Stage2_WaterfallCurved.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!mat) { mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_Waterfall.mat")); AssetDatabase.CreateAsset(mat, mp); }
        mat.SetFloat("_Curved", 1f); EditorUtility.SetDirty(mat);
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
            var bc = box.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(width, (pb - pa).magnitude + 0.05f, thick + 0.5f);
            var wf = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWaterfall>(box);
            wf.forwardPush = 3.5f;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(wf); NoSync(wf);
        }
        // しぶきの霧と音：水のテスト場の滝を落ちる所に置いて、膜と押す箱だけ外す
        var land = pts[pts.Count - 1];
        FuwaStage2WaterTest.Waterfall(g0, "Splash", new Vector3(land.x, 0, land.z), bottom + 0.1f, bottom, width, f);
        var sp = g0.Find("Splash");
        foreach (var n in new[] { "Curtain", "Push" }) { var c = sp.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
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

    // ---- 水：水面の帯（流れの筋とふちの泡が帯にそう）・底・ふち・トリガーの箱の列 ----
    static void BuildWater(Transform parent, Water w, Material bedMat, Material rimMat, FuwaWaterFx fx, AudioSource splash)
    {
        var g = new GameObject(w.name).transform; g.SetParent(parent, false);
        float bed = w.surf - w.depth;
        float bodyBot = Mathf.Min(w.bodyBottom, bed - 1.3f);
        // 水面
        var V = new List<Vector3>(); var T = new List<int>(); var U1 = new List<Vector4>(); var U2 = new List<Vector2>();
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
                U1.Add(new Vector4(lat, hw, w.open0 ? 99f : s - w.s0, w.open1 ? 99f : w.s1 - s)); U2.Add(new Vector2(w.back ? -s : s, 0));
            }
        }
        for (int k = 0; k < n; k++)
            for (int i = 0; i < across; i++)
            {
                int a = k * (across + 1) + i, b = a + across + 1;
                AddTriDir(V, T, a, b, a + 1, Vector3.up); AddTriDir(V, T, a + 1, b, b + 1, Vector3.up);
            }
        var sm = new Mesh { name = "Course2_1_" + w.name + "_Surface", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        sm.SetVertices(V); sm.SetTriangles(T, 0); sm.SetUVs(0, V.Select(v => Vector2.zero).ToList()); sm.SetUVs(1, U1); sm.SetUVs(2, U2);
        sm.normals = V.Select(v => Vector3.up).ToArray(); sm.RecalculateBounds();
        sm = Save(sm, MeshDir + "/" + w.name + "_Surface.asset");
        var surf = new GameObject("Surface"); surf.transform.SetParent(g, false);
        surf.AddComponent<MeshFilter>().sharedMesh = sm; surf.AddComponent<MeshRenderer>().sharedMaterial = WaterMat(w.flow, true, w.deep);
        // 底（地面＝ふわふわが触るとアウト）と、その下のかたまり（横に当たるだけ）
        var bedGo = Sweep(g, "Bed", w.s0, w.s1, s => w.off(s) - w.hw(s) - 0.01f, s => w.off(s) + w.hw(s) + 0.01f, s => bed - 0.3f, s => bed, bedMat, 11, true);
        // 浅瀬は全部（2026-10-23 浅くしたので、流されたふわふわが底に当たりやすい）
        if (!w.deep && w.bedBounce <= 0f) w.bedBounce = ChanBedBounce;
        if (w.bedBounce > 0f)
        {
            // 地面のレイヤーのまま（真下の道として覚える＝道の外でアウト、にならない）。当たった時だけ跳ねる
            var bb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBounce>(bedGo);
            bb.bounceSpeed = w.bedBounce; bb.keepTangent = 0.8f; bb.alwaysUp = false; bb.useContactNormal = true;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bb); NoSync(bb);
        }
        Sweep(g, "Body", w.s0 - (w.open0 ? 0f : RimW), w.s1 + (w.open1 ? 0f : RimW), s => w.off(s) - w.hw(Mathf.Clamp(s, w.s0, w.s1)) - (w.rimL ? RimW : 0f), s => w.off(s) + w.hw(Mathf.Clamp(s, w.s0, w.s1)) + (w.rimR ? RimW : 0f),
            s => bodyBot, s => bed - 0.3f, rimMat, 0, true, true);
        // ふち（上に乗るとアウト）
        float rimTop = w.surf + RimUp;
        if (w.rimL && w.gapL1 > w.gapL0)
        {
            // 切れ目は水面すれすれの低いふち（水があふれて流れ落ちて見える）
            Sweep(g, "RimL", w.s0, w.gapL0, s => w.off(s) - w.hw(s) - RimW, s => w.off(s) - w.hw(s), s => bed - 0.3f, s => rimTop, rimMat, 0, true, true);
            Sweep(g, "RimL2", w.gapL1, w.s1, s => w.off(s) - w.hw(s) - RimW, s => w.off(s) - w.hw(s), s => bed - 0.3f, s => rimTop, rimMat, 0, true, true);
            Sweep(g, "RimL_Lip", w.gapL0, w.gapL1, s => w.off(s) - w.hw(s) - RimW, s => w.off(s) - w.hw(s), s => bed - 0.3f, s => w.surf - 0.04f, rimMat, 0, true, true);
        }
        else if (w.rimL) Sweep(g, "RimL", w.s0, w.s1, s => w.off(s) - w.hw(s) - RimW, s => w.off(s) - w.hw(s), s => bed - 0.3f, s => rimTop, rimMat, 0, true, true);
        if (w.rimR) Sweep(g, "RimR", w.s0, w.s1, s => w.off(s) + w.hw(s), s => w.off(s) + w.hw(s) + RimW, s => bed - 0.3f, s => rimTop, rimMat, 0, true, true);
        if (!w.open0) Sweep(g, "RimStart", w.s0 - RimW, w.s0, s => w.off(w.s0) - w.hw(w.s0) - (w.rimL ? RimW : 0f), s => w.off(w.s0) + w.hw(w.s0) + (w.rimR ? RimW : 0f), s => bed - 0.3f, s => rimTop, rimMat, 0, true, true);
        if (!w.open1) Sweep(g, "RimEnd", w.s1, w.s1 + RimW, s => w.off(w.s1) - w.hw(w.s1) - (w.rimL ? RimW : 0f), s => w.off(w.s1) + w.hw(w.s1) + (w.rimR ? RimW : 0f), s => bed - 0.3f, s => rimTop, rimMat, 0, true, true);
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
            for (int ch = 1; ch <= 2; ch++) { var u = new List<Vector4>(); m.GetUVs(ch, u); if (u.Count == m.vertexCount) old.SetUVs(ch, u); }   // CopyInto は uv0 だけ（水面の帯は uv1/uv2 を使う）
            old.UploadMeshData(false); EditorUtility.SetDirty(old); return old;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
