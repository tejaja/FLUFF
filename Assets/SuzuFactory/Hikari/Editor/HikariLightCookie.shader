Shader "Hidden/SuzuFactory/Hikari/LightCookie"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        CGINCLUDE
        #include "UnityCG.cginc"

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }
        ENDCG

        Pass
        {
            Name "COPY2D"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            sampler2D _MainTex;
            float _HikariFlipV;

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if (_HikariFlipV > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }
                return float4(tex2D(_MainTex, uv).rgb, 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "CUBETOEQUIRECT"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            samplerCUBE _HikariCookieCube;

            float4 frag(v2f i) : SV_Target
            {
                float theta = i.uv.y * UNITY_PI;
                float phi = i.uv.x * 2.0 * UNITY_PI;
                float sin_theta = sin(theta);
                float3 direction = float3(
                    sin_theta * sin(phi),
                    -cos(theta),
                    sin_theta * cos(phi));
                return float4(texCUBElod(_HikariCookieCube, float4(direction, 0.0)).rgb, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
