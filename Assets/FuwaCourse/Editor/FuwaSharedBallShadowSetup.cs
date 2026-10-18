using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 同期ふわふわ（FuwaSharedBall）全部に、コースのふわふわと同じウソ影を付ける（FuwaSystem/SharedBallShadows、FuwaSharedBallShadows）。
// 2026-10-13 てじゃ「同期フワフワの影がみえない」。同期ふわふわを作り直した後（チルルームを作り直す等）にも実行する。何度実行してもよい
public static class FuwaSharedBallShadowSetup
{
    [MenuItem("FuwaCourse/済み（押す前に相談）/同期ふわふわの影を作り直す")]
    public static void Menu()
    {
        Debug.Log(Run());
        var sys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaSystem" && t.parent == null);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sys.gameObject.scene);
    }

    public static string Run()
    {
        var sys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaSystem" && t.parent == null);
        var src = sys.Find("BallShadow/ShadowQuad");
        if (src == null) return "no BallShadow/ShadowQuad";
        var old = sys.Find("SharedBallShadows"); if (old != null) Object.DestroyImmediate(old.gameObject);
        string path = "Assets/FuwaCourse/Scripts/FuwaSharedBallShadows.asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) == null)
        {
            var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaSharedBallShadows.cs");
            AssetDatabase.CreateAsset(prog, path); AssetDatabase.SaveAssets();
        }
        var root = new GameObject("SharedBallShadows"); root.transform.SetParent(sys, false);
        // 同期物ではない（影は自分の画面だけで置く）ので FuwaSystem の下でよい
        var balls = Object.FindObjectsOfType<FuwaSharedBall>(true).OrderBy(b => b.transform.root.name).ThenBy(b => b.name).ToArray();
        var markers = new Renderer[balls.Length];
        for (int i = 0; i < balls.Length; i++)
        {
            var g = Object.Instantiate(src.gameObject, root.transform, false); g.name = "Shadow_" + balls[i].name;
            var r = g.GetComponent<Renderer>(); r.enabled = false; markers[i] = r;
            GameObjectUtility.SetStaticEditorFlags(g, 0);
        }
        var sh = UdonSharpUndo.AddComponent<FuwaSharedBallShadows>(root);
        sh.balls = balls; sh.markers = markers;
        // コースのふわふわの影（FuwaGroundMarker）と同じ値
        var gm = sys.Find("BallShadow").GetComponent<FuwaGroundMarker>();
        if (gm != null)
        {
            sh.groundMask = gm.groundMask; sh.maxDistance = gm.maxDistance; sh.nearSize = gm.nearSize; sh.farSize = gm.farSize;
            sh.nearAlpha = gm.nearAlpha; sh.farAlpha = gm.farAlpha; sh.farHeight = gm.farHeight; sh.decalBelow = gm.decalBelow; sh.decalAbove = gm.decalAbove;
        }
        UdonSharpEditorUtility.CopyProxyToUdon(sh);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(sh); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        return "shared ball shadows: " + balls.Length;
    }
}
