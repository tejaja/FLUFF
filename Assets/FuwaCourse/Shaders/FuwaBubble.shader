// しゃぼん玉（パーティクルのビルボード用）。円の中は透明で、ふちだけ虹色にゆらめく＋白いハイライト。
// 頂点カラーのアルファで全体の濃さ（出てくる・消える）を決める
Shader "FuwaCourse/Bubble"
{
    Properties
    {
        _RimWidth ("Rim Width", Range(0.02, 0.6)) = 0.28
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.75
        _FillAlpha ("Fill Alpha", Range(0, 0.3)) = 0.05
        _Iris ("Iridescence", Range(0, 1)) = 0.8
        _Highlight ("Highlight", Range(0, 2)) = 1.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            half _RimWidth, _RimAlpha, _FillAlpha, _Iris, _Highlight;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 rnd : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float seed : TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color;
                o.seed = v.rnd.x;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            half3 Hue(half h) { h = frac(h); return saturate(abs(frac(h + half3(0, 0.6667, 0.3333)) * 6 - 3) - 1); }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                if (r > 1) discard;
                // 球っぽさ：ふちほど厚い（フレネル風）
                float z = sqrt(saturate(1 - r * r));
                float rim = pow(1 - z, 1.0 / max(_RimWidth, 0.01) * 0.35);
                // 虹色：ふちからの距離＋角度＋時間でゆらめく
                float ang = atan2(p.y, p.x);
                half3 iris = Hue(rim * 1.3 + ang * 0.12 + _Time.y * 0.15 + i.seed * 3.1 + sin(ang * 3 + _Time.y * 0.8) * 0.08);
                half3 col = lerp(half3(1, 1, 1), iris * 1.15 + 0.1, _Iris) * (0.8 + 0.35 * rim);
                float a = _FillAlpha + rim * _RimAlpha;
                // 白いハイライト（左上に小さい窓のような光）
                float2 hp = p - float2(-0.38, 0.42);
                float hl = smoothstep(0.24, 0.12, length(hp * float2(1.0, 1.4)));
                float hl2 = smoothstep(0.1, 0.04, length(p - float2(0.42, -0.36)));
                col += (hl + hl2 * 0.6) * _Highlight;
                a = saturate(a + (hl + hl2 * 0.5) * 0.8);
                // ふちの外側はやわらかく
                a *= smoothstep(1.0, 0.94, r);
                fixed4 o = fixed4(col, a * i.color.a);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
}
