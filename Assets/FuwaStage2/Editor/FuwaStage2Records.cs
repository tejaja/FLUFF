using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2のロビーの「さいそくタイム」の看板（2026-10-28 てじゃ「ステージ２ロビーに、ステージ２用のを置こう」）。
// ステージ1の看板（FuwaLobby/BestTimes）と記録係（FuwaSystem/Records）を写して、ステージ2の6コースを並べる。
// 置き場所はステージ1と同じ（ステージ1の看板は「つぎのしま」ポータルの上、こっちは「ステージ1へもどる」ポータルの上）。
// ゴールのタイムと言語の切り替えは、ステージ1の記録係の next からこっちへ回る。まだ無いコースは スタートなし（記録は --）
public static class FuwaStage2Records
{
    const float Drop = 170f;   // ステージ2のロビーはステージ1のロビーの真下
    const float RowH = 0.48f;  // 看板の1コース分の高さ（タイムの行＋名前の行＋すき間）

    [MenuItem("FuwaCourse/ステージ2/さいそくタイムの看板を作り直す")]
    public static void Build()
    {
        var board1 = GameObject.Find("FuwaLobby/BestTimes"); var rec1 = Object.FindObjectsOfType<FuwaRecords>(true).FirstOrDefault(r => r.name == "Records");
        var lobby2 = GameObject.Find("FuwaStage2/Lobby");
        if (!board1 || !rec1 || !lobby2) { Debug.LogError("[ステージ2] さいそくタイム：元の看板・記録係・ステージ2のロビーのどれかがない"); return; }

        // 看板
        var old = lobby2.transform.Find("BestTimes2"); if (old) Object.DestroyImmediate(old.gameObject);
        var board = Object.Instantiate(board1, lobby2.transform); board.name = "BestTimes2";
        board.transform.SetPositionAndRotation(board1.transform.position - Vector3.up * Drop, board1.transform.rotation);
        board.transform.localScale = board1.transform.localScale;

        // 記録係（同期するので FuwaSystem の下に）
        var oldR = rec1.transform.parent.Find("Records2"); if (oldR) Object.DestroyImmediate(oldR.gameObject);
        var rec = Object.Instantiate(rec1.gameObject, rec1.transform.parent).GetComponent<FuwaRecords>(); rec.name = "Records2";

        int n = FuwaStage2Builder.CourseJa.Length;
        var starts = new Transform[n]; var ja = new string[n]; var en = new string[n]; var keys = new string[n]; var vers = new int[n];
        for (int i = 0; i < n; i++)
        {
            var c = GameObject.Find("FuwaCourse2_" + (i + 1));
            starts[i] = c ? c.transform.Find("Start/BallStart") : null;
            ja[i] = "2–" + (i + 1) + " " + FuwaStage2Builder.CourseJa[i];
            en[i] = "2-" + (i + 1) + " " + FuwaStage2Builder.CourseEn[i];
            keys[i] = "s2_" + (i + 1); vers[i] = 1;
        }
        // ステージ1は5コース、こっちは6コース：のびた行の分だけ板・ふち・文字の枠を上へのばす（下のはしはそのまま。題が板の上にはみ出た）
        float dh = (n - rec1.courseStarts.Length) * RowH;
        if (dh > 0f)
        {
            foreach (var nm in new[] { "Panel", "Border", "Text" })
            {
                var t = board.transform.Find(nm); if (!t) continue;
                t.localPosition += Vector3.up * dh * 0.5f;
                var sr = t.GetComponent<SpriteRenderer>(); if (sr) sr.size += new Vector2(0f, dh / t.localScale.y);
                var rt = t as RectTransform; if (rt) rt.sizeDelta += new Vector2(0f, dh);
            }
        }
        rec.courseStarts = starts; rec.courseNamesJa = ja; rec.courseNamesEn = en; rec.courseKeys = keys; rec.courseVersions = vers;
        rec.boardText = board.GetComponentInChildren<TMPro.TextMeshPro>(true);
        rec.playground = null;   // おもちゃの解放はステージ1のコースの番号なので、こっちからはしない
        rec.next = null;
        EditorUtility.SetDirty(rec);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(rec);

        rec.Render(); EditorUtility.SetDirty(rec.boardText);   // エディタでも並びが見えるように（実機では Start で書き直す）
        rec1.next = rec; EditorUtility.SetDirty(rec1);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(rec1);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(board.scene);
        Debug.Log("[ステージ2] さいそくタイムの看板を作り直した");
    }
}
