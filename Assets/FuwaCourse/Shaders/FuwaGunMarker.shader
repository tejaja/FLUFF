// 銃の台の上に浮かぶ▼。上下にふわふわ動きながら、ゆっくり回る（動きは全部シェーダーの中で、スクリプトは表示のON/OFFだけ）。
// オブジェクトの原点が回転の中心。
Shader "FuwaCourse/GunMarker"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.94, 1, 1)
        _Emission ("Emission", Range(0, 2)) = 0.55
        _BobHeight ("Bob Height", Float) = 0.05
        _BobSpeed ("Bob Speed", Float) = 2.6
        _SpinSpeed ("Spin Speed (rad/s)", Float) = 1.2
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "DisableBatching"="True" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Emission;
            float _BobHeight, _BobSpeed, _SpinSpeed;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                float a = _Time.y * _SpinSpeed;
                float s = sin(a), c = cos(a);
                float3 p = v.vertex.xyz;
                p.xz = float2(p.x * c - p.z * s, p.x * s + p.z * c);
                float3 nn = v.normal;
                nn.xz = float2(nn.x * c - nn.z * s, nn.x * s + nn.z * c);
                p.y += sin(_Time.y * _BobSpeed) * _BobHeight;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.n = UnityObjectToWorldNormal(nn);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 面ごとに少し明るさを変えて、立体に見えるように
                float3 n = normalize(i.n);
                half shade = 0.62 + 0.38 * saturate(dot(n, normalize(float3(0.4, 0.8, 0.3))) * 0.5 + 0.5);
                half3 col = _Color.rgb * (shade + _Emission);
                fixed4 r = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, r);
                return r;
            }
            ENDCG
        }
    }
}
