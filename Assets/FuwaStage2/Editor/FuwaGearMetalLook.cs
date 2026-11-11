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
    public static float Env = 0.5f, Spec = 0.6f, Size = 0.93f;
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
        int n2 = 0;
        foreach (var mr in course.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || !GearMesh.IsMatch(mf.sharedMesh.name)) continue;
            var ms = mr.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++) if (ms[i] && map.TryGetValue(ms[i], out var to)) { ms[i] = to; ch = true; }
            if (ch) { mr.sharedMaterials = ms; EditorUtility.SetDirty(mr); n2++; }
        }
        AssetDatabase.SaveAssets();
        return "[歯車の金属] " + n2 + " 個";
    }
}
