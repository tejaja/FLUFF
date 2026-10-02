// ワープのポータルに乗っている間の「光の幕」。床の輪っか(ProgressRing)の位置から上へ伸びる筒に貼る。
// uv.x = 輪っかと同じ角度(0〜1、たまっていく向き)、uv.y = 高さ(0=足元〜1=てっぺん)。
// _Fill の分だけ足元からぐるっと回り込みながら幕が立ち上がり、満タンで体を一周包む。下が明るく上ほど薄い、縦に流れるすじ模様。
Shader "FuwaCourse/WarpVeil"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.9, 1, 1)
        _Fill ("Fill (0-1)", Range(0, 1)) = 0
        _Alpha ("Max Alpha", Range(0, 1)) = 0.5
        _Rise ("Rise Width (fill)", Range(0.05, 1)) = 0.3
        _NearFade ("Near Fade Distance", Float) = 0.25
        _Start ("Start Angle (0-1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            float _Fill, _Alpha, _Rise, _NearFade, _Start;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float camDist : TEXCOORD1; float3 wn : TEXCOORD2; float3 wv : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.camDist = length(UnityObjectToViewPos(v.vertex));
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wv = WorldSpaceViewDir(v.vertex);
                return o;
            }

            // 1本の柱（角度 x）の明るさ。x は始まりからの角度（一周先の分も x+1 で呼ぶ）
            static const float W = 0.12;   // 始まりの端をぼかす幅
            float Column(float x, float h, float F, float done, inout float bodyMax)
            {
                if (x > F) return 0;
                float top = lerp(saturate((F - x) / _Rise), 1, done);   // 先頭は足元、後ろほど高い。最後は全部てっぺんまで
                if (top <= 0.001) return 0;
                float t = saturate(h / top);
                float body = (0.55 + 0.45 * (1 - t)) * (1 - smoothstep(0.5, 1.0, t));   // 目の高さくらいまでしっかり、上でやわらかく消える
                float base = exp(-h * 22) * 0.6 * saturate(top * 4);                     // 足元のふちは少し強く
                bodyMax = max(bodyMax, body + base);
                return (body + base) * smoothstep(0.0, W, x);             // 始まりの端はぼかす
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Fill <= 0.001) discard;
                float ang = frac(i.uv.x - _Start + 1), h = i.uv.y;   // 乗った時に向いていた方向から始める
                float done = smoothstep(0.9, 1.0, _Fill);
                // 先頭は一周＋ぼかし幅まで進む。一周先の分(ang+1)を重ねて、始まりのぼかしと先頭がなめらかにつながる
                float F = _Fill * (1 + W);
                float bodyMax = 0;
                float v = max(Column(ang, h, F, done, bodyMax), Column(ang + 1, h, F, done, bodyMax));
                if (v <= 0.0005) discard;
                // 連続した光の膜：ゆっくり上へ流れるやわらかいゆらぎ（すじは出さない）。角度は整数周期で一周つながる
                float tt = _Time.y;
                float flow = 0.88 + 0.12 * sin(h * 5.0 - tt * 1.4 + sin(ang * 6.2831853 * 2 + tt * 0.3) * 1.2);
                float sway = 0.92 + 0.08 * sin(ang * 6.2831853 * 3 - tt * 0.5 + h * 1.5);
                // 膜を斜めに見る所ほど明るい（面っぽく見える）
                float ndv = abs(dot(normalize(i.wn), normalize(i.wv)));
                float rim = 0.75 + 0.6 * pow(1 - ndv, 2);
                float streak = flow * sway * rim, shimmer = 1;
                float a = v * streak * shimmer * _Alpha;
                a *= 1 + done * 0.8;                                   // 満タンでふわっと明るく
                a *= smoothstep(_NearFade * 0.4, _NearFade, i.camDist);  // 目の前の幕は消す
                // 明るい空の前でも見えるよう、普通の半透明で少し色をのせる。濃い所ほど白っぽく
                float3 col = lerp(_Color.rgb, 1, saturate(bodyMax * 0.2 + done * 0.3));
                return fixed4(col, saturate(a));
            }
            ENDCG
        }
    }
}
