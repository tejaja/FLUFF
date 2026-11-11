using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 2–4 の歯車だけ金属っぽく（2026-11-11 てじゃ「歯車の質感っていい感じに」→ A 案 →「柱部分はいらんかも」「歯車のとこだけでいい」）
//  歯車のメッシュ（名前に歯の数 _z〇〇・Spur・Pinion・Rack など）の材質だけ、金属の設定をした複製に差しかえる。柱・枠・軸・トラスは今のまま
//  柄（てじゃ「ABやてみよ」：削り目の同心円＋歯の帯を一段暗く・根元に段差の線）は FuwaCourse/ToonLit の _GearPattern で1ピクセルずつ（頂点ごとだとギザギザになった）
//   メッシュに「歯車の面を半径 1 にした平面の座標」の uv を入れて、材質に根元の割合（歯の数から）・半径（m）を入れる。メッシュごとに材質を分ける（Materials/GearMetal/）
//   内側に歯がある輪の歯車（名前に Ring）は柄なし（うつりこみだけ）
//  ハイライトはなし（てじゃ「ライトが真上だからまん丸の光りがでちゃう」）
//  FuwaCourse2_4Builder.Build の最後で呼ぶ（作り直すと元の材質にもどるので）
public static class FuwaGearMetalLook
{
    public static float Env = 0.5f, Spec = 0f, Size = 0.93f, BandDark = 0.86f, RingsPerM = 40f, RingStr = 0.035f;
    static readonly Regex GearMesh = new Regex(@"(Spur|Pinion|Rack_|_z\d|Bevel_z|PathGear_|Gear_z)", RegexOptions.IgnoreCase);
    static readonly Regex Teeth = new Regex(@"_z(\d+)");
    static readonly string[] Bases = { "FanTest_Brass", "GearTest_Gear", "GearTest_GearLight" };
    const string Dir = "Assets/FuwaStage2/Materials/GearMetal";

    [MenuItem("FuwaCourse/ステージ2/2–4 歯車だけ金属っぽくする")]
    public static void Menu() { Debug.Log(Apply(GameObject.Find("FuwaCourse2_4")?.transform)); }

    public static string Apply(Transform course)
    {
        if (!course) return "2–4 がない";
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Materials", "GearMetal");
        var bases = new Dictionary<string, Material>();
        foreach (var n in Bases) { var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/" + n + ".mat"); if (src) bases[n] = src; }
        var info = new Dictionary<Mesh, (float root, float R, bool pattern)>();
        var mats = new Dictionary<string, Material>();
        int n2 = 0;
        foreach (var mr in course.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || !GearMesh.IsMatch(mf.sharedMesh.name)) continue;
            var mesh = mf.sharedMesh;
            var ms = mr.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++)
            {
                if (!ms[i]) continue;
                string bn = BaseName(ms[i].name); if (bn == null || !bases.ContainsKey(bn)) continue;
                if (!info.TryGetValue(mesh, out var inf)) { inf = RadialUV(mesh); info[mesh] = inf; }
                string key = bn + "_" + mesh.name;
                if (!mats.TryGetValue(key, out var m)) { m = GearMat(bases[bn], key, inf); mats[key] = m; }
                ms[i] = m; ch = true;
            }
            if (ch) { mr.sharedMaterials = ms; EditorUtility.SetDirty(mr); n2++; }
        }
        AssetDatabase.SaveAssets();
        return "[歯車の金属] " + n2 + " 個（メッシュ " + info.Count + "・材質 " + mats.Count + "）";
    }

    // 元の材質の名前（_Metal や歯車ごとの複製からも元をたどる）
    static string BaseName(string n)
    {
        foreach (var b in Bases.OrderByDescending(x => x.Length)) if (n == b || n.StartsWith(b + "_")) return b;
        return null;
    }

    static Material GearMat(Material src, string key, (float root, float R, bool pattern) inf)
    {
        string p = Dir + "/" + key.Replace("/", "_") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, p); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.mainTexture = null;
        m.SetFloat("_MetalEnv", Env); m.SetFloat("_MetalSpec", Spec); m.SetFloat("_MetalSize", Size);
        m.SetFloat("_GearPattern", inf.pattern ? 1f : 0f); m.SetFloat("_GearRoot", inf.root); m.SetFloat("_GearR", inf.R);
        m.SetFloat("_GearBandDark", BandDark); m.SetFloat("_GearRingsPerM", RingsPerM); m.SetFloat("_GearRingStr", RingStr);
        EditorUtility.SetDirty(m); return m;
    }

    // 歯車の面を半径 1 にした平面の座標を uv に（軸は一番うすい向き）。根元の割合はふつうの歯車の (z-2.5)/(z+2)、歯の数が名前にない物（ラック）は帯なし
    static (float root, float R, bool pattern) RadialUV(Mesh m)
    {
        var v = m.vertices; if (v.Length == 0) return (2f, 1f, false);
        var b = m.bounds; var ext = b.size; int ax = ext.x <= ext.y && ext.x <= ext.z ? 0 : (ext.y <= ext.z ? 1 : 2);
        int a1 = ax == 0 ? 1 : 0, a2 = ax == 2 ? 1 : 2;
        var c = b.center; float R = 0f;
        foreach (var p in v) R = Mathf.Max(R, new Vector2(p[a1] - c[a1], p[a2] - c[a2]).magnitude);
        if (R <= 1e-5f) return (2f, 1f, false);
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(0.5f + (v[i][a1] - c[a1]) / (2f * R), 0.5f + (v[i][a2] - c[a2]) / (2f * R));
        m.uv = uv; EditorUtility.SetDirty(m);
        bool ring = m.name.Contains("Ring");
        var mz = Teeth.Match(m.name); float root = 2f;
        if (mz.Success && int.TryParse(mz.Groups[1].Value, out int z) && z > 4) root = (z - 2.5f) / (z + 2f);
        return (root, R, !ring);
    }
}
