using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2のロビーの、コースごとの色（2026-10-19）。ステージ1と同じ作り：
// ・まん中からワープの台へ、そのコースの道と同じ色のシマシマ道（FuwaLobbyPaths.Strip）
// ・コース看板（雲）の後ろの色の雲をコースの色に（てじゃ「ふちは道同様濃いめの青で」）
// 色が決まったコースだけ（道の材質 Course2_N_PathA/B があるコース）。FuwaStage2Builder の作り直しからも呼ぶ
public static class FuwaStage2LobbyCourses
{
    // 看板の後ろの雲の色（null＝まだ決めてない、今のまま）
    // 2026-10-19 てじゃと決めたコースの色：水=青、火=赤、氷=銀、機械=グレー/黒、遺跡=テラコッタ、きのこのくに=かぜのみちより濃い緑
    public static readonly string[] SignRim = { "4A8FE8", "E2625B", "B4C2D2", "5C626E", "D0794F", "3FAE6A" };
    const string MeshDir = "Assets/FuwaStage2/Generated/Lobby";
    const float RingIn = 0.85f, StartR = 1.2f;

    [MenuItem("FuwaCourse/ステージ2/ロビーのコース色（看板・道）をつけ直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby")?.transform; if (!lobby) return "ステージ2のロビーがない";
        var log = new System.Text.StringBuilder();
        var center = lobby.Find("LobbySpawn").position;
        float floorY = lobby.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Generated", "Lobby");
        var holder = lobby.Find("CoursePaths");
        if (holder) Object.DestroyImmediate(holder.gameObject);
        holder = new GameObject("CoursePaths").transform; holder.SetParent(lobby, false);
        var lang = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(x => x && x.gameObject.scene.IsValid() && x.GetType().Name == "FuwaLanguage");
        var langSo = lang ? new SerializedObject(lang) : null;
        for (int i = 0; i < 6; i++)
        {
            var slot = lobby.Find("Slot" + i); if (!slot) continue;
            // 看板の文字（コース名）。日本語・英語の切り替えの配列もそろえる
            var lab = slot.Find("Label")?.GetComponent<TMPro.TMP_Text>();
            if (lab)
            {
                string ja = "<voffset=0.12em><size=60%>コース</size> 2–" + (i + 1) + "</voffset>\n<size=51%>" + FuwaStage2Builder.CourseJa[i] + "</size>";
                string en = "<voffset=0.12em><size=60%>Course</size> 2–" + (i + 1) + "</voffset>\n<size=51%>" + FuwaStage2Builder.CourseEn[i] + "</size>";
                lab.text = ja; EditorUtility.SetDirty(lab);
                if (langSo != null)
                {
                    var texts = langSo.FindProperty("texts"); var jaP = langSo.FindProperty("ja"); var enP = langSo.FindProperty("en");
                    for (int k = 0; k < texts.arraySize; k++)
                        if (texts.GetArrayElementAtIndex(k).objectReferenceValue == lab) { jaP.GetArrayElementAtIndex(k).stringValue = ja; enP.GetArrayElementAtIndex(k).stringValue = en; }
                }
            }
            // 看板の後ろの雲
            if (SignRim[i] != null && ColorUtility.TryParseHtmlString("#" + SignRim[i], out var c))
            {
                var rim = slot.Find("Label/SignDeco/CloudRim")?.GetComponent<Renderer>();
                if (rim)
                {
                    string p = "Assets/FuwaCourse/Generated/Signs/Sign_CloudRim_" + SignRim[i] + ".mat";
                    var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (!m) { m = new Material(rim.sharedMaterial); AssetDatabase.CreateAsset(m, p); }
                    m.SetColor("_Color", c); m.SetColor("_UnderColor", FuwaSignStyle.RimUnderColor(c)); EditorUtility.SetDirty(m);
                    rim.sharedMaterial = m; EditorUtility.SetDirty(rim);
                    log.Append("看板2–" + (i + 1) + " ");
                }
            }
            // ワープ：そのコースができていたら（ルート FuwaCourse2_N に Start/BallStart がある）、ステージ1のワープの台を写してつなぐ
            var course = GameObject.Find("FuwaCourse2_" + (i + 1));
            var cStart = course ? course.transform.Find("Start") : null;
            if (cStart && cStart.Find("BallStart"))
            {
                var src = GameObject.Find("FuwaLobby/Slot1/WarpToCourse1_Trigger"); var srcG = GameObject.Find("FuwaLobby/Slot1/WarpToCourse1_Trigger_Gauge");
                foreach (var n in new[] { "WarpToCourse" + i + "_Trigger", "WarpToCourse" + i + "_Trigger_Gauge" }) { var o = slot.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
                var trig = Object.Instantiate(src, slot, false); trig.name = "WarpToCourse" + i + "_Trigger";
                trig.transform.localPosition = src.transform.localPosition; trig.transform.localRotation = src.transform.localRotation; trig.transform.localScale = src.transform.localScale;
                var gauge = Object.Instantiate(srcG, slot, false); gauge.name = "WarpToCourse" + i + "_Trigger_Gauge";
                gauge.transform.localPosition = srcG.transform.localPosition; gauge.transform.localRotation = srcG.transform.localRotation; gauge.transform.localScale = srcG.transform.localScale;
                var ring = slot.Find("WarpToCourse" + i + "_ProgressRing");
                var w = trig.GetComponents<MonoBehaviour>().First(m => m.GetType().Name == "FuwaCourseWarp");
                var so = new SerializedObject(w);
                so.FindProperty("courseStart").objectReferenceValue = cStart.Find("BallStart");
                so.FindProperty("playerSpot").objectReferenceValue = cStart.Find("PlayerSpot");
                so.FindProperty("gunSpot").objectReferenceValue = null;   // コースの銃の台はまだない
                so.FindProperty("lobbyReturnSpot").objectReferenceValue = null;
                so.FindProperty("stage").intValue = 2;
                so.FindProperty("timed").boolValue = false;   // ゴールができるまではタイムなし
                so.FindProperty("progressRing").objectReferenceValue = ring ? ring.GetComponent<Renderer>() : null;
                so.FindProperty("progressVeil").objectReferenceValue = ring && ring.Find("WarpVeil") ? ring.Find("WarpVeil").GetComponent<Renderer>() : null;
                so.FindProperty("gauge").objectReferenceValue = gauge.GetComponent<Renderer>();
                so.FindProperty("chargeSound").objectReferenceValue = trig.GetComponent<AudioSource>();
                so.ApplyModifiedProperties();
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)w);
                log.Append("ワープ2–" + (i + 1) + " ");
            }
            // ロビーの道
            var a = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_" + (i + 1) + "_PathA.mat");
            var b = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_" + (i + 1) + "_PathB.mat");
            if (!a || !b) continue;
            var warp = slot.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("WarpToCourse"));
            float warpR = warp ? warp.bounds.extents.x : 1.2f;
            var to = slot.position - center; to.y = 0f;
            var mesh = FuwaLobbyPaths.Strip(StartR, to.magnitude - warpR * RingIn, floorY - center.y);
            mesh.name = "Stage2LobbyPath_" + i;
            string path = MeshDir + "/Stage2LobbyPath_" + i + ".asset";
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!ex) AssetDatabase.CreateAsset(mesh, path); else { ex.Clear(false); FuwaBevelTools.CopyInto(ex, mesh); ex.name = mesh.name; mesh = ex; }
            var go = new GameObject("Path_Course" + i, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(to.normalized, Vector3.up));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { a, b };
            go.layer = lobby.Find("LobbyFloor").gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            log.Append("道2–" + (i + 1) + " ");
        }
        if (langSo != null) { langSo.ApplyModifiedProperties(); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)lang); }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        return "[ステージ2のロビー] " + log;
    }
}
