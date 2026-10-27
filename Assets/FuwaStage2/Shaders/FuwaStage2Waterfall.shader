// 滝のカーテン（ステージ2、2026-10-17）。縦に落ちる白い筋と、上下のふちの泡。少しだけ透ける
// 板（Quad）の uv：x＝横、y＝下0〜上1。落ちる速さは _FallSpeed（m/s）、筋の間隔は板の大きさ（行列）から
// _Curved=1：曲がった滝のメッシュ用（2026-10-19、2–1 の落ち口がRで張り出す滝）。uv1＝(横 m, 下からの長さ m)、uv2＝(全体の幅 m, 全体の長さ m)
Shader "FuwaStage2/Waterfall"
{
    Properties
    {
        _Top ("Top Color", Color) = (0.50, 0.84, 0.96, 1)
        _Bottom ("Bottom Color", Color) = (0.30, 0.66, 0.90, 1)
        _Foam ("Foam Color", Color) = (0.97, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.92
        _FallSpeed ("Fall Speed (m/s)", Float) = 3.0
        _StreakScale ("Streak Size", Float) = 1.6
        _EdgeFoam ("Top/Bottom Foam (m)", Float) = 0.8
        [Toggle] _Curved ("Curved Mesh (uv1/uv2)", Float) = 0
        // 厚みのある滝（2026-10-25 てじゃ「各種滝、太くしたい」「最初の滝も、太いっていうより２重ってかんじ」）：
        // 前・後ろ・横の面で閉じた形を、透けさせず手前の面だけ描く（Cull Back・ZWrite On・Alpha 1）。横の面は頂点カラーで少し暗く
        _SideFade ("Side Edge Fade (m, 0=none)", Float) = 0.12
        [Toggle] _UseVColor ("Multiply Vertex Color", Float) = 0
        // 上のはしから _PondTop m（落ち口）までは池の水の色（落ち口の手前の平らな所。2026-10-27）
        _PondTop ("Pond Part From Top (m, 0=none)", Float) = 0
        _PondColor ("Pond Color", Color) = (0.26, 0.66, 0.86, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
    }
    SubShader
    {
        // DisableBatching：板の大きさを行列から取るので、まとめ描き（ダイナミックバッチング）されると 1m×1m 扱いになって
        // 筋と泡が変になる。どの滝が同時に映るかで切り替わって見えた（2026-10-23 てじゃ「距離とか向きとかで変に切り替わる」）
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        Cull [_Cull]
        ZWrite [_ZWrite]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Top, _Bottom, _Foam, _PondColor;
            float _Alpha, _FallSpeed, _StreakScale, _EdgeFoam, _Curved, _SideFade, _UseVColor, _PondTop;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float2 m : TEXCOORD1; float2 tot : TEXCOORD2; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 size : TEXCOORD1; UNITY_FOG_COORDS(2) fixed4 color : TEXCOORD3; float ny : TEXCOORD4; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.size = float2(length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0))), length(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0))));
                // 厚みのある滝の横の面（ローカル x 向きの面）は、横＝奥行き（z）の長さ（筋が横に詰まらないように）
                if (_UseVColor > 0.5 && abs(v.normal.x) > 0.5) o.size.x = length(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1)));
                if (_Curved > 0.5) { o.size = v.tot; o.uv = v.m / max(v.tot, 0.001); }
                o.color = _UseVColor > 0.5 ? v.color : fixed4(1, 1, 1, 1);
                o.ny = UnityObjectToWorldNormal(v.normal).y;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 m = i.uv * i.size;   // メートル
                float s = _StreakScale;
                float3 col = lerp(_Bottom.rgb, _Top.rgb, i.uv.y);
                // 縦の白い筋：横に細かく、縦に長いノイズを下へ流す（2つ重ねて速さをずらす）
                float n = Noise(float2(m.x * 7.0 / s, (m.y + _Time.y * _FallSpeed) * 0.8 / s)) * 0.6
                        + Noise(float2(m.x * 11.0 / s + 3.1, (m.y + _Time.y * _FallSpeed * 1.3) * 1.1 / s)) * 0.4;
                col = lerp(col, _Foam.rgb, smoothstep(0.62, 0.74, n) * 0.6);   // 2026-10-27 てじゃ「白い模様をすこし控えめに」「もっとすくなくても」0.48〜0.62×0.85 → 0.55〜0.68×0.7 →
                // 上のふち（落ち口）と下（滝つぼに当たる所）の泡
                // 2026-10-27 まっすぐな帯に見えたので、ふちをノイズでぎざぎざに。下はわき上がるしぶき（上へ流れるノイズで高さがゆらぐ）
                float topN = Noise(float2(m.x * 3.0, (m.y + _Time.y * _FallSpeed) * 1.2));
                float top = 1 - smoothstep(0, _EdgeFoam * (0.45 + 0.7 * topN), (1 - i.uv.y) * i.size.y);
                float botN = Noise(float2(m.x * 2.6, m.y * 1.4 - _Time.y * 2.2)) * 0.65 + Noise(float2(m.x * 5.3 + 2.0, m.y * 2.5 - _Time.y * 3.1)) * 0.35;
                float bh = _EdgeFoam * 1.5 * (0.6 + 0.9 * botN);
                float bot = 1 - smoothstep(bh * 0.55, bh, i.uv.y * i.size.y);
                if (_PondTop > 0.001)
                {
                    // 落ち口の手前は池の色、落ち口へ近づくと白い筋の滝の色へ。上のはし（池の下へもぐる所）の泡は出さない
                    float dT = (1 - i.uv.y) * i.size.y;
                    float pk = 1 - smoothstep(_PondTop - 0.1, _PondTop + 0.35, dT);
                    pk *= smoothstep(0.3, 0.7, i.ny);   // 上向きの面だけ（滝の裏から見える下向きの面は滝の色のまま。2026-10-27 てじゃ「滝の裏からみたとき上が違和感」）
                    col = lerp(col, _PondColor.rgb, pk);
                    top = 0;
                }
                col = lerp(col, _Foam.rgb, saturate(top * 0.9 + bot));
                // 左右のふちは少し薄く（厚みのある滝は横の面があるので薄くしない）
                float side = _SideFade > 0.0001 ? smoothstep(0, _SideFade, min(i.uv.x, 1 - i.uv.x) * i.size.x) : 1;
                col *= i.color.rgb;
                fixed4 c = fixed4(col, _Alpha * side);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
