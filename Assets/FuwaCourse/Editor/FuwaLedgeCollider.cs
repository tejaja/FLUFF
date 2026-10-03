using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 道から落ちた時に、道の横・下の岩の「乗れる段」に着地して詰まらないように、
// 岩の当たり判定から「道より下にある上向きの面」だけを抜いたメッシュを作る（見た目はそのまま）。
// 急な面（壁）は残すので、道の横の岩にめり込むことはない。
// 元の当たり判定メッシュは残して、<名前>_NoLedge.asset を作って MeshCollider に差し替える。
public static class FuwaLedgeCollider
{
    public const float MinUpNormal = 0.5f;    // これより上向きの面を「乗れる段」とみなす
    public static float Radius = 3f;          // 道からこの水平距離以内の段だけ
    public const float BelowPath = 0.5f;      // 道の上面よりこれ以上低い段だけ

    static List<Vector3> PathPoints(MeshCollider path)
    {
        var pts = new List<Vector3>();
        var m = path.sharedMesh; var v = m.vertices; var t = m.triangles; var tr = path.transform;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = tr.TransformPoint(v[t[i]]), b = tr.TransformPoint(v[t[i + 1]]), c = tr.TransformPoint(v[t[i + 2]]);
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (n.y < 0.6f) continue;   // 道の上面だけ
            // 三角形の中を細かく（だいたい0.5m間隔）
            float len = Mathf.Max((b - a).magnitude, (c - a).magnitude);
            int k = Mathf.Clamp(Mathf.CeilToInt(len / 0.5f), 1, 40);
            for (int i1 = 0; i1 <= k; i1++)
                for (int i2 = 0; i2 <= k - i1; i2++)
                    pts.Add(a + (b - a) * (i1 / (float)k) + (c - a) * (i2 / (float)k));
        }
        return pts;
    }

    // 抜く三角形の判定（world 座標）
    public static List<bool> Ledges(MeshCollider rock, List<Vector3> pathPts)
    {
        const float cs = 2f;
        var grid = new Dictionary<Vector2Int, List<Vector3>>();
        foreach (var p in pathPts)
        {
            var key = new Vector2Int(Mathf.FloorToInt(p.x / cs), Mathf.FloorToInt(p.z / cs));
            if (!grid.TryGetValue(key, out var l)) { l = new List<Vector3>(); grid[key] = l; }
            l.Add(p);
        }
        int rr = Mathf.CeilToInt(Radius / cs);
        var m = rock.sharedMesh; var v = m.vertices; var t = m.triangles; var tr = rock.transform;
        var res = new List<bool>();
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = tr.TransformPoint(v[t[i]]), b = tr.TransformPoint(v[t[i + 1]]), c = tr.TransformPoint(v[t[i + 2]]);
            var n = Vector3.Cross(b - a, c - a).normalized;
            bool ledge = false;
            if (n.y >= MinUpNormal)
            {
                var ce = (a + b + c) / 3f;
                var k0 = new Vector2Int(Mathf.FloorToInt(ce.x / cs), Mathf.FloorToInt(ce.z / cs));
                for (int dx = -rr; dx <= rr && !ledge; dx++)
                    for (int dz = -rr; dz <= rr && !ledge; dz++)
                    {
                        if (!grid.TryGetValue(k0 + new Vector2Int(dx, dz), out var l)) continue;
                        foreach (var p in l)
                        {
                            float hd = new Vector2(p.x - ce.x, p.z - ce.z).magnitude;
                            if (hd < Radius && p.y > ce.y + BelowPath) { ledge = true; break; }
                        }
                    }
            }
            res.Add(ledge);
        }
        return res;
    }

    public static string Apply(Transform course, string[] rockPaths, bool previewOnly)
    {
        var path = course.Find("PathMesh").GetComponent<MeshCollider>();
        var pts = PathPoints(path);
        string log = "";
        var prev = course.Find("LedgePreview");
        if (prev != null) Object.DestroyImmediate(prev.gameObject);
        foreach (var rp in rockPaths)
        {
            var go = course.Find(rp); if (go == null) { log += rp + " なし "; continue; }
            var mc = go.GetComponent<MeshCollider>();
            // 元の当たり判定（_NoLedge を差し替え済みなら元に戻して計算し直す）
            var src = mc.sharedMesh;
            if (src.name.EndsWith("_NoLedge"))
            {
                var orig = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GetAssetPath(src).Replace("_NoLedge.asset", ".asset"));
                if (orig != null) { mc.sharedMesh = orig; src = orig; }
            }
            var ledges = Ledges(mc, pts);
            var v = src.vertices; var t = src.triangles;
            var keep = new List<int>(); var drop = new List<int>();
            for (int i = 0; i < t.Length; i += 3) (ledges[i / 3] ? drop : keep).AddRange(new[] { t[i], t[i + 1], t[i + 2] });
            log += rp + " 三角形 " + t.Length / 3 + " → 抜く " + drop.Count / 3 + "  ";
            if (previewOnly)
            {
                var pmGo = new GameObject("LedgePreview_" + go.name, typeof(MeshFilter), typeof(MeshRenderer));
                if (prev == null) { prev = new GameObject("LedgePreview").transform; prev.SetParent(course, false); }
                pmGo.transform.SetParent(prev, false);
                pmGo.transform.SetPositionAndRotation(go.position, go.rotation); pmGo.transform.localScale = go.lossyScale;
                var pmM = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                pmM.SetVertices(v); pmM.SetTriangles(drop, 0); pmM.RecalculateNormals();
                pmGo.GetComponent<MeshFilter>().sharedMesh = pmM;
                var mat = new Material(Shader.Find("Unlit/Color")); mat.color = Color.red;
                pmGo.GetComponent<MeshRenderer>().sharedMaterial = mat;
                continue;
            }
            var nm = new Mesh { name = src.name + "_NoLedge", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            nm.SetVertices(v); nm.SetTriangles(keep, 0); nm.RecalculateNormals(); nm.RecalculateBounds();
            string p2 = AssetDatabase.GetAssetPath(src).Replace(".asset", "_NoLedge.asset");
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(p2);
            if (old != null) { old.Clear(); old.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; old.SetVertices(v); old.SetTriangles(keep, 0); old.RecalculateNormals(); old.RecalculateBounds(); EditorUtility.SetDirty(old); nm = old; }
            else AssetDatabase.CreateAsset(nm, p2);
            Undo.RecordObject(mc, "ledge");
            mc.sharedMesh = null; mc.sharedMesh = nm;
            EditorUtility.SetDirty(mc);
        }
        AssetDatabase.SaveAssets();
        return log;
    }
}
