// ボタン・UIの板用：UI/Default ＋ 下に向かって色味のある影のグラデーション（2026-11-21 てじゃ「ボタンもやってみよ」）。
// 看板（Fuwa/PanelSprite の _Grad）と同じ考え方：黒に寄せるとくすむので、
//  色つきの板は自分の色を掛けて濃く・あざやかに、白っぽい板は _GradColor（うすい青むらさき）を掛ける。
//  どっちにするかは色のあざやかさ（max-min）で自動で混ぜる → ボタンの色が実行中に変わっても（▶■の切りかえなど）そのまま合う。
// 9分割の絵（RoundedPanel）なので、上下のふちの中で影が変わる（まん中は一定）
// 2026-11-22 Fuwa/UIDepth と同じく板の奥行きを書く（てじゃ「またシャボン玉がボタンを貫通してみえてる」：グラデ化で UIDepth の奥行きが抜けて、
//  奥の泡・風・綿毛（キュー3008）がボタンの上に出ていた）。ほぼ透明な所（_Cutoff 以下）は捨てる、板だけ 5mm 奥へずらして文字は欠けない、キューは文字より先の 2990、裏は描かない
Shader "Fuwa/UIGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Grad ("Gradient (下の影の強さ)", Range(0, 1)) = 0.6
        _GradBorder ("Gradient Border (9分割のふち、uv の割合)", Range(0, 0.49)) = 0.25
        _GradColor ("Shade Color (白っぽい板)", Color) = (0.8, 0.82, 0.96, 1)
        _SatScale ("あざやかさ→自分の色の影 の効き", Float) = 4

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _Cutoff ("Depth Cutoff", Range(0,1)) = 0.05
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("ZWrite", Float) = 1
        _Push ("Depth Push (m)", Float) = 0.005
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull [_Cull]
        Lighting Off
        ZWrite [_ZWrite]
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            half _Grad, _GradBorder, _SatScale;
            fixed _Cutoff;
            float _Push;
            fixed4 _GradColor;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                // 同じ面の文字が奥行きで欠けないように、板の奥行きだけ奥へずらす（UIDepth と同じ）
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                viewPos += normalize(viewPos) * _Push;
                OUT.vertex = mul(UNITY_MATRIX_P, float4(viewPos, 1));
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                // 下 0 〜 上 1。ボタンは端が半円（9分割の上下のふちだけで高さ全部）なので、下のふち 0〜0.5・まん中 0.5・上のふち 0.5〜1 にする
                // （まん中で 0〜1 にすると、まん中の高さが0のボタンで真ん中にくっきり段ができた）
                half u = IN.texcoord.y, b = max(1e-3, _GradBorder);
                half gt = u < b ? 0.5 * u / b : (u > 1 - b ? 0.5 + 0.5 * (u - (1 - b)) / b : 0.5);
                half sat = max(color.r, max(color.g, color.b)) - min(color.r, min(color.g, color.b));
                half3 shade = lerp(_GradColor.rgb, color.rgb, saturate(sat * _SatScale));
                color.rgb *= lerp(half3(1, 1, 1), shade, _Grad * (1 - gt));
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif
                clip(color.a - _Cutoff);
                return color;
            }
            ENDCG
        }
    }
}
