// モニターの画面（おっかけカメラ・写真館の映像を貼る）。光の影響を受けない Unlit/Texture と同じ見た目。
// Unlit/Texture は深度テクスチャ用のパス（ShadowCaster）が無く、画面が深度に入らないので、
// 深度から地面を探す風の床の模様（Fuwa/WindDecal）が画面の奥の床に描いて、モニター越しに透けて見えた（2026-10-17 てじゃ）。
// 影は落とさない（太陽の影の地図には描かない）
Shader "Fuwa/MonitorScreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        // カメラの深度テクスチャ用。太陽（平行光源）の影の地図は正射影なので、その時は三角形を画面の外へ飛ばす
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { V2F_SHADOW_CASTER; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                if (UNITY_MATRIX_P[3][3] > 0.5) o.pos = float4(2, 2, 2, 1);
                return o;
            }

            float4 frag (v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
