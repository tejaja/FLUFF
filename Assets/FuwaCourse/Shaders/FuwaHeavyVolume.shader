// 重くなるエリアの紫の箱。両面描くので、中に入っても視界が紫になる。
// 上は薄く下ほど濃いグラデーション＋下へ流れる帯（下に引っぱられる感じ）。
Shader "Fuwa/HeavyVolume"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 0.2, 0.75, 1)
        _TopAlpha ("上の濃さ", Range(0,1)) = 0.05
        _BottomAlpha ("下の濃さ", Range(0,1)) = 0.4
        _BandAlpha ("流れる帯の強さ", Range(0,1)) = 0.12
        _BandCount ("帯の数（高さあたり）", Float) = 3
        _BandSpeed ("帯が下へ流れる速さ", Float) = 0.6
        _ClipBottom ("底を描かない（ドーム用。球では0）", Float) = 1
    }
    SubShader
    {
        // 他の半透明（中の落ちる粒・床のグラデ）より必ず後に描く。
        // 見る角度で描く順番が入れ替わるとチラつくため、順番を固定する
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // 1回目：奥側の面（内側から見える面）
        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _TopAlpha, _BottomAlpha, _BandAlpha, _BandCount, _BandSpeed, _ClipBottom;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float h : TEXCOORD0;   // 0=下 1=上（箱の中の高さ）
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.h = saturate(v.vertex.y + 0.5);   // Unityの立方体は-0.5〜0.5
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 箱の底面は道の表面とちょうど同じ高さでちらつくので描かない
                if (_ClipBottom > 0.5) clip(i.h - 0.01);   // ドームの底面は道と重なるので描かない（球は全部描く）
                float a = lerp(_BottomAlpha, _TopAlpha, i.h);
                // 下へ流れる帯：下側がくっきり、上へなだらかに消える
                float p = frac(i.h * _BandCount + _Time.y * _BandSpeed);
                float band = pow(p, 3.0) * smoothstep(1.0, 0.92, p);
                a += _BandAlpha * band * (1.0 - i.h * 0.5);
                fixed4 c = fixed4(_Color.rgb, saturate(a));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        // 2回目：手前側の面
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _TopAlpha, _BottomAlpha, _BandAlpha, _BandCount, _BandSpeed, _ClipBottom;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float h : TEXCOORD0;   // 0=下 1=上（箱の中の高さ）
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.h = saturate(v.vertex.y + 0.5);   // Unityの立方体は-0.5〜0.5
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 箱の底面は道の表面とちょうど同じ高さでちらつくので描かない
                if (_ClipBottom > 0.5) clip(i.h - 0.01);   // ドームの底面は道と重なるので描かない（球は全部描く）
                float a = lerp(_BottomAlpha, _TopAlpha, i.h);
                // 下へ流れる帯：下側がくっきり、上へなだらかに消える
                float p = frac(i.h * _BandCount + _Time.y * _BandSpeed);
                float band = pow(p, 3.0) * smoothstep(1.0, 0.92, p);
                a += _BandAlpha * band * (1.0 - i.h * 0.5);
                fixed4 c = fixed4(_Color.rgb, saturate(a));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
