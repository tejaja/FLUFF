// 2026-11-16 てじゃ「真ん中の台座のれちゃうのと、真ん中つっきれちゃうのが問題」「台座の外周から真上にうっすらバリアみたいな円柱のバリア」
//  広場のまん中の円柱のバリア（ふたなしの筒）。物体の座標の uv：u＝まわりの長さ(m)、v＝高さ(m)
//  ・下のふちが少し濃く、上へいくほど消える（うっすら）
//  ・横から見たふち（筒の左右のはし）が少し濃い（筒っぽく見える）
//  ・六角形のふちがかすかに光る＋下から上へ光の波がゆっくり流れる
Shader "Fuwa/PillarBarrier"
{
    Properties
    {
        _Color ("色", Color) = (0.45, 0.9, 1, 1)
        _Alpha ("濃さ", Range(0, 1)) = 0.35
        _Cell ("六角形の大きさ（m）", Float) = 0.5
        _Height ("高さ（m、上で消える）", Float) = 6
        _Fade ("上で消えはじめる割合", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color; half _Alpha; float _Cell, _Height, _Fade;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float rim : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv;
                float3 wn = normalize(UnityObjectToWorldNormal(v.normal));
                float3 vd = normalize(WorldSpaceViewDir(v.vertex));
                o.rim = 1 - abs(dot(wn, vd));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            // 六角形のふちまでのきょり（0＝ふち）
            float HexEdge(float2 p)
            {
                const float2 s = float2(1, 1.7320508);
                float2 h = s * 0.5;
                float2 a = fmod(abs(p), s) - h, b = fmod(abs(p - h), s) - h;
                float2 g = dot(a, a) < dot(b, b) ? a : b;
                g = abs(g);
                return 0.5 - max(dot(g, normalize(s)), g.x);
            }
            half4 frag (v2f i) : SV_Target
            {
                float y = i.uv.y / max(_Height, 0.01);
                float top = 1 - smoothstep(1 - _Fade, 1, y);
                float bottom = 0.45 + 0.55 * exp(-i.uv.y * 1.6);
                float t = _Time.y;
                float e = HexEdge(i.uv / _Cell);
                float hl = exp(-e * e * 400);
                float wave = 0.5 + 0.5 * sin(i.uv.y * 1.3 - t * 1.6);
                float rim = pow(saturate(i.rim), 2.5);
                // 2026-11-16 てじゃ「模様なくていいかも」→ 六角もようと波はなし（下のふちと横のふちが少し濃いだけ）
                //  → てじゃ「軽くグラデが上昇アニメしてるかんじに」：ゆるい明るい帯が下から上へゆっくり流れる
                float rise = frac(i.uv.y / 2.6 - t * 0.35);
                float band = smoothstep(0.0, 0.55, rise) * (1 - smoothstep(0.55, 1.0, rise));
                float a = _Alpha * top * (bottom * 0.4 + rim * 0.6 + band * 0.35);
                a = saturate(a);
                half3 col = _Color.rgb;
                half4 c = half4(col * a, a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, half4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
