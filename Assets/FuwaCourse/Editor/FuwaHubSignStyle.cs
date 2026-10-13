using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 入口（タイトル）の「チャレンジ」「あそびば」の看板を、ぷっくりしたクッションの看板にする
//（2026-10-13 てじゃ「入り口の看板もコースみたいに盛りたい、ただコースと同じなのもさみしい」→ 案A。上の小物（旗・トロフィー・扇風機・ふわふわ）は「小物なくても良いかな」で外した）。
// ・板：白いクッション（前後にふくらむ）＋ふちにモードの色のパイピング（筒）＋内側にステッチ。コースの雲看板（ふちが波打つ）とは形で分ける
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
    const bool ShowStitch = false;      // 内側のステッチ（2026-10-13 てじゃ「ステッチも外してみて」）

    [MenuItem("FuwaCourse/入口の看板をぷっくりにする")]
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
        if (ShowStitch)
        {
            // ステッチ：ふくらんだ面の少し手前（ふちからの距離で面の高さを出す）
            float q = Mathf.Clamp01(StitchInset / Rise);
            float sz = zc - Front * Mathf.Sqrt(1f - (1f - q) * (1f - q)) - 0.006f;
            var sc = Color.Lerp(accent, Color.white, 0.25f);
            FuwaSignStyle.Part(deco, "Stitch", Save(FuwaSignStyle.Stitch(hw, hh, Round, StitchInset, sz, 0.07f, 0.05f, 0.022f), key + "_Stitch"), Mat(sc, "HubStitch_" + kind, 0.2f), Vector3.zero);
        }

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
