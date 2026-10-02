// 床に置く「たまっていく輪っか」。_Fill（0〜1）の分だけ、上から見て反時計回りに明るい輪が伸びる。
// 残りの部分は _BgColor で表示（ゴールのポータルはてじゃ希望で a=0＝完全に見えない）。平らな Quad を床に寝かせて使う。
Shader "FuwaCourse/ProgressRing"
{
    Properties
    {
        _Color ("Fill Color", Color) = (1, 1, 1, 0.95)
        _BgColor ("Background Color", Color) = (1, 1, 1, 0.22)
        _Fill ("Fill (0-1)", Range(0, 1)) = 0
        _Inner ("Inner Radius (0-0.5)", Range(0, 0.5)) = 0.4
        _Outer ("Outer Radius (0-0.5)", Range(0, 0.5)) = 0.47
        _Start ("Start Angle (0-1)", Range(0, 1)) = 0
        _Soft ("Soft Glow (0=sharp band)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _BgColor;
            float _Fill, _Inner, _Outer, _Start, _Soft;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv - 0.5;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float aa = fwidth(r) * 1.2;
                float band = smoothstep(_Inner - aa, _Inner + aa, r) * (1 - smoothstep(_Outer - aa, _Outer + aa, r));
                // ふちのぼやけた光の輪（_Soft=1）：帯の真ん中が一番明るく、内外へなだらかに消える
                float mid = (_Inner + _Outer) * 0.5, hw = (_Outer - _Inner) * 0.5;
                float glow = exp(-pow((r - mid) / (hw * 1.1), 2) * 2.2) * (1 - smoothstep(0.47, 0.5, r));
                band = lerp(band, glow, _Soft);
                // 真上(0)から 0→1。床に寝かせて上から見た時に反時計回り（ポータルの渦と同じ向き）
                float ang = atan2(-i.uv.x, i.uv.y) / 6.2831853;
                if (ang < 0) ang += 1;
                ang = frac(ang - _Start + 1);   // 乗った時に向いていた方向から始める
                float fa = min(fwidth(ang), 0.01) * 1.5;   // 始まりの所（角度の切れ目）で線が出ないように
                float filled = _Fill >= 0.999 ? 1 : 1 - smoothstep(_Fill - fa, _Fill + fa, ang);
                filled *= _Fill > 0.001 ? 1 : 0;   // 0の時に、始まりの所へ細い線が残らないように
                // 光の輪(_Soft)の時は、始まりと先頭の端もぼかす
                float edge = smoothstep(0.0, 0.08, ang) * (1 - smoothstep(_Fill - 0.06, _Fill, ang));
                edge = lerp(edge, 1, smoothstep(0.88, 1.0, _Fill));   // 満タンに近づいたら一周つながる
                filled = lerp(filled, filled * edge, _Soft);
                fixed4 c = lerp(_BgColor, _Color, filled);
                c.a *= band;
                return c;
            }
            ENDCG
        }
    }
}
