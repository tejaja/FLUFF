using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// コースを目線の高さで撮って回る（見た目の点検用。シーンは変えない）。2026-11-24 てじゃ「コース全体的にモデルのトポロジを見直したい」→「まず1やろう」
//  歩ける所（コースの当たりの上面）を 1m おきに打って、となり（2.6m 以内・高さの差 1.2m 以内）をつないだ道の網を作り、
//  スタート → チェックポイント（スタートから近い順）→ ゴール を最短でたどる。その線を step m おきに、目線 1.6m・少し下向きで撮る。
//  6 枚ずつ 1 枚の画像（3×2）にまとめて outDir に書き出す。つながっていない所（ジャンプ・ワープ）は直線でつなぐ
public static class FuwaCourseTour
{
    public static string Run(Transform root, string outDir, float step = 5f, string tag = null)
    {
        tag = tag ?? root.name;
        Physics.SyncTransforms();
        // 1) 歩ける点
        var rs = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        const float G = 1f;
        var pts = new List<Vector3>(); var cell = new Dictionary<Vector2Int, List<int>>();
        for (float x = b.min.x; x <= b.max.x; x += G)
            for (float z = b.min.z; z <= b.max.z; z += G)
            {
                var hs = Physics.RaycastAll(new Vector3(x, b.max.y + 5f, z), Vector3.down, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hs)
                {
                    if (!h.collider.transform.IsChildOf(root) || h.normal.y < 0.6f) continue;
                    // 上に 1.2m あいている（人が立てる）
                    if (Physics.Raycast(h.point + Vector3.up * 0.05f, Vector3.up, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    int id = pts.Count; pts.Add(h.point);
                    var k = new Vector2Int(Mathf.FloorToInt(x / 3f), Mathf.FloorToInt(z / 3f)); if (!cell.TryGetValue(k, out var l)) cell[k] = l = new List<int>(); l.Add(id);
                }
            }
        if (pts.Count == 0) return tag + "：歩ける所がない";
        // 2) 網
        var nb = new List<int>[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            nb[i] = new List<int>(); var p = pts[i]; var k = new Vector2Int(Mathf.FloorToInt(p.x / 3f), Mathf.FloorToInt(p.z / 3f));
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                if (!cell.TryGetValue(k + new Vector2Int(dx, dz), out var l)) continue;
                foreach (var j in l) { if (j == i) continue; var q = pts[j]; if (new Vector2(q.x - p.x, q.z - p.z).magnitude <= 2.6f && Mathf.Abs(q.y - p.y) <= 1.2f) nb[i].Add(j); }
            }
        }
        int Near(Vector3 w) { int best = 0; float bd = float.MaxValue; for (int i = 0; i < pts.Count; i++) { float d = (pts[i] - w).sqrMagnitude; if (d < bd) { bd = d; best = i; } } return best; }
        float[] Dij(int s, out int[] prev)
        {
            var dist = Enumerable.Repeat(float.MaxValue, pts.Count).ToArray(); prev = Enumerable.Repeat(-1, pts.Count).ToArray(); dist[s] = 0;
            var pq = new SortedSet<(float, int)> { (0f, s) };
            while (pq.Count > 0)
            {
                var (d, u) = pq.Min; pq.Remove(pq.Min); if (d > dist[u]) continue;
                foreach (var v in nb[u]) { float nd = d + (pts[v] - pts[u]).magnitude; if (nd < dist[v]) { if (dist[v] < float.MaxValue) pq.Remove((dist[v], v)); dist[v] = nd; prev[v] = u; pq.Add((nd, v)); } }
            }
            return dist;
        }
        // 3) 立ち寄る所：スタート、チェックポイント、ゴール
        Transform F(string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
        var start = F("PlayerSpot") ?? F("BallStart"); var goal = F("GoalTrigger") ?? F("ReturnPoint");
        var stops = new List<Vector3> { start ? start.position : pts[0] };
        var cps = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "RespawnPoint").Select(t => t.position).ToList();
        int s0 = Near(stops[0]); var d0 = Dij(s0, out _);
        // 区間（Run0, Run1…）があるコース（2–5）はその順。区間の頭からつぎの区間の頭へ
        var runs = root.GetComponentsInChildren<Transform>(true).Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^Run\d+$")).OrderBy(t => int.Parse(t.name.Substring(3))).ToList();
        if (runs.Count > 0) stops.AddRange(runs.Select(t => t.position));
        else stops.AddRange(cps.OrderBy(c => { var n = Near(c); return d0[n] < float.MaxValue ? d0[n] : 1e6f + (c - stops[0]).magnitude; }));
        if (goal) stops.Add(goal.position);
        // 4) たどる線
        var route = new List<Vector3>();
        for (int i = 0; i + 1 < stops.Count; i++)
        {
            int a = Near(stops[i]), c = Near(stops[i + 1]);
            Dij(a, out var prev); var seg = new List<Vector3>();
            if (prev[c] >= 0 || a == c) { for (int u = c; u >= 0; u = prev[u]) { seg.Add(pts[u]); if (u == a) break; } seg.Reverse(); }
            else seg = new List<Vector3> { pts[a], pts[c] };
            route.AddRange(seg);
        }
        // 直線でつないだ所（ジャンプ・ワープ）も撮れるよう、0.5m おきに点を足す
        { var dense = new List<Vector3>(); for (int i = 0; i < route.Count; i++) { if (i > 0) { var a = route[i - 1]; var c = route[i]; int n = Mathf.FloorToInt((c - a).magnitude / 0.5f); for (int k = 1; k < n; k++) dense.Add(Vector3.Lerp(a, c, k / (float)n)); } dense.Add(route[i]); } route = dense; }
        // 5) step m おきに撮る
        var shots = new List<(Vector3 pos, Vector3 fwd)>(); float acc = step;
        for (int i = 1; i < route.Count; i++)
        {
            float l = (route[i] - route[i - 1]).magnitude; acc += l;
            if (acc < step) continue; acc = 0f;
            var ahead = route[Mathf.Min(route.Count - 1, i + 4)] - route[Mathf.Max(0, i - 1)]; ahead.y = 0; if (ahead.sqrMagnitude < 1e-4f) continue;
            shots.Add((route[i] + Vector3.up * 1.6f, ahead.normalized));
        }
        Directory.CreateDirectory(outDir);
        const int W = 480, H = 300, C = 3, R = 2;
        var go = new GameObject("TourCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 70; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600;
        var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
        var sheet = new Texture2D(W * C, H * R, TextureFormat.RGB24, false); var tile = new Texture2D(W, H, TextureFormat.RGB24, false);
        int sheets = 0;
        for (int i = 0; i < shots.Count; i++)
        {
            cam.transform.position = shots[i].pos; cam.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(12f, Vector3.Cross(Vector3.up, shots[i].fwd)) * shots[i].fwd, Vector3.up);
            cam.Render(); RenderTexture.active = rt; tile.ReadPixels(new Rect(0, 0, W, H), 0, 0); tile.Apply();
            int slot = i % (C * R); int cx = slot % C, cy = R - 1 - slot / C;
            sheet.SetPixels(cx * W, cy * H, W, H, tile.GetPixels());
            if (slot == C * R - 1 || i == shots.Count - 1)
            {
                if (slot != C * R - 1) for (int s = slot + 1; s < C * R; s++) { int ex = s % C, ey = R - 1 - s / C; sheet.SetPixels(ex * W, ey * H, W, H, Enumerable.Repeat(Color.black, W * H).ToArray()); }
                sheet.Apply(); File.WriteAllBytes(Path.Combine(outDir, tag + "_" + sheets.ToString("00") + ".png"), sheet.EncodeToPNG()); sheets++;
            }
        }
        RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(go); Object.DestroyImmediate(rt);
        // 撮った場所（コースの根っこから見た座標）
        var log = string.Join("\n", shots.Select((s, i) => i.ToString("00") + " " + root.InverseTransformPoint(s.pos).ToString("F1")));
        File.WriteAllText(Path.Combine(outDir, tag + "_shots.txt"), log);
        return tag + "：歩ける点 " + pts.Count + "・立ち寄り " + stops.Count + "・撮影 " + shots.Count + " 枚・画像 " + sheets + " 枚";
    }
}
