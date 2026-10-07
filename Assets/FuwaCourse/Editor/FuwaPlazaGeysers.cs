using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// コース2「いわのみち」ゴール前の崩れた広場（2026-10-07）：
// 穴の底から立つ岩柱3本の噴気孔が順番にドンッと噴く（ふわふわ用の FuwaWindArea＋噴いている間だけ人も持ち上げる FuwaPlayerUpdraft）。
// v4（2026-10-07）：人は回り込む上り坂（Rocks2/PlazaWalk）を歩くだけ。噴気孔はふわふわ専用で、崖下で拾う1本と、
// 坂の内側でゴールの高さまで持ち上げる1本（ゴールの方へ少し傾けて噴く）。柱は当たっても跳ね返るだけ（FuwaBounce）。
// ふわふわが穴に落ちたらアウト（DiveFloor を穴全体に広げる）。
// 岩の形は Blender（FLUFF_models.blend の C2Plaza、scratchpad の c2plaza.py）→ ImportRocks で Rocks2/Plaza*。
// 何度実行しても同じ結果（Udon の付いた物は作り直さずに設定だけ上書き、見た目の Fx だけ作り直す）。
public static class FuwaPlazaGeysers
{
    // 噴気孔の口（コースローカル。c2plaza.py の VENTS / CREST と同じ）
    // 2026-10-07 改：人もふわふわも柱A(2)→柱B(3)の噴気をたどる一本道。柱A・Bは足場より低い（噴気に乗らないと上がれない）。1本目は崖から落ちたふわふわを拾う用
    static readonly Vector3[] Vents = { new Vector3(19.6377f, 2.3f, 71.0213f), new Vector3(14.7774f, 3.0f, 76.1998f) };
    static readonly float[] Tops = { 6.5f, 8.2f };       // 風の届く高さ（コースローカル y）。2本目はゴールの柵(5.6+0.6)より上まで
    static readonly float[] Tilts = { 0f, 15f };         // ゴールの方へ傾ける角度
    static readonly Vector3 GoalCenter = new Vector3(12.74638f, 0f, 81.45003f);
    public static float OnSec = 2.0f, OffSec = 1.6f, Lag = 1.2f;   // 2026-10-07 てじゃ「ムズい」→ 長く・間隔短く（元 1.3/2.3）   // 1→2→3 の順に Lag 秒ずつ遅れて噴く
    public static float BaseLevel = 0.3f;   // 7×0.3=2.1 > 落ちる力1.5 → 噴気の上ではゆっくり上がって止まる
    public static float BallStrength = 7f, BallFade = 0.35f;   // 1回噴くと約3m上がる（geysim.py で試算）。10だと崖より高く上がって滑空しほうだい
    const string BurstPath = "Assets/FuwaCourse/Audio/sfx_geyser_burst.wav";

    [MenuItem("FuwaCourse/Course2/ゴール前の噴気孔広場を作る")]
    public static void Menu() { Debug.Log(Build()); }

    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "geyser"); }
        return t;
    }

    static T USharp<T>(GameObject go) where T : UdonSharp.UdonSharpBehaviour
    {
        var c = go.GetComponent<T>();
        return c != null ? c : UdonSharpUndo.AddComponent<T>(go);
    }

    public static string Build()
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var holder = Child(root, "Gimmick_PlazaGeysers");
        holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity; holder.localScale = Vector3.one;
        var puff = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/SteamPuff.mat");
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/VentGlow.mat");
        var hiss = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/sfx_steam_loop.wav");
        var burst = MakeBurst(false);
        float cycle = OnSec + OffSec;
        string log = "";
        for (int k = 0; k < Vents.Length; k++)
        {
            var g = Child(holder, "Geyser" + (k + 1));
            g.localPosition = Vents[k]; g.localRotation = Quaternion.identity;
            var toGoal = GoalCenter - Vents[k]; toGoal.y = 0f; toGoal.Normalize();
            var tiltRot = Quaternion.AngleAxis(Tilts[k], Vector3.Cross(Vector3.up, toGoal));

            // ---- 見た目（毎回作り直す。Udon なし）
            var oldFx = g.Find("Fx"); if (oldFx != null) Object.DestroyImmediate(oldFx.gameObject);
            var fx = new GameObject("Fx").transform; fx.SetParent(g, false);
            fx.localPosition = new Vector3(0f, -0.15f, 0f);
            var erupt = MakeErupt(fx, puff);
            var jet = MakeJet(fx, puff);
            erupt.transform.localRotation = tiltRot; jet.transform.localRotation = tiltRot;
            MakeIdle(fx, puff);
            MakeGlow(fx, glow);
            var boom = MakeAudio(fx, "Burst", burst, false, 0.55f, 26f, 0.95f + 0.05f * k);
            MakeAudio(fx, "Hiss", hiss, true, 0.1f, 9f, 0.85f + 0.04f * k);

            // ---- ふわふわ用の噴気
            var zone = Child(g, "Zone");
            zone.gameObject.layer = 0;
            // 口の0.3m下から、傾けた向きに Tops の高さまで
            float y0 = -0.3f, y1 = (Tops[k] - Vents[k].y) / Mathf.Cos(Tilts[k] * Mathf.Deg2Rad);
            zone.localRotation = tiltRot; zone.localPosition = tiltRot * new Vector3(0f, (y0 + y1) * 0.5f, 0f); zone.localScale = Vector3.one;
            var bc = zone.GetComponent<BoxCollider>(); if (bc == null) bc = zone.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.center = Vector3.zero; bc.size = new Vector3(1.5f, y1 - y0, 1.5f);
            var w = USharp<FuwaWindArea>(zone.gameObject);
            w.localDirection = Vector3.up; w.strength = BallStrength; w.fadeAlongDirection = BallFade;
            w.onSeconds = OnSec; w.offSeconds = OffSec;
            w.phaseOffset = Mathf.Repeat(-Lag * k, cycle);
            w.gustCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f));
            w.baseLevel = BaseLevel; w.calmSeconds = 0f;   // 噴いていない間も弱い上昇気流（ふわふわを置いておけば落ちずに待つ）
            w.warnSeconds = 0.5f; w.warnLevel = 0.12f;
            w.visual = erupt.gameObject; w.extraVisuals = new[] { jet };
            w.fadeSeconds = 0.15f; w.visualLead = 0.12f;
            w.gustSound = boom;
            w.groundHighlight = null;
            UdonSharpEditorUtility.CopyProxyToUdon(w);

            // 人用の上昇気流は v4 でなくした（人は坂を歩く）
            var oldLift = g.Find("PlayerLift");
            if (oldLift != null) { var ou = oldLift.GetComponent<FuwaPlayerUpdraft>(); if (ou != null) UdonSharpUndo.DestroyImmediate(ou); Undo.DestroyObjectImmediate(oldLift.gameObject); }
            log += g.name + " phase " + w.phaseOffset.ToString("F2") + "  ";
        }
        // 前の版の3本目
        for (int k = Vents.Length; k < 6; k++) { var old = holder.Find("Geyser" + (k + 1)); if (old == null) continue; foreach (var ub in old.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)) UdonSharpUndo.DestroyImmediate(ub); Undo.DestroyObjectImmediate(old.gameObject); log += "removed Geyser" + (k + 1) + "  "; }

        var rocks = root.Find("Rocks2");
        // ---- 浮き岩は v4 でなくした
        foreach (var name in new[] { "PlazaFloat1", "PlazaFloat2" })
        {
            var t = rocks.Find(name); if (t == null) continue;
            foreach (var ub in t.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)) UdonSharpUndo.DestroyImmediate(ub);
            Undo.DestroyObjectImmediate(t.gameObject); log += "removed " + name + "  ";
        }
        // ---- 柱：ふわふわが当たっても跳ね返るだけ（アウトにしない）
        var pil = rocks.Find("PlazaPillars");
        if (pil != null)
        {
            pil.gameObject.layer = 0;
            var bo = USharp<FuwaBounce>(pil.gameObject);
            bo.bounceSpeed = 2.4f; bo.keepTangent = 0.8f; bo.alwaysUp = false; bo.useContactNormal = true;
            UdonSharpEditorUtility.CopyProxyToUdon(bo);
        }
        // ---- 道の外側のふちに夜光キノコ（目印）。位置は c2plaza.py が書き出した info.lights
        MakePathLights(root, holder);

        // ---- ふわふわが穴に落ちたらアウト：DiveFloor（見えないハザード）を崖下〜ゴールの島の手前まで広げる
        var df = root.Find("DiveFloor");
        {
            Vector3 O = new Vector3(18.53511f, 0f, 72.68986f), D = new Vector3(-0.5513058f, 0f, 0.8342972f);
            float s0 = 71.7f, s1 = 88.0f, sc = (s0 + s1) * 0.5f;
            var c = O + D * (sc - 79f);
            Undo.RecordObject(df, "dive floor");
            df.localPosition = new Vector3(c.x, 1.8f, c.z);
            var box = df.GetComponent<BoxCollider>(); Undo.RecordObject(box, "dive floor");
            box.size = new Vector3(12f, 0.4f, s1 - s0); box.center = Vector3.zero;
            // 下に道がない所で「道から落ちた」と判定されないよう、すき間の目印（GapZone）も穴全体に広げる（高さはそのまま）
            var gz = root.Find("GapZone");
            if (gz != null)
            {
                float g0 = 70.75f, gc = (g0 + s1) * 0.5f;
                var gp = O + D * (gc - 79f);
                Undo.RecordObject(gz, "gap zone");
                gz.localPosition = new Vector3(gp.x, gz.localPosition.y, gp.z);
                var gb = gz.GetComponent<BoxCollider>(); Undo.RecordObject(gb, "gap zone");
                gb.size = new Vector3(gb.size.x, gb.size.y, s1 - g0);
            }
        }

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log;
    }

    // 道の目印の位置（c2plaza.py の info.lights と同じ。x,y,z,外向きx,z）
    static readonly float[][] Lights =
    {
        new[] { 16.7051f, 3.0684f, 73.4077f, 0.9707f, -0.2403f },
        new[] { 17.8423f, 3.5882f, 75.9360f, 0.8693f, -0.4943f },
        new[] { 19.0697f, 4.2384f, 78.6718f, 0.9713f, -0.2379f },
        new[] { 19.1278f, 4.8102f, 81.4181f, 0.9735f, 0.2287f },
        new[] { 17.3329f, 5.2474f, 83.5164f, 0.3714f, 0.9285f },
    };

    static void MakePathLights(Transform root, Transform holder)
    {
        var src = root.Find("Gimmick_CaveLights/CaveGlow0/Yakou");
        var old = holder.Find("PathLights"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var pl = new GameObject("PathLights").transform; pl.SetParent(holder, false);
        Undo.RegisterCreatedObjectUndo(pl.gameObject, "lights");
        for (int i = 0; i < Lights.Length; i++)
        {
            var L = Lights[i];
            var y = Object.Instantiate(src.gameObject, pl).transform;
            y.name = "Yakou" + i;
            // 高さは道の上の面に合わせる（坂の形を変えても付いてくる）
            float top = L[1];
            if (Physics.Raycast(root.TransformPoint(new Vector3(L[0], L[1] + 3f, L[2])), Vector3.down, out var hit, 6f, 1 << 11, QueryTriggerInteraction.Ignore))
                top = root.InverseTransformPoint(hit.point).y;
            y.localPosition = new Vector3(L[0], top - 0.02f, L[2]);
            y.localRotation = Quaternion.LookRotation(new Vector3(L[3], 0f, L[4])) * Quaternion.Euler(0f, 37f * i, 0f);
            y.localScale = Vector3.one * (i % 2 == 0 ? 2.1f : 1.8f);
            foreach (var t in y.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }
    }

    // ===== 見た目 =====
    static ParticleSystem NewPS(Transform parent, string name, Material mat, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 4f; main.loop = true; main.prewarm = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = max;
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        var tsa = ps.textureSheetAnimation;
        tsa.enabled = true; tsa.numTilesX = 4; tsa.numTilesY = 4;
        tsa.animation = ParticleSystemAnimationType.WholeSheet;
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone; sh.rotation = new Vector3(-90f, 0f, 0f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.maxParticleSize = 3f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static void Fade(ParticleSystem ps, float inT, float peak, float midT, float mid)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inT), new GradientAlphaKey(mid, midT), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
    }

    static void Grow(ParticleSystem ps, float a, float b, float c)
    {
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, a), new Keyframe(0.3f, b), new Keyframe(1f, c)));
    }

    // 噴いている間の大きな柱（FuwaWindArea.visual。出る量を風の強さでフェード）
    static ParticleSystem MakeErupt(Transform fx, Material puff)
    {
        var ps = NewPS(fx, "Erupt", puff, 140);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 10.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
        main.gravityModifier = -0.03f;
        var em = ps.emission; em.rateOverTime = 50f;
        var sh = ps.shape; sh.angle = 6f; sh.radius = 0.28f;
        var vol = ps.velocityOverLifetime; vol.enabled = true;
        vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 0.55f), new Keyframe(0.7f, 0.18f), new Keyframe(1f, 0.06f)));
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.5f; noise.scrollSpeed = 0.5f; noise.damping = true;
        Fade(ps, 0.06f, 0.92f, 0.55f, 0.5f);
        Grow(ps, 0.45f, 1.1f, 2.6f);
        return ps;
    }

    // 口のところの勢いのいい噴き出し（extraVisuals）
    static ParticleSystem MakeJet(Transform fx, Material puff)
    {
        var ps = NewPS(fx, "Jet", puff, 90);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(13f, 17f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.45f);
        main.startRotation = 0f;
        var em = ps.emission; em.rateOverTime = 80f;
        var sh = ps.shape; sh.angle = 4f; sh.radius = 0.15f;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 1.2f;
        var jr = ps.GetComponent<ParticleSystemRenderer>();
        jr.renderMode = ParticleSystemRenderMode.Stretch; jr.velocityScale = 0.06f; jr.lengthScale = 1.6f;
        var tsa = ps.textureSheetAnimation; tsa.enabled = false;
        var rol = ps.rotationOverLifetime; rol.enabled = false;
        Fade(ps, 0.03f, 0.95f, 0.5f, 0.6f);
        Grow(ps, 0.6f, 1.2f, 1.9f);
        return ps;
    }

    // 噴いていない間もうっすら湯気（ここが噴くと分かるように）
    static void MakeIdle(Transform fx, Material puff)
    {
        var ps = NewPS(fx, "Idle", puff, 24);
        var main = ps.main; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.gravityModifier = -0.02f;
        var em = ps.emission; em.rateOverTime = 5f;
        var sh = ps.shape; sh.angle = 10f; sh.radius = 0.25f;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.4f;
        Fade(ps, 0.15f, 0.45f, 0.6f, 0.25f);
        Grow(ps, 0.6f, 1.0f, 2.0f);
    }

    static void MakeGlow(Transform fx, Material glow)
    {
        var g = new GameObject("Glow");
        g.transform.SetParent(fx, false);
        g.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        g.transform.localScale = Vector3.one * 0.85f;
        g.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterial = glow;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    static AudioSource MakeAudio(Transform fx, string name, AudioClip clip, bool loop, float volume, float maxDist, float pitch)
    {
        var a = new GameObject(name);
        a.transform.SetParent(fx, false);
        a.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        var src = a.AddComponent<AudioSource>();
        src.clip = clip; src.loop = loop; src.playOnAwake = loop;
        src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 1.2f; src.maxDistance = maxDist; src.dopplerLevel = 0f;
        src.volume = volume; src.pitch = pitch;
        return src;
    }

    // ===== 「ボシュッ」と噴く音（1.4秒）=====
    [MenuItem("FuwaCourse/Course2/噴気孔の噴く音を作り直す")]
    static void RemakeBurst() { MakeBurst(true); }

    static AudioClip MakeBurst(bool force)
    {
        if (!force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(BurstPath);
            if (existing != null) return existing;
        }
        const int rate = 44100; const float sec = 1.4f;
        int n = (int)(rate * sec);
        var data = new float[n];
        var rnd = new System.Random(4242);
        float lp = 0f, lp2 = 0f, low = 0f, hpPrev = 0f, hpOut = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float w = (float)(rnd.NextDouble() * 2 - 1);
            // 出だしは低くこもった「ボッ」→だんだん明るい「シュー」へ
            float bright = Mathf.Lerp(0.18f, 0.6f, Mathf.Clamp01(t / 0.35f));
            lp += (w - lp) * bright;
            lp2 += (lp - lp2) * bright;
            hpOut = 0.985f * (hpOut + lp2 - hpPrev); hpPrev = lp2;
            low += (w - low) * 0.02f;   // 低いドン
            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 2.4f);
            float thump = low * 6f * Mathf.Exp(-t * 9f);
            data[i] = (hpOut * 0.75f + thump) * env;
        }
        float peak = 0f; foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        if (peak > 0f) for (int i = 0; i < n; i++) data[i] *= 0.85f / peak;
        Directory.CreateDirectory(Path.GetDirectoryName(BurstPath));
        using (var fs = new FileStream(BurstPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int bytes = n * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
            foreach (float s in data) bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
        AssetDatabase.ImportAsset(BurstPath);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(BurstPath);
    }
}
