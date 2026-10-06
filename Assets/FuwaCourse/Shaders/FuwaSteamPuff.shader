// 蒸気のもくもく（パーティクル用）。テクスチャ（もくもくの形・上が明るく下が影色）× 頂点カラー。
// ソフトパーティクルは使わない（_InvFade の罠：物の手前で消える）。霧には合わせる
Shader "FuwaCourse/SteamPuff"
{
    Properties
    {
        _MainTex ("Puff Sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color (下側の影色)", Color) = (0.92, 0.92, 0.96, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _ShadeColor;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                // テクスチャの赤＝明るさ（上が明るい）。影色から白へ
                fixed3 c = lerp(_ShadeColor.rgb, fixed3(1, 1, 1), t.r) * i.color.rgb;
                fixed4 col = fixed4(c, t.a * i.color.a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
