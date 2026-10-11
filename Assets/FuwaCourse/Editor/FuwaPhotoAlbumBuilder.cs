using System.Linq;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 名場面アルバム（FuwaPhotoAlbum）を組み立てる。2026-10-11 てじゃ「遊んでるところのスクショを自動で撮ってロビーで見れるやつ、テストで作ろう」
// ・FuwaSystem/PhotoAlbum：本体（Udon）、撮る用のカメラ、VRChatカメラ差し替え用の板
// ・FuwaLobby/PhotoGallery：写真を飾るボード（コース4とコース5の看板の間、最速タイムの看板と同じデザイン）
// ・FuwaLanguage につなぐ。各コースのフォトポイントは FuwaPhotoPointBuilder（ないコースだけ作る）
// 本体（Udon）は作り直さない（ネットワークIDの一覧が崩れるので）。ボードと中身は何度でも作り直してよい
public static class FuwaPhotoAlbumBuilder
{
    const string ScriptPath = "Assets/FuwaCourse/Scripts/FuwaPhotoAlbum.cs";
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaPhotoAlbum.asset";
    const string FeedMatPath = "Assets/FuwaCourse/Materials/PhotoAlbumFeed.mat";
    const string PhotoMatPath = "Assets/FuwaCourse/Materials/PhotoAlbumPhoto.mat";
    const int Cols = 3, Rows = 2;
    // ロビーの中心から見た角度（度、+x から反時計回り。コース5の看板=218°、コース4=270°の間）と距離
    const float AngleDeg = 244f;
    const float Radius = 7.4f;
    static readonly Vector2 PhotoSize = new Vector2(1.16f, 0.6525f);

    [MenuItem("FuwaCourse/名場面アルバムを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new System.Collections.Generic.List<string>();
        EnsureProgram();
        var sys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaSystem" && t.parent == null);
        var lobby = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaLobby" && t.parent == null);

        // 本体
        var ht = sys.Find("PhotoAlbum");
        var holder = ht != null ? ht.gameObject : new GameObject("PhotoAlbum");
        holder.transform.SetParent(sys, false);
        var album = holder.GetComponent<FuwaPhotoAlbum>();
        if (album == null) { album = UdonSharpUndo.AddComponent<FuwaPhotoAlbum>(holder); log.Add("added FuwaPhotoAlbum"); }
        FixSyncNone(album);

        // 撮る用のカメラ（追っかけカメラの設定をまねる）
        var camT = holder.transform.Find("ShotCam");
        var cam = camT != null ? camT.GetComponent<Camera>() : null;
        if (cam == null) { var go = new GameObject("ShotCam"); go.transform.SetParent(holder.transform, false); cam = go.AddComponent<Camera>(); }
        var follow = Object.FindObjectsOfType<FuwaFollowCam>(true).FirstOrDefault();
        if (follow != null && follow.cam != null) EditorUtility.CopySerialized(follow.cam, cam);
        cam.enabled = false;
        cam.targetTexture = null;
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.05f;
        // 自分のアバターは頭ありの方（MirrorReflection）を写す。一人称用（PlayerLocal、頭が消えてる）とメニュー類は写さない
        int mask = ~0;
        foreach (var l in new[] { "PlayerLocal", "UiMenu", "InternalUI", "StereoLeft", "StereoRight", "HardwareObjects" })
        {
            int li = LayerMask.NameToLayer(l);
            if (li >= 0) mask &= ~(1 << li);
        }
        cam.cullingMask = mask;

        // VRChatカメラ差し替え用の板
        var feedMat = AssetDatabase.LoadAssetAtPath<Material>(FeedMatPath);
        if (feedMat == null) { feedMat = new Material(Shader.Find("Fuwa/PhotoAlbumFeed")); AssetDatabase.CreateAsset(feedMat, FeedMatPath); }
        feedMat.shader = Shader.Find("Fuwa/PhotoAlbumFeed");
        var feedT = holder.transform.Find("Feed");
        if (feedT != null) Object.DestroyImmediate(feedT.gameObject);
        var feed = GameObject.CreatePrimitive(PrimitiveType.Quad);
        feed.name = "Feed";
        Object.DestroyImmediate(feed.GetComponent<Collider>());
        feed.transform.SetParent(holder.transform, false);
        feed.transform.localScale = Vector3.one * 0.2f;
        var feedR = feed.GetComponent<MeshRenderer>();
        feedR.sharedMaterial = feedMat;
        feedR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        feedR.receiveShadows = false;
        feedR.sortingOrder = 32767;   // 看板の文字（sortingOrder 1）より上に描く
        feedR.enabled = false;

        // 飾るボード
        var old = lobby.Find("PhotoGallery");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var board = new GameObject("PhotoGallery").transform;
        board.SetParent(lobby, false);
        Vector3 center = lobby.position;
        float a = AngleDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        board.position = new Vector3(center.x, 8.29f, center.z) + dir * Radius;
        board.rotation = Quaternion.LookRotation(dir, Vector3.up);   // 見る人（ロビーの中）から見て +z が奥
        // 写真館の島があればそこへ（FuwaPhotoStudioBuilder の BoardSpot）
        var studioSpot = lobby.Find("PhotoStudio/BoardSpot");
        if (studioSpot != null) { board.SetPositionAndRotation(studioSpot.position, studioSpot.rotation); board.localScale = Vector3.one * 0.85f; }   // 島が小さいので最速タイムの看板と同じ0.85倍

        var best = lobby.GetComponentsInChildren<Transform>(true).First(t => t.name == "BestTimes");   // 写真館の島へ引っ越していても見つける
        foreach (Transform leg in best.Cast<Transform>().Where(t => t.name == "Leg"))
        {
            var l = Object.Instantiate(leg.gameObject, board).transform;
            l.name = "Leg";
            l.localPosition = new Vector3(Mathf.Sign(leg.localPosition.x) * 1.55f, leg.localPosition.y, leg.localPosition.z);
            l.localRotation = leg.localRotation; l.localScale = leg.localScale;
        }
        var srcPanel = best.Find("Panel").GetComponent<SpriteRenderer>();
        var panel = Object.Instantiate(srcPanel.gameObject, board).GetComponent<SpriteRenderer>();
        panel.name = "Panel";
        panel.transform.localPosition = new Vector3(0f, 2.2f, 0.03f);
        panel.transform.localRotation = Quaternion.identity;
        panel.size = new Vector2(4.15f, 2.75f);

        var srcText = best.Find("Text").GetComponent<TextMeshPro>();
        var title = Text(board, "Title", srcText, new Vector3(0f, 3.25f, 0f), new Vector2(3.8f, 0.4f), 2.4f, TextAlignmentOptions.Center);
        title.text = "さっきの名場面";

        var photoMat = AssetDatabase.LoadAssetAtPath<Material>(PhotoMatPath);
        if (photoMat == null) { photoMat = new Material(Shader.Find("Unlit/Texture")); AssetDatabase.CreateAsset(photoMat, PhotoMatPath); }

        var frames = new Renderer[Cols * Rows];
        var caps = new TextMeshPro[Cols * Rows];
        var empties = new GameObject[Cols * Rows];
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
        {
            int i = r * Cols + c;
            var slot = Slot(board, i, new Vector3((c - 1) * 1.28f, 2.5f - r * 0.95f, 0f), srcPanel, photoMat, out frames[i], out empties[i]);
            caps[i] = Text(slot, "Caption", srcText, new Vector3(0f, -PhotoSize.y * 0.5f - 0.1f, 0f), new Vector2(1.25f, 0.16f), 1.0f, TextAlignmentOptions.Center);   // ポイント名（コース名は見出しに出す）
            caps[i].text = "";
        }

        // ゴールの結果発表の板（ゴールの奥の空に出す。どのコースでも使い回すので FuwaSystem の下）
        var gbOld = holder.transform.Find("GoalBoard");
        if (gbOld != null) Object.DestroyImmediate(gbOld.gameObject);
        var gb = new GameObject("GoalBoard").transform;
        gb.SetParent(holder.transform, false);
        var gPanel = Object.Instantiate(srcPanel.gameObject, gb).GetComponent<SpriteRenderer>();
        gPanel.name = "Panel";
        gPanel.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        gPanel.transform.localRotation = Quaternion.identity;
        gPanel.transform.localScale = Vector3.one;
        gPanel.size = new Vector2(4.15f, 1.95f);
        var gFrames = new Renderer[Cols * Rows];
        var gEmpties = new GameObject[Cols * Rows];
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
        {
            int i = r * Cols + c;
            Slot(gb, i, new Vector3((c - 1) * 1.28f, 0.42f - r * 0.84f, 0f), srcPanel, photoMat, out gFrames[i], out gEmpties[i]);
        }
        gb.localScale = Vector3.one * 1.7f;   // ゴールの手前から読める大きさ
        gb.gameObject.SetActive(false);

        // 本体の設定
        var records = Object.FindObjectsOfType<FuwaRecords>(true).FirstOrDefault();
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        var hud = Object.FindObjectsOfType<FuwaHud>(true).FirstOrDefault();
        album.shotCam = cam;
        album.frames = frames;
        album.captions = caps;
        album.emptyMarks = empties;
        album.titleText = title;
        album.goalBoard = gb;
        album.goalFrames = gFrames;
        album.goalEmptyMarks = gEmpties;
        album.feed = feed.transform;
        album.feedRenderer = feedR;
        album.language = lang;
        if (records != null)
        {
            // 最速タイムの看板にないチュートリアルも足す
            var starts = records.courseStarts.ToList(); var ja = records.courseNamesJa.ToList(); var en = records.courseNamesEn.ToList();
            var c0 = GameObject.Find("FuwaCourse0/Start/BallStart");
            if (c0 != null && !starts.Contains(c0.transform)) { starts.Insert(0, c0.transform); ja.Insert(0, "0 チュートリアル"); en.Insert(0, "0 Tutorial"); }
            album.courseStarts = starts.ToArray();
            album.courseNamesJa = ja.ToArray();
            album.courseNamesEn = en.ToArray();
        }
        album.hideWhileShooting = hud != null ? hud.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        UdonSharpEditorUtility.CopyProxyToUdon(album);
        EditorUtility.SetDirty(album);

        if (lang != null && lang.album != album) { lang.album = album; UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang); }
        // ゴール（ゴールの奥に結果発表の板を出してもらう）
        foreach (var goal in Object.FindObjectsOfType<FuwaGoal>(true))
            if (goal.album != album) { goal.album = album; UdonSharpEditorUtility.CopyProxyToUdon(goal); EditorUtility.SetDirty(goal); }
        // 銃（撃った瞬間を知らせてもらう。PlayerObject のもとの銃に入れれば全員の銃に入る）
        foreach (var gun in Object.FindObjectsOfType<FuwaBlower>(true))
            if (gun.photoAlbum != album) { gun.photoAlbum = album; UdonSharpEditorUtility.CopyProxyToUdon(gun); EditorUtility.SetDirty(gun); }
        // フォトポイント（すでにあるコースはそのまま。てじゃが動かした位置・構図を消さない）
        log.Add(FuwaPhotoPointBuilder.Run(album));
        log.Add("board at " + board.position);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        return string.Join("\n", log);
    }

    // 写真1枚ぶん（木の色の額縁・まだ写真がない時の紙・写真の面）
    static Transform Slot(Transform parent, int i, Vector3 pos, SpriteRenderer srcPanel, Material photoMat, out Renderer image, out GameObject empty)
    {
        var slot = new GameObject("Photo" + i).transform;
        slot.SetParent(parent, false);
        slot.localPosition = pos;
        var fr = Object.Instantiate(srcPanel.gameObject, slot).GetComponent<SpriteRenderer>();
        fr.name = "Frame";
        fr.transform.localPosition = new Vector3(0f, 0f, 0.018f);
        fr.transform.localRotation = Quaternion.identity;
        fr.transform.localScale = Vector3.one * 0.25f;
        fr.size = (PhotoSize + new Vector2(0.07f, 0.07f)) / 0.25f;
        fr.color = new Color(0.62f, 0.47f, 0.36f, 1f);
        var em = Object.Instantiate(srcPanel.gameObject, slot).GetComponent<SpriteRenderer>();
        em.name = "Empty";
        em.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        em.transform.localRotation = Quaternion.identity;
        em.transform.localScale = Vector3.one * 0.12f;
        em.size = PhotoSize / 0.12f;
        em.color = new Color(0.86f, 0.88f, 0.91f, 1f);
        empty = em.gameObject;
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Image";
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(slot, false);
        q.transform.localPosition = new Vector3(0f, 0f, 0.005f);
        q.transform.localScale = new Vector3(PhotoSize.x, PhotoSize.y, 1f);
        var qr = q.GetComponent<MeshRenderer>();
        qr.sharedMaterial = photoMat;
        qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        qr.receiveShadows = false;
        qr.enabled = false;
        image = qr;
        return slot;
    }

    static TextMeshPro Text(Transform parent, string name, TextMeshPro src, Vector3 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.font = src.font;
        t.fontSharedMaterial = src.fontSharedMaterial;
        t.color = src.color;
        t.fontSize = fontSize;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = size;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    static void EnsureProgram()
    {
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath) != null) return;
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = script;
        AssetDatabase.CreateAsset(prog, ProgramPath);
        AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
    }

    static void FixSyncNone(UdonSharpBehaviour b)
    {
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        if (ub == null || ub.SyncMethod == VRC.SDKBase.Networking.SyncType.None) return;
        var so = new SerializedObject(ub); so.FindProperty("_syncMethod").intValue = (int)VRC.SDKBase.Networking.SyncType.None; so.ApplyModifiedPropertiesWithoutUndo();
    }
}
