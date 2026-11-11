using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 2–4 の羽根車のかごの床の見た目（2026-11-11 てじゃ「羽がおいてあるあたりの床もなんかほしい」「縞鋼板じゃ被る」→ C案「いいね」）
//  ・床（CageFloor・CageRamp・CageApron）：パンチングメタル（丸い穴のならんだ鉄板）。uv はメートル（どの大きさの板でも穴の大きさがそろう）
//  ・CageFloor のふち：黄色×黒の注意ライン（橋の先と同じ Course2_4_Hazard）。上の面を内がわ＋ふちの帯に分けた1つのメッシュ（重ねないのでちらつかない）
//  ・羽根車の軸のまわり：黄色い円＋両回りの矢印（撃つ側で回る向きが変わるので両方）。床の上 1cm の板
//  当たりは今のまま（Visual の見た目だけ差しかえ）。FuwaCourse2_4Builder.Build の最後で呼ぶ
public static class FuwaCageFloorLook
{
    const string MeshDir = "Assets/FuwaStage2/Meshes/Course2_4";
    public static float Border = 0.3f, MarkR = 0.55f, MarkW = 0.08f;   // 円は軸から床のふちまで 1m しかないので、注意ラインの帯（0.3m）の内がわに収まる大きさ

    [MenuItem("FuwaCourse/ステージ2/2–4 羽根車のかごの床の見た目をつけ直す")]
    public static void Menu() { Debug.Log(Apply(GameObject.Find("FuwaCourse2_4")?.transform)); }

    public static string Apply(Transform course)
    {
        if (!course) return "2–4 がない";
        var punch = PunchMat(); var hazard = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_Hazard.mat");
        var markM = FuwaStage2WaterTest.ToonMat("Course2_4_CageMark", new Color(1f, 0.78f, 0.22f));
        int n = 0;
        foreach (var F in course.GetComponentsInChildren<Transform>(true).Where(t => t.name == "CageFloor" || t.name == "CageRamp" || t.name == "CageApron").ToList())
        {
            var vis = F.Find("Visual"); if (!vis) continue;
            bool border = F.name == "CageFloor" && hazard;
            var s = vis.lossyScale; var ls = vis.localScale;
            var mesh = FloorMesh(F.parent.name + "_" + F.name, ls, border ? Border : 0f);
            vis.GetComponent<MeshFilter>().sharedMesh = mesh;
            vis.GetComponent<MeshRenderer>().sharedMaterials = border ? new[] { punch, hazard } : new[] { punch };
            n++;
        }
        // 羽根車の軸のまわりの印
        int k = 0;
        foreach (var tt in course.GetComponentsInChildren<FuwaTurnTable>(true))
        {
            if (!tt.handle) continue;
            var floor = tt.transform.root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "CageFloor").Select(t => t.GetComponent<Collider>()).Where(c => c)
                .OrderBy(c => Vector2.Distance(new Vector2(c.bounds.center.x, c.bounds.center.z), new Vector2(tt.handle.position.x, tt.handle.position.z))).FirstOrDefault();
            if (!floor) continue;
            var b = floor.bounds;
            var at = new Vector3(tt.handle.position.x, b.max.y + 0.01f, tt.handle.position.z);
            var old = floor.transform.Find("SpinMark"); if (old) Object.DestroyImmediate(old.gameObject);
            var g = new GameObject("SpinMark"); g.transform.SetParent(floor.transform, true); g.transform.position = at; g.transform.rotation = Quaternion.identity;
            g.AddComponent<MeshFilter>().sharedMesh = MarkMesh();
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = markM; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            k++;
        }
        AssetDatabase.SaveAssets();
        return "[羽根車のかごの床] 板 " + n + "、印 " + k;
    }

    // 単位の箱（-0.5〜0.5）。uv はメートル（ls を掛ける）。border>0 なら上の面を内がわ（サブメッシュ0）とふちの帯（サブメッシュ1）に
    static Mesh FloorMesh(string name, Vector3 ls, float border)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t0 = new List<int>(); var t1 = new List<int>();
        void Quad(List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, System.Func<Vector3, Vector2> U)
        {
            int i = v.Count; foreach (var p in new[] { a, b, c, d }) { v.Add(p); uv.Add(U(p)); }
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }
        Vector2 Top(Vector3 p) => new Vector2(p.x * ls.x, p.z * ls.z);
        Vector2 SideX(Vector3 p) => new Vector2(p.z * ls.z, p.y * ls.y);
        Vector2 SideZ(Vector3 p) => new Vector2(p.x * ls.x, p.y * ls.y);
        float h = 0.5f;
        Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
        // 上の面
        if (border <= 0f) Quad(t0, P(-h, h, -h), P(h, h, -h), P(h, h, h), P(-h, h, h), Vector3.up, Top);
        else
        {
            float bx = Mathf.Min(0.45f, border / Mathf.Max(0.01f, ls.x)), bz = Mathf.Min(0.45f, border / Mathf.Max(0.01f, ls.z));
            float x0 = -h + bx, x1 = h - bx, z0 = -h + bz, z1 = h - bz;
            Quad(t0, P(x0, h, z0), P(x1, h, z0), P(x1, h, z1), P(x0, h, z1), Vector3.up, Top);
            Quad(t1, P(-h, h, -h), P(h, h, -h), P(h, h, z0), P(-h, h, z0), Vector3.up, Top);   // 手前の帯（角ふくむ）
            Quad(t1, P(-h, h, z1), P(h, h, z1), P(h, h, h), P(-h, h, h), Vector3.up, Top);     // 奥の帯
            Quad(t1, P(-h, h, z0), P(x0, h, z0), P(x0, h, z1), P(-h, h, z1), Vector3.up, Top); // 左の帯
            Quad(t1, P(x1, h, z0), P(h, h, z0), P(h, h, z1), P(x1, h, z1), Vector3.up, Top);   // 右の帯
        }
        Quad(t0, P(-h, -h, -h), P(-h, -h, h), P(h, -h, h), P(h, -h, -h), Vector3.down, Top);
        Quad(t0, P(-h, -h, -h), P(h, -h, -h), P(h, h, -h), P(-h, h, -h), Vector3.back, SideZ);
        Quad(t0, P(-h, -h, h), P(-h, h, h), P(h, h, h), P(h, -h, h), Vector3.forward, SideZ);
        Quad(t0, P(-h, -h, -h), P(-h, h, -h), P(-h, h, h), P(-h, -h, h), Vector3.left, SideX);
        Quad(t0, P(h, -h, -h), P(h, -h, h), P(h, h, h), P(h, h, -h), Vector3.right, SideX);
        var m = new Mesh { name = "CageFloor_" + name };
        m.SetVertices(v); m.SetUVs(0, uv); m.subMeshCount = border > 0f ? 2 : 1; m.SetTriangles(t0, 0); if (border > 0f) m.SetTriangles(t1, 1);
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        return Save(m);
    }

    // 黄色い円＋両回りの矢印（上から見て、まん中が軸）
    static Mesh MarkMesh()
    {
        var v = new List<Vector3>(); var t = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); if (Vector3.Cross(b - a, c - a).y < 0f) { t.Add(i); t.Add(i + 2); t.Add(i + 1); } else { t.Add(i); t.Add(i + 1); t.Add(i + 2); } }
        Vector3 Pol(float a, float r) => new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        float r0 = MarkR - MarkW / 2f, r1 = MarkR + MarkW / 2f;
        // 円（矢印の所はあける）：4つの弧、弧の終わりに矢じり。向きは交互（両回り）
        int arcs = 4; float gap = 0.32f;
        for (int k = 0; k < arcs; k++)
        {
            float a0 = 2f * Mathf.PI * k / arcs + gap / 2f, a1 = 2f * Mathf.PI * (k + 1) / arcs - gap / 2f;
            int seg = 14;
            for (int i = 0; i < seg; i++)
            {
                float b0 = Mathf.Lerp(a0, a1, i / (float)seg), b1 = Mathf.Lerp(a0, a1, (i + 1) / (float)seg);
                Tri(Pol(b0, r0), Pol(b1, r0), Pol(b1, r1)); Tri(Pol(b0, r0), Pol(b1, r1), Pol(b0, r1));
            }
            // 矢じり：k が偶数なら a1 の先へ（反時計回り）、奇数なら a0 の先へ（時計回り）
            bool ccw = k % 2 == 0; float tipA = ccw ? a1 + gap * 0.8f : a0 - gap * 0.8f, baseA = ccw ? a1 : a0;
            float hw = MarkW * 1.9f;
            Tri(Pol(baseA, MarkR - hw), Pol(baseA, MarkR + hw), Pol(tipA, MarkR));
        }
        var m = new Mesh { name = "CageFloor_SpinMark" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return Save(m);
    }

    static Mesh Save(Mesh src)
    {
        string path = MeshDir + "/" + src.name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!ex) { AssetDatabase.CreateAsset(src, path); return src; }
        ex.Clear(false); FuwaBevelTools.CopyInto(ex, src); ex.name = src.name; EditorUtility.SetDirty(ex); return ex;
    }

    // パンチングメタル：明るい鉄板に、ずらしてならべた丸い穴（穴の中は暗く、下のふちに明るい線）。1枚 = 0.25m 角に穴2つ
    static Material PunchMat()
    {
        string tp = "Assets/FuwaStage2/Textures/PunchingMetal.png", mp = "Assets/FuwaStage2/Materials/Course2_4_PunchMetal.mat";
        int N = 256; var px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N, w = (y + 0.5f) / N; float c = 0.78f;
            foreach (var h in new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Vector2(0.75f, -0.25f), new Vector2(0.25f, 1.25f), new Vector2(-0.25f, 0.75f), new Vector2(1.25f, 0.25f), new Vector2(0.75f, 1.25f), new Vector2(1.25f, 1.25f), new Vector2(-0.25f, -0.25f), new Vector2(1.25f, -0.25f), new Vector2(-0.25f, 1.25f), new Vector2(0.25f, -0.25f) })
            {
                float d = Vector2.Distance(new Vector2(u, w), h);
                if (d < 0.14f) c = 0.30f + 0.12f * Mathf.Clamp01((w - h.y + 0.14f) / 0.28f);      // 穴の中（上ほど少し明るい）
                else if (d < 0.165f && w < h.y) c = 0.92f;                                        // 穴の下のふちの光
                else if (d < 0.165f) c = Mathf.Min(c, 0.66f);                                     // 穴の上のふちの影
            }
            px[y * N + x] = new Color(c, c, c * 1.02f, 1f);
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true); tex.SetPixels(px); tex.Apply();
        System.IO.File.WriteAllBytes(tp, tex.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; ti.SaveAndReimport();
        var baseM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/GearTest_Floor.mat");
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (!m) { m = baseM ? new Material(baseM) : new Material(Shader.Find("FuwaCourse/ToonLit")); AssetDatabase.CreateAsset(m, mp); }
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); m.mainTextureScale = new Vector2(4f, 4f);   // 1m に 4枚（穴の間 0.18m）
        if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.82f, 0.84f, 0.9f));
        EditorUtility.SetDirty(m); return m;
    }
}
