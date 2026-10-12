using UdonSharp;
using UnityEngine;

// 名場面アルバムのフォトポイント（自分にだけ、同期なし）。2026-10-11 てじゃ「各コースごとに、数カ所フォトポイントを作るのがいい」
// 自分のふわふわがこの区画（トリガー）に入ったら、「ふわふわ主役」と「自分に寄った」の2枚を撮ってもらう（FuwaPhotoAlbum）。
// camPose は撮る側（左右）の好みと、どこにもカメラを置けない時の予備。
// 1回の挑戦で1回まで（スタートし直したらまた撮れる）。
// 位置・構図はシーンで自由に動かしてよい（作り直しメニューは、すでにあるフォトポイントを動かさない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPhotoPoint : UdonSharpBehaviour
{
    public FuwaPhotoAlbum album;
    [Tooltip("カメラの位置と、だいたいの向き（撮る時はふわふわの方へ少し向け直す）")]
    public Transform camPose;
    [Tooltip("画角（度）")]
    public float fieldOfView = 45f;
    [Tooltip("コースの中の順番（0〜2）。撮る角度とボードの並び（ポイント1の2枚、ポイント2の…）に使う")]
    public int order;
    [Tooltip("ボードの写真の下に出す名前")]
    public string nameJa = "";
    public string nameEn = "";
    [Tooltip("このポイントで撮るのは1枚目（遠く/近く）か")]
    public bool takeFirst = true;
    [Tooltip("このポイントで撮るのは2枚目（撃った瞬間/近く）か。1枚目と2枚目を別の場所で撮りたい時は、同じ order のポイントを2つ置いて片方ずつ（2026-10-12 まもののす：1枚目はスタート、2枚目は巣の中）")]
    public bool takeSecond = true;

    private float _shotRun = -1f;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || album == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null || !ball.IsInRun()) return;   // タイムを計らない道（チュートリアルの本番）でも撮る
        float run = ball.GetRunStartTime();
        if (run == _shotRun) return;
        _shotRun = run;
        album.OnPhotoPoint(this, ball);
    }
}
