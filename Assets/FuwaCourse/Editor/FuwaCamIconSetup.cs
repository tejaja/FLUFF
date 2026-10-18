using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// CAMボタン（追っかけカメラの切り替え）の「CAM」の文字を、カメラのピクトグラムに置き換える（2026-10-11 てじゃ「📷みたいなピクトグラムにできないかな」）
// 絵は Tools/ui/make_camera_icon.py で作った Icon_Camera.png（白、まわり透明）。シーンの CamButton 全部（ハブ・たいせんロビー・写真館）に。何度実行しても同じ
public static class FuwaCamIconSetup
{
    const string IconPath = "Assets/FuwaCourse/Textures/Icon_Video.png";   // 2026-10-11 📷→📹（モニターのアングル切り替えなので。てじゃ）

    [MenuItem("FuwaCourse/済み（押す前に相談）/CAMボタンをカメラの絵にする")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        if (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled || !imp.alphaIsTransparency)
        {
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        int n = 0;
        foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "CamButton" && t.Find("Canvas") != null))
        {
            var seg = t.Find("Canvas/Button/Seg"); if (seg == null) continue;
            var head = seg.Find("Head"); if (head == null) continue;
            var icon = seg.Find("Icon");
            if (icon == null)
            {
                icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
                icon.SetParent(seg, false);
                icon.SetSiblingIndex(head.GetSiblingIndex() + 1);
            }
            var hr = (RectTransform)head; var ir = (RectTransform)icon;
            ir.anchorMin = hr.anchorMin; ir.anchorMax = hr.anchorMax; ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = hr.anchoredPosition + new Vector2(10f, 0f);
            ir.sizeDelta = new Vector2(250f, 195f);   // 絵の縦横比 512:400
            ir.localRotation = Quaternion.identity; ir.localScale = Vector3.one;
            var img = icon.GetComponent<Image>();
            img.sprite = sprite; img.color = Color.white; img.raycastTarget = false; img.preserveAspect = true;
            head.gameObject.SetActive(false);   // 文字の「CAM」は残しておいて隠すだけ
            EditorUtility.SetDirty(icon.gameObject); EditorUtility.SetDirty(head.gameObject);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "cam icon: " + n + " buttons";
    }
}
