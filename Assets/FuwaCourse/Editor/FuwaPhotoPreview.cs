using UnityEditor;
using UnityEngine;

// 撮影セットのプレビュー窓（2026-10-18 てじゃ「縦看板、目安がわからなくてムズい」）。
// ・撮影カメラ（_PhotoSet/PhotoCamera）の見え方を、撮る時と同じ縦横の比で表示する
// ・「Scene ビューに追従」をオンにすると、Scene ビューを動かすだけで撮影カメラがついてくる（位置・向き・画角）
// ・目安の線：サムネは VRChat で切られる枠（ゲーム内＝赤、ホバー＝緑、ポータル＝青の丸、右上の PC/Quest の印）、
//   縦看板はブースの看板の比（640×812）で、下のロゴ・文字の帯
// ・「撮る」で今の形のまま書き出す（ぼかしつき）
public class FuwaPhotoPreview : EditorWindow
{
    enum Mode { Thumbnail, Poster }
    Mode mode = Mode.Thumbnail;
    bool follow = false;   // 開いた時は今の撮影カメラのまま（いきなり Scene ビューに飛ばないように）
    RenderTexture rt;

    [MenuItem("FuwaCourse/撮影/プレビュー窓（目安つき）")]
    static void Open() { GetWindow<FuwaPhotoPreview>("撮影プレビュー").Show(); }

    void OnEnable() { EditorApplication.update += Tick; }
    void OnDisable() { EditorApplication.update -= Tick; if (rt) { rt.Release(); DestroyImmediate(rt); } }

    Camera Cam()
    {
        var root = GameObject.Find("_PhotoSet");
        return root ? root.transform.Find("PhotoCamera")?.GetComponent<Camera>() : null;
    }

    Vector2Int Size() => mode == Mode.Thumbnail ? new Vector2Int(1200, 900) : new Vector2Int(640, 812);

    void Tick()
    {
        var cam = Cam(); if (!cam) return;
        if (follow && SceneView.lastActiveSceneView != null)
        {
            var sv = SceneView.lastActiveSceneView.camera;
            if (cam.transform.position != sv.transform.position || cam.transform.rotation != sv.transform.rotation || !Mathf.Approximately(cam.fieldOfView, SceneView.lastActiveSceneView.cameraSettings.fieldOfView))
            {
                cam.transform.SetPositionAndRotation(sv.transform.position, sv.transform.rotation);
                cam.fieldOfView = SceneView.lastActiveSceneView.cameraSettings.fieldOfView;
                EditorUtility.SetDirty(cam.transform); EditorUtility.SetDirty(cam);
            }
        }
        Repaint();
    }

    void OnGUI()
    {
        var cam = Cam();
        using (new EditorGUILayout.HorizontalScope())
        {
            mode = (Mode)GUILayout.Toolbar((int)mode, new[] { "サムネ（4:3）", "縦看板（640×812）" });
            follow = GUILayout.Toggle(follow, "Scene ビューに追従", "Button", GUILayout.Width(130));
            // ニタ（と持っている銃）を出す・隠す（2026-10-18 てじゃ「アバターが写ってるのが過剰かも」：ワールドの看板はふわふわと景色だけで）
            // 体だけ消す（銃は残す。てじゃ「銃まできえちゃう」）＝見えない手で構えた銃、主観の絵にも
            var nita = GameObject.Find("_PhotoSet")?.transform.Find("Booth/Nita");
            if (nita != null)
            {
                var body = nita.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool shown = body.Length > 0 && body[0].enabled;
                bool on = GUILayout.Toggle(shown, "ニタ", "Button", GUILayout.Width(50));
                if (on != shown) foreach (var r in body) { Undo.RecordObject(r, "photo nita"); r.enabled = on; }
            }
        }
        if (!cam) { EditorGUILayout.HelpBox("撮影セットがない（FuwaCourse/撮影/撮影セットを作る）", MessageType.Info); return; }
        // 縦看板は画角を縦で合わせる（Scene ビューの画角は縦の画角）
        var size = Size();
        float scale = Mathf.Min((position.width - 10) / size.x, (position.height - 70) / size.y);
        var rect = new Rect((position.width - size.x * scale) / 2, 28, size.x * scale, size.y * scale);
        int pw = Mathf.Max(64, Mathf.RoundToInt(rect.width)), ph = Mathf.Max(64, Mathf.RoundToInt(rect.height));
        if (rt == null || rt.width != pw || rt.height != ph) { if (rt) { rt.Release(); DestroyImmediate(rt); } rt = new RenderTexture(pw, ph, 24) { antiAliasing = 4 }; }
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        GUI.DrawTexture(rect, rt, ScaleMode.StretchToFill, false);
        Handles.BeginGUI();
        if (mode == Mode.Thumbnail) DrawThumbGuides(rect); else DrawPosterGuides(rect);
        Handles.EndGUI();

        GUILayout.Space(rect.height + 8);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("撮る（ぼかしつき・8枚）"))
            {
                string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var s = mode == Mode.Thumbnail ? new Vector2Int(2400, 1800) : new Vector2Int(1280, 1624);
                for (int i = 0; i < 8; i++) Debug.Log(FuwaPhotoSet.ShootDof(s.x, s.y, "FLUFF_" + stamp + (mode == Mode.Thumbnail ? "_thumb_" : "_poster_") + (i + 1), 2.5f + i * 0.37f));
            }
            if (GUILayout.Button("撮る（ぼかしなし・1枚）"))
            {
                var s = mode == Mode.Thumbnail ? new Vector2Int(2400, 1800) : new Vector2Int(1280, 1624);
                Debug.Log(FuwaPhotoSet.Shoot(s.x, s.y, "FLUFF_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + (mode == Mode.Thumbnail ? "_thumb" : "_poster")));
            }
        }
    }

    // 1200×900 の画像の座標（VRChat の公式テンプレートの枠）を窓の中へ
    static Vector2 P(Rect r, float x, float y, float w, float h) => new Vector2(r.x + x / w * r.width, r.y + y / h * r.height);

    static void Box(Rect r, float x0, float y0, float x1, float y1, Color c, float w, float h)
    {
        Handles.color = c;
        var a = P(r, x0, y0, w, h); var b = P(r, x1, y0, w, h); var cc = P(r, x1, y1, w, h); var d = P(r, x0, y1, w, h);
        Handles.DrawAAPolyLine(3f, a, b, cc, d, a);
    }

    static void DrawThumbGuides(Rect r)
    {
        Box(r, 2, 102, 1198, 798, new Color(1, 0.2f, 0.2f, 0.9f), 1200, 900);    // ゲーム内のサムネ
        Box(r, 36, 124, 1166, 774, new Color(0.2f, 1, 0.2f, 0.9f), 1200, 900);   // ホバーで少し寄る
        // 右上の PC/Quest の印（ここに大事な物を置かない）
        Handles.color = new Color(0.2f, 0.9f, 0.2f, 0.35f);
        Handles.DrawAAConvexPolygon(P(r, 905, 124, 1200, 900), P(r, 1166, 124, 1200, 900), P(r, 1166, 300, 1200, 900), P(r, 905, 300, 1200, 900));
        // ポータルの丸（見えるのはこの中）
        Handles.color = new Color(0.2f, 0.5f, 1f, 0.9f);
        var pts = new Vector3[65];
        for (int i = 0; i <= 64; i++) { float a = i * Mathf.PI * 2 / 64; pts[i] = P(r, 600 + Mathf.Cos(a) * 440, 450 + Mathf.Sin(a) * 400, 1200, 900); }
        Handles.DrawAAPolyLine(3f, pts);
    }

    static void DrawPosterGuides(Rect r)
    {
        // 下の帯：ロゴ（FLUFF）と「Game world - coming soon」を重ねる所。顔や大事な物はこの上に
        Handles.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        Handles.DrawAAConvexPolygon(P(r, 0, 600, 640, 812), P(r, 640, 600, 640, 812), P(r, 640, 812, 640, 812), P(r, 0, 812, 640, 812));
        Box(r, 24, 24, 616, 788, new Color(1, 1, 1, 0.6f), 640, 812);   // 看板の枠で少し隠れる
        var st = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(1f, 0.95f, 0.6f) } };
        GUI.Label(new Rect(P(r, 20, 610, 640, 812), new Vector2(300, 20)), "ロゴ・文字の帯", st);
    }
}
