using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 入口（タイトル）の「チャレンジ」「あそびば」の看板を、ぷっくりしたクッションの看板にして、上にモードの小物をのせる
//（2026-10-13 てじゃ「入り口の看板もコースみたいに盛りたい、ただコースと同じなのもさみしい」→ 案A）。
// ・板：白いクッション（前後にふくらむ）＋ふちにモードの色のパイピング（筒）＋内側にステッチ。コースの雲看板（ふちが波打つ）とは形で分ける
// ・小物（看板の上のふちに座らせる）：チャレンジ＝旗・トロフィー・ふわふわ1個／あそびば＝扇風機・ゴールリング・カラフルなふわふわ
// 見た目だけ（当たり判定・スクリプトなし）。看板の Panel・Border は隠す（Remove で元に戻る）。何度実行してもよい
public static class FuwaHubSignStyle
{
    const string Dir = "Assets/FuwaCourse/Generated/Signs";
    const float Round = 0.42f;          // 角の丸み
    const float Front = 0.16f, Back = 0.09f, Rise = 0.42f;   // ふくらみ（前・後ろ）、ふくらみきるまでの幅
    const float PipeR = 0.065f;         // パイピングの太さ
    const float StitchInset = 0.17f;    // ステッチの内側への入り
    const float BodyBehind = 0.2f;      // 板の真ん中の厚みの中心（文字より後ろ）
    const float ExtraH = 0.24f;         // 元の板より縦にのばす分（下へ）

    [MenuItem("FuwaCourse/入口の看板をぷっくりにする（小物つき）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var hub = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaHub" && t.parent == null);
        var log = new List<string>();
        foreach (var (gate, kind) in new[] { ("SoloGate", "Challenge"), ("PlaygroundGate", "Playground") })
        {
            var label = hub.Find(gate + "/Label"); if (label == null) { log.Add(gate + ": no label"); continue; }
            log.Add(Apply(label, kind));
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
        return string.Join("\n", log);
    }

    public static void Remove(Transform sign)
    {
        var d = sign.Find("SignDeco"); if (d != null) Object.DestroyImmediate(d.gameObject);
        foreach (var n in new[] { "Panel", "Border" }) { var t = sign.Find(n); if (t != null) t.gameObject.SetActive(true); }
    }

    static string Apply(Transform sign, string kind)
    {
        Remove(sign);
        var pt = sign.Find("Panel"); var bt = sign.Find("Border");
        var panel = pt.GetComponent<SpriteRenderer>();
        Color accent = bt != null ? bt.GetComponent<SpriteRenderer>().color : new Color(0.53f, 0.58f, 0.75f); accent.a = 1f;
        Color cream = panel.color; cream.a = 1f;
        // 縦は元の板より少し高く（下に伸ばす）：人数の行がステッチにかからないように
        float hw = panel.size.x * pt.localScale.x * 0.5f, hh = panel.size.y * pt.localScale.y * 0.5f + ExtraH * 0.5f;
        pt.gameObject.SetActive(false); if (bt != null) bt.gameObject.SetActive(false);
        string key = "Hub_" + kind;
        var deco = new GameObject("SignDeco").transform; deco.SetParent(sign, false);
        deco.localPosition = new Vector3(pt.localPosition.x, pt.localPosition.y - ExtraH * 0.5f, 0f); deco.localRotation = Quaternion.identity;
        float zc = pt.localPosition.z + BodyBehind;

        // 板（クッション）：下の方はモードの色をうっすら（光の向きに関係なく下向きの面）
        var body = FuwaSignStyle.Outline(hw, hh, Round, 12, 0.03f);
        var cm = Mat(cream, "HubCushion_" + kind, 0.3f);
        cm.SetColor("_UnderColor", Color.Lerp(Color.white, accent, 0.45f)); cm.SetFloat("_UnderStrength", 1f); cm.SetFloat("_UnderGrad", 0.45f); EditorUtility.SetDirty(cm);
        FuwaSignStyle.Part(deco, "Cushion", Save(FuwaSignStyle.Pillow(body, hw, hh, zc, Front, Back, Rise, 16), key + "_Cushion"), cm, Vector3.zero);
        // パイピング（ふちの筒）
        var pm = Mat(accent, "HubPiping_" + kind, 0.2f);
        FuwaSignStyle.Part(deco, "Piping", Save(FuwaSignStyle.Tube(hw, hh, Round, PipeR, zc, 14), key + "_Piping"), pm, Vector3.zero);
        // ステッチ：ふくらんだ面の少し手前（ふちからの距離で面の高さを出す）
        float q = Mathf.Clamp01(StitchInset / Rise);
        float sz = zc - Front * Mathf.Sqrt(1f - (1f - q) * (1f - q)) - 0.006f;
        var sc = Color.Lerp(accent, Color.white, 0.25f);
        FuwaSignStyle.Part(deco, "Stitch", Save(FuwaSignStyle.Stitch(hw, hh, Round, StitchInset, sz, 0.07f, 0.05f, 0.022f), key + "_Stitch"), Mat(sc, "HubStitch_" + kind, 0.2f), Vector3.zero);

        // 小物：看板の上のふち（パイピングの上）に座らせる
        float top = hh + PipeR;
        var props = new GameObject("Props").transform; props.SetParent(deco, false); props.localPosition = new Vector3(0f, top, zc);
        if (kind == "Challenge") ChallengeProps(props, accent);
        else PlaygroundProps(props, accent);

        // 文字はふくらみより手前（文字は Label の z=0、板の前の真ん中は zc - Front）
        var text = sign.GetComponent<TMPro.TMP_Text>();
        if (text != null)
        {
            var rt = text.rectTransform;
            float sx = Mathf.Abs(rt.localScale.x) > 1e-4f ? Mathf.Abs(rt.localScale.x) : 1f;
            rt.sizeDelta = new Vector2((hw - StitchInset - 0.15f) * 2f / sx, (hh + 0.3f) * 2f / sx);
            if (!text.enableAutoSizing) { text.fontSizeMax = text.fontSize; text.enableAutoSizing = true; }
            text.fontSizeMin = text.fontSizeMax * 0.4f;
            EditorUtility.SetDirty(text);
        }
        return sign.parent.name + " -> cushion " + kind + " (" + hw.ToString("F2") + "x" + hh.ToString("F2") + ")";
    }

    // ---------- 小物 ----------
    static void ChallengeProps(Transform p, Color accent)
    {
        // 旗（左はし）：白い棒＋コーラルの三角の旗（少し波打つ）
        var flag = new GameObject("Flag").transform; flag.SetParent(p, false); flag.localPosition = new Vector3(-1.18f, 0f, 0f); flag.localRotation = Quaternion.Euler(0f, 0f, 6f);
        FuwaSignStyle.Part(flag, "Pole", Save(Cylinder(0.026f, 0.82f, 10), "Hub_FlagPole"), Mat(new Color(0.98f, 0.96f, 0.92f), "HubWhite", 0.3f), Vector3.zero);
        FuwaSignStyle.Part(flag, "Knob", Save(Sphere(0.05f, 10, 7), "Hub_FlagKnob"), Mat(new Color(1f, 0.82f, 0.35f), "HubGold", 0.25f), new Vector3(0f, 0.84f, 0f));
        FuwaSignStyle.Part(flag, "Pennant", Save(Pennant(0.5f, 0.32f, 0.04f), "Hub_Pennant"), Mat(new Color(1f, 0.54f, 0.48f), "HubCoral", 0.2f), new Vector3(0.02f, 0.78f, 0f));
        // トロフィー（右はし）：金の回転体＋取っ手
        var tro = new GameObject("Trophy").transform; tro.SetParent(p, false); tro.localPosition = new Vector3(1.12f, 0f, 0f); tro.localRotation = Quaternion.Euler(0f, -15f, 0f);
        var gold = Mat(new Color(1f, 0.8f, 0.3f), "HubGold", 0.25f);
        FuwaSignStyle.Part(tro, "Cup", Save(Lathe(TrophyProfile(), 20), "Hub_TrophyCup"), gold, Vector3.zero);
        FuwaSignStyle.Part(tro, "HandleL", Save(Torus(0.085f, 0.018f, 16, 8, 200f), "Hub_TrophyHandle"), gold, new Vector3(-0.15f, 0.38f, 0f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        FuwaSignStyle.Part(tro, "HandleR", Save(Torus(0.085f, 0.018f, 16, 8, 200f), "Hub_TrophyHandle"), gold, new Vector3(0.15f, 0.38f, 0f)).transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        FuwaSignStyle.Part(tro, "Star", Save(Star(0.07f, 0.03f, 0.02f), "Hub_Star"), Mat(new Color(1f, 0.95f, 0.75f), "HubStarLight", 0.2f), new Vector3(0f, 0.3f, -0.19f));
        // ふわふわ1個（トロフィーの横にちょこんと）
        Fluff(p, "Fluff", new Vector3(0.62f, 0.15f, -0.02f), 0.3f, new Color(0.98f, 0.95f, 0.93f));
    }

    static void PlaygroundProps(Transform p, Color accent)
    {
        // 扇風機（左はし）：あそびばの扇風機と同じ形
        var fanMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/PlaygroundFan.fbx").OfType<Mesh>().ToDictionary(m => m.name);
        var fan = new GameObject("Fan").transform; fan.SetParent(p, false); fan.localPosition = new Vector3(-1.15f, 0f, 0f); fan.localRotation = Quaternion.Euler(0f, 160f, 0f); fan.localScale = Vector3.one * 1.1f;
        FuwaSignStyle.Part(fan, "Body", fanMeshes["Chill_FanBody"], AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Playground/Playground_FanBody.mat"), Vector3.zero);
        FuwaSignStyle.Part(fan, "Blades", fanMeshes["Chill_FanBlades"], AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Playground/Playground_FanBlades.mat"), new Vector3(0f, 0.36f, 0f));
        // ゴールリング（右はし、ななめに立てかける）
        var ringM = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.FindAssets("Goal_Ring t:Material").Select(AssetDatabase.GUIDToAssetPath).First(pp => pp.EndsWith("/Goal_Ring.mat")));
        var ring = FuwaSignStyle.Part(p, "Ring", Save(Torus(0.3f, 0.04f, 36, 10, 360f), "Hub_Ring"), ringM, new Vector3(1.08f, 0.31f, 0.02f));
        ring.transform.localRotation = Quaternion.Euler(0f, 25f, -12f);
        // カラフルなふわふわ（あそびばの色）
        Fluff(p, "Fluff0", new Vector3(-0.55f, 0.13f, -0.03f), 0.26f, new Color(1f, 0.72f, 0.82f));
        Fluff(p, "Fluff1", new Vector3(-0.25f, 0.1f, 0.05f), 0.2f, new Color(1f, 0.92f, 0.55f));
        Fluff(p, "Fluff2", new Vector3(0.52f, 0.14f, -0.02f), 0.28f, new Color(0.62f, 0.83f, 1f));
        Fluff(p, "Fluff3", new Vector3(0.8f, 0.09f, 0.06f), 0.18f, new Color(0.68f, 0.92f, 0.72f));
    }

    static void Fluff(Transform p, string name, Vector3 pos, float size, Color c)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FuwaCourse/Prefabs/Playground/Fluff.prefab");
        var mesh = src.GetComponent<MeshFilter>().sharedMesh;
        var baseM = src.GetComponent<MeshRenderer>().sharedMaterial;
        string path = Dir + "/HubFluff_" + ColorUtility.ToHtmlStringRGB(c) + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(baseM) { name = "HubFluff" }; AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c); m.SetColor("_TipColor", Color.Lerp(c, Color.white, 0.6f)); EditorUtility.SetDirty(m);
        var g = FuwaSignStyle.Part(p, name, mesh, m, pos); g.transform.localScale = Vector3.one * size;
    }

    // ---------- 形 ----------
    static List<Vector2> TrophyProfile()
    {
        // (半径, 高さ)。下の台 → 細い軸 → 杯（上が広い）
        return new List<Vector2> {
            new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.13f, 0.05f), new Vector2(0.09f, 0.07f), new Vector2(0.09f, 0.1f),
            new Vector2(0.035f, 0.13f), new Vector2(0.03f, 0.2f), new Vector2(0.05f, 0.24f), new Vector2(0.12f, 0.29f), new Vector2(0.155f, 0.37f),
            new Vector2(0.165f, 0.47f), new Vector2(0.15f, 0.48f), new Vector2(0.14f, 0.42f), new Vector2(0f, 0.36f) };
    }

    static Mesh Lathe(List<Vector2> prof, int seg)
    {
        var V = new List<Vector3>(); var T = new List<int>(); int n = prof.Count;
        for (int s = 0; s <= seg; s++)
        {
            float a = s / (float)seg * Mathf.PI * 2f;
            foreach (var pr in prof) V.Add(new Vector3(Mathf.Cos(a) * pr.x, pr.y, Mathf.Sin(a) * pr.x));
        }
        for (int s = 0; s < seg; s++)
            for (int i = 0; i < n - 1; i++)
            {
                int a0 = s * n + i, a1 = a0 + 1, b0 = a0 + n, b1 = b0 + 1;
                T.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
        return Fin(V, T, true);
    }

    static Mesh Cylinder(float r, float h, int seg)
    {
        return Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, h), new Vector2(0f, h) }, seg);
    }

    static Mesh Sphere(float r, int seg, int rings)
    {
        var prof = new List<Vector2>();
        for (int i = 0; i <= rings; i++) { float t = i / (float)rings * Mathf.PI; prof.Add(new Vector2(Mathf.Sin(t) * r, -Mathf.Cos(t) * r)); }
        return Lathe(prof, seg);
    }

    static Mesh Torus(float R, float r, int seg, int ring, float arcDeg)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        int segs = Mathf.Max(3, Mathf.RoundToInt(seg * arcDeg / 360f));
        for (int i = 0; i <= segs; i++)
        {
            float a = (i / (float)segs * arcDeg - arcDeg * 0.5f) * Mathf.Deg2Rad;
            var c = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f); var dir = c.normalized;
            for (int k = 0; k <= ring; k++)
            {
                float b = k / (float)ring * Mathf.PI * 2f;
                V.Add(c + dir * Mathf.Cos(b) * r + Vector3.forward * Mathf.Sin(b) * r);
            }
        }
        for (int i = 0; i < segs; i++)
            for (int k = 0; k < ring; k++)
            {
                int a0 = i * (ring + 1) + k, a1 = a0 + 1, b0 = a0 + ring + 1, b1 = b0 + 1;
                T.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        return Fin(V, T, false);
    }

    // 三角の旗：棒側が高さ h、先が細い。少し波打たせる（両面）
    static Mesh Pennant(float len, float h, float wave)
    {
        var V = new List<Vector3>(); var T = new List<int>(); int cols = 10;
        for (int i = 0; i <= cols; i++)
        {
            float u = i / (float)cols; float half = h * 0.5f * (1f - u * 0.92f);
            float z = Mathf.Sin(u * Mathf.PI * 1.6f) * wave * u;
            V.Add(new Vector3(len * u, -half, z)); V.Add(new Vector3(len * u, half, z));
        }
        for (int i = 0; i < cols; i++) { int a = i * 2; T.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
        int n = V.Count; for (int i = 0; i < n; i++) V.Add(V[i]);
        int c = T.Count; for (int i = 0; i < c; i += 3) T.AddRange(new[] { T[i] + n, T[i + 2] + n, T[i + 1] + n });
        var m = new Mesh { name = "Pennant" }; m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        // 下に向けて少しずらす（棒の上の方から垂れる）
        var vs = m.vertices; for (int i = 0; i < vs.Length; i++) vs[i].y -= h * 0.5f; m.vertices = vs; m.RecalculateBounds();
        return m;
    }

    // 平たい星（トロフィーの杯の正面に貼る）
    static Mesh Star(float rOut, float rIn, float thick)
    {
        var V = new List<Vector3>(); var T = new List<int>(); var ring = new List<Vector2>();
        for (int i = 0; i < 10; i++) { float a = (90f + i * 36f) * Mathf.Deg2Rad; float r = i % 2 == 0 ? rOut : rIn; ring.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
        V.Add(new Vector3(0f, 0f, -thick * 0.5f)); foreach (var p in ring) V.Add(new Vector3(p.x, p.y, 0f));
        for (int i = 0; i < 10; i++) T.AddRange(new[] { 0, 1 + (i + 1) % 10, 1 + i });
        var m = new Mesh { name = "Star" }; m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    static Mesh Fin(List<Vector3> V, List<int> T, bool outwardCheck)
    {
        var m = new Mesh { name = "Prop" }; m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        if (outwardCheck)
        {
            // 回転体：法線が外向きか（側面の頂点で、中心からの向きと比べる）
            var vs = m.vertices; var ns = m.normals; float dot = 0f;
            for (int i = 0; i < vs.Length; i++) { var o = new Vector3(vs[i].x, 0f, vs[i].z); if (o.sqrMagnitude > 1e-4f) dot += Vector3.Dot(ns[i], o.normalized); }
            if (dot < 0f) { var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } m.triangles = t; m.RecalculateNormals(); }
        }
        return m;
    }

    static Mesh Save(Mesh m, string name)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Signs");
        string path = Dir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null)
        {
            old.Clear(); old.indexFormat = m.indexFormat; old.SetVertices(m.vertices); old.SetNormals(m.normals);
            if (m.uv.Length == m.vertexCount) old.SetUVs(0, m.uv);
            old.SetTriangles(m.triangles, 0); old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old;
        }
        m.name = name; AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material Mat(Color c, string name, float shadow)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Signs");
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = FuwaSignStyle.Toon(c, name); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c); m.SetFloat("_ShadowStrength", shadow); m.SetFloat("_AmbientStrength", 1.2f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
