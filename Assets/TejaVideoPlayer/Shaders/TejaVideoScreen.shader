Shader "TejaVideo/Screen"
{
    // 動画プレイヤーの画面。光の影響を受けない（自分で光る）。
    // 動画の縦横比を保って、余った所は黒い帯（レターボックス）。動画が無い時は黒
    Properties
    {
        _MainTex ("Video (VRCAVProVideoScreen が入れる)", 2D) = "black" {}
        _Brightness ("Brightness", Range(0, 2)) = 1
        _ScreenAspect ("Screen Aspect (幅÷高さ)", Float) = 1.7777778
        [Toggle] _ApplyGamma ("Apply Gamma (AVPro + Linear)", Float) = 1
        [Toggle] _FlipY ("Flip Y on PC (AVPro)", Float) = 1
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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;   // zw = 動画の幅・高さ（ピクセル）
            float _Brightness, _ScreenAspect, _ApplyGamma, _FlipY;

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
                // 縦横比を合わせる（動画が横長なら上下に帯、縦長なら左右に帯）
                float va = _MainTex_TexelSize.z > 1 ? _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1) : _ScreenAspect;
                float2 uv = i.uv - 0.5;
                if (va > _ScreenAspect) uv.y *= va / _ScreenAspect;
                else uv.x *= _ScreenAspect / va;
                uv += 0.5;
                // AVPro の映像は PC（DirectX）だと上下さかさまで来る（Quest/Android はそのまま）（2026-10-15 てじゃ「動画逆さまかも」）
                #if !defined(SHADER_API_MOBILE)
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                #endif
                float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                float3 c = tex2D(_MainTex, uv).rgb * inside;
                // AVPro の映像はガンマ空間のまま来るので、リニアのプロジェクトでは戻す（SDK の Video/RealtimeEmissiveGamma と同じ）
                #if !defined(UNITY_COLORSPACE_GAMMA)
                if (_ApplyGamma > 0.5) c = pow(c, 2.2);
                #endif
                fixed4 col = fixed4(c * _Brightness, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
