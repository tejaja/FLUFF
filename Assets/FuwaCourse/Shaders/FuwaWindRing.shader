// 風でできた輪：トーラスに貼る。uv.x = 輪に沿った位置(0〜1)、uv.y = 管のまわりの角度(0〜1)。
// ねじれた風の筋が輪に沿ってぐるぐる流れる。筋以外はうっすら、ふちほど明るい（空気の層っぽく）。
Shader "FuwaCourse/WindRing"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 1, 1, 1)
        _Alpha ("Max Alpha", Range(0, 1)) = 0.85
        _Base ("Base Alpha", Range(0, 1)) = 0.12
        _Streaks ("Streak Count", Float) = 7
        _Twist ("Twist", Float) = 2
        _Speed ("Flow Speed", Float) = 0.6
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

            fixed4 _Color;
            float _Alpha, _Base, _Streaks, _Twist, _Speed;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wv : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wv = WorldSpaceViewDir(v.vertex);
                return o;
            }

            float streak(float x, float w)
            {
                float f = abs(frac(x) - 0.5) * 2;          // 0=筋の真ん中
                return 1 - smoothstep(0, w, f);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float u = i.uv.x, v = i.uv.y;
                // 管のまわりにねじれながら、輪に沿って流れる筋（2本の層：速い細い筋とゆっくり太い筋）
                float a = streak(u * _Streaks + v * _Twist - t, 0.18);
                float b = streak(u * (_Streaks - 3) - v * (_Twist - 1) - t * 0.6 + 0.37, 0.3) * 0.55;
                // 筋の濃さを輪に沿ってゆらす（途切れ途切れの風っぽく）
                float gust = 0.55 + 0.45 * sin(u * 6.2831853 * 3 - t * 2.2);
                float lines = saturate(a * gust + b);
                float ndv = abs(dot(normalize(i.wn), normalize(i.wv)));
                float rim = pow(1 - ndv, 1.5);
                float alpha = (_Base * (0.4 + rim) + lines * (0.5 + 0.5 * rim)) * _Alpha;
                float3 col = lerp(_Color.rgb, 1, lines * 0.6);
                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
}
