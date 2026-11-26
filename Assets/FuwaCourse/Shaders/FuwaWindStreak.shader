Shader "FuwaCourse/WindStreak"
{
    // 風の筋（パーティクルのトレイル用）。帯の幅方向のふちをぼかし、長さ方向の両端も薄くする。色と濃さは頂点カラー
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.8)
        _EdgeSoft ("Edge Softness", Range(0.05, 1)) = 0.75
        _EndFade ("End Fade", Range(0.01, 0.5)) = 0.25
        _FadeStart ("Fade Start (m)", Float) = 15
        _FadeEnd ("Fade End (m)", Float) = 45
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.35
        // 目の前の筋は消す（2026-10-18 てじゃ「クモの巣みたいに目の前は消えるように、クモの巣より至近距離でいい」。クモの巣は 0.45〜1.0m）
        _NearHide ("Camera Fade: invisible within (m)", Float) = 0.2
        _NearShow ("Camera Fade: fully visible beyond (m)", Float) = 0.6
        // 正面（筋の向きに沿って）から見ると、帯が縮んで丸いもやの塊に見える（2026-11-26 てじゃ「1-1最初の追い風を正面から見てる時、真正面にある風エフェクトがちょと変」「真正面はなくてもいい」）
        //  → 画面で見た帯の「長さ÷幅」がこれより小さいと消す（横から見ると細長い＝大きい、正面からだと短く太い＝小さい）
        _HeadOnHide ("Head-on: invisible when screen length/width below", Float) = 1.5
        _HeadOnShow ("Head-on: fully visible when screen length/width above", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _EdgeSoft, _EndFade;
            float _FadeStart, _FadeEnd; half _FarAlpha;
            float _NearHide, _NearShow;
            float _HeadOnHide, _HeadOnShow;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) float3 wp : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                // カメラから遠いほどうっすら
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = distance(o.wp, _WorldSpaceCameraPos);
                o.color.a *= lerp(1, _FarAlpha, saturate((d - _FadeStart) / max(0.01, _FadeEnd - _FadeStart)));
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 幅方向（uv.y 0〜1）：真ん中が濃く、ふちに向かってなめらかに消える
                half across = 1 - abs(i.uv.y * 2 - 1);
                half edge = smoothstep(0, _EdgeSoft, across);
                // 長さ方向（uv.x 0=先頭〜1=しっぽ）：両端をうすく
                half ends = smoothstep(0, _EndFade, i.uv.x) * smoothstep(1, 1 - _EndFade * 2, i.uv.x);
                // 目の前は消す（筋は長いので頂点ではなくピクセルごとの距離で）
                half nearFade = smoothstep(_NearHide, max(_NearShow, _NearHide + 0.01), distance(i.wp, _WorldSpaceCameraPos));
                // 正面から見た筋は消す：uv の画面での変わり方から、帯の見かけの長さ÷幅（= 幅方向の変化 ÷ 長さ方向の変化）
                float elong = fwidth(i.uv.y) / max(fwidth(i.uv.x), 1e-5);
                nearFade *= smoothstep(_HeadOnHide, max(_HeadOnShow, _HeadOnHide + 0.01), elong);
                { fixed4 fogc__ = fixed4(i.color.rgb, i.color.a * edge * ends * nearFade); UNITY_APPLY_FOG(i.fogCoord, fogc__); return fogc__; }
            }
            ENDCG
        }
    }
}
