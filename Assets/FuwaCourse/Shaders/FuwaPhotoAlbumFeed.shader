// 名場面アルバムの写真を、VRChatの手持ちカメラ（写真・ストリーム）にだけ映す（FuwaPhotoAlbum）。
// Fuwa/PhotoCamFeed と同じく、レンズの前に置いた板を「画面全体をおおう四角」に描き直す。
// 写真（16:9）とVRChatカメラの縦横比が違う時は、写真の縦横比を崩さずに画面いっぱいまで広げる（はみ出た所は切る）。
Shader "Fuwa/PhotoAlbumFeed"
{
    Properties
    {
        _MainTex ("Photo", 2D) = "black" {}
        _SrcAspect ("Photo Aspect", Float) = 1.7777778
        _MaxCameraDistance ("Max Camera Distance", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Overlay+200" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _SrcAspect;
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                bool photoCam = _VRChatCameraMode > 0.5 && _VRChatMirrorMode < 0.5
                    && distance(_WorldSpaceCameraPos, center) < _MaxCameraDistance;
                float2 xy = v.uv * 2 - 1;
                xy.y *= _ProjectionParams.x;
                o.pos = photoCam ? float4(xy, 0.5, 1) : float4(0, 0, 0, 0);
                // 画面の縦横比に合わせて、写真の使う範囲を真ん中から切り出す
                float screenAspect = _ScreenParams.x / _ScreenParams.y;
                float2 k = screenAspect > _SrcAspect ? float2(1, _SrcAspect / screenAspect) : float2(screenAspect / _SrcAspect, 1);
                o.uv = (v.uv - 0.5) * k + 0.5;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(tex2D(_MainTex, i.uv).rgb, 1);
            }
            ENDCG
        }
    }
}
