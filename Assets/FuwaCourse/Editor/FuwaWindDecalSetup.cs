using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 風のエリアの地面の模様を、道の形の板（Fuwa/GroundHighlight）から「地面に投影する箱」（Fuwa/WindDecal）に置き換える（2026-10-12）。
// 板だと道の外（芝のふち・島）に出なかった（てじゃ「風の地面エフェクトが芝に出てない」）。
// 風のエリア（FuwaWindArea の箱）の足もと（箱の底から下へ 0.6m〜上へ 0.9m）にある地面全部に描く。前の板は非表示にして残す。
// FuwaWindArea.groundHighlight を新しい箱に付け替える（色・流れ・太さはスクリプトがこれまでどおり動かす）。
// 風のエリアを作り直した（道の板が付け直された）後はもう一度実行する。何度実行しても同じ結果
public static class FuwaWindDecalSetup
{
    const string Dir = "Assets/FuwaCourse/Generated/WindDecal";
    const float Below = 0.6f, Height = 1.5f;   // 上は 0.9m まで（ふわふわ・アバターの頭に模様が乗らないように）
    const float JoinExtend = 0.6f;             // つながった箱のすき間を埋める長さ(m)。はみ出た分は境目の面で切る

    [MenuItem("FuwaCourse/風の地面の模様を地面に投影する形にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "WindDecal");
        var shader = Shader.Find("Fuwa/WindDecal");
        int n = 0; var log = new System.Text.StringBuilder();
        foreach (var w in Object.FindObjectsOfType<FuwaWindArea>(true))
        {
            var old = w.groundHighlight;
            if (old == null) continue;
            var bc = w.GetComponent<BoxCollider>(); if (bc == null) continue;
            var t = w.transform;
            var decalT = t.Find("WindDecal");
            // 元の板（前回付け替えた後なら、元の板は非表示のまま見つける）
            Renderer src = old.name == "WindDecal" ? null : old;
            Material srcMat = src != null ? src.sharedMaterial : (decalT != null ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(decalT.GetComponent<Renderer>().sharedMaterial)) : null);
            if (srcMat == null) continue;
            if (decalT == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "WindDecal";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(t, false); decalT = go.transform;
            }
            // 箱：風のエリアの足もと。横・前後は風のエリアと同じ
            Vector3 c = bc.center, s = bc.size;
            float sy = Mathf.Abs(t.lossyScale.y) > 1e-4f ? t.lossyScale.y : 1f;
            float bottomL = c.y - s.y * 0.5f;                       // 箱の底（風のエリアのローカル）
            float hL = Height / sy, belowL = Below / sy;
            decalT.localPosition = new Vector3(c.x, bottomL - belowL + hL * 0.5f, c.z);
            decalT.localRotation = Quaternion.identity;
            decalT.localScale = new Vector3(s.x, hL, s.z);
            var r = decalT.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(decalT.gameObject, 0);
            // ふつうのレイヤー（当たりは無いので Ignore Raycast にする必要はない）。
            // 前は Ignore Raycast で、VRChat のカメラ（写真）に写らなかった（2026-10-16 てじゃ「風の床のグラデーション、VRChatのカメラにうつんない」）
            decalT.gameObject.layer = 0;
            // 材質：エリアごと（風の向きが違うので）。元の板の値を写す
            string key = t.parent != null ? t.parent.name + "_" + t.name : t.name;
            string path = Dir + "/WindDecal_" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            if (src != null) { m.shader = srcMat.shader; m.CopyPropertiesFromMaterial(srcMat); }
            m.shader = shader;
            Vector3 d = w.localDirection; d.y = 0f; if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
            m.SetVector("_WindDir", new Vector4(d.x, 0f, d.z, 0f));
            m.renderQueue = -1;
            EditorUtility.SetDirty(m);
            r.sharedMaterial = m;
            r.enabled = old.enabled;
            // 付け替え
            if (src != null) { src.gameObject.SetActive(false); }
            var so = new SerializedObject(w); so.FindProperty("groundHighlight").objectReferenceValue = r; so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditorUtility.CopyProxyToUdon(w); EditorUtility.SetDirty(w);
            n++; log.Append(key + " ");
        }
        // 同じ親の下でつながった風のエリア（かぜのおかの追い風 Zone0〜4）は、間の風上・風下のふちをぼかさない（つなぎ目が薄くならないように）
        foreach (var g in Object.FindObjectsOfType<FuwaWindArea>(true).Where(w => w.transform.Find("WindDecal") != null).GroupBy(w => w.transform.parent))
        {
            var list = g.ToList();
            Vector3 wdW = list[0].transform.TransformDirection(list[0].localDirection); wdW.y = 0f; wdW.Normalize();
            list.Sort((a, b) => Vector3.Dot(a.transform.position, wdW).CompareTo(Vector3.Dot(b.transform.position, wdW)));
            // 境目の面：カーブに沿って回した箱は内側でくさび形に重なって二重に塗られ、外側はすき間になる
            // （2026-10-16 てじゃ「グラデのとこになんか濃い境目」）。隣どうしの境目（前の箱の風下のふちと次の箱の風上のふちの中間、
            // 向きは2つの風向きの平均）から先は塗らない。すき間を埋めるため、つながる側は箱を JoinExtend だけ長くする
            var front = new Vector3[list.Count]; var back = new Vector3[list.Count]; var fwd = new Vector3[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var dt = list[i].transform.Find("WindDecal");
                Vector3 dl = list[i].localDirection; dl.y = 0f; dl = dl.sqrMagnitude < 1e-6f ? Vector3.forward : dl.normalized;
                front[i] = dt.TransformPoint(dl * 0.5f); back[i] = dt.TransformPoint(-dl * 0.5f);
                fwd[i] = dt.TransformDirection(dl); fwd[i].y = 0f; fwd[i].Normalize();
            }
            for (int i = 0; i < list.Count; i++)
            {
                var dt = list[i].transform.Find("WindDecal");
                var m = dt.GetComponent<Renderer>().sharedMaterial;
                m.SetFloat("_FadeBack", i == 0 ? 0.6f : 0f);
                m.SetFloat("_FadeFront", i == list.Count - 1 ? 0.6f : 0f);
                Vector4 cutF = Vector4.zero, cutB = Vector4.zero;
                if (i < list.Count - 1)
                {
                    Vector3 p = (front[i] + back[i + 1]) * 0.5f, nn = (fwd[i] + fwd[i + 1]).normalized;
                    cutF = new Vector4(nn.x, nn.y, nn.z, Vector3.Dot(p, nn));
                }
                if (i > 0)
                {
                    Vector3 p = (front[i - 1] + back[i]) * 0.5f, nn = -(fwd[i - 1] + fwd[i]).normalized;
                    cutB = new Vector4(nn.x, nn.y, nn.z, Vector3.Dot(p, nn));
                }
                m.SetVector("_CutFront", cutF); m.SetVector("_CutBack", cutB);
                EditorUtility.SetDirty(m);
                // つながる側を長くする（風のエリアのローカルで）
                Vector3 dl = list[i].localDirection; dl.y = 0f; dl = dl.sqrMagnitude < 1e-6f ? Vector3.forward : dl.normalized;
                float perM = 1f / Mathf.Max(1e-4f, list[i].transform.TransformVector(dl).magnitude);
                float eF = (i < list.Count - 1 ? JoinExtend : 0f) * perM, eB = (i > 0 ? JoinExtend : 0f) * perM;
                dt.localPosition += dl * (eF - eB) * 0.5f;
                Vector3 ad = new Vector3(Mathf.Abs(dl.x), 0f, Mathf.Abs(dl.z));
                dt.localScale += ad * (eF + eB);
            }
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "wind decals " + n + ": " + log;
    }
}
