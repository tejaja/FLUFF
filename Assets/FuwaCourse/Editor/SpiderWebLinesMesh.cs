// 2026-11-26 クモの巣のメッシュ版（てじゃ「PICO4でクモの巣を至近距離で見るとFPSが落ちる」→ B：本物の糸のメッシュ）
// FuwaCourse/SpiderWeb（板1枚＋1ピクセルごとの計算）の巣を、同じ形の「糸1本ずつの帯」のメッシュにする。
// 形の式は FuwaSpiderWeb.shader と同じ（縦糸の角度・先端・輪の位置・たるみ・夜露の場所）。
// シェーダーは FuwaCourse/SpiderWebLines（プロパティ名が同じなので、マテリアルのシェーダーを差しかえるだけ）。
// メッシュの頂点の中身は FuwaSpiderWebLines.shader の先頭の説明を参照。
// 板版に戻す時は「元の板にもどす」（シェーダーを SpiderWeb、メッシュを Quad に戻す）。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SpiderWebLinesMesh
{
    const string LinesShader = "FuwaCourse/SpiderWebLines";
    const string BoardShader = "FuwaCourse/SpiderWeb";

    [MenuItem("FuwaCourse/済み（押す前に相談）/Spider Web/巣を糸のメッシュにする（軽量化B）")]
    static void ToLinesMenu() { Debug.Log(ConvertAll(true)); }
    [MenuItem("FuwaCourse/済み（押す前に相談）/Spider Web/巣を元の板にもどす")]
    static void ToBoardMenu() { Debug.Log(ConvertAll(false)); }

    // シーンの巣（SpiderWeb / SpiderWebLines のマテリアルを持つ描画物）を全部切りかえる
    public static string ConvertAll(bool lines)
    {
        var board = Shader.Find(BoardShader); var ls = Shader.Find(LinesShader);
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mats = new HashSet<Material>();
        int nr = 0, skipped = 0;
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.gameObject.scene.IsValid()) continue;
            var m = r.sharedMaterial;
            if (m == null || (m.shader != board && m.shader != ls)) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null) continue;
            if (lines && m.GetFloat("_TearW") > 0.001f) { skipped++; continue; }   // 裂け目のある巣は板のまま
            Undo.RecordObject(mf, "Spider Web Lines");
            if (lines) mf.sharedMesh = BuildMesh(m);
            else mf.sharedMesh = quad;
            mats.Add(m); nr++;
        }
        foreach (var m in mats)
        {
            Undo.RecordObject(m, "Spider Web Lines");
            int q = m.renderQueue;
            m.shader = lines ? ls : board;
            m.renderQueue = q;
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        return (lines ? "糸のメッシュにした: " : "板にもどした: ") + nr + " 個（マテリアル " + mats.Count + "）" + (skipped > 0 ? " 裂け目で板のまま " + skipped : "");
    }

    // ───── GPU と同じ式（float で計算） ─────
    static float Frac(float x) => x - Mathf.Floor(x);
    static float Hash(float x, float y, float seed)
    {
        float px = x + seed, py = y + seed;
        float d = px * 127.1f + py * 311.7f;
        return Frac((float)System.Math.Sin(d) * 43758.5453f);
    }
    static float SpokeIndex(float i, float n) { float a = i % n; if (a < 0) a += n; return a % n; }

    class Data
    {
        public List<Vector3> pos = new List<Vector3>();
        public List<Vector4> uv0 = new List<Vector4>(), uv1 = new List<Vector4>(), uv2 = new List<Vector4>();
        public List<int> tri = new List<int>();
        public void V(Vector2 p, Vector4 a, Vector4 b, Vector4 c, Vector2 size, Vector2 hc)
        {
            pos.Add(new Vector3((p.x + hc.x) / size.x, (p.y + hc.y) / size.y, 0));
            uv0.Add(a); uv1.Add(b); uv2.Add(c);
        }
        // 2本の列（横 -1 と +1）の帯
        public void Strip(int start, int count)
        {
            for (int k = 0; k + 1 < count; k++)
            {
                int a = start + k * 2, b = a + 1, c = a + 2, d = a + 3;
                tri.Add(a); tri.Add(c); tri.Add(b);
                tri.Add(b); tri.Add(c); tri.Add(d);
            }
        }
    }

    public static Mesh BuildMesh(Material m)
    {
        int n = Mathf.Max(3, Mathf.RoundToInt(m.GetFloat("_Spokes")));
        float nf = n;
        float seed = m.GetFloat("_Seed");
        float hr = m.GetFloat("_HoleRadius");
        float hm = Mathf.Max(m.GetFloat("_HoleRadiusMax"), hr);
        float a = m.GetFloat("_RingStart"), b = m.GetFloat("_RingGrow");
        float spRef = m.GetFloat("_SpacingRef");
        bool spiral = m.GetFloat("_Spiral") > 0.5f;
        float gapChance = m.GetFloat("_GapChance"), dewChance = m.GetFloat("_DewChance");
        float splitChance = m.GetFloat("_SplitChance"), splitStart = m.GetFloat("_SplitStart");
        float sag = m.GetFloat("_Sag");
        Vector2 size = m.GetVector("_Size"), hc = m.GetVector("_HoleCenter");
        bool frame = m.GetFloat("_FrameN") > 2.5f;
        int fn = Mathf.RoundToInt(m.GetFloat("_FrameN"));

        var dir = new Vector2[n]; var R = new float[n];
        for (int k = 0; k < n; k++)
        {
            float ang = SpiderWebSpokeTex.SpokeAngle(m, k, n);
            dir[k] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            R[k] = SpiderWebSpokeTex.OuterRadius(m, k, dir[k]);
        }
        float Spacing(float r) => spRef > 0 ? Mathf.Clamp((r - hm) / spRef, 0.3f, 1f) : 1f;
        void RingParts(float k, float i, float sc, out float s, out float w)
        {
            float kc = k + (spiral ? i / nf : 0);
            s = (a / b) * (Mathf.Pow(1 + b, kc) - 1) * sc;
            w = Mathf.Lerp(0.95f, 1.05f, Hash(SpokeIndex(i, nf), Mathf.Floor(kc + 1e-3f), seed));
        }

        var D = new Data();
        var z4 = Vector4.zero;

        // 外枠の角（多角形の枠なら枠の角、そうでなければ縦糸の先端を結んだ形）
        var corners = new List<Vector2>();
        if (frame)
        {
            Vector4[] fv = { m.GetVector("_F01"), m.GetVector("_F23"), m.GetVector("_F45"), m.GetVector("_F67") };
            for (int i = 0; i < fn && i < 8; i++) corners.Add(i % 2 == 0 ? new Vector2(fv[i / 2].x, fv[i / 2].y) : new Vector2(fv[i / 2].z, fv[i / 2].w));
        }
        else
            for (int k = 0; k < n; k++) corners.Add(dir[k] * R[k]);
        // 縦糸の先端を切る向き＝当たる外枠の糸の向き
        Vector2 CutDir(int k)
        {
            if (!frame)
            {
                Vector2 e = corners[(k + 1) % n] - corners[(k + n - 1) % n];
                return e.normalized;
            }
            float best = 1e9f; Vector2 bd = Vector2.zero;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector2 A = corners[i], B = corners[(i + 1) % corners.Count], e = B - A;
                float den = dir[k].x * e.y - dir[k].y * e.x;
                if (Mathf.Abs(den) < 1e-6f) continue;
                float t = (A.x * e.y - A.y * e.x) / den;
                float u = (A.x * dir[k].y - A.y * dir[k].x) / den;
                if (t > 0 && u >= -1e-4f && u <= 1 + 1e-4f && t < best) { best = t; bd = e.normalized; }
            }
            return bd;
        }

        // 縦糸
        for (int k = 0; k < n; k++)
        {
            float f = -1;
            if (splitChance > 0 && Hash(k + 0.75f, 3.3f, seed) < splitChance)
                f = splitStart * Mathf.Lerp(0.7f, 1.2f, Hash(k, 7.7f, seed));
            int s0 = D.pos.Count;
            Vector2 cut = CutDir(k);
            for (int e = 0; e < 2; e++)
                for (int sd = -1; sd <= 1; sd += 2)
                    D.V(dir[k] * (e == 0 ? hr : R[k]), new Vector4(0, sd, e, 0), new Vector4(dir[k].x, dir[k].y, 0, 0), new Vector4(R[k], f, cut.x, cut.y), size, hc);
            D.Strip(s0, 2);
        }

        // 横糸（輪）と夜露
        var dews = new Dictionary<long, (Vector2 d, float s, float w, float h)>();
        int rings = 0;
        for (int s0 = 0; s0 < n; s0++)
        {
            int s1 = (s0 + 1) % n;
            Vector2 d0 = dir[s0], d1 = dir[s1];
            float R0 = R[s0], R1 = R[s1];
            float sc0 = Spacing(R0), sc1 = Spacing(R1);
            for (int k = 1; k < 400; k++)
            {
                RingParts(k, s0, sc0, out float sA, out float wA);
                RingParts(k, s0 + 1, sc1, out float sB, out float wB);
                if ((hm + sA) * wA > R0 && (hm + sB) * wB > R1) break;
                float gap = a * Mathf.Pow(1 + b, k) * Mathf.Min(sc0, sc1);
                float room = Mathf.Min(R0 - (hm + sA) * wA, R1 - (hm + sB) * wB);
                if (!(room > gap * 0.6f)) continue;
                if (gapChance > 0)
                {
                    float kc0 = k + (spiral ? s0 / nf : 0);
                    if (Hash(SpokeIndex(s0, nf) + 0.25f, Mathf.Floor(kc0 + 1e-3f) * 1.7f, seed) < gapChance) continue;
                }
                Vector2 A = d0 * (hr + sA) * wA, B = d1 * (hr + sB) * wB;
                float L = (B - A).magnitude;
                int seg = Mathf.Clamp(Mathf.CeilToInt(L / 0.06f), 3, 14);
                Vector2 outN = (A + B).normalized;
                int st = D.pos.Count;
                for (int j = 0; j <= seg; j++)
                {
                    float t = j / (float)seg;
                    Vector2 c = A + (B - A) * t - outN * (sag * L * 4 * t * (1 - t));
                    for (int sd = -1; sd <= 1; sd += 2)
                        D.V(c, new Vector4(1, sd, t, 0), new Vector4(d0.x, d0.y, d1.x, d1.y), new Vector4(sA, wA, sB, wB), size, hc);
                }
                D.Strip(st, seg + 1);
                rings++;
                if (dewChance > 0)
                {
                    float h0 = Hash(SpokeIndex(s0, nf) + 0.5f, k, seed), h1 = Hash(SpokeIndex(s0 + 1, nf) + 0.5f, k, seed);
                    if (h0 < dewChance) AddDew(dews, d0, sA, wA, h0, hr);
                    if (h1 < dewChance) AddDew(dews, d1, sB, wB, h1, hr);
                }
            }
        }

        // 外枠の糸
        for (int i = 0; i < corners.Count; i++)
        {
            int cn = corners.Count;
            Vector2 A = corners[i], B = corners[(i + 1) % cn];
            Vector2 tp = (A - corners[(i + cn - 1) % cn]).normalized, tn = (corners[(i + 2) % cn] - B).normalized;   // 前後の糸の向き
            int st = D.pos.Count;
            for (int e = 0; e < 2; e++)
                for (int sd = -1; sd <= 1; sd += 2)
                    D.V(e == 0 ? A : B, new Vector4(2, sd, e, 0), new Vector4(A.x, A.y, B.x, B.y), new Vector4(tp.x, tp.y, tn.x, tn.y), size, hc);
            D.Strip(st, 2);
        }

        // 穴のふち（半径は頂点で _HoleRadius から）
        {
            const int segs = 96;
            int st = D.pos.Count;
            for (int j = 0; j <= segs; j++)
            {
                float ang = j * Mathf.PI * 2 / segs;
                var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                for (int sd = -1; sd <= 1; sd += 2)
                    D.V(d * hr, new Vector4(3, sd, j / (float)segs, 0), new Vector4(d.x, d.y, 0, 0), z4, size, hc);
            }
            D.Strip(st, segs + 1);
        }

        // 夜露（四角：角 = (side, t*2-1)）
        foreach (var dw in dews.Values)
        {
            int st = D.pos.Count;
            Vector2 c = dw.d * (hr + dw.s) * dw.w;
            for (int e = 0; e < 2; e++)
                for (int sd = -1; sd <= 1; sd += 2)
                    D.V(c, new Vector4(4, sd, e, 0), new Vector4(dw.d.x, dw.d.y, 0, 0), new Vector4(dw.s, dw.w, dw.h, 0), size, hc);
            D.Strip(st, 2);
        }

        var mesh = new Mesh { name = m.name + "_Lines" };
        mesh.indexFormat = D.pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(D.pos);
        mesh.SetUVs(0, D.uv0); mesh.SetUVs(1, D.uv1); mesh.SetUVs(2, D.uv2);
        mesh.SetTriangles(D.tri, 0);
        // 形は頂点シェーダーで決まるので、大きさは板（Quad）と同じにしておく（穴が呼吸しても板の中）
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1.02f, 1.02f, 0.02f));

        string matPath = AssetDatabase.GetAssetPath(m);
        string path = System.IO.Path.GetDirectoryName(matPath).Replace('\\', '/') + "/" + System.IO.Path.GetFileNameWithoutExtension(matPath) + "_Lines.asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        ex.Clear();
        ex.indexFormat = mesh.indexFormat;
        ex.SetVertices(D.pos);
        ex.SetUVs(0, D.uv0); ex.SetUVs(1, D.uv1); ex.SetUVs(2, D.uv2);
        ex.SetTriangles(D.tri, 0);
        ex.bounds = mesh.bounds;
        EditorUtility.SetDirty(ex);
        Object.DestroyImmediate(mesh);
        return ex;
    }

    static void AddDew(Dictionary<long, (Vector2 d, float s, float w, float h)> dews, Vector2 d, float s, float w, float h, float hr)
    {
        Vector2 p = d * (hr + s) * w;
        long key = ((long)Mathf.RoundToInt(p.x * 1000) << 32) ^ (uint)Mathf.RoundToInt(p.y * 1000);
        if (!dews.ContainsKey(key)) dews[key] = (d, s, w, h);
    }
}
