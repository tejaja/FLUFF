using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// チュートリアル（FuwaCourse0）をまるごと一本道に組み立てる。何度実行しても同じ形に作り直す（FuwaCourse0/Tutorial を消して作る）。
// 入口の島（ロビーから着く）→ 説明の小道（区画 BaySpan ごとに壁1枚、お手本台＋看板＋ためし撃ちの場所）→ キノコの島（元のスタートの島を動かす）
// → 短いれんしゅうコース（チェックポイント・ゴールは元のものを動かす）。道はコースの前へまっすぐ、左右にゆるくうねる。
// 区画を足す（Bays に足す）と、キノコの島から先も自動で後ろへずれる。
public static class FuwaPracticeTutorial
{
    const string Course = "FuwaCourse0";
    const string RootName = "Tutorial";
    const string MeshDir = "Assets/FuwaCourse/Meshes/Tutorial";

    // 道の中心線の制御点（ワールドのXZ）。Run で Lane() から作る
    static Vector2[] Ctrl = new Vector2[0];
    // スタートの島の柵（StartArea のローカルで、+x から +z へ測った角度）。この範囲の柵をなくして小道を通す
    const float GapFrom = -128f, GapTo = -52f;   // 後ろ（-z、小道が入ってくる側）を開ける
    const float Top = 3.0f;        // 道の上面（今のれんしゅうの道と同じ）
    const float Thick = 0.3f;
    const float HalfW = 1.75f;
    const float Stripe = 0.6f;     // しましまの幅（今の道と同じ）
    // 島（BevelCyl）の上面の高さ。Run で実物の地面（WarpGround）の上面から測る（道の上面3.0より1cmちょっと高い）
    static float IslandTop = 3.012f;

    // お手本区画：小道の上の位置（入口からの距離 m）と中身
    struct Bay
    {
        public float s; public string key; public string ja; public string en;
        public Bay(float s, string key, string ja, string en) { this.s = s; this.key = key; this.ja = ja; this.en = en; }
    }
    const float BaySpan = 11f;   // 区画の長さ（となりの区画との間隔。さかいめに壁が1枚）
    static readonly Bay[] Bays =
    {
        new Bay(7.5f, "TapCharge",
            "<size=130%><color=#FF6B5C>ちょん押し と 溜め</color></size>\nちょん押し：すぐ撃てる・ちかく\n長押し：溜めるほど強く・とおく\n銃口の玉が金色になったら満タン",
            "<size=130%><color=#FF6B5C>Tap & Charge</color></size>\nTap: quick shot, short\nHold: the longer, the stronger\nOrb turns gold = full charge"),
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

    // ---------- 一本道のレイアウト ----------
    // 入口の島の中心（＝元のれんしゅうのスタートの島の位置）から、コースの前（FuwaCourse0 の forward）へまっすぐ、左右にゆるくうねる
    static readonly Vector3 Origin = new Vector3(-2.01f, 0f, 14.84f);
    const float Sway = 1.3f, SwayLen = 40f;   // うねりの幅と周期(m)
    const float FirstWall = 4.6f;             // 入口の島（半径3）を出てすぐの壁
    const float ToMushroom = 5.0f;            // 最後の壁からキノコの島の中心まで
    const float ToCheckpoint = 12f, ToGoal = 25f;   // キノコの島からチェックポイント・ゴールまで

    static Vector3 Lane(Vector3 fwd, float s)
    {
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        return Origin + fwd * s + right * (Sway * Mathf.Sin(s * 2f * Mathf.PI / SwayLen));
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
        var goal = course.Find("Goal");
        var cp = course.Find("Checkpoint1");
        var ball = Object.FindObjectsOfType<FuwaBall>(true).First(b => b.name == "FuwaBall");
        var blowerTmpl = Object.FindObjectsOfType<FuwaBlower>(true).First(b => b.transform.parent == null && b.name == "FuwaBlower");
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        var log = new List<string>();
        log.Add("lang purged " + PurgeLang(lang));

        // 元のれんしゅうの道・飾りはOFF（消さない：ほかのスクリプトの参照が切れないように）
        foreach (var n in new[] { "PathMesh", "Deco", "EdgeProps", "Bubbles" })
        {
            var t = course.Find(n); if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); log.Add("off " + n); }
        }

        // ---------- 位置を決める ----------
        Vector3 F = course.forward; F.y = 0; F.Normalize();
        int nb = Bays.Length;
        var bayS = new float[nb];
        for (int i = 0; i < nb; i++) bayS[i] = FirstWall + BaySpan * (i + 0.5f);
        float lastWall = FirstWall + BaySpan * nb;
        float sMush = lastWall + ToMushroom, sCp = sMush + ToCheckpoint, sGoal = sMush + ToGoal;
        var cl = new List<Vector2>();
        float sEnd = sGoal - 2.6f;   // 道はゴールの島のふちまで（島のまん中のリングにかぶらないように）
        for (float s = 0f; s < sEnd - 0.5f; s += 4f) { var p = Lane(F, s); cl.Add(new Vector2(p.x, p.z)); }
        { var p = Lane(F, sEnd); cl.Add(new Vector2(p.x, p.z)); }
        Ctrl = cl.ToArray();
        var pts = Dense(out var dist);
        float len = dist[dist.Count - 1];
        // s（入口からまっすぐ測った距離）→ 道の上の距離（うねりで少し長いぶんを比で合わせる）
        float S(float s) { return s * len / sEnd; }

        // ---------- 道（入口の島〜ゴールの島まで一本） ----------
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

        // ---------- 島（スタートの島の地面と下の岩をコピー） ----------
        var groundSrc = startArea.Find("Ground"); var underSrc = startArea.Find("IslandUnder");
        var smallGround = startArea.Find("WarpGround"); var smallUnder = startArea.Find("IslandUnder_Warp");
        IslandTop = smallGround.GetComponent<Renderer>().bounds.max.y;
        Transform Island(string name, Vector3 center, bool big)
        {
            var t = new GameObject(name).transform; t.SetParent(root, false); t.position = new Vector3(center.x, 0f, center.z);
            var srcG = big ? groundSrc : smallGround; var srcU = big ? underSrc : smallUnder;
            var g = Dup(srcG, t, "Ground"); g.SetActive(true);
            g.transform.position = new Vector3(center.x, srcG.position.y, center.z);
            var u = Dup(srcU, t, "IslandUnder"); u.SetActive(true);
            u.transform.position = new Vector3(center.x, srcU.position.y, center.z);
            return t;
        }

        // ---------- キノコの島（元のスタートの島）を、小道の終わりへ動かす ----------
        At(pts, dist, S(sMush), out var mc, out var mf);
        start.position = new Vector3(mc.x, start.position.y, mc.z);
        start.rotation = Quaternion.LookRotation(mf);
        // 後ろのワープの小島と橋はもう使わない（入口の島にロビーへのワープがある）
        foreach (var n in new[] { "WarpGround", "IslandUnder_Warp", "Bridge", "BridgeWood" }) { var t = startArea.Find(n); if (t != null) t.gameObject.SetActive(false); }
        foreach (Transform t in start) if (t.name.StartsWith("WarpToLobby")) t.gameObject.SetActive(false);
        Transform warpLabel = null;
        foreach (Transform t in start) { var tm0 = t.GetComponent<TMPro.TextMeshPro>(); if (t.name == "Label" && tm0 != null && tm0.text.Contains("ロビー")) warpLabel = t; }
        log.Add(OpenStartFence(startArea));

        // ---------- チェックポイントとゴールを、れんしゅうコースの道の上へ ----------
        At(pts, dist, S(sCp), out var cc, out var cf);
        cp.position = new Vector3(cc.x, cp.position.y, cc.z); cp.rotation = Quaternion.LookRotation(cf);
        Vector3 gc = Lane(F, sGoal), gf = (Lane(F, sGoal) - Lane(F, sGoal - 1f)); gf.y = 0; gf.Normalize();
        goal.position = new Vector3(gc.x, goal.position.y, gc.z); goal.rotation = Quaternion.LookRotation(gf);   // ゴールの柵の入口はローカルの -z 側

        // ---------- 入口の島 ----------
        At(pts, dist, 0f, out var e0, out var ef);
        var entrance = Island("Entrance", e0, true);
        var er = new Vector3(ef.z, 0, -ef.x);
        var spot = new GameObject("PlayerSpot").transform; spot.SetParent(entrance, false);
        var sp0 = e0 - ef * 1.0f; spot.position = new Vector3(sp0.x, 3.0f, sp0.z); spot.rotation = Quaternion.LookRotation(ef);
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
            var srcPad = start.Find("WarpToLobby");
            foreach (var n in srcNames)
            {
                var src = start.Find(n);
                var c = Dup(src, w, n.Replace("WarpToLobby", name)); c.SetActive(true);
                c.transform.position = at + (src.position - srcPad.position);
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
            var labSrc = warpLabel != null ? warpLabel : start.Find("Label");
            var lab = Dup(labSrc, w, "Label"); lab.SetActive(true);
            lab.transform.position = at + Vector3.up * 1.35f;
            var tmp = lab.GetComponent<TMPro.TextMeshPro>(); tmp.text = labelJa;
            AddLang(lang, tmp, labelJa, labelEn);
            return w;
        }
        var startSpot = start.Find("PlayerSpot"); var ballStart = start.Find("BallStart");
        MakeWarp("WarpToLobby", e0 - ef * 0.9f - er * 1.9f + Vector3.up * 0.02f, "ロビーにもどる", "Back to Lobby", null, null, null, true);
        MakeWarp("SkipTutorial", e0 - ef * 0.9f + er * 1.9f + Vector3.up * 0.02f, "説明をとばす", "Skip", startSpot, ballStart, gsSrc.Find("GunSpot"), false);
        if (warpLabel != null) warpLabel.gameObject.SetActive(false);

        // ロビーの「チュートリアル」ワープの行き先を入口の島に
        var lobbyWarp = Object.FindObjectsOfType<FuwaCourseWarp>(true).First(w => w.name == "WarpToCourse0_Trigger");
        SetU(lobbyWarp, "playerSpot", spot); SetU(lobbyWarp, "gunSpot", gs.transform.Find("GunSpot")); Sync(lobbyWarp);

        // ---------- お手本区画 ----------
        var signSrc = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "BestTimes" && t.parent != null && t.parent.name == "FuwaLobby");
        for (int bi = 0; bi < nb; bi++)
        {
            var bay = Bays[bi];
            At(pts, dist, S(bayS[bi]), out var c, out var f);
            // お手本の島：左右交互（偶数番は右手・奇数番は左手）
            Vector3 outw = new Vector3(f.z, 0, -f.x) * ((bi % 2 == 0) ? 1f : -1f);
            var bayRoot = new GameObject("Bay_" + bay.key).transform; bayRoot.SetParent(root, false);
            Vector3 ic = c + outw * (HalfW + 1.6f - 0.25f);
            var isl = Island("Island", ic, false); isl.SetParent(bayRoot, true);
            BuildDemo(bayRoot, bay.key, ic, f, outw, f, blowerTmpl, ball, log);
            // 看板：区画の手前（入口側）、お手本の島の側の道のふちに立てて、来る人の方を向く
            var sign = new GameObject("Sign").transform; sign.SetParent(bayRoot, false);
            Vector3 sp = c - f * 3.4f + outw * (HalfW - 0.35f);
            sign.position = new Vector3(sp.x, Top, sp.z);
            sign.rotation = Quaternion.LookRotation((f + outw * 0.45f).normalized);
            var panel = Dup(signSrc.Find("Panel"), sign, "Panel"); var text = Dup(signSrc.Find("Text"), sign, "Text");
            panel.transform.localPosition = new Vector3(0, 1.55f, 0.01f); panel.transform.localRotation = Quaternion.identity; panel.transform.localScale = Vector3.one * 0.62f;
            var sr = panel.GetComponent<SpriteRenderer>(); sr.size = new Vector2(3.0f, 1.45f);
            text.transform.localPosition = new Vector3(0, 1.55f, 0f); text.transform.localRotation = Quaternion.identity; text.transform.localScale = Vector3.one * 0.62f;
            var tm = text.GetComponent<TMPro.TextMeshPro>(); tm.text = bay.ja; tm.alignment = TMPro.TextAlignmentOptions.Center;
            tm.rectTransform.sizeDelta = new Vector2(2.75f, 1.25f); tm.enableAutoSizing = true; tm.fontSizeMax = 2.2f; tm.fontSizeMin = 0.8f;
            var recs = text.GetComponent<FuwaRecords>(); if (recs != null) Object.DestroyImmediate(recs);
            AddLang(lang, tm, bay.ja, bay.en);
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); leg.name = "Leg"; Object.DestroyImmediate(leg.GetComponent<Collider>());
            leg.transform.SetParent(sign, false); leg.transform.localPosition = new Vector3(0, 0.55f, 0.03f); leg.transform.localScale = new Vector3(0.07f, 0.55f, 0.07f);
            leg.GetComponent<Renderer>().sharedMaterial = Mat("Course_Wood");
            // ためし撃ちの場所（区画のまん中の少し手前）と、そこへふわふわを呼ぶ範囲（区画まるごと）
            var trySpot = new GameObject("TrySpot").transform; trySpot.SetParent(bayRoot, false);
            At(pts, dist, S(bayS[bi] - 0.5f), out var tc, out var tf);
            trySpot.position = tc + Vector3.up * (bay.key == "Down" ? 0.4f : 1.15f); trySpot.rotation = Quaternion.LookRotation(tf);
            var zone = new GameObject("TryZone"); zone.transform.SetParent(bayRoot, false);
            zone.transform.position = c + Vector3.up * 1.2f; zone.transform.rotation = Quaternion.LookRotation(f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(HalfW * 2f + 4f, 3f, BaySpan - 0.2f);
            zone.layer = 0;
            var tz = UdonSharpUndo.AddComponent<FuwaTryZone>(zone);
            SetU(tz, "ball", ball); SetU(tz, "spot", trySpot); Sync(tz);
        }
        // 区画のさかいめに、ふわふわだけ止める壁を1枚ずつ（となりの区画と共通。横＝道の外は開けておく）
        {
            var walls = new GameObject("Barriers").transform; walls.SetParent(root, false);
            for (int i = 0; i <= nb; i++)
            {
                At(pts, dist, S(FirstWall + BaySpan * i), out var wc, out var wf);
                MakeBarrier(walls, "Barrier_" + i, wc, wf);
            }
        }
        // 最後の壁〜キノコの島：ふわふわをコースのスタート（キノコ）へ戻す
        {
            At(pts, dist, S(lastWall + 1.6f), out var c, out var f);
            var zone = new GameObject("ToCourseZone"); zone.transform.SetParent(root, false);
            zone.transform.position = c + Vector3.up * 1.2f; zone.transform.rotation = Quaternion.LookRotation(f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(HalfW * 2f + 1f, 3f, 2.6f);
            var tz = UdonSharpUndo.AddComponent<FuwaTryZone>(zone);
            SetU(tz, "ball", ball); SetU(tz, "courseStart", ballStart); Sync(tz);
        }

        // ---------- 落ちた時の受け止め：れんしゅうの FallCatcher を一本道の下に合わせる ----------
        var fcT = course.Find("FallCatcher");
        {
            var mid = Lane(F, sGoal * 0.5f);
            fcT.rotation = Quaternion.LookRotation(F);
            fcT.position = new Vector3(mid.x, fcT.position.y, mid.z);
            fcT.localScale = new Vector3(36f, fcT.localScale.y, sGoal + 24f);
        }

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
        return "Tutorial built: " + string.Join(" / ", log) + $" / mushroom s={sMush:0} goal s={sGoal:0}";
    }

    // 島の上面の円の中に、dir と平行なしま模様（1本おき）。lineThrough の点（銃口）を通る線がしまの真ん中に来るようにそろえる
    static void MakeStripes(Transform parent, Vector3 center, Vector3 dir, Vector3 lineThrough)
    {
        const float R = 1.5f, Band = 0.22f;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float shift = Vector3.Dot(lineThrough - center, side);   // 銃口の線の、島の中心からの横のずれ
        var V = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
        // 頂点はワールドの向きで作るので、物体の回転はワールドで0にする（親のコースの回転がかかると向きがずれる）
        var go = new GameObject("DirStripes"); go.transform.SetParent(parent, false); go.transform.position = center; go.transform.rotation = Quaternion.identity;
        for (int k = -10; k <= 10; k++)
        {
            if ((k & 1) != 0) continue;
            float o0 = shift + (k - 0.5f) * Band, o1 = shift + (k + 0.5f) * Band;
            // 円の中だけ（細かく区切って、円のふちに沿わせる）
            int n = 10;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(o0, o1, (float)i / n), b = Mathf.Lerp(o0, o1, (float)(i + 1) / n);
                if (Mathf.Abs(a) >= R || Mathf.Abs(b) >= R) continue;
                float la = Mathf.Sqrt(R * R - a * a), lb = Mathf.Sqrt(R * R - b * b);
                int bi = V.Count;
                V.Add(side * a - dir * la); V.Add(side * a + dir * la); V.Add(side * b - dir * lb); V.Add(side * b + dir * lb);
                for (int j = 0; j < 4; j++) C.Add(new Color(1f, 1f, 1f, 0.32f));
                T.AddRange(new[] { bi, bi + 1, bi + 2, bi + 2, bi + 1, bi + 3 });
            }
        }
        // 上向きにそろえる
        for (int i = 0; i < T.Count; i += 3)
            if (Vector3.Cross(V[T[i + 1]] - V[T[i]], V[T[i + 2]] - V[T[i]]).y < 0f) { int tmp = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = tmp; }
        var m = new Mesh { name = "Tutorial_DirStripes" }; m.SetVertices(V); m.SetColors(C); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        string path = MeshDir + "/Tutorial_DirStripes.asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) AssetDatabase.CreateAsset(m, path); else { ex.Clear(); EditorUtility.CopySerialized(m, ex); m = ex; }
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // ふわふわだけ止める壁：Walkthrough レイヤー（ふわふわ＝Walkthrough とは当たる、プレイヤーとは当たらない）。跳ね返りは岩と同じ
    const float BarrierW = HalfW * 2f + 0.6f, BarrierH = 4.2f;   // 真上に撃つと3.5mくらい上がるので高め（上はシェーダーで消える）
    static void MakeBarrier(Transform parent, string name, Vector3 center, Vector3 fwd)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = center + Vector3.up * (BarrierH * 0.5f - 0.02f);
        go.transform.rotation = Quaternion.LookRotation(fwd);
        go.transform.localScale = new Vector3(BarrierW, BarrierH, 1f);
        go.layer = LayerMask.NameToLayer("Walkthrough");
        var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(1f, 1f, 0.12f);
        bc.sharedMaterial = AssetDatabase.FindAssets("RockBounce t:PhysicMaterial").Select(AssetDatabase.GUIDToAssetPath).Select(p => AssetDatabase.LoadAssetAtPath<PhysicMaterial>(p)).FirstOrDefault();
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = BarrierMat();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    static Material BarrierMat()
    {
        const string path = "Assets/FuwaCourse/Materials/SoftBarrier.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/SoftBarrier")); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    // 柵の部品を抜くか：島のふちの輪より外（元の橋・ワープの小島の柵）は全部、輪の上は開ける角度の範囲だけ
    static bool InGap(Vector3 localP, Vector3 center, float maxR)
    {
        var p = localP - center;
        float r = new Vector2(p.x, p.z).magnitude;
        if (r > maxR) return true;
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
        foreach (var r in sum.Keys) if (InGap(sum[r] / cnt[r], cVis, 3.05f)) { removedRoots.Add(r); removedParts++; }
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
        // 遠くで切り替わる軽い版（Fence/Fence_Far）も同じように開ける（こっちは間引いたメッシュなので三角形ごとに）
        var farT = fence.Find("Fence_Far");
        if (farT != null)
        {
            var srcFar = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Generated/Far/FuwaCourse_StartFence_Bevel_Far.asset");
            if (srcFar != null)
            {
                Vector3 cFar = farT.InverseTransformPoint(startArea.Find("Ground").position);
                var fv = srcFar.vertices; var ft = srcFar.triangles; var keepF = new List<int>();
                for (int i = 0; i < ft.Length; i += 3)
                {
                    var cen = (fv[ft[i]] + fv[ft[i + 1]] + fv[ft[i + 2]]) / 3f;
                    if (!InGap(cen, cFar, 3.05f)) keepF.AddRange(new[] { ft[i], ft[i + 1], ft[i + 2] });
                }
                var far = Object.Instantiate(srcFar); far.name = "Course0_StartFence_Open_Far"; far.SetTriangles(keepF, 0); far.RecalculateBounds();
                far = Save(far, MeshDir + "/Course0_StartFence_Open_Far.asset");
                farT.GetComponent<MeshFilter>().sharedMesh = far;
            }
        }
        var mc = fcol.GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = col;

        // 開けた所にある飾りキノコはどける（非表示）
        int hid = 0;
        var mush = startArea.Find("Mushrooms");
        if (mush != null)
            foreach (Transform m in mush)
            {
                bool inGap = InGap(startArea.InverseTransformPoint(m.position), startArea.InverseTransformPoint(startArea.Find("Ground").position), 3.4f);
                m.gameObject.SetActive(!inGap);
                if (inGap) hid++;
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
                MakeLane(bayRoot, "Lane_Tap", islandCenter - along * 1.2f - lat * 0.55f, along, 0.95f, new Vector3(0, 0.05f, 1.0f), new[] { 1.3f }, new[] { 0f }, null, null, false, tmpl, ballSrc, floorTop);
                MakeLane(bayRoot, "Lane_Charge", islandCenter - along * 1.2f + lat * 0.55f, along, 0.95f, new Vector3(0, 0.05f, 1.0f), new[] { 1.3f }, new[] { 1f }, null, null, false, tmpl, ballSrc, floorTop);
                break;
            }
            case "Down":
                // 銃を高めにして、斜め下のふわふわを撃つ（ねらった線も見せる：下を向いているのに上がる）
                MakeLane(bayRoot, "Lane", islandCenter - along * 1.0f, along, 1.2f, new Vector3(0, -0.86f, 0.9f), new[] { 1.3f }, new[] { 1f }, null, null, true, tmpl, ballSrc, floorTop);
                break;
            case "Center":
                // ふわふわの横（道の進む向きの側）をねらう。ねらった線を見せて、そこへ寄っていくのを見せる
                MakeLane(bayRoot, "Lane", islandCenter - away * 1.25f, away, 0.95f, new Vector3(0, 0.05f, 1.1f), new[] { 1.3f }, new[] { 1f }, new[] { new Vector3(-0.5f, 0f, 0f) }, null, true, tmpl, ballSrc, floorTop);
                {
                    // 島の上に、撃つ向き（銃の向き）と平行な縦じま。ふわふわがどっちへ寄っていくか分かりやすいように
                    var lane = bayRoot.Find("Lane"); var gunT = lane.Find("DemoGun"); var nz = gunT.Find("Nozzle"); var bs = lane.Find("BallStart");
                    // 銃の向き（MakeLane で決めて、実行中は動かない）にそろえる
                    Vector3 sdir = gunT.forward; sdir.y = 0; sdir.Normalize();
                    MakeStripes(bayRoot, new Vector3(islandCenter.x, IslandTop + 0.012f, islandCenter.z), sdir, nz.position);
                }
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
        // 銃は台の棒なしで浮かせる（見えない誰かが持って撃っている見本。2026-10-08）
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
        // 満タンの弾は、満タンになってから少し間を置いて撃つ（最速だと「溜めて撃つ」感じが出ないので）
        if (holds == null) { holds = new float[times.Length]; for (int i = 0; i < holds.Length; i++) holds[i] = charges[i] >= 1f ? 0.4f : 0f; }
        SetArr(ds, "shotHolds", holds);
        SetUF(ds, "cycleLength", 4f);
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
        // 銃の向きはここで決めて、実行中は動かさない（keepGunPose）。ねらう所（ふわふわの中心＋ずれ）へ、
        // 銃口が回りこむぶんも含めてぴったり向くまで向き直りをくり返す
        Vector3 off0 = (aimOffsets != null && aimOffsets.Length > 0) ? aimOffsets[0] : Vector3.zero;
        for (int it = 0; it < 30; it++)
        {
            Vector3 fw = (bs.position - nozzle.position).normalized;
            Vector3 rt = Vector3.Cross(Vector3.up, fw).normalized;
            Vector3 up2 = Vector3.Cross(fw, rt);
            Vector3 aimPt = bs.position + rt * off0.x + up2 * off0.y + fw * off0.z;
            gun.rotation = Quaternion.LookRotation((aimPt - nozzle.position).normalized, Vector3.up);
        }
        SetUB(ds, "keepGunPose", true);
        Sync(ds);
    }
}
