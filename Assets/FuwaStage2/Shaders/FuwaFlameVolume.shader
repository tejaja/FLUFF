// 2–2 の焚き火と燃える島の炎（2026-11-01、てじゃの参考 GIF から）。板でもビルボードでもなく、箱の中の立体ノイズをレイで見る本当の立体。
// 色はオレンジと黄色の 2 色だけ。視線が通る中でいちばん濃い所で色を決めるので、黄色は外から透けて見える。ノイズが上へ流れて、先のかたまりがちぎれて飛ぶ
// 箱はオブジェクト座標で x,z = ±0.5、y = 0〜1.6。箱の裏面を描いて、当たった所の深さを書く（箱の前に物があっても、炎より奥なら隠れない）
Shader "FuwaCourse/FlameVolume"
{
    Properties
    {
        [NoScaleOffset] _Noise ("立体ノイズ（R、タイル）", 3D) = "gray" {}
        _Orange ("外の色", Color) = (0.93, 0.43, 0, 1)
        _Yellow ("中の色", Color) = (0.98, 1, 0.04, 1)
        _Speed ("動きの速さ", Float) = 1
        _FlowSpeed ("模様が上へ流れる速さ（タイル／秒）", Float) = 0.75
        _NoiseScale ("模様の細かさ（タイル／オブジェクトの長さ）", Float) = 0.62
        _EggY ("たまごの中心の高さ", Float) = 0.42
        _EggR ("たまごの太さ", Float) = 0.31
        _EggUp ("上へののび", Float) = 2.5
        _EggDown ("下へののび", Float) = 0.95
        _AmpBase ("根元のでこぼこ", Float) = 0.3
        _AmpTop ("先のでこぼこ", Float) = 1.25
        _YellowAt ("黄色になる濃さ", Float) = 0.42
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Front
            ZWrite On
            AlphaToMask On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _FLAME_LITE
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "FuwaFireFar.cginc"

            #if defined(SHADER_API_MOBILE) || defined(_FLAME_LITE)
                #define FLAME_LITE 1
                #define TRACE_STEPS 11
                #define COLOR_STEPS 5
            #else
                #define TRACE_STEPS 20
                #define COLOR_STEPS 9
            #endif
            #define BOX_H 1.6

            sampler3D _Noise;
            half4 _Orange, _Yellow;
            float _Speed, _FlowSpeed, _NoiseScale, _EggY, _EggR, _EggUp, _EggDown, _AmpBase, _AmpTop, _YellowAt;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 op : TEXCOORD0;      // 箱の面（オブジェクト座標）
                float3 oc : TEXCOORD1;      // カメラ（オブジェクト座標）。目ごとに違う
                UNITY_FOG_COORDS(2)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.op = v.vertex.xyz;
                o.oc = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // 0 より大きいと炎の中。_YellowAt より大きいと黄色
            float Field(float3 p, float t)
            {
                float k = saturate(p.y / BOX_H);
                float3 w = p;
                w.x += (sin(t * 2.3 + p.y * 3.1) + 0.5 * sin(t * 3.9 + p.y * 5.3)) * 0.05 * k;   // 上ほど大きくゆれる
                w.z += (sin(t * 1.9 + p.y * 2.7 + 2.0) + 0.5 * sin(t * 3.3 + p.y * 4.7)) * 0.05 * k;
                float3 q = w - float3(0, _EggY, 0);
                q.y /= q.y > 0 ? _EggUp : _EggDown;
                float body = 1.0 - length(q) / _EggR - smoothstep(0.62, 1.0, k) * 1.1;   // 箱のてっぺんで切れないように消す
                float3 a = w * _NoiseScale; a.y -= frac(t * _FlowSpeed);
                float n = tex3Dlod(_Noise, float4(a, 0)).r;
                #if !defined(FLAME_LITE)
                    // 速さのちがう 2 枚目を混ぜて、ただ流れるだけに見えないようにする
                    float3 b = w * _NoiseScale * 1.7 + float3(0.37, 0, 0.61); b.y -= frac(t * _FlowSpeed * 1.45);
                    n = n * 0.66 + tex3Dlod(_Noise, float4(b, 0)).r * 0.34;
                #endif
                return body + (n - 0.5) * 2.0 * lerp(_AmpBase, _AmpTop, k);
            }

            struct FOut { fixed4 c : SV_Target; float d : SV_Depth; };

            FOut frag (v2f i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 ro = i.oc, rd = normalize(i.op - ro);
                float pixel = length(fwidth(rd));   // 1 ピクセルの広がり（距離 1 あたり）

                // 箱に入る所と出る所
                float3 inv = 1.0 / rd;
                float3 t0 = (float3(-0.5, 0, -0.5) - ro) * inv, t1 = (float3(0.5, BOX_H, 0.5) - ro) * inv;
                float3 tn = min(t0, t1), tf = max(t0, t1);
                float s = max(max(tn.x, max(tn.y, tn.z)), 0.0), sEnd = min(tf.x, min(tf.y, tf.z));

                float t = _Time.y * _Speed, best = -1e5, sBest = s;
                bool hit = false;
                for (int k2 = 0; k2 < TRACE_STEPS && s < sEnd; k2++)
                {
                    float d = Field(ro + rd * s, t);
                    if (d > best) { best = d; sBest = s; }
                    if (d > 0.0) { hit = true; break; }
                    s += clamp(-d * 0.2, 0.015, 0.14);
                }
                // ふちはピクセルの半分だけ外まで少しずつ（AlphaToMask でなめらかに）
                float e = max(pixel * sBest * 4.0, 1e-4);
                float cover = hit ? 1.0 : saturate(best / e + 1.0);
                clip(cover - 0.01);

                // 中を通り抜けて、いちばん濃い所で色を決める
                float top = best, q = sBest;
                float dq = max((sEnd - sBest) / COLOR_STEPS, 0.05); dq = min(dq, 0.09);
                for (int c2 = 0; c2 < COLOR_STEPS && top < _YellowAt + e; c2++)
                {
                    q += dq;
                    if (q >= sEnd) break;
                    top = max(top, Field(ro + rd * q, t));
                }
                half3 c = lerp(_Orange.rgb, _Yellow.rgb, smoothstep(_YellowAt - e, _YellowAt + e, top));

                float3 hitP = ro + rd * sBest;
                float3 wp = mul(unity_ObjectToWorld, float4(hitP, 1)).xyz;
                c = FuwaFireCalm(c, FuwaFireFar(wp));
                FOut o;
                o.c = fixed4(c, cover);
                UNITY_APPLY_FOG(i.fogCoord, o.c);
                float4 clipP = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.d = clipP.z / clipP.w;
                #if defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)
                    o.d = o.d * 0.5 + 0.5;
                #endif
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
