using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// VRChatのリスポーン（メニューの手動リスポーン）をした時は、今いるモードのロビー（ひとり／たいせん）に戻す。
// 入口ロビーにいる時（まだモードを選んでいない）は、VRChatのスポーン地点＝入口ロビーのまま。
// 落ちた・トゲなどでやられた時は、各コースのFallCatcher／トゲ側でチェックポイントへ戻すので、ここは通らない。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaRespawnManager : UdonSharpBehaviour
{
    [Tooltip("リスポーンしたら、チュートリアルの看板の赤▼（FuwaReadMarks）をまた最初から出す")]
    public FuwaReadMarks readMarks;

    [Tooltip("最初のリスポーン地点（ロビー）")]
    public Transform lobbySpot;
    [Tooltip("リスポーンした時に倒したモンスターを復活させるため")]
    public FuwaBall ball;
    [Tooltip("（旧）手動リスポーンした時に使う「ロビーへ」ワープ。今は SetHomeWarp で決まった戻り先を使う")]
    public FuwaCourseWarp lobbyWarp;
    private FuwaCourseWarp _home;
    [Tooltip("入口ロビーに戻った時にエリアの表示を切り替えるため")]
    public FuwaAreaVisibility areaVisibility;

    // モードのロビーへのワープから呼ばれる（null＝入口ロビー）
    public void SetHomeWarp(FuwaCourseWarp warp)
    {
        _home = warp;
    }

    private Transform _current;
    private FuwaBlower _myBlower;

    // 自分の風の道具から呼ばれる
    public void RegisterMyBlower(FuwaBlower blower)
    {
        _myBlower = blower;
    }

    public FuwaBlower GetMyBlower() { return _myBlower; }

    // 自分の道具を指定の台へ呼び寄せる（持っている時は何もしない）。台の上では見た目の銃が浮かんで回る（FuwaGunShowcase）
    public void BringGun(Transform spot)
    {
        if (_myBlower != null) { _myBlower.Dock(spot); _myBlower.PlaySpawnSound(); }
    }

    private void Start()
    {
        _current = lobbySpot;
    }

    public void SetRespawnSpot(Transform spot)
    {
        if (spot != null) _current = spot;
    }

    // 手動リスポーンでロビーへ戻った時の立ち位置・向き（最後に入ったコースのポータルの方を向く。null＝ふつうのロビーの向き）
    private Transform _lobbyReturn;
    public void SetLobbyReturnSpot(Transform spot)
    {
        _lobbyReturn = spot;
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        // チュートリアルの赤▼のリセットは、チュートリアルから出た時（FuwaBall.StartCourse）にまとめた。手動リスポーンはロビーへのワープなので、そこで出たことになる
        // モードのロビーにいる（どこかのモードを選んだ）なら、そのロビーへ。入口ロビーならVRChatのスポーンのまま
        if (_home != null)
        {
            // 手動リスポーンは暗転なしで、すぐ戻す。コースに入ったことがあれば、そのコースのポータルの方を向かせる
            Transform face = _lobbyReturn;
            _home.DoWarp();
            if (face != null)
            {
                player.TeleportTo(face.position, face.rotation);
                _lobbyReturn = face;
                _current = face;
            }
        }
        else if (areaVisibility != null) areaVisibility.ShowArea(0);
        if (ball != null) ball.ReviveEnemies();
    }
}

