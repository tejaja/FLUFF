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
        _ArchR ("上の半円の半径（m）", Float) = 1.9
        _RimW ("ふちから模様が見えるはば（m）", Float) = 0.9
        _EmissionColor ("光（スクリプト）", Color) = (1, 1, 1, 1)
        _FadeNear ("カメラからこれより近いと見えない（m）", Float) = 0.45
        _FadeFar ("これより遠いとふつうに見える（m）", Float) = 1.0
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
            half4 _Color; float _Cell, _Width, _Height, _ArchR, _RimW; half _Alpha; float _FadeNear, _FadeFar;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            // インスタンスIDとステレオの順は D3D11（PC VR）とそれ以外（Quest）で逆が正解（memory: v2f のステレオとインスタンスIDの順）
            #if defined(SHADER_API_D3D11)
            struct v2f { float4 pos : SV_POSITION; float2 op : TEXCOORD0; float3 wp : TEXCOORD2; UNITY_FOG_COORDS(1) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            #else
            struct v2f { float4 pos : SV_POSITION; float2 op : TEXCOORD0; float3 wp : TEXCOORD2; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO UNITY_VERTEX_INPUT_INSTANCE_ID };
            #endif
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.op = v.vertex.xy;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
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
                // 2026-11-15 てじゃ「六角形マークがチラチラしてる青いカーテンくらいがいいかな」
                //  → ほかのカーテン（大きさのカーテン）と同じく、下ほど濃く上へ消える青い幕。六角形はところどころの1つずつがチラッと光る（全面の網目はなし）
                float hw = _Width * 0.5, colH = _Height - _ArchR;
                float dIn = i.op.y < colH ? hw - abs(i.op.x) : _ArchR - length(float2(i.op.x, i.op.y - colH));   // 枠までの距離（m）
                float side = smoothstep(0.0, 0.35, dIn);
                float y01 = saturate(i.op.y / _Height);
                float vfade = pow(saturate(1 - y01), 1.3) * (0.55 + 0.45 * saturate(y01 * 6));
                float shimmer = 0.85 + 0.15 * sin(i.op.y * 3.0 - t * 1.8 + i.op.x * 0.7);
                float curtain = _Alpha * 1.05 * vfade * shimmer;
                // 六角形のマーク：セルごとにばらばらのタイミングで ふっと出て消える
                float hsh = H2(floor(id * 10.0 + 0.5));
                float blink = saturate(sin(t * (1.3 + hsh * 1.7) + hsh * 40.0) * 7.0 - 6.0);   // たまに 0→1→0（少なめ）
                float mark = blink * (edge * 0.9 + (1 - smoothstep(0.30, 0.46, d)) * 0.18);
                float a1 = (curtain + mark * (0.15 + 0.85 * vfade)) * side + flash * (0.3 + 0.5 * edge) * side;
                edge = mark;   // 下の色計算で、光っている六角形だけ明るく
                // 床ぎわは消す
                a1 *= smoothstep(0.0, 0.25, i.op.y);
                // 2026-11-24 てじゃ「クモの巣とかみたいに至近距離できえてほしい」：カメラのすぐ前では消える（くぐる時に視界をふさがない）
                a1 *= smoothstep(_FadeNear, max(_FadeFar, _FadeNear + 0.01), distance(i.wp, _WorldSpaceCameraPos));
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
