Shader "FuwaCourse/DandelionTuft"
{
    // たんぽぽの綿毛の冠毛：いつもカメラを向く、放射状の細い毛の半透明な板（ビルボード）。
    // メッシュは冠毛1つにつき4頂点で、4つとも冠毛の中心に置き、uv=角(-1〜1)・uv2.x=大きさ(m)・uv2.y=回転 で広げる。
    // 風のゆれは綿毛の毛玉（FuwaCourse/Fur）・茎（InkCap）と同じ式（uv3.x＝重み、uv3.y＝位相）
    Properties
    {
        _MainTex ("Tuft", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 2)) = 1
        _SwayAmp ("Sway (m, 0=off)", Float) = 0
        _SwaySpeed ("Sway Speed", Float) = 1.2
        _SwayDir ("Sway Direction (world)", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Opacity;
            float _SwayAmp, _SwaySpeed; float4 _SwayDir;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float4 uv3 : TEXCOORD3; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 wn : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 world = mul(unity_ObjectToWorld, v.vertex);
                if (_SwayAmp > 0 && v.uv3.x > 0)
                {
                    float t = _Time.y * _SwaySpeed + v.uv3.y;
                    float s = sin(t) * 0.65 + sin(t * 1.73 + 1.1) * 0.35;
                    float3 dw = normalize(_SwayDir.xyz) * (0.35 + 0.65 * (s * 0.5 + 0.5)) + cross(float3(0, 1, 0), normalize(_SwayDir.xyz)) * sin(t * 0.83 + 2.0) * 0.3;
                    world.xyz += dw * _SwayAmp * v.uv3.x;
                }
                // カメラを向く板に広げる（ビュー空間で。VRの左右の目でも同じ形になるよう、回転は uv2.y で決める）
                float4 view = mul(UNITY_MATRIX_V, world);
                float c = cos(v.uv2.y), s2 = sin(v.uv2.y);
                float2 corner = float2(v.uv.x * c - v.uv.y * s2, v.uv.x * s2 + v.uv.y * c);
                view.xy += corner * v.uv2.x;
                o.pos = mul(UNITY_MATRIX_P, view);
                o.uv = v.uv * 0.5 + 0.5;
                o.color = v.color;
                o.wn = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // 冠毛の向き（玉の外向き）で、日の当たる側を少し明るく。裏側も白っぽいまま（光が透ける綿毛）
                float ndl = saturate(dot(normalize(i.wn), normalize(_WorldSpaceLightPos0.xyz)) * 0.35 + 0.65);
                float3 col = _Color.rgb * i.color.rgb * (ndl * _LightColor0.rgb + ShadeSH9(float4(normalize(i.wn), 1)));
                fixed4 o = fixed4(col, saturate(tex.a * _Opacity * i.color.a));
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
}
