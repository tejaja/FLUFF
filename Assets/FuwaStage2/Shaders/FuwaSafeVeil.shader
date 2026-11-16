// 2026-11-15 2–5 クリスタルの広場：セーフティーゾーン（バリアの影）のまわりに立つ、うすい白いヴェール（てじゃ「セーフティーゾーンの周りにうっすら白いヴェールほしい」）
//  メッシュは「ふちに沿った帯」の型だけ（uv.x＝ふちの上の位置 0〜1、uv.y＝高さ 0〜1）。形は頂点シェーダーで毎フレーム作る：
//   クリスタル（_Src）から見たバリア（_Ball、xyz＝位置 w＝半径）の、上から見た2Dの影（FuwaShockwave.Safe と同じ形）
//   0〜1/3＝左の線（遠く→バリアの横）、1/3〜2/3＝バリアのまわりの円（クリスタル側をまわる）、2/3〜1＝右の線
//  当たる範囲（広場の円＋入口の道）の外は消す。強さは床（Fuwa/ShockFloor）と同じ _Charge・_WaveR・_WaveFade
Shader "Fuwa/SafeVeil"
{
    Properties
    {
        _Color ("色", Color) = (1, 1, 1, 1)
        _Alpha ("いちばん濃い所", Range(0, 1)) = 0.22
        _Height ("幕の高さ(m)", Float) = 1.6
        _Len ("影の線の長さ(m、バリアから)", Float) = 30
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+3" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color; half _Alpha; float _Height, _Len;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Charge)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveR)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveFade)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Src)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Ball)
                UNITY_DEFINE_INSTANCED_PROP(float, _Pad)
                UNITY_DEFINE_INSTANCED_PROP(float, _Near)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Cen)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Corr)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float2 uv : TEXCOORD1; float sl : TEXCOORD2; UNITY_FOG_COORDS(3) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float4 src = UNITY_ACCESS_INSTANCED_PROP(Props, _Src);
                float4 ball = UNITY_ACCESS_INSTANCED_PROP(Props, _Ball);
                float pad = UNITY_ACCESS_INSTANCED_PROP(Props, _Pad), nr = UNITY_ACCESS_INSTANCED_PROP(Props, _Near);
                float2 b = ball.xz - src.xz; float bl = max(length(b), 0.01);
                float2 bh = b / bl, n = float2(-bh.y, bh.x);
                float s = v.uv.x;
                float2 p; float sl;   // sl＝ふちに沿った長さ（m、ゆらぎの模様用）
                // 形は FuwaSafeShape.cginc と同じ：円（半径 r0）と、クリスタルからその円に接する2本の線
                float r0 = ball.w + nr;
                float sa = saturate(r0 / bl), ca = sqrt(1 - sa * sa), ta = sa / max(ca, 0.05);
                float at = bl - r0 * sa;                         // 接する所の奥行き
                float phi0 = 0.5 * UNITY_PI + asin(sa);          // 接する所の角度（bh から。横より少しクリスタル側）
                if (s < 1.0 / 3.0)
                {
                    float along = lerp(at + _Len, at, s * 3.0);
                    p = bh * along + n * (along * ta);
                    sl = -(along - at);
                }
                else if (s < 2.0 / 3.0)
                {
                    float th = lerp(phi0, 2.0 * UNITY_PI - phi0, (s - 1.0 / 3.0) * 3.0);   // 左の接点 → クリスタル側 → 右の接点
                    p = b + r0 * (cos(th) * bh + sin(th) * n);
                    sl = (th - phi0) * r0;
                }
                else
                {
                    float along = lerp(at, at + _Len, (s - 2.0 / 3.0) * 3.0);
                    p = bh * along - n * (along * ta);
                    sl = (2.0 * UNITY_PI - 2.0 * phi0) * r0 + (along - at);
                }
                float y0 = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).y;
                float3 wp = float3(src.x + p.x, y0 + v.uv.y * _Height, src.z + p.y);
                if (ball.w <= 0) wp = float3(src.x, -10000, src.z);   // バリアなし：見えない所へ
                o.wp = wp; o.uv = v.uv; o.sl = sl;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float k = UNITY_ACCESS_INSTANCED_PROP(Props, _Charge);
                float wr = UNITY_ACCESS_INSTANCED_PROP(Props, _WaveR);
                float wf = UNITY_ACCESS_INSTANCED_PROP(Props, _WaveFade);
                float4 cen = UNITY_ACCESS_INSTANCED_PROP(Props, _Cen);
                float4 cor = UNITY_ACCESS_INSTANCED_PROP(Props, _Corr);
                // 当たる範囲（広場の円＋入口の道）の中だけ。ふちはぼかす
                float2 q = i.wp.xz - cen.xz;
                float inDisc = 1 - smoothstep(cen.w - 0.9, cen.w - 0.1, length(q));
                float inCor = 0;
                if (cor.w > 0)
                {
                    float2 dir = cor.xy; float along = dot(q, dir), lat = abs(q.x * dir.y - q.y * dir.x);
                    inCor = smoothstep(0, 0.6, along) * (1 - smoothstep(cor.w - 0.8, cor.w, along)) * (1 - smoothstep(cor.z - 0.5, cor.z, lat));
                }
                float zone = max(inDisc, inCor);
                // 強さ：床のセーフの線と同じ（ためている間と壁が来る前）
                float kk = k * k;
                float on = (k > 0.001 || wr >= 0) ? (0.35 + 0.65 * max(kk, wf)) : 0;
                float h = i.uv.y;
                float vfade = pow(saturate(1 - h), 1.6) * smoothstep(0.0, 0.06, h + 0.02);   // 2026-11-16 てじゃ「セーフティゾーンのフチ、上の方に黒い線」→ 上のふちで 1-h が少しマイナスになり pow が NaN（黒い点線）。saturate で 0〜1 に
                float t = _Time.y;
                float shimmer = 0.75 + 0.25 * sin(i.sl * 2.3 - t * 1.4 + h * 3.0) * sin(i.sl * 0.9 + t * 0.8);
                float a = _Alpha * vfade * shimmer * zone * on;
                if (a < 0.002) discard;
                half4 o = half4(_Color.rgb * a, a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, o, half4(0, 0, 0, 0));
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
