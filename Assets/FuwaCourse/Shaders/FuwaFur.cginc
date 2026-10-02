// シェルファー（毛玉）の共通処理。各パスで FUR_H（0=根元〜1=毛先）を定義してからincludeする
#include "UnityCG.cginc"
#include "Lighting.cginc"

fixed4 _Color;
fixed4 _TipColor;
float _FurLength;
float _Density;
float _Thinness;
float _Shadow;
float4 _Drag;       // 毛先を引っぱる向き（ワールド空間、スクリプトから速度に応じて入れる）
float _Gravity;
float _RimPower;
float _RimStrength;
float _Alpha;       // ゴースト用（不透明版では無視される）
float4 _GlowColor;  // 風などの影響を受けている間の光（rgb=色、a=強さ0〜1、スクリプトから入れる）
float4 _GlowDir;    // 光る向き（ワールド空間。この向きの側ほど明るく、光の帯もこの向きに流れる）
float4 _Squash;     // 撃たれた時のポヨン（xyz=ワールドの向き、w=量。+で向きに潰れて横にふくらむ、-で伸びる）

struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct v2f
{
    float4 pos : SV_POSITION;
    float3 objPos : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert (appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float h = FUR_H;
    float3 n = normalize(v.normal);
    float4 world = mul(unity_ObjectToWorld, v.vertex);
    float3 wn = UnityObjectToWorldNormal(n);
    // ポヨン：玉の中心から見て、撃たれた向きの成分を縮めて、横の成分をふくらませる（体積はだいたいそのまま）
    if (abs(_Squash.w) > 0.0001)
    {
        float3 c = unity_ObjectToWorld._m03_m13_m23;
        float3 d = world.xyz - c;
        float along = dot(d, _Squash.xyz);
        float3 side = d - _Squash.xyz * along;
        world.xyz = c + _Squash.xyz * along * (1.0 - _Squash.w) + side * (1.0 + _Squash.w * 0.5);
        // 法線は形の変形の逆向きに（楕円体の法線）。毛が潰れた形に沿って生えるように
        float wa = dot(wn, _Squash.xyz);
        wn = normalize(_Squash.xyz * wa / max(1.0 - _Squash.w, 0.2) + (wn - _Squash.xyz * wa) / (1.0 + _Squash.w * 0.5));
    }
    // 毛を法線方向に伸ばし、先ほど重力となびきで曲げる
    float scale = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0)));
    world.xyz += wn * _FurLength * scale * h;
    world.xyz += (float3(0, -_Gravity, 0) + _Drag.xyz) * scale * h * h;
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
    float h = FUR_H;
    // 3Dのマス目ごとに毛の長さを決める（毛が短いマスは途中の層で消える）
    float3 cell = floor(i.objPos * _Density);
    float hairLen = hash3(cell);
    float3 local = frac(i.objPos * _Density) - 0.5;
    float radial = length(local);
    // 先に行くほど細く、長さを超えたら消す
    float thickness = (1.0 - h) * _Thinness;
    if (h > 0.001) clip(min(hairLen - h, thickness - radial));

    float3 n = normalize(i.worldNormal);
    float3 l = normalize(_WorldSpaceLightPos0.xyz);
    float ndl = saturate(dot(n, l) * 0.6 + 0.4);
    float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
    float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength;
    float ao = lerp(1.0 - _Shadow, 1.0, h);
    float3 col = lerp(_Color.rgb, _TipColor.rgb, h);
    col = col * (ndl * _LightColor0.rgb + ShadeSH9(float4(n, 1))) * ao + rim * _TipColor.rgb;
    // 風の光：玉の中心から見て _GlowDir 側ほど明るいグラデ＋その向きに流れる帯。毛先ほど強く
    if (_GlowColor.a > 0.001)
    {
        float3 center = unity_ObjectToWorld._m03_m13_m23;
        float radius = length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0))) * (0.5 + _FurLength);
        float d = dot(i.worldPos - center, normalize(_GlowDir.xyz)) / max(radius, 1e-4);   // -1(後ろ)〜1(前)
        float grad = saturate(d * 0.5 + 0.5);
        float band = frac(d * 1.2 - _Time.y * 2.2);
        band = smoothstep(0.0, 0.25, band) * (1.0 - smoothstep(0.35, 0.7, band));
        float glow = _GlowColor.a * (0.25 + 0.75 * grad) * (0.7 + 0.6 * band) * (0.6 + 0.4 * h);
        // 白い毛に足し算だと白のままなので、光の色へ寄せてから少し明るくする
        col = lerp(col, _GlowColor.rgb * 1.2, saturate(glow)) + _GlowColor.rgb * glow * 0.3;
    }
    return fixed4(col, _Alpha);
}
