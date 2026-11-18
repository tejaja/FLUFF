using UdonSharp;
using UnityEngine;

// 2026-11-18 てじゃ「見た目のやつも、そのコースに居ないときは写ってないときは止めても良いかも」：
//  見た目で動く仕掛け（火山岩・歯車・転がる岩など）を、そのコースにいない かつ 画面に映っていない間だけ止める（自分の画面だけ）。
//  このスクリプトは見た目（Renderer）と同じ物に置く（映った・映らなくなったの知らせは Renderer の物に来る）。影や鏡に映っている間も「映っている」扱い
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaVisibleGate : UdonSharpBehaviour
{
    public VRC.Udon.UdonBehaviour target;
    public FuwaCourseLod lod;
    [Tooltip("どのコースの物か（lod.fullRoots の番号）")]
    public int course = -1;
    [Tooltip("同じ仕掛けを見張るほかの番人（どれか1つでも映っていたら止めない。歯車のようにあちこちに見た目がある物）")]
    public FuwaVisibleGate[] group;
    private bool _visible = true;
    public bool IsVisible() { return _visible; }
    private bool AnyVisible()
    {
        if (_visible) return true;
        if (group != null) for (int i = 0; i < group.Length; i++) if (group[i] != null && group[i] != this && group[i].IsVisible()) return true;
        return false;
    }

    private bool InCourse() { return lod != null && lod.CurrentCourse() == course; }

    private void OnBecameVisible()
    {
        _visible = true;
        if (target != null && !target.enabled) target.enabled = true;
    }

    private void OnBecameInvisible()
    {
        _visible = false;
        if (InCourse() || AnyVisible()) return;
        Stop();
    }

    // コースが変わった時（FuwaCourseLod から）：そのコースに入ったら動かす、出た時に映っていなければ止める
    public void Recheck()
    {
        if (target == null) return;
        if (InCourse() || AnyVisible()) { if (!target.enabled) target.enabled = true; }
        else Stop();
    }

    private void Stop()
    {
        if (target == null || !target.enabled) return;
        target.SendCustomEvent("LodStop");
        target.enabled = false;
    }
}
