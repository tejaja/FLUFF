using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ステージ2の動かない物に BatchingStatic を付ける（2026-11-01 負荷対策）。ビルダーの最後でも呼ぶので、作り直しても消えない。何度実行してもOK。
// 付けないもの：スクリプトが動かす物（下の StillScripts 以外の U# の下、または参照先）、Animator・Rigidbody の下、オブジェクト座標を使う材質（静的バッチで結合されると座標が変わる）
public static class FuwaStage2Static
{
    // 自分の見た目を動かさないスクリプト
    static readonly HashSet<string> StillScripts = new HashSet<string> { "FuwaWater", "FuwaShallowWater", "FuwaHazard", "FuwaTopOut", "FuwaPlayerCatcher", "FuwaWindArea", "FuwaFireJet", "FuwaHeatSource", "FuwaFarShadows", "FuwaCourseLod" };

    [MenuItem("FuwaCourse/ステージ2/ステージ2の動かない物を静的バッチにする")]
    static void Menu()
    {
        foreach (var name in new[] { "FuwaCourse2_1", "FuwaCourse2_2" })
        {
            var root = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t && t.name == name && t.parent == null && t.gameObject.scene.IsValid());
            if (root) Apply(root);
        }
    }

    static bool SafeMaterial(Material m)
    {
        if (!m) return true;
        if (m.shader.name == "FuwaCourse/Lava") return true;   // ワールド座標で模様を取る
        if (m.shader.name != "FuwaCourse/ToonLit") return false;
        // ToonLit のふもとの色と模様はオブジェクト座標の高さを使う
        if (m.HasProperty("_BaseFadeLength") && m.GetFloat("_BaseFadeLength") > 0f) return false;
        if (m.HasProperty("_PatternFromBase") && m.GetFloat("_PatternFromBase") > 0.5f) return false;
        return true;
    }

    // シーン中のスクリプトの欄から、動かされるかもしれない物を集める
    static HashSet<Transform> Referenced()
    {
        var set = new HashSet<Transform>();
        void Add(object v)
        {
            if (v is Component c && c) set.Add(c.transform);
            else if (v is GameObject g && g) set.Add(g.transform);
        }
        foreach (var u in Resources.FindObjectsOfTypeAll<UdonSharp.UdonSharpBehaviour>())
        {
            if (!u || !u.gameObject.scene.IsValid() || StillScripts.Contains(u.GetType().Name)) continue;
            foreach (var f in u.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var v = f.GetValue(u);
                if (v is System.Array a) { foreach (var e in a) Add(e); }
                else Add(v);
            }
        }
        return set;
    }

    public static int Apply(Transform root)
    {
        var refs = Referenced(); int count = 0; var why = new Dictionary<string, int>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)) continue;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            if (!r.sharedMaterials.All(SafeMaterial)) continue;
            bool moves = false;
            for (var p = r.transform; p && p != root && !moves; p = p.parent)   // コースの親はエリアの出し入れで参照されるだけなので見ない
            {
                if (refs.Contains(p) || p.GetComponent<Animator>() || p.GetComponent<Rigidbody>()) { moves = true; why[p.name] = why.TryGetValue(p.name, out var w) ? w + 1 : 1; }
                else if (p.GetComponents<UdonSharp.UdonSharpBehaviour>().Any(u => u && !StillScripts.Contains(u.GetType().Name))) { moves = true; why[p.name] = why.TryGetValue(p.name, out var w) ? w + 1 : 1; }
            }
            if (moves) continue;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.BatchingStatic);   // ContributeGI は足さない（焼きに影響しない）
            count++;
        }
        Debug.Log("[ステージ2] " + root.name + " 静的バッチ +" + count + "（動く物として除外：" + string.Join(", ", why.OrderByDescending(k => k.Value).Take(12).Select(k => k.Key + "×" + k.Value)) + "）");
        return count;
    }
}
