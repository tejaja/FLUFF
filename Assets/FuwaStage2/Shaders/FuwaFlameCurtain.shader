// 2–2 ジグザグの炎の壁（2026-10-31）。ぼやけた粒のかたまりの代わりに、くっきりしたトゥーンの炎の板。uv.x は長さ（m）、uv.y は下 0〜上 1。
// 模様は上へ流れて、上にいくほど切れて細くなる。光を受けず自分で光る。深度に書かない（うそ影や風の模様が炎に乗らないように、影のパスもなし）
Shader "FuwaCourse/FlameCurtain"
{
    Properties
    {
        [HDR] _Hot ("内側の色", Color) = (1.2, 0.86, 0.3, 1)
        [HDR] _Mid ("まん中の色", Color) = (1.2, 0.46, 0.07, 1)
        [HDR] _Tip ("先の色", Color) = (1.0, 0.15, 0.04, 1)
        _Speed ("動きの速さ", Float) = 1
        _Height ("炎の高さの割合", Range(0.3,1)) = 1
        _Sway ("ゆらぎ", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            half4 _Hot, _Mid, _Tip;
            float _Speed, _Height, _Sway;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float h : TEXCOORD2; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.h = v.color.r;   // その場所の炎の高さの割合（帯のはしは低い）
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed; float y = i.uv.y;
                // 上へ流れるうねり（上ほど大きく）
                float x = i.uv.x + (sin(y * 8.0 - t * 5.0 + i.uv.x * 1.1) * 0.55 + sin(y * 15.0 - t * 8.0 + i.uv.x * 2.3) * 0.22) * _Sway * (0.2 + y * 2.0);
                // 炎の舌：|sin| の山をかさねた高さ（テクスチャを使わないのでふちがなめらか）
                float a = 1.0 - abs(sin(x * 1.25 + t * 0.9)), b = 1.0 - abs(sin(x * 2.9 - t * 1.4 + 1.3)), c3 = 1.0 - abs(sin(x * 6.1 + t * 2.1 + 2.7));
                float top = (0.64 + 0.22 * a * a + 0.1 * b + 0.04 * c3) * _Height * i.h;   // 低い所でも当たりの高さの 2/3 近くまで（低くすると、炎の上の何もない所でアウトになって見える）
                clip(top - y);
                // 下のはしも舌の形で切る（まっすぐ切れて板に見えないように）
                float low = 0.09 * (1.0 - abs(sin(x * 1.9 - t * 0.7))) + 0.05 * (1.0 - abs(sin(x * 4.7 + t))) + 0.005;
                clip(y - low);
                // 舌の形に合わせた帯：内側が熱い。下のはしは赤
                float u = (y - low) / max(top - low, 0.01);
                half3 c = u < 0.1 ? _Tip.rgb : (u < 0.42 ? _Hot.rgb : (u < 0.74 ? _Mid.rgb : _Tip.rgb));
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
