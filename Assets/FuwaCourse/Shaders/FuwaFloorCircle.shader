// 床に描く半透明の円（レースの集合場所など）。平らな Quad に貼る。
// 中はうっすら塗り、ふちは濃い輪、その内側に点線の輪がゆっくり回る。全体がゆっくり呼吸するように明滅。
Shader "FuwaCourse/FloorCircle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.62, 0.26, 1)
        _Fill ("Fill Alpha", Range(0, 1)) = 0.18
        _Ring ("Ring Alpha", Range(0, 1)) = 0.8
        _RingWidth ("Ring Width (0-1 of radius)", Range(0.01, 0.3)) = 0.06
        _Dash ("Dash Alpha", Range(0, 1)) = 0.45
        _DashCount ("Dash Count", Float) = 24
        _DashSpeed ("Dash Speed", Float) = 0.15
        _Pulse ("Pulse", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Fill, _Ring, _RingWidth, _Dash, _DashCount, _DashSpeed, _Pulse;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 p : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = v.uv * 2 - 1;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.p);
                float aa = fwidth(r) * 1.5;
                float inside = 1 - smoothstep(1 - aa, 1, r);
                // ふちの濃い輪
                float ring = smoothstep(1 - _RingWidth - aa, 1 - _RingWidth, r) * inside;
                // 内側の点線の輪（ゆっくり回る）
                float dr = 1 - _RingWidth * 2.6;
                float band = 1 - smoothstep(_RingWidth * 0.45, _RingWidth * 0.45 + aa, abs(r - dr));
                float ang = atan2(i.p.y, i.p.x) / 6.2831853 + _Time.y * _DashSpeed;
                float dash = step(0.5, frac(ang * _DashCount)) * band;
                float a = _Fill * inside;
                a = max(a, _Ring * ring);
                a = max(a, _Dash * dash);
                a *= 1 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 2.2));
                return fixed4(_Color.rgb, a * _Color.a);
            }
            ENDCG
        }
    }
}
