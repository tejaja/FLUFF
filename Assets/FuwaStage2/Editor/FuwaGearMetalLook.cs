using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 2–4 の歯車だけ金属っぽく（2026-11-11 てじゃ「歯車の質感っていい感じに」→ A 案 →「柱部分はいらんかも」「歯車のとこだけでいい」）
//  歯車のメッシュ（名前に歯の数 _z〇〇・Spur・Pinion・Rack など）の材質だけ、金属の設定をした複製（〇〇_Metal）に差しかえる。柱・枠・軸・トラスは今のまま
//  FuwaCourse2_4Builder.Build の最後で呼ぶ（作り直すと元の材質にもどるので）
public static class FuwaGearMetalLook
{
    public static float Env = 0.5f, Spec = 0f, Size = 0.93f;   // ハイライトはなし（2026-11-11 てじゃ「ライトが真上だからまん丸の光りがでちゃう」）
    static readonly Regex GearMesh = new Regex(@"(Spur|Pinion|Rack_|_z\d|Bevel_z|PathGear_|Gear_z)", RegexOptions.IgnoreCase);
    static readonly string[] Bases = { "FanTest_Brass", "GearTest_Gear", "GearTest_GearLight" };

    [MenuItem("FuwaCourse/ステージ2/2–4 歯車だけ金属っぽくする")]
    public static void Menu() { Debug.Log(Apply(GameObject.Find("FuwaCourse2_4")?.transform)); }

    public static string Apply(Transform course)
    {
        if (!course) return "2–4 がない";
        var map = new Dictionary<Material, Material>();
        foreach (var n in Bases)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/" + n + ".mat"); if (!src) continue;
            string p = "Assets/FuwaStage2/Materials/" + n + "_Metal.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, p); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
            m.SetFloat("_MetalEnv", Env); m.SetFloat("_MetalSpec", Spec); m.SetFloat("_MetalSize", Size); EditorUtility.SetDirty(m);
            map[src] = m;
        }
        // A＋B（2026-11-11 てじゃ「のっぺり単色だからきになるのかな」→「ABやてみよ」）：円盤の削り目（同心円）＋歯の部分を一段暗く（境目に段差の線）
        //  歯車のメッシュに、半径を 0〜1 にした丸い uv を入れる（歯の根元＝0.8）。柄は 1 枚の丸いテクスチャ（GearRadial）
        var tex = RadialTex();
        foreach (var m in map.Values) { m.mainTexture = tex; m.mainTextureScale = Vector2.one; m.mainTextureOffset = Vector2.zero; EditorUtility.SetDirty(m); }
        var done = new HashSet<Mesh>();
        int n2 = 0;
        foreach (var mr in course.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || !GearMesh.IsMatch(mf.sharedMesh.name)) continue;
            var ms = mr.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++) if (ms[i] && map.TryGetValue(ms[i], out var to)) { ms[i] = to; ch = true; } else if (ms[i] && map.ContainsValue(ms[i])) ch = true;   // もう差しかえ済み（uv だけ入れ直す）
            if (ch) { mr.sharedMaterials = ms; EditorUtility.SetDirty(mr); n2++; if (done.Add(mf.sharedMesh)) RadialUV(mf.sharedMesh); }
        }
        AssetDatabase.SaveAssets();
        return "[歯車の金属] " + n2 + " 個（uv " + done.Count + " メッシュ）";
    }

    public const float RootAt = 0.8f;   // テクスチャの上で歯の根元の半径（0〜1）
    static readonly Regex Teeth = new Regex(@"_z(\d+)");
    // 半径を 0〜1 に：歯の根元（ふつうの歯車の割合 (z-2.5)/(z+2)）を RootAt に。歯の数が名前にない物（ラック）は歯の帯なし。軸は一番うすい向き
    static void RadialUV(Mesh m)
    {
        var v = m.vertices; if (v.Length == 0) return;
        var b = m.bounds; var ext = b.size; int ax = ext.x <= ext.y && ext.x <= ext.z ? 0 : (ext.y <= ext.z ? 1 : 2);
        int a1 = ax == 0 ? 1 : 0, a2 = ax == 2 ? 1 : 2;
        var c = b.center; float R = 0f;
        foreach (var p in v) R = Mathf.Max(R, new Vector2(p[a1] - c[a1], p[a2] - c[a2]).magnitude);
        if (R <= 1e-5f) return;
        var mz = Teeth.Match(m.name); float root = 1.1f;
        if (mz.Success && int.TryParse(mz.Groups[1].Value, out int z) && z > 4) root = (z - 2.5f) / (z + 2f);
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var d = new Vector2(v[i][a1] - c[a1], v[i][a2] - c[a2]); float r = d.magnitude / R;
            float f = r < root ? RootAt * r / root : RootAt + (1f - RootAt) * Mathf.Clamp01((r - root) / Mathf.Max(1e-4f, 1f - root));
            var dir = r > 1e-6f ? d / d.magnitude : Vector2.zero;
            uv[i] = new Vector2(0.5f, 0.5f) + dir * (f * 0.5f);
        }
        m.uv = uv; EditorUtility.SetDirty(m);
    }

    // 丸い柄：内側は明るい円盤にうっすら同心円の削り目、根元（RootAt）に段差の線（暗い線＋明るい線）、外は一段暗い歯の帯
    static Texture2D RadialTex()
    {
        string tp = "Assets/FuwaStage2/Textures/GearRadial.png";
        int N = 512; var px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = (x + 0.5f) / N * 2f - 1f, w = (y + 0.5f) / N * 2f - 1f, r = Mathf.Sqrt(u * u + w * w);
            float c;
            if (r < RootAt - 0.03f)
            {
                c = 1f;
                // 削り目：細かい同心円（ふたつの周期を重ねてむらに）
                float g = Mathf.Sin(r * 260f) * 0.5f + Mathf.Sin(r * 97f + 1.3f) * 0.5f;
                c -= 0.035f * (0.5f + 0.5f * g);
                if (r < 0.12f) c -= 0.06f;   // まん中（軸のまわり）は少し暗く
            }
            else if (r < RootAt - 0.012f) c = 0.78f;   // 段差の影
            else if (r < RootAt) c = 1.06f;            // 段差の明るいふち
            else c = 0.86f;                             // 歯の帯
            c = Mathf.Clamp01(c);
            px[y * N + x] = new Color(c, c, c, 1f);
        }
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true); t.SetPixels(px); t.Apply();
        System.IO.File.WriteAllBytes(tp, t.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.wrapMode = TextureWrapMode.Clamp; ti.anisoLevel = 4; ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
    }
}
