using UnityEditor;
using UnityEngine;

// 写真用の太陽（2026-10-18 てじゃ「縦看板の右上に、ちょっと見切れてる感じで太陽見せたいな。写真用に今だけおけないかな」）。
// Vket ブースの立体の太陽と同じ形（メッシュは VketWinter2026 の Assets/2562/Booth/Sun/Sun.asset のコピー）を、撮影カメラ（_PhotoSet/PhotoCamera）の子に置いて
// 画面の右上に少しはみ出して見せる。撮影セット（EditorOnly）の中なので、ワールドには入らない。カメラを動かしたら、もう一度「置く」で位置を合わせる
public static class FuwaPhotoSun
{
    const string Dir = "Assets/FuwaCourse/PhotoSet";
    // 画面の中の位置（0〜1、右上が 1,1）と、円の半径（画面の高さに対して）。縦看板（640×812）で見る
    public static Vector2 ViewPos = new Vector2(0.93f, 0.95f);
    public static float ViewRadius = 0.075f, Distance = 60f, Spin = 0.12f;
    const float PosterAspect = 640f / 812f;

    [MenuItem("FuwaCourse/撮影/写真用の太陽を置く（右上）")]
    public static void PlaceMenu() { Debug.Log(Place(PosterAspect)); }

    // 太陽だけを透明の背景に撮る（決まった写真にあとから重ねる用。photo_sun_overlay.py）
    public static string RenderSunOnly(int w, int h, string path)
    {
        var cam = Cam(); var t = cam ? cam.transform.Find("PhotoSun") : null; if (!t) return "太陽がない";
        const int Layer = 31; int oldLayer = t.gameObject.layer; t.gameObject.layer = Layer;
        int oldMask = cam.cullingMask; var oldClear = cam.clearFlags; var oldBg = cam.backgroundColor;
        try
        {
            cam.cullingMask = 1 << Layer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        }
        finally { cam.cullingMask = oldMask; cam.clearFlags = oldClear; cam.backgroundColor = oldBg; t.gameObject.layer = oldLayer; }
        return path;
    }

    [MenuItem("FuwaCourse/撮影/写真用の太陽を外す")]
    public static void RemoveMenu()
    {
        var cam = Cam(); var t = cam ? cam.transform.Find("PhotoSun") : null;
        if (t) Undo.DestroyObjectImmediate(t.gameObject);
    }

    static Camera Cam() { var root = GameObject.Find("_PhotoSet"); return root ? root.transform.Find("PhotoCamera")?.GetComponent<Camera>() : null; }

    public static string Place(float aspect)
    {
        var cam = Cam(); if (!cam) return "撮影セットがない";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/PhotoSun.mat");
        if (!mat) { mat = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Dir + "/FuwaPhotoSun.shader")); AssetDatabase.CreateAsset(mat, Dir + "/PhotoSun.mat"); }
        mat.SetFloat("_Spin", Spin);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/PhotoSun_Mesh.asset");
        var t = cam.transform.Find("PhotoSun");
        if (!t) { t = new GameObject("PhotoSun", typeof(MeshFilter), typeof(MeshRenderer)).transform; t.SetParent(cam.transform, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "photo sun"); }
        t.gameObject.tag = "EditorOnly";
        // aspect＝撮る画像の縦横比。看板は撮った画像のまん中を 640:812 に切るので、看板の中での位置・大きさになるように
        float th = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = th * aspect, tanV = Mathf.Min(th, tanH / PosterAspect); tanH = tanV * PosterAspect;
        var p = new Vector3((ViewPos.x * 2 - 1) * tanH * Distance, (ViewPos.y * 2 - 1) * tanV * Distance, Distance);
        t.localPosition = p;
        t.localRotation = Quaternion.LookRotation(-p.normalized, Vector3.up);   // 顔をカメラに向ける
        t.localScale = Vector3.one * ViewRadius * 2 * tanV * Distance;
        t.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = t.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        return "[写真用の太陽] 画面 " + ViewPos + "、半径 " + ViewRadius;
    }
}
