// 2026-11-26 クモの巣の軽量化（てじゃ「PICO4でクモの巣を至近距離で見るとやけにFPSが落ちる」）
// FuwaCourse/SpiderWeb シェーダーは1ピクセルごとに「近くの縦糸4本の向きと先端までの長さ」を計算していた。
// 特に枠が多角形の巣は、先端の長さを出すのに枠の辺を全部調べるので重い。
// 縦糸ごとの値は巣ごとに決まっていて動かない（穴の大きさにもよらない）ので、ここで表（横 = 縦糸の本数、縦 1 の小さいテクスチャ）にして渡す。
//  1ピクセル目 k = 縦糸 k の (cos, sin, 先端までの長さ, 0)。式はシェーダーの spokeAngle / outerRadius と同じ。
// SpiderWebBuild.Build でも自動で作る。表がない（_SpokeTexOn = 0）マテリアルは今まで通りシェーダーで計算する。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SpiderWebSpokeTex
{
    [MenuItem("FuwaCourse/済み（押す前に相談）/Spider Web/縦糸の表を作る（軽量化）")]
    static void BakeAllMenu() { Debug.Log(BakeAll()); }

    public static string BakeAll()
    {
        var sh = Shader.Find("FuwaCourse/SpiderWeb");
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader != sh) continue;
            Bake(m); n++;
        }
        AssetDatabase.SaveAssets();
        return "クモの巣の縦糸の表: " + n + " 個";
    }

    public static void Bake(Material m)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(m.GetFloat("_Spokes")));
        var px = new Color[n];
        for (int k = 0; k < n; k++)
        {
            float a = SpokeAngle(m, k, n);
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            px[k] = new Color(d.x, d.y, OuterRadius(m, k, d), 0);
        }

        string matPath = AssetDatabase.GetAssetPath(m);
        string path = System.IO.Path.GetDirectoryName(matPath).Replace('\\', '/') + "/" + System.IO.Path.GetFileNameWithoutExtension(matPath) + "_Spokes.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null || tex.format != TextureFormat.RGBAFloat)
        {
            if (tex != null) AssetDatabase.DeleteAsset(path);
            tex = new Texture2D(n, 1, TextureFormat.RGBAFloat, false, true);
            Setup(tex, px);
            AssetDatabase.CreateAsset(tex, path);
        }
        else
        {
            if (tex.width != n) tex.Reinitialize(n, 1);
            Setup(tex, px);
            EditorUtility.SetDirty(tex);
        }
        m.SetTexture("_SpokeTex", tex);
        m.SetFloat("_SpokeTexOn", 1);
        EditorUtility.SetDirty(m);
    }

    static void Setup(Texture2D tex, Color[] px)
    {
        tex.name = "Spokes";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.anisoLevel = 0;
        tex.SetPixels(px);
        tex.Apply(false, false);
    }

    // ───── 以下、FuwaSpiderWeb.shader と同じ式 ─────

    internal static float SpokeAngle(Material m, int k, int n)
    {
        float seed = m.GetFloat("_Seed");
        float j = Mathf.Sin(k * 1.93f + seed * 0.77f) * 0.18f;
        return (k + j) * 6.2831853f / n;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    internal static float OuterRadius(Material m, int k, Vector2 d)
    {
        int fn = Mathf.RoundToInt(m.GetFloat("_FrameN"));
        if (m.GetFloat("_FrameN") > 2.5f)
        {
            var fc = new List<Vector2>();
            Vector4[] fv = { m.GetVector("_F01"), m.GetVector("_F23"), m.GetVector("_F45"), m.GetVector("_F67") };
            for (int i = 0; i < 8; i++) fc.Add(i % 2 == 0 ? new Vector2(fv[i / 2].x, fv[i / 2].y) : new Vector2(fv[i / 2].z, fv[i / 2].w));
            float best = 1e4f;
            for (int i = 0; i < 8 && i < fn; i++)
            {
                Vector2 A = fc[i], B = fc[i + 1 < fn ? i + 1 : 0];
                Vector2 e = B - A;
                float den = Cross(d, e);
                if (Mathf.Abs(den) > 1e-6f)
                {
                    float t = Cross(A, e) / den;
                    float u = Cross(A, d) / den;
                    if (t > 0 && u >= -1e-4f && u <= 1 + 1e-4f) best = Mathf.Min(best, t);
                }
            }
            return best;
        }
        float seed = m.GetFloat("_Seed");
        Vector2 hs = (Vector2)m.GetVector("_Size") * 0.5f;
        Vector2 c = m.GetVector("_HoleCenter");
        float tx = Mathf.Abs(d.x) > 1e-4f ? ((d.x > 0 ? hs.x : -hs.x) - c.x) / d.x : 1e4f;
        float ty = Mathf.Abs(d.y) > 1e-4f ? ((d.y > 0 ? hs.y : -hs.y) - c.y) / d.y : 1e4f;
        float w = 0.65f * Mathf.Sin(k * 0.7f + seed * 0.9f) + 0.35f * Mathf.Sin(k * 2.399f + seed * 1.3f);
        float f = Mathf.Lerp(m.GetFloat("_OuterMin"), m.GetFloat("_OuterMax"), 0.5f + 0.5f * w);
        Vector2 he = hs + new Vector2(Mathf.Abs(c.x), Mathf.Abs(c.y));
        float te = 1f / Mathf.Sqrt((d.x * d.x) / (he.x * he.x) + (d.y * d.y) / (he.y * he.y));
        return Mathf.Min(Mathf.Min(tx, ty), te * m.GetFloat("_Round")) * f;
    }
}
