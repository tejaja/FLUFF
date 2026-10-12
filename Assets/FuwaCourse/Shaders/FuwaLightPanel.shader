// チュートリアルの区切りの壁の見た目：シンプルな「光の板」（2026-10-12 てじゃ「シンプルに光の板でもいいかも」）
// ・下（床ぎわ）がいちばん明るく、上へいくほどすーっと消える。下のふちと左右のはしに細い光の線
// ・ゆっくり上へ流れるうすい光の帯（動いてるのがかすかに分かる程度）
// ・見ている人から遠いと消える（_FadeNear〜_FadeFar m）
// 大きさ（m）で計算するので、まとめ描き（バッチング）は禁止
Shader "FuwaCourse/LightPanel"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.85, 1, 1)
        _Alpha ("板の濃さ（床ぎわ）", Range(0, 1)) = 0.35
        _Height ("光が消える高さ (m)", Float) = 2.2
        _LineAlpha ("ふちの線の濃さ", Range(0, 1)) = 0.8
        _LineWidth ("ふちの線の太さ (m)", Float) = 0.035
        _SideHeight ("左右の線が消える高さ (m)", Float) = 1.6
        _BandAlpha ("流れる光の帯の濃さ", Range(0, 1)) = 0.12
        _BandSpeed ("帯の速さ (m/秒)", Float) = 0.35
        _BandGap ("帯の間隔 (m)", Float) = 1.1
        _FadeNear ("この距離から消えはじめる (m)", Float) = 14
        _FadeFar ("この距離で消える (m)", Float) = 24
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        Blend SrcAlpha One   // 光なので足し算（後ろが明るくなる）
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
            float _Alpha, _Height, _LineAlpha, _LineWidth, _SideHeight, _BandAlpha, _BandSpeed, _BandGap, _FadeNear, _FadeFar;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 sx = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                float2 m = float2(min(i.uv.x, 1 - i.uv.x) * sx.x, i.uv.y * sx.y);   // 左右のはし・床からの距離（m）

                // 板：床ぎわが明るく、上へなめらかに消える。左右のはしも少しぼかす
                float h = saturate(m.y / _Height);
                float body = _Alpha * pow(1 - h, 2.2) * smoothstep(0, 0.25, m.x);

                // 流れる光の帯（下から上へ）
                float bp = frac((m.y - _Time.y * _BandSpeed) / _BandGap);
                float band = _BandAlpha * smoothstep(0.0, 0.5, bp) * smoothstep(1.0, 0.5, bp) * (1 - h);
                band *= smoothstep(0, 0.25, m.x);

                // ふちの線（下と左右。左右は上へいくほど消える）
                float w = max(_LineWidth, fwidth(m.y) * 1.5);
                float wx = max(_LineWidth, fwidth(m.x) * 1.5);
                float bottom = 1 - smoothstep(w * 0.4, w, m.y);
                float side = (1 - smoothstep(wx * 0.4, wx, m.x)) * (1 - smoothstep(_SideHeight * 0.3, _SideHeight, m.y));
                float ln = _LineAlpha * max(bottom, side);

                float a = saturate(body + band + ln);
                float camD = distance(_WorldSpaceCameraPos, i.wpos);
                a *= 1 - smoothstep(_FadeNear, _FadeFar, camD);
                if (a <= 0.002) discard;
                // 線はちょっと白っぽく
                float3 col = lerp(_Color.rgb, 1, saturate(ln) * 0.5);
                fixed4 c = fixed4(col, a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));   // 足し算なので霧の中では黒（＝足さない）へ
                return c;
            }
            ENDCG
        }
    }
}
