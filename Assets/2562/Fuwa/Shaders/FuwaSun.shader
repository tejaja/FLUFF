// ブースの空に浮かべる立体の太陽（2026-10-18 てじゃ「右上に太陽でも浮かせとく？FLUFFにつかってるやつ」「いつもこっちむくやつはやだな」）。
// FLUFF の空（FuwaCourse/GradientSky）のデフォルメ太陽を立体に：ぷっくりした球＋まわりに先の丸いトゲ（FuwaBoothSun が作るメッシュ）。
// トゲはゆっくり回って、ときどき外へピョンと広がる（FLUFF と同じ動き、頂点を動かす）。球の正面（オブジェクトの +Z）に顔（棒の目・ほっぺ）。
// 色は光らせ気味（太陽なので影はうすく）、ふちは少し濃い色（FLUFF の円のふち）。
// オブジェクト座標を使うので、静的バッチングされない Dynamic の下に置く（memory: reference_static_batching_object_space）
// uv1.x：0=球、1=トゲ／uv1.y：トゲの根元0〜先1／uv2.xy：トゲの向き（XY 平面、ピョンで動かす向き）
Shader "Custom/2562/Fuwa/Sun"
{
    Properties
    {
        _SunColor ("Sun Color", Color) = (1, 0.97, 0.88, 1)
        _DiscColor ("Disc Color", Color) = (1, 0.93, 0.55, 1)
        _RayColor ("Ray / Rim Color", Color) = (1, 0.72, 0.3, 1)
        _RaySpin ("Ray Spin Speed (rad/s)", Float) = 0.08
        _PopEvery ("トゲがピョンと広がる間隔(秒)", Float) = 5.5
        _PopTime ("ピョンの長さ(秒)", Float) = 0.9
        _PopAmount ("ピョンで外へ出る量(半径の何倍)", Float) = 0.45
        _Shade ("光の当たらない側の暗さ", Range(0, 0.5)) = 0.12
        _EyeColor ("Eye Color", Color) = (0.38, 0.22, 0.12, 1)
        _CheekColor ("Cheek Color (a=strength)", Color) = (1, 0.55, 0.62, 0.85)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "FuwaBooth.cginc"

            fixed4 _SunColor, _DiscColor, _RayColor, _EyeColor, _CheekColor;
            float _RaySpin, _PopEvery, _PopTime, _PopAmount, _Shade;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 objN : TEXCOORD1; float3 wpos : TEXCOORD2; float part : TEXCOORD3; };

            float2 rot(float2 p, float a) { float s, c; sincos(a, s, c); return float2(p.x * c - p.y * s, p.x * s + p.y * c); }

            v2f vert (appdata v)
            {
                v2f o;
                float3 p = v.vertex.xyz, n = v.normal;
                if (v.uv1.x > 0.5)
                {
                    // ときどきトゲが外へピョンと広がって、ぷるんと戻る（たまに1回休み）。先のほうが少し多く出る（FLUFF と同じ）
                    float popI = floor(_Time.y / _PopEvery);
                    float popX = (_Time.y - popI * _PopEvery) / max(_PopTime, 0.05);
                    float popOn = frac(sin(popI * 12.9898) * 43758.5453) < 0.8 ? 1 : 0;
                    float pop = popX < 1 ? popOn * (1 - popX) * (1 - popX) * sin(popX * 9.4) : 0;
                    p.xy += v.uv2 * _PopAmount * pop * (1 + 0.15 * v.uv1.y);
                    // ゆっくり回る
                    float a = -_Time.y * _RaySpin;
                    p.xy = rot(p.xy, a); n.xy = rot(n.xy, a);
                }
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.n = UnityObjectToWorldNormal(n);
                o.objN = v.normal;
                o.wpos = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.part = v.uv1.x;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                // 太陽なので影はうすく：光の当たらない側を少しだけ暗く（立体感だけ）
                float lit = saturate(dot(n, FUWA_BOOTH_SUN_DIR) * 0.5 + 0.5);
                float shade = lerp(1 - _Shade, 1, lit);
                float3 col;
                if (i.part > 0.5) col = _RayColor.rgb;
                else
                {
                    // 球：中は明るく、ふち（見た目の輪郭の近く）は少し濃い色（FLUFF の円のふち 0.84 ＝ 視線との角度で同じくらい）
                    float ndv = saturate(dot(n, v));
                    float aa = max(fwidth(ndv), 1e-4);
                    float rim = 1 - smoothstep(0.54 - aa, 0.54 + aa, ndv);
                    col = lerp(_DiscColor.rgb, _RayColor.rgb, rim);
                    // 顔：球の正面（+Z）を前から見た形（XY に投影＝FLUFF の円の上の顔と同じ座標）
                    float3 on = normalize(i.objN);
                    if (on.z > 0)
                    {
                        float2 fp = on.xy;
                        float fa = max(fwidth(length(fp)), 1e-4) * 1.2;
                        float2 e = float2(abs(fp.x) - 0.3, clamp(fp.y, 0.02, 0.3));
                        float eye = 1 - smoothstep(0.065 - fa, 0.065 + fa, length(float2(e.x, fp.y - e.y)));
                        float cheek = 1 - smoothstep(0.15 - fa, 0.15 + fa, length(float2(abs(fp.x) - 0.52, fp.y + 0.16)));
                        col = lerp(col, _CheekColor.rgb, cheek * _CheekColor.a);
                        col = lerp(col, _EyeColor.rgb, eye);
                    }
                }
                return fixed4(col * _SunColor.rgb * shade, 1);
            }
            ENDCG
        }
    }
}
