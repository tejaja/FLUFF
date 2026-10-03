using UdonSharp;
using UnityEngine;

// 風の飾り（吹き流し・かざぐるま）。つながっている FuwaWindArea の風に合わせて動く（自分の画面だけ・同期なし）。
// ・吹き流し（sock）：風が吹くと風下へたなびいて、止むと垂れる。吹いている間はぱたぱた揺れる
// ・かざぐるま（head＋rotor）：頭が風上を向いて、風の強さに合わせて回る
// wind が空なら fixedDirection の風がずっと吹いている扱い
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWindDeco : UdonSharpBehaviour
{
    public FuwaWindArea wind;
    [Tooltip("wind が空の時の風の向き（ワールド）")]
    public Vector3 fixedDirection = Vector3.forward;
    [Tooltip("風が弱い時（止んでいる時）も少しだけ吹いている扱いにする量")]
    public float idleLevel = 0f;

    [Header("吹き流し")]
    public Transform sock;
    [Tooltip("垂れた時の、風下への傾き（0=真下、1=水平）")]
    public float droopLean = 0.2f;
    public float flutterAmount = 6f;
    public float flutterSpeed = 11f;

    [Header("かざぐるま")]
    public Transform head;
    public Transform rotor;
    public float spinMax = 540f;
    public float spinIdle = 25f;

    private float _level;
    private float _spin;
    private float _seed;
    private Vector3 _lastDir = Vector3.forward;

    private void Start()
    {
        _seed = (transform.position.x * 3.7f + transform.position.z * 1.3f) % 10f;
        _lastDir = fixedDirection.sqrMagnitude > 0.001f ? fixedDirection.normalized : Vector3.forward;
    }

    private void Update()
    {
        float target = wind != null ? wind.GetGustLevel() : 1f;
        target = Mathf.Max(target, idleLevel);
        // 吹き始めは速く、止む時はゆっくり
        float rate = target > _level ? 6f : 1.5f;
        _level = Mathf.MoveTowards(_level, target, rate * Time.deltaTime);
        if (wind != null && wind.GetGustLevel() > 0.01f) _lastDir = wind.GetWorldDirection();
        else if (wind == null) _lastDir = fixedDirection.normalized;
        Vector3 h = new Vector3(_lastDir.x, 0f, _lastDir.z);
        h = h.sqrMagnitude > 0.0001f ? h.normalized : Vector3.forward;
        float t = Time.time + _seed;

        if (sock != null)
        {
            Vector3 droop = (Vector3.down + h * droopLean).normalized;
            Vector3 blow = (h + Vector3.up * Mathf.Clamp(_lastDir.y, -0.3f, 0.6f) * 0.5f).normalized;
            Vector3 dir = Vector3.Slerp(droop, blow, _level);
            Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
            float f = flutterAmount * (0.3f + 0.7f * _level);
            q = q * Quaternion.Euler(Mathf.Sin(t * flutterSpeed) * f, Mathf.Sin(t * flutterSpeed * 0.73f + 1.3f) * f, 0f);
            sock.rotation = Quaternion.Slerp(sock.rotation, q, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        if (head != null)
        {
            // 風上を向く（羽の正面＝ローカル -Z 側が風上）
            Quaternion hq = Quaternion.LookRotation(h, Vector3.up);
            head.rotation = Quaternion.Slerp(head.rotation, hq, 1f - Mathf.Exp(-2f * Time.deltaTime));
        }
        if (rotor != null)
        {
            _spin += Mathf.Lerp(spinIdle, spinMax, _level) * Time.deltaTime;
            if (_spin > 360f) _spin -= 360f;
            rotor.localRotation = Quaternion.Euler(0f, 0f, _spin);
        }
    }
}
