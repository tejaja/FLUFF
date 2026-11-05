Shader "Hidden/SuzuFactory/Hikari/MaterialProbeCoverage"
{
    Properties
    {
        _HikariUvRemap ("Uv Remap", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        Pass
        {
            Cull Off
            ZTest Always
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _HikariUvRemap;
            struct appdata
            {
                float2 uv0 : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            v2f vert(appdata v)
            {
                v2f o;
                float2 remapped = v.uv0 * _HikariUvRemap.xy + _HikariUvRemap.zw;
                o.pos = UnityObjectToClipPos(float4(remapped, 0.0, 1.0));
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(1.0, 1.0, 1.0, 1.0);
            }
            ENDCG
        }
    }
}
