using UnityEditor;
using UnityEngine;

// 入口（タイトル）の島の中心からチャレンジのワープまでシマシマの道をのばす（2026-11-17 てじゃ「チャレンジのほうに、中心から道のようなものをひくか」）。
// 作りはロビーの道（FuwaLobbyPaths.Strip）と同じ。色はチャレンジ看板のふち（青むらさき）を明るくしたもの。先はワープのうずの下にもぐらせる。
// 何度実行してもよい（ワープを動かしたら作り直す）
public static class FuwaHubPath
{
    const float RingIn = 0.85f;     // うずの半径の何割の所まで道を入れるか（ロビーと同じ）
    const string MeshPath = "Assets/FuwaCourse/Meshes/Lobby/HubPath_Challenge.asset";
    static readonly Color ColA = new Color(0.84f, 0.87f, 0.98f), ColB = new Color(0.72f, 0.77f, 0.94f);

    [MenuItem("FuwaCourse/済み（押す前に相談）/入口のチャレンジへの道を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var hub = GameObject.Find("FuwaHub").transform;
        var floor = hub.Find("HubFloor");
        var gate = hub.Find("SoloGate");
        float floorY = floor.GetComponent<Renderer>().bounds.max.y;
        Vector3 center = new Vector3(hub.position.x, floorY, hub.position.z);
        float warpR = gate.Find("SoloGate_Pad").GetComponent<Renderer>().bounds.extents.x;
        var to = gate.position - center; to.y = 0f;
        float len = to.magnitude - warpR * RingIn;

        var mesh = FuwaLobbyPaths.Strip(0f, len, 0f);
        mesh.name = "HubPath_Challenge";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (ex == null) AssetDatabase.CreateAsset(mesh, MeshPath); else { ex.Clear(); EditorUtility.CopySerialized(mesh, ex); ex.name = mesh.name; mesh = ex; }

        var old = hub.Find("ChallengePath"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("ChallengePath", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(hub, true);
        go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(to.normalized, Vector3.up));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat("HubPath_ChallengeA", ColA), Mat("HubPath_ChallengeB", ColB) };
        go.layer = floor.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hub.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "hub path " + len.ToString("F2") + "m";
    }

    // ロビーの道の材質（Course0_PathA）を写して色だけ変える
    static Material Mat(string n, Color c)
    {
        string p = "Assets/FuwaCourse/Materials/" + n + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(FuwaLobbyPaths.FindMat("Course0_PathA")); AssetDatabase.CreateAsset(m, p); }
        m.color = c; EditorUtility.SetDirty(m);
        return m;
    }
}
