using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 2026-11-17 てじゃ「よくかんがえたら洞窟に影ないの違和感あるな」「偽物でもいいからなんかつけるか」
//  いわのみち（FuwaCourse2）の洞窟（Rocks2/Tunnel）の中の道に、うそ影（FuwaCourse/BlobDecal の細長い楕円）を敷く。
//  洞窟の下の床の点（上に洞窟がある所）を集めて、その広がりの向き・長さに合わせた楕円。両はしは入口に向かって自然に薄くなる
public static class FuwaFakeCaveShadow
{
    public static float Alpha = 0.6f, FalloffStart = 0.82f, Power = 1f, Margin = 2.6f, WidthExtra = 2.5f;

    [MenuItem("FuwaCourse/ステージ1/洞窟のうそ影を付け直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var root = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "FuwaCourse2" && t.parent == null); if (!root) return "FuwaCourse2 がない";
        var tun = root.Find("Rocks2/Tunnel"); if (!tun) return "洞窟がない";
        var old = tun.parent.Find("TunnelShadow"); if (old) Object.DestroyImmediate(old.gameObject);
        bool was = root.gameObject.activeSelf; root.gameObject.SetActive(true); Physics.SyncTransforms();
        var b = tun.GetComponent<Renderer>().bounds;
        var pts = new List<Vector3>();
        for (float x = b.min.x; x <= b.max.x; x += 0.4f)
            for (float z = b.min.z; z <= b.max.z; z += 0.4f)
            {
                var hits = Physics.RaycastAll(new Vector3(x, b.max.y + 2f, z), Vector3.down, b.size.y + 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
                foreach (var h in hits)
                {
                    if (h.collider.transform == tun || h.collider.transform.IsChildOf(tun)) continue;
                    if (h.normal.y < 0.7f) break;
                    if (Physics.RaycastAll(h.point + Vector3.up * 0.2f, Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore).Any(u => u.collider.transform == tun || u.collider.transform.IsChildOf(tun))) pts.Add(h.point);
                    break;
                }
            }
        root.gameObject.SetActive(was);
        if (pts.Count < 4) return "洞窟の下の床が見つからない";
        // 水平の広がりの向き（主成分）
        var c = new Vector3(pts.Average(p => p.x), pts.Average(p => p.y), pts.Average(p => p.z));
        float sxx = 0, szz = 0, sxz = 0; foreach (var p in pts) { float dx = p.x - c.x, dz = p.z - c.z; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
        float ang = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
        var ax = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)); var side = new Vector3(-ax.z, 0f, ax.x);
        float a0 = pts.Min(p => Vector3.Dot(p - c, ax)), a1 = pts.Max(p => Vector3.Dot(p - c, ax));
        float s0 = pts.Min(p => Vector3.Dot(p - c, side)), s1 = pts.Max(p => Vector3.Dot(p - c, side));
        float y0 = pts.Min(p => p.y), y1 = pts.Max(p => p.y);
        var center = c + ax * ((a0 + a1) / 2f) + side * ((s0 + s1) / 2f); center.y = (y0 + y1) / 2f;
        var mat = Mat();
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "TunnelShadow"; Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(tun.parent, true);
        g.transform.position = center; g.transform.rotation = Quaternion.LookRotation(side, Vector3.up);   // local x＝洞窟の向き、z＝横
        g.transform.localScale = Vector3.one;
        var ls = new Vector3((a1 - a0) + Margin, (y1 - y0) + 2.5f, (s1 - s0) + WidthExtra);   // 横は丸いふちで道のはしまで薄くならないよう広めに
        g.transform.localScale = new Vector3(ls.x / g.transform.lossyScale.x, ls.y / g.transform.lossyScale.y, ls.z / g.transform.lossyScale.z);
        // rotation：LookRotation(side) で forward＝side、right＝ax（x が洞窟の向き）
        var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(g, 0);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(g.scene);
        return $"[洞窟のうそ影] 床 {pts.Count} 点、長さ {ls.x:F1} 幅 {ls.z:F1}";
    }

    static Material Mat()
    {
        string path = "Assets/FuwaCourse/Materials/TunnelShadowDecal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("FuwaCourse/BlobDecal")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", new Color(0.12f, 0.08f, 0.2f, 1f)); m.SetFloat("_Alpha", Alpha); m.SetFloat("_FalloffStart", FalloffStart); m.SetFloat("_Power", Power);
        EditorUtility.SetDirty(m); return m;
    }
}
