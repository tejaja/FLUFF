using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2026-11-10 2–5 そらのいせき ①ポータルの試作（てじゃ「島と島はワープポータルで渡る」「壁に開いてて向こう側がみえてるタイプ、ふわふわも人もそこを通って進む」）
//  小さい遺跡の島2つ（島1・島2）と、ポータル1組（島1の北のはしの A、島2の南のはしの B）。A の奥へ進む → B から北へ出る。B の奥へ（南へ）進む → A から南へ出る
//  向こうの見え方（カメラなし）：A の穴の奥に「島2のコピー」、B の穴の奥に「島1のコピー」を置き、ポータルの穴（Fuwa/PortalMask がステンシルを書く）の中だけに描く
//   コピーは FuwaCourse/ToonLit を Comp=Equal・Ref=ポータルの番号・_PortalClip（ポータルの面の手前を切る）にした材質。影は出さない・受けない
//   A と B は向きが 180° ちがうだけ（ずれは平行移動だけ）→ 空の向きも同じなので、穴の中の空は本物の空のままで合う
//  島の床はポータルの面でちょうど終わる（面の奥に本物の床があると、穴の中でコピーの床と重なってちらつく）
//  島2 は島1 の南の上（A の穴から北を見ても、本物の島2は見えない所）
public static class FuwaPortalTest
{
    public static readonly Vector3 Origin = new Vector3(-40f, -162f, -140f);
    public static Vector3 Island2Offset = new Vector3(9f, 5f, -3f);   // 2026-11-10 てじゃ「遠景がポータル越しでもワープ前のがみえてる」→ 入口と出口の距離を短く（前は (6,14,-38) で 40m）。島2は島1の南東の上（A の穴からは見えない所）
    public const float HalfW = 1.1f, HoleH = 3.0f, FloorTh = 0.8f;
    const string Root = "PortalTest";
    static readonly Dictionary<string, Material> clones = new Dictionary<string, Material>();

    [MenuItem("FuwaCourse/ステージ2/2-5 ポータルのお試しを作る")]
    public static string Build()
    {
        Remove(); clones.Clear();
        var stone = FuwaStage2WaterTest.ToonMat("Ruins_Stone", new Color(0.86f, 0.74f, 0.62f));
        var terra = FuwaStage2WaterTest.ToonMat("Ruins_Terracotta", new Color(0.82f, 0.48f, 0.33f));
        var terraD = FuwaStage2WaterTest.ToonMat("Ruins_TerracottaDark", new Color(0.62f, 0.34f, 0.24f));
        var moss = FuwaStage2WaterTest.ToonMat("Ruins_Moss", new Color(0.55f, 0.72f, 0.45f));
        var glow = FuwaStage2WaterTest.ToonMat("Ruins_PortalRim", new Color(0.55f, 0.9f, 1f));
        glow.SetColor("_EmissionColor", new Color(0.35f, 0.8f, 1f) * 1.4f); EditorUtility.SetDirty(glow);
        var maskM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Ruins_PortalMask.mat");
        if (!maskM) { maskM = new Material(Shader.Find("Fuwa/PortalMask")); AssetDatabase.CreateAsset(maskM, "Assets/FuwaStage2/Materials/Ruins_PortalMask.mat"); }

        var root = new GameObject(Root).transform; root.position = Origin;
        // 島1（原点、北のはし z=+5 に A）・島2（Island2Offset、南のはし z=-5 に B）
        var i1 = Island(root, "Island1", Vector3.zero, -6f, 5f, stone, terra, terraD, moss, false);
        var i2 = Island(root, "Island2", Island2Offset, -5f, 6f, stone, terra, terraD, moss, true);
        var A = Portal(i1, "PortalA", new Vector3(0f, 0f, 5f), 180f, stone, terraD, glow, maskM, 1);   // 手前（forward）＝南
        var B = Portal(i2, "PortalB", new Vector3(0f, 0f, -5f), 0f, stone, terraD, glow, maskM, 2);    // 手前＝北
        // 向こうのコピー：A の奥に島2（B→A の変換）、B の奥に島1（A→B の変換）
        Copy(i2, A, B, root, "ViewThroughA", 1);
        Copy(i1, B, A, root, "ViewThroughB", 2);

        // 運ぶスクリプト（A→B、B→A）
        var ballT = Resources.FindObjectsOfTypeAll<FuwaBall>().FirstOrDefault(b => b.gameObject.scene.IsValid() && b.transform.parent && b.transform.parent.name == "FuwaSystem");
        float br = ballT ? ballT.GetComponent<SphereCollider>().radius * ballT.transform.lossyScale.x : 0.15f;
        foreach (var (en, ex, sref) in new[] { (A, B, 1), (B, A, 2) })
        {
            var pt = UdonSharpUndo.AddComponent<FuwaPortal>(en.gameObject);
            pt.entry = en; pt.exit = ex; pt.halfW = HalfW; pt.height = HoleH; pt.ball = ballT; pt.ballRadius = br;
            pt.ghost = BallGhost(root, ballT, en, sref, false);
            pt.nearGhost = BallGhost(root, ballT, en, sref, true);
            UdonSharpEditorUtility.CopyProxyToUdon(pt); FuwaStage2WaterTest.NoSync(pt);
        }

        // 銃：ポータルの穴ごしに向こうのふわふわを撃てるように（シーンのポータル全部を入れる）
        LinkBlowers();

        // ふわふわの台・ワープ（ロビー ⇄ 島1）
        var spot = new GameObject("BallSpot").transform; spot.SetParent(i1, false); spot.localPosition = new Vector3(-1.6f, 1.6f, -2.5f);
        FuwaStage2WaterTest.BallStand(i1, "BallStand", new Vector3(-2.6f, 0f, -3.2f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(i1, false); spawn.localPosition = new Vector3(0f, 0f, -4f); spawn.localRotation = Quaternion.identity;
        var warpMat = FuwaGearTest.Mat("PortalTest_Warp", new Color(0.6f, 0.95f, 1f));
        var lobby = GameObject.Find("FuwaStage2/Lobby/LobbySpawn");
        if (lobby)
        {
            FuwaIceTestBuilder.Warp(root, "WarpToPortalTest", lobby.transform.position + new Vector3(-5f, 0.7f, -3f), "PORTAL TEST", spawn, warpMat);
            FuwaIceTestBuilder.Warp(root, "WarpBack", i1.TransformPoint(new Vector3(3f, 0.7f, -4.5f)), "BACK", lobby.transform, warpMat);
        }
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "[ポータルのお試し] 作った（コピーの材質 " + clones.Count + "）";
    }

    [MenuItem("FuwaCourse/ステージ2/2-5 ポータルのお試しを消す")]
    public static void Remove() { var o = GameObject.Find(Root); if (o) Object.DestroyImmediate(o); LinkBlowers(); }

    // シーンのポータル（FuwaPortal）を全部、銃（FuwaBlower）に入れる
    public static void LinkBlowers()
    {
        var ps = Resources.FindObjectsOfTypeAll<FuwaPortal>().Where(x => x.gameObject.scene.IsValid() && x != null).ToArray();
        foreach (var b in Resources.FindObjectsOfTypeAll<FuwaBlower>().Where(x => x.gameObject.scene.IsValid()))
        {
            b.portals = ps; UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b);
        }
    }

    // 島：床（z0〜z1、ポータルのはしでちょうど終わる）＋下の岩＋柱。land=true の島2はクリスタルの目印つき
    static Transform Island(Transform root, string name, Vector3 lp, float z0, float z1, Material stone, Material terra, Material terraD, Material moss, bool mark)
    {
        var t = new GameObject(name).transform; t.SetParent(root, false); t.localPosition = lp;
        var vis = new GameObject("Visual").transform; vis.SetParent(t, false);
        float w = 7f;
        // 床（当たりあり）：テラコッタのタイルを市松に
        var fl = new GameObject("Floor"); fl.transform.SetParent(t, false); fl.layer = 11; fl.transform.localPosition = new Vector3(0f, -FloorTh / 2f, (z0 + z1) / 2f);
        fl.AddComponent<BoxCollider>().size = new Vector3(w, FloorTh, z1 - z0);
        int nx = 7, nz = Mathf.RoundToInt(z1 - z0);
        for (int i = 0; i < nx; i++) for (int k = 0; k < nz; k++)
            Prim(PrimitiveType.Cube, vis, "Tile", new Vector3(-w / 2f + (i + 0.5f) * w / nx, -FloorTh / 2f, z0 + (k + 0.5f) * (z1 - z0) / nz), new Vector3(w / nx, FloorTh, (z1 - z0) / nz), (i + k) % 2 == 0 ? terra : terraD);
        // 下の岩（ポータルの面より奥へ出ないように、少し内がわで先細り）
        Prim(PrimitiveType.Cylinder, vis, "Under", new Vector3(0f, -FloorTh - 1.2f, (z0 + z1) / 2f - 0.3f), new Vector3(w * 0.85f, 1.2f, (z1 - z0) * 0.8f), stone);
        Prim(PrimitiveType.Cylinder, vis, "Under2", new Vector3(0f, -FloorTh - 3.0f, (z0 + z1) / 2f - 0.4f), new Vector3(w * 0.5f, 1.0f, (z1 - z0) * 0.5f), stone);
        // 柱（折れたのも）
        foreach (var (x, z, h) in new[] { (-3.0f, z0 + 1.0f, 3.4f), (3.0f, z0 + 1.0f, 2.1f), (-3.0f, (z0 + z1) / 2f, 1.3f), (3.0f, (z0 + z1) / 2f, 3.4f) })
        {
            Prim(PrimitiveType.Cylinder, vis, "Column", new Vector3(x, h / 2f, z), new Vector3(0.6f, h / 2f, 0.6f), stone);
            Prim(PrimitiveType.Cube, vis, "ColumnBase", new Vector3(x, 0.1f, z), new Vector3(0.85f, 0.2f, 0.85f), stone);
            if (h > 3f) Prim(PrimitiveType.Cube, vis, "ColumnTop", new Vector3(x, h + 0.1f, z), new Vector3(0.85f, 0.2f, 0.85f), stone);
        }
        Prim(PrimitiveType.Sphere, vis, "MossClump", new Vector3(-2.2f, 0.05f, z0 + 2f), new Vector3(1.4f, 0.3f, 1f), moss);
        if (mark)
        {
            // 島2の目印：大きいクリスタル（穴の向こうに見えると分かりやすい）
            var cm = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_3_Crystal.mat") ?? terra;
            var c = Prim(PrimitiveType.Cube, vis, "Crystal", new Vector3(1.5f, 1.4f, z1 - 2.2f), new Vector3(0.7f, 2.8f, 0.7f), cm); c.localRotation = Quaternion.Euler(8f, 45f, 6f);
            var c2 = Prim(PrimitiveType.Cube, vis, "Crystal", new Vector3(2.3f, 0.8f, z1 - 1.6f), new Vector3(0.45f, 1.6f, 0.45f), cm); c2.localRotation = Quaternion.Euler(-14f, 20f, 18f);
        }
        return t;
    }

    // ポータル：石の枠（左右の柱・上の石・しきい）＋ふちの光る帯＋穴（PortalMask）。transform の forward＝手前（出てくる側）
    static Transform Portal(Transform island, string name, Vector3 lp, float yaw, Material stone, Material dark, Material glow, Material maskM, int stencil)
    {
        var p = new GameObject(name).transform; p.SetParent(island, false); p.localPosition = lp; p.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var fr = new GameObject("PortalFrame").transform; fr.SetParent(p, false);
        float d = 0.7f, pw = 0.7f;   // 枠の奥行き（面の前後に半分ずつ）・柱の太さ
        foreach (float sx in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, fr, "Pillar", new Vector3(sx * (HalfW + pw / 2f), (HoleH + 0.6f) / 2f, 0f), new Vector3(pw, HoleH + 0.6f, d), stone);
        Prim(PrimitiveType.Cube, fr, "Lintel", new Vector3(0f, HoleH + 0.3f, 0f), new Vector3(2f * HalfW + 2f * pw + 0.3f, 0.6f, d + 0.1f), stone);
        Prim(PrimitiveType.Cube, fr, "Cap", new Vector3(0f, HoleH + 0.75f, 0f), new Vector3(1.2f, 0.3f, d - 0.1f), dark);
        // ふちの光る帯（穴のまわり、面の手前に少しだけ出す）
        float g = 0.07f;
        foreach (float sx in new[] { -1f, 1f }) Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(sx * (HalfW + g / 2f), HoleH / 2f, 0f), new Vector3(g, HoleH, d + 0.04f), glow);
        Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(0f, HoleH + g / 2f, 0f), new Vector3(2f * HalfW + 2f * g, g, d + 0.04f), glow);
        // 穴（手前から見える面）。床の高さから HoleH まで。少し下へのばして床とのすき間をなくす
        var m = GameObject.CreatePrimitive(PrimitiveType.Quad); m.name = "PortalHole"; Object.DestroyImmediate(m.GetComponent<Collider>());
        m.transform.SetParent(p, false); m.transform.localPosition = new Vector3(0f, (HoleH - 0.6f) / 2f, 0f); m.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // Quad の表は -z → forward（手前）へ向ける
        m.transform.localScale = new Vector3(2f * HalfW, HoleH + 0.6f, 1f);
        var mr = m.GetComponent<MeshRenderer>(); var mm = new Material(maskM) { name = "Ruins_PortalMask_" + stencil }; mm.SetFloat("_StencilRef", stencil);
        string mp = "Assets/FuwaStage2/Materials/Ruins_PortalMask_" + stencil + ".mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(mp); if (old) { old.CopyPropertiesFromMaterial(mm); old.shader = mm.shader; mm = old; } else AssetDatabase.CreateAsset(mm, mp);
        mr.sharedMaterial = mm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        // 面の裏の見えない床（当たりだけ）：くぐった時、足はまだ面の裏側に出る（頭でくぐるので）。2026-11-10 押し出しをやめて、ここに立つ
        var back = new GameObject("PortalBackFloor"); back.transform.SetParent(p, false); back.layer = 0;
        back.transform.localPosition = new Vector3(0f, -FloorTh / 2f, -0.6f);
        back.AddComponent<BoxCollider>().size = new Vector3(2f * HalfW + 0.6f, FloorTh, 1.2f);
        return p;
    }

    // src（島）の見た目を、portalFrom の奥に見えるように置く（src 側の出口 srcPortal → 見る側の portalFrom の変換）。srcPortal の枠はのぞく（見る側の枠と重なる）
    static void Copy(Transform src, Transform portalFrom, Transform srcPortal, Transform root, string name, int stencil)
    {
        // srcPortal の奥へ進む → portalFrom の手前へ、の逆：src の点 q を portalFrom の奥へ：q' = portalFrom · R180 · srcPortal⁻¹ · q
        var M = portalFrom.localToWorldMatrix * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * srcPortal.worldToLocalMatrix;
        var holder = new GameObject(name).transform; holder.SetParent(root, false);
        Vector3 n = -portalFrom.forward; var clip = new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, portalFrom.position) - 0.01f);
        foreach (var r in src.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.transform.GetComponentsInParent<Transform>(true).Any(x => x.name == "PortalFrame" || x.name == "PortalHole") || r.name == "PortalHole") continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var g = new GameObject(r.name); g.transform.SetParent(holder, false);
            var W = M * r.transform.localToWorldMatrix;
            g.transform.position = W.GetColumn(3); g.transform.rotation = W.rotation; g.transform.localScale = r.transform.lossyScale;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var nr = g.AddComponent<MeshRenderer>(); nr.sharedMaterials = r.sharedMaterials.Select(m => StencilClone(m, stencil, clip)).ToArray();
            nr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; nr.receiveShadows = false;
            nr.lightmapIndex = r.lightmapIndex; nr.lightmapScaleOffset = r.lightmapScaleOffset;
        }
    }

    // ふわふわのうつし（2026-11-10 てじゃ「でもふわふわ見えないか」）：ふわふわと同じメッシュ・同じ毛の材質を、穴の中だけ・面の奥だけに見える設定で
    //  near=true：穴の手前に出ている分（ステンシルなし、入口の面の奥を切る）
    static MeshRenderer BallGhost(Transform root, FuwaBall ball, Transform portal, int stencil, bool near)
    {
        if (!ball) return null;
        var br = ball.GetComponent<MeshRenderer>(); var bf = ball.GetComponent<MeshFilter>(); if (!br || !bf) return null;
        var g = new GameObject((near ? "BallGhostNear_" : "BallGhost_") + portal.name); g.transform.SetParent(root, false);
        g.AddComponent<MeshFilter>().sharedMesh = bf.sharedMesh;
        var r = g.AddComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var src = br.sharedMaterial; string path = "Assets/FuwaStage2/Materials/PortalView/" + src.name + (near ? "_GhostNear" : "_Ghost") + stencil + ".mat";
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Materials/PortalView")) AssetDatabase.CreateFolder("Assets/FuwaStage2/Materials", "PortalView");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, path); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        if (!near)
        {
            Vector3 n = -portal.forward;
            m.SetFloat("_StencilRef", 64 | stencil); m.SetFloat("_StencilReadMask", 63); m.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Equal);
            m.SetVector("_PortalClip", new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, portal.position) - 0.01f));
        }
        else
        {
            Vector3 n = portal.forward;   // 入口の面の手前だけ残す（ふつうのふわふわと同じステンシル）
            m.SetFloat("_StencilRef", 64); m.SetFloat("_StencilReadMask", 64); m.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Always);
            m.SetVector("_PortalClip", new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, portal.position) + 0.005f));
        }
        EditorUtility.SetDirty(m); r.sharedMaterial = m; r.enabled = false;
        return r;
    }

    static Material StencilClone(Material m, int stencil, Vector4 clip)
    {
        if (!m) return m;
        string key = m.name + "_" + stencil;
        if (clones.TryGetValue(key, out var c)) return c;
        if (m.shader.name != "FuwaCourse/ToonLit") { Debug.LogWarning("[ポータル] ToonLit でない材質はコピーできない（穴の外にも見える）: " + m.name); return m; }
        string path = "Assets/FuwaStage2/Materials/PortalView/" + m.name + "_P" + stencil + ".mat";
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Materials/PortalView")) AssetDatabase.CreateFolder("Assets/FuwaStage2/Materials", "PortalView");
        var nm = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!nm) { nm = new Material(m); AssetDatabase.CreateAsset(nm, path); } else { nm.shader = m.shader; nm.CopyPropertiesFromMaterial(m); }
        nm.SetFloat("_StencilRef", stencil); nm.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Equal);
        nm.SetVector("_PortalClip", clip); nm.renderQueue = 2002; nm.enableInstancing = false;
        EditorUtility.SetDirty(nm); clones[key] = nm; return nm;
    }

    static Transform Prim(PrimitiveType t, Transform parent, string name, Vector3 lp, Vector3 ls, Material m)
    {
        var g = GameObject.CreatePrimitive(t); Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<MeshRenderer>().sharedMaterial = m; return g.transform;
    }
}
