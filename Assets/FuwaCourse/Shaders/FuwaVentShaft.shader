// 噴気孔の縦穴の内側：FuwaVentHoleMask が印をつけた所だけに描く。
// uv.y = 深さ（0=穴の口、1=底）。口の近くは岩の色、奥ほど暗く。面ごとの向きで少し明暗をつけて、ごつごつした岩肌に見せる
Shader "FuwaCourse/VentShaft"
{
    Properties
    {
        _Top ("口の近くの色", Color) = (0.42, 0.37, 0.47, 1)
        _Bottom ("奥の色", Color) = (0.03, 0.02, 0.06, 1)
        _Falloff ("暗くなる速さ", Float) = 1.6
        _Facet ("面の明暗の強さ", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+11" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Pass
        {
            Stencil { Ref 37 Comp Equal }
            ZWrite On
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Top, _Bottom; float _Falloff, _Facet;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float depth : TEXCOORD0; float shade : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = v.uv.y;
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                // 上から光が入る：上を向いた面（底や段）と、斜め上からの光に向いた面が少し明るい
                float l = saturate(dot(n, normalize(float3(0.35, 0.8, 0.45)))) ;
                o.shade = lerp(1.0 - _Facet, 1.0, l);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float k = 1.0 - exp(-_Falloff * saturate(i.depth) * 2.2);
                fixed4 c = lerp(_Top, _Bottom, k);
                c.rgb *= lerp(i.shade, 1.0, k * 0.8);   // 奥は暗いので明暗も目立たなく
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
