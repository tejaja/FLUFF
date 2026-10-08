using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// れんしゅう（FuwaCourse0）の前半「説明の小道」を作る。何度実行しても同じ形に作り直す（FuwaCourse0/Tutorial を消して作る）。
// ロビー側の入口の島 → 右回りの小道（今のれんしゅうと同じピンクのしましま）→ スタートの島の横につながる。
// 道ぞいに「お手本区画」：道の外側に小さな島（お手本台）＋看板、道の上に自分のふわふわが浮く「ためし撃ちの場所」（FuwaTryZone）。
public static class FuwaPracticeTutorial
{
    const string Course = "FuwaCourse0";
    const string RootName = "Tutorial";
    const string MeshDir = "Assets/FuwaCourse/Meshes/Tutorial";

    // 小道の中心線（ワールドのXZ）。入口の島の中心から、スタートの島の東のふちまで
    // 最後の2点はスタートの島の柵を開けた所（EntryAngle）へまっすぐ入るように Run で足す
    static readonly Vector2[] CtrlBase =
    {
        new Vector2(8.0f, 0.0f), new Vector2(9.6f, 3.4f), new Vector2(12.4f, 9.0f), new Vector2(13.6f, 15.0f),
        new Vector2(12.6f, 20.4f), new Vector2(9.6f, 22.8f), new Vector2(6.6f, 21.2f),
    };
    static Vector2[] Ctrl = CtrlBase;
    // スタートの島の柵（StartArea のローカルで、+x から +z へ測った角度）。この範囲の柵をなくして小道を通す
    const float EntryAngle = 3f, GapFrom = -33f, GapTo = 39f;
    const float Top = 3.0f;        // 道の上面（今のれんしゅうの道と同じ）
    const float Thick = 0.3f;
    const float HalfW = 1.75f;
    const float Stripe = 0.6f;     // しましまの幅（今の道と同じ）
    const float IslandTop = 2.935f; // 島（BevelCyl）の上面。道より少し低い＝重ねてもZファイトしない

    // お手本区画：小道の上の位置（入口からの距離 m）と中身
    struct Bay
    {
        public float s; public string key; public string ja; public string en;
        public Bay(float s, string key, string ja, string en) { this.s = s; this.key = key; this.ja = ja; this.en = en; }
    }
    static readonly Bay[] Bays =
    {
        new Bay(7.5f, "TapCharge",
            "<size=130%><color=#FF6B5C>ちょん押し と 溜め</color></size>\nちょん押し：すぐ撃てる・ちかく\n長押し：溜めて離すと・とおく",
            "<size=130%><color=#FF6B5C>Tap & Charge</color></size>\nTap: quick shot, short\nHold & release: charged, far"),
        new Bay(15.0f, "Down",
            "<size=130%><color=#FF6B5C>下を撃っても上がる</color></size>\n風はいつもふわっと持ち上げる\n床に押しつけたりしない",
            "<size=130%><color=#FF6B5C>Shoot down, it goes up</color></size>\nThe wind always lifts the fluff\nIt never pushes it into the floor"),
        new Bay(22.5f, "Center",
            "<size=130%><color=#FF6B5C>風の真ん中に寄る</color></size>\nふわふわの横を撃っても\n銃の正面の方へ寄りながら飛ぶ",
            "<size=130%><color=#FF6B5C>Pulled to the center</color></size>\nShoot beside the fluff and\nit drifts toward where your gun points"),
        // 「溜めると吸い寄せる」は 2026-10-08 に仕組みごとなくした（端撃ちのじゃまになるので。chargePull=0）
    };

    [MenuItem("FuwaCourse/れんしゅう/説明の小道を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    // ---------- 中心線 ----------
    static Vector2 CR(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // 0.05m刻みでたどった点の列（位置・距離）
    static List<Vector2> Dense(out List<float> dist)
    {
        var pts = new List<Vector2>();
        int n = Ctrl.Length;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = Ctrl[Mathf.Max(0, i - 1)], p1 = Ctrl[i], p2 = Ctrl[i + 1], p3 = Ctrl[Mathf.Min(n - 1, i + 2)];
            for (int k = 0; k < 200; k++) pts.Add(CR(p0, p1, p2, p3, k / 200f));
        }
        pts.Add(Ctrl[n - 1]);
        dist = new List<float> { 0f };
        for (int i = 1; i < pts.Count; i++) dist.Add(dist[i - 1] + (pts[i] - pts[i - 1]).magnitude);
        return pts;
    }

    // 距離 s の位置と向き
    static void At(List<Vector2> pts, List<float> dist, float s, out Vector3 pos, out Vector3 fwd)
    {
        int i = 1;
        while (i < dist.Count - 1 && dist[i] < s) i++;
        float t = Mathf.InverseLerp(dist[i - 1], dist[i], s);
        Vector2 p = Vector2.Lerp(pts[i - 1], pts[i], t);
        Vector2 d = (pts[i] - pts[i - 1]).normalized;
        pos = new Vector3(p.x, Top, p.y);
        fwd = new Vector3(d.x, 0f, d.y);
    }

    // ---------- 道のメッシュ（上面はしましま2色＝サブメッシュ2つ、側面と底はA） ----------
    static Mesh PathMesh(List<Vector2> pts, List<float> dist, Matrix4x4 toLocal)
    {
        float len = dist[dist.Count - 1];
        int segs = Mathf.CeilToInt(len / 0.2f);
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
        var A = new List<int>(); var B = new List<int>();
        Vector3 L(Vector3 w) { return toLocal.MultiplyPoint3x4(w); }
        Vector3 Ld(Vector3 w) { return toLocal.MultiplyVector(w).normalized; }
        var cs = new List<Vector3>(); var rs = new List<Vector3>(); var ss = new List<float>();
        for (int k = 0; k <= segs; k++)
        {
            float s = len * k / segs;
            At(pts, dist, s, out var c, out var f);
            cs.Add(c); rs.Add(new Vector3(f.z, 0f, -f.x)); ss.Add(s);
        }
        // 上面：しまの境目でちょうど切れるように、各区間をしま単位で分ける
        for (int k = 0; k < segs; k++)
        {
            float s0 = ss[k], s1 = ss[k + 1];
            // この区間のしまの境目
            var cuts = new List<float> { s0 };
            for (float b = Mathf.Ceil(s0 / Stripe) * Stripe; b < s1 - 1e-4f; b += Stripe) if (b > s0 + 1e-4f) cuts.Add(b);
            cuts.Add(s1);
            for (int q = 0; q < cuts.Count - 1; q++)
            {
                float a = cuts[q], b2 = cuts[q + 1];
                float ta = (a - s0) / (s1 - s0), tb = (b2 - s0) / (s1 - s0);
                Vector3 ca = Vector3.Lerp(cs[k], cs[k + 1], ta), cb = Vector3.Lerp(cs[k], cs[k + 1], tb);
                Vector3 ra = Vector3.Lerp(rs[k], rs[k + 1], ta).normalized, rb = Vector3.Lerp(rs[k], rs[k + 1], tb).normalized;
                int stripe = Mathf.FloorToInt(((a + b2) * 0.5f) / Stripe) % 2;
                int bi = V.Count;
                V.Add(L(ca - ra * HalfW)); V.Add(L(ca + ra * HalfW)); V.Add(L(cb - rb * HalfW)); V.Add(L(cb + rb * HalfW));
                for (int j = 0; j < 4; j++) N.Add(Ld(Vector3.up));
                UV.Add(new Vector2(0, a)); UV.Add(new Vector2(1, a)); UV.Add(new Vector2(0, b2)); UV.Add(new Vector2(1, b2));
                var T = stripe == 0 ? A : B;
                T.AddRange(new[] { bi, bi + 2, bi + 1, bi + 1, bi + 2, bi + 3 });
            }
        }
        // 側面（左右）と底
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; k < segs; k++)
            {
                Vector3 o0 = cs[k] + rs[k] * HalfW * side, o1 = cs[k + 1] + rs[k + 1] * HalfW * side;
                Vector3 nrm = Ld((rs[k] + rs[k + 1]) * side);
                int bi = V.Count;
                V.Add(L(o0)); V.Add(L(o1)); V.Add(L(o0 + Vector3.down * Thick)); V.Add(L(o1 + Vector3.down * Thick));
                for (int j = 0; j < 4; j++) N.Add(nrm);
                UV.Add(new Vector2(0, ss[k])); UV.Add(new Vector2(0, ss[k + 1])); UV.Add(new Vector2(1, ss[k])); UV.Add(new Vector2(1, ss[k + 1]));
                if (side > 0) A.AddRange(new[] { bi, bi + 1, bi + 2, bi + 2, bi + 1, bi + 3 });
                else A.AddRange(new[] { bi, bi + 2, bi + 1, bi + 1, bi + 2, bi + 3 });
            }
        }
        for (int k = 0; k < segs; k++)
        {
            Vector3 d = Vector3.down * Thick;
            int bi = V.Count;
            V.Add(L(cs[k] - rs[k] * HalfW + d)); V.Add(L(cs[k] + rs[k] * HalfW + d)); V.Add(L(cs[k + 1] - rs[k + 1] * HalfW + d)); V.Add(L(cs[k + 1] + rs[k + 1] * HalfW + d));
            for (int j = 0; j < 4; j++) N.Add(Ld(Vector3.down));
            for (int j = 0; j < 4; j++) UV.Add(Vector2.zero);
            A.AddRange(new[] { bi, bi + 1, bi + 2, bi + 1, bi + 3, bi + 2 });
        }
        // 両はしのふた
        for (int e = 0; e < 2; e++)
        {
            int k = e == 0 ? 0 : segs;
            Vector3 c = cs[k], r = rs[k];
            Vector3 f = e == 0 ? Vector3.Cross(r, Vector3.up) : -Vector3.Cross(r, Vector3.up);   // 外向き
            int bi = V.Count;
            V.Add(L(c - r * HalfW)); V.Add(L(c + r * HalfW)); V.Add(L(c - r * HalfW + Vector3.down * Thick)); V.Add(L(c + r * HalfW + Vector3.down * Thick));
            for (int j = 0; j < 4; j++) N.Add(Ld(f));
            for (int j = 0; j < 4; j++) UV.Add(Vector2.zero);
            if (e == 0) A.AddRange(new[] { bi, bi + 1, bi + 2, bi + 2, bi + 1, bi + 3 });
            else A.AddRange(new[] { bi, bi + 2, bi + 1, bi + 1, bi + 2, bi + 3 });
        }
        // 三角形の向きを、決めた法線にそろえる（裏向きのものは裏返す）
        foreach (var T in new[] { A, B })
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 p0 = V[T[i]], p1 = V[T[i + 1]], p2 = V[T[i + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), N[T[i]]) < 0f) { int tmp = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = tmp; }
            }
        var m = new Mesh { name = "Course0_TutorialPath" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV);
        m.subMeshCount = 2; m.SetTriangles(A, 0); m.SetTriangles(B, 1);
        m.RecalculateBounds();
        // 向きの確認：上面の三角形が上を向いているか（間違っていたら裏返す）
        return m;
    }

    static Material Mat(string n)
    {
        return AssetDatabase.FindAssets(n + " t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(p => AssetDatabase.LoadAssetAtPath<Material>(p)).First(x => x != null && x.name == n);
    }

    static GameObject Dup(Transform src, Transform parent, string name)
    {
        var g = Object.Instantiate(src.gameObject, parent, true);
        g.name = name;
        return g;
    }

    static void SetU(Component c, string prop, Object v)
    {
        var so = new SerializedObject(c);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("no prop " + prop + " on " + c.GetType().Name); return; }
        p.objectReferenceValue = v;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetUF(Component c, string prop, float v) { var so = new SerializedObject(c); so.FindProperty(prop).floatValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void SetUB(Component c, string prop, bool v) { var so = new SerializedObject(c); so.FindProperty(prop).boolValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

    static void SetArr(Component c, string prop, float[] v)
    {
        var so = new SerializedObject(c); var p = so.FindProperty(prop); p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).floatValue = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArrV(Component c, string prop, Vector3[] v)
    {
        var so = new SerializedObject(c); var p = so.FindProperty(prop); p.arraySize = v.Length;
        for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).vector3Value = v[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Sync(Component c)
    {
        if (c is UdonSharp.UdonSharpBehaviour u) UdonSharpEditorUtility.CopyProxyToUdon(u);
    }

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var course = scene.GetRootGameObjects().First(g => g.name == Course).transform;
        var old = course.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var proto = course.Find("DemoProto");
        if (proto != null) Object.DestroyImmediate(proto.gameObject);
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Tutorial");

        var root = new GameObject(RootName).transform;
        root.SetParent(course, false);
        var start = course.Find("Start");
        var startArea = start.Find("StartArea");
        var ball = Object.FindObjectsOfType<FuwaBall>(true).First(b => b.name == "FuwaBall");
        var blowerTmpl = Object.FindObjectsOfType<FuwaBlower>(true).First(b => b.transform.parent == null && b.name == "FuwaBlower");
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        var log = new List<string>();
        log.Add("lang purged " + PurgeLang(lang));

        // ---------- スタートの島の柵を開ける ＆ 小道の終わりを決める ----------
        var islandCenter = startArea.Find("Ground").position;
        float ea = EntryAngle * Mathf.Deg2Rad;
        Vector3 u = startArea.TransformDirection(new Vector3(Mathf.Cos(ea), 0f, Mathf.Sin(ea))); u.y = 0; u.Normalize();
        var cl = new List<Vector2>(CtrlBase);
        foreach (float r in new[] { 5.5f, 1.6f }) { var p = islandCenter + u * r; cl.Add(new Vector2(p.x, p.z)); }
        Ctrl = cl.ToArray();
        log.Add(OpenStartFence(startArea));

        // ---------- 小道 ----------
        var pts = Dense(out var dist);
        float len = dist[dist.Count - 1];
        var pathGo = new GameObject("TutorialPath");
        pathGo.transform.SetParent(root, false);
        var mesh = PathMesh(pts, dist, pathGo.transform.worldToLocalMatrix);
        string meshPath = MeshDir + "/Course0_TutorialPath.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); mesh = existing; }
        else AssetDatabase.CreateAsset(mesh, meshPath);
        pathGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        pathGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mat("Course0_PathA"), Mat("Course0_PathB") };
        pathGo.AddComponent<MeshCollider>().sharedMesh = mesh;
        pathGo.layer = 11;
        log.Add($"path {len:0.0}m");

        // ---------- 島（今のスタートの島の地面と下の岩をコピー） ----------
        var groundSrc = startArea.Find("Ground"); var underSrc = startArea.Find("IslandUnder");
        var smallGround = startArea.Find("WarpGround"); var smallUnder = startArea.Find("IslandUnder_Warp");
        Transform Island(string name, Vector3 center, bool big)
        {
            var t = new GameObject(name).transform; t.SetParent(root, false); t.position = new Vector3(center.x, 0f, center.z);
            var g = Dup(big ? groundSrc : smallGround, t, "Ground");
            g.transform.position = new Vector3(center.x, (big ? groundSrc : smallGround).position.y, center.z);
            var u = Dup(big ? underSrc : smallUnder, t, "IslandUnder");
            u.transform.position = new Vector3(center.x, (big ? underSrc : smallUnder).position.y, center.z);
            return t;
        }

        // 入口の島
        At(pts, dist, 0f, out var e0, out var ef);
        var entrance = Island("Entrance", e0, true);
        var er = new Vector3(ef.z, 0, -ef.x);
        // プレイヤーの着く所（小道の方を向く）
        var spot = new GameObject("PlayerSpot").transform; spot.SetParent(entrance, false);
        var sp0 = e0 - ef * 1.6f; spot.position = new Vector3(sp0.x, 3.0f, sp0.z); spot.rotation = Quaternion.LookRotation(ef);
        // 銃の台：スタートの島での「PlayerSpot から見た位置・向き」をそのまま入口の島へ
        var startSpotT = start.Find("PlayerSpot");
        Quaternion rel = spot.rotation * Quaternion.Inverse(startSpotT.rotation);
        var gsSrc = start.Find("GunStand_Course0");
        var gs = Dup(gsSrc, entrance, "GunStand_Tutorial");
        gs.transform.position = spot.position + rel * (gsSrc.position - startSpotT.position);
        gs.transform.rotation = rel * gsSrc.rotation;
        var gsMarkerSrc = start.Find("GunStand_Course0_Marker");
        GameObject gsMarker = null;
        if (gsMarkerSrc != null) { gsMarker = Dup(gsMarkerSrc, entrance, "GunStand_Tutorial_Marker"); gsMarker.transform.position = spot.position + rel * (gsMarkerSrc.position - startSpotT.position); gsMarker.transform.rotation = rel * gsMarkerSrc.rotation; }
        var gsComp = gs.GetComponent<FuwaGunStand>();
        SetU(gsComp, "gunSpot", gs.transform.Find("GunSpot")); if (gsMarker != null) SetU(gsComp, "marker", gsMarker); Sync(gsComp);

        // ロビーへ戻るワープ（スタートの島のものをコピー）と、説明をとばすワープ
        Transform MakeWarp(string name, Vector3 at, string labelJa, string labelEn, Transform playerSpot, Transform courseStart, Transform gunSpot, bool toLobby)
        {
            var w = new GameObject(name).transform; w.SetParent(entrance, false); w.position = at;
            var srcNames = new[] { "WarpToLobby", "WarpToLobby_Trigger", "WarpToLobby_Motes", "WarpToLobby_ProgressRing" };
            Transform trig = null, ring = null;
            foreach (var n in srcNames)
            {
                var src = start.Find(n);
                var c = Dup(src, w, n.Replace("WarpToLobby", name));
                c.transform.position = at + (src.position - start.Find("WarpToLobby").position);
                if (n.EndsWith("_Trigger")) trig = c.transform;
                if (n.EndsWith("_ProgressRing")) ring = c.transform;
            }
            var warp = trig.GetComponent<FuwaCourseWarp>();
            SetU(warp, "progressRing", ring.GetComponent<Renderer>());
            SetU(warp, "chargeSound", trig.GetComponent<AudioSource>());
            var veil = ring.Find("WarpVeil"); if (veil != null) SetU(warp, "progressVeil", veil.GetComponent<Renderer>());
            if (!toLobby)
            {
                SetU(warp, "playerSpot", playerSpot); SetU(warp, "courseStart", courseStart); SetU(warp, "gunSpot", gunSpot);
                SetU(warp, "lobbyReturnSpot", null);
            }
            Sync(warp);
            // 札（いつも見える）
            var labSrc = start.Find("Label");
            var lab = Dup(labSrc, w, "Label"); lab.SetActive(true);
            lab.transform.position = at + Vector3.up * 1.35f;
            var tmp = lab.GetComponent<TMPro.TextMeshPro>(); tmp.text = labelJa;
            AddLang(lang, tmp, labelJa, labelEn);
            return w;
        }
        var startSpot = start.Find("PlayerSpot"); var ballStart = start.Find("BallStart");
        MakeWarp("WarpToLobby", e0 - ef * 1.2f - er * 1.9f + Vector3.up * 0.02f, "ロビーにもどる", "Back to Lobby", null, null, null, true);
        MakeWarp("SkipTutorial", e0 - ef * 1.2f + er * 1.9f + Vector3.up * 0.02f, "説明をとばす", "Skip", startSpot, ballStart, gsSrc.Find("GunSpot"), false);

        // ロビーの「れんしゅう」ワープの行き先を入口の島に
        var lobbyWarp = Object.FindObjectsOfType<FuwaCourseWarp>(true).First(w => w.name == "WarpToCourse0_Trigger");
        SetU(lobbyWarp, "playerSpot", spot); SetU(lobbyWarp, "gunSpot", gs.transform.Find("GunSpot")); Sync(lobbyWarp);

        // ---------- お手本区画 ----------
        var signSrc = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "BestTimes" && t.parent != null && t.parent.name == "FuwaLobby");
        foreach (var bay in Bays)
        {
            At(pts, dist, bay.s, out var c, out var f);
            // 外側＝進む向きの右手（小道は左へ曲がっていくので、右手側がカーブの外。かぜのみちとの間の広い空）
            Vector3 outw = new Vector3(f.z, 0, -f.x);
            var bayRoot = new GameObject("Bay_" + bay.key).transform; bayRoot.SetParent(root, false);
            Vector3 ic = c + outw * (HalfW + 1.6f - 0.25f);
            var isl = Island("Island", ic, false); isl.SetParent(bayRoot, true);
            // お手本は道の進む向きと外向きの間（斜め前）へ撃つ
            Vector3 d = (f + outw).normalized;
            BuildDemo(bayRoot, bay.key, ic, d, outw, f, blowerTmpl, ball, log);
            // 看板：区画の手前（入口側）、道の外側のふちに立てて、来る人の方を向く
            var sign = new GameObject("Sign").transform; sign.SetParent(bayRoot, false);
            Vector3 sp = c - f * 2.0f + outw * (HalfW - 0.35f);
            sign.position = new Vector3(sp.x, Top, sp.z);
            // 文字は forward の反対側から読める。道のまん中を歩いてくる人の方へ少し向ける
            sign.rotation = Quaternion.LookRotation((f + outw * 0.45f).normalized);
            var panel = Dup(signSrc.Find("Panel"), sign, "Panel"); var text = Dup(signSrc.Find("Text"), sign, "Text");
            panel.transform.localPosition = new Vector3(0, 1.55f, 0.01f); panel.transform.localRotation = Quaternion.identity; panel.transform.localScale = Vector3.one * 0.62f;
            var sr = panel.GetComponent<SpriteRenderer>(); sr.size = new Vector2(3.0f, 1.45f);
            text.transform.localPosition = new Vector3(0, 1.55f, 0f); text.transform.localRotation = Quaternion.identity; text.transform.localScale = Vector3.one * 0.62f;
            var tm = text.GetComponent<TMPro.TextMeshPro>(); tm.text = bay.ja; tm.alignment = TMPro.TextAlignmentOptions.Center;
            tm.rectTransform.sizeDelta = new Vector2(2.75f, 1.25f); tm.enableAutoSizing = true; tm.fontSizeMax = 2.2f; tm.fontSizeMin = 0.8f;
            var recs = text.GetComponent<FuwaRecords>(); if (recs != null) Object.DestroyImmediate(recs);
            AddLang(lang, tm, bay.ja, bay.en);
            // 脚
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); leg.name = "Leg"; Object.DestroyImmediate(leg.GetComponent<Collider>());
            leg.transform.SetParent(sign, false); leg.transform.localPosition = new Vector3(0, 0.55f, 0.03f); leg.transform.localScale = new Vector3(0.07f, 0.55f, 0.07f);
            leg.GetComponent<Renderer>().sharedMaterial = Mat("Course_Wood");
            // ためし撃ちの場所（道の上、区画の正面）と、そこへふわふわを呼ぶ範囲
            var trySpot = new GameObject("TrySpot").transform; trySpot.SetParent(bayRoot, false);
            // 下向きショットの区画は、自分で撃つふわふわも地面ギリギリに（下を撃っても上がるのを試せるように）
            trySpot.position = c + f * 0.3f + Vector3.up * (bay.key == "Down" ? 0.22f : 1.15f); trySpot.rotation = Quaternion.LookRotation(f);
            var zone = new GameObject("TryZone"); zone.transform.SetParent(bayRoot, false);
            zone.transform.position = c + Vector3.up * 1.2f; zone.transform.rotation = Quaternion.LookRotation(f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(HalfW * 2f + 4f, 3f, 6.5f);
            zone.layer = 0;
            var tz = UdonSharpUndo.AddComponent<FuwaTryZone>(zone);
            SetU(tz, "ball", ball); SetU(tz, "spot", trySpot); Sync(tz);
        }
        // 小道の終わり〜スタートの島：ふわふわをコースのスタート（キノコ）へ戻す
        {
            At(pts, dist, len - 2.5f, out var c, out var f);
            var zone = new GameObject("ToCourseZone"); zone.transform.SetParent(root, false);
            zone.transform.position = c + Vector3.up * 1.2f; zone.transform.rotation = Quaternion.LookRotation(f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(HalfW * 2f + 1f, 3f, 3f);
            var tz = UdonSharpUndo.AddComponent<FuwaTryZone>(zone);
            SetU(tz, "ball", ball); SetU(tz, "courseStart", ballStart); Sync(tz);
        }

        // ---------- 落ちた時の受け止め（今のれんしゅうのものをコピーして、小道の下に） ----------
        var fcSrc = course.Find("FallCatcher");
        var fc = Dup(fcSrc, root, "FallCatcher");
        fc.transform.rotation = Quaternion.identity;
        Vector3 mn = new Vector3(float.MaxValue, 0, float.MaxValue), mx = new Vector3(float.MinValue, 0, float.MinValue);
        foreach (var p in pts) { mn = Vector3.Min(mn, new Vector3(p.x, 0, p.y)); mx = Vector3.Max(mx, new Vector3(p.x, 0, p.y)); }
        mn -= new Vector3(7, 0, 6); mx += new Vector3(9, 0, 6);   // 南はロビーの下に入らないように控えめに
        fc.transform.position = new Vector3((mn.x + mx.x) * 0.5f, fcSrc.position.y, (mn.z + mx.z) * 0.5f);
        fc.transform.localScale = new Vector3(mx.x - mn.x, fcSrc.lossyScale.y, mx.z - mn.z);
        foreach (var ub in fc.GetComponents<UdonSharp.UdonSharpBehaviour>()) Sync(ub);

        // ---------- まとめ ----------
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            if (go.GetComponent<FuwaDemoStand>() != null || go.GetComponentInParent<FuwaDemoStand>() != null) { GameObjectUtility.SetStaticEditorFlags(go, 0); continue; }
            if (go.GetComponent<Renderer>() != null && go.GetComponent<ParticleSystem>() == null && go.GetComponent<TMPro.TextMeshPro>() == null && go.name != "Label")
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }
        Sync(lang);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "Tutorial built: " + string.Join(" / ", log);
    }

    static bool InGap(Vector3 localP, Vector3 center, float maxR)
    {
        var p = localP - center;
        float r = new Vector2(p.x, p.z).magnitude;
        if (r > maxR) return false;
        float a = Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg;
        return a > GapFrom + 2f && a < GapTo - 2f;   // 両はしの柱は残す
    }

    // スタートの島の柵（かぜのみちと同じメッシュを使っているので、元は触らずに れんしゅう用のコピーを作る）から、
    // 小道が入る所の柱と横木を抜く。見た目は「部品（柱1本・横木1本）ごと」に、当たり判定は三角形ごとに
    static string OpenStartFence(Transform startArea)
    {
        var fence = startArea.Find("Fence"); var fcol = startArea.Find("FenceCollider");
        var srcVis = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Bevel/FuwaCourse_StartFence_Bevel.asset");
        var srcColPath = AssetDatabase.FindAssets("FuwaCourse_StartFenceCollider t:Mesh").Select(AssetDatabase.GUIDToAssetPath).First();
        var srcCol = AssetDatabase.LoadAllAssetsAtPath(srcColPath).OfType<Mesh>().First(m => m.name == "FuwaCourse_StartFenceCollider");
        Vector3 cVis = fence.InverseTransformPoint(startArea.Find("Ground").position);
        Vector3 cCol = fcol.InverseTransformPoint(startArea.Find("Ground").position);

        // 見た目：つながっている部品ごとに、部品の中心が開ける範囲（島のふちの輪の上）にあれば消す
        var v = srcVis.vertices; var tris = srcVis.triangles;
        var parent = Enumerable.Range(0, v.Length).ToArray();
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        var byPos = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000), Mathf.RoundToInt(v[i].y * 1000), Mathf.RoundToInt(v[i].z * 1000));
            if (byPos.TryGetValue(k, out var j)) parent[Find(i)] = Find(j); else byPos[k] = i;
        }
        for (int i = 0; i < tris.Length; i += 3) { parent[Find(tris[i + 1])] = Find(tris[i]); parent[Find(tris[i + 2])] = Find(tris[i]); }
        var sum = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>();
        for (int i = 0; i < v.Length; i++) { int r = Find(i); sum[r] = (sum.TryGetValue(r, out var s0) ? s0 : Vector3.zero) + v[i]; cnt[r] = (cnt.TryGetValue(r, out var c0) ? c0 : 0) + 1; }
        var keepTri = new List<int>(); int removedParts = 0; var removedRoots = new HashSet<int>();
        foreach (var r in sum.Keys) if (InGap(sum[r] / cnt[r], cVis, 3.0f)) { removedRoots.Add(r); removedParts++; }
        for (int i = 0; i < tris.Length; i += 3) if (!removedRoots.Contains(Find(tris[i]))) keepTri.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
        var vis = Object.Instantiate(srcVis); vis.name = "Course0_StartFence_Open"; vis.SetTriangles(keepTri, 0); vis.RecalculateBounds();

        var cv = srcCol.vertices; var ct = srcCol.triangles; var keepC = new List<int>();
        for (int i = 0; i < ct.Length; i += 3)
        {
            var cen = (cv[ct[i]] + cv[ct[i + 1]] + cv[ct[i + 2]]) / 3f;
            if (!InGap(cen, cCol, 3.3f)) keepC.AddRange(new[] { ct[i], ct[i + 1], ct[i + 2] });
        }
        var col = Object.Instantiate(srcCol); col.name = "Course0_StartFenceCollider_Open"; col.SetTriangles(keepC, 0); col.RecalculateBounds();

        Mesh Save(Mesh m, string path)
        {
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (ex == null) { AssetDatabase.CreateAsset(m, path); return m; }
            ex.Clear(); EditorUtility.CopySerialized(m, ex); ex.name = m.name; return ex;
        }
        vis = Save(vis, MeshDir + "/Course0_StartFence_Open.asset");
        col = Save(col, MeshDir + "/Course0_StartFenceCollider_Open.asset");
        fence.GetComponent<MeshFilter>().sharedMesh = vis;
        var mc = fcol.GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = col;

        // 開けた所にある飾りキノコはどける（非表示）
        int hid = 0;
        var mush = startArea.Find("Mushrooms");
        if (mush != null)
            foreach (Transform m in mush)
            {
                bool inGap = InGap(startArea.InverseTransformPoint(m.position), startArea.InverseTransformPoint(startArea.Find("Ground").position), 3.4f);
                if (inGap && m.gameObject.activeSelf) { m.gameObject.SetActive(false); hid++; }
            }
        return $"fence parts -{removedParts}, collider tris {ct.Length / 3}->{keepC.Count / 3}, mushrooms hidden {hid}";
    }

    // 作り直しで消えた看板（null）を、言語の配列から詰める
    static int PurgeLang(FuwaLanguage lang)
    {
        var so = new SerializedObject(lang);
        var tx = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
        var keepT = new List<Object>(); var keepJ = new List<string>(); var keepE = new List<string>();
        int removed = 0;
        for (int i = 0; i < tx.arraySize; i++)
        {
            var o = tx.GetArrayElementAtIndex(i).objectReferenceValue;
            if (o == null) { removed++; continue; }
            keepT.Add(o);
            keepJ.Add(i < pj.arraySize ? pj.GetArrayElementAtIndex(i).stringValue : "");
            keepE.Add(i < pe.arraySize ? pe.GetArrayElementAtIndex(i).stringValue : "");
        }
        if (removed == 0) return 0;
        tx.arraySize = keepT.Count; pj.arraySize = keepT.Count; pe.arraySize = keepT.Count;
        for (int i = 0; i < keepT.Count; i++)
        {
            tx.GetArrayElementAtIndex(i).objectReferenceValue = keepT[i];
            pj.GetArrayElementAtIndex(i).stringValue = keepJ[i];
            pe.GetArrayElementAtIndex(i).stringValue = keepE[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return removed;
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

    // ---------- お手本台 ----------
    static void BuildDemo(Transform bayRoot, string key, Vector3 islandCenter, Vector3 d, Vector3 outw, Vector3 f, FuwaBlower tmpl, FuwaBall ballSrc, List<string> log)
    {
        var floorTop = new GameObject("FloorTop").transform; floorTop.SetParent(bayRoot, false);
        floorTop.position = new Vector3(islandCenter.x, IslandTop, islandCenter.z);
        // 撃つ向き：ふつうは道とほぼ平行（道から見て横から飛び方が見える）。
        // 「真ん中に寄る」だけは道から遠ざかる向き（横へのずれが、道から見て左右の動きになるように）
        Vector3 along = (f + outw * 0.3f).normalized;
        Vector3 away = (outw + f * 0.25f).normalized;
        switch (key)
        {
            case "TapCharge":
            {
                var lat = Vector3.Cross(Vector3.up, along).normalized;
                MakeLane(bayRoot, "Lane_Tap", islandCenter - along * 1.2f - lat * 0.55f, along, 0.95f, new Vector3(0, 0.05f, 1.0f), new[] { 1.1f }, new[] { 0f }, null, null, false, tmpl, ballSrc, floorTop);
                MakeLane(bayRoot, "Lane_Charge", islandCenter - along * 1.2f + lat * 0.55f, along, 0.95f, new Vector3(0, 0.05f, 1.0f), new[] { 1.1f }, new[] { 1f }, null, null, false, tmpl, ballSrc, floorTop);
                break;
            }
            case "Down":
                // 銃を高めにして、斜め下のふわふわを撃つ（ねらった線も見せる：下を向いているのに上がる）
                MakeLane(bayRoot, "Lane", islandCenter - along * 1.0f, along, 1.2f, new Vector3(0, -1.04f, 0.9f), new[] { 1.1f }, new[] { 1f }, null, null, true, tmpl, ballSrc, floorTop);
                break;
            case "Center":
                // ふわふわの横（道の進む向きの側）をねらう。ねらった線を見せて、そこへ寄っていくのを見せる
                MakeLane(bayRoot, "Lane", islandCenter - away * 1.25f, away, 0.95f, new Vector3(0, 0.05f, 1.1f), new[] { 1.1f }, new[] { 1f }, new[] { new Vector3(-0.5f, 0f, 0f) }, null, true, tmpl, ballSrc, floorTop);
                break;
            case "Pull":
                // 少し離れたふわふわを、長く溜めて引き寄せてから撃つ
                MakeLane(bayRoot, "Lane", islandCenter - along * 1.3f, along, 0.95f, new Vector3(0, 0.05f, 1.42f), new[] { 3.6f }, new[] { 1f }, null, new[] { 2.5f }, false, tmpl, ballSrc, floorTop);
                break;
        }
    }

    // ballOffset：銃口から見た (右, 上, 前) のふわふわの位置
    static void MakeLane(Transform parent, string name, Vector3 basePos, Vector3 d, float gunHeight, Vector3 ballOffset, float[] times, float[] charges, Vector3[] aimOffsets, float[] holds, bool showAimLine, FuwaBlower tmpl, FuwaBall ballSrc, Transform floorTop)
    {
        var lane = new GameObject(name).transform; lane.SetParent(parent, false);
        lane.position = new Vector3(basePos.x, IslandTop, basePos.z); lane.rotation = Quaternion.LookRotation(d);
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); post.name = "Post"; Object.DestroyImmediate(post.GetComponent<Collider>());
        post.transform.SetParent(lane, false); post.transform.localPosition = new Vector3(0, gunHeight * 0.5f - 0.02f, -0.08f); post.transform.localScale = new Vector3(0.07f, gunHeight * 0.5f, 0.07f);
        post.GetComponent<Renderer>().sharedMaterial = Mat("Course_Wood");
        var gun = new GameObject("DemoGun").transform; gun.SetParent(lane, false); gun.localPosition = new Vector3(0, gunHeight, 0);
        foreach (var cn in new[] { "Visual", "Nozzle" }) { var cp = Object.Instantiate(tmpl.transform.Find(cn).gameObject, gun, false); cp.name = cn; }
        foreach (var g in gun.GetComponentsInChildren<Renderer>(true)) if (g.name.StartsWith("GunGauge")) g.enabled = false;
        var nozzle = gun.Find("Nozzle");
        var bs = new GameObject("BallStart").transform; bs.SetParent(lane, false);
        Vector3 lat = lane.right;
        bs.position = nozzle.position + lat * ballOffset.x + Vector3.up * ballOffset.y + d * ballOffset.z;
        var bt = new GameObject("DemoBall").transform; bt.SetParent(lane, false); bt.position = bs.position; bt.localScale = ballSrc.transform.localScale;
        bt.gameObject.AddComponent<MeshFilter>().sharedMesh = ballSrc.GetComponent<MeshFilter>().sharedMesh;
        var mr = bt.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterials = ballSrc.GetComponent<MeshRenderer>().sharedMaterials;
        var pe = Object.Instantiate(ballSrc.transform.Find("PopEffect").gameObject, lane, false); pe.name = "PopEffect"; pe.transform.localScale = Vector3.one;
        var snd = new GameObject("PopSfx"); snd.transform.SetParent(lane, false);
        var a = snd.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/sfx_burst.wav"); a.playOnAwake = false; a.spatialBlend = 1f; a.volume = 0.5f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 1f; a.maxDistance = 14f;
        snd.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
        var ds = UdonSharpUndo.AddComponent<FuwaDemoStand>(lane.gameObject);
        SetU(ds, "tuning", tmpl); SetU(ds, "ballTuning", ballSrc); SetU(ds, "gun", gun); SetU(ds, "nozzle", nozzle); SetU(ds, "ball", bt); SetU(ds, "ballFur", mr);
        SetU(ds, "ballStart", bs); SetU(ds, "floor", floorTop); SetU(ds, "popEffect", pe.GetComponent<ParticleSystem>()); SetU(ds, "popSound", a);
        SetU(ds, "puffEffect", nozzle.Find("Puff").GetComponent<ParticleSystem>()); SetU(ds, "airRing", nozzle.Find("Puff/AirRing").GetComponent<ParticleSystem>());
        SetU(ds, "chargeParticles", nozzle.Find("ChargeParticles").GetComponent<ParticleSystem>()); SetU(ds, "fullBurst", nozzle.Find("FullBurst").GetComponent<ParticleSystem>());
        SetU(ds, "chargedSparkle", nozzle.Find("ChargedSparkle").GetComponent<ParticleSystem>()); SetU(ds, "chargeOrb", nozzle.Find("ChargeOrb"));
        SetU(ds, "puffSound", nozzle.GetComponent<AudioSource>()); SetU(ds, "puffBigSound", nozzle.Find("PuffBigSfx").GetComponent<AudioSource>());
        SetU(ds, "chargeSound", nozzle.Find("ChargeSfx").GetComponent<AudioSource>()); SetU(ds, "chargedSound", nozzle.Find("ChargedSfx").GetComponent<AudioSource>());
        SetU(ds, "pumpBulb", gun.Find("Visual/PumpBulb"));
        SetArr(ds, "shotTimes", times); SetArr(ds, "shotCharges", charges);
        SetArrV(ds, "shotAimOffsets", aimOffsets ?? new Vector3[times.Length]);
        SetArr(ds, "shotHolds", holds ?? new float[times.Length]);
        SetUF(ds, "cycleLength", holds != null ? 6f : 4f);
        if (showAimLine)
        {
            var lgo = new GameObject("AimLine"); lgo.transform.SetParent(lane, false);
            var lr = lgo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.positionCount = 2; lr.widthMultiplier = 0.025f;
            lr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            lr.startColor = new Color(1f, 1f, 1f, 0.75f); lr.endColor = new Color(1f, 1f, 1f, 0f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.enabled = false;
            SetU(ds, "aimLine", lr);
        }
        // 銃は最初からふわふわの方を向けておく
        gun.rotation = Quaternion.LookRotation((bs.position - nozzle.position).normalized);
        Sync(ds);
    }
}
