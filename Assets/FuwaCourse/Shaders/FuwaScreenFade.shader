// 暗転用。頭の位置に置いた板を、シェーダーの中で「画面全体をおおう四角」に描き直す。
// 何にも隠されないよう一番最後に描き、奥行きを無視する。
// 本人の目で見ている時だけ描く：VRChatの手持ちカメラ・写真・ミラーには映らない。
// 板を置いた頭の位置から離れたカメラ（第三者視点のカメラなど）にも映らない。
Shader "Fuwa/ScreenFade"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0)
        _MaxCameraDistance ("Max Camera Distance From Head", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Overlay+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _MaxCameraDistance;
            // VRChatが設定する値。0=本人の目 / 1,2=手持ちカメラ / 3=写真
            float _VRChatCameraMode;
            // 0=普通 / 1,2=ミラーの中
            float _VRChatMirrorMode;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                bool ownEyes = _VRChatCameraMode == 0 && _VRChatMirrorMode == 0
                    && distance(_WorldSpaceCameraPos, center) < _MaxCameraDistance;
                // UV(0〜1)をそのまま画面の四隅(-1〜1)へ。本人の目以外は面積0にして何も描かない
                float2 xy = v.uv * 2 - 1;
                o.pos = ownEyes ? float4(xy, 0.5, 1) : float4(0, 0, 0, 0);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
