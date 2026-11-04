// 火山の岩の通り道の点線（2026-11-04 てじゃ「この点線いいな、予告線みたいな色で置いて、岩が出たら奥から手前まで順番に点灯するのを繰り返す」）。
// 点は 1 つのメッシュ。uv.x が通り道ぞいの距離(m)。ふだんはうっすら、FuwaLavaRock が _Head（光の先頭の距離）を動かすと、そこから後ろ _Tail m が光る
Shader "FuwaCourse/RockPathDots"
{
    Properties
    {
        _Color ("ふだんの色", Color) = (1, 0.82, 0.1, 1)
        _Dim ("ふだんの濃さ", Range(0,1)) = 0.35
        _LitColor ("光った色", Color) = (1, 0.95, 0.55, 1)
        _Head ("光の先頭(m)。スクリプトが動かす", Float) = -100
        _Tail ("光の長さ(m)", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On   // キュー 2005 で描くので、空（スカイボックス）に上書きされないよう深さを書く
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _LitColor;
            half _Dim;
            float _Head, _Tail;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float d : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.d = v.uv.x;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // 先頭がいちばん明るく、後ろへなだらかに戻る
                float x = _Head - i.d;
                half lit = (x >= 0 && x < _Tail) ? 1 - x / max(0.01, _Tail) : 0;
                lit = lit * lit * (3 - 2 * lit);
                fixed4 c = fixed4(lerp(_Color.rgb, _LitColor.rgb, lit), lerp(_Dim, 1, lit));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
