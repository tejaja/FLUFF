// 岩にあけた穴の内側：入口は岩の影の色、奥へ行くほど真っ暗に（ライトなし・軽い）。
// _MouthPos（入口の中心）から _InDir（奥へ向かう向き）にどれだけ入ったかで暗くする
Shader "FuwaCourse/CaveInner"
{
    Properties
    {
        _MouthColor ("Mouth Color", Color) = (0.36, 0.30, 0.46, 1)
        _DeepColor ("Deep Color", Color) = (0.03, 0.02, 0.06, 1)
        _MouthPos ("Mouth Position (world)", Vector) = (0, 0, 0, 0)
        _InDir ("Inward Direction (world)", Vector) = (0, 0, 1, 0)
        _Depth ("Depth to Dark", Float) = 2.2
        _FacetShade ("面の向きによる明暗の強さ", Range(0, 0.6)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _MouthColor, _DeepColor; float4 _MouthPos, _InDir; float _Depth, _FacetShade;
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 wn : TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert (appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; o.wn = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float d = dot(i.wp - _MouthPos.xyz, normalize(_InDir.xyz));
                float k = smoothstep(-0.3, _Depth, d);
                // 面の向きで明暗（のっぺり防止）：上向き＋入口の方を向いた面ほど明るく、でこぼこが見えるように
                float3 n = normalize(i.wn);
                float3 lightDir = normalize(float3(0, 0.8, 0) - normalize(_InDir.xyz));
                float shade = lerp(1.0 - _FacetShade, 1.0 + _FacetShade * 0.4, saturate(dot(n, lightDir) * 0.5 + 0.5));
                fixed4 c = lerp(_MouthColor, _DeepColor, k) * shade;
                c.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
