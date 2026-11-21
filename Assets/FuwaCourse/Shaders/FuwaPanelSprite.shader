// 看板・ラベルの角丸パネル用。半透明だけど奥行き（Zバッファ）を書くので、
// 手前のパネルの後ろにある別の看板の文字が透けて見えない。
// 文字(TextMeshPro, Queue 3000)より先に描くため Queue は 2990。
Shader "Fuwa/PanelSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff (これ以下は奥行きも書かない)", Range(0,1)) = 0.3
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull（Backで裏から見えない看板に）", Float) = 0
        // 枠（Border）はパネルの6mm後ろ。遠くでパネルとチラつかないよう奥へずらす（+1）。ふだんは0
        _OffsetFactor ("Depth Offset Factor", Float) = 0
        _OffsetUnits ("Depth Offset Units", Float) = 0
        // 2026-11-21 てじゃ「各種ボードもちょっぴりグラデほしい」：上がそのままの色→下に向かって _Grad だけ濃く（ボタンの RoundedPanelGrad と同じくらい）。
        // 9分割の絵なので uv.y はふちの所で速く変わる → ふち（_GradBorder＝絵のふちの割合）の内側だけで 0〜1 になるようにならす
        _Grad ("Gradient (下を何割暗く)", Range(0, 0.3)) = 0
        _GradBorder ("Gradient Border (9分割のふち、uv の割合)", Range(0, 0.49)) = 0.25
        // 下の色：黒に寄せるとくすむので（てじゃ「黒だとくすんでみえる」）、色味のある影にする。
        //  _GradSelf=1 なら自分の色を掛ける（濃く・あざやかに。色の枠向け）、0 なら _GradColor を掛ける（白い板向け、うすい青むらさき）
        _GradColor ("Gradient Shade Color", Color) = (0.8, 0.82, 0.96, 1)
        _GradSelf ("Gradient Self Tint (0=_GradColor, 1=自分の色)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull [_Cull]
        ZWrite On
        Offset [_OffsetFactor], [_OffsetUnits]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed _Cutoff;
            half _Grad, _GradBorder, _GradSelf;
            fixed4 _GradColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                half gt = saturate((i.uv.y - _GradBorder) / max(1e-3, 1 - 2 * _GradBorder));   // 下 0 〜 上 1
                half3 shade = lerp(_GradColor.rgb, c.rgb, _GradSelf);   // 影の色（掛ける）
                c.rgb *= lerp(half3(1, 1, 1), shade, _Grad * (1 - gt));
                // 角丸の外側（ほぼ透明な所）は捨てて、奥行きも書かない
                clip(c.a - _Cutoff);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
