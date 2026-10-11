using UdonSharp;
using UnityEngine;

// 名場面アルバムのフォトポイント（自分にだけ、同期なし）。2026-10-11 てじゃ「各コースごとに、数カ所フォトポイントを作るのがいい」
// 自分のふわふわがこの区画（トリガー）に入ったら、camPose の位置から1枚撮ってもらう（FuwaPhotoAlbum）。
// 1回の挑戦で1枚まで（スタートし直したらまた撮れる）。
// 位置・構図はシーンで自由に動かしてよい（作り直しメニューは、すでにあるフォトポイントを動かさない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPhotoPoint : UdonSharpBehaviour
{
    public FuwaPhotoAlbum album;
    [Tooltip("カメラの位置と、だいたいの向き（撮る時はふわふわの方へ少し向け直す）")]
    public Transform camPose;
    [Tooltip("画角（度）")]
    public float fieldOfView = 45f;
    [Tooltip("ボードの写真の下に出す名前")]
    public string nameJa = "";
    public string nameEn = "";

    private float _shotRun = -1f;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || album == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null || !ball.IsRunning()) return;
        float run = ball.GetRunStartTime();
        if (run == _shotRun) return;
        _shotRun = run;
        album.OnPhotoPoint(this, ball);
    }
}
