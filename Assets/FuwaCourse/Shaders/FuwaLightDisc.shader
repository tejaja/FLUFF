// チュートリアルの課題の壁：大きな円の「光の板」（2026-11-25 てじゃ「壁、デカイ円にしよう」「コース下まで貫通してて」）
// FuwaLightPanel の円版。メッシュの uv0 = 道の上面・扉のまん中からの位置 (m)、uv1 = (円の半径 m, 円の中心の高さ m)
// ・道の高さがいちばん明るく、上にも下にもすーっと消える。円のふちに細い光の線
// ・ゆっくり流れるうすい光の帯（道から上へ・下へ）
// ・見ている人から遠いと消える（_FadeNear〜_FadeFar m）
Shader "FuwaCourse/LightDisc"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.85, 1, 1)
        _Alpha ("板の濃さ（道の高さ）", Range(0, 1)) = 0.6
        _Height ("光が消える高さ・上 (m)", Float) = 3.6
        _Depth ("光が消える深さ・下 (m)", Float) = 2.4
        _LineAlpha ("ふちの線の濃さ", Range(0, 1)) = 0.8
        _LineWidth ("ふちの線の太さ (m)", Float) = 0.05
        _LineFar ("ふちの線の、道から遠い所の濃さ（倍）", Range(0, 1)) = 0.45
        _BandAlpha ("流れる光の帯の濃さ", Range(0, 1)) = 0.12
        _BandSpeed ("帯の速さ (m/秒)", Float) = 0.35
        _BandGap ("帯の間隔 (m)", Float) = 1.1
        _FadeNear ("この距離から消えはじめる (m)", Float) = 14
        _FadeFar ("この距離で消える (m)", Float) = 24
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One   // 光なので足し算
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Alpha, _Height, _Depth, _LineAlpha, _LineWidth, _LineFar, _BandAlpha, _BandSpeed, _BandGap, _FadeNear, _FadeFar;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 circ : TEXCOORD1; float3 wpos : TEXCOORD2; UNITY_FOG_COORDS(3) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.circ = v.uv1;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float y = i.uv.y;                                   // 道の上面からの高さ (m)、下はマイナス
                float rim = i.circ.x - length(i.uv - float2(0, i.circ.y));   // 円のふちまでの距離 (m)

                // 板：道の高さが明るく、上・下へなめらかに消える。ふちの近くは少しぼかす
                float h = y >= 0 ? saturate(y / _Height) : saturate(-y / _Depth);
                float body = _Alpha * pow(1 - h, 2.2) * smoothstep(0, 0.35, rim);

                // 流れる光の帯（道から上へ・下へ）
                float bp = frac((abs(y) - _Time.y * _BandSpeed) / _BandGap);
                float band = _BandAlpha * smoothstep(0.0, 0.5, bp) * smoothstep(1.0, 0.5, bp) * (1 - h) * smoothstep(0, 0.35, rim);

                // 円のふちの線（道から遠いほど少しうすく）
                float w = max(_LineWidth, fwidth(rim) * 1.5);
                float ln = _LineAlpha * (1 - smoothstep(w * 0.4, w, rim)) * lerp(1, _LineFar, saturate(abs(y - 0) / max(0.01, i.circ.x)));

                float a = saturate(body + band + ln);
                float camD = distance(_WorldSpaceCameraPos, i.wpos);
                a *= 1 - smoothstep(_FadeNear, _FadeFar, camD);
                if (a <= 0.002) discard;
                float3 col = lerp(_Color.rgb, 1, saturate(ln) * 0.5);
                fixed4 c = fixed4(col, a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
}
