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
        // 道の上に少しはみ出して、スポーンの正面ちょい左・4.6m先（てじゃ「もっと右にはみ出して」）。スポーンの方を向く
        // （近すぎると出た瞬間に「読んだ」になって「！」が消えるので、読む距離より遠く）
        w.position = spot.position + spot.forward * 4.6f - spot.right * 1.4f;
        Vector3 face = spot.position - w.position; face.y = 0f;
        w.rotation = Quaternion.LookRotation(-face.normalized, Vector3.up);   // 文字の面（-z）がスポーンの方
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
            // 丸と「！」は角丸の板（RoundedPanel、角の丸み＝0.25×スケール）で作る：まん丸の赤い玉に、太い白い棒と点（文字だと細くて片寄った）
            ColorUtility.TryParseHtmlString("#F2302A", out var red);
            SpriteRenderer Blob(string nm, float w, float h, float y, float z, Color col)
            {
                var r = Object.Instantiate(srcPanel.gameObject, m).GetComponent<SpriteRenderer>();
                r.name = nm; r.gameObject.SetActive(true);
                foreach (var lg in r.GetComponents<LODGroup>()) Object.DestroyImmediate(lg);
                float sc = Mathf.Min(w, h) * 0.5f / 0.25f;   // 短い辺の半分が角の丸みになるスケール
                r.transform.localPosition = new Vector3(0f, y, z); r.transform.localRotation = Quaternion.identity;
                r.transform.localScale = Vector3.one * sc;
                r.drawMode = SpriteDrawMode.Sliced; r.size = new Vector2(w, h) / sc;
                r.color = col; r.sortingOrder = 0;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return r;
            }
            Blob("Circle", 0.8f, 0.8f, 0f, 0f, red);
            Blob("Bar", 0.15f, 0.36f, 0.07f, -0.004f, Color.white);
            Blob("Dot", 0.15f, 0.15f, -0.21f, -0.004f, Color.white);
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
