// 足元からゆっくり広がって消える輪（銃の台の目印など）。平らな Quad（uv 0〜1）に貼る。
// _Period 秒ごとに、中心から外へ広がりながら薄くなる輪を描く。2本ずらして重ねる。
Shader "FuwaCourse/PulseRing"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.94, 1, 1)
        _Period ("Period", Float) = 1.8
        _Width ("Ring Width (uv)", Range(0.005, 0.2)) = 0.05
        _Inner ("Start Radius (uv)", Range(0, 0.5)) = 0.12
        _Alpha ("Alpha", Range(0, 1)) = 0.75
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
            float _Period, _Width, _Inner;
            half _Alpha;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half Ring(float d, float t)
            {
                float r = lerp(_Inner, 0.5 - _Width, t);
                half band = saturate(1 - abs(d - r) / _Width);
                band = band * band * (3 - 2 * band);
                half fade = (1 - t) * saturate(t * 6);   // 出る時はふわっと、広がるほど薄く
                return band * fade;
            }

            fixed4 frag (v2f i) : SV_Target
            { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float d = length(i.uv - 0.5);
                float t = frac(_Time.y / _Period);
                half a = max(Ring(d, t), Ring(d, frac(t + 0.5)));
                fixed4 c = fixed4(_Color.rgb, a * _Alpha);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
