using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// あそびばの蓄音機（2026-11-20 てじゃ「あそびばのBGM変更モード」「みためは蓄音機」「球の右側に」）
// モデルは Blender（BlenderSource/FLUFF_models.blend の Gramophone シーン）→ Models/PlaygroundGramophone.fbx
// FuwaPlayground の直下に置く（Room の作り直しでは消えない）。何度でも置き直せる
public static class FuwaGramophoneBuilder
{
    const string Fbx = "Assets/FuwaCourse/Models/PlaygroundGramophone.fbx";
    const string MatDir = "Assets/FuwaCourse/Materials/Playground";
    public const float Angle = 4f;       // カメラの球（-5.5°）の右。もどすボタンがいた場所
    public const float Radius = 12.3f;   // 島の真ん中から（柵は 14m）
    public const float Scale = 1.25f;    // 島の真ん中からも見えるよう少し大きく
    static readonly Vector3 Center = new Vector3(-72f, 88f, 80f);

    // えらべる曲（FuwaBgm.tracks の番号、-1＝おまかせ）。最後（おまけ）は全クリ後
    static readonly int[] Tracks = { -1, 0, 6, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 7, 14 };
    static readonly string[] Ja = { "おまかせ", "ロビー", "れんしゅう", "1 かぜのおか", "2 いわのみち", "3 やみのもり", "4 まもののす", "5 きのこのたに",
        "2–1 みずのこみち", "2–2 ほのおのやま", "2–3 こおりのみち", "2–4 はぐるまのしま", "2–5 そらのいせき", "ゴールのあと", "おまけ" };
    static readonly string[] En = { "Auto", "Lobby", "Practice", "1 Windy Hill", "2 Rock Road", "3 Dark Forest", "4 Monster Den", "5 Mushroom Valley",
        "2-1 Water Path", "2-2 Flame Mountain", "2-3 Ice Road", "2-4 Gear Isles", "2-5 Sky Ruins", "After the goal", "Bonus" };

    static Material Mat(string name, Color c, bool metal = false)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/ToonLit")) { name = name }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Color", c);
        m.SetColor("_ShadowTint", new Color(0.72f, 0.78f, 0.92f));
        m.SetFloat("_ShadowStrength", 0.4f);
        m.SetFloat("_RimStrength", 0.25f);
        if (metal) { m.SetFloat("_MetalSpec", 0.6f); m.SetFloat("_MetalEnv", 0.25f); m.SetFloat("_MetalSize", 0.92f); }   // 1 だとラッパが白く飛んだ
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("FuwaCourse/あそびば/蓄音機を置く")]
    public static string Build()
    {
        var pg = GameObject.Find("FuwaPlayground"); if (pg == null) return "FuwaPlayground がない";
        var old = pg.transform.Find("Gramophone"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx); if (src == null) return "FBX がない";
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src); PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Gramophone"; go.transform.SetParent(pg.transform, false);

        // 置き場所：デッキの上（床は下に光線を飛ばして決める）、島の真ん中を向く
        float a = Angle * Mathf.Deg2Rad;
        var pos = Center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Radius;
        float floorY = Center.y;
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
        pos.y = floorY;
        var toC = Center - pos; toC.y = 0f;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(toC.normalized));
        go.transform.localScale = Vector3.one * Scale;

        // 材質
        var wood = Mat("Gramo_Wood", new Color(0.86f, 0.62f, 0.42f));
        var woodDark = Mat("Gramo_WoodDark", new Color(0.62f, 0.42f, 0.28f));
        var brass = Mat("Gramo_Brass", new Color(0.86f, 0.62f, 0.3f), true);
        var record = Mat("Gramo_Record", new Color(0.22f, 0.22f, 0.32f));
        var label = Mat("Gramo_Label", new Color(1f, 0.62f, 0.68f));
        var plate = Mat("Gramo_Plate", new Color(1f, 0.96f, 0.88f));
        var button = Mat("Gramo_Button", new Color(1f, 0.66f, 0.7f));
        var sw = Mat("Gramo_Switch", new Color(0.6f, 0.8f, 1f));
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material m = wood;
            switch (r.name)
            {
                case "Gramo_Plinth": case "Gramo_Top": case "Gramo_Platter": m = woodDark; break;
                case "Gramo_Horn": case "Gramo_Arm": case "Gramo_Neck": case "Gramo_Soundbox": case "Gramo_Crank": case "Gramo_Spindle": m = brass; break;
                case "Gramo_Record": m = record; break;
                case "Gramo_RecordLabel": m = label; break;
                case "Gramo_Plate": case "Gramo_SwitchBase": m = plate; break;
                case "Gramo_BtnPrev": case "Gramo_BtnNext": m = button; break;
                case "Gramo_SwitchLever": m = sw; break;
            }
            r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = r.name == "Gramo_Horn" || r.name == "Gramo_Box" || r.name == "Gramo_Top" ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);
        }
        var T = go.transform;
        Transform F(string n) => T.Find(n);

        // 当たり：台（ぶつかる）
        var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.27f, 0f); bc.size = new Vector3(0.86f, 0.54f, 0.86f);

        // 本体のスクリプト
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        var clear = Object.FindObjectsOfType<FuwaAllClearReveal>(true).FirstOrDefault();
        var gm = UdonSharpUndo.AddComponent<FuwaGramophone>(go);
        gm.bgm = bgm; gm.language = lang; gm.allClear = clear;
        gm.trackIds = Tracks; gm.namesJa = Ja; gm.namesEn = En; gm.lockedLast = true;
        gm.record = F("Gramo_Record");

        // 文字（札と、スイッチの上）：テレビの題名と同じフォント
        var srcTmp = GameObject.Find("FuwaPlayground/Room/Monitor").GetComponentsInChildren<TMPro.TextMeshPro>(true).First(t => t.name == "Title");
        TMPro.TextMeshPro Txt(string n, Vector3 lp, Vector2 size, float fs)
        {
            var o = new GameObject(n); o.transform.SetParent(T, false);
            o.transform.localPosition = lp; o.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 前（+Z）から読める
            var t = o.AddComponent<TMPro.TextMeshPro>();
            t.font = srcTmp.font; t.fontSharedMaterial = srcTmp.fontSharedMaterial;
            t.rectTransform.sizeDelta = size; t.fontSize = fs; t.enableAutoSizing = true; t.fontSizeMin = fs * 0.4f; t.fontSizeMax = fs;
            t.alignment = TMPro.TextAlignmentOptions.Center; t.color = new Color(0.42f, 0.3f, 0.24f);
            t.enableWordWrapping = false;
            return t;
        }
        gm.label = Txt("TrackName", new Vector3(0f, 0.32f, 0.412f), new Vector2(0.34f, 0.1f), 0.6f);
        gm.switchLabel = Txt("SwitchLabel", new Vector3(0f, 0.225f, 0.41f), new Vector2(0.4f, 0.05f), 0.35f);
        gm.label.text = Ja[0]; gm.switchLabel.text = "動画中もBGM：OFF";   // Play 前の見た目（Play 中は言語に合わせて入れ直す）

        // 矢印（ボタンの上の三角）
        Mesh Tri(bool right)
        {
            var mesh = new Mesh { name = right ? "Gramo_ArrowR" : "Gramo_ArrowL" };
            float s = right ? 1f : -1f;
            mesh.vertices = new[] { new Vector3(-0.018f * s, 0.022f, 0f), new Vector3(-0.018f * s, -0.022f, 0f), new Vector3(0.022f * s, 0f, 0f) };
            mesh.triangles = right ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 };
            mesh.RecalculateNormals(); return mesh;
        }
        var arrowMat = Mat("Gramo_Arrow", new Color(1f, 1f, 1f));
        void Arrow(Transform btn, bool towardRight)
        {
            var o = new GameObject("Arrow"); o.transform.SetParent(btn, false);
            var mb = btn.GetComponent<MeshFilter>().sharedMesh.bounds;
            o.transform.localPosition = new Vector3(mb.center.x, mb.center.y, mb.max.z + 0.002f);
            o.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            o.AddComponent<MeshFilter>().sharedMesh = Tri(towardRight);
            var mr = o.AddComponent<MeshRenderer>(); mr.sharedMaterial = arrowMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // 前から見て：BtnPrev が左（モデルの +X）、BtnNext が右（-X）。三角の向きは見る人から見て
        Arrow(F("Gramo_BtnPrev"), false);   // 見る人から見て左向き
        Arrow(F("Gramo_BtnNext"), true);

        // ボタン（クリック）
        void Btn(Transform part, string ev, string ja, string en)
        {
            var mb = part.GetComponent<MeshFilter>().sharedMesh.bounds;
            var c = part.gameObject.AddComponent<BoxCollider>(); c.center = mb.center; c.size = mb.size + new Vector3(0.03f, 0.03f, 0.03f);
            var b = UdonSharpUndo.AddComponent<FuwaGramophoneButton>(part.gameObject);
            b.target = gm; b.eventName = ev; b.pressPart = part; b.pressOffset = new Vector3(0f, 0f, -0.012f);
            UdonSharpEditorUtility.CopyProxyToUdon(b);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b); if (ub != null) { ub.interactText = ja; ub.proximity = 3f; EditorUtility.SetDirty(ub); }
        }
        Btn(F("Gramo_BtnPrev"), "Prev", "まえの曲", "Previous");
        Btn(F("Gramo_BtnNext"), "Next", "つぎの曲", "Next");
        var lever = F("Gramo_SwitchLever");
        var sbase = F("Gramo_SwitchBase");
        // レバーは台ごとクリックできるように
        {
            var mb = sbase.GetComponent<MeshFilter>().sharedMesh.bounds;
            var c = sbase.gameObject.AddComponent<BoxCollider>(); c.center = mb.center; c.size = mb.size + new Vector3(0.03f, 0.04f, 0.05f);
            var b = UdonSharpUndo.AddComponent<FuwaGramophoneButton>(sbase.gameObject);
            b.target = gm; b.eventName = "ToggleVideoKeep"; b.pressPart = null;
            UdonSharpEditorUtility.CopyProxyToUdon(b);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b); if (ub != null) { ub.interactText = "動画中もBGM"; ub.proximity = 3f; EditorUtility.SetDirty(ub); }
        }
        // レバーの位置：OFF は左（見る人から）、ON は右
        var lp = lever.localPosition;
        gm.lever = lever; gm.leverOffPos = lp; gm.leverOnPos = lp + new Vector3(-0.07f, 0f, 0f);
        UdonSharpEditorUtility.CopyProxyToUdon(gm);
        var gub = UdonSharpEditorUtility.GetBackingUdonBehaviour(gm); if (gub != null) { gub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(gub); }

        // BGM：蓄音機の曲が効く場所＝あそびばの BallPark
        if (bgm != null)
        {
            bgm.overrideArea = pg.transform.Find("BallPark");
            UdonSharpEditorUtility.CopyProxyToUdon(bgm); EditorUtility.SetDirty(bgm);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pg.scene);
        return $"蓄音機を置いた pos={pos} bgm={(bgm != null)} lang={(lang != null)} allClear={(clear != null)} area={(bgm != null ? bgm.overrideArea : null)}";
    }
}
