// 2026-11-15 2–5 上の段：クリスタルの衝撃波（てじゃ「一定間隔で広場全体に放射状に衝撃波」「ふわふわのバリアの周囲+後ろ側はセーフゾーン」）
//  広がる光の壁（たての帯）と床の輪。uv.y＝0 がいちばん濃い所（帯の下・輪の外のふち）〜 1 で消える
//  バリアの影（クリスタル _Src から見てバリア _Ball の後ろ、上から見た2D）は欠ける＝セーフな所が見える。FuwaShockwave の判定と同じ形
//  _Ball.w＝バリアの半径（0 で影なし）、_Pad＝影の横のゆとり、_Alpha＝全体の濃さ（スクリプト）
Shader "Fuwa/Shockwave"
{
    Properties
    {
        _Color ("色", Color) = (0.55, 0.92, 1, 1)
        _Core ("芯の色", Color) = (1, 1, 1, 1)
        _Alpha ("濃さ（スクリプト）", Range(0, 1)) = 1
        _Src ("出どころ（スクリプト）", Vector) = (0, 0, 0, 0)
        _Ball ("バリア（スクリプト、w=半径）", Vector) = (0, 0, 0, 0)
        _Pad ("影のゆとり", Float) = 0.2
        _Streak ("すじの細かさ", Float) = 40
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "FuwaSafeShape.cginc"
            half4 _Color, _Core; float _Pad, _Streak;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Src)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Ball)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float alpha = UNITY_ACCESS_INSTANCED_PROP(Props, _Alpha);
                float4 src = UNITY_ACCESS_INSTANCED_PROP(Props, _Src);
                float4 ball = UNITY_ACCESS_INSTANCED_PROP(Props, _Ball);
                float y = saturate(i.uv.y);
                // 下（外のふち）が濃く、上（内がわ）へ消える。芯は細く白い
                float body = pow(1 - y, 1.8);
                float core = exp(-y * 14);
                // たてのすじ（ゆらぐ）
                float2 q = i.wp.xz - src.xz;
                float ang = atan2(q.y, q.x);
                float st = 0.75 + 0.25 * sin(ang * _Streak + _Time.y * 9) * sin(ang * _Streak * 0.37 - _Time.y * 5);
                float a = (body * 0.8 * st + core * 0.7) * alpha;
                // バリアの影
                float shade = 1;
                if (ball.w > 0)
                {
                    float d = FuwaSafeDist(q, ball.xz - src.xz, ball.w + _Pad);   // FuwaSafeShape.cginc
                    shade = 1 - smoothstep(0.06, -0.06, d);
                    // 影のふちを少し光らせる（守られている所が分かるように）
                    a += exp(-pow(d * 10, 2)) * 0.25 * alpha * body;
                }
                a *= shade;
                a = saturate(a);
                half3 col = lerp(_Color.rgb, _Core.rgb, saturate(core * 1.2));
                return half4(col * a, a);
            }
            ENDCG
        }
    }
    FallBack Off
}
