// 滝のカーテン（ステージ2、2026-10-17）。縦に落ちる白い筋と、上下のふちの泡。少しだけ透ける
// 板（Quad）の uv：x＝横、y＝下0〜上1。落ちる速さは _FallSpeed（m/s）、筋の間隔は板の大きさ（行列）から
Shader "FuwaStage2/Waterfall"
{
    Properties
    {
        _Top ("Top Color", Color) = (0.50, 0.84, 0.96, 1)
        _Bottom ("Bottom Color", Color) = (0.30, 0.66, 0.90, 1)
        _Foam ("Foam Color", Color) = (0.97, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.92
        _FallSpeed ("Fall Speed (m/s)", Float) = 3.0
        _StreakScale ("Streak Size", Float) = 1.0
        _EdgeFoam ("Top/Bottom Foam (m)", Float) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Top, _Bottom, _Foam;
            float _Alpha, _FallSpeed, _StreakScale, _EdgeFoam;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 size : TEXCOORD1; UNITY_FOG_COORDS(2) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.size = float2(length(mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0))), length(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0))));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 m = i.uv * i.size;   // メートル
                float s = _StreakScale;
                float3 col = lerp(_Bottom.rgb, _Top.rgb, i.uv.y);
                // 縦の白い筋：横に細かく、縦に長いノイズを下へ流す（2つ重ねて速さをずらす）
                float n = Noise(float2(m.x * 7.0 / s, (m.y + _Time.y * _FallSpeed) * 0.8 / s)) * 0.6
                        + Noise(float2(m.x * 11.0 / s + 3.1, (m.y + _Time.y * _FallSpeed * 1.3) * 1.1 / s)) * 0.4;
                col = lerp(col, _Foam.rgb, smoothstep(0.48, 0.62, n) * 0.85);
                // 上のふち（落ち口）と下（滝つぼに当たる所）の泡
                float top = 1 - smoothstep(0, _EdgeFoam, (1 - i.uv.y) * i.size.y);
                float bot = 1 - smoothstep(0, _EdgeFoam * 1.5, i.uv.y * i.size.y);
                float wob = 0.75 + 0.25 * Noise(float2(m.x * 2.0, _Time.y * 2.0));
                col = lerp(col, _Foam.rgb, saturate((top + bot) * wob));
                // 左右のふちは少し薄く
                float side = smoothstep(0, 0.12, min(i.uv.x, 1 - i.uv.x) * i.size.x);
                fixed4 c = fixed4(col, _Alpha * side);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
