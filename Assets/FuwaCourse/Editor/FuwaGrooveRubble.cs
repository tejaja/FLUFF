using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// いわのみち序盤の「3連噴気孔」の溝の床に、平たい岩の板をガタガタに敷く（見た目だけ、当たり判定なし。2026-10-07 てじゃ「床をゴツっと」）。
// 岩のマテリアル（ToonLit）はなめらかな凸凹だと陰影がほとんど出ないので、板のふちの段差と影でゴツゴツに見せる。
// 噴気孔のまわり・壁ぎわ・道へ上がる坂の上はあける。何度実行しても同じ結果。
public static class FuwaGrooveRubble
{
    [MenuItem("FuwaCourse/Course2/噴気孔の溝の床に岩の板を敷く")]
    public static void Menu() { Debug.Log(Build()); }

    public class MB { public List<Vector3> V = new List<Vector3>(); public List<int> T = new List<int>(); }

    static void Tri(MB m, Vector3 a, Vector3 b, Vector3 c, Vector3 outHint)
    {
        var n = Vector3.Cross(b - a, c - a);
        int i = m.V.Count; m.V.Add(a);
        if (Vector3.Dot(n, outHint) >= 0) { m.V.Add(b); m.V.Add(c); } else { m.V.Add(c); m.V.Add(b); }
        m.T.Add(i); m.T.Add(i + 1); m.T.Add(i + 2);
    }

    // 平たい板：上の面（少しふくらむ）＋側面。底は見えないのでなし
    public static void Slab(MB m, Vector3 c, Quaternion rot, float r, float thick, int sides, System.Random rnd)
    {
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        var top = new List<Vector3>(); var bot = new List<Vector3>();
        float a0 = R(0, 360);
        for (int i = 0; i < sides; i++)
        {
            float a = (a0 + 360f * i / sides + R(-14, 14)) * Mathf.Deg2Rad;
            float rr = r * R(0.75f, 1.15f);
            var p = new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
            top.Add(c + rot * (p * 0.9f + Vector3.up * thick));   // 上の面は少しすぼめて、ふちに面取り
            bot.Add(c + rot * (p + Vector3.up * R(-0.02f, 0.02f)));
        }
        var ctr = c + rot * (Vector3.up * (thick + r * 0.06f));
        var up = rot * Vector3.up;
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Tri(m, ctr, top[i], top[j], up);
            var mid = (top[i] + top[j] + bot[i] + bot[j]) * 0.25f; var outv = mid - c; outv.y = 0;
            Tri(m, top[i], bot[i], bot[j], outv); Tri(m, top[i], bot[j], top[j], outv);
        }
    }

    public static string Build()
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var floor = root.Find("Rocks2/GrooveFloor");
        var floorCol = floor.GetComponent<MeshCollider>();
        var vents = root.Find("Gimmick_GrooveVents").Cast<Transform>().Select(t => t.position).ToList();
        var b = floor.GetComponent<Renderer>().bounds;
        var rnd = new System.Random(2026);
        float R(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
        var mb = new MB();
        var placed = new List<(Vector3 p, float r)>();
        int tries = 0;
        while (tries++ < 9000 && placed.Count < 160)
        {
            var o = new Vector3(R(b.min.x, b.max.x), b.max.y + 3f, R(b.min.z, b.max.z));
            if (!floorCol.Raycast(new Ray(o, Vector3.down), out var hit, 10f)) continue;
            if (hit.normal.y < 0.88f || hit.point.y > 3.92f) continue;
            // 上に別の物（壁など）がかぶっていない所だけ
            if (Physics.Raycast(o, Vector3.down, out var any, 10f, ~0, QueryTriggerInteraction.Ignore) && any.collider != floorCol) continue;
            bool big = placed.Count < 70 || rnd.NextDouble() < 0.5;
            float r = big ? R(0.24f, 0.42f) : R(0.09f, 0.16f);
            if (vents.Any(v => new Vector2(v.x - hit.point.x, v.z - hit.point.z).magnitude - r * 0.6f < 0.92f))   // 噴気孔のふち（半径約1m）に少しもぐるくらいまでは置く continue;
            if (placed.Any(q => new Vector2(q.p.x - hit.point.x, q.p.z - hit.point.z).magnitude < (q.r + r) * 0.82f)) continue;
            // ふちの4方向にも床があること（壁ぎわ・崖っぷちからはみ出さない）
            bool ok = true;
            for (int k = 0; k < 4 && ok; k++)
            {
                var d = Quaternion.Euler(0, k * 90 + 45, 0) * Vector3.forward * r;
                if (!floorCol.Raycast(new Ray(hit.point + d + Vector3.up * 1.5f, Vector3.down), out var h2, 3f) || Mathf.Abs(h2.point.y - hit.point.y) > 0.2f) ok = false;
            }
            if (!ok) continue;
            float thick = big ? R(0.07f, 0.13f) : R(0.04f, 0.07f);
            var tiltAxis = Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward;
            var rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.AngleAxis(R(3f, 9f), tiltAxis) * Quaternion.Euler(0, R(0, 360), 0);
            var c = hit.point - Vector3.up * thick * R(0.35f, 0.6f);   // 半分くらい埋める
            Slab(mb, c, rot, r, thick, rnd.Next(6, 9), rnd);
            placed.Add((hit.point, r));
        }
        var holder = root.Find("Rocks2/GrooveRubble");
        if (holder == null)
        {
            var go = new GameObject("GrooveRubble", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root.Find("Rocks2"), false);
            Undo.RegisterCreatedObjectUndo(go, "rubble");
            holder = go.transform;
        }
        holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity; holder.localScale = Vector3.one;
        var w2l = holder.worldToLocalMatrix;
        var mesh = new Mesh { name = "Course2_GrooveRubble", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(mb.V.Select(p => w2l.MultiplyPoint3x4(p)).ToList()); mesh.SetTriangles(mb.T, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = "Assets/FuwaCourse/Meshes/Course2_GrooveRubble.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { old.Clear(); EditorUtility.CopySerialized(mesh, old); mesh = old; } else AssetDatabase.CreateAsset(mesh, path);
        holder.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = holder.GetComponent<MeshRenderer>();
        mr.sharedMaterial = floor.GetComponent<MeshRenderer>().sharedMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        holder.gameObject.layer = 0;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "rubble slabs " + placed.Count + " tris " + mb.T.Count / 3;
    }
}
