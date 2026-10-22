using System.Linq;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 「◀ ○○ ▶」のボタン（おっかけカメラの CamButton・TargetButton、がしつ の QualityButton）で、
// レーザー／カーソルが当たっている側の矢印だけ色を濃くする（2026-10-22 てじゃ「レーザーあてたときだけ対応した◁が色かわるとかできないかな」）。
// 左の押す所（BackButton / PrevButton）と右の押す所（Button / NextButton）に EventTrigger を付けて、FuwaArrowHoverFx を呼ぶ。何度実行しても同じ。
// TargetButton は CamButton の複製、あそびばのモニターは写真館の複製なので、作り直しても付いたまま来る。QualityButton は FuwaQualitySwitchSetup から呼ぶ
public static class FuwaArrowHover
{
    [MenuItem("FuwaCourse/済み（押す前に相談）/◀▶ボタンの当たっている側の矢印に色をつける")]
    public static void Menu() { Debug.Log(ApplyAll()); }

    public static string ApplyAll()
    {
        int n = 0;
        foreach (var arrow in Object.FindObjectsOfType<TextMeshProUGUI>(true).Where(t => t.name == "ArrowL" && t.gameObject.scene.IsValid()))
        {
            var canvas = arrow.GetComponentInParent<Canvas>(true);
            if (canvas != null && Apply(canvas.transform)) n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[◀▶の色] " + n + " 個";
    }

    public static bool Apply(Transform canvas)
    {
        var texts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        var al = texts.FirstOrDefault(t => t.name == "ArrowL"); var ar = texts.FirstOrDefault(t => t.name == "ArrowR");
        var buttons = canvas.GetComponentsInChildren<Button>(true);
        var prev = buttons.FirstOrDefault(b => b.name == "BackButton" || b.name == "PrevButton");
        var next = buttons.FirstOrDefault(b => b.name == "NextButton") ?? buttons.FirstOrDefault(b => b.name == "Button");
        if (al == null || ar == null || prev == null || next == null) return false;

        var fx = canvas.GetComponent<FuwaArrowHoverFx>();
        if (fx == null) fx = UdonSharpUndo.AddComponent<FuwaArrowHoverFx>(canvas.gameObject);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(fx);
        Color baseCol = al.color;
        Color.RGBToHSV(baseCol, out var h, out var s, out var v);
        fx.arrowL = al; fx.arrowR = ar;
        fx.normalColor = baseCol;
        fx.hoverColor = Color.HSVToRGB(h, Mathf.Min(1f, s * 1.5f + 0.15f), v * 0.7f);   // 当たっている間：濃く
        UdonSharpEditorUtility.CopyProxyToUdon(fx);
        ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); EditorUtility.SetDirty(fx);

        Trigger(prev.gameObject, ub, "EnterL", "ExitL");
        Trigger(next.gameObject, ub, "EnterR", "ExitR");
        return true;
    }

    static void Trigger(GameObject go, VRC.Udon.UdonBehaviour ub, string enter, string exit)
    {
        var et = go.GetComponent<EventTrigger>() ?? Undo.AddComponent<EventTrigger>(go);
        Undo.RecordObject(et, "arrow hover");
        et.triggers.Clear();
        foreach (var (type, ev) in new[] { (EventTriggerType.PointerEnter, enter), (EventTriggerType.PointerExit, exit) })
        {
            var e = new EventTrigger.Entry { eventID = type };
            UnityEventTools.AddStringPersistentListener(e.callback, ub.SendCustomEvent, ev);
            et.triggers.Add(e);
        }
        EditorUtility.SetDirty(et);
    }
}
