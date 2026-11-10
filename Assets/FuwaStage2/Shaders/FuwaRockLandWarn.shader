// 落ちる岩が次に着く所の予告の丸（2026-11-10 てじゃ「落ちる岩の予告をもっとわかりやすく」→ 着地点に丸）。
// uv は丸のまん中からの位置（-1〜1、1 が丸のふち）。火球・火炎放射の予告（FuwaCourse/FireWarn の _Mode 1）と同じく、下の道の色に掛ける（影の下に見える）。
// ふちの輪はずっと出ていて、中が _Fill でまん中から広がる。ふちまで埋まった時に岩が着く。出し入れは _Alpha（スクリプトが動かす）
Shader "FuwaCourse/RockLandWarn"
{
    Properties
    {
        _Tint ("掛ける色(0-2、1 で変わらない)", Color) = (1.15, 1.7, 0.4, 1)
        _EdgeTint ("ふちの掛ける色", Color) = (1.2, 1.05, 0.15, 1)
        _EdgeW ("ふちの幅(半径の0-1)", Range(0,1)) = 0.16
        _Inner ("まだ埋まっていない所の濃さ", Range(0,1)) = 0.2
        _Soft ("外のふちのぼかし(半径の0-1)", Range(0,0.3)) = 0.06
        _Fill ("埋まった半径（スクリプトが動かす）", Range(0,1)) = 0
        _Alpha ("出ている量（スクリプトが動かす）", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+20" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend DstColor SrcColor
            ZWrite Off
            Offset -2, -2
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            half4 _Tint, _EdgeTint; half _EdgeW, _Inner, _Fill, _Alpha, _Soft;

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
                float r = length(i.uv);
                float aa = max(fwidth(r), 0.002);
                half inside = 1 - smoothstep(1 - max(aa, _Soft), 1, r);          // 丸の外は何もしない。外のふちだけほんの少しぼかす（2026-11-10 てじゃ「ぱきっとしすぎ、極わずかだけフチをボカシて」）
                half rim = smoothstep(1 - _EdgeW - aa, 1 - _EdgeW, r);           // ふちの輪
                half filled = 1 - smoothstep(_Fill - aa * 2, _Fill, r);          // まん中から広がる所
                half am = lerp(lerp(_Inner, 1, filled), 1, rim) * inside * _Alpha;
                half3 tint = lerp(_Tint.rgb, _EdgeTint.rgb, rim);
                // 出力 s で 2*dst*s。0.5 で変わらず、_Tint*0.5 で _Tint 倍
                half4 m = half4(lerp(half3(1, 1, 1), tint, am) * 0.5, 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, m, half4(0.5, 0.5, 0.5, 1));
                return m;
            }
            ENDCG
        }
    }
    FallBack Off
}
