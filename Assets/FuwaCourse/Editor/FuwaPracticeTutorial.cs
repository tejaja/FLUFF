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
            "<size=130%><color=#FF6B5C>ちょん押し と 溜め</color></size>\nちょん押し：すぐ撃てる・ちかく\n長押し：溜めるほど強く・とおく\nキラッと光ったら満タン",
            "<size=130%><color=#FF6B5C>Tap & Charge</color></size>\nTap: quick shot, short\nHold: the longer, the stronger\nIt sparkles when fully charged"),
        new Bay(15.0f, "Down",
            "<size=130%><color=#FF6B5C>下を撃っても上がる</color></size>\n風はいつもふわっと持ち上げる\n床に押しつけたりしない",
            "<size=130%><color=#FF6B5C>Shoot down, it goes up</color></size>\nThe wind always lifts the fluff\nIt never pushes it into the floor"),
        new Bay(0f, "Steep",
            "<size=130%><color=#FF6B5C>上を向きすぎない</color></size>\n真上に近いほど、高く上がるだけ\n前に進めたい時は\n低めに撃とう",
            "<size=130%><color=#FF6B5C>Don't aim too high</color></size>\nThe steeper you shoot,\nthe more it just goes up\nAim low to move it forward"),
        new Bay(22.5f, "Center",
            "<size=130%><color=#FF6B5C>風の真ん中に寄る</color></size>\nふわふわの横を撃っても\n銃の正面の方へ寄りながら飛ぶ",
            "<size=130%><color=#FF6B5C>Pulled to the center</color></size>\nShoot beside the fluff and\nit drifts toward where your gun points"),
        new Bay(0f, "Air",
            "<size=130%><color=#FF6B5C>空気ぎれに注意</color></size>\n連打すると空気がなくなる\n溜めると一気にもどる\n（待ってもゆっくりもどる）",
            "<size=130%><color=#FF6B5C>Watch your air</color></size>\nRapid taps use up your air\nCharging refills it fast\n(waiting refills it slowly)"),
        new Bay(0f, "Wind",
            "<size=130%><color=#FF6B5C>風に注意</color></size>\n風でふわふわが流される\n溜めて撃った直後は\n流されにくい",
            "<size=130%><color=#FF6B5C>Watch the wind</color></size>\nThe wind pushes the fluff around\nRight after a charged shot,\nit resists the wind"),
        new Bay(0f, "Monster",
            "<size=130%><color=#FF6B5C>まものに注意</color></size>\nふわふわめがけて体当たりしてくる\n撃って押し返そう",
            "<size=130%><color=#FF6B5C>Watch out for monsters</color></size>\nThey ram your fluff away\nShoot them back!"),
        // （「横風」「まもの」は一度外したが、てじゃが歩いてみて「そんなに長くなかった」ので戻した 2026-10-08）
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

    // ---------- 道の形（道の上の距離 d ごとの、半分の幅・上面の高さ・溝） ----------
    // 説明の小道（キノコの島まで）は幅 HalfW・高さ Top のまま。最後の本番の道は広く、登り坂と溝がある（Run で決める）
    static System.Func<float, float> PathHalfW = d => HalfW;
    static System.Func<float, float> PathTop = d => Top;
    static float GrooveD0 = -1f, GrooveD1 = -1f;   // 溝（道が切れている所）の道の上の距離。なければ -1

    // 距離 s の位置（高さは道の上面）と向き（水平）
    static void At(List<Vector2> pts, List<float> dist, float s, out Vector3 pos, out Vector3 fwd)
    {
        int i = 1;
        while (i < dist.Count - 1 && dist[i] < s) i++;
        float t = Mathf.InverseLerp(dist[i - 1], dist[i], s);
        Vector2 p = Vector2.Lerp(pts[i - 1], pts[i], t);
        Vector2 d = (pts[i] - pts[i - 1]).normalized;
        pos = new Vector3(p.x, PathTop(s), p.y);
        fwd = new Vector3(d.x, 0f, d.y);
    }

    // 道のつながっている区間（溝で切れる）
    static List<Vector2> PathPieces(float len)
    {
        var list = new List<Vector2>();
        if (GrooveD0 > 0f && GrooveD1 > GrooveD0 && GrooveD1 < len) { list.Add(new Vector2(0f, GrooveD0)); list.Add(new Vector2(GrooveD1, len)); }
        else list.Add(new Vector2(0f, len));
        return list;
    }

    // ---------- 見た目の道：ほかのコース（FuwaCourse1Builder / FuwaCourse3Builder）と同じ、上の角を丸めた断面 ----------
    // 半径 Bevel・BevelSteps 段の丸め。溝に面した端も丸める（コース3と同じ）。道の両はしは島の中に埋まるのでふただけ。
    // 当たり判定は PathMesh（角あり）
    const float Bevel = 0.08f;
    const int BevelSteps = 4;

    static List<Vector4> BevelProfile(float W)
    {
        // (横, 高さ, 法線の横, 法線の上)
        var prof = new List<Vector4> { new Vector4(-W, -Thick, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-W + Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(W - Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(W, -Thick, 1, 0));
        return prof;
    }

    static Mesh BevelPathMesh(List<Vector2> pts, List<float> dist, Matrix4x4 toLocal)
    {
        float len = dist[dist.Count - 1];
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
        var A = new List<int>(); var B = new List<int>();
        foreach (var pc in PathPieces(len))
        {
            float s0p = pc.x, s1p = pc.y;
            bool b0 = s0p > 0.01f, b1 = s1p < len - 0.01f;   // 溝に面した端だけ丸める
            // 断面を置く位置：(位置, 内側へのへこみ, 端の丸めの角度, 向き)
            var st = new List<Vector4>();
            if (b0) for (int k = BevelSteps; k >= 1; k--) { float th = (Mathf.PI / 2) * k / BevelSteps; st.Add(new Vector4(s0p + Bevel * (1 - Mathf.Sin(th)), Bevel * (1 - Mathf.Cos(th)), th, -1)); }
            float a0 = b0 ? s0p + Bevel : s0p, a1 = b1 ? s1p - Bevel : s1p;
            // 0.2m刻み＋しまの境目（境目で色がちょうど切り替わるように）
            var mids = new List<float>();
            int segs = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 0.2f));
            for (int k = 0; k <= segs; k++) mids.Add(Mathf.Lerp(a0, a1, k / (float)segs));
            for (float b = Mathf.Ceil(a0 / Stripe) * Stripe; b < a1 - 1e-3f; b += Stripe) if (b > a0 + 1e-3f) mids.Add(b);
            mids.Sort();
            for (int i = mids.Count - 1; i > 0; i--) if (mids[i] - mids[i - 1] < 1e-3f) mids.RemoveAt(i);
            foreach (var m in mids) st.Add(new Vector4(m, 0, 0, 0));
            if (b1) for (int k = 1; k <= BevelSteps; k++) { float th = (Mathf.PI / 2) * k / BevelSteps; st.Add(new Vector4(s1p - Bevel * (1 - Mathf.Sin(th)), Bevel * (1 - Mathf.Cos(th)), th, 1)); }

            var rings = new List<int>(); int P = 0;
            foreach (var sv in st)
            {
                float s = sv.x, inset = sv.y, th = sv.z, dir = sv.w;
                var prof = BevelProfile(PathHalfW(s)); P = prof.Count;
                At(pts, dist, s, out var c, out var f);
                var right = new Vector3(f.z, 0f, -f.x);
                float slope = (PathTop(s + 0.05f) - PathTop(s - 0.05f)) / 0.1f;
                var t3 = (f + Vector3.up * slope).normalized;
                var up = Vector3.Cross(t3, right).normalized; if (up.y < 0f) up = -up;
                rings.Add(V.Count);
                foreach (var q in prof)
                {
                    // 端の丸め：断面の法線の向きに inset だけ内側へ寄せる
                    float qx = q.x - q.z * inset, qy = q.y - q.w * inset;
                    V.Add(toLocal.MultiplyPoint3x4(c + right * qx + Vector3.up * qy));
                    var nn = (right * q.z + up * q.w).normalized * Mathf.Cos(th) + t3 * dir * Mathf.Sin(th);
                    N.Add(toLocal.MultiplyVector(nn).normalized);
                    UV.Add(new Vector2(q.x, s));
                }
            }
            for (int r = 0; r < st.Count - 1; r++)
            {
                int stripe = Mathf.FloorToInt(((st[r].x + st[r + 1].x) * 0.5f) / Stripe) % 2;
                var T = stripe == 0 ? A : B;
                int ra = rings[r], rb = rings[r + 1];
                for (int i = 0; i < P - 1; i++) { int x0 = ra + i, x1 = ra + i + 1, y0 = rb + i, y1 = rb + i + 1; T.AddRange(new[] { x0, y0, x1, x1, y0, y1 }); }
                // 底
                int bb = V.Count; var down = toLocal.MultiplyVector(Vector3.down).normalized;
                foreach (int idx in new[] { ra, rb }) { V.Add(V[idx]); V.Add(V[idx + P - 1]); N.Add(down); N.Add(down); UV.Add(Vector2.zero); UV.Add(Vector2.zero); }
                A.AddRange(new[] { bb, bb + 1, bb + 2, bb + 1, bb + 3, bb + 2 });
            }
            // 両はしのふた（扇形）
            foreach (var (ri, sign) in new[] { (0, -1f), (st.Count - 1, 1f) })
            {
                At(pts, dist, st[ri].x, out _, out var f);
                var nrm = toLocal.MultiplyVector(f * sign).normalized;
                int cb = V.Count;
                for (int i = 0; i < P; i++) { V.Add(V[rings[ri] + i]); N.Add(nrm); UV.Add(Vector2.zero); }
                for (int i = 1; i < P - 1; i++) A.AddRange(new[] { cb, cb + i, cb + i + 1 });
            }
        }
        // 三角形の向きを、決めた法線にそろえる
        foreach (var T in new[] { A, B })
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 p0 = V[T[i]], p1 = V[T[i + 1]], p2 = V[T[i + 2]];
                Vector3 want = N[T[i]] + N[T[i + 1]] + N[T[i + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), want) < 0f) { int tmp = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = tmp; }
            }
        var m2 = new Mesh { name = "Course0_TutorialPath_Bevel" };
        m2.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m2.SetVertices(V); m2.SetNormals(N); m2.SetUVs(0, UV);
        m2.subMeshCount = 2; m2.SetTriangles(A, 0); m2.SetTriangles(B, 1);
        m2.RecalculateBounds(); m2.RecalculateTangents();
        return m2;
    }

    // ---------- 当たり判定の道（角ありの箱。幅・高さ・溝は見た目と同じ） ----------
    static Mesh PathMesh(List<Vector2> pts, List<float> dist, Matrix4x4 toLocal)
    {
        float len = dist[dist.Count - 1];
        var V = new List<Vector3>(); var T = new List<int>();
        foreach (var pc in PathPieces(len))
        {
            float s0p = pc.x, s1p = pc.y;
            int n = Mathf.Max(1, Mathf.CeilToInt((s1p - s0p) / 0.25f));
            int first = V.Count;
            for (int k = 0; k <= n; k++)
            {
                float s = Mathf.Lerp(s0p, s1p, k / (float)n), W = PathHalfW(s);
                At(pts, dist, s, out var c, out var f);
                var r = new Vector3(f.z, 0f, -f.x);
                // 断面の4点：左上・右上・右下・左下
                V.Add(toLocal.MultiplyPoint3x4(c - r * W)); V.Add(toLocal.MultiplyPoint3x4(c + r * W));
                V.Add(toLocal.MultiplyPoint3x4(c + r * W + Vector3.down * Thick)); V.Add(toLocal.MultiplyPoint3x4(c - r * W + Vector3.down * Thick));
            }
            for (int k = 0; k < n; k++)
            {
                int a = first + k * 4, b = a + 4;
                for (int q = 0; q < 4; q++) { int q1 = (q + 1) % 4; T.AddRange(new[] { a + q, b + q, a + q1, a + q1, b + q, b + q1 }); }
            }
            int e = first + n * 4;
            T.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            T.AddRange(new[] { e, e + 2, e + 1, e, e + 3, e + 2 });
        }
        var m = new Mesh { name = "Course0_TutorialPath" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(V); m.SetTriangles(T, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
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
    // 最後の本番の道（キノコの島〜ゴール。2026-10-08 てじゃ）：キノコの島の中心からの距離 u で並べる
    // 直線 → 右カーブ → 登り坂 → 溝（飛び越える。下は埋まっていて、落ちてもジャンプで上がれる）→ ゆるい斜めの横風（左から右）
    // → チェックポイント → 道の右端にまもの（背後にイバラ）→ ゴール。道は広め（島の柵の入口に合わせて、両はしは元の幅にもどす）
    const float FinHalfW = 2.5f;
    const float FinStraight = 8f;                       // 島の中心からカーブの始まりまで
    const float FinCurveDeg = 75f, FinCurveR = 7f;      // 右カーブ
    const float FinSlopeLen = 6f, FinRise = 1.0f;       // 登り坂
    const float FinGrooveW = 1.0f, FinGrooveDepth = 0.6f;   // 溝。深さ0.6：歩いては上がれない（段差の乗り越えは0.5まで）がジャンプなら上がれる（柵の0.65と同じ）
    const float FinWindLen = 7f, FinWindStrength = 0.5f, FinWindAngle = 30f;   // 横風（右へ、少し前向き）

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
        float sMush = lastWall + ToMushroom;
        // 最後の本番の道の目印（キノコの島の中心からの距離 u）
        float uCurve1 = FinStraight + FinCurveR * FinCurveDeg * Mathf.Deg2Rad;
        float uSlope0 = uCurve1 + 1f, uSlope1 = uSlope0 + FinSlopeLen;
        float uGroove0 = uSlope1 + 3f, uGroove1 = uGroove0 + FinGrooveW;
        float uWind0 = uGroove1 + 3f, uWind1 = uWind0 + FinWindLen;
        float uCp = uWind1 + 2.5f, uMon = uCp + 5.5f, uGoal = uMon + 9f;
        float uEnd = uGoal - 2.6f;   // 道はゴールの島のふちまで（島のまん中のリングにかぶらないように）
        float sCp = sMush + uCp, sGoal = sMush + uGoal, sEnd = sMush + uEnd;
        // 中心線：説明の小道（うねる）→ キノコの島から先は 直線＋右カーブ＋直線
        Vector3 pM = Lane(F, sMush), d0 = (Lane(F, sMush + 0.01f) - Lane(F, sMush - 0.01f)); d0.y = 0f; d0.Normalize();
        Vector3 r0 = new Vector3(d0.z, 0f, -d0.x);
        void Fin(float u, out Vector3 p, out Vector3 d)
        {
            if (u <= FinStraight) { p = pM + d0 * u; d = d0; return; }
            float a = Mathf.Min(u - FinStraight, uCurve1 - FinStraight) / FinCurveR;
            Vector3 cen = pM + d0 * FinStraight + r0 * FinCurveR;
            p = cen - r0 * FinCurveR * Mathf.Cos(a) + d0 * FinCurveR * Mathf.Sin(a);
            d = (d0 * Mathf.Cos(a) + r0 * Mathf.Sin(a)).normalized;
            if (u > uCurve1) p += d * (u - uCurve1);
        }
        var cl = new List<Vector2>();
        for (float s = 0f; s < sMush - 2f; s += 4f) { var p = Lane(F, s); cl.Add(new Vector2(p.x, p.z)); }
        int iMush = cl.Count; cl.Add(new Vector2(pM.x, pM.z));
        for (float u = 1f; u < uEnd - 0.3f; u += 1f) { Fin(u, out var p, out _); cl.Add(new Vector2(p.x, p.z)); }
        { Fin(uEnd, out var p, out _); cl.Add(new Vector2(p.x, p.z)); }
        Ctrl = cl.ToArray();
        var pts = Dense(out var dist);
        float len = dist[dist.Count - 1];
        float dMush = dist[iMush * 200];
        // s（入口からまっすぐ測った距離）→ 道の上の距離（説明の小道はうねりで少し長いぶんを比で合わせる。本番の道は u そのまま）
        float S(float s) { return s <= sMush ? s * dMush / sMush : dMush + (s - sMush) * (len - dMush) / uEnd; }
        float D(float u) { return S(sMush + u); }
        float U(float d) { return d <= dMush ? (d - dMush) : (d - dMush) * uEnd / (len - dMush); }
        // 道の形：広さ（島の柵の入口では元の幅）・登り坂・溝
        PathHalfW = d =>
        {
            float u = U(d);
            float k = Mathf.Min(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 6f, u)), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(uGoal - 3f, uGoal - 6f, u)));
            return Mathf.Lerp(HalfW, FinHalfW, k);
        };
        PathTop = d => Top + FinRise * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(uSlope0, uSlope1, U(d)));
        GrooveD0 = D(uGroove0); GrooveD1 = D(uGroove1);

        // ---------- 道（入口の島〜ゴールの島まで一本） ----------
        var pathGo = new GameObject("TutorialPath");
        pathGo.transform.SetParent(root, false);
        Mesh SaveMesh(Mesh m, string path)
        {
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (ex == null) { AssetDatabase.CreateAsset(m, path); return m; }
            FuwaBevelTools.CopyInto(ex, m); ex.UploadMeshData(false); EditorUtility.SetDirty(ex); return ex;
        }
        // 見た目は角を丸めた道（ほかのコースとそろえる）、当たり判定は角ありの箱
        var mesh = SaveMesh(BevelPathMesh(pts, dist, pathGo.transform.worldToLocalMatrix), MeshDir + "/Course0_TutorialPath_Bevel.asset");
        var colMesh = SaveMesh(PathMesh(pts, dist, pathGo.transform.worldToLocalMatrix), MeshDir + "/Course0_TutorialPath.asset");
        pathGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        pathGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mat("Course0_PathA"), Mat("Course0_PathB") };
        pathGo.AddComponent<MeshCollider>().sharedMesh = colMesh;
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
        cp.position = cc; cp.rotation = Quaternion.LookRotation(cf);   // cc の高さ＝道の上面（坂の上）
        // チェックポイントの光るキノコ（道の両わきの丸太）を、広い道のふちの外へ
        foreach (Transform cl0 in cp.Find("GlowMushrooms"))
        {
            var lp = cl0.localPosition; lp.x = Mathf.Sign(lp.x) * (PathHalfW(S(sCp)) + 0.28f); cl0.localPosition = lp;
        }
        Fin(uGoal, out var gc, out var gf);
        // ゴールの島の地面の上面（＝ゴールの位置の高さ）は、道の上面より2cm低い（元と同じ関係）
        goal.position = new Vector3(gc.x, PathTop(len) - 0.02f, gc.z); goal.rotation = Quaternion.LookRotation(gf);   // ゴールの柵の入口はローカルの -z 側

        // ---------- 入口の島 ----------
        At(pts, dist, 0f, out var e0, out var ef);
        var entrance = Island("Entrance", e0, true);
        var er = new Vector3(ef.z, 0, -ef.x);
        // ほかのコースのスタートと同じ形にする（2026-10-08 てじゃ「せまい」「キノコだけなくす、小さいのも含め」）：
        // かぜのみちのスタートの柵・後ろの橋・ワープの小島を、地面の位置を合わせてコピー（キノコは持ってこない）
        var c1Start = scene.GetRootGameObjects().First(g => g.name == "FuwaCourse").transform.Find("Start");
        var c1Area = c1Start.Find("StartArea");
        Quaternion eRot = Quaternion.LookRotation(ef);
        Quaternion rel1 = eRot * Quaternion.Inverse(c1Start.rotation);
        Vector3 g1 = c1Area.Find("Ground").position, gE = entrance.Find("Ground").position;
        Vector3 MapC1(Vector3 p) { return gE + rel1 * (p - g1); }
        foreach (var n in new[] { "Fence", "FenceCollider", "Bridge", "BridgeWood", "WarpGround", "IslandUnder_Warp" })
        {
            var src = c1Area.Find(n); if (src == null) continue;
            var c = Dup(src, entrance, n); c.SetActive(true);
            c.transform.SetPositionAndRotation(MapC1(src.position), rel1 * src.rotation);
        }
        Vector3 lobbyPad = MapC1(c1Start.Find("WarpToLobby").position);
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
        // ロビーへのワープは、ほかのコースと同じく後ろの小島に
        MakeWarp("WarpToLobby", lobbyPad, "ロビーにもどる", "Back to Lobby", null, null, null, true);
        // 説明をとばすのは、床のワープではなく柵に付けた小さいボタン（2026-10-08 てじゃ）。入って左前（銃の台は右前）
        // ワープの処理は FuwaCourseWarp をそのまま使い、床の見た目と踏んで入る当たり判定は消して、ボタン（FuwaInteractWarp）から呼ぶ
        var skip = MakeWarp("SkipTutorial", e0 + ef * 1.3f - er * 1.35f + Vector3.up * 0.02f, "説明をとばす", "Skip", startSpot, ballStart, gsSrc.Find("GunSpot"), false);
        var skipTrig = skip.Find("SkipTutorial_Trigger");
        foreach (Transform ch in skip) if (ch != skipTrig) ch.gameObject.SetActive(false);
        foreach (var col in skipTrig.GetComponents<Collider>()) col.enabled = false;
        log.Add(FenceButton(entrance, entrance.Find("Ground"), entrance.Find("FenceCollider"), e0, ef, 55f, "SkipButton", "説明をとばす ▶", "Skip tutorial ▶", skipTrig.GetComponent<FuwaCourseWarp>(), lang));
        if (warpLabel != null) warpLabel.gameObject.SetActive(false);

        // ロビーの「チュートリアル」ワープの行き先を入口の島に
        var lobbyWarp = Object.FindObjectsOfType<FuwaCourseWarp>(true).First(w => w.name == "WarpToCourse0_Trigger");
        SetU(lobbyWarp, "playerSpot", spot); SetU(lobbyWarp, "gunSpot", gs.transform.Find("GunSpot")); Sync(lobbyWarp);
        // とばした先（キノコの島）には「説明にもどる」ボタン。行き先はロビーの「チュートリアル」ワープと同じ（入口の島）
        // ボタンは Tutorial の下に置く（作り直しの時に一緒に消えるように）
        log.Add(FenceButton(root, startArea.Find("Ground"), startArea.Find("FenceCollider"), startArea.Find("Ground").position, start.forward, 55f,
            "BackToTutorialButton", "◀ 説明にもどる", "◀ Back to tutorial", lobbyWarp, lang));

        // ---------- お手本区画 ----------
        var signSrc = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "BestTimes" && t.parent != null && t.parent.name == "FuwaLobby");
        for (int bi = 0; bi < nb; bi++)
        {
            var bay = Bays[bi];
            At(pts, dist, S(bayS[bi]), out var c, out var f);
            // お手本の島：全部右手にそろえる（左右交互だと、せっかちな人が反対側を見落として素通りしやすい。2026-10-08）
            Vector3 outw = new Vector3(f.z, 0, -f.x);
            var bayRoot = new GameObject("Bay_" + bay.key).transform; bayRoot.SetParent(root, false);
            Vector3 ic = c + outw * (HalfW + 1.6f - 0.25f);
            var isl = Island("Island", ic, false); isl.SetParent(bayRoot, true);
            BuildDemo(bayRoot, bay.key, ic, f, outw, f, blowerTmpl, ball, log);
            if (bay.key == "Wind") MakeWindZone(bayRoot, pts, dist, S(bayS[bi]), -outw, course, log, BaySpan - 0.4f, HalfW, WindStrength, "Tutorial_WindHighlight");
            // 看板：区画の手前（入口側）、お手本の島の側の道のふちに立てて、来る人の方を向く
            MakeSign(bayRoot, c - f * 3.4f + outw * (HalfW - 0.35f), (f + outw * 0.45f).normalized, bay.ja, bay.en, signSrc, lang);
            // ためし撃ちの場所（区画のまん中の少し手前）と、そこへふわふわを呼ぶ範囲（区画まるごと）
            var trySpot = new GameObject("TrySpot").transform; trySpot.SetParent(bayRoot, false);
            At(pts, dist, S(bayS[bi] - 0.5f), out var tc, out var tf);
            // 下向き：足元に置いて下を撃たせる／上を向きすぎない：あえて頭より上に置いて、見上げて撃つと進まないのを自分で試せるように
            trySpot.position = tc + Vector3.up * (bay.key == "Down" ? 0.4f : bay.key == "Steep" ? 2.1f : 1.15f); trySpot.rotation = Quaternion.LookRotation(tf);
            var zone = new GameObject("TryZone"); zone.transform.SetParent(bayRoot, false);
            zone.transform.position = c + Vector3.up * 1.2f; zone.transform.rotation = Quaternion.LookRotation(f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(HalfW * 2f + 4f, 3f, BaySpan - 0.2f);
            zone.layer = 0;
            var tz = UdonSharpUndo.AddComponent<FuwaTryZone>(zone);
            SetU(tz, "ball", ball); SetU(tz, "spot", trySpot); Sync(tz);
            // まものの区画：ためし撃ちの道の上に本物のまものを1匹（試し撃ちのふわふわで押し返す練習。2026-10-08 てじゃ）
            if (bay.key == "Monster")
            {
                At(pts, dist, S(bayS[bi] + 2.6f), out var pc, out var pf);
                var enemySrc = Object.FindObjectsOfType<FuwaEnemy>(true).First(e => e.name == "Enemy4");
                Physics.SyncTransforms();
                float up = Physics.Raycast(enemySrc.transform.position + Vector3.up * 0.6f, Vector3.down, out var gh, 5f, 1 << 11, QueryTriggerInteraction.Ignore) ? enemySrc.transform.position.y - gh.point.y : 0.2f;
                var pm = Object.Instantiate(enemySrc.gameObject, bayRoot, true); pm.name = "PracticeMonster"; pm.SetActive(true);
                pm.transform.SetPositionAndRotation(pc + Vector3.up * up, Quaternion.LookRotation(-pf));
                var fe = pm.GetComponent<FuwaEnemy>();
                SetUF(fe, "wanderRadius", 0.4f);
                SetUF(fe, "leashRange", 4f);   // 説明の小道は細いので、遠くまで追いかけない
                Sync(fe);
                var bso = new SerializedObject(ball); var ep = bso.FindProperty("enemies");
                var keep = new List<Object>();
                for (int i = 0; i < ep.arraySize; i++) { var o = ep.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) keep.Add(o); }
                keep.Add(fe);
                ep.arraySize = keep.Count; for (int i = 0; i < keep.Count; i++) ep.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                bso.ApplyModifiedPropertiesWithoutUndo(); Sync(ball);
                log.Add("practice monster on the path");
            }
        }
        // 道の左側（お手本の島がない側）に、元のれんしゅうのしゃぼん玉（大きいのがゆっくり上っていく）を並べる
        {
            var bubSrc = course.Find("Bubbles/Spot0");
            var bubs = new GameObject("LeftBubbles").transform; bubs.SetParent(root, false);
            var rnd = new System.Random(17);
            int k = 0;
            for (float s = 3f; s < lastWall + 2f; s += 6.5f, k++)
            {
                At(pts, dist, S(s), out var bc0, out var bf0);
                Vector3 left = -new Vector3(bf0.z, 0, -bf0.x);
                var b = Object.Instantiate(bubSrc.gameObject, bubs, false); b.name = "Spot" + k; b.SetActive(true);
                b.transform.position = bc0 + left * (HalfW + 3.4f + (float)rnd.NextDouble() * 2.2f) + Vector3.up * (-2.2f + (float)rnd.NextDouble() * 1.6f);
                var ps = b.GetComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var em = ps.emission; em.rateOverTime = 0.1f;   // 元より少し多め（1か所に1〜2個）
                // 元の大きさ（直径1〜4m）だと大きすぎて道のじゃまになった（2026-10-08 てじゃ）。小さめに
                var mn = ps.main; mn.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(1000 + k * 37);
            }
            log.Add("bubbles " + k);
        }
        // 区画のさかいめの、ふわふわだけ止める壁はなくした（2026-10-08 てじゃ）。
        // 次の区画に入ると TryZone がふわふわをその区画のためし撃ちの場所へ移すので、壁がなくても区画ごとに分かれる（MakeBarrier は残してある）
        // 説明の小道の出口：「この先にはほかにも仕掛けがある」看板（全部は説明しない）
        {
            At(pts, dist, S(lastWall + 2.2f), out var c, out var f);
            Vector3 outw = new Vector3(f.z, 0, -f.x);
            MakeSign(root, c + outw * (HalfW - 0.35f), (f + outw * 0.45f).normalized,
                "<size=130%><color=#FF6B5C>ここからが本番！</color></size>\nゴールまで運んでみよう\nほかのコースには、もっといろんな\nしかけがあるよ",
                "<size=130%><color=#FF6B5C>Here we go!</color></size>\nCarry it to the goal\nThe other courses have\nlots more tricks", signSrc, lang, "Sign_MoreTricks");
        }
        // 最後の壁〜キノコの島：ふわふわをコースのスタート（キノコ）へ戻す
        {
            At(pts, dist, S(lastWall + 1.6f), out var c, out var f);
            var zone = new GameObject("ToCourseZone"); zone.transform.SetParent(root, false);
            zone.transform.position = c + Vector3.up * 1.2f; zone.transform.rotation = Quaternion.LookRotation(f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(HalfW * 2f + 1f, 3f, 2.6f);
            var tz = UdonSharpUndo.AddComponent<FuwaTryZone>(zone);
            SetU(tz, "ball", ball); SetU(tz, "courseStart", ballStart); Sync(tz);
            // キノコの島から先（本番の道）は、タイムなしでもアウトでプレイヤーも戻す（2026-10-08 てじゃ）。ためし撃ちの区画は今まで通りふわふわだけ戻る
            var bso = new SerializedObject(ball); var rp = bso.FindProperty("returnPlayerStarts");
            rp.arraySize = 1; rp.GetArrayElementAtIndex(0).objectReferenceValue = ballStart;
            bso.ApplyModifiedPropertiesWithoutUndo(); Sync(ball);
        }

        // ---------- 最後の本番の道のしかけ ----------
        {
            var fin = new GameObject("FinalRun").transform; fin.SetParent(root, false);
            var pathA = Mat("Course0_PathB");   // 溝の中は濃い方の色（上から見て穴だとわかるように）
            Transform Box(string n, Vector3 center, Quaternion rot, Vector3 size)
            {
                var g = new GameObject(n); g.transform.SetParent(fin, false);
                g.transform.SetPositionAndRotation(center, rot); g.transform.localScale = size;
                g.AddComponent<MeshFilter>().sharedMesh = FuwaBevelTools.RoundCube(size, 0.03f, 2);
                g.AddComponent<MeshRenderer>().sharedMaterial = pathA;
                g.AddComponent<BoxCollider>();
                g.layer = 11;
                return g.transform;
            }
            // 溝：道が切れている所の底（道の上面から FinGrooveDepth 下）と、溝の前後の壁（道の下に続けて、溝の中から見て段差がつながるように）
            {
                float dg = (GrooveD0 + GrooveD1) * 0.5f;
                At(pts, dist, dg, out var gcen, out var gfw);
                var rot = Quaternion.LookRotation(gfw);
                float W = PathHalfW(dg) * 2f, gw = GrooveD1 - GrooveD0, wallT = 0.15f;
                Box("GrooveFloor", gcen + Vector3.down * (FinGrooveDepth + 0.15f), rot, new Vector3(W, 0.3f, gw));
                float wallH = FinGrooveDepth + 0.3f - Thick;   // 道の下面〜底の下面
                Box("GrooveWallA", gcen - gfw * (gw * 0.5f + wallT * 0.5f) + Vector3.down * (Thick + wallH * 0.5f), rot, new Vector3(W, wallH, wallT));
                Box("GrooveWallB", gcen + gfw * (gw * 0.5f + wallT * 0.5f) + Vector3.down * (Thick + wallH * 0.5f), rot, new Vector3(W, wallH, wallT));
                log.Add($"groove {gw:0.0}m depth {FinGrooveDepth}");
            }
            // ゆるい斜めの横風：左から右へ、少し前向き
            {
                float dm = D((uWind0 + uWind1) * 0.5f);
                At(pts, dist, dm, out _, out var wf);
                var wr = new Vector3(wf.z, 0f, -wf.x);
                var wdir = wr * Mathf.Cos(FinWindAngle * Mathf.Deg2Rad) + wf * Mathf.Sin(FinWindAngle * Mathf.Deg2Rad);
                MakeWindZone(fin, pts, dist, dm, wdir, course, log, FinWindLen, FinHalfW, FinWindStrength, "Tutorial_FinalWindHighlight");
                // 吹き流し（かぜのみちの Windsock0 のコピー）：風の入口の左（風上）のふち。風が吹いている向きになびく
                var wsSrc = Object.FindObjectsOfType<FuwaWindDeco>(true).FirstOrDefault(w => w.name == "Windsock0");
                if (wsSrc != null)
                {
                    float uWs = uWind0 + 1.2f;
                    At(pts, dist, D(uWs), out var wc, out var wf2);
                    var wl = -new Vector3(wf2.z, 0f, -wf2.x);
                    var ws = Object.Instantiate(wsSrc.gameObject, fin, false); ws.name = "Windsock"; ws.SetActive(true);
                    ws.transform.position = wc + wl * (FinHalfW - 0.3f);
                    ws.transform.rotation = Quaternion.LookRotation(wf2);
                    var wd = ws.GetComponent<FuwaWindDeco>();
                    SetU(wd, "wind", fin.Find("WindZone").GetComponent<FuwaWindArea>());
                    var wso = new SerializedObject(wd);
                    wso.FindProperty("fixedDirection").vector3Value = wdir.normalized;
                    wso.FindProperty("droopAway").vector3Value = wl;   // 風がない時は道の外へ垂れる
                    wso.ApplyModifiedPropertiesWithoutUndo();
                    Sync(wd);
                }

            }
            // 道の右端にまもの、その背後（さらに右＝道のふち）にイバラ。撃ってイバラへ押し込むと倒せる
            {
                At(pts, dist, D(uMon), out var mcen, out var mfw);
                var mr = new Vector3(mfw.z, 0f, -mfw.x);
                float GroundBelow(Vector3 p) { return Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out var h, 5f, 1 << 11, QueryTriggerInteraction.Ignore) ? h.point.y : p.y; }
                var enemySrc = Object.FindObjectsOfType<FuwaEnemy>(true).First(e => e.name == "Enemy4");
                var spSrc = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "SpikePatch4" && t.parent != null && t.parent.name == "Spikes");
                Physics.SyncTransforms();
                float enemyUp = enemySrc.transform.position.y - GroundBelow(enemySrc.transform.position);
                float spUp = spSrc.position.y - GroundBelow(spSrc.position);
                var mon = Object.Instantiate(enemySrc.gameObject, fin, true); mon.name = "Monster"; mon.SetActive(true);
                mon.transform.position = mcen + mr * 0.7f + Vector3.up * enemyUp;   // まものの当たり（半径0.45）＋うろうろでもイバラの当たり（ふちから0.9〜）に届かない所
                mon.transform.rotation = Quaternion.LookRotation(-mr);
                var fe = mon.GetComponent<FuwaEnemy>();
                SetUF(fe, "wanderRadius", 0.2f);
                Sync(fe);
                for (int k = 0; k < 2; k++)
                {
                    var sp = Object.Instantiate(spSrc.gameObject, fin, true); sp.name = "Bramble" + k; sp.SetActive(true);
                    At(pts, dist, D(uMon + (k == 0 ? -1.05f : 1.05f)), out var bc, out var bf);
                    var br = new Vector3(bf.z, 0f, -bf.x);
                    sp.transform.SetPositionAndRotation(bc + br * (FinHalfW - 0.55f) + Vector3.up * spUp, Quaternion.LookRotation(bf));
                    sp.transform.localScale = new Vector3(0.5f, 1f, 1f);   // 横（道を横切る向き）を細く：道のふちに沿った帯
                }
                // 球の「まもの一覧」に入れる（アウトの時に復活させる）。作り直しで消えた分は詰める
                var bso = new SerializedObject(ball); var ep = bso.FindProperty("enemies");
                var keep = new List<Object>();
                for (int i = 0; i < ep.arraySize; i++) { var o = ep.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) keep.Add(o); }
                keep.Add(fe);
                ep.arraySize = keep.Count; for (int i = 0; i < keep.Count; i++) ep.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                bso.ApplyModifiedPropertiesWithoutUndo(); Sync(ball);
                log.Add($"monster+brambles at u={uMon:0.0}, enemies {keep.Count}");
            }
        }

        // ---------- 本番の道の飾り：前のれんしゅうコースの小物（マカロンの塔・ぺろぺろキャンディ・草花）を道のふちに ----------
        // 説明の小道には置かない（てじゃ「説明ゾーンはシンプルに無し」）。島・溝・チェックポイント・まものの側は避ける
        {
            var deco = new GameObject("FinalDeco").transform; deco.SetParent(root, false);
            var props = PracticeProps();   // 小物ごとのメッシュ（根元が原点）
            var candy = course.Find("EdgeProps/Visual").GetComponent<MeshRenderer>().sharedMaterials;
            var clusters = course.Find("Deco").Cast<Transform>().ToList();
            var rnd = new System.Random(23);
            bool Free(float u, int side)
            {
                if (u < 5f || u > uGoal - 4.5f) return false;                                   // 島の近く
                if (u > uGroove0 - 1.5f && u < uGroove1 + 1.5f) return false;                    // 溝
                if (Mathf.Abs(u - uCp) < 1.8f) return false;                                     // チェックポイントの丸太
                if (side > 0 && u > uMon - 3.5f && u < uMon + 3.5f) return false;                // まもの・イバラ（右）
                if (side < 0 && Mathf.Abs(u - (uWind0 + 1.2f)) < 2f) return false;               // 吹き流し（左）
                if (side > 0 && u > uWind0 - 1f && u < uWind1 + 2f) return false;               // 横風の風下（右）：流されたふわふわが小物に当たってアウトにならないように
                return true;
            }
            Physics.SyncTransforms();
            float GroundY(Vector3 p) { return Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var h, 4f, 1 << 11, QueryTriggerInteraction.Ignore) ? h.point.y : p.y; }
            int np = 0, nc = 0, pi = 0;
            // 大きい小物：左右交互に約4.5mおき
            int sideA = -1;
            for (float u = 5.5f; u < uGoal - 4.5f; u += 4.5f, sideA = -sideA)
            {
                int side = sideA;
                if (!Free(u, side)) { side = -side; if (!Free(u, side)) continue; }
                At(pts, dist, D(u), out var c, out var f);
                var r = new Vector3(f.z, 0f, -f.x);
                var pm = props[pi++ % props.Count];
                float rad = Mathf.Max(pm.bounds.extents.x, pm.bounds.extents.z);
                var p = c + r * side * (PathHalfW(D(u)) - rad - 0.15f);
                var g = new GameObject("Prop" + np); g.transform.SetParent(deco, false);
                g.transform.position = new Vector3(p.x, GroundY(p) - 0.01f, p.z);
                g.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                g.AddComponent<MeshFilter>().sharedMesh = pm;
                g.AddComponent<MeshRenderer>().sharedMaterials = candy;
                // 当たり判定：元のれんしゅうと同じく道のレイヤー（ふわふわが当たるとアウト、人は通れない）
                var cap = g.AddComponent<CapsuleCollider>();
                cap.radius = rad * 0.8f; cap.height = pm.bounds.size.y; cap.center = new Vector3(0f, pm.bounds.size.y * 0.5f, 0f);
                g.layer = 11;
                np++;
            }
            // 草花：左右に約2.6mおき（小物とずらして）
            for (float u = 6.8f; u < uGoal - 4.5f; u += 2.6f)
                for (int side = -1; side <= 1; side += 2)
                {
                    float uu = u + (side > 0 ? 1.3f : 0f);
                    if (!Free(uu, side) || rnd.NextDouble() < 0.25) continue;
                    At(pts, dist, D(uu), out var c, out var f);
                    var r = new Vector3(f.z, 0f, -f.x);
                    var src = clusters[rnd.Next(clusters.Count)];
                    var gcl = Object.Instantiate(src.gameObject, deco, false); gcl.name = "Grass" + nc; gcl.SetActive(true);
                    var p = c + r * side * (PathHalfW(D(uu)) - 0.45f);
                    gcl.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f));
                    // 草花は1本ずつ地面に置き直す（坂でも浮かないように）。道から外れる物は消す
                    foreach (Transform ch in gcl.transform.Cast<Transform>().ToList())
                    {
                        var w = ch.position;
                        if (ch.name.StartsWith("Deco_Vines"))
                        {
                            // ツタは上の端（原点）を道のふちちょうどに置いて、道の横に垂らす（元のれんしゅうと同じ）
                            float ds = D(uu) + Vector3.Dot(w - c, f);
                            At(pts, dist, ds, out var ec, out var ef2);
                            var er2 = new Vector3(ef2.z, 0f, -ef2.x) * side;
                            ch.SetPositionAndRotation(ec + er2 * PathHalfW(ds) + Vector3.up * 0.01f, Quaternion.LookRotation(er2));
                            continue;
                        }

                        if (!Physics.Raycast(w + Vector3.up * 1.5f, Vector3.down, out var h, 4f, 1 << 11, QueryTriggerInteraction.Ignore) || h.point.y < c.y - 0.3f || h.collider.name.StartsWith("Prop")) { Object.DestroyImmediate(ch.gameObject); continue; }   // 道から外れる・小物にかぶる物は消す
                        ch.position = new Vector3(w.x, h.point.y - 0.005f, w.z);
                    }
                    nc++;
                }
            log.Add($"final deco props {np}, grass {nc}");
        }

        // ---------- 落ちた時の受け止め：れんしゅうの FallCatcher を一本道の下に合わせる ----------
        var fcT = course.Find("FallCatcher");
        {
            // 道全体（説明の小道＋カーブした本番の道）を囲む四角
            float x0 = pts.Min(p => p.x), x1 = pts.Max(p => p.x), z0 = pts.Min(p => p.y), z1 = pts.Max(p => p.y);
            fcT.rotation = Quaternion.identity;
            fcT.position = new Vector3((x0 + x1) * 0.5f, fcT.position.y, (z0 + z1) * 0.5f);
            fcT.localScale = new Vector3(x1 - x0 + 30f, fcT.localScale.y, z1 - z0 + 30f);

        }

        // ---------- まとめ ----------
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            if (go.GetComponent<FuwaDemoStand>() != null || go.GetComponentInParent<FuwaDemoStand>() != null) { GameObjectUtility.SetStaticEditorFlags(go, 0); continue; }
            if (go.GetComponentInParent<FuwaEnemy>() != null) { GameObjectUtility.SetStaticEditorFlags(go, 0); continue; }   // まものは動く

            if (go.GetComponent<Renderer>() != null && go.GetComponent<ParticleSystem>() == null && go.GetComponent<TMPro.TextMeshPro>() == null && go.name != "Label")
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }
        Sync(lang);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "Tutorial built: " + string.Join(" / ", log) + $" / mushroom s={sMush:0} goal s={sGoal:0}";
    }

    // 島の上面の円の中に、dir と平行なしま模様（1本おき）。lineThrough の点を通る線がしまの真ん中に来るようにそろえる
    // （縦じま＝撃つ向きと平行で銃口を通す／横じま＝撃つ向きと直角でふわふわの出る所を通す）
    static void MakeStripes(Transform parent, Vector3 center, Vector3 dir, Vector3 lineThrough, string name = "DirStripes")
    {
        const float R = 1.5f, Band = 0.22f;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float shift = Vector3.Dot(lineThrough - center, side);   // 銃口の線の、島の中心からの横のずれ
        var V = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
        // 頂点はワールドの向きで作るので、物体の回転はワールドで0にする（親のコースの回転がかかると向きがずれる）
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = center; go.transform.rotation = Quaternion.identity;
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
        var m = new Mesh { name = "Tutorial_" + parent.name + "_" + name }; m.SetVertices(V); m.SetColors(C); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        string path = MeshDir + "/Tutorial_" + (name == "DirStripes" ? "" : parent.name + "_") + name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) AssetDatabase.CreateAsset(m, path); else { ex.Clear(); EditorUtility.CopySerialized(m, ex); m = ex; }
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // ---------- 空気ぎれの区画の銃（大きく・高く。ふわふわは銃口の真上より少し手前） ----------
    // ふわふわは見せない（銃の向きを決めるためだけの見えない的）：銃口の1m先・0.45m上＝約25°上向き
    const float AirGunScale = 1.5f, AirGunHeight = 1.1f, AirBallUp = 0.45f, AirBallBack = 1.0f;

    // ---------- 横風 ----------
    const float WindStrength = 0.8f;   // 加速度 m/s²（コース1の横風のゆるい時くらい）
    const float WindAimDeg = 30f;      // 風上の台が風上へ向ける角度

    // お手本のふわふわが落ちる所（FuwaDemoStand.FixedUpdate と同じ計算。床の高さまで落ちた所）
    static float LastSimTime;   // SimDemoLanding で、撃ってから床に落ちるまでの秒
    static Vector3 SimDemoLanding(FuwaBlower b, FuwaBall bt, Vector3 nz, Vector3 aim, Vector3 p, float charge, Vector3 wind, float floorY)
    {
        LastSimTime = 10f;
        Vector3 v = b.DemoShotVelocity(nz, aim, charge, p);
        Vector3 dd = v.normalized;
        float amt = Mathf.Lerp(bt.shotWindDampMin, bt.shotWindDampMax, charge), hold = bt.shotWindDampHoldMax * charge * charge;
        float tm = Mathf.Lerp(bt.shotWindDampTimeMin, bt.shotWindDampTimeMax, charge);
        float r = bt.transform.lossyScale.y * 0.5f, dt = 0.02f;
        for (float t = 0f; t < 10f; t += dt)
        {
            Vector3 a = wind;
            float tt = t - hold, left = (tm > 0f && tt < tm) ? 1f - Mathf.Max(0f, tt) / tm : 0f;
            if (left > 0f) { float k = 1f - amt * left; float al = Vector3.Dot(a, dd); Vector3 keep = al > 0f ? dd * al : Vector3.zero; a = keep + (a - keep) * k; }
            v += a * dt; v += Vector3.down * bt.fallAcceleration * dt; v *= Mathf.Clamp01(1f - bt.airDrag * dt);
            if (v.y < -bt.maxFallSpeed) v.y = -bt.maxFallSpeed;
            if (v.magnitude > bt.maxSpeed) v = v.normalized * bt.maxSpeed;
            p += v * dt;
            if (p.y < floorY + r) { LastSimTime = t; break; }

        }
        return p;
    }

    // 的（島の上の丸い輪）
    static void MakeTargetRing(Transform parent, string name, Vector3 center)
    {
        const float R0 = 0.2f, R1 = 0.3f; const int N = 40;
        var V = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
        var col = new Color(1f, 0.42f, 0.36f, 0.75f);
        for (int i = 0; i <= N; i++)
        {
            float a = Mathf.PI * 2f * i / N; Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            V.Add(d * R0); V.Add(d * R1); C.Add(col); C.Add(col);
            if (i < N) { int k = i * 2; T.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
        }
        // 真ん中の点
        int c0 = V.Count; V.Add(Vector3.zero); C.Add(col);
        for (int i = 0; i <= N; i++) { float a = Mathf.PI * 2f * i / N; V.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.06f); C.Add(col); }
        for (int i = 0; i < N; i++) T.AddRange(new[] { c0, c0 + 2 + i, c0 + 1 + i });
        for (int i = 0; i < T.Count; i += 3)
            if (Vector3.Cross(V[T[i + 1]] - V[T[i]], V[T[i + 2]] - V[T[i]]).y < 0f) { int tmp = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = tmp; }
        var m = new Mesh { name = "Tutorial_TargetRing" }; m.SetVertices(V); m.SetColors(C); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        string path = MeshDir + "/Tutorial_TargetRing.asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) AssetDatabase.CreateAsset(m, path); else { ex.Clear(); EditorUtility.CopySerialized(m, ex); m = ex; }
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = center; go.transform.rotation = Quaternion.identity;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    // 区画の道の上に本物の横風（ためし撃ちのふわふわが流される）。見た目はコース1の横風（G3_CrossRight）と同じ：地面の＞＞＞と風の粒
    // dMid：道の上の距離（風の箱のまん中）、len：道に沿った長さ、halfW：道の半分の幅、meshName：地面の＞＞＞のメッシュの名前（区画ごとに別）
    // 前のれんしゅうコースの小物（EdgeProps/Visual にまとめてあるメッシュ）を、1つずつのメッシュに分ける（根元のまん中が原点）
    // つながっている部品ごとに分けて、横に重なる部品（棒と飴、積んだマカロン）を1つの小物にまとめる
    static List<Mesh> PracticeProps()
    {
        var res = new List<Mesh>();
        for (int i = 0; ; i++)
        {
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/PracticeProp_" + i + ".asset");
            if (ex == null) break;
            res.Add(ex);
        }
        if (res.Count > 0) { RecolorPracticeProps(res); return res; }
        var vis = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Visual" && t.parent != null && t.parent.name == "EdgeProps" && t.parent.parent != null && t.parent.parent.name == Course);
        var src = vis.GetComponent<MeshFilter>().sharedMesh; var tr = vis.transform;
        var v = src.vertices; var nrm = src.normals; var uv = src.uv; var col = src.colors; var tri = src.triangles;
        int n = v.Length;
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        var weld = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < n; i++) { var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000), Mathf.RoundToInt(v[i].y * 1000), Mathf.RoundToInt(v[i].z * 1000)); if (weld.TryGetValue(k, out int j)) parent[Find(i)] = Find(j); else weld[k] = i; }
        for (int i = 0; i < tri.Length; i += 3) { int a = Find(tri[i]); parent[Find(tri[i + 1])] = a; parent[Find(tri[i + 2])] = Find(a); }
        var wv = v.Select(x => tr.TransformPoint(x)).ToArray();
        var cb = new Dictionary<int, Bounds>();
        for (int i = 0; i < n; i++) { int r = Find(i); if (cb.TryGetValue(r, out var b)) { b.Encapsulate(wv[i]); cb[r] = b; } else cb[r] = new Bounds(wv[i], Vector3.zero); }
        var keys = cb.Keys.ToList(); var grp = keys.ToDictionary(k => k, k => k);
        int G(int x) { while (grp[x] != x) x = grp[x]; return x; }
        for (int i = 0; i < keys.Count; i++)
            for (int j = i + 1; j < keys.Count; j++)
            {
                var a = cb[keys[i]]; var b = cb[keys[j]];
                if (new Vector2(a.center.x - b.center.x, a.center.z - b.center.z).magnitude < (a.extents.x + b.extents.x) * 0.9f + 0.05f) grp[G(keys[j])] = G(keys[i]);
            }
        var byGroup = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++) { int g = G(Find(i)); if (!byGroup.TryGetValue(g, out var l)) byGroup[g] = l = new List<int>(); l.Add(i); }
        int idx = 0;
        foreach (var kv in byGroup)
        {
            var set = new HashSet<int>(kv.Value);
            var b = new Bounds(wv[kv.Value[0]], Vector3.zero); foreach (var i in kv.Value) b.Encapsulate(wv[i]);
            var origin = new Vector3(b.center.x, b.min.y, b.center.z);
            var map = new Dictionary<int, int>(); var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var C = new List<Color>(); var T = new List<int>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                if (!set.Contains(tri[i])) continue;
                for (int q = 0; q < 3; q++)
                {
                    int s = tri[i + q];
                    if (!map.TryGetValue(s, out int d))
                    {
                        d = V.Count; map[s] = d; V.Add(wv[s] - origin);
                        if (nrm.Length == n) N.Add(tr.TransformDirection(nrm[s]));
                        if (uv.Length == n) U.Add(uv[s]);
                        if (col.Length == n) C.Add(col[s]);
                    }
                    T.Add(d);
                }
            }
            var m = new Mesh { name = "PracticeProp_" + idx };
            m.SetVertices(V); if (N.Count == V.Count) m.SetNormals(N); if (U.Count == V.Count) m.SetUVs(0, U); if (C.Count == V.Count) m.SetColors(C);
            m.SetTriangles(T, 0); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, MeshDir + "/PracticeProp_" + idx + ".asset");
            res.Add(m); idx++;
        }
        RecolorPracticeProps(res);
        return res;
    }

    // 3段のマカロンの塔（青・オレンジ・青だった物）を、上から赤・黄・青に（2026-10-08 てじゃ）。
    // 段＝つながった部品ごとに、いちばん多い色をその段の色とみなして、明るさの比はそのままで色だけ置き換える（何度やっても同じ結果）
    static void RecolorPracticeProps(List<Mesh> props)
    {
        var target = props.FirstOrDefault(m => m.name == "PracticeProp_3");
        if (target == null) return;
        var tiers = new[] { new Color(1f, 0.56f, 0.56f), new Color(1f, 0.9f, 0.55f), new Color(0.62f, 0.8f, 1f) };   // 上から赤・黄・青（パステル）
        var v = target.vertices; var col = target.colors; var tri = target.triangles;
        if (col.Length != v.Length) return;
        int n = v.Length;
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        var weld = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < n; i++) { var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000), Mathf.RoundToInt(v[i].y * 1000), Mathf.RoundToInt(v[i].z * 1000)); if (weld.TryGetValue(k, out int j)) parent[Find(i)] = Find(j); else weld[k] = i; }
        for (int i = 0; i < tri.Length; i += 3) { int a = Find(tri[i]); parent[Find(tri[i + 1])] = a; parent[Find(tri[i + 2])] = Find(a); }
        var comps = Enumerable.Range(0, n).GroupBy(Find).Select(g => g.ToList()).OrderByDescending(g => g.Average(i => v[i].y)).ToList();
        if (comps.Count != tiers.Length) return;
        for (int t = 0; t < comps.Count; t++)
        {
            var idxs = comps[t];
            var main = idxs.GroupBy(i => (Color32)col[i]).OrderByDescending(g => g.Count()).First().Key;
            float mb = Mathf.Max(0.001f, ((Color)main).grayscale);
            foreach (var i in idxs)
            {
                float k = Mathf.Clamp(col[i].grayscale / mb, 0.5f, 1.2f);
                var c = tiers[t] * k; c.a = col[i].a;
                col[i] = c;
            }
        }
        target.colors = col;
        EditorUtility.SetDirty(target);
    }


    static void MakeWindZone(
Transform bayRoot, List<Vector2> pts, List<float> dist, float dMid, Vector3 windDir, Transform course, List<string> log, float len, float halfW, float strength, string meshName)
    {
        var tmplArea = Object.FindObjectsOfType<FuwaWindArea>(true).FirstOrDefault(w => w.transform.parent != null && w.transform.parent.name == "G3_CrossRight");
        At(pts, dist, dMid, out var c, out var f);
        windDir.y = 0f; windDir.Normalize();
        // 箱：道に沿って len、横は道より少し広く（斜めの風でも道の上を覆うように）
        float wid = halfW * 2f + 2f, hei = 4f;
        var zone = new GameObject("WindZone"); zone.transform.SetParent(bayRoot, false);
        zone.transform.position = c + Vector3.up * (hei * 0.5f - 0.3f);
        var acrossPath = new Vector3(f.z, 0f, -f.x);
        zone.transform.rotation = Quaternion.LookRotation(acrossPath);   // 箱は道にそろえる（風の向きは localDirection で。斜めの風でも道の上をむらなく覆う）
        zone.transform.localScale = new Vector3(len, hei, wid);   // ローカルz＝風の向き（道を横切る）、x＝道に沿う
        zone.layer = 0;
        var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = Vector3.one;
        var wa = UdonSharpUndo.AddComponent<FuwaWindArea>(zone);
        var so = new SerializedObject(wa);
        so.FindProperty("localDirection").vector3Value = Quaternion.Inverse(zone.transform.rotation) * windDir;
        so.FindProperty("strength").floatValue = strength;
        so.FindProperty("offSeconds").floatValue = 0f;
        if (tmplArea != null)
        {
            so.FindProperty("highlightAlpha").floatValue = tmplArea.highlightAlpha;
            so.FindProperty("highlightSpeedMin").floatValue = tmplArea.highlightSpeedMin;
            so.FindProperty("highlightSpeedMax").floatValue = tmplArea.highlightSpeedMax;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // 地面の＞＞＞：道の上だけ（UVのx＝風の向きに沿った距離m、y＝道に沿った距離m。ふちはぼかす）
        var V = new List<Vector3>(); var C = new List<Color>(); var UV = new List<Vector2>(); var T = new List<int>();
        float[] across = { -halfW, -halfW + 0.35f, halfW - 0.35f, halfW };
        float[] aAcross = { 0f, 1f, 1f, 0f };
        float s0 = dMid - len * 0.5f, s1 = dMid + len * 0.5f; int rows = 0;
        Vector3 origin = c + Vector3.up * 0.005f;
        for (float s = s0; s <= s1 + 0.001f; s += 0.5f, rows++)
        {
            At(pts, dist, s, out var pc, out var pf);
            Vector3 outw = new Vector3(pf.z, 0, -pf.x);
            float aRow = Mathf.Clamp01(Mathf.Min(s - s0, s1 - s) / 0.8f);
            for (int j = 0; j < across.Length; j++)
            {
                Vector3 w = pc + Vector3.up * 0.005f + outw * across[j];
                V.Add(w - origin);
                UV.Add(new Vector2(Vector3.Dot(w - origin, windDir), s - dMid));
                C.Add(new Color(1f, 1f, 1f, aAcross[j] * aRow));
            }
        }
        for (int r = 0; r < rows - 1; r++)
            for (int j = 0; j < across.Length - 1; j++)
            {
                int a = r * across.Length + j, b2 = a + across.Length;
                T.AddRange(new[] { a, b2, a + 1, a + 1, b2, b2 + 1 });
            }
        for (int i = 0; i < T.Count; i += 3)
            if (Vector3.Cross(V[T[i + 1]] - V[T[i]], V[T[i + 2]] - V[T[i]]).y < 0f) { int tmp = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = tmp; }
        var m = new Mesh { name = meshName }; m.SetVertices(V); m.SetColors(C); m.SetUVs(0, UV); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        string path = MeshDir + "/" + meshName + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) AssetDatabase.CreateAsset(m, path); else { ex.Clear(); EditorUtility.CopySerialized(m, ex); m = ex; }
        var hl = new GameObject("WindHighlight"); hl.transform.SetParent(bayRoot, false); hl.transform.position = origin; hl.transform.rotation = Quaternion.identity;
        hl.AddComponent<MeshFilter>().sharedMesh = m;
        var hr = hl.AddComponent<MeshRenderer>();
        hr.sharedMaterial = tmplArea != null && tmplArea.groundHighlight != null ? tmplArea.groundHighlight.sharedMaterial : null;
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; hr.receiveShadows = false;
        SetU(wa, "groundHighlight", hr);

        // 風の粒：コース1の横風の粒をコピーして、この区画の箱に合わせる（粒の向き＝風の向き）
        if (tmplArea != null && tmplArea.visual != null)
        {
            var wp = Object.Instantiate(tmplArea.visual, bayRoot, false); wp.name = "WindParticles";
            wp.transform.position = c + Vector3.up * 1.5f;
            wp.transform.rotation = Quaternion.LookRotation(windDir);
            wp.transform.localScale = Vector3.one;
            var ps = wp.GetComponent<ParticleSystem>();
            var sh = ps.shape; sh.position = Vector3.zero; sh.rotation = Vector3.zero; sh.scale = new Vector3(len, 3f, wid);
            // 量は箱の大きさに合わせる（元：6.6×5×12 の箱で rateOverTime）
            var srcSh = tmplArea.visual.GetComponent<ParticleSystem>().shape;
            float k = (len * wid) / Mathf.Max(1f, srcSh.scale.x * srcSh.scale.z);
            var em = ps.emission; em.rateOverTimeMultiplier *= k;
            var mn = ps.main; mn.maxParticles = Mathf.CeilToInt(mn.maxParticles * k) + 4;
            SetU(wa, "visual", wp);
        }
        Sync(wa);
        log.Add("wind zone");
    }

    // 看板（ロビーの BestTimes の板と文字をコピー）。pos＝道のふちの足もと、face＝看板の向き（来る人の方を向く）
    static void MakeSign(Transform parent, Vector3 pos, Vector3 face, string ja, string en, Transform signSrc, FuwaLanguage lang, string name = "Sign")
    {
        var sign = new GameObject(name).transform; sign.SetParent(parent, false);
        sign.position = new Vector3(pos.x, Top, pos.z);
        sign.rotation = Quaternion.LookRotation(face);
        var panel = Dup(signSrc.Find("Panel"), sign, "Panel"); var text = Dup(signSrc.Find("Text"), sign, "Text");
        panel.transform.localPosition = new Vector3(0, 1.55f, 0.01f); panel.transform.localRotation = Quaternion.identity; panel.transform.localScale = Vector3.one * 0.62f;
        var sr = panel.GetComponent<SpriteRenderer>(); sr.size = new Vector2(3.0f, 1.45f);
        text.transform.localPosition = new Vector3(0, 1.55f, 0f); text.transform.localRotation = Quaternion.identity; text.transform.localScale = Vector3.one * 0.62f;
        var tm = text.GetComponent<TMPro.TextMeshPro>(); tm.text = ja; tm.alignment = TMPro.TextAlignmentOptions.Center;
        tm.rectTransform.sizeDelta = new Vector2(2.75f, 1.25f); tm.enableAutoSizing = true; tm.fontSizeMax = 2.2f; tm.fontSizeMin = 0.8f;
        var recs = text.GetComponent<FuwaRecords>(); if (recs != null) Object.DestroyImmediate(recs);
        AddLang(lang, tm, ja, en);
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); leg.name = "Leg"; Object.DestroyImmediate(leg.GetComponent<Collider>());
        leg.transform.SetParent(sign, false); leg.transform.localPosition = new Vector3(0, 0.55f, 0.03f); leg.transform.localScale = new Vector3(0.07f, 0.55f, 0.07f);
        leg.GetComponent<Renderer>().sharedMaterial = Mat("Course_Wood");
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

    // つながっている部品ごとに、部品の中心が開ける範囲（島のふちの輪の上）にあれば消す。残す三角形を返す
    static List<int> KeepParts(Mesh src, Vector3 center, out int removedParts)
    {
        var v = src.vertices; var tris = src.triangles;
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
        var keepTri = new List<int>(); removedParts = 0; var removedRoots = new HashSet<int>();
        foreach (var r in sum.Keys) if (InGap(sum[r] / cnt[r], center, 3.05f)) { removedRoots.Add(r); removedParts++; }
        for (int i = 0; i < tris.Length; i += 3) if (!removedRoots.Contains(Find(tris[i]))) keepTri.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
        return keepTri;
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
        var keepTri = KeepParts(srcVis, cVis, out int removedParts);
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
        // 遠くで切り替わる軽い版（Fence/Fence_Far）は、開けた見た目をそのまま間引いて作る
        // （前は間引き済みの柵から三角形ごとに抜いていて、切り口に穴が開いて遠くで欠けて見えた）
        var farT = fence.Find("Fence_Far");
        if (farT != null)
        {
            var ls = fence.lossyScale; float cell = FuwaFarViewSetup.FarCell;
            var far = FuwaFarViewSetup.Simplify(vis, new Vector3(cell / Mathf.Abs(ls.x), cell / Mathf.Abs(ls.y), cell / Mathf.Abs(ls.z)));
            far.name = "Course0_StartFence_Open_Far";
            far = Save(far, MeshDir + "/Course0_StartFence_Open_Far.asset");
            farT.GetComponent<MeshFilter>().sharedMesh = far;
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
        // 作り直しで消えた文字（null）を、3D の文字（texts/ja/en）と UI の文字（uiTexts/uiJa/uiEn）の両方から詰める
        var so = new SerializedObject(lang);
        int removed = 0;
        foreach (var (tn, jn, en) in new[] { ("texts", "ja", "en"), ("uiTexts", "uiJa", "uiEn") })
        {
            var tx = so.FindProperty(tn); var pj = so.FindProperty(jn); var pe = so.FindProperty(en);
            var keepT = new List<Object>(); var keepJ = new List<string>(); var keepE = new List<string>();
            int rm = 0;
            for (int i = 0; i < tx.arraySize; i++)
            {
                var o = tx.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o == null) { rm++; continue; }
                keepT.Add(o);
                keepJ.Add(i < pj.arraySize ? pj.GetArrayElementAtIndex(i).stringValue : "");
                keepE.Add(i < pe.arraySize ? pe.GetArrayElementAtIndex(i).stringValue : "");
            }
            if (rm == 0) continue;
            removed += rm;
            tx.arraySize = keepT.Count; pj.arraySize = keepT.Count; pe.arraySize = keepT.Count;
            for (int i = 0; i < keepT.Count; i++)
            {
                tx.GetArrayElementAtIndex(i).objectReferenceValue = keepT[i];
                pj.GetArrayElementAtIndex(i).stringValue = keepJ[i];
                pe.GetArrayElementAtIndex(i).stringValue = keepE[i];
            }
        }
        if (removed > 0) so.ApplyModifiedPropertiesWithoutUndo();
        return removed;
    }

    // 島の柵（前から leftDeg 度左）に貼る、レーザーで押すワープボタン。ロビーの設定ボタン（HudButton）と同じ
    // ワールド空間の Canvas＋VRCUiShape＋Button（角丸の板 RoundedPanel）。onClick → FuwaInteractWarp.Press → warp
    // 入口の島の「説明をとばす」と、キノコの島の「説明にもどる」で使う
    static string FenceButton(Transform parent, Transform ground, Transform fence, Vector3 center, Vector3 fwd, float leftDeg,
        string name, string ja, string en, FuwaCourseWarp warp, FuwaLanguage lang)
    {
        var src = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HudButton" && t.parent != null && t.parent.name == "SettingsStand").Find("Canvas");
        float groundTop = ground.GetComponent<Renderer>().bounds.max.y;
        fwd.y = 0f; fwd.Normalize();
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        // 前から leftDeg 度左の向きで、柵の当たり判定の内側の面を探す
        Vector3 dir = (fwd * Mathf.Cos(leftDeg * Mathf.Deg2Rad) - right * Mathf.Sin(leftDeg * Mathf.Deg2Rad)).normalized;
        Physics.SyncTransforms();
        var fenceCol = fence.GetComponent<Collider>();
        Vector3 from = new Vector3(center.x, groundTop + 0.4f, center.z);
        float r = fenceCol.Raycast(new Ray(from, dir), out var hit, 6f) ? hit.distance : 3.2f;
        var b = new GameObject(name).transform; b.SetParent(parent, false);
        // 柵の内側に、少し上向きに傾けて貼る（柵に向かって立った人が見下ろして読める）
        b.position = from + dir * (r - 0.07f) + Vector3.up * 0.12f;
        b.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-20f, 0f, 0f);
        // 押した時の処理（Canvas とは別の物に付ける：ここに当たり判定はないのでクリックの「Use」は出ない）
        var iw = UdonSharpUndo.AddComponent<FuwaInteractWarp>(b.gameObject);
        SetU(iw, "warp", warp); Sync(iw);
        var ub = b.GetComponent<VRC.Udon.UdonBehaviour>();

        var cv = Object.Instantiate(src.gameObject, b, false); cv.name = "Canvas"; cv.SetActive(true);
        var crt = (RectTransform)cv.transform;
        crt.localPosition = Vector3.zero; crt.localRotation = Quaternion.identity; crt.localScale = Vector3.one * 0.00045f;
        crt.sizeDelta = new Vector2(1250f, 330f);
        var box = cv.GetComponent<BoxCollider>(); if (box != null) { box.size = new Vector3(1250f, 330f, 20f); box.center = Vector3.zero; }
        var btn = cv.GetComponentInChildren<UnityEngine.UI.Button>(true);
        var seg = btn.transform.Find("Seg"); if (seg != null) Object.DestroyImmediate(seg.gameObject);   // ON/OFF の切り替え表示はいらない
        var text = btn.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>(); text.gameObject.SetActive(true);
        text.text = ja; text.fontSize = 150f; text.alignment = TMPro.TextAlignmentOptions.Center;
        // onClick を付け替える（元は HUD の切り替え）
        while (btn.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, 0);
        UnityEditor.Events.UnityEventTools.AddStringPersistentListener(btn.onClick, new UnityEngine.Events.UnityAction<string>(ub.SendCustomEvent), "Press");
        EditorUtility.SetDirty(btn);
        AddLangUI(lang, text, ja, en);
        return $"{name} r={r:0.00}";
    }

    // UI の文字（TextMeshProUGUI）は uiTexts / uiJa / uiEn に入れる
    static void AddLangUI(FuwaLanguage lang, TMPro.TextMeshProUGUI t, string ja, string en)
    {
        var so = new SerializedObject(lang);
        var tx = so.FindProperty("uiTexts"); var pj = so.FindProperty("uiJa"); var pe = so.FindProperty("uiEn");
        int n = tx.arraySize;
        tx.arraySize = n + 1; pj.arraySize = n + 1; pe.arraySize = n + 1;
        tx.GetArrayElementAtIndex(n).objectReferenceValue = t;
        pj.GetArrayElementAtIndex(n).stringValue = ja;
        pe.GetArrayElementAtIndex(n).stringValue = en;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (lang is UdonSharp.UdonSharpBehaviour u) UdonSharpEditorUtility.CopyProxyToUdon(u);
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
                CrossStripes(bayRoot, bayRoot.Find("Lane_Tap"), islandCenter);
                break;
            }
            case "Down":
                // 銃を高めにして、斜め下のふわふわを撃つ（ねらった線も見せる：下を向いているのに上がる）
                MakeLane(bayRoot, "Lane", islandCenter - along * 1.0f, along, 1.2f, new Vector3(0, -0.86f, 0.9f), new[] { 1.3f }, new[] { 1f }, null, null, true, tmpl, ballSrc, floorTop);
                CrossStripes(bayRoot, bayRoot.Find("Lane"), islandCenter);
                break;
            case "Steep":
            {
                // 2台ならべて、同じ強さ（半分溜め。ちょん押しだと差が0.2mしか出ない）で撃つ。左＝低め（斜め約15°）→約2m先、右＝上向きすぎ（約75°）→高く上がって0.6mくらいしか進まない
                // 落ちる所に的（本物と同じ計算）。高く上がった方は落ちるまでが長いので、1回分の長さもそれに合わせる
                var lat = Vector3.Cross(Vector3.up, along).normalized;
                MakeLane(bayRoot, "Lane_Low", islandCenter - along * 1.5f - lat * 0.55f, along, 0.8f, new Vector3(0, 0.25f, 0.95f), new[] { 1.3f }, new[] { 0.5f }, null, null, false, tmpl, ballSrc, floorTop);
                MakeLane(bayRoot, "Lane_High", islandCenter - along * 1.5f + lat * 0.55f, along, 0.15f, new Vector3(0, 1.0f, 0.08f), new[] { 1.3f }, new[] { 0.5f }, null, null, false, tmpl, ballSrc, floorTop);
                float longest = 0f;
                foreach (var ln in new[] { "Lane_Low", "Lane_High" })
                {
                    var lane = bayRoot.Find(ln);
                    var nz = lane.Find("DemoGun/Nozzle"); var bs = lane.Find("BallStart");
                    Vector3 land = SimDemoLanding(tmpl, ballSrc, nz.position, nz.forward, bs.position, 0.5f, Vector3.zero, IslandTop);
                    longest = Mathf.Max(longest, LastSimTime);
                    MakeTargetRing(bayRoot, "Target_" + ln.Substring(5), new Vector3(land.x, IslandTop + 0.012f, land.z));
                    var flat = land - bs.position; flat.y = 0f;
                    log.Add($"{ln} forward {Vector3.Dot(flat, along):0.00}m, {LastSimTime:0.0}s");
                }
                foreach (var ln in new[] { "Lane_Low", "Lane_High" })
                {
                    var ds = bayRoot.Find(ln).GetComponent<FuwaDemoStand>();
                    SetUF(ds, "cycleLength", Mathf.Max(4f, 1.3f + longest + 1.2f)); Sync(ds);
                }
                CrossStripes(bayRoot, bayRoot.Find("Lane_Low"), islandCenter);
                break;
            }
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
            case "Air":
            {
                // 2台ならべる（ちょん押しと溜めの台と同じ）。どちらも下からちょん押し6発で浮かせ続けて、7発目でスカッ（空気ぎれ）。
                // 左：すぐ溜めて満タン撃ち（溜めている間に空気が一気に戻る）／右：待って自然に戻ってから、またちょん押し
                // 時刻は本物と同じ計算で確かめた（ふわふわは床まで落ちず、どの弾も当たる）
                var lat = Vector3.Cross(Vector3.up, along).normalized;
                var taps = new List<float>(); for (int i = 0; i < 7; i++) taps.Add(0.6f + 0.4f * i);
                var tA = new List<float>(taps) { 3.65f }; var cA = new float[tA.Count]; cA[cA.Length - 1] = 1f;
                var hA = new float[tA.Count]; hA[hA.Length - 1] = 0.15f;
                var tB = new List<float>(taps) { 4.0f, 4.4f, 4.8f }; var cB = new float[tB.Count];
                // 銃が主役なので大きく・胸の高さ・道寄り（2026-10-08 てじゃ「銃が下すぎる」）。ふわふわは見せない（hideBall）：
                // 銃は道から見て向こう向き・少し上向き（自分で持っている時と同じ向きで、銃の後ろのゲージが道から見える）
                Vector3 bo = new Vector3(0, AirBallUp, AirBallBack);
                MakeLane(bayRoot, "Lane_Charge", islandCenter - along * 0.9f - lat * 0.7f, along, AirGunHeight, bo, tA.ToArray(), cA, null, hA, false, tmpl, ballSrc, floorTop, AirGunScale);
                MakeLane(bayRoot, "Lane_Wait", islandCenter - along * 0.9f + lat * 0.7f, along, AirGunHeight, bo, tB.ToArray(), cB, null, null, false, tmpl, ballSrc, floorTop, AirGunScale);
                foreach (var ln in new[] { "Lane_Charge", "Lane_Wait" })
                {
                    var lane = bayRoot.Find(ln); var ds = lane.GetComponent<FuwaDemoStand>(); var gun = lane.Find("DemoGun");
                    var gauges = gun.GetComponentsInChildren<Renderer>(true).Where(g => g.name.StartsWith("GunGauge")).ToArray();
                    foreach (var g in gauges) g.enabled = true;
                    var so = new SerializedObject(ds); var ga = so.FindProperty("gunGauges"); ga.arraySize = gauges.Length;
                    for (int i = 0; i < gauges.Length; i++) ga.GetArrayElementAtIndex(i).objectReferenceValue = gauges[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    SetUB(ds, "useAir", true); SetUB(ds, "hideBall", true); SetUF(ds, "cycleLength", 7f);
                    var es = gun.Find("Nozzle/EmptySfx"); if (es != null) SetU(ds, "emptySound", es.GetComponent<AudioSource>());
                    SetU(ds, "hudColors", tmpl.hud);
                    Sync(ds);
                }
                break;
            }
            case "Wind":
            {
                // 横風（道の側＝左手へ吹く）。見本は1台：まっすぐ的へ撃って、流されて外れる。的＝風がなければ落ちる所（本物と同じ計算）
                // 風上へ向けて撃つ見本はなくした（2026-10-08 てじゃ「風の仕組みくらいさすがに分かってほしい」）
                Vector3 wind = -outw * WindStrength;
                MakeLane(bayRoot, "Lane_Straight", islandCenter - along * 1.0f, along, 0.95f, new Vector3(0, 0.05f, 1.0f), new[] { 1.3f }, new[] { 0f }, null, null, false, tmpl, ballSrc, floorTop);
                {
                    var lane = bayRoot.Find("Lane_Straight"); var ds = lane.GetComponent<FuwaDemoStand>();
                    var so = new SerializedObject(ds); so.FindProperty("windAccel").vector3Value = wind; so.ApplyModifiedPropertiesWithoutUndo();
                    Sync(ds);
                    var nz = lane.Find("DemoGun/Nozzle"); var bs = lane.Find("BallStart");
                    Vector3 land = SimDemoLanding(tmpl, ballSrc, nz.position, nz.forward, bs.position, 0f, Vector3.zero, IslandTop);
                    MakeTargetRing(bayRoot, "Target_Straight", new Vector3(land.x, IslandTop + 0.012f, land.z));
                    Vector3 drift = SimDemoLanding(tmpl, ballSrc, nz.position, nz.forward, bs.position, 0f, wind, IslandTop);
                    log.Add($"wind demo: lands {(drift - land).magnitude:0.00}m off the ring");
                }
                break;
            }
            case "Monster":
            {
                // 2台ならべる。どちらもふわふわの前へ見た目だけのまもの（コースの FuwaEnemy のコピー）が寄ってきて、ぷるぷる溜める。
                // 左：銃はあるけど撃たない → 体当たりでふわふわが吹っ飛ばされて落ちる／右：ぷるぷるの間に銃がちょん押し → 押し返されて目を回す
                // （左も銃を見せる。ふわふわだけだと何の見本か分かりにくい 2026-10-08 てじゃ）
                var lat = Vector3.Cross(Vector3.up, along).normalized;
                var enemySrc = Object.FindObjectsOfType<FuwaEnemy>(true).First(e => !e.sleeping && !e.hideInNest);
                const float ShotAt = 2.1f;
                for (int li = 0; li < 2; li++)
                {
                    bool shoots = li == 1;
                    string ln = shoots ? "Lane_Shoot" : "Lane_NoShot";
                    Vector3 basePos = islandCenter - along * 1.15f + lat * (shoots ? 0.75f : -0.75f);
                    // ふわふわは銃の横（風の円すいの外）。銃はまものがぷるぷるする所をねらう
                    Vector3 bo = new Vector3(shoots ? -0.45f : 0.45f, -0.35f, 0.15f);
                    MakeLane(bayRoot, ln, basePos, along, 0.95f, bo, shoots ? new[] { ShotAt } : new float[0], shoots ? new[] { 0f } : new float[0], null, null, false, tmpl, ballSrc, floorTop);
                    var lane = bayRoot.Find(ln); var ds = lane.GetComponent<FuwaDemoStand>();
                    SetUF(ds, "cycleLength", 5.8f);
                    var bs = lane.Find("BallStart"); var gun = lane.Find("DemoGun"); var nz = gun.Find("Nozzle");
                    Vector3 wind = bs.position + along * 1.0f; wind.y = IslandTop + 0.25f;
                    Vector3 homeP = wind + along * 1.0f;
                    var wt = new GameObject("WindupSpot").transform; wt.SetParent(lane, false); wt.position = wind;
                    var ht = new GameObject("MonsterHome").transform; ht.SetParent(lane, false); ht.position = homeP;
                    {
                        Vector3 aimPt = wind + Vector3.up * 0.15f;
                        for (int it = 0; it < 30; it++) gun.rotation = Quaternion.LookRotation((aimPt - nz.position).normalized, Vector3.up);
                    }
                    Sync(ds);
                    // まものの見た目と音（コースのまもののコピー）
                    var mon = new GameObject("DemoMonster").transform; mon.SetParent(lane, false); mon.position = homeP;
                    mon.rotation = Quaternion.LookRotation(-along);
                    Transform Copy(string n) { var src = enemySrc.transform.Find(n); if (src == null) return null; var c = Object.Instantiate(src.gameObject, mon, false); c.name = n; return c.transform; }
                    var body = Copy("Body"); var eyes = Copy("Eyes");
                    var wsfx = Copy("WindupSfx"); var jsfx = Copy("JumpSfx"); var bsfx = Copy("BlownSfx");
                    foreach (var col in mon.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                    var dm = UdonSharpUndo.AddComponent<FuwaDemoMonster>(mon.gameObject);
                    SetU(dm, "stand", ds); SetU(dm, "visual", body); SetU(dm, "eyes", eyes);
                    SetU(dm, "home", ht); SetU(dm, "windupSpot", wt);
                    if (wsfx != null) SetU(dm, "windupSound", wsfx.GetComponent<AudioSource>());
                    if (jsfx != null) SetU(dm, "dashSound", jsfx.GetComponent<AudioSource>());
                    if (bsfx != null) SetU(dm, "blownSound", bsfx.GetComponent<AudioSource>());
                    // ふわふわに当たった時の音（本物はまものの本体に付いている AudioSource）
                    if (enemySrc.hitSound != null)
                    {
                        var hsfx = Object.Instantiate(enemySrc.hitSound.gameObject, mon, false);
                        hsfx.name = "HitSfx";
                        foreach (Transform ch in hsfx.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(ch.gameObject);
                        foreach (var c in hsfx.GetComponents<Component>()) if (!(c is Transform) && !(c is AudioSource)) Object.DestroyImmediate(c);
                        hsfx.transform.localPosition = Vector3.zero; hsfx.transform.localRotation = Quaternion.identity;
                        SetU(dm, "hitSound", hsfx.GetComponent<AudioSource>());
                    }
                    SetUF(dm, "dashSpeed", enemySrc.dashSpeed); SetUF(dm, "hopHeight", enemySrc.hopHeight);
                    SetUF(dm, "maxHopHeight", enemySrc.maxHopHeight); SetUF(dm, "ballOvershoot", enemySrc.ballOvershoot);
                    SetUF(dm, "ballHitRadius", enemySrc.ballHitRadius);
                    SetUF(dm, "ballKnockback", enemySrc.ballKnockback); SetUF(dm, "ballKnockUp", enemySrc.ballKnockUp);
                    SetUF(dm, "shotAt", shoots ? ShotAt : -1f);
                    Sync(dm);
                }
                break;
            }
            case "Pull":
                // 少し離れたふわふわを、長く溜めて引き寄せてから撃つ
                MakeLane(bayRoot, "Lane", islandCenter - along * 1.3f, along, 0.95f, new Vector3(0, 0.05f, 1.42f), new[] { 3.6f }, new[] { 1f }, null, new[] { 2.5f }, false, tmpl, ballSrc, floorTop);
                break;
        }
    }

    // 島の上に、撃つ向きと直角の横じま（目もり代わり：どこまで飛んだか・上がったかが分かるように）。
    // ふわふわの出る所を通る線がしまの真ん中に来る
    static void CrossStripes(Transform bayRoot, Transform lane, Vector3 islandCenter)
    {
        Vector3 sdir = lane.Find("DemoGun").forward; sdir.y = 0; sdir.Normalize();
        MakeStripes(bayRoot, new Vector3(islandCenter.x, IslandTop + 0.012f, islandCenter.z), Vector3.Cross(Vector3.up, sdir).normalized, lane.Find("BallStart").position, "CrossStripes");
    }

    // ballOffset：銃口から見た (右, 上, 前) のふわふわの位置
    static void MakeLane(Transform parent, string name, Vector3 basePos, Vector3 d, float gunHeight, Vector3 ballOffset, float[] times, float[] charges, Vector3[] aimOffsets, float[] holds, bool showAimLine, FuwaBlower tmpl, FuwaBall ballSrc, Transform floorTop, float gunScale = 1f)
    {
        var lane = new GameObject(name).transform; lane.SetParent(parent, false);
        lane.position = new Vector3(basePos.x, IslandTop, basePos.z); lane.rotation = Quaternion.LookRotation(d);
        // 銃は台の棒なしで浮かせる（見えない誰かが持って撃っている見本。2026-10-08）
        var gun = new GameObject("DemoGun").transform; gun.SetParent(lane, false); gun.localPosition = new Vector3(0, gunHeight, 0);
        foreach (var cn in new[] { "Visual", "Nozzle" }) { var cp = Object.Instantiate(tmpl.transform.Find(cn).gameObject, gun, false); cp.name = cn; }
        foreach (var g in gun.GetComponentsInChildren<Renderer>(true)) if (g.name.StartsWith("GunGauge")) g.enabled = false;
        gun.localScale = Vector3.one * gunScale;   // 銃そのものを見せたい区画（空気ぎれ）は大きく
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
