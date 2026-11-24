// チュートリアルの課題の輪（2026-11-24 てじゃ「クリア前リングに回転する光、クリア後は全部光る」）
//  輪のまわり（uv.x）を光の帯が回る。_Lit=1 で全体が光る（クリア後の材質）。ふつうの半ランバート＋霧。影は FallBack の ShadowCaster
Shader "FuwaCourse/RingChase"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.45, 1)
        _Glow ("Glow", Color) = (1, 0.97, 0.8, 1)
        _Speed ("Speed (turns/sec)", Float) = 0.45
        _Count ("Bands", Float) = 2
        _Width ("Band width", Range(0.02, 0.5)) = 0.2
        _Lit ("All lit", Range(0, 1)) = 0
        _Pulse ("Pulse when lit", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _Glow;
            float _Speed, _Count, _Width, _Lit, _Pulse;
            float4 _LightColor0;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.n = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float nl = dot(n, normalize(_WorldSpaceLightPos0.xyz)) * 0.5 + 0.5;
                float3 base = _Color.rgb * lerp(0.72, 1.06, nl);
                float p = frac(i.uv.x * _Count - _Time.y * _Speed * _Count);
                float d = min(p, 1.0 - p);
                float band = smoothstep(_Width, 0.0, d);
                float pulse = 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 4.0));
                float g = max(band * (1.0 - _Lit), _Lit * pulse);
                float3 col = lerp(base, _Glow.rgb, g);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "VertexLit"
}
