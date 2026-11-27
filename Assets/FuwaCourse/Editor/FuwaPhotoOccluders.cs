using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 「かってにスクショ」のカメラが、当たりのない物で手前をふさがれないように、その物を球で FuwaPhotoAlbum に入れる。
// 2026-11-26 てじゃ「横からの写真でチェックポイントが近いと、手前にチェックポイントが被ることがある」
//  → チェックポイントの光るキノコ（GlowMushrooms/Cluster*）。当たりは根元の丸太だけで、カメラの調べ方（当たり）では見えなかった
// 名場面アルバムの作り直し（FuwaPhotoAlbumBuilder）の最後でも呼ぶ。コースを直したらもう一度押す
public static class FuwaPhotoOccluders
{
    [MenuItem("FuwaCourse/済み（押す前に相談）/かってにスクショ：手前をふさぐ物を入れ直す")]
    static void Menu() { Debug.Log(Fill()); }

    const float WebWeight = 0.35f;   // 巣は透けるので軽め（巣のフォトポイントでは巣の手前も写したい）

    public static string Fill()
    {
        var album = Object.FindObjectsOfType<FuwaPhotoAlbum>(true).FirstOrDefault();
        if (album == null) return "[かってにスクショ] FuwaPhotoAlbum がない";
        var C = new List<Vector3>(); var R = new List<float>(); var W = new List<float>();
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (!t.name.StartsWith("Cluster") || t.parent == null || t.parent.name != "GlowMushrooms") continue;
            if (!t.gameObject.activeInHierarchy) continue;
            // かさと軸（根元の丸太は当たりがあるので入れなくていいが、まとめて1つの球に）
            var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && r.bounds.size.sqrMagnitude > 0.01f && !r.name.EndsWith("_Far")).ToList();
            if (rs.Count == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            C.Add(b.center);
            // 球は横の広がりで（縦長の箱をそのまま囲うと大きすぎて、どこにも置けなくなる）
            R.Add(Mathf.Max(b.extents.x, b.extents.z, b.extents.y * 0.8f)); W.Add(1f);
        }
        int mush = C.Count;
        // クモの巣（当たりがないか、通りぬけ用のトリガーだけ）。巣ごしの写真は糸で画面がうるさくなる（2026-11-26 試し撮りで、キノコをよけたら巣ごしになった）
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.gameObject.activeInHierarchy || !r.enabled || r.sharedMaterial == null || !r.sharedMaterial.shader.name.Contains("SpiderWeb")) continue;
            var b = r.bounds; if (b.size.sqrMagnitude < 0.04f) continue;
            C.Add(b.center); R.Add(Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 0.9f); W.Add(WebWeight);
        }
        // 2026-11-27 2–5 の大きさのカーテン（当たりなし）。てじゃ「カーテンがかぶりすぎ」。平らな板なので、長い方に沿って球を並べる
        int beforeCurtain = C.Count;
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.gameObject.activeInHierarchy || !r.enabled || r.sharedMaterial == null || r.sharedMaterial.shader.name != "Fuwa/SizeCurtain") continue;
            var b = r.bounds;
            Vector3 right = r.transform.right; float w = r.transform.lossyScale.x, h = r.transform.lossyScale.y;
            float rad = Mathf.Max(0.5f, Mathf.Min(w, h) * 0.5f);
            int n = Mathf.Max(1, Mathf.CeilToInt(w / (rad * 1.4f)));
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1) - 0.5f) * (w - rad);
                C.Add(b.center + right * t); R.Add(rad); W.Add(1f);
            }
        }
        Undo.RecordObject(album, "photo occluders");
        album.occluderCenters = C.ToArray(); album.occluderRadii = R.ToArray(); album.occluderWeights = W.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(album); EditorUtility.SetDirty(album);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(album.gameObject.scene);
        return "[かってにスクショ] 手前をふさぐ物 " + C.Count + " 個（光るキノコ " + mush + "・クモの巣 " + (C.Count - mush) + "）、半径 " + (R.Count > 0 ? R.Min().ToString("F2") + "〜" + R.Max().ToString("F2") : "-") + "m";
    }
}
