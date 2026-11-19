// Vketブース版 WindLeaf（元は FLUFF の FuwaCourse/WindLeaf）。風に舞う綿毛（メッシュパーティクル用）。
// 両面描画、色は頂点カラー、上からのやわらかい陰だけ。霧・インスタンシング・遠くで薄くする処理は外した（キーワードなし）
Shader "Custom/2562/Fuwa/WindLeaf"
{
    Properties
    {
        _Shade ("Shade", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "IgnoreProjector"="True" }
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half _Shade;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.n) * (facing > 0 ? 1 : -1);
                half shade = lerp(1 - _Shade, 1, saturate(n.y * 0.5 + 0.5));
                return fixed4(i.color.rgb * shade, i.color.a);
            }
            ENDCG
        }
    }
}
