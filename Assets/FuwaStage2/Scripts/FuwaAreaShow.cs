using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

// 2026-11-18 てじゃ「Pickupのこっててもおもろいかも」「全員にみえていいよ」：おまけ本島のボード（文字・ステージ1・ステージ2）を、みんなで持って動かせる（VRCObjectSync）。
//  同期する物は隠れるエリア（FuwaBonusHub）に置けない（入り直した人に見えなくなる罠）ので FuwaSystem に置き、自分が area にいない間は
//  見た目・当たり・持つのを自分で止める（GameObject は出したまま＝同期は続く）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaAreaShow : UdonSharpBehaviour
{
    public FuwaAreaVisibility areaVisibility;
    public int area = 4;
    public Renderer[] renderers;
    public Collider[] colliders;
    public VRCPickup pickup;
    [Tooltip("出した時に書き直す（自己ベストのボード。なくてもOK）")]
    public FuwaThanksBoard board;
    public float interval = 0.25f;
    private int _shown = -1;
    private float _next;

    private void Start() { Apply(false); _shown = 0; }

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + interval;
        bool on = areaVisibility != null && areaVisibility.GetArea() == area;
        int s = on ? 1 : 0;
        if (s == _shown) return;
        _shown = s;
        Apply(on);
        if (on && board != null) board.Render();
    }

    private void Apply(bool on)
    {
        if (renderers != null) for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = on;
        if (colliders != null) for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = on;
        if (pickup != null) { if (!on) pickup.Drop(); pickup.pickupable = on; }
    }
}
