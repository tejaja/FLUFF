using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// プレイヤーの足元の丸い影（本物の影の代わり、自分の画面だけ）。
// 影を焼いた後はリアルタイムの影を切るので、プレイヤーの足元に影がないと浮いて見える → ぼかした丸を床に置く。
// ・shadows に丸い影（Quad、FuwaCourse/BlobShadow）を人数ぶん並べておく。足りない分の人には出さない
// ・足元から真下に線を飛ばして、当たった床に置く。床から離れるほど薄く・少し大きく
// ・sun を入れて disableRealtimeShadows を ON にすると、始まった時に太陽のリアルタイムの影を切る（焼いた影だけにする）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlayerShadows : UdonSharpBehaviour
{
    public Renderer[] shadows;
    [Tooltip("当たる地面のレイヤー")]
    public LayerMask groundMask = (1 << 0) | (1 << 11);
    public float maxDistance = 6f;
    [Tooltip("身長（目の高さ）1.6m の時の直径")]
    public float baseSize = 0.75f;
    public float nearAlpha = 0.45f;
    [Tooltip("この高さ(m)まで離れると消える")]
    public float fadeHeight = 4f;
    public float lift = 0.02f;
    [Tooltip("デカール（FuwaCourse/BlobDecal の箱）の時：当たった地面から下・上に何mの範囲の表面に描くか")]
    public bool decal = true;
    public float decalBelow = 0.4f;
    // 2026-10-19 てじゃ「手とか下につくとこうなる」：0.08 だと床に手をついた時に手（アバターも深度に入る）に影が乗って、細かい面の向きでザラザラに見えた → 0.04。
    // 低すぎると凸凹の地面の出っぱりに影が乗らなくなる（てじゃ「凸凹の地面とかおかしくならない？」）ので、手が床すれすれの時だけ乗る程度
    // 4cm でも床に手をつくと指（床から0〜2cm）に全開で乗ったまま（てじゃ「あんまかわってない」）→ 3cm にして、床から fadeFrom より上はだんだん薄く（てじゃ A案）
    [Tooltip("当たった地面から上に何mの表面に描くか（ここで影が消える）")]
    public float decalAboveGround = 0.05f;   // 2026-11-28 てじゃ「離れると足元のフチだけ影が消える」：深度の誤差で地面がふちをこえていた → 3cm→5cm、薄くし始める所も 0.5cm→2cm
    [Tooltip("地面からこの高さ(m)までは影の濃さそのまま、そこから decalAboveGround にかけて薄く")]
    public float fadeFrom = 0.02f;
    [Tooltip("影の円のまわり（半径のこの割合）にも下へレイを飛ばし、いちばん高い地面（足もとより stepUp 以内）を影の地面にする。小さい段差（デッキの板・道のふち）で影が欠けないように（2026-11-28 てじゃ）")]
    public float probeRadius = 0.35f;
    public float stepUp = 0.15f;
    [Tooltip("影を塗る面の向き（地面の上向きとの dot）。ほかのうそ影と同じ 0.55〜0.85（厳しくすると凸凹の地面で影が欠ける）")]
    public float upMin = 0.55f;
    public float upMax = 0.85f;
    [Tooltip("何フレームおきに人のリストを取り直すか")]
    public int refreshFrames = 30;

    [Header("地面のシェーダーで描く（FuwaPlayerBlob.cginc）")]
    // 2026-11-28 てじゃ「アバターに乗らないようにはできないの？」→ 箱のデカール（深度頼み）をやめ、地面のシェーダー（ToonLit・InkCap・Deco）が自分で暗くする。
    //  近いプレイヤー16人まで、足元の地面の位置・半径・濃さを全シェーダー共通の値で渡す。shadows の箱は色・ぼかしの設定を読むだけで出さない
    public bool receiver = true;
    [Tooltip("足元の地面より上の段に何mまで影を落とすか（小さい段差）")]
    public float receiverUpTol = 0.2f;
    [Tooltip("足元の地面より下の段に何mまで影を落とすか")]
    public float receiverDownTol = 0.5f;

    [Header("リアルタイムの影")]
    public Light sun;
    public bool disableRealtimeShadows = false;

    private VRCPlayerApi[] _players = new VRCPlayerApi[96];
    private int _count;
    private int _frame;
    private Material[] _mats;
    private float[] _alpha;
    private Vector4[] _pb = new Vector4[16];
    private Vector4[] _pq = new Vector4[16];
    private float[] _pbDist = new float[16];
    private int _idPB, _idPBA, _idPBCount;

    private void Start()
    {
        int n = shadows != null ? shadows.Length : 0;
        _mats = new Material[n];
        _alpha = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (shadows[i] == null) continue;
            _mats[i] = shadows[i].material;
            // 箱の上のふちで薄くする所：地面＋fadeFrom から上（箱のローカルの y は -0.5〜0.5、地面は上から decalAboveGround の所）
            float edgeY = 0.5f - (decalAboveGround - fadeFrom) / (decalAboveGround + decalBelow);
            if (_mats[i] != null) { _mats[i].SetFloat("_UpMin", upMin); _mats[i].SetFloat("_UpMax", upMax); _mats[i].SetFloat("_EdgeY", edgeY); }
            _alpha[i] = -1f;
            shadows[i].enabled = false;
        }
        if (disableRealtimeShadows && sun != null) sun.shadows = LightShadows.None;
        if (receiver)
        {
            _idPB = VRCShader.PropertyToID("_UdonFuwaPB");
            _idPBA = VRCShader.PropertyToID("_UdonFuwaPQ");
            _idPBCount = VRCShader.PropertyToID("_UdonFuwaPBCount");
            // 色・ぼかしは今までの箱の材質（BlobDecal）から
            Color col = new Color(0.12f, 0.08f, 0.2f, 1f); float fs = 0.35f, pw = 1f;
            Material m0 = n > 0 && shadows[0] != null ? shadows[0].sharedMaterial : null;
            if (m0 != null) { col = m0.GetColor("_Color"); fs = m0.GetFloat("_FalloffStart"); pw = m0.GetFloat("_Power"); }
            VRCShader.SetGlobalColor(VRCShader.PropertyToID("_UdonFuwaPBColor"), col);
            VRCShader.SetGlobalVector(VRCShader.PropertyToID("_UdonFuwaPBParam"), new Vector4(fs, pw, receiverUpTol, receiverDownTol));
            VRCShader.SetGlobalVectorArray(_idPB, _pb);
            VRCShader.SetGlobalVectorArray(_idPBA, _pq);
            VRCShader.SetGlobalFloat(_idPBCount, 0f);
        }
#if UNITY_ANDROID || UNITY_IOS
        // スマホ・Quest はプレイヤーの影なし（2026-11-21 深度をやめた。てじゃ「プレイヤと銃はいらん」）
        enabled = false;
#endif
    }

    private void LateUpdate()
    {
        if (shadows == null) return;
        if (_frame-- <= 0)
        {
            _frame = refreshFrames;
            _count = VRCPlayerApi.GetPlayerCount();
            if (_players.Length < _count) _players = new VRCPlayerApi[_count + 8];
            VRCPlayerApi.GetPlayers(_players);
        }
        if (receiver) { UpdateReceiver(); return; }
        int used = 0;
        for (int i = 0; i < _count && used < shadows.Length; i++)
        {
            VRCPlayerApi p = _players[i];
            if (p == null || !Utilities.IsValid(p)) continue;
            Renderer r = shadows[used];
            if (r == null) { used++; continue; }
            Vector3 pos = p.GetPosition();
            RaycastHit hit;
            if (!Physics.Raycast(pos + Vector3.up * 0.4f, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (r.enabled) r.enabled = false;
                used++;
                continue;
            }
            float h = Mathf.Max(0f, hit.distance - 0.4f);
            float k = Mathf.Clamp01(h / Mathf.Max(0.01f, fadeHeight));
            float eye = p.GetAvatarEyeHeightAsMeters();
            float size = baseSize * Mathf.Clamp(eye / 1.6f, 0.4f, 2.5f) * (1f + k * 0.4f);
            Transform t = r.transform;
            if (decal)
            {
                // 箱で囲んだ範囲の実際の地面にだけ描く（崖のふちで空中に浮かない）
                // まわりの少し高い地面（段差の上の段）も影の地面に入るよう、まわりのいちばん高い所を地面にする（低い所は decalBelow の範囲で塗られる）
                Vector3 gp = hit.point;
                if (probeRadius > 0f && hit.normal.y > 0.7f)
                {
                    float pr = size * 0.5f * probeRadius;
                    for (int q = 0; q < 4; q++)
                    {
                        Vector3 off = q == 0 ? new Vector3(pr, 0f, 0f) : q == 1 ? new Vector3(-pr, 0f, 0f) : q == 2 ? new Vector3(0f, 0f, pr) : new Vector3(0f, 0f, -pr);
                        RaycastHit h2;
                        if (Physics.Raycast(hit.point + off + Vector3.up * (stepUp + 0.05f), Vector3.down, out h2, stepUp + 0.3f, groundMask, QueryTriggerInteraction.Ignore)
                            && h2.normal.y > 0.7f && h2.point.y > gp.y && h2.point.y - hit.point.y <= stepUp)
                            gp.y = h2.point.y;
                    }
                }
                t.position = gp + hit.normal * ((decalAboveGround - decalBelow) * 0.5f);
                t.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                t.localScale = new Vector3(size, decalAboveGround + decalBelow, size);
            }
            else
            {
                t.position = hit.point + hit.normal * lift;
                t.rotation = Quaternion.LookRotation(-hit.normal, Mathf.Abs(hit.normal.y) > 0.9f ? Vector3.forward : Vector3.up);
                t.localScale = new Vector3(size, size, size);
            }
            float a = nearAlpha * (1f - k);
            if (_mats[used] != null && Mathf.Abs(a - _alpha[used]) > 0.01f) { _alpha[used] = a; _mats[used].SetFloat("_Alpha", a); }
            if (!r.enabled) r.enabled = true;
            used++;
        }
        for (int i = used; i < shadows.Length; i++)
            if (shadows[i] != null && shadows[i].enabled) shadows[i].enabled = false;
    }

    // 地面のシェーダーへ渡す（近い16人まで）
    private void UpdateReceiver()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        Vector3 me = lp != null ? lp.GetPosition() : Vector3.zero;
        int n = 0;
        for (int i = 0; i < _count; i++)
        {
            VRCPlayerApi p = _players[i];
            if (p == null || !Utilities.IsValid(p)) continue;
            Vector3 pos = p.GetPosition();
            RaycastHit hit;
            if (!Physics.Raycast(pos + Vector3.up * 0.4f, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore)) continue;
            float h = Mathf.Max(0f, hit.distance - 0.4f);
            float k = Mathf.Clamp01(h / Mathf.Max(0.01f, fadeHeight));
            float eye = p.GetAvatarEyeHeightAsMeters();
            float size = baseSize * Mathf.Clamp(eye / 1.6f, 0.4f, 2.5f) * (1f + k * 0.4f);
            float a = nearAlpha * (1f - k);
            if (a <= 0.001f) continue;
            float dd = (pos - me).sqrMagnitude;
            int slot = n;
            if (n >= 16)
            {
                // いっぱいなら、いちばん遠い人と入れ替え（自分に近い人を優先）
                int far = 0;
                for (int q = 1; q < 16; q++) if (_pbDist[q] > _pbDist[far]) far = q;
                if (_pbDist[far] <= dd) continue;
                slot = far;
            }
            else n++;
            _pb[slot] = new Vector4(hit.point.x, hit.point.y, hit.point.z, size * 0.5f);
            _pq[slot] = new Vector4(0f, 1f, 1f, a);
            _pbDist[slot] = dd;
        }
        VRCShader.SetGlobalVectorArray(_idPB, _pb);
        VRCShader.SetGlobalVectorArray(_idPBA, _pq);
        VRCShader.SetGlobalFloat(_idPBCount, n);
    }
}
