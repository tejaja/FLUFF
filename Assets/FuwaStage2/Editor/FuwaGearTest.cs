using UnityEditor;
using UnityEngine;

// 2–4 はぐるまのとう の歯車お試し（2026-11-06）
// ・歯の形はインボリュート（圧力角 20°）。かみ合う物はみんな同じモジュール m
// ・ピッチ半径 r = m z / 2、歯先 r + m、歯元 r - 1.25m、中心距離 = m (z1 + z2) / 2
// ・かみ合う2枚は逆回り、角速度は歯数の逆比。最初の角度は「A の歯が B を向いた時、B は歯のすき間が A を向く」でそろえる
// ・角度は 2D（x→z へ反時計回り）で計算して、Unity の Y 回転には符号を反転して入れる
public static class FuwaGearTest
{
    const string MatDir = "Assets/FuwaStage2/Materials/";
    const string MeshDir = "Assets/FuwaStage2/Meshes/GearTest/";
    public const float M = 0.5f;          // モジュール（歯の大きさ）。歯の高さ 2.25m = 1.1m くらい
    public const float Alpha = 20f * Mathf.Deg2Rad;
    public const float Thick = 0.8f;      // 歯車の厚み
    public const float Backlash = 0.04f;  // 歯のあそび（ピッチ円で歯を少し細く。本物の歯車もすき間がある）
    static readonly Vector3 Origin = new Vector3(-40f, -162f, -70f);

    // 歯車のつながり
    public class Gear { public string name; public int z; public Vector2 c; public float ratio; public float phase; public float y; }
    public static Gear[] Gears;
    public static float RackPinionR;
    public static Vector2 RackPinionC;

    static void Layout()
    {
        // G1（30枚、元）→ G2（18枚）→ G3（2段：大24枚 + 同じ軸の小18枚）→ G4（36枚、G3 の小とかみ合う、上の段）
        var g1 = new Gear { name = "G1_z30", z = 30, c = Vector2.zero, ratio = 1f, phase = 0f };
        var g2 = Mesh2(g1, "G2_z18", 18, 0f);
        var g3 = Mesh2(g2, "G3_z24", 24, 70f);
        var g3s = new Gear { name = "G3s_z18", z = 18, c = g3.c, ratio = g3.ratio, phase = 7f * Mathf.Deg2Rad, y = Thick };   // 同じ軸（同じ回り方）
        var g4 = Mesh2(g3s, "G4_z36", 36, 60f); g4.y = Thick;
        Gears = new[] { g1, g2, g3, g3s, g4 };

        // ラック＆ピニオン（別の所、行ったり来たり）
        RackPinionR = M * 20 / 2f;
        RackPinionC = new Vector2(-22f, 6f);
    }

    // a とかみ合う z 枚の歯車を、a から dirDeg の向きに置く
    public static Gear Mesh2(Gear a, string name, int z, float dirDeg, float m = -1f)
    {
        if (m <= 0f) m = M;
        float psi = dirDeg * Mathf.Deg2Rad;
        float dist = m * (a.z + z) / 2f;
        var g = new Gear { name = name, z = z, c = a.c + new Vector2(Mathf.Cos(psi), Mathf.Sin(psi)) * dist, y = a.y };
        // θb = ψ + π + π/zb − (za/zb)(θa − ψ)、θa = ratio_a·T + phase_a
        g.ratio = -a.ratio * a.z / z;
        g.phase = psi + Mathf.PI + Mathf.PI / z - (float)a.z / z * (a.phase - psi);
        return g;
    }

    public static float Inv(float a) => Mathf.Tan(a) - a;

    // 歯車の輪郭（中心から見て星形なので扇で三角形にできる）。角度0に歯の真ん中
    // m：モジュール（省略で M）。a0〜a1（ラジアン）を渡すと、その角度の範囲の歯だけの扇形（跳ね橋の歯車用、中心まで埋める）
    public static Vector2[] GearOutline(int z, int flankSteps = 10, float m = -1f, float a0 = 0f, float a1 = 0f)
    {
        if (m <= 0f) m = M;
        bool sector = a1 > a0;
        float r = m * z / 2f, rb = r * Mathf.Cos(Alpha), ra = r + m, rf = Mathf.Max(r - 1.25f * m, 0.1f);
        float half = (Mathf.PI * m / 2f - Backlash) / (2f * r) + Inv(Alpha);   // 基礎円での歯の半分の角度（あそびの分だけ細く）
        float pitch = 2f * Mathf.PI / z;
        var flank = new System.Collections.Generic.List<Vector2>(); // (半径, 歯の中心からの角度) 右側の歯面、根元→歯先
        float r0 = Mathf.Max(rf, rb);
        if (rf < rb) flank.Add(new Vector2(rf, half));    // 基礎円より下はまっすぐ
        for (int i = 0; i <= flankSteps; i++)
        {
            float rr = Mathf.Lerp(r0, ra, i / (float)flankSteps);
            float phi = Mathf.Acos(Mathf.Clamp(rb / rr, -1f, 1f));
            flank.Add(new Vector2(rr, half - Inv(phi)));
        }
        float tipHalf = flank[flank.Count - 1].y;
        float rootHalf = pitch / 2f - half;              // 歯元のすき間の半分
        var pts = new System.Collections.Generic.List<Vector2>();
        for (int k = 0; k < z; k++)
        {
            float c = k * pitch;
            if (sector)
            {
                // 範囲の中の歯だけ（a0 から順に並ぶように角度をそろえる）
                c = a0 + Mathf.Repeat(-a0, pitch) + k * pitch;
                if (c + pitch / 2f > a1) break;
                if (c - pitch / 2f < a0) continue;
            }
            // 歯元（前の歯との間）
            for (int s = 0; s < 3; s++) pts.Add(P(rf, c - half - rootHalf + rootHalf * s / 3f));
            // 左の歯面（根元→歯先）＝ 角度は c - flank
            for (int i = 0; i < flank.Count; i++) pts.Add(P(flank[i].x, c - flank[i].y));
            // 歯先
            for (int s = 1; s < 3; s++) pts.Add(P(ra, c - tipHalf + 2f * tipHalf * s / 3f));
            // 右の歯面（歯先→根元）
            for (int i = flank.Count - 1; i >= 0; i--) pts.Add(P(flank[i].x, c + flank[i].y));
            // 歯元（次の歯との間）
            for (int s = 1; s < 3; s++) pts.Add(P(rf, c + half + rootHalf * s / 3f));
        }
        if (sector) pts.Add(Vector2.zero);   // 扇の中心へ戻る
        return pts.ToArray();
    }

    static Vector2 P(float r, float a) => new Vector2(r * Mathf.Cos(a), r * Mathf.Sin(a));

    // 輪郭を上下にのばしたメッシュ（x,z 平面、厚み Thick）。側面は面ごとに頂点を分けて角を立てる
    public static Mesh Extrude(Vector2[] o, float thick, string name, Vector2 center2D)
    {
        var v = new System.Collections.Generic.List<Vector3>();
        var t = new System.Collections.Generic.List<int>();
        int n = o.Length;
        // 上
        int top = v.Count; v.Add(new Vector3(center2D.x, thick, center2D.y));
        foreach (var p in o) v.Add(new Vector3(p.x, thick, p.y));
        for (int i = 0; i < n; i++) { t.Add(top); t.Add(top + 1 + (i + 1) % n); t.Add(top + 1 + i); }
        // 下
        int bot = v.Count; v.Add(new Vector3(center2D.x, 0f, center2D.y));
        foreach (var p in o) v.Add(new Vector3(p.x, 0f, p.y));
        for (int i = 0; i < n; i++) { t.Add(bot); t.Add(bot + 1 + i); t.Add(bot + 1 + (i + 1) % n); }
        // 横（2026-11-07 軽くした：なめらかな所はとなりの面と点を共有、35°より曲がる角だけ分ける）
        var sA = new int[n]; var sB = new int[n];
        for (int i = 0; i < n; i++)
        {
            var e0 = o[i] - o[(i - 1 + n) % n]; var e1 = o[(i + 1) % n] - o[i];
            sA[i] = v.Count; v.Add(new Vector3(o[i].x, 0f, o[i].y)); v.Add(new Vector3(o[i].x, thick, o[i].y));
            if (Vector2.Angle(e0, e1) > 35f) { sB[i] = v.Count; v.Add(new Vector3(o[i].x, 0f, o[i].y)); v.Add(new Vector3(o[i].x, thick, o[i].y)); } else sB[i] = sA[i];
        }
        for (int i = 0; i < n; i++)
        {
            int a0 = sA[i], b0 = sB[(i + 1) % n];   // a0/b0：下、+1：上（前と同じ巻き：a下, a上, b上 / a下, b上, b下）
            t.Add(a0); t.Add(a0 + 1); t.Add(b0 + 1); t.Add(a0); t.Add(b0 + 1); t.Add(b0);
        }
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ラックは凸じゃないので、歯と土台を分けて三角形に（土台の四角＋歯ごとの台形）
    static Mesh RackMesh(int teeth, string name)
    {
        float p = Mathf.PI * M, ha = M, hf = 1.25f * M, ta = Mathf.Tan(Alpha);
        float x0 = -teeth * p / 2f;
        var outline = new System.Collections.Generic.List<Vector2[]>();
        outline.Add(new[] { new Vector2(x0, -hf - 1f), new Vector2(x0 + teeth * p, -hf - 1f), new Vector2(x0 + teeth * p, -hf), new Vector2(x0, -hf) });
        for (int k = 0; k < teeth; k++)
        {
            float c = x0 + (k + 0.5f) * p;
            float q4 = p / 4f - Backlash / 2f;
            outline.Add(new[] { new Vector2(c - q4 - hf * ta, -hf), new Vector2(c + q4 + hf * ta, -hf), new Vector2(c + q4 - ha * ta, ha), new Vector2(c - q4 + ha * ta, ha) });
        }
        var cis = new CombineInstance[outline.Count];
        for (int i = 0; i < outline.Count; i++)
        {
            var q = outline[i]; var cen = (q[0] + q[1] + q[2] + q[3]) / 4f;
            // 凸の四角なので重心から扇で（反時計回りの並び）
            cis[i] = new CombineInstance { mesh = Extrude(q, Thick, "q", cen), transform = Matrix4x4.identity };
        }
        var m = new Mesh { name = name }; m.CombineMeshes(cis, true); m.RecalculateBounds();
        return m;
    }

    public static Material Mat(string name, Color c)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/ToonLit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c); EditorUtility.SetDirty(m);
        return m;
    }

    public static Mesh SaveMesh(Mesh m)
    {
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Meshes/GearTest")) AssetDatabase.CreateFolder("Assets/FuwaStage2/Meshes", "GearTest");
        string path = MeshDir + m.name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(m, path); return m; }
        old.Clear(false);
        old.SetVertices(m.vertices); old.SetTriangles(m.triangles, 0); old.SetNormals(m.normals); old.RecalculateBounds();
        EditorUtility.SetDirty(old); return old;
    }

    const int RackTeeth = 14;

    [MenuItem("FuwaCourse/ステージ2/歯車のお試しを作る")]
    public static void Build()
    {
        Remove();
        Layout();
        var root = new GameObject("GearTest").transform;
        root.position = Origin;
        var colA = Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));      // #5C626E
        var colB = Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var colMark = Mat("GearTest_Mark", new Color(0.95f, 0.72f, 0.25f));
        var axle = Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));

        foreach (var g in Gears)
        {
            var go = new GameObject(g.name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(g.c.x, g.y, g.c.y);
            var mesh = SaveMesh(Extrude(GearOutline(g.z), Thick, "Gear_m05_z" + g.z, Vector2.zero));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = (g.name.StartsWith("G2") || g.name.StartsWith("G3s") || g.name.StartsWith("G4")) ? colB : colA;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            // 回ってるのがわかる印（歯の1本の根元に黄色い点）
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.name = "Mark"; mark.transform.SetParent(go.transform, false);
            float rm = M * g.z / 2f - 1.6f * M;
            mark.transform.localPosition = new Vector3(rm, Thick + 0.02f, 0f);
            mark.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            mark.GetComponent<MeshRenderer>().sharedMaterial = colMark;
            // 軸
            var ax = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(ax.GetComponent<Collider>());
            ax.name = "Axle"; ax.transform.SetParent(go.transform, false);
            ax.transform.localPosition = new Vector3(0f, Thick * 0.5f, 0f);
            ax.transform.localScale = new Vector3(0.9f, Thick * 0.5f + 0.15f, 0.9f);
            ax.GetComponent<MeshRenderer>().sharedMaterial = axle;
        }

        // ラック＆ピニオン
        {
            var pin = new GameObject("Pinion_z20");
            pin.transform.SetParent(root, false);
            pin.transform.localPosition = new Vector3(RackPinionC.x, 0f, RackPinionC.y);
            var pm = SaveMesh(Extrude(GearOutline(20), Thick, "Gear_m05_z20", Vector2.zero));
            pin.AddComponent<MeshFilter>().sharedMesh = pm;
            pin.AddComponent<MeshRenderer>().sharedMaterial = colB;
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.name = "Mark"; mark.transform.SetParent(pin.transform, false);
            mark.transform.localPosition = new Vector3(RackPinionR - 1.6f * M, Thick + 0.02f, 0f);
            mark.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            mark.GetComponent<MeshRenderer>().sharedMaterial = colMark;

            var rack = new GameObject("Rack");
            rack.transform.SetParent(root, false);
            var rm = SaveMesh(RackMesh(RackTeeth, "Rack_m05"));
            rack.AddComponent<MeshFilter>().sharedMesh = rm;
            rack.AddComponent<MeshRenderer>().sharedMaterial = colA;
        }

        SetTime(0f);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[歯車のお試し] 作った");
    }

    [MenuItem("FuwaCourse/ステージ2/歯車のお試しを消す")]
    public static void Remove()
    {
        var old = GameObject.Find("GearTest");
        if (old) Object.DestroyImmediate(old);
    }

    public const float DriveDegPerSec = 18f;   // G1 の回る速さ

    // 時刻 T 秒の角度にする（エディタで動画を撮る用）
    public static void SetTime(float T)
    {
        if (Gears == null) Layout();
        var root = GameObject.Find("GearTest"); if (!root) return;
        float w = DriveDegPerSec * Mathf.Deg2Rad;
        foreach (var g in Gears)
        {
            var tr = root.transform.Find(g.name); if (!tr) continue;
            float th = g.ratio * w * T + g.phase;
            tr.localRotation = Quaternion.Euler(0f, -th * Mathf.Rad2Deg, 0f);
        }
        // ピニオン：行ったり来たり（±100°、8秒で1往復）。歯の真ん中が真下（-z 側…ではなく +z 側にラック）
        // ラックはピニオンの -z 側、歯は +z 向き。ピニオンの歯が -90° を向いた時、ラックはすき間が x=0
        var pin = root.transform.Find("Pinion_z20"); var rack = root.transform.Find("Rack");
        if (pin && rack)
        {
            float amp = 100f * Mathf.Deg2Rad;
            float th = amp * Mathf.Sin(T * 2f * Mathf.PI / 8f);
            float th0 = -Mathf.PI / 2f;   // 歯が真下（-z）
            pin.localRotation = Quaternion.Euler(0f, -(th0 + th) * Mathf.Rad2Deg, 0f);
            // 2D 反時計回りで真下の点は +x へ動く → ラックの x = r θ。ラックは歯が偶数本なので x=0 がすき間の真ん中
            float s = RackPinionR * th;
            rack.localPosition = new Vector3(RackPinionC.x + s, 0f, RackPinionC.y - RackPinionR);
        }
    }
}
