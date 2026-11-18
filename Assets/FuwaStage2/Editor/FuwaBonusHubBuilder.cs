using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2026-11-18 てじゃ「おまけゴール→ゴール表示はなし、花火とかキノコは出る、ポータルが開く→おまけ本島行き」「場所は遊び場みたいにどっかに飛ばす」「B（記念の島）」
//  おまけ本島（記念の島）：ステージ2ロビーの島を大きくして別の場所に置く（FuwaAreaVisibility の area 4＝bonusObjects、ふだんは消えている）。
//  ・まわりに全10コースの記念の品（各コースの物の見た目だけを写して縮めた物）を、コースの道の色の台にのせて並べる
//  ・まん中に「Thank you for playing!」＋自己ベスト一覧のボード（おまけのゴールの ClearBanner をこちらへ移す）、左右にきんピカ銃の台・金ピカふわふわ
//  ・奥にステージ2ロビーへのポータル
//  ・おまけのゴール：文字（ClearBanner）は出さない。花火・キノコのあとに開くワープ（GoalWarp）の行き先をここに
//  ・BGM：ゴール後の曲（BGM_bonus_after）をここでもそのまま流す（starts にここの BallPark）
//  FuwaRewardIslandBuilder（ごほうびの島を作り直す）の最後からも呼ぶ。何度実行してもよい
public static class FuwaBonusHubBuilder
{
    const string RootName = "FuwaBonusHub";
    public static Vector3 Pos = new Vector3(-200f, 120f, -200f);
    public static float Shrink = 0.75f;
    public static bool BoardPickup = true;   // 置き場所さがし用（一時的）   // 最後に全部まとめて縮める（1.4倍のロビー → ロビーの1.05倍くらい）
    public static float Scale = 1.4f, ItemRing = 8.0f, PedR = 1.1f, PedH = 0.45f, ItemSize = 1.5f, BoardY = 4.3f;
    // スクショコーナー：キノコの輪（中心は島のまん中から RingShift 奥）、ボードは輪の中の奥、その手前に立つ所、撮る人の印
    public static int RingCount = 9; public static float RingHFront = 1.4f, RingHBack = 4.0f, RingHVar = 0.25f, RingJitterDeg = 6f, RingJitterR = 0.5f, BoardClear = 0.3f, BackCenterScale = 1.2f, MushOverlapR = 0.8f, BoardMarginX = 0.25f, FrontLeftScale = 0.6f, RingFrontR = 3.0f, LeanMin = 3f, LeanMax = 8f;
    public static float RingShift = 1.5f, RingR = 4.4f, RingGap = 55f, BoardBack = 3.4f, TitleY = 4.45f, TitleScale = 1.0f,
        ColX = 1.75f, ColY = 3.3f, ColScale = 1f, ColFont = 1.9f, PanelW = 3.0f, PanelH = 0.95f, StageR = 2.5f, CamDist = 7.0f;
    const string ThanksText = "<size=62%><color=#FF8FA3>T</color><color=#FFC36B>h</color><color=#7FD7A8>a</color><color=#7FB8F0>n</color><color=#B79BF0>k</color> <color=#FF8FA3>y</color><color=#FFC36B>o</color><color=#7FD7A8>u</color> <color=#7FB8F0>f</color><color=#B79BF0>o</color><color=#FF8FA3>r</color> <color=#FFC36B>p</color><color=#7FD7A8>l</color><color=#7FB8F0>a</color><color=#B79BF0>y</color><color=#FF8FA3>i</color><color=#FFC36B>n</color><color=#7FD7A8>g</color><color=#7FB8F0>!</color></size>";

    [MenuItem("FuwaCourse/ステージ2/おまけ本島（記念の島）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    static Transform Find(string path) { var p = path.Split(new[] { '/' }, 2); var r = GameObject.Find(p[0]); return r == null ? null : p.Length > 1 ? r.transform.Find(p[1]) : r.transform; }

    public static string Build()
    {
        var log = new System.Text.StringBuilder("[おまけ本島] ");
        var lobby = Find("FuwaStage2/Lobby"); var reward = Find("FuwaRewardIsland");
        if (!lobby || !reward) return "ステージ2ロビー／ごほうびの島がない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);   // 消えている（非表示）物も
        var root = new GameObject(RootName).transform; root.position = Pos;
        var lc = lobby.Find("LobbySpawn").position; float floorTop = lobby.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;

        // ---- 島（ステージ2ロビーの床・ふち・柵・下の岩・まん中の丸を、ロビーのまん中を原点に Scale 倍）----
        var island = new GameObject("Island").transform; island.SetParent(root, false);
        foreach (var nm in new[] { "LobbyFloor", "LobbyRing", "Fence", "FenceCollider", "IslandUnder" })
        {
            var src = lobby.Find(nm); if (!src) continue;
            var g = Object.Instantiate(src.gameObject, island, true); g.name = nm;
            FuwaStage2Builder.StripBehaviour(g);
            var off = src.position - new Vector3(lc.x, floorTop, lc.z);
            g.transform.position = Pos + off * Scale; g.transform.localScale = src.lossyScale * Scale;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
        }
        float R = 8f * Scale;   // 床の半径（ロビーは16m）

        // ---- 立ち位置・ふわふわ・銃 ----
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false);
        spawn.position = Pos + Vector3.back * (R - 2.2f) + Vector3.up * 0.1f; spawn.rotation = Quaternion.LookRotation(Vector3.forward);
        var ballPark = new GameObject("BallPark").transform; ballPark.SetParent(root, false);
        ballPark.position = Pos + Vector3.back * (R - 3.4f) + Vector3.right * 1.2f + Vector3.up * 1.2f;
        var gunSpot = new GameObject("GunSpot").transform; gunSpot.SetParent(root, false);
        gunSpot.position = Pos + Vector3.back * (R - 2.6f) + Vector3.left * 1.0f + Vector3.up * 1.0f; gunSpot.rotation = spawn.rotation;

        // ---- まん中：ボード（おまけのゴールの ClearBanner を移す）・きんピカ台・金ピカふわふわ ----
        var goal = reward.Find("Goal");
        // 文字・板の見本：おまけのゴールの ClearBanner（あれば。使ったら消す）、なければ 2–4 のゴールの物
        var rewardBanner = goal ? goal.Find("ClearBanner") : null;
        var banner = rewardBanner ? rewardBanner : Find("FuwaCourse2_4/Goal/ClearBanner");
        // 2026-11-18 てじゃ「大きなキノコのフェアリーリング、真ん中にThank you～の文字列と全タイム表示。文字やボードの向きは固定、いわゆるスクショコーナー。プレイヤーが立つスペースも考慮」
        //  ゴールの ClearBanner の文字（ClearLabel）と時間の板（TimeLabel）を写して、向き固定（FuwaFacePlayer を外す）の背景ボードを作る。
        //  タイムはステージ1／ステージ2の2列（頭で隠れにくいよう横長・高め）。元の ClearBanner は消す（ゴールでは文字を出さない）
        var ringC = Pos + Vector3.forward * RingShift;
        if (banner)
        {
            var board = new GameObject("Board").transform; board.SetParent(root, false);
            board.position = ringC + Vector3.forward * BoardBack; board.rotation = Quaternion.identity;   // 文字は南（スタート側）から読める向き
            var cl = banner.Find("ClearLabel"); var tl = banner.Find("TimeLabel");
            if (cl)
            {
                var t = Object.Instantiate(cl.gameObject, board, false); t.name = "Title"; FuwaStage2Builder.StripBehaviour(t);
                t.transform.localPosition = Vector3.up * TitleY; t.transform.localRotation = Quaternion.identity; t.transform.localScale = Vector3.one * TitleScale;
                var tt = t.GetComponent<TMPro.TextMeshPro>(); if (tt) { tt.text = ThanksText; tt.rectTransform.sizeDelta = new Vector2(8f, 2.4f); }
            }
            var recs = Object.FindObjectsOfType<FuwaRecords>(true).Where(x => x.name == "Records" || x.name == "Records2").OrderBy(x => x.name).ToArray();
            for (int c = 0; c < 2 && tl; c++)
            {
                var t = Object.Instantiate(tl.gameObject, board, false); t.name = "Times" + (c + 1); FuwaStage2Builder.StripBehaviour(t);
                t.transform.localPosition = new Vector3((c == 0 ? -1 : 1) * ColX, ColY, 0f); t.transform.localRotation = Quaternion.identity; t.transform.localScale = Vector3.one * ColScale;
                var tmp = t.GetComponent<TMPro.TextMeshPro>();
                if (tmp) { tmp.fontSize = ColFont; tmp.rectTransform.sizeDelta = new Vector2(PanelW, PanelH); tmp.alignment = TMPro.TextAlignmentOptions.Center; }
                var p = t.transform.Find("Panel"); var sr = p ? p.GetComponent<SpriteRenderer>() : null;
                if (sr) { p.localScale = new Vector3(0.62f, 0.62f, 1f); if (sr.drawMode != SpriteDrawMode.Simple) sr.size = new Vector2((PanelW + 0.4f) / 0.62f, (PanelH + 0.3f) / 0.62f); }
                var tb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaThanksBoard>(t);
                tb.records = c < recs.Length ? new[] { recs[c] } : new FuwaRecords[0]; tb.text = tmp;
                tb.title = "";   // 見出しなし（てじゃ「ステージ◯も消してもっと詰めよう」）
                tb.columns = true; tb.numberPrefix = c == 0 ? "1–" : "2–";   // 列そろえ・ステージ1も「1–1」
                if (tmp) { tmp.alignment = TMPro.TextAlignmentOptions.Left; tmp.margin = new Vector4(BoardMarginX, 0f, BoardMarginX, 0f); tmp.enableWordWrapping = false; tmp.overflowMode = TMPro.TextOverflowModes.Overflow; }
                UdonSharpEditorUtility.CopyProxyToUdon(tb); FuwaStage2WaterTest.NoSync(tb);
                if (tmp) tmp.text = "1 ……  0:00.00";
            }
            if (rewardBanner) Object.DestroyImmediate(rewardBanner.gameObject);
            var g = goal ? goal.GetComponentInChildren<FuwaGoal>(true) : null;
            if (g) { var so = new SerializedObject(g); so.FindProperty("clearBanner").objectReferenceValue = null; so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(g); }
            log.Append("ボード ");
        }
        var ra = goal ? goal.GetComponent<FuwaRewardAfterGoal>() : null;
        var stands = new List<Transform>();
        foreach (var nm in new[] { "GoldGunStand", "GoldFluffStand" })
        {
            var t = goal ? goal.Find(nm) : null; if (!t && old) t = old.transform.Find(nm);
            if (!t) continue;
            t.SetParent(root, true); t.gameObject.SetActive(true); stands.Add(t);
        }
        // きんピカ台・金ピカふわふわ：輪の外の右（東）。島のまん中を向く
        if (stands.Count > 0) { stands[0].position = Pos + new Vector3(8.4f, 0f, -1.6f); stands[0].rotation = Quaternion.LookRotation(Vector3.left); }
        if (stands.Count > 1) { stands[1].position = Pos + new Vector3(8.4f, 0f, 1.6f); stands[1].rotation = Quaternion.LookRotation(Vector3.left); }
        if (ra) Undo.DestroyObjectImmediate(ra);   // ゴールの後に台を出す係はいらない（台はいつもここにある）

        // ---- 奥：ステージ2ロビーへのポータル（ごほうびの島のスタートのワープを写す）----
        var st = reward.Find("Start");
        var portal = new GameObject("ToLobby").transform; portal.SetParent(root, false);
        portal.position = Pos + Vector3.left * (R - 2.6f); portal.rotation = Quaternion.LookRotation(Vector3.right);   // 輪の外の左（西）
        if (st)
        {
            var vis = st.Cast<Transform>().Where(t => t.name.StartsWith("WarpToLobby") && !t.name.EndsWith("_Trigger") && !t.name.EndsWith("_Trigger_Gauge")).ToList();
            var refT = st.Find("WarpToLobby");
            foreach (var v in vis)
            {
                var g = Object.Instantiate(v.gameObject, portal, false); g.name = v.name;
                g.transform.localPosition = v.localPosition - (refT ? refT.localPosition : Vector3.zero); g.transform.localRotation = v.localRotation; g.transform.localScale = v.localScale;
                FuwaStage2Builder.StripBehaviour(g);
            }
            var trig = st.Find("WarpToLobby_Trigger"); var gauge = st.Find("WarpToLobby_Trigger_Gauge");
            if (trig && gauge)
            {
                var w = (FuwaCourseWarp)FuwaStage2Builder.MakeWarp(portal, "WarpToLobby", trig.gameObject, gauge.gameObject,
                    lobby.Find("BallPark"), reward.Find("LobbySpawn_FromReward") ?? lobby.Find("LobbySpawn"), lobby.Find("GunStand_Lobby/GunSpot"), 2);
                foreach (var n in new[] { "WarpToLobby_Trigger", "WarpToLobby_Trigger_Gauge" })
                {
                    var c = portal.Find(n); var s = st.Find(n); if (!c || !s) continue;
                    c.localPosition = s.localPosition - (refT ? refT.localPosition : Vector3.zero);
                }
                // 足もとの台（ごほうびの島のワープの台）。うずはその上に（床とチラつかないように）
                var wg = st.Find("StartArea/WarpGround");
                if (wg && refT)
                {
                    var g = Object.Instantiate(wg.gameObject, portal, true); g.name = "WarpGround"; FuwaStage2Builder.StripBehaviour(g);
                    float top = wg.GetComponent<Renderer>().bounds.max.y, swirlY = refT.position.y;
                    g.transform.position = portal.position + (wg.position - refT.position);
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
                    // 台の上面（＝うずの高さの近く）が島の床より 15cm 上に出るように、ポータルごと持ち上げる
                    float rise = Pos.y + 0.15f - (portal.position.y + (top - swirlY));
                    if (rise > 0f) portal.position += Vector3.up * rise;
                }
                log.Append("戻りポータル ");
            }
        }

        // ---- キノコの輪（フェアリーリング）----
        // 2026-11-18 てじゃ「ゴールのミニキノコが大きくなったみたいなイメージ。カラーのラインナップもおなじに」「スケールでかくするんじゃなくて、ちゃんとつくろう」
        //  → Blender の bigmush_build.py（FLUFF_models.blend、Islets シーンの big_params）で、ミニキノコと同じ形の考え方の大きいキノコを6種類作った
        //  （ポリゴン細かめ・かさの裏に本物のひだ・根元の広がり・かさのふちのゆらぎ）。Tools/bigmush.json → BigMush_1〜6.asset。
        //  色はゴールのミニキノコと同じ（かさ：パステルの赤・ピンク・クリーム・オレンジ、ひだ MiniMush_Gill、軸 Mush_Stem）。手前（撮る人側）はあける
        var meshes = ImportBigMush();
        var ring = new GameObject("FairyRing").transform; ring.SetParent(root, false);
        var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        if (meshes.Length > 0)
        {
            var caps = new[] { "Mush_CapRed", "Mush_CapPink", "Mush_CapCream", "MiniMush_CapOrange" }.Select(FuwaLobbyPaths.FindMat).Select(FuwaGiantMushrooms.RimMat).ToArray();   // ふちのグラデつき
            var stemM = FuwaLobbyPaths.FindMat("Mush_Stem"); var gillM = FuwaGiantMushrooms.UnderMat();   // 裏のすじ模様（きのこのたにの巨大キノコと同じ）
            int n = RingCount; var rnd = new System.Random(77);
            var boardT = root.Find("Board"); var boardB = new Bounds();
            if (boardT) { var brs = boardT.GetComponentsInChildren<Renderer>(true); if (brs.Length > 0) { boardB = brs[0].bounds; foreach (var br in brs) boardB.Encapsulate(br.bounds); } }
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(-(180f - RingGap), 180f - RingGap, i / (float)(n - 1));   // 北（奥）が 0
                float back = Mathf.Cos(a * Mathf.Deg2Rad);
                float h = Mathf.Lerp(RingHFront, RingHBack, (back + 1f) * 0.5f) * (1f - RingHVar + 2f * RingHVar * (float)rnd.NextDouble());   // てじゃ「大きさにはちょっとムラつけてね」
                // てじゃ「規則正しすぎる、若干の曲がりとか角度とかのムラがほしい」：輪の上の位置（角度・半径）を少しずらし、1本ずつ少し傾ける
                a += ((float)rnd.NextDouble() - 0.5f) * 2f * RingJitterDeg;
                float rad = RingR + ((float)rnd.NextDouble() - 0.5f) * 2f * RingJitterR;
                if (back < 0f) rad += (RingFrontR - RingR) * -back;   // 手前のは立つ所の丸に寄せる（2026-11-18 てじゃ「もっとサークルに寄せていいかも」「てまえのやつ」）
                var at = ringC + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * rad;
                var mesh = meshes[(i * 4 + 1) % meshes.Length];
                var go = new GameObject("Mush" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(ring, false);
                bool centerBack = i == n / 2;   // 奥のまん中：大きめの赤（2026-11-18 てじゃ「奥の真ん中は大きめの赤いキノコがいいな」）
                if (centerBack) h = RingHBack * BackCenterScale;
                if (i == 0) h *= FrontLeftScale;   // 手前の左（赤）は小さめ（2026-11-18 てじゃ「左手前の赤もっとちいさく」）
                float k = h / mesh.bounds.size.y;
                go.transform.position = new Vector3(at.x, Pos.y - 0.12f, at.z);   // 傾けても根元が浮かないよう少しうめる
                float lean = Mathf.Lerp(LeanMin, LeanMax, (float)rnd.NextDouble()), leanDir = (float)rnd.NextDouble() * 360f;
                go.transform.rotation = Quaternion.AngleAxis(lean, Quaternion.Euler(0f, leanDir, 0f) * Vector3.right) * Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * k;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { stemM, centerBack || i == 0 ? caps[0] : caps[(i * 3 + 2) % caps.Length], gillM };   // caps[0] は赤。手前の左も赤（てじゃ「手前二個とも黄色だから左は赤に」）
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                go.layer = 11;
                // ボードとかぶらないように（2026-11-18 てじゃ「おまけ島、キノコとボードが被ってた気がする」：大人の形でかさが広がった）：
                //  奥のまん中だけは外へずらす（大きいまま）。ほかは小さくする（2026-11-18 てじゃ「もっとキノコ寄せよう、重なるようなら縮める」）
                if (boardB.size != Vector3.zero && !centerBack)
                {
                    var bbx = boardB; bbx.Expand(BoardClear);
                    for (int it = 0; it < 40 && mr.bounds.Intersects(bbx); it++) go.transform.localScale *= 0.94f;
                }
                if (boardB.size != Vector3.zero && centerBack)
                {
                    var bbx = boardB; bbx.Expand(BoardClear);
                    var dirOut = new Vector3(at.x - ringC.x, 0f, at.z - ringC.z).normalized;
                    for (int it = 0; it < 30 && mr.bounds.Intersects(bbx); it++)
                    {
                        var pos = go.transform.position + dirOut * 0.3f;
                        if (new Vector2(pos.x - Pos.x, pos.z - Pos.z).magnitude < R - 1.8f) go.transform.position = pos;
                        else go.transform.localScale *= 0.93f;
                    }
                }
                // 茎の当たり判定（かさの上には乗れない高さ）
                var col = go.AddComponent<CapsuleCollider>(); col.direction = 1;
                col.radius = mesh.bounds.size.x * 0.13f; col.height = mesh.bounds.size.y * 0.75f; col.center = new Vector3(0f, col.height * 0.5f, 0f);
            }
            // キノコどうしが重なったら小さくする（かさを上から見た丸で判定。大きい順に場所を取り、あとの方を縮める）
            {
                var ms = ring.Cast<Transform>().Select(t => t.GetComponent<MeshRenderer>()).OrderByDescending(r => r.name == "Mush" + (n / 2) ? 1e9f : r.bounds.size.y).ToList();
                var placed = new List<MeshRenderer>();
                foreach (var r in ms)
                {
                    for (int it = 0; it < 40; it++)
                    {
                        var b = r.bounds; var c = new Vector2(b.center.x, b.center.z); float rr = Mathf.Max(b.extents.x, b.extents.z) * MushOverlapR;
                        bool hit = placed.Any(o => { var ob = o.bounds; return (new Vector2(ob.center.x, ob.center.z) - c).magnitude < rr + Mathf.Max(ob.extents.x, ob.extents.z) * MushOverlapR; });
                        if (!hit) break;
                        r.transform.localScale *= 0.94f;
                    }
                    placed.Add(r);
                }
            }
            // ときどき1本ずつぽよんと揺れる（2026-11-18 てじゃ「おまけ本島のきのこもピョコピョコしてほしい」）。動くので静的にしない
            foreach (Transform m in ring) GameObjectUtility.SetStaticEditorFlags(m.gameObject, 0);
            var sq = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaIdleSquash>(ring.gameObject); sq.root = ring;
            UdonSharpEditorUtility.CopyProxyToUdon(sq); FuwaStage2WaterTest.NoSync(sq);
            log.Append("キノコの輪 " + n + " ");
        }
        // 立つ所：ボードの手前の、少しだけ高い丸いステージ（クリーム色）
        {
            var st0 = new GameObject("Stage", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)).transform; st0.SetParent(root, false);
            var cb = cyl.bounds.size;
            st0.position = ringC + Vector3.forward * (BoardBack - StageR - 0.2f) + Vector3.up * 0.05f;
            st0.localScale = new Vector3(StageR * 2f / cb.x, 0.1f / cb.y, StageR * 2f / cb.z);
            st0.GetComponent<MeshFilter>().sharedMesh = cyl; st0.GetComponent<MeshCollider>().sharedMesh = cyl;
            st0.GetComponent<MeshRenderer>().sharedMaterial = BonusMat("Bonus_PhotoStage", new Color(1f, 0.9f, 0.88f));
            st0.gameObject.layer = 11;
            // 撮る人の立ち位置の印：ボードが画面いっぱいに入る距離。足あと代わりの水色の丸
            var mk = new GameObject("CameraSpot", typeof(MeshFilter), typeof(MeshRenderer)).transform; mk.SetParent(root, false);
            mk.position = ringC + Vector3.forward * (BoardBack - CamDist) + Vector3.up * 0.012f;
            mk.localScale = new Vector3(0.9f / cb.x, 0.02f / cb.y, 0.9f / cb.z);
            mk.GetComponent<MeshFilter>().sharedMesh = cyl;
            mk.GetComponent<MeshRenderer>().sharedMaterial = BonusMat("Bonus_CameraSpot", new Color(0.62f, 0.83f, 1f));
            mk.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---- エリアの出し入れ・ワープ・BGM ----
        var av = Object.FindObjectsOfType<FuwaAreaVisibility>(true).First();
        {
            var so = new SerializedObject(av); var arr = so.FindProperty("bonusObjects");
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null || arr.GetArrayElementAtIndex(i).objectReferenceValue == (old ? old : null)) arr.DeleteArrayElementAtIndex(i);
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject;
            // 遠くのけしき（2026-11-18 てじゃ「遠景みえててほしいな」「雲と小島とか」）：ステージ1の雲・遠くの浮島（コースは出さない）
            var rootsAll = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var far = new[] { "CloudPuffs", "FarIslands" }.Select(nm => rootsAll.FirstOrDefault(g => g.name == nm)).Where(g => g).ToArray();
            var lites = far.Where(g => g.name == "CourseLites").SelectMany(g => g.transform.Cast<Transform>().Select(t => t.gameObject)).ToArray();
            void SetArr(string name, GameObject[] objs) { var pa = so.FindProperty(name); pa.arraySize = objs.Length; for (int i = 0; i < objs.Length; i++) pa.GetArrayElementAtIndex(i).objectReferenceValue = objs[i]; }
            SetArr("bonusFarObjects", far); SetArr("bonusLites", lites);
            log.Append("遠景 " + far.Length + "・軽い版 " + lites.Length + " ");
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(av);
        }
        var gw = goal ? goal.Find("GoalWarp") : null;
        var gwt = gw ? gw.GetComponentsInChildren<FuwaCourseWarp>(true).FirstOrDefault() : null;
        if (gwt)
        {
            var so = new SerializedObject(gwt);
            so.FindProperty("courseStart").objectReferenceValue = ballPark;
            so.FindProperty("playerSpot").objectReferenceValue = spawn;
            so.FindProperty("gunSpot").objectReferenceValue = gunSpot;
            so.FindProperty("area").intValue = 4; so.FindProperty("stage").intValue = 0; so.FindProperty("homeAction").intValue = 0; so.FindProperty("timed").boolValue = false;
            so.FindProperty("hideOnWarp").objectReferenceValue = null;   // 開きっぱなし（ワープしても隠さない。2026-11-18 てじゃ「ためしてるけどなってない」）
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(gwt);
            foreach (var t in gw.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.text = "<size=80%>きねんのしま</size>"; EditorUtility.SetDirty(t); }
            // 一度開いたら開きっぱなし（2026-11-18 てじゃ）
            var fg = goal.GetComponentInChildren<FuwaGoal>(true);
            if (fg) { var gso = new SerializedObject(fg); gso.FindProperty("keepWarpOpen").boolValue = true; gso.FindProperty("noGunMessage").boolValue = true; gso.FindProperty("stayDone").boolValue = true; gso.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(fg); }
            log.Append("ゴールのワープ→本島 ");
        }
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        if (bgm)
        {
            var so = new SerializedObject(bgm); var tracks = so.FindProperty("tracks"); var starts = so.FindProperty("starts");
            for (int i = 0; i < tracks.arraySize && i < starts.arraySize; i++)
            {
                var a = tracks.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                if (a && a.name == "BGM_bonus_after") { starts.GetArrayElementAtIndex(i).objectReferenceValue = ballPark; log.Append("BGM "); }
            }
            // おまけのコース（BGM_bonus）は一度ゴールしたら、その後ずっとゴール後の曲（2026-11-18 てじゃ「一度ゴールのポータル開いた後も、BGM消えなくなって」）
            var sticky = so.FindProperty("stickyAfterGoal"); sticky.arraySize = starts.arraySize;
            for (int i = 0; i < starts.arraySize; i++)
            {
                var a = i < tracks.arraySize ? tracks.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource : null;
                sticky.GetArrayElementAtIndex(i).boolValue = a && a.name == "BGM_bonus";
            }
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(bgm);
        }
        // 2026-11-18 てじゃ「おまけ島、全体的にデカすぎるかな」「本島のもの全て」：島・キノコ・ボード・台・ポータルぜんぶを島のまん中（床の高さ）を中心に縮める
        // 2026-11-18 てじゃ「一旦ボードをPickupオブジェクトにしてよ」：置き場所さがし用（持って動かせる、重力なし・離したらその場に浮く）。決まったら BoardPickup=false に
        if (BoardPickup)
        {
            var bt = root.Find("Board");
            var brs2 = bt ? bt.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            if (brs2.Length > 0)
            {
                var wb = brs2[0].bounds; foreach (var br in brs2) wb.Encapsulate(br.bounds);
                var bc = bt.gameObject.AddComponent<BoxCollider>();
                bc.center = bt.InverseTransformPoint(wb.center); var sz = bt.InverseTransformVector(wb.size); bc.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Max(0.2f, Mathf.Abs(sz.z)));
                var rb = bt.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                var pu = bt.gameObject.AddComponent<VRC.SDK3.Components.VRCPickup>();
                pu.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pu.InteractionText = "ボード"; pu.proximity = 3f; pu.allowManipulationWhenEquipped = true;
                bt.gameObject.layer = 13;   // Pickup
                log.Append("ボードを持てる ");
            }
        }
        root.localScale = Vector3.one * Shrink;
        if (old) Object.DestroyImmediate(old);
        root.gameObject.SetActive(false);   // ふだんは消えている（area 4 の時だけ）
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log.ToString();
    }

    // Tools/bigmush.json（Blender の bigmush_build.py が書き出す）→ メッシュのアセット
    [System.Serializable] class MushItem { public string name; public float[] v, n, uv; public int[] s0, s1, s2; }
    [System.Serializable] class MushFile { public MushItem[] items; }
    const string MushDir = "Assets/FuwaStage2/Generated/BigMush";
    public static Mesh[] ImportBigMush()
    {
        var path = System.IO.Path.Combine(Application.dataPath, "../Tools/bigmush.json");
        if (!System.IO.File.Exists(path)) return new Mesh[0];
        var f = JsonUtility.FromJson<MushFile>(System.IO.File.ReadAllText(path));
        if (!AssetDatabase.IsValidFolder(MushDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Generated", "BigMush");
        var list = new List<Mesh>();
        foreach (var it in f.items)
        {
            string ap = MushDir + "/" + it.name + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(ap); bool isNew = !m;   // CopySerialized はサブメッシュが消えるので、今のアセットに直接入れ直す
            if (isNew) m = new Mesh(); else m.Clear();
            m.name = it.name; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var vs = new Vector3[it.v.Length / 3]; var ns = new Vector3[vs.Length];
            for (int i = 0; i < vs.Length; i++) { vs[i] = new Vector3(it.v[i * 3], it.v[i * 3 + 1], it.v[i * 3 + 2]); ns[i] = new Vector3(it.n[i * 3], it.n[i * 3 + 1], it.n[i * 3 + 2]); }
            m.vertices = vs; m.normals = ns; m.subMeshCount = 3;
            if (it.uv != null && it.uv.Length == vs.Length * 2) { var us = new Vector2[vs.Length]; for (int i = 0; i < us.Length; i++) us[i] = new Vector2(it.uv[i * 2], it.uv[i * 2 + 1]); m.uv = us; }
            m.SetTriangles(it.s0, 0); m.SetTriangles(it.s1, 1); m.SetTriangles(it.s2, 2);
            m.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(m, ap); else EditorUtility.SetDirty(m);
            list.Add(m);
        }
        AssetDatabase.SaveAssets();
        return list.ToArray();
    }

    static Material BonusMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_PathA.mat"); m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        if (m.HasProperty("_Color")) m.SetColor("_Color", c); EditorUtility.SetDirty(m); return m;
    }

    // 高さが h になるよう縮めて、底を slot の高さに、真ん中を slot の位置に
    static void FitHeight(Transform vis, Transform slot, float h)
    {
        var rs = vis.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
        Bounds B() { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        vis.SetParent(slot, true);
        var b0 = B(); vis.localScale *= h / Mathf.Max(0.01f, b0.size.y);
        var stem = rs.FirstOrDefault(r => r.name == "Stem"); var b1 = B();
        var cx = stem ? stem.bounds.center : b1.center;
        vis.position += new Vector3(slot.position.x - cx.x, slot.position.y - b1.min.y, slot.position.z - cx.z);
    }

    static Material PathMat(string key)
    {
        if (key.Contains("_")) return AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course" + key.Replace("2_", "2_") + "_PathA.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_" + key.Substring(2) + "_PathA.mat");
        var pm = GameObject.Find(key)?.transform.Find("PathMesh")?.GetComponent<Renderer>();
        return pm ? pm.sharedMaterial : AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course1_PathA.mat");
    }

    // 見た目だけを写す：形のある部品（MeshRenderer）だけ、LODは一番細かい物だけ、影・模様・ベール・軽い版などは除く
    static Transform CopyVisual(Transform src, Transform parent)
    {
        var holder = new GameObject("Visual").transform; holder.SetParent(parent, false);
        holder.SetPositionAndRotation(src.position, src.rotation);
        var lodOut = new HashSet<Renderer>();
        foreach (var lg in src.GetComponentsInChildren<LODGroup>(true)) { var lods = lg.GetLODs(); for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r) lodOut.Add(r); }
        string[] badName = { "IslandTop", "IslandUnder", "Lite", "_Far", "Shadow", "Decal", "Highlight", "Veil", "Trigger", "Gauge", "Marker", "Bubble", "Alert", "Sleep", "Text", "Label", "Halo" };
        string[] badShader = { "Decal", "Veil", "Highlight", "Invisible", "Shadow", "Glow", "Mist" };
        foreach (var r in src.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r.enabled || lodOut.Contains(r)) continue;
            if (badName.Any(b => r.name.Contains(b))) continue;
            if (r.sharedMaterials.Any(m => m && badShader.Any(b => m.shader.name.Contains(b)))) continue;
            // 非表示の物（寝てる目など）は除く。src から上の非表示（エリアの出し入れ）は気にしない
            bool hidden = false; for (var t = r.transform; t && t != src.parent; t = t.parent) if (!t.gameObject.activeSelf) { hidden = true; break; }
            if (hidden) continue;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var g = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(holder, true);
            g.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            g.transform.localScale = DivideScale(r.transform.lossyScale, holder.lossyScale);
            g.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var nr = g.GetComponent<MeshRenderer>(); nr.sharedMaterials = r.sharedMaterials;
            nr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; nr.receiveShadows = r.receiveShadows;
            nr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }
        return holder;
    }
    static Vector3 DivideScale(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

    // いちばん長い辺が size になるよう縮めて、台の上（slot の上 pedH）に底をのせ、真ん中をそろえる
    static void Fit(Transform vis, Transform slot, float size, float pedH)
    {
        var rs = vis.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
        Bounds B() { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        var b0 = B(); float m = Mathf.Max(b0.size.x, b0.size.y, b0.size.z);
        float k = size / Mathf.Max(0.01f, m);
        // 向き：元の向きのまま、台の向き（まん中を向く）に合わせて回す
        vis.SetParent(slot, true);
        vis.localScale = vis.localScale * k;
        var b = B();
        vis.position += new Vector3(slot.position.x - b.center.x, slot.position.y + pedH - b.min.y, slot.position.z - b.center.z);
    }
}
