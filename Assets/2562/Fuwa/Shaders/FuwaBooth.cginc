// Vketブース用の共通ライティング（2026-10-14）。
// ブースは Directional ライト禁止で、太陽の向き・色や空の明るさは会場ワールドのものになってしまうので、
// 光の色・明るさと空の色はシェーダーの中に固定で持たせて、どの会場でもかぜのおかと同じ見た目にする（向きだけ会場の太陽に合わせる）。
// 規定（グローバルキーワード禁止）のため、multi_compile 系は使わない。ライトマップ・リアルタイムの影もなし。
// 値を変えたい時はここだけ直せば、ブースの全部の材質がそろって変わる。
#ifndef FUWA_BOOTH_INCLUDED
#define FUWA_BOOTH_INCLUDED

#include "UnityCG.cginc"

#include "UnityLightingCommon.cginc"

// 太陽の向き：いつもブース専用のにせの太陽（ブースの正面 +Z の、やや左上から当てて見る人の側を明るく）。
// 前は会場に太陽があればその向きにしていたが、会場の太陽が左奥上で、手前から見るとブースが逆光（吹き流し・黄色いたんぽぽが影側）になった（2026-10-21 てじゃ）。
// 地面の影はブースの座標で焼いてある（ほぼ真上から）ので、会場の太陽に合わせる意味もない。色・明るさも会場に関係なく固定（かぜのおかの見た目を守る）。
// 会場ではブースが回して置かれるので、ブースの +X のワールドの向きを _FuwaBoothX に入れてもらう（FuwaBoothGun が起動時に sunMaterials へ）。
// 入っていない時（0）はブースが回っていない時と同じ
#define FUWA_BOOTH_SUN_DIR_LOCAL float3(-0.45, 0.8, 0.55)
#define FUWA_BOOTH_SUN_COLOR  half3(1.0, 0.98, 0.94)
#define FUWA_BOOTH_SUN_DIR    FuwaBoothSunDir()

float4 _FuwaBoothX;

float3 FuwaBoothSunDir()
{
    float3 bx = _FuwaBoothX.xyz; bx.y = 0;
    bx = dot(bx, bx) > 1e-8 ? normalize(bx) : float3(1, 0, 0);
    float3 bz = cross(bx, float3(0, 1, 0));
    float3 l = FUWA_BOOTH_SUN_DIR_LOCAL;
    return normalize(bx * l.x + float3(0, l.y, 0) + bz * l.z);
}

// ブースの座標で書いた向き（材質の _SwayDir など）をワールドの向きに（2026-10-17 会場でブースが回して置かれて、ゆれ・つぶしの向きがずれた）。
// 接線（TANGENT）にブースの +X を入れておく（FuwaBoothCombine が合体メッシュに入れる）。会場の静的バッチングでメッシュがワールドに焼かれても
// 接線は一緒に回るので、そこからブースの向きがわかる。上はいつもワールドの上。接線が無い時はワールドの X（＝前と同じ）
// FLUFF の撮影セット用（2026-11-19）：合体していない元のメッシュは接線にブースの +X が入っていないので、
// _FuwaPhotoBoothX（w=1 の時、ワールドでのブースの +X）を FuwaBoothXSetter から全体に入れて使う。大きさは物の大きさ（撮影セットはブースの 0.4 倍）
float4 _FuwaPhotoBoothX;
// 綿毛の板の大きさ・ゆれ幅はブースでの大きさのメートルでメッシュに焼いてある（FuwaBoothDandelionScale）ので、ブース全体の縮め（撮影セットの Booth の大きさ）だけかける
float _FuwaPhotoBoothScale;
float FuwaBoothScale() { return _FuwaPhotoBoothScale > 0 ? _FuwaPhotoBoothScale : 1; }
float3 FuwaBoothDir(float3 d, float4 tangent)
{
    float3 bx = mul((float3x3)unity_ObjectToWorld, tangent.xyz); bx.y = 0;
    if (_FuwaPhotoBoothX.w > 0.5) bx = _FuwaPhotoBoothX.xyz;
    bx = dot(bx, bx) > 1e-8 ? normalize(bx) : float3(1, 0, 0);
    float3 bz = cross(bx, float3(0, 1, 0));
    return bx * d.x + float3(0, d.y, 0) + bz * d.z;
}

// 空の明るさ（FLUFF の Lighting の Gradient と同じ3色。Inspector の色＝ガンマなのでリニアに直して使う）
#define FUWA_BOOTH_SKY     half3(0.50, 0.60, 0.78)
#define FUWA_BOOTH_EQUATOR half3(0.60, 0.64, 0.70)
#define FUWA_BOOTH_GROUND  half3(0.58, 0.50, 0.56)

// 法線の向きでの空の明るさ（ShadeSH9 の代わり。上＝空、横＝地平線、下＝地面）
half3 FuwaBoothAmbient(float3 n)
{
    half y = n.y;
    half3 c = y > 0 ? lerp(FUWA_BOOTH_EQUATOR, FUWA_BOOTH_SKY, y) : lerp(FUWA_BOOTH_EQUATOR, FUWA_BOOTH_GROUND, -y);
    #ifdef UNITY_COLORSPACE_GAMMA
    return c;
    #else
    return GammaToLinearSpace(c);
    #endif
}

#endif
