// 2026-11-15 2–5 クリスタルの広場：衝撃波の範囲を床に出す（てじゃ「衝撃波の範囲、もっと目立たせたいな、特に床」）
//  広場の床にかぶせる円盤。スクリプト（FuwaShockwave）が MPB で
//   _Charge（0〜1 ため）、_WaveR（広がる壁の半径、<0 ならなし）、_WaveFade、_Src（クリスタル）、_Ball（バリア xyz・w=半径、w=0 ならなし）、_Pad・_Near
//  ため：外へ流れる光の輪が床いっぱいに（ためるほど濃く・速く）。バリアの影（セーフ）だけ抜けて、ふちに白い線
//  ドン：壁の所に明るい帯＋通ったあとにうすい光の尾。影の所は光らない
Shader "Fuwa/ShockFloor"
{
    Properties
    {
        _Color ("色", Color) = (0.45, 0.9, 1, 1)
        _SafeColor ("セーフのふちの色", Color) = (1, 1, 1, 1)
        _Radius ("広場の半径", Float) = 9
        _Charge ("ため（スクリプト）", Range(0, 1)) = 0
        _WaveR ("壁の半径（スクリプト）", Float) = -1
        _WaveFade ("壁の濃さ（スクリプト）", Range(0, 1)) = 0
        _Src ("出どころ（スクリプト）", Vector) = (0, 0, 0, 0)
        _Ball ("バリア（スクリプト、w=半径）", Vector) = (0, 0, 0, 0)
        _Pad ("影の横のゆとり", Float) = 0.45
        _Near ("バリアのまわりのゆとり", Float) = 0.8
        _Strength ("全体の強さ", Float) = 1
        _ChargeGain ("ためている時の黒い模様の強さ", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+2" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            Offset -2, -2
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "FuwaSafeShape.cginc"
            half4 _Color, _SafeColor; float _Radius, _Pad, _Near, _Strength, _ChargeGain;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Charge)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlowPh)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlickPh)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveR)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveFade)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Src)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Ball)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 cen : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.cen = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float k = UNITY_ACCESS_INSTANCED_PROP(Props, _Charge);
                float wr = UNITY_ACCESS_INSTANCED_PROP(Props, _WaveR);
                float wf = UNITY_ACCESS_INSTANCED_PROP(Props, _WaveFade);
                float4 src = UNITY_ACCESS_INSTANCED_PROP(Props, _Src);
                float4 ball = UNITY_ACCESS_INSTANCED_PROP(Props, _Ball);
                float2 q = i.wp.xz - src.xz;
                float r = length(q);
                float rc = length(i.wp.xz - i.cen.xz);
                float edge = 1 - smoothstep(_Radius - 0.6, _Radius, rc);
                // バリアの影（セーフ）と、そのふちまでの距離（＋なら外）
                float safe = 0, dEdge = 99;
                if (ball.w > 0)
                {
                    dEdge = FuwaSafeDist(q, ball.xz - src.xz, ball.w + _Near);   // FuwaSafeShape.cginc
                    safe = 1 - smoothstep(-0.05, 0.05, dEdge);
                }
                float t = _Time.y;
                // ため：外へ流れる輪（ためるほど速く・こく）
                float kk = k * k;
                // 2026-11-15 てじゃ「チャージのときの地面の黒いグラデーション、やけにガクガクしてる」
                //  前は t×（ためで変わる速さ）だったので、速さが変わるたびに大きい t がかかって模様が飛んでいた → 進んだ量はスクリプトが足していく（_FlowPh・_FlickPh）
                float flow = frac(r * 0.9 - UNITY_ACCESS_INSTANCED_PROP(Props, _FlowPh));
                float bands = smoothstep(0.0, 0.12, flow) * pow(saturate(1 - flow), 3);
                float charge = kk * (0.38 + 0.9 * bands) * (0.85 + 0.15 * sin(UNITY_ACCESS_INSTANCED_PROP(Props, _FlickPh))) * _ChargeGain;   // 2026-11-16 てじゃ「地面のチャージ時の黒いグラデ、もっとつよく」
                // セーフのふちの線（ためている間と壁が来る前）
                float sline = (k > 0.001 || wr >= 0) ? (1 - smoothstep(0.03, 0.11, abs(dEdge))) * (0.35 + 0.65 * max(kk, wf)) : 0;
                // ドン：壁の帯＋通ったあとの尾
                float front = 0, trail = 0;
                if (wr >= 0)
                {
                    float d = r - wr;
                    front = exp(-d * d / 0.35) * wf;
                    trail = (d < 0 ? exp(d / 2.2) * 0.45 : 0) * wf;
                }
                float danger = (charge + front * 1.6 + trail) * (1 - safe) * edge;
                half3 col = _Color.rgb * danger + _SafeColor.rgb * sline * edge * 0.9;
                float a = saturate((danger * 0.8 + sline * 0.7) * edge) * _Strength;
                col *= _Strength;
                half4 o = half4(col, a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, o, half4(0, 0, 0, 0));
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
