using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// いわのみち：頂上の柱ゾーン手前の崩落（谷）に、手前の道から先へ突き出た足場（2026-10-13 てじゃ「黄色い道をこの赤枠の分伸ばして」）。
// 手前の道の切り口から、谷の向こうの崖のふもとの手前まで、道の左寄り（幅1.5m）だけ伸ばす。先はギザギザに崩れた形。
// 道と同じ黄色いしましま（Course2_PathA/B、0.75m ごと）、横と裏は岩。当たり判定は道と同じ Environment レイヤー。
// 場所は崩落の切り抜き（FuwaCourse2Collapse）と同じ決まり（当時のワールド座標を今のコースの位置へ変換）で決める。何度実行してもよい
public static class FuwaCourse2Ledge
{
    // 崩落の切り抜きの基準（FuwaCourse2Collapse と同じ）
    static readonly Vector3 OldRootPos = new Vector3(44.312717f, 1.5f, 16.566227f);
    static readonly Quaternion OldRootRot = new Quaternion(0f, 0.6234898f, 0f, 0.78183156f);
    static readonly Vector3 OldOrigin = new Vector3(100f, 0f, 0.95f);
    static readonly Vector3 OldAxis = new Vector3(1f, 0f, 0.08f);

    // 足場の範囲（崩落の切り抜きの軸に沿った距離 s、横 l。左が -）。手前の道の切り口（s≒0）の少し下から
    const float S0 = -0.45f, S1 = 1.6f, L0 = -1.2f, L1 = 0.3f;
    const float Thick = 0.3f;
    const float StripeStart = -0.5f, StripeLen = 0.75f;   // しましまの区切り（手前の道に合わせた）
    const string MeshPath = "Assets/FuwaCourse/Meshes/Course2_CollapseLedge.asset";

    [MenuItem("FuwaCourse/いわのみち：崩落の谷の足場を作り直す")]
    public static void Menu()
    {
        Debug.Log(Run());
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    public static string Run()
    {
        var root = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaCourse2" && t.parent == null);
        var map = root.localToWorldMatrix * Matrix4x4.TRS(OldRootPos, OldRootRot, Vector3.one).inverse;
        Vector3 origin = map.MultiplyPoint3x4(OldOrigin);
        Vector3 axis = map.MultiplyVector(OldAxis.normalized); axis.y = 0f; axis.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;   // +l
        // 上面の高さ：手前の道の切り口のすぐ手前の高さ（足場はまっすぐ平らに）
        var path = root.Find("PathMesh");
        Physics.SyncTransforms();
        float topY = origin.y;
        var col = path.GetComponent<MeshCollider>();
        Vector3 probe = origin + axis * (S0 - 0.3f) + side * ((L0 + L1) * 0.5f) + Vector3.up * 50f;
        if (col.Raycast(new Ray(probe, Vector3.down), out var hit, 100f)) topY = hit.point.y;
        // 見た目の道（面取りしたメッシュ）の上面の高さに合わせる（当たり判定のメッシュとは少し違う）。重なる所が同じ高さでチラつかないよう 1cm 下げる
        {
            var pm = path.GetComponent<MeshFilter>().sharedMesh; var l2w = path.localToWorldMatrix; float best = float.MinValue;
            foreach (var v in pm.vertices)
            {
                var wv = l2w.MultiplyPoint3x4(v); var d = wv - origin; float ss = Vector3.Dot(d, axis), ll = Vector3.Dot(d, side);
                if (ss < -0.9f || ss > -0.3f || ll < L0 || ll > L1 || Mathf.Abs(wv.y - topY) > 0.5f) continue;
                best = Mathf.Max(best, wv.y);
            }
            if (best > float.MinValue) topY = best;
            topY -= 0.01f;
        }

        // 先のギザギザ（決まった乱数）
        var rnd = new System.Random(131);
        const int nEdge = 7;
        var tip = new float[nEdge + 1];
        for (int i = 0; i <= nEdge; i++) tip[i] = S1 - (float)rnd.NextDouble() * 0.35f - (i % 2 == 0 ? 0f : 0.12f);
        tip[0] = S1 - 0.45f; tip[nEdge] = S1 - 0.4f;   // 角は少し欠けた形

        // 上面を s 方向にしましまで区切ったポリゴンを作る（各しましま区間ごとに、左右のふちと先のギザギザで囲む）
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
        var subA = new List<int>(); var subB = new List<int>(); var subRock = new List<int>();
        Vector3 W(float s, float l, float y) { return origin + axis * s + side * l + Vector3.up * (y - origin.y); }
        float TipAt(float l) { float f = Mathf.InverseLerp(L0, L1, l) * nEdge; int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, nEdge - 1); return Mathf.Lerp(tip[i], tip[i + 1], f - i); }

        // 横の分割（ギザギザの点に合わせる）
        var ls = new List<float>(); for (int i = 0; i <= nEdge; i++) ls.Add(Mathf.Lerp(L0, L1, i / (float)nEdge));
        // 上面：横の各帯（l[i]..l[i+1]）を、s の区切り（しましま）で縦に分ける
        for (int i = 0; i < nEdge; i++)
        {
            float la = ls[i], lb = ls[i + 1];
            float ta = tip[i], tb = tip[i + 1];
            float sEnd = Mathf.Max(ta, tb);
            float s = S0;
            while (s < sEnd - 0.001f)
            {
                int k = Mathf.FloorToInt((s - StripeStart) / StripeLen + 0.0001f);
                float next = Mathf.Min(StripeStart + (k + 1) * StripeLen, sEnd);
                float sa0 = Mathf.Min(s, ta), sa1 = Mathf.Min(next, ta);
                float sb0 = Mathf.Min(s, tb), sb1 = Mathf.Min(next, tb);
                if (sa1 - sa0 > 0.0005f || sb1 - sb0 > 0.0005f)
                {
                    int b = verts.Count;
                    foreach (var p in new[] { W(sa0, la, topY), W(sb0, lb, topY), W(sb1, lb, topY), W(sa1, la, topY) }) { verts.Add(root.InverseTransformPoint(p)); norms.Add(Vector3.up); uvs.Add(new Vector2(p.x, p.z)); }
                    var list = (k % 2 == 0) ? subA : subB;
                    list.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                }
                s = next;
            }
        }
        // 横と先の面（岩）：上面のふちを下へ Thick 分
        void Wall(Vector3 a, Vector3 c)
        {
            int b = verts.Count;
            Vector3 n = Vector3.Cross(c - a, Vector3.down).normalized;
            foreach (var p in new[] { a, c, c + Vector3.down * Thick, a + Vector3.down * Thick }) { verts.Add(root.InverseTransformPoint(p)); norms.Add(root.InverseTransformDirection(n)); uvs.Add(new Vector2(p.x + p.z, p.y)); }
            subRock.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            subRock.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });   // 両面（向きを気にしない）
        }
        Wall(W(S0, L0, topY), W(TipAt(L0), L0, topY));
        Wall(W(TipAt(L1), L1, topY), W(S0, L1, topY));
        for (int i = 0; i < nEdge; i++) Wall(W(tip[i], ls[i], topY), W(tip[i + 1], ls[i + 1], topY));
        // 裏
        {
            int b = verts.Count;
            foreach (var p in new[] { W(S0, L0, topY - Thick), W(S0, L1, topY - Thick), W(S1, L1, topY - Thick), W(S1, L0, topY - Thick) }) { verts.Add(root.InverseTransformPoint(p)); norms.Add(Vector3.down); uvs.Add(new Vector2(p.x, p.z)); }
            subRock.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        // 上面の高さを少しだけ上げ下げしない（手前の道とぴったり）。ただし重なり（Zファイト）を避けるため、手前の道の下に潜る分は 2mm 下げる
        var m = new Mesh { name = "Course2_CollapseLedge" };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs);
        m.subMeshCount = 3; m.SetTriangles(subA, 0); m.SetTriangles(subB, 1); m.SetTriangles(subRock, 2);
        m.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (old != null) { old.Clear(); old.SetVertices(verts); old.SetNormals(norms); old.SetUVs(0, uvs); old.subMeshCount = 3; old.SetTriangles(subA, 0); old.SetTriangles(subB, 1); old.SetTriangles(subRock, 2); old.RecalculateBounds(); EditorUtility.SetDirty(old); m = old; }
        else AssetDatabase.CreateAsset(m, MeshPath);
        AssetDatabase.SaveAssets();

        var goT = root.Find("CollapseLedge"); var go = goT != null ? goT.gameObject : null;
        if (go == null) { go = new GameObject("CollapseLedge"); go.transform.SetParent(root, false); }
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        go.layer = path.gameObject.layer;
        var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = m;
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = path.GetComponent<MeshRenderer>().sharedMaterials;
        var mc = go.GetComponent<MeshCollider>(); if (mc == null) mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = m;
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(path.gameObject) & ~StaticEditorFlags.ContributeGI);
        return "ledge top=" + topY.ToString("F2") + " verts=" + verts.Count;
    }
}
