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
    // 2026-11-13 てじゃ「乗れるギアだけ目立たせる方法ないかな」→ B「乗れない歯車を暗くして沈める」：乗れない歯車の色を Dim 倍（1 で なし）
    //  乗れる＝FuwaGearRide の歯車・窓のある歯車 HG・ごつい歯車 CG・段つき歯車 SG の、ふちより下に出ていない部品（下の動力の歯車・軸は暗くする）
    public static float Dim = 0.55f;
    // 2026-11-17 てじゃ「歯車の円形の模様、もうちょい中心よりにずらせないかな」：歯の帯（一段暗い所）と段差の線を、歯の根元よりこれだけ内へ（m）
    public static float RootIn = 0.1f;
    static readonly Regex RideRoot = new Regex(@"^(HG\d+_z\d+|CG\d+_z\d+|SG[AB])$");
    static HashSet<Transform> RideRoots(Transform course)
    {
        var set = new HashSet<Transform>();
        foreach (var t in course.GetComponentsInChildren<Transform>(true)) if (RideRoot.IsMatch(t.name)) set.Add(t);
        foreach (var gr in course.GetComponentsInChildren<FuwaGearRide>(true)) if (gr.gears != null) foreach (var g in gr.gears) if (g) set.Add(g);
        return set;
    }
    static bool IsRide(MeshRenderer mr, HashSet<Transform> roots)
    {
        for (var t = mr.transform; t; t = t.parent) if (roots.Contains(t)) return mr.bounds.max.y >= t.position.y - 0.05f;
        return false;
    }
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
        var rides = RideRoots(course); var dimGears = new HashSet<Transform>();
        int n2 = 0, nDim = 0;
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
                bool dim = Dim < 0.999f && !IsRide(mr, rides) && !InTank(mr.transform);
                string key = bn + "_" + mesh.name + (dim ? "_Dim" : "");
                if (!mats.TryGetValue(key, out var m)) { m = GearMat(bases[bn], key, inf, dim ? Dim : 1f); mats[key] = m; }
                if (dim) { nDim++; dimGears.Add(mr.transform); }
                ms[i] = m; ch = true;
            }
            if (ch) { mr.sharedMaterials = ms; EditorUtility.SetDirty(mr); n2++; }
        }
        // 2026-11-13 てじゃ「黄色が濃いところに明るい黄色のパーツつながってると気になる」→ 暗くした歯車にくっついている部品（軸・輪・線など、歯車でないメッシュ）も同じだけ暗く
        //  くっついている＝親が暗くした歯車／乗れる歯車の下の部品／動力クリスタルの金具（暗いでかい歯車につく）
        int nPart = 0;
        if (Dim < 0.999f)
            foreach (var mr in course.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh && GearMesh.IsMatch(mf.sharedMesh.name)) continue;
                bool dim = false; Transform rr = null;
                for (var t = mr.transform; t; t = t.parent) if (rides.Contains(t)) { rr = t; break; }
                if (InTank(mr.transform)) dim = false;
                else if (rr) dim = !IsRide(mr, rides);
                else if (mr.transform.parent && dimGears.Contains(mr.transform.parent)) dim = true;
                else for (var t = mr.transform; t; t = t.parent) if (t.name == "CrystalMount") { dim = true; break; }
                var ms = mr.sharedMaterials; bool ch = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (!ms[i]) continue;
                    string bn = PartBase(ms[i].name); if (bn == null) continue;
                    var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/" + bn + ".mat"); if (!src) continue;
                    var want = dim ? PartDimMat(src, bn) : src;
                    if (ms[i] != want) { ms[i] = want; ch = true; }
                }
                if (ch) { mr.sharedMaterials = ms; EditorUtility.SetDirty(mr); nPart++; }
            }
        AssetDatabase.SaveAssets();
        return "[歯車の金属] " + n2 + " 個（部品の入れかえ " + nPart + "・暗くした " + nDim + "・メッシュ " + info.Count + "・材質 " + mats.Count + "）";
    }

    static readonly string[] PartBases = { "FanTest_Brass", "GearTest_Gear", "GearTest_GearLight", "GearTest_Axle" };
    // 2026-11-13 てじゃ「液体タンク周りは明るい黄色でいいかも」→ 回るタンク（SpinTank）の中は歯車も部品も暗くしない
    static bool InTank(Transform t) { for (; t; t = t.parent) if (t.name.StartsWith("SpinTank")) return true; return false; }   // SpinTank_Carrier（下の遊星歯車3枚、てじゃ「3枚あるやつ」も）
    static string PartBase(string n) { foreach (var b in PartBases.OrderByDescending(x => x.Length)) if (n == b || n == b + "_PartDim") return b; return null; }
    static Material PartDimMat(Material src, string bn)
    {
        string p = Dir + "/" + bn + "_PartDim.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, p); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        DimColor(m, Dim); EditorUtility.SetDirty(m); return m;
    }
    static void DimColor(Material m, float dim)
    {
        if (dim >= 0.999f || !m.HasProperty("_Color")) return;
        var c = m.GetColor("_Color"); float g = c.grayscale; var d = Color.Lerp(c, new Color(g, g, g, c.a), 0.3f) * dim; d.a = c.a; m.SetColor("_Color", d);   // 少し色を抜いて暗く
    }
    // 元の材質の名前（_Metal や歯車ごとの複製からも元をたどる）
    static string BaseName(string n)
    {
        foreach (var b in Bases.OrderByDescending(x => x.Length)) if (n == b || n.StartsWith(b + "_")) return b;
        return null;
    }

    static Material GearMat(Material src, string key, (float root, float R, bool pattern) inf, float dim)
    {
        string p = Dir + "/" + key.Replace("/", "_") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, p); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.mainTexture = null;
        DimColor(m, dim);
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
        if (mz.Success && int.TryParse(mz.Groups[1].Value, out int z) && z > 4) root = Mathf.Max(0.3f, (z - 2.5f) / (z + 2f) - RootIn / R);
        return (root, R, !ring);
    }
}
