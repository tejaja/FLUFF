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
    public static float Shrink = 0.65f;   // 2026-11-18 てじゃ「本島もうちょい小さく」0.75→0.65
    public static float SpawnFromEdge = 3.2f, PortalFromEdge = 1.3f; public static bool PortalBase = false, CameraSpotMark = false;
    public static bool BoardPickup = false;
    public static bool SharedBoards = true;
    public static float MushSinkMax = 0.04f, StageDirtUv = 2.5f;   // キノコの根元が床にうまる深さの上限(m、縮める前)
    public static float[] PhotoFluffSizes = { 0.165f, 0.3f, 0.9f }; public static float PhotoFluffSpread = 1.2f;   // てじゃ「通常＋2-5にある２種で」：ふつう30cm、ちいさく（×0.55）、おおきく（×3）
    public static float GoldFluffSize = 0.7f, StatueYaw = 90f;   // 金の銃の像の向き（台の向きから）   // 金ピカふわふわの直径(m)   // ボード3つをみんなで持てる（同期）   // 置き場所さがし用（一時的）   // 最後に全部まとめて縮める（1.4倍のロビー → ロビーの1.05倍くらい）
    // 2026-11-19 てじゃ「おまけ本島やっぱ広すぎるかなぁ」→ A（島だけ小さく、物は今の大きさで寄せる）：床・柵・下の岩を横だけ IslandXZ 倍。
    //  スポーン・戻りポータル・西の橋は床の半径 R から、金の台は GoldX、ボタン・あしあとは柵の半径から決まる。きのこの輪・ボード・ステージは PhotoShiftZ だけ手前へ
    public static float IslandXZ = 0.75f, PhotoShiftZ = -1.5f, CenterBackPush = 0.6f;
    public static float GoldX => 8.4f * IslandXZ;
    public static float Scale = 1.4f, ItemRing = 8.0f, PedR = 1.1f, PedH = 0.45f, ItemSize = 1.5f, BoardY = 4.3f;
    // スクショコーナー：キノコの輪（中心は島のまん中から RingShift 奥）、ボードは輪の中の奥、その手前に立つ所、撮る人の印
    public static int RingCount = 9; public static float RingHFront = 1.4f, RingHBack = 4.0f, RingHVar = 0.25f, RingJitterDeg = 6f, RingJitterR = 0.5f, BoardClear = 0.3f, BackCenterScale = 1.2f, MushOverlapR = 0.8f, BoardMarginX = 0.25f, FrontLeftScale = 0.6f, RingFrontR = 3.0f, RingHugGap = 0.7f, LeanMin = 3f, LeanMax = 8f;
    public static float RingShift = 1.5f, RingR = 4.4f, RingGap = 55f, BoardBack = 3.4f, TitleY = 4.45f, TitleScale = 1.0f,
        ColX = 1.75f, ColY = 3.3f, ColScale = 1f, ColFont = 1.9f, PanelW = 3.0f, PanelH = 0.95f, StageR = 2.5f, CamDist = 7.0f;

    // 2026-11-18 てじゃがエディタで手で並べた配置（「いったんこれでためす」「わるくなさそう」）。root から見た local（Board の子は Board から、キノコは FairyRing から）。
    //  UseHandLayout=true の間は、自動の並べ方・縮め方のあとにこの値で上書きする
    public static bool UseHandLayout = true;
    static readonly (string path, Vector3 pos, Vector3 euler, Vector3 scale)[] HandLayout = {
        ("Board", new Vector3(0f, -0.62f, 4.899994f), Vector3.zero, new Vector3(0.7f, 0.7f, 0.7f)),
        ("Board/Title", new Vector3(0f, 5.09f, -1.43f), Vector3.zero, new Vector3(2f, 2f, 2f)),
        ("Board/Times1", new Vector3(-2.43f, 2.54f, -3.12f), new Vector3(0f, 312.2557f, 0f), Vector3.one),
        ("Board/Times2", new Vector3(2.23f, 2.52600527f, -3.04f), new Vector3(0f, 44.2630539f, 0f), Vector3.one),
        ("FairyRing/Mush0", new Vector3(-2.362f, -0.047f, 0.467f), new Vector3(352.655457f, 146.4802f, 357.214844f), Vector3.one * 0.15f),
        ("FairyRing/Mush1", new Vector3(-3.054f, -0.018f, 1.993f), new Vector3(5.750139f, 190.43129f, 358.625336f), Vector3.one * 0.261761874f),
        ("FairyRing/Mush2", new Vector3(-3.012329f, -0.120002747f, 3.47380066f), new Vector3(5.932863f, 17.08477f, 356.654846f), Vector3.one * 0.3926907f),
        ("FairyRing/Mush3", new Vector3(-2.11f, -0.13f, 5.19f), new Vector3(3.647875f, 48.99988f, 2.14669538f), Vector3.one * 0.242029339f),
        ("FairyRing/Mush4", new Vector3(0.057f, 0.003f, 5.899f), new Vector3(359.469849f, 93.81726f, 359.049835f), Vector3.one * 1.00292528f),
        ("FairyRing/Mush5", new Vector3(2.09614563f, -0.120002747f, 4.906769f), new Vector3(356.7245f, 190.813278f, 353.735138f), Vector3.one * 0.407284319f),
        ("FairyRing/Mush6", new Vector3(2.78622437f, -0.120002747f, 3.56419373f), new Vector3(6.41943169f, 217.716934f, 4.67472124f), Vector3.one * 0.180159524f),
        ("FairyRing/Mush7", new Vector3(3.024f, -0.04f, 1.952f), new Vector3(2.621931f, 208.9491f, 3.99743962f), Vector3.one * 0.3f),
        ("FairyRing/Mush8", new Vector3(2.374f, -0.047f, 0.626f), new Vector3(2.949647f, 331.0045f, 5.557061f), Vector3.one * 0.1446052f),
    };
    // 2026-11-18 てじゃ「Pickupのこっててもおもろいかも」「全員にみえていいよ」：文字・ステージ1・ステージ2のボードを、みんなで持って動かせるように。
    //  同期する物は隠れるエリアに置けない（入り直した人に見えない罠）ので FuwaSystem/BonusBoards に移し、FuwaAreaShow で本島にいない間は見た目・当たり・持つのを止める
    // 2026-11-23 てじゃ「おまけ本島のボードとかのピックアップ判定の位置がちょっと変」「ボードとコングラッチレーションのやつ、同じくらいの大きさの判定用の透明いた」
    //  前は見た目の囲む箱（ワールドの向きの箱）を板の向きに直していたので、ななめに置いた板（Times2 は 44°）で当たりが横向きの細い板になっていた。
    //  文字（実行中に書きかわる）の大きさもあてにならない → 板（Panel）があれば板の大きさ、なければ文字の枠（TextMeshPro の表示範囲）で、板と同じ向き・厚み thick m の箱
    static void FitBoardCollider(Transform part, BoxCollider bc, float thick)
    {
        float tz = thick / Mathf.Max(0.01f, Mathf.Abs(part.lossyScale.z));
        var pan = part.Find("Panel"); var sr = pan ? pan.GetComponent<SpriteRenderer>() : null;
        if (sr)
        {
            Vector2 sz = sr.drawMode != SpriteDrawMode.Simple ? sr.size : (Vector2)sr.sprite.bounds.size;
            bc.center = pan.localPosition;
            bc.size = new Vector3(Mathf.Abs(sz.x * pan.localScale.x), Mathf.Abs(sz.y * pan.localScale.y), tz);
            return;
        }
        var tmp = part.GetComponent<TMPro.TextMeshPro>();
        if (tmp)
        {
            tmp.ForceMeshUpdate();
            var tb = tmp.textBounds;
            bc.center = new Vector3(tb.center.x, tb.center.y, 0f);
            bc.size = new Vector3(tb.size.x + 0.3f, tb.size.y + 0.3f, tz);   // 文字のまわりに少し余白
            return;
        }
    }
    [MenuItem("FuwaCourse/おまけ/ボードの持つ判定を板の大きさにそろえる")]
    static void FitBoardCollidersMenu()
    {
        var bb = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t && t.name == "BonusBoards" && t.gameObject.scene.IsValid());
        if (!bb) { Debug.LogError("BonusBoards がない"); return; }
        int n = 0;
        foreach (Transform part in bb) { var bc = part.GetComponent<BoxCollider>(); if (!bc || !part.GetComponent<TMPro.TextMeshPro>()) continue; Undo.RecordObject(bc, "board collider"); FitBoardCollider(part, bc, 0.15f); EditorUtility.SetDirty(bc); n++; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bb.gameObject.scene);
        Debug.Log("ボードの持つ判定をそろえた：" + n);
    }

    static string MakeSharedBoards(Transform root)
    {
        var sys = Find("FuwaSystem"); var bt = root.Find("Board");
        var av = Resources.FindObjectsOfTypeAll<FuwaAreaVisibility>().FirstOrDefault(x => x && x.gameObject.scene.IsValid());
        if (!sys || !bt || !av) return "みんなのボード：FuwaSystem／Board／エリア表示がない ";
        var holder = new GameObject("BonusBoards").transform; holder.SetParent(sys, false);
        int n = 0;
        foreach (Transform part in bt.Cast<Transform>().ToList())
        {
            var rs = part.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
            var wb = rs[0].bounds; foreach (var br in rs) wb.Encapsulate(br.bounds);
            part.SetParent(holder, true);   // 見た目の位置・大きさはそのまま
            var bc = part.gameObject.AddComponent<BoxCollider>();
            FitBoardCollider(part, bc, 0.15f);
            var rb = part.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var pu = part.gameObject.AddComponent<VRC.SDK3.Components.VRCPickup>();
            pu.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pu.InteractionText = part.name == "Title" ? "もじ" : "ボード"; pu.proximity = 3f; pu.allowManipulationWhenEquipped = true;
            part.gameObject.AddComponent<VRC.SDK3.Components.VRCObjectSync>();   // 離したらその場に浮く（重力なし・kinematic）
            part.gameObject.layer = 13;   // Pickup
            var sh = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaAreaShow>(part.gameObject);
            sh.areaVisibility = av; sh.area = 4; sh.renderers = rs; sh.colliders = new Collider[] { bc }; sh.pickup = pu; sh.board = part.GetComponent<FuwaThanksBoard>();
            UdonSharpEditorUtility.CopyProxyToUdon(sh); FuwaStage2WaterTest.NoSync(sh);
            n++;
        }
        Object.DestroyImmediate(bt.gameObject);   // 空になった Board
        // 金ピカふわふわ：台なしで大きめを床にドンと置く。みんなで持てる（2026-11-18 てじゃ「やや大きめの金ピカのふわふわをドンと置いておこう、台のやつなし、Pickupもできる」）
        //  元の台（GoldFluffStand）は次の作り直しの写し元に残して隠す
        var gs = root.Find("GoldFluffStand"); var gf0 = gs ? gs.Find("GoldFluff") : null;
        if (gf0)
        {
            // 2026-11-19 てじゃ「金ピカフワフワはやめて、ピックアップできるただのフワフワにしよう、USE で表情かえてポヨン、撮影用、離すと空中に止まる」
            // 大きさは3種類（2026-11-19 てじゃ「大きさは３種類おいとこ」）。ならべて置く
            for (int si = 0; si < PhotoFluffSizes.Length; si++)
            {
            float size = PhotoFluffSizes[si];
            var gf = Object.Instantiate(gf0.gameObject, holder, false).transform; gf.name = "PhotoFluff" + (si + 1);
            var mf = gf.GetComponent<MeshFilter>(); float meshD = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size.x : 1f;
            gf.localScale = Vector3.one * (size / meshD); gf.rotation = gs.rotation;
            var basePos = gs.position + gs.rotation * new Vector3(PhotoFluffSpread * (si - 1), 0f, 0f);
            gf.position = new Vector3(basePos.x, Pos.y + size * 0.5f + 0.05f, basePos.z);
            // 2026-11-19 てじゃ「ふわふわ置物各種、入ってきた場所のほう向かせて」：顔（+z）をスポーンの方へ
            { var sp = root.Find("Spawn"); if (sp) { var d = sp.position - gf.position; d.y = 0f; if (d.sqrMagnitude > 1e-4f) gf.rotation = Quaternion.LookRotation(d.normalized); } }
            var furMat = FuwaLobbyPaths.FindMat("Fuwa_BallFur"); var fr = gf.GetComponent<MeshRenderer>(); if (furMat && fr) fr.sharedMaterial = furMat;
            var rb = gf.GetComponent<Rigidbody>() ?? gf.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var pu = gf.GetComponent<VRC.SDK3.Components.VRCPickup>() ?? gf.gameObject.AddComponent<VRC.SDK3.Components.VRCPickup>();
            pu.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pu.proximity = 3f; pu.InteractionText = "ふわふわ"; pu.UseText = "ひょうじょう";
            if (!gf.GetComponent<VRC.SDK3.Components.VRCObjectSync>()) gf.gameObject.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            gf.gameObject.layer = 13;
            var pf = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPhotoFluff>(gf.gameObject);
            pf.fur = fr; pf.pickup = pu; pf.colliders = gf.GetComponentsInChildren<Collider>(true); pf.areaVisibility = av; pf.area = 4;
            UdonSharpEditorUtility.CopyProxyToUdon(pf);
            n++;
            }
            gs.gameObject.SetActive(false);
            n++;
        }
        return "みんなのボード " + n + " ";
    }

    // 2026-11-18 てじゃ「台の上に金ピカの銃が光って回ってて、手に取ると元の銃が台に写って手持ちが金ピカ…元の銃取ると戻る」：
    //  きんピカ台（GoldGunStand）を FuwaGoldGunDisplay に。見本の銃は台の上で回る銃（FuwaSystem/GunShowcase の Visual）の写し。
    //  あわせて、台の上で回る銃（GunShowcase）も金ピカに対応させる
    static string GoldGunDisplay(Transform root)
    {
        var gs = root.Find("GoldGunStand");
        var sc = Object.FindObjectsOfType<FuwaGunShowcase>(true).FirstOrDefault();
        var bl = Object.FindObjectsOfType<FuwaBlower>(true).FirstOrDefault(b => b.goldRenderers != null && b.goldRenderers.Length > 0);
        var mgr = Object.FindObjectsOfType<FuwaRespawnManager>(true).FirstOrDefault();
        if (!gs || !sc || !sc.shownVisual || !bl || !mgr) return "金の銃の台：材料がない ";
        // 名前 → 金色の材質
        var goldOf = new Dictionary<string, Material>();
        for (int i = 0; i < bl.goldRenderers.Length && i < bl.goldMats.Length; i++) if (bl.goldRenderers[i] && bl.goldMats[i]) goldOf[bl.goldRenderers[i].name] = bl.goldMats[i];
        // 台の上で回る銃を金ピカ対応に
        {
            var rs = sc.shownVisual.GetComponentsInChildren<Renderer>(true).Where(r => goldOf.ContainsKey(r.name)).ToArray();
            sc.goldRenderers = rs; sc.goldMats = rs.Select(r => goldOf[r.name]).ToArray();
            // 頂点の色でまとめた銃（FuwaGunMerge）：メッシュも金色の写しに差しかえる（名前でそろえる）
            var meshOf = new Dictionary<string, Mesh>();
            if (bl.goldMeshFilters != null && bl.goldMeshes != null)
                for (int i = 0; i < bl.goldMeshFilters.Length && i < bl.goldMeshes.Length; i++) if (bl.goldMeshFilters[i] && bl.goldMeshes[i]) meshOf[bl.goldMeshFilters[i].name] = bl.goldMeshes[i];
            var mfs = sc.shownVisual.GetComponentsInChildren<MeshFilter>(true).Where(f => meshOf.ContainsKey(f.name)).ToArray();
            sc.goldMeshFilters = mfs; sc.goldMeshes = mfs.Select(f => meshOf[f.name]).ToArray();
            var oldFx = sc.shownVisual.Find("GoldSparkle"); if (oldFx) Object.DestroyImmediate(oldFx.gameObject);
            if (bl.goldFx)
            {
                var fx = Object.Instantiate(bl.goldFx, sc.shownVisual, false); fx.name = "GoldSparkle";
                fx.transform.localPosition = bl.goldFx.transform.localPosition; fx.transform.localRotation = bl.goldFx.transform.localRotation; fx.transform.localScale = bl.goldFx.transform.localScale;
                fx.SetActive(false); sc.goldFx = fx;
                // キラキラが出る面は、見た目の銃の本体に付けかえる（写すと本物の銃の本体を指したままになる）
                var ps = fx.GetComponent<ParticleSystem>(); var body = sc.shownVisual.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(x => x.name == "GunBody");
                if (ps && body) { var shp = ps.shape; if (shp.shapeType == ParticleSystemShapeType.MeshRenderer) { shp.meshRenderer = body; fx.transform.localPosition = Vector3.zero; } }
            }
            UdonSharpEditorUtility.CopyProxyToUdon(sc); EditorUtility.SetDirty(sc);
        }
        // 前のスイッチ・見本を消す（台は作り直しのたびに引きつぐので）
        AudioSource click = null;
        foreach (var sw in gs.GetComponents<FuwaGoldGunSwitch>()) { click = sw.clickSound; var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw); Object.DestroyImmediate(sw); if (ub) Object.DestroyImmediate(ub); }
        foreach (var d0 in gs.GetComponents<FuwaGoldGunDisplay>()) { click = click ?? d0.clickSound; var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(d0); Object.DestroyImmediate(d0); if (ub) Object.DestroyImmediate(ub); }
        foreach (var nm in new[] { "Display", "GunSpot" }) { var o = gs.Find(nm); if (o) Object.DestroyImmediate(o.gameObject); }
        foreach (var nm in new[] { "Medal", "MedalRim" }) { var o = gs.Find(nm); if (o) o.gameObject.SetActive(false); }   // 金の銃の像に置きかえ
        { var o = gs.Find("Label"); if (o) { o.gameObject.SetActive(false); o.localPosition = new Vector3(0f, 0.6f, 0.24f); o.rotation = Quaternion.LookRotation(-gs.forward, Vector3.up); } }   // 柱の「きんピカ ON/OFF」（スイッチ）。島のまん中側から読めるように
        float topY = Pedestal(gs);
        var spot = new GameObject("GunSpot").transform; spot.SetParent(gs, false);
        spot.position = new Vector3(gs.position.x, topY + 0.01f, gs.position.z); spot.rotation = gs.rotation;
        var disp = new GameObject("Display").transform; disp.SetParent(gs, false);
        disp.position = spot.position + Vector3.up * bl.dockHover; disp.rotation = spot.rotation;
        disp.localScale = Vector3.one * (sc.spin ? sc.spin.lossyScale.x : 1.4f) / gs.lossyScale.x;   // 台の上で回る銃と同じ大きさ
        var spin = new GameObject("Spin").transform; spin.SetParent(disp, false);
        GameObject Model(string name, bool gold)
        {
            var g = Object.Instantiate(sc.shownVisual.gameObject, spin, false); g.name = name;
            g.transform.localPosition = sc.shownVisual.localPosition; g.transform.localRotation = sc.shownVisual.localRotation; g.transform.localScale = sc.shownVisual.localScale;
            FuwaStage2Builder.StripBehaviour(g);
            var fx = g.transform.Find("GoldSparkle"); if (fx) fx.gameObject.SetActive(gold);
            if (gold) foreach (var r in g.GetComponentsInChildren<Renderer>(true)) if (goldOf.TryGetValue(r.name, out var m)) r.sharedMaterial = m;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.lightmapIndex = -1; }
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return g;
        }
        var goldM = Model("GoldModel", true); var normM = Model("NormalModel", false); normM.SetActive(false);   // 像は「クリックするとこうなる」色（FuwaGoldGunSwitch）
        // 2026-11-18 てじゃ「わかりづらいか、やっぱただの固定の像みたいに飾って、下にオンオフスイッチ式に」：像は金ピカで固定（回さない）、台のクリックでオン／オフ
        spin.localRotation = Quaternion.Euler(0f, StatueYaw, 0f) * Quaternion.Euler(sc ? sc.tilt : Vector3.zero);
        // うしろの大きい光（後光）はなし（2026-11-19 てじゃ「金銃台のデカい後光みたいなのいらない」）
        GameObject spk = null;
        if (sc.sparkles) { var sp = Object.Instantiate(sc.sparkles, disp, false); sp.name = "Sparkles"; sp.transform.localPosition = sc.sparkles.transform.localPosition; sp.SetActive(true); spk = sp; }
        Object.DestroyImmediate(spot.gameObject);   // 銃は呼ばない
        var gsw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaGoldGunSwitch>(gs.gameObject);
        gsw.manager = mgr; gsw.clickSound = click; gsw.label = null; gsw.goldModel = goldM; gsw.normalModel = normM; gsw.sparkles = spk; gsw.language = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(x => x.gameObject.scene.IsValid());   // 2026-11-19 てじゃ「台に通常銃が乗ってるときはキラキラ消そう」   // 文字なし（てじゃ「スイッチは分かりづらい」）
        UdonSharpEditorUtility.CopyProxyToUdon(gsw); FuwaStage2WaterTest.NoSync(gsw);
        return "金の銃の像＋スイッチ・回る銃の金ピカ " + sc.goldRenderers.Length + " ";
    }

    // 2026-11-19 てじゃ「金の銃の台をちょっとちゃんとつくろ」→ B（Blender で新しく）：段つきの丸い台座に金の縁と帯、柱に金のぷっくり星、足元に雲
    //  Blender：FLUFF_models.blend の GoldStand シーン、goldstand_build.py（形）／goldstand_export.py（Tools/goldstand.json へ）。材質は s0 台座・s1 金・s2 雲
    //  前の円柱3つ（Base・Pillar・Top）は消さずに隠す。天板の上の高さを返す（ワールド）
    public static Color PedestalBody = new Color(0.80f, 0.80f, 0.95f), PedestalCloud = new Color(1f, 1f, 1f);
    static float Pedestal(Transform gs)
    {
        var mesh = ImportJsonMeshes("goldstand.json", "Assets/FuwaStage2/Generated/GoldStand").FirstOrDefault();
        var old = gs.Find("Pedestal"); if (old) Object.DestroyImmediate(old.gameObject);
        if (!mesh) { var top = gs.Find("Top"); return top ? top.GetComponent<Renderer>().bounds.max.y : gs.position.y + 0.95f * gs.lossyScale.y; }
        foreach (var nm in new[] { "Base", "Pillar", "Top" }) { var o = gs.Find(nm); if (o) o.gameObject.SetActive(false); }
        var p = new GameObject("Pedestal"); p.transform.SetParent(gs, false); p.isStatic = false;
        p.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = p.AddComponent<MeshRenderer>();
        var gold = AssetDatabase.FindAssets("Fuwa_Blower_Gold t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m && m.name == "Fuwa_Blower_Gold");
        var mats = new List<Material> { BonusMat("GoldStand_Body", PedestalBody), gold ? gold : BonusMat("GoldStand_Gold", new Color(0.98f, 0.78f, 0.3f)) };
        if (mesh.GetTriangles(2).Length > 0) mats.Add(BonusMat("GoldStand_Cloud", PedestalCloud));   // 2026-11-19 てじゃ「足元のもこもこいらない、もうちょい細めに、星もなし」→ 雲・星なし、太さ 0.8 倍（Blender の params）
        mr.sharedMaterials = mats.ToArray();
        return gs.TransformPoint(new Vector3(0f, mesh.bounds.max.y, 0f)).y;
    }

    // 2026-11-18 てじゃ「おまけ本島の隅っこに、各コースの隠し記録的な感じで閲覧する場所つくろ」：FuwaSystem/Stats（FuwaStats、数える係）と、本島の西の柵ぎわの看板（FuwaStatsBoard）
    // 2026-11-19 てじゃ「あしあとは金ピカ銃の右くらい」：東のきんピカ台（局所 (8.4, -1.6)）の右（南）へ。読む向きは島のまん中から（東を向く）
    // 2026-11-19 てじゃ「ボタンとかボード、ちゃんと柵基準で」：柵（局所の半径 FenceR）の内側に沿わせて、島のまん中を向ける。角度は +x（東）から +z（北）へ測る
    //  → 島を小さくした（IslandXZ）ので、半径は柵から、角度は金の台の向きと柵ぞいの長さ（m、縮める前）から決める（前の値：ボタン -20°、看板 -34°、リセット 10.8°）
    //  → 同日「ボタンをちょい右にずらして、ボードはボタンの上にだすようにしよ」：ボタンは StatsBtnArc（1.77→2.4）、看板はボタンと同じ向きで上に（下のはしがボタンの上 StatsAboveGap）
    public static float FenceR0 = 11.37f, BtnIn = 0.37f, StatsIn = 0.52f, StatsBtnArc = 2.4f, StatsAboveGap = 0.15f;
    public static float FenceR => FenceR0 * IslandXZ;
    public static float BtnR => FenceR - BtnIn;
    public static float StatsR => FenceR - StatsIn;
    static float GoldDeg => Mathf.Atan2(1.6f, GoldX) * Mathf.Rad2Deg;   // 金ふわふわの台の向き（金の銃の台はその逆）
    public static float ResetDeg => GoldDeg;                                          // まん中のふわふわ（金ふわふわの台）の奥
    public static float StatsBtnDeg => -GoldDeg - StatsBtnArc / BtnR * Mathf.Rad2Deg;   // 金の銃の台の右
    public static float StatsDeg => StatsBtnDeg;   // あしあとボタンの上に看板
    public static Vector3 OnFence(float r, float deg, float y) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
    static Quaternion FaceOut(Vector3 p) { var o = new Vector3(p.x, 0f, p.z); return o.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(o.normalized) : Quaternion.identity; }   // 読む人は島のまん中から
    // 2026-11-19 てじゃ「あしあとボード、もうちょいコンパクトにしたい、あとちょっと上に」「余白減らす感じで」：板の余白をへらす（StatsPanelW/H）、まん中の高さ StatsY。StatsScale は板ごとの倍率（今は1）
    public static float StatsScale = 1f;
    public static float StatsY => BtnY + BtnHalfH + StatsAboveGap + StatsPanelH * 0.5f;   // ボタンの上（BtnHalfH：ボタンの高さの半分）
    public static float BtnHalfH = 0.2f, StatsPanelW = 3.0f, StatsPanelH = 2.65f, StatsFont = 1.25f;   // 縮める前の島の座標（西が -x）
    // 2026-11-19 てじゃ「あしあと、もうちょい幅狭められないかな」「英語コース名注意」：列を詰めて幅をへらす。名前の列は英語の一番長い名前（Mushroom Valley ≒1.0）も入る幅を残す
    //  文字の幅（文字の大きさ1.25）：名前 1.0、さいしょう 0.42（80%）、ノーミス 0.44、999999 0.42、ふわふわが進んだきょり 1.21
    //  → 同日「全部英語版もほしいな」「それもとに幅も調整しよ」：言葉ごとに列の位置（左はしから・島の座標）と欄の幅を決める。板の幅は欄＋0.4（FuwaStatsBoard が切りかえ）
    //    日本語：名前 0.75、ノーミス 0.44／英語：名前 1.01（Mushroom Valley）、No Miss 0.47、Distance fluffs traveled 1.46。列のすき間 0.12
    public static float[] StatsColsJa = { 0.30f, 1.17f, 1.73f, 2.27f }, StatsColsEn = { 0.30f, 1.43f, 2.02f, 2.56f };
    public static float StatsTextW = 2.6f, StatsTextWEn = 2.9f;   // 板の幅 StatsPanelW は大きい方＋0.4（作った時は日本語）
    static string Pct(float x, float w) => (x / w * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    public static void SetStatsCols(FuwaStatsBoard sb, SpriteRenderer panel)
    {
        float w = StatsTextW, we = StatsTextWEn; var j = StatsColsJa; var e = StatsColsEn;
        sb.namePos = Pct(j[0], w); sb.outPos = Pct(j[1], w); sb.hitPos = Pct(j[2], w); sb.clearPos = Pct(j[3], w);
        sb.namePosEn = Pct(e[0], we); sb.outPosEn = Pct(e[1], we); sb.hitPosEn = Pct(e[2], we); sb.clearPosEn = Pct(e[3], we);
        sb.textW = w; sb.textWEn = we; sb.panelPad = 0.4f; sb.panel = panel; sb.textRect = sb.text ? sb.text.rectTransform : null;
    }
    // 2026-11-19 てじゃ「見づらいから、極わずかに行間あけつつ、行ごとに色を交互につけよう」：行間 StatsLineGap、コースの行に1行おきに角丸の帯（板と文字の間）
    //  （TMP の <mark> は文字の上にかぶって文字がうすくなるので、帯は板と同じ角丸の絵で別に置く）
    public static float StatsLineGap = 12f, StripeScale = 0.3f, StripePad = 0.08f, StripeZ = 0.015f;
    public static Color StripeColor = new Color(0.99f, 0.87f, 0.72f, 0.75f);
    public static SpriteRenderer[] StatsStripes(Transform b, TMPro.TextMeshPro tmp, SpriteRenderer panel)
    {
        foreach (var old in b.Cast<Transform>().Where(c => c.name.StartsWith("Stripe")).ToArray()) Object.DestroyImmediate(old.gameObject);
        if (!panel) return new SpriteRenderer[0];
        // 本番と同じ並び（タイトル・見出し2行・コース10行・空行・2行）の見本で行の位置をはかる
        string keep = tmp.text;
        string smp = "<size=125%>あ</size>\n<size=80%>あ\nあ</size>";
        for (int i = 0; i < 10; i++) smp += "\n1–1 あ";
        tmp.text = smp + "\n\nあ\nあ"; tmp.ForceMeshUpdate();
        var li = tmp.textInfo.lineInfo; var list = new List<SpriteRenderer>();
        // 帯の材質：板（Queue 2990、深度を書く）の後・文字（3000）の前に描く。同じ材質だと距離の順で帯が先に描かれ、板が帯の所だけ描かれず空が透けた
        const string stripeMatPath = "Assets/FuwaCourse/Materials/PanelSpriteStripe.mat";
        var smat = AssetDatabase.LoadAssetAtPath<Material>(stripeMatPath);
        if (!smat) { smat = new Material(panel.sharedMaterial) { name = "PanelSpriteStripe" }; AssetDatabase.CreateAsset(smat, stripeMatPath); }
        smat.renderQueue = 2995; EditorUtility.SetDirty(smat);
        float w = tmp.rectTransform.sizeDelta.x + StripePad * 2f;
        for (int r = 0; r < 10; r += 2)
        {
            int l = 3 + r;
            float top = (li[l - 1].descender + li[l].ascender) * 0.5f, bot = (li[l].descender + li[l + 1].ascender) * 0.5f;
            var g = new GameObject("Stripe" + r / 2); g.transform.SetParent(b, false);
            g.transform.localPosition = new Vector3(0f, (top + bot) * 0.5f, StripeZ); g.transform.localScale = new Vector3(StripeScale, StripeScale, 1f);
            var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = panel.sprite; sr.sharedMaterial = smat; sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w / StripeScale, (top - bot) / StripeScale); sr.color = StripeColor; sr.sortingOrder = panel.sortingOrder;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
            list.Add(sr);
        }
        tmp.text = keep; tmp.ForceMeshUpdate();
        return list.ToArray();
    }
    static string StatsBoard(Transform root)
    {
        var sys = Find("FuwaSystem"); var src = root.Find("Board/Times1");
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(b => b.records != null);
        if (!sys || !src || !ball) return "あしあと：材料がない ";
        var recs = Object.FindObjectsOfType<FuwaRecords>(true).Where(x => x.name == "Records" || x.name == "Records2").OrderBy(x => x.name).ToArray();
        var stT = sys.Find("Stats"); FuwaStats st = stT ? stT.GetComponent<FuwaStats>() : null;
        if (!st) { var g = new GameObject("Stats"); g.transform.SetParent(sys, false); st = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaStats>(g); }
        st.ball = ball; st.records = recs; st.savePlayerData = recs.Length > 0 && recs[0].savePlayerData;   // 自己ベストの保存と同じ時に
        UdonSharpEditorUtility.CopyProxyToUdon(st); FuwaStage2WaterTest.NoSync(st);
        var bso = new SerializedObject(ball); bso.FindProperty("stats").objectReferenceValue = st; bso.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(ball);
        // 看板（自己ベストの板を写して大きく）
        var b = Object.Instantiate(src.gameObject, root, false).transform; b.name = "StatsBoard";
        foreach (var tb in b.GetComponents<FuwaThanksBoard>()) { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tb); Object.DestroyImmediate(tb); if (ub) Object.DestroyImmediate(ub); }
        b.localPosition = OnFence(StatsR, StatsDeg, StatsY); b.localRotation = FaceOut(b.localPosition); b.localScale = Vector3.one * StatsScale;   // 島のまん中から読める（文字の正面＝+z が西向き）
        var tmp = b.GetComponent<TMPro.TextMeshPro>(); tmp.rectTransform.sizeDelta = new Vector2(StatsTextW, StatsPanelH - 0.2f); tmp.fontSize = StatsFont;   // 幅はこの後 FuwaStatsBoard が言葉で切りかえ（2026-11-19 コメントの後ろに入って文字の大きさが Times1 の 1.9 のままになっていた）
        tmp.alignment = TMPro.TextAlignmentOptions.Left; tmp.enableWordWrapping = false; tmp.margin = Vector4.zero;
        var panel = b.Find("Panel")?.GetComponent<SpriteRenderer>();
        if (panel && panel.drawMode != SpriteDrawMode.Simple)
        {
            var ps = panel.transform.localScale; panel.size = new Vector2(StatsPanelW / ps.x, StatsPanelH / ps.y);   // 板の拡大率の分を割る
            panel.transform.localPosition = new Vector3(0f, 0f, panel.transform.localPosition.z);
        }
        var sb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaStatsBoard>(b.gameObject); sb.stats = st; sb.text = tmp; sb.language = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(x => x.gameObject.scene.IsValid());
        tmp.lineSpacing = StatsLineGap;
        SetStatsCols(sb, panel); sb.stripes = StatsStripes(b, tmp, panel); sb.stripePad = StripePad;
        UdonSharpEditorUtility.CopyProxyToUdon(sb); FuwaStage2WaterTest.NoSync(sb);
        tmp.text = "あしあと";
        return "あしあと ";
    }

    // 2026-11-19 てじゃ「ボードとかの位置リセットボタンもほしいのと、かくしデータもボタンで展開するタイプにしよ」
    //  → 同日「オブジェクトリセットは、ふわふわの奥くらいにレーザーボタン式にしよ」「あしあとは金ピカ銃の右くらい、これもレーザーボタン」
    //  あそびばの「おもちゃをもどす」（柵の前にうかぶ丸いレーザーボタン）を写して使う。縮める前の島の座標、どちらも島のまん中から押す（東を向く）
    public static float BtnY = 1.09f;   // ResetDeg＝まん中のふわふわの奥、StatsBtnDeg＝あしあとの看板の左（てじゃ「あしあとボタンはボードの左側に」）
    public static float LaserBtnScale = 0.97f;   // あそびばの物（0.63）を本島の縮み（0.65）で割った大きさ＝見た目は同じ
    static string Buttons(Transform root)
    {
        var holder = Find("FuwaSystem/BonusBoards"); var stats = root.Find("StatsBoard");
        var src = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t && t.name == "ResetToysButton" && t.gameObject.scene.IsValid());
        if (!src) return "ボタン：見本（あそびばの ResetToysButton）がない ";
        foreach (var nm in new[] { "ResetButton", "StatsButton" }) { var old = root.Find(nm); if (old) Object.DestroyImmediate(old.gameObject); }
        (Transform, TMPro.TextMeshProUGUI) Make(string name, Vector3 lp, Color col, string text)
        {
            var b = Object.Instantiate(src.gameObject, root, false).transform; b.name = name;
            b.localPosition = lp; b.localRotation = FaceOut(lp); b.localScale = Vector3.one * LaserBtnScale;
            var btn = b.GetComponentInChildren<UnityEngine.UI.Button>(true);
            var cb = btn.colors; float h, sat, v; Color.RGBToHSV(cb.normalColor, out h, out sat, out v);
            Color.RGBToHSV(col, out float h2, out float s2, out float v2);
            cb.highlightedColor = Color.HSVToRGB(h2, s2, Mathf.Min(1f, v2 * 1.08f)); cb.pressedColor = Color.HSVToRGB(h2, Mathf.Min(1f, s2 * 1.2f), v2 * 0.85f); cb.selectedColor = col; cb.normalColor = col;
            btn.colors = cb;
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
            var tm = b.GetComponentInChildren<TMPro.TextMeshProUGUI>(true); if (tm) tm.text = text;
            return (b, tm);
        }
        void Hook(Transform b, UdonSharp.UdonSharpBehaviour u)
        {
            var btn = b.GetComponentInChildren<UnityEngine.UI.Button>(true); var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(u);
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(btn.onClick, ub.SendCustomEvent, "Press");
            EditorUtility.SetDirty(btn);
        }
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(x => x.gameObject.scene.IsValid());   // 2026-11-19 てじゃ「全部英語版もほしいな」
        int n = 0;
        if (holder)
        {
            var (b, tm) = Make("ResetButton", OnFence(BtnR, ResetDeg, BtnY), new Color(0.45f, 0.72f, 0.95f), "もとにもどす");
            var fb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBonusButton>(b.gameObject);
            fb.mode = 0; fb.resetObjects = holder.Cast<Transform>().Select(t => t.gameObject).ToArray();
            fb.label = tm; fb.language = lang; fb.openText = fb.closeText = "もとにもどす"; fb.openTextEn = fb.closeTextEn = "Reset";
            UdonSharpEditorUtility.CopyProxyToUdon(fb); FuwaStage2WaterTest.NoSync(fb); Hook(b, fb);
            n++;
        }
        if (stats)
        {
            var (b, tm) = Make("StatsButton", OnFence(BtnR, StatsBtnDeg, BtnY), new Color(0.96f, 0.68f, 0.30f), "あしあと");
            var fb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBonusButton>(b.gameObject);
            fb.mode = 1; fb.toggleTarget = stats.gameObject; fb.label = tm; fb.openText = fb.closeText = "あしあと";   // てじゃ「ボタン名は『あしあと』だけでいいよ」
            fb.language = lang; fb.openTextEn = fb.closeTextEn = "Footprints";
            fb.toggleAnim = stats.GetComponent<FuwaStatsBoard>();   // 閉じる時のアニメ（2026-11-19）
            UdonSharpEditorUtility.CopyProxyToUdon(fb); FuwaStage2WaterTest.NoSync(fb); Hook(b, fb);
            n++;
        }
        return "ボタン " + n + " ";
    }

    // 2026-11-19 てじゃ「さっき埋めた柵のとこ、やっぱまた開けて橋と島をつくってほしい、ワープポータルおいて、移動先はタイトル、ただし向きは後ろ側（ワールドポータルとか置いてある島の向き）」
    //  西の柵の切れ目（ロビーと同じ Open 版の柵）から橋 → 小島（本島の島の小さい写し、出入り口を本島に向ける）→ まん中にタイトル行きのワープ
    public static bool WestBridge = true;
    public static float WestGap = 4f, WestScale = 0.3f, WestBridgeW = 2.2f, WestPush = 1.3f;   // WestPush：橋が本島に入りこみすぎないよう、小島を外へ（m）
    static string WestIsland(Transform root, Transform reward)
    {
        var isl = root.Find("Island"); if (!isl) return "西の島：島がない ";
        float R = 8f * Scale * IslandXZ, r = R * WestScale;
        // 柵の切れ目の向きを当たりで探す（ロビーの Open 版の柵は西が少しずれて開いている）
        float gapDeg = 180f;
        {
            Physics.SyncTransforms();
            var fcol = isl.Find("FenceCollider") ? isl.Find("FenceCollider").GetComponent<Collider>() : null;
            if (fcol)
            {
                var open = new List<float>();
                for (float a = 120f; a <= 240f; a += 0.5f)
                {
                    var dir = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                    var o = root.position + Vector3.up * 0.4f;
                    if (!fcol.Raycast(new Ray(o, dir), out var hit, R * 1.5f)) open.Add(a);
                }
                if (open.Count > 0) gapDeg = open.Average();
            }
        }
        var gdir = new Vector3(Mathf.Cos(gapDeg * Mathf.Deg2Rad), 0f, Mathf.Sin(gapDeg * Mathf.Deg2Rad));
        // 2026-11-19 てじゃ「スタート島のロビー帰還島の大きさのやつでいいよ」「そのままもてきてもいいかも」：
        //  2–4 のスタートにある「ロビーへ帰るワープの小島」（WarpGround・下の岩・木の橋・柵）をそのまま写す。柵はスタートの島とひとつながりなので、小島と橋のまわりだけ切り出す。
        //  本島は最後に Shrink 倍するので、写す物は 1/Shrink 倍して元の大きさのままにする
        var c24 = Find("FuwaCourse2_4"); var sst = c24 ? c24.Find("Start") : null; var sa = sst ? sst.Find("StartArea") : null;
        var warpSrc = sst ? sst.Find("WarpToLobby") : null; var groundSrc = sa ? sa.Find("Ground") : null;
        if (!sa || !warpSrc || !groundSrc) return "西の島：2–4 のスタートがない ";
        Vector3 wc0 = warpSrc.position; Vector3 gc0 = groundSrc.GetComponent<Renderer>().bounds.center;
        Vector3 toMain = gc0 - wc0; toMain.y = 0f; float centerDist = toMain.magnitude; toMain /= centerDist;
        float groundR = groundSrc.GetComponent<Renderer>().bounds.extents.x;
        float reach = centerDist - groundR;   // 小島の中心から、橋がスタートの島のふちに着くまで
        float inv = 1f / Shrink;
        float cd = R + (reach + WestPush) * inv; var cpos = gdir * cd;
        var w = new GameObject("WestIsland").transform; w.SetParent(root, false); w.localPosition = cpos;
        w.localRotation = Quaternion.FromToRotation(toMain, -gdir); w.localScale = Vector3.one * inv;
        foreach (var nm in new[] { "WarpGround", "IslandUnder_Warp", "BridgeWood", "Bridge" })
        {
            var src = sa.Find(nm); if (!src) continue;
            var g = Object.Instantiate(src.gameObject, w, true); g.name = nm;
            FuwaStage2Builder.StripBehaviour(g);
            var lp = src.position - wc0; g.transform.localPosition = lp; g.transform.localRotation = src.rotation; g.transform.localScale = src.lossyScale;
            foreach (var rr in g.GetComponentsInChildren<Renderer>(true)) rr.lightmapIndex = -1;
            if (nm == "WarpGround")   // 床の色は本島の芝にそろえる
            { var fl = isl.Find("LobbyFloor"); var fm = fl ? fl.GetComponent<Renderer>().sharedMaterial : null; if (fm) foreach (var rr in g.GetComponentsInChildren<Renderer>(true)) { var ms = rr.sharedMaterials; for (int q = 0; q < ms.Length; q++) ms[q] = fm; rr.sharedMaterials = ms; } }
        }
        // 柵の切り出し：小島（ワープの島の半径＋少し）と橋のまわりの三角形だけ
        var fsrc = sa.Find("Fence");
        if (fsrc)
        {
            var wgR = sa.Find("WarpGround") ? sa.Find("WarpGround").GetComponent<Renderer>().bounds.extents.x + 0.6f : 3f;
            var bwB = sa.Find("BridgeWood") ? sa.Find("BridgeWood").GetComponent<Renderer>().bounds : new Bounds(wc0, Vector3.zero); bwB.Expand(new Vector3(0.3f, 2f, 0.3f));
            Mesh Cut(Mesh m, Matrix4x4 l2w, string name)
            {
                var vs = m.vertices; var nsrc = m.normals; var uvs = m.uv; var outV = new List<Vector3>(); var outN = new List<Vector3>(); var outUV = new List<Vector2>(); var outT = new List<int>(); var map = new Dictionary<int, int>();
                var tris = m.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 cc = (l2w.MultiplyPoint3x4(vs[tris[i]]) + l2w.MultiplyPoint3x4(vs[tris[i + 1]]) + l2w.MultiplyPoint3x4(vs[tris[i + 2]])) / 3f;
                    var hd = cc - wc0; hd.y = 0f;
                    bool onMainSide = Vector3.Dot(hd, toMain) > reach - 0.1f;   // スタートの島の方は入れない
                    if (onMainSide || !(hd.magnitude < wgR || bwB.Contains(cc))) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int vi = tris[i + k];
                        if (!map.TryGetValue(vi, out int ni)) { ni = outV.Count; map[vi] = ni; outV.Add(l2w.MultiplyPoint3x4(vs[vi]) - wc0); outN.Add(nsrc.Length == vs.Length ? l2w.MultiplyVector(nsrc[vi]).normalized : Vector3.up); outUV.Add(uvs.Length == vs.Length ? uvs[vi] : Vector2.zero); }
                        outT.Add(ni);
                    }
                }
                string ap = "Assets/FuwaStage2/Generated/BonusHub_" + name + ".asset";
                var mm = AssetDatabase.LoadAssetAtPath<Mesh>(ap); bool isNew = !mm; if (isNew) mm = new Mesh(); else mm.Clear();
                mm.name = name; mm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; mm.SetVertices(outV); mm.SetNormals(outN); mm.SetUVs(0, outUV); mm.SetTriangles(outT, 0); mm.RecalculateBounds();
                if (isNew) AssetDatabase.CreateAsset(mm, ap); else EditorUtility.SetDirty(mm);
                return mm;
            }
            var fmf = fsrc.GetComponent<MeshFilter>();
            if (fmf && fmf.sharedMesh)
            {
                var fm = Cut(fmf.sharedMesh, fsrc.localToWorldMatrix, "WestFence");
                var fg = new GameObject("Fence", typeof(MeshFilter), typeof(MeshRenderer)).transform; fg.SetParent(w, false);
                fg.GetComponent<MeshFilter>().sharedMesh = fm; fg.GetComponent<MeshRenderer>().sharedMaterials = fsrc.GetComponent<MeshRenderer>().sharedMaterials;
                fg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fmc = fsrc.GetComponent<MeshCollider>();
                if (fmc && fmc.sharedMesh) { var cm = Cut(fmc.sharedMesh, fsrc.localToWorldMatrix, "WestFenceCol"); var mc = fg.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = cm; }
            }
            // 柵の当たりが別の物にあれば（FenceCollider）それも切り出す
            var fcs = sa.Find("FenceCollider");
            if (fcs && fcs.GetComponent<MeshCollider>() && fcs.GetComponent<MeshCollider>().sharedMesh)
            { var cm = Cut(fcs.GetComponent<MeshCollider>().sharedMesh, fcs.localToWorldMatrix, "WestFenceCol2"); var cg = new GameObject("FenceCollider").transform; cg.SetParent(w, false); cg.gameObject.AddComponent<MeshCollider>().sharedMesh = cm; }
        }
        // タイトル行きのワープ（見た目と仕組みは戻りポータルと同じ）
        var st = reward.Find("Start"); var trig = st ? st.Find("WarpToLobby_Trigger") : null; var gauge = st ? st.Find("WarpToLobby_Trigger_Gauge") : null; var refT = st ? st.Find("WarpToLobby") : null;
        if (!trig || !gauge) return "西の島：ワープの見本がない ";
        var portal = new GameObject("ToTitle").transform; portal.SetParent(root, false); portal.localPosition = cpos + Vector3.up * 0.03f; portal.localRotation = Quaternion.LookRotation(gdir);
        foreach (var v in st.Cast<Transform>().Where(t => t.name.StartsWith("WarpToLobby") && !t.name.EndsWith("_Trigger") && !t.name.EndsWith("_Trigger_Gauge")).ToList())
        {
            var g = Object.Instantiate(v.gameObject, portal, false); g.name = v.name.Replace("WarpToLobby", "WarpToTitle");
            g.transform.localPosition = v.localPosition - (refT ? refT.localPosition : Vector3.zero); g.transform.localRotation = v.localRotation; g.transform.localScale = v.localScale;
            FuwaStage2Builder.StripBehaviour(g);
        }
        // 着く所：タイトルのスポーンで、うしろ（ワールドポータルの島の方）を向く
        var hubSpawn = UnityEngine.Object.FindObjectsOfType<VRC.SDK3.Components.VRCSceneDescriptor>(true).First().spawns[0];
        // 本島は最後に縮めるので、着く所は本島の外（入口の島の下）に置く
        var hubRoot = Find("FuwaHub");
        var spotOld = hubRoot ? hubRoot.Find("TitleSpot_Back") : null; if (spotOld) Object.DestroyImmediate(spotOld.gameObject);
        var spot = new GameObject("TitleSpot_Back").transform; spot.SetParent(hubRoot ? hubRoot : root, false);
        spot.SetPositionAndRotation(hubSpawn.position, Quaternion.LookRotation(Vector3.back));
        var wc = (FuwaCourseWarp)FuwaStage2Builder.MakeWarp(portal, "WarpToTitle", trig.gameObject, gauge.gameObject, null, spot, null, 0);
        foreach (var nm in new[] { "WarpToTitle_Trigger", "WarpToTitle_Trigger_Gauge" })
        { var c2 = portal.Find(nm); var s2 = st.Find(nm.Replace("WarpToTitle", "WarpToLobby")); if (c2 && s2) c2.localPosition = s2.localPosition - (refT ? refT.localPosition : Vector3.zero); }
        // 2026-11-19 てじゃ「タイトル戻りワープ、もどってもBGMがラストBGMのまま」：BGMはふわふわの居場所（startPoint）で決まるので、
        //  ワールドに入った時と同じ居場所（ステージ1ロビーの BallPark、ステージ1）に戻す
        var lobbyPark = Find("FuwaLobby") ? Find("FuwaLobby").transform.Find("BallPark") : null;
        var so = new SerializedObject(wc); so.FindProperty("area").intValue = 0; so.FindProperty("stage").intValue = 1; so.FindProperty("homeAction").intValue = 2; so.FindProperty("timed").boolValue = false;
        so.FindProperty("courseStart").objectReferenceValue = lobbyPark;
        so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(wc);
        foreach (var rr in portal.GetComponentsInChildren<Renderer>(true)) rr.lightmapIndex = -1;
        return "西の島・橋・タイトル行きワープ（切れ目 " + gapDeg.ToString("F0") + "°） ";
    }

    // 2026-11-19 てじゃ「柵のつなぎ目が甘いのと、木の橋の繫いだ柵だけ形やたかさがちがう」「この辺本島こみできっちり作り直して」：
    //  本島（ロビーの柵を横に縮めた物）・橋と小島（2–4 のスタートの柵の切り出し）・つなぎ目（四角い棒）の3種類をやめて、
    //  本島の輪 → 橋の片側 → 小島の輪 → 橋の反対側 → 本島、とひとつながりの道にそって同じ形の柵（あそびばと同じ寸法：柱 0.11×0.6、横木 0.22/0.52）を作る。
    //  当たりは柱の間ごとの壁（高さ PathFenceColH）。前の柵（Island/Fence・FenceCollider、WestIsland/Fence・FenceCollider）は隠す（当たりの切れ目さがしには使うので消さない）
    public static bool UsePathFence = true, ReturnPortal = false;
    public static float PathFencePostW = 0.11f, PathFencePostH = 0.6f, PathFenceRailW = 0.06f, PathFenceRailH = 0.06f, PathFenceSpacing = 1.0f, PathFenceColH = 1.0f;
    public static float[] PathFenceRailY = { 0.22f, 0.52f };
    public static float PathFenceBridgeIn = 0.08f, PathFenceIsletIn = 0.14f;
    static string PathFence(Transform root)
    {
        var isl = root.Find("Island"); var wi = root.Find("WestIsland");
        var floor = isl ? isl.Find("LobbyFloor") : null; var bridge = wi ? wi.Find("Bridge") : null; var wg = wi ? wi.Find("WarpGround") : null;
        if (!floor || !bridge || !wg) return "柵：材料がない ";
        var oldJoin = root.Find("WestFenceJoin"); if (oldJoin) Object.DestroyImmediate(oldJoin.gameObject);
        Vector3 O = root.position; O.y = 0f;
        float R = FenceR * root.lossyScale.x;                                     // 本島の柵の半径（ワールド）
        float yMain = floor.position.y + floor.lossyScale.y;                     // 円柱は高さ 2 → 上の面は中心＋スケール
        float yBridge = bridge.position.y + bridge.lossyScale.y * 0.5f;          // 箱
        float yIslet = wg.position.y + wg.lossyScale.y;
        Vector3 Ci = wg.position; Ci.y = 0f;
        float ri = wg.lossyScale.x * 0.5f - PathFenceIsletIn;
        { var wr = wg.GetComponent<MeshFilter>(); if (wr && wr.sharedMesh) { var mb = wr.sharedMesh.bounds; ri = Mathf.Max(mb.extents.x, mb.extents.z) * wg.lossyScale.x - PathFenceIsletIn; } }
        Vector3 Bc = bridge.position; Bc.y = 0f;
        Vector3 a = O - Ci; a.y = 0f; a.Normalize();                              // 橋の向き（小島 → 本島）
        Vector3 sd = Vector3.Cross(Vector3.up, a);
        float hw = bridge.lossyScale.x * 0.5f - PathFenceBridgeIn;
        // 直線 P(t)=Bc+sd*off+a*t と円の交点（本島は小島側、小島は本島側）
        float Hit(Vector3 c, float r, float off, bool far)
        {
            Vector3 p0 = Bc + sd * off - c; float b = Vector3.Dot(p0, a), cc = Vector3.Dot(p0, p0) - r * r, disc = b * b - cc;
            if (disc < 0f) disc = 0f; float sq = Mathf.Sqrt(disc);
            return far ? -b + sq : -b - sq;
        }
        Vector3 Pt(float off, float t) => Bc + sd * off + a * t;
        Vector3 mL = Pt(-hw, Hit(O, R, -hw, false)), mR = Pt(hw, Hit(O, R, hw, false));
        Vector3 iL = Pt(-hw, Hit(Ci, ri, -hw, true)), iR = Pt(hw, Hit(Ci, ri, hw, true));
        float Ang(Vector3 c, Vector3 p) => Mathf.Atan2(p.z - c.z, p.x - c.x);
        // 円弧：from から to へ、橋のある向き（bridgeDir）をまたがない方向で回る
        List<Vector3> Arc(Vector3 c, float r, Vector3 from, Vector3 to, Vector3 bridgeDir)
        {
            float a0 = Ang(c, from), a1 = Ang(c, to), ab = Mathf.Atan2(bridgeDir.z, bridgeDir.x);
            float d = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;   // 短い方
            float mid = a0 + d * 0.5f;
            bool crossesBridge = Mathf.Abs(Mathf.DeltaAngle(mid * Mathf.Rad2Deg, ab * Mathf.Rad2Deg)) < 90f;
            if (crossesBridge) d = d > 0 ? d - Mathf.PI * 2f : d + Mathf.PI * 2f;   // 長い方へ
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(d) * r / PathFenceSpacing));
            var l = new List<Vector3>();
            for (int i = 0; i <= n; i++) { float t = a0 + d * i / n; l.Add(c + new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * r); }
            return l;
        }
        List<Vector3> Line(Vector3 p, Vector3 q) { int n = Mathf.Max(1, Mathf.CeilToInt((q - p).magnitude / PathFenceSpacing)); var l = new List<Vector3>(); for (int i = 0; i <= n; i++) l.Add(Vector3.Lerp(p, q, (float)i / n)); return l; }
        // ひとつながり：本島の輪（mR→mL、橋の反対回り）→ 橋の左（mL→iL）→ 小島の輪（iL→iR、本島の反対回り）→ 橋の右（iR→mR）
        var pts = new List<Vector3>(); var ys = new List<float>();
        void Add(List<Vector3> l, float yEdge0, float yMid, float yEdge1, bool skipFirst)
        {
            for (int i = skipFirst ? 1 : 0; i < l.Count; i++) { pts.Add(l[i]); ys.Add(i == 0 ? yEdge0 : (i == l.Count - 1 ? yEdge1 : yMid)); }
        }
        Add(Arc(O, R, mR, mL, -a), yMain, yMain, yMain, false);
        Add(Line(mL, iL), yMain, yBridge, yIslet, true);
        Add(Arc(Ci, ri, iL, iR, a), yIslet, yIslet, yIslet, true);
        Add(Line(iR, mR), yIslet, yBridge, yMain, true);   // 最後の点（mR）は最初の点と同じ
        // メッシュ（ワールドの寸法。root は縮んでいるので、逆の大きさの子に入れる）
        var fence = root.Find("PathFence"); if (fence) Object.DestroyImmediate(fence.gameObject);
        fence = new GameObject("PathFence").transform; fence.SetParent(root, false);
        fence.position = new Vector3(O.x, root.position.y, O.z); fence.rotation = Quaternion.identity;
        fence.localScale = new Vector3(1f / root.lossyScale.x, 1f / root.lossyScale.y, 1f / root.lossyScale.z);
        Vector3 Lp(Vector3 w, float y) => new Vector3(w.x, y, w.z) - fence.position;
        var postMesh = FuwaBevelTools.RoundCube(new Vector3(PathFencePostW, PathFencePostH, PathFencePostW), 0.03f, 1);
        var parts = new List<CombineInstance>(); var colParts = new List<CombineInstance>();
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        const float baseY = -0.02f;
        for (int i = 0; i < pts.Count; i++)
        {
            var P = Lp(pts[i], ys[i]);
            if (i < pts.Count - 1) parts.Add(new CombineInstance { mesh = postMesh, transform = Matrix4x4.TRS(P + Vector3.up * (baseY + PathFencePostH * 0.5f), Quaternion.identity, new Vector3(PathFencePostW, PathFencePostH, PathFencePostW)) });
            if (i + 1 >= pts.Count) break;
            var Q = Lp(pts[i + 1], ys[i + 1]);
            Vector3 d = Q - P; float len = d.magnitude; if (len < 0.01f) continue;
            Quaternion rr = Quaternion.LookRotation(d / len, Vector3.up);
            var railMesh = FuwaBevelTools.RoundCube(new Vector3(PathFenceRailW, PathFenceRailH, len + 0.04f), 0.02f, 1);
            foreach (float ry in PathFenceRailY) parts.Add(new CombineInstance { mesh = railMesh, transform = Matrix4x4.TRS((P + Q) * 0.5f + Vector3.up * (baseY + ry), rr, new Vector3(PathFenceRailW, PathFenceRailH, len + 0.04f)) });
            Vector3 dh = d; dh.y = 0f; Quaternion rh = Quaternion.LookRotation(dh.normalized, Vector3.up);
            colParts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS((P + Q) * 0.5f + Vector3.up * (PathFenceColH * 0.5f), rh, new Vector3(0.12f, PathFenceColH, dh.magnitude + 0.05f)) });
        }
        var vis = new Mesh { name = "BonusHub_PathFence", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; vis.CombineMeshes(parts.ToArray(), true, true); vis.RecalculateBounds();
        var col = new Mesh { name = "BonusHub_PathFenceCollider", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; col.CombineMeshes(colParts.ToArray(), true, true); col.RecalculateBounds();
        Mesh Save(Mesh m, string nm)
        {
            string ap = "Assets/FuwaStage2/Generated/" + nm + ".asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (!old) { AssetDatabase.CreateAsset(m, ap); return m; }
            old.Clear(); old.indexFormat = m.indexFormat; old.SetVertices(m.vertices); old.SetNormals(m.normals); old.SetUVs(0, m.uv); old.SetTriangles(m.triangles, 0); old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old;
        }
        vis = Save(vis, "BonusHub_PathFence"); col = Save(col, "BonusHub_PathFenceCollider");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat");
        var vg = new GameObject("Fence", typeof(MeshFilter), typeof(MeshRenderer)); vg.transform.SetParent(fence, false);
        vg.GetComponent<MeshFilter>().sharedMesh = vis; vg.GetComponent<MeshRenderer>().sharedMaterial = wood;
        vg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 2026-11-23 てじゃ「おまけ本島の柵の影でてる」→ ロビーの柵と同じく影なし
        var cg = new GameObject("FenceCollider"); cg.transform.SetParent(fence, false); cg.AddComponent<MeshCollider>().sharedMesh = col;
        GameObjectUtility.SetStaticEditorFlags(vg, StaticEditorFlags.BatchingStatic);
        GameObjectUtility.SetStaticEditorFlags(cg, StaticEditorFlags.BatchingStatic);
        // 前の柵は隠す
        foreach (var t in new[] { isl.Find("Fence"), isl.Find("FenceCollider"), wi.Find("Fence"), wi.Find("FenceCollider") }) if (t) t.gameObject.SetActive(false);
        return "柵（ひとつながり）柱 " + (pts.Count - 1) + "（本島 r=" + R.ToString("F2") + "・小島 r=" + ri.ToString("F2") + "・橋の柵の間 " + (hw * 2f).ToString("F2") + "） ";
    }

    [MenuItem("FuwaCourse/ステージ2/おまけ本島の柵だけ作り直す")]
    static void MenuPathFence()
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (!root) { Debug.Log("おまけ本島がない"); return; }
        Debug.Log(PathFence(root.transform));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }

    // 2026-11-19 てじゃ「柵もちゃんとつないで～」：本島の柵の切れ目のはし と 橋の柵のはし を、同じ形（柱＋横木2本）でつなぐ（大きさを決めた後のワールドで作る）
    static string JoinWestFence(Transform root)
    {
        var wi = root.Find("WestIsland"); var isl = root.Find("Island");
        var bf = wi ? wi.Find("Fence") : null; var mf = isl ? isl.Find("Fence") : null; var fcol = isl && isl.Find("FenceCollider") ? isl.Find("FenceCollider").GetComponent<Collider>() : null;
        if (!bf || !mf || !fcol) return "柵つなぎ：材料がない ";
        var old = root.Find("WestFenceJoin"); if (old) Object.DestroyImmediate(old.gameObject);
        Physics.SyncTransforms();
        Vector3 c = root.position; float y0 = c.y;
        Vector3 toW = wi.position - c; toW.y = 0f; float distW = toW.magnitude; toW /= distW;
        Vector3 side = Vector3.Cross(Vector3.up, toW);
        // 本島の柵の切れ目のはし：見た目の柵の頂点のうち、橋の向きから左右それぞれいちばん角度が近い物（当たりの切れ目は見た目とずれるので見た目で）
        Vector3[] fenceEnd = new Vector3[2]; float[] bestA = { 1e9f, 1e9f };
        var fmesh = mf.GetComponent<MeshFilter>().sharedMesh; var fvs = fmesh.vertices;
        foreach (int vi in new HashSet<int>(fmesh.triangles))   // 切れ目の所も頂点だけは残っているので、三角形に使われている頂点だけ見る
        {
            var v = fvs[vi];
            var wp = mf.TransformPoint(v); if (wp.y < y0 + 0.3f) continue;
            var d = wp - c; d.y = 0f; if (d.magnitude < 1f) continue;
            float ang = Vector3.SignedAngle(toW, d, Vector3.up); int k = Vector3.Dot(d, side) > 0 ? 1 : 0;
            if (Mathf.Abs(ang) < bestA[k]) { bestA[k] = Mathf.Abs(ang); fenceEnd[k] = new Vector3(wp.x, y0, wp.z); }
        }
        // 切れ目のまん中に橋を向ける（てじゃ「こんどは横のがはみだしてる」：当たりで探した切れ目の向きが見た目とずれていて、橋が片側に寄っていた）
        {
            Vector3 f0 = fenceEnd[0] - c, f1 = fenceEnd[1] - c; f0.y = 0f; f1.y = 0f;
            Vector3 mid = (f0.normalized + f1.normalized); mid.y = 0f;
            if (mid.sqrMagnitude > 1e-4f)
            {
                float turn = Vector3.SignedAngle(toW, mid, Vector3.up);
                var q = Quaternion.AngleAxis(turn, Vector3.up);
                foreach (var t in new[] { wi, root.Find("ToTitle") }) { if (!t) continue; t.position = c + q * (t.position - c); t.rotation = q * t.rotation; }
                Physics.SyncTransforms();
                toW = wi.position - c; toW.y = 0f; toW.Normalize(); side = Vector3.Cross(Vector3.up, toW);
            }
        }
        // 橋の柵のはし（本島にいちばん近い柱）：左右それぞれ
        var bm = bf.GetComponent<MeshFilter>().sharedMesh; var bvs = bm.vertices;
        Vector3[] bridgeEnd = new Vector3[2]; float[] best = { 1e9f, 1e9f };
        foreach (var v in bvs)
        {
            var wp = bf.TransformPoint(v); var d = wp - c; d.y = 0f;
            if (wp.y < y0 + 0.3f) continue;
            float along = Vector3.Dot(d, toW); float sd = Vector3.Dot(d, side); int k = sd > 0 ? 1 : 0;
            if (along < best[k]) { best[k] = along; bridgeEnd[k] = new Vector3(wp.x, y0, wp.z); }
        }
        // 左右の組み合わせを合わせる（近い方同士）
        if ((fenceEnd[0] - bridgeEnd[0]).sqrMagnitude + (fenceEnd[1] - bridgeEnd[1]).sqrMagnitude > (fenceEnd[0] - bridgeEnd[1]).sqrMagnitude + (fenceEnd[1] - bridgeEnd[0]).sqrMagnitude)
        { var t = fenceEnd[0]; fenceEnd[0] = fenceEnd[1]; fenceEnd[1] = t; }
        var wood = mf.GetComponent<MeshRenderer>().sharedMaterial;
        var jr = new GameObject("WestFenceJoin").transform; jr.SetParent(root, true);   // ワールドの大きさ1のまま（子はワールドの寸法で作る）
        int n = 0;
        // まっすぐ（てじゃ「まっすぐになってないよ～」）：橋の柵はそのまま本島の柵の輪までまっすぐのばし、輪の上は切れ目のはしまで弧でつなぐ
        void Seg(Vector3 a, Vector3 b, string nm, bool post)
        {
            float len = (b - a).magnitude; if (len < 0.03f) return;
            var rot = Quaternion.LookRotation(b - a);
            foreach (var hy in new[] { 0.18f, 0.46f })
            {
                var r = GameObject.CreatePrimitive(PrimitiveType.Cube); r.name = nm; Object.DestroyImmediate(r.GetComponent<Collider>()); r.transform.SetParent(jr, true);
                r.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * hy, rot); r.transform.localScale = new Vector3(0.05f, 0.06f, len + 0.04f);
                r.GetComponent<MeshRenderer>().sharedMaterial = wood; r.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (post)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube); p.name = nm + "_Post"; Object.DestroyImmediate(p.GetComponent<Collider>()); p.transform.SetParent(jr, true);
                p.transform.SetPositionAndRotation(a + Vector3.up * 0.26f, rot); p.transform.localScale = new Vector3(0.08f, 0.52f, 0.08f);
                p.GetComponent<MeshRenderer>().sharedMaterial = wood; p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var cg = new GameObject(nm + "_Col"); cg.transform.SetParent(jr, true); cg.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 0.6f, rot);
            cg.AddComponent<BoxCollider>().size = new Vector3(0.15f, 1.2f, len + 0.1f);
        }
        for (int k = 0; k < 2; k++)
        {
            Vector3 fe = fenceEnd[k] - c, be = bridgeEnd[k] - c; fe.y = 0f; be.y = 0f;
            float Rf = fe.magnitude;
            // 橋の柵の線（toW 向き、横の位置 sd）が半径 Rf の輪と交わる所
            float sd = Vector3.Dot(be, side); float al = Mathf.Sqrt(Mathf.Max(0f, Rf * Rf - sd * sd));
            Vector3 meet = side * sd + toW * al;
            Seg(meet + c, bridgeEnd[k], "Line" + k, (meet - fe).magnitude > 0.25f);   // 本島の柵のはしの柱とほぼ同じ所なら柱は足さない（柱が2本ならぶので）
            // 輪の上：meet から fe まで弧（0.5m ごと）
            float a0 = Mathf.Atan2(meet.z, meet.x), a1 = Mathf.Atan2(fe.z, fe.x);
            float da = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            // 橋の柵の線が切れ目の外（本島の柵がもうある所）に当たる時は弧はいらない（てじゃ「こんどは横のがはみだしてる」）
            float angMeet = Mathf.Abs(Vector3.SignedAngle(toW, meet, Vector3.up)), angFe = Mathf.Abs(Vector3.SignedAngle(toW, fe, Vector3.up));
            if (angMeet >= angFe - 0.2f) { n++; continue; }
            int ns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(da) * Rf / 0.5f));
            for (int q = 0; q < ns; q++)
            {
                float t0 = a0 + da * q / ns, t1 = a0 + da * (q + 1) / ns;
                Seg(c + new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0)) * Rf, c + new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1)) * Rf, "Arc" + k + "_" + q, q == 0);
            }
            n++;
        }
        return "柵つなぎ " + n + "（" + string.Join(",", new[] { 0, 1 }.Select(k => (bridgeEnd[k] - fenceEnd[k]).magnitude.ToString("F2"))) + "m） ";
    }

    static void ApplyHandLayout(Transform root)
    {
        foreach (var (path, pos, euler, scale) in HandLayout)
        {
            var t = root.Find(path); if (!t) continue;
            t.localPosition = pos; t.localEulerAngles = euler; t.localScale = scale;
        }
    }
    const string ThanksText = "<size=62%><color=#FF8FA3>C</color><color=#FFC36B>o</color><color=#7FD7A8>n</color><color=#7FB8F0>g</color><color=#B79BF0>r</color><color=#FF8FA3>a</color><color=#FFC36B>t</color><color=#7FD7A8>u</color><color=#7FB8F0>l</color><color=#B79BF0>a</color><color=#FF8FA3>t</color><color=#FFC36B>i</color><color=#7FD7A8>o</color><color=#7FB8F0>n</color><color=#B79BF0>s</color><color=#FF8FA3>!</color></size>";   // 2026-11-18 てじゃ「文字、Congratulations! のほうがいいきがしてきたな」

    [MenuItem("FuwaCourse/ステージ2/おまけ本島（記念の島）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    static Transform Find(string path) { var p = path.Split(new[] { '/' }, 2); var r = GameObject.Find(p[0]); return r == null ? null : p.Length > 1 ? r.transform.Find(p[1]) : r.transform; }

    public static string Build()
    {
        var log = new System.Text.StringBuilder("[おまけ本島] ");
        var lobby = Find("FuwaStage2/Lobby"); var reward = Find("FuwaRewardIsland");
        if (!lobby || !reward) return "ステージ2ロビー／ごほうびの島がない";
        { var ob = Find("FuwaSystem/BonusBoards"); if (ob) Object.DestroyImmediate(ob.gameObject); }   // 前に作った、みんなで持てるボード
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
            off.x *= IslandXZ; off.z *= IslandXZ;
            g.transform.position = Pos + off * Scale; g.transform.localScale = Vector3.Scale(src.lossyScale * Scale, new Vector3(IslandXZ, 1f, IslandXZ));
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
            // 柵は切れ目のない版に（ロビーの柵は橋をつなぐ所が開いている Open 版。2026-11-19 てじゃ「外の柵に切れ目ある」）
            if (nm == "Fence" && !WestBridge)
            {
                var cm = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Bevel/FuwaLobby_Fence_Bevel.asset"); var mf0 = g.GetComponent<MeshFilter>(); if (cm && mf0) mf0.sharedMesh = cm;
                var far = g.transform.Find("Fence_Far"); var cmf = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Generated/Far/FuwaLobby_Fence_Bevel_Far.asset");
                if (far && cmf) far.GetComponent<MeshFilter>().sharedMesh = cmf;   // 遠くの軽い版も切れ目なし
            }
            if (nm == "FenceCollider" && !WestBridge) { var cm = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/FuwaLobby_FenceCollider.asset"); var mc0 = g.GetComponent<MeshCollider>(); if (cm && mc0) mc0.sharedMesh = cm; var mf1 = g.GetComponent<MeshFilter>(); if (cm && mf1) mf1.sharedMesh = cm; }
            // 芝はステージ1の色（2026-11-18 てじゃ「本島の芝はステージ1のカラーにしといて」）
            if (nm == "LobbyFloor") { var gm = FuwaLobbyPaths.FindMat("Goal_Ground"); var fr = g.GetComponent<MeshRenderer>(); if (gm && fr) fr.sharedMaterials = fr.sharedMaterials.Select(m => m && m.name == "Stage2_LobbyGround" ? gm : m).ToArray(); }
        }
        float R = 8f * Scale * IslandXZ;   // 床の半径（ロビーは16m）

        // ---- 立ち位置・ふわふわ・銃 ----
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false);
        spawn.position = Pos + Vector3.back * (R - SpawnFromEdge) + Vector3.up * 0.1f; spawn.rotation = Quaternion.LookRotation(Vector3.forward);   // 真うしろに戻りポータル（ふれない距離をあける）
        var ballPark = new GameObject("BallPark").transform; ballPark.SetParent(root, false);
        ballPark.position = Pos + Vector3.back * (R - 3.4f) + Vector3.right * 1.2f + Vector3.up * 1.2f;
        var gunSpot = new GameObject("GunSpot").transform; gunSpot.SetParent(root, false);
        gunSpot.position = Pos + Vector3.back * (R - 3.6f) + Vector3.left * 1.0f + Vector3.up * 1.0f; gunSpot.rotation = spawn.rotation;

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
                tb.language = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(x => x.gameObject.scene.IsValid());   // 2026-11-19 英語にも
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
            t.localScale = Vector3.one;   // 前の本島（0.75倍に縮めた後）から移すと、作り直すたびに 0.75 倍ずつ小さくなっていた（2026-11-18 てじゃ「金の銃と金ふわふわどっかいってる」）
        }
        // きんピカ台・金ピカふわふわ：輪の外の右（東）。島のまん中を向く
        if (stands.Count > 0) { stands[0].position = Pos + new Vector3(GoldX, 0f, -1.6f); stands[0].rotation = Quaternion.LookRotation(Vector3.left); }
        if (stands.Count > 1) { stands[1].position = Pos + new Vector3(GoldX, 0f, 1.6f); stands[1].rotation = Quaternion.LookRotation(Vector3.left); }
        if (ra) Undo.DestroyObjectImmediate(ra);   // ゴールの後に台を出す係はいらない（台はいつもここにある）

        // ---- 奥：ステージ2ロビーへのポータル（ごほうびの島のスタートのワープを写す）----
        var st = reward.Find("Start");
        // 2026-11-19 てじゃ「入ってきたとこの戻るポータルは無しでいいや、左側のタイトル行きポータルだけでいいよ」→ ReturnPortal=false で作らない
        var portal = new GameObject("ToLobby").transform; portal.SetParent(root, false);
        // 2026-11-18 てじゃ「戻るポータルはここにスポーンしたときの真後ろに」：スポーンの真うしろ（島の手前のふち寄り）。台はなし、うずは床の少し上
        portal.position = Pos + Vector3.back * (R - PortalFromEdge) + Vector3.up * 0.03f; portal.rotation = Quaternion.LookRotation(Vector3.forward);
        if (!ReturnPortal) { Object.DestroyImmediate(portal.gameObject); log.Append("戻りポータルなし "); }
        else if (st)
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
                var wg = PortalBase ? st.Find("StartArea/WarpGround") : null;   // てじゃ「戻りポータルが乗ってる台もいらないかな」
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
        var allMush = ImportBigMush();
        // 2026-11-19 てじゃ「本島の一番デカいきのこ、隣のやつくらい平たい寄りにしたい」「0.75くらいで」：奥のまん中だけ専用の形 BigMush_C
        //  （BigMush_6 のかさを open 0.75 にした物。BigMush_6 は輪の小さい2本でも使っているので別にした）。輪の並びは今まで通り 1〜6 から選ぶ
        var centerMesh = allMush.FirstOrDefault(m => m.name == "BigMush_C");
        var meshes = allMush.Where(m => m.name != "BigMush_C").ToArray();
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
                // 奥の3本以外は、立つ所の丸（Stage）にぴったり寄せる（2026-11-18 てじゃ「奥の3つ以外がっつりサークルによせて」）
                if (Mathf.Abs(i - n / 2) > 1)
                {
                    var stageC = ringC + Vector3.forward * (BoardBack - StageR - 0.2f);
                    at = stageC + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * (StageR + RingHugGap + ((float)rnd.NextDouble() - 0.5f) * 0.3f);
                }
                var mesh = meshes[(i * 4 + 1) % meshes.Length];
                var go = new GameObject("Mush" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(ring, false);
                bool centerBack = i == n / 2;   // 奥のまん中：大きめの赤（2026-11-18 てじゃ「奥の真ん中は大きめの赤いキノコがいいな」）
                if (centerBack) h = RingHBack * BackCenterScale;
                if (i == 0) h *= FrontLeftScale;   // 手前の左（赤）は小さめ（2026-11-18 てじゃ「左手前の赤もっとちいさく」）
                float k = h / mesh.bounds.size.y;
                if (centerBack && centerMesh) mesh = centerMesh;   // 大きさ（k）は元の形で決めて、かさだけ平たく（軸の太さ・長さはそのまま）
                go.transform.position = new Vector3(at.x, Pos.y - 0.12f, at.z);   // 傾けても根元が浮かないよう少しうめる
                float lean = Mathf.Lerp(LeanMin, LeanMax, (float)rnd.NextDouble()), leanDir = (float)rnd.NextDouble() * 360f;
                go.transform.rotation = Quaternion.AngleAxis(lean, Quaternion.Euler(0f, leanDir, 0f) * Vector3.right) * Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                if (centerBack) LeanAway(go.transform, mesh, new Vector3(at.x - ringC.x, 0f, at.z - ringC.z));
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
                // 2026-11-19 てじゃ「当たり判定あるきのことないきのこがある、全部なくしたい」：当たり判定はつけない（前は茎にカプセル）
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
            // 2026-11-19 てじゃ「白いステージが地味すぎる」「土でもいいかもね、タイトル画面みたいな、ただ芝とのフチをどうするか」：
            //  タイトルの島と同じ土（Lobby_Floor、石のもよう）。ふちはゆるく波打たせて「芝がはげて土が出た」感じ、境目に少し濃い土の帯。芝からほんの少しだけ上
            var cb = cyl.bounds.size;
            var st0 = new GameObject("Stage", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)).transform; st0.SetParent(root, false);
            st0.position = ringC + Vector3.forward * (BoardBack - StageR - 0.2f) + Vector3.up * 0.012f;
            st0.localScale = Vector3.one;
            var dirt0 = FuwaLobbyPaths.FindMat("Lobby_Floor") ?? BonusMat("Bonus_PhotoStage", new Color(1f, 0.9f, 0.88f));
            // 石のもようを細かく（てじゃ「土の模様密度ひくいかも」）：もようはワールドの xz で取るので、大きさの値を小さくした写しを使う
            const string dirtPath = "Assets/FuwaStage2/Materials/Bonus_StageDirt.mat";
            var dirt = AssetDatabase.LoadAssetAtPath<Material>(dirtPath);
            if (!dirt) { dirt = new Material(dirt0); AssetDatabase.CreateAsset(dirt, dirtPath); } else { dirt.shader = dirt0.shader; dirt.CopyPropertiesFromMaterial(dirt0); }
            dirt.name = "Bonus_StageDirt";
            foreach (var pn in new[] { "_GroundFineScale", "_GroundMidScale", "_GroundPatchScale" }) if (dirt.HasProperty(pn)) dirt.SetFloat(pn, dirt0.GetFloat(pn) / StageDirtUv);
            EditorUtility.SetDirty(dirt);
            Color dc = dirt.HasProperty("_Color") ? dirt.color : new Color(0.75f, 0.6f, 0.45f);
            var rimM = BonusMat("Bonus_StageRim", new Color(dc.r * 0.82f, dc.g * 0.78f, dc.b * 0.74f));
            float R0 = StageR; float RadiusAt(float a) => R0 * (1f + 0.045f * Mathf.Sin(a * 5f + 0.7f) + 0.03f * Mathf.Sin(a * 9f + 2.1f) + 0.02f * Mathf.Sin(a * 14f + 0.3f));
            Mesh Disc(string name, float grow, float y, bool ringOnly)
            {
                const int N = 96; var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tri = new List<int>();
                float uvk = Shrink;
                if (!ringOnly) { vs.Add(new Vector3(0f, y, 0f)); uvs.Add(Vector2.zero); }
                for (int i = 0; i < N; i++)
                {
                    float a2 = i * Mathf.PI * 2f / N; float r = RadiusAt(a2) + grow;
                    var p = new Vector3(Mathf.Cos(a2) * r, y, Mathf.Sin(a2) * r); vs.Add(p); uvs.Add(new Vector2(p.x, p.z) * uvk);
                }
                if (!ringOnly) for (int i = 0; i < N; i++) { tri.Add(0); tri.Add(1 + (i + 1) % N); tri.Add(1 + i); }
                else
                {
                    for (int i = 0; i < N; i++) { float a2 = i * Mathf.PI * 2f / N; float r = RadiusAt(a2) - 0.02f; var p = new Vector3(Mathf.Cos(a2) * r, y, Mathf.Sin(a2) * r); vs.Add(p); uvs.Add(new Vector2(p.x, p.z) * uvk); }
                    for (int i = 0; i < N; i++) { int j = (i + 1) % N; tri.Add(i); tri.Add(N + i); tri.Add(j); tri.Add(j); tri.Add(N + i); tri.Add(N + j); }
                }
                string ap = "Assets/FuwaStage2/Generated/BonusHub_" + name + ".asset";
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(ap); bool isNew = !m; if (isNew) m = new Mesh(); else m.Clear();
                m.name = name; m.SetVertices(vs); m.SetUVs(0, uvs); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
                if (isNew) AssetDatabase.CreateAsset(m, ap); else EditorUtility.SetDirty(m);
                return m;
            }
            var top = Disc("StageDirt", 0f, 0.004f, false);
            st0.GetComponent<MeshFilter>().sharedMesh = top; st0.GetComponent<MeshCollider>().sharedMesh = top;
            st0.GetComponent<MeshRenderer>().sharedMaterial = dirt;
            var rim = new GameObject("StageRim", typeof(MeshFilter), typeof(MeshRenderer)).transform; rim.SetParent(st0, false);
            rim.GetComponent<MeshFilter>().sharedMesh = Disc("StageRim", 0.12f, 0f, true); rim.GetComponent<MeshRenderer>().sharedMaterial = rimM;
            foreach (var rr in st0.GetComponentsInChildren<MeshRenderer>(true)) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            st0.gameObject.layer = 11;
            // 撮る人の立ち位置の印：ボードが画面いっぱいに入る距離。足あと代わりの水色の丸（2026-11-18 てじゃ「青い円は消してOK」→ CameraSpotMark=false）
            if (CameraSpotMark) {
            var mk = new GameObject("CameraSpot", typeof(MeshFilter), typeof(MeshRenderer)).transform; mk.SetParent(root, false);
            mk.position = ringC + Vector3.forward * (BoardBack - CamDist) + Vector3.up * 0.012f;
            mk.localScale = new Vector3(0.9f / cb.x, 0.02f / cb.y, 0.9f / cb.z);
            mk.GetComponent<MeshFilter>().sharedMesh = cyl;
            mk.GetComponent<MeshRenderer>().sharedMaterial = BonusMat("Bonus_CameraSpot", new Color(0.62f, 0.83f, 1f));
            mk.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
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
            if (fg) { var gso = new SerializedObject(fg); gso.FindProperty("keepWarpOpen").boolValue = true; gso.FindProperty("saveKey").stringValue = "bonus_goal"; gso.FindProperty("noGunMessage").boolValue = true; gso.FindProperty("stayDone").boolValue = true; gso.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(fg); }
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
        if (WestBridge) log.Append(WestIsland(root, reward));
        if (UseHandLayout) { ApplyHandLayout(root); log.Append("手で並べた配置 "); }
        // 2026-11-19 てじゃ「おまけ本島のデカい傘の傾きがもどっちゃってるきがする」：手で並べた配置（HandLayout）の向きが LeanAway の傾きを上書きしていた → 位置・大きさはそのまま、向きだけ傾け直す
        {
            var m4 = root.Find("FairyRing/Mush" + (RingCount / 2)); var mf4 = m4 ? m4.GetComponent<MeshFilter>() : null;
            if (mf4 && mf4.sharedMesh)
            {
                var ringC0 = Pos + Vector3.forward * RingShift;
                LeanAway(m4, mf4.sharedMesh, new Vector3(m4.position.x - ringC0.x, 0f, m4.position.z - ringC0.z));
            }
        }
        foreach (var nm in new[] { "Board", "FairyRing", "Stage" }) { var t = root.Find(nm); if (t) t.localPosition += Vector3.forward * PhotoShiftZ; }   // 島を小さくした分、写真コーナーを手前へ
        // 奥のまん中の大きいきのこは、かさが「Congratulations!」の文字の前にかぶらないよう少し奥へ（かさを大きくした分。柵までは余裕あり）
        { var m4 = root.Find("FairyRing/Mush" + (RingCount / 2)); if (m4) m4.localPosition += Vector3.forward * CenterBackPush; }
        // うまりすぎのキノコを持ち上げる（2026-11-19 てじゃ「したのツボまで埋まってるやつあるから埋まりすぎなやつちょっぴりもちあげて」）：
        //  いちばん下の頂点が床から MushSinkMax より深いものは、そこまで上げる（傾いているので頂点で測る）
        {
            int lifted = 0; var ring2 = root.Find("FairyRing");
            if (ring2) foreach (Transform m in ring2)
            {
                var mf2 = m.GetComponent<MeshFilter>(); if (!mf2 || !mf2.sharedMesh) continue;
                float minY = float.MaxValue; var l2w = m.localToWorldMatrix;
                foreach (var v in mf2.sharedMesh.vertices) { float y = l2w.MultiplyPoint3x4(v).y; if (y < minY) minY = y; }
                float want = Pos.y - MushSinkMax;
                if (minY < want) { m.position += Vector3.up * (want - minY); lifted++; }
            }
            log.Append("キノコを持ち上げた " + lifted + " ");
        }
        // 2026-11-18 てじゃ「一旦ボードをPickupオブジェクトにしてよ」：置き場所さがし用（持って動かせる、重力なし・離したらその場に浮く）。決まったら BoardPickup=false に
        if (BoardPickup)
        {
            // 文字・ステージ1のボード・ステージ2のボードをそれぞれ別に持てる（てじゃ「文字と各ステージのボードそれぞれわけて」）
            var bt = root.Find("Board"); int np = 0;
            foreach (Transform part in bt ? bt.Cast<Transform>().ToList() : new List<Transform>())
            {
                var brs2 = part.GetComponentsInChildren<Renderer>(true); if (brs2.Length == 0) continue;
                var wb = brs2[0].bounds; foreach (var br in brs2) wb.Encapsulate(br.bounds);
                var bc = part.gameObject.AddComponent<BoxCollider>();
                FitBoardCollider(part, bc, 0.2f);
                var rb = part.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                var pu = part.gameObject.AddComponent<VRC.SDK3.Components.VRCPickup>();
                pu.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pu.InteractionText = part.name; pu.proximity = 3f; pu.allowManipulationWhenEquipped = true;
                part.gameObject.layer = 13;   // Pickup
                var pl = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPosLogger>(part.gameObject); pl.basis = root; pl.label = part.name;   // 離したらログに位置を書く
                UdonSharpEditorUtility.CopyProxyToUdon(pl); FuwaStage2WaterTest.NoSync(pl);
                np++;
            }
            log.Append("持てる " + np + " ");
        }
        root.localScale = Vector3.one * Shrink;
        if (WestBridge) log.Append(UsePathFence ? PathFence(root) : JoinWestFence(root));
        log.Append(StatsBoard(root));
        if (SharedBoards) log.Append(MakeSharedBoards(root));
        log.Append(Buttons(root));
        log.Append(GoldGunDisplay(root));
        // 入口島のミニキノコに影（2026-11-18 てじゃ「入口島のミニキノコに影がない」：2–4 からの写しで、近くだけ影を出す係に入っていなかった）
        {
            int ns = 0;
            foreach (var mr in reward.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || !mf.sharedMesh.name.StartsWith("MiniMush")) continue;
                if (mr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; ns++; }
            }
            log.Append("入口島のミニキノコの影 " + ns + " ");
        }
        if (old) Object.DestroyImmediate(old);
        root.gameObject.SetActive(false);   // ふだんは消えている（area 4 の時だけ）
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log.ToString();
    }

    // 2026-11-19 てじゃ「いま手前にかたむいてるけど、奥に傘が傾いてる感じにしたい、軸もあわせて」：
    //  奥のまん中のキノコは、かさの傾き（メッシュの capTilt）が outward（輪の外＝撮る人から見て奥）を向くように回して、軸ごと CenterLean 度だけ奥へ倒す
    public static float CenterLean = 6f;
    public static void LeanAway(Transform t, Mesh mesh, Vector3 outward)
    {
        outward.y = 0f; if (outward.sqrMagnitude < 1e-6f) return; outward.Normalize();
        // かさの傾きの向き（メッシュの中）：かさのてっぺん（サブメッシュ1のいちばん高い点）が、軸の上の端の真上からどっちへずれているか
        //  （かさ全体の重心は軸のてっぺんの高さ付近にあって、傾けてもほとんど動かないので使えない）
        var vs = mesh.vertices;
        Vector3 apex = Vector3.zero; float best = float.MinValue;
        foreach (var i in new HashSet<int>(mesh.GetTriangles(1))) if (vs[i].y > best) { best = vs[i].y; apex = vs[i]; }
        var stemIdx = new HashSet<int>(mesh.GetTriangles(0)); float sTop = float.MinValue; foreach (var i in stemIdx) sTop = Mathf.Max(sTop, vs[i].y);
        Vector3 st = Vector3.zero; int sn = 0; foreach (var i in stemIdx) if (vs[i].y > sTop - 0.05f) { st += vs[i]; sn++; }
        if (sn > 0) st /= sn;
        Vector3 cap = apex - st; cap.y = 0f;
        float capYaw = cap.sqrMagnitude > 1e-8f ? Mathf.Atan2(cap.x, cap.z) * Mathf.Rad2Deg : 0f;
        float outYaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
        // かさのてっぺんは手前（輪の中）へずらす＝かさの面が奥へ倒れて見える（2026-11-19 てじゃ「傘だけほんのちょい奥に」→ 外向きにしたら「ぎゃく」）
        var yaw = Quaternion.Euler(0f, outYaw - capYaw + 180f, 0f);
        var tilt = Quaternion.AngleAxis(CenterLean, Vector3.Cross(Vector3.up, outward));   // 上を外へ倒す
        t.rotation = tilt * yaw;
    }

    // Tools/bigmush.json（Blender の bigmush_build.py が書き出す）→ メッシュのアセット
    [System.Serializable] class MushItem { public string name; public float[] v, n, uv; public int[] s0, s1, s2; }
    [System.Serializable] class MushFile { public MushItem[] items; }
    const string MushDir = "Assets/FuwaStage2/Generated/BigMush";
    public static Mesh[] ImportBigMush() { return ImportJsonMeshes("bigmush.json", MushDir); }

    // Tools/<file> の JSON（Blender で書き出し：座標・法線・UV と、材質ごとの三角形 s0〜s2）→ dir/<名前>.asset
    public static Mesh[] ImportJsonMeshes(string file, string dir)
    {
        var path = System.IO.Path.Combine(Application.dataPath, "../Tools/" + file);
        if (!System.IO.File.Exists(path)) return new Mesh[0];
        var f = JsonUtility.FromJson<MushFile>(System.IO.File.ReadAllText(path));
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir).Replace("\\", "/"), System.IO.Path.GetFileName(dir));
        var list = new List<Mesh>();
        foreach (var it in f.items)
        {
            string ap = dir + "/" + it.name + ".asset";
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
