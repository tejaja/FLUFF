using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 2026-11-15 てじゃ「影響範囲に仮の透明メッシュ置いて2-2方式とかできないの」→ A案：
//  風の地面の模様（Fuwa/WindDecal）は深度に投影するので、箱に入ったアバターにも塗っていた。
//  → 箱ごとに「見た目の地面の高さ」を下向きレイで測って小さいテクスチャ（高さマップ）に焼き、シェーダーで地面＋数cmより上は塗らない。
//  ・レイは当たり判定ではなく見た目のメッシュに当てる（一時的に MeshCollider を作る）。芝のふちなど見た目だけの物も地面になる
//  ・上向きの面だけ地面にする（草の葉・柱の横は飛ばす）。マスの中は細かく測って一番高い所（でこぼこの上も塗れるように）
//  ・地面のないマスは、となりの地面のマスで埋める（ふちが1マスぶん欠けないように）
//  ・材質は箱ごとにひとつ（ほかの箱と共有していたら複製する）
public static class FuwaWindDecalHeight
{
    const string Dir = "Assets/FuwaCourse/Generated/WindDecal";
    const string HDir = "Assets/FuwaCourse/Generated/WindDecal/Height";
    const int TempLayer = 31;
    public static float Texel = 0.2f;   // m
    const int Sub = 3;                  // マスの中を Sub×Sub で測る

    [MenuItem("FuwaCourse/風の地面の模様：地面の高さを焼き直す（アバターに写らないように）")]
    public static void BakeAllMenu() { Debug.Log(BakeAll()); }

    public static string BakeAll(Transform under = null)
    {
        var decals = Object.FindObjectsOfType<MeshRenderer>(true).Where(r => r.sharedMaterial && r.sharedMaterial.shader && r.sharedMaterial.shader.name == "Fuwa/WindDecal" && !InPath(r.transform, "BuildTemplates") && (under == null || r.transform.IsChildOf(under))).ToList();
        if (decals.Count == 0) return "風の模様なし";
        if (!AssetDatabase.IsValidFolder(HDir)) AssetDatabase.CreateFolder(Dir, "Height");
        // 共有している材質は箱ごとに分ける（高さマップが箱ごとなので）
        var all = Object.FindObjectsOfType<MeshRenderer>(true).Where(r => r.sharedMaterial && r.sharedMaterial.shader && r.sharedMaterial.shader.name == "Fuwa/WindDecal").ToList();
        var temps = new List<GameObject>();
        var sb = new System.Text.StringBuilder();
        try
        {
            foreach (var d in decals)
            {
                if (all.Count(o => o != d && o.sharedMaterial == d.sharedMaterial) > 0)
                {
                    string key = (d.transform.parent && d.transform.parent.parent ? d.transform.parent.parent.name + "_" : "") + (d.transform.parent ? d.transform.parent.name : d.name);
                    string mp = Dir + "/WindDecal_" + key + ".mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                    if (!m) { m = new Material(d.sharedMaterial); AssetDatabase.CreateAsset(m, mp); }
                    else m.CopyPropertiesFromMaterial(d.sharedMaterial);
                    d.sharedMaterial = m; EditorUtility.SetDirty(d);
                }
            }
            // 地面になりうる見た目のメッシュに一時的な当たり（箱たちのまわりだけ）
            Bounds area = decals[0].bounds; foreach (var d in decals) area.Encapsulate(d.bounds);
            var decalSet = new HashSet<Renderer>(all.Cast<Renderer>());
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>(false))
            {
                if (!r.enabled || decalSet.Contains(r) || r.name == "GroundHighlight") continue;
                if (!r.bounds.Intersects(area) || !decals.Any(d => d.bounds.Intersects(r.bounds))) continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                if (r.GetComponentInParent<Rigidbody>()) continue;   // 動く物・持てる物
                if (InPath(r.transform, "CourseLites")) continue;     // ほかのコースの軽い版（重なっている）
                if (!IsLod0(r)) continue;
                if (r.sharedMaterials.Any(m => m && m.renderQueue >= 2500)) continue;   // 透明な物（水・もや・ほかの模様）
                var g = new GameObject("__WindDecalHeightTmp"); g.hideFlags = HideFlags.HideAndDontSave; g.layer = TempLayer;
                g.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation); g.transform.localScale = r.transform.lossyScale;
                var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
                temps.Add(g);
            }
            Physics.SyncTransforms();
            foreach (var d in decals) sb.Append(Bake(d) + "; ");
        }
        finally { foreach (var g in temps) Object.DestroyImmediate(g); Physics.SyncTransforms(); }
        AssetDatabase.SaveAssets();
        return "風の模様の高さマップ " + decals.Count + " 個（" + sb + "）";
    }

    static bool InPath(Transform t, string s) { for (; t; t = t.parent) if (t.name.Contains(s)) return true; return false; }
    static bool IsLod0(Renderer r)
    {
        var lg = r.GetComponentInParent<LODGroup>(); if (!lg) return true;
        var lods = lg.GetLODs(); if (lods.Length == 0) return true;
        if (lods[0].renderers.Contains(r)) return true;
        return !lods.Any(l => l.renderers.Contains(r));
    }

    static string Bake(MeshRenderer d)
    {
        var t = d.transform; var sc = t.lossyScale;
        int w = Mathf.Clamp(Mathf.CeilToInt(sc.x / Texel), 4, 256), h = Mathf.Clamp(Mathf.CeilToInt(sc.z / Texel), 4, 256);
        var hv = new float[w * h]; var has = new bool[w * h];
        Vector3 up = t.up; float len = sc.y;
        int hits = 0;
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float best = float.NegativeInfinity;
                for (int sj = 0; sj < Sub; sj++)
                    for (int si = 0; si < Sub; si++)
                    {
                        float u = (i + (si + 0.5f) / Sub) / w - 0.5f, v = (j + (sj + 0.5f) / Sub) / h - 0.5f;
                        var o = t.TransformPoint(new Vector3(u, 0.5f, v)) + up * 0.01f;
                        var rs = Physics.RaycastAll(o, -up, len + 0.02f, 1 << TempLayer, QueryTriggerInteraction.Ignore);
                        foreach (var hit in rs)
                        {
                            if (Vector3.Dot(hit.normal, up) < 0.55f) continue;   // 上向きの面だけ
                            float ly = t.InverseTransformPoint(hit.point).y;
                            if (ly > best) best = ly;
                        }
                    }
                if (!float.IsNegativeInfinity(best)) { hv[j * w + i] = best; has[j * w + i] = true; hits++; }
            }
        // 地面のないマスを、となりの地面のマスで埋める（いちばん高い所）
        var hv2 = (float[])hv.Clone(); var has2 = (bool[])has.Clone();
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                if (has[j * w + i]) continue;
                float best = float.NegativeInfinity;
                for (int dj = -1; dj <= 1; dj++) for (int di = -1; di <= 1; di++)
                    {
                        int x = i + di, y = j + dj; if (x < 0 || y < 0 || x >= w || y >= h || !has[y * w + x]) continue;
                        best = Mathf.Max(best, hv[y * w + x]);
                    }
                if (!float.IsNegativeInfinity(best)) { hv2[j * w + i] = best; has2[j * w + i] = true; }
            }
        var px = new Color32[w * h];
        for (int k = 0; k < px.Length; k++)
        {
            if (!has2[k]) { px[k] = new Color32(0, 0, 0, 0); continue; }
            int q = Mathf.Clamp(Mathf.RoundToInt((hv2[k] + 0.5f) * 65535f), 0, 65535);
            px[k] = new Color32((byte)(q >> 8), (byte)(q & 255), 0, 255);
        }
        var mat = d.sharedMaterial;
        string tp = HDir + "/" + mat.name + "_H.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
        if (tex && (tex.width != w || tex.height != h || tex.format != TextureFormat.RGBA32)) { tex.Reinitialize(w, h, TextureFormat.RGBA32, false); }
        if (!tex) { tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true); AssetDatabase.CreateAsset(tex, tp); }
        tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px); tex.Apply(false, false); EditorUtility.SetDirty(tex);
        AssetDatabase.SaveAssetIfDirty(tex);
        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);   // 作ったばかりの物を直接入れたら材質の参照が空になっていた（2026-11-15）→ 保存してから読み直して入れる
        mat.SetTexture("_HeightMap", tex); mat.SetFloat("_UseHeight", hits > 0 ? 1f : 0f); EditorUtility.SetDirty(mat);
        return mat.name.Replace("WindDecal_", "") + " " + w + "x" + h + " " + (100 * hits / (w * h)) + "%";
    }
}
