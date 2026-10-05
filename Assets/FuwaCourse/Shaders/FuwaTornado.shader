// つむじかぜ（竜巻）の本体：じょうご形のメッシュ（FuwaTornadoFunnel、uv.x=まわりの角度0〜1、uv.y=高さ0〜1）に貼る。
// ねじれた白い筋が回りながら上へ流れる。根元と上のふちはぼかし、ふちほど（視線とすれる所ほど）濃く見せる。
// 全体は根元を固定して、上ほどゆっくりくねくね揺れる（動きは全部シェーダーの中）。
Shader "FuwaCourse/Tornado"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.55
        _Bands ("Bands (around)", Float) = 5
        _Twist ("Twist (turns from bottom to top)", Float) = 1.2
        _Spin ("Spin Speed", Float) = 1.1
        _Rise ("Rise Speed", Float) = 0.6
        _Sharp ("Band Sharpness", Range(0.5, 8)) = 2.5
        _Rim ("Rim Strength", Range(0, 2)) = 1.0
        _Base ("Base Fill", Range(0, 1)) = 0.18
        _SwayAmp ("Sway Amp (m at top)", Float) = 0.25
        _SwaySpeed ("Sway Speed", Float) = 0.9
        _BottomFade ("Bottom Fade", Range(0.01, 0.5)) = 0.12
        _TopFade ("Top Fade", Range(0.01, 0.5)) = 0.25
        _NearDist ("Fade Start Distance", Float) = 5
        _FarDist ("Fade End Distance", Float) = 13
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Alpha, _Bands, _Twist, _Spin, _Rise, _Sharp, _Rim, _Base, _BottomFade, _TopFade;
            float _SwayAmp, _SwaySpeed, _NearDist, _FarDist;
            half _FarAlpha;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_FOG_COORDS(3) };

            v2f vert (appdata v)
            {
                v2f o;
                float h = v.uv.y;
                float t = _Time.y * _SwaySpeed;
                // 上ほど大きく、2つの周期を重ねたくねくね（根元はほぼ動かない）
                float k = h * h;
                float3 p = v.vertex.xyz;
                float3 sway = float3(sin(t + h * 2.2) + 0.5 * sin(t * 1.7 + 1.3), 0, cos(t * 0.8 + h * 1.7) + 0.5 * sin(t * 1.3 + 0.4)) * k;
                // オブジェクトのスケールの影響を受けないよう、メートル単位で足す
                float3 sc = float3(length(unity_ObjectToWorld._m00_m10_m20), 1, length(unity_ObjectToWorld._m02_m12_m22));
                p += sway * _SwayAmp / max(sc, 0.001);
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.uv = v.uv;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float h = i.uv.y;
                // ねじれた筋：まわりの角度に、高さに比例したねじれと時間の回転・上昇を足す
                float a = i.uv.x * _Bands + h * _Twist * _Bands - _Time.y * _Spin * _Bands * 0.25 - _Time.y * _Rise;
                float band = 0.5 + 0.5 * sin(a * 6.2832);
                band = pow(band, _Sharp);
                // 細かい筋を少し重ねて、風の流れっぽく
                float a2 = i.uv.x * _Bands * 3 + h * _Twist * _Bands * 2.2 - _Time.y * _Spin * _Bands * 0.4;
                band = saturate(band + pow(0.5 + 0.5 * sin(a2 * 6.2832), 6) * 0.35);
                // ふち（視線と面がすれる所）ほど濃く
                float3 vd = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = abs(dot(normalize(i.wn), vd));
                float rim = pow(1 - ndv, 2) * _Rim;
                float fade = smoothstep(0, _BottomFade, h) * smoothstep(1, 1 - _TopFade, h);
                float alpha = saturate(_Base + band * 0.8 + rim) * fade * _Alpha;
                // 裏側（奥の面）は少し薄く
                alpha *= facing > 0 ? 1 : 0.6;
                // 遠くのつむじかぜは薄く（手前のと重なっても奥が見えるように）。距離はつむじかぜの根元で測る
                float3 org = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float dist = distance(_WorldSpaceCameraPos, org);
                alpha *= lerp(1, _FarAlpha, smoothstep(_NearDist, _FarDist, dist));
                fixed4 c = fixed4(_Color.rgb * (0.9 + 0.1 * band), alpha);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
