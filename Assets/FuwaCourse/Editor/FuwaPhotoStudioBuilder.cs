using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 写真館の小島（2026-10-11 てじゃ「ステージ2にするっていった場所から橋伸ばして小島作ろう、ステージ2のポータルもそこに」）。
// ひとりでロビーの空いたうずまきの場所（遊び方の看板と最速タイムの看板の間、もと Stage2Portal を仮置きした所）から外へ：
// ・ロビーの柵に入口を開ける（柱1本と横木を抜いて、柱の間2.0m。スタートの島と橋の作りと同じ）。元の柵のメッシュは触らず、開けた版を作る
// ・かぜのおかのスタートの後ろの木の橋をコピーしてかける（床2.24m）
// ・島：スタートの島と同じ地面（直径8m）と島の下をコピー。まわりに柵（ロビーと同じ柱＋横木2段）、橋の所だけ開ける
// ・名場面アルバムのボード（FuwaPhotoAlbumBuilder が BoardSpot に置く）と、ステージ2のポータル（今はまだ非表示のまま）
// 何度実行してもよい（島はまるごと作り直す。柵は元のメッシュから開け直す）
public static class FuwaPhotoStudioBuilder
{
    const string MeshDir = "Assets/FuwaCourse/Meshes";
    // ロビーの中心から見た向き（度、+x から反時計回り）。もと Stage2Portal の位置 (6.5, -14.5) の方
    const float AngleDeg = 167.6f;
    const float LobbyFloorR = 8.0f;
    const float FenceR = 8.1f;          // ロビーの柵の半径
    const float GapHalfDeg = 5.5f;      // 柵を開ける範囲（柱1本＋両側の横木）
    static float IslandR = 3.0f;        // 島の地面の半径（スタートの島と同じ。Run でスタートの地面から測り直す）
    static float BridgeGap = 1.15f;     // 島どうしのふちの間（スタートの島→ワープの小島と同じ。Run で測り直す）

    [MenuItem("FuwaCourse/写真館の島を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var lobby = roots.First(g => g.name == "FuwaLobby").transform;
        var c1 = roots.First(g => g.name == "FuwaCourse").transform;
        var c1Start = c1.Find("Start");
        var c1Area = c1Start.Find("StartArea");
        {
            var gT = c1Area.Find("Ground");
            IslandR = gT.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x * Mathf.Abs(gT.lossyScale.x);
        }

        float a = AngleDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float floorTop = lobby.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        Vector3 lc = new Vector3(lobby.position.x, floorTop, lobby.position.z);

        log.Add(OpenLobbyFence(lobby, dir));

        // 島（まるごと作り直す）
        var old = lobby.Find("PhotoStudio");
        Transform keepPortal = null;
        if (old != null)
        {
            // ステージ2のポータルは作り直さない（中身はロビーにあった物そのまま）
            keepPortal = old.Find("Stage2Portal");
            if (keepPortal != null) keepPortal.SetParent(lobby, true);
            Object.DestroyImmediate(old.gameObject);
        }
        var studio = new GameObject("PhotoStudio").transform;
        studio.SetParent(lobby, false);
        Vector3 center = lc + dir * (LobbyFloorR + BridgeGap + IslandR);
        studio.position = center;
        studio.rotation = Quaternion.LookRotation(dir, Vector3.up);   // +z＝ロビーから外へ

        // かぜのおかのスタートの島での位置関係をそのまま使う：スタートの地面の中心 G1、ワープの小島の向き d1
        var g1T = c1Area.Find("Ground");
        Vector3 g1 = g1T.position;
        float g1Top = g1T.GetComponent<Renderer>().bounds.max.y;
        Vector3 d1 = c1Area.Find("WarpGround").position - g1; d1.y = 0f; d1.Normalize();
        // スタートの島のふち（ワープ側）→ ロビーのふち（島側）に重ねる
        Vector3 from = new Vector3(g1.x, g1Top, g1.z) + d1 * IslandR;
        Vector3 to = lc + dir * LobbyFloorR;
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Inverse(Quaternion.LookRotation(d1));
        // ワープの小島の半径も測って、島どうしのふちの間を合わせる
        var wT = c1Area.Find("WarpGround");
        float warpR = wT.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x * Mathf.Abs(wT.lossyScale.x);
        BridgeGap = (c1Area.Find("WarpGround").position - g1T.position).magnitude - IslandR - warpR;
        center = lc + dir * (LobbyFloorR + BridgeGap + IslandR);
        studio.position = center;
        Vector3 Map(Vector3 p) { return to + rot * (p - from); }
        foreach (var n in new[] { "Bridge", "BridgeWood" })
        {
            var src = c1Area.Find(n);
            var c = Dup(src, studio, n);
            c.transform.SetPositionAndRotation(Map(src.position), rot * src.rotation);
        }
        // 島の地面と島の下（スタートの島のもの）。地面の中心＝島の中心
        foreach (var n in new[] { "Ground", "IslandUnder" })
        {
            var src = c1Area.Find(n);
            var c = Dup(src, studio, n);
            Vector3 off = src.position - new Vector3(g1.x, g1Top, g1.z);
            c.transform.SetPositionAndRotation(center + rot * off, rot * src.rotation);
        }

        // 島の柵（橋の所だけ開ける）
        log.Add(StudioFence(studio, center, -dir));

        // ボードを置く所（島の奥、ロビーの方を向く）と、ステージ2のポータル（島の左手前）
        var spot = new GameObject("BoardSpot").transform;
        spot.SetParent(studio, false);
        spot.position = center + dir * 1.5f - side * 0.7f;   // ポータルの札とかぶらないよう少し左へ
        spot.rotation = Quaternion.LookRotation(dir, Vector3.up);   // ボードは +z を奥にして置く（見る人はロビー側）
        var portal = keepPortal != null ? keepPortal : lobby.Find("Stage2Portal");
        if (portal != null)
        {
            portal.SetParent(studio, true);
            portal.position = center - dir * 0.4f + side * 1.9f;
            portal.rotation = Quaternion.LookRotation(-dir, Vector3.up);   // 札（Label）が橋の方から読める向き（札は台の奥側に立つ）
            log.Add("stage2 portal moved (active=" + portal.gameObject.activeSelf + ")");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        log.Add("studio at " + center.ToString("F2"));
        return string.Join("\n", log);
    }

    static GameObject Dup(Transform src, Transform parent, string name)
    {
        var c = Object.Instantiate(src.gameObject, parent);
        c.name = name;
        c.SetActive(true);
        return c;
    }

    // ロビーの柵：元のメッシュから、入口の範囲（角度）にある部品（柱・横木）を抜いた版を作る。当たり判定は三角形ごとに
    static string OpenLobbyFence(Transform lobby, Vector3 dir)
    {
        var fence = lobby.Find("Fence");
        var fcol = lobby.Find("FenceCollider");
        var srcVis = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Bevel/FuwaLobby_Fence_Bevel.asset");
        var srcCol = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/FuwaLobby_FenceCollider.asset");
        float gapA = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        bool InGap(Vector3 local)
        {
            float ang = Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(ang, gapA)) < GapHalfDeg;
        }
        // 見た目：つながった部品ごとに中心の角度で判定
        var v = srcVis.vertices; var t = srcVis.triangles;
        int n = v.Length;
        var par = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        void Union(int p, int q) { p = Find(p); q = Find(q); if (p != q) par[p] = q; }
        var at = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < n; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000), Mathf.RoundToInt(v[i].y * 1000), Mathf.RoundToInt(v[i].z * 1000));
            if (at.TryGetValue(k, out int j)) Union(i, j); else at[k] = i;
        }
        for (int i = 0; i < t.Length; i += 3) { Union(t[i], t[i + 1]); Union(t[i], t[i + 2]); }
        var sum = new Dictionary<int, Vector3>(); var cnt = new Dictionary<int, int>();
        for (int i = 0; i < n; i++) { int r = Find(i); sum[r] = (sum.TryGetValue(r, out var s0) ? s0 : Vector3.zero) + v[i]; cnt[r] = (cnt.TryGetValue(r, out var c0) ? c0 : 0) + 1; }
        var removed = new HashSet<int>(sum.Keys.Where(r => InGap(sum[r] / cnt[r])));
        var keep = new List<int>();
        for (int i = 0; i < t.Length; i += 3) if (!removed.Contains(Find(t[i]))) keep.AddRange(new[] { t[i], t[i + 1], t[i + 2] });
        var vis = Object.Instantiate(srcVis); vis.name = "FuwaLobby_Fence_Open"; vis.SetTriangles(keep, 0); vis.RecalculateBounds();

        var cv = srcCol.vertices; var ct = srcCol.triangles; var keepC = new List<int>();
        for (int i = 0; i < ct.Length; i += 3)
        {
            var cen = (cv[ct[i]] + cv[ct[i + 1]] + cv[ct[i + 2]]) / 3f;
            if (!InGap(cen)) keepC.AddRange(new[] { ct[i], ct[i + 1], ct[i + 2] });
        }
        var col = Object.Instantiate(srcCol); col.name = "FuwaLobby_FenceCollider_Open"; col.SetTriangles(keepC, 0); col.RecalculateBounds();

        vis = Save(vis, MeshDir + "/FuwaLobby_Fence_Open.asset");
        col = Save(col, MeshDir + "/FuwaLobby_FenceCollider_Open.asset");
        fence.GetComponent<MeshFilter>().sharedMesh = vis;
        var farT = fence.Find("Fence_Far");
        if (farT != null)
        {
            var ls = fence.lossyScale; float cell = FuwaFarViewSetup.FarCell;
            var far = FuwaFarViewSetup.Simplify(vis, new Vector3(cell / Mathf.Abs(ls.x), cell / Mathf.Abs(ls.y), cell / Mathf.Abs(ls.z)));
            far.name = "FuwaLobby_Fence_Open_Far";
            far = Save(far, MeshDir + "/FuwaLobby_Fence_Open_Far.asset");
            farT.GetComponent<MeshFilter>().sharedMesh = far;
        }
        var mc = fcol.GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = col;
        EditorUtility.SetDirty(fence.gameObject); EditorUtility.SetDirty(fcol.gameObject);
        return $"lobby fence parts -{removed.Count}, collider tris {ct.Length / 3}->{keepC.Count / 3}";
    }

    // 島の柵：ロビーの柵と同じ寸法（柱 0.11角×0.6、約1mおき、横木2段）。toLobby の向きに 2.0m の入口
    static string StudioFence(Transform studio, Vector3 center, Vector3 toLobby)
    {
        float r = IslandR + 0.1f;
        const float postW = 0.11f, postH = 0.6f, railW = 0.06f, railH = 0.06f;
        float baseY = -0.02f;                       // 柱の根元（地面の上から）
        float[] railY = { 0.22f, 0.52f };           // 横木の中心（柱の根元から）
        float gapA = Mathf.Atan2(toLobby.z, toLobby.x);
        float gapHalf = 1.0f / r;                    // 入口の柱の間 2.0m
        int segs = Mathf.RoundToInt((Mathf.PI * 2f * r - 2.0f) / 1.0f);
        float span = Mathf.PI * 2f - gapHalf * 2f;
        var postMesh = FuwaBevelTools.RoundCube(new Vector3(postW, postH, postW), 0.03f, 2);
        var parts = new List<CombineInstance>();
        var colParts = new List<CombineInstance>();
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Vector3 P(float ang) { return new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r); }
        Vector3 cL = studio.InverseTransformPoint(center);
        Quaternion inv = Quaternion.Inverse(studio.rotation);
        for (int i = 0; i <= segs; i++)
        {
            float ang = gapA + gapHalf + span * i / segs;
            Vector3 p = cL + inv * P(ang);
            parts.Add(new CombineInstance { mesh = postMesh, transform = Matrix4x4.TRS(p + Vector3.up * (baseY + postH * 0.5f), Quaternion.identity, new Vector3(postW, postH, postW)) });
            if (i == segs) break;
            float ang2 = gapA + gapHalf + span * (i + 1) / segs;
            Vector3 q = cL + inv * P(ang2);
            Vector3 mid = (p + q) * 0.5f; Vector3 d = q - p; float len = d.magnitude;
            Quaternion rr = Quaternion.LookRotation(d / len, Vector3.up);
            var railMesh = FuwaBevelTools.RoundCube(new Vector3(railW, railH, len + 0.04f), 0.02f, 2);
            foreach (float ry in railY)
                parts.Add(new CombineInstance { mesh = railMesh, transform = Matrix4x4.TRS(mid + Vector3.up * (baseY + ry), rr, new Vector3(railW, railH, len + 0.04f)) });
            // 当たり判定：横木の区間ごとに、高さ0.65の板
            colParts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(mid + Vector3.up * (baseY + 0.33f), rr, new Vector3(0.12f, 0.65f, len)) });
        }
        var vis = new Mesh { name = "PhotoStudio_Fence", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        vis.CombineMeshes(parts.ToArray(), true, true);
        vis.RecalculateBounds();
        var col = new Mesh { name = "PhotoStudio_FenceCollider" };
        col.CombineMeshes(colParts.ToArray(), true, true);
        vis = Save(vis, MeshDir + "/PhotoStudio_Fence.asset");
        col = Save(col, MeshDir + "/PhotoStudio_FenceCollider.asset");

        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat");
        var f = new GameObject("Fence");
        f.transform.SetParent(studio, false);
        f.AddComponent<MeshFilter>().sharedMesh = vis;
        var mr = f.AddComponent<MeshRenderer>(); mr.sharedMaterial = wood;
        GameObjectUtility.SetStaticEditorFlags(f, (StaticEditorFlags)~0);
        var fc = new GameObject("FenceCollider");
        fc.transform.SetParent(studio, false);
        fc.AddComponent<MeshCollider>().sharedMesh = col;
        GameObjectUtility.SetStaticEditorFlags(fc, (StaticEditorFlags)~0);
        return "studio fence posts " + (segs + 1);
    }

    static Mesh Save(Mesh m, string path)
    {
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) { AssetDatabase.CreateAsset(m, path); return m; }
        ex.Clear(); EditorUtility.CopySerialized(m, ex); ex.name = m.name; return ex;
    }
}
