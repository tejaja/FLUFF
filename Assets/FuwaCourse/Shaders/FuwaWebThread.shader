// クモの巣の支えの糸。細い円柱だと遠くで1ピクセル未満になって点線みたいにぶつ切れるので、
// 画面に向いた帯として描いて、最低1ピクセルの太さを保ち、細すぎるぶんは薄さ（アルファ）で表す。
// メッシュは糸1本につき頂点4つ：position = この端、uv0 = (横 -1/+1, 端 0/1, 太さm)、uv1 = 反対側の端。
Shader "FuwaCourse/WebThread"
{
    Properties
    {
        _Color ("Color", Color) = (0.42, 0.40, 0.52, 0.94)
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
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
                float4 other : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 e : TEXCOORD0; // x: distance from center (px), y: half width (px), z: fade
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 ca = UnityObjectToClipPos(v.vertex);
                float4 cb = UnityObjectToClipPos(float4(v.other.xyz, 1));
                float2 hs = _ScreenParams.xy * 0.5;
                float2 sa = ca.xy / max(ca.w, 1e-4) * hs;
                float2 sb = cb.xy / max(cb.w, 1e-4) * hs;
                float2 d = v.uv.y < 0.5 ? sb - sa : sa - sb; // always A -> B so both ends agree on the side
                d = length(d) > 1e-5 ? normalize(d) : float2(1, 0);
                float2 perp = float2(-d.y, d.x);
                float pixW = v.uv.z * abs(UNITY_MATRIX_P[1][1]) * hs.y / max(ca.w, 1e-4);
                float drawW = max(pixW, 1.0);
                float ext = drawW * 0.5 + 1.0; // +1px for smoothing
                ca.xy += perp * (v.uv.x * ext) / hs * ca.w;
                o.pos = ca;
                o.e = float3(v.uv.x * ext, drawW * 0.5, pixW / drawW);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float a = saturate(i.e.y + 0.5 - abs(i.e.x)) * i.e.z;
                return fixed4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
}
