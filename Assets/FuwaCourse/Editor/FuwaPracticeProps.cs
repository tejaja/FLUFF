using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// れんしゅう（FuwaCourse0）の道の端に置く、特に意味のないおじゃまオブジェ。何度実行しても同じ結果（作り直し）。
// B：キャンディ杭（しましまの短い杭＋上にまんまるの玉、2〜3本ずつ高さ違い）
// D：積みマシュマロ（角の丸い円柱を2〜3個ずらして積んだ柱）
// 道のふちギリギリに、左右交互にぽつぽつ。まん中を通れば当たらない。見た目は1つのメッシュ（Den_Twig・頂点カラー）、当たり判定はカプセル
public static class FuwaPracticeProps
{
    const string Course = "FuwaCourse0";
    const string MeshPath = "Assets/FuwaCourse/Generated/Practice_EdgeProps.asset";

    class MB
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>();
        public List<Color> C = new List<Color>(); public List<int> T = new List<int>();
    }

    static readonly Color[] Pastel =
    {
        new Color(1f, 0.62f, 0.78f), new Color(0.6f, 0.88f, 0.8f), new Color(0.78f, 0.7f, 0.98f),
        new Color(1f, 0.92f, 0.62f), new Color(0.62f, 0.8f, 1f), new Color(1f, 0.75f, 0.6f),
    };
    static readonly Color Cream = new Color(1f, 0.97f, 0.92f);

    [MenuItem("FuwaCourse/れんしゅうの道ばたオブジェを作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var root = scene.GetRootGameObjects().First(g => g.name == Course).transform;
        var path = root.Find("PathMesh"); var pathCol = path.GetComponent<Collider>();
        var twig = AssetDatabase.FindAssets("Den_Twig t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).First(m => m.name == "Den_Twig");
        var rnd = new System.Random(2024);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        // ---- 道の中心線とふちを、レイで端を探しながらたどる ----
        var st = root.GetComponentsInChildren<Transform>().First(t => t.name == "BallStart");
        var goal = root.Find("Goal").position; var cp = root.Find("Checkpoint1").position;
        bool Hit(Vector3 p, out Vector3 at)
        {
            at = p;
            if (pathCol.Raycast(new Ray(p + Vector3.up * 5f, Vector3.down), out var h, 12f)) { at = h.point; return true; }
            return false;
        }
        var samples = new List<(Vector3 c, Vector3 d, float hw)>();
        Vector3 pos = st.position, dir = st.forward; dir.y = 0; dir.Normalize();
        for (int i = 0; i < 400; i++)
        {
            var side = new Vector3(-dir.z, 0, dir.x);
            float l = 0, r = 0;
            for (float o = 0; o < 8f; o += 0.05f) { if (Hit(pos + side * o, out _)) l = o; else break; }
            for (float o = 0; o < 8f; o += 0.05f) { if (Hit(pos - side * o, out _)) r = o; else break; }
            if (l == 0 && r == 0) break;
            var c = pos + side * (l - r) * 0.5f; Hit(c, out var cy);
            samples.Add((cy, dir, (l + r) * 0.5f));
            if (new Vector2(c.x - goal.x, c.z - goal.z).magnitude < 3.5f) break;
            // 少し先の中心へ向きを合わせる
            var ahead = c + dir * 1.5f; var s2 = new Vector3(-dir.z, 0, dir.x);
            float l2 = 0, r2 = 0;
            for (float o = 0; o < 8f; o += 0.1f) { if (Hit(ahead + s2 * o, out _)) l2 = o; else break; }
            for (float o = 0; o < 8f; o += 0.1f) { if (Hit(ahead - s2 * o, out _)) r2 = o; else break; }
            var c2 = ahead + s2 * (l2 - r2) * 0.5f;
            var nd = c2 - c; nd.y = 0; if (nd.sqrMagnitude > 1e-4f) dir = Vector3.Slerp(dir, nd.normalized, 0.6f).normalized;
            pos = c + dir * 0.5f;
        }

        // ---- 置き場所：長さ方向に 5〜8m おき、左右交互 ----
        var holder = root.Find("EdgeProps");
        if (holder != null) Undo.DestroyObjectImmediate(holder.gameObject);
        holder = new GameObject("EdgeProps").transform; holder.SetParent(root, false); Undo.RegisterCreatedObjectUndo(holder.gameObject, "props");
        holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity; holder.localScale = Vector3.one;
        var mb = new MB(); int count = 0, sideSign = rnd.NextDouble() < 0.5 ? 1 : -1;
        float acc = 0, next = R(3f, 4.5f); string skips = "";
        for (int i = 1; i < samples.Count; i++)
        {
            acc += (samples[i].c - samples[i - 1].c).magnitude;
            if (acc < next) continue;
            next = acc + R(3.5f, 5.5f);
            var (c, d, hw) = samples[i];
            if ((c - st.position).magnitude < 4f) { skips += "s"; continue; }
            if ((c - cp).magnitude < 3f) { skips += "c"; continue; }
            if (new Vector2(c.x - goal.x, c.z - goal.z).magnitude < 5f) { skips += "g"; continue; }
            var side = new Vector3(-d.z, 0, d.x) * sideSign; sideSign = -sideSign;
            var go = new GameObject("Prop" + (++count)).transform; go.SetParent(holder, false); go.position = c;
            go.gameObject.layer = path.gameObject.layer;
            if (rnd.NextDouble() < 0.5) CandyPosts(mb, go, c, d, side, hw, rnd);
            else Marshmallows(mb, go, c, side, hw, rnd);
        }

        var w2l = holder.worldToLocalMatrix;
        var mesh = new Mesh { name = "Practice_EdgeProps", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(mb.V.Select(x => w2l.MultiplyPoint3x4(x)).ToList()); mesh.SetNormals(mb.N.Select(x => w2l.MultiplyVector(x).normalized).ToList());
        mesh.SetColors(mb.C); mesh.SetTriangles(mb.T, 0); mesh.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (old != null) { old.Clear(); EditorUtility.CopySerialized(mesh, old); mesh = old; } else AssetDatabase.CreateAsset(mesh, MeshPath);
        var vis = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer)); vis.transform.SetParent(holder, false);
        vis.GetComponent<MeshFilter>().sharedMesh = mesh; var candy = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Practice_Candy.mat");
        if (candy == null) { candy = new Material(twig); candy.SetFloat("_Ambient", 0.62f); candy.SetFloat("_Wrap", 0.75f); AssetDatabase.CreateAsset(candy, "Assets/FuwaCourse/Materials/Practice_Candy.mat"); }   // パステルが沈まないように明るめ
        vis.GetComponent<MeshRenderer>().sharedMaterial = candy;
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "samples " + samples.Count + " props " + count + " tris " + mb.T.Count / 3 + " skips " + skips;
    }

    // B：キャンディ杭 2〜3本（ふちに沿って並べる。高さ違い）
    static void CandyPosts(MB m, Transform go, Vector3 c, Vector3 d, Vector3 side, float hw, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        int n = rnd.Next(2, 4); float along0 = -(n - 1) * 0.22f;
        for (int k = 0; k < n; k++)
        {
            float pr = R(0.07f, 0.09f), br = R(0.15f, 0.21f), h = R(0.45f, 0.95f);
            var basePt = c + side * (hw - R(0.12f, 0.2f)) + d * (along0 + k * 0.44f + R(-0.05f, 0.05f));
            var c1 = Pastel[rnd.Next(Pastel.Length)]; var c2 = Cream; var ball = Pastel[rnd.Next(Pastel.Length)];
            // しましまの杭（帯ごとに色を変える）
            int bands = Mathf.Max(3, Mathf.RoundToInt(h / 0.11f));
            for (int b = 0; b < bands; b++)
            {
                float y0 = h * b / bands, y1 = h * (b + 1) / bands;
                Cylinder(m, basePt + Vector3.up * y0, pr, y1 - y0, 12, b % 2 == 0 ? c1 : c2, b == 0, false);
            }
            Sphere(m, basePt + Vector3.up * (h + br * 0.75f), br, ball, 14, 9);
            var cc = new GameObject("Col").AddComponent<CapsuleCollider>(); cc.transform.SetParent(go, false); cc.gameObject.layer = go.gameObject.layer;
            cc.transform.position = basePt + Vector3.up * (h + br * 1.75f) * 0.5f; cc.direction = 1; cc.radius = br; cc.height = h + br * 1.75f;
        }
    }

    // D：積みマシュマロ 2〜3段（ふちから半分はみ出すくらい）
    static void Marshmallows(MB m, Transform go, Vector3 c, Vector3 side, float hw, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        int n = rnd.Next(2, 4); float r0 = R(0.3f, 0.4f);
        var basePt = c + side * (hw - r0 * 0.35f);
        float y = 0; var p = basePt; float top = 0;
        for (int k = 0; k < n; k++)
        {
            float r = r0 * (1f - k * 0.13f) * R(0.95f, 1.05f), h = r * R(0.75f, 0.95f);
            var col = Color.Lerp(Pastel[rnd.Next(Pastel.Length)], Cream, R(0.15f, 0.4f));
            var q = p + new Vector3(R(-0.06f, 0.06f), 0, R(-0.06f, 0.06f));
            var rot = Quaternion.Euler(R(-4, 4), R(0, 360), R(-4, 4));
            Marsh(m, q + Vector3.up * y, rot, r, h, col);
            y += h * 0.96f; p = q; top = y;
        }
        var cc = new GameObject("Col").AddComponent<CapsuleCollider>(); cc.transform.SetParent(go, false); cc.gameObject.layer = go.gameObject.layer;
        cc.transform.position = basePt + Vector3.up * top * 0.5f; cc.direction = 1; cc.radius = r0 * 0.95f; cc.height = Mathf.Max(top, r0 * 2f);
    }

    // 角の丸い円柱（上下のふちを丸める）
    static void Marsh(MB m, Vector3 at, Quaternion rot, float r, float h, Color col)
    {
        const int seg = 18; float bev = Mathf.Min(r, h) * 0.32f;
        var prof = new List<Vector2>();   // (半径, 高さ)
        prof.Add(new Vector2(0, 0));
        for (int i = 0; i <= 4; i++) { float a = -Mathf.PI / 2 + Mathf.PI / 2 * i / 4f; prof.Add(new Vector2(r - bev + Mathf.Cos(a) * bev, bev + Mathf.Sin(a) * bev)); }
        for (int i = 0; i <= 4; i++) { float a = Mathf.PI / 2 * i / 4f; prof.Add(new Vector2(r - bev + Mathf.Cos(a) * bev, h - bev + Mathf.Sin(a) * bev)); }
        prof.Add(new Vector2(0, h));
        Lathe(m, at, rot, prof, seg, col);
    }

    static void Cylinder(MB m, Vector3 at, float r, float h, int seg, Color col, bool capBottom, bool capTop)
    {
        var prof = new List<Vector2>();
        if (capBottom) prof.Add(new Vector2(0, 0));
        prof.Add(new Vector2(r, 0)); prof.Add(new Vector2(r, h));
        if (capTop) prof.Add(new Vector2(0, h));
        Lathe(m, at, Quaternion.identity, prof, seg, col);
    }

    static void Sphere(MB m, Vector3 c, float r, Color col, int seg, int rings)
    {
        var prof = new List<Vector2>();
        for (int j = 0; j <= rings; j++) { float th = Mathf.PI * j / rings; prof.Add(new Vector2(Mathf.Sin(th) * r, -Mathf.Cos(th) * r)); }
        Lathe(m, c, Quaternion.identity, prof, seg, col);
    }

    // 輪郭（半径, 高さ）を縦軸まわりに回した形。面ごとに法線を分けず、輪郭の向きから法線を出す（なめらか）
    static void Lathe(MB m, Vector3 at, Quaternion rot, List<Vector2> prof, int seg, Color col)
    {
        int bi = m.V.Count, np = prof.Count;
        for (int j = 0; j < np; j++)
        {
            var t = prof[Mathf.Min(j + 1, np - 1)] - prof[Mathf.Max(j - 1, 0)];
            var n2 = new Vector2(t.y, -t.x).normalized;   // 輪郭の外向き法線
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2 * i / seg; var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var p = radial * prof[j].x + Vector3.up * prof[j].y;
                var n = radial * n2.x + Vector3.up * n2.y;
                m.V.Add(at + rot * p); m.N.Add(rot * n); m.C.Add(new Color(col.r, col.g, col.b, 1));
            }
        }
        for (int j = 0; j < np - 1; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = bi + j * (seg + 1) + i, b = a + seg + 1;
                m.T.Add(a); m.T.Add(b); m.T.Add(a + 1); m.T.Add(a + 1); m.T.Add(b); m.T.Add(b + 1);
            }
    }
}
