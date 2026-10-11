using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// チュートリアルの入口の「ようこそ！」看板と、看板の上の「！」（FuwaReadMarks）を作る（2026-10-12 てじゃ「まずは説明を読みながら進むってのを解説するなにかほしい」→ A＋C）
// 何度実行しても同じ結果
public static class FuwaTutorialWelcomeBuilder
{
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaReadMarks.asset";
    const string WelcomeJa = "<size=130%><color=#FF6B5C>ようこそ！</color></size>\n看板を読みながら、道にそって進もう\n<size=85%>「<color=#FF6B5C>！</color>」がついている看板を読んでね\nとばしたい人は「説明をとばす」へ</size>";
    const string WelcomeEn = "<size=130%><color=#FF6B5C>Welcome!</color></size>\nRead the signs as you walk along the path\n<size=85%>Look for signs with a \"<color=#FF6B5C>!</color>\"\nTo skip, use \"Skip tutorial\"</size>";

    [MenuItem("FuwaCourse/チュートリアルの「ようこそ」と「！」を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var tut = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Tutorial" && t.parent != null && t.parent.name == "FuwaCourse0");
        var ent = tut.Find("Entrance");
        var spot = ent.Find("PlayerSpot");
        var gunSign = ent.Find("GunSign");

        // ---- A：ようこそ看板（エアガンの看板を、道の向きを軸に左右反転した位置へ）----
        var old = ent.Find("WelcomeSign"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var w = Object.Instantiate(gunSign.gameObject, ent).transform; w.name = "WelcomeSign";
        Vector3 lp = spot.InverseTransformPoint(gunSign.position); lp.x = -lp.x;
        w.position = spot.TransformPoint(lp);
        float yaw = 2f * spot.eulerAngles.y - gunSign.eulerAngles.y;
        w.rotation = Quaternion.Euler(0f, yaw, 0f);
        var wt = w.GetComponentInChildren<TMPro.TMP_Text>(true);
        wt.text = WelcomeJa;
        log.Add("welcome at " + w.position.ToString("F2"));

        // 言語の切り替えに登録（消えた古い看板の分は詰める）
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null)
        {
            var so = new SerializedObject(lang);
            var tx = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
            for (int i = tx.arraySize - 1; i >= 0; i--)
                if (tx.GetArrayElementAtIndex(i).objectReferenceValue == null)
                { tx.DeleteArrayElementAtIndex(i); if (i < pj.arraySize) pj.DeleteArrayElementAtIndex(i); if (i < pe.arraySize) pe.DeleteArrayElementAtIndex(i); }
            int n = tx.arraySize; tx.arraySize = n + 1; pj.arraySize = n + 1; pe.arraySize = n + 1;
            tx.GetArrayElementAtIndex(n).objectReferenceValue = wt;
            pj.GetArrayElementAtIndex(n).stringValue = WelcomeJa; pe.GetArrayElementAtIndex(n).stringValue = WelcomeEn;
            so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang);
        }

        // ---- C：看板の上の「！」----
        var signs = new List<Transform> { w, gunSign };
        foreach (Transform bay in tut) if (bay.name.StartsWith("Bay_")) { var s = bay.Find("Sign"); if (s != null) signs.Add(s); }
        foreach (var n in new[] { "Sign_ShootMushroom", "Sign_MoreTricks" }) { var s = tut.Find(n); if (s != null) signs.Add(s); }

        var holderOld = tut.Find("ReadMarks"); if (holderOld != null) Object.DestroyImmediate(holderOld.gameObject);
        var holder = new GameObject("ReadMarks").transform; holder.SetParent(tut, false);
        var srcPanel = gunSign.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.name == "Panel");
        var srcText = wt;
        var marks = new List<Transform>();
        foreach (var s in signs)
        {
            // 看板の木（SignModel）のてっぺんの上。なければ紙の板
            Renderer top = s.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "SignModel");
            if (top == null) top = s.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Panel");
            Bounds b = top != null ? top.bounds : new Bounds(s.position + Vector3.up * 1.5f, Vector3.one);
            var m = new GameObject("Mark_" + (s.parent != null && s.parent.name.StartsWith("Bay_") ? s.parent.name : s.name)).transform;
            m.SetParent(holder, false);
            m.position = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
            // 丸（角丸の板を正方形にして角の丸み＝半分）
            var c = Object.Instantiate(srcPanel.gameObject, m).GetComponent<SpriteRenderer>();
            c.name = "Circle"; c.gameObject.SetActive(true); c.transform.localPosition = Vector3.zero; c.transform.localRotation = Quaternion.identity;
            c.transform.localScale = Vector3.one; c.drawMode = SpriteDrawMode.Sliced; c.size = new Vector2(0.5f, 0.5f);
            ColorUtility.TryParseHtmlString("#FF8C6B", out var oc); c.color = oc;
            foreach (var lg in c.GetComponents<LODGroup>()) Object.DestroyImmediate(lg);
            var tgo = new GameObject("Text"); tgo.transform.SetParent(m, false); tgo.transform.localPosition = new Vector3(0f, 0.005f, -0.01f);
            var t = tgo.AddComponent<TMPro.TextMeshPro>();
            t.font = srcText.font; t.fontSharedMaterial = srcText.fontSharedMaterial; t.color = Color.white;
            t.fontSize = 3.2f; t.fontStyle = TMPro.FontStyles.Bold; t.alignment = TMPro.TextAlignmentOptions.Center;
            t.enableWordWrapping = false; t.rectTransform.sizeDelta = new Vector2(0.5f, 0.5f); t.text = "!";
            tgo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marks.Add(m);
        }

        // ---- 動かすスクリプト ----
        var prog = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
        if (prog == null)
        {
            prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaReadMarks.cs");
            AssetDatabase.CreateAsset(prog, ProgramPath);
            AssetDatabase.SaveAssets();
        }
        var rm = UdonSharpUndo.AddComponent<FuwaReadMarks>(holder.gameObject);
        rm.marks = marks.ToArray();
        rm.signs = signs.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(rm);
        // 同期しない
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(rm);
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        EditorUtility.SetDirty(rm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tut.gameObject.scene);
        log.Add("marks " + marks.Count);
        return string.Join("\n", log);
    }
}
