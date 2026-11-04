using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// 2–3 こおりのみち：氷の道の上で人が滑る（2026-11-03、氷のテスト場 FuwaIceFloor の mode 4 をコース用にしたもの。てじゃ実機で「Cいいね」）。
// ・足の下を下向きのレイで見て、当たったのが ice の当たりなら氷の上。曲がった道・坂でも使える
// ・動かし方：毎フレームの小さい TeleportTo（部屋ごと平行に動かす）。SetVelocity だと VRChat が浮いている扱いにして落下モーションが出て跳べない
// ・歩く速さ skateSpeed は残して足を動かし、VRChat の歩きのぶんを引いて足りない分だけワープで足す
// ・坂では下る向きに少しずつ流される（slopeSlide）
// 自分のローカルの動きだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaIceSkate : UdonSharpBehaviour
{
    [Tooltip("氷の道の当たり（この中のどれかに足が乗っていたら滑る）")]
    public Collider[] ice;
    [Tooltip("足の下を見るレイのレイヤー（道・壁）")]
    public LayerMask groundMask = 1 | (1 << 11);
    [Tooltip("滑っている時に止まる壁のレイヤー")]
    public LayerMask wallMask = 1 | (1 << 11);
    public float accel = 3f, friction = 1f, maxSpeed = 4f, skateSpeed = 2f, bodyRadius = 0.25f;
    [Tooltip("坂で下る向きに流される強さ（重力×sin(傾き)×これ）。accel と比べて、登れる限界の角度が決まる（asin(accel/(9.81×これ))、0.6 なら約31度）")]
    public float slopeSlide = 0.6f;
    [Tooltip("坂で流されて出る速さの上限")]
    public float maxSlideSpeed = 8f;
    [Tooltip("壁・障害物にぶつかった時の跳ね返り（壁に向かう速さの何倍で戻るか）と、跳ねる最低の速さ")]
    public float bounce = 0.45f, bounceMin = 1.0f;

    [Tooltip("氷から跳んでふつうの床に着地した時の止まり方（毎秒これだけ減る）。空中では切り替えない")]
    public float landBrake = 14f;
    [Tooltip("氷から跳んでいる間、スティック・キーの向きへ毎秒これだけ寄せる（地面の accel より弱く。2026-11-04 てじゃ「ジャンプ中も多少制御できるように」）。速さは跳んだ時より上げない（最高 maxSpeed）")]
    public float airAccel = 1.5f;

    [Header("元の値（VRChat の既定値）")]
    public float baseWalk = 2f, baseRun = 4f, baseStrafe = 2f;

    private bool _in, _braking;
    private Vector3 _v, _lastPos;
    // 調べる用のログ（2026-11-04 動く板で棒立ちなのに加速する件）：氷の上で 0.5 秒ごとに、自分で足したワープの量・勢い・VRChat の速さ
    public bool debugLog = false;
    private Vector3 _dbgShift; private float _dbgT;
    // 動く床（FuwaMovingIce）の上：床が毎フレーム自分を運ぶ時、VRChat の GetVelocity は「床の速さのマイナス」くらいを返す（2026-11-04 実機ログ。ClientSim は 0）。
    //  そのまま「VRChat の歩き」として引くと、床の速さぶん余計にワープして床の 2 倍で流れた（てじゃ「棒立ちで床の移動方向に加速」）。
    //  → 歩きは flat + floorVelK × 床の速さ とみなす。floorVelK は手を離して乗っている間の実測で合わせる（はじめは実機の 1）
    [HideInInspector] public Vector3 floorVel;
    [HideInInspector] public int floorFrame = -10;
    public float floorVelK = 1f;
    private float _moveH, _moveV;

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { _moveH = value; }
    public override void InputMoveVertical(float value, UdonInputEventArgs args) { _moveV = value; }

    private bool IsIce(Collider c)
    {
        if (c == null || ice == null) return false;
        for (int i = 0; i < ice.Length; i++) if (ice[i] == c) return true;
        return false;
    }

    private void Enter(VRCPlayerApi p, Vector3 flat)
    {
        _in = true; _braking = false; _v = flat;
        p.SetWalkSpeed(skateSpeed); p.SetRunSpeed(skateSpeed); p.SetStrafeSpeed(skateSpeed);
    }

    private void Exit(VRCPlayerApi p)
    {
        _in = false; _braking = false;
        if (p != null) { p.SetWalkSpeed(baseWalk); p.SetRunSpeed(baseRun); p.SetStrafeSpeed(baseStrafe); }
    }

    private void OnDisable()
    {
        if (_in) Exit(Networking.LocalPlayer);
    }

    // 部屋（プレイエリアの原点）ごと平行に動かす。体の向きは入れ直さない（VR で着地の向きが変になった）
    private void Shift(VRCPlayerApi p, Vector3 step)
    {
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + step, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }

    private void Update()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        Vector3 v = p.GetVelocity();
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float dt = Time.deltaTime;
        if (Time.frameCount - floorFrame <= 1)
        {
            Vector3 fv = new Vector3(floorVel.x, 0f, floorVel.z);
            float f2 = fv.sqrMagnitude;
            if (f2 > 0.04f && Mathf.Abs(_moveH) < 0.05f && Mathf.Abs(_moveV) < 0.05f)
                floorVelK = Mathf.Lerp(floorVelK, Mathf.Clamp(-Vector3.Dot(flat, fv) / f2, 0f, 1.5f), 0.05f);
            flat += fv * floorVelK;
        }
        // ワープ・リスポーンで大きく動いたら、勢いを捨てて氷から出る
        if (_in && (pos - _lastPos).sqrMagnitude > 4f) Exit(p);
        _lastPos = pos;

        RaycastHit g;
        bool hitGround = Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out g, 0.45f, groundMask, QueryTriggerInteraction.Ignore);
        bool onFloor = hitGround && g.distance < 0.38f;
        // 2026-11-03 てじゃ「氷からふつうの床へジャンプした時、空中で切り替わって止まっちゃう」
        //  → 着地する直前（レイが床に届いた時）に切り替えていた。ふつうの床では、本当に着地してから（IsPlayerGrounded）短くブレーキをかけて止まる
        if (hitGround)
        {
            bool iceHere = IsIce(g.collider);
            if (iceHere) { if (!_in) Enter(p, flat); else if (_braking && onFloor) _braking = false; }
            else if (_in && !_braking && onFloor && p.IsPlayerGrounded())
            {
                _braking = true;
                p.SetWalkSpeed(baseWalk); p.SetRunSpeed(baseRun); p.SetStrafeSpeed(baseStrafe);
            }
        }
        if (!_in) return;   // 空中は、氷から跳んだ・落ちた時だけ勢いをそのまま
        if (_braking)
        {
            // ふつうの床に着地：勢いを短く落としきってから氷を出る。自分の歩き（flat）より遅くなったらおしまい
            _v = Vector3.MoveTowards(_v, Vector3.zero, landBrake * dt);
            if (_v.sqrMagnitude <= flat.sqrMagnitude + 0.01f) { Exit(p); return; }
        }

        Vector3 n = onFloor ? g.normal : Vector3.up;
        float yaw = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation.eulerAngles.y;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 input = Vector3.ClampMagnitude(q * Vector3.forward * _moveV + q * Vector3.right * _moveH, 1f);
        if (onFloor && !_braking)
        {
            // 坂の下る向き（横の向きだけ、長さ 1）と sin(傾き)
            // 2026-11-03 てじゃ「角度の強さに応じてずり落ちてもいい」「キツすぎると登れない」→ 押す力は sin(傾き) そのまま（前は sin×cos で 45 度でも弱かった）
            Vector3 dn = Vector3.ProjectOnPlane(Vector3.down, n);
            float sinA = dn.magnitude; dn.y = 0f;
            Vector3 dh = dn.sqrMagnitude > 0.000001f ? dn.normalized : Vector3.zero;
            if (sinA < 0.05f) dh = Vector3.zero;   // 3 度より平らなら坂あつかいしない
            if (input.sqrMagnitude > 0.01f) _v = Vector3.MoveTowards(_v, input * maxSpeed, accel * dt);
            else if (dh == Vector3.zero) _v = Vector3.MoveTowards(_v, Vector3.zero, friction * dt);
            else
            {
                // 坂で手を離している：下る向きの速さにはブレーキをかけない（ゆるい坂でもちゃんと流れる）。横と登る向きだけ減らす
                float along = Vector3.Dot(_v, dh);
                Vector3 lat = Vector3.MoveTowards(_v - dh * along, Vector3.zero, friction * dt);
                if (along < 0f) along = Mathf.Min(0f, along + friction * dt);
                _v = lat + dh * along;
            }
            _v += dh * (9.81f * slopeSlide * sinA * dt);
            if (_v.sqrMagnitude > maxSlideSpeed * maxSlideSpeed) _v = _v.normalized * maxSlideSpeed;
        }
        else if (!onFloor && !_braking && input.sqrMagnitude > 0.01f && airAccel > 0f)
        {
            // 空中：入力の向きへ少しだけ足す。今の速さ（maxSpeed より遅ければ maxSpeed）より速くはしない
            float cap = Mathf.Max(_v.magnitude, maxSpeed);
            _v += input * (airAccel * dt);
            if (_v.sqrMagnitude > cap * cap) _v = _v.normalized * cap;
        }

        // 2026-11-03 てじゃ「氷の上で壁にずっとぶつかりつづけると、すり抜けたり視点だけ先に行ったり」
        //  → 前は 0.6m の高さの小さい玉1つで、少しでもめりこむと（玉の始まりが壁の中だと当たらない）そのまま押し込み続けていた。
        //    体の高さぶんのカプセルで、少し後ろから・ふちに余裕をもって調べる。壁に向かう分は勢いからも1フレームの動きからも消す。
        //    消したあとの横すべりの動きももう一度調べて、それでも当たるなら動かさない（ワープは当たりを無視するので、自分で止めきる）
        WallClip(pos);
        // VRChat の歩き（flat）のぶんを引いて、足りない分だけワープで足す
        Vector3 delta = _v - flat;
        if (delta.sqrMagnitude > 0.0004f)
        {
            Vector3 step = new Vector3(delta.x, 0f, delta.z) * dt;
            step = ClipStep(pos, step);
            if (step.sqrMagnitude > 0.0000001f)
            {
                float dy = onFloor ? -(n.x * step.x + n.z * step.z) / n.y : 0f;   // 坂の面にそって
                Shift(p, new Vector3(step.x, dy, step.z));
                _dbgShift += step;
            }
        }
        if (debugLog && Time.time - _dbgT >= 0.5f)
        {
            Debug.Log("[FuwaIceSkate] shift=" + _dbgShift.ToString("F3") + " v=" + _v.ToString("F2") + " flat=" + flat.ToString("F2") + " onFloor=" + onFloor + " braking=" + _braking + " input=" + input.ToString("F2") + " k=" + floorVelK.ToString("F2"));
            _dbgShift = Vector3.zero; _dbgT = Time.time;
        }
    }

    private const float Skin = 0.08f;

    // 壁の向き（横だけ、長さ 1）。60 度よりゆるい面は坂なので壁あつかいしない → zero
    private Vector3 WallNormal(RaycastHit w)
    {
        if (w.normal.y >= 0.5f) return Vector3.zero;
        Vector3 wn = new Vector3(w.normal.x, 0f, w.normal.z);
        return wn.sqrMagnitude > 0.0001f ? wn.normalized : Vector3.zero;
    }

    private RaycastHit _hit;   // U# は自作メソッドの out が使えないので、当たりはここに
    private bool CastBody(Vector3 pos, Vector3 dir, float dist)
    {
        // 体のカプセル（足もとから 0.35m 〜 1.4m）。少し後ろ（Skin）から始めて、めりこみかけでも当たるように
        Vector3 back = -dir * Skin;
        Vector3 a = pos + Vector3.up * (0.35f + bodyRadius) + back, b = pos + Vector3.up * (1.4f - bodyRadius) + back;
        return Physics.CapsuleCast(a, b, bodyRadius, dir, out _hit, dist + Skin * 2f, wallMask, QueryTriggerInteraction.Ignore);
    }

    // 勢い _v のうち、すぐ近くの壁に向かう分を消す
    private void WallClip(Vector3 pos)
    {
        Vector3 hv = new Vector3(_v.x, 0f, _v.z);
        if (hv.sqrMagnitude < 0.0004f) return;
        if (!CastBody(pos, hv.normalized, 0.1f)) return;
        Vector3 wn = WallNormal(_hit); if (wn == Vector3.zero) return;
        // 2026-11-03 てじゃ「壁や障害物にぶつかったときにちょっと跳ね返ってほしい」→ bounceMin より速くぶつかったら、壁に向かう分の bounce 倍で跳ね返す
        //  （ゆっくり押しつけている時は跳ねない＝壁ぎわでガタガタしない）
        float into = Vector3.Dot(_v, wn);
        if (into < -bounceMin) _v -= wn * into * (1f + bounce);
        else if (into < 0f) _v -= wn * into;
    }

    // 1フレームの動き step を、壁の手前で止める／壁にそわせる（2回まで）
    private Vector3 ClipStep(Vector3 pos, Vector3 step)
    {
        for (int i = 0; i < 2; i++)
        {
            float len = step.magnitude; if (len < 0.0003f) return Vector3.zero;
            Vector3 d = step / len;
            if (!CastBody(pos, d, len)) return step;
            Vector3 wn = WallNormal(_hit); if (wn == Vector3.zero) return step;
            float free = Mathf.Max(0f, _hit.distance - Skin * 2f);   // 壁の手前まで（ふちに余裕）
            if (free >= len) return step;
            Vector3 rest = step - d * free;
            float into = Vector3.Dot(rest, wn); if (into < 0f) rest -= wn * into;   // 残りは壁にそって
            float into2 = Vector3.Dot(_v, wn); if (into2 < 0f) _v -= wn * into2;
            step = d * free + rest;
            if (i == 1) return d * free;
        }
        return step;
    }
}
