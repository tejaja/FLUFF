// 風の筋（パーティクルのトレイル用）。帯の幅方向のふちをぼかし、長さ方向の両端も薄くする。色と濃さは頂点カラー。
// Vketブース版（2026-10-17、元は FLUFF の FuwaCourse/WindStreak）：キーワードなし（霧・インスタンシングなし）、遠くでうすくする処理なし
Shader "Custom/2562/Fuwa/WindStreak"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.8)
        _EdgeSoft ("Edge Softness", Range(0.05, 1)) = 0.75
        _EndFade ("End Fade", Range(0.01, 0.5)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-9" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _EdgeSoft, _EndFade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 幅方向（uv.y 0〜1）：真ん中が濃く、ふちに向かってなめらかに消える
                half across = 1 - abs(i.uv.y * 2 - 1);
                half edge = smoothstep(0, _EdgeSoft, across);
                // 長さ方向（uv.x 0=先頭〜1=しっぽ）：両端をうすく
                half ends = smoothstep(0, _EndFade, i.uv.x) * smoothstep(1, 1 - _EndFade * 2, i.uv.x);
                return fixed4(i.color.rgb, i.color.a * edge * ends);
            }
            ENDCG
        }
    }
}
