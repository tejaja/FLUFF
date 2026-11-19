// AirFX をまとめて1材質で描く版（2026-10-17 SetPass 対策）。発射エフェクトの4材質と銃のゲージを1つのメッシュ・1材質に。
// ・絵は1枚のアトラス（FuwaBoothCombine が作る）、uv0 はアトラスの中の位置
// ・uv1.x＝球のふちのぼかし（0=板）、uv1.y＝球のふちの白さ。材質ごとの色は頂点カラーに焼いてある
// キーワードなし・霧なし・ライトなし
Shader "Custom/2562/Fuwa/AirFXAtlas"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
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

            sampler2D _MainTex;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float2 fx : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float2 fx : TEXCOORD3; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.fx = v.fx;
                o.color = v.color;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color;
                if (i.fx.x > 0.001)
                {
                    // 球：正面ほど濃く、ふちで消える。ふちの少し内側を白く光らせる
                    float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                    float f = saturate(abs(dot(normalize(i.wn), v)));
                    c.a *= pow(f, i.fx.x);
                    c.rgb = lerp(c.rgb, 1, i.fx.y * (1 - f));
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
