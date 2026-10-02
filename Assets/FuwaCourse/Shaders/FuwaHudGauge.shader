// 空気ゲージ（カプセル型）。枠・中の暗い所・ゲージを1枚の板で計算して描くので、
// 小さくしても縁がきれいで、枠の太さが上下左右どこでも同じになる。
// ゲージの量は _Fill（0〜1）、板の横÷縦の比率は _Aspect（スクリプトが入れる）。
Shader "Fuwa/HudGauge"
{
    Properties
    {
        _Fill ("Fill", Range(0, 1)) = 1
        _Aspect ("Aspect (width / height)", Float) = 6
        _Border ("Border (height=1)", Range(0, 0.45)) = 0.2
        _FillColor ("Fill Color", Color) = (0.45, 0.82, 1, 0.95)
        _InnerColor ("Inner Color", Color) = (0.1, 0.07, 0.15, 0.55)
        _BorderColor ("Border Color", Color) = (0.12, 0.08, 0.18, 0.8)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (Always=HUD / LessEqual=銃に貼る)", Float) = 8
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off=HUD / Back=銃に貼る：裏側の面が透けて見えないように)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull [_Cull]
        ZWrite Off
        ZTest [_ZTest]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float _Fill, _Aspect, _Border;
            fixed4 _FillColor, _InnerColor, _BorderColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 横の半分の長さ hx、縦の半分 hy、角の半径 r の角丸四角（カプセル）までの距離
            float sdRound(float2 p, float2 h, float r)
            {
                float2 q = abs(p) - (h - r);
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 高さ=1 の座標系（横は _Aspect）
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1);
                float hx = _Aspect * 0.5;
                float dOuter = sdRound(p, float2(hx, 0.5), 0.5);
                float ri = 0.5 - _Border;
                float dInner = sdRound(p, float2(hx - _Border, ri), ri);
                // ゲージ：内側の形（枠の内側のカプセル）を、左端から量の所で縦にまっすぐ切ったもの。
                // 左端と上下は枠に沿った丸み、減っていく側の端はまっすぐ。満タンの時は枠の中にぴったり収まる
                float innerW = (hx - _Border) * 2;
                float xEdge = -(hx - _Border) + innerW * saturate(_Fill);

                float aaO = max(fwidth(dOuter), 1e-4);
                float aaI = max(fwidth(dInner), 1e-4);
                float aaX = max(fwidth(p.x), 1e-4);
                float inOuter = 1 - smoothstep(-aaO, aaO, dOuter);
                float inInner = 1 - smoothstep(-aaI, aaI, dInner);
                float inFill = (1 - smoothstep(-aaX, aaX, p.x - xEdge)) * (_Fill > 0.0001 ? 1 : 0);

                fixed4 c = lerp(_BorderColor, _InnerColor, inInner);
                c = lerp(c, _FillColor, inFill * inInner);
                c.a *= inOuter;
                return c;
            }
            ENDCG
        }
    }
}
