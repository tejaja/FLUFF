// 2–2 の岩がはねる場所のヒビ（2026-11-02 てじゃ「岩がバウンドする予定地にヒビ模様を入れよう」）。道の上に貼る板で、R が濃さ、G がまん中の熱い所。
// 道と重なってチラつかないよう、少し持ち上げた上で Offset をかけて、深さは書かない
Shader "FuwaCourse/RockCrack"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("ヒビ (R 濃さ、G 熱いまん中)", 2D) = "black" {}
        _Dark ("ヒビの色", Color) = (0.2, 0.07, 0.06, 1)
        [HDR] _Hot ("熱いまん中の色", Color) = (1.2, 0.42, 0.08, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+20" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -2, -2
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Dark;
            half4 _Hot;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed2 t = tex2D(_MainTex, i.uv).rg;
                fixed a = smoothstep(0.35, 0.6, t.r);
                fixed4 c = fixed4(lerp(_Dark.rgb, _Hot.rgb, smoothstep(0.4, 0.8, t.g)), a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
