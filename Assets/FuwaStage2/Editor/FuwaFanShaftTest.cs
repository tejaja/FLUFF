using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4 はぐるまのとう：ファンの上昇気流のお試し（2026-11-06）
// 床の格子の下の穴に、横向きの大きいファン。モーター（横の軸）→ かさ歯車（16枚 → 32枚、軸が 90° 曲がる）→ ファン（たての軸）
// ・かさ歯車：ピッチ円すいの頂点を2つの歯車で共有。tan δ1 = z1/z2、δ1 + δ2 = 90°。歯の形は外端（背円すい）にふつうの歯形を置いて、頂点へ向かって縮める（近似）
// ・回る速さ：ω1 z1 = ω2 z2。どちらも FuwaSpinner（サーバー時刻）で回すので、ずっとかみ合ったまま
// ・上昇気流は 2–2 の Updraft1（ふわふわ用の風＋人用の上昇気流）を写して高さ 10m に
public static class FuwaFanShaftTest
{
    const float Mod = 0.08f; const int Z1 = 16, Z2 = 32;
    const float FanSpeed = 240f;     // ファンの回る速さ（度/秒）
    const float PitHalf = 2.6f, PitDepth = 2.8f, Lift = 10f;
    static readonly Vector3 Origin = new Vector3(-12f, -162f, -104f);   // 穴のまん中、床の上の高さ

    [MenuItem("FuwaCourse/ステージ2/ファンの上昇気流のお試しを作る")]
    public static void Build()
    {
        Remove();
        var dark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var axleM = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var floorM = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));
        var bladeM = FuwaGearTest.Mat("FanTest_Blade", new Color(0.55f, 0.82f, 0.95f));
        var pitM = FuwaGearTest.Mat("FanShaftTest_Pit", new Color(0.3f, 0.32f, 0.37f));

        var root = new GameObject("FanShaftTest").transform; root.position = Origin;
        // 床：穴のまわり（12×12 の床に 5.2×5.2 の穴）。西のはしは跳ね橋の向こうの床（x=-18）につながる
        float F = 6f;
        Floor(root, "FloorN", new Vector3(0f, 0f, (PitHalf + F) / 2f), new Vector2(2f * F, F - PitHalf), floorM);
        Floor(root, "FloorS", new Vector3(0f, 0f, -(PitHalf + F) / 2f), new Vector2(2f * F, F - PitHalf), floorM);
        Floor(root, "FloorW", new Vector3(-(PitHalf + F) / 2f, 0f, 0f), new Vector2(F - PitHalf, 2f * PitHalf), floorM);
        Floor(root, "FloorE", new Vector3((PitHalf + F) / 2f, 0f, 0f), new Vector2(F - PitHalf, 2f * PitHalf), floorM);
        // 穴の壁と底
        foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            var c = d * (PitHalf + 0.1f) + Vector3.down * (PitDepth / 2f + 0.5f);
            var s = d.x != 0f ? new Vector3(0.2f, PitDepth - 1f, 2f * PitHalf + 0.4f) : new Vector3(2f * PitHalf + 0.4f, PitDepth - 1f, 0.2f);
            Prim(PrimitiveType.Cube, root, "PitWall", c, s, pitM);
        }
        Prim(PrimitiveType.Cube, root, "PitBottom", new Vector3(0f, -PitDepth - 0.1f, 0f), new Vector3(2f * PitHalf + 0.4f, 0.2f, 2f * PitHalf + 0.4f), pitM);
        // 格子（歩ける。ふわふわは上昇気流の中なので当たらない）
        var grille = new GameObject("Grille"); grille.transform.SetParent(root, false); grille.layer = 11;
        for (int i = -6; i <= 6; i++)
        {
            float x = i * (2f * PitHalf / 13f);
            Prim(PrimitiveType.Cube, grille.transform, "BarX" + i, new Vector3(x, -0.06f, 0f), new Vector3(0.06f, 0.12f, 2f * PitHalf), light);
            Prim(PrimitiveType.Cube, grille.transform, "BarZ" + i, new Vector3(0f, -0.09f, x), new Vector3(2f * PitHalf, 0.06f, 0.06f), light);
        }
        var gc = grille.AddComponent<BoxCollider>(); gc.center = new Vector3(0f, -0.06f, 0f); gc.size = new Vector3(2f * PitHalf, 0.12f, 2f * PitHalf);

        // かさ歯車の寸法
        float d1 = Mathf.Atan2(Z1, Z2), d2 = Mathf.PI / 2f - d1;   // ピッチ円すいの半分の角
        float rp1 = Mod * Z1 / 2f, rp2 = Mod * Z2 / 2f;
        float A = rp2 / Mathf.Sin(d2);                               // 頂点からピッチ円までの長さ
        float face = 0.32f * A;
        float yF = -2.2f;                                            // ファンの歯車のピッチ円の高さ
        Vector3 apex = new Vector3(0f, yF + A * Mathf.Cos(d2), 0f);

        // ファン（たて軸）：歯車＋軸＋羽根をまとめて回す
        var fanShaft = new GameObject("FanShaft").transform; fanShaft.SetParent(root, false); fanShaft.localPosition = new Vector3(0f, yF, 0f);
        var g2 = new GameObject("Bevel_z32").transform; g2.SetParent(fanShaft, false);
        g2.gameObject.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(BevelMesh(Z2, d2, A, face, "FanShaft_Bevel_z32"));
        g2.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        Prim(PrimitiveType.Cylinder, fanShaft, "Shaft", new Vector3(0f, (-0.5f - yF) / 2f, 0f), new Vector3(0.14f, (-0.5f - yF) / 2f + 0.3f, 0.14f), axleM);
        var blades = new GameObject("Blades").transform; blades.SetParent(fanShaft, false); blades.localPosition = new Vector3(0f, -0.55f - yF, 0f);
        Prim(PrimitiveType.Cylinder, blades, "Hub", Vector3.zero, new Vector3(0.6f, 0.12f, 0.6f), brass);
        for (int k = 0; k < 6; k++)
        {
            var bl = new GameObject("Blade" + k).transform; bl.SetParent(blades, false); bl.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
            Prim(PrimitiveType.Sphere, bl, "V", new Vector3(1.25f, 0f, 0f), new Vector3(2.0f, 0.05f, 0.7f), bladeM).localRotation = Quaternion.Euler(24f, 0f, 0f);
        }
        var sp2 = UdonSharpUndo.AddComponent<FuwaSpinner>(fanShaft.gameObject);
        sp2.axis = Vector3.up; sp2.degPerSec = FanSpeed; sp2.phase = 0f;
        UdonSharpEditorUtility.CopyProxyToUdon(sp2); FuwaStage2WaterTest.NoSync(sp2);
        // 軸受け（穴の底から）
        Prim(PrimitiveType.Cylinder, root, "Bearing", new Vector3(0f, (-PitDepth + yF) / 2f, 0f), new Vector3(0.4f, (yF + PitDepth) / 2f, 0.4f), dark);

        // モーターの歯車（横の軸、+X 側）。ローカル Y（歯車の軸、頂点の向き）→ ワールド -X
        Vector3 M = apex + Vector3.right * (A * Mathf.Cos(d1));
        var g1 = new GameObject("Bevel_z16").transform; g1.SetParent(root, false); g1.localPosition = M;
        g1.localRotation = Quaternion.Euler(0f, 0f, 90f);
        g1.gameObject.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(BevelMesh(Z1, d1, A, face, "FanShaft_Bevel_z16"));
        g1.gameObject.AddComponent<MeshRenderer>().sharedMaterial = dark;
        Prim(PrimitiveType.Cylinder, g1, "Shaft", new Vector3(0f, -0.5f, 0f), new Vector3(0.1f, 0.55f, 0.1f), axleM);
        // ω1 z1 = ω2 z2。モーターは自分の軸（ワールド -X）まわりに -ω1（かみ合う所が同じ向きに動く）。最初の角度は歯半分ずらす（すき間がファンの歯に向く）
        var sp1 = UdonSharpUndo.AddComponent<FuwaSpinner>(g1.gameObject);
        sp1.axis = Vector3.up; sp1.degPerSec = -FanSpeed * Z2 / Z1; sp1.phase = -180f / Z1;
        UdonSharpEditorUtility.CopyProxyToUdon(sp1); FuwaStage2WaterTest.NoSync(sp1);
        // モーター本体（穴の東の壁の外）
        var motorC = new Vector3(PitHalf + 0.7f, M.y, 0f);
        Prim(PrimitiveType.Cylinder, root, "MotorShaft", (M + motorC) / 2f, new Vector3(0.1f, (motorC.x - M.x) / 2f, 0.1f), axleM).localRotation = Quaternion.Euler(0f, 0f, 90f);
        Prim(PrimitiveType.Cylinder, root, "Motor", motorC, new Vector3(0.7f, 0.45f, 0.7f), light).localRotation = Quaternion.Euler(0f, 0f, 90f);

        // 上昇気流：2–2 の Updraft1 を写して、格子の上から 10m
        var src = GameObject.Find("FuwaCourse2/Gimmick_Updrafts/Updraft1");
        if (src)
        {
            var up = Object.Instantiate(src, root); up.name = "Updraft";
            up.transform.localRotation = Quaternion.identity;
            var zone = up.transform.Find("Zone");
            var bc = zone.GetComponent<BoxCollider>();
            // 今の大きさ（ワールド）→ ほしい大きさ（3.4 × 10 × 3.4）になるように親を伸ばす
            Vector3 cur = Vector3.Scale(bc.size, zone.lossyScale);
            var ps = up.transform.localScale;
            up.transform.localScale = new Vector3(ps.x * 3.4f / cur.x, ps.y * Lift / cur.y, ps.z * 3.4f / cur.z);
            // 風の箱の下のはしを格子の高さ、まん中を穴のまん中に
            Physics.SyncTransforms();
            var b = zone.GetComponent<BoxCollider>().bounds;
            up.transform.position += new Vector3(Origin.x - b.center.x, Origin.y + 0.05f - b.min.y, Origin.z - b.center.z);
            foreach (var u in up.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(u);
        }

        // 上の床（てっぺんの東どなり）
        Floor(root, "TopFloor", new Vector3(PitHalf + 3.2f, Lift, 0f), new Vector2(5f, 6f), floorM);

        // ふわふわを出す台
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(-4f, 1.6f, 0f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(-4f, 0f, 3.5f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ファンの上昇気流のお試し] 作った δ1={d1 * Mathf.Rad2Deg:F1}° δ2={d2 * Mathf.Rad2Deg:F1}° A={A:F3}");
    }

    // かさ歯車：ローカル Y が軸、頂点は +Y（高さ A cosδ）、ピッチ円は y=0 で半径 A sinδ。
    // 外端（背円すい）にふつうの歯形（歯数 z）を置く：歯の高さ方向は背円すいの母線（cosδ, sinδ）。内端は頂点へ向かって (A−face)/A に縮める
    static Mesh BevelMesh(int z, float delta, float A, float face, string name)
    {
        var o = FuwaGearTest.GearOutline(z, 8, Mod);
        float rp = A * Mathf.Sin(delta);
        Vector3 apex = new Vector3(0f, A * Mathf.Cos(delta), 0f);
        float k = (A - face) / A;
        int n = o.Length;
        var heel = new Vector3[n]; var toe = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float r = o[i].magnitude, th = Mathf.Atan2(o[i].y, o[i].x);
            float h = r - rp;
            float rad = rp + h * Mathf.Cos(delta), ax = h * Mathf.Sin(delta);
            heel[i] = new Vector3(rad * Mathf.Cos(th), ax, rad * Mathf.Sin(th));
            toe[i] = apex + (heel[i] - apex) * k;
        }
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        // 歯の面（外端 → 内端）
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n, s = v.Count;
            v.Add(heel[i]); v.Add(heel[j]); v.Add(toe[j]); v.Add(toe[i]);
            t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
        }
        // 外端のふた・内端のふた（軸の上の点から扇で）
        int hc = v.Count; v.Add(Vector3.zero); for (int i = 0; i < n; i++) v.Add(heel[i]);
        for (int i = 0; i < n; i++) { t.Add(hc); t.Add(hc + 1 + i); t.Add(hc + 1 + (i + 1) % n); }
        int tc = v.Count; v.Add(apex * (1f - k)); for (int i = 0; i < n; i++) v.Add(toe[i]);
        for (int i = 0; i < n; i++) { t.Add(tc); t.Add(tc + 1 + (i + 1) % n); t.Add(tc + 1 + i); }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    [MenuItem("FuwaCourse/ステージ2/ファンの上昇気流のお試しを消す")]
    public static void Remove() { var o = GameObject.Find("FanShaftTest"); if (o) Object.DestroyImmediate(o); }

    // エディタで動画を撮る用（FuwaSpinner と同じ計算）
    public static void SetTime(float sec)
    {
        var o = GameObject.Find("FanShaftTest"); if (!o) return;
        foreach (var s in o.GetComponentsInChildren<FuwaSpinner>(true))
        {
            float a = Mathf.Repeat(sec * s.degPerSec + s.phase, 360f);
            Quaternion b = s.transform.name.StartsWith("Bevel_z16") ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            s.transform.localRotation = b * Quaternion.AngleAxis(a, s.axis);
        }
    }

    static void Floor(Transform root, string name, Vector3 c, Vector2 size, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.layer = 11;
        g.transform.localPosition = c + new Vector3(0f, -0.5f, 0f);
        Prim(PrimitiveType.Cube, g.transform, "Visual", Vector3.zero, new Vector3(size.x, 1f, size.y), m);
        g.AddComponent<BoxCollider>().size = new Vector3(size.x, 1f, size.y);
    }

    static Transform Prim(PrimitiveType t, Transform parent, string name, Vector3 lp, Vector3 ls, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g.transform;
    }
}
