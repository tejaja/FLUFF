using System.Linq;
using UnityEditor;
using UnityEngine;

// 2–2「ほのおのやま」の熱のテスト場（2026-10-28）。ステージ2のロビーの東 30m（前の水のテスト場の所）。
// てじゃ「炎はめちゃくちゃいれたい」→ 案①「温まると浮く」の手ざわりを決める：
//  ・スタートの台 → 低い道 → かがり火1 → 2.5m 上の段（温めて撃てば上がれる）
//  ・上の段の先は 4m の谷、まん中の空中にかがり火の輪（もう一度温めて渡る）→ 向こうの段
// 触って決めたら本番のコースに同じ部品で組む。テスト場はあとで消す（公開前に必ず）
public static class FuwaStage2FireTest
{
    const string RootName = "FuwaStage2_FireTest";
    static Vector3 Offset = new Vector3(30f, 0f, 0f);

    [MenuItem("FuwaCourse/ステージ2/熱のテスト場（2–2 試作）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "Lobby" && t.parent && t.parent.name == "FuwaStage2");
        if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;
        root.position = lobby.position + Offset;
        var ground = FuwaStage2WaterTest.ToonMat("Stage2_FireTestGround", new Color(0.82f, 0.45f, 0.38f));
        var path = FuwaStage2WaterTest.ToonMat("Stage2_FireTestPath", new Color(0.95f, 0.72f, 0.55f));
        float y0 = 0.1f;
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(b => b.transform.parent && b.transform.parent.name == "FuwaSystem") ?? Object.FindObjectOfType<FuwaBall>(true);

        // 熱（1つ）
        var hg = new GameObject("Heat"); hg.transform.SetParent(root, false);
        var heat = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHeat>(hg); heat.ball = ball;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(heat); FuwaStage2WaterTest.NoSync(heat);

        // 道（ふわふわが乗るのは道のレイヤー11）
        FuwaStage2WaterTest.Box(root, "Walkway", new Vector3(-9f, y0 - 0.25f, 0f), new Vector3(14f, 0.5f, 3f), path, 11, false);
        FuwaStage2WaterTest.Box(root, "Low", new Vector3(3f, y0 - 0.25f, 0f), new Vector3(10f, 0.5f, 3f), path, 11, false);
        // 上の段（2.5m）：手前は壁
        float up = 2.5f;
        FuwaStage2WaterTest.Box(root, "Upper", new Vector3(12f, y0 + up - 0.25f, 0f), new Vector3(8f, 0.5f, 3f), path, 11, false);
        FuwaStage2WaterTest.Box(root, "UpperWall", new Vector3(8.25f, y0 + up / 2f - 0.25f, 0f), new Vector3(0.5f, up, 3f), ground, 0, false);
        // 谷の向こうの段
        FuwaStage2WaterTest.Box(root, "Far", new Vector3(24f, y0 + up + 0.5f - 0.25f, 0f), new Vector3(8f, 0.5f, 3f), path, 11, false);

        // かがり火1：上の段の手前、低い道のはし
        Brazier(root, "Brazier1", new Vector3(6.5f, y0, 0f), heat, false);
        // 回る羽根：上の段の入口。かがり火で浮かせて止めて、羽根のすき間をねらって撃つ（2026-10-28 てじゃ「とどまるのを利用、タイミング図るのムズイポイント」）
        Fan(root, "Fan1", new Vector3(9.6f, y0 + up + 1.7f, 0f), 75f);   // 羽根の先が床にあたらない高さ（羽根の先まで 1.55m）
        // かがり火の輪：谷のまん中の空中
        Brazier(root, "FireRing", new Vector3(18f, y0 + up + 1.2f, 0f), heat, true);

        // テスト用のふわふわの台（公開前に消す）
        var spot = new GameObject("TestBallStart").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(-3f, y0 + 1.2f, 0f);
        FuwaStage2WaterTest.BallStand(root, "BallStand_Fire", new Vector3(-4.5f, y0, -1.2f), spot, "ふわふわ：熱のテスト", new Color(1f, 0.55f, 0.2f));
        var spot2 = new GameObject("TestBallUpper").transform; spot2.SetParent(root, false); spot2.localPosition = new Vector3(13.5f, y0 + up + 1.2f, 0f);
        FuwaStage2WaterTest.BallStand(root, "BallStand_Upper", new Vector3(13.5f, y0 + up, -1.2f), spot2, "ふわふわ：上の段から", new Color(1f, 0.35f, 0.15f));

        // ステージ2にいる時だけ表示
        var vis = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "FuwaAreaVisibility");
        if (vis)
        {
            var so = new SerializedObject(vis); var arr = so.FindProperty("stage2Objects");
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject;
            so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)vis);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[熱のテスト場] " + root.position;
    }

    // 回る羽根：進む向き（+X）を軸に回る3枚羽根。羽根の当たりは跳ね返すだけ（アウトにしない）
    static void Fan(Transform root, string name, Vector3 lp, float degPerSec)
    {
        var g = new GameObject(name).transform; g.SetParent(root, false); g.localPosition = lp;
        var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder); hub.name = "Hub"; hub.transform.SetParent(g, false);
        hub.transform.localRotation = Quaternion.Euler(0, 0, 90f); hub.transform.localScale = new Vector3(0.35f, 0.2f, 0.35f);
        hub.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_FireTestRock", new Color(0.35f, 0.3f, 0.32f));
        var rot = new GameObject("Rotor"); rot.transform.SetParent(g, false);
        var rb = rot.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        for (int k = 0; k < 3; k++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = "Blade" + k; b.transform.SetParent(rot.transform, false);
            b.transform.localRotation = Quaternion.Euler(k * 120f, 0, 0);
            b.transform.localPosition = b.transform.localRotation * new Vector3(0, 0.8f, 0);
            b.transform.localScale = new Vector3(0.25f, 1.5f, 0.35f);
            b.GetComponent<Renderer>().sharedMaterial = FuwaStage2WaterTest.ToonMat("Stage2_FireTestBlade", new Color(0.95f, 0.5f, 0.3f));
        }
        var sp = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaSpinner>(rot); sp.axis = Vector3.right; sp.degPerSec = degPerSec;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sp); FuwaStage2WaterTest.NoSync(sp);
    }

    // かがり火：台（輪なら台なし）＋炎の粒＋熱の範囲（トリガー、半径 1m・高さ 2m）
    static void Brazier(Transform root, string name, Vector3 lp, FuwaHeat heat, bool ring)
    {
        var g = new GameObject(name).transform; g.SetParent(root, false); g.localPosition = lp;
        var rock = FuwaStage2WaterTest.ToonMat("Stage2_FireTestRock", new Color(0.35f, 0.3f, 0.32f));
        if (!ring)
        {
            var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bowl.name = "Bowl"; bowl.transform.SetParent(g, false);
            bowl.transform.localPosition = new Vector3(0, 0.4f, 0); bowl.transform.localScale = new Vector3(0.9f, 0.4f, 0.9f);
            bowl.GetComponent<Renderer>().sharedMaterial = rock;
        }
        else
        {
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "Ring" + k; b.transform.SetParent(g, false);
                b.transform.localPosition = new Vector3(0, Mathf.Sin(a) * 1.1f, Mathf.Cos(a) * 1.1f); b.transform.localScale = Vector3.one * 0.28f;
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.GetComponent<Renderer>().sharedMaterial = rock;
            }
        }
        // 炎の粒
        var fo = new GameObject("Flame"); fo.transform.SetParent(g, false); fo.transform.localPosition = new Vector3(0, ring ? -1.0f : 0.8f, 0);
        fo.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
        var ps = fo.AddComponent<ParticleSystem>();
        var main = ps.main; main.startLifetime = 0.8f; main.startSpeed = 1.6f; main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.35f, 0.1f)); main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = ring ? 60f : 40f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = ring ? 0.25f : 0.35f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0.2f)));
        var pr = fo.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        // 熱の範囲
        var z = new GameObject("HeatZone"); z.transform.SetParent(g, false);
        z.transform.localPosition = new Vector3(0, ring ? 0f : 1.6f, 0);
        var bc = z.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = ring ? new Vector3(1.2f, 2.0f, 2.0f) : new Vector3(1.8f, 2.0f, 1.8f);
        z.layer = 4;
        var hs = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHeatSource>(z); hs.heat = heat;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hs); FuwaStage2WaterTest.NoSync(hs);
    }
}
