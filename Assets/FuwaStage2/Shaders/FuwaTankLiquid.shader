// 2–4 回るタンクの中の液体（2026-11-10 てじゃ「中身を液体っぽくしたい」）。
// 回る液は上の面がすり鉢（放物面）になる → メッシュの形。色は物の中の角度と半径で、うずまきのしま（タンクと一緒に回り、さらに _Swirl で先へ回る）
// 上の面はふちほど明るく、少しつやを足す。不透明（ガラスはあとで重ねて描く）
Shader "FuwaCourse/TankLiquid"
{
    Properties
    {
        _Color ("液の色", Color) = (1, 0.55, 0.75, 1)
        _Deep ("奥・下の色", Color) = (0.8, 0.3, 0.55, 1)
        _Stripe ("うずのしまの明るさ", Range(0,1)) = 0.35
        _Arms ("うずの本数", Float) = 3
        _Twist ("うずのねじれ（中ほど回る）", Float) = 9
        _Swirl ("うずが回る速さ（回/秒、タンクの回りに足す）", Float) = 0.35
        _Gloss ("つや", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color, _Deep, _LightColor0; half _Stripe, _Arms, _Twist, _Swirl, _Gloss;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 op : TEXCOORD0; float3 wn : TEXCOORD1; float3 wv : TEXCOORD2; float3 on : TEXCOORD3; UNITY_FOG_COORDS(4) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.op = v.vertex.xyz; o.on = v.normal;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wv = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = length(i.op.xz), ang = atan2(i.op.z, i.op.x);
                float top = saturate(i.on.y * 2);   // 上の面
                // うず：上の面は半径でねじれる、横の面は高さでななめのしま
                float ph = ang * _Arms + lerp(i.op.y * 6, -r * _Twist, top) + _Time.y * _Swirl * 6.2832 * _Arms;
                half st = smoothstep(0.2, 0.9, sin(ph) * 0.5 + 0.5) * _Stripe;
                float3 n = normalize(i.wn), vd = normalize(i.wv), l = normalize(_WorldSpaceLightPos0.xyz);
                half lam = saturate(dot(n, l)) * 0.4 + 0.6;
                half3 c = lerp(_Deep.rgb, _Color.rgb, saturate(0.45 + top * 0.55));
                c = lerp(c, half3(1, 1, 1), st) * lam;
                half f = pow(1 - saturate(dot(n, vd)), 3) * top;
                half sp = pow(saturate(dot(n, normalize(l + vd))), 40) * _Gloss;
                c += f * 0.35 + sp;
                half4 col = half4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
