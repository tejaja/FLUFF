// 2026-11-27 てじゃ「コツの紙がちょっと見た目いまいち」→「A：コツだけ別の小さいメモ紙（うすい黄色・ちょっと傾けてピンどめ）」「ちょい左寄りに」
// チュートリアルの各湾（Bay_*/Challenge/Hint）のコツを、大きい看板の下に貼った小さいメモ紙にする。
//  紙（HintPaper）は文字の大きさ＋ふち、うすい黄色。全体を少し左へずらして少し傾け、上のまん中に赤いピン。
//  FuwaTutorialChallenge の作り直しでも最後に呼ぶ。何回押しても同じになる（ずらし・傾きは元の位置から計算）
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class FuwaTutorialHintNote
{
    const string NoteMatPath = "Assets/FuwaCourse/Materials/Sign_NotePaper.mat";
    const string PinMatPath = "Assets/FuwaCourse/Materials/Sign_NotePin.mat";
    const float ShiftLeft = 0.14f;   // m（看板から見て左へ）
    const float Tilt = 2.5f;         // 度（左回り）
    static readonly Color NoteColor = new Color(1f, 0.95f, 0.76f, 1f);
    static readonly Color PinColor = new Color(0.86f, 0.24f, 0.24f, 1f);

    [MenuItem("FuwaCourse/済み（押す前に相談）/チュートリアル：コツをメモ紙にする")]
    static void Menu() { Debug.Log(ApplyAll()); }

    public static string ApplyAll()
    {
        int n = 0;
        foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (!t.gameObject.scene.IsValid() || t.name != "Hint" || t.transform.parent == null || t.transform.parent.name != "Challenge") continue;
            if (Style(t)) n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[コツのメモ紙] " + n + " か所";
    }

    static Material Mat(string path, Color c, string from)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(from);
            m = new Material(src); AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_Color", c); EditorUtility.SetDirty(m);
        return m;
    }

    public static bool Style(TMP_Text hint)
    {
        var paper = hint.transform.Find("HintPaper"); if (paper == null) return false;
        var note = Mat(NoteMatPath, NoteColor, "Assets/FuwaCourse/Materials/Sign_Paper.mat");
        var pinMat = Mat(PinMatPath, PinColor, "Assets/FuwaCourse/Materials/Sign_Paper.mat");
        var ht = hint.transform;
        Undo.RecordObject(ht, "hint note");

        // 元の位置・向き（初めての時に覚える。2回目以降はそこから計算）
        var basePos = ht.Find("NoteBase");
        if (basePos == null)
        {
            basePos = new GameObject("NoteBase").transform; Undo.RegisterCreatedObjectUndo(basePos.gameObject, "hint note");
            basePos.SetParent(ht, false);
            basePos.SetParent(ht.parent, true);   // 親の下に元の位置で置いて、あとで中へ
            var bp = basePos.localPosition; var br = basePos.localRotation;
            basePos.SetParent(ht, false); basePos.localPosition = Vector3.zero; basePos.localRotation = Quaternion.identity;
            basePos.name = "NoteBase|" + bp.x.ToString("R") + "|" + bp.y.ToString("R") + "|" + bp.z.ToString("R") + "|" + br.x.ToString("R") + "|" + br.y.ToString("R") + "|" + br.z.ToString("R") + "|" + br.w.ToString("R");
        }
        var f = basePos.name.Split('|');
        if (f.Length == 8)
        {
            var p0 = new Vector3(float.Parse(f[1]), float.Parse(f[2]), float.Parse(f[3]));
            var r0 = new Quaternion(float.Parse(f[4]), float.Parse(f[5]), float.Parse(f[6]), float.Parse(f[7]));
            ht.localPosition = p0; ht.localRotation = r0;
            ht.position -= ht.right * ShiftLeft;
            ht.localRotation = r0 * Quaternion.Euler(0f, 0f, Tilt);
        }

        // 紙は文字の大きさ＋ふち
        bool wasActive = hint.gameObject.activeInHierarchy;
        var off = new System.Collections.Generic.List<GameObject>();
        for (var x = ht; x != null; x = x.parent) if (!x.gameObject.activeSelf) { off.Add(x.gameObject); x.gameObject.SetActive(true); }
        hint.ForceMeshUpdate();
        var b = hint.textBounds;
        foreach (var g in off) g.SetActive(false);
        Undo.RecordObject(paper, "hint note");
        float z = paper.localPosition.z;
        paper.localPosition = new Vector3(b.center.x, b.center.y, z);
        paper.localScale = new Vector3(b.size.x + 0.42f, b.size.y + 0.34f, 1f);
        var pr = paper.GetComponent<Renderer>(); Undo.RecordObject(pr, "hint note"); pr.sharedMaterial = note;

        // ピン
        var pin = ht.Find("NotePin");
        if (pin == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "NotePin"; Undo.RegisterCreatedObjectUndo(go, "hint note");
            go.transform.SetParent(ht, false); pin = go.transform;
        }
        pin.localPosition = new Vector3(b.center.x, b.max.y + 0.1f, z - 0.03f);
        pin.localRotation = Quaternion.identity;
        pin.localScale = new Vector3(0.12f, 0.12f, 0.06f);
        var r = pin.GetComponent<Renderer>(); r.sharedMaterial = pinMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pin.gameObject.isStatic = ht.gameObject.isStatic;
        return true;
    }
}
