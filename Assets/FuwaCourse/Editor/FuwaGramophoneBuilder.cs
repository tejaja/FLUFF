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

    // えらべる曲（FuwaBgm.tracks の番号）。最後（おまけ）は全クリ後
    // 2026-11-21 てじゃ「おまかせってなに？」「そもそもロビーと同じ」→おまかせは消して先頭をロビーに
    // 最後は BGMなし（-2。2026-11-21 てじゃ「BGMなしもいれとこ」）。おまけ（LockedIndex）は全クリ後だけ出る
    static readonly int[] Tracks = { 0, 6, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 7, 14, -2 };
    const int LockedIndex = 13;
    static readonly string[] Ja = { "ロビー", "れんしゅう", "1 かぜのおか", "2 いわのみち", "3 やみのもり", "4 まもののす", "5 きのこのたに",
        "2–1 みずのこみち", "2–2 ほのおのやま", "2–3 こおりのみち", "2–4 はぐるまのしま", "2–5 そらのいせき", "ゴールのあと", "おまけ", "BGMなし" };
    static readonly string[] En = { "Lobby", "Practice", "1 Windy Hill", "2 Rock Road", "3 Dark Forest", "4 Monster Den", "5 Mushroom Valley",
        "2-1 Water Path", "2-2 Flame Mountain", "2-3 Ice Road", "2-4 Gear Isles", "2-5 Sky Ruins", "After the goal", "Bonus", "No music" };

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

        // レコードの子（ラベル・軸）：FBX で X に90°回った向きで入ってきて、レコードの下の箱の中に埋まっていた。
        // 回すとレコードの周りをぐるぐる回っていた（2026-11-21 てじゃ「レコードの動きおかしい」）。頂点はもう上向きなので回転をなくす
        foreach (var n in new[] { "Gramo_RecordLabel", "Gramo_Spindle" })
        {
            var c = T.Find("Gramo_Record/" + n); if (c == null) continue;
            c.localPosition = Vector3.zero; c.localRotation = Quaternion.identity; c.localScale = Vector3.one;
        }

        // 当たり：台（ぶつかる）
        var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.27f, 0f); bc.size = new Vector3(0.86f, 0.54f, 0.86f);

        // 本体のスクリプト
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        var clear = Object.FindObjectsOfType<FuwaAllClearReveal>(true).FirstOrDefault();
        var gm = UdonSharpUndo.AddComponent<FuwaGramophone>(go);
        gm.bgm = bgm; gm.language = lang; gm.allClear = clear;
        gm.trackIds = Tracks; gm.namesJa = Ja; gm.namesEn = En; gm.lockedIndex = LockedIndex;
        gm.record = F("Gramo_Record");

        // 文字（札と、スイッチの横）：テレビの題名と同じフォント
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
        gm.switchLabel = Txt("SwitchLabel", new Vector3(0.15f, 0.165f, 0.41f), new Vector2(0.28f, 0.07f), 0.5f);
        gm.label.text = Ja[0]; gm.switchLabel.text = "動画再生中";   // Play 前の見た目（Play 中は言語に合わせて入れ直す）

        // ボタンはほかと同じレーザーで押すボタン（2026-11-21 てじゃ「ボタンを他のボタンみたいなビーム式にしよ」）。
        // 前の3Dのボタン・レバーは隠す
        foreach (var n in new[] { "Gramo_BtnPrev", "Gramo_BtnNext", "Gramo_SwitchBase", "Gramo_SwitchLever" }) { var t = F(n); if (t != null) t.gameObject.SetActive(false); }
        UdonSharpEditorUtility.CopyProxyToUdon(gm);
        Transform Laser(string name, Vector3 lp, float w, float h, string text, string evt)
        {
            // lp・w・h は蓄音機のローカル（大きさ Scale 倍で世界の大きさ）。ボタンの板は 400 単位＝高さ h
            float worldH = h * Scale, worldW = w * Scale;
            FuwaPlaygroundBuilder.LaserButton(T, name, T.TransformPoint(lp), T.rotation * Quaternion.Euler(0f, 180f, 0f), (400f * worldW / worldH) / 1400f, text, gm, evt);
            var b = T.Find(name);
            b.localScale = Vector3.one * (worldH / 0.4f) / Scale;
            return b;
        }
        // ◀ ▶ はフォントに字がない（豆腐になる）ので、三角の板をボタンの面のすぐ手前に置く
        var prev = Laser("PrevButton", new Vector3(0.27f, 0.32f, 0.405f), 0.105f, 0.105f, "", "Prev");
        var next = Laser("NextButton", new Vector3(-0.27f, 0.32f, 0.405f), 0.105f, 0.105f, "", "Next");
        foreach (var b in new[] { prev, next }) FuwaPlaygroundBuilder.OutlinedStyle(b);
        Mesh Tri(bool right)
        {
            var mesh = new Mesh { name = right ? "Gramo_ArrowR" : "Gramo_ArrowL" };
            float s = right ? 1f : -1f;   // 大きさは蓄音機のローカル（ボタンの直径 0.105 の半分くらい）
            mesh.vertices = new[] { new Vector3(-0.022f * s, 0.027f, 0f), new Vector3(-0.022f * s, -0.027f, 0f), new Vector3(0.027f * s, 0f, 0f) };
            mesh.triangles = right ? new[] { 0, 2, 1 } : new[] { 0, 1, 2 };
            mesh.RecalculateNormals(); return mesh;
        }
        var arrowMat = Mat("Gramo_Arrow", new Color(0.27f, 0.36f, 0.62f));   // スイッチの文字と同じ紺
        void Arrow(Vector3 lp, bool towardRight)
        {
            var o = new GameObject(towardRight ? "ArrowNext" : "ArrowPrev"); o.transform.SetParent(T, false);
            o.transform.localPosition = lp; o.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            o.AddComponent<MeshFilter>().sharedMesh = Tri(towardRight);
            var mr = o.AddComponent<MeshRenderer>(); mr.sharedMaterial = arrowMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // 前から見て：Prev が左（モデルの +X）、Next が右（-X）。三角の向きは見る人から見て
        Arrow(new Vector3(0.27f, 0.32f, 0.411f), false);
        Arrow(new Vector3(-0.27f, 0.32f, 0.411f), true);
        var onB = Laser("VideoOn", new Vector3(-0.09f, 0.165f, 0.405f), 0.13f, 0.075f, "ON", "VideoKeepOn");
        var offB = Laser("VideoOff", new Vector3(-0.25f, 0.165f, 0.405f), 0.13f, 0.075f, "OFF", "VideoKeepOff");
        UnityEngine.UI.Image Face(Transform b)
        {
            var btn = b.GetComponentInChildren<UnityEngine.UI.Button>(true);
            var cb = btn.colors; cb.normalColor = Color.white; cb.selectedColor = Color.white; cb.highlightedColor = new Color(0.94f, 0.94f, 0.94f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); btn.colors = cb;
            FuwaPlaygroundBuilder.PillCorners(b);
            EditorUtility.SetDirty(btn);
            return btn.GetComponent<UnityEngine.UI.Image>();
        }
        TMPro.TextMeshProUGUI Lab(Transform b) { var t = b.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).First(x => x.name != "TextOutlined"); t.margin = new Vector4(20f, 0f, 20f, 30f); EditorUtility.SetDirty(t); return t; }
        gm.onBg = Face(onB); gm.offBg = Face(offB); gm.onText = Lab(onB); gm.offText = Lab(offB);
        gm.selectedBg = Color.white; gm.idleBg = new Color(0.62f, 0.42f, 0.28f);   // 選んでない方は台の濃い木の色
        gm.selectedText = new Color(0.27f, 0.36f, 0.62f); gm.idleText = new Color(1f, 0.93f, 0.82f);
        // エディタで見た時も今の状態（OFF）に
        gm.onBg.color = gm.idleBg; gm.offBg.color = gm.selectedBg; gm.onText.color = gm.idleText; gm.offText.color = gm.selectedText;
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
