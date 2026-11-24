using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// チュートリアルの課題（2026-11-24 てじゃ「課題式にするなら全部やりたい」「壁を通過不可にして、課題クリアで壁解放」）
//  区画ごとに：輪（ふわふわがくぐったらクリア）＋ 先の区画のさかいめに「プレイヤーが通れない壁」（クリアで下へ沈む）＋ 看板に「クリア！」のはんこ・コツ。
//  動きは FuwaTutorialGoal（U#）。区画の中の「Challenge」を消して作り直す（何回押しても同じ）
public static class FuwaTutorialChallenge
{
    const string MeshDir = "Assets/FuwaCourse/Meshes/Tutorial";
    const float HalfW = 1.75f;
    public static Color RingCol = new Color(1f, 0.55f, 0.45f), ClearCol = new Color(1f, 0.84f, 0.25f), LockCol = new Color(1f, 0.55f, 0.72f, 1f);
    public static float RingR = 0.55f, RingTube = 0.06f;

    static Transform Tut() { var c = GameObject.Find("FuwaCourse0"); return c ? c.transform.Find("Tutorial") : null; }
    static FuwaBall Ball() => Object.FindObjectsOfType<FuwaBall>(true).First(b => b.name == "FuwaBall");
    static FuwaBlower Tmpl() => Object.FindObjectsOfType<FuwaBlower>(true).First(b => b.transform.parent == null && b.name == "FuwaBlower");

    // 道の上の、ある所の「まん中・上の面の高さ・進む向き・右（島の側）」
    public struct Frame { public Vector3 c, f, r; public float top, halfW; }
    static Frame PathFrame(Transform bay, Transform nextBay) => PathFrameB(bay, nextBay ? nextBay.Find("BayBarrier") : null);
    static Frame PathFrameB(Transform bay, Transform wb)
    {
        var fr = new Frame();
        var wa = bay.Find("BayBarrier");
        var zone = bay.GetComponentsInChildren<Transform>(true).First(t => t.name == "TryZone");
        Vector3 f = (wb ? wb.position : zone.position + zone.forward) - (wa ? wa.position : zone.position - zone.forward); f.y = 0f; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f).normalized;
        var isl = bay.Find("Island"); if (isl && Vector3.Dot(isl.position - zone.position, r) < 0f) r = -r;
        Physics.SyncTransforms();
        float Top(Vector3 p)
        {
            float best = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.name.Contains("TutorialPath")) best = Mathf.Max(best, h.point.y);
            return best;
        }
        Vector3 p0 = zone.position;
        float lo = 0f, hi = 0f;
        for (float x = 0f; x > -5f; x -= 0.02f) { if (float.IsNegativeInfinity(Top(p0 + r * x))) break; lo = x; }
        for (float x = 0f; x < 5f; x += 0.02f) { if (float.IsNegativeInfinity(Top(p0 + r * x))) break; hi = x; }
        fr.c = p0 + r * ((lo + hi) * 0.5f); fr.halfW = (hi - lo) * 0.5f; fr.top = Top(fr.c); fr.c.y = fr.top; fr.f = f; fr.r = r;
        return fr;
    }

    // 道の上の点 p（だいたい）の所の、まん中・幅・進む向き（道はうねっているので、その場所で測り直す）
    static float TopAt(Vector3 p)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.name.Contains("TutorialPath")) best = Mathf.Max(best, h.point.y);
        return best;
    }
    static Vector3 CenterAt(Vector3 p, Vector3 r, out float halfW)
    {
        float lo = 0f, hi = 0f;
        for (float x = 0f; x > -6f; x -= 0.02f) { if (float.IsNegativeInfinity(TopAt(p + r * x))) break; lo = x; }
        for (float x = 0f; x < 6f; x += 0.02f) { if (float.IsNegativeInfinity(TopAt(p + r * x))) break; hi = x; }
        halfW = (hi - lo) * 0.5f; var c = p + r * ((lo + hi) * 0.5f); c.y = TopAt(c); return c;
    }
    static Frame FrameAt(Frame near, float s)
    {
        Physics.SyncTransforms();
        var fr = near; float hw;
        Vector3 c0 = CenterAt(near.c + near.f * s, near.r, out hw);
        Vector3 ca = CenterAt(near.c + near.f * (s - 1f), near.r, out _), cb = CenterAt(near.c + near.f * (s + 1f), near.r, out _);
        Vector3 f = cb - ca; f.y = 0f; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f).normalized; if (Vector3.Dot(r, near.r) < 0f) r = -r;
        fr.c = c0; fr.f = f; fr.r = r; fr.halfW = hw; fr.top = c0.y;
        return fr;
    }

    // 撃ってからの道すじ（SimDemoLanding と同じ計算。風なし）
    static List<Vector3> SimPath(FuwaBlower b, FuwaBall bt, Vector3 nz, Vector3 aim, Vector3 p, float charge, float floorY, float tMax = 4f) => SimPathW(b, bt, nz, aim, p, charge, floorY, Vector3.zero, tMax);
    static List<Vector3> SimPathW(FuwaBlower b, FuwaBall bt, Vector3 nz, Vector3 aim, Vector3 p, float charge, float floorY, Vector3 wind, float tMax = 4f)
    {
        var pts = new List<Vector3> { p };
        Vector3 v = b.DemoShotVelocity(nz, aim, charge, p);
        if (v.sqrMagnitude < 0.0001f) return pts;
        Vector3 dd = v.normalized;
        float amt = Mathf.Lerp(bt.shotWindDampMin, bt.shotWindDampMax, charge), hold = bt.shotWindDampHoldMax * charge * charge;
        float tm = Mathf.Lerp(bt.shotWindDampTimeMin, bt.shotWindDampTimeMax, charge);
        float dt = 0.01f;
        for (float t = 0f; t < tMax; t += dt)
        {
            Vector3 a = wind;
            float tt = t - hold, left = (tm > 0f && tt < tm) ? 1f - Mathf.Max(0f, tt) / tm : 0f;
            if (left > 0f) { float k = 1f - amt * left; float al = Vector3.Dot(a, dd); Vector3 keep = al > 0f ? dd * al : Vector3.zero; a = keep + (a - keep) * k; }
            v += a * dt;
            v += Vector3.down * bt.fallAcceleration * dt; v *= Mathf.Clamp01(1f - bt.airDrag * dt);
            if (v.y < -bt.maxFallSpeed) v.y = -bt.maxFallSpeed;
            if (v.magnitude > bt.maxSpeed) v = v.normalized * bt.maxSpeed;
            p += v * dt; pts.Add(p);
            if (p.y < floorY - 3f) break;
        }
        return pts;
    }
    // 道すじが輪（中心 rc・向き rn・内がわの半径 ri）をくぐるか
    static bool Through(List<Vector3> pts, Vector3 rc, Vector3 rn, float ri)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            float a = Vector3.Dot(pts[i - 1] - rc, rn), b = Vector3.Dot(pts[i] - rc, rn);
            if (a <= 0f && b > 0f)
            {
                var q = Vector3.Lerp(pts[i - 1], pts[i], a / (a - b));
                if ((q - rc).magnitude < ri) return true;
            }
        }
        return false;
    }

    // ===== 4 真ん中に寄る =====
    //  ふわふわは道の左のふちの外（空中）に浮かぶ。輪はその先・道の上。ふわふわの真うしろ（輪→ふわふわの延長）は道の外なので立てない
    //  → 道の上に立って「輪の方へ」向いて撃ち、ふわふわを風の端に入れると、真ん中へ寄って輪をくぐる
    public static float CBallOut = 0.55f, CBallUp = 1.1f, CBallS = 1.2f;   // ふわふわ：道のふちの外 m・道の上 m・看板から前へ
    public static float CStandS = -1.75f, CStandX = 0.35f, CRingAhead = 1.35f, CCharge = 1f, CAimUp = 0.15f;   // 想定の立ち位置（ふわふわから後ろ・道のふちから内へ）・輪までの前・溜め・上向き
    [MenuItem("FuwaCourse/れんしゅう/課題：4 真ん中に寄る（試作）")]
    public static void CenterMenu() { Debug.Log(BuildCenter()); }
    public static string BuildCenter()
    {
        var tut = Tut(); if (!tut) return "no tutorial";
        var bay = tut.Find("Bay_Center"); var next = tut.Find("Bay_Air");
        var fr = PathFrame(bay, next);
        var tmpl = Tmpl(); var ball = Ball();
        Vector3 L = -fr.r;   // 左（島と反対の空の側）
        // 2026-11-24 てじゃ「もっと看板の横に。看板読んでから戻るかんじになる」→ ふわふわは看板の前後の位置＋CBallS（看板を読みながら立つ所から撃てる）
        float signS = Vector3.Dot(bay.Find("Sign").position - fr.c, fr.f);
        var fb = FrameAt(fr, signS + CBallS);   // ふわふわの所の道
        fr = FrameAt(fr, signS + CBallS + CStandS * 0.5f);   // 立ち位置〜輪のまん中あたりの道で、向き・まん中を測り直す
        L = -fr.r;
        float bs = Vector3.Dot(fb.c - fr.c, fr.f);
        Vector3 B = fb.c + L * (fb.halfW + CBallOut) + Vector3.up * CBallUp;
        // お手本の立ち位置：道の上・ふわふわより後ろ
        var fs = FrameAt(fr, bs + CStandS);   // 立つ所の道
        Vector3 P = fs.c - fs.r * (fs.halfW - CStandX);
        Vector3 eye = P + Vector3.up * 1.25f;
        // 輪の場所を決める：道のふちの近くに立って「道と平行に前へ」撃つと、ふわふわは風の端に入って道の方へ寄りながら前へ飛ぶ。その道すじの上
        //  （ふわふわへ向いて撃つと、外＝空の方へ飛んでいく）
        Vector3 aim = (fr.f + Vector3.up * CAimUp).normalized;
        Vector3 nz = eye + aim * 0.45f;
        var pts = SimPath(tmpl, ball, nz, aim, B, CCharge, fr.top);
        // 道すじで、ふわふわから前へ CRingAhead 進んだ所
        Vector3 rc = B; Vector3 rn = fr.f; int k = 0;
        for (int i = 1; i < pts.Count; i++) if (Vector3.Dot(pts[i] - B, fr.f) >= CRingAhead) { k = i; break; }
        if (k == 0) return "[課題4] 道すじが前に届かない（" + pts.Count + "）";
        rc = pts[k]; rn = (pts[k] - pts[k - 1]); rn.y *= 0.5f; rn.Normalize();
        // 確かめ：立ち位置・向きを少しずつずらして、くぐる割合
        int ok = 0, all = 0;
        for (float dx = -0.4f; dx <= 0.41f; dx += 0.2f)
            for (float ds = -0.6f; ds <= 0.61f; ds += 0.3f)
                for (float da = -6f; da <= 6.1f; da += 3f)
                    for (float de = -4f; de <= 4.1f; de += 4f)
                    {
                        Vector3 e2 = eye + L * dx + fr.f * ds;
                        Vector3 a2 = Quaternion.AngleAxis(da, Vector3.up) * aim;
                        a2 = Quaternion.AngleAxis(de, Vector3.Cross(a2, Vector3.up)) * a2;
                        all++; if (Through(SimPath(tmpl, ball, e2 + a2 * 0.45f, a2, B, CCharge, fr.top), rc, rn, RingR - RingTube - 0.12f)) ok++;
                    }
        // ふわふわの真ん中をねらう撃ち方（ふわふわへ向いて撃つ）でくぐれる立ち位置が道の上にあるか
        int naive = 0, naiveAll = 0;
        for (float s = -4f; s <= 0.01f; s += 0.25f)
            for (float x = -fr.halfW + 0.2f; x <= fr.halfW - 0.2f; x += 0.2f)
            {
                Vector3 e3 = fr.c + fr.f * (bs + s) + fr.r * x + Vector3.up * 1.25f;
                if ((B - e3).magnitude > 3f) continue;
                Vector3 a3 = (B - e3).normalized; naiveAll++;
                if (Through(SimPath(tmpl, ball, e3 + a3 * 0.45f, a3, B, CCharge, fr.top), rc, rn, RingR - RingTube - 0.12f)) naive++;
            }
        string sim = $"お手本の撃ち方のまわりでくぐる {ok}/{all}・ふわふわの真ん中をねらってくぐれる立ち位置 {naive}/{naiveAll}";
        return Build(bay, next.Find("BayBarrier"), fr, B, rc, rn,
            "輪をくぐらせよう", "Send it through the ring",
            "<color=#FF6B5C>コツ</color>：ふわふわじゃなく\n<color=#FF6B5C>輪</color>の方に銃を向けて撃とう",
            "<color=#FF6B5C>Tip</color>: point the gun at the <color=#FF6B5C>ring</color>,\nnot at the fluff",
            ball) + " | " + sim;
    }

    // ===== ほかの区画（2026-11-24）。ふわふわは看板の BayBallS m 先（看板を読んだ所からそのまま撃てる）=====
    public struct Shot { public float s, dx, pitch, yaw, charge; public bool atBall; }
    static Shot S(float s, float dx, float pitch, float yaw, float charge, bool atBall = false) => new Shot { s = s, dx = dx, pitch = pitch, yaw = yaw, charge = charge, atBall = atBall };
    public static float BayBallS = 1.2f;
    static Vector3 Eye(Frame fr, Vector3 B, Shot sh) { var e = B + fr.f * sh.s + fr.r * sh.dx; e.y = fr.top + 1.25f; return e; }
    static Vector3 Aim(Frame fr, Vector3 eye, Vector3 B, Shot sh)
    {
        if (sh.atBall) return (B - eye).normalized;
        var d = Quaternion.AngleAxis(sh.yaw, Vector3.up) * fr.f;
        return (d * Mathf.Cos(sh.pitch * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(sh.pitch * Mathf.Deg2Rad)).normalized;
    }
    static List<Vector3> Fire(Frame fr, Vector3 B, Shot sh, Vector3 wind)
    {
        var eye = Eye(fr, B, sh); var aim = Aim(fr, eye, B, sh);
        return SimPathW(Tmpl(), Ball(), eye + aim * 0.45f, aim, B, sh.charge, fr.top, wind);
    }
    static float RingIn => RingR - RingTube - 0.12f;
    // rule 0：前へ val m 進んだ所（輪は飛ぶ向きを向く）／rule 1：上がっていく途中で val m 上がった所（輪は水平）
    static bool PickRing(List<Vector3> pts, Vector3 B, Frame fr, int rule, float val, out Vector3 rc, out Vector3 rn)
    {
        rc = B; rn = fr.f;
        for (int i = 1; i < pts.Count; i++)
        {
            bool hit = rule == 0 ? Vector3.Dot(pts[i] - B, fr.f) >= val : (pts[i].y - B.y >= val && pts[i].y > pts[i - 1].y);
            if (!hit) continue;
            rc = pts[i];
            if (rule == 0) { rn = pts[i] - pts[i - 1]; rn.y *= 0.5f; rn.Normalize(); } else rn = Vector3.up;
            return true;
        }
        return false;
    }
    static string Check(Frame fr, Vector3 B, Shot good, Shot[] bad, Vector3 wind, Vector3 rc, Vector3 rn)
    {
        int ok = 0, all = 0;
        foreach (float ds in new[] { -0.4f, 0f, 0.4f })
            foreach (float dx in new[] { -0.3f, 0f, 0.3f })
                foreach (float da in new[] { -5f, 0f, 5f })
                    foreach (float de in new[] { -4f, 0f, 4f })
                    {
                        var sh = good; sh.s += ds; sh.dx += dx; sh.yaw += da; sh.pitch += de;
                        all++; if (Through(Fire(fr, B, sh, wind), rc, rn, RingIn)) ok++;
                    }
        string b = string.Join(" ", bad.Select(x => Through(Fire(fr, B, x, wind), rc, rn, RingIn) ? "×くぐる" : "○外れる"));
        return $"ねらい通りのまわり {ok}/{all}・ちがう撃ち方 {b}";
    }
    static Transform Bay(string n) => Tut().Find(n);
    static Transform Barrier(string nextBay) => nextBay == null ? Tut().Find("MushBarrier") : Bay(nextBay).Find("BayBarrier");

    static string RingBay(string bayName, string nextBay, float ballX, float ballUp, Shot good, Shot[] bad, int rule, float val, Vector3 wind, string hintJa, string hintEn, bool upright = false)
    {
        var bay = Bay(bayName); var nb = Barrier(nextBay);
        var fr0 = PathFrameB(bay, nb);
        float signS = Vector3.Dot(bay.Find("Sign").position - fr0.c, fr0.f);
        var fr = FrameAt(fr0, signS + BayBallS);
        Vector3 B = fr.c + fr.r * ballX + Vector3.up * ballUp;
        var pts = Fire(fr, B, good, wind);
        if (!PickRing(pts, B, fr, rule, val, out var rc, out var rn)) return $"[課題] {bayName}：道すじが届かない（{pts.Count}）";
        if (upright) rn = fr.f;   // 輪はまっすぐ立てて正面向き
        string chk = Check(fr, B, good, bad, wind, rc, rn);
        return Build(bay, nb, fr, B, rc, rn, "", "", hintJa, hintEn, Ball()) + " | " + chk;
    }

    const string R = "<color=#FF6B5C>", E = "</color>";
    [MenuItem("FuwaCourse/れんしゅう/課題：1 ちょん押しと溜め")] public static void M1() => Debug.Log(BuildTap());
    public static string BuildTap() => RingBay("Bay_TapCharge", "Bay_Down", -0.5f, 1.1f, S(-1.2f, 0f, 8f, 0f, 1f), new[] { S(-1.2f, 0f, 8f, 0f, 0f), S(-1.2f, 0f, 8f, 0f, 0.3f) }, 0, 2.0f, Vector3.zero,
        R + "コツ" + E + "：輪が遠い時は" + R + "長押しで溜めて" + E + "から撃とう\n溜めるほど遠くまで飛ぶ",
        R + "Tip" + E + ": the ring is far. " + R + "Hold to charge" + E + ", then shoot.\nThe longer you charge, the farther it flies");
    [MenuItem("FuwaCourse/れんしゅう/課題：2 下向き")] public static void M2() => Debug.Log(BuildDown());
    // 2 下向き（2026-11-24 てじゃ「リングは普通に低めに正面向きで置くだけで十分」）：上から下へ撃つと上がって前へ → 前の低めの立てた輪
    public static float DownRingAhead = 1.6f;
    public static string BuildDown() => RingBay("Bay_Down", "Bay_Steep", -0.5f, 0.4f, S(-0.7f, 0f, 0f, 0f, 1f, true), new Shot[0], 0, DownRingAhead, Vector3.zero,
        R + "コツ" + E + "：低いふわふわは" + R + "上から下へ" + E + "撃とう\n下に撃っても上に飛んでいく",
        R + "Tip" + E + ": shoot the low fluff " + R + "from above, downward" + E + "\nIt still flies up", true);
    [MenuItem("FuwaCourse/れんしゅう/課題：3 上向きすぎない")] public static void M3() => Debug.Log(BuildSteep());
    // 3 上を向きすぎに注意（2026-11-24 てじゃ「ふわふわはもっと上に」「リングはちょい奥に、まっすぐ普通に置く」
    //  「上のを撃ってあんまり進まなかった後に、まっすぐ撃たせる想定」）
    //  ふわふわは高い所（道の上 SteepBallUp）。見上げて撃つと上がるだけで前に進まない → 落ちてきた所をまっすぐ撃つと輪へ。輪は立てて前向き
    public static float SteepBallUp = 2.1f, SteepRingAhead = 2.4f, SteepRingUp = 1.9f, SteepBallX = -0.5f;   // 輪の場所は2発目（落ちてきた所をまっすぐ）が一番入る所を探して決めた
    public static string BuildSteep()
    {
        var bay = Bay("Bay_Steep"); var nb = Barrier("Bay_Center");
        var fr0 = PathFrameB(bay, nb);
        float signS = Vector3.Dot(bay.Find("Sign").position - fr0.c, fr0.f);
        var fr = FrameAt(fr0, signS + BayBallS);
        Vector3 B = fr.c + fr.r * SteepBallX + Vector3.up * SteepBallUp;
        var fa = FrameAt(fr, SteepRingAhead);
        Vector3 rc = fa.c + fa.r * SteepBallX + Vector3.up * SteepRingUp; Vector3 rn = fa.f;
        // 1発目：見上げて撃つ（ふわふわへ向けて、半分溜め）→ あまり前に進まない
        var one = Fire(fr, B, S(-0.9f, 0f, 0f, 0f, 0.5f, true), Vector3.zero);
        float adv = 0f; foreach (var q in one) adv = Mathf.Max(adv, Vector3.Dot(q - B, fr.f));
        bool oneThrough = Through(one, rc, rn, RingIn);
        // 2発目：落ちてきて目の高さあたり（道の上 when）に来た所を、うしろからまっすぐ撃つ
        int ok = 0, all = 0;
        foreach (float when in new[] { 1.1f, 1.3f, 1.5f })
            foreach (float back in new[] { -1.0f, -1.3f, -1.6f })
                foreach (float pitch in new[] { 0f, 6f, 12f })
                    foreach (float ch in new[] { 0.5f, 1f })
                    {
                        int k = -1; for (int i = 5; i < one.Count; i++) if (one[i].y < one[i - 1].y && one[i].y <= fr.top + when) { k = i; break; }
                        if (k < 0) continue;
                        Vector3 B2 = one[k];
                        var p2 = Fire(fr, B2, S(back, 0f, pitch, 0f, ch), Vector3.zero);
                        all++; if (Through(p2, rc, rn, RingIn)) ok++;
                    }
        // ふわふわが高いうちに、うしろからまっすぐ（見上げ気味に）1発で入れられるか
        int direct = 0, dAll = 0;
        foreach (float back in new[] { -1.5f, -2.0f, -2.5f })
            foreach (float ch in new[] { 0.5f, 1f })
            { dAll++; if (Through(Fire(fr, B, S(back, 0f, 0f, 0f, ch, true), Vector3.zero), rc, rn, RingIn)) direct++; }
        return Build(bay, nb, fr, B, rc, rn, "", "",
            R + "コツ" + E + "：上を向いて撃つと前に進まない\n落ちてきたら" + R + "まっすぐ前" + E + "に撃とう",
            R + "Tip" + E + ": shooting upward won't move it forward\nWhen it comes down, shoot " + R + "straight ahead" + E, Ball())
            + $" | 見上げ1発：前へ {adv:F2}m・輪 {(oneThrough ? "×入る" : "○入らない")}・落ちてきてまっすぐ {ok}/{all}・高いうちに1発 {direct}/{dAll}";
    }
    public static float WindYaw = 22f;
    [MenuItem("FuwaCourse/れんしゅう/課題：6 横風")] public static void M6() => Debug.Log(BuildWind());
    public static string BuildWind()
    {
        var area = Bay("Bay_Wind").GetComponentsInChildren<FuwaWindArea>(true).First();
        Vector3 wind = area.transform.TransformDirection(area.localDirection).normalized * area.strength; wind.y = 0f;
        var fr = PathFrameB(Bay("Bay_Wind"), Barrier("Bay_Monster"));
        float yaw = Vector3.Dot(wind, fr.r) > 0f ? -WindYaw : WindYaw;   // 風上へ
        return RingBay("Bay_Wind", "Bay_Monster", 0f, 1.1f, S(-1.2f, 0f, 8f, yaw, 1f), new[] { S(-1.2f, 0f, 8f, 0f, 1f), S(-1.2f, 0f, 8f, 0f, 0.5f) }, 0, 2.0f, wind,
            R + "コツ" + E + "：風で流されるぶん" + R + "風上" + E + "をねらって撃とう\n溜めて撃つと流されにくい",
            R + "Tip" + E + ": aim " + R + "upwind" + E + " to make up for the drift\nCharged shots drift less") + $" | 風 {wind.magnitude:F2}";
    }
    // 5 空気ぎれ：1発では届かない遠い輪。落ちてきたらもう1発（溜めると空気がもどる）
    public static float AirRingAhead = 4.0f, AirRingUp = 0.7f;
    [MenuItem("FuwaCourse/れんしゅう/課題：5 空気ぎれ")] public static void M5() => Debug.Log(BuildAir());
    public static string BuildAir()
    {
        var bay = Bay("Bay_Air"); var nb = Barrier("Bay_Wind");
        var fr0 = PathFrameB(bay, nb);
        float signS = Vector3.Dot(bay.Find("Sign").position - fr0.c, fr0.f);
        var fr = FrameAt(fr0, signS + BayBallS - 1.5f);
        Vector3 B = fr.c + fr.r * -0.3f + Vector3.up * 1.1f;
        var fa = FrameAt(fr, AirRingAhead);
        Vector3 rc = fa.c + fa.r * -0.3f + Vector3.up * (1.1f + AirRingUp); Vector3 rn = fa.f;
        // 1発だけ（満タン）では届かない／2発（落ちてきた所でもう1発）なら届くか
        var one = Fire(fr, B, S(-1.2f, 0f, 8f, 0f, 1f), Vector3.zero);
        bool oneOk = Through(one, rc, rn, RingIn);
        int twoOk = 0, twoAll = 0;
        foreach (float when in new[] { 0.2f, 0.4f, 0.6f })
            foreach (float pitch in new[] { 5f, 12f, 20f })
            {
                var p1 = one; int k = p1.FindIndex(q => q.y < B.y + when && p1.IndexOf(q) > 20); if (k < 0) continue;
                Vector3 B2 = p1[k]; var fr2 = fr; fr2.top = fr.top;
                var p2 = Fire(fr2, B2, S(-1.2f, 0f, pitch, 0f, 1f), Vector3.zero);
                twoAll++; if (Through(p2, rc, rn, RingIn)) twoOk++;
            }
        return Build(bay, nb, fr, B, rc, rn, "", "", R + "コツ" + E + "：1発じゃ届かない。落ちてきたら" + R + "もう1発" + E + "\n空気がなくなったら" + R + "溜めると戻る" + E,
            R + "Tip" + E + ": one shot won't reach. Hit it " + R + "again" + E + " as it comes down\nOut of air? " + R + "Charge" + E + " to refill", Ball())
            + $" | 1発 {(oneOk ? "×届く" : "○届かない")}・2発 {twoOk}/{twoAll}";
    }
    [MenuItem("FuwaCourse/れんしゅう/課題：7 まもの")] public static void M7() => Debug.Log(BuildMonster());
    public static string BuildMonster()
    {
        var bay = Bay("Bay_Monster"); var nb = Barrier(null);
        var fr = PathFrameB(bay, nb);
        var mon = bay.GetComponentsInChildren<FuwaEnemy>(true).First();
        return Build(bay, nb, fr, Vector3.zero, mon.transform.position, fr.f, "", "",
            R + "コツ" + E + "：まものがぷるぷるしたら撃って\n" + R + "トゲの方へ押し返そう" + E,
            R + "Tip" + E + ": when it wobbles, blast it\n" + R + "back into the thorns" + E, Ball(), mon);
    }
    [MenuItem("FuwaCourse/れんしゅう/課題：ぜんぶ作り直す")] public static void MAll()
    {
        Debug.Log(BuildTap()); Debug.Log(BuildDown()); Debug.Log(BuildSteep()); Debug.Log(BuildCenter()); Debug.Log(BuildAir()); Debug.Log(BuildWind()); Debug.Log(BuildMonster());
    }

    // ===== 共通：輪・壁・看板 =====
    static string Build(Transform bay, Transform nb, Frame fr, Vector3 ballPos, Vector3 ringC, Vector3 ringN, string titleJa, string titleEn, string hintJa, string hintEn, FuwaBall ball, FuwaEnemy monster = null)
    {
        bool hasRing = monster == null;
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        var old = bay.Find("Challenge");
        if (old) { RemoveLang(lang, old); Object.DestroyImmediate(old.gameObject); }
        var root = new GameObject("Challenge").transform; root.SetParent(bay, false); root.position = fr.c; root.rotation = Quaternion.LookRotation(fr.f);
        // ためし撃ちの場所
        var spot = bay.GetComponentsInChildren<Transform>(true).First(t => t.name == "TrySpot");
        if (hasRing) { Undo.RecordObject(spot, "spot"); spot.position = ballPos; spot.rotation = Quaternion.LookRotation(fr.f); }
        // 輪（まものの区画は輪なし：まものが倒れたらクリア）
        var ring = new GameObject(hasRing ? "Ring" : "Goal"); ring.transform.SetParent(root, false); ring.transform.position = ringC;
        ring.transform.rotation = Quaternion.LookRotation(ringN, Mathf.Abs(Vector3.Dot(ringN, Vector3.up)) > 0.9f ? fr.f : Vector3.up);
        ring.layer = LayerMask.NameToLayer("Walkthrough");
        var mRing = FuwaStage2WaterTest.ToonMat("Tutorial_GoalRing", RingCol); var mClear = FuwaStage2WaterTest.ToonMat("Tutorial_GoalRingClear", ClearCol);
        MeshRenderer rr = null;
        if (hasRing)
        {
            var ringMesh = SaveMesh(TorusMesh(RingR, RingTube, 48, 10), "Tutorial_GoalRing");
            var rv = new GameObject("Visual"); rv.transform.SetParent(ring.transform, false); rv.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            rr = rv.AddComponent<MeshRenderer>(); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;   // 2026-11-24 てじゃ「リングに影」
            rr.sharedMaterial = mRing;
            var mc = ring.AddComponent<MeshCollider>(); mc.sharedMesh = SaveMesh(DiscMesh(RingR - RingTube, 0.06f, 20), "Tutorial_GoalRingTrigger"); mc.convex = true; mc.isTrigger = true;
        }
        // キラキラ
        var fx = MakeSparkle(ring.transform);
        // 音（自分にだけ聞こえる 2D）
        var sClear = Sfx(ring.transform, "ClearSfx", "Assets/FuwaCourse/Audio/sfx_checkpoint.wav", 0.8f);
        // 先の壁（2026-11-24 てじゃ「壁は1枚を常設、ふわふわは当たると消滅」「クリアしても壁は消えない、扉みたいに真ん中が切り抜かれる」「文字はなし」）
        //  見た目は BayBarrier（ふわふわを割る壁）と同じ材質の板を、くり抜く前／扉の形にくり抜いた後の2枚。BayBarrier の板は見えなくする（当たりはそのまま＝扉の所もふわふわは割れる）
        Vector3 back = -fr.f;
        var gate = new GameObject("Gate").transform; gate.SetParent(root, false); gate.position = nb.position; gate.rotation = nb.rotation;
        var nbR = nb.GetComponent<Renderer>(); var wallMat = nbR.sharedMaterial;
        Undo.RecordObject(nbR, "hide"); nbR.enabled = false;
        // 扉：道のまん中に、はば DoorW・高さ DoorH（板のローカル座標で）
        float sx = nb.lossyScale.x, sy = nb.lossyScale.y;
        var fg = fr; fg.c = new Vector3(nb.position.x, fr.top, nb.position.z);
        var fgc = FrameAt(fg, 0f);
        float dc = Vector3.Dot(fgc.c - nb.position, nb.right) / sx;
        float dl = dc - DoorW * 0.5f / sx, dr = dc + DoorW * 0.5f / sx, dt = -0.5f + (fgc.top + DoorH - (nb.position.y - sy * 0.5f)) / sy;
        GameObject Panel(string n, Mesh m)
        {
            var g = new GameObject(n); g.transform.SetParent(gate, false); g.transform.localScale = nb.localScale;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = wallMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return g;
        }
        var solid = Panel("Solid", nb.GetComponent<MeshFilter>().sharedMesh);
        var door = Panel("Door", SaveMesh(DoorMeshArch(dl, dr, dt, sx, sy), "Tutorial_GateDoor_" + bay.name));
        // 扉のふち（2026-11-24 てじゃ「開いた扉のフチをちょっと強調」）：くり抜いた穴の内がわにそって明るい細い帯（板と重ならないので Z ファイトなし）。扉といっしょに出る
        {
            float wx = DoorEdge / sx, wy = DoorEdge / sy;
            var fm = new Mesh(); var V = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
            void R(float x0, float y0, float x1, float y1)
            {
                int k = V.Count; V.Add(new Vector3(x0, y0, 0)); V.Add(new Vector3(x1, y0, 0)); V.Add(new Vector3(x1, y1, 0)); V.Add(new Vector3(x0, y1, 0));
                for (int q = 0; q < 4; q++) C.Add(DoorEdgeCol);
                T.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            // 上は ∩ の半円（ワールドで丸く。板は横 sx・縦 sy に引きのばされているので、ローカルでは楕円）
            float rw = (dr - dl) * 0.5f, xc = (dl + dr) * 0.5f, rh = rw * sx / sy, yc = dt - rh;
            R(dl, -0.5f, dl + wx, yc); R(dr - wx, -0.5f, dr, yc);
            const int AN = 24;
            for (int i = 0; i < AN; i++)
            {
                float a0 = Mathf.PI * i / AN, a1 = Mathf.PI * (i + 1) / AN;
                Vector3 o0 = new Vector3(xc + Mathf.Cos(a0) * rw, yc + Mathf.Sin(a0) * rh, 0), o1 = new Vector3(xc + Mathf.Cos(a1) * rw, yc + Mathf.Sin(a1) * rh, 0);
                Vector3 i0 = new Vector3(xc + Mathf.Cos(a0) * (rw - wx), yc + Mathf.Sin(a0) * (rh - wy), 0), i1 = new Vector3(xc + Mathf.Cos(a1) * (rw - wx), yc + Mathf.Sin(a1) * (rh - wy), 0);
                int k = V.Count; V.Add(o0); V.Add(o1); V.Add(i1); V.Add(i0); for (int q = 0; q < 4; q++) C.Add(DoorEdgeCol);
                T.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            fm.SetVertices(V); fm.SetColors(C); fm.SetTriangles(T, 0); fm.RecalculateNormals(); fm.RecalculateBounds();
            var fg2 = new GameObject("DoorEdge"); fg2.transform.SetParent(door.transform, false);
            fg2.AddComponent<MeshFilter>().sharedMesh = SaveMesh(fm, "Tutorial_GateDoorEdge_" + bay.name);
            var fr2 = fg2.AddComponent<MeshRenderer>(); fr2.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            fr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fr2.receiveShadows = false;
        }
        var doorFx = MakeSparkle(gate); doorFx.transform.position = fgc.c + Vector3.up * (DoorH * 0.5f); doorFx.transform.rotation = Quaternion.LookRotation(fr.f);
        var block = new GameObject("PlayerBlock"); block.transform.SetParent(gate, false); block.layer = 0;
        var bc = block.AddComponent<BoxCollider>(); bc.size = new Vector3(nb.localScale.x, nb.localScale.y, 0.3f);
        // 地面の矢印（扉の先、次の看板まで出しておく）
        var arrows = new GameObject("Arrows").transform; arrows.SetParent(gate, true);
        var am = SaveMesh(ArrowMesh(), "Tutorial_GateArrow");
        var amat = ArrowMat("Tutorial_ArrowDim", new Color(1f, 1f, 1f, 0.4f)); var amatLit = ArrowMat("Tutorial_ArrowLit", Color.white);
        var arrowRs = new List<Renderer>();
        for (int i = 0; i < ArrowN; i++)
        {
            var fa = FrameAt(fgc, ArrowStart + i * ArrowGap);
            var ag = new GameObject("Arrow" + i); ag.transform.SetParent(arrows, true);
            ag.transform.position = GroundAt(fa.c) + Vector3.up * 0.02f; ag.transform.rotation = Quaternion.LookRotation(fa.f);   // 道の外（キノコの島の芝など）でも一番上の面の 2cm 上（芝と重なってちらついた）
            ag.AddComponent<MeshFilter>().sharedMesh = am;
            var ar = ag.AddComponent<MeshRenderer>(); ar.sharedMaterial = amat; ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ar.receiveShadows = false; arrowRs.Add(ar);
        }
        var nextSign = nb.parent == Tut() ? Tut().Find("Sign_MoreTricks") : nb.parent.Find("Sign");
        var sOpen = Sfx(gate, "OpenSfx", "Assets/FuwaCourse/Audio/sfx_unlock_open.wav", 0.7f);
        // 看板：タイトルを課題に・クリアのはんこ・コツ
        var sign = bay.Find("Sign"); var st = sign.Find("Text");
        var stamp = Text(sign, "ClearStamp", "クリア！", 2.6f, new Color(0.95f, 0.3f, 0.25f));
        stamp.transform.localScale = st.localScale;
        stamp.transform.localPosition = st.localPosition + new Vector3(0.66f, 0.36f, -0.01f); stamp.transform.localRotation = Quaternion.Euler(0f, 0f, -14f); stamp.fontStyle = TMPro.FontStyles.Bold;
        stamp.transform.SetParent(root, true);
        AddLang(lang, stamp, "クリア！", "Clear!");
        // コツ：看板の紙の下に、小さい紙をもう1枚（柱にはる）
        var hint = Text(sign, "Hint", hintJa, 1.45f, new Color(0.25f, 0.22f, 0.3f));
        hint.transform.localScale = st.localScale; hint.rectTransform.sizeDelta = new Vector2(2.95f, 0.75f); hint.enableWordWrapping = true;
        hint.transform.localPosition = new Vector3(0f, 0.86f, st.localPosition.z - 0.004f); hint.transform.localRotation = st.localRotation; hint.transform.SetParent(root, true);
        var hb = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(hb.GetComponent<Collider>()); hb.name = "HintPaper"; hb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hb.transform.SetParent(hint.transform, false); hb.transform.localPosition = new Vector3(0f, 0f, 0.006f / st.localScale.z); hb.transform.localScale = new Vector3(3.05f, 0.85f, 1f);
        hb.GetComponent<Renderer>().sharedMaterial = sign.Find("SignModel").GetComponent<Renderer>().sharedMaterials.Skip(1).FirstOrDefault() ?? hb.GetComponent<Renderer>().sharedMaterial;
        hb.transform.localRotation = Quaternion.identity;
        AddLang(lang, hint, hintJa, hintEn);
        // 動き
        var goal = UdonSharpUndo.AddComponent<FuwaTutorialGoal>(ring);
        goal.ball = ball; goal.trySpot = spot; goal.ringRenderers = rr ? new Renderer[] { rr } : new Renderer[0]; goal.ringMat = mRing; goal.clearMat = mClear; goal.monster = monster;
        goal.clearFx = fx; goal.sfxClear = sClear; goal.gateBlock = block; goal.gateSolid = solid; goal.gateDoor = door; goal.doorFx = doorFx; goal.sfxOpen = sOpen;
        goal.arrows = arrows.gameObject; goal.arrowsHideAt = nextSign; goal.arrowsHideDist = 3f; goal.arrowRenderers = arrowRs.ToArray(); goal.arrowDim = amat; goal.arrowLit = amatLit;
        goal.clearMark = stamp.gameObject; goal.hint = hint.gameObject;
        UdonSharpEditorUtility.CopyProxyToUdon(goal);
        stamp.gameObject.SetActive(false); hint.gameObject.SetActive(false); door.SetActive(false); arrows.gameObject.SetActive(false);
        // チュートリアルから出たら戻す
        var marks = Object.FindObjectsOfType<FuwaReadMarks>(true).FirstOrDefault();
        if (marks)
        {
            var list = (marks.goals ?? new FuwaTutorialGoal[0]).Where(g => g != null && g != goal).ToList(); list.Add(goal);
            marks.goals = list.ToArray();
            if (hasRing) RevealWithSign(marks, ring, sign.gameObject);
            UdonSharpEditorUtility.CopyProxyToUdon(marks);
        }
        UdonSharpEditorUtility.CopyProxyToUdon(lang);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bay.gameObject.scene);
        return $"[課題] {bay.name}：輪 ({ringC - fr.c:F2}) 道幅 {fr.halfW * 2f:F2}m";
    }

    public static float DoorW = 1.7f, DoorH = 2.4f, ArrowStart = -1.3f, ArrowGap = 1.3f; public static int ArrowN = 3;   // 矢印は壁をまたぐ（手前・扉の所・先。2026-11-24 てじゃ）
    public static float DoorEdge = 0.07f; public static Color DoorEdgeCol = new Color(0.92f, 0.98f, 1f, 0.95f);   // 扉のふちの帯（はば m・色）
    public static Color ArrowCol = new Color(0.98f, 0.42f, 0.38f, 0.95f);   // 道のピンクに負けない濃いめのさんご色（的の輪と同じ系統）
    // 板（-0.5〜0.5 の四角）から、下のまん中の扉（dl〜dr、下から dt まで）をくり抜いた形。両面
    static Mesh DoorMesh(float dl, float dr, float dt)
    {
        var V = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
        void R(float x0, float y0, float x1, float y1)
        {
            int k = V.Count;
            foreach (var (x, y) in new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) }) { V.Add(new Vector3(x, y, 0f)); U.Add(new Vector2(x + 0.5f, y + 0.5f)); }
            T.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2, k, k + 1, k + 2, k, k + 2, k + 3 });
        }
        R(-0.5f, -0.5f, dl, 0.5f); R(dr, -0.5f, 0.5f, 0.5f); R(dl, dt, dr, 0.5f);
        var m = new Mesh(); m.SetVertices(V); m.SetUVs(0, U); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    // 扉の上を ∩ に（2026-11-24 てじゃ「扉の上、丸っこく」）：dl〜dr のはばの半円が上。板の外がわはそのまま。両面
    static Mesh DoorMeshArch(float dl, float dr, float dt, float sx, float sy)
    {
        var V = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int k = V.Count; foreach (var v in new[] { a, b, c, d }) { V.Add(v); U.Add(new Vector2(v.x + 0.5f, v.y + 0.5f)); }
            T.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3, k, k + 2, k + 1, k, k + 3, k + 2 });
        }
        Q(new Vector3(-0.5f, -0.5f), new Vector3(dl, -0.5f), new Vector3(dl, 0.5f), new Vector3(-0.5f, 0.5f));
        Q(new Vector3(dr, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(dr, 0.5f));
        float rw = (dr - dl) * 0.5f, xc = (dl + dr) * 0.5f, rh = rw * sx / sy, yc = dt - rh;
        const int AN = 24;
        for (int i = 0; i < AN; i++)
        {
            // 半円の上の点から板の上のふちまで（左から右へ）
            float a0 = Mathf.PI * (1f - (float)i / AN), a1 = Mathf.PI * (1f - (float)(i + 1) / AN);
            Vector3 p0 = new Vector3(xc + Mathf.Cos(a0) * rw, yc + Mathf.Sin(a0) * rh), p1 = new Vector3(xc + Mathf.Cos(a1) * rw, yc + Mathf.Sin(a1) * rh);
            Q(p0, p1, new Vector3(p1.x, 0.5f), new Vector3(p0.x, 0.5f));
        }
        var m = new Mesh(); m.SetVertices(V); m.SetUVs(0, U); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    // 地面の矢印（前向きの△、平ら、+z が前。2026-11-24 てじゃ「矢印を△に」「先端をちょっぴりだけ丸く」）
    public static float ArrowW = 0.42f, ArrowD = 0.55f, ArrowTipR = 0.07f;
    static Mesh ArrowMesh()
    {
        Vector3 L = new Vector3(-ArrowW, 0, -ArrowD * 0.5f), Rr = new Vector3(ArrowW, 0, -ArrowD * 0.5f), Tp = new Vector3(0, 0, ArrowD * 0.5f);
        // 3つの角をどれも半径 ArrowTipR の円弧に（2026-11-24 てじゃ「他の頂点も」）。角 P で、となりの2辺に接する円の弧
        var corners = new[] { L, Tp, Rr };
        var pts = new List<Vector3>();
        const int N = 8;
        for (int c = 0; c < 3; c++)
        {
            Vector3 P = corners[c], A = corners[(c + 2) % 3], Bv = corners[(c + 1) % 3];
            Vector3 da = (A - P).normalized, db = (Bv - P).normalized;
            float th = Vector3.Angle(da, db) * Mathf.Deg2Rad;
            float tl = ArrowTipR / Mathf.Tan(th * 0.5f);
            Vector3 t1 = P + da * tl, t2 = P + db * tl;
            Vector3 cc = P + (da + db).normalized * (ArrowTipR / Mathf.Sin(th * 0.5f));
            Vector3 u1 = (t1 - cc).normalized, u2 = (t2 - cc).normalized;
            for (int i = 0; i <= N; i++) pts.Add(cc + Vector3.Slerp(u1, u2, (float)i / N) * ArrowTipR);
        }
        Vector3 ctr = (L + Rr + Tp) / 3f;
        var V = new List<Vector3> { ctr }; V.AddRange(pts);
        var T = new List<int>(); for (int i = 1; i < V.Count - 1; i++) T.AddRange(new[] { 0, i, i + 1 });
        T.AddRange(new[] { 0, V.Count - 1, 1 });
        var C = Enumerable.Repeat(ArrowCol, V.Count).ToList();
        var m = new Mesh(); m.SetVertices(V); m.SetColors(C); m.SetTriangles(T, 0);
        var tr = m.triangles; for (int i = 0; i < tr.Length; i += 3) if (Vector3.Cross(V[tr[i + 1]] - V[tr[i]], V[tr[i + 2]] - V[tr[i]]).y < 0f) { int x = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = x; }
        m.triangles = tr; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 輪は看板と同時に出す（2026-11-24 てじゃ「各リングも看板と同時に出てくる感じに」）：FuwaReadMarks.reveal に看板と同じ revealAt で入れる（消えた物は詰める）
    static void RevealWithSign(FuwaReadMarks marks, GameObject obj, GameObject sign)
    {
        var rv = (marks.reveal ?? new GameObject[0]).ToList(); var ra = (marks.revealAt ?? new int[0]).ToList();
        int at = -1; for (int j = 0; j < rv.Count; j++) if (rv[j] == sign) at = j < ra.Count ? ra[j] : 0;
        if (at < 0) return;
        var nrv = new List<GameObject>(); var nra = new List<int>();
        for (int j = 0; j < rv.Count; j++) if (rv[j] != null && rv[j] != obj) { nrv.Add(rv[j]); nra.Add(j < ra.Count ? ra[j] : 0); }
        nrv.Add(obj); nra.Add(at);
        marks.reveal = nrv.ToArray(); marks.revealAt = nra.ToArray();
    }
    [MenuItem("FuwaCourse/れんしゅう/課題：輪を看板と同時に出す（登録だけ）")]
    public static void RevealRingsMenu()
    {
        var marks = Object.FindObjectsOfType<FuwaReadMarks>(true).First(); int n = 0;
        foreach (var bn in new[] { "Bay_TapCharge", "Bay_Down", "Bay_Steep", "Bay_Center", "Bay_Air", "Bay_Wind" })
        {
            var ring = Bay(bn).Find("Challenge/Ring"); if (!ring) continue;
            RevealWithSign(marks, ring.gameObject, Bay(bn).Find("Sign").gameObject); n++;
        }
        UdonSharpEditorUtility.CopyProxyToUdon(marks); EditorUtility.SetDirty(marks);
        Debug.Log("[課題] 輪を看板と同時に " + n);
    }

    static Material ArrowMat(string name, Color c)
    {
        string path = "Assets/FuwaCourse/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat")); AssetDatabase.CreateAsset(m, path); }
        m.color = c; EditorUtility.SetDirty(m); return m;
    }
    // 真下の一番上の面（トリガー以外・矢印自身以外）
    static Vector3 GroundAt(Vector3 p)
    {
        float best = float.NegativeInfinity;
        int mask = ~(1 << LayerMask.NameToLayer("Walkthrough"));   // 区画の壁（ふわふわだけ当たる）は見ない
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, mask, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.name.StartsWith("Arrow") || h.collider.name == "PlayerBlock") continue;
            best = Mathf.Max(best, h.point.y);
        }
        if (!float.IsNegativeInfinity(best)) p.y = best;
        return p;
    }
    static Material LockMat(Material like)
    {
        const string p = MeshDir + "/../../Materials/Tutorial_LockPanel.mat";
        string path = "Assets/FuwaCourse/Materials/Tutorial_LockPanel.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(like); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", LockCol); m.SetFloat("_Alpha", 0.55f); m.SetFloat("_BandAlpha", 0.28f); m.SetFloat("_Height", 3.2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static TMPro.TextMeshPro Text(Transform parent, string name, string s, float size, Color col)
    {
        var src = Tut().GetComponentsInChildren<TMPro.TextMeshPro>(true).First(t => t.name == "Text" && t.transform.parent.name == "Sign");
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TMPro.TextMeshPro>();
        t.font = src.font; t.fontSharedMaterial = src.fontSharedMaterial; t.text = s; t.fontSize = size; t.color = col;
        t.alignment = TMPro.TextAlignmentOptions.Center; t.enableWordWrapping = false;
        t.rectTransform.sizeDelta = new Vector2(4f, 1f);
        return t;
    }

    static AudioSource Sfx(Transform parent, string name, string clip, float vol)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        var a = g.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clip); a.playOnAwake = false; a.volume = vol; a.spatialBlend = 0f;
        return a;
    }

    static ParticleSystem MakeSparkle(Transform parent)
    {
        var g = new GameObject("ClearFx"); g.transform.SetParent(parent, false);
        var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = 0.5f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.6f, 0.75f)); m.gravityModifier = 0.25f; m.maxParticles = 80;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = RingR; sh.radiusThickness = 0f; sh.rotation = Vector3.zero;
        var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }); col.color = gr;
        var r = g.GetComponent<ParticleSystemRenderer>();
        var src = Object.FindObjectsOfType<ParticleSystemRenderer>(true).FirstOrDefault(x => x.name == "WindParticles" && x.sharedMaterial);
        var anyMat = src ? src.sharedMaterial : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        r.sharedMaterial = anyMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    static Mesh TorusMesh(float R, float r, int nu, int nv)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>();
        for (int i = 0; i <= nu; i++)
        {
            float u = Mathf.PI * 2f * i / nu; var cu = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
            for (int j = 0; j <= nv; j++)
            {
                float w = Mathf.PI * 2f * j / nv; var n = cu * Mathf.Cos(w) + Vector3.forward * Mathf.Sin(w);
                V.Add(cu * R + n * r); N.Add(n);
            }
        }
        for (int i = 0; i < nu; i++) for (int j = 0; j < nv; j++)
            {
                int a = i * (nv + 1) + j, b = a + nv + 1;
                T.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var m = new Mesh(); m.SetVertices(V); m.SetNormals(N); m.SetTriangles(T, 0);
        // 外向きの面に（法線と三角形の向きをそろえる）
        var tr = m.triangles; var vv = m.vertices; var nn = m.normals;
        for (int k = 0; k < tr.Length; k += 3)
        {
            var c = Vector3.Cross(vv[tr[k + 1]] - vv[tr[k]], vv[tr[k + 2]] - vv[tr[k]]);
            if (Vector3.Dot(c, nn[tr[k]] + nn[tr[k + 1]] + nn[tr[k + 2]]) < 0f) { int t = tr[k + 1]; tr[k + 1] = tr[k + 2]; tr[k + 2] = t; }
        }
        m.triangles = tr; m.RecalculateBounds();
        return m;
    }
    static Mesh DiscMesh(float R, float h, int n)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        for (int i = 0; i < n; i++) { float a = Mathf.PI * 2f * i / n; V.Add(new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, -h)); V.Add(new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, h)); }
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; T.AddRange(new[] { i * 2, j * 2, j * 2 + 1, i * 2, j * 2 + 1, i * 2 + 1 }); }
        for (int i = 1; i < n - 1; i++) { T.AddRange(new[] { 0, i * 2, (i + 1) * 2 }); T.AddRange(new[] { 1, (i + 1) * 2 + 1, i * 2 + 1 }); }
        var m = new Mesh(); m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    static Mesh SaveMesh(Mesh m, string name)
    {
        m.name = name; string path = MeshDir + "/" + name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) { AssetDatabase.CreateAsset(m, path); return m; }
        ex.Clear(false); ex.SetVertices(m.vertices); if (m.normals.Length > 0) ex.SetNormals(m.normals);
        if (m.uv.Length > 0) ex.uv = m.uv; if (m.colors.Length > 0) ex.colors = m.colors;
        ex.SetTriangles(m.triangles, 0); ex.RecalculateBounds();
        if (m.normals.Length == 0) ex.RecalculateNormals();
        EditorUtility.SetDirty(ex); return ex;
    }

    static void AddLang(FuwaLanguage lang, TMPro.TextMeshPro t, string ja, string en)
    {
        var so = new SerializedObject(lang);
        var tx = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
        int n = tx.arraySize;
        tx.arraySize = n + 1; pj.arraySize = n + 1; pe.arraySize = n + 1;
        tx.GetArrayElementAtIndex(n).objectReferenceValue = t;
        pj.GetArrayElementAtIndex(n).stringValue = ja;
        pe.GetArrayElementAtIndex(n).stringValue = en;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    // 作り直す前に、その下の文字を言語の一覧から外す（null も詰める）
    static void RemoveLang(FuwaLanguage lang, Transform under)
    {
        var mine = new HashSet<Object>(under.GetComponentsInChildren<TMPro.TextMeshPro>(true));
        var so = new SerializedObject(lang);
        var tx = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
        for (int i = tx.arraySize - 1; i >= 0; i--)
        {
            var o = tx.GetArrayElementAtIndex(i).objectReferenceValue;
            if (o == null || mine.Contains(o))
            {
                tx.GetArrayElementAtIndex(i).objectReferenceValue = null; tx.DeleteArrayElementAtIndex(i); pj.DeleteArrayElementAtIndex(i); pe.DeleteArrayElementAtIndex(i);
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
