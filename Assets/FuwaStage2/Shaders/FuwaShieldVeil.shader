// 2026-11-15 2–5 上の段：バリアの門のアーチの中の幕（てじゃ「バリア付与カーテンとこもなんかほしいな」「バリア模様？」）
//  アーチの口の形のメッシュ（物体の座標 x・y＝メートル）にかける。六角形のハチの巣もようの光の膜
//  ・六角形のふちが光る、下から上へ光の波が流れる、ところどころの六角形がふわっと明るくなる
//  ・ふちのほうは濃く、まん中はうすい（通れる感じ）
//  ・ふわふわがくぐった時：FuwaShieldGate が _EmissionColor を明るくする → 全体がぱっと光る
Shader "Fuwa/ShieldVeil"
{
    Properties
    {
        _Color ("色", Color) = (0.45, 0.9, 1, 1)
        _Cell ("六角形の大きさ（m）", Float) = 0.42
        _Alpha ("濃さ", Range(0, 1)) = 0.55
        _Width ("口のはば（m、ふちのフェード用）", Float) = 4.2
        _Height ("口の高さ（m）", Float) = 5.6
        _EmissionColor ("光（スクリプト）", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color; float _Cell, _Width, _Height; half _Alpha;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 op : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.op = v.vertex.xy;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float H2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float HexD(float2 p) { p = abs(p); return max(dot(p, float2(0.8660254, 0.5)), p.y); }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 em = UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor).rgb;
                float flash = saturate((max(em.r, max(em.g, em.b)) - 0.6) / 1.8);   // 門の光：ふだん 0.5、くぐった時 〜2.7
                float t = _Time.y;
                // ハチの巣：2つのずらした格子の近いほう
                float2 s = float2(1.7320508, 1.0);
                float2 p = i.op / _Cell;
                float2 a = (frac(p / s) - 0.5) * s;
                float2 b = (frac((p - s * 0.5) / s) - 0.5) * s;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                float2 id = p - gv;
                float d = HexD(gv);                       // 0＝まん中、0.5＝ふち
                float edge = smoothstep(0.40, 0.49, d);   // 六角形のふちの線
                // 下から上へ流れる光の波＋六角形ごとのまたたき
                float wave = 0.5 + 0.5 * sin(i.op.y * 2.2 - t * 2.4 + id.x * 0.15);
                float tw = step(0.86, frac(H2(floor(id * 10.0 + 0.5)) + t * 0.18));
                // 口のふちのほうが濃い（まん中は通れる感じにうすく）
                float ex = abs(i.op.x) / (_Width * 0.5);
                float ey = i.op.y / _Height;
                float rim = saturate(max(ex, ey * 0.9) * 1.25 - 0.25);
                float baseA = _Alpha * (0.10 + 0.35 * rim);
                float a1 = baseA + edge * _Alpha * (0.35 + 0.45 * wave) + tw * (1 - d * 1.6) * 0.18 + flash * (0.35 + 0.6 * edge);
                // 床ぎわは消す
                a1 *= smoothstep(0.0, 0.25, i.op.y);
                a1 = saturate(a1);
                half3 col = _Color.rgb * (0.8 + 0.6 * edge + 1.2 * flash) + edge * 0.25;
                half4 o = half4(col * a1, a1 * 0.9);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, o, half4(0, 0, 0, 0));
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
