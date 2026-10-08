// チュートリアルの「ふわふわだけ止める」壁。人は通り抜けられる（当たり判定は Walkthrough レイヤー）。
// ふだんは見えない。自分のふわふわ（_UdonFuwaBallPos、FuwaBall が毎フレーム渡す。w=見えている時1）が近づくと、
// そのまわりだけ波紋のように丸く浮かび上がる（シャボンの膜っぽく、見る角度で少し虹色）。
// 見ている人がすぐ近くにいる時だけ、ふちがうっすら見える（通り抜ける時に「ここに膜がある」と分かる程度）。
Shader "FuwaCourse/SoftBarrier"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.95, 1, 0.55)
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 0.35)
        _Reveal ("Reveal Radius (m)", Float) = 1.1
        _RingFreq ("Ring Freq", Float) = 22
        _RingSpeed ("Ring Speed", Float) = 5
        _EdgeWidth ("Edge Width (UV)", Range(0.005, 0.2)) = 0.03
        _CamNear ("Edge Visible Within (m)", Float) = 2.5
        _Iri ("Iridescence", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _EdgeColor;
            float _Reveal, _RingFreq, _RingSpeed, _EdgeWidth, _CamNear, _Iri;
            float4 _UdonFuwaBallPos;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 wn : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ふわふわのまわりだけ浮かび上がる
                float d = distance(i.wpos, _UdonFuwaBallPos.xyz);
                float reveal = (1 - smoothstep(0.15, _Reveal, d)) * saturate(_UdonFuwaBallPos.w);
                float ring = 0.5 + 0.5 * sin(d * _RingFreq - _Time.y * _RingSpeed);
                ring = smoothstep(0.6, 1, ring);
                float3 vdir = normalize(_WorldSpaceCameraPos - i.wpos);
                float ndv = abs(dot(normalize(i.wn), vdir));
                float3 iri = 0.5 + 0.5 * cos(6.2831 * (ndv * 0.8 + d * 0.6 + float3(0, 0.33, 0.67)));
                float3 col = lerp(_Color.rgb, _Color.rgb * (0.7 + 0.6 * iri), _Iri);
                float a = _Color.a * reveal * (0.35 + 0.65 * ring);

                // すぐ近くにいる時だけ、ふちがうっすら（上へいくほど消える）
                float2 uv = i.uv;
                float edge = max(1 - smoothstep(0, _EdgeWidth, uv.y), max(1 - smoothstep(0, _EdgeWidth, uv.x), 1 - smoothstep(0, _EdgeWidth, 1 - uv.x)));
                float camD = distance(_WorldSpaceCameraPos, i.wpos);
                float camNear = 1 - smoothstep(_CamNear * 0.5, _CamNear, camD);
                float ea = _EdgeColor.a * edge * camNear * (1 - smoothstep(0.1, 0.6, uv.y));
                col = lerp(col, _EdgeColor.rgb, saturate(ea / max(0.001, a + ea)));
                a = max(a, ea);
                if (a <= 0.002) discard;
                return fixed4(col, saturate(a));
            }
            ENDCG
        }
    }
}
