using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2026-11-10 2–5 そらのいせき ②大きさの魔法の試作（てじゃ「ふわふわがちいさくなったりおおきくなったりする」）
//  まっすぐなレーン（+z）：ふわふわの台 → 青いルーン（小さく）→ 鉄格子（小さいと通れる）→ オレンジのルーン（大きく）→ 強い横風（大きいと流されにくい）→ 白いルーン（元に）
//  鉄格子は Walkthrough レイヤー（ふわふわとだけ当たる、人は通れる）で FuwaBounce（はね返る、アウトにならない）
public static class FuwaSizeTest
{
    public static readonly Vector3 Origin = new Vector3(-60f, -162f, -60f);
    const string Root = "SizeRuneTest";
    const float LaneW = 4f, LaneL = 32f, RuneY = 1.7f, RuneR = 1.15f;

    [MenuItem("FuwaCourse/ステージ2/2-5 大きさの魔法のお試しを作る")]
    public static string Build()
    {
        Remove();
        var stone = FuwaStage2WaterTest.ToonMat("Ruins_Stone", new Color(0.86f, 0.74f, 0.62f));
        var terra = FuwaStage2WaterTest.ToonMat("Ruins_Terracotta", new Color(0.82f, 0.48f, 0.33f));
        var terraD = FuwaStage2WaterTest.ToonMat("Ruins_TerracottaDark", new Color(0.62f, 0.34f, 0.24f));
        var iron = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var root = new GameObject(Root).transform; root.position = Origin;

        // 床（当たりあり、ふわふわがさわったらアウト＝いつもと同じ）
        var fl = new GameObject("Floor"); fl.transform.SetParent(root, false); fl.layer = 11; fl.transform.localPosition = new Vector3(0f, -0.4f, LaneL / 2f);
        fl.AddComponent<BoxCollider>().size = new Vector3(LaneW, 0.8f, LaneL);
        int nz = Mathf.RoundToInt(LaneL / 1.2f);
        for (int k = 0; k < nz; k++) Prim(PrimitiveType.Cube, fl.transform, "Tile", new Vector3(0f, 0f, -LaneL / 2f + (k + 0.5f) * LaneL / nz), new Vector3(LaneW, 0.8f, LaneL / nz), k % 2 == 0 ? terra : terraD);

        Rune(root, "RuneSmall", 5f, -1, new Color(0.35f, 0.75f, 1f), "青：小さく");
        Grate(root, 10f, stone, iron);
        Rune(root, "RuneBig", 15f, 1, new Color(1f, 0.55f, 0.2f), "オレンジ：大きく");
        CrossWind(root, 18f, 23f);
        Rune(root, "RuneNormal", 27f, 0, new Color(0.95f, 0.95f, 1f), "白：元に");

        // ふわふわの台・ワープ
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(0f, 1.6f, 1.5f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(-1.4f, 0f, 0.6f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false); spawn.localPosition = new Vector3(0f, 0f, 0.3f);
        var warpMat = FuwaGearTest.Mat("PortalTest_Warp", new Color(0.6f, 0.95f, 1f));
        var lobby = GameObject.Find("FuwaStage2/Lobby/LobbySpawn");
        if (lobby)
        {
            FuwaIceTestBuilder.Warp(root, "WarpToSizeTest", lobby.transform.position + new Vector3(-5f, 0.7f, 0f), "SIZE TEST", spawn, warpMat);
            FuwaIceTestBuilder.Warp(root, "WarpBack", root.TransformPoint(new Vector3(1.4f, 0.7f, 0.2f)), "BACK", lobby.transform, warpMat);
        }
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "[大きさの魔法のお試し] 作った";
    }

    [MenuItem("FuwaCourse/ステージ2/2-5 大きさの魔法のお試しを消す")]
    public static void Remove() { var o = GameObject.Find(Root); if (o) Object.DestroyImmediate(o); }

    // ルーン：たての光る輪＋輪の中で回る魔法陣（加算の板）＋くぐる所のトリガー
    static void Rune(Transform root, string name, float z, int mode, Color col, string label)
    {
        var r = new GameObject(name).transform; r.SetParent(root, false); r.localPosition = new Vector3(0f, RuneY, z);
        var ringM = FuwaStage2WaterTest.ToonMat("Rune_Ring_" + mode, Color.Lerp(col, Color.white, 0.35f));
        ringM.SetColor("_EmissionColor", col * 1.2f); EditorUtility.SetDirty(ringM);
        var ring = new GameObject("Ring"); ring.transform.SetParent(r, false);
        ring.AddComponent<MeshFilter>().sharedMesh = Torus(RuneR, 0.075f, 48, 10);
        var rr = ring.AddComponent<MeshRenderer>(); rr.sharedMaterial = ringM;
        // 台座（輪を下からささえる2本の石の柱）
        var stone = FuwaStage2WaterTest.ToonMat("Ruins_Stone", new Color(0.86f, 0.74f, 0.62f));
        foreach (float sx in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, r, "Post", new Vector3(sx * (RuneR + 0.25f), -RuneY / 2f, 0f), new Vector3(0.3f, RuneY, 0.3f), stone);
        // 魔法陣（回る）
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad); g.name = "Glyph"; Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(r, false); g.transform.localScale = Vector3.one * (RuneR * 2f - 0.1f);
        g.GetComponent<MeshRenderer>().sharedMaterial = GlyphMat(mode, col);
        g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // くぐる所（輪の内がわの薄い円柱）
        //  トリガーは FuwaSizeRune と同じ物に（子の当たりだと OnTriggerEnter がスクリプトに届かない）
        r.gameObject.layer = 2;
        var bc = r.gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(RuneR * 1.7f, RuneR * 1.7f, 0.35f);
        // はじける粒
        var ps = new GameObject("Burst").AddComponent<ParticleSystem>(); ps.transform.SetParent(r, false);
        var main = ps.main; main.playOnAwake = false; main.loop = false; main.startLifetime = 0.8f; main.startSpeed = 2.2f; main.startSize = 0.12f; main.startColor = Color.Lerp(col, Color.white, 0.3f); main.maxParticles = 64; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = RuneR; sh.arc = 360f;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Rune_Glyph_" + mode + ".mat");
        ps.Stop();
        var sr = UdonSharpUndo.AddComponent<FuwaSizeRune>(r.gameObject);
        sr.mode = mode; sr.glyph = g.transform; sr.glows = new Renderer[] { rr }; sr.burst = ps; sr.glowColor = col;
        UdonSharpEditorUtility.CopyProxyToUdon(sr); FuwaStage2WaterTest.NoSync(sr);
    }

    // 鉄格子：レーンをふさぐ。すき間 Gap（小さいふわふわ 直径 0.17 は通る、ふつう 0.3 は通らない）
    const float BarD = 0.06f, Gap = 0.22f, GrateH = 3.6f;
    static void Grate(Transform root, float z, Material stone, Material iron)
    {
        var g = new GameObject("Grate").transform; g.SetParent(root, false); g.localPosition = new Vector3(0f, 0f, z);
        int n = Mathf.FloorToInt(LaneW / (BarD + Gap));
        float w = n * (BarD + Gap);
        for (int i = 0; i <= n; i++)
        {
            float x = -w / 2f + i * (BarD + Gap);
            var b = Prim(PrimitiveType.Cylinder, g, "Bar", new Vector3(x, GrateH / 2f, 0f), new Vector3(BarD, GrateH / 2f, BarD), iron);
            var c = new GameObject("BarHit"); c.transform.SetParent(g, false); c.layer = 17; c.transform.localPosition = b.localPosition;
            var cc = c.AddComponent<CapsuleCollider>(); cc.radius = BarD / 2f; cc.height = GrateH; cc.direction = 1;
        }
        // 横の帯（上と下）
        foreach (float y in new[] { 0.35f, GrateH - 0.1f })
            Prim(PrimitiveType.Cube, g, "Rail", new Vector3(0f, y, 0f), new Vector3(w + 0.1f, 0.08f, 0.1f), iron);
        foreach (float sx in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, g, "GratePost", new Vector3(sx * (w / 2f + 0.25f), GrateH / 2f, 0f), new Vector3(0.4f, GrateH + 0.2f, 0.5f), stone);
        // はね返す（アウトにしない）：格子全体の当たりに FuwaBounce
        foreach (Transform c in g) if (c.name == "BarHit")
        {
            var bo = UdonSharpUndo.AddComponent<FuwaBounce>(c.gameObject);
            bo.alwaysUp = false; bo.useContactNormal = true; bo.bounceSpeed = 1.2f; bo.keepTangent = 0.3f;
            UdonSharpEditorUtility.CopyProxyToUdon(bo); FuwaStage2WaterTest.NoSync(bo);
        }
    }

    // 強い横風（+x へ、レーンの外へ吹き出す）
    static void CrossWind(Transform root, float z0, float z1)
    {
        var w = new GameObject("CrossWind"); w.transform.SetParent(root, false); w.layer = 2;
        w.transform.localPosition = new Vector3(0f, 2f, (z0 + z1) / 2f);
        var bc = w.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(LaneW + 2f, 4f, z1 - z0);
        var wa = UdonSharpUndo.AddComponent<FuwaWindArea>(w);
        wa.localDirection = Vector3.right; wa.strength = 3.2f; wa.offSeconds = 0f; wa.baseLevel = 1f;
        UdonSharpEditorUtility.CopyProxyToUdon(wa); FuwaStage2WaterTest.NoSync(wa);
        // 目印：風の筋（うすい板）
        var m = FuwaGearTest.Mat("SizeTest_WindMark", new Color(0.8f, 0.95f, 1f));
        for (int k = 0; k < 5; k++)
        {
            var s = Prim(PrimitiveType.Cube, w.transform, "Streak", new Vector3(-1.5f + k * 0.8f, -1.2f + (k % 3) * 0.9f, -1.6f + k * 0.8f), new Vector3(1.2f, 0.03f, 0.03f), m);
        }
    }

    static Mesh Torus(float R, float r, int n, int m)
    {
        string path = "Assets/FuwaStage2/Meshes/GearTest/Rune_Torus.asset";
        var v = new List<Vector3>(); var nr = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float a = 2f * Mathf.PI * i / n; var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= m; j++)
            {
                float b = 2f * Mathf.PI * j / m; var d = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                v.Add(c * R + d * r); nr.Add(d);
            }
        }
        for (int i = 0; i < n; i++) for (int j = 0; j < m; j++)
        {
            int a = i * (m + 1) + j, b = a + m + 1;
            t.Add(a); t.Add(a + 1); t.Add(b); t.Add(b); t.Add(a + 1); t.Add(b + 1);
        }
        var mesh = new Mesh { name = "Rune_Torus" }; mesh.SetVertices(v); mesh.SetNormals(nr); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
        // 表裏：外向きの法線と三角形の向きをそろえる
        var tt = mesh.triangles; var vv = mesh.vertices;
        if (Vector3.Dot(Vector3.Cross(vv[tt[1]] - vv[tt[0]], vv[tt[2]] - vv[tt[0]]), nr[tt[0]]) < 0f) { for (int q = 0; q < tt.Length; q += 3) { int s = tt[q + 1]; tt[q + 1] = tt[q + 2]; tt[q + 2] = s; } mesh.triangles = tt; }
        return FuwaGearRideTest.SaveMeshSub(mesh);
    }

    // 魔法陣のテクスチャ（加算）：二重の円・目盛り・まわりの小さな記号・まん中の印（小さく＝内向きの矢、大きく＝外向き、元に＝丸）
    static Material GlyphMat(int mode, Color col)
    {
        string tp = "Assets/FuwaStage2/Textures/Rune_Glyph_" + mode + ".png";
        int N = 256; var px = new Color[N * N];
        float Ring(float d, float r0, float w) => Mathf.Clamp01(1f - Mathf.Abs(d - r0) / w);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f; float d = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
            float c = Ring(d, 0.93f, 0.025f) + Ring(d, 0.80f, 0.018f) * 0.8f + Ring(d, 0.42f, 0.02f) * 0.7f;
            // 目盛り（二つの円の間）
            float tick = Mathf.Repeat(a / (2f * Mathf.PI) * 24f, 1f);
            if (d > 0.81f && d < 0.92f && tick < 0.12f) c += 0.8f;
            // 小さな記号（8つ、ひし形）
            for (int k = 0; k < 8; k++)
            {
                float ak = k * Mathf.PI / 4f + Mathf.PI / 8f; float cx = Mathf.Cos(ak) * 0.62f, cy = Mathf.Sin(ak) * 0.62f;
                float dd = Mathf.Abs(u - cx) + Mathf.Abs(v - cy);
                c += Mathf.Clamp01(1f - Mathf.Abs(dd - 0.07f) / 0.015f) * 0.9f;
            }
            // まん中の印
            if (mode != 0)
                for (int k = 0; k < 4; k++)
                {
                    float ak = k * Mathf.PI / 2f; var dir = new Vector2(Mathf.Cos(ak), Mathf.Sin(ak));
                    var p = new Vector2(u, v); float along = Vector2.Dot(p, dir), side = Mathf.Abs(Vector2.Dot(p, new Vector2(-dir.y, dir.x)));
                    // 矢印：小さく＝先が内向き（中心寄りがとがる）、大きく＝外向き
                    float tipR = mode < 0 ? 0.12f : 0.34f, baseR = mode < 0 ? 0.34f : 0.12f;
                    if (along > Mathf.Min(tipR, baseR) && along < Mathf.Max(tipR, baseR))
                    {
                        float f = Mathf.InverseLerp(baseR, tipR, along);   // 0=根元 1=先
                        float half = 0.09f * (1f - f);
                        c += Mathf.Clamp01(1f - Mathf.Abs(side - half) / 0.012f) * 0.9f;
                    }
                }
            else c += Ring(d, 0.18f, 0.03f);
            c = Mathf.Clamp01(c);
            px[y * N + x] = new Color(c, c, c, c);
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true); tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(tp, tex.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        string mp = "Assets/FuwaStage2/Materials/Rune_Glyph_" + mode + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!m) { m = new Material(Shader.Find("Legacy Shaders/Particles/Additive")); AssetDatabase.CreateAsset(m, mp); }
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp)); m.SetColor("_TintColor", col * 0.55f);
        EditorUtility.SetDirty(m); return m;
    }

    static Transform Prim(PrimitiveType t, Transform parent, string name, Vector3 lp, Vector3 ls, Material m)
    {
        var g = GameObject.CreatePrimitive(t); Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<MeshRenderer>().sharedMaterial = m; return g.transform;
    }
}
