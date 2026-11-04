// 2–2 の焚き火と燃える島の炎（2026-11-01）。FlameVolume の見た目のまま軽くした版（てじゃ「重くなるのはやだ」）。ふつうのメッシュを頂点で動かすだけで、色はオレンジと黄色の 2 色
// uv.x：0 = 外、1 = 中の黄色、2 = ちぎれて飛ぶかたまり（頂点が出る場所、法線が玉の形）。uv.y：かたまりごとのずれ。色 r：根元 0〜先 1。ノイズを読むのは頂点だけ（上のほうだけピクセルで 1 回、穴をあける）
// 黄色は深さだけ手前にずらして外から透けて見せる（描く位置は変えない）
Shader "FuwaCourse/FlameMesh"
{
    Properties
    {
        [NoScaleOffset] _Noise ("立体ノイズ（R、タイル）", 3D) = "gray" {}
        _Orange ("外の色", Color) = (0.93, 0.43, 0, 1)
        _OrangeTop ("外の色（先）", Color) = (0.93, 0.43, 0, 1)
        _Yellow ("中の色", Color) = (0.98, 1, 0.04, 1)
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("炎の場所の印（岩の影用、Replace で付ける）", Float) = 0
        _CoreSoft ("中の色のふちをぼかす（頂点で計算、透明にしない）", Range(0,1)) = 0
        _ShadeRound ("丸みの陰影（ふちほど濃い色、頂点で計算）", Range(0,1)) = 0
        _ShadeBump ("でこぼこの陰影（ノイズで明暗、頂点で計算）", Range(0,1)) = 0
        _ShadeDeep ("陰影の濃い色", Color) = (0.85, 0.23, 0.12, 1)
        _Speed ("動きの速さ", Float) = 1
        _FlowSpeed ("模様が上へ流れる速さ（タイル／秒）", Float) = 0.75
        _NoiseScale ("模様の細かさ（タイル／オブジェクトの長さ）", Float) = 0.75
        _AmpBase ("根元のでこぼこ", Float) = 0.1
        _AmpTop ("先のでこぼこ", Float) = 0.42
        _Tear ("先の穴のあき具合", Range(0,1.5)) = 0.75
        _Sway ("ゆれ（オブジェクトの長さ）", Float) = 0.05
        _BlobRise ("飛ぶかたまりの上がる高さ", Float) = 0.58
        _BlobSize ("飛ぶかたまりの大きさ", Float) = 1
        _Bias ("黄色を手前にずらす量", Float) = 1
        _Burn ("根元から燃えつきる（火炎放射）", Range(0,1)) = 0
        _Pulse ("かたまりごとの伸び縮み（オブジェクトの長さ）", Float) = 0
        _TimeAdd ("時間を足す（撮影用）", Float) = 0
        _Wave ("上のふちの大きなうねり（オブジェクトの長さ）", Float) = 0
        _WaveScale ("うねりの細かさ（タイル／オブジェクトの長さ）", Float) = 0.09
        _FireFadeNear ("遠くを薄く：はじまり(m)", Float) = 14
        _FireFadeFar ("遠くを薄く：いちばん薄い所(m)", Float) = 40
        _FireFadeMin ("遠くを薄く：いちばん薄い時の濃さ（1 で薄くしない）", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Off
            ZWrite On
            AlphaToMask On
            Stencil { Ref 32 WriteMask 32 Comp Always Pass [_StencilPass] }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "FuwaFireFar.cginc"

            sampler3D _Noise;
            half4 _Orange, _OrangeTop, _Yellow;
            float _CoreSoft, _ShadeRound, _ShadeBump;
            half4 _ShadeDeep;
            float _Speed, _FlowSpeed, _NoiseScale, _AmpBase, _AmpTop, _Tear, _Sway, _BlobRise, _BlobSize, _Bias, _Burn, _Pulse, _TimeAdd, _Wave, _WaveScale;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 np : TEXCOORD0;     // 穴をあけるノイズの座標（流れ込み）
                half3 col : TEXCOORD1;
                half tear : TEXCOORD2;     // 0 なら穴なし
                half k : TEXCOORD3;        // 根元 0〜先 1
                float3 wp : TEXCOORD5;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float NoiseAt(float3 w, float t)
            {
                float3 a = w * _NoiseScale; a.y -= frac(t * _FlowSpeed);
                float3 b = w * _NoiseScale * 1.7 + float3(0.37, 0, 0.61); b.y -= frac(t * _FlowSpeed * 1.45);
                float n = tex3Dlod(_Noise, float4(a, 0)).r * 0.66 + tex3Dlod(_Noise, float4(b, 0)).r * 0.34;
                return (n - 0.5) * 2.0;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t = _Time.y * _Speed + _TimeAdd, layer = v.uv.x, seed = v.uv.y;
                float k = v.color.r;
                o.k = k;
                float2 sway = float2(sin(t * 2.3) + 0.5 * sin(t * 3.9), sin(t * 1.9 + 2.0) + 0.5 * sin(t * 3.3)) * _Sway;
                float3 p = v.vertex.xyz, nor = v.normal;
                float yellow = layer > 0.5 && layer < 1.5 ? 1 : 0;
                float bumpN = 0;
                if (layer > 1.5)
                {
                    // ちぎれて飛ぶかたまり：上がりながらふくらんで、消える
                    float ph = frac(t * (0.55 + seed * 0.35) + seed * 7.13);
                    float size = (0.05 + 0.055 * frac(seed * 3.7)) * _BlobSize * pow(sin(ph * 3.14159), 0.7);
                    float3 c = p + float3(0, _BlobRise * ph, 0);
                    c.xz += sway * k + float2(sin(ph * 4.0 + seed * 9.0), cos(ph * 3.3 + seed * 5.0)) * 0.05 * _BlobSize;
                    float n = NoiseAt(c + nor * 0.2, t);
                    bumpN = n;
                    p = c + nor * size * (1.0 + n * 0.7);
                }
                else
                {
                    p.xz += sway * k;
                    p.y += k * _Pulse * (sin(t * (2.1 + seed * 1.7) + seed * 40.0) + 0.5 * sin(t * (3.7 + seed) + seed * 17.0)) * step(0.001, seed);
                    if (_Wave > 0.001)
                    {
                        // 横に広い炎：場所ごとに大きく伸び縮みして、先は横へも流れる
                        float3 wa = float3(p.x * _WaveScale + frac(t * 0.05), frac(t * 0.13), p.z * _WaveScale);
                        float wn = (tex3Dlod(_Noise, float4(wa, 0)).r - 0.5) * 2.0;
                        float wn2 = (tex3Dlod(_Noise, float4(wa.yzx + float3(0.31, 0.57, 0.11), 0)).r - 0.5) * 2.0;
                        float kw = k * k;
                        p.y += kw * _Wave * wn;
                        p.x += kw * _Wave * 0.5 * wn2;
                    }
                    float amp = lerp(_AmpBase, _AmpTop, k) * (yellow > 0.5 ? 0.9 : 1.0);
                    bumpN = NoiseAt(p, t);
                    p += nor * bumpN * amp;
                    o.tear = yellow > 0.5 ? 0 : smoothstep(0.42, 0.95, k) * _Tear;
                }
                float3 wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.wp = wp;
                o.pos = UnityWorldToClipPos(wp);
                if (yellow > 0.5)
                {
                    // 深さだけ手前へ（根元は少しだけ。多いと穴のふちを越えて見える）
                    float len = length(unity_ObjectToWorld[0].xyz);
                    float bias = lerp(0.12, 0.46, saturate(k / 0.26)) * len * _Bias;
                    float4 near = UnityWorldToClipPos(wp + normalize(_WorldSpaceCameraPos - wp) * bias);
                    o.pos.z = near.z / near.w * o.pos.w;
                }
                float3 a = p * _NoiseScale * 1.35; a.y -= frac(t * _FlowSpeed * 1.2);
                o.np = a;
                half3 outer = lerp(_Orange.rgb, _OrangeTop.rgb, smoothstep(0.3, 0.95, k));
                half3 c = outer;
                if (yellow > 0.5)
                {
                    // 正面ほど中の色、ふちは外の色へ。透明にしないのでピクセルの負荷は増えない
                    float3 wn = normalize(UnityObjectToWorldNormal(nor));
                    float facing = abs(dot(wn, normalize(_WorldSpaceCameraPos - wp)));
                    c = lerp(_Yellow.rgb, outer, _CoreSoft * (1.0 - smoothstep(0.1, 0.8, facing)));
                }
                else if (_ShadeRound + _ShadeBump > 0.001)
                {
                    // VR で遠近感を出す陰影（2026-11-02 てじゃ「遠近感が分かりづらい」）。ふちとへこみは濃く、出っぱりは明るく。頂点だけで計算する
                    float3 wn = normalize(UnityObjectToWorldNormal(nor));
                    float facing = abs(dot(wn, normalize(_WorldSpaceCameraPos - wp)));
                    float dark = _ShadeRound * (1.0 - smoothstep(0.15, 0.9, facing)) + _ShadeBump * saturate(-bumpN * 2.0);
                    c = lerp(outer, _ShadeDeep.rgb, saturate(dark));
                    c = lerp(c, _Yellow.rgb, _ShadeBump * saturate(bumpN * 2.0) * 0.5);
                }
                o.col = FuwaFireCalm(c, FuwaFireFar(wp));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                FuwaFireFadeClip(i.wp, i.pos);
                half cover = 1;
                if (i.tear > 0.001 || _Burn > 0.001)
                {
                    float n = tex3Dlod(_Noise, float4(i.np, 0)).r;
                    float d = i.tear > 0.001 ? n - i.tear * 0.5 : 1.0;   // tear が大きいほど穴が増える
                    if (_Burn > 0.001) d = min(d, i.k + (n - 0.5) * 0.25 - _Burn * 1.15);   // 根元から消える
                    cover = saturate(d / max(fwidth(d), 1e-4) + 0.5);
                    clip(cover - 0.01);
                }
                fixed4 o = fixed4(i.col, cover);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
