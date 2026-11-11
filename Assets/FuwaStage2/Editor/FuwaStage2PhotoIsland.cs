using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ステージ2のロビーにも撮影の島（2026-11-11 てじゃ「ステージ２にもスクショとカメラモニタがある島つくろう」「ステージ１の形のままでOK」「スクショ板はまだガワだけでいい」）
//  ステージ1のロビーの PhotoStudio（橋・島・モニター・カメラのボタン・映す人のボタン・VRChatカメラを入れる球）と PhotoGallery（スクショの板）を、
//  ステージ2のロビーの同じ所（ロビーから見て同じ位置・向き）に写す
//  ・モニターは FuwaFollowCam の extra（チルルームのモニターと同じしくみ）に足す。ボタンの文字・札も足す。日本語・英語も登録
//  ・スクショの板は見た目だけ（名場面アルバム FuwaPhotoAlbum にはつながない）
public static class FuwaStage2PhotoIsland
{
    const string StudioName = "PhotoStudio", GalleryName = "PhotoGallery";

    [MenuItem("FuwaCourse/ステージ2/ロビーの撮影の島を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var l1 = GameObject.Find("FuwaLobby")?.transform; var l2 = GameObject.Find("FuwaStage2/Lobby")?.transform;
        if (!l1 || !l2) return "ロビーがない";
        var studio = l1.Find(StudioName); var gallery = l1.Find(GalleryName);
        if (!studio || !gallery) return "ステージ1の撮影の島がない";
        foreach (var n in new[] { StudioName, GalleryName }) { var o = l2.Find(n); if (o) { Unregister(o); Object.DestroyImmediate(o.gameObject); } }

        var map = new Dictionary<Object, Object>();
        var s2 = Copy(studio, l2, map); var g2 = Copy(gallery, l2, map);
        // スクショの板：ガワだけ（写真は名場面アルバムが実行中に貼るので、写しただけだと空の枠のまま）
        FuwaCourse2_1Builder.FixCopied(s2, map);   // 中の U# の参照の付け替え・言語の登録（文字がステージ1の言語の一覧にあれば）
        FuwaCourse2_1Builder.FixCopied(g2, map);

        string log = RegisterMonitor(studio, s2) + " " + RegisterProximity(map) + " " + RegisterFarShadows(map);
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(l2.gameObject.scene);
        return "[ステージ2の撮影の島] " + log;
    }

    static Transform Copy(Transform src, Transform dstLobby, Dictionary<Object, Object> map)
    {
        var holder = new GameObject(src.name).transform; holder.SetParent(dstLobby, false);
        holder.localPosition = src.localPosition; holder.localRotation = src.localRotation; holder.localScale = src.localScale;
        FuwaCourse2_1Builder.CopyChildren(src, holder, null, map);
        map[src] = holder; map[src.gameObject] = holder.gameObject;
        return holder;
    }

    static string Rel(Transform t, Transform top) { return t == top ? "" : (t.parent == top ? t.name : Rel(t.parent, top) + "/" + t.name); }
    static Transform Mirror(Transform t, Transform srcTop, Transform dstTop) => t == null || !t.IsChildOf(srcTop) ? null : (t == srcTop ? dstTop : dstTop.Find(Rel(t, srcTop)));
    static void Add(SerializedProperty p, Object v) { p.arraySize++; p.GetArrayElementAtIndex(p.arraySize - 1).objectReferenceValue = v; }

    // モニター・球・ボタンの文字を FuwaFollowCam に足す（写真館の分と同じ並びで）
    static string RegisterMonitor(Transform studio, Transform s2)
    {
        var fc = Object.FindObjectsOfType<FuwaFollowCam>(true).FirstOrDefault(); if (!fc) return "（FuwaFollowCam がない）";
        var so = new SerializedObject(fc);
        var scr = Mirror(fc.monitorScreen ? fc.monitorScreen.transform : null, studio, s2);
        var off = Mirror(fc.monitorOffMark ? fc.monitorOffMark.transform : null, studio, s2);
        var nm = Mirror(fc.monitorName ? fc.monitorName.transform : null, studio, s2);
        var zt = Mirror(fc.zone, studio, s2);
        var zr = Mirror(fc.zoneRenderer ? fc.zoneRenderer.transform : null, studio, s2);
        Add(so.FindProperty("extraScreens"), scr ? scr.GetComponent<Renderer>() : null);
        Add(so.FindProperty("extraOffMarks"), off ? off.gameObject : null);
        Add(so.FindProperty("extraNames"), nm ? nm.GetComponent<TMPro.TextMeshPro>() : null);
        Add(so.FindProperty("extraZones"), zt);
        Add(so.FindProperty("extraZoneRenderers"), zr ? zr.GetComponent<Renderer>() : null);
        var mt = so.FindProperty("modeTexts"); var mp = so.FindProperty("modePills"); int n0 = mt.arraySize, added = 0;
        for (int i = 0; i < n0; i++)
        {
            var t = mt.GetArrayElementAtIndex(i).objectReferenceValue as Component; var ct = t ? Mirror(t.transform, studio, s2) : null; if (!ct) continue;
            Add(mt, ct.GetComponent<TMPro.TextMeshProUGUI>());
            var pill = i < mp.arraySize ? mp.GetArrayElementAtIndex(i).objectReferenceValue as Component : null; var cp = pill ? Mirror(pill.transform, studio, s2) : null;
            mp.arraySize = mt.arraySize; mp.GetArrayElementAtIndex(mp.arraySize - 1).objectReferenceValue = cp ? cp.GetComponent<UnityEngine.UI.Image>() : null;
            added++;
        }
        var tt = so.FindProperty("targetTexts"); int t0 = tt.arraySize;
        for (int i = 0; i < t0; i++)
        {
            var t = tt.GetArrayElementAtIndex(i).objectReferenceValue as Component; var ct = t ? Mirror(t.transform, studio, s2) : null; if (!ct) continue;
            Add(tt, ct.GetComponent<TMPro.TextMeshProUGUI>()); added++;
        }
        so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(fc); EditorUtility.SetDirty(fc);
        // ボタンの onClick は FuwaFollowCam を呼んでいるので写したままで動く。UI の文字の日本語・英語（uiTexts）も写す
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(); int la = 0;
        if (lang)
        {
            var lso = new SerializedObject(lang);
            foreach (var t in s2.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                var srcT = studio.Find(Rel(t.transform, s2)); if (!srcT) continue;
                int i = System.Array.IndexOf(lang.uiTexts, srcT.GetComponent<TMPro.TextMeshProUGUI>()); if (i < 0) continue;
                foreach (var pn in new[] { "uiTexts", "uiJa", "uiEn" }) lso.FindProperty(pn).arraySize++;
                int k = lso.FindProperty("uiTexts").arraySize - 1;
                lso.FindProperty("uiTexts").GetArrayElementAtIndex(k).objectReferenceValue = t;
                lso.FindProperty("uiJa").GetArrayElementAtIndex(k).stringValue = lang.uiJa[i];
                lso.FindProperty("uiEn").GetArrayElementAtIndex(k).stringValue = lang.uiEn[i];
                la++;
            }
            lso.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang);
        }
        return "モニター（文字 " + added + "、言語 +" + la + "）";
    }

    // 近づいた時だけボタンを押せるようにする一覧（FuwaUiProximity）に、写したボタンの当たりも足す（距離も元と同じ）
    static string RegisterProximity(Dictionary<Object, Object> map)
    {
        var up = Object.FindObjectsOfType<FuwaUiProximity>(true).FirstOrDefault(); if (!up) return "";
        var so = new SerializedObject(up); var cols = so.FindProperty("uiColliders"); var rs = so.FindProperty("ranges"); int n0 = cols.arraySize, n = 0;
        for (int i = 0; i < n0; i++)
        {
            var c = cols.GetArrayElementAtIndex(i).objectReferenceValue; if (!c || !map.TryGetValue(c, out var to)) continue;
            float r = i < rs.arraySize ? rs.GetArrayElementAtIndex(i).floatValue : 0f;
            Add(cols, to); if (rs.arraySize > 0 || r != 0f) { rs.arraySize = cols.arraySize; rs.GetArrayElementAtIndex(rs.arraySize - 1).floatValue = r; }
            n++;
        }
        so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(up); EditorUtility.SetDirty(up);
        return "ボタンの当たり +" + n;
    }

    // 遠くでは影を切る一覧（FuwaFarShadows）にも足す
    static string RegisterFarShadows(Dictionary<Object, Object> map)
    {
        var fs = Object.FindObjectsOfType<FuwaFarShadows>(true).FirstOrDefault(); if (!fs) return "";
        var so = new SerializedObject(fs); var p = so.FindProperty("targets"); if (p == null || !p.isArray) return "";
        int n0 = p.arraySize, n = 0;
        for (int i = 0; i < n0; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o && map.TryGetValue(o, out var to)) { Add(p, to); n++; } }
        so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(fs); EditorUtility.SetDirty(fs);
        return "影 +" + n;
    }

    // 作り直しの前に：前に写した物を一覧から外す
    static void Unregister(Transform oldRoot)
    {
        bool Under(Object o) => o != null && (o is Component c ? c.transform.IsChildOf(oldRoot) : o is GameObject g && g.transform.IsChildOf(oldRoot));
        void Clean(UdonSharp.UdonSharpBehaviour b, params string[][] groups)
        {
            if (!b) return;
            var so = new SerializedObject(b);
            foreach (var grp in groups)
            {
                var first = so.FindProperty(grp[0]); if (first == null) continue;
                for (int i = first.arraySize - 1; i >= 0; i--)
                {
                    if (!Under(first.GetArrayElementAtIndex(i).objectReferenceValue)) continue;
                    foreach (var pn in grp)
                    {
                        var p = so.FindProperty(pn); if (p == null || i >= p.arraySize) continue;
                        if (p.propertyType == SerializedPropertyType.Generic && p.GetArrayElementAtIndex(i).propertyType == SerializedPropertyType.ObjectReference) p.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        p.DeleteArrayElementAtIndex(i);
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b);
        }
        Clean(Object.FindObjectsOfType<FuwaFollowCam>(true).FirstOrDefault(), new[] { "modeTexts", "modePills" }, new[] { "extraZones", "extraZoneRenderers" }, new[] { "extraScreens" }, new[] { "extraOffMarks" }, new[] { "extraNames" }, new[] { "targetTexts" });
        Clean(Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(), new[] { "uiTexts", "uiJa", "uiEn" }, new[] { "texts", "ja", "en" });
        Clean(Object.FindObjectsOfType<FuwaUiProximity>(true).FirstOrDefault(), new[] { "uiColliders", "ranges" });
        Clean(Object.FindObjectsOfType<FuwaFarShadows>(true).FirstOrDefault(), new[] { "targets" });
    }
}
