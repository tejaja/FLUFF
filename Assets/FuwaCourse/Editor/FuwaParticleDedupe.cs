using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// シーンのダイエット（2026-11-25 てじゃ「そもそもシーンをもっとダイエットさせられないかな」「やっちゃっていいよ」）
//  シーンの大きさの6割がパーティクル（ParticleSystem は1個でシーンのファイル上 約76〜115KB）。同じ設定の粒が何か所にも並んでいる（ゴール12か所・銃12丁・キノコの群れ…）。
//  → 設定がまったく同じ粒（粒の部品だけでできた小さな木。子の粒・サブエミッターも含む）を見つけて、1つのプレハブ（Generated/ParticlePrefabs）にし、全部をそのプレハブの置き物に置きかえる。
//    置き場所・名前・向き・大きさは置き物ごと（プレハブの上書き）。スクリプト・Udon からの参照は、置きかえた先の同じ部品につなぎ直す。
//  ・まとめない物：プレハブの中の粒／粒・描画・Transform 以外の部品がある木／木の外のシーンの物を参照している粒（当たり判定の板・形のメッシュの描画物など）
//  ・ビルダーで作り直して粒がまた増えたら、このメニューをもう一度（同じ設定なら同じプレハブを使い回す）。使われなくなったプレハブは消す
public static class FuwaParticleDedupe
{
    const string Dir = "Assets/FuwaCourse/Generated/ParticlePrefabs";

    [MenuItem("FuwaCourse/よく使う/同じ設定の粒をプレハブにまとめる（シーンのダイエット）")]
    static void Menu() { Debug.Log(Run(false)); }
    [MenuItem("FuwaCourse/よく使う/同じ設定の粒をプレハブにまとめる（数えるだけ）")]
    static void MenuDry() { Debug.Log(Run(true)); }

    public static string Run(bool dry)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // 粒の木の根っこ：粒があって、親に粒がない（または親が粒の部品だけの木ではない）
        var roots = new List<GameObject>();
        foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
        {
            if (ps == null || EditorUtility.IsPersistent(ps) || ps.gameObject.scene != scene) continue;
            if (PrefabUtility.IsPartOfAnyPrefab(ps)) continue;
            var p = ps.transform.parent;
            if (p != null && p.GetComponent<ParticleSystem>() != null) continue;   // 子の粒は根っこと一緒に
            roots.Add(ps.gameObject);
        }
        // 木ごとの「設定の指紋」
        var groups = new Dictionary<string, List<GameObject>>();
        int skipped = 0; var skipWhy = new Dictionary<string, int>();
        foreach (var r in roots)
        {
            string why;
            string sig = Signature(r, out why);
            if (sig == null) { skipped++; skipWhy[why] = skipWhy.TryGetValue(why, out var c) ? c + 1 : 1; continue; }
            if (!groups.TryGetValue(sig, out var list)) groups[sig] = list = new List<GameObject>();
            list.Add(r);
        }
        var multi = groups.Where(g => g.Value.Count >= 2).ToList();
        int replace = multi.Sum(g => g.Value.Count);
        var sb = new StringBuilder();
        sb.Append("[粒まとめ] 粒の木 " + roots.Count + "（まとめられない " + skipped + "：" + string.Join(" / ", skipWhy.Select(k => k.Key + " " + k.Value)) + "）\n");
        sb.Append("  同じ設定の組 " + multi.Count + "・置きかえる木 " + replace + "\n");
        foreach (var g in multi.OrderByDescending(g => g.Value.Count).Take(40)) sb.Append("   " + g.Value.Count + "× " + g.Value[0].name + " <" + (g.Value[0].transform.parent ? g.Value[0].transform.parent.name : "-") + ">\n");
        if (dry) return sb.ToString();

        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "ParticlePrefabs");
        // シーンの参照を先に集めておく（古い部品 → 新しい部品につなぎ直す）
        var remap = new Dictionary<Object, Object>();
        int done = 0;
        var usedPrefabs = new HashSet<string>();
        foreach (var g in multi)
        {
            var first = g.Value[0];
            string hash = Hash(g.Key);
            string path = Dir + "/" + Safe(first.name) + "_" + hash + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var tmp = Object.Instantiate(first);
                tmp.name = first.name;
                tmp.transform.SetParent(null, false);
                tmp.transform.localPosition = Vector3.zero; tmp.transform.localRotation = Quaternion.identity; tmp.transform.localScale = Vector3.one;
                prefab = PrefabUtility.SaveAsPrefabAsset(tmp, path);
                Object.DestroyImmediate(tmp);
            }
            usedPrefabs.Add(path);
            foreach (var old in g.Value)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
                Undo.RegisterCreatedObjectUndo(inst, "粒まとめ");
                inst.name = old.name;
                inst.transform.localPosition = old.transform.localPosition; inst.transform.localRotation = old.transform.localRotation; inst.transform.localScale = old.transform.localScale;
                inst.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                if (inst.activeSelf != old.activeSelf) inst.SetActive(old.activeSelf);
                // 部品の対応（同じ並びの木なので、順番どおりに対応する）
                var a = old.GetComponentsInChildren<Transform>(true); var b = inst.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < a.Length && i < b.Length; i++)
                {
                    remap[a[i]] = b[i]; remap[a[i].gameObject] = b[i].gameObject;
                    var ca = a[i].GetComponents<Component>(); var cb = b[i].GetComponents<Component>();
                    for (int k = 0; k < ca.Length && k < cb.Length; k++) remap[ca[k]] = cb[k];
                    if (b[i] != inst.transform && b[i].name != a[i].name) { /* 子の名前は同じはず */ }
                }
                done++;
            }
        }
        // シーンのすべての部品の参照をつなぎ直す（Udon の publicVariablesUnityEngineObjects も）
        int fixedRefs = 0;
        var touched = new HashSet<Component>();
        foreach (var c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c == null || EditorUtility.IsPersistent(c) || c.gameObject.scene != scene) continue;
            if (remap.ContainsKey(c)) continue;   // 消す側
            var so = new SerializedObject(c); var it = so.GetIterator(); bool ch = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var v = it.objectReferenceValue;
                if (v != null && remap.TryGetValue(v, out var nv)) { it.objectReferenceValue = nv; ch = true; fixedRefs++; }
            }
            if (ch) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(c); touched.Add(c); }
        }
        foreach (var c in touched) if (c is UdonSharp.UdonSharpBehaviour usb) UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(usb);
        // 古い木を消す
        foreach (var g in multi) foreach (var old in g.Value) if (old != null) Undo.DestroyObjectImmediate(old);
        // 使われていない前のプレハブを消す（シーンのどこにも置かれていない物）
        int removed = 0;
        var placed = new HashSet<string>();
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene == scene && PrefabUtility.IsAnyPrefabInstanceRoot(go)) placed.Add(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go));
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Dir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!placed.Contains(p)) { AssetDatabase.DeleteAsset(p); removed++; }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        sb.Append("  置きかえ " + done + "・参照つなぎ直し " + fixedRefs + "（部品 " + touched.Count + "）・使われないプレハブ削除 " + removed);
        return sb.ToString();
    }

    // 粒の木の指紋。まとめられない時は null（why に理由）
    static string Signature(GameObject root, out string why)
    {
        why = null;
        var sb = new StringBuilder();
        var trs = root.GetComponentsInChildren<Transform>(true);
        var inTree = new HashSet<Object>();
        foreach (var t in trs) { inTree.Add(t); inTree.Add(t.gameObject); foreach (var c in t.GetComponents<Component>()) if (c != null) inTree.Add(c); }
        for (int i = 0; i < trs.Length; i++)
        {
            var t = trs[i];
            if (PrefabUtility.IsPartOfAnyPrefab(t)) { why = "プレハブ入り"; return null; }
            sb.Append("#T").Append(i).Append(' ').Append(Rel(root.transform, t)).Append('|');
            if (i > 0) sb.Append(t.localPosition.ToString("F4")).Append(t.localRotation.ToString("F4")).Append(t.localScale.ToString("F4"));
            var go = t.gameObject;
            sb.Append(go.layer).Append(go.tag).Append(GameObjectUtility.GetStaticEditorFlags(go)).Append(i > 0 ? go.activeSelf.ToString() : "");
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) { why = "スクリプトが外れている"; return null; }
                if (c is Transform) continue;
                if (!(c is ParticleSystem) && !(c is ParticleSystemRenderer)) { why = c.GetType().Name + " つき"; return null; }
                sb.Append("#C").Append(c.GetType().Name);
                var so = new SerializedObject(c); var it = so.GetIterator();
                while (it.Next(true))
                {
                    string n = it.propertyPath;
                    if (n.StartsWith("m_GameObject") || n.StartsWith("m_PrefabInstance") || n.StartsWith("m_PrefabAsset") || n.StartsWith("m_CorrespondingSourceObject") || n == "m_ObjectHideFlags") continue;
                    if (n.EndsWith(".m_FileID") || n.EndsWith(".m_PathID")) continue;   // 参照の中身の数（参照そのものは上の ObjectReference で見る）
                    if (n == "randomSeed" && c is ParticleSystem aps && aps.useAutoRandomSeed) continue;   // 自動の時は使われない（置き物ごとにちがう数が入っている）
                    switch (it.propertyType)
                    {
                        case SerializedPropertyType.ObjectReference:
                            var v = it.objectReferenceValue;
                            if (v == null) sb.Append(n).Append("=0;");
                            else if (inTree.Contains(v)) sb.Append(n).Append("=in:").Append(RelObj(root.transform, v)).Append(';');
                            else if (EditorUtility.IsPersistent(v)) sb.Append(n).Append("=a:").Append(AssetDatabase.GetAssetPath(v)).Append(':').Append(v.name).Append(';');
                            else { why = "木の外の物を参照"; return null; }
                            break;
                        case SerializedPropertyType.Float: sb.Append(it.floatValue.ToString("R")).Append(';'); break;
                        case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.LayerMask: case SerializedPropertyType.ArraySize: sb.Append(it.longValue).Append(';'); break;
                        case SerializedPropertyType.Boolean: sb.Append(it.boolValue ? '1' : '0'); break;
                        case SerializedPropertyType.String: sb.Append(it.stringValue).Append(';'); break;
                        case SerializedPropertyType.Color: sb.Append(it.colorValue.ToString("R")).Append(';'); break;
                        case SerializedPropertyType.Vector2: sb.Append(it.vector2Value.ToString("R")).Append(';'); break;
                        case SerializedPropertyType.Vector3: sb.Append(it.vector3Value.ToString("R")).Append(';'); break;
                        case SerializedPropertyType.Vector4: sb.Append(it.vector4Value.ToString("R")).Append(';'); break;
                        case SerializedPropertyType.Quaternion: sb.Append(it.quaternionValue.ToString("R")).Append(';'); break;
                        default: break;   // AnimationCurve・Gradient などは中の数値の子として出てくる
                    }
                }
            }
        }
        return sb.ToString();
    }

    static string Rel(Transform root, Transform t) { var s = ""; while (t != null && t != root) { s = t.name + "/" + s; t = t.parent; } return s; }
    static string RelObj(Transform root, Object o)
    {
        Transform t = o is GameObject g ? g.transform : o is Component c ? c.transform : null;
        return (t != null ? Rel(root, t) : "?") + ":" + o.GetType().Name;
    }
    static string Hash(string s)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            var h = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
            return System.BitConverter.ToString(h, 0, 4).Replace("-", "").ToLowerInvariant();
        }
    }
    static string Safe(string n) { foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_'); return n; }
}
