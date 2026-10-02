using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 道や足場の「見た目用」メッシュの上の角を丸める（当たり判定は元のメッシュのまま使う）。
// 元のメッシュ：上面（法線が上向きの四角）＋側面（横向きの四角）＋底面、の箱を並べた形（コース1〜5の道）。
// やること：
//  1. 上面の四角のふち（外周・穴のまわり・一段下がる所）を見つける。となりに同じ高さの上面がある辺と、
//     となりがもっと高い（＝段の内側の角）辺は丸めない
//  2. ふちの頂点を内側へ r だけずらし、そのぶん側面の上端を r 下げる
//  3. 間を 1/4 円の帯でつなぐ（法線もなめらかに）
public static class FuwaBevelTools
{
    struct TopQuad { public int[] idx; public int sub; public float y; public Vector3 c; }

    static string KeyXZ(Vector3 p) { return Mathf.RoundToInt(p.x * 100) + "," + Mathf.RoundToInt(p.z * 100); }
    // 頂点の場所（xz＋高さ1cm単位）。同じ所でも高さが違う上面は別扱い
    static string VKey(Vector3 p) { return KeyXZ(p) + "@" + Mathf.RoundToInt(p.y * 100); }
    static string EdgeKey(Vector3 a, Vector3 b) { string x = KeyXZ(a), y = KeyXZ(b); return string.CompareOrdinal(x, y) < 0 ? x + "|" + y : y + "|" + x; }

    public static Mesh BevelTopEdges(Mesh src, float r, int steps, out string info)
    {
        var v = src.vertices; var n = src.normals; var uv = src.uv;
        if (uv.Length != v.Length) uv = new Vector2[v.Length];
        // ---- 三角形を2つずつ四角にまとめる ----
        var tops = new List<TopQuad>();
        var sides = new List<(int[] idx, int sub)>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var t = src.GetTriangles(s);
            for (int k = 0; k + 5 < t.Length; k += 6)
            {
                var nrm = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]).normalized;
                var ids = new List<int>();
                for (int j = 0; j < 6; j++) { int id = t[k + j]; bool dup = false; foreach (var e in ids) if ((v[e] - v[id]).sqrMagnitude < 1e-8f) dup = true; if (!dup) ids.Add(id); }
                if (ids.Count != 4) continue;
                var c = Vector3.zero; foreach (var id in ids) c += v[id]; c /= 4f;
                ids.Sort((a, b) => Mathf.Atan2(v[a].z - c.z, v[a].x - c.x).CompareTo(Mathf.Atan2(v[b].z - c.z, v[b].x - c.x)));
                if (nrm.y > 0.9f) tops.Add(new TopQuad { idx = ids.ToArray(), sub = s, y = c.y, c = c });
                else if (Mathf.Abs(nrm.y) < 0.2f) sides.Add((ids.ToArray(), s));
            }
        }
        // ---- 辺ごとに、同じ xz の辺を持つ上面を集める ----
        var edgeOwners = new Dictionary<string, List<(int q, float y)>>();
        for (int qi = 0; qi < tops.Count; qi++)
            for (int e = 0; e < 4; e++)
            {
                var a = v[tops[qi].idx[e]]; var b = v[tops[qi].idx[(e + 1) % 4]];
                string key = EdgeKey(a, b);
                if (!edgeOwners.TryGetValue(key, out var l)) edgeOwners[key] = l = new List<(int, float)>();
                l.Add((qi, (a.y + b.y) * 0.5f));
            }
        // ---- 丸めるふちを決める ----
        var bEdges = new List<(int q, int e, Vector3 no)>();
        var vertNormals = new Dictionary<string, List<Vector3>>();
        for (int qi = 0; qi < tops.Count; qi++)
            for (int e = 0; e < 4; e++)
            {
                var a = v[tops[qi].idx[e]]; var b = v[tops[qi].idx[(e + 1) % 4]];
                float y = (a.y + b.y) * 0.5f; bool rim = true;
                foreach (var o in edgeOwners[EdgeKey(a, b)])
                {
                    if (o.q == qi) continue;
                    if (o.y > y - 0.03f) { rim = false; break; }   // 同じ高さが続く or となりが高い（内側の角）→ 丸めない
                }
                if (!rim) continue;
                var dir = b - a; dir.y = 0; var no = new Vector3(dir.z, 0, -dir.x).normalized;
                var mid = (a + b) * 0.5f; if (Vector3.Dot(no, mid - tops[qi].c) < 0) no = -no;
                bEdges.Add((qi, e, no));
                foreach (var p in new[] { a, b }) { string k = VKey(p); if (!vertNormals.TryGetValue(k, out var l)) vertNormals[k] = l = new List<Vector3>(); l.Add(no); }
            }
        // 頂点ごとのずらし方（角は2本のふちの間＝マイター）
        var offs = new Dictionary<string, Vector3>();
        foreach (var kv in vertNormals)
        {
            var sum = Vector3.zero; foreach (var x in kv.Value) sum += x;
            var m = sum.sqrMagnitude > 1e-6f ? sum.normalized : kv.Value[0];
            float d = Vector3.Dot(m, kv.Value[0]); float len = r / Mathf.Max(0.35f, d);
            offs[kv.Key] = m * len;   // 外向き。内側へずらす時は -offs
        }

        var V = new List<Vector3>(v); var N = new List<Vector3>(n); var U = new List<Vector2>(uv);
        var T = new List<int>[src.subMeshCount]; for (int s = 0; s < src.subMeshCount; s++) T[s] = new List<int>(src.GetTriangles(s));
        // 上面の頂点を内側へ
        var moved = new HashSet<int>();
        foreach (var tq in tops) foreach (var id in tq.idx)
            {
                if (moved.Contains(id)) continue;
                if (offs.TryGetValue(VKey(v[id]), out var o)) { V[id] = v[id] - o; moved.Add(id); }
            }
        // 側面の上端を r 下げる（丸めるふちの真下の側面だけ）
        var rimEdges = new HashSet<string>(); var rimVerts = new HashSet<string>();
        foreach (var be in bEdges)
        {
            var tq = tops[be.q]; var a = v[tq.idx[be.e]]; var b = v[tq.idx[(be.e + 1) % 4]];
            rimEdges.Add(EdgeKey(a, b)); rimVerts.Add(VKey(a)); rimVerts.Add(VKey(b));
        }
        int loweredSides = 0;
        foreach (var sd in sides)
        {
            // 側面の四角の上側2点（高い順に2つ）
            var ids = new List<int>(sd.idx); ids.Sort((p, q) => v[q].y.CompareTo(v[p].y));
            var topIds = new List<int> { ids[0], ids[1] };
            if (!rimEdges.Contains(EdgeKey(v[topIds[0]], v[topIds[1]]))) continue;
            if (!rimVerts.Contains(VKey(v[topIds[0]])) || !rimVerts.Contains(VKey(v[topIds[1]]))) continue;
            foreach (var id in topIds) V[id] = new Vector3(v[id].x, v[id].y - r, v[id].z);
            loweredSides++;
        }
        // 1/4円の帯
        foreach (var be in bEdges)
        {
            var tq = tops[be.q]; var a = v[tq.idx[be.e]]; var b = v[tq.idx[(be.e + 1) % 4]];
            var oa = offs[VKey(a)]; var ob = offs[VKey(b)];
            var ma = oa.normalized; var mb = ob.normalized;
            int b0 = V.Count;
            for (int k = 0; k <= steps; k++)
            {
                float th = k * Mathf.PI * 0.5f / steps; float s = Mathf.Sin(th), c = Mathf.Cos(th);
                V.Add(a - oa * (1 - s) + Vector3.down * r * (1 - c)); N.Add((Vector3.up * c + ma * s).normalized); U.Add(uv[tq.idx[be.e]]);
                V.Add(b - ob * (1 - s) + Vector3.down * r * (1 - c)); N.Add((Vector3.up * c + mb * s).normalized); U.Add(uv[tq.idx[(be.e + 1) % 4]]);
            }
            for (int k = 0; k < steps; k++)
            {
                int p0 = b0 + k * 2, p1 = p0 + 1, p2 = p0 + 3, p3 = p0 + 2;
                var want = (N[p0] + N[p3]).normalized;
                var cr = Vector3.Cross(V[p1] - V[p0], V[p2] - V[p0]);
                if (Vector3.Dot(cr, want) >= 0) T[tq.sub].AddRange(new[] { p0, p1, p2, p0, p2, p3 });
                else T[tq.sub].AddRange(new[] { p0, p2, p1, p0, p3, p2 });
            }
        }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, U); mesh.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(T[s], s);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        info = "tops=" + tops.Count + " sides=" + sides.Count + " rimEdges=" + bEdges.Count + " loweredSides=" + loweredSides + " verts=" + V.Count;
        return mesh;
    }

    // PathMesh の見た目だけ丸めた版に差し替える（MeshCollider は元のメッシュのまま）
    public static string ApplyToPath(Transform pathMesh, float r, int steps)
    {
        var mf = pathMesh.GetComponent<MeshFilter>(); var mc = pathMesh.GetComponent<MeshCollider>();
        var src = mc != null && mc.sharedMesh != null ? mc.sharedMesh : mf.sharedMesh;
        if (mc != null && mc.sharedMesh == null) mc.sharedMesh = src;
        var mesh = BevelTopEdges(src, r, steps, out string info);
        mesh.name = src.name + "_Bevel";
        string dir = "Assets/FuwaCourse/Meshes/Bevel";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Bevel");
        string p = dir + "/" + mesh.name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old != null) { CopyInto(old, mesh); mesh = old; } else AssetDatabase.CreateAsset(mesh, p);
        Undo.RecordObject(mf, "bevel"); mf.sharedMesh = mesh;
        AssetDatabase.SaveAssets();
        return pathMesh.root.name + ": " + info;
    }

    // 既存のメッシュアセットの中身を入れ替える（CopySerialized だと描画用のデータが古いまま残ることがあったので、Clear して入れ直す）
    public static void CopyInto(Mesh dst, Mesh src)
    {
        dst.Clear();
        dst.indexFormat = src.indexFormat;
        dst.SetVertices(src.vertices); dst.SetNormals(src.normals); dst.SetUVs(0, src.uv);
        if (src.tangents.Length == src.vertexCount) dst.SetTangents(src.tangents);
        dst.subMeshCount = src.subMeshCount;
        for (int i = 0; i < src.subMeshCount; i++) dst.SetTriangles(src.GetTriangles(i), i);
        dst.RecalculateBounds();
        EditorUtility.SetDirty(dst);
    }

    // ---------- 箱・円柱・箱の寄せ集め（柵）を丸める ----------
    const string Dir = "Assets/FuwaCourse/Meshes/Bevel";

    static Mesh SaveAsset(Mesh mesh, string name)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Bevel");
        mesh.name = name; string p = Dir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old != null) { CopyInto(old, mesh); return old; }
        AssetDatabase.CreateAsset(mesh, p); return mesh;
    }

    // 角の丸い箱を、任意の向き（axes）・半分の大きさ（h）で作って頂点リストに足す。toLocal で最終の座標へ
    static void AddRoundBox(List<Vector3> V, List<Vector3> N, List<Vector2> U, List<int> T, Vector3 center, Vector3[] axes, Vector3 h, float r, int steps,
                            System.Func<Vector3, Vector3> toLocalP, System.Func<Vector3, Vector3> toLocalN)
    {
        r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.45f);
        float[] Coords(float hh)
        {
            var l = new List<float>();
            for (int k = 0; k <= steps; k++) l.Add(-hh + r * (1 - Mathf.Cos(k * Mathf.PI / 2 / steps)));
            for (int k = steps; k >= 0; k--) l.Add(hh - r * (1 - Mathf.Cos(k * Mathf.PI / 2 / steps)));
            return l.ToArray();
        }
        var cs = new[] { Coords(h.x), Coords(h.y), Coords(h.z) };
        for (int ax = 0; ax < 3; ax++)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3; var ca = cs[a1]; var cb = cs[a2]; int b0 = V.Count;
                for (int i = 0; i < ca.Length; i++)
                    for (int j = 0; j < cb.Length; j++)
                    {
                        var p = new Vector3(); p[ax] = sg * h[ax]; p[a1] = ca[i]; p[a2] = cb[j];
                        var q = new Vector3(Mathf.Clamp(p.x, -h.x + r, h.x - r), Mathf.Clamp(p.y, -h.y + r, h.y - r), Mathf.Clamp(p.z, -h.z + r, h.z - r));
                        var d = p - q; var fnL = new Vector3(); fnL[ax] = sg; var nl = d.sqrMagnitude > 1e-12f ? d.normalized : fnL;
                        var pl = q + nl * r;
                        var pw = center + axes[0] * pl.x + axes[1] * pl.y + axes[2] * pl.z;
                        var nw = (axes[0] * nl.x + axes[1] * nl.y + axes[2] * nl.z).normalized;
                        V.Add(toLocalP(pw)); N.Add(toLocalN(nw)); U.Add(Mathf.Abs(nw.y) > 0.7f ? new Vector2(pw.x, pw.z) : new Vector2(pw.x + pw.z, pw.y));
                    }
                var fn = toLocalN(axes[ax] * sg);
                for (int i = 0; i + 1 < ca.Length; i++)
                    for (int j = 0; j + 1 < cb.Length; j++)
                    {
                        int a = b0 + i * cb.Length + j, bb = a + 1, c = a + cb.Length + 1, d2 = a + cb.Length;
                        var cr = Vector3.Cross(V[bb] - V[a], V[c] - V[a]); if (cr.sqrMagnitude < 1e-14f) cr = Vector3.Cross(V[c] - V[a], V[d2] - V[a]);
                        if (Vector3.Dot(cr, fn) >= 0) T.AddRange(new[] { a, bb, c, a, c, d2 }); else T.AddRange(new[] { a, c, bb, a, d2, c });
                    }
            }
    }

    static Mesh Finish(List<Vector3> V, List<Vector3> N, List<Vector2> U, List<int> T)
    {
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(T, 0); m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    // Unityの Cube（-0.5〜0.5）を、物体のスケール込みの実寸で角を丸めた版
    public static Mesh RoundCube(Vector3 scale, float r, int steps)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
        AddRoundBox(V, N, U, T, Vector3.zero, new[] { Vector3.right, Vector3.up, Vector3.forward }, scale * 0.5f, r, steps,
            p => new Vector3(p.x / scale.x, p.y / scale.y, p.z / scale.z), n => new Vector3(n.x * scale.x, n.y * scale.y, n.z * scale.z).normalized);
        return SaveAsset(Finish(V, N, U, T), "BevelBox_" + scale.x.ToString("F2") + "x" + scale.y.ToString("F2") + "x" + scale.z.ToString("F2") + "_r" + r.ToString("F3"));
    }

    // Unityの Cylinder（半径0.5・高さ2）を、実寸でふちを丸めた版
    public static Mesh RoundCylinder(Vector3 scale, float r, int steps)
    {
        float R = 0.5f * scale.x, H = scale.y; r = Mathf.Min(r, Mathf.Min(R, H) * 0.95f);
        int seg = R > 1f ? 64 : 24;
        var prof = new List<Vector4> { new Vector4(0, H, 0, 1) };
        for (int k = 0; k <= steps; k++) { float a = Mathf.Lerp(90, 0, k / (float)steps) * Mathf.Deg2Rad; prof.Add(new Vector4(R - r + r * Mathf.Cos(a), H - r + r * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int k = 0; k <= steps; k++) { float a = Mathf.Lerp(0, -90, k / (float)steps) * Mathf.Deg2Rad; prof.Add(new Vector4(R - r + r * Mathf.Cos(a), -H + r + r * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(0, -H, 0, -1));
        var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
        int P = prof.Count;
        for (int s = 0; s <= seg; s++)
        {
            float th = s * Mathf.PI * 2 / seg; float c = Mathf.Cos(th), sn = Mathf.Sin(th);
            for (int i = 0; i < P; i++)
            {
                var p = prof[i]; var pw = new Vector3(p.x * c, p.y, p.x * sn); var nw = (i == 0 || i == P - 1) ? new Vector3(0, p.w, 0) : new Vector3(p.z * c, p.w, p.z * sn);
                V.Add(new Vector3(pw.x / scale.x, pw.y / scale.y, pw.z / scale.z)); N.Add(new Vector3(nw.x * scale.x, nw.y * scale.y, nw.z * scale.z).normalized); U.Add(new Vector2(pw.x, pw.z));
            }
        }
        for (int s = 0; s < seg; s++)
            for (int i = 0; i + 1 < P; i++)
            {
                int a = s * P + i, b = a + 1, c = a + P + 1, d = a + P;
                var want = (N[a] + N[c]).normalized; var cr = Vector3.Cross(V[b] - V[a], V[c] - V[a]); if (cr.sqrMagnitude < 1e-14f) cr = Vector3.Cross(V[c] - V[a], V[d] - V[a]);
                if (Vector3.Dot(cr, want) >= 0) T.AddRange(new[] { a, b, c, a, c, d }); else T.AddRange(new[] { a, c, b, a, d, c });
            }
        return SaveAsset(Finish(V, N, U, T), "BevelCyl_" + scale.x.ToString("F2") + "x" + scale.y.ToString("F2") + "x" + scale.z.ToString("F2") + "_r" + r.ToString("F3"));
    }

    // 箱（頂点24個ずつ）を寄せ集めたメッシュ（柵など）の、箱ひとつひとつを丸める
    public static Mesh RoundBoxGroup(Mesh src, float rMax, int steps)
    {
        var v = src.vertices; var n = src.normals;
        var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
        for (int b = 0; b + 24 <= v.Length; b += 24)
        {
            var center = Vector3.zero; for (int i = 0; i < 24; i++) center += v[b + i]; center /= 24f;
            var axes = new List<Vector3>();
            for (int i = 0; i < 24 && axes.Count < 3; i++) { var nn = n[b + i].normalized; bool ok = true; foreach (var a in axes) if (Mathf.Abs(Vector3.Dot(a, nn)) > 0.5f) ok = false; if (ok) axes.Add(nn); }
            if (axes.Count < 3) axes.Add(Vector3.Cross(axes[0], axes[1]).normalized);
            var h = new Vector3();
            for (int k = 0; k < 3; k++) { float mx = 0; for (int i = 0; i < 24; i++) mx = Mathf.Max(mx, Mathf.Abs(Vector3.Dot(v[b + i] - center, axes[k]))); h[k] = mx; }
            AddRoundBox(V, N, U, T, center, axes.ToArray(), h, rMax, steps, p => p, nn => nn);
        }
        return SaveAsset(Finish(V, N, U, T), src.name + "_Bevel");
    }

    // コース（ルート）の中の、角ばった物の見た目を丸める（当たり判定はそのまま）
    public static string ApplyToProps(Transform root)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh;
            if (mr == null || m == null) continue;
            string nm = mf.name; var s = mf.transform.lossyScale; s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            Mesh nmesh = null;
            if (m.name == "Cylinder" && (nm == "Ground" || nm == "WarpGround" || nm == "GoalGround")) nmesh = RoundCylinder(s, 0.07f, 4);
            else if (m.name == "Cylinder" && nm.StartsWith("GunStand")) nmesh = RoundCylinder(s, 0.03f, 4);
            else if (m.name == "Cube" && (nm == "Bridge" || nm == "Post_L" || nm == "Post_R")) nmesh = RoundCube(s, 0.04f, 4);
            else if (m.name == "Cube" && nm == "Bar") nmesh = RoundCube(s, 0.06f, 4);
            else if (m.name == "Cube" && nm == "PadBase") nmesh = RoundCube(s, 0.025f, 4);
            else if (nm == "Fence" && !m.name.EndsWith("_Bevel") && m.vertexCount % 24 == 0) nmesh = RoundBoxGroup(m, 0.025f, 2);
            if (nmesh == null) continue;
            var mc = mf.GetComponent<MeshCollider>(); if (mc != null && mc.sharedMesh == null) mc.sharedMesh = m;
            Undo.RecordObject(mf, "bevel"); mf.sharedMesh = nmesh;
            sb.Append(nm + " ");
        }
        AssetDatabase.SaveAssets();
        return root.name + ": " + sb;
    }
}
