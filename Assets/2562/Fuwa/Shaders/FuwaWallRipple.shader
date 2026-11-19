// ブースの見えない壁にふわふわが当たった時の波紋（2026-10-19 てじゃ「前つくった壁の光の波紋みたいなの一瞬だせないかな」）。
// FLUFF のチュートリアルの壁（FuwaCourse/SoftBarrier）の、ふわふわのまわりに出るシャボンっぽい虹色の波紋と同じ見た目。
// FLUFF はふわふわの位置をシェーダー全体に配っていたが、Vket は SetGlobal 系が禁止なので、当たった所に板を出して板の真ん中から描く。
// _Age（0→1、FuwaBoothBall がプロパティブロックで入れる）で広がって消える。_Size は板の大きさ(m)。キーワードなし・霧なし
Shader "Custom/2562/Fuwa/WallRipple"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.95, 1, 0.55)
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 0.35)
        _RingFreq ("Ring Freq", Float) = 22
        _RingSpeed ("Ring Speed", Float) = 5
        _Iri ("Iridescence", Range(0, 1)) = 0.45
        _Age ("Age (set by script, 0-1)", Range(0, 1)) = 0
        _Size ("板の大きさ (m、スクリプトが入れる)", Float) = 1.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent-8" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _EdgeColor;
            float _RingFreq, _RingSpeed, _Iri, _Age, _Size;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 wn : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * _Size;             // 当たった所からの距離(m)
                float age = saturate(_Age);
                // 当たった所から丸く浮かび上がって外へ広がる（SoftBarrier の reveal＝ふわふわのまわり 0.15〜1.1m）
                float front = lerp(0.35, 0.5 * _Size, 1 - (1 - age) * (1 - age));
                float reveal = 1 - smoothstep(front * 0.45, front, d);
                float ring = 0.5 + 0.5 * sin(d * _RingFreq - (_Time.y + age * 2.0) * _RingSpeed);
                ring = smoothstep(0.6, 1, ring);
                float3 vdir = normalize(_WorldSpaceCameraPos - i.wpos);
                float ndv = abs(dot(normalize(i.wn), vdir));
                float3 iri = 0.5 + 0.5 * cos(6.2831 * (ndv * 0.8 + d * 0.6 + float3(0, 0.33, 0.67)));
                float3 col = lerp(_Color.rgb, _Color.rgb * (0.7 + 0.6 * iri), _Iri);
                float fade = (1 - age) * (1 - age) * smoothstep(0, 0.08, age);   // すっと出て、だんだん消える
                float a = _Color.a * reveal * (0.35 + 0.65 * ring) * fade;
                // 広がっていく波の先だけ、ふちを白く
                float rim = (1 - smoothstep(0, 0.06, abs(d - front * 0.8))) * _EdgeColor.a * fade;
                col = lerp(col, _EdgeColor.rgb, saturate(rim / max(0.001, a + rim)));
                a = max(a, rim * reveal);
                a *= 1 - smoothstep(0.42, 0.5, max(abs(i.uv.x - 0.5), abs(i.uv.y - 0.5)));   // 板のふちで切れて見えないように
                if (a <= 0.002) discard;
                return fixed4(col, saturate(a));
            }
            ENDCG
        }
    }
}
