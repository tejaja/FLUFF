using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 写真館の小島（2026-10-11 てじゃ「ステージ2にするっていった場所から橋伸ばして小島作ろう、ステージ2のポータルもそこに」）。
// ひとりでロビーの空いたうずまきの場所（遊び方の看板と最速タイムの看板の間、もと Stage2Portal を仮置きした所）から外へ：
// ・ロビーの柵に入口を開ける（柱1本と横木を抜いて、柱の間2.0m。スタートの島と橋の作りと同じ）。元の柵のメッシュは触らず、開けた版を作る
// ・かぜのおかのスタートの後ろの木の橋をコピーしてかける（床2.24m）
// ・島：スタートの島と同じ地面（直径8m）と島の下をコピー。まわりに柵（ロビーと同じ柱＋横木2段）、橋の所だけ開ける
// ・奥の正面に「名場面」ボード（FuwaPhotoAlbumBuilder が BoardSpot に置く）、左の横に「最速タイム」の看板（ロビーから引っ越し）、右の横にCAMボタン。
//   ステージ2のポータル（今はまだ非表示のまま）は、最速タイムの看板があったロビーの場所へ（てじゃ）
//   島はスタートの島の 1.35 倍（看板2枚が入るように）
// 何度実行してもよい（島はまるごと作り直す。柵は元のメッシュから開け直す）
public static class FuwaPhotoStudioBuilder
{
    const string MeshDir = "Assets/FuwaCourse/Meshes";
    // ロビーの中心から見た向き（度、+x から反時計回り）。もと Stage2Portal の位置 (6.5, -14.5) の方
    const float AngleDeg = 167.6f;
    const float LobbyFloorR = 8.0f;
    const float FenceR = 8.1f;          // ロビーの柵の半径
    const float GapHalfDeg = 5.5f;      // 柵を開ける範囲（柱1本＋両側の横木）
    const float IslandScale = 1.35f;    // スタートの島に対する大きさ
    const float BoardRingR = 2.75f;     // 看板を置く、島の中心からの距離
    const float BoardAngle = 40f;       // 看板2枚の、奥の真ん中からの角度（左右）
    static readonly Vector3 BestTimesLobbyPos = new Vector3(5.83f, 8.29f, -17.63f);   // 最速タイムの看板がロビーにあった場所
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
            // 看板・ポータルは作り直さない（ロビーにあった物そのまま）。島を消す前にロビーへ戻しておく
            keepPortal = old.Find("Stage2Portal");
            if (keepPortal != null) keepPortal.SetParent(lobby, true);
            var oldBest = old.Find("BestTimes");
            if (oldBest != null) oldBest.SetParent(lobby, true);
            var oldBoard = old.Find("PhotoGallery");
            if (oldBoard != null) oldBoard.SetParent(lobby, true);
            var oldCam = old.Find("CamButton");
            if (oldCam != null) oldCam.SetParent(lobby, true);
            Object.DestroyImmediate(old.gameObject);
        }
        var studio = new GameObject("PhotoStudio").transform;
        studio.SetParent(lobby, false);
        float srcIslandR = IslandR;
        IslandR = srcIslandR * IslandScale;
        Vector3 center = lc + dir * (LobbyFloorR + BridgeGap + IslandR);
        studio.position = center;
        studio.rotation = Quaternion.LookRotation(dir, Vector3.up);   // +z＝ロビーから外へ

        // かぜのおかのスタートの島での位置関係をそのまま使う：スタートの地面の中心 G1、ワープの小島の向き d1
        var g1T = c1Area.Find("Ground");
        Vector3 g1 = g1T.position;
        float g1Top = g1T.GetComponent<Renderer>().bounds.max.y;
        Vector3 d1 = c1Area.Find("WarpGround").position - g1; d1.y = 0f; d1.Normalize();
        // スタートの島のふち（ワープ側）→ ロビーのふち（島側）に重ねる
        Vector3 from = new Vector3(g1.x, g1Top, g1.z) + d1 * srcIslandR;
        Vector3 to = lc + dir * LobbyFloorR;
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Inverse(Quaternion.LookRotation(d1));
        // ワープの小島の半径も測って、島どうしのふちの間を合わせる
        var wT = c1Area.Find("WarpGround");
        float warpR = wT.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x * Mathf.Abs(wT.lossyScale.x);
        BridgeGap = (c1Area.Find("WarpGround").position - g1T.position).magnitude - srcIslandR - warpR;
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
            var ls = c.transform.localScale; c.transform.localScale = new Vector3(ls.x * IslandScale, ls.y, ls.z * IslandScale);
        }

        // 島の柵（橋の所だけ開ける）
        log.Add(StudioFence(studio, center, -dir));

        // 奥に看板2枚をハの字に（どちらも島の中心の方を向く。+z が外向き＝見る人は内側）
        Transform RingSpot(string name, float deg)
        {
            var t = new GameObject(name).transform;
            t.SetParent(studio, false);
            Vector3 d = Quaternion.Euler(0f, deg, 0f) * dir;
            t.position = center + d * BoardRingR;
            t.rotation = Quaternion.LookRotation(d, Vector3.up);
            return t;
        }
        var bs = RingSpot("BoardSpot", BoardAngle);   // 名場面（FuwaPhotoAlbumBuilder が置く）
        var bestSpot = RingSpot("BestTimesSpot", -BoardAngle);
        // 2026-10-11 てじゃ「名場面を大きくして、島から入って真正面にドン。左側にスコアボード（縮めてもOK）右側にCAM」
        // 名場面：奥の正面（1.15倍は FuwaPhotoAlbumBuilder 側）。外のはしは柵（半径4.08）の内側
        bs.localRotation = Quaternion.identity; bs.localPosition = new Vector3(0f, 0f, 2.7f);
        // 最速タイム：左の横、0.75倍。柵に沿う向き（島の中心から見た方向 -68°、半径3.55、真ん中を向く）
        // （てじゃ「右と左は柵にあわせて角度つけて」「もうちょい名場面に寄せる、柵に寄せて角度もずらす」）
        { float ba = -68f * Mathf.Deg2Rad; bestSpot.localPosition = new Vector3(Mathf.Sin(ba), 0f, Mathf.Cos(ba)) * 3.55f; bestSpot.localRotation = Quaternion.Euler(0f, -68f, 0f); }
        var best = lobby.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BestTimes" && t.GetComponentInChildren<TMPro.TextMeshPro>(true) != null);
        if (best != null)
        {
            best.SetParent(studio, true);
            best.SetPositionAndRotation(new Vector3(bestSpot.position.x, BestTimesLobbyPos.y - 8.1f + floorTop, bestSpot.position.z), bestSpot.rotation);
            best.localScale = Vector3.one * 0.75f;
            log.Add("best times moved");
        }

        // CAMボタン（追っかけカメラの切り替え）：最速タイムの看板の子だったのを、名場面ボードの右に独立させて、下に説明の札（てじゃ）
        var cam = lobby.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CamButton" && t.GetComponentInChildren<Canvas>(true) != null && t.parent != null && (t.parent.name == "BestTimes" || t.parent == lobby));
        var boardSpot = studio.Find("BoardSpot");
        if (cam != null && boardSpot != null)
        {
            cam.SetParent(studio, true);
            // 2026-10-11 てじゃがシーンで動かした位置（島の右手、真ん中向き。説明の札はボタンの上）
            // → 名場面に寄せて柵ぎわへ：島の中心から見た方向 62°、半径3.6、柵に沿う向き
            { const float camDeg = 62f; float ca = camDeg * Mathf.Deg2Rad; cam.localPosition = new Vector3(Mathf.Sin(ca) * 3.6f, 1.033f, Mathf.Cos(ca) * 3.6f); cam.localRotation = Quaternion.Euler(0f, camDeg, 0f); }
            log.Add(CamInfo(studio, cam, boardSpot));
        }

        // ステージ2のポータルは、最速タイムの看板があったロビーの場所へ（柵にかからないよう少し内側）。札がロビーの真ん中から読める向き
        var portal = keepPortal != null ? keepPortal : lobby.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Stage2Portal");
        if (portal != null)
        {
            portal.SetParent(lobby, true);
            Vector3 o = BestTimesLobbyPos - lc; o.y = 0f; o.Normalize();
            portal.position = lc + o * 6.9f;
            portal.rotation = Quaternion.LookRotation(-o, Vector3.up);
            log.Add("stage2 portal -> lobby (active=" + portal.gameObject.activeSelf + ")");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        log.Add("studio at " + center.ToString("F2"));
        return string.Join("\n", log);
    }

    // CAMボタンの説明の札（角丸の板＋文字）。言語ボタンで切り替わるように FuwaLanguage に登録
    static string CamInfo(Transform studio, Transform cam, Transform boardSpot)
    {
        // モード名はボタンと同じ英語のまま並べるだけ（てじゃ：ボタンは英語固定なので日本語版もここは英語に）
        const string modes = "BACK / SIDE R / SIDE L / TOP / OFF";
        // 見出しは「あそびかた」の看板と同じオレンジ・1.5倍（てじゃ）
        const string ja = "<size=150%><color=#FF6B5C>CAMボタン</color></size>\nVRChatのカメラ（写真・配信）にだけ、\nふわふわを追いかける映像を映すよ\n" + modes;
        const string en = "<size=150%><color=#FF6B5C>CAM Button</color></size>\nShows a fluff-chasing view\nonly in VRChat's camera (photo/stream)\n" + modes;
        var lobby = studio.parent;
        var best = studio.GetComponentsInChildren<Transform>(true).First(t => t.name == "BestTimes");
        var srcPanel = best.Find("Panel").GetComponent<SpriteRenderer>();
        var srcText = best.Find("Text").GetComponent<TMPro.TextMeshPro>();
        var info = new GameObject("CamInfo").transform;
        info.SetParent(studio, false);
        info.localPosition = cam.localPosition + Vector3.up * 0.65f;   // 0.85倍に縮めた分、ボタンとのすき間がそのままになるよう少し下げる
        info.localRotation = cam.localRotation;
        info.localScale = Vector3.one * 0.85f;   // てじゃ「説明看板もちょいちいさく」
        var panel = Object.Instantiate(srcPanel.gameObject, info).GetComponent<SpriteRenderer>();
        panel.name = "Panel";
        panel.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = Vector3.one * 0.5f;
        panel.size = new Vector2(2.05f, 0.95f) / 0.5f;
        var go = new GameObject("Text");
        go.transform.SetParent(info, false);
        var t = go.AddComponent<TMPro.TextMeshPro>();
        t.font = srcText.font; t.fontSharedMaterial = srcText.fontSharedMaterial; t.color = srcText.color;
        t.fontSize = 1.15f; t.alignment = TMPro.TextAlignmentOptions.Center; t.enableWordWrapping = false;
        t.lineSpacing = -10f;
        t.rectTransform.sizeDelta = new Vector2(1.95f, 0.9f);
        t.text = ja;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 板は文字にぴったり（上下の余白を減らす、てじゃ）。下のはしは今まで通り（ボタンとのすき間を変えない）。幅は長いほうの言語に合わせる
        {
            float w = 0f, h = 0f;
            foreach (var s in new[] { en, ja }) { t.text = s; t.ForceMeshUpdate(); w = Mathf.Max(w, t.textBounds.size.x); h = Mathf.Max(h, t.textBounds.size.y); }
            t.text = ja; t.ForceMeshUpdate();
            float pw = w + 0.12f, ph = h + 0.16f, bottom = -0.475f;   // 左右の余白も小さく（てじゃ）
            panel.size = new Vector2(pw, ph) / 0.5f;
            panel.transform.localPosition = new Vector3(0f, bottom + ph * 0.5f, 0.02f);
            t.rectTransform.sizeDelta = new Vector2(pw - 0.1f, ph);
            go.transform.localPosition = new Vector3(0f, bottom + ph * 0.5f - (t.textBounds.center.y), 0f);
        }

        // 言語の切り替えに登録（作り直しで消えた古い札は詰める）
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null)
        {
            var so = new SerializedObject(lang);
            var tx = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
            for (int i = tx.arraySize - 1; i >= 0; i--)
                if (tx.GetArrayElementAtIndex(i).objectReferenceValue == null)
                { tx.DeleteArrayElementAtIndex(i); if (i < pj.arraySize) pj.DeleteArrayElementAtIndex(i); if (i < pe.arraySize) pe.DeleteArrayElementAtIndex(i); }
            int n = tx.arraySize;
            tx.arraySize = n + 1; pj.arraySize = n + 1; pe.arraySize = n + 1;
            tx.GetArrayElementAtIndex(n).objectReferenceValue = t;
            pj.GetArrayElementAtIndex(n).stringValue = ja;
            pe.GetArrayElementAtIndex(n).stringValue = en;
            so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(lang);
            EditorUtility.SetDirty(lang);
        }
        return "cam info";
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
