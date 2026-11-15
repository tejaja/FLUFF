// 2026-11-15 2–5 クリスタルの広場：ためでクリスタルに黒いオーラ（てじゃ「チャージでクリスタルも黒オーラみたいな色つけよ」）
//  クリスタルをつつむ球。ふちほど濃い（フレネル）黒紫のもや＋上へ流れるけむりのムラ＋ふちに紫の光。重ねて暗くする（加算ではない）
//  _Alpha はスクリプト（FuwaShockwave）が MPB で
Shader "Fuwa/DarkAura"
{
    Properties
    {
        _Color ("もやの色", Color) = (0.06, 0.02, 0.10, 1)
        _Rim ("ふちの光", Color) = (0.55, 0.18, 0.85, 1)
        _Alpha ("濃さ（スクリプト）", Range(0, 1.5)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+3" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color, _Rim;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float3 op : TEXCOORD2; UNITY_FOG_COORDS(3) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.op = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float H(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
            float VN(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                float a = lerp(lerp(H(i), H(i + float3(1, 0, 0)), f.x), lerp(H(i + float3(0, 1, 0)), H(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(H(i + float3(0, 0, 1)), H(i + float3(1, 0, 1)), f.x), lerp(H(i + float3(0, 1, 1)), H(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float al = UNITY_ACCESS_INSTANCED_PROP(Props, _Alpha);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = saturate(abs(dot(normalize(i.wn), v)));
                float rim = pow(1 - ndv, 1.6);
                float t = _Time.y;
                // 上へ流れるけむりのムラ（物体の座標なので回っても流れは上）
                float3 q = float3(i.op.x * 3.0, i.op.y * 2.2 - t * 1.6, i.op.z * 3.0);
                float n = VN(q) * 0.65 + VN(q * 2.3 + 4.1) * 0.35;
                // 泡みたいなふちの線にならないように：まん中ほど濃く、ふちはふわっと消える。けむりのムラを強めに
                float soft = smoothstep(0.0, 0.65, ndv);
                float wisp = smoothstep(0.25, 0.85, n);
                float a = al * soft * (0.25 + 0.95 * wisp);
                a = saturate(a);
                half3 col = lerp(_Color.rgb, _Rim.rgb, saturate((1 - soft) * 1.2 + wisp * 0.35) * 0.6);
                half4 o = half4(col * a, a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, o, half4(0, 0, 0, 0));
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
