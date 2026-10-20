using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// いわのみち：転がる岩の坂のわき、こわれた橋の奥の岩の上に立てる「ここを登る」の矢印
//（2026-10-20 てじゃ「洞窟目指しちゃう人いるんだよね、たまーにだけど」「斜め上を向いてる感じ、ここ登るんだぞって」
// 「棒にささった赤いデカい矢印って感じがいいかも」）。
// 岩の通り道の外に置く。厚みのある赤い矢印（表裏は赤、側面は少し暗い赤）を棒の先に刺した形。見た目だけ（当たり判定なし）。何度実行してもよい
public static class FuwaCourse2ClimbArrow
{
    const string SignName = "ClimbArrowSign";
    const string MeshPath = "Assets/FuwaCourse/Meshes/Course2_ClimbArrowSign.asset";
    public static Vector3 Pos = new Vector3(99.8f, 0f, 7.0f);      // 棒の足もと（高さは床に当てて決める）。橋の奥の段の左わきの岩の上
    public static Vector3 LookFrom = new Vector3(88f, 0f, 1.5f);   // 坂を登ってくる人の立ち位置。矢印の面はここを向く
    public static float ArrowDeg = 55f;                            // 矢印の向き（見る人から見て、右＝0°、上＝90°）
    public static float TiltSide = 0f, TiltBack = 0f;              // 少し傾ける（度）
    // てじゃが手で合わせた向き（2026-10-20「ちょっと角度変えた」）。null で上の自動の向き（LookFrom を向く）に戻る
    public static bool UseWood = true;
    const string WoodModel = "Assets/FuwaCourse/Models/Course2_ClimbArrowWood.fbx";
    public static Vector3? ManualRot = new Vector3(16.28f, 13.042f, 357.229f);
    // 矢印の大きさ(m)：長さ・頭の長さ・軸の太さ（半分）・頭の幅（半分）・厚み。中心の高さ・棒の太さ
    public static float Len = 1.5f, Head = 0.6f, ShaftHalf = 0.15f, HeadHalf = 0.4f, Thick = 0.08f, CenterY = 1.9f, PoleR = 0.045f;

    [MenuItem("FuwaCourse/Course2/「ここを登る」の矢印を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var old = root.Find(SignName); if (old != null) Object.DestroyImmediate(old.gameObject);

        // サブメッシュ：0 赤（表・裏）・1 暗い赤（側面）・2 グレー（棒）。表は -z（来る人の方）。見る人から見て右＝+x・上＝+y
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new[] { new List<int>(), new List<int>(), new List<int>() };
        float a = ArrowDeg * Mathf.Deg2Rad; var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var w = new Vector2(-u.y, u.x);
        Vector2 Q(float along, float side) => u * along + w * side;
        float t0 = -Len * 0.5f, neck = Len * 0.5f - Head, tip = Len * 0.5f;
        // 外形（反時計回り＝見る人から見て。軸の左下から）。上下にカエシのある矢印
        //（下のふちを一直線にした片側だけのカエシも試したが、てじゃ「やっぱもどそう」）
        var outline = new[] { Q(t0, -ShaftHalf), Q(neck, -ShaftHalf), Q(neck, -HeadHalf), Q(tip, 0f), Q(neck, HeadHalf), Q(neck, ShaftHalf), Q(t0, ShaftHalf) };
        float zf = -Thick * 0.5f, zb = Thick * 0.5f;
        Vector3 P(Vector2 q, float z) => new Vector3(q.x, CenterY + q.y, z);
        // 表（-z）と裏（+z）：軸の四角＋頭の三角。Unity の表＝見る人から時計回り
        void Face(float z, Vector3 n, bool front)
        {
            int i = V.Count;
            foreach (var q in outline) { V.Add(P(q, z)); N.Add(n); }
            int[] tris = { 0, 1, 5, 0, 5, 6, 2, 3, 4 };   // 軸（0,1,5,6）と頭（2,3,4）
            for (int k = 0; k < tris.Length; k += 3)
            {
                // outline は反時計回り → 表（-z から見る）は時計回りにするため 2 と 3 番目を入れかえる。裏はそのまま
                if (front) T[0].AddRange(new[] { i + tris[k], i + tris[k + 2], i + tris[k + 1] });
                else T[0].AddRange(new[] { i + tris[k], i + tris[k + 1], i + tris[k + 2] });
            }
        }
        Face(zf, Vector3.back, true);
        Face(zb, Vector3.forward, false);
        // 側面
        for (int k = 0; k < outline.Length; k++)
        {
            var p0 = outline[k]; var p1 = outline[(k + 1) % outline.Length];
            var e = (p1 - p0).normalized; var n2 = new Vector2(e.y, -e.x);   // 反時計回りの外向き
            var n = new Vector3(n2.x, n2.y, 0f);
            int i = V.Count;
            V.Add(P(p0, zf)); V.Add(P(p1, zf)); V.Add(P(p1, zb)); V.Add(P(p0, zb));
            for (int j = 0; j < 4; j++) N.Add(n);
            T[1].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        // 棒：矢印の裏から地面まで（矢印の真ん中に刺さる）
        {
            int ps = 12; float pz = zb + PoleR * 0.5f, y0 = -0.15f, y1 = CenterY + 0.05f;
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
        if (mesh == null) { mesh = new Mesh { name = "Course2_ClimbArrowSign" }; AssetDatabase.CreateAsset(mesh, MeshPath); }
        mesh.Clear(false); mesh.SetVertices(V); mesh.SetNormals(N); mesh.subMeshCount = 3;
        for (int s = 0; s < 3; s++) mesh.SetTriangles(T[s], s);
        mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);

        var go = new GameObject(SignName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        float y = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Ray(new Vector3(Pos.x, 30f, Pos.z), Vector3.down), 40f))
            if (!h.collider.isTrigger) y = Mathf.Max(y, h.point.y);
        if (y == float.MinValue) y = 9f;
        go.transform.position = new Vector3(Pos.x, y, Pos.z);
        var face = Pos - LookFrom; face.y = 0f;
        go.transform.rotation = Quaternion.LookRotation(face.normalized) * Quaternion.Euler(TiltBack, 0f, TiltSide);
        if (ManualRot is Vector3 mr) go.transform.rotation = Quaternion.Euler(mr);
        go.transform.localScale = Vector3.one / root.lossyScale.x;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[]
        {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Sign_Red.mat"),
            Mat("Sign_RedDark", new Color(0.62f, 0.12f, 0.12f)),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Sign_Pole.mat"),
        };
        // 木の板の版（Blender で作った Models/Course2_ClimbArrowWood.fbx）：板5枚・裏の横木・釘・木の杭、角は丸く、ペンキのはがれ・ふちの欠け
        //（2026-10-20 てじゃ「もっと丸みをつけて、木の板で作ってちょっとボロくなったみたいな」）。原点は杭の足もと、表は -z、杭の根元 15cm は埋める
        if (UseWood)
        {
            var wm = AssetDatabase.LoadAllAssetsAtPath(WoodModel);
            Mesh wood = null; foreach (var o in wm) if (o is Mesh m2) { wood = m2; break; }
            if (wood != null)
            {
                go.GetComponent<MeshFilter>().sharedMesh = wood;
                go.GetComponent<MeshRenderer>().sharedMaterials = new[]   // サブメッシュ：木・赤いペンキ・釘
                {
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Sign_RedSoft.mat"),   // 表は少し淡い赤（Sign_Red の彩度 0.78 倍。濃い茶色も試したが てじゃ「赤にするかぁ、ちょっとだけ淡めで」）
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Sign_Pole.mat"),
                };
                go.transform.position += go.transform.up * -0.15f;
            }
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "climb arrow at " + go.transform.position.ToString("F2");
    }

    static Material Mat(string name, Color col)
    {
        string path = "Assets/FuwaCourse/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Sign_Red.mat")) { name = name };
        m.color = col;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
