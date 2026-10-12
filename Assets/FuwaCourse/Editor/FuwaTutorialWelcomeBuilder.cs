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
    const string WelcomeJa = "<size=130%><color=#FF6B5C>ようこそ！</color></size>\n看板を読みながら、道にそって進もう\n<size=85%>「<color=#FF6B5C>▼</color>」がついている看板を読んでね\nエアガンを忘れずに持っていこう！</size>";
    const string WelcomeEn = "<size=130%><color=#FF6B5C>Welcome!</color></size>\nRead the signs as you walk along the path\n<size=85%>Look for signs with a \"<color=#FF6B5C>▼</color>\"\nDon't forget to grab the air gun!</size>";

    // ▼の大きさ（スタートの▼は 0.29×0.35。看板の上は少し離れて見るので大きめ）
    const float MarkW = 0.42f, MarkH = 0.5f;
    const string ArrowMatPath = "Assets/FuwaCourse/Materials/ReadMarkArrow.mat";
    static Material ArrowMat()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(ArrowMatPath);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/StartArrow.mat")); AssetDatabase.CreateAsset(m, ArrowMatPath); }
        ColorUtility.TryParseHtmlString("#F2302A", out var red);
        m.SetColor("_Color", red); EditorUtility.SetDirty(m);
        return m;
    }

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
        // 2026-10-12 てじゃ「ようこそ→エアガンの順に」：前のエアガンの看板の場所（てじゃが手で合わせた右前・右へ50°）へ。
        // エアガンの看板と台は道に入ったとこの左へ（FuwaPracticeTutorial.GunSignLocal）。
        // 読む距離（3.2m）より少しだけ遠くして、出た瞬間に「読んだ」になって▼が消えないように
        w.position = spot.TransformPoint(new Vector3(1.95f, 0f, 2.9f));
        RaycastHit gh;
        if (Physics.Raycast(w.position + Vector3.up * 3f, Vector3.down, out gh, 6f)) w.position = new Vector3(w.position.x, gh.point.y - 0.02f, w.position.z);
        w.rotation = Quaternion.Euler(0f, spot.eulerAngles.y + 50f, 0f);
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
        // 道の順（ようこそ → 各練習場 → 最後の2枚）。エアガンの看板はスポーンのすぐ横なので付けない
        var signs = new List<Transform> { w };
        foreach (Transform bay in tut) if (bay.name.StartsWith("Bay_")) { var s = bay.Find("Sign"); if (s != null) signs.Add(s); }
        var tail = new List<Transform>();
        foreach (var n in new[] { "Sign_ShootMushroom", "Sign_MoreTricks" }) { var s = tut.Find(n); if (s != null) tail.Add(s); }
        tail.Sort((x, y) => Vector3.Distance(spot.position, x.position).CompareTo(Vector3.Distance(spot.position, y.position)));
        signs.AddRange(tail);

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
            // 看板の上の、道（スポーンの向き）側の右寄りにはみ出す（てじゃ「もっとコースにはみ出して右よりでも」）
            Vector3 toRoad = -s.forward; toRoad.y = 0f; toRoad.Normalize();   // 文字の読める側＝道の側
            Vector3 right = Vector3.Cross(Vector3.up, toRoad);                  // 道から看板を見て右
            m.position = new Vector3(b.center.x, b.max.y + 0.55f, b.center.z);   // 看板のまん中の上（てじゃ）
            // 目印はスタートの▼と同じ形（MarkerPyramid を逆さに）で、色だけ赤（2026-10-12 てじゃ「！より▽」「形スタートとかと合わせて、色だけ赤く」）
            // ピラミッドは底が原点で高さ1。逆さにすると下へのびるので、真ん中が m の位置に来るよう半分上げる（跳ねる時のつぶれの基準）
            var a = new GameObject("Arrow"); a.transform.SetParent(m, false);
            a.transform.localPosition = new Vector3(0f, MarkH * 0.5f, 0f);
            a.transform.localRotation = Quaternion.Euler(0f, 180f, 180f);
            a.transform.localScale = new Vector3(MarkW, MarkH, MarkW);
            a.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/MarkerPyramid.asset");
            var ar = a.AddComponent<MeshRenderer>(); ar.sharedMaterial = ArrowMat();
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
        rm.radius = MarkH * 0.5f;
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
