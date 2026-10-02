// 島のずっと下に置く「雲の海」。大きな板に貼ると、ワールド座標で雲の模様がゆっくり流れる。
// 見る人から遠いほど空の色に溶ける（板のはしが見えないように）。
Shader "FuwaCourse/CloudSea"
{
    Properties
    {
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade Color", Color) = (0.74, 0.84, 0.97, 1)
        _Scale ("Scale (1/m)", Float) = 0.025
        _Cover ("Cover", Range(0, 1)) = 0.48
        _Soft ("Softness", Range(0.01, 0.5)) = 0.16
        _Speed ("Speed (m/s, xz)", Vector) = (0.6, 0, 0.3, 0)
        _Alpha ("Alpha", Range(0, 1)) = 0.95
        _FadeStart ("Fade Start (m)", Float) = 120
        _FadeEnd ("Fade End (m)", Float) = 280
    }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CloudColor, _CloudShade;
            float _Scale, _Cover, _Soft, _Alpha, _FadeStart, _FadeEnd;
            float4 _Speed;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                float a = hash(i), b = hash(i + float2(1, 0)), c = hash(i + float2(0, 1)), d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += vnoise(p) * a; p = p * 2.03 + float2(17.1, 9.2); a *= 0.5; }
                return s;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.wp.xz + _Time.y * _Speed.xz) * _Scale;
                float n = fbm(p);
                float dens = smoothstep(_Cover, _Cover + _Soft, n);
                if (dens <= 0.001) discard;
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float n2 = fbm(p + sunDir.xz * 0.12);
                float lit = saturate(0.65 + (n - n2) * 5);
                float3 col = lerp(_CloudShade.rgb, _CloudColor.rgb, lit);
                float dist = distance(i.wp.xz, _WorldSpaceCameraPos.xz);
                float fade = 1 - smoothstep(_FadeStart, _FadeEnd, dist);
                return fixed4(col, dens * _Alpha * fade);
            }
            ENDCG
        }
    }
}
