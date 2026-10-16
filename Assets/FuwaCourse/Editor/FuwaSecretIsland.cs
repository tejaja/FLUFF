using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ひみつの小島（2026-10-16 てじゃ「おもちゃ全解放スイッチ、ウラワザみたいな感じで」「ここ飛び降りた先に小島つくるとか」「ボードを突っ切る形」）。
// ロビーの写真館の「かってにスクショ」ボード（当たりなし）を突っ切って柵を飛び越えると、6mくらい下に小島がある。
// 見た目は写真館の島（草の円盤＋下の岩）を小さくしたもの。まわりの飾りの浮島にまぎれる。
// ・ひみつのきのこ（FuwaSecretUnlock）：クリックであそびばのおもちゃを全部出す
// ・ロビーへのワープ（あそびばへのワープを写して、行き先をロビーに）
// ・下に落ちたら暗転してロビーへ（FuwaLobbyFall。写真館から飛び降りて島に届かなかった時も）
// 何度実行しても同じ結果（FuwaLobby/SecretIsland と SecretFallReturn を作り直す）
public static class FuwaSecretIsland
{
    const string Root = "SecretIsland", FallName = "SecretFallReturn";
    const float Drop = 6f;           // 写真館の地面から小島の地面まで
    const float Beyond = 2.4f;       // 柵から小島の真ん中まで（横）
    const float FenceR = 4.6f;       // 写真館の真ん中から柵まで
    const float IslandScale = 0.37f; // 写真館の島（直径9.7m）→ 3.6m
    const string MatDir = "Assets/FuwaCourse/Materials/";

    [MenuItem("FuwaCourse/ロビー/ひみつの小島を作る（おもちゃ全部出すきのこ）")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaLobby").transform;
        var studio = lobby.Find("PhotoStudio");
        var gallery = lobby.Find("PhotoGallery");
        foreach (var n in new[] { Root, FallName }) { var o = lobby.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }

        // 位置：写真館の真ん中から「かってにスクショ」ボードの向こうへ
        Vector3 c = studio.position;
        Vector3 d = gallery.position - c; d.y = 0f; d.Normalize();
        Vector3 top = c + d * (FenceR + Beyond) + Vector3.down * Drop;
        var root = new GameObject(Root).transform; root.SetParent(lobby, false);
        root.position = top; root.rotation = Quaternion.LookRotation(-d, Vector3.up);   // 前（+Z）＝写真館の方

        // 島：写真館の草の円盤と下の岩を小さくして写す
        foreach (var n in new[] { "Ground", "IslandUnder" })
        {
            var src = studio.Find(n);
            var g = Object.Instantiate(src.gameObject, root, false); g.name = n;
            var s = src.localScale;
            g.transform.localPosition = new Vector3(0f, src.localPosition.y * (n == "Ground" ? 1f : 0.8f), 0f);
            g.transform.localRotation = src.localRotation;
            g.transform.localScale = n == "Ground" ? new Vector3(s.x * IslandScale, s.y, s.z * IslandScale) : new Vector3(s.x * IslandScale, s.y * 0.5f, s.z * IslandScale);
            g.layer = src.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(g, GameObjectUtility.GetStaticEditorFlags(src.gameObject) | StaticEditorFlags.BatchingStatic);
        }

        var spawner = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        string mush = MakeMushroom(root, lobby, spawner);
        string warp = MakeReturnWarp(root, lobby);
        MakeFallReturn(lobby, top);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        return "secret island at " + top.ToString("F2") + " " + mush + " " + warp;
    }

    // ひみつのきのこ：コースのスタートのきのこと同じ形で、傘が金色。まわりにキラキラ
    static string MakeMushroom(Transform root, Transform lobby, FuwaPlaygroundSpawner spawner)
    {
        var pm = lobby.Find("PracticeMushroom");
        var m = new GameObject("SecretMushroom").transform; m.SetParent(root, false);
        // 降りた人は写真館と反対（-Z）を向いているので、きのことワープはその前に左右に並べる
        m.localPosition = new Vector3(0.6f, 0f, -0.3f); m.localRotation = Quaternion.identity; m.localScale = Vector3.one * 1.15f;
        foreach (var n in new[] { "Stem", "Cap", "Gill" })
        {
            var src = pm.Find(n);
            var g = new GameObject(n); g.transform.SetParent(m, false);
            g.transform.localPosition = src.localPosition; g.transform.localRotation = src.localRotation; g.transform.localScale = src.localScale;
            g.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            var r = g.AddComponent<MeshRenderer>(); EditorUtility.CopySerialized(src.GetComponent<MeshRenderer>(), r);
            if (n == "Cap") r.sharedMaterial = SecretCapMaterial(r.sharedMaterial);
            if (n == "Gill") g.SetActive(src.gameObject.activeSelf);
        }
        var cap = m.Find("Cap");
        var sc = cap.gameObject.AddComponent<SphereCollider>(); var ssc = pm.Find("Cap").GetComponent<SphereCollider>();
        sc.center = ssc.center; sc.radius = ssc.radius;

        // キラキラ（あそびばへのワープの粒を金色にして写す）と、押した時の粒（練習のきのこの胞子を写す）
        var motesSrc = lobby.Find("ToPlayground/ReturnToHub_Pad_Motes");
        var motes = Object.Instantiate(motesSrc.gameObject, m, false); motes.name = "Sparkles"; motes.SetActive(true);
        motes.transform.localPosition = new Vector3(0f, 0.2f, 0f); motes.transform.localScale = Vector3.one * 0.6f;
        Tint(motes.GetComponent<ParticleSystem>(), new Color(1f, 0.86f, 0.35f), new Color(1f, 0.97f, 0.75f));
        var burstSrc = lobby.Find("PracticeSporeBurst");
        var burst = Object.Instantiate(burstSrc.gameObject, m, false); burst.name = "Burst";
        burst.transform.localPosition = new Vector3(0f, 0.62f, 0f); burst.transform.localRotation = Quaternion.identity; burst.transform.localScale = Vector3.one;
        var bps = burst.GetComponent<ParticleSystem>();
        Tint(bps, new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 0.8f));
        var bmain = bps.main; bmain.playOnAwake = false;

        // 音
        var au = cap.gameObject.AddComponent<AudioSource>();
        au.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/sfx_goal.wav");
        au.playOnAwake = false; au.spatialBlend = 1f; au.rolloffMode = AudioRolloffMode.Linear; au.minDistance = 2f; au.maxDistance = 25f; au.volume = 0.8f;
        cap.gameObject.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
        var again = new GameObject("AgainSfx"); again.transform.SetParent(cap, false);
        var au2 = again.AddComponent<AudioSource>();
        au2.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/sfx_boing.wav");
        au2.playOnAwake = false; au2.spatialBlend = 1f; au2.rolloffMode = AudioRolloffMode.Linear; au2.minDistance = 2f; au2.maxDistance = 20f; au2.volume = 0.7f;
        again.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();

        // きのこの上に浮かぶ文字（ワープの札と同じ見た目。2行入る大きさ）。押した時だけ出す
        var pop = Object.Instantiate(lobby.Find("ToPlayground/Label").gameObject, m, false); pop.name = "Popup";
        pop.transform.localPosition = new Vector3(0f, 1.45f, 0f); pop.transform.localRotation = Quaternion.identity; pop.transform.localScale = Vector3.one;
        var popT = pop.GetComponent<TMPro.TextMeshPro>(); popT.text = "あそびばの おもちゃが\nぜんぶ でたよ！"; popT.fontSize = 2.6f; popT.lineSpacing = -10f;
        var panel = pop.transform.Find("Panel").GetComponent<SpriteRenderer>(); panel.size = new Vector2(2.75f, 1.05f);
        foreach (var lg in pop.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lg);
        pop.SetActive(false);

        // スクリプト（傘に付ける。当たりも傘）
        EnsureProgram("FuwaSecretUnlock");
        var su = UdonSharpUndo.AddComponent<FuwaSecretUnlock>(cap.gameObject);
        su.spawner = spawner; su.burst = bps; su.sound = au; su.soundAgain = au2; su.cap = cap;
        su.popup = popT; su.popupRoot = pop.transform; su.popupPanel = panel; su.language = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        UdonSharpEditorUtility.CopyProxyToUdon(su);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(su);
        if (ub != null) { ub.interactText = "？？？"; ub.proximity = 2f; ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        EditorUtility.SetDirty(su);
        foreach (var t in m.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        return "mushroom ok";
    }

    static Material SecretCapMaterial(Material baseMat)
    {
        string path = MatDir + "Mush_CapSecret.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var src = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Mush_CapPink.mat");
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        else m.CopyPropertiesFromMaterial(src);
        m.SetColor("_Color", new Color(1f, 0.8f, 0.32f));
        if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0.45f);
        if (m.HasProperty("_RimColor")) m.SetColor("_RimColor", new Color(1f, 0.98f, 0.85f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Tint(ParticleSystem ps, Color a, Color b)
    {
        if (ps == null) return;
        var main = ps.main; main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        var col = ps.colorOverLifetime;
        if (col.enabled)
        {
            // 透明度の変化だけ残して色は白に（startColor の色が出るように）
            var g = col.color.gradient;
            if (g != null) { var ng = new Gradient(); ng.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, g.alphaKeys); col.color = new ParticleSystem.MinMaxGradient(ng); }
        }
    }

    // ロビーへ戻るワープ：あそびばへのワープを写して、行き先をロビーにする（ハブからロビーへ行くワープと同じ設定）
    static string MakeReturnWarp(Transform root, Transform lobby)
    {
        var src = lobby.Find("ToPlayground");
        var w = Object.Instantiate(src.gameObject, root, false); w.name = "SecretReturn";
        w.transform.localPosition = new Vector3(-0.55f, 0f, -0.45f); w.transform.localRotation = Quaternion.identity; w.transform.localScale = Vector3.one * 0.6f;
        // ロビーのあそびばへのワープは今はボタンで呼ぶので、床の輪・文字・粒が非表示になっている。こっちは乗って使うので出す
        foreach (Transform ch in w.transform) ch.gameObject.SetActive(true);
        var trig = w.GetComponentInChildren<FuwaCourseWarp>(true);
        var solo = GameObject.Find("FuwaHub").transform.Find("SoloGate/SoloGate_Trigger").GetComponent<FuwaCourseWarp>();
        var so = new SerializedObject(trig); var ss = new SerializedObject(solo);
        foreach (var f in new[] { "courseStart", "playerSpot", "gunSpot" }) so.FindProperty(f).objectReferenceValue = ss.FindProperty(f).objectReferenceValue;
        so.FindProperty("area").intValue = ss.FindProperty("area").intValue;
        so.FindProperty("homeAction").intValue = ss.FindProperty("homeAction").intValue;
        so.ApplyModifiedPropertiesWithoutUndo();
        UdonSharpEditorUtility.CopyProxyToUdon(trig); EditorUtility.SetDirty(trig);
        foreach (var r in w.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);

        // 文字：「ロビーへ」（言語切りかえに登録）
        var label = w.transform.Find("Label").GetComponent<TMPro.TextMeshPro>();
        label.text = "ロビーへ"; EditorUtility.SetDirty(label);
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        var lso = new SerializedObject(lang);
        var texts = lso.FindProperty("texts"); var ja = lso.FindProperty("ja"); var en = lso.FindProperty("en");
        int k = texts.arraySize;
        for (int i = 0; i < texts.arraySize; i++) if (texts.GetArrayElementAtIndex(i).objectReferenceValue == null) { k = i; break; }
        if (k == texts.arraySize) { texts.arraySize++; ja.arraySize = texts.arraySize; en.arraySize = texts.arraySize; }
        texts.GetArrayElementAtIndex(k).objectReferenceValue = label;
        ja.GetArrayElementAtIndex(k).stringValue = "ロビーへ";
        en.GetArrayElementAtIndex(k).stringValue = "Lobby";
        lso.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang);
        return "warp ok (lang " + k + ")";
    }

    // 写真館・小島の下に落ちたら暗転してロビーへ（ロビーの下の FallReturn を写して、写真館のまわりを広くおおう）
    static void MakeFallReturn(Transform lobby, Vector3 top)
    {
        var src = lobby.Find("FallReturn");
        var g = Object.Instantiate(src.gameObject, lobby, false); g.name = FallName;
        g.transform.position = new Vector3((top.x + lobby.Find("PhotoStudio").position.x) * 0.5f, top.y - 4f, (top.z + lobby.Find("PhotoStudio").position.z) * 0.5f);
        g.transform.localScale = new Vector3(12f, src.localScale.y, 12f);
        var lf = g.GetComponent<FuwaLobbyFall>(); if (lf != null) { UdonSharpEditorUtility.CopyProxyToUdon(lf); EditorUtility.SetDirty(lf); }
    }

    static void EnsureProgram(string name)
    {
        string path = "Assets/FuwaCourse/Scripts/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/" + name + ".cs");
        AssetDatabase.CreateAsset(prog, path); AssetDatabase.SaveAssets();
    }
}
