// Vketブース版（元は FLUFF の FuwaFur.cginc）。光は FuwaBooth.cginc の固定の太陽と空の色、霧なし
// シェルファー（毛玉）の共通処理。各パスで FUR_H（0=根元〜1=毛先）を定義してからincludeする
#include "UnityCG.cginc"
#include "FuwaBooth.cginc"

fixed4 _Color;
fixed4 _TipColor;
float _FurLength;
float _Density;
float _Thinness;
float _Shadow;
// 向きの値（_Drag・_GlowDir・_Squash.xyz・_SwayDir）はブース版ではこの物のローカルの向き（2026-10-17）
float4 _Drag;       // 毛先を引っぱる向き（スクリプトから速度に応じて入れる）
float _Gravity;
float _RimPower;
float _RimStrength;
float _Alpha;       // ゴースト用（不透明版では無視される）
float4 _GlowColor;  // 風などの影響を受けている間の光（rgb=色、a=強さ0〜1、スクリプトから入れる）
float4 _GlowDir;    // 光る向き（ワールド空間。この向きの側ほど明るく、光の帯もこの向きに流れる）
float4 _Squash;
float4 _SquashWobble;   // ポヨポヨ（x=つぶしの量のゆれ幅、y=速さ。2026-10-17 てじゃ「ちょっとポヨポヨ動かす」）
float _SwayAmp, _SwaySpeed; float4 _SwayDir;
float _WindFlutter;   // 毛先が風（_SwayDir）にそよぐ量（毛の長さに対する割合、0=なし。2026-10-16 ブースの止まった場面に動きを）   // 風でゆれる（たんぽぽの綿毛。uv3.x＝重み、uv3.y＝位相）     // 撃たれた時のポヨン（xyz=ワールドの向き、w=量。+で向きに潰れて横にふくらむ、-で伸びる）
float4 _EyeDirL, _EyeDirR;   // 目のある向き（オブジェクト空間）。この向きのまわりは毛を生やさない（目を毛が貫通しないように）
float4 _EyeMask;    // x=これより内側(cos)は毛を短く、y=ここまでで元の長さ。x<=0 なら使わない
float _EyeMinLen;
float _EyeTint;     // 目のまわりの短い毛を、どれだけ毛先寄りの色に寄せるか   // 目のまわりの毛の長さ（元の長さに対する割合）。0=毛なし（地肌が見える）

// FUR_ONE_PASS：毛の層を全部重ねたメッシュ（層ごとに球を複製し、uv4.x＝層の高さ 0〜1）を1パスで描く版
//（2026-10-17 Vket の SetPass 制限。17パス→1パス。見た目は同じ）
#ifdef FUR_ONE_PASS
struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 uv3 : TEXCOORD3; float2 uv4 : TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID };
#else
struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 uv3 : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
#endif
struct v2f
{
    float4 pos : SV_POSITION;
    float3 objPos : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
#ifdef FUR_ONE_PASS
    float furH : TEXCOORD3;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert (appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
#ifdef FUR_ONE_PASS
    float h = v.uv4.x; o.furH = h;
#else
    float h = FUR_H;
#endif
    float3 n = normalize(v.normal);
    float4 world = mul(unity_ObjectToWorld, v.vertex);
    float3 wn = UnityObjectToWorldNormal(n);
    // 向きの値（_Squash/_SwayDir/_Drag）はこの物のローカルの向きで持つ（2026-10-17 ワールドの向きだと、会場でブースが回して置かれた時にずれた）。
    // ふわふわは静的バッチングしないので行列が残る。左右反転（スケール x が負）もここで一緒にかかる
    float3x3 o2w = (float3x3)unity_ObjectToWorld;
    float3 sqDir = mul(o2w, _Squash.xyz); sqDir = dot(sqDir, sqDir) > 1e-8 ? normalize(sqDir) : float3(0, 1, 0);
    float3 swDir = mul(o2w, _SwayDir.xyz); swDir = dot(swDir, swDir) > 1e-8 ? normalize(swDir) : float3(1, 0, 0);
    // ポヨン：玉の中心から見て、撃たれた向きの成分を縮めて、横の成分をふくらませる（体積はだいたいそのまま）
    float sw = _Squash.w;
    if (_SquashWobble.x > 0) { float wt = _Time.y * _SquashWobble.y; sw += _SquashWobble.x * (sin(wt) * 0.75 + sin(wt * 1.93 + 0.7) * 0.25); }
    if (abs(sw) > 0.0001)
    {
        float3 c = unity_ObjectToWorld._m03_m13_m23;
        float3 d = world.xyz - c;
        float along = dot(d, sqDir);
        float3 side = d - sqDir * along;
        world.xyz = c + sqDir * along * (1.0 - sw) + side * (1.0 + sw * 0.5);
        // 法線は形の変形の逆向きに（楕円体の法線）。毛が潰れた形に沿って生えるように
        float wa = dot(wn, sqDir);
        wn = normalize(sqDir * wa / max(1.0 - sw, 0.2) + (wn - sqDir * wa) / (1.0 + sw * 0.5));
    }
    if (_SwayAmp > 0 && v.uv3.x > 0)
    {
        float t = _Time.y * _SwaySpeed + v.uv3.y;
        float s = sin(t) * 0.65 + sin(t * 1.73 + 1.1) * 0.35;
        float3 dw = swDir * (0.35 + 0.65 * (s * 0.5 + 0.5)) + cross(float3(0, 1, 0), swDir) * sin(t * 0.83 + 2.0) * 0.3;
        world.xyz += dw * _SwayAmp * v.uv3.x;
    }
    // 毛を法線方向に伸ばし、先ほど重力となびきで曲げる
    float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
    world.xyz += wn * _FurLength * scale * h;
    world.xyz += (float3(0, -_Gravity, 0) + mul(o2w, _Drag.xyz) / max(scale, 1e-4)) * scale * h * h;
    if (_WindFlutter > 0)
    {
        // 毛ごと（位置）に少しずつずれた速いそよぎ。毛先ほど大きく
        float t = _Time.y * _SwaySpeed * 2.2 + dot(v.vertex.xyz, float3(23.1, 17.3, 19.7));
        float s = sin(t) * 0.6 + sin(t * 2.31 + 1.7) * 0.4;
        float3 d = swDir;
        world.xyz += (d * (0.6 + 0.4 * s) + cross(float3(0, 1, 0), d) * sin(t * 1.37) * 0.25) * _WindFlutter * _FurLength * scale * h * h;
    }
    o.pos = mul(UNITY_MATRIX_VP, world);
    o.objPos = v.vertex.xyz;
    o.worldNormal = wn;
    o.worldPos = world.xyz;
    return o;
}

float hash3(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

fixed4 frag (v2f i) : SV_Target
{
#ifdef FUR_ONE_PASS
    float h = i.furH;
#else
    float h = FUR_H;
#endif
    // 3Dのマス目ごとに毛の長さを決める（毛が短いマスは途中の層で消える）
    float3 cell = floor(i.objPos * _Density);
    float hairLen = hash3(cell);
    float eyeK = 0;
    if (_EyeMask.x > 0)
    {
        float3 od = normalize(i.objPos);
        float k = max(dot(od, normalize(_EyeDirL.xyz)), dot(od, normalize(_EyeDirR.xyz)));
        eyeK = smoothstep(_EyeMask.y, _EyeMask.x, k);
        // 毛を消すと地肌（暗い根元の色）が目立つので、短く刈るだけにする
        hairLen *= lerp(1.0, _EyeMinLen, eyeK);
    }
    float3 local = frac(i.objPos * _Density) - 0.5;
    float radial = length(local);
    // 先に行くほど細く、長さを超えたら消す
    float thickness = (1.0 - h) * _Thinness;
    if (h > 0.001) clip(min(hairLen - h, thickness - radial));

    float3 n = normalize(i.worldNormal);
    float3 l = FUWA_BOOTH_SUN_DIR;
    float ndl = saturate(dot(n, l) * 0.6 + 0.4);
    float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
    float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength;
    float ao = lerp(1.0 - _Shadow, 1.0, saturate(h + eyeK * _EyeTint));
    // 目のまわりの短い毛は、根元の暗い色ではなく毛先寄りの色に（短く刈った毛並みに見えるように）
    float3 col = lerp(_Color.rgb, _TipColor.rgb, saturate(h + eyeK * _EyeTint));
    col = col * (ndl * FUWA_BOOTH_SUN_COLOR + FuwaBoothAmbient(n)) * ao + rim * _TipColor.rgb;
    // 風の光：玉の中心から見て _GlowDir 側ほど明るいグラデ＋その向きに流れる帯。毛先ほど強く
    if (_GlowColor.a > 0.001)
    {
        float3 center = unity_ObjectToWorld._m03_m13_m23;
        float radius = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0))) * (0.5 + _FurLength);
        float d = dot(i.worldPos - center, normalize(mul((float3x3)unity_ObjectToWorld, _GlowDir.xyz))) / max(radius, 1e-4);   // -1(後ろ)〜1(前)
        float grad = saturate(d * 0.5 + 0.5);
        float band = frac(d * 1.2 - _Time.y * 2.2);
        band = smoothstep(0.0, 0.25, band) * (1.0 - smoothstep(0.35, 0.7, band));
        float glow = _GlowColor.a * (0.25 + 0.75 * grad) * (0.7 + 0.6 * band) * (0.6 + 0.4 * h);
        // 白い毛に足し算だと白のままなので、光の色へ寄せてから少し明るくする
        col = lerp(col, _GlowColor.rgb * 1.2, saturate(glow)) + _GlowColor.rgb * glow * 0.3;
    }
    return fixed4(col, _Alpha);
}
