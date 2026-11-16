// 2026-11-15 2–5 クリスタルの広場：床の魔法陣（てじゃ「見た目クオリティあげてく」の3）
//  床にぴったり重ねる板（Offset で床より手前に描く・ZWrite Off）。uv は板のまん中から -1〜1、_Radius m が uv 1
//  ふだんは彫った線（暗い）。ため _Charge（0〜1）で外から内へ光が走る。ドン _Flash で全体が光って消える
//  模様：二重の輪・スポークと小さい輪・目盛り・ルーン（乱数のマス）
Shader "Fuwa/MagicCircle"
{
    Properties
    {
        _Groove ("彫った線の色（掛ける）", Color) = (0.72, 0.62, 0.5, 1)
        _GrooveA ("彫った線の濃さ", Range(0, 1)) = 0.55
        _Glow ("光の色", Color) = (0.45, 0.9, 1, 1)
        _Radius ("板の半径(m)", Float) = 9
        _R0 ("模様の内がわ(m)", Float) = 2.5
        _Charge ("ため（スクリプト）", Range(0, 1)) = 0
        _Flash ("ドン（スクリプト）", Range(0, 1)) = 0
        _LineW ("線のはば(m)", Float) = 0.06
        _DarkGlow ("ためきった時の光の色", Color) = (0.1, 0.03, 0.16, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+30" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite Off
            Offset -2, -2
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Groove, _Glow, _DarkGlow; half _GrooveA; float _Radius, _R0, _LineW;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Charge)
                UNITY_DEFINE_INSTANCED_PROP(float, _Flash)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            // r(m) の位置に はば w の線
            float Ring(float r, float at, float w, float aa) { return 1 - smoothstep(w * 0.5, w * 0.5 + aa, abs(r - at)); }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float charge = UNITY_ACCESS_INSTANCED_PROP(Props, _Charge);
                float flash = UNITY_ACCESS_INSTANCED_PROP(Props, _Flash);
                float r = length(i.uv) * _Radius;
                float ang = atan2(i.uv.y, i.uv.x);          // -π〜π
                float aa = max(fwidth(r), 0.004);
                float w = _LineW;
                float R = _Radius;
                // 帯の位置（m）
                float b0 = _R0, b1 = _R0 + 0.4, b2 = R * 0.6, b3 = R * 0.6 + 0.4, b4 = R - 0.75, b5 = R - 0.45;
                float m = 0;
                m = max(m, Ring(r, b0, w, aa)); m = max(m, Ring(r, b1, w, aa));
                m = max(m, Ring(r, b2, w, aa)); m = max(m, Ring(r, b3, w * 1.4, aa));
                m = max(m, Ring(r, b4, w * 1.4, aa)); m = max(m, Ring(r, b5, w, aa));
                // スポーク 12本（b1〜b2）と、そのまん中の小さい輪
                float seg = 6.2831853 / 12;
                float sa = (frac((ang + seg * 0.5) / seg) - 0.5) * seg;   // スポークからの角度
                float sd = abs(sa) * r;                                   // スポークからの距離(m)
                if (r > b1 && r < b2) m = max(m, 1 - smoothstep(w * 0.5, w * 0.5 + aa, sd));
                {
                    float mr = (b1 + b2) * 0.5;
                    float2 sp = float2(cos(ang - sa), sin(ang - sa)) * mr;  // いちばん近いスポークの上の点
                    float2 pp = i.uv * _Radius;
                    float dd = length(pp - sp);
                    m = max(m, Ring(dd, 0.42, w, aa));
                    m = max(m, (1 - smoothstep(0.12, 0.12 + aa, dd)) * 0.9);
                }
                // スポークの間の三角（b1〜b2 の外がわ寄り、ななめの線）
                {
                    float tseg = seg;
                    float ta = (frac(ang / tseg) - 0.5) * tseg;   // スポークの間のまん中から
                    float u = (r - b1) / (b2 - b1);
                    float edge = abs(ta) * r - (1 - u) * (tseg * 0.5 * r) * 0.85;
                    if (u > 0.15 && u < 1) m = max(m, (1 - smoothstep(w * 0.5, w * 0.5 + aa, abs(edge))) * step(0, ta * 0 + 1));
                }
                // 目盛り（b3〜b4 の内がわのふち）
                {
                    float tk = 6.2831853 / 96;
                    float ka = (frac(ang / tk + 0.5) - 0.5) * tk * r;
                    float long_ = step(0.5, frac(floor(ang / tk + 0.5) / 4 + 0.01) < 0.25 ? 1 : 0);
                    float tl = lerp(0.18, 0.35, long_);
                    if (r > b3 && r < b3 + tl) m = max(m, 1 - smoothstep(w * 0.45, w * 0.45 + aa, abs(ka)));
                }
                // ルーン（b3+0.45 〜 b4-0.15 の帯を角度で区切ったマスに、乱数の短い線）
                {
                    float rr0 = b3 + 0.45, rr1 = b4 - 0.15;
                    if (r > rr0 && r < rr1)
                    {
                        float cells = 40;
                        float cid = floor((ang + 3.14159) / 6.2831853 * cells);
                        float cu = frac((ang + 3.14159) / 6.2831853 * cells);       // マスの中の 0〜1（角度）
                        float cv = (r - rr0) / (rr1 - rr0);                          // マスの中の 0〜1（半径）
                        float cw = 6.2831853 / cells * r;                            // マスのはば(m)
                        float2 q = float2((cu - 0.5) * cw, (cv - 0.5) * (rr1 - rr0));
                        float hh = Hash(float2(cid, 3.1));
                        float g = 0;
                        // 縦線・横線・ななめを乱数で組み合わせる
                        if (hh > 0.15) g = max(g, 1 - smoothstep(w * 0.5, w * 0.5 + aa, abs(q.x + (Hash(float2(cid, 7)) - 0.5) * cw * 0.4)));
                        if (Hash(float2(cid, 11)) > 0.4) g = max(g, (1 - smoothstep(w * 0.5, w * 0.5 + aa, abs(q.y - (Hash(float2(cid, 13)) - 0.5) * 0.5))) * step(abs(q.x), cw * 0.3));
                        if (Hash(float2(cid, 17)) > 0.6) g = max(g, (1 - smoothstep(w * 0.5, w * 0.5 + aa, abs(q.x - q.y))) * step(abs(q.y), 0.3));
                        g *= step(abs(q.y), (rr1 - rr0) * 0.42) * step(abs(q.x), cw * 0.38);
                        m = max(m, g);
                    }
                }
                m = saturate(m) * step(r, R);
                // ため：外から内へ光が走る（先頭が少し明るい）
                float front = R - charge * (R - b0 + 0.6);
                float lit = smoothstep(front - 0.5, front + 0.3, r) * step(0.001, charge);
                float head = exp(-pow((r - front) * 2.2, 2)) * step(0.001, charge) * step(charge, 0.999);
                float glowK = saturate(lit * (0.55 + 0.45 * charge) + head * 1.2 + flash);
                // 彫った線（床の色を暗く）＋光
                // 2026-11-16 てじゃ「模様の光もどんどん黒くできたりする？」→ ためるほど光の色が黒紫へ（ドンの光は水色のまま）
                float dk = saturate(charge * charge * 1.2) * (1 - saturate(flash * 2));
                half3 glowC = lerp(_Glow.rgb * (1.2 + 1.5 * head), _DarkGlow.rgb * (1 + 1.5 * head), dk) + _Glow.rgb * flash * 1.5;
                half a = m * lerp(_GrooveA, 1, glowK);
                half3 c = lerp(_Groove.rgb * 0.35, glowC, glowK) * a;
                half4 o = half4(c, a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, o, half4(0, 0, 0, 0));
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
