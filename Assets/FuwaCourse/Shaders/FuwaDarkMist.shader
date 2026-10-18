// 遠くから見たやみのもりを包む「暗いもや」（2026-10-08）。だ円体のメッシュ1つで、視線がもやの中を通る長さで濃さを決める。
// ふちは通る距離が短いので自然に薄く、まん中ほど濃い。下ほど少し濃く、ゆっくりゆらぐムラをつける。
// 外から見る用（表の面だけ描く）。中にいる時は遠景ごと消えるので考えない。
Shader "FuwaCourse/DarkMist"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.19, 0.29, 1)
        _Density ("Density (per m)", Float) = 0.035
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.9
        _LowBoost ("Lower part extra", Range(0, 2)) = 0.6
        _Wobble ("Wobble", Range(0, 1)) = 0.25
        _EdgeSoft ("Edge Softness", Range(0.01, 1)) = 0.8
        _Wisp ("Wisp (edge raggedness)", Range(0, 1)) = 0.7
        _WispScale ("Wisp Scale (per m)", Float) = 0.12
        _WispSpeed ("Wisp Speed", Float) = 0.06
        // ゆら〜っと動く大きな濃淡（2026-10-18 てじゃ「薄めにして、グラデみたいにゆら～っと濃淡がうごく」。粒を足さずにこのもやの中で）
        _Drift ("Drift (big slow shading)", Range(0, 1)) = 0
        _DriftScale ("Drift Scale (per m)", Float) = 0.035
        _DriftSpeed ("Drift Speed", Float) = 0.05
        _DriftPeak ("Drift: thick part (x)", Float) = 1.7
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Back ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color; float _Density, _MaxAlpha, _LowBoost, _Wobble, _EdgeSoft, _Wisp, _WispScale, _WispSpeed, _Drift, _DriftScale, _DriftSpeed, _DriftPeak;
            float hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float vnoise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1,0,0)), f.x), lerp(hash3(i + float3(0,1,0)), hash3(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0,0,1)), hash3(i + float3(1,0,1)), f.x), lerp(hash3(i + float3(0,1,1)), hash3(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            float fbm(float3 p) { return vnoise(p) * 0.55 + vnoise(p * 2.07 + 13.1) * 0.3 + vnoise(p * 4.3 + 7.7) * 0.15; }
            struct v2f { float4 pos : SV_POSITION; float3 opos : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_base v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.opos = v.vertex.xyz; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);   // まとめて描く時も自分の座標で（インスタンシング）
                // オブジェクト空間（半径1の球。組み込みの Sphere メッシュ）で視線と球の交わりを求め、通る長さをワールドの距離に直す
                float3 camO = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 d = normalize(i.opos - camO);
                float b = dot(camO, d), c = dot(camO, camO) - 1.0;
                float h = b * b - c;
                if (h <= 0) discard;
                h = sqrt(h);
                float t0 = max(-b - h, 0), t1 = -b + h;
                float3 segW = mul((float3x3)unity_ObjectToWorld, d * (t1 - t0));
                float len = length(segW);
                // 通る所の高さ（オブジェクト空間の中点の y）：下ほど濃く
                float3 mid = camO + d * (t0 + t1) * 0.5;
                float low = saturate(0.5 - mid.y * 0.5);
                // ゆっくり動くムラ（安いサイン波の重ね）
                float3 wp = mul(unity_ObjectToWorld, float4(mid, 1)).xyz;
                float wob = sin(wp.x * 0.11 + _Time.y * 0.15) * sin(wp.z * 0.09 - _Time.y * 0.12) * 0.5 + 0.5;
                float dens = _Density * (1 + low * _LowBoost) * lerp(1, 0.6 + wob * 0.8, _Wobble);
                float a = (1 - exp(-len * dens)) * _MaxAlpha;
                // カメラがもやの中・すぐ近く（コースの中やスタートの島）にいる時は消す（コースの中は AreaFog が担当）
                a *= smoothstep(1.1, 1.4, length(camO));
                // ふちをもやもやに：視線が球に入る所（ワールド）でノイズを取り、ふちの消え方をちぎれたように揺らす
                float3 entryW = mul(unity_ObjectToWorld, float4(camO + d * t0, 1)).xyz;
                float3 q = entryW * _WispScale + float3(_Time.y * _WispSpeed, _Time.y * _WispSpeed * 0.4, -_Time.y * _WispSpeed * 0.7);
                float n = fbm(q);
                float edge = h - (n - 0.45) * _Wisp;         // ノイズで「ふち」の位置を前後させる
                a *= smoothstep(0, _EdgeSoft, edge);
                // 中のほうにもうっすらムラ（ちぎれた雲みたいな濃淡）
                a *= lerp(1, 0.55 + n * 0.9, _Wisp * 0.5);
                // 大きくゆっくり流れる濃淡：視線が通る所の真ん中（ワールド）で、ゆるいノイズを2つの向きに流して重ねる。濃い所と薄い所がグラデで入れかわる
                if (_Drift > 0)
                {
                    float t = _Time.y * _DriftSpeed;
                    float g = fbm(wp * _DriftScale + float3(t, t * 0.3, -t * 0.6)) * 0.6 + fbm(wp * _DriftScale * 0.6 + float3(-t * 0.7, 0, t * 0.8) + 21.7) * 0.4;
                    g = smoothstep(0.25, 0.75, g);
                    a *= lerp(1, 0.2 + g * (_DriftPeak - 0.2), _Drift);   // 薄い所は 0.2倍、濃い所は _DriftPeak 倍
                    a = min(a, 0.95);
                }
                return fixed4(_Color.rgb, a * _Color.a);
            }
            ENDCG
        }
    }
}
