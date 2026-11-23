// 2026-11-23 あそびばのおもちゃポータルの裏（てじゃ「裏の真っ黒板、わかりやすいんだけど なんか怖い」「裏も渦だと分かりづらい」）
//  うずまき（表）とはっきりちがう「閉じたふた」：うずなし。うすいラベンダーの上下グラデ＋ふちへ少し濃く＋ゆっくりまたたく小さい星
//  メッシュは表の穴（Swirl）と同じだ円。lpos（メッシュの座標）で模様を作る。不透明・片面（裏から見た時だけ見える向きに置く）
Shader "Fuwa/PortalBackSoft"
{
    Properties
    {
        _Top ("上の色", Color) = (0.86, 0.84, 0.97, 1)
        _Bottom ("下の色", Color) = (0.72, 0.76, 0.95, 1)
        _Edge ("ふちの色", Color) = (0.6, 0.62, 0.88, 1)
        _HalfW ("だ円の半はば(m)", Float) = 1.16
        _HalfH ("だ円の半高さ(m)", Float) = 1.56
        _Star ("星の色", Color) = (1, 1, 1, 1)
        _StarDensity ("星の数（1mあたり）", Float) = 3.5
        _StarSize ("星の大きさ", Range(0.02, 0.3)) = 0.12
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Top, _Bottom, _Edge, _Star; float _HalfW, _HalfH, _StarDensity, _StarSize;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 lpos : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.lpos = v.vertex.xyz; UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float H2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 frag (v2f i) : SV_Target
            {
                float2 q = i.lpos.xy;
                float rn = length(q / float2(_HalfW, _HalfH));          // 0 まん中 → 1 ふち
                float3 col = lerp(_Bottom.rgb, _Top.rgb, saturate(q.y / _HalfH * 0.5 + 0.5));
                col = lerp(col, _Edge.rgb, smoothstep(0.55, 1.0, rn) * 0.7);
                col += 0.06 * (1 - rn);                                   // まん中をほんのり明るく
                // 星：ます目ごとに1つ、ゆっくりまたたく（4つの光の十字）
                float2 g = q * _StarDensity; float2 cell = floor(g); float2 f = frac(g) - 0.5;
                float h = H2(cell);
                float2 off = (float2(H2(cell + 3.1), H2(cell + 7.7)) - 0.5) * 0.6;
                float2 d = f - off;
                float tw = 0.5 + 0.5 * sin(_Time.y * (0.8 + h * 1.6) + h * 30.0);
                float s = _StarSize * (0.5 + 0.8 * h) * tw * step(0.45, h);
                float cross = max(0, 1 - abs(d.x) / (s * 0.18 + 1e-4)) * max(0, 1 - abs(d.y) / (s + 1e-4))
                            + max(0, 1 - abs(d.y) / (s * 0.18 + 1e-4)) * max(0, 1 - abs(d.x) / (s + 1e-4));
                float dotc = max(0, 1 - length(d) / (s * 0.35 + 1e-4));
                col = lerp(col, _Star.rgb, saturate(cross * 0.7 + dotc) * (1 - smoothstep(0.8, 0.95, rn)));
                half4 o = half4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
