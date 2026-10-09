using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// まもののすの最初と最後の木の橋を、ほかのコースと同じシマシマ道（巣の小枝っぽい茶色）に置きかえる（2026-10-09 てじゃ
// 「道と道で挟めば、道の途中にできた巣っぽい」）。見た目だけ差しかえ：当たり判定は橋のもの（BridgeA_Col / BridgeD_Col）をそのまま使う。
// 道の上面は橋の上面と同じ高さ。スタート島の側は島の下にもぐらせる、ゴールの側は島のふちまで。
// 巣とのつなぎ目は「道が割れて途切れていて、まものが小枝を並べてツタでしばってつないだ」形（2026-10-09 てじゃ「割れてる道に、なにかで繋いでるみたいな」）。
// 何度実行してもよい（道は作り直し、橋の見た目は非表示にするだけで消さない）
public static class FuwaCourse4Roads
{
    const float Thick = 0.3f, Bevel = 0.08f, Stripe = 0.6f;
    const int BevelSteps = 4, TopSteps = 14;
    const float GapA = 1.9f, GapD = 0.75f;   // 割れ目（小枝でつないだ所）の長さ
    const float JagMax = 0.36f;              // 割れたはしの、いちばん欠けている所の深さ
    const string MeshDir = "Assets/FuwaCourse/Meshes/Course4";

    [MenuItem("FuwaCourse/まもののす/最初と最後の橋をシマシマ道にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var c4 = GameObject.Find("FuwaCourse4").transform;
        var den = c4.Find("Den");
        Physics.SyncTransforms();   // 島を動かした直後でも、当たり判定を新しい位置で測れるように
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Course4");
        var matA = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/LobbyCourse4_PathA.mat");
        var matB = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/LobbyCourse4_PathB.mat");
        var log = new System.Text.StringBuilder();
        var startG = c4.Find("Start/StartArea/Ground"); var goalG = c4.Find("Goal/GoalGround");
        // (橋, 始まりの島, 終わりの島, 始まり側の床, 終わり側の床, …, 始まり側の割れ目, 終わり側の割れ目)
        log.Append(Road(den, "BridgeA", startG.position, den.Find("Nest1").GetComponent<Renderer>().bounds.center, null, den.Find("Nest1_Col").GetComponent<Collider>(), matA, matB, "Course4_Road_Start", 0f, GapA));
        log.Append(Road(den, "BridgeD", den.Find("MamaNestCenter").position, goalG.position, den.Find("MamaNest_Col").GetComponent<Collider>(), goalG.GetComponent<Collider>(), matA, matB, "Course4_Road_Goal", GapD, 0f));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c4.gameObject.scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        m.name = name;
        string path = MeshDir + "/" + name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) { AssetDatabase.CreateAsset(m, path); return m; }
        ex.Clear(); EditorUtility.CopySerialized(m, ex); ex.name = name; return ex;
    }

    // a→b の向きに、橋の当たり判定の上面（幅・高さ）を測って道を作る。
    // fromCol があれば「a 側の島（巣）の床が終わる所」から、なければ橋のはしから（島の下にもぐる）。toCol も同じ。
    // gap0 / gap1：そのはしの手前で道を割って、その長さを小枝でつなぐ
    static string Road(Transform den, string bridge, Vector3 a3, Vector3 b3, Collider fromCol, Collider toCol, Material mA, Material mB, string name, float gap0, float gap1)
    {
        var br = den.Find(bridge); var bcolT = den.Find(bridge + "_Col"); var bcol = bcolT.GetComponent<Collider>();
        bool bcolWasOn = bcolT.gameObject.activeSelf; bcolT.gameObject.SetActive(true);   // 測るあいだだけ（道の当たり判定に置きかえた後も測れるように）
        Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z), dir = (b - a).normalized, right = new Vector2(dir.y, -dir.x);
        float L = (b - a).magnitude;
        bool Hit(Collider c, Vector2 p, out float y) { y = 0f; if (c.Raycast(new Ray(new Vector3(p.x, 50f, p.y), Vector3.down), out var h, 100f)) { y = h.point.y; return true; } return false; }
        // 橋の範囲・幅・上面
        float s0 = 99f, s1 = -99f, xl = 99f, xr = -99f, top = -99f;
        for (float s = 0f; s <= L; s += 0.05f)
            for (float x = -3f; x <= 3f; x += 0.05f)
                if (Hit(bcol, a + dir * s + right * x, out var y)) { s0 = Mathf.Min(s0, s); s1 = Mathf.Max(s1, s); xl = Mathf.Min(xl, x); xr = Mathf.Max(xr, x); top = Mathf.Max(top, y); }
        // はし：島（巣）の床が終わる所。なければ橋のはし
        float e0 = s0, e1 = s1;
        if (fromCol != null) { for (float s = 0f; s <= L; s += 0.02f) if (!Hit(fromCol, a + dir * s, out _)) { e0 = s; break; } }
        if (toCol != null) { for (float s = L; s >= 0f; s -= 0.02f) if (!Hit(toCol, a + dir * s, out _)) { e1 = s + 0.02f; break; } }
        float halfW = (xr - xl) * 0.5f, cx = (xr + xl) * 0.5f;
        var o = a + right * cx;
        var pos = new Vector3(o.x, 0f, o.y); var rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
        var flags = GameObjectUtility.GetStaticEditorFlags(br.gameObject);

        GameObject Put(string n, Mesh m, Material[] mats)
        {
            var old = den.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(den, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.GetComponent<MeshFilter>().sharedMesh = m;
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
            go.layer = br.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            return go;
        }
        // 道（割れ目のぶん短くして、割れた側のはしをギザギザに）
        Put(name, SaveMesh(Strip(e0 + gap0, e1 - gap1, halfW, top, gap0 > 0f, gap1 > 0f), name), new[] { mA, mB });
        // 割れ目をつなぐ小枝（巣の床の中まで少し入れる、道のギザギザの下にも少しもぐらせる）
        string pn = name + "_Patch";
        var oldP = den.Find(pn); if (oldP != null) Object.DestroyImmediate(oldP.gameObject);
        if (gap0 > 0f || gap1 > 0f)
        {
            float p0 = gap0 > 0f ? e0 - 0.25f : e1 - gap1 - JagMax;
            float p1 = gap0 > 0f ? e0 + gap0 + JagMax : e1 + 0.25f;
            // 丸太を出すのは割れ目（巣の床のふち〜道の割れたはし）だけ
            float v0 = gap0 > 0f ? e0 : e1 - gap1, v1 = gap0 > 0f ? e0 + gap0 : e1;
            Put(pn, SaveMesh(Patch(p0, p1, v0, v1, halfW, top, name.Length * 7919 + 17), pn), new[] { br.GetComponent<Renderer>().sharedMaterial });
        }
        br.gameObject.SetActive(false);   // 木の橋の見た目は消さずに非表示（当たり判定 _Col はそのまま）
        // 島（巣）どうしの間が橋より長い時（ゴールを奥へずらした等）は、橋の当たり判定の代わりに道の形の箱を置く
        string cn = name + "_Col";
        var oldC = den.Find(cn); if (oldC != null) Object.DestroyImmediate(oldC.gameObject);
        if (e0 < s0 - 0.3f || e1 > s1 + 0.3f)
        {
            float need0 = fromCol != null ? e0 - 0.3f : s0, need1 = toCol != null ? e1 + 0.3f : s1;
            var cg = new GameObject(cn, typeof(BoxCollider)); cg.transform.SetParent(den, false); cg.transform.SetPositionAndRotation(pos, rot);
            cg.layer = bcolT.gameObject.layer;
            var bx = cg.GetComponent<BoxCollider>(); bx.center = new Vector3(0f, top - Thick * 0.5f, (need0 + need1) * 0.5f); bx.size = new Vector3(halfW * 2f, Thick, need1 - need0);
            bcolWasOn = false;
        }
        bcolT.gameObject.SetActive(bcolWasOn);
        return bridge + "→" + name + " " + e0.ToString("F2") + "〜" + e1.ToString("F2") + "m 幅" + (halfW * 2f).ToString("F2") + " 上面" + top.ToString("F2") + " / ";
    }

    static float Hash(int i) { float h = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }

    // ローカル：z が道の向き（z0〜z1）、x が横。上面 top、厚み Thick、上のふちを丸める。しまは Stripe ごと。
    // jag0 / jag1：そのはしを割れたギザギザにする（断面の点ごとに引っこめる量を変える）
    static Mesh Strip(float z0, float z1, float halfW, float top, bool jag0, bool jag1)
    {
        float bot = top - Thick;
        // 断面（閉じた輪）：左下 → 左の側面 → 丸め → 上面（細かく）→ 丸め → 右の側面 → 右下 → 底（細かく）
        var prof = new List<Vector4> { new Vector4(-halfW, bot, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-halfW + Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int k = 1; k < TopSteps; k++) prof.Add(new Vector4(Mathf.Lerp(-halfW + Bevel, halfW - Bevel, k / (float)TopSteps), top, 0, 1));
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(halfW - Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(halfW, bot, 1, 0));
        for (int k = 1; k < TopSteps; k++) prof.Add(new Vector4(Mathf.Lerp(halfW, -halfW, k / (float)TopSteps), bot, 0, -1));
        int P = prof.Count;
        // 引っこみ：となりどうしで大きくちがう＝ギザギザ。底は上より少し控えめ
        float Jag(int i) { float j = 0.05f + (JagMax - 0.05f) * Hash(i * 7 + 3); return prof[i].w < -0.5f ? j * 0.7f + 0.04f : j; }
        var cuts = new List<float> { z0 };
        for (float z = Mathf.Ceil(z0 / Stripe) * Stripe; z < z1 - 1e-3f; z += Stripe) if (z > z0 + 1e-3f) cuts.Add(z);
        cuts.Add(z1);
        // ギザギザのはしの手前の区切りは、引っこみより奥に（重ならないように）
        if (jag0) { cuts.RemoveAll(z => z > z0 && z < z0 + JagMax + 0.05f); cuts.Add(z0 + JagMax + 0.05f); }
        if (jag1) { cuts.RemoveAll(z => z < z1 && z > z1 - JagMax - 0.05f); cuts.Add(z1 - JagMax - 0.05f); }
        cuts.Sort();
        int last = cuts.Count - 1;
        float ZAt(int ring, int i) { float z = cuts[ring]; if (jag0 && ring == 0) z += Jag(i); if (jag1 && ring == last) z -= Jag(i); return z; }
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var A = new List<int>(); var B = new List<int>();
        for (int s = 0; s < last; s++)
        {
            var T = (Mathf.FloorToInt((cuts[s] + cuts[s + 1]) * 0.5f / Stripe) % 2 == 0) ? A : B;
            int b0 = V.Count;
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < P; i++) { var q = prof[i]; float z = ZAt(s + r, i); V.Add(new Vector3(q.x, q.y, z)); N.Add(new Vector3(q.z, q.w, 0)); UV.Add(new Vector2(q.x, z)); }
            for (int i = 0; i < P; i++) { int i1 = (i + 1) % P; int x0 = b0 + i, x1 = b0 + i1, y0 = b0 + P + i, y1 = b0 + P + i1; T.AddRange(new[] { x0, y0, x1, x1, y0, y1 }); }
        }
        // 両はしのふた：断面のまん中の点から扇形に（割れた面はでこぼこ）
        foreach (var (ring, sgn) in new[] { (0, -1f), (last, 1f) })
        {
            float zc = cuts[ring];
            var T = Mathf.FloorToInt((zc - sgn * 0.01f) / Stripe) % 2 == 0 ? A : B;
            float jc = (ring == 0 && jag0) ? JagMax * 0.5f : (ring == last && jag1) ? -JagMax * 0.5f : 0f;
            int cb = V.Count;
            V.Add(new Vector3(0f, (top + bot) * 0.5f, zc + jc)); N.Add(new Vector3(0, 0, sgn)); UV.Add(Vector2.zero);
            for (int i = 0; i < P; i++) { var q = prof[i]; V.Add(new Vector3(q.x, q.y, ZAt(ring, i))); N.Add(new Vector3(0, 0, sgn)); UV.Add(Vector2.zero); }
            for (int i = 0; i < P; i++) T.AddRange(new[] { cb, cb + 1 + i, cb + 1 + (i + 1) % P });
        }
        foreach (var T in new[] { A, B })
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 p0 = V[T[i]], p1 = V[T[i + 1]], p2 = V[T[i + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), N[T[i]] + N[T[i + 1]] + N[T[i + 2]]) < 0f) { int t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t; }
            }
        var m = new Mesh();
        m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV);
        m.subMeshCount = 2; m.SetTriangles(A, 0); m.SetTriangles(B, 1);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    // 割れ目をつなぐ小枝：道を横切る丸太（両はしは少し細い）を z0〜z1 にすき間なく並べ、左右2か所をツタでしばる。
    // 色は頂点カラー（橋と同じ Den_Twig の材質）。丸太の上は道の上面とほぼ同じ高さ（当たり判定は橋の平らな面）
    static Mesh Patch(float z0, float z1, float v0, float v1, float halfW, float top, int seed)
    {
        var rnd = new System.Random(seed);
        float R(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
        void Log(Vector3 p0, Vector3 p1, float r0, float r1, Color c, int seg)
        {
            var ax = (p1 - p0).normalized; var u = Vector3.Cross(ax, Vector3.up); u = u.sqrMagnitude < 0.01f ? Vector3.right : u.normalized; var w = Vector3.Cross(u, ax);
            int b = V.Count;
            for (int e = 0; e < 2; e++)
                for (int k = 0; k < seg; k++)
                {
                    float an = k * Mathf.PI * 2f / seg; var n = u * Mathf.Cos(an) + w * Mathf.Sin(an);
                    V.Add((e == 0 ? p0 : p1) + n * (e == 0 ? r0 : r1)); N.Add(n);
                    float sh = 0.9f + 0.1f * Mathf.Sin(an * 3f + p0.z * 9f); C.Add(new Color(c.r * sh, c.g * sh, c.b * sh, 1f));
                }
            for (int k = 0; k < seg; k++) { int k1 = (k + 1) % seg; T.AddRange(new[] { b + k, b + seg + k, b + k1, b + k1, b + seg + k, b + seg + k1 }); }
            foreach (var (e, sgn) in new[] { (0, -1f), (1, 1f) })   // 切り口（明るめ）
            {
                int cb = V.Count; var cc = new Color(Mathf.Min(1f, c.r * 1.5f), Mathf.Min(1f, c.g * 1.5f), Mathf.Min(1f, c.b * 1.4f), 1f);
                V.Add(e == 0 ? p0 : p1); N.Add(ax * sgn); C.Add(cc);
                for (int k = 0; k < seg; k++) { V.Add(V[b + e * seg + k]); N.Add(ax * sgn); C.Add(cc); }
                for (int k = 0; k < seg; k++) T.AddRange(new[] { cb, cb + 1 + k, cb + 1 + (k + 1) % seg });
            }
        }
        var browns = new[] { new Color(0.47f, 0.32f, 0.21f), new Color(0.56f, 0.39f, 0.26f), new Color(0.40f, 0.27f, 0.18f), new Color(0.62f, 0.45f, 0.30f) };
        var logs = new List<(float z, float r)>();
        float zz = z0;
        while (true)
        {
            float r = R(0.065f, 0.095f);
            if (zz + 2f * r > z1) break;
            zz += r;
            float len = halfW * 2f * R(0.92f, 1.12f), off = R(-0.12f, 0.12f), yaw = R(-7f, 7f) * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var c = new Vector3(off, top - r * 0.55f + R(-0.01f, 0.015f), zz);
            float rA = r * R(0.75f, 0.95f), rB = r * R(0.75f, 0.95f); var col = browns[rnd.Next(browns.Length)];
            // 巣の床や道の下に埋まる丸太は出さない（てじゃ）。ツタは埋まる所まで続ける（床・道の上に乗る）
            bool vis = zz - r >= v0 && zz + r <= v1;
            if (vis) Log(c - d * len * 0.5f, c + d * len * 0.5f, rA, rB, col, 7);
            logs.Add((zz, vis ? r : 0f));
            zz += r + R(0f, 0.02f);
        }
        // ツタ：丸太の上を道の向きに2本。丸太ごとに少し上下して、乗っている感じに
        var vine = new Color(0.33f, 0.52f, 0.24f);
        foreach (float x in new[] { -halfW + 0.3f + R(-0.05f, 0.05f), halfW - 0.3f + R(-0.05f, 0.05f) })
            for (int i = 0; i < logs.Count - 1; i++)
            {
                var p = new Vector3(x + R(-0.02f, 0.02f), top + logs[i].r * 0.45f + 0.025f, logs[i].z);
                var q = new Vector3(x + R(-0.02f, 0.02f), top + logs[i + 1].r * 0.45f + 0.025f, logs[i + 1].z);
                Log(p, q, 0.022f, 0.022f, vine, 5);
            }
        // 三角形の向きを法線にそろえる
        for (int i = 0; i < T.Count; i += 3)
        {
            Vector3 p0 = V[T[i]], p1 = V[T[i + 1]], p2 = V[T[i + 2]];
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), N[T[i]] + N[T[i + 1]] + N[T[i + 2]]) < 0f) { int t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t; }
        }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(V); m.SetNormals(N); m.SetColors(C); m.SetTriangles(T, 0);
        m.RecalculateBounds();
        return m;
    }
}
