using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// シーンを軽くする（2026-10-18 シーンが 80MB になり GitHub の 50MB 警告。中身の 74% がパーティクル 552 個）。
// 同じ名前のパーティクル（いちばん上の粒の物＋子）が 2 個以上あるものをプレハブにして、シーンの物をその場でプレハブのインスタンスに変換する。
// 物はそのまま（スクリプトからの参照は切れない）。元と違う所は上書きとしてシーンに残る（シーンには違いだけが書かれる）。
// ビルダーで作り直すと普通の物に戻るので、作り直した後にもう一度実行する（何度実行しても同じ。プレハブ済みの物は飛ばす）
public static class FuwaFxPrefabize
{
    const string Dir = "Assets/FuwaCourse/Prefabs/FX";

    [MenuItem("FuwaCourse/よく使う/パーティクルをプレハブにしてシーンを軽くする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        Folder(Dir);
        var groups = new Dictionary<string, List<GameObject>>();
        foreach (var ps in Object.FindObjectsOfType<ParticleSystem>(true))
        {
            var go = ps.gameObject;
            if (EditorUtility.IsPersistent(go) || PrefabUtility.IsPartOfAnyPrefab(go)) continue;
            if (HasParticleParent(go.transform)) continue;   // いちばん上の粒の物だけ（子はいっしょにプレハブに入る）
            if (go.GetComponentsInChildren<Component>(true).Any(c => c == null || !(c is Transform || c is ParticleSystem || c is ParticleSystemRenderer))) continue;   // 粒だけの物（Udon などは入れない）
            string key = go.name + "|" + Structure(go.transform);
            if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<GameObject>();
            l.Add(go);
        }
        var settings = new ConvertToPrefabInstanceSettings
        {
            objectMatchMode = ObjectMatchMode.ByHierarchy,
            componentsNotMatchedBecomesOverride = true,
            gameObjectsNotMatchedBecomesOverride = true,
            recordPropertyOverridesOfMatches = true,
            changeRootNameToAssetName = false,
            logInfo = false,
        };
        int made = 0, converted = 0;
        foreach (var kv in groups)
        {
            var list = kv.Value;
            string name = list[0].name;
            string path = Dir + "/" + Safe(name) + "_" + Hash(kv.Key) + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                if (list.Count < 2) continue;
                // いちばん多い設定のものを元にする（違いが少ないほどシーンに残る上書きが減る）
                var src = list.GroupBy(Signature).OrderByDescending(g => g.Count()).First().First();
                asset = PrefabUtility.SaveAsPrefabAsset(src, path);
                made++;
            }
            PrefabUtility.ConvertToPrefabInstances(list.ToArray(), asset, settings, InteractionMode.UserAction);
            converted += list.Count;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        return $"[プレハブ化] プレハブ {made} 個を作って、{converted} 個をインスタンスに変換";
    }

    static bool HasParticleParent(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.GetComponent<ParticleSystem>() != null) return true;
        return false;
    }

    // 子の並び（名前の木）。同じ名前でも中身の形が違う物は別のプレハブに
    static string Structure(Transform t)
    {
        var sb = new System.Text.StringBuilder();
        void Walk(Transform x, string pre) { foreach (Transform c in x) { sb.Append(pre + c.name + ";"); Walk(c, pre + c.name + "/"); } }
        Walk(t, "");
        return sb.ToString();
    }

    // 粒の設定の中身（乱数の種は物ごとに違うので除く）
    static string Signature(GameObject go)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in go.GetComponentsInChildren<Component>(true))
        {
            if (c is Transform) continue;
            sb.Append(Regex.Replace(EditorJsonUtility.ToJson(c), "\"(randomSeed|m_RandomSeed|autoRandomSeed)\":[^,}]*", ""));
        }
        return sb.ToString();
    }

    static string Hash(string s) { unchecked { uint h = 2166136261; foreach (char ch in s) { h ^= ch; h *= 16777619; } return h.ToString("x8"); } }
    static string Safe(string s) => Regex.Replace(s, "[^A-Za-z0-9_]", "_");
    static void Folder(string p) { if (AssetDatabase.IsValidFolder(p)) return; var i = p.LastIndexOf('/'); Folder(p.Substring(0, i)); AssetDatabase.CreateFolder(p.Substring(0, i), p.Substring(i + 1)); }
}
