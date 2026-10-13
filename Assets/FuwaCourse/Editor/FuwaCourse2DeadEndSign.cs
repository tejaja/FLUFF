using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// いわのみち：割れて抜け落ちた道の先（転がる岩の洞窟の前）に立てる進入禁止の標識（赤い丸に白い横棒）
//（2026-10-13 てじゃ「右に飛び出てるとこの手前側くらいに、行き止まりがわかるような看板」「文字じゃなくて絵」「進入禁止看板がいいな、バリケードはいらない」）。
// 行き止まり専用の世界共通マークは無いので、どの国でも同じ形の進入禁止にした。
// 丸い板（ふちは白、面は赤、白い横棒は少し浮かせる、裏はグレー）＋グレーの柱を1つのメッシュで作る。見た目だけ（当たり判定なし）。
// 転がる岩の通り道（右はしの Boulder2 で z≈1.8〜2.4）にかからないよう、右のふちの岩だまりの手前に置く。何度実行してもよい
public static class FuwaCourse2DeadEndSign
{
    const string SignName = "NoEntrySign";
    const string MeshPath = "Assets/FuwaCourse/Meshes/Course2_NoEntrySign.asset";
    static readonly Vector3 Pos = new Vector3(101.75f, 0f, 0.62f);    // 足もと（高さは床に当てて決める）
    static readonly Vector3 LookFrom = new Vector3(97.2f, 0f, 3.0f);  // 割れ目の段差のあたり（来る人の立ち位置）。標識はここを向く
    const float PlateR = 0.42f, PlateT = 0.035f, RimW = 0.045f, CenterY = 1.55f;
    const float BarW = 0.56f, BarH = 0.13f, BarLift = 0.006f;
    const float PoleR = 0.035f;
    const int Seg = 40;
    const float TiltSide = -9f, TiltBack = 4f;   // 傾き（度）：横（見る人から見て右へ）・うしろ

    [MenuItem("FuwaCourse/Course2/いきどまりの標識（進入禁止）を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        foreach (var n in new[] { SignName, "DeadEndSign" }) { var old = root.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }

        // サブメッシュ：0 赤（面）・1 白（ふち・横棒）・2 グレー（裏・側面・柱）。標識の表は -z（来る人の方）
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new[] { new List<int>(), new List<int>(), new List<int>() };
        void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d); for (int k = 0; k < 4; k++) N.Add(n);
            T[s].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        // 円盤の輪（r0〜r1）を z の面に、向き n で
        void Ring(int s, float r0, float r1, float z, Vector3 n)
        {
            for (int k = 0; k < Seg; k++)
            {
                float a0 = k * Mathf.PI * 2 / Seg, a1 = (k + 1) * Mathf.PI * 2 / Seg;
                Vector3 P(float r, float a) => new Vector3(Mathf.Cos(a) * r, CenterY + Mathf.Sin(a) * r, z);
                if (n.z < 0) Quad(s, P(r1, a0), P(r0, a0), P(r0, a1), P(r1, a1), n);
                else Quad(s, P(r1, a1), P(r0, a1), P(r0, a0), P(r1, a0), n);
            }
        }
        float zf = -PlateT * 0.5f, zb = PlateT * 0.5f;
        Ring(0, 0f, PlateR - RimW, zf, Vector3.back);           // 赤い面
        Ring(1, PlateR - RimW, PlateR, zf, Vector3.back);        // 白いふち
        Ring(2, 0f, PlateR, zb, Vector3.forward);                // 裏
        for (int k = 0; k < Seg; k++)                            // 側面
        {
            float a0 = k * Mathf.PI * 2 / Seg, a1 = (k + 1) * Mathf.PI * 2 / Seg;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0);
            Vector3 c = new Vector3(0, CenterY, 0);
            int i = V.Count;
            V.Add(c + d0 * PlateR + Vector3.forward * zf); V.Add(c + d1 * PlateR + Vector3.forward * zf); V.Add(c + d1 * PlateR + Vector3.forward * zb); V.Add(c + d0 * PlateR + Vector3.forward * zb);
            N.Add(d0); N.Add(d1); N.Add(d1); N.Add(d0);
            T[2].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        // 白い横棒（面から少し浮かせた板＋厚みの側面）
        {
            float hw = BarW * 0.5f, hh = BarH * 0.5f, z1 = zf - BarLift;
            Vector3 p(float x, float y, float z) => new Vector3(x, CenterY + y, z);
            Quad(1, p(-hw, -hh, z1), p(-hw, hh, z1), p(hw, hh, z1), p(hw, -hh, z1), Vector3.back);
            Quad(1, p(-hw, hh, z1), p(-hw, hh, zf), p(hw, hh, zf), p(hw, hh, z1), Vector3.up);
            Quad(1, p(hw, -hh, z1), p(hw, -hh, zf), p(-hw, -hh, zf), p(-hw, -hh, z1), Vector3.down);
            Quad(1, p(-hw, -hh, zf), p(-hw, hh, zf), p(-hw, hh, z1), p(-hw, -hh, z1), Vector3.left);
            Quad(1, p(hw, -hh, z1), p(hw, hh, z1), p(hw, hh, zf), p(hw, -hh, zf), Vector3.right);
        }
        // 柱（板の裏、地面から板の上の方まで）
        {
            int ps = 12; float pz = zb + PoleR, y0 = -0.1f, y1 = CenterY + PlateR * 0.6f;
            for (int k = 0; k < ps; k++)
            {
                float a0 = k * Mathf.PI * 2 / ps, a1 = (k + 1) * Mathf.PI * 2 / ps;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 c0 = new Vector3(0, y0, pz), c1 = new Vector3(0, y1, pz);
                int i = V.Count;
                V.Add(c0 + d0 * PoleR); V.Add(c0 + d1 * PoleR); V.Add(c1 + d1 * PoleR); V.Add(c1 + d0 * PoleR);
                N.Add(d0); N.Add(d1); N.Add(d1); N.Add(d0);
                T[2].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
                int j = V.Count; V.Add(c1); V.Add(c1 + d0 * PoleR); V.Add(c1 + d1 * PoleR); N.Add(Vector3.up); N.Add(Vector3.up); N.Add(Vector3.up);
                T[2].AddRange(new[] { j, j + 2, j + 1 });
            }
        }
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null) { mesh = new Mesh { name = "Course2_NoEntrySign" }; AssetDatabase.CreateAsset(mesh, MeshPath); }
        mesh.Clear(); mesh.SetVertices(V); mesh.SetNormals(N); mesh.subMeshCount = 3;
        for (int s = 0; s < 3; s++) mesh.SetTriangles(T[s], s);
        mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);

        var go = new GameObject(SignName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        float y = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Ray(new Vector3(Pos.x, 14f, Pos.z), Vector3.down), 8f))
            if (h.collider.name == "Mountain" || h.collider.name == "PathMesh") y = Mathf.Max(y, h.point.y);
        go.transform.position = new Vector3(Pos.x, y, Pos.z);
        var face = Pos - LookFrom; face.y = 0f;
        // 表（-z）が来る人の方。少し傾けて、崩れた道ぎわに立ちっぱなしの古い標識っぽく（2026-10-13 てじゃ「ちょっと傾けちゃお」）
        go.transform.rotation = Quaternion.LookRotation(face.normalized) * Quaternion.Euler(TiltBack, 0f, TiltSide);
        go.transform.localScale = Vector3.one / root.lossyScale.x;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat("Sign_Red", new Color(0.86f, 0.18f, 0.18f)), Mat("Sign_Paper", Color.white), Mat("Sign_Pole", new Color(0.62f, 0.63f, 0.67f)) };
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "no entry sign at " + go.transform.position.ToString("F2");
    }

    // 看板の色（木の看板と同じシェーダー）。無ければ作る
    static Material Mat(string name, Color col)
    {
        string path = "Assets/FuwaCourse/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat")) { name = name };
        m.color = col;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
