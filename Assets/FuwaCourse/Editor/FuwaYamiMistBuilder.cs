using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// やみのもりの「小さいもや」（2026-10-22 てじゃ「森全体を囲うっていうよりコース内に漂ってるってかんじ」）。
// 前の大きいもや（YamiMist：森をまるごと包むだ円体1つ）は描かないだけにして残す（FuwaSkyZone が空を暗くする範囲に使っているので消さない）。
// 代わりに同じシェーダー（FuwaCourse/DarkMist）の小さいだ円体を、コースの床（FuwaCourseLod の床の箱）からばらけるように選んだ所に置く。
// 軽い版（CourseLites/FuwaCourse3_Lite）にも同じ物を入れる。濃さなどは YamiMistSmall.mat で調整（作り直しても材質は上書きしない）。
// コースを直したら、軽い版を作り直したあとにこのメニューをもう一度押す（何度実行しても同じ結果）
public static class FuwaYamiMistBuilder
{
    const string MatPath = "Assets/FuwaCourse/Materials/YamiMistSmall.mat";
    public static int Count = 11;
    public static float MinSize = 7f, MaxSize = 12f;   // だ円体の半径(m)の目安（横はこの1.1〜1.6倍、高さは0.55倍）
    public static float Lift = 1f, LiftRandom = 4f;    // 床の上にどれだけ浮かせるか(m)
    public static float Jitter = 8f;                    // 選んだ床の所から横にずらす幅(m)
    const int Seed = 11;

    [MenuItem("FuwaCourse/よく使う/やみのもりの小さいもやを作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var course = GameObject.Find("FuwaCourse3"); if (!course) return "FuwaCourse3 がない";
        var big = course.transform.Find("YamiMist"); if (!big) return "FuwaCourse3/YamiMist がない";
        var lod = Object.FindObjectOfType<FuwaCourseLod>(true); if (!lod) return "FuwaCourseLod がない";
        int ci = System.Array.FindIndex(lod.fullRoots, r => r == course); if (ci < 0) return "FuwaCourseLod に FuwaCourse3 がない";
        var lite = lod.liteRoots[ci];

        // 材質：無ければ大きいもやの材質から作る（あれば調整した値をそのまま使う）
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(big.GetComponent<MeshRenderer>().sharedMaterial) { name = "YamiMistSmall", enableInstancing = true };
            mat.SetFloat("_Density", 0.035f); mat.SetFloat("_MaxAlpha", 0.4f);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        var mesh = big.GetComponent<MeshFilter>().sharedMesh;

        // 置く所：床の箱の上の真ん中から、前に選んだ所からいちばん遠い所を順に（スタートに近い所から始める）
        var pts = new List<Vector3>();
        for (int i = 0; i < lod.cellCourse.Length; i++)
            if (lod.cellCourse[i] == ci) pts.Add(new Vector3((lod.cellMin[i].x + lod.cellMax[i].x) / 2, lod.cellMax[i].y, (lod.cellMin[i].z + lod.cellMax[i].z) / 2));
        if (pts.Count == 0) return "FuwaCourse3 の床の箱がない（先に「コースの軽い版を作る」）";
        var start = lod.starts[ci] != null ? lod.starts[ci].position : pts[0];
        var chosen = new List<Vector3> { pts.OrderBy(p => (p - start).sqrMagnitude).First() };
        while (chosen.Count < Mathf.Min(Count, pts.Count))
            chosen.Add(pts.OrderByDescending(p => chosen.Min(c => (p - c).sqrMagnitude)).First());

        var rnd = new System.Random(Seed); float R() => (float)rnd.NextDouble();
        var holder = Rebuild(course.transform, "YamiMists");
        int k = 0;
        foreach (var c in chosen)
        {
            var g = new GameObject("Mist" + k++); g.transform.SetParent(holder, false);
            g.transform.position = c + new Vector3((R() - 0.5f) * Jitter, Lift + R() * LiftRandom, (R() - 0.5f) * Jitter);
            float s = MinSize + R() * (MaxSize - MinSize);
            g.transform.localScale = new Vector3(s * (1.1f + R() * 0.5f), s * 0.55f, s);
            g.transform.rotation = Quaternion.Euler(0, R() * 360, 0);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        // 大きいもやは描かない（形は FuwaSkyZone の範囲として残す）
        HideBig(big);
        string liteLog = "";
        if (lite != null)
        {
            var lh = Rebuild(lite.transform, "YamiMists");
            foreach (Transform t in holder) { var cp = Object.Instantiate(t.gameObject, lh); cp.name = t.name; cp.transform.SetPositionAndRotation(t.position, t.rotation); cp.transform.localScale = t.localScale; }
            var lb = lite.transform.Find("YamiMist"); if (lb) HideBig(lb);
            liteLog = "、軽い版にも";
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(course.scene);
        return "[やみのもりの小さいもや] " + chosen.Count + " 個" + liteLog;
    }

    static Transform Rebuild(Transform parent, string name)
    {
        var old = parent.Find(name); if (old) Undo.DestroyObjectImmediate(old.gameObject);
        var h = new GameObject(name); Undo.RegisterCreatedObjectUndo(h, "yami mists");
        h.transform.SetParent(parent, false); h.transform.localPosition = Vector3.zero; h.transform.localRotation = Quaternion.identity; h.transform.localScale = Vector3.one;
        // 静的バッチングに入るとオブジェクト座標が消えてもやが壊れるので Static にしない
        GameObjectUtility.SetStaticEditorFlags(h, 0);
        return h.transform;
    }

    static void HideBig(Transform big)
    {
        var r = big.GetComponent<MeshRenderer>(); if (!r || !r.enabled) return;
        Undo.RecordObject(r, "hide big mist"); r.enabled = false; EditorUtility.SetDirty(r);
    }
}
