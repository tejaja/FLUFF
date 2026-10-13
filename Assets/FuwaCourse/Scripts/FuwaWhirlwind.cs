using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// つむじ風：筒の中のふわふわを、ぐるぐる回しながら上へ巻き上げる。てっぺんまで来たら前へ送り出す。
// このオブジェクトの位置＝筒の底の中心、+Y＝上、+Z＝てっぺんで送り出す向き。判定は子の Collider（トリガー）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWhirlwind : UdonSharpBehaviour
{
    [Tooltip("筒の半径(m)（底）")]
    public float radius = 1.6f;
    [Tooltip("てっぺんの半径(m)。底より大きくすると上へ広がるらせんになる（0以下なら底と同じ）")]
    public float topRadius = 0f;
    [Tooltip("巻き上げる高さ(m)。ここより上は送り出し")]
    public float height = 6f;
    [Tooltip("上へ持ち上げる強さ（加速度）。上へ行くほど少し弱まる")]
    public float lift = 4.2f;
    [Range(0f, 1f)] public float liftFade = 0.35f;
    [Tooltip("ぐるぐる回す強さ（加速度）")]
    public float swirl = 2.2f;
    [Tooltip("真ん中へ吸い寄せる強さ（加速度）。外ほど強い")]
    public float inward = 2.5f;
    [Tooltip("てっぺんで +Z へ送り出す強さ（加速度）")]
    public float exitPush = 3.5f;
    [Tooltip("送り出しが始まる高さ（height に対する割合）")]
    [Range(0f, 1f)] public float exitFrom = 0.85f;
    [Tooltip("回る向き（1=上から見て反時計回り、-1=時計回り）")]
    public float spinDir = 1f;

    [Header("捕まえるつむじ風（trap）")]
    [Tooltip("オンにすると、巻き上げて送り出す代わりに、中心へ吸い寄せて少し浮かせたまま捕まえる。強く撃てば抜けられる")]
    public bool trap = false;
    [Tooltip("中心へ吸い寄せる強さ（加速度）。これに逆らって外まで届く速さで撃てば抜けられる")]
    public float trapPull = 2.6f;
    [Tooltip("中心へ向かって動いている時だけかけるブレーキ（行き過ぎて反対側へ飛び出さないように）")]
    public float trapInwardDamp = 2.5f;
    [Tooltip("捕まえている間、中心のまわりを回る速さ(m/s)")]
    public float trapSpin = 0.8f;
    [Tooltip("ぐるぐる回る輪の半径（radius に対する割合）。0なら中心に寄せる。外周寄りほど撃ち出しやすい")]
    [Range(0f, 0.95f)] public float trapRing = 0f;
    [Tooltip("輪より内側に入った時に外へ戻すばねの強さ")]
    public float trapRingSpring = 8f;
    [Tooltip("捕まえておく高さ(m)（底から）")]
    public float trapHeight = 0.8f;
    public float trapHoldStiffness = 3f;
    public float trapHoldDamp = 2f;
    [Tooltip("回りながら上下する幅(m)。輪の上の位置と時間でゆるく波打つ")]
    public float trapBob = 0f;
    [Tooltip("上下の波がずれていく速さ（1秒あたりのラジアン）")]
    public float trapBobSpeed = 0.8f;
    [Header("溜めて撃つと抜けられる（2026-10-13 てじゃ「なかなか抜け出しにくい」）")]
    [Tooltip("この溜め具合（0〜1）から上で撃たれたら、しばらく捕まえる力をゆるめて、撃った向きへ運び出す。ふつう撃ちの連打では抜けない")]
    [Range(0f, 1f)] public float trapEscapeFromCharge = 0.3f;
    [Tooltip("フルチャージで撃った時に放しておく秒数（溜めが少ないほど短く、最短で半分）")]
    public float trapEscapeTime = 1.0f;
    [Tooltip("放している間、撃った向き（水平）へ運ぶ強さ（加速度）。フルチャージで全部、溜めが少ないほど弱い")]
    public float trapEscapeCarry = 7f;

    public void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        // あそびばの竜巻（2026-10-12）：同期ふわふわ（FuwaSharedBall）も巻き上げる・捕まえる。全員の画面で同じ風
        FuwaSharedBall shared = ball == null ? other.GetComponent<FuwaSharedBall>() : null;
        if (ball == null && shared == null) return;
        Vector3 local = transform.InverseTransformPoint(other.transform.position);
        float h = Mathf.Clamp01(local.y / Mathf.Max(0.01f, height));
        Vector3 flat = new Vector3(local.x, 0f, local.z);
        float r = flat.magnitude;
        // その高さでの半径（上へ行くほど広がる）。箱の角など円の外では効かせない
        float rHere = topRadius > 0f ? Mathf.Lerp(radius, topRadius, h) : radius;
        if (r > rHere) return;
        if (trap && ball != null) { Trap(ball, other, local, flat, r); return; }
        if (trap && shared != null) { TrapShared(shared, other, local, flat, r); return; }
        Vector3 acc = Vector3.zero;
        // 上へ（てっぺんに近いほど弱め、送り出し区間では浮かせる程度）
        float up = lift * (1f - liftFade * h);
        if (h > exitFrom) up = Mathf.Lerp(up, 1.6f, (h - exitFrom) / (1f - exitFrom));
        acc += Vector3.up * up;
        if (r > 0.001f)
        {
            Vector3 radial = flat / r;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * spinDir;
            float outer = Mathf.Clamp01(r / Mathf.Max(0.01f, rHere));
            // 送り出し区間では回転を止めて、まっすぐ前へ出す（横に飛ばされないように）
            float exitness = h > exitFrom ? Mathf.Clamp01((h - exitFrom) / (1f - exitFrom) * 2f) : 0f;
            acc += tangent * swirl * (0.4f + 0.6f * outer) * (1f - exitness);
            acc -= radial * inward * outer;
        }
        // てっぺんで前へ送り出す
        if (h > exitFrom) acc += Vector3.forward * exitPush * Mathf.Clamp01((h - exitFrom) / (1f - exitFrom) * 2f);
        if (ball != null) ball.AddWind(transform.TransformDirection(acc));
        else shared.AddWind(transform.TransformDirection(acc) * Mathf.Sqrt(Mathf.Max(0.05f, shared.shotScale)));   // 重い玉（巨大）は巻き上がりにくく
    }

    // 捕まえるつむじ風：中心へ吸い寄せて、中心のまわりをゆっくり回しながら trapHeight に浮かせておく
    private void Trap(FuwaBall ball, Collider other, Vector3 local, Vector3 flat, float r)
    {
        Rigidbody rb = other.attachedRigidbody;
        Vector3 vel = rb != null ? transform.InverseTransformDirection(rb.velocity) : Vector3.zero;
        Vector3 acc = TrapAcc(local, flat, r, vel, ball.fallAcceleration, ball.LastShotCharge(), ball.LastShotAge(), ball.LastShotDir(), Time.time, 1f);
        // 光る向きは力の向き（ころころ変わる）ではなく、回っている向き＋少し上向きにして落ち着かせる
        Vector3 glow = r > 0.001f ? Vector3.Cross(Vector3.up, flat / r) * spinDir + Vector3.up * 0.6f : Vector3.up;
        ball.AddWindGlowDir(transform.TransformDirection(acc), transform.TransformDirection(glow), 0.55f);
    }

    // あそびばの竜巻（2026-10-13 てじゃ「なるべくコースと同じ動きに」）：同期ふわふわも同じ決まりで捕まえる。
    // 全員の画面でそれぞれ同じ計算（位置・速さ・撃った情報は同期済みの物、上下の波はサーバー時刻）なので、ずれにくい
    private void TrapShared(FuwaSharedBall shared, Collider other, Vector3 local, Vector3 flat, float r)
    {
        Rigidbody rb = other.attachedRigidbody;
        Vector3 vel = rb != null ? transform.InverseTransformDirection(rb.velocity) : Vector3.zero;
        // 上下の波：画面ごとに Time.time が違うのでサーバー時刻（ミリ秒）で。波1周ぶんで割った余りにして、float の桁落ちを防ぐ
        float periodMs = trapBobSpeed > 0.0001f ? 2f * Mathf.PI / trapBobSpeed * 1000f : 1000f;
        float bobT = (Networking.GetServerTimeInMilliseconds() % Mathf.Max(1, Mathf.RoundToInt(periodMs))) / 1000f;
        // 重い玉（巨大）は運び出す力も弱め（撃った速さも小さいので、抜けにくい）
        float carryK = Mathf.Sqrt(Mathf.Max(0.05f, shared.shotScale));
        Vector3 acc = TrapAcc(local, flat, r, vel, shared.fallAcceleration, shared.LastShotCharge(), shared.LastShotAge(), shared.LastShotDir(), bobT, carryK);
        shared.AddWind(transform.TransformDirection(acc));
    }

    // 捕まえる力（このつむじ風の向きの加速度）。vel もこの向き。shotDir はワールドの向き
    private Vector3 TrapAcc(Vector3 local, Vector3 flat, float r, Vector3 vel, float fallAcc, float shotCharge, float shotAge, Vector3 shotDir, float bobTime, float carryK)
    {
        Vector3 vFlat = new Vector3(vel.x, 0f, vel.z);
        Vector3 acc = Vector3.zero;
        if (r > 0.001f)
        {
            Vector3 radial = flat / r;
            float vr = Vector3.Dot(vFlat, radial);
            float ring = radius * trapRing;
            if (r > ring)
            {
                // 輪より外：一定の力で内へ（中心寄せの時は真ん中でがたつかないよう弱める）。内へ向かう動きにだけブレーキ
                acc -= radial * trapPull * (ring > 0f ? 1f : Mathf.Clamp01(r / 0.3f));
                if (vr < 0f) acc -= radial * vr * trapInwardDamp;
            }
            else
            {
                // 輪より内：ばねで輪へ戻す。外へ向かう動きにブレーキ（輪を行き過ぎないように）
                acc += radial * (ring - r) * trapRingSpring;
                if (vr > 0f) acc -= radial * vr * trapInwardDamp;
            }
            // 中心のまわりを回す（回る速さを trapSpin に近づけるだけ。外へははじかない）
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * spinDir;
            float vt = Vector3.Dot(vFlat, tangent);
            acc += tangent * (trapSpin * Mathf.Clamp01(r / 0.4f) - vt) * 2f;
        }
        // 溜めて撃たれた直後：つかむ力（横）をゆるめて、撃った向きへ運び出す（2026-10-13 てじゃ「なかなか抜け出しにくい」）。
        // 撃つと手前側のふわふわは中心へ押し込まれるので、ゆるめるだけだと反対側まで届かなかった（フルチャージでも半分くらいしか抜けられない）
        if (shotCharge >= trapEscapeFromCharge)
        {
            float rel = Mathf.Clamp01((shotCharge - trapEscapeFromCharge) / Mathf.Max(0.001f, 1f - trapEscapeFromCharge));
            if (rel > 0f && shotAge >= 0f && shotAge < trapEscapeTime * (0.5f + 0.5f * rel))
            {
                acc.x *= 1f - rel; acc.z *= 1f - rel;
                Vector3 sd = transform.InverseTransformDirection(shotDir); sd.y = 0f;
                if (sd.sqrMagnitude > 0.000001f) acc += sd.normalized * trapEscapeCarry * rel * carryK;
            }
        }
        // 高さ：重さを打ち消して、目標の高さへばねで戻す。目標は輪の上の位置と時間でゆるく波打つ（回りながら上下する）
        float targetH = trapHeight;
        if (trapBob > 0f && r > 0.001f) targetH += trapBob * Mathf.Sin(Mathf.Atan2(flat.z, flat.x) * spinDir + bobTime * trapBobSpeed);
        acc.y += fallAcc + (targetH - local.y) * trapHoldStiffness - vel.y * trapHoldDamp;
        return acc;
    }
}
