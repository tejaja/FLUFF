// 2026-11-15 2–5 クリスタルの衝撃波：光の壁のあとを追う砂ぼこりの輪（見た目クオリティの7）
//  たての帯（uv.x＝ぐるっと一周 0〜1、uv.y＝高さ 0〜1）にかける。ふわふわしたけむり（値ノイズ2段）が下から湧いて上へ消える
//  バリアの影は Fuwa/Shockwave と同じく欠ける（_Src・_Ball・_Pad）。_Alpha・_Age（0〜1）はスクリプト
Shader "Fuwa/DustRing"
{
    Properties
    {
        _Color ("けむりの色", Color) = (0.74, 0.63, 0.5, 1)
        _Alpha ("濃さ（スクリプト）", Range(0, 1)) = 0
        _Src ("出どころ（スクリプト）", Vector) = (0, 0, 0, 0)
        _Ball ("バリア（スクリプト、w=半径）", Vector) = (0, 0, 0, 0)
        _Pad ("影のゆとり", Float) = 0.2
        _Cells ("一周のけむりの数", Float) = 70
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+8" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color; float _Pad, _Cells;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Src)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Ball)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float H(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float VN(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(H(i), H(i + float2(1, 0)), f.x), lerp(H(i + float2(0, 1)), H(i + float2(1, 1)), f.x), f.y); }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float alpha = UNITY_ACCESS_INSTANCED_PROP(Props, _Alpha);
                float4 src = UNITY_ACCESS_INSTANCED_PROP(Props, _Src);
                float4 ball = UNITY_ACCESS_INSTANCED_PROP(Props, _Ball);
                float y = saturate(i.uv.y);
                float2 p = float2(i.uv.x * _Cells, y * 2.2 - _Time.y * 1.3);
                float n = VN(p) * 0.65 + VN(p * 2.3 + 7.1) * 0.35;
                float puff = smoothstep(0.25 + y * 0.45, 0.75, n);
                float a = puff * pow(1 - y, 1.6) * alpha;
                // バリアの影
                if (ball.w > 0)
                {
                    float2 q = i.wp.xz - src.xz, b = ball.xz - src.xz; float bl = length(b);
                    if (bl > 0.01)
                    {
                        float2 bh = b / bl; float along = dot(q, bh); float perp = length(q - bh * along);
                        float w = ball.w * along / bl + _Pad;
                        a *= 1 - smoothstep(w + 0.1, w - 0.1, perp) * smoothstep(bl - 0.2, bl + 0.1, along);
                    }
                }
                half4 c = half4(_Color.rgb * (0.85 + 0.25 * n), saturate(a));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
