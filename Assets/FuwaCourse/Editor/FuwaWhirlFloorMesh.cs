using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// かぜのおかの螺旋風の床のうずまき（FloorSwirl）を、今の道の形に合わせて作り直す。何度実行してもOK。
// うずまきの円（半径 Radius）のうち道の上面にかかる所だけを 4cm の格子で張り、道のふちに近い所は頂点アルファでぼかす。
// uv は円の直径で 0〜1（FuwaCourse/WhirlFloor シェーダーが uv で模様を描く）。道の形を変えたら実行し直す（2026-10-09、道幅を広げたら欠けた）
public static class FuwaWhirlFloorMesh
{
    const float Radius = 1.4f, Cell = 0.04f, Lift = 0.012f, Fade = 0.16f;

    [MenuItem("FuwaCourse/Course1/螺旋風の床のうずまきを作り直す")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var root = FuwaCourse1Builder.Root();
        var path = root.Find("PathMesh").GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        var log = new System.Text.StringBuilder();
        foreach (var w in root.GetComponentsInChildren<FuwaWhirlwind>(true))
        {
            var floor = w.transform.Find("FloorSwirl"); if (floor == null) continue;
            var mf = floor.GetComponent<MeshFilter>();
            string ap = "Assets/FuwaCourse/Meshes/WhirlFloor_" + w.name + ".asset";
            if (AssetDatabase.GetAssetPath(mf.sharedMesh) != ap) continue;   // 道のまん中の練習用（ふつうの四角い板）はそのまま

            int n = Mathf.RoundToInt(Radius * 2 / Cell);
            var on = new bool[n + 1, n + 1];
            // 道の上面（ふちの丸みより内側の平らな所）にある格子点
            float topY = floor.TransformPoint(Vector3.zero).y;
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    var lp = new Vector3(-Radius + i * Cell, 0, -Radius + j * Cell);
                    if (new Vector2(lp.x, lp.z).magnitude > Radius + Cell * 1.5f) continue;
                    var wp = floor.TransformPoint(lp);
                    if (path.Raycast(new Ray(wp + Vector3.up * 1f, Vector3.down), out var h, 2f) && h.normal.y > 0.99f && Mathf.Abs(h.point.y - topY) < 0.01f) on[i, j] = true;
                }
            // 道のふち（上面でない点）からの距離（格子をたどる近似）
            var dist = new float[n + 1, n + 1];
            var q = new Queue<(int, int)>();
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    dist[i, j] = float.MaxValue;
                    if (!on[i, j]) continue;
                    bool edge = false;
                    for (int di = -1; di <= 1 && !edge; di++) for (int dj = -1; dj <= 1; dj++) { int a = i + di, b = j + dj; if (a < 0 || b < 0 || a > n || b > n || !on[a, b]) { edge = true; break; } }
                    if (edge) { dist[i, j] = 0; q.Enqueue((i, j)); }
                }
            while (q.Count > 0)
            {
                var (i, j) = q.Dequeue();
                for (int di = -1; di <= 1; di++) for (int dj = -1; dj <= 1; dj++)
                {
                    int a = i + di, b = j + dj; if (a < 0 || b < 0 || a > n || b > n || !on[a, b]) continue;
                    float nd = dist[i, j] + Cell * Mathf.Sqrt(di * di + dj * dj);
                    if (nd < dist[a, b] - 1e-5f) { dist[a, b] = nd; q.Enqueue((a, b)); }
                }
            }
            var V = new List<Vector3>(); var U = new List<Vector2>(); var C = new List<Color>(); var N = new List<Vector3>(); var T = new List<int>();
            var idx = new int[n + 1, n + 1];
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    idx[i, j] = -1; if (!on[i, j]) continue;
                    float x = -Radius + i * Cell, z = -Radius + j * Cell;
                    idx[i, j] = V.Count;
                    V.Add(new Vector3(x, Lift, z)); U.Add(new Vector2((x + Radius) / (2 * Radius), (z + Radius) / (2 * Radius)));
                    C.Add(new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp01(dist[i, j] / Fade)))); N.Add(Vector3.up);
                }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int a = idx[i, j], b = idx[i + 1, j], c = idx[i + 1, j + 1], d = idx[i, j + 1];
                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                    // 円の外だけの四角は省く（シェーダーでも円の外は描かない）
                    float cx = -Radius + (i + 0.5f) * Cell, cz = -Radius + (j + 0.5f) * Cell;
                    if (new Vector2(cx, cz).magnitude > Radius + Cell) continue;
                    T.Add(a); T.Add(d); T.Add(c); T.Add(a); T.Add(c); T.Add(b);
                }
            var mesh = mf.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(V); mesh.SetUVs(0, U); mesh.SetColors(C); mesh.SetNormals(N); mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds(); mesh.UploadMeshData(false);
            EditorUtility.SetDirty(mesh);
            mf.sharedMesh = null; mf.sharedMesh = mesh;
            log.Append(w.name + " verts " + V.Count + " ");
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }
}
