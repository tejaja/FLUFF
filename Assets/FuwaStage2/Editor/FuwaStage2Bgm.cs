using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2のコースのBGMを FuwaBgm につなぐ（各コースのビルダーが作り直すたびに呼ぶ）。
// FuwaSystem/BGM に曲の AudioSource（無ければ srcName を写して作る）を置き、tracks / starts（このコースの BallStart）/ trackGains / loopStarts / loopEnds を同じ番号でそろえる。
// loopEnd が 0 以下なら曲全体をループ。2026-11-09 よそで作った曲（イントロ＋ループ）に差しかえた時に、ビルダーが古い曲へ戻さないように共通化
public static class FuwaStage2Bgm
{
    public static void Register(Transform root, string trackName, string srcName, string clipPath, float gain, float loopStart, float loopEnd)
    {
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault(); if (!bgm) return;
        var start = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BallStart"); if (!start) return;
        var tr = bgm.transform.Find(trackName);
        if (!tr)
        {
            var src = bgm.transform.Find(srcName); if (!src) return;
            tr = Object.Instantiate(src.gameObject, bgm.transform, false).transform; tr.name = trackName;
            Undo.RegisterCreatedObjectUndo(tr.gameObject, "BGM " + trackName);
        }
        var a = tr.GetComponent<AudioSource>();
        Undo.RecordObject(a, "BGM " + trackName);
        a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath); EditorUtility.SetDirty(a);

        var so = new SerializedObject(bgm);
        var tracks = so.FindProperty("tracks");
        int idx = -1;
        for (int i = 0; i < tracks.arraySize; i++) if (tracks.GetArrayElementAtIndex(i).objectReferenceValue == a) idx = i;
        if (idx < 0) { idx = tracks.arraySize; tracks.arraySize++; tracks.GetArrayElementAtIndex(idx).objectReferenceValue = a; }
        var starts = so.FindProperty("starts");
        while (starts.arraySize <= idx) { starts.arraySize++; starts.GetArrayElementAtIndex(starts.arraySize - 1).objectReferenceValue = null; }
        starts.GetArrayElementAtIndex(idx).objectReferenceValue = start;
        SetFloat(so.FindProperty("trackGains"), idx, gain, 1f);
        SetFloat(so.FindProperty("loopStarts"), idx, loopStart, 0f);
        SetFloat(so.FindProperty("loopEnds"), idx, loopEnd, 0f);
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bgm);
    }

    static void SetFloat(SerializedProperty arr, int idx, float v, float fill)
    {
        while (arr.arraySize <= idx) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).floatValue = fill; }
        arr.GetArrayElementAtIndex(idx).floatValue = v;
    }
}
