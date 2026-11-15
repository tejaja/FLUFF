// 2026-11-15 2–5 大きさのカーテン（てじゃ「カーテンをもっと目立つようにグラデ演出、巨大化は中心から外に広がる円、小型化は逆、通常化はうずまき」）
//  Quad（uv 0〜1）にかける。_Mode 1＝外へ広がる輪、-1＝内へちぢむ輪、0＝回るうずまき。下ほど濃く上へ消える
//  ふわふわがくぐった時の光：FuwaSizeRune が _EmissionColor を MPB で明るくする（明るさで全体を強く）
Shader "Fuwa/SizeCurtain"
{
    Properties
    {
        _Color ("色", Color) = (1, 0.6, 0.25, 1)
        _Mode ("模様（1 外へ / -1 内へ / 0 うずまき）", Float) = 1
        _Freq ("輪の細かさ", Float) = 3.2
        _Speed ("速さ", Float) = 0.55
        _Arms ("うずまきの腕", Float) = 3
        _Alpha ("全体の濃さ", Range(0, 1)) = 0.6
        _EmissionColor ("光（スクリプト）", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color; float _Mode, _Freq, _Speed, _Arms; half _Alpha;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float aspect : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float sx = length(unity_ObjectToWorld._m00_m10_m20), sy = length(unity_ObjectToWorld._m01_m11_m21);
                o.aspect = sx / max(sy, 1e-3);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 em = UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor).rgb;
                float flash = saturate((max(em.r, max(em.g, em.b)) - 1.3) / 2.2);   // ふだん ~1.2、くぐった時 ~3.7
                float2 p = (i.uv - float2(0.5, 0.42)) * float2(i.aspect, 1);
                float r = length(p);
                float ang = atan2(p.y, p.x) / 6.2831853;
                float t = _Time.y * _Speed;
                float w;
                if (_Mode > 0.5) w = frac(r * _Freq - t);
                else if (_Mode < -0.5) w = frac(r * _Freq + t);
                else w = frac(ang * _Arms + r * _Freq * 0.7 - t * 0.8);
                // やわらかい帯（先が明るく、うしろへ尾を引く）
                float band = smoothstep(0.0, 0.08, w) * pow(1 - w, 2.2);
                // 縦のグラデ（下が濃く上へ消える）と左右のふち
                float y = i.uv.y;
                float vfade = saturate(1 - y) * (0.55 + 0.45 * saturate(y * 6));
                float side = smoothstep(0.0, 0.06, i.uv.x) * smoothstep(1.0, 0.94, i.uv.x);
                float a = (_Alpha * (0.35 + 0.9 * band) + flash * 0.5) * vfade * side;
                half3 col = _Color.rgb * (0.7 + 0.8 * band + flash) + band * 0.35;
                a = saturate(a);
                half4 o = half4(col * a, a * 0.85);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, o, half4(0, 0, 0, 0));
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
