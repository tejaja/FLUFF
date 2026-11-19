// ブースの「止めた発射エフェクト」用（2026-10-16 てじゃ「銃のゲージと発射エフェクトもほしい」→ 動かさず形で固定する案）。
// FLUFF のエアガンの粒（空気の玉・輪・風の筋・光）と同じ絵を、板や球に貼って空中に置く。
// _Soft > 0 の時は球用：ふちほど透明（ふわっとした空気の玉）。0 の時は絵の透明度そのまま（板用）。
// キーワードなし・霧なし・ライトなし（ブースの規定に合わせる）
Shader "Custom/2562/Fuwa/AirFX"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 0.8)
        _Soft ("球のふちのぼかし（0=板）", Range(0, 4)) = 0
        _Rim ("球のふちの白さ", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent-8" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; float _Soft, _Rim;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = _Color * i.color;
                if (_Soft > 0.001)
                {
                    // 球：正面ほど濃く、ふちで消える。ふちの少し内側を白く光らせる
                    float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                    float f = saturate(abs(dot(normalize(i.wn), v)));
                    c.a *= pow(f, _Soft);
                    c.rgb = lerp(c.rgb, 1, _Rim * (1 - f));
                }
                else
                {
                    c *= tex2D(_MainTex, i.uv);
                }
                return c;
            }
            ENDCG
        }
    }
}
