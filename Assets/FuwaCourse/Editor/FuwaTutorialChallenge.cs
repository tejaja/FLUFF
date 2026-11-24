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
    static Frame PathFrame(Transform bay, Transform nextBay)
    {
        var fr = new Frame();
        var wa = bay.Find("BayBarrier"); var wb = nextBay ? nextBay.Find("BayBarrier") : null;
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
    static List<Vector3> SimPath(FuwaBlower b, FuwaBall bt, Vector3 nz, Vector3 aim, Vector3 p, float charge, float floorY, float tMax = 4f)
    {
        var pts = new List<Vector3> { p };
        Vector3 v = b.DemoShotVelocity(nz, aim, charge, p);
        if (v.sqrMagnitude < 0.0001f) return pts;
        float dt = 0.01f;
        for (float t = 0f; t < tMax; t += dt)
        {
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
        return Build(bay, next, fr, B, rc, rn,
            "輪をくぐらせよう", "Send it through the ring",
            "<color=#FF6B5C>コツ</color>：ふわふわじゃなく<color=#FF6B5C>輪</color>の方を向いて撃とう\nふわふわが風の端にいれば、真ん中に寄って輪へ飛ぶ",
            "<color=#FF6B5C>Tip</color>: aim at the <color=#FF6B5C>ring</color>, not the fluff.\nIf the fluff is at the edge of the blast, it slides to the center and flies to the ring",
            ball) + " | " + sim;
    }

    // ===== 共通：輪・壁・看板 =====
    static string Build(Transform bay, Transform next, Frame fr, Vector3 ballPos, Vector3 ringC, Vector3 ringN, string titleJa, string titleEn, string hintJa, string hintEn, FuwaBall ball)
    {
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        var old = bay.Find("Challenge");
        if (old) { RemoveLang(lang, old); Object.DestroyImmediate(old.gameObject); }
        var root = new GameObject("Challenge").transform; root.SetParent(bay, false); root.position = fr.c; root.rotation = Quaternion.LookRotation(fr.f);
        // ためし撃ちの場所
        var spot = bay.GetComponentsInChildren<Transform>(true).First(t => t.name == "TrySpot");
        Undo.RecordObject(spot, "spot"); spot.position = ballPos; spot.rotation = Quaternion.LookRotation(fr.f);
        // 輪
        var ring = new GameObject("Ring"); ring.transform.SetParent(root, false); ring.transform.position = ringC; ring.transform.rotation = Quaternion.LookRotation(ringN, Vector3.up);
        ring.layer = LayerMask.NameToLayer("Walkthrough");
        var ringMesh = SaveMesh(TorusMesh(RingR, RingTube, 48, 10), "Tutorial_GoalRing");
        var rv = new GameObject("Visual"); rv.transform.SetParent(ring.transform, false); rv.AddComponent<MeshFilter>().sharedMesh = ringMesh;
        var rr = rv.AddComponent<MeshRenderer>(); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var mRing = FuwaStage2WaterTest.ToonMat("Tutorial_GoalRing", RingCol); var mClear = FuwaStage2WaterTest.ToonMat("Tutorial_GoalRingClear", ClearCol);
        rr.sharedMaterial = mRing;
        var mc = ring.AddComponent<MeshCollider>(); mc.sharedMesh = SaveMesh(DiscMesh(RingR - RingTube, 0.06f, 20), "Tutorial_GoalRingTrigger"); mc.convex = true; mc.isTrigger = true;
        // キラキラ
        var fx = MakeSparkle(ring.transform);
        // 音（自分にだけ聞こえる 2D）
        var sClear = Sfx(ring.transform, "ClearSfx", "Assets/FuwaCourse/Audio/sfx_checkpoint.wav", 0.8f);
        // 先の壁（次の区画のさかいめ）。ふわふわを割る壁（BayBarrier）の手前 15cm に、プレイヤーも通れない色つきの壁
        var nb = next.Find("BayBarrier");
        var gate = new GameObject("Gate").transform; gate.SetParent(root, false);
        Vector3 back = -fr.f; gate.position = nb.position + back * 0.15f; gate.rotation = nb.rotation;
        var gv = new GameObject("Visual"); gv.transform.SetParent(gate, false);
        var panel = new GameObject("Panel"); panel.transform.SetParent(gv.transform, false); panel.transform.localScale = nb.localScale;
        panel.AddComponent<MeshFilter>().sharedMesh = nb.GetComponent<MeshFilter>().sharedMesh;
        var gr = panel.AddComponent<MeshRenderer>(); gr.sharedMaterial = LockMat(nb.GetComponent<Renderer>().sharedMaterial); gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; gr.receiveShadows = false;
        // 壁の文字（区画がわ）
        var label = Text(gv.transform, "Label", "課題をクリアすると開くよ", 5f, new Color(0.45f, 0.2f, 0.32f)); label.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
        label.transform.position = new Vector3(nb.position.x, fr.top + 1.7f, nb.position.z) + back * 0.18f; label.transform.rotation = Quaternion.LookRotation(fr.f);
        AddLang(lang, label, "課題をクリアすると開くよ", "Clear the challenge to open");
        var block = new GameObject("PlayerBlock"); block.transform.SetParent(gate, false); block.layer = 0;
        var bc = block.AddComponent<BoxCollider>(); bc.size = new Vector3(nb.localScale.x, nb.localScale.y, 0.3f);
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
        var hb = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(hb.GetComponent<Collider>()); hb.name = "HintPaper";
        hb.transform.SetParent(hint.transform, false); hb.transform.localPosition = new Vector3(0f, 0f, 0.006f / st.localScale.z); hb.transform.localScale = new Vector3(3.05f, 0.85f, 1f);
        hb.GetComponent<Renderer>().sharedMaterial = sign.Find("SignModel").GetComponent<Renderer>().sharedMaterials.Skip(1).FirstOrDefault() ?? hb.GetComponent<Renderer>().sharedMaterial;
        hb.transform.localRotation = Quaternion.identity;
        AddLang(lang, hint, hintJa, hintEn);
        // 動き
        var goal = UdonSharpUndo.AddComponent<FuwaTutorialGoal>(ring);
        goal.ball = ball; goal.trySpot = spot; goal.ringRenderers = new Renderer[] { rr }; goal.ringMat = mRing; goal.clearMat = mClear;
        goal.clearFx = fx; goal.sfxClear = sClear; goal.gateBlock = block; goal.gateVisual = gv.transform; goal.sfxOpen = sOpen;
        goal.clearMark = stamp.gameObject; goal.hint = hint.gameObject; goal.gateHeight = nb.localScale.y + 0.5f;
        UdonSharpEditorUtility.CopyProxyToUdon(goal);
        stamp.gameObject.SetActive(false); hint.gameObject.SetActive(false);
        // チュートリアルから出たら戻す
        var marks = Object.FindObjectsOfType<FuwaReadMarks>(true).FirstOrDefault();
        if (marks)
        {
            var list = (marks.goals ?? new FuwaTutorialGoal[0]).Where(g => g != null && g != goal).ToList(); list.Add(goal);
            marks.goals = list.ToArray(); UdonSharpEditorUtility.CopyProxyToUdon(marks);
        }
        UdonSharpEditorUtility.CopyProxyToUdon(lang);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bay.gameObject.scene);
        return $"[課題] {bay.name}：輪 ({ringC - fr.c:F2}) 道幅 {fr.halfW * 2f:F2}m";
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
        ex.Clear(false); ex.SetVertices(m.vertices); if (m.normals.Length > 0) ex.SetNormals(m.normals); ex.SetTriangles(m.triangles, 0); ex.RecalculateBounds();
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
