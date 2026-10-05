// ゴールの輪の内側の床に描く、中心へ向かって寄っていく波（同心円の輪が外から内へ流れる）。平らな Quad（uv 0〜1）に貼る。
// 中心ほどほんのり明るいグラデの上に、波の輪が外のふちで生まれて中心で消える。
Shader "FuwaCourse/GoalWaves"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.6, 0.85, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _Count ("Waves", Float) = 3
        _Speed ("Speed (waves/s)", Float) = 0.45
        _Width ("Wave Width", Range(0.02, 0.5)) = 0.18
        _Fill ("Center Glow", Range(0, 1)) = 0.35
        _InnerR ("Wave Area Radius (0-1 of the board)", Range(0.1, 1)) = 1
        _ShadowR ("Ring Shadow Radius (0-1)", Range(0, 1)) = 0.9
        _ShadowW ("Ring Shadow Width", Range(0.01, 0.5)) = 0.12
        _ShadowA ("Ring Shadow Alpha", Range(0, 1)) = 0.3
        _Fade ("Fade (script)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Alpha, _Count, _Speed, _Width, _Fill, _InnerR, _ShadowR, _ShadowW, _ShadowA, _Fade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float rb = length(i.uv - 0.5) * 2;   // 板の中心0〜ふち1
                float r = rb / _InnerR;              // 波の範囲（輪の内側）で0〜1
                // 外から内へ流れる波：r に時間を足した位相（時間が進むと同じ位相が内側へ移る）
                float ph = frac(r * _Count + _Time.y * _Speed);
                // ph が 0 付近にくっきりした前側、後ろへなだらかに尾を引く波
                half wave = smoothstep(_Width, 0, ph) + smoothstep(1 - _Width * 0.35, 1, ph);
                // ふちで生まれて、中心に近づくほど細く薄くなって消える
                half life = smoothstep(0.02, 0.35, r) * (1 - smoothstep(0.85, 1.0, r));
                half glow = (1 - smoothstep(0, 1, r)) * _Fill;
                half a = saturate(wave * life * 0.8 + glow) * _Alpha * step(r, 1);
                // 輪の影：輪の真下にぼんやり暗い帯（輪は影を落とさないシェーダーなので、地面から浮いて見えないように）
                half sd = saturate(1 - abs(rb - _ShadowR) / _ShadowW);
                half sa = sd * sd * (3 - 2 * sd) * _ShadowA * step(rb, 1);
                // 影（黒）の上に波（色）を重ねた色と不透明度
                half outA = a + sa * (1 - a);
                half3 col = outA > 0.0001 ? (_Color.rgb * a) / outA : _Color.rgb;
                fixed4 c = fixed4(col, outA * _Fade);   // ゴールでリングが消える時に一緒に消す
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
